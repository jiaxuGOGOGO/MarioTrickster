using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// S217：试玩时按 F8 = 记一条反馈（截图 + 当时的情况：哪个场景、几点、第几局、马里奥在干嘛），不打断游戏、不用打字。
/// 游戏里出的红字错误也自动记下（同一条只记一次）。全部写在项目根目录 PlaytestLogs/Feedback/（不进 git）。
/// 测试中心 → "打包反馈" 把截图、说明、错误、体检报告、测试报告打成一个 zip，直接发给 AI。
/// 纯记录，不碰玩法（H4）。每个场景自动常驻一个（RuntimeInitializeOnLoadMethod），不用往场景里拖。
/// </summary>
public sealed class Step1Feedback : MonoBehaviour
{
    public const string Folder = "PlaytestLogs/Feedback";
    public const string LogName = "feedback.md";
    public const int MaxErrors = 40;

    /// <summary>当前场景补充一句"此刻的情况"（小镇：几点/下一扇门/你在哪；房间自己算）。</summary>
    public static System.Func<string> Context;

    private static Step1Feedback inst;
    private static int errors;
    private static readonly System.Collections.Generic.HashSet<string> seenErrors = new System.Collections.Generic.HashSet<string>();
    private string toast = ""; private float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; errors = 0; seenErrors.Clear(); Context = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("Step1Feedback");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<Step1Feedback>();
        Application.logMessageReceived += OnLog;
    }

    private void OnDestroy() { if (inst == this) { Application.logMessageReceived -= OnLog; inst = null; } }

    public static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Folder);

    /// <summary>已记下几条（数截图）。</summary>
    public static int Count() => Directory.Exists(Root) ? Directory.GetFiles(Root, "feedback_*.png").Length : 0;

    private void Update()
    {
        if (Step1PlaytestLog.IsTyping) return;
        if (Step1Keys.Down(KeyCode.F8)) Capture();
    }

    /// <summary>S240：别的系统（卡住救援）自动记一条反馈：截图 + 一句说明。没有常驻实例时什么都不做。</summary>
    public static void CaptureNote(string note) { if (inst != null) inst.Capture(note); }

    private void Capture() => Capture(null);

    private void Capture(string note)
    {
        try
        {
            Directory.CreateDirectory(Root);
            int n = Count() + 1;
            string png = Path.Combine(Root, $"feedback_{n:000}.png");
            ScreenCapture.CaptureScreenshot(png);
            Append($"\n## 反馈 {n}  {System.DateTime.Now:MM-dd HH:mm:ss}\n- 截图：feedback_{n:000}.png\n- 场景：{gameObject.scene.name}/{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n- 情况：{Describe()}\n- 你的话：{(note ?? "（在测试中心里补一句，可不写）")}\n");
            if (note == null) Toast(Step1Text.FeedbackSaved(n));
        }
        catch (System.Exception e) { Debug.LogWarning("[Step1Feedback] " + e.Message); }
    }

    /// <summary>此刻的情况（一行）。只读公开状态，F8 按下时才算（不在 Update 里找物体）。</summary>
    private static string Describe()
    {
        var sb = new StringBuilder();
        if (Context != null) { try { sb.Append(Context()); } catch { } }
        var gm = GameManager.Instance;
        if (gm != null) sb.Append($" 第{gm.CurrentRound}局 剩{Mathf.CeilToInt(Mathf.Max(0f, gm.GameTimer))}秒 状态{gm.CurrentState}");
        var d = FindObjectOfType<MarioMindDriver>();
        if (d != null && d.Mind != null) sb.Append($" 马里奥:{d.Mind.State} 在({d.transform.position.x:0.0},{d.transform.position.y:0.0})");
        var t = FindObjectOfType<TricksterController>();
        if (t != null) sb.Append($" 你在({t.transform.position.x:0.0},{t.transform.position.y:0.0})");
        sb.Append($" timeScale={Time.timeScale:0.##}");
        return sb.ToString().Trim();
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (errors >= MaxErrors || msg.StartsWith("[Step1Feedback]")) return;
        string key = msg.Length > 160 ? msg.Substring(0, 160) : msg;
        if (!seenErrors.Add(key)) return;
        errors++;
        string first = (stack ?? "").Split('\n')[0];
        Append($"\n## 错误 {errors}  {System.DateTime.Now:MM-dd HH:mm:ss}\n- {type}: {key}\n- 位置：{first}\n- 情况：{Describe()}\n");
        if (inst != null && errors == 1) inst.Toast(Step1Text.FeedbackError);
    }

    private static void Append(string text)
    {
        try
        {
            Directory.CreateDirectory(Root);
            string p = Path.Combine(Root, LogName);
            if (!File.Exists(p)) File.WriteAllText(p, "# 试玩反馈（F8 截图 + 自动记下的错误）\n");
            File.AppendAllText(p, text);
        }
        catch { }
    }

    private void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 2.5f; }

    private void OnGUI()
    {
        if (Time.unscaledTime > toastUntil) return;
        GUI.depth = -900;
        var r = new Rect(Screen.width - 430, Screen.height - 120, 420, 70);
        GUI.Box(r, "");
        GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), toast, Step1Gui.Text(15, TextAnchor.MiddleLeft));
    }
}
