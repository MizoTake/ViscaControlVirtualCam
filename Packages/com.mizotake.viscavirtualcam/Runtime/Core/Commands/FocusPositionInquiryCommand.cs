using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus Position Inquiry Command (8X 09 04 48 FF)
    /// Response: Y0 50 0p 0p 0p 0p FF
    /// </summary>
    public class FocusPositionInquiryCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusPositionInquiry";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x09), (2, 0x04), (3, 0x48));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleFocusPositionInquiry(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Query current focus position [{FormatHex(frame)}]";
        }
    }
}
