using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Configuration for ViscaServerCore.
    /// </summary>
    public sealed class ViscaServerOptions
    {
        public IPAddress BindAddress = IPAddress.Any;
        public Action<string> Logger = null;
        public bool LogReceivedCommands = true;
        public int MaxClients = 4;
        public int MaxFrameSize = 4096;
        public int TcpPort = ViscaProtocol.DefaultTcpPort;
        public ViscaTransport Transport = ViscaTransport.UdpRawVisca;
        public int UdpPort = ViscaProtocol.DefaultUdpPort;
        public bool VerboseLog = true;

        /// <summary>
        ///     Interceptor for raw packet processing.
        ///     Args: (rawPacket, originalResponder)
        ///     Return: responder used for local processing. Return null to skip local processing.
        /// </summary>
        public Func<byte[], Action<byte[]>, Action<byte[]>> ProcessingInterceptor = null;
    }

    /// <summary>
    ///     Pure C# VISCA server core. No Unity/MonoBehaviour dependencies.
    /// </summary>
    public sealed class ViscaServerCore : IDisposable
    {
        // Reusable empty responder to avoid allocations during command name lookup
        private static readonly Action<byte[]> EmptyResponder = _ => { };

        private readonly ConcurrentDictionary<TcpClient, ViscaFrameFramer> _clients =
            new();

        private readonly IViscaCommandHandler _handler;
        private readonly ViscaServerOptions _opt;

        private int _clientCount;
        private CancellationTokenSource _cts;
        private TcpListener _tcp;
        private UdpClient _udp;

        public ViscaServerCore(IViscaCommandHandler handler, ViscaServerOptions options)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _opt = options ?? new ViscaServerOptions();
            CommandRegistry = new ViscaCommandRegistry();
        }

        /// <summary>
        ///     Access to the command registry for custom command registration
        /// </summary>
        public ViscaCommandRegistry CommandRegistry { get; }

        public void Dispose()
        {
            Stop();
        }

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
            try
            {
                _cts?.Cancel();
            }
            catch (Exception e)
            {
                Log($"Stop cancel error: {e}");
            }

            try
            {
                _udp?.Close();
            }
            catch (Exception e)
            {
                Log($"UDP close error: {e}");
            }

            try
            {
                _tcp?.Stop();
            }
            catch (Exception e)
            {
                Log($"TCP stop error: {e}");
            }

            _udp = null;
            _tcp = null;
            try
            {
                _cts?.Dispose();
            }
            catch (Exception e)
            {
                Log($"CTS dispose error: {e}");
            }
            _cts = null;
            foreach (var kv in _clients)
                try
                {
                    kv.Key.Close();
                }
                catch (Exception e)
                {
                    Log($"TCP client close error: {e}");
                }

            _clients.Clear();
            Interlocked.Exchange(ref _clientCount, 0);
        }

        private void StartUdp()
        {
            var bindAddress = _opt.BindAddress ?? IPAddress.Any;
            _udp = new UdpClient(new IPEndPoint(bindAddress, _opt.UdpPort));
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            try
            {
                _udp.BeginReceive(UdpReceiveCallback, null);
            }
            catch (Exception e)
            {
                Log($"UDP begin receive error: {e}");
                throw;
            }

            Log($"VISCA UDP server started on {bindAddress}:{_opt.UdpPort}");
        }

        private void StartTcp()
        {
            var bindAddress = _opt.BindAddress ?? IPAddress.Any;
            _tcp = new TcpListener(bindAddress, _opt.TcpPort);
            _tcp.Start();
            ThreadPool.QueueUserWorkItem(_ => AcceptLoopTcp());
            Log($"VISCA TCP server started on {bindAddress}:{_opt.TcpPort}");
        }

        private void UdpReceiveCallback(IAsyncResult ar)
        {
            // Snapshot to avoid race with Stop()
            var udp = _udp;
            if (udp == null) return;

            var remote = new IPEndPoint(IPAddress.Any, 0);
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
                if (_udp != null) Log($"UDP receive error: {e}");
                try
                {
                    _udp?.BeginReceive(UdpReceiveCallback, null);
                }
                catch (Exception ex)
                {
                    Log($"UDP begin receive error: {ex}");
                }

                return;
            }

            try
            {
                _udp?.BeginReceive(UdpReceiveCallback, null);
            }
            catch (Exception e)
            {
                Log($"UDP begin receive error: {e}");
            }

            Action<byte[]> send = bytes =>
            {
                try
                {
                    var u = _udp;
                    if (u != null) u.Send(bytes, bytes.Length, remote);
                }
                catch (Exception e)
                {
                    Log($"UDP send error: {e}");
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
                    if (Interlocked.Increment(ref _clientCount) > _opt.MaxClients)
                    {
                        Log("TCP client refused: max clients reached");
                        Interlocked.Decrement(ref _clientCount);
                        try
                        {
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            Log($"TCP client close error: {ex}");
                        }

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
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (_tcp != null) Log($"TCP accept error: {e}");
                }
            }
        }

        private void ClientLoopTcp(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[8192];
                Action<byte[]> send = bytes =>
                {
                    try
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                    catch (Exception e)
                    {
                        Log($"TCP send error: {e}");
                    }
                };
                while (_cts != null && !_cts.IsCancellationRequested && client.Connected)
                {
                    int n;
                    try
                    {
                        n = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (Exception e)
                    {
                        Log($"TCP read error: {e}");
                        break;
                    }

                    if (n <= 0) break;
                    if (_clients.TryGetValue(client, out var framer))
                        framer.Append(new ReadOnlySpan<byte>(buffer, 0, n), frame =>
                        {
                            if (frame.Length > _opt.MaxFrameSize)
                            {
                                Log($"TCP frame too large: {frame.Length}");
                                return; // drop
                            }

                            ProcessFrame(frame, send);
                        });
                    // No framer state; drop
                }

                _clients.TryRemove(client, out _);
                Interlocked.Decrement(ref _clientCount);
            }
        }

        private void ProcessFrame(byte[] packet, Action<byte[]> rawSend)
        {
            if (packet == null || packet.Length == 0) return;

            if (_opt.ProcessingInterceptor != null)
            {
                rawSend = _opt.ProcessingInterceptor(packet, rawSend);
                if (rawSend == null) return;
            }

            var frame = packet;
            var responder = rawSend;
            var payloadTypeSupported = true;
            var socketIdFromPayload = ViscaProtocol.DefaultSocketId;

            if (TryParseViscaIpEnvelope(packet, out var envelope, out var payload, out var headerError,
                    out payloadTypeSupported))
            {
                responder = WrapResponderWithViscaIpHeader(rawSend, envelope);
                frame = payload;
                socketIdFromPayload = ViscaProtocol.ExtractSocketId(payload);

                if (!payloadTypeSupported)
                {
                    Log($"Unsupported VISCA IP payload type: {envelope.TypeMsb:X2}{envelope.TypeLsb:X2}");
                    ViscaResponse.SendError(responder, ViscaProtocol.ErrorCommandNotExecutable, socketIdFromPayload);
                    return;
                }

                if (envelope.TypeMsb == ViscaProtocol.IpPayloadTypeMsbControl &&
                    envelope.TypeLsb == ViscaProtocol.IpPayloadTypeLsbControlCommand)
                {
                    // Treat control payload as keepalive/open/close no-op with completion when well-formed
                    var maxPayload = Math.Min(_opt.MaxFrameSize, ViscaProtocol.MaxFrameLength);
                    var valid = frame != null &&
                                frame.Length > 0 &&
                                frame.Length <= maxPayload &&
                                frame[^1] == ViscaProtocol.FrameTerminator;
                    if (!valid)
                    {
                        Log("Invalid VISCA IP control payload (syntax)");
                        _handler.HandleError(frame ?? Array.Empty<byte>(), responder, ViscaProtocol.ErrorSyntax);
                    }
                    else
                    {
                        ViscaResponse.SendCompletion(responder, ViscaReplyMode.AckAndCompletion, socketIdFromPayload);
                    }

                    return;
                }

                if (!string.IsNullOrEmpty(headerError))
                {
                    Log(headerError);
                    _handler.HandleError(frame, responder, ViscaProtocol.ErrorMessageLength);
                    return;
                }
            }

            // Validate frame
            if (!IsValidViscaPayload(frame, responder)) return;

            // Log received command (only generate details string when logging enabled)
            if (_opt.LogReceivedCommands)
            {
                var details = CommandRegistry.GetCommandDetails(frame, responder);
                Log($"RX: {details}");
            }

            // Try to execute command through registry (O(1) lookup for most commands)
            var context = CommandRegistry.TryExecute(frame, _handler, responder);
            if (context.HasValue) return;

            // Unknown command - send error response
            var cmdName = CommandRegistry.GetCommandName(frame);
            Log($"WARNING: Unknown command: {cmdName}");
            _handler.HandleError(frame, responder, ViscaProtocol.ErrorSyntax);
        }

        private bool TryParseViscaIpEnvelope(byte[] packet, out ViscaIpEnvelope envelope, out byte[] payload,
            out string error, out bool supportedPayloadType)
        {
            envelope = default;
            payload = packet;
            error = null;
            supportedPayloadType = true;

            if (packet == null || packet.Length < ViscaProtocol.ViscaIpHeaderLength)
                return false;

            var typeMsb = packet[0];
            var typeLsb = packet[1];
            var looksLikeHeader = typeMsb == ViscaProtocol.IpPayloadTypeMsbVisca ||
                                  typeMsb == ViscaProtocol.IpPayloadTypeMsbControl;
            if (!looksLikeHeader)
                return false;

            envelope = new ViscaIpEnvelope(
                typeMsb,
                typeLsb,
                (ushort)((packet[2] << 8) | packet[3]),
                (uint)((packet[4] << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7]));

            int declaredLength = envelope.PayloadLength;
            var actualLength = packet.Length - ViscaProtocol.ViscaIpHeaderLength;

            // Copy available payload for downstream socket extraction even if invalid
            var copyLength = Math.Max(0, Math.Min(actualLength, declaredLength > 0 ? declaredLength : actualLength));
            payload = new byte[copyLength];
            if (copyLength > 0) Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, copyLength);

            supportedPayloadType =
                (typeMsb == ViscaProtocol.IpPayloadTypeMsbVisca &&
                 (typeLsb == ViscaProtocol.IpPayloadTypeLsbCommand ||
                  typeLsb == ViscaProtocol.IpPayloadTypeLsbInquiry ||
                  typeLsb == ViscaProtocol.IpPayloadTypeLsbReply)) ||
                (typeMsb == ViscaProtocol.IpPayloadTypeMsbControl &&
                 typeLsb == ViscaProtocol.IpPayloadTypeLsbControlCommand);

            if (!supportedPayloadType) return true;

            // Enforce documented VISCA payload length (1..16 bytes)
            if (declaredLength <= 0 || declaredLength > ViscaProtocol.MaxFrameLength)
            {
                error = $"Invalid VISCA IP payload length: {declaredLength}";
                return true;
            }

            if (actualLength != declaredLength)
            {
                error = $"VISCA IP payload length mismatch: expected {declaredLength}, got {actualLength}";
                return true;
            }

            if (actualLength > ViscaProtocol.MaxFrameLength)
            {
                error = $"VISCA IP payload exceeds max length: {actualLength}";
                return true;
            }

            payload = new byte[declaredLength];
            Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, declaredLength);
            return true;
        }

        private bool IsValidViscaPayload(byte[] frame, Action<byte[]> responder)
        {
            var safeResponder = responder ?? (_ => { });

            if (frame == null || frame.Length < ViscaProtocol.MinFrameLength)
            {
                Log($"Frame too short: {frame?.Length ?? 0} bytes");
                _handler.HandleError(frame ?? Array.Empty<byte>(), safeResponder, ViscaProtocol.ErrorMessageLength);
                return false;
            }

            if (frame[^1] != ViscaProtocol.FrameTerminator)
            {
                Log($"Invalid VISCA frame (no terminator): {BitConverter.ToString(frame)}");
                _handler.HandleError(frame, safeResponder, ViscaProtocol.ErrorSyntax);
                return false;
            }

            var maxPayload = Math.Min(_opt.MaxFrameSize, ViscaProtocol.MaxFrameLength);
            if (frame.Length > maxPayload)
            {
                Log($"Frame too large: {frame.Length} bytes");
                _handler.HandleError(frame, safeResponder, ViscaProtocol.ErrorMessageLength);
                return false;
            }

            return true;
        }

        private Action<byte[]> WrapResponderWithViscaIpHeader(Action<byte[]> rawSend, ViscaIpEnvelope envelope)
        {
            return payload =>
            {
                var effectivePayload = payload;
                if (effectivePayload == null || effectivePayload.Length == 0)
                    effectivePayload = new byte[]
                        { 0x90, ViscaProtocol.ResponseError, ViscaProtocol.ErrorSyntax, ViscaProtocol.FrameTerminator };

                var maxPayload = Math.Min(_opt.MaxFrameSize, ViscaProtocol.MaxFrameLength);
                var length = (ushort)Math.Min(effectivePayload.Length, maxPayload);
                var packet = new byte[ViscaProtocol.ViscaIpHeaderLength + length];
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
    }
}
