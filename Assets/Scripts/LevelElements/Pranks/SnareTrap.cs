using UnityEngine;

/// <summary>
/// S198：猎人绳套（ASCII 'Y'）—— 踩到就被绳子倒吊起来 snareSeconds 秒（默认 10，可配置），然后掉下来。
/// 两种用法：
///   1. **被动陷阱**：谁踩到都中——马里奥中了 = 你的大好机会；**你自己踩到也会被吊**（伪装着踩到也会被吊并现形）→ 走路要看地面。
///   2. **机关**：捣蛋者伪装在旁边按 L = "上弦"（预警后本回合重新装好 / 或让已经吊着的人提前掉下来）。
/// 规则：
///   - H3：地上有看得见的绳圈（M 图例标"绳套"）；踩中后 0.3 秒才把人吊起（能看清发生了什么）；
///   - H9：吊起期间位置固定在绳套上方 1.5 格（不会卡墙），时间到必然放下；一回合一个绳套只触发一次（上弦后可再用）；
///   - H4：不读取捣蛋者信息；马里奥 AI 被吊时判定为"晕"；
///   - 数值：snareSeconds / snareRearmUses 在 RushMarioTuning。
/// 参考：Dead by Daylight 的捕兽夹（被动放置、踩中定身、双方都要看地面）——只借规则。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SnareTrap : ControllableLevelElement
{
    [SerializeField] private float holdSeconds = 10f;
    [SerializeField] private float hoistDelay = 0.3f;
    [SerializeField] private float hoistHeight = 1.5f;
    private Transform victim;
    private Rigidbody2D victimBody;
    private float timer;
    private bool armed = true, hoisted;
    private Vector3 hangPos;
    public bool Armed => armed;
    public bool Holding => victim != null;
    public static event System.Action<MarioController> MarioSnared;

    public void Configure(float seconds) { holdSeconds = Mathf.Max(0.5f, seconds); }

    protected override void Awake()
    {
        propName = "绳套";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "踩到被倒吊 10 秒；捣蛋者按 L 重新装好或放人";
        base.Awake();
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    // L：上弦（没装好时重新装好）；正吊着人时 = 提前放下
    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }
    protected override void OnActivate(Vector2 direction)
    {
        if (victim != null) { Release(); return; }
        armed = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed || victim != null || other == null || other.attachedRigidbody == null) return;
        var mario = other.GetComponentInParent<MarioController>();
        var figure = mario == null ? other.GetComponentInParent<TricksterController>() : null;
        if (mario == null && figure == null) return;
        if (figure != null && figure.IsDisguised) figure.OnDisguisePressed(); // 伪装着踩到也会现形
        victim = (mario != null ? mario.transform : figure.transform);
        victimBody = other.attachedRigidbody;
        armed = false; hoisted = false;
        timer = hoistDelay;
        hangPos = transform.position + Vector3.up * hoistHeight;
        if (mario != null) { mario.ApplyKnockbackStun(hoistDelay + holdSeconds); MarioSnared?.Invoke(mario); Step1Hint.Show(Step1Text.SnareMario, 2f); }
        else { figure.ApplyKnockbackStun(hoistDelay + holdSeconds); Step1Hint.Show(Step1Text.SnareYou, 2f); }
    }

    private void FixedUpdate()
    {
        if (victim == null) return;
        timer -= Time.fixedDeltaTime;
        if (!hoisted)
        {
            if (timer > 0f) return;
            hoisted = true; timer = holdSeconds;
        }
        // 吊着：固定在绳套上方（H9：位置是地图里的空气格，不会卡墙——摆放规则要求上方 2 格空）
        victim.position = hangPos;
        if (victimBody != null) victimBody.velocity = Vector2.zero;
        var m = victim.GetComponent<MarioController>(); if (m != null) m.ApplyKnockbackStun(Mathf.Max(0.1f, timer));
        var f = victim.GetComponent<TricksterController>(); if (f != null) f.ApplyKnockbackStun(Mathf.Max(0.1f, timer));
        if (timer <= 0f) Release();
    }

    private void Release()
    {
        victim = null; victimBody = null; hoisted = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        Release(); armed = true;
    }

    /// <summary>纯逻辑：总共被控多久（踩中到落地前）。</summary>
    public static float TotalHold(float delay, float seconds) => Mathf.Max(0f, delay) + Mathf.Max(0f, seconds);
}
