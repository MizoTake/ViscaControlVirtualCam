using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Focus One Push AF Command (8X 01 04 18 01 FF)
    /// Triggers one-time auto focus
    /// </summary>
    public class FocusOnePushCommand : ViscaCommandBase
    {
        public override string CommandName => "FocusOnePush";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 6) && CheckBytes(frame, (1, 0x01), (2, 0x04), (3, 0x18), (4, 0x01));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            return handler.HandleFocusOnePush(responder);
        }

        public override string GetDetails(byte[] frame)
        {
            return $"{CommandName}: Trigger one-push auto focus [{FormatHex(frame)}]";
        }
    }
}
