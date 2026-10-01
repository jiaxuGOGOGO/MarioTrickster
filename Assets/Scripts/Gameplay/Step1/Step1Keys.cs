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
            case KeyCode.P: return kb.pKey.wasPressedThisFrame;
            case KeyCode.L: return kb.lKey.wasPressedThisFrame;
            case KeyCode.E: return kb.eKey.wasPressedThisFrame;
            case KeyCode.Q: return kb.qKey.wasPressedThisFrame; // S220 雷云
            case KeyCode.Return: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            // S217：以前漏了这些（新输入系统的机器上"按 R 再来一天 / 空格 / 方向键"没反应）
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.N: return kb.nKey.wasPressedThisFrame;
            case KeyCode.Y: return kb.yKey.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
            case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.A: return kb.aKey.wasPressedThisFrame;
            case KeyCode.D: return kb.dKey.wasPressedThisFrame;
            case KeyCode.W: return kb.wKey.wasPressedThisFrame;
            case KeyCode.S: return kb.sKey.wasPressedThisFrame;
            case KeyCode.F8: return kb.f8Key.wasPressedThisFrame;
            case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
            case KeyCode.F9: return kb.f9Key.wasPressedThisFrame;       // S217：F8 = 记一条试玩反馈（截图 + 当时情况）
            case KeyCode.Minus: return kb.minusKey.wasPressedThisFrame; // S217：小镇镜头缩小 / 放大
            case KeyCode.Equals: return kb.equalsKey.wasPressedThisFrame;
            default: return false;
        }
    }

    /// <summary>S217：这一帧按了任意键（两套输入系统都读）。说明面板"按任意键关闭"用。</summary>
    public static bool AnyDown()
    {
        bool legacy = false;
        try { legacy = Input.anyKeyDown; } catch (System.InvalidOperationException) { }
        if (legacy) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.anyKey.wasPressedThisFrame;
    }

    /// <summary>S210：按住（大地图走路用方向键 / WASD，两套输入系统都读）。</summary>
    public static bool Held(KeyCode key)
    {
        bool legacy = false;
        try { legacy = Input.GetKey(key); } catch (System.InvalidOperationException) { }
        if (legacy) return true;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        switch (key)
        {
            case KeyCode.LeftArrow: return kb.leftArrowKey.isPressed;
            case KeyCode.RightArrow: return kb.rightArrowKey.isPressed;
            case KeyCode.UpArrow: return kb.upArrowKey.isPressed;
            case KeyCode.DownArrow: return kb.downArrowKey.isPressed;
            case KeyCode.A: return kb.aKey.isPressed;
            case KeyCode.D: return kb.dKey.isPressed;
            case KeyCode.W: return kb.wKey.isPressed;
            case KeyCode.S: return kb.sKey.isPressed;
            case KeyCode.Tab: return kb.tabKey.isPressed; // S212：按住看去门的路线
            case KeyCode.Space: return kb.spaceKey.isPressed; // S213：大地图按住快进
            case KeyCode.Minus: return kb.minusKey.isPressed; // S217
            case KeyCode.Equals: return kb.equalsKey.isPressed;
            default: return false;
        }
    }
}
