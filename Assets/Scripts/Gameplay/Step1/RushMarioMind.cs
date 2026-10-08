using System;
using UnityEngine;

public enum MarioMindState { Running, Curious, Investigating, Chasing, Searching }

/// <summary>S225：他这次起疑的原因（只来自 MarioPercept：自己看见/听见/挨打，H4）。只用来换头顶台词。</summary>
public enum SuspicionCause { None, SawYou, OddProp, SawTrap, Rustle, Taunt, Hurt }

/// <summary>马里奥这一帧"知道"的全部信息。只能由 MarioEyes / 自身状态填写（H4）。</summary>
public struct MarioPercept
{
    public Vector2 marioPos;
    public bool seesFigure;
    public Vector2 figurePos;
    public bool figureLooksLikeProp;
    /// <summary>S199：全场警报中（公开的环境状态，不是捣蛋者信息）。警报时"静止的伪装"也会慢慢引起怀疑。</summary>
    public bool alarm;
    public bool figureMoving;
    /// <summary>S186：看得见时，它的移动速度（由两帧所见位置算出，看不见时为 0）。</summary>
    public Vector2 figureVelocity;
    public bool witnessedActivation;
    public Vector2 activationPos;
    public bool hurt;
    public bool scanReady;
    public bool carryingLoot;
    /// <summary>S187：看见草丛晃了（只知道位置，不知道是风还是人）。</summary>
    public bool sawRustle;
    public Vector2 rustlePos;
    /// <summary>S202：看得见的危险（正在冒烟的炸弹/油桶：公开的场景物，谁都看得见，H4 合规）。dangerRadius=0 表示没有。</summary>
    public Vector2 dangerPos;
    public float dangerRadius;
    /// <summary>S202：看得见的道具箱（亮着的问号方块）。</summary>
    public bool seesPickup;
    public Vector2 pickupPos;
    /// <summary>S200：听见挑衅（只有位置，H4）。</summary>
    public bool heardTaunt;
    public Vector2 tauntPos;
    /// <summary>S187：马里奥自己的朝向（自身状态）。</summary>
    public bool facingRight;
}

public struct MarioOrder
{
    public MarioMindState state;
    public Vector2? moveTarget;
    public bool scan;
    public bool tryCatch;
    /// <summary>S197：追人状态下此刻亲眼看得见（马里奥自己的感知，H4）。时间静止等技能用。</summary>
    public bool seesQuarry;
    public string mark;
    public string intent;
    /// <summary>S225：这次起疑的原因 + 本局第几次因为它起疑（头顶分层台词用，纯表现）。</summary>
    public SuspicionCause cause;
    public int causeTimes;
}

/// <summary>
/// 设计宪法第 1 步：冲冲型马里奥的"心智"。纯逻辑状态机，只吃 MarioPercept，不接触捣蛋者对象。
/// Running（拿宝/回家）→ Curious '?'（停下看）→ Investigating '!'（走过去扫描）
///                                           → Chasing '!!'（看见本体，追）→ Searching '?!'（跟丢，去最后位置找）→ Running
/// 移动仍由现有 HeuristicBot 执行，本类只给出目标点与是否扫描/抓捕。
/// </summary>
public sealed class RushMarioMind
{
    private readonly MarioMindTuningSO t;
    private float stateTime, lostTime, hurtFlash, celebrate, postScan, stun, glance;
    private System.Random dice = new System.Random(0);
    private bool scannedHere;
    private Vector2 lookPoint, lastSeen, lastSeenVelocity, glanceTarget;

    public SuspicionMeter Meter { get; }
    // S200 学习层：在哪里被坑过（他自己的记忆，只有位置）。赶路经过这些地方会放慢、小心；追人时气昏头，不会小心。
    private readonly System.Collections.Generic.List<Vector2> hurtSpots = new System.Collections.Generic.List<Vector2>();
    public System.Collections.Generic.IReadOnlyList<Vector2> HurtSpots => hurtSpots;
    public bool Cautious { get; private set; }
    public const int MaxHurtSpots = 6;

    /// <summary>纯逻辑：前方（面向）cautiousRadius 格内有被坑过的地方 → 小心。</summary>
    public static bool NearHurtSpot(System.Collections.Generic.IReadOnlyList<Vector2> spots, Vector2 mario, bool facingRight, float radius)
    {
        foreach (var s in spots)
        {
            float dx = s.x - mario.x;
            if (Mathf.Abs(s.y - mario.y) > 1.5f) continue;
            if ((facingRight ? dx : -dx) >= -0.3f && Mathf.Abs(dx) <= radius) return true;
        }
        return false;
    }

    /// <summary>S202 纯逻辑：危险在前方且自己在爆炸圈（+安全边）内 → 该退到哪里（圈外）；不用退返回 null。</summary>
    public static Vector2? DodgeTarget(Vector2 mario, Vector2 danger, float radius, float margin)
    {
        if (radius <= 0f) return null;
        float dx = mario.x - danger.x;
        if (Mathf.Abs(mario.y - danger.y) > radius + 0.5f || Mathf.Abs(dx) > radius + margin) return null;
        float side = Mathf.Abs(dx) < 0.05f ? 1f : Mathf.Sign(dx);
        return new Vector2(danger.x + side * (radius + margin + 0.5f), mario.y);
    }

    /// <summary>S202 纯逻辑：道具箱值不值得绕过去（同层、离得近、不往回走太多）。</summary>
    public static bool WorthPickup(Vector2 mario, Vector2 pickup, float maxDetour)
        => Mathf.Abs(pickup.y - mario.y) <= 1.5f && Mathf.Abs(pickup.x - mario.x) <= maxDetour;

    /// <summary>S203 纯逻辑：这种性格要不要去捡（贪财型：跨层也去、距离放宽）。</summary>
    public static bool WantsPickup(MarioPersonality.Traits tr, Vector2 mario, Vector2 pickup)
    {
        if (tr.pickupDetour < 0f) return false;
        if (tr.pickupAnyFloor) return Vector2.Distance(mario, pickup) <= tr.pickupDetour;
        return WorthPickup(mario, pickup, tr.pickupDetour);
    }

    public bool Dodging { get; private set; }
    /// <summary>S203：本回合性格（开局按种子抽，见 MarioPersonality）。</summary>
    public MarioPersonalityKind Personality { get; private set; } = MarioPersonalityKind.Rush;
    public MarioPersonality.Traits Traits { get; private set; }
    /// <summary>S203：强制性格（测试 / 调参面板"固定性格"用）。null = 按权重随机。</summary>
    public MarioPersonalityKind? ForcedPersonality { get; set; }
    private float introTimer;
    public bool ShowingPersonality => introTimer > 0f;
    // S202：捡道具放弃机制（够不着的道具箱不会让他一直原地走，H10）
    private float grabTime; private Vector2 grabTarget; private readonly System.Collections.Generic.List<Vector2> skippedPickups = new System.Collections.Generic.List<Vector2>();
    public const float GrabGiveUpSeconds = 3f;
    /// <summary>纯逻辑：这个道具箱是不是已经放弃了。</summary>
    public static bool Skipped(System.Collections.Generic.IReadOnlyList<Vector2> skipped, Vector2 at) { foreach (var s in skipped) if ((s - at).sqrMagnitude < 0.25f) return true; return false; }

    public void RememberHurt(Vector2 at)
    {
        foreach (var s in hurtSpots) if ((s - at).sqrMagnitude < 1f) return;
        hurtSpots.Add(at);
        if (hurtSpots.Count > MaxHurtSpots) hurtSpots.RemoveAt(0);
    }
    public MarioMindState State { get; private set; } = MarioMindState.Running;
    public Vector2 Focus { get; private set; }
    /// <summary>S225：最近一次让他起疑的原因（只来自感知）+ 本局每种原因的次数（进入 ? 时 +1）。</summary>
    public SuspicionCause Cause { get; private set; }
    private readonly int[] causeTimes = new int[7];
    public int CauseTimes(SuspicionCause c) => causeTimes[(int)c];
    /// <summary>S225 纯逻辑：这一帧的感知里最"具体"的原因（看见你 > 挨打 > 看见机关动 > 东西在动 > 挑衅 > 草晃）。</summary>
    public static SuspicionCause StrongestCause(bool seesTrickster, bool seesOddProp, bool witnessed, bool rustle, bool taunt, bool hurt)
    {
        if (seesTrickster) return SuspicionCause.SawYou;
        if (hurt) return SuspicionCause.Hurt;
        if (witnessed) return SuspicionCause.SawTrap;
        if (seesOddProp) return SuspicionCause.OddProp;
        if (taunt) return SuspicionCause.Taunt;
        if (rustle) return SuspicionCause.Rustle;
        return SuspicionCause.None;
    }
    public event Action<MarioMindState, MarioMindState> StateChanged;

    public RushMarioMind(MarioMindTuningSO tuning) { t = tuning; Meter = new SuspicionMeter(tuning); Traits = MarioPersonality.For(MarioPersonalityKind.Rush, tuning); }

    /// <summary>S187：本回合的随机种子（回头看等性格随机都由它决定，可复现；写入试玩记录）。</summary>
    public int Seed { get; private set; }

    public void Reset() => Reset(Seed);

    public void Reset(int seed)
    {
        Seed = seed; dice = new System.Random(seed);
        Meter.Reset(); hurtFlash = celebrate = stun = glance = 0f;
        System.Array.Clear(causeTimes, 0, causeTimes.Length); Cause = SuspicionCause.None;
        hurtSpots.Clear(); Cautious = false; skippedPickups.Clear(); grabTime = 0f; Dodging = false;
        Personality = ForcedPersonality ?? (t.ForcedPersonalityIndex >= 0 ? (MarioPersonalityKind)t.ForcedPersonalityIndex : MarioPersonality.Roll(seed, t.rushWeight, t.cautiousWeight, t.greedyWeight));
        Traits = MarioPersonality.For(Personality, t);
        introTimer = t.personalityIntroSeconds;
        Enter(MarioMindState.Running, Vector2.zero);
    }

    public void OnCaught()
    {
        Meter.Set(0f); celebrate = t.celebrateSeconds;
        Enter(MarioMindState.Running, Focus);
    }

    public MarioOrder Tick(float dt, MarioPercept p)
    {
        dt = Mathf.Max(0f, dt);
        stateTime += dt;
        hurtFlash = Mathf.Max(0f, hurtFlash - dt);
        celebrate = Mathf.Max(0f, celebrate - dt);
        stun = Mathf.Max(0f, stun - dt);
        glance = Mathf.Max(0f, glance - dt);
        introTimer = Mathf.Max(0f, introTimer - dt);

        bool seesTrickster = p.seesFigure && !p.figureLooksLikeProp;
        bool seesOddProp = p.seesFigure && p.figureLooksLikeProp && (p.figureMoving || p.alarm);
        float rise = 0f;
        if (seesTrickster) { rise += t.seeTricksterPerSecond; Focus = p.figurePos; }
        else if (seesOddProp) { rise += p.figureMoving ? t.seeDisguisedMovePerSecond : t.seeDisguisedMovePerSecond * t.alarmStillFactor; Focus = p.figurePos; }
        if (p.witnessedActivation) { Meter.Add(t.witnessedActivation); Focus = p.activationPos; }
        if (p.sawRustle)
        {
            Meter.Add(t.rustleSuspicion);
            if (!seesTrickster && !seesOddProp) Focus = p.rustlePos;
        }
        if (p.heardTaunt)
        {
            Meter.Add(t.tauntSuspicion);
            if (!seesTrickster) Focus = p.tauntPos;
        }
        if (p.hurt)
        {
            if (t.learnFromHurt) RememberHurt(p.marioPos);
            Meter.Add(t.hurtByTrap); hurtFlash = t.hurtFlashSeconds; stun = t.hurtStunSeconds;
            if (!seesTrickster && !seesOddProp && !p.witnessedActivation) Focus = p.marioPos;
        }
        var nowCause = StrongestCause(seesTrickster, seesOddProp, p.witnessedActivation, p.sawRustle, p.heardTaunt, p.hurt);
        if (nowCause != SuspicionCause.None && (State == MarioMindState.Running || State == MarioMindState.Curious || nowCause == SuspicionCause.SawYou)) Cause = nowCause;
        bool greedyGrab = Personality == MarioPersonalityKind.Greedy && grabTime > 0f && p.seesPickup;
        Meter.Tick(dt, rise * Traits.suspicionScale * (greedyGrab ? 0.5f : 1f));

        var order = new MarioOrder();
        switch (State)
        {
            case MarioMindState.Running:
                if (Meter.Level != SuspicionLevel.Calm)
                {
                    float side = Mathf.Sign(Focus.x - p.marioPos.x);
                    lookPoint = new Vector2(p.marioPos.x + side * t.lookStep, p.marioPos.y);
                    causeTimes[(int)Cause]++; // S225：本局第几次因为这个原因起疑（台词分层）
                    Enter(MarioMindState.Curious, Focus);
                }
                break;
            case MarioMindState.Curious:
                if (Meter.Level == SuspicionLevel.Alert) Enter(seesTrickster ? MarioMindState.Chasing : MarioMindState.Investigating, Focus);
                else if (Meter.Level == SuspicionLevel.Calm) Enter(MarioMindState.Running, Focus);
                break;
            case MarioMindState.Investigating:
                if (seesTrickster && Meter.Level == SuspicionLevel.Alert) Enter(MarioMindState.Chasing, Focus);
                else if (scannedHere && (postScan += dt) >= t.postScanSeconds) GiveUp();
                else if (stateTime > t.investigateTimeout) Enter(MarioMindState.Searching, Focus);
                break;
            case MarioMindState.Chasing:
                if (seesTrickster) { lastSeen = p.figurePos; lastSeenVelocity = p.figureVelocity; lostTime = 0f; }
                else
                {
                    lostTime += dt;
                    if (lostTime > t.chaseGiveUpSeconds || Vector2.Distance(p.marioPos, lastSeen) <= t.arriveDistance)
                        Enter(MarioMindState.Searching, lastSeen);
                }
                break;
            case MarioMindState.Searching:
                if (seesTrickster && Meter.Level == SuspicionLevel.Alert) Enter(MarioMindState.Chasing, Focus);
                else if (stateTime > t.searchSeconds) GiveUp();
                break;
        }

        order.state = State; order.cause = Cause; order.causeTimes = causeTimes[(int)Cause];
        if (State != MarioMindState.Running) { Cautious = false; Dodging = false; }
        switch (State)
        {
            case MarioMindState.Running:
                order.moveTarget = null;
                order.mark = celebrate > 0f ? "GOTCHA!" : hurtFlash > 0f ? "OUCH!" : "";
                order.intent = p.carryingLoot ? "RUN HOME" : "GET LOOT";
                // S187 性格随机：赶路时偶尔回头看一眼（概率与时长来自调参，种子可复现）
                if (glance <= 0f && dt > 0f && t.glanceChancePerSecond > 0f && dice.NextDouble() < t.glanceChancePerSecond * dt)
                { glance = t.glanceSeconds; glanceTarget = p.marioPos + (p.facingRight ? Vector2.left : Vector2.right) * t.glanceStep; }
                if (glance > 0f) { order.moveTarget = glanceTarget; order.intent = "LOOK BACK"; }
                // S202：看得见的危险（冒烟的炸弹/油桶）→ 先退到爆炸圈外等它炸（引信最多 1.5 秒，不会卡住，H10）
                var dodge = t.dodgeVisibleDanger ? DodgeTarget(p.marioPos, p.dangerPos, p.dangerRadius, t.dodgeMargin) : null;
                Dodging = dodge.HasValue;
                if (Dodging) { order.moveTarget = dodge; order.intent = "DODGE"; order.mark = "!"; }
                // S202：看得见的道具箱在附近 → 顺路捡（和你抢道具，反转变数）
                else if (p.seesPickup && Traits.pickupDetour >= 0f && WantsPickup(Traits, p.marioPos, p.pickupPos) && !Skipped(skippedPickups, p.pickupPos))
                {
                    if ((grabTarget - p.pickupPos).sqrMagnitude > 0.25f) { grabTarget = p.pickupPos; grabTime = 0f; }
                    grabTime += dt;
                    if (grabTime > (Personality == MarioPersonalityKind.Greedy ? t.greedyGiveUpSeconds : GrabGiveUpSeconds)) skippedPickups.Add(p.pickupPos); // 够不着 → 放弃这个箱子，继续赶路（贪财型更执着）
                    else { order.moveTarget = p.pickupPos; order.intent = "GRAB"; }
                }
                // S200 学习层：前面是上次被坑的地方 → 放慢脚步（司机层减速），头顶显示"小心"
                Cautious = t.learnFromHurt && Traits.slowNearHurtSpots && NearHurtSpot(hurtSpots, p.marioPos, p.facingRight, t.cautiousRadius);
                if (Cautious && glance <= 0f && !Dodging && order.intent != "GRAB") order.intent = "CAREFUL";
                break;
            case MarioMindState.Curious:
                order.moveTarget = lookPoint; order.mark = "?"; order.intent = "HUH?";
                break;
            case MarioMindState.Investigating:
                order.moveTarget = Focus; order.mark = "!"; order.intent = "CHECK THAT";
                order.scan = TryScanAt(p, Focus);
                break;
            case MarioMindState.Chasing:
                // S186：跟丢时追"它刚才往哪跑"（只用亲眼看到的最后位置 + 最后速度推算，H4 合规），
                // 从头顶跳过去时马里奥会转身追，而不是傻站在原地。
                order.moveTarget = seesTrickster ? lastSeen : PredictLost(p.marioPos);
                order.mark = "!!"; order.intent = "GET BACK HERE!";
                order.tryCatch = seesTrickster && Vector2.Distance(p.marioPos, p.figurePos) <= t.catchRadius;
                order.seesQuarry = seesTrickster;
                break;
            case MarioMindState.Searching:
                order.moveTarget = Focus; order.mark = "?!"; order.intent = "WHERE'D IT GO?";
                order.scan = TryScanAt(p, Focus);
                break;
        }
        // S183：被机关伤到 → 原地发晕（状态机照常，只是先站住不动；不扫描、不抓人）
        if (stun > 0f)
        {
            order.moveTarget = p.marioPos; order.scan = false; order.tryCatch = false;
            order.mark = "OUCH!"; order.intent = "DIZZY";
        }
        return order;
    }

    public bool IsStunned => stun > 0f;
    public bool IsGlancing => glance > 0f;

    /// <summary>S187：本回合的速度随机倍率（以种子决定，1 ± variance）。</summary>
    public static float RoundSpeedFactor(int seed, float variance)
    {
        variance = Mathf.Clamp01(variance);
        var r = new System.Random(unchecked(seed * 7919 + 17));
        return 1f + ((float)r.NextDouble() * 2f - 1f) * variance;
    }

    /// <summary>跟丢后的追踪点：最后所见位置 + 最后所见速度 × 已跟丢时间（上限 chasePredictSeconds）。</summary>
    private Vector2 PredictLost(Vector2 marioPos)
    {
        float ahead = Mathf.Min(lostTime, t.chasePredictSeconds);
        Vector2 guess = lastSeen + lastSeenVelocity * ahead;
        // 只推算水平方向（避免目标点跑到天上导致原地乱跳）
        return new Vector2(guess.x, lastSeen.y);
    }

    /// <summary>S185 连招奖励：在当前眩晕上追加时间（上限 cap 秒）。</summary>
    public void ExtendStun(float seconds, float cap)
    {
        if (seconds <= 0f) return;
        stun = Mathf.Min(Mathf.Max(0f, stun) + seconds, Mathf.Max(cap, 0f));
    }

    private bool TryScanAt(MarioPercept p, Vector2 point)
    {
        if (scannedHere || !p.scanReady) return false;
        if (Vector2.Distance(p.marioPos, point) > t.investigateScanDistance) return false;
        scannedHere = true; postScan = 0f;
        return true;
    }

    private void GiveUp()
    {
        Meter.Set(Mathf.Min(Meter.Value, t.afterSearchSuspicion));
        Enter(MarioMindState.Running, Focus);
    }

    private void Enter(MarioMindState next, Vector2 focus)
    {
        MarioMindState previous = State;
        State = next; Focus = focus; stateTime = 0f; lostTime = 0f; postScan = 0f; scannedHere = false;
        if (next == MarioMindState.Chasing) { lastSeen = focus; lastSeenVelocity = Vector2.zero; }
        if (previous != next) StateChanged?.Invoke(previous, next);
    }
}
