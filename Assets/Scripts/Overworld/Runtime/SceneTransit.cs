using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// S211：小镇 ↔ 房间的平滑切换。一个跨场景常驻的小物体（DontDestroyOnLoad），负责：
/// 淡出黑幕 → 标题卡（"门 2 · 两层监狱" / "回到小镇 13:30"）→ 后台异步加载（LoadSceneAsync + allowSceneActivation=false，
/// 黑屏里加载，玩家看不到卡顿）→ 加载好了才换场景 → 淡入。切换期间 Busy = true，小镇和房间都不吃按键。
/// 用不走时间的计时（unscaled），房间里暂停 / 顿帧也不会卡住黑幕。
/// [AI防坑警告] 不要"提前预加载下一个房间"：allowSceneActivation=false 会堵住 Unity 之后所有异步加载，且没法取消（Unity 文档）。
/// </summary>
public sealed class SceneTransit : MonoBehaviour
{
    private static SceneTransit inst;
    private readonly SceneTransitPlan plan = new SceneTransitPlan();
    private string target = "";
    private Texture2D black;

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
    public static bool Go(string scene, string title)
    {
        if (!CanLoad(scene)) return false;
        if (inst == null)
        {
            var go = new GameObject("SceneTransit");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SceneTransit>();
        }
        if (!inst.plan.Begin(title)) return false;
        inst.target = scene;
        inst.StartCoroutine(inst.Run());
        return true;
    }

    private IEnumerator Run()
    {
        while (plan.phase == SceneTransitPlan.Phase.FadeOut) { plan.Tick(Time.unscaledDeltaTime, false); yield return null; }

        AsyncOperation op = null;
        try { op = SceneManager.LoadSceneAsync(target); }
        catch (System.Exception e) { Debug.LogError("[SceneTransit] " + e.Message); }
        if (op == null) { plan.Abort(); yield break; }
        op.allowSceneActivation = false;

        while (!plan.Tick(Time.unscaledDeltaTime, op.progress >= 0.9f)) yield return null;
        Time.timeScale = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null; // 新场景的 Start 先跑完（小镇搭画面、房间开局），再揭开黑幕
        plan.Activated();
        while (plan.Busy) { plan.Tick(Time.unscaledDeltaTime, true); yield return null; }
    }

    private void OnGUI()
    {
        if (!plan.Busy) return;
        if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.white); black.Apply(); }
        GUI.depth = -1000;
        float a = plan.Alpha;
        GUI.color = new Color(0f, 0f, 0f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
        GUI.color = Color.white;
        if (a > 0.6f && plan.title.Length > 0)
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            st.normal.textColor = new Color(1f, 1f, 1f, (a - 0.6f) / 0.4f);
            GUI.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), plan.title, st);
        }
    }
}
