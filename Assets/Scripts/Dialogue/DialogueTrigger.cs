using UnityEngine;

/// <summary>
/// Trigger zone that starts a DialogueData sequence when the player enters.
/// Put on an object with a Collider (Is Trigger = true).
/// </summary>
[RequireComponent(typeof(Collider))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] DialogueData dialogue;

    [Header("Trigger behaviour")]
    [Tooltip("On = plays only the first time the player enters.")]
    [SerializeField] bool onlyOnce = true;

    [Tooltip("If Only Once is off: seconds before the area can trigger again.")]
    [SerializeField, Min(0f)] float cooldown = 10f;

    [Tooltip("On = ignores trigger if another dialogue is already playing.")]
    [SerializeField] bool ignoreIfBusy = true;

    [Header("Debug")]
    [SerializeField] bool hasTriggered;

    float lastTriggerTime = float.NegativeInfinity;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (MenuUtility.AnyMenuOpen) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        TryTrigger();
    }

    public bool TryTrigger()
    {
        if (dialogue == null) return false;
        if (onlyOnce && hasTriggered) return false;
        if (Time.time - lastTriggerTime < cooldown) return false;

        DialogueManager manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[DialogueTrigger] No HUD_Dialogue / DialogueManager in the scene.", this);
            return false;
        }
        if (ignoreIfBusy && manager.IsPlaying) return false;

        hasTriggered = true;
        lastTriggerTime = Time.time;
        manager.Play(dialogue);
        return true;
    }

    /// <summary>Allows the zone to trigger again (e.g. on respawn).</summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
        lastTriggerTime = float.NegativeInfinity;
    }

    void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}