using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 死亡区域触发器（掉落深渊等）
/// 功能: 任何带有PlayerHealth的角色碰触后直接死亡
/// 使用方式: 挂载到关卡底部的长条GameObject上，需要BoxCollider2D(isTrigger=true)
///
/// S48b: 三重死亡检测 —
///   1. OnTriggerEnter2D: 标准进入检测
///   2. OnTriggerStay2D: 安全网（防止 Enter 被跳过的边界情况）
///   3. Update fallback: Y 坐标兜底（防止 Trigger 完全不触发的极端情况）
///
/// S48c: 防刷屏修复 —
///   - 每个角色只触发一次 TakeDamage，用 HashSet 记录已击杀对象
///   - 死亡后冻结角色 Rigidbody2D（防止继续下坠刷日志）
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
public class KillZone : MonoBehaviour
{
    [SerializeField] private int damage = 999;

    // [AI防坑警告] fallbackY 是 Update 兜底检测的 Y 阈值。
    // 当角色低于此 Y 值时直接判定死亡，不依赖 Trigger 碰撞。
    // 默认 -15 足够低，不会误杀正常游戏中的角色。
    // TestConsoleWindow 创建 KillZone 时会自动设置此值为 KillZone.position.y - 5。
    [SerializeField] private float fallbackY = -15f;

    // P2: 缓存 PlayerHealth 引用，避免每帧 FindObjectsOfType
    private PlayerHealth[] cachedPlayers;

    // S48c: 已击杀角色集合，防止同一角色被重复 TakeDamage 导致日志刷屏
    private readonly HashSet<PlayerHealth> killedSet = new HashSet<PlayerHealth>();

    /// <summary>设置 Y 坐标兜底阈值（由 TestConsoleWindow / TestSceneBuilder 调用）</summary>
    public void SetFallbackY(float y) { fallbackY = y; }

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void Start()
    {
        // 缓存场景中所有 PlayerHealth（Mario + Trickster）
        cachedPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other);
    }

    // S48b: 安全网 — 防止高速穿越或物理引擎跳帧导致 Enter 未触发
    private void OnTriggerStay2D(Collider2D other)
    {
        TryKill(other);
    }

    private void Update()
    {
        // S48b: Y 坐标兜底 — 检查缓存的 PlayerHealth 实例，
        // 如果任何角色低于 fallbackY 则直接判定死亡。
        // 这是最后一道防线，即使 Trigger 完全失效也能兜底。
        if (cachedPlayers == null) return;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            PlayerHealth health = cachedPlayers[i];
            if (health != null && !killedSet.Contains(health) &&
                health.transform.position.y < fallbackY)
            {
                Kill(health);
            }
        }
    }

    private void TryKill(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && !killedSet.Contains(health))
        {
            Kill(health);
        }
    }

    /// <summary>
    /// S48c: 统一击杀入口 — 确保每个角色只被处理一次
    /// </summary>
    private void Kill(PlayerHealth health)
    {
        if (health.CurrentHealth <= 0) return;

        killedSet.Add(health);
        health.TakeDamage(damage);
        Debug.Log($"[KillZone] {health.gameObject.name} 掉入深渊！");

        // S48c: 冻结角色 Rigidbody，防止尸体继续下坠刷日志
        Rigidbody2D rb = health.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }
    }
}
