using UnityEngine;

/// <summary>
/// Sprite 自动适配 — 视碰分离 (Visual-Collision Decoupling) 版本
///
/// 核心理念：
///   碰撞体尺寸 = 物理真相（由 PhysicsMetrics 定义，绝不修改）
///   视觉 Sprite = 纯粹装饰（自动适配碰撞体，换素材不影响物理）
///
/// 三种适配模式：
///   1. Tiled（推荐）：Sprite 以原始尺寸平铺填满碰撞体区域
///      - 适用于：地面、墙壁、平台等可重复贴图的元素
///      - 优势：换素材时不会拉伸变形，保持像素完美
///      - 要求：Sprite 导入设置 Mesh Type = Full Rect
///
///   2. Scaled（兼容模式）：用 transform.localScale 拉伸匹配
///      - 适用于：角色、敌人、道具等不可重复的独立 Sprite
///      - 注意：会导致非正方形素材变形
///
///   3. SlicedNineSlice（九宫格）：使用 Sprite 的 9-Slice 边框拉伸
///      - 适用于：UI 元素、带边框的平台
///      - 要求：Sprite 已设置 9-Slice 边框
///
/// 使用方式：
///   挂到任何有 SpriteRenderer + BoxCollider2D 的物体上。
///   根据元素类型选择适配模式，Sprite 自动匹配碰撞体尺寸。
///
/// Session 32: 视碰分离重构
///   - 新增 Tiled 模式（推荐用于地形元素）
///   - 新增 Sliced 模式（用于带边框元素）
///   - 保留 Scaled 模式向后兼容
///   - transform.localScale 在 Tiled/Sliced 模式下锁定为 (1,1,1)
/// </summary>
[ExecuteInEditMode]
[RequireComponent(typeof(SpriteRenderer))]
// [AI防坑警告] S191：不能 RequireComponent(BoxCollider2D)。本组件挂在 Visual 子节点上，
// Require 会在 Visual 上自动加一个多余碰撞体（草丛这类触发器会变成实心、大炮挡路）。
// 物理真相只在 Root 的 BoxCollider2D，这里用 GetComponentInParent 读取。
public class SpriteAutoFit : MonoBehaviour
{
    /// <summary>适配模式</summary>
    public enum FitMode
    {
        /// <summary>平铺模式（推荐）：Sprite 以原始尺寸重复填满碰撞体区域</summary>
        Tiled,
        /// <summary>缩放模式（兼容）：用 localScale 拉伸匹配碰撞体</summary>
        Scaled,
        /// <summary>九宫格模式：使用 Sprite 的 9-Slice 边框</summary>
        SlicedNineSlice,
        /// <summary>S191 等比放入模式：Sprite 等比缩放放进"显示框"（默认=碰撞体，可指定），不变形，底边对齐。用于箱子/大炮/火/道具。</summary>
        Contain
    }

    [Header("=== 适配设置 ===")]
    [Tooltip("适配模式：Tiled=平铺（推荐地形），Scaled=拉伸（角色/道具），SlicedNineSlice=九宫格")]
    [SerializeField] private FitMode fitMode = FitMode.Tiled;

    [Tooltip("S191：显示框（格）。≤0 = 用碰撞体尺寸。Contain/Scaled 模式下按它适配，碰撞体永远不动（视碰分离）")]
    [SerializeField] private Vector2 displayBox = Vector2.zero;
    [Tooltip("S191：Contain 模式底边对齐显示框底边（站在地上的东西不悬空）")]
    [SerializeField] private bool alignBottom = true;

    [Tooltip("是否在运行时持续适配（关闭则仅在 Start 时适配一次）")]
    [SerializeField] private bool continuousFit = false;

    // [AI防坑警告] 不要删除 Scaled 模式！
    // 角色、敌人、道具等非重复 Sprite 仍然需要 Scale 适配。
    // Tiled 模式仅适用于可重复贴图的地形元素。

    private SpriteRenderer sr;
    private BoxCollider2D col;

    private void Awake()
    {
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上，BoxCollider2D 保留在根物体
        sr = GetComponentInChildren<SpriteRenderer>();
        col = GetComponentInParent<BoxCollider2D>();
    }

    private void Start()
    {
        FitSprite();
    }

    private void Update()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
            if (col == null) col = GetComponentInParent<BoxCollider2D>();
            FitSprite();
            return;
        }
        #endif

        if (continuousFit) FitSprite();
    }

    /// <summary>根据当前模式将 Sprite 适配到 BoxCollider2D 的大小</summary>
    public void FitSprite()
    {
        if (sr == null || sr.sprite == null || col == null) return;

        switch (fitMode)
        {
            case FitMode.Tiled:
                FitTiled();
                break;
            case FitMode.Scaled:
                FitScaled();
                break;
            case FitMode.SlicedNineSlice:
                FitSliced();
                break;
            case FitMode.Contain:
                FitContain();
                break;
        }
    }

    /// <summary>
    /// 核心物理 Root 缩放防腐层：SpriteAutoFit 只能挂在 Visual 表现节点上。
    /// </summary>
    private bool ShouldBlockRootScaleMutation()
    {
        bool isRoot = GetComponent<MarioController>() != null || GetComponent<TricksterController>() != null || GetComponent<LevelElementBase>() != null;
        if (!isRoot) return false;

        Debug.LogWarning("[防腐层拦截] SpriteAutoFit 严禁修改包含核心物理的 Root 节点的缩放或尺寸！已拦截，请将图片与 SpriteAutoFit 挂载到 Visual 子节点。");
        return true;
    }

    /// <summary>
    /// Tiled 模式：使用 SpriteRenderer.drawMode = Tiled
    /// Sprite 以原始尺寸平铺，不拉伸不变形。
    /// transform.localScale 锁定为 (1,1,1)。
    /// </summary>
    private void FitTiled()
    {
        if (ShouldBlockRootScaleMutation()) return;

        // 锁定 Scale 为 1（Tiled 模式下 Scale 必须为 1，否则平铺计算会错）
        transform.localScale = Vector3.one;

        sr.drawMode = SpriteDrawMode.Tiled;
        // 设置 Tiled 区域大小 = 碰撞体大小
        sr.size = col.size;

        // 适配性提示：如果 Sprite 的 Mesh Type 不是 Full Rect，Tiled 模式会报警告
        // 但不会崩溃，只是视觉效果不正确。导入素材时需要设置 Mesh Type = Full Rect。
    }

    /// <summary>
    /// Scaled 模式（向后兼容）：用 transform.localScale 拉伸匹配碰撞体。
    /// 适用于角色、敌人等不可重复的独立 Sprite。
    /// </summary>
    private void FitScaled()
    {
        if (ShouldBlockRootScaleMutation()) return;

        // 确保 drawMode 是 Simple（非 Tiled/Sliced）
        sr.drawMode = SpriteDrawMode.Simple;

        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        if (spriteWidth <= 0 || spriteHeight <= 0) return;

        float targetWidth = TargetBox.x;
        float targetHeight = TargetBox.y;

        float scaleX = targetWidth / spriteWidth;
        float scaleY = targetHeight / spriteHeight;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    /// <summary>
    /// 九宫格模式：使用 SpriteRenderer.drawMode = Sliced
    /// 适用于带边框的 UI 元素或平台。
    /// 要求 Sprite 已在导入设置中配置 9-Slice 边框。
    /// </summary>
    private void FitSliced()
    {
        if (ShouldBlockRootScaleMutation()) return;

        transform.localScale = Vector3.one;

        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = col.size;
    }

    /// <summary>显示框：设置了就用设置值，否则用碰撞体尺寸。</summary>
    private Vector2 TargetBox => displayBox.x > 0f && displayBox.y > 0f ? displayBox : col.size;

    /// <summary>设置显示框（格）。由换肤流程按 Registry.visualScale 传入。</summary>
    public void SetDisplayBox(Vector2 box, bool bottomAligned)
    {
        displayBox = box;
        alignBottom = bottomAligned;
        FitSprite();
    }

    /// <summary>纯计算：等比放入的缩放（取宽、高比例中较小者）。供测试。</summary>
    public static float ContainScale(Vector2 sprite, Vector2 box)
    {
        if (sprite.x <= 0.0001f || sprite.y <= 0.0001f) return 1f;
        return Mathf.Min(box.x / sprite.x, box.y / sprite.y);
    }

    /// <summary>
    /// S191 Contain：等比缩放放进显示框，不变形；底边对齐时 Visual 往下挪，使图的底边贴着显示框底边。
    /// </summary>
    private void FitContain()
    {
        if (ShouldBlockRootScaleMutation()) return;
        sr.drawMode = SpriteDrawMode.Simple;
        Vector2 spriteSize = sr.sprite.bounds.size;
        Vector2 box = TargetBox;
        float k = ContainScale(spriteSize, box);
        transform.localScale = new Vector3(k, k, 1f);
        if (alignBottom && transform.parent != null)
        {
            // 图的包围盒中心（考虑 pivot）→ 让图的底边 = 显示框底边（显示框以碰撞体中心为中心）
            float spriteBottom = (sr.sprite.bounds.center.y - sr.sprite.bounds.extents.y) * k;
            float boxBottom = col.offset.y - box.y * 0.5f;
            var p = transform.localPosition;
            transform.localPosition = new Vector3(col.offset.x, boxBottom - spriteBottom, p.z);
        }
    }

    /// <summary>
    /// 运行时切换适配模式（供其他脚本调用）
    /// </summary>
    public void SetFitMode(FitMode mode)
    {
        fitMode = mode;
        FitSprite();
    }

    /// <summary>当前适配模式</summary>
    public FitMode CurrentFitMode => fitMode;
}
