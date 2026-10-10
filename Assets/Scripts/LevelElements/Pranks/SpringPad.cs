using UnityEngine;

/// <summary>
/// S193：弹簧板（ASCII 'J'）—— 玩家机关，"浮空追击"的起手（格斗游戏的 launcher）。
/// 平时就是一块普通地面（实心、可站、安全）。捣蛋者伪装在旁按 L → 预警（闪黄+抖）→ 把站在板上的马里奥弹上天。
/// 马里奥在空中无法控制（knockback stun），落地点可预判 → 玩家可以在落点提前摆好火 / 塌桥，形成"浮空 → 追击"连招。
/// 规则约束：
///   - H3：有预警；只弹"正站在板上"的人，判定框 = 板面，不大于精灵；
///   - H4：不读取捣蛋者信息；不伤人，只改速度；
///   - H9：弹射后必然落回地图内（弹射速度与房间高度由测试校验），不会卡进天花板；
///   - 数值全部在 Inspector / 调参数据中。
/// 参考：Super Mario 的 Trampoline / Note Block（只借"踩上去被弹起"的规则，不借素材）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SpringPad : ControllableLevelElement
{
    [Header("=== 弹簧 ===")]
    [Tooltip("弹起速度（格/秒）。S216：被弹飞全程有重力（LaunchFeel.gravity=40），15 ≈ 弹高 2.8 格")]
    [SerializeField] private float launchSpeed = 15f;
    [Tooltip("水平推送（格/秒，正数 = 朝马里奥当前朝向）")]
    [SerializeField] private float forwardPush = 2.5f;
    [Tooltip("空中不受控的时间（秒）")]
    [SerializeField] private float airStunSeconds = 0.6f;
    [Tooltip("板面上方多高范围内算'站在板上'")]
    [SerializeField] private float detectHeight = 0.6f;

    private BoxCollider2D body;
    private static readonly Collider2D[] s_hits = new Collider2D[8];
    public event System.Action<MarioController> Launched;

    private bool firedThisActivation;

    /// <summary>构建器用：数值来自调参资产。预警短（仍可见，H3），激活窗口内踩上去的都会被弹。</summary>
    public void Configure(float speed, float push, float stun, float telegraph, float activeWindow)
    {
        launchSpeed = speed; forwardPush = push; airStunSeconds = stun;
        telegraphDuration = Mathf.Max(0.1f, telegraph); activeDuration = Mathf.Max(0.1f, activeWindow);
    }

    protected override void Awake()
    {
        propName = "弹簧板";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Interactive | ElementTag.Resettable | ElementTag.AffectsPhysics;
        elementDescription = "捣蛋者按 L 把站在上面的马里奥弹上天（浮空追击起手）";
        base.Awake();
        body = GetComponent<BoxCollider2D>();
    }

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        firedThisActivation = false;
        TryLaunch();
    }

    private float bounceAt = -10f;
    private Transform padVisual;
    private Vector3 padHome;

    protected override void Update()
    {
        base.Update();
        // S216：板子自己"压下 → 弹出 → 回弹"（只动画面子节点，不动碰撞体）
        if (padVisual == null) { padVisual = transform.Find("Visual"); if (padVisual != null) padHome = padVisual.localScale; }
        if (padVisual != null)
        {
            float t = Time.time - bounceAt;
            float sy = t < 1f ? Step1Feel.SpringPadScaleY(t) : 1f;
            padVisual.localScale = new Vector3(padHome.x * (1f + (1f - sy) * 0.3f), padHome.y * sy, padHome.z);
        }
        if (currentState == PropControlState.Active && !firedThisActivation) TryLaunch();
    }

    private void TryLaunch()
    {
        Bounds b = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 center = new Vector2(b.center.x, b.max.y + detectHeight * 0.5f);
        Vector2 size = new Vector2(b.size.x * 0.9f, detectHeight);
        int n = Physics2D.OverlapBoxNonAlloc(center, size, 0f, s_hits);
        for (int i = 0; i < n; i++)
        {
            var mario = s_hits[i] != null ? s_hits[i].GetComponentInParent<MarioController>() : null;
            if (mario == null) continue;
            var rb = mario.GetComponent<Rigidbody2D>();
            if (rb == null) continue;
            rb.velocity = LaunchVelocity(mario.IsFacingRight, launchSpeed, forwardPush);
            mario.ApplyKnockbackStun(airStunSeconds, true, false); // S216：落地前都不能动（空中轨迹 = 抛物线，落点可预判）
            bounceAt = Time.time;
            Step1Fx.Dust(new Vector2(b.center.x, b.max.y), 1.3f);
            Step1Fx.Ring(new Vector2(b.center.x, b.max.y), 0.9f, new Color(0.45f, 1f, 0.6f, 1f));
            firedThisActivation = true;
            Launched?.Invoke(mario);
            SpringPadEvents.RaiseLaunched();
            break;
        }
    }

    /// <summary>纯计算：弹射速度（供测试）。</summary>
    public static Vector2 LaunchVelocity(bool facingRight, float speed, float push) =>
        new Vector2((facingRight ? 1f : -1f) * push, Mathf.Max(0f, speed));

    /// <summary>纯计算：弹射最高点（格），g 为重力加速度（正数）。S216 起被弹飞全程受 LaunchFeel.gravity 影响。</summary>
    public static float ApexHeight(float speed, float g) => g <= 0f ? 0f : speed * speed / (2f * g);
}
