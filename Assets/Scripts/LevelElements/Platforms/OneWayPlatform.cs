using System.Collections;
using UnityEngine;

/// <summary>
/// 单向平台 - 关卡设计系统 · 平台类
/// 
/// 框架定位: LevelElementBase（不需要Trickster操控的纯关卡元素）
/// 分类: Platform | 标签: AffectsPhysics
/// 
/// 功能:
///   - 玩家可从下方穿过，落在上方
///   - 按 S+Jump（下+跳）组合键可从平台上方穿过落下
///   - 使用 PlatformEffector2D 实现单向碰撞
/// 
/// Session 19 优化（参考行业最佳实践）:
///   - 改为 S+Jump 组合键触发下落（之前是单独按S，容易误操作）
///   - 参考 Super Smash Bros / 大多数2D平台游戏的标准做法：Down+Jump = 穿越平台
///   - InputManager 在检测到 S+Space 同时按下时调用 AllowDropThrough()
///   - 单独按 S 不再触发下落，避免移动时误操作
///
/// S41 修复（终极方案 — 定向忽略碰撞）:
///   - S37 视碰分离后 Root.localScale 保持 (1,1,1)，碰撞体实际厚度从 0.075 变为 0.25
///   - 原 rotationalOffset=180 翻转方案在碰撞体变厚后失效
///   - collider.enabled=false 方案会让平台对整个物理世界消失（敌人/Trickster 也会掉落）
///   - 终极方案：Physics2D.IgnoreCollision 定向忽略 Mario↔平台 的碰撞
///     只有触发下落的 Mario 穿过，其他实体（敌人、Trickster）不受影响
///   - 参考《空洞骑士》《蔚蓝》等顶尖 2D 平台跳跃游戏的标准做法
/// 
/// // [AI防坑警告] 不要用 collider.enabled=false 或 rotationalOffset=180 实现下落！
/// // collider.enabled=false 会让平台对所有物体消失，多实体场景下是灾难。
/// // rotationalOffset=180 在 S37 视碰分离后碰撞体变厚时会卡脚。
/// // 唯一正确方案是 Physics2D.IgnoreCollision 定向忽略特定玩家碰撞体。
/// 
/// 扩展/删除指南: 删除此文件不影响其他脚本
/// Session 15: 关卡设计系统新增
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(PlatformEffector2D))]
public class OneWayPlatform : LevelElementBase
{
    [Header("=== 单向平台设置 ===")]
    [Tooltip("定向忽略碰撞的持续时长（秒），需足够让玩家穿过碰撞体厚度")]
    [SerializeField] private float dropThroughDuration = 0.35f;

    // 组件
    private BoxCollider2D boxCollider;
    private PlatformEffector2D effector;

    private static readonly System.Collections.Generic.List<RaycastHit2D> surfaceHits =
        new System.Collections.Generic.List<RaycastHit2D>(16);

    // Controller casts do not inherit PlatformEffector2D or per-pair IgnoreCollision rules.
    // Keep both players consistent: query all hits so a passable hit cannot hide a solid one.
    public static bool HasBlockingSurface(Collider2D body, Vector2 direction, float distance,
        LayerMask mask, float verticalVelocity)
    {
        bool previous = Physics2D.queriesStartInColliders;
        try
        {
            Physics2D.queriesStartInColliders = false;
            var bounds = body.bounds;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(mask);
            surfaceHits.Clear();
            Physics2D.BoxCast(bounds.center, new Vector2(bounds.size.x * 0.9f, bounds.size.y),
                0f, direction, filter, surfaceHits, distance);
            foreach (var hit in surfaceHits)
            {
                var other = hit.collider;
                if (other == null || other == body || other.isTrigger ||
                    (body.attachedRigidbody != null && other.attachedRigidbody == body.attachedRigidbody) ||
                    Physics2D.GetIgnoreCollision(body, other) || Vector2.Dot(hit.normal, direction) > -0.5f)
                    continue;
                var platform = other.GetComponent<PlatformEffector2D>();
                if (other.usedByEffector && platform != null && platform.enabled && platform.useOneWay)
                {
                    // Authored one-way platforms are horizontal. Never truncate an upward jump,
                    // nor re-ground the player while rising through or dropping through the deck.
                    if (direction.y > 0f || verticalVelocity > 0f || bounds.min.y < other.bounds.max.y - 0.05f)
                        continue;
                }
                return true;
            }
            return false;
        }
        finally { surfaceHits.Clear(); Physics2D.queriesStartInColliders = previous; }
    }

    private void Awake()
    {
        elementName = "单向平台";
        category = ElementCategory.Platform;
        tags = ElementTag.AffectsPhysics;
        description = "可从下方穿过的平台，按 S+Jump 组合键可落下";

        boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.usedByEffector = true;

        effector = GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 170f; // 允许从侧面略微进入
    }

    /// <summary>
    /// 允许指定玩家从平台上方穿过落下（定向忽略碰撞）
    /// 由 MarioInteractionHelper.TryDropThroughPlatform 调用，传入 Mario 的碰撞体
    /// 
    /// S41: 使用 Physics2D.IgnoreCollision 只让触发者穿过，
    ///       平台对其他所有实体（敌人、Trickster 等）保持坚固
    /// </summary>
    /// <param name="playerCollider">触发下落的玩家碰撞体</param>
    // Only own pairs that were colliding before our request. Do not clear another system's ignore.
    private readonly System.Collections.Generic.Dictionary<Collider2D, Coroutine> drops =
        new System.Collections.Generic.Dictionary<Collider2D, Coroutine>();

    public void AllowDropThrough(Collider2D playerCollider)
    {
        if (!isActiveAndEnabled || playerCollider == null || boxCollider == null ||
            !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy || !boxCollider.enabled) return;
        if (drops.TryGetValue(playerCollider, out var previous)) StopCoroutine(previous);
        else if (Physics2D.GetIgnoreCollision(playerCollider, boxCollider)) return;
        Physics2D.IgnoreCollision(playerCollider, boxCollider, true);
        drops[playerCollider] = StartCoroutine(DropThroughRoutine(playerCollider));
    }

    private IEnumerator DropThroughRoutine(Collider2D playerCollider)
    {
        yield return new WaitForSeconds(dropThroughDuration);
        RestorePair(playerCollider);
        drops.Remove(playerCollider);
    }

    private void RestorePair(Collider2D playerCollider)
    {
        if (playerCollider != null && boxCollider != null)
            Physics2D.IgnoreCollision(playerCollider, boxCollider, false);
    }

    private void CancelDrops()
    {
        // [AI防坑警告] 停协程不会恢复 IgnoreCollision；重复请求要续期，重置/禁用要释放本组件拥有的关系。
        foreach (var drop in drops)
        {
            StopCoroutine(drop.Value);
            RestorePair(drop.Key);
        }
        drops.Clear();
    }

    protected override void OnDisable()
    {
        CancelDrops();
        base.OnDisable();
    }

    public override void OnLevelReset()
    {
        if (boxCollider != null) boxCollider.enabled = true;
        CancelDrops();
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
