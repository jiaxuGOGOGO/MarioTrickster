using UnityEngine;

/// <summary>
/// 可收集物品（金币、道具等）
/// 功能: Mario碰触后收集，可触发效果（加分、回血等）
/// 使用方式: 挂载到金币/道具GameObject上，需要Collider2D(isTrigger=true)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Collectible : MonoBehaviour
{
    public enum CollectibleType
    {
        Coin,       // 金币（加分）
        HealthPack, // 回血
        SpeedBoost, // 加速（预留）
        ExtraLife   // 额外生命（预留）
    }

    [Header("=== 物品设置 ===")]
    [SerializeField] private CollectibleType type = CollectibleType.Coin;
    [SerializeField] private int value = 1;

    [Header("=== 视觉效果 ===")]
    [SerializeField] private GameObject collectVFXPrefab;
    [SerializeField] private bool bobUpDown = true;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.2f;

    private Vector3 startPosition;

    // 静态计数器
    public static int TotalCoinsCollected { get; private set; }

    private void Start()
    {
        startPosition = transform.position;

        // 确保Collider是Trigger
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void Update()
    {
        // 上下浮动动画
        if (bobUpDown)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只有Mario可以收集
        MarioController mario = other.GetComponent<MarioController>();
        if (mario == null) return;

        // 应用效果
        switch (type)
        {
            case CollectibleType.Coin:
                TotalCoinsCollected += value;
                Debug.Log($"[Collectible] 金币 +{value}（总计: {TotalCoinsCollected}）");
                break;

            case CollectibleType.HealthPack:
                PlayerHealth health = other.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.Heal(value);
                    Debug.Log($"[Collectible] 回血 +{value}");
                }
                break;

            case CollectibleType.SpeedBoost:
                Debug.Log("[Collectible] 加速道具（待实现）");
                break;

            case CollectibleType.ExtraLife:
                Debug.Log("[Collectible] 额外生命（待实现）");
                break;
        }

        // 播放特效
        if (collectVFXPrefab != null)
        {
            Instantiate(collectVFXPrefab, transform.position, Quaternion.identity);
        }

        // 销毁自身
        Destroy(gameObject);
    }

    /// <summary>重置金币计数（新关卡时调用）</summary>
    public static void ResetCoinCount()
    {
        TotalCoinsCollected = 0;
    }
}
