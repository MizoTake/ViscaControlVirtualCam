namespace ViscaControlVirtualCam
{
    /// <summary>
    /// PlayerPrefs adapter interface for dependency injection
    /// </summary>
    public interface IPlayerPrefsAdapter
    {
        void SetFloat(string key, float value);
        float GetFloat(string key, float defaultValue);
        bool HasKey(string key);
        void DeleteKey(string key);
        void Save();
    }
}
