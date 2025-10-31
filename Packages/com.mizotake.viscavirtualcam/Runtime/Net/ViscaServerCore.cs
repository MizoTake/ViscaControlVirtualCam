using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ViscaControlVirtualCam
{
    public class ViscaServerOptions
    {
        public ViscaTransport Transport = ViscaTransport.UdpRawVisca;
        public int UdpPort = 52381;
        public int TcpPort = 52380;
        public int MaxClients = 4;
        public bool VerboseLog = true;
        public bool LogReceivedCommands = true; // Log every received VISCA command with details
        public Action<string> Logger = null; // optional
        public int MaxFrameSize = 4096; // guard for malformed streams
    }

    // Pure C# server core. No Unity/MonoBehaviour dependencies.
    public class ViscaServerCore : IDisposable
    {
        private readonly IViscaCommandHandler _handler;
        private readonly ViscaServerOptions _opt;
        private readonly ViscaCommandRegistry _commandRegistry;
        private UdpClient _udp;
        private TcpListener _tcp;
        private CancellationTokenSource _cts;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TcpClient, ViscaFrameFramer> _clients = new();
        private int _clientCount = 0;

        public ViscaServerCore(IViscaCommandHandler handler, ViscaServerOptions options)
        {
            _handler = handler;
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
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            try { _tcp?.Stop(); } catch { }
            _udp = null;
            _tcp = null;
            _cts = null;
            foreach (var kv in _clients)
            {
                try { kv.Key.Close(); } catch { }
            }
            _clients.Clear();
            System.Threading.Interlocked.Exchange(ref _clientCount, 0);
        }

        private void StartUdp()
        {
            _udp = new UdpClient(_opt.UdpPort);
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            _udp.BeginReceive(UdpReceiveCallback, null);
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
                try { _udp?.BeginReceive(UdpReceiveCallback, null); } catch { }
                return;
            }

            try { _udp?.BeginReceive(UdpReceiveCallback, null); } catch { }

            Action<byte[]> send = (bytes) =>
            {
                try
                {
                    var u = _udp;
                    if (u != null) u.Send(bytes, bytes.Length, remote);
                }
                catch { }
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
                Action<byte[]> send = (bytes) => { try { stream.Write(bytes, 0, bytes.Length); stream.Flush(); } catch { } };
                while (_cts != null && !_cts.IsCancellationRequested && client.Connected)
                {
                    int n; try { n = stream.Read(buffer, 0, buffer.Length); } catch { break; }
                    if (n <= 0) break;
                    if (_clients.TryGetValue(client, out var framer))
                    {
                        int before = 0;
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

        private void ProcessFrame(byte[] frame, Action<byte[]> send)
        {
            if (frame == null || frame.Length == 0) return;
            if (frame[^1] != 0xFF)
            {
                Log($"Invalid VISCA frame (no terminator): {BitConverter.ToString(frame)}");
                _handler.HandleSyntaxError(frame, send);
                return;
            }
            if (frame.Length > _opt.MaxFrameSize)
            {
                Log($"Frame too large: {frame.Length} bytes");
                return;
            }

            // Log received command with details
            if (_opt.LogReceivedCommands)
            {
                string details = _commandRegistry.GetCommandDetails(frame);
                Log($"RX: {details}");
            }

            // Try to execute command through registry
            var command = _commandRegistry.TryExecute(frame, _handler, send);
            if (command != null)
            {
                // Command was handled successfully
                return;
            }

            // Unknown command
            string cmdName = _commandRegistry.GetCommandName(frame);
            Log($"WARNING: Command not handled: {cmdName}");
            // Intentionally no error; we just log.
        }

        private void Log(string msg)
        {
            if (_opt.VerboseLog) _opt.Logger?.Invoke(msg);
        }

        public void Dispose() => Stop();
    }
}
