using UnityEngine;

public enum OverworldMarioState { Walking, Curious, Investigating, Chasing, Searching, Dizzy, Waiting, InRoom, Home }

/// <summary>S210：大地图上马里奥这一帧"知道"的全部（只能由 OverworldMario 的眼睛/耳朵填，H4）。</summary>
public struct OverworldPercept
{
    public Vector2 marioPos;
    public bool seesFigure;
    public Vector2 figurePos;
    /// <summary>看起来像个木箱（伪装外观，谁都看得见的外观，不是内部状态）。</summary>
    public bool figureLooksLikeProp;
    public bool figureMoving;
    public bool sawRustle;
    public Vector2 rustlePos;
    public bool heardTaunt;
    public Vector2 tauntPos;
    public bool slipped;
    /// <summary>S218：听见大机关的动静（炮声 / 滚石 / 落地）——只知道声音在哪（H4）。</summary>
    public bool heardNoise;
    public Vector2 noisePos;
    public float noiseSuspicion;
    /// <summary>S218：这一帧被大机关砸中（落地 / 被碾），晕几秒。</summary>
    public float bigStun;
    /// <summary>日程：现在要去的地方（门口 / 家）。null = 时间没到，在原地等。</summary>
    public Vector2? scheduleTarget;
}

public struct OverworldOrder
{
    public OverworldMarioState state;
    /// <summary>要走去的点（null = 站着不动）。</summary>
    public Vector2? target;
    public bool tryCatch;
    public string mark;
    public string intent;
}

/// <summary>
/// S210：大地图马里奥的心智（纯逻辑，沙盒可测）。和横版房间的 RushMarioMind 用同一个起疑表（SuspicionMeter）与同一组阈值——
/// 同一个信号在两个视角里意思一样（H6）：'?' 停下看 → '!' 走过去查 → '!!' 看见本体追 → '?!' 跟丢了去最后位置找 → 回到日程。
/// 规则保障：H2（Meter 保证先 '?' 后 '!'）；H9（追/查/找/晕都有时限）；H10（没人捣乱时只按日程走，DaySchedule 检查过能走完）。
/// </summary>
public sealed class OverworldMind
{
    private readonly MarioMindTuningSO t;
    private float stateTime, lostTime, dizzy;
    private Vector2 focus, lastSeen;
    public SuspicionMeter Meter { get; }
    public OverworldMarioState State { get; private set; } = OverworldMarioState.Walking;
    /// <summary>今天被你拖住/引走的总秒数（给结算看）。</summary>
    public float DelayedSeconds { get; private set; }

    public OverworldMind(MarioMindTuningSO tuning) { t = tuning; Meter = new SuspicionMeter(tuning); }

    /// <summary>S221：被你拖住（在雷云外等）——算进今天拖住他的秒数。</summary>
    public void AddDelay(float dt) { DelayedSeconds += Mathf.Max(0f, dt); }

    public void Reset() { Meter.Reset(); State = OverworldMarioState.Walking; stateTime = lostTime = dizzy = 0f; DelayedSeconds = 0f; }

    /// <summary>进门/回家：由驱动层设置（不经过起疑表）。</summary>
    public void SetInRoom(bool inRoom) { State = inRoom ? OverworldMarioState.InRoom : OverworldMarioState.Walking; stateTime = 0f; }
    public void SetHome() { State = OverworldMarioState.Home; stateTime = 0f; }

    /// <summary>抓到你之后：他满意地回到日程（起疑降到 afterSearchSuspicion 以下）。</summary>
    public void OnCaught() { Meter.Set(Mathf.Min(Meter.Value, t.afterSearchSuspicion)); Enter(OverworldMarioState.Walking); }

    public OverworldOrder Tick(float dt, OverworldPercept p)
    {
        dt = Mathf.Max(0f, dt);
        var o = new OverworldOrder();
        if (State == OverworldMarioState.InRoom || State == OverworldMarioState.Home) { o.state = State; return o; }
        stateTime += dt;
        if (p.slipped) dizzy = Mathf.Min(t.maxStunSeconds, Mathf.Max(dizzy, t.overworldSlipStunSeconds));
        if (p.bigStun > 0f) dizzy = Mathf.Min(t.maxStunSeconds, Mathf.Max(dizzy, p.bigStun));
        dizzy = Mathf.Max(0f, dizzy - dt);

        bool seesYou = p.seesFigure && !p.figureLooksLikeProp;
        bool seesOdd = p.seesFigure && p.figureLooksLikeProp && p.figureMoving;
        float rise = 0f;
        if (seesYou) { rise += t.seeTricksterPerSecond; focus = p.figurePos; lastSeen = p.figurePos; lostTime = 0f; }
        else if (seesOdd) { rise += t.seeDisguisedMovePerSecond; focus = p.figurePos; }
        if (p.sawRustle) { Meter.Add(t.rustleSuspicion * dt * 2f); if (!seesYou && !seesOdd) focus = p.rustlePos; }
        if (p.heardTaunt) { Meter.Add(t.tauntSuspicion); if (!seesYou) focus = p.tauntPos; }
        if (p.heardNoise) { Meter.Add(p.noiseSuspicion); if (!seesYou) focus = p.noisePos; } // S218：只到 '?'（停下看一眼），不会一声就 '!'（H2）
        Meter.Tick(dt, rise);

        switch (State)
        {
            case OverworldMarioState.Walking:
            case OverworldMarioState.Waiting:
                if (Meter.Level != SuspicionLevel.Calm) Enter(OverworldMarioState.Curious);
                break;
            case OverworldMarioState.Curious:
                if (Meter.Level == SuspicionLevel.Alert) Enter(seesYou ? OverworldMarioState.Chasing : OverworldMarioState.Investigating);
                else if (Meter.Level == SuspicionLevel.Calm) Enter(OverworldMarioState.Walking);
                break;
            case OverworldMarioState.Investigating:
                if (seesYou && Meter.Level == SuspicionLevel.Alert) Enter(OverworldMarioState.Chasing);
                else if (stateTime > t.investigateTimeout || Vector2.Distance(p.marioPos, focus) <= t.arriveDistance) Enter(OverworldMarioState.Searching);
                break;
            case OverworldMarioState.Chasing:
                if (!seesYou)
                {
                    lostTime += dt;
                    if (lostTime > t.chaseGiveUpSeconds || Vector2.Distance(p.marioPos, lastSeen) <= t.arriveDistance) { focus = lastSeen; Enter(OverworldMarioState.Searching); }
                }
                if (stateTime > t.overworldMaxChaseSeconds) GiveUp();
                break;
            case OverworldMarioState.Searching:
                if (seesYou && Meter.Level == SuspicionLevel.Alert) Enter(OverworldMarioState.Chasing);
                else if (stateTime > t.searchSeconds) GiveUp();
                break;
        }

        o.state = State;
        switch (State)
        {
            case OverworldMarioState.Walking:
            case OverworldMarioState.Waiting:
                o.target = p.scheduleTarget; o.state = p.scheduleTarget.HasValue ? OverworldMarioState.Walking : OverworldMarioState.Waiting;
                if (o.state != State) State = o.state;
                o.mark = ""; o.intent = p.scheduleTarget.HasValue ? "ON MY WAY" : "WAITING";
                break;
            case OverworldMarioState.Curious: o.target = null; o.mark = "?"; o.intent = "HUH?"; DelayedSeconds += dt; break;
            case OverworldMarioState.Investigating: o.target = focus; o.mark = "!"; o.intent = "CHECK THAT"; DelayedSeconds += dt; break;
            case OverworldMarioState.Chasing:
                o.target = lastSeen; o.mark = "!!"; o.intent = "GET BACK HERE!"; DelayedSeconds += dt;
                o.tryCatch = seesYou && Vector2.Distance(p.marioPos, p.figurePos) <= t.catchRadius;
                break;
            case OverworldMarioState.Searching: o.target = focus; o.mark = "?!"; o.intent = "WHERE'D IT GO?"; DelayedSeconds += dt; break;
        }
        if (dizzy > 0f) { o.target = null; o.tryCatch = false; o.mark = "OUCH!"; o.intent = "DIZZY"; o.state = OverworldMarioState.Dizzy; DelayedSeconds += dt; }
        return o;
    }

    private void GiveUp() { Meter.Set(Mathf.Min(Meter.Value, t.afterSearchSuspicion)); Enter(OverworldMarioState.Walking); }
    private void Enter(OverworldMarioState s) { State = s; stateTime = 0f; if (s != OverworldMarioState.Chasing) lostTime = 0f; }

    /// <summary>
    /// 门口的结果（纯逻辑）：你先到门口按 E = 埋伏（正常开打，他有开局准备时间）；
    /// 他先进门、你在 lateWindowSeconds 内跟进去 = 迟到（开打，但他没有开局等待，直接冲）；
    /// 超过时间还没进 = 他在里面安安稳稳拿了宝，这扇门算他赢。
    /// </summary>
    public enum DoorOutcome { Ambush, Late, Missed }
    public static DoorOutcome AtDoor(bool tricksterFirst, float secondsSinceMarioEntered, float lateWindowSeconds) =>
        tricksterFirst ? DoorOutcome.Ambush : secondsSinceMarioEntered <= lateWindowSeconds ? DoorOutcome.Late : DoorOutcome.Missed;

    /// <summary>从大地图带进房间的起疑值：追你时进门 → 进房就是 '?'（看得见的预兆，不跳过 H2）；平静进门 → 0。上限 curious 阈值，不会一进门就 '!'。</summary>
    public static float CarriedSuspicion(OverworldMarioState stateAtDoor, float meterValue, float curiousThreshold) =>
        stateAtDoor == OverworldMarioState.Walking || stateAtDoor == OverworldMarioState.Waiting ? 0f : Mathf.Min(meterValue, curiousThreshold);
}
