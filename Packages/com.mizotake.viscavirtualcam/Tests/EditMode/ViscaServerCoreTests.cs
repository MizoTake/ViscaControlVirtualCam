using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaServerCoreTests
{
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
        byte[] packet = BuildViscaIpPacket(0x01, 0x00, 0x0014, 0x00000009, new byte[20]);
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
        byte[] packet = BuildViscaIpPacket(0x02, 0x00, (ushort)payload.Length, 0xABCDEF01u, payload);
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
        byte[] packet = BuildViscaIpPacket(0x02, 0x00, (ushort)payload.Length, 0x00000002u, payload);
        InvokeProcessFrame(server, packet, sent);
        server.Dispose();

        Assert.AreEqual(1, sent.Count, "Should respond once");
        var resp = sent[0];
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x11, 0x00, 0x04, 0x00, 0x00, 0x00, 0x02 }, resp.Take(8).ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x62, 0x02, 0xFF }, resp.Skip(8).ToArray());
    }

    private static byte[] BuildViscaIpPacket(byte typeMsb, byte typeLsb, ushort payloadLength, uint sequence, byte[] payload)
    {
        byte[] packet = new byte[ViscaProtocol.ViscaIpHeaderLength + payload.Length];
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
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            ViscaResponse.SendError(responder, errorCode, ViscaProtocol.ExtractSocketId(frame));
        }
    }
}
