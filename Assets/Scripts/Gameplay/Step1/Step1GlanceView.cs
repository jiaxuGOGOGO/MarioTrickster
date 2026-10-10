using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S242：一目了然——画在世界里的"作战图"（代替以前按 M / V 出来的一堆字）。由 Step1Combo 运行时自动挂上（旧场景不用重建）。
///   · 马里奥头顶一个图标气泡：宝箱 = 去拿宝、门 = 要逃了、? = 起疑、! = 追你、眼 = 找你、星 = 晕了（不写字）；
///   · 马里奥接下来要走的路：一串小点（平时淡淡的；按 M 变亮）——和他自己寻路用的是同一个 LevelPathPlanner；
///   · 按 M：路线上他最先经过的几个机关标红色"埋伏点"靶心 + 2 个字的动词 + "3 秒"（他几秒后走到）；
///   · 按 M：只给离你最近的一个东西出一张小卡（图标 + 名字 + 一句话），别的不写字。
/// 颜色只用三种：红 = 坑他、蓝 = 躲 / 钻、黄 = 目标（Step1Glance.ColorOf）。参考见 Step1Glance。纯画面，不影响玩法。
/// </summary>
public class Step1GlanceView : MonoBehaviour
{
    private MarioMindTuningSO tuning;
    private MarioMindDriver driver; private MarioController mario; private Transform you;
    private string[] grid;
    private float replan;
    private readonly List<Vector2> route = new List<Vector2>();
    private List<Vector2> dashes = new List<Vector2>();
    private List<Step1Glance.Ambush> ambushes = new List<Step1Glance.Ambush>();
    private readonly List<(Transform t, char ch, ControllablePropBase prop)> things = new List<(Transform, char, ControllablePropBase)>();
    private Vector2 lastMario; private float speedEst = 4f;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        driver = FindObjectOfType<MarioMindDriver>();
        mario = driver != null ? driver.GetComponent<MarioController>() : null;
        var y = FindObjectOfType<TricksterController>(); // 只拿位置（给"离你最近的说明卡"用；显示层，马里奥心智不读）
        you = y != null ? y.transform : null;
        var src = Step1PrankRoomBuilderBridge.CurrentRoom;
        grid = src != null ? System.Array.ConvertAll(src, Step1Layout.StripSlots) : null;
        var root = GameObject.Find("Step1_PrankRoom");
        if (root != null)
            foreach (Transform c in root.transform)
            {
                var info = ElementCatalog.ByKey(Step1ElementLabels.KeyOf(c.name));
                if (info == null || Step1Glance.Verb(info.ch).Length == 0) continue;
                things.Add((c, info.ch, c.GetComponent<ControllablePropBase>()));
            }
        if (mario != null) lastMario = mario.transform.position;
    }

    private bool On => tuning != null && tuning.glanceMap && !Step1HandsOffCheck.IsRunning;

    private void Update()
    {
        if (!On || driver == null || mario == null) return;
        Vector2 mp = mario.transform.position;
        if (Time.deltaTime > 0f) speedEst = Mathf.Lerp(speedEst, Mathf.Clamp((mp - lastMario).magnitude / Time.deltaTime, 1.5f, 8f), 0.05f);
        lastMario = mp;
        replan -= Time.deltaTime;
        if (replan > 0f) return;
        replan = 0.5f;
        Plan(mp);
    }

    private void Plan(Vector2 mp)
    {
        route.Clear(); dashes.Clear(); ambushes.Clear();
        var goal = driver.Mind != null && (driver.Mind.State == MarioMindState.Chasing || driver.Mind.State == MarioMindState.Investigating) ? (Vector2?)driver.Mind.Focus : driver.CurrentGoal();
        if (grid == null || goal == null) return;
        var path = LevelPathPlanner.Path(grid, new LevelPathPlanner.Cell(Mathf.RoundToInt(mp.x), Mathf.RoundToInt(mp.y)), new LevelPathPlanner.Cell(Mathf.RoundToInt(goal.Value.x), Mathf.RoundToInt(goal.Value.y)));
        if (path == null) return;
        route.Add(mp);
        foreach (var c in path) route.Add(new Vector2(c.x, c.y));
        dashes = Step1Glance.Dashes(route, 0.6f);
        var props = new List<(Vector2, char, bool)>();
        foreach (var (t, ch, prop) in things) if (t != null && t.gameObject.activeInHierarchy) props.Add(((Vector2)t.position, ch, prop == null || !prop.SpentThisRound));
        ambushes = Step1Glance.AmbushesOnRoute(route, props, speedEst, 1.2f, tuning.glanceAmbushCount);
    }

    private Vector2 Screen2(Vector3 world, float scale)
    {
        Vector3 sp = Camera.main.WorldToScreenPoint(world);
        return sp.z < 0f ? new Vector2(-999, -999) : new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
    }

    private void OnGUI()
    {
        if (!On || Step1Screen.HelpOpen || Camera.main == null || mario == null) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        bool full = Step1MapLegend.Visible;
        var old = GUI.color;

        // 路线小点
        if (full || tuning.glanceRouteAlways)
        {
            var c = Step1Glance.ColorOf(Step1Glance.Tint.Goal); c.a = full ? 0.9f : 0.35f;
            GUI.color = c;
            float d = full ? 7f : 5f;
            for (int i = 1; i < dashes.Count; i++) { var p = Screen2(dashes[i], scale); GUI.DrawTexture(new Rect(p.x - d * 0.5f, p.y - d * 0.5f, d, d), Texture2D.whiteTexture); }
            GUI.color = old;
        }

        // 头顶意图气泡
        if (driver != null && driver.Mind != null)
        {
            var intent = Step1Glance.IntentOf(driver.Mind.State.ToString(), LootObjective.IsLootCarried, mario.IsStunned);
            var icon = Step1PropIcons.IconSprite(Step1Glance.IntentIcon(intent));
            var p = Screen2(mario.transform.position + Vector3.up * 1.55f, scale);
            if (icon != null) GUI.DrawTextureWithTexCoords(new Rect(p.x - 17, p.y - 34, 34, 34), icon.texture, new Rect(0, 0, 1, 1));
            if (full) GUI.Label(new Rect(p.x - 50, p.y, 100, 20), $"<b>{Step1Glance.IntentZh(intent)}</b>", Step1Gui.Text(15, TextAnchor.UpperCenter, false));
        }
        if (!full) return;

        // 埋伏点：靶心 + 动词 + 几秒后到
        var target = Step1PropIcons.IconSprite("BadgeAmbush");
        foreach (var a in ambushes)
        {
            var p = Screen2(new Vector3(a.pos.x, a.pos.y + 1.1f, 0f), scale);
            if (target != null) GUI.DrawTextureWithTexCoords(new Rect(p.x - 15, p.y - 30, 30, 30), target.texture, new Rect(0, 0, 1, 1));
            var r = new Rect(p.x - 46, p.y + 1, 92, 24);
            Step1Gui.Panel(r, 0.7f);
            GUI.Label(r, $"<color=#{ColorUtility.ToHtmlStringRGB(Step1Glance.ColorOf(Step1Glance.Tint.Prank))}><b>{Step1Glance.Verb(a.ch)}</b></color> {Step1Glance.EtaText(a.eta)}", Step1Gui.Text(15, TextAnchor.MiddleCenter, false));
        }

        // 只给离你最近的一个东西出说明卡
        if (you != null)
        {
            var pos = new List<Vector2>(); foreach (var th in things) pos.Add(th.t != null && th.t.gameObject.activeInHierarchy ? (Vector2)th.t.position : new Vector2(-999, -999));
            int i = Step1Glance.Nearest(pos, you.position, 2.5f);
            if (i >= 0)
            {
                var (t, ch, prop) = things[i]; var info = ElementCatalog.Get(ch);
                var p = Screen2(t.position + Vector3.up * 1.2f, scale);
                var r = new Rect(p.x - 140, p.y - 66, 280, 58);
                Step1Gui.Panel(r, 0.85f);
                var tint = Step1Glance.ColorOf(Step1Glance.TintOf(ch));
                GUI.color = tint; GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), Texture2D.whiteTexture); GUI.color = old;
                var ic = info != null ? Step1PropIcons.IconSprite(info.themeKey) : null;
                if (ic != null) GUI.DrawTextureWithTexCoords(new Rect(r.x + 10, r.y + 9, 40, 40), ic.texture, new Rect(0, 0, 1, 1));
                string spent = prop != null && prop.SpentThisRound ? "  <color=#999999>（这局用过了）</color>" : "";
                GUI.Label(new Rect(r.x + 58, r.y + 4, r.width - 64, 26), $"<b>{(info != null ? info.zh : ch.ToString())}</b>  <color=#{ColorUtility.ToHtmlStringRGB(tint)}>{Step1Glance.Verb(ch)}</color>{spent}", Step1Gui.Text(18, TextAnchor.MiddleLeft, false));
                GUI.Label(new Rect(r.x + 58, r.y + 30, r.width - 64, 24), Step1Glance.HowTo(ch), Step1Gui.Text(14, TextAnchor.MiddleLeft, false));
            }
        }
    }
}
