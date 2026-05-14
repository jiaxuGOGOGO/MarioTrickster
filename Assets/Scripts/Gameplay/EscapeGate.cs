using System;
using UnityEngine;

/// <summary>
/// Commit 5：撤离门 — Mario 携带目标物时触碰即通关。
///
/// 核心规则（S130 第三阶段）：
///   - Mario 触碰 EscapeGate 时检查 LootObjective.IsLootCarried。
///   - 已拿目标 → 调用 GameManager.EndRound("Mario")。
///   - 未拿目标 → 显示提示"还没拿目标物"，不通关。
///   - 可选：Trickster 在 Alert/Lockdown 时可短暂"封锁"出口（增加通过延迟）。
///   - 回合重置时恢复为可用状态。
///
/// 关卡设计意义：
///   - 与 LootObjective 配合形成"进入→拿宝→撤离"的路径结构。
///   - Trickster 在 Mario 返程时更想连续出手（热度自然升高）。
///   - Mario 产生"继续赶路 vs 停下来查可疑物 vs 绕路撤离"的判断。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EscapeGate : MonoBehaviour
{
    // ─────────────────────────────────────────────────────
    #region 配置

    [Header("=== Commit 5 撤离门 ===")]
    [Tooltip("Alert 档时通过延迟（秒）— 模拟出口短暂封锁")]
    [SerializeField] private float alertPassDelay = 0.5f;

    [Tooltip("Lockdown 档时通过延迟（秒）")]
    [SerializeField] private float lockdownPassDelay = 1.5f;

    [Tooltip("未拿目标时的提示持续时间")]
    [SerializeField] private float hintDuration = 2f;

    [Tooltip("是否输出调试日志")]
    [SerializeField] private bool showDebugInfo = true;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 运行时状态

    private bool isActive = true;
    private float passDelayTimer;
    private bool marioInGate;
    private float hintTimer;
    private string hintMessage;

    // 引用
    private TricksterHeatMeter heatMeter;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 事件

    /// <summary>Mario 成功撤离时触发</summary>
    public static event Action OnEscapeSuccess;

    /// <summary>Mario 尝试撤离但未携带目标物时触发</summary>
    public static event Action OnEscapeDenied;

    /// <summary>出口被封锁/解封时触发（isLocked）</summary>
    public static event Action<bool> OnGateLockChanged;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共属性

    public bool IsGateLocked => passDelayTimer > 0f;
    public float PassDelayRemaining => passDelayTimer;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Awake()
    {
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
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

    private void Update()
    {
        // 通过延迟倒计时
        if (passDelayTimer > 0f)
        {
            passDelayTimer -= Time.deltaTime;
            if (passDelayTimer <= 0f)
            {
                OnGateLockChanged?.Invoke(false);

                // 如果 Mario 仍在门内且有目标物，立即通关
                if (marioInGate && LootObjective.IsLootCarried)
                {
                    TriggerEscape();
                }
            }
        }

        // 提示倒计时
        if (hintTimer > 0f)
        {
            hintTimer -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        if (!IsMario(other)) return;

        marioInGate = true;
        AttemptEscape();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 处理 Mario 在门内时目标物状态变化的情况
        if (!isActive) return;
        if (!IsMario(other)) return;

        if (LootObjective.IsLootCarried && passDelayTimer <= 0f && marioInGate)
        {
            TriggerEscape();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsMario(other)) return;
        marioInGate = false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void AttemptEscape()
    {
        if (!LootObjective.IsLootCarried)
        {
            // 未拿目标物
            hintMessage = "Need to collect the objective first!";
            hintTimer = hintDuration;

            if (showDebugInfo)
            {
                Debug.Log("[EscapeGate] Mario tried to escape without loot!");
            }

            OnEscapeDenied?.Invoke();
            return;
        }

        // 检查热度封锁延迟
        float delay = GetPassDelay();
        if (delay > 0f)
        {
            passDelayTimer = delay;
            hintMessage = $"Gate locked! Wait {delay:F1}s...";
            hintTimer = delay + 0.5f;

            if (showDebugInfo)
            {
                Debug.Log($"[EscapeGate] Gate locked for {delay:F1}s due to heat tier.");
            }

            OnGateLockChanged?.Invoke(true);
            return;
        }

        // 直接通关
        TriggerEscape();
    }

    private void TriggerEscape()
    {
        if (!isActive) return;
        isActive = false;

        if (showDebugInfo)
        {
            Debug.Log("[EscapeGate] Mario escaped with loot! Round won!");
        }

        OnEscapeSuccess?.Invoke();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMarioReachedGoal();
        }
    }

    private float GetPassDelay()
    {
        if (heatMeter == null) return 0f;

        switch (heatMeter.CurrentTier)
        {
            case TricksterHeatMeter.HeatTier.Alert:
                return alertPassDelay;
            case TricksterHeatMeter.HeatTier.Lockdown:
                return lockdownPassDelay;
            default:
                return 0f;
        }
    }

    private void HandleRoundStart()
    {
        isActive = true;
        passDelayTimer = 0f;
        marioInGate = false;
        hintTimer = 0f;
    }

    private bool IsMario(Collider2D other)
    {
        if (other.CompareTag("Player")) return true;
        if (other.GetComponent<MarioController>() != null) return true;
        if (other.transform.parent != null &&
            other.transform.parent.GetComponent<MarioController>() != null) return true;
        if (other.name.Contains("Mario")) return true;
        return false;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region Gizmo & HUD

    private void OnDrawGizmos()
    {
        Gizmos.color = isActive ? Color.green : Color.gray;
        Gizmos.DrawWireCube(transform.position, new Vector3(1.5f, 2f, 0f));

#if UNITY_EDITOR
        string label = isActive ? "[ESCAPE GATE]" : "[GATE USED]";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, label);
#endif
    }

    private void OnGUI()
    {
        if (hintTimer <= 0f) return;

        float alpha = Mathf.Clamp01(hintTimer / hintDuration);
        GUI.color = new Color(1f, 1f, 1f, alpha);

        GUIStyle style = GUI.skin.label;
        style.fontSize = 18;
        style.alignment = TextAnchor.MiddleCenter;

        float w = 300f;
        float h = 30f;
        Rect rect = new Rect(Screen.width * 0.5f - w * 0.5f,
                             Screen.height * 0.7f, w, h);

        // 背景
        GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);

        // 文字
        GUI.color = IsGateLocked ?
            new Color(1f, 0.3f, 0.3f, alpha) :
            new Color(1f, 0.8f, 0.2f, alpha);
        GUI.Label(rect, hintMessage, style);

        GUI.color = Color.white;
    }

    #endregion
}
