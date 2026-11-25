using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Save point (GDD): activated when the player walks into its area; on death, Continue respawns
/// the player here. Prefab: Assets/Art/Prefabs/SavePoints/SavePoint.prefab — drag it into the scene,
/// scale the box to cover the path, and move the child "SpawnPoint" to where the player should stand
/// (its blue arrow = the way the player looks after respawning).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SavePoint : MonoBehaviour
{
    [Header("Respawn")]
    [Tooltip("Where the player appears (put it on the ground; its forward = look direction). Empty = this object.")]
    [SerializeField] Transform spawnPoint;
    [Tooltip("Madness after respawning here (0 = fully sane).")]
    [SerializeField, Range(0f, 100f)] float respawnMadness = 0f;
    [Tooltip("Place the player's feet on the ground below the spawn point.")]
    [SerializeField] bool snapToGround = true;

    [Header("Activation")]
    [Tooltip("Active when the scene starts (e.g. the first save point of the level).")]
    [SerializeField] bool activeAtStart = false;
    [Tooltip("Order along the level: higher = later. Used by Only Forward.")]
    [SerializeField] int order = 0;
    [Tooltip("On = walking back into an earlier save point (lower Order) does not move the respawn back.")]
    [SerializeField] bool onlyForward = true;

    [Header("Feedback (optional)")]
    [Tooltip("AudioManager clip name played on activation (empty = none).")]
    [SerializeField] string activateSound = "";
    [Tooltip("Called when this save point becomes the active one.")]
    public UnityEvent onActivated;
    [Tooltip("Called when the player respawns here.")]
    public UnityEvent onRespawned;

    [Header("Debug (live)")]
    [SerializeField] bool isActive;

    public int Order => order;
    public bool OnlyForward => onlyForward;
    public float RespawnMadness => respawnMadness;
    public bool IsActive => isActive;
    public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 3f, 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
    }

    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    void Start()
    {
        if (activeAtStart) SavePointSystem.Activate(this, force: true);
    }

    void OnTriggerEnter(Collider other)
    {
        if (MenuUtility.AnyMenuOpen) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        var madness = other.GetComponentInParent<PlayerMadness>();
        if (madness != null && madness.IsDead) return;
        SavePointSystem.Activate(this);
    }

    void OnDestroy()
    {
        if (SavePointSystem.Active == this) SavePointSystem.Clear();
    }

    /// <summary>Where and how the player should stand after respawning.</summary>
    public void GetSpawnPose(Transform player, out Vector3 position, out Quaternion rotation)
    {
        Transform spawn = SpawnPoint;
        Vector3 forward = Vector3.ProjectOnPlane(spawn.forward, Vector3.up);
        rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : player.rotation;

        // The player's pivot is the middle of its CharacterController, not its feet.
        var controller = player.GetComponent<CharacterController>();
        float feetToPivot = controller != null ? controller.height * 0.5f - controller.center.y + controller.skinWidth : 1f;

        Vector3 ground = spawn.position;
        if (snapToGround)
        {
            var hits = Physics.RaycastAll(spawn.position + Vector3.up * 1.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player)) continue;
                if (hit.distance < best) { best = hit.distance; ground = hit.point; }
            }
        }
        position = ground + Vector3.up * (feetToPivot + 0.05f);
    }

    internal void NotifyActivated()
    {
        isActive = true;
        if (!string.IsNullOrEmpty(activateSound)) AudioManager.Instance.PlaySFX(activateSound);
        onActivated?.Invoke();
    }

    internal void NotifyDeactivated() => isActive = false;

    internal void NotifyRespawned() => onRespawned?.Invoke();

    [ContextMenu("Test: activate (Play Mode)")]
    void TestActivate()
    {
        if (Application.isPlaying) SavePointSystem.Activate(this, force: true);
    }

    [ContextMenu("Test: activate and respawn here (Play Mode)")]
    void TestRespawn()
    {
        if (!Application.isPlaying) return;
        SavePointSystem.Activate(this, force: true);
        SavePointSystem.Respawn();
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        Color c = isActive ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.85f, 0.1f);
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = c;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        Transform spawn = SpawnPoint;
        Vector3 p = spawn.position;
        Gizmos.color = c;
        Gizmos.DrawWireSphere(p + Vector3.up * 0.1f, 0.4f);
        Gizmos.DrawLine(p, p + Vector3.up * 1.8f);
        Gizmos.color = new Color(0.2f, 0.5f, 1f);
        Vector3 f = Vector3.ProjectOnPlane(spawn.forward, Vector3.up).normalized;
        Vector3 tip = p + Vector3.up * 0.1f + f * 1.2f;
        Gizmos.DrawLine(p + Vector3.up * 0.1f, tip);
        Gizmos.DrawLine(tip, tip - f * 0.35f + Vector3.Cross(Vector3.up, f) * 0.25f);
        Gizmos.DrawLine(tip, tip - f * 0.35f - Vector3.Cross(Vector3.up, f) * 0.25f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(p + Vector3.up * 2.1f, name + "  (order " + order + (isActive ? ", ACTIVE" : "") + ")");
#endif
    }
}
