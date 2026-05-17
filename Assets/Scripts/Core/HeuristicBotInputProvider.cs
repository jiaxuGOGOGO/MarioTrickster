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
//   5. 获取状态必须通过现有公开 API，严禁反射突破私有变量
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
    // Mario Brain 内部状态
    // ═══════════════════════════════════════════════════════════

    private MarioController _mario;
    private MarioCounterplayProbe _probe;
    private bool _marioCacheReady;
    private float _jumpHoldTimer;
    private LayerMask _solidMask;
    private bool _solidMaskReady;

    private const float JUMP_HOLD_DURATION   = 0.35f;
    private const float PIT_CHECK_DEPTH      = 2.5f;
    private const float PIT_CHECK_FORWARD    = 0.7f;
    private const float WALL_CHECK_DISTANCE  = 0.5f;
    private const float WALL_CHECK_HEIGHT    = 0.3f;

    // ── AI 意图标签（供 UI / Debug 显示） ──
    public string MarioIntent  = "";
    public string TricksterIntent = "";

    // 陷阱雷达参数
    private const float TRAP_RADAR_RANGE     = 4.0f;   // 前方探测距离（格）
    private const float TRAP_RADAR_HEIGHT    = 1.5f;   // BoxCast 高度
    private const float TRAP_SAFE_DISTANCE   = 1.5f;   // 安全停步距离
    private bool _waitingForTrap;                       // 当前是否因陷阱停步
    private bool _baitingTrap;                          // 当前是否后退骗技能
    private const float BAIT_RETREAT_SPEED   = -1f;     // 骗技能时后退速度（反向）

    // ── Mario 强扫描条件参数 ──
    private const float SCAN_ANCHOR_RANGE    = 6.0f;    // 附近有锚点才扫描的范围

    // ── Mario 垂直寻路 & 防卡死参数 ──
    // [AI防坑警告] 以下参数用于解决 Mario 遇高台发呆的问题。
    // Wiggle 让 Mario 在目标正上方时左右徘徊寻找可跳跃路径，
    // 防卡死机制在 Mario 有水平输入但实际位移极小时强制反向跳跃脱离死角。
    private const float VERTICAL_TARGET_DY   = 1.5f;   // 目标在头顶的 Y 阈值
    private const float VERTICAL_TARGET_DX   = 1.0f;   // 目标在头顶的 X 容差
    private const float WIGGLE_PERIOD        = 0.6f;    // 左右徘徊周期（秒）
    private float _wiggleTimer;                         // 徘徊计时器

    private const float STUCK_CHECK_INTERVAL = 0.5f;    // 卡死检测间隔（秒）
    private const float STUCK_MIN_DISPLACEMENT = 0.1f;  // 最小位移阈值
    private const float STUCK_ESCAPE_DURATION  = 0.5f;  // 反向跳跃持续时间
    private float _stuckCheckTimer;                      // 卡死检测计时器
    private Vector2 _lastMarioPos;                       // 上次记录的 Mario 位置
    private bool _lastMarioPosValid;                     // 位置缓存是否有效
    private float _stuckEscapeTimer;                     // 反向跳跃剩余时间
    private float _stuckEscapeDir;                       // 反向跳跃方向（-1 或 1）

    // ═══════════════════════════════════════════════════════════
    // Trickster Brain 内部状态
    // ═══════════════════════════════════════════════════════════

    private TricksterController _trickster;
    private TricksterPossessionGate _gate;
    private TricksterAbilitySystem _ability;
    private TricksterHeatMeter _heatMeter;
    private PropComboTracker _comboTracker;
    private bool _tricksterCacheReady;

    // ── Trickster 连锁追击参数 ──
    private const float COMBO_RUSH_RANGE_MAX = 12.0f;   // 连锁中扩大搜索范围
    private string _lastComboPropName = "";             // 上次连锁使用的道具类型名

    // 目标锚点（当前选定的伏击位置）
    private PossessionAnchor _targetAnchor;

    // 精准处决：人类延迟模拟
    private float _executeDelayTimer;
    private bool _executeArmed;                // 已进入击杀窗口，正在等延迟
    private const float EXECUTE_DELAY_MIN = 0.10f;
    private const float EXECUTE_DELAY_MAX = 0.20f;

    // 热度规避：高热度时强制冷静计时器
    private float _heatCooloffTimer;
    private const float HEAT_COOLOFF_DURATION = 3.0f;

    // 伏击距离参数
    private const float AMBUSH_RANGE_MIN = 3.0f;   // 锚点在 Mario 前方最近距离
    private const float AMBUSH_RANGE_MAX = 8.0f;   // 锚点在 Mario 前方最远距离
    private const float ANCHOR_ARRIVE_DIST = 0.5f;  // 到达锚点的判定距离
    private const float EXECUTE_KILL_DIST = 2.5f;   // Mario 进入此距离时触发处决

    // ── Trickster 提前量预判参数 ──
    // [AI防坑警告] Lead Target 机制：不再死等 Mario 贴脸，
    // 而是结合 Mario 速度和机关预警时间提前触发，提高拦截成功率。
    private const float LEAD_TARGET_RANGE    = 6.0f;   // 提前量预判的最大距离
    private const float LEAD_HEIGHT_TOLERANCE = 2.0f;  // 同高度层判定容差

    // ── Trickster 防死锁参数 ──
    // [AI防坑警告] 防止 Trickster 在 Possessing 状态下无限等待 Mario。
    // 超过 POSSESS_TIMEOUT 秒且 Mario 未靠近时，强制解除附身重新走位。
    private const float POSSESS_TIMEOUT      = 6.0f;   // 附身超时（秒）
    private const float POSSESS_TIMEOUT_DIST = 8.0f;   // Mario 未靠近的距离阈值
    private float _possessTimer;                        // 附身状态累计时间

    // ── Trickster Roaming 射线避障参数 ──
    private const float T_WALL_CHECK_DIST    = 0.6f;   // 前方墙壁检测距离
    private const float T_WALL_CHECK_HEIGHT  = 0.4f;   // 墙壁检测射线高度
    private const float T_PIT_CHECK_FORWARD  = 0.8f;   // 前方坑洞检测偏移
    private const float T_PIT_CHECK_DEPTH    = 2.5f;   // 坑洞检测深度

    // ═══════════════════════════════════════════════════════════
    // Tick
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧由 InputManager.UpdateInputProvider() 调用。
    /// 执行顺序：清零上帧 Down → Brain 决策 → 返回（ReadP1/ReadP2 紧接着读取）。
    /// </summary>
    public void Tick(float dt)
    {
        ResetDownFlags();
        UpdateMarioBrain(dt);
        UpdateTricksterBrain(dt);
    }

    // ═══════════════════════════════════════════════════════════
    // Mario Brain — 基础探路与生存逻辑
    // ═══════════════════════════════════════════════════════════

    protected virtual void UpdateMarioBrain(float dt)
    {
        MarioIntent = "";

        if (!_marioCacheReady)
        {
            _mario = Object.FindObjectOfType<MarioController>();
            _probe = Object.FindObjectOfType<MarioCounterplayProbe>();
            _marioCacheReady = true;
        }
        if (_mario == null || !_mario.enabled)
        {
            p1Horizontal = 0f;
            p1JumpHeld = false;
            _jumpHoldTimer = 0f;
            return;
        }

        Vector2 marioPos = _mario.transform.position;
        float facingDir = _mario.IsFacingRight ? 1f : -1f;

        // ── 0. 防卡死：反向跳跃逃脱中，优先执行 ──
        if (_stuckEscapeTimer > 0f)
        {
            _stuckEscapeTimer -= dt;
            p1Horizontal = _stuckEscapeDir;
            facingDir = _stuckEscapeDir;
            if (_mario.IsGrounded && _jumpHoldTimer <= 0f)
            {
                p1JumpDown = true;
                p1JumpHeld = true;
                _jumpHoldTimer = JUMP_HOLD_DURATION;
            }
            // 跳跃持续按住
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
            return;
        }

        // ── 1. 目标寻路 ──
        Vector2? targetPos = FindMarioTarget();
        bool verticalWiggle = false;

        if (targetPos.HasValue)
        {
            float dx = targetPos.Value.x - marioPos.x;
            float dy = targetPos.Value.y - marioPos.y;

            // [垂直寻路] 目标在头顶且水平距离很近 → Wiggle 模式
            if (dy > VERTICAL_TARGET_DY && Mathf.Abs(dx) < VERTICAL_TARGET_DX)
            {
                verticalWiggle = true;
                _wiggleTimer += dt;
                // 左右徘徊：半周期向右，半周期向左
                float phase = _wiggleTimer % WIGGLE_PERIOD;
                p1Horizontal = phase < WIGGLE_PERIOD * 0.5f ? 1f : -1f;
            }
            else
            {
                if (Mathf.Abs(dx) > 0.3f)
                    p1Horizontal = dx > 0f ? 1f : -1f;
                else
                    p1Horizontal = 0f;
            }

            if (Mathf.Abs(p1Horizontal) > 0.01f)
                facingDir = p1Horizontal > 0f ? 1f : -1f;
        }
        else
        {
            p1Horizontal = 1f;
            facingDir = 1f;
        }

        // ── 2. 陷阱雷达：前方 3~4 格探测 ControllablePropBase ──
        _waitingForTrap = false;
        _baitingTrap = false;
        RaycastHit2D[] trapHits = Physics2D.BoxCastAll(
            marioPos + new Vector2(facingDir * 0.5f, 0.5f),
            new Vector2(0.5f, TRAP_RADAR_HEIGHT),
            0f,
            new Vector2(facingDir, 0f),
            TRAP_RADAR_RANGE
        );
        foreach (var hit in trapHits)
        {
            if (hit.collider == null) continue;
            ControllablePropBase prop = hit.collider.GetComponent<ControllablePropBase>();
            if (prop == null)
                prop = hit.collider.GetComponentInParent<ControllablePropBase>();
            if (prop == null) continue;
            PropControlState trapState = prop.GetControlState();
            if (trapState == PropControlState.Active)
            {
                // 机关正在激活中 → 停步等待
                _waitingForTrap = true;
                break;
            }
            if (trapState == PropControlState.Telegraph)
            {
                // 机关处于预警期 → 后退骗技能（Baiting）
                _baitingTrap = true;
                break;
            }
        }

        // 停步：机关正在激活，原地等待
        if (_waitingForTrap)
        {
            p1Horizontal = 0f;
            p1JumpHeld = false;
            _jumpHoldTimer = 0f;
            MarioIntent = "[Dodging Active Trap]";
            return;
        }

        // 骗技能：机关处于预警期，后退拉距离
        if (_baitingTrap)
        {
            p1Horizontal = -facingDir; // 反向后退
            p1JumpHeld = false;
            _jumpHoldTimer = 0f;
            MarioIntent = "[Baiting Trap]";
            return;
        }

        // ── 3. 射线避障与跳跃 ──
        LayerMask solidMask = GetSolidMask();
        bool shouldJump = false;

        if (_mario.IsGrounded)
        {
            // 遇坑跳（从 Mario 脚底前方向下打射线）
            Vector2 pitOrigin = marioPos + new Vector2(facingDir * PIT_CHECK_FORWARD, -0.3f);
            RaycastHit2D pitHit = Physics2D.Raycast(pitOrigin, Vector2.down, PIT_CHECK_DEPTH, solidMask);
            if (pitHit.collider == null)
                shouldJump = true;

            // 遇墙跳
            if (!shouldJump)
            {
                Vector2 wallOrigin = marioPos + new Vector2(0f, WALL_CHECK_HEIGHT);
                RaycastHit2D wallHit = Physics2D.Raycast(wallOrigin, new Vector2(facingDir, 0f), WALL_CHECK_DISTANCE, solidMask);
                if (wallHit.collider != null)
                    shouldJump = true;
            }
        }

        // [垂直寻路] Wiggle 模式下高频触发跳跃
        if (verticalWiggle && _mario.IsGrounded)
            shouldJump = true;

        if (shouldJump && _jumpHoldTimer <= 0f)
        {
            p1JumpDown = true;
            p1JumpHeld = true;
            _jumpHoldTimer = JUMP_HOLD_DURATION;
        }

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

        // ── 4. 防卡死检测：有水平输入但位移极小 → 反向跳跃脱离 ──
        if (Mathf.Abs(p1Horizontal) > 0.01f)
        {
            if (!_lastMarioPosValid)
            {
                _lastMarioPos = marioPos;
                _lastMarioPosValid = true;
                _stuckCheckTimer = 0f;
            }
            else
            {
                _stuckCheckTimer += dt;
                if (_stuckCheckTimer >= STUCK_CHECK_INTERVAL)
                {
                    float displacement = Mathf.Abs(marioPos.x - _lastMarioPos.x);
                    if (displacement < STUCK_MIN_DISPLACEMENT)
                    {
                        // 卡住了！强制反向跳跃
                        _stuckEscapeDir = -p1Horizontal;
                        if (Mathf.Abs(_stuckEscapeDir) < 0.01f)
                            _stuckEscapeDir = 1f;
                        else
                            _stuckEscapeDir = _stuckEscapeDir > 0f ? 1f : -1f;
                        _stuckEscapeTimer = STUCK_ESCAPE_DURATION;
                        _jumpHoldTimer = 0f; // 重置跳跃计时器以便立即跳
                    }
                    // 重置检测周期
                    _lastMarioPos = marioPos;
                    _stuckCheckTimer = 0f;
                }
            }
        }
        else
        {
            // 无水平输入时重置卡死检测
            _lastMarioPosValid = false;
            _stuckCheckTimer = 0f;
        }

        // ── 5. 自动反制（强扫描） ──
        // 只有 IsStrongScanReady 且附近有可附身锚点时才按 Q，避免浪费扫描
        if (_probe != null && _probe.IsStrongScanReady && HasNearbyAnchor(marioPos, SCAN_ANCHOR_RANGE))
        {
            p1ScanDown = true;
            MarioIntent = "[Executing Strong Scan]";
        }
        else if (string.IsNullOrEmpty(MarioIntent))
        {
            MarioIntent = "[Pathing]";
        }
    }

    /// <summary>
    /// Mario 目标选择策略：
    ///   - 已拿宝 (IsLootCarried=true)：直接去最近的撤离门
    ///   - 实战房未拿宝 (场景有 LootObjective)：必须先去拿 LootObjective，不能贪近去终点
    ///   - 旧关卡 (无 LootObjective)：去最近的 Collectible 或 GoalZone
    ///   - 无目标时默认向右探索
    /// </summary>
    private Vector2? FindMarioTarget()
    {
        if (_mario == null) return null;
        Vector2 marioPos = (Vector2)_mario.transform.position;

        // ── 已拿宝：直接去最近的撤离门/终点 ──
        if (LootObjective.IsLootCarried)
        {
            return FindNearestTarget<EscapeGate>(marioPos)
                ?? FindNearestTarget<GoalZone>(marioPos);
        }

        // ── 实战房未拿宝：必须先拿 LootObjective ──
        Vector2? lootPos = FindNearestTarget<LootObjective>(marioPos);
        if (lootPos.HasValue)
            return lootPos;

        // ── 旧关卡兼容：没有 LootObjective，去最近的 Collectible 或 GoalZone ──
        Vector2? best = null;
        float bestDist = float.MaxValue;
        TryUpdateNearest<Collectible>(marioPos, ref best, ref bestDist);
        TryUpdateNearest<GoalZone>(marioPos, ref best, ref bestDist);
        TryUpdateNearest<EscapeGate>(marioPos, ref best, ref bestDist);
        return best;
    }

    /// <summary>查找场景中最近的 T 类型组件位置</summary>
    private static Vector2? FindNearestTarget<T>(Vector2 from) where T : Component
    {
        T[] all = Object.FindObjectsOfType<T>();
        Vector2? best = null;
        float bestDist = float.MaxValue;
        foreach (var obj in all)
        {
            if (obj == null || !obj.gameObject.activeInHierarchy) continue;
            float dist = Vector2.Distance(from, (Vector2)obj.transform.position);
            if (dist < bestDist) { bestDist = dist; best = (Vector2)obj.transform.position; }
        }
        return best;
    }

    /// <summary>尝试用 T 类型的最近实例更新当前最佳目标</summary>
    private static void TryUpdateNearest<T>(Vector2 from, ref Vector2? best, ref float bestDist) where T : Component
    {
        T[] all = Object.FindObjectsOfType<T>();
        foreach (var obj in all)
        {
            if (obj == null || !obj.gameObject.activeInHierarchy) continue;
            float dist = Vector2.Distance(from, (Vector2)obj.transform.position);
            if (dist < bestDist) { bestDist = dist; best = (Vector2)obj.transform.position; }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Trickster Brain — 战术伏击逻辑
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Trickster 的启发式决策：
    ///   1. 战术走位 — 找 Mario 前方 3~8 格的空闲锚点并走过去（含射线避障）
    ///   2. 自动伪装 — 到达锚点后钻入
    ///   3. 精准处决 — 完全融入后等 Mario 踏入危险距离，加人类延迟后开机关
    ///      3b. 提前量预判 — 结合 Mario 速度和预警时间提前触发
    ///   4. 热度管理 — Alert/Lockdown 时强制停手 3 秒
    ///   5. 防死锁 — Possessing 超时且 Mario 未靠近时强制解除附身
    /// </summary>
    protected virtual void UpdateTricksterBrain(float dt)
    {
        // ── 惰性缓存 ──
        if (!_tricksterCacheReady)
        {
            _trickster = Object.FindObjectOfType<TricksterController>();
            if (_trickster != null)
            {
                _gate = _trickster.GetComponent<TricksterPossessionGate>();
                _ability = _trickster.AbilitySystem;
            }
            _heatMeter = Object.FindObjectOfType<TricksterHeatMeter>();
            _comboTracker = Object.FindObjectOfType<PropComboTracker>();
            _tricksterCacheReady = true;
        }
        if (_trickster == null || !_trickster.enabled)
        {
            p2Horizontal = 0f;
            return;
        }

        // Mario 引用（共享 Mario Brain 的缓存）
        if (_mario == null) return;

        TricksterIntent = "";

        // ── 热度规避计时器 ──
        if (_heatCooloffTimer > 0f)
        {
            _heatCooloffTimer -= dt;
        }

        // ── 检查热度：Alert 或 Lockdown 时启动冷静期 ──
        if (_heatMeter != null)
        {
            var tier = _heatMeter.CurrentTier;
            if (tier == TricksterHeatMeter.HeatTier.Alert ||
                tier == TricksterHeatMeter.HeatTier.Lockdown)
            {
                if (_heatCooloffTimer <= 0f)
                {
                    // 刚进入高热度，启动冷静计时器
                    _heatCooloffTimer = HEAT_COOLOFF_DURATION;
                }
            }
        }

        bool heatSuppressed = _heatCooloffTimer > 0f;

        // ── Lockdown 强制逃跑：解除附身 + 反向跑 ──
        if (_heatMeter != null && _heatMeter.CurrentTier == TricksterHeatMeter.HeatTier.Lockdown)
        {
            TricksterPossessionState lockState = TricksterPossessionState.Roaming;
            if (_gate != null) lockState = _gate.CurrentState;
            // 如果还在附身状态，强制解除
            if (lockState == TricksterPossessionState.Possessing ||
                lockState == TricksterPossessionState.Blending)
            {
                p2DisguiseDown = true;
            }
            // 向远离 Mario 的方向逃跑
            Vector2 tPos = _trickster.transform.position;
            Vector2 mPos = _mario.transform.position;
            float fleeDir = (tPos.x >= mPos.x) ? 1f : -1f;
            p2Horizontal = fleeDir;
            p2JumpDown = true; // 跳跃辅助越障
            TricksterIntent = "[Fleeing! High Heat]";
            return;
        }

        // ── 获取当前附身状态 ──
        TricksterPossessionState state = TricksterPossessionState.Roaming;
        if (_gate != null)
            state = _gate.CurrentState;

        Vector2 tricksterPos = _trickster.transform.position;
        Vector2 marioPos = _mario.transform.position;
        float marioFacing = _mario.IsFacingRight ? 1f : -1f;

        // ── 连锁追击：在连锁窗口内加速寻找不同类型锚点 ──
        bool comboRushing = false;
        if (_comboTracker != null && _comboTracker.IsComboActive && !heatSuppressed)
        {
            // 记录上次连锁的道具类型
            var history = _comboTracker.ComboHistory;
            if (history.Count > 0)
                _lastComboPropName = history[history.Count - 1].PropName;
            comboRushing = true;
            TricksterIntent = "[Chasing Combo]";
        }

        switch (state)
        {
            // ────────────────────────────────────────
            // 状态 A: Roaming — 战术走位 + 自动伪装 + 射线避障
            // ────────────────────────────────────────
            case TricksterPossessionState.Roaming:
                HandleRoaming(tricksterPos, marioPos, marioFacing, comboRushing);
                _possessTimer = 0f; // 重置附身计时器
                break;

            // ────────────────────────────────────────
            // 状态 B: Blending — 等待融入完成，不输出任何按键
            // ────────────────────────────────────────
            case TricksterPossessionState.Blending:
                p2Horizontal = 0f;
                // 清除处决状态（新一轮伏击）
                _executeArmed = false;
                _executeDelayTimer = 0f;
                _possessTimer = 0f;
                break;

            // ────────────────────────────────────────
            // 状态 C: Possessing — 精准处决 + 提前量预判 + 防死锁
            // ────────────────────────────────────────
            case TricksterPossessionState.Possessing:
                HandlePossessing(dt, marioPos, heatSuppressed);
                break;

            // ────────────────────────────────────────
            // 状态 D: Revealed / Escaping — 被暴露，停止一切操作
            // ────────────────────────────────────────
            case TricksterPossessionState.Revealed:
            case TricksterPossessionState.Escaping:
                p2Horizontal = 0f;
                _executeArmed = false;
                _executeDelayTimer = 0f;
                _targetAnchor = null;
                _possessTimer = 0f;
                break;

            // ────────────────────────────────────────
            // 状态 E: Underlining — 暗线转移中，不干预
            // ────────────────────────────────────────
            case TricksterPossessionState.Underlining:
                p2Horizontal = 0f;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Trickster Brain 子逻辑
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Roaming 状态：寻找 Mario 前方的空闲锚点并走过去，到达后自动伪装。
    /// [升级] 加入射线检测：遇墙或遇坑时强制跳跃越障。
    /// </summary>
    private void HandleRoaming(Vector2 tricksterPos, Vector2 marioPos, float marioFacing, bool comboRushing = false)
    {
        // 如果没有目标锚点或目标锚点已失效，重新选择
        if (_targetAnchor == null || !_targetAnchor.CanBePossessed())
        {
            _targetAnchor = comboRushing
                ? FindComboAnchor(marioPos, marioFacing)
                : FindAmbushAnchor(marioPos, marioFacing);
        }
        // 连锁中如果当前锚点和上次同类型，强制重新选择不同类型
        if (comboRushing && _targetAnchor != null && _targetAnchor.ControllableProp != null
            && _targetAnchor.ControllableProp.PropName == _lastComboPropName)
        {
            _targetAnchor = FindComboAnchor(marioPos, marioFacing);
        }

        if (_targetAnchor == null)
        {
            // 场景中没有可用锚点，原地待命
            p2Horizontal = 0f;
            return;
        }

        Vector2 anchorPos = (Vector2)_targetAnchor.AnchorTransform.position;
        float dx = anchorPos.x - tricksterPos.x;

        if (Mathf.Abs(dx) > ANCHOR_ARRIVE_DIST)
        {
            // 还没到，继续走
            float moveDir = dx > 0f ? 1f : -1f;
            p2Horizontal = moveDir;

            // ── 射线避障：遇墙或遇坑强制跳跃 ──
            if (_trickster.IsGrounded)
            {
                LayerMask solidMask = GetSolidMask();
                bool needJump = false;

                // 遇墙检测
                Vector2 wallOrigin = tricksterPos + new Vector2(0f, T_WALL_CHECK_HEIGHT);
                RaycastHit2D wallHit = Physics2D.Raycast(
                    wallOrigin, new Vector2(moveDir, 0f), T_WALL_CHECK_DIST, solidMask);
                if (wallHit.collider != null)
                    needJump = true;

                // 遇坑检测
                if (!needJump)
                {
                    Vector2 pitOrigin = tricksterPos + new Vector2(moveDir * T_PIT_CHECK_FORWARD, -0.3f);
                    RaycastHit2D pitHit = Physics2D.Raycast(
                        pitOrigin, Vector2.down, T_PIT_CHECK_DEPTH, solidMask);
                    if (pitHit.collider == null)
                        needJump = true;
                }

                if (needJump)
                {
                    p2JumpDown = true;
                    p2JumpHeld = true;
                }
            }
        }
        else
        {
            // 到达锚点，停止移动并触发伪装
            p2Horizontal = 0f;
            p2DisguiseDown = true;
            if (string.IsNullOrEmpty(TricksterIntent))
                TricksterIntent = "[Disguising]";
        }

        if (string.IsNullOrEmpty(TricksterIntent))
            TricksterIntent = "[Roaming to Anchor]";
    }

    /// <summary>
    /// Possessing 状态：完全融入后等待 Mario 进入危险距离，加人类延迟后触发处决。
    /// 热度过高时强制停手。
    /// [升级] 提前量预判：结合 Mario 速度和预警时间提前触发。
    /// [升级] 防死锁：超时且 Mario 未靠近时强制解除附身。
    /// </summary>
    private void HandlePossessing(float dt, Vector2 marioPos, bool heatSuppressed)
    {
        p2Horizontal = 0f;

        // ── 防死锁：附身超时检测 ──
        _possessTimer += dt;

        // 必须有附身锚点
        PossessionAnchor currentAnchor = _gate != null ? _gate.CurrentAnchor : null;
        if (currentAnchor == null) return;

        Vector2 anchorPos = (Vector2)currentAnchor.AnchorTransform.position;
        float distToMario = Vector2.Distance(marioPos, anchorPos);

        // [防死锁] 附身超过 POSSESS_TIMEOUT 秒且 Mario 未靠近 → 强制解除附身重新走位
        if (_possessTimer >= POSSESS_TIMEOUT && distToMario > POSSESS_TIMEOUT_DIST)
        {
            p2DisguiseDown = true;
            _executeArmed = false;
            _executeDelayTimer = 0f;
            _targetAnchor = null;
            _possessTimer = 0f;
            TricksterIntent = "[Anti-Deadlock: Unpossessing]";
            return;
        }

        // 热度压制：高热度时不开机关
        if (heatSuppressed) return;

        // 必须完全融入
        if (!_trickster.IsFullyBlended) return;

        // ── 提前量预判 (Lead Target) ──
        // 获取当前绑定机关的预警时间
        float telegraphDuration = 0f;
        IControllableProp boundProp = _ability != null ? _ability.BoundProp : null;
        if (boundProp != null)
            telegraphDuration = boundProp.GetTelegraphDuration();

        // 计算 Mario 速度在机关方向上的分量
        Vector2 marioVelocity = _mario.Velocity;
        Vector2 toAnchor = anchorPos - marioPos;
        float approachSpeed = 0f;
        if (toAnchor.sqrMagnitude > 0.01f)
            approachSpeed = Vector2.Dot(marioVelocity, toAnchor.normalized);

        // 预测 Mario 在预警时间后的距离
        // 只有 Mario 正在靠近（approachSpeed > 0）时才做提前量
        float predictedDist = distToMario;
        if (approachSpeed > 0.1f && telegraphDuration > 0f)
            predictedDist = distToMario - approachSpeed * telegraphDuration;

        // 同高度层判定：Mario 和机关的 Y 差距在容差内
        float heightDiff = Mathf.Abs(marioPos.y - anchorPos.y);
        bool sameHeight = heightDiff <= LEAD_HEIGHT_TOLERANCE;

        // 判定条件：Mario 贴脸（原逻辑）或 提前量预判命中
        bool inKillZone = distToMario <= EXECUTE_KILL_DIST;
        bool leadTargetHit = sameHeight
            && distToMario <= LEAD_TARGET_RANGE
            && approachSpeed > 0.1f
            && predictedDist <= EXECUTE_KILL_DIST;

        if (inKillZone || leadTargetHit)
        {
            TricksterIntent = leadTargetHit && !inKillZone
                ? "[Lead Target: Pre-firing]"
                : "[Executing Kill]";
            // Mario 进入危险距离（实际或预测）
            if (!_executeArmed)
            {
                // 首次进入：启动人类延迟计时器
                _executeArmed = true;
                _executeDelayTimer = Random.Range(EXECUTE_DELAY_MIN, EXECUTE_DELAY_MAX);
            }
            else
            {
                // 正在等待延迟
                _executeDelayTimer -= dt;
                if (_executeDelayTimer <= 0f)
                {
                    // 延迟结束，检查门禁是否允许操作
                    if (_ability != null && _ability.IsPossessionActionAllowed)
                    {
                        p2AbilityDown = true;
                    }
                    // 无论是否成功，重置处决状态（防止连续触发）
                    _executeArmed = false;
                    _executeDelayTimer = 0f;
                }
            }
        }
        else
        {
            // Mario 离开危险距离，重置处决状态
            if (_executeArmed)
            {
                _executeArmed = false;
                _executeDelayTimer = 0f;
            }
            if (string.IsNullOrEmpty(TricksterIntent))
                TricksterIntent = "[Possessing: Waiting]";
        }
    }

    /// <summary>
    /// 在所有 PossessionAnchor 中，找一个位于 Mario 前方 3~8 格内的空闲锚点。
    /// 优先选择最靠近 Mario 前进路线的锚点（距离最近的）。
    /// </summary>
    private PossessionAnchor FindAmbushAnchor(Vector2 marioPos, float marioFacing)
    {
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>();
        if (anchors == null || anchors.Length == 0) return null;

        PossessionAnchor best = null;
        float bestDist = float.MaxValue;

        foreach (var anchor in anchors)
        {
            if (anchor == null || !anchor.CanBePossessed()) continue;

            Vector2 anchorPos = (Vector2)anchor.AnchorTransform.position;
            float dx = (anchorPos.x - marioPos.x) * marioFacing;

            // 必须在 Mario 前方 AMBUSH_RANGE_MIN ~ AMBUSH_RANGE_MAX 格内
            if (dx < AMBUSH_RANGE_MIN || dx > AMBUSH_RANGE_MAX) continue;

            // 在合格范围内选距离最近的
            float dist = Vector2.Distance(marioPos, anchorPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = anchor;
            }
        }

        // 如果前方没有合适锚点，退而求其次：选任意可用锚点中最近的
        if (best == null)
        {
            foreach (var anchor in anchors)
            {
                if (anchor == null || !anchor.CanBePossessed()) continue;

                float dist = Vector2.Distance(marioPos, (Vector2)anchor.AnchorTransform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = anchor;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// 连锁追击用锚点选择：优先选择与上次连锁不同类型的锚点，扩大搜索范围。
    /// </summary>
    private PossessionAnchor FindComboAnchor(Vector2 marioPos, float marioFacing)
    {
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>();
        if (anchors == null || anchors.Length == 0) return null;

        PossessionAnchor bestDiff = null;  // 不同类型优先
        float bestDiffDist = float.MaxValue;
        PossessionAnchor bestAny = null;   // 任意可用回退
        float bestAnyDist = float.MaxValue;

        foreach (var anchor in anchors)
        {
            if (anchor == null || !anchor.CanBePossessed()) continue;
            float dist = Vector2.Distance(marioPos, (Vector2)anchor.AnchorTransform.position);
            if (dist > COMBO_RUSH_RANGE_MAX) continue;

            // 记录任意可用锚点
            if (dist < bestAnyDist)
            {
                bestAnyDist = dist;
                bestAny = anchor;
            }

            // 优先选不同类型
            string propName = anchor.ControllableProp != null ? anchor.ControllableProp.PropName : "";
            if (!string.IsNullOrEmpty(_lastComboPropName) && propName == _lastComboPropName)
                continue;

            if (dist < bestDiffDist)
            {
                bestDiffDist = dist;
                bestDiff = anchor;
            }
        }

        return bestDiff != null ? bestDiff : bestAny;
    }

    /// <summary>
    /// 检测指定位置附近是否有可附身锚点（用于 Mario 强扫描前置条件）。
    /// </summary>
    private static bool HasNearbyAnchor(Vector2 pos, float range)
    {
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>();
        foreach (var anchor in anchors)
        {
            if (anchor == null || !anchor.CanBePossessed()) continue;
            if (Vector2.Distance(pos, (Vector2)anchor.AnchorTransform.position) <= range)
                return true;
        }
        return false;
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
        _marioCacheReady = false;
        _mario = null;
        _probe = null;
        _jumpHoldTimer = 0f;
        _solidMaskReady = false;

        // Mario 防卡死状态重置
        _wiggleTimer = 0f;
        _stuckCheckTimer = 0f;
        _lastMarioPosValid = false;
        _stuckEscapeTimer = 0f;

        _tricksterCacheReady = false;
        _trickster = null;
        _gate = null;
        _ability = null;
        _heatMeter = null;
        _comboTracker = null;
        _lastComboPropName = "";
        _targetAnchor = null;
        _executeArmed = false;
        _executeDelayTimer = 0f;
        _heatCooloffTimer = 0f;
        _possessTimer = 0f;
    }

    /// <summary>
    /// 获取地面检测用的 LayerMask。
    /// 优先从 MarioController 的 groundLayer 字段读取（通过反射），
    /// 保证 AI 射线与角色自身地面检测使用完全相同的层。
    /// 回退顺序：MarioController.groundLayer → "Ground" 层 → "Land" 层 → Default 层。
    /// </summary>
    private LayerMask GetSolidMask()
    {
        if (_solidMaskReady) return _solidMask;

        // 优先从 MarioController 读取 groundLayer（与角色地面检测完全一致）
        if (_mario != null)
        {
            var field = typeof(MarioController).GetField("groundLayer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                _solidMask = (LayerMask)field.GetValue(_mario);
                if (_solidMask.value != 0)
                {
                    _solidMaskReady = true;
                    return _solidMask;
                }
            }
        }

        // 回退：尝试 "Ground" 层名
        int idx = LayerMask.NameToLayer("Ground");
        if (idx >= 0) { _solidMask = 1 << idx; _solidMaskReady = true; return _solidMask; }

        // 回退：尝试 "Land" 层名（项目实际使用的层名）
        idx = LayerMask.NameToLayer("Land");
        if (idx >= 0) { _solidMask = 1 << idx; _solidMaskReady = true; return _solidMask; }

        // 最终回退：Default 层
        _solidMask = 1 << 0;
        _solidMaskReady = true;
        return _solidMask;
    }

    // ═══════════════════════════════════════════════════════════
    // 单帧事件清零
    // ═══════════════════════════════════════════════════════════

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
