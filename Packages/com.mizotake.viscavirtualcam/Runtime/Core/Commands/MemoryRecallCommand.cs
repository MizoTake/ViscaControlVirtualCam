using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Memory Recall Command (8X 01 04 3F 02 pp FF) - Blackmagic extension
    /// </summary>
    public class MemoryRecallCommand : ViscaCommandBase
    {
        public override string CommandName => "MemoryRecall";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 7) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x3F), (4, 0x02));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleMemoryRecall(frame[5], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: MemoryNumber={frame[5]} [{FormatHex(frame)}]";
        }
    }
}
