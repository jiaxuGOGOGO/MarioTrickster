using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地双人输入管理器 - MVP核心脚本
/// 参考: InputSystem_Warriors + local-multiplayer-unity
/// 功能: 管理Player1(Mario)和Player2(Trickster)的输入，分发给各自的控制器
/// 
/// 输入方案（键盘双人）:
///   Player1 (Mario):     WASD移动, Space跳跃
///   Player2 (Trickster): 方向键移动, 右Ctrl跳跃, 右Shift伪装, Enter切换形态, 右Alt操控道具
/// 
/// 也支持手柄: 第一个手柄→Mario, 第二个手柄→Trickster
/// 
/// 使用方式: 挂载到场景中的空GameObject上，在Inspector中拖入Mario和Trickster引用
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("=== 玩家引用 ===")]
    [SerializeField] private MarioController marioController;
    [SerializeField] private TricksterController tricksterController;

    [Header("=== 调试选项 ===")]
    [SerializeField] private bool showDebugInput = false;

    // Player1 (Mario) 输入状态
    private Vector2 p1MoveInput;
    private bool p1JumpPressed;
    private bool p1JumpHeld;

    // Player2 (Trickster) 输入状态
    private Vector2 p2MoveInput;
    private bool p2JumpPressed;
    private bool p2JumpHeld;
    private bool p2DisguisePressed;
    private bool p2SwitchDisguisePressed;
    private float p2SwitchDirection;
    private bool p2AbilityPressed; // 操控道具

    // 手柄支持
    private Gamepad gamepad1;
    private Gamepad gamepad2;

    private void Update()
    {
        // 检测手柄
        DetectGamepads();

        // 读取输入
        ReadPlayer1Input();
        ReadPlayer2Input();

        // 分发输入到控制器
        DispatchPlayer1Input();
        DispatchPlayer2Input();

        // 重置单帧输入
        ResetSingleFrameInputs();
    }

    #region 手柄检测

    private void DetectGamepads()
    {
        var gamepads = Gamepad.all;
        gamepad1 = gamepads.Count > 0 ? gamepads[0] : null;
        gamepad2 = gamepads.Count > 1 ? gamepads[1] : null;
    }

    #endregion

    #region Player1 (Mario) 输入 - WASD + Space

    private void ReadPlayer1Input()
    {
        // 键盘输入
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) && gamepad1 == null)
            horizontal += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) && gamepad1 == null)
            horizontal -= 1f;
        if (Input.GetKey(KeyCode.W))
            vertical += 1f;
        if (Input.GetKey(KeyCode.S))
            vertical -= 1f;

        // 手柄输入（覆盖键盘）
        if (gamepad1 != null)
        {
            Vector2 stick = gamepad1.leftStick.ReadValue();
            if (stick.magnitude > 0.1f)
            {
                horizontal = stick.x;
                vertical = stick.y;
            }
        }

        p1MoveInput = new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));

        // 跳跃
        if (Input.GetKeyDown(KeyCode.Space) || (gamepad1 != null && gamepad1.buttonSouth.wasPressedThisFrame))
        {
            p1JumpPressed = true;
        }

        p1JumpHeld = Input.GetKey(KeyCode.Space) || (gamepad1 != null && gamepad1.buttonSouth.isPressed);
    }

    #endregion

    #region Player2 (Trickster) 输入 - 方向键 + RCtrl/RShift/Enter/RAlt

    private void ReadPlayer2Input()
    {
        float horizontal = 0f;
        float vertical = 0f;

        // 键盘输入（方向键）
        // 注意：如果没有手柄，Player1用WASD，Player2用方向键
        // 如果有手柄，方向键不再用于Player1
        if (Input.GetKey(KeyCode.RightArrow) && gamepad1 != null)
            horizontal += 1f; // 有手柄时方向键给P2
        else if (Input.GetKey(KeyCode.RightArrow) && gamepad1 == null)
        {
            // 无手柄时方向键也给P2（P1只用WASD）
        }

        // 简化：Player2 始终使用 IJKL 或 方向键
        // 为避免与P1冲突，P2键盘使用：方向键移动
        horizontal = 0f;
        vertical = 0f;

        if (Input.GetKey(KeyCode.RightArrow))
            horizontal += 1f;
        if (Input.GetKey(KeyCode.LeftArrow))
            horizontal -= 1f;
        if (Input.GetKey(KeyCode.UpArrow))
            vertical += 1f;
        if (Input.GetKey(KeyCode.DownArrow))
            vertical -= 1f;

        // 手柄输入
        if (gamepad2 != null)
        {
            Vector2 stick = gamepad2.leftStick.ReadValue();
            if (stick.magnitude > 0.1f)
            {
                horizontal = stick.x;
                vertical = stick.y;
            }
        }

        p2MoveInput = new Vector2(Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));

        // 跳跃 - 右Ctrl 或 手柄南键(A/X)
        if (Input.GetKeyDown(KeyCode.RightControl) || (gamepad2 != null && gamepad2.buttonSouth.wasPressedThisFrame))
        {
            p2JumpPressed = true;
        }
        p2JumpHeld = Input.GetKey(KeyCode.RightControl) || (gamepad2 != null && gamepad2.buttonSouth.isPressed);

        // 伪装 - 右Shift 或 手柄西键(X/Square)
        if (Input.GetKeyDown(KeyCode.RightShift) || (gamepad2 != null && gamepad2.buttonWest.wasPressedThisFrame))
        {
            p2DisguisePressed = true;
        }

        // 切换伪装形态 - Enter/Backspace 或 手柄肩键
        if (Input.GetKeyDown(KeyCode.Return) || (gamepad2 != null && gamepad2.rightShoulder.wasPressedThisFrame))
        {
            p2SwitchDisguisePressed = true;
            p2SwitchDirection = 1f;
        }
        if (Input.GetKeyDown(KeyCode.Backspace) || (gamepad2 != null && gamepad2.leftShoulder.wasPressedThisFrame))
        {
            p2SwitchDisguisePressed = true;
            p2SwitchDirection = -1f;
        }

        // 操控道具 - 右Alt 或 手柄北键(Y/Triangle)
        if (Input.GetKeyDown(KeyCode.RightAlt) || (gamepad2 != null && gamepad2.buttonNorth.wasPressedThisFrame))
        {
            p2AbilityPressed = true;
        }
    }

    #endregion

    #region 输入分发

    private void DispatchPlayer1Input()
    {
        if (marioController == null) return;

        marioController.SetMoveInput(p1MoveInput);

        if (p1JumpPressed)
        {
            marioController.OnJumpPressed();
        }

        if (!p1JumpHeld)
        {
            marioController.OnJumpReleased();
        }
    }

    private void DispatchPlayer2Input()
    {
        if (tricksterController == null) return;

        tricksterController.SetMoveInput(p2MoveInput);

        if (p2JumpPressed)
        {
            tricksterController.OnJumpPressed();
        }

        if (!p2JumpHeld)
        {
            tricksterController.OnJumpReleased();
        }

        if (p2DisguisePressed)
        {
            tricksterController.OnDisguisePressed();
        }

        if (p2SwitchDisguisePressed)
        {
            tricksterController.OnSwitchDisguise(p2SwitchDirection);
        }

        // 操控道具
        if (p2AbilityPressed)
        {
            tricksterController.OnAbilityPressed();
        }
    }

    #endregion

    #region 工具方法

    private void ResetSingleFrameInputs()
    {
        p1JumpPressed = false;
        p2JumpPressed = false;
        p2DisguisePressed = false;
        p2SwitchDisguisePressed = false;
        p2AbilityPressed = false;
    }

    #endregion

    #region 公共方法（供GameManager等调用）

    /// <summary>禁用所有输入（如游戏暂停、胜负判定时）</summary>
    public void DisableAllInput()
    {
        enabled = false;
        if (marioController != null)
            marioController.SetMoveInput(Vector2.zero);
        if (tricksterController != null)
            tricksterController.SetMoveInput(Vector2.zero);
    }

    /// <summary>启用所有输入</summary>
    public void EnableAllInput()
    {
        enabled = true;
    }

    /// <summary>运行时动态设置Mario引用</summary>
    public void SetMarioController(MarioController controller)
    {
        marioController = controller;
    }

    /// <summary>运行时动态设置Trickster引用</summary>
    public void SetTricksterController(TricksterController controller)
    {
        tricksterController = controller;
    }

    #endregion

    #region 调试

    private void OnGUI()
    {
        if (!showDebugInput) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 220));
        GUILayout.Label($"P1 Move: {p1MoveInput}");
        GUILayout.Label($"P1 Jump Held: {p1JumpHeld}");
        GUILayout.Label($"P2 Move: {p2MoveInput}");
        GUILayout.Label($"P2 Jump Held: {p2JumpHeld}");
        GUILayout.Label($"P2 Ability: {p2AbilityPressed}");
        GUILayout.Label($"Gamepad1: {(gamepad1 != null ? gamepad1.displayName : "None")}");
        GUILayout.Label($"Gamepad2: {(gamepad2 != null ? gamepad2.displayName : "None")}");
        GUILayout.EndArea();
    }

    #endregion
}
