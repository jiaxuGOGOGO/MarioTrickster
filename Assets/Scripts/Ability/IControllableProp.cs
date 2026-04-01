using UnityEngine;

/// <summary>
/// 可操控道具接口 - Trickster 能力系统核心契约
/// 参考: Crawl 游戏的 Ghost Possess Trap 机制
/// 
/// 所有可被 Trickster 变身后操控的关卡道具都必须实现此接口。
/// 设计要点:
///   1. Telegraph（预警）→ Active（爆发）→ Cooldown（冷却）三阶段
///   2. 每种道具可配置操控次数或持续时间限制
///   3. 预警阶段给 Mario 玩家反应窗口，提高博弈深度
/// 
/// 使用方式:
///   在关卡道具脚本上实现此接口，TricksterAbilitySystem 会自动检测并调用
/// </summary>
public interface IControllableProp
{
    /// <summary>
    /// 道具的显示名称（用于 UI 提示）
    /// </summary>
    string PropName { get; }

    /// <summary>
    /// 道具当前是否可以被操控
    /// 返回 false 的情况: 正在冷却、次数用尽、正在预警/激活中
    /// </summary>
    bool CanBeControlled();

    /// <summary>
    /// Trickster 触发操控（进入预警阶段）
    /// 由 TricksterAbilitySystem 调用
    /// </summary>
    /// <param name="direction">Trickster 的输入方向（某些道具需要方向参数）</param>
    void OnTricksterActivate(Vector2 direction);

    /// <summary>
    /// 获取当前剩余操控次数（-1 表示无限次）
    /// </summary>
    int GetRemainingUses();

    /// <summary>
    /// 获取当前冷却进度（0=冷却完毕可用, 1=刚触发正在冷却）
    /// </summary>
    float GetCooldownProgress();

    /// <summary>
    /// 获取预警时长（秒），用于 UI 显示预警进度条
    /// </summary>
    float GetTelegraphDuration();

    /// <summary>
    /// 当前道具的操控状态
    /// </summary>
    PropControlState GetControlState();
}

/// <summary>
/// 道具操控状态枚举
/// Idle → Telegraph → Active → Cooldown → Idle
/// </summary>
public enum PropControlState
{
    Idle,       // 空闲，可被操控
    Telegraph,  // 预警阶段（给 Mario 反应时间）
    Active,     // 激活阶段（正在执行阻碍效果）
    Cooldown,   // 冷却阶段（不可操控）
    Exhausted   // 次数用尽（本回合不可再操控）
}
