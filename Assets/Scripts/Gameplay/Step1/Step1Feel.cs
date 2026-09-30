using UnityEngine;

/// <summary>
/// S216：手感 / 视觉纯逻辑（沙盒可验证）。所有"被弹飞、被打中、预警、特效"的曲线都在这里，控制器与特效只接线。
///
/// 为什么有这个文件（审计发现）：
///   以前角色处于"受击硬直 stun"时，**只有往下掉才有重力**（vy ≤ 0 才加速下落）。
///   结果：弹簧板 16 格/秒 → 0.6 秒内匀速往上飘 9.6 格（设计是 5 格，房间头顶只空 5–8 格 → 撞天花板）；
///   炸弹把人横着推 8 格、往上飘 3.6 格；人肉炮飞 17 格。像在月球上，看着"生硬、不自然"。
///
/// 行业做法（只借规则）：
///   - Smash Bros 击飞：受击后仍受重力，轨迹是**抛物线**且可预判；受击硬直期间有专门的下落速度
///     （https://www.ssbwiki.com/Knockback ）；
///   - 格斗游戏 hitstun / pushback：击退距离与硬直一起决定"读得懂"（https://critpoints.net/2016/08/14/stunning-detail/ ）；
///   - Vlambeer《The art of screenshake》/ Juice it or lose it：命中要有冲击点、粒子、震屏，但**因果要看得见**
///     （https://www.youtube.com/watch?v=AJdEqssNZ-U ，https://www.youtube.com/watch?v=Fy0aCDmgnxg ）；
///   - Squirrel Eiserloh《Juicing Your Cameras With Math》：震屏用**平滑噪声 + 强度平方衰减**，不用每帧随机
///     （https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing ）；
///   - 迪士尼动画原则"预备动作 anticipation"：预警越接近发动越急。
/// </summary>
public static class Step1Feel
{
    // ── 1. 被弹飞 / 被打飞的抛物线 ─────────────────────────────

    /// <summary>
    /// 硬直期间一步物理（FixedUpdate 用）。**全程有重力**（launchGravity，比平时跳跃的重力轻一点 → 飞得"有滞空感"但仍是抛物线）；
    /// 空中水平方向轻微阻力；落地后摩擦刹停（slide = true 时不刹：香蕉皮就是要滑）。
    /// </summary>
    public static Vector2 StunStep(Vector2 v, bool grounded, float dt, float gravity, float maxFall, float airDrag, float groundFriction, bool slide)
    {
        if (!grounded || v.y > 0f) v.y = Mathf.MoveTowards(v.y, -Mathf.Abs(maxFall), Mathf.Max(0f, gravity) * dt);
        float drag = grounded && v.y <= 0f ? (slide ? 0f : groundFriction) : airDrag;
        v.x = Mathf.MoveTowards(v.x, 0f, Mathf.Max(0f, drag) * dt);
        return v;
    }

    public struct Arc { public float apex, range, airTime, slideAfter; }

    /// <summary>纯模拟：从平地以速度 v 被弹出，最高多少格、落地时水平飞了多远、空中几秒、落地后还滑多远（不含 slide）。</summary>
    public static Arc Simulate(Vector2 v, float gravity, float maxFall, float airDrag, float groundFriction, float dt = 0.02f)
    {
        var a = new Arc(); float x = 0f, y = 0f, t = 0f;
        bool left = v.y > 0f;
        if (left)
            while (t < 5f)
            {
                v = StunStep(v, false, dt, gravity, maxFall, airDrag, groundFriction, false);
                x += v.x * dt; y += v.y * dt; t += dt;
                if (y > a.apex) a.apex = y;
                if (y <= 0f) break;
            }
        a.range = Mathf.Abs(x); a.airTime = t;
        float gf = Mathf.Max(0.01f, groundFriction);
        a.slideAfter = v.x * v.x / (2f * gf);
        return a;
    }

    /// <summary>最高点（格）：v²/2g。</summary>
    public static float Apex(float vy, float gravity) => gravity <= 0f || vy <= 0f ? 0f : vy * vy / (2f * gravity);

    /// <summary>从平地弹起再落回原高度的空中时间（秒）。</summary>
    public static float AirTime(float vy, float gravity) => gravity <= 0f || vy <= 0f ? 0f : 2f * vy / gravity;

    /// <summary>
    /// 硬直什么时候结束：计时到了**并且**已经落地（被弹飞的人不会在半空突然"恢复控制"换成另一套重力）。
    /// 安全上限：计时结束后最多再等 maxExtra 秒（H9：任何控制都有结束）。
    /// </summary>
    public static bool StunOver(float timerLeft, bool untilGround, bool grounded, float maxExtra)
    {
        if (timerLeft > 0f) return false;
        if (!untilGround || grounded) return true;
        return timerLeft <= -Mathf.Max(0f, maxExtra);
    }

    // ── 2. 预警（anticipation）──────────────────────────────

    /// <summary>预警闪烁频率：开始时 baseRate，快发动时 ×2.5（越来越急，玩家看得出"马上要来了"）。progress 0→1。</summary>
    public static float TelegraphRate(float baseRate, float progress) => baseRate * Mathf.Lerp(1f, 2.5f, Mathf.Clamp01(progress));

    /// <summary>预警抖动：平滑（两个不同频率的正弦）而不是每帧随机跳；幅度随进度从 40% 涨到 100%。只动画面，不动碰撞体。</summary>
    public static Vector2 TelegraphShake(float time, float progress, float intensity)
    {
        float k = intensity * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(progress));
        return new Vector2((float)System.Math.Sin(time * 53f) * k, (float)System.Math.Sin(time * 41f + 1.3f) * k * 0.6f);
    }

    // ── 3. 弹簧板自身的"压下 → 弹出 → 回弹"────────────────────

    /// <summary>弹簧板画面的竖直缩放：发动瞬间压到 0.55，0.08 秒弹到 1.35，之后阻尼回弹到 1（Secrets of Springs 式简谐衰减）。t = 发动后秒数。</summary>
    public static float SpringPadScaleY(float t)
    {
        if (t <= 0f) return 0.55f;
        if (t < 0.08f) return Mathf.Lerp(0.55f, 1.35f, t / 0.08f);
        float u = t - 0.08f;
        if (u > 0.6f) return 1f;
        return 1f + 0.35f * (float)System.Math.Exp(-7f * u) * (float)System.Math.Cos(u * 26f);
    }

    // ── 4. 特效曲线 ─────────────────────────────────────────

    /// <summary>冲击环：t 0→1，大小 easeOutCubic 从 startScale 到 1，透明度 (1-t)²。</summary>
    public static void Ring(float t, float startScale, out float scale, out float alpha)
    {
        t = Mathf.Clamp01(t);
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        scale = Mathf.Lerp(startScale, 1f, e);
        alpha = (1f - t) * (1f - t);
    }

    /// <summary>缓入缓出（绳套把人吊起来、铁笼落下用）。</summary>
    public static float SmoothStep01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    /// <summary>铁笼下落：加速落下（t²），time 秒走完。返回 0（在头顶）→1（落地）。</summary>
    public static float DropProgress(float t, float time) => time <= 0f ? 1f : Mathf.Clamp01(t / time) * Mathf.Clamp01(t / time);

    // ── 5. 受伤 ─────────────────────────────────────────────

    /// <summary>受伤一瞬间的红白闪（0→1 强度）：前 flash 秒满强度然后淡出；之后交给无敌闪烁。</summary>
    public static float HurtTint(float sinceHit, float flash)
    {
        if (sinceHit < 0f || flash <= 0f) return 0f;
        if (sinceHit < flash * 0.5f) return 1f;
        return Mathf.Clamp01(1f - (sinceHit - flash * 0.5f) / (flash * 0.5f));
    }

    // ── 6. 震屏 ─────────────────────────────────────────────

    /// <summary>
    /// 平滑震屏偏移（Eiserloh）：强度 = amp × 剩余比例²（尾巴收得干净），方向 = 平滑值噪声（不是每帧随机跳）。
    /// left01 = 剩余时间/总时间。
    /// </summary>
    public static Vector2 ShakeOffset(float time, float amp, float left01, float frequency = 22f)
    {
        float k = amp * Mathf.Clamp01(left01) * Mathf.Clamp01(left01);
        if (k <= 0f) return Vector2.zero;
        return new Vector2(Noise(time * frequency, 0.37f), Noise(time * frequency, 5.11f)) * k;
    }

    /// <summary>一维平滑值噪声，输出 -1..1（确定性，沙盒可测）。</summary>
    public static float Noise(float x, float seed)
    {
        int i = Mathf.FloorToInt(x); float f = x - i; float u = f * f * (3f - 2f * f);
        return Mathf.Lerp(Hash(i, seed), Hash(i + 1, seed), u);
    }

    private static float Hash(int i, float seed)
    {
        double s = System.Math.Sin(i * 127.1 + seed * 311.7) * 43758.5453;
        return (float)(s - System.Math.Floor(s)) * 2f - 1f;
    }
}

/// <summary>
/// S216：运行时手感参数（MarioController / TricksterController / 陷阱读这里）。默认值 = 调参资产默认值，
/// 进入第 1 步房间时由 Step1Combo 从 RushMarioTuning 写入。非第 1 步的旧测试场景也用这套默认值（同一个物理 bug 一起修掉）。
/// </summary>
public static class LaunchFeel
{
    public static float gravity = 40f, airDrag = 2f, groundFriction = 40f, landGrace = 1.5f, hurtLift = 6f, blastLift = 8f, hurtFlash = 0.18f;
    public static bool fx = true;

    public static void Apply(MarioMindTuningSO t)
    {
        if (t == null) return;
        gravity = t.launchGravity; airDrag = t.launchAirDrag; groundFriction = t.launchGroundFriction; landGrace = t.launchLandGraceSeconds;
        hurtLift = t.hurtLift; blastLift = t.blastLift; hurtFlash = t.hurtHitFlashSeconds; fx = t.juiceFx;
    }
}
