using UnityEngine;

/// <summary>
/// S197：第 1 步技能按键读取。项目开的是"新旧输入系统都启用"（activeInputHandler=2），
/// 但有的机器上旧 Input 读不到新加的键（用户反馈"按 B 没反应"）。这里两套都读，任一按下即算。
/// </summary>
public static class Step1Keys
{
    /// <summary>S200：Shift 按住（两套输入系统都读）。</summary>
    public static bool Shift()
    {
        bool legacy = false;
        try { legacy = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } catch (System.InvalidOperationException) { }
        if (legacy) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.shiftKey.isPressed;
    }

    public static bool Down(KeyCode key)
    {
        bool legacy = false;
        try { legacy = Input.GetKeyDown(key); } catch (System.InvalidOperationException) { }
        if (legacy) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.B: return kb.bKey.wasPressedThisFrame;
            case KeyCode.G: return kb.gKey.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey.wasPressedThisFrame;
            case KeyCode.J: return kb.jKey.wasPressedThisFrame;
            case KeyCode.K: return kb.kKey.wasPressedThisFrame;
            case KeyCode.T: return kb.tKey.wasPressedThisFrame;
            case KeyCode.U: return kb.uKey.wasPressedThisFrame;
            case KeyCode.X: return kb.xKey.wasPressedThisFrame;
            case KeyCode.Z: return kb.zKey.wasPressedThisFrame;
            case KeyCode.V: return kb.vKey.wasPressedThisFrame;
            case KeyCode.C: return kb.cKey.wasPressedThisFrame;
            case KeyCode.H: return kb.hKey.wasPressedThisFrame;
            case KeyCode.M: return kb.mKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
            default: return false;
        }
    }
}
