using System.Collections.Generic;
using System.Text;

/// <summary>
/// S195：楼层拼接（纯逻辑，关卡工坊"加一层"/"监狱塔"用）。"地下一百层"的做法：**随机的是组合，每一层都是手工模板**（Spelunky 式），
/// 拼接只决定顺序与楼梯口位置；拼完仍要过死局检查（H1/H9），不合格的组合不输出。
/// 约定：
///   - 每层模板宽度相同，最上一行与最下一行是地板/天花板（'#' 或 'x'），两侧是墙 'W'；
///   - 层与层共用一行楼板：楼板上开 2 格"楼梯口"（'.'），并在下层楼梯口正下方放单向台面阶梯，保证能从下往上爬；
///   - 宝物放最底层，出口与出生点在最上层（"越狱：从地下带赃物爬回地面"）。
/// </summary>
public static class FloorStacker
{
    /// <summary>一层楼的内部（不含上下楼板），3 行：第 0 行最上。用 '.' 空气、'-' 单向台面、机关字符。
    /// 层高只有 3 格，所以楼层模板里不放弹簧板（需要头顶空 4 格）；要用弹簧就在工坊里把那层加高。</summary>
    public static readonly string[][] FloorPool =
    {
        new[] { "..............................",
                "..............................",
                ".....~.......n.........[......",
                "" },
        new[] { "..............................",
                "..............................",
                "..b.....n.......~.....c.......",
                "" },
        new[] { "..............................",
                "..............................",
                "......[....~.......n....b.....",
                "" },
        new[] { "..............................",
                "..............................",
                "...c...~....b......~......n...",
                "" },
    };

    public const int FloorInnerHeight = 3; // 每层内部有效 3 行（跳 2 格 + 站 1 格）
    /// <summary>工坊画布最高 48 行：1 行顶墙 + 每层 4 行 + 1 行底 → 最多 11 层。更多层 = 多个房间串联（见路线图），不塞进一个画布。</summary>
    public const int MaxFloors = 11;

    /// <summary>
    /// 拼一座 floors 层的监狱塔。seed 决定每层用哪个模板与楼梯口位置（可复现）。
    /// 返回完整网格（第 0 行在最上）；楼梯口在每层左右交替，迫使马里奥在每层横穿一次（经过该层机关）。
    /// </summary>
    public static string[] Build(int floors, int seed, int width = 32)
    {
        floors = System.Math.Max(2, System.Math.Min(floors, MaxFloors));
        var rng = new System.Random(seed);
        int inner = width - 2;
        var rows = new List<string>();
        rows.Add(new string('W', width));
        // 顶层上方留 1 行空气（出口层可跳）
        for (int f = 0; f < floors; f++)
        {
            var tpl = FloorPool[rng.Next(FloorPool.Length)];
            var floor = new List<char[]>();
            for (int r = 0; r < FloorInnerHeight; r++) floor.Add(Fit(tpl[r], inner).ToCharArray());
            bool top = f == 0, bottom = f == floors - 1;
            // 楼梯口：偶数层在右边，奇数层在左边（交替 → 每层都要横穿）
            bool holeRight = f % 2 == 0;
            int holeX = holeRight ? inner - 5 : 3;
            if (top)
            {
                // 出口 G、出生点 M 在顶层左侧（远离第一个楼梯口）；T 在顶层中间
                Put(floor[FloorInnerHeight - 1], 1, 'G'); Put(floor[FloorInnerHeight - 1], 3, 'M');
                Put(floor[FloorInnerHeight - 1], inner / 2, 'T');
                ClearNear(floor[FloorInnerHeight - 1], holeX, 3);
            }
            if (bottom)
            {
                bool holeAboveRight = (f - 1) % 2 == 0; // 上方楼板的楼梯口属于上一层
                int lootX = holeAboveRight ? 2 : inner - 3; // 宝物在最底层、离上方楼梯口远的一端 → 横穿整层
                Put(floor[FloorInnerHeight - 1], lootX, 'o');
            }
            foreach (var line in floor) rows.Add("W" + new string(line) + "W");
            // 楼板
            if (!bottom)
            {
                var slab = new string('#', inner).ToCharArray();
                slab[holeX] = '.'; slab[holeX + 1] = '.';
                rows.Add("W" + new string(slab) + "W");
            }
        }
        rows.Add(new string('W', width).Remove(1, inner).Insert(1, new string('#', inner)));
        var grid = rows.ToArray();
        AddStairs(grid, width);
        ClearAboveHoles(grid, width);
        return grid;
    }

    /// <summary>在每个楼梯口正下方那层里放一级单向台面（离地 2 格），从下层地面 → 台面 → 穿过楼梯口到上层。</summary>
    private static void AddStairs(string[] grid, int width)
    {
        int h = grid.Length;
        for (int row = 1; row < h - 1; row++)
        {
            string line = grid[row];
            if (line.IndexOf('#') < 0) continue; // 只看楼板行
            for (int x = 1; x < width - 2; x++)
            {
                if (line[x] != '.' || line[x + 1] != '.') continue;
                // 楼梯口 → 下方第 2 行放 "--"（下层地面在楼板下方第 FloorInnerHeight+1 行）
                int stairRow = row + 2;
                if (stairRow >= h - 1) continue;
                var s = grid[stairRow].ToCharArray();
                int sx = x; // 台面正对楼梯口：站在台面上头顶是洞，跳 2 格穿过洞口站上楼板
                for (int k = 0; k < 2; k++) s[sx + k] = '-';
                grid[stairRow] = new string(s);
                // 楼梯口下方的落点清空危险物
                var landing = grid[row + FloorInnerHeight].ToCharArray();
                for (int k = -1; k <= 2; k++) if (x + k > 0 && x + k < width - 1 && landing[x + k] != '.' && landing[x + k] != 'o') landing[x + k] = '.';
                grid[row + FloorInnerHeight] = new string(landing);
                break;
            }
        }
    }

    /// <summary>楼梯口正上方那一格（上一层地面）不能放东西——它脚下是洞，会悬空。</summary>
    private static void ClearAboveHoles(string[] grid, int width)
    {
        for (int row = 1; row < grid.Length - 1; row++)
        {
            if (grid[row].IndexOf('#') < 0) continue;
            var above = grid[row - 1].ToCharArray();
            for (int x = 1; x < width - 1; x++)
                if (grid[row][x] == '.' && above[x] != '.' && above[x] != 'W') above[x] = '.';
            grid[row - 1] = new string(above);
        }
    }

    private static string Fit(string s, int w)
    {
        if (string.IsNullOrEmpty(s)) s = "";
        if (s.Length >= w) return s.Substring(0, w);
        var sb = new StringBuilder(s);
        while (sb.Length < w) sb.Append('.');
        return sb.ToString();
    }

    private static void Put(char[] line, int x, char c) { if (x >= 0 && x < line.Length) line[x] = c; }

    private static void ClearNear(char[] line, int x, int r)
    {
        for (int k = x - r; k <= x + r + 1; k++) if (k >= 0 && k < line.Length && line[k] != 'G' && line[k] != 'M' && line[k] != 'T') line[k] = '.';
    }

    /// <summary>在给定网格上方再加一层空楼（楼板 + 3 行空气），供工坊"加一层"。</summary>
    public static string[] AddFloorOnTop(IList<string> grid)
    {
        if (grid == null || grid.Count == 0) return new string[0];
        int w = grid[0].Length;
        var rows = new List<string> { new string('W', w) };
        for (int r = 0; r < FloorInnerHeight; r++) rows.Add("W" + new string('.', w - 2) + "W");
        var slab = ("W" + new string('#', w - 2) + "W").ToCharArray();
        slab[w - 5] = '.'; slab[w - 4] = '.'; // 右侧楼梯口
        rows.Add(new string(slab));
        for (int i = 1; i < grid.Count; i++) rows.Add(grid[i]); // 原来的天花板行被新楼板取代
        return rows.ToArray();
    }
}
