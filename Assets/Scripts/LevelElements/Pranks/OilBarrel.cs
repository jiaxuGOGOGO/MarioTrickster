using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S199：油桶（ASCII 'U'）—— 连锁爆炸的"引信"。平时是实心障碍（挡路、挡视线、可站上去）。
/// 点燃方式（任一）：炸弹爆炸波及、喷火的火焰碰到、炮弹打中、**另一个油桶爆炸波及**（连锁）。
/// 点燃后：冒烟闪烁 oilFuseSeconds（默认 0.8 秒，H3 预警）→ 爆炸：与炸弹同规则（半径 oilRadius 内炸毁普通地形/摆件、
///        伤马里奥与你、点燃范围内其它油桶）。一回合一个桶只炸一次，回合重置复原。
/// 规则：H1——死局检查把油桶当"实心"（最坏情况：不炸）；炸只会多开路。H3——引信期闪烁 + 倒计时字幕。H4——只看火/爆炸，不看人。
/// 参考：Spelunky 的"几乎所有东西都会被爆炸影响"、火蛙爆炸像炸弹 → 元素之间互相作用 = 涌现（Critical Gaming 分析）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class OilBarrel : LevelElementBase
{
    [SerializeField] private float fuseSeconds = 0.8f;
    [SerializeField] private float radius = 1.8f;
    [SerializeField] private float stun = 1f, knock = 6f;
    [SerializeField] private int damageMario = 1, damageSelf = 1;
    private float fuse = -1f;
    private bool exploded;
    private BoxCollider2D body;
    private Transform visual;
    private SpriteRenderer sr;
    private Color baseColor;
    private static readonly List<OilBarrel> all = new List<OilBarrel>();
    public static IReadOnlyList<OilBarrel> All => all;
    public bool Lit => fuse >= 0f;
    public float Radius => radius;
    public bool Exploded => exploded;

    public void Configure(float fuseS, float r, float stunS, float knockV, int dmgM, int dmgS)
    { fuseSeconds = Mathf.Max(0.2f, fuseS); radius = r; stun = stunS; knock = knockV; damageMario = dmgM; damageSelf = dmgS; }

    private void Awake()
    {
        elementName = "油桶";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive | ElementTag.OneShot | ElementTag.Resettable | ElementTag.Damaging;
        description = "被火/爆炸/炮弹点燃后连锁爆炸";
        body = GetComponent<BoxCollider2D>();
        visual = transform.Find("Visual");
        sr = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponentInChildren<SpriteRenderer>();
        baseColor = sr != null ? sr.color : Color.white;
    }

    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); }

    public void Ignite() { if (!exploded && fuse < 0f) fuse = fuseSeconds; }

    /// <summary>纯逻辑：一次爆炸（中心、半径）会点燃哪些桶（下标）。</summary>
    public static List<int> ChainTargets(IList<Vector2> barrels, Vector2 center, float r, int except = -1)
    {
        var hit = new List<int>();
        for (int i = 0; i < barrels.Count; i++) if (i != except && (barrels[i] - center).sqrMagnitude <= r * r) hit.Add(i);
        return hit;
    }

    private void Update()
    {
        if (exploded) return;
        // 被喷火点燃
        if (fuse < 0f)
            foreach (var fire in FireTrapCache.All)
                if (fire != null && fire.IsFiring && fire.FlameBounds.Intersects(body.bounds)) { Ignite(); break; }
        if (fuse < 0f) return;
        fuse -= Time.deltaTime;
        if (sr != null) sr.color = Mathf.Sin(Time.time * 30f) > 0f ? new Color(1f, 0.35f, 0.1f) : baseColor;
        if (fuse <= 0f) Explode();
    }

    private void OnTriggerEnter2D(Collider2D other) { if (other != null && other.GetComponentInParent<CannonBall>() != null) Ignite(); }
    private void OnCollisionEnter2D(Collision2D c) { if (c != null && c.collider.GetComponentInParent<CannonBall>() != null) Ignite(); }

    private void Explode()
    {
        exploded = true; fuse = -1f;
        if (body != null) body.enabled = false;
        if (visual != null) visual.gameObject.SetActive(false);
        TricksterBomb.Blast(transform.position, radius, stun, knock, damageMario, damageSelf, this);
    }

    public override void OnLevelReset()
    {
        exploded = false; fuse = -1f;
        if (body != null) body.enabled = true;
        if (visual != null) visual.gameObject.SetActive(true);
        if (sr != null) sr.color = baseColor;
    }
}

/// <summary>S199：喷火器列表缓存（油桶查询用，避免每帧 FindObjectsOfType）。</summary>
public static class FireTrapCache
{
    private static FireTrap[] cache = new FireTrap[0];
    private static int frame = -1;
    public static FireTrap[] All
    {
        get
        {
            if (frame < 0 || Time.frameCount - frame > 60) { cache = Object.FindObjectsOfType<FireTrap>(); frame = Time.frameCount; }
            return cache;
        }
    }
}
