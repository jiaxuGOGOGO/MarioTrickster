using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// BaseHazard — 统一陷阱/危险区域基类 (S52)
//
// [AI防坑警告] 此基类是 S52 陷阱机制基建的核心。
// 所有被动关卡危险元素（KillZone、未来的锯片/激光/尖刺等）
// 都应继承此基类，以获得统一的伤害接口和防刷屏保护。
//
// ╔══════════════════════════════════════════════════════════════╗
// ║                    [机制扩展指南]                             ║
// ║                                                              ║
// ║  如何添加新陷阱（例如 SawBlade / LaserBeam）：               ║
// ║                                                              ║
// ║  1. 在 Assets/Scripts/LevelMechanics/ 下新建 .cs 文件        ║
// ║  2. 继承 BaseHazard                                          ║
// ║  3. 重写 OnHazardTriggered(PlayerHealth) 实现自定义伤害逻辑  ║
// ║  4. 可选：重写 GetDamage() 返回自定义伤害值                  ║
// ║  5. 可选：重写 OnPlayerKilled(PlayerHealth) 添加死亡特效     ║
// ║  6. 防刷屏保护由基类自动处理，子类无需关心                   ║
// ║                                                              ║
// ║  示例：                                                      ║
// ║  public class SawBlade : BaseHazard                          ║
// ║  {                                                           ║
// ║      [SerializeField] private float rotateSpeed = 360f;      ║
// ║      [SerializeField] private int sawDamage = 50;            ║
// ║                                                              ║
// ║      protected override int GetDamage() => sawDamage;        ║
// ║                                                              ║
// ║      protected override void OnHazardTriggered(              ║
// ║          PlayerHealth health)                                ║
// ║      {                                                       ║
// ║          // 自定义：锯片只造成伤害，不一击必杀               ║
// ║          health.TakeDamage(GetDamage());                     ║
// ║          // 可选：添加击退力                                 ║
// ║      }                                                       ║
// ║                                                              ║
// ║      private void Update()                                   ║
// ║      {                                                       ║
// ║          transform.Rotate(0, 0, rotateSpeed * Time.deltaTime);║
// ║      }                                                       ║
// ║  }                                                           ║
// ╚══════════════════════════════════════════════════════════════╝
//
// 设计原则：
//   - 基类提供统一的 Trigger 碰撞检测和防刷屏 HashSet
//   - 子类通过重写 OnHazardTriggered 实现差异化伤害逻辑
//   - 默认行为：一击必杀（999 伤害），与 KillZone 一致
//   - S48c 防刷屏逻辑（HashSet + Rigidbody 冻结）内置于基类
//   - 子类可重写 ShouldFreezeOnKill 控制是否冻结尸体
//
// 架构关系：
//   BaseHazard（基类）
//     ├── KillZone（深渊死亡区，S48b/c 三重检测 + 防刷屏）
//     ├── SawBlade（未来：旋转锯片，非致命伤害）
//     ├── LaserBeam（未来：激光束，周期性伤害）
//     └── SpikeTrap（未来：尖刺陷阱，接触伤害）
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// S52: 统一陷阱/危险区域基类。
/// 
/// 提供：
/// - 统一的 Trigger 碰撞检测（OnTriggerEnter2D / OnTriggerStay2D）
/// - S48c 防刷屏保护（HashSet 确保每个角色只被处理一次）
/// - 可重写的伤害逻辑和死亡后处理
/// </summary>
public abstract class BaseHazard : MonoBehaviour
{
    [Header("BaseHazard 基础配置")]
    [Tooltip("默认伤害值（999 = 一击必杀）")]
    [SerializeField] protected int baseDamage = 999;

    // S48c: 已处理角色集合，防止同一角色被重复伤害导致日志刷屏
    // [AI防坑警告] 此 HashSet 是防刷屏的核心机制。
    // 绝对不要在子类中清除它，除非你明确知道角色已经复活。
    private readonly System.Collections.Generic.HashSet<PlayerHealth> _processedSet
        = new System.Collections.Generic.HashSet<PlayerHealth>();

    // ═══════════════════════════════════════════════════
    // 碰撞检测（子类通常不需要重写）
    // ═══════════════════════════════════════════════════

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        TryProcess(other);
    }

    // S48b: 安全网 — 防止高速穿越或物理引擎跳帧导致 Enter 未触发
    protected virtual void OnTriggerStay2D(Collider2D other)
    {
        TryProcess(other);
    }

    // S57b: 角色离开危险区域后，从 _processedSet 中移除，
    // 允许非致命 hazard（如 SawBlade）在角色再次进入时重新触发伤害。
    // 对 KillZone 无影响：一击必杀后角色已死亡，不会再离开。
    // [AI防坑警告] 此 Exit 回调与 Enter/Stay 的 _processedSet 配合使用，
    // 不要删除，否则非致命 hazard 会变成“只伤害一次”。
    protected virtual void OnTriggerExit2D(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            _processedSet.Remove(health);
        }
    }

    // ═══════════════════════════════════════════════════
    // 核心处理流程
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 尝试处理碰撞体。如果目标有 PlayerHealth 且未被处理过，则触发伤害。
    /// </summary>
    private void TryProcess(Collider2D other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null && health.CurrentHealth > 0 && !_processedSet.Contains(health))
        {
            _processedSet.Add(health);
            OnHazardTriggered(health);

            // 如果角色被击杀，执行后处理
            if (health.CurrentHealth <= 0)
            {
                OnPlayerKilled(health);

                if (ShouldFreezeOnKill())
                {
                    FreezeRigidbody(health);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 子类可重写的虚方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 当陷阱触发时的伤害逻辑。子类重写此方法实现差异化行为。
    /// 默认：直接造成 GetDamage() 点伤害。
    /// </summary>
    protected virtual void OnHazardTriggered(PlayerHealth health)
    {
        health.TakeDamage(GetDamage());
    }

    /// <summary>
    /// 获取伤害值。子类可重写返回自定义伤害。
    /// 默认：返回 baseDamage（999 = 一击必杀）。
    /// </summary>
    protected virtual int GetDamage()
    {
        return baseDamage;
    }

    /// <summary>
    /// 角色被击杀后的回调。子类可重写添加死亡特效/音效。
    /// 默认：输出调试日志。
    /// </summary>
    protected virtual void OnPlayerKilled(PlayerHealth health)
    {
        Debug.Log($"[{GetType().Name}] {health.gameObject.name} 被 {gameObject.name} 击杀！");
    }

    /// <summary>
    /// 是否在击杀后冻结角色 Rigidbody。
    /// 默认 true（防止尸体继续运动刷日志）。
    /// 某些陷阱（如弹射尖刺）可能需要返回 false。
    /// </summary>
    protected virtual bool ShouldFreezeOnKill()
    {
        return true;
    }

    // ═══════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// S48c: 冻结角色 Rigidbody，防止尸体继续运动刷日志。
    /// </summary>
    protected void FreezeRigidbody(PlayerHealth health)
    {
        Rigidbody2D rb = health.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }
    }

    /// <summary>
    /// 重置已处理集合。当角色复活或场景重载时调用。
    /// </summary>
    public void ResetProcessedSet()
    {
        _processedSet.Clear();
    }

    /// <summary>
    /// 检查某个角色是否已被此陷阱处理过。
    /// </summary>
    public bool HasProcessed(PlayerHealth health)
    {
        return _processedSet.Contains(health);
    }
}
