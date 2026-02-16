using UnityEngine;

namespace ViscaControlVirtualCam
{
    // MonoBehaviour adapter that owns a PtzModel and applies its step to a rig.
    public class PtzControllerBehaviour : MonoBehaviour
    {
        [Header("Rig Targets")] public Transform panPivot; // yaw
        public Transform tiltPivot; // pitch
        public Camera targetCamera; // FOV

        [Header("Settings Preset")] public PtzSettings settings;

        [Header("Tuning (Optional)")] public PtzTuningProfile tuningProfile;

        [Header("Memory Presets")] [Tooltip("Enable persistent memory presets using PlayerPrefs")]
        public bool enablePersistentMemory = true;

        [Tooltip("Prefix for PlayerPrefs keys (default: ViscaPtz_)")]
        public string prefsKeyPrefix = "ViscaPtz_";

        private IPlayerPrefsAdapter _playerPrefs;
        public PtzModel Model { get; private set; }

        private void Awake()
        {
            if (panPivot == null) panPivot = transform;
            if (tiltPivot == null) tiltPivot = transform;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();

            // Create PlayerPrefs adapter if persistent memory is enabled
            _playerPrefs = enablePersistentMemory ? new UnityPlayerPrefsAdapter() : null;

            Model = new PtzModel(_playerPrefs, prefsKeyPrefix);
            ApplySettings();

            // Capture initial transform/camera as home baseline
            var yaw = PtzMathUtils.NormalizeAngle(panPivot.localEulerAngles.y);
            var pitch = -PtzMathUtils.NormalizeAngle(tiltPivot.localEulerAngles.x);
            var fov = targetCamera != null ? targetCamera.fieldOfView : 60f;
            Model.SetHomeBaseline(yaw, pitch, fov, Model.CurrentFocus, Model.CurrentIris);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var currentYaw = PtzMathUtils.NormalizeAngle(panPivot.localEulerAngles.y);
            var currentPitch = -PtzMathUtils.NormalizeAngle(tiltPivot.localEulerAngles.x); // define +up
            var currentFov = targetCamera != null ? targetCamera.fieldOfView : 60f;

            var step = Model.Step(currentYaw, currentPitch, currentFov, dt);

            if (Mathf.Abs(step.DeltaYawDeg) > 0.0001f)
                panPivot.Rotate(0f, step.DeltaYawDeg, 0f, Space.Self);
            if (Mathf.Abs(step.DeltaPitchDeg) > 0.0001f)
                tiltPivot.Rotate(-step.DeltaPitchDeg, 0f, 0f, Space.Self);
            if (step.HasNewFov && targetCamera != null)
                targetCamera.fieldOfView = step.NewFovDeg;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                // In editor, create model without PlayerPrefs
                if (Model == null) Model = new PtzModel(null, prefsKeyPrefix);
                ApplySettings();

                // Keep home baseline in sync in Editor
                var yaw = PtzMathUtils.NormalizeAngle(panPivot != null ? panPivot.localEulerAngles.y : 0f);
                var pitch = -PtzMathUtils.NormalizeAngle(tiltPivot != null ? tiltPivot.localEulerAngles.x : 0f);
                var fov = targetCamera != null ? targetCamera.fieldOfView : 60f;
                Model.SetHomeBaseline(yaw, pitch, fov, Model.CurrentFocus, Model.CurrentIris);
            }
        }
#endif

        [ContextMenu("Apply Settings Now")]
        public void ApplySettings()
        {
            if (settings != null) settings.ApplyTo(Model);

            if (tuningProfile != null) tuningProfile.ApplyTo(Model);
        }
    }
}