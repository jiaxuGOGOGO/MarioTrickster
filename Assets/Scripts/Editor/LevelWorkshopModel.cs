using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S189：关卡工坊的纯逻辑（不依赖 EditorWindow，可测试）。
///   - 调色板：按 ElementCatalog 的角色分组，只列出当前模式可用的元素（第 1 步模式只给第 1 步能用的）；
///   - 工具：画笔 / 矩形填充 / 橡皮 / 吸管；
///   - 检查：L1 结构 + L2 可达 + 摆放规则 + 死局分析，汇总成"格子 → 问题"，窗口直接在画布上标红/标黄；
///   - 所有编辑都落在 LevelStudioDocument（ASCII）上：ASCII 仍是唯一的关卡源，美术换图、验证器、生成器全部照旧。
/// </summary>
public static class LevelWorkshopModel
{
    public enum Tool { Brush, Rect, Erase, Pick }

    public sealed class Group
    {
        public string title;
        public List<ElementCatalog.Info> items = new List<ElementCatalog.Info>();
    }

    /// <summary>调色板分组（按 Mario Maker 式分类：地形 / 摆件 / 你的机关 / 目标与角色 / 其他）。随机槽位作为"特殊摆件"附在摆件组。</summary>
    public static List<Group> Palette(bool step1Only)
    {
        var order = new[]
        {
            (ElementCatalog.Role.Terrain, "地形 Terrain"),
            (ElementCatalog.Role.Scenery, "场景摆件 Scenery"),
            (ElementCatalog.Role.PlayerPrank, "你的机关 Pranks（伪装后按 L）"),
            (ElementCatalog.Role.Objective, "目标 Goals"),
            (ElementCatalog.Role.Spawn, "角色 Characters"),
            (ElementCatalog.Role.Movement, "移动类 Movement"),
            (ElementCatalog.Role.AutoHazard, "自动危险 Hazards"),
            (ElementCatalog.Role.Special, "其他 Special"),
            (ElementCatalog.Role.Enemy, "敌人 Enemies"),
        };
        var groups = new List<Group>();
        foreach (var (role, title) in order)
        {
            var g = new Group { title = title };
            g.items.AddRange(ElementCatalog.All.Where(i => i.role == role && i.ch != '.' && i.ch != ' ' && (!step1Only || i.step1)));
            if (g.items.Count > 0) groups.Add(g);
        }
        return groups;
    }

    /// <summary>随机槽位（数字 1/2/3）：说明给人看。</summary>
    public static readonly (char ch, string zh)[] RandomSlots =
    {
        ('1', "随机：箱子或草丛"), ('2', "随机：草丛或空"), ('3', "随机：火或空"),
    };

    public sealed class CellIssue
    {
        public int x, y;
        public bool error;       // true = 必须改（红），false = 提示（黄）
        public string text;
    }

    public sealed class CheckResult
    {
        public readonly List<CellIssue> cells = new List<CellIssue>();
        public readonly List<string> general = new List<string>();
        public int errors, warnings;
        public HashSet<int> deadlock = new HashSet<int>(), temporary = new HashSet<int>();
        public bool Playable => errors == 0;
        public string Headline => errors == 0
            ? (warnings == 0 ? "✓ 可以试玩：没有发现问题" : $"✓ 可以试玩，有 {warnings} 个提示")
            : $"✗ 有 {errors} 个问题要改（红格）";
    }

    /// <summary>
    /// 全面检查。grid 第 0 行在最上面。随机槽位（1/2/3）会把所有组合都检查一遍（组合太多时只查最坏的两种：全实心 / 全空）。
    /// </summary>
    /// <summary>快速检查（画画时用）：只查代表布局（槽位取第一个选项）的摆放 + 结构，~1ms。完整检查在停笔后再跑。</summary>
    public static CheckResult QuickCheck(IList<string> grid, bool step1Rules, System.Func<char, bool> isSolid)
    {
        var result = new CheckResult();
        var variant = Fill(grid, 0, false);
        foreach (var issue in ElementCatalog.PlacementIssues(variant, step1Rules, isSolid)) AddParsed(result, issue, true);
        return result;
    }

    public static CheckResult Check(IList<string> grid, bool step1Rules, System.Func<char, bool> isSolid)
    {
        var result = new CheckResult();
        if (grid == null || grid.Count == 0) { result.general.Add("画布是空的"); result.errors++; return result; }
        foreach (var c in new[] { 'M', 'T', 'G' })
        {
            int n = grid.Sum(r => r.Count(ch => ch == c));
            if (n != 1) { result.general.Add($"需要且只能有一个 {ElementCatalog.Get(c)?.zh ?? c.ToString()}（现在 {n} 个）"); result.errors++; }
        }
        if (step1Rules && grid.Sum(r => r.Count(ch => ch == 'o')) != 1) { result.general.Add("第 1 步房间需要且只能有一个宝物 o"); result.errors++; }
        if (result.errors > 0) return result;

        var seenPhysics = new HashSet<string>();
        foreach (var variant in Variants(grid))
        {
            // 摆放规则
            foreach (var issue in ElementCatalog.PlacementIssues(variant, step1Rules, isSolid))
                AddParsed(result, issue, true);
            // S192 性能：结构检查与死局只取决于"实心/危险/机关"的分布；草丛、装饰、火（非实心非危险）
            // 不同的组合结果相同，按"物理签名"去重（32 组合 → 通常 2–4 组）。
            string signature = PhysicsSignature(variant);
            if (!seenPhysics.Add(signature)) continue;
            // L1 结构
            string text = string.Join("\n", variant);
            var l1 = AsciiLevelValidator.ValidateTemplate(text);
            foreach (var e in l1.errors) AddGeneral(result, "结构：" + e, true);
            // 死局（含拿宝往返、塌桥塌后能否出去、封路墙暂时阻挡）
            var dl = LevelDeadlockAnalyzer.Analyze(variant);
            result.deadlock.UnionWith(dl.deadlockCells);
            result.temporary.UnionWith(dl.temporaryCells);
            foreach (var i in dl.issues)
            {
                if (i.severity == LevelDeadlockAnalyzer.Severity.Info) continue;
                if (i.x >= 0) AddCell(result, i.x, i.y, i.severity == LevelDeadlockAnalyzer.Severity.Error, i.message);
                else AddGeneral(result, i.message, i.severity == LevelDeadlockAnalyzer.Severity.Error);
            }
        }
        return result;
    }

    /// <summary>死局分析只关心：实心、危险、塌桥、封路墙、出生点/宝物/出口；其它字符视为空气。</summary>
    public static string PhysicsSignature(IList<string> grid)
    {
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars();
        var hazard = reg.GetHazardChars();
        var sb = new System.Text.StringBuilder();
        foreach (var row in grid)
        {
            foreach (char c in row)
                sb.Append(solid.Contains(c) || hazard.Contains(c) || c == 'C' || c == 'x' || c == '|' || c == '%' || c == '[' || c == 'M' || c == 'G' || c == 'o' ? c : '.');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>随机槽位的检查组合：≤ 64 种全部枚举；更多时取"每个槽位第一个选项 / 最后一个选项"两种极端。</summary>
    public static IEnumerable<List<string>> Variants(IList<string> grid)
    {
        var slots = Step1Layout.SlotOptions(grid.ToArray());
        long total = 1;
        foreach (var o in slots) { total *= System.Math.Max(1, o.Length); if (total > 64) break; }
        if (slots.Count == 0) { yield return grid.ToList(); yield break; }
        if (total <= 64)
        {
            for (long n = 0; n < total; n++) yield return Fill(grid, n, true);
        }
        else
        {
            yield return Fill(grid, 0, false);
            yield return Fill(grid, -1, false);
        }
    }

    private static List<string> Fill(IList<string> grid, long n, bool enumerate)
    {
        long rest = n;
        var rows = new List<string>(grid.Count);
        foreach (var r in grid)
        {
            var row = r.ToCharArray();
            for (int c = 0; c < row.Length; c++)
                if (Step1Layout.Slots.TryGetValue(row[c], out string o))
                {
                    if (enumerate) { row[c] = o[(int)(rest % o.Length)]; rest /= o.Length; }
                    else row[c] = n < 0 ? o[o.Length - 1] : o[0];
                }
            rows.Add(new string(row));
        }
        return rows;
    }

    private static void AddParsed(CheckResult r, string issue, bool error)
    {
        // "(x,y) ..." → 格子问题
        if (issue.StartsWith("(") && issue.IndexOf(')') > 0)
        {
            var coords = issue.Substring(1, issue.IndexOf(')') - 1).Split(',');
            if (coords.Length == 2 && int.TryParse(coords[0], out int x) && int.TryParse(coords[1], out int y))
            { AddCell(r, x, y, error, issue.Substring(issue.IndexOf(')') + 1).Trim()); return; }
        }
        AddGeneral(r, issue, error);
    }

    private static void AddCell(CheckResult r, int x, int y, bool error, string text)
    {
        if (r.cells.Exists(c => c.x == x && c.y == y && c.text == text)) return;
        r.cells.Add(new CellIssue { x = x, y = y, error = error, text = text });
        if (error) r.errors++; else r.warnings++;
    }

    private static void AddGeneral(CheckResult r, string text, bool error)
    {
        if (r.general.Contains(text)) return;
        r.general.Add(text);
        if (error) r.errors++; else r.warnings++;
    }

    /// <summary>矩形填充（x0..x1, y0..y1，含边界）。</summary>
    public static void FillRect(LevelStudioDocument doc, int x0, int y0, int x1, int y1, char value)
    {
        int ax = System.Math.Min(x0, x1), bx = System.Math.Max(x0, x1), ay = System.Math.Min(y0, y1), by = System.Math.Max(y0, y1);
        for (int y = ay; y <= by; y++) for (int x = ax; x <= bx; x++) doc.Paint(x, y, value);
    }

    /// <summary>新建空房间：四周墙 + 三层地面 + M T G o，保证"一打开就能试玩"。</summary>
    /// <summary>
    /// S193：样板房"两层监狱"——演示纵向逃脱：马里奥在地下层拿宝，要爬回地面出口。
    /// 裂缝地板 x 把上层的人掉回下层；弹簧板 J 正好在楼板洞口下方（你可以把他"弹回楼上"打乱路线），另一条回去的路是单向台面阶梯；
    /// 所以任何机关打开后都有回去的路（死局检查必须通过——测试校验）。
    /// </summary>
    public static readonly string[] PrisonSample =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W..............................................W",
        "W..............................................W",
        "W.G.M....b..n..~..[....T.....c..~..............W",
        "W#########xxxx####################.....########W",
        "W...................................--.........W",
        "W..............................................W",
        "W.................................--...........W",
        "W.........~...n.........~.[....b......J....o...W",
        "W##############################################W",
    };

    /// <summary>
    /// S196：箱庭样板"地下监狱·四层"——每层一个身份，层间多路连接，有捷径与秘密（参考 Undead Burg / Stormveil 的手法）：
    ///   F1 看守所（地面）：出口、出生点；封路墙守着走廊；**捷径门 |**（x=15）从右边开——下去绕一圈回来才能打开，打开后回出口近一大截。
    ///   F2 放风场（高 6 格）：弹簧板主题——广场上两块弹簧，楼板中间一段**裂缝地板**（可以把他掉进牢房区）；楼板上 **裂墙 %** 是通往看守所的秘密竖井。
    ///   F3 牢房区：塌桥主题——中间一座塌桥横跨（楼板上），右侧牢门 | 从右边开（从金库爬上来才能打开）。
    ///   F4 金库（最深处）：宝物在最右；两门大炮对射；左侧**裂墙暗室**（藏身处）。
    /// 环路：去程走左梯，回程可以走右梯 → 中梯 → 捷径门，或从秘密竖井直接回看守所。死局检查按"全部打开 / 全部关着"都通过（测试校验）。
    /// </summary>
    public static readonly string[] HakoniwaSample =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W..............W...............................W",
        "W..............W...............................W",
        "W.G.M......b...|....[......~.....c....[........W",
        "W#######..####################%%############..#W",
        "W..............................................W",
        "W.......--..................................--.W",
        "W..........--..................................W",
        "W.........................................--...W",
        "W............--.........................--.....W",
        "W....c................J.T.n.......J..b.........W",
        "W###############xxxx##########..############..#W",
        "W.....................................W........W",
        "W.............................--......W.....--.W",
        "W.........~...b...................c...|........W",
        "W####..###############CCCC################..###W",
        "W...........W..................................W",
        "W....--.....W.............................--...W",
        "W..c........%...K..............~....k...b...o..W",
        "W##############################################W",
    };

    public static string NewRoom(int width, int height)
    {
        width = System.Math.Max(16, System.Math.Min(LevelStudioDocument.MaxWidth, width));
        height = System.Math.Max(8, System.Math.Min(LevelStudioDocument.MaxHeight, height));
        var rows = new char[height][];
        for (int r = 0; r < height; r++)
        {
            rows[r] = new string('.', width).ToCharArray();
            rows[r][0] = rows[r][width - 1] = 'W';
        }
        for (int x = 0; x < width; x++) rows[0][x] = 'W';
        for (int y = 0; y < 3; y++) for (int x = 1; x < width - 1; x++) rows[height - 1 - y][x] = '#';
        int stand = height - 1 - 3;
        rows[stand][2] = 'G'; rows[stand][4] = 'M'; rows[stand][System.Math.Min(width - 4, 10)] = 'T'; rows[stand][width - 4] = 'o';
        return string.Join("\n", rows.Select(r => new string(r)));
    }
}
