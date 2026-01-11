using System.Collections.Generic;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Manages PTZ memory presets with optional persistence via PlayerPrefs.
    ///     Handles saving, loading, recalling, and deleting camera position presets.
    /// </summary>
    public class PtzMemoryManager
    {
        private const int DefaultPresetCount = 10;

        private readonly Dictionary<byte, PtzMemoryPreset> _memoryPresets = new();
        private readonly IPlayerPrefsAdapter _playerPrefs;
        private readonly string _prefsKeyPrefix;

        /// <summary>
        ///     Constructor with optional PlayerPrefs adapter for persistence.
        /// </summary>
        /// <param name="playerPrefs">PlayerPrefs adapter (null = no persistence)</param>
        /// <param name="prefsKeyPrefix">Prefix for PlayerPrefs keys (default: "ViscaPtz_")</param>
        public PtzMemoryManager(IPlayerPrefsAdapter playerPrefs = null, string prefsKeyPrefix = "ViscaPtz_")
        {
            _playerPrefs = playerPrefs;
            _prefsKeyPrefix = prefsKeyPrefix;

            if (_playerPrefs != null)
                LoadAllPresets();
        }

        /// <summary>
        ///     Try to get a preset by memory number.
        /// </summary>
        /// <param name="memoryNumber">Memory slot number (0-255)</param>
        /// <param name="preset">Output preset if found</param>
        /// <returns>True if preset exists</returns>
        public bool TryGetPreset(byte memoryNumber, out PtzMemoryPreset preset)
        {
            return _memoryPresets.TryGetValue(memoryNumber, out preset);
        }

        /// <summary>
        ///     Save current camera state to a memory slot.
        /// </summary>
        /// <param name="memoryNumber">Memory slot number (0-255)</param>
        /// <param name="panDeg">Current pan angle in degrees</param>
        /// <param name="tiltDeg">Current tilt angle in degrees</param>
        /// <param name="fovDeg">Current field of view in degrees</param>
        /// <param name="focusPos">Current focus position</param>
        /// <param name="irisPos">Current iris position</param>
        public void SavePreset(byte memoryNumber, float panDeg, float tiltDeg, float fovDeg, float focusPos,
            float irisPos)
        {
            var preset = new PtzMemoryPreset
            {
                PanDeg = panDeg,
                TiltDeg = tiltDeg,
                FovDeg = fovDeg,
                FocusPos = focusPos,
                IrisPos = irisPos
            };

            _memoryPresets[memoryNumber] = preset;
            PersistPreset(memoryNumber, preset);
        }

        /// <summary>
        ///     Delete a preset from memory and persistence.
        /// </summary>
        /// <param name="memoryNumber">Memory slot number to delete</param>
        public void DeletePreset(byte memoryNumber)
        {
            _memoryPresets.Remove(memoryNumber);
            DeletePersistedPreset(memoryNumber);
        }

        /// <summary>
        ///     Get all saved preset numbers.
        /// </summary>
        public IEnumerable<byte> GetSavedPresets()
        {
            return _memoryPresets.Keys;
        }

        /// <summary>
        ///     Check if a preset exists.
        /// </summary>
        public bool HasPreset(byte memoryNumber)
        {
            return _memoryPresets.ContainsKey(memoryNumber);
        }

        /// <summary>
        ///     Clear all presets from memory (does not affect persistence).
        /// </summary>
        public void ClearAll()
        {
            _memoryPresets.Clear();
        }

        private void PersistPreset(byte memoryNumber, PtzMemoryPreset preset)
        {
            if (_playerPrefs == null) return;

            var key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
            _playerPrefs.SetFloat(key + "Pan", preset.PanDeg);
            _playerPrefs.SetFloat(key + "Tilt", preset.TiltDeg);
            _playerPrefs.SetFloat(key + "Fov", preset.FovDeg);
            _playerPrefs.SetFloat(key + "Focus", preset.FocusPos);
            _playerPrefs.SetFloat(key + "Iris", preset.IrisPos);
            _playerPrefs.Save();
        }

        private bool LoadPreset(byte memoryNumber, out PtzMemoryPreset preset)
        {
            preset = default;
            if (_playerPrefs == null) return false;

            var key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
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

        private void LoadAllPresets()
        {
            if (_playerPrefs == null) return;

            for (byte i = 0; i < DefaultPresetCount; i++)
                if (LoadPreset(i, out var preset))
                    _memoryPresets[i] = preset;
        }

        private void DeletePersistedPreset(byte memoryNumber)
        {
            if (_playerPrefs == null) return;

            var key = $"{_prefsKeyPrefix}Mem{memoryNumber}_";
            _playerPrefs.DeleteKey(key + "Pan");
            _playerPrefs.DeleteKey(key + "Tilt");
            _playerPrefs.DeleteKey(key + "Fov");
            _playerPrefs.DeleteKey(key + "Focus");
            _playerPrefs.DeleteKey(key + "Iris");
            _playerPrefs.Save();
        }
    }
}
