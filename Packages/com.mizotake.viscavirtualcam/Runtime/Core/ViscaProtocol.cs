namespace ViscaControlVirtualCam
{
    /// <summary>
    /// VISCA protocol constants and specifications.
    /// Reference: Sony VISCA Protocol Manual
    /// </summary>
    public static class ViscaProtocol
    {
        public const byte DefaultSocketId = 0x01;

        // Frame structure
        public const byte FrameTerminator = 0xFF;
        public const byte AddressBroadcast = 0x88;
        public const byte AddressCamera1 = 0x81;
        public const int MinFrameLength = 3; // Address + Command + Terminator
        public const int MaxFrameLength = 16;

        // VISCA over IP header
        public const int ViscaIpHeaderLength = 8;
        public const byte IpPayloadTypeMsbVisca = 0x01;
        public const byte IpPayloadTypeLsbCommand = 0x00;
        public const byte IpPayloadTypeLsbInquiry = 0x10;
        public const byte IpPayloadTypeLsbReply = 0x11;
        public const byte IpPayloadTypeMsbControl = 0x02;
        public const byte IpPayloadTypeLsbControlCommand = 0x00;

        // Command categories (byte[1])
        public const byte CategoryCommand = 0x01;
        public const byte CategoryInquiry = 0x09;

        // Command groups (byte[2])
        public const byte GroupInterface = 0x00;
        public const byte GroupCamera = 0x04;
        public const byte GroupPanTilt = 0x06;

        // Response types
        public const byte ResponseAck = 0x40;
        public const byte ResponseCompletion = 0x50;
        public const byte ResponseError = 0x60;

        // Error codes
        public const byte ErrorMessageLength = 0x01;
        public const byte ErrorSyntax = 0x02;
        public const byte ErrorCommandBuffer = 0x03;
        public const byte ErrorCommandCancelled = 0x04;
        public const byte ErrorNoSocket = 0x05;
        public const byte ErrorCommandNotExecutable = 0x41;

        // Pan/Tilt speed limits (Sony standard)
        public const byte PanSpeedMin = 0x01;
        public const byte PanSpeedMax = 0x18; // 24 in decimal
        public const byte TiltSpeedMin = 0x01;
        public const byte TiltSpeedMax = 0x14; // 20 in decimal

        // Pan/Tilt direction codes
        public const byte DirectionLeft = 0x01;
        public const byte DirectionRight = 0x02;
        public const byte DirectionUp = 0x01;
        public const byte DirectionDown = 0x02;
        public const byte DirectionStop = 0x03;

        // Zoom direction codes (high nibble)
        public const byte ZoomTeleNibble = 0x02;
        public const byte ZoomWideNibble = 0x03;
        public const byte ZoomSpeedMax = 0x07;

        // Focus direction codes
        public const byte FocusFar = 0x02;
        public const byte FocusNear = 0x03;

        // Iris direction codes
        public const byte IrisOpen = 0x02;
        public const byte IrisClose = 0x03;

        // Focus mode
        public const byte FocusModeAuto = 0x02;
        public const byte FocusModeManual = 0x03;

        // Memory preset limits
        public const byte MemoryPresetMin = 0x00;
        public const byte MemoryPresetMax = 0xFF;
        public const int MemoryPresetLoadRangeDefault = 10; // 0-9 loaded by default

        // Position encoding (16-bit as 4 nibbles)
        public const ushort PositionMin = 0x0000;
        public const ushort PositionMax = 0xFFFF;
        public const ushort PositionCenter = 0x8000;

        // Default network ports
        public const int DefaultUdpPort = 52381;
        public const int DefaultTcpPort = 52380;

        // Math constants
        /// <summary>
        /// Epsilon for safe floating-point division (avoid NaN/Infinity).
        /// </summary>
        public const float DivisionEpsilon = 0.001f;

        /// <summary>
        /// Extract socket id from VISCA payload (lower nibble of first byte).
        /// Falls back to DefaultSocketId when unavailable or zero.
        /// </summary>
        public static byte ExtractSocketId(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
                return DefaultSocketId;

            // Command Cancel encodes socket in byte[1] low nibble (format: 8X 2Z FF)
            if (frame.Length >= 2 && (frame[1] & 0xF0) == 0x20)
            {
                byte cancelSocket = (byte)(frame[1] & 0x0F);
                if (cancelSocket != 0) return cancelSocket;
            }

            byte socket = (byte)(frame[0] & 0x0F);
            return socket == 0 ? DefaultSocketId : socket;
        }
    }

    /// <summary>
    /// Command identifiers for fast lookup.
    /// Combines distinguishing bytes into a single value.
    /// </summary>
    public readonly struct ViscaCommandKey
    {
        public readonly byte Category;  // byte[1]: 0x01=command, 0x09=inquiry
        public readonly byte Group;     // byte[2]: 0x04=camera, 0x06=pan/tilt
        public readonly byte SubCommand;// byte[3]: specific command

        public ViscaCommandKey(byte category, byte group, byte subCommand)
        {
            Category = category;
            Group = group;
            SubCommand = subCommand;
        }

        public static ViscaCommandKey FromFrame(byte[] frame)
        {
            if (frame == null || frame.Length < 4)
                return default;
            return new ViscaCommandKey(frame[1], frame[2], frame[3]);
        }

        public override int GetHashCode()
        {
            return (Category << 16) | (Group << 8) | SubCommand;
        }

        public override bool Equals(object obj)
        {
            return obj is ViscaCommandKey other &&
                   Category == other.Category &&
                   Group == other.Group &&
                   SubCommand == other.SubCommand;
        }

        public override string ToString()
        {
            return $"[{Category:X2} {Group:X2} {SubCommand:X2}]";
        }
    }
}
