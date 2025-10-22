using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    // MonoBehaviour adapter that hosts the pure ViscaServerCore and wires it to a PtzModel via PtzViscaHandler.
    [DefaultExecutionOrder(-50)]
    public class ViscaServerBehaviour : MonoBehaviour
    {
        [Header("Server")] public bool autoStart = true;
        public ViscaTransport transport = ViscaTransport.UdpRawVisca;
        public int udpPort = 52381;
        public int tcpPort = 52380;
        public int maxClients = 4;
        public ViscaReplyMode replyMode = ViscaReplyMode.AckAndCompletion;
        public bool verboseLog = true;

        [Header("Targets")] public PtzControllerBehaviour ptzController;

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
                Logger = msg => { if (verboseLog) Debug.Log(msg); }
            };
            _core = new ViscaServerCore(handler, opt);
            _core.Start();
        }

        public void StopServer()
        {
            try { _core?.Stop(); } catch { }
            _core = null;
        }
    }
}
