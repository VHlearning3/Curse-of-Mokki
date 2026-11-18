using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Madness = player HP (0 sane, 100 insane). A hit at or above the death threshold kills; drains add madness
/// but never kill; sanity recovers passively after a delay.
/// </summary>
[DisallowMultipleComponent]
public class PlayerMadness : MonoBehaviour
{
    public const float MaxMadness = 100f;

    [Header("Madness")]
    [Tooltip("Madness when the scene starts and after ResetMadness(). 0 = fully sane.")]
    [SerializeField, Range(0f, MaxMadness)] float startMadness = 0f;

    [Tooltip("If the player gets hit while madness is at or above this (percent), the player dies.")]
    [SerializeField, Range(0f, 100f)] float deathThresholdPercent = 80f;

    [Tooltip("Depletion rate: madness added per second by each active drain source (enemy aura, hiding place...).")]
    [SerializeField, Min(0f)] float depletionRate = 5f;

    [Tooltip("Madness added by TakeHit() when the caller doesn't give an amount.")]
    [SerializeField, Min(0f)] float defaultHitAmount = 25f;

    [Header("Passive sanity recovery")]
    [Tooltip("Gain rate: sanity recovered per second (madness removed per second).")]
    [SerializeField, Min(0f)] float passiveGainRate = 0.5f;

    [Tooltip("Seconds since madness last went up before passive recovery starts.")]
    [SerializeField, Min(0f)] float recoveryDelay = 3f;

    [Header("Debug (live value)")]
    [Tooltip("Current madness. Shown for debugging; editing it in Play Mode changes the value.")]
    [SerializeField, Range(0f, MaxMadness)] float currentMadness;

    // --- Events -------------------------------------------------------------

    /// <summary>Madness changed. Argument: madness 0..1.</summary>
    public event Action<float> OnMadnessChanged;

    /// <summary>Crossed the death threshold. Argument: true = now at or above it.</summary>
    public event Action<bool> OnDangerZoneChanged;

    /// <summary>The player died (hit while in the danger zone).</summary>
    public event Action OnDied;

    // --- Read-only state ----------------------------------------------------

    public float Madness => currentMadness;
    public float Madness01 => currentMadness / MaxMadness;
    public float Sanity => MaxMadness - currentMadness;
    public float DeathThresholdPercent => deathThresholdPercent;
    /// <summary>True when a hit now would kill the player.</summary>
    public bool IsInDangerZone => Madness01 * 100f >= deathThresholdPercent;
    /// <summary>Madness is at 100 (GDD: sneaking doesn't help when fully insane).</summary>
    public bool IsFullyInsane => currentMadness >= MaxMadness;
    public bool IsDead { get; private set; }
    public bool IsDraining => drainSources.Count > 0;

    // --- Internal state -----------------------------------------------------

    readonly Dictionary<object, float> drainSources = new Dictionary<object, float>();
    readonly List<object> staleSources = new List<object>();

    struct TimedRecovery { public float remaining; public float perSecond; }
    readonly List<TimedRecovery> timedRecoveries = new List<TimedRecovery>();

    float timeSinceLastGain;
    bool wasInDangerZone;
    float lastNotifiedMadness;

    void Awake()
    {
        currentMadness = Mathf.Clamp(startMadness, 0f, MaxMadness);
        lastNotifiedMadness = currentMadness;
        wasInDangerZone = IsInDangerZone;
        timeSinceLastGain = recoveryDelay; // recovery may start right away if we spawn with madness
    }

    void Update()
    {
        Step(Time.deltaTime); // Time.deltaTime is 0 when the game is paused (timeScale 0)
    }

    void Step(float dt)
    {
        if (IsDead || dt <= 0f) return;

        RemoveDestroyedSources();

        float delta = 0f;

        // Depletion from drain sources
        float drainMultiplier = 0f;
        foreach (float m in drainSources.Values) drainMultiplier += m;

        if (drainMultiplier > 0f && depletionRate > 0f)
        {
            delta += depletionRate * drainMultiplier * dt;
            timeSinceLastGain = 0f;
        }
        else
        {
            timeSinceLastGain += dt;
        }

        // Timed recoveries (e.g. pills)
        for (int i = timedRecoveries.Count - 1; i >= 0; i--)
        {
            TimedRecovery r = timedRecoveries[i];
            float amount = Mathf.Min(r.remaining, r.perSecond * dt);
            delta -= amount;
            r.remaining -= amount;
            if (r.remaining <= 0.0001f) timedRecoveries.RemoveAt(i);
            else timedRecoveries[i] = r;
        }

        // Passive sanity recovery, after the delay
        if (!IsDraining && timeSinceLastGain >= recoveryDelay)
            delta -= passiveGainRate * dt;

        if (delta != 0f) SetMadnessInternal(currentMadness + delta);
    }

    // --- Public API ---------------------------------------------------------

    /// <summary>Enemy hit: kills at or above the death threshold, otherwise adds madness. True = killed.</summary>
    public bool TakeHit(float madnessAmount)
    {
        if (IsDead) return true;
        if (IsInDangerZone)
        {
            Die();
            return true;
        }
        AddMadness(madnessAmount);
        return false;
    }

    /// <summary>Enemy hit with the default amount set in the Inspector.</summary>
    public bool TakeHit() => TakeHit(defaultHitAmount);

    /// <summary>Adds madness instantly (scripted moments, cutscenes). Never kills.</summary>
    public void AddMadness(float amount)
    {
        if (IsDead || amount <= 0f) return;
        timeSinceLastGain = 0f;
        SetMadnessInternal(currentMadness + amount);
    }

    /// <summary>Removes madness instantly.</summary>
    public void ReduceMadness(float amount)
    {
        if (IsDead || amount <= 0f) return;
        SetMadnessInternal(currentMadness - amount);
    }

    /// <summary>Removes madness gradually, e.g. pills: RecoverOverTime(40, 7).</summary>
    public void RecoverOverTime(float amount, float duration)
    {
        if (IsDead || amount <= 0f) return;
        if (duration <= 0f) { ReduceMadness(amount); return; }
        timedRecoveries.Add(new TimedRecovery { remaining = amount, perSecond = amount / duration });
    }

    /// <summary>Starts a drain (enemy aura, hiding place). Stop it with EndDrain(same source).</summary>
    public void BeginDrain(object source, float multiplier = 1f)
    {
        if (source == null) return;
        drainSources[source] = Mathf.Max(0f, multiplier);
    }

    public void EndDrain(object source)
    {
        if (source == null) return;
        drainSources.Remove(source);
    }

    /// <summary>Sets madness directly (cutscenes, loading a save). Counts as a gain if it goes up.</summary>
    public void SetMadness(float value)
    {
        if (IsDead) return;
        if (value > currentMadness) timeSinceLastGain = 0f;
        SetMadnessInternal(value);
    }

    /// <summary>Respawn / restart: clears death, drains and timed recoveries.</summary>
    public void ResetMadness() => ResetMadness(startMadness);

    public void ResetMadness(float value)
    {
        IsDead = false;
        drainSources.Clear();
        timedRecoveries.Clear();
        timeSinceLastGain = recoveryDelay;
        SetMadnessInternal(value);
    }

    // --- Helpers ------------------------------------------------------------

    void SetMadnessInternal(float value)
    {
        value = Mathf.Clamp(value, 0f, MaxMadness);
        if (Mathf.Approximately(value, currentMadness) && Mathf.Approximately(value, lastNotifiedMadness)) return;

        currentMadness = value;
        lastNotifiedMadness = value;
        OnMadnessChanged?.Invoke(Madness01);

        bool inDanger = IsInDangerZone;
        if (inDanger != wasInDangerZone)
        {
            wasInDangerZone = inDanger;
            OnDangerZoneChanged?.Invoke(inDanger);
        }
    }

    void Die()
    {
        IsDead = true;
        drainSources.Clear();
        timedRecoveries.Clear();
        Debug.Log($"[PlayerMadness] Player died (hit at {currentMadness:0.#} madness, threshold {deathThresholdPercent:0.#} %).", this);
        OnDied?.Invoke();
    }

    void RemoveDestroyedSources()
    {
        if (drainSources.Count == 0) return;
        staleSources.Clear();
        foreach (object s in drainSources.Keys)
            if (s is UnityEngine.Object o && o == null) staleSources.Add(s);
        for (int i = 0; i < staleSources.Count; i++) drainSources.Remove(staleSources[i]);
    }

    void OnValidate()
    {
        // Keep the live value valid when edited in the Inspector during Play Mode
        if (Application.isPlaying) SetMadnessInternal(currentMadness);
    }
}
