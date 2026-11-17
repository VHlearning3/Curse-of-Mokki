using System;
using UnityEngine;

/// <summary>
/// Settings panel (GDD: music and SFX volume, music and SFX on/off). Used by both the main menu and the pause menu.
/// Lives in the Menu_Settings prefab; the UI controls are assigned there and can be restyled freely.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Slider musicVolume;
    [SerializeField] UnityEngine.UI.Toggle musicOn;
    [SerializeField] UnityEngine.UI.Slider sfxVolume;
    [SerializeField] UnityEngine.UI.Toggle sfxOn;
    [SerializeField] UnityEngine.UI.Button backButton;

    /// <summary>The panel was closed (Back).</summary>
    public event Action OnClosed;

    public bool IsOpen => gameObject.activeSelf;

    bool wired;

    void Awake() => Wire();

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (musicVolume != null) musicVolume.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
        if (sfxVolume != null) sfxVolume.onValueChanged.AddListener(v => GameSettings.SfxVolume = v);
        if (musicOn != null) musicOn.onValueChanged.AddListener(v => GameSettings.MusicEnabled = v);
        if (sfxOn != null) sfxOn.onValueChanged.AddListener(v => GameSettings.SfxEnabled = v);
        if (backButton != null) backButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        Wire();
        if (musicVolume != null) musicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxVolume != null) sfxVolume.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (musicOn != null) musicOn.SetIsOnWithoutNotify(GameSettings.MusicEnabled);
        if (sfxOn != null) sfxOn.SetIsOnWithoutNotify(GameSettings.SfxEnabled);
        MenuUtility.Select(musicVolume != null ? musicVolume.gameObject : null);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        GameSettings.Save();
        gameObject.SetActive(false);
        OnClosed?.Invoke();
    }
}
