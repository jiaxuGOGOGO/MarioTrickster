using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S213：小镇一帧的全部规则（纯逻辑，沙盒可测）——从 OverworldGame 里抽出来，游戏和"玩家视角模拟"（sim 里的机器人玩家）跑的是同一份代码。
/// OverworldGame 只负责：读键盘 → Input、画画面、切场景。
/// 读写 OverworldSession（跨场景的一天）；马里奥只通过 OverworldPercept 知道你（H4）。
/// </summary>
public sealed class OverworldTown
{
    public struct Input { public float h, v; public bool disguise, peel, taunt, door, fastForward; }

    public readonly OverworldMap.Map map;
    public readonly MarioMindTuningSO tuning;
    public readonly OverworldMind mind;
    public OverworldWalker mario;
    public double tx, ty;
    public bool disguised, lastMoved;
    public float frozen;
    public readonly List<OverworldMap.Door> stops = new List<OverworldMap.Door>();
    public readonly Dictionary<int, OverworldMap.Cell> doorCells = new Dictionary<int, OverworldMap.Cell>();
    public readonly List<OverworldMap.Cell> lamps;
    public readonly OverworldMap.Cell home, spawn;
    public bool marioInside;
    public float insideSeconds;
    public bool goingHome, dayOver;
    public OverworldOrder lastOrder;
    /// <summary>香蕉皮：格子编号 → (闪烁剩余, 生效剩余)。</summary>
    public readonly Dictionary<int, Vector2> peels = new Dictionary<int, Vector2>();
    public bool tauntedThisFrame;
    /// <summary>S213：刚从房间出来，他在门口喘口气/清点（看不见你），你有时间走开。</summary>
    public float exitGrace;
    /// <summary>S213：这一帧时间走得多快（快进时 &gt;1）。</summary>
    public float timeScale = 1f;

    // ── 这一帧发生的事（驱动层读完就清）──
    /// <summary>这一帧要给玩家的提示（驱动层翻成 Step1Text 文字；纯逻辑这边不碰文字，sim 才能跑）。</summary>
    public enum Note { None, TauntNone, Pickup, PeelNo, Peel, TooEarly, RoomMissing, Missed, LateHint, Caught, AmbushWait, Spotted }
    public Note hint; public float hintSeconds;
    public bool wantsEnter; public OverworldMap.Door enterDoor; public OverworldMind.DoorOutcome enterOutcome;

    private readonly System.Func<int, bool> roomReady;
    public const float PeelFlashSeconds = 0.5f, PeelActiveSeconds = 3f;
    public const int AmbushBonusBombs = 1, MaxBonusBombs = 3;
    /// <summary>快进倍率（按住空格）：只在"没事发生"时生效。</summary>
    public const float FastForwardScale = 4f;

    public OverworldTown(OverworldMap.Map m, MarioMindTuningSO t, System.Func<int, bool> roomReady = null)
    {
        map = m; tuning = t; this.roomReady = roomReady;
        home = OverworldMap.Find(map, 'M')[0];
        var ts = OverworldMap.Find(map, 'T'); spawn = ts.Count > 0 ? ts[0] : home;
        lamps = OverworldMap.Find(map, 'i');
        foreach (var d in map.doors.OrderBy2()) { var c = OverworldMap.Find(map, (char)('0' + d.n)); if (c.Count == 1) { stops.Add(d); doorCells[d.n] = c[0]; } }
        mind = new OverworldMind(t);
        if (OverworldSession.HasPositions)
        {
            mario = new OverworldWalker(OverworldSession.MarioX, OverworldSession.MarioY);
            tx = OverworldSession.TricksterX; ty = OverworldSession.TricksterY;
            // S213：刚从房间出来——两人都站在门口。以前他一出门就面朝你、1 秒内 '!!' 抓人（玩家视角模拟：反应慢 1 秒的人每次出门都被抓）。
            exitGrace = t.overworldExitGraceSeconds;
            mario.fx = 0; mario.fy = 1; // 面朝门（背对你）
        }
        else
        {
            mario = new OverworldWalker(home.x + 0.5, home.y + 0.5);
            tx = spawn.x + 0.5; ty = spawn.y + 0.5;
        }
        dayOver = OverworldSession.DayOver;
    }

    public OverworldMap.Door NextStop => OverworldSession.NextStop < stops.Count ? stops[OverworldSession.NextStop] : null;

    /// <summary>快进只在"没事"时允许：他平静、没在门里、你没被定身。任何起疑 / 他进门 → 立刻恢复正常速度。</summary>
    public bool CanFastForward => !dayOver && !marioInside && frozen <= 0f && exitGrace <= 0f
        && (mind.State == OverworldMarioState.Walking || mind.State == OverworldMarioState.Waiting) && mind.Meter.Level == SuspicionLevel.Calm
        && (MarioStepsToDoor < 0 || MarioStepsToDoor > tuning.overworldAmbushSteps + 6); // 他快进入埋伏范围 → 自动恢复正常速度，不会快进错过

    public void Tick(float dt, Input i)
    {
        hint = Note.None; wantsEnter = false;
        if (dayOver) return;
        timeScale = i.fastForward && CanFastForward ? FastForwardScale : 1f;
        float gdt = dt * timeScale; // 游戏时间（人和马里奥都按它走：快进 = 整个世界快放，公平）
        OverworldSession.Minute += gdt * tuning.overworldMinutesPerSecond;
        if (exitGrace > 0f) exitGrace -= dt;
        UpdateSteps(gdt);
        Trickster(gdt, i);
        if (wantsEnter) return;
        TickPeels(gdt);
        Mario(gdt);
        if (OverworldSession.Minute >= OverworldMap.DayEnd) EndDay();
    }

    private void Hint(Note n, float secs = 2f) { hint = n; hintSeconds = secs; }

    private void Trickster(float dt, Input i)
    {
        tauntedThisFrame = false;
        if (frozen > 0f) { frozen -= dt; lastMoved = false; return; }
        var dir = new Vector2(i.h, i.v); if (dir.magnitude > 1f) dir = dir / dir.magnitude;
        double sp = tuning.overworldTricksterSpeed * OverworldMap.SpeedFactor(Here(tx, ty)) * (disguised ? 0.6 : 1.0) * dt;
        var (nx, ny) = OverworldMap.Move(map, tx, ty, dir.x * sp, dir.y * sp);
        lastMoved = (nx - tx) * (nx - tx) + (ny - ty) * (ny - ty) > 1e-8;
        tx = nx; ty = ny;
        if (i.disguise) disguised = !disguised;
        if (i.peel) TryPeel();
        if (i.taunt)
        {
            if (OverworldSession.TauntsUsed >= tuning.overworldTaunts) Hint(Note.TauntNone);
            else { OverworldSession.TauntsUsed++; tauntedThisFrame = true; }
        }
        int cx = (int)System.Math.Floor(tx), cy = (int)System.Math.Floor(ty), id = cy * map.W + cx;
        if (map.At(cx, cy) == '?' && !OverworldSession.UsedCells.Contains(id))
        {
            OverworldSession.UsedCells.Add(id);
            OverworldSession.BonusBombs = Mathf.Min(MaxBonusBombs, OverworldSession.BonusBombs + 1);
            Hint(Note.Pickup);
        }
        if (i.door) TryDoor();
    }

    /// <summary>S213：他离下一扇门还有几格路（他还没出发 / 在门里 = -1）。每 0.2 秒算一次。</summary>
    public int MarioStepsToDoor { get; private set; } = -1;
    private float stepsAt;
    /// <summary>现在按 E 算埋伏吗：他已出发去这扇门、离门 ≤ overworldAmbushSteps 格。</summary>
    public bool AmbushReady => !marioInside && MarioStepsToDoor >= 0 && MarioStepsToDoor <= tuning.overworldAmbushSteps && !Spotted;
    /// <summary>他盯上你了（'!' 查看 / '!!' 追 / '?!' 找）：这时进门不算埋伏——先甩掉他。</summary>
    public bool Spotted => mind.State == OverworldMarioState.Investigating || mind.State == OverworldMarioState.Chasing || mind.State == OverworldMarioState.Searching;

    private void UpdateSteps(float dt)
    {
        stepsAt -= dt; if (stepsAt > 0f) return; stepsAt = 0.2f;
        var next = NextStop;
        if (next == null || marioInside || OverworldSession.Minute < next.minute || !doorCells.TryGetValue(next.n, out var dc)) { MarioStepsToDoor = -1; return; }
        MarioStepsToDoor = OverworldMap.Steps(OverworldMap.Path(map, OverworldGuide.Near(map, mario.x, mario.y), dc));
    }

    public bool NearDoor(int n, double reach = 1.3) => doorCells.TryGetValue(n, out var c) && Dist(c.x + 0.5, c.y + 0.5, tx, ty) <= reach;

    private void TryPeel()
    {
        int best = -1; double bd = tuning.overworldPrankRange * tuning.overworldPrankRange;
        foreach (var c in OverworldMap.Find(map, 'n'))
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id) || peels.ContainsKey(id)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bd) { bd = d; best = id; }
        }
        if (best < 0) { Hint(Note.PeelNo); return; }
        peels[best] = new Vector2(PeelFlashSeconds, PeelActiveSeconds);
        Hint(Note.Peel);
    }

    private void TickPeels(float dt)
    {
        var keys = new List<int>(peels.Keys);
        foreach (var k in keys)
        {
            var p = peels[k];
            if (p.x > 0f) p.x -= dt; else p.y -= dt;
            if (p.y <= 0f) { peels.Remove(k); OverworldSession.UsedCells.Add(k); }
            else peels[k] = p;
        }
    }

    private void TryDoor()
    {
        foreach (var kv in doorCells)
        {
            if (!NearDoor(kv.Key)) continue;
            var next = NextStop;
            if (next == null || next.n != kv.Key) { Hint(Note.TooEarly); return; }
            var outcome = OverworldMind.AtDoor(!marioInside, insideSeconds, tuning.overworldLateWindowSeconds);
            if (outcome == OverworldMind.DoorOutcome.Missed) return;
            // S213：埋伏 = 他快到了你已经守在门口。他还远 → 不进门，告诉你等（躲草丛 / P 伪装 / 空格快进）
            if (outcome == OverworldMind.DoorOutcome.Ambush && !AmbushReady) { Hint(Spotted ? Note.Spotted : Note.AmbushWait); return; }
            EnterRoom(next, outcome);
            return;
        }
    }

    private void EnterRoom(OverworldMap.Door d, OverworldMind.DoorOutcome outcome)
    {
        if (roomReady != null && !roomReady(d.n)) { Hint(Note.RoomMissing); return; }
        var c = doorCells[d.n];
        OverworldSession.PendingDoor = d.n;
        OverworldSession.PendingOutcome = outcome;
        if (outcome == OverworldMind.DoorOutcome.Ambush) OverworldSession.BonusBombs = Mathf.Min(MaxBonusBombs, OverworldSession.BonusBombs + AmbushBonusBombs);
        OverworldSession.CarriedSuspicion = OverworldMind.CarriedSuspicion(mind.State == OverworldMarioState.InRoom ? OverworldMarioState.Walking : mind.State, mind.Meter.Value, tuning.curiousThreshold);
        OverworldSession.Minute = System.Math.Max(OverworldSession.Minute, d.minute) + tuning.overworldVisitMinutes;
        OverworldSession.DelayedSeconds += mind.DelayedSeconds;
        OverworldSession.NextStop++;
        var below = OverworldMap.Walkable(map, c.x, c.y - 1) ? new OverworldMap.Cell(c.x, c.y - 1) : c;
        OverworldSession.MarioX = c.x + 0.5; OverworldSession.MarioY = c.y + 0.5;
        OverworldSession.TricksterX = below.x + 0.5; OverworldSession.TricksterY = below.y + 0.5;
        OverworldSession.HasPositions = true;
        wantsEnter = true; enterDoor = d; enterOutcome = outcome;
    }

    private void Mario(float dt)
    {
        var next = NextStop;
        // S213：最后一扇门有了结果 → 这一天直接结算（玩家模拟：以前还要干等他走回家 ~20 秒，期间路过你还会抓你，毫无意义）
        if (next == null && !marioInside) { goingHome = true; EndDay(); return; }
        if (marioInside)
        {
            insideSeconds += dt;
            if (next != null && insideSeconds > tuning.overworldLateWindowSeconds)
            {
                OverworldSession.RecordMissed(next.n);
                OverworldSession.Minute += tuning.overworldVisitMinutes;
                OverworldSession.NextStop++;
                marioInside = false; insideSeconds = 0f;
                mind.SetInRoom(false);
                Hint(Note.Missed, 3f);
            }
            return;
        }

        Vector2? schedule = null; OverworldMap.Cell goalCell = home;
        if (next != null) { if (OverworldSession.Minute >= next.minute) { goalCell = doorCells[next.n]; schedule = Center(goalCell); } }
        else { goingHome = true; goalCell = home; schedule = Center(home); }

        var r = Sight();
        bool blind = exitGrace > 0f; // 出门缓冲：他在清点战利品，不看不听（头上有 '…' 标记，H6）
        bool sees = !blind && OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r);
        var p = new OverworldPercept
        {
            marioPos = new Vector2((float)mario.x, (float)mario.y),
            seesFigure = sees,
            figurePos = new Vector2((float)tx, (float)ty),
            figureLooksLikeProp = disguised,
            figureMoving = lastMoved,
            sawRustle = !blind && !sees && lastMoved && OverworldMap.SeesRustle(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r),
            rustlePos = new Vector2((float)tx, (float)ty),
            heardTaunt = !blind && tauntedThisFrame && Dist(mario.x, mario.y, tx, ty) <= tuning.overworldVisionRange * 1.5f,
            tauntPos = new Vector2((float)tx, (float)ty),
            scheduleTarget = blind ? (Vector2?)null : schedule,
        };
        int mid = (int)System.Math.Floor(mario.y) * map.W + (int)System.Math.Floor(mario.x);
        if (peels.TryGetValue(mid, out var peel) && peel.x <= 0f) { p.slipped = true; peels.Remove(mid); OverworldSession.UsedCells.Add(mid); }

        var o = mind.Tick(dt, p);
        if (blind && string.IsNullOrEmpty(o.mark)) o.mark = "…";
        lastOrder = o;
        if (o.target.HasValue)
        {
            var tc = new OverworldMap.Cell(Mathf.FloorToInt(o.target.Value.x), Mathf.FloorToInt(o.target.Value.y));
            if (!OverworldMap.Walkable(map, tc.x, tc.y) && !OverworldCatalog.IsDoor(map.At(tc.x, tc.y))) tc = goalCell;
            mario.SetGoal(map, tc);
            float speed = o.state == OverworldMarioState.Chasing ? tuning.overworldChaseSpeed : tuning.overworldMarioSpeed;
            mario.Step(map, speed, dt);
        }
        if (o.tryCatch) Caught();

        if (o.state == OverworldMarioState.Walking && mario.Arrived && schedule.HasValue && mario.Goal.Equals(goalCell))
        {
            if (next != null) { marioInside = true; insideSeconds = 0f; mind.SetInRoom(true); Hint(Note.LateHint, tuning.overworldLateWindowSeconds); }
            else EndDay();
        }
    }

    private void Caught()
    {
        OverworldSession.Caught++;
        tx = spawn.x + 0.5; ty = spawn.y + 0.5; disguised = false;
        frozen = tuning.overworldCaughtPenaltySeconds;
        mind.OnCaught();
        Hint(Note.Caught, 2.5f);
    }

    public void EndDay()
    {
        if (dayOver) return;
        dayOver = true; OverworldSession.DayOver = true;
        OverworldSession.DelayedSeconds += mind.DelayedSeconds;
        if (!goingHome) mind.SetHome();
    }

    public OverworldMap.SightRules Sight() => new OverworldMap.SightRules
    {
        range = tuning.overworldVisionRange, nightRange = tuning.overworldNightVisionRange, halfAngleDeg = tuning.overworldVisionHalfAngle,
        nearRadius = tuning.overworldNearSense, grassRadius = tuning.overworldGrassSeeRadius, lampRadius = tuning.overworldLampRadius,
        night = OverworldSession.Minute >= OverworldMap.NightStart,
    };

    public OverworldMap.Rules Rules => new OverworldMap.Rules
    {
        marioSpeed = tuning.overworldMarioSpeed, tricksterSpeed = tuning.overworldTricksterSpeed,
        minutesPerSecond = tuning.overworldMinutesPerSecond, visitMinutes = tuning.overworldVisitMinutes,
    };

    private char Here(double x, double y) => map.At((int)System.Math.Floor(x), (int)System.Math.Floor(y));
    private static Vector2 Center(OverworldMap.Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
    public static float Dist(double ax, double ay, double bx, double by) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
