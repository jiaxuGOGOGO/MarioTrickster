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
    public const int CurrentDataVersion = 4;
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
