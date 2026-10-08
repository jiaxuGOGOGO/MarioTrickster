using UnityEngine;

/// <summary>
/// S187：恶作剧大炮（ASCII 'K'）。玩家专属机关，有两种用法——
///   1. 开炮：有炮弹时，伪装在旁边 → **←→ 调炮口左右、↑↓ 调仰角**（金色炮管显示指向，H3）→ 按 L → 预警 → 朝炮管方向打出一发。
///      每门炮的炮弹数可配置（RushMarioTuning.cannonShotsPerRound，默认 1）。
///   2. 人肉炮弹：
///      - 捣蛋者（S240）：没伪装时站在炮口按 **↓ 坐进去**（有没有炮弹都行）→ 身体锁在炮里，**←→ 调方向、↑↓ 调仰角**，
///        虚线显示会飞到哪 → **空格（或 L）发射**；坐太久（tricksterCannonMaxSitSeconds）自动发射。
///        速度 tricksterCannonSpeed（默认 26 ≈ 40° 飞 15 格），落地 tricksterCannonCooldown（默认 1.5 秒）后就能再进去 = 反复进。
///      - 马里奥：炮弹打完后钻进炮口 → 装填 0.6 秒 → 打出去；冷却 cannonLaunchCooldown（默认 30 秒）。只在"炮口朝向他的目标且目标够远"时才会钻（AI 决策）。
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

    [Header("=== 坐进大炮（S240：捣蛋者自己按空格发射）===")]
    [Tooltip("捣蛋者发射速度（格/秒）；运行时以调参 tricksterCannonSpeed 为准")]
    [SerializeField] private float seatSpeed = 26f;
    [Tooltip("捣蛋者飞出去后多少秒能再坐进来；运行时以调参 tricksterCannonCooldown 为准")]
    [SerializeField] private float seatCooldown = 1.5f;
    [Tooltip("坐着最多几秒就自动发射；运行时以调参 tricksterCannonMaxSitSeconds 为准")]
    [SerializeField] private float seatMaxSeconds = 8f;
    [Tooltip("坐着时按住 ↑↓ 每秒转多少度")]
    [SerializeField] private float seatAimSpeed = 90f;
    public const float SeatAimMin = -15f, SeatAimMax = 80f;

    private TricksterController seated;
    private float seatTimer, seatCooldownTimer, seatAim = 40f, nearAt = -10f;
    private TricksterController near;
    private LineRenderer preview;
    /// <summary>S240：捣蛋者正坐在哪门炮里（null = 没坐）。其他按键（B/G/T/Z/P/L/跳）坐着时都不生效。</summary>
    public static PranksterCannon SeatedIn { get; private set; }
    public static bool TricksterSeated => SeatedIn != null;
    public bool IsSeated => seated != null;
    public float SeatAim => seatAim;
    public float SeatCooldownRemaining => Mathf.Max(0f, seatCooldownTimer);
    public float SeatSecondsLeft => seated != null ? Mathf.Max(0f, seatTimer) : 0f;
    public float SeatSpeed => seatSpeed;
    public void ConfigureSeat(float speed, float cooldown, float maxSit) { seatSpeed = Mathf.Max(1f, speed); seatCooldown = Mathf.Max(0f, cooldown); seatMaxSeconds = Mathf.Max(1f, maxSit); }

    private int shotsLeft;
    private float recoilAt = -10f;
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
        elementDescription = "伪装在旁：←→ 调炮口方向，L 开炮；没伪装时站炮口按 ↓ 坐进去，方向键瞄准，空格把自己打出去（可以反复进）";
        base.Awake();
        if (GetComponent<CannonFacesLeft>() != null) facingRight = false;
        body = GetComponent<BoxCollider2D>();
        body.isTrigger = true; // 炮身是触发器：不挡路，可以"钻进去"
        shotsLeft = shotsPerRound;
        barrel = BuildBarrel();
    }

    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); Unseat(); }

    private void Start()
    {
        var t = MarioMindTuningSO.LoadOrDefault(); // S240：调参改了马上生效（不用重建房间）
        if (t != null) ConfigureSeat(t.tricksterCannonSpeed, t.tricksterCannonCooldown, t.tricksterCannonMaxSitSeconds);
    }

    protected override bool ExtraControlCondition() => HasAmmo;
    protected override bool GreyWhenSpent => false; // S240：炮弹打完还能坐进去飞，不变灰

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        shotsLeft = shotsPerRound;
        loadTimer = -1f; loading = null; launchCooldownTimer = 0f; aimAngle = 0f;
        Unseat(); seatCooldownTimer = 0f; seatAim = launchAngle;
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
        if (seatCooldownTimer > 0f) seatCooldownTimer -= Time.deltaTime;
        UpdateSeat();
        // 炮管朝向（H3：看得见炮口指向哪里）
        if (barrel != null)
        {
            float a = seated != null ? seatAim : loading != null ? loadingAim : aimAngle;
            Vector2 d = AimDirection(facingRight, a);
            float rk = Time.time - recoilAt; float recoil = rk < 0.25f ? 0.2f * (1f - rk / 0.25f) : 0f; // S216 后坐
            barrel.localPosition = (Vector3)(d * (0.45f - recoil));
            barrel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
        if (loading != null)
        {
            // 装填中（只剩马里奥会走这条：AI 取默认角）
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
        // S216：炮口火光 + 烟 + 炮管后坐（画面）
        Step1Fx.Ring((Vector2)transform.position + dir * 0.8f, 0.6f, new Color(1f, 0.75f, 0.3f, 1f));
        Step1Fx.Burst((Vector2)transform.position + dir * 0.8f, 5, new Color(0.8f, 0.8f, 0.8f, 0.8f), 3f, dir, 60f, -2f, 0.2f, 0.45f);
        recoilAt = Time.time;
    }

    // ── 人肉炮弹 ─────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == null) return;
        var figure = other.GetComponentInParent<TricksterController>();
        if (figure != null) { near = figure; nearAt = Time.time; return; } // S240：捣蛋者不再碰到就被吸进去——要自己按 ↓
        if (!CanHumanLaunch || loading != null || seated != null) return;
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
        Step1Hint.Show(Step1Text.CannonLoadMario, loadSeconds + 0.2f);
    }

    /// <summary>S240 纯逻辑：捣蛋者现在能不能坐进这门炮（没伪装、没缩小、冷却好了、炮没在开火、没人在里面）。</summary>
    public static bool CanSeat(bool disguised, bool shrunk, float seatCooldownLeft, bool firing, bool occupied) =>
        !disguised && !shrunk && seatCooldownLeft <= 0f && !firing && !occupied;

    /// <summary>站在这门炮的炮口附近（最近 0.15 秒内碰到过触发器）。底栏用它显示"↓ 进炮"。</summary>
    public bool FigureAtMouth(TricksterController f) => f != null && near == f && Time.time - nearAt < 0.15f;
    public static PranksterCannon MouthOf(TricksterController f)
    {
        foreach (var c in all) if (c != null && c.FigureAtMouth(f)) return c;
        return null;
    }
    public bool SeatReadyFor(TricksterController f)
    {
        var kit = TricksterKit.Instance;
        return f != null && CanSeat(f.IsDisguised, kit != null && kit.Shrunk, seatCooldownTimer,
            currentState == PropControlState.Telegraph || currentState == PropControlState.Active, seated != null || loading != null || SeatedIn != null);
    }

    /// <summary>S240 纯逻辑：坐着时的瞄准。←→ 按下 = 调头；↑↓ 按住 = 连续转（每秒 aimSpeed 度），夹在 [min,max]。</summary>
    public static (bool faceRight, float angle) SeatAimStep(bool faceRight, float angle, bool leftDown, bool rightDown, float vertical, float aimSpeed, float dt, float min, float max)
    {
        if (leftDown) faceRight = false;
        if (rightDown) faceRight = true;
        angle = Mathf.Clamp(angle + vertical * aimSpeed * dt, min, max);
        return (faceRight, angle);
    }

    /// <summary>S240 纯逻辑：轨迹预览点（与真飞行同一套 Step1Feel.StunStep：重力 + 空中阻力）。从炮口出发，最多 seconds 秒。</summary>
    public static System.Collections.Generic.List<Vector2> PreviewArc(Vector2 origin, Vector2 v, float gravity, float maxFall, float airDrag, float seconds = 1.6f, float dt = 0.04f, float floorDrop = 8f)
    {
        var pts = new System.Collections.Generic.List<Vector2> { origin };
        Vector2 p = origin;
        for (float t = 0f; t < seconds; t += dt)
        {
            v = Step1Feel.StunStep(v, false, dt, gravity, maxFall, airDrag, 0f, false);
            p += v * dt;
            pts.Add(p);
            if (p.y < origin.y - floorDrop) break;
        }
        return pts;
    }

    private void UpdateSeat()
    {
        // 进炮：站在炮口、没伪装，按 ↓
        if (seated == null && near != null && FigureAtMouth(near) && Step1Keys.Down(KeyCode.DownArrow))
        {
            if (SeatReadyFor(near)) Seat(near);
            else if (seatCooldownTimer > 0f) Step1Hint.Show(string.Format(Step1Text.CannonSeatCooldown, seatCooldownTimer), 0.8f);
            else if (near.IsDisguised) Step1Hint.Show(Step1Text.CannonSeatDisguised, 1f);
        }
        if (seated == null) { if (preview != null) preview.enabled = false; return; }
        // 被抓 / 回合重置把人传送走了 → 放开，不拽回来发射
        if (Step1Bounds.Teleported(seated.transform.position, transform.position, 1.5f)) { Unseat(); return; }
        seated.transform.position = SeatPos();
        var r = SeatAimStep(facingRight, seatAim, Step1Keys.Down(KeyCode.LeftArrow), Step1Keys.Down(KeyCode.RightArrow),
            (Step1Keys.Held(KeyCode.UpArrow) ? 1f : 0f) - (Step1Keys.Held(KeyCode.DownArrow) && Time.time - seatedAt > 0.25f ? 1f : 0f),
            seatAimSpeed, Time.deltaTime, SeatAimMin, SeatAimMax);
        facingRight = r.faceRight; seatAim = r.angle;
        DrawPreview();
        seatTimer -= Time.deltaTime;
        if (Step1Keys.Down(KeyCode.Space) || Step1Keys.Down(KeyCode.L) || seatTimer <= 0f) FireSeated();
    }

    private float seatedAt;
    private Vector2 SeatPos() => (Vector2)transform.position + Vector2.up * 0.2f;

    private void Seat(TricksterController f)
    {
        seated = f; SeatedIn = this; seatTimer = seatMaxSeconds; seatedAt = Time.time;
        if (seatAim < 5f) seatAim = launchAngle;
        f.EnterSeat(SeatPos());
        Step1Hint.Show(Step1Text.CannonLoadYou, 2.5f);
        Step1Fx.Ring(transform.position, 0.5f, new Color(1f, 0.85f, 0.4f, 1f));
    }

    private void Unseat()
    {
        if (seated != null) seated.ExitSeat();
        seated = null;
        if (SeatedIn == this) SeatedIn = null;
        if (preview != null) preview.enabled = false;
    }

    private void FireSeated()
    {
        var f = seated;
        Unseat();
        if (f == null) return;
        f.transform.position = SeatPos();
        Vector2 v = LaunchVelocity(facingRight, seatSpeed, seatAim);
        f.Launch(v, launchStunSeconds);
        HumanLaunched?.Invoke(f);
        seatCooldownTimer = seatCooldown;
        Step1Fx.Ring((Vector2)transform.position + v.normalized * 0.8f, 0.9f, new Color(1f, 0.75f, 0.3f, 1f));
        Step1Fx.Burst((Vector2)transform.position + v.normalized * 0.8f, 8, new Color(0.8f, 0.8f, 0.8f, 0.8f), 3f, v, 60f, -2f, 0.2f, 0.45f);
        recoilAt = Time.time;
    }

    private static readonly RaycastHit2D[] s_arcHits = new RaycastHit2D[6];
    private void DrawPreview()
    {
        if (preview == null)
        {
            var go = new GameObject("SeatPreview");
            go.transform.SetParent(transform, false);
            preview = go.AddComponent<LineRenderer>();
            preview.useWorldSpace = true; preview.widthMultiplier = 0.08f; preview.numCapVertices = 2;
            preview.material = new Material(Shader.Find("Sprites/Default"));
            preview.textureMode = LineTextureMode.Tile;
            preview.sortingOrder = 40;
            preview.startColor = new Color(1f, 0.9f, 0.4f, 0.95f); preview.endColor = new Color(1f, 0.9f, 0.4f, 0.15f);
        }
        preview.enabled = true;
        Vector2 o = SeatPos() + AimDirection(facingRight, seatAim) * 0.6f;
        var pts = PreviewArc(o, LaunchVelocity(facingRight, seatSpeed, seatAim), LaunchFeel.gravity, 40f, LaunchFeel.airDrag);
        int n = pts.Count;
        for (int i = 1; i < pts.Count; i++)
        {
            int hits = Physics2D.LinecastNonAlloc(pts[i - 1], pts[i], s_arcHits);
            bool wall = false;
            for (int k = 0; k < hits; k++) { var c = s_arcHits[k].collider; if (c != null && !c.isTrigger && !SightLine.IsOneWayPlatform(c) && c.GetComponentInParent<TricksterController>() == null && c.GetComponentInParent<MarioController>() == null) { wall = true; pts[i] = s_arcHits[k].point; break; } }
            if (wall) { n = i + 1; break; }
        }
        // 轨迹线：撞到墙就停在墙上（H3：看得见会落在哪）
        preview.positionCount = n;
        for (int i = 0; i < n; i++) preview.SetPosition(i, pts[i]);
    }

    /// <summary>纯计算：人肉炮弹发射速度（供测试）。</summary>
    public static Vector2 LaunchVelocity(bool faceRight, float speed, float angleDeg) => AimDirection(faceRight, angleDeg) * speed;

    private void LaunchLoaded()
    {
        var who = loading;
        loading = null;
        if (who == null) return;
        if (Step1Bounds.Teleported(who.position, transform.position, 1f)) return; // S235：装填时被传送走了（被抓 / 掉出房间回出生点）→ 不再把人拽回炮口发射
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
                mario.ApplyKnockbackStun(launchStunSeconds + 0.35f, true, false); // S216：飞到落地为止
                MarioLaunched?.Invoke(mario);
            }
        }
        launchCooldownTimer = launchCooldown;
        Step1Fx.Ring((Vector2)transform.position + v.normalized * 0.8f, 0.8f, new Color(1f, 0.75f, 0.3f, 1f));
        Step1Fx.Burst((Vector2)transform.position + v.normalized * 0.8f, 6, new Color(0.8f, 0.8f, 0.8f, 0.8f), 3f, v, 60f, -2f, 0.2f, 0.45f);
        recoilAt = Time.time;
    }
}
