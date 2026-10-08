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
    [SerializeField] private float selfSeconds = 3f; // S228：你自己踩到只吊 3 秒（宪法 P4：10 秒干等 = 死区）
    [SerializeField] private float hoistDelay = 0.3f;
    [SerializeField] private float hoistHeight = 1.5f;
    private Transform victim;
    private Rigidbody2D victimBody;
    private float timer;
    private bool armed = true, hoisted;
    private float hoistT;
    private Vector3 hangPos;
    public bool Armed => armed;
    public bool Holding => victim != null;
    public static event System.Action<MarioController> MarioSnared;

    public void Configure(float seconds, float self = 3f) { holdSeconds = Mathf.Max(0.5f, seconds); selfSeconds = Mathf.Max(0.5f, self); }
    private float holdFor;
    /// <summary>S228 纯逻辑：被吊多久（马里奥 = 10 秒；你自己 = 短的那个，不会比马里奥久）。</summary>
    public static float HoldFor(bool isMario, float marioSeconds, float selfSecs) => isMario ? Mathf.Max(0.5f, marioSeconds) : Mathf.Max(0.5f, Mathf.Min(marioSeconds, selfSecs));

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
        timer = hoistDelay; holdFor = HoldFor(mario != null, holdSeconds, selfSeconds);
        hangPos = transform.position + Vector3.up * hoistHeight;
        if (mario != null) { mario.ApplyKnockbackStun(hoistDelay + holdSeconds); MarioSnared?.Invoke(mario); Step1Hint.Show(Step1Text.SnareMario, 2f); }
        else { figure.ApplyKnockbackStun(hoistDelay + holdFor); Step1Hint.Show(Step1Text.SnareYou, 2f); }
    }

    private void FixedUpdate()
    {
        if (victim == null) return;
        timer -= Time.fixedDeltaTime;
        if (!hoisted)
        {
            if (timer > 0f) return;
            hoisted = true; timer = holdFor; hoistT = 0f;
            Step1Fx.Burst(transform.position, 4, new Color(0.8f, 0.65f, 0.4f, 1f), 3f, Vector2.up, 90f, 10f, 0.12f, 0.35f);
        }
        // S235：人已经被"传送"走了（被抓回出生点 / 掉出房间回出生点 / 卡住救援）→ 放人。以前会每帧把他拽回绳套，回出生点等于白回
        if (Step1Bounds.Teleported(victim.position, hangPos, hoistHeight)) { Release(); return; }
        // 吊着：固定在绳套上方（H9：位置是地图里的空气格，不会卡墙——摆放规则要求上方 2 格空）
        // S216：前 0.25 秒缓动拉上去（以前一帧瞬移 1.5 格），之后轻轻晃
        hoistT += Time.fixedDeltaTime;
        float up = Step1Feel.SmoothStep01(hoistT / 0.25f);
        Vector3 from = new Vector3(hangPos.x, hangPos.y - hoistHeight, hangPos.z);
        victim.position = Vector3.Lerp(from, hangPos, up) + Vector3.right * (up >= 1f ? Mathf.Sin(hoistT * 3f) * 0.06f : 0f);
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
