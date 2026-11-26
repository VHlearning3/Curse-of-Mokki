using System;
using UnityEngine;

/// <summary>
/// The player's friend (GDD "Kaveri"): talks at the start in the cottage, gets taken (cutscene 2),
/// is alive again in the ending and points out of the window (cutscene 5).
/// Prefab: Assets/Art/Prefabs/NPC/Friend.prefab. Talk starts from FriendTalkZone boxes (or Talk()).
/// Cutscenes drive it with WalkTo / WalkPath / LookAt / PointAt / Disappear / Appear / StopAndIdle.
/// The model is a child object; this root faces +Z.
/// </summary>
public class FriendNPC : MonoBehaviour, IRespawnResettable
{
    public enum State { Idle, Talking, Walking, Pointing, Gone }

    [Header("Talk")]
    [Tooltip("Dialogue used when a FriendTalkZone has no dialogue of its own (e.g. DLG_CottageIntro).")]
    [SerializeField] DialogueData defaultDialogue;
    [Tooltip("Turn to face the player while talking.")]
    [SerializeField] bool facePlayerWhileTalking = true;
    [Tooltip("Turn to face the player when they come this close while idle (0 = off).")]
    [SerializeField, Min(0f)] float facePlayerWhenNear = 3f;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] float walkSpeed = 1.4f;
    [Tooltip("Degrees per second.")]
    [SerializeField, Min(1f)] float turnSpeed = 240f;
    [Tooltip("Distance at which a walk point counts as reached.")]
    [SerializeField, Min(0.05f)] float arriveDistance = 0.2f;
    [Tooltip("Keep the feet on the ground (raycast down).")]
    [SerializeField] bool snapToGround = true;
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Animation (Animator state names; empty = Idle)")]
    [Tooltip("Leave empty to use the Animator on a child (the model).")]
    [SerializeField] Animator animator;
    [SerializeField] string idleState = "Idle";
    [SerializeField] string walkState = "Walk";
    [SerializeField] string talkState = "";
    [SerializeField] string pointState = "";
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.2f;

    [Header("Save points")]
    [Tooltip("On = when the player respawns at a save point, the friend goes back to where and how it was at scene start. Off (default) = the friend stays as it is (e.g. stays gone after being taken).")]
    [SerializeField] bool resetOnRespawn = false;

    [Header("Debug (live)")]
    [SerializeField] State state = State.Idle;

    public State CurrentState => state;
    public bool IsTalking => state == State.Talking;
    public DialogueData DefaultDialogue => defaultDialogue;

    /// <summary>Reached the end of WalkTo / WalkPath.</summary>
    public event Action OnArrived;
    public event Action OnTalkStarted;
    public event Action OnTalkEnded;

    Transform player;
    Vector3 startPosition;
    Quaternion startRotation;
    bool startActive;

    // walking
    Vector3 singleTarget;
    PatrolPath walkPath;
    int pathIndex;
    float targetYaw;
    bool hasTargetYaw;
    // looking / pointing
    Transform lookTarget;
    Vector3 lookPoint;
    bool hasLookPoint;
    int currentAnim;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startActive = gameObject.activeSelf;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        SavePointSystem.Register(this);
    }

    void OnDestroy() => SavePointSystem.Unregister(this);

    void Start()
    {
        FindPlayer();
        if (snapToGround) SnapToGround();
        PlayAnim(idleState);
    }

    void FindPlayer()
    {
        if (player != null) return;
        var movement = FindFirstObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    void Update()
    {
        if (MenuUtility.AnyMenuOpen) return;
        Step(Time.deltaTime);
    }

    /// <summary>One frame of behaviour (separate from Update so tests can step time).</summary>
    void Step(float dt)
    {
        if (player == null) FindPlayer();

        switch (state)
        {
            case State.Idle:
                PlayAnim(idleState);
                if (hasLookPoint || lookTarget != null) TurnTowards(LookPosition(), dt);
                else if (facePlayerWhenNear > 0f && player != null && FlatDistance(player.position) <= facePlayerWhenNear)
                    TurnTowards(player.position, dt);
                else if (hasTargetYaw) TurnToYaw(targetYaw, dt);
                break;

            case State.Talking:
                PlayAnim(string.IsNullOrEmpty(talkState) ? idleState : talkState);
                if (facePlayerWhileTalking && player != null) TurnTowards(player.position, dt);
                var manager = DialogueManager.Instance;
                if (manager == null || !manager.IsPlaying) EndTalk();
                break;

            case State.Walking:
                PlayAnim(walkState);
                StepWalk(dt);
                break;

            case State.Pointing:
                PlayAnim(string.IsNullOrEmpty(pointState) ? idleState : pointState);
                TurnTowards(LookPosition(), dt);
                break;
        }
    }

    // --- Talk ---------------------------------------------------------------

    /// <summary>Plays a dialogue (null = Default Dialogue) and faces the player while it runs.
    /// Returns false if there is nothing to play, no HUD_Dialogue in the scene, or (ignoreIfBusy) another dialogue runs.</summary>
    public bool Talk(DialogueData dialogue = null, bool ignoreIfBusy = true)
    {
        if (dialogue == null) dialogue = defaultDialogue;
        if (dialogue == null || state == State.Gone || !gameObject.activeInHierarchy) return false;
        var manager = DialogueManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[FriendNPC] No HUD_Dialogue / DialogueManager in the scene.", this);
            return false;
        }
        if (ignoreIfBusy && manager.IsPlaying) return false;

        walkPath = null;
        state = State.Talking;
        manager.Play(dialogue);
        if (!manager.IsPlaying) { state = State.Idle; return false; }
        OnTalkStarted?.Invoke();
        return true;
    }

    void EndTalk()
    {
        state = State.Idle;
        OnTalkEnded?.Invoke();
    }

    // --- Cutscene API ---------------------------------------------------------

    /// <summary>Walks straight to a point, then OnArrived.</summary>
    public void WalkTo(Vector3 position)
    {
        if (state == State.Gone) return;
        StopTalkIfTalking();
        walkPath = null;
        singleTarget = position;
        ClearLook();
        state = State.Walking;
    }

    /// <summary>Walks to a point and ends facing the same way as that object.</summary>
    public void WalkTo(Transform point)
    {
        if (point == null) return;
        WalkTo(point.position);
        targetYaw = point.eulerAngles.y;
        hasTargetYaw = true;
    }

    /// <summary>Walks through the children of a PatrolPath in order (first to last), then OnArrived.</summary>
    public void WalkPath(PatrolPath path)
    {
        if (path == null || path.Count == 0 || state == State.Gone) return;
        StopTalkIfTalking();
        walkPath = path;
        pathIndex = 0;
        ClearLook();
        state = State.Walking;
    }

    /// <summary>Stands still and turns to look at a target (keeps following it if it moves).</summary>
    public void LookAt(Transform target)
    {
        if (state == State.Gone) return;
        StopTalkIfTalking();
        ClearLook();
        lookTarget = target;
        state = State.Idle;
    }

    public void LookAt(Vector3 point)
    {
        if (state == State.Gone) return;
        StopTalkIfTalking();
        ClearLook();
        lookPoint = point;
        hasLookPoint = true;
        state = State.Idle;
    }

    /// <summary>Turns to the target and points at it (ending: points out of the window).
    /// Uses the Point State animation if set, otherwise just turns.</summary>
    public void PointAt(Transform target)
    {
        if (target == null || state == State.Gone) return;
        StopTalkIfTalking();
        ClearLook();
        lookTarget = target;
        state = State.Pointing;
    }

    public void PointAt(Vector3 point)
    {
        if (state == State.Gone) return;
        StopTalkIfTalking();
        ClearLook();
        lookPoint = point;
        hasLookPoint = true;
        state = State.Pointing;
    }

    /// <summary>Stops walking / pointing / looking and stands idle.</summary>
    public void StopAndIdle()
    {
        if (state == State.Gone) return;
        StopTalkIfTalking();
        walkPath = null;
        ClearLook();
        state = State.Idle;
    }

    /// <summary>The friend is taken (cutscene 2): hidden until Appear().</summary>
    public void Disappear()
    {
        StopTalkIfTalking();
        walkPath = null;
        ClearLook();
        state = State.Gone;
        gameObject.SetActive(false);
    }

    /// <summary>Shows the friend again, optionally at a place (facing that object's direction).</summary>
    public void Appear(Transform at = null)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (at != null)
        {
            transform.SetPositionAndRotation(at.position, Quaternion.Euler(0f, at.eulerAngles.y, 0f));
            if (snapToGround) SnapToGround();
        }
        walkPath = null;
        ClearLook();
        state = State.Idle;
        currentAnim = 0;
        PlayAnim(idleState);
    }

    public void ResetForRespawn()
    {
        if (!resetOnRespawn) return;
        if (gameObject.activeSelf != startActive) gameObject.SetActive(startActive);
        transform.SetPositionAndRotation(startPosition, startRotation);
        walkPath = null;
        ClearLook();
        state = State.Idle;
        currentAnim = 0;
    }

    // --- Helpers ------------------------------------------------------------

    void StopTalkIfTalking()
    {
        if (state != State.Talking) return;
        DialogueManager.Instance?.StopDialogue();
        EndTalk();
    }

    void ClearLook()
    {
        lookTarget = null;
        hasLookPoint = false;
        hasTargetYaw = false;
    }

    Vector3 LookPosition() => lookTarget != null ? lookTarget.position : lookPoint;

    void StepWalk(float dt)
    {
        Vector3 target = walkPath != null ? walkPath.GetPoint(pathIndex) : singleTarget;
        if (MoveTowards(target, dt))
        {
            if (walkPath != null && pathIndex < walkPath.Count - 1) { pathIndex++; return; }
            walkPath = null;
            state = State.Idle;
            OnArrived?.Invoke();
        }
    }

    /// <summary>Moves on the ground towards a point. True when it is reached.</summary>
    bool MoveTowards(Vector3 target, float dt)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance <= arriveDistance) return true;
        TurnTowards(target, dt);
        transform.position += to / distance * Mathf.Min(walkSpeed * dt, distance);
        if (snapToGround) SnapToGround();
        return false;
    }

    void TurnTowards(Vector3 worldPoint, float dt)
    {
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        TurnToYaw(Quaternion.LookRotation(dir).eulerAngles.y, dt);
    }

    void TurnToYaw(float yaw, float dt)
    {
        Quaternion goal = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, turnSpeed * dt);
    }

    float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 1.5f;
        var hits = Physics.RaycastAll(origin, Vector3.down, 4f, groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = transform.position;
        bool found = false;
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            if (player != null && hit.transform.IsChildOf(player)) continue;
            if (hit.distance < best) { best = hit.distance; ground = hit.point; found = true; }
        }
        if (found) transform.position = new Vector3(transform.position.x, ground.y, transform.position.z);
    }

    void PlayAnim(string stateName)
    {
        if (animator == null || !animator.isActiveAndEnabled || string.IsNullOrEmpty(stateName)) return;
        int hash = Animator.StringToHash(stateName);
        if (hash == currentAnim) return;
        if (!animator.HasState(0, hash))
        {
            if (stateName == idleState) return;
            hash = Animator.StringToHash(idleState);
            if (hash == currentAnim || !animator.HasState(0, hash)) return;
        }
        animator.CrossFadeInFixedTime(hash, crossFadeSeconds);
        currentAnim = hash;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f);
        if (facePlayerWhenNear > 0f) Gizmos.DrawWireSphere(transform.position, facePlayerWhenNear);
        Gizmos.DrawLine(transform.position + Vector3.up * 1.6f, transform.position + Vector3.up * 1.6f + transform.forward * 0.8f);
    }
}
