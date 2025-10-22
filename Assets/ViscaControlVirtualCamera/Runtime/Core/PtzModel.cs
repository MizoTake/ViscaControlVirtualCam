using System;

namespace ViscaControlVirtualCam
{
    public struct PtzStepResult
    {
        public float DeltaYawDeg;   // +right
        public float DeltaPitchDeg; // +up
        public float NewFovDeg;     // absolute value
        public bool HasNewFov;
    }

    // Pure C# PTZ core: holds state and computes step deltas from commands.
    public class PtzModel
    {
        // Limits and mapping
        public float PanMaxDegPerSec = 120f;
        public float TiltMaxDegPerSec = 90f;
        public float ZoomMaxFovPerSec = 40f;
        public float MinFov = 15f;
        public float MaxFov = 90f;

        public float PanMinDeg = -170f;
        public float PanMaxDeg = 170f;
        public float TiltMinDeg = -30f;
        public float TiltMaxDeg = 90f;
        public float MoveDamping = 6f; // for absolute moves

        public float SpeedGamma = 1.0f;
        public byte PanVmin = 0x01, PanVmax = 0x18;
        public byte TiltVmin = 0x01, TiltVmax = 0x14;

        private float _omegaPan;  // +right, deg/s
        private float _omegaTilt; // +up, deg/s
        private float _omegaFov;  // +increase FOV, deg/s

        private float? _targetPanDeg;  // absolute target yaw
        private float? _targetTiltDeg; // absolute target pitch

        public void CommandPanTiltVariable(byte vv, byte ww, AxisDirection panDir, AxisDirection tiltDir)
        {
            float vPan = MapSpeed(vv, PanVmin, PanVmax, PanMaxDegPerSec, SpeedGamma);
            float vTilt = MapSpeed(ww, TiltVmin, TiltVmax, TiltMaxDegPerSec, SpeedGamma);
            _omegaPan = panDir == AxisDirection.Stop ? 0f : vPan * (panDir == AxisDirection.Positive ? 1f : -1f);
            _omegaTilt = tiltDir == AxisDirection.Stop ? 0f : vTilt * (tiltDir == AxisDirection.Positive ? 1f : -1f);
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
            p = Clamp(p, 0, 7);
            float speed = (float)Math.Pow(p / 7f, Math.Max(0.01f, SpeedGamma)) * ZoomMaxFovPerSec;
            float sign = dirNibble == 0x2 ? -1f : +1f; // Tele reduces FOV
            _omegaFov = speed * sign;
        }

        public void CommandPanTiltAbsolute(byte vv, byte ww, ushort panPos, ushort tiltPos)
        {
            float panDeg = Lerp(PanMinDeg, PanMaxDeg, panPos / 65535f);
            float tiltDeg = Lerp(TiltMinDeg, TiltMaxDeg, tiltPos / 65535f);
            _targetPanDeg = panDeg;
            _targetTiltDeg = tiltDeg;
            _omegaPan = 0f;
            _omegaTilt = 0f;
        }

        public PtzStepResult Step(float currentYawDeg, float currentPitchDeg, float currentFovDeg, float dt)
        {
            var result = new PtzStepResult();

            // Velocity drive
            result.DeltaYawDeg += _omegaPan * dt;
            result.DeltaPitchDeg += _omegaTilt * dt;

            // Absolute with damping
            if (_targetPanDeg.HasValue)
            {
                float targetYaw = Clamp(_targetPanDeg.Value, PanMinDeg, PanMaxDeg);
                float newYaw = Damp(currentYawDeg, targetYaw, MoveDamping, dt);
                float delta = DeltaAngle(currentYawDeg, newYaw);
                result.DeltaYawDeg += delta;
                if (Math.Abs(DeltaAngle(newYaw, targetYaw)) < 0.1f) _targetPanDeg = null;
            }

            if (_targetTiltDeg.HasValue)
            {
                float targetPitch = Clamp(_targetTiltDeg.Value, TiltMinDeg, TiltMaxDeg);
                float newPitch = Damp(currentPitchDeg, targetPitch, MoveDamping, dt);
                float delta = newPitch - currentPitchDeg;
                result.DeltaPitchDeg += delta;
                if (Math.Abs(newPitch - targetPitch) < 0.1f) _targetTiltDeg = null;
            }

            // Zoom
            float newFov = Clamp(currentFovDeg + _omegaFov * dt, MinFov, MaxFov);
            if (Math.Abs(newFov - currentFovDeg) > 1e-4f)
            {
                result.NewFovDeg = newFov;
                result.HasNewFov = true;
            }

            return result;
        }

        private static float MapSpeed(byte v, byte vmin, byte vmax, float maxDegPerSec, float gamma)
        {
            if (v == 0x00) v = vmin;
            float t = InverseLerp(vmin, vmax, Clamp(v, vmin, vmax));
            float mapped = (float)Math.Pow(t, Math.Max(0.01f, gamma));
            return mapped * maxDegPerSec;
        }

        private static float Damp(float current, float target, float damping, float dt)
        {
            float k = 1f - (float)Math.Exp(-damping * dt);
            return current + (target - current) * k;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float InverseLerp(float a, float b, float v) => (v - a) / (b - a);
        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        private static float DeltaAngle(float a, float b)
        {
            float diff = (b - a) % 360f;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;
            return diff;
        }
    }
}
