using UnityEngine;

/// <summary>
/// 玩家生命值管理 - 通用脚本，Mario和Trickster都可使用
/// 功能: 生命值管理、受伤无敌帧、死亡事件
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("=== 生命值 ===")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    [Header("=== 无敌帧 ===")]
    [SerializeField] private float invincibleDuration = 1.5f;
    [SerializeField] private float blinkInterval = 0.1f;

    private bool isInvincible;
    private float invincibleTimer;
    private SpriteRenderer spriteRenderer;

    // 事件
    public System.Action<int, int> OnHealthChanged; // (当前, 最大)
    public System.Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isInvincible)
        {
            invincibleTimer -= Time.deltaTime;

            // 闪烁效果
            float alpha = Mathf.PingPong(Time.time / blinkInterval, 1f) > 0.5f ? 1f : 0.3f;
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }

            if (invincibleTimer <= 0)
            {
                isInvincible = false;
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = 1f;
                    spriteRenderer.color = c;
                }
            }
        }
    }

    /// <summary>受到伤害</summary>
    public void TakeDamage(int damage = 1)
    {
        if (isInvincible || currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            OnDeath?.Invoke();
        }
        else
        {
            // 启动无敌帧
            isInvincible = true;
            invincibleTimer = invincibleDuration;
        }
    }

    /// <summary>恢复生命</summary>
    public void Heal(int amount = 1)
    {
        if (currentHealth <= 0) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>重置生命值</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isInvincible = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
