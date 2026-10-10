using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S197：试玩时的地图图例（按 M 或 Tab 开关）。用户反馈"玩的时候想看清哪个是墙、哪些能炸，好决定用不用技能"。
/// 做两件事：
///   1. 左下角一块图例面板：颜色 → 名字 → "能炸 / 能钻 / 捷径 / 减速"；
///   2. 在场景里给**可破坏/可交互**的东西头上画小标签（裂墙"可炸"、裂缝地板"可炸/可踩塌"、箱子"可炸"、捷径门"从 ← 开"、通风管编号、毒池/黏胶）。
/// 名字、颜色都来自 ElementCatalog（唯一来源）。纯显示，不影响玩法，H10 自动检查时不显示。
/// </summary>
public class Step1MapLegend : MonoBehaviour
{
    public static bool Visible { get; private set; }
    private readonly List<(Transform t, string text, Color c)> tags = new List<(Transform, string, Color)>();
    private float rescan;

    /// <summary>S247：图例条目来自 ElementLook（唯一来源）。</summary>
    public static readonly (char ch, string use)[] Entries = ElementLook.LegendEntries();

    /// <summary>S241：图例分三组（用户："图例太密集，大脑过载"）——先看"我能按 L 的"，再看"能躲 / 能钻的"，最后"挡路 / 地形"。</summary>
    public enum Group { Prank, Hide, Terrain }
    /// <summary>S247：分组 = 三色（以前和作战图颜色各写各的：油桶、绳套红色却列在"躲"里）。</summary>
    public static Group GroupOf(char ch) => (Group)ElementLook.GroupIndex(ch);
    public static string GroupTitle(Group g) => g == Group.Prank ? "能按 L 的机关" : g == Group.Hide ? "能躲 · 能钻 · 能捡" : "挡路 · 地形";

    /// <summary>S241 纯逻辑：这个房间的图例 = 只列房间里真的有的字符，按组排好（没有的不列）。</summary>
    public static List<(char ch, string use)> ForRoom(IList<string> rows)
    {
        var have = new HashSet<char>();
        if (rows != null) foreach (var r in rows) foreach (var c in Step1Layout.StripSlots(r)) have.Add(c);
        if (rows != null) foreach (var r in rows) foreach (var c in r) if (Step1Layout.Slots.TryGetValue(c, out var opts)) foreach (var o in opts) if (o != '.') have.Add(o);
        var list = new List<(char, string)>();
        foreach (Group g in new[] { Group.Prank, Group.Hide, Group.Terrain })
            foreach (var e in Entries) if (GroupOf(e.ch) == g && (rows == null || have.Contains(e.ch) || (e.ch == 'K' && have.Contains('k')))) list.Add(e);
        return list;
    }

    /// <summary>S241 纯逻辑：场景小标签只显示你身边的（太多字看不过来）。radius ≤ 0 = 全显示。</summary>
    public static bool TagNear(Vector2 tag, Vector2 you, float radius) => radius <= 0f || Vector2.Distance(tag, you) <= radius;

    private List<(char ch, string use)> roomEntries;
    private Transform you; private float tagRadius = 5.5f; private bool glance = true;

    private void Start()
    {
        roomEntries = ForRoom(Step1PrankRoomBuilderBridge.CurrentRoom);
        var t = MarioMindTuningSO.LoadOrDefault(); if (t != null) { tagRadius = t.legendTagRadius; glance = t.glanceMap; }
        var y = FindObjectOfType<TricksterController>(); if (y != null) you = y.transform;
    }

    private void OnDestroy() { Visible = false; }

    private void Update()
    {
        if (Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen) return;
        if (Step1Keys.Down(KeyCode.M) || Step1Keys.Down(KeyCode.Tab)) { Visible = !Visible; rescan = 0f; }
        if (!Visible) return;
        rescan -= Time.unscaledDeltaTime;
        if (rescan <= 0f) { Scan(); rescan = 0.25f; }
    }

    private void Scan()
    {
        tags.Clear();
        var vents = new List<Vent>(FindObjectsOfType<Vent>());
        var ventPos = new List<Vector2>(); foreach (var v in vents) ventPos.Add(v.transform.position);
        var order = new List<int>(); for (int i = 0; i < vents.Count; i++) order.Add(i);
        order.Sort((a, b) => ventPos[a].y != ventPos[b].y ? ventPos[b].y.CompareTo(ventPos[a].y) : ventPos[a].x.CompareTo(ventPos[b].x));
        for (int k = 0; k < order.Count; k++) tags.Add((vents[order[k]].transform, $"管 {k / 2 + 1}{(k % 2 == 0 ? "A" : "B")}", new Color(0.7f, 0.8f, 1f)));
        foreach (var w in FindObjectsOfType<CrackedWall>()) if (!w.Broken) tags.Add((w.transform, "可炸", new Color(1f, 0.55f, 0.4f)));
        foreach (var d in FindObjectsOfType<OneWayDoor>()) if (!d.IsOpen) tags.Add((d.transform, d.OpenFromLeft ? "← 从左开" : "从右开 →", new Color(1f, 0.85f, 0.3f)));
        foreach (var s in FindObjectsOfType<SlowTerrain>()) tags.Add((s.transform, s.TerrainKind == SlowTerrain.Kind.Poison ? "毒" : "黏", new Color(0.7f, 1f, 0.4f)));
        foreach (var o in OilBarrel.All) if (o != null && !o.Exploded) tags.Add((o.transform, o.Lit ? "要炸了!" : "油", new Color(1f, 0.5f, 0.3f)));
        if (DecoyAbility.Active != null) tags.Add((DecoyAbility.Active.transform, "诱饵", new Color(0.6f, 0.8f, 1f)));
        foreach (var s in FindObjectsOfType<SnareTrap>()) tags.Add((s.transform, s.Armed ? "绳套" : "绳套(空)", new Color(1f, 0.85f, 0.5f)));
        foreach (var c in PranksterCannon.All) tags.Add((c.transform, c.HasAmmo ? $"炮 {c.ShotsLeft} 发" : c.LaunchCooldownRemaining > 0f ? $"冷却 {c.LaunchCooldownRemaining:F0}s" : "可钻", new Color(1f, 0.9f, 0.4f)));
        foreach (var p in FindObjectsOfType<PickupSpot>()) if (p.Live) tags.Add((p.transform, "?", new Color(1f, 0.85f, 0.2f)));
        foreach (var p in FindObjectsOfType<SceneryProp>()) if (p.gameObject.activeInHierarchy && p.name.StartsWith("Crate")) tags.Add((p.transform, "可炸", new Color(1f, 0.7f, 0.4f)));
        foreach (var d in Destructible.All) if (d != null && d.Width <= 3 && d.name.StartsWith("Wall_")) tags.Add((d.transform, "可炸", new Color(1f, 0.55f, 0.4f)));
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen) return;
        float w = Step1Gui.Begin();
        float h = Step1Gui.VirtualHeight;
        if (!Visible)
        {
            GUI.Label(new Rect(20, h - 118, 360, 30), glance ? "<color=#BBBBBB>M / Tab = 作战图 Plan</color>" : "<color=#BBBBBB>M / Tab = 图例 Legend</color>", Step1Gui.Text(18));
            return;
        }
        var entries = roomEntries ?? new List<(char, string)>(Entries);
        if (glance) { DrawGrid(entries, h); DrawTags(h); return; } // S242：图标格子（图标 + 2–4 个字），不写整句
        int groups = 0; Group? last = null; foreach (var e in entries) { var g = GroupOf(e.ch); if (g != last) { groups++; last = g; } }
        float rowH = 30f, panelH = 50f + entries.Count * rowH + groups * 26f;
        var r = new Rect(20, Mathf.Max(10f, h - 130 - panelH), 480, panelH);
        Step1Gui.Panel(r, 0.82f);
        GUI.Label(new Rect(r.x + 14, r.y + 8, 460, 30), "<b>这个房间有什么</b>  <size=16>（M/Tab 关闭）</size>", Step1Gui.Text(22));
        var old = GUI.color;
        float y = r.y + 44; last = null;
        foreach (var (ch, use) in entries)
        {
            var g = GroupOf(ch);
            if (g != last) { GUI.Label(new Rect(r.x + 14, y, 440, 24), $"<color=#FFD24A><b>{GroupTitle(g)}</b></color>", Step1Gui.Text(17, TextAnchor.MiddleLeft, false)); y += 26f; last = g; }
            var info = ElementCatalog.Get(ch);
            var icon = info != null ? Step1PropIcons.IconSprite(info.themeKey) : null;
            var box = new Rect(r.x + 16, y + 3, 24, 24);
            if (icon != null) GUI.DrawTextureWithTexCoords(box, icon.texture, new Rect(0, 0, 1, 1));
            else { var c = ElementCatalog.EditorColor(ch); c.a = 1f; GUI.color = c; GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = old; }
            GUI.Label(new Rect(r.x + 50, y, 420, rowH), $"<b>{(info != null ? info.zh : ch.ToString())}</b>  {use}", Step1Gui.Text(18, TextAnchor.MiddleLeft, false));
            y += rowH;
        }
        DrawTags(h);
    }

    /// <summary>S242：图例 = 三色分组的图标格子（红 坑他 / 蓝 躲·钻 / 灰 地形），每格 = 图标 + 2–4 个字。参考 Into the Breach、Untitled Goose Game（见 Step1Glance）。</summary>
    private void DrawGrid(List<(char ch, string use)> entries, float h)
    {
        const int cols = 4; const float cw = 112f, ch = 64f;
        var groups = new List<(Group g, List<char> items)>();
        foreach (Group g in new[] { Group.Prank, Group.Hide, Group.Terrain })
        {
            var items = new List<char>(); foreach (var e in entries) if (GroupOf(e.ch) == g) items.Add(e.ch);
            if (items.Count > 0) groups.Add((g, items));
        }
        float panelH = 46f; foreach (var gr in groups) panelH += 26f + Mathf.CeilToInt(gr.items.Count / (float)cols) * ch;
        var r = new Rect(20, Mathf.Max(10f, h - 130 - panelH), cols * cw + 20, panelH);
        Step1Gui.Panel(r, 0.82f);
        GUI.Label(new Rect(r.x + 12, r.y + 8, r.width - 24, 30), "<b>作战图</b>  <size=15>虚线 = 他要走的路  靶心 = 埋伏点   M 关</size>", Step1Gui.Text(20));
        float y = r.y + 42; var old = GUI.color;
        foreach (var (g, items) in groups)
        {
            var tint = g == Group.Prank ? Step1Glance.ColorOf(Step1Glance.Tint.Prank) : g == Group.Hide ? Step1Glance.ColorOf(Step1Glance.Tint.Hide) : Step1Glance.ColorOf(Step1Glance.Tint.Neutral);
            GUI.Label(new Rect(r.x + 12, y, r.width - 24, 24), $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}><b>{(g == Group.Prank ? "坑他（按 L）" : g == Group.Hide ? "躲 · 钻 · 捡" : "地形")}</b></color>", Step1Gui.Text(16, TextAnchor.MiddleLeft, false));
            y += 26f;
            for (int i = 0; i < items.Count; i++)
            {
                char c = items[i]; var info = ElementCatalog.Get(c);
                var cell = new Rect(r.x + 10 + (i % cols) * cw, y + (i / cols) * ch, cw - 6, ch - 6);
                GUI.color = new Color(tint.r, tint.g, tint.b, 0.18f); GUI.DrawTexture(cell, Texture2D.whiteTexture); GUI.color = old;
                var icon = info != null ? Step1PropIcons.IconSprite(info.themeKey) : null;
                var box = new Rect(cell.x + 4, cell.y + 6, 44, 44);
                if (icon != null) GUI.DrawTextureWithTexCoords(box, icon.texture, new Rect(0, 0, 1, 1));
                else { var col = ElementCatalog.EditorColor(c); col.a = 1f; GUI.color = col; GUI.DrawTexture(new Rect(box.x + 6, box.y + 6, 32, 32), Texture2D.whiteTexture); GUI.color = old; }
                string verb = Step1Glance.Verb(c);
                GUI.Label(new Rect(cell.x + 50, cell.y + 4, cell.width - 52, 26), $"<b>{(info != null ? info.zh : c.ToString())}</b>", Step1Gui.Text(15, TextAnchor.MiddleLeft, false));
                if (verb.Length > 0) GUI.Label(new Rect(cell.x + 50, cell.y + 28, cell.width - 52, 24), $"<color=#{ColorUtility.ToHtmlStringRGB(tint)}>{verb}</color>", Step1Gui.Text(15, TextAnchor.MiddleLeft, false));
            }
            y += Mathf.CeilToInt(items.Count / (float)cols) * ch;
        }
    }

    private void DrawTags(float h)
    {
        if (Camera.main == null) return;
        float scale = Mathf.Max(0.1f, Screen.height / h);
        var st = Step1Gui.Text(16, TextAnchor.MiddleCenter, false);
        foreach (var (t, text, color) in tags)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (you != null && !TagNear(t.position, you.position, tagRadius)) continue; // S241：只标你身边的
            Vector3 sp = Camera.main.WorldToScreenPoint(t.position + Vector3.up * 0.75f);
            if (sp.z < 0f) continue;
            var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            var lr = new Rect(at.x - 50, at.y - 13, 100, 26);
            Step1Gui.Panel(lr, 0.6f);
            GUI.Label(lr, $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>", st);
        }
    }
}
