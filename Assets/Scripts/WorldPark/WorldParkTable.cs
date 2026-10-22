using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S252 博弈桌（Unity IMGUI 绘制）。只照 WorldParkTableModel.View 画：圆桌、座位头像、命/分、别人对你说的话、
/// 逐个翻牌、结算大字、新规则宣布、底部出手区，以及可展开的“规则与记录”侧栏（完整文字记录仍然随时可查）。
/// 规则结果全部来自 BorderlandGame；本类不判任何胜负。
/// 返回本帧的操作：null 没有；数字/花色/分偷 = 提交选择；Skip = 跳过亮牌；Confirm = 确认最终结果。
/// </summary>
public static class WorldParkTable
{
    public const string Confirm = "__confirm__", Skip = "__skip__";
    public static bool showLog;
    private static Vector2 logScroll;
    private static int logCount = -1;
    private static Texture2D disc, oval;
    private static GUIStyle big, mid, small, tiny, left, cardFace, button, bigButton, field;
    private static int styleHeight = -1;
    private static int styleRev = -1;
    private static float scale = 1f;

    private static readonly Color[] Palette =
    {
        new Color(0.95f, 0.58f, 0.38f), new Color(0.48f, 0.76f, 1f), new Color(0.72f, 0.58f, 1f), new Color(0.45f, 0.9f, 0.62f),
        new Color(1f, 0.82f, 0.38f), new Color(1f, 0.52f, 0.72f), new Color(0.58f, 0.92f, 0.92f), new Color(0.86f, 0.86f, 0.86f)
    };

    public static string Draw(WorldParkTableModel.View v, string card, string venueName, bool red, string rules, IList<string> transcript, ref string number)
    {
        EnsureAssets();
        float W = Screen.width, H = Screen.height;
        Color tone = red ? new Color(1f, 0.35f, 0.45f) : new Color(0.55f, 0.92f, 1f);
        Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.05f, 1f));
        string result = null;

        // —— 顶栏 ——
        Text(new Rect(W * 0.03f, H * 0.015f, W * 0.4f, H * 0.06f), card + "　" + venueName, mid, tone, TextAnchor.MiddleLeft);
        Text(new Rect(W * 0.3f, H * 0.015f, W * 0.4f, H * 0.06f), v.roundLine, small, Color.white, TextAnchor.MiddleCenter);
        if (GUI.Button(new Rect(W * 0.8f, H * 0.02f, W * 0.17f, H * 0.05f), showLog ? "收起规则与记录" : "规则与记录", button)) showLog = !showLog;
        Text(new Rect(W * 0.8f, H * 0.07f, W * 0.17f, H * 0.025f), "Esc 菜单", tiny, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleCenter);

        // —— 桌面 ——
        Rect T = new Rect(W * 0.04f, H * 0.09f, W * (showLog ? 0.6f : 0.92f), H * 0.62f);
        Rect O = new Rect(T.x + T.width * 0.1f, T.y + T.height * 0.2f, T.width * 0.8f, T.height * 0.6f);
        Tex(oval, Grow(O, 0.035f), new Color(tone.r, tone.g, tone.b, 0.18f));
        Tex(oval, Grow(O, 0.012f), new Color(tone.r * 0.8f, tone.g * 0.8f, tone.b * 0.8f, 0.95f));
        Tex(oval, O, new Color(0.04f, 0.15f, 0.13f, 1f));
        Vector2 center = new Vector2(O.center.x, O.center.y);
        float d = Mathf.Max(34f, Mathf.Min(T.width, T.height) * 0.11f);

        foreach (var s in v.seats) DrawCard(s, T, d, center, v, tone);
        foreach (var s in v.seats) DrawSeat(s, T, d, v, tone);
        foreach (var s in v.seats) DrawBubble(s, T, d, v);
        DrawCenter(v, O, tone);

        // —— 底部出手区 ——
        Rect B = new Rect(W * 0.04f, H * 0.73f, W * 0.92f, H * 0.25f);
        Fill(B, new Color(0f, 0f, 0f, 0.6f));
        Frame(B, 1f, new Color(tone.r, tone.g, tone.b, 0.35f));
        float y = B.y + 6f;
        if (v.info.Count > 0)
        {
            Text(new Rect(B.x + 12f, y, B.width - 24f, H * 0.04f), string.Join("　·　", ToArray(v.info)), tiny, new Color(0.8f, 0.85f, 0.9f), TextAnchor.MiddleLeft);
            y += H * 0.04f;
        }
        if (v.inputOpen)
        {
            Text(new Rect(B.x + 12f, y, B.width - 24f, H * 0.04f), v.prompt, small, Color.white, TextAnchor.MiddleLeft);
            y += H * 0.045f;
            Rect row = new Rect(B.x + 12f, y, B.width - 24f, Mathf.Min(H * 0.1f, B.yMax - y - H * 0.04f));
            result = DrawControls(v, row, ref number);
            Text(new Rect(B.x + 12f, B.yMax - H * 0.035f, B.width - 24f, H * 0.03f), "提交后不能撤回；中途退出不存档，下次从会场入场检查点重新开始。", tiny, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleLeft);
        }
        else if (v.finished)
        {
            Text(new Rect(B.x, y, B.width, H * 0.07f), v.finishLine, mid, v.won ? new Color(0.5f, 1f, 0.65f) : new Color(1f, 0.35f, 0.4f), TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(B.x + B.width * 0.3f, y + H * 0.08f, B.width * 0.4f, H * 0.07f), "确认博弈结果，继续世界流程", button)) result = Confirm;
            Text(new Rect(B.x, B.yMax - H * 0.035f, B.width, H * 0.03f), "真实结果由规则产生，剧情不会重判。", tiny, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleCenter);
        }
        else if (v.revealing)
        {
            Text(new Rect(B.x, y + H * 0.04f, B.width, H * 0.06f), v.phase == "reveal" ? "亮牌中……" : "结算", mid, tone, TextAnchor.MiddleCenter);
            Text(new Rect(B.x, B.yMax - H * 0.04f, B.width, H * 0.03f), "点击或空格跳过", tiny, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
        }

        if (showLog) DrawLog(new Rect(W * 0.66f, H * 0.09f, W * 0.31f, H * 0.62f), rules, transcript);

        Event e = Event.current;
        if (v.revealing && result == null && (e.type == EventType.MouseDown || (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return))))
        {
            e.Use();
            result = Skip;
        }
        GUI.color = Color.white; GUI.contentColor = Color.white; GUI.backgroundColor = Color.white;
        return result;
    }

    // ———————————————————————— 各部分 ————————————————————————

    private static void DrawSeat(WorldParkTableModel.Seat s, Rect T, float d, WorldParkTableModel.View v, Color tone)
    {
        float x = T.x + (float)s.x * T.width, y = T.y + (float)s.y * T.height;
        Color col = s.me ? new Color(1f, 0.95f, 0.82f) : Palette[s.index % Palette.Length];
        float dim = !s.alive ? 0.3f : (v.mode == "split" && !s.me && !s.spotlight ? 0.5f : 1f);
        if (s.spotlight && s.alive) Tex(disc, new Rect(x - d, y - d, d * 2f, d * 2f), new Color(tone.r, tone.g, tone.b, 0.25f));
        if (s.winner) Tex(disc, new Rect(x - d * 0.75f, y - d * 0.75f, d * 1.5f, d * 1.5f), new Color(1f, 0.85f, 0.3f, 0.55f));
        Tex(disc, new Rect(x - d / 2f - 3f, y - d / 2f - 3f, d + 6f, d + 6f), new Color(0f, 0f, 0f, 0.7f));
        Tex(disc, new Rect(x - d / 2f, y - d / 2f, d, d), new Color(col.r * dim, col.g * dim, col.b * dim, 1f));
        Text(new Rect(x - d / 2f, y - d / 2f, d, d), s.initial, mid, new Color(0.06f, 0.06f, 0.09f, 1f), TextAnchor.MiddleCenter);
        Text(new Rect(x - d * 1.3f, y + d / 2f + 1f, d * 2.6f, d * 0.4f), s.name, small, s.alive ? Color.white : new Color(0.55f, 0.55f, 0.6f), TextAnchor.MiddleCenter);
        string hearts = !s.alive ? "出局" : s.maxLives <= 6 ? new string('●', Mathf.Clamp(s.lives, 0, s.maxLives)) + new string('○', Mathf.Max(0, s.maxLives - s.lives)) : s.lives + " 命";
        if (v.mode == "split" && s.alive) hearts += "　" + s.points + " 分";
        Text(new Rect(x - d * 1.3f, y + d / 2f + d * 0.4f, d * 2.6f, d * 0.36f), hearts, tiny, s.alive ? new Color(1f, 0.42f, 0.48f) : new Color(1f, 0.3f, 0.3f, 0.8f), TextAnchor.MiddleCenter);
        if (v.resultShown && s.lost > 0) Text(new Rect(x - d, y - d * 1.05f, d * 2f, d * 0.5f), "−" + s.lost + " 命", mid, new Color(1f, 0.3f, 0.35f), TextAnchor.MiddleCenter);
        else if (v.resultShown && s.gain > 0) Text(new Rect(x - d, y - d * 1.05f, d * 2f, d * 0.5f), "+" + s.gain + " 分", mid, new Color(0.5f, 1f, 0.65f), TextAnchor.MiddleCenter);
        if (!s.alive || s.eliminatedNow) Text(new Rect(x - d, y - d * 0.3f, d * 2f, d * 0.6f), "出局", mid, new Color(1f, 0.2f, 0.25f, 0.95f), TextAnchor.MiddleCenter);
    }

    private static void DrawCard(WorldParkTableModel.Seat s, Rect T, float d, Vector2 center, WorldParkTableModel.View v, Color tone)
    {
        if (!s.active) return;
        float x = T.x + (float)s.x * T.width, y = T.y + (float)s.y * T.height;
        float cx = Mathf.Lerp(x, center.x, s.me ? 0.45f : 0.42f), cy = Mathf.Lerp(y, center.y, s.me ? 0.45f : 0.42f);
        float cw = d * 0.78f, ch = d * 1.05f;
        Rect r = new Rect(cx - cw / 2f, cy - ch / 2f, cw, ch);
        bool faceUp = v.phase != "choose" && (s.me || s.revealed);
        Fill(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.5f));
        if (!faceUp)
        {
            Fill(r, s.me ? new Color(0.12f, 0.12f, 0.16f, 1f) : new Color(0.1f, 0.14f, 0.32f, 1f));
            Frame(r, 2f, new Color(tone.r, tone.g, tone.b, s.me ? 0.4f : 0.8f));
            Text(r, s.me ? "你" : "?", mid, new Color(tone.r, tone.g, tone.b, 0.8f), TextAnchor.MiddleCenter);
            return;
        }
        Fill(r, new Color(0.96f, 0.96f, 0.93f, 1f));
        Frame(r, 2f, s.winner ? new Color(1f, 0.8f, 0.2f) : s.wrong ? new Color(1f, 0.25f, 0.3f) : new Color(0.3f, 0.3f, 0.35f));
        bool redSuit = s.pick == "♥" || s.pick == "♦" || s.pick == "偷";
        Text(r, s.pick, cardFace, redSuit ? new Color(0.85f, 0.1f, 0.15f) : new Color(0.08f, 0.08f, 0.12f), TextAnchor.MiddleCenter);
        if (v.resultShown && s.truth != "")
            Text(new Rect(r.x - cw * 0.4f, r.yMax + 1f, cw * 1.8f, d * 0.34f), "真实 " + s.truth, tiny, s.wrong ? new Color(1f, 0.35f, 0.4f) : new Color(0.5f, 1f, 0.65f), TextAnchor.MiddleCenter);
    }

    private static void DrawBubble(WorldParkTableModel.Seat s, Rect T, float d, WorldParkTableModel.View v)
    {
        if (v.phase != "choose" || string.IsNullOrEmpty(s.bubble) || !s.alive) return;
        float x = T.x + (float)s.x * T.width, y = T.y + (float)s.y * T.height;
        float bw = Mathf.Min(T.width * 0.28f, 300f * scale);
        float bh = Mathf.Max(d * 0.6f, small.CalcHeight(new GUIContent(s.bubble), bw - 16f) + 12f);
        bool right = s.x <= 0.5;
        Rect b = new Rect(right ? x + d * 0.65f : x - d * 0.65f - bw, y - bh / 2f, bw, bh);
        if (b.x < 4f) b.x = 4f;
        if (b.xMax > Screen.width - 4f) b.x = Screen.width - 4f - bw;
        Fill(b, new Color(1f, 1f, 1f, 0.93f));
        Fill(new Rect(right ? b.x - 8f : b.xMax, y - 5f, 8f, 10f), new Color(1f, 1f, 1f, 0.93f));
        Text(new Rect(b.x + 8f, b.y + 6f, b.width - 16f, b.height - 12f), s.bubble, small, new Color(0.08f, 0.08f, 0.12f), TextAnchor.MiddleLeft);
    }

    private static void DrawCenter(WorldParkTableModel.View v, Rect O, Color tone)
    {
        Rect c = new Rect(O.x + O.width * 0.15f, O.y + O.height * 0.3f, O.width * 0.7f, O.height * 0.24f);
        if (v.resultShown)
        {
            Text(new Rect(c.x + 2f, c.y + 2f, c.width, c.height), v.centerBig, big, new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleCenter);
            Text(c, v.centerBig, big, Color.Lerp(tone, Color.white, 0.35f), TextAnchor.MiddleCenter);
            Text(new Rect(c.x, c.yMax, c.width, O.height * 0.14f), v.centerSmall, small, Color.white, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(v.newRule))
            {
                Rect nr = new Rect(O.x - O.width * 0.02f, O.y + O.height * 0.04f, O.width * 1.04f, O.height * 0.16f);
                Fill(nr, new Color(0.35f, 0f, 0.05f, 0.85f));
                Text(nr, v.newRule, small, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter);
            }
        }
        else if (v.phase == "choose")
            Text(c, v.mode == "beauty" ? "写下 0–100 的一个数字" : v.mode == "mark" ? "你看不见自己背后的标记" : "分，还是偷？", mid, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
        else
            Text(c, "亮牌", big, new Color(tone.r, tone.g, tone.b, 0.8f), TextAnchor.MiddleCenter);
    }

    private static string DrawControls(WorldParkTableModel.View v, Rect row, ref string number)
    {
        string result = null;
        if (v.mode == "beauty")
        {
            int val;
            if (!int.TryParse(number ?? "", out val)) val = 0;
            val = Mathf.Clamp(val, 0, 100);
            Text(new Rect(row.x, row.y, row.width * 0.12f, row.height), val.ToString(), big, Color.white, TextAnchor.MiddleCenter);
            float slid = GUI.HorizontalSlider(new Rect(row.x + row.width * 0.13f, row.y + row.height * 0.42f, row.width * 0.36f, 18f), val, 0f, 100f);
            if (Mathf.RoundToInt(slid) != val) val = Mathf.RoundToInt(slid);
            float bx = row.x + row.width * 0.51f, bw = row.width * 0.055f;
            int[] steps = { -10, -1, 1, 10 };
            for (int i = 0; i < steps.Length; i++)
                if (GUI.Button(new Rect(bx + i * (bw + 4f), row.y + row.height * 0.2f, bw, row.height * 0.6f), (steps[i] > 0 ? "+" : "") + steps[i], button)) val = Mathf.Clamp(val + steps[i], 0, 100);
            string typed = GUI.TextField(new Rect(bx + 4 * (bw + 4f) + 6f, row.y + row.height * 0.2f, row.width * 0.07f, row.height * 0.6f), val.ToString(), 3, field);
            int parsed;
            if (typed != val.ToString() && int.TryParse(typed, out parsed)) val = Mathf.Clamp(parsed, 0, 100);
            number = val.ToString();
            if (GUI.Button(new Rect(row.x + row.width * 0.8f, row.y, row.width * 0.2f, row.height), "写下 " + val, bigButton)) result = val.ToString();
        }
        else if (v.mode == "mark")
        {
            string[] suits = { "♠", "♥", "♦", "♣" };
            float bw = (row.width - 3 * 10f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                GUI.contentColor = i == 1 || i == 2 ? new Color(1f, 0.45f, 0.5f) : Color.white;
                if (GUI.Button(new Rect(row.x + i * (bw + 10f), row.y, bw, row.height), "宣告 " + suits[i], bigButton)) result = suits[i];
            }
            GUI.contentColor = Color.white;
        }
        else
        {
            float bw = (row.width - 20f) / 2f;
            GUI.backgroundColor = new Color(0.45f, 0.7f, 1f);
            if (GUI.Button(new Rect(row.x, row.y, bw, row.height), "分", bigButton)) result = "分";
            GUI.backgroundColor = new Color(1f, 0.45f, 0.5f);
            if (GUI.Button(new Rect(row.x + bw + 20f, row.y, bw, row.height), "偷", bigButton)) result = "偷";
            GUI.backgroundColor = Color.white;
        }
        return result;
    }

    private static void DrawLog(Rect L, string rules, IList<string> transcript)
    {
        Fill(L, new Color(0.05f, 0.05f, 0.08f, 0.96f));
        Frame(L, 1f, new Color(1f, 1f, 1f, 0.2f));
        GUILayout.BeginArea(new Rect(L.x + 10f, L.y + 8f, L.width - 20f, L.height - 16f));
        GUILayout.Label("公开规则", left);
        GUILayout.Label(rules ?? "", left);
        GUILayout.Space(6f);
        GUILayout.Label("完整记录（人人都看得见的事实）", left);
        if (transcript != null && transcript.Count != logCount) { logCount = transcript.Count; logScroll.y = float.MaxValue; }
        logScroll = GUILayout.BeginScrollView(logScroll);
        if (transcript != null) foreach (string line in transcript) GUILayout.Label(line, left);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ———————————————————————— 工具 ————————————————————————

    private static void EnsureAssets()
    {
        if (disc == null) disc = Ellipse(64, 64);
        if (oval == null) oval = Ellipse(256, 160);
        if (big != null && styleHeight == Screen.height && styleRev == WorldParkUi.Revision) return;
        styleHeight = Screen.height;
        styleRev = WorldParkUi.Revision;
        // S254：再乘玩家在“设置”里选的界面大小（85%–150%）。
        scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2.2f) * WorldParkUi.UserScale;
        big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(38 * scale), wordWrap = false, clipping = TextClipping.Overflow };
        mid = new GUIStyle(big) { fontSize = Mathf.RoundToInt(21 * scale) };
        small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(15 * scale), wordWrap = true };
        tiny = new GUIStyle(small) { fontSize = Mathf.RoundToInt(12 * scale) };
        left = new GUIStyle(small) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(13 * scale) };
        cardFace = new GUIStyle(big) { fontSize = Mathf.RoundToInt(26 * scale) };
        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * scale), wordWrap = true };
        bigButton = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(26 * scale), fontStyle = FontStyle.Bold };
        field = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(18 * scale), alignment = TextAnchor.MiddleCenter };
        foreach (GUIStyle st in new[] { big, mid, small, tiny, left, cardFace }) st.normal.textColor = Color.white;
    }

    private static Texture2D Ellipse(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[w * h];
        float rx = w / 2f, ry = h / 2f, edge = 1.5f / Mathf.Min(rx, ry);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((1f - dist) / edge)));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Rect Grow(Rect r, float k) { float gx = r.width * k, gy = r.height * k; return new Rect(r.x - gx, r.y - gy, r.width + 2f * gx, r.height + 2f * gy); }
    private static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); }
    private static void Tex(Texture2D t, Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, t); }
    private static void Frame(Rect r, float th, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, th), c); Fill(new Rect(r.x, r.yMax - th, r.width, th), c);
        Fill(new Rect(r.x, r.y, th, r.height), c); Fill(new Rect(r.xMax - th, r.y, th, r.height), c);
    }
    private static void Text(Rect r, string text, GUIStyle style, Color c, TextAnchor anchor)
    {
        if (string.IsNullOrEmpty(text)) return;
        TextAnchor old = style.alignment;
        style.alignment = anchor;
        GUI.color = c;
        GUI.Label(r, text, style);
        style.alignment = old;
    }
    private static string[] ToArray(List<string> list) { return list.ToArray(); }
}
