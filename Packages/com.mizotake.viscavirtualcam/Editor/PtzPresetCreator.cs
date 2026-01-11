#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ViscaControlVirtualCam.Editor
{
    public static class PtzPresetCreator
    {
        private const string PresetDir = "Assets/ViscaControlVirtualCamera/Presets";

        [MenuItem("Tools/Visca/Create PTZ Presets (Indoor Outdoor Fast)", priority = 12)]
        public static void CreatePresets()
        {
            if (!Directory.Exists(PresetDir)) Directory.CreateDirectory(PresetDir);
            CreatePreset("PTZ_Indoor.asset", s =>
            {
                s.panMaxDegPerSec = 60f;
                s.tiltMaxDegPerSec = 45f;
                s.zoomMaxFovPerSec = 20f;
                s.minFov = 20f;
                s.maxFov = 80f;
                s.speedGamma = 1.2f;
            });
            CreatePreset("PTZ_Outdoor.asset", s =>
            {
                s.panMaxDegPerSec = 120f;
                s.tiltMaxDegPerSec = 90f;
                s.zoomMaxFovPerSec = 35f;
                s.minFov = 15f;
                s.maxFov = 90f;
                s.speedGamma = 1.0f;
            });
            CreatePreset("PTZ_FastMove.asset", s =>
            {
                s.panMaxDegPerSec = 200f;
                s.tiltMaxDegPerSec = 150f;
                s.zoomMaxFovPerSec = 50f;
                s.minFov = 15f;
                s.maxFov = 100f;
                s.speedGamma = 0.9f;
            });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("PTZ Presets", $"Created/Updated presets under:\n{PresetDir}", "OK");
        }

        private static void CreatePreset(string name, Action<PtzSettings> configure)
        {
            var path = Path.Combine(PresetDir, name);
            PtzSettings settings = null;
            if (File.Exists(path)) settings = AssetDatabase.LoadAssetAtPath<PtzSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PtzSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }

            configure(settings);
            EditorUtility.SetDirty(settings);
        }
    }
}
#endif