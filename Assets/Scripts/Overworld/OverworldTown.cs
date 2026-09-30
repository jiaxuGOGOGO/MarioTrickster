using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S213：小镇一帧的全部规则（纯逻辑，沙盒可测）——从 OverworldGame 里抽出来，游戏和"玩家视角模拟"（sim 里的机器人玩家）跑的是同一份代码。
/// OverworldGame 只负责：读键盘 → Input、画画面、切场景。
/// 读写 OverworldSession（跨场景的一天）；马里奥只通过 OverworldPercept 知道你（H4）。
/// </summary>
public sealed class OverworldTown
{
    public struct Input { public float h, v; public bool disguise, peel, taunt, door, fastForward; /// <summary>S219：这一帧按下的方向（坐在炮里瞄准用）：0 没按，1 右 2 左 3 上 4 下。</summary>
        public int aim; }

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
    public enum Note { None, TauntNone, Pickup, PeelNo, Peel, TooEarly, RoomMissing, Missed, LateHint, Caught, AmbushWait, Spotted, BigArmed, BigHit, BigChain, BigReloaded, BigSelf, BigStuck, CannonSeat, CannonBadAim, CannonTamper, MarioRides, Lightning, Mudslide, CaveHop, CaveNoExit }
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
        /// <summary>S219：巨炮瞄准的距离；rider = 坐在里面的人（0 没人，1 你，2 马里奥）；tampered = 马里奥坐炮时被你拨歪了。</summary>
        public int dist; public int rider; public bool tampered;
    }
    public sealed class Flight { public double fx, fy, tx, ty; public float t, dur; public int depth; public bool mario, you, shell, ride, tampered; public double X => fx + (tx - fx) * System.Math.Min(1f, t / dur); public double Y => fy + (ty - fy) * System.Math.Min(1f, t / dur); public float Arc => 4f * (t / dur) * (1f - t / dur); }
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
    public bool BigBusy => active.Count > 0 || flights.Count > 0 || seat != null || (ride != null && ride.seated);
    public float PeelActive => weather.kind == OverworldEvents.Kind.Rain ? PeelActiveSeconds * 1.5f : PeelActiveSeconds;

    public OverworldTown(OverworldMap.Map m, MarioMindTuningSO t, System.Func<int, bool> roomReady = null)
    {
        // S218：自己留一份（大机关会改地形；赶集日会改门的时间）——调用方的地图不变，重建小镇也不会越改越多
        m = OverworldMap.Parse(OverworldMap.ToText(m));
        weather = OverworldEvents.Of(m, OverworldSession.Day); // S219：天气池看地图格局（有路灯才有雷雨、有山洞才有酸雨）
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
        hint = Note.None; wantsEnter = false; changedCells.Clear(); impacts.Clear(); bolts.Clear();
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
        tauntedThisFrame = false; aimedThisFrame = false;
        if (youFlying) { lastMoved = false; return; }
        if (seat != null) { SeatTick(dt, i); return; }
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
        if (i.door && !TryCave() && !TryDoor()) TryBoard();
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
        // S218：L = 离你最近的一个能用的机关（香蕉皮 / 巨炮 / 滚石 / 水塔）；S219：他在炮里瞄准 → 先拨炮管；雷雨天路灯也算（召唤闪电）
        if (TryTamper()) return;
        double pr = PrankRange;
        OverworldMap.Cell? big = null; double bb = pr * pr;
        var cands = OverworldProps.All(map);
        if (weather.kind == OverworldEvents.Kind.Storm) cands.AddRange(lamps);
        foreach (var c in cands)
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bb) { bb = d; big = c; }
        }
        int best = -1; double bd = pr * pr;
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

    /// <summary>返回 true = 你在某扇门旁边（E 被门用掉了）。</summary>
    private bool TryDoor()
    {
        foreach (var kv in doorCells)
        {
            if (!NearDoor(kv.Key)) continue;
            var next = NextStop;
            if (next == null || next.n != kv.Key) { Hint(Note.TooEarly); return true; }
            var outcome = OverworldMind.AtDoor(!marioInside, insideSeconds, tuning.overworldLateWindowSeconds);
            if (outcome == OverworldMind.DoorOutcome.Missed) return true;
            // S213：埋伏 = 他快到了你已经守在门口。他还远 → 不进门，告诉你等（躲草丛 / P 伪装 / 空格快进）
            if (outcome == OverworldMind.DoorOutcome.Ambush && !AmbushReady) { Hint(Spotted ? Note.Spotted : Note.AmbushWait); return true; }
            EnterRoom(next, outcome);
            return true;
        }
        return false;
    }

    // ═════════ S219：坐进巨炮（你 / 马里奥都能坐）、现场瞄准、山洞隧道 ═════════
    public sealed class Seat { public OverworldMap.Cell k, board; public int dir, dist; public float t, fuse = -1f; }
    public sealed class Ride { public OverworldMap.Cell k, board; public int dir, dist, forStop; public bool seated, tampered; public float t; }
    /// <summary>你坐在炮里（null = 没坐）。</summary>
    public Seat seat;
    /// <summary>马里奥打算 / 正在坐的炮（null = 没有）。</summary>
    public Ride ride;
    private int ridePlannedFor = -1;
    private bool aimedThisFrame;
    public bool Seated => seat != null;
    public bool MarioSeated => ride != null && ride.seated;

    private int Wind => weather.kind == OverworldEvents.Kind.Wind ? weather.wind : -1;
    private static int Pack(int dir, int dist) => dir * 100 + dist;

    /// <summary>这门炮现在瞄着哪（你上次瞄好的；没瞄过 = 靶心 X）。</summary>
    public void AimOf(OverworldMap.Cell k, out int dir, out int dist)
    {
        if (OverworldSession.CannonAim.TryGetValue(k.y * map.W + k.x, out int a)) { dir = a / 100; dist = a % 100; return; }
        OverworldProps.DefaultAim(map, k, out dir, out dist);
    }
    public OverworldMap.Cell AimLandingOf(OverworldMap.Cell k, int dir, int dist) => OverworldProps.AimLanding(map, k, dir, dist, Wind);

    /// <summary>画面层用：现在所有"瞄着的"落点（你坐炮 / 他坐炮 / 预警中的巨炮）。ok = 这一炮能打。</summary>
    public struct AimView { public OverworldMap.Cell k, land; public int dir; public bool ok, mario; }
    public List<AimView> Aims()
    {
        var l = new List<AimView>();
        if (seat != null) { var ld = AimLandingOf(seat.k, seat.dir, seat.dist); l.Add(new AimView { k = seat.k, land = ld, dir = seat.dir, ok = OverworldProps.AimOk(map, seat.k, seat.dir, ld, home) }); }
        if (ride != null && ride.seated) l.Add(new AimView { k = ride.k, land = AimLandingOf(ride.k, ride.dir, ride.dist), dir = ride.dir, ok = true, mario = true });
        foreach (var b in active) if (b.kind == 'K' && b.fuse > 0f && b.rider == 0) l.Add(new AimView { k = b.c, land = AimLandingOf(b.c, b.dir, b.dist), dir = b.dir, ok = true });
        return l;
    }

    private bool CannonFree(OverworldMap.Cell k) => map.At(k.x, k.y) == 'K' && !OverworldSession.UsedCells.Contains(k.y * map.W + k.x)
        && (seat == null || !seat.k.Equals(k)) && (ride == null || !ride.k.Equals(k)) && !active.Exists(b => b.c.Equals(k));

    /// <summary>E 靠近一门没用过的巨炮（1.6 格内）→ 坐进去。</summary>
    private void TryBoard()
    {
        OverworldMap.Cell? best = null; double bd = 1.6 * 1.6;
        foreach (var c in OverworldProps.All(map))
        {
            if (!CannonFree(c)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bd) { bd = d; best = c; }
        }
        if (!best.HasValue) return;
        var k = best.Value; AimOf(k, out int dir, out int dist);
        seat = new Seat { k = k, board = OverworldGuide.Near(map, tx, ty), dir = dir, dist = dist };
        disguised = false; tx = k.x + 0.5; ty = k.y + 0.5; lastMoved = false;
        Hint(Note.CannonSeat, 3f);
    }

    /// <summary>坐在炮里：方向键瞄准（落点实时画出来），L 发射，E 下来（瞄好的方向留着——以后在外面按 L 就打那里）。坐太久自动发射（H9）。</summary>
    private void SeatTick(float dt, Input i)
    {
        lastMoved = false;
        var s = seat;
        if (s.fuse >= 0f) return; // 已经点火（TickBigs 在数）
        s.t += dt;
        if (i.aim >= 1 && i.aim <= 4) { int od = s.dir, ok = s.dist; OverworldProps.AimStep(map, s.k, ref s.dir, ref s.dist, i.aim - 1); aimedThisFrame = od != s.dir || ok != s.dist; }
        OverworldSession.CannonAim[s.k.y * map.W + s.k.x] = Pack(s.dir, s.dist);
        var land = AimLandingOf(s.k, s.dir, s.dist); bool good = OverworldProps.AimOk(map, s.k, s.dir, land, home);
        if (i.door) { LeaveSeat(); return; }
        bool timeUp = s.t >= tuning.overworldCannonSeatSeconds;
        if (i.peel || timeUp)
        {
            if (!good) { if (timeUp) LeaveSeat(); else Hint(Note.CannonBadAim); return; }
            var b = new Big { c = s.k, kind = 'K', dir = s.dir, dist = s.dist, depth = 1, rider = 1, fuse = tuning.overworldCannonFireSeconds, fuseTotal = tuning.overworldCannonFireSeconds };
            OverworldSession.UsedCells.Add(s.k.y * map.W + s.k.x); active.Add(b); s.fuse = b.fuse;
        }
    }

    private void LeaveSeat() { if (seat == null) return; tx = seat.board.x + 0.5; ty = seat.board.y + 0.5; seat = null; }

    /// <summary>站在山洞 h 里按 E → 从配对的另一个山洞出来。返回 true = E 被山洞用掉了。</summary>
    private bool TryCave()
    {
        var here = new OverworldMap.Cell((int)System.Math.Floor(tx), (int)System.Math.Floor(ty));
        if (map.At(here.x, here.y) != 'h') return false;
        var exit = OverworldProps.CaveExit(map, here);
        if (!exit.HasValue) { Hint(Note.CaveNoExit); return true; }
        tx = exit.Value.x + 0.5; ty = exit.Value.y + 0.5; disguised = false; lastMoved = false;
        OverworldSession.CaveHops++; Hint(Note.CaveHop, 1.5f);
        impacts.Add(new Impact3((float)tx, (float)ty, 0.5f));
        return true;
    }

    /// <summary>
    /// 马里奥会不会坐炮（H4：只用公开的东西——炮、靶心、地图；H10：落点一定走得回家）。
    /// 每一站只算一次：步数场（从门 / 从他）各一次 BFS，每门没用过的炮试所有瞄准，省下 ≥ overworldMarioCannonSaveSteps 格才坐。吃过炮的亏（MarioWary 有 K）就不坐。
    /// </summary>
    private void PlanRide(OverworldMap.Cell door)
    {
        ridePlannedFor = OverworldSession.NextStop; ride = null;
        if (OverworldSession.MarioWary.Contains('K')) return;
        var ks = OverworldProps.All(map).FindAll(c => map.At(c.x, c.y) == 'K' && CannonFree(c)); if (ks.Count == 0) return;
        var me = OverworldGuide.Near(map, mario.x, mario.y);
        var fromDoor = OverworldProps.StepsField(map, door); var fromMe = OverworldProps.StepsField(map, me);
        int direct = fromDoor[me.y * map.W + me.x]; if (direct < 0) return;
        int bestCost = direct - tuning.overworldMarioCannonSaveSteps; Ride best = null;
        foreach (var k in ks)
        {
            OverworldMap.Cell? board = null; int bb = int.MaxValue;
            for (int d = 0; d < 4; d++) { int x = k.x + OverworldProps.DX[d], y = k.y + OverworldProps.DY[d]; if (!OverworldMap.Walkable(map, x, y)) continue; int v = fromMe[y * map.W + x]; if (v >= 0 && v < bb) { bb = v; board = new OverworldMap.Cell(x, y); } }
            if (!board.HasValue) continue;
            for (int d = 0; d < 4; d++)
            {
                if (OverworldProps.MuzzleCells(map, k, d).Count == 0) continue;
                for (int dist = OverworldProps.MinAim; dist <= OverworldProps.MaxAim; dist++)
                {
                    var land = AimLandingOf(k, d, dist); int rest = fromDoor[land.y * map.W + land.x];
                    if (rest < 0) continue; // 落点走得到门 = 走得回家（门本来就和家连通，H1）
                    int cost = bb + 2 + rest;
                    if (cost < bestCost) { bestCost = cost; best = new Ride { k = k, board = board.Value, dir = d, dist = dist, forStop = OverworldSession.NextStop }; }
                }
            }
        }
        ride = best;
    }

    private bool RideStillFree() => ride != null && !OverworldSession.UsedCells.Contains(ride.k.y * map.W + ride.k.x) && (seat == null || !seat.k.Equals(ride.k)) && !active.Exists(x => x.c.Equals(ride.k)); // 你先坐进去了 / 已经被发动

    /// <summary>马里奥坐在炮里这一帧：瞄 overworldMarioCannonAimSeconds 秒（落点一直画在地上），然后发射。</summary>
    private void MarioRide(float dt)
    {
        ride.t += dt;
        lastOrder = new OverworldOrder { state = OverworldMarioState.Waiting, mark = "AIM", intent = "AIMING" };
        if (ride.t < tuning.overworldMarioCannonAimSeconds) return;
        // 发射：他飞到落点；炮口里的人（你）也被轰过去
        var land = AimLandingOf(ride.k, ride.dir, ride.dist); double lx = land.x + 0.5, ly = land.y + 0.5;
        OverworldSession.UsedCells.Add(ride.k.y * map.W + ride.k.x);
        flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = lx, ty = ly, dur = FlightSeconds, mario = true, ride = true, tampered = ride.tampered });
        var muzzle = OverworldProps.MuzzleCells(map, ride.k, ride.dir);
        if (!youFlying && seat == null && muzzle.Exists(q => q.x == (int)System.Math.Floor(tx) && q.y == (int)System.Math.Floor(ty))) { flights.Add(new Flight { fx = tx, fy = ty, tx = lx + 0.01, ty = ly, dur = FlightSeconds, you = true }); disguised = false; }
        flights.Add(new Flight { fx = ride.k.x + 0.5, fy = ride.k.y + 0.5, tx = lx, ty = ly, dur = FlightSeconds, shell = true, depth = 1 });
        Noise(ride.k.x + 0.5, ride.k.y + 0.5);
        ride = null;
    }

    /// <summary>他坐在炮里瞄准时，你在旁边按 L：炮管拨到下一个能打的方向（距离不变）。他落地会晕（被你耍了），以后不再坐炮。</summary>
    private bool TryTamper()
    {
        if (ride == null || !ride.seated) return false;
        double r = PrankRange; if (Dist(ride.k.x + 0.5, ride.k.y + 0.5, tx, ty) > r) return false;
        for (int s = 1; s <= 3; s++)
        {
            int d = (ride.dir + s) % 4; if (OverworldProps.MuzzleCells(map, ride.k, d).Count == 0) continue;
            var land = AimLandingOf(ride.k, d, ride.dist); if (!OverworldProps.AimOk(map, ride.k, d, land, home)) continue;
            ride.dir = d; ride.tampered = true; ride.t = Mathf.Min(ride.t, tuning.overworldMarioCannonAimSeconds * 0.5f); Hint(Note.CannonTamper, 2f); return true;
        }
        return false;
    }

    /// <summary>站在山丘上 L 够得远 1 格。</summary>
    public double PrankRange => tuning.overworldPrankRange + (Here(tx, ty) == '^' ? 1 : 0);

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
        // S219：他要去门了 → 看看坐炮是不是快很多（每一站算一次）；坐在炮里时他看不见外面
        if (schedule.HasValue && next != null && ridePlannedFor != OverworldSession.NextStop && mind.State == OverworldMarioState.Walking && exitGrace <= 0f) PlanRide(goalCell);
        if (ride != null && ride.seated) { MarioRide(dt); return; }
        if (ride != null && (ride.forStop != OverworldSession.NextStop || !schedule.HasValue || !RideStillFree())) ride = null;
        var r = Sight();
        bool blind = exitGrace > 0f; // 出门缓冲：他在清点战利品，不看不听（头上有 '…' 标记，H6）
        bool sees = !blind && OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r);
        var p = new OverworldPercept
        {
            marioPos = new Vector2((float)mario.x, (float)mario.y),
            seesFigure = sees,
            figurePos = new Vector2((float)tx, (float)ty),
            figureLooksLikeProp = disguised || seat != null, // S219：坐在炮里 = 他只看见一门炮；炮管转动 = 可疑（和伪装同一条规则 H6）
            figureMoving = lastMoved || aimedThisFrame,
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
        bool toCannon = ride != null && o.state == OverworldMarioState.Walking; // S219：平静地赶路时才去坐炮（起疑 / 追你就先不坐）
        if (toCannon) { o.target = new Vector2(ride.board.x + 0.5f, ride.board.y + 0.5f); o.intent = "CANNON!"; lastOrder = o; }
        if (o.target.HasValue)
        {
            var tc = new OverworldMap.Cell(Mathf.FloorToInt(o.target.Value.x), Mathf.FloorToInt(o.target.Value.y));
            if (!OverworldMap.Walkable(map, tc.x, tc.y) && !OverworldCatalog.IsDoor(map.At(tc.x, tc.y))) tc = goalCell;
            mario.SetGoal(map, tc);
            float speed = o.state == OverworldMarioState.Chasing ? tuning.overworldChaseSpeed : tuning.overworldMarioSpeed;
            mario.Step(map, speed, dt);
        }
        if (o.tryCatch) Caught();
        if (toCannon && Dist(mario.x, mario.y, ride.board.x + 0.5, ride.board.y + 0.5) < 0.35) { ride.seated = true; ride.t = 0f; mario.x = ride.k.x + 0.5; mario.y = ride.k.y + 0.5; mario.Clear(); Hint(Note.MarioRides, 2.5f); return; }

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
        bool ok = OverworldProps.IsBig(k) || (k == 'i' && weather.kind == OverworldEvents.Kind.Storm) || (k == '^' && OverworldProps.MudDir(map, c) >= 0);
        if (!ok || OverworldSession.UsedCells.Contains(id)) return false;
        if (k == 'K' && ((seat != null && seat.k.Equals(c)) || (ride != null && ride.seated && ride.k.Equals(c)))) return false; // 有人坐着，由坐的人发射
        var b = new Big { c = c, kind = k, depth = depth, fuse = tuning.overworldBigFuseSeconds, fuseTotal = tuning.overworldBigFuseSeconds };
        if (k == 'K')
        {
            AimOf(c, out b.dir, out b.dist); // S219：你瞄好的方向（没瞄过 = 靶心）
            if (!OverworldProps.AimOk(map, c, b.dir, AimLandingOf(c, b.dir, b.dist), home)) return false;
        }
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
        if (b.kind == 'i') { var l = new List<OverworldMap.Cell>(); int r = (int)System.Math.Ceiling(OverworldProps.LightningRadius); for (int y = b.c.y - r; y <= b.c.y + r; y++) for (int x = b.c.x - r; x <= b.c.x + r; x++) if (OverworldMap.Walkable(map, x, y) && Dist(x + 0.5, y + 0.5, b.c.x + 0.5, b.c.y + 0.5) <= OverworldProps.LightningRadius + 1e-6) l.Add(new OverworldMap.Cell(x, y)); return l; }
        if (b.kind == '^') return OverworldProps.MudLane(map, b.c);
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
        if (OverworldEvents.Wet(weather)) foreach (var t in OverworldProps.MudSources(map, x, y)) if (!t.Equals(self)) Arm(t, depth + 1, x, y); // S219：下雨天震到山丘 = 泥石流
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
            if (f.mario) { mario.x = f.tx; mario.y = f.ty; mario.Clear(); if (!f.ride || f.tampered) HitMario('K'); } // S219：他自己坐炮、没被你拨歪 = 平稳落地
            if (f.you) { tx = f.tx; ty = f.ty; frozen = Mathf.Max(frozen, 0.4f); if (seat != null) seat = null; }
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
            var land = AimLandingOf(b.c, b.dir, b.dist); // S219：瞄准的落点（没瞄过 = 靶心 X，和 S218 一样）
            var muzzle = OverworldProps.MuzzleCells(map, b.c, b.dir);
            bool In(double x, double y) => muzzle.Exists(m => m.x == (int)System.Math.Floor(x) && m.y == (int)System.Math.Floor(y));
            double lx = land.x + 0.5, ly = land.y + 0.5;
            if (!marioInside && !marioFlying && In(mario.x, mario.y)) flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = lx, ty = ly, dur = FlightSeconds, mario = true });
            if (!youFlying && (b.rider == 1 || (seat == null && In(tx, ty)))) { flights.Add(new Flight { fx = tx, fy = ty, tx = lx + 0.01, ty = ly, dur = FlightSeconds, you = true }); disguised = false; if (b.rider == 1) OverworldSession.CannonRides++; }
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
        if (b.kind == 'i') return FireLightning(b);
        if (b.kind == '^') return FireMud(b);
        int rad = OverworldProps.FloodRadius + (weather.kind == OverworldEvents.Kind.Rain ? 1 : 0);
        // S219 山洪：水塔淹到的山丘（泥石流源头）也冲下来（不管天气）
        foreach (var hc in OverworldMap.Find(map, '^')) if (OverworldProps.MudDir(map, hc) >= 0 && (hc.x - b.c.x) * (hc.x - b.c.x) + (hc.y - b.c.y) * (hc.y - b.c.y) <= rad * rad) Arm(hc, b.depth + 1, cx, cy);
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

    /// <summary>S219 闪电：打在路灯上，1.5 格内的人晕 2 秒；冲击会震响旁边的大机关 / 山丘（雷雨 = 湿，会泥石流）。</summary>
    private bool FireLightning(Big b)
    {
        double cx = b.c.x + 0.5, cy = b.c.y + 0.5, r = OverworldProps.LightningRadius + 0.3;
        if (!marioInside && !marioFlying && !MarioSeated && Dist(cx, cy, mario.x, mario.y) <= r) HitMario('i');
        if (!youFlying && seat == null && Dist(cx, cy, tx, ty) <= r) { frozen = Mathf.Max(frozen, tuning.overworldBigStunSeconds); disguised = false; Hint(Note.BigSelf, 2f); }
        OverworldSession.Lightnings++; bolts.Add(new Impact3((float)cx, (float)cy, 1f));
        Impact(cx, cy, b.depth, b.c); impacts.Add(new Impact3((float)cx, (float)cy, (float)OverworldProps.LightningRadius));
        if (hint == Note.None || hint == Note.BigArmed) Hint(Note.Lightning, 2f);
        return false;
    }

    /// <summary>S219 泥石流：山丘朝离开山的方向冲 6 格，全部变泥地（木箱栅栏冲垮），冲到的人晕；冲到头再震一下（连锁）。</summary>
    private bool FireMud(Big b)
    {
        var lane = OverworldProps.MudLane(map, b.c);
        Change(b.c.x, b.c.y, 'g');
        foreach (var q in lane)
        {
            if (!marioInside && !marioFlying && !MarioSeated && (int)System.Math.Floor(mario.x) == q.x && (int)System.Math.Floor(mario.y) == q.y) HitMario('^');
            if (!youFlying && seat == null && (int)System.Math.Floor(tx) == q.x && (int)System.Math.Floor(ty) == q.y) { frozen = Mathf.Max(frozen, tuning.overworldBigStunSeconds); Hint(Note.BigSelf, 2f); }
            if (OverworldProps.Muddable(map.At(q.x, q.y))) Change(q.x, q.y, 'g'); impacts.Add(new Impact3(q.x + 0.5f, q.y + 0.5f, 0.6f)); // 门 / 家 / 山洞 / 靶心 冲过但不改
        }
        OverworldSession.Mudslides++;
        var end = lane.Count > 0 ? lane[lane.Count - 1] : b.c;
        Impact(end.x + 0.5, end.y + 0.5, b.depth, b.c);
        if (hint == Note.None || hint == Note.BigArmed) Hint(Note.Mudslide, 2f);
        return false;
    }

    /// <summary>这一帧打下来的闪电（画面层画一道光）。</summary>
    public readonly List<Impact3> bolts = new List<Impact3>();

    /// <summary>他站在他吃过亏的那种大机关的危险格里、而且看得见那个机关 → 最近的安全格（≤ overworldDodgeSteps 步）。H4：只用他看得见的预警 + 自己的经历。</summary>
    private OverworldMap.Cell? DodgeCell(OverworldMap.SightRules r)
    {
        if (marioInside || exitGrace > 0f) return null;
        var danger = new HashSet<(int, int)>();
        foreach (var b in active)
        {
            if (!OverworldSession.MarioWary.Contains(b.kind) || b.kind == 'U' || b.rider == 2) continue;
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

    private float Fog => weather.kind == OverworldEvents.Kind.Fog ? tuning.overworldFogSight : weather.kind == OverworldEvents.Kind.Acid ? tuning.overworldAcidSight : 1f;

    private char Here(double x, double y) => map.At((int)System.Math.Floor(x), (int)System.Math.Floor(y));
    private static Vector2 Center(OverworldMap.Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
    public static float Dist(double ax, double ay, double bx, double by) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
