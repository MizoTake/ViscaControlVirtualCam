using System;
using System.Threading;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Bridges VISCA commands to a PtzModel. Pure C#.
    ///     Implements the unified IViscaCommandHandler interface.
    /// </summary>
    public sealed class PtzViscaHandler : IViscaCommandHandler
    {
        private readonly Action<string> _logger;
        private readonly Action<Action> _mainThreadDispatcher;

        private readonly int _maxPendingOperations;
        private readonly PtzModel _model;
        private readonly ViscaReplyMode _replyMode;
        private byte _focusMode = ViscaProtocol.FocusModeManual;
        private int _pendingOperations;
        private int _cancelGeneration;

        public PtzViscaHandler(PtzModel model, Action<Action> mainThreadDispatcher, ViscaReplyMode replyMode,
            Action<string> logger = null, int maxPendingOperations = 64)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _mainThreadDispatcher =
                mainThreadDispatcher ?? throw new ArgumentNullException(nameof(mainThreadDispatcher));
            _replyMode = replyMode;
            _logger = logger;
            _maxPendingOperations = Math.Max(1, maxPendingOperations);
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
            var socketId = ViscaProtocol.ExtractSocketId(frame);
            ViscaResponse.SendError(responder, errorCode, socketId);
        }

        private bool HandlePanTiltDrive(in ViscaCommandContext ctx)
        {
            var capturedCtx = ctx;
            if (!TryEnqueue(capturedCtx, () =>
                {
                    var pdir = ViscaParser.DirFromVisca(capturedCtx.PanDirection);
                    var tdir = ViscaParser.DirFromVisca(capturedCtx.TiltDirection);
                    _model.CommandPanTiltVariable(capturedCtx.PanSpeed, capturedCtx.TiltSpeed, pdir, tdir);
                }))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandlePanTiltAbsolute(in ViscaCommandContext ctx)
        {
            var capturedCtx = ctx;
            if (!TryEnqueue(capturedCtx,
                    () => _model.CommandPanTiltAbsolute(capturedCtx.PanSpeed, capturedCtx.TiltSpeed,
                        capturedCtx.PanPosition, capturedCtx.TiltPosition)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleHome(in ViscaCommandContext ctx)
        {
            var capturedCtx = ctx;
            if (!TryEnqueue(capturedCtx, () => _model.CommandHome()))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandlePanTiltReset(in ViscaCommandContext ctx)
        {
            var capturedCtx2 = ctx;
            if (!TryEnqueue(capturedCtx2,
                    () => _model.CommandPanTiltAbsolute(0, 0, ViscaProtocol.PositionCenter,
                        ViscaProtocol.PositionCenter)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleZoomVariable(in ViscaCommandContext ctx)
        {
            var capturedCtx3 = ctx;
            if (!TryEnqueue(capturedCtx3, () => _model.CommandZoomVariable(capturedCtx3.ZoomSpeed)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleZoomDirect(in ViscaCommandContext ctx)
        {
            var capturedCtx4 = ctx;
            if (!TryEnqueue(capturedCtx4, () => _model.CommandZoomDirect(capturedCtx4.ZoomPosition)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleFocusVariable(in ViscaCommandContext ctx)
        {
            var capturedCtx5 = ctx;
            if (!TryEnqueue(capturedCtx5, () => _model.CommandFocusVariable(capturedCtx5.FocusSpeed)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleFocusDirect(in ViscaCommandContext ctx)
        {
            var capturedCtx6 = ctx;
            if (!TryEnqueue(capturedCtx6, () => _model.CommandFocusDirect(capturedCtx6.FocusPosition)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleFocusMode(in ViscaCommandContext ctx)
        {
            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            _focusMode = ctx.FocusMode;
            var modeName = ctx.FocusMode == ViscaProtocol.FocusModeAuto ? "Auto" : "Manual";
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
            var capturedCtx7 = ctx;
            if (!TryEnqueue(capturedCtx7, () => _model.CommandIrisVariable(capturedCtx7.IrisDirection)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleIrisDirect(in ViscaCommandContext ctx)
        {
            var capturedCtx8 = ctx;
            if (!TryEnqueue(capturedCtx8, () => _model.CommandIrisDirect(capturedCtx8.IrisPosition)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleMemoryRecall(in ViscaCommandContext ctx)
        {
            var capturedCtx9 = ctx;
            if (!TryEnqueue(capturedCtx9, () => _model.CommandMemoryRecall(capturedCtx9.MemoryNumber)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleMemorySet(in ViscaCommandContext ctx)
        {
            var capturedCtx10 = ctx;
            if (!TryEnqueue(capturedCtx10, () => _model.CommandMemorySet(capturedCtx10.MemoryNumber)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandleMemoryReset(in ViscaCommandContext ctx)
        {
            var capturedCtx11 = ctx;
            if (!TryEnqueue(capturedCtx11, () => _model.DeletePreset(capturedCtx11.MemoryNumber)))
                return true;

            ViscaResponse.SendAck(ctx.Responder, _replyMode, ctx.SocketId);
            return true;
        }

        private bool HandlePanTiltPositionInquiry(in ViscaCommandContext ctx)
        {
            var panRange = _model.PanMaxDeg - _model.PanMinDeg;
            var tiltRange = _model.TiltMaxDeg - _model.TiltMinDeg;

            // Avoid division by zero using consistent epsilon
            var panNorm = panRange > ViscaProtocol.DivisionEpsilon
                ? (_model.CurrentPanDeg - _model.PanMinDeg) / panRange
                : 0.5f;
            var tiltNorm = tiltRange > ViscaProtocol.DivisionEpsilon
                ? (_model.CurrentTiltDeg - _model.TiltMinDeg) / tiltRange
                : 0.5f;

            var panPos = (ushort)(Clamp01(panNorm) * 65535f);
            var tiltPos = (ushort)(Clamp01(tiltNorm) * 65535f);

            ViscaResponse.SendInquiryResponse32(ctx.Responder, panPos, tiltPos, ctx.SocketId);
            return true;
        }

        private bool HandleZoomPositionInquiry(in ViscaCommandContext ctx)
        {
            var zoomPos = _model.GetZoomPositionFromFov(_model.CurrentFovDeg);

            ViscaResponse.SendInquiryResponse16(ctx.Responder, zoomPos, ctx.SocketId);
            return true;
        }

        private bool HandleFocusPositionInquiry(in ViscaCommandContext ctx)
        {
            var focusPos = (ushort)_model.CurrentFocus;
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
            // Always acknowledge cancel as CommandCanceled per spec, even if nothing is pending
            Interlocked.Exchange(ref _pendingOperations, 0); // best-effort clear any pending work
            Interlocked.Increment(ref _cancelGeneration); // suppress completions from earlier enqueued commands
            ViscaResponse.SendError(ctx.Responder, ViscaProtocol.ErrorCommandCancelled, ctx.SocketId);
            return true;
        }

        private bool TryEnqueue(ViscaCommandContext ctx, Action action)
        {
            var responder = ctx.Responder;
            var socketId = ctx.SocketId;
            var generation = _cancelGeneration;

            var newCount = Interlocked.Increment(ref _pendingOperations);
            if (newCount > _maxPendingOperations)
            {
                Interlocked.Decrement(ref _pendingOperations);
                ViscaResponse.SendError(responder, ViscaProtocol.ErrorCommandBuffer, socketId);
                return false;
            }

            _mainThreadDispatcher(() =>
            {
                var shouldComplete = true;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    _logger?.Invoke($"Command execution error: {e.Message}");
                    ViscaResponse.SendError(responder, ViscaProtocol.ErrorCommandNotExecutable, socketId);
                    shouldComplete = false;
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingOperations);
                    if (shouldComplete && generation == _cancelGeneration)
                        ViscaResponse.SendCompletion(responder, _replyMode, socketId);
                }
            });

            return true;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }
}
