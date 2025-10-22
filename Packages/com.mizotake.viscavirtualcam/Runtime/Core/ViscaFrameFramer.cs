using System;

namespace ViscaControlVirtualCam
{
    // Incremental 0xFF-terminated framer to extract VISCA frames from a byte stream.
    public sealed class ViscaFrameFramer
    {
        private byte[] _buffer;
        private int _length;
        private readonly int _maxFrame;

        public ViscaFrameFramer(int maxFrameSize = 4096)
        {
            _buffer = new byte[Math.Max(256, maxFrameSize)];
            _length = 0;
            _maxFrame = Math.Max(64, maxFrameSize);
        }

        public void Reset()
        {
            _length = 0;
        }

        // Append data and invoke onFrame for each extracted frame (including 0xFF)
        public void Append(ReadOnlySpan<byte> data, Action<byte[]> onFrame)
        {
            if (data.Length == 0) return;
            EnsureCapacity(_length + data.Length);
            data.CopyTo(new Span<byte>(_buffer, _length, data.Length));
            _length += data.Length;

            int start = 0;
            for (int i = 0; i < _length; i++)
            {
                if (_buffer[i] == 0xFF)
                {
                    int frameLen = i - start + 1;
                    var frame = new byte[frameLen];
                    Buffer.BlockCopy(_buffer, start, frame, 0, frameLen);
                    onFrame(frame);
                    start = i + 1;
                }
            }

            if (start > 0)
            {
                // shift remaining
                int remain = _length - start;
                Buffer.BlockCopy(_buffer, start, _buffer, 0, remain);
                _length = remain;
            }

            // Guard against buffer abuse (no terminator for too long)
            if (_length > _maxFrame)
            {
                // Drop buffer content to avoid unbounded growth
                _length = 0;
                // Caller should log this situation.
            }
        }

        private void EnsureCapacity(int required)
        {
            if (_buffer.Length >= required) return;
            int newSize = _buffer.Length;
            while (newSize < required) newSize *= 2;
            Array.Resize(ref _buffer, newSize);
        }
    }
}
