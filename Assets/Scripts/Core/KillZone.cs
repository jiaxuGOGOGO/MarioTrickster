using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 死亡区域触发器（掉落深渊等）
/// 功能: 任何带有PlayerHealth的角色碰触后直接死亡
/// 使用方式: 挂载到关卡底部的长条GameObject上，需要BoxCollider2D(isTrigger=true)
///
/// S48b: 三重死亡检测 —
///   1. OnTriggerEnter2D: 标准进入检测（继承自 BaseHazard）
///   2. OnTriggerStay2D: 安全网（继承自 BaseHazard）
///   3. Update fallback: Y 坐标兜底（KillZone 独有，防止 Trigger 完全不触发的极端情况）
///
/// S48c: 防刷屏修复 —
///   - 每个角色只触发一次 TakeDamage，由 BaseHazard 的 HashSet 统一管理
///   - 死亡后冻结角色 Rigidbody2D（由 BaseHazard.FreezeRigidbody 处理）
///
/// S52: 重构为继承 BaseHazard 基类 —
///   - Trigger 碰撞检测和防刷屏保护由基类统一处理
///   - KillZone 保留独有的 Update Y 坐标兜底检测
///   - 所有 S48b/c 行为完全保留，零行为变化
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[SelectionBase] // S37 视碰分离: 确保框选时选中 Root 而非 Visual 子节点
public class KillZone : BaseHazard
{
    // [AI防坑警告] fallbackY 是 Update 兜底检测的 Y 阈值。
    // 当角色低于此 Y 值时直接判定死亡，不依赖 Trigger 碰撞。
    // 默认 -15 足够低，不会误杀正常游戏中的角色。
    // TestConsoleWindow 创建 KillZone 时会自动设置此值为 KillZone.position.y - 5。
    [SerializeField] private float fallbackY = -15f;

    // P2: 缓存 PlayerHealth 引用，避免每帧 FindObjectsOfType
    private PlayerHealth[] cachedPlayers;

    // S48c: KillZone 独有的已击杀集合（用于 Update 兜底检测）
    // 注意：Trigger 碰撞的防刷屏由 BaseHazard._processedSet 处理
    // 此集合仅用于 Update fallback 路径，两者互不干扰
    private readonly HashSet<PlayerHealth> killedByFallback = new HashSet<PlayerHealth>();

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

    // ═══════════════════════════════════════════════════
    // 重写 BaseHazard 虚方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// KillZone 的伤害逻辑：一击必杀（999 伤害）。
    /// 继承自 BaseHazard，由 Trigger 碰撞自动调用。
    /// </summary>
    protected override void OnHazardTriggered(PlayerHealth health)
    {
        health.TakeDamage(GetDamage());
    }

    /// <summary>
    /// KillZone 击杀后的日志。
    /// </summary>
    protected override void OnPlayerKilled(PlayerHealth health)
    {
        Debug.Log($"[KillZone] {health.gameObject.name} 掉入深渊！");
    }

    // ═══════════════════════════════════════════════════
    // KillZone 独有：Update Y 坐标兜底检测 (S48b)
    // ═══════════════════════════════════════════════════

    private void Update()
    {
        // S48b: Y 坐标兜底 — 检查缓存的 PlayerHealth 实例，
        // 如果任何角色低于 fallbackY 则直接判定死亡。
        // 这是最后一道防线，即使 Trigger 完全失效也能兜底。
        if (cachedPlayers == null) return;

        for (int i = 0; i < cachedPlayers.Length; i++)
        {
            PlayerHealth health = cachedPlayers[i];
            if (health != null && health.CurrentHealth > 0 &&
                !HasProcessed(health) && !killedByFallback.Contains(health) &&
                health.transform.position.y < fallbackY)
            {
                killedByFallback.Add(health);
                health.TakeDamage(GetDamage());
                Debug.Log($"[KillZone] {health.gameObject.name} 掉入深渊！(Y坐标兜底)");

                // S48c: 冻结角色 Rigidbody，防止尸体继续下坠刷日志
                if (health.CurrentHealth <= 0)
                {
                    FreezeRigidbody(health);
                }
            }
        }
    }
}
