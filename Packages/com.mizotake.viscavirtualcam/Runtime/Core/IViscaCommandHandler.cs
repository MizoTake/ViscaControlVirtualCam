using System;

namespace ViscaControlVirtualCam
{
    public interface IViscaCommandHandler
    {
        // Returns true if command accepted. Implementations should be thread-safe.
        bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, Action<byte[]> responder);

        bool HandleZoomVariable(byte zz, Action<byte[]> responder);

        bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, Action<byte[]> responder);

        // For error reporting path
        void HandleSyntaxError(byte[] frame, Action<byte[]> responder);
    }
}
