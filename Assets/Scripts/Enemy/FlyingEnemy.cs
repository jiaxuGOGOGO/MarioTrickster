using UnityEngine;

/// <summary>
/// 飞行敌人 — 空中巡逻的威胁 (S56)
///
/// 设计参考：
///   - Super Mario Bros: Paratroopa 上下飞行巡逻
///   - Hollow Knight: 空中巡逻敌人，可从上方踩踏消灭
///
/// ASCII 字符: 'f'
/// 行为: 在生成位置上下（或左右）做正弦波巡逻，碰到玩家造成伤害 + 击退
///        Mario 从上方踩踏可消灭（与 BouncingEnemy/SimpleEnemy 一致）
///
/// 不使用 Rigidbody2D 重力（飞行敌人不受重力影响），
/// 使用 Kinematic Rigidbody2D 做碰撞检测。
/// </summary>
public class FlyingEnemy : MonoBehaviour
{
    [Header("=== 飞行参数 ===")]
    [SerializeField] private float patrolAmplitude = 2f;
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private bool horizontalPatrol = false;

    [Header("=== 战斗参数 ===")]
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float knockbackHorizontal = 4f;
    [SerializeField] private float knockbackVertical = 2f;
    [SerializeField] private float stompBounceForce = 10f;

    private Vector3 startPosition;
    private float timer;
    private bool isDead;
    private Transform visualTransform;
    private Vector3 originalVisualScale = Vector3.one;

    private void Awake()
    {
        startPosition = transform.position;

        // 视碰分离：查找 Visual 子节点
        Transform vis = transform.Find("Visual");
        if (vis != null)
            visualTransform = vis;

        if (visualTransform != null)
            originalVisualScale = visualTransform.localScale;

        // 确保碰撞体不是 Trigger（需要 OnCollisionEnter2D）
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = false;
    }

    private void Update()
    {
        if (isDead) return;

        timer += Time.deltaTime * patrolSpeed;
        float offset = Mathf.Sin(timer) * patrolAmplitude;

        if (horizontalPatrol)
        {
            transform.position = startPosition + new Vector3(offset, 0, 0);
        }
        else
        {
            transform.position = startPosition + new Vector3(0, offset, 0);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        PlayerHealth health = collision.gameObject.GetComponent<PlayerHealth>();
        if (health == null) return;

        // 踩踏判定：Mario 从上方落下（与 BouncingEnemy 一致的逻辑）
        Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (playerRb != null && playerRb.velocity.y < -0.1f)
        {
            // 检查接触点是否在敌人上方
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.point.y > transform.position.y + 0.1f)
                {
                    // 踩踏消灭
                    Die();
                    // 给 Mario 一个弹跳
                    playerRb.velocity = new Vector2(playerRb.velocity.x, stompBounceForce);
                    return;
                }
            }
        }

        // 非踩踏：造成伤害 + 击退
        health.TakeDamage(contactDamage);
        if (playerRb != null)
        {
            Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                health.transform, transform, playerRb,
                knockbackHorizontal, knockbackVertical);
            playerRb.AddForce(knockback, ForceMode2D.Impulse);
            KnockbackHelper.NotifyKnockbackStun(collision.gameObject);
        }
    }

    private void Die()
    {
        isDead = true;
        // 视觉反馈：缩小消失
        if (visualTransform != null)
        {
            StartCoroutine(DeathAnimation());
        }
        else
        {
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null) col.enabled = false;
            Destroy(gameObject, 0.1f);
        }
    }

    private System.Collections.IEnumerator DeathAnimation()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = false;

        float t = 0f;
        Vector3 originalScale = visualTransform.localScale;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float scale = Mathf.Lerp(1f, 0f, t / 0.3f);
            visualTransform.localScale = originalScale * scale;
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>关卡重置</summary>
    public void OnLevelReset()
    {
        isDead = false;
        timer = 0f;
        transform.position = startPosition;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = true;
        if (visualTransform != null)
            visualTransform.localScale = originalVisualScale;
    }
}
