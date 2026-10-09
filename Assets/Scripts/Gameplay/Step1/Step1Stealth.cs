using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>S241：房间光照模式（资产按数字存，新值只能加在末尾）。</summary>
public enum Step1LightMode { [InspectorName("自动轮换（第 1 局白天）")] Auto, [InspectorName("白天")] Day, [InspectorName("夜晚")] Night, [InspectorName("雨天")] Rain, [InspectorName("雨夜")] NightRain }

/// <summary>
/// S241（用户："马里奥拿着手电筒，捣蛋者在阴影里 / 遁地 / 像蜘蛛侠一样荡过他头顶 / 机关早一点晚一点也有效果"）——全部判断的纯逻辑，沙盒可测。
/// MonoBehaviour（Step1Lighting / TricksterBurrow / TricksterSilk / TricksterAbilitySystem 预约）只做接线。
///
/// 第一性原理：
///  · 看见 = 视锥 + 无遮挡 + <b>被照亮</b>（Mark of the Ninja：光是二值的，亮 = 能被看见，暗 = 只能被听见）。白天处处亮；夜里只有灯、火、炸弹和他的手电筒锥是亮的。
///  · 你 = 全局视野（整个房间你都看得见，只是暗处蒙一层灰）；他 = 只看得见亮处，暗处只能靠耳朵（脚步声小圈）。
///  · 遁地：只在裸露的地面（#）上会拱起土包——草地（v）、草丛下、下雨、暗处都看不见土包（Rek'Sai：只感知移动中的目标）。
///  · 蛛丝：单摆；收线时角动量守恒 ω' = ω·(L/L')² → 线速度 v' = v·L/L'（越收越快，荡得过他头顶）。
///  · 机关预约：按早了不算失败——机关先"等着"，他走进预判区才发动（输入缓冲的放大版）；等不到就退还次数和能量。
/// H4：这里的函数只收位置 / 地形 / 光源，不收任何捣蛋者内部状态。
/// </summary>
public static class Step1Stealth
{
    // ═════════ 光照 ═════════
    /// <summary>Auto：第 1 局白天，之后 白天 → 夜晚 → 雨天 → 雨夜 轮换（每局不同 = 重玩变化来自环境，不来自地图平移）。</summary>
    public static Step1LightMode Resolve(Step1LightMode setting, int round)
    {
        if (setting != Step1LightMode.Auto) return setting;
        if (round <= 1) return Step1LightMode.Day;
        switch ((round - 1) % 4) { case 1: return Step1LightMode.Night; case 2: return Step1LightMode.Rain; case 3: return Step1LightMode.NightRain; default: return Step1LightMode.Day; }
    }
    public static bool IsDark(Step1LightMode m) => m == Step1LightMode.Night || m == Step1LightMode.NightRain;
    public static bool IsRain(Step1LightMode m) => m == Step1LightMode.Rain || m == Step1LightMode.NightRain;

    public static string ModeName(Step1LightMode m)
    {
        switch (m)
        {
            case Step1LightMode.Night: return "🌙 夜晚";
            case Step1LightMode.Rain: return "🌧 雨天";
            case Step1LightMode.NightRain: return "🌧🌙 雨夜";
            default: return "☀ 白天";
        }
    }

    /// <summary>挡光 / 挡视线的格子（单向台面 '-' 是薄板，不挡）。</summary>
    public const string OpaqueChars = "#W%|cxCJ=vX";

    /// <summary>格子 DDA：从 a 到 b 之间（不含两端所在格）有没有挡光的格子。grid 第 0 行在最上面，y 从下往上数（和生成器一致）。</summary>
    public static bool ClearLine(IList<string> grid, Vector2 a, Vector2 b, string opaque = OpaqueChars)
    {
        if (grid == null || grid.Count == 0) return true;
        int ax = Mathf.RoundToInt(a.x), ay = Mathf.RoundToInt(a.y), bx = Mathf.RoundToInt(b.x), by = Mathf.RoundToInt(b.y);
        float dx = b.x - a.x, dy = b.y - a.y; float len = Mathf.Sqrt(dx * dx + dy * dy);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
        for (int i = 1; i < steps; i++)
        {
            float t = i / (float)steps;
            int cx = Mathf.RoundToInt(a.x + dx * t), cy = Mathf.RoundToInt(a.y + dy * t);
            if ((cx == ax && cy == ay) || (cx == bx && cy == by)) continue;
            if (opaque.IndexOf(LevelPathPlanner.At(grid, cx, cy)) >= 0) return false;
        }
        return true;
    }

    /// <summary>手电筒锥（和视锥同一套几何：前方、半角、距离）。</summary>
    public static bool InCone(Vector2 eye, bool facingRight, Vector2 p, float range, float halfAngleDeg)
    {
        float dx = p.x - eye.x, dy = p.y - eye.y; float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d > range) return false;
        if (d < 0.6f) return true;
        float fx = facingRight ? 1f : -1f;
        double cos = (dx * fx) / d;
        return cos >= Math.Cos(halfAngleDeg * Math.PI / 180.0);
    }

    /// <summary>一个光源（灯 / 火 / 炸弹 / 点燃的油桶）。</summary>
    public struct Light { public Vector2 at; public float radius; public Light(Vector2 a, float r) { at = a; radius = r; } }

    /// <summary>
    /// 这一点亮不亮。白天 = 处处亮。夜里：在某个光源半径内且没被墙挡住，或者在他的手电筒锥里且没被挡住。
    /// </summary>
    public static bool Lit(bool dark, Vector2 p, IList<Light> lights, bool flashlightOn, Vector2 eye, bool facingRight, float flashRange, float flashHalfAngle, IList<string> grid)
    {
        if (!dark) return true;
        if (lights != null)
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (Vector2.Distance(l.at, p) <= l.radius && ClearLine(grid, l.at, p)) return true;
            }
        return flashlightOn && InCone(eye, facingRight, p, flashRange, flashHalfAngle) && ClearLine(grid, eye, p);
    }

    /// <summary>他能不能"看清"这一点：亮 = 能；暗 = 只有贴身（nearRadius 内）才看得见。</summary>
    public static bool Visible(bool lit, float distance, float nearRadius) => lit || distance <= nearRadius;

    /// <summary>头顶感知：你在他头顶上方 radius 格内（高出半格以上），而且那里被照亮 → 他会察觉（抬头看见一个影子）。暗处荡过去 = 不察觉。</summary>
    public static bool OverheadNoticed(Vector2 mario, Vector2 you, float radius, bool litThere)
    {
        if (!litThere) return false;
        float dy = you.y - mario.y; if (dy < 0.6f) return false;
        return Vector2.Distance(mario, you) <= radius;
    }

    // ═════════ 声音 ═════════
    /// <summary>你跑动的脚步声能传多远（圈 = 判定同一个数，S224 规则）。下雨盖住一部分。</summary>
    public static float FootstepRadius(MarioMindTuningSO t, bool rain) =>
        Step1Readability.SoundRadius(Step1Readability.Sound.Footstep, t) * (rain ? Mathf.Clamp01(t.rainHearingScale) : 1f);

    /// <summary>跑动才有脚步声（伪装 / 遁地 / 摆荡 / 站着都没有）。</summary>
    public static bool MakesFootstep(bool grounded, float speedX, bool quiet) => grounded && !quiet && Mathf.Abs(speedX) > 2f;

    // ═════════ 遁地 ═════════
    /// <summary>能钻进去：站在地上、没伪装 / 没缩小 / 没坐炮 / 没在荡，脚下是土（# 或草地 v）。</summary>
    public static bool CanBurrow(bool grounded, bool disguised, bool shrunk, bool seated, bool swinging, char below, float cooldown) =>
        grounded && !disguised && !shrunk && !seated && !swinging && cooldown <= 0f && (below == '#' || below == 'v');

    /// <summary>土包看不看得见：只有裸露地面（#、上面没草丛）+ 亮 + 不下雨 + 你在动。</summary>
    public static bool MoundVisible(char ground, bool bushAbove, bool lit, bool rain, bool moving) =>
        ground == '#' && !bushAbove && lit && !rain && moving;

    /// <summary>他踩到你（裸露地面上、几乎同一格）→ 把你踩出来。</summary>
    public static bool Trampled(Vector2 mario, Vector2 you, char ground) =>
        ground == '#' && Mathf.Abs(mario.x - you.x) <= 0.6f && Mathf.Abs(mario.y - you.y) <= 1.1f;

    /// <summary>他的扫描（Q）能把地下的你逼出来：半径内就算（扫描 100% 真实，H5）。</summary>
    public static bool ScanFlushes(Vector2 mario, Vector2 you, float scanRadius) => Vector2.Distance(mario, you) <= scanRadius;

    // ═════════ 蛛丝（单摆）═════════
    /// <summary>从你身上朝斜上方（60°）射丝，找第一块能挂的实心格（单向台面不算）；斜上方没有就试正上方。返回挂点（格子下沿）。</summary>
    public static Vector2? FindAnchor(IList<string> grid, Vector2 from, bool facingRight, float range, string solid = OpaqueChars)
    {
        if (grid == null) return null;
        double[] angles = { 60, 75, 90 };
        foreach (var deg in angles)
        {
            double r = deg * Math.PI / 180.0;
            float dx = (float)Math.Cos(r) * (facingRight ? 1f : -1f), dy = (float)Math.Sin(r);
            for (float s = 1f; s <= range; s += 0.25f)
            {
                var p = new Vector2(from.x + dx * s, from.y + dy * s);
                int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
                if (solid.IndexOf(LevelPathPlanner.At(grid, cx, cy)) >= 0)
                {
                    if (s < 1.5f) break; // 太近（贴着天花板）没法荡
                    return new Vector2(p.x, cy - 0.5f);
                }
            }
        }
        return null;
    }

    /// <summary>摆的状态：角度（0 = 正下方，正 = 往右摆出去）、角速度、线长。</summary>
    public struct Swing { public double angle, omega, length; }

    public static Swing FromBody(Vector2 anchor, Vector2 pos, Vector2 vel)
    {
        double rx = pos.x - anchor.x, ry = pos.y - anchor.y;
        double L = Math.Max(0.01, Math.Sqrt(rx * rx + ry * ry));
        double a = Math.Atan2(rx, -ry);
        double omega = (rx * vel.y - ry * vel.x) / (L * L); // 角速度 = (r × v) / L²
        return new Swing { angle = a, omega = omega, length = L };
    }

    public static Vector2 PosOf(Vector2 anchor, Swing s) => new Vector2(anchor.x + (float)(Math.Sin(s.angle) * s.length), anchor.y - (float)(Math.Cos(s.angle) * s.length));
    public static Vector2 VelOf(Swing s) => new Vector2((float)(Math.Cos(s.angle) * s.length * s.omega), (float)(Math.Sin(s.angle) * s.length * s.omega));

    /// <summary>
    /// 走一步：重力回摆 + ←→ 打秋千（切向加速度 pump）+ ↑↓ 收放线（角动量守恒：ω·L² 不变）+ 少量阻尼。
    /// newLength 会被夹在 [minLen, maxLen]。
    /// </summary>
    public static Swing Step(Swing s, double newLength, double pump, double g, double dt, double damping, double minLen, double maxLen)
    {
        double L2 = Math.Max(minLen, Math.Min(maxLen, newLength));
        if (Math.Abs(L2 - s.length) > 1e-9) { double k = s.length / L2; s.omega *= k * k; s.length = L2; }
        double alpha = -(g / s.length) * Math.Sin(s.angle) + pump / s.length - damping * s.omega;
        s.omega += alpha * dt;
        s.angle += s.omega * dt;
        if (s.angle > 2.6) { s.angle = 2.6; if (s.omega > 0) s.omega = 0; }    // 不绕过挂点头顶（约 150°）
        if (s.angle < -2.6) { s.angle = -2.6; if (s.omega < 0) s.omega = 0; }
        return s;
    }

    /// <summary>能射丝：没伪装 / 没坐炮 / 没遁地 / 冷却好了。</summary>
    public static bool CanSilk(bool disguised, bool seated, bool burrowed, float cooldown) => !disguised && !seated && !burrowed && cooldown <= 0f;

    // ═════════ 机关预约（容错）═════════
    public enum Arm { FireNow, Wait, Expire }

    /// <summary>按下 L 之后每一帧：他已进预判区 → 发动；等太久 → 作废（退还）；否则继续等（不预警、不暴露）。</summary>
    public static Arm ArmStep(bool marioInZone, float waited, float window)
    {
        if (marioInZone) return Arm.FireNow;
        return waited >= window ? Arm.Expire : Arm.Wait;
    }

    /// <summary>一次发动算不算"没打中"：从开始预警到进入冷却这段时间里一次都没坑到他。</summary>
    public static bool Missed(float firedAt, float lastHitAt, float now) => !(lastHitAt >= firedAt - 0.01f && lastHitAt <= now + 0.01f);
}
