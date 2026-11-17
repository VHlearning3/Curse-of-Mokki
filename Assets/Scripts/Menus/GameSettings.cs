using System;
using UnityEngine;

/// <summary>
/// Player settings (GDD: music and SFX volume + on/off). Saved with PlayerPrefs.
/// The audio manager (Otto) reads these values and listens to OnChanged; the settings menu writes them.
/// </summary>
public static class GameSettings
{
    const string MusicVolumeKey = "Settings.MusicVolume";
    const string SfxVolumeKey = "Settings.SfxVolume";
    const string MusicEnabledKey = "Settings.MusicEnabled";
    const string SfxEnabledKey = "Settings.SfxEnabled";

    public const float DefaultVolume = 0.8f;

    /// <summary>Any setting changed.</summary>
    public static event Action OnChanged;

    /// <summary>Music volume 0..1 (slider value, ignores the on/off toggle).</summary>
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);
        set { PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); Changed(); }
    }

    /// <summary>Sound effects volume 0..1 (slider value, ignores the on/off toggle).</summary>
    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);
        set { PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); Changed(); }
    }

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
        set { PlayerPrefs.SetInt(MusicEnabledKey, value ? 1 : 0); Changed(); }
    }

    public static bool SfxEnabled
    {
        get => PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
        set { PlayerPrefs.SetInt(SfxEnabledKey, value ? 1 : 0); Changed(); }
    }

    /// <summary>The volume to actually play music at (0 when music is off).</summary>
    public static float EffectiveMusicVolume => MusicEnabled ? MusicVolume : 0f;

    /// <summary>The volume to actually play sound effects at (0 when SFX is off).</summary>
    public static float EffectiveSfxVolume => SfxEnabled ? SfxVolume : 0f;

    /// <summary>Writes the settings to disk (the settings menu calls this when closed).</summary>
    public static void Save() => PlayerPrefs.Save();

    static void Changed() => OnChanged?.Invoke();
}
