using System.Collections.Generic;
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
    /// <summary>S210：从大地图'迟到'跟进房间 → 他不等你，直接开跑。</summary>
    public void SkipStartDelay() { startDelay = 0f; }
    /// <summary>S218：从小镇带进来的"还晕着"——开局多等几秒（上限 maxStunSeconds，H9）。</summary>
    public void AddStartDelay(float seconds) { if (seconds > 0f) startDelay = Mathf.Max(0f, startDelay) + Mathf.Min(seconds, tuning != null ? tuning.maxStunSeconds : 3f); }
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

    private string[] allRows; // S203：任何房间都保留网格（谨慎型绕路 / 贪财型跨层抢道具用）
    private void ParseGrid()
    {
        gridRows = null; allRows = null;
        if (string.IsNullOrEmpty(roomGrid)) return;
        var rows = roomGrid.Replace("\r", "").Split('\n');
        for (int i = 0; i < rows.Length; i++) rows[i] = Step1Layout.StripSlots(rows[i]);
        allRows = rows;
        if (LevelPathPlanner.NeedsPlanning(rows)) gridRows = rows;
    }

    /// <summary>S203：调参"固定性格"（S238 起 = 性格下拉选了某一种）。</summary>
    private MarioPersonalityKind? forcedPersonality => tuning != null && tuning.ForcedPersonalityIndex >= 0 ? (MarioPersonalityKind?)(MarioPersonalityKind)tuning.ForcedPersonalityIndex : null;
    /// <summary>纯逻辑：自动检查第 n 局用哪种性格（1→冲冲 2→谨慎 3→贪财 4→冲冲…）。</summary>
    public static MarioPersonalityKind CycledPersonality(int round) => (MarioPersonalityKind)(((round - 1) % 3 + 3) % 3);
    public static string PersonalityTip(MarioPersonalityKind k) => k == MarioPersonalityKind.Cautious ? Step1Text.CautiousTip : k == MarioPersonalityKind.Greedy ? Step1Text.GreedyTip : "";

    /// <summary>S203：谨慎型当前是不是在绕开被坑点（头顶显示"绕开"）。</summary>
    public bool Detouring { get; private set; }
    private float detourReplan; private Vector2? detourWaypoint; private bool hopRequest;

    /// <summary>纯逻辑：只绕"前面还没到"的被坑点（离自己 ≤ 半径+1 的不算，否则刚被坑完就原地卡住）。</summary>
    public static List<Vector2> SpotsAhead(IReadOnlyList<Vector2> spots, Vector2 mario, float radius)
    {
        var list = new List<Vector2>();
        foreach (var s in spots) if (Vector2.Distance(s, mario) > radius + 1f) list.Add(s);
        return list;
    }

    /// <summary>纯逻辑：楼层路点在上方（要往上跳）时用"指定路线"转向（对准再跳），避免旧 AI 头顶目标的左右徘徊。</summary>
    public static bool UseAuthoredSteering(Vector2? waypoint, Vector2 marioPos) =>
        waypoint.HasValue && waypoint.Value.y > marioPos.y + 0.4f;

    /// <summary>纯逻辑：在网格上为"去 target"算出下一个路点（世界坐标 = 格坐标，与生成器一致）。</summary>
    public static Vector2? PlanWaypoint(string[] rows, Vector2 from, Vector2 target)
    {
        var path = LevelPathPlanner.Path(rows,
            new LevelPathPlanner.Cell(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y)),
            new LevelPathPlanner.Cell(Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.y)));
        if (path == null || path.Count < 2) return null;
        var w = LevelPathPlanner.NextWaypoint(path, from.x);
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
        TricksterBomb.Exploded += eyes.NoteNoise;
        Vent.Clanged += eyes.NoteNoiseNear;
        TauntAbility.Taunted += eyes.NoteTaunt;          // S200：挑衅 = 听见一个位置
        ChainPlan.LinkFired += eyes.NoteChainLink;       // S200：连锁自动触发的机关，看见了照样起疑
        ChainPlan.Clicked += eyes.NoteNoiseNear;         // S200：编号时"咔哒"一声（很近才听得见）
        TricksterFootsteps.Stepped += eyes.NoteFootstep; // S241：夜里你跑动的脚步声（只有位置）
        TricksterBurrow.MoundStirred += eyes.NoteRustle; // S241：看得见的土包在动 = 和草丛晃同一通道（看没看见由眼睛判断）
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
        if (eyes != null) { RustleOnPass.Rustled -= eyes.NoteRustle; CrackedWall.Smashed -= eyes.NoteNoise; TricksterBomb.Exploded -= eyes.NoteNoise; Vent.Clanged -= eyes.NoteNoiseNear; TauntAbility.Taunted -= eyes.NoteTaunt; ChainPlan.LinkFired -= eyes.NoteChainLink; ChainPlan.Clicked -= eyes.NoteNoiseNear; TricksterFootsteps.Stepped -= eyes.NoteFootstep; TricksterBurrow.MoundStirred -= eyes.NoteRustle; }
        if (health != null) health.OnHealthChanged -= HandleHealthChanged;
        if (subscribedManager != null) subscribedManager.OnRoundStart -= ResetForRound;
    }

    public void ResetForRound()
    {
        // S187：每回合一个新种子（时间 + 回合计数），决定回头看/速度浮动；写入记录可复现。
        int seed = unchecked(Environment.TickCount * 31 + (++roundCounter) * 7919);
        // S203：自动检查时轮流测三种性格（每种都要能自己通关，H10）
        Mind.ForcedPersonality = Step1HandsOffCheck.IsRunning && tuning.autoCheckCyclePersonalities ? (MarioPersonalityKind?)CycledPersonality(roundCounter) : forcedPersonality;
        Mind.Reset(seed);
        if (!Step1HandsOffCheck.IsRunning && Mind.Personality != MarioPersonalityKind.Rush) Step1Hint.Show(string.Format(Step1Text.PersonalityHint, Mind.Traits.zh, PersonalityTip(Mind.Personality)), 3.5f);
        roundSpeedFactor = RushMarioMind.RoundSpeedFactor(seed, tuning.roundSpeedVariance);
        eyes?.Forget();
        startDelay = tuning.startDelaySeconds;
        hurtThisFrame = false;
        if (health != null) lastHealth = health.CurrentHealth;
        if (hybrid != null) { hybrid.Bot.ExplorationTarget = null; hybrid.InvalidateCache(); }
    }

    /// <summary>S198：马里奥此刻的赶路目标（宝物，拿到后是出口）。只看他自己的目标（H4）。</summary>
    public Vector2? CurrentGoal() => LootObjective.IsLootCarried ? FindPos<GoalZone>() : FindPos<LootObjective>();

    private Vector2? FindPos<T>() where T : Component
    {
        var c = FindObjectOfType<T>();
        return c != null && c.gameObject.activeInHierarchy ? (Vector2?)c.transform.position : null;
    }

    /// <summary>S220：从小镇带心进房间——直接设血量，并同步 lastHealth（不当成"受伤"、不吃护盾）。</summary>
    public void SetCarriedHealth(int n)
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (health == null) return;
        lastHealth = -1; health.SetCurrent(n); lastHealth = health.CurrentHealth; hurtThisFrame = false;
    }

    private void HandleHealthChanged(int current, int max)
    {
        // S198 道具：护盾 = 下一次受伤免疫（把血加回去）
        if (lastHealth >= 0 && current < lastHealth && RandomPickups.MarioShield && health != null)
        {
            RandomPickups.MarioShield = false;
            health.Heal(lastHealth - current);
            Step1Hint.Show(Step1Text.ShieldBlocked, 1.5f);
            return;
        }
        if (lastHealth >= 0 && current < lastHealth) hurtThisFrame = true;
        lastHealth = current;
    }

    private void Update()
    {
        if (hybrid == null || eyes == null) return;
        var gm = GameManager.Instance;
        bool playing = gm == null || gm.CurrentState == GameState.Playing;
        // 每帧读取，Play 中改调参资产立即生效；追你时提速（S186）
        hybrid.Bot.MarioSpeedScale = (Mind.State == MarioMindState.Chasing ? tuning.chaseSpeedScale : tuning.marioSpeedScale) * roundSpeedFactor * SlowTerrain.CurrentMarioSpeedScale
            * (Time.time < RandomPickups.MarioSpeedUntil ? tuning.pickupSpeedBoost : 1f)
            * (Mind.Cautious ? tuning.cautiousSpeedScale : 1f)
            * (Mind.State == MarioMindState.Running ? Mind.Traits.speedScale : 1f);
        hybrid.Bot.TrapCommitDistance = tuning.trapCommitDistance;
        hybrid.Bot.SkipReactionDelayForTerrain = tuning.smoothJumps;
        hybrid.Bot.HoldStill = false;
        if (!playing) { hybrid.Bot.ExplorationTarget = (Vector2)transform.position; return; }

        if (startDelay > 0f)
        {
            // S224 少等待：准备好了按 Enter 马上开始（开局 0.3 秒后才认，免得关说明面板那一下误触；自动检查时不认）
            if (!Step1HandsOffCheck.IsRunning && !Step1Screen.HelpOpen && !Step1PlaytestLog.IsTyping && startDelay < tuning.startDelaySeconds - 0.3f && Step1Keys.Down(KeyCode.Return)) startDelay = 0f;
            startDelay -= Time.deltaTime;
            hybrid.Bot.ExplorationTarget = (Vector2)transform.position;
            LastOrder = new MarioOrder { state = MarioMindState.Running, mark = "", intent = "READY..." };
            return;
        }

        var percept = new MarioPercept
        {
            scanReady = scan != null && scan.isActiveAndEnabled && scan.IsReady,
            carryingLoot = LootObjective.IsLootCarried,
            hurt = hurtThisFrame,
            alarm = Step1HakoniwaEvents.AlarmOn
        };
        eyes.Look(Time.deltaTime, ref percept);
        if (hurtThisFrame) Hurt?.Invoke(Mind.State);
        hurtThisFrame = false;

        MarioOrder order = Mind.Tick(Time.deltaTime, percept);
        // S195：赶路（去拿宝/回出口）时，多层楼房间按楼层路径给下一个路点；其它状态（查看/追你）不改
        if (order.moveTarget == null && gridRows != null && order.state == MarioMindState.Running)
        {
            replanTimer -= Time.deltaTime;
            if (replanTimer <= 0f || floorWaypoint == null || Vector2.Distance(transform.position, floorWaypoint.Value) < 0.5f)
            {
                replanTimer = tuning.floorReplanSeconds;
                Vector2? goal = LootObjective.IsLootCarried ? FindPos<GoalZone>() : FindPos<LootObjective>();
                floorWaypoint = goal.HasValue ? PlanWaypoint(gridRows, transform.position, goal.Value) : null;
            }
            order.moveTarget = floorWaypoint;
        }
        else floorWaypoint = null;
        // S203 谨慎型：赶路时绕开被坑过的地方（能绕就绕，绕不开走原路，H1/H10）
        Detouring = false;
        if (order.state == MarioMindState.Running && order.intent != "DODGE" && order.intent != "GRAB" && allRows != null
            && Mind.Traits.avoidHurtSpots && Mind.HurtSpots.Count > 0)
        {
            detourReplan -= Time.deltaTime;
            Vector2? goal = CurrentGoal();
            if (goal.HasValue && (detourReplan <= 0f || detourWaypoint == null || Vector2.Distance(transform.position, detourWaypoint.Value) < 0.5f))
            {
                detourReplan = tuning.floorReplanSeconds;
                var ahead = SpotsAhead(Mind.HurtSpots, transform.position, tuning.avoidRadius);
                bool took = false;
                detourWaypoint = ahead.Count > 0 ? MarioPersonality.DetourWaypoint(allRows, transform.position, goal.Value, ahead, tuning.avoidRadius, out took) : null;
                if (!took) detourWaypoint = null;
            }
            if (detourWaypoint.HasValue) { order.moveTarget = detourWaypoint; floorWaypoint = detourWaypoint; Detouring = true; order.intent = "DETOUR"; }
            else if (MarioPersonality.ShouldHop(Mind.HurtSpots, transform.position, marioController != null && marioController.IsFacingRight)) { hopRequest = true; order.intent = "HOP"; }
        }
        else detourWaypoint = null;
        // S203 贪财型：道具箱在别的楼层 → 用楼层寻路走过去
        if (order.intent == "GRAB" && order.moveTarget.HasValue && allRows != null && Mathf.Abs(order.moveTarget.Value.y - transform.position.y) > 1.5f)
        {
            var w = PlanWaypoint(allRows, transform.position, order.moveTarget.Value);
            if (w.HasValue) { order.moveTarget = w; floorWaypoint = w; }
        }
        // S198（修"来回跳"）：往上走的路点（楼板洞口正上方）会触发旧 AI 的"头顶目标 → 左右徘徊跳"模式。
        // 楼层路点一律按"指定路线"处理：对准路点正下方再起跳（HeuristicBotInputProvider 的 AuthoredRouteTarget 通道）。
        hybrid.Bot.AuthoredRouteTarget = UseAuthoredSteering(floorWaypoint, transform.position);
        hybrid.Bot.JumpRequest = hopRequest; hopRequest = false;
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
