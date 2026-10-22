using UnityEngine;

/// <summary>S196/S197：屏幕底部的一行提示（技能失败原因、听见响声等）。H6：没有声音也看得见。</summary>
public class Step1Hint : MonoBehaviour
{
    private static string text = "";
    private static float until;
    /// <summary>S242：每条提示都通知一声（黑匣子记成面包屑："按了 G → 诱饵用完了"这种"按了没反应"的原因 AI 一眼看到）。</summary>
    public static event System.Action<string> Shown;
    public static void Show(string msg, float seconds = 1.6f) { text = msg; until = Time.unscaledTime + seconds; Shown?.Invoke(msg); }

    private void OnGUI()
    {
        if (Time.unscaledTime > until || string.IsNullOrEmpty(text) || Step1HandsOffCheck.IsRunning) return;
        float w = Step1Gui.Begin();
        var r = new Rect(w * 0.5f - 380f, Step1Gui.VirtualHeight - 170f, 760f, 60f);
        Step1Gui.Panel(r, 0.65f);
        GUI.Label(r, text, Step1Gui.Text(26, TextAnchor.MiddleCenter));
    }
}
