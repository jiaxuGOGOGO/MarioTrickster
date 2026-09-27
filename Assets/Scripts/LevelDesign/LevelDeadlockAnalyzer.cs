using System.Collections.Generic;
using System.Text;

/// <summary>
/// S189：死局 / 卡死分析（纯逻辑，编辑器与运行时共用）。回答"机关触发后，马里奥还能不能把游戏玩下去"（宪法 H9 无卡死）。
///
/// 做法（复用 L2 可达性 BFS，跳跃/下落规则完全相同，不另造物理）：
///   1. 基础检查：马里奥能走到宝物 o，拿到后能回到出口 G（原 L2 只查 M→G，漏了拿宝往返）。
///   2. 机关"最坏情况"预演：把每种玩家机关的效果套进网格——
///        塌桥 C 塌掉（桥下有人时不会长回来，视为持续状态）；封路墙 [ 升起（只持续 3.5 秒，属于暂时阻挡）；
///      然后对马里奥可能站到的每一格（含从桥上掉下去的落点），检查还能不能到达出口。
///        到不了 + 持续状态 = 死局（错误，必须改）；到不了 + 暂时阻挡 = 提示（几秒后恢复）。
///   3. 输出带坐标的问题列表 + 死局格集合（编辑器画热力图，运行时防卡死救援用）。
/// 它不证明关卡好玩，也不模拟 AI 的具体走法；只保证"物理上还有路"。
/// </summary>
public static class LevelDeadlockAnalyzer
{
    public enum Severity { Error, Warning, Info }

    public sealed class Issue
    {
        public Severity severity;
        public int x = -1, y = -1;
        public string message;
        public override string ToString() => (x >= 0 ? $"({x},{y}) " : "") + message;
    }

    public sealed class Report
    {
        public readonly List<Issue> issues = new List<Issue>();
        /// <summary>持续状态下到不了出口的格（死局）。key = LevelReachabilityAnalyzer.CellKey。</summary>
        public readonly HashSet<int> deadlockCells = new HashSet<int>();
        /// <summary>只在暂时阻挡期间到不了出口的格。</summary>
        public readonly HashSet<int> temporaryCells = new HashSet<int>();
        public int standingCellsChecked;
        public bool HasErrors => issues.Exists(i => i.severity == Severity.Error);
        public string Summary()
        {
            var sb = new StringBuilder();
            int e = issues.FindAll(i => i.severity == Severity.Error).Count, w = issues.FindAll(i => i.severity == Severity.Warning).Count;
            sb.Append(e == 0 ? "✓ 没发现死局" : $"✗ {e} 个死局/不可达问题").Append($"，{w} 个提示；检查了 {standingCellsChecked} 个站位。");
            return sb.ToString();
        }
    }

    public const char Loot = 'o', Exit = 'G', Mario = 'M', Bridge = 'C', Blocker = '[';

    /// <summary>分析一个完整关卡（grid 第 0 行在最上面）。</summary>
    public static Report Analyze(IList<string> grid)
    {
        var report = new Report();
        if (grid == null || grid.Count == 0) return report;
        string baseText = string.Join("\n", grid);
        if (!Find(grid, Mario, out int mx, out int my) || !Find(grid, Exit, out int gx, out int gy))
        {
            report.issues.Add(new Issue { severity = Severity.Error, message = "缺少马里奥出生点 M 或出口 G" });
            return report;
        }
        bool hasLoot = Find(grid, Loot, out int ox, out int oy);

        // 1) 基础：M→o→G
        var fromM = LevelReachabilityAnalyzer.ReachableFrom(baseText, mx, my);
        if (hasLoot)
        {
            if (!fromM.Contains(LevelReachabilityAnalyzer.CellKey(ox, oy)))
                report.issues.Add(new Issue { severity = Severity.Error, x = ox, y = oy, message = "马里奥走不到宝物" });
            else if (!LevelReachabilityAnalyzer.ReachableFrom(baseText, ox, oy).Contains(LevelReachabilityAnalyzer.CellKey(gx, gy)))
                report.issues.Add(new Issue { severity = Severity.Error, x = ox, y = oy, message = "拿到宝物后回不到出口" });
        }
        else if (!fromM.Contains(LevelReachabilityAnalyzer.CellKey(gx, gy)))
            report.issues.Add(new Issue { severity = Severity.Error, x = mx, y = my, message = "马里奥走不到出口" });

        // 2) 持续状态：所有塌桥塌掉
        if (Contains(grid, Bridge))
            CheckState(grid, report, mx, my, hasLoot, ox, oy, gx, gy, Bridge, '.', true,
                "塌桥塌掉后，站在这里的马里奥再也回不到出口（死局：给坑里留一条跳出来的路，比如单向台面 -）");
        // 3) 暂时状态：所有封路墙升起
        if (Contains(grid, Blocker))
            CheckState(grid, report, mx, my, hasLoot, ox, oy, gx, gy, Blocker, 'W', false,
                "封路墙升起时这里暂时出不去（3.5 秒后恢复，不算死局）");
        return report;
    }

    /// <summary>把网格里所有 from 字符换成 to（机关触发后的样子）。供编辑器"最坏情况预览"用。</summary>
    public static List<string> ApplyPrankState(IList<string> grid, char from, char to)
    {
        var result = new List<string>(grid.Count);
        foreach (var row in grid) result.Add(row.Replace(from, to));
        return result;
    }

    private static void CheckState(IList<string> grid, Report report, int mx, int my, bool hasLoot, int ox, int oy, int gx, int gy,
        char from, char to, bool persistent, string message)
    {
        var state = ApplyPrankState(grid, from, to);
        string text = string.Join("\n", state);
        int h = grid.Count;
        // 马里奥可能站的格：状态下从 M 与从宝物出发可达的格 + 基础状态可达格 + 机关格正下方的落点
        var candidates = new HashSet<int>();
        string baseText = string.Join("\n", grid);
        candidates.UnionWith(LevelReachabilityAnalyzer.ReachableFrom(baseText, mx, my));
        if (hasLoot) candidates.UnionWith(LevelReachabilityAnalyzer.ReachableFrom(baseText, ox, oy));
        var stateStand = StandableCells(state);
        for (int row = 0; row < h; row++)
            for (int x = 0; x < grid[row].Length; x++)
                if (grid[row][x] == from)
                {
                    int y = h - 1 - row;
                    int landing = FirstLandingBelow(stateStand, x, y);
                    if (landing >= 0) candidates.Add(LevelReachabilityAnalyzer.CellKey(x, landing));
                }
        int exitKey = LevelReachabilityAnalyzer.CellKey(gx, gy);
        var standable = stateStand;
        // S192 性能：原来对每个候选格各跑一次 BFS（~115 次）。严格成立的剪枝：
        // 若 B ∈ Reach(A) 且 Reach(A) 不含出口，则 Reach(B) ⊆ Reach(A) 也不含出口 → B 直接判死，不再 BFS。
        // 不改 L2 算法本身，只减少调用次数。
        var cache = new Dictionary<int, bool>();
        foreach (int key in candidates)
        {
            if (!standable.Contains(key)) continue; // 触发后这格站不住了（例如桥面本身）→ 马里奥会掉到落点，落点已单独加入
            report.standingCellsChecked++;
            int x = key / 100000, y = key % 100000;
            if (!cache.TryGetValue(key, out bool canExit))
            {
                var reach = LevelReachabilityAnalyzer.ReachableFrom(text, x, y);
                canExit = reach.Contains(exitKey);
                cache[key] = canExit;
                if (!canExit) foreach (int b in reach) cache[b] = false; // Reach(B) ⊆ Reach(A)：都到不了出口
            }
            if (canExit) continue;
            if (persistent)
            {
                if (report.deadlockCells.Add(key))
                    report.issues.Add(new Issue { severity = Severity.Error, x = x, y = y, message = message });
            }
            else if (report.temporaryCells.Add(key) && report.temporaryCells.Count <= 3)
                report.issues.Add(new Issue { severity = Severity.Info, x = x, y = y, message = message });
        }
    }

    /// <summary>纯函数：某格往下第一个能站的高度（-1 = 掉出地图）。</summary>
    private static int FirstLandingBelow(IList<string> state, int x, int y) => FirstLandingBelow(StandableCells(state), x, y);

    private static int FirstLandingBelow(HashSet<int> stand, int x, int y)
    {
        for (int ny = y; ny >= 0; ny--) if (stand.Contains(LevelReachabilityAnalyzer.CellKey(x, ny))) return ny;
        return -1;
    }

    /// <summary>能站立的格：自己不是实心、不是危险物，脚下是实心（与 L2 的 CanStandAt 同规则）。</summary>
    public static HashSet<int> StandableCells(IList<string> grid)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars();
        var hazard = reg.GetHazardChars();
        var cells = new HashSet<int>();
        int h = grid.Count;
        for (int row = 0; row < h; row++)
        {
            int y = h - 1 - row;
            for (int x = 0; x < grid[row].Length; x++)
            {
                char c = grid[row][x];
                if (solid.Contains(c) || hazard.Contains(c)) continue;
                bool support = y == 0 || (row + 1 < h && x < grid[row + 1].Length && solid.Contains(grid[row + 1][x]));
                if (support) cells.Add(LevelReachabilityAnalyzer.CellKey(x, y));
            }
        }
        return cells;
    }

    private static bool Contains(IList<string> grid, char c) { foreach (var r in grid) if (r.IndexOf(c) >= 0) return true; return false; }

    private static bool Find(IList<string> grid, char c, out int x, out int y)
    {
        for (int row = 0; row < grid.Count; row++)
        {
            int col = grid[row].IndexOf(c);
            if (col >= 0) { x = col; y = grid.Count - 1 - row; return true; }
        }
        x = y = -1; return false;
    }
}
