using UnityEngine;

/// <summary>
/// S187：恶作剧大炮（ASCII 'K'）。玩家专属机关，有两种用法——
///   1. 开炮：有炮弹时，伪装在旁边 → **←→ 调炮口左右、↑↓ 调仰角**（金色炮管显示指向，H3）→ 按 L → 预警 → 朝炮管方向打出一发。
///      每门炮的炮弹数可配置（RushMarioTuning.cannonShotsPerRound，默认 1）。
///   2. 人肉炮弹：炮弹打完后，**捣蛋者或马里奥**都能钻进炮口 → 装填 0.6 秒（捣蛋者可按方向键调角度）→ 把自己打出去；
///      冷却 cannonLaunchCooldown（默认 30 秒，可配置）。马里奥只在"炮口朝向他的目标且目标够远"时才会钻（AI 决策）。
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
    [Tooltip("初始炮口朝右（false = 朝左）")]
    [SerializeField] private bool facingRight = true;
    [Tooltip("每回合的炮弹数（开炮用，0 = 这门炮只能当人肉炮用）。数据来自 RushMarioTuning.cannonShotsPerRound")]
    [SerializeField] private int shotsPerRound = 1;
    [SerializeField] private float ballSpeed = 12f;
    [SerializeField] private float ballLifetime = 3f;
    [SerializeField] private float ballRadius = 0.3f;
    [SerializeField] private int ballDamage = 1;
    [SerializeField] private float ballKnockback = 7f;
    [SerializeField] private float ballKnockbackUp = 3f;
    [Tooltip("开炮后'激活'阶段多长（秒）")]
    [SerializeField] private float fireActiveSeconds = 0.25f;

    [Header("=== 瞄准（S198）===")]
    [Tooltip("当前瞄准角（度）：0 = 水平朝炮口方向，正数朝上，负数朝下")]
    [Range(-60f, 80f)] [SerializeField] private float aimAngle = 0f;
    [Tooltip("每按一次方向键转多少度")]
    [SerializeField] private float aimStep = 15f;
    [SerializeField] private float aimMin = -45f, aimMax = 75f;

    [Header("=== 人肉炮弹（逃跑，S198：马里奥也能用）===")]
    [Tooltip("站进炮口多久后自动发射")]
    [SerializeField] private float loadSeconds = 0.6f;
    [Tooltip("发射速度（格/秒）")]
    [SerializeField] private float launchSpeed = 18.5f;
    [Tooltip("人肉发射的默认仰角（度）；站在炮里时可以按方向键改")]
    [Range(0f, 80f)] [SerializeField] private float launchAngle = 40f;
    [SerializeField] private float launchStunSeconds = 0.25f;
    [Tooltip("两次人肉发射之间的冷却（秒，默认 30，数据来自 RushMarioTuning.cannonLaunchCooldown）")]
    [SerializeField] private float launchCooldown = 30f;

    private int shotsLeft;
    private float loadTimer = -1f;
    private float launchCooldownTimer;
    private Transform loading;           // 正在装填的人（马里奥或捣蛋者）
    private float loadingAim;
    private BoxCollider2D body;
    private Transform barrel;

    public bool FacingRight => facingRight;
    public int ShotsLeft => shotsLeft;
    public bool HasAmmo => shotsLeft > 0;
    public float AimAngle => aimAngle;
    public float LaunchCooldownRemaining => Mathf.Max(0f, launchCooldownTimer);
    public bool IsLoading => loading != null;
    public bool CanHumanLaunch => !HasAmmo && launchCooldownTimer <= 0f &&
        currentState != PropControlState.Telegraph && currentState != PropControlState.Active;
    public event System.Action<TricksterController> HumanLaunched;
    public static event System.Action<MarioController> MarioLaunched;
    public event System.Action<CannonBall> Fired;
    private static readonly System.Collections.Generic.List<PranksterCannon> all = new System.Collections.Generic.List<PranksterCannon>();
    public static System.Collections.Generic.IReadOnlyList<PranksterCannon> All => all;

    public void Configure(bool faceRight, int shots)
    {
        facingRight = faceRight;
        shotsPerRound = Mathf.Max(0, shots);
        shotsLeft = shotsPerRound;
    }

    public void ResetLaunchCooldown() { launchCooldownTimer = 0f; }

    public void ConfigureLaunch(float cooldown, float load) { launchCooldown = Mathf.Max(0f, cooldown); loadSeconds = Mathf.Max(0.1f, load); }

    protected override void Awake()
    {
        propName = "大炮";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "伪装在旁：←→ 调炮口方向，L 开炮；没炮弹后任何人都能钻进去把自己打出去（冷却 30 秒）";
        base.Awake();
        if (GetComponent<CannonFacesLeft>() != null) facingRight = false;
        body = GetComponent<BoxCollider2D>();
        body.isTrigger = true; // 炮身是触发器：不挡路，可以"钻进去"
        shotsLeft = shotsPerRound;
        barrel = BuildBarrel();
    }

    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); }

    protected override bool ExtraControlCondition() => HasAmmo;

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        shotsLeft = shotsPerRound;
        loadTimer = -1f; loading = null; launchCooldownTimer = 0f; aimAngle = 0f;
    }

    // ── 瞄准 ─────────────────────────────────────────
    /// <summary>纯逻辑：瞄准方向（单位向量）。facingRight 决定左右，angle 决定上下。</summary>
    public static Vector2 AimDirection(bool faceRight, float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        return new Vector2((faceRight ? 1f : -1f) * Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>
    /// 捣蛋者伪装控制这门炮时按方向键：← → 调左右（按反方向 = 调头），↑ ↓ 调仰角。
    /// 纯逻辑版本返回新的 (facingRight, angle)。
    /// </summary>
    public static (bool faceRight, float angle) Aim(bool faceRight, float angle, Vector2 input, float step, float min, float max)
    {
        if (input.x > 0.5f && !faceRight) { faceRight = true; angle = 0f; }
        else if (input.x < -0.5f && faceRight) { faceRight = false; angle = 0f; }
        else if (input.y > 0.5f) angle = Mathf.Min(max, angle + step);
        else if (input.y < -0.5f) angle = Mathf.Max(min, angle - step);
        return (faceRight, angle);
    }

    public void Nudge(Vector2 input)
    {
        var r = Aim(facingRight, aimAngle, input, aimStep, aimMin, aimMax);
        facingRight = r.faceRight; aimAngle = r.angle;
    }

    private Transform BuildBarrel()
    {
        var t = transform.Find("Barrel");
        if (t != null) return t;
        var go = new GameObject("Barrel");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        sr.color = new Color(0.9f, 0.85f, 0.3f);
        sr.sortingOrder = 22;
        go.transform.localScale = new Vector3(0.7f, 0.14f, 1f);
        return go.transform;
    }

    protected override void Update()
    {
        base.Update();
        if (launchCooldownTimer > 0f) launchCooldownTimer -= Time.deltaTime;
        // 炮管朝向（H3：看得见炮口指向哪里）
        if (barrel != null)
        {
            float a = loading != null ? loadingAim : aimAngle;
            Vector2 d = AimDirection(facingRight, a);
            barrel.localPosition = (Vector3)(d * 0.45f);
            barrel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
        if (loading != null)
        {
            // 装填中：里面的人可以按 ↑↓ 调发射角（捣蛋者用键盘；马里奥 AI 取默认角）
            if (loading.GetComponentInParent<TricksterController>() != null)
            {
                if (Step1Keys.Down(KeyCode.UpArrow)) loadingAim = Mathf.Min(aimMax, loadingAim + aimStep);
                if (Step1Keys.Down(KeyCode.DownArrow)) loadingAim = Mathf.Max(0f, loadingAim - aimStep);
                if (Step1Keys.Down(KeyCode.LeftArrow)) facingRight = false;
                if (Step1Keys.Down(KeyCode.RightArrow)) facingRight = true;
            }
            loadTimer -= Time.deltaTime;
            if (loadTimer <= 0f) LaunchLoaded();
        }
    }

    // ── 开炮 ─────────────────────────────────────────
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
        Vector2 dir = AimDirection(facingRight, aimAngle);
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
        col.radius = ballRadius * 0.9f;
        var ball = go.AddComponent<CannonBall>();
        ball.Launch(transform, dir * ballSpeed, ballLifetime, ballDamage, ballKnockback, ballKnockbackUp);
        Fired?.Invoke(ball);
    }

    // ── 人肉炮弹（捣蛋者与马里奥都能用）──────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!CanHumanLaunch || loading != null || other == null) return;
        var figure = other.GetComponentInParent<TricksterController>();
        if (figure != null)
        {
            if (figure.IsDisguised) return;
            BeginLoad(figure.transform);
            return;
        }
        var mario = other.GetComponentInParent<MarioController>();
        if (mario != null && MarioMayUse(mario)) BeginLoad(mario.transform);
    }

    /// <summary>马里奥只在"赶路时这门炮正对着他的目标方向"才会钻（AI 决策，H4：只看自己的目标）。</summary>
    private bool MarioMayUse(MarioController mario)
    {
        var driver = mario.GetComponent<MarioMindDriver>();
        if (driver == null || driver.Tuning == null || !driver.Tuning.marioUsesCannons) return false;
        var goal = driver.CurrentGoal();
        if (!goal.HasValue) return false;
        return WorthLaunching(transform.position, goal.Value, facingRight, launchSpeed, launchAngle);
    }

    /// <summary>纯逻辑：目标在炮口那一侧、且至少 4 格远，才值得把自己打出去。</summary>
    public static bool WorthLaunching(Vector2 cannon, Vector2 goal, bool faceRight, float speed, float angle)
    {
        float dx = goal.x - cannon.x;
        if (faceRight ? dx < 4f : dx > -4f) return false;
        return speed > 0f;
    }

    private void BeginLoad(Transform who)
    {
        loading = who;
        loadTimer = loadSeconds;
        loadingAim = launchAngle;
        Step1Hint.Show(who.GetComponentInParent<TricksterController>() != null ? Step1Text.CannonLoadYou : Step1Text.CannonLoadMario, loadSeconds + 0.2f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (loading != null && other != null && other.transform.IsChildOf(loading) && loadTimer > 0f && loading.GetComponentInParent<TricksterController>() != null)
            loading = null; // 捣蛋者走出去 = 反悔（马里奥进去就不反悔）
    }

    /// <summary>纯计算：人肉炮弹发射速度（供测试）。</summary>
    public static Vector2 LaunchVelocity(bool faceRight, float speed, float angleDeg) => AimDirection(faceRight, angleDeg) * speed;

    private void LaunchLoaded()
    {
        var who = loading;
        loading = null;
        if (who == null) return;
        who.position = (Vector2)transform.position + Vector2.up * 0.2f;
        Vector2 v = LaunchVelocity(facingRight, launchSpeed, loadingAim);
        var figure = who.GetComponentInParent<TricksterController>();
        if (figure != null) { figure.Launch(v, launchStunSeconds); HumanLaunched?.Invoke(figure); }
        else
        {
            var mario = who.GetComponentInParent<MarioController>();
            if (mario != null)
            {
                var rb = mario.GetComponent<Rigidbody2D>();
                if (rb != null) rb.velocity = v;
                mario.ApplyKnockbackStun(launchStunSeconds + 0.35f);
                MarioLaunched?.Invoke(mario);
            }
        }
        launchCooldownTimer = launchCooldown;
    }
}
