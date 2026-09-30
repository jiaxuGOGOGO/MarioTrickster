using System.Collections.Generic;

/// <summary>
/// S196：箱庭结构分析（纯逻辑，关卡工坊"箱庭总览"用）。回答设计者最关心的 4 个问题：
///   1. 楼层：这张图有几层？每层的"身份"（主机关、藏身处）是什么——每层是不是有自己的特色？
///   2. 连接：层与层之间靠什么连（楼梯口、裂缝地板、弹簧、捷径门、裂墙）？有几条路？
///   3. 环路：从出口出发、拿宝、回出口，有没有**不走回头路的第二条路**（环 = 箱庭的核心）？
///   4. 捷径：捷径门 / 裂墙打开后，回程缩短了多少格？（"打通的爽感"可量化）
/// 规则：只看网格；死局与可达性仍以 LevelDeadlockAnalyzer 为准（门/裂墙按关着算）。这里是设计提示。
/// 参考：Level Design Book《Undead Burg》（主路 + 绕回主路的支路、单向下落、打开捷径）；
///       Team Cherry 谈 Hollow Knight 地图（"尽可能多的区域间连接"，真实空间让人能在脑中建图）。
/// </summary>
public static class HakoniwaAnalyzer
{
    public sealed class Floor
    {
        public int index;          // 0 = 最上层
        public int yMin, yMax;     // 这层可站立的高度范围（格，y 从下往上）
        public readonly Dictionary<string, int> pranks = new Dictionary<string, int>();
        public int cover;          // 藏身处数量（箱子、草丛）
        public string Identity
        {
            get
            {
                string main = ""; int best = 0;
                foreach (var kv in pranks) if (kv.Value > best) { best = kv.Value; main = kv.Key; }
                return main == "" ? "（没有机关）" : main + (pranks.Count > 1 ? $" 等 {pranks.Count} 种" : "");
            }
        }
    }

    public sealed class Connection
    {
        public int upper, lower;   // 楼层下标
        public int x, y;           // 连接点（格）
        public string kind;        // 楼梯口 / 裂缝地板 / 弹簧 / 单向台面 / 捷径门 / 裂墙
        public bool shortcut;      // 需要"打开"才生效（门、裂墙、裂缝）
    }

    public sealed class Result
    {
        public readonly List<Floor> floors = new List<Floor>();
        public readonly List<Connection> links = new List<Connection>();
        public int routeLength = -1;          // 最坏情况（门关、墙不破）M→o→G 的步数
        public int shortcutRouteLength = -1;  // 全部捷径打开后的步数
        public bool hasLoop;                  // 有环（至少两条不同的楼层间连接连着同一对楼层，或回程能不走原路）
        public readonly List<string> advice = new List<string>();
        public int ShortcutSaves => routeLength > 0 && shortcutRouteLength > 0 ? routeLength - shortcutRouteLength : 0;
        public string Summary
        {
            get
            {
                string s = $"{floors.Count} 层，{links.Count} 处层间连接";
                if (routeLength > 0) s += $"；通关路线 {routeLength} 步";
                if (ShortcutSaves > 0) s += $"，打通捷径后省 {ShortcutSaves} 步";
                s += hasLoop ? "；有环路 ✓" : "；没有环路（线性）";
                return s;
            }
        }
    }

    public const string Openers = "|%x";   // 打开后变空气的"捷径类"元素

    /// <summary>grid 第 0 行在最上面。</summary>
    public static Result Analyze(IList<string> grid)
    {
        var r = new Result();
        int h = grid.Count;
        if (h == 0) return r;
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars();
        var hazard = reg.GetHazardChars();

        // 1) 楼层 = 被"几乎整行实心"的楼板行分开的带子
        var slabRows = new List<int>();
        for (int row = 1; row < h - 1; row++)
        {
            string line = grid[row];
            int solidCount = 0, inner = System.Math.Max(1, line.Length - 2);
            for (int x = 1; x < line.Length - 1; x++) if (solid.Contains(line[x]) && line[x] != '-') solidCount++;
            if (solidCount >= inner * 0.6f) slabRows.Add(row);
        }
        // 底部地面行（最后一行或倒数第二行实心）也视为楼板
        var bands = new List<(int top, int bottom)>(); // 行号区间（含），楼板之间
        int prev = 0;
        foreach (int s in slabRows) { if (s - prev > 1) bands.Add((prev + 1, s - 1)); prev = s; }
        if (h - 1 - prev > 1) bands.Add((prev + 1, h - 2));
        for (int i = 0; i < bands.Count; i++)
        {
            var f = new Floor { index = i, yMin = h - 1 - bands[i].bottom, yMax = h - 1 - bands[i].top };
            for (int row = bands[i].top; row <= System.Math.Min(h - 1, bands[i].bottom + 1); row++)
                foreach (char c in grid[row])
                {
                    var info = ElementCatalog.Get(c);
                    if (info == null) continue;
                    if (info.role == ElementCatalog.Role.PlayerPrank) { f.pranks.TryGetValue(info.zh, out int n); f.pranks[info.zh] = n + 1; }
                    if (c == 'c' || c == 'b' || c == '1') f.cover++;
                }
            r.floors.Add(f);
        }

        // 2) 层间连接：楼板行上的洞 / 裂缝 / 门 / 裂墙
        for (int k = 0; k < slabRows.Count; k++)
        {
            int row = slabRows[k];
            int upper = FloorAbove(bands, row), lower = FloorBelow(bands, row);
            if (upper < 0 || lower < 0) continue;
            string line = grid[row];
            for (int x = 1; x < line.Length - 1; x++)
            {
                char c = line[x];
                string kind = null; bool sc = false;
                if (c == 'x') { kind = "裂缝地板"; sc = true; }
                else if (c == '-') kind = "单向台面";
                else if (c == '%') { kind = "裂墙"; sc = true; }
                else if (c == '|') { kind = "捷径门"; sc = true; }
                else if (!solid.Contains(c) && !hazard.Contains(c)) kind = "楼梯口";
                if (kind == null) continue;
                // 合并同一段
                if (r.links.Count > 0) { var last = r.links[r.links.Count - 1]; if (last.upper == upper && last.kind == kind && last.y == h - 1 - row && x - last.x <= 3) continue; }
                r.links.Add(new Connection { upper = upper, lower = lower, x = x, y = h - 1 - row, kind = kind, shortcut = sc });
            }
        }
        // 同层内的捷径门 / 裂墙（竖墙上的）也算连接（同层两个区域）
        for (int row = 0; row < h; row++)
        {
            if (slabRows.Contains(row)) continue;
            for (int x = 1; x < grid[row].Length - 1; x++)
            {
                char c = grid[row][x];
                if (c != '|' && c != '%') continue;
                int f = FloorOfRow(bands, row);
                if (f < 0) continue;
                if (r.links.Exists(l => l.x == x && System.Math.Abs(l.y - (h - 1 - row)) <= 2 && l.kind == (c == '|' ? "捷径门" : "裂墙"))) continue;
                r.links.Add(new Connection { upper = f, lower = f, x = x, y = h - 1 - row, kind = c == '|' ? "捷径门" : "裂墙", shortcut = true });
            }
        }

        // 3) 路线长度（最坏 / 全部打开）
        r.routeLength = RouteLength(grid);
        var opened = LevelDeadlockAnalyzer.ApplyPrankState(grid, Openers, '.');
        r.shortcutRouteLength = RouteLength(opened);

        // 4) 环路：去程与回程走的格子重叠不到一半 / 或同一对楼层之间有 ≥2 处连接
        var pairCount = new Dictionary<(int, int), int>();
        foreach (var l in r.links) { var key = (System.Math.Min(l.upper, l.lower), System.Math.Max(l.upper, l.lower)); pairCount.TryGetValue(key, out int n); pairCount[key] = n + 1; }
        foreach (var kv in pairCount) if (kv.Value >= 2) r.hasLoop = true;
        if (!r.hasLoop && r.shortcutRouteLength > 0 && r.routeLength > 0 && r.shortcutRouteLength < r.routeLength) r.hasLoop = true;

        // 5) 建议（箱庭检查清单）
        var identities = new HashSet<string>();
        foreach (var f in r.floors)
        {
            if (f.pranks.Count == 0) r.advice.Add($"第 {f.index + 1} 层没有机关：每层最好有一个'主题机关'（身份）");
            else if (!identities.Add(f.Identity)) r.advice.Add($"第 {f.index + 1} 层和别的层主机关相同（{f.Identity}）：换一个，让每层有辨识度");
            if (f.cover == 0) r.advice.Add($"第 {f.index + 1} 层没有藏身处：捣蛋者在这层无处可躲");
        }
        if (r.floors.Count >= 2 && !r.hasLoop) r.advice.Add("没有环路：加一扇捷径门 | 或裂墙 %，让回程能绕回起点附近（箱庭的核心）");
        if (r.floors.Count >= 2 && !r.links.Exists(l => l.shortcut)) r.advice.Add("没有需要'打开'的捷径：加捷径门 | / 裂墙 % / 裂缝地板 x");
        return r;
    }

    /// <summary>
    /// 捷径门开启侧（纯逻辑，构建器用）：门左右两格里，从出生点走过去**更远**的一侧 = 开启侧（先绕远路才能打开）。
    /// 返回 true = 从左边开。两侧都到不了时默认从右开。
    /// </summary>
    public static bool DoorOpensFromLeft(IList<string> grid, int doorX, int doorY)
    {
        var m = Find(grid, 'M');
        if (m.x < 0) return false;
        int L = PathLen(grid, m, new LevelPathPlanner.Cell(doorX - 1, doorY));
        int R = PathLen(grid, m, new LevelPathPlanner.Cell(doorX + 1, doorY));
        if (L < 0 && R < 0) return false;
        if (L < 0) return false;
        if (R < 0) return true;
        return L > R;
    }

    private static int PathLen(IList<string> grid, LevelPathPlanner.Cell a, LevelPathPlanner.Cell b)
    {
        var p = LevelPathPlanner.Path(grid, a, b);
        if (p == null) return -1;
        var end = p[p.Count - 1];
        return end.x == b.x ? p.Count : -1; // 落点必须真的在门旁那一列
    }

    private static int FloorAbove(List<(int top, int bottom)> bands, int row) { for (int i = bands.Count - 1; i >= 0; i--) if (bands[i].bottom < row) return i; return -1; }
    private static int FloorBelow(List<(int top, int bottom)> bands, int row) { for (int i = 0; i < bands.Count; i++) if (bands[i].top > row) return i; return -1; }
    private static int FloorOfRow(List<(int top, int bottom)> bands, int row) { for (int i = 0; i < bands.Count; i++) if (row >= bands[i].top && row <= bands[i].bottom) return i; return -1; }

    /// <summary>M→o→G 路径长度（格数）；到不了返回 -1。</summary>
    public static int RouteLength(IList<string> grid)
    {
        var m = Find(grid, 'M'); var o = Find(grid, 'o'); var g = Find(grid, 'G');
        if (m.x < 0 || g.x < 0) return -1;
        if (o.x < 0) { var p = LevelPathPlanner.Path(grid, m, g); return p != null ? p.Count : -1; }
        var a = LevelPathPlanner.Path(grid, m, o); var b = LevelPathPlanner.Path(grid, o, g);
        return a != null && b != null ? a.Count + b.Count : -1;
    }

    private static LevelPathPlanner.Cell Find(IList<string> grid, char c)
    {
        for (int row = 0; row < grid.Count; row++) { int x = grid[row].IndexOf(c); if (x >= 0) return new LevelPathPlanner.Cell(x, grid.Count - 1 - row); }
        return new LevelPathPlanner.Cell(-1, -1);
    }
}
