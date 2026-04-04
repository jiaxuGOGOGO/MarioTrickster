using UnityEngine;

/// <summary>
/// 通用伤害触发器
/// 功能: 碰触时对目标造成伤害（用于尖刺、怪物、Trickster的Hazard伪装等）
/// 使用方式: 挂载到危险物体上，需要Collider2D(isTrigger=true)
///
/// Session 16 更新:
///   B023 - 击退后通知 MarioController/TricksterController 进入 knockback stun，
///          防止帧速度架构在下一帧覆盖击退力
///
/// Session 17 更新:
///   - 使用 KnockbackHelper 统一击退方向计算
///   - 击退方向改为 Mario 移动方向的反方向后退，避免反复二次伤害
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

    [Header("=== 击退 (Session 17 修正) ===")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float knockbackUpForce = 2f;

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

            // Session 17: 使用 KnockbackHelper 统一击退逻辑
            if (applyKnockback)
            {
                Rigidbody2D targetRb = other.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                        other.transform, transform, targetRb, knockbackForce, knockbackUpForce);
                    targetRb.velocity = Vector2.zero;
                    targetRb.AddForce(knockback, ForceMode2D.Impulse);

                    KnockbackHelper.NotifyKnockbackStun(other);
                }
            }

            Debug.Log($"[DamageDealer] {gameObject.name} 对 {other.gameObject.name} 造成 {damage} 点伤害");
        }
    }
}
