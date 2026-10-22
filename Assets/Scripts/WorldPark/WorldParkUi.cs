using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// S254 电脑端（Windows 优先）通用界面：界面大小、音量、全屏 / 窗口、退出，以及标题页、暂停菜单、设置面板。
/// 设置存 PlayerPrefs（Windows 独立版在注册表 HKCU\Software\公司名\产品名），与世界检查点分开，删存档不丢设置。
/// HUD 缩放：1080p 下 1.2 倍（正文 16px → 19px，对应 PC 正文 ≥ 18px@1080p 的无障碍准则），再乘玩家选的界面大小。
/// 全屏演出层（亮灯 / 字幕卡 / 博弈桌）各自按屏幕高度排版，只额外乘 UserScale，并在 Revision 变化时重建样式。
/// 本类只画界面、返回玩家点了什么；推进流程、存档、换场景仍由 WorldParkRuntime 执行。
/// </summary>
public static class WorldParkUi
{
    private const string KeyScale = "worldpark.ui.scale", KeyVolume = "worldpark.ui.volume";
    public const float MinScale = 0.85f, MaxScale = 1.5f;
    public static readonly float[] Scales = { 0.85f, 1f, 1.15f, 1.3f, 1.5f };
    private static bool loaded;
    private static float user = 1f;
    private static int revision;

    // 菜单返回值（标题页与暂停菜单共用）
    public const string Resume = "resume", Save = "save", Retry = "retry", Title = "title", Quit = "quit", NewJourney = "new", ContinueJourney = "continue";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { loaded = false; revision++; styles = null; }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        user = Mathf.Clamp(PlayerPrefs.GetFloat(KeyScale, 1f), MinScale, MaxScale);
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
    }

    /// <summary>界面大小或字号相关设置的版本号；样式缓存看到它变化就重建。</summary>
    public static int Revision { get { Load(); return revision; } }
    public static float UserScale { get { Load(); return user; } }
    public static float Volume { get { Load(); return AudioListener.volume; } }
    public static float HudFactor => Mathf.Clamp(Screen.height / 900f, 0.8f, 3f) * UserScale;

    /// <summary>HUD 用：把 GUI 坐标缩放成“虚拟像素”。返回旧矩阵，调用方必须在绘制完后还原。鼠标坐标 Unity 会按矩阵自动换算。</summary>
    public static Matrix4x4 BeginHud(out float width, out float height)
    {
        float f = HudFactor;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = old * Matrix4x4.Scale(new Vector3(f, f, 1f));
        width = Screen.width / f; height = Screen.height / f;
        return old;
    }

    public static void SetScale(float value)
    {
        Load();
        float v = Mathf.Clamp(value, MinScale, MaxScale);
        if (Mathf.Approximately(v, user)) return;
        user = v; revision++;
        PlayerPrefs.SetFloat(KeyScale, user); PlayerPrefs.Save();
    }

    public static void SetVolume(float value)
    {
        Load();
        AudioListener.volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyVolume, AudioListener.volume);
    }

    public static bool Fullscreen => Screen.fullScreen;

    /// <summary>独立版：全屏窗口 ↔ 约 2/3 屏幕大小的窗口（Alt+Enter 是 Unity 自带的同一切换）。编辑器里无效果。</summary>
    public static void ToggleFullscreen()
    {
        int sw = Mathf.Max(1280, Display.main.systemWidth), sh = Mathf.Max(720, Display.main.systemHeight);
        if (Screen.fullScreen) Screen.SetResolution(Mathf.Max(1280, sw * 2 / 3), Mathf.Max(720, sh * 2 / 3), FullScreenMode.Windowed);
        else Screen.SetResolution(sw, sh, FullScreenMode.FullScreenWindow);
    }

    public static void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ───────────────────────── 共用样式（HUD 虚拟像素） ─────────────────────────
    public sealed class Styles { public GUIStyle huge, title, text, small, button, bigButton, box; }
    private static Styles styles;
    private static int stylesRev = -1;

    /// <summary>只能在 OnGUI 里取（GUI.skin 只在 OnGUI 可用）。字号是虚拟像素，实际大小 = 字号 × HudFactor。</summary>
    public static Styles S
    {
        get
        {
            if (styles != null && stylesRev == Revision) return styles;
            stylesRev = Revision;
            var s = new Styles();
            s.text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            s.text.normal.textColor = new Color(0.92f, 0.93f, 0.96f);
            s.title = new GUIStyle(s.text) { fontSize = 22, fontStyle = FontStyle.Bold };
            s.huge = new GUIStyle(s.text) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            s.small = new GUIStyle(s.text) { fontSize = 13 };
            s.small.normal.textColor = new Color(0.72f, 0.74f, 0.8f);
            s.button = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true };
            s.bigButton = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };
            s.box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18, 18, 14, 14) };
            styles = s;
            return s;
        }
    }

    public static void Fill(Rect r, Color c)
    {
        Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
    }

    /// <summary>设置面板内容（调用方负责 BeginArea / 背景）。返回 true = 玩家点了“返回”。</summary>
    public static bool DrawSettings()
    {
        Load();
        var st = S;
        GUILayout.Label("设置", st.title);
        GUILayout.Space(6);
        GUILayout.Label("界面大小：" + Mathf.RoundToInt(user * 100f) + "%（看不清字就调大）", st.text);
        GUILayout.BeginHorizontal();
        foreach (float s in Scales)
        {
            bool on = Mathf.Abs(user - s) < 0.01f;
            if (GUILayout.Toggle(on, Mathf.RoundToInt(s * 100f) + "%", st.button, GUILayout.Height(34)) && !on) SetScale(s);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.Label("音量：" + Mathf.RoundToInt(Volume * 100f) + "%", st.text);
        float nv = GUILayout.HorizontalSlider(Volume, 0f, 1f, GUILayout.Height(22));
        if (Mathf.Abs(nv - Volume) > 0.005f) SetVolume(nv);
        GUILayout.Space(12);
        if (GUILayout.Button(Fullscreen ? "切换为窗口模式" : "切换为全屏", st.button, GUILayout.Height(36))) ToggleFullscreen();
        GUILayout.Label("也可以随时按 Alt+Enter 切换全屏 / 窗口（编辑器里无效，只在 Windows 独立版生效）。", st.small);
        GUILayout.Space(6);
        GUILayout.Label("存档：会场入场、剧情、揭示、结局时自动保存检查点；现场探索和博弈的中途不存，继续旅程会从上个检查点重进。", st.small);
        GUILayout.Space(10);
        bool back = GUILayout.Button("返回", st.button, GUILayout.Height(38));
        if (back) PlayerPrefs.Save();
        return back;
    }

    /// <summary>
    /// 暂停菜单（Esc）：半透明遮罩 + 居中面板。note 写清“现在退出会丢什么”。
    /// 返回 Resume / Save / Retry / Title / Quit 或 null。settingsOpen 由调用方保存（菜单内切到设置页）。
    /// Esc 由调用方处理（探索场景在 Update 里读按键，剧情场景在 OnGUI 里读事件），这里不读，避免同一帧开了又关。
    /// </summary>
    public static string DrawPauseMenu(string heading, string note, bool canSave, bool canRetry, ref bool settingsOpen)
    {
        Matrix4x4 old = BeginHud(out float w, out float h);
        string result = null;
        try
        {
            Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.62f));
            float pw = Mathf.Min(460f, w - 40f), ph = Mathf.Min(settingsOpen ? 470f : 440f, h - 40f);
            GUILayout.BeginArea(new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph), S.box);
            if (settingsOpen) { if (DrawSettings()) settingsOpen = false; }
            else
            {
                GUILayout.Label("暂停 · " + heading, S.title);
                if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, S.small);
                GUILayout.Space(8);
                if (GUILayout.Button("继续（Esc）", S.bigButton, GUILayout.Height(42))) result = Resume;
                if (GUILayout.Button("设置（界面大小 / 音量 / 全屏）", S.button, GUILayout.Height(36))) settingsOpen = true;
                if (canSave && GUILayout.Button("保存检查点", S.button, GUILayout.Height(36))) result = Save;
                if (canRetry && GUILayout.Button("重来本会场（回到刚进入这个会场时）", S.button, GUILayout.Height(36))) result = Retry;
                if (GUILayout.Button("回到标题", S.button, GUILayout.Height(36))) result = Title;
                if (GUILayout.Button(Application.isEditor ? "退出 Play" : "退出游戏", S.button, GUILayout.Height(36))) result = Quit;
            }
            GUILayout.EndArea();
        }
        finally { GUI.matrix = old; }
        return result;
    }

    /// <summary>
    /// 标题页（Windows 独立版启动、或“回到标题”后）：世界名、设定、继续旅程 / 开始新旅程 / 设置 / 退出。
    /// confirmNew：已有检查点时“开始新旅程”要点两次（第二次才返回 NewJourney），避免误覆盖。
    /// </summary>
    public static string DrawTitle(string worldName, string premise, bool hasSave, string saveLine, string message, ref bool settingsOpen, ref bool confirmNew)
    {
        Matrix4x4 old = BeginHud(out float w, out float h);
        string result = null;
        try
        {
            Fill(new Rect(0, 0, w, h), new Color(0.015f, 0.015f, 0.035f, 1f));
            float t = Time.unscaledTime;
            // 四种花色的霓虹（只是氛围）
            string[] suits = { "♠", "♥", "♦", "♣" };
            for (int i = 0; i < 4; i++)
            {
                float glow = 0.55f + 0.45f * Mathf.Sin(t * 1.7f + i * 1.3f);
                Color c = i == 1 || i == 2 ? new Color(1f, 0.32f, 0.45f, glow) : new Color(0.5f, 0.9f, 1f, glow);
                Color oc = GUI.color; GUI.color = c;
                GUI.Label(new Rect(w / 2f - 150f + i * 75f, h * 0.12f, 75f, 70f), suits[i], S.huge);
                GUI.color = oc;
            }
            GUI.Label(new Rect(0, h * 0.12f + 74f, w, 64f), string.IsNullOrEmpty(worldName) ? "无名的世界" : worldName, S.huge);
            float pw = Mathf.Min(520f, w - 40f);
            GUILayout.BeginArea(new Rect((w - pw) / 2f, h * 0.12f + 150f, pw, h - (h * 0.12f + 160f)));
            if (settingsOpen) { if (DrawSettings()) settingsOpen = false; }
            else
            {
                if (!string.IsNullOrEmpty(premise)) { GUILayout.Label(premise, S.text); GUILayout.Space(10); }
                if (hasSave)
                {
                    if (GUILayout.Button("继续旅程", S.bigButton, GUILayout.Height(46))) result = ContinueJourney;
                    if (!string.IsNullOrEmpty(saveLine)) GUILayout.Label(saveLine, S.small);
                    GUILayout.Space(4);
                }
                string newLabel = hasSave ? (confirmNew ? "确定开始新旅程？再点一次（检查点会换成新旅程的进度，旧的一份保留为 .previous）" : "开始新旅程") : "开始旅程";
                if (GUILayout.Button(newLabel, hasSave ? S.button : S.bigButton, GUILayout.Height(hasSave ? 40 : 46)))
                {
                    if (!hasSave || confirmNew) { result = NewJourney; confirmNew = false; }
                    else confirmNew = true;
                }
                if (GUILayout.Button("设置", S.button, GUILayout.Height(36))) { settingsOpen = true; confirmNew = false; }
                if (GUILayout.Button(Application.isEditor ? "退出 Play" : "退出游戏", S.button, GUILayout.Height(36))) result = Quit;
                if (!string.IsNullOrEmpty(message)) { GUILayout.Space(8); GUILayout.Label(message, S.small); }
                GUILayout.Space(8);
                GUILayout.Label("操作：WASD / 方向键移动 · E 调查 · N 线索笔记 · 空格 / 回车 继续 · Esc 暂停菜单 · Alt+Enter 全屏", S.small);
            }
            GUILayout.EndArea();
        }
        finally { GUI.matrix = old; }
        return result;
    }
}
