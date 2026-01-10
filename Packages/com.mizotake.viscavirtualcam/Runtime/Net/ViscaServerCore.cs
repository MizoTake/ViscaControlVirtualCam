using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Configuration for ViscaServerCore.
    /// </summary>
    public sealed class ViscaServerOptions
    {
        public ViscaTransport Transport = ViscaTransport.UdpRawVisca;
        public int UdpPort = ViscaProtocol.DefaultUdpPort;
        public int TcpPort = ViscaProtocol.DefaultTcpPort;
        public int MaxClients = 4;
        public bool VerboseLog = true;
        public bool LogReceivedCommands = true;
        public Action<string> Logger = null;
        public int MaxFrameSize = 4096;
    }

    /// <summary>
    /// Pure C# VISCA server core. No Unity/MonoBehaviour dependencies.
    /// </summary>
    public sealed class ViscaServerCore : IDisposable
    {
        private readonly IViscaCommandHandler _handler;
        private readonly ViscaServerOptions _opt;
        private readonly ViscaCommandRegistry _commandRegistry;
        private UdpClient _udp;
        private TcpListener _tcp;
        private CancellationTokenSource _cts;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TcpClient, ViscaFrameFramer> _clients = new();
        private int _clientCount = 0;

        private readonly struct ViscaIpEnvelope
        {
            public readonly byte TypeMsb;
            public readonly byte TypeLsb;
            public readonly ushort PayloadLength;
            public readonly uint Sequence;

            public ViscaIpEnvelope(byte typeMsb, byte typeLsb, ushort payloadLength, uint sequence)
            {
                TypeMsb = typeMsb;
                TypeLsb = typeLsb;
                PayloadLength = payloadLength;
                Sequence = sequence;
            }
        }

        // Reusable empty responder to avoid allocations during command name lookup
        private static readonly Action<byte[]> EmptyResponder = _ => { };

        public ViscaServerCore(IViscaCommandHandler handler, ViscaServerOptions options)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _opt = options ?? new ViscaServerOptions();
            _commandRegistry = new ViscaCommandRegistry();
        }

        /// <summary>
        /// Access to the command registry for custom command registration
        /// </summary>
        public ViscaCommandRegistry CommandRegistry => _commandRegistry;

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            if (_opt.Transport == ViscaTransport.UdpRawVisca || _opt.Transport == ViscaTransport.Both)
                StartUdp();
            if (_opt.Transport == ViscaTransport.TcpRawVisca || _opt.Transport == ViscaTransport.Both)
                StartTcp();
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch (Exception e) { Log($"Stop cancel error: {e.Message}"); }
            try { _udp?.Close(); } catch (Exception e) { Log($"UDP close error: {e.Message}"); }
            try { _tcp?.Stop(); } catch (Exception e) { Log($"TCP stop error: {e.Message}"); }
            _udp = null;
            _tcp = null;
            _cts = null;
            foreach (var kv in _clients)
            {
                try { kv.Key.Close(); }
                catch (Exception e) { Log($"TCP client close error: {e.Message}"); }
            }
            _clients.Clear();
            System.Threading.Interlocked.Exchange(ref _clientCount, 0);
        }

        private void StartUdp()
        {
            _udp = new UdpClient(_opt.UdpPort);
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            try
            {
                _udp.BeginReceive(UdpReceiveCallback, null);
            }
            catch (Exception e)
            {
                Log($"UDP begin receive error: {e.Message}");
                throw;
            }
            Log($"VISCA UDP server started on {_opt.UdpPort}");
        }

        private void StartTcp()
        {
            _tcp = new TcpListener(IPAddress.Any, _opt.TcpPort);
            _tcp.Start();
            ThreadPool.QueueUserWorkItem(_ => AcceptLoopTcp());
            Log($"VISCA TCP server started on {_opt.TcpPort}");
        }

        private void UdpReceiveCallback(IAsyncResult ar)
        {
            // Snapshot to avoid race with Stop()
            var udp = _udp;
            if (udp == null) return;

            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = udp.EndReceive(ar, ref remote);
            }
            catch (ObjectDisposedException)
            {
                // Socket was disposed while awaiting receive; exit quietly
                return;
            }
            catch (Exception e)
            {
                // Only log if still active to avoid noise during shutdown
                if (_udp != null) Log($"UDP receive error: {e.Message}");
                try { _udp?.BeginReceive(UdpReceiveCallback, null); }
                catch (Exception ex)
                {
                    Log($"UDP begin receive error: {ex.Message}");
                }
                return;
            }

            try { _udp?.BeginReceive(UdpReceiveCallback, null); }
            catch (Exception e)
            {
                Log($"UDP begin receive error: {e.Message}");
            }

            Action<byte[]> send = (bytes) =>
            {
                try
                {
                    var u = _udp;
                    if (u != null) u.Send(bytes, bytes.Length, remote);
                }
                catch (Exception e)
                {
                    Log($"UDP send error: {e.Message}");
                }
            };
            ProcessFrame(data, send);
        }

        private void AcceptLoopTcp()
        {
            var token = _cts;
            while (token != null && !token.IsCancellationRequested)
            {
                var listener = _tcp;
                if (listener == null) break;
                try
                {
                    var client = listener.AcceptTcpClient();
                    if (System.Threading.Interlocked.Increment(ref _clientCount) > _opt.MaxClients)
                    {
                        Log("TCP client refused: max clients reached");
                        System.Threading.Interlocked.Decrement(ref _clientCount);
                        try { client.Close(); } catch { }
                        continue;
                    }
                    client.NoDelay = true;
                    _clients[client] = new ViscaFrameFramer(_opt.MaxFrameSize);
            ThreadPool.QueueUserWorkItem(_ => ClientLoopTcp(client));
        }
                catch (SocketException)
                {
                    if (_tcp == null) break; // shutting down
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception e)
                {
                    if (_tcp != null) Log($"TCP accept error: {e.Message}");
                }
            }
        }

        private void ClientLoopTcp(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                Action<byte[]> send = (bytes) =>
                {
                    try
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    catch (Exception e)
                    {
                        Log($"TCP send error: {e.Message}");
                    }
                };
                while (_cts != null && !_cts.IsCancellationRequested && client.Connected)
                {
                    int n; try { n = stream.Read(buffer, 0, buffer.Length); } catch { break; }
                    if (n <= 0) break;
                    if (_clients.TryGetValue(client, out var framer))
                    {
                        framer.Append(new ReadOnlySpan<byte>(buffer, 0, n), frame =>
                        {
                            if (frame.Length > _opt.MaxFrameSize)
                            {
                                Log($"TCP frame too large: {frame.Length}");
                                return; // drop
                            }
                            ProcessFrame(frame, send);
                        });
                    }
                    else
                    {
                        // No framer state; drop
                    }
                }
                _clients.TryRemove(client, out _);
                System.Threading.Interlocked.Decrement(ref _clientCount);
            }
        }

        private void ProcessFrame(byte[] packet, Action<byte[]> rawSend)
        {
            if (packet == null || packet.Length == 0) return;

            byte[] frame = packet;
            Action<byte[]> responder = rawSend;

            if (TryParseViscaIpEnvelope(packet, out var envelope, out var payload, out var headerError))
            {
                responder = WrapResponderWithViscaIpHeader(rawSend, envelope);
                frame = payload;

                if (!string.IsNullOrEmpty(headerError))
                {
                    Log(headerError);
                    _handler.HandleError(frame, responder, ViscaProtocol.ErrorMessageLength);
                    return;
                }
            }

            // Validate frame
            if (frame.Length < ViscaProtocol.MinFrameLength)
            {
                Log($"Frame too short: {frame.Length} bytes");
                _handler.HandleError(frame, responder, ViscaProtocol.ErrorMessageLength);
                return;
            }

            if (frame[^1] != ViscaProtocol.FrameTerminator)
            {
                Log($"Invalid VISCA frame (no terminator): {BitConverter.ToString(frame)}");
                _handler.HandleError(frame, responder, ViscaProtocol.ErrorSyntax);
                return;
            }

            if (frame.Length > _opt.MaxFrameSize)
            {
                Log($"Frame too large: {frame.Length} bytes");
                _handler.HandleError(frame, responder, ViscaProtocol.ErrorMessageLength);
                return;
            }

            // Log received command (only generate details string when logging enabled)
            if (_opt.LogReceivedCommands)
            {
                string details = _commandRegistry.GetCommandDetails(frame, responder);
                Log($"RX: {details}");
            }

            // Try to execute command through registry (O(1) lookup for most commands)
            var context = _commandRegistry.TryExecute(frame, _handler, responder);
            if (context.HasValue)
            {
                return;
            }

            // Unknown command - send error response
            string cmdName = _commandRegistry.GetCommandName(frame);
            Log($"WARNING: Unknown command: {cmdName}");
            _handler.HandleError(frame, responder, ViscaProtocol.ErrorSyntax);
        }

        private bool TryParseViscaIpEnvelope(byte[] packet, out ViscaIpEnvelope envelope, out byte[] payload, out string error)
        {
            envelope = default;
            payload = packet;
            error = null;

            if (packet == null || packet.Length < ViscaProtocol.ViscaIpHeaderLength)
                return false;

            byte typeMsb = packet[0];
            byte typeLsb = packet[1];
            bool looksLikeHeader = typeMsb == ViscaProtocol.IpPayloadTypeMsbVisca || typeMsb == ViscaProtocol.IpPayloadTypeMsbControl;
            if (!looksLikeHeader)
                return false;

            envelope = new ViscaIpEnvelope(
                typeMsb,
                typeLsb,
                (ushort)((packet[2] << 8) | packet[3]),
                (uint)((packet[4] << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7]));

            int declaredLength = envelope.PayloadLength;
            int actualLength = packet.Length - ViscaProtocol.ViscaIpHeaderLength;

            // Copy available payload for downstream socket extraction even if invalid
            int copyLength = Math.Max(0, Math.Min(actualLength, declaredLength > 0 ? declaredLength : actualLength));
            payload = new byte[copyLength];
            if (copyLength > 0)
            {
                Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, copyLength);
            }

            bool supportedPayloadType =
                (typeMsb == ViscaProtocol.IpPayloadTypeMsbVisca &&
                    (typeLsb == ViscaProtocol.IpPayloadTypeLsbCommand ||
                     typeLsb == ViscaProtocol.IpPayloadTypeLsbInquiry ||
                     typeLsb == ViscaProtocol.IpPayloadTypeLsbReply)) ||
                (typeMsb == ViscaProtocol.IpPayloadTypeMsbControl &&
                     typeLsb == ViscaProtocol.IpPayloadTypeLsbControlCommand);

            if (!supportedPayloadType)
            {
                error = $"Unsupported VISCA IP payload type: {typeMsb:X2}{typeLsb:X2}";
                return true;
            }

            if (declaredLength <= 0 || declaredLength > _opt.MaxFrameSize)
            {
                error = $"Invalid VISCA IP payload length: {declaredLength}";
                return true;
            }

            if (actualLength != declaredLength)
            {
                error = $"VISCA IP payload length mismatch: expected {declaredLength}, got {actualLength}";
                return true;
            }

            payload = new byte[declaredLength];
            Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, declaredLength);
            return true;
        }

        private Action<byte[]> WrapResponderWithViscaIpHeader(Action<byte[]> rawSend, ViscaIpEnvelope envelope)
        {
            return payload =>
            {
                byte[] effectivePayload = payload;
                if (effectivePayload == null || effectivePayload.Length == 0)
                {
                    effectivePayload = new byte[] { 0x90, ViscaProtocol.ResponseError, ViscaProtocol.ErrorSyntax, ViscaProtocol.FrameTerminator };
                }

                int maxPayload = Math.Min(_opt.MaxFrameSize, ViscaProtocol.MaxFrameLength);
                ushort length = (ushort)Math.Min(effectivePayload.Length, maxPayload);
                byte[] packet = new byte[ViscaProtocol.ViscaIpHeaderLength + length];
                packet[0] = ViscaProtocol.IpPayloadTypeMsbVisca;
                packet[1] = ViscaProtocol.IpPayloadTypeLsbReply;
                packet[2] = (byte)((length >> 8) & 0xFF);
                packet[3] = (byte)(length & 0xFF);
                packet[4] = (byte)((envelope.Sequence >> 24) & 0xFF);
                packet[5] = (byte)((envelope.Sequence >> 16) & 0xFF);
                packet[6] = (byte)((envelope.Sequence >> 8) & 0xFF);
                packet[7] = (byte)(envelope.Sequence & 0xFF);
                Buffer.BlockCopy(effectivePayload, 0, packet, ViscaProtocol.ViscaIpHeaderLength, length);
                rawSend(packet);
            };
        }

        private void Log(string msg)
        {
            if (_opt.VerboseLog) _opt.Logger?.Invoke(msg);
        }

        public void Dispose() => Stop();
    }
}
