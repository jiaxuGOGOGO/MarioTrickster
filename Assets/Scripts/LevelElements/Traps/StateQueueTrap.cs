using UnityEngine;

/// <summary>
/// 公开下一状态机关 - Onitama 式原型 C。
///
/// 设计定位：机关自身按固定队列自动循环，并始终向 Mario 公开 Current / Next。
/// Trickster 的操控不再直接触发攻击，而是强行跳过当前队列状态；这种打乱规律的下注
/// 会给当前锚点追加高 Suspicion / Evidence / Heat，并让机关进入较长 Recovery 破绽期。
///
/// S53 薄层原则：只复用 ControllableLevelElement 状态、TextMesh、MarioSuspicionTracker
/// 与 TricksterHeatMeter，不引入复杂特效或新系统。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class StateQueueTrap : ControllableLevelElement
{
    private enum QueueTrapState
    {
        LeftAttack,
        RightAttack,
        SafePause
    }

    [Header("=== 队列设置 ===")]
    [Tooltip("左攻击持续时间。")]
    [SerializeField] private float leftAttackDuration = 1.25f;

    [Tooltip("右攻击持续时间。")]
    [SerializeField] private float rightAttackDuration = 1.25f;

    [Tooltip("安全停顿持续时间。")]
    [SerializeField] private float safePauseDuration = 0.9f;

    [Header("=== 公开信息 TextMesh ===")]
    [Tooltip("显示 Current / Next 的 TextMesh；留空则自动创建。")]
    [SerializeField] private TextMesh stateText;

    [Tooltip("状态文本相对机关的位置。")]
    [SerializeField] private Vector3 textLocalOffset = new Vector3(0f, 0.9f, 0f);

    [Header("=== 攻击判定 ===")]
    [Tooltip("攻击盒距离机关中心的横向偏移。")]
    [SerializeField] private float attackOffset = 0.85f;

    [Tooltip("攻击盒尺寸。")]
    [SerializeField] private Vector2 attackBoxSize = new Vector2(1.4f, 0.8f);

    [Tooltip("攻击状态下的伤害间隔。")]
    [SerializeField] private float attackTickInterval = 0.55f;

    [Tooltip("每次攻击造成的伤害。")]
    [SerializeField] private int attackDamage = 1;

    [Tooltip("命中后的水平击退。")]
    [SerializeField] private float knockbackForce = 4f;

    [Tooltip("命中后的轻微向上击退。")]
    [SerializeField] private float knockbackUpForce = 1.5f;

    [Header("=== 强制跳状态代价 ===")]
    [Tooltip("Trickster 强行跳状态后，机关进入的长 Recovery 破绽期。")]
    [SerializeField] private float forcedSkipRecoveryDuration = 3.2f;

    [Tooltip("强行跳状态追加给锚点的可疑度。")]
    [SerializeField] private float forcedSkipSuspicionPenalty = 90f;

    [Tooltip("强行跳状态追加给锚点的证据层数。")]
    [SerializeField] private int forcedSkipEvidencePenalty = 3;

    [Tooltip("强行跳状态追加给 Trickster 的热度。")]
    [SerializeField] private float forcedSkipHeatPenalty = 45f;

    [Header("=== 视觉颜色 ===")]
    [SerializeField] private Color leftAttackColor = new Color(1f, 0.35f, 0.25f, 0.95f);
    [SerializeField] private Color rightAttackColor = new Color(1f, 0.55f, 0.20f, 0.95f);
    [SerializeField] private Color safePauseColor = new Color(0.35f, 0.9f, 0.45f, 0.8f);
    [SerializeField] private Color forcedRecoveryColor = new Color(0.55f, 0.65f, 1f, 0.65f);

    private readonly QueueTrapState[] queue =
    {
        QueueTrapState.LeftAttack,
        QueueTrapState.RightAttack,
        QueueTrapState.SafePause
    };

    private int currentQueueIndex;
    private float queueTimer;
    private float attackTickTimer;

    private BoxCollider2D boxCollider;
    private SpriteRenderer sr;
    private MarioSuspicionTracker suspicionTracker;
    private TricksterHeatMeter heatMeter;
    // 使用基类 ControllablePropBase.originalColor (protected)，不再重复声明

    private QueueTrapState CurrentQueueState => queue[currentQueueIndex];
    private QueueTrapState NextQueueState => queue[(currentQueueIndex + 1) % queue.Length];

    protected override void Awake()
    {
        propName = "公开队列机关";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Resettable;
        elementDescription = "按公开队列自动循环 Left/Right/Safe；Trickster 可高代价强行跳到下一状态";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        originalColor = sr != null ? sr.color : Color.white;

        suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        heatMeter = FindObjectOfType<TricksterHeatMeter>();

        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }

        EnsureStateText();
        ResetQueue(0);
    }

    protected override void Update()
    {
        base.Update();

        if (currentState == PropControlState.Recovery)
        {
            ApplyVisual(forcedRecoveryColor);
            UpdateStateText();
            return;
        }

        if (currentState == PropControlState.Cooldown || currentState == PropControlState.Exhausted)
        {
            UpdateStateText();
            return;
        }

        queueTimer -= Time.deltaTime;
        if (queueTimer <= 0f)
        {
            AdvanceQueueState();
        }

        ApplyVisualForCurrentQueueState();
        TickCurrentQueueEffect();
        UpdateStateText();
    }

    /// <summary>
    /// 原型 C 的关键重定义：Trickster Activate 不是直接触发攻击，而是强行跳过当前队列状态。
    /// </summary>
    public override void OnTricksterActivate(Vector2 direction)
    {
        if (!CanBeControlled()) return;

        activateDirection = direction;
        if (maxUses >= 0)
        {
            remainingUses--;
        }

        AdvanceQueueState();
        ApplyForcedSkipPenalty();
        EnterForcedRecovery();
    }

    protected override void OnTelegraphStart()
    {
        // StateQueueTrap 不使用基类 Telegraph→Active 作为攻击入口。
    }

    protected override void OnTelegraphEnd()
    {
        // Trickster 只能跳队列，不能借由 Telegraph 直接制造攻击。
    }

    protected override void OnActivate(Vector2 direction)
    {
        // 保持空实现：攻击只来自公开队列的自动循环。
    }

    protected override void OnActiveEnd()
    {
        // 保持空实现。
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        ResetQueue(0);
        if (sr != null) sr.color = originalColor;
        if (boxCollider != null) boxCollider.isTrigger = true;
        UpdateStateText();
    }

    private void ResetQueue(int index)
    {
        currentQueueIndex = Mathf.Clamp(index, 0, queue.Length - 1);
        queueTimer = GetDuration(CurrentQueueState);
        attackTickTimer = 0f;
        ApplyVisualForCurrentQueueState();
        UpdateStateText();
    }

    private void AdvanceQueueState()
    {
        currentQueueIndex = (currentQueueIndex + 1) % queue.Length;
        queueTimer = GetDuration(CurrentQueueState);
        attackTickTimer = 0f;
        ApplyVisualForCurrentQueueState();
        UpdateStateText();
    }

    private float GetDuration(QueueTrapState state)
    {
        switch (state)
        {
            case QueueTrapState.LeftAttack:
                return Mathf.Max(0.1f, leftAttackDuration);
            case QueueTrapState.RightAttack:
                return Mathf.Max(0.1f, rightAttackDuration);
            case QueueTrapState.SafePause:
                return Mathf.Max(0.1f, safePauseDuration);
            default:
                return 1f;
        }
    }

    private void TickCurrentQueueEffect()
    {
        if (CurrentQueueState == QueueTrapState.SafePause) return;

        attackTickTimer -= Time.deltaTime;
        if (attackTickTimer > 0f) return;

        DealDirectionalAttack(CurrentQueueState == QueueTrapState.LeftAttack ? Vector2.left : Vector2.right);
        attackTickTimer = Mathf.Max(0.1f, attackTickInterval);
    }

    private void DealDirectionalAttack(Vector2 direction)
    {
        Vector2 center = (Vector2)transform.position + direction.normalized * attackOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackBoxSize, 0f);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            MarioController mario = hit.GetComponentInParent<MarioController>();
            if (mario == null) continue;

            PlayerHealth health = mario.GetComponent<PlayerHealth>();
            if (health == null || health.IsInvincible) continue;

            health.TakeDamage(attackDamage);
            ApplyKnockback(mario, hit);
            break;
        }
    }

    private void ApplyKnockback(MarioController mario, Collider2D hit)
    {
        Rigidbody2D rb = mario.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
            mario.transform,
            transform,
            rb,
            knockbackForce,
            knockbackUpForce);

        rb.velocity = Vector2.zero;
        rb.AddForce(knockback, ForceMode2D.Impulse);
        KnockbackHelper.NotifyKnockbackStun(hit);
    }

    private void ApplyForcedSkipPenalty()
    {
        PossessionAnchor anchor = GetComponent<PossessionAnchor>();

        if (suspicionTracker == null)
        {
            suspicionTracker = FindObjectOfType<MarioSuspicionTracker>();
        }

        if (suspicionTracker != null && anchor != null)
        {
            suspicionTracker.ApplySuspicionEvidencePenalty(
                anchor,
                forcedSkipSuspicionPenalty,
                forcedSkipEvidencePenalty,
                "StateQueueTrapForceSkip");
        }

        if (heatMeter == null)
        {
            heatMeter = FindObjectOfType<TricksterHeatMeter>();
        }

        if (heatMeter != null)
        {
            heatMeter.AddHeat(forcedSkipHeatPenalty);
        }
    }

    private void EnterForcedRecovery()
    {
        currentState = PropControlState.Recovery;
        stateTimer = Mathf.Max(recoveryDuration, forcedSkipRecoveryDuration);
        ApplyVisual(forcedRecoveryColor);
        UpdateStateText();
    }

    private void EnsureStateText()
    {
        if (stateText == null)
        {
            stateText = GetComponentInChildren<TextMesh>();
        }

        if (stateText == null)
        {
            GameObject textObject = new GameObject("StateQueueText");
            textObject.transform.SetParent(transform, false);
            stateText = textObject.AddComponent<TextMesh>();
        }

        stateText.transform.localPosition = textLocalOffset;
        stateText.anchor = TextAnchor.MiddleCenter;
        stateText.alignment = TextAlignment.Center;
        stateText.fontSize = 32;
        stateText.characterSize = 0.055f;
        stateText.color = Color.white;
    }

    private void UpdateStateText()
    {
        if (stateText == null) return;

        stateText.text = "Current: " + GetStateLabel(CurrentQueueState) + "\n" +
                         "Next: " + GetStateLabel(NextQueueState);
    }

    private string GetStateLabel(QueueTrapState state)
    {
        switch (state)
        {
            case QueueTrapState.LeftAttack:
                return "Left Attack";
            case QueueTrapState.RightAttack:
                return "Right Attack";
            case QueueTrapState.SafePause:
                return "Safe Pause";
            default:
                return "Unknown";
        }
    }

    private void ApplyVisualForCurrentQueueState()
    {
        switch (CurrentQueueState)
        {
            case QueueTrapState.LeftAttack:
                ApplyVisual(leftAttackColor);
                break;
            case QueueTrapState.RightAttack:
                ApplyVisual(rightAttackColor);
                break;
            case QueueTrapState.SafePause:
                ApplyVisual(safePauseColor);
                break;
        }
    }

    private void ApplyVisual(Color color)
    {
        if (sr != null)
        {
            sr.color = color;
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.red;
        Vector3 leftCenter = transform.position + Vector3.left * attackOffset;
        Vector3 rightCenter = transform.position + Vector3.right * attackOffset;
        Gizmos.DrawWireCube(leftCenter, attackBoxSize);
        Gizmos.DrawWireCube(rightCenter, attackBoxSize);
    }
}
