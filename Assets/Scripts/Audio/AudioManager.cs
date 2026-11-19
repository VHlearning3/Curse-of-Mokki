using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays every sound in the game. No scene setup: it creates itself the first time
/// a script uses AudioManager.Instance, and it loads every AudioClip that sits in a
/// Resources folder (put sound files in Assets/Audio/Resources).
///
/// Clips are found by their asset name from the GDD ("SFX_WalkSnow"). Numbered alt
/// versions ("SFX_WalkSnow2") are found automatically and one is picked at random.
/// Volumes and on/off come from GameSettings (the settings menu).
///
///   AudioManager.Instance.PlaySFX("SFX_Pills");
///   AudioManager.Instance.PlaySFXAt("SFX_StoneThrown", hitPoint);
///   AudioManager.Instance.PlayMusic("SFX_MUSChase");
///   AudioManager.Instance.PlayAmbience("SFX_AMBForest");
/// </summary>
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    private static bool quitting;

    /// <summary>The one audio manager. Created on first use. Null only while the game is closing.</summary>
    public static AudioManager Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                instance = FindFirstObjectByType<AudioManager>();
                if (instance == null)
                {
                    instance = new GameObject("AudioManager").AddComponent<AudioManager>();
                }
                instance.Initialize();
            }
            return instance;
        }
    }

    [Header("Clips")]
    [Tooltip("Optional. Clips in a Resources folder are loaded by themselves; add clips here only if they live somewhere else.")]
    [SerializeField] private List<AudioClip> extraClips = new List<AudioClip>();

    [Header("Tuning")]
    [Tooltip("Seconds a music or ambience change takes to fade.")]
    [SerializeField] private float fadeTime = 1.5f;
    [Tooltip("Random pitch change on each SFX so repeated steps don't sound identical. 0 = off.")]
    [Range(0f, 0.3f)] [SerializeField] private float sfxPitchVariation = 0.05f;
    [Tooltip("Distance in meters where a 3D sound is no longer heard.")]
    [SerializeField] private float sfx3DMaxDistance = 25f;

    // Clip name -> the clip and its numbered alt versions.
    private readonly Dictionary<string, List<AudioClip>> library = new Dictionary<string, List<AudioClip>>();
    private readonly HashSet<string> warnedMissing = new HashSet<string>();
    // Sources in the middle of a fade; the fade drives their volume.
    private readonly HashSet<AudioSource> fading = new HashSet<AudioSource>();

    private AudioSource sfxSource;      // 2D one-shots (UI, the player's own sounds)
    private AudioSource musicSource;    // looping music
    private AudioSource ambienceSource; // looping ambience, follows the SFX volume
    private Coroutine musicFade;
    private Coroutine ambienceFade;
    private bool initialized;

    // ---------- Setup ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
    }

    private static void OnQuitting()
    {
        quitting = true;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }
        initialized = true;

        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        sfxSource = CreateSource("SFX", false);
        musicSource = CreateSource("Music", true);
        ambienceSource = CreateSource("Ambience", true);

        BuildLibrary();

        GameSettings.OnChanged += ApplyVolumes;
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        if (initialized)
        {
            GameSettings.OnChanged -= ApplyVolumes;
        }
        if (instance == this)
        {
            instance = null;
        }
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void BuildLibrary()
    {
        library.Clear();
        foreach (var clip in Resources.LoadAll<AudioClip>(""))
        {
            AddToLibrary(clip);
        }
        foreach (var clip in extraClips)
        {
            AddToLibrary(clip);
        }
    }

    private void AddToLibrary(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        // "SFX_WalkSnow2" is stored under "SFX_WalkSnow" as an alt version.
        string key = clip.name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (key.Length == 0)
        {
            key = clip.name;
        }

        if (!library.TryGetValue(key, out var list))
        {
            list = new List<AudioClip>();
            library.Add(key, list);
        }
        if (!list.Contains(clip))
        {
            list.Add(clip);
        }
    }

    private AudioClip GetClip(string clipName)
    {
        if (library.TryGetValue(clipName, out var list) && list.Count > 0)
        {
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        // Warn once per name, so a missing footstep doesn't flood the Console.
        if (warnedMissing.Add(clipName))
        {
            Debug.LogWarning($"AudioManager: no clip named '{clipName}'. Put the sound file in Assets/Audio/Resources.", this);
        }
        return null;
    }

    /// <summary>True if a clip with this name was found.</summary>
    public bool HasClip(string clipName) => library.ContainsKey(clipName);

    // ---------- Sound effects ----------

    /// <summary>Plays a sound that is not placed in the world (UI, the player's own sounds).</summary>
    public void PlaySFX(string clipName, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null)
        {
            return;
        }

        sfxSource.pitch = 1f + UnityEngine.Random.Range(-sfxPitchVariation, sfxPitchVariation);
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>Plays a sound at a point in the world, so it is heard from that direction.</summary>
    public void PlaySFXAt(string clipName, Vector3 position, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null)
        {
            return;
        }

        var temp = new GameObject("SFX_" + clip.name);
        temp.transform.position = position;
        var source = temp.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = sfx3DMaxDistance;
        source.pitch = 1f + UnityEngine.Random.Range(-sfxPitchVariation, sfxPitchVariation);
        source.volume = GameSettings.EffectiveSfxVolume * volumeScale;
        source.Play();
        Destroy(temp, clip.length / Mathf.Max(0.01f, source.pitch) + 0.1f);
    }

    // ---------- Music and ambience ----------

    /// <summary>Fades to a new looping music track. Does nothing if it is already playing.</summary>
    public void PlayMusic(string clipName)
    {
        StartFade(ref musicFade, musicSource, GetClip(clipName), () => GameSettings.EffectiveMusicVolume);
    }

    public void StopMusic()
    {
        StartFade(ref musicFade, musicSource, null, () => GameSettings.EffectiveMusicVolume);
    }

    /// <summary>Fades to a new looping ambience. Ambience follows the SFX volume.</summary>
    public void PlayAmbience(string clipName)
    {
        StartFade(ref ambienceFade, ambienceSource, GetClip(clipName), () => GameSettings.EffectiveSfxVolume);
    }

    public void StopAmbience()
    {
        StartFade(ref ambienceFade, ambienceSource, null, () => GameSettings.EffectiveSfxVolume);
    }

    private void StartFade(ref Coroutine running, AudioSource source, AudioClip next, Func<float> targetVolume)
    {
        if (next != null && source.clip == next && source.isPlaying)
        {
            return;
        }

        if (running != null)
        {
            StopCoroutine(running);
        }
        running = StartCoroutine(FadeTo(source, next, targetVolume));
    }

    private IEnumerator FadeTo(AudioSource source, AudioClip next, Func<float> targetVolume)
    {
        fading.Add(source);

        // Fade the old track out. Unscaled time, so it also works while the game is paused.
        if (source.isPlaying && fadeTime > 0f)
        {
            float start = source.volume;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, 0f, t / fadeTime);
                yield return null;
            }
        }

        source.Stop();
        source.clip = next;
        if (next == null)
        {
            source.volume = targetVolume();
            fading.Remove(source);
            yield break;
        }

        // Fade the new track in.
        source.volume = 0f;
        source.Play();
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(0f, targetVolume(), t / fadeTime);
            yield return null;
        }
        source.volume = targetVolume();
        fading.Remove(source);
    }

    // ---------- Volume ----------

    // Runs at start and every time the settings menu changes something.
    private void ApplyVolumes()
    {
        if (sfxSource == null)
        {
            return;
        }

        sfxSource.volume = GameSettings.EffectiveSfxVolume;
        // Don't fight a running fade; it reads the new value itself.
        if (!fading.Contains(musicSource))
        {
            musicSource.volume = GameSettings.EffectiveMusicVolume;
        }
        if (!fading.Contains(ambienceSource))
        {
            ambienceSource.volume = GameSettings.EffectiveSfxVolume;
        }
    }
}
