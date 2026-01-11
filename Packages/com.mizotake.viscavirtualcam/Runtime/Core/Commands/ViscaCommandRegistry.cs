using System;
using System.Collections.Generic;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Delegate for parsing a frame into a command context.
    /// Returns null if frame doesn't match expected format.
    /// </summary>
    public delegate ViscaCommandContext? CommandParser(byte[] frame, Action<byte[]> responder);

    /// <summary>
    /// Registry for VISCA commands with O(1) lookup.
    /// Commands are indexed by their distinguishing byte pattern.
    /// </summary>
    public sealed class ViscaCommandRegistry
    {
        /// <summary>
        /// Registered command entry
        /// </summary>
        public readonly struct CommandEntry
        {
            public readonly ViscaCommandType Type;
            public readonly string Name;
            public readonly CommandParser Parser;

            public CommandEntry(ViscaCommandType type, string name, CommandParser parser)
            {
                Type = type;
                Name = name;
                Parser = parser;
            }
        }

        // Primary lookup: key -> command entry (O(1) for exact matches)
        private readonly Dictionary<int, CommandEntry> _commandsByKey = new Dictionary<int, CommandEntry>();

        // Secondary lookup for commands that need length/content inspection
        private readonly List<CommandEntry> _variableLengthCommands = new List<CommandEntry>();

        public ViscaCommandRegistry()
        {
            RegisterDefaultCommands();
        }

        private void RegisterDefaultCommands()
        {
            // Command Cancel: 8X 2Z FF (Z=socket). Minimal length 3 bytes.
            RegisterVariable(ViscaCommandType.CommandCancel, "CommandCancel",
                (frame, responder) =>
                {
                    if (frame.Length == 3 && (frame[1] & 0xF0) == 0x20 && frame[2] == ViscaProtocol.FrameTerminator)
                        return ViscaCommandContext.CommandCancel(frame, responder);
                    return null;
                });

            // Pan/Tilt Drive: 8X 01 06 01 VV WW PP TT FF
            Register(0x01, 0x06, 0x01, ViscaCommandType.PanTiltDrive, "PanTiltDrive",
                (frame, responder) =>
                {
                    if (frame.Length < 9) return null;
                    return ViscaCommandContext.PanTiltDrive(frame, responder,
                        frame[4], frame[5], frame[6], frame[7]);
                });

            // Pan/Tilt Absolute: 8X 01 06 02 VV WW 0p 0p 0p 0p 0t 0t 0t 0t FF
            Register(0x01, 0x06, 0x02, ViscaCommandType.PanTiltAbsolute, "PanTiltAbsolute",
                (frame, responder) =>
                {
                    // Accept both 13-byte (no speed) and 15+ byte (with speed) variants for compatibility
                    if (frame.Length < 13) return null;

                    bool hasSpeed = frame.Length >= 15;
                    int posStart = hasSpeed ? 6 : 4; // nibble positions start after optional VV/WW
                    int requiredLength = posStart + 8 + 1; // 8 nibbles + terminator
                    if (frame.Length < requiredLength) return null;

                    byte vv = 0, ww = 0;
                    if (hasSpeed)
                    {
                        vv = frame[4];
                        ww = frame[5];
                    }

                    ushort pan = ViscaParser.DecodeNibble16(frame[posStart], frame[posStart + 1], frame[posStart + 2], frame[posStart + 3]);
                    ushort tilt = ViscaParser.DecodeNibble16(frame[posStart + 4], frame[posStart + 5], frame[posStart + 6], frame[posStart + 7]);

                    return ViscaCommandContext.PanTiltAbsolute(frame, responder, vv, ww, pan, tilt);
                });

            // Pan/Tilt Home: 8X 01 06 04 FF
            Register(0x01, 0x06, 0x04, ViscaCommandType.PanTiltHome, "PanTiltHome",
                (frame, responder) => ViscaCommandContext.PanTiltHome(frame, responder));

            // Pan/Tilt Reset: 8X 01 06 05 FF
            Register(0x01, 0x06, 0x05, ViscaCommandType.PanTiltReset, "PanTiltReset",
                (frame, responder) => ViscaCommandContext.PanTiltReset(frame, responder));

            // Zoom Variable: 8X 01 04 07 ZZ FF
            Register(0x01, 0x04, 0x07, ViscaCommandType.ZoomVariable, "ZoomVariable",
                (frame, responder) =>
                {
                    if (frame.Length < 6) return null;
                    return ViscaCommandContext.ZoomVariable(frame, responder, frame[4]);
                });

            // Zoom Direct: 8X 01 04 47 0p 0p 0p 0p FF
            Register(0x01, 0x04, 0x47, ViscaCommandType.ZoomDirect, "ZoomDirect",
                (frame, responder) =>
                {
                    if (frame.Length < 9) return null;
                    ushort pos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
                    return ViscaCommandContext.ZoomDirect(frame, responder, pos);
                });

            // Focus Variable: 8X 01 04 08 ZZ FF
            Register(0x01, 0x04, 0x08, ViscaCommandType.FocusVariable, "FocusVariable",
                (frame, responder) =>
                {
                    if (frame.Length < 6) return null;
                    return ViscaCommandContext.FocusVariable(frame, responder, frame[4]);
                });

            // Focus Direct: 8X 01 04 48 0p 0p 0p 0p FF
            Register(0x01, 0x04, 0x48, ViscaCommandType.FocusDirect, "FocusDirect",
                (frame, responder) =>
                {
                    if (frame.Length < 9) return null;
                    ushort pos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
                    return ViscaCommandContext.FocusDirect(frame, responder, pos);
                });

            // Focus Mode: 8X 01 04 38 02/03 FF (Auto/Manual)
            Register(0x01, 0x04, 0x38, ViscaCommandType.FocusMode, "FocusMode",
                (frame, responder) =>
                {
                    if (frame.Length < 6) return null;
                    return ViscaCommandContext.FocusModeSet(frame, responder, frame[4]);
                });

            // Focus One Push: 8X 01 04 18 01 FF
            Register(0x01, 0x04, 0x18, ViscaCommandType.FocusOnePush, "FocusOnePush",
                (frame, responder) => ViscaCommandContext.FocusOnePush(frame, responder));

            // Iris Variable: 8X 01 04 0B 00/02/03 FF
            Register(0x01, 0x04, 0x0B, ViscaCommandType.IrisVariable, "IrisVariable",
                (frame, responder) =>
                {
                    if (frame.Length < 6) return null;
                    return ViscaCommandContext.IrisVariable(frame, responder, frame[4]);
                });

            // Iris Direct: 8X 01 04 4B 0p 0p 0p 0p FF
            Register(0x01, 0x04, 0x4B, ViscaCommandType.IrisDirect, "IrisDirect",
                (frame, responder) =>
                {
                    if (frame.Length < 9) return null;
                    ushort pos = ViscaParser.DecodeNibble16(frame[4], frame[5], frame[6], frame[7]);
                    return ViscaCommandContext.IrisDirect(frame, responder, pos);
                });

            // Memory Recall: 8X 01 04 3F 02 PP FF
            RegisterVariable(ViscaCommandType.MemoryRecall, "MemoryRecall",
                (frame, responder) =>
                {
                    if (frame.Length < 7) return null;
                    if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x3F || frame[4] != 0x02)
                        return null;
                    return ViscaCommandContext.MemoryRecall(frame, responder, frame[5]);
                });

            // Memory Set: 8X 01 04 3F 01 PP FF
            RegisterVariable(ViscaCommandType.MemorySet, "MemorySet",
                (frame, responder) =>
                {
                    if (frame.Length < 7) return null;
                    if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x3F || frame[4] != 0x01)
                        return null;
                    return ViscaCommandContext.MemorySet(frame, responder, frame[5]);
                });

            // Memory Reset: 8X 01 04 3F 00 PP FF
            RegisterVariable(ViscaCommandType.MemoryReset, "MemoryReset",
                (frame, responder) =>
                {
                    if (frame.Length < 7) return null;
                    if (frame[1] != 0x01 || frame[2] != 0x04 || frame[3] != 0x3F || frame[4] != 0x00)
                        return null;
                    return ViscaCommandContext.MemoryReset(frame, responder, frame[5]);
                });

            // Pan/Tilt Position Inquiry: 8X 09 06 12 FF
            Register(0x09, 0x06, 0x12, ViscaCommandType.PanTiltPositionInquiry, "PanTiltPositionInquiry",
                (frame, responder) => ViscaCommandContext.PanTiltPositionInquiry(frame, responder));

            // Zoom Position Inquiry: 8X 09 04 47 FF
            Register(0x09, 0x04, 0x47, ViscaCommandType.ZoomPositionInquiry, "ZoomPositionInquiry",
                (frame, responder) => ViscaCommandContext.ZoomPositionInquiry(frame, responder));

            // Focus Position Inquiry: 8X 09 04 48 FF
            Register(0x09, 0x04, 0x48, ViscaCommandType.FocusPositionInquiry, "FocusPositionInquiry",
                (frame, responder) => ViscaCommandContext.FocusPositionInquiry(frame, responder));

            // Focus Mode Inquiry: 8X 09 04 38 FF
            Register(0x09, 0x04, 0x38, ViscaCommandType.FocusModeInquiry, "FocusModeInquiry",
                (frame, responder) => ViscaCommandContext.FocusModeInquiry(frame, responder));
        }

        /// <summary>
        /// Register a command with exact 3-byte key match (O(1) lookup)
        /// </summary>
        public void Register(byte category, byte group, byte subCommand,
            ViscaCommandType type, string name, CommandParser parser)
        {
            int key = (category << 16) | (group << 8) | subCommand;
            _commandsByKey[key] = new CommandEntry(type, name, parser);
        }

        /// <summary>
        /// Register a command that requires additional inspection (falls back to O(n) for these)
        /// </summary>
        public void RegisterVariable(ViscaCommandType type, string name, CommandParser parser)
        {
            _variableLengthCommands.Add(new CommandEntry(type, name, parser));
        }

        /// <summary>
        /// Try to parse and execute a command.
        /// O(1) for standard commands, O(m) for variable-length commands where m is small.
        /// </summary>
        /// <returns>Command context if handled, null if unknown command</returns>
        public ViscaCommandContext? TryExecute(byte[] frame, IViscaCommandHandler handler, Action<byte[]> responder)
        {
            if (frame == null || frame.Length == 0)
                return null;

            // O(1) lookup for exact key match (only when enough bytes are present)
            if (frame.Length >= 4)
            {
                int key = (frame[1] << 16) | (frame[2] << 8) | frame[3];
                if (_commandsByKey.TryGetValue(key, out var entry))
                {
                    var contextNullable = entry.Parser(frame, responder);
                    if (contextNullable.HasValue)
                    {
                        var context = contextNullable.Value;
                        handler.Handle(in context);
                        return contextNullable;
                    }
                }
            }

            // Fallback for variable-length commands (Memory commands with sub-type in byte[4])
            foreach (var varEntry in _variableLengthCommands)
            {
                var contextNullable = varEntry.Parser(frame, responder);
                if (contextNullable.HasValue)
                {
                    var context = contextNullable.Value;
                    handler.Handle(in context);
                    return contextNullable;
                }
            }

            return null;
        }

        /// <summary>
        /// Get command name for logging (O(1) for known commands)
        /// </summary>
        public string GetCommandName(byte[] frame)
        {
            if (frame == null || frame.Length < 4)
                return "Invalid";

            int key = (frame[1] << 16) | (frame[2] << 8) | frame[3];
            if (_commandsByKey.TryGetValue(key, out var entry))
                return entry.Name;

            foreach (var varEntry in _variableLengthCommands)
            {
                if (varEntry.Parser(frame, _ => { }) != null)
                    return varEntry.Name;
            }

            return $"Unknown({frame[1]:X2} {frame[2]:X2} {frame[3]:X2})";
        }

        /// <summary>
        /// Get command details for logging.
        /// Uses lazy description generation to reduce allocations.
        /// </summary>
        public string GetCommandDetails(byte[] frame, Action<byte[]> responder)
        {
            if (frame == null || frame.Length < 4)
                return "Invalid frame";

            int key = (frame[1] << 16) | (frame[2] << 8) | frame[3];
            if (_commandsByKey.TryGetValue(key, out var entry))
            {
                var context = entry.Parser(frame, responder);
                if (context.HasValue)
                    return context.Value.GetDescription();
            }

            foreach (var varEntry in _variableLengthCommands)
            {
                var context = varEntry.Parser(frame, responder);
                if (context.HasValue)
                    return context.Value.GetDescription();
            }

            return $"Unknown [{BitConverter.ToString(frame)}]";
        }

        /// <summary>
        /// Number of registered commands
        /// </summary>
        public int Count => _commandsByKey.Count + _variableLengthCommands.Count;
    }
}
