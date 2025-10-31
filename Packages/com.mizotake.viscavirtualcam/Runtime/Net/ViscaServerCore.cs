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
        public Action<string> Logger = null; // optional
        public int MaxFrameSize = 4096; // guard for malformed streams
    }

    // Pure C# server core. No Unity/MonoBehaviour dependencies.
    public class ViscaServerCore : IDisposable
    {
        private readonly IViscaCommandHandler _handler;
        private readonly ViscaServerOptions _opt;
        private UdpClient _udp;
        private TcpListener _tcp;
        private CancellationTokenSource _cts;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<TcpClient, ViscaFrameFramer> _clients = new();
        private int _clientCount = 0;

        public ViscaServerCore(IViscaCommandHandler handler, ViscaServerOptions options)
        {
            _handler = handler;
            _opt = options ?? new ViscaServerOptions();
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
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try { data = _udp.EndReceive(ar, ref remote); }
            catch (ObjectDisposedException) { return; }
            catch (Exception e) { Log($"UDP receive error: {e.Message}"); if (_udp != null) _udp.BeginReceive(UdpReceiveCallback, null); return; }

            if (_udp != null) _udp.BeginReceive(UdpReceiveCallback, null);

            Action<byte[]> send = (bytes) => { try { _udp?.Send(bytes, bytes.Length, remote); } catch { } };
            ProcessFrame(data, send);
        }

        private void AcceptLoopTcp()
        {
            var token = _cts;
            while (token != null && !token.IsCancellationRequested)
            {
                try
                {
                    var client = _tcp.AcceptTcpClient();
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
                    if (_tcp == null) break;
                }
                catch (ObjectDisposedException) { break; }
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
                Log("Invalid VISCA frame (no terminator)");
                _handler.HandleSyntaxError(frame, send);
                return;
            }
            if (frame.Length > _opt.MaxFrameSize)
            {
                Log($"Frame too large: {frame.Length}");
                return;
            }
            // Standard VISCA commands
            if (ViscaParser.TryParsePanTiltDrive(frame, out var vv, out var ww, out var pp, out var tt)) { _handler.HandlePanTiltDrive(vv, ww, pp, tt, send); return; }
            if (ViscaParser.TryParseZoomVariable(frame, out var zz)) { _handler.HandleZoomVariable(zz, send); return; }
            if (ViscaParser.TryParsePanTiltAbsolute(frame, out var avv, out var aww, out var panPos, out var tiltPos)) { _handler.HandlePanTiltAbsolute(avv, aww, panPos, tiltPos, send); return; }

            // Blackmagic PTZ Control extended commands
            if (ViscaParser.TryParseZoomDirect(frame, out var zoomPos)) { _handler.HandleZoomDirect(zoomPos, send); return; }
            if (ViscaParser.TryParseFocusVariable(frame, out var focusSpeed)) { _handler.HandleFocusVariable(focusSpeed, send); return; }
            if (ViscaParser.TryParseFocusDirect(frame, out var focusPos)) { _handler.HandleFocusDirect(focusPos, send); return; }
            if (ViscaParser.TryParseIrisVariable(frame, out var irisDir)) { _handler.HandleIrisVariable(irisDir, send); return; }
            if (ViscaParser.TryParseIrisDirect(frame, out var irisPos)) { _handler.HandleIrisDirect(irisPos, send); return; }
            if (ViscaParser.TryParseMemoryRecall(frame, out var memRecall)) { _handler.HandleMemoryRecall(memRecall, send); return; }
            if (ViscaParser.TryParseMemorySet(frame, out var memSet)) { _handler.HandleMemorySet(memSet, send); return; }

            // Known but unsupported commands: log what came in, do not apply to camera.
            var name = ViscaParser.GetCommandName(frame);
            Log($"Ignored VISCA command: {name} frame={BitConverter.ToString(frame)}");
            // Intentionally no error; we just log.
        }

        private void Log(string msg)
        {
            if (_opt.VerboseLog) _opt.Logger?.Invoke(msg);
        }

        public void Dispose() => Stop();
    }
}
