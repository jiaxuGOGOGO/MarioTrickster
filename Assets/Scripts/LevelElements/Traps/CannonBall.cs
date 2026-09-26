using UnityEngine;

/// <summary>
/// S187：炮弹（由 PranksterCannon 在运行时生成，不单独放置）。
/// - 直线飞行（运动学刚体，不受重力，轨迹可读），碰到实体墙/地面消失；
/// - 命中马里奥：扣 1 血 + 安全击退（复用 KnockbackHelper，与火焰同一套受伤流程 → 马里奥心智的"被坑晕"、连招都自动生效）；
/// - 不伤捣蛋者（是玩家自己的炮）；超时自毁，不会残留。
/// 危险判定只在飞行中存在，碰撞体不大于视觉（宪法 H3）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class CannonBall : MonoBehaviour
{
    private Vector2 velocity;
    private float life;
    private int damage;
    private float knockback, knockbackUp;
    private Transform owner;
    private Rigidbody2D body;
    private bool spent;

    public bool Spent => spent;

    public void Launch(Transform source, Vector2 v, float lifetime, int dmg, float knock, float knockUp)
    {
        owner = source; velocity = v; life = lifetime; damage = dmg; knockback = knock; knockbackUp = knockUp;
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.velocity = velocity;
        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
    }

    private void FixedUpdate()
    {
        if (spent) return;
        body.velocity = velocity;
        life -= Time.fixedDeltaTime;
        if (life <= 0f) Pop();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (spent || other == null) return;
        if (owner != null && other.transform.IsChildOf(owner)) return;
        if (other.GetComponentInParent<TricksterController>() != null) return; // 玩家自己的炮，不伤自己

        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null && other.GetComponentInParent<MarioController>() != null)
        {
            if (!health.IsInvincible)
            {
                health.TakeDamage(damage);
                var rb = other.attachedRigidbody;
                if (rb != null)
                {
                    Vector2 push = new Vector2(Mathf.Sign(velocity.x) * knockback, knockbackUp);
                    rb.velocity = Vector2.zero;
                    rb.AddForce(push, ForceMode2D.Impulse);
                    KnockbackHelper.NotifyKnockbackStun(other);
                }
            }
            Pop();
            return;
        }

        // 撞到实体（墙/地面/箱子）就碎；触发器与单向台面穿过
        if (!other.isTrigger && !MarioSuspicionTracker.IsOneWayPlatform(other)) Pop();
    }

    private void Pop()
    {
        spent = true;
        Destroy(gameObject);
    }
}
