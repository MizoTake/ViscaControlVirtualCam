using System;
using System.Collections.Generic;
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
        var id = ViscaProtocol.ExtractSocketId(new byte[] { 0x80, 0x01, 0x06, 0x01, 0x00, 0xFF });

        Assert.AreEqual(ViscaProtocol.DefaultSocketId, id);
    }

    [Test]
    public void ExtractSocketId_CommandCancel_UsesSecondByte()
    {
        var id = ViscaProtocol.ExtractSocketId(new byte[] { 0x81, 0x25, 0xFF });

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

    [Test]
    public void CommandCancel_NoPending_StillReturnsCanceled()
    {
        var model = new PtzModel();
        byte[] sent = null;
        var handler = new PtzViscaHandler(model, a => a(), ViscaReplyMode.AckAndCompletion, _ => { });
        var ctx = ViscaCommandContext.CommandCancel(new byte[] { 0x81, 0x21, 0xFF }, b => sent = b);

        handler.Handle(in ctx);

        CollectionAssert.AreEqual(new byte[] { 0x90, 0x61, 0x04, 0xFF }, sent);
    }

    [Test]
    public void CommandCancel_SuppressesCompletionOfPendingActions()
    {
        var model = new PtzModel();
        var sent = new System.Collections.Generic.List<byte[]>();
        var actions = new System.Collections.Generic.List<Action>();
        var handler = new PtzViscaHandler(model, a => actions.Add(a), ViscaReplyMode.AckAndCompletion, _ => { });

        var moveCtx = ViscaCommandContext.PanTiltDrive(
            new byte[] { 0x85, 0x01, 0x06, 0x01, 0x10, 0x10, 0x02, 0x02, 0xFF },
            b => sent.Add(b),
            0x10, 0x10, 0x02, 0x02);

        handler.Handle(in moveCtx); // enqueues action, sends ACK

        var cancelCtx = ViscaCommandContext.CommandCancel(new byte[] { 0x85, 0x25, 0xFF }, b => sent.Add(b));
        handler.Handle(in cancelCtx); // sends cancel

        // Execute pending action after cancel; completion should be suppressed
        foreach (var act in actions) act();

        Assert.AreEqual(2, sent.Count, "Only ACK and Cancel should be sent");
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x45, 0xFF }, sent[0]);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x65, 0x04, 0xFF }, sent[1]);
    }

    [Test]
    public void InterfaceClear_ReturnsCompletionOnly()
    {
        var model = new PtzModel();
        var handler = new PtzViscaHandler(model, a => a(), ViscaReplyMode.AckAndCompletion, _ => { });
        var registry = new ViscaCommandRegistry();
        var sent = new List<byte[]>();
        var frame = new byte[] { 0x83, 0x01, 0x00, 0x01, 0xFF };

        var context = registry.TryExecute(frame, handler, b => sent.Add(b));

        Assert.IsTrue(context.HasValue);
        Assert.AreEqual(ViscaCommandType.InterfaceClear, context.Value.CommandType);
        Assert.AreEqual(1, sent.Count, "IF_Clear should return completion only");
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x53, 0xFF }, sent[0]);
    }

    [Test]
    public void CameraPowerInquiry_ReturnsPowerOnByDefault()
    {
        var model = new PtzModel();
        var handler = new PtzViscaHandler(model, a => a(), ViscaReplyMode.AckAndCompletion, _ => { });
        var registry = new ViscaCommandRegistry();
        byte[] sent = null;

        var context = registry.TryExecute(new byte[] { 0x84, 0x09, 0x04, 0x00, 0xFF }, handler, b => sent = b);

        Assert.IsTrue(context.HasValue);
        Assert.AreEqual(ViscaCommandType.CameraPowerInquiry, context.Value.CommandType);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x54, ViscaProtocol.PowerOn, 0xFF }, sent);
    }

    [Test]
    public void CameraPower_SetOffThenInquiry_ReturnsPowerOff()
    {
        var model = new PtzModel();
        var handler = new PtzViscaHandler(model, a => a(), ViscaReplyMode.AckAndCompletion, _ => { });
        var registry = new ViscaCommandRegistry();
        var responses = new List<byte[]>();

        var setContext = registry.TryExecute(
            new byte[] { 0x82, 0x01, 0x04, 0x00, ViscaProtocol.PowerOff, 0xFF },
            handler,
            b => responses.Add(b));

        Assert.IsTrue(setContext.HasValue);
        Assert.AreEqual(ViscaCommandType.CameraPower, setContext.Value.CommandType);
        Assert.AreEqual(2, responses.Count, "Power set should return ACK and Completion");
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x42, 0xFF }, responses[0]);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x52, 0xFF }, responses[1]);

        responses.Clear();
        registry.TryExecute(new byte[] { 0x82, 0x09, 0x04, 0x00, 0xFF }, handler, b => responses.Add(b));

        Assert.AreEqual(1, responses.Count);
        CollectionAssert.AreEqual(new byte[] { 0x90, 0x52, ViscaProtocol.PowerOff, 0xFF }, responses[0]);
    }

    [Test]
    public void VersionInquiry_ReturnsVersionPayload()
    {
        var model = new PtzModel();
        var handler = new PtzViscaHandler(model, a => a(), ViscaReplyMode.AckAndCompletion, _ => { });
        var registry = new ViscaCommandRegistry();
        byte[] sent = null;

        var context = registry.TryExecute(new byte[] { 0x85, 0x09, 0x00, 0x02, 0xFF }, handler, b => sent = b);

        Assert.IsTrue(context.HasValue);
        Assert.AreEqual(ViscaCommandType.VersionInquiry, context.Value.CommandType);
        Assert.IsNotNull(sent);
        Assert.AreEqual(10, sent.Length);
        Assert.AreEqual(0x90, sent[0]);
        Assert.AreEqual(0x55, sent[1]);
        Assert.AreEqual(ViscaProtocol.VersionMaxSocketCount, sent[8]);
        Assert.AreEqual(0xFF, sent[9]);
    }

    private sealed class CapturingHandler : IViscaCommandHandler
    {
        public ViscaCommandContext? LastContext;

        public bool Handle(in ViscaCommandContext context)
        {
            LastContext = context;
            if (context.CommandType == ViscaCommandType.CommandCancel)
                ViscaResponse.SendError(context.Responder, ViscaProtocol.ErrorCommandCancelled, context.SocketId);
            return true;
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            // Not needed for these tests
        }
    }
}
