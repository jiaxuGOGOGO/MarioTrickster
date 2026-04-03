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
    private Vector3 initialPosition;
    private Color initialColor;

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
        sr = GetComponent<SpriteRenderer>();
        initialPosition = transform.localPosition;
        initialColor = sr != null ? sr.color : Color.white;
    }

    protected override void Update()
    {
        base.Update();

        switch (state)
        {
            case CollapseState.Shaking:
                // 震动效果
                float shakeX = Mathf.Sin(Time.time * shakeFrequency) * shakeIntensity;
                transform.localPosition = initialPosition + new Vector3(shakeX, 0, 0);

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

        // 检查是否从上方踩踏
        ContactPoint2D contact = collision.GetContact(0);
        if (contact.normal.y < -0.5f && collision.gameObject.GetComponent<MarioController>() != null)
        {
            StartShaking();
        }
    }

    private void StartShaking()
    {
        state = CollapseState.Shaking;
        collapseTimer = collapseDelay;
        Debug.Log($"[CollapsingPlatform] {gameObject.name} 开始震动，{collapseDelay}秒后崩塌");
    }

    private void Collapse()
    {
        state = CollapseState.Collapsed;
        collapseTimer = respawnDelay;
        boxCollider.enabled = false;
        transform.localPosition = initialPosition;

        if (sr != null) sr.color = new Color(initialColor.r, initialColor.g, initialColor.b, 0f);

        tricksterForceCollapse = false;
        Debug.Log($"[CollapsingPlatform] {gameObject.name} 已崩塌");
    }

    private void Respawn()
    {
        state = CollapseState.Respawning;
        collapseTimer = 0.5f; // 渐显时间
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
        transform.localPosition = initialPosition;
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
