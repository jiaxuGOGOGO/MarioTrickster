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

    public static readonly (char ch, string use)[] Entries =
    {
        ('#', "地面：挡路"), ('W', "墙：挡路挡视线（炸不开）"), ('%', "裂墙：<b>炸弹能炸开</b>"),
        ('x', "裂缝地板：按 L 踩塌 / 炸弹炸开"), ('|', "捷径门：只能从一侧推开"), ('-', "单向台面：能从下面跳上去"),
        ('C', "塌桥：按 L 让它塌"), ('c', "箱子：挡路挡视线，<b>炸弹能炸掉</b>"), ('b', "草丛：能躲"),
        ('O', "通风管：按 ↓ 钻到配对的管口"), ('w', "毒池：减速 + 晕"), ('g', "黏胶：减速、跳不高"),
        ('J', "弹簧板：按 L 弹飞他"), ('n', "香蕉皮：按 L 让他滑"), ('~', "火：按 L 喷火"), ('[', "封路墙：按 L 升墙"),
    };

    private void OnDestroy() { Visible = false; }

    private void Update()
    {
        if (Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen) return;
        if (Step1Keys.Down(KeyCode.M) || Step1Keys.Down(KeyCode.Tab)) { Visible = !Visible; rescan = 0f; }
        if (!Visible) return;
        rescan -= Time.unscaledDeltaTime;
        if (rescan <= 0f) { Scan(); rescan = 0.5f; }
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
        foreach (var p in FindObjectsOfType<SceneryProp>()) if (p.gameObject.activeInHierarchy && p.name.StartsWith("Crate")) tags.Add((p.transform, "可炸", new Color(1f, 0.7f, 0.4f)));
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen) return;
        float w = Step1Gui.Begin();
        float h = Step1Gui.VirtualHeight;
        if (!Visible)
        {
            GUI.Label(new Rect(20, h - 118, 360, 30), "<color=#BBBBBB>M / Tab = 图例 Legend</color>", Step1Gui.Text(18));
            return;
        }
        float rowH = 30f, panelH = 50f + Entries.Length * rowH;
        var r = new Rect(20, h - 130 - panelH, 460, panelH);
        Step1Gui.Panel(r, 0.82f);
        GUI.Label(new Rect(r.x + 14, r.y + 8, 440, 30), "<b>图例 Legend</b>  <size=16>（M/Tab 关闭）</size>", Step1Gui.Text(22));
        var old = GUI.color;
        for (int i = 0; i < Entries.Length; i++)
        {
            var (ch, use) = Entries[i];
            float y = r.y + 44 + i * rowH;
            var c = ElementCatalog.EditorColor(ch); c.a = 1f;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x + 16, y + 4, 22, 22), Texture2D.whiteTexture);
            GUI.color = old;
            var info = ElementCatalog.Get(ch);
            GUI.Label(new Rect(r.x + 48, y, 400, rowH), $"<b>{(info != null ? info.zh : ch.ToString())}</b>  {use}", Step1Gui.Text(18, TextAnchor.MiddleLeft, false));
        }
        if (Camera.main == null) return;
        float scale = Mathf.Max(0.1f, Screen.height / h);
        var st = Step1Gui.Text(16, TextAnchor.MiddleCenter, false);
        foreach (var (t, text, color) in tags)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(t.position + Vector3.up * 0.75f);
            if (sp.z < 0f) continue;
            var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            var lr = new Rect(at.x - 50, at.y - 13, 100, 26);
            Step1Gui.Panel(lr, 0.6f);
            GUI.Label(lr, $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>", st);
        }
    }
}
