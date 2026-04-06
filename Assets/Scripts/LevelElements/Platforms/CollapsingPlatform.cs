using UnityEngine;

/// <summary>
/// 崩塌平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: Platform | 标签: Controllable, OneShot, Resettable
/// 
/// 功能:
///   - 玩家踩上后开始震动，延迟后崩塌消失
///   - 崩塌后经过恢复时间自动重生（可配置）
///   - Trickster操控: 提前触发崩塌或延长延迟时间
/// 
/// Session 17 更新:
///   - 修复崩塌后重生位置：使用当前 localPosition 作为重生基准
///     移动平台后重生在新位置，不会回到初始位置
///   - 修复 Trickster 也能触发崩塌：OnCollisionEnter2D 不再限制只有 Mario
///     任何有 Rigidbody2D 的对象从上方踩踏都能触发
///   - Trickster 按 L 键也能远程触发崩塌（通过 OnActivate）
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CollapsingPlatform : ControllableLevelElement
{
    [Header("=== 崩塌设置 ===")]
    [SerializeField] private float collapseDelay = 1f;
    [SerializeField] private float respawnDelay = 5f;
    [SerializeField] private bool canRespawn = true;

    [Header("=== 震动设置 ===")]
    [SerializeField] private float shakeIntensity = 0.05f;
    [SerializeField] private float shakeFrequency = 20f;

    // 组件
    private BoxCollider2D boxCollider;
    private SpriteRenderer sr;

    // 状态
    private enum CollapseState { Stable, Shaking, Collapsed, Respawning }
    private CollapseState state = CollapseState.Stable;
    private float collapseTimer;
    private Color initialColor;

    // Session 17: 使用 stablePosition 记录震动前的稳定位置（而非 Awake 时的初始位置）
    // 这样移动平台后崩塌/重生都基于当前位置
    private Vector3 stablePosition;

    // Trickster覆盖
    private bool tricksterForceCollapse;

    protected override void Awake()
    {
        propName = "崩塌平台";
        elementCategory = ElementCategory.Platform;
        elementTags = ElementTag.Controllable | ElementTag.OneShot | ElementTag.Resettable;
        elementDescription = "踩上后延迟崩塌的平台";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        sr = GetComponentInChildren<SpriteRenderer>();
        stablePosition = transform.localPosition;
        initialColor = sr != null ? sr.color : Color.white;
    }

    protected override void Update()
    {
        base.Update();

        switch (state)
        {
            case CollapseState.Stable:
                // Session 17: 在稳定状态持续更新 stablePosition
                // 这样如果平台被移动（编辑器/代码），重生位置跟着更新
                stablePosition = transform.localPosition;
                break;

            case CollapseState.Shaking:
                // 震动效果（基于 stablePosition 偏移）
                float shakeX = Mathf.Sin(Time.time * shakeFrequency) * shakeIntensity;
                transform.localPosition = stablePosition + new Vector3(shakeX, 0, 0);

                // 颜色渐变提示
                if (sr != null)
                {
                    float progress = 1f - (collapseTimer / collapseDelay);
                    sr.color = Color.Lerp(initialColor, new Color(1, 0.3f, 0.3f, 0.5f), progress);
                }

                collapseTimer -= Time.deltaTime;
                if (collapseTimer <= 0f || tricksterForceCollapse)
                {
                    Collapse();
                }
                break;

            case CollapseState.Collapsed:
                if (canRespawn)
                {
                    collapseTimer -= Time.deltaTime;
                    if (collapseTimer <= 0f)
                    {
                        Respawn();
                    }
                }
                break;

            case CollapseState.Respawning:
                // 渐显动画
                collapseTimer -= Time.deltaTime;
                if (sr != null)
                {
                    float alpha = 1f - (collapseTimer / 0.5f);
                    sr.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                }
                if (collapseTimer <= 0f)
                {
                    state = CollapseState.Stable;
                    if (sr != null) sr.color = initialColor;
                    boxCollider.enabled = true;
                }
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state != CollapseState.Stable) return;

        // Session 17: 不再限制只有 Mario 才能触发
        // 任何有 Rigidbody2D 的对象从上方踩踏都能触发崩塌
        ContactPoint2D contact = collision.GetContact(0);
        if (contact.normal.y < -0.5f && collision.gameObject.GetComponent<Rigidbody2D>() != null)
        {
            StartShaking();
        }
    }

    private void StartShaking()
    {
        // 记录开始震动时的位置作为稳定位置
        stablePosition = transform.localPosition;
        state = CollapseState.Shaking;
        collapseTimer = collapseDelay;
        Debug.Log($"[CollapsingPlatform] {gameObject.name} 开始震动，{collapseDelay}秒后崩塌");
    }

    private void Collapse()
    {
        state = CollapseState.Collapsed;
        collapseTimer = respawnDelay;
        boxCollider.enabled = false;

        // Session 17: 崩塌时回到 stablePosition（震动前的位置）
        transform.localPosition = stablePosition;

        if (sr != null) sr.color = new Color(initialColor.r, initialColor.g, initialColor.b, 0f);

        tricksterForceCollapse = false;
        Debug.Log($"[CollapsingPlatform] {gameObject.name} 已崩塌");
    }

    private void Respawn()
    {
        state = CollapseState.Respawning;
        collapseTimer = 0.5f; // 渐显时间

        // Session 17: 重生在 stablePosition（即崩塌前的位置）
        transform.localPosition = stablePosition;

        Debug.Log($"[CollapsingPlatform] {gameObject.name} 开始重生");
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        if (state == CollapseState.Stable)
        {
            StartShaking();
            tricksterForceCollapse = true; // 立即崩塌
        }
        else if (state == CollapseState.Shaking)
        {
            tricksterForceCollapse = true;
        }
    }

    protected override void OnActiveEnd()
    {
        tricksterForceCollapse = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        state = CollapseState.Stable;
        collapseTimer = 0f;
        boxCollider.enabled = true;
        // Session 17: 重置时回到 stablePosition
        transform.localPosition = stablePosition;
        if (sr != null) sr.color = initialColor;
        tricksterForceCollapse = false;
    }

    private void OnDrawGizmos()
    {
        Color c = state == CollapseState.Stable ? new Color(0.8f, 0.6f, 0.2f, 0.5f) :
                  state == CollapseState.Shaking ? new Color(1f, 0.3f, 0.3f, 0.5f) :
                  new Color(0.3f, 0.3f, 0.3f, 0.2f);
        Gizmos.color = c;
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
    }
}
