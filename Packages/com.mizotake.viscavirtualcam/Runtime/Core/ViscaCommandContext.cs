using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// All supported VISCA command types
    /// </summary>
    public enum ViscaCommandType
    {
        Unknown = 0,

        // Pan/Tilt commands
        PanTiltDrive,
        PanTiltAbsolute,
        PanTiltHome,
        PanTiltReset,

        // Zoom commands
        ZoomVariable,
        ZoomDirect,

        // Focus commands
        FocusVariable,
        FocusDirect,
        FocusMode,
        FocusOnePush,

        // Iris commands
        IrisVariable,
        IrisDirect,

        // Memory commands
        MemoryRecall,
        MemorySet,
        MemoryReset,

        // Inquiry commands
        PanTiltPositionInquiry,
        ZoomPositionInquiry,
        FocusPositionInquiry,
        FocusModeInquiry,
    }

    /// <summary>
    /// Encapsulates all data needed to handle a VISCA command.
    /// Immutable struct to avoid allocations.
    /// </summary>
    public readonly struct ViscaCommandContext
    {
        /// <summary>
        /// The type of command
        /// </summary>
        public readonly ViscaCommandType CommandType;

        /// <summary>
        /// Raw frame bytes (for reference/logging)
        /// </summary>
        public readonly byte[] Frame;

        /// <summary>
        /// Response callback
        /// </summary>
        public readonly Action<byte[]> Responder;

        // Pan/Tilt parameters
        public readonly byte PanSpeed;
        public readonly byte TiltSpeed;
        public readonly byte PanDirection;
        public readonly byte TiltDirection;
        public readonly ushort PanPosition;
        public readonly ushort TiltPosition;

        // Zoom parameters
        public readonly byte ZoomSpeed;
        public readonly ushort ZoomPosition;

        // Focus parameters
        public readonly byte FocusSpeed;
        public readonly ushort FocusPosition;
        public readonly byte FocusMode;

        // Iris parameters
        public readonly byte IrisDirection;
        public readonly ushort IrisPosition;

        // Memory parameters
        public readonly byte MemoryNumber;

        private ViscaCommandContext(
            ViscaCommandType commandType,
            byte[] frame,
            Action<byte[]> responder,
            byte panSpeed = 0,
            byte tiltSpeed = 0,
            byte panDirection = 0,
            byte tiltDirection = 0,
            ushort panPosition = 0,
            ushort tiltPosition = 0,
            byte zoomSpeed = 0,
            ushort zoomPosition = 0,
            byte focusSpeed = 0,
            ushort focusPosition = 0,
            byte focusMode = 0,
            byte irisDirection = 0,
            ushort irisPosition = 0,
            byte memoryNumber = 0)
        {
            CommandType = commandType;
            Frame = frame;
            Responder = responder;
            PanSpeed = panSpeed;
            TiltSpeed = tiltSpeed;
            PanDirection = panDirection;
            TiltDirection = tiltDirection;
            PanPosition = panPosition;
            TiltPosition = tiltPosition;
            ZoomSpeed = zoomSpeed;
            ZoomPosition = zoomPosition;
            FocusSpeed = focusSpeed;
            FocusPosition = focusPosition;
            FocusMode = focusMode;
            IrisDirection = irisDirection;
            IrisPosition = irisPosition;
            MemoryNumber = memoryNumber;
        }

        // Factory methods for each command type
        public static ViscaCommandContext PanTiltDrive(byte[] frame, Action<byte[]> responder,
            byte panSpeed, byte tiltSpeed, byte panDir, byte tiltDir)
        {
            return new ViscaCommandContext(
                ViscaCommandType.PanTiltDrive, frame, responder,
                panSpeed: panSpeed, tiltSpeed: tiltSpeed,
                panDirection: panDir, tiltDirection: tiltDir);
        }

        public static ViscaCommandContext PanTiltAbsolute(byte[] frame, Action<byte[]> responder,
            byte panSpeed, byte tiltSpeed, ushort panPos, ushort tiltPos)
        {
            return new ViscaCommandContext(
                ViscaCommandType.PanTiltAbsolute, frame, responder,
                panSpeed: panSpeed, tiltSpeed: tiltSpeed,
                panPosition: panPos, tiltPosition: tiltPos);
        }

        public static ViscaCommandContext PanTiltHome(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.PanTiltHome, frame, responder);
        }

        public static ViscaCommandContext PanTiltReset(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.PanTiltReset, frame, responder);
        }

        public static ViscaCommandContext ZoomVariable(byte[] frame, Action<byte[]> responder, byte zoomSpeed)
        {
            return new ViscaCommandContext(
                ViscaCommandType.ZoomVariable, frame, responder,
                zoomSpeed: zoomSpeed);
        }

        public static ViscaCommandContext ZoomDirect(byte[] frame, Action<byte[]> responder, ushort zoomPos)
        {
            return new ViscaCommandContext(
                ViscaCommandType.ZoomDirect, frame, responder,
                zoomPosition: zoomPos);
        }

        public static ViscaCommandContext FocusVariable(byte[] frame, Action<byte[]> responder, byte focusSpeed)
        {
            return new ViscaCommandContext(
                ViscaCommandType.FocusVariable, frame, responder,
                focusSpeed: focusSpeed);
        }

        public static ViscaCommandContext FocusDirect(byte[] frame, Action<byte[]> responder, ushort focusPos)
        {
            return new ViscaCommandContext(
                ViscaCommandType.FocusDirect, frame, responder,
                focusPosition: focusPos);
        }

        public static ViscaCommandContext FocusModeSet(byte[] frame, Action<byte[]> responder, byte mode)
        {
            return new ViscaCommandContext(
                ViscaCommandType.FocusMode, frame, responder,
                focusMode: mode);
        }

        public static ViscaCommandContext FocusOnePush(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.FocusOnePush, frame, responder);
        }

        public static ViscaCommandContext IrisVariable(byte[] frame, Action<byte[]> responder, byte irisDir)
        {
            return new ViscaCommandContext(
                ViscaCommandType.IrisVariable, frame, responder,
                irisDirection: irisDir);
        }

        public static ViscaCommandContext IrisDirect(byte[] frame, Action<byte[]> responder, ushort irisPos)
        {
            return new ViscaCommandContext(
                ViscaCommandType.IrisDirect, frame, responder,
                irisPosition: irisPos);
        }

        public static ViscaCommandContext MemoryRecall(byte[] frame, Action<byte[]> responder, byte memNum)
        {
            return new ViscaCommandContext(
                ViscaCommandType.MemoryRecall, frame, responder,
                memoryNumber: memNum);
        }

        public static ViscaCommandContext MemorySet(byte[] frame, Action<byte[]> responder, byte memNum)
        {
            return new ViscaCommandContext(
                ViscaCommandType.MemorySet, frame, responder,
                memoryNumber: memNum);
        }

        public static ViscaCommandContext MemoryReset(byte[] frame, Action<byte[]> responder, byte memNum)
        {
            return new ViscaCommandContext(
                ViscaCommandType.MemoryReset, frame, responder,
                memoryNumber: memNum);
        }

        public static ViscaCommandContext PanTiltPositionInquiry(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.PanTiltPositionInquiry, frame, responder);
        }

        public static ViscaCommandContext ZoomPositionInquiry(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.ZoomPositionInquiry, frame, responder);
        }

        public static ViscaCommandContext FocusPositionInquiry(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.FocusPositionInquiry, frame, responder);
        }

        public static ViscaCommandContext FocusModeInquiry(byte[] frame, Action<byte[]> responder)
        {
            return new ViscaCommandContext(ViscaCommandType.FocusModeInquiry, frame, responder);
        }

        /// <summary>
        /// Human-readable description for logging (lazy generation)
        /// </summary>
        public string GetDescription()
        {
            return CommandType switch
            {
                ViscaCommandType.PanTiltDrive =>
                    $"PanTiltDrive: PanSpeed={PanSpeed:X2}, TiltSpeed={TiltSpeed:X2}, PanDir={FormatDirection(PanDirection, true)}, TiltDir={FormatDirection(TiltDirection, false)}",
                ViscaCommandType.PanTiltAbsolute =>
                    $"PanTiltAbsolute: PanSpeed={PanSpeed:X2}, TiltSpeed={TiltSpeed:X2}, PanPos={PanPosition:X4}, TiltPos={TiltPosition:X4}",
                ViscaCommandType.PanTiltHome => "PanTiltHome",
                ViscaCommandType.PanTiltReset => "PanTiltReset",
                ViscaCommandType.ZoomVariable =>
                    $"ZoomVariable: {FormatZoomDirection(ZoomSpeed)}",
                ViscaCommandType.ZoomDirect =>
                    $"ZoomDirect: Position={ZoomPosition:X4}",
                ViscaCommandType.FocusVariable =>
                    $"FocusVariable: {FormatFocusDirection(FocusSpeed)}",
                ViscaCommandType.FocusDirect =>
                    $"FocusDirect: Position={FocusPosition:X4}",
                ViscaCommandType.FocusMode =>
                    $"FocusMode: {(FocusMode == ViscaProtocol.FocusModeAuto ? "Auto" : "Manual")}",
                ViscaCommandType.FocusOnePush => "FocusOnePush",
                ViscaCommandType.IrisVariable =>
                    $"IrisVariable: {FormatIrisDirection(IrisDirection)}",
                ViscaCommandType.IrisDirect =>
                    $"IrisDirect: Position={IrisPosition:X4}",
                ViscaCommandType.MemoryRecall =>
                    $"MemoryRecall: Preset={MemoryNumber}",
                ViscaCommandType.MemorySet =>
                    $"MemorySet: Preset={MemoryNumber}",
                ViscaCommandType.MemoryReset =>
                    $"MemoryReset: Preset={MemoryNumber}",
                ViscaCommandType.PanTiltPositionInquiry => "PanTiltPositionInquiry",
                ViscaCommandType.ZoomPositionInquiry => "ZoomPositionInquiry",
                ViscaCommandType.FocusPositionInquiry => "FocusPositionInquiry",
                ViscaCommandType.FocusModeInquiry => "FocusModeInquiry",
                _ => $"Unknown [{BitConverter.ToString(Frame)}]"
            };
        }

        private static string FormatDirection(byte dir, bool isPan)
        {
            return dir switch
            {
                ViscaProtocol.DirectionStop => "Stop",
                0x01 => isPan ? "Left" : "Up",
                0x02 => isPan ? "Right" : "Down",
                _ => $"0x{dir:X2}"
            };
        }

        private static string FormatZoomDirection(byte zz)
        {
            if (zz == 0x00) return "Stop";
            int dir = (zz >> 4) & 0x0F;
            int speed = zz & 0x0F;
            string dirStr = dir == ViscaProtocol.ZoomTeleNibble ? "Tele" : "Wide";
            return $"{dirStr} Speed={speed}";
        }

        private static string FormatFocusDirection(byte speed)
        {
            return speed switch
            {
                0x00 => "Stop",
                ViscaProtocol.FocusFar => "Far",
                ViscaProtocol.FocusNear => "Near",
                _ => $"0x{speed:X2}"
            };
        }

        private static string FormatIrisDirection(byte dir)
        {
            return dir switch
            {
                0x00 => "Stop",
                ViscaProtocol.IrisOpen => "Open",
                ViscaProtocol.IrisClose => "Close",
                _ => $"0x{dir:X2}"
            };
        }
    }
}
