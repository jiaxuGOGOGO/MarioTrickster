using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// S211：小镇 ↔ 房间的平滑切换。一个跨场景常驻的小物体（DontDestroyOnLoad），负责：
/// 淡出黑幕 → 标题卡（"门 2 · 两层监狱" / "回到小镇 13:30"）→ 后台异步加载（LoadSceneAsync + allowSceneActivation=false，
/// 黑屏里加载，玩家看不到卡顿）→ 加载好了才换场景 → 淡入。切换期间 Busy = true，小镇和房间都不吃按键。
/// 用不走时间的计时（unscaled），房间里暂停 / 顿帧也不会卡住黑幕。
/// S212：圆形收缩/展开（从门口收拢 → 在新场景里的你身上展开）+ 缓入缓出 + 激活卡顿那一帧不跳 + 声音跟着淡出淡入
/// + 切换后释放旧场景没用的资源（Resources.UnloadUnusedAssets，在黑屏里做，看不到卡）。
/// 没装任何插件：借鉴 mygamedevtools/scene-loader 的"先盖住 → 加载 → 揭开"流程，但它 4.1.2 起要 Unity 6，
/// 且和 GameManager 里直接 SceneManager.LoadScene 的 F5 重开混用时会记错当前场景——自己写这 100 行更稳。
/// [AI防坑警告] 不要"提前预加载下一个房间"：allowSceneActivation=false 会堵住 Unity 之后所有异步加载，且没法取消（Unity 文档）。
/// </summary>
public sealed class SceneTransit : MonoBehaviour
{
    private static SceneTransit inst;
    private readonly SceneTransitPlan plan = new SceneTransitPlan();
    private string target = "";
    private Texture2D black, hole;
    private GUIStyle titleStyle;
    private float volumeBefore = 1f;
    private bool revealSet;

    public static bool Busy => inst != null && inst.plan.Busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inst = null; }

    /// <summary>场景能加载吗（名字或完整路径都行；必须在 Build Settings 里且勾上）。</summary>
    public static bool CanLoad(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        if (scene.EndsWith(".unity")) return SceneUtility.GetBuildIndexByScenePath(scene) >= 0;
        return Application.CanStreamedLevelBeLoaded(scene);
    }

    /// <summary>开始切换。场景不存在 / 没登记到 Build Settings / 正在切换 → 返回 false，什么都不做。</summary>
    public static bool Go(string scene, string title) => Go(scene, title, null);

    /// <summary>S212：from = 圆从哪里收拢（世界坐标，比如门口）；null = 屏幕中央。</summary>
    public static bool Go(string scene, string title, Vector3? from)
    {
        if (!CanLoad(scene)) return false;
        if (inst == null)
        {
            var go = new GameObject("SceneTransit");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SceneTransit>();
        }
        if (!inst.plan.Begin(title)) return false;
        inst.plan.SetFocus(0.5f, 0.5f);
        if (from.HasValue && Camera.main != null)
        {
            var v = Camera.main.WorldToViewportPoint(from.Value);
            if (v.z > 0f) inst.plan.SetFocus(v.x, v.y);
        }
        inst.target = scene;
        inst.volumeBefore = AudioListener.volume;
        inst.revealSet = false;
        inst.StartCoroutine(inst.Run());
        return true;
    }

    /// <summary>S212：新场景告诉转场"在哪里展开"（通常是你站的位置）。只在切换中有效；不调用 = 屏幕中央展开。</summary>
    public static void RevealAt(Vector3 world, Camera cam = null)
    {
        if (inst == null || !inst.plan.Busy || inst.plan.phase == SceneTransitPlan.Phase.FadeOut) return;
        cam = cam != null ? cam : Camera.main;
        if (cam == null) return;
        var v = cam.WorldToViewportPoint(world);
        if (v.z > 0f) { inst.plan.SetFocus(v.x, v.y); inst.revealSet = true; }
    }

    private IEnumerator Run()
    {
        while (plan.phase == SceneTransitPlan.Phase.FadeOut) { plan.Tick(plan.Step(Time.unscaledDeltaTime), false); Volume(); yield return null; }

        AsyncOperation op = null;
        try { op = SceneManager.LoadSceneAsync(target); }
        catch (System.Exception e) { Debug.LogError("[SceneTransit] " + e.Message); }
        if (op == null) { plan.Abort(); AudioListener.volume = volumeBefore; yield break; }
        op.allowSceneActivation = false;

        while (!plan.Tick(plan.Step(Time.unscaledDeltaTime), op.progress >= 0.9f)) yield return null;
        Time.timeScale = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        var unload = Resources.UnloadUnusedAssets(); // 黑屏里释放旧场景的贴图/网格（新场景里看不到这一下）
        while (!unload.isDone) yield return null;
        yield return null; // 新场景的 Start 先跑完（小镇搭画面、房间开局），再揭开黑幕
        yield return null; // 再等一帧：镜头已就位，RevealAt 算出的圆心是准的
        if (!revealSet) plan.SetFocus(0.5f, 0.5f);
        plan.Activated();
        while (plan.Busy) { plan.Tick(plan.Step(Time.unscaledDeltaTime), true); Volume(); yield return null; }
        AudioListener.volume = volumeBefore;
    }

    private void Volume() => AudioListener.volume = volumeBefore * (1f - plan.Alpha);

    private void OnDisable() { if (plan.Busy) AudioListener.volume = volumeBefore; }

    private void OnGUI()
    {
        if (!plan.Busy) return;
        if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.white); black.Apply(); }
        if (hole == null) hole = MakeHole(128);
        GUI.depth = -1000;
        float a = plan.Alpha, w = Screen.width, h = Screen.height;
        GUI.color = Color.black;
        if (plan.style == SceneTransitPlan.Style.Iris && a < 0.999f)
        {
            // 圆洞：中间透明、边缘柔和的贴图 + 洞外四块黑。圆心在屏幕上（GUI 坐标 y 向下）
            float cx = plan.focusX * w, cy = (1f - plan.focusY) * h;
            float r = Mathf.Max(1f, plan.IrisOpen * SceneTransitPlan.FarCorner(plan.focusX, plan.focusY, w, h) * 1.08f);
            var box = new Rect(cx - r, cy - r, r * 2f, r * 2f);
            GUI.DrawTexture(box, hole);
            GUI.DrawTexture(new Rect(0, 0, w, Mathf.Max(0, box.yMin)), black);
            GUI.DrawTexture(new Rect(0, box.yMax, w, Mathf.Max(0, h - box.yMax)), black);
            GUI.DrawTexture(new Rect(0, box.yMin, Mathf.Max(0, box.xMin), box.height), black);
            GUI.DrawTexture(new Rect(box.xMax, box.yMin, Mathf.Max(0, w - box.xMax), box.height), black);
        }
        else
        {
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(0, 0, w, h), black);
        }
        GUI.color = Color.white;
        if (a > 0.6f && plan.title.Length > 0)
        {
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleStyle.normal.textColor = new Color(1f, 1f, 1f, (a - 0.6f) / 0.4f);
            GUI.Label(new Rect(0, h / 2f - 60, w, 120), plan.title, titleStyle);
        }
    }

    /// <summary>白色贴图：圆内透明、圆外不透明，边缘 12% 柔和过渡（画的时候用 GUI.color 染成黑）。</summary>
    private static Texture2D MakeHole(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.88f) / 0.12f));
            }
        t.SetPixels(px); t.Apply();
        return t;
    }
}
