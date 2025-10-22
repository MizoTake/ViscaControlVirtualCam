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
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;
        public bool verboseLog = true;

        [Header("Targets")]
        public PtzController ptz;

        private UdpClient _udp;
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
            if (transport != ViscaTransport.UdpRawVisca)
            {
                Debug.LogWarning($"Transport {transport} not implemented; falling back to UdpRawVisca.");
            }
            _cts = new CancellationTokenSource();
            _udp = new UdpClient(udpPort);
            _udp.Client.ReceiveBufferSize = 64 * 1024;
            _udp.BeginReceive(ReceiveCallback, null);
            if (verboseLog) Debug.Log($"VISCA UDP server started on {udpPort}");
        }

        public void StopServer()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp?.Close(); } catch { }
            _cts = null;
            _udp = null;
        }

        private void ReceiveCallback(IAsyncResult ar)
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
                if (_udp != null) _udp.BeginReceive(ReceiveCallback, null);
                return;
            }

            if (_udp != null) _udp.BeginReceive(ReceiveCallback, null);

            // Raw mode: assume one VISCA frame per datagram
            ProcessDatagram(data, remote);
        }

        private void ProcessDatagram(byte[] frame, IPEndPoint remote)
        {
            if (frame == null || frame.Length == 0) return;
            if (frame[^1] != 0xFF)
            {
                if (verboseLog) Debug.LogWarning("Invalid VISCA frame (no terminator)");
                HandleSyntaxError(frame, remote);
                return;
            }
            // Dispatch by category
            if (ViscaParser.TryParsePanTiltDrive(frame, out var vv, out var ww, out var pp, out var tt))
            {
                HandlePanTiltDrive(vv, ww, pp, tt, remote);
                return;
            }
            if (ViscaParser.TryParseZoomVariable(frame, out var zz))
            {
                HandleZoomVariable(zz, remote);
                return;
            }
            if (ViscaParser.TryParsePanTiltAbsolute(frame, out var avv, out var aww, out var panPos, out var tiltPos))
            {
                HandlePanTiltAbsolute(avv, aww, panPos, tiltPos, remote);
                return;
            }

            if (verboseLog) Debug.LogWarning($"Unsupported VISCA command: {BitConverter.ToString(frame)}");
            HandleSyntaxError(frame, remote);
        }

        private void SendAck(IPEndPoint remote)
        {
            if (replyMode == ViscaReplyMode.None) return;
            // Minimal ACK (socket nibble Z is 0 by default): 90 40 FF
            var payload = new byte[] { 0x90, 0x40, 0xFF };
            try { _udp?.Send(payload, payload.Length, remote); } catch { }
        }

        private void SendCompletion(IPEndPoint remote)
        {
            if (replyMode != ViscaReplyMode.AckAndCompletion) return;
            // Minimal Completion: 90 50 FF
            var payload = new byte[] { 0x90, 0x50, 0xFF };
            try { _udp?.Send(payload, payload.Length, remote); } catch { }
        }

        private void SendError(IPEndPoint remote, byte ee)
        {
            if (replyMode == ViscaReplyMode.None) return;
            // Error: 90 60 EE FF
            var payload = new byte[] { 0x90, 0x60, ee, 0xFF };
            try { _udp?.Send(payload, payload.Length, remote); } catch { }
        }

        public bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, IPEndPoint remote)
        {
            SendAck(remote);
            var pdir = ViscaParser.DirFromVisca(panDir);
            var tdir = ViscaParser.DirFromVisca(tiltDir);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null)
                {
                    ptz.CommandPanTiltVariable(panSpeed, tiltSpeed, pdir, tdir);
                }
                SendCompletion(remote);
            });
            if (verboseLog) Debug.Log($"PT Drive vv={panSpeed:X2} ww={tiltSpeed:X2} pp={panDir:X2} tt={tiltDir:X2}");
            return true;
        }

        public bool HandleZoomVariable(byte zz, IPEndPoint remote)
        {
            SendAck(remote);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null) ptz.CommandZoomVariable(zz);
                SendCompletion(remote);
            });
            if (verboseLog) Debug.Log($"Zoom ZZ={zz:X2}");
            return true;
        }

        public bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, IPEndPoint remote)
        {
            SendAck(remote);
            _mainThreadActions.Enqueue(() =>
            {
                if (ptz != null) ptz.CommandPanTiltAbsolute(panSpeed, tiltSpeed, panPos, tiltPos);
                SendCompletion(remote);
            });
            if (verboseLog) Debug.Log($"PT Abs vv={panSpeed:X2} ww={tiltSpeed:X2} pan={panPos:X4} tilt={tiltPos:X4}");
            return true;
        }

        public void HandleSyntaxError(byte[] frame, IPEndPoint remote)
        {
            if (verboseLog) Debug.LogWarning("VISCA syntax error");
            SendError(remote, 0x02); // Syntax Error
        }
    }
}

