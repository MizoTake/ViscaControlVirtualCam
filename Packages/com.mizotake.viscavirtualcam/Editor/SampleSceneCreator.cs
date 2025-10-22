// Editor-only utility to generate a simple PTZ sample scene
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ViscaControlVirtualCam.Editor
{
    public static class SampleSceneCreator
    {
        private const string SceneDir = "Assets/ViscaControlVirtualCamera/Scenes";
        private const string ScenePath = SceneDir + "/PTZ_Sample.unity";

        [MenuItem("Tools/Visca/Create PTZ Sample Scene", priority = 10)]
        public static void CreateSample()
        {
            if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "PTZ_Sample";

            // Find or create main camera under a PTZ rig
            var rig = new GameObject("PTZ Rig");
            var pan = rig.transform; // pan pivot
            var tiltGo = new GameObject("Tilt Pivot");
            var tilt = tiltGo.transform;
            tilt.SetParent(pan, false);

            // Create camera under tilt
            Camera cam;
            var camGo = new GameObject("PTZ Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.transform.SetParent(tilt, false);
            cam.transform.localPosition = new Vector3(0, 0, 0);
            cam.transform.localRotation = Quaternion.identity;
            cam.fieldOfView = 60f;

            // Add controller behaviour to rig
            var controller = rig.AddComponent<PtzControllerBehaviour>();
            controller.panPivot = pan;
            controller.tiltPivot = tilt;
            controller.targetCamera = cam;

            // Create PTZ settings asset (preset) under Assets and assign
            // Ensure default and sample presets exist
            PtzPresetCreator.CreatePresets();
            var defaultPath = "Assets/ViscaControlVirtualCamera/Presets/PTZ_Outdoor.asset";
            var defaultSettings = AssetDatabase.LoadAssetAtPath<PtzSettings>(defaultPath);
            controller.settings = defaultSettings != null ? defaultSettings : AssetDatabase.LoadAssetAtPath<PtzSettings>("Assets/ViscaControlVirtualCamera/Presets/PTZ_Indoor.asset");

            // Add server behaviour object
            var serverGo = new GameObject("VISCA Server");
            var server = serverGo.AddComponent<ViscaServerBehaviour>();
            server.ptzController = controller;
            server.autoStart = true;
            server.udpPort = 52381;

            // Position rig and a simple cube to look at
            rig.transform.position = new Vector3(0, 1.2f, -4f);
            var targetCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetCube.name = "Target";
            targetCube.transform.position = Vector3.zero;

            // Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("PTZ Sample", $"Sample scene created:\n{ScenePath}\n\nRun play mode and send VISCA commands to UDP {server.udpPort}.", "OK");
        }
    }
}
#endif
