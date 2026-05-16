using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// HybridInputProvider — 人机混合输入路由
//
// 核心设计：
//   内部持有两个 Provider：keyboard（KeyboardInputProvider）和
//   bot（HeuristicBotInputProvider）。通过 MarioIsAI / TricksterIsAI
//   两个布尔值，在每个接口方法中动态路由到对应的输入源。
//
// 路由规则：
//   - P1 (Mario) 方法：MarioIsAI ? bot : keyboard
//   - P2 (Trickster) 方法：TricksterIsAI ? bot : keyboard
//   - 系统级按键（Pause/Restart/NoCooldown/RestartRound/NextRound）：
//     永远优先读取 keyboard，保证人类玩家随时能暂停/重启
//
// 热键支持：
//   - F1：翻转 MarioIsAI，游玩中一键夺舍
//   - F2：翻转 TricksterIsAI，游玩中一键夺舍
//   - 热键检测在 Tick() 中执行，不依赖 Editor UI
//
// Tick 时序：
//   InputManager.Update 调用顺序：
//     UpdateInputProvider() → ReadP1() → ReadP2() → Dispatch → LateReset()
//   HybridInputProvider.Tick() 在 UpdateInputProvider() 中被调用，
//   因此 Tick 中同时更新 keyboard 和 bot，保证两者状态都是最新的。
//
// [AI防坑警告]
//   - 人机切换在 PlayMode 任意时刻都是平滑的
//   - 切换时不干扰底层物理和跳跃状态机
//   - bot 始终在后台运转（即使当前未被路由），随时准备接管
//   - keyboard 始终在后台运转（即使当前未被路由），随时准备接管
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 人机混合输入路由。
/// 根据 MarioIsAI / TricksterIsAI 动态路由 P1/P2 输入到 keyboard 或 bot。
/// 系统级按键永远走 keyboard。
/// </summary>
public class HybridInputProvider : IInputProvider
{
    // ═══════════════════════════════════════════════════════════
    // 内部 Provider 实例
    // ═══════════════════════════════════════════════════════════

    private readonly KeyboardInputProvider keyboard = new KeyboardInputProvider();
    private readonly HeuristicBotInputProvider bot = new HeuristicBotInputProvider();

    // ═══════════════════════════════════════════════════════════
    // 公开控制：人机切换
    // ═══════════════════════════════════════════════════════════

    /// <summary>true = Mario 由 AI 控制，false = Mario 由人类控制</summary>
    public bool MarioIsAI;

    /// <summary>true = Trickster 由 AI 控制，false = Trickster 由人类控制</summary>
    public bool TricksterIsAI;

    // ═══════════════════════════════════════════════════════════
    // 公开访问器（供 Editor UI 和外部代码查询）
    // ═══════════════════════════════════════════════════════════

    /// <summary>获取内部的 keyboard provider（只读访问）</summary>
    public KeyboardInputProvider Keyboard => keyboard;

    /// <summary>获取内部的 bot provider（只读访问）</summary>
    public HeuristicBotInputProvider Bot => bot;

    // ═══════════════════════════════════════════════════════════
    // Tick — 每帧更新
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧由 InputManager.UpdateInputProvider() 调用。
    /// 同时更新 keyboard 和 bot，保证两者状态都是最新的。
    /// 检测 F1/F2 热键实现游玩中一键夺舍。
    /// </summary>
    public void Tick(float dt)
    {
        // 1. 更新 keyboard（手柄检测）
        keyboard.UpdateGamepads();

        // 2. 更新 bot（AI 决策 + 单帧事件清零）
        bot.Tick(dt);

        // 3. 检测 F1/F2 热键
        CheckHotSwapKeys();
    }

    /// <summary>
    /// 检测 F1/F2 热键，按下时翻转对应的 AI 控制状态。
    /// F1 = Mario 人机切换，F2 = Trickster 人机切换。
    /// 使用 GetKeyDown 确保单帧触发。
    /// </summary>
    private void CheckHotSwapKeys()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            MarioIsAI = !MarioIsAI;
            string who = MarioIsAI ? "\U0001f916 AI" : "\U0001f464 \u4eba\u7c7b";
            Debug.Log($"<color=#00FF88><b>[\u8f93\u5165\u6d41] Mario \u5df2\u5207\u6362\u4e3a{who}\u63a7\u5236\uff01(F1)</b></color>");
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            TricksterIsAI = !TricksterIsAI;
            string who = TricksterIsAI ? "\U0001f916 AI" : "\U0001f464 \u4eba\u7c7b";
            Debug.Log($"<color=#FF8800><b>[\u8f93\u5165\u6d41] Trickster \u5df2\u5207\u6362\u4e3a{who}\u63a7\u5236\uff01(F2)</b></color>");
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 缓存管理
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 强制刷新 bot 的场景引用缓存。
    /// 在场景重载或回合重置后调用。
    /// </summary>
    public void InvalidateCache()
    {
        bot.InvalidateCache();
    }

    /// <summary>
    /// 重置所有状态到默认值。
    /// </summary>
    public void ResetAll()
    {
        MarioIsAI = false;
        TricksterIsAI = false;
        bot.ResetAll();
    }

    // ═══════════════════════════════════════════════════════════
    // IInputProvider 接口实现 — 动态路由
    // ═══════════════════════════════════════════════════════════

    // ── P1 (Mario) ──
    // MarioIsAI ? bot : keyboard

    public float GetP1Horizontal() => MarioIsAI ? bot.GetP1Horizontal() : keyboard.GetP1Horizontal();
    public float GetP1Vertical()   => MarioIsAI ? bot.GetP1Vertical()   : keyboard.GetP1Vertical();
    public bool GetP1JumpHeld()    => MarioIsAI ? bot.GetP1JumpHeld()   : keyboard.GetP1JumpHeld();
    public bool GetP1JumpDown()    => MarioIsAI ? bot.GetP1JumpDown()   : keyboard.GetP1JumpDown();
    public bool GetP1SHeld()       => MarioIsAI ? bot.GetP1SHeld()     : keyboard.GetP1SHeld();
    public bool GetP1SDown()       => MarioIsAI ? bot.GetP1SDown()     : keyboard.GetP1SDown();
    public bool GetP1ScanDown()    => MarioIsAI ? bot.GetP1ScanDown()  : keyboard.GetP1ScanDown();

    // ── P2 (Trickster) ──
    // TricksterIsAI ? bot : keyboard

    public float GetP2Horizontal()      => TricksterIsAI ? bot.GetP2Horizontal()      : keyboard.GetP2Horizontal();
    public float GetP2Vertical()        => TricksterIsAI ? bot.GetP2Vertical()        : keyboard.GetP2Vertical();
    public bool GetP2JumpHeld()         => TricksterIsAI ? bot.GetP2JumpHeld()        : keyboard.GetP2JumpHeld();
    public bool GetP2JumpDown()         => TricksterIsAI ? bot.GetP2JumpDown()        : keyboard.GetP2JumpDown();
    public bool GetP2DirectionDown()    => TricksterIsAI ? bot.GetP2DirectionDown()   : keyboard.GetP2DirectionDown();
    public bool GetP2DisguiseDown()     => TricksterIsAI ? bot.GetP2DisguiseDown()    : keyboard.GetP2DisguiseDown();
    public float GetP2SwitchDirection() => TricksterIsAI ? bot.GetP2SwitchDirection() : keyboard.GetP2SwitchDirection();
    public bool GetP2AbilityDown()      => TricksterIsAI ? bot.GetP2AbilityDown()     : keyboard.GetP2AbilityDown();

    // ── 全局输入 ──
    // 系统级按键永远优先读取 keyboard，保证人类玩家随时能暂停/重启

    public bool GetPauseDown()            => keyboard.GetPauseDown();
    public bool GetRestartDown()          => keyboard.GetRestartDown();
    public bool GetNoCooldownToggleDown() => keyboard.GetNoCooldownToggleDown();
    public bool GetRestartRoundDown()     => keyboard.GetRestartRoundDown();
    public bool GetNextRoundDown()        => keyboard.GetNextRoundDown();
}
