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
    public enum Tool { Brush, Rect, Erase, Pick, Move } // S207：Move 追加在末尾（窗口按数字存）

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

    /// <summary>
    /// S204：读网页"关卡设计台"导出的 .studio.json 里的网格（纯逻辑，不依赖 JSON 库：只解析 "grid":[ "...", ... ]）。
    /// 未登记的字符（网页里的新机制提案）换成空气，并在 note 里说明——提案要先按设计单实现登记才能生成。
    /// </summary>
    public const int MinWidth = 12, MinHeight = 6;

    /// <summary>S206：搭建范围规则（纯逻辑）。</summary>
    public static List<string> BoundsIssues(IList<string> grid, System.Func<char, bool> isSolid)
    {
        var list = new List<string>();
        int h = grid.Count, w = grid.Count > 0 ? grid.Max(r => r.Length) : 0;
        if (w < MinWidth || h < MinHeight) list.Add($"房间太小：至少 {MinWidth} 宽 × {MinHeight} 高（现在 {w}×{h}）");
        if (w > LevelStudioDocument.MaxWidth || h > LevelStudioDocument.MaxHeight) list.Add($"房间太大：最多 {LevelStudioDocument.MaxWidth} 宽 × {LevelStudioDocument.MaxHeight} 高（现在 {w}×{h}）");
        if (grid.Any(r => r.Length != w)) list.Add("每一行长度要一样");
        if (list.Count > 0 || h == 0) return list;
        bool Solid(char c) => isSolid(c);
        if (!grid[0].All(Solid) || !grid[h - 1].All(Solid)) list.Add("最上面一行和最下面一行必须全是实心（墙 W / 地面 #），马里奥不能掉出地图");
        if (!grid.All(r => Solid(r[0]) && Solid(r[w - 1]))) list.Add("最左和最右一列必须全是墙 W");
        return list;
    }

    public static string[] GridFromStudioJson(string json, out string note)
    {
        note = "";
        if (string.IsNullOrEmpty(json)) { note = "文件是空的"; return null; }
        int i = json.IndexOf("\"grid\"", System.StringComparison.Ordinal);
        if (i < 0) { note = "不是设计台导出的 .json（找不到 grid）"; return null; }
        int a = json.IndexOf('[', i), b = json.IndexOf(']', a + 1);
        if (a < 0 || b < 0) { note = "grid 格式不对"; return null; }
        var rows = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json.Substring(a, b - a), "\"((?:[^\"\\\\]|\\\\.)*)\""))
            rows.Add(System.Text.RegularExpressions.Regex.Unescape(m.Groups[1].Value));
        if (rows.Count == 0) { note = "grid 是空的"; return null; }
        var reg = AsciiElementRegistry.GetDefault();
        var unknown = new HashSet<char>();
        for (int r = 0; r < rows.Count; r++)
        {
            var ch = rows[r].ToCharArray();
            for (int x = 0; x < ch.Length; x++)
                if (ch[x] != '.' && reg.GetEntry(ch[x]) == null && !Step1Layout.Slots.ContainsKey(ch[x])) { unknown.Add(ch[x]); ch[x] = '.'; }
            rows[r] = new string(ch);
        }
        if (unknown.Count > 0) note = $"网页里的新机制提案 {string.Join(" ", unknown)} 还没实现，已先换成空气。把设计单交给 AI 实现后再导入即可。";
        return rows.ToArray();
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
        // S206：搭建范围（和网页设计台一致）：宽 12–128、高 6–48；外圈一圈必须是实心（最外列 W，顶/底行实心）
        foreach (var msg in BoundsIssues(grid, isSolid)) { result.general.Add(msg); result.errors++; }
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
            // S209：按马里奥 AI 的真实走法（身体宽 0.8、同一条转向规则）走一遍——理论上走得到 ≠ 他真的会走过去
            if (step1Rules && dl.issues.All(i => i.severity != LevelDeadlockAnalyzer.Severity.Error))
            {
                var walk = LevelRouteFollower.Run(variant);
                if (!walk.ok) AddCell(result, (int)System.Math.Round(walk.stuckX), (int)System.Math.Round(walk.stuckY), false,
                    $"按马里奥的走法走一遍：{walk.Summary}（请把这里告诉 AI；游戏里会触发卡住救援）");
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

    // ── S207：移动工具（纯逻辑，网页 logic.js 同规则）──────────────────
    // 用户："摆放的关卡道具都不能点击移动"。点一个东西 = 选中它（塌桥/单向台面/毒池等连成一片的同种元素一起选），拖动 = 搬过去；
    // 在空处拖 = 框选一整块。规则：
    //   - 搬走后原地变空气；落点只覆盖"搬过来的实物"，选区里的空气是透明的（不会把目标处的东西擦掉）；
    //   - 外圈（最左/最右列、顶行、底行）永远不动也不会被覆盖——保证搭建范围规则不被搬坏；
    //   - M/T/G/o 是"搬"不是"复制"：粘贴时跳过，整张图始终各 1 个。
    public struct Sel { public int x0, y0, x1, y1; public Sel(int ax, int ay, int bx, int by) { x0 = System.Math.Min(ax, bx); y0 = System.Math.Min(ay, by); x1 = System.Math.Max(ax, bx); y1 = System.Math.Max(ay, by); } public bool Contains(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= y1; }

    /// <summary>S207：状态栏一句话告诉你"现在点画布会发生什么"（用户反馈吸管不明确）。网页设计台同一套文字。</summary>
    public static string ToolHint(Tool t, bool hasSelection)
    {
        switch (t)
        {
            case Tool.Rect: return "矩形：按住拖出一块，松手填满";
            case Tool.Erase: return "橡皮：点/拖擦成空气";
            case Tool.Pick: return "吸管：点画布上一个格子 → 画笔变成它，自动回到画笔";
            case Tool.Move: return hasSelection ? "移动：拖黄框里的东西到新位置；方向键挪 1 格；Ctrl+C/V 复制；Delete 清空；Esc 取消" : "移动：点一个东西选中（连成一片的整段选），或在空处拖出框选一块";
            default: return "画笔：点/拖着画；右键擦；Alt+点 = 吸取";
        }
    }

    public static bool IsFrame(int x, int y, int w, int h) => x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1;
    static char At(IList<string> g, int x, int y) => g[g.Count - 1 - y][x];
    /// <summary>大块地形（地面/墙/平台）点一下只选一格；其它元素选连成一片的同种（整座塌桥、一排单向台面）。</summary>
    public static bool GroupsWithNeighbours(char c) => c != '.' && c != ' ' && c != '#' && c != 'W' && c != '=';

    /// <summary>点 (x,y)：返回要选中的范围（空气/外圈 → null）。</summary>
    public static Sel? SelectAt(IList<string> g, int x, int y)
    {
        int h = g.Count, w = h > 0 ? g[0].Length : 0;
        if (x < 0 || y < 0 || x >= w || y >= h || IsFrame(x, y, w, h)) return null;
        char c = At(g, x, y);
        if (c == '.' || c == ' ') return null;
        var sel = new Sel(x, y, x, y);
        if (!GroupsWithNeighbours(c)) return sel;
        var seen = new HashSet<int>(); var q = new Stack<(int, int)>(); q.Push((x, y));
        while (q.Count > 0)
        {
            var (cx, cy) = q.Pop();
            if (cx < 0 || cy < 0 || cx >= w || cy >= h || IsFrame(cx, cy, w, h) || At(g, cx, cy) != c || !seen.Add(cx * 1000 + cy)) continue;
            sel = new Sel(System.Math.Min(sel.x0, cx), System.Math.Min(sel.y0, cy), System.Math.Max(sel.x1, cx), System.Math.Max(sel.y1, cy));
            q.Push((cx + 1, cy)); q.Push((cx - 1, cy)); q.Push((cx, cy + 1)); q.Push((cx, cy - 1));
        }
        return sel;
    }

    /// <summary>把选区里的实物整体挪 (dx,dy)。外圈不动；挪出房间内圈的部分丢弃（调用方一般先用 ClampMove 限制）。</summary>
    public static string[] MoveBlock(IList<string> g, Sel s, int dx, int dy)
    {
        int h = g.Count, w = g[0].Length;
        var rows = g.Select(r => r.ToCharArray()).ToArray();
        var lifted = new List<(int x, int y, char c)>();
        for (int y = s.y0; y <= s.y1; y++)
            for (int x = s.x0; x <= s.x1; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || IsFrame(x, y, w, h)) continue;
                char c = rows[h - 1 - y][x];
                if (c == '.' || c == ' ') continue;
                lifted.Add((x, y, c)); rows[h - 1 - y][x] = '.';
            }
        foreach (var (x, y, c) in lifted)
        {
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h || IsFrame(nx, ny, w, h)) continue;
            rows[h - 1 - ny][nx] = c;
        }
        return rows.Select(r => new string(r)).ToArray();
    }

    /// <summary>限制挪动量：选区不能挪进外圈（挪到头就停住，不会把东西挤没）。</summary>
    public static (int dx, int dy) ClampMove(int w, int h, Sel s, int dx, int dy)
    {
        dx = System.Math.Max(1 - s.x0, System.Math.Min(w - 2 - s.x1, dx));
        dy = System.Math.Max(1 - s.y0, System.Math.Min(h - 2 - s.y1, dy));
        return (dx, dy);
    }

    /// <summary>复制选区（给粘贴用）：只记实物；M/T/G/o 等唯一元素不复制。</summary>
    public static List<(int dx, int dy, char c)> CopyBlock(IList<string> g, Sel s)
    {
        int h = g.Count, w = g[0].Length; var list = new List<(int, int, char)>();
        for (int y = s.y0; y <= s.y1; y++)
            for (int x = s.x0; x <= s.x1; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || IsFrame(x, y, w, h)) continue;
                char c = At(g, x, y);
                if (c == '.' || c == ' ' || IsUniqueChar(c)) continue;
                list.Add((x - s.x0, y - s.y0, c));
            }
        return list;
    }

    public static bool IsUniqueChar(char c) { var i = ElementCatalog.Get(c); return c == 'M' || c == 'T' || c == 'G' || (i != null && i.unique); }

    /// <summary>把复制的块贴到 (x,y)（左下角对齐）；外圈、唯一元素（M/T/G/o）所在格不覆盖。</summary>
    public static string[] PasteBlock(IList<string> g, IList<(int dx, int dy, char c)> block, int x, int y)
    {
        int h = g.Count, w = g[0].Length;
        var rows = g.Select(r => r.ToCharArray()).ToArray();
        foreach (var (bx, by, c) in block)
        {
            int nx = x + bx, ny = y + by;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h || IsFrame(nx, ny, w, h) || IsUniqueChar(rows[h - 1 - ny][nx])) continue;
            rows[h - 1 - ny][nx] = c;
        }
        return rows.Select(r => new string(r)).ToArray();
    }

    /// <summary>清空选区里的实物（外圈、M/T/G/o 保留——删掉会让关卡不能玩）。</summary>
    public static string[] ClearBlock(IList<string> g, Sel s)
    {
        int h = g.Count, w = g[0].Length;
        var rows = g.Select(r => r.ToCharArray()).ToArray();
        for (int y = s.y0; y <= s.y1; y++)
            for (int x = s.x0; x <= s.x1; x++)
                if (x >= 0 && y >= 0 && x < w && y < h && !IsFrame(x, y, w, h) && !IsUniqueChar(rows[h - 1 - y][x])) rows[h - 1 - y][x] = '.';
        return rows.Select(r => new string(r)).ToArray();
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
        "W..................................--..........W",
        "W..............................................W",
        "W..................................--..........W",
        "W.........~...n.........~.[....b......J....o...W",
        "W##############################################W",
    };

    /// <summary>
    /// S200：样板"诱捕走廊"——专门练"以身入局"：出生点在左，宝物在右；中段是一条排好的连锁：
    ///   绊线 R → 香蕉皮 n（滑过去）→ 火 ~（油桶 U 挨着）→ 塌桥 CCC（掉下一层：下层还有火+油桶、弹簧 J）→ 右边单向台面爬回 → 封路墙 [。
    /// 玩法：先 Shift+F 一键编号，再跑到马里奥面前按 T 挑衅，引他追你冲过绊线。
    /// S203：上方一条连续的单向台面"高路"——谨慎型被坑过之后会走高路绕开；你要么在高路尽头埋伏，要么把他引下来。
    /// </summary>
    public static readonly string[] LureSample =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W..............................................W",
        "W..............................................W",
        "W..............................................W",
        "W.........------------------------.............W",
        "W......--......................................W",
        "W.G.M....b.....R.n...~U.............[...T...o..W",
        "W########################CCC##########..#######W",
        "W..............................................W",
        "W.....................................--.......W",
        "W..................................--..........W",
        "W..............................................W",
        "W...............................--.............W",
        "W.....................J.......~.U......b.......W",
        "W##############################################W",
    };

    /// <summary>
    /// S207：样板"长廊远征"（94×15）——大房间镜头示范：两段诱捕走廊连起来，出生在最左、宝物在最右。
    ///   宽度超过 64 → 游戏里自动用"智能跟随"镜头（死亡细胞式），马里奥在屏幕外时边缘有红箭头，右上角小地图。
    ///   后半段把香蕉皮换成黏胶 g，连锁节奏不同；回合时间按路线长度自动放宽（不会"还没走到就超时"）。
    /// </summary>
    public static readonly string[] LongHallSample =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W............................................................................................W",
        "W............................................................................................W",
        "W............................................................................................W",
        "W.........------------------------......................------------------------.............W",
        "W......--............................................--......................................W",
        "W.G.M....b.....R.n...~U.............[...T..............b.....R.g...~U.............[.......o..W",
        "W########################CCC##########..###############################CCC##########..#######W",
        "W............................................................................................W",
        "W.....................................--............................................--.......W",
        "W..................................--............................................--..........W",
        "W............................................................................................W",
        "W...............................--............................................--.............W",
        "W.....................J.......~.U......b............................J.......~.U......b.......W",
        "W############################################################################################W",
    };

    /// <summary>
    /// S196：箱庭样板"地下监狱·四层"——每层一个身份，层间多路连接，有捷径与秘密（参考 Undead Burg / Stormveil 的手法）：
    ///   F1 看守所（地面）：出口、出生点；封路墙守着走廊；**捷径门 |**（x=15）从右边开——下去绕一圈回来才能打开，打开后回出口近一大截。
    ///   F2 放风场（高 6 格）：弹簧板主题——广场上两块弹簧，楼板中间一段**裂缝地板**（可以把他掉进牢房区）；楼板上 **裂墙 %** 是通往看守所的秘密竖井。
    ///   F3 牢房区：塌桥主题——中间一座塌桥横跨（楼板上），右侧牢门 | 从右边开（从金库爬上来才能打开）。
    ///   F4 金库（最深处）：宝物在最右；两门大炮对射；左侧**裂墙暗室**（藏身处）。
    /// 环路：去程走左梯，回程可以走右梯 → 中梯 → 捷径门，或从秘密竖井直接回看守所。死局检查按"全部打开 / 全部关着"都通过（测试校验）。
    /// S197：两对通风管 O（看守所右 ↔ 金库左、放风场 ↔ 牢房区）只给捣蛋者用；金库里一段毒池 w，牢房区楼梯前一块黏胶 g。
    /// S198：放风场一个绳套 Y；四个道具点 ?（每局随机亮 2 个）。
    /// S199：三层各一个油桶 U 紧挨着火（喷火 → 点燃 → 连锁）；放风场一个铁笼 Q。
    /// S200：三处绊线 R 放在连锁第一环前（踩到 → 启动你用 F 布置的连锁）。
    /// </summary>
    public static readonly string[] HakoniwaSample =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W..............W...............................W",
        "W..............W...............................W",
        "W.G.M......b...|....[...?R.~.U...c....[...O....W",
        "W#######..####################%%############..#W",
        "W..............................................W",
        "W.......--..................................--.W",
        "W..........--..................................W",
        "W.........................................--...W",
        "W............--.........................--.....W",
        "W....c.....O.....Q.R..J.T.n..Y....J..b..?......W",
        "W###############xxxx##########..############..#W",
        "W.....................................W........W",
        "W.............................--......W.....--.W",
        "W.......R.~.U.b.....?.........O...c...|.g......W",
        "W####..###############CCCC################..###W",
        "W...........W..................................W",
        "W....--.....W.............................--...W",
        "W..c....O...%...K...?...ww...U.~....k...b...o..W",
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
