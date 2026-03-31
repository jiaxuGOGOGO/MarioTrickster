using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 伪装/变身系统 - MVP核心脚本
/// 参考: Among-Us-Imposter + PropHunt概念
/// 功能: Trickster可变身为预设的场景物体（砖块、管道、怪物等）
/// 实现: 切换SpriteRenderer的sprite + 调整Collider尺寸 + 可选行为
/// </summary>
public class DisguiseSystem : MonoBehaviour
{
    [Header("=== 伪装配置 ===")]
    [SerializeField] private List<DisguiseData> availableDisguises = new List<DisguiseData>();
    [SerializeField] private int currentDisguiseIndex = 0;

    [Header("=== 伪装参数 ===")]
    [Tooltip("变身冷却时间（秒）")]
    [SerializeField] private float disguiseCooldown = 2f;
    [Tooltip("变身持续时间（0=无限）")]
    [SerializeField] private float disguiseDuration = 0f;
    [Tooltip("静止多久后完全融入场景（秒）")]
    [SerializeField] private float blendInTime = 1.5f;

    [Header("=== 视觉效果 ===")]
    [SerializeField] private GameObject transformVFXPrefab;

    // 组件
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;

    // 原始数据（用于还原）
    private Sprite originalSprite;
    private Vector2 originalColliderSize;
    private Vector2 originalColliderOffset;
    private Vector3 originalScale;

    // 状态
    private bool isDisguised;
    private float cooldownTimer;
    private float durationTimer;
    private float stillTimer; // 静止计时
    private bool isFullyBlended; // 是否完全融入场景
    private Vector3 lastPosition;

    // 公共属性
    public bool IsDisguised => isDisguised;
    public bool IsFullyBlended => isFullyBlended;
    public DisguiseData CurrentDisguise => availableDisguises.Count > 0 ? availableDisguises[currentDisguiseIndex] : null;
    public float CooldownRemaining => cooldownTimer;
    public float CooldownProgress => disguiseCooldown > 0 ? 1f - (cooldownTimer / disguiseCooldown) : 1f;

    // 事件
    public System.Action<bool> OnDisguiseChanged; // true=变身, false=还原
    public System.Action<DisguiseData> OnDisguiseSelected; // 切换选中的伪装

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();

        // 保存原始数据
        originalSprite = spriteRenderer.sprite;
        originalColliderSize = boxCollider.size;
        originalColliderOffset = boxCollider.offset;
        originalScale = transform.localScale;
    }

    private void Update()
    {
        // 冷却计时
        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 持续时间计时
        if (isDisguised && disguiseDuration > 0)
        {
            durationTimer -= Time.deltaTime;
            if (durationTimer <= 0)
            {
                Undisguise();
            }
        }

        // 静止融入检测
        if (isDisguised)
        {
            float moved = Vector3.Distance(transform.position, lastPosition);
            if (moved < 0.01f)
            {
                stillTimer += Time.deltaTime;
                if (stillTimer >= blendInTime && !isFullyBlended)
                {
                    isFullyBlended = true;
                    // 完全融入时可以改变透明度或添加其他效果
                    SetBlendedVisual(true);
                }
            }
            else
            {
                stillTimer = 0f;
                if (isFullyBlended)
                {
                    isFullyBlended = false;
                    SetBlendedVisual(false);
                }
            }
            lastPosition = transform.position;
        }
    }

    #region 公共方法

    /// <summary>切换伪装状态</summary>
    public void ToggleDisguise()
    {
        if (isDisguised)
        {
            Undisguise();
        }
        else
        {
            Disguise();
        }
    }

    /// <summary>执行变身</summary>
    public void Disguise()
    {
        if (isDisguised || cooldownTimer > 0) return;
        if (availableDisguises.Count == 0) return;

        DisguiseData data = availableDisguises[currentDisguiseIndex];
        if (data == null || data.disguiseSprite == null) return;

        isDisguised = true;
        durationTimer = disguiseDuration;
        stillTimer = 0f;
        isFullyBlended = false;
        lastPosition = transform.position;

        // 切换外观
        spriteRenderer.sprite = data.disguiseSprite;
        spriteRenderer.color = Color.white; // 确保颜色正常

        // 调整碰撞体
        if (data.customColliderSize != Vector2.zero)
        {
            boxCollider.size = data.customColliderSize;
            boxCollider.offset = data.customColliderOffset;
        }

        // 调整缩放
        if (data.customScale != Vector3.zero)
        {
            transform.localScale = data.customScale;
        }

        // 变身特效
        SpawnVFX();

        OnDisguiseChanged?.Invoke(true);
    }

    /// <summary>解除变身</summary>
    public void Undisguise()
    {
        if (!isDisguised) return;

        isDisguised = false;
        isFullyBlended = false;
        cooldownTimer = disguiseCooldown;

        // 还原外观
        spriteRenderer.sprite = originalSprite;
        spriteRenderer.color = Color.white;

        // 还原碰撞体
        boxCollider.size = originalColliderSize;
        boxCollider.offset = originalColliderOffset;

        // 还原缩放
        transform.localScale = originalScale;

        // 变身特效
        SpawnVFX();

        OnDisguiseChanged?.Invoke(false);
    }

    /// <summary>选择下一个伪装形态</summary>
    public void NextDisguise()
    {
        if (availableDisguises.Count == 0) return;
        currentDisguiseIndex = (currentDisguiseIndex + 1) % availableDisguises.Count;
        OnDisguiseSelected?.Invoke(availableDisguises[currentDisguiseIndex]);
    }

    /// <summary>选择上一个伪装形态</summary>
    public void PreviousDisguise()
    {
        if (availableDisguises.Count == 0) return;
        currentDisguiseIndex--;
        if (currentDisguiseIndex < 0) currentDisguiseIndex = availableDisguises.Count - 1;
        OnDisguiseSelected?.Invoke(availableDisguises[currentDisguiseIndex]);
    }

    #endregion

    #region 内部方法

    private void SetBlendedVisual(bool blended)
    {
        // 完全融入场景时，移除任何可能暴露身份的视觉提示
        // MVP阶段：简单地调整sorting order使其与场景物体一致
        if (blended)
        {
            spriteRenderer.sortingOrder = 0; // 与场景物体同层
        }
        else
        {
            spriteRenderer.sortingOrder = 5; // 恢复到角色层
        }
    }

    private void SpawnVFX()
    {
        if (transformVFXPrefab != null)
        {
            GameObject vfx = Instantiate(transformVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 1f);
        }
    }

    #endregion
}

/// <summary>
/// 伪装数据 - 定义一种可变身的形态
/// 在Inspector中配置，或用ScriptableObject管理
/// </summary>
[System.Serializable]
public class DisguiseData
{
    [Header("基本信息")]
    public string disguiseName = "Brick Block";
    public Sprite disguiseSprite;
    public Sprite iconSprite; // UI显示用的图标

    [Header("碰撞体调整")]
    public Vector2 customColliderSize = Vector2.zero; // (0,0)表示不调整
    public Vector2 customColliderOffset = Vector2.zero;

    [Header("缩放调整")]
    public Vector3 customScale = Vector3.zero; // (0,0,0)表示不调整

    [Header("伪装类型")]
    public DisguiseType type = DisguiseType.Static;
}

/// <summary>伪装类型枚举</summary>
public enum DisguiseType
{
    Static,     // 静态物体（砖块、管道等）
    Enemy,      // 伪装为敌人（可以有简单AI行为）
    Hazard      // 伪装为危险物（尖刺等，接触即伤害Mario）
}
