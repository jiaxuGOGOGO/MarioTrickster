using UnityEngine;

/// <summary>
/// S193：裂缝地板（ASCII 'x'）—— 可破坏地形 + 玩家机关，"多层楼 / 地下监狱"题材的核心砖。
/// 平时是实心地面（可站、安全）。捣蛋者伪装在旁按 L → 预警（裂纹闪烁+抖）→ 地板碎掉，站在上面的人掉到下一层。
/// 与塌桥 'C' 的区别：
///   - 塌桥会重生（机关循环）；裂缝地板**本回合永久打开**（地形被破坏，回合重置时复原）；
///   - 设计用途：打通楼层（"从这层掉到下一层"），在关卡工坊里做纵向关卡、监狱逃脱的"凿地板"。
/// 规则约束：
///   - H1/H9：碎掉后等同"空气"。关卡工坊的死局分析会把它按"永久打开"检查——碎后仍必须能回到出口，否则标红；
///   - H3：有预警；碎地板本身不伤人；
///   - H4：不读取捣蛋者信息。
/// 参考：Spelunky 的可破坏地形（整张图可炸，炸出新路线）、Super Mario 的砖块（只借规则，不借素材）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CrackFloor : ControllableLevelElement
{
    [Header("=== 裂缝地板 ===")]
    [Tooltip("预警时裂纹颜色")]
    [SerializeField] private Color crackColor = new Color(0.95f, 0.85f, 0.35f, 1f);

    private static readonly System.Collections.Generic.List<CrackFloor> s_all = new System.Collections.Generic.List<CrackFloor>();
    private BoxCollider2D body;
    private Transform visual;
    private bool broken;
    public bool Broken => broken;
    public event System.Action<CrackFloor> Shattered;

    protected override void Awake()
    {
        propName = "裂缝地板";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.OneShot | ElementTag.Resettable | ElementTag.AffectsPhysics;
        elementDescription = "捣蛋者按 L 把地板打碎，站在上面的人掉到下一层（本回合不复原）";
        base.Awake();
        body = GetComponent<BoxCollider2D>();
        visual = transform.Find("Visual");
        telegraphColor = crackColor;
    }

    protected override void OnEnable() { base.OnEnable(); if (!s_all.Contains(this)) s_all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); s_all.Remove(this); }

    public void Configure(float telegraph) { telegraphDuration = Mathf.Max(0.1f, telegraph); }

    protected override bool ExtraControlCondition() => !broken;

    /// <summary>纯计算：从 start 出发，同一行左右相连（间距 ≤ 1.05 格）的所有裂缝格下标——裂缝会沿整条线蔓延。</summary>
    public static System.Collections.Generic.List<int> ContiguousLine(System.Collections.Generic.IList<Vector2> cells, int start)
    {
        var result = new System.Collections.Generic.List<int> { start };
        var used = new System.Collections.Generic.HashSet<int> { start };
        for (int k = 0; k < result.Count; k++)
            for (int i = 0; i < cells.Count; i++)
                if (!used.Contains(i) && Mathf.Abs(cells[i].y - cells[result[k]].y) < 0.1f && Mathf.Abs(cells[i].x - cells[result[k]].x) <= 1.05f)
                { used.Add(i); result.Add(i); }
        return result;
    }
    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }

    private static readonly Collider2D[] s_hits = new Collider2D[8];
    private static int s_fellFrame = -1;

    protected override void OnActivate(Vector2 direction)
    {
        if (broken) return;
        // 裂缝沿同一行相连的地板蔓延（一次 L 打开整条裂缝）
        var cells = new System.Collections.Generic.List<Vector2>(s_all.Count);
        foreach (var c in s_all) cells.Add(c.transform.position);
        int self = s_all.IndexOf(this);
        if (self < 0) { Shatter(); return; }
        foreach (int i in ContiguousLine(cells, self)) s_all[i].Shatter();
    }

    /// <summary>S197：被炸弹炸开（整条裂缝一起碎，与按 L 同效果）。</summary>
    public void ShatterFromBlast() => OnActivate(Vector2.zero);

    private void Shatter()
    {
        if (broken) return;
        // 碎之前看看马里奥是不是正站在上面（用于连招计"掉下一层"）
        if (body != null)
        {
            Bounds b = body.bounds;
            int n = Physics2D.OverlapBoxNonAlloc(new Vector2(b.center.x, b.max.y + 0.3f), new Vector2(b.size.x * 0.9f, 0.6f), 0f, s_hits);
            for (int i = 0; i < n; i++)
                if (s_hits[i] != null && s_hits[i].GetComponentInParent<MarioController>() != null) { if (!s_fellFrame.Equals(Time.frameCount)) { s_fellFrame = Time.frameCount; CrackFloorEvents.RaiseMarioFell(); } break; }
        }
        broken = true;
        if (body != null) body.enabled = false;
        if (visual != null) visual.gameObject.SetActive(false);
        else if (spriteRenderer != null) spriteRenderer.enabled = false;
        Shattered?.Invoke(this);
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        broken = false;
        if (body != null) body.enabled = true;
        if (visual != null) visual.gameObject.SetActive(true);
        else if (spriteRenderer != null) spriteRenderer.enabled = true;
    }
}
