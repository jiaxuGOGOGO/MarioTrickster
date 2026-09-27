using System.Collections.Generic;

/// <summary>
/// S195：楼层寻路（纯逻辑，可测试）——多层楼 / 地下监狱里，马里奥要知道"先去哪个楼梯口"。
/// 移动模型与 L2 可达性一致的保守子集：走路、走下台阶、下落、跳（高 ≤ 2 格、按抛物线缩短水平距离）、单向台面可从下穿过。
/// 只用关卡地形与马里奥自己的目标（宝物/出口），不读取捣蛋者任何信息（H4）。
/// 用法：Path(grid, from, to) → 格子路径；NextWaypoint 取"第一次换高度的落点"，交给现有 AI 走过去。
/// </summary>
public static class LevelPathPlanner
{
    public struct Cell { public int x, y; public Cell(int x, int y) { this.x = x; this.y = y; } }

    public const int JumpUp = 2, JumpSide = 4, MaxFall = 30;
    /// <summary>S197：向上跳的最大水平距离。AI 只在目标水平距离 &lt; 2.25 格时起跳（HeuristicBotInputProvider），规划必须与之一致，否则会规划出"跳不上去"的路线（用户反馈马里奥卡住）。</summary>
    public const int JumpUpSide = 2;

    private static bool Solid(char c, HashSet<char> solid) => solid.Contains(c);

    /// <summary>grid 第 0 行在最上面。y 从下往上数。</summary>
    public static char At(IList<string> grid, int x, int y)
    {
        int row = grid.Count - 1 - y;
        if (row < 0 || row >= grid.Count || x < 0 || x >= grid[row].Length) return 'W';
        return grid[row][x];
    }

    public static bool CanStand(IList<string> grid, int x, int y, HashSet<char> solid, HashSet<char> hazard)
    {
        char c = At(grid, x, y);
        if (Solid(c, solid) || hazard.Contains(c)) return false;
        return y == 0 || Solid(At(grid, x, y - 1), solid);
    }

    /// <summary>从 from 到 to 的一条路径（含首尾）；到不了返回 null。to 不可站时取它正下方第一个可站格。</summary>
    public static List<Cell> Path(IList<string> grid, Cell from, Cell to)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars();
        var hazard = reg.GetHazardChars();
        from = Settle(grid, from, solid, hazard);
        to = Settle(grid, to, solid, hazard);
        if (from.x < 0 || to.x < 0) return null;
        var prev = new Dictionary<int, int>();
        var q = new Queue<Cell>();
        int Key(Cell c) => c.x * 1000 + c.y;
        prev[Key(from)] = -1; q.Enqueue(from);
        int w = 0; foreach (var r in grid) if (r.Length > w) w = r.Length;
        int h = grid.Count;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            if (c.x == to.x && c.y == to.y)
            {
                var path = new List<Cell>();
                for (int k = Key(c); k != -1; k = prev[k]) path.Add(new Cell(k / 1000, k % 1000));
                path.Reverse();
                return path;
            }
            foreach (var n in Moves(grid, c, w, h, solid, hazard))
                if (!prev.ContainsKey(Key(n))) { prev[Key(n)] = Key(c); q.Enqueue(n); }
        }
        return null;
    }

    private static Cell Settle(IList<string> grid, Cell c, HashSet<char> solid, HashSet<char> hazard)
    {
        for (int y = c.y; y >= 0; y--) if (CanStand(grid, c.x, y, solid, hazard)) return new Cell(c.x, y);
        return new Cell(-1, -1);
    }

    private static IEnumerable<Cell> Moves(IList<string> grid, Cell c, int w, int h, HashSet<char> solid, HashSet<char> hazard)
    {
        // 走路 / 走下台阶 / 下落
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int nx = c.x + dx;
            if (nx < 0 || nx >= w || Solid(At(grid, nx, c.y), solid)) continue;
            for (int y = c.y; y >= System.Math.Max(0, c.y - MaxFall); y--)
            {
                if (Solid(At(grid, nx, y), solid)) break;
                if (CanStand(grid, nx, y, solid, hazard)) { yield return new Cell(nx, y); break; }
            }
        }
        // 从当前格正下方掉（站在单向台面上不能往下掉——本模型保守：不算）
        // 跳：向上 1..JumpUp、水平 0..JumpSide（按抛物线缩短），头顶只允许单向台面
        for (int dy = 1; dy <= JumpUp; dy++)
        {
            bool headClear = true;
            for (int k = 1; k <= dy; k++) { char above = At(grid, c.x, c.y + k); if (Solid(above, solid) && above != '-') { headClear = false; break; } }
            if (!headClear) break;
            int side = JumpUpSide;
            for (int dx = -side; dx <= side; dx++)
            {
                int nx = c.x + dx, ny = c.y + dy;
                if (nx < 0 || nx >= w || ny >= h) continue;
                if (!CanStand(grid, nx, ny, solid, hazard)) continue;
                if (!ArcClear(grid, c.x, nx, c.y + dy, solid)) continue;
                yield return new Cell(nx, ny);
            }
        }
        // 平跳过坑：同高或更低，水平 2..JumpSide
        for (int dx = -JumpSide; dx <= JumpSide; dx++)
        {
            if (System.Math.Abs(dx) < 2) continue;
            int nx = c.x + dx;
            if (nx < 0 || nx >= w || !ArcClear(grid, c.x, nx, c.y + 1, solid)) continue;
            for (int y = c.y; y >= System.Math.Max(0, c.y - MaxFall); y--)
            {
                if (Solid(At(grid, nx, y), solid)) break;
                if (CanStand(grid, nx, y, solid, hazard)) { yield return new Cell(nx, y); break; }
            }
        }
    }

    /// <summary>跳的最高那一行（apexY）上，起点到落点之间不能有实心墙（单向台面可穿）。</summary>
    private static bool ArcClear(IList<string> grid, int x0, int x1, int apexY, HashSet<char> solid)
    {
        int a = System.Math.Min(x0, x1), b = System.Math.Max(x0, x1);
        for (int x = a; x <= b; x++) { char c = At(grid, x, apexY); if (Solid(c, solid) && c != '-') return false; }
        return true;
    }

    /// <summary>
    /// 下一个路点：沿路径走，遇到第一次"换高度"的格就停在那里（跳上去/掉下去的落点）；全程同高则返回终点。
    /// </summary>
    /// <summary>
    /// S197：带"起跳点"的路点——换高度前先走到起跳格（同层），站到起跳格上（±0.4 格）后才把目标换成落点（此时 AI 会起跳）。
    /// 原来直接给落点：马里奥会走到平台正下方、头顶撞楼板，卡住。
    /// </summary>
    public static Cell NextWaypoint(List<Cell> path, float fromX)
    {
        if (path == null || path.Count == 0) return new Cell(-1, -1);
        int y0 = path[0].y;
        for (int i = 1; i < path.Count; i++)
        {
            if (path[i].y == y0) continue;
            var takeoff = path[i - 1];
            if (path[i].y > y0 && System.Math.Abs(fromX - takeoff.x) > 0.4f) return takeoff; // 往上：先站到起跳点
            return path[i];                                                                 // 往下：直接走过去掉下去
        }
        return path[path.Count - 1];
    }

    public static Cell NextWaypoint(List<Cell> path)
    {
        if (path == null || path.Count == 0) return new Cell(-1, -1);
        int y0 = path[0].y;
        for (int i = 1; i < path.Count; i++) if (path[i].y != y0) return path[i];
        return path[path.Count - 1];
    }

    /// <summary>这张图需不需要楼层寻路：宝物与出口不在同一层（高度差 > 2 格）。默认恶作剧房间 = 不需要（行为不变）。</summary>
    public static bool NeedsPlanning(IList<string> grid)
    {
        int oy = -1, gy = -1;
        for (int row = 0; row < grid.Count; row++)
        {
            if (grid[row].IndexOf('o') >= 0) oy = grid.Count - 1 - row;
            if (grid[row].IndexOf('G') >= 0) gy = grid.Count - 1 - row;
        }
        return oy >= 0 && gy >= 0 && System.Math.Abs(oy - gy) > 2;
    }
}
