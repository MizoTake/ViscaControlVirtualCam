using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ViscaControlVirtualCam
{
    public static class IpSetupFrameCodec
    {
        public const byte FrameStart = 0x02;
        public const byte FrameEnd = 0x03;
        public const byte UnitTerminator = 0xFF;

        public static bool TryParse(byte[] frame, out List<string> units, out string error)
        {
            units = new List<string>();
            error = null;

            if (frame == null || frame.Length < 2)
            {
                error = "Frame is null or too short.";
                return false;
            }

            if (frame[0] != FrameStart)
            {
                error = $"Invalid STX: 0x{frame[0]:X2}";
                return false;
            }

            if (frame[frame.Length - 1] != FrameEnd)
            {
                error = $"Invalid ETX: 0x{frame[frame.Length - 1]:X2}";
                return false;
            }

            var payloadEnd = frame.Length - 1;
            var unitStart = 1;

            for (var i = 1; i < payloadEnd; i++)
                if (frame[i] == UnitTerminator)
                {
                    AppendUnit(frame, unitStart, i - unitStart, units);
                    unitStart = i + 1;
                }

            AppendUnit(frame, unitStart, payloadEnd - unitStart, units);
            return true;
        }

        public static byte[] Build(IReadOnlyList<string> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));

            using (var stream = new MemoryStream())
            {
                stream.WriteByte(FrameStart);
                for (var i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    if (string.IsNullOrWhiteSpace(unit))
                        continue;

                    var bytes = Encoding.ASCII.GetBytes(unit);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.WriteByte(UnitTerminator);
                }

                stream.WriteByte(FrameEnd);
                return stream.ToArray();
            }
        }

        private static void AppendUnit(byte[] frame, int offset, int length, List<string> units)
        {
            if (length <= 0) return;

            var text = Encoding.ASCII.GetString(frame, offset, length).Trim();
            if (string.IsNullOrEmpty(text))
                return;

            units.Add(text);
        }
    }
}
