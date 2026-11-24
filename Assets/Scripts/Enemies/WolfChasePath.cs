using UnityEngine;

/// <summary>
/// Route for the wolf chase (GDD: end of the Forest, the wolf forces the player forward).
/// The child objects are the route points in Hierarchy order: the wolf starts at the first one and
/// runs towards the last one. The chase starts when the player enters the start circle and ends
/// (the wolf leaves) when the player reaches the end circle around the last point.
/// Shows in the Scene view: red route, yellow start circle, green end circle.
/// </summary>
public class WolfChasePath : MonoBehaviour
{
    [Header("Start")]
    [Tooltip("Where the player starts the chase. Empty = the second route point (the wolf waits at the first).")]
    [SerializeField] Transform startTrigger;
    [Tooltip("Radius of the start circle (metres).")]
    [SerializeField, Min(0.1f)] float startRadius = 6f;

    [Header("End")]
    [Tooltip("Radius of the safe circle around the last route point (metres). The chase ends when the player gets there.")]
    [SerializeField, Min(0.1f)] float endRadius = 6f;

    [Header("Scene view")]
    [SerializeField] Color routeColor = new Color(1f, 0.2f, 0.2f, 1f);

    public int Count
    {
        get
        {
            int n = 0;
            foreach (Transform c in transform) if (c != startTrigger) n++;
            return n;
        }
    }

    /// <summary>Route point by index (children in Hierarchy order, the start trigger not counted).</summary>
    public Vector3 GetPoint(int index)
    {
        int n = 0;
        foreach (Transform c in transform)
        {
            if (c == startTrigger) continue;
            if (n == index) return c.position;
            n++;
        }
        return transform.position;
    }

    public Vector3 StartPosition => startTrigger != null ? startTrigger.position : (Count > 1 ? GetPoint(1) : GetPoint(0));
    public Vector3 EndPosition => Count > 0 ? GetPoint(Count - 1) : transform.position;
    public float StartRadius => startRadius;
    public float EndRadius => endRadius;

    public bool IsInStart(Vector3 p) => FlatDistance(p, StartPosition) <= startRadius;
    public bool IsInEnd(Vector3 p) => FlatDistance(p, EndPosition) <= endRadius;

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmos()
    {
        int count = Count;
        Gizmos.color = routeColor;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = GetPoint(i);
            Gizmos.DrawWireCube(p, Vector3.one * 0.5f);
            if (i + 1 < count) Gizmos.DrawLine(p, GetPoint(i + 1));
        }
        if (count == 0) return;
        DrawCircle(StartPosition, startRadius, new Color(1f, 0.9f, 0.1f, 1f));
        DrawCircle(EndPosition, endRadius, new Color(0.2f, 1f, 0.3f, 1f));
    }

    static void DrawCircle(Vector3 centre, float radius, Color color)
    {
        Gizmos.color = color;
        const int segments = 40;
        Vector3 previous = centre + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
