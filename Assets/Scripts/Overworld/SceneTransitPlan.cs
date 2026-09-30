/// <summary>
/// S211：场景切换的节奏（纯逻辑，沙盒可测）。淡出 → 黑屏里后台加载 → 加载好了才激活 → 淡入。
/// 参考 GitHub 上的场景加载库（mygamedevtools/scene-loader 的 TransitionAsync、Advanced Scene Manager 的 Loading Screen）：
/// 画面先盖住再加载，玩家看不到卡顿和"跳一下"；同一时间只允许一次切换（重复按 E / Enter 不会叠加）。
/// 运行时 SceneTransit 每帧调用 Tick，并在 Tick 返回 true 时激活场景（AsyncOperation.allowSceneActivation = true）。
/// </summary>
public sealed class SceneTransitPlan
{
    public enum Phase { Idle, FadeOut, Loading, Activating, FadeIn }

    public float fadeOutSeconds = 0.25f;
    public float fadeInSeconds = 0.3f;
    /// <summary>黑屏至少停这么久：标题卡读得清，也不会一闪而过。</summary>
    public float minHoldSeconds = 0.35f;
    /// <summary>加载太久也不会永远卡在黑屏：到点就照样激活（Unity 会在激活时补完剩下的加载）。</summary>
    public float maxLoadSeconds = 10f;

    public Phase phase { get; private set; } = Phase.Idle;
    public float t { get; private set; }
    public string title { get; private set; } = "";
    public bool Busy => phase != Phase.Idle;

    public bool Begin(string cardTitle)
    {
        if (Busy) return false;
        phase = Phase.FadeOut; t = 0f; title = cardTitle ?? "";
        return true;
    }

    /// <summary>推进时间。返回 true = 现在该激活新场景（只会返回一次）。</summary>
    public bool Tick(float dt, bool loadReady)
    {
        if (dt < 0f) dt = 0f;
        t += dt;
        switch (phase)
        {
            case Phase.FadeOut:
                if (t >= fadeOutSeconds) { phase = Phase.Loading; t = 0f; }
                return false;
            case Phase.Loading:
                if ((loadReady && t >= minHoldSeconds) || t >= maxLoadSeconds) { phase = Phase.Activating; t = 0f; return true; }
                return false;
            case Phase.FadeIn:
                if (t >= fadeInSeconds) { phase = Phase.Idle; t = 0f; title = ""; }
                return false;
            default:
                return false;
        }
    }

    /// <summary>新场景已经激活（第一帧跑过了）→ 开始淡入。</summary>
    public void Activated()
    {
        if (phase == Phase.Activating || phase == Phase.Loading) { phase = Phase.FadeIn; t = 0f; }
    }

    /// <summary>出错时直接收起黑幕（不会永远黑屏）。</summary>
    public void Abort() { phase = Phase.Idle; t = 0f; title = ""; }

    /// <summary>黑幕不透明度 0..1。</summary>
    public float Alpha
    {
        get
        {
            switch (phase)
            {
                case Phase.FadeOut: return fadeOutSeconds <= 0f ? 1f : Clamp01(t / fadeOutSeconds);
                case Phase.Loading:
                case Phase.Activating: return 1f;
                case Phase.FadeIn: return fadeInSeconds <= 0f ? 0f : 1f - Clamp01(t / fadeInSeconds);
                default: return 0f;
            }
        }
    }

    /// <summary>加载瞬间完成时，一次切换最短多久（淡出 + 最短黑屏 + 淡入）。</summary>
    public float MinTotalSeconds => fadeOutSeconds + minHoldSeconds + fadeInSeconds;

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
