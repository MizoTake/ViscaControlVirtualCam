using System;

namespace ViscaControlVirtualCam
{
    // Bridges VISCA commands to a PtzModel. Pure C#.
    public class PtzViscaHandler : IViscaCommandHandler
    {
        private readonly PtzModel _model;
        private readonly Action<Action> _mainThreadDispatcher;
        private readonly ViscaReplyMode _replyMode;
        private readonly Action<string> _logger;
        private byte _focusMode = 0x03; // Default: Manual

        public PtzViscaHandler(PtzModel model, Action<Action> mainThreadDispatcher, ViscaReplyMode replyMode, Action<string> logger = null)
        {
            _model = model;
            _mainThreadDispatcher = mainThreadDispatcher ?? (_ => { });
            _replyMode = replyMode;
            _logger = logger;
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

        // Standard VISCA: Pan/Tilt Home
        public bool HandleHome(Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.CommandHome();
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Memory Reset
        public bool HandleMemoryReset(byte memoryNumber, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                _model.DeletePreset(memoryNumber);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Pan/Tilt Reset to center
        public bool HandlePanTiltReset(Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _mainThreadDispatcher(() =>
            {
                // Reset to center position (0, 0)
                _model.CommandPanTiltAbsolute(0, 0, 0x8000, 0x8000);
                SendCompletion(responder, _replyMode);
            });
            return true;
        }

        // Focus Mode (Auto/Manual) - Unity Camera doesn't support auto focus, log only
        public bool HandleFocusMode(byte mode, Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _focusMode = mode;
            string modeName = mode switch { 0x02 => "Auto", 0x03 => "Manual", _ => $"0x{mode:X2}" };
            _logger?.Invoke($"[VISCA] Focus Mode: {modeName} (Unity Camera does not support auto focus)");
            SendCompletion(responder, _replyMode);
            return true;
        }

        // Focus One Push AF - Unity Camera doesn't support auto focus, log only
        public bool HandleFocusOnePush(Action<byte[]> responder)
        {
            SendAck(responder, _replyMode);
            _logger?.Invoke($"[VISCA] Focus One Push AF (Unity Camera does not support auto focus)");
            SendCompletion(responder, _replyMode);
            return true;
        }

        // Inquiry: Pan/Tilt Position
        public bool HandlePanTiltPositionInquiry(Action<byte[]> responder)
        {
            // Convert current position to VISCA format
            float panDeg = _model.CurrentPanDeg;
            float tiltDeg = _model.CurrentTiltDeg;

            // Map to 0-65535 range
            float panNorm = (panDeg - _model.PanMinDeg) / (_model.PanMaxDeg - _model.PanMinDeg);
            float tiltNorm = (tiltDeg - _model.TiltMinDeg) / (_model.TiltMaxDeg - _model.TiltMinDeg);
            ushort panPos = (ushort)(panNorm * 65535f);
            ushort tiltPos = (ushort)(tiltNorm * 65535f);

            // Encode as nibbles: Y0 50 0p 0p 0p 0p 0t 0t 0t 0t FF
            responder(new byte[]
            {
                0x90, 0x50,
                (byte)((panPos >> 12) & 0x0F),
                (byte)((panPos >> 8) & 0x0F),
                (byte)((panPos >> 4) & 0x0F),
                (byte)(panPos & 0x0F),
                (byte)((tiltPos >> 12) & 0x0F),
                (byte)((tiltPos >> 8) & 0x0F),
                (byte)((tiltPos >> 4) & 0x0F),
                (byte)(tiltPos & 0x0F),
                0xFF
            });
            return true;
        }

        // Inquiry: Zoom Position
        public bool HandleZoomPositionInquiry(Action<byte[]> responder)
        {
            // Convert FOV to zoom position (inverse relationship)
            float fovNorm = (_model.CurrentFovDeg - _model.MinFov) / (_model.MaxFov - _model.MinFov);
            ushort zoomPos = (ushort)((1.0f - fovNorm) * 65535f); // Inverted: small FOV = high zoom

            // Encode as nibbles: Y0 50 0p 0p 0p 0p FF
            responder(new byte[]
            {
                0x90, 0x50,
                (byte)((zoomPos >> 12) & 0x0F),
                (byte)((zoomPos >> 8) & 0x0F),
                (byte)((zoomPos >> 4) & 0x0F),
                (byte)(zoomPos & 0x0F),
                0xFF
            });
            return true;
        }

        // Inquiry: Focus Position
        public bool HandleFocusPositionInquiry(Action<byte[]> responder)
        {
            ushort focusPos = (ushort)_model.CurrentFocus;

            // Encode as nibbles: Y0 50 0p 0p 0p 0p FF
            responder(new byte[]
            {
                0x90, 0x50,
                (byte)((focusPos >> 12) & 0x0F),
                (byte)((focusPos >> 8) & 0x0F),
                (byte)((focusPos >> 4) & 0x0F),
                (byte)(focusPos & 0x0F),
                0xFF
            });
            return true;
        }

        // Inquiry: Focus Mode
        public bool HandleFocusModeInquiry(Action<byte[]> responder)
        {
            // Y0 50 02/03 FF (02=Auto, 03=Manual)
            responder(new byte[] { 0x90, 0x50, _focusMode, 0xFF });
            return true;
        }

        public void HandleSyntaxError(byte[] frame, Action<byte[]> responder)
        {
            SendError(responder, 0x02, _replyMode);
        }
    }
}
