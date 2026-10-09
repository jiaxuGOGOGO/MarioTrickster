using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S242：伪装装备栏（挂在捣蛋者身上，Step1Lighting 运行时自动挂，旧场景不用重建）。
/// 用户："捣蛋者可以选择三个（或者更多可自定义）游戏场景内的道具或者机关本身……诱饵也是选择的这三个中的任何一个 可以自由切换"。
///   · 开局自动带这个房间里最多的 disguiseLoadoutSize 种东西（藏木于林）；外观照抄房间里真的那个（图 + 颜色 + 大小 + 图标）。
///   · 数字键 1–N 选形态（伪装中也能换——换的一瞬间被他看见 = 起疑）；O / I 也能轮换。
///   · 站在一个东西旁边按 E = 把它"取样"进当前格（换掉原来的）。
///   · G 诱饵 = 丢出当前格的假道具（DecoyAbility 读 CurrentChar）。
///   · 屏幕底部中间一排小格：图标 + 数字键，当前格亮框。
/// 逻辑在 Step1Loadout（sim 能测）。只改你自己的外观，马里奥照旧只用眼睛看（H4）。
/// </summary>
public class TricksterLoadout : MonoBehaviour
{
    public static TricksterLoadout Instance { get; private set; }

    private sealed class Look { public char ch; public Sprite sprite, icon; public Color color; public Vector2 size; public string zh; }

    private readonly Dictionary<char, Look> looks = new Dictionary<char, Look>();
    private List<char> slots = new List<char>();
    private TricksterController self; private DisguiseSystem disguise; private MarioMindTuningSO tuning; private GameManager manager;
    private Transform iconChild; private SpriteRenderer bodySr; // 身体的图（在建图标之前记下，免得找成图标自己）

    public int Count => slots.Count;
    public int Current => disguise != null ? disguise.CurrentIndex : 0;
    public char CurrentChar => Current >= 0 && Current < slots.Count ? slots[Current] : '\0';
    public IReadOnlyList<char> Slots => slots;

    /// <summary>第 i 格的外观（诱饵照抄）。</summary>
    public (Sprite sprite, Color color, Vector2 size, Sprite icon) LookOf(char ch) =>
        looks.TryGetValue(ch, out var l) ? (l.sprite, l.color, l.size, l.icon) : (null, Color.white, Vector2.one, null);

    public string Describe()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < slots.Count; i++) sb.Append(i == Current ? "[" : "").Append(i + 1).Append(':').Append(NameOf(slots[i])).Append(i == Current ? "] " : " ");
        return sb.ToString().Trim();
    }

    private string NameOf(char ch) { var i = ElementCatalog.Get(ch); return i != null ? i.zh : ch.ToString(); }

    private void Awake() { Instance = this; }

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        disguise = GetComponent<DisguiseSystem>();
        bodySr = GetComponentInChildren<SpriteRenderer>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += Build;
        Build();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; if (manager != null) manager.OnRoundStart -= Build; }

    /// <summary>扫一遍房间：每种可变的东西记下第一个的外观；按房间里多少排出默认装备。</summary>
    private void Build()
    {
        if (disguise == null) return;
        looks.Clear();
        var root = GameObject.Find("Step1_PrankRoom");
        if (root == null) return;
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            var info = ElementCatalog.ByKey(Step1ElementLabels.KeyOf(child.name));
            if (info == null) continue;
            char ch = info.ch == 'k' ? 'K' : info.ch;
            if (!Step1Loadout.CanDisguiseAs(ch) || looks.ContainsKey(ch)) continue;
            var vis = child.Find("Visual");
            var sr = vis != null ? vis.GetComponent<SpriteRenderer>() : child.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null) continue;
            var ic = child.Find("Icon");
            var isr = ic != null ? ic.GetComponent<SpriteRenderer>() : null;
            looks[ch] = new Look
            {
                ch = ch, sprite = sr.sprite, color = sr.color, zh = info.zh,
                size = new Vector2(Mathf.Abs(sr.transform.lossyScale.x) * sr.sprite.bounds.size.x, Mathf.Abs(sr.transform.lossyScale.y) * sr.sprite.bounds.size.y),
                icon = isr != null ? isr.sprite : Step1PropIcons.IsWhiteBox((int)sr.sprite.rect.width, (int)sr.sprite.rect.height) ? Step1PropIcons.IconSprite(info.themeKey) : null, // 换了美术的不叠图标（和房间里一样）
            };
        }
        if (looks.Count == 0) return; // 房间里没有能变的 → 保留旧的固定形态
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom;
        var stripped = rows != null ? System.Array.ConvertAll(rows, Step1Layout.StripSlots) : null;
        var want = Step1Loadout.DefaultPick(stripped, tuning.disguiseLoadoutSize);
        slots = new List<char>();
        foreach (var c in want) if (looks.ContainsKey(c)) slots.Add(c);
        foreach (var c in Step1Loadout.Candidates(stripped)) { if (slots.Count >= Step1Loadout.ClampSize(tuning.disguiseLoadoutSize)) break; if (looks.ContainsKey(c) && !slots.Contains(c)) slots.Add(c); }
        if (slots.Count == 0) foreach (var c in looks.Keys) { slots.Add(c); if (slots.Count >= Step1Loadout.ClampSize(tuning.disguiseLoadoutSize)) break; }
        Apply(0);
    }

    private void Apply(int select)
    {
        var list = new List<DisguiseData>();
        foreach (var c in slots)
        {
            var l = looks[c]; var body = Step1Loadout.BodySize(l.size);
            // 视觉缩放 = 那个东西的大小 / 身体原图大小（sprite 是 4×4 白方块 → 1 格 = 1）
            list.Add(new DisguiseData
            {
                disguiseName = l.zh, disguiseSprite = l.sprite, iconSprite = l.icon, tint = l.color, sourceChar = c,
                customColliderSize = body, customScale = new Vector3(l.size.x / Mathf.Max(0.01f, l.sprite.bounds.size.x), l.size.y / Mathf.Max(0.01f, l.sprite.bounds.size.y), 1f),
            });
        }
        disguise.SetDisguises(list, select);
        RefreshIcon();
    }

    private void Update()
    {
        if (self == null || disguise == null || slots.Count == 0) return;
        if (Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f || Step1Feedback.TagOpen) return;
        if (manager != null && manager.CurrentState != GameState.Playing) return; // 一局结束的问卷也用数字键
        if (PranksterCannon.TricksterSeated || TricksterBurrow.BodyBusy) return;
        int d = Step1Keys.Digit1to5();
        int slot = Step1Loadout.SlotOfDigit(d, slots.Count);
        if (slot >= 0 && slot != Current) { disguise.Select(slot); RefreshIcon(); Step1Hint.Show(string.Format(Step1Text.LoadoutPicked, slot + 1, NameOf(slots[slot])), 1f); }
        if (Step1Keys.Down(KeyCode.E)) TrySample();
    }

    /// <summary>E：把身边 1.3 格内最近的可变东西放进当前格。</summary>
    private void TrySample()
    {
        var root = GameObject.Find("Step1_PrankRoom");
        if (root == null) return;
        Transform best = null; char bc = '\0'; float bd = 1.3f;
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeInHierarchy) continue;
            var info = ElementCatalog.ByKey(Step1ElementLabels.KeyOf(child.name));
            if (info == null) continue;
            char ch = info.ch == 'k' ? 'K' : info.ch;
            if (!Step1Loadout.CanDisguiseAs(ch) || !looks.ContainsKey(ch)) continue;
            float dd = Vector2.Distance(child.position, transform.position);
            if (dd < bd) { bd = dd; best = child; bc = ch; }
        }
        if (best == null) { Step1Hint.Show(Step1Text.LoadoutNothingNear, 1.4f); return; }
        if (slots[Current] == bc) { Step1Hint.Show(string.Format(Step1Text.LoadoutAlready, NameOf(bc)), 1.2f); return; }
        int keep = Current;
        slots = Step1Loadout.Sample(slots, keep, bc);
        Apply(keep);
        Step1Hint.Show(string.Format(Step1Text.LoadoutSampled, keep + 1, NameOf(bc)), 1.4f);
    }

    /// <summary>伪装时把那个东西的小图标也叠在身上（和房间里的一模一样）。</summary>
    private void RefreshIcon()
    {
        if (iconChild == null) { var go = new GameObject("LoadoutIcon"); go.transform.SetParent(transform, false); iconChild = go.transform; go.AddComponent<SpriteRenderer>(); }
        var sr = iconChild.GetComponent<SpriteRenderer>();
        var l = slots.Count > 0 && looks.TryGetValue(CurrentChar, out var lk) ? lk : null;
        sr.sprite = l != null ? l.icon : null;
        float s = l != null ? Mathf.Clamp(Mathf.Min(l.size.x, l.size.y) * 0.9f, 0.55f, 0.95f) : 0.8f;
        iconChild.localScale = new Vector3(s, s, 1f);
        sr.sortingOrder = bodySr != null ? bodySr.sortingOrder + 1 : 6;
    }

    private void LateUpdate()
    {
        if (iconChild == null) return;
        var sr = iconChild.GetComponent<SpriteRenderer>();
        sr.enabled = self != null && self.IsDisguised && sr.sprite != null && tuning != null && tuning.propIcons;
        if (sr.enabled) { var bp = bodySr != null ? bodySr.transform.localPosition : Vector3.zero; iconChild.localPosition = new Vector3(bp.x, bp.y, -0.01f); sr.sortingOrder = bodySr != null ? bodySr.sortingOrder + 1 : 6; } // 融入时身体层级会变，图标跟着
    }

    /// <summary>屏幕底部中间的快捷栏（图标 + 数字键）。</summary>
    private void OnGUI()
    {
        if (slots.Count == 0 || Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen) return;
        float w = Step1Gui.Begin(), h = Step1Gui.VirtualHeight;
        const float cell = 58f, gap = 8f;
        float total = slots.Count * cell + (slots.Count - 1) * gap;
        float x0 = w * 0.5f - total * 0.5f, y0 = h - 132f - cell;
        var old = GUI.color;
        for (int i = 0; i < slots.Count; i++)
        {
            var r = new Rect(x0 + i * (cell + gap), y0, cell, cell);
            bool cur = i == Current;
            Step1Gui.Panel(r, cur ? 0.85f : 0.55f);
            if (cur) { GUI.color = Step1Glance.ColorOf(Step1Glance.Tint.Hide); DrawFrame(r, 3f); GUI.color = old; }
            var l = looks.TryGetValue(slots[i], out var lk) ? lk : null;
            var tex = l != null && l.icon != null ? l.icon.texture : null;
            var inner = new Rect(r.x + 8, r.y + 6, cell - 16, cell - 16);
            if (tex != null) GUI.DrawTextureWithTexCoords(inner, tex, new Rect(0, 0, 1, 1));
            else if (l != null) { GUI.color = l.color; GUI.DrawTexture(inner, Texture2D.whiteTexture); GUI.color = old; }
            GUI.Label(new Rect(r.x + 3, r.y + 1, 20, 20), $"<b>{i + 1}</b>", Step1Gui.Text(15, TextAnchor.UpperLeft, false));
        }
        var cap = new Rect(x0 - 4, y0 + cell + 2, total + 8, 22);
        GUI.Label(cap, $"<size=14>{Step1Text.LoadoutCaption}</size>", Step1Gui.Text(14, TextAnchor.MiddleCenter, false));
    }

    private static void DrawFrame(Rect r, float t)
    {
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
    }
}
