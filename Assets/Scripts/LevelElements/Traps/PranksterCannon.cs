using UnityEngine;

/// <summary>
/// S187：恶作剧大炮（ASCII 'K'）。玩家专属机关，有两种用法——
///   1. 开炮：有炮弹时，伪装在旁边按 L → 预警（炮口闪烁、抖动）→ 朝炮口方向打出一发炮弹，打中马里奥扣 1 血并击退。
///      默认每回合 1 发（shotsPerRound，数据可调）。
///   2. 人肉炮弹（逃跑 / 秀操作）：炮弹打完后，捣蛋者（不伪装）走到炮口里 → 短暂装填 → 朝炮口方向斜上方把自己打出去。
///      打出去时会暴露（马里奥看得见就是看得见，H4 不变）；不需要能量，冷却后可再用。
/// 设计参考：Donkey Kong Country 的 Barrel Cannon（钻进去、朝箭头方向发射、可用于跨越/逃跑，
///   来源 https://www.mariowiki.com/Barrel_Cannon）与 Bill Blaster（固定炮台、朝一侧直线射击，
///   来源 https://www.mariowiki.com/Bill_Blaster）。只借"规则"，不借素材与代码。
/// 规则约束：
///   - H3：开炮前有预警（telegraphDuration），炮弹判定只在飞行中；
///   - H4：炮不给马里奥任何关于捣蛋者的信息；马里奥只能用眼睛看见炮弹/飞出去的人；
///   - H9：炮弹有寿命、撞墙即碎；发射后捣蛋者控制权立即回来；
///   - 所有数值在 Inspector / 调参数据里，炮口朝向由 facingRight 决定（模板旁边可用 Override 或构建器设置）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PranksterCannon : ControllableLevelElement
{
    [Header("=== 炮 ===")]
    [Tooltip("炮口朝右（false = 朝左）")]
    [SerializeField] private bool facingRight = true;
    [Tooltip("每回合的炮弹数（开炮用）。打完后可当人肉炮弹逃跑")]
    [SerializeField] private int shotsPerRound = 1;
    [SerializeField] private float ballSpeed = 12f;
    [SerializeField] private float ballLifetime = 3f;
    [SerializeField] private float ballRadius = 0.3f;
    [SerializeField] private int ballDamage = 1;
    [SerializeField] private float ballKnockback = 7f;
    [SerializeField] private float ballKnockbackUp = 3f;
    [Tooltip("开炮后'激活'阶段多长（秒）。很短：炮弹打出去就结束，马里奥不会在炮前傻等")]
    [SerializeField] private float fireActiveSeconds = 0.25f;

    [Header("=== 人肉炮弹（逃跑）===")]
    [Tooltip("炮弹打完后，捣蛋者站进炮口多久后自动发射（给玩家反悔/对准的时间）")]
    [SerializeField] private float loadSeconds = 0.35f;
    [Tooltip("发射速度（格/秒）")]
    [SerializeField] private float launchSpeed = 18.5f;
    [Tooltip("发射仰角（度，0 = 水平）")]
    [Range(0f, 80f)] [SerializeField] private float launchAngle = 40f;
    [Tooltip("发射后空中不受控的时间（秒）。这段时间内保持发射速度，之后正常重力与控制（约飞 8–9 格远、4 格高）")]
    [SerializeField] private float launchStunSeconds = 0.25f;
    [Tooltip("两次人肉发射之间的冷却")]
    [SerializeField] private float launchCooldown = 2.5f;

    private int shotsLeft;
    private float loadTimer = -1f;
    private float launchCooldownTimer;
    private TricksterController loadingFigure;
    private BoxCollider2D body;

    public bool FacingRight => facingRight;
    public int ShotsLeft => shotsLeft;
    public bool HasAmmo => shotsLeft > 0;
    public bool CanHumanLaunch => !HasAmmo && launchCooldownTimer <= 0f &&
        currentState != PropControlState.Telegraph && currentState != PropControlState.Active;
    public event System.Action<TricksterController> HumanLaunched;
    public event System.Action<CannonBall> Fired;

    public void Configure(bool faceRight, int shots)
    {
        facingRight = faceRight;
        shotsPerRound = Mathf.Max(0, shots);
        shotsLeft = shotsPerRound;
    }

    protected override void Awake()
    {
        propName = "大炮";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "有炮弹时开炮打马里奥；打完后可钻进去把自己打出去逃跑";
        base.Awake();
        if (GetComponent<CannonFacesLeft>() != null) facingRight = false;
        body = GetComponent<BoxCollider2D>();
        body.isTrigger = true; // 炮身是触发器：不挡路，捣蛋者可以"钻进去"
        shotsLeft = shotsPerRound;
    }

    protected override bool ExtraControlCondition() => HasAmmo;

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        shotsLeft = shotsPerRound;
        loadTimer = -1f; loadingFigure = null; launchCooldownTimer = 0f;
    }

    protected override void Update()
    {
        base.Update();
        if (launchCooldownTimer > 0f) launchCooldownTimer -= Time.deltaTime;
        if (loadingFigure != null)
        {
            loadTimer -= Time.deltaTime;
            if (loadTimer <= 0f) LaunchFigure();
        }
    }

    // ── 开炮（五段生命周期）──────────────────────────────
    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        if (shotsLeft <= 0) return;
        shotsLeft--;
        Fire();
        stateTimer = Mathf.Min(stateTimer, fireActiveSeconds);
    }

    protected override void OnActiveEnd() { }

    private void Fire()
    {
        Vector2 dir = facingRight ? Vector2.right : Vector2.left;
        var go = new GameObject("CannonBall");
        go.transform.position = (Vector2)transform.position + dir * 0.7f;
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        sr.color = new Color(0.12f, 0.12f, 0.12f);
        sr.sortingOrder = 20;
        visual.transform.localScale = Vector3.one * ballRadius * 2f;
        go.AddComponent<Rigidbody2D>();
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = ballRadius * 0.9f; // 判定略小于视觉（H3）
        var ball = go.AddComponent<CannonBall>();
        ball.Launch(transform, dir * ballSpeed, ballLifetime, ballDamage, ballKnockback, ballKnockbackUp);
        Fired?.Invoke(ball);
    }

    // ── 人肉炮弹 ────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!CanHumanLaunch || loadingFigure != null) return;
        var figure = other.GetComponentInParent<TricksterController>();
        if (figure == null || figure.IsDisguised) return;
        loadingFigure = figure;
        loadTimer = loadSeconds;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (loadingFigure != null && other.GetComponentInParent<TricksterController>() == loadingFigure && loadTimer > 0f)
            loadingFigure = null; // 走出去 = 反悔
    }

    /// <summary>纯计算：人肉炮弹发射速度（供测试）。</summary>
    public static Vector2 LaunchVelocity(bool faceRight, float speed, float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return new Vector2((faceRight ? 1f : -1f) * Mathf.Cos(a), Mathf.Sin(a)) * speed;
    }

    private void LaunchFigure()
    {
        var figure = loadingFigure;
        loadingFigure = null;
        if (figure == null) return;
        figure.transform.position = (Vector2)transform.position + Vector2.up * 0.2f;
        figure.Launch(LaunchVelocity(facingRight, launchSpeed, launchAngle), launchStunSeconds);
        launchCooldownTimer = launchCooldown;
        HumanLaunched?.Invoke(figure);
    }
}
