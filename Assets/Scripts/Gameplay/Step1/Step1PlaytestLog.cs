using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步退出条件的记录工具：连玩 20 局还想玩 + 至少 3 种不同的恶作剧方式。
/// - 恶作剧归因：捣蛋者触发某个机关后 prankAttributionSeconds 内马里奥受伤 → 记一次该机关类型的恶作剧；
///   马里奥追丢（Chasing → Searching → Running 且没抓到）→ 记一次 "Escape"。
/// - 回合结束：屏幕提示按 1–5 打分（还想再玩一局吗），写入 PlaytestLogs/step1_playtest.csv（在 Assets 外，不进 git）。
/// - 左上角 HUD（英文，避开中文字体风险）。
/// 本类只做记录与显示，不影响马里奥决策。
/// </summary>
public class Step1PlaytestLog : MonoBehaviour
{
    public const string LogFolder = "PlaytestLogs";
    public const string LogFile = "step1_playtest.csv";

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
        BeginRound();
    }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnPropActivated -= HandleProp;
        if (driver != null) { driver.Hurt -= HandleHurt; driver.Caught -= HandleCaught; if (driver.Mind != null) driver.Mind.StateChanged -= HandleState; }
        if (manager != null) { manager.OnRoundStart -= BeginRound; manager.OnGameOver -= HandleGameOver; }
    }

    private void BeginRound()
    {
        roundPranks.Clear(); roundOmens = roundAlerts = roundCaught = 0;
        chaseOpen = false; awaitingRating = false; lastPropKind = ""; lastPropTime = -999f;
    }

    private void HandleProp(IControllableProp prop) { lastPropKind = PrankKindOf(prop); lastPropTime = Time.time; }

    private void HandleHurt(MarioMindState state)
    {
        if (!string.IsNullOrEmpty(lastPropKind) && Time.time - lastPropTime <= tuning.prankAttributionSeconds)
        { Count(lastPropKind); lastPropKind = ""; }
    }

    private void HandleCaught() { roundCaught++; chaseOpen = false; }

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

    private void HandleGameOver(string winner) { lastWinner = winner; awaitingRating = true; }

    private void Update()
    {
        if (!awaitingRating) return;
        for (int i = 1; i <= 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) { WriteRow(i); awaitingRating = false; break; }
    }

    public static string CsvHeader => "timestamp,round,winner,reason,seconds,trickster_lives_left,times_caught,omens,alerts,pranks,distinct_kinds,want_again_1to5";

    public static string CsvRow(DateTime time, int round, string winner, string reason, float seconds, int livesLeft,
        int caught, int omens, int alerts, IReadOnlyDictionary<string, int> pranks, int rating)
    {
        var parts = new List<string>();
        foreach (var kv in pranks) parts.Add(kv.Key + ":" + kv.Value);
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", time.ToString("yyyy-MM-dd HH:mm:ss"), round, Clean(winner), Clean(reason),
            seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture), livesLeft, caught, omens, alerts,
            string.Join(" ", parts), pranks.Count, rating);
    }

    private static string Clean(string s) => (s ?? "").Replace(",", ";").Replace("\n", " ");

    private void WriteRow(int rating)
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
                lives != null ? lives.Lives : 0, roundCaught, roundOmens, roundAlerts, roundPranks, rating));
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
        sb.Append("R restart  N next round  Esc pause");
        GUI.Box(new Rect(10, 10, 470, 150), sb.ToString(), style);

        if (awaitingRating)
        {
            var big = new GUIStyle(style) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(Screen.width * 0.5f - 260, Screen.height * 0.5f - 60, 520, 120),
                $"<b>{lastWinner} wins</b>\nWant to play another round?\nPress 1 (no) ... 5 (yes, right now!)", big);
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
