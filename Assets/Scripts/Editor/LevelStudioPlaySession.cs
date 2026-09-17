using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Owns the editor play/edit boundary, independent of whether the Studio window is open.
/// SessionState survives domain reload. Never edits the scene or overrides normal builds.
/// </summary>
[InitializeOnLoad]
public static class LevelStudioPlaySession
{
    private const string Prefix = "MarioTrickster.Studio.Play.";
    private static GameManager observed;
    private static MarioController runner;
    private static bool attemptOpen;

    [Serializable]
    public sealed class Report
    {
        public string identity = "";
        public int role;
        public int attempts;
        public int clears;
        public int failures;
        public int unfinished;
        public float bestSeconds;
        public float lastSeconds;
        public Vector3 lastPosition;
        public bool hasResult;
        public bool lastFailed;
        public string outcome = "";
        public string reason = "";
    }

    public static bool Active => SessionState.GetBool(Prefix + "active", false);
    public static Report Latest => JsonUtility.FromJson<Report>(SessionState.GetString(Prefix + "report", "{}")) ?? new Report();

    static LevelStudioPlaySession()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Observe;
        GameManager.EditorRestartHandler = Retry;
    }

    public static bool CanStart()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        if (EditorSettings.enterPlayModeOptionsEnabled &&
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
        {
            EditorUtility.DisplayDialog("试玩需要场景复位", "请在 Project Settings > Editor > Enter Play Mode Settings 启用 Reload Scene。这样重试才能恢复已销毁的敌人、金币与机关。工具不会擅自改你的项目设置。", "知道了");
            return false;
        }
        return true;
    }

    public static void Begin(string identity, int role)
    {
        if (!CanStart()) return;
        Report report = Latest;
        if (report.identity != identity || report.role != role)
            Save(new Report { identity = identity, role = role });
        SessionState.SetInt(Prefix + "role", role);
        SessionState.SetBool(Prefix + "active", true);
        SessionState.SetBool(Prefix + "retry", false);
        LevelBrushTool.Deactivate();
        EditorApplication.isPlaying = true;
    }

    public static bool Retry()
    {
        if (!EditorApplication.isPlaying) return false;
        // [AI防坑警告] 完整 Stop/Play 恢复未保存场景，避免 buildIndex=-1，
        // 也避免不完整的 ResetRound 遗漏被 Destroy 的对象。不要强行自动连局。
        SessionState.SetBool(Prefix + "retry", true);
        EditorApplication.isPlaying = false;
        return true;
    }

    public static void ReturnToEdit()
    {
        SessionState.SetBool(Prefix + "retry", false);
        EditorApplication.isPlaying = false;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Observe();
            if (Active)
                EditorApplication.delayCall += () =>
                {
                    if (Active && EditorApplication.isPlaying)
                        EditorApplication.ExecuteMenuItem("Window/General/Game");
                };
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (observed != null && attemptOpen) RecordResult("未完成", false, false);
            Detach();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            bool retry = SessionState.GetBool(Prefix + "retry", false);
            SessionState.SetBool(Prefix + "retry", false);
            if (retry)
            {
                EditorApplication.delayCall += () =>
                {
                    if (CanStart()) EditorApplication.isPlaying = true;
                    else SessionState.SetBool(Prefix + "active", false);
                };
            }
            else SessionState.SetBool(Prefix + "active", false);
        }
    }

    private static void Observe()
    {
        if (!Active || !EditorApplication.isPlaying || observed != null) return;
        GameManager manager = GameManager.Instance;
        if (manager == null) return;
        observed = manager;
        runner = UnityEngine.Object.FindObjectOfType<MarioController>(); // once per play session
        var input = UnityEngine.Object.FindObjectOfType<InputManager>();
        if (input != null)
        {
            int role = SessionState.GetInt(Prefix + "role", 0);
            var provider = input.GetCurrentProvider() as HybridInputProvider ?? new HybridInputProvider();
            provider.MarioIsAI = role == 1;
            provider.TricksterIsAI = role == 0;
            provider.MarioIsTAS = false;
            input.SetInputProvider(provider);
        }
        observed.OnRoundStart += StartAttempt;
        observed.OnGameOver += FinishAttempt;
        if (observed.CurrentState != GameState.WaitingToStart) StartAttempt();
    }

    private static void StartAttempt()
    {
        if (attemptOpen) RecordResult("未完成", false, false);
        Report report = Latest;
        report.attempts++;
        Save(report);
        attemptOpen = true;
    }

    private static void FinishAttempt(string winner) => RecordResult(winner == "Mario" ? "闯关成功" : "闯关受阻", winner == "Mario", true);

    private static void RecordResult(string outcome, bool cleared, bool completed)
    {
        if (!attemptOpen || observed == null) return;
        Report report = Latest;
        report.hasResult = true;
        report.outcome = outcome;
        report.lastFailed = completed && !cleared;
        report.lastSeconds = observed.RoundElapsed;
        report.lastPosition = completed ? observed.LastRoundPosition : (runner != null ? runner.transform.position : Vector3.zero);
        report.reason = completed ? observed.LastRoundReason : "本次主动结束；不计为失败。";
        if (completed)
        {
            if (cleared)
            {
                report.clears++;
                if (report.bestSeconds <= 0f || report.lastSeconds < report.bestSeconds) report.bestSeconds = report.lastSeconds;
            }
            else report.failures++;
        }
        else report.unfinished++;
        Save(report);
        attemptOpen = false;
    }

    private static void Save(Report report) => SessionState.SetString(Prefix + "report", JsonUtility.ToJson(report));

    private static void Detach()
    {
        if (observed != null)
        {
            observed.OnRoundStart -= StartAttempt;
            observed.OnGameOver -= FinishAttempt;
        }
        observed = null;
        runner = null;
        attemptOpen = false;
    }
}
