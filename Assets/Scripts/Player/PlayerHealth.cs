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
    private float hitAt = -10f;           // S216
    private bool tinting;                 // S216
    private Color baseColor = Color.white; // S216：闪完回到原来的颜色（伪装/主题色不丢）

    // ── Test Console 调试开关（仅 Editor/Development Build 可用）──
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// God Mode：开启后 TakeDamage 完全无效，不扣血不触发死亡。
    /// 默认 false，只由调试代码在运行时设置（S238 旧测试台已删；第 1 步无限技能用 F9），
    /// 每次 Play 自动重置为 false，不影响自动化测试。
    /// </summary>
    [System.NonSerialized] public bool DebugGodMode = false;
#endif

    // 事件
    public System.Action<int, int> OnHealthChanged; // (当前, 最大)
    public System.Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isInvincible)
        {
            invincibleTimer -= Time.deltaTime;

            // 闪烁效果（S216：被打中的前 LaunchFeel.hurtFlash 秒先整个人红白闪一下 = "哎哟"的冲击点，之后才是无敌半透明闪烁）
            float alpha = Mathf.PingPong(Time.time / blinkInterval, 1f) > 0.5f ? 1f : 0.3f;
            float tint = Step1Feel.HurtTint(Time.time - hitAt, LaunchFeel.hurtFlash);
            if (spriteRenderer != null)
            {
                Color c;
                if (tint > 0f) { tinting = true; c = Color.Lerp(baseColor, (Mathf.FloorToInt((Time.time - hitAt) / 0.045f) % 2 == 0) ? Color.white : new Color(1f, 0.25f, 0.2f), tint); c.a = 1f; }
                else
                {
                    // 闪完只恢复一次原色，之后和以前一样只改透明度（不覆盖别的系统的颜色效果）
                    if (tinting) { tinting = false; c = baseColor; } else c = spriteRenderer.color;
                    c.a = alpha;
                }
                spriteRenderer.color = c;
            }

            if (invincibleTimer <= 0)
            {
                isInvincible = false;
                if (spriteRenderer != null)
                {
                    Color c = tinting ? baseColor : spriteRenderer.color;
                    tinting = false;
                    c.a = 1f;
                    spriteRenderer.color = c;
                }
            }
        }
    }

    /// <summary>受到伤害</summary>
    public void TakeDamage(int damage = 1)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // [AI防坑警告] God Mode 拦截：仅在调试开关开启时跳过伤害，默认关闭，不影响自动化测试
        if (DebugGodMode) return;
#endif
        if (isInvincible || currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        hitAt = Time.time;
        if (spriteRenderer != null && !isInvincible) baseColor = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);
        Step1Fx.Burst((Vector2)transform.position + Vector2.up * 0.5f, 6, new Color(1f, 0.95f, 0.5f, 1f), 5f, Vector2.up, 200f, 10f, 0.12f, 0.3f); // S216：受击星星
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

    /// <summary>S220：直接设成 n 颗心（从小镇带进房间用）。夹在 1..max，不触发受伤闪烁 / 无敌。</summary>
    public void SetCurrent(int n)
    {
        currentHealth = Mathf.Clamp(n, 1, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>重置生命值</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isInvincible = false;
        hitAt = -10f;
        if (spriteRenderer != null && tinting) { var c = baseColor; c.a = 1f; spriteRenderer.color = c; }
        tinting = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
