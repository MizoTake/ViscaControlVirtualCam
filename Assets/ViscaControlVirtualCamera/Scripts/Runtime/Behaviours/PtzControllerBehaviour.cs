using UnityEngine;

namespace ViscaControlVirtualCam
{
    // MonoBehaviour adapter that owns a PtzModel and applies its step to a rig.
    public class PtzControllerBehaviour : MonoBehaviour
    {
        [Header("Rig Targets")] public Transform panPivot; // yaw
        public Transform tiltPivot; // pitch
        public Camera targetCamera; // FOV

        [Header("Motion Limits")] public float panMaxDegPerSec = 120f;
        public float tiltMaxDegPerSec = 90f;
        public float zoomMaxFovPerSec = 40f;
        public float minFov = 15f;
        public float maxFov = 90f;

        [Header("Absolute Ranges")] public float panMinDeg = -170f;
        public float panMaxDeg = 170f;
        public float tiltMinDeg = -30f;
        public float tiltMaxDeg = 90f;
        public float moveDamping = 6f;

        [Header("Speed Mapping")] [Range(0.1f, 3f)] public float speedGamma = 1.0f;
        public byte panVmin = 0x01, panVmax = 0x18;
        public byte tiltVmin = 0x01, tiltVmax = 0x14;

        private PtzModel _model;
        public PtzModel Model => _model;

        private void Awake()
        {
            if (panPivot == null) panPivot = transform;
            if (tiltPivot == null) tiltPivot = transform;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();

            _model = new PtzModel
            {
                PanMaxDegPerSec = panMaxDegPerSec,
                TiltMaxDegPerSec = tiltMaxDegPerSec,
                ZoomMaxFovPerSec = zoomMaxFovPerSec,
                MinFov = minFov,
                MaxFov = maxFov,
                PanMinDeg = panMinDeg,
                PanMaxDeg = panMaxDeg,
                TiltMinDeg = tiltMinDeg,
                TiltMaxDeg = tiltMaxDeg,
                MoveDamping = moveDamping,
                SpeedGamma = speedGamma,
                PanVmin = panVmin,
                PanVmax = panVmax,
                TiltVmin = tiltVmin,
                TiltVmax = tiltVmax
            };
        }

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

        private static float NormalizeAngle(float euler)
        {
            float a = euler;
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }
    }
}

