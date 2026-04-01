using UnityEngine;

/// <summary>
/// 死亡区域触发器（掉落深渊等）
/// 功能: 任何带有PlayerHealth的角色碰触后直接死亡
/// 使用方式: 挂载到关卡底部的长条GameObject上，需要BoxCollider2D(isTrigger=true)
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class KillZone : MonoBehaviour
{
    [SerializeField] private int damage = 999;

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Debug.Log($"[KillZone] {other.gameObject.name} 掉入深渊！");
        }
    }
}
