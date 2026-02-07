using System;
using System.Collections.Generic;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class PtzViscaHandlerTests
{
    [Test]
    public void PendingQueue_IsPerSocket()
    {
        var model = new PtzModel();
        var queued = new List<Action>();
        Action<Action> dispatcher = act => queued.Add(act);
        var handler = new PtzViscaHandler(model, dispatcher, ViscaReplyMode.AckAndCompletion, null, 1);
        var responses = new List<byte[]>();
        Action<byte[]> responder = bytes => responses.Add(bytes);

        byte[] frame1 = { 0x81, 0x01, 0x06, 0x01, 0x01, 0x01, 0x03, 0x03, 0xFF };
        var ctx1 = ViscaCommandContext.PanTiltDrive(frame1, responder, 0x01, 0x01, 0x03, 0x03);
        handler.Handle(ctx1);

        byte[] frame2 = { 0x81, 0x01, 0x06, 0x01, 0x01, 0x01, 0x03, 0x03, 0xFF };
        var ctx2 = ViscaCommandContext.PanTiltDrive(frame2, responder, 0x01, 0x01, 0x03, 0x03);
        handler.Handle(ctx2);

        byte[] frame3 = { 0x82, 0x01, 0x06, 0x01, 0x01, 0x01, 0x03, 0x03, 0xFF };
        var ctx3 = ViscaCommandContext.PanTiltDrive(frame3, responder, 0x01, 0x01, 0x03, 0x03);
        handler.Handle(ctx3);

        Assert.AreEqual(3, responses.Count, "Expected ack, buffer-full, ack");
        Assert.AreEqual((byte)(ViscaProtocol.ResponseAck | 0x01), responses[0][1]);
        Assert.AreEqual((byte)(ViscaProtocol.ResponseError | 0x01), responses[1][1]);
        Assert.AreEqual(ViscaProtocol.ErrorCommandBuffer, responses[1][2]);
        Assert.AreEqual((byte)(ViscaProtocol.ResponseAck | 0x02), responses[2][1]);
    }

    [Test]
    public void CommandCancel_SuppressesCompletion_OnlyForSocket()
    {
        var model = new PtzModel();
        var actions = new List<Action>();
        Action<Action> dispatcher = act => actions.Add(act);
        var handler = new PtzViscaHandler(model, dispatcher, ViscaReplyMode.AckAndCompletion, null, 8);
        var responses = new List<byte[]>();
        Action<byte[]> responder = bytes => responses.Add(bytes);

        byte[] frame1 = { 0x81, 0x01, 0x06, 0x01, 0x10, 0x10, 0x03, 0x03, 0xFF };
        var ctx1 = ViscaCommandContext.PanTiltDrive(frame1, responder, 0x10, 0x10, 0x03, 0x03);
        handler.Handle(ctx1);

        byte[] frame2 = { 0x82, 0x01, 0x06, 0x01, 0x10, 0x10, 0x03, 0x03, 0xFF };
        var ctx2 = ViscaCommandContext.PanTiltDrive(frame2, responder, 0x10, 0x10, 0x03, 0x03);
        handler.Handle(ctx2);

        var cancelCtx = ViscaCommandContext.CommandCancel(new byte[] { 0x81, 0x21, 0xFF }, responder);
        handler.Handle(cancelCtx);

        foreach (var act in actions) act();

        Assert.AreEqual(4, responses.Count, "Expected ack, ack, cancel, completion");
        Assert.IsTrue(HasSimpleResponse(responses, ViscaProtocol.ResponseAck, 0x01));
        Assert.IsTrue(HasSimpleResponse(responses, ViscaProtocol.ResponseAck, 0x02));
        Assert.IsTrue(HasErrorResponse(responses, ViscaProtocol.ErrorCommandCancelled, 0x01));
        Assert.IsTrue(HasSimpleResponse(responses, ViscaProtocol.ResponseCompletion, 0x02));
        Assert.IsFalse(HasSimpleResponse(responses, ViscaProtocol.ResponseCompletion, 0x01));
    }

    private static bool HasSimpleResponse(List<byte[]> responses, byte baseCode, byte socketId)
    {
        var code = (byte)(baseCode | (socketId & 0x0F));
        foreach (var response in responses)
        {
            if (response.Length == 3 && response[0] == 0x90 && response[1] == code &&
                response[2] == ViscaProtocol.FrameTerminator)
                return true;
        }

        return false;
    }

    private static bool HasErrorResponse(List<byte[]> responses, byte errorCode, byte socketId)
    {
        var code = (byte)(ViscaProtocol.ResponseError | (socketId & 0x0F));
        foreach (var response in responses)
        {
            if (response.Length == 4 && response[0] == 0x90 && response[1] == code &&
                response[2] == errorCode && response[3] == ViscaProtocol.FrameTerminator)
                return true;
        }

        return false;
    }
}
