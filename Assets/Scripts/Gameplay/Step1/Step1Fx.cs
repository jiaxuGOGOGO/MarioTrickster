using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S216：轻量特效（纯画面，不碰物理/碰撞/AI —— 宪法 H4：马里奥不读这些物体）。
/// 冲击环、尘土、碎屑、"连锁火花线"。全部运行时生成方块精灵，自动销毁，有同屏上限（性能红线）。
/// 曲线来自 Step1Feel（沙盒测试过）。H10 自动检查期间不生成。
/// </summary>
public class Step1Fx : MonoBehaviour
{
    private enum Kind { Ring, Particle, Line, Sound }
    private Kind kind;
    private float life, age;
    private Color color;
    private float size, startScale;
    private Vector2 vel;
    private float gravity;
    private SpriteRenderer sr;
    private LineRenderer line;

    private static int alive;
    public const int MaxAlive = 90;
    public static int Alive => alive;

    public static bool Enabled => LaunchFeel.fx && !Step1HandsOffCheck.IsRunning && Application.isPlaying;

    private static SpriteRenderer MakeSprite(GameObject go, Color c, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Step1Sprites.Square; sr.color = c; sr.sortingOrder = order;
        return sr;
    }

    /// <summary>冲击环（其实是一个淡出的方形"冲击波"，像素风）。radius = 最终半径（格）。</summary>
    public static void Ring(Vector2 at, float radius, Color c)
    {
        if (!Enabled || alive >= MaxAlive || radius <= 0f) return;
        var go = new GameObject("Fx_Ring");
        go.transform.position = at;
        var fx = go.AddComponent<Step1Fx>();
        fx.kind = Kind.Ring; fx.life = 0.28f; fx.color = c; fx.size = radius * 2f; fx.startScale = 0.25f;
        fx.sr = MakeSprite(go, c, 40);
        go.transform.localScale = Vector3.one * fx.size * fx.startScale;
        go.transform.rotation = Quaternion.Euler(0, 0, 45f);
    }

    /// <summary>一小撮颗粒（尘土 / 碎屑 / 火星）。dir = 大致方向（0 = 四散），spread = 散开角（度）。</summary>
    public static void Burst(Vector2 at, int count, Color c, float speed, Vector2 dir, float spread = 360f, float grav = 18f, float sizeCells = 0.14f, float lifeSeconds = 0.4f)
    {
        if (!Enabled) return;
        count = Mathf.Min(count, MaxAlive - alive);
        float baseAng = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Fx_Bit");
            go.transform.position = at;
            var fx = go.AddComponent<Step1Fx>();
            float a = (baseAng + (count == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, (i + 0.5f) / count)) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            fx.kind = Kind.Particle; fx.life = lifeSeconds * Random.Range(0.75f, 1.2f); fx.color = c;
            fx.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.6f, 1.1f);
            fx.gravity = grav; fx.size = sizeCells * Random.Range(0.8f, 1.3f);
            fx.sr = MakeSprite(go, c, 39);
            go.transform.localScale = Vector3.one * fx.size;
        }
    }

    /// <summary>地面尘土（落地 / 弹起 / 滑倒）：左右两边各喷一点。</summary>
    public static void Dust(Vector2 feet, float strength = 1f)
    {
        var c = new Color(0.86f, 0.8f, 0.68f, 0.9f);
        int n = Mathf.Clamp(Mathf.RoundToInt(3 * strength), 2, 6);
        Burst(feet, n, c, 3.2f * strength, Vector2.left, 50f, 6f, 0.16f, 0.35f);
        Burst(feet, n, c, 3.2f * strength, Vector2.right, 50f, 6f, 0.16f, 0.35f);
    }

    /// <summary>连锁"导火线"：从上一环到下一环一道快速变淡的火花线 + 终点小火星 → 玩家看得见"是谁引发了谁"。</summary>
    public static void Link(Vector2 from, Vector2 to, Color c)
    {
        if (!Enabled || alive >= MaxAlive || (to - from).sqrMagnitude < 0.01f) return;
        var go = new GameObject("Fx_Link");
        var fx = go.AddComponent<Step1Fx>();
        fx.kind = Kind.Line; fx.life = 0.4f; fx.color = c;
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, from);
        lr.useWorldSpace = true; lr.startWidth = 0.12f; lr.endWidth = 0.05f; lr.sortingOrder = 41;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = c; lr.endColor = c;
        fx.line = lr; fx.vel = to; fx.startScale = 0f; fx.size = 0f;
        fx.linkFrom = from;
        Burst(to, 5, c, 3.5f, Vector2.up, 160f, 10f, 0.12f, 0.3f);
    }

    private Vector2 linkFrom;

    /// <summary>S224 声音圈（Mark of the Ninja：声音 = 画面上的圆，静音也能读）：从发声点扩散到 radius（= 马里奥真实的听力范围）再淡出。
    /// 他在圈里 = 他听见了（只知道位置，H4）。只是画面，没有碰撞体。</summary>
    public static void SoundRing(Vector2 at, float radius) => SoundRing(at, radius, Step1Readability.SoundColor);
    public static void SoundRing(Vector2 at, float radius, Color c)
    {
        if (!Enabled || alive >= MaxAlive || radius <= 0f) return;
        var go = new GameObject("Fx_Sound");
        go.transform.position = at;
        var fx = go.AddComponent<Step1Fx>();
        fx.kind = Kind.Sound; fx.life = 0.9f; fx.color = c; fx.size = radius; fx.linkFrom = at;
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true; lr.loop = true; lr.positionCount = SoundSegments; lr.startWidth = lr.endWidth = 0.08f; lr.sortingOrder = 42;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = c;
        fx.line = lr;
        fx.DrawCircle(0.15f * radius);
    }
    private const int SoundSegments = 40;
    private void DrawCircle(float r)
    {
        for (int i = 0; i < SoundSegments; i++)
        {
            float a = i * Mathf.PI * 2f / SoundSegments;
            line.SetPosition(i, linkFrom + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
    }

    private void OnEnable() { alive++; }
    private void OnDisable() { alive--; }

    private void Update()
    {
        age += Time.deltaTime;
        float t = life > 0f ? age / life : 1f;
        if (t >= 1f) { Destroy(gameObject); return; }
        switch (kind)
        {
            case Kind.Ring:
                Step1Feel.Ring(t, startScale, out float s, out float a);
                transform.localScale = Vector3.one * size * s;
                var c = color; c.a *= a * 0.8f; sr.color = c;
                break;
            case Kind.Particle:
                vel.y -= gravity * Time.deltaTime;
                transform.position += (Vector3)(vel * Time.deltaTime);
                var pc = color; pc.a *= 1f - t * t; sr.color = pc;
                transform.localScale = Vector3.one * size * (1f - 0.5f * t);
                break;
            case Kind.Sound:
                // 前 60% 扩散到真实范围（缓出），后面停在边上淡出 → 最后看到的就是"他能听见的边界"
                DrawCircle(size * Mathf.Lerp(0.15f, 1f, Step1Feel.SmoothStep01(Mathf.Clamp01(t / 0.6f))));
                var sc = color; sc.a *= 1f - Mathf.Clamp01((t - 0.6f) / 0.4f); line.startColor = sc; line.endColor = sc;
                break;
            case Kind.Line:
                // 前 40% 时间火花从起点"烧"到终点，然后整条线淡出
                float grow = Mathf.Clamp01(t / 0.4f);
                line.SetPosition(0, Vector2.Lerp(linkFrom, vel, Mathf.Clamp01((t - 0.4f) / 0.6f)));
                line.SetPosition(1, Vector2.Lerp(linkFrom, vel, Step1Feel.SmoothStep01(grow)));
                var lc = color; lc.a *= 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);
                line.startColor = lc; line.endColor = lc;
                break;
        }
    }

    private void OnDestroy() { if (line != null && line.material != null) Destroy(line.material); }
}
