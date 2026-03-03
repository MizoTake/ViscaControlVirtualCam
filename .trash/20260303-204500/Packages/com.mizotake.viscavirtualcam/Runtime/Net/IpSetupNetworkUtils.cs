using System;
using System.Net;
using System.Net.Sockets;

namespace ViscaControlVirtualCam
{
    public static class IpSetupNetworkUtils
    {
        public static string ResolveLocalIpv4(IPAddress bindAddress, IPAddress remoteAddress)
        {
            if (IsUsableIpv4(bindAddress))
                return bindAddress.ToString();

            if (remoteAddress != null && remoteAddress.AddressFamily == AddressFamily.InterNetwork)
            {
                try
                {
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.Connect(new IPEndPoint(remoteAddress, 9));
                        if (socket.LocalEndPoint is IPEndPoint local && IsUsableIpv4(local.Address))
                            return local.Address.ToString();
                    }
                }
                catch
                {
                    // Fall through to host address lookup.
                }
            }

            try
            {
                var addresses = Dns.GetHostAddresses(Dns.GetHostName());
                for (var i = 0; i < addresses.Length; i++)
                {
                    var address = addresses[i];
                    if (IsUsableIpv4(address))
                        return address.ToString();
                }
            }
            catch
            {
                // Fallback below.
            }

            return IPAddress.Loopback.ToString();
        }

        private static bool IsUsableIpv4(IPAddress address)
        {
            if (address == null)
                return false;
            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;
            if (IPAddress.Any.Equals(address) || IPAddress.None.Equals(address) || IPAddress.Broadcast.Equals(address))
                return false;
            if (IPAddress.IsLoopback(address))
                return false;
            return true;
        }
    }
}
