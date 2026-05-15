using UnityEngine;

/// <summary>
/// ExplosiveHazard — 碰撞爆炸型陷阱
///
/// 继承 BaseHazard，当玩家碰触时：
///   1. 对爆炸半径内的所有 PlayerHealth 造成范围伤害
///   2. 施加击退力
///   3. 发送爆炸表现信号（由 VisualFeedbackBridge 统一表现）
///   4. 自毁（延迟销毁，等表现播完）
///
/// 设计参考：
///   - Super Mario Bros 炸弹兵（Bob-omb）：碰触/倒计时 → 范围爆炸
///   - Celeste Chapter 5 爆炸水晶：碰触 → 范围冲击波
///
/// 使用方式：
///   通过 AssetApplyToSelected 的"碰撞爆炸"模板自动挂载，
///   或手动挂载到任何有 Collider2D(isTrigger=true) 的物体上。
///
/// [AI防坑警告]
///   1. 必须有 Collider2D 且 isTrigger=true
///   2. 爆炸范围检测使用 Physics2D.OverlapCircleAll，不依赖自身碰撞体
///   3. 自毁前保留 destroyDelay，确保表现层有时间播放溶解/闪白
///   4. 继承 BaseHazard 获得防刷屏保护（同一玩家不会被爆炸伤害两次）
///   5. 禁止直接调用 SpriteEffectController 或 Camera Shake；表现统一走 GameplayEventBus
/// </summary>
public class ExplosiveHazard : BaseHazard
{
    [Header("=== 爆炸参数 ===")]
    [Tooltip("爆炸伤害")]
    [SerializeField] private int explosionDamage = 3;

    [Tooltip("爆炸半径（世界单位）")]
    [SerializeField] private float explosionRadius = 2f;

    [Tooltip("爆炸击退力度")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("爆炸击退向上分量")]
    [SerializeField] private float explosionUpForce = 4f;

    [Tooltip("爆炸后自毁延迟（秒，等动画播完）")]
    [SerializeField] private float destroyDelay = 0.8f;

    [Tooltip("是否只爆炸一次（false = 每次碰触都爆炸）")]
    [SerializeField] private bool oneShot = true;

    // 状态
    private bool _hasExploded;

    private void Awake()
    {
        baseDamage = explosionDamage;
    }

    /// <summary>
    /// 由 AssetApplyToSelected 编辑器调用，设置爆炸参数。
    /// </summary>
    public void SetExplosionParams(int damage, float radius, float force)
    {
        explosionDamage = damage;
        explosionRadius = radius;
        explosionForce = force;
        baseDamage = damage;
    }

    protected override int GetDamage() => explosionDamage;

    protected override void OnHazardTriggered(PlayerHealth health)
    {
        if (oneShot && _hasExploded) return;
        _hasExploded = true;

        // 范围爆炸：检测爆炸半径内的所有 PlayerHealth
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            PlayerHealth ph = hit.GetComponent<PlayerHealth>();
            if (ph != null && ph.CurrentHealth > 0)
            {
                // 造成伤害
                ph.TakeDamage(explosionDamage);

                // 击退
                Rigidbody2D targetRb = ph.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    Vector2 dir = (ph.transform.position - transform.position).normalized;
                    Vector2 force = new Vector2(dir.x * explosionForce, explosionUpForce);
                    targetRb.velocity = Vector2.zero;
                    targetRb.AddForce(force, ForceMode2D.Impulse);

                    // 通知控制器进入击退僵直
                    KnockbackHelper.NotifyKnockbackStun(ph.gameObject);
                }
            }
        }

        // 表现层信号：闪白、溶解、震动由 VisualFeedbackBridge 统一处理。
        GameplayEventBus.SendTrapTriggered(
            gameObject,
            health != null ? health.gameObject : null,
            transform.position,
            hitFlashDuration: 0.1f,
            hitFlashColor: Color.white,
            playDissolve: true,
            dissolveDuration: destroyDelay * 0.8f,
            shakeDuration: 0.18f,
            shakeMagnitude: 0.08f,
            reason: "ExplosiveHazard");

        // 延迟自毁
        Destroy(gameObject, destroyDelay);
    }


    protected override void OnPlayerKilled(PlayerHealth health)
    {
        Debug.Log($"[ExplosiveHazard] {health.gameObject.name} 被 {gameObject.name} 的爆炸击杀！");
    }

    protected override bool ShouldFreezeOnKill() => true;

    /// <summary>关卡重置时恢复状态</summary>
    public void OnLevelReset()
    {
        _hasExploded = false;
        ResetProcessedSet();
    }

    // 编辑器可视化
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, explosionRadius);
    }
}
