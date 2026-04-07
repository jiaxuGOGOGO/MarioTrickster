using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// IInputProvider — 输入抽象接口（S49: 全自动测试基建）
//
// [AI防坑警告] 这是输入解耦的核心接口，所有输入源（键盘、手柄、自动化测试）
// 都必须实现此接口。InputManager 通过此接口读取输入，而非直接调用 Input.GetKey()。
// 修改此接口时必须同步更新所有实现类：
//   - KeyboardInputProvider（真实键盘+手柄输入）
//   - AutomatedInputProvider（自动化测试虚拟输入）
//
// 设计原则（参考 Jordan Cassady / NSubstitute 最佳实践）：
//   1. 接口只定义"读取"方法，不定义"写入"方法
//   2. 每个方法对应一个原子输入信号，不做组合判断
//   3. 组合键逻辑（如 S+Jump 下落穿越）保留在 InputManager 中
//   4. 接口不持有状态，状态由实现类管理
//
// 架构参考:
//   - Celeste/TowerFall: Actor 不持有速度概念，只接收移动量（Maddy Thorson）
//   - NSubstitute 模式: IPlayerInput 接口 + Substitute.For<IPlayerInput>()
//   - Unity 官方测试指南: 输入抽象是 PlayMode 自动化测试的前提
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 输入提供者抽象接口。
/// 
/// 所有输入源（键盘、手柄、自动化测试脚本）实现此接口，
/// InputManager 通过此接口读取原始输入信号，实现输入源的热替换。
/// 
/// 方法分为两类：
///   - 持续状态（GetKey 语义）：每帧返回当前按住状态
///   - 单帧事件（GetKeyDown 语义）：仅在按下瞬间返回 true
/// 
/// 使用方式：
///   InputManager.SetInputProvider(new KeyboardInputProvider());  // 正常游戏
///   InputManager.SetInputProvider(new AutomatedInputProvider(sequence));  // 自动化测试
/// </summary>
public interface IInputProvider
{
    // ── P1 (Mario) 输入 ──────────────────────────────────────

    /// <summary>P1 水平移动（-1 左, 0 静止, +1 右）</summary>
    float GetP1Horizontal();

    /// <summary>P1 垂直移动（-1 下, 0 静止, +1 上）</summary>
    float GetP1Vertical();

    /// <summary>P1 跳跃键持续按住</summary>
    bool GetP1JumpHeld();

    /// <summary>P1 跳跃键刚按下（单帧事件）</summary>
    bool GetP1JumpDown();

    /// <summary>P1 S键持续按住（用于 S+Jump 组合检测）</summary>
    bool GetP1SHeld();

    /// <summary>P1 S键刚按下（单帧事件，隐藏通道交互）</summary>
    bool GetP1SDown();

    /// <summary>P1 扫描键刚按下（Q键，单帧事件）</summary>
    bool GetP1ScanDown();

    // ── P2 (Trickster) 输入 ──────────────────────────────────

    /// <summary>P2 水平移动（-1 左, 0 静止, +1 右）</summary>
    float GetP2Horizontal();

    /// <summary>P2 垂直移动（-1 下, 0 静止, +1 上）</summary>
    float GetP2Vertical();

    /// <summary>P2 跳跃键持续按住</summary>
    bool GetP2JumpHeld();

    /// <summary>P2 跳跃键刚按下（单帧事件）</summary>
    bool GetP2JumpDown();

    /// <summary>P2 方向键刚按下（单帧事件，用于磁吸切换触发）</summary>
    bool GetP2DirectionDown();

    /// <summary>P2 伪装键刚按下（P键，单帧事件）</summary>
    bool GetP2DisguiseDown();

    /// <summary>P2 切换形态键刚按下（O/I键，单帧事件）。返回方向：+1 下一个, -1 上一个, 0 未按</summary>
    float GetP2SwitchDirection();

    /// <summary>P2 操控道具键刚按下（L键，单帧事件）</summary>
    bool GetP2AbilityDown();

    // ── 全局输入 ──────────────────────────────────────────────

    /// <summary>暂停键刚按下（ESC，单帧事件）</summary>
    bool GetPauseDown();

    /// <summary>快速重启键刚按下（F5，单帧事件）</summary>
    bool GetRestartDown();

    /// <summary>无冷却切换键刚按下（F9，单帧事件）</summary>
    bool GetNoCooldownToggleDown();

    /// <summary>重新开始键刚按下（R，回合结束时，单帧事件）</summary>
    bool GetRestartRoundDown();

    /// <summary>下一回合键刚按下（N，回合结束时，单帧事件）</summary>
    bool GetNextRoundDown();
}
