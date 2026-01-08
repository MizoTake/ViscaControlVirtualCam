using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Unified VISCA command handler interface.
    /// Single method handles all command types via ViscaCommandContext.
    /// </summary>
    public interface IViscaCommandHandler
    {
        /// <summary>
        /// Handle a VISCA command.
        /// </summary>
        /// <param name="context">Command context containing type and parameters</param>
        /// <returns>True if command was handled successfully</returns>
        bool Handle(in ViscaCommandContext context);

        /// <summary>
        /// Handle syntax/protocol errors.
        /// </summary>
        /// <param name="frame">Raw frame that caused the error</param>
        /// <param name="responder">Response callback</param>
        /// <param name="errorCode">VISCA error code (see ViscaProtocol.Error*)</param>
        void HandleError(byte[] frame, Action<byte[]> responder, byte errorCode);
    }

    /// <summary>
    /// Response helpers for VISCA handlers.
    /// </summary>
    public static class ViscaResponse
    {
        private static readonly byte[] AckResponse = { 0x90, ViscaProtocol.ResponseAck, ViscaProtocol.FrameTerminator };
        private static readonly byte[] CompletionResponse = { 0x90, ViscaProtocol.ResponseCompletion, ViscaProtocol.FrameTerminator };

        /// <summary>
        /// Send ACK response (command received).
        /// </summary>
        public static void SendAck(Action<byte[]> responder, ViscaReplyMode mode)
        {
            if (mode == ViscaReplyMode.None) return;
            responder(AckResponse);
        }

        /// <summary>
        /// Send Completion response (command executed).
        /// </summary>
        public static void SendCompletion(Action<byte[]> responder, ViscaReplyMode mode)
        {
            if (mode != ViscaReplyMode.AckAndCompletion) return;
            responder(CompletionResponse);
        }

        /// <summary>
        /// Send Error response.
        /// </summary>
        public static void SendError(Action<byte[]> responder, byte errorCode)
        {
            responder(new byte[] { 0x90, ViscaProtocol.ResponseError, errorCode, ViscaProtocol.FrameTerminator });
        }

        /// <summary>
        /// Send inquiry response with 16-bit value as 4 nibbles.
        /// Format: 90 50 0n 0n 0n 0n FF
        /// </summary>
        public static void SendInquiryResponse16(Action<byte[]> responder, ushort value)
        {
            responder(new byte[]
            {
                0x90, ViscaProtocol.ResponseCompletion,
                (byte)((value >> 12) & 0x0F),
                (byte)((value >> 8) & 0x0F),
                (byte)((value >> 4) & 0x0F),
                (byte)(value & 0x0F),
                ViscaProtocol.FrameTerminator
            });
        }

        /// <summary>
        /// Send inquiry response with two 16-bit values (Pan/Tilt position).
        /// Format: 90 50 0p 0p 0p 0p 0t 0t 0t 0t FF
        /// </summary>
        public static void SendInquiryResponse32(Action<byte[]> responder, ushort value1, ushort value2)
        {
            responder(new byte[]
            {
                0x90, ViscaProtocol.ResponseCompletion,
                (byte)((value1 >> 12) & 0x0F),
                (byte)((value1 >> 8) & 0x0F),
                (byte)((value1 >> 4) & 0x0F),
                (byte)(value1 & 0x0F),
                (byte)((value2 >> 12) & 0x0F),
                (byte)((value2 >> 8) & 0x0F),
                (byte)((value2 >> 4) & 0x0F),
                (byte)(value2 & 0x0F),
                ViscaProtocol.FrameTerminator
            });
        }

        /// <summary>
        /// Send inquiry response with single byte value.
        /// Format: 90 50 XX FF
        /// </summary>
        public static void SendInquiryResponse8(Action<byte[]> responder, byte value)
        {
            responder(new byte[]
            {
                0x90, ViscaProtocol.ResponseCompletion,
                value,
                ViscaProtocol.FrameTerminator
            });
        }
    }
}
