using System;
using UnityEngine;

/// <summary>
/// Wolf (GDD): chasing enemy at the end of the Forest that forces the player forward.
/// Waits at the first point of its WolfChasePath; when the player enters the start circle it runs
/// along the route. If it gets close to the player it lunges straight at them; reaching them kills
/// the player (too slow = dead). When the player reaches the end circle the wolf leaves.
/// Put on the wolf root. Add EnemyMadnessAura for the sanity losing aura.
/// </summary>
public class WolfChase : MonoBehaviour
{
    public enum State { Waiting, Chasing, Lunging, Done }

    [Tooltip("Leave empty to find the player automatically.")]
    [SerializeField] PlayerMadness playerMadness;
    [Tooltip("The wolf's own route (object with WolfChasePath and child points).")]
    [SerializeField] WolfChasePath path;

    [Header("Chase speed")]
    [Tooltip("Running speed along the route (m/s). GDD: Chase speed. Player sprint is 6, walk 3.")]
    [SerializeField, Min(0f)] float chaseSpeed = 5f;
    [Tooltip("If the wolf is this far behind the player, it runs at Catch Up Speed instead (keeps the pressure on). 0 = off.")]
    [SerializeField, Min(0f)] float maxGapDistance = 25f;
    [SerializeField, Min(0f)] float catchUpSpeed = 8f;
    [Tooltip("Seconds after the start before the wolf starts running (time for the howl).")]
    [SerializeField, Min(0f)] float startDelay = 1.5f;
    [SerializeField, Min(1f)] float turnSpeed = 540f;
    [Tooltip("180 = the model faces -Z (like the cultist). 0 = faces +Z.")]
    [SerializeField] float modelYawOffset = 0f;

    [Header("Catching the player")]
    [Tooltip("Closer than this the wolf leaves the route and runs straight at the player (m).")]
    [SerializeField, Min(0f)] float lungeDistance = 6f;
    [Tooltip("Lunge speed (m/s).")]
    [SerializeField, Min(0f)] float lungeSpeed = 9f;
    [Tooltip("Distance at which the wolf catches the player (m).")]
    [SerializeField, Min(0.1f)] float killDistance = 1.3f;
    [Tooltip("On = the wolf always kills when it catches the player. Off = a normal hit (kills only in the madness danger zone).")]
    [SerializeField] bool alwaysKill = true;
    [Tooltip("On = a hidden player is safe. GDD: the wolf forces the player forward, so off by default.")]
    [SerializeField] bool hidingProtects = false;

    [Header("End")]
    [Tooltip("Seconds after the player reaches the end circle before the wolf disappears.")]
    [SerializeField, Min(0f)] float disappearDelay = 1f;

    [Header("Ground")]
    [SerializeField] bool snapToGround = true;
    [SerializeField] LayerMask groundLayers = ~0;

    [Header("Animation (Animator state names, optional)")]
    [SerializeField] Animator animator;
    [SerializeField] string idleState = "Idle";
    [SerializeField] string runState = "Run";
    [SerializeField] string attackState = "Attack";
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.15f;

    [Header("Sounds (AudioManager names, empty = none). GDD: SFX_WolfHowling, SFX_WolfChase, SFX_WolfAttack")]
    [SerializeField] string howlSound = "";
    [SerializeField] string chaseSound = "";
    [SerializeField] string attackSound = "";

    [Header("Debug (live values)")]
    [SerializeField] State state;
    [SerializeField] int targetPoint;
    [SerializeField] float distanceToPlayer;

    public State CurrentState => state;
    public event Action OnChaseStarted;
    public event Action OnCaughtPlayer;
    public event Action OnChaseEnded;

    PlayerMadness madness;
    PlayerMovement movement;
    Transform player;
    float timer;
    int currentAnim;
    bool chaseSoundPlayed;

    void Start()
    {
        madness = playerMadness != null ? playerMadness : FindFirstObjectByType<PlayerMadness>();
        if (madness != null)
        {
            player = madness.transform;
            movement = player.GetComponent<PlayerMovement>();
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (path != null && path.Count > 0)
        {
            transform.position = path.GetPoint(0);
            if (snapToGround) SnapToGround();
            if (path.Count > 1) Face(path.GetPoint(1) - transform.position, 1000f);
        }
        state = State.Waiting;
        targetPoint = 1;
        PlayAnim(idleState);
    }

    void Update()
    {
        if (MenuUtility.AnyMenuOpen) return;
        Step(Time.deltaTime);
    }

    void Step(float dt)
    {
        if (player == null || path == null || path.Count == 0) return;
        distanceToPlayer = FlatDistance(player.position);

        switch (state)
        {
            case State.Waiting:
                if (path.IsInStart(player.position)) BeginChase();
                break;

            case State.Chasing:
                if (CheckEnd()) break;
                if (timer > 0f) { timer -= dt; break; }
                if (!chaseSoundPlayed) { chaseSoundPlayed = true; Play(chaseSound); }
                if (madness.IsDead) { state = State.Done; PlayAnim(idleState); break; }
                if (distanceToPlayer <= lungeDistance && CanTouchPlayer()) { state = State.Lunging; break; }
                float speed = (maxGapDistance > 0f && distanceToPlayer > maxGapDistance) ? catchUpSpeed : chaseSpeed;
                FollowRoute(speed, dt);
                break;

            case State.Lunging:
                if (CheckEnd()) break;
                if (madness.IsDead) { state = State.Done; PlayAnim(idleState); break; }
                if (!CanTouchPlayer() || distanceToPlayer > lungeDistance * 1.5f) { state = State.Chasing; break; }
                MoveTowards(player.position, lungeSpeed, dt);
                if (FlatDistance(player.position) <= killDistance) Catch();
                break;

            case State.Done:
                if (timer > 0f)
                {
                    timer -= dt;
                    if (timer <= 0f && !madness.IsDead) gameObject.SetActive(false);
                }
                break;
        }
    }

    /// <summary>Starts the chase now (also usable from a cutscene).</summary>
    public void BeginChase()
    {
        if (state != State.Waiting) return;
        state = State.Chasing;
        timer = startDelay;
        Play(howlSound);
        OnChaseStarted?.Invoke();
    }

    bool CheckEnd()
    {
        if (!path.IsInEnd(player.position)) return false;
        state = State.Done;
        timer = Mathf.Max(0.01f, disappearDelay);
        PlayAnim(idleState);
        OnChaseEnded?.Invoke();
        return true;
    }

    bool CanTouchPlayer() => !(hidingProtects && movement != null && movement.IsHidden);

    void FollowRoute(float speed, float dt)
    {
        if (targetPoint >= path.Count)
        {
            // Route ran out before the end circle: go straight for the player.
            MoveTowards(player.position, speed, dt);
            return;
        }
        if (MoveTowards(path.GetPoint(targetPoint), speed, dt)) targetPoint++;
    }

    void Catch()
    {
        state = State.Done;
        timer = 0f;
        PlayAnim(attackState);
        Play(attackSound);
        if (alwaysKill) madness.SetMadness(100f);
        madness.TakeHit();
        OnCaughtPlayer?.Invoke();
    }

    /// <summary>Moves on the ground towards a point. True when it is reached.</summary>
    bool MoveTowards(Vector3 target, float speed, float dt)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance <= 0.3f) return true;
        Face(to, dt);
        transform.position += to / distance * Mathf.Min(speed * dt, distance);
        if (snapToGround) SnapToGround();
        PlayAnim(runState);
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

    bool PlayAnim(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName) || animator.runtimeAnimatorController == null) return false;
        int hash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, hash)) return false;
        if (hash != currentAnim)
        {
            animator.CrossFadeInFixedTime(hash, crossFadeSeconds);
            currentAnim = hash;
        }
        return true;
    }

    static void Play(string sound)
    {
        if (string.IsNullOrEmpty(sound)) return;
        var audio = AudioManager.Instance;
        if (audio != null) audio.PlaySFX(sound);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, lungeDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, killDistance);
    }
}
