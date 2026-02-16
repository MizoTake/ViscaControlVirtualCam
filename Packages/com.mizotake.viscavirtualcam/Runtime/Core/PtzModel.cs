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
        private float _omegaZoom; // +tele, normalized/s
        private float _omegaZoomCurrent;
        private float _omegaIris; // +open, units/s
        private float _omegaPan; // +right, deg/s
        private float _omegaPanCurrent;
        private float _omegaTilt; // +up, deg/s
        private float _omegaTiltCurrent;
        private float _panVelSmoothed;
        private float _tiltVelSmoothed;
        private float _zoomVelSmoothed;

        // Absolute targets
        private float? _targetFocus;
        private float? _targetFov;
        private float? _targetIris;
        private float? _targetPanDeg;
        private float? _targetTiltDeg;
        private float _targetPanSpeedLimit;
        private float _targetPanSpeedFloor;
        private float _targetTiltSpeedLimit;
        private float _targetTiltSpeedFloor;

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
        public float PanMinDegPerSec = 0f;
        public float MoveDamping = 6f;
        public float PanAccelDegPerSec2 = 600f;
        public float PanDecelDegPerSec2 = 600f;
        public float PanMaxDeg = 170f;
        public float PanMaxDegPerSec = 120f;
        public float PanPresetMaxDegPerSec = 0f;
        public float PanPresetMinDegPerSec = 0f;
        public float PanSlowMaxDegPerSec = 60f;
        public float PanSlowMinDegPerSec = 0f;
        public float PanMinDeg = -170f;
        public byte PanVmin = 0x01, PanVmax = 0x18;
        public float SpeedGamma = 1.0f;
        public float TiltAccelDegPerSec2 = 600f;
        public float TiltDecelDegPerSec2 = 600f;
        public float TiltMaxDeg = 90f;
        public float TiltMaxDegPerSec = 90f;
        public float TiltMinDegPerSec = 0f;
        public float TiltPresetMaxDegPerSec = 0f;
        public float TiltPresetMinDegPerSec = 0f;
        public float TiltSlowMaxDegPerSec = 60f;
        public float TiltSlowMinDegPerSec = 0f;
        public float TiltMinDeg = -30f;
        public byte TiltVmin = 0x01, TiltVmax = 0x14;
        public bool UseSlowPanTilt;
        public bool UseAccelerationLimit;
        public bool UseTargetBraking;
        public float ZoomAccelDegPerSec2 = 300f;
        public float ZoomDecelDegPerSec2 = 300f;
        public float ZoomMaxFovPerSec = 40f;
        public bool UseZoomPositionSpeed;
        public float ZoomMaxNormalizedPerSec = 0f;
        public float PanStopDistanceDeg = 0.1f;
        public float TiltStopDistanceDeg = 0.1f;
        public float ZoomStopDistanceDeg = 0.1f;
        public bool EnablePanTiltSpeedScaleByZoom;
        public float PanTiltSpeedScaleAtTele = 0.6f;
        public bool UseVelocitySmoothing;
        public float VelocitySmoothingTime = 0.1f;

        // Lens profile (optional)
        public bool UseLensProfile;
        public float SensorWidthMm = 0f;
        public float SensorHeightMm = 0f;
        public float FocalLengthMinMm = 0f;
        public float FocalLengthMaxMm = 0f;
        public bool ZoomPositionTeleAtMax = true;

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
            GetPanSpeedRange(out var panMin, out var panMax);
            GetTiltSpeedRange(out var tiltMin, out var tiltMax);
            var vPan = PtzMathUtils.MapSpeed(vv, PanVmin, PanVmax, panMin, panMax, SpeedGamma);
            var vTilt = PtzMathUtils.MapSpeed(ww, TiltVmin, TiltVmax, tiltMin, tiltMax, SpeedGamma);
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
            GetPanSpeedRange(out var panMin, out var panMax);
            GetTiltSpeedRange(out var tiltMin, out var tiltMax);
            _targetPanSpeedLimit = vv == 0
                ? -1f
                : PtzMathUtils.MapSpeed(vv, PanVmin, PanVmax, panMin, panMax, SpeedGamma);
            _targetTiltSpeedLimit = ww == 0
                ? -1f
                : PtzMathUtils.MapSpeed(ww, TiltVmin, TiltVmax, tiltMin, tiltMax, SpeedGamma);
            _targetPanSpeedFloor = 0f;
            _targetTiltSpeedFloor = 0f;
        }

        #endregion

        #region Zoom Commands

        public void CommandZoomVariable(byte zz)
        {
            if (zz == 0x00)
            {
                _omegaFov = 0f;
                _omegaZoom = 0f;
                return;
            }

            var dirNibble = (zz & 0xF0) >> 4;
            var p = PtzMathUtils.Clamp(zz & 0x0F, 0, 7);
            var speedFactor = (float)Math.Pow(p / 7f, Math.Max(0.01f, SpeedGamma));
            var isTele = dirNibble == ViscaProtocol.ZoomTeleNibble;
            if (UseZoomPositionSpeed)
            {
                var speed = speedFactor * GetZoomMaxNormalizedPerSec();
                var sign = isTele ? 1f : -1f;
                _omegaZoom = speed * sign;
                _omegaFov = 0f;
            }
            else
            {
                var speed = speedFactor * ZoomMaxFovPerSec;
                var sign = isTele ? -1f : +1f;
                _omegaFov = speed * sign;
                _omegaZoom = 0f;
            }
        }

        public void CommandZoomDirect(ushort zoomPos)
        {
            var fov = GetFovFromZoomPosition(zoomPos);
            _targetFov = fov;
            _omegaFov = 0f;
            _omegaZoom = 0f;
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
            _omegaZoom = 0f;
            _omegaFocus = 0f;
            _omegaIris = 0f;
            _targetPanSpeedLimit = GetPresetPanMaxSpeed();
            _targetTiltSpeedLimit = GetPresetTiltMaxSpeed();
            _targetPanSpeedFloor = GetPresetPanMinSpeed();
            _targetTiltSpeedFloor = GetPresetTiltMinSpeed();
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
                _omegaZoom = 0f;
                _omegaFocus = 0f;
                _omegaIris = 0f;
                _targetPanSpeedLimit = GetPresetPanMaxSpeed();
                _targetTiltSpeedLimit = GetPresetTiltMaxSpeed();
                _targetPanSpeedFloor = GetPresetPanMinSpeed();
                _targetTiltSpeedFloor = GetPresetTiltMinSpeed();
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
            GetFovLimits(out var fovMin, out var fovMax);

            var desiredPanVel = _omegaPan;
            var desiredTiltVel = _omegaTilt;
            var desiredFovVel = _omegaFov;
            var desiredZoomVel = _omegaZoom;
            var useZoomPositionSpeed = UseZoomPositionSpeed && !_targetFov.HasValue;
            var panSnapped = false;
            var tiltSnapped = false;
            var fovSnapped = false;
            var zoomScale = GetPanTiltZoomScale(currentFovDeg);

            // Target braking for Pan/Tilt/Zoom
            UpdatePanTargetBraking(currentYawDeg, zoomScale, ref result, ref desiredPanVel, ref panSnapped);
            UpdateTiltTargetBraking(currentPitchDeg, zoomScale, ref result, ref desiredTiltVel, ref tiltSnapped);
            UpdateZoomTargetBraking(currentFovDeg, fovMin, fovMax, ref result, ref desiredFovVel, ref fovSnapped);

            // Apply zoom scale to velocities when not using target braking
            if (!UseTargetBraking || !_targetPanDeg.HasValue) desiredPanVel *= zoomScale;
            if (!UseTargetBraking || !_targetTiltDeg.HasValue) desiredTiltVel *= zoomScale;

            // Apply velocity smoothing
            desiredPanVel = ApplyVelocitySmoothing(ref _panVelSmoothed, desiredPanVel, dt);
            desiredTiltVel = ApplyVelocitySmoothing(ref _tiltVelSmoothed, desiredTiltVel, dt);
            if (useZoomPositionSpeed)
                desiredZoomVel = ApplyVelocitySmoothing(ref _zoomVelSmoothed, desiredZoomVel, dt);
            else
                desiredFovVel = ApplyVelocitySmoothing(ref _zoomVelSmoothed, desiredFovVel, dt);

            // Apply acceleration limiting and update current velocities
            var panVel = desiredPanVel;
            var tiltVel = desiredTiltVel;
            var fovVel = desiredFovVel;
            var zoomVel = desiredZoomVel;
            ApplyAccelerationLimiting(useZoomPositionSpeed, dt, ref panVel, ref tiltVel, ref fovVel, ref zoomVel,
                desiredPanVel, desiredTiltVel, desiredFovVel, desiredZoomVel);

            // Apply velocity to result
            ApplyPanTiltVelocity(currentYawDeg, currentPitchDeg, dt, panVel, tiltVel, panSnapped, tiltSnapped,
                ref result);

            // Update Zoom/FOV
            UpdateZoomFov(currentFovDeg, fovMin, fovMax, dt, useZoomPositionSpeed, fovVel, zoomVel, fovSnapped,
                ref result);

            // Update Focus and Iris
            UpdateFocus(dt);
            UpdateIris(dt);

            return result;
        }

        private void UpdatePanTargetBraking(float currentYawDeg, float zoomScale, ref PtzStepResult result,
            ref float desiredPanVel, ref bool panSnapped)
        {
            if (!UseTargetBraking || !_targetPanDeg.HasValue) return;

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
                var speedLimit = _targetPanSpeedLimit >= 0f ? _targetPanSpeedLimit : PanMaxDegPerSec;
                var speedFloor = _targetPanSpeedFloor > 0f ? _targetPanSpeedFloor : 0f;
                desiredPanVel = ComputeBrakingVelocity(distance, PanDecelDegPerSec2, PanStopDistanceDeg,
                    speedLimit * zoomScale, speedFloor * zoomScale);
            }
        }

        private void UpdateTiltTargetBraking(float currentPitchDeg, float zoomScale, ref PtzStepResult result,
            ref float desiredTiltVel, ref bool tiltSnapped)
        {
            if (!UseTargetBraking || !_targetTiltDeg.HasValue) return;

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
                var speedLimit = _targetTiltSpeedLimit >= 0f ? _targetTiltSpeedLimit : TiltMaxDegPerSec;
                var speedFloor = _targetTiltSpeedFloor > 0f ? _targetTiltSpeedFloor : 0f;
                desiredTiltVel = ComputeBrakingVelocity(distance, TiltDecelDegPerSec2, TiltStopDistanceDeg,
                    speedLimit * zoomScale, speedFloor * zoomScale);
            }
        }

        private void UpdateZoomTargetBraking(float currentFovDeg, float fovMin, float fovMax, ref PtzStepResult result,
            ref float desiredFovVel, ref bool fovSnapped)
        {
            if (!UseTargetBraking || !_targetFov.HasValue) return;

            var targetFov = PtzMathUtils.Clamp(_targetFov.Value, fovMin, fovMax);
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

        private void ApplyAccelerationLimiting(bool useZoomPositionSpeed, float dt,
            ref float panVel, ref float tiltVel, ref float fovVel, ref float zoomVel,
            float desiredPanVel, float desiredTiltVel, float desiredFovVel, float desiredZoomVel)
        {
            if (UseAccelerationLimit)
            {
                panVel = ApplyAccelLimit(_omegaPanCurrent, desiredPanVel, PanAccelDegPerSec2, PanDecelDegPerSec2, dt);
                tiltVel = ApplyAccelLimit(_omegaTiltCurrent, desiredTiltVel, TiltAccelDegPerSec2, TiltDecelDegPerSec2,
                    dt);
                if (useZoomPositionSpeed)
                {
                    zoomVel = ApplyAccelLimit(_omegaZoomCurrent, desiredZoomVel, ZoomAccelDegPerSec2,
                        ZoomDecelDegPerSec2, dt);
                    _omegaZoomCurrent = zoomVel;
                    _omegaFovCurrent = 0f;
                }
                else
                {
                    fovVel = ApplyAccelLimit(_omegaFovCurrent, desiredFovVel, ZoomAccelDegPerSec2, ZoomDecelDegPerSec2,
                        dt);
                    _omegaFovCurrent = fovVel;
                    _omegaZoomCurrent = 0f;
                }

                _omegaPanCurrent = panVel;
                _omegaTiltCurrent = tiltVel;
            }
            else
            {
                _omegaPanCurrent = panVel;
                _omegaTiltCurrent = tiltVel;
                if (useZoomPositionSpeed)
                {
                    _omegaZoomCurrent = zoomVel;
                    _omegaFovCurrent = 0f;
                }
                else
                {
                    _omegaFovCurrent = fovVel;
                    _omegaZoomCurrent = 0f;
                }
            }
        }

        private void ApplyPanTiltVelocity(float currentYawDeg, float currentPitchDeg, float dt,
            float panVel, float tiltVel, bool panSnapped, bool tiltSnapped, ref PtzStepResult result)
        {
            // Apply velocity
            if (!panSnapped) result.DeltaYawDeg += panVel * dt;
            if (!tiltSnapped) result.DeltaPitchDeg += tiltVel * dt;

            // Apply absolute positioning with damping (when not using target braking)
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
        }

        private void UpdateZoomFov(float currentFovDeg, float fovMin, float fovMax, float dt,
            bool useZoomPositionSpeed, float fovVel, float zoomVel, bool fovSnapped, ref PtzStepResult result)
        {
            var newFov = currentFovDeg;
            if (useZoomPositionSpeed)
            {
                var zoomNorm = GetZoomNormalizedFromFov(currentFovDeg);
                if (!fovSnapped) zoomNorm += zoomVel * dt;
                newFov = GetFovFromZoomNormalized(zoomNorm);
            }
            else
            {
                if (!fovSnapped) newFov = currentFovDeg + fovVel * dt;
                if (!UseTargetBraking && _targetFov.HasValue)
                {
                    var targetFov = PtzMathUtils.Clamp(_targetFov.Value, fovMin, fovMax);
                    newFov = PtzMathUtils.Damp(currentFovDeg, targetFov, MoveDamping, dt);
                    if (Math.Abs(newFov - targetFov) < 0.1f) _targetFov = null;
                }
            }

            newFov = PtzMathUtils.Clamp(newFov, fovMin, fovMax);
            if (Math.Abs(newFov - currentFovDeg) > 1e-4f)
            {
                result.NewFovDeg = newFov;
                result.HasNewFov = true;
            }
        }

        private void UpdateFocus(float dt)
        {
            CurrentFocus += _omegaFocus * dt;
            if (_targetFocus.HasValue)
            {
                var targetFocus = PtzMathUtils.Clamp(_targetFocus.Value, FocusMin, FocusMax);
                CurrentFocus = PtzMathUtils.Damp(CurrentFocus, targetFocus, MoveDamping, dt);
                if (Math.Abs(CurrentFocus - targetFocus) < 1f) _targetFocus = null;
            }

            CurrentFocus = PtzMathUtils.Clamp(CurrentFocus, FocusMin, FocusMax);
        }

        private void UpdateIris(float dt)
        {
            CurrentIris += _omegaIris * dt;
            if (_targetIris.HasValue)
            {
                var targetIris = PtzMathUtils.Clamp(_targetIris.Value, IrisMin, IrisMax);
                CurrentIris = PtzMathUtils.Damp(CurrentIris, targetIris, MoveDamping, dt);
                if (Math.Abs(CurrentIris - targetIris) < 1f) _targetIris = null;
            }

            CurrentIris = PtzMathUtils.Clamp(CurrentIris, IrisMin, IrisMax);
        }

        #endregion

        public float GetFovFromZoomPosition(ushort zoomPos)
        {
            if (TryGetFovFromZoomPosition(zoomPos, out var fovDeg))
                return fovDeg;
            return MinFov;
        }

        public ushort GetZoomPositionFromFov(float fovDeg)
        {
            var zoomNorm = GetZoomNormalizedFromFov(fovDeg);
            return (ushort)(Clamp01(zoomNorm) * 65535f);
        }

        private float GetPanTiltZoomScale(float currentFovDeg)
        {
            if (!EnablePanTiltSpeedScaleByZoom) return 1f;
            var range = MaxFov - MinFov;
            if (range <= ViscaProtocol.DivisionEpsilon) return 1f;
            var zoomT = PtzMathUtils.Clamp((MaxFov - currentFovDeg) / range, 0f, 1f);
            return PtzMathUtils.Clamp(PtzMathUtils.Lerp(1f, PanTiltSpeedScaleAtTele, zoomT), 0f, 2f);
        }

        private void GetPanSpeedRange(out float min, out float max)
        {
            if (UseSlowPanTilt && PanSlowMaxDegPerSec > 0f)
            {
                min = PanSlowMinDegPerSec;
                max = PanSlowMaxDegPerSec;
            }
            else
            {
                min = PanMinDegPerSec;
                max = PanMaxDegPerSec;
            }

            NormalizeMinMax(ref min, ref max);
        }

        private void GetTiltSpeedRange(out float min, out float max)
        {
            if (UseSlowPanTilt && TiltSlowMaxDegPerSec > 0f)
            {
                min = TiltSlowMinDegPerSec;
                max = TiltSlowMaxDegPerSec;
            }
            else
            {
                min = TiltMinDegPerSec;
                max = TiltMaxDegPerSec;
            }

            NormalizeMinMax(ref min, ref max);
        }

        private float GetPresetPanMaxSpeed()
        {
            return PanPresetMaxDegPerSec > 0f ? PanPresetMaxDegPerSec : PanMaxDegPerSec;
        }

        private float GetPresetPanMinSpeed()
        {
            return PanPresetMinDegPerSec > 0f ? PanPresetMinDegPerSec : PanMinDegPerSec;
        }

        private float GetPresetTiltMaxSpeed()
        {
            return TiltPresetMaxDegPerSec > 0f ? TiltPresetMaxDegPerSec : TiltMaxDegPerSec;
        }

        private float GetPresetTiltMinSpeed()
        {
            return TiltPresetMinDegPerSec > 0f ? TiltPresetMinDegPerSec : TiltMinDegPerSec;
        }

        private void GetFovLimits(out float min, out float max)
        {
            min = MinFov;
            max = MaxFov;

            if (UseLensProfile && IsLensProfileValid())
            {
                var lensMin = ComputeVerticalFovDeg(FocalLengthMaxMm, SensorHeightMm);
                var lensMax = ComputeVerticalFovDeg(FocalLengthMinMm, SensorHeightMm);
                if (lensMax < lensMin)
                {
                    var tmp = lensMin;
                    lensMin = lensMax;
                    lensMax = tmp;
                }

                min = Math.Max(min, lensMin);
                max = Math.Min(max, lensMax);
            }

            NormalizeMinMax(ref min, ref max);
        }

        private float GetZoomMaxNormalizedPerSec()
        {
            if (ZoomMaxNormalizedPerSec > 0f) return ZoomMaxNormalizedPerSec;

            GetFovLimits(out var fovMin, out var fovMax);
            var range = Math.Max(ViscaProtocol.DivisionEpsilon, fovMax - fovMin);
            return ZoomMaxFovPerSec / range;
        }

        private bool TryGetFovFromZoomPosition(ushort zoomPos, out float fovDeg)
        {
            var zoomNorm = zoomPos / 65535f;
            if (!ZoomPositionTeleAtMax) zoomNorm = 1f - zoomNorm;

            if (UseLensProfile && IsLensProfileValid())
            {
                var focal = PtzMathUtils.Lerp(FocalLengthMinMm, FocalLengthMaxMm, zoomNorm);
                fovDeg = ComputeVerticalFovDeg(focal, SensorHeightMm);
                return true;
            }

            var fovNorm = ZoomPositionTeleAtMax ? 1f - zoomNorm : zoomNorm;
            fovDeg = PtzMathUtils.Lerp(MinFov, MaxFov, Clamp01(fovNorm));
            return true;
        }

        private float GetFovFromZoomNormalized(float zoomNorm)
        {
            var zoomPosNorm = Clamp01(zoomNorm);
            if (!ZoomPositionTeleAtMax) zoomPosNorm = 1f - zoomPosNorm;

            if (UseLensProfile && IsLensProfileValid())
            {
                var focal = PtzMathUtils.Lerp(FocalLengthMinMm, FocalLengthMaxMm, zoomPosNorm);
                return ComputeVerticalFovDeg(focal, SensorHeightMm);
            }

            var fovNorm = ZoomPositionTeleAtMax ? 1f - zoomPosNorm : zoomPosNorm;
            return PtzMathUtils.Lerp(MinFov, MaxFov, Clamp01(fovNorm));
        }

        private float GetZoomNormalizedFromFov(float fovDeg)
        {
            if (UseLensProfile && IsLensProfileValid())
            {
                var focal = ComputeFocalLengthFromVerticalFov(fovDeg, SensorHeightMm);
                var lensNorm = PtzMathUtils.SafeInverseLerp(FocalLengthMinMm, FocalLengthMaxMm, focal);
                lensNorm = Clamp01(lensNorm);
                return ZoomPositionTeleAtMax ? lensNorm : 1f - lensNorm;
            }

            var range = MaxFov - MinFov;
            if (range <= ViscaProtocol.DivisionEpsilon)
                return ZoomPositionTeleAtMax ? 1f : 0f;

            var fovNorm = Clamp01((fovDeg - MinFov) / range);
            return ZoomPositionTeleAtMax ? 1f - fovNorm : fovNorm;
        }

        private bool IsLensProfileValid()
        {
            return SensorHeightMm > 0f &&
                   FocalLengthMinMm > 0f &&
                   FocalLengthMaxMm > FocalLengthMinMm;
        }

        private static float ComputeVerticalFovDeg(float focalLengthMm, float sensorHeightMm)
        {
            if (focalLengthMm <= 0f || sensorHeightMm <= 0f) return 0f;
            var half = sensorHeightMm / (2f * focalLengthMm);
            return (float)(2.0 * (180.0 / Math.PI) * Math.Atan(half));
        }

        private static float ComputeFocalLengthFromVerticalFov(float fovDeg, float sensorHeightMm)
        {
            if (sensorHeightMm <= 0f) return 0f;
            var rad = fovDeg * Math.PI / 180.0;
            var tanHalf = Math.Tan(rad / 2.0);
            if (Math.Abs(tanHalf) < 1e-6) return 0f;
            return (float)(sensorHeightMm / (2.0 * tanHalf));
        }

        private static void NormalizeMinMax(ref float min, ref float max)
        {
            if (min < 0f) min = 0f;
            if (max < min) max = min;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
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

        private float ApplyVelocitySmoothing(ref float smoothed, float target, float dt)
        {
            if (!UseVelocitySmoothing || VelocitySmoothingTime <= 0f)
            {
                smoothed = target;
                return target;
            }

            var t = Math.Max(0f, VelocitySmoothingTime);
            var alpha = 1f - (float)Math.Exp(-dt / t);
            smoothed += (target - smoothed) * alpha;
            return smoothed;
        }

        private static float ComputeBrakingVelocity(float distance, float decel, float stopDistance, float speedLimit,
            float minSpeed = 0f)
        {
            if (Math.Abs(distance) <= stopDistance) return 0f;
            var limit = Math.Abs(speedLimit);
            if (limit <= 0f) return 0f;
            var floor = Math.Max(0f, Math.Min(minSpeed, limit));
            if (decel <= 0f)
            {
                var vLimit = limit;
                var v = vLimit < floor ? floor : vLimit;
                return Math.Sign(distance) * v;
            }

            var vBrake = (float)Math.Sqrt(2f * decel * Math.Max(0f, Math.Abs(distance) - stopDistance));
            var vClamped = Math.Min(vBrake, limit);
            if (vClamped < floor) vClamped = floor;
            return Math.Sign(distance) * vClamped;
        }
    }
}
