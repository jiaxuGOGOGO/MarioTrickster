using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S242：试玩黑匣子的"录音机"。和 Step1Feedback 一起常驻（每个场景自动有一个，不用往场景里拖）。
/// 每秒 blackBoxHz 次记下马里奥 / 你的位置、速度、状态、按键、timeScale、fps，只留最近 blackBoxSeconds 秒；
/// 同时收"面包屑"（提示文字、机关事件、受伤、救援、错误），最近 150 条。
/// 自动检测：马里奥卡住、按住方向键不动、画面顿卡、位置变 NaN / 出房间、时间停住、同一个键狂按 → 限频后自动记一条反馈。
/// F8 / 自动记录时 Step1Feedback 调 Dump() 把这些写进 blackbox_NNN.md（AI 直接看文字，不用猜截图）。
/// 纯记录：只读公开状态，不改任何玩法（H4：马里奥心智不读它）。逻辑都在 Step1BlackBox（sim 能测）。
/// </summary>
public sealed class Step1BlackBoxRecorder : MonoBehaviour
{
    public static Step1BlackBoxRecorder Instance { get; private set; }

    private Step1BlackBox.Ring<Step1BlackBox.Sample> samples;
    private readonly Step1BlackBox.Ring<string> crumbs = new Step1BlackBox.Ring<string>(Step1BlackBox.BreadcrumbCap);
    private Step1BlackBox.Throttle throttle;
    private MarioMindTuningSO tuning;
    private float nextSample, heldFor, frozenFor, lastHitchAt = -99f;
    private readonly Dictionary<KeyCode, List<float>> presses = new Dictionary<KeyCode, List<float>>();
    private static readonly KeyCode[] Watched = { KeyCode.L, KeyCode.P, KeyCode.G, KeyCode.B, KeyCode.U, KeyCode.K, KeyCode.T, KeyCode.Z, KeyCode.E }; // 技能键（方向 / 跳正常会连按，不算）

    // 缓存的场景物（场景切换时重新找；不在每帧 Find）
    private MarioMindDriver driver; private MarioController mario; private TricksterController you; private Rigidbody2D marioRb, youRb;
    private Vector2 lastMario, lastYou; private bool hasLast;
    private string sceneName = "";
    private string[] room;

    public void Init(MarioMindTuningSO t)
    {
        tuning = t != null ? t : MarioMindTuningSO.LoadOrDefault();
        samples = new Step1BlackBox.Ring<Step1BlackBox.Sample>(Step1BlackBox.SampleCap(tuning.blackBoxSeconds, tuning.blackBoxHz));
        throttle = new Step1BlackBox.Throttle(tuning.blackBoxCooldown, tuning.blackBoxMaxAuto);
    }

    private void Awake() { Instance = this; if (tuning == null) Init(null); Step1Hint.Shown += OnHint; Hook(true); }
    private void OnDestroy() { if (Instance == this) Instance = null; Step1Hint.Shown -= OnHint; Hook(false); }

    private void Hook(bool on)
    {
        if (on)
        {
            CrackedWall.Smashed += OnSmash; Tripwire.Tripped += OnTrip; TricksterBomb.Exploded += OnBoom; IronCage.MarioCaged += OnCage; SnareTrap.MarioSnared += OnSnare;
            ControllablePropBase.MissRefunded += OnRefund; MarioMindLabel.Rescued += OnRescued; Vent.Clanged += OnVent; CannonBall.HitMario += OnCannon;
        }
        else
        {
            CrackedWall.Smashed -= OnSmash; Tripwire.Tripped -= OnTrip; TricksterBomb.Exploded -= OnBoom; IronCage.MarioCaged -= OnCage; SnareTrap.MarioSnared -= OnSnare;
            ControllablePropBase.MissRefunded -= OnRefund; MarioMindLabel.Rescued -= OnRescued; Vent.Clanged -= OnVent; CannonBall.HitMario -= OnCannon;
        }
    }

    private void OnHint(string msg) => Crumb("提示：" + Strip(msg));
    private void OnSmash(Vector2 p) => Crumb($"裂墙被打碎 ({p.x:0},{p.y:0})");
    private void OnTrip(Vector2 p) => Crumb($"绊线被踩 ({p.x:0},{p.y:0})");
    private void OnBoom(Vector2 p) => Crumb($"爆炸 ({p.x:0},{p.y:0})");
    private void OnCage(MarioController m) => Crumb("马里奥被铁笼关住");
    private void OnSnare(MarioController m) => Crumb("马里奥被绳套吊住");
    private void OnRefund(ControllablePropBase p) => Crumb("机关没打中，次数退回：" + (p != null ? p.name : "?"));
    private void OnRescued() { Crumb("卡住救援把马里奥放回路上"); }
    private void OnVent(Vector2 p) => Crumb($"通风管咣当 ({p.x:0},{p.y:0})");
    private void OnCannon() => Crumb("炮弹打中马里奥");

    private static string Strip(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var o = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "");
        return o.Length > 80 ? o.Substring(0, 80) : o;
    }

    /// <summary>外面也能塞一条面包屑（例如错误、F8 标签）。</summary>
    public static void Note(string text) { if (Instance != null) Instance.Crumb(text); }

    private void Crumb(string text) => crumbs.Add($"{Time.unscaledTime:0.0}s {text}");

    private void Rebind()
    {
        string now = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool newScene = now != sceneName;
        sceneName = now;
        driver = FindObjectOfType<MarioMindDriver>();
        mario = driver != null ? driver.GetComponent<MarioController>() : FindObjectOfType<MarioController>();
        you = FindObjectOfType<TricksterController>();
        marioRb = mario != null ? mario.GetComponent<Rigidbody2D>() : null;
        youRb = you != null ? you.GetComponent<Rigidbody2D>() : null;
        var src = Step1PrankRoomBuilderBridge.CurrentRoom;
        room = src != null ? System.Array.ConvertAll(src, Step1Layout.StripSlots) : null;
        if (!newScene) return; // 定期重找（场景里还没有角色）不清空记录
        hasLast = false; heldFor = 0f; frozenFor = 0f;
        samples.Clear();
        Crumb("进入场景 " + sceneName);
    }

    private void Update()
    {
        if (tuning == null || samples == null) Init(null);
        if (sceneName != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name || (mario == null && you == null && Time.frameCount % 120 == 0)) Rebind();
        float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

        // 按键：面包屑 + 狂按检测
        foreach (var k in Watched)
        {
            if (!Step1Keys.Down(k)) continue;
            if (!presses.TryGetValue(k, out var l)) presses[k] = l = new List<float>();
            l.Add(now); l.RemoveAll(t => now - t > 3f);
            if (Step1BlackBox.Mash(l, now, 2f, 6)) { Auto(Step1BlackBox.Kind.Mash, $"2 秒内按了 {l.Count} 次 {k}"); l.Clear(); }
        }

        // 顿卡（忽略开头几帧和切场景）
        if (Time.frameCount > 30 && Step1BlackBox.Hitch(dt, tuning.blackBoxHitchSeconds) && now - lastHitchAt > 1f) { lastHitchAt = now; Auto(Step1BlackBox.Kind.Hitch, $"一帧 {dt:0.00} 秒"); }

        // 时间停住（暂停菜单 / 问卷 / 帮助页 / 回放 = 正常）
        var gm = GameManager.Instance;
        bool legit = (gm != null && gm.CurrentState != GameState.Playing) || Step1Screen.HelpOpen || Step1PlaytestLog.IsTyping || Step1HandsOffCheck.IsRunning;
        frozenFor = Time.timeScale <= 0.001f ? frozenFor + dt : 0f;
        if (Step1BlackBox.TimeFrozen(frozenFor, legit, 6f)) { Auto(Step1BlackBox.Kind.TimeFrozen, $"timeScale=0 已 {frozenFor:0} 秒，没有暂停菜单"); frozenFor = -60f; }

        // 按住方向键却不动
        bool held = Step1Keys.Held(KeyCode.LeftArrow) || Step1Keys.Held(KeyCode.RightArrow) || Step1Keys.Held(KeyCode.A) || Step1Keys.Held(KeyCode.D);
        heldFor = held && Time.timeScale > 0f ? heldFor + dt : 0f;

        if (now < nextSample) return;
        nextSample = now + 1f / Mathf.Max(1f, tuning.blackBoxHz);
        Sample(now, held);
    }

    private void Sample(float now, bool held)
    {
        var s = new Step1BlackBox.Sample { t = now, timeScale = Time.timeScale, fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f };
        if (mario != null) { s.mario = mario.transform.position; s.marioVel = marioRb != null ? marioRb.velocity : Vector2.zero; }
        s.marioState = driver != null && driver.Mind != null ? driver.Mind.State.ToString() + (mario != null && mario.IsStunned ? "/晕" : "") + (driver.IsWaitingToStart ? "/等开局" : "") : "-";
        if (you != null)
        {
            s.you = you.transform.position; s.youVel = youRb != null ? youRb.velocity : Vector2.zero;
            var f = new System.Text.StringBuilder();
            if (you.IsDisguised) f.Append("伪");
            if (TricksterBurrow.Instance != null && TricksterBurrow.BodyBusy && !TricksterSilk.Swinging) f.Append("地");
            if (TricksterSilk.Swinging) f.Append("丝");
            if (PranksterCannon.TricksterSeated) f.Append("炮");
            if (TricksterKit.Instance != null && TricksterKit.Instance.Shrunk) f.Append("缩");
            s.youFlags = f.Length > 0 ? f.ToString() : "-";
        }
        else s.youFlags = "-";
        var keys = new System.Text.StringBuilder();
        if (Step1Keys.Held(KeyCode.LeftArrow) || Step1Keys.Held(KeyCode.A)) keys.Append("←");
        if (Step1Keys.Held(KeyCode.RightArrow) || Step1Keys.Held(KeyCode.D)) keys.Append("→");
        if (Step1Keys.Held(KeyCode.UpArrow) || Step1Keys.Held(KeyCode.W)) keys.Append("↑");
        if (Step1Keys.Held(KeyCode.DownArrow) || Step1Keys.Held(KeyCode.S)) keys.Append("↓");
        s.keys = keys.Length > 0 ? keys.ToString() : "-";
        samples.Add(s);

        int w = room != null && room.Length > 0 ? room[0].Length : 0, h = room != null ? room.Length : 0;
        if (mario != null && Step1BlackBox.BadNumber(s.mario, w, h)) Auto(Step1BlackBox.Kind.BadNumber, $"马里奥在 ({s.mario.x:0.0},{s.mario.y:0.0})");
        if (you != null && Step1BlackBox.BadNumber(s.you, w, h)) Auto(Step1BlackBox.Kind.BadNumber, $"你在 ({s.you.x:0.0},{s.you.y:0.0})");

        // 最近 4 秒的窗口
        var mw = new List<Vector2>(); var yw = new List<Vector2>(); float span = 0f;
        for (int i = samples.Count - 1; i >= 0; i--) { var x = samples[i]; if (now - x.t > 4f) break; mw.Add(x.mario); yw.Add(x.you); span = now - x.t; }
        var gm = GameManager.Instance;
        bool marioShouldMove = mario != null && driver != null && driver.Mind != null && !mario.IsStunned && !driver.IsWaitingToStart && Time.timeScale > 0f
                               && (driver.Mind.State == MarioMindState.Running || driver.Mind.State == MarioMindState.Chasing) // 起疑 / 找你时站着东张西望是正常的
                               && (gm == null || gm.CurrentState == GameState.Playing) && !Step1HandsOffCheck.IsRunning;
        if (Step1BlackBox.Stuck(mw, marioShouldMove, span, 3.5f)) Auto(Step1BlackBox.Kind.Stuck, $"马里奥 4 秒没挪窝 ({s.mario.x:0.0},{s.mario.y:0.0}) 状态 {s.marioState}");
        bool youMayMove = you != null && !you.IsDisguised && !PranksterCannon.TricksterSeated && (gm == null || gm.CurrentState == GameState.Playing);
        if (Step1BlackBox.HeldNoMove(heldFor, 2.5f, yw, youMayMove)) { Auto(Step1BlackBox.Kind.HeldNoMove, $"按住方向键 {heldFor:0.0} 秒，你没动 ({s.you.x:0.0},{s.you.y:0.0}) 状态 {s.youFlags}"); heldFor = -5f; }
    }

    /// <summary>自动记一条（限频）。</summary>
    public void Auto(Step1BlackBox.Kind kind, string why)
    {
        Crumb($"[自动] {Step1BlackBox.KindZh(kind)}：{why}");
        if (throttle == null || !throttle.Allow(kind, Time.unscaledTime)) return;
        Step1Feedback.CaptureAuto(kind, why);
    }

    public bool AllowAuto(Step1BlackBox.Kind kind) => throttle != null && throttle.Allow(kind, Time.unscaledTime);

    /// <summary>写一份 blackbox md（Step1Feedback 调）。</summary>
    public string Dump(Step1BlackBox.Report r) => Step1BlackBox.Markdown(r, samples.ToList(), crumbs.ToList(), room);
}
