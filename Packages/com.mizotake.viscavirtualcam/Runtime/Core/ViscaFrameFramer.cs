using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Incremental 0xFF-terminated framer to extract VISCA frames from a byte stream.
    /// Uses smart buffer sizing to minimize allocations and memory waste.
    /// </summary>
    public sealed class ViscaFrameFramer
    {
        private byte[] _buffer;
        private int _length;
        private readonly int _maxFrameSize;
        private readonly int _initialBufferSize;

        // Statistics for debugging
        private int _framesExtracted;
        private int _bufferOverflows;

        /// <summary>
        /// Number of frames successfully extracted.
        /// </summary>
        public int FramesExtracted => _framesExtracted;

        /// <summary>
        /// Number of times buffer was reset due to overflow.
        /// </summary>
        public int BufferOverflows => _bufferOverflows;

        /// <summary>
        /// Current buffer usage.
        /// </summary>
        public int CurrentBufferLength => _length;

        public ViscaFrameFramer(int maxFrameSize = 4096)
        {
            // Initial buffer sized for typical VISCA frames (most are under 20 bytes)
            _initialBufferSize = Math.Min(256, maxFrameSize);
            _buffer = new byte[_initialBufferSize];
            _length = 0;
            _maxFrameSize = Math.Max(ViscaProtocol.MaxFrameLength, maxFrameSize);
        }

        public void Reset()
        {
            _length = 0;
            // Shrink buffer back to initial size if it grew too large
            if (_buffer.Length > _initialBufferSize * 4)
            {
                _buffer = new byte[_initialBufferSize];
            }
        }

        /// <summary>
        /// Append data and invoke onFrame for each extracted frame (including 0xFF terminator).
        /// </summary>
        public void Append(ReadOnlySpan<byte> data, Action<byte[]> onFrame)
        {
            if (data.Length == 0) return;

            EnsureCapacity(_length + data.Length);
            data.CopyTo(new Span<byte>(_buffer, _length, data.Length));
            _length += data.Length;

            int start = 0;
            for (int i = start; i < _length; i++)
            {
                if (_buffer[i] == ViscaProtocol.FrameTerminator)
                {
                    int frameLen = i - start + 1;
                    var frame = new byte[frameLen];
                    Buffer.BlockCopy(_buffer, start, frame, 0, frameLen);
                    _framesExtracted++;
                    onFrame(frame);
                    start = i + 1;
                }
            }

            // Shift remaining data to start of buffer
            if (start > 0)
            {
                int remain = _length - start;
                if (remain > 0)
                {
                    Buffer.BlockCopy(_buffer, start, _buffer, 0, remain);
                }
                _length = remain;
            }

            // Guard against buffer abuse (no terminator for too long)
            if (_length > _maxFrameSize)
            {
                _bufferOverflows++;
                _length = 0;
            }
        }

        private void EnsureCapacity(int required)
        {
            if (_buffer.Length >= required) return;

            // Grow by 1.5x instead of 2x to reduce memory waste
            // But ensure we have at least the required size
            int newSize = _buffer.Length;
            while (newSize < required)
            {
                newSize = Math.Max(newSize + newSize / 2, newSize + 64);
            }

            // Cap at max frame size + some overhead
            newSize = Math.Min(newSize, _maxFrameSize + 256);

            var newBuffer = new byte[newSize];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);
            _buffer = newBuffer;
        }
    }
}
