using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// S218：小镇大机关（比房间里的机关夸张一个量级：巨炮把人轰过半个镇、滚石一路撞碎木箱栅栏、水塔把路口淹成泥地）——纯几何规则（沙盒可测）。
/// 网页 overworld.js 的 owProps* 是逐行移植（检查文字 / 总览文字逐字一致，verify.sh 对照）。
///
/// 设计依据（调研见 docs/step1/S218_BIG_TOWN_PRANKS.md）：
///  · BotW GDC 2017 化学引擎"三条规则"：元素改变材质、元素互相改变、材质之间不互相影响 → 本文件只有 3 种"作用"：冲击（炮弹落地 / 滚石撞停）会震响 1.5 格内的大机关；水只改地面、冲活香蕉皮；滚石只撞碎木箱/栅栏。
///  · Into the Breach：结果完全可预判（炮口、滚道、淹没范围编辑器和游戏里都画出来）；随机只在"做决定之前公布"（Keith Burgun：输入随机）→ 天气在 06:00 公布，见 OverworldEvents。
///  · Noita：会改地形的涌现要能复原 → 所有地形改变只活一天，并且只会"打开"地形（撞碎 / 变泥地都还能走）：H1 永远不会被机关关死。
///  · Mario Maker：作者检查——靶心落点走不回家 = 红色错误（被轰过去就困住）。
/// </summary>
public static class OverworldProps
{
    public const int Muzzle = 3, MaxShot = 96, WindShift = 3, FloodRadius = 3, MaxRoll = 64, MaxBig = 12;
    public const double ChainRadius = 1.5;
    public static readonly int[] DX = { 1, -1, 0, 0 }, DY = { 0, 0, 1, -1 };
    public static readonly string[] DirZh = { "右", "左", "上", "下" };

    public static bool IsBig(char c) => c == 'K' || c == 'O' || c == 'U' || c == 'B'; // S228：钟楼 B
    public static string Label(OverworldMap.Map m, OverworldMap.Cell c) { char ch = m.At(c.x, c.y); var t = OverworldCatalog.Get(ch); return $"{(t != null ? t.zh : "?")}{ch}({c.x},{c.y})"; }

    /// <summary>扫描顺序：从上到下、从左到右（和 OverworldMap.Find 一样）。</summary>
    public static List<OverworldMap.Cell> All(OverworldMap.Map m)
    {
        var l = new List<OverworldMap.Cell>();
        for (int r = 0; r < m.H; r++) for (int x = 0; x < m.W; x++) if (IsBig(m.rows[r][x])) l.Add(new OverworldMap.Cell(x, m.H - 1 - r));
        return l;
    }

    // ── 巨炮：同一行 / 列最近的靶心 X（平手按 右 左 上 下）──
    public static bool Aim(OverworldMap.Map m, OverworldMap.Cell k, out OverworldMap.Cell target, out int dir, out int dist)
    {
        target = k; dir = -1; dist = int.MaxValue;
        for (int d = 0; d < 4; d++)
            for (int s = 1; s <= MaxShot; s++)
            {
                int x = k.x + DX[d] * s, y = k.y + DY[d] * s;
                if (x < 0 || y < 0 || x >= m.W || y >= m.H) break;
                if (m.At(x, y) == 'X') { if (s < dist) { dist = s; dir = d; target = new OverworldMap.Cell(x, y); } break; }
            }
        return dir >= 0;
    }

    /// <summary>炮口：朝靶心方向紧挨着的能走的格子（最多 3 格，碰到挡路就停）。站在这里的人会被轰走。</summary>
    public static List<OverworldMap.Cell> MuzzleCells(OverworldMap.Map m, OverworldMap.Cell k, int dir)
    {
        var l = new List<OverworldMap.Cell>();
        for (int s = 1; s <= Muzzle; s++) { int x = k.x + DX[dir] * s, y = k.y + DY[dir] * s; if (!OverworldMap.Walkable(m, x, y)) break; l.Add(new OverworldMap.Cell(x, y)); }
        return l;
    }

    /// <summary>落点：靶心（大风天往风向偏 3 格），再找最近的能走的格（一圈一圈往外找，顺序固定）。</summary>
    public static OverworldMap.Cell Landing(OverworldMap.Map m, OverworldMap.Cell target, int wind)
    {
        int px = target.x, py = target.y;
        if (wind >= 0) { px += DX[wind] * WindShift; py += DY[wind] * WindShift; }
        px = Math.Max(1, Math.Min(m.W - 2, px)); py = Math.Max(1, Math.Min(m.H - 2, py));
        for (int r = 0; r <= 12; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    if (OverworldMap.Walkable(m, px + dx, py + dy)) return new OverworldMap.Cell(px + dx, py + dy);
                }
        return target;
    }

    // ── 滚石：一路滚，撞碎木箱 c / 栅栏 f，撞上别的挡路就停 ──
    public static bool Smashable(char c) => c == 'c' || c == 'f';

    public static List<OverworldMap.Cell> Lane(OverworldMap.Map m, OverworldMap.Cell o, int dir, List<OverworldMap.Cell> smashed = null)
    {
        var l = new List<OverworldMap.Cell>();
        for (int s = 1; s <= MaxRoll; s++)
        {
            int x = o.x + DX[dir] * s, y = o.y + DY[dir] * s;
            if (x <= 0 || y <= 0 || x >= m.W - 1 || y >= m.H - 1) break; // 最外一圈永远不动（H1：不会滚出一个缺口）
            char c = m.At(x, y);
            if (OverworldMap.Walkable(m, x, y)) l.Add(new OverworldMap.Cell(x, y));
            else if (Smashable(c)) { l.Add(new OverworldMap.Cell(x, y)); smashed?.Add(new OverworldMap.Cell(x, y)); }
            else break;
        }
        return l;
    }

    /// <summary>推的方向 = 从你（或冲击点）指向滚石的主方向；那边滚不动就按 右 左 上 下 找第一个能滚的。-1 = 四面都被堵死。</summary>
    public static int PushDir(OverworldMap.Map m, OverworldMap.Cell o, double fromX, double fromY)
    {
        double dx = o.x + 0.5 - fromX, dy = o.y + 0.5 - fromY;
        int p = Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 0 : 1) : (dy >= 0 ? 2 : 3);
        if (Lane(m, o, p).Count > 0) return p;
        for (int d = 0; d < 4; d++) if (Lane(m, o, d).Count > 0) return d;
        return -1;
    }

    // ── 水塔：半径内的草地 / 石子路 / 高草变泥地 ──
    public static bool Floodable(char c) => c == '.' || c == '=' || c == '"';

    public static List<OverworldMap.Cell> Flood(OverworldMap.Map m, OverworldMap.Cell u, int radius)
    {
        var l = new List<OverworldMap.Cell>();
        for (int y = u.y - radius; y <= u.y + radius; y++)
            for (int x = u.x - radius; x <= u.x + radius; x++)
            {
                if ((x - u.x) * (x - u.x) + (y - u.y) * (y - u.y) > radius * radius) continue;
                if (x <= 0 || y <= 0 || x >= m.W - 1 || y >= m.H - 1) continue;
                if (Floodable(m.At(x, y))) l.Add(new OverworldMap.Cell(x, y));
            }
        return l;
    }

    /// <summary>冲击点 radius 格内的大机关（按距离、再按扫描顺序）。</summary>
    public static List<OverworldMap.Cell> ChainTargets(OverworldMap.Map m, double ix, double iy, double radius = ChainRadius)
    {
        var l = new List<(double d, int k, OverworldMap.Cell c)>(); int k = 0;
        foreach (var c in All(m)) { double dx = c.x + 0.5 - ix, dy = c.y + 0.5 - iy, d = dx * dx + dy * dy; if (d <= radius * radius + 1e-9) l.Add((d, k, c)); k++; }
        return l.OrderBy(t => t.d).ThenBy(t => t.k).Select(t => t.c).ToList();
    }

    // ── S219：巨炮现场瞄准（方向键移落点，发射前落点画出来；落点走不回家 = 不许发射，H1）──
    public const int MinAim = 3, MaxAim = 24;

    /// <summary>默认瞄准 = 靶心 X（没有靶心就朝第一个有炮口的方向打 12 格）。</summary>
    public static void DefaultAim(OverworldMap.Map m, OverworldMap.Cell k, out int dir, out int dist)
    {
        if (Aim(m, k, out _, out dir, out dist)) { dist = Math.Max(MinAim, dist); return; } // 靶心比 24 格远也照旧（S218 的图不变）
        dir = 0; dist = 12;
        for (int d = 0; d < 4; d++) if (MuzzleCells(m, k, d).Count > 0) { dir = d; return; }
    }

    /// <summary>按一下方向键：顺着炮管 = 远 1 格；反着 = 近 1 格（近到头就调头）；横着 = 炮管转过去（距离不变）。炮口被堵死的方向跳过。</summary>
    public static void AimStep(OverworldMap.Map m, OverworldMap.Cell k, ref int dir, ref int dist, int key)
    {
        if (key < 0 || key > 3) return;
        int opp = dir ^ 1;
        if (key == dir) { dist = Math.Min(Math.Max(MaxAim, dist), dist + 1); return; }
        if (key == opp) { if (dist > MinAim) { dist--; return; } if (MuzzleCells(m, k, opp).Count > 0) dir = opp; return; }
        if (MuzzleCells(m, k, key).Count > 0) dir = key;
    }

    /// <summary>S228 房间 → 小镇：他在房间里挨了你的炮 → 出门时从门口往"离开房子"的方向（房子 W 在门的哪边就往反方向；
    /// 都没挨着房子就往下）被轰出去 cells 格。落点必须能走、走得回家（H1）；远的不行就近一格，全不行 = 不轰（返回 null）。</summary>
    public static OverworldMap.Cell? WindowLanding(OverworldMap.Map m, OverworldMap.Cell door, int cells, OverworldMap.Cell home)
    {
        int dir = 3; // 下
        for (int d = 0; d < 4; d++) if (m.At(door.x + DX[d], door.y + DY[d]) == 'W') { dir = d ^ 1; break; }
        for (int s = Math.Max(1, cells); s >= 2; s--)
        {
            int x = door.x + DX[dir] * s, y = door.y + DY[dir] * s;
            if (x <= 0 || y <= 0 || x >= m.W - 1 || y >= m.H - 1 || !OverworldMap.Walkable(m, x, y)) continue;
            var c = new OverworldMap.Cell(x, y);
            if (OverworldMap.Path(m, c, home) != null) return c;
        }
        return null;
    }

    /// <summary>瞄准的落点（和真的发射一样：先偏风，再找最近的能走的格）。</summary>
    public static OverworldMap.Cell AimLanding(OverworldMap.Map m, OverworldMap.Cell k, int dir, int dist, int wind)
        => Landing(m, new OverworldMap.Cell(k.x + DX[dir] * dist, k.y + DY[dir] * dist), wind);

    /// <summary>这一炮能不能打：炮口有空地、落点走得回马里奥的家（被轰过去不会困住，H1）。</summary>
    public static bool AimOk(OverworldMap.Map m, OverworldMap.Cell k, int dir, OverworldMap.Cell land, OverworldMap.Cell home)
        => MuzzleCells(m, k, dir).Count > 0 && OverworldMap.Path(m, land, home) != null;

    /// <summary>从 c 出发到每一格的步数（4 方向 BFS，-1 = 走不到）。马里奥算"坐炮值不值"用：每站只算两次。</summary>
    public static int[] StepsField(OverworldMap.Map m, OverworldMap.Cell c)
    {
        int w = m.W, h = m.H; var d = new int[w * h]; for (int i = 0; i < d.Length; i++) d[i] = -1;
        if (!OverworldMap.Walkable(m, c.x, c.y)) return d;
        var q = new Queue<int>(); d[c.y * w + c.x] = 0; q.Enqueue(c.y * w + c.x);
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % w, y = i / w;
            for (int k = 0; k < 4; k++) { int nx = x + DX[k], ny = y + DY[k]; if (!OverworldMap.Walkable(m, nx, ny)) continue; int j = ny * w + nx; if (d[j] >= 0) continue; d[j] = d[i] + 1; q.Enqueue(j); }
        }
        return d;
    }

    // ── S219：山地（高度）/ 山洞 / 泥石流 / 闪电 ──
    public const int MudLen = 6;
    public const double LightningRadius = 1.5;

    /// <summary>高度：山 A = 2，山丘 ^ = 1，其余 0。视线：中间的格子比两个人都高 → 挡住（躲在山丘后面）。</summary>
    public static int Height(char c) => c == 'A' ? 2 : c == '^' ? 1 : 0;

    /// <summary>山洞一对一对（扫描顺序：从上到下、从左到右；第 1 和第 2 个一对……）。单出来的那个没有另一头。</summary>
    public static OverworldMap.Cell? CaveExit(OverworldMap.Map m, OverworldMap.Cell c)
    {
        var l = OverworldMap.Find(m, 'h'); int i = l.IndexOf(c);
        if (i < 0) return null; int j = i ^ 1;
        return j < l.Count ? l[j] : (OverworldMap.Cell?)null;
    }

    public static bool NextTo(OverworldMap.Map m, OverworldMap.Cell c, char k) { for (int d = 0; d < 4; d++) if (m.At(c.x + DX[d], c.y + DY[d]) == k) return true; return false; }

    /// <summary>泥石流的源头 = 紧挨着山 A 的山丘 ^。方向 = 离开山（第一个挨着的山按 右 左 上 下 找，往反方向冲）；-1 = 不是源头。</summary>
    public static int MudDir(OverworldMap.Map m, OverworldMap.Cell c)
    {
        if (m.At(c.x, c.y) != '^') return -1;
        for (int d = 0; d < 4; d++) if (m.At(c.x + DX[d], c.y + DY[d]) == 'A') return d ^ 1;
        return -1;
    }

    /// <summary>泥石流冲过的格子（最多 6 格；能走的 / 木箱栅栏冲垮；碰到别的挡路就停；不碰最外一圈）。全部变泥地——只会"打开"，H1 不会关死。</summary>
    public static List<OverworldMap.Cell> MudLane(OverworldMap.Map m, OverworldMap.Cell c, List<OverworldMap.Cell> smashed = null)
    {
        var l = new List<OverworldMap.Cell>(); int dir = MudDir(m, c); if (dir < 0) return l;
        for (int s = 1; s <= MudLen; s++)
        {
            int x = c.x + DX[dir] * s, y = c.y + DY[dir] * s;
            if (x <= 0 || y <= 0 || x >= m.W - 1 || y >= m.H - 1) break;
            char ch = m.At(x, y);
            if (OverworldMap.Walkable(m, x, y)) l.Add(new OverworldMap.Cell(x, y));
            else if (Smashable(ch)) { l.Add(new OverworldMap.Cell(x, y)); smashed?.Add(new OverworldMap.Cell(x, y)); }
            else break;
        }
        return l;
    }

    /// <summary>泥石流会把哪些格子变泥地：地面（草 / 石子路 / 高草 / 山丘）和冲垮的木箱栅栏。门、家、出生点、山洞、靶心、道具冲过去但不改。</summary>
    public static bool Muddable(char c) => Floodable(c) || c == '^' || Smashable(c);

    /// <summary>冲击点 1.5 格内的泥石流源头（只在湿的天：雨 / 雷雨 / 酸雨）。</summary>
    public static List<OverworldMap.Cell> MudSources(OverworldMap.Map m, double ix, double iy)
    {
        var l = new List<OverworldMap.Cell>();
        foreach (var c in OverworldMap.Find(m, '^'))
        {
            if (MudDir(m, c) < 0) continue;
            double dx = c.x + 0.5 - ix, dy = c.y + 0.5 - iy;
            if (dx * dx + dy * dy <= ChainRadius * ChainRadius + 1e-9) l.Add(c);
        }
        return l;
    }

    // ── 检查（OverworldMap.Check 里调用；网页 owCheck 同位置同文字）──
    public static void CheckCounts(OverworldMap.Map m, Action<string, int, int> E, Action<string, int, int> Wn)
    {
        var all = All(m);
        if (all.Count > MaxBig) E($"大机关（巨炮 / 滚石 / 水塔）最多 {MaxBig} 个（现在 {all.Count} 个）：太多了玩家记不住，也看不清谁连着谁", -1, -1);
        foreach (var c in all)
        {
            if (m.At(c.x, c.y) != 'K') continue;
            if (!Aim(m, c, out _, out int dir, out _))
            {
                bool any = false; for (int d = 0; d < 4; d++) if (MuzzleCells(m, c, d).Count > 0) any = true;
                if (!any) E("巨炮 K 四面都被挡住：炮口前要空地（最好 3 格）", c.x, c.y);
                else Wn("巨炮 K 同一行 / 同一列找不到靶心 X：只能坐进去自己瞄（远程按 L / 被连锁震响时没有默认落点，会打 12 格远）", c.x, c.y); // S219：能瞄准了，靶心只是默认落点
                continue;
            }
            if (MuzzleCells(m, c, dir).Count == 0) E($"巨炮 K 朝靶心那边第一格就被挡住：炮口前要空地（最好 3 格）", c.x, c.y);
        }
    }

    /// <summary>S219：山洞 / 山的提醒（黄色，不挡试玩）。</summary>
    public static void CheckMountains(OverworldMap.Map m, Action<string, int, int> Wn)
    {
        var caves = OverworldMap.Find(m, 'h');
        if (caves.Count % 2 == 1) { var c = caves[caves.Count - 1]; Wn($"山洞 h 有 {caves.Count} 个：两个一对，最后一个没配对（按 E 钻不过去）", c.x, c.y); }
        foreach (var c in caves) if (!NextTo(m, c, 'A')) Wn("山洞 h 旁边没有山 A：画在山脚下，玩家一眼就知道这是洞", c.x, c.y);
    }

    public static void CheckReach(OverworldMap.Map m, OverworldMap.Cell home, Action<string, int, int> E, Action<string, int, int> Wn)
    {
        foreach (var c in All(m))
        {
            char ch = m.At(c.x, c.y);
            if (ch == 'K' && Aim(m, c, out var tg, out _, out _))
            {
                for (int w = -1; w < 4; w++)
                {
                    var l = Landing(m, tg, w);
                    if (OverworldMap.Path(m, l, home) == null) { E($"巨炮 K 的落点 {l}{(w < 0 ? "" : "（大风往" + DirZh[w] + "吹）")}走不回马里奥的家：被轰过去就困住了（把靶心 X 挪到开阔处）", c.x, c.y); break; }
                }
            }
            else if (ch == 'O')
            {
                int best = 0; for (int d = 0; d < 4; d++) best = Math.Max(best, Lane(m, c, d).Count);
                if (best < 3) Wn($"滚石 O 四个方向最多只能滚 {best} 格：放在长直路的一头才有用", c.x, c.y);
            }
        }
    }

    // ── 总览（编辑器侧栏"大机关 · 连锁"；网页同文字）──
    public struct Line { public string text; public int x, y; }

    /// <summary>这个大机关发动后，冲击会震响谁（不算天气）。巨炮 = 靶心落点周围；滚石 = 任一方向滚到头周围；水塔 = 没有（水不引爆东西）。</summary>
    public static List<OverworldMap.Cell> Triggers(OverworldMap.Map m, OverworldMap.Cell c)
    {
        var res = new List<OverworldMap.Cell>(); char ch = m.At(c.x, c.y);
        void Add(double ix, double iy) { foreach (var t in ChainTargets(m, ix, iy)) if (!t.Equals(c) && !res.Contains(t)) res.Add(t); }
        if (ch == 'K' && Aim(m, c, out var tg, out _, out _)) { var l = Landing(m, tg, -1); Add(l.x + 0.5, l.y + 0.5); }
        else if (ch == 'O') for (int d = 0; d < 4; d++) { var lane = Lane(m, c, d); if (lane.Count > 0) Add(lane[lane.Count - 1].x + 0.5, lane[lane.Count - 1].y + 0.5); }
        return res;
    }

    public static List<Line> Describe(OverworldMap.Map m)
    {
        var lines = new List<Line>();
        var all = All(m);
        if (all.Count == 0) { lines.Add(new Line { text = "还没有大机关：巨炮 K + 靶心 X、滚石 O、水塔 U 是小镇专用的夸张机关（L 发动）", x = -1, y = -1 }); DescribeMountains(m, lines); return lines; }
        foreach (var c in all)
        {
            char ch = m.At(c.x, c.y); string s;
            if (ch == 'K')
            {
                if (!Aim(m, c, out var tg, out int dir, out int dist)) s = $"{Label(m, c)}：同一行 / 列没有靶心 X（坐进去自己瞄）";
                else
                {
                    var l = Landing(m, tg, -1); var near = new List<string>();
                    foreach (var dc in DoorsNear(m, l, 3)) near.Add("门 " + m.At(dc.x, dc.y));
                    foreach (var t in Triggers(m, c)) near.Add("震响 " + Label(m, t));
                    int peels = 0; for (int yy = l.y - 1; yy <= l.y + 1; yy++) for (int xx = l.x - 1; xx <= l.x + 1; xx++) if (m.At(xx, yy) == 'n') peels++;
                    if (peels > 0) near.Add($"香蕉皮 {peels}");
                    int dirs = 0; for (int d = 0; d < 4; d++) if (MuzzleCells(m, c, d).Count > 0) dirs++;
                    s = $"{Label(m, c)} → 靶心 X({tg.x},{tg.y})：往{DirZh[dir]}飞 {dist} 格，炮口 {MuzzleCells(m, c, dir).Count} 格；落点旁：{(near.Count > 0 ? string.Join("、", near) : "空地")}；坐进去能瞄 {dirs} 个方向";
                }
            }
            else if (ch == 'O')
            {
                var parts = new List<string>();
                for (int d = 0; d < 4; d++)
                {
                    var sm = new List<OverworldMap.Cell>(); var lane = Lane(m, c, d, sm);
                    if (lane.Count == 0) continue;
                    var end = lane[lane.Count - 1]; var tr = ChainTargets(m, end.x + 0.5, end.y + 0.5).Where(t => !t.Equals(c)).Select(t => Label(m, t)).ToList();
                    parts.Add($"往{DirZh[d]} {lane.Count} 格" + (sm.Count > 0 ? $"（撞碎 {sm.Count}）" : "") + (tr.Count > 0 ? " → 震响 " + string.Join("、", tr) : ""));
                }
                s = $"{Label(m, c)}：" + (parts.Count > 0 ? string.Join("；", parts) : "四面堵死，推不动");
            }
            else if (ch == 'B') s = $"{Label(m, c)}：钟一响全镇听见（他停下转头看），不伤人；冲击 {ChainRadius} 格内会震响它";
            else
            {
                var f = Flood(m, c, FloodRadius); var f2 = Flood(m, c, FloodRadius + 1);
                int grass = f.Count(p => m.At(p.x, p.y) == '"'), path = f.Count(p => m.At(p.x, p.y) == '='), peels = 0;
                for (int yy = c.y - FloodRadius; yy <= c.y + FloodRadius; yy++) for (int xx = c.x - FloodRadius; xx <= c.x + FloodRadius; xx++)
                    if ((xx - c.x) * (xx - c.x) + (yy - c.y) * (yy - c.y) <= FloodRadius * FloodRadius && m.At(xx, yy) == 'n') peels++;
                s = $"{Label(m, c)}：淹 {f.Count} 格变泥地（雨天 {f2.Count} 格），其中石子路 {path}" + (grass > 0 ? $"、高草 {grass}（你的藏身处也没了）" : "") + (peels > 0 ? $"；冲活香蕉皮 {peels}" : "");
            }
            lines.Add(new Line { text = s, x = c.x, y = c.y });
        }
        var chain = LongestChain(m);
        lines.Add(chain.Count >= 2
            ? new Line { text = $"最长连锁：{string.Join(" → ", chain.Select(c => Label(m, c)))}（{chain.Count} 连）", x = chain[0].x, y = chain[0].y }
            : new Line { text = "还没有连锁：把靶心 X 放在滚石 / 水塔 / 另一门巨炮旁 1 格内，或让滚石滚到头正好撞上它们", x = -1, y = -1 });
        DescribeMountains(m, lines);
        return lines;
    }

    /// <summary>S219：山洞配对、泥石流源头（下雨天被冲击才会冲）、雷雨天能召唤闪电的路灯。</summary>
    public static void DescribeMountains(OverworldMap.Map m, List<Line> lines)
    {
        var caves = OverworldMap.Find(m, 'h');
        for (int i = 0; i + 1 < caves.Count; i += 2) lines.Add(new Line { text = $"山洞 h({caves[i].x},{caves[i].y}) ⇄ h({caves[i + 1].x},{caves[i + 1].y})：钻进去按 E 从另一头出来（马里奥不知道这条路）", x = caves[i].x, y = caves[i].y });
        foreach (var c in OverworldMap.Find(m, '^'))
        {
            int d = MudDir(m, c); if (d < 0) continue;
            var sm = new List<OverworldMap.Cell>(); var lane = MudLane(m, c, sm); if (lane.Count == 0) continue;
            int path = lane.Count(p => m.At(p.x, p.y) == '=');
            lines.Add(new Line { text = $"泥石流 ^({c.x},{c.y})：下雨天被冲击 → 往{DirZh[d]}冲 {lane.Count} 格变泥地" + (path > 0 ? $"（石子路 {path}）" : "") + (sm.Count > 0 ? $"，冲垮 {sm.Count}" : ""), x = c.x, y = c.y });
        }
        foreach (var c in OverworldMap.Find(m, 'i'))
        {
            var near = new List<string>();
            foreach (var t in ChainTargets(m, c.x + 0.5, c.y + 0.5, LightningRadius)) near.Add("震响 " + Label(m, t));
            foreach (var t in MudSources(m, c.x + 0.5, c.y + 0.5)) near.Add($"泥石流 ^({t.x},{t.y})");
            if (near.Count > 0) lines.Add(new Line { text = $"路灯 i({c.x},{c.y})：雷雨天按 L 召唤闪电 → " + string.Join("、", near), x = c.x, y = c.y });
        }
    }

    public static List<OverworldMap.Cell> DoorsNear(OverworldMap.Map m, OverworldMap.Cell c, int r)
    {
        var l = new List<OverworldMap.Cell>();
        for (int n = 1; n <= 9; n++) foreach (var d in OverworldMap.Find(m, (char)('0' + n))) if (Math.Abs(d.x - c.x) + Math.Abs(d.y - c.y) <= r) l.Add(d);
        return l;
    }

    /// <summary>最长连锁（深度优先，最多 12 个节点；平手取扫描顺序靠前的起点）。</summary>
    public static List<OverworldMap.Cell> LongestChain(OverworldMap.Map m)
    {
        var all = All(m); var edges = all.ToDictionary(c => c, c => Triggers(m, c));
        var best = new List<OverworldMap.Cell>();
        void Dfs(List<OverworldMap.Cell> path)
        {
            if (path.Count > best.Count) best = new List<OverworldMap.Cell>(path);
            foreach (var n in edges[path[path.Count - 1]]) if (!path.Contains(n)) { path.Add(n); Dfs(path); path.RemoveAt(path.Count - 1); }
        }
        foreach (var c in all) Dfs(new List<OverworldMap.Cell> { c });
        return best;
    }
}

/// <summary>
/// S218：每天的小镇天气 / 事件（输入随机：06:00 公布，整天不变；同一张图同一天永远一样 → 可复现、可在编辑器里预览）。
/// 第 1 天永远晴天（先学规则）。网页 owDayOf 逐行移植（用自己的整数哈希，不用各语言不同的 Random）。
/// </summary>
public static class OverworldEvents
{
    public enum Kind { Clear, Wind, Rain, Fog, Market, Storm, Acid }
    public struct Day { public int day; public Kind kind; public int wind; public uint h; }

    public static uint Hash(string s) { uint h = 2166136261u; foreach (char c in s ?? "") { h ^= c; h = unchecked(h * 16777619u); } return h; }

    public static Day Of(string mapName, int day) => Of(mapName, day, Base);

    /// <summary>S219：天气池按地图格局来——有路灯才会有雷雨（闪电要打在路灯上）；有山洞才会有酸雨（高草枯了，只剩山洞能躲）。</summary>
    public static Day Of(OverworldMap.Map m, int day) => Of(m.name, day, Pool(m));

    private static readonly Kind[] Base = { Kind.Clear, Kind.Wind, Kind.Rain, Kind.Fog, Kind.Market };
    public static Kind[] Pool(OverworldMap.Map m)
    {
        var l = new List<Kind>(Base);
        if (OverworldMap.Find(m, 'i').Count > 0 || m.storms.Count > 0) l.Add(Kind.Storm); // S220：画了雷区也会有雷雨天
        if (OverworldMap.Find(m, 'h').Count >= 2) l.Add(Kind.Acid);
        return l.ToArray();
    }

    public static Day Of(string mapName, int day, Kind[] pool)
    {
        uint h = Hash(mapName);
        h ^= unchecked((uint)day * 2654435761u);
        h ^= h << 13; h ^= h >> 17; h ^= h << 5;
        var d = new Day { day = day, h = h, kind = day <= 1 ? Kind.Clear : pool[(int)(h % (uint)pool.Length)], wind = (int)((h >> 8) % 4) };
        return d;
    }

    /// <summary>湿的天（泥石流会被冲击引发）。</summary>
    public static bool Wet(Day d) => d.kind == Kind.Rain || d.kind == Kind.Storm || d.kind == Kind.Acid;

    /// <summary>赶集日：门 n 晚 0 / 15 / 30 分钟；按时间顺序往后推，保证每扇门至少比上一扇晚 15 分钟、不超过 20:00。</summary>
    public static int MarketDelay(Day d, int door) => (int)((d.h >> (door * 3)) % 3) * 15;

    public static void ApplyTo(OverworldMap.Map m, Day d)
    {
        if (d.kind == Kind.Acid) { foreach (var c in OverworldMap.Find(m, '"')) OverworldMap.Set(m, c.x, c.y, '.'); return; } // 酸雨：高草枯了一天（只会"打开"，H1 不受影响）
        if (d.kind != Kind.Market) return;
        int prev = -9999;
        foreach (var door in m.doors.OrderBy2())
        {
            int t = door.minute + MarketDelay(d, door.n);
            t = Math.Max(t, prev + 15); t = Math.Min(t, OverworldMap.LatestDoor);
            door.minute = t; prev = t;
        }
    }

    public static string Zh(Day d)
    {
        switch (d.kind)
        {
            case Kind.Wind: return $"🌬 大风（往{OverworldProps.DirZh[d.wind]}吹）：巨炮落点被吹偏 {OverworldProps.WindShift} 格";
            case Kind.Rain: return $"🌧 雨天：水塔淹得更大（半径 {OverworldProps.FloodRadius + 1}），香蕉皮滑得更久，山丘被震会泥石流";
            case Kind.Fog: return "🌫 雾天：他只看得见平时 6 成远（你也更好躲）";
            case Kind.Market: return "🧺 赶集日：他每扇门晚 0–30 分钟出门（时间表已更新）";
            case Kind.Storm: return "⛈ 雷雨：雷区一阵阵劈闪电（地上闪光 = 快跑），在路灯旁按 L 召唤闪电，山丘被震会泥石流";
            case Kind.Acid: return "☂ 酸雨：高草全枯了（只剩山洞能躲），他打着伞只看得见 7 成远";
            default: return "☀ 晴天：一切照常";
        }
    }

    /// <summary>编辑器预览：第 from 天起 n 天的天气（赶集日附上每扇门的新时间）。</summary>
    public static List<string> Preview(OverworldMap.Map m, int from, int n)
    {
        var l = new List<string>();
        for (int day = from; day < from + n; day++)
        {
            var d = Of(m, day); string s = $"第 {day} 天 {Zh(d)}";
            if (d.kind == Kind.Market)
            {
                var c = OverworldMap.Parse(OverworldMap.ToText(m)); ApplyTo(c, d);
                s += "：" + string.Join(" ", c.doors.OrderBy2().Select(x => $"门{x.n} {OverworldMap.Clock(x.minute)}"));
            }
            l.Add(s);
        }
        return l;
    }
}
