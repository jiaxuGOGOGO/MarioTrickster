using System;
using System.Collections.Generic;

/// <summary>
/// S210：大地图上"按路线走"的纯逻辑（游戏里的马里奥和沙盒体检用同一套）。
/// 吸取 S209 教训：检查"理论上走得到"不够，要按他真实的身体（半径 0.3）、速度和地面减速一步步走一遍。
/// </summary>
public sealed class OverworldWalker
{
    public double x, y;
    public double fx = 0, fy = -1; // 面朝方向（最后一次移动方向，单位向量；初始朝下 = 面向玩家）
    private List<OverworldMap.Cell> path;
    private int index;
    private OverworldMap.Cell goal = new OverworldMap.Cell(-999, -999);
    public bool Arrived { get; private set; } = true;
    public OverworldMap.Cell Goal => goal;

    public OverworldWalker(double x, double y) { this.x = x; this.y = y; }

    public OverworldMap.Cell CellHere => new OverworldMap.Cell((int)Math.Floor(x), (int)Math.Floor(y));

    /// <summary>设定目标格：目标没变不重新寻路。返回 false = 走不到。</summary>
    public bool SetGoal(OverworldMap.Map m, OverworldMap.Cell g)
    {
        if (g.Equals(goal) && path != null) return true;
        var p = OverworldMap.Path(m, CellHere, g);
        goal = g;
        if (p == null) { path = null; Arrived = true; return false; }
        path = p; index = p.Count > 1 ? 1 : 0; Arrived = false;
        return true;
    }

    public void Clear() { path = null; Arrived = true; goal = new OverworldMap.Cell(-999, -999); }

    /// <summary>走 dt 秒（速度 = 格/秒 × 脚下地面倍率）。返回这一帧实际移动的距离。</summary>
    public double Step(OverworldMap.Map m, double speed, double dt)
    {
        if (path == null || Arrived) return 0;
        double budget = speed * OverworldMap.SpeedFactor(m.At((int)Math.Floor(x), (int)Math.Floor(y))) * dt, moved = 0;
        int guard = 0;
        while (budget > 1e-9 && index < path.Count && guard++ < 8)
        {
            double tx = path[index].x + 0.5, ty = path[index].y + 0.5, dx = tx - x, dy = ty - y, d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-6) { index++; continue; }
            double s = Math.Min(budget, d);
            var (nx, ny) = OverworldMap.Move(m, x, y, dx / d * s, dy / d * s);
            double real = Math.Sqrt((nx - x) * (nx - x) + (ny - y) * (ny - y));
            fx = dx / d; fy = dy / d;
            x = nx; y = ny; moved += real; budget -= s;
            if (real < s * 0.5) break; // 被挡住（不应该发生：路线走格子中心）
            if (s >= d - 1e-9) index++;
        }
        if (index >= path.Count) Arrived = true;
        return moved;
    }

    /// <summary>
    /// 沙盒体检：没人捣乱时按日程走完一天（和游戏里同一个 Walker、同样的速度和时钟）。
    /// 返回 (ok, 说明)。ok = 每扇门都走到、按时回家、没有哪一段卡住超过 3 秒。
    /// </summary>
    public static (bool ok, string summary, double homeMinute) SimulateDay(OverworldMap.Map m, OverworldMap.Rules r)
    {
        var home = OverworldMap.Find(m, 'M');
        if (home.Count != 1) return (false, "没有马里奥的家", 0);
        var w = new OverworldWalker(home[0].x + 0.5, home[0].y + 0.5);
        double minute = OverworldMap.DayStart, dt = 1.0 / 30;
        var stops = new List<(OverworldMap.Cell cell, int minute)>();
        foreach (var d in m.doors.OrderBy2())
        {
            var c = OverworldMap.Find(m, (char)('0' + d.n));
            if (c.Count == 1) stops.Add((c[0], d.minute));
        }
        var log = new List<string>();
        foreach (var (cell, at) in stops)
        {
            if (minute < at) minute = at;
            if (!w.SetGoal(m, cell)) return (false, $"走不到门 {cell}", minute);
            double stuck = 0, t = 0;
            while (!w.Arrived)
            {
                double mv = w.Step(m, r.marioSpeed, dt); t += dt; minute += dt * r.minutesPerSecond;
                stuck = mv < 1e-4 ? stuck + dt : 0;
                if (stuck > 3 || t > 600) return (false, $"去门 {cell} 的路上卡在 ({w.x:0.0},{w.y:0.0})", minute);
            }
            log.Add($"{OverworldMap.Clock(minute)} 到 {cell}");
            minute += r.visitMinutes;
        }
        if (!w.SetGoal(m, home[0])) return (false, "回不了家", minute);
        double s2 = 0, t2 = 0;
        while (!w.Arrived)
        {
            double mv = w.Step(m, r.marioSpeed, dt); t2 += dt; minute += dt * r.minutesPerSecond;
            s2 = mv < 1e-4 ? s2 + dt : 0;
            if (s2 > 3 || t2 > 600) return (false, $"回家路上卡在 ({w.x:0.0},{w.y:0.0})", minute);
        }
        bool ok = minute <= OverworldMap.DayEnd;
        return (ok, string.Join("；", log) + $"；{OverworldMap.Clock(minute)} 到家" + (ok ? "" : "（超过 22:00）"), minute);
    }
}

static class OverworldDoorOrder
{
    public static IEnumerable<OverworldMap.Door> OrderBy2(this List<OverworldMap.Door> l)
    {
        var c = new List<OverworldMap.Door>(l);
        c.Sort((a, b) => a.minute != b.minute ? a.minute.CompareTo(b.minute) : a.n.CompareTo(b.n));
        return c;
    }
}
