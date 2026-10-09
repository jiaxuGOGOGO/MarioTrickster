using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S188：按 V 在每个关卡元素头上显示"它是什么"（中英文名 + 字符），再按 V 关闭。
/// 名字来自 ElementCatalog（唯一来源），按物体名前缀（= 主题键）匹配，所以美术换图后标签依然正确。
/// 地形（地面/墙/台面）不标，避免满屏字。纯表现，不影响玩法。
/// </summary>
public class Step1ElementLabels : MonoBehaviour
{
    public static bool Visible { get; private set; }

    private readonly List<(Transform t, string text, Color color)> items = new List<(Transform, string, Color)>();
    private float rescan;

    private Transform you; private float tagRadius = 5.5f;
    private void Start()
    {
        var t = MarioMindTuningSO.LoadOrDefault(); if (t != null) tagRadius = t.legendTagRadius;
        var y = FindObjectOfType<TricksterController>(); if (y != null) you = y.transform;
    }

    private void OnDestroy() { Visible = false; }

    private void Update()
    {
        if (Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen) return;
        if (Step1Keys.Down(KeyCode.V)) { Visible = !Visible; rescan = 0f; }
        if (!Visible) return;
        rescan -= Time.unscaledDeltaTime;
        if (rescan <= 0f) { Scan(); rescan = 1f; }
    }

    private void Scan()
    {
        items.Clear();
        var root = GameObject.Find("Step1_PrankRoom");
        if (root == null) return;
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            string key = KeyOf(child.name);
            var info = ElementCatalog.ByKey(key);
            if (info == null || info.role == ElementCatalog.Role.Terrain) continue;
            // 大炮区分朝向
            if (info.muzzle != 0)
            {
                var cannon = child.GetComponent<PranksterCannon>();
                if (cannon != null) info = ElementCatalog.Get(cannon.FacingRight ? 'K' : 'k') ?? info;
            }
            // S242：只写短名 + 2 个字的动词（以前中英两行，满屏字）；颜色 = 作战图三色
            string verb = Step1Glance.Verb(info.ch);
            items.Add((child, verb.Length > 0 ? $"{info.zh} · {verb}" : info.zh, Step1Glance.TintOf(info.ch) == Step1Glance.Tint.Neutral ? ColorOf(info.role) : Step1Glance.ColorOf(Step1Glance.TintOf(info.ch))));
        }
    }

    public static string KeyOf(string objectName)
    {
        // 生成器命名 "Key_x_y"；实战房会把宝物/出口改名为 "LootObjective_Collectible_x_y" / "EscapeGate_GoalZone_x_y"
        string n = objectName ?? "";
        if (n.StartsWith("LootObjective_")) n = n.Substring("LootObjective_".Length);
        if (n.StartsWith("EscapeGate_")) n = n.Substring("EscapeGate_".Length);
        int cut = n.IndexOf('_');
        return cut > 0 ? n.Substring(0, cut) : n;
    }

    private static Color ColorOf(ElementCatalog.Role role)
    {
        switch (role)
        {
            case ElementCatalog.Role.PlayerPrank: return new Color(1f, 0.82f, 0.3f);
            case ElementCatalog.Role.Scenery: return new Color(0.7f, 0.95f, 0.7f);
            case ElementCatalog.Role.Objective: return new Color(0.6f, 0.9f, 1f);
            default: return Color.white;
        }
    }

    private void OnGUI()
    {
        if (!Visible || Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Camera.main == null) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        var style = Step1Gui.Text(16, TextAnchor.MiddleCenter, false);
        foreach (var (t, text, color) in items)
        {
            if (t == null) continue;
            if (you != null && !Step1MapLegend.TagNear(t.position, you.position, tagRadius)) continue; // S241：只标你身边的（满屏字 = 看不过来）
            Vector3 sp = Camera.main.WorldToScreenPoint(t.position + Vector3.up * 0.9f);
            if (sp.z < 0f) continue;
            var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            var r = new Rect(at.x - 64f, at.y - 30f, 128f, 26f);
            Step1Gui.Panel(r, 0.65f);
            var old = GUI.color; GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }
        var tip = new Rect(Step1Gui.Begin() * 0.5f - 200f, 70f, 400f, 36f);
        Step1Gui.Panel(tip, 0.6f);
        GUI.Label(tip, "你身边的东西叫什么（V 关闭）  Labels near you (V to hide)", Step1Gui.Text(18, TextAnchor.MiddleCenter, false));
    }
}
