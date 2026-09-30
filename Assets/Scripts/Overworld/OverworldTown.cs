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
    public enum Note { None, TauntNone, Pickup, PeelNo, Peel, TooEarly, RoomMissing, Missed, LateHint, Caught, AmbushWait, Spotted, BigArmed, BigHit, BigChain, BigReloaded, BigSelf, BigStuck }
    public Note hint; public float hintSeconds;
    public bool wantsEnter; public OverworldMap.Door enterDoor; public OverworldMind.DoorOutcome enterOutcome;

    private readonly System.Func<int, bool> roomReady;
    public const float PeelFlashSeconds = 0.5f, PeelActiveSeconds = 3f;
    public const int AmbushBonusBombs = 1, MaxBonusBombs = 3;
    /// <summary>快进倍率（按住空格）：只在"没事发生"时生效。</summary>
    public const float FastForwardScale = 4f;

    // ── S218：小镇大机关（巨炮 K / 滚石 O / 水塔 U）+ 天气 ──
    public sealed class Big
    {
        public OverworldMap.Cell c; public char kind; public float fuse, fuseTotal; public int dir = -1, depth = 1;
        public bool rolling; public double rx, ry; public List<OverworldMap.Cell> lane; public double rolled; public bool hitMario, hitYou;
    }
    public sealed class Flight { public double fx, fy, tx, ty; public float t, dur; public int depth; public bool mario, you, shell; public double X => fx + (tx - fx) * System.Math.Min(1f, t / dur); public double Y => fy + (ty - fy) * System.Math.Min(1f, t / dur); public float Arc => 4f * (t / dur) * (1f - t / dur); }
    public readonly List<Big> active = new List<Big>();
    public readonly List<Flight> flights = new List<Flight>();
    /// <summary>这一帧被改掉的格子（画面层据此换贴图）/ 冲击点（画面层放冲击环 + 震屏）。</summary>
    public readonly List<int> changedCells = new List<int>();
    public struct Impact3 { public float x, y, z; public Impact3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public readonly List<Impact3> impacts = new List<Impact3>();
    public readonly OverworldEvents.Day weather;
    public const float FlightSeconds = 0.9f;
    private float pendingMarioStun;
    private Vector2? heardNoise;
    public bool marioFlying => flights.Exists(f => f.mario);
    public bool youFlying => flights.Exists(f => f.you);
    public bool BigBusy => active.Count > 0 || flights.Count > 0;
    public float PeelActive => weather.kind == OverworldEvents.Kind.Rain ? PeelActiveSeconds * 1.5f : PeelActiveSeconds;

    public OverworldTown(OverworldMap.Map m, MarioMindTuningSO t, System.Func<int, bool> roomReady = null)
    {
        // S218：自己留一份（大机关会改地形；赶集日会改门的时间）——调用方的地图不变，重建小镇也不会越改越多
        m = OverworldMap.Parse(OverworldMap.ToText(m));
        weather = OverworldEvents.Of(m.name, OverworldSession.Day);
        OverworldEvents.ApplyTo(m, weather);
        foreach (var kv in OverworldSession.Changed) OverworldMap.Set(m, kv.Key % m.W, kv.Key / m.W, kv.Value);
        map = m; tuning = t; this.roomReady = roomReady;
        home = OverworldMap.Find(map, 'M')[0];
        var ts = OverworldMap.Find(map, 'T'); spawn = ts.Count > 0 ? ts[0] : home;
        lamps = OverworldMap.Find(map, 'i');
        foreach (var d in map.doors.OrderBy2()) { var c = OverworldMap.Find(map, (char)('0' + d.n)); if (c.Count == 1) { stops.Add(d); doorCells[d.n] = c[0]; } }
        mind = new OverworldMind(t);
        Reload();
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
        && !BigBusy
        && (MarioStepsToDoor < 0 || MarioStepsToDoor > tuning.overworldAmbushSteps + 6); // 他快进入埋伏范围 → 自动恢复正常速度，不会快进错过

    public void Tick(float dt, Input i)
    {
        hint = Note.None; wantsEnter = false; changedCells.Clear(); impacts.Clear();
        if (dayOver) return;
        timeScale = i.fastForward && CanFastForward ? FastForwardScale : 1f;
        float gdt = dt * timeScale; // 游戏时间（人和马里奥都按它走：快进 = 整个世界快放，公平）
        OverworldSession.Minute += gdt * tuning.overworldMinutesPerSecond;
        if (exitGrace > 0f) exitGrace -= dt;
        UpdateSteps(gdt);
        Trickster(gdt, i);
        if (wantsEnter) return;
        TickPeels(gdt);
        TickBigs(gdt);
        Mario(gdt);
        if (OverworldSession.Minute >= OverworldMap.DayEnd) EndDay();
    }

    private void Hint(Note n, float secs = 2f) { hint = n; hintSeconds = secs; }

    private void Trickster(float dt, Input i)
    {
        tauntedThisFrame = false;
        if (youFlying) { lastMoved = false; return; }
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
        // S218：L = 离你最近的一个能用的机关（香蕉皮 / 巨炮 / 滚石 / 水塔）
        OverworldMap.Cell? big = null; double bb = tuning.overworldPrankRange * tuning.overworldPrankRange;
        foreach (var c in OverworldProps.All(map))
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bb) { bb = d; big = c; }
        }
        int best = -1; double bd = tuning.overworldPrankRange * tuning.overworldPrankRange;
        foreach (var c in OverworldMap.Find(map, 'n'))
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id) || peels.ContainsKey(id)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bd) { bd = d; best = id; }
        }
        if (big.HasValue && (best < 0 || bb <= bd)) { if (Arm(big.Value, 1, tx, ty)) Hint(Note.BigArmed); else Hint(Note.BigStuck); return; }
        if (best < 0) { Hint(Note.PeelNo); return; }
        peels[best] = new Vector2(PeelFlashSeconds, PeelActive);
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
        OverworldSession.CarriedDaze = OverworldSession.Minute - OverworldSession.LastBigHitMinute <= tuning.overworldDazeCarryMinutes ? tuning.overworldDazeCarrySeconds : 0f; // S218：刚被大机关砸晕就进门 → 房间开局他还晕着
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

        if (marioFlying) return; // S218：被巨炮轰在天上（TickBigs 在移动他）
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
            heardNoise = !blind && heardNoise.HasValue, noisePos = heardNoise ?? default, noiseSuspicion = tuning.overworldNoiseSuspicion,
            bigStun = pendingMarioStun,
        };
        heardNoise = null; pendingMarioStun = 0f;
        int mid = (int)System.Math.Floor(mario.y) * map.W + (int)System.Math.Floor(mario.x);
        if (peels.TryGetValue(mid, out var peel) && peel.x <= 0f) { p.slipped = true; peels.Remove(mid); OverworldSession.UsedCells.Add(mid); }

        var o = mind.Tick(dt, p);
        if (blind && string.IsNullOrEmpty(o.mark)) o.mark = "…";
        lastOrder = o;
        // S218：他吃过亏的大机关在预警 / 正在滚，而且他看得见 → 先闪开（只躲几格、只在危险期间，H10 不会停住）
        var dodge = (o.state == OverworldMarioState.Walking || o.state == OverworldMarioState.Waiting || o.state == OverworldMarioState.Curious) ? DodgeCell(r) : null;
        if (dodge.HasValue)
        {
            o.target = new Vector2(dodge.Value.x + 0.5f, dodge.Value.y + 0.5f); o.mark = "WHOA!"; o.intent = "DODGE";
            mario.SetGoal(map, dodge.Value); mario.Step(map, tuning.overworldChaseSpeed, dt); lastOrder = o;
            return;
        }
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

    // ═════════ S218 大机关 ═════════
    /// <summary>发动（按 L 或被冲击震响）。先预警 fuse 秒（H3），UsedCells 立刻记上（每天一次）。from = 推的人 / 冲击点（决定滚石往哪滚）。</summary>
    public bool Arm(OverworldMap.Cell c, int depth, double fromX, double fromY)
    {
        int id = c.y * map.W + c.x; char k = map.At(c.x, c.y);
        if (!OverworldProps.IsBig(k) || OverworldSession.UsedCells.Contains(id)) return false;
        var b = new Big { c = c, kind = k, depth = depth, fuse = tuning.overworldBigFuseSeconds, fuseTotal = tuning.overworldBigFuseSeconds };
        if (k == 'K') { if (!OverworldProps.Aim(map, c, out _, out b.dir, out _)) return false; }
        else if (k == 'O') { b.dir = OverworldProps.PushDir(map, c, fromX, fromY); if (b.dir < 0) return false; }
        OverworldSession.UsedCells.Add(id);
        active.Add(b);
        if (depth > OverworldSession.BestChain) OverworldSession.BestChain = depth;
        if (depth >= 2) Hint(Note.BigChain, 2.5f);
        return true;
    }

    /// <summary>这个大机关现在会伤到哪些格（预警期间画出来；马里奥吃过亏就躲这些格）。水塔不伤人 → 空。</summary>
    public List<OverworldMap.Cell> Danger(Big b)
    {
        if (b.kind == 'K') return OverworldProps.MuzzleCells(map, b.c, b.dir);
        if (b.kind == 'O') { var l = b.lane ?? OverworldProps.Lane(map, b.c, b.dir); int from = (int)System.Math.Floor(b.rolled); return l.GetRange(System.Math.Min(from, l.Count), l.Count - System.Math.Min(from, l.Count)); }
        return new List<OverworldMap.Cell>();
    }

    private void Change(int x, int y, char c)
    {
        int id = y * map.W + x; OverworldMap.Set(map, x, y, c); OverworldSession.Changed[id] = c; changedCells.Add(id);
    }

    private void Noise(double x, double y)
    {
        if (!marioInside && Dist(x, y, mario.x, mario.y) <= tuning.overworldNoiseRange) heardNoise = new Vector2((float)x, (float)y);
        impacts.Add(new Impact3((float)x, (float)y, 1.5f));
    }

    private void HitMario(char kind)
    {
        pendingMarioStun = Mathf.Min(tuning.maxStunSeconds, tuning.overworldBigStunSeconds);
        OverworldSession.MarioWary.Add(kind); OverworldSession.BigHits++; OverworldSession.LastBigHitMinute = OverworldSession.Minute;
        Hint(Note.BigHit, 2f);
    }

    private void Impact(double x, double y, int depth, OverworldMap.Cell self)
    {
        Noise(x, y);
        foreach (var t in OverworldProps.ChainTargets(map, x, y)) if (!t.Equals(self)) Arm(t, depth + 1, x, y);
        foreach (var c in OverworldMap.Find(map, 'n')) // 冲击也会把旁边的香蕉皮震活
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id) || peels.ContainsKey(id)) continue;
            if (Dist(c.x + 0.5, c.y + 0.5, x, y) <= OverworldProps.ChainRadius) peels[id] = new Vector2(PeelFlashSeconds, PeelActive);
        }
    }

    private void TickBigs(float dt)
    {
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i]; f.t += dt;
            if (f.mario) { mario.x = f.X; mario.y = f.Y; }
            if (f.you) { tx = f.X; ty = f.Y; }
            if (f.t < f.dur) continue;
            flights.RemoveAt(i);
            if (f.mario) { mario.x = f.tx; mario.y = f.ty; mario.Clear(); HitMario('K'); }
            if (f.you) { tx = f.tx; ty = f.ty; frozen = Mathf.Max(frozen, 0.4f); }
            if (f.shell) Impact(f.tx, f.ty, f.depth, new OverworldMap.Cell(-1, -1));
        }
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var b = active[i];
            if (b.fuse > 0f) { b.fuse -= dt; if (b.fuse <= 0f && !Fire(b)) active.RemoveAt(i); continue; }
            if (!b.rolling) { active.RemoveAt(i); continue; }
            double before = b.rolled; b.rolled = System.Math.Min(b.lane.Count, b.rolled + tuning.overworldRollSpeed * dt);
            for (int k = (int)System.Math.Floor(before); k < (int)System.Math.Floor(b.rolled) && k < b.lane.Count; k++)
                if (OverworldProps.Smashable(map.At(b.lane[k].x, b.lane[k].y))) { Change(b.lane[k].x, b.lane[k].y, '.'); impacts.Add(new Impact3(b.lane[k].x + 0.5f, b.lane[k].y + 0.5f, 0.6f)); }
            double p = b.rolled, sx = b.c.x + 0.5 + OverworldProps.DX[b.dir] * p, sy = b.c.y + 0.5 + OverworldProps.DY[b.dir] * p;
            b.rx = sx; b.ry = sy;
            if (!b.hitMario && !marioInside && !marioFlying && Dist(sx, sy, mario.x, mario.y) < 0.8f) { b.hitMario = true; HitMario('O'); }
            if (!b.hitYou && !youFlying && Dist(sx, sy, tx, ty) < 0.8f) { b.hitYou = true; frozen = Mathf.Max(frozen, tuning.overworldBigStunSeconds); Hint(Note.BigSelf, 2f); }
            if (b.rolled >= b.lane.Count) { active.RemoveAt(i); var end = b.lane.Count > 0 ? b.lane[b.lane.Count - 1] : b.c; Impact(end.x + 0.5, end.y + 0.5, b.depth, b.c); }
        }
    }

    /// <summary>发动那一下。返回 false = 这个机关已经结束（不是滚石）。</summary>
    private bool Fire(Big b)
    {
        double cx = b.c.x + 0.5, cy = b.c.y + 0.5;
        if (b.kind == 'K')
        {
            OverworldProps.Aim(map, b.c, out var tg, out _, out _);
            var land = OverworldProps.Landing(map, tg, weather.kind == OverworldEvents.Kind.Wind ? weather.wind : -1);
            var muzzle = OverworldProps.MuzzleCells(map, b.c, b.dir);
            bool In(double x, double y) => muzzle.Exists(m => m.x == (int)System.Math.Floor(x) && m.y == (int)System.Math.Floor(y));
            double lx = land.x + 0.5, ly = land.y + 0.5;
            if (!marioInside && !marioFlying && In(mario.x, mario.y)) flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = lx, ty = ly, dur = FlightSeconds, mario = true });
            if (!youFlying && In(tx, ty)) { flights.Add(new Flight { fx = tx, fy = ty, tx = lx + 0.01, ty = ly, dur = FlightSeconds, you = true }); disguised = false; }
            flights.Add(new Flight { fx = cx, fy = cy, tx = lx, ty = ly, dur = FlightSeconds, shell = true, depth = b.depth });
            Noise(cx, cy);
            return false;
        }
        if (b.kind == 'O')
        {
            b.lane = OverworldProps.Lane(map, b.c, b.dir); b.rolling = true; b.rx = cx; b.ry = cy;
            Change(b.c.x, b.c.y, '.');
            Noise(cx, cy);
            return true;
        }
        int rad = OverworldProps.FloodRadius + (weather.kind == OverworldEvents.Kind.Rain ? 1 : 0);
        foreach (var c in OverworldProps.Flood(map, b.c, rad)) Change(c.x, c.y, 'g');
        foreach (var c in OverworldMap.Find(map, 'n'))
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id) || peels.ContainsKey(id)) continue;
            if ((c.x - b.c.x) * (c.x - b.c.x) + (c.y - b.c.y) * (c.y - b.c.y) <= rad * rad) peels[id] = new Vector2(PeelFlashSeconds, PeelActive);
        }
        impacts.Add(new Impact3((float)cx, (float)cy, rad));
        return false;
    }

    /// <summary>他站在他吃过亏的那种大机关的危险格里、而且看得见那个机关 → 最近的安全格（≤ overworldDodgeSteps 步）。H4：只用他看得见的预警 + 自己的经历。</summary>
    private OverworldMap.Cell? DodgeCell(OverworldMap.SightRules r)
    {
        if (marioInside || exitGrace > 0f) return null;
        var danger = new HashSet<(int, int)>();
        foreach (var b in active)
        {
            if (!OverworldSession.MarioWary.Contains(b.kind) || b.kind == 'U') continue;
            double px = b.rolling ? b.rx : b.c.x + 0.5, py = b.rolling ? b.ry : b.c.y + 0.5;
            if (!OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, px, py, r) && Dist(px, py, mario.x, mario.y) > r.range) continue;
            foreach (var c in Danger(b)) danger.Add((c.x, c.y));
        }
        var here = OverworldGuide.Near(map, mario.x, mario.y);
        if (!danger.Contains((here.x, here.y))) return null;
        var q = new Queue<(OverworldMap.Cell c, int d)>(); var seen = new HashSet<(int, int)> { (here.x, here.y) }; q.Enqueue((here, 0));
        while (q.Count > 0)
        {
            var (c, d) = q.Dequeue();
            if (!danger.Contains((c.x, c.y))) return c;
            if (d >= tuning.overworldDodgeSteps) continue;
            for (int k = 0; k < 4; k++)
            {
                var n = new OverworldMap.Cell(c.x + OverworldProps.DX[k], c.y + OverworldProps.DY[k]);
                if (!OverworldMap.Walkable(map, n.x, n.y) || !seen.Add((n.x, n.y))) continue;
                q.Enqueue((n, d + 1));
            }
        }
        return null;
    }

    /// <summary>S218 房间 → 小镇：刚守住的那户，离它最近的一个用过的巨炮 / 水塔重新装填（滚石碎了不能装）。</summary>
    private void Reload()
    {
        int door = OverworldSession.ReloadDoor; OverworldSession.ReloadDoor = 0;
        if (door <= 0 || !doorCells.TryGetValue(door, out var dc)) return;
        OverworldMap.Cell? best = null; double bd = double.MaxValue;
        foreach (var c in OverworldProps.All(map))
        {
            if (map.At(c.x, c.y) == 'O' || !OverworldSession.UsedCells.Contains(c.y * map.W + c.x)) continue;
            double d = Dist(c.x, c.y, dc.x, dc.y); if (d < bd) { bd = d; best = c; }
        }
        if (best.HasValue) { OverworldSession.UsedCells.Remove(best.Value.y * map.W + best.Value.x); reloaded = best; Hint(Note.BigReloaded, 3f); }
    }
    public OverworldMap.Cell? reloaded;

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
        range = tuning.overworldVisionRange * Fog, nightRange = tuning.overworldNightVisionRange * Fog, halfAngleDeg = tuning.overworldVisionHalfAngle,
        nearRadius = tuning.overworldNearSense, grassRadius = tuning.overworldGrassSeeRadius, lampRadius = tuning.overworldLampRadius,
        night = OverworldSession.Minute >= OverworldMap.NightStart,
    };

    public OverworldMap.Rules Rules => new OverworldMap.Rules
    {
        marioSpeed = tuning.overworldMarioSpeed, tricksterSpeed = tuning.overworldTricksterSpeed,
        minutesPerSecond = tuning.overworldMinutesPerSecond, visitMinutes = tuning.overworldVisitMinutes,
    };

    private float Fog => weather.kind == OverworldEvents.Kind.Fog ? tuning.overworldFogSight : 1f;

    private char Here(double x, double y) => map.At((int)System.Math.Floor(x), (int)System.Math.Floor(y));
    private static Vector2 Center(OverworldMap.Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
    public static float Dist(double ax, double ay, double bx, double by) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
