using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Iris Variable Command (8X 01 04 0B ZZ FF) - Blackmagic extension
    /// </summary>
    public class IrisVariableCommand : ViscaCommandBase
    {
        public override string CommandName => "IrisVariable";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 6) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x0B));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleIrisVariable(frame[4], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            byte dir = frame[4];
            string direction = dir switch { 0x02 => "Open", 0x03 => "Close", 0x00 => "Stop", _ => $"0x{dir:X2}" };
            return $"{CommandName}: Direction={direction} [{FormatHex(frame)}]";
        }
    }
}
