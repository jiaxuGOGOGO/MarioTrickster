using UnityEngine;

/// <summary>
/// 可操控方块 - Trickster 变身后可操控的场景方块
/// 参考: Ultimate Chicken Horse 的平台放置/移除机制
/// 
/// 操控效果:
///   - 预警阶段: 方块闪烁 + 震动，给 Mario 反应时间
///   - 激活阶段: 方块消失（或变为半透明无碰撞），Mario 会掉落
///   - 冷却阶段: 方块恢复实体
/// 
/// 操控模式:
///   - Vanish:   方块暂时消失（最常用，适合脚下的地面方块）
///   - Slide:    方块朝指定方向滑动一格（适合推箱子式的阻碍）
///   - Bounce:   方块变成弹跳板，把 Mario 弹飞（适合出其不意）
/// 
/// 使用方式:
///   1. 在场景中创建方块 GameObject（Sprite + BoxCollider2D）
///   2. 挂载此脚本
///   3. 方块默认是正常的实体地面，Trickster 操控时才会变化
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ControllableBlock : ControllablePropBase
{
    [Header("=== 方块操控模式 ===")]
    [SerializeField] private BlockControlMode blockMode = BlockControlMode.Vanish;

    [Header("=== Vanish 模式参数 ===")]
    [Tooltip("消失时的透明度（0=完全不可见，0.3=半透明提示）")]
    [SerializeField] private float vanishAlpha = 0.15f;

    [Header("=== Slide 模式参数 ===")]
    [Tooltip("滑动距离（格数）")]
    [SerializeField] private float slideDistance = 1f;

    [Tooltip("滑动速度")]
    [SerializeField] private float slideSpeed = 8f;

    [Header("=== Bounce 模式参数 ===")]
    [Tooltip("弹跳力度")]
    [SerializeField] private float bounceForce = 15f;

    // 组件
    private BoxCollider2D boxCollider;

    // 原始状态（注意：originalColor 已在父类 ControllablePropBase 中定义为 protected）
    private Vector3 originalPosition;
    private bool originalColliderEnabled;

    // Slide 模式状态
    private Vector3 slideTargetPosition;
    private bool isSliding;

    // Bounce 模式状态
    private bool isBounceActive;

    protected override void Awake()
    {
        base.Awake();
        propName = "方块";

        boxCollider = GetComponent<BoxCollider2D>();
        originalColliderEnabled = boxCollider.enabled;

        // originalColor 已在父类 base.Awake() 中赋值，无需重复赋值
        originalPosition = transform.localPosition;
    }

    protected override void Update()
    {
        base.Update();

        // Slide 模式：平滑移动
        if (isSliding)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, slideTargetPosition,
                slideSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.localPosition, slideTargetPosition) < 0.01f)
            {
                transform.localPosition = slideTargetPosition;
                isSliding = false;
            }
        }
    }

    #region ControllablePropBase 实现

    protected override void OnTelegraphStart()
    {
        // 预警音效占位
        // AudioManager.Instance?.PlaySFX("block_warning");
    }

    protected override void OnTelegraphEnd()
    {
        // 预警结束
    }

    protected override void OnActivate(Vector2 direction)
    {
        switch (blockMode)
        {
            case BlockControlMode.Vanish:
                ActivateVanish();
                break;
            case BlockControlMode.Slide:
                ActivateSlide(direction);
                break;
            case BlockControlMode.Bounce:
                ActivateBounce();
                break;
        }
    }

    protected override void OnActiveEnd()
    {
        switch (blockMode)
        {
            case BlockControlMode.Vanish:
                DeactivateVanish();
                break;
            case BlockControlMode.Slide:
                DeactivateSlide();
                break;
            case BlockControlMode.Bounce:
                DeactivateBounce();
                break;
        }
    }

    #endregion

    #region Vanish 模式

    private void ActivateVanish()
    {
        // 方块变为半透明且无碰撞
        boxCollider.enabled = false;

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = vanishAlpha;
            spriteRenderer.color = c;
        }
    }

    private void DeactivateVanish()
    {
        // 恢复实体
        boxCollider.enabled = originalColliderEnabled;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    #endregion

    #region Slide 模式

    private void ActivateSlide(Vector2 direction)
    {
        if (direction.magnitude < 0.1f)
        {
            direction = Vector2.right; // 默认朝右
        }

        // 只允许四方向滑动
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            direction = new Vector2(Mathf.Sign(direction.x), 0f);
        }
        else
        {
            direction = new Vector2(0f, Mathf.Sign(direction.y));
        }

        slideTargetPosition = transform.localPosition + new Vector3(direction.x, direction.y, 0f) * slideDistance;
        isSliding = true;
    }

    private void DeactivateSlide()
    {
        // 激活结束后滑回原位
        slideTargetPosition = originalPosition;
        isSliding = true;
    }

    #endregion

    #region Bounce 模式

    private void ActivateBounce()
    {
        isBounceActive = true;

        // 视觉提示：方块变为弹跳颜色
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(0.5f, 1f, 0.5f, 1f); // 绿色
        }
    }

    private void DeactivateBounce()
    {
        isBounceActive = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isBounceActive) return;

        // 检查是否是 Mario 从上方踩到方块
        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        if (mario == null) return;

        // 检查碰撞方向（Mario 从上方接触）
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) // 法线朝下 = Mario 从上方踩到
            {
                Rigidbody2D marioRb = collision.gameObject.GetComponent<Rigidbody2D>();
                if (marioRb != null)
                {
                    marioRb.velocity = new Vector2(marioRb.velocity.x, 0f);
                    marioRb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);
                }
                break;
            }
        }
    }

    #endregion

    #region 调试可视化

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        if (blockMode == BlockControlMode.Slide)
        {
            // 显示四个可能的滑动方向
            Gizmos.color = Color.blue;
            Vector3 pos = transform.position;
            Gizmos.DrawLine(pos, pos + Vector3.right * slideDistance);
            Gizmos.DrawLine(pos, pos + Vector3.left * slideDistance);
            Gizmos.DrawLine(pos, pos + Vector3.up * slideDistance);
            Gizmos.DrawLine(pos, pos + Vector3.down * slideDistance);
        }
    }

    #endregion
}

/// <summary>
/// 方块操控模式
/// </summary>
public enum BlockControlMode
{
    Vanish,     // 暂时消失
    Slide,      // 朝指定方向滑动
    Bounce      // 变成弹跳板
}
