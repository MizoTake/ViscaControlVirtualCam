using System;
using System.Net;

namespace ViscaControlVirtualCam
{
    public enum IpSetupResponderMode
    {
        Unicast,
        Broadcast
    }

    public enum IpSetupAdvertisedAddressSource
    {
        BindAddress,
        CustomAddress
    }

    [Serializable]
    public sealed class VirtualDeviceIdentity
    {
        public string virtualMac = "88-C9-E8-00-00-03";
        public string modelName = "BRC-X400";
        public string serial = "VC000001";
        public string softVersion = "1.00";
        public int webPort = 80;
        public string friendlyName = "Visca Virtual Cam";
    }

    [Serializable]
    public sealed class VirtualNetworkConfig
    {
        public string logicalIp = "192.168.0.100";
        public string logicalMask = "255.255.255.0";
        public string logicalGateway = "0.0.0.0";
    }

    public sealed class IpSetupResponderOptions
    {
        public IPAddress BindAddress = IPAddress.Any;
        public int Port = ViscaProtocol.DefaultTcpPort;
        public IpSetupResponderMode ResponderMode = IpSetupResponderMode.Unicast;
        public int EnqDebounceMilliseconds = 250;
    }

    public sealed class IpSetupProcessResult
    {
        public bool ShouldRespond;
        public bool IsEnq;
        public bool IsSet;
        public string Summary = string.Empty;
        public string[] ResponseUnits = Array.Empty<string>();
    }
}
