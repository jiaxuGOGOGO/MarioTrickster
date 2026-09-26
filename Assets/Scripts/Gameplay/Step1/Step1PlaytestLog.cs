using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步退出条件的记录工具：连玩 20 局还想玩 + 至少 3 种不同的恶作剧方式。
/// - 恶作剧归因：捣蛋者触发某个机关后 prankAttributionSeconds 内马里奥受伤 → 记一次该机关类型的恶作剧；
///   马里奥追丢（Chasing → Searching → Running 且没抓到）→ 记一次 "Escape"。
/// - 回合结束：屏幕上答宪法第 3 层每局问卷（Step1RoundSurvey：算准了/差点被发现/被抓服气吗/想再来 1–5/一句话），
///   写入 PlaytestLogs/step1_rounds.csv（在 Assets 外，不进 git）。问卷答完之前 R/N 被屏蔽，避免误开下一局。
/// - 左上角 HUD（英文，避开中文字体风险）。
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
    private TricksterLives lives;
    private TricksterAbilitySystem abilities;
    private Step1RoomCamera roomCamera;
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
    private GUIStyle style;

    public IReadOnlyDictionary<string, int> RoundPranks => roundPranks;
    public int DistinctKindsThisSession => sessionKinds.Count;

    /// <summary>把机关组件映射成恶作剧类别（供归因和测试使用）。</summary>
    public static string PrankKindOf(IControllableProp prop)
    {
        if (prop == null) return "";
        if (prop is FireTrap) return "Fire";
        if (prop is ControllableBlocker) return "Blocker";
        if (prop is CollapsingPlatform) return "Collapse";
        return prop.GetType().Name;
    }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        driver = FindObjectOfType<MarioMindDriver>();
        lives = FindObjectOfType<TricksterLives>();
        roomCamera = FindObjectOfType<Step1RoomCamera>();
        var figure = FindObjectOfType<TricksterController>();
        abilities = figure != null ? figure.AbilitySystem : null;
        if (abilities != null) abilities.OnPropActivated += HandleProp;
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
        if (driver != null) { driver.Hurt -= HandleHurt; driver.Caught -= HandleCaught; if (driver.Mind != null) driver.Mind.StateChanged -= HandleState; }
        if (manager != null) { manager.OnRoundStart -= BeginRound; manager.OnGameOver -= HandleGameOver; }
        GameManager.BlockRoundOverKeys = null;
        IsTyping = false;
    }

    private void BeginRound()
    {
        roundPranks.Clear(); roundOmens = roundAlerts = roundCaught = 0;
        chaseOpen = false; awaitingRating = false; survey = null; IsTyping = false; noteDraft = ""; lastPropKind = ""; lastPropTime = -999f;
    }

    private void HandleProp(IControllableProp prop) { lastPropKind = PrankKindOf(prop); lastPropTime = Time.time; }

    private void HandleHurt(MarioMindState state)
    {
        if (!string.IsNullOrEmpty(lastPropKind) && Time.time - lastPropTime <= tuning.prankAttributionSeconds)
        { Count(lastPropKind); lastPropKind = ""; }
    }

    private void HandleCaught() { roundCaught++; chaseOpen = false; }

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

    private void HandleGameOver(string winner)
    {
        lastWinner = winner;
        // 自动无干预检查（H10）时没人答题，由 Step1HandsOffCheck 记录。
        if (Step1HandsOffCheck.IsRunning) return;
        awaitingRating = true;
        survey = new Step1RoundSurvey(CaughtThisRound > 0);
        noteDraft = "";
    }

    private void Update()
    {
        if (!awaitingRating || survey == null) return;
        IsTyping = survey.Current == Step1RoundSurvey.Step.Note;
        if (IsTyping) return; // 文本在 OnGUI 里收
        if (Input.GetKeyDown(KeyCode.Y)) survey.AnswerYesNo(true);
        else if (Input.GetKeyDown(KeyCode.N)) survey.AnswerYesNo(false);
        for (int i = 1; i <= 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) { survey.AnswerNumber(i); break; }
    }

    private void FinishSurvey()
    {
        survey.SubmitNote(noteDraft);
        WriteRow(survey);
        awaitingRating = false;
        IsTyping = false;
    }

    public static string CsvHeader => "timestamp,round,winner,reason,seconds,trickster_lives_left,times_caught,omens,alerts,pranks,distinct_kinds," +
        "calculated_moment,near_miss_moment,caught_verdict,want_again_1to5,note";

    public static string CsvRow(DateTime time, int round, string winner, string reason, float seconds, int livesLeft,
        int caught, int omens, int alerts, IReadOnlyDictionary<string, int> pranks, Step1RoundSurvey answers)
    {
        var parts = new List<string>();
        foreach (var kv in pranks) parts.Add(kv.Key + ":" + kv.Value);
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", time.ToString("yyyy-MM-dd HH:mm:ss"), round, Clean(winner), Clean(reason),
            seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), livesLeft, caught, omens, alerts,
            string.Join(" ", parts), pranks.Count,
            Step1RoundSurvey.YesNo(answers?.Calculated), Step1RoundSurvey.YesNo(answers?.NearMiss), Clean(answers?.CaughtVerdict),
            answers != null ? answers.WantAgain : 0, Clean(answers?.Note));
    }

    private static string Clean(string s) => (s ?? "").Replace(",", ";").Replace("\n", " ").Replace("\r", " ");

    private void WriteRow(Step1RoundSurvey answers)
    {
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", LogFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, LogFile);
            bool fresh = !File.Exists(path);
            var sb = new StringBuilder();
            if (fresh) sb.AppendLine(CsvHeader);
            sb.AppendLine(CsvRow(DateTime.Now, manager != null ? manager.CurrentRound : 0, lastWinner,
                manager != null ? manager.LastRoundReason : "", manager != null ? manager.RoundElapsed : 0f,
                lives != null ? lives.Lives : 0, CaughtThisRound, roundOmens, roundAlerts, roundPranks, answers));
            File.AppendAllText(path, sb.ToString());
            roundsLogged++;
            Debug.Log($"[Step1PlaytestLog] Round logged ({roundsLogged}) -> {path}");
        }
        catch (Exception e) { Debug.LogWarning("[Step1PlaytestLog] Could not write log: " + e.Message); }
    }

    private void OnGUI()
    {
        if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true };
        var sb = new StringBuilder();
        sb.AppendLine("<b>STEP 1 - PRANK ROOM</b>");
        if (lives != null) sb.AppendLine($"Trickster lives: {new string('\u2665', Mathf.Max(0, lives.Lives))}{(lives.IsInvulnerable ? "  (safe)" : "")}");
        if (driver != null && driver.Mind != null) sb.AppendLine($"Mario: {driver.Mind.State}  suspicion {driver.Mind.Meter.Value:F0}");
        if (manager != null) sb.AppendLine($"Time: {manager.GameTimer:F0}s   Round {manager.CurrentRound}   Logged {roundsLogged}");
        sb.AppendLine($"Pranks this round: {Describe()}   Kinds this session: {sessionKinds.Count}/3");
        if (roomCamera != null) sb.AppendLine($"Camera: {roomCamera.Mode}  [C] cycle");
        sb.AppendLine("Arrows move / Up jump  P disguise  O/I switch  L trigger");
        sb.AppendLine("Mario: ? = huh  ! = checking  !! = sees you  ?! = lost you");
        sb.Append("R restart  N next round  Esc pause");
        GUI.Box(new Rect(10, 10, 490, 170), sb.ToString(), style);

        if (awaitingRating && survey != null)
        {
            var big = new GUIStyle(style) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            var rect = new Rect(Screen.width * 0.5f - 330, Screen.height * 0.5f - 90, 660, 180);
            GUI.Box(rect, $"<b>{lastWinner} wins</b>   (answer to save this round)\n\n{survey.Prompt}", big);
            if (survey.Current == Step1RoundSurvey.Step.Note)
            {
                Event e = Event.current;
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
                GUI.SetNextControlName("Step1Note");
                noteDraft = GUI.TextField(new Rect(rect.x + 30, rect.yMax - 45, rect.width - 60, 30), noteDraft, 200);
                GUI.FocusControl("Step1Note");
                if (enter) { FinishSurvey(); e.Use(); }
            }
        }
    }

    private string Describe()
    {
        if (roundPranks.Count == 0) return "-";
        var parts = new List<string>();
        foreach (var kv in roundPranks) parts.Add(kv.Key + " x" + kv.Value);
        return string.Join(", ", parts);
    }
}
