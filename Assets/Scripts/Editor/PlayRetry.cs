using UnityEditor;

/// <summary>
/// S238：编辑器里 F5 / 回合结束按 R = 停止再进 Play（场景完整复原，被炸掉 / 吃掉的东西都回来）。
/// 以前这件事由旧测试台的 LevelStudioPlaySession 顺带做；旧测试台删掉后单独留这一个小钩子。
/// 小镇里的房间有自己的平滑重开（GameManager.RestartOverride），优先于这里。
/// </summary>
[InitializeOnLoad]
public static class PlayRetry
{
    private const string Key = "MarioTrickster.PlayRetry";

    static PlayRetry()
    {
        GameManager.EditorRestartHandler = Retry;
        EditorApplication.playModeStateChanged += st =>
        {
            if (st != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode && !TestReportRunner.IsRunning) EditorApplication.isPlaying = true; };
        };
    }

    public static bool Retry()
    {
        if (!EditorApplication.isPlaying) return false;
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = false;
        return true;
    }
}
