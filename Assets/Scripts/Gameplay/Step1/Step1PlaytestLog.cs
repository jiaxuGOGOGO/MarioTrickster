using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步退出条件的记录工具：连玩 20 局还想玩 + 至少 3 种不同的恶作剧方式。
/// - 恶作剧归因：捣蛋者触发某个机关后 prankAttributionSeconds 内马里奥受伤 → 记一次该机关类型的恶作剧；
///   马里奥追丢（Chasing → Searching → Running 且没抓到）→ 记一次 "Escape"。
/// - 回合结束：屏幕上答宪法第 3 层每局问卷（Step1RoundSurvey：算准了/差点被发现/笑出来了吗(S223)/被抓服气吗/想再来 1–5/一句话），
///   写入 PlaytestLogs/step1_rounds.csv（在 Assets 外，不进 git）。问卷答完之前 R/N 被屏蔽，避免误开下一局。
/// - S182：界面中英对照、只留必要信息（左上角 3 行状态 + 底部 1 行按键），结算用按钮/按键作答，答完才显示 N/R。
/// 本类只做记录与显示，不影响马里奥决策。
/// </summary>
public class Step1PlaytestLog : MonoBehaviour
{
    public const string LogFolder = "PlaytestLogs";
    public const string LogFile = "step1_rounds.csv";
    /// <summary>问卷打字时其它热键（C 切镜头等）应忽略。</summary>
    public static bool IsTyping { get; private set; }

    [SerializeField] private MarioMindTuningSO tuning;

    private MarioMindDriver driver;
    private TricksterController barFigure; private MarioMindTuningSO barTuning; private DecoyAbility barDecoy; private TauntAbility barTaunt;
    /// <summary>S226 E9：看情况的按键条（调参 contextKeyBar 关掉 = 回到以前那一整行）。</summary>
    private string CurrentControlsBar()
    {
        if (barTuning == null) barTuning = MarioMindTuningSO.LoadOrDefault();
        if (!barTuning.contextKeyBar) return Step1Text.ControlsBar;
        if (barFigure == null) { barFigure = FindObjectOfType<TricksterController>(); if (barFigure != null) { barDecoy = barFigure.GetComponent<DecoyAbility>(); barTaunt = barFigure.GetComponent<TauntAbility>(); } }
        if (barFigure == null) return Step1Text.ControlsBar;
        if (PranksterCannon.SeatedIn != null) return string.Format(Step1Text.ControlsBarSeated, PranksterCannon.SeatedIn.SeatSecondsLeft); // S240
        var kit = TricksterKit.Instance; bool dis = barFigure.IsDisguised, sh = kit != null && kit.Shrunk;
        return Step1Text.ControlsBarFor(dis, sh,
            kit != null && TricksterKit.CanBomb(dis, sh, kit.BombsLeft, kit.BombCooldown),
            barDecoy != null && DecoyAbility.CanDecoy(dis, sh, barDecoy.DecoysLeft, DecoyAbility.Active != null),
            barTaunt != null && TauntAbility.CanTaunt(dis, barTaunt.TauntsLeft, barTaunt.Cooldown),
            kit != null && TricksterKit.CanShrink(sh, kit.ShrinksLeft),
            Vent.AnyWithin(barFigure.transform.position, 1.2f),
            PranksterCannon.MouthOf(barFigure) is PranksterCannon mouth && mouth.SeatReadyFor(barFigure),
            TricksterBurrow.Instance != null && TricksterBurrow.Instance.CanBurrowNow,
            TricksterSilk.Instance != null && TricksterSilk.Instance.CanSilkNow,
            TricksterBurrow.Instance != null && TricksterBurrow.Instance.Burrowed,
            TricksterSilk.Swinging);
    }
    private TricksterLives lives;
    private TricksterAbilitySystem abilities;
    private Step1RoomCamera roomCamera;
    private Step1Combo combo;
    private Step1LayoutVariants layout;
    private Step1StuckRescue rescue;
    private GameManager manager;
    private string lastPropKind = "";
    private float lastPropTime = -999f;
    private bool chaseOpen;
    private readonly Dictionary<string, int> roundPranks = new Dictionary<string, int>();
    private readonly HashSet<string> sessionKinds = new HashSet<string>();
    private int roundOmens, roundAlerts, roundCaught;
    private int roundsLogged;
    private bool awaitingRating;
    private Step1RoundSurvey survey;
    private string noteDraft = "";
    private string lastWinner = "";
    private bool savedThisRound;
    private string lastReason = "";
    private string roundMode = "room";
    /// <summary>S224：这局他起疑过几次、最险一次到多少（问卷"差点被发现"题下面给参考）。</summary>
    private readonly NearMissLog roomNear = new NearMissLog();

    public IReadOnlyDictionary<string, int> RoundPranks => roundPranks;
    public int DistinctKindsThisSession => sessionKinds.Count;

    /// <summary>把机关组件映射成恶作剧类别（供归因和测试使用）。</summary>
    public static string PrankKindOf(IControllableProp prop)
    {
        if (prop == null) return "";
        if (prop is FireTrap) return "Fire";
        if (prop is ControllableBlocker) return "Blocker";
        if (prop is CollapsingPlatform) return "Collapse";
        if (prop is PranksterCannon) return "Cannon";
        return prop.GetType().Name;
    }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        driver = FindObjectOfType<MarioMindDriver>();
        lives = FindObjectOfType<TricksterLives>();
        roomCamera = FindObjectOfType<Step1RoomCamera>();
        combo = FindObjectOfType<Step1Combo>();
        layout = FindObjectOfType<Step1LayoutVariants>();
        rescue = FindObjectOfType<Step1StuckRescue>();
        var figure = FindObjectOfType<TricksterController>();
        abilities = figure != null ? figure.AbilitySystem : null;
        if (abilities != null) abilities.OnPropActivated += HandleProp;
        if (combo != null) combo.ComboRegistered += HandleCombo;
        if (driver != null)
        {
            driver.Hurt += HandleHurt;
            driver.Caught += HandleCaught;
            driver.Mind.StateChanged += HandleState;
        }
        manager = GameManager.Instance;
        if (manager != null) { manager.OnRoundStart += BeginRound; manager.OnGameOver += HandleGameOver; }
        GameManager.BlockRoundOverKeys = () => awaitingRating;
        BeginRound();
    }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnPropActivated -= HandleProp;
        if (combo != null) combo.ComboRegistered -= HandleCombo;
        if (driver != null) { driver.Hurt -= HandleHurt; driver.Caught -= HandleCaught; if (driver.Mind != null) driver.Mind.StateChanged -= HandleState; }
        if (manager != null) { manager.OnRoundStart -= BeginRound; manager.OnGameOver -= HandleGameOver; }
        GameManager.BlockRoundOverKeys = null;
        IsTyping = false;
    }

    private void BeginRound()
    {
        rareLine = ""; roundPranks.Clear(); roundOmens = roundAlerts = roundCaught = 0;
        chaseOpen = false; awaitingRating = false; savedThisRound = false; survey = null; IsTyping = false; noteDraft = ""; lastPropKind = ""; lastPropTime = -999f; lastComboCount = -999f;
        roomNear.Clear();
        Step1QuickTest.UsedThisRound = Step1QuickTest.NoLimits; // S236：这一局一开始就开着 F9 = 这局算 F9
    }

    private void HandleProp(IControllableProp prop) { lastPropKind = PrankKindOf(prop); lastPropTime = Time.time; }

    private void HandleHurt(MarioMindState state)
    {
        if (!string.IsNullOrEmpty(lastPropKind) && Time.time - lastPropTime <= tuning.prankAttributionSeconds)
        { Count(lastPropKind); lastPropKind = ""; }
    }

    /// <summary>
    /// S227：以前"坑法"只认 会伤人的机关（火/挡板/塌桥/大炮）+ 甩掉追捕；香蕉皮、弹簧、裂缝、铁笼、绊线、绳套、掉坑、挡停
    /// 都算"坑到"（连招计数里有），却进不了第 1 步出口的"≥3 种坑法"。这里把连招的种类也记进去（hurt 已由机关归因，不重复）。
    /// </summary>
    public static string PrankKindOfCombo(string comboKind)
    {
        switch (comboKind)
        {
            case "slip": return "Banana";
            case "launch": return "Spring";
            case "drop": return "CrackFloor";
            case "cage": return "Cage";
            case "trip": return "Tripwire";
            case "snare": return "Snare";
            case "pit": return "Pit";
            case "stop": return "Blocker";
            default: return ""; // hurt 由 HandleHurt 按机关归因
        }
    }

    private float lastComboCount = -999f;
    private void HandleCombo(int n, string kind)
    {
        string k = PrankKindOfCombo(kind);
        if (k.Length == 0 || Time.time - lastComboCount < 0.25f) return; // 同一下（掉坑 + 裂缝）只算一次
        lastComboCount = Time.time; Count(k);
    }

    private void HandleCaught() { roundCaught++; chaseOpen = false; roomNear.Caught(RoundClock, ""); }
    private string RoundClock => (manager != null ? manager.RoundElapsed : 0f).ToString("0") + "s";

    // [AI防坑警告] 第 3 次被抓时 TricksterLives 先 EndRound 再返回，Caught 事件晚于 OnGameOver；以裁判计数为准。
    private int CaughtThisRound => lives != null ? Mathf.Max(roundCaught, lives.TimesCaught) : roundCaught;

    private void HandleState(MarioMindState from, MarioMindState to)
    {
        if (to == MarioMindState.Curious) roundOmens++;
        if (to == MarioMindState.Investigating || to == MarioMindState.Chasing) roundAlerts++;
        if (to == MarioMindState.Chasing) chaseOpen = true;
        if (to == MarioMindState.Running && chaseOpen) { chaseOpen = false; Count("Escape"); }
    }

    private void Count(string kind)
    {
        roundPranks.TryGetValue(kind, out int n);
        roundPranks[kind] = n + 1;
        sessionKinds.Add(kind);
    }

    /// <summary>S231：结算时"最稀罕的一招"（和以前所有局比；只在真稀罕时有字）。</summary>
    private string rareLine = "";
    public string RareLine => rareLine;

    private void ComputeRareLine()
    {
        rareLine = "";
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", LogFolder);
            if (!Directory.Exists(folder)) return;
            var history = Step1ExitReport.ParseAll(Directory.GetFiles(folder, "step1_rounds*.csv").Select(File.ReadAllText));
            rareLine = Step1ExitReport.RarestLine(history, roundPranks.Keys);
        }
        catch (Exception e) { Debug.LogWarning("[Step1PlaytestLog] rare line: " + e.Message); }
    }

    private void HandleGameOver(string winner)
    {
        ComputeRareLine(); // 先算（这局还没写进文件）
        lastWinner = winner;
        lastReason = manager != null ? manager.LastRoundReason : "";
        // 自动无干预检查（H10）时没人答题，由 Step1HandsOffCheck 记录。
        if (Step1HandsOffCheck.IsRunning) return;
        // S210：大地图一天里的房间不弹问卷（打完直接回小镇；问卷在单独试玩房间时照旧）
        // S217：快速测试模式不弹 5 道问卷（结算直接按 R / N）；反馈改用 F8 随手记
        // S227：这两种以前连一行都不记 → 第 1 步出口的局数/坑法一直停在 09-27。现在照样记一行（问卷列留空），不弹任何东西。
        if (OverworldSession.Active || Step1QuickTest.On || Step1QuickTest.UsedThisRound) { roundMode = OverworldSession.Active ? "town" : Step1QuickTest.UsedThisRound ? "f9" : "quick"; WriteRow(null); return; } // S236：开过 F9 的局只记一行（mode=f9），不弹问卷、不算出口
        roundMode = "room";
        awaitingRating = true;
        survey = new Step1RoundSurvey(CaughtThisRound > 0);
        noteDraft = "";
    }

    private void Update()
    {
        if (!awaitingRating && driver != null && driver.Mind != null && (manager == null || manager.CurrentState == GameState.Playing))
            roomNear.Feed(driver.Mind.Meter.Level != SuspicionLevel.Calm, driver.Mind.Meter.Normalized, RoundClock, "");
        if (!awaitingRating || survey == null) return;
        IsTyping = survey.Current == Step1RoundSurvey.Step.Note;
        if (IsTyping) return; // 文本在 OnGUI 里收
        if (Step1Screen.HelpOpen) return;
        if (Step1Keys.Down(KeyCode.Y)) survey.AnswerYesNo(true);
        else if (Step1Keys.Down(KeyCode.N)) survey.AnswerYesNo(false);
        int d = Step1Keys.Digit1to5(); // S236：两套输入都读（以前只读旧输入 → 有的机器按数字没反应，问卷卡住）
        if (d > 0) survey.AnswerNumber(d);
    }

    private void FinishSurvey()
    {
        survey.SubmitNote(noteDraft);
        WriteRow(survey);
        awaitingRating = false;
        savedThisRound = true;
        IsTyping = false;
    }

    public static string CsvHeader => "timestamp,round,winner,reason,seconds,trickster_lives_left,times_caught,omens,alerts,pranks,distinct_kinds," +
        "calculated_moment,near_miss_moment,caught_verdict,want_again_1to5,note,max_combo,layout_seed,stuck_rescues,combo_score,personality,laughed,tuning_version,mode"; // S227：加了 tuning_version（旧局另列，不混进结论）

    public static string CsvRow(DateTime time, int round, string winner, string reason, float seconds, int livesLeft,
        int caught, int omens, int alerts, IReadOnlyDictionary<string, int> pranks, Step1RoundSurvey answers, int maxCombo = 0, int layoutSeed = 0, int stuckRescues = 0, int comboScore = 0, string personality = "", int tuningVersion = 0, string mode = "")
    {
        var parts = new List<string>();
        foreach (var kv in pranks) parts.Add(kv.Key + ":" + kv.Value);
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", time.ToString("yyyy-MM-dd HH:mm:ss"), round, Clean(winner), Clean(reason),
            seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), livesLeft, caught, omens, alerts,
            string.Join(" ", parts), pranks.Count,
            Step1RoundSurvey.YesNo(answers?.Calculated), Step1RoundSurvey.YesNo(answers?.NearMiss), Clean(answers?.CaughtVerdict),
            answers != null ? answers.WantAgain : 0, Clean(answers?.Note), maxCombo, layoutSeed, stuckRescues, comboScore, Clean(personality), Step1RoundSurvey.YesNo(answers?.Laughed), tuningVersion, Clean(mode));
    }

    private static string Clean(string s) => (s ?? "").Replace(",", ";").Replace("\n", " ").Replace("\r", " ");

    private void WriteRow(Step1RoundSurvey answers)
    {
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", LogFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, LogFile);
            // S223：表头加了列（laughed）→ 旧文件改名留档，新文件重新写表头（不让新旧列错位）
            if (File.Exists(path))
            {
                string head;
                using (var r = new StreamReader(path)) head = r.ReadLine() ?? "";
                if (head != CsvHeader) File.Move(path, Path.Combine(folder, "step1_rounds_before_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"));
            }
            bool fresh = !File.Exists(path);
            var sb = new StringBuilder();
            if (fresh) sb.AppendLine(CsvHeader);
            sb.AppendLine(CsvRow(DateTime.Now, manager != null ? manager.CurrentRound : 0, lastWinner,
                manager != null ? manager.LastRoundReason : "", manager != null ? manager.RoundElapsed : 0f,
                lives != null ? lives.Lives : 0, CaughtThisRound, roundOmens, roundAlerts, roundPranks, answers, combo != null ? combo.MaxThisRound : 0, layout != null ? layout.LastSeed : 0, rescue != null ? rescue.RescuesThisRound : 0, combo != null ? combo.ScoreThisRound : 0, driver != null ? driver.Mind.Personality.ToString() : "", tuning != null ? tuning.dataVersion : 0, roundMode));
            File.AppendAllText(path, sb.ToString());
            roundsLogged++;
            Debug.Log($"[Step1PlaytestLog] Round logged ({roundsLogged}) -> {path}");
        }
        catch (Exception e) { Debug.LogWarning("[Step1PlaytestLog] Could not write log: " + e.Message); }
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen) return;
        float w = Step1Gui.Begin();
        float h = Step1Gui.VirtualHeight;
        var gm = manager;
        bool roundOver = gm != null && gm.CurrentState == GameState.RoundOver;

        // ── 左上角：只放 3 行 ──
        var sb = new StringBuilder();
        if (lives != null)
            sb.AppendLine($"你的命 Lives  <color=#FF7A7A>{new string('\u2665', Mathf.Max(0, lives.Lives))}</color><color=#555555>{new string('\u2665', Mathf.Max(0, lives.MaxLives - lives.Lives))}</color>" +
                          (lives.IsInvulnerable ? "  <color=#9AD0FF>(无敌 safe)</color>" : ""));
        if (gm != null) sb.AppendLine($"剩余时间 Time  <b>{Mathf.CeilToInt(Mathf.Max(0f, gm.GameTimer))}s</b>     第 {gm.CurrentRound} 局 Round");
        if (driver != null && driver.Mind != null)
            sb.Append("马里奥 Mario  <b>" + Step1Text.MarioStateText(driver.Mind.State, driver.IsWaitingToStart, LootObjective.IsLootCarried, driver.StartDelayRemaining, driver.Mind.IsStunned) + "</b>");
        if (Step1QuickTest.NoLimits) sb.Append("\n" + Step1QuickTest.NoLimitsBadge); // S236
        Step1Gui.Panel(new Rect(16f, 16f, 760f, 120f));
        GUI.Label(new Rect(32f, 24f, 740f, 110f), sb.ToString(), Step1Gui.Text(24));

        // ── 底部：一行按键 ──
        if (!roundOver)
        {
            Step1Gui.Panel(new Rect(0f, h - 54f, w, 54f), 0.6f);
            GUI.Label(new Rect(0f, h - 54f, w, 54f), CurrentControlsBar(), Step1Gui.Text(22, TextAnchor.MiddleCenter, false));
            return;
        }

        // ── 结算：结果 + 一次一个问题 ──
        var outcome = Step1Text.Classify(lastWinner, lastReason);
        var box = new Rect(w * 0.5f - 520f, h * 0.5f - 300f, 1040f, 600f);
        Step1Gui.Panel(box, 0.93f);
        string color = Step1Text.PlayerWon(outcome) ? "#7CFC7C" : "#FFB347";
        GUI.Label(new Rect(box.x, box.y + 24f, box.width, 100f), $"<color={color}><b>{Step1Text.Headline(outcome)}</b></color>",
            Step1Gui.Text(34, TextAnchor.MiddleCenter));
        var inner = new Rect(box.x + 40f, box.y + 150f, box.width - 80f, box.height - 180f);
        if (rareLine.Length > 0) GUI.Label(new Rect(box.x, box.y + 104f, box.width, 36f), "<color=#FFD966>" + rareLine + "</color>", Step1Gui.Text(22, TextAnchor.MiddleCenter));

        if (savedThisRound || survey == null)
        {
            GUI.Label(inner, savedThisRound || !Step1QuickTest.On ? Step1Text.AfterSurvey : Step1Text.QuickTestRoundOver, Step1Gui.Text(30, TextAnchor.MiddleCenter));
            return;
        }

        GUI.Label(new Rect(inner.x, inner.y - 10f, inner.width, 30f), $"<color=#AAAAAA>记录这一局 Quick feedback   {survey.StepNumber} / {survey.StepCount}</color>",
            Step1Gui.Text(20, TextAnchor.MiddleCenter));
        GUI.Label(new Rect(inner.x, inner.y + 30f, inner.width, 90f), "<b>" + survey.Prompt + "</b>", Step1Gui.Text(28, TextAnchor.MiddleCenter));
        if (survey.Current == Step1RoundSurvey.Step.NearMiss) // S224：给参考（他这局起疑过几次），答案还是你自己选
        {
            var top = roomNear.Closest(1);
            GUI.Label(new Rect(inner.x, inner.y + 118f, inner.width, 30f), "<color=#FFD966>" + Step1Text.RoomNearMissHint(roomNear.NearMisses, top.Count > 0 ? top[0].peak : 0f) + "</color>", Step1Gui.Text(20, TextAnchor.MiddleCenter));
        }

        if (survey.Current == Step1RoundSurvey.Step.Note)
        {
            Event e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            var field = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleLeft };
            if (Step1Gui.Font != null) field.font = Step1Gui.Font;
            GUI.SetNextControlName("Step1Note");
            noteDraft = GUI.TextField(new Rect(inner.x + 40f, inner.y + 150f, inner.width - 80f, 56f), noteDraft, 200, field);
            GUI.FocusControl("Step1Note");
            var btn = Button(24);
            if (GUI.Button(new Rect(inner.center.x - 180f, inner.y + 240f, 360f, 70f), "保存 Save  [Enter]", btn) || enter)
            { FinishSurvey(); if (enter) e.Use(); }
            return;
        }

        string[] options = survey.Options;
        float gap = 16f;
        float bw = Mathf.Min(300f, (inner.width - gap * (options.Length - 1)) / options.Length);
        float total = bw * options.Length + gap * (options.Length - 1);
        float x0 = inner.center.x - total * 0.5f;
        var style = Button(options.Length > 2 ? 21 : 26);
        for (int i = 0; i < options.Length; i++)
            if (GUI.Button(new Rect(x0 + i * (bw + gap), inner.y + 150f, bw, 110f), options[i], style)) survey.Choose(i);
        GUI.Label(new Rect(inner.x, inner.y + 290f, inner.width, 40f), "<color=#AAAAAA>点按钮或按括号里的键  Click or press the key</color>",
            Step1Gui.Text(20, TextAnchor.MiddleCenter));
    }

    private static readonly Dictionary<int, GUIStyle> buttonCache = new Dictionary<int, GUIStyle>();
    private static GUIStyle Button(int size)
    {
        if (buttonCache.TryGetValue(size, out var cached) && cached != null) return cached;
        var st = new GUIStyle(GUI.skin.button) { fontSize = size, wordWrap = true, richText = true };
        if (Step1Gui.Font != null) st.font = Step1Gui.Font;
        buttonCache[size] = st;
        return st;
    }

    private string Describe()
    {
        if (roundPranks.Count == 0) return "-";
        var parts = new List<string>();
        foreach (var kv in roundPranks) parts.Add(kv.Key + " x" + kv.Value);
        return string.Join(", ", parts);
    }
}
