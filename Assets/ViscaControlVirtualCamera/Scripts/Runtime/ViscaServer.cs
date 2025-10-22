using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    [DefaultExecutionOrder(-50)]
    public class ViscaServer : MonoBehaviour, IViscaCommandHandler
    {
        [Header("Server")]
        public bool autoStart = true;
        public ViscaTransport transport = ViscaTransport.UdpRawVisca;
        public int udpPort = 52381;
        public int tcpPort = 52380;
        public int maxClients = 4;
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;
        public bool verboseLog = true;

        [Header("Targets")]
        public PtzController ptz;

        private UdpClient _udp;
        private TcpListener _tcp;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new();

        private void Awake()
        {
            if (ptz == null) ptz = FindObjectOfType<PtzController>();
        }

        private void Start()
        {
            if (autoStart) StartServer();
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var act))
            {
                try { act(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        public void StartServer()
        {
            StopServer();
            _cts = new CancellationTokenSource();
            if (transport == ViscaTransport.UdpRawVisca || transport == ViscaTransport.Both)
            {
                StartUdp();
            }
            if (transport == ViscaTransport.TcpRawVisca || transport == ViscaTransport.Both)
            {
                StartTcp();
            }
        }

        public void StopServer()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            try { _tcp?.Stop(); } catch { }
            _cts = null;
            _udp = null;
            _tcp = null;
        }

        private void StartUdp()
        {
            _udp = new UdpClient(udpPort);
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            _udp.BeginReceive(ReceiveCallbackUdp, null);
            if (verboseLog) Debug.Log($"VISCA UDP server started on {udpPort}");
        }

        private void StartTcp()
        {
            _tcp = new TcpListener(IPAddress.Any, tcpPort);
            _tcp.Start();
            ThreadPool.QueueUserWorkItem(_ => AcceptLoopTcp());
            if (verboseLog) Debug.Log($"VISCA TCP server started on {tcpPort}");
        }

        private void ReceiveCallbackUdp(IAsyncResult ar)
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            byte[] data;
            try
            {
                data = _udp.EndReceive(ar, ref remote);
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception e)
            {
                if (verboseLog) Debug.LogWarning($"VISCA receive error: {e.Message}");
                if (_udp != null) _udp.BeginReceive(ReceiveCallbackUdp, null);
                return;
            }

            if (_udp != null) _udp.BeginReceive(ReceiveCallbackUdp, null);

            // Raw mode: assume one VISCA frame per datagram
            Action<byte[]> send = (bytes) =>
            {
                try { _udp?.Send(bytes, bytes.Length, remote); } catch { }
            };
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
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void ClientLoopTcp(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var buffer = new byte[4096];
                var acc = new System.IO.MemoryStream();
                Action<byte[]> send = (bytes) =>
                {
                    try { stream.Write(bytes, 0, bytes.Length); stream.Flush(); } catch { }
                };
                while (_cts != null && !_cts.IsCancellationRequested && client.Connected)
                {
                    int n;
                    try { n = stream.Read(buffer, 0, buffer.Length); }
                    catch { break; }
                    if (n <= 0) break;
                    acc.Write(buffer, 0, n);
                    // Extract frames terminated by 0xFF
                    while (true)
                    {
                        var data = acc.ToArray();
                        int idx = Array.IndexOf(data, (byte)0xFF);
                        if (idx < 0) break;
                        var frame = new byte[idx + 1];
                        Array.Copy(data, frame, idx + 1);
                        // left over
                        var remain = new byte[data.Length - (idx + 1)];
                        Array.Copy(data, idx + 1, remain, 0, remain.Length);
                        acc.SetLength(0);
                        acc.Write(remain, 0, remain.Length);
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
                if (verboseLog) Debug.LogWarning("Invalid VISCA frame (no terminator)");
                HandleSyntaxError(frame, send);
                return;
            }
            // Dispatch by category
            if (ViscaParser.TryParsePanTiltDrive(frame, out var vv, out var ww, out var pp, out var tt))
            {
                HandlePanTiltDrive(vv, ww, pp, tt, send);
                return;
            }
            if (ViscaParser.TryParseZoomVariable(frame, out var zz))
            {
                HandleZoomVariable(zz, send);
                return;
            }
            if (ViscaParser.TryParsePanTiltAbsolute(frame, out var avv, out var aww, out var panPos, out var tiltPos))
            {
                HandlePanTiltAbsolute(avv, aww, panPos, tiltPos, send);
                return;
            }

            if (verboseLog) Debug.LogWarning($"Unsupported VISCA command: {BitConverter.ToString(frame)}");
            HandleSyntaxError(frame, send);
        }

        private void SendAck(Action<byte[]> send)
        {
            if (replyMode == ViscaReplyMode.None) return;
            // Minimal ACK (socket nibble Z is 0 by default): 90 40 FF
            var payload = new byte[] { 0x90, 0x40, 0xFF };
            try { send(payload); } catch { }
        }

        private void SendCompletion(Action<byte[]> send)
        {
            if (replyMode != ViscaReplyMode.AckAndCompletion) return;
            // Minimal Completion: 90 50 FF
            var payload = new byte[] { 0x90, 0x50, 0xFF };
            try { send(payload); } catch { }
        }

        private void SendError(Action<byte[]> send, byte ee)
        {
            if (replyMode == ViscaReplyMode.None) return;
            // Error: 90 60 EE FF
            var payload = new byte[] { 0x90, 0x60, ee, 0xFF };
            try { send(payload); } catch { }
        }

        public bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, Action<byte[]> responder)
        {
            SendAck(responder);
            var pdir = ViscaParser.DirFromVisca(panDir);
            var tdir = ViscaParser.DirFromVisca(tiltDir);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null)
                {
                    ptz.CommandPanTiltVariable(panSpeed, tiltSpeed, pdir, tdir);
                }
                SendCompletion(responder);
            });
            if (verboseLog) Debug.Log($"PT Drive vv={panSpeed:X2} ww={tiltSpeed:X2} pp={panDir:X2} tt={tiltDir:X2}");
            return true;
        }

        public bool HandleZoomVariable(byte zz, Action<byte[]> responder)
        {
            SendAck(responder);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null) ptz.CommandZoomVariable(zz);
                SendCompletion(responder);
            });
            if (verboseLog) Debug.Log($"Zoom ZZ={zz:X2}");
            return true;
        }

        public bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, Action<byte[]> responder)
        {
            SendAck(responder);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null) ptz.CommandPanTiltAbsolute(panSpeed, tiltSpeed, panPos, tiltPos);
                SendCompletion(responder);
            });
            if (verboseLog) Debug.Log($"PT Abs vv={panSpeed:X2} ww={tiltSpeed:X2} pan={panPos:X4} tilt={tiltPos:X4}");
            return true;
        }

        public void HandleSyntaxError(byte[] frame, Action<byte[]> responder)
        {
            if (verboseLog) Debug.LogWarning("VISCA syntax error");
            SendError(responder, 0x02); // Syntax Error
        }
    }
}
