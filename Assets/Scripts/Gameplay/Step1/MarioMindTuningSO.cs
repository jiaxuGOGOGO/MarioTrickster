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

    [Header("Identity")]
    public string personaName = "Rush";

    [Header("Movement persona (feeds the existing HeuristicBot)")]
    [Range(0f, 1.5f)] public float reactionDelay = 0.12f;
    [Range(0f, 1f)] public float riskTolerance = 0.8f;
    [Tooltip("开局站定几秒，给玩家就位时间")]
    public float startDelaySeconds = 2f;

    [Header("Vision (H4: cone + range + occlusion only)")]
    public float visionRange = 9f;
    [Range(5f, 90f)] public float visionHalfAngle = 50f;
    [Tooltip("贴身距离内不看朝向也能察觉（仍需无遮挡）")]
    public float nearSenseRadius = 0.75f;
    public float eyeHeight = 0.3f;
    public bool showVisionCone = true;

    [Header("Suspicion sources")]
    [Tooltip("看见未伪装的捣蛋者：每秒增加")]
    public float seeTricksterPerSecond = 150f;
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
    public float arriveDistance = 0.8f;
    public float searchSeconds = 2.5f;
    public float afterSearchSuspicion = 30f;
    [Tooltip("裁判抓捕距离（中心距）。台面高 1 格，略大于 1 以便跳上台可抓")]
    public float catchRadius = 1.1f;
    public float hurtFlashSeconds = 0.8f;
    public float celebrateSeconds = 1.2f;

    [Header("Rules")]
    public int startingLives = 3;
    public float respawnInvulnerableSeconds = 2f;
    [Tooltip("机关触发后多少秒内马里奥受伤，算作这次恶作剧命中")]
    public float prankAttributionSeconds = 2.5f;
    public float roundTimeLimit = 150f;

    [Header("Playtest tools (S181)")]
    [Tooltip("菜单 Hands-off check：连续自动跑几局（宪法 H10：无干预时马里奥应能自己通关）")]
    public int autoCheckRounds = 5;
    [Tooltip("自动检查时的时间倍速（只影响检查，不影响正常试玩）。默认 1 = 与真实试玩相同的物理与决策节奏")]
    public float autoCheckTimeScale = 1f;
    [Tooltip("自动检查每局结束后停留多久（真实秒）再开下一局")]
    public float autoCheckRoundGapSeconds = 1.5f;
    [Tooltip("自动检查每局最多等多少秒；超时 = 马里奥卡住（记下卡住的位置）")]
    public float autoCheckRoundTimeoutSeconds = 45f;

    [Header("Playtest screen (S182)")]
    [Tooltip("每次进入 Play 先显示玩法说明并暂停，按任意键开始")]
    public bool showHelpOnStart = true;

    [Header("Camera")]
    public Step1CameraMode cameraMode = Step1CameraMode.WholeRoom;
    public float cameraPadding = 0.6f;
    public float frameBothMinHeight = 9f;

    public static MarioMindTuningSO LoadOrDefault()
    {
        var tuning = Resources.Load<MarioMindTuningSO>(ResourcePath);
        return tuning != null ? tuning : CreateInstance<MarioMindTuningSO>();
    }
}

/// <summary>第 1 步摄像机：单房间默认整屏（《地狱邻居》式），玩家同时看得到自己和马里奥。</summary>
public enum Step1CameraMode { WholeRoom, FrameBoth, FollowTrickster }
