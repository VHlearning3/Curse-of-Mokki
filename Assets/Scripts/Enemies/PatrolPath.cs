using UnityEngine;

/// <summary>
/// Patrol route for an enemy: the child objects of this object are the points, in Hierarchy order.
/// Move, add (Ctrl+D) or remove children to edit the route. Shows in the Scene view.
/// </summary>
public class PatrolPath : MonoBehaviour
{
    public enum Mode { BackAndForth, Loop }

    [Tooltip("Back And Forth = 1-2-3-2-1… Loop = 1-2-3-1-2-3…")]
    [SerializeField] Mode mode = Mode.BackAndForth;
    [SerializeField] Color gizmoColor = new Color(1f, 0.6f, 0f, 1f);

    public int Count => transform.childCount;
    public Vector3 GetPoint(int index) => transform.GetChild(Mathf.Clamp(index, 0, Count - 1)).position;

    /// <summary>Next point index. direction is +1 / -1 and flips at the ends in Back And Forth mode.</summary>
    public int Next(int current, ref int direction)
    {
        int count = Count;
        if (count <= 1) return 0;
        if (mode == Mode.Loop) return (current + 1) % count;
        if (direction == 0) direction = 1;
        int next = current + direction;
        if (next >= count || next < 0)
        {
            direction = -direction;
            next = current + direction;
        }
        return next;
    }

    /// <summary>True for the turning points: first and last (Back And Forth), first (Loop).</summary>
    public bool IsEnd(int index)
    {
        if (mode == Mode.Loop) return index == 0;
        return index == 0 || index == Count - 1;
    }

    /// <summary>Index of the point closest to a position.</summary>
    public int Closest(Vector3 position)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Count; i++)
        {
            float d = (GetPoint(i) - position).sqrMagnitude;
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return best;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        for (int i = 0; i < Count; i++)
        {
            Vector3 p = GetPoint(i);
            Gizmos.DrawWireSphere(p, 0.3f);
            if (i + 1 < Count) Gizmos.DrawLine(p, GetPoint(i + 1));
        }
        if (mode == Mode.Loop && Count > 2) Gizmos.DrawLine(GetPoint(Count - 1), GetPoint(0));
    }
}
