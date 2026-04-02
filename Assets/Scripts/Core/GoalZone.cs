using UnityEngine;

/// <summary>
/// 终点区域触发器
/// 功能: Mario碰触后触发胜利判定
/// 使用方式: 挂载到终点旗帜/门的GameObject上，需要BoxCollider2D(isTrigger=true)
///
/// Session 11 修复 B017：到达终点无胜利提示
///   根因分析：OnTriggerEnter2D 可能因以下原因未触发：
///     1. Mario 缺少 "Player" 标签且 GetComponent 在 Collider2D 上找不到（子物体情况）
///     2. Rigidbody2D 的 bodyType 或 simulated 设置问题
///     3. Layer 碰撞矩阵未启用
///   修复：
///     - 同时使用 OnTriggerEnter2D 和 OnTriggerStay2D（双保险）
///     - 检测时同时查 CompareTag("Player")、自身 GetComponent、父物体 GetComponentInParent
///     - 添加详细 Debug.Log 帮助排查
///     - Awake 中添加自检日志
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GoalZone : MonoBehaviour
{
    [Header("=== 视觉效果 ===")]
    [SerializeField] private GameObject victoryVFXPrefab;

    private bool triggered = false;
    private BoxCollider2D col;

    /// <summary>重置触发状态（由 GameManager.ResetRound 在新回合开始时调用）</summary>
    public void ResetTrigger()
    {
        triggered = false;
        Debug.Log("[GoalZone] 触发状态已重置，等待新回合");
    }

    private void Awake()
    {
        // 确保Collider是Trigger
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;

        Debug.Log($"[GoalZone] 初始化完成 | 位置: {transform.position} | Collider大小: {col.size} | isTrigger: {col.isTrigger}");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[GoalZone] OnTriggerEnter2D 触发 | 碰撞物体: {other.gameObject.name} | Tag: {other.tag}");
        TryTriggerGoal(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 双保险：如果 Enter 没触发，Stay 也能捕获
        TryTriggerGoal(other);
    }

    private void TryTriggerGoal(Collider2D other)
    {
        if (triggered) return;

        // 多种方式检测 Mario（防止标签未设置或组件在父物体上）
        bool isMario = false;

        // 方式1：标签检测
        if (other.CompareTag("Player"))
        {
            isMario = true;
            Debug.Log("[GoalZone] 通过 Player 标签检测到 Mario");
        }

        // 方式2：直接获取组件
        if (!isMario && other.GetComponent<MarioController>() != null)
        {
            isMario = true;
            Debug.Log("[GoalZone] 通过 GetComponent<MarioController> 检测到 Mario");
        }

        // 方式3：从父物体获取（如果 Collider 在子物体上）
        if (!isMario && other.GetComponentInParent<MarioController>() != null)
        {
            isMario = true;
            Debug.Log("[GoalZone] 通过 GetComponentInParent<MarioController> 检测到 Mario");
        }

        // 方式4：名称包含 Mario（最后的兜底）
        if (!isMario && other.gameObject.name.ToLower().Contains("mario"))
        {
            isMario = true;
            Debug.Log("[GoalZone] 通过名称匹配检测到 Mario");
        }

        if (isMario)
        {
            triggered = true;
            Debug.Log("[GoalZone] ★ Mario 到达终点！触发胜利判定 ★");

            // 播放特效
            if (victoryVFXPrefab != null)
            {
                Instantiate(victoryVFXPrefab, transform.position, Quaternion.identity);
            }

            // 通知GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMarioReachedGoal();
                Debug.Log("[GoalZone] 已通知 GameManager");
            }
            else
            {
                Debug.LogError("[GoalZone] GameManager.Instance 为 null！无法触发胜利判定");
            }
        }
    }

    /// <summary>可视化终点区域（Scene视图中显示绿色方框）</summary>
    private void OnDrawGizmos()
    {
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 center = transform.position + (Vector3)boxCol.offset;
            Vector3 size = boxCol.size;
            Gizmos.DrawCube(center, size);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
