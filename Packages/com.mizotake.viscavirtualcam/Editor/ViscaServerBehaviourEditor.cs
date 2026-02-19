using UnityEditor;
using UnityEngine;

namespace ViscaControlVirtualCam.Editor
{
    [CustomEditor(typeof(ViscaServerBehaviour))]
    public class ViscaServerBehaviourEditor : UnityEditor.Editor
    {
        private SerializedProperty autoStart;
        private SerializedProperty bindAddress;
        private SerializedProperty logLevel;
        private SerializedProperty logReceivedCommands;
        private SerializedProperty maxClients;
        private SerializedProperty operationMode;
        private SerializedProperty ptzController;
        private SerializedProperty realCameraIp;
        private SerializedProperty realCameraPort;
        private SerializedProperty replyMode;
        private SerializedProperty tcpPort;
        private SerializedProperty transport;
        private SerializedProperty udpPort;
        private SerializedProperty verboseLog;

        private void OnEnable()
        {
            autoStart = serializedObject.FindProperty("autoStart");
            transport = serializedObject.FindProperty("transport");
            bindAddress = serializedObject.FindProperty("bindAddress");
            udpPort = serializedObject.FindProperty("udpPort");
            tcpPort = serializedObject.FindProperty("tcpPort");
            maxClients = serializedObject.FindProperty("maxClients");
            replyMode = serializedObject.FindProperty("replyMode");
            operationMode = serializedObject.FindProperty("operationMode");
            realCameraIp = serializedObject.FindProperty("realCameraIp");
            realCameraPort = serializedObject.FindProperty("realCameraPort");
            verboseLog = serializedObject.FindProperty("verboseLog");
            logReceivedCommands = serializedObject.FindProperty("logReceivedCommands");
            logLevel = serializedObject.FindProperty("logLevel");
            ptzController = serializedObject.FindProperty("ptzController");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Server section
            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoStart);
            EditorGUILayout.PropertyField(transport);
            EditorGUILayout.PropertyField(bindAddress);
            EditorGUILayout.PropertyField(udpPort);
            EditorGUILayout.PropertyField(tcpPort);
            EditorGUILayout.PropertyField(maxClients);
            EditorGUILayout.PropertyField(replyMode);
            EditorGUILayout.PropertyField(operationMode);

            if ((ViscaOperationMode)operationMode.enumValueIndex != ViscaOperationMode.VirtualOnly)
            {
                EditorGUILayout.PropertyField(realCameraIp);
                EditorGUILayout.PropertyField(realCameraPort);
            }

            EditorGUILayout.Space();

            // Logging section
            EditorGUILayout.LabelField("Logging", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(verboseLog);

            // Only show detailed logging options if verboseLog is enabled
            if (verboseLog.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(logReceivedCommands);
                EditorGUILayout.PropertyField(logLevel);

                // Help box explaining log levels
                EditorGUILayout.HelpBox(GetLogLevelDescription((ViscaLogLevel)logLevel.enumValueIndex),
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Targets section
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(ptzController);

            EditorGUILayout.Space();

            // Runtime controls
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);
                var server = (ViscaServerBehaviour)target;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Start Server")) server.StartServer();
                if (GUILayout.Button("Stop Server")) server.StopServer();
                EditorGUILayout.EndHorizontal();

                // Runtime log level adjustment
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(logLevel, new GUIContent("Runtime Log Level"));
                if (EditorGUI.EndChangeCheck() && Application.isPlaying)
                {
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(target);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string GetLogLevelDescription(ViscaLogLevel level)
        {
            return level switch
            {
                ViscaLogLevel.None => "No logging output",
                ViscaLogLevel.Errors => "Log only errors",
                ViscaLogLevel.Warnings => "Log errors and warnings",
                ViscaLogLevel.Info => "Log errors, warnings, and connection events",
                ViscaLogLevel.Commands => "Log all received VISCA commands (verbose)",
                ViscaLogLevel.Debug => "Log everything including debug information",
                _ => "Unknown log level"
            };
        }
    }
}
