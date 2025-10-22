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
    }

    // Pure C# server core. No Unity/MonoBehaviour dependencies.
    public class ViscaServerCore : IDisposable
    {
        private readonly IViscaCommandHandler _handler;
        private readonly ViscaServerOptions _opt;
        private UdpClient _udp;
        private TcpListener _tcp;
        private CancellationTokenSource _cts;

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
                    client.NoDelay = true;
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
                var buffer = new byte[4096];
                var acc = new System.IO.MemoryStream();
                Action<byte[]> send = (bytes) => { try { stream.Write(bytes, 0, bytes.Length); stream.Flush(); } catch { } };
                while (_cts != null && !_cts.IsCancellationRequested && client.Connected)
                {
                    int n; try { n = stream.Read(buffer, 0, buffer.Length); } catch { break; }
                    if (n <= 0) break;
                    acc.Write(buffer, 0, n);
                    while (true)
                    {
                        var data = acc.ToArray();
                        int idx = Array.IndexOf(data, (byte)0xFF);
                        if (idx < 0) break;
                        var frame = new byte[idx + 1]; Array.Copy(data, frame, idx + 1);
                        var remain = new byte[data.Length - (idx + 1)]; Array.Copy(data, idx + 1, remain, 0, remain.Length);
                        acc.SetLength(0); acc.Write(remain, 0, remain.Length);
                        ProcessFrame(frame, send);
                    }
                }
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
            if (ViscaParser.TryParsePanTiltDrive(frame, out var vv, out var ww, out var pp, out var tt)) { _handler.HandlePanTiltDrive(vv, ww, pp, tt, send); return; }
            if (ViscaParser.TryParseZoomVariable(frame, out var zz)) { _handler.HandleZoomVariable(zz, send); return; }
            if (ViscaParser.TryParsePanTiltAbsolute(frame, out var avv, out var aww, out var panPos, out var tiltPos)) { _handler.HandlePanTiltAbsolute(avv, aww, panPos, tiltPos, send); return; }
            Log($"Unsupported VISCA command: {BitConverter.ToString(frame)}");
            _handler.HandleSyntaxError(frame, send);
        }

        private void Log(string msg)
        {
            if (_opt.VerboseLog) _opt.Logger?.Invoke(msg);
        }

        public void Dispose() => Stop();
    }
}

