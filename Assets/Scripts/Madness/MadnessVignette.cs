using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Red madness edges (own URP Volume): from 50 % madness, stronger in the danger zone and building up while
/// draining. The camera needs Post Processing on.
/// </summary>
public class MadnessVignette : MonoBehaviour
{
    [Tooltip("Leave empty to use PlayerMadness on this object, or the first one in the scene.")]
    [SerializeField] PlayerMadness madness;

    [Header("Effects on/off")]
    [Tooltip("Red edges of the screen.")]
    [SerializeField] bool enableEdges = true;
    [Tooltip("Red tint over the whole image in the danger zone.")]
    [SerializeField] bool enableTint = true;
    [Tooltip("Visual pulse of the edges in the danger zone.")]
    [SerializeField] bool enablePulse = true;

    [Header("Before danger zone")]
    [Tooltip("Effect starts when madness passes this percent (50 = sanity below half).")]
    [SerializeField, Range(0f, 100f)] float startPercent = 50f;
    [Tooltip("Edge intensity just before the death threshold.")]
    [SerializeField, Range(0f, 1f)] float preDangerIntensity = 0.3f;
    [Tooltip("How the edges grow between startPercent (0) and the death threshold (1).")]
    [SerializeField] AnimationCurve preDangerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Danger zone (madness >= death threshold)")]
    [Tooltip("Edge intensity when entering the danger zone.")]
    [SerializeField, Range(0f, 1f)] float dangerIntensity = 0.4f;
    [Tooltip("Extra intensity per second while in the danger zone AND a drain is active (enemy near, hiding place).")]
    [SerializeField, Min(0f)] float buildUpPerSecond = 0.02f;
    [Tooltip("How fast the build-up fades per second when no drain is active.")]
    [SerializeField, Min(0f)] float buildUpFadePerSecond = 0.05f;
    [Tooltip("Maximum edge intensity (before pulse).")]
    [SerializeField, Range(0f, 1f)] float maxIntensity = 0.6f;

    [Header("Pulse (visual)")]
    [SerializeField, Range(0f, 0.3f)] float pulseAmount = 0.05f;
    [Tooltip("Pulses per second.")]
    [SerializeField, Min(0f)] float pulseSpeed = 1.2f;

    [Header("Look")]
    [SerializeField] Color edgeColor = new Color(0.6f, 0f, 0f);
    [SerializeField, Range(0.01f, 1f)] float edgeSmoothness = 0.6f;
    [SerializeField] Color tintColor = new Color(1f, 0.55f, 0.55f);
    [Tooltip("Tint strength when the build-up is at its maximum. Half of this when just entering the danger zone.")]
    [SerializeField, Range(0f, 1f)] float maxTintStrength = 0.6f;
    [Tooltip("How fast the effect follows changes (per second). Higher = snappier.")]
    [SerializeField, Min(0.01f)] float smoothingSpeed = 0.5f;

    [Header("Debug (live values)")]
    [SerializeField] float currentEdgeIntensity;
    [SerializeField] float currentBuildUp;
    [SerializeField] float currentTint;

    Volume volume;
    VolumeProfile profile;
    Vignette vignette;
    ColorAdjustments colorAdjustments;
    float smoothedBase;
    float dangerWeight;
    float pulseTime;

    void Awake()
    {
        if (madness == null) madness = GetComponent<PlayerMadness>();
        if (madness == null) madness = FindFirstObjectByType<PlayerMadness>();
        if (madness == null) Debug.LogWarning("[MadnessVignette] No PlayerMadness found.", this);

        var volumeObject = new GameObject("MadnessVolume (runtime)");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "MadnessProfile (runtime)";
        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0f);
        colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.colorFilter.Override(Color.white);
        volume.sharedProfile = profile;
    }

    void OnDestroy()
    {
        if (profile != null) Destroy(profile);
    }

    void Update()
    {
        Step(Time.deltaTime); // 0 when paused
    }

    void Step(float dt)
    {
        if (madness == null || vignette == null) return;
        float threshold = Mathf.Max(madness.DeathThresholdPercent, startPercent + 0.01f);
        float percent = madness.Madness01 * 100f;
        bool inDanger = madness.IsInDangerZone;
        float buildUpMax = Mathf.Max(0f, maxIntensity - dangerIntensity);

        // Target from the madness value
        float baseTarget;
        if (madness.IsDead) baseTarget = maxIntensity;
        else if (inDanger) baseTarget = dangerIntensity;
        else if (percent <= startPercent) baseTarget = 0f;
        else baseTarget = preDangerCurve.Evaluate(Mathf.InverseLerp(startPercent, threshold, percent)) * preDangerIntensity;

        // Build-up only while in danger AND near a drain; fades otherwise
        if (madness.IsDead) currentBuildUp = 0f;
        else if (inDanger && madness.IsDraining) currentBuildUp += buildUpPerSecond * dt;
        else currentBuildUp -= buildUpFadePerSecond * dt;
        currentBuildUp = Mathf.Clamp(currentBuildUp, 0f, buildUpMax);

        smoothedBase = Mathf.MoveTowards(smoothedBase, baseTarget, smoothingSpeed * dt);
        dangerWeight = Mathf.MoveTowards(dangerWeight, inDanger || madness.IsDead ? 1f : 0f, smoothingSpeed * 2f * dt);

        // Visual pulse in the danger zone (added after smoothing so it isn't smoothed away)
        float pulse = 0f;
        if (enablePulse && !madness.IsDead && dangerWeight > 0f)
        {
            pulseTime += dt;
            pulse = Mathf.Sin(pulseTime * pulseSpeed * Mathf.PI * 2f) * pulseAmount * dangerWeight;
        }

        currentEdgeIntensity = Mathf.Clamp01(Mathf.Min(smoothedBase + currentBuildUp, maxIntensity) + pulse);

        float buildUp01 = buildUpMax > 0f ? currentBuildUp / buildUpMax : 0f;
        currentTint = madness.IsDead ? maxTintStrength : maxTintStrength * dangerWeight * Mathf.Lerp(0.5f, 1f, buildUp01);

        // Apply (on/off toggles work live in Play Mode)
        vignette.active = enableEdges;
        vignette.color.Override(edgeColor);
        vignette.smoothness.Override(edgeSmoothness);
        vignette.intensity.Override(currentEdgeIntensity);

        colorAdjustments.active = enableTint;
        colorAdjustments.colorFilter.Override(Color.Lerp(Color.white, tintColor, currentTint));
    }
}
