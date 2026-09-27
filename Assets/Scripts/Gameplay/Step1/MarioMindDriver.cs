using System;
using UnityEngine;

/// <summary>
/// 设计宪法第 1 步：把 RushMarioMind 接到场景上（挂在 Mario 身上）。
/// 每帧：MarioEyes 看 → 组装 MarioPercept → Mind.Tick → 把 MarioOrder 交给现有 HeuristicBot（移动）/ ScanAbility（扫描）/ TricksterLives（抓捕裁判）。
/// 执行顺序早于 InputManager，保证本帧 Bot 读到的是本帧目标。
/// H4：本类不读捣蛋者任何状态；捣蛋者引用只转交给 MarioEyes 和 TricksterLives（裁判）。
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MarioController))]
public class MarioMindDriver : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;

    private MarioController marioController;
    private PlayerHealth health;
    private ScanAbility scan;
    private InputManager inputManager;
    private HybridInputProvider hybrid;
    private TricksterLives lives;
    private TricksterAbilitySystem abilities;
    private MarioEyes eyes;
    private int lastHealth = -1;
    private bool hurtThisFrame;
    private float startDelay;
    private GameManager subscribedManager;
    private float roundSpeedFactor = 1f;
    private int roundCounter;
    /// <summary>本回合随机种子（写入试玩记录，便于复现）。</summary>
    public int RoundSeed => Mind != null ? Mind.Seed : 0;

    public RushMarioMind Mind { get; private set; }
    public MarioOrder LastOrder { get; private set; }
    public MarioMindTuningSO Tuning => tuning;
    public bool IsWaitingToStart => startDelay > 0f;
    public float StartDelayRemaining => Mathf.Max(0f, startDelay);
    /// <summary>移动执行层（只读，供连招统计读"是否被机关挡停"）。</summary>
    public HeuristicBotInputProvider Bot => hybrid != null ? hybrid.Bot : null;
    /// <summary>马里奥被机关伤到（供试玩日志做恶作剧归因）。</summary>
    // S195：楼层寻路（只在"宝物与出口不在同一层"的房间启用；默认恶作剧房间行为不变）
    [SerializeField, TextArea(2, 20)] private string roomGrid = "";
    private string[] gridRows;
    private float replanTimer;
    private Vector2? floorWaypoint;
    public Vector2? FloorWaypoint => floorWaypoint;
    public bool FloorPlanning => gridRows != null;

    /// <summary>构建器写入房间网格（第 0 行在最上面）。宝物与出口同层时不启用。</summary>
    public void SetRoomGrid(string[] rows)
    {
        roomGrid = rows != null ? string.Join("\n", rows) : "";
        ParseGrid();
    }

    private void ParseGrid()
    {
        gridRows = null;
        if (string.IsNullOrEmpty(roomGrid)) return;
        var rows = roomGrid.Replace("\r", "").Split('\n');
        for (int i = 0; i < rows.Length; i++) rows[i] = Step1Layout.StripSlots(rows[i]);
        if (LevelPathPlanner.NeedsPlanning(rows)) gridRows = rows;
    }

    /// <summary>纯逻辑：在网格上为"去 target"算出下一个路点（世界坐标 = 格坐标，与生成器一致）。</summary>
    public static Vector2? PlanWaypoint(string[] rows, Vector2 from, Vector2 target)
    {
        var path = LevelPathPlanner.Path(rows,
            new LevelPathPlanner.Cell(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y)),
            new LevelPathPlanner.Cell(Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.y)));
        if (path == null || path.Count < 2) return null;
        var w = LevelPathPlanner.NextWaypoint(path);
        return new Vector2(w.x, w.y);
    }

    public event Action<MarioMindState> Hurt;
    public event Action Caught;

    private void Awake()
    {
        ParseGrid();
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        marioController = GetComponent<MarioController>();
        health = GetComponent<PlayerHealth>();
        scan = GetComponent<ScanAbility>();
        Mind = new RushMarioMind(tuning);
    }

    private void Start()
    {
        var figure = FindObjectOfType<TricksterController>();
        eyes = new MarioEyes(tuning, marioController, figure);
        lives = FindObjectOfType<TricksterLives>();
        abilities = figure != null ? figure.AbilitySystem : null;
        if (abilities != null) abilities.OnPropActivated += eyes.NotePropActivated;
        RustleOnPass.Rustled += eyes.NoteRustle;
        CrackedWall.Smashed += eyes.NoteNoise;
        if (health != null) { health.OnHealthChanged += HandleHealthChanged; lastHealth = health.CurrentHealth; }

        inputManager = FindObjectOfType<InputManager>();
        hybrid = new HybridInputProvider { MarioIsAI = true, TricksterIsAI = false };
        var persona = ScriptableObject.CreateInstance<BotPersonaConfigSO>();
        persona.personaName = tuning.personaName;
        persona.reactionDelay = tuning.reactionDelay;
        persona.riskTolerance = tuning.riskTolerance;
        persona.scanAggression = 0f;
        hybrid.marioPersona = persona;
        hybrid.Bot.RunnerStrategy = HeuristicBotInputProvider.RunnerPolicy.Rush;
        // H2：所有扫描都由心智在 '!' 之后发出；关闭 Bot 自带扫描（证据扫描 + 盲扫，scanAggression=0）。
        hybrid.Bot.EvidenceDrivenScanning = false;
        if (inputManager != null) inputManager.SetInputProvider(hybrid);

        subscribedManager = GameManager.Instance;
        if (subscribedManager != null) subscribedManager.OnRoundStart += ResetForRound;
        ResetForRound();
    }

    private void OnDestroy()
    {
        if (abilities != null && eyes != null) abilities.OnPropActivated -= eyes.NotePropActivated;
        if (eyes != null) { RustleOnPass.Rustled -= eyes.NoteRustle; CrackedWall.Smashed -= eyes.NoteNoise; }
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        if (subscribedManager != null) subscribedManager.OnRoundStart -= ResetForRound;
    }

    public void ResetForRound()
    {
        // S187：每回合一个新种子（时间 + 回合计数），决定回头看/速度浮动；写入记录可复现。
        int seed = unchecked(Environment.TickCount * 31 + (++roundCounter) * 7919);
        Mind.Reset(seed);
        roundSpeedFactor = RushMarioMind.RoundSpeedFactor(seed, tuning.roundSpeedVariance);
        eyes?.Forget();
        startDelay = tuning.startDelaySeconds;
        hurtThisFrame = false;
        if (health != null) lastHealth = health.CurrentHealth;
        if (hybrid != null) { hybrid.Bot.ExplorationTarget = null; hybrid.InvalidateCache(); }
    }

    private Vector2? FindPos<T>() where T : Component
    {
        var c = FindObjectOfType<T>();
        return c != null && c.gameObject.activeInHierarchy ? (Vector2?)c.transform.position : null;
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (lastHealth >= 0 && current < lastHealth) hurtThisFrame = true;
        lastHealth = current;
    }

    private void Update()
    {
        if (hybrid == null || eyes == null) return;
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.CurrentState == GameState.Playing;
        // 每帧读取，Play 中改调参资产立即生效；追你时提速（S186）
        hybrid.Bot.MarioSpeedScale = (Mind.State == MarioMindState.Chasing ? tuning.chaseSpeedScale : tuning.marioSpeedScale) * roundSpeedFactor;
        hybrid.Bot.TrapCommitDistance = tuning.trapCommitDistance;
        hybrid.Bot.SkipReactionDelayForTerrain = tuning.smoothJumps;
        hybrid.Bot.HoldStill = false;
        if (!playing) { hybrid.Bot.ExplorationTarget = (Vector2)transform.position; return; }

        if (startDelay > 0f)
        {
            startDelay -= Time.deltaTime;
            hybrid.Bot.ExplorationTarget = (Vector2)transform.position;
            LastOrder = new MarioOrder { state = MarioMindState.Running, mark = "", intent = "READY..." };
            return;
        }

        var percept = new MarioPercept
        {
            scanReady = scan != null && scan.isActiveAndEnabled && scan.IsReady,
            carryingLoot = LootObjective.IsLootCarried,
            hurt = hurtThisFrame
        };
        eyes.Look(Time.deltaTime, ref percept);
        if (hurtThisFrame) Hurt?.Invoke(Mind.State);
        hurtThisFrame = false;

        MarioOrder order = Mind.Tick(Time.deltaTime, percept);
        // S195：赶路（去拿宝/回出口）时，多层楼房间按楼层路径给下一个路点；其它状态（查看/追你）不改
        if (order.moveTarget == null && gridRows != null && order.state == MarioMindState.Running)
        {
            replanTimer -= Time.deltaTime;
            if (replanTimer <= 0f || floorWaypoint == null || Vector2.Distance(transform.position, floorWaypoint.Value) < 0.8f)
            {
                replanTimer = tuning.floorReplanSeconds;
                Vector2? goal = LootObjective.IsLootCarried ? FindPos<GoalZone>() : FindPos<LootObjective>();
                floorWaypoint = goal.HasValue ? PlanWaypoint(gridRows, transform.position, goal.Value) : null;
            }
            order.moveTarget = floorWaypoint;
        }
        else floorWaypoint = null;
        hybrid.Bot.ExplorationTarget = order.moveTarget;
        if (order.scan && scan != null) scan.ActivateScan();
        if (order.tryCatch && lives != null && lives.TryCatch(transform.position))
        {
            Mind.OnCaught();
            eyes.Forget();
            Caught?.Invoke();
            order = Mind.Tick(0f, new MarioPercept { marioPos = percept.marioPos, carryingLoot = percept.carryingLoot });
            hybrid.Bot.ExplorationTarget = order.moveTarget;
        }
        LastOrder = order;
        hybrid.Bot.HoldStill = Mind.IsStunned; // S185：晕的时候真的站住（不抖、不跳）
    }
}
