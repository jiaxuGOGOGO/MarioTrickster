using UnityEngine;

/// <summary>
/// S193：顿帧执行器。用 unscaledTime 计时；暂停 / 帮助页 / H10 自动检查期间不介入（它们自己管 timeScale）。
/// 只修改 Time.timeScale，时间一到恢复为进入前的值；多次命中取较长的那次，不叠加。
/// </summary>
public class Step1Hitstop : MonoBehaviour
{
    private float until = -1f;
    private float restoreScale = 1f;
    private float appliedScale = 1f;
    private bool active;
    public static Step1Hitstop Instance { get; private set; }
    public bool Active => active;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; if (active) Time.timeScale = restoreScale; }

    public void Request(float seconds, float slowScale)
    {
        if (seconds <= 0f || Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Time.timeScale <= 0f && !active) return;
        if (!active) { restoreScale = Time.timeScale; active = true; }
        until = Mathf.Max(until, Time.unscaledTime + seconds);
        appliedScale = Mathf.Clamp(slowScale, 0f, 1f);
        Time.timeScale = appliedScale;
    }

    private void Update()
    {
        if (!active) return;
        if (Step1Screen.HelpOpen || Step1HandsOffCheck.IsRunning) { active = false; return; } // 别人接管了 timeScale
        if (!Mathf.Approximately(Time.timeScale, appliedScale)) { active = false; return; }    // 顿帧期间按了暂停：不去"恢复"成未暂停
        if (Time.unscaledTime >= until) { active = false; Time.timeScale = restoreScale; }
    }
}
