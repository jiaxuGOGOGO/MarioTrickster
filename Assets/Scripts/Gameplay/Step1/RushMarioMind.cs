using System;
using UnityEngine;

public enum MarioMindState { Running, Curious, Investigating, Chasing, Searching }

/// <summary>马里奥这一帧"知道"的全部信息。只能由 MarioEyes / 自身状态填写（H4）。</summary>
public struct MarioPercept
{
    public Vector2 marioPos;
    public bool seesFigure;
    public Vector2 figurePos;
    public bool figureLooksLikeProp;
    public bool figureMoving;
    /// <summary>S186：看得见时，它的移动速度（由两帧所见位置算出，看不见时为 0）。</summary>
    public Vector2 figureVelocity;
    public bool witnessedActivation;
    public Vector2 activationPos;
    public bool hurt;
    public bool scanReady;
    public bool carryingLoot;
}

public struct MarioOrder
{
    public MarioMindState state;
    public Vector2? moveTarget;
    public bool scan;
    public bool tryCatch;
    public string mark;
    public string intent;
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
    private float stateTime, lostTime, hurtFlash, celebrate, postScan, stun;
    private bool scannedHere;
    private Vector2 lookPoint, lastSeen, lastSeenVelocity;

    public SuspicionMeter Meter { get; }
    public MarioMindState State { get; private set; } = MarioMindState.Running;
    public Vector2 Focus { get; private set; }
    public event Action<MarioMindState, MarioMindState> StateChanged;

    public RushMarioMind(MarioMindTuningSO tuning) { t = tuning; Meter = new SuspicionMeter(tuning); }

    public void Reset()
    {
        Meter.Reset(); hurtFlash = celebrate = stun = 0f;
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

        bool seesTrickster = p.seesFigure && !p.figureLooksLikeProp;
        bool seesOddProp = p.seesFigure && p.figureLooksLikeProp && p.figureMoving;
        float rise = 0f;
        if (seesTrickster) { rise += t.seeTricksterPerSecond; Focus = p.figurePos; }
        else if (seesOddProp) { rise += t.seeDisguisedMovePerSecond; Focus = p.figurePos; }
        if (p.witnessedActivation) { Meter.Add(t.witnessedActivation); Focus = p.activationPos; }
        if (p.hurt)
        {
            Meter.Add(t.hurtByTrap); hurtFlash = t.hurtFlashSeconds; stun = t.hurtStunSeconds;
            if (!seesTrickster && !seesOddProp && !p.witnessedActivation) Focus = p.marioPos;
        }
        Meter.Tick(dt, rise);

        var order = new MarioOrder();
        switch (State)
        {
            case MarioMindState.Running:
                if (Meter.Level != SuspicionLevel.Calm)
                {
                    float side = Mathf.Sign(Focus.x - p.marioPos.x);
                    lookPoint = new Vector2(p.marioPos.x + side * t.lookStep, p.marioPos.y);
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

        order.state = State;
        switch (State)
        {
            case MarioMindState.Running:
                order.moveTarget = null;
                order.mark = celebrate > 0f ? "GOTCHA!" : hurtFlash > 0f ? "OUCH!" : "";
                order.intent = p.carryingLoot ? "RUN HOME" : "GET LOOT";
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
