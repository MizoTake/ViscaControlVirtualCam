using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Pan/Tilt Reset Command (8X 01 06 05 FF)
    /// Resets pan/tilt position to center (0, 0)
    /// </summary>
    public class PanTiltResetCommand : ViscaCommandBase
    {
        public override string CommandName => "PanTiltReset";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x05));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandlePanTiltReset(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Reset to center position [{FormatHex(frame)}]";
        }
    }
}
