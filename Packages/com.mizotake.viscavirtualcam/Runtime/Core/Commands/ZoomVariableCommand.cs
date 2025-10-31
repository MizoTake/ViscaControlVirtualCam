using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Zoom Variable Command (8X 01 04 07 ZZ FF)
    /// </summary>
    public class ZoomVariableCommand : ViscaCommandBase
    {
        public override string CommandName => "ZoomVariable";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 6) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x07));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleZoomVariable(frame[4], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            byte zz = frame[4];
            int dirNibble = (zz & 0xF0) >> 4;
            int speed = (zz & 0x0F);
            string dir = dirNibble switch { 0x2 => "Tele", 0x3 => "Wide", 0x0 => "Stop", _ => $"0x{dirNibble:X}" };
            return $"{CommandName}: Direction={dir}, Speed={speed} (0x{zz:X2}) [{FormatHex(frame)}]";
        }
    }
}
