using System;

/// <summary>
/// S193：新机关的全局事件（连招 / 试玩记录订阅）。事件只说明"马里奥被弹起 / 掉下一层"，不带任何捣蛋者信息（H4）。
/// </summary>
public static class SpringPadEvents
{
    public static event Action Launched;
    public static void RaiseLaunched() => Launched?.Invoke();
}

public static class BananaPeelEvents
{
    public static event Action Slipped;
    public static void RaiseSlipped() => Slipped?.Invoke();
}

public static class CrackFloorEvents
{
    public static event Action MarioFell;
    public static void RaiseMarioFell() => MarioFell?.Invoke();
}
