using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaInquiryClientTests
{
    [Test]
    [Timeout(5000)]
    public void TryGetStatus_WithInquiryReplies_ReturnsDecodedRawValues()
    {
        using (var fakeCamera = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            fakeCamera.Client.ReceiveTimeout = 100;
            var port = ((IPEndPoint)fakeCamera.Client.LocalEndPoint).Port;
            var running = true;
            var worker = new Thread(() => FakeCameraLoop(
                fakeCamera,
                () => running,
                (_, _, _, remote, packetType, sequence) =>
                {
                    if (packetType == InquiryPacketType.PanTilt)
                    {
                        var payload = new byte[]
                        {
                            0x90, 0x51,
                            0x01, 0x02, 0x03, 0x04,
                            0x0A, 0x0B, 0x0C, 0x0D,
                            0xFF
                        };
                        SendReply(fakeCamera, remote, sequence, payload);
                    }
                    else if (packetType == InquiryPacketType.Zoom)
                    {
                        var payload = new byte[]
                        {
                            0x90, 0x51,
                            0x04, 0x05, 0x06, 0x07,
                            0xFF
                        };
                        SendReply(fakeCamera, remote, sequence, payload);
                    }
                }))
            {
                IsBackground = true
            };

            worker.Start();
            try
            {
                using (var client = new ViscaInquiryClient("127.0.0.1", port, timeoutMilliseconds: 200,
                           retryCount: 0))
                {
                    var ok = client.TryGetStatus(out var status);

                    Assert.IsTrue(ok);
                    Assert.AreEqual(0x1234, status.PanRaw);
                    Assert.AreEqual(0xABCD, status.TiltRaw);
                    Assert.AreEqual(0x4567, status.ZoomRaw);
                }
            }
            finally
            {
                running = false;
                worker.Join(500);
            }
        }
    }

    [Test]
    [Timeout(5000)]
    public void TryGetStatus_WhenFirstPanTiltTimesOut_RetriesWithSameSequence()
    {
        using (var fakeCamera = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            fakeCamera.Client.ReceiveTimeout = 100;
            var port = ((IPEndPoint)fakeCamera.Client.LocalEndPoint).Port;
            var running = true;
            var panTiltSequences = new List<uint>();
            var zoomSequences = new List<uint>();
            var panTiltRequestCount = 0;
            var sync = new object();

            var worker = new Thread(() => FakeCameraLoop(
                fakeCamera,
                () => running,
                (_, _, _, remote, packetType, sequence) =>
                {
                    if (packetType == InquiryPacketType.PanTilt)
                    {
                        lock (sync)
                        {
                            panTiltSequences.Add(sequence);
                            panTiltRequestCount++;
                            if (panTiltRequestCount == 1)
                                return; // drop first request to force retransmission
                        }

                        var payload = new byte[]
                        {
                            0x90, 0x51,
                            0x00, 0x00, 0x00, 0x01,
                            0x00, 0x00, 0x00, 0x02,
                            0xFF
                        };
                        SendReply(fakeCamera, remote, sequence, payload);
                    }
                    else if (packetType == InquiryPacketType.Zoom)
                    {
                        lock (sync)
                        {
                            zoomSequences.Add(sequence);
                        }

                        var payload = new byte[]
                        {
                            0x90, 0x51,
                            0x00, 0x00, 0x00, 0x03,
                            0xFF
                        };
                        SendReply(fakeCamera, remote, sequence, payload);
                    }
                }))
            {
                IsBackground = true
            };

            worker.Start();
            try
            {
                using (var client = new ViscaInquiryClient("127.0.0.1", port, timeoutMilliseconds: 80,
                           retryCount: 1))
                {
                    var ok = client.TryGetStatus(out _);

                    Assert.IsTrue(ok, "Retry should recover from first timeout.");
                }

                lock (sync)
                {
                    Assert.AreEqual(2, panTiltRequestCount, "Pan/Tilt inquiry should be sent twice.");
                    Assert.AreEqual(2, panTiltSequences.Count);
                    Assert.AreEqual(panTiltSequences[0], panTiltSequences[1],
                        "Retransmission must reuse same sequence number.");
                    Assert.AreEqual(1, zoomSequences.Count);
                    Assert.AreEqual(unchecked(panTiltSequences[0] + 1u), zoomSequences[0],
                        "Next inquiry should increment sequence number.");
                }
            }
            finally
            {
                running = false;
                worker.Join(500);
            }
        }
    }

    private static void FakeCameraLoop(UdpClient fakeCamera, Func<bool> isRunning,
        Action<byte, byte, byte[], IPEndPoint, InquiryPacketType, uint> onPacket)
    {
        while (isRunning())
        {
            try
            {
                var remote = new IPEndPoint(IPAddress.Any, 0);
                var request = fakeCamera.Receive(ref remote);
                if (!TryParseViscaInquiry(request, out var typeMsb, out var typeLsb, out var payload, out var sequence))
                    continue;
                if (typeMsb != ViscaProtocol.IpPayloadTypeMsbVisca ||
                    typeLsb != ViscaProtocol.IpPayloadTypeLsbInquiry)
                    continue;

                var packetType = ClassifyInquiry(payload);
                if (packetType == InquiryPacketType.Unknown)
                    continue;

                onPacket(typeMsb, typeLsb, payload, remote, packetType, sequence);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private static void SendReply(UdpClient fakeCamera, IPEndPoint remote, uint sequence, byte[] payload)
    {
        var packet = new byte[ViscaProtocol.ViscaIpHeaderLength + payload.Length];
        packet[0] = ViscaProtocol.IpPayloadTypeMsbVisca;
        packet[1] = ViscaProtocol.IpPayloadTypeLsbReply;
        packet[2] = (byte)((payload.Length >> 8) & 0xFF);
        packet[3] = (byte)(payload.Length & 0xFF);
        packet[4] = (byte)((sequence >> 24) & 0xFF);
        packet[5] = (byte)((sequence >> 16) & 0xFF);
        packet[6] = (byte)((sequence >> 8) & 0xFF);
        packet[7] = (byte)(sequence & 0xFF);
        Buffer.BlockCopy(payload, 0, packet, ViscaProtocol.ViscaIpHeaderLength, payload.Length);
        fakeCamera.Send(packet, packet.Length, remote);
    }

    private static bool TryParseViscaInquiry(byte[] packet, out byte typeMsb, out byte typeLsb, out byte[] payload,
        out uint sequence)
    {
        typeMsb = 0;
        typeLsb = 0;
        payload = null;
        sequence = 0;

        if (packet == null || packet.Length < ViscaProtocol.ViscaIpHeaderLength)
            return false;

        typeMsb = packet[0];
        typeLsb = packet[1];
        var payloadLength = (packet[2] << 8) | packet[3];
        if (payloadLength <= 0 || packet.Length != ViscaProtocol.ViscaIpHeaderLength + payloadLength)
            return false;

        sequence = (uint)((packet[4] << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7]);
        payload = new byte[payloadLength];
        Buffer.BlockCopy(packet, ViscaProtocol.ViscaIpHeaderLength, payload, 0, payloadLength);
        return true;
    }

    private static InquiryPacketType ClassifyInquiry(byte[] payload)
    {
        if (payload == null || payload.Length != 5)
            return InquiryPacketType.Unknown;
        if (payload[1] != ViscaProtocol.CategoryInquiry)
            return InquiryPacketType.Unknown;

        if (payload[2] == ViscaProtocol.GroupPanTilt && payload[3] == 0x12)
            return InquiryPacketType.PanTilt;
        if (payload[2] == ViscaProtocol.GroupCamera && payload[3] == 0x47)
            return InquiryPacketType.Zoom;
        return InquiryPacketType.Unknown;
    }

    private enum InquiryPacketType
    {
        Unknown,
        PanTilt,
        Zoom
    }
}
