using UnityEngine;

/// <summary>
/// Commit 0：附身锚点。
///
/// 该组件只提供“这个对象是否可以作为 Trickster 附身/操控目标”的轻量元数据，
/// 不改 ControllablePropBase 的 Telegraph → Active → Cooldown 状态机，也不直接改变物理表现。
/// 推荐挂在已有 ControllablePropBase 同一个 GameObject 上；旧场景未挂载时，
/// TricksterPossessionGate 会在运行时为锁定到的可操控对象补齐默认锚点。
/// </summary>
[DisallowMultipleComponent]
public class PossessionAnchor : MonoBehaviour
{
    [Header("=== 附身锚点 ===")]
    [Tooltip("锚点是否允许被 Trickster 附身/锁定")]
    [SerializeField] private bool possessionEnabled = true;

    [Tooltip("调试用锚点 ID；为空时使用 GameObject 名称")]
    [SerializeField] private string anchorId;

    [Tooltip("仅作为 Commit 1+ 暴露/残留系统的默认提示时间，不在 Commit 0 主动生成残留")]
    [SerializeField] private float defaultResidueSeconds = 1.2f;

    private IControllableProp controllableProp;

    public string AnchorId => string.IsNullOrEmpty(anchorId) ? gameObject.name : anchorId;
    public bool PossessionEnabled => possessionEnabled;
    public float DefaultResidueSeconds => defaultResidueSeconds;
    public IControllableProp ControllableProp => controllableProp;
    public Transform AnchorTransform => transform;

    /// <summary>
    /// 当前锚点是否可以被锁定为附身目标。
    /// </summary>
    public bool CanBePossessed()
    {
        return possessionEnabled && controllableProp != null && controllableProp.CanBeControlled();
    }

    /// <summary>
    /// 返回适合 UI/日志显示的锚点状态，不承担玩法逻辑。
    /// </summary>
    public string GetDebugStatus()
    {
        if (!possessionEnabled) return $"{AnchorId}: Disabled";
        if (controllableProp == null) return $"{AnchorId}: Missing IControllableProp";
        return $"{AnchorId}: {controllableProp.PropName} / {controllableProp.GetControlState()}";
    }

    private void Awake()
    {
        CacheControllableProp();
    }

    private void Reset()
    {
        anchorId = gameObject.name;
        CacheControllableProp();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(anchorId))
        {
            anchorId = gameObject.name;
        }

        defaultResidueSeconds = Mathf.Max(0f, defaultResidueSeconds);
    }

    private void CacheControllableProp()
    {
        controllableProp = GetComponent<IControllableProp>();
    }
}
