using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Forwards VISCA packets to a real camera over UDP and relays replies back to the latest controller responder.
    /// </summary>
    public sealed class ViscaForwarder : IDisposable
    {
        private readonly Action<string> _logger;
        private readonly string _realCameraIp;
        private readonly int _realCameraPort;
        private readonly object _sync = new();

        private volatile Action<byte[]> _controllerResponder;
        private Thread _receiveThread;
        private volatile bool _running;
        private UdpClient _udp;

        public ViscaForwarder(string realCameraIp, int realCameraPort, Action<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(realCameraIp))
                throw new ArgumentException("Real camera IP is required.", nameof(realCameraIp));
            if (realCameraPort <= 0 || realCameraPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(realCameraPort), "Port must be in 1..65535.");

            _realCameraIp = realCameraIp;
            _realCameraPort = realCameraPort;
            _logger = logger;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_running) return;

                _udp = new UdpClient(AddressFamily.InterNetwork);
                _udp.Connect(_realCameraIp, _realCameraPort);
                _running = true;
                _receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "ViscaForwarder.ReceiveLoop"
                };
                _receiveThread.Start();
            }
        }

        public void Stop()
        {
            UdpClient udpToClose;
            Thread receiveThreadToJoin;

            lock (_sync)
            {
                _running = false;
                _controllerResponder = null;

                udpToClose = _udp;
                _udp = null;

                receiveThreadToJoin = _receiveThread;
                _receiveThread = null;
            }

            try
            {
                udpToClose?.Close();
            }
            catch (Exception e)
            {
                Log($"Forwarder UDP close error: {e.Message}");
            }

            if (receiveThreadToJoin != null &&
                receiveThreadToJoin.IsAlive &&
                receiveThreadToJoin != Thread.CurrentThread)
                receiveThreadToJoin.Join(500);
        }

        public void Forward(byte[] packet, Action<byte[]> controllerResponder)
        {
            if (packet == null || packet.Length == 0) return;

            var udp = _udp;
            if (udp == null || !_running)
            {
                Log("Forward skipped: forwarder is not started.");
                return;
            }

            _controllerResponder = controllerResponder;

            try
            {
                udp.Send(packet, packet.Length);
            }
            catch (ObjectDisposedException)
            {
                // ignore during shutdown
            }
            catch (Exception e)
            {
                Log($"Forward send error: {e.Message}");
            }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                var udp = _udp;
                if (udp == null) break;

                try
                {
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref remote);
                    if (data == null || data.Length == 0) continue;

                    _controllerResponder?.Invoke(data);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException e)
                {
                    if (!_running ||
                        e.SocketErrorCode == SocketError.Interrupted ||
                        e.SocketErrorCode == SocketError.OperationAborted)
                        break;

                    Log($"Forwarder UDP receive error: {e.Message}");
                }
                catch (Exception e)
                {
                    if (_running) Log($"Forwarder receive loop error: {e.Message}");
                }
            }
        }

        private void Log(string message)
        {
            _logger?.Invoke(message);
        }
    }
}
