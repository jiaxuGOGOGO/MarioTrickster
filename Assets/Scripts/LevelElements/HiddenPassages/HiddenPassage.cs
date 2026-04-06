using UnityEngine;

/// <summary>
/// 隐藏通道 - 关卡设计系统 · 隐藏通道类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: HiddenPassage | 标签: Controllable, Hidden, Interactive
/// 
/// 功能:
///   - 类似马里奥水管，玩家进入触发区后按交互键传送到目标位置
///   - 入口默认隐藏（可配置为可见/半透明提示）
///   - 支持双向传送（入口↔出口）
///   - Trickster操控: 临时关闭入口或改变传送目标
/// 
/// Session 19 优化（修复双向穿越）:
///   - 问题：之前只能从入口传送到出口，无法从出口传回入口
///   - 原因：exitPoint 只是一个 Transform，出口位置没有 HiddenPassage 组件
///   - 解决方案：
///     · 如果 exitPoint 上挂有另一个 HiddenPassage，自动建立双向配对
///     · 如果 exitPoint 只是普通 Transform，在出口位置动态创建触发区
///       允许玩家在出口按 S 键传回入口
///   - 新增 returnTrigger：在出口位置自动创建的返回触发区
///   - 新增 teleportCooldown：传送后的短暂冷却，防止来回弹跳
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HiddenPassage : ControllableLevelElement
{
    [Header("=== 通道设置 ===")]
    [Tooltip("传送目标位置（另一个HiddenPassage或空Transform）")]
    [SerializeField] private Transform exitPoint;

    [Tooltip("是否双向传送（允许从出口传回入口）")]
    [SerializeField] private bool isBidirectional = true;

    [Tooltip("传送前的延迟（秒）")]
    [SerializeField] private float teleportDelay = 0.5f;

    [Tooltip("传送后的冷却时间（秒），防止来回弹跳")]
    [SerializeField] private float teleportCooldown = 0.5f;

    [Header("=== 可见性设置 ===")]
    [Tooltip("入口默认可见性")]
    [SerializeField] private PassageVisibility visibility = PassageVisibility.Hidden;

    [Tooltip("玩家靠近时的提示距离")]
    [SerializeField] private float hintDistance = 2f;

    [Header("=== 交互设置 ===")]
    [Tooltip("需要钥匙才能使用")]
    [SerializeField] private bool requiresKey;

    [Tooltip("所需钥匙ID（空字符串=任意钥匙）")]
    [SerializeField] private string requiredKeyId = "";

    // 组件
    private BoxCollider2D triggerCollider;
    private SpriteRenderer sr;

    // 状态
    private bool isActive = true;
    private bool playerInRange;
    private GameObject playerInTrigger;
    private bool isTeleporting;
    private float teleportTimer;

    // Session 19: 双向穿越状态
    private bool isOnCooldown;
    private float cooldownTimer;
    private HiddenPassage pairedPassage;     // 配对的另一端 HiddenPassage
    private GameObject returnTriggerObj;      // 动态创建的返回触发区对象
    private bool playerAtReturn;             // 玩家是否在返回触发区内
    private GameObject playerAtReturnObj;    // 返回触发区内的玩家对象

    // Trickster覆盖
    private bool tricksterDisabled;

    public bool IsActive => isActive && !tricksterDisabled && !isOnCooldown;
    public Transform ExitPoint => exitPoint;

    /// <summary>获取配对通道（供返回触发区使用）</summary>
    public HiddenPassage PairedPassage => pairedPassage;

    protected override void Awake()
    {
        propName = "隐藏通道";
        elementCategory = ElementCategory.HiddenPassage;
        elementTags = ElementTag.Controllable | ElementTag.Hidden | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "进入后传送到隐藏区域的通道（支持双向）";

        base.Awake();

        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        sr = GetComponentInChildren<SpriteRenderer>();

        UpdateVisibility(false);
    }

    private void Start()
    {
        // Session 19: 建立双向配对
        if (isBidirectional && exitPoint != null)
        {
            SetupBidirectional();
        }
    }

    /// <summary>Session 19: 建立双向传送配对</summary>
    private void SetupBidirectional()
    {
        // 检查出口是否已经有 HiddenPassage 组件
        pairedPassage = exitPoint.GetComponent<HiddenPassage>();

        if (pairedPassage != null)
        {
            // 出口已有 HiddenPassage → 自动配对（双方互指）
            if (pairedPassage.exitPoint == null)
            {
                pairedPassage.exitPoint = this.transform;
            }
            // 确保对方也知道配对关系
            if (pairedPassage.pairedPassage == null)
            {
                pairedPassage.pairedPassage = this;
            }
            Debug.Log($"[HiddenPassage] {gameObject.name} ↔ {pairedPassage.gameObject.name} 双向配对完成");
        }
        else
        {
            // 出口只是普通 Transform → 动态创建返回触发区
            CreateReturnTrigger();
        }
    }

    /// <summary>Session 19: 在出口位置动态创建返回触发区</summary>
    private void CreateReturnTrigger()
    {
        returnTriggerObj = new GameObject($"{gameObject.name}_ReturnTrigger");
        returnTriggerObj.transform.position = exitPoint.position;
        returnTriggerObj.transform.SetParent(exitPoint); // 跟随出口位置

        // 添加触发区碰撞体（与入口大小相同）
        BoxCollider2D returnCol = returnTriggerObj.AddComponent<BoxCollider2D>();
        returnCol.isTrigger = true;
        returnCol.size = triggerCollider.size;

        // 添加返回触发区脚本
        HiddenPassageReturnTrigger returnScript = returnTriggerObj.AddComponent<HiddenPassageReturnTrigger>();
        returnScript.Initialize(this);

        Debug.Log($"[HiddenPassage] {gameObject.name} 在出口 {exitPoint.name} 创建了返回触发区");
    }

    protected override void Update()
    {
        base.Update();

        // 传送冷却
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
            }
        }

        // 可见性更新
        if (playerInRange && visibility == PassageVisibility.HintWhenNear)
        {
            UpdateVisibility(true);
        }
        else if (visibility != PassageVisibility.Visible)
        {
            UpdateVisibility(false);
        }

        // 传送延迟
        if (isTeleporting)
        {
            teleportTimer -= Time.deltaTime;
            if (teleportTimer <= 0f)
            {
                ExecuteTeleport();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<MarioController>() == null) return;
        playerInTrigger = other.gameObject;
        playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == playerInTrigger)
        {
            playerInTrigger = null;
            playerInRange = false;
        }
    }

    /// <summary>
    /// 玩家按下交互键时调用（由InputSystem或MarioController转发）
    /// </summary>
    public void TryEnterPassage(GameObject player)
    {
        if (!IsActive || isTeleporting || exitPoint == null) return;
        if (playerInTrigger != player) return;

        if (requiresKey)
        {
            Debug.Log($"[HiddenPassage] 需要钥匙 '{requiredKeyId}' 才能使用");
            return;
        }

        StartTeleport(player, exitPoint.position);
    }

    /// <summary>
    /// Session 19: 从返回触发区传送回入口
    /// </summary>
    public void TryReturnFromExit(GameObject player)
    {
        if (!isActive || isTeleporting || !isBidirectional) return;
        if (isOnCooldown) return;

        // 传送回入口位置
        StartTeleportReturn(player);
    }

    /// <summary>Session 19: 通知有玩家进入返回触发区</summary>
    public void OnPlayerEnterReturnZone(GameObject player)
    {
        playerAtReturn = true;
        playerAtReturnObj = player;
    }

    /// <summary>Session 19: 通知玩家离开返回触发区</summary>
    public void OnPlayerExitReturnZone(GameObject player)
    {
        if (playerAtReturnObj == player)
        {
            playerAtReturn = false;
            playerAtReturnObj = null;
        }
    }

    /// <summary>Session 19: 检查玩家是否在返回触发区内并尝试传送</summary>
    public bool TryReturnPassage(GameObject player)
    {
        if (!playerAtReturn || playerAtReturnObj != player) return false;
        if (!isActive || isTeleporting || !isBidirectional || isOnCooldown) return false;

        StartTeleportReturn(player);
        return true;
    }

    // 传送模式标记（区分正向/返回，避免状态判断歧义）
    private enum TeleportMode { None, Forward, Return }
    private TeleportMode currentTeleportMode = TeleportMode.None;
    private GameObject teleportTarget; // 待传送的玩家对象

    private void StartTeleport(GameObject player, Vector2 targetPos)
    {
        isTeleporting = true;
        teleportTimer = teleportDelay;
        currentTeleportMode = TeleportMode.Forward;
        teleportTarget = player;

        Debug.Log($"[HiddenPassage] {player.name} 进入通道，{teleportDelay}秒后传送到 {targetPos}");
    }

    private void StartTeleportReturn(GameObject player)
    {
        if (player == null) return;

        isTeleporting = true;
        teleportTimer = teleportDelay;
        currentTeleportMode = TeleportMode.Return;
        teleportTarget = player;

        Debug.Log($"[HiddenPassage] {player.name} 从出口返回，{teleportDelay}秒后传送回入口");
    }

    private void ExecuteTeleport()
    {
        isTeleporting = false;

        if (teleportTarget == null)
        {
            currentTeleportMode = TeleportMode.None;
            return;
        }

        switch (currentTeleportMode)
        {
            case TeleportMode.Forward:
                // 正向传送：入口 → 出口
                if (exitPoint != null)
                {
                    teleportTarget.transform.position = exitPoint.position;
                    Debug.Log($"[HiddenPassage] {teleportTarget.name} 已传送到出口 {exitPoint.position}");

                    StartCooldown();
                    if (pairedPassage != null) pairedPassage.StartCooldown();
                }
                break;

            case TeleportMode.Return:
                // 返回传送：出口 → 入口
                teleportTarget.transform.position = transform.position;
                Debug.Log($"[HiddenPassage] {teleportTarget.name} 已传送回入口 {transform.position}");

                StartCooldown();
                break;
        }

        // 清除传送状态
        currentTeleportMode = TeleportMode.None;
        teleportTarget = null;
        playerAtReturn = false;
        playerAtReturnObj = null;
    }

    /// <summary>Session 19: 启动传送冷却</summary>
    public void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = teleportCooldown;
    }

    private void UpdateVisibility(bool showHint)
    {
        if (sr == null) return;

        switch (visibility)
        {
            case PassageVisibility.Hidden:
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);
                break;
            case PassageVisibility.HintWhenNear:
                float alpha = showHint ? 0.4f : 0f;
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
                break;
            case PassageVisibility.Visible:
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
                break;
        }
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        // Trickster操控：关闭通道
        tricksterDisabled = true;
        Debug.Log($"[HiddenPassage] Trickster关闭了通道 {gameObject.name}");
    }

    protected override void OnActiveEnd()
    {
        tricksterDisabled = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        isActive = true;
        tricksterDisabled = false;
        isTeleporting = false;
        isOnCooldown = false;
        cooldownTimer = 0f;
        playerInTrigger = null;
        playerInRange = false;
        playerAtReturn = false;
        playerAtReturnObj = null;
        currentTeleportMode = TeleportMode.None;
        teleportTarget = null;
    }

    private void OnDestroy()
    {
        // 清理动态创建的返回触发区
        if (returnTriggerObj != null)
        {
            Destroy(returnTriggerObj);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsActive ? new Color(0, 1, 1, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);

        // 连线到出口（双向箭头）
        if (exitPoint != null)
        {
            // 正向连线（实线）
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawLine(transform.position, exitPoint.position);
            Gizmos.DrawWireSphere(exitPoint.position, 0.3f);

            // Session 19: 双向标识（虚线效果用额外线段）
            if (isBidirectional)
            {
                Gizmos.color = new Color(1, 1, 0, 0.4f);
                Vector3 mid = (transform.position + exitPoint.position) * 0.5f;
                // 在中点画双向箭头标识
                Vector3 dir = (exitPoint.position - transform.position).normalized;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0) * 0.2f;
                Gizmos.DrawLine(mid - perp, mid + perp);
                Gizmos.DrawWireSphere(transform.position, 0.3f); // 入口也画圈
            }
        }

        // 提示范围
        if (visibility == PassageVisibility.HintWhenNear)
        {
            Gizmos.color = new Color(0, 1, 1, 0.1f);
            Gizmos.DrawWireSphere(transform.position, hintDistance);
        }
    }
}

public enum PassageVisibility
{
    Hidden,         // 完全隐藏
    HintWhenNear,   // 玩家靠近时半透明提示
    Visible         // 始终可见
}
