using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// VISCA utility functions for nibble decoding and axis direction conversion
    /// </summary>
    public static class ViscaParser
    {

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
