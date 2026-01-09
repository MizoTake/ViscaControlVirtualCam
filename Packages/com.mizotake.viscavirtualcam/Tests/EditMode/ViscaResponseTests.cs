using System;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class ViscaResponseTests
{
    [Test]
    public void SendAck_UsesSocketId()
    {
        byte[] sent = null;
        ViscaResponse.SendAck(b => sent = b, ViscaReplyMode.AckAndCompletion, 0x03);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x43, 0xFF }, sent);
    }

    [Test]
    public void SendCompletion_UsesSocketId()
    {
        byte[] sent = null;
        ViscaResponse.SendCompletion(b => sent = b, ViscaReplyMode.AckAndCompletion, 0x02);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x52, 0xFF }, sent);
    }

    [Test]
    public void SendInquiryResponse_IncludesSocketId()
    {
        byte[] sent = null;
        ViscaResponse.SendInquiryResponse16(b => sent = b, 0x1234, 0x04);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x54, 0x01, 0x02, 0x03, 0x04, 0xFF }, sent);
    }

    [Test]
    public void CommandContext_StoresSocketId()
    {
        var registry = new ViscaCommandRegistry();
        var handler = new CapturingHandler();
        var frame = new byte[] { 0x85, 0x01, 0x06, 0x01, 0x10, 0x05, 0x01, 0x01, 0xFF };

        var context = registry.TryExecute(frame, handler, _ => { });

        Assert.IsTrue(context.HasValue);
        Assert.IsTrue(handler.LastContext.HasValue);
        Assert.AreEqual(0x05, handler.LastContext.Value.SocketId);
    }

    [Test]
    public void ExtractSocketId_DefaultsWhenZero()
    {
        byte id = ViscaProtocol.ExtractSocketId(new byte[] { 0x80, 0x01, 0x06, 0x01, 0x00, 0xFF });

        Assert.AreEqual(ViscaProtocol.DefaultSocketId, id);
    }

    [Test]
    public void ExtractSocketId_CommandCancel_UsesSecondByte()
    {
        byte id = ViscaProtocol.ExtractSocketId(new byte[] { 0x81, 0x25, 0xFF });

        Assert.AreEqual(0x05, id);
    }

    [Test]
    public void CommandCancel_ParsesAndReturnsCancelledError()
    {
        var registry = new ViscaCommandRegistry();
        var handler = new CapturingHandler();
        var frame = new byte[] { 0x81, 0x23, 0xFF };
        byte[] sent = null;

        var context = registry.TryExecute(frame, handler, b => sent = b);

        Assert.IsTrue(context.HasValue);
        Assert.AreEqual(ViscaCommandType.CommandCancel, context.Value.CommandType);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x63, 0x04, 0xFF }, sent);
    }

    private sealed class CapturingHandler : IViscaCommandHandler
    {
        public ViscaCommandContext? LastContext;

        public bool Handle(in ViscaCommandContext context)
        {
            LastContext = context;
            if (context.CommandType == ViscaCommandType.CommandCancel)
            {
                ViscaResponse.SendError(context.Responder, ViscaProtocol.ErrorCommandCancelled, context.SocketId);
            }
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            // Not needed for these tests
        }
    }
}
