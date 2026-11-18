using UnityEngine;

/// <summary>Main menu: Play, Settings, Credits, Quit game. Restyle it in the Menu_MainMenu prefab.</summary>
public class MainMenu : MonoBehaviour
{
    [Header("Parts (set in the prefab)")]
    [Tooltip("Everything visible. Hidden when the menu is closed.")]
    [SerializeField] GameObject content;
    [Tooltip("The title + button list. Hidden while Settings or Credits is open.")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] UnityEngine.UI.Button playButton;
    [SerializeField] UnityEngine.UI.Button settingsButton;
    [SerializeField] UnityEngine.UI.Button creditsButton;
    [SerializeField] UnityEngine.UI.Button quitButton;
    [SerializeField] SettingsMenu settings;
    [SerializeField] CreditsMenu credits;

    [Header("Play")]
    [Tooltip("Off = Play only closes the menu (testing inside a game scene). On = Play loads the scene below.")]
    [SerializeField] bool loadSceneOnPlay = false;
    [SerializeField] string playSceneName = "MAIN SCENE";

    [Header("Behaviour")]
    [Tooltip("Show the menu when the scene starts.")]
    [SerializeField] bool openOnStart = true;
    [Tooltip("Pause the game (time) while the menu is open. Needed when the menu is shown on top of a game scene.")]
    [SerializeField] bool pauseGameWhileOpen = true;

    /// <summary>True while the main menu is shown.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>The main menu in the current scene, if any.</summary>
    public static MainMenu Instance { get; private set; }

    /// <summary>Set before reloading a scene to keep its main menu closed once (e.g. Continue after death).</summary>
    public static bool SkipOpenOnNextStart { get; set; }

    CursorLockMode savedLockMode;
    bool savedCursorVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsOpen = false; Instance = null; SkipOpenOnNextStart = false; }

    void Awake()
    {
        Instance = this;
        MenuUtility.EnsureEventSystem();
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
        if (quitButton != null) quitButton.onClick.AddListener(MenuUtility.QuitGame);
        if (settings != null) settings.OnClosed += ShowMainPanel;
        if (credits != null) credits.OnClosed += ShowMainPanel;
    }

    void Start()
    {
        bool skip = SkipOpenOnNextStart;
        SkipOpenOnNextStart = false;
        if (openOnStart && !skip) Open();
        else if (content != null) content.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (IsOpen && pauseGameWhileOpen) Time.timeScale = 1f;
        IsOpen = false;
        Instance = null;
    }

    /// <summary>Shows the main menu (e.g. "Quit to main menu" in test mode).</summary>
    public void Open()
    {
        if (!IsOpen)
        {
            savedLockMode = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
        }
        IsOpen = true;
        if (content != null) content.SetActive(true);
        if (pauseGameWhileOpen) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ShowMainPanel();
    }

    /// <summary>Hides the main menu and lets the game continue.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (settings != null && settings.IsOpen) settings.gameObject.SetActive(false);
        if (credits != null && credits.IsOpen) credits.gameObject.SetActive(false);
        if (content != null) content.SetActive(false);
        if (pauseGameWhileOpen) Time.timeScale = 1f;
        Cursor.lockState = savedLockMode;
        Cursor.visible = savedCursorVisible;
    }

    public void Play()
    {
        if (loadSceneOnPlay)
        {
            IsOpen = false;
            MenuUtility.LoadScene(playSceneName);
        }
        else Close();
    }

    public void OpenSettings()
    {
        if (settings == null) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        settings.Open();
    }

    public void OpenCredits()
    {
        if (credits == null) return;
        if (mainPanel != null) mainPanel.SetActive(false);
        credits.Open();
    }

    void ShowMainPanel()
    {
        if (!IsOpen) return;
        if (mainPanel != null) mainPanel.SetActive(true);
        MenuUtility.Select(playButton != null ? playButton.gameObject : null);
    }
}
