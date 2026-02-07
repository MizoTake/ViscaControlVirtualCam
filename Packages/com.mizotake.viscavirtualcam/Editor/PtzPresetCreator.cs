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

        [MenuItem("Tools/Visca/Create PTZ Presets (Indoor Outdoor Fast BRC-X400)", priority = 12)]
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
            CreatePreset("PTZ_BRC-X400.asset", s =>
            {
                s.panMaxDegPerSec = 101f;
                s.tiltMaxDegPerSec = 91f;
                s.panMinDegPerSec = 0.5f;
                s.tiltMinDegPerSec = 0.5f;
                s.panPresetMaxDegPerSec = 300f;
                s.tiltPresetMaxDegPerSec = 126f;
                s.panPresetMinDegPerSec = 1.1f;
                s.tiltPresetMinDegPerSec = 1.1f;
                s.zoomMaxFovPerSec = 25f;
                s.minFov = 2.1f;
                s.maxFov = 40.4f;
                s.panMinDeg = -170f;
                s.panMaxDeg = 170f;
                s.tiltMinDeg = -20f;
                s.tiltMaxDeg = 90f;
                s.speedGamma = 1.0f;
                s.enablePanTiltSpeedScaleByZoom = true;
                s.panTiltSpeedScaleAtTele = 0.5f;
                s.useSlowPanTilt = false;
                s.panSlowMaxDegPerSec = 60f;
                s.tiltSlowMaxDegPerSec = 60f;
                s.panSlowMinDegPerSec = 0.5f;
                s.tiltSlowMinDegPerSec = 0.5f;
                s.useLensProfile = true;
                s.sensorWidthMm = 5.76f;
                s.sensorHeightMm = 3.24f;
                s.focalLengthMinMm = 4.4f;
                s.focalLengthMaxMm = 88.0f;
                s.zoomPositionTeleAtMax = true;
            });

            CreateTuningPreset("PTZ_Tuning_BRC-X400.asset", t =>
            {
                t.enableAccelerationLimit = true;
                t.panAccelDegPerSec2 = 800f;
                t.tiltAccelDegPerSec2 = 600f;
                t.zoomAccelDegPerSec2 = 300f;
                t.panDecelDegPerSec2 = 900f;
                t.tiltDecelDegPerSec2 = 700f;
                t.zoomDecelDegPerSec2 = 350f;
                t.enableTargetBraking = true;
                t.panStopDistanceDeg = 0.2f;
                t.tiltStopDistanceDeg = 0.2f;
                t.zoomStopDistanceDeg = 0.15f;
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

        private static void CreateTuningPreset(string name, Action<PtzTuningProfile> configure)
        {
            var path = Path.Combine(PresetDir, name);
            PtzTuningProfile profile = null;
            if (File.Exists(path)) profile = AssetDatabase.LoadAssetAtPath<PtzTuningProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PtzTuningProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            configure(profile);
            EditorUtility.SetDirty(profile);
        }
    }
}
#endif
