using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Visual-only smoking effect (test feature, no gameplay impact).
/// Attach to the player camera. A joint is held in the lower right of the view.
/// Press the smoke key to take a hit: the joint is raised to the mouth, the ember
/// glows while inhaling, the joint is lowered, smoke is exhaled, and then the screen
/// gets a green haze and the field of view sways for a while.
/// Builds everything it needs at runtime (placeholder joint from primitives),
/// so no prefabs, models or materials are required.
/// </summary>
[RequireComponent(typeof(Camera))]
public class SmokingEffect : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key smokeKey = Key.G;

    [Header("Joint (positions are relative to the camera)")]
    [Tooltip("Keep z above the camera's near clip plane (0.3 by default) or the joint is cut off.")]
    [SerializeField] private Vector3 jointRestPosition = new Vector3(0.22f, -0.2f, 0.55f);
    [SerializeField] private Vector3 jointRestRotation = new Vector3(55f, -30f, 0f);
    [SerializeField] private Vector3 jointMouthPosition = new Vector3(0.02f, -0.15f, 0.42f);
    [SerializeField] private Vector3 jointMouthRotation = new Vector3(82f, 0f, 0f);
    [SerializeField] private float jointLength = 0.12f;
    [SerializeField] private float jointThickness = 0.014f;
    [SerializeField] private Color paperColor = new Color(0.92f, 0.9f, 0.82f);
    [SerializeField] private Color emberColor = new Color(0.7f, 0.2f, 0.05f);
    [SerializeField] private Color emberGlowColor = new Color(1f, 0.6f, 0.15f);

    [Header("Hit animation (seconds)")]
    [SerializeField] private float raiseTime = 0.5f;
    [SerializeField] private float inhaleTime = 1.2f;
    [SerializeField] private float lowerTime = 0.6f;

    [Header("High effect (seconds)")]
    [SerializeField] private float fadeInTime = 1.5f;
    [SerializeField] private float holdTime = 6f;
    [SerializeField] private float fadeOutTime = 4f;

    [Header("Screen haze")]
    [SerializeField] private Color hazeColor = new Color(0.45f, 0.75f, 0.35f);
    [SerializeField, Range(0f, 1f)] private float hazeMaxAlpha = 0.25f;

    [Header("Camera sway")]
    [SerializeField] private float fovSwayAmount = 6f;
    [SerializeField] private float fovSwaySpeed = 0.8f;

    [Header("Smoke puff")]
    [SerializeField] private int puffParticles = 25;
    [SerializeField] private Color smokeColor = new Color(0.85f, 0.85f, 0.85f, 0.5f);

    /// <summary>True from the start of a hit until the high effect has faded out.</summary>
    public bool IsHigh { get; private set; }

    /// <summary>True while the raise / inhale / lower animation is playing.</summary>
    public bool IsTakingHit { get; private set; }

    private Camera cam;
    private float baseFov;
    private Image hazeImage;
    private ParticleSystem smoke;
    private Transform joint;
    private Transform ember;
    private Material emberMaterial;
    private Vector3 emberBaseScale;
    private float strength;
    private Coroutine routine;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        baseFov = cam.fieldOfView;
        BuildHazeOverlay();
        BuildSmokeEmitter();
        BuildJoint();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[smokeKey].wasPressedThisFrame)
        {
            Smoke();
        }
    }

    /// <summary>
    /// Takes a hit. Ignored while a hit animation is already playing;
    /// during the high effect it takes another hit and extends the effect.
    /// Other scripts may call this.
    /// </summary>
    public void Smoke()
    {
        if (IsTakingHit)
        {
            return;
        }
        if (routine != null)
        {
            StopCoroutine(routine);
        }
        routine = StartCoroutine(SmokeRoutine());
    }

    private IEnumerator SmokeRoutine()
    {
        IsHigh = true;
        IsTakingHit = true;

        Quaternion restRotation = Quaternion.Euler(jointRestRotation);
        Quaternion mouthRotation = Quaternion.Euler(jointMouthRotation);

        // Raise the joint to the mouth.
        for (float t = 0f; t < raiseTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / raiseTime);
            SetJointPose(Vector3.Lerp(jointRestPosition, jointMouthPosition, k),
                         Quaternion.Slerp(restRotation, mouthRotation, k));
            ApplyHigh(strength);
            yield return null;
        }
        SetJointPose(jointMouthPosition, mouthRotation);

        // Inhale: the ember brightens and swells, then settles.
        for (float t = 0f; t < inhaleTime; t += Time.deltaTime)
        {
            SetEmberGlow(Mathf.Sin(t / inhaleTime * Mathf.PI));
            ApplyHigh(strength);
            yield return null;
        }
        SetEmberGlow(0f);

        // Lower the joint back to rest.
        for (float t = 0f; t < lowerTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / lowerTime);
            SetJointPose(Vector3.Lerp(jointMouthPosition, jointRestPosition, k),
                         Quaternion.Slerp(mouthRotation, restRotation, k));
            ApplyHigh(strength);
            yield return null;
        }
        SetJointPose(jointRestPosition, restRotation);
        IsTakingHit = false;

        // Exhale, then the high sets in.
        smoke.Emit(puffParticles);

        float startStrength = strength;
        for (float t = 0f; t < fadeInTime; t += Time.deltaTime)
        {
            ApplyHigh(Mathf.Lerp(startStrength, 1f, t / fadeInTime));
            yield return null;
        }

        for (float t = 0f; t < holdTime; t += Time.deltaTime)
        {
            ApplyHigh(1f);
            yield return null;
        }

        for (float t = 0f; t < fadeOutTime; t += Time.deltaTime)
        {
            ApplyHigh(1f - t / fadeOutTime);
            yield return null;
        }

        ApplyHigh(0f);
        IsHigh = false;
        routine = null;
    }

    /// <summary>Applies the haze and sway at the given strength, 0 (off) to 1 (full).</summary>
    private void ApplyHigh(float newStrength)
    {
        strength = Mathf.Clamp01(newStrength);

        Color c = hazeColor;
        c.a = hazeMaxAlpha * strength;
        hazeImage.color = c;

        float sway = Mathf.Sin(Time.time * fovSwaySpeed * Mathf.PI * 2f) * fovSwayAmount;
        cam.fieldOfView = baseFov + sway * strength;
    }

    private void SetJointPose(Vector3 localPosition, Quaternion localRotation)
    {
        joint.localPosition = localPosition;
        joint.localRotation = localRotation;
    }

    /// <summary>Sets how brightly the ember burns, 0 (resting) to 1 (full drag).</summary>
    private void SetEmberGlow(float glow)
    {
        emberMaterial.color = Color.Lerp(emberColor, emberGlowColor, glow);
        ember.localScale = emberBaseScale * (1f + 0.4f * glow);
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (hazeImage != null)
        {
            ApplyHigh(0f);
        }
        if (joint != null)
        {
            SetJointPose(jointRestPosition, Quaternion.Euler(jointRestRotation));
            SetEmberGlow(0f);
        }
        IsHigh = false;
        IsTakingHit = false;
    }

    /// <summary>
    /// Builds a placeholder joint from primitives: a paper cylinder with an ember at the far tip.
    /// The joint's local Y axis runs along its length, tip at +Y.
    /// </summary>
    private void BuildJoint()
    {
        GameObject root = new GameObject("Joint");
        joint = root.transform;
        joint.SetParent(transform, false);
        SetJointPose(jointRestPosition, Quaternion.Euler(jointRestRotation));

        // Unity's cylinder is 2 units tall and 1 unit wide.
        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        paper.name = "Paper";
        Destroy(paper.GetComponent<Collider>());
        paper.transform.SetParent(joint, false);
        paper.transform.localScale = new Vector3(jointThickness, jointLength * 0.5f, jointThickness);
        paper.GetComponent<MeshRenderer>().material = BuildUnlitMaterial(paperColor);

        GameObject emberObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        emberObject.name = "Ember";
        Destroy(emberObject.GetComponent<Collider>());
        ember = emberObject.transform;
        ember.SetParent(joint, false);
        ember.localPosition = new Vector3(0f, jointLength * 0.5f, 0f);
        emberBaseScale = Vector3.one * jointThickness * 1.05f;
        ember.localScale = emberBaseScale;

        emberMaterial = BuildUnlitMaterial(emberColor);
        emberObject.GetComponent<MeshRenderer>().material = emberMaterial;
    }

    /// <summary>Flat-coloured material that stays visible in dark scenes.</summary>
    private static Material BuildUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private void BuildHazeOverlay()
    {
        GameObject canvasObject = new GameObject("SmokingHazeCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        GameObject imageObject = new GameObject("Haze");
        imageObject.transform.SetParent(canvasObject.transform, false);

        hazeImage = imageObject.AddComponent<Image>();
        hazeImage.raycastTarget = false;
        hazeImage.color = Color.clear;

        RectTransform rect = hazeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildSmokeEmitter()
    {
        GameObject smokeObject = new GameObject("SmokingPuff");
        smokeObject.transform.SetParent(transform, false);
        smokeObject.transform.localPosition = new Vector3(0f, -0.15f, 0.4f);

        smoke = smokeObject.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = smokeColor;
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.02f;

        ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.5f));

        ParticleSystem.ColorOverLifetimeModule color = smoke.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystemRenderer particleRenderer = smokeObject.GetComponent<ParticleSystemRenderer>();

        // Sprites/Default is alpha blended and tinted by particle colour, and works in URP.
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.mainTexture = BuildSoftCircleTexture(32);
        particleRenderer.material = material;
    }

    /// <summary>Creates a white round texture that fades out towards the edge.</summary>
    private static Texture2D BuildSoftCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float alpha = Mathf.Clamp01(1f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        texture.Apply();
        return texture;
    }
}
