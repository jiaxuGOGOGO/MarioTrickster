using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S208：帮"不知道从何下手"的设计者起步——新建关卡向导（点子 + 主角机关 + 时长 → 自动铺好起承转合 4 段）、
/// 8 个模式印章、节奏条（紧张 / 喘气）、转移点提示（机关旁没有草丛/箱子）。纯逻辑，沙盒可测。
/// 与网页 tools/LevelStudioWeb/logic.js 的 PATTERNS / stampPattern / wizardLevel / beatBounds / routePasses / rhythm / coverHints
/// **同规则同数据**——改一边必须改另一边（测试 S208 会对照两边的印章文字）。
/// 依据：任天堂起承转合（一关一个点子）、Celeste"安全感"控节奏、节奏组论文（段间休息区）、
///       Dahlskog &amp; Togelius 关卡模式、Morai Maker 研究（建议要可预期，别乱填空地）。
/// 这些都只是"起草 + 提示"，不改游戏规则；生成的关卡照样走完整检查（H1 死局、炸弹策略模拟）。
/// </summary>
public static class LevelBlueprint
{
    public sealed class Pattern
    {
        public string id, zh, tip;
        public char def;
        public int stand;          // rows 里哪一行是"马里奥站的那一行"
        public string[] rows;      // 从上到下；'_' = 不动原格，'*' = 主角机关
        public int Width => rows[0].Length;
    }

    public static readonly Pattern[] Patterns =
    {
        new Pattern { id = "ambush", zh = "伏击点", def = '~', stand = 0, rows = new[] { "b..*" }, tip = "草丛隔两格放机关：你躲着、按准时机（最基础）" },
        new Pattern { id = "slide", zh = "滑铲送火", def = '~', stand = 0, rows = new[] { "b.R.n..b*U" }, tip = "绊线启动 → 香蕉皮滑过去 → 火点燃油桶（连锁入门）" },
        new Pattern { id = "highlow", zh = "高低两路", def = '~', stand = 2, rows = new[] { "...--------", "--.........", "...*...*...", "___________" }, tip = "地面有机关，头顶一条单向台面高路：谨慎型会走高路" },
        new Pattern { id = "pit", zh = "陷坑回廊", def = '~', stand = 0, rows = new[] { "b.......", "__CCC___", "__.*.___" }, tip = "塌桥下挖一格深的坑（坑里放火），掉下去能跳出来——地面要 ≥3 格厚" },
        new Pattern { id = "spring", zh = "弹射落点", def = '~', stand = 0, rows = new[] { "b.J..b*" }, tip = "弹簧把他弹上天（空中不能动），落点放机关；弹簧头顶要空 4 格" },
        new Pattern { id = "gate", zh = "关门打狗", def = '~', stand = 0, rows = new[] { "b.*[" }, tip = "封路墙挡他 3.5 秒，让他停在机关上" },
        new Pattern { id = "snare", zh = "回马枪", def = 'Y', stand = 0, rows = new[] { "b..*." }, tip = "放在宝物旁：他拿宝后急着回去，回程第一个坑" },
        new Pattern { id = "cannon", zh = "炮台走廊", def = 'K', stand = 0, rows = new[] { "c.*...." }, tip = "箱子挡在炮后，炮口前空 3 格（每局 1 发，要选准时机）" },
    };

    public static Pattern Get(string id) => Patterns.FirstOrDefault(p => p.id == id);

    /// <summary>盖章：左下角对齐 (x, standY)（y 从下 0）。外圈、M/T/G/o 所在格不动。star = 主角机关（'\0' = 用模式默认）。</summary>
    public static string[] Stamp(IList<string> g, Pattern p, int x, int standY, char star = '\0')
    {
        int h = g.Count, w = g[0].Length;
        var rows = g.Select(r => r.ToCharArray()).ToArray();
        for (int i = 0; i < p.rows.Length; i++)
        {
            int y = standY + (p.stand - i);
            string line = p.rows[i];
            for (int dx = 0; dx < line.Length; dx++)
            {
                char c = line[dx];
                if (c == '_') continue;
                if (c == '*') c = star != '\0' ? star : p.def;
                int nx = x + dx;
                if (nx <= 0 || y <= 0 || nx >= w - 1 || y >= h - 1) continue;
                int row = h - 1 - y;
                if ("MTGo".IndexOf(rows[row][nx]) >= 0) continue;
                rows[row][nx] = c;
            }
        }
        return rows.Select(r => new string(r)).ToArray();
    }

    // ── 新建关卡向导 ─────────────────────────────────
    public static readonly char[] WizardStars = { '~', 'n', '[', 'J', 'Y', 'Q', 'K', 'C' };
    public static int WidthFor(int seconds) => seconds >= 40 ? 94 : seconds >= 30 ? 64 : 48;

    static readonly Dictionary<char, (string id, char star)[]> Recipes = new Dictionary<char, (string, char)[]>
    {
        { '~', new[] { ("ambush", '~'), ("slide", '~'), ("pit", '~'), ("gate", '~') } },
        { 'n', new[] { ("ambush", 'n'), ("slide", 'n'), ("pit", '~'), ("gate", 'n') } },
        { '[', new[] { ("ambush", '['), ("slide", '~'), ("pit", '~'), ("gate", '~') } },
        { 'J', new[] { ("spring", '~'), ("ambush", 'n'), ("pit", '~'), ("gate", '~') } },
        { 'Y', new[] { ("ambush", 'Y'), ("slide", 'Y'), ("pit", '~'), ("gate", 'Y') } },
        { 'Q', new[] { ("ambush", 'Q'), ("slide", 'Q'), ("pit", '~'), ("gate", 'Q') } },
        { 'K', new[] { ("cannon", 'K'), ("cannon", 'K'), ("pit", '~'), ("gate", '~') } },
        { 'C', new[] { ("pit", '~'), ("ambush", '~'), ("spring", '~'), ("pit", '~') } },
    };
    public static (string id, char star)[] Recipe(char star) => Recipes.TryGetValue(star, out var r) ? r : Recipes['~'];

    public static readonly string[] BeatZh = { "起", "承", "转", "合" };
    public static readonly string[] BeatTip =
    {
        "起 · 教：主角机关单独出现，旁边有草丛。先摸清他走多快、什么时候按",
        "承 · 加深：同一个机关接上别的，连成一套",
        "转 · 意外：让地形变一变（塌桥掉坑 / 弹簧弹飞），打乱他的路线",
        "合 · 收尾：宝物旁最后一道——他拿宝后回程第一个就是这里",
    };

    public sealed class Draft
    {
        public string[] grid;
        public int[] beats;                       // 5 个 x：4 段的边界
        public readonly List<(int x, int y, string text)> notes = new List<(int, int, string)>();
        public string goal;
    }

    /// <summary>向导：确定性（同样的选择永远同样的结果）。高 12；下面 3 层地面（挖坑不会挖穿）；马里奥站在 y=3。</summary>
    public static Draft Wizard(char star, int seconds, string idea)
    {
        int w = WidthFor(seconds), h = 12, sy = 3;
        var g = new string[h];
        for (int r = 0; r < h; r++) g[r] = r == 0 ? new string('W', w) : r >= h - 3 ? "W" + new string('#', w - 2) + "W" : "W" + new string('.', w - 2) + "W";
        void Put(int x, char c) { var a = g[h - 1 - sy].ToCharArray(); a[x] = c; g[h - 1 - sy] = new string(a); }
        Put(2, 'G'); Put(4, 'M'); Put(w - 4, 'o');
        int x0 = 7, x4 = w - 6;
        var beats = Enumerable.Range(0, 5).Select(i => RoundJs(x0 + (x4 - x0) * i / 4.0)).ToArray();
        var d = new Draft { beats = beats };
        var recipe = Recipe(star);
        for (int i = 0; i < 4; i++)
        {
            var p = Get(recipe[i].id); int span = beats[i + 1] - beats[i];
            bool extra = span >= 17; // 长关卡：主模式放前面，后面补一个伏击点，避免 10 秒以上什么都没发生
            int px = beats[i] + (extra ? 2 : System.Math.Max(0, (span - p.Width) / 2));
            g = Stamp(g, p, px, sy, recipe[i].star);
            if (extra) g = Stamp(g, Get("ambush"), beats[i] + (int)System.Math.Floor(span * 0.62), sy, i == 2 || recipe[i].star == 'K' ? '~' : recipe[i].star);
            d.notes.Add((px, sy, BeatTip[i] + $"（模式：{p.zh}）"));
        }
        // 捣蛋者出生点：中间附近第一块空地（脚下实心、左右各空 1 格）
        int mid = w / 2, row = h - 1 - sy; bool placed = false;
        for (int dd = 0; dd < w && !placed; dd++)
            foreach (int x in new[] { mid - dd, mid + dd })
            {
                if (x < 6 || x > w - 7) continue;
                if (g[row][x] == '.' && g[row][x - 1] == '.' && g[row][x + 1] == '.' && g[row + 1][x] == '#') { Put(x, 'T'); placed = true; break; }
            }
        string zh = ElementCatalog.Get(star)?.zh ?? star.ToString();
        d.goal = string.IsNullOrWhiteSpace(idea) ? $"这关让马里奥被{zh}坑：起（教）→ 承（连起来）→ 转（地形变了）→ 合（回程第一个坑）" : idea.Trim();
        d.grid = g;
        return d;
    }

    /// <summary>JS Math.round（.5 向上），保证两边分段一模一样。</summary>
    public static int RoundJs(double v) => (int)System.Math.Floor(v + 0.5);

    /// <summary>起承转合分段：马里奥出生点 → 宝物之间平均切 4 段（太短返回 null）。</summary>
    public static int[] BeatBounds(IList<string> g)
    {
        (int x, int y)? Find(char c) { for (int r = 0; r < g.Count; r++) { int i = g[r].IndexOf(c); if (i >= 0) return (i, g.Count - 1 - r); } return null; }
        var M = Find('M'); var O = Find('o') ?? Find('G');
        if (M == null || O == null) return null;
        int a = System.Math.Min(M.Value.x, O.Value.x) + 2, b = System.Math.Max(M.Value.x, O.Value.x) - 1;
        if (b - a < 8) return null;
        return Enumerable.Range(0, 5).Select(i => RoundJs(a + (b - a) * i / 4.0)).ToArray();
    }

    // ── 节奏 ─────────────────────────────────────────
    public const float BusyHalf = 1f, MaxBusy = 8f, MaxIdle = 10f, StartGrace = 4f;
    public sealed class Seg { public float a, b; public bool busy; }

    /// <summary>马里奥每次经过机关的时刻（去程 + 回程都算）。route + 每点时刻 times（同长），stops = 机关格。</summary>
    public static List<float> Passes(IList<(int x, int y)> route, IList<float> times, IEnumerable<(int x, int y)> stops)
    {
        var o = new List<float>();
        if (route == null || times == null) return o;
        foreach (var s in stops)
        {
            bool inPass = false; double best = 1e9; float bt = 0;
            for (int i = 0; i < route.Count; i++)
            {
                double d = System.Math.Sqrt((route[i].x - s.x) * (route[i].x - s.x) + (route[i].y - s.y) * (route[i].y - s.y));
                if (d <= 1.5) { if (!inPass || d < best) { best = d; bt = times[i]; } inPass = true; }
                else if (inPass) { o.Add(bt); inPass = false; best = 1e9; }
            }
            if (inPass) o.Add(bt);
        }
        o.Sort();
        return o;
    }

    /// <summary>机关前后 1 秒 = 紧张，其余 = 喘气。连续紧张 ≥8 秒 / 连续没事 ≥10 秒（开局 4 秒等待不算）→ 提醒。</summary>
    public static (List<Seg> segs, List<string> warn) Rhythm(IEnumerable<float> passes, float total)
    {
        var segs = new List<Seg>(); var warn = new List<string>();
        if (!(total > 0)) return (segs, warn);
        var iv = passes.Select(t => (a: System.Math.Max(0f, t - BusyHalf), b: System.Math.Min(total, t + BusyHalf))).OrderBy(v => v.a).ToList();
        var busy = new List<float[]>();
        foreach (var s in iv) { var l = busy.Count > 0 ? busy[busy.Count - 1] : null; if (l != null && s.a <= l[1]) l[1] = System.Math.Max(l[1], s.b); else busy.Add(new[] { s.a, s.b }); }
        float t0 = 0;
        foreach (var b in busy) { if (b[0] > t0) segs.Add(new Seg { a = t0, b = b[0] }); segs.Add(new Seg { a = b[0], b = b[1], busy = true }); t0 = b[1]; }
        if (t0 < total) segs.Add(new Seg { a = t0, b = total });
        foreach (var s in segs)
        {
            float len = s.b - s.a;
            if (s.busy && len >= MaxBusy) warn.Add($"{s.a:F0}–{s.b:F0} 秒连续 {len:F0} 秒都在机关里，没有喘气的地方：中间空出 3–5 格");
            if (!s.busy && len >= MaxIdle && s.b > StartGrace + 0.01f) warn.Add($"{s.a:F0}–{s.b:F0} 秒连续 {len:F0} 秒什么都没发生：这里可以加一个机关（或者是故意留的长休息）");
        }
        return (segs, warn);
    }

    // ── 转移点提示 ───────────────────────────────────
    /// <summary>草丛、箱子、油桶；随机槽位 1（箱子或草丛）/ 2（草丛或空）也算；房间里的隔墙（不是外圈）也挡视线。</summary>
    public const string CoverChars = "bcU12";

    /// <summary>伪装着站着不动他不会怀疑，但你走过去的路上被看见会起疑：机关 5 格（操控范围）内没有遮挡 → 返回它（整段塌桥/裂缝只报一次）。</summary>
    public static List<(char c, int x, int y)> CoverHints(IList<string> g, IEnumerable<(char c, int x, int y)> stops)
    {
        int h = g.Count; var o = new List<(char, int, int)>();
        foreach (var s in stops)
        {
            int row = h - 1 - s.y; bool ok = false;
            for (int dy = -1; dy <= 2 && !ok; dy++)
                for (int dx = -5; dx <= 5 && !ok; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int r = row - dy, x = s.x + dx;
                    if (r < 0 || r >= h || x <= 0 || x >= g[0].Length - 1) continue;
                    char c = g[r][x];
                    if (CoverChars.IndexOf(c) >= 0 || (c == 'W' && r > 0 && r < h - 1)) ok = true;
                }
            if (!ok && !o.Any(v => v.Item1 == s.c && System.Math.Abs(v.Item2 - s.x) <= 1 && v.Item3 == s.y)) o.Add(s);
        }
        return o;
    }
}
