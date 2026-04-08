using UnityEngine;

/// <summary>
/// 可破坏方块 — 从下方撞击可破坏的方块 (S56)
///
/// 设计参考：
///   - Super Mario Bros: 问号方块/砖块，从下方顶碎
///   - Shovel Knight: 可破坏地形揭示隐藏路径
///
/// ASCII 字符: 'X'
/// 行为: 玩家从下方跳跃撞击时破坏，消失后露出通路
///        继承 LevelElementBase 获得自动注册
///
/// 碰撞检测逻辑：
///   使用 OnCollisionEnter2D，当接触法线指向下方（玩家从下方撞击）时触发破坏。
///   这与 Mario 系列的经典"顶砖块"操作一致。
/// </summary>
public class BreakableBlock : LevelElementBase
{
    [Header("=== 破坏参数 ===")]
    [SerializeField] private float breakThreshold = 0.3f;

    private bool isBroken;
    private Vector3 originalPosition;
    private Transform visualTransform;

    protected override void OnEnable()
    {
        elementName = "BreakableBlock";
        category = ElementCategory.Platform;
        tags = ElementTag.OneShot | ElementTag.Interactive;
        base.OnEnable();
    }

    private void Awake()
    {
        originalPosition = transform.position;
        Transform vis = transform.Find("Visual");
        if (vis != null)
            visualTransform = vis;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isBroken) return;

        // 只有玩家可以破坏
        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        if (mario == null) return;

        // 检查是否从下方撞击（接触法线指向下方 = 玩家在下面）
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -breakThreshold)
            {
                Break();
                return;
            }
        }
    }

    private void Break()
    {
        isBroken = true;
        Debug.Log($"[BreakableBlock] Broken at {transform.position}");

        // 视觉反馈 + 禁用碰撞
        StartCoroutine(BreakAnimation());
    }

    private System.Collections.IEnumerator BreakAnimation()
    {
        // 禁用碰撞体（立即让出通路）
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = false;

        // 简单的缩小消失动画
        if (visualTransform != null)
        {
            Vector3 originalScale = visualTransform.localScale;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(1f, 0f, t / 0.2f);
                visualTransform.localScale = originalScale * scale;
                yield return null;
            }
            visualTransform.gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public override void OnLevelReset()
    {
        isBroken = false;
        transform.position = originalPosition;

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = true;

        if (visualTransform != null)
        {
            visualTransform.gameObject.SetActive(true);
            // 恢复原始缩放
            visualTransform.localScale = Vector3.one;
        }
        else
        {
            gameObject.SetActive(true);
        }
    }
}
