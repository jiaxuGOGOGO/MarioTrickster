using System;
using UnityEngine;

/// <summary>
/// Commit 5：拢宝目标 — Mario 触碰后获得"目标物已携带"状态。
///
/// 核心规则（S130 第三阶段）：
///   - Mario 触碰 LootObjective 后标记为已收集，发出 OnLootCollected。
///   - 收集后物体可选择隐藏或播放收集动画（灰盒阶段直接隐藏）。
///   - 不修改 GoalZone，与 EscapeGate 配合形成两步胜利。
///   - 回合重置时恢复为未收集状态。
///
/// 关卡设计意义：
///   - 形成路径结构：Mario 先进危险区拿宝，再带着目标回到出口。
///   - Trickster 要提前选择藏身点，干扰 Mario 的进入/撤退路线。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LootObjective : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 5 拢宝目标 ===")]
    [Tooltip("目标物名称（用于 UI 显示）")]
    [SerializeField] private string lootName = "Star Fragment";

    [Tooltip("收集后是否隐藏物体")]
    [SerializeField] private bool hideOnCollect = true;

    [Tooltip("收集后给 Trickster 增加的热度（Mario 拿到宝后 Trickster 压力增大）")]
    [SerializeField] private float heatOnCollect = 10f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = true;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private bool isCollected;
    private SpriteRenderer spriteRenderer;
    private TricksterHeatMeter heatMeter;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件

    /// <summary>Mario 收集目标物时触发</summary>
    public static event Action OnLootCollected;

    /// <summary>目标物重置时触发</summary>
    public static event Action OnLootReset;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共属性

    /// <summary>是否已被收集</summary>
    public static bool IsLootCarried { get; private set; }

    public string LootName => lootName;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        IsLootCarried = false;
        isCollected = false;
        heatMeter = FindObjectOfType<TricksterHeatMeter>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart += HandleRoundStart;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStart -= HandleRoundStart;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        // 检查是否是 Mario
        if (!IsMario(other)) return;

        Collect();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void Collect()
    {
        isCollected = true;
        IsLootCarried = true;

        if (hideOnCollect && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        // 给 Trickster 增加热度压力
        if (heatMeter != null && heatOnCollect > 0f)
        {
            heatMeter.AddHeat(heatOnCollect);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[LootObjective] Mario collected '{lootName}'! Escape gate now active.");
        }

        OnLootCollected?.Invoke();
    }

    private void HandleRoundStart()
    {
        isCollected = false;
        IsLootCarried = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        OnLootReset?.Invoke();
    }

    private bool IsMario(Collider2D other)
    {
        // 多重检测：tag、组件、父级组件、名称
        if (other.CompareTag("Player")) return true;
        if (other.GetComponent<MarioController>() != null) return true;
        if (other.transform.parent != null &&
            other.transform.parent.GetComponent<MarioController>() != null) return true;
        if (other.name.Contains("Mario")) return true;
        return false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Gizmo

    private void OnDrawGizmos()
    {
        Gizmos.color = isCollected ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // 标签
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f,
            isCollected ? $"[{lootName}] COLLECTED" : $"[{lootName}]");
#endif
    }

    #endregion
}
