using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One place that plays every sound in the game. Put it on an empty GameObject
/// in the first scene; it survives scene loads and removes duplicates.
///
/// Clips are looked up by their asset name from the GDD (for example
/// "SFX_WalkSnow"). Numbered alt versions ("SFX_WalkSnow2", "SFX_WalkSnow3")
/// are found automatically and one is picked at random on each play.
///
/// Usage from any script:
///   AudioManager.Instance.PlaySFX("SFX_Pills");
///   AudioManager.Instance.PlaySFXAt("SFX_StoneThrown", hitPoint);
///   AudioManager.Instance.PlayMusic("SFX_MUSChase");
///   AudioManager.Instance.PlayAmbience("SFX_AMBForest");
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips")]
    [Tooltip("Every clip the game can play. Drag the audio files from Assets/Audio here.")]
    [SerializeField] private List<AudioClip> clips = new List<AudioClip>();

    [Header("Volume")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private bool musicMuted;
    [SerializeField] private bool sfxMuted;

    [Header("Tuning")]
    [Tooltip("Seconds a music or ambience change takes to fade.")]
    [SerializeField] private float fadeTime = 1.5f;
    [Tooltip("Random pitch change on each SFX so repeated steps don't sound identical. 0 = off.")]
    [Range(0f, 0.3f)] [SerializeField] private float sfxPitchVariation = 0.05f;
    [Tooltip("Distance in meters where a 3D sound is no longer heard.")]
    [SerializeField] private float sfx3DMaxDistance = 25f;

    private const string MusicVolumeKey = "Audio_MusicVolume";
    private const string SfxVolumeKey = "Audio_SfxVolume";
    private const string MusicMutedKey = "Audio_MusicMuted";
    private const string SfxMutedKey = "Audio_SfxMuted";

    // Clip name -> the clip and its numbered alt versions.
    private readonly Dictionary<string, List<AudioClip>> library = new Dictionary<string, List<AudioClip>>();

    private AudioSource sfxSource;      // 2D one-shots (UI, player sounds)
    private AudioSource musicSource;    // looping music
    private AudioSource ambienceSource; // looping ambience
    private Coroutine musicFade;
    private Coroutine ambienceFade;
    // Sources that are in the middle of a fade; their volume is driven by the fade.
    private readonly HashSet<AudioSource> fading = new HashSet<AudioSource>();

    // ---------- Volume (used by the settings menu) ----------

    public float MusicVolume
    {
        get => musicVolume;
        set { musicVolume = Mathf.Clamp01(value); ApplyVolumes(); SaveSettings(); }
    }

    public float SFXVolume
    {
        get => sfxVolume;
        set { sfxVolume = Mathf.Clamp01(value); ApplyVolumes(); SaveSettings(); }
    }

    public bool MusicMuted
    {
        get => musicMuted;
        set { musicMuted = value; ApplyVolumes(); SaveSettings(); }
    }

    public bool SFXMuted
    {
        get => sfxMuted;
        set { sfxMuted = value; ApplyVolumes(); SaveSettings(); }
    }

    private float EffectiveMusicVolume => musicMuted ? 0f : musicVolume;
    private float EffectiveSfxVolume => sfxMuted ? 0f : sfxVolume;

    // ---------- Setup ----------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        sfxSource = CreateSource("SFX", false);
        musicSource = CreateSource("Music", true);
        ambienceSource = CreateSource("Ambience", true);

        BuildLibrary();
        LoadSettings();
        ApplyVolumes();
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
        foreach (var clip in clips)
        {
            if (clip == null) continue;

            // "SFX_WalkSnow2" is stored under "SFX_WalkSnow" as an alt version.
            string key = clip.name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            if (key.Length == 0) key = clip.name;

            if (!library.TryGetValue(key, out var list))
            {
                list = new List<AudioClip>();
                library.Add(key, list);
            }
            list.Add(clip);
        }
    }

    private AudioClip GetClip(string clipName)
    {
        if (library.TryGetValue(clipName, out var list) && list.Count > 0)
            return list[Random.Range(0, list.Count)];

        Debug.LogWarning($"AudioManager: no clip named '{clipName}'. Add it to the Clips list.", this);
        return null;
    }

    /// <summary>True if a clip with this name is in the Clips list.</summary>
    public bool HasClip(string clipName) => library.ContainsKey(clipName);

    // ---------- Sound effects ----------

    /// <summary>Plays a sound that is not placed in the world (UI, the player's own sounds).</summary>
    public void PlaySFX(string clipName, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null) return;

        sfxSource.pitch = 1f + Random.Range(-sfxPitchVariation, sfxPitchVariation);
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>Plays a sound at a point in the world, so it is heard from that direction.</summary>
    public void PlaySFXAt(string clipName, Vector3 position, float volumeScale = 1f)
    {
        var clip = GetClip(clipName);
        if (clip == null) return;

        var temp = new GameObject("SFX_" + clip.name);
        temp.transform.position = position;
        var source = temp.AddComponent<AudioSource>();
        source.clip = clip;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = sfx3DMaxDistance;
        source.pitch = 1f + Random.Range(-sfxPitchVariation, sfxPitchVariation);
        source.volume = EffectiveSfxVolume * volumeScale;
        source.Play();
        Destroy(temp, clip.length / Mathf.Max(0.01f, source.pitch) + 0.1f);
    }

    // ---------- Music and ambience ----------

    /// <summary>Fades to a new looping music track. Does nothing if it is already playing.</summary>
    public void PlayMusic(string clipName)
    {
        StartFade(ref musicFade, musicSource, GetClip(clipName), () => EffectiveMusicVolume);
    }

    public void StopMusic()
    {
        StartFade(ref musicFade, musicSource, null, () => EffectiveMusicVolume);
    }

    /// <summary>Fades to a new looping ambience. Ambience follows the SFX volume.</summary>
    public void PlayAmbience(string clipName)
    {
        StartFade(ref ambienceFade, ambienceSource, GetClip(clipName), () => EffectiveSfxVolume);
    }

    public void StopAmbience()
    {
        StartFade(ref ambienceFade, ambienceSource, null, () => EffectiveSfxVolume);
    }

    private void StartFade(ref Coroutine running, AudioSource source, AudioClip next, System.Func<float> targetVolume)
    {
        if (next != null && source.clip == next && source.isPlaying) return;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(FadeTo(source, next, targetVolume));
    }

    private IEnumerator FadeTo(AudioSource source, AudioClip next, System.Func<float> targetVolume)
    {
        fading.Add(source);

        // Fade the old track out. Unscaled time so it also works while the game is paused.
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Settings ----------

    private void ApplyVolumes()
    {
        if (sfxSource == null) return;

        sfxSource.volume = EffectiveSfxVolume;
        // Don't fight a fade that is running; it reads the new value itself.
        if (!fading.Contains(musicSource)) musicSource.volume = EffectiveMusicVolume;
        if (!fading.Contains(ambienceSource)) ambienceSource.volume = EffectiveSfxVolume;
    }

    private void LoadSettings()
    {
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
        musicMuted = PlayerPrefs.GetInt(MusicMutedKey, musicMuted ? 1 : 0) == 1;
        sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, sfxMuted ? 1 : 0) == 1;
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.SetInt(MusicMutedKey, musicMuted ? 1 : 0);
        PlayerPrefs.SetInt(SfxMutedKey, sfxMuted ? 1 : 0);
    }
}
