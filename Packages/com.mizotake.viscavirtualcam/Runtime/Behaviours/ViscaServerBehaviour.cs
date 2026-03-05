using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
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
        public IpSetupResponderMode ipSetupResponderMode = IpSetupResponderMode.Broadcast;
        public IpSetupAdvertisedAddressSource ipSetupAdvertisedAddressSource = IpSetupAdvertisedAddressSource.BindAddress;
        [Tooltip("Used when Advertised Address Source = CustomAddress")]
        public string ipSetupCustomAdvertisedAddress = "192.168.0.100";
        [Min(0)] public int ipSetupEnqDebounceMilliseconds = 250;
        public VirtualDeviceIdentity ipSetupIdentity = new VirtualDeviceIdentity();
        public VirtualNetworkConfig ipSetupNetwork = new VirtualNetworkConfig();

        [Header("Operation Mode")] public ViscaOperationMode operationMode = ViscaOperationMode.VirtualOnly;

        [Header("Real Camera Forwarding")] public string realCameraIp = "192.168.1.10";

        [Min(1)]
        public int realCameraPort = 52381;

        [Header("Inquiry Polling (Real Camera -> Virtual Sync)")]
        public bool enableInquiryPolling = false;

        [Min(20)] public int inquiryPollingIntervalMs = 100;
        [Min(1)] public int inquiryTimeoutMs = 150;
        [Min(0)] public int inquiryRetryCount = 1;
        public bool applyInquiryToVirtualCamera = true;

        [Header("Logging")] [Tooltip("Enable general logging (connection events, errors, etc.)")]
        public bool verboseLog = true;

        [Tooltip("Log every received VISCA command with detailed information")]
        public bool logReceivedCommands = true;

        [Tooltip("Log level for filtering messages")]
        public ViscaLogLevel logLevel = ViscaLogLevel.Commands;

        [Header("Targets")] public PtzControllerBehaviour ptzController;

        private readonly ConcurrentQueue<Action> _mainThreadActions = new();
        private readonly ManualResetEventSlim _inquiryStopEvent = new(false);
        private ViscaServerCore _core;
        private ViscaForwarder _forwarder;
        private ViscaInquiryClient _inquiryClient;
        private Thread _inquiryThread;
        private volatile bool _inquiryRunning;
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
                StartInquiryPolling();
            }
            catch
            {
                StopInquiryPolling();
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
            StopInquiryPolling();
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

        private void StartInquiryPolling()
        {
            StopInquiryPolling();

            if (!enableInquiryPolling || !applyInquiryToVirtualCamera)
                return;
            if (ptzController == null)
            {
                LogMessage("Inquiry polling skipped: ptzController is not assigned.");
                return;
            }
            if (ptzController.Model == null)
            {
                LogMessage("Inquiry polling skipped: ptzController.Model is null.");
                return;
            }

            try
            {
                _inquiryClient = new ViscaInquiryClient(
                    realCameraIp,
                    realCameraPort,
                    Mathf.Max(1, inquiryTimeoutMs),
                    Mathf.Max(0, inquiryRetryCount),
                    ViscaProtocol.DefaultSocketId,
                    msg => LogMessage(msg));

                _inquiryStopEvent.Reset();
                _inquiryRunning = true;
                _inquiryThread = new Thread(InquiryLoop)
                {
                    IsBackground = true,
                    Name = "ViscaInquiryPolling"
                };
                _inquiryThread.Start();
                LogMessage(
                    $"Inquiry polling started: {realCameraIp}:{realCameraPort}, interval={Mathf.Max(20, inquiryPollingIntervalMs)}ms, timeout={Mathf.Max(1, inquiryTimeoutMs)}ms, retries={Mathf.Max(0, inquiryRetryCount)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[VISCA] Failed to start inquiry polling: {e.Message}");
                StopInquiryPolling();
            }
        }

        private void StopInquiryPolling()
        {
            _inquiryRunning = false;
            _inquiryStopEvent.Set();

            var thread = _inquiryThread;
            _inquiryThread = null;
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
                thread.Join(500);

            _inquiryClient?.Dispose();
            _inquiryClient = null;
        }

        private void InquiryLoop()
        {
            while (_inquiryRunning)
            {
                var startTick = Environment.TickCount;
                try
                {
                    if (_inquiryClient != null && _inquiryClient.TryGetStatus(out var status))
                    {
                        _mainThreadActions.Enqueue(() =>
                        {
                            if (!_inquiryRunning || !applyInquiryToVirtualCamera)
                                return;
                            if (ptzController == null)
                                return;

                            ptzController.ApplyInquiryStatus(in status);
                        });
                    }
                }
                catch (Exception e)
                {
                    LogMessage($"Inquiry polling error: {e.Message}");
                }

                var elapsedMs = (int)((uint)Environment.TickCount - (uint)startTick);
                var intervalMs = Mathf.Max(20, inquiryPollingIntervalMs);
                var waitMs = Math.Max(0, intervalMs - elapsedMs);
                if (_inquiryStopEvent.Wait(waitMs))
                    break;
            }
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
                    ipSetupNetwork,
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
            ipSetupNetwork ??= new VirtualNetworkConfig();

            if (!IpSetupMessageProcessor.TryNormalizeMac(ipSetupIdentity.virtualMac, out var normalizedMac))
                normalizedMac = "88-C9-E8-00-00-03";
            ipSetupIdentity.virtualMac = normalizedMac;

            if (string.IsNullOrWhiteSpace(ipSetupIdentity.modelName))
                ipSetupIdentity.modelName = "IPCA";
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.serial))
                ipSetupIdentity.serial = "VC000001";
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.softVersion))
                ipSetupIdentity.softVersion = "2.10";
            if (ipSetupIdentity.webPort <= 0 || ipSetupIdentity.webPort > 65535)
                ipSetupIdentity.webPort = 80;
            if (string.IsNullOrWhiteSpace(ipSetupIdentity.friendlyName))
                ipSetupIdentity.friendlyName = "CAM1";

            if (!TryParseIpv4(ipSetupNetwork.logicalIp, out _))
                ipSetupNetwork.logicalIp = advertisedAddress;
            if (!TryParseIpv4(ipSetupNetwork.logicalMask, out _))
                ipSetupNetwork.logicalMask = "255.255.255.0";
            if (!TryParseIpv4(ipSetupNetwork.logicalGateway, out _))
                ipSetupNetwork.logicalGateway = "0.0.0.0";
        }

        private bool TryResolveIpSetupAdvertisedAddress(IPAddress viscaBindAddress, out string advertisedAddress)
        {
            advertisedAddress = null;

            if (ipSetupAdvertisedAddressSource == IpSetupAdvertisedAddressSource.BindAddress)
            {
                var value = viscaBindAddress?.ToString();
                if (!TryParseIpv4(value, out _) || !IsConcreteIpv4(viscaBindAddress))
                {
                    Debug.LogError(
                        $"[VISCA] IP Setup advertised address source is BindAddress but bindAddress '{bindAddress}' is not a concrete IPv4. Use a NIC IP or CustomAddress.");
                    return false;
                }
                if (IPAddress.IsLoopback(viscaBindAddress))
                {
                    Debug.LogError(
                        $"[VISCA] IP Setup advertised address must be real IPv4. Loopback '{value}' is not allowed.");
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
            if (IPAddress.IsLoopback(IPAddress.Parse(ipSetupCustomAdvertisedAddress.Trim())))
            {
                Debug.LogError(
                    $"[VISCA] IP Setup advertised address must be real IPv4. Loopback '{ipSetupCustomAdvertisedAddress}' is not allowed.");
                return false;
            }

            advertisedAddress = ipSetupCustomAdvertisedAddress.Trim();
            return true;
        }

        private static bool IsConcreteIpv4(IPAddress address)
        {
            if (address == null)
                return false;
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;
            if (IPAddress.Any.Equals(address) || IPAddress.None.Equals(address) || IPAddress.Broadcast.Equals(address))
                return false;
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
