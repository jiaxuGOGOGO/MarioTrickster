using UnityEngine;

/// <summary>
/// Sprite 自动适配 - 将 Sprite 自动拉伸填满 BoxCollider2D 的大小
///
/// 使用方式：
///   挂到任何有 SpriteRenderer + BoxCollider2D 的物体上即可。
///   Sprite 会自动缩放到与 Collider 大小一致，无需手动调整。
///
/// 原理：
///   计算 Collider 的世界尺寸与 Sprite 原始尺寸的比值，
///   用 transform.localScale 缩放使两者匹配。
/// </summary>
[ExecuteInEditMode]  // 编辑器模式下也生效，方便预览
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class SpriteAutoFit : MonoBehaviour
{
    [Tooltip("是否在运行时持续适配（关闭则仅在 Start 时适配一次）")]
    [SerializeField] private bool continuousFit = false;

    private SpriteRenderer sr;
    private BoxCollider2D col;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();
    }

    private void Start()
    {
        FitSprite();
    }

    private void Update()
    {
        // 编辑器模式下始终适配，方便预览
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (col == null) col = GetComponent<BoxCollider2D>();
            FitSprite();
            return;
        }
        #endif

        if (continuousFit) FitSprite();
    }

    /// <summary>将 Sprite 缩放到匹配 BoxCollider2D 的大小</summary>
    public void FitSprite()
    {
        if (sr == null || sr.sprite == null || col == null) return;

        // Sprite 原始尺寸（世界单位，在 Scale=(1,1,1) 时）
        float spriteWidth  = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        if (spriteWidth <= 0 || spriteHeight <= 0) return;

        // Collider 目标尺寸（本地空间）
        float targetWidth  = col.size.x;
        float targetHeight = col.size.y;

        // 计算需要的缩放比
        float scaleX = targetWidth  / spriteWidth;
        float scaleY = targetHeight / spriteHeight;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);
    }
}
