using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S252 世界演出层（跨场景常驻的 IMGUI 叠加层）。只负责“看起来像弥留之国”：
/// 进入会场时霓虹扑克牌亮灯、活动失败时激光落下、结局盖 GAME CLEAR / GAME OVER 章、探索倒计时大字、被鬼看见时边缘闪红。
/// 何时触发由 WorldParkStageCues（纯逻辑）决定；本类不改变任何规则结果。使用 unscaledTime，剧情面板暂停时间也照常播放。
/// 模态演出（入场、激光、结局章）期间点击或按空格可跳过，并吞掉这次点击，避免误按下面的按钮。
/// </summary>
public sealed class WorldParkStage : MonoBehaviour
{
    private static WorldParkStage instance;
    private readonly Queue<WorldParkStageCues.Cue> queue = new Queue<WorldParkStageCues.Cue>();
    private WorldParkStageCues.Cue current;
    private float startedAt;
    private float countdown = -1f;
    private int countdownFrame = -10;
    private float flashUntil, flashLength = 0.5f;
    private string bannerText = "";
    private float bannerUntil;

    private GUIStyle huge, big, mid, small;
    private int styleHeight = -1;
    private int styleRev = -1;

    public static bool Busy { get { return instance != null && instance.current != null && Modal(instance.current.kind); } }

    public static void Show(WorldParkStageCues.Cue cue)
    {
        if (cue == null || !Application.isPlaying) return;
        Ensure();
        instance.queue.Enqueue(cue);
    }

    /// <summary>每帧由探索运行器调用：remaining&lt;0 表示不显示倒计时。</summary>
    public static void SetCountdown(float remaining)
    {
        if (!Application.isPlaying) return;
        Ensure();
        instance.countdown = remaining;
        instance.countdownFrame = Time.frameCount;
    }

    /// <summary>边缘闪红（被看见、被抓）。</summary>
    public static void Flash(float seconds = 0.5f)
    {
        if (!Application.isPlaying) return;
        Ensure();
        instance.flashLength = Mathf.Max(0.1f, seconds);
        instance.flashUntil = Time.unscaledTime + instance.flashLength;
    }

    /// <summary>顶部短横幅（追逐提示等），不挡操作。</summary>
    public static void Banner(string text, float seconds = 1.6f)
    {
        if (string.IsNullOrEmpty(text) || !Application.isPlaying) return;
        Ensure();
        instance.bannerText = text;
        instance.bannerUntil = Time.unscaledTime + seconds;
    }

    public static void ClearAll()
    {
        if (instance == null) return;
        instance.queue.Clear();
        instance.current = null;
        instance.countdown = -1f;
        instance.bannerUntil = 0f;
    }

    private static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("世界演出层");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<WorldParkStage>();
    }

    private static bool Modal(string kind) { return kind == "entrance" || kind == "laser" || kind == "clear" || kind == "gameover" || kind == "end"; }

    private void Update()
    {
        if (current != null && Time.unscaledTime - startedAt >= (float)current.seconds) current = null;
        if (current == null && queue.Count > 0)
        {
            current = queue.Dequeue();
            startedAt = Time.unscaledTime;
            WorldParkSfx.Play(current.sound);
            if (current.kind == "laser" || current.kind == "gameover") Flash(0.8f);
        }
        if (Time.frameCount - countdownFrame > 2) countdown = -1f;
    }

    private void EnsureStyles()
    {
        if (huge != null && styleHeight == Screen.height && styleRev == WorldParkUi.Revision) return;
        styleHeight = Screen.height;
        styleRev = WorldParkUi.Revision;
        // S254：再乘玩家在“设置”里选的界面大小（85%–150%）。
        float s = Mathf.Clamp(Screen.height / 720f, 0.6f, 2.2f) * WorldParkUi.UserScale;
        huge = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(110 * s), wordWrap = false };
        big = new GUIStyle(huge) { fontSize = Mathf.RoundToInt(54 * s) };
        mid = new GUIStyle(huge) { fontSize = Mathf.RoundToInt(28 * s), fontStyle = FontStyle.Normal, wordWrap = true };
        small = new GUIStyle(mid) { fontSize = Mathf.RoundToInt(18 * s) };
    }

    private void OnGUI()
    {
        GUI.depth = -300;
        EnsureStyles();
        float now = Time.unscaledTime;
        DrawFlash(now);
        DrawCountdown(now);
        DrawBanner(now);
        if (current == null) return;
        float t = now - startedAt, len = (float)current.seconds;
        Event e = Event.current;
        if (Modal(current.kind) && t > 0.25f && (e.type == EventType.MouseDown || (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return))))
        {
            current = null;
            e.Use();
            return;
        }
        if (Modal(current.kind) && (e.type == EventType.MouseDown || e.type == EventType.MouseUp)) e.Use();
        float fadeOut = Mathf.Clamp01((len - t) / 0.4f);
        switch (current.kind)
        {
            case "entrance": DrawEntrance(t, fadeOut); break;
            case "laser": DrawLaser(t, fadeOut, true); break;
            case "eliminated": DrawLaser(t, fadeOut, false); break;
            case "pass": DrawStamp(t, fadeOut, new Color(0.45f, 1f, 0.6f), 0f, false); break;
            case "clear": DrawStamp(t, fadeOut, new Color(0.45f, 1f, 0.6f), 0.65f, true); break;
            case "gameover": DrawStamp(t, fadeOut, new Color(1f, 0.25f, 0.3f), 0.8f, true); break;
            default: DrawStamp(t, fadeOut, new Color(0.95f, 0.95f, 1f), 0.7f, true); break;
        }
        GUI.color = Color.white;
    }

    private Color Neon { get { return current != null && current.tone == "red" ? new Color(1f, 0.32f, 0.45f) : new Color(0.55f, 0.92f, 1f); } }

    private static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    private static void Glow(Rect r, string text, GUIStyle style, Color c, float alpha)
    {
        if (alpha <= 0.001f) return;
        GUI.color = new Color(c.r, c.g, c.b, alpha * 0.28f);
        for (int i = 0; i < 4; i++)
        {
            float ox = (i % 2 == 0 ? 1 : -1) * 3f, oy = (i < 2 ? 1 : -1) * 3f;
            GUI.Label(new Rect(r.x + ox, r.y + oy, r.width, r.height), text, style);
        }
        GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.35f), Mathf.Lerp(c.g, 1f, 0.35f), Mathf.Lerp(c.b, 1f, 0.35f), alpha);
        GUI.Label(r, text, style);
    }

    private static void Frame(Rect r, float thick, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, thick), c);
        Fill(new Rect(r.x, r.yMax - thick, r.width, thick), c);
        Fill(new Rect(r.x, r.y, thick, r.height), c);
        Fill(new Rect(r.xMax - thick, r.y, thick, r.height), c);
    }

    /// <summary>亮灯入场：黑幕 → 霓虹扑克牌闪烁通电 → 会场名 → 规则行 → 淡出。</summary>
    private void DrawEntrance(float t, float fadeOut)
    {
        float W = Screen.width, H = Screen.height;
        Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.9f * Mathf.Clamp01(t / 0.25f) * fadeOut));
        bool on = t >= 0.15f && (t > 1.0f || Mathf.PerlinNoise(t * 16f, 0.37f) > 0.42f);
        float power = (on ? 1f : 0.12f) * fadeOut * (t > 1.0f ? 0.9f + 0.1f * Mathf.Sin(t * 7f) : 1f);
        float cw = Mathf.Min(W * 0.26f, H * 0.3f), ch = cw * 1.4f;
        Rect card = new Rect((W - cw) / 2f, H * 0.1f, cw, ch);
        Color c = Neon;
        Frame(new Rect(card.x - 6, card.y - 6, card.width + 12, card.height + 12), 10f, new Color(c.r, c.g, c.b, 0.18f * power));
        Frame(card, 4f, new Color(c.r, c.g, c.b, power));
        Glow(card, string.IsNullOrEmpty(current.card) ? "?" : current.card, huge, c, power);
        float titleA = Mathf.Clamp01((t - 1.0f) / 0.35f) * fadeOut;
        Glow(new Rect(0, card.yMax + H * 0.03f, W, H * 0.1f), current.title, big, Color.white, titleA);
        float subA = Mathf.Clamp01((t - 1.6f) / 0.35f) * fadeOut;
        GUI.color = new Color(0.85f, 0.85f, 0.9f, subA);
        GUI.Label(new Rect(W * 0.1f, card.yMax + H * 0.14f, W * 0.8f, H * 0.12f), current.sub, mid);
        GUI.color = new Color(1, 1, 1, 0.45f * subA);
        GUI.Label(new Rect(0, H - H * 0.08f, W, H * 0.05f), "点击或空格跳过", small);
    }

    /// <summary>激光：一束红光从天而降 → 白闪 → 红色余晖与“出局”。</summary>
    private void DrawLaser(float t, float fadeOut, bool modal)
    {
        float W = Screen.width, H = Screen.height;
        float x = W / 2f;
        float drop = Mathf.Clamp01(t / 0.3f);
        float beamA = t < 0.6f ? 1f : Mathf.Clamp01((1.0f - t) / 0.4f);
        Fill(new Rect(x - 14f, 0, 28f, H * 0.62f * drop), new Color(1f, 0.1f, 0.15f, 0.25f * beamA));
        Fill(new Rect(x - 3f, 0, 6f, H * 0.62f * drop), new Color(1f, 0.45f, 0.45f, 0.95f * beamA));
        if (t >= 0.3f)
        {
            float flash = Mathf.Clamp01(1f - (t - 0.3f) / 0.18f) * (modal ? 0.85f : 0.3f);
            Fill(new Rect(0, 0, W, H), new Color(1f, 1f, 1f, flash));
            Fill(new Rect(0, 0, W, H), new Color(0.6f, 0f, 0.05f, (modal ? 0.35f : 0.12f) * fadeOut));
            float a = Mathf.Clamp01((t - 0.35f) / 0.2f) * fadeOut;
            Glow(new Rect(0, H * (modal ? 0.62f : 0.2f), W, H * 0.14f), current.title, modal ? big : mid, new Color(1f, 0.25f, 0.3f), a);
            if (!string.IsNullOrEmpty(current.sub))
            {
                GUI.color = new Color(1f, 0.85f, 0.85f, a);
                GUI.Label(new Rect(W * 0.1f, H * (modal ? 0.76f : 0.3f), W * 0.8f, H * 0.12f), current.sub, modal ? mid : small);
            }
        }
    }

    /// <summary>结局章 / 通过提示：文字从放大缩回原位，像盖章。</summary>
    private void DrawStamp(float t, float fadeOut, Color c, float backdrop, bool modal)
    {
        float W = Screen.width, H = Screen.height;
        if (backdrop > 0f) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, backdrop * Mathf.Clamp01(t / 0.2f) * fadeOut));
        float k = Mathf.Clamp01(t / 0.18f);
        float scale = Mathf.Lerp(1.6f, 1f, k);
        float y = modal ? H * 0.32f : H * 0.12f;
        Matrix4x4 old = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(W / 2f, y + H * 0.07f));
        Glow(new Rect(0, y, W, H * 0.14f), current.title, modal ? big : mid, c, k * fadeOut);
        GUI.matrix = old;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 0.3f) / 0.3f) * fadeOut);
        GUI.Label(new Rect(W * 0.1f, y + H * 0.15f, W * 0.8f, H * 0.1f), current.sub, modal ? mid : small);
        if (modal && !string.IsNullOrEmpty(current.card))
        {
            GUI.color = new Color(c.r, c.g, c.b, 0.6f * fadeOut);
            GUI.Label(new Rect(0, y - H * 0.12f, W, H * 0.1f), current.card, mid);
        }
    }

    private void DrawCountdown(float now)
    {
        if (countdown < 0f || countdown > WorldParkStageCues.CountdownFrom) return;
        float W = Screen.width, H = Screen.height;
        int sec = Mathf.CeilToInt(countdown - 1e-4f);
        bool urgent = WorldParkStageCues.Urgent(countdown);
        float frac = countdown - Mathf.Floor(countdown);
        float pulse = urgent ? 1f + 0.25f * frac : 1f;
        if (urgent)
        {
            float a = 0.22f * frac;
            float th = Mathf.Min(W, H) * 0.07f;
            Fill(new Rect(0, 0, W, th), new Color(1f, 0f, 0.1f, a));
            Fill(new Rect(0, H - th, W, th), new Color(1f, 0f, 0.1f, a));
            Fill(new Rect(0, 0, th, H), new Color(1f, 0f, 0.1f, a));
            Fill(new Rect(W - th, 0, th, H), new Color(1f, 0f, 0.1f, a));
        }
        Matrix4x4 old = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(pulse, pulse), new Vector2(W / 2f, H * 0.11f));
        Glow(new Rect(0, H * 0.03f, W, H * 0.16f), sec.ToString(), big, urgent ? new Color(1f, 0.25f, 0.3f) : new Color(1f, 0.92f, 0.55f), 1f);
        GUI.matrix = old;
    }

    private void DrawFlash(float now)
    {
        if (now >= flashUntil) return;
        float W = Screen.width, H = Screen.height;
        float a = 0.45f * Mathf.Clamp01((flashUntil - now) / flashLength);
        float th = Mathf.Min(W, H) * 0.12f;
        Fill(new Rect(0, 0, W, th), new Color(1f, 0f, 0.1f, a));
        Fill(new Rect(0, H - th, W, th), new Color(1f, 0f, 0.1f, a));
        Fill(new Rect(0, 0, th, H), new Color(1f, 0f, 0.1f, a));
        Fill(new Rect(W - th, 0, th, H), new Color(1f, 0f, 0.1f, a));
    }

    private void DrawBanner(float now)
    {
        if (now >= bannerUntil || string.IsNullOrEmpty(bannerText)) return;
        float W = Screen.width, H = Screen.height;
        float a = Mathf.Clamp01((bannerUntil - now) / 0.3f);
        Rect r = new Rect(W * 0.2f, H * 0.2f, W * 0.6f, H * 0.07f);
        Fill(r, new Color(0f, 0f, 0f, 0.6f * a));
        GUI.color = new Color(1f, 0.85f, 0.4f, a);
        GUI.Label(r, bannerText, small);
    }
}
