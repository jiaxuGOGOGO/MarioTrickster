using UnityEngine;

/// <summary>
/// 设计宪法第 1 步：冲冲型马里奥的全部调参值（宪法 §6：调参值放数据文件，玩法代码不写字面常量）。
/// 资产位置：Assets/Resources/Step1/RushMarioTuning.asset（由 Step 1 菜单自动创建）；缺失时用这里的默认值。
/// 所有数字都是起点，按试玩结果校准。
/// </summary>
[CreateAssetMenu(fileName = "RushMarioTuning", menuName = "MarioTrickster/Step1 Mario Mind Tuning")]
public class MarioMindTuningSO : ScriptableObject
{
    public const string ResourcePath = "Step1/RushMarioTuning";

    /// <summary>
    /// 数据版本：旧资产缺这个字段时反序列化为 0，编辑器据此把 S183 校准值写入一次（不覆盖之后的手动调参）。
    /// [AI防坑警告] 初始值必须是 0，新建资产时由编辑器写入 CurrentDataVersion。
    /// </summary>
    public const int CurrentDataVersion = 10;
    public int dataVersion = 0;

    [Header("Identity")]
    public string personaName = "Rush";

    [Header("Movement persona (feeds the existing HeuristicBot)")]
    [Range(0f, 1.5f)] public float reactionDelay = 0.12f;
    [Range(0f, 1f)] public float riskTolerance = 0.8f;
    [Tooltip("开局站定几秒，给玩家就位时间（S183 用户反馈来不及：2→4）")]
    public float startDelaySeconds = 4f;
    [Tooltip("马里奥走路速度倍率（1 = 原速 9 格/秒；S183 用户反馈太快：0.55 ≈ 5 格/秒）")]
    [Range(0.2f, 1f)] public float marioSpeedScale = 0.55f;
    [Tooltip("追你时的速度倍率（S186 用户反馈太容易逃脱：追逐时提速到 0.8 ≈ 7.2 格/秒，仍略慢于你的 8 格/秒，能甩掉但要跑）")]
    [Range(0.2f, 1f)] public float chaseSpeedScale = 0.8f;

    [Header("Vision (H4: cone + range + occlusion only)")]
    public float visionRange = 9f;
    [Range(5f, 90f)] public float visionHalfAngle = 50f;
    [Tooltip("贴身距离内不看朝向也能察觉（仍需无遮挡）")]
    public float nearSenseRadius = 0.75f;
    public float eyeHeight = 0.3f;
    public bool showVisionCone = true;

    [Header("Suspicion sources")]
    [Tooltip("看见未伪装的捣蛋者：每秒增加")]
    public float seeTricksterPerSecond = 90f;
    [Tooltip("看见一个'道具'在动：每秒增加")]
    public float seeDisguisedMovePerSecond = 60f;
    [Tooltip("速度超过此值（单位/秒）才算'在动'")]
    public float disguisedMoveThreshold = 0.6f;
    [Tooltip("亲眼看见机关被触发：一次性增加")]
    public float witnessedActivation = 45f;
    [Tooltip("机关触发后这么久内进入马里奥视野，才算'亲眼看见触发'")]
    public float activationWitnessWindow = 0.35f;
    [Tooltip("被机关伤到：一次性增加")]
    public float hurtByTrap = 25f;
    public float decayPerSecond = 14f;

    [Header("Thresholds (H2: '?' must show before '!')")]
    public float curiousThreshold = 35f;
    public float alertThreshold = 100f;
    [Tooltip("H2：'?' 至少显示这么久才允许变成 '!'")]
    public float minOmenSeconds = 0.4f;
    public float maxSuspicion = 130f;

    [Header("Behaviour")]
    public float lookStep = 0.4f;
    public float investigateTimeout = 5f;
    [Tooltip("离可疑点这么近才扫描（扫描半径 5，留余量）")]
    public float investigateScanDistance = 3f;
    public float postScanSeconds = 0.8f;
    public float chaseGiveUpSeconds = 4f;
    [Tooltip("跟丢后按'它刚才跑的方向'往前推算几秒（从头顶跳过去时会转身追）")]
    public float chasePredictSeconds = 0.8f;
    public float arriveDistance = 0.8f;
    public float searchSeconds = 2.5f;
    public float afterSearchSuspicion = 30f;
    [Tooltip("裁判抓捕距离（中心距）。台面高 1 格，略大于 1 以便跳上台可抓")]
    public float catchRadius = 1.1f;
    public float hurtFlashSeconds = 0.8f;
    [Tooltip("被机关伤到后原地发晕几秒（给玩家换位/补刀的窗口；S183 用户反馈机关拦不住他）")]
    public float hurtStunSeconds = 1.2f;
    public float celebrateSeconds = 1.2f;

    [Header("Rules")]
    public int startingLives = 3;
    public float respawnInvulnerableSeconds = 2f;
    [Tooltip("机关触发后多少秒内马里奥受伤，算作这次恶作剧命中")]
    public float prankAttributionSeconds = 2.5f;
    public float roundTimeLimit = 150f;

    [Header("Prank props (S183)")]
    [Tooltip("封路墙升起后挡多久（秒）；原默认 1.5 秒太短，用户反馈拦不住马里奥")]
    public float blockerActiveSeconds = 3.5f;

    [Header("Smoothness & combos (S185)")]
    [Tooltip("机关预警时：离机关不超过这么多格就硬冲过去，否则原地停下等（每次预警只决定一次，不再前后抖）")]
    public float trapCommitDistance = 1.5f;
    [Tooltip("跳坑/跳箱子不再先刹车（反应延迟只用于机关），跳得更顺")]
    public bool smoothJumps = true;
    [Tooltip("两次坑到马里奥（烧到 / 挡停 / 掉坑）间隔不超过这么多秒，算连招")]
    public float comboWindowSeconds = 4f;
    [Tooltip("连招每多一段，被烧到时额外多晕几秒")]
    public float comboBonusStunSeconds = 0.6f;
    [Tooltip("一次最多晕几秒（防止无限控）")]
    public float maxStunSeconds = 3f;
    [Tooltip("同一次'被挡停'至少间隔几秒才再计一次")]
    public float stopDebounceSeconds = 1.5f;
    [Tooltip("连招提示显示几秒")]
    public float comboFlashSeconds = 1.5f;
    [Tooltip("伪装后站着不动多久才能操控机关（原 1.5 秒；缩短让连续触发更顺）")]
    public float disguiseBlendSeconds = 0.8f;

    [Header("Combo feel (S193: fighting-game rules)")]
    [Tooltip("命中顿帧：第 1 段停多久（真实秒，0 = 关闭）")]
    public float hitstopBaseSeconds = 0.05f;
    [Tooltip("每多一段连招，顿帧多停多久")]
    public float hitstopPerStepSeconds = 0.03f;
    [Tooltip("顿帧上限（秒）")]
    public float hitstopMaxSeconds = 0.16f;
    [Tooltip("顿帧期间的时间倍率（0 = 完全定格，0.05 = 几乎定格）")]
    [Range(0f, 1f)] public float hitstopTimeScale = 0.05f;
    [Tooltip("连招每段追加晕眩的递减倍率（格斗游戏 damage scaling）：第 n 段追加 = comboBonusStunSeconds × 此值^(n-2)")]
    [Range(0.1f, 1f)] public float comboStunScaling = 0.7f;
    [Tooltip("屏幕震动：每段幅度（格）")]
    public float shakePerStep = 0.06f;
    [Tooltip("屏幕震动：幅度上限（格）")]
    public float shakeMax = 0.3f;
    [Tooltip("屏幕震动持续（秒）")]
    public float shakeSeconds = 0.25f;

    [Header("New pranks (S193)")]
    [Tooltip("弹簧板弹起速度（格/秒）")]
    public float springLaunchSpeed = 16f;
    [Tooltip("弹簧板水平推送（格/秒）")]
    public float springForwardPush = 2.5f;
    [Tooltip("被弹起后空中不能动的时间（秒）")]
    public float springAirStunSeconds = 0.6f;
    [Tooltip("弹簧板预警（秒，H3 必须 > 0）。短一点，马里奥还没走下板就弹")]
    public float springTelegraphSeconds = 0.4f;
    [Tooltip("弹簧板激活窗口（秒）：窗口内踩上去的都会被弹")]
    public float springActiveSeconds = 0.5f;
    [Tooltip("S195 楼层寻路：多久重算一次路径（秒）。只在宝物与出口不同层的房间生效")]
    public float floorReplanSeconds = 0.5f;
    [Tooltip("S195：房间高于这么多格时，镜头默认'框住两人'而不是'看整个房间'（C 键仍可切换）")]
    public float maxWholeRoomHeight = 16f;
    [Header("Hakoniwa (S196: Souls-style interconnected floors)")]
    [Tooltip("马里奥听见爆炸/塌墙声的范围（格，隔墙也听得见）；通风管咣当声只有它的 1/3")]
    public float hearingRange = 14f;
    [Tooltip("高速撞碎裂墙所需速度（格/秒）")]
    public float wallBreakSpeed = 9f;
    [Tooltip("捷径门：贴近开启侧站多久打开（秒）")]
    public float doorOpenSeconds = 0.6f;
    [Tooltip("每回合随机事件（涌现）：本回合随机选 1 面裂墙在开局前就已经塌了的概率")]
    [Range(0f, 1f)] public float preCollapsedWallChance = 0.35f;

    [Header("Trickster kit (S197)")]
    [Tooltip("捣蛋者身上的炸弹数（每回合，B 键放置，必须现形）")]
    public int bombsPerRound = 3;
    [Tooltip("炸弹引信（秒）：闪烁 + 倒计时（H3）")]
    public float bombFuseSeconds = 1.5f;
    [Tooltip("爆炸半径（格）：炸开范围内的裂墙/裂缝地板/箱子")]
    public float bombRadius = 1.6f;
    [Tooltip("马里奥在爆炸范围内：晕几秒（不扣命）")]
    public float bombStunSeconds = 1.0f;
    [Tooltip("爆炸击退速度（格/秒）")]
    public float bombKnockback = 6f;
    [Tooltip("两次放炸弹之间的冷却（秒）")]
    public float bombCooldown = 2f;
    [Tooltip("缩小：每回合次数（Z 键）")]
    public int shrinkUsesPerRound = 2;
    [Tooltip("缩小：持续秒数（再按 Z 可提前变回）")]
    public float shrinkSeconds = 5f;
    [Tooltip("缩小：身体比例（0.5 = 半高，可以钻 1 格高的缝）")]
    [Range(0.3f, 0.9f)] public float shrinkScale = 0.5f;
    [Tooltip("缩小：移动速度倍率")]
    public float shrinkSpeedMultiplier = 1.25f;
    [Tooltip("通风管：钻入所需时间（秒）")]
    public float ventEnterSeconds = 0.35f;
    [Tooltip("通风管：冷却（秒）")]
    public float ventCooldown = 3f;
    [Tooltip("捣蛋者跳跃力（S197：原 18 只能跳 2.0 格，个别台阶跳不上；20 ≈ 2.5 格，与马里奥一致）")]
    public float tricksterJumpPower = 20f;

    [Header("Mario time stop (S197)")]
    [Tooltip("马里奥每回合能用几次时间静止（0 = 关闭）")]
    public int timeStopUsesPerRound = 1;
    [Tooltip("预警时长（秒）：屏幕边缘蓝光 + 字幕，给你时间钻管/躲草丛（H3）")]
    public float timeStopWarnSeconds = 1.0f;
    [Tooltip("冻结捣蛋者多久（秒）")]
    public float timeStopSeconds = 2.0f;
    [Tooltip("两次之间冷却（秒）")]
    public float timeStopCooldown = 20f;
    [Tooltip("开局多少秒内不会用（给你布置的时间）")]
    public float timeStopFirstDelay = 15f;
    [Tooltip("追你时离你多近才会用（格）")]
    public float timeStopRange = 7f;

    [Header("S198: bombs hurt, cannon aim, snare, pickups")]
    [Tooltip("炸弹炸到马里奥：扣几点血（0 = 只晕不扣）")]
    public int bombDamageMario = 1;
    [Tooltip("炸弹炸到捣蛋者自己：掉几条命（0 = 不伤自己）")]
    public int bombDamageSelf = 1;
    [Tooltip("人肉炮弹冷却（秒）——捣蛋者和马里奥都能钻进没炮弹的炮")]
    public float cannonLaunchCooldown = 30f;
    [Tooltip("人肉炮弹装填时间（秒）")]
    public float cannonLoadSeconds = 0.6f;
    [Tooltip("马里奥 AI 会不会钻大炮（炮口朝他的目标且目标够远时）")]
    public bool marioUsesCannons = true;
    [Tooltip("绳套：被吊起多久（秒）")]
    public float snareSeconds = 10f;
    [Tooltip("每回合随机刷出几个道具箱（'?' 道具点里随机选）")]
    public int pickupsPerRound = 2;
    [Tooltip("道具效果时长（秒）：隐身 / 加速；透视为其 0.6 倍")]
    public float pickupEffectSeconds = 8f;
    [Tooltip("加速道具：马里奥速度倍率")]
    public float pickupSpeedBoost = 1.35f;

    [Header("Movement-limiting terrain (S197)")]
    [Tooltip("毒池：移动速度倍率")]
    public float poisonSpeedScale = 0.55f;
    [Tooltip("毒池：每隔几秒晕一下")]
    public float poisonTickSeconds = 1.2f;
    [Tooltip("毒池：每次晕多久（秒，不扣命）")]
    public float poisonStunSeconds = 0.35f;
    [Tooltip("黏胶：移动速度倍率")]
    public float glueSpeedScale = 0.45f;
    [Tooltip("黏胶：跳跃倍率")]
    public float glueJumpScale = 0.6f;

    [Tooltip("关卡工坊'连招路线'：两个机关水平距离不超过这么多格，就画一条可连线（≈ 连招窗口 × 马里奥赶路速度）")]
    public float comboRouteCells = 10f;
    [Tooltip("香蕉皮滑行速度（格/秒）")]
    public float bananaSlideSpeed = 7f;
    [Tooltip("香蕉皮失控时间（秒）。滑行距离 ≈ 速度 × 时间")]
    public float bananaSlipSeconds = 0.5f;
    [Tooltip("香蕉皮预警（秒）")]
    public float bananaTelegraphSeconds = 0.4f;
    [Tooltip("香蕉皮激活窗口（秒）：窗口内踩上去就滑")]
    public float bananaActiveSeconds = 1.2f;
    [Tooltip("裂缝地板预警（秒）")]
    public float crackTelegraphSeconds = 0.6f;

    [Header("Cannon (S187)")]
    [Tooltip("大炮每回合炮弹数（开炮打马里奥）。打完后可钻进炮口把自己打出去逃跑")]
    public int cannonShotsPerRound = 1;

    [Header("Randomness for emergence (S187)")]
    [Tooltip("看见草丛晃动：起疑值一次性增加（风和人晃得一模一样，马里奥分不出）")]
    public float rustleSuspicion = 40f;
    [Tooltip("草丛起风的最短/最长间隔（秒，≤0 关闭）")]
    public float windMinSeconds = 7f;
    public float windMaxSeconds = 16f;
    [Tooltip("赶路时每秒回头看一眼的概率")]
    public float glanceChancePerSecond = 0.07f;
    [Tooltip("回头看持续几秒")]
    public float glanceSeconds = 0.7f;
    [Tooltip("回头时往回走几格（转身）")]
    public float glanceStep = 1f;
    [Tooltip("每回合马里奥速度随机浮动 ±比例")]
    [Range(0f, 0.3f)] public float roundSpeedVariance = 0.08f;

    [Header("Anti-stuck (S189, H9)")]
    [Tooltip("马里奥这么多秒几乎没动（且没在发晕/起步）就判定卡住并救出")]
    public float stuckSeconds = 6f;
    [Tooltip("判定'几乎没动'的距离（格）")]
    public float stuckMoveEpsilon = 0.6f;
    [Tooltip("S198：卡住判定——这么多秒内到目标的距离没有缩短这么多格，就算卡住（来回跳也算）")]
    public float stuckProgressCells = 1.5f;

    [Header("Theme (S187)")]
    [Tooltip("房间配色主题：Whitebox / AmusementPark(游乐园) / CityPark(公园) / MountainPark(山上公园)。只换颜色/图，不改玩法")]
    public string themePreset = "AmusementPark";

    [Header("Collapse bridge (S183)")]
    [Tooltip("桥重生前检查桥下多深（格）；有人在下面就推迟重生（H9 防止把马里奥封在坑里）")]
    public float bridgeRespawnClearDepth = 2f;
    [Tooltip("桥下检查向左右各扩展几格（覆盖整个坑）")]
    public float bridgeRespawnClearMarginX = 4.5f;

    [Header("Playtest tools (S181)")]
    [Tooltip("菜单 Hands-off check：连续自动跑几局（宪法 H10：无干预时马里奥应能自己通关）")]
    public int autoCheckRounds = 5;
    [Tooltip("自动检查时的时间倍速（只影响检查，不影响正常试玩）。默认 1 = 与真实试玩相同的物理与决策节奏")]
    public float autoCheckTimeScale = 1f;
    [Tooltip("自动检查每局结束后停留多久（真实秒）再开下一局")]
    public float autoCheckRoundGapSeconds = 1.5f;
    [Tooltip("自动检查每局最多等多少秒；超时 = 马里奥卡住（记下卡住的位置）")]
    public float autoCheckRoundTimeoutSeconds = 70f;

    [Header("Playtest screen (S182)")]
    [Tooltip("每次进入 Play 先显示玩法说明并暂停，按任意键开始")]
    public bool showHelpOnStart = true;

    [Header("Camera")]
    public Step1CameraMode cameraMode = Step1CameraMode.WholeRoom;
    public float cameraPadding = 0.6f;
    public float frameBothMinHeight = 9f;

    /// <summary>把 S183 校准值写入旧资产（只在 dataVersion 较旧时执行一次）。返回是否有改动。</summary>
    public bool UpgradeData()
    {
        if (dataVersion >= CurrentDataVersion) return false;
        if (dataVersion < 1)
        {
            startDelaySeconds = 4f;
            seeTricksterPerSecond = 90f;
            marioSpeedScale = 0.55f;
            hurtStunSeconds = 1.2f;
            blockerActiveSeconds = 3.5f;
        }
        if (dataVersion < 10)
        {
            bombDamageMario = 1; bombDamageSelf = 1; cannonLaunchCooldown = 30f; cannonLoadSeconds = 0.6f; marioUsesCannons = true;
            snareSeconds = 10f; pickupsPerRound = 2; pickupEffectSeconds = 8f; pickupSpeedBoost = 1.35f; stuckProgressCells = 1.5f;
        }
        if (dataVersion < 9)
        {
            bombsPerRound = 3; bombFuseSeconds = 1.5f; bombRadius = 1.6f; bombStunSeconds = 1f; bombKnockback = 6f; bombCooldown = 2f;
            shrinkUsesPerRound = 2; shrinkSeconds = 5f; shrinkScale = 0.5f; shrinkSpeedMultiplier = 1.25f;
            ventEnterSeconds = 0.35f; ventCooldown = 3f; tricksterJumpPower = 20f;
            timeStopUsesPerRound = 1; timeStopWarnSeconds = 1f; timeStopSeconds = 2f; timeStopCooldown = 20f; timeStopFirstDelay = 15f; timeStopRange = 7f;
            poisonSpeedScale = 0.55f; poisonTickSeconds = 1.2f; poisonStunSeconds = 0.35f; glueSpeedScale = 0.45f; glueJumpScale = 0.6f;
        }
        if (dataVersion < 8)
        {
            hearingRange = 14f; wallBreakSpeed = 9f; doorOpenSeconds = 0.6f; preCollapsedWallChance = 0.35f;
        }
        if (dataVersion < 7)
        {
            hitstopBaseSeconds = 0.05f; hitstopPerStepSeconds = 0.03f; hitstopMaxSeconds = 0.16f; hitstopTimeScale = 0.05f;
            comboStunScaling = 0.7f; shakePerStep = 0.06f; shakeMax = 0.3f; shakeSeconds = 0.25f;
            springLaunchSpeed = 16f; springForwardPush = 2.5f; springAirStunSeconds = 0.6f;
            springTelegraphSeconds = 0.4f; springActiveSeconds = 0.5f; crackTelegraphSeconds = 0.6f;
            comboRouteCells = 10f; floorReplanSeconds = 0.5f; maxWholeRoomHeight = 16f; bananaSlideSpeed = 7f; bananaSlipSeconds = 0.5f; bananaTelegraphSeconds = 0.4f; bananaActiveSeconds = 1.2f;
        }
        if (dataVersion < 6)
        {
            stuckSeconds = 6f; stuckMoveEpsilon = 0.6f;
        }
        if (dataVersion < 5)
        {
            cannonShotsPerRound = 1; rustleSuspicion = 40f; windMinSeconds = 7f; windMaxSeconds = 16f;
            glanceChancePerSecond = 0.07f; glanceSeconds = 0.7f; glanceStep = 1f; roundSpeedVariance = 0.08f;
            themePreset = "AmusementPark";
        }
        if (dataVersion < 4)
        {
            chaseSpeedScale = 0.8f; chasePredictSeconds = 0.8f;
        }
        if (dataVersion < 3)
        {
            trapCommitDistance = 1.5f; smoothJumps = true; comboWindowSeconds = 4f;
            comboBonusStunSeconds = 0.6f; maxStunSeconds = 3f; stopDebounceSeconds = 1.5f; comboFlashSeconds = 1.5f;
            disguiseBlendSeconds = 0.8f;
        }
        if (dataVersion < 2)
        {
            autoCheckRoundTimeoutSeconds = 70f; // S184 房间变大，单局更长
        }
        dataVersion = CurrentDataVersion;
        return true;
    }

    public static MarioMindTuningSO LoadOrDefault()
    {
        var tuning = Resources.Load<MarioMindTuningSO>(ResourcePath);
        if (tuning != null) return tuning;
        tuning = CreateInstance<MarioMindTuningSO>();
        tuning.dataVersion = CurrentDataVersion;
        return tuning;
    }
}

/// <summary>第 1 步摄像机：单房间默认整屏（《地狱邻居》式），玩家同时看得到自己和马里奥。</summary>
public enum Step1CameraMode { WholeRoom, FrameBoth, FollowTrickster }
