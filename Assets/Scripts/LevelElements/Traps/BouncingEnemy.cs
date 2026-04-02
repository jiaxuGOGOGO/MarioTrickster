using UnityEngine;

/// <summary>
/// 弹跳小怪物 - 关卡设计系统 · 敌人类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Enemy | 标签: Controllable, Damaging, Periodic, MovingPart
/// 
/// 功能:
///   - 原地弹跳的简单敌人，碰到Mario造成伤害
///   - Mario踩踏可消灭（从上方碰撞）
///   - 可配置弹跳高度、频率和水平漂移
///   - Trickster操控: 改变弹跳高度/频率，或强制静止
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BouncingEnemy : ControllableLevelElement
{
    [Header("=== 弹跳设置 ===")]
    [SerializeField] private float bounceForce = 8f;
    [SerializeField] private float bounceInterval = 1.5f;
    [SerializeField] private float horizontalDrift = 0f;

    [Header("=== 伤害设置 ===")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackUpForce = 3f;

    [Header("=== 被踩踏设置 ===")]
    [SerializeField] private bool canBeStomped = true;
    [SerializeField] private float stompBounceForce = 10f;

    [Header("=== 生命 ===")]
    [SerializeField] private int maxHealth = 1;

    // 组件
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // 状态
    private float bounceTimer;
    private int currentHealth;
    private bool isAlive = true;
    private Vector3 initialPosition;

    // Trickster覆盖
    private bool tricksterOverride;
    private float tricksterBounceMult = 1f;

    protected override void Awake()
    {
        propName = "弹跳怪物";
        elementCategory = ElementCategory.Enemy;
        elementTags = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Periodic | ElementTag.MovingPart | ElementTag.Resettable;
        elementDescription = "原地弹跳的小怪物，可被踩踏消灭";

        base.Awake();

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        boxCollider = GetComponent<BoxCollider2D>();

        currentHealth = maxHealth;
        bounceTimer = bounceInterval;
        initialPosition = transform.position;
    }

    protected override void Update()
    {
        base.Update();
        if (!isAlive) return;

        if (tricksterOverride)
        {
            bounceTimer -= Time.deltaTime;
            if (bounceTimer <= 0f)
            {
                rb.velocity = new Vector2(horizontalDrift, bounceForce * tricksterBounceMult);
                bounceTimer = bounceInterval;
            }
        }
        else
        {
            bounceTimer -= Time.deltaTime;
            if (bounceTimer <= 0f)
            {
                rb.velocity = new Vector2(horizontalDrift, bounceForce);
                bounceTimer = bounceInterval;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAlive) return;

        // 检查是否被踩踏（Mario从上方碰撞）
        if (canBeStomped && collision.gameObject.GetComponent<MarioController>() != null)
        {
            ContactPoint2D contact = collision.GetContact(0);
            if (contact.normal.y < -0.5f) // Mario在上方
            {
                TakeDamageFromStomp(collision.gameObject);
                return;
            }
        }

        // 对Mario造成伤害
        PlayerHealth health = collision.gameObject.GetComponent<PlayerHealth>();
        if (health != null && !health.IsInvincible)
        {
            health.TakeDamage(contactDamage);
            Rigidbody2D targetRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                Vector2 dir = (collision.transform.position - transform.position).normalized;
                targetRb.velocity = Vector2.zero;
                targetRb.AddForce(new Vector2(dir.x * knockbackForce, knockbackUpForce), ForceMode2D.Impulse);
            }
        }
    }

    private void TakeDamageFromStomp(GameObject stomper)
    {
        currentHealth--;
        if (currentHealth <= 0)
        {
            Die();
        }

        // 给踩踏者一个弹跳
        Rigidbody2D stomperRb = stomper.GetComponent<Rigidbody2D>();
        if (stomperRb != null)
        {
            stomperRb.velocity = new Vector2(stomperRb.velocity.x, stompBounceForce);
        }
    }

    private void Die()
    {
        isAlive = false;
        boxCollider.enabled = false;
        rb.simulated = false;

        // 简单的死亡效果：缩小消失
        StartCoroutine(DeathAnimation());
    }

    private System.Collections.IEnumerator DeathAnimation()
    {
        float t = 0;
        Vector3 originalScale = transform.localScale;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t / 0.3f);
            yield return null;
        }
        gameObject.SetActive(false);
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterOverride = true;
        if (direction.y > 0.5f) tricksterBounceMult = 2f;       // 高弹跳
        else if (direction.y < -0.5f) tricksterBounceMult = 0f;  // 停止弹跳
        else tricksterBounceMult = 1.5f;
    }

    protected override void OnActiveEnd()
    {
        tricksterOverride = false;
        tricksterBounceMult = 1f;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        isAlive = true;
        currentHealth = maxHealth;
        boxCollider.enabled = true;
        rb.simulated = true;
        transform.position = initialPosition;
        transform.localScale = Vector3.one;
        gameObject.SetActive(true);
        tricksterOverride = false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isAlive ? Color.magenta : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        // 弹跳高度预览
        Gizmos.color = new Color(1, 0, 1, 0.2f);
        float peakHeight = (bounceForce * bounceForce) / (2f * 9.81f * 2f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * peakHeight);
    }
}
