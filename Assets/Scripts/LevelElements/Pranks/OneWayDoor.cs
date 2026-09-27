using UnityEngine;

/// <summary>
/// S196：单向捷径门（ASCII '|'）—— 魂系"从另一边打开的门"（Undead Burg / Stormveil 的核心手法）。
/// 平时是实心墙。**只能从门的开启侧打开**：有人（马里奥或捣蛋者）从开启侧贴近门站 openSeconds 秒 → 门打开，本回合保持打开。
/// 箱庭意义：第一次到这里时只能看见门，打不开 → 绕一大圈从另一边回来 → 打开后形成回到起点附近的捷径（loop back）。
/// 规则：
///   - H1/H9：死局检查把门当"关着"（最坏情况）；工坊"箱庭总览"单独把它标为"捷径"（打开后的可达性另算，只是奖励）。
///   - H4：门不读取捣蛋者信息；马里奥开门只因为他走到了开启侧（寻路按关门算，不会把门当必经路）。
///   - 开启侧：构建器自动判定——门两侧哪一侧**离马里奥出生点的路更远**，就从哪一侧开（= 先绕远路才能打开，经典魂系捷径）。
///     也可在 Inspector 勾选 openFromLeft 手动改。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class OneWayDoor : LevelElementBase
{
    [Tooltip("true = 只能从左边打开；false = 只能从右边打开")]
    [SerializeField] private bool openFromLeft = false;
    [Tooltip("贴近门站多久打开（秒）")]
    [SerializeField] private float openSeconds = 0.6f;
    [Tooltip("多近算贴近（格）")]
    [SerializeField] private float reach = 0.9f;

    private BoxCollider2D body;
    private Transform visual;
    private float progress;
    private bool open;
    private static readonly Collider2D[] s_hits = new Collider2D[8];
    public bool IsOpen => open;
    public bool OpenFromLeft => openFromLeft;
    public static event System.Action<OneWayDoor> Opened;
    private static readonly System.Collections.Generic.List<OneWayDoor> doors = new System.Collections.Generic.List<OneWayDoor>();
    public static System.Collections.Generic.IReadOnlyList<OneWayDoor> AllDoors => doors;
    private float shake;
    protected override void OnEnable() { base.OnEnable(); if (!doors.Contains(this)) doors.Add(this); }
    protected override void OnDisable() { base.OnDisable(); doors.Remove(this); }
    /// <summary>S199：被踢时抖动（看得见，H3）。</summary>
    public void Shake() { shake = 0.15f; }

    public void Configure(bool fromLeft, float seconds) { openFromLeft = fromLeft; openSeconds = seconds; }

    private void Awake()
    {
        elementName = "捷径门";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive | ElementTag.Resettable | ElementTag.AffectsPhysics;
        description = "只能从一侧打开的门；打开后本回合保持打开（魂系捷径）";
        body = GetComponent<BoxCollider2D>();
        visual = transform.Find("Visual");
        if (GetComponent<OneWayDoorOpensFromLeft>() != null) openFromLeft = true;
    }

    /// <summary>纯逻辑：站在 who 位置的人能不能开这扇门（门在 door，开启侧 fromLeft）。</summary>
    public static bool OnOpeningSide(Vector2 door, Vector2 who, bool fromLeft, float reach)
    {
        float dx = who.x - door.x;
        if (Mathf.Abs(who.y - door.y) > 0.8f) return false;
        return fromLeft ? dx < 0f && dx > -(0.5f + reach) : dx > 0f && dx < 0.5f + reach;
    }

    private void Update()
    {
        if (open) return;
        if (shake > 0f && visual != null) { shake -= Time.deltaTime; visual.localPosition = new Vector3(Mathf.Sin(Time.time * 60f) * 0.06f, 0f, 0f); if (shake <= 0f) visual.localPosition = Vector3.zero; }
        Vector2 door = transform.position;
        Vector2 probe = door + new Vector2(openFromLeft ? -(0.5f + reach * 0.5f) : (0.5f + reach * 0.5f), 0f);
        int n = Physics2D.OverlapBoxNonAlloc(probe, new Vector2(reach, 0.9f), 0f, s_hits);
        bool someone = false;
        for (int i = 0; i < n; i++)
        {
            var c = s_hits[i];
            if (c == null || c.attachedRigidbody == null) continue;
            if (c.GetComponentInParent<MarioController>() == null && c.GetComponentInParent<TricksterController>() == null) continue;
            if (OnOpeningSide(door, c.transform.position, openFromLeft, reach)) { someone = true; break; }
        }
        progress = someone ? progress + Time.deltaTime : 0f;
        if (visual != null) visual.localScale = new Vector3(1f - Mathf.Clamp01(progress / Mathf.Max(0.01f, openSeconds)) * 0.3f, 1f, 1f);
        if (progress >= openSeconds) Open();
    }

    public void Open()
    {
        if (open) return;
        open = true;
        if (body != null) body.enabled = false;
        if (visual != null) { visual.localScale = new Vector3(0.15f, 1f, 1f); visual.localPosition = new Vector3(openFromLeft ? 0.42f : -0.42f, 0f, 0f); }
        Opened?.Invoke(this);
    }

    public override void OnLevelReset()
    {
        open = false; progress = 0f;
        if (body != null) body.enabled = true;
        if (visual != null) { visual.localScale = Vector3.one; visual.localPosition = Vector3.zero; }
    }
}

/// <summary>标记组件：挂上它的捷径门只能从左边打开（构建器按模板元数据添加，零代码扩展）。</summary>
public class OneWayDoorOpensFromLeft : MonoBehaviour { }
