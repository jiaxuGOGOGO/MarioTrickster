using UnityEngine;

/// <summary>
/// S241：U 遁地（用户："捣蛋者在地面上可以遁地移动，裸露的地面可以看到，并且马里奥可以反制；草地遮盖的看不到；
/// 白天有光的地面是这样，晚上或者下雨天的裸露地面也看不到"）。
///   · 条件：现形、站在地上、脚下是土（地面 # 或草地 v）。钻进去后身体看不见（马里奥的眼睛看不到你），走得慢一点（×0.8），
///     不能跳 / 触发机关 / 伪装；最多 burrowMaxSeconds 秒，走出土地（台面、箱子、坑边）自动钻出来。
///   · 代价（土包）：在<b>裸露的地面</b>上走 → 拱起土包。土包只有 "裸地 + 亮 + 不下雨 + 你在动" 时看得见（Step1Stealth.MoundVisible）；
///     看得见的土包被马里奥看见 = 和草丛晃一样（他会起疑、过来查看）。
///   · 反制：他<b>踩到</b>你头顶的土包，或者用<b>扫描</b>扫到 → 你被逼出来、晕 burrowFlushStunSeconds 秒。
/// H4：土包是一个独立的公开物体（不是你的子物体），马里奥只通过 MoundStirred（一个 Transform）和眼睛看见它；
///     被踩 / 被扫的判断在你这边（你本来就看得见全局），马里奥那边一行不读你。
/// 参考：Rek'Sai（英雄联盟）遁地 + 震感只感知移动中的目标；Dig Dug 的地下移动。只借规则不借素材。
/// 由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class TricksterBurrow : MonoBehaviour
{
    /// <summary>看得见的土包在动（只给土包物体，马里奥的眼睛判断看没看见，和草丛晃同一通道）。</summary>
    public static event System.Action<Transform> MoundStirred;
    public static TricksterBurrow Instance { get; private set; }

    private MarioMindTuningSO tuning;
    private TricksterController self;
    private BoxCollider2D box;
    private Rigidbody2D rb;
    private Renderer[] renderers;
    private string[] grid;
    private MarioController mario;
    private ScanAbility scan;
    private GameManager manager;
    private Transform mound; private SpriteRenderer moundSr;
    private bool burrowed;
    private float under, cooldown, airborne, stirAge;
    private float savedSpeed = 1f;
    private int groundMask;
    private static readonly Collider2D[] s_hits = new Collider2D[8];

    public bool Burrowed => burrowed;
    /// <summary>S241：在地下或挂在蛛丝上 = 身体被占用，别的技能键（B Z G T F ↓进管/坐炮）都不生效。</summary>
    public static bool BodyBusy => (Instance != null && Instance.burrowed) || TricksterSilk.Swinging;
    public float Cooldown => cooldown;
    public bool MoundShowing { get; private set; }

    private void Awake() { Instance = this; }

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        box = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<Renderer>(true);
        var src = Step1PrankRoomBuilderBridge.CurrentRoom; // 复制一份再去槽位（别改共享的房间表）
        grid = src != null ? new string[src.Length] : null;
        if (grid != null) for (int i = 0; i < grid.Length; i++) grid[i] = Step1Layout.StripSlots(src[i]);
        mario = FindObjectOfType<MarioController>();
        scan = mario != null ? mario.GetComponent<ScanAbility>() : null;
        if (scan != null) scan.OnScanActivated += HandleScan;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        groundMask = LayerMask.GetMask("Ground");
        var go = new GameObject("S241_BurrowMound"); // 独立物体：不是你的子物体（H4）
        mound = go.transform;
        moundSr = go.AddComponent<SpriteRenderer>();
        moundSr.sprite = Step1Sprites.Square; moundSr.sortingOrder = 6;
        go.transform.localScale = new Vector3(0.9f, 0.32f, 1f);
        go.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (scan != null) scan.OnScanActivated -= HandleScan;
        if (manager != null) manager.OnRoundStart -= ResetRound;
        if (mound != null) Destroy(mound.gameObject);
    }

    private void ResetRound() { if (burrowed) Surface(false); cooldown = 0f; }

    /// <summary>脚下那一格的字符（看地形图；炸掉的格子用物理再确认一次）。</summary>
    private char GroundBelow()
    {
        if (grid == null || box == null) return '.';
        var b = box.bounds;
        int x = Mathf.RoundToInt(b.center.x), y = Mathf.RoundToInt(b.min.y - 0.25f);
        char c = LevelPathPlanner.At(grid, x, y);
        if ((c == '#' || c == 'v') && groundMask != 0 && Physics2D.OverlapPoint(new Vector2(b.center.x, b.min.y - 0.25f), groundMask) == null) return '.';
        return c;
    }

    private bool BushHere()
    {
        int n = Physics2D.OverlapCircleNonAlloc(transform.position, 0.3f, s_hits);
        for (int i = 0; i < n; i++) if (s_hits[i] != null && s_hits[i].GetComponent<SightBlocker>() != null) return true;
        return false;
    }

    private void Update()
    {
        if (self == null || tuning == null) return;
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        bool inputOk = !Step1HandsOffCheck.IsRunning && !Step1PlaytestLog.IsTyping && !Step1Screen.HelpOpen && Time.timeScale > 0f;
        if (burrowed) TickUnder();
        if (!inputOk || !Step1Keys.Down(KeyCode.U)) return;
        if (burrowed) { Surface(true); return; }
        TryBurrow();
    }

    public bool CanBurrowNow =>
        self != null && Step1Stealth.CanBurrow(self.IsGrounded, self.IsDisguised, TricksterKit.BlocksPranks, PranksterCannon.TricksterSeated, TricksterSilk.Swinging, GroundBelow(), cooldown);

    private void TryBurrow()
    {
        if (!CanBurrowNow)
        {
            char g = GroundBelow();
            if (self.IsDisguised || TricksterKit.BlocksPranks || PranksterCannon.TricksterSeated || TricksterSilk.Swinging) Step1Hint.Show(Step1Text.BurrowBusy);
            else if (cooldown > 0f) Step1Hint.Show(string.Format(Step1Text.BurrowCooldown, cooldown));
            else if (g != '#' && g != 'v') Step1Hint.Show(Step1Text.BurrowNoGround);
            return;
        }
        burrowed = true; under = 0f; airborne = 0f;
        savedSpeed = self.AbilitySpeedMultiplier;
        self.AbilitySpeedMultiplier = savedSpeed * tuning.burrowSpeedMultiplier;
        self.BlockJump = true; self.BusyMoving = true;
        SetVisible(false);
        if (mound != null) mound.gameObject.SetActive(true);
        Step1Fx.Dust(transform.position + Vector3.down * 0.4f, 1.2f);
        Step1Hint.Show(Step1Text.BurrowIn, 2.2f);
    }

    private void TickUnder()
    {
        under += Time.deltaTime;
        airborne = self.IsGrounded ? 0f : airborne + Time.deltaTime;
        char g = GroundBelow();
        if (under >= tuning.burrowMaxSeconds || airborne > 0.15f || (g != '#' && g != 'v') || self.IsStunned) { Surface(true); return; }
        // 土包：跟着你，贴在地面上
        var b = box.bounds;
        mound.position = new Vector3(b.center.x, b.min.y + 0.12f, 0f);
        bool moving = rb != null && Mathf.Abs(rb.velocity.x) > 0.6f;
        bool lit = Step1Lighting.IsLit(mound.position);
        MoundShowing = Step1Stealth.MoundVisible(g, BushHere(), lit, Step1Lighting.Raining, moving);
        // 看得见的土包 = 实心棕色；看不见的 = 只有你看得见的虚影（你要知道自己在哪）
        moundSr.color = MoundShowing ? new Color(0.55f, 0.36f, 0.18f, 1f) : new Color(0.55f, 0.45f, 0.35f, 0.3f);
        stirAge += Time.deltaTime;
        if (MoundShowing && stirAge > 0.5f) { stirAge = 0f; MoundStirred?.Invoke(mound); if (LaunchFeel.fx) Step1Fx.Dust(mound.position, 0.4f); }
        // 反制①：他踩到你
        if (mario != null && Step1Stealth.Trampled(mario.transform.position, transform.position, g)) Flush();
    }

    private void HandleScan()
    {
        if (!burrowed || mario == null || tuning == null) return;
        if (Step1Stealth.ScanFlushes(mario.transform.position, transform.position, tuning.scanRadius)) Flush(); // 反制②：扫描（H5 真实）
    }

    private void Flush()
    {
        Surface(false);
        self.ApplyKnockbackStun(tuning.burrowFlushStunSeconds);
        if (rb != null) rb.velocity = new Vector2(0f, 6f);
        Step1Fx.Burst(transform.position, 8, new Color(0.6f, 0.42f, 0.22f, 1f), 5f, Vector2.up, 120f, 18f, 0.14f, 0.45f);
        Step1Hint.Show(Step1Text.BurrowFlushed, 2f);
    }

    private void Surface(bool hint)
    {
        if (!burrowed) return;
        burrowed = false; MoundShowing = false;
        self.AbilitySpeedMultiplier = savedSpeed;
        self.BlockJump = false; self.BusyMoving = false;
        SetVisible(true);
        if (mound != null) mound.gameObject.SetActive(false);
        cooldown = tuning.burrowCooldown;
        Step1Fx.Dust(transform.position + Vector3.down * 0.4f, 1f);
        if (hint) Step1Hint.Show(Step1Text.BurrowOut, 1f);
    }

    private void SetVisible(bool on)
    {
        if (renderers == null) return;
        foreach (var r in renderers) if (r != null && !(r is LineRenderer)) r.enabled = on;
    }
}
