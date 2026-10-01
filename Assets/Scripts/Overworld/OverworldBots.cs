using System.Collections.Generic;

/// <summary>
/// S213：玩家视角模拟——几种"像真人"的机器人玩家，用和游戏同一份 OverworldTown 规则玩完一天，找出"玩起来不对"的地方。
/// 机器人只用玩家看得见的东西（地图、门时间表、屏幕上马里奥的位置和头顶标记、箭头 / 赛跑提示），不偷看 AI 内部。
/// 房间里的战斗不在这里模拟：进门即记一个"房间结果"占位（守住），只看大地图这一层顺不顺、会不会让人卡住 / 受挫。
/// </summary>
public static class OverworldBots
{
    public enum Kind
    {
        /// <summary>跟着箭头走：每扇门都跟着指引抄近路去门口，站着狂按 E（不躲——大多数新手）。</summary>
        Follower,
        /// <summary>会躲：到门口按 P 伪装成木箱不动，等他走近再按 E（学会了规则的玩家）。</summary>
        Hider,
        /// <summary>反应慢：出门后愣 1.5 秒才动，走路偶尔停顿。</summary>
        Slow,
        /// <summary>贪心：先去捡所有道具箱再去门口。</summary>
        Greedy,
        /// <summary>捣蛋：躲草里用香蕉皮 / 挑衅拖他，再去门口。</summary>
        Prankster,
        /// <summary>挂机：什么都不按（H10：一天也要能自己结束）。</summary>
        Idle,
        /// <summary>乱按：随机方向、随机按 P/L/T/E（找卡死、连续被抓、一天不结束这类问题）。</summary>
        Chaos,
    }

    public sealed class Report
    {
        public Kind kind;
        public int ambush, late, missed, caught, doors, maxCaughtStreak;
        public double longestFrozenGap = double.MaxValue;
        public double realSeconds, idleSeconds, longestIdle, fastForwardSeconds;
        public bool dayEnded;
        public readonly List<string> log = new List<string>();
        public override string ToString() => $"{kind}: 埋伏 {ambush} 迟到 {late} 没赶上 {missed} / {doors} 门，被抓 {caught}，用时 {realSeconds:0} 秒（干等 {idleSeconds:0} 秒，最长一次 {longestIdle:0} 秒，快进 {fastForwardSeconds:0} 秒）{(dayEnded ? "" : " ✗一天没结束")}";
    }

    /// <summary>玩完一天。useFastForward = 机器人在"没事可干"时按住快进（S213 新功能）。房间里的时间不计入。</summary>
    public static Report PlayDay(OverworldMap.Map m, MarioMindTuningSO t, Kind kind, bool useFastForward, int seed = 1, int day = 1)
    {
        OverworldSession.NewDay(m.name, "Town", day); OverworldSession.Active = true; // S218：day 决定天气
        var rep = new Report { kind = kind, doors = 0 };
        var town = new OverworldTown(m, t);
        rep.doors = town.stops.Count;
        const float dt = 1f / 30f;
        var rng = new System.Random(seed);
        double idleRun = 0; float react = 0f, chaosH = 0f, chaosV = 0f; int pickupsLeft = OverworldMap.Find(m, '?').Count;
        int guard = 0; OverworldMap.Cell? energyGoal = null;
        while (!OverworldSession.DayOver && guard++ < 30 * 60 * 30)
        {
            var next = town.NextStop;
            var input = new OverworldTown.Input();
            OverworldMap.Cell? goal = null;
            if (kind != Kind.Idle && next != null && town.doorCells.TryGetValue(next.n, out var dc))
            {
                goal = dc;
                if (kind == Kind.Greedy && pickupsLeft > 0)
                {
                    var boxes = OverworldMap.Find(m, '?'); OverworldMap.Cell? best = null; double bd = 1e9;
                    foreach (var b in boxes) { if (OverworldSession.UsedCells.Contains(b.y * m.W + b.x)) continue; double d = OverworldTown.Dist(b.x + .5, b.y + .5, town.tx, town.ty); if (d < bd) { bd = d; best = b; } }
                    if (best.HasValue) goal = best; else pickupsLeft = 0;
                }
                // S221：捣蛋型有空就去捡能量 *（真人会这样攒 Q）：来得及"去捡 + 再赶到门口"（留 30% 余量）才去
                if (kind == Kind.Prankster && OverworldSession.Energy < OverworldSession.MaxEnergy && energyGoal == null)
                {
                    double spare = (next.minute - OverworldSession.Minute) / t.overworldMinutesPerSecond * t.overworldTricksterSpeed * 0.95;
                    var here = OverworldGuide.Near(town.map, town.tx, town.ty); int bestLen = int.MaxValue;
                    foreach (var e in OverworldMap.Find(m, '*'))
                    {
                        if (OverworldSession.UsedCells.Contains(e.y * m.W + e.x)) continue;
                        var a1 = OverworldMap.Path(town.map, here, e); var a2 = a1 == null ? null : OverworldMap.Path(town.map, e, dc);
                        if (a1 == null || a2 == null) continue; int len = a1.Count + a2.Count;
                        if (len < spare && len < bestLen) { bestLen = len; energyGoal = e; }
                    }
                }
                if (energyGoal.HasValue)
                {
                    var e = energyGoal.Value;
                    if (OverworldSession.UsedCells.Contains(e.y * m.W + e.x) || OverworldSession.Energy >= OverworldSession.MaxEnergy) energyGoal = null; else goal = e;
                }
                // 捣蛋：他在路上且离门还远 → 先去他路线上最近的香蕉皮按 L
                if (kind == Kind.Prankster && !town.marioInside && OverworldSession.Minute >= next.minute)
                {
                    foreach (var pc in OverworldMap.Find(m, 'n'))
                    {
                        int id = pc.y * m.W + pc.x; if (OverworldSession.UsedCells.Contains(id) || town.peels.ContainsKey(id)) continue;
                        if (OverworldTown.Dist(pc.x + .5, pc.y + .5, town.mario.x, town.mario.y) < 6 && OverworldTown.Dist(pc.x + .5, pc.y + .5, town.tx, town.ty) < 2.5) { input.peel = true; break; }
                    }
                    // S218：路过大机关、他在 8 格内 → 按 L（真人会这样乱试）
                    foreach (var bc in OverworldProps.All(town.map))
                    {
                        if (OverworldSession.UsedCells.Contains(bc.y * town.map.W + bc.x)) continue;
                        if (OverworldTown.Dist(bc.x + .5, bc.y + .5, town.tx, town.ty) < 2.5 && OverworldTown.Dist(bc.x + .5, bc.y + .5, town.mario.x, town.mario.y) < 8) { input.peel = true; break; }
                    }
                }
            }
            // S220：能量满 → 捣蛋型在他走近（4 格内）时按 Q；乱按型随便按；会躲的从不按
            if (kind == Kind.Prankster && OverworldSession.Energy >= OverworldSession.MaxEnergy && !town.marioInside && OverworldTown.Dist(town.mario.x, town.mario.y, town.tx, town.ty) < 4) input.weather = true;
            if (kind == Kind.Chaos)
            {
                input.weather = rng.NextDouble() < 0.01;
                if (rng.NextDouble() < 0.05) { chaosH = (float)(rng.NextDouble() * 2 - 1); chaosV = (float)(rng.NextDouble() * 2 - 1); }
                input.h = chaosH; input.v = chaosV; goal = null;
                input.disguise = rng.NextDouble() < 0.01; input.peel = rng.NextDouble() < 0.02; input.taunt = rng.NextDouble() < 0.005; input.door = rng.NextDouble() < 0.05;
            }
            // S221：捣蛋型召唤雷云后先跑出圈（离云心 > 半径 + 1）再回去干活
            if (kind == Kind.Prankster && town.cloud != null && OverworldTown.Dist(town.tx, town.ty, town.cloud.x, town.cloud.y) <= t.overworldCloudRadius + 1.2)
            {
                OverworldMap.Cell? best = null; double bd = 1e9;
                var here = OverworldGuide.Near(town.map, town.tx, town.ty);
                for (int y = here.y - 6; y <= here.y + 6; y++) for (int x = here.x - 6; x <= here.x + 6; x++)
                {
                    if (!OverworldMap.Walkable(town.map, x, y) || OverworldTown.Dist(x + .5, y + .5, town.cloud.x, town.cloud.y) <= t.overworldCloudRadius + 1.5) continue;
                    double d = OverworldTown.Dist(x + .5, y + .5, town.tx, town.ty); if (d < bd) { bd = d; best = new OverworldMap.Cell(x, y); }
                }
                if (best.HasValue) goal = best;
            }
            if (react > 0f) { react -= dt; goal = null; }
            if (goal.HasValue)
            {
                var path = OverworldMap.Path(town.map, OverworldGuide.Near(town.map, town.tx, town.ty), goal.Value); // S218：走今天的地形（撞碎 / 淹过的）
                if (path != null && path.Count > 1)
                {
                    var c = path[1]; double dx = c.x + 0.5 - town.tx, dy = c.y + 0.5 - town.ty, d = System.Math.Sqrt(dx * dx + dy * dy);
                    if (d > 1e-3) { input.h = (float)(dx / d); input.v = (float)(dy / d); }
                }
                else if (path != null && path.Count == 1)
                {
                    double dx = goal.Value.x + 0.5 - town.tx, dy = goal.Value.y + 0.5 - town.ty, d = System.Math.Sqrt(dx * dx + dy * dy);
                    if (d > 0.3) { input.h = (float)(dx / d); input.v = (float)(dy / d); }
                }
                if (kind == Kind.Slow && rng.NextDouble() < 0.01) react = 0.6f;
            }
            // 到门口：真人会等到"他快来了"或"他进去了"再按 E（先到就按 = 埋伏）
            if (kind != Kind.Idle && kind != Kind.Chaos && next != null && town.NearDoor(next.n))
            {
                input.h = input.v = 0f; // 到门口就站住
                bool hides = kind == Kind.Hider || kind == Kind.Prankster || kind == Kind.Slow;
                if (hides && !town.disguised && !town.marioInside) input.disguise = true;
                input.door = true; // 真人会一直按 E（没到时机只会看到"等他走近"）
            }
            bool atDoor = next != null && town.NearDoor(next.n);
            bool idle = kind != Kind.Chaos && input.h == 0 && input.v == 0 && !input.peel && (!input.door || !town.AmbushReady);
            // 快进：没事可干（站在门口等 / 挂机）时按住
            input.fastForward = useFastForward && (idle || atDoor);
            int prevStop = OverworldSession.NextStop; int prevCaught = OverworldSession.Caught;
            town.Tick(dt, input);
            float stepReal = dt;
            rep.realSeconds += stepReal;
            if (town.timeScale > 1f) rep.fastForwardSeconds += stepReal;
            bool reallyIdle = idle && town.timeScale <= 1f && !town.marioInside;
            if (reallyIdle) { idleRun += stepReal; rep.idleSeconds += stepReal; if (idleRun > rep.longestIdle) rep.longestIdle = idleRun; } else idleRun = 0;
            if (OverworldSession.Caught > prevCaught) { rep.caught++; rep.log.Add($"{OverworldMap.Clock(OverworldSession.Minute)} 被抓"); }
            if (town.hint == OverworldTown.Note.Missed) { rep.missed++; rep.log.Add($"{OverworldMap.Clock(OverworldSession.Minute)} 没赶上门 {town.stops[prevStop].n}"); }
            if (town.wantsEnter)
            {
                if (town.enterOutcome == OverworldMind.DoorOutcome.Ambush) rep.ambush++; else rep.late++;
                rep.log.Add($"{OverworldMap.Clock(OverworldSession.Minute - t.overworldVisitMinutes)} 进门 {town.enterDoor.n}（{town.enterOutcome}）");
                OverworldSession.RecordRoom(town.enterDoor.n, true);
                if (town.disguised) { } // 新场景里默认不伪装
                town = new OverworldTown(m, t); // 回到小镇（和真游戏一样：新场景、从门口开始）
                if (kind == Kind.Slow) react = 1.5f;
                idleRun = 0;
            }
        }
        rep.dayEnded = OverworldSession.DayOver;
        return rep;
    }
}
