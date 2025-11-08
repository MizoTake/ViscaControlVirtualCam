using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Memory Reset Command (8X 01 04 3F 00 pp FF)
    /// Deletes a memory preset
    /// </summary>
    public class MemoryResetCommand : ViscaCommandBase
    {
        public override string CommandName => "MemoryReset";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 7) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x3F), (4, 0x00));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleMemoryReset(frame[5], responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: MemoryNumber={frame[5]} [{FormatHex(frame)}]";
        }
    }
}
