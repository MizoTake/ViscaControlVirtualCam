using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ViscaControlVirtualCam
{
    public sealed class IpSetupMessageProcessor
    {
        private readonly Func<IPAddress, string> _advertisedIpResolver;
        private readonly VirtualDeviceIdentity _identity;
        private readonly VirtualNetworkConfig _network;

        public IpSetupMessageProcessor(
            VirtualDeviceIdentity identity,
            VirtualNetworkConfig network,
            Func<IPAddress, string> advertisedIpResolver)
        {
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _advertisedIpResolver = advertisedIpResolver;
        }

        public IpSetupProcessResult Process(IPEndPoint remoteEndpoint, IReadOnlyList<string> rawUnits)
        {
            if (rawUnits == null || rawUnits.Count == 0)
                return new IpSetupProcessResult
                {
                    ShouldRespond = false,
                    Summary = "IPSETUP Ignored: no units."
                };

            var units = ParseUnits(rawUnits);
            var isEnq = ContainsKey(units, "ENQ");
            var isSet = ContainsKey(units, "SET") ||
                        ContainsKey(units, "SETMAC") ||
                        ContainsKey(units, "MAC") ||
                        ContainsKey(units, "IPADR") ||
                        ContainsKey(units, "MASK") ||
                        ContainsKey(units, "GATEWAY") ||
                        ContainsKey(units, "WEBPORT") ||
                        ContainsKey(units, "NAME");

            if (isEnq)
                return HandleEnq(units, remoteEndpoint);

            if (isSet)
                return HandleSet(units);

            return new IpSetupProcessResult
            {
                ShouldRespond = false,
                Summary = "IPSETUP Ignored: unsupported request."
            };
        }

        private IpSetupProcessResult HandleEnq(List<IpSetupUnit> units, IPEndPoint remoteEndpoint)
        {
            var enqSelector = TryGetValue(units, "ENQ", out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : "network";
            var advertisedIp = ResolveAdvertisedIp(remoteEndpoint?.Address);

            var response = BuildNetworkInfoUnits(enqSelector, advertisedIp);

            return new IpSetupProcessResult
            {
                ShouldRespond = true,
                IsEnq = true,
                Summary = $"IPSETUP ENQ handled ({enqSelector})",
                ResponseUnits = response
            };
        }

        private IpSetupProcessResult HandleSet(List<IpSetupUnit> units)
        {
            if (!TryGetSetMac(units, out var setMac))
                return BuildNak("SETMAC_REQUIRED");

            if (!TryNormalizeMac(setMac, out var normalizedSetMac))
                return BuildNak("INVALID_SETMAC");

            if (!string.Equals(normalizedSetMac, _identity.virtualMac, StringComparison.OrdinalIgnoreCase))
                return BuildNak("MAC_MISMATCH");

            return new IpSetupProcessResult
            {
                ShouldRespond = true,
                IsSet = true,
                Summary = "IPSETUP SET/MAC accepted",
                ResponseUnits = new[] { $"ACK:{normalizedSetMac}" }
            };
        }

        private static bool TryGetSetMac(List<IpSetupUnit> units, out string setMac)
        {
            if (TryGetValue(units, "SETMAC", out setMac))
                return true;

            return TryGetValue(units, "MAC", out setMac);
        }

        private string[] BuildNetworkInfoUnits(string selector, string advertisedIp)
        {
            if (!string.Equals(selector, "network", StringComparison.OrdinalIgnoreCase))
                selector = "network";

            return new[]
            {
                $"MAC:{_identity.virtualMac}",
                $"INFO:{selector}",
                $"MODEL:{_identity.modelName}",
                $"SOFTVERSION:{_identity.softVersion}",
                $"IPADR:{advertisedIp}",
                $"MASK:{_network.logicalMask}",
                $"GATEWAY:{_network.logicalGateway}",
                $"NAME:{_identity.friendlyName}",
                "WRITE:on"
            };
        }

        private IpSetupProcessResult BuildNak(string reason)
        {
            return new IpSetupProcessResult
            {
                ShouldRespond = true,
                IsSet = true,
                Summary = $"IPSETUP SET rejected ({reason})",
                ResponseUnits = new[] { $"NAK:{reason}" }
            };
        }

        private string ResolveAdvertisedIp(IPAddress remoteAddress)
        {
            var resolved = _advertisedIpResolver?.Invoke(remoteAddress);
            if (IsValidIpv4(resolved) && !IsLoopbackIpv4(resolved))
                return resolved;

            return IsValidIpv4(_network.logicalIp) && !IsLoopbackIpv4(_network.logicalIp)
                ? _network.logicalIp
                : string.Empty;
        }

        private static bool IsValidIpv4(string value)
        {
            return IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
        }

        private static bool IsLoopbackIpv4(string value)
        {
            return IPAddress.TryParse(value, out var address) && IPAddress.IsLoopback(address);
        }

        public static bool TryNormalizeMac(string input, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var hex = new StringBuilder(12);
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (Uri.IsHexDigit(c))
                    hex.Append(char.ToUpperInvariant(c));
            }

            if (hex.Length != 12)
                return false;

            normalized = string.Concat(
                hex[0], hex[1], "-",
                hex[2], hex[3], "-",
                hex[4], hex[5], "-",
                hex[6], hex[7], "-",
                hex[8], hex[9], "-",
                hex[10], hex[11]);
            return true;
        }

        private static bool ContainsKey(List<IpSetupUnit> units, string key)
        {
            for (var i = 0; i < units.Count; i++)
                if (string.Equals(units[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool TryGetValue(List<IpSetupUnit> units, string key, out string value)
        {
            for (var i = units.Count - 1; i >= 0; i--)
                if (string.Equals(units[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = units[i].Value;
                    return true;
                }

            value = null;
            return false;
        }

        private static List<IpSetupUnit> ParseUnits(IReadOnlyList<string> rawUnits)
        {
            var units = new List<IpSetupUnit>(rawUnits.Count);
            for (var i = 0; i < rawUnits.Count; i++)
            {
                var raw = rawUnits[i] ?? string.Empty;
                var idx = raw.IndexOf(':');
                if (idx < 0)
                {
                    units.Add(new IpSetupUnit(raw.Trim(), string.Empty));
                    continue;
                }

                var key = raw.Substring(0, idx).Trim();
                var value = idx + 1 < raw.Length ? raw.Substring(idx + 1).Trim() : string.Empty;
                units.Add(new IpSetupUnit(key, value));
            }

            return units;
        }

        private readonly struct IpSetupUnit
        {
            public readonly string Key;
            public readonly string Value;

            public IpSetupUnit(string key, string value)
            {
                Key = key ?? string.Empty;
                Value = value ?? string.Empty;
            }
        }
    }
}
