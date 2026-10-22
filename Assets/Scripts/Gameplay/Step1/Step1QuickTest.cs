using UnityEngine;

/// <summary>
/// S217：快速测试模式（测试中心里一键开关，记在 PlayerPrefs，编辑器和游戏都读得到）。
/// 开着时：进房间/小镇不弹玩法说明、房间结算不弹 5 道问卷（直接 R / N）——反复测试不被打断。反馈改用 F8 随手记（Step1Feedback）。
/// 只改"界面打不打断你"，不改任何玩法、数值和马里奥（H4/H10 不受影响）。
/// </summary>
public static class Step1QuickTest
{
    public const string Key = "MarioTrickster.QuickTest";
    public static bool On
    {
        get { return PlayerPrefs.GetInt(Key, 0) == 1; }
        set { PlayerPrefs.SetInt(Key, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>S236：F9 测试——技能用不完、没冷却（炸弹 / 缩小 / 诱饵 / 挑衅；机关和伪装的冷却由 GameManager 的 F9 清掉）。
    /// 只在这次 Play 里有效（不存盘），开过的局 CSV 记 mode=f9，不算进第 1 步出口。马里奥不读它（H4）。</summary>
    public static bool NoLimits;
    /// <summary>这一局开过 F9（中途关掉也算）。</summary>
    public static bool UsedThisRound;
    public static string NoLimitsBadge => "<color=#7CFC7C><b>F9 测试：技能无限</b></color>（再按 F9 关）";
}
