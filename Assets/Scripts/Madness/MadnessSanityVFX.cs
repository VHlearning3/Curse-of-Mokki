using UnityEngine;

/// <summary>
/// Shows the SanityVFX eyes around the player when madness is high (GDD: VFX_Sanity).
/// Put on the player; the SanityVFX prefab goes in the Eyes slot (child of the player works best).
/// </summary>
public class MadnessSanityVFX : MonoBehaviour
{
    [Tooltip("Leave empty to use PlayerMadness on this object, or the first one in the scene.")]
    [SerializeField] PlayerMadness madness;
    [Tooltip("The SanityVFX particle system (Assets/Art/VFX/SanityVFX.prefab).")]
    [SerializeField] ParticleSystem eyes;

    [Header("When eyes appear")]
    [Tooltip("Eyes start when madness passes this percent (50 = sanity below half).")]
    [SerializeField, Range(0f, 100f)] float startPercent = 50f;
    [Tooltip("Eyes per second just after startPercent.")]
    [SerializeField, Min(0f)] float minEyesPerSecond = 0.5f;
    [Tooltip("Eyes per second at 100 % madness.")]
    [SerializeField, Min(0f)] float maxEyesPerSecond = 4f;

    [Header("Placement")]
    [Tooltip("On = eyes appear in a ring around the player (settings below). Off = the prefab's own Shape module is used.")]
    [SerializeField] bool ringAroundPlayer = true;
    [Tooltip("Ring radius in metres.")]
    [SerializeField, Min(0f)] float ringRadius = 8f;
    [Tooltip("0 = eyes only on the ring edge, 1 = anywhere inside the ring.")]
    [SerializeField, Range(0f, 1f)] float ringThickness = 0.3f;
    [Tooltip("Height of the ring above the player's feet.")]
    [SerializeField] float eyeHeight = 1.5f;
    [Tooltip("Random offset per eye in metres (up/down and sideways), so the eyes aren't all at one height.")]
    [SerializeField, Min(0f)] float randomOffset = 0.5f;
    [Tooltip("On = eyes stay where they appeared when the player moves. Off = they move with the player.")]
    [SerializeField] bool eyesStayInPlace = true;

    [Header("Debug (live value)")]
    [SerializeField] float currentEyesPerSecond;

    /// <summary>The eyes particle system (MadnessEpisode uses the same one).</summary>
    public ParticleSystem Eyes => eyes;

    void Awake()
    {
        if (madness == null) madness = GetComponent<PlayerMadness>();
        if (madness == null) madness = FindFirstObjectByType<PlayerMadness>();
        if (eyes == null) eyes = GetComponentInChildren<ParticleSystem>();
        if (eyes == null) { Debug.LogWarning("MadnessSanityVFX: no Eyes particle system set.", this); return; }

        var main = eyes.main;
        if (eyesStayInPlace) main.simulationSpace = ParticleSystemSimulationSpace.World;

        if (ringAroundPlayer)
        {
            var shape = eyes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = ringRadius;
            shape.radiusThickness = ringThickness;
            shape.arc = 360f;
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(90f, 0f, 0f); // lay the circle flat
            shape.scale = Vector3.one;
            shape.randomPositionAmount = randomOffset;
            if (eyes.transform != transform)
            {
                eyes.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
                eyes.transform.localRotation = Quaternion.identity;
            }
        }
        Apply(0f);
    }

    void OnEnable() { if (madness != null) madness.OnMadnessChanged += Apply; }
    void OnDisable() { if (madness != null) madness.OnMadnessChanged -= Apply; }
    void Start() { if (madness != null) Apply(madness.Madness01); }

    void Apply(float madness01)
    {
        if (eyes == null) return;
        float percent = madness01 * 100f;
        float rate = 0f;
        if (percent > startPercent)
        {
            float t = startPercent >= 100f ? 1f : Mathf.InverseLerp(startPercent, 100f, percent);
            rate = Mathf.Lerp(minEyesPerSecond, maxEyesPerSecond, t);
        }
        currentEyesPerSecond = rate;
        var emission = eyes.emission;
        emission.rateOverTime = rate;
        if (rate > 0f && !eyes.isPlaying) eyes.Play();
    }
}
