using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus Mode Command (8X 01 04 38 pp FF)
    /// 02 = Auto Focus, 03 = Manual Focus
    /// </summary>
    public class FocusModeCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusMode";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 6) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x38));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            byte mode = frame[4];
            return handler.HandleFocusMode(mode, responder);
        }

        public override string GetDetails(byte[] frame)
        {
            byte mode = frame[4];
            string modeName = mode switch
            {
                0x02 => "Auto",
                0x03 => "Manual",
                _ => $"0x{mode:X2}"
            };
            return $"{CommandName}: Mode={modeName} [{FormatHex(frame)}]";
        }
    }
}
