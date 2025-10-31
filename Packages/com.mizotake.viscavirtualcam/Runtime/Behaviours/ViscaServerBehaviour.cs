using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    // MonoBehaviour adapter that hosts the pure ViscaServerCore and wires it to a PtzModel via PtzViscaHandler.
    [DefaultExecutionOrder(-50)]
    public class ViscaServerBehaviour : MonoBehaviour
    {
        [Header("Server")]
        public bool autoStart = true;
        public ViscaTransport transport = ViscaTransport.UdpRawVisca;
        public int udpPort = 52381;
        public int tcpPort = 52380;
        public int maxClients = 4;
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;

        [Header("Logging")]
        [Tooltip("Enable general logging (connection events, errors, etc.)")]
        public bool verboseLog = true;

        [Tooltip("Log every received VISCA command with detailed information")]
        public bool logReceivedCommands = true;

        [Tooltip("Log level for filtering messages")]
        public ViscaLogLevel logLevel = ViscaLogLevel.Commands;

        [Header("Targets")]
        public PtzControllerBehaviour ptzController;

        private readonly ConcurrentQueue<Action> _mainThreadActions = new();
        private ViscaServerCore _core;

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
            {
                try { act(); } catch (Exception e) { Debug.LogException(e); }
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

            var handler = new PtzViscaHandler(ptzController.Model, a => _mainThreadActions.Enqueue(a), replyMode);
            var opt = new ViscaServerOptions
            {
                Transport = transport,
                UdpPort = udpPort,
                TcpPort = tcpPort,
                MaxClients = maxClients,
                VerboseLog = verboseLog,
                LogReceivedCommands = logReceivedCommands,
                Logger = msg => LogMessage(msg)
            };
            _core = new ViscaServerCore(handler, opt);
            _core.Start();
        }

        private void LogMessage(string message)
        {
            if (!verboseLog) return;

            // Determine message type from content
            ViscaLogLevel messageLevel = ViscaLogLevel.Info;

            bool ContainsCI(string s, string needle) => s?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

            if (message.StartsWith("RX:"))
            {
                messageLevel = ViscaLogLevel.Commands;
            }
            // Explicitly classify known error patterns emitted by ViscaServerCore
            else if (
                ContainsCI(message, "invalid visca frame") ||
                ContainsCI(message, "no terminator") ||
                ContainsCI(message, "frame too large") ||
                ContainsCI(message, "receive error") ||
                ContainsCI(message, " error:") || // generic error phrasing
                ContainsCI(message, "exception"))
            {
                messageLevel = ViscaLogLevel.Errors;
            }
            else if (ContainsCI(message, "warning"))
            {
                messageLevel = ViscaLogLevel.Warnings;
            }
            else if (ContainsCI(message, "started") || ContainsCI(message, "stopped"))
            {
                messageLevel = ViscaLogLevel.Info;
            }
            else
            {
                messageLevel = ViscaLogLevel.Debug;
            }

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
            try { _core?.Stop(); } catch { }
            _core = null;
        }
    }
}
