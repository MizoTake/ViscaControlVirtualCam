using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus Mode Inquiry Command (8X 09 04 38 FF)
    /// Response: Y0 50 02/03 FF (02=Auto, 03=Manual)
    /// </summary>
    public class FocusModeInquiryCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusModeInquiry";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x09), (2, 0x04), (3, 0x38));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleFocusModeInquiry(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Query current focus mode (Auto/Manual) [{FormatHex(frame)}]";
        }
    }
}
