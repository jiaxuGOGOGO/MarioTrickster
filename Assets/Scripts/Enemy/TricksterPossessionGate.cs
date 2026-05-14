using System;
using UnityEngine;

/// <summary>
/// Commit 0：Trickster 附身状态门禁。
///
/// 职责边界：
///   - 监听 DisguiseSystem 与 TricksterAbilitySystem 的既有事件，推导身份状态。
///   - 对“能否切换附身目标 / 能否出手操控”提供单一判定入口。
///   - 出手后进入 Revealed → Escaping 短窗口，防止连续无成本骚扰。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph → Active → Cooldown 状态机。
///   - Commit 1 才会把 Revealed/Residue 转成 Mario 可感知的证据。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DisguiseSystem))]
[RequireComponent(typeof(TricksterAbilitySystem))]
public class TricksterPossessionGate : MonoBehaviour
{
    [Header("=== Commit 0 附身状态门禁 ===")]
    [Tooltip("出手后保持暴露状态的时间，期间不能继续操控")]
    [SerializeField] private float revealDuration = 0.8f;

    [Tooltip("暴露结束后的撤离缓冲时间，期间不能立刻再次附身")]
    [SerializeField] private float escapeDuration = 0.35f;

    [Tooltip("是否输出状态切换调试日志")]
    [SerializeField] private bool showDebugInfo = false;

    private DisguiseSystem disguiseSystem;
    private TricksterAbilitySystem abilitySystem;

    private TricksterPossessionState currentState = TricksterPossessionState.Roaming;
    private float stateTimer;
    private PossessionAnchor currentAnchor;
    private IControllableProp currentProp;

    public TricksterPossessionState CurrentState => currentState;
    public float StateTimerRemaining => stateTimer;
    public PossessionAnchor CurrentAnchor => currentAnchor;
    public IControllableProp CurrentProp => currentProp;
    public bool IsHiddenAndArmed => currentState == TricksterPossessionState.Possessing;
    public bool CanSwitchTarget => currentState == TricksterPossessionState.Possessing;
    public bool CanActivatePossession => currentState == TricksterPossessionState.Possessing && currentAnchor != null;

    public event Action<TricksterPossessionState> OnStateChanged;
    public event Action<PossessionAnchor> OnAnchorChanged;

    private void Awake()
    {
        disguiseSystem = GetComponent<DisguiseSystem>();
        abilitySystem = GetComponent<TricksterAbilitySystem>();
    }

    private void OnEnable()
    {
        if (disguiseSystem != null)
        {
            disguiseSystem.OnDisguiseChanged += HandleDisguiseChanged;
        }

        if (abilitySystem != null)
        {
            abilitySystem.OnPropBound += HandlePropBound;
            abilitySystem.OnPropUnbound += HandlePropUnbound;
            abilitySystem.OnPropActivated += HandlePropActivated;
        }
    }

    private void OnDisable()
    {
        if (disguiseSystem != null)
        {
            disguiseSystem.OnDisguiseChanged -= HandleDisguiseChanged;
        }

        if (abilitySystem != null)
        {
            abilitySystem.OnPropBound -= HandlePropBound;
            abilitySystem.OnPropUnbound -= HandlePropUnbound;
            abilitySystem.OnPropActivated -= HandlePropActivated;
        }
    }

    private void Update()
    {
        if (currentState == TricksterPossessionState.Revealed || currentState == TricksterPossessionState.Escaping)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f)
            {
                return;
            }

            if (currentState == TricksterPossessionState.Revealed)
            {
                SetState(TricksterPossessionState.Escaping, escapeDuration);
                return;
            }
        }

        RefreshStateFromDisguiseAndAnchor();
    }

    /// <summary>
    /// TricksterAbilitySystem 在出手和切换目标前调用此方法，避免 Revealed/Escaping 状态继续操作。
    /// </summary>
    public bool AllowsAbilityAction(string actionName = "Ability")
    {
        if (CanActivatePossession)
        {
            return true;
        }

        if (showDebugInfo)
        {
            string anchorInfo = currentAnchor != null ? currentAnchor.GetDebugStatus() : "No Anchor";
            Debug.Log($"[TricksterPossessionGate] Block {actionName}: state={currentState}, anchor={anchorInfo}");
        }

        return false;
    }

    /// <summary>
    /// 由 Mario 反制或危机扫描触发的强制揭穿入口。
    /// 保留当前锚点/道具引用，让后续证据、奖励、HUD 与撤离缓冲保持同一条状态链。
    /// </summary>
    public void ForceReveal(float bonusDuration = 0f, string source = "ExternalReveal")
    {
        float duration = revealDuration + Mathf.Max(0f, bonusDuration);
        SetState(TricksterPossessionState.Revealed, duration);

        if (showDebugInfo)
        {
            Debug.Log($"[TricksterPossessionGate] ForceReveal by {source}: {GetDebugStatus()}");
        }
    }

    /// <summary>
    /// 调试/测试用状态摘要。
    /// </summary>
    public string GetDebugStatus()
    {
        string anchorName = currentAnchor != null ? currentAnchor.AnchorId : "None";
        string propName = currentProp != null ? currentProp.PropName : "None";
        return $"State={currentState}, Timer={stateTimer:F2}, Anchor={anchorName}, Prop={propName}";
    }

    private void HandleDisguiseChanged(bool isDisguised)
    {
        if (!isDisguised)
        {
            SetCurrentAnchor(null, null);
            SetState(TricksterPossessionState.Roaming, 0f);
            return;
        }

        SetState(TricksterPossessionState.Blending, 0f);
    }

    private void HandlePropBound(IControllableProp prop)
    {
        SetCurrentAnchor(FindAnchor(prop), prop);
        RefreshStateFromDisguiseAndAnchor();
    }

    private void HandlePropUnbound()
    {
        SetCurrentAnchor(null, null);
        RefreshStateFromDisguiseAndAnchor();
    }

    private void HandlePropActivated(IControllableProp prop)
    {
        SetCurrentAnchor(FindAnchor(prop), prop);
        SetState(TricksterPossessionState.Revealed, revealDuration);
    }

    private void RefreshStateFromDisguiseAndAnchor()
    {
        if (disguiseSystem == null || !disguiseSystem.IsDisguised)
        {
            SetState(TricksterPossessionState.Roaming, 0f);
            return;
        }

        if (!disguiseSystem.IsFullyBlended)
        {
            SetState(TricksterPossessionState.Blending, 0f);
            return;
        }

        if (currentAnchor != null && currentAnchor.PossessionEnabled)
        {
            SetState(TricksterPossessionState.Possessing, 0f);
            return;
        }

        SetState(TricksterPossessionState.Blending, 0f);
    }

    private void SetCurrentAnchor(PossessionAnchor anchor, IControllableProp prop)
    {
        if (currentAnchor == anchor && currentProp == prop)
        {
            return;
        }

        currentAnchor = anchor;
        currentProp = prop;
        OnAnchorChanged?.Invoke(currentAnchor);
    }

    private void SetState(TricksterPossessionState nextState, float timer)
    {
        timer = Mathf.Max(0f, timer);
        if (currentState == nextState)
        {
            stateTimer = timer;
            return;
        }

        currentState = nextState;
        stateTimer = timer;
        OnStateChanged?.Invoke(currentState);

        if (showDebugInfo)
        {
            Debug.Log($"[TricksterPossessionGate] State → {currentState} ({GetDebugStatus()})");
        }
    }

    private PossessionAnchor FindAnchor(IControllableProp prop)
    {
        if (prop == null)
        {
            return null;
        }

        Transform propTransform = prop.GetTransform();
        if (propTransform == null)
        {
            return null;
        }

        PossessionAnchor anchor = propTransform.GetComponent<PossessionAnchor>();
        if (anchor == null)
        {
            anchor = propTransform.gameObject.AddComponent<PossessionAnchor>();
        }

        return anchor;
    }

    private void OnValidate()
    {
        revealDuration = Mathf.Max(0f, revealDuration);
        escapeDuration = Mathf.Max(0f, escapeDuration);
    }
}
