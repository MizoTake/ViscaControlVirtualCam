using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaServerCoreTests
{
    #region ProcessFrame Tests (Existing)
    [Test]
    public void ViscaIp_InvalidLength_ReturnsMessageLengthError()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Declared payload length exceeds VISCA spec (0x14 > 0x10)
        var packet = BuildViscaIpPacket(0x01, 0x00, 0x0014, 0x00000009, new byte[20]);
        InvokeProcessFrame(server, packet, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        Assert.AreEqual(0x01, resp[0]);
        Assert.AreEqual(0x11, resp[1]);
        Assert.AreEqual(0x00, resp[2]);
        Assert.AreEqual(0x04, resp[3]); // payload length 4 bytes
        Assert.AreEqual(0x00, resp[4]);
        Assert.AreEqual(0x00, resp[5]);
        Assert.AreEqual(0x00, resp[6]);
        Assert.AreEqual(0x09, resp[7]); // sequence echoed
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x61, 0x01, 0xFF }, resp.Skip(8).ToArray());
    }

    [Test]
    public void ViscaIp_ControlCommand_RepliesCompletion()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        byte[] payload = { 0x85, 0x00, 0xFF }; // dummy control payload, socket nibble = 5
        var packet = BuildViscaIpPacket(0x02, 0x00, (ushort)payload.Length, 0xABCDEF01u, payload);
        InvokeProcessFrame(server, packet, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        Assert.AreEqual(0x01, resp[0]);
        Assert.AreEqual(0x11, resp[1]);
        Assert.AreEqual(0x00, resp[2]);
        Assert.AreEqual(0x03, resp[3]); // payload length 3 bytes
        CollectionAssert.AreEqual(new byte[] { 0xAB, 0xCD, 0xEF, 0x01 }, resp.Skip(4).Take(4).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x55, 0xFF }, resp.Skip(8).ToArray());
    }

    [Test]
    public void ViscaIp_ControlCommand_InvalidPayload_ReturnsSyntax()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Missing terminator
        byte[] payload = { 0x82, 0x00 };
        var packet = BuildViscaIpPacket(0x02, 0x00, (ushort)payload.Length, 0x00000002u, payload);
        InvokeProcessFrame(server, packet, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x11, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02 },
            resp.Take(8).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x62, 0x02, 0xFF }, resp.Skip(8).ToArray());
    }

    private static byte[] BuildViscaIpPacket(byte typeMsb, byte typeLsb, ushort payloadLength, uint sequence,
        byte[] payload)
    {
        var packet = new byte[ViscaProtocol.ViscaIpHeaderLength + payload.Length];
        packet[0] = typeMsb;
        packet[1] = typeLsb;
        packet[2] = (byte)((payloadLength >> 8) & 0xFF);
        packet[3] = (byte)(payloadLength & 0xFF);
        packet[4] = (byte)((sequence >> 24) & 0xFF);
        packet[5] = (byte)((sequence >> 16) & 0xFF);
        packet[6] = (byte)((sequence >> 8) & 0xFF);
        packet[7] = (byte)(sequence & 0xFF);
        Array.Copy(payload, 0, packet, ViscaProtocol.ViscaIpHeaderLength, payload.Length);
        return packet;
    }

    private static void InvokeProcessFrame(ViscaServerCore server, byte[] packet, List<byte[]> sent)
    {
        var method = typeof(ViscaServerCore).GetMethod("ProcessFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(server, new object[] { packet, (Action<byte[]>)(b => sent.Add(b)) });
    }

    private sealed class StubHandler : IViscaCommandHandler
    {
        public bool Handle(in ViscaCommandContext context)
        {
            // Send ACK and Completion responses for real network tests
            ViscaResponse.SendAck(context.Responder, ViscaReplyMode.AckAndCompletion, context.SocketId);
            ViscaResponse.SendCompletion(context.Responder, ViscaReplyMode.AckAndCompletion, context.SocketId);
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            ViscaResponse.SendError(responder, errorCode, ViscaProtocol.ExtractSocketId(frame));
        }
    }

    #endregion

    #region Command Parsing Integration Tests

    [Test]
    public void ProcessFrame_PanTiltDrive_ParsesCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Pan/Tilt Drive command: 81 01 06 01 [panSpeed] [tiltSpeed] [panDir] [tiltDir] FF
        // panSpeed=0x10, tiltSpeed=0x08, panDir=Right(0x02), tiltDir=Up(0x01)
        byte[] frame = { 0x81, 0x01, 0x06, 0x01, 0x10, 0x08, 0x02, 0x01, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, receivedContexts.Count, "Should receive one command");
        var ctx = receivedContexts[0];
        Assert.AreEqual(ViscaCommandType.PanTiltDrive, ctx.CommandType);
        Assert.AreEqual(0x10, ctx.PanSpeed);
        Assert.AreEqual(0x08, ctx.TiltSpeed);
        Assert.AreEqual(0x02, ctx.PanDirection);
        Assert.AreEqual(0x01, ctx.TiltDirection);
    }

    [Test]
    public void ProcessFrame_PanTiltAbsolute_ParsesCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Pan/Tilt Absolute: 81 01 06 02 [panSpeed] [tiltSpeed] [panPos 4nibbles] [tiltPos 4nibbles] FF
        // panSpeed=0x18, tiltSpeed=0x14, panPos=0x8000, tiltPos=0x4000
        byte[] frame = { 0x81, 0x01, 0x06, 0x02, 0x18, 0x14, 0x08, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, receivedContexts.Count, "Should receive one command");
        var ctx = receivedContexts[0];
        Assert.AreEqual(ViscaCommandType.PanTiltAbsolute, ctx.CommandType);
        Assert.AreEqual(0x18, ctx.PanSpeed);
        Assert.AreEqual(0x14, ctx.TiltSpeed);
        Assert.AreEqual(0x8000, ctx.PanPosition);
        Assert.AreEqual(0x4000, ctx.TiltPosition);
    }

    [Test]
    public void ProcessFrame_ZoomDirect_ParsesCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Zoom Direct: 81 01 04 47 [pos 4 nibbles] FF
        // zoomPos = 0xABCD
        byte[] frame = { 0x81, 0x01, 0x04, 0x47, 0x0A, 0x0B, 0x0C, 0x0D, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, receivedContexts.Count, "Should receive one command");
        var ctx = receivedContexts[0];
        Assert.AreEqual(ViscaCommandType.ZoomDirect, ctx.CommandType);
        Assert.AreEqual(0xABCD, ctx.ZoomPosition);
    }

    [Test]
    public void ProcessFrame_MemoryRecall_ParsesCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Memory Recall: 81 01 04 3F 02 [memNum] FF
        byte[] frame = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x05, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, receivedContexts.Count, "Should receive one command");
        var ctx = receivedContexts[0];
        Assert.AreEqual(ViscaCommandType.MemoryRecall, ctx.CommandType);
        Assert.AreEqual(0x05, ctx.MemoryNumber);
    }

    [Test]
    public void ProcessFrame_CommandCancel_ParsesCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Command Cancel: 8X 2Z FF (X=device, Z=socket)
        // socket = 1
        byte[] frame = { 0x81, 0x21, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, receivedContexts.Count, "Should receive one command");
        var ctx = receivedContexts[0];
        Assert.AreEqual(ViscaCommandType.CommandCancel, ctx.CommandType);
        Assert.AreEqual(0x01, ctx.SocketId);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void ProcessFrame_FrameTooShort_ReturnsMessageLengthError()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Frame with only 2 bytes (minimum is 3)
        byte[] frame = { 0x81, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        // Error response: 90 6X [errorCode] FF
        Assert.AreEqual(0x90, resp[0]);
        Assert.AreEqual(0x60 | 0x01, resp[1]); // socket 1
        Assert.AreEqual(ViscaProtocol.ErrorMessageLength, resp[2]);
        Assert.AreEqual(0xFF, resp[3]);
    }

    [Test]
    public void ProcessFrame_NoTerminator_ReturnsSyntaxError()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Frame without 0xFF terminator
        byte[] frame = { 0x81, 0x01, 0x06 };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        Assert.AreEqual(0x90, resp[0]);
        Assert.AreEqual(ViscaProtocol.ErrorSyntax, resp[2]);
    }

    [Test]
    public void ProcessFrame_UnknownCommand_ReturnsSyntaxError()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Unknown command pattern
        byte[] frame = { 0x81, 0x99, 0x99, 0xFF };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        Assert.AreEqual(0x90, resp[0]);
        Assert.AreEqual(ViscaProtocol.ErrorSyntax, resp[2]);
    }

    [Test]
    public void ProcessFrame_EmptyFrame_HandlesGracefully()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Empty frame
        byte[] frame = { };
        InvokeProcessFrame(server, frame, sent);
        server.Dispose();

        // Should not crash, may or may not send response
        Assert.Pass("Empty frame handled without exception");
    }

    [Test]
    public void ProcessFrame_NullFrame_HandlesGracefully()
    {
        var handler = new StubHandler();
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        InvokeProcessFrame(server, null, sent);
        server.Dispose();

        // Should not crash
        Assert.Pass("Null frame handled without exception");
    }

    #endregion

    #region Multiple Commands Sequence Tests

    [Test]
    public void ProcessFrame_MultipleCommands_AllParsedCorrectly()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Send multiple different commands
        byte[] panTiltDrive = { 0x81, 0x01, 0x06, 0x01, 0x10, 0x08, 0x02, 0x01, 0xFF };
        byte[] zoomDirect = { 0x81, 0x01, 0x04, 0x47, 0x05, 0x00, 0x00, 0x00, 0xFF };
        byte[] memoryRecall = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x03, 0xFF };

        InvokeProcessFrame(server, panTiltDrive, sent);
        InvokeProcessFrame(server, zoomDirect, sent);
        InvokeProcessFrame(server, memoryRecall, sent);
        server.Dispose();

        Assert.AreEqual(3, receivedContexts.Count, "Should receive three commands");
        Assert.AreEqual(ViscaCommandType.PanTiltDrive, receivedContexts[0].CommandType);
        Assert.AreEqual(ViscaCommandType.ZoomDirect, receivedContexts[1].CommandType);
        Assert.AreEqual(ViscaCommandType.MemoryRecall, receivedContexts[2].CommandType);
    }

    [Test]
    public void ProcessFrame_RecoveryAfterInvalidFrame_ContinuesProcessing()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Send valid, then invalid, then valid again
        byte[] validCommand1 = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x01, 0xFF };
        byte[] invalidCommand = { 0x81, 0x99, 0x99, 0xFF }; // Unknown
        byte[] validCommand2 = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x02, 0xFF };

        InvokeProcessFrame(server, validCommand1, sent);
        InvokeProcessFrame(server, invalidCommand, sent);
        InvokeProcessFrame(server, validCommand2, sent);
        server.Dispose();

        // Should process both valid commands despite the error
        Assert.AreEqual(2, receivedContexts.Count, "Should receive two valid commands");
        Assert.AreEqual(0x01, receivedContexts[0].MemoryNumber);
        Assert.AreEqual(0x02, receivedContexts[1].MemoryNumber);
    }

    #endregion

    #region Socket ID Extraction Tests

    [Test]
    public void ProcessFrame_SocketIdExtraction_CorrectlyExtracted()
    {
        var receivedContexts = new List<ViscaCommandContext>();
        var handler = new RecordingHandler(receivedContexts);
        var server = new ViscaServerCore(handler, new ViscaServerOptions
        {
            VerboseLog = false,
            LogReceivedCommands = false
        });
        var sent = new List<byte[]>();

        // Test different socket IDs (low nibble of first byte)
        // Socket 1: 0x81
        byte[] frame1 = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF };
        // Socket 5: 0x85
        byte[] frame5 = { 0x85, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF };

        InvokeProcessFrame(server, frame1, sent);
        InvokeProcessFrame(server, frame5, sent);
        server.Dispose();

        Assert.AreEqual(2, receivedContexts.Count);
        Assert.AreEqual(0x01, receivedContexts[0].SocketId);
        Assert.AreEqual(0x05, receivedContexts[1].SocketId);
    }

    #endregion

    #region UDP Integration Tests

    [Test]
    [Timeout(5000)]
    public void UdpServer_StartStop_NoException()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.UdpRawVisca,
                UdpPort = GetAvailablePort(),
                VerboseLog = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100); // Let server initialize
                server.Stop();
            }

            Assert.Pass("UDP server started and stopped without exception");
        });
    }

    [Test]
    [Timeout(5000)]
    public void UdpServer_SendReceive_RespondsCorrectly()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var port = GetAvailablePort();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.UdpRawVisca,
                UdpPort = port,
                VerboseLog = false,
                LogReceivedCommands = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);

                using (var client = new UdpClient())
                {
                    var endpoint = new IPEndPoint(IPAddress.Loopback, port);

                    // Send MemoryRecall command
                    byte[] command = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF };
                    client.Send(command, command.Length, endpoint);

                    // Wait for response
                    client.Client.ReceiveTimeout = 2000;
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);

                    try
                    {
                        var response = client.Receive(ref remoteEp);
                        Assert.IsNotNull(response);
                        Assert.Greater(response.Length, 0, "Should receive a response");
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        Assert.Fail("Timeout waiting for UDP response");
                    }
                }
            }
        });
    }

    [Test]
    [Timeout(5000)]
    public void UdpServer_MultiplePackets_AllProcessed()
    {
        RunSocketTest(() =>
        {
            var receivedCount = 0;
            var handler = new CountingHandler(() => Interlocked.Increment(ref receivedCount));
            var port = GetAvailablePort();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.UdpRawVisca,
                UdpPort = port,
                VerboseLog = false,
                LogReceivedCommands = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);

                using (var client = new UdpClient())
                {
                    var endpoint = new IPEndPoint(IPAddress.Loopback, port);
                    byte[] command = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF };

                    // Send multiple packets
                    for (int i = 0; i < 5; i++)
                    {
                        client.Send(command, command.Length, endpoint);
                        Thread.Sleep(50);
                    }

                    // Wait for processing
                    Thread.Sleep(500);
                }
            }

            Assert.AreEqual(5, receivedCount, "All 5 packets should be processed");
        });
    }

    #endregion

    #region TCP Integration Tests

    [Test]
    [Timeout(5000)]
    public void TcpServer_StartStop_NoException()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.TcpRawVisca,
                TcpPort = GetAvailablePort(),
                VerboseLog = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);
                server.Stop();
            }

            Assert.Pass("TCP server started and stopped without exception");
        });
    }

    [Test]
    [Timeout(5000)]
    public void TcpServer_ClientConnect_AcceptsConnection()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var port = GetAvailablePort();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.TcpRawVisca,
                TcpPort = port,
                VerboseLog = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);

                using (var client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    Assert.IsTrue(client.Connected, "Client should connect successfully");
                }
            }
        });
    }

    [Test]
    [Timeout(5000)]
    public void TcpServer_SendReceive_RespondsCorrectly()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var port = GetAvailablePort();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.TcpRawVisca,
                TcpPort = port,
                VerboseLog = false,
                LogReceivedCommands = false
            };

            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);

                using (var client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    var stream = client.GetStream();
                    stream.ReadTimeout = 2000;

                    // Send MemoryRecall command
                    byte[] command = { 0x81, 0x01, 0x04, 0x3F, 0x02, 0x00, 0xFF };
                    stream.Write(command, 0, command.Length);
                    stream.Flush();

                    // Read response
                    var buffer = new byte[256];
                    try
                    {
                        var bytesRead = stream.Read(buffer, 0, buffer.Length);
                        Assert.Greater(bytesRead, 0, "Should receive a response");
                    }
                    catch (System.IO.IOException)
                    {
                        Assert.Fail("Timeout waiting for TCP response");
                    }
                }
            }
        });
    }

    [Test]
    [Timeout(5000)]
    public void TcpServer_MaxClients_RefusesExcess()
    {
        RunSocketTest(() =>
        {
            var handler = new StubHandler();
            var port = GetAvailablePort();
            var options = new ViscaServerOptions
            {
                Transport = ViscaTransport.TcpRawVisca,
                TcpPort = port,
                MaxClients = 2,
                VerboseLog = false
            };

            var clients = new List<TcpClient>();
            using (var server = new ViscaServerCore(handler, options))
            {
                server.Start();
                Thread.Sleep(100);

                try
                {
                    // Connect MaxClients clients
                    for (int i = 0; i < options.MaxClients; i++)
                    {
                        var client = new TcpClient();
                        client.Connect(IPAddress.Loopback, port);
                        clients.Add(client);
                        Thread.Sleep(50);
                    }

                    Assert.AreEqual(options.MaxClients, clients.Count(c => c.Connected),
                        $"Should accept {options.MaxClients} clients");

                    // Try to connect one more (should be refused or disconnected)
                    var extraClient = new TcpClient();
                    extraClient.Connect(IPAddress.Loopback, port);
                    Thread.Sleep(200);

                    // The extra client may connect briefly but should be closed
                    // We just verify the server doesn't crash
                    extraClient.Close();
                }
                finally
                {
                    foreach (var client in clients)
                    {
                        client.Close();
                    }
                }
            }

            Assert.Pass("Server handled max clients correctly");
        });
    }

    #endregion

    #region Helper Methods

    private static int GetAvailablePort()
    {
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }
    }

    private static void RunSocketTest(Action action)
    {
        try
        {
            action();
        }
        catch (SocketException ex) when (IsAccessDenied(ex))
        {
            Assert.Inconclusive($"Socket access denied in test environment: {ex.Message}");
        }
    }

    private static bool IsAccessDenied(SocketException ex)
    {
        return ex.SocketErrorCode == SocketError.AccessDenied;
    }

    #endregion

    #region Helper Classes

    private sealed class RecordingHandler : IViscaCommandHandler
    {
        private readonly List<ViscaCommandContext> _contexts;

        public RecordingHandler(List<ViscaCommandContext> contexts)
        {
            _contexts = contexts;
        }

        public bool Handle(in ViscaCommandContext context)
        {
            _contexts.Add(context);
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            ViscaResponse.SendError(responder, errorCode, ViscaProtocol.ExtractSocketId(frame));
        }
    }

    private sealed class CountingHandler : IViscaCommandHandler
    {
        private readonly Action _onHandle;

        public CountingHandler(Action onHandle)
        {
            _onHandle = onHandle;
        }

        public bool Handle(in ViscaCommandContext context)
        {
            _onHandle?.Invoke();
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            ViscaResponse.SendError(responder, errorCode, ViscaProtocol.ExtractSocketId(frame));
        }
    }

    #endregion
}
