using System;
using System.Net;
using System.Net.Sockets;

namespace ViscaControlVirtualCam
{
    public readonly struct ViscaInquiryStatus
    {
        public readonly ushort PanRaw;
        public readonly ushort TiltRaw;
        public readonly ushort ZoomRaw;
        public readonly double TimestampUtcSeconds;

        public ViscaInquiryStatus(ushort panRaw, ushort tiltRaw, ushort zoomRaw, double timestampUtcSeconds)
        {
            PanRaw = panRaw;
            TiltRaw = tiltRaw;
            ZoomRaw = zoomRaw;
            TimestampUtcSeconds = timestampUtcSeconds;
        }
    }

    /// <summary>
    ///     VISCA over IP Inquiry client for polling PTZ status from a real camera.
    /// </summary>
    public sealed class ViscaInquiryClient : IDisposable
    {
        private static readonly DateTime UnixEpochUtc = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly byte _cameraAddress;
        private readonly Action<string> _logger;
        private readonly int _retryCount;
        private readonly int _timeoutMilliseconds;
        private readonly UdpClient _udp;
        private uint _sequence;

        public ViscaInquiryClient(string cameraIp, int cameraPort,
            int timeoutMilliseconds = 150, int retryCount = 1, byte cameraAddressNibble = 0x01,
            Action<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(cameraIp))
                throw new ArgumentException("Camera IP is required.", nameof(cameraIp));
            if (cameraPort <= 0 || cameraPort > 65535)
                throw new ArgumentOutOfRangeException(nameof(cameraPort), "Port must be in 1..65535.");
            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Timeout must be positive.");
            if (retryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count must be 0 or greater.");

            _timeoutMilliseconds = timeoutMilliseconds;
            _retryCount = retryCount;
            _logger = logger;

            var nibble = (byte)(cameraAddressNibble & 0x0F);
            if (nibble == 0) nibble = ViscaProtocol.DefaultSocketId;
            _cameraAddress = (byte)(0x80 | nibble);

            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Connect(cameraIp, cameraPort);
        }

        public void Dispose()
        {
            _udp?.Close();
            _udp?.Dispose();
        }

        public bool TryGetStatus(out ViscaInquiryStatus status)
        {
            status = default;

            if (!TryQueryPanTilt(out var panRaw, out var tiltRaw))
                return false;
            if (!TryQueryZoom(out var zoomRaw))
                return false;

            var timestamp = (DateTime.UtcNow - UnixEpochUtc).TotalSeconds;
            status = new ViscaInquiryStatus(panRaw, tiltRaw, zoomRaw, timestamp);
            return true;
        }

        private bool TryQueryPanTilt(out ushort panRaw, out ushort tiltRaw)
        {
            panRaw = 0;
            tiltRaw = 0;

            // Pan/Tilt Position Inquiry: 8x 09 06 12 FF
            var payload = new byte[]
            {
                _cameraAddress,
                ViscaProtocol.CategoryInquiry,
                ViscaProtocol.GroupPanTilt,
                0x12,
                ViscaProtocol.FrameTerminator
            };

            var sequence = _sequence;
            var packet = BuildInquiryPacket(payload, sequence);
            for (var attempt = 0; attempt <= _retryCount; attempt++)
            {
                if (!TrySend(packet))
                    break;

                if (WaitPanTiltReply(sequence, out panRaw, out tiltRaw))
                {
                    _sequence = unchecked(sequence + 1);
                    return true;
                }
            }

            _sequence = unchecked(sequence + 1);
            Log($"Inquiry timeout (PanTilt): seq={sequence}, retries={_retryCount}");
            return false;
        }

        private bool TryQueryZoom(out ushort zoomRaw)
        {
            zoomRaw = 0;

            // Zoom Position Inquiry: 8x 09 04 47 FF
            var payload = new byte[]
            {
                _cameraAddress,
                ViscaProtocol.CategoryInquiry,
                ViscaProtocol.GroupCamera,
                0x47,
                ViscaProtocol.FrameTerminator
            };

            var sequence = _sequence;
            var packet = BuildInquiryPacket(payload, sequence);
            for (var attempt = 0; attempt <= _retryCount; attempt++)
            {
                if (!TrySend(packet))
                    break;

                if (WaitZoomReply(sequence, out zoomRaw))
                {
                    _sequence = unchecked(sequence + 1);
                    return true;
                }
            }

            _sequence = unchecked(sequence + 1);
            Log($"Inquiry timeout (Zoom): seq={sequence}, retries={_retryCount}");
            return false;
        }

        private bool WaitPanTiltReply(uint expectedSequence, out ushort panRaw, out ushort tiltRaw)
        {
            panRaw = 0;
            tiltRaw = 0;
            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMilliseconds);

            while (DateTime.UtcNow < deadline)
            {
                var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0)
                    break;

                try
                {
                    _udp.Client.ReceiveTimeout = remaining;
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var packet = _udp.Receive(ref remote);
                    if (!TryParseReplyPacket(packet, out var sequence, out var replyPayload))
                        continue;
                    if (sequence != expectedSequence)
                        continue;

                    if (TryParsePanTiltReply(replyPayload, out panRaw, out tiltRaw))
                        return true;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (Exception e)
                {
                    Log($"Inquiry receive error: {e.Message}");
                    return false;
                }
            }

            return false;
        }

        private bool WaitZoomReply(uint expectedSequence, out ushort zoomRaw)
        {
            zoomRaw = 0;
            var deadline = DateTime.UtcNow.AddMilliseconds(_timeoutMilliseconds);

            while (DateTime.UtcNow < deadline)
            {
                var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0)
                    break;

                try
                {
                    _udp.Client.ReceiveTimeout = remaining;
                    var remote = new IPEndPoint(IPAddress.Any, 0);
                    var packet = _udp.Receive(ref remote);
                    if (!TryParseReplyPacket(packet, out var sequence, out var replyPayload))
                        continue;
                    if (sequence != expectedSequence)
                        continue;

                    if (TryParseZoomReply(replyPayload, out zoomRaw))
                        return true;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                catch (Exception e)
                {
                    Log($"Inquiry receive error: {e.Message}");
                    return false;
                }
            }

            return false;
        }

        private bool TrySend(byte[] packet)
        {
            try
            {
                _udp.Send(packet, packet.Length);
                return true;
            }
            catch (Exception e)
            {
                Log($"Inquiry send error: {e.Message}");
                return false;
            }
        }

        private static byte[] BuildInquiryPacket(byte[] payload, uint sequence)
        {
            var length = (ushort)payload.Length;
            var packet = new byte[ViscaProtocol.ViscaIpHeaderLength + length];
            packet[0] = ViscaProtocol.IpPayloadTypeMsbVisca;
            packet[1] = ViscaProtocol.IpPayloadTypeLsbInquiry;
            packet[2] = (byte)((length >> 8) & 0xFF);
            packet[3] = (byte)(length & 0xFF);
            packet[4] = (byte)((sequence >> 24) & 0xFF);
            packet[5] = (byte)((sequence >> 16) & 0xFF);
            packet[6] = (byte)((sequence >> 8) & 0xFF);
            packet[7] = (byte)(sequence & 0xFF);
            Buffer.BlockCopy(payload, 0, packet, ViscaProtocol.ViscaIpHeaderLength, length);
            return packet;
        }

        private static bool TryParseReplyPacket(byte[] packet, out uint sequence, out byte[] payload)
        {
            sequence = 0;
            payload = null;

            if (packet == null || packet.Length < ViscaProtocol.ViscaIpHeaderLength)
                return false;
            if (packet[0] != ViscaProtocol.IpPayloadTypeMsbVisca ||
                packet[1] != ViscaProtocol.IpPayloadTypeLsbReply)
                return false;

            var payloadLength = (packet[2] << 8) | packet[3];
            if (payloadLength <= 0 || payloadLength > ViscaProtocol.MaxFrameLength)
                return false;
            if (packet.Length != ViscaProtocol.ViscaIpHeaderLength + payloadLength)
                return false;

            sequence = (uint)((packet[4] << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7]);
            payload = new byte[payloadLength];
            Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, payloadLength);
            return true;
        }

        private static bool TryParsePanTiltReply(byte[] payload, out ushort panRaw, out ushort tiltRaw)
        {
            panRaw = 0;
            tiltRaw = 0;

            if (payload == null || payload.Length != 11)
                return false;
            if (payload[0] != 0x90 || (payload[1] & 0xF0) != ViscaProtocol.ResponseCompletion)
                return false;
            if (payload[10] != ViscaProtocol.FrameTerminator)
                return false;

            panRaw = ViscaParser.DecodeNibble16(payload[2], payload[3], payload[4], payload[5]);
            tiltRaw = ViscaParser.DecodeNibble16(payload[6], payload[7], payload[8], payload[9]);
            return true;
        }

        private static bool TryParseZoomReply(byte[] payload, out ushort zoomRaw)
        {
            zoomRaw = 0;

            if (payload == null || payload.Length != 7)
                return false;
            if (payload[0] != 0x90 || (payload[1] & 0xF0) != ViscaProtocol.ResponseCompletion)
                return false;
            if (payload[6] != ViscaProtocol.FrameTerminator)
                return false;

            zoomRaw = ViscaParser.DecodeNibble16(payload[2], payload[3], payload[4], payload[5]);
            return true;
        }

        private void Log(string message)
        {
            _logger?.Invoke(message);
        }
    }
}
