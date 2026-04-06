using UnityEngine;

/// <summary>
/// 伪装墙壁 - 关卡设计系统 · 隐藏通道类
/// 
/// 框架定位: ControllableLevelElement
/// 分类: HiddenPassage | 标签: Controllable, Hidden, Disguisable
/// 
/// 功能:
///   - 看起来是普通墙壁，但玩家可以穿过
///   - 玩家穿过时墙壁变半透明，暗示可通过
///   - Trickster操控: 将伪装墙变为真实墙壁，阻挡Mario
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class FakeWall : ControllableLevelElement
{
    [Header("=== 伪装墙设置 ===")]
    [Tooltip("玩家穿过时的透明度")]
    [SerializeField] private float revealAlpha = 0.3f;

    [Tooltip("透明度过渡速度")]
    [SerializeField] private float fadeSpeed = 3f;

    // 组件
    private BoxCollider2D boxCollider;
    private SpriteRenderer sr;

    // 状态
    private bool playerInside;
    private float currentAlpha = 1f;
    private Color baseColor;

    // Trickster覆盖
    private bool tricksterSolidified;

    protected override void Awake()
    {
        propName = "伪装墙壁";
        elementCategory = ElementCategory.HiddenPassage;
        elementTags = ElementTag.Controllable | ElementTag.Hidden | ElementTag.Disguisable;
        elementDescription = "看起来是墙壁但可以穿过，Trickster可使其变为实体";

        base.Awake();

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = true; // 默认可穿过
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        sr = GetComponentInChildren<SpriteRenderer>();
        baseColor = sr != null ? sr.color : Color.white;
        currentAlpha = 1f;
    }

    protected override void Update()
    {
        base.Update();

        // Trickster操控：变为实体
        boxCollider.isTrigger = !tricksterSolidified;

        // 透明度过渡
        float targetAlpha = playerInside && !tricksterSolidified ? revealAlpha : 1f;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (sr != null)
        {
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, currentAlpha);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<MarioController>() != null)
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponent<MarioController>() != null)
        {
            playerInside = false;
        }
    }

    // ── ControllablePropBase 实现 ────────────────────────

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        tricksterSolidified = true;
        Debug.Log($"[FakeWall] Trickster将 {gameObject.name} 变为实体墙壁");
    }

    protected override void OnActiveEnd()
    {
        tricksterSolidified = false;
    }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        tricksterSolidified = false;
        playerInside = false;
        currentAlpha = 1f;
        boxCollider.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = tricksterSolidified ? new Color(1, 0, 0, 0.5f) : new Color(0.5f, 0.5f, 1f, 0.3f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
    }
}
