using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Represents a VISCA command that can be parsed and executed
    /// </summary>
    public interface IViscaCommand
    {
        /// <summary>
        /// Command name for identification and logging
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Try to parse the VISCA frame for this command type
        /// </summary>
        /// <param name="frame">Raw VISCA frame bytes</param>
        /// <returns>True if this command matches the frame</returns>
        bool TryParse(byte[] frame);

        /// <summary>
        /// Execute the command through the handler
        /// </summary>
        /// <param name="frame">Raw VISCA frame bytes</param>
        /// <param name="handler">Command handler to execute the command</param>
        /// <param name="responder">Response callback</param>
        /// <returns>True if command was handled</returns>
        bool Execute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder);

        /// <summary>
        /// Get detailed description for logging
        /// </summary>
        /// <param name="frame">Raw VISCA frame bytes</param>
        /// <returns>Detailed command description</returns>
        string GetDetails(byte[] frame);
    }
}
