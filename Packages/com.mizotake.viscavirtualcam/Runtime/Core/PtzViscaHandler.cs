using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Bridges VISCA commands to a PtzModel. Pure C#.
    /// Implements the unified IViscaCommandHandler interface.
    /// </summary>
    public sealed class PtzViscaHandler : IViscaCommandHandler
    {
        private readonly PtzModel _model;
        private readonly Action<Action> _mainThreadDispatcher;
        private readonly ViscaReplyMode _replyMode;
        private readonly Action<string> _logger;
        private byte _focusMode = ViscaProtocol.FocusModeManual;

        public PtzViscaHandler(PtzModel model, Action<Action> mainThreadDispatcher, ViscaReplyMode replyMode, Action<string> logger = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _mainThreadDispatcher = mainThreadDispatcher ?? throw new ArgumentNullException(nameof(mainThreadDispatcher));
            _replyMode = replyMode;
            _logger = logger;
        }

        public bool Handle(in ViscaCommandContext ctx)
        {
            return ctx.CommandType switch
            {
                // Pan/Tilt commands
                ViscaCommandType.PanTiltDrive => HandlePanTiltDrive(ctx),
                ViscaCommandType.PanTiltAbsolute => HandlePanTiltAbsolute(ctx),
                ViscaCommandType.PanTiltHome => HandleHome(ctx),
                ViscaCommandType.PanTiltReset => HandlePanTiltReset(ctx),

                // Zoom commands
                ViscaCommandType.ZoomVariable => HandleZoomVariable(ctx),
                ViscaCommandType.ZoomDirect => HandleZoomDirect(ctx),

                // Focus commands
                ViscaCommandType.FocusVariable => HandleFocusVariable(ctx),
                ViscaCommandType.FocusDirect => HandleFocusDirect(ctx),
                ViscaCommandType.FocusMode => HandleFocusMode(ctx),
                ViscaCommandType.FocusOnePush => HandleFocusOnePush(ctx),

                // Iris commands
                ViscaCommandType.IrisVariable => HandleIrisVariable(ctx),
                ViscaCommandType.IrisDirect => HandleIrisDirect(ctx),

                // Memory commands
                ViscaCommandType.MemoryRecall => HandleMemoryRecall(ctx),
                ViscaCommandType.MemorySet => HandleMemorySet(ctx),
                ViscaCommandType.MemoryReset => HandleMemoryReset(ctx),

                // Inquiry commands
                ViscaCommandType.PanTiltPositionInquiry => HandlePanTiltPositionInquiry(ctx),
                ViscaCommandType.ZoomPositionInquiry => HandleZoomPositionInquiry(ctx),
                ViscaCommandType.FocusPositionInquiry => HandleFocusPositionInquiry(ctx),
                ViscaCommandType.FocusModeInquiry => HandleFocusModeInquiry(ctx),

                // Control commands
                ViscaCommandType.CommandCancel => HandleCommandCancel(ctx),

                _ => false
            };
        }

        public void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode)
        {
            byte socketId = ViscaProtocol.ExtractSocketId(frame);
            ViscaResponse.SendError(responder, errorCode, socketId);
        }

        private bool HandlePanTiltDrive(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var pdir = ViscaParser.DirFromVisca(ctx.PanDirection);
            var tdir = ViscaParser.DirFromVisca(ctx.TiltDirection);
            var responder = ctx.Responder;
            byte panSpeed = ctx.PanSpeed;
            byte tiltSpeed = ctx.TiltSpeed;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandPanTiltVariable(panSpeed, tiltSpeed, pdir, tdir);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandlePanTiltAbsolute(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            ushort panPos = ctx.PanPosition;
            ushort tiltPos = ctx.TiltPosition;
            byte panSpeed = ctx.PanSpeed;
            byte tiltSpeed = ctx.TiltSpeed;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandPanTiltAbsolute(panSpeed, tiltSpeed, panPos, tiltPos);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleHome(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandHome();
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandlePanTiltReset(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandPanTiltAbsolute(0, 0, ViscaProtocol.PositionCenter, ViscaProtocol.PositionCenter);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleZoomVariable(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte zoomSpeed = ctx.ZoomSpeed;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandZoomVariable(zoomSpeed);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleZoomDirect(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            ushort zoomPos = ctx.ZoomPosition;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandZoomDirect(zoomPos);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleFocusVariable(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte focusSpeed = ctx.FocusSpeed;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandFocusVariable(focusSpeed);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleFocusDirect(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            ushort focusPos = ctx.FocusPosition;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandFocusDirect(focusPos);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleFocusMode(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            _focusMode = ctx.FocusMode;
            string modeName = ctx.FocusMode == ViscaProtocol.FocusModeAuto ? "Auto" : "Manual";
            _logger?.Invoke($"Focus Mode: {modeName} (Unity Camera does not support auto focus)");
            ViscaResponse.SendCompletion(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleFocusOnePush(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            _logger?.Invoke("Focus One Push AF (Unity Camera does not support auto focus)");
            ViscaResponse.SendCompletion(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleIrisVariable(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte irisDir = ctx.IrisDirection;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandIrisVariable(irisDir);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleIrisDirect(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            ushort irisPos = ctx.IrisPosition;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandIrisDirect(irisPos);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleMemoryRecall(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte memNum = ctx.MemoryNumber;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandMemoryRecall(memNum);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleMemorySet(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte memNum = ctx.MemoryNumber;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.CommandMemorySet(memNum);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandleMemoryReset(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            var responder = ctx.Responder;
            byte memNum = ctx.MemoryNumber;
            byte socketId = ctx.SocketId;
            _mainThreadDispatcher(() =>
            {
                _model.DeletePreset(memNum);
                ViscaResponse.SendCompletion(responder, _replyMode, socketId);
            });
            return true;
        }

        private bool HandlePanTiltPositionInquiry(in ViscaCommandContext ctx)
        {
            float panRange = _model.PanMaxDeg - _model.PanMinDeg;
            float tiltRange = _model.TiltMaxDeg - _model.TiltMinDeg;

            // Avoid division by zero using consistent epsilon
            float panNorm = panRange > ViscaProtocol.DivisionEpsilon
                ? (_model.CurrentPanDeg - _model.PanMinDeg) / panRange
                : 0.5f;
            float tiltNorm = tiltRange > ViscaProtocol.DivisionEpsilon
                ? (_model.CurrentTiltDeg - _model.TiltMinDeg) / tiltRange
                : 0.5f;

            ushort panPos = (ushort)(Clamp01(panNorm) * 65535f);
            ushort tiltPos = (ushort)(Clamp01(tiltNorm) * 65535f);

            ViscaResponse.SendInquiryResponse32(ctx.Responder, panPos, tiltPos, ctx.SocketId);
            return true;
        }

        private bool HandleZoomPositionInquiry(in ViscaCommandContext ctx)
        {
            float fovRange = _model.MaxFov - _model.MinFov;

            // Avoid division by zero using consistent epsilon
            float fovNorm = fovRange > ViscaProtocol.DivisionEpsilon
                ? (_model.CurrentFovDeg - _model.MinFov) / fovRange
                : 0.5f;

            // Inverted: small FOV = high zoom position
            ushort zoomPos = (ushort)((1.0f - Clamp01(fovNorm)) * 65535f);

            ViscaResponse.SendInquiryResponse16(ctx.Responder, zoomPos, ctx.SocketId);
            return true;
        }

        private bool HandleFocusPositionInquiry(in ViscaCommandContext ctx)
        {
            ushort focusPos = (ushort)_model.CurrentFocus;
            ViscaResponse.SendInquiryResponse16(ctx.Responder, focusPos, ctx.SocketId);
            return true;
        }

        private bool HandleFocusModeInquiry(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendInquiryResponse8(ctx.Responder, _focusMode, ctx.SocketId);
            return true;
        }

        private bool HandleCommandCancel(in ViscaCommandContext ctx)
        {
            // Immediately notify cancellation
            ViscaResponse.SendError(ctx.Responder, ViscaProtocol.ErrorCommandCancelled, ctx.SocketId);
            return true;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
