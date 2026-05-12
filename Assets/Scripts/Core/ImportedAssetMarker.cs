using UnityEngine;

/// <summary>
/// ImportedAssetMarker — 导入资产标记组件
///
/// 挂在通过 Asset Import Pipeline 生成的 Object 根节点上，
/// 记录导入时的元数据（源 Sprite 列表、物理类型、帧数等），
/// 方便后续工具（如 SEF Quick Apply、动画生成器）识别和处理。
///
/// 运行时无任何开销（不含 Update/LateUpdate），仅作为数据容器。
/// 发布时可通过 Build Stripping 自动移除。
/// </summary>
public class ImportedAssetMarker : MonoBehaviour
{
    [Header("导入元数据（由 Asset Import Pipeline 自动填写）")]
    [Tooltip("源 Sprite 帧列表")]
    public Sprite[] sourceSprites;

    [Tooltip("物理类型: 0=角色, 1=地形, 2=陷阱, 3=特效, 4=道具")]
    public int physicsType;

    [Tooltip("总帧数")]
    public int frameCount;

    [Tooltip("导入时间戳")]
    public string importTimestamp;

    [Tooltip("源文件路径（相对于 Assets）")]
    public string sourceAssetPath;

    [Header("商业素材统一分类（向后兼容，可为空）")]
    [Tooltip("素材角色分类：Character / Enemy / Prop / Collectible / Hazard / Platform / Background / SceneAnimation / VFX / UI / Unknown")]
    public string assetRole = "Unknown";

    [Tooltip("动画接入模式：None / Loop / StateDriven / OneShot")]
    public string animationMode = "None";

    [Tooltip("运行行为：PlayerStateDriven / PhysicsProp / PickupConsume / HazardContact / BackgroundLayer / AmbientLoop / VFX / KeepExisting")]
    public string runtimeBehavior = "KeepExisting";

    [Tooltip("已识别出的动画状态，逗号分隔，例如 idle,run,jump,fall")]
    public string animationStates = "";

    [Tooltip("锁定当前 Visual/SEF 效果，Quick Apply 默认不覆盖")]
    public bool visualEffectLocked;

    [Range(0f, 1f)]
    [Tooltip("自动分类置信度，1 表示命名/路径强匹配")]
    public float classificationConfidence;

    [TextArea(2, 6)]
    [Tooltip("自动分类说明，方便后续维护与排错")]
    public string classificationNotes = "";
}
