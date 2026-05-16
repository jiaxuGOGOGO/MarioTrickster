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
//   4. 纯射线探测，绝不引入 NavMesh
//
// 单帧事件（xxxDown）的生命周期：
//   InputManager.Update 的调用顺序：
//     UpdateInputProvider() → ReadP1() → ReadP2() → Dispatch → LateReset()
//   因此 Tick() 在 ReadP1/ReadP2 之前执行。
//   为保证 Brain 设置的 xxxDown 能被 ReadP1/ReadP2 读到：
//     - Tick 开头先清零上一帧的 Down 字段
//     - 然后执行 Brain 逻辑（可能设置新的 Down = true）
//     - ReadP1/ReadP2 紧接着读取到本帧的 Down 值
//
// 使用方式：
//   var bot = new HeuristicBotInputProvider();
//   inputManager.SetInputProvider(bot);
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
/// 单帧事件在每次 Tick 开头自动清零，然后由 Brain 重新设置。
/// </summary>
public class HeuristicBotInputProvider : IInputProvider
{
    // ═══════════════════════════════════════════════════════════
    // P1 (Mario) 输入字段
    // ═══════════════════════════════════════════════════════════

    public float p1Horizontal;
    public float p1Vertical;
    public bool p1JumpHeld;
    public bool p1JumpDown;
    public bool p1SHeld;
    public bool p1SDown;
    public bool p1ScanDown;

    // ═══════════════════════════════════════════════════════════
    // P2 (Trickster) 输入字段
    // ═══════════════════════════════════════════════════════════

    public float p2Horizontal;
    public float p2Vertical;
    public bool p2JumpHeld;
    public bool p2JumpDown;
    public bool p2DirectionDown;
    public bool p2DisguiseDown;
    public float p2SwitchDir;
    public bool p2AbilityDown;

    // ═══════════════════════════════════════════════════════════
    // 全局输入字段
    // ═══════════════════════════════════════════════════════════

    public bool pauseDown;
    public bool restartDown;
    public bool noCooldownDown;
    public bool restartRoundDown;
    public bool nextRoundDown;

    // ═══════════════════════════════════════════════════════════
    // Mario Brain 内部状态（跨帧缓存）
    // ═══════════════════════════════════════════════════════════

    // 场景引用缓存（惰性查找，避免每帧 FindObjectOfType）
    private MarioController _mario;
    private MarioCounterplayProbe _probe;
    private bool _cacheInitialized;

    // 跳跃持续计时器：按下跳跃后保持 JumpHeld 的剩余秒数
    private float _jumpHoldTimer;

    // 跳跃持续时长（秒）：模拟玩家按住跳跃键的时间，影响跳跃高度
    private const float JUMP_HOLD_DURATION = 0.35f;

    // 射线参数
    private const float PIT_CHECK_DEPTH     = 2.5f;   // 向下探测深度（超过此深度视为深坑）
    private const float PIT_CHECK_FORWARD   = 0.7f;   // 前方探测偏移（半格多一点）
    private const float WALL_CHECK_DISTANCE = 0.5f;   // 前方墙壁探测距离
    private const float WALL_CHECK_HEIGHT   = 0.3f;   // 墙壁射线起点高度偏移（略高于脚底）

    // ═══════════════════════════════════════════════════════════
    // Tick
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧由 InputManager.UpdateInputProvider() 调用。
    /// 执行顺序：清零上帧 Down → Brain 决策 → 返回（ReadP1/ReadP2 紧接着读取）。
    /// </summary>
    public void Tick(float dt)
    {
        // 阶段 1: 清零上一帧的单帧事件
        ResetDownFlags();

        // 阶段 2: AI 决策（可能重新设置 Down = true）
        UpdateMarioBrain(dt);
        UpdateTricksterBrain(dt);

        // 阶段 3: 返回后 InputManager 立即调用 ReadP1/ReadP2 读取本帧值
    }

    // ═══════════════════════════════════════════════════════════
    // Mario Brain — 基础探路与生存逻辑
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Mario 的启发式决策：目标寻路 + 射线避障跳跃 + 自动反制。
    /// 纯靠短射线探测前方地形，不使用 NavMesh。
    /// </summary>
    protected virtual void UpdateMarioBrain(float dt)
    {
        // ── 惰性缓存场景引用 ──
        if (!_cacheInitialized)
        {
            _mario = Object.FindObjectOfType<MarioController>();
            _probe = Object.FindObjectOfType<MarioCounterplayProbe>();
            _cacheInitialized = true;
        }

        // Mario 不存在或已被禁用（死亡/胜利）→ 全部归零
        if (_mario == null || !_mario.enabled)
        {
            p1Horizontal = 0f;
            p1JumpHeld = false;
            _jumpHoldTimer = 0f;
            return;
        }

        Vector2 marioPos = _mario.transform.position;
        float facingDir = _mario.IsFacingRight ? 1f : -1f;

        // ────────────────────────────────────────────
        // 1. 目标寻路
        // ────────────────────────────────────────────
        Vector2? targetPos = FindCurrentTarget();

        if (targetPos.HasValue)
        {
            float dx = targetPos.Value.x - marioPos.x;
            // 死区：距离目标 X 轴 < 0.3 格时不再左右移动（防抖动）
            if (Mathf.Abs(dx) > 0.3f)
            {
                p1Horizontal = dx > 0f ? 1f : -1f;
            }
            else
            {
                p1Horizontal = 0f;
            }
            // 更新 facingDir 为移动方向（供射线使用）
            if (Mathf.Abs(p1Horizontal) > 0.01f)
            {
                facingDir = p1Horizontal > 0f ? 1f : -1f;
            }
        }
        else
        {
            // 没有目标时默认向右探索
            p1Horizontal = 1f;
            facingDir = 1f;
        }

        // ────────────────────────────────────────────
        // 2. 射线避障与跳跃
        // ────────────────────────────────────────────
        // 构建 Ground 层掩码（与 MarioController 使用相同的 "Ground" 层）
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        // 兜底：如果 Ground 层不存在，使用 Default 层
        LayerMask solidMask = groundLayerIndex >= 0
            ? (1 << groundLayerIndex)
            : Physics2D.AllLayers;

        bool shouldJump = false;

        // ── 遇坑跳：前方半格向下射线未击中地面 → 深渊 ──
        // 只在 Mario 落地时检测（空中不重复触发跳跃）
        if (_mario.IsGrounded)
        {
            Vector2 pitCheckOrigin = marioPos + new Vector2(facingDir * PIT_CHECK_FORWARD, 0f);
            RaycastHit2D pitHit = Physics2D.Raycast(pitCheckOrigin, Vector2.down, PIT_CHECK_DEPTH, solidMask);

            if (pitHit.collider == null)
            {
                // 前方是深渊
                shouldJump = true;
            }

            // ── 遇墙跳：前方射线击中障碍物 → 尝试翻越 ──
            if (!shouldJump)
            {
                Vector2 wallCheckOrigin = marioPos + new Vector2(0f, WALL_CHECK_HEIGHT);
                RaycastHit2D wallHit = Physics2D.Raycast(wallCheckOrigin, new Vector2(facingDir, 0f), WALL_CHECK_DISTANCE, solidMask);

                if (wallHit.collider != null)
                {
                    shouldJump = true;
                }
            }
        }

        // ── 跳跃输出 ──
        if (shouldJump && _jumpHoldTimer <= 0f)
        {
            // 触发新跳跃
            p1JumpDown = true;
            p1JumpHeld = true;
            _jumpHoldTimer = JUMP_HOLD_DURATION;
        }

        // 维护跳跃持续按住计时器
        if (_jumpHoldTimer > 0f)
        {
            _jumpHoldTimer -= dt;
            p1JumpHeld = true;

            if (_jumpHoldTimer <= 0f)
            {
                p1JumpHeld = false;
                _jumpHoldTimer = 0f;
            }
        }

        // ────────────────────────────────────────────
        // 3. 自动反制（MarioCounterplayProbe）
        // ────────────────────────────────────────────
        // 如果 Probe 存在且强扫描就绪（附近有破绽且证据确凿），立刻触发 Q 揭穿
        if (_probe != null && _probe.IsStrongScanReady)
        {
            p1ScanDown = true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 目标查找辅助方法
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 按优先级查找 Mario 当前应前往的目标位置：
    ///   1. LootObjective（未收集时）→ 先去拿宝
    ///   2. EscapeGate（已收集后）→ 带着宝去撤离门
    ///   3. GoalZone（都没有时）→ 普通终点
    /// 返回 null 表示场景中没有找到任何目标。
    /// </summary>
    private Vector2? FindCurrentTarget()
    {
        // 优先级 1: 未收集的 LootObjective
        if (!LootObjective.IsLootCarried)
        {
            LootObjective loot = Object.FindObjectOfType<LootObjective>();
            if (loot != null && loot.gameObject.activeInHierarchy)
            {
                return (Vector2)loot.transform.position;
            }
        }

        // 优先级 2: 已收集 → 找 EscapeGate
        if (LootObjective.IsLootCarried)
        {
            EscapeGate gate = Object.FindObjectOfType<EscapeGate>();
            if (gate != null && gate.gameObject.activeInHierarchy)
            {
                return (Vector2)gate.transform.position;
            }
        }

        // 优先级 3: 兜底 → 找 GoalZone
        GoalZone goal = Object.FindObjectOfType<GoalZone>();
        if (goal != null && goal.gameObject.activeInHierarchy)
        {
            return (Vector2)goal.transform.position;
        }

        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // Trickster Brain（本阶段暂空）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Trickster 的启发式决策（后续实现）。
    /// </summary>
    protected virtual void UpdateTricksterBrain(float dt)
    {
        // TODO: 填入 Trickster 启发式决策逻辑
    }

    // ═══════════════════════════════════════════════════════════
    // 引用缓存管理
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 强制刷新场景引用缓存。
    /// 在场景重载或回合重置后调用，确保不持有已销毁的引用。
    /// </summary>
    public void InvalidateCache()
    {
        _cacheInitialized = false;
        _mario = null;
        _probe = null;
        _jumpHoldTimer = 0f;
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
        p1JumpDown = false;
        p1SDown = false;
        p1ScanDown = false;

        p2JumpDown = false;
        p2DirectionDown = false;
        p2DisguiseDown = false;
        p2SwitchDir = 0f;
        p2AbilityDown = false;

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
        p1Horizontal = 0f;
        p1Vertical = 0f;
        p1JumpHeld = false;
        p1SHeld = false;

        p2Horizontal = 0f;
        p2Vertical = 0f;
        p2JumpHeld = false;

        _jumpHoldTimer = 0f;

        ResetDownFlags();
        InvalidateCache();
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
