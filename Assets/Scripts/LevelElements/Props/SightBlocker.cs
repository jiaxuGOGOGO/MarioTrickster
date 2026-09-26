using UnityEngine;

/// <summary>
/// S187：标记——这个物体（即使碰撞体是 Trigger、人可以穿过）也挡住马里奥的视线。
/// 用于草丛等"藏身处"。MarioSuspicionTracker.CanWitness 与视锥显示都会遵守它。
/// 只会让马里奥看得更少，不给他任何额外信息（宪法 H4 安全方向）。
/// </summary>
public class SightBlocker : MonoBehaviour
{
    /// <summary>这个碰撞体会挡住从 viewer 看出去的视线吗？（viewer 在同一丛里 = 不挡：他走进草丛就能看见你）</summary>
    public static bool Blocks(Collider2D c, Vector2 viewer)
    {
        if (c == null || c.GetComponentInParent<SightBlocker>() == null) return false;
        return !c.OverlapPoint(viewer);
    }
}
