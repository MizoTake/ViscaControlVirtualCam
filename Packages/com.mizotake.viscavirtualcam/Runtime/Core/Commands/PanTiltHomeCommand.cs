using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Pan/Tilt Home Command (8X 01 06 04 FF)
    /// Resets PTZ model to its home/initial values.
    /// </summary>
    public class PanTiltHomeCommand : ViscaCommandBase
    {
        public override string CommandName => "PanTiltHome";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x04));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleHome(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName} [{FormatHex(frame)}]";
        }
    }
}

