/// <summary>
/// S211：场景切换的节奏（纯逻辑，沙盒可测）。淡出 → 黑屏里后台加载 → 加载好了才激活 → 淡入。
/// 参考 GitHub 上的场景加载库（mygamedevtools/scene-loader 的 TransitionAsync、Advanced Scene Manager 的 Loading Screen）：
/// 画面先盖住再加载，玩家看不到卡顿和"跳一下"；同一时间只允许一次切换（重复按 E / Enter 不会叠加）。
/// 运行时 SceneTransit 每帧调用 Tick，并在 Tick 返回 true 时激活场景（AsyncOperation.allowSceneActivation = true）。
/// S212：① 缓入缓出（smoothstep，Reddit r/Unity3D 共识"S 曲线比直线顺"）② 单帧时间封顶 1/30 秒——新场景激活那一帧
/// Unity 会卡一下（几百毫秒），不封顶的话淡入第一帧就跳掉一大截（Meta 开发者博客：激活卡顿随物体数量增长）
/// ③ 圆形收缩/展开（iris wipe，经典马里奥式转场）：从门口收拢、在你身上展开，眼睛知道"从哪来到哪去"。
/// </summary>
public sealed class SceneTransitPlan
{
    public enum Phase { Idle, FadeOut, Loading, Activating, FadeIn }
    public enum Style { Fade, Iris }

    public float fadeOutSeconds = 0.25f;
    public float fadeInSeconds = 0.35f;
    /// <summary>黑屏至少停这么久：标题卡读得清，也不会一闪而过。</summary>
    public float minHoldSeconds = 0.35f;
    /// <summary>加载太久也不会永远卡在黑屏：到点就照样激活（Unity 会在激活时补完剩下的加载）。</summary>
    public float maxLoadSeconds = 10f;
    /// <summary>S212：单帧最多推进这么久（激活卡顿那一帧不会让动画跳一截）。</summary>
    public const float MaxStep = 1f / 30f;

    public Phase phase { get; private set; } = Phase.Idle;
    public float t { get; private set; }
    public string title { get; private set; } = "";
    public Style style = Style.Iris;
    /// <summary>S212：圆心（屏幕比例 0..1，左下为 0,0）。收拢时 = 门口；展开时 = 新场景里的你。</summary>
    public float focusX = 0.5f, focusY = 0.5f;
    public bool Busy => phase != Phase.Idle;

    public bool Begin(string cardTitle)
    {
        if (Busy) return false;
        phase = Phase.FadeOut; t = 0f; title = cardTitle ?? "";
        return true;
    }

    /// <summary>S212：运行时每帧先过一遍这个再 Tick。动画阶段封顶 MaxStep；等加载阶段按真实时间（maxLoad 才准）。</summary>
    public float Step(float unscaledDt) => phase == Phase.Loading ? unscaledDt : System.Math.Min(unscaledDt, MaxStep);

    public void SetFocus(float x01, float y01) { focusX = Clamp01(x01); focusY = Clamp01(y01); }

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

    /// <summary>画面被盖住的程度 0..1（已缓入缓出）。</summary>
    public float Alpha
    {
        get
        {
            switch (phase)
            {
                case Phase.FadeOut: return fadeOutSeconds <= 0f ? 1f : Ease(t / fadeOutSeconds);
                case Phase.Loading:
                case Phase.Activating: return 1f;
                case Phase.FadeIn: return fadeInSeconds <= 0f ? 0f : 1f - Ease(t / fadeInSeconds);
                default: return 0f;
            }
        }
    }

    /// <summary>S212：圆形转场的洞有多大（占屏幕对角线从圆心到最远角的比例，1 = 整屏都看得见，0 = 全黑）。</summary>
    public float IrisOpen => 1f - Alpha;

    /// <summary>圆心到最远屏幕角的距离（像素）：洞开到这么大时整个屏幕都露出来。</summary>
    public static float FarCorner(float fx01, float fy01, float w, float h)
    {
        float dx = System.Math.Max(fx01, 1f - fx01) * w, dy = System.Math.Max(fy01, 1f - fy01) * h;
        return (float)System.Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>加载瞬间完成时，一次切换最短多久（淡出 + 最短黑屏 + 淡入）。</summary>
    public float MinTotalSeconds => fadeOutSeconds + minHoldSeconds + fadeInSeconds;

    /// <summary>smoothstep：开头和结尾都慢，中间快（没有"咔"一下起步/停下）。</summary>
    public static float Ease(float x) { x = Clamp01(x); return x * x * (3f - 2f * x); }

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
}
