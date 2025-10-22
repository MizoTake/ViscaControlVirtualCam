using System;

namespace ViscaControlVirtualCam
{
    // Minimal VISCA parser for the subset in Docs.
    public static class ViscaParser
    {
        public static bool TryParsePanTiltDrive(byte[] frame, out byte vv, out byte ww, out byte pp, out byte tt)
        {
            vv = ww = pp = tt = 0;
            if (frame == null || frame.Length < 9) return false;
            // 8X 01 06 01 VV WW PP TT FF
            if (frame[1] != 0x01 || frame[2] != 0x06 || frame[3] != 0x01) return false;
            vv = frame[4];
            ww = frame[5];
            pp = frame[6];
            tt = frame[7];
            return frame[^1] == 0xFF;
        }

        public static bool TryParseZoomVariable(byte[] frame, out byte zz)
        {
            zz = 0;
            if (frame == null || frame.Length < 6) return false;
            // 8X 01 04 07 ZZ FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x07) return false;
            zz = frame[4];
            return frame[^1] == 0xFF;
        }

        public static bool TryParsePanTiltAbsolute(byte[] frame, out byte vv, out byte ww, out ushort pan, out ushort tilt)
        {
            vv = ww = 0; pan = tilt = 0;
            // 8X 01 06 02 VV WW p1 p2 p3 p4 t1 t2 t3 t4 FF
            if (frame == null || frame.Length < 14) return false;
            if (frame[1] != 0x01 || frame[2] != 0x06 || frame[3] != 0x02) return false;
            vv = frame[4];
            ww = frame[5];
            pan = DecodeNibble16(frame[6], frame[7], frame[8], frame[9]);
            tilt = DecodeNibble16(frame[10], frame[11], frame[12], frame[13]);
            return frame[^1] == 0xFF;
        }

        public static ushort DecodeNibble16(byte n1, byte n2, byte n3, byte n4)
        {
            ushort v = 0;
            v |= (ushort)((n1 & 0x0F) << 12);
            v |= (ushort)((n2 & 0x0F) << 8);
            v |= (ushort)((n3 & 0x0F) << 4);
            v |= (ushort)(n4 & 0x0F);
            return v;
        }

        public static AxisDirection DirFromVisca(byte b)
        {
            // 01=L/Up(+/- depends on axis), 02=R/Down, 03=Stop; mapping handled by caller per axis.
            return b switch
            {
                0x03 => AxisDirection.Stop,
                0x01 => AxisDirection.Negative, // Pan: Left(-), Tilt: Up(+)
                0x02 => AxisDirection.Positive, // Pan: Right(+), Tilt: Down(-)
                _ => AxisDirection.Stop
            };
        }
    }
}

