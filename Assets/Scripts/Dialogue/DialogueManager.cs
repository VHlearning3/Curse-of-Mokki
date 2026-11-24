using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Displays dialogue lines at the bottom of the screen (HUD_Dialogue prefab).
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References (HUD_Dialogue prefab)")]
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] TextMeshProUGUI speakerText;
    [SerializeField] TextMeshProUGUI dialogueText;

    [Header("Fade")]
    [SerializeField, Min(0.01f)] float fadeSeconds = 0.25f;

    [Header("Skip")]
    [SerializeField] Key skipKey = Key.E;
    [SerializeField] bool allowMouseClickSkip = true;

    public bool IsPlaying => currentRoutine != null;

    Coroutine currentRoutine;
    PlayerMovement playerMovement;
    PlayerMadness playerMadness;
    AudioSource voiceSource;
    bool movementBlocked;
    bool skipRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panelGroup != null) panelGroup.alpha = 0f;

        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;

        playerMovement = FindFirstObjectByType<PlayerMovement>();
        playerMadness = FindFirstObjectByType<PlayerMadness>();
    }

    void OnEnable()
    {
        if (playerMadness != null) playerMadness.OnDied += StopDialogue;
    }

    void OnDisable()
    {
        if (playerMadness != null) playerMadness.OnDied -= StopDialogue;
        RestoreMovement();
    }

    void Update()
    {
        if (!IsPlaying || MenuUtility.AnyMenuOpen) return;

        bool skipPressed = false;
        if (Keyboard.current != null && skipKey != Key.None && Keyboard.current[skipKey].wasPressedThisFrame)
            skipPressed = true;
        if (allowMouseClickSkip && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            skipPressed = true;

        if (skipPressed) skipRequested = true;
    }

    /// <summary>Starts playing a DialogueData sequence.</summary>
    public void Play(DialogueData data)
    {
        if (data == null || data.Lines.Count == 0) return;
        if (playerMadness != null && playerMadness.IsDead) return;

        StopDialogue();
        currentRoutine = StartCoroutine(PlayRoutine(data));
    }

    /// <summary>Stops the current dialogue immediately.</summary>
    public void StopDialogue()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }
        if (voiceSource != null && voiceSource.isPlaying) voiceSource.Stop();
        if (panelGroup != null) panelGroup.alpha = 0f;
        RestoreMovement();
    }

    IEnumerator PlayRoutine(DialogueData data)
    {
        if (data.BlockMovement && playerMovement != null)
        {
            playerMovement.enabled = false;
            movementBlocked = true;
        }

        for (int i = 0; i < data.Lines.Count; i++)
        {
            DialogueData.Line line = data.Lines[i];
            skipRequested = false;

            // Update UI texts
            if (speakerText != null)
            {
                bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speakerName);
                speakerText.gameObject.SetActive(hasSpeaker);
                speakerText.text = line.speakerName;
            }
            if (dialogueText != null) dialogueText.text = line.text;

            // Fade in on first line
            if (i == 0) yield return FadeTo(1f);

            // Play voice clip (pitchVariation = 0f so voices are not pitched randomly)
            PlayVoice(line);

            // Wait for duration or skip
            float duration = data.GetDuration(i);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!MenuUtility.AnyMenuOpen)
                {
                    if (data.CanSkip && skipRequested) break;
                    elapsed += Time.deltaTime;
                }
                yield return null;
            }

            if (voiceSource.isPlaying) voiceSource.Stop();
        }

        yield return FadeTo(0f);
        RestoreMovement();
        currentRoutine = null;
    }

    void PlayVoice(DialogueData.Line line)
    {
        if (!string.IsNullOrWhiteSpace(line.voiceClipName))
        {
            AudioManager.Instance.PlaySFX(line.voiceClipName, 1f, 0f);
        }
        else if (line.voiceClip != null)
        {
            voiceSource.volume = GameSettings.EffectiveSfxVolume;
            voiceSource.PlayOneShot(line.voiceClip);
        }
    }

    IEnumerator FadeTo(float targetAlpha)
    {
        if (panelGroup == null) yield break;
        float start = panelGroup.alpha;
        for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            panelGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeSeconds);
            yield return null;
        }
        panelGroup.alpha = targetAlpha;
    }

    void RestoreMovement()
    {
        if (movementBlocked && playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        movementBlocked = false;
    }
}