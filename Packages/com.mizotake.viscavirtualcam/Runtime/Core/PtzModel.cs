using System;
using System.Collections.Generic;

namespace ViscaControlVirtualCam
{
    public struct PtzStepResult
    {
        public float DeltaYawDeg; // +right
        public float DeltaPitchDeg; // +up
        public float NewFovDeg; // absolute value
        public bool HasNewFov;
    }

    public struct PtzMemoryPreset
    {
        public float PanDeg;
        public float TiltDeg;
        public float FovDeg;
        public float FocusPos;
        public float IrisPos;
    }

    /// <summary>
    ///     Pure C# PTZ core: holds state and computes step deltas from commands.
    ///     Delegates memory management to PtzMemoryManager and math operations to PtzMathUtils.
    /// </summary>
    public class PtzModel
    {
        private readonly PtzMemoryManager _memoryManager;

        // Home (initial) baseline
        private bool _hasHome;
        private float _homeFocus;
        private float _homeFovDeg;
        private float _homeIris;
        private float _homePanDeg;
        private float _homeTiltDeg;

        // Velocity state
        private float _omegaFocus; // +far, units/s
        private float _omegaFov; // +increase FOV, deg/s
        private float _omegaFovCurrent;
        private float _omegaIris; // +open, units/s
        private float _omegaPan; // +right, deg/s
        private float _omegaPanCurrent;
        private float _omegaTilt; // +up, deg/s
        private float _omegaTiltCurrent;

        // Absolute targets
        private float? _targetFocus;
        private float? _targetFov;
        private float? _targetIris;
        private float? _targetPanDeg;
        private float? _targetTiltDeg;
        private float _targetPanSpeedLimit;
        private float _targetTiltSpeedLimit;

        #region Configuration Properties

        // Focus/Iris limits
        public float FocusMax = 65535f;
        public float FocusMaxSpeed = 100f;
        public float FocusMin = 0f;

        // Inversion settings
        public bool InvertPan;
        public bool InvertPanAbsolute;
        public bool InvertTilt;
        public bool InvertTiltAbsolute;

        // Iris limits
        public float IrisMax = 65535f;
        public float IrisMaxSpeed = 50f;
        public float IrisMin = 0f;

        // FOV limits
        public float MaxFov = 90f;
        public float MinFov = 15f;

        // Motion parameters
        public float MoveDamping = 6f;
        public float PanAccelDegPerSec2 = 600f;
        public float PanDecelDegPerSec2 = 600f;
        public float PanMaxDeg = 170f;
        public float PanMaxDegPerSec = 120f;
        public float PanMinDeg = -170f;
        public byte PanVmin = 0x01, PanVmax = 0x18;
        public float SpeedGamma = 1.0f;
        public float TiltAccelDegPerSec2 = 600f;
        public float TiltDecelDegPerSec2 = 600f;
        public float TiltMaxDeg = 90f;
        public float TiltMaxDegPerSec = 90f;
        public float TiltMinDeg = -30f;
        public byte TiltVmin = 0x01, TiltVmax = 0x14;
        public bool UseAccelerationLimit;
        public bool UseTargetBraking;
        public float ZoomAccelDegPerSec2 = 300f;
        public float ZoomDecelDegPerSec2 = 300f;
        public float ZoomMaxFovPerSec = 40f;
        public float PanStopDistanceDeg = 0.1f;
        public float TiltStopDistanceDeg = 0.1f;
        public float ZoomStopDistanceDeg = 0.1f;
        public bool EnablePanTiltSpeedScaleByZoom;
        public float PanTiltSpeedScaleAtTele = 0.6f;

        #endregion

        /// <summary>
        ///     Constructor with optional PlayerPrefs adapter for persistence.
        /// </summary>
        /// <param name="playerPrefs">PlayerPrefs adapter (null = no persistence)</param>
        /// <param name="prefsKeyPrefix">Prefix for PlayerPrefs keys (default: "ViscaPtz_")</param>
        public PtzModel(IPlayerPrefsAdapter playerPrefs = null, string prefsKeyPrefix = "ViscaPtz_")
        {
            _memoryManager = new PtzMemoryManager(playerPrefs, prefsKeyPrefix);
        }

        #region Current State Properties

        public float CurrentPanDeg { get; private set; }
        public float CurrentTiltDeg { get; private set; }
        public float CurrentFovDeg { get; private set; }
        public float CurrentFocus { get; private set; }
        public float CurrentIris { get; private set; }

        #endregion

        #region Pan/Tilt Commands

        public void CommandPanTiltVariable(byte vv, byte ww, AxisDirection panDir, AxisDirection tiltDir)
        {
            var vPan = PtzMathUtils.MapSpeed(vv, PanVmin, PanVmax, PanMaxDegPerSec, SpeedGamma);
            var vTilt = PtzMathUtils.MapSpeed(ww, TiltVmin, TiltVmax, TiltMaxDegPerSec, SpeedGamma);
            var panSign = panDir == AxisDirection.Positive ? 1f : -1f;
            var tiltSign = tiltDir == AxisDirection.Positive ? 1f : -1f;
            if (InvertPan) panSign *= -1f;
            if (InvertTilt) tiltSign *= -1f;

            _omegaPan = panDir == AxisDirection.Stop ? 0f : vPan * panSign;
            _omegaTilt = tiltDir == AxisDirection.Stop ? 0f : vTilt * tiltSign;
            if (panDir != AxisDirection.Stop) _targetPanDeg = null;
            if (tiltDir != AxisDirection.Stop) _targetTiltDeg = null;
        }

        public void CommandPanTiltStop()
        {
            _omegaPan = 0f;
            _omegaTilt = 0f;
        }

        public void CommandPanTiltAbsolute(byte vv, byte ww, ushort panPos, ushort tiltPos)
        {
            var panNorm = panPos / 65535f;
            var tiltNorm = tiltPos / 65535f;
            if (InvertPanAbsolute) panNorm = 1f - panNorm;
            if (InvertTiltAbsolute) tiltNorm = 1f - tiltNorm;

            var panDeg = PtzMathUtils.Lerp(PanMinDeg, PanMaxDeg, panNorm);
            var tiltDeg = PtzMathUtils.Lerp(TiltMinDeg, TiltMaxDeg, tiltNorm);
            _targetPanDeg = panDeg;
            _targetTiltDeg = tiltDeg;
            _omegaPan = 0f;
            _omegaTilt = 0f;
            _targetPanSpeedLimit = PtzMathUtils.MapSpeed(vv, PanVmin, PanVmax, PanMaxDegPerSec, SpeedGamma);
            _targetTiltSpeedLimit = PtzMathUtils.MapSpeed(ww, TiltVmin, TiltVmax, TiltMaxDegPerSec, SpeedGamma);
        }

        #endregion

        #region Zoom Commands

        public void CommandZoomVariable(byte zz)
        {
            if (zz == 0x00)
            {
                _omegaFov = 0f;
                return;
            }

            var dirNibble = (zz & 0xF0) >> 4;
            var p = PtzMathUtils.Clamp(zz & 0x0F, 0, 7);
            var speed = (float)Math.Pow(p / 7f, Math.Max(0.01f, SpeedGamma)) * ZoomMaxFovPerSec;
            var sign = dirNibble == 0x2 ? -1f : +1f;
            _omegaFov = speed * sign;
        }

        public void CommandZoomDirect(ushort zoomPos)
        {
            var fov = PtzMathUtils.Lerp(MinFov, MaxFov, zoomPos / 65535f);
            _targetFov = fov;
            _omegaFov = 0f;
        }

        #endregion

        #region Focus Commands

        public void CommandFocusVariable(byte focusSpeed)
        {
            if (focusSpeed == 0x00)
            {
                _omegaFocus = 0f;
                return;
            }

            var sign = focusSpeed == 0x02 ? 1f : -1f;
            _omegaFocus = FocusMaxSpeed * sign;
        }

        public void CommandFocusDirect(ushort focusPos)
        {
            _targetFocus = focusPos;
            _omegaFocus = 0f;
        }

        #endregion

        #region Iris Commands

        public void CommandIrisVariable(byte irisDir)
        {
            if (irisDir == 0x00)
            {
                _omegaIris = 0f;
                return;
            }

            var sign = irisDir == 0x02 ? 1f : -1f;
            _omegaIris = IrisMaxSpeed * sign;
        }

        public void CommandIrisDirect(ushort irisPos)
        {
            _targetIris = irisPos;
            _omegaIris = 0f;
        }

        #endregion

        #region Home Command

        public void SetHomeBaseline(float panDeg, float tiltDeg, float fovDeg, float focus, float iris)
        {
            _homePanDeg = panDeg;
            _homeTiltDeg = tiltDeg;
            _homeFovDeg = fovDeg;
            _homeFocus = focus;
            _homeIris = iris;
            _hasHome = true;
        }

        public void CommandHome()
        {
            if (!_hasHome)
            {
                _homePanDeg = 0f;
                _homeTiltDeg = 0f;
                _homeFovDeg = PtzMathUtils.Clamp(CurrentFovDeg == 0 ? 60f : CurrentFovDeg, MinFov, MaxFov);
                _homeFocus = CurrentFocus;
                _homeIris = CurrentIris;
                _hasHome = true;
            }

            _targetPanDeg = _homePanDeg;
            _targetTiltDeg = _homeTiltDeg;
            _targetFov = _homeFovDeg;
            _targetFocus = _homeFocus;
            _targetIris = _homeIris;

            _omegaPan = 0f;
            _omegaTilt = 0f;
            _omegaFov = 0f;
            _omegaFocus = 0f;
            _omegaIris = 0f;
            _targetPanSpeedLimit = PanMaxDegPerSec;
            _targetTiltSpeedLimit = TiltMaxDegPerSec;
        }

        #endregion

        #region Memory Commands

        public void CommandMemoryRecall(byte memoryNumber)
        {
            if (_memoryManager.TryGetPreset(memoryNumber, out var preset))
            {
                _targetPanDeg = preset.PanDeg;
                _targetTiltDeg = preset.TiltDeg;
                _targetFov = preset.FovDeg;
                _targetFocus = preset.FocusPos;
                _targetIris = preset.IrisPos;
                _omegaPan = 0f;
                _omegaTilt = 0f;
                _omegaFov = 0f;
                _omegaFocus = 0f;
                _omegaIris = 0f;
                _targetPanSpeedLimit = PanMaxDegPerSec;
                _targetTiltSpeedLimit = TiltMaxDegPerSec;
            }
        }

        public void CommandMemorySet(byte memoryNumber)
        {
            _memoryManager.SavePreset(memoryNumber, CurrentPanDeg, CurrentTiltDeg, CurrentFovDeg, CurrentFocus,
                CurrentIris);
        }

        public void DeletePreset(byte memoryNumber)
        {
            _memoryManager.DeletePreset(memoryNumber);
        }

        public IEnumerable<byte> GetSavedPresets()
        {
            return _memoryManager.GetSavedPresets();
        }

        #endregion

        #region Step Update

        public PtzStepResult Step(float currentYawDeg, float currentPitchDeg, float currentFovDeg, float dt)
        {
            CurrentPanDeg = currentYawDeg;
            CurrentTiltDeg = currentPitchDeg;
            CurrentFovDeg = currentFovDeg;

            var result = new PtzStepResult();

            // Calculate velocities with optional acceleration limiting
            var desiredPanVel = _omegaPan;
            var desiredTiltVel = _omegaTilt;
            var desiredFovVel = _omegaFov;
            var panSnapped = false;
            var tiltSnapped = false;
            var fovSnapped = false;
            var zoomScale = GetPanTiltZoomScale(currentFovDeg);

            if (UseTargetBraking && _targetPanDeg.HasValue)
            {
                var targetYaw = PtzMathUtils.Clamp(_targetPanDeg.Value, PanMinDeg, PanMaxDeg);
                var distance = PtzMathUtils.DeltaAngle(currentYawDeg, targetYaw);
                if (Math.Abs(distance) <= PanStopDistanceDeg)
                {
                    result.DeltaYawDeg += distance;
                    _targetPanDeg = null;
                    _omegaPanCurrent = 0f;
                    desiredPanVel = 0f;
                    panSnapped = true;
                }
                else
                {
                    var speedLimit = _targetPanSpeedLimit > 0f ? _targetPanSpeedLimit : PanMaxDegPerSec;
                    desiredPanVel = ComputeBrakingVelocity(distance, PanDecelDegPerSec2, PanStopDistanceDeg,
                        speedLimit * zoomScale);
                }
            }

            if (UseTargetBraking && _targetTiltDeg.HasValue)
            {
                var targetPitch = PtzMathUtils.Clamp(_targetTiltDeg.Value, TiltMinDeg, TiltMaxDeg);
                var distance = targetPitch - currentPitchDeg;
                if (Math.Abs(distance) <= TiltStopDistanceDeg)
                {
                    result.DeltaPitchDeg += distance;
                    _targetTiltDeg = null;
                    _omegaTiltCurrent = 0f;
                    desiredTiltVel = 0f;
                    tiltSnapped = true;
                }
                else
                {
                    var speedLimit = _targetTiltSpeedLimit > 0f ? _targetTiltSpeedLimit : TiltMaxDegPerSec;
                    desiredTiltVel = ComputeBrakingVelocity(distance, TiltDecelDegPerSec2, TiltStopDistanceDeg,
                        speedLimit * zoomScale);
                }
            }

            if (UseTargetBraking && _targetFov.HasValue)
            {
                var targetFov = PtzMathUtils.Clamp(_targetFov.Value, MinFov, MaxFov);
                var distance = targetFov - currentFovDeg;
                if (Math.Abs(distance) <= ZoomStopDistanceDeg)
                {
                    result.NewFovDeg = targetFov;
                    result.HasNewFov = true;
                    _targetFov = null;
                    _omegaFovCurrent = 0f;
                    desiredFovVel = 0f;
                    fovSnapped = true;
                }
                else
                {
                    desiredFovVel = ComputeBrakingVelocity(distance, ZoomDecelDegPerSec2, ZoomStopDistanceDeg,
                        ZoomMaxFovPerSec);
                }
            }

            if (!UseTargetBraking || !_targetPanDeg.HasValue) desiredPanVel *= zoomScale;
            if (!UseTargetBraking || !_targetTiltDeg.HasValue) desiredTiltVel *= zoomScale;

            var panVel = desiredPanVel;
            var tiltVel = desiredTiltVel;
            var fovVel = desiredFovVel;

            if (UseAccelerationLimit)
            {
                panVel = ApplyAccelLimit(_omegaPanCurrent, desiredPanVel, PanAccelDegPerSec2, PanDecelDegPerSec2, dt);
                tiltVel = ApplyAccelLimit(_omegaTiltCurrent, desiredTiltVel, TiltAccelDegPerSec2, TiltDecelDegPerSec2,
                    dt);
                fovVel = ApplyAccelLimit(_omegaFovCurrent, desiredFovVel, ZoomAccelDegPerSec2, ZoomDecelDegPerSec2, dt);

                _omegaPanCurrent = panVel;
                _omegaTiltCurrent = tiltVel;
                _omegaFovCurrent = fovVel;
            }

            // Apply velocity
            if (!panSnapped) result.DeltaYawDeg += panVel * dt;
            if (!tiltSnapped) result.DeltaPitchDeg += tiltVel * dt;

            // Apply absolute positioning with damping
            if (!UseTargetBraking && _targetPanDeg.HasValue)
            {
                var targetYaw = PtzMathUtils.Clamp(_targetPanDeg.Value, PanMinDeg, PanMaxDeg);
                var newYaw = PtzMathUtils.Damp(currentYawDeg, targetYaw, MoveDamping, dt);
                var delta = PtzMathUtils.DeltaAngle(currentYawDeg, newYaw);
                result.DeltaYawDeg += delta;
                if (Math.Abs(PtzMathUtils.DeltaAngle(newYaw, targetYaw)) < 0.1f) _targetPanDeg = null;
            }

            if (!UseTargetBraking && _targetTiltDeg.HasValue)
            {
                var targetPitch = PtzMathUtils.Clamp(_targetTiltDeg.Value, TiltMinDeg, TiltMaxDeg);
                var newPitch = PtzMathUtils.Damp(currentPitchDeg, targetPitch, MoveDamping, dt);
                var delta = newPitch - currentPitchDeg;
                result.DeltaPitchDeg += delta;
                if (Math.Abs(newPitch - targetPitch) < 0.1f) _targetTiltDeg = null;
            }

            // Zoom/FOV
            var newFov = currentFovDeg;
            if (!fovSnapped) newFov = currentFovDeg + fovVel * dt;
            if (!UseTargetBraking && _targetFov.HasValue)
            {
                var targetFov = PtzMathUtils.Clamp(_targetFov.Value, MinFov, MaxFov);
                newFov = PtzMathUtils.Damp(currentFovDeg, targetFov, MoveDamping, dt);
                if (Math.Abs(newFov - targetFov) < 0.1f) _targetFov = null;
            }

            newFov = PtzMathUtils.Clamp(newFov, MinFov, MaxFov);
            if (Math.Abs(newFov - currentFovDeg) > 1e-4f)
            {
                result.NewFovDeg = newFov;
                result.HasNewFov = true;
            }

            // Focus
            CurrentFocus += _omegaFocus * dt;
            if (_targetFocus.HasValue)
            {
                var targetFocus = PtzMathUtils.Clamp(_targetFocus.Value, FocusMin, FocusMax);
                CurrentFocus = PtzMathUtils.Damp(CurrentFocus, targetFocus, MoveDamping, dt);
                if (Math.Abs(CurrentFocus - targetFocus) < 1f) _targetFocus = null;
            }

            CurrentFocus = PtzMathUtils.Clamp(CurrentFocus, FocusMin, FocusMax);

            // Iris
            CurrentIris += _omegaIris * dt;
            if (_targetIris.HasValue)
            {
                var targetIris = PtzMathUtils.Clamp(_targetIris.Value, IrisMin, IrisMax);
                CurrentIris = PtzMathUtils.Damp(CurrentIris, targetIris, MoveDamping, dt);
                if (Math.Abs(CurrentIris - targetIris) < 1f) _targetIris = null;
            }

            CurrentIris = PtzMathUtils.Clamp(CurrentIris, IrisMin, IrisMax);

            return result;
        }

        #endregion

        private float GetPanTiltZoomScale(float currentFovDeg)
        {
            if (!EnablePanTiltSpeedScaleByZoom) return 1f;
            var range = MaxFov - MinFov;
            if (range <= ViscaProtocol.DivisionEpsilon) return 1f;
            var zoomT = PtzMathUtils.Clamp((MaxFov - currentFovDeg) / range, 0f, 1f);
            return PtzMathUtils.Clamp(PtzMathUtils.Lerp(1f, PanTiltSpeedScaleAtTele, zoomT), 0f, 2f);
        }

        private static float ApplyAccelLimit(float current, float target, float accel, float decel, float dt)
        {
            var sameDirection = Math.Sign(current) == Math.Sign(target) || Math.Abs(current) < 1e-5f;
            var increasingMagnitude = Math.Abs(target) > Math.Abs(current);
            var useAccel = sameDirection && increasingMagnitude;
            var maxDelta = (useAccel ? accel : decel) * dt;
            if (maxDelta < 0f) maxDelta = 0f;
            return PtzMathUtils.MoveTowards(current, target, maxDelta);
        }

        private static float ComputeBrakingVelocity(float distance, float decel, float stopDistance, float speedLimit)
        {
            if (Math.Abs(distance) <= stopDistance) return 0f;
            if (decel <= 0f) return Math.Sign(distance) * Math.Abs(speedLimit);
            var v = (float)Math.Sqrt(2f * decel * Math.Max(0f, Math.Abs(distance) - stopDistance));
            v = Math.Min(v, Math.Abs(speedLimit));
            return Math.Sign(distance) * v;
        }
    }
}
