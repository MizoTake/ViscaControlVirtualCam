using System;

namespace ViscaControlVirtualCam
{
    public interface IViscaCommandHandler
    {
        // Returns true if command accepted. Implementations should be thread-safe.
        bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, Action<byte[]> responder);

        bool HandleZoomVariable(byte zz, Action<byte[]> responder);

        bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, Action<byte[]> responder);

        // Blackmagic PTZ Control extended commands
        bool HandleZoomDirect(ushort zoomPos, Action<byte[]> responder);

        bool HandleFocusVariable(byte focusSpeed, Action<byte[]> responder);

        bool HandleFocusDirect(ushort focusPos, Action<byte[]> responder);

        bool HandleIrisVariable(byte irisDir, Action<byte[]> responder);

        bool HandleIrisDirect(ushort irisPos, Action<byte[]> responder);

        bool HandleMemoryRecall(byte memoryNumber, Action<byte[]> responder);

        bool HandleMemorySet(byte memoryNumber, Action<byte[]> responder);

        bool HandleMemoryReset(byte memoryNumber, Action<byte[]> responder);

        // Standard VISCA: Pan/Tilt Home (also used to reset to initial values)
        bool HandleHome(Action<byte[]> responder);

        // Pan/Tilt Reset to center position
        bool HandlePanTiltReset(Action<byte[]> responder);

        // Focus Mode (Auto/Manual)
        bool HandleFocusMode(byte mode, Action<byte[]> responder);

        // Focus One Push Auto Focus
        bool HandleFocusOnePush(Action<byte[]> responder);

        // Inquiry Commands
        bool HandlePanTiltPositionInquiry(Action<byte[]> responder);

        bool HandleZoomPositionInquiry(Action<byte[]> responder);

        bool HandleFocusPositionInquiry(Action<byte[]> responder);

        bool HandleFocusModeInquiry(Action<byte[]> responder);

        // For error reporting path
        void HandleSyntaxError(byte[] frame, Action<byte[]> responder);
    }
}
