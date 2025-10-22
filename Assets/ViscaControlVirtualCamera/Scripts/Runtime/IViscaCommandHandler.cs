using System.Net;

namespace ViscaControlVirtualCam
{
    public interface IViscaCommandHandler
    {
        // Returns true if command accepted. Implementations should be thread-safe.
        bool HandlePanTiltDrive(byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir, IPEndPoint remote);

        bool HandleZoomVariable(byte zz, IPEndPoint remote);

        bool HandlePanTiltAbsolute(byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos, IPEndPoint remote);

        // For error reporting path
        void HandleSyntaxError(byte[] frame, IPEndPoint remote);
    }
}

