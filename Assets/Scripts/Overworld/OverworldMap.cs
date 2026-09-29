using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// S210：大地图（星露谷式俯视小镇）的纯逻辑——解析 / 检查 / 寻路 / 视线 / 碰撞 / 马里奥日程。不依赖 UnityEngine，沙盒可测。
/// 网页设计台 logic.js 里 ow* 函数是同一套规则（改一边必须改另一边；verify.sh 会逐项对照）。
///
/// 坐标：x 从左 0、y 从下 0（与横版关卡一致）；格子 (x,y) 占 [x,x+1)×[y,y+1)，中心 (x+0.5, y+0.5)。
/// 文件格式（Assets/Levels/Overworld/名字.txt）：
///   # Overworld: 名字        # Goal: 设计意图        # Note: (x,y) 批注
///   # Door: 1 | 08:00 | 关卡名        ← 门 1 连到哪个横版房间（关卡库名字或样板名），马里奥几点出发去
///   其余每一行 = 网格一行（第一行在最上面）。大地图里没有 '#' 字符，所以 '#' 开头的都是元数据。
/// </summary>
public static class OverworldMap
{
    public const int MinW = 16, MinH = 12, MaxW = 96, MaxH = 64;
    public const int DayStart = 6 * 60, DayEnd = 22 * 60, LatestDoor = 20 * 60;
    public const int MaxPickups = 3;

    public sealed class Door { public int n; public int minute = 8 * 60; public string room = ""; }
    public sealed class Note { public int x, y; public string text = ""; }

    public sealed class Map
    {
        public string name = "", goal = "", id = "";
        public string[] rows = new string[0];
        public readonly List<Door> doors = new List<Door>();
        public readonly List<Note> notes = new List<Note>();
        public int W => rows.Length > 0 ? rows[0].Length : 0;
        public int H => rows.Length;
        public char At(int x, int y)
        {
            int r = H - 1 - y;
            if (r < 0 || r >= H || x < 0 || x >= rows[r].Length) return 't';
            return rows[r][x];
        }
        public Door DoorOf(int n) => doors.FirstOrDefault(d => d.n == n);
    }

    public struct Cell : IEquatable<Cell>
    {
        public int x, y;
        public Cell(int x, int y) { this.x = x; this.y = y; }
        public bool Equals(Cell o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Cell c && Equals(c);
        public override int GetHashCode() => x * 1000 + y;
        public override string ToString() => $"({x},{y})";
    }

    // ── 时间 ───────────────────────────────────────────
    public static string Clock(int minute) { minute = Math.Max(0, minute); return $"{minute / 60:00}:{minute % 60:00}"; }
    public static string Clock(double minute) => Clock((int)Math.Floor(minute));

    public static bool TryParseClock(string s, out int minute)
    {
        minute = 0; s = (s ?? "").Trim().Replace('：', ':');
        var p = s.Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out int h) || !int.TryParse(p[1], out int m) || h < 0 || h > 23 || m < 0 || m > 59) return false;
        minute = h * 60 + m; return true;
    }

    // ── 文本格式 ─────────────────────────────────────────
    public static bool IsOverworldText(string text) => (text ?? "").Replace("\r", "").Split('\n').Any(l => l.StartsWith("# Overworld:"));

    public static Map Parse(string text)
    {
        var m = new Map();
        var grid = new List<string>();
        foreach (var raw in (text ?? "").Replace("\r", "").Replace("\uFEFF", "").Split('\n'))
        {
            if (raw.StartsWith("#"))
            {
                string line = raw.TrimEnd();
                if (line.StartsWith("# Overworld:")) m.name = line.Substring(12).Trim();
                else if (line.StartsWith("# Goal:")) m.goal = line.Substring(7).Trim();
                else if (line.StartsWith("# Source:")) m.id = line.Substring(9).Trim();
                else if (line.StartsWith("# Door:"))
                {
                    var p = line.Substring(7).Split('|');
                    if (p.Length >= 1 && int.TryParse(p[0].Trim(), out int n) && n >= 1 && n <= 9)
                    {
                        var d = new Door { n = n };
                        if (p.Length >= 2 && TryParseClock(p[1], out int mm)) d.minute = mm;
                        if (p.Length >= 3) d.room = string.Join("|", p.Skip(2)).Trim();
                        m.doors.RemoveAll(o => o.n == n); m.doors.Add(d);
                    }
                }
                else if (line.StartsWith("# Note: ("))
                {
                    int close = line.IndexOf(')');
                    var xy = close > 9 ? line.Substring(9, close - 9).Split(',') : new string[0];
                    if (xy.Length == 2 && int.TryParse(xy[0], out int nx) && int.TryParse(xy[1], out int ny))
                        m.notes.Add(new Note { x = nx, y = ny, text = line.Substring(close + 1).Trim() });
                }
                continue;
            }
            string row = raw.TrimEnd();
            if (row.Length > 0) grid.Add(row.Replace(' ', '.'));
        }
        int w = grid.Count > 0 ? grid.Max(r => r.Length) : 0;
        m.rows = grid.Select(r => r.PadRight(w, '.')).ToArray();
        m.doors.Sort((a, b) => a.n.CompareTo(b.n));
        return m;
    }

    public static string ToText(Map m)
    {
        var sb = new StringBuilder();
        sb.Append("# Overworld: ").AppendLine(OneLine(m.name.Length > 0 ? m.name : "未命名小镇"));
        if (m.goal.Length > 0) sb.Append("# Goal: ").AppendLine(OneLine(m.goal));
        if (m.id.Length > 0) sb.Append("# Source: ").AppendLine(OneLine(m.id));
        foreach (var d in m.doors.OrderBy(d => d.n)) sb.Append("# Door: ").Append(d.n).Append(" | ").Append(Clock(d.minute)).Append(" | ").AppendLine(OneLine(d.room).Replace("|", "/"));
        foreach (var n in m.notes) sb.Append("# Note: (").Append(n.x).Append(',').Append(n.y).Append(") ").AppendLine(OneLine(n.text));
        foreach (var r in m.rows) sb.AppendLine(r);
        return sb.ToString();
    }

    /// <summary>关卡包 JSON 里 kind="overworld" 的一关（MiniJson 解析后的字典）→ Map。</summary>
    public static Map FromJson(Dictionary<string, object> d)
    {
        var m = new Map { name = Str(d, "name"), goal = Str(d, "goal"), id = Str(d, "id") };
        if (d.TryGetValue("grid", out var g) && g is List<object> gl) m.rows = gl.Select(x => (x as string ?? "").Replace(' ', '.')).ToArray();
        int w = m.rows.Length > 0 ? m.rows.Max(r => r.Length) : 0;
        m.rows = m.rows.Select(r => r.PadRight(w, '.')).ToArray();
        if (d.TryGetValue("doors", out var ds) && ds is List<object> dl)
            foreach (var o in dl)
                if (o is Dictionary<string, object> dd)
                {
                    int n = (int)Num(dd, "n");
                    if (n < 1 || n > 9) continue;
                    var door = new Door { n = n, room = Str(dd, "room") };
                    if (TryParseClock(Str(dd, "time"), out int mm)) door.minute = mm;
                    m.doors.RemoveAll(x => x.n == n); m.doors.Add(door);
                }
        if (d.TryGetValue("notes", out var ns) && ns is List<object> nl)
            foreach (var o in nl) if (o is Dictionary<string, object> nd) m.notes.Add(new Note { x = (int)Num(nd, "x"), y = (int)Num(nd, "y"), text = Str(nd, "text") });
        m.doors.Sort((a, b) => a.n.CompareTo(b.n));
        return m;
    }

    public static string ToJson(Map m)
    {
        var sb = new StringBuilder("{\"kind\":\"overworld\",\"id\":").Append(Js(m.id)).Append(",\"name\":").Append(Js(m.name)).Append(",\"goal\":").Append(Js(m.goal)).Append(",\"grid\":[");
        sb.Append(string.Join(",", m.rows.Select(Js))).Append("],\"doors\":[");
        sb.Append(string.Join(",", m.doors.OrderBy(d => d.n).Select(d => $"{{\"n\":{d.n},\"time\":{Js(Clock(d.minute))},\"room\":{Js(d.room)}}}"))).Append("],\"notes\":[");
        sb.Append(string.Join(",", m.notes.Select(n => $"{{\"x\":{n.x},\"y\":{n.y},\"text\":{Js(n.text)}}}"))).Append("]}");
        return sb.ToString();
    }

    public static string[] NewMap(int w, int h)
    {
        w = Math.Max(MinW, Math.Min(MaxW, w)); h = Math.Max(MinH, Math.Min(MaxH, h));
        var rows = new string[h];
        for (int r = 0; r < h; r++) rows[r] = r == 0 || r == h - 1 ? new string('t', w) : "t" + new string('.', w - 2) + "t";
        return rows;
    }

    public static List<Cell> Find(Map m, char c)
    {
        var l = new List<Cell>();
        for (int r = 0; r < m.H; r++) for (int x = 0; x < m.rows[r].Length; x++) if (m.rows[r][x] == c) l.Add(new Cell(x, m.H - 1 - r));
        return l;
    }

    public static bool Walkable(Map m, int x, int y) => x >= 0 && y >= 0 && x < m.W && y < m.H && !OverworldCatalog.Solid(m.At(x, y));

    // ── 寻路（Dijkstra，按地面代价；与网页 owPath 完全同序）──────────────
    private static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };

    /// <summary>从 a 走到 b 的格子路线（含两端）。走不到返回 null。</summary>
    public static List<Cell> Path(Map m, Cell a, Cell b)
    {
        int w = m.W, h = m.H;
        if (!Walkable(m, a.x, a.y) || !Walkable(m, b.x, b.y)) return null;
        var dist = new int[w * h]; var prev = new int[w * h];
        for (int i = 0; i < dist.Length; i++) { dist[i] = int.MaxValue; prev[i] = -1; }
        var heap = new MinHeap();
        int s = a.y * w + a.x, goal = b.y * w + b.x;
        dist[s] = 0; heap.Push(0, s);
        while (heap.Count > 0)
        {
            var (d, i) = heap.Pop();
            if (d > dist[i]) continue;
            if (i == goal) break;
            int x = i % w, y = i / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + DX[k], ny = y + DY[k];
                if (!Walkable(m, nx, ny)) continue;
                int j = ny * w + nx, nd = d + OverworldCatalog.Cost(m.At(nx, ny));
                if (nd < dist[j]) { dist[j] = nd; prev[j] = i; heap.Push(nd, j); }
            }
        }
        if (dist[goal] == int.MaxValue) return null;
        var path = new List<Cell>();
        for (int i = goal; i != -1; i = prev[i]) path.Add(new Cell(i % w, i / w));
        path.Reverse();
        return path;
    }

    /// <summary>路线长度（格，按直线步数；4 方向每步 1）。</summary>
    public static int Steps(List<Cell> p) => p == null ? -1 : Math.Max(0, p.Count - 1);

    /// <summary>沿路线走完要几秒：每格按那一格的走路速度倍率（泥地慢、高草稍慢）。</summary>
    public static double WalkSeconds(Map m, List<Cell> p, double speed)
    {
        if (p == null) return -1;
        double t = 0;
        for (int i = 1; i < p.Count; i++) t += 1.0 / (speed * SpeedFactor(m.At(p[i].x, p[i].y)));
        return t;
    }

    /// <summary>地面对走路速度的影响（你和马里奥一样）：泥地 0.55、高草 0.8、其余 1。</summary>
    public static double SpeedFactor(char c) => c == 'g' ? 0.55 : c == '"' ? 0.8 : 1.0;

    private sealed class MinHeap
    {
        private readonly List<(int d, int i)> a = new List<(int, int)>();
        public int Count => a.Count;
        private static bool Less((int d, int i) p, (int d, int i) q) => p.d < q.d || (p.d == q.d && p.i < q.i);
        public void Push(int d, int i)
        {
            a.Add((d, i)); int c = a.Count - 1;
            while (c > 0) { int p = (c - 1) / 2; if (!Less(a[c], a[p])) break; var t = a[c]; a[c] = a[p]; a[p] = t; c = p; }
        }
        public (int, int) Pop()
        {
            var top = a[0]; var last = a[a.Count - 1]; a.RemoveAt(a.Count - 1);
            if (a.Count > 0)
            {
                a[0] = last; int c = 0;
                while (true)
                {
                    int l = c * 2 + 1, r = l + 1, m = c;
                    if (l < a.Count && Less(a[l], a[m])) m = l;
                    if (r < a.Count && Less(a[r], a[m])) m = r;
                    if (m == c) break;
                    var t = a[c]; a[c] = a[m]; a[m] = t; c = m;
                }
            }
            return top;
        }
    }

    // ── 视线 ───────────────────────────────────────────
    /// <summary>从 (ax,ay) 看 (bx,by)：中间有挡视线的格子（房子、树、木箱）→ 看不见。起点与终点所在格不算。</summary>
    public static bool LineOfSight(Map m, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, dist = Math.Sqrt(dx * dx + dy * dy);
        int steps = Math.Max(1, (int)Math.Ceiling(dist * 4));
        int sx = (int)Math.Floor(ax), sy = (int)Math.Floor(ay), ex = (int)Math.Floor(bx), ey = (int)Math.Floor(by);
        for (int i = 1; i < steps; i++)
        {
            double t = (double)i / steps;
            int cx = (int)Math.Floor(ax + dx * t), cy = (int)Math.Floor(ay + dy * t);
            if ((cx == sx && cy == sy) || (cx == ex && cy == ey)) continue;
            if (OverworldCatalog.BlocksSight(m.At(cx, cy))) return false;
        }
        return true;
    }

    /// <summary>路灯照亮：离任意一盏路灯 ≤ radius。</summary>
    public static bool Lit(IList<Cell> lamps, double x, double y, double radius)
    {
        foreach (var l in lamps) { double ddx = l.x + 0.5 - x, ddy = l.y + 0.5 - y; if (ddx * ddx + ddy * ddy <= radius * radius) return true; }
        return false;
    }

    public struct SightRules
    {
        public double range, nightRange, halfAngleDeg, nearRadius, grassRadius, lampRadius;
        public bool night;
    }

    /// <summary>
    /// 马里奥能不能看见 (tx,ty)（H4：只有视锥 + 距离 + 遮挡；高草里只有贴身才看得见；晚上看得近，路灯下照样看得远）。
    /// facing = 他面朝的方向（最后一次走路的方向，单位向量）。
    /// </summary>
    public static bool CanSee(Map m, IList<Cell> lamps, double ex, double ey, double fx, double fy, double tx, double ty, SightRules r)
    {
        double dx = tx - ex, dy = ty - ey, d = Math.Sqrt(dx * dx + dy * dy);
        char under = m.At((int)Math.Floor(tx), (int)Math.Floor(ty));
        if (OverworldCatalog.Hides(under) && d > r.grassRadius) return false;
        if (d > r.nearRadius)
        {
            double range = r.night && !Lit(lamps, tx, ty, r.lampRadius) ? Math.Min(r.range, r.nightRange) : r.range;
            if (d > range) return false;
            double fl = Math.Sqrt(fx * fx + fy * fy);
            if (fl > 1e-6)
            {
                double cos = (dx * fx + dy * fy) / (d * fl);
                if (cos < Math.Cos(r.halfAngleDeg * Math.PI / 180.0)) return false;
            }
        }
        return LineOfSight(m, ex, ey, tx, ty);
    }

    /// <summary>"草晃了"：你在高草里走动，在他视锥和距离内（草本身不挡这次判定）。只知道位置，不知道是谁（H4）。</summary>
    public static bool SeesRustle(Map m, IList<Cell> lamps, double ex, double ey, double fx, double fy, double tx, double ty, SightRules r)
    {
        if (!OverworldCatalog.Hides(m.At((int)Math.Floor(tx), (int)Math.Floor(ty)))) return false;
        var open = r; open.grassRadius = 1e9;
        return CanSee(m, lamps, ex, ey, fx, fy, tx, ty, open);
    }

    // ── 碰撞 ───────────────────────────────────────────
    public const double BodyRadius = 0.3;

    static bool Overlaps(Map m, double x, double y, double r)
    {
        int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Floor(x + r - 1e-9), y0 = (int)Math.Floor(y - r), y1 = (int)Math.Floor(y + r - 1e-9);
        for (int cx = x0; cx <= x1; cx++) for (int cy = y0; cy <= y1; cy++) if (!Walkable(m, cx, cy)) return true;
        return false;
    }

    /// <summary>俯视移动：先 x 后 y 分开试（贴墙能滑），每小步 ≤0.1 格，不会穿墙。</summary>
    public static (double x, double y) Move(Map m, double x, double y, double dx, double dy, double r = BodyRadius)
    {
        int n = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 0.1));
        double sx = dx / n, sy = dy / n;
        for (int i = 0; i < n; i++)
        {
            if (sx != 0 && !Overlaps(m, x + sx, y, r)) x += sx;
            if (sy != 0 && !Overlaps(m, x, y + sy, r)) y += sy;
        }
        return (x, y);
    }

    // ── 日程（马里奥一天去哪几扇门）───────────────────────
    public sealed class Stop { public Door door; public Cell cell; public List<Cell> path; public double depart, arrive, leave, tricksterSeconds, marioSeconds; }
    public sealed class Schedule { public readonly List<Stop> stops = new List<Stop>(); public List<Cell> homePath; public double homeArrive; public bool ok; }

    /// <summary>
    /// 无人捣乱时马里奥的一天（H10 同思路）：按门的时间从家出发 → 走到门 → 进去 visitMinutes → 出来去下一扇门（时间没到就等到点再出发）→ 最后回家。
    /// minutesPerSecond：现实 1 秒 = 游戏几分钟。tricksterSeconds = 你从出生点（第一扇门）/ 上一扇门出发赶到这扇门要几秒（埋伏参考）。
    /// </summary>
    public static Schedule DaySchedule(Map m, double marioSpeed, double tricksterSpeed, double minutesPerSecond, double visitMinutes)
    {
        var sc = new Schedule { ok = true };
        var home = Find(m, 'M'); var tsp = Find(m, 'T');
        if (home.Count != 1) { sc.ok = false; return sc; }
        Cell at = home[0]; Cell tAt = tsp.Count == 1 ? tsp[0] : home[0];
        double now = DayStart;
        foreach (var d in m.doors.OrderBy(d => d.minute).ThenBy(d => d.n))
        {
            var cells = Find(m, (char)('0' + d.n));
            if (cells.Count != 1) { sc.ok = false; continue; }
            var p = Path(m, at, cells[0]);
            if (p == null) { sc.ok = false; continue; }
            var st = new Stop { door = d, cell = cells[0], path = p, depart = Math.Max(now, d.minute) };
            st.marioSeconds = WalkSeconds(m, p, marioSpeed);
            st.arrive = st.depart + st.marioSeconds * minutesPerSecond;
            st.leave = st.arrive + visitMinutes;
            st.tricksterSeconds = WalkSeconds(m, Path(m, tAt, cells[0]), tricksterSpeed);
            sc.stops.Add(st);
            at = cells[0]; tAt = cells[0]; now = st.leave;
        }
        sc.homePath = Path(m, at, home[0]);
        sc.homeArrive = now + (sc.homePath != null ? WalkSeconds(m, sc.homePath, marioSpeed) * minutesPerSecond : 0);
        if (sc.homePath == null || sc.homeArrive > DayEnd) sc.ok = false;
        return sc;
    }

    // ── 检查 ───────────────────────────────────────────
    public enum Sev { Error, Warn, Info }
    public sealed class Issue { public int x = -1, y = -1; public string text; public Sev sev; public override string ToString() => (x >= 0 ? $"({x},{y}) " : "") + text; }
    public sealed class Report
    {
        public readonly List<Issue> issues = new List<Issue>();
        public Schedule schedule;
        public bool Playable => issues.All(i => i.sev != Sev.Error);
        public int Errors => issues.Count(i => i.sev == Sev.Error);
        public string Headline => Playable ? $"✓ 可以试玩（{issues.Count(i => i.sev == Sev.Warn)} 个提醒）" : $"✗ {Errors} 个问题要改";
    }

    public struct Rules
    {
        public double marioSpeed, tricksterSpeed, minutesPerSecond, visitMinutes;
        public static Rules Default => new Rules { marioSpeed = 3.4, tricksterSpeed = 5.0, minutesPerSecond = 4.0, visitMinutes = 60.0 };
    }

    /// <summary>
    /// 大地图检查（网页 owCheck 同规则）。roomOk(名字) 返回 null = 这个房间找得到且能玩；否则返回原因。传 null 跳过房间检查。
    /// </summary>
    public static Report Check(Map m, Rules rules, Func<string, string> roomOk = null)
    {
        var rep = new Report();
        void E(string t, int x = -1, int y = -1) => rep.issues.Add(new Issue { text = t, x = x, y = y, sev = Sev.Error });
        void Wn(string t, int x = -1, int y = -1) => rep.issues.Add(new Issue { text = t, x = x, y = y, sev = Sev.Warn });
        void I(string t, int x = -1, int y = -1) => rep.issues.Add(new Issue { text = t, x = x, y = y, sev = Sev.Info });
        int w = m.W, h = m.H;
        if (w == 0 || h == 0) { E("画布是空的"); return rep; }
        if (m.rows.Any(r => r.Length != w)) { E("每一行长度要一样"); return rep; }
        if (w < MinW || h < MinH) E($"小镇太小：至少 {MinW} 宽 × {MinH} 高（现在 {w}×{h}）");
        if (w > MaxW || h > MaxH) E($"小镇太大：最多 {MaxW} 宽 × {MaxH} 高（现在 {w}×{h}）");
        for (int r = 0; r < h; r++) for (int x = 0; x < w; x++)
            if (!OverworldCatalog.Known(m.rows[r][x])) { E($"不认识的字符 '{m.rows[r][x]}'（大地图只能用大地图的格子）", x, h - 1 - r); return rep; }
        bool frame = true;
        for (int x = 0; x < w && frame; x++) if (!OverworldCatalog.Solid(m.rows[0][x]) || !OverworldCatalog.Solid(m.rows[h - 1][x])) frame = false;
        for (int r = 0; r < h && frame; r++) if (!OverworldCatalog.Solid(m.rows[r][0]) || !OverworldCatalog.Solid(m.rows[r][w - 1])) frame = false;
        if (!frame) E("最外一圈必须挡路（树 t / 房屋 W / 水 w / 栅栏 f），不然会走出地图");
        foreach (char c in "MT") { int n = Find(m, c).Count; if (n != 1) E($"需要且只能有一个{OverworldCatalog.Get(c).zh} {c}（现在 {n} 个）"); }
        var doorCells = new Dictionary<int, Cell>();
        for (int n = 1; n <= 9; n++)
        {
            var cs = Find(m, (char)('0' + n));
            if (cs.Count > 1) E($"门 {n} 画了 {cs.Count} 个：每个数字只能用一次", cs[1].x, cs[1].y);
            if (cs.Count >= 1) doorCells[n] = cs[0];
        }
        if (doorCells.Count == 0) E("至少要有一扇门（数字 1–9）：门连到横版房间，马里奥每天去门里拿宝");
        int pickups = Find(m, '?').Count;
        if (pickups > MaxPickups) E($"道具箱最多 {MaxPickups} 个（现在 {pickups} 个）");
        if (!rep.Playable) return rep;

        var home = Find(m, 'M')[0]; var tsp = Find(m, 'T')[0];
        foreach (var kv in doorCells)
        {
            var d = m.DoorOf(kv.Key); var c = kv.Value;
            if (d == null || string.IsNullOrWhiteSpace(d.room)) { E($"门 {kv.Key} 还没连房间（右边选它通到哪个横版关卡）", c.x, c.y); continue; }
            if (d.minute < DayStart || d.minute > LatestDoor) E($"门 {kv.Key} 的时间 {Clock(d.minute)} 要在 06:00–20:00 之间", c.x, c.y);
            if (roomOk != null) { string why = roomOk(d.room); if (why != null) E($"门 {kv.Key} 连的房间「{d.room}」{why}", c.x, c.y); }
            if (Path(m, home, c) == null) E($"马里奥从家走不到门 {kv.Key}", c.x, c.y);
            if (Path(m, tsp, c) == null) E($"你（捣蛋者）走不到门 {kv.Key}", c.x, c.y);
            bool nearHouse = false;
            for (int k = 0; k < 4; k++) if (m.At(c.x + DX[k], c.y + DY[k]) == 'W') nearHouse = true;
            if (!nearHouse) Wn($"门 {kv.Key} 旁边没有房屋 W：画在房子墙面前一格，玩家一眼就知道这是门", c.x, c.y);
            bool cover = false;
            for (int yy = c.y - 4; yy <= c.y + 4 && !cover; yy++) for (int xx = c.x - 4; xx <= c.x + 4; xx++)
                if ((xx != c.x || yy != c.y) && "\"ct".IndexOf(m.At(xx, yy)) >= 0 && !(xx <= 0 || yy <= 0 || xx >= w - 1 || yy >= h - 1)) { cover = true; break; }
            if (!cover) I($"门 {kv.Key} 附近 4 格内没有高草/木箱/树：你在门口等他时没地方躲", c.x, c.y);
        }
        foreach (var d in m.doors) if (!doorCells.ContainsKey(d.n)) Wn($"门 {d.n} 有设置但地图上没画（多余的设置会被忽略）");
        var times = m.doors.Where(d => doorCells.ContainsKey(d.n)).GroupBy(d => d.minute).Where(g => g.Count() > 1).ToList();
        foreach (var g in times) E($"门 {string.Join("、", g.Select(d => d.n))} 的时间都是 {Clock(g.Key)}：每扇门的时间要不一样（马里奥一次只去一扇）");
        if (Path(m, home, tsp) == null) E("你的出生点和马里奥的家不连通");
        if (!rep.Playable) return rep;

        var sc = DaySchedule(m, rules.marioSpeed, rules.tricksterSpeed, rules.minutesPerSecond, rules.visitMinutes);
        rep.schedule = sc;
        if (sc.homePath == null) E("马里奥最后回不了家");
        else if (sc.homeArrive > DayEnd) E($"日程太满：没人捣乱时他 {Clock(sc.homeArrive)} 才到家，要在 {Clock(DayEnd)} 前（把门的时间提前、少一扇门，或把门放近一点）");
        for (int k = 0; k < sc.stops.Count; k++)
        {
            var st = sc.stops[k];
            if (k > 0 && st.door.minute < sc.stops[k - 1].leave - 0.01) I($"门 {st.door.n}：他 {Clock(sc.stops[k - 1].leave)} 才从上一扇门出来，比 {Clock(st.door.minute)} 晚，会马上直接过去（你少了准备时间）", st.cell.x, st.cell.y);
            double lead = AmbushLead(sc, k, rules.minutesPerSecond);
            if (lead < 0) Wn($"门 {st.door.n}：你全速赶过去也比他晚 {-lead:0.0} 秒——只能用香蕉皮/挑衅拖住他，或把门的时间往后挪", st.cell.x, st.cell.y);
        }
        int lamps = Find(m, 'i').Count;
        if (lamps == 0 && sc.stops.Any(s => s.arrive >= NightStart)) I("晚上还有门要去，但地图上没有路灯 i：晚上他看得很近，门口全是暗处（对你很有利）");
        return rep;
    }

    public const int NightStart = 19 * 60;

    /// <summary>这扇门你能提前几秒（现实秒）到（正数 = 能先到，可以埋伏）。第一扇门：你 06:00 从出生点出发；之后：从上一扇门出来就出发。</summary>
    public static double AmbushLead(Schedule sc, int index, double minutesPerSecond)
    {
        var st = sc.stops[index];
        double prevFree = index == 0 ? DayStart : sc.stops[index - 1].leave; // 上一个房间打完，你和他一起从那扇门出来
        double youArrive = prevFree + st.tricksterSeconds * minutesPerSecond;
        return (st.arrive - youArrive) / minutesPerSecond;
    }

    private static string OneLine(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
    private static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is string s ? s : "";
    private static double Num(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double n ? n : 0;
    private static string Js(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s ?? "")
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c == '\n') sb.Append("\\n");
            else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }
}
