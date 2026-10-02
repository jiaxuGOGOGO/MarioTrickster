using System;
using UnityEngine;

/// <summary>
/// S185：连招计数（纯逻辑，可测试）。两次"坑到马里奥"间隔 ≤ window 秒 → 连招 +1，否则重新从 1 开始。
/// </summary>
public sealed class Step1ComboCounter
{
    private float lastHit = float.NegativeInfinity;
    public float Window { get; set; }
    public int Count { get; private set; }
    public int Max { get; private set; }
    public float LastHitTime => lastHit;
    /// <summary>S193：本回合连招总分（换招加分、同招减半）。</summary>
    public int Score { get; private set; }
    public string LastKind { get; private set; } = "";

    public Step1ComboCounter(float window) { Window = window; }

    public int Register(float time) => Register(time, "");

    public int Register(float time, string kind)
    {
        Count = time - lastHit <= Window ? Count + 1 : 1;
        lastHit = time;
        if (Count > Max) Max = Count;
        Score += Step1ComboFeel.StepScore(Count, kind ?? "", Count > 1 ? LastKind : "");
        LastKind = kind ?? "";
        return Count;
    }

    public void Reset() { lastHit = float.NegativeInfinity; Count = 0; Max = 0; Score = 0; LastKind = ""; }
}

/// <summary>
/// S185：第 1 步连招系统（用户反馈"希望陷阱能连招，单个陷阱反制马里奥不丝滑"）。
/// 坑到 = 被机关烧到 / 被机关挡停（封路墙或正在喷的火）/ 掉进坑里。
/// 连招 ≥2：被烧到时额外多晕 comboBonusStunSeconds×(连招-1)，上限 maxStunSeconds；马里奥头顶弹出"连招 x2!"。
/// 只读取马里奥自身状态与机关事件，不给马里奥任何关于捣蛋者的信息（H4 不受影响）。
/// </summary>
public class Step1Combo : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;

    public event Action<int, string> ComboRegistered;
    public Step1ComboCounter Counter { get; private set; }
    public int MaxThisRound => Counter != null ? Counter.Max : 0;
    public int ScoreThisRound => Counter != null ? Counter.Score : 0;
    private Step1RoomCamera roomCamera;
    private Step1Hitstop hitstop;

    private MarioMindDriver driver;
    private MarioController mario;
    private GameManager manager;
    private bool wasWaiting, wasInPit;
    private float lastStopAt = float.NegativeInfinity;
    private float floorY = float.NaN;
    private float flashUntil;
    private string flashText = "";
    private int flashSize = 32;

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        LaunchFeel.Apply(tuning); // S216：手感参数（被弹飞的重力、受伤小跳、特效开关）
        Counter = new Step1ComboCounter(tuning.comboWindowSeconds);
        driver = FindObjectOfType<MarioMindDriver>();
        mario = driver != null ? driver.GetComponent<MarioController>() : null;
        if (driver != null) driver.Hurt += HandleHurt;
        // S223：中招反应（纯画面）。运行时自动挂上 → 旧场景不用重建
        if (driver != null && driver.GetComponent<MarioReactionView>() == null) driver.gameObject.AddComponent<MarioReactionView>();
        // S224：声音圈（纯画面）。运行时自动挂上 → 旧场景不用重建
        if (GetComponent<Step1SoundRings>() == null) gameObject.AddComponent<Step1SoundRings>();
        // S225：按 L / P 没成功时说清楚原因（纯表现）。运行时自动挂上 → 旧场景不用重建
        if (GetComponent<Step1FailFeedback>() == null) gameObject.AddComponent<Step1FailFeedback>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        roomCamera = FindObjectOfType<Step1RoomCamera>();
        hitstop = GetComponent<Step1Hitstop>();
        if (hitstop == null) hitstop = gameObject.AddComponent<Step1Hitstop>();
        SpringPadEvents.Launched += HandleLaunched;
        CrackFloorEvents.MarioFell += HandleFell;
        BananaPeelEvents.Slipped += HandleSlipped;
        Tripwire.Tripped += HandleTripped;
        IronCage.MarioCaged += HandleCaged;
        SnareTrap.MarioSnared += HandleSnared;
    }

    // S200：绊线/铁笼/绳套也算"坑到"，能接进连招
    private void HandleTripped(Vector2 at) => Register("trip");
    private void HandleCaged(MarioController m) => Register("cage");
    private void HandleSnared(MarioController m) => Register("snare");

    private void HandleSlipped() => Register("slip");

    private void HandleLaunched() => Register("launch");
    private void HandleFell() => Register("drop");

    private void OnDestroy()
    {
        if (driver != null) driver.Hurt -= HandleHurt;
        if (manager != null) manager.OnRoundStart -= ResetRound;
        SpringPadEvents.Launched -= HandleLaunched;
        CrackFloorEvents.MarioFell -= HandleFell;
        BananaPeelEvents.Slipped -= HandleSlipped;
        Tripwire.Tripped -= HandleTripped;
        IronCage.MarioCaged -= HandleCaged;
        SnareTrap.MarioSnared -= HandleSnared;
    }

    private void ResetRound()
    {
        Counter?.Reset();
        wasWaiting = wasInPit = false; lastStopAt = float.NegativeInfinity; floorY = float.NaN; flashUntil = 0f;
    }

    private int Register(string kind)
    {
        Counter.Window = tuning.comboWindowSeconds;
        int n = Counter.Register(Time.time, kind);
        if (n >= 2)
        {
            flashText = $"<color={Step1ComboFeel.TierColor(n)}>x{n}  {Step1ComboFeel.TierName(n)}</color>";
            flashUntil = Time.time + tuning.comboFlashSeconds;
            flashSize = 30 + Mathf.Min(4, n - 2) * 4; // 段位越高字越大
        }
        // S193 手感：顿帧 + 屏幕震动（段数越高越重）
        if (hitstop != null)
            hitstop.Request(Step1ComboFeel.HitstopSeconds(n, tuning.hitstopBaseSeconds, tuning.hitstopPerStepSeconds, tuning.hitstopMaxSeconds), tuning.hitstopTimeScale);
        if (roomCamera != null)
            roomCamera.Shake(Step1ComboFeel.ShakeAmplitude(n, tuning.shakePerStep, tuning.shakeMax), tuning.shakeSeconds);
        ComboRegistered?.Invoke(n, kind);
        return n;
    }

    private void HandleHurt(MarioMindState state)
    {
        int n = Register("hurt");
        // S193：递减追加（格斗游戏 damage scaling）—— 第 2 段 +0.6s，第 3 段 +0.42s，第 4 段 +0.29s…，总量仍受 maxStunSeconds 限制
        if (n >= 2 && driver != null && driver.Mind != null)
            driver.Mind.ExtendStun(Step1ComboFeel.BonusStun(n, tuning.comboBonusStunSeconds, tuning.comboStunScaling), tuning.maxStunSeconds);
    }

    private void Update()
    {
        if (driver == null || mario == null || manager == null || manager.CurrentState != GameState.Playing) return;
        if (driver.IsWaitingToStart) { floorY = float.NaN; return; }

        var bot = driver.Bot;
        bool waiting = bot != null && bot.IsWaitingForTrap;
        if (waiting && !wasWaiting && Time.time - lastStopAt >= tuning.stopDebounceSeconds) { lastStopAt = Time.time; Register("stop"); }
        wasWaiting = waiting;

        // 掉坑：第一次站稳的高度 = 地面；之后站在比地面低半格以下 = 掉进坑里
        if (mario.IsGrounded)
        {
            float y = mario.transform.position.y;
            if (float.IsNaN(floorY)) floorY = y;
            bool inPit = y < floorY - 0.5f;
            if (inPit && !wasInPit) Register("pit");
            wasInPit = inPit;
        }
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Time.time > flashUntil || mario == null || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(mario.transform.position + Vector3.up * 3.2f);
        if (sp.z < 0f) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
        // 弹出动画：刚出现时放大再回落
        float age = tuning.comboFlashSeconds - (flashUntil - Time.time);
        float pop = 1f + Mathf.Max(0f, 0.35f - age) * 1.2f;
        var r = new Rect(at.x - 210f * pop, at.y - 45f * pop, 420f * pop, 90f * pop);
        Step1Gui.Panel(r, 0.7f);
        GUI.Label(r, $"<b>{flashText}</b>", Step1Gui.Text(Mathf.RoundToInt(flashSize * pop), TextAnchor.MiddleCenter));
    }
}
