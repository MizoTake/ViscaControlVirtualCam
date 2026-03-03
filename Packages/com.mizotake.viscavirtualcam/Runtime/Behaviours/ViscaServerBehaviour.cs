using System;
using System.Collections.Concurrent;
using System.Net;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    // MonoBehaviour adapter that hosts the pure ViscaServerCore and wires it to a PtzModel via PtzViscaHandler.
    [DefaultExecutionOrder(-50)]
    public class ViscaServerBehaviour : MonoBehaviour
    {
        private static readonly Action<byte[]> NoOpResponder = _ => { };

        [Header("Server")] public bool autoStart = true;

        public ViscaTransport transport = ViscaTransport.UdpRawVisca;
        [Tooltip("Server bind address (e.g. 0.0.0.0, 127.0.0.1, specific NIC address)")]
        public string bindAddress = "0.0.0.0";
        public int udpPort = 52381;
        public int tcpPort = 52380;
        public int maxClients = 4;
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;
        [Min(1)] public int pendingQueueLimit = 64;

        [Header("IP Setup (UDP 52380)")] public bool enableIpSetupResponder = true;

        [Min(1)] public int ipSetupPort = 52380;
        public IpSetupResponderMode ipSetupResponderMode = IpSetupResponderMode.Unicast;
        public IpSetupAdvertisedAddressSource ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.BindAddress;
        [Tooltip("Used when Advertised Address Source = CustomAddress")]
        public string ipSetupCustomAdvertisedAddress = "127.0.0.1";
        [Min(0)] public int ipSetupEnqDebounceMilliseconds = 250;
        public VirtualDeviceIdentity ipSetupIdentity = new VirtualDeviceIdentity();

        [Header("Operation Mode")] public ViscaOperationMode operationMode = ViscaOperationMode.VirtualOnly;

        [Header("Real Camera Forwarding")] public string realCameraIp = "192.168.1.10";

        [Min(1)]
        public int realCameraPort = 52381;

        [Header("Logging")] [Tooltip("Enable general logging (connection events, errors, etc.)")]
        public bool verboseLog = true;

        [Tooltip("Log every received VISCA command with detailed information")]
        public bool logReceivedCommands = true;

        [Tooltip("Log level for filtering messages")]
        public ViscaLogLevel logLevel = ViscaLogLevel.Commands;

        [Header("Targets")] public PtzControllerBehaviour ptzController;

        private readonly ConcurrentQueue<Action> _mainThreadActions = new();
        private readonly VirtualNetworkConfig _ipSetupNetwork = new();
        private ViscaServerCore _core;
        private ViscaForwarder _forwarder;
        private IpSetupResponder _ipSetupResponder;

        private void Awake()
        {
            if (ptzController == null) ptzController = FindObjectOfType<PtzControllerBehaviour>();
        }

        private void Start()
        {
            if (autoStart) StartServer();
        }

        private void Update()
        {
            while (_mainThreadActions.TryDequeue(out var act))
                try
                {
                    act();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        public void StartServer()
        {
            StopServer();
            if (ptzController == null || ptzController.Model == null)
            {
                Debug.LogError("ViscaServerBehaviour: PTZ controller/model not found.");
                return;
            }

            if (!TryParseBindAddress(out var bindIpAddress))
            {
                return;
            }

            StartIpSetupResponder(bindIpAddress);

            var handler = new PtzViscaHandler(ptzController.Model, a => _mainThreadActions.Enqueue(a), replyMode,
                msg => LogMessage(msg), pendingQueueLimit);
            Func<byte[], Action<byte[]>, Action<byte[]>> interceptor = null;

            if (operationMode != ViscaOperationMode.VirtualOnly)
            {
                try
                {
                    _forwarder = new ViscaForwarder(realCameraIp, realCameraPort, msg => LogMessage(msg));
                    _forwarder.Start();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VISCA] Failed to start forwarder: {e.Message}");
                    _forwarder?.Dispose();
                    _forwarder = null;
                    return;
                }

                interceptor = operationMode switch
                {
                    ViscaOperationMode.RealOnly => (packet, originalResponder) =>
                    {
                        _forwarder?.Forward(packet, originalResponder);
                        return null;
                    },
                    ViscaOperationMode.Linked => (packet, originalResponder) =>
                    {
                        _forwarder?.Forward(packet, originalResponder);
                        return NoOpResponder;
                    },
                    _ => null
                };
            }

            var opt = new ViscaServerOptions
            {
                Transport = transport,
                BindAddress = bindIpAddress,
                UdpPort = udpPort,
                TcpPort = tcpPort,
                MaxClients = maxClients,
                VerboseLog = verboseLog,
                LogReceivedCommands = logReceivedCommands,
                Logger = msg => LogMessage(msg),
                ProcessingInterceptor = interceptor
            };
            _core = new ViscaServerCore(handler, opt);
            try
            {
                _core.Start();
            }
            catch
            {
                _forwarder?.Dispose();
                _forwarder = null;
                StopIpSetupResponder();
                throw;
            }
        }

        private void LogMessage(string message)
        {
            if (!verboseLog) return;

            // Determine message type from content
            var messageLevel = ViscaLogLevel.Info;

            bool ContainsCI(string s, string needle)
            {
                return s?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (message.StartsWith("RX:"))
                messageLevel = ViscaLogLevel.Commands;
            // Explicitly classify known error patterns emitted by ViscaServerCore
            else if (
                ContainsCI(message, "invalid visca frame") ||
                ContainsCI(message, "no terminator") ||
                ContainsCI(message, "frame too large") ||
                ContainsCI(message, "receive error") ||
                ContainsCI(message, " error:") || // generic error phrasing
                ContainsCI(message, "exception"))
                messageLevel = ViscaLogLevel.Errors;
            else if (ContainsCI(message, "warning"))
                messageLevel = ViscaLogLevel.Warnings;
            else if (ContainsCI(message, "started") || ContainsCI(message, "stopped"))
                messageLevel = ViscaLogLevel.Info;
            else
                messageLevel = ViscaLogLevel.Debug;

            // Filter based on log level
            if ((int)messageLevel > (int)logLevel) return;

            // Output to Unity console with appropriate log type
            if (messageLevel <= ViscaLogLevel.Errors)
                Debug.LogError($"[VISCA] {message}");
            else if (messageLevel == ViscaLogLevel.Warnings)
                Debug.LogWarning($"[VISCA] {message}");
            else
                Debug.Log($"[VISCA] {message}");
        }

        public void StopServer()
        {
            StopIpSetupResponder();

            try
            {
                _core?.Stop();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VISCA] StopServer error: {e.Message}");
            }

            _core = null;

            try
            {
                _forwarder?.Stop();
                _forwarder?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VISCA] Forwarder stop error: {e.Message}");
            }

            _forwarder = null;
        }

        private void StartIpSetupResponder(IPAddress viscaBindAddress)
        {
            StopIpSetupResponder();
            if (!enableIpSetupResponder) return;

            if (ipSetupPort <= 0 || ipSetupPort > 65535)
            {
                Debug.LogError($"[VISCA] Invalid ipSetupPort: {ipSetupPort}. Use 1-65535.");
                return;
            }

            try
            {
                if (!TryResolveIpSetupAdvertisedAddress(viscaBindAddress, out var advertisedAddress))
                    return;

                EnsureIpSetupDefaults(advertisedAddress);

                var processor = new IpSetupMessageProcessor(
                    ipSetupIdentity,
                    _ipSetupNetwork,
                    _ => advertisedAddress);

                var options = new IpSetupResponderOptions
                {
                    BindAddress = IPAddress.Any,
                    Port = ipSetupPort,
                    ResponderMode = ipSetupResponderMode,
                    EnqDebounceMilliseconds = Math.Max(0, ipSetupEnqDebounceMilliseconds)
                };

                _ipSetupResponder = new IpSetupResponder(processor, options, msg => LogMessage(msg));
                _ipSetupResponder.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VISCA] Failed to start IP setup responder: {e.Message}");
                StopIpSetupResponder();
            }
        }

        private void StopIpSetupResponder()
        {
            try
            {
                _ipSetupResponder?.Stop();
                _ipSetupResponder?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VISCA] IP setup responder stop error: {e.Message}");
            }

            _ipSetupResponder = null;
        }

        private void EnsureIpSetupDefaults(string advertisedAddress)
        {
            ipSetupIdentity ??= new VirtualDeviceIdentity();

            if (!IpSetupMessageProcessor.TryNormalizeMac(ipSetupIdentity.virtualMac, out var normalizedMac))
                normalizedMac = "02:00:00:00:00:01";
            ipSetupIdentity.virtualMac = normalizedMac;

            if (string.IsNullOrWhiteSpace(ipSetupIdentity.modelName))
                ipSetupIdentity.modelName = "BRC-X400";
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.serial))
                ipSetupIdentity.serial = "VC000001";
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.softVersion))
                ipSetupIdentity.softVersion = "1.00";
            if (ipSetupIdentity.webPort <= 0 || ipSetupIdentity.webPort > 65535)
                ipSetupIdentity.webPort = 80;
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.friendlyName))
                ipSetupIdentity.friendlyName = "Visca Virtual Cam";

            if (!TryParseIpv4(_ipSetupNetwork.logicalIp, out _))
                _ipSetupNetwork.logicalIp = advertisedAddress;
            if (!TryParseIpv4(_ipSetupNetwork.logicalMask, out _))
                _ipSetupNetwork.logicalMask = "255.255.255.0";
            if (!TryParseIpv4(_ipSetupNetwork.logicalGateway, out _))
                _ipSetupNetwork.logicalGateway = "0.0.0.0";
        }

        private bool TryResolveIpSetupAdvertisedAddress(IPAddress viscaBindAddress, out string advertisedAddress)
        {
            advertisedAddress = null;

            if (ipSetupAdvertisedAddressSource == IpSetupAdvertisedAddressSource.BindAddress)
            {
                var value = viscaBindAddress?.ToString();
                if (!TryParseIpv4(value, out _))
                {
                    Debug.LogError(
                        $"[VISCA] IP Setup advertised address source is BindAddress but bindAddress '{bindAddress}' is not a concrete IPv4. Use a NIC IP or CustomAddress.");
                    return false;
                }

                advertisedAddress = value;
                return true;
            }

            if (!TryParseIpv4(ipSetupCustomAdvertisedAddress, out _))
            {
                Debug.LogError(
                    $"[VISCA] Invalid ipSetupCustomAdvertisedAddress: '{ipSetupCustomAdvertisedAddress}'. Use IPv4 like 192.168.0.100.");
                return false;
            }

            advertisedAddress = ipSetupCustomAdvertisedAddress.Trim();
            return true;
        }

        private static bool TryParseIpv4(string value, out IPAddress address)
        {
            if (!IPAddress.TryParse(value, out address))
                return false;
            return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private bool TryParseBindAddress(out IPAddress address)
        {
            var value = string.IsNullOrWhiteSpace(bindAddress) ? IPAddress.Any.ToString() : bindAddress.Trim();
            if (!IPAddress.TryParse(value, out address))
            {
                Debug.LogError($"[VISCA] Invalid bindAddress: '{bindAddress}'. Use IP format like 0.0.0.0 or 127.0.0.1.");
                return false;
            }

            return true;
        }
    }
}
