using System.Collections;
using UnityEngine;

/// <summary>
/// Death / end screen: Continue, Quit. Death opens on PlayerMadness.OnDied; the end via ShowEnd().
/// Restyle it in the Menu_DeathEndScreen prefab.
/// </summary>
public class DeathEndScreen : MonoBehaviour
{
    [Header("Parts (set in the prefab)")]
    [Tooltip("Everything visible. Hidden until death or the end.")]
    [SerializeField] GameObject content;
    [Tooltip("The title + buttons. Hidden while the credits are shown.")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] TMPro.TextMeshProUGUI titleText;
    [SerializeField] UnityEngine.UI.Button continueButton;
    [SerializeField] UnityEngine.UI.Button quitButton;
    [SerializeField] CreditsMenu credits;

    [Header("Texts")]
    [SerializeField] string deathTitle = "You died";
    [SerializeField] string endTitle = "The End";

    [Header("Death")]
    [Tooltip("Leave empty to use the first PlayerMadness in the scene.")]
    [SerializeField] PlayerMadness madness;
    [Tooltip("Open automatically when the player dies.")]
    [SerializeField] bool showOnDeath = true;
    [Tooltip("Seconds between the death and the screen (lets the death effect play).")]
    [SerializeField, Min(0f)] float deathDelay = 1.5f;

    [Header("Quit to main menu")]
    [Tooltip("Off = restarts this scene with its main menu open (testing). On = loads the scene below.")]
    [SerializeField] bool loadSceneOnQuit = false;
    [SerializeField] string mainMenuSceneName = "";

    public enum Mode { Hidden, Death, End }

    /// <summary>True while the death or end screen is shown.</summary>
    public static bool IsOpen { get; private set; }

    public Mode CurrentMode { get; private set; } = Mode.Hidden;

    Coroutine pendingShow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsOpen = false; }

    void Awake()
    {
        MenuUtility.EnsureEventSystem();
        if (madness == null) madness = FindFirstObjectByType<PlayerMadness>();
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (quitButton != null) quitButton.onClick.AddListener(QuitToMainMenu);
        if (credits != null) credits.OnClosed += HandleCreditsClosed;
        if (content != null) content.SetActive(false);
    }

    void OnEnable()
    {
        if (madness != null) madness.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (madness != null) madness.OnDied -= HandleDied;
    }

    void OnDestroy()
    {
        if (CurrentMode != Mode.Hidden) { IsOpen = false; Time.timeScale = 1f; }
    }

    void HandleDied()
    {
        if (!showOnDeath) return;
        if (pendingShow != null) StopCoroutine(pendingShow);
        pendingShow = StartCoroutine(ShowDeathAfterDelay());
    }

    IEnumerator ShowDeathAfterDelay()
    {
        if (deathDelay > 0f) yield return new WaitForSecondsRealtime(deathDelay);
        pendingShow = null;
        ShowDeath();
    }

    /// <summary>Shows the death screen now.</summary>
    public void ShowDeath() => Show(Mode.Death, deathTitle);

    /// <summary>Shows the end screen (call at the end of the final cutscene).</summary>
    public void ShowEnd() => Show(Mode.End, endTitle);

    void Show(Mode mode, string title)
    {
        if (PauseMenu.IsPaused) FindFirstObjectByType<PauseMenu>()?.Resume();
        CurrentMode = mode;
        IsOpen = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (titleText != null) titleText.text = title;
        if (content != null) content.SetActive(true);
        if (credits != null && credits.IsOpen) credits.gameObject.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        MenuUtility.Select(continueButton != null ? continueButton.gameObject : null);
    }

    public void Continue()
    {
        if (CurrentMode == Mode.Death)
        {
            Hide();
            MenuUtility.ReloadCurrentScene(skipMainMenu: true); // TODO: respawn at the last save point when it exists
        }
        else if (CurrentMode == Mode.End)
        {
            if (credits == null) { QuitToMainMenu(); return; }
            if (mainPanel != null) mainPanel.SetActive(false);
            credits.Open();
        }
    }

    public void QuitToMainMenu()
    {
        Hide();
        if (loadSceneOnQuit) MenuUtility.LoadScene(mainMenuSceneName);
        else MenuUtility.ReloadCurrentScene(skipMainMenu: false);
    }

    void HandleCreditsClosed()
    {
        if (CurrentMode == Mode.End) QuitToMainMenu();
    }

    void Hide()
    {
        CurrentMode = Mode.Hidden;
        IsOpen = false;
        if (content != null) content.SetActive(false);
        Time.timeScale = 1f;
    }
}
