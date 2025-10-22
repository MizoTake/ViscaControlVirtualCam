using System;
using UnityEngine;

namespace ViscaControlVirtualCam
{
    [DefaultExecutionOrder(0)]
    public class PtzController : MonoBehaviour
    {
        [Header("Rig Targets")]
        [Tooltip("Yaw (pan) pivot. If null, uses this transform.")]
        public Transform panPivot;
        [Tooltip("Pitch (tilt) pivot. If null, uses this transform.")]
        public Transform tiltPivot;
        [Tooltip("Target camera for FOV zoom.")]
        public Camera targetCamera;

        [Header("Motion Limits")] public float panMaxDegPerSec = 120f;
        public float tiltMaxDegPerSec = 90f;
        public float zoomMaxFovPerSec = 40f;
        public float minFov = 15f;
        public float maxFov = 90f;

        [Header("Absolute Position Ranges")] public float panMinDeg = -170f;
        public float panMaxDeg = 170f;
        public float tiltMinDeg = -30f;
        public float tiltMaxDeg = 90f;
        public float moveDamping = 6f; // smoothing for absolute moves

        [Header("Speed Mapping")] [Range(0.1f, 3f)] public float speedGamma = 1.0f;
        public byte panVmin = 0x01, panVmax = 0x18;
        public byte tiltVmin = 0x01, tiltVmax = 0x14;

        // Current commanded velocities (deg/s and fov deg/s)
        private float _omegaPan, _omegaTilt, _omegaFov;

        // Absolute targets (nullable)
        private float? _targetPanDeg;
        private float? _targetTiltDeg;

        private void Awake()
        {
            if (panPivot == null) panPivot = transform;
            if (tiltPivot == null) tiltPivot = transform;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Apply velocity drive for pan/tilt
            if (Mathf.Abs(_omegaPan) > 0.001f)
                panPivot.Rotate(0f, _omegaPan * dt, 0f, Space.Self);

            if (Mathf.Abs(_omegaTilt) > 0.001f)
                tiltPivot.Rotate(-_omegaTilt * dt, 0f, 0f, Space.Self);

            // Apply absolute targets with damping
            if (_targetPanDeg.HasValue)
            {
                float currentYaw = panPivot.localEulerAngles.y;
                currentYaw = NormalizeAngle(currentYaw);
                float newYaw = Mathf.Lerp(currentYaw, Mathf.Clamp(_targetPanDeg.Value, panMinDeg, panMaxDeg), 1 - Mathf.Exp(-moveDamping * dt));
                float delta = Mathf.DeltaAngle(currentYaw, newYaw);
                panPivot.Rotate(0f, delta, 0f, Space.Self);
                if (Mathf.Abs(Mathf.DeltaAngle(newYaw, Mathf.Clamp(_targetPanDeg.Value, panMinDeg, panMaxDeg))) < 0.1f)
                    _targetPanDeg = null;
            }

            if (_targetTiltDeg.HasValue)
            {
                float currentPitch = -NormalizeAngle(tiltPivot.localEulerAngles.x);
                float target = Mathf.Clamp(_targetTiltDeg.Value, tiltMinDeg, tiltMaxDeg);
                float newPitch = Mathf.Lerp(currentPitch, target, 1 - Mathf.Exp(-moveDamping * dt));
                float delta = newPitch - currentPitch;
                tiltPivot.Rotate(-delta, 0f, 0f, Space.Self);
                if (Mathf.Abs(newPitch - target) < 0.1f)
                    _targetTiltDeg = null;
            }

            // Apply zoom
            if (targetCamera != null && Mathf.Abs(_omegaFov) > 0.001f)
            {
                float fov = targetCamera.fieldOfView;
                fov = Mathf.Clamp(fov + _omegaFov * dt, minFov, maxFov);
                targetCamera.fieldOfView = fov;
            }
        }

        public void CommandPanTiltVariable(byte vv, byte ww, AxisDirection panDir, AxisDirection tiltDir)
        {
            float vPan = MapSpeed(vv, panVmin, panVmax, panMaxDegPerSec, speedGamma);
            float vTilt = MapSpeed(ww, tiltVmin, tiltVmax, tiltMaxDegPerSec, speedGamma);
            _omegaPan = (panDir == AxisDirection.Stop ? 0 : vPan * (panDir == AxisDirection.Positive ? 1 : -1));
            _omegaTilt = (tiltDir == AxisDirection.Stop ? 0 : vTilt * (tiltDir == AxisDirection.Positive ? -1 : 1)); // Up is positive in docs -> negative rotation around X
            // Cancel absolute targets for axes that are being driven
            if (panDir != AxisDirection.Stop) _targetPanDeg = null;
            if (tiltDir != AxisDirection.Stop) _targetTiltDeg = null;
        }

        public void CommandPanTiltStop()
        {
            _omegaPan = 0f;
            _omegaTilt = 0f;
        }

        public void CommandZoomVariable(byte zz)
        {
            if (zz == 0x00)
            {
                _omegaFov = 0f;
                return;
            }

            int dirNibble = (zz & 0xF0) >> 4; // 0x2p Tele, 0x3p Wide
            int p = (zz & 0x0F);
            p = Mathf.Clamp(p, 0, 7);
            float speed = Mathf.Pow(p / 7f, Mathf.Max(0.01f, speedGamma)) * zoomMaxFovPerSec;
            float sign = dirNibble == 0x2 ? -1f : +1f; // Tele reduces FOV
            _omegaFov = speed * sign;
        }

        public void CommandPanTiltAbsolute(byte vv, byte ww, ushort panPos, ushort tiltPos)
        {
            // Convert to degrees within configured ranges
            float panDeg = Mathf.Lerp(panMinDeg, panMaxDeg, panPos / 65535f);
            float tiltDeg = Mathf.Lerp(tiltMinDeg, tiltMaxDeg, tiltPos / 65535f);
            _targetPanDeg = panDeg;
            _targetTiltDeg = tiltDeg;
            // Optionally use vv/ww for damping changes if desired; currently not used
            _omegaPan = 0f;
            _omegaTilt = 0f;
        }

        private static float MapSpeed(byte v, byte vmin, byte vmax, float maxDegPerSec, float gamma)
        {
            if (v == 0x00) v = vmin; // treat 0 as minimum
            float t = Mathf.InverseLerp(vmin, vmax, Mathf.Clamp(v, vmin, vmax));
            float mapped = Mathf.Pow(t, Mathf.Max(0.01f, gamma));
            return mapped * maxDegPerSec;
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
