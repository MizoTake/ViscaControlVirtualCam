using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Pan/Tilt Drive Command (8X 01 06 01 VV WW PP TT FF)
    /// </summary>
    public class PanTiltDriveCommand : ViscaCommandBase
    {
        public override string CommandName => "PanTiltDrive";

        public override bool TryParse(byte[] frame)
        {
            return ValidateFrame(frame, 9) && CheckBytes(frame, (1, 0x01), (2, 0x06), (3, 0x01));
        }

        public override bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            byte vv = frame[4], ww = frame[5], pp = frame[6], tt = frame[7];
            return handler.HandlePanTiltDrive(vv, ww, pp, tt, responder);
        }

        public override string GetDetails(byte[] frame)
        {
            byte vv = frame[4], ww = frame[5], pp = frame[6], tt = frame[7];
            string panDir = pp switch { 0x01 => "Left", 0x02 => "Right", 0x03 => "Stop", _ => $"0x{pp:X2}" };
            string tiltDir = tt switch { 0x01 => "Up", 0x02 => "Down", 0x03 => "Stop", _ => $"0x{tt:X2}" };
            return $"{CommandName}: PanSpeed=0x{vv:X2}, TiltSpeed=0x{ww:X2}, PanDir={panDir}, TiltDir={tiltDir} [{FormatHex(frame)}]";
        }
    }
}
