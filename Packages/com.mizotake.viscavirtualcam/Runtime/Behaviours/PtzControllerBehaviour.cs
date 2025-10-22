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

        private PtzModel _model;
        public PtzModel Model => _model;

        private void Awake()
        {
            if (panPivot == null) panPivot = transform;
            if (tiltPivot == null) tiltPivot = transform;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();

            _model = new PtzModel();
            ApplySettings();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (_model == null) _model = new PtzModel();
                ApplySettings();
            }
        }
#endif

        private void Update()
        {
            float dt = Time.deltaTime;
            float currentYaw = NormalizeAngle(panPivot.localEulerAngles.y);
            float currentPitch = -NormalizeAngle(tiltPivot.localEulerAngles.x); // define +up
            float currentFov = targetCamera != null ? targetCamera.fieldOfView : 60f;

            var step = _model.Step(currentYaw, currentPitch, currentFov, dt);

            if (Mathf.Abs(step.DeltaYawDeg) > 0.0001f)
                panPivot.Rotate(0f, step.DeltaYawDeg, 0f, Space.Self);
            if (Mathf.Abs(step.DeltaPitchDeg) > 0.0001f)
                tiltPivot.Rotate(-step.DeltaPitchDeg, 0f, 0f, Space.Self);
            if (step.HasNewFov && targetCamera != null)
                targetCamera.fieldOfView = step.NewFovDeg;
        }

        [ContextMenu("Apply Settings Now")]
        public void ApplySettings()
        {
            if (settings != null)
            {
                settings.ApplyTo(_model);
            }
        }

        private static float NormalizeAngle(float euler)
        {
            float a = euler;
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }
    }
}
