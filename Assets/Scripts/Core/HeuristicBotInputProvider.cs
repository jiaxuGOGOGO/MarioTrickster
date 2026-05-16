using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// HeuristicBotInputProvider — 启发式 AI 玩家输入桥接层
//
// [AI防坑警告] 此类实现 IInputProvider 接口，作为启发式 AI 的输入源。
// AI 决策层（UpdateMarioBrain / UpdateTricksterBrain）通过写入内部字段
// 来"模拟按键"，InputManager 通过接口方法读取这些字段，
// 就像读取真实玩家的键盘输入一样。
//
// 核心原则（S53 宪章）：
//   1. 绝不修改 MarioController / TricksterController
//   2. AI 完全受制于物理引擎和冷却时间，与真实玩家体验一致
//   3. 只做薄层接口扩展，不做底层重构
//
// 单帧事件（xxxDown）的生命周期：
//   - Brain 方法在 Tick() 中将 xxxDown 设为 true
//   - InputManager.ReadP1/ReadP2 读取后，该值在同一 Tick 末尾被清零
//   - 这保证单帧事件只生效一帧，不会"卡死"
//
// 使用方式：
//   var bot = new HeuristicBotInputProvider();
//   inputManager.SetInputProvider(bot);
//   // 之后每帧由 InputManager.UpdateInputProvider() 调用 bot.Tick()
//   // 或由外部驱动器手动调用 bot.Tick(Time.deltaTime)
//
// 扩展方式：
//   在 UpdateMarioBrain() / UpdateTricksterBrain() 中填入启发式决策逻辑，
//   通过读取场景状态（Mario 位置、敌人位置、终点方向等）来设置输入字段。
//
// 架构参考:
//   - AutomatedInputProvider: 预录制帧序列回放（TAS 风格）
//   - HeuristicBotInputProvider: 实时决策驱动（AI 风格）
//   - 两者都实现 IInputProvider，可通过 SetInputProvider() 热替换
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 启发式 AI 输入提供者。
///
/// 内部维护与 IInputProvider 接口一一对应的输入字段，
/// AI 决策层在 Tick() 中写入这些字段，InputManager 通过接口方法读取。
/// 单帧事件在每次 Tick 末尾自动清零，防止长按卡死。
///
/// 支持：
///   - 实时启发式决策（基于场景状态的 AI 输入）
///   - 外部脚本直接注入输入（手动设置字段后等待读取）
///   - 与 InputManager 的 UpdateInputProvider 兼容
/// </summary>
public class HeuristicBotInputProvider : IInputProvider
{
    // ═══════════════════════════════════════════════════════════
    // P1 (Mario) 输入字段
    // ═══════════════════════════════════════════════════════════

    /// <summary>P1 水平移动（-1 左, 0 静止, +1 右）</summary>
    public float p1Horizontal;

    /// <summary>P1 垂直移动（-1 下, 0 静止, +1 上）</summary>
    public float p1Vertical;

    /// <summary>P1 跳跃键持续按住</summary>
    public bool p1JumpHeld;

    /// <summary>P1 跳跃键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p1JumpDown;

    /// <summary>P1 S键持续按住</summary>
    public bool p1SHeld;

    /// <summary>P1 S键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p1SDown;

    /// <summary>P1 扫描键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p1ScanDown;

    // ═══════════════════════════════════════════════════════════
    // P2 (Trickster) 输入字段
    // ═══════════════════════════════════════════════════════════

    /// <summary>P2 水平移动（-1 左, 0 静止, +1 右）</summary>
    public float p2Horizontal;

    /// <summary>P2 垂直移动（-1 下, 0 静止, +1 上）</summary>
    public float p2Vertical;

    /// <summary>P2 跳跃键持续按住</summary>
    public bool p2JumpHeld;

    /// <summary>P2 跳跃键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p2JumpDown;

    /// <summary>P2 方向键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p2DirectionDown;

    /// <summary>P2 伪装键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p2DisguiseDown;

    /// <summary>P2 切换形态方向（+1 下一个, -1 上一个, 0 未按）。单帧事件，Tick 末尾自动清零</summary>
    public float p2SwitchDir;

    /// <summary>P2 操控道具键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool p2AbilityDown;

    // ═══════════════════════════════════════════════════════════
    // 全局输入字段
    // ═══════════════════════════════════════════════════════════

    /// <summary>暂停键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool pauseDown;

    /// <summary>快速重启键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool restartDown;

    /// <summary>无冷却切换键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool noCooldownDown;

    /// <summary>重新开始键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool restartRoundDown;

    /// <summary>下一回合键刚按下（单帧事件，Tick 末尾自动清零）</summary>
    public bool nextRoundDown;

    // ═══════════════════════════════════════════════════════════
    // Tick — 每帧由 InputManager 或外部驱动器调用
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧调用一次。先执行 AI 决策（写入输入字段），
    /// 然后在末尾清零所有单帧事件字段。
    ///
    /// 调用时机：
    ///   - 由 InputManager.UpdateInputProvider() 在 ReadP1/ReadP2 之前调用
    ///   - 或由外部测试驱动器手动调用
    ///
    /// 注意：Brain 方法设置的 xxxDown 字段会在本次 Tick 中被 InputManager 读取，
    /// 然后在 Tick 末尾清零。因此 Brain 方法必须在每次需要触发时重新设为 true。
    /// </summary>
    /// <param name="dt">本帧时间增量（秒），供 Brain 方法做时间相关决策</param>
    public void Tick(float dt)
    {
        // ── 阶段 1: AI 决策（写入输入字段）──
        UpdateMarioBrain(dt);
        UpdateTricksterBrain(dt);

        // ── 阶段 2: InputManager 将在此 Tick 之后读取字段 ──
        // （读取发生在 InputManager.ReadP1/ReadP2 中，不在此处）

        // ── 阶段 3: 清零单帧事件（在下一次 Tick 开始前生效）──
        // 注意：此清零在 Brain 写入之后、下一帧 Brain 写入之前执行。
        // InputManager 的 ReadP1/ReadP2 在 Tick 调用之后立即执行，
        // 所以本帧设置的 xxxDown 会被正确读取，然后在下一次 Tick 开头前被清零。
        ResetDownFlags();
    }

    // ═══════════════════════════════════════════════════════════
    // AI 决策方法（本阶段方法体暂空，后续填入启发式逻辑）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Mario 的启发式决策。
    ///
    /// 在此方法中读取场景状态（Mario 位置、敌人位置、终点方向、
    /// 平台布局等），然后设置 p1Horizontal / p1JumpDown / p1JumpHeld 等字段。
    ///
    /// 示例（后续实现）：
    ///   - 检测终点在右侧 → p1Horizontal = 1f
    ///   - 检测前方有坑 → p1JumpDown = true; p1JumpHeld = true
    ///   - 检测头顶有敌人 → p1ScanDown = true
    /// </summary>
    /// <param name="dt">本帧时间增量（秒）</param>
    protected virtual void UpdateMarioBrain(float dt)
    {
        // TODO: 填入 Mario 启发式决策逻辑
    }

    /// <summary>
    /// Trickster 的启发式决策。
    ///
    /// 在此方法中读取场景状态（Trickster 位置、Mario 位置、
    /// 可伪装目标、能量值等），然后设置 p2Horizontal / p2DisguiseDown 等字段。
    ///
    /// 示例（后续实现）：
    ///   - 检测 Mario 接近 → p2DisguiseDown = true（伪装）
    ///   - 检测需要移动到陷阱位置 → p2Horizontal = -1f
    ///   - 检测可操控道具 → p2AbilityDown = true
    /// </summary>
    /// <param name="dt">本帧时间增量（秒）</param>
    protected virtual void UpdateTricksterBrain(float dt)
    {
        // TODO: 填入 Trickster 启发式决策逻辑
    }

    // ═══════════════════════════════════════════════════════════
    // 单帧事件清零
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 清零所有单帧事件字段（xxxDown），防止长按卡死。
    /// 持续状态字段（xxxHeld / Horizontal / Vertical）不受影响。
    /// </summary>
    private void ResetDownFlags()
    {
        // P1 单帧事件
        p1JumpDown = false;
        p1SDown = false;
        p1ScanDown = false;

        // P2 单帧事件
        p2JumpDown = false;
        p2DirectionDown = false;
        p2DisguiseDown = false;
        p2SwitchDir = 0f;
        p2AbilityDown = false;

        // 全局单帧事件
        pauseDown = false;
        restartDown = false;
        noCooldownDown = false;
        restartRoundDown = false;
        nextRoundDown = false;
    }

    /// <summary>
    /// 重置所有输入字段到默认值（包括持续状态和单帧事件）。
    /// 用于 AI 重新初始化或测试清理。
    /// </summary>
    public void ResetAll()
    {
        // P1
        p1Horizontal = 0f;
        p1Vertical = 0f;
        p1JumpHeld = false;
        p1SHeld = false;

        // P2
        p2Horizontal = 0f;
        p2Vertical = 0f;
        p2JumpHeld = false;

        ResetDownFlags();
    }

    // ═══════════════════════════════════════════════════════════
    // IInputProvider 接口实现
    // ═══════════════════════════════════════════════════════════

    // ── P1 (Mario) ──

    public float GetP1Horizontal() => p1Horizontal;
    public float GetP1Vertical()   => p1Vertical;
    public bool GetP1JumpHeld()    => p1JumpHeld;
    public bool GetP1JumpDown()    => p1JumpDown;
    public bool GetP1SHeld()       => p1SHeld;
    public bool GetP1SDown()       => p1SDown;
    public bool GetP1ScanDown()    => p1ScanDown;

    // ── P2 (Trickster) ──

    public float GetP2Horizontal()      => p2Horizontal;
    public float GetP2Vertical()        => p2Vertical;
    public bool GetP2JumpHeld()         => p2JumpHeld;
    public bool GetP2JumpDown()         => p2JumpDown;
    public bool GetP2DirectionDown()    => p2DirectionDown;
    public bool GetP2DisguiseDown()     => p2DisguiseDown;
    public float GetP2SwitchDirection() => p2SwitchDir;
    public bool GetP2AbilityDown()      => p2AbilityDown;

    // ── 全局 ──

    public bool GetPauseDown()            => pauseDown;
    public bool GetRestartDown()          => restartDown;
    public bool GetNoCooldownToggleDown() => noCooldownDown;
    public bool GetRestartRoundDown()     => restartRoundDown;
    public bool GetNextRoundDown()        => nextRoundDown;
}
