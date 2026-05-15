using UnityEngine;

/// <summary>
/// Commit 3：连锁 HUD — 灰盒阶段的 OnGUI 显示。
///
/// 显示内容：
///   - 当前 Chain 数和倍率
///   - 连锁窗口剩余时间（进度条）
///   - 不同锚点/不同机关的加成提示
///   - 连锁断裂时的短暂提示
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class PropComboHUD : MonoBehaviour
{
    [Header("=== Commit 3 连锁 HUD ===")]
    [Tooltip("连锁断裂提示持续时间")]
    [SerializeField] private float breakMessageDuration = 1.5f;

    // ── 引用 ──
    private PropComboTracker comboTracker;

    // ── 状态 ──
    private float breakMessageTimer;
    private int lastBreakCount;
    private float lastBreakMultiplier;
    private string lastHitMessage;
    private float hitMessageTimer;

    private void Start()
    {
        comboTracker = FindObjectOfType<PropComboTracker>();

        if (comboTracker != null)
        {
            comboTracker.OnComboBreak += HandleComboBreak;
            comboTracker.OnComboHit += HandleComboHit;
        }
    }

    private void OnDestroy()
    {
        if (comboTracker != null)
        {
            comboTracker.OnComboBreak -= HandleComboBreak;
            comboTracker.OnComboHit -= HandleComboHit;
        }
    }

    private void Update()
    {
        if (breakMessageTimer > 0f)
        {
            breakMessageTimer -= Time.deltaTime;
        }

        if (hitMessageTimer > 0f)
        {
            hitMessageTimer -= Time.deltaTime;
        }
    }


    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleComboBreak(int count, float multiplier)
    {
        lastBreakCount = count;
        lastBreakMultiplier = multiplier;
        breakMessageTimer = breakMessageDuration;
    }

    private void HandleComboHit(int count, float hitMult, bool diffAnchor, bool diffProp)
    {
        if (count <= 1)
        {
            lastHitMessage = "Chain started!";
        }
        else if (diffAnchor && diffProp)
        {
            lastHitMessage = "Different anchor + prop!";
        }
        else if (diffAnchor)
        {
            lastHitMessage = "Different anchor!";
        }
        else if (diffProp)
        {
            lastHitMessage = "Different prop!";
        }
        else
        {
            lastHitMessage = "Same point... suspicion rising!";
        }

        hitMessageTimer = 1.2f;
    }

    #endregion
}
