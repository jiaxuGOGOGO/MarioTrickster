using UnityEngine;

/// <summary>
/// S241：K 蛛丝（用户："像蜘蛛侠一样喷射线挂在天花板上荡着走；黑夜可以伸长缩短调整线荡过马里奥头顶，前提是头顶没光，有光会被察觉"）。
///   · 现形时按 K：朝面向的斜上方射丝（silkRange 格内找天花板，单向台面不算），挂上就开始荡。
///   · ←→ 打秋千，↑↓ 收放线（收线 = 越荡越快：角动量守恒 v' = v·L/L'），K 松手（保留速度飞出去）。
///   · 最多 silkMaxSeconds 秒自动松手；被打 / 晕 = 松手。挂着时不能触发机关、不能伪装、不能跳。
///   · 被发现的规则在马里奥眼睛里（MarioEyes：你在他头顶 overheadNoticeRadius 内、而且那里亮 → 他察觉）。
/// 物理：单摆纯函数 Step1Stealth.Step（沙盒可测）；这里每个物理帧把摆的速度写进刚体，碰撞照常（撞墙会停）。
/// 参考：Spider-Man（2018）摆荡——收线加速、松手保留动量；Bionic Commando 的钩爪。只借规则不借素材。
/// 由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class TricksterSilk : MonoBehaviour
{
    public static bool Swinging => Instance != null && Instance.swinging;
    public static TricksterSilk Instance { get; private set; }

    private MarioMindTuningSO tuning;
    private TricksterController self;
    private Rigidbody2D rb;
    private string[] grid;
    private GameManager manager;
    private LineRenderer line; private SpriteRenderer hook; // S245：钩爪（摆荡锚点）像素图
    private bool swinging;
    private Vector2 anchor;
    private Step1Stealth.Swing s;
    private float held, cooldown;

    public float Cooldown => cooldown;

    private void Awake() { Instance = this; }

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        rb = GetComponent<Rigidbody2D>();
        var src = Step1PrankRoomBuilderBridge.CurrentRoom; // 复制一份再去槽位（别改共享的房间表）
        grid = src != null ? new string[src.Length] : null;
        if (grid != null) for (int i = 0; i < grid.Length; i++) grid[i] = Step1Layout.StripSlots(src[i]);
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        var go = new GameObject("S241_SilkLine"); // 独立物体：线是公开画面，但不挂在你身上（不会被"你的渲染器"判定带走）
        line = go.AddComponent<LineRenderer>();
        line.positionCount = 2; line.startWidth = line.endWidth = 0.06f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(0.92f, 0.95f, 1f, 0.9f);
        line.sortingOrder = 20; line.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (manager != null) manager.OnRoundStart -= ResetRound;
        if (line != null) Destroy(line.gameObject);
        if (hook != null) Destroy(hook.gameObject);
    }

    private void ResetRound() { Release(false); cooldown = 0f; }

    public bool CanSilkNow => self != null && Step1Stealth.CanSilk(self.IsDisguised, PranksterCannon.TricksterSeated, TricksterBurrow.Instance != null && TricksterBurrow.Instance.Burrowed, cooldown) && !TricksterKit.BlocksPranks;

    private void Update()
    {
        if (self == null || tuning == null) return;
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (swinging)
        {
            held += Time.deltaTime;
            if (held >= tuning.silkMaxSeconds || self.IsStunned || self.IsDisguised) Release(false);
            if (line != null) { line.SetPosition(0, anchor); line.SetPosition(1, transform.position); }
        }
        bool inputOk = !Step1HandsOffCheck.IsRunning && !Step1PlaytestLog.IsTyping && !Step1Screen.HelpOpen && Time.timeScale > 0f;
        if (!inputOk || !Step1Keys.Down(KeyCode.K)) return;
        if (swinging) { Release(true); return; }
        TryShoot();
    }

    private void TryShoot()
    {
        if (!CanSilkNow)
        {
            if (cooldown > 0f) Step1Hint.Show(string.Format(Step1Text.SilkCooldown, cooldown));
            else Step1Hint.Show(Step1Text.SilkBusy);
            return;
        }
        var a = Step1Stealth.FindAnchor(grid, transform.position, self.IsFacingRightValue, tuning.silkRange);
        if (!a.HasValue) { Step1Hint.Show(string.Format(Step1Text.SilkNoAnchor, tuning.silkRange)); return; }
        anchor = a.Value;
        s = Step1Stealth.FromBody(anchor, transform.position, rb != null ? rb.velocity : Vector2.zero);
        swinging = true; held = 0f;
        self.ExternalDrive = true; self.BlockJump = true; self.BusyMoving = true;
        if (line != null) line.enabled = true;
        if (hook == null) { var sp = Step1ArtSkin.SkillSprite("FxHook"); if (sp != null) { var hg = new GameObject("S245_Hook"); hook = hg.AddComponent<SpriteRenderer>(); hook.sprite = sp; hook.sortingOrder = 21; hg.transform.localScale = Vector3.one * 0.7f; } }
        if (hook != null) { hook.enabled = true; hook.transform.position = new Vector3(anchor.x, anchor.y, 0f); }
        Step1Fx.Link(transform.position, anchor, new Color(0.9f, 0.95f, 1f, 1f));
        Step1Hint.Show(Step1Text.SilkOn, 2.4f);
    }

    private void FixedUpdate()
    {
        if (!swinging || rb == null) return;
        // 碰撞修正：被墙挡住时用刚体真实位置重算摆的状态（不穿墙）
        var real = Step1Stealth.FromBody(anchor, rb.position, rb.velocity);
        if (Vector2.Distance(Step1Stealth.PosOf(anchor, s), rb.position) > 0.25f) s = real;
        float dt = Time.fixedDeltaTime;
        Vector2 input = self.MoveInput;
        bool up = Step1Keys.Held(KeyCode.UpArrow) || Step1Keys.Held(KeyCode.W);
        bool down = Step1Keys.Held(KeyCode.DownArrow) || Step1Keys.Held(KeyCode.S);
        double reel = (down ? 1 : 0) - (up ? 1 : 0);
        double newLen = s.length + reel * tuning.silkReelSpeed * dt;
        s = Step1Stealth.Step(s, newLen, input.x * tuning.silkPump, LaunchFeel.gravity * 0.5, dt, 0.15, 1.2, tuning.silkRange);
        Vector2 target = Step1Stealth.PosOf(anchor, s);
        rb.velocity = (target - rb.position) / dt;
    }

    private void Release(bool hint)
    {
        if (!swinging) return;
        swinging = false;
        if (self != null) { self.ExternalDrive = false; self.BlockJump = false; self.BusyMoving = false; }
        if (rb != null) rb.velocity = Step1Stealth.VelOf(s);
        if (line != null) line.enabled = false;
        if (hook != null) hook.enabled = false;
        cooldown = tuning != null ? tuning.silkCooldown : 1.5f;
    }
}
