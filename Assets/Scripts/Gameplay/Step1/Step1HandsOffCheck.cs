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

    [SerializeField] private MarioMindTuningSO tuning;

    public struct RoundResult { public string winner, reason; public float seconds; public Vector2 marioPos; public bool hadLoot; }

    public static bool IsRunning { get; private set; }
    private readonly List<RoundResult> results = new List<RoundResult>();
    private GameManager manager;
    private float nextRoundAt = -1f;
    private bool finished;
    private bool lootSeenThisRound;
    private GUIStyle style;

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
        if (!IsRunning) return;
        PlayerPrefs.DeleteKey(RequestKey);
        PlayerPrefs.Save();
        // 在所有 Start 之前让捣蛋者退场：马里奥的眼睛/裁判都找不到它，等于"玩家完全不操作、也不在场"。
        var figure = FindObjectOfType<TricksterController>();
        if (figure != null) figure.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (!IsRunning) { enabled = false; return; }
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        if (manager != null) manager.OnGameOver += HandleGameOver;
        LootObjective.OnLootCollected += HandleLoot;
    }

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
            marioPos = manager.LastRoundPosition, hadLoot = lootSeenThisRound || LootObjective.IsLootCarried
        });
        if (results.Count >= Mathf.Max(1, tuning.autoCheckRounds)) Finish();
        else nextRoundAt = Time.unscaledTime + tuning.autoCheckRoundGapSeconds;
    }

    private void Update()
    {
        if (finished || manager == null) return;
        // GameManager.StartGame 每局会把 timeScale 设回 1，这里每帧保持检查倍速。
        if (manager.CurrentState == GameState.Playing) Time.timeScale = Mathf.Max(0.1f, tuning.autoCheckTimeScale);
        if (nextRoundAt > 0f && Time.unscaledTime >= nextRoundAt)
        {
            nextRoundAt = -1f;
            lootSeenThisRound = false;
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
            if (fresh) sb.AppendLine("timestamp,check_round,winner,got_loot,reason,seconds,mario_x,mario_y");
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.AppendLine(string.Join(",", stamp, i + 1, r.winner, r.hadLoot ? "yes" : "no", (r.reason ?? "").Replace(",", ";"),
                    r.seconds.ToString("F1", inv), r.marioPos.x.ToString("F1", inv), r.marioPos.y.ToString("F1", inv)));
            }
            File.AppendAllText(path, sb.ToString());
            Debug.Log($"[Step1 H10] Mario cleared {MarioClears(results)}/{results.Count} rounds without interference -> {path}");
        }
        catch (Exception e) { Debug.LogWarning("[Step1 H10] Could not write log: " + e.Message); }
    }

    private void OnGUI()
    {
        if (!IsRunning) return;
        if (style == null) style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
        int total = Mathf.Max(1, tuning != null ? tuning.autoCheckRounds : 5);
        var sb = new StringBuilder();
        sb.AppendLine("<b>HANDS-OFF CHECK (H10)</b> - don't touch anything");
        sb.AppendLine($"Mario cleared {MarioClears(results)} / {results.Count}   (round {Mathf.Min(results.Count + 1, total)} of {total})");
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            bool ok = r.winner == "Mario" && r.hadLoot;
            sb.AppendLine($"#{i + 1}: {(ok ? "<color=#7CFC00>CLEAR</color>" : "<color=#FF6060>STUCK/FAIL</color>")}  {r.seconds:F0}s  at x={r.marioPos.x:F0} y={r.marioPos.y:F0}");
        }
        if (finished) sb.Append("<b>Done.</b> Stop Play. Result saved to PlaytestLogs/step1_handsoff.csv");
        GUI.Box(new Rect(Screen.width * 0.5f - 300, 20, 600, 70 + 26 * (results.Count + (finished ? 1 : 0))), sb.ToString(), style);
    }
}
