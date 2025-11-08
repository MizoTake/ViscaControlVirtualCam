using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Zoom Position Inquiry Command (8X 09 04 47 FF)
    /// Response: Y0 50 0p 0p 0p 0p FF
    /// </summary>
    public class ZoomPositionInquiryCommand : ViscaCommandBase
    {
        public override string CommandName => "ZoomPositionInquiry";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 5) && CheckBytes(frame, (1, 0x09), (2, 0x04), (3, 0x47));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleZoomPositionInquiry(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Query current zoom position [{FormatHex(frame)}]";
        }
    }
}
