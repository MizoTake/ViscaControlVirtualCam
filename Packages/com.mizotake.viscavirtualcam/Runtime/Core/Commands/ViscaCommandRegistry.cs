using System;
using System.Collections.Generic;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Registry for all supported VISCA commands
    /// </summary>
    public class ViscaCommandRegistry
    {
        private readonly List<IViscaCommand> _commands = new List<IViscaCommand>();

        public ViscaCommandRegistry()
        {
            RegisterDefaultCommands();
        }

        private void RegisterDefaultCommands()
        {
            // Standard VISCA commands
            Register(new PanTiltDriveCommand());
            Register(new PanTiltAbsoluteCommand());
            Register(new PanTiltHomeCommand());
            Register(new PanTiltResetCommand());
            Register(new ZoomVariableCommand());

            // Blackmagic PTZ Control extended commands
            Register(new ZoomDirectCommand());
            Register(new FocusVariableCommand());
            Register(new FocusDirectCommand());
            Register(new FocusModeCommand());
            Register(new FocusOnePushCommand());
            Register(new IrisVariableCommand());
            Register(new IrisDirectCommand());
            Register(new MemoryRecallCommand());
            Register(new MemorySetCommand());
            Register(new MemoryResetCommand());

            // Inquiry commands
            Register(new PanTiltPositionInquiryCommand());
            Register(new ZoomPositionInquiryCommand());
            Register(new FocusPositionInquiryCommand());
            Register(new FocusModeInquiryCommand());
        }

        /// <summary>
        /// Register a new VISCA command
        /// </summary>
        public void Register(IViscaCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _commands.Add(command);
        }

        /// <summary>
        /// Try to find and execute a command for the given frame
        /// </summary>
        /// <param name="frame">Raw VISCA frame</param>
        /// <param name="handler">Command handler</param>
        /// <param name="responder">Response callback</param>
        /// <returns>The command that was executed, or null if no match found</returns>
        public IViscaCommand TryExecute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            foreach (var command in _commands)
            {
                if (command.TryParse(frame))
                {
                    command.Execute(frame, handler, responder);
                    return command;
                }
            }
            return null;
        }

        /// <summary>
        /// Get command name for a frame (for logging purposes)
        /// </summary>
        public string GetCommandName(byte[] frame)
        {
            foreach (var command in _commands)
            {
                if (command.TryParse(frame))
                {
                    return command.CommandName;
                }
            }

            // Fallback to byte inspection for unknown commands
            if (frame == null || frame.Length < 5) return "Invalid";
            byte b1 = frame[1], b2 = frame[2], b3 = frame[3];
            return $"Unknown({b1:X2} {b2:X2} {b3:X2})";
        }

        /// <summary>
        /// Get detailed description for a frame (for logging purposes)
        /// </summary>
        public string GetCommandDetails(byte[] frame)
        {
            foreach (var command in _commands)
            {
                if (command.TryParse(frame))
                {
                    return command.GetDetails(frame);
                }
            }

            // Fallback for unknown commands
            string hex = frame != null ? BitConverter.ToString(frame) : "null";
            string name = GetCommandName(frame);
            return $"{name} [{hex}]";
        }

        /// <summary>
        /// Get all registered commands
        /// </summary>
        public IReadOnlyList<IViscaCommand> Commands => _commands.AsReadOnly();
    }
}
