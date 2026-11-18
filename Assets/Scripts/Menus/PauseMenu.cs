using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pause menu (P): Resume, Settings, Quit to main menu. Stops time and frees the cursor.
/// Restyle it in the Menu_PauseMenu prefab.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Parts (set in the prefab)")]
    [Tooltip("Everything visible. Hidden when not paused.")]
    [SerializeField] GameObject content;
    [Tooltip("The title + button list. Hidden while Settings is open.")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] UnityEngine.UI.Button resumeButton;
    [SerializeField] UnityEngine.UI.Button settingsButton;
    [SerializeField] UnityEngine.UI.Button quitToMainMenuButton;
    [SerializeField] SettingsMenu settings;

    [Header("Key")]
    [Tooltip("Opens and closes the pause menu.")]
    [SerializeField] Key pauseKey = Key.P;

    [Header("Quit to main menu")]
    [Tooltip("Off = opens the main menu in this scene (testing). On = loads the scene below.")]
    [SerializeField] bool loadSceneOnQuit = false;
    [SerializeField] string mainMenuSceneName = "";

    /// <summary>True while the game is paused by this menu.</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>Pause state changed. Argument: true = paused.</summary>
    public static event Action<bool> OnPauseChanged;

    float savedTimeScale = 1f;
    CursorLockMode savedLockMode;
    bool savedCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsPaused = false; OnPauseChanged = null; }

    void Awake()
    {
        MenuUtility.EnsureEventSystem();
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitToMainMenuButton != null) quitToMainMenuButton.onClick.AddListener(QuitToMainMenu);
        if (settings != null) settings.OnClosed += ShowMainPanel;
        if (content != null) content.SetActive(false);
    }

    void OnDestroy()
    {
        if (IsPaused) SetPaused(false, restore: true);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || pauseKey == Key.None || !kb[pauseKey].wasPressedThisFrame) return;
        if (MainMenu.IsOpen || DeathEndScreen.IsOpen) return;

        if (!IsPaused) Pause();
        else if (settings != null && settings.IsOpen) settings.Close(); // back to the pause menu
        else Resume();
    }

    public void Pause()
    {
        if (IsPaused || MainMenu.IsOpen || DeathEndScreen.IsOpen) return;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        savedLockMode = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        SetPaused(true, restore: false);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        SetPaused(false, restore: true);
    }

    public void OpenSettings()
    {
        if (settings == null) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        settings.Open();
    }

    public void QuitToMainMenu()
    {
        SetPaused(false, restore: false);
        Time.timeScale = 1f;
        if (loadSceneOnQuit) MenuUtility.LoadScene(mainMenuSceneName);
        else if (MainMenu.Instance != null) MainMenu.Instance.Open();
        else Debug.LogWarning("[PauseMenu] No main menu in this scene and Load Scene On Quit is off.", this);
    }

    void SetPaused(bool paused, bool restore)
    {
        IsPaused = paused;
        if (paused)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (content != null) content.SetActive(true);
            ShowMainPanel();
        }
        else
        {
            if (settings != null && settings.IsOpen) settings.gameObject.SetActive(false);
            if (content != null) content.SetActive(false);
            if (restore)
            {
                Time.timeScale = savedTimeScale;
                Cursor.lockState = savedLockMode;
                Cursor.visible = savedCursorVisible;
            }
        }
        OnPauseChanged?.Invoke(paused);
    }

    void ShowMainPanel()
    {
        if (!IsPaused) return;
        if (mainPanel != null) mainPanel.SetActive(true);
        MenuUtility.Select(resumeButton != null ? resumeButton.gameObject : null);
    }
}
