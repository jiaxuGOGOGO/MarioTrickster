using System;
using System.Collections.Generic;

/// <summary>
/// S212：地图指引（纯逻辑，沙盒可测；网页 overworld.js 的 owMarioAt / owRace 是同一套）。
/// 调研（r/gamedesign "告诉玩家往哪走的方法"、r/Games 面包屑讨论、GitHub off-screen-indicator）：
///   ① 屏幕边缘箭头指向目标（目标在屏幕里就不画，不挡视线）；
///   ② 赛跑提示比"还剩几分钟"更有用——直接告诉你"你 12 秒 / 他 35 秒 → 来得及"；
///   ③ 面包屑按住才出现（Tab），平时不糊满屏幕；小地图随时按 M 看全局。
/// 全部只用"你自己知道的信息"（门的时间表是公开的、马里奥的位置你看得见），不影响 AI（H4 只约束马里奥侧）。
/// </summary>
public static class OverworldGuide
{
    // ── 屏幕边缘箭头 ─────────────────────────────────
    public struct Arrow { public bool onScreen; public float x, y, angleDeg; }

    /// <summary>
    /// 目标的视口坐标（0..1，左下 0,0；behind = 在镜头后面）→ 箭头位置（同样 0..1）与朝向（度，0 = 向右，逆时针）。
    /// 目标在 [margin, 1-margin] 里 = 在屏幕上，不画箭头。否则从屏幕中心沿方向射到边框内侧。
    /// </summary>
    public static Arrow EdgeArrow(float vx, float vy, float margin = 0.06f, bool behind = false)
    {
        float dx = vx - 0.5f, dy = vy - 0.5f;
        if (behind) { dx = -dx; dy = -dy; }
        var a = new Arrow { angleDeg = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI) };
        if (!behind && vx >= margin && vx <= 1f - margin && vy >= margin && vy <= 1f - margin) { a.onScreen = true; a.x = vx; a.y = vy; return a; }
        float half = 0.5f - margin;
        float sx = Math.Abs(dx) < 1e-6f ? float.MaxValue : half / Math.Abs(dx);
        float sy = Math.Abs(dy) < 1e-6f ? float.MaxValue : half / Math.Abs(dy);
        float s = Math.Min(sx, sy);
        if (s == float.MaxValue) s = 0f;
        a.x = 0.5f + dx * s; a.y = 0.5f + dy * s;
        return a;
    }

    // ── 赛跑：你和他谁先到下一扇门 ─────────────────────
    public enum Verdict { Ahead, Tight, Behind, Inside, None }
    public struct Race { public double you, him; public Verdict verdict; public double margin; }

    /// <summary>
    /// 你从 (tx,ty) 全速赶到门要几秒；他要几秒（还没到点 = 先等到点再出发）。现实秒。
    /// margin = him - you；≥ tightSeconds 来得及，0..tight 很紧，&lt;0 来不及。他已进门 → Inside。
    /// </summary>
    public static Race RaceTo(OverworldMap.Map m, OverworldMap.Rules r, OverworldMap.Cell door, int doorMinute, double minuteNow,
        double tx, double ty, double mx, double my, bool marioInside, double tightSeconds = 4)
    {
        var res = new Race { verdict = Verdict.None };
        if (marioInside) { res.verdict = Verdict.Inside; return res; }
        var you = OverworldMap.Path(m, Near(m, tx, ty), door);
        var him = OverworldMap.Path(m, Near(m, mx, my), door);
        if (you == null || him == null) return res;
        res.you = OverworldMap.WalkSeconds(m, you, r.tricksterSpeed);
        double wait = Math.Max(0, (doorMinute - minuteNow) / Math.Max(0.01, r.minutesPerSecond));
        res.him = wait + OverworldMap.WalkSeconds(m, him, r.marioSpeed);
        res.margin = res.him - res.you;
        res.verdict = res.margin >= tightSeconds ? Verdict.Ahead : res.margin >= 0 ? Verdict.Tight : Verdict.Behind;
        return res;
    }

    /// <summary>站的位置所在格；万一贴墙算到了挡路格，取最近的能走的邻格。</summary>
    public static OverworldMap.Cell Near(OverworldMap.Map m, double x, double y)
    {
        var c = new OverworldMap.Cell((int)Math.Floor(x), (int)Math.Floor(y));
        if (OverworldMap.Walkable(m, c.x, c.y)) return c;
        OverworldMap.Cell best = c; double bd = double.MaxValue;
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
        {
            int nx = c.x + dx, ny = c.y + dy; if (!OverworldMap.Walkable(m, nx, ny)) continue;
            double d = (nx + 0.5 - x) * (nx + 0.5 - x) + (ny + 0.5 - y) * (ny + 0.5 - y);
            if (d < bd) { bd = d; best = new OverworldMap.Cell(nx, ny); }
        }
        return best;
    }

    // ── 时间轴：某一分钟马里奥（无人捣乱时）在哪 ─────────
    public struct Where { public OverworldMap.Cell cell; public int insideDoor; public string what; }

    /// <summary>
    /// 小镇工坊 / 网页的"时间滑条"：拖到 13:00 → 他在哪一格、在做什么。和 DaySchedule 同一套日程（出发—到—出来—回家）。
    /// 走路中的位置按每格的真实走路时间（泥地慢）落在路线上。
    /// </summary>
    public static Where MarioAt(OverworldMap.Map m, OverworldMap.Schedule sc, double minute, double marioSpeed, double minutesPerSecond)
    {
        var home = OverworldMap.Find(m, 'M');
        var w = new Where { cell = home.Count > 0 ? home[0] : new OverworldMap.Cell(0, 0), what = "在家" };
        if (sc == null) return w;
        OverworldMap.Cell at = w.cell; string atWhat = "在家";
        foreach (var s in sc.stops)
        {
            if (minute < s.depart) { w.cell = at; w.what = atWhat == "在家" ? $"在家（{OverworldMap.Clock(s.depart)} 出发去门 {s.door.n}）" : atWhat; return w; }
            if (minute < s.arrive) { w.cell = Along(m, s.path, (minute - s.depart) / minutesPerSecond, marioSpeed); w.what = $"走向门 {s.door.n}（{OverworldMap.Clock(s.arrive)} 到）"; return w; }
            if (minute < s.leave) { w.cell = s.cell; w.insideDoor = s.door.n; w.what = $"在门 {s.door.n} 里面偷东西（{OverworldMap.Clock(s.leave)} 出来）"; return w; }
            at = s.cell; atWhat = $"刚从门 {s.door.n} 出来";
        }
        double homeDepart = sc.stops.Count > 0 ? sc.stops[sc.stops.Count - 1].leave : OverworldMap.DayStart;
        if (sc.homePath != null && minute < sc.homeArrive && minute >= homeDepart)
        { w.cell = Along(m, sc.homePath, (minute - homeDepart) / minutesPerSecond, marioSpeed); w.what = $"回家路上（{OverworldMap.Clock(sc.homeArrive)} 到家）"; return w; }
        w.cell = home.Count > 0 ? home[0] : at; w.what = "到家了";
        return w;
    }

    /// <summary>沿路线走了 seconds 秒停在哪一格（每格的时间 = 1 / (速度 × 地面倍率)，与 WalkSeconds 一致）。</summary>
    public static OverworldMap.Cell Along(OverworldMap.Map m, List<OverworldMap.Cell> p, double seconds, double speed)
    {
        if (p == null || p.Count == 0) return new OverworldMap.Cell(0, 0);
        double t = 0;
        for (int i = 1; i < p.Count; i++)
        {
            t += 1.0 / (speed * OverworldMap.SpeedFactor(m.At(p[i].x, p[i].y)));
            if (t > seconds + 1e-9) return p[i - 1];
        }
        return p[p.Count - 1];
    }
}
