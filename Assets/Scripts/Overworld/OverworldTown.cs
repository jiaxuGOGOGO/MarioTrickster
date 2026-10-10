using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S213：小镇一帧的全部规则（纯逻辑，沙盒可测）——从 OverworldGame 里抽出来，游戏和"玩家视角模拟"（sim 里的机器人玩家）跑的是同一份代码。
/// OverworldGame 只负责：读键盘 → Input、画画面、切场景。
/// 读写 OverworldSession（跨场景的一天）；马里奥只通过 OverworldPercept 知道你（H4）。
/// </summary>
public sealed class OverworldTown
{
    public struct Input { public float h, v; public bool disguise, peel, taunt, door, fastForward, /// <summary>S220：Q = 能量满时召唤雷云。</summary>
        weather; /// <summary>S219：这一帧按下的方向（坐在炮里瞄准用）：0 没按，1 右 2 左 3 上 4 下。</summary>
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
    /// <summary>S224 少等待：你不碰键盘这么多秒（真实秒）后自动快进（0 = 关）。驱动层按调参设；纯逻辑默认关，老的模拟不受影响。
    /// 自动快进和按住空格完全一样：只在"没事"时生效（CanFastForward），他一起疑 / 快到门口 / 附近有闪电就立刻恢复。</summary>
    public float autoFastIdleSeconds;
    /// <summary>S224：在门口按过 E 但他还远 = 预约埋伏：躲着别走开，他一走进埋伏范围就自动进门（不用一直按 E），等的时候自动快进。
    /// 走开门口 / 被他盯上 = 取消（和以前"必须他快到了才算埋伏"同一条规则，只是不用你掐着时间按）。</summary>
    public bool ambushArmed;
    private float idleReal;
    /// <summary>S224 声音圈（Mark of the Ninja）：这一帧发出的声音（x, y, 他多远听得见）。圈的大小 = 判定用的同一个数。</summary>
    public readonly List<Impact3> sounds = new List<Impact3>();

    // ── 这一帧发生的事（驱动层读完就清）──
    /// <summary>这一帧要给玩家的提示（驱动层翻成界面文字；纯逻辑这边不碰文字，sim 才能跑）。</summary>
    public enum Note { None, TauntNone, Pickup, PeelNo, Peel, TooEarly, RoomMissing, Missed, LateHint, Caught, AmbushWait, Spotted, BigArmed, BigHit, BigChain, BigReloaded, BigSelf, BigStuck, /* S228 */ Bell, BellIgnored, WindowFling, CannonSeat, CannonBadAim, CannonTamper, MarioRides, Lightning, Mudslide, CaveHop, CaveNoExit,
        /* S220 */ YouHurt, YouKO, MarioKO, Heal, MarioHeal, EnergyUp, EnergyFull, Cloud, CloudLow, StormBolt, /* S224 */ AmbushArmed, /* S229 */ MarioDied, YouDied, BothDied, /* S232 */ PickupTaunt, PickupHeart, PickupWasted }
    public Note hint; public float hintSeconds;
    /// <summary>S222：他最后一次看见你的原因（被抓时告诉玩家，H4 同一组视线输入）。</summary>
    public OverworldMap.SeenWhy seenWhy, caughtWhy;
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
        /// <summary>S220：这一下是你引发的（你按 L / 连锁自你的机关）→ 他掉心你得能量。马里奥自己坐炮 / 雷区天灾 = false。</summary>
        public bool byYou = true;
    }
    public sealed class Flight { public double fx, fy, tx, ty; public float t, dur; public int depth; public bool mario, you, shell, ride, tampered, /* S220 */ hurt, byYou, /* S228 */ window; public double X => fx + (tx - fx) * System.Math.Min(1f, t / dur); public double Y => fy + (ty - fy) * System.Math.Min(1f, t / dur); public float Arc => 4f * (t / dur) * (1f - t / dur); }
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
    /// <summary>S228：这一声是钟楼（他上过当 = 不再停下）；他最后一次听见钟的游戏分钟（钟响后几秒内挨砸 = 学会）。</summary>
    private bool heardBell; private double bellHeardMinute = -9999;
    public const float BellLearnSeconds = 4f;
    public bool marioFlying => flights.Exists(f => f.mario);
    public bool youFlying => flights.Exists(f => f.you);
    public bool BigBusy => active.Count > 0 || flights.Count > 0 || seat != null || (ride != null && ride.seated) || cloud != null;

    // ═════════ S220：心 / 闪电预警 / 雷云 ═════════
    /// <summary>一道要劈下来的闪电：地上闪 total 秒（最后 0.3 秒变白），劈十字形 1 格。zone = 雷区（-1 = 雷云）。</summary>
    public sealed class Strike { public OverworldMap.Cell c; public float t, total; public int zone; public bool byYou; public float Left => t; public bool White => t <= OverworldStorm.FlashSeconds; }
    public readonly List<Strike> strikes = new List<Strike>();
    /// <summary>雷云：停在召唤的地方（不跟着你走——你自己得逃出来）。</summary>
    public sealed class Cloud { public double x, y; public float t, next; public int volley, id; }
    public Cloud cloud;
    /// <summary>这一帧谁受了什么伤（画面层飘字 / 角标）。kind 0 = 掉 1 颗心（红碎心），1 = 只晕（黄星），2 = 变慢（蓝水滴），3 = 补心（绿），4 = 能量（紫）。</summary>
    public struct HurtFx { public float x, y; public bool mario; public int kind; }
    public readonly List<HurtFx> hurts = new List<HurtFx>();
    private float marioGrace, youGrace;
    private readonly OverworldMap.Map stormMap; // 雷区落点按"设计台里画的那张图"算（不受今天撞碎 / 淹掉的地形影响）→ 和网页预览一模一样
    private int[] stormLast = new int[0];
    public float MarioGrace => marioGrace; public float YouGrace => youGrace;
    public float PeelActive => weather.kind == OverworldEvents.Kind.Rain ? PeelActiveSeconds * 1.5f : PeelActiveSeconds;

    public OverworldTown(OverworldMap.Map m, MarioMindTuningSO t, System.Func<int, bool> roomReady = null)
    {
        // S218：自己留一份（大机关会改地形；赶集日会改门的时间）——调用方的地图不变，重建小镇也不会越改越多
        m = OverworldMap.Parse(OverworldMap.ToText(m));
        stormMap = OverworldMap.Parse(OverworldMap.ToText(m));
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
        WindowFlingStart();
        // S220：雷区从"现在这一轮"开始数——进场景 / 从房间出来不会立刻劈、错过的轮次不补
        stormLast = new int[map.storms.Count];
        for (int z = 0; z < stormLast.Length; z++) stormLast[z] = OverworldStorm.VolleyIndex(OverworldSession.Minute, t.overworldStormVolleySeconds, t.overworldMinutesPerSecond, z);
    }

    /// <summary>S228：从房间出来、他在房间里挨过你的炮 → 从门口往外被轰出去（落点先算好、走得回家，H1）。</summary>
    public bool windowFlung;
    private void WindowFlingStart()
    {
        int door = OverworldSession.WindowFlingDoor; OverworldSession.WindowFlingDoor = 0;
        if (door <= 0 || !OverworldSession.HasPositions || !doorCells.TryGetValue(door, out var dc)) return;
        var land = OverworldProps.WindowLanding(map, dc, tuning.overworldWindowFlingCells, home);
        if (!land.HasValue) return;
        flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = land.Value.x + 0.5, ty = land.Value.y + 0.5, dur = FlightSeconds, mario = true, byYou = true, window = true });
        OverworldSession.WindowFlings++; windowFlung = true;
    }

    public OverworldMap.Door NextStop => OverworldSession.NextStop < stops.Count ? stops[OverworldSession.NextStop] : null;

    /// <summary>快进只在"没事"时允许：他平静、没在门里、你没被定身。任何起疑 / 他进门 → 立刻恢复正常速度。</summary>
    public bool CanFastForward => !dayOver && !marioInside && frozen <= 0f && exitGrace <= 0f
        && (mind.State == OverworldMarioState.Walking || mind.State == OverworldMarioState.Waiting) && mind.Meter.Level == SuspicionLevel.Calm
        && !BigBusy && !StormNear(8)
        && (MarioStepsToDoor < 0 || MarioStepsToDoor > tuning.overworldAmbushSteps + 6); // 他快进入埋伏范围 → 自动恢复正常速度，不会快进错过

    public void Tick(float dt, Input i)
    {
        hint = Note.None; wantsEnter = false; changedCells.Clear(); impacts.Clear(); bolts.Clear(); hurts.Clear(); sounds.Clear();
        if (dayOver) return;
        bool touched = i.h != 0f || i.v != 0f || i.disguise || i.peel || i.taunt || i.weather || i.aim != 0 || (i.door && !ambushArmed);
        idleReal = touched ? 0f : idleReal + dt;
        bool autoFast = ambushArmed || (autoFastIdleSeconds > 0f && idleReal >= autoFastIdleSeconds);
        timeScale = (i.fastForward || autoFast) && CanFastForward ? FastForwardScale : 1f;
        float gdt = dt * timeScale; // 游戏时间（人和马里奥都按它走：快进 = 整个世界快放，公平）
        OverworldSession.Minute += gdt * tuning.overworldMinutesPerSecond;
        if (exitGrace > 0f) exitGrace -= dt;
        UpdateSteps(gdt);
        Unstick();
        Trickster(gdt, i);
        if (wantsEnter) return;
        TickPeels(gdt);
        TickBigs(gdt);
        TickStorms(gdt);
        if (marioGrace > 0f) marioGrace -= gdt; if (youGrace > 0f) youGrace -= gdt;
        Mario(gdt);
        ArmedAmbush();
        OverworldSession.NearMiss.Feed(!marioInside && !dayOver && mind.Meter.Level != SuspicionLevel.Calm, mind.Meter.Normalized, OverworldMap.Clock(OverworldSession.Minute), seenWhy == OverworldMap.SeenWhy.None ? "" : seenWhy.ToString());
        TrackReaction(dt, i.h != 0f || i.v != 0f);
        if (marioDies || youDies) { Die(); return; }
        if (OverworldSession.Minute >= OverworldMap.DayEnd) EndDay();
    }

    // ═════════ S229：心掉光 = 这一天立刻结束（结算 → 重开） ═════════
    private bool marioDies, youDies, marioDeathByYou; private char marioDeathKind = ' ', youDeathKind = ' ';
    /// <summary>同一帧里收集"谁的心掉光了"，帧末统一结算：只有他 = 你赢；只有你 = 他赢；同一下两人都没了 = 平局。
    /// 不困死（H1/H9）：结束 = 结算画面，按 R 立刻重来（或几秒后自动重来）；而且死之前每一下都有 1.2 秒预警 + 保护期（S221），不会被连控打死。</summary>
    private void Die()
    {
        OverworldSession.Death = marioDies && youDies ? OverworldSession.DeathEnd.Both : marioDies ? OverworldSession.DeathEnd.MarioDied : OverworldSession.DeathEnd.YouDied;
        OverworldSession.DeathCause = marioDies ? marioDeathKind : youDeathKind; OverworldSession.YouDeathCause = youDies ? youDeathKind : ' ';
        OverworldSession.DeathByYou = marioDies && marioDeathByYou;
        Hint(marioDies && youDies ? Note.BothDied : marioDies ? Note.MarioDied : Note.YouDied, 4f);
        dayOver = true; OverworldSession.DayOver = true;
        OverworldSession.DelayedSeconds += mind.DelayedSeconds;
    }

    /// <summary>S229：反应时间 = 危险预警出现在你脚下（你没被定身、也没在按方向）→ 你第一次按方向键。只用现实秒（不受快进影响）。
    /// 预警结束前没动 = 不算（量不到）。给机器人手抖校准用：sim 里机器人愣 0.15–0.6 秒，你的中位数写进 town_days.csv。</summary>
    private float reactT = -1f; private bool youDangerPrev;
    public float ReactingFor => reactT;
    private void TrackReaction(float realDt, bool moved)
    {
        bool danger = !youFlying && frozen <= 0f && seat == null && YouInDanger();
        if (reactT >= 0f)
        {
            reactT += realDt;
            if (moved) { OverworldSession.Reactions.Add(reactT); reactT = -1f; }
            else if (!danger) reactT = -1f;
        }
        else if (danger && !youDangerPrev && !moved) reactT = 0f;
        youDangerPrev = danger;
    }

    /// <summary>你站的格子现在在不在某个预警里（闪电十字 / 大机关的危险格）。</summary>
    public bool YouInDanger()
    {
        int x = (int)System.Math.Floor(tx), y = (int)System.Math.Floor(ty);
        foreach (var st in strikes) if (OverworldStorm.InPlus(st.c, tx, ty)) return true;
        foreach (var b in active) { if (b.kind == 'U' || b.kind == 'B') continue; foreach (var c in Danger(b)) if (c.x == x && c.y == y) return true; }
        return false;
    }

    /// <summary>S224：预约埋伏——走开 / 被盯上就取消；他一走进埋伏范围就自动进门（同 TryDoor 的规则）。</summary>
    private void ArmedAmbush()
    {
        if (!ambushArmed || wantsEnter) return;
        var next = NextStop;
        if (next == null || !NearDoor(next.n) || marioInside || dayOver || frozen > 0f) { ambushArmed = false; return; }
        if (Spotted) { ambushArmed = false; Hint(Note.Spotted); return; }
        if (AmbushReady) { ambushArmed = false; EnterRoom(next, OverworldMind.DoorOutcome.Ambush); }
    }

    private void Hint(Note n, float secs = 2f) { hint = n; hintSeconds = secs; }

    /// <summary>S235 防卡死（H9）：你 / 马里奥的身体压进了不能走的格子或地图外（坐炮、飞行中除外）→ 挪到最近能站的地方。
    /// 以前：落点算歪 / 从房间带回的位置不对时，Move 每一步都被挡 → 永远动不了、马里奥却照样走（和房间里"掉出去回不来"同一类问题）。</summary>
    public int unsticks;
    private void Unstick()
    {
        if (seat == null && !youFlying && !OverworldMap.Free(map, tx, ty))
        {
            (tx, ty) = OverworldMap.Unstick(map, tx, ty); unsticks++;
            impacts.Add(new Impact3((float)tx, (float)ty, 0.4f));
        }
        if (!marioInside && !marioFlying && !MarioSeated && !OverworldMap.Free(map, mario.x, mario.y))
        {
            var (mx, my) = OverworldMap.Unstick(map, mario.x, mario.y); mario.x = mx; mario.y = my; mario.Clear(); unsticks++;
        }
    }

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
            else { OverworldSession.TauntsUsed++; tauntedThisFrame = true; sounds.Add(new Impact3((float)tx, (float)ty, Step1Readability.TownTauntRadius(tuning))); }
        }
        int cx = (int)System.Math.Floor(tx), cy = (int)System.Math.Floor(ty), id = cy * map.W + cx;
        if (map.At(cx, cy) == '?' && !OverworldSession.UsedCells.Contains(id))
        {
            OverworldSession.UsedCells.Add(id);
            Pickup(OverworldPickupBag.Of(map.name, OverworldSession.Day, OverworldPickupBag.IndexOf(map, cx, cy), OverworldMap.Find(map, '?').Count)); // S232：洗牌袋（早上已公布）
        }
        // S220：补心 +（少了心才捡，每天每个一次）、能量 *（只有你捡）
        if (map.At(cx, cy) == '+' && !OverworldSession.UsedCells.Contains(id) && OverworldSession.YouHearts < OverworldSession.MaxHearts)
        { OverworldSession.UsedCells.Add(id); OverworldSession.YouHearts++; hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 3 }); Hint(Note.Heal); }
        if (map.At(cx, cy) == '*' && !OverworldSession.UsedCells.Contains(id) && OverworldSession.Energy < OverworldSession.MaxEnergy)
        { OverworldSession.UsedCells.Add(id); GainEnergy(); }
        if (i.weather) TryCloud();
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
            if (outcome == OverworldMind.DoorOutcome.Ambush && !AmbushReady)
            {
                if (Spotted) { Hint(Note.Spotted); return true; }
                if (!ambushArmed) { ambushArmed = true; Hint(Note.AmbushArmed, 3f); } // S224：预约埋伏（不用一直按 E）
                return true;
            }
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
        flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = lx, ty = ly, dur = FlightSeconds, mario = true, ride = true, tampered = ride.tampered, byYou = ride.tampered });
        var muzzle = OverworldProps.MuzzleCells(map, ride.k, ride.dir);
        if (!youFlying && seat == null && muzzle.Exists(q => q.x == (int)System.Math.Floor(tx) && q.y == (int)System.Math.Floor(ty))) { flights.Add(new Flight { fx = tx, fy = ty, tx = lx + 0.01, ty = ly, dur = FlightSeconds, you = true, hurt = true }); disguised = false; }
        flights.Add(new Flight { fx = ride.k.x + 0.5, fy = ride.k.y + 0.5, tx = lx, ty = ly, dur = FlightSeconds, shell = true, depth = 1, byYou = ride.tampered });
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
        var why = sees && (!(disguised || seat != null) || lastMoved) ? OverworldMap.WhySeen(map, lamps, mario.x, mario.y, tx, ty, r, disguised || seat != null) : OverworldMap.SeenWhy.None;
        var p = new OverworldPercept
        {
            marioPos = new Vector2((float)mario.x, (float)mario.y),
            seesFigure = sees,
            figurePos = new Vector2((float)tx, (float)ty),
            figureLooksLikeProp = disguised || seat != null, // S219：坐在炮里 = 他只看见一门炮；炮管转动 = 可疑（和伪装同一条规则 H6）
            figureMoving = lastMoved || aimedThisFrame,
            sawRustle = !blind && !sees && lastMoved && OverworldMap.SeesRustle(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r),
            rustlePos = new Vector2((float)tx, (float)ty),
            heardTaunt = !blind && tauntedThisFrame && Dist(mario.x, mario.y, tx, ty) <= Step1Readability.TownTauntRadius(tuning), // S224：圈和判定同一个数
            tauntPos = new Vector2((float)tx, (float)ty),
            scheduleTarget = blind ? (Vector2?)null : schedule,
            heardNoise = !blind && heardNoise.HasValue, noisePos = heardNoise ?? default, noiseSuspicion = tuning.overworldNoiseSuspicion,
            bigStun = pendingMarioStun,
        };
        heardNoise = null; heardBell = false; pendingMarioStun = 0f;
        int mid = (int)System.Math.Floor(mario.y) * map.W + (int)System.Math.Floor(mario.x);
        if (peels.TryGetValue(mid, out var peel) && peel.x <= 0f) { p.slipped = true; peels.Remove(mid); OverworldSession.UsedCells.Add(mid); hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 1 }); }
        if (map.At(mid % map.W, mid / map.W) == '+' && !OverworldSession.UsedCells.Contains(mid) && OverworldSession.MarioHearts < OverworldSession.MaxHearts) // S220：他路过补心就顺手捡（不绕路）
        { OverworldSession.UsedCells.Add(mid); OverworldSession.MarioHearts++; hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 3 }); Hint(Note.MarioHeal); }

        if (why == OverworldMap.SeenWhy.None && p.sawRustle) why = OverworldMap.SeenWhy.Rustle;
        if (why == OverworldMap.SeenWhy.None && p.heardTaunt) why = OverworldMap.SeenWhy.Taunt;
        bool wasCalm = mind.Meter.Level == SuspicionLevel.Calm;
        var o = mind.Tick(dt, p);
        if (why != OverworldMap.SeenWhy.None && (wasCalm || seenWhy == OverworldMap.SeenWhy.None)) seenWhy = why; // S222：记"他是怎么注意到你的"（从平静变起疑那一刻）
        if (mind.Meter.Level == SuspicionLevel.Calm && !Spotted) seenWhy = OverworldMap.SeenWhy.None;
        if (blind && string.IsNullOrEmpty(o.mark)) o.mark = "…";
        if (o.state == OverworldMarioState.Curious) FaceToward(mind.Focus); // S228：'?' 时转头看声音 / 可疑的地方（房间里的马里奥本来就会；小镇以前只停不转 → 钟声没用）
        lastOrder = o;
        // S218：他吃过亏的大机关在预警 / 正在滚，而且他看得见 → 先闪开（只躲几格、只在危险期间，H10 不会停住）
        var dodge = (o.state == OverworldMarioState.Walking || o.state == OverworldMarioState.Waiting || o.state == OverworldMarioState.Curious) ? DodgeCell(r) : null;
        // S221：被劈过的他看见雷云挡在要走的路上 → 在云外等它散（最多 overworldCloudSeconds 秒，H10）。以前他只闪一步就穿过去，雷云对"学会了"的他毫无用处
        if (!dodge.HasValue && o.state == OverworldMarioState.Walking && StormBlocksRoute(r, goalCell))
        {
            o.target = null; o.mark = "WAIT"; o.intent = "WAIT STORM"; lastOrder = o; mind.AddDelay(dt); // H6：头上写 WAIT（和出门缓冲的 "…" 区分开）
            return;
        }
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
    public bool Arm(OverworldMap.Cell c, int depth, double fromX, double fromY, bool byYou = true)
    {
        int id = c.y * map.W + c.x; char k = map.At(c.x, c.y);
        bool ok = OverworldProps.IsBig(k) || (k == 'i' && weather.kind == OverworldEvents.Kind.Storm) || (k == '^' && OverworldProps.MudDir(map, c) >= 0);
        if (!ok || OverworldSession.UsedCells.Contains(id)) return false;
        if (k == 'K' && ((seat != null && seat.k.Equals(c)) || (ride != null && ride.seated && ride.k.Equals(c)))) return false; // 有人坐着，由坐的人发射
        var b = new Big { c = c, kind = k, depth = depth, byYou = byYou, fuse = tuning.overworldBigFuseSeconds, fuseTotal = tuning.overworldBigFuseSeconds };
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

    /// <summary>S228：转头（只改朝向，不动位置）。朝向决定视锥（OverworldMap.CanSee）。</summary>
    private void FaceToward(Vector2 p)
    {
        double dx = p.x - mario.x, dy = p.y - mario.y, d = System.Math.Sqrt(dx * dx + dy * dy);
        if (d > 0.3) { mario.fx = dx / d; mario.fy = dy / d; }
    }

    private void Change(int x, int y, char c)
    {
        int id = y * map.W + x; OverworldMap.Set(map, x, y, c); OverworldSession.Changed[id] = c; changedCells.Add(id);
    }

    private void Noise(double x, double y) => Noise(x, y, Step1Readability.TownNoiseRadius(tuning), false);

    private void Noise(double x, double y, float radius, bool bell)
    {
        if (!marioInside && Dist(x, y, mario.x, mario.y) <= radius)
        {
            if (bell && OverworldSession.MarioWary.Contains('B')) Hint(Note.BellIgnored, 2.5f); // 他上过钟楼的当：不再停下（只凭自己的经历，H4）
            else { heardNoise = new Vector2((float)x, (float)y); if (bell) { heardBell = true; bellHeardMinute = OverworldSession.Minute; } }
        }
        sounds.Add(new Impact3((float)x, (float)y, radius));
        if (!bell) impacts.Add(new Impact3((float)x, (float)y, 1.5f));
    }

    /// <summary>S220：马里奥被闪电 / 滚石 / 泥石流 / 被迫轰飞 打中：晕 2 秒 + 掉 1 颗心。掉光 = S229 起这一天结束（Die）；关掉开关 = 晕倒 3 秒、剩 1 颗。
    /// S221：保护期 = 晕的时间 + 站起来后 overworldHurtGraceSeconds 秒；保护期里什么都打不到他（不掉心、也不再晕）——
    /// 以前保护期在晕的时候就倒计时、而且照样晕 → 雷云里他 9 秒晕 8 秒（连控）。参考：塞尔达 / 马里奥受伤闪烁全无敌、格斗游戏起身无敌、DbD 挨打后加速逃开。</summary>
    /// <summary>S233：最近一次大机关砸中马里奥（位置 + 种类；序号变了 = 新的一下）。只给"居民当场喊一句"用，马里奥 AI 不读（H4）。</summary>
    public int hitSerial; public float hitX, hitY; public char hitKind = ' ';
    private void HitMario(char kind, bool byYou = true)
    {
        hitSerial++; hitX = (float)mario.x; hitY = (float)mario.y; hitKind = kind;
        OverworldSession.MarioWary.Add(kind); OverworldSession.BigHits++; OverworldSession.LastBigHitMinute = OverworldSession.Minute;
        if (BellFooled(OverworldSession.Minute, bellHeardMinute, tuning.overworldMinutesPerSecond)) OverworldSession.MarioWary.Add('B'); // S228：钟一响就挨砸 = 他记住"钟声是圈套"
        Hint(Note.BigHit, 2f);
        if (marioGrace > 0f) { hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 1 }); return; } // 闪烁中 = 全无敌（画面在闪，H6）
        float stun = Mathf.Min(tuning.maxStunSeconds, tuning.overworldBigStunSeconds);
        OverworldSession.MarioHearts--; OverworldSession.MarioHeartsLost++;
        hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 0 });
        if (byYou) GainEnergy();
        if (OverworldSession.MarioHearts <= 0 && tuning.overworldDeathEndsDay)
        { OverworldSession.MarioHearts = 0; marioDies = true; marioDeathKind = kind; marioDeathByYou = byYou; pendingMarioStun = Mathf.Max(pendingMarioStun, stun); return; } // S229：这一天结束（Tick 最后统一结算，同一下两人都死 = 平局）
        if (OverworldSession.MarioHearts <= 0)
        {
            OverworldSession.MarioHearts = 1; OverworldSession.Kos++;
            stun = Mathf.Min(tuning.maxStunSeconds, Mathf.Max(stun, tuning.overworldKoSeconds));
            Hint(Note.MarioKO, 2.5f);
        }
        pendingMarioStun = Mathf.Max(pendingMarioStun, stun);
        marioGrace = GraceAfter(stun);
    }

    /// <summary>S228 纯逻辑：听见钟后 BellLearnSeconds 秒（现实秒，按游戏分钟换算）内挨砸 = 上当。</summary>
    public static bool BellFooled(double nowMinute, double bellMinute, float minutesPerSecond) => nowMinute - bellMinute >= 0 && nowMinute - bellMinute <= BellLearnSeconds * minutesPerSecond;

    /// <summary>S221：受伤保护期 = 晕多久 + 站起来后还护多久（站起来那一刻一定有时间跑开）。</summary>
    public float GraceAfter(float stun) => stun + tuning.overworldHurtGraceSeconds;

    /// <summary>S220：你被自己 / 天灾打中：晕 2 秒 + 掉 1 颗心。掉光 = S229 起这一天结束（Die）；关掉开关 = 晕倒 3 秒、剩 1 颗（不回出生点、不算被抓）。S221：保护期同上（闪烁中全无敌）。</summary>
    private void HurtYou() => HurtYouBy('i');
    /// <summary>S229：带上"被什么打中"（结算里说死因）。</summary>
    private void HurtYouBy(char kind)
    {
        if (youGrace > 0f) { hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 1 }); return; }
        float stun = tuning.overworldBigStunSeconds;
        disguised = false;
        OverworldSession.YouHearts--; OverworldSession.YouHeartsLost++;
        hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 0 });
        if (OverworldSession.YouHearts <= 0 && tuning.overworldDeathEndsDay)
        { OverworldSession.YouHearts = 0; youDies = true; youDeathKind = kind; frozen = Mathf.Max(frozen, stun); return; } // S229：这一天结束（Tick 最后统一结算）
        if (OverworldSession.YouHearts <= 0) { OverworldSession.YouHearts = 1; OverworldSession.Kos++; stun = Mathf.Max(stun, tuning.overworldKoSeconds); Hint(Note.YouKO, 2.5f); }
        else Hint(Note.YouHurt, 2f);
        frozen = Mathf.Max(frozen, stun);
        youGrace = GraceAfter(stun);
    }

    /// <summary>S232：道具箱的效果（上限都不变：炸弹 ≤ MaxBonusBombs、心 ≤ 3、能量 ≤ 3）。满了就提示"浪费了"——你早上就知道里面是什么。</summary>
    private void Pickup(OverworldPickupBag.Kind k)
    {
        switch (k)
        {
            case OverworldPickupBag.Kind.Energy:
                if (OverworldSession.Energy >= OverworldSession.MaxEnergy) Hint(Note.PickupWasted); else GainEnergy(); break;
            case OverworldPickupBag.Kind.Taunt:
                OverworldSession.TauntsUsed--; Hint(Note.PickupTaunt); break;
            case OverworldPickupBag.Kind.Heart:
                if (OverworldSession.YouHearts >= OverworldSession.MaxHearts) Hint(Note.PickupWasted);
                else { OverworldSession.YouHearts++; hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 3 }); Hint(Note.PickupHeart); }
                break;
            default:
                if (OverworldSession.BonusBombs >= MaxBonusBombs) Hint(Note.PickupWasted);
                else { OverworldSession.BonusBombs = Mathf.Min(MaxBonusBombs, OverworldSession.BonusBombs + 1); Hint(Note.Pickup); }
                break;
        }
    }

    private void GainEnergy()
    {
        if (OverworldSession.Energy >= OverworldSession.MaxEnergy) return;
        OverworldSession.Energy++;
        hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 4 });
        Hint(OverworldSession.Energy >= OverworldSession.MaxEnergy ? Note.EnergyFull : Note.EnergyUp, 2f);
    }

    private void Impact(double x, double y, int depth, OverworldMap.Cell self, bool byYou = true, bool noise = true)
    {
        if (noise) Noise(x, y); else impacts.Add(new Impact3((float)x, (float)y, 1.5f));
        foreach (var t in OverworldProps.ChainTargets(map, x, y)) if (!t.Equals(self)) Arm(t, depth + 1, x, y, byYou);
        if (OverworldEvents.Wet(weather)) foreach (var t in OverworldProps.MudSources(map, x, y)) if (!t.Equals(self)) Arm(t, depth + 1, x, y, byYou); // S219：下雨天震到山丘 = 泥石流
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
            if (f.mario && f.window) // S228：房间里挨了你的炮 → 被轰出窗户：落地晕、不掉心（房间刚打完回满），冲击照样连锁
            {
                mario.x = f.tx; mario.y = f.ty; mario.Clear();
                float st = Mathf.Min(tuning.maxStunSeconds, tuning.overworldBigStunSeconds);
                pendingMarioStun = Mathf.Max(pendingMarioStun, st); marioGrace = GraceAfter(st); OverworldSession.LastBigHitMinute = OverworldSession.Minute;
                hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 1 });
                Impact(f.tx, f.ty, 1, new OverworldMap.Cell(-1, -1), true);
                continue;
            }
            if (f.mario) { mario.x = f.tx; mario.y = f.ty; mario.Clear(); if (!f.ride || f.tampered) HitMario('K', f.byYou); } // S219：他自己坐炮、没被你拨歪 = 平稳落地
            if (f.you) { tx = f.tx; ty = f.ty; frozen = Mathf.Max(frozen, 0.4f); if (seat != null) seat = null; if (f.hurt) HurtYouBy('K'); } // S220：自己坐炮不疼；站在炮口被轰 = 掉心
            if (f.shell) Impact(f.tx, f.ty, f.depth, new OverworldMap.Cell(-1, -1), f.byYou);
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
            if (!b.hitMario && !marioInside && !marioFlying && Dist(sx, sy, mario.x, mario.y) < 0.8f) { b.hitMario = true; HitMario('O', b.byYou); }
            if (!b.hitYou && !youFlying && Dist(sx, sy, tx, ty) < 0.8f) { b.hitYou = true; HurtYouBy('O'); Hint(Note.BigSelf, 2f); }
            if (b.rolled >= b.lane.Count) { active.RemoveAt(i); var end = b.lane.Count > 0 ? b.lane[b.lane.Count - 1] : b.c; Impact(end.x + 0.5, end.y + 0.5, b.depth, b.c, b.byYou); }
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
            if (!marioInside && !marioFlying && In(mario.x, mario.y)) flights.Add(new Flight { fx = mario.x, fy = mario.y, tx = lx, ty = ly, dur = FlightSeconds, mario = true, hurt = true, byYou = b.byYou });
            if (!youFlying && (b.rider == 1 || (seat == null && In(tx, ty)))) { flights.Add(new Flight { fx = tx, fy = ty, tx = lx + 0.01, ty = ly, dur = FlightSeconds, you = true, hurt = b.rider != 1 }); disguised = false; if (b.rider == 1) OverworldSession.CannonRides++; }
            flights.Add(new Flight { fx = cx, fy = cy, tx = lx, ty = ly, dur = FlightSeconds, shell = true, depth = b.depth, byYou = b.byYou });
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
        if (b.kind == 'B') { Noise(cx, cy, Step1Readability.TownBellRadius(tuning), true); OverworldSession.BellRings++; impacts.Add(new Impact3((float)cx, (float)cy, 0.8f)); if (hint == Note.None || hint == Note.BigArmed) Hint(Note.Bell, 2.5f); return false; } // S228 钟楼：只有声音，不伤人、不改地形、不连锁
        if (b.kind == '^') return FireMud(b);
        int rad = OverworldProps.FloodRadius + (weather.kind == OverworldEvents.Kind.Rain ? 1 : 0);
        // S219 山洪：水塔淹到的山丘（泥石流源头）也冲下来（不管天气）
        foreach (var hc in OverworldMap.Find(map, '^')) if (OverworldProps.MudDir(map, hc) >= 0 && (hc.x - b.c.x) * (hc.x - b.c.x) + (hc.y - b.c.y) * (hc.y - b.c.y) <= rad * rad) Arm(hc, b.depth + 1, cx, cy, b.byYou);
        foreach (var c in OverworldProps.Flood(map, b.c, rad))
        {
            Change(c.x, c.y, 'g');
            if (!marioInside && (int)System.Math.Floor(mario.x) == c.x && (int)System.Math.Floor(mario.y) == c.y) hurts.Add(new HurtFx { x = (float)mario.x, y = (float)mario.y, mario = true, kind = 2 }); // S220：水淹只变慢（蓝水滴），不掉心
            if ((int)System.Math.Floor(tx) == c.x && (int)System.Math.Floor(ty) == c.y) hurts.Add(new HurtFx { x = (float)tx, y = (float)ty, kind = 2 });
        }
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
        if (!marioInside && !marioFlying && !MarioSeated && Dist(cx, cy, mario.x, mario.y) <= r) HitMario('i', b.byYou);
        if (!youFlying && seat == null && Dist(cx, cy, tx, ty) <= r) { HurtYouBy('i'); Hint(Note.BigSelf, 2f); }
        OverworldSession.Lightnings++; bolts.Add(new Impact3((float)cx, (float)cy, 1f));
        Impact(cx, cy, b.depth, b.c, b.byYou); impacts.Add(new Impact3((float)cx, (float)cy, (float)OverworldProps.LightningRadius));
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
            if (!marioInside && !marioFlying && !MarioSeated && (int)System.Math.Floor(mario.x) == q.x && (int)System.Math.Floor(mario.y) == q.y) HitMario('^', b.byYou);
            if (!youFlying && seat == null && (int)System.Math.Floor(tx) == q.x && (int)System.Math.Floor(ty) == q.y) { HurtYouBy('^'); Hint(Note.BigSelf, 2f); }
            if (OverworldProps.Muddable(map.At(q.x, q.y))) Change(q.x, q.y, 'g'); impacts.Add(new Impact3(q.x + 0.5f, q.y + 0.5f, 0.6f)); // 门 / 家 / 山洞 / 靶心 冲过但不改
        }
        OverworldSession.Mudslides++;
        var end = lane.Count > 0 ? lane[lane.Count - 1] : b.c;
        Impact(end.x + 0.5, end.y + 0.5, b.depth, b.c, b.byYou);
        if (hint == Note.None || hint == Note.BigArmed) Hint(Note.Mudslide, 2f);
        return false;
    }

    // ═════════ S220：雷区 + 雷云 ═════════
    /// <summary>附近 range 格内有闪电预警 / 雷云 → 不许快进（快进会让人来不及看预警）。</summary>
    public bool StormNear(double range)
    {
        foreach (var s in strikes) if (Dist(s.c.x + 0.5, s.c.y + 0.5, mario.x, mario.y) <= range || Dist(s.c.x + 0.5, s.c.y + 0.5, tx, ty) <= range) return true;
        return false;
    }

    private void TickStorms(float dt)
    {
        for (int z = 0; z < stormLast.Length && z < map.storms.Count; z++)
        {
            int idx = OverworldStorm.VolleyIndex(OverworldSession.Minute, tuning.overworldStormVolleySeconds, tuning.overworldMinutesPerSecond, z);
            if (idx <= stormLast[z]) continue;
            stormLast[z] = idx;
            if (!OverworldStorm.ActiveOn(map.storms[z], weather)) continue;
            foreach (var c in OverworldStorm.Volley(stormMap, z, OverworldSession.Day, idx))
                strikes.Add(new Strike { c = c, t = tuning.overworldBoltTelegraphSeconds, total = tuning.overworldBoltTelegraphSeconds, zone = z, byYou = false });
        }
        if (cloud != null)
        {
            cloud.t += dt;
            if (cloud.t >= cloud.next && cloud.t < tuning.overworldCloudSeconds) { CloudVolley(); cloud.next += tuning.overworldCloudVolleySeconds; }
            if (cloud.t >= tuning.overworldCloudSeconds && !strikes.Exists(s => s.zone < 0)) cloud = null;
        }
        for (int i = strikes.Count - 1; i >= 0; i--)
        {
            var s = strikes[i]; s.t -= dt;
            if (s.t > 0f) continue;
            strikes.RemoveAt(i);
            FireStrike(s);
        }
    }

    private void FireStrike(Strike s)
    {
        double cx = s.c.x + 0.5, cy = s.c.y + 0.5;
        if (!marioInside && !marioFlying && !MarioSeated && OverworldStorm.InPlus(s.c, mario.x, mario.y)) HitMario('i', s.byYou);
        if (!youFlying && seat == null && OverworldStorm.InPlus(s.c, tx, ty)) HurtYouBy('i');
        OverworldSession.Lightnings++; OverworldSession.StormBolts++;
        bolts.Add(new Impact3((float)cx, (float)cy, 1f)); impacts.Add(new Impact3((float)cx, (float)cy, 1f));
        if (s.zone < 0) Impact(cx, cy, 1, new OverworldMap.Cell(-1, -1), s.byYou, false); // 雷云：完整冲击链（但只在召唤时响一声）
        else if (OverworldEvents.Wet(weather)) foreach (var t in OverworldProps.MudSources(map, cx, cy)) Arm(t, 2, cx, cy, false); // 雷区：不出声、不连锁；下雨天会引发泥石流
        if (hint == Note.None) Hint(Note.StormBolt, 1.5f);
    }

    /// <summary>Q：能量满 → 头顶召唤一朵雷云（停在这里 9 秒，每 2.5 秒劈 2 道：一道冲着云里的马里奥，一道随便劈——可能劈到你自己）。</summary>
    private void TryCloud()
    {
        if (cloud != null) return;
        if (OverworldSession.Energy < OverworldSession.MaxEnergy) { Hint(Note.CloudLow); return; }
        OverworldSession.Energy = 0; OverworldSession.Clouds++;
        cloud = new Cloud { x = System.Math.Floor(tx) + 0.5, y = System.Math.Floor(ty) + 0.5, t = 0f, next = 1f, id = OverworldSession.Clouds };
        Noise(tx, ty); // 一声闷雷：他会过来看
        Hint(Note.Cloud, 2.5f);
    }

    public List<OverworldMap.Cell> CloudCells()
    {
        var l = new List<OverworldMap.Cell>(); if (cloud == null) return l;
        int r = (int)System.Math.Ceiling(tuning.overworldCloudRadius);
        int x0 = (int)System.Math.Floor(cloud.x), y0 = (int)System.Math.Floor(cloud.y);
        for (int y = y0 - r; y <= y0 + r; y++) for (int x = x0 - r; x <= x0 + r; x++)
            if (OverworldMap.Walkable(map, x, y) && Dist(x + 0.5, y + 0.5, cloud.x, cloud.y) <= tuning.overworldCloudRadius + 1e-6) l.Add(new OverworldMap.Cell(x, y));
        return l;
    }

    private void CloudVolley()
    {
        var cells = CloudCells(); if (cells.Count == 0) return;
        uint h = OverworldStorm.Seed(map.name, OverworldSession.Day, 100 + cloud.id, cloud.volley++);
        var picks = new List<OverworldMap.Cell>();
        if (!marioInside && Dist(mario.x, mario.y, cloud.x, cloud.y) <= tuning.overworldCloudRadius) picks.Add(OverworldGuide.Near(map, mario.x, mario.y));
        while (picks.Count < 2 && cells.Count > 0) { h = OverworldStorm.Next(h); int i = (int)(h % (uint)cells.Count); if (!picks.Contains(cells[i])) picks.Add(cells[i]); cells.RemoveAt(i); }
        foreach (var c in picks) strikes.Add(new Strike { c = c, t = tuning.overworldBoltTelegraphSeconds, total = tuning.overworldBoltTelegraphSeconds, zone = -1, byYou = true });
    }

    /// <summary>S221：他吃过闪电的亏、看得见雷云（云很大：在视野里或 overworldVisionRange 内）、他接下来 8 格路要穿过云 → true（他在云外等）。
    /// H4：只用他看得见的云 + 自己的经历；云最多 overworldCloudSeconds 秒就散（H10 不会停住）。他已经在云里 → 交给 DodgeCell 往外躲。</summary>
    public bool StormBlocksRoute(OverworldMap.SightRules r, OverworldMap.Cell goal)
    {
        if (cloud == null || marioInside || !OverworldSession.MarioWary.Contains('i')) return false;
        double d = Dist(mario.x, mario.y, cloud.x, cloud.y), rad = tuning.overworldCloudRadius;
        if (d <= rad + 0.2 || d > rad + 6) return false;
        if (!OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, cloud.x, cloud.y, r) && d > r.range) return false;
        var path = OverworldMap.Path(map, OverworldGuide.Near(map, mario.x, mario.y), goal);
        if (path == null) return false;
        for (int k = 1; k < path.Count && k <= 8; k++) if (Dist(path[k].x + 0.5, path[k].y + 0.5, cloud.x, cloud.y) <= rad + 0.6) return true;
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
        if (OverworldSession.MarioWary.Contains('i')) // S220：被劈过一次 → 看见地上闪电预警就躲（只躲看得见的）
            foreach (var s in strikes)
            {
                double px = s.c.x + 0.5, py = s.c.y + 0.5;
                if (!OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, px, py, r) && Dist(px, py, mario.x, mario.y) > 2.5) continue;
                foreach (var c in OverworldStorm.Plus(map, s.c)) danger.Add((c.x, c.y));
            }
        if (cloud != null && OverworldSession.MarioWary.Contains('i')) foreach (var c in CloudCells()) danger.Add((c.x, c.y)); // S221：吃过亏 = 整朵云都危险（往云外躲）
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
        caughtWhy = seenWhy == OverworldMap.SeenWhy.None ? OverworldMap.SeenWhy.Open : seenWhy;
        OverworldSession.Caught++;
        tx = spawn.x + 0.5; ty = spawn.y + 0.5; disguised = false;
        frozen = tuning.overworldCaughtPenaltySeconds; ambushArmed = false;
        OverworldSession.NearMiss.Caught(OverworldMap.Clock(OverworldSession.Minute), caughtWhy.ToString()); // S224：这段起疑算"被抓"，不算差点
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
