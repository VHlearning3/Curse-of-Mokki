using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>TEST ONLY. Madness bar on screen + keys: H hit, J drain on/off, K reset.</summary>
[RequireComponent(typeof(PlayerMadness))]
public class MadnessDebug : MonoBehaviour
{
    [Header("Test keys")]
    [SerializeField] Key hitKey = Key.H;
    [SerializeField] Key toggleDrainKey = Key.J;
    [SerializeField] Key resetKey = Key.K;

    [Header("Overlay")]
    [SerializeField] bool showOverlay = true;

    PlayerMadness madness;
    bool draining;
    string lastEvent = "-";

    void Awake()
    {
        madness = GetComponent<PlayerMadness>();
    }

    void OnEnable()
    {
        if (madness == null) madness = GetComponent<PlayerMadness>();
        madness.OnDied += HandleDied;
        madness.OnDangerZoneChanged += HandleDangerZone;
    }

    void OnDisable()
    {
        if (madness == null) return;
        madness.OnDied -= HandleDied;
        madness.OnDangerZoneChanged -= HandleDangerZone;
        if (draining) { madness.EndDrain(this); draining = false; }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (Pressed(kb, hitKey))
        {
            bool killed = madness.TakeHit();
            lastEvent = killed ? "Hit -> DIED" : "Hit";
        }

        if (Pressed(kb, toggleDrainKey))
        {
            draining = !draining;
            if (draining) madness.BeginDrain(this);
            else madness.EndDrain(this);
            lastEvent = draining ? "Drain ON" : "Drain OFF";
        }

        if (Pressed(kb, resetKey))
        {
            draining = false;
            madness.ResetMadness();
            lastEvent = "Reset";
        }
    }

    static bool Pressed(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;

    void HandleDied() => lastEvent = "DIED";
    void HandleDangerZone(bool inDanger) => lastEvent = inDanger ? "Entered danger zone" : "Left danger zone";

    void OnGUI()
    {
        if (!showOverlay || madness == null) return;

        const float x = 20f, y = 20f, w = 320f, barH = 22f;
        GUI.Box(new Rect(x - 10f, y - 10f, w + 20f, 128f), GUIContent.none);

        // Bar background
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, barH), Texture2D.whiteTexture);

        // Fill: green when safe, red in the danger zone
        GUI.color = madness.IsInDangerZone ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.3f, 0.75f, 0.35f);
        GUI.DrawTexture(new Rect(x, y, w * madness.Madness01, barH), Texture2D.whiteTexture);

        // Death threshold marker
        GUI.color = Color.white;
        float tx = x + w * (madness.DeathThresholdPercent / 100f);
        GUI.DrawTexture(new Rect(tx - 1f, y - 3f, 2f, barH + 6f), Texture2D.whiteTexture);

        GUI.Label(new Rect(x, y + barH + 4f, w, 20f),
            $"Madness {madness.Madness:0.0} / {PlayerMadness.MaxMadness:0}   Sanity {madness.Sanity:0.0}");
        GUI.Label(new Rect(x, y + barH + 22f, w, 20f),
            $"Danger: {madness.IsInDangerZone}   Draining: {madness.IsDraining}   Dead: {madness.IsDead}");
        GUI.Label(new Rect(x, y + barH + 40f, w, 20f), $"Last: {lastEvent}");
        GUI.Label(new Rect(x, y + barH + 58f, w, 20f),
            $"[{hitKey}] hit   [{toggleDrainKey}] drain   [{resetKey}] reset");
    }
}
