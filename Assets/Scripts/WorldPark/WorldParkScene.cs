using UnityEngine;

/// <summary>
/// S252 剧情字幕卡（Unity IMGUI 绘制）：上下黑边的电影画幅、霓虹扑克牌、标题、逐字正文、
/// 揭示时逐张翻开的反转卡（当时以为 → 其实）、错过的线索、结局章与“下一个会场”的门、随身线索本与旅程进度。
/// 只照 WorldParkSceneModel.Scene 画；推进流程由 WorldParkRuntime 根据返回值执行。
/// 返回：null 无操作；Skip 显示全部；Continue 主按钮；End 保存并结束旅程；Save 保存检查点；Retry 重来本会场（S254，仅 GAME OVER 结局）；"travel:" + 会场 id。
/// </summary>
public static class WorldParkScene
{
    public const string Skip = "__skip__", Continue = "__continue__", End = "__end__", Save = "__save__", Retry = "__retry__", TravelPrefix = "travel:";
    public static bool showNotes, showJourney;
    /// <summary>S254：本帧的结局是 GAME OVER 时由运行器设为 true，底栏多一个“重来本会场”（与网页试玩一致）。</summary>
    public static bool offerRetry;
    private static Vector2 scroll;
    private static GUIStyle huge, title, body, small, tiny, button, bigButton;
    private static int styleHeight = -1;
    private static int styleRev = -1;
    private static float scale = 1f;

    public static string Draw(WorldParkSceneModel.Scene sc, float t, string lastResult, string checkpoint)
    {
        EnsureStyles();
        float W = Screen.width, H = Screen.height;
        Color tone = sc.tone == "red" ? new Color(1f, 0.35f, 0.45f) : new Color(0.55f, 0.92f, 1f);
        bool ready = t >= WorldParkSceneModel.ReadyAt(sc);
        string result = null;

        Fill(new Rect(0, 0, W, H), new Color(0.015f, 0.015f, 0.03f, 1f));
        float bar = H * 0.075f;
        Fill(new Rect(0, 0, W, bar), Color.black);
        Fill(new Rect(0, H - bar, W, bar), Color.black);
        Fill(new Rect(0, bar, W, 1.5f), new Color(tone.r, tone.g, tone.b, 0.5f));
        Fill(new Rect(0, H - bar - 1.5f, W, 1.5f), new Color(tone.r, tone.g, tone.b, 0.5f));
        Text(new Rect(W * 0.03f, 0, W * 0.6f, bar), sc.card + "　" + sc.venueName, title, tone, TextAnchor.MiddleLeft);
        Text(new Rect(W * 0.6f, 0, W * 0.37f, bar), KindLabel(sc.kind) + "　·　Esc 菜单", small, new Color(0.7f, 0.7f, 0.75f), TextAnchor.MiddleRight);

        float y = bar + H * 0.025f;
        if (sc.kind == "ending" && !string.IsNullOrEmpty(sc.stamp))
        {
            Color sc1 = sc.stamp.Contains("OVER") ? new Color(1f, 0.3f, 0.35f) : sc.stamp.Contains("CLEAR") ? new Color(0.5f, 1f, 0.65f) : Color.white;
            Text(new Rect(3f, y + 3f, W, H * 0.11f), sc.stamp, huge, new Color(0f, 0f, 0f, 0.7f), TextAnchor.MiddleCenter);
            Text(new Rect(0, y, W, H * 0.11f), sc.stamp, huge, sc1, TextAnchor.MiddleCenter);
            y += H * 0.11f;
            if (!string.IsNullOrEmpty(sc.stampSub)) { Text(new Rect(W * 0.1f, y, W * 0.8f, H * 0.04f), sc.stampSub, small, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter); y += H * 0.045f; }
        }
        Text(new Rect(0, y, W, H * 0.06f), sc.title, title, Color.Lerp(tone, Color.white, 0.4f), TextAnchor.MiddleCenter);
        y += H * 0.06f;
        if (!string.IsNullOrEmpty(lastResult) && sc.kind != "story")
        {
            Text(new Rect(W * 0.1f, y, W * 0.8f, H * 0.035f), "上个活动的真实结果：" + lastResult, tiny, new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleCenter);
            y += H * 0.04f;
        }

        // —— 正文 + 反转卡（可滚动） ——
        float doorsH = sc.kind == "ending" && sc.doors.Count > 0 ? H * 0.22f : 0f;
        Rect M = new Rect(W * 0.14f, y + H * 0.01f, W * 0.72f, H - bar - y - H * 0.03f - doorsH);
        float cw = M.width - 22f;
        string shown = sc.text.Substring(0, WorldParkSceneModel.Visible(sc.text, t));
        float textH = body.CalcHeight(new GUIContent(sc.text), cw);
        float cardH = Mathf.Max(H * 0.1f, 0f);
        float contentH = textH + 12f + sc.twists.Count * (cardH + 10f) + (sc.missed.Count > 0 ? H * 0.05f : 0f);
        scroll = GUI.BeginScrollView(M, scroll, new Rect(0, 0, cw, Mathf.Max(contentH, M.height - 4f)));
        Text(new Rect(0, 0, cw, textH), shown, body, new Color(0.93f, 0.93f, 0.95f), TextAnchor.UpperLeft);
        float cy = textH + 12f;
        for (int i = 0; i < sc.twists.Count; i++)
        {
            float at = (float)WorldParkSceneModel.TwistAt(sc, i);
            float a = Mathf.Clamp01((t - at) / 0.3f);
            if (a > 0f) DrawTwist(new Rect((1f - a) * 24f, cy, cw, cardH), sc.twists[i], tone, a);
            cy += cardH + 10f;
        }
        if (sc.missed.Count > 0 && ready)
            Text(new Rect(0, cy, cw, H * 0.045f), "你错过了：" + string.Join("、", sc.missed.ToArray()) + "（下次进入这个会场时留意）", small, new Color(0.65f, 0.65f, 0.7f), TextAnchor.MiddleLeft);
        GUI.EndScrollView();

        // —— 结局：下一个会场的门 ——
        if (doorsH > 0f && ready)
        {
            int k = sc.doors.Count;
            float dw = Mathf.Min(W * 0.2f, (W * 0.84f - (k - 1) * 14f) / k), dh = doorsH - H * 0.02f;
            float dx = (W - (k * dw + (k - 1) * 14f)) / 2f, dy = H - bar - doorsH;
            for (int i = 0; i < k; i++)
            {
                var d = sc.doors[i];
                Rect r = new Rect(dx + i * (dw + 14f), dy, dw, dh);
                Color c = d.open ? tone : new Color(0.4f, 0.4f, 0.45f);
                Fill(r, new Color(0.03f, 0.03f, 0.06f, 1f));
                Frame(r, d.open ? 3f : 1f, new Color(c.r, c.g, c.b, d.open ? 0.95f : 0.5f));
                Text(new Rect(r.x, r.y + dh * 0.05f, r.width, dh * 0.45f), d.card, huge, new Color(c.r, c.g, c.b, d.open ? 1f : 0.5f), TextAnchor.MiddleCenter);
                Text(new Rect(r.x + 4f, r.y + dh * 0.52f, r.width - 8f, dh * 0.2f), d.name, small, d.open ? Color.white : new Color(0.6f, 0.6f, 0.65f), TextAnchor.MiddleCenter);
                Text(new Rect(r.x + 4f, r.y + dh * 0.72f, r.width - 8f, dh * 0.24f), d.open ? "前往 ▶" : d.reason, tiny, d.open ? tone : new Color(0.6f, 0.6f, 0.65f), TextAnchor.MiddleCenter);
                if (d.open && GUI.Button(r, GUIContent.none, GUIStyle.none)) result = TravelPrefix + d.venue;
            }
        }

        // —— 底栏 ——
        float by = H - bar + bar * 0.15f, bh = bar * 0.7f;
        if (GUI.Button(new Rect(W * 0.03f, by, W * 0.13f, bh), "线索本（" + sc.notes.Count + "）", button)) { showNotes = !showNotes; showJourney = false; }
        if (GUI.Button(new Rect(W * 0.17f, by, W * 0.1f, bh), "旅程", button)) { showJourney = !showJourney; showNotes = false; }
        if (sc.kind == "ending")
        {
            if (offerRetry && ready && GUI.Button(new Rect(W * 0.44f, by, W * 0.24f, bh), "重来本会场", bigButton)) result = Retry;
            if (GUI.Button(new Rect(W * 0.7f, by, W * 0.27f, bh), "保存检查点，回到标题", button)) result = End;
        }
        else
        {
            if (GUI.Button(new Rect(W * 0.29f, by, W * 0.12f, bh), "保存检查点", button)) result = Save;
            if (GUI.Button(new Rect(W * 0.72f, by, W * 0.25f, bh), ready ? sc.action + " ▶" : "显示全部", bigButton)) result = ready ? Continue : Skip;
        }
        if (!ready) Text(new Rect(W * 0.42f, by, W * 0.29f, bh), "点击任意处显示全部", tiny, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter);

        if (showNotes) DrawPanel(new Rect(W * 0.58f, bar + 8f, W * 0.4f, H - 2f * bar - 16f), "线索本 · " + sc.venueName, sc, tone, true, checkpoint);
        if (showJourney) DrawPanel(new Rect(W * 0.58f, bar + 8f, W * 0.4f, H - 2f * bar - 16f), "旅程", sc, tone, false, checkpoint);

        Event e = Event.current;
        if (result == null && !ready && e.type == EventType.MouseDown) { e.Use(); result = Skip; }
        if (result == null && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return))
        {
            e.Use();
            result = !ready ? Skip : sc.kind == "ending" ? null : Continue;
        }
        GUI.color = Color.white;
        return result;
    }

    private static void DrawTwist(Rect r, WorldParkSceneModel.Note n, Color tone, float a)
    {
        Fill(r, new Color(0.06f, 0.06f, 0.1f, 0.95f * a));
        Frame(r, 2f, new Color(tone.r, tone.g, tone.b, 0.8f * a));
        float half = r.width * 0.46f;
        Text(new Rect(r.x + 10f, r.y + 4f, half - 10f, r.height * 0.3f), "「" + n.name + "」当时以为", tiny, new Color(0.65f, 0.65f, 0.7f, a), TextAnchor.MiddleLeft);
        Text(new Rect(r.x + 10f, r.y + r.height * 0.3f, half - 10f, r.height * 0.68f), n.before, small, new Color(0.7f, 0.7f, 0.75f, a), TextAnchor.UpperLeft);
        Text(new Rect(r.x + half, r.y, r.width * 0.08f, r.height), "→", title, new Color(tone.r, tone.g, tone.b, a), TextAnchor.MiddleCenter);
        Text(new Rect(r.x + half + r.width * 0.08f, r.y + 4f, r.width - half - r.width * 0.08f - 10f, r.height * 0.3f), "其实", tiny, new Color(tone.r, tone.g, tone.b, a), TextAnchor.MiddleLeft);
        Text(new Rect(r.x + half + r.width * 0.08f, r.y + r.height * 0.3f, r.width - half - r.width * 0.08f - 10f, r.height * 0.68f), n.after, small, new Color(1f, 1f, 1f, a), TextAnchor.UpperLeft);
    }

    private static void DrawPanel(Rect r, string head, WorldParkSceneModel.Scene sc, Color tone, bool notes, string checkpoint)
    {
        Fill(r, new Color(0.04f, 0.04f, 0.07f, 0.97f));
        Frame(r, 1f, new Color(tone.r, tone.g, tone.b, 0.5f));
        GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f));
        GUI.color = tone; GUILayout.Label(head, title); GUI.color = Color.white;
        if (notes)
        {
            if (sc.notes.Count == 0) GUILayout.Label("这个会场还没有发现任何线索。", small);
            foreach (var n in sc.notes)
            {
                GUILayout.Label("「" + n.name + "」" + n.before, small);
                if (n.after != "") { GUI.color = tone; GUILayout.Label("　其实：" + n.after, small); GUI.color = Color.white; }
                GUILayout.Space(4f);
            }
            if (!string.IsNullOrEmpty(sc.rules)) { GUILayout.Space(6f); GUILayout.Label("公开规则：" + sc.rules, tiny); }
        }
        else
        {
            foreach (string line in sc.journey) GUILayout.Label(line, small);
            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(checkpoint)) GUILayout.Label(checkpoint, tiny);
            GUILayout.Label("只在会场入场 / 剧情 / 揭示 / 结局时存档；活动中途的位置与计时不存。继续旅程会从上一个安全阶段重进。", tiny);
        }
        GUILayout.EndArea();
    }

    private static string KindLabel(string kind)
    {
        switch (kind)
        {
            case "story": return "剧情";
            case "reveal": return "揭示 · 反转";
            case "ending": return "结局";
            case "room": return "进入房间前";
            default: return kind ?? "";
        }
    }

    private static void EnsureStyles()
    {
        if (huge != null && styleHeight == Screen.height && styleRev == WorldParkUi.Revision) return;
        styleHeight = Screen.height;
        styleRev = WorldParkUi.Revision;
        // S254：再乘玩家在“设置”里选的界面大小（85%–150%）；正文在滚动区里，调大也不会被截断。
        scale = Mathf.Clamp(Screen.height / 720f, 0.6f, 2.2f) * WorldParkUi.UserScale;
        huge = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(44 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
        title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        body = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * scale), wordWrap = true, alignment = TextAnchor.UpperLeft };
        small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * scale), wordWrap = true };
        tiny = new GUIStyle(small) { fontSize = Mathf.RoundToInt(12 * scale) };
        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(14 * scale), wordWrap = true };
        bigButton = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(18 * scale), fontStyle = FontStyle.Bold };
        foreach (GUIStyle st in new[] { huge, title, body, small, tiny }) st.normal.textColor = Color.white;
    }

    private static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); }
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
}
