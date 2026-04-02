using UnityEngine;

/// <summary>
/// 单向平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: LevelElementBase（不需要Trickster操控的纯关卡元素）
/// 分类: Platform | 标签: AffectsPhysics
/// 
/// 功能:
///   - 玩家可从下方穿过，落在上方
///   - 按下蹲键可从平台上方穿过落下
///   - 使用 PlatformEffector2D 实现单向碰撞
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : LevelElementBase
{
    [Header("=== 单向平台设置 ===")]
    [Tooltip("玩家按下蹲键后禁用碰撞的时长")]
    [SerializeField] private float dropThroughDuration = 0.3f;

    // 组件
    private BoxCollider2D boxCollider;
    private PlatformEffector2D effector;

    // 状态
    private bool isDropThrough;
    private float dropTimer;

    private void Awake()
    {
        elementName = "单向平台";
        category = ElementCategory.Platform;
        tags = ElementTag.AffectsPhysics;
        description = "可从下方穿过的平台，按下蹲键可落下";

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.usedByEffector = true;

        effector = GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f; // 允许从侧面略微进入
    }

    private void Update()
    {
        if (isDropThrough)
        {
            dropTimer -= Time.deltaTime;
            if (dropTimer <= 0f)
            {
                isDropThrough = false;
                effector.rotationalOffset = 0f;
            }
        }
    }

    /// <summary>
    /// 允许玩家从平台上方穿过落下
    /// 由 MarioController 在检测到下蹲输入时调用
    /// </summary>
    public void AllowDropThrough()
    {
        if (isDropThrough) return;
        isDropThrough = true;
        dropTimer = dropThroughDuration;
        effector.rotationalOffset = 180f; // 翻转碰撞方向
    }

    public override void OnLevelReset()
    {
        isDropThrough = false;
        dropTimer = 0f;
        effector.rotationalOffset = 0f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.5f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, new Vector3(col.size.x, col.size.y * 0.3f, 0));
        }
        // 方向箭头（从下到上）
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.8f);
        Gizmos.DrawRay(transform.position + Vector3.down * 0.5f, Vector3.up * 1f);
    }
}
