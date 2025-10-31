using System;

namespace ViscaControlVirtualCam
{
    // Bridges VISCA commands to a PtzModel. Pure C#.
    public class PtzViscaHandler : IViscaCommandHandler
    {
        private readonly PtzModel _model;
        private readonly Action<Action> _mainThreadDispatcher;
        private readonly ViscaReplyMode _replyMode;

        public PtzViscaHandler(PtzModel model, Action<Action> mainThreadDispatcher, ViscaReplyMode replyMode)
        {
            _model = model;
            _mainThreadDispatcher = mainThreadDispatcher ?? (_ => { });
            _replyMode = replyMode;
        }

        private static void SendAck(Action<byte[]> responder, ViscaReplyMode mode)
        {
            if (mode == ViscaReplyMode.None) return;
            responder(new byte[] { 0x90, 0x40, 0xFF });
        }

        private static void SendCompletion(Action<byte[]> responder, ViscaReplyMode mode)
        {
            if (mode != ViscaReplyMode.AckAndCompletion) return;
            responder(new byte[] { 0x90, 0x50, 0xFF });
        }

        private static void SendError(Action<byte[]> responder, byte ee, ViscaReplyMode mode)
        {
            if (mode == ViscaReplyMode.None) return;
            responder(new byte[] { 0x90, 0x60, ee, 0xFF });
        }

        public bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            var pdir = ViscaParser.DirFromVisca(panDir);
            var tdir = ViscaParser.DirFromVisca(tiltDir);
            _mainThreadDispatcher(() =>
            {
                _model.CommandPanTiltVariable(panSpeed, tiltSpeed, pdir, tdir);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        public bool HandleZoomVariable(byte zz, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandZoomVariable(zz);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        public bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandPanTiltAbsolute(panSpeed, tiltSpeed, panPos, tiltPos);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Zoom Direct
        public bool HandleZoomDirect(ushort zoomPos, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandZoomDirect(zoomPos);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Focus Variable
        public bool HandleFocusVariable(byte focusSpeed, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandFocusVariable(focusSpeed);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Focus Direct
        public bool HandleFocusDirect(ushort focusPos, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandFocusDirect(focusPos);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Iris Variable
        public bool HandleIrisVariable(byte irisDir, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandIrisVariable(irisDir);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Iris Direct
        public bool HandleIrisDirect(ushort irisPos, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandIrisDirect(irisPos);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Memory Recall
        public bool HandleMemoryRecall(byte memoryNumber, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandMemoryRecall(memoryNumber);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Blackmagic PTZ Control: Memory Set
        public bool HandleMemorySet(byte memoryNumber, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandMemorySet(memoryNumber);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        public void HandleSyntaxError(byte[] frame, Action<byte[]> responder)
        {
            SendError(responder, 0x02, _replyMode);
        }
    }
}
