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
}
