using System;
using UnityEngine;

/// <summary>
/// Cultist (GDD): patrols a PatrolPath, chases the player on sight and hits (PlayerMadness.TakeHit:
/// kills at the death threshold, otherwise adds madness). Disappears if the hit doesn't kill.
/// Put on the cultist root (with the Animator). Add EnemyMadnessAura for the aura.
/// </summary>
public class CultistAI : MonoBehaviour
{
    public enum State { Patrol, Wait, Chase, Search, Attack, Gone }

    [Tooltip("Leave empty to find the player automatically.")]
    [SerializeField] PlayerMadness playerMadness;

    [Header("Patrol")]
    [Tooltip("Route to walk (object with child points). Empty = stands still.")]
    [SerializeField] PatrolPath path;
    [Tooltip("Walking speed while patrolling (m/s).")]
    [SerializeField, Min(0f)] float walkSpeed = 1.4f;
    [Tooltip("Seconds to stand at a point.")]
    [SerializeField, Min(0f)] float waitAtPoint = 2f;
    [Tooltip("Off = stands only at the ends of the route (turning points) and walks straight through the points in between.")]
    [SerializeField] bool waitAtEveryPoint = false;
    [Tooltip("How close counts as reaching a point (m).")]
    [SerializeField, Min(0.05f)] float pointReachDistance = 0.4f;
    [Tooltip("Turning speed (degrees per second).")]
    [SerializeField, Min(1f)] float turnSpeed = 300f;
    [Tooltip("Turn this if the model walks backwards or sideways: 180 = model faces -Z (the cultist model).")]
    [SerializeField] float modelYawOffset = 180f;

    [Header("Seeing the player")]
    [Tooltip("How far the cultist sees (m).")]
    [SerializeField, Min(0f)] float viewDistance = 15f;
    [Tooltip("Width of the view cone (degrees).")]
    [SerializeField, Range(1f, 360f)] float viewAngle = 120f;
    [Tooltip("Closer than this the cultist notices the player in any direction (m). Hidden players are never seen.")]
    [SerializeField, Min(0f)] float noticeDistance = 2.5f;
    [Tooltip("Height of the cultist's eyes above its feet (m).")]
    [SerializeField] float eyeHeight = 1.6f;
    [Tooltip("Layers that block the view (walls, terrain).")]
    [SerializeField] LayerMask sightBlockers = ~0;
    [Tooltip("Seconds without seeing the player before giving up the chase.")]
    [SerializeField, Min(0f)] float loseSightSeconds = 3f;
    [Tooltip("Seconds to look around at the last seen spot before going back to patrol.")]
    [SerializeField, Min(0f)] float searchSeconds = 3f;

    [Header("Chase and hit")]
    [Tooltip("Speed while chasing (m/s).")]
    [SerializeField, Min(0f)] float chaseSpeed = 3.5f;
    [Tooltip("Distance to start a hit (m).")]
    [SerializeField, Min(0.1f)] float attackRange = 1.6f;
    [Tooltip("Seconds between starting the attack and the hit landing (the player can still run). 0.45 = arms fully out in the attack animation.")]
    [SerializeField, Min(0f)] float attackWindup = 0.45f;
    [Tooltip("Madness added by a hit. 0 = the PlayerMadness default hit amount.")]
    [SerializeField, Min(0f)] float hitMadness = 0f;
    [Tooltip("GDD: the cultist disappears if its hit doesn't kill.")]
    [SerializeField] bool disappearAfterHit = true;
    [Tooltip("Seconds after the hit before disappearing (lets the attack animation finish).")]
    [SerializeField, Min(0f)] float disappearDelay = 0.8f;
    [Tooltip("Seconds the attack animation keeps playing after the hit before idle/walk can take over.")]
    [SerializeField, Min(0f)] float attackFinishSeconds = 0.8f;

    [Header("Ground")]
    [Tooltip("Keep the feet on the ground (raycast down).")]
    [SerializeField] bool snapToGround = true;
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Animation (Animator state names)")]
    [Tooltip("Leave empty to use the Animator on this object or its children.")]
    [SerializeField] Animator animator;
    [SerializeField] string idleState = "Idle";
    [SerializeField] string walkState = "Walk";
    [Tooltip("Optional. If the Animator has no such state, idle is used.")]
    [SerializeField] string attackState = "Attack";
    [Tooltip("Walk animation speed while patrolling.")]
    [SerializeField, Min(0f)] float walkAnimSpeed = 1f;
    [Tooltip("Walk animation speed while chasing (no run animation yet).")]
    [SerializeField, Min(0f)] float chaseAnimSpeed = 2f;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.2f;

    [Header("Debug (live values)")]
    [SerializeField] State state;
    [SerializeField] bool canSeePlayer;

    public State CurrentState => state;
    public bool CanSeePlayer => canSeePlayer;
    public event Action OnSpottedPlayer;
    public event Action<bool> OnHitPlayer; // true = killed

    PlayerMadness madness;
    PlayerMovement movement;
    Transform player;
    Transform playerEye;
    int pointIndex;
    int direction = 1;
    float timer;
    float lostTimer;
    bool searchArrived;
    Vector3 lastSeen;
    int currentAnim;
    float animHold;

    void Start()
    {
        madness = playerMadness != null ? playerMadness : FindFirstObjectByType<PlayerMadness>();
        if (madness != null)
        {
            player = madness.transform;
            movement = player.GetComponent<PlayerMovement>();
            var cam = player.GetComponentInChildren<Camera>();
            playerEye = cam != null ? cam.transform : player;
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (path != null && path.Count > 0) pointIndex = path.Closest(transform.position);
        if (path != null && path.Count > 0) state = State.Patrol;
        else EnterWait(float.PositiveInfinity);
    }

    void Update()
    {
        if (MenuUtility.AnyMenuOpen) return;
        Step(Time.deltaTime);
    }

    void Step(float dt)
    {
        if (player == null || state == State.Gone && !gameObject.activeSelf) return;
        if (animHold > 0f) animHold -= dt;

        if (state == State.Gone)
        {
            timer -= dt;
            if (timer <= 0f) gameObject.SetActive(false);
            return;
        }

        canSeePlayer = !madness.IsDead && LookForPlayer();

        switch (state)
        {
            case State.Patrol:
                if (canSeePlayer) { StartChase(); break; }
                if (path == null || path.Count == 0) { EnterWait(float.PositiveInfinity); break; }
                if (MoveTowards(path.GetPoint(pointIndex), walkSpeed, walkAnimSpeed, dt))
                {
                    if (waitAtEveryPoint || path.IsEnd(pointIndex)) EnterWait(waitAtPoint);
                    else pointIndex = path.Next(pointIndex, ref direction);
                }
                break;

            case State.Wait:
                if (canSeePlayer) { StartChase(); break; }
                PlayAnim(idleState, 1f);
                timer -= dt;
                if (timer <= 0f && path != null && path.Count > 1)
                {
                    pointIndex = path.Next(pointIndex, ref direction);
                    state = State.Patrol;
                }
                break;

            case State.Chase:
                if (madness.IsDead) { EnterWait(float.PositiveInfinity); break; }
                if (canSeePlayer) { lastSeen = player.position; lostTimer = 0f; }
                else lostTimer += dt;

                if (canSeePlayer && FlatDistance(player.position) <= attackRange)
                {
                    state = State.Attack;
                    timer = attackWindup;
                    break;
                }
                bool reached = MoveTowards(lastSeen, chaseSpeed, chaseAnimSpeed, dt);
                if (lostTimer >= loseSightSeconds || (!canSeePlayer && reached)) EnterSearch();
                break;

            case State.Search:
                if (canSeePlayer) { StartChase(); break; }
                if (!searchArrived)
                {
                    searchArrived = MoveTowards(lastSeen, walkSpeed, walkAnimSpeed, dt);
                    break;
                }
                PlayAnim(idleState, 1f);
                timer -= dt;
                if (timer <= 0f)
                {
                    if (path != null && path.Count > 0)
                    {
                        pointIndex = path.Closest(transform.position);
                        state = State.Patrol;
                    }
                    else EnterWait(float.PositiveInfinity);
                }
                break;

            case State.Attack:
                Face(player.position - transform.position, dt);
                if (!PlayAnim(attackState, 1f)) PlayAnim(idleState, 1f);
                timer -= dt;
                if (timer > 0f) break;
                bool hidden = movement != null && movement.IsHidden;
                if (!hidden && !madness.IsDead && FlatDistance(player.position) <= attackRange * 1.3f) Hit();
                else state = State.Chase;
                break;
        }
    }

    void Hit()
    {
        bool killed = hitMadness > 0f ? madness.TakeHit(hitMadness) : madness.TakeHit();
        OnHitPlayer?.Invoke(killed);
        animHold = attackFinishSeconds; // let the attack animation play out
        if (!killed && disappearAfterHit)
        {
            state = State.Gone;
            timer = disappearDelay;
        }
        else EnterWait(float.PositiveInfinity);
    }

    void StartChase()
    {
        lastSeen = player.position;
        lostTimer = 0f;
        if (state != State.Chase && state != State.Attack) OnSpottedPlayer?.Invoke();
        state = State.Chase;
    }

    void EnterWait(float seconds)
    {
        state = State.Wait;
        timer = seconds;
        PlayAnim(idleState, 1f);
    }

    void EnterSearch()
    {
        state = State.Search;
        timer = searchSeconds;
        searchArrived = false;
    }

    /// <summary>The way the model looks (transform.forward corrected by Model Yaw Offset).</summary>
    Vector3 Facing => transform.rotation * Quaternion.Euler(0f, -modelYawOffset, 0f) * Vector3.forward;

    /// <summary>Can the cultist see the player right now?</summary>
    bool LookForPlayer()
    {
        if (movement != null && movement.IsHidden) return false;
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = playerEye.position - eye;
        float distance = toPlayer.magnitude;
        if (distance > viewDistance || distance < 0.01f) return false;
        if (distance > noticeDistance)
        {
            Vector3 flat = toPlayer;
            flat.y = 0f;
            if (Vector3.Angle(Facing, flat) > viewAngle * 0.5f) return false;
        }
        if (Physics.Raycast(eye, toPlayer / distance, out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(transform)) return false;
        }
        return true;
    }

    /// <summary>Walks towards a point. Returns true when it is reached.</summary>
    bool MoveTowards(Vector3 target, float speed, float animSpeed, float dt)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance <= pointReachDistance) return true;

        Face(to, dt);
        transform.position += to / distance * Mathf.Min(speed * dt, distance);
        if (snapToGround) SnapToGround();
        PlayAnim(walkState, animSpeed);
        return false;
    }

    void Face(Vector3 direction, float dt)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
    }

    void SnapToGround()
    {
        Vector3 from = transform.position + Vector3.up * 2f;
        var hits = Physics.RaycastAll(from, Vector3.down, 6f, groundLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(transform) || (player != null && hit.transform.IsChildOf(player))) continue;
            if (hit.distance < best) best = hit.distance;
        }
        if (best < float.MaxValue) transform.position = from + Vector3.down * best;
    }

    float FlatDistance(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    /// <summary>Cross-fades to an Animator state by name. Returns false if there is no such state.</summary>
    bool PlayAnim(string stateName, float speed)
    {
        if (animator == null || string.IsNullOrEmpty(stateName) || animator.runtimeAnimatorController == null) return false;
        if (animHold > 0f && stateName != attackState) return true; // attack animation still finishing
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash)) return false;
        animator.speed = speed;
        if (hash != currentAnim)
        {
            animator.CrossFadeInFixedTime(hash, crossFadeSeconds);
            currentAnim = hash;
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        float half = Mathf.Min(viewAngle, 359f) * 0.5f;
        Vector3 left = Quaternion.Euler(0f, -half, 0f) * Facing * viewDistance;
        Vector3 right = Quaternion.Euler(0f, half, 0f) * Facing * viewDistance;
        Gizmos.DrawLine(eye, eye + left);
        Gizmos.DrawLine(eye, eye + right);
        const int segments = 24;
        Vector3 previous = eye + left;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = eye + Quaternion.Euler(0f, -half + i * (2f * half / segments), 0f) * Facing * viewDistance;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, noticeDistance);
    }
}
