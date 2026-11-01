using System;
using UnityEngine;

namespace Vigilante8.Gameplay
{
    /// <summary>
    /// Persistent, validated player presentation settings. All displayed options
    /// correspond to an actual Unity/Windows API, not mock menu toggles.
    /// </summary>
    public sealed class GameMenuSettings
    {
        private const string Prefix = "v8pc.preferences.v1.";
        public const string CharacterKey = Prefix + "character";
        public const string VehicleKey = Prefix + "vehicle";
        public const string ArenaKey = Prefix + "arena";

        public float MasterVolume { get; private set; } = 0.85f;
        public bool Fullscreen { get; private set; } = true;
        public int QualityLevel { get; private set; }

        public static float NormalizeVolume(float volume)
        {
            return float.IsNaN(volume) || float.IsInfinity(volume)
                ? 0.85f : Mathf.Clamp01(volume);
        }

        public static int NormalizeQuality(int level, int available)
        {
            if (available <= 0) return 0;
            return Mathf.Clamp(level, 0, available - 1);
        }

        public void Load()
        {
            MasterVolume = NormalizeVolume(PlayerPrefs.GetFloat(Prefix + "volume", 0.85f));
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", 1) != 0;
            QualityLevel = NormalizeQuality(
                PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel()),
                QualitySettings.names.Length);
            Apply();
        }

        public void SetVolume(float value)
        {
            MasterVolume = NormalizeVolume(value);
            Apply();
            Save();
        }

        public void SetFullscreen(bool enabled)
        {
            Fullscreen = enabled;
            Apply();
            Save();
        }

        public void SetQuality(int level)
        {
            QualityLevel = NormalizeQuality(level, QualitySettings.names.Length);
            Apply();
            Save();
        }

        private void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (QualitySettings.names.Length > 0)
                QualitySettings.SetQualityLevel(QualityLevel, true);
            // Avoid changing the developer's Editor game view.
            if (!Application.isEditor)
                Screen.fullScreen = Fullscreen;
        }

        private void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "volume", MasterVolume);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.Save();
        }

        public static string RestoreSelection(string key, string[] officialIds)
        {
            if (officialIds == null || officialIds.Length == 0) return null;
            string stored = PlayerPrefs.GetString(key, "");
            return Array.IndexOf(officialIds, stored) >= 0 ? stored : officialIds[0];
        }

        public static void StoreSelection(string key, string id, string[] officialIds)
        {
            if (officialIds == null || Array.IndexOf(officialIds, id) < 0)
                throw new ArgumentException("Cannot persist a nonofficial catalog selection", nameof(id));
            if (key != CharacterKey && key != VehicleKey && key != ArenaKey)
                throw new ArgumentException("Invalid selection preference key", nameof(key));
            PlayerPrefs.SetString(key, id);
            PlayerPrefs.Save();
        }
    }
}
