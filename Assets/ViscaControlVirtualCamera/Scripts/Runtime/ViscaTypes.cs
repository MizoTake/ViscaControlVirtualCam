using System;

namespace ViscaControlVirtualCam
{
    public enum ViscaTransport
    {
        UdpRawVisca,
        TcpRawVisca,
        Both,
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
}
