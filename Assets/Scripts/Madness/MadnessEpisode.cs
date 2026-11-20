using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Short madness attack (GDD 2.5 sanity lost moment): red screen + eyes in front of the camera for a few seconds.
/// Put on the player. Started by SanityLostZone, or call Play(). Visual only. Camera needs Post Processing on.
/// </summary>
public class MadnessEpisode : MonoBehaviour
{
    [Tooltip("Eyes particle system (SanityVFX). Leave empty to use the one in MadnessSanityVFX on this object.")]
    [SerializeField] ParticleSystem eyes;
    [Tooltip("Camera the eyes appear in front of. Leave empty to use the camera under the player (or Camera.main).")]
    [SerializeField] Camera viewCamera;

    [Header("Timing (a zone sets its own duration and strength)")]
    [Tooltip("Used when Play() is called without values.")]
    [SerializeField, Min(0.1f)] float defaultDuration = 4f;
    [Tooltip("Used when Play() is called without values. 1 = full effect.")]
    [SerializeField, Range(0f, 1f)] float defaultStrength = 1f;
    [Tooltip("Seconds the effect takes to come in.")]
    [SerializeField, Min(0f)] float fadeInSeconds = 0.4f;
    [Tooltip("Seconds the effect takes to go away at the end (inside the duration).")]
    [SerializeField, Min(0f)] float fadeOutSeconds = 1f;

    [Header("Red screen")]
    [SerializeField] bool enableRed = true;
    [Tooltip("Colour the whole image turns at full strength.")]
    [SerializeField] Color redColor = new Color(1f, 0.15f, 0.15f);
    [Tooltip("Dark red edges at full strength.")]
    [SerializeField, Range(0f, 1f)] float edgeIntensity = 0.6f;
    [SerializeField] Color edgeColor = new Color(0.4f, 0f, 0f);
    [SerializeField, Range(0.01f, 1f)] float edgeSmoothness = 0.8f;

    [Header("Eyes")]
    [SerializeField] bool enableEyes = true;
    [Tooltip("Eyes that appear per second at full strength.")]
    [SerializeField, Min(0f)] float eyesPerSecond = 25f;
    [Tooltip("Closest distance of an eye from the camera (metres).")]
    [SerializeField, Min(0.1f)] float eyeDistanceMin = 2f;
    [Tooltip("Farthest distance of an eye from the camera (metres).")]
    [SerializeField, Min(0.1f)] float eyeDistanceMax = 6f;
    [Tooltip("How far to the sides the eyes spread (degrees from the middle of the view).")]
    [SerializeField, Range(0f, 90f)] float eyeSpreadAngle = 45f;

    [Header("Debug (live values)")]
    [SerializeField] float currentWeight;
    [SerializeField] float timeLeft;

    public bool IsPlaying => timeLeft > 0f;
    public event Action OnEpisodeStarted;
    public event Action OnEpisodeEnded;

    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    ColorAdjustments colorAdjustments;
    float duration;
    float strength;
    float eyesToEmit;

    void Awake()
    {
        if (eyes == null)
        {
            var sanityVfx = GetComponent<MadnessSanityVFX>();
            if (sanityVfx != null) eyes = sanityVfx.Eyes;
        }
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>(true);
        if (viewCamera == null) viewCamera = Camera.main;

        var volumeObject = new GameObject("MadnessEpisodeVolume (runtime)");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 110f; // above MadnessVignette (100)
        volume.weight = 0f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "MadnessEpisodeProfile (runtime)";
        vignette = profile.Add<Vignette>(true);
        colorAdjustments = profile.Add<ColorAdjustments>(true);
        volume.sharedProfile = profile;
        ApplyLook();
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
    }

    /// <summary>Starts an attack with the default duration and strength.</summary>
    public void Play() => Play(defaultDuration, defaultStrength);

    /// <summary>Starts an attack. A new one during an attack restarts it.</summary>
    public void Play(float seconds, float strength01)
    {
        duration = Mathf.Max(0.1f, seconds);
        timeLeft = duration;
        strength = Mathf.Clamp01(strength01);
        eyesToEmit = 0f;
        if (eyes != null && !eyes.isPlaying) eyes.Play();
        OnEpisodeStarted?.Invoke();
    }

    /// <summary>Ends the attack at once.</summary>
    public void Stop()
    {
        if (!IsPlaying) return;
        timeLeft = 0f;
        currentWeight = 0f;
        if (volume != null) volume.weight = 0f;
        OnEpisodeEnded?.Invoke();
    }

    void Update()
    {
        Step(Time.deltaTime); // 0 while paused
    }

    void Step(float dt)
    {
        if (!IsPlaying) return;
        timeLeft -= dt;
        if (timeLeft < 0.001f) timeLeft = 0f;
        float elapsed = duration - timeLeft;
        float fadeIn = fadeInSeconds > 0f ? Mathf.Clamp01(elapsed / fadeInSeconds) : 1f;
        float fadeOut = fadeOutSeconds > 0f ? Mathf.Clamp01(timeLeft / fadeOutSeconds) : 1f;
        float envelope = fadeIn * fadeOut;

        ApplyLook();
        currentWeight = enableRed ? envelope * strength : 0f;
        volume.weight = currentWeight;

        if (enableEyes) EmitEyes(dt * envelope);

        if (timeLeft <= 0f)
        {
            currentWeight = 0f;
            volume.weight = 0f;
            OnEpisodeEnded?.Invoke();
        }
    }

    void ApplyLook()
    {
        vignette.color.Override(edgeColor);
        vignette.smoothness.Override(edgeSmoothness);
        vignette.intensity.Override(edgeIntensity);
        colorAdjustments.colorFilter.Override(redColor);
    }

    void EmitEyes(float dt)
    {
        if (eyes == null || viewCamera == null) return;
        eyesToEmit += eyesPerSecond * strength * dt;
        bool world = eyes.main.simulationSpace == ParticleSystemSimulationSpace.World;
        Transform cam = viewCamera.transform;
        while (eyesToEmit >= 1f)
        {
            eyesToEmit -= 1f;
            Quaternion turn = Quaternion.Euler(
                UnityEngine.Random.Range(-eyeSpreadAngle * 0.6f, eyeSpreadAngle * 0.6f),
                UnityEngine.Random.Range(-eyeSpreadAngle, eyeSpreadAngle), 0f);
            Vector3 position = cam.position + cam.rotation * turn * Vector3.forward
                * UnityEngine.Random.Range(eyeDistanceMin, Mathf.Max(eyeDistanceMin, eyeDistanceMax));

            var emit = new ParticleSystem.EmitParams
            {
                position = world ? position : eyes.transform.InverseTransformPoint(position),
                applyShapeToPosition = false,
                startLifetime = Mathf.Clamp(timeLeft, 0.5f, 6f) // eyes go away with the attack
            };
            eyes.Emit(emit, 1);
        }
    }
}
