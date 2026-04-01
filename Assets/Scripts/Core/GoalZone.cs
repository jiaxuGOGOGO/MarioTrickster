using UnityEngine;

/// <summary>
/// 终点区域触发器
/// 功能: Mario碰触后触发胜利判定
/// 使用方式: 挂载到终点旗帜/门的GameObject上，需要BoxCollider2D(isTrigger=true)
/// Mario的GameObject需要有"Mario"标签
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GoalZone : MonoBehaviour
{
    [Header("=== 视觉效果 ===")]
    [SerializeField] private GameObject victoryVFXPrefab;

    private bool triggered = false;

    private void Awake()
    {
        // 确保Collider是Trigger
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        // 检查是否是Mario
        if (other.CompareTag("Player") || other.GetComponent<MarioController>() != null)
        {
            triggered = true;

            Debug.Log("[GoalZone] Mario 到达终点！");

            // 播放特效
            if (victoryVFXPrefab != null)
            {
                Instantiate(victoryVFXPrefab, transform.position, Quaternion.identity);
            }

            // 通知GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMarioReachedGoal();
            }
        }
    }
}
