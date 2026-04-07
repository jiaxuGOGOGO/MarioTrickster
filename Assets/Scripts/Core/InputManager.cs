using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地双人输入管理器
///
/// S49 重构：输入解耦（全自动测试基建）
///   - 引入 IInputProvider 接口，将键盘/手柄读取抽象为可替换的输入源
///   - 默认使用 KeyboardInputProvider（真实键盘+手柄输入）
///   - 自动化测试时通过 SetInputProvider() 注入 AutomatedInputProvider
///   - 所有组合键逻辑（S+Jump 下落穿越）和分发逻辑保持不变
///   - 原有的 DispatchP1/DispatchP2/LateReset 逻辑完全保留
///
/// 键盘方案：
///   P1 (Mario)    : WASD 移动 | Space 跳跃 | Q 扫描
///                   S+Space（下+跳）: 单向平台下落（Session 19 改为组合键）
///                   S 键（下蹲/交互）: 隐藏通道传送
///   P2 (Trickster): 左右方向键 移动 | 上方向键/右Ctrl 跳跃
///                   P 伪装 | O/I 切换形态 | L 操控道具
///
/// 手柄方案：
///   第一个手柄 → Mario，第二个手柄 → Trickster
///   左摇杆移动 | 南键(A/×) 跳跃 | 东键(B/○) 扫描
///   右肩键 下一形态 | 左肩键 上一形态 | 北键(Y/△) 操控道具
///
/// Session 10 更新：添加 P1 扫描技能按键
/// Session 17 更新：
///   - 添加 S 键下蹲交互路由
/// Session 19 更新：
///   - 单向平台下落改为 S+Jump 组合键（Down+Jump），避免误操作
///   - 隐藏通道传送保持 S 键单独触发
///   - 参考 Super Smash Bros / 行业标准的 Down+Jump 穿越平台做法
/// Session 20 更新：
///   - 融入状态下方向键拦截：不再作为移动输入，而是转发给 TricksterController.OnDirectionInput()
///   - 融入状态下方向键不产生 moveInput，防止打破融入状态
///   - 上方向键在融入状态下也作为磁吸切换方向（不触发跳跃）
/// Session 49 更新（全自动测试基建）：
///   - 引入 IInputProvider 接口解耦输入源
///   - ReadP1/ReadP2 不再直接调用 Input.GetKey，改为通过 _inputProvider 读取
///   - 新增 SetInputProvider() 公共方法，支持运行时热替换输入源
///   - 新增 GetCurrentProvider() 供测试代码查询当前输入源
///
/// 使用方式：挂载到 Managers 对象，在 Inspector 中拖入 Mario 和 Trickster 引用
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("玩家引用")]
    [SerializeField] private MarioController marioController;
    [SerializeField] private TricksterController tricksterController;

    [Header("调试")]
    [SerializeField] private bool showDebugInput = false;

    // ── S49: 输入提供者（可替换的输入源）──────────────────────
    // [AI防坑警告] _inputProvider 是所有输入读取的唯一入口。
    // 绝对不要在 ReadP1/ReadP2 中直接调用 Input.GetKey()！
    // 所有键盘/手柄读取已迁移到 KeyboardInputProvider 中。
    // 自动化测试通过 SetInputProvider(new AutomatedInputProvider(...)) 注入虚拟输入。
    private IInputProvider _inputProvider;

    // ── P1 输入状态 ────────────────────────────────────────
    private Vector2 p1Move;
    private bool p1JumpDown;
    private bool p1JumpHeld;
    private bool p1WasJumpHeld;
    private bool p1ScanDown;
    private bool p1DownInteractDown;     // S键下蹲交互（隐藏通道）
    private bool p1DropThroughDown;      // Session 19: S+Jump 组合键（单向平台下落）
    private bool p1SHeld;               // S键持续按住状态

    // ── P2 输入状态 ────────────────────────────────────────
    private Vector2 p2Move;
    private bool p2JumpDown;
    private bool p2JumpHeld;
    private bool p2WasJumpHeld;
    private bool p2DisguiseDown;
    private bool p2SwitchDown;
    private float p2SwitchDir;
    private bool p2AbilityDown;

    // ── Session 20: P2 方向键原始输入（用于磁吸切换）────────
    private Vector2 p2RawDirection;
    private bool p2DirectionDown; // 方向键刚按下（单帧事件）

    // ── Mario 扫描技能引用（自动查找）────────────────────────
    private ScanAbility marioScanAbility;

    private void Start()
    {
        // S49: 默认使用键盘输入提供者
        if (_inputProvider == null)
        {
            _inputProvider = new KeyboardInputProvider();
        }

        // 自动查找 Mario 的扫描技能
        if (marioController != null)
        {
            marioScanAbility = marioController.GetComponent<ScanAbility>();
        }
    }

    // ─────────────────────────────────────────────────────
    private void Update()
    {
        // S49: 确保 inputProvider 已初始化（防御 Start 未执行的边界情况）
        if (_inputProvider == null)
        {
            _inputProvider = new KeyboardInputProvider();
        }

        // S49: 更新输入源（手柄检测 / 自动化帧推进）
        UpdateInputProvider();

        ReadP1();
        ReadP2();
        DispatchP1();
        DispatchP2();
        LateReset();
    }

    // ─────────────────────────────────────────────────────
    #region S49: 输入源管理

    /// <summary>
    /// 更新输入源的内部状态。
    /// KeyboardInputProvider: 检测手柄连接
    /// AutomatedInputProvider: 推进帧计数器
    /// </summary>
    private void UpdateInputProvider()
    {
        if (_inputProvider is KeyboardInputProvider kbProvider)
        {
            kbProvider.UpdateGamepads();
        }
        else if (_inputProvider is AutomatedInputProvider autoProvider)
        {
            autoProvider.Tick();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region P1 (Mario) 输入

    // [AI防坑警告] ReadP1 的所有输入读取必须通过 _inputProvider 接口。
    // 组合键逻辑（S+Jump 下落穿越）保留在此方法中，因为这是 InputManager 的职责。
    // _inputProvider 只负责提供原子输入信号，不做组合判断。
    private void ReadP1()
    {
        float h = _inputProvider.GetP1Horizontal();
        float v = _inputProvider.GetP1Vertical();

        p1Move = new Vector2(Mathf.Clamp(h, -1f, 1f), Mathf.Clamp(v, -1f, 1f));

        bool jumpHeld = _inputProvider.GetP1JumpHeld();

        p1JumpDown = jumpHeld && !p1WasJumpHeld;
        p1JumpHeld = jumpHeld;

        // S键持续按住状态
        p1SHeld = _inputProvider.GetP1SHeld();

        // 扫描（Q 键 / 手柄东键 B/○）
        if (_inputProvider.GetP1ScanDown())
            p1ScanDown = true;

        // Session 19: S+Jump 组合键检测（单向平台下落）
        // 当 S 键被按住 且 Jump 键刚按下时，触发下落穿越
        if (p1SHeld && p1JumpDown)
        {
            p1DropThroughDown = true;
        }

        // Session 17/19: S键下蹲交互（隐藏通道传送）
        // 只在 S 键单独按下（不与 Jump 组合）时触发
        if (_inputProvider.GetP1SDown())
        {
            // 标记为待处理，在 Dispatch 中判断是否被组合键消费
            p1DownInteractDown = true;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region P2 (Trickster) 输入

    // [AI防坑警告] ReadP2 的所有输入读取必须通过 _inputProvider 接口。
    // Session 20 的融入状态拦截逻辑保留在 DispatchP2 中（不在此处）。
    private void ReadP2()
    {
        float h = _inputProvider.GetP2Horizontal();
        float v = _inputProvider.GetP2Vertical();

        // Session 20: 保存原始方向输入（含上下），用于磁吸切换
        p2RawDirection = new Vector2(Mathf.Clamp(h, -1f, 1f), Mathf.Clamp(v, -1f, 1f));

        // 移动输入仍只使用水平分量（原始行为保持不变）
        p2Move = new Vector2(Mathf.Clamp(h, -1f, 1f), 0f);

        // Session 20: 检测方向键刚按下（单帧事件，用于磁吸切换触发）
        if (_inputProvider.GetP2DirectionDown())
        {
            p2DirectionDown = true;
        }

        bool jumpHeld = _inputProvider.GetP2JumpHeld();

        p2JumpDown = jumpHeld && !p2WasJumpHeld;
        p2JumpHeld = jumpHeld;

        // 伪装（P 键）
        if (_inputProvider.GetP2DisguiseDown())
            p2DisguiseDown = true;

        // 切换形态（O = 下一个, I = 上一个）
        float switchDir = _inputProvider.GetP2SwitchDirection();
        if (Mathf.Abs(switchDir) > 0.01f)
        {
            p2SwitchDown = true;
            p2SwitchDir = switchDir;
        }

        // 操控道具（L 键）
        if (_inputProvider.GetP2AbilityDown())
            p2AbilityDown = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 输入分发

    private void DispatchP1()
    {
        if (marioController == null) return;

        marioController.SetMoveInput(p1Move);

        // Session 19: S+Jump 组合键 → 单向平台下落（优先级最高）
        if (p1DropThroughDown)
        {
            MarioInteractionHelper.HandleDropThrough(marioController.gameObject);
            // 组合键已消费 Jump，不再触发普通跳跃
            // 同时消费 S 的交互事件，避免同时触发隐藏通道
            p1DownInteractDown = false;
        }
        else
        {
            // 普通跳跃（非组合键时）
            if (p1JumpDown)              marioController.OnJumpPressed();
            if (!p1JumpHeld && p1WasJumpHeld) marioController.OnJumpReleased();
        }

        // 扫描技能
        if (p1ScanDown && marioScanAbility != null)
        {
            marioScanAbility.ActivateScan();
        }

        // Session 19: S键单独交互 → 隐藏通道传送（仅在非组合键时触发）
        if (p1DownInteractDown && !p1DropThroughDown)
        {
            MarioInteractionHelper.HandlePassageInteraction(marioController.gameObject);
        }
    }

    private void DispatchP2()
    {
        if (tricksterController == null) return;

        // Session 20: 融入状态下方向键拦截
        // 当 Trickster 处于完全融入状态时：
        //   - 方向键不作为移动输入（防止打破融入状态）
        //   - 方向键转发为磁吸切换目标指令
        //   - 上方向键不触发跳跃（融入状态下不可跳跃）
        bool isBlended = tricksterController.IsFullyBlended;

        if (isBlended)
        {
            // 融入状态：方向键拦截为磁吸切换
            tricksterController.SetMoveInput(Vector2.zero); // 不产生移动

            // 方向键刚按下时触发磁吸切换
            if (p2DirectionDown && p2RawDirection.sqrMagnitude > 0.01f)
            {
                tricksterController.OnDirectionInput(p2RawDirection);
            }

            // 融入状态下不触发跳跃（上方向键已被拦截）
            // 但仍允许 RightControl/Keypad0 触发跳跃（如果需要的话可以取消注释）
            // if (p2JumpDown) tricksterController.OnJumpPressed();
            // if (!p2JumpHeld && p2WasJumpHeld) tricksterController.OnJumpReleased();
        }
        else
        {
            // 正常状态：方向键作为移动输入
            tricksterController.SetMoveInput(p2Move);

            if (p2JumpDown)                   tricksterController.OnJumpPressed();
            if (!p2JumpHeld && p2WasJumpHeld) tricksterController.OnJumpReleased();
        }

        if (p2DisguiseDown)               tricksterController.OnDisguisePressed();
        if (p2SwitchDown)                 tricksterController.OnSwitchDisguise(p2SwitchDir);
        if (p2AbilityDown)                tricksterController.OnAbilityPressed();
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 帧末重置

    private void LateReset()
    {
        // 记录本帧 held 状态，供下帧判断 JumpReleased
        p1WasJumpHeld = p1JumpHeld;
        p2WasJumpHeld = p2JumpHeld;

        // 单帧事件清零
        p1JumpDown = false;
        p1ScanDown = false;
        p1DownInteractDown = false;
        p1DropThroughDown = false; // Session 19
        p2JumpDown = false;
        p2DisguiseDown = false;
        p2SwitchDown = false;
        p2AbilityDown = false;
        p2DirectionDown = false; // Session 20
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共接口

    public void DisableAllInput()
    {
        enabled = false;
        marioController?.SetMoveInput(Vector2.zero);
        tricksterController?.SetMoveInput(Vector2.zero);
    }

    public void EnableAllInput() => enabled = true;

    public void SetMarioController(MarioController c)
    {
        marioController = c;
        // 重新查找扫描技能
        marioScanAbility = c != null ? c.GetComponent<ScanAbility>() : null;
    }

    public void SetTricksterController(TricksterController c) => tricksterController = c;

    /// <summary>
    /// S49: 热替换输入源。
    /// 
    /// 正常游戏时使用 KeyboardInputProvider（默认）。
    /// 自动化测试时注入 AutomatedInputProvider。
    /// 
    /// 用法：
    ///   inputManager.SetInputProvider(new AutomatedInputProvider(sequence));
    /// 
    /// 传入 null 会回退到 KeyboardInputProvider。
    /// </summary>
    public void SetInputProvider(IInputProvider provider)
    {
        _inputProvider = provider ?? new KeyboardInputProvider();
    }

    /// <summary>
    /// S49: 获取当前输入源（供测试代码查询状态）。
    /// </summary>
    public IInputProvider GetCurrentProvider() => _inputProvider;

    #endregion

    // ─────────────────────────────────────────────────────
    #region 调试 GUI

    private void OnGUI()
    {
        if (!showDebugInput) return;
        GUILayout.BeginArea(new Rect(10, 10, 280, 220));
        GUILayout.Label($"P1 Move: {p1Move}  JumpHeld: {p1JumpHeld}  Scan: {p1ScanDown}");
        GUILayout.Label($"P1 SHeld: {p1SHeld}  DropThrough: {p1DropThroughDown}");
        GUILayout.Label($"P2 Move: {p2Move}  JumpHeld: {p2JumpHeld}  Blended: {(tricksterController != null ? tricksterController.IsFullyBlended.ToString() : "N/A")}");
        GUILayout.Label($"P2 Disguise: {p2DisguiseDown}  Ability: {p2AbilityDown}  DirSwitch: {p2DirectionDown}");
        // S49: 显示当前输入源类型
        string providerName = _inputProvider != null ? _inputProvider.GetType().Name : "None";
        GUILayout.Label($"InputProvider: {providerName}");
        GUILayout.EndArea();
    }

    #endregion
}
