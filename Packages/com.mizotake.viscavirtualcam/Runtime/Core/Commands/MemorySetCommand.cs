using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Memory Set Command (8X 01 04 3F 01 pp FF) - Blackmagic extension
    /// </summary>
    public class MemorySetCommand : ViscaCommandBase
    {
        public override string CommandName => "MemorySet";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 7) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x3F), (4, 0x01));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleMemorySet(frame[5], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: MemoryNumber={frame[5]} [{FormatHex(frame)}]";
        }
    }
}
