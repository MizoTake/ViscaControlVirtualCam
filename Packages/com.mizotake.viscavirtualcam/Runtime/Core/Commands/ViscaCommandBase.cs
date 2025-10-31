using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Base class for VISCA commands to reduce boilerplate
    /// </summary>
    public abstract class ViscaCommandBase : IViscaCommand
    {
        public abstract string CommandName { get; }
        public abstract bool TryParse(byte[] frame);
        public abstract bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder);
        public abstract string GetDetails(byte[] frame);

        protected static bool ValidateFrame(byte[] frame, int minLength)
        {
            return frame != null && frame.Length >= minLength && frame[^1] == 0xFF;
        }

        protected static bool CheckBytes(byte[] frame, params (int index, byte value)[] checks)
        {
            foreach (var (index, value) in checks)
            {
                if (frame[index] != value) return false;
            }
            return true;
        }

        protected static string FormatHex(byte[] frame) => BitConverter.ToString(frame);
    }
}
