using System;
using System.Collections.Generic;
using System.Linq;

namespace ViscaControlVirtualCam
{
    public struct PtzStepResult
    {
        public float DeltaYawDeg;   // +right
        public float DeltaPitchDeg; // +up
        public float NewFovDeg;     // absolute value
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

    // Pure C# PTZ core: holds state and computes step deltas from commands.
    public class PtzModel
    {
        // Limits and mapping
        public float PanMaxDegPerSec = 120f;
        public float TiltMaxDegPerSec = 90f;
        public float ZoomMaxFovPerSec = 40f;
        public float MinFov = 15f;
        public float MaxFov = 90f;

        public bool InvertPan;
        public bool InvertTilt;
        public bool InvertPanAbsolute;
        public bool InvertTiltAbsolute;

        public bool UseAccelerationLimit;
        public float PanAccelDegPerSec2 = 600f;
        public float TiltAccelDegPerSec2 = 600f;
        public float ZoomAccelDegPerSec2 = 300f;

        public float PanMinDeg = -170f;
        public float PanMaxDeg = 170f;
        public float TiltMinDeg = -30f;
        public float TiltMaxDeg = 90f;
        public float MoveDamping = 6f; // for absolute moves

        public float SpeedGamma = 1.0f;
        public byte PanVmin = 0x01, PanVmax = 0x18;
        public byte TiltVmin = 0x01, TiltVmax = 0x14;

        // Blackmagic PTZ Control: Focus/Iris support
        public float FocusMaxSpeed = 100f; // units/sec
        public float IrisMaxSpeed = 50f;   // units/sec
        public float FocusMin = 0f;
        public float FocusMax = 65535f;
        public float IrisMin = 0f;
        public float IrisMax = 65535f;

        private float _omegaPan;  // +right, deg/s
        private float _omegaTilt; // +up, deg/s
        private float _omegaFov;  // +increase FOV, deg/s
        private float _omegaFocus; // +far, units/s
        private float _omegaIris;  // +open, units/s

        // Acceleration-limited velocities (optional)
        private float _omegaPanCurrent;
        private float _omegaTiltCurrent;
        private float _omegaFovCurrent;

        private float? _targetPanDeg;  // absolute target yaw
        private float? _targetTiltDeg; // absolute target pitch
        private float? _targetFov;     // absolute target FOV
        private float? _targetFocus;   // absolute target focus
        private float? _targetIris;    // absolute target iris
        
        // Home (initial) baseline
        private bool _hasHome;
        private float _homePanDeg;
        private float _homeTiltDeg;
        private float _homeFovDeg;
        private float _homeFocus;
        private float _homeIris;

        // Memory presets (Blackmagic PTZ Control)
        private readonly Dictionary<byte, PtzMemoryPreset> _memoryPresets = new Dictionary<byte, PtzMemoryPreset>();

        // PlayerPrefs adapter for persistence
        private readonly IPlayerPrefsAdapter _playerPrefs;
        private readonly string _prefsKeyPrefix;

        // Current state for memory operations
        public float CurrentPanDeg { get; private set; }
        public float CurrentTiltDeg { get; private set; }
        public float CurrentFovDeg { get; private set; }
        public float CurrentFocus { get; private set; }
        public float CurrentIris { get; private set; }

        /// <summary>
        /// Constructor with optional PlayerPrefs adapter for persistence
        /// </summary>
        /// <param name="playerPrefs">PlayerPrefs adapter (null = no persistence)</param>
        /// <param name="prefsKeyPrefix">Prefix for PlayerPrefs keys (default: "ViscaPtz_")</param>
        public PtzModel(IPlayerPrefsAdapter playerPrefs = null, string prefsKeyPrefix = "ViscaPtz_")
        {
            _playerPrefs = playerPrefs;
            _prefsKeyPrefix = prefsKeyPrefix;

            // Load presets from PlayerPrefs if available
            if (_playerPrefs != null)
            {
                LoadAllPresets();
            }
        }

        public void CommandPanTiltVariable(byte vv, byte ww, AxisDirection panDir, AxisDirection tiltDir)
        {
            float vPan = MapSpeed(vv, PanVmin, PanVmax, PanMaxDegPerSec, SpeedGamma);
            float vTilt = MapSpeed(ww, TiltVmin, TiltVmax, TiltMaxDegPerSec, SpeedGamma);
            float panSign = panDir == AxisDirection.Positive ? 1f : -1f;
            float tiltSign = tiltDir == AxisDirection.Positive ? 1f : -1f;
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
            float panNorm = panPos / 65535f;
            float tiltNorm = tiltPos / 65535f;
            if (InvertPanAbsolute) panNorm = 1f - panNorm;
            if (InvertTiltAbsolute) tiltNorm = 1f - tiltNorm;

            float panDeg = Lerp(PanMinDeg, PanMaxDeg, panNorm);
            float tiltDeg = Lerp(TiltMinDeg, TiltMaxDeg, tiltNorm);
            _targetPanDeg = panDeg;
            _targetTiltDeg = tiltDeg;
            _omegaPan = 0f;
            _omegaTilt = 0f;
        }

        /// <summary>
        /// Set home baseline (initial values) used by Home command
        /// </summary>
        public void SetHomeBaseline(float panDeg, float tiltDeg, float fovDeg, float focus, float iris)
        {
            _homePanDeg = panDeg;
            _homeTiltDeg = tiltDeg;
            _homeFovDeg = fovDeg;
            _homeFocus = focus;
            _homeIris = iris;
            _hasHome = true;
        }

        /// <summary>
        /// Reset targets to home baseline (Pan/Tilt/FOV/Focus/Iris)
        /// </summary>
        public void CommandHome()
        {
            if (!_hasHome)
            {
                // If home not set, default to zeros and current/mid values
                _homePanDeg = 0f;
                _homeTiltDeg = 0f;
                _homeFovDeg = Clamp(CurrentFovDeg == 0 ? 60f : CurrentFovDeg, MinFov, MaxFov);
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
        }

        // Blackmagic PTZ Control: Zoom Direct
        public void CommandZoomDirect(ushort zoomPos)
        {
            float fov = Lerp(MinFov, MaxFov, zoomPos / 65535f);
            _targetFov = fov;
            _omegaFov = 0f;
        }

        // Blackmagic PTZ Control: Focus Variable
        public void CommandFocusVariable(byte focusSpeed)
        {
            if (focusSpeed == 0x00)
            {
                _omegaFocus = 0f;
                return;
            }
            // 0x02 = Far, 0x03 = Near
            float sign = focusSpeed == 0x02 ? 1f : -1f;
            _omegaFocus = FocusMaxSpeed * sign;
        }

        // Blackmagic PTZ Control: Focus Direct
        public void CommandFocusDirect(ushort focusPos)
        {
            _targetFocus = focusPos;
            _omegaFocus = 0f;
        }

        // Blackmagic PTZ Control: Iris Variable
        public void CommandIrisVariable(byte irisDir)
        {
            if (irisDir == 0x00)
            {
                _omegaIris = 0f;
                return;
            }
            // 0x02 = Open, 0x03 = Close
            float sign = irisDir == 0x02 ? 1f : -1f;
            _omegaIris = IrisMaxSpeed * sign;
        }

        // Blackmagic PTZ Control: Iris Direct
        public void CommandIrisDirect(ushort irisPos)
        {
            _targetIris = irisPos;
            _omegaIris = 0f;
        }

        // Blackmagic PTZ Control: Memory Recall
        public void CommandMemoryRecall(byte memoryNumber)
        {
            if (_memoryPresets.TryGetValue(memoryNumber, out var preset))
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
            }
        }

        // Blackmagic PTZ Control: Memory Set
        public void CommandMemorySet(byte memoryNumber)
        {
            var preset = new PtzMemoryPreset
            {
                PanDeg = CurrentPanDeg,
                TiltDeg = CurrentTiltDeg,
                FovDeg = CurrentFovDeg,
                FocusPos = CurrentFocus,
                IrisPos = CurrentIris
            };
            _memoryPresets[memoryNumber] = preset;

            // Save to PlayerPrefs if available
            SavePreset(memoryNumber, preset);
        }

        /// <summary>
        /// Save a preset to PlayerPrefs
        /// </summary>
        private void SavePreset(byte memoryNumber, PtzMemoryPreset preset)
        {
            if (_playerPrefs == null) return;

            string key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
            _playerPrefs.SetFloat(key + "Pan", preset.PanDeg);
            _playerPrefs.SetFloat(key + "Tilt", preset.TiltDeg);
            _playerPrefs.SetFloat(key + "Fov", preset.FovDeg);
            _playerPrefs.SetFloat(key + "Focus", preset.FocusPos);
            _playerPrefs.SetFloat(key + "Iris", preset.IrisPos);
            _playerPrefs.Save();
        }

        /// <summary>
        /// Load a preset from PlayerPrefs
        /// </summary>
        private bool LoadPreset(byte memoryNumber, out PtzMemoryPreset preset)
        {
            preset = default;
            if (_playerPrefs == null) return false;

            string key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
            if (!_playerPrefs.HasKey(key + "Pan")) return false;

            preset = new PtzMemoryPreset
            {
                PanDeg = _playerPrefs.GetFloat(key + "Pan", 0f),
                TiltDeg = _playerPrefs.GetFloat(key + "Tilt", 0f),
                FovDeg = _playerPrefs.GetFloat(key + "Fov", 60f),
                FocusPos = _playerPrefs.GetFloat(key + "Focus", 0f),
                IrisPos = _playerPrefs.GetFloat(key + "Iris", 0f)
            };
            return true;
        }

        /// <summary>
        /// Load all presets from PlayerPrefs
        /// </summary>
        private void LoadAllPresets()
        {
            if (_playerPrefs == null) return;

            // Load presets 0-9 (common range for PTZ cameras)
            for (byte i = 0; i < 10; i++)
            {
                if (LoadPreset(i, out var preset))
                {
                    _memoryPresets[i] = preset;
                }
            }
        }

        /// <summary>
        /// Delete a preset from memory and PlayerPrefs
        /// </summary>
        public void DeletePreset(byte memoryNumber)
        {
            _memoryPresets.Remove(memoryNumber);

            if (_playerPrefs == null) return;

            string key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
            _playerPrefs.DeleteKey(key + "Pan");
            _playerPrefs.DeleteKey(key + "Tilt");
            _playerPrefs.DeleteKey(key + "Fov");
            _playerPrefs.DeleteKey(key + "Focus");
            _playerPrefs.DeleteKey(key + "Iris");
            _playerPrefs.Save();
        }

        /// <summary>
        /// Get all saved preset numbers
        /// </summary>
        public IEnumerable<byte> GetSavedPresets()
        {
            return _memoryPresets.Keys;
        }

        public PtzStepResult Step(float currentYawDeg, float currentPitchDeg, float currentFovDeg, float dt)
        {
            // Update current state for memory operations
            CurrentPanDeg = currentYawDeg;
            CurrentTiltDeg = currentPitchDeg;
            CurrentFovDeg = currentFovDeg;

            var result = new PtzStepResult();

            // Velocity drive
            float panVel = _omegaPan;
            float tiltVel = _omegaTilt;
            float fovVel = _omegaFov;

            if (UseAccelerationLimit)
            {
                panVel = MoveTowards(_omegaPanCurrent, _omegaPan, PanAccelDegPerSec2 * dt);
                tiltVel = MoveTowards(_omegaTiltCurrent, _omegaTilt, TiltAccelDegPerSec2 * dt);
                fovVel = MoveTowards(_omegaFovCurrent, _omegaFov, ZoomAccelDegPerSec2 * dt);

                _omegaPanCurrent = panVel;
                _omegaTiltCurrent = tiltVel;
                _omegaFovCurrent = fovVel;
            }

            result.DeltaYawDeg += panVel * dt;
            result.DeltaPitchDeg += tiltVel * dt;

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
            float newFov = currentFovDeg + fovVel * dt;
            if (_targetFov.HasValue)
            {
                float targetFov = Clamp(_targetFov.Value, MinFov, MaxFov);
                newFov = Damp(currentFovDeg, targetFov, MoveDamping, dt);
                if (Math.Abs(newFov - targetFov) < 0.1f) _targetFov = null;
            }
            newFov = Clamp(newFov, MinFov, MaxFov);
            if (Math.Abs(newFov - currentFovDeg) > 1e-4f)
            {
                result.NewFovDeg = newFov;
                result.HasNewFov = true;
            }

            // Focus (Blackmagic PTZ Control)
            CurrentFocus += _omegaFocus * dt;
            if (_targetFocus.HasValue)
            {
                float targetFocus = Clamp(_targetFocus.Value, FocusMin, FocusMax);
                CurrentFocus = Damp(CurrentFocus, targetFocus, MoveDamping, dt);
                if (Math.Abs(CurrentFocus - targetFocus) < 1f) _targetFocus = null;
            }
            CurrentFocus = Clamp(CurrentFocus, FocusMin, FocusMax);

            // Iris (Blackmagic PTZ Control)
            CurrentIris += _omegaIris * dt;
            if (_targetIris.HasValue)
            {
                float targetIris = Clamp(_targetIris.Value, IrisMin, IrisMax);
                CurrentIris = Damp(CurrentIris, targetIris, MoveDamping, dt);
                if (Math.Abs(CurrentIris - targetIris) < 1f) _targetIris = null;
            }
            CurrentIris = Clamp(CurrentIris, IrisMin, IrisMax);

            return result;
        }

        private static float MapSpeed(byte v, byte vmin, byte vmax, float maxDegPerSec, float gamma)
        {
            if (v == 0x00) v = vmin;
            float t = SafeInverseLerp(vmin, vmax, Clamp(v, vmin, vmax));
            float mapped = (float)Math.Pow(t, Math.Max(0.01f, gamma));
            return mapped * maxDegPerSec;
        }

        private static float Damp(float current, float target, float damping, float dt)
        {
            float k = 1f - (float)Math.Exp(-damping * dt);
            return current + (target - current) * k;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// Safe inverse lerp that handles equal min/max (returns 0.5 instead of NaN/Infinity).
        /// </summary>
        private static float SafeInverseLerp(float a, float b, float v)
        {
            float range = b - a;
            if (Math.Abs(range) < ViscaProtocol.DivisionEpsilon) return 0.5f;
            return (v - a) / range;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        private static float DeltaAngle(float a, float b)
        {
            float diff = (b - a) % 360f;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;
            return diff;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
