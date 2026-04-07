using UnityEngine;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════
// AutomatedInputProvider — 自动化测试虚拟输入（S49: 全自动测试基建）
//
// [AI防坑警告] 此类是无头全自动跑图测试的核心输入注入器。
// 它通过预定义的 InputFrame 序列，按帧回放虚拟按键，
// 让 InputManager 以为有真实玩家在操作。
//
// 使用方式（PlayMode 测试中）：
//   var sequence = new List<InputFrame> {
//       new InputFrame { duration = 30, p1Horizontal = 1f },           // 向右走30帧
//       new InputFrame { duration = 1, p1JumpDown = true, p1JumpHeld = true }, // 跳跃
//       new InputFrame { duration = 60, p1Horizontal = 1f, p1JumpHeld = true }, // 空中向右
//   };
//   var provider = new AutomatedInputProvider(sequence);
//   inputManager.SetInputProvider(provider);
//
// 设计原则：
//   1. 每个 InputFrame 定义一段持续时间内的输入状态
//   2. 单帧事件（JumpDown、ScanDown 等）只在该帧段的第一帧返回 true
//   3. 序列播放完毕后所有输入归零（角色停止）
//   4. 支持运行时动态追加帧（用于 AI 决策驱动的自适应测试）
//
// 架构参考:
//   - Celeste TAS (Tool-Assisted Speedrun): 逐帧输入回放
//   - Unity InputTestFixture: 虚拟设备输入注入
//   - 自动化关卡验证: 预录制输入序列 → 回放 → 断言角色状态
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 单个输入帧的定义。
/// 
/// 每个 InputFrame 描述一段连续帧内的输入状态。
/// duration 表示此输入状态持续的帧数（FixedUpdate 帧）。
/// 单帧事件（xxxDown）只在该段的第一帧生效。
/// </summary>
[System.Serializable]
public struct InputFrame
{
    /// <summary>此输入状态持续的帧数（至少为1）</summary>
    public int duration;

    // ── P1 (Mario) ──
    public float p1Horizontal;
    public float p1Vertical;
    public bool p1JumpHeld;
    public bool p1JumpDown;      // 单帧事件：仅第一帧生效
    public bool p1SHeld;
    public bool p1SDown;         // 单帧事件
    public bool p1ScanDown;      // 单帧事件

    // ── P2 (Trickster) ──
    public float p2Horizontal;
    public float p2Vertical;
    public bool p2JumpHeld;
    public bool p2JumpDown;      // 单帧事件
    public bool p2DirectionDown; // 单帧事件
    public bool p2DisguiseDown;  // 单帧事件
    public float p2SwitchDir;    // 单帧事件：+1/-1/0
    public bool p2AbilityDown;   // 单帧事件

    // ── 全局 ──
    public bool pauseDown;       // 单帧事件
    public bool restartDown;     // 单帧事件
    public bool noCooldownDown;  // 单帧事件
    public bool restartRoundDown; // 单帧事件
    public bool nextRoundDown;   // 单帧事件
}

/// <summary>
/// 自动化测试输入提供者。
/// 
/// 按预定义的 InputFrame 序列逐帧回放虚拟输入。
/// 序列播放完毕后所有输入归零。
/// 
/// 支持：
///   - 预录制序列回放（TAS 风格）
///   - 运行时动态追加帧（AI 决策驱动）
///   - 序列完成回调（用于测试断言触发）
/// </summary>
public class AutomatedInputProvider : IInputProvider
{
    private readonly List<InputFrame> _sequence;
    private int _currentIndex;
    private int _frameInSegment;
    private bool _finished;

    /// <summary>序列是否已播放完毕</summary>
    public bool IsFinished => _finished;

    /// <summary>当前正在播放的帧段索引</summary>
    public int CurrentSegmentIndex => _currentIndex;

    /// <summary>序列播放完毕时触发的回调</summary>
    public System.Action OnSequenceComplete;

    /// <summary>
    /// 创建自动化输入提供者。
    /// </summary>
    /// <param name="sequence">输入帧序列。传入 null 或空列表则立即完成。</param>
    public AutomatedInputProvider(List<InputFrame> sequence)
    {
        _sequence = sequence ?? new List<InputFrame>();
        _currentIndex = 0;
        _frameInSegment = 0;
        _finished = _sequence.Count == 0;
    }

    /// <summary>
    /// 每帧由 InputManager 调用，推进帧计数器。
    /// 必须在读取输入之前调用。
    /// </summary>
    public void Tick()
    {
        if (_finished) return;

        _frameInSegment++;

        // 检查当前帧段是否播放完毕
        if (_frameInSegment >= _sequence[_currentIndex].duration)
        {
            _currentIndex++;
            _frameInSegment = 0;

            if (_currentIndex >= _sequence.Count)
            {
                _finished = true;
                OnSequenceComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// 运行时动态追加输入帧（用于 AI 决策驱动的自适应测试）。
    /// 如果序列已完成，追加后会重新激活播放。
    /// </summary>
    public void AppendFrames(List<InputFrame> frames)
    {
        if (frames == null || frames.Count == 0) return;

        _sequence.AddRange(frames);

        if (_finished)
        {
            _finished = false;
            // _currentIndex 和 _frameInSegment 已经指向新追加的帧
        }
    }

    /// <summary>重置到序列开头</summary>
    public void Reset()
    {
        _currentIndex = 0;
        _frameInSegment = 0;
        _finished = _sequence.Count == 0;
    }

    // ── 内部辅助 ──────────────────────────────────────────────

    private InputFrame Current => (!_finished && _currentIndex < _sequence.Count)
        ? _sequence[_currentIndex]
        : default;

    private bool IsFirstFrame => _frameInSegment == 0;

    // ═══════════════════════════════════════════════════════════
    // IInputProvider 实现
    // ═══════════════════════════════════════════════════════════

    // ── P1 (Mario) ──

    public float GetP1Horizontal() => Current.p1Horizontal;
    public float GetP1Vertical()   => Current.p1Vertical;
    public bool GetP1JumpHeld()    => Current.p1JumpHeld;
    public bool GetP1JumpDown()    => Current.p1JumpDown && IsFirstFrame;
    public bool GetP1SHeld()       => Current.p1SHeld;
    public bool GetP1SDown()       => Current.p1SDown && IsFirstFrame;
    public bool GetP1ScanDown()    => Current.p1ScanDown && IsFirstFrame;

    // ── P2 (Trickster) ──

    public float GetP2Horizontal()      => Current.p2Horizontal;
    public float GetP2Vertical()        => Current.p2Vertical;
    public bool GetP2JumpHeld()         => Current.p2JumpHeld;
    public bool GetP2JumpDown()         => Current.p2JumpDown && IsFirstFrame;
    public bool GetP2DirectionDown()    => Current.p2DirectionDown && IsFirstFrame;
    public bool GetP2DisguiseDown()     => Current.p2DisguiseDown && IsFirstFrame;
    public float GetP2SwitchDirection() => IsFirstFrame ? Current.p2SwitchDir : 0f;
    public bool GetP2AbilityDown()      => Current.p2AbilityDown && IsFirstFrame;

    // ── 全局 ──

    public bool GetPauseDown()            => Current.pauseDown && IsFirstFrame;
    public bool GetRestartDown()          => Current.restartDown && IsFirstFrame;
    public bool GetNoCooldownToggleDown() => Current.noCooldownDown && IsFirstFrame;
    public bool GetRestartRoundDown()     => Current.restartRoundDown && IsFirstFrame;
    public bool GetNextRoundDown()        => Current.nextRoundDown && IsFirstFrame;
}
