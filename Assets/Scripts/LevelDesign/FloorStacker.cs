using System.Collections.Generic;

/// <summary>
/// S195/S196：监狱塔拼接（纯逻辑，关卡工坊"监狱塔…"菜单用）。
/// S196 改为**箱庭式**（用户反馈：只是堆层数，没有每层特色与层间巧思）：
///   - 每层从"主题楼层"里选（放风场/牢房区/水牢/看守所…），**同一座塔不重复主题** → 每层有身份（主机关 + 藏身处布局）；
///   - 层与层之间**至少两条路**：主楼梯口（左右交替，迫使横穿） + 一条"要打开的捷径"（裂墙 % 或裂缝地板 x，交替出现）；
///   - 每两层一扇**单向捷径门 |**，把长回程切短（Undead Burg 式 loop back）；
///   - 宝物在最底层远端，出口/出生点在顶层；所有组合过死局检查（门/墙"全关"与"全开"两种最坏情况，测试校验）。
/// 随机的是组合，每一层都是手工模板（Spelunky 的做法）；画布最高 48 行 → 最多 11 层。
/// </summary>
public static class FloorStacker
{
    public sealed class Theme
    {
        public string zh;       // 楼层名（显示在总览里）
        public string row;      // 站立行（30 格内部宽度），机关与藏身处
        public Theme(string zh, string row) { this.zh = zh; this.row = row; }
    }

    /// <summary>主题楼层（站立行，内部宽 30）。层高 3 格 → 不放弹簧板（需要头顶空 4 格）。</summary>
    public static readonly Theme[] Themes =
    {
        new Theme("放风场·香蕉皮", "...b....n......c......n....b.."),
        new Theme("牢房区·封路墙", "..c...[.....b......[.....c...."),
        new Theme("锅炉房·火", "...~....b....~.......c...~...."),
        new Theme("看守所·大炮", "..b.......K..........k.....c.."),
        new Theme("水牢·混合", "...c...~.......n......b...[..."),
        new Theme("废墟·裂缝与香蕉", "..b..c....n..b.....c..n..b...."),
    };

    public const int FloorInnerHeight = 3;
    public const int MaxFloors = 11;

    public static string[] Build(int floors, int seed, int width = 32) => Build(floors, seed, width, out _);

    /// <summary>
    /// 结构（每层 4 行：空气、空气、站立行、楼板）：
    ///   - 主楼梯口左右交替（每层都要横穿、经过该层机关）；
    ///   - 第二条路：楼板上另一侧的裂墙 %（砸开）或裂缝地板 x（踩塌），交替出现；
    ///   - **捷径竖井**（最左 3 列）：只在最底层开口，一路单向台面爬到顶层，顶层出口旁一扇从竖井一侧开的捷径门 |。
    ///     拿宝后绕回竖井 → 爬上去 → 开门 = 直通出口（Undead Burg 式"绕一大圈后打开的回头门"）。门关着时整座塔也能通关。
    /// 主题物件最后放：遇到楼梯口/落点/竖井等保留列就挪到最近空位（不再被清空）。
    /// </summary>
    public static string[] Build(int floors, int seed, int width, out List<string> floorNames)
    {
        floors = System.Math.Max(2, System.Math.Min(floors, MaxFloors));
        width = 32;
        int inner = width - 2;
        var rng = new System.Random(seed);
        var order = new List<int>(); for (int i = 0; i < Themes.Length; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        floorNames = new List<string>();

        int h = 1 + floors * 4;      // 顶墙 + 每层 4 行（最底层的"楼板"= 地面）
        var g = new char[h][];
        for (int r = 0; r < h; r++) { g[r] = Line('.', width); g[r][0] = g[r][width - 1] = 'W'; }
        for (int x = 0; x < width; x++) g[0][x] = 'W';
        const int shaftL = 1, shaftR = 3, shaftWall = 4;
        var standRows = new int[floors];
        for (int f = 0; f < floors; f++)
        {
            int a1 = 1 + f * 4, a2 = a1 + 1, st = a1 + 2, slab = a1 + 3;
            standRows[f] = st;
            bool bottom = f == floors - 1;
            for (int x = 1; x < width - 1; x++) g[slab][x] = '#';
            if (!bottom)
            {
                bool mainRight = f % 2 == 0;
                int mainX = mainRight ? inner - 4 : 7;
                int altX = mainRight ? 10 : inner - 11;
                g[slab][mainX] = g[slab][mainX + 1] = '.';
                char alt = f % 2 == 0 ? '%' : 'x';
                for (int k = 0; k < (alt == 'x' ? 3 : 2); k++) g[slab][altX + k] = alt;
                // 主楼梯口下方一级台面（下层 a2 行）
                g[slab + 2][mainX] = g[slab + 2][mainX + 1] = '-';
                if (alt == '%') { g[slab + 2][altX] = g[slab + 2][altX + 1] = '-'; }
            }
            // 竖井：左 3 列，楼板行与 a2 行放单向台面；竖井墙（最底层不设 → 入口）
            for (int x = shaftL; x <= shaftR; x++) { g[slab][x] = bottom ? '#' : '-'; g[a2][x] = '-'; }
            if (!bottom) { g[a1][shaftWall] = 'W'; g[a2][shaftWall] = 'W'; g[st][shaftWall] = f == 0 ? '|' : 'W'; }
            if (f == 0) { g[a1][shaftWall] = 'W'; }
        }
        // 最顶层竖井顶端：a2 行的台面去掉（顶上是天花板，没必要）
        for (int x = shaftL; x <= shaftR; x++) g[2][x] = '.';

        // 保留列（站立行上不能放东西的格）
        for (int f = 0; f < floors; f++)
        {
            var theme = Themes[order[f % order.Count]];
            floorNames.Add(theme.zh);
            int st = standRows[f];
            var reserved = new HashSet<int> { 0, width - 1, shaftL, shaftL + 1, shaftR, shaftWall, shaftWall + 1 };
            // 脚下（本层楼板）的开口 / 裂缝 / 裂墙 → 上面不能放（会悬空）
            for (int x = 1; x < width - 1; x++) if (g[st + 1][x] != '#') reserved.Add(x);
            // 头顶（上层楼板）的开口 → 落点 ±1 留空
            if (f > 0) for (int x = 1; x < width - 1; x++) { char c = g[st - 3][x]; if (c == '.' || c == '%' || c == 'x') { reserved.Add(x - 1); reserved.Add(x); reserved.Add(x + 1); } }
            if (f == 0) { Place(g[st], 6, 'G', reserved); Place(g[st], 8, 'M', reserved); Place(g[st], inner / 2 + 2, 'T', reserved); }
            if (f == floors - 1)
            {
                bool holeAboveRight = (f - 1) % 2 == 0;
                Place(g[st], holeAboveRight ? 9 : inner - 2, 'o', reserved);
            }
            for (int x = 0; x < theme.row.Length; x++)
            {
                char c = theme.row[x];
                if (c == '.') continue;
                Place(g[st], x + 1, c, reserved);
            }
        }
        var grid = new string[h];
        for (int i = 0; i < h; i++) grid[i] = new string(g[i]);
        return grid;
    }

    /// <summary>把 c 放在 x 附近最近的空位（不在保留列、原本是空气），放下后该列及两侧加入保留（物件之间留空隙）。</summary>
    private static void Place(char[] row, int x, char c, HashSet<int> reserved)
    {
        for (int d = 0; d < row.Length; d++)
            foreach (int nx in new[] { x + d, x - d })
            {
                if (nx <= 0 || nx >= row.Length - 1 || reserved.Contains(nx) || row[nx] != '.') continue;
                row[nx] = c;
                reserved.Add(nx); reserved.Add(nx - 1); reserved.Add(nx + 1);
                if (c == 'K') { reserved.Add(nx + 1); reserved.Add(nx + 2); reserved.Add(nx + 3); } // 炮口前留空
                if (c == 'k') { reserved.Add(nx - 1); reserved.Add(nx - 2); reserved.Add(nx - 3); }
                return;
            }
    }

    private static char[] Line(char c, int w) { var a = new char[w]; for (int i = 0; i < w; i++) a[i] = c; return a; }

    /// <summary>在给定网格上方再加一层空楼（楼板 + 3 行空气），供工坊"加一层"。</summary>
    public static string[] AddFloorOnTop(IList<string> grid)
    {
        if (grid == null || grid.Count == 0) return new string[0];
        int w = grid[0].Length;
        var rows = new List<string> { new string('W', w) };
        for (int r = 0; r < FloorInnerHeight; r++) rows.Add("W" + new string('.', w - 2) + "W");
        var slab = ("W" + new string('#', w - 2) + "W").ToCharArray();
        slab[w - 5] = '.'; slab[w - 4] = '.';
        rows.Add(new string(slab));
        for (int i = 1; i < grid.Count; i++) rows.Add(grid[i]);
        return rows.ToArray();
    }
}
