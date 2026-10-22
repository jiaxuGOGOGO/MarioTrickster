using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// S181：宪法 H10"无玩家干预时马里奥通关率 ≥95%"的一键检查（菜单 MarioTrickster → Step 1 → Hands-off Check）。
/// 编辑器菜单写入 PlayerPrefs 请求标记后进入 Play；本组件读到标记就：
///   - 让捣蛋者退场（隐藏 = 不存在，马里奥看不到也抓不到），不需要用户松开键盘之类的操作；
///   - 自动连跑 autoCheckRounds 局（每局结束停 autoCheckRoundGapSeconds 真实秒后自动开下一局）；
///   - 每局记下：谁赢、原因、用时、马里奥最后位置、有没有拿到宝；写入 PlaytestLogs/step1_handsoff.csv；
///   - 屏幕显示 "Mario cleared x / N"，结束后恢复正常速度。
/// 本类只做检查与记录，不改变马里奥任何决策。
/// </summary>
public class Step1HandsOffCheck : MonoBehaviour
{
    public const string RequestKey = "MarioTrickster.Step1.HandsOffRequested";
    public const string LogFile = "step1_handsoff.csv";
    /// <summary>S202：陷阱试探模式（捣蛋者由 Step1TrapProbe 按固定策略操作，测"机关连起来会不会把马里奥坑死/卡死"）。</summary>
    public const string ProbeKey = "MarioTrickster.Step1.TrapProbeRequested";
    /// <summary>S202：每局马里奥走过的格子（工坊"自动检查轨迹"热力图用）。</summary>
    public const string TrackFile = "step1_track.txt";
    public static bool ProbeMode { get; private set; }

    [SerializeField] private MarioMindTuningSO tuning;
    [Tooltip("S207：构建时按这张图的路线长度算出的超时（秒，0 = 用调参里的 autoCheckRoundTimeoutSeconds）")]
    [SerializeField] private float roomTimeoutSeconds;
    public float RoundTimeout => Mathf.Max(tuning != null ? tuning.autoCheckRoundTimeoutSeconds : 70f, roomTimeoutSeconds);

    public struct RoundResult { public string winner, reason; public float seconds; public Vector2 marioPos; public bool hadLoot; public int rescues; public int hurts; public string personality; }

    public static bool IsRunning { get; private set; }
    private readonly List<RoundResult> results = new List<RoundResult>();
    private GameManager manager;
    private float nextRoundAt = -1f;
    private bool finished;
    private bool lootSeenThisRound;
    private MarioController mario;
    private float roundStartedAt;

    public IReadOnlyList<RoundResult> Results => results;

    /// <summary>纯函数：通关局数（马里奥拿宝回到出口）。供测试与显示使用。</summary>
    public static int MarioClears(IReadOnlyList<RoundResult> rs)
    {
        int n = 0;
        foreach (var r in rs) if (r.winner == "Mario" && r.hadLoot) n++;
        return n;
    }

    private void Awake()
    {
        IsRunning = PlayerPrefs.GetInt(RequestKey, 0) == 1;
        ProbeMode = IsRunning && PlayerPrefs.GetInt(ProbeKey, 0) == 1;
        if (!IsRunning) return;
        PlayerPrefs.DeleteKey(RequestKey);
        PlayerPrefs.DeleteKey(ProbeKey);
        PlayerPrefs.Save();
        // 在所有 Start 之前让捣蛋者退场：马里奥的眼睛/裁判都找不到它，等于"玩家完全不操作、也不在场"。
        // S202 陷阱试探也必须退场：Step1TrapProbe 挂在马里奥身上，直接按相同预判触发机关；
        // 若把可见的捣蛋者留在路线旁，结果会被“马里奥追到人”主导，不能回答机关本身会不会把他坑死或卡住。
        var figure = FindObjectOfType<TricksterController>();
        if (figure != null) figure.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (!IsRunning) { enabled = false; return; }
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        mario = FindObjectOfType<MarioController>();
        if (manager != null) manager.OnGameOver += HandleGameOver;
        roundStartedAt = Time.time;
        LootObjective.OnLootCollected += HandleLoot;
        rescue = FindObjectOfType<Step1StuckRescue>();
        var driver = FindObjectOfType<MarioMindDriver>();
        driverRef = driver;
        if (driver != null) driver.Hurt += HandleHurt;
        if (ProbeMode && mario != null) mario.gameObject.AddComponent<Step1TrapProbe>().SetTuning(tuning);
    }

    private Step1StuckRescue rescue;
    private MarioMindDriver driverRef;
    private int hurtsThisRound;
    private readonly HashSet<int> trackCells = new HashSet<int>();
    private readonly HashSet<int> trackStuck = new HashSet<int>();
    private void HandleHurt(MarioMindState s) { hurtsThisRound++; }

    /// <summary>纯逻辑：轨迹文件一行一格 "x,y,次数"，stuck 行以 "S," 开头。</summary>
    public static string TrackText(string roomHash, IDictionary<int, int> visits, IEnumerable<int> stuck)
    {
        var sb = new StringBuilder();
        sb.Append("# room ").AppendLine(roomHash ?? "");
        foreach (var kv in visits) sb.Append(kv.Key / 1000).Append(',').Append(kv.Key % 1000).Append(',').Append(kv.Value).AppendLine();
        foreach (int k in stuck) sb.Append("S,").Append(k / 1000).Append(',').Append(k % 1000).AppendLine();
        return sb.ToString();
    }

    /// <summary>纯逻辑：读轨迹文件。</summary>
    public static (string room, Dictionary<int, int> visits, HashSet<int> stuck) ParseTrack(string text)
    {
        var visits = new Dictionary<int, int>(); var stuck = new HashSet<int>(); string room = "";
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("# room ")) { room = line.Substring(7).Trim(); continue; }
            var p = line.Split(',');
            if (p.Length == 3 && p[0] == "S" && int.TryParse(p[1], out int sx) && int.TryParse(p[2], out int sy)) stuck.Add(sx * 1000 + sy);
            else if (p.Length == 3 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) && int.TryParse(p[2], out int n)) visits[x * 1000 + y] = n;
        }
        return (room, visits, stuck);
    }

    private readonly Dictionary<int, int> visits = new Dictionary<int, int>();
    private int lastTrackKey = -1;

    private void OnDestroy()
    {
        if (manager != null) manager.OnGameOver -= HandleGameOver;
        LootObjective.OnLootCollected -= HandleLoot;
        if (IsRunning) Time.timeScale = 1f;
        IsRunning = false;
    }

    private void HandleLoot() { lootSeenThisRound = true; }

    private void HandleGameOver(string winner)
    {
        results.Add(new RoundResult
        {
            winner = winner, reason = manager.LastRoundReason, seconds = manager.RoundElapsed,
            marioPos = manager.LastRoundPosition, hadLoot = lootSeenThisRound || LootObjective.IsLootCarried,
            rescues = rescue != null ? rescue.RescuesThisRound : 0, hurts = hurtsThisRound,
            personality = driverRef != null ? driverRef.Mind.Traits.zh : ""
        });
        if (rescue != null && rescue.RescuesThisRound > 0) trackStuck.Add(Mathf.RoundToInt(rescue.LastStuckAt.x) * 1000 + Mathf.RoundToInt(rescue.LastStuckAt.y));
        if (manager.LastRoundReason == Step1Text.HandsOffTimeoutReason) trackStuck.Add(Mathf.RoundToInt(manager.LastRoundPosition.x) * 1000 + Mathf.RoundToInt(manager.LastRoundPosition.y));
        hurtsThisRound = 0;
        if (results.Count >= Mathf.Max(1, tuning.autoCheckRounds)) Finish();
        else nextRoundAt = Time.unscaledTime + tuning.autoCheckRoundGapSeconds;
    }

    private void Update()
    {
        if (finished || manager == null) return;
        // GameManager.StartGame 每局会把 timeScale 设回 1，这里每帧保持检查倍速。
        if (manager.CurrentState == GameState.Playing && mario != null)
        {
            int k = Mathf.RoundToInt(mario.transform.position.x) * 1000 + Mathf.Max(0, Mathf.RoundToInt(mario.transform.position.y));
            if (k != lastTrackKey) { lastTrackKey = k; visits.TryGetValue(k, out int n); visits[k] = n + 1; }
        }
        if (manager.CurrentState == GameState.Playing)
        {
            Time.timeScale = Mathf.Max(0.1f, tuning.autoCheckTimeScale);
            // S182：马里奥卡住时不要一直干等到 150 秒倒计时，超时即记为卡住。
            if (Time.time - roundStartedAt > RoundTimeout)
                manager.EndRound("Trickster", Step1Text.HandsOffTimeoutReason);
        }
        if (nextRoundAt > 0f && Time.unscaledTime >= nextRoundAt)
        {
            nextRoundAt = -1f;
            lootSeenThisRound = false;
            roundStartedAt = Time.time;
            manager.ResetRound();
        }
    }

    private void Finish()
    {
        finished = true;
        Time.timeScale = 1f;
        try
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Step1PlaytestLog.LogFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, LogFile);
            bool fresh = !File.Exists(path);
            var sb = new StringBuilder();
            if (fresh) sb.AppendLine("timestamp,check_round,winner,got_loot,reason,seconds,mario_x,mario_y,mode,rescues,hurts,personality");
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.AppendLine(string.Join(",", stamp, i + 1, r.winner, r.hadLoot ? "yes" : "no", (r.reason ?? "").Replace(",", ";"),
                    r.seconds.ToString("F1", inv), r.marioPos.x.ToString("F1", inv), r.marioPos.y.ToString("F1", inv),
                    ProbeMode ? "probe" : "handsoff", r.rescues, r.hurts, r.personality ?? ""));
            }
            File.AppendAllText(path, sb.ToString());
            // S202：轨迹（覆盖写，只保留最近一次检查）——工坊"自动检查轨迹"按钮读它画热力图
            File.WriteAllText(Path.Combine(folder, TrackFile), TrackText(StrategySim.Hash(string.Join("\n", Step1PrankRoomBuilderBridge.CurrentRoom ?? new string[0])), visits, trackStuck));
            Debug.Log($"[Step1 H10] Mario cleared {MarioClears(results)}/{results.Count} rounds without interference -> {path}");
        }
        catch (Exception e) { Debug.LogWarning("[Step1 H10] Could not write log: " + e.Message); }
    }

    /// <summary>一局的中文+英文结论（纯函数，供显示与测试）。</summary>
    public static string Describe(RoundResult r)
    {
        string extra = (r.hurts > 0 ? $"  被坑 {r.hurts} 次" : "") + (r.rescues > 0 ? $"  <color=#FFB347>救援 {r.rescues} 次</color>" : "");
        if (r.winner == "Mario" && r.hadLoot) return $"<color=#7CFC7C>\u2713 通关 Cleared</color>   {r.seconds:F0}s{extra}";
        string where = $"(x={r.marioPos.x:F0}, y={r.marioPos.y:F0})";
        if (r.reason == Step1Text.HandsOffTimeoutReason)
            return $"<color=#FF7070>\u2717 卡住 Stuck</color>   {(r.hadLoot ? "拿到宝后 after loot" : "没拿到宝 before loot")} {where}{extra}";
        if (r.winner == "Trickster" && (r.reason ?? "").StartsWith("Health")) return $"<color=#FFB347>\u2717 被坑倒 KO</color>   {where}{extra}";
        return $"<color=#FF7070>\u2717 失败 Failed</color>   {where}{extra}";
    }

    private void OnGUI()
    {
        if (!IsRunning) return;
        float w = Step1Gui.Begin();
        int total = Mathf.Max(1, tuning != null ? tuning.autoCheckRounds : 5);
        int clears = MarioClears(results);
        var sb = new StringBuilder();
        if (ProbeMode)
        {
            sb.AppendLine("<b>陷阱试探：机关连起来坑他，会不会把他坑死/卡死</b>");
            sb.AppendLine("<b>Trap probe: can chained traps kill or trap Mario?</b>");
            sb.AppendLine("<color=#BBBBBB>测试替身只在他走到时按 L；你的角色已移出房间，避免抓人干扰机关结果  Probe presses L; player is removed</color>");
        }
        else
        {
            sb.AppendLine("<b>自动检查：马里奥能不能自己通关</b>");
            sb.AppendLine("<b>Auto check: can Mario finish on his own?</b>");
            sb.AppendLine("<color=#BBBBBB>你不用操作，看着就行（你的角色已移出房间）  Just watch — you're removed from the room</color>");
        }
        sb.AppendLine();
        for (int i = 0; i < total; i++)
        {
            if (i < results.Count) sb.AppendLine($"第 {i + 1} 局 Round {i + 1}  [{results[i].personality}]:   {Describe(results[i])}");
            else if (i == results.Count && !finished) sb.AppendLine($"第 {i + 1} 局 Round {i + 1}:   <color=#FFD966>进行中… running…</color>");
            else sb.AppendLine($"<color=#777777>第 {i + 1} 局 Round {i + 1}:   —</color>");
        }
        if (finished)
        {
            sb.AppendLine();
            string verdict = clears == results.Count
                ? "<color=#7CFC7C><b>\u2713 合格 PASS：马里奥每局都能自己通关</b></color>"
                : $"<color=#FF7070><b>\u2717 不合格 FAIL：{results.Count - clears} 局没通关</b></color>";
            sb.AppendLine(verdict);
            sb.Append("<color=#BBBBBB>完成，可以点 Stop 停止。  Done — press Stop.</color>");
        }
        float height = 210f + 36f * total + (finished ? 90f : 0f);
        var r = new Rect(w * 0.5f - 480f, 20f, 960f, height);
        Step1Gui.Panel(r, 0.88f);
        GUI.Label(new Rect(r.x + 30f, r.y + 20f, r.width - 60f, r.height - 30f), sb.ToString(), Step1Gui.Text(24));
    }
}
