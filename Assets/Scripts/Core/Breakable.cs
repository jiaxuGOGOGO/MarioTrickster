using UnityEngine;

/// <summary>
/// 可破坏/可交互方块（问号砖块、普通砖块等）
/// 功能: Mario从下方顶撞时触发效果（掉落物品/破碎）
/// 使用方式: 挂载到砖块GameObject上，需要BoxCollider2D
/// </summary>
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
public class Breakable : MonoBehaviour
{
    public enum BlockType
    {
        Brick,      // 普通砖块（可破坏）
        QuestionBlock, // 问号砖块（出道具后变空）
        Unbreakable // 不可破坏（仅有顶撞反馈）
    }

    [Header("=== 方块设置 ===")]
    [SerializeField] private BlockType blockType = BlockType.Brick;
    [SerializeField] private int hitsToBreak = 1;

    [Header("=== 掉落物 ===")]
    [SerializeField] private GameObject dropItemPrefab;
    [SerializeField] private Vector3 dropOffset = new Vector3(0, 1f, 0);

    [Header("=== 视觉效果 ===")]
    [SerializeField] private GameObject breakVFXPrefab;
    [SerializeField] private Sprite emptyBlockSprite; // 问号砖块被顶后的空状态

    [Header("=== 顶撞动画 ===")]
    [SerializeField] private float bumpHeight = 0.3f;
    [SerializeField] private float bumpSpeed = 8f;

    private int currentHits;
    private bool isEmpty;
    private Vector3 originalPosition;
    private bool isBumping;
    private float bumpTimer;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        originalPosition = transform.position;
    }

    private void Update()
    {
        // 顶撞动画
        if (isBumping)
        {
            bumpTimer += Time.deltaTime * bumpSpeed;
            float yOffset = Mathf.Sin(bumpTimer * Mathf.PI) * bumpHeight;
            transform.position = originalPosition + new Vector3(0, yOffset, 0);

            if (bumpTimer >= 1f)
            {
                isBumping = false;
                transform.position = originalPosition;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isEmpty) return;

        // 检查是否从下方撞击
        MarioController mario = collision.gameObject.GetComponent<MarioController>();
        if (mario == null) return;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) // Mario在下方
            {
                OnHitFromBelow();
                break;
            }
        }
    }

    private void OnHitFromBelow()
    {
        currentHits++;

        // 顶撞动画
        isBumping = true;
        bumpTimer = 0f;

        switch (blockType)
        {
            case BlockType.Brick:
                if (currentHits >= hitsToBreak)
                {
                    BreakBlock();
                }
                break;

            case BlockType.QuestionBlock:
                SpawnDropItem();
                isEmpty = true;
                if (emptyBlockSprite != null && spriteRenderer != null)
                {
                    spriteRenderer.sprite = emptyBlockSprite;
                }
                break;

            case BlockType.Unbreakable:
                // 只有顶撞反馈，不做其他事
                break;
        }
    }

    private void SpawnDropItem()
    {
        if (dropItemPrefab != null)
        {
            Instantiate(dropItemPrefab, transform.position + dropOffset, Quaternion.identity);
        }
    }

    private void BreakBlock()
    {
        if (breakVFXPrefab != null)
        {
            Instantiate(breakVFXPrefab, transform.position, Quaternion.identity);
        }

        Debug.Log($"[Breakable] {gameObject.name} 被破坏！");
        Destroy(gameObject);
    }
}
