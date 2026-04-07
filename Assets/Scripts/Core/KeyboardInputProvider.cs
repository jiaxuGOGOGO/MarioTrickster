using UnityEngine;
using UnityEngine.InputSystem;

// ═══════════════════════════════════════════════════════════════════
// KeyboardInputProvider — 真实键盘+手柄输入实现（S49: 全自动测试基建）
//
// [AI防坑警告] 此类是从 InputManager.ReadP1() / ReadP2() 中提取的原始键盘读取逻辑。
// 所有 Input.GetKey / Input.GetKeyDown 调用必须且仅在此类中出现。
// 修改按键映射时只需改此文件，不要在 InputManager 中直接调用 Input API。
//
// 键盘方案（与原 InputManager 完全一致）：
//   P1 (Mario)    : WASD 移动 | Space 跳跃 | Q 扫描 | S 交互/下落
//   P2 (Trickster): 方向键 移动 | 上/右Ctrl/Keypad0 跳跃 | P 伪装 | O/I 切换 | L 操控
//   全局          : ESC 暂停 | F5 重启 | F9 无冷却 | R 重开 | N 下一回合
//
// 手柄方案：
//   第一个手柄 → Mario，第二个手柄 → Trickster
//   左摇杆移动 | 南键(A/×) 跳跃 | 东键(B/○) 扫描
//   右肩键 下一形态 | 左肩键 上一形态 | 北键(Y/△) 操控道具
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 真实键盘+手柄输入提供者。
/// 
/// 从 InputManager 的 ReadP1()/ReadP2() 中提取的所有 Input.GetKey/GetKeyDown 调用，
/// 封装为 IInputProvider 接口实现。InputManager 通过此接口读取输入，
/// 不再直接调用 Unity Input API。
/// 
/// 手柄检测在每帧 Update 前由 InputManager 调用 UpdateGamepads() 完成。
/// </summary>
public class KeyboardInputProvider : IInputProvider
{
    // ── 手柄引用（由 InputManager 每帧更新）────────────────────
    private Gamepad _gamepad1;
    private Gamepad _gamepad2;

    /// <summary>
    /// 每帧由 InputManager 调用，更新手柄引用。
    /// 必须在读取输入之前调用。
    /// </summary>
    public void UpdateGamepads()
    {
        var pads = Gamepad.all;
        _gamepad1 = pads.Count > 0 ? pads[0] : null;
        _gamepad2 = pads.Count > 1 ? pads[1] : null;
    }

    // ═══════════════════════════════════════════════════════════
    // P1 (Mario) 输入
    // ═══════════════════════════════════════════════════════════

    public float GetP1Horizontal()
    {
        float h = 0f;
        if (Input.GetKey(KeyCode.D)) h += 1f;
        if (Input.GetKey(KeyCode.A)) h -= 1f;

        if (_gamepad1 != null)
        {
            Vector2 s = _gamepad1.leftStick.ReadValue();
            if (s.magnitude > 0.1f) h = s.x;
        }

        return Mathf.Clamp(h, -1f, 1f);
    }

    public float GetP1Vertical()
    {
        float v = 0f;
        if (Input.GetKey(KeyCode.W)) v += 1f;
        if (Input.GetKey(KeyCode.S)) v -= 1f;

        if (_gamepad1 != null)
        {
            Vector2 s = _gamepad1.leftStick.ReadValue();
            if (s.magnitude > 0.1f) v = s.y;
        }

        return Mathf.Clamp(v, -1f, 1f);
    }

    public bool GetP1JumpHeld()
    {
        return Input.GetKey(KeyCode.Space)
            || (_gamepad1 != null && _gamepad1.buttonSouth.isPressed);
    }

    public bool GetP1JumpDown()
    {
        return Input.GetKeyDown(KeyCode.Space)
            || (_gamepad1 != null && _gamepad1.buttonSouth.wasPressedThisFrame);
    }

    public bool GetP1SHeld()
    {
        return Input.GetKey(KeyCode.S)
            || (_gamepad1 != null && _gamepad1.dpad.down.isPressed);
    }

    public bool GetP1SDown()
    {
        return Input.GetKeyDown(KeyCode.S)
            || (_gamepad1 != null && _gamepad1.dpad.down.wasPressedThisFrame);
    }

    public bool GetP1ScanDown()
    {
        return Input.GetKeyDown(KeyCode.Q)
            || (_gamepad1 != null && _gamepad1.buttonEast.wasPressedThisFrame);
    }

    // ═══════════════════════════════════════════════════════════
    // P2 (Trickster) 输入
    // ═══════════════════════════════════════════════════════════

    public float GetP2Horizontal()
    {
        float h = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.LeftArrow))  h -= 1f;

        if (_gamepad2 != null)
        {
            Vector2 s = _gamepad2.leftStick.ReadValue();
            if (s.magnitude > 0.1f) h = s.x;
        }

        return Mathf.Clamp(h, -1f, 1f);
    }

    public float GetP2Vertical()
    {
        float v = 0f;
        if (Input.GetKey(KeyCode.UpArrow))    v += 1f;
        if (Input.GetKey(KeyCode.DownArrow))  v -= 1f;

        if (_gamepad2 != null)
        {
            Vector2 s = _gamepad2.leftStick.ReadValue();
            if (s.magnitude > 0.1f) v = s.y;
        }

        return Mathf.Clamp(v, -1f, 1f);
    }

    public bool GetP2JumpHeld()
    {
        return Input.GetKey(KeyCode.UpArrow)
            || Input.GetKey(KeyCode.RightControl)
            || Input.GetKey(KeyCode.Keypad0)
            || (_gamepad2 != null && _gamepad2.buttonSouth.isPressed);
    }

    public bool GetP2JumpDown()
    {
        return Input.GetKeyDown(KeyCode.UpArrow)
            || Input.GetKeyDown(KeyCode.RightControl)
            || Input.GetKeyDown(KeyCode.Keypad0)
            || (_gamepad2 != null && _gamepad2.buttonSouth.wasPressedThisFrame);
    }

    public bool GetP2DirectionDown()
    {
        return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow);
    }

    public bool GetP2DisguiseDown()
    {
        return Input.GetKeyDown(KeyCode.P)
            || (_gamepad2 != null && _gamepad2.buttonWest.wasPressedThisFrame);
    }

    public float GetP2SwitchDirection()
    {
        if (Input.GetKeyDown(KeyCode.O)
            || (_gamepad2 != null && _gamepad2.rightShoulder.wasPressedThisFrame))
            return 1f;

        if (Input.GetKeyDown(KeyCode.I)
            || (_gamepad2 != null && _gamepad2.leftShoulder.wasPressedThisFrame))
            return -1f;

        return 0f;
    }

    public bool GetP2AbilityDown()
    {
        return Input.GetKeyDown(KeyCode.L)
            || (_gamepad2 != null && _gamepad2.buttonNorth.wasPressedThisFrame);
    }

    // ═══════════════════════════════════════════════════════════
    // 全局输入
    // ═══════════════════════════════════════════════════════════

    public bool GetPauseDown()             => Input.GetKeyDown(KeyCode.Escape);
    public bool GetRestartDown()           => Input.GetKeyDown(KeyCode.F5);
    public bool GetNoCooldownToggleDown()  => Input.GetKeyDown(KeyCode.F9);
    public bool GetRestartRoundDown()      => Input.GetKeyDown(KeyCode.R);
    public bool GetNextRoundDown()         => Input.GetKeyDown(KeyCode.N);
}
