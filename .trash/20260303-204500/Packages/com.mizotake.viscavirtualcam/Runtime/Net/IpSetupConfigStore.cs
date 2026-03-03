using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    public sealed class IpSetupConfigStore
    {
        private const string IdentityFileName = "virtual_cam_identity.json";
        private const string NetworkFileName = "virtual_cam_network.json";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

        private readonly string _configDirectory;
        private readonly string _identityPath;
        private readonly Action<string> _logger;
        private readonly string _networkPath;

        public IpSetupConfigStore(string configDirectory = "config", Action<string> logger = null)
        {
            _configDirectory = string.IsNullOrWhiteSpace(configDirectory) ? "config" : configDirectory.Trim();
            _identityPath = Path.Combine(_configDirectory, IdentityFileName);
            _networkPath = Path.Combine(_configDirectory, NetworkFileName);
            _logger = logger;
        }

        public VirtualDeviceIdentity LoadOrCreateIdentity(int instanceId)
        {
            var changed = false;
            var identity = LoadJson<VirtualDeviceIdentity>(_identityPath);
            if (identity == null)
            {
                identity = new VirtualDeviceIdentity();
                changed = true;
            }

            if (identity.instanceId <= 0)
            {
                identity.instanceId = Math.Max(1, instanceId);
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(identity.virtualMac))
            {
                identity.virtualMac = GenerateVirtualMac();
                changed = true;
            }

            if (!TryNormalizeMac(identity.virtualMac, out var normalizedMac))
            {
                normalizedMac = GenerateVirtualMac();
                changed = true;
            }

            if (!string.Equals(identity.virtualMac, normalizedMac, StringComparison.Ordinal))
            {
                identity.virtualMac = normalizedMac;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(identity.modelName))
            {
                identity.modelName = "BRC-X400";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(identity.serial))
            {
                identity.serial = $"VC{normalizedMac.Replace(":", string.Empty).Substring(0, 8)}";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(identity.softVersion))
            {
                identity.softVersion = "1.00";
                changed = true;
            }

            if (identity.webPort <= 0 || identity.webPort > 65535)
            {
                identity.webPort = 80;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(identity.friendlyName))
            {
                identity.friendlyName = $"Visca Virtual Cam {identity.instanceId}";
                changed = true;
            }

            if (changed)
                SaveIdentity(identity);

            return identity;
        }

        public VirtualNetworkConfig LoadOrCreateNetwork(string defaultIp)
        {
            var changed = false;
            var network = LoadJson<VirtualNetworkConfig>(_networkPath);
            if (network == null)
            {
                network = new VirtualNetworkConfig();
                changed = true;
            }

            var fallbackIp = IsValidIpv4(defaultIp) ? defaultIp : "127.0.0.1";
            if (!IsValidIpv4(network.logicalIp))
            {
                network.logicalIp = fallbackIp;
                changed = true;
            }

            if (!IsValidIpv4(network.logicalMask))
            {
                network.logicalMask = "255.255.255.0";
                changed = true;
            }

            if (!IsValidIpv4(network.logicalGateway))
            {
                network.logicalGateway = "0.0.0.0";
                changed = true;
            }

            if (changed)
                SaveNetwork(network);

            return network;
        }

        public void SaveIdentity(VirtualDeviceIdentity identity)
        {
            if (identity == null) return;
            SaveJson(_identityPath, identity);
        }

        public void SaveNetwork(VirtualNetworkConfig network)
        {
            if (network == null) return;
            SaveJson(_networkPath, network);
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
                hex[0], hex[1], ":",
                hex[2], hex[3], ":",
                hex[4], hex[5], ":",
                hex[6], hex[7], ":",
                hex[8], hex[9], ":",
                hex[10], hex[11]);
            return true;
        }

        private static string GenerateVirtualMac()
        {
            var bytes = new byte[6];
            RandomNumberGenerator.Fill(bytes);
            bytes[0] = (byte)((bytes[0] | 0x02) & 0xFE); // locally administered / unicast
            return $"{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:{bytes[3]:X2}:{bytes[4]:X2}:{bytes[5]:X2}";
        }

        private T LoadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                _logger?.Invoke($"IPSETUP Config load failed: {path} ({e.Message})");
                return null;
            }
        }

        private void SaveJson<T>(string path, T value) where T : class
        {
            try
            {
                Directory.CreateDirectory(_configDirectory);

                var keepBom = HasUtf8Bom(path);
                var json = JsonUtility.ToJson(value, true);
                var encoding = keepBom ? Utf8WithBom : Utf8NoBom;
                File.WriteAllText(path, json, encoding);
            }
            catch (Exception e)
            {
                _logger?.Invoke($"IPSETUP Config save failed: {path} ({e.Message})");
            }
        }

        private static bool HasUtf8Bom(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 3)
                        return false;

                    var b0 = stream.ReadByte();
                    var b1 = stream.ReadByte();
                    var b2 = stream.ReadByte();
                    return b0 == 0xEF && b1 == 0xBB && b2 == 0xBF;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidIpv4(string value)
        {
            return IPAddress.TryParse(value, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }
    }
}
