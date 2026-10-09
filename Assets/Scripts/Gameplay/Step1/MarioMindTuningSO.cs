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
    public const int CurrentDataVersion = 33;
    /// <summary>S226 E7：房间游戏速度只许 0.5~1。</summary>
    public static float ClampRoomSpeed(float v) => Mathf.Clamp(v, 0.5f, 1f);
    public int dataVersion = 0;

    [Header("Identity")]
    [Tooltip("马里奥 AI 的名字（只用于日志显示，不影响玩法）")]
    public string personaName = "Rush";

    [Header("Movement persona (feeds the existing HeuristicBot)")]
    [Tooltip("看见机关预警后过多久才反应（秒）")]
    [Range(0f, 1.5f)] public float reactionDelay = 0.2f;
    [Tooltip("多敢冒险：越大越敢硬冲预警中的机关")]
    [Range(0f, 1f)] public float riskTolerance = 0.8f;
    [Tooltip("开局站定几秒，给玩家就位时间（S183 用户反馈来不及：2→4）")]
    public float startDelaySeconds = 4f;
    [Tooltip("马里奥走路速度倍率（1 = 原速 9 格/秒；S183 用户反馈太快：0.55 ≈ 5 格/秒）")]
    [Range(0.2f, 1f)] public float marioSpeedScale = 0.55f;
    [Tooltip("追你时的速度倍率（S186 用户反馈太容易逃脱：追逐时提速到 0.8 ≈ 7.2 格/秒，仍略慢于你的 8 格/秒，能甩掉但要跑）")]
    [Range(0.2f, 1f)] public float chaseSpeedScale = 0.8f;

    [Header("Vision (H4: cone + range + occlusion only)")]
    [Tooltip("马里奥能看多远（格）。越大越难躲")]
    public float visionRange = 9f;
    [Tooltip("视野扇形的一半角度（度）：50 = 前方 100° 都看得见")]
    [Range(5f, 90f)] public float visionHalfAngle = 50f;
    [Tooltip("贴身距离内不看朝向也能察觉（仍需无遮挡）")]
    public float nearSenseRadius = 0.75f;
    [Tooltip("马里奥眼睛离脚底多高（格）：决定矮墙/箱子能不能挡住视线")]
    public float eyeHeight = 0.3f;
    [Tooltip("画出马里奥的白色视野扇形（只是显示，不改规则）")]
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
    [Tooltip("起疑每秒自己降多少（你躲好后他多快忘掉）")]
    public float decayPerSecond = 14f;

    [Header("Thresholds (H2: '?' must show before '!')")]
    [Tooltip("起疑到这么多 → 头顶 ?（停下来看一眼）")]
    public float curiousThreshold = 35f;
    [Tooltip("起疑到这么多 → 头顶 !（过来查看 / 追你）")]
    public float alertThreshold = 100f;
    [Tooltip("H2：'?' 至少显示这么久才允许变成 '!'")]
    public float minOmenSeconds = 0.45f;
    [Tooltip("起疑最多能到多少（上限）")]
    public float maxSuspicion = 130f;

    [Header("Behaviour")]
    [Tooltip("起疑时往可疑方向探头看几格")]
    public float lookStep = 0.4f;
    [Tooltip("走去查看可疑点，最多走几秒就放弃")]
    public float investigateTimeout = 5f;
    [Tooltip("离可疑点这么近才扫描（扫描半径 5，留余量）")]
    public float investigateScanDistance = 3f;
    [Tooltip("到了可疑点扫一眼之后，再站几秒才走")]
    public float postScanSeconds = 0.8f;
    [Tooltip("追丢你之后几秒放弃")]
    public float chaseGiveUpSeconds = 4f;
    [Tooltip("跟丢后按'它刚才跑的方向'往前推算几秒（从头顶跳过去时会转身追）")]
    public float chasePredictSeconds = 0.8f;
    [Tooltip("离可疑点这么近（格）算走到了")]
    public float arriveDistance = 0.8f;
    [Tooltip("在可疑点附近搜几秒")]
    public float searchSeconds = 2.5f;
    [Tooltip("搜完没找到，起疑降到这么多（仍然有点警惕）")]
    public float afterSearchSuspicion = 30f;
    [Tooltip("裁判抓捕距离（中心距）。台面高 1 格，略大于 1 以便跳上台可抓")]
    public float catchRadius = 1.1f;
    [Tooltip("被机关打中时身体闪烁几秒（只是显示）")]
    public float hurtFlashSeconds = 0.8f;
    [Tooltip("被机关伤到后原地发晕几秒（给玩家换位/补刀的窗口；S183 用户反馈机关拦不住他）")]
    public float hurtStunSeconds = 1.2f;
    [Tooltip("拿到宝物后原地高兴几秒（你的补刀窗口）")]
    public float celebrateSeconds = 1.2f;

    [Header("Rules")]
    [Tooltip("你（捣蛋者）每局几条命：被他抓到 / 被炸到都会掉")]
    public int startingLives = 3;
    [Tooltip("你掉命后几秒无敌（不会连着掉）")]
    public float respawnInvulnerableSeconds = 2f;
    [Tooltip("机关触发后多少秒内马里奥受伤，算作这次恶作剧命中")]
    public float prankAttributionSeconds = 2.5f;
    [Tooltip("一局最长多少秒（长关卡会按路线自动加长，见 roundTimePerRouteSecond）")]
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
    [Tooltip("弹簧板弹起速度（格/秒）。S216 起全程有重力 launchGravity=40：15 → 最高约 2.8 格，头顶空 4 格正好够")]
    public float springLaunchSpeed = 15f;
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

    [Header("S203: Mario personalities (Rush / Cautious / Greedy)")]
    [Tooltip("马里奥的性格：随机 = 每回合按下面的权重抽一种；选冲冲 / 谨慎 / 贪财 = 每回合都是它（试某种性格时用）。S238：以前是「随机开关」+「固定性格」两个设置做同一件事，合成这一个")]
    public Step1PersonalityChoice marioPersonality = Step1PersonalityChoice.Random;
    // S238 以前的两个旧字段：只为读旧资产（数据版本 28 换算到 marioPersonality），Inspector 不显示，代码别再用。
    [HideInInspector, SerializeField] private bool personalitiesEnabled = true;
    [HideInInspector, SerializeField] private int fixedPersonality = -1;
    /// <summary>S238：-1 = 随机；0/1/2 = 固定那种性格。</summary>
    public int ForcedPersonalityIndex => marioPersonality == Step1PersonalityChoice.Random ? -1 : (int)marioPersonality - 1;
    /// <summary>S238 纯逻辑：旧的两个设置 → 新下拉（固定性格优先；关了随机 = 冲冲）。</summary>
    public static Step1PersonalityChoice FromOld(bool enabled, int fixedIndex)
        => fixedIndex >= 0 ? (Step1PersonalityChoice)(Mathf.Clamp(fixedIndex, 0, 2) + 1) : enabled ? Step1PersonalityChoice.Random : Step1PersonalityChoice.Rush;
    [Tooltip("抽到冲冲型的权重")]
    public float rushWeight = 1f;
    [Tooltip("抽到谨慎型的权重")]
    public float cautiousWeight = 1f;
    [Tooltip("抽到贪财型的权重")]
    public float greedyWeight = 1f;
    [Tooltip("开局头顶显示性格几秒")]
    public float personalityIntroSeconds = 3f;
    [Tooltip("谨慎型赶路速度倍率")]
    [Range(0.5f, 1f)] public float cautiousTypeSpeedScale = 0.9f;
    [Tooltip("谨慎型起疑速度倍率（>1 更容易起疑）")]
    public float cautiousTypeSuspicionScale = 1.25f;
    [Tooltip("谨慎型绕开被坑点的范围（格）")]
    public float avoidRadius = 2f;
    [Tooltip("贪财型：多远的道具箱都去抢（格，跨层也算）")]
    public float greedyPickupDetourCells = 14f;
    [Tooltip("贪财型：够不着的道具箱几秒后放弃")]
    public float greedyGiveUpSeconds = 5f;
    [Tooltip("贪财型起疑速度倍率（抢道具时再减半）")]
    public float greedySuspicionScale = 0.9f;
    [Tooltip("自动检查时轮流测三种性格")]
    public bool autoCheckCyclePersonalities = true;

    [Header("S202: replay, strategy sim, trap probe, Mario dodge/grab")]
    [Tooltip("完美连锁/大连招后慢动作回放")]
    public bool replayEnabled = true;
    [Tooltip("回放最近几秒")]
    public float replaySeconds = 4f;
    [Tooltip("回放速度（0.35 = 慢动作）")]
    [Range(0.1f, 1f)] public float replaySpeed = 0.35f;
    [Tooltip("连招达到几段也回放（0 = 只在完美连锁时回放）")]
    public int replayMinCombo = 4;
    [Tooltip("两次回放之间至少隔多久（真实秒）")]
    public float replayCooldown = 12f;
    [Tooltip("构建房间时：模拟'最坏的对手用炸弹把马里奥困死'，把会被利用的承重格加固（炸不掉、画铆钉）")]
    public bool reinforceAgainstBombs = true;
    [Tooltip("陷阱试探：每个机关每局最多触发几次")]
    public int probeUsesPerTrap = 1;
    [Tooltip("马里奥看见冒烟的炸弹/油桶会先退到爆炸圈外")]
    public bool dodgeVisibleDanger = true;
    [Tooltip("躲闪：退到爆炸半径外多少格")]
    public float dodgeMargin = 0.8f;
    [Tooltip("马里奥会顺路捡道具箱：同层、这么多格以内")]
    public float pickupDetourCells = 4f;

    [Header("S200: chain plan (F), taunt (T), tripwire, Mario learning")]
    [Tooltip("一条连锁最多几个机关")]
    public int maxChainLinks = 4;
    [Tooltip("按 F 编号：离你多近的机关（格）")]
    public float chainArmRange = 2.5f;
    [Tooltip("Shift+F 一键布置：你周围多远的机关（格）")]
    public float chainAutoRange = 10f;
    [Tooltip("连锁启动后，多久没接上下一环就结束（秒，每接一环续时）")]
    public float chainLiveSeconds = 6f;
    [Tooltip("预判容差：马里奥预计落点离机关这么近（格）就触发")]
    public float chainFireTolerance = 0.8f;
    [Tooltip("大炮：马里奥进入炮口前方多远（格）就开炮")]
    public float chainCannonRange = 6f;
    [Tooltip("每接一环的顿帧（秒，演出感）")]
    public float chainHitstopSeconds = 0.06f;
    [Tooltip("挑衅：每回合次数")]
    public int tauntUses = 3;
    [Tooltip("挑衅：冷却（秒）")]
    public float tauntCooldown = 8f;
    [Tooltip("挑衅：马里奥听见后增加的起疑值（35 = 起疑，100 = 过来）")]
    public float tauntSuspicion = 70f;
    [Tooltip("绊线：绊一下多久（秒）")]
    public float tripStunSeconds = 0.4f;
    [Tooltip("马里奥会记住被坑的地方，下次经过放慢（学习层）")]
    public bool learnFromHurt = true;
    [Tooltip("学习层：前方多远内有被坑过的地方就小心（格）")]
    public float cautiousRadius = 3f;
    [Tooltip("学习层：小心时的速度倍率（越小越慢，给你重新布置的机会也更难坑中）")]
    [Range(0.3f, 1f)] public float cautiousSpeedScale = 0.7f;

    [Header("S199: oil barrel, cage, decoy, alarm, door kick")]
    [Tooltip("油桶引信（秒）：被点燃后闪烁多久爆炸")]
    public float oilFuseSeconds = 0.8f;
    [Tooltip("油桶爆炸半径（格），比炸弹略大")]
    public float oilRadius = 1.8f;
    [Tooltip("铁笼：关住多久（秒，之后自动打开）")]
    public float cageSeconds = 3f;
    [Tooltip("诱饵：每回合次数（G 键；S242：装备栏里有形态 = 丢一个假道具，伪装中也能丢）")]
    public int decoysPerRound = 2;
    [Tooltip("诱饵：存在多久（秒）")]
    public float decoySeconds = 6f;
    [Tooltip("诱饵：放下后往前走多久（秒）")]
    public float decoyWalkSeconds = 1.5f;
    [Tooltip("诱饵：马里奥离它多近会识破（格）")]
    public float decoyRevealDistance = 2.5f;
    [Tooltip("警报：每回合出现的概率")]
    [Range(0f, 1f)] public float alarmChance = 0.4f;
    [Tooltip("警报：最早第几秒开始")]
    public float alarmEarliest = 25f;
    [Tooltip("警报：最晚第几秒开始")]
    public float alarmLatest = 70f;
    [Tooltip("警报：持续多久（秒）")]
    public float alarmSeconds = 12f;
    [Tooltip("警报期间：静止的伪装被看见时的起疑速度 = 移动伪装速度 × 这个倍率")]
    [Range(0f, 1f)] public float alarmStillFactor = 0.35f;
    [Tooltip("马里奥踢开捷径门要多久（秒，0 = 不会踢门）")]
    public float doorKickSeconds = 2f;
    [Tooltip("H10 自动检查时也允许踢门（保证无人干预也能过）")]
    public bool doorKickInHandsOff = true;

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
    [Tooltip("草丛起风的最长间隔（秒）")]
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
    [Tooltip("开局镜头（C 键循环切换）：整屏 / 框住两人 / 跟随你 / 死亡细胞式智能跟随")]
    public Step1CameraMode cameraMode = Step1CameraMode.WholeRoom;
    [Tooltip("镜头四周多留几格边")]
    public float cameraPadding = 0.6f;
    [Tooltip("「框住两人」镜头最少看多少格高")]
    public float frameBothMinHeight = 9f;

    [Header("Big rooms (S207: Dead Cells-style follow camera)")]
    [Tooltip("S207：房间宽于这么多格时，开局镜头自动改为'大房间镜头'（默认房间 48 宽不受影响）")]
    public float maxWholeRoomWidth = 64f;
    [Tooltip("S207：房间太宽/太高时用哪种镜头（默认 智能跟随 = 跟着你走，马里奥靠近时自动拉远框住两人）")]
    public Step1CameraMode bigRoomCamera = Step1CameraMode.SmartFollow;
    [Tooltip("智能跟随：平时一屏看多少格高（12 = 和默认房间同样大小的角色）")]
    public float followViewHeight = 12f;
    [Tooltip("智能跟随：马里奥靠近时最多拉远到多少格高")]
    public float followMaxViewHeight = 18f;
    [Tooltip("智能跟随：马里奥离你多少格以内时镜头把他也框进来")]
    public float followIncludeMarioCells = 16f;
    [Tooltip("智能跟随：朝移动方向多看几格（死亡细胞式前瞻）")]
    public float followLookAhead = 3f;
    [Tooltip("智能跟随：上下死区（格）——小跳不晃镜头，跳上/掉下一层才跟")]
    public float followVerticalDeadZone = 1.5f;
    [Tooltip("马里奥在屏幕外时，屏幕边缘显示红箭头（带他头顶的 ? ! !! 和距离）")]
    public bool offscreenMarkers = true;
    [Tooltip("镜头看不到整个房间时，右上角显示小地图（你/马里奥/宝物/出口 + 当前镜头框）")]
    public bool miniMap = true;
    [Tooltip("回合时间下限 = 马里奥走完一趟的估算秒数 × 这个倍数（默认房间 20 秒 × 3 = 60 < 150，不变；长关卡自动放宽）")]
    public float roundTimePerRouteSecond = 3f;
    [Tooltip("自动检查超时下限 = 估算秒数 × 这个倍数 + 余量（防止长关卡被误判为'卡住'）")]
    public float handsOffTimePerRouteSecond = 2f;
    [Tooltip("自动检查超时余量（秒）")]
    public float handsOffTimeMargin = 20f;

    [Header("Overworld (S210: Stardew-style top-down town)")]
    [Tooltip("S210 大地图：马里奥走路速度（格/秒）。默认 3.4，比你慢一些——你能抄近路先到门口埋伏")]
    public float overworldMarioSpeed = 3.4f;
    [Tooltip("S210 大地图：你（捣蛋者）走路速度（格/秒）")]
    public float overworldTricksterSpeed = 5f;
    [Tooltip("S210 大地图：追你时马里奥的速度（格/秒）。比你慢一点点，能甩掉但要跑")]
    public float overworldChaseSpeed = 4.6f;
    [Tooltip("S210 大地图：现实 1 秒 = 游戏里几分钟（星露谷是 0.7 秒 1 分钟；这里 4 = 一天 06:00–22:00 约 4 分钟现实时间）")]
    public float overworldMinutesPerSecond = 4f;
    [Tooltip("S210 大地图：马里奥进一个房间算用掉多少游戏分钟（房间里打完回到大地图，钟跳过这么久）")]
    public float overworldVisitMinutes = 60f;
    [Tooltip("S210 大地图：他先进门后，你在这么多秒（现实秒）内跟进去算'迟到'（照样开打，但他没有开局等待）；超过 = 这扇门算他赢")]
    public float overworldLateWindowSeconds = 6f;
    [Tooltip("S210 大地图：视野距离（格）")]
    public float overworldVisionRange = 7f;
    [Tooltip("S210 大地图：晚上（19:00 后）视野距离（格）——路灯 3 格内照样看得远")]
    public float overworldNightVisionRange = 3.5f;
    [Tooltip("S210 大地图：视锥半角（度）")]
    [Range(20f, 90f)] public float overworldVisionHalfAngle = 55f;
    [Tooltip("S210 大地图：贴身这么近不看朝向也能察觉（仍需无遮挡）")]
    public float overworldNearSense = 1.2f;
    [Tooltip("S210 大地图：站在高草里，这么近才看得见")]
    public float overworldGrassSeeRadius = 1.5f;
    [Tooltip("S210 大地图：路灯照亮半径（格）")]
    public float overworldLampRadius = 3f;
    [Tooltip("S210 大地图：追人最长多少秒（H9：追不到就放弃，回到日程）")]
    public float overworldMaxChaseSeconds = 8f;
    [Tooltip("S210 大地图：踩到香蕉皮晕多久（秒，不超过 maxStunSeconds）")]
    public float overworldSlipStunSeconds = 1.5f;
    [Tooltip("S210 大地图：香蕉皮按 L 的距离（格）")]
    public float overworldPrankRange = 3f;
    [Tooltip("S210 大地图：被抓到后你在出生点定身几秒（代价：错过埋伏时机）")]
    public float overworldCaughtPenaltySeconds = 3f;
    [Tooltip("S210 大地图：挑衅 T 每天几次")]
    public int overworldTaunts = 3;
    [Tooltip("S213 大地图：他从房间出来后清点几秒（头上 '…'，不看不听），你有时间走开。0 = 一出门就能看见你（玩家模拟：反应慢的人每次出门都被抓）")]
    public float overworldExitGraceSeconds = 2.5f;
    [Tooltip("S213 大地图：他离门还有几格路时你才能按 E 埋伏。以前任何时候按 E 都算埋伏（玩家模拟：直奔门口按 E 就全胜，躲藏/伪装/香蕉皮都没用）。太大 = 太容易，太小 = 他离你太近容易被看见")]
    public int overworldAmbushSteps = 8;

    [Header("S218 小镇大机关 / 天气 / 小镇↔房间联动")]
    [Tooltip("S218：巨炮 / 滚石 / 水塔 按 L 后预警多久才发动（秒）。马里奥看得见预警（炮口闪、石头晃、水塔吱呀）")]
    public float overworldBigFuseSeconds = 1.2f;
    [Tooltip("S218：被巨炮轰飞落地 / 被滚石碾到 晕多久（秒，不超过 maxStunSeconds）")]
    public float overworldBigStunSeconds = 2f;
    [Tooltip("S218：滚石每秒滚几格")]
    public float overworldRollSpeed = 9f;
    [Tooltip("S218：炮声 / 滚石声 多远听得见（格）。听见 = 起疑一下（?），只知道声音在哪（H4）")]
    public float overworldNoiseRange = 14f;
    [Tooltip("S218：听见大机关的动静加多少起疑（35 = 刚好 '?' 停一下看）")]
    public float overworldNoiseSuspicion = 50f;
    [Tooltip("S218：被大机关砸中后这么多游戏分钟内进门 → 房间开局他还晕着")]
    public float overworldDazeCarryMinutes = 40f;
    [Tooltip("S218：带进房间的晕（开局多等几秒）")]
    public float overworldDazeCarrySeconds = 2f;
    [Tooltip("S218：雾天视野倍率")]
    [Range(0.3f, 1f)] public float overworldFogSight = 0.6f;
    [Tooltip("S218：他看见吃过亏的大机关在预警时，最多往旁边躲几格")]
    public int overworldDodgeSteps = 4;
    [Tooltip("S219：你钻进巨炮后最多待几秒（到点自动发射，H9 不会一直躲在炮里）")]
    public float overworldCannonSeatSeconds = 6f;
    [Tooltip("S219：按 L 发射后炮身抖几秒才发（落点早就画出来了，这是最后的预警 H3）")]
    public float overworldCannonFireSeconds = 0.6f;
    [Tooltip("S219：马里奥坐炮要瞄几秒（这段时间落点一直画在地上，你可以跑过去按 L 把炮管转走）")]
    public float overworldMarioCannonAimSeconds = 1.6f;
    [Tooltip("S219：坐炮比走路少走几格，马里奥才会去坐（他只看得见公开的炮和落点）")]
    public int overworldMarioCannonSaveSteps = 10;
    [Tooltip("S219：酸雨天马里奥打着伞，看得见平时的几成远")]
    [Range(0.3f, 1f)] public float overworldAcidSight = 0.7f;

    [Header("S220: 小镇的心 / 雷区 / 雷云")]
    [Tooltip("S220：雷区多久劈一轮（真实秒，按游戏时间换算，快进也一样）")]
    public float overworldStormVolleySeconds = 10f;
    [Tooltip("S220：闪电落下前地上闪多久（秒）。最后 0.3 秒变白 = 马上劈（两段预警：反应 0.25 秒 + 走出 1 格 0.5 秒刚好来得及）")]
    public float overworldBoltTelegraphSeconds = 1.2f;
    [Tooltip("S221：受伤后，站起来（晕完）之后还有几秒全无敌（身体闪烁；不掉心也不再晕）。保护期 = 晕的秒数 + 这个值，防止连控")]
    public float overworldHurtGraceSeconds = 1.5f;
    [Tooltip("S220：心掉光 = 晕倒几秒，然后剩 1 颗心站起来（只在关掉 overworldDeathEndsDay 时用；S229 起默认心掉光 = 这一天结束）")]
    public float overworldKoSeconds = 3f;
    [Tooltip("S220：雷云（能量满按 Q）持续几秒")]
    public float overworldCloudSeconds = 9f;
    [Tooltip("S220：雷云半径（格）。雷云停在召唤的地方，不跟着你走——你得自己逃出来")]
    public float overworldCloudRadius = 3f;
    [Tooltip("S220：雷云每隔几秒劈一轮（每轮 2 道：一道冲着云里的马里奥，一道随机——可能劈到你自己）")]
    public float overworldCloudVolleySeconds = 2.5f;
    [Tooltip("S220：马里奥带进房间至少几颗心（小镇里被打得再惨，房间里也要能打）")]
    public int overworldRoomHeartFloor = 2;

    [Header("S224: 少等待 + 可读性（总方案阶段 C）")]
    [Tooltip("S224：小镇里你多少秒（真实秒）不碰键盘就自动快进 ×4（只在没事发生时；他起疑 / 快到门口 / 附近有闪电立刻恢复）。0 = 关（只能按住空格）。实测：不快进时一天 79% 时间在干等")]
    public float overworldAutoFastIdleSeconds = 1.5f;
    [Tooltip("S224：视锥里按起疑程度灌黄色（Shadow Tactics：灌到你 = 被发现）。只是把已有的起疑值画出来，不改规则")]
    public bool visionConeFill = true;
    [Tooltip("S224：马里奥听得见的声音画成圈（Mark of the Ninja）：圈的大小 = 他真实的听力范围")]
    public bool soundRings = true;
    [Tooltip("S226 E9 底部按键条只显示现在能用的键（核心键 + 最多 3 个能力键）。关掉 = 以前那一整行全部按键")]
    public bool contextKeyBar = true;
    [Tooltip("S226 E7 无障碍：房间游戏速度 0.5~1（1 = 正常；0.7 = 慢三成，反应慢/手不方便时用）。只影响房间玩法，不影响暂停/回放")]
    [Range(0.5f, 1f)] public float roomGameSpeed = 1f;

    [Header("S228: 按调研定的数值 + 钟楼 + 房间大炮轰出窗户")]
    [Tooltip("S228：你自己踩到绳套被吊几秒（马里奥仍是 snareSeconds）。宪法 P4：10 秒没事可做 = 死区；以前你也吊 10 秒。参考 DbD 捕兽夹：设陷阱的人自己踩到只是短暂定住")]
    public float snareSelfSeconds = 3f;
    [Tooltip("S235：你掉出房间（被炮/炸弹轰出去、挤出外墙）→ 回出生点并掉几条命（无敌期内不掉）。0 = 只回出生点不罚。以前掉出去就回不来、命不掉、马里奥照样跑")]
    [Range(0, 3)] public int fallOutLivesLost = 1;
    [Tooltip("S228：他在房间里挨了你的炮 → 回到小镇时从门口被轰出去几格（落地晕 overworldBigStunSeconds，不掉心）")]
    public int overworldWindowFlingCells = 6;
    [Tooltip("S229：小镇里心掉光 = 这一天立刻结束（他掉光 = 你赢，你掉光 = 他赢，同一下都掉光 = 平局）→ 结算 → 重开。关掉 = 回到 S220 的'晕倒 3 秒剩 1 颗'")]
    public bool overworldDeathEndsDay = true;
    [Tooltip("S229：有人被打死后，结算画面停几秒自动开始新的一天（按 R 立刻开始；0 = 不自动，只能按 R）")]
    public float overworldDeathRestartSeconds = 6f;

    [Header("S216: 手感 / 被弹飞的抛物线 / 特效")]
    [Tooltip("S216 被弹飞/打飞时的重力（格/秒²）。以前硬直期间往上飞没有重力 → 弹簧弹 11 格撞天花板、炸弹推 8 格像在月球。平时跳跃重力 80；这里略轻 → 有滞空感但仍是抛物线")]
    public float launchGravity = 40f;
    [Tooltip("S216 被弹飞时空中水平阻力（格/秒²）：飞得越久越慢，落点更好预判")]
    public float launchAirDrag = 2f;
    [Tooltip("S216 被打飞落地后的地面摩擦（格/秒²）：落地一小段就停，不会一直滑（香蕉皮除外）")]
    public float launchGroundFriction = 40f;
    [Tooltip("S216 被弹上天的人：计时到了还在空中就等落地再恢复控制，最多再等几秒（H9）")]
    public float launchLandGraceSeconds = 1.5f;
    [Tooltip("S216 受伤小跳（格/秒）：被火/刺/炮弹打中时至少往上弹这么快 → 约 0.45 格高、后退 1–2 格的'哎哟'小跳（马里奥系列的受伤反馈）")]
    public float hurtLift = 6f;
    [Tooltip("S216 炸弹/油桶把人往上掀的速度（格/秒）：有重力后约 0.8 格高的小跳，看得出'被炸飞'")]
    public float blastLift = 8f;
    [Tooltip("S216 受伤一瞬间红白闪的时长（秒），之后是原来的无敌闪烁")]
    public float hurtHitFlashSeconds = 0.18f;
    [Tooltip("S216 特效（冲击环、尘土、连锁火花线）开关。关掉只剩机关本身的闪烁（性能差的电脑用）")]
    public bool juiceFx = true;

    [Header("S240: 坐进大炮 · 用过的机关 · 连击看得懂 · 卡住记录")]
    [Tooltip("你坐进大炮按空格发射的速度（格/秒）。26 ≈ 40° 时飞 15 格远、3 格高；马里奥钻炮仍用旧速度")]
    public float tricksterCannonSpeed = 26f;
    [Tooltip("你从大炮飞出去后，多少秒能再钻进去（马里奥那边仍是 cannonLaunchCooldown）")]
    public float tricksterCannonCooldown = 1.5f;
    [Tooltip("坐在炮里最多几秒，到点自动发射（防止一直躲在炮里）")]
    public float tricksterCannonMaxSitSeconds = 8f;
    [Tooltip("本回合已经用光的机关（裂缝已碎、铁笼已落、次数用完）变灰，不再被选中")]
    public bool greySpentProps = true;
    [Tooltip("马里奥这么多秒到目标都没有进展（中间被晕、东张西望也算进去）就强制救出——总兜底")]
    public float stuckHardCapSeconds = 15f;
    [Tooltip("每次救援自动截一张图 + 记下位置（工坊'检查轨迹'里用红叉标出）")]
    public bool stuckAutoReport = true;

    [Header("S241: 机关预约 · 光影 · 遁地 · 蛛丝 · 图标")]
    [Tooltip("按 L 时他还没走到：机关先静静等着（不闪、不暴露），他走进预判区才真正发动。最多等几秒，等不到就作废并退还次数和能量。0 = 关掉（按下立刻发动）")]
    public float propArmSeconds = 5.5f;
    [Tooltip("预约时'走进预判区'的宽度（格）：越大越容易接上，越小越准。和连锁自动触发同一个判定")]
    public float propArmTolerance = 1.1f;
    [Tooltip("发动了但一下都没坑到他：冷却减半，并把这次的次数还给你（火、弹簧、香蕉皮这类；封路墙 / 塌桥 / 灯不退）")]
    public bool refundOnMiss = true;
    [Tooltip("房间光照：自动 = 第 1 局白天，之后 夜晚 → 雨天 → 雨夜 轮换；也可以固定一种")]
    public Step1LightMode lightMode = Step1LightMode.Auto;
    [Tooltip("夜里暗处蒙多黑（0–1，只是画面；你永远看得见全房间，他只看得见亮处）")]
    [Range(0f, 0.9f)] public float nightDarkness = 0.55f;
    [Tooltip("马里奥手电筒照多远（格），只在夜里打开")]
    public float flashlightRange = 6.5f;
    [Tooltip("手电筒半张角（度）")]
    public float flashlightHalfAngle = 28f;
    [Tooltip("房间里灯 'i' 照多远（格）")]
    public float lampRadius = 4f;
    [Tooltip("火 / 炸弹 / 点燃的油桶照多远（格）")]
    public float fireLightRadius = 2.5f;
    [Tooltip("暗处贴得多近他也能看见你（格）——贴身就藏不住")]
    public float darkSeeRadius = 1.2f;
    [Tooltip("你在他头顶这么近、而且那里亮着 → 他抬头察觉（格）。暗处荡过去不察觉")]
    public float overheadNoticeRadius = 2.5f;
    [Tooltip("你跑动的脚步声传多远（格；伪装 / 遁地 / 摆荡 / 站着没声音）。蓝圈 = 这个数")]
    public float footstepRadius = 3f;
    [Tooltip("下雨时脚步声打几折（0–1）")]
    [Range(0f, 1f)] public float rainHearingScale = 0.6f;
    [Tooltip("遁地：在地下最多几秒（到点自动钻出来）")]
    public float burrowMaxSeconds = 6.5f;
    [Tooltip("遁地：钻出来后多少秒能再钻")]
    public float burrowCooldown = 4f;
    [Tooltip("遁地时移动速度倍数")]
    public float burrowSpeedMultiplier = 0.8f;
    [Tooltip("遁地被他踩到 / 扫到逼出来：你晕几秒")]
    public float burrowFlushStunSeconds = 1f;
    [Tooltip("蛛丝：最远能射多远（格）")]
    public float silkRange = 8f;
    [Tooltip("蛛丝：最多荡几秒（到点自动松手）")]
    public float silkMaxSeconds = 7f;
    [Tooltip("蛛丝：松手后多少秒能再射")]
    public float silkCooldown = 1.5f;
    [Tooltip("蛛丝：↑↓ 收放线的速度（格/秒）")]
    public float silkReelSpeed = 3f;
    [Tooltip("蛛丝：←→ 打秋千的力度")]
    public float silkPump = 9f;
    [Tooltip("灯 'i'：按 L 灭灯多少秒（之后自己亮回来）")]
    public float lampOffSeconds = 8f;
    [Tooltip("机关头上的小图标（一眼认出是什么：火苗、弹簧、香蕉…）。关掉 = 只有色块")]
    public bool propIcons = true;
    [Tooltip("图例只列这个房间出现的东西；'可炸'等小标签只在你附近几格内显示（太多字看不过来）")]
    public float legendTagRadius = 5.5f;

    [Header("S242: 伪装装备栏 · 道具诱饵 · 一目了然 · 黑匣子")]
    [Tooltip("伪装装备栏：带几种形态（1–5，数字键 1–N 切换）。默认 = 这个房间里最多的 3 种东西")]
    [Range(1, 5)] public int disguiseLoadoutSize = 3;
    [Tooltip("伪装中换形态后多少秒内被他看见 = 算你动了（他会起疑）。0 = 换形态不会被发现")]
    public float shapeShiftTellSeconds = 0.5f;
    [Tooltip("道具诱饵：丢出去多远（格）")]
    public float propDecoyThrow = 2.5f;
    [Tooltip("道具诱饵：每隔几秒扭一下（扭的时候他看见会过来查看）")]
    public float propDecoyWriggleEvery = 1.6f;
    [Tooltip("作战图（M）：马里奥路线虚线 + 埋伏点 + 头顶意图图标。关掉 = 回到以前的文字图例")]
    public bool glanceMap = true;
    [Tooltip("平时（没按 M）也淡淡地显示马里奥接下来的路线")]
    public bool glanceRouteAlways = true;
    [Tooltip("作战图：最多标几个埋伏点（路线上马里奥最先经过的几个机关）")]
    [Range(0, 6)] public int glanceAmbushCount = 3;
    [Tooltip("黑匣子：记住最近多少秒（F8 或自动记录时一起存下）")]
    public float blackBoxSeconds = 25f;
    [Tooltip("黑匣子：每秒采样几次")]
    public float blackBoxHz = 4f;
    [Tooltip("黑匣子：一帧超过多少秒算顿卡（自动记一条）")]
    public float blackBoxHitchSeconds = 0.3f;
    [Tooltip("黑匣子：同一类问题最快多少秒记一次（防止刷屏）")]
    public float blackBoxCooldown = 30f;
    [Tooltip("黑匣子：一次试玩最多自动记几条")]
    public int blackBoxMaxAuto = 25;
    [Tooltip("反馈截图最宽多少像素（JPG）。越小包越小；0 = 原尺寸")]
    public int feedbackShotWidth = 960;
    [Tooltip("反馈包最大多少 MB（超了先丢最旧的截图）")]
    public float feedbackPackMB = 8f;

    [Header("S243: 美术皮肤（AI 生成的像素角色 · 机关 · 地形 · 背景）")]
    [Tooltip("角色换成像素小人：马里奥 = 红帽寻宝人、你 = 蓝色小恶魔（站 / 跑 / 跳 / 晕 会换帧）。关掉 = 回到红蓝方块")]
    public bool artCharacters = true;
    [Tooltip("机关 / 道具换成像素图（火、弹簧、油桶…整块替换色块，不再只是头上贴小图标）。关掉 = 色块 + 小图标")]
    public bool artProps = true;
    [Tooltip("地面 / 墙 / 单向板换成像素图块（最上层带草）")]
    public bool artTiles = true;
    [Tooltip("房间后面加一张低对比的像素背景（不画描边，不会被当成能站的地方）")]
    public bool artBackground = true;

    [Header("S244: 动作画面 · 节奏 · 暂停 · 存档")]
    [Tooltip("动作画面：跑步扬尘、落地扬尘 + 压扁回弹、马里奥晕倒头上转星星、变身冒烟（纯外观）")]
    public bool artJuice = true;
    [Tooltip("开局倒计时：马里奥出发前最后 3 秒屏幕中间大字 3 · 2 · 1 → 开始！")]
    public bool startCountdown = true;
    [Tooltip("横幅：他拿到宝 / 最后 10 秒时屏幕上方大字提示 1.6 秒")]
    public bool roundBanners = true;
    [Tooltip("震屏（爆炸、弹飞、撞墙时画面抖一下）。晕 3D / 不喜欢可以关")]
    public bool screenShake = true;
    [Tooltip("小镇自动存档：每 20 秒、从房间回来、天亮时各存一次；下次进小镇可以继续这一天")]
    public bool townAutoSave = true;

    [Header("S238: 扫描 · 能量 · 附身（以前在第二个调参文件 GameplayLoopConfig）")]
    [Tooltip("马里奥 Q 扫描：半径（格）。扫到就真的暴露你（宪法 H5：扫描 100% 真实）")]
    [Range(0.5f, 20f)] public float scanRadius = 5f;
    [Tooltip("马里奥 Q 扫描：冷却（秒）")]
    [Range(0f, 30f)] public float scanCooldown = 8f;
    [Tooltip("扫到你之后你身上红色标记持续多久（秒）")]
    [Range(0f, 10f)] public float scanRevealDuration = 2f;
    [Tooltip("扫到你之后额外多久不能再附身（秒）")]
    [Range(0f, 10f)] public float scanRevealGateBonusDuration = 1.2f;
    [Tooltip("扫描圈扩散速度（格/秒，只是画面）")]
    [Range(1f, 40f)] public float scanPulseSpeed = 15f;
    [Tooltip("扫描圈线宽（只是画面）")]
    [Range(0.01f, 1f)] public float scanPulseLineWidth = 0.15f;
    [Tooltip("被扫到时闪烁频率（次/秒，只是画面）")]
    [Range(0.1f, 30f)] public float scanFlashFrequency = 6f;
    [Tooltip("被扫到时的标记颜色")]
    public Color scanRevealColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    [Tooltip("你的能量上限")]
    [Range(1f, 300f)] public float energyMaxEnergy = 100f;
    [Tooltip("开局能量（-1 = 满）")]
    [Range(-1f, 300f)] public float energyStartEnergy = -1f;
    [Tooltip("伪装一次花多少能量")]
    [Range(0f, 100f)] public float energyDisguiseCost = 20f;
    [Tooltip("伪装着每秒花多少能量")]
    [Range(0f, 50f)] public float energyDisguiseDrainPerSecond = 5f;
    [Tooltip("完全融入场景后，伪装耗能打几折")]
    [Range(0f, 2f)] public float energyBlendedDrainMultiplier = 0.5f;
    [Tooltip("按 L 发动机关一次花多少能量")]
    [Range(0f, 100f)] public float energyControlCost = 15f;
    [Tooltip("没伪装时每秒回多少能量")]
    [Range(0f, 50f)] public float energyRegenPerSecond = 8f;
    [Tooltip("伪装时回能量的倍率（0 = 伪装时不回）")]
    [Range(0f, 2f)] public float energyDisguisedRegenMultiplier = 0f;
    [Tooltip("发动机关后多久才开始回能量（秒）")]
    [Range(0f, 10f)] public float energyRegenDelayAfterControl = 2f;
    [Tooltip("能量低于这个比例时提示能量不够")]
    [Range(0f, 1f)] public float energyLowEnergyThreshold = 0.25f;
    [Tooltip("发动机关后你暴露多久（秒）")]
    [Range(0f, 10f)] public float possessionRevealDuration = 0.8f;
    [Tooltip("暴露结束后多久才能再附身（秒）")]
    [Range(0f, 5f)] public float possessionEscapeDuration = 0.35f;

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
        if (dataVersion < 16)
        {
            overworldMarioSpeed = 3.4f; overworldTricksterSpeed = 5f; overworldChaseSpeed = 4.6f; overworldMinutesPerSecond = 4f; overworldVisitMinutes = 60f;
            overworldLateWindowSeconds = 6f; overworldVisionRange = 7f; overworldNightVisionRange = 3.5f; overworldVisionHalfAngle = 55f; overworldNearSense = 1.2f;
            overworldGrassSeeRadius = 1.5f; overworldLampRadius = 3f; overworldMaxChaseSeconds = 8f; overworldSlipStunSeconds = 1.5f; overworldPrankRange = 3f;
            overworldCaughtPenaltySeconds = 3f; overworldTaunts = 3;
        }
        if (dataVersion < 17)
        {
            overworldExitGraceSeconds = 2.5f; overworldAmbushSteps = 8;
        }
        if (dataVersion < 15)
        {
            maxWholeRoomWidth = 64f; bigRoomCamera = Step1CameraMode.SmartFollow; followViewHeight = 12f; followMaxViewHeight = 18f;
            followIncludeMarioCells = 16f; followLookAhead = 3f; followVerticalDeadZone = 1.5f; offscreenMarkers = true; miniMap = true;
            roundTimePerRouteSecond = 3f; handsOffTimePerRouteSecond = 2f; handsOffTimeMargin = 20f;
        }
        if (dataVersion < 14)
        {
            marioPersonality = Step1PersonalityChoice.Random; rushWeight = cautiousWeight = greedyWeight = 1f; personalityIntroSeconds = 3f;
            cautiousTypeSpeedScale = 0.9f; cautiousTypeSuspicionScale = 1.25f; avoidRadius = 2f; greedyPickupDetourCells = 14f; greedyGiveUpSeconds = 5f; greedySuspicionScale = 0.9f;
            autoCheckCyclePersonalities = true;
        }
        if (dataVersion < 13)
        {
            replayEnabled = true; replaySeconds = 4f; replaySpeed = 0.35f; replayMinCombo = 4; replayCooldown = 12f; reinforceAgainstBombs = true; probeUsesPerTrap = 1;
            dodgeVisibleDanger = true; dodgeMargin = 0.8f; pickupDetourCells = 4f;
        }
        if (dataVersion < 12)
        {
            maxChainLinks = 4; chainArmRange = 2.5f; chainAutoRange = 10f; chainLiveSeconds = 6f; chainFireTolerance = 0.8f; chainCannonRange = 6f; chainHitstopSeconds = 0.06f;
            tauntUses = 3; tauntCooldown = 8f; tauntSuspicion = 70f; tripStunSeconds = 0.4f; learnFromHurt = true; cautiousRadius = 3f; cautiousSpeedScale = 0.7f;
        }
        if (dataVersion < 11)
        {
            oilFuseSeconds = 0.8f; oilRadius = 1.8f; cageSeconds = 3f; decoysPerRound = 1; decoySeconds = 6f; decoyWalkSeconds = 1.5f; decoyRevealDistance = 2.5f;
            alarmChance = 0.4f; alarmEarliest = 25f; alarmLatest = 70f; alarmSeconds = 12f; alarmStillFactor = 0.35f; doorKickSeconds = 2f; doorKickInHandsOff = true;
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
        // S216 放最后：比它旧的块会写旧的 springLaunchSpeed，这里要最后覆盖
        if (dataVersion < 18)
        {
            launchGravity = 40f; launchAirDrag = 2f; launchGroundFriction = 40f; launchLandGraceSeconds = 1.5f;
            springLaunchSpeed = 15f; hurtLift = 6f; blastLift = 8f; hurtHitFlashSeconds = 0.18f; juiceFx = true;
        }
        if (dataVersion < 19)
        {
            overworldBigFuseSeconds = 1.2f; overworldBigStunSeconds = 2f; overworldRollSpeed = 9f; overworldNoiseRange = 14f; overworldNoiseSuspicion = 35f;
            overworldDazeCarryMinutes = 40f; overworldDazeCarrySeconds = 2f; overworldFogSight = 0.6f; overworldDodgeSteps = 4;
        }
        if (dataVersion < 20)
        {
            overworldCannonSeatSeconds = 6f; overworldCannonFireSeconds = 0.6f; overworldMarioCannonAimSeconds = 1.6f; overworldMarioCannonSaveSteps = 10; overworldAcidSight = 0.7f;
        }
        if (dataVersion < 21)
        {
            overworldStormVolleySeconds = 10f; overworldBoltTelegraphSeconds = 1.2f; overworldHurtGraceSeconds = 2.5f; overworldKoSeconds = 3f;
            overworldCloudSeconds = 9f; overworldCloudRadius = 3f; overworldCloudVolleySeconds = 2.5f; overworldRoomHeartFloor = 2;
        }
        if (dataVersion < 22)
        {
            overworldHurtGraceSeconds = 1.5f; // S221：含义改为"站起来后还护几秒"（总保护 = 晕 2 + 1.5 = 3.5 秒；掉光 = 3 + 1.5 = 4.5 秒）
        }
        if (dataVersion < 23)
        {
            overworldAutoFastIdleSeconds = 1.5f; visionConeFill = true; soundRings = true; // S224：少等待 + 视锥灌注 + 声音圈
        }
        if (dataVersion < 24)
        {
            contextKeyBar = true; roomGameSpeed = 1f; // S226：看情况的按键条 + 游戏速度（默认不变）
        }
        if (dataVersion < 25)
        {
            // S228（调研定值，见 docs/step1/S228_RESEARCHED_NUMBERS_BELL_WINDOW.md）：
            overworldNoiseSuspicion = 50f; // 35 = 刚好到 ? 又被同帧衰减抹掉 → 改成"到 ? 后还能看 ≈1 秒"：35 + 14×1
            minOmenSeconds = 0.45f;        // ? 到 ! 至少 0.45 秒 ≥ 二选一反应时间 ≈0.44 秒（Card/Moran/Newell 常数 + Hick 定律）
            reactionDelay = 0.2f;          // 马里奥躲机关的反应 ≥ 人的视觉简单反应 ≈0.19 秒（AI 不比人快）
            snareSelfSeconds = 3f; overworldWindowFlingCells = 6;
        }
        if (dataVersion < 26)
        {
            // S229：心掉光 = 结算重开（docs/step1/S229_DEATH_ENDS_DAY.md）。自动重开 6 秒 = 看清结算（≈3 行字）+ 不用伸手按键。
            overworldDeathEndsDay = true; overworldDeathRestartSeconds = 6f;
        }
        if (dataVersion < 27)
        {
            // S235：掉出房间 = 回出生点 + 掉 1 条命（docs/step1/S235_FALL_OUT_OF_ROOM.md）。和"自己的炸弹炸到自己"同一个代价：是你自己的失误，但不该一下就输。
            fallOutLivesLost = 1;
        }
        if (dataVersion < 28)
        {
            // S238：① 性格两个设置合成一个下拉（旧值原样换算）② 扫描 / 能量 / 附身 20 个数值从 GameplayLoopConfig 搬进来——新字段的默认值 = 旧文件里的值，不用写。
            marioPersonality = FromOld(personalitiesEnabled, fixedPersonality);
        }
        if (dataVersion < 29)
        {
            // S240：坐进大炮自己发射（docs/step1/S240_CANNON_SEAT_SPENT_PROPS_COMBO_STUCK.md）——新字段的默认值就是要的值，不用写。
            tricksterCannonSpeed = 26f; tricksterCannonCooldown = 1.5f;
        }
        if (dataVersion < 30)
        {
            // S241：机关预约 / 光影 / 遁地 / 蛛丝（docs/step1/S241_FORGIVING_PROPS_LIGHT_BURROW_SILK.md）——新字段的默认值就是要的值，写一遍防旧资产读成 0。
            propArmSeconds = 5.5f; propArmTolerance = 1.1f; refundOnMiss = true; nightDarkness = 0.55f; flashlightRange = 6.5f; flashlightHalfAngle = 28f;
            lampRadius = 4f; fireLightRadius = 2.5f; darkSeeRadius = 1.2f; overheadNoticeRadius = 2.5f; footstepRadius = 3f; rainHearingScale = 0.6f;
            burrowMaxSeconds = 6.5f; burrowCooldown = 4f; burrowSpeedMultiplier = 0.8f; burrowFlushStunSeconds = 1f;
            silkRange = 8f; silkMaxSeconds = 7f; silkCooldown = 1.5f; silkReelSpeed = 3f; silkPump = 9f; lampOffSeconds = 8f; propIcons = true; legendTagRadius = 5.5f;
        }
        if (dataVersion < 31)
        {
            // S242：伪装装备栏 / 道具诱饵 / 一目了然 / 黑匣子（docs/step1/S242_LOADOUT_PROP_DECOY_GLANCE_BLACKBOX.md）——诱饵每局 1 → 2（道具诱饵伪装中也能丢）。
            decoysPerRound = Mathf.Max(decoysPerRound, 2);
            disguiseLoadoutSize = 3; shapeShiftTellSeconds = 0.5f; propDecoyThrow = 2.5f; propDecoyWriggleEvery = 1.6f; glanceMap = true; glanceRouteAlways = true; glanceAmbushCount = 3;
            blackBoxSeconds = 25f; blackBoxHz = 4f; blackBoxHitchSeconds = 0.3f; blackBoxCooldown = 30f; blackBoxMaxAuto = 25; feedbackShotWidth = 960; feedbackPackMB = 8f;
        }
        if (dataVersion < 32)
        {
            // S243：美术皮肤（docs/step1/S243_ART_SKIN.md）——默认全开，旧资产读成 false 时补上。
            artCharacters = true; artProps = true; artTiles = true; artBackground = true;
        }
        if (dataVersion < 33)
        {
            // S244：动作画面 / 开局倒计时 / 横幅 / 震屏开关 / 小镇自动存档（docs/step1/S244_SELF_CHECK_ART_RHYTHM_PAUSE_SAVE.md）——默认全开。
            artJuice = true; startCountdown = true; roundBanners = true; screenShake = true; townAutoSave = true;
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

/// <summary>S238：马里奥性格下拉（随机 + 三种）。新值只能加在末尾（资产按数字存）。</summary>
public enum Step1PersonalityChoice { [InspectorName("随机（按权重）")] Random, [InspectorName("冲冲型")] Rush, [InspectorName("谨慎型")] Cautious, [InspectorName("贪财型")] Greedy }

/// <summary>第 1 步摄像机：单房间默认整屏（《地狱邻居》式），玩家同时看得到自己和马里奥。
/// S207：SmartFollow = 死亡细胞式跟随（前瞻 + 上下死区，马里奥靠近时自动拉远框住两人）；宽/高房间自动使用。新值只能加在末尾（资产按数字存）。</summary>
public enum Step1CameraMode { WholeRoom, FrameBoth, FollowTrickster, SmartFollow }
