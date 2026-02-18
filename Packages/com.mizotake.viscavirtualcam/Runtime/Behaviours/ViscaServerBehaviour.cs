using System;
using System.Collections.Concurrent;
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
        public int udpPort = 52381;
        public int tcpPort = 52380;
        public int maxClients = 4;
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;
        [Min(1)] public int pendingQueueLimit = 64;

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
        private ViscaServerCore _core;
        private ViscaForwarder _forwarder;

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
    }
}
