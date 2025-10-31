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
            // 8X 01 06 02 [VV WW] p1 p2 p3 p4 t1 t2 t3 t4 FF
            if (frame == null || frame.Length < 12) return false;
            if (frame[1] != 0x01 || frame[2] != 0x06 || frame[3] != 0x02) return false;
            int idx = 4;
            if (frame.Length >= 14) // speeds present
            {
                vv = frame[idx++];
                ww = frame[idx++];
            }
            pan = DecodeNibble16(frame[idx + 0], frame[idx + 1], frame[idx + 2], frame[idx + 3]);
            tilt = DecodeNibble16(frame[idx + 4], frame[idx + 5], frame[idx + 6], frame[idx + 7]);
            return frame[^1] == 0xFF;
        }

        public static bool TryParseZoomDirect(byte[] frame, out ushort zoomPos)
        {
            zoomPos = 0;
            if (frame == null || frame.Length < 9) return false;
            // 8X 01 04 47 p1 p2 p3 p4 FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x47) return false;
            zoomPos = DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
            return frame[^1] == 0xFF;
        }

        public static bool TryParseFocusVariable(byte[] frame, out byte focusSpeed)
        {
            focusSpeed = 0;
            if (frame == null || frame.Length < 6) return false;
            // 8X 01 04 08 ZZ FF (ZZ: 02=Far, 03=Near, 00=Stop)
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x08) return false;
            focusSpeed = frame[4];
            return frame[^1] == 0xFF;
        }

        public static bool TryParseFocusDirect(byte[] frame, out ushort focusPos)
        {
            focusPos = 0;
            if (frame == null || frame.Length < 9) return false;
            // 8X 01 04 48 p1 p2 p3 p4 FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x48) return false;
            focusPos = DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
            return frame[^1] == 0xFF;
        }

        public static bool TryParseIrisVariable(byte[] frame, out byte irisDir)
        {
            irisDir = 0;
            if (frame == null || frame.Length < 6) return false;
            // 8X 01 04 0B ZZ FF (ZZ: 02=Open, 03=Close, 00=Stop)
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x0B) return false;
            irisDir = frame[4];
            return frame[^1] == 0xFF;
        }

        public static bool TryParseIrisDirect(byte[] frame, out ushort irisPos)
        {
            irisPos = 0;
            if (frame == null || frame.Length < 9) return false;
            // 8X 01 04 4B p1 p2 p3 p4 FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x4B) return false;
            irisPos = DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
            return frame[^1] == 0xFF;
        }

        public static bool TryParseMemoryRecall(byte[] frame, out byte memoryNumber)
        {
            memoryNumber = 0;
            if (frame == null || frame.Length < 7) return false;
            // 8X 01 04 3F 02 pp FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x3F || frame[4] != 0x02) return false;
            memoryNumber = frame[5];
            return frame[^1] == 0xFF;
        }

        public static bool TryParseMemorySet(byte[] frame, out byte memoryNumber)
        {
            memoryNumber = 0;
            if (frame == null || frame.Length < 7) return false;
            // 8X 01 04 3F 01 pp FF
            if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x3F || frame[4] != 0x01) return false;
            memoryNumber = frame[5];
            return frame[^1] == 0xFF;
        }

        public static string GetCommandName(byte[] frame)
        {
            if (frame == null || frame.Length < 5) return "Invalid";
            byte b1 = frame[1], b2 = frame[2], b3 = frame[3];
            if (b1 == 0x01 && b2 == 0x06 && b3 == 0x01) return "PanTiltDrive";
            if (b1 == 0x01 && b2 == 0x06 && b3 == 0x02) return "PanTiltAbsolute";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x07) return "ZoomVariable";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x47) return "ZoomDirect";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x08) return "FocusVariable";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x48) return "FocusDirect";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x0B) return "IrisVariable";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x4B) return "IrisDirect";
            if (b1 == 0x01 && b2 == 0x04 && b3 == 0x3F)
            {
                if (frame.Length >= 6)
                {
                    if (frame[4] == 0x02) return "MemoryRecall";
                    if (frame[4] == 0x01) return "MemorySet";
                }
                return "Memory";
            }
            return $"Unknown({b1:X2} {b2:X2} {b3:X2})";
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
