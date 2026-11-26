using UnityEngine;

/// <summary>
/// Invisible box that makes the friend talk when the player walks in (for now; later the
/// interaction key may start talks too). Put the FriendTalkZone prefab next to the friend,
/// resize the box, and set Friend + Dialogue. Box is invisible in the game, orange in the Scene view.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FriendTalkZone : MonoBehaviour
{
    [Tooltip("The friend who talks. Empty = the first FriendNPC in the scene.")]
    [SerializeField] FriendNPC friend;
    [Tooltip("What the friend says. Empty = the friend's Default Dialogue.")]
    [SerializeField] DialogueData dialogue;

    [Header("Trigger behaviour")]
    [Tooltip("On = talks only the first time the player enters.")]
    [SerializeField] bool onlyOnce = true;
    [Tooltip("If Only Once is off: seconds before the box can start a talk again.")]
    [SerializeField, Min(0f)] float cooldown = 10f;

    [Header("Debug")]
    [SerializeField] bool hasTriggered;

    float lastTriggerTime = float.NegativeInfinity;

    void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 2.5f, 4f);
        box.center = new Vector3(0f, 1.25f, 0f);
    }

    void Awake()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
        if (friend == null) friend = FindFirstObjectByType<FriendNPC>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (MenuUtility.AnyMenuOpen) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        TryTalk();
    }

    /// <summary>Starts the talk if the box rules allow it.</summary>
    public bool TryTalk()
    {
        if (friend == null) { Debug.LogWarning("[FriendTalkZone] No FriendNPC set or found.", this); return false; }
        if (onlyOnce && hasTriggered) return false;
        if (Time.time - lastTriggerTime < cooldown) return false;
        if (!friend.Talk(dialogue)) return false;
        hasTriggered = true;
        lastTriggerTime = Time.time;
        return true;
    }

    /// <summary>Lets the box start a talk again.</summary>
    public void ResetZone()
    {
        hasTriggered = false;
        lastTriggerTime = float.NegativeInfinity;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.55f, 0.1f, hasTriggered ? 0.05f : 0.18f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (friend != null)
        {
            Gizmos.DrawLine(transform.position + Vector3.up * box.center.y, friend.transform.position + Vector3.up * 1.6f);
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (box.center.y + box.size.y * 0.5f + 0.2f),
            "Talk: " + (dialogue != null ? dialogue.name : friend != null && friend.DefaultDialogue != null ? friend.DefaultDialogue.name + " (default)" : "—"));
#endif
    }
}
