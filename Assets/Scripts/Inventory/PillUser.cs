using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Use pills (GDD: Q, recover 30-50 sanity over 5-10 seconds). Takes one pill from PlayerInventory and
/// calls PlayerMadness.RecoverOverTime with a random amount and duration from the ranges below.
/// Put on the player next to PlayerInventory and PlayerMadness.
/// The key is read directly for now; see the Q-key question to Otto in Claude log.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class PillUser : MonoBehaviour
{
    [Tooltip("The pill item (ITEM_Pills).")]
    [SerializeField] ItemData pillItem;

    [Tooltip("Leave empty to use PlayerMadness on this object, or the first one in the scene.")]
    [SerializeField] PlayerMadness madness;

    [SerializeField] Key useKey = Key.Q;

    [Header("Effect (GDD: 30-50 sanity over 5-10 s)")]
    [Tooltip("Sanity recovered per pill, random between X and Y.")]
    [SerializeField] Vector2 recoverAmount = new Vector2(30f, 50f);
    [Tooltip("Seconds the recovery takes, random between X and Y.")]
    [SerializeField] Vector2 recoverDuration = new Vector2(5f, 10f);
    [Tooltip("Seconds before another pill can be taken.")]
    [SerializeField, Min(0f)] float cooldown = 1f;

    [Header("Overdose")]
    [SerializeField] bool enableOverdose = false;
    [Tooltip("Taking this many pills within the window counts as an overdose.")]
    [SerializeField, Min(2)] int overdosePills = 3;
    [SerializeField, Min(1f)] float overdoseWindowSeconds = 20f;
    [Tooltip("Madness added on an overdose.")]
    [SerializeField, Min(0f)] float overdoseMadness = 30f;

    /// <summary>A pill was taken. Arguments: sanity amount, duration.</summary>
    public event Action<float, float> OnPillTaken;
    public event Action OnOverdose;

    PlayerInventory inventory;
    float nextAllowedTime;
    readonly Queue<float> recentPills = new Queue<float>();

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        if (madness == null) madness = GetComponent<PlayerMadness>();
        if (madness == null) madness = FindFirstObjectByType<PlayerMadness>();
        if (pillItem == null) Debug.LogWarning("[PillUser] Pill item not set.", this);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || useKey == Key.None || Time.deltaTime <= 0f) return; // not while paused
        if (kb[useKey].wasPressedThisFrame) TryUsePill();
    }

    /// <summary>Takes one pill if the player has one. Returns true if a pill was taken.</summary>
    public bool TryUsePill()
    {
        if (pillItem == null || madness == null || madness.IsDead) return false;
        if (Time.time < nextAllowedTime) return false;
        if (!inventory.Remove(pillItem, 1)) return false;

        nextAllowedTime = Time.time + cooldown;

        float amount = UnityEngine.Random.Range(Mathf.Min(recoverAmount.x, recoverAmount.y), Mathf.Max(recoverAmount.x, recoverAmount.y));
        float duration = UnityEngine.Random.Range(Mathf.Min(recoverDuration.x, recoverDuration.y), Mathf.Max(recoverDuration.x, recoverDuration.y));
        madness.RecoverOverTime(amount, duration);
        OnPillTaken?.Invoke(amount, duration);

        if (enableOverdose)
        {
            recentPills.Enqueue(Time.time);
            while (recentPills.Count > 0 && Time.time - recentPills.Peek() > overdoseWindowSeconds) recentPills.Dequeue();
            if (recentPills.Count >= overdosePills)
            {
                recentPills.Clear();
                madness.AddMadness(overdoseMadness);
                OnOverdose?.Invoke();
            }
        }
        return true;
    }
}
