using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Incremental 0xFF-terminated framer to extract VISCA frames from a byte stream.
    ///     Uses smart buffer sizing to minimize allocations and memory waste.
    /// </summary>
    public sealed class ViscaFrameFramer
    {
        private readonly int _initialBufferSize;
        private readonly int _maxFrameSize;
        private byte[] _buffer;

        // Statistics for debugging

        public ViscaFrameFramer(int maxFrameSize = 4096)
        {
            // Initial buffer sized for typical VISCA frames (most are under 20 bytes)
            _initialBufferSize = Math.Min(256, maxFrameSize);
            _buffer = new byte[_initialBufferSize];
            CurrentBufferLength = 0;
            _maxFrameSize = Math.Max(ViscaProtocol.MaxFrameLength, maxFrameSize);
        }

        /// <summary>
        ///     Number of frames successfully extracted.
        /// </summary>
        public int FramesExtracted { get; private set; }

        /// <summary>
        ///     Number of times buffer was reset due to overflow.
        /// </summary>
        public int BufferOverflows { get; private set; }

        /// <summary>
        ///     Current buffer usage.
        /// </summary>
        public int CurrentBufferLength { get; private set; }

        public void Reset()
        {
            CurrentBufferLength = 0;
            // Shrink buffer back to initial size if it grew too large
            if (_buffer.Length > _initialBufferSize * 4) _buffer = new byte[_initialBufferSize];
        }

        /// <summary>
        ///     Append data and invoke onFrame for each extracted frame (including 0xFF terminator).
        /// </summary>
        public void Append(ReadOnlySpan<byte> data, Action<byte[]> onFrame)
        {
            if (data.Length == 0) return;

            EnsureCapacity(CurrentBufferLength + data.Length);
            data.CopyTo(new Span<byte>(_buffer, CurrentBufferLength, data.Length));
            CurrentBufferLength += data.Length;

            var start = 0;
            for (var i = start; i < CurrentBufferLength; i++)
                if (_buffer[i] == ViscaProtocol.FrameTerminator)
                {
                    var frameLen = i - start + 1;
                    var frame = new byte[frameLen];
                    Buffer.BlockCopy(_buffer, start, frame, 0, frameLen);
                    FramesExtracted++;
                    onFrame(frame);
                    start = i + 1;
                }

            // Shift remaining data to start of buffer
            if (start > 0)
            {
                var remain = CurrentBufferLength - start;
                if (remain > 0) Buffer.BlockCopy(_buffer, start, _buffer, 0, remain);
                CurrentBufferLength = remain;
            }

            // Guard against buffer abuse (no terminator for too long)
            if (CurrentBufferLength > _maxFrameSize)
            {
                BufferOverflows++;
                CurrentBufferLength = 0;
            }
        }

        private void EnsureCapacity(int required)
        {
            if (_buffer.Length >= required) return;

            // Grow by 1.5x instead of 2x to reduce memory waste
            // But ensure we have at least the required size
            var newSize = _buffer.Length;
            while (newSize < required) newSize = Math.Max(newSize + newSize / 2, newSize + 64);

            // Cap at max frame size + some overhead
            newSize = Math.Min(newSize, _maxFrameSize + 256);

            var newBuffer = new byte[newSize];
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, CurrentBufferLength);
            _buffer = newBuffer;
        }
    }
}