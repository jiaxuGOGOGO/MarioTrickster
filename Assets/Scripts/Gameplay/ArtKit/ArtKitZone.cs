using UnityEngine;

/// <summary>
/// S245：素材互动区（素材槽 z Z a r 自带；也可以手动挂在任何物体上）——进入范围的人按素材包里配的互动：
/// 每 tickSeconds 掉半格 / 一格心（负数 = 回血）、减速、晕一下；谁会中（马里奥 / 你）可选。
/// 半格记账：马里奥是 PlayerHealth（整颗心）、你是 TricksterLives（整条命）→ 两次半格 = 真扣一颗（ArtKitRules.Accumulate），攒着的半格头上显示半颗心。
/// H3：区域本身一直看得见（素材就是预兆）；H9：间隔 ≥0.3 秒、晕 ≤1.5 秒；H4：马里奥心智不读这里，只是身体被打到。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ArtKitZone : MonoBehaviour
{
    [Tooltip("素材包里的名字（ArtSlot1..4 或任何图的名字）。互动从素材包读")] public string key = "ArtSlot1";
    [Tooltip("勾上 = 不读素材包，用下面这份")] public bool overrideBehavior;
    public ArtKitRules.Behavior behavior = ArtKitRules.Behavior.None;

    private ArtKitRules.Behavior b;
    private float marioTick, youTick; private int marioPending, youPending;
    private int marioInside, youInside;
    private MarioController mario; private TricksterController you;
    private SpriteRenderer halfMario, halfYou;
    public static float MarioSpeedScale { get; private set; } = 1f;
    private static int slowCount;

    public ArtKitRules.Behavior Effective => ArtKitRules.Clamp(overrideBehavior ? behavior : ArtKit.BehaviorOf(key));

    private void Awake()
    {
        var col = GetComponent<BoxCollider2D>(); col.isTrigger = true;
        if (string.IsNullOrEmpty(key) || key == "ArtSlot1") { string k = Step1ElementLabels.KeyOf(gameObject.name); if (k.StartsWith("ArtSlot")) key = k; }
        b = Effective;
    }

    /// <summary>S245：给"不是素材槽的元素"挂互动（例如素材包里给内置毒池 PoisonPool 配了"掉半格"）：换名字后重新读互动。</summary>
    public void Bind(string k) { key = k; b = Effective; }

    /// <summary>S245：素材包里某个名字勾了互动（例如内置毒池 PoisonPool 改成"掉一格"）→ 这个房间里所有同名元素加一个触发区子物体。
    /// 子物体自己带触发碰撞体：原来的实心碰撞体一点不动（H3：只加不改）。素材槽本来就挂着 ArtKitZone，跳过。</summary>
    public static void AttachAll(Transform root)
    {
        if (ArtKit.Current == null) return;
        foreach (Transform ch in root)
        {
            if (ch.GetComponentInChildren<ArtKitZone>() != null) continue;
            string key = Step1ElementLabels.KeyOf(ch.name);
            if (string.IsNullOrEmpty(key) || !ArtKit.BehaviorOf(key).enabled) continue;
            var go = new GameObject("S245_KitZone"); go.transform.SetParent(ch, false);
            var col = go.AddComponent<BoxCollider2D>(); col.isTrigger = true;
            var pc = ch.GetComponent<Collider2D>();
            Vector2 size = pc != null ? (Vector2)pc.bounds.size : Vector2.one;
            var lossy = ch.lossyScale; col.size = new Vector2(Mathf.Max(0.3f, size.x / Mathf.Max(0.01f, Mathf.Abs(lossy.x))) + 0.1f, Mathf.Max(0.3f, size.y / Mathf.Max(0.01f, Mathf.Abs(lossy.y))) + 0.1f);
            if (pc != null) col.offset = (Vector2)ch.InverseTransformPoint(pc.bounds.center);
            go.AddComponent<ArtKitZone>().Bind(key);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !b.enabled) return;
        var m = other.GetComponentInParent<MarioController>();
        if (m != null && b.hitsMario) { if (marioInside++ == 0) { mario = m; marioTick = b.tickSeconds * 0.5f; if (b.speedScale < 1f) { slowCount++; MarioSpeedScale = b.speedScale; } } }
        var t = other.GetComponentInParent<TricksterController>();
        if (t != null && b.hitsYou) { if (youInside++ == 0) { you = t; youTick = b.tickSeconds * 0.5f; if (b.speedScale < 1f) t.AbilitySpeedMultiplier = b.speedScale; } }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        if (other.GetComponentInParent<MarioController>() != null && marioInside > 0 && --marioInside == 0) { if (b.speedScale < 1f && --slowCount <= 0) { slowCount = 0; MarioSpeedScale = 1f; } }
        var t = other.GetComponentInParent<TricksterController>();
        if (t != null && youInside > 0 && --youInside == 0 && b.speedScale < 1f) t.AbilitySpeedMultiplier = 1f;
    }

    private void Update()
    {
        if (!b.enabled) return;
        var gm = GameManager.Instance; if (gm != null && gm.CurrentState != GameState.Playing) return;
        if (marioInside > 0 && mario != null) { marioTick -= Time.deltaTime; if (marioTick <= 0f) { marioTick = b.tickSeconds; HitMario(); } }
        if (youInside > 0 && you != null) { youTick -= Time.deltaTime; if (youTick <= 0f) { youTick = b.tickSeconds; HitYou(); } }
        Half(ref halfMario, mario != null ? mario.transform : null, marioPending);
        Half(ref halfYou, you != null ? you.transform : null, youPending);
    }

    private void HitMario()
    {
        var hp = mario.GetComponent<PlayerHealth>();
        int hearts = ArtKitRules.Accumulate(ref marioPending, b.damageHalves);
        if (hp != null) { if (hearts > 0) hp.TakeDamage(hearts); else if (hearts < 0) hp.Heal(-hearts); }
        if (b.stunSeconds > 0f) mario.ApplyKnockbackStun(b.stunSeconds);
        Move(mario.GetComponent<Rigidbody2D>(), mario.transform.position, v => { mario.ApplyKnockbackStun(Mathf.Max(0.15f, b.stunSeconds), true, false); });
        Pulse(mario.transform.position); Spent();
    }

    private void HitYou()
    {
        var lives = FindObjectOfType<TricksterLives>();
        int hearts = ArtKitRules.Accumulate(ref youPending, b.damageHalves);
        if (lives != null) { if (hearts > 0) lives.HitByZone(hearts); else if (hearts < 0) lives.SetLives(Mathf.Min(lives.MaxLives, lives.Lives - hearts)); }
        if (b.stunSeconds > 0f) you.ApplyKnockbackStun(b.stunSeconds);
        if (b.revealsYou && you.IsDisguised) { var ds = you.GetComponent<DisguiseSystem>(); if (ds != null) ds.Undisguise(); }
        if (b.bounceUp > 0f || b.knockback > 0f) you.Launch(LaunchV(you.transform.position), Mathf.Max(0.15f, b.stunSeconds));
        Pulse(you.transform.position); Spent();
    }

    /// <summary>S246：弹起 / 击退 = 一次性速度（和弹簧板一样：落地前不能动）。</summary>
    private Vector2 LaunchV(Vector3 who)
    {
        Vector2 v = Vector2.zero;
        if (b.knockback > 0f) { var d = ArtKitRules.KnockDir(transform.position.x, transform.position.y, who.x, who.y); v += new Vector2(d[0], d[1]) * b.knockback; }
        if (b.bounceUp > 0f) v.y = Mathf.Max(v.y, b.bounceUp);
        return v;
    }
    private void Move(Rigidbody2D rb, Vector3 who, System.Action<Vector2> stun)
    {
        if (rb == null || (b.bounceUp <= 0f && b.knockback <= 0f)) return;
        var v = LaunchV(who); rb.velocity = v; stun(v);
    }
    private bool spent;
    /// <summary>S246：一次性（地雷）：生效一次后整个物体消失（只关掉触发区和画面，不删别的碰撞体——H3）。</summary>
    private void Spent()
    {
        if (!b.oneShot || spent) return; spent = true; b.enabled = false;
        foreach (var c in GetComponents<Collider2D>()) if (c.isTrigger) c.enabled = false;
        var root = transform.parent != null && name == "S245_KitZone" ? transform.parent : transform;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        if (LaunchFeel.fx) Step1Fx.Burst(transform.position, 12, new Color(1f, 0.7f, 0.3f, 1f), 6f, Vector2.up, 360f, 12f, 0.16f, 0.5f);
        OnTriggerExit2DAll();
    }
    private void OnTriggerExit2DAll()
    {
        if (marioInside > 0 && b.speedScale < 1f && --slowCount <= 0) { slowCount = 0; MarioSpeedScale = 1f; }
        if (youInside > 0 && you != null && b.speedScale < 1f) you.AbilitySpeedMultiplier = 1f;
        marioInside = youInside = 0;
    }

    /// <summary>S246：持续推（风 / 传送带）：每个物理帧给里面的人加平台速度（和传送带同一个接口，不和走路打架）。</summary>
    private void FixedUpdate()
    {
        if (!b.enabled || b.pushX == 0f) return;
        var v = new Vector2(b.pushX, 0f);
        if (marioInside > 0 && mario != null) mario.SetPlatformVelocity(v);
        if (youInside > 0 && you != null) you.SetPlatformVelocity(v);
    }

    private void Pulse(Vector3 at)
    {
        if (!LaunchFeel.fx) return;
        var c = b.damageHalves < 0 ? new Color(0.5f, 1f, 0.6f, 0.9f) : b.damageHalves > 0 ? new Color(1f, 0.45f, 0.4f, 0.9f) : new Color(0.85f, 0.85f, 1f, 0.8f);
        Step1Fx.Burst((Vector2)at + Vector2.up * 0.6f, 5, c, 3f, Vector2.up, 120f, 4f, 0.12f, 0.35f); // 每跳一次 = 一小团粒子（H6：静音也看得见在掉 / 回）
    }

    /// <summary>攒着半格时头上一颗半透明的小心（看得出"再一下就扣一颗"）。</summary>
    private void Half(ref SpriteRenderer sr, Transform who, int pending)
    {
        if (who == null) return;
        if (sr == null) { var go = new GameObject("S245_HalfHeart"); go.transform.SetParent(who, false); go.transform.localPosition = new Vector3(0.45f, 0.9f, 0f); go.transform.localScale = Vector3.one * 0.35f; sr = go.AddComponent<SpriteRenderer>(); sr.sprite = Step1ArtSkin.Get("Heart", false) ?? Step1Sprites.Square; sr.sortingOrder = 230; }
        bool on = pending != 0; if (sr.enabled != on) sr.enabled = on;
        if (on) sr.color = pending > 0 ? new Color(1f, 1f, 1f, 0.55f) : new Color(0.6f, 1f, 0.7f, 0.55f);
    }

    private void OnDisable() { if (marioInside > 0 && b.speedScale < 1f && --slowCount <= 0) { slowCount = 0; MarioSpeedScale = 1f; } marioInside = youInside = 0; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { slowCount = 0; MarioSpeedScale = 1f; }
}
