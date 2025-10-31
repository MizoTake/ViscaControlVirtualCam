using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus Direct Command (8X 01 04 48 p1 p2 p3 p4 FF) - Blackmagic extension
    /// </summary>
    public class FocusDirectCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusDirect";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 9) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x48));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            ushort pos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
            return handler.HandleFocusDirect(pos, responder);
        }

        public override string GetDetails(byte[] frame)
        {
            ushort pos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
            return $"{CommandName}: FocusPos=0x{pos:X4} ({pos}) [{FormatHex(frame)}]";
        }
    }
}
