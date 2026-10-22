using UnityEngine;

/// <summary>
/// 视线检测（马里奥看不看得见、炮弹穿不穿过单向台面）。S239 从已删的旧起疑追踪器 MarioSuspicionTracker 里搬出来，判定一模一样。
/// H4 纯函数：viewer 能否在 range 内无遮挡地看到 target。
/// 忽略触发器（草丛 SightBlocker 除外）、单向台面、Mario、Trickster 以及目标自身的碰撞体；任何其他实体碰撞体都算遮挡。
/// </summary>
public static class SightLine
{
    private static RaycastHit2D[] s_hits = new RaycastHit2D[32];
    private static readonly ContactFilter2D s_noFilter = NoFilter();
    private static ContactFilter2D NoFilter() { var f = new ContactFilter2D(); f.NoFilter(); return f; }

    public static bool CanWitness(Vector2 viewer, Vector2 target, float range, Transform targetRoot)
    {
        if (float.IsNaN(viewer.x) || float.IsNaN(viewer.y) || float.IsNaN(target.x) || float.IsNaN(target.y)) return false;
        if (float.IsInfinity(viewer.x) || float.IsInfinity(viewer.y) || float.IsInfinity(target.x) || float.IsInfinity(target.y)) return false;
        if (Vector2.Distance(viewer, target) > range) return false;
        // S192 性能：共享缓冲的非分配版本（不用 LinecastAll）
        int count = Physics2D.Linecast(viewer, target, s_noFilter, s_hits);
        if (count == s_hits.Length) { s_hits = new RaycastHit2D[s_hits.Length * 2]; count = Physics2D.Linecast(viewer, target, s_noFilter, s_hits); }
        for (int i = 0; i < count; i++)
        {
            var hit = s_hits[i];
            Collider2D c = hit.collider;
            if (c == null) continue;
            if (targetRoot != null && c.transform.IsChildOf(targetRoot)) continue;
            // S187：触发器默认不挡视线；带 SightBlocker 的触发器（草丛）挡视线——但看的人自己站在同一丛里时看得见。
            if (c.isTrigger) { if (SightBlocker.Blocks(c, viewer)) return false; continue; }
            // 单向平台是薄台面，不挡视线
            if (IsOneWayPlatform(c)) continue;
            if (c.GetComponentInParent<MarioController>() != null) continue;
            if (c.GetComponentInParent<TricksterController>() != null) continue;
            return false;
        }
        return true;
    }

    public static bool IsOneWayPlatform(Collider2D c)
    {
        if (c == null || !c.usedByEffector) return false;
        var effector = c.GetComponent<PlatformEffector2D>();
        return effector != null && effector.enabled && effector.useOneWay;
    }
}
