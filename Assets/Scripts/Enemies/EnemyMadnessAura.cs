using UnityEngine;

/// <summary>
/// Sanity losing aura (GDD): while the player is within the radius, their madness rises
/// (PlayerMadness drain, never kills). Put on any enemy. The sphere shows in the Scene view.
/// </summary>
public class EnemyMadnessAura : MonoBehaviour
{
    [Tooltip("Leave empty to find the player automatically.")]
    [SerializeField] PlayerMadness playerMadness;
    [Tooltip("Aura on/off (can be changed in Play Mode).")]
    [SerializeField] bool auraEnabled = true;
    [Tooltip("Aura radius in metres.")]
    [SerializeField, Min(0f)] float radius = 8f;
    [Tooltip("How fast madness rises inside the aura. 1 = PlayerMadness depletion rate (5 per second by default), 0.5 = half.")]
    [SerializeField, Min(0f)] float drainMultiplier = 1f;
    [Tooltip("On = the aura works only when nothing blocks the line between the enemy and the player.")]
    [SerializeField] bool needsLineOfSight = false;
    [Tooltip("Height of the aura centre above the enemy's feet.")]
    [SerializeField] float centreHeight = 1.2f;
    [SerializeField] Color gizmoColor = new Color(0.6f, 0f, 0.8f, 0.8f);

    [Header("Debug (live value)")]
    [SerializeField] bool playerInside;

    public bool PlayerInside => playerInside;
    public float Radius { get => radius; set => radius = Mathf.Max(0f, value); }
    public bool AuraEnabled { get => auraEnabled; set => auraEnabled = value; }

    PlayerMadness madness;
    float appliedMultiplier = -1f;

    void Start()
    {
        madness = playerMadness != null ? playerMadness : FindFirstObjectByType<PlayerMadness>();
    }

    void Update()
    {
        if (madness == null) return;
        Vector3 centre = transform.position + Vector3.up * centreHeight;
        Vector3 target = madness.transform.position + Vector3.up * 1f;
        bool inside = auraEnabled && !madness.IsDead && (target - centre).sqrMagnitude <= radius * radius;
        if (inside && needsLineOfSight) inside = !Blocked(centre, target);

        if (inside && (!playerInside || appliedMultiplier != drainMultiplier))
        {
            madness.BeginDrain(this, drainMultiplier);
            appliedMultiplier = drainMultiplier;
        }
        else if (!inside && playerInside)
        {
            madness.EndDrain(this);
            appliedMultiplier = -1f;
        }
        playerInside = inside;
    }

    bool Blocked(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.01f) return false;
        if (!Physics.Raycast(from, dir / dist, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore)) return false;
        return !hit.transform.IsChildOf(madness.transform) && !hit.transform.IsChildOf(transform);
    }

    void OnDisable()
    {
        if (playerInside && madness != null) madness.EndDrain(this);
        playerInside = false;
        appliedMultiplier = -1f;
    }

    void OnDrawGizmos()
    {
        if (!auraEnabled) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * centreHeight, radius);
    }
}
