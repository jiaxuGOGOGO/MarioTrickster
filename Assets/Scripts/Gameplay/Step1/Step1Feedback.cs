using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// S217：试玩时按 F8 = 记一条反馈（截图 + 当时的情况：哪个场景、几点、第几局、马里奥在干嘛），不打断游戏、不用打字。
/// 游戏里出的红字错误也自动记下（同一条只记一次）。全部写在项目根目录 PlaytestLogs/Feedback/（不进 git）。
/// 测试中心 → "打包反馈" 把截图、说明、错误、体检报告、测试报告打成一个 zip，直接发给 AI。
/// S242（黑匣子）：
///   · 截图改成缩小的 JPG（最宽 feedbackShotWidth，质量 72：一张 ≈ 100KB，原来全尺寸 PNG 1–4MB）；在这一帧画完之后、提示框画出来之前截；
///   · 每条记录另存 blackbox_NNN.md = 最近 25 秒的位置 / 速度 / 状态 / 按键表 + 出事前的面包屑 + 房间快照（M / T 标在房间字符画上）；
///   · 卡住、按住方向键不动、画面顿卡、位置坏了、时间停住、狂按 → 自动记一条（同一类 30 秒最多一次，一次试玩最多 25 条）；
///   · F8 之后 3 秒内按 1–4 给这条打标签（1 卡住 2 不好玩 3 看不懂 4 bug），不用打字；
///   · events.tsv 一行一条（类型 / 时间 / 文件 / 一句话），打包时自动出总结。
/// 纯记录，不碰玩法（H4）。每个场景自动常驻一个（RuntimeInitializeOnLoadMethod），不用往场景里拖。
/// </summary>
public sealed class Step1Feedback : MonoBehaviour
{
    public const string Folder = "PlaytestLogs/Feedback";
    public const string LogName = "feedback.md";
    public const string EventsName = "events.tsv";
    public const int MaxErrors = 40;
    public const float TagWindow = 3f;

    /// <summary>当前场景补充一句"此刻的情况"（小镇：几点/下一扇门/你在哪；房间自己算）。</summary>
    public static System.Func<string> Context;

    private static Step1Feedback inst;
    private static int errors;
    private static readonly System.Collections.Generic.HashSet<string> seenErrors = new System.Collections.Generic.HashSet<string>();
    private string toast = ""; private float toastUntil;
    private int tagFor; private float tagUntil;
    private Step1BlackBoxRecorder box;
    private MarioMindTuningSO tuning;
    private bool capturing;

    /// <summary>S242：F8 刚按下、还在等你按 1–4 打标签（这 3 秒里伪装装备栏不吃数字键）。</summary>
    public static bool TagOpen => inst != null && Time.unscaledTime < inst.tagUntil;

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

    private void Awake()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        box = gameObject.AddComponent<Step1BlackBoxRecorder>();
        box.Init(tuning);
    }

    private void OnDestroy() { if (inst == this) { Application.logMessageReceived -= OnLog; inst = null; } }

    public static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Folder);

    /// <summary>已记下几条（数截图；S242 以前是 png，现在是 jpg，两种都算）。</summary>
    public static int Count() => Directory.Exists(Root) ? Directory.GetFiles(Root, "feedback_*.png").Length + Directory.GetFiles(Root, "feedback_*.jpg").Length : 0;

    private static int NextNumber()
    {
        int n = 0;
        if (Directory.Exists(Root))
            foreach (var f in Directory.GetFiles(Root, "blackbox_*.md").Concat(Directory.GetFiles(Root, "feedback_*.*")))
            {
                var name = Path.GetFileNameWithoutExtension(f); int cut = name.LastIndexOf('_');
                if (cut > 0 && int.TryParse(name.Substring(cut + 1), out var k)) n = Mathf.Max(n, k);
            }
        return n + 1;
    }

    private void Update()
    {
        if (Step1PlaytestLog.IsTyping) return;
        if (Step1Keys.Down(KeyCode.F8)) { Capture(Step1BlackBox.Kind.Manual, null); return; }
        if (TagOpen)
        {
            int d = Step1Keys.Digit1to5();
            if (d >= 1 && d <= 4) { Tag(tagFor, d); tagUntil = 0f; }
        }
    }

    /// <summary>S240：别的系统（卡住救援）自动记一条反馈：截图 + 一句说明。没有常驻实例时什么都不做。</summary>
    public static void CaptureNote(string note) { if (inst != null) inst.Capture(Step1BlackBox.Kind.Rescue, note); }

    /// <summary>S242：黑匣子的自动检测记一条（已经限过频）。</summary>
    public static void CaptureAuto(Step1BlackBox.Kind kind, string why) { if (inst != null) inst.Capture(kind, why); }

    public static readonly string[] TagNames = { "", "卡住了", "不好玩 / 别扭", "看不懂", "bug" };

    private void Tag(int n, int d)
    {
        Append($"- 你的标签（记录 {n:000}）：{TagNames[d]}\n");
        AppendEvent(Step1BlackBox.Kind.Manual, n, "标签：" + TagNames[d]);
        Step1BlackBoxRecorder.Note("你给记录 " + n + " 打了标签：" + TagNames[d]);
        Toast(string.Format(Step1Text.FeedbackTagged, n, TagNames[d]));
    }

    private void Capture(Step1BlackBox.Kind kind, string note)
    {
        if (capturing) return;
        try
        {
            Directory.CreateDirectory(Root);
            int n = NextNumber();
            string when = System.DateTime.Now.ToString("MM-dd HH:mm:ss");
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string ctx = Describe();
            string shot = $"feedback_{n:000}.jpg";
            string md = $"blackbox_{n:000}.md";
            if (box != null) File.WriteAllText(Path.Combine(Root, md), box.Dump(new Step1BlackBox.Report { n = n, kind = kind, when = when, scene = scene, note = note, context = ctx }), Encoding.UTF8);
            StartCoroutine(Shoot(Path.Combine(Root, shot)));
            string say = note ?? (kind == Step1BlackBox.Kind.Manual ? "（F8 后 3 秒内按 1–4 打标签；或在测试中心补一句）" : "");
            Append($"\n## 记录 {n:000} · {Step1BlackBox.KindZh(kind)}  {when}\n- 截图：{shot}\n- 黑匣子：{md}（最近 {tuning.blackBoxSeconds:0} 秒的位置 / 状态 / 按键 + 出事前发生了什么）\n- 场景：{scene}\n- 情况：{ctx}\n- 说明：{say}\n");
            AppendEvent(kind, n, note ?? ctx);
            if (kind == Step1BlackBox.Kind.Manual) { Toast(Step1Text.FeedbackSaved(n)); tagFor = n; tagUntil = Time.unscaledTime + TagWindow; }
        }
        catch (System.Exception e) { Debug.LogWarning("[Step1Feedback] " + e.Message); }
    }

    /// <summary>S242：等这一帧画完再截（提示框还没画）→ 缩小 → JPG。失败就退回 Unity 自带的全尺寸 PNG。</summary>
    private IEnumerator Shoot(string jpgPath)
    {
        capturing = true;
        // capturing = true 时 OnGUI 不画提示框 → 截图里没有它
        yield return new WaitForEndOfFrame();
        Texture2D full = null, small = null;
        try
        {
            full = ScreenCapture.CaptureScreenshotAsTexture();
            var (w, h) = Step1BlackBox.Fit(full.width, full.height, tuning != null ? tuning.feedbackShotWidth : 960);
            if (w != full.width)
            {
                var rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(full, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0); small.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            }
            File.WriteAllBytes(jpgPath, (small != null ? small : full).EncodeToJPG(72));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Step1Feedback] JPG 截图失败，改存 PNG：" + e.Message);
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(jpgPath, ".png"));
        }
        finally
        {
            if (full != null) Destroy(full);
            if (small != null) Destroy(small);
            capturing = false;
        }
    }

    /// <summary>此刻的情况（一行）。只读公开状态，记录时才算（不在 Update 里找物体）。</summary>
    private static string Describe()
    {
        var sb = new StringBuilder();
        if (Context != null) { try { sb.Append(Context()); } catch { } }
        var gm = GameManager.Instance;
        if (gm != null) sb.Append($" 第{gm.CurrentRound}局 剩{Mathf.CeilToInt(Mathf.Max(0f, gm.GameTimer))}秒 状态{gm.CurrentState}");
        var d = FindObjectOfType<MarioMindDriver>();
        if (d != null && d.Mind != null) sb.Append($" 马里奥:{d.Mind.State} 在({d.transform.position.x:0.0},{d.transform.position.y:0.0})");
        var t = FindObjectOfType<TricksterController>();
        if (t != null) sb.Append($" 你在({t.transform.position.x:0.0},{t.transform.position.y:0.0}){(t.IsDisguised ? " 伪装中" : "")}");
        var lo = TricksterLoadout.Instance;
        if (lo != null) sb.Append(" 装备栏 " + lo.Describe());
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
        AppendEvent(Step1BlackBox.Kind.Error, errors, key + " @ " + first);
        Step1BlackBoxRecorder.Note("红字错误：" + key);
        if (inst != null && errors == 1) inst.Toast(Step1Text.FeedbackError);
        // 第一条错误同时存一份黑匣子（出错前 25 秒发生了什么）
        if (inst != null && errors <= 3 && inst.box != null && inst.box.AllowAuto(Step1BlackBox.Kind.Error)) inst.Capture(Step1BlackBox.Kind.Error, key);
    }

    private static void Append(string text)
    {
        try
        {
            Directory.CreateDirectory(Root);
            string p = Path.Combine(Root, LogName);
            if (!File.Exists(p)) File.WriteAllText(p, "# 试玩反馈（F8 截图 + 自动记下的问题和错误）\n");
            File.AppendAllText(p, text);
        }
        catch { }
    }

    /// <summary>S242：一行一条（类型 \t 时间 \t 文件 \t 一句话），打包时做总结。</summary>
    private static void AppendEvent(Step1BlackBox.Kind kind, int n, string line)
    {
        try
        {
            Directory.CreateDirectory(Root);
            string clean = (line ?? "").Replace("\t", " ").Replace("\n", " ");
            if (clean.Length > 200) clean = clean.Substring(0, 200);
            File.AppendAllText(Path.Combine(Root, EventsName), $"{kind}\t{System.DateTime.Now:MM-dd HH:mm:ss}\tblackbox_{n:000}.md\t{clean}\n");
        }
        catch { }
    }

    private void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 2.5f; }

    private void OnGUI()
    {
        if (capturing || Time.unscaledTime > toastUntil) return;
        GUI.depth = -900;
        var r = new Rect(Screen.width - 430, Screen.height - 120, 420, 70);
        GUI.Box(r, "");
        GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 12), toast, Step1Gui.Text(15, TextAnchor.MiddleLeft));
    }
}
