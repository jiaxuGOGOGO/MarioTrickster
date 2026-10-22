using System.Collections.Generic;

/// <summary>
/// S203：绕路寻路（纯逻辑，沙盒可测）——谨慎型马里奥"绕开被坑过的地方"。
/// 做法：把每个被坑点周围 radius 格（上下各 1 格）设为禁区，用 LevelPathPlanner 同一套移动模型找路；
/// 只有换了一条**经过别的高度**的路才算绕路（同层原地跳过去的走法 AI 执行不了 → 按原路 + 放慢处理）。
/// 绕不开 → detoured=false，调用方走原路（H1/H10：不会因为怕坑而停住）。起点、终点永远不算禁区。
/// </summary>
public static class DetourPlanner
{
    public static HashSet<int> AvoidCells(IList<(int x, int y)> spots, float radius, LevelPathPlanner.Cell from, LevelPathPlanner.Cell to)
    {
        var set = new HashSet<int>();
        if (spots == null) return set;
        int r = System.Math.Max(0, (int)System.Math.Ceiling(radius));
        foreach (var (sx, sy) in spots)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx * dx <= radius * radius) set.Add((sx + dx) * 1000 + (sy + dy));
        set.Remove(from.x * 1000 + from.y); set.Remove(to.x * 1000 + to.y);
        return set;
    }

    /// <summary>返回下一个路点（x&lt;0 = 没有）；detoured = 是否真的换了一条路。</summary>
    public static LevelPathPlanner.Cell Detour(IList<string> rows, int fx, int fy, int tx, int ty, float fromX, IList<(int x, int y)> spots, float radius, out bool detoured)
    {
        detoured = false;
        var a = new LevelPathPlanner.Cell(fx, fy); var b = new LevelPathPlanner.Cell(tx, ty);
        if (spots == null || spots.Count == 0) return new LevelPathPlanner.Cell(-1, -1);
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = reg.GetHazardChars();
        a = LevelPathPlanner.Settle(rows, a, solid, hazard); b = LevelPathPlanner.Settle(rows, b, solid, hazard);
        if (a.x < 0 || b.x < 0) return new LevelPathPlanner.Cell(-1, -1);
        var safe = LevelPathPlanner.Path(rows, a, b, AvoidCells(spots, radius, a, b));
        if (safe == null || safe.Count < 2) return new LevelPathPlanner.Cell(-1, -1);
        var plain = LevelPathPlanner.Path(rows, a, b);
        detoured = (plain == null || !Same(plain, safe)) && UsesOtherLevel(safe, plain);
        return detoured ? LevelPathPlanner.NextWaypoint(safe, fromX) : new LevelPathPlanner.Cell(-1, -1);
    }

    public static bool UsesOtherLevel(List<LevelPathPlanner.Cell> safe, List<LevelPathPlanner.Cell> plain)
    {
        var heights = new HashSet<int>();
        if (plain != null) foreach (var c in plain) heights.Add(c.y);
        foreach (var c in safe) if (!heights.Contains(c.y) && !heights.Contains(c.y - 1) && !heights.Contains(c.y + 1)) return true;
        return false;
    }

    private static bool Same(List<LevelPathPlanner.Cell> p, List<LevelPathPlanner.Cell> q)
    {
        if (p.Count != q.Count) return false;
        for (int i = 0; i < p.Count; i++) if (p[i].x != q[i].x || p[i].y != q[i].y) return false;
        return true;
    }
}
