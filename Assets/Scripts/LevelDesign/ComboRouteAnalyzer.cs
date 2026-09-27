using System.Collections.Generic;

/// <summary>
/// S193：连招路线分析（纯逻辑，关卡工坊"连招路线"开关用）。
/// 问题：设计关卡时看不出"哪几个机关能连成一套"。
/// 规则（只用网格，不跑物理）：
///   - 只看玩家机关（ElementCatalog.Role.PlayerPrank）；
///   - 两个机关的**水平距离 ≤ chainCells** 且高度差 ≤ 3 格，就认为马里奥能在连招窗口内依次经过 → 连一条线；
///     chainCells 默认 = 连招窗口 4 秒 × 马里奥赶路速度（约 2.5 格/秒）≈ 10 格（数据可调，不写死在规则里）；
///   - 起手招（弹簧板 / 香蕉皮 / 大炮）会让他失控移动，给它们的连线多算 3 格；
///   - 输出：连线列表 + 连通组（一组 = 一套可连的机关）+ 每组"不同机关种类数"（种类越多越好，宪法 P1）。
/// 这是**设计提示**，不是规则判定：真的能不能连，仍以试玩与 H10/问卷为准。
/// </summary>
public static class ComboRouteAnalyzer
{
    public struct Node { public int x, y; public char ch; }
    public struct Link { public int a, b; }

    public sealed class Result
    {
        public readonly List<Node> nodes = new List<Node>();
        public readonly List<Link> links = new List<Link>();
        public readonly List<List<int>> groups = new List<List<int>>();
        public int BestGroupSize { get { int m = 0; foreach (var g in groups) if (g.Count > m) m = g.Count; return m; } }
        public int BestGroupKinds
        {
            get
            {
                int best = 0;
                foreach (var g in groups) { var kinds = new HashSet<string>(); foreach (int i in g) kinds.Add(Kind(nodes[i].ch)); if (kinds.Count > best) best = kinds.Count; }
                return best;
            }
        }
        public string Summary => nodes.Count == 0 ? "没有玩家机关"
            : $"{nodes.Count} 个机关，{links.Count} 条可连线；最长一套 {BestGroupSize} 个机关、{BestGroupKinds} 种不同机关";
    }

    public const int LauncherBonusCells = 3;
    public const int MaxHeightDiff = 3;

    /// <summary>同一种"招"：大炮朝左/右算一种。</summary>
    public static string Kind(char c) { var i = ElementCatalog.Get(c); return i != null ? i.themeKey : c.ToString(); }

    public static bool IsLauncher(char c) => c == 'J' || c == 'n' || c == 'K' || c == 'k';

    /// <summary>grid 第 0 行在最上面；chainCells = 连招窗口 × 马里奥速度。随机槽位字符（1/2/3）当作空气。</summary>
    public static Result Analyze(IList<string> grid, float chainCells)
    {
        var r = new Result();
        int h = grid.Count;
        for (int row = 0; row < h; row++)
            for (int x = 0; x < grid[row].Length; x++)
            {
                var info = ElementCatalog.Get(grid[row][x]);
                if (info != null && info.role == ElementCatalog.Role.PlayerPrank) r.nodes.Add(new Node { x = x, y = h - 1 - row, ch = grid[row][x] });
            }
        // 同一种机关连成一片的（例如 4 格塌桥、3 格裂缝）只算一个节点：保留最左边那格
        var merged = new List<Node>();
        foreach (var n in r.nodes)
        {
            bool dup = false;
            foreach (var m in merged) if (m.ch == n.ch && m.y == n.y && System.Math.Abs(m.x - n.x) <= 4 && Kind(m.ch) == Kind(n.ch) && (n.ch == 'C' || n.ch == 'x')) { dup = true; break; }
            if (!dup) merged.Add(n);
        }
        r.nodes.Clear(); r.nodes.AddRange(merged);

        for (int i = 0; i < r.nodes.Count; i++)
            for (int j = i + 1; j < r.nodes.Count; j++)
            {
                var a = r.nodes[i]; var b = r.nodes[j];
                float reach = chainCells + (IsLauncher(a.ch) || IsLauncher(b.ch) ? LauncherBonusCells : 0);
                if (System.Math.Abs(a.x - b.x) <= reach && System.Math.Abs(a.y - b.y) <= MaxHeightDiff)
                    r.links.Add(new Link { a = i, b = j });
            }
        // 连通组
        var parent = new int[r.nodes.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int v) { while (parent[v] != v) v = parent[v] = parent[parent[v]]; return v; }
        foreach (var l in r.links) parent[Find(l.a)] = Find(l.b);
        var byRoot = new Dictionary<int, List<int>>();
        for (int i = 0; i < parent.Length; i++) { int root = Find(i); if (!byRoot.TryGetValue(root, out var g)) byRoot[root] = g = new List<int>(); g.Add(i); }
        foreach (var g in byRoot.Values) r.groups.Add(g);
        return r;
    }
}
