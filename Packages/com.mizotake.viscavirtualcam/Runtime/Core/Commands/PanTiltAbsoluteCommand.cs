using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Pan/Tilt Absolute Position Command (8X 01 06 02 [VV WW] p1 p2 p3 p4 t1 t2 t3 t4 FF)
    /// </summary>
    public class PanTiltAbsoluteCommand : ViscaCommandBase
    {
        public override string CommandName => "PanTiltAbsolute";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 12) && CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x02));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            int idx = 4;
            byte vv = 0, ww = 0;
            if (frame.Length >= 14) { vv = frame[idx++]; ww = frame[idx++]; }
            ushort pan = ViscaParser.DecodeNibble16(frame[idx + 0], frame[idx + 1], frame[idx + 2], frame[idx + 3]);
            ushort tilt = ViscaParser.DecodeNibble16(frame[idx + 4], frame[idx + 5], frame[idx + 6], frame[idx + 7]);
            return handler.HandlePanTiltAbsolute(vv, ww, pan, tilt, responder);
        }

        public override string GetDetails(byte[] frame)
        {
            int idx = 4;
            byte vv = 0, ww = 0;
            if (frame.Length >= 14) { vv = frame[idx++]; ww = frame[idx++]; }
            ushort pan = ViscaParser.DecodeNibble16(frame[idx + 0], frame[idx + 1], frame[idx + 2], frame[idx + 3]);
            ushort tilt = ViscaParser.DecodeNibble16(frame[idx + 4], frame[idx + 5], frame[idx + 6], frame[idx + 7]);
            return $"{CommandName}: PanSpeed=0x{vv:X2}, TiltSpeed=0x{ww:X2}, PanPos=0x{pan:X4} ({pan}), TiltPos=0x{tilt:X4} ({tilt}) [{FormatHex(frame)}]";
        }
    }
}
