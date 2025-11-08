using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Pan/Tilt Position Inquiry Command (8X 09 06 12 FF)
    /// Response: Y0 50 0p 0p 0p 0p 0t 0t 0t 0t FF
    /// </summary>
    public class PanTiltPositionInquiryCommand : ViscaCommandBase
    {
        public override string CommandName => "PanTiltPositionInquiry";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x09), (2, 0x06), (3, 0x12));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandlePanTiltPositionInquiry(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Query current pan/tilt position [{FormatHex(frame)}]";
        }
    }
}
