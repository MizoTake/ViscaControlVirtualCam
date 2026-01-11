using UnityEngine;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Unity PlayerPrefs implementation
    /// </summary>
    public class UnityPlayerPrefsAdapter : IPlayerPrefsAdapter
    {
        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }

        public float GetFloat(string key, float defaultValue)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}