using System;

namespace ViscaControlVirtualCam
{
    public enum ViscaTransport
    {
        UdpRawVisca,
        TcpRawVisca,

        Both
        // Future: SonyViscaOverIp
    }

    public enum ViscaReplyMode
    {
        None,
        AckOnly,
        AckAndCompletion
    }

    public enum AxisDirection
    {
        Negative = -1,
        Stop = 0,
        Positive = 1
    }

    [Flags]
    public enum PtzAxes
    {
        None = 0,
        Pan = 1,
        Tilt = 2,
        Zoom = 4,
        All = Pan | Tilt | Zoom
    }

    public enum ViscaLogLevel
    {
        None = 0, // No logging
        Errors = 1, // Log only errors
        Warnings = 2, // Log errors and warnings
        Info = 3, // Log errors, warnings, and info (connection events)
        Commands = 4, // Log all received commands (verbose)
        Debug = 5 // Log everything including debug info
    }
}