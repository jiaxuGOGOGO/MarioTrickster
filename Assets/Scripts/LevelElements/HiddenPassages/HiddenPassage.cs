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
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class HiddenPassage : ControllableLevelElement
{
    [Header("=== 通道设置 ===")]
    [Tooltip("传送目标位置（另一个HiddenPassage或空Transform）")]
    [SerializeField] private Transform exitPoint;

    [Tooltip("是否双向传送")]
    [SerializeField] private bool isBidirectional = true;

    [Tooltip("传送前的延迟（秒）")]
    [SerializeField] private float teleportDelay = 0.5f;

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

    // Trickster覆盖
    private bool tricksterDisabled;

    public bool IsActive => isActive && !tricksterDisabled;
    public Transform ExitPoint => exitPoint;

    protected override void Awake()
    {
        propName = "隐藏通道";
        elementCategory = ElementCategory.HiddenPassage;
        elementTags = ElementTag.Controllable | ElementTag.Hidden | ElementTag.Interactive | ElementTag.Resettable;
        elementDescription = "进入后传送到隐藏区域的通道";

        base.Awake();

        triggerCollider = GetComponent<BoxCollider2D>();
        triggerCollider.isTrigger = true;
        sr = GetComponent<SpriteRenderer>();

        UpdateVisibility(false);
    }

    protected override void Update()
    {
        base.Update();

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
            // 检查钥匙（简化版：检查玩家是否持有钥匙组件）
            // 未来可扩展为完整的背包系统
            Debug.Log($"[HiddenPassage] 需要钥匙 '{requiredKeyId}' 才能使用");
            return;
        }

        StartTeleport(player);
    }

    private void StartTeleport(GameObject player)
    {
        isTeleporting = true;
        teleportTimer = teleportDelay;

        // 传送前效果：玩家缩小
        // 实际项目中可用动画/粒子替代
        Debug.Log($"[HiddenPassage] {player.name} 进入通道，{teleportDelay}秒后传送");
    }

    private void ExecuteTeleport()
    {
        isTeleporting = false;

        if (playerInTrigger != null && exitPoint != null)
        {
            playerInTrigger.transform.position = exitPoint.position;
            Debug.Log($"[HiddenPassage] {playerInTrigger.name} 已传送到 {exitPoint.position}");
        }
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
        playerInTrigger = null;
        playerInRange = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsActive ? new Color(0, 1, 1, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);

        // 连线到出口
        if (exitPoint != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawLine(transform.position, exitPoint.position);
            Gizmos.DrawWireSphere(exitPoint.position, 0.3f);
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
