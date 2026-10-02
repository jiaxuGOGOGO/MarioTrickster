using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S197：通风管（ASCII 'O'）—— 捣蛋者专用的快速转移通道（马里奥进不去：管口太小）。
/// 配对规则（零配置）：房间里的通风管按"从上到下、从左到右"编号，**1↔2、3↔4…** 两两相连。
/// 用法：站在管口上按 ↓（或 S）；管口贴墙时也可以朝墙按 ← / →（墙上的通风口）→ 0.35 秒钻进去（看得见的钻入动画，H3）→ 从配对的管口出来，冷却 ventCooldown 秒。
/// 代价：钻管时解除伪装；管口会"咣当"一声，**离出入口近的马里奥听得见**（H4：只知道位置），所以不是无代价瞬移。
/// 箱庭意义：让捣蛋者能在层间快速换场地——一条只属于你的"内部通道"（Hollow Knight 的鹿角站 / 魂系的电梯，只借规则）。
/// 不参与马里奥可达性（可站立、不是实心、不是危险）。
/// </summary>
public class Vent : LevelElementBase
{
    [SerializeField] private float enterSeconds = 0.35f;
    [SerializeField] private float cooldown = 3f;
    private static readonly List<Vent> all = new List<Vent>();
    /// <summary>S226 E9：p 附近 r 格内有没有通风管（按键条用来决定要不要显示"↓ 钻通风管"）。</summary>
    public static bool AnyWithin(Vector2 p, float r)
    {
        foreach (var v in all) if (v != null && ((Vector2)v.transform.position - p).sqrMagnitude <= r * r) return true;
        return false;
    }
    private static float sharedCooldown;
    private TricksterController loading;
    private float timer;
    public static event System.Action<Vector2> Clanged;

    [Tooltip("S198：管口左侧紧贴墙（朝墙按 ← 进管）")]
    [SerializeField] private bool wallLeft;
    [Tooltip("S198：管口右侧紧贴墙（朝墙按 → 进管）")]
    [SerializeField] private bool wallRight;
    public bool WallLeft => wallLeft; public bool WallRight => wallRight;

    public void Configure(float enter, float cd) { enterSeconds = enter; cooldown = cd; }
    public void ConfigureWalls(bool left, bool right) { wallLeft = left; wallRight = right; }

    private void Awake()
    {
        elementName = "通风管";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive;
        description = "捣蛋者站在管口按 ↓ 钻到配对的管口（马里奥进不去，但听得见咣当声）";
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
    }

    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); }

    /// <summary>纯逻辑：给定所有管口位置，返回 index 的配对下标（按上→下、左→右排序后 0↔1、2↔3…；落单的返回 -1）。</summary>
    public static int PairOf(IList<Vector2> positions, int index)
    {
        var order = new List<int>(); for (int i = 0; i < positions.Count; i++) order.Add(i);
        order.Sort((a, b) => positions[a].y != positions[b].y ? positions[b].y.CompareTo(positions[a].y) : positions[a].x.CompareTo(positions[b].x));
        int k = order.IndexOf(index);
        int mate = k % 2 == 0 ? k + 1 : k - 1;
        return mate >= 0 && mate < order.Count ? order[mate] : -1;
    }

    private Vent Mate()
    {
        var pos = new List<Vector2>(all.Count);
        foreach (var v in all) pos.Add(v.transform.position);
        int m = PairOf(pos, all.IndexOf(this));
        return m >= 0 ? all[m] : null;
    }

    private void Update()
    {
        if (sharedCooldown > 0f && all.Count > 0 && all[0] == this) sharedCooldown -= Time.deltaTime;
        if (loading == null) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        var mate = Mate();
        var who = loading; loading = null;
        if (mate == null || who == null) return;
        who.transform.position = (Vector2)mate.transform.position + Vector2.up * 0.1f;
        var rb = who.GetComponent<Rigidbody2D>(); if (rb != null) rb.velocity = Vector2.zero;
        sharedCooldown = cooldown;
        Clanged?.Invoke(mate.transform.position);
        Step1Hint.Show(Step1Text.VentOut, 1f);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (loading != null || sharedCooldown > 0f || other == null) return;
        var t = other.GetComponentInParent<TricksterController>();
        if (t == null) return;
        // S198：↓ = 管口下面、← / → = 管口在墙里侧面（贴着管口朝管口方向按）——**只要朝管口按**就进
        if (!EnterPressed(wallLeft, wallRight)) return;
        if (Mate() == null) { Step1Hint.Show(Step1Text.VentNoMate); return; }
        if (t.IsDisguised) t.OnDisguisePressed();
        loading = t; timer = enterSeconds;
        Clanged?.Invoke(transform.position);
        Step1Hint.Show(Step1Text.VentIn, enterSeconds + 0.2f);
    }

    /// <summary>
    /// 纯逻辑：进管方向（S198）。站在管口上：↓ 永远能进；管口左边/右边紧贴墙时，**朝墙按 ← / →** 也能进（墙上的通风口）。
    /// 走路路过地上的管口按 ← / → 不会误进（那一侧没有墙）。
    /// </summary>
    public static bool WantsEnter(bool down, bool left, bool right, bool wallLeft, bool wallRight) =>
        down || (left && wallLeft) || (right && wallRight);

    private static bool EnterPressed(bool wallLeft, bool wallRight)
    {
        bool d = false, l = false, r = false;
        try
        {
            d = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            l = Input.GetKey(KeyCode.LeftArrow); r = Input.GetKey(KeyCode.RightArrow);
        }
        catch (System.InvalidOperationException) { }
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            d |= kb.downArrowKey.isPressed || kb.sKey.isPressed;
            l |= kb.leftArrowKey.isPressed; r |= kb.rightArrowKey.isPressed;
        }
        return WantsEnter(d, l, r, wallLeft, wallRight);
    }

    public override void OnLevelReset() { loading = null; sharedCooldown = 0f; }
}
