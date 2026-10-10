using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// S202：策略模拟（纯逻辑，编辑器/运行时/沙盒共用）——回答两个"静态死局检查"答不了的问题：
///
/// 1. **炸弹策略死局**：捣蛋者有 3 颗炸弹、能炸掉普通地形（# W -）。一个"最坏的对手"会不会把楼梯/台阶炸掉，
///    让马里奥掉进一个再也爬不出来的坑、或者再也拿不到宝？（宪法 H9 无卡死 / H1 总有路）
///    做法：贪心对手——把炸弹放在每个能站的格子上试炸（半径同游戏），挑"让马里奥可活动区域缩得最多"的那一颗，
///    最多放 bombs 颗；只要出现"原来能回家、炸完回不去"的站位 → 找到一个陷阱（报告炸弹位置）。
///    **加固**（Reinforce）：把陷阱用到的"承重格"锁成炸不掉，重复直到对手再也困不住他。游戏里这些格会画上铆钉（H6 看得见）。
/// 2. **路线时间线**：马里奥按楼层寻路走 M → 宝 → 出口，估算用时、途经哪些机关（按时间顺序）、哪些机关离路线太远用不上。
///
/// 移动模型 = LevelPathPlanner（与马里奥 AI 实际能力一致的保守子集），只看地形，不看捣蛋者（H4）。
/// </summary>
public static class StrategySim
{
    public const string BombableChars = "#W-";
    public struct Cell { public int x, y; public Cell(int x, int y) { this.x = x; this.y = y; } public override string ToString() => $"({x},{y})"; }

    public sealed class Trap
    {
        public readonly List<Cell> bombs = new List<Cell>();
        public readonly List<Cell> removed = new List<Cell>();
        public readonly List<Cell> victims = new List<Cell>();
        public bool lootLost;
        public string Describe() => $"在 {string.Join("、", bombs)} 放炸弹后，站在 {string.Join("、", victims.Take(3))}{(victims.Count > 3 ? " 等" : "")} 的马里奥" + (lootLost ? "再也拿不到宝" : "再也回不了出口");
    }

    public sealed class RouteStop { public char ch; public int x, y; public float at; }

    public sealed class Report
    {
        public List<LevelPathPlanner.Cell> route;
        public float routeSeconds;
        public readonly List<RouteStop> onRoute = new List<RouteStop>();
        public readonly List<Cell> offRoute = new List<Cell>();
        public Trap trapBeforeReinforce;
        public readonly HashSet<int> reinforced = new HashSet<int>();
        public bool trapAfterReinforce;
        public readonly List<string> warnings = new List<string>();
        public string Summary()
        {
            var sb = new StringBuilder();
            if (route == null) sb.Append("✗ 马里奥按寻路走不通（M→宝→出口）");
            else sb.Append($"马里奥走完一趟约 {routeSeconds:F0} 秒，途经 {onRoute.Count} 个机关");
            if (offRoute.Count > 0) sb.Append($"，{offRoute.Count} 个机关离路线太远");
            sb.Append(trapBeforeReinforce == null ? "；炸弹困不住他 ✓" : $"；炸弹能困住他 → 已自动加固 {reinforced.Count} 格{(trapAfterReinforce ? "（仍有风险 ✗）" : " ✓")}");
            return sb.ToString();
        }
    }

    public static int Key(int x, int y) => x * 1000 + y;
    public static bool Locked(int x, int y, int w, int h) => x <= 0 || x >= w - 1 || y <= 0 || y >= h - 1;

    // ── 图 ─────────────────────────────────────────────
    private sealed class Graph
    {
        public readonly Dictionary<int, List<int>> fwd = new Dictionary<int, List<int>>();
        public readonly Dictionary<int, List<int>> rev = new Dictionary<int, List<int>>();
    }

    private static Graph Build(IList<string> g, HashSet<char> solid, HashSet<char> hazard)
    {
        var gr = new Graph();
        int h = g.Count, w = 0; foreach (var r in g) if (r.Length > w) w = r.Length;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!LevelPathPlanner.CanStand(g, x, y, solid, hazard)) continue;
                int k = Key(x, y);
                if (!gr.fwd.TryGetValue(k, out var f)) gr.fwd[k] = f = new List<int>();
                foreach (var n in LevelPathPlanner.Moves(g, new LevelPathPlanner.Cell(x, y), w, h, solid, hazard))
                {
                    int nk = Key(n.x, n.y);
                    f.Add(nk);
                    if (!gr.rev.TryGetValue(nk, out var rl)) gr.rev[nk] = rl = new List<int>();
                    rl.Add(k);
                }
            }
        return gr;
    }

    private static HashSet<int> Bfs(Dictionary<int, List<int>> edges, int start)
    {
        var seen = new HashSet<int> { start };
        var q = new Queue<int>(); q.Enqueue(start);
        while (q.Count > 0) { int c = q.Dequeue(); if (!edges.TryGetValue(c, out var l)) continue; foreach (int n in l) if (seen.Add(n)) q.Enqueue(n); }
        return seen;
    }

    private static bool Find(IList<string> g, char c, out Cell cell)
    {
        for (int row = 0; row < g.Count; row++) { int x = g[row].IndexOf(c); if (x >= 0) { cell = new Cell(x, g.Count - 1 - row); return true; } }
        cell = new Cell(-1, -1); return false;
    }

    private static int SettleKey(IList<string> g, int x, int y, HashSet<char> solid, HashSet<char> hazard)
    {
        var s = LevelPathPlanner.Settle(g, new LevelPathPlanner.Cell(x, y), solid, hazard);
        return s.x < 0 ? -1 : Key(s.x, s.y);
    }

    /// <summary>最坏情况底图：塌桥/裂缝地板已打开（'.'），捷径门当作能过（马里奥会踢门，S199），其余不变。</summary>
    public static List<string> WorstBase(IList<string> grid)
    {
        var rows = LevelDeadlockAnalyzer.ApplyPrankState(grid, "Cx|", '.');
        for (int i = 0; i < rows.Count; i++) rows[i] = Step1Layout.StripSlots(rows[i]);
        return rows;
    }

    /// <summary>纯逻辑：一颗炸弹（站在 center 格放下）会炸掉哪些格（可炸字符、非锁定、非加固）。</summary>
    public static List<Cell> BlastCells(IList<string> g, Cell center, float radius, ICollection<int> reinforced)
    {
        var list = new List<Cell>();
        int h = g.Count, w = g[0].Length, r = (int)System.Math.Ceiling(radius);
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = center.x + dx, y = center.y + dy;
                if (x < 0 || y < 0 || x >= w || y >= h || dx * dx + dy * dy > radius * radius) continue;
                if (Locked(x, y, w, h) || (reinforced != null && reinforced.Contains(Key(x, y)))) continue;
                if (BombableChars.IndexOf(LevelPathPlanner.At(g, x, y)) < 0) continue;
                list.Add(new Cell(x, y));
            }
        return list;
    }

    private static List<string> Remove(IList<string> g, IEnumerable<Cell> cells)
    {
        var rows = g.Select(r => r.ToCharArray()).ToList();
        int h = g.Count;
        foreach (var c in cells) rows[h - 1 - c.y][c.x] = '.';
        return rows.Select(r => new string(r)).ToList();
    }

    private struct Eval { public int dead; public int alive; public List<Cell> victims; public bool lootLost; }

    /// <summary>对某个状态打分：原来能回家（且能拿宝）的站位里，现在有几个不行了。</summary>
    private static Eval Score(IList<string> state, List<int> plausible, HashSet<int> baseDead, Cell exit, Cell loot, bool hasLoot, HashSet<char> solid, HashSet<char> hazard)
    {
        var gr = Build(state, solid, hazard);
        int ek = SettleKey(state, exit.x, exit.y, solid, hazard);
        var home = ek >= 0 ? Bfs(gr.rev, ek) : new HashSet<int>();
        HashSet<int> toLoot = null; int lk = -1;
        if (hasLoot) { lk = SettleKey(state, loot.x, loot.y, solid, hazard); toLoot = lk >= 0 ? Bfs(gr.rev, lk) : new HashSet<int>(); }
        bool lootHome = !hasLoot || (lk >= 0 && home.Contains(lk));
        var e = new Eval { alive = home.Count, victims = new List<Cell>() };
        foreach (int p in plausible)
        {
            if (baseDead.Contains(p)) continue;
            int s = SettleKey(state, p / 1000, p % 1000, solid, hazard);
            bool ok = s >= 0 && home.Contains(s) && (toLoot == null || toLoot.Contains(s)) && lootHome;
            if (!ok) { e.dead++; if (e.victims.Count < 8) e.victims.Add(new Cell(p / 1000, p % 1000)); if (!lootHome || (toLoot != null && s >= 0 && !toLoot.Contains(s) && home.Contains(s))) e.lootLost = true; }
        }
        return e;
    }

    /// <summary>
    /// 贪心对手找炸弹陷阱。bombs = 炸弹数，radius = 爆炸半径（格）。reinforced = 已加固（炸不掉）的格。找不到返回 null。
    /// </summary>
    public static Trap FindBombTrap(IList<string> grid, int bombs, float radius, ICollection<int> reinforced = null, int beam = 3, float barrelRadius = 0f)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = new HashSet<char>(); // 第 1 步危险物都是限时的（火只在喷时伤人），不算永久站不住
        var baseState = WorstBase(grid);
        if (barrelRadius > 0f) baseState = ExplodeBarrels(baseState, barrelRadius, reinforced);
        if (!Find(baseState, 'G', out var exit) || !Find(baseState, 'M', out var m)) return null;
        bool hasLoot = Find(baseState, 'o', out var loot);
        var gr0 = Build(baseState, solid, hazard);
        int mk = SettleKey(baseState, m.x, m.y, solid, hazard);
        if (mk < 0) return null;
        var plausibleSet = Bfs(gr0.fwd, mk);
        if (hasLoot) { int lk = SettleKey(baseState, loot.x, loot.y, solid, hazard); if (lk >= 0 && plausibleSet.Contains(lk)) plausibleSet.UnionWith(Bfs(gr0.fwd, lk)); }
        var plausible = plausibleSet.ToList();
        var baseEval = Score(baseState, plausible, new HashSet<int>(), exit, loot, hasLoot, solid, hazard);
        var baseDead = new HashSet<int>();
        foreach (int p in plausible)
        {
            // 底图里就回不去的格不算炸弹造成的（那是死局检查的事）
            var one = Score(baseState, new List<int> { p }, new HashSet<int>(), exit, loot, hasLoot, solid, hazard);
            if (one.dead > 0) baseDead.Add(p);
        }
        var frontier = new List<(List<string> state, List<Cell> bombs, List<Cell> removed, int alive)> { (baseState, new List<Cell>(), new List<Cell>(), baseEval.alive) };
        for (int k = 0; k < bombs; k++)
        {
            var next = new List<(List<string>, List<Cell>, List<Cell>, int)>();
            foreach (var (state, used, removedSoFar, _) in frontier)
            {
                var seen = new HashSet<string>();
                int h = state.Count, w = state[0].Length;
                for (int y = 1; y < h - 1; y++)
                    for (int x = 1; x < w - 1; x++)
                    {
                        if (!LevelPathPlanner.CanStand(state, x, y, solid, hazard)) continue;
                        var cells = BlastCells(state, new Cell(x, y), radius, reinforced);
                        // 只有炸掉"承重格"（上面有人能站的实心格）才可能让人回不去；纯炸墙只会多开路
                        var supports = cells.Where(c => LevelPathPlanner.CanStand(state, c.x, c.y + 1, solid, hazard)).ToList();
                        if (supports.Count == 0) continue;
                        string sig = string.Join(";", cells.Select(c => Key(c.x, c.y)));
                        if (!seen.Add(sig)) continue;
                        var ns = Remove(state, cells);
                        var ev = Score(ns, plausible, baseDead, exit, loot, hasLoot, solid, hazard);
                        var nb = new List<Cell>(used) { new Cell(x, y) };
                        var nr = new List<Cell>(removedSoFar); nr.AddRange(cells);
                        if (ev.dead > 0)
                        {
                            var trap = new Trap { lootLost = ev.lootLost };
                            trap.bombs.AddRange(nb); trap.removed.AddRange(nr); trap.victims.AddRange(ev.victims);
                            return trap;
                        }
                        next.Add((ns, nb, nr, ev.alive));
                    }
            }
            if (next.Count == 0) break;
            frontier = next.OrderBy(n => n.Item4).Take(System.Math.Max(1, beam)).ToList();
        }
        return null;
    }

    /// <summary>油桶全部炸掉之后的样子（油桶本身变空气，周围可炸格被炸掉）。</summary>
    public static List<string> ExplodeBarrels(IList<string> g, float radius, ICollection<int> reinforced)
    {
        var cells = new List<Cell>(); int h = g.Count;
        for (int row = 0; row < h; row++)
            for (int x = 0; x < g[row].Length; x++)
                if (g[row][x] == 'U') { var c = new Cell(x, h - 1 - row); cells.Add(c); cells.AddRange(BlastCells(g, c, radius, reinforced)); }
        return cells.Count == 0 ? g.ToList() : Remove(g, cells);
    }

    /// <summary>加固：反复找陷阱，把陷阱炸掉的承重格锁住，直到困不住（最多 maxRounds 轮）。返回加固格（Key）。</summary>
    public static HashSet<int> Reinforce(IList<string> grid, int bombs, float radius, out Trap first, out bool stillTrapped, int maxRounds = 24, float barrelRadius = 0f)
    {
        bool hasBarrels = barrelRadius > 0f && grid.Any(r => r.IndexOf('U') >= 0);
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = new HashSet<char>();
        var baseState = WorstBase(grid);
        var locked = new HashSet<int>();
        first = null; stillTrapped = false;
        for (int round = 0; round < maxRounds; round++)
        {
            var trap = FindBombTrap(grid, bombs, radius, locked);
            if (trap == null && hasBarrels) trap = FindBombTrap(grid, bombs, radius, locked, 3, barrelRadius); // 最坏：油桶也全炸了
            if (trap == null) { stillTrapped = false; return locked; }
            if (first == null) first = trap;
            int before = locked.Count;
            foreach (var c in trap.removed)
                if (BombableChars.IndexOf(LevelPathPlanner.At(baseState, c.x, c.y)) >= 0 && (LevelPathPlanner.CanStand(baseState, c.x, c.y + 1, solid, hazard) || c.y + 1 < baseState.Count && LevelPathPlanner.At(baseState, c.x, c.y) == '-'))
                    locked.Add(Key(c.x, c.y));
            if (locked.Count == before) foreach (var c in trap.removed) locked.Add(Key(c.x, c.y));
        }
        stillTrapped = FindBombTrap(grid, bombs, radius, locked) != null || (hasBarrels && FindBombTrap(grid, bombs, radius, locked, 3, barrelRadius) != null);
        return locked;
    }

    /// <summary>路线时间线 + 炸弹策略（加固）。speedCellsPerSecond = 马里奥赶路速度（格/秒），startDelay = 开局等待秒数。</summary>
    public static Report Analyze(IList<string> grid, float speedCellsPerSecond, float startDelay, int bombs, float radius, float nearCells = 1.5f, float farCells = 3f, float barrelRadius = 0f)
    {
        var rep = new Report();
        var g = grid.Select(Step1Layout.StripSlots).ToList();
        LevelPathPlanner.Cell F(char c) { for (int r = 0; r < g.Count; r++) { int x = g[r].IndexOf(c); if (x >= 0) return new LevelPathPlanner.Cell(x, g.Count - 1 - r); } return new LevelPathPlanner.Cell(-1, -1); }
        var m = F('M'); var o = F('o'); var e = F('G');
        var a = LevelPathPlanner.Path(g, m, o.x >= 0 ? o : e);
        var b = o.x >= 0 ? LevelPathPlanner.Path(g, o, e) : new List<LevelPathPlanner.Cell>();
        if (a != null && b != null)
        {
            rep.route = new List<LevelPathPlanner.Cell>(a); rep.route.AddRange(b.Skip(1));
            float len = 0f; var times = new List<float> { 0f };
            for (int i = 1; i < rep.route.Count; i++)
            {
                var p = rep.route[i - 1]; var q = rep.route[i];
                len += (float)System.Math.Sqrt((q.x - p.x) * (q.x - p.x) + (q.y - p.y) * (q.y - p.y));
                times.Add(startDelay + len / System.Math.Max(0.5f, speedCellsPerSecond));
            }
            rep.routeSeconds = startDelay + len / System.Math.Max(0.5f, speedCellsPerSecond);
            int h = g.Count;
            for (int row = 0; row < h; row++)
                for (int x = 0; x < g[row].Length; x++)
                {
                    char ch = g[row][x];
                    var info = ElementCatalog.Get(ch);
                    if (info == null || !(info.role == ElementCatalog.Role.PlayerPrank || ComboRouteAnalyzer.IsChainPart(ch))) continue;
                    int y = h - 1 - row;
                    float best = float.MaxValue; int bi = 0;
                    for (int i = 0; i < rep.route.Count; i++)
                    {
                        float dx = rep.route[i].x - x, dy = rep.route[i].y - y;
                        float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (d < best) { best = d; bi = i; }
                    }
                    if (best <= nearCells + (ch == 'K' || ch == 'k' ? 4f : 0f)) rep.onRoute.Add(new RouteStop { ch = ch, x = x, y = y, at = times[bi] });
                    else if (best > farCells) rep.offRoute.Add(new Cell(x, y));
                }
            rep.onRoute.Sort((p, q) => p.at.CompareTo(q.at));
        }
        if (bombs > 0)
        {
            var locked = Reinforce(g, bombs, radius, out var first, out bool still, 24, barrelRadius);
            rep.trapBeforeReinforce = first; rep.trapAfterReinforce = still;
            rep.reinforced.UnionWith(locked);
            if (first != null) rep.warnings.Add("炸弹策略：" + first.Describe() + $" → 游戏里已把 {locked.Count} 个承重格加固（画铆钉，炸不掉）");
            if (still) rep.warnings.Add("✗ 加固后仍能困住马里奥：请给坑里多留一条回去的路（单向台面 -）");
        }
        if (rep.route == null) rep.warnings.Add("✗ 马里奥按楼层寻路走不通 M→宝→出口（跳跃间距可能刚好在临界区，H8）");
        if (rep.offRoute.Count > 0) rep.warnings.Add($"{rep.offRoute.Count} 个机关离他的路线超过 {farCells:F0} 格：只能靠挑衅/诱饵把他引过去");
        return rep;
    }

    /// <summary>S207：回合时间（秒）= max(基础时间, 马里奥一趟估算秒数 × 倍数)。默认房间 20s×3=60 &lt; 150 → 不变；长关卡自动放宽，防止"还没走到就超时"。</summary>
    public static float RoundTimeLimit(float baseLimit, float routeSeconds, float perRouteSecond) =>
        System.Math.Max(baseLimit, routeSeconds * System.Math.Max(0f, perRouteSecond));

    /// <summary>S207：自动检查超时（秒）= max(基础, 估算秒数 × 倍数 + 余量)。长关卡不会被误判"卡住"。</summary>
    public static float HandsOffTimeout(float baseTimeout, float routeSeconds, float perRouteSecond, float margin) =>
        System.Math.Max(baseTimeout, routeSeconds * System.Math.Max(0f, perRouteSecond) + margin);

    /// <summary>马里奥赶路速度（格/秒）估算 = 物理最高速 × 调参倍率。</summary>
    public static float RunSpeed(float maxSpeed, float speedScale) => System.Math.Max(0.5f, maxSpeed * speedScale);

    /// <summary>稳定的关卡指纹（FNV-1a），用于"自动检查轨迹"和当前画布对上号。</summary>
    public static string Hash(string text)
    {
        unchecked
        {
            uint hsh = 2166136261;
            foreach (char c in (text ?? "").Replace("\r", "")) { hsh ^= c; hsh *= 16777619; }
            return hsh.ToString("x8");
        }
    }
}
