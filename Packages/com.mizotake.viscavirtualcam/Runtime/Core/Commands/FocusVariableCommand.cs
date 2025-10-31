using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus Variable Command (8X 01 04 08 ZZ FF) - Blackmagic extension
    /// </summary>
    public class FocusVariableCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusVariable";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 6) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x08));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleFocusVariable(frame[4], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            byte speed = frame[4];
            string dir = speed switch { 0x02 => "Far", 0x03 => "Near", 0x00 => "Stop", _ => $"0x{speed:X2}" };
            return $"{CommandName}: Direction={dir} [{FormatHex(frame)}]";
        }
    }
}
