using UnityEngine;

/// <summary>
/// 通用伤害触发器
/// 功能: 碰触时对目标造成伤害（用于尖刺、怪物、Trickster的Hazard伪装等）
/// 使用方式: 挂载到危险物体上，需要Collider2D(isTrigger=true)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageDealer : MonoBehaviour
{
    [Header("=== 伤害设置 ===")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageCooldown = 0.5f; // 防止连续伤害

    [Header("=== 目标过滤 ===")]
    [Tooltip("只对带有这些标签的对象造成伤害（空=对所有PlayerHealth生效）")]
    [SerializeField] private string[] targetTags = { "Player" };

    [Header("=== 击退 ===")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackUpForce = 3f;

    private float lastDamageTime;

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;

        // 标签过滤
        if (targetTags.Length > 0)
        {
            bool tagMatch = false;
            foreach (string tag in targetTags)
            {
                if (other.CompareTag(tag))
                {
                    tagMatch = true;
                    break;
                }
            }
            if (!tagMatch) return;
        }

        // 造成伤害
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && !health.IsInvincible)
        {
            health.TakeDamage(damage);
            lastDamageTime = Time.time;

            // 击退
            if (applyKnockback)
            {
                Rigidbody2D targetRb = other.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockDir = (other.transform.position - transform.position).normalized;
                    knockDir.y = 0;
                    if (knockDir.x == 0) knockDir.x = 1f; // 默认向右
                    knockDir = knockDir.normalized;

                    targetRb.velocity = Vector2.zero;
                    targetRb.AddForce(new Vector2(knockDir.x * knockbackForce, knockbackUpForce), ForceMode2D.Impulse);
                }
            }

            Debug.Log($"[DamageDealer] {gameObject.name} 对 {other.gameObject.name} 造成 {damage} 点伤害");
        }
    }
}
