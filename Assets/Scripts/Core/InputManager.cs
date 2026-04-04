using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地双人输入管理器
///
/// 键盘方案：
///   P1 (Mario)    : WASD 移动 | Space 跳跃 | Q 扫描
///                   S 键（下蹲/交互）: 单向平台下落 + 隐藏通道传送
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
///   - 添加 S 键下蹲交互路由：
///     · 检测 Mario 脚下的 OneWayPlatform → 调用 AllowDropThrough()
///     · 检测 Mario 所在的 HiddenPassage → 调用 TryEnterPassage()
///   - 使用 MarioInteractionHelper 统一处理交互检测
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

    // ── 手柄 ──────────────────────────────────────────────
    private Gamepad gamepad1;
    private Gamepad gamepad2;

    // ── P1 输入状态 ────────────────────────────────────────
    private Vector2 p1Move;
    private bool p1JumpDown;
    private bool p1JumpHeld;
    private bool p1WasJumpHeld;
    private bool p1ScanDown;
    private bool p1DownInteractDown; // Session 17: S键下蹲交互

    // ── P2 输入状态 ────────────────────────────────────────
    private Vector2 p2Move;
    private bool p2JumpDown;
    private bool p2JumpHeld;
    private bool p2WasJumpHeld;
    private bool p2DisguiseDown;
    private bool p2SwitchDown;
    private float p2SwitchDir;
    private bool p2AbilityDown;

    // ── Mario 扫描技能引用（自动查找）────────────────────────
    private ScanAbility marioScanAbility;

    private void Start()
    {
        // 自动查找 Mario 的扫描技能
        if (marioController != null)
        {
            marioScanAbility = marioController.GetComponent<ScanAbility>();
        }
    }

    // ─────────────────────────────────────────────────────
    private void Update()
    {
        DetectGamepads();
        ReadP1();
        ReadP2();
        DispatchP1();
        DispatchP2();
        LateReset();
    }

    // ─────────────────────────────────────────────────────
    #region 手柄检测

    private void DetectGamepads()
    {
        var pads = Gamepad.all;
        gamepad1 = pads.Count > 0 ? pads[0] : null;
        gamepad2 = pads.Count > 1 ? pads[1] : null;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region P1 (Mario) 输入

    private void ReadP1()
    {
        float h = 0f, v = 0f;

        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;

        if (gamepad1 != null)
        {
            Vector2 s = gamepad1.leftStick.ReadValue();
            if (s.magnitude > 0.1f) { h = s.x; v = s.y; }
        }

        p1Move = new Vector2(Mathf.Clamp(h, -1f, 1f), Mathf.Clamp(v, -1f, 1f));

        bool jumpKey = Input.GetKey(KeyCode.Space)
                    || (gamepad1 != null && gamepad1.buttonSouth.isPressed);

        p1JumpDown = jumpKey && !p1WasJumpHeld;
        p1JumpHeld = jumpKey;

        // 扫描（Q 键 / 手柄东键 B/○）
        if (Input.GetKeyDown(KeyCode.Q)
            || (gamepad1 != null && gamepad1.buttonEast.wasPressedThisFrame))
            p1ScanDown = true;

        // Session 17: S键下蹲交互（按下瞬间触发）
        if (Input.GetKeyDown(KeyCode.S)
            || (gamepad1 != null && gamepad1.dpad.down.wasPressedThisFrame))
            p1DownInteractDown = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region P2 (Trickster) 输入

    private void ReadP2()
    {
        float h = 0f, v = 0f;

        if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.LeftArrow))  h -= 1f;

        if (gamepad2 != null)
        {
            Vector2 s = gamepad2.leftStick.ReadValue();
            if (s.magnitude > 0.1f) { h = s.x; v = s.y; }
        }

        p2Move = new Vector2(Mathf.Clamp(h, -1f, 1f), Mathf.Clamp(v, -1f, 1f));

        bool jumpKey = Input.GetKey(KeyCode.UpArrow)
                    || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.Keypad0)
                    || (gamepad2 != null && gamepad2.buttonSouth.isPressed);

        p2JumpDown = jumpKey && !p2WasJumpHeld;
        p2JumpHeld = jumpKey;

        // 伪装（P 键）
        if (Input.GetKeyDown(KeyCode.P)
            || (gamepad2 != null && gamepad2.buttonWest.wasPressedThisFrame))
            p2DisguiseDown = true;

        // 切换形态（O = 下一个, I = 上一个）
        if (Input.GetKeyDown(KeyCode.O)
            || (gamepad2 != null && gamepad2.rightShoulder.wasPressedThisFrame))
        { p2SwitchDown = true; p2SwitchDir = 1f; }

        if (Input.GetKeyDown(KeyCode.I)
            || (gamepad2 != null && gamepad2.leftShoulder.wasPressedThisFrame))
        { p2SwitchDown = true; p2SwitchDir = -1f; }

        // 操控道具（L 键）
        if (Input.GetKeyDown(KeyCode.L)
            || (gamepad2 != null && gamepad2.buttonNorth.wasPressedThisFrame))
            p2AbilityDown = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 输入分发

    private void DispatchP1()
    {
        if (marioController == null) return;

        marioController.SetMoveInput(p1Move);

        if (p1JumpDown)              marioController.OnJumpPressed();
        if (!p1JumpHeld && p1WasJumpHeld) marioController.OnJumpReleased();

        // 扫描技能
        if (p1ScanDown && marioScanAbility != null)
        {
            marioScanAbility.ActivateScan();
        }

        // Session 17: S键下蹲交互 — 单向平台下落 + 隐藏通道传送
        if (p1DownInteractDown)
        {
            MarioInteractionHelper.HandleDownInteraction(marioController.gameObject);
        }
    }

    private void DispatchP2()
    {
        if (tricksterController == null) return;

        tricksterController.SetMoveInput(p2Move);

        if (p2JumpDown)                   tricksterController.OnJumpPressed();
        if (!p2JumpHeld && p2WasJumpHeld) tricksterController.OnJumpReleased();
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
        p1DownInteractDown = false; // Session 17
        p2JumpDown = false;
        p2DisguiseDown = false;
        p2SwitchDown = false;
        p2AbilityDown = false;
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

    #endregion

    // ─────────────────────────────────────────────────────
    #region 调试 GUI

    private void OnGUI()
    {
        if (!showDebugInput) return;
        GUILayout.BeginArea(new Rect(10, 10, 280, 200));
        GUILayout.Label($"P1 Move: {p1Move}  JumpHeld: {p1JumpHeld}  Scan: {p1ScanDown}");
        GUILayout.Label($"P2 Move: {p2Move}  JumpHeld: {p2JumpHeld}");
        GUILayout.Label($"P2 Disguise: {p2DisguiseDown}  Ability: {p2AbilityDown}");
        GUILayout.Label($"Pad1: {(gamepad1 != null ? gamepad1.displayName : "None")}");
        GUILayout.Label($"Pad2: {(gamepad2 != null ? gamepad2.displayName : "None")}");
        GUILayout.EndArea();
    }

    #endregion
}
