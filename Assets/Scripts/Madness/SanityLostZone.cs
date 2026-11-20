using System;
using UnityEngine;

/// <summary>
/// Circle area (GDD 2.5 sanity lost moment): when the player walks in, they get a madness attack
/// (MadnessEpisode: red screen + eyes). Place one at each point of interest (camp, statue/pillar).
/// The circle shows in the Scene view. Needs MadnessEpisode on the player.
/// </summary>
public class SanityLostZone : MonoBehaviour
{
    [Tooltip("Leave empty to find the player's MadnessEpisode automatically.")]
    [SerializeField] MadnessEpisode episode;

    [Header("Area")]
    [Tooltip("Radius of the circle in metres.")]
    [SerializeField, Min(0.1f)] float radius = 6f;
    [Tooltip("How far above or below the zone's centre the player still counts as inside (metres).")]
    [SerializeField, Min(0.1f)] float height = 4f;

    [Header("Attack")]
    [Tooltip("How long the attack lasts (seconds).")]
    [SerializeField, Min(0.1f)] float duration = 4f;
    [Tooltip("How strong the red screen and eyes are. 1 = full.")]
    [SerializeField, Range(0f, 1f)] float strength = 1f;
    [Tooltip("Madness added when the attack starts (0 = none). Never kills.")]
    [SerializeField, Min(0f)] float madnessAdded = 10f;

    [Header("Repeat")]
    [Tooltip("On = happens only the first time the player walks in.")]
    [SerializeField] bool onlyOnce = true;
    [Tooltip("If not only once: seconds before it can happen again.")]
    [SerializeField, Min(0f)] float cooldown = 30f;

    [Header("Point of interest")]
    [Tooltip("Optional: what the attack is about (camp, statue). Not used yet: the camera turning to it needs Otto's camera.")]
    [SerializeField] Transform focusPoint;

    [Header("Scene view")]
    [SerializeField] Color gizmoColor = new Color(1f, 0.1f, 0.1f, 0.9f);

    [Header("Debug (live value)")]
    [SerializeField] bool used;

    /// <summary>Fired when the attack starts (for example for a camera turn).</summary>
    public event Action<SanityLostZone> OnTriggered;
    public Transform FocusPoint => focusPoint;
    public float Duration => duration;

    PlayerMadness madness;
    Transform player;
    bool wasInside;
    float lastTriggerTime = float.NegativeInfinity;

    void Start()
    {
        if (episode == null) episode = FindFirstObjectByType<MadnessEpisode>();
        if (episode != null) madness = episode.GetComponent<PlayerMadness>();
        if (madness == null) madness = FindFirstObjectByType<PlayerMadness>();
        if (episode != null) player = episode.transform;
        else if (madness != null) player = madness.transform;
        if (episode == null) Debug.LogWarning("[SanityLostZone] No MadnessEpisode on the player — no attack will show.", this);
    }

    void Update()
    {
        if (player == null || MenuUtility.AnyMenuOpen) return;
        bool inside = IsInside(player.position);
        if (inside && !wasInside) TryTrigger();
        wasInside = inside;
    }

    /// <summary>True if the point is inside the circle (and within the height).</summary>
    public bool IsInside(Vector3 point)
    {
        Vector3 offset = point - transform.position;
        if (Mathf.Abs(offset.y) > height) return false;
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    /// <summary>Starts the attack if allowed (once / cooldown). Returns true if it started.</summary>
    public bool TryTrigger()
    {
        if (onlyOnce && used) return false;
        if (Time.time - lastTriggerTime < cooldown) return false;
        if (madness != null && madness.IsDead) return false;

        used = true;
        lastTriggerTime = Time.time;
        if (madness != null && madnessAdded > 0f) madness.AddMadness(madnessAdded);
        if (episode != null) episode.Play(duration, strength);
        OnTriggered?.Invoke(this);
        return true;
    }

    /// <summary>Lets the zone happen again (for example after a respawn).</summary>
    public void ResetZone()
    {
        used = false;
        lastTriggerTime = float.NegativeInfinity;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Vector3 centre = transform.position;
        const int segments = 48;
        Vector3 previous = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.DrawLine(centre + Vector3.down * height, centre + Vector3.up * height);
        if (focusPoint != null) Gizmos.DrawLine(centre, focusPoint.position);
    }
}
