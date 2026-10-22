using System.Collections.Generic;

/// <summary>
/// S209：路线跟随模拟（纯逻辑，沙盒/工坊都能跑）——用和游戏里马里奥 AI 相同的"转向规则"（LevelPathPlanner.SteerX）
/// 和同一套楼层路点（Path + NextWaypoint），按真实身体宽度（0.8 格）一帧一帧走：出生点 → 宝物 → 出口。
/// 死局检查只回答"理论上走得到吗"；这里回答"按 AI 的走法会不会停在半路"（例如：宝物在台子边缘正下方，
/// 旧规则"离目标不到 0.3 格就停"会让他站在台边不下去——S209 用户截图的问题）。
/// 简化：沿规划路径逐步走；需要跳的一步（往上 / 跨 2 格以上）在起跳格上直接落到规划器认可的落点；
/// 走路、走下台边、掉落按真实身体宽度连续模拟（这正是旧规则出错的地方）；下落 12 格/秒；机关不触发；门按关着（最坏情况）。
/// 只用关卡地形与马里奥自己的目标（H4）。
/// </summary>
public static class LevelRouteFollower
{
    public const float Speed = 6f, FallSpeed = 12f, Dt = 0.02f, HalfWidth = 0.4f, MaxSeconds = 90f, StuckSeconds = 6f, ProgressCells = 1.5f;

    public struct Result
    {
        public bool ok;
        public float seconds;
        public string leg;          // "去拿宝" / "回出口"
        public float stuckX, stuckY;
        public string Summary => ok ? $"走完全程 {seconds:F0} 秒" : $"{leg}时停在 ({stuckX:F1},{stuckY:F1}) 走不动";
    }

    /// <param name="legacyRule">true = 用 S208 以前的转向规则（只用来证明旧规则会卡住）。</param>
    public static Result Run(IList<string> grid, bool legacyRule = false)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars();
        var m = Find(grid, 'M'); var o = Find(grid, 'o'); var g = Find(grid, 'G');
        var res = new Result { ok = false, leg = "去拿宝" };
        if (m.x < 0 || o.x < 0 || g.x < 0) { res.ok = true; return res; }
        var hazard = reg.GetHazardChars();
        float x = m.x, y = m.y, t = 0f;
        var start = LevelPathPlanner.Settle(grid, m, solid, hazard);
        if (start.x >= 0) y = start.y;
        foreach (var leg in new[] { ("去拿宝", o), ("回出口", g) })
        {
            res.leg = leg.Item1;
            var goal = LevelPathPlanner.Settle(grid, leg.Item2, solid, hazard);
            float best = float.MaxValue, still = 0f, facing = goal.x >= x ? 1f : -1f;
            bool done = false;
            int cacheKey = -1; List<LevelPathPlanner.Cell> cached = null;
            while (t < MaxSeconds)
            {
                if (System.Math.Abs(x - goal.x) < 0.7f && System.Math.Abs(y - goal.y) < 0.8f) { done = true; break; }
                bool grounded = System.Math.Abs(y - System.Math.Round(y)) < 0.001f && Supported(grid, solid, x, y);
                var wp = goal;
                if (grounded)
                {
                    // 站在哪一格：脚下托着的那格（台边时身体中心可能已经伸出台外）
                    int cx = (int)System.Math.Round(x);
                    if (!LevelPathPlanner.CanStand(grid, cx, (int)y, solid, hazard))
                        foreach (int k in new[] { (int)System.Math.Floor(x), (int)System.Math.Ceiling(x) })
                            if (LevelPathPlanner.CanStand(grid, k, (int)y, solid, hazard)) { cx = k; break; }
                    var here = new LevelPathPlanner.Cell(cx, (int)y);
                    int key = here.x * 1000 + here.y;
                    if (key != cacheKey) { cacheKey = key; cached = LevelPathPlanner.Path(grid, here, goal); }
                    var path = cached;
                    if (path == null) { res.stuckX = x; res.stuckY = y; res.seconds = t; return res; }
                    if (path.Count >= 2)
                    {
                        var n = path[1];
                        bool jump = n.y > here.y || System.Math.Abs(n.x - here.x) >= 2;
                        if (jump && System.Math.Abs(x - here.x) <= 0.45f) { x = n.x; y = n.y; t += 0.4f; continue; } // 起跳（规划器保证落得到）
                        wp = jump ? here : LevelPathPlanner.NextWaypoint(path, x);
                    }
                }
                float dx = wp.x - x, dy = wp.y - y;
                float steer = legacyRule ? (System.Math.Abs(dx) > LevelPathPlanner.ArriveDx ? System.Math.Sign(dx) : 0f)
                                         : LevelPathPlanner.SteerX(dx, dy, grounded, facing);
                if (steer != 0f) facing = steer;
                float nx = x + steer * Speed * Dt;
                if (!Blocked(grid, solid, nx, y)) x = nx;
                if (!grounded || !Supported(grid, solid, x, y))
                {
                    float ny = y - FallSpeed * Dt;
                    int top = (int)System.Math.Floor(y), bottom = (int)System.Math.Ceiling(ny);
                    for (int L = top; L >= bottom; L--)
                        if (L < y + 0.001f && Supported(grid, solid, x, L)) { ny = L; break; }
                    y = ny;
                    if (y < -2f) { res.stuckX = x; res.stuckY = y; res.seconds = t; return res; }
                }
                float d = System.Math.Abs(goal.x - x) + System.Math.Abs(goal.y - y);
                if (d < best - ProgressCells) { best = d; still = 0f; }
                else if ((still += Dt) >= StuckSeconds) { res.stuckX = x; res.stuckY = y; res.seconds = t; return res; }
                t += Dt;
            }
            if (!done) { res.stuckX = x; res.stuckY = y; res.seconds = t; return res; }
        }
        res.ok = true; res.seconds = t;
        return res;
    }

    private static LevelPathPlanner.Cell Find(IList<string> g, char c)
    {
        for (int r = 0; r < g.Count; r++) { int i = g[r].IndexOf(c); if (i >= 0) return new LevelPathPlanner.Cell(i, g.Count - 1 - r); }
        return new LevelPathPlanner.Cell(-1, -1);
    }

    /// <summary>身体（宽 0.8）脚下有没有东西托着（站在第 L 行 = 下面第 L-1 行有实心/单向台面）。</summary>
    public static bool Supported(IList<string> grid, HashSet<char> solid, float x, float L)
    {
        int row = (int)System.Math.Round(L) - 1;
        for (int cx = (int)System.Math.Floor(x - HalfWidth - 0.5f) + 1; cx - 0.5f < x + HalfWidth; cx++)
            if (cx + 0.5f > x - HalfWidth && solid.Contains(LevelPathPlanner.At(grid, cx, row))) return true;
        return false;
    }

    private static bool Blocked(IList<string> grid, HashSet<char> solid, float x, float y)
    {
        int row = (int)System.Math.Round(y);
        foreach (int cx in new[] { (int)System.Math.Round(x - HalfWidth), (int)System.Math.Round(x + HalfWidth) })
        { char c = LevelPathPlanner.At(grid, cx, row); if (solid.Contains(c) && c != '-') return true; }
        return false;
    }

    /// <summary>
    /// S240：临界跳检查（帮搭图）——找出"只能靠往上跳满 2 格才去得了宝物 / 出口"的起跳格。
    /// 2 格正好是马里奥跳高的极限（PhysicsMetrics 最高 2.5 格，AI 还要水平对准 &lt; 2.25 格才起跳）：
    /// 静态检查说"走得通"，真玩时被弹一下、落点偏一点、头顶碰一下就上不去 → 卡在下面（S240 用户截图：箱庭楼梯、坑里）。
    /// 规则：从马里奥出生点走得到的格里，如果允许跳 2 格能到目标、只许跳 1 格就到不了，而且这一格本身有一个"往上 2 格"的跳法 → 标出来。
    /// 返回格键（x*1000+y，与轨迹文件一致）。只用地形（H4）。建议：在这些格旁边加一级台阶（变成两次 1 格跳）。
    /// </summary>
    public static HashSet<int> CriticalJumpCells(IList<string> grid)
    {
        var res = CriticalJumpCells(grid, null);
        // 塌后再查一遍：塌桥 C / 裂缝地板 x 没了，他掉到下面那一格起步（S240 用户截图 1：塌桥坑里出不来）
        var after = Collapsed(grid, out var drops);
        if (drops.Count > 0) res.UnionWith(CriticalJumpCells(after, drops));
        return res;
    }

    /// <summary>S240 纯逻辑：塌后的地图（C、x 变空），以及每个塌掉格正下方的落点（起步格）。</summary>
    public static List<string> Collapsed(IList<string> grid, out List<LevelPathPlanner.Cell> drops)
    {
        var rows = new List<string>(); drops = new List<LevelPathPlanner.Cell>();
        foreach (var r in grid) rows.Add(r.Replace('C', '.').Replace('x', '.'));
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = reg.GetHazardChars();
        for (int r = 0; r < grid.Count; r++)
            for (int x = 0; x < grid[r].Length; x++)
            {
                char c = grid[r][x];
                if (c != 'C' && c != 'x') continue;
                var land = LevelPathPlanner.Settle(rows, new LevelPathPlanner.Cell(x, grid.Count - 1 - r), solid, hazard);
                if (land.x >= 0) drops.Add(land);
            }
        return rows;
    }

    private static HashSet<int> CriticalJumpCells(IList<string> grid, List<LevelPathPlanner.Cell> extraStarts)
    {
        var res = new HashSet<int>();
        if (grid == null || grid.Count == 0) return res;
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = reg.GetHazardChars();
        var m = Find(grid, 'M'); var o = Find(grid, 'o'); var g = Find(grid, 'G');
        if (m.x < 0) return res;
        var start = LevelPathPlanner.Settle(grid, m, solid, hazard);
        if (start.x < 0) return res;
        int w = 0; foreach (var r in grid) if (r.Length > w) w = r.Length;
        int h = grid.Count;
        var targets = new List<LevelPathPlanner.Cell>();
        if (o.x >= 0) targets.Add(o);
        if (g.x >= 0) targets.Add(g);
        // 出生点能走到的格（完整跳法）
        var seen = new HashSet<int> { start.x * 1000 + start.y };
        var q = new Queue<LevelPathPlanner.Cell>(); q.Enqueue(start);
        if (extraStarts != null) foreach (var e in extraStarts) if (seen.Add(e.x * 1000 + e.y)) q.Enqueue(e);
        var cells = new List<LevelPathPlanner.Cell>();
        while (q.Count > 0)
        {
            var c = q.Dequeue(); cells.Add(c);
            foreach (var n in LevelPathPlanner.Moves(grid, c, w, h, solid, hazard))
                if (seen.Add(n.x * 1000 + n.y)) q.Enqueue(n);
            if (cells.Count > 4000) break;
        }
        // "盆地" = 只许跳 1 格就到不了目标的格。标出盆地里"往上跳 2 格正好跳出盆地"的起跳格——那一跳就是唯一出路。
        foreach (var t in targets)
        {
            var ok1 = new Dictionary<int, bool>();
            bool Ok1(LevelPathPlanner.Cell c)
            {
                int k = c.x * 1000 + c.y;
                if (!ok1.TryGetValue(k, out bool v)) { v = LevelPathPlanner.Path(grid, c, t, null, 1) != null; ok1[k] = v; }
                return v;
            }
            foreach (var c in cells)
            {
                if (Ok1(c)) continue;
                foreach (var n in LevelPathPlanner.Moves(grid, c, w, h, solid, hazard))
                    if (n.y - c.y >= 2 && Ok1(n)) { res.Add(c.x * 1000 + c.y); break; }
            }
        }
        return res;
    }
}
