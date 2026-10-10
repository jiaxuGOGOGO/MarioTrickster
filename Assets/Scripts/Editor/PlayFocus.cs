using UnityEditor;

/// <summary>
/// S217：每次进入 Play 自动把 Game 窗口切到前面并获得键盘焦点。
/// 用户反馈"运行的时候画面固定不动、控制不了马里奥/捣蛋者"——常见原因之一是键盘还在工坊窗口/Scene 窗口里，
/// 游戏收不到按键（Unity 只有 Game 窗口有焦点时才把键盘给游戏）。游戏里也会提示"先点一下游戏画面"。
/// </summary>
[InitializeOnLoad]
public static class PlayFocus
{
    static PlayFocus() { EditorApplication.playModeStateChanged += OnPlay; }

    private static void OnPlay(PlayModeStateChange st)
    {
        if (st != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExecuteMenuItem("Window/General/Game"); };
    }
}
