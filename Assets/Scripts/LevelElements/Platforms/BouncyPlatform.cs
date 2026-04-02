using UnityEngine;

/// <summary>
/// 弹跳平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, AffectsPhysics, Resettable
/// 
/// 功能:
///   - 玩家踩上后被弹射到高处（类似蘑菇弹簧）
///   - 可配置弹射力度和方向
///   - 弹射时有视觉压缩/回弹动画
///   - Trickster操控: 改变弹射方向或力度
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BouncyPlatform : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [SerializeField] private float bounceForce = 15f;
    [SerializeField] private Vector2 bounceDirection = Vector2.up;
    [SerializeField] private bool addHorizontalMomentum = true;

    [Header("=== 动画设置 ===")]
    [SerializeField] private float squashAmount = 0.3f;
    [SerializeField] private float squashDuration = 0.1f;
    [SerializeField] private float stretchDuration = 0.15f;

    // 组件
    private BoxCollider2D boxCollider;

    // 状态
    private Vector3 originalScale;
    private bool isAnimating;

    // Trickster覆盖
    private bool tricksterOverride;
    private Vector2 tricksterDirection;
    private float tricksterForceMult = 1f;

    protected override void Awake()
    {
        propName = "弹跳平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.AffectsPhysics | ElementTag.Resettable;
        elementDescription = "踩上后弹射到高处的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false; // 实体碰撞
        originalScale = transform.localScale;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 只有从上方落下才触发弹跳
        ContactPoint2D contact = collision.GetContact(0);
        if (contact.normal.y < -0.5f) return; // 不是从上方

        Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;

        // 计算弹射方向和力度
        Vector2 dir = tricksterOverride ? tricksterDirection : bounceDirection.normalized;
        float force = tricksterOverride ? bounceForce * tricksterForceMult : bounceForce;

        // 应用弹射
        Vector2 velocity = dir * force;
        if (addHorizontalMomentum)
        {
            velocity.x += targetRb.velocity.x * 0.5f; // 保留部分水平动量
        }
        targetRb.velocity = velocity;

        // 也尝试调用MarioController.Bounce
        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        if (mario != null)
        {
            mario.Bounce(force);
        }

        // 弹跳动画
        if (!isAnimating)
        {
            StartCoroutine(BounceAnimation());
        }

        Debug.Log($"[BouncyPlatform] 弹射 {collision.gameObject.name}, 力度={force}, 方向={dir}");
    }

    private System.Collections.IEnumerator BounceAnimation()
    {
        isAnimating = true;

        // 压缩
        float t = 0;
        Vector3 squashedScale = new Vector3(originalScale.x * (1 + squashAmount), originalScale.y * (1 - squashAmount), originalScale.z);
        while (t < squashDuration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, squashedScale, t / squashDuration);
            yield return null;
        }

        // 拉伸回弹
        t = 0;
        Vector3 stretchedScale = new Vector3(originalScale.x * (1 - squashAmount * 0.5f), originalScale.y * (1 + squashAmount * 0.5f), originalScale.z);
        while (t < stretchDuration)
        {
            t += Time.deltaTime;
            float progress = t / stretchDuration;
            if (progress < 0.5f)
                transform.localScale = Vector3.Lerp(squashedScale, stretchedScale, progress * 2f);
            else
                transform.localScale = Vector3.Lerp(stretchedScale, originalScale, (progress - 0.5f) * 2f);
            yield return null;
        }

        transform.localScale = originalScale;
        isAnimating = false;
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        tricksterDirection = direction.magnitude > 0.1f ? direction.normalized : bounceDirection.normalized;
        tricksterForceMult = 1.5f; // 操控时加强弹力
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
        tricksterForceMult = 1f;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        transform.localScale = originalScale;
        tricksterOverride = false;
        isAnimating = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
        // 弹射方向预览
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawRay(transform.position, bounceDirection.normalized * 2f);
    }
}
