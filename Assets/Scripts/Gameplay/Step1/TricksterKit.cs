using UnityEngine;

/// <summary>
/// S197：捣蛋者技能包（数据驱动，全部数值在 RushMarioTuning）。
///   B = 炸弹：身上 bombsPerRound 枚（默认 3）。放下 → 引信 bombFuseSeconds（闪烁 + 滴答字幕，H3/H6）→ 炸开半径 bombRadius 内的
///       **裂墙 %、裂缝地板 x、箱子 c**，马里奥在范围内会被炸晕并击退（不扣命，只晕）。爆炸声马里奥隔墙也听得见（H4：只知道位置）。
///       必须**现形**才能放（伪装时双手被占）。
///   Z = 缩小：瞬间缩到 shrinkScale（默认 0.5），持续 shrinkSeconds，每回合 shrinkUsesPerRound 次。
///       缩小时能钻 1 格高的缝、跑得更快，但**不能伪装、不能触发机关**（代价）。头顶有东西时不会变回（等到能站起来）。
///   通风管 = 场景元素 '@'（Vent.cs），靠近按 ↓ 进入，由那里负责。
/// 规则：所有技能事件只带位置，不带捣蛋者信息（H4）；自动检查（H10）时技能不工作。
/// </summary>
public class TricksterKit : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private TricksterController self;
    private GameManager manager;
    private int bombsLeft, shrinksLeft;
    private float shrinkTimer, bombCooldown;
    private bool shrunk;
    private static readonly Collider2D[] s_hits = new Collider2D[16];

    public int BombsLeft => bombsLeft;
    public int ShrinksLeft => shrinksLeft;
    public bool Shrunk => shrunk;
    public float ShrinkRemaining => Mathf.Max(0f, shrinkTimer);
    public static TricksterKit Instance { get; private set; }

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; if (manager != null) manager.OnRoundStart -= ResetRound; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        ResetRound();
    }

    private void ResetRound()
    {
        bombsLeft = tuning.bombsPerRound; shrinksLeft = tuning.shrinkUsesPerRound; bombCooldown = 0f;
        if (shrunk) Grow(true);
    }

    private void Update()
    {
        if (bombCooldown > 0f) bombCooldown -= Time.deltaTime;
        if (shrunk)
        {
            shrinkTimer -= Time.deltaTime;
            if (self != null && self.IsDisguised) self.OnDisguisePressed(); // 缩小时不能伪装（代价）
            if (shrinkTimer <= 0f) Grow(false);
        }
        if (self == null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f) return;
        if (Step1Keys.Down(KeyCode.B)) TryBomb();
        if (Step1Keys.Down(KeyCode.Z)) TryShrink();
    }

    // ── 炸弹 ──────────────────────────────────────────
    public static bool CanBomb(bool disguised, bool shrunk, int left, float cooldown) => !disguised && !shrunk && left > 0 && cooldown <= 0f;

    public bool TryBomb()
    {
        if (!CanBomb(self.IsDisguised, shrunk, bombsLeft, bombCooldown))
        {
            Step1Hint.Show(self.IsDisguised ? Step1Text.BombNeedUndisguise : shrunk ? Step1Text.BombWhileSmall : bombsLeft <= 0 ? Step1Text.BombNone : Step1Text.BombCooldown);
            return false;
        }
        bombsLeft--;
        bombCooldown = tuning.bombCooldown;
        var go = new GameObject("TricksterBomb");
        go.transform.position = (Vector2)transform.position + Vector2.down * 0.2f;
        var bomb = go.AddComponent<TricksterBomb>();
        bomb.Arm(tuning.bombFuseSeconds, tuning.bombRadius, tuning.bombStunSeconds, tuning.bombKnockback);
        Step1Hint.Show(string.Format(Step1Text.BombPlaced, bombsLeft));
        return true;
    }

    // ── 缩小 ──────────────────────────────────────────
    public static bool CanShrink(bool shrunk, int left) => !shrunk && left > 0;

    public bool TryShrink()
    {
        if (shrunk) { Grow(false); return false; } // 再按一次提前变回（头顶有东西就不变）
        if (!CanShrink(shrunk, shrinksLeft)) { Step1Hint.Show(Step1Text.ShrinkNone); return false; }
        if (self.IsDisguised) self.OnDisguisePressed();
        shrinksLeft--;
        shrunk = true; shrinkTimer = tuning.shrinkSeconds;
        self.SetBodyScale(tuning.shrinkScale);
        self.AbilitySpeedMultiplier = tuning.shrinkSpeedMultiplier;
        Step1Hint.Show(string.Format(Step1Text.ShrinkOn, tuning.shrinkSeconds, shrinksLeft));
        return true;
    }

    private void Grow(bool force)
    {
        if (!force && !RoomToStand()) { shrinkTimer = 0.2f; Step1Hint.Show(Step1Text.ShrinkBlocked, 0.5f); return; }
        shrunk = false;
        self.SetBodyScale(1f);
        self.AbilitySpeedMultiplier = 1f;
    }

    /// <summary>头顶是否有空间站起来（防止卡进天花板，H9）。</summary>
    private bool RoomToStand()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return true;
        Bounds b = col.bounds;
        float fullH = PhysicsMetrics.TRICKSTER_COLLIDER_HEIGHT;
        Vector2 center = new Vector2(b.center.x, b.min.y + fullH * 0.5f + 0.02f);
        int n = Physics2D.OverlapBoxNonAlloc(center, new Vector2(b.size.x * 0.9f, fullH - 0.06f), 0f, s_hits);
        for (int i = 0; i < n; i++)
        {
            var c = s_hits[i];
            if (c == null || c.isTrigger || c.transform.IsChildOf(transform)) continue;
            if (MarioSuspicionTracker.IsOneWayPlatform(c)) continue;
            return false;
        }
        return true;
    }

    /// <summary>缩小时机关触发被禁用（代价）。TricksterController 的 L 键会先问这里。</summary>
    public static bool BlocksPranks => Instance != null && Instance.shrunk;
}

/// <summary>S197：炸弹本体。引信期间闪烁并显示倒计时（H3），爆炸 = 小范围破坏 + 晕眩。</summary>
public class TricksterBomb : MonoBehaviour
{
    private float fuse, radius, stun, knock, total;
    private SpriteRenderer sr;
    private static readonly Collider2D[] s_hits = new Collider2D[24];
    public static event System.Action<Vector2> Exploded;

    public void Arm(float fuseSeconds, float r, float stunSeconds, float knockback)
    {
        fuse = total = Mathf.Max(0.3f, fuseSeconds); radius = r; stun = stunSeconds; knock = knockback;
        var v = new GameObject("Visual");
        v.transform.SetParent(transform, false);
        sr = v.AddComponent<SpriteRenderer>();
        sr.sprite = Step1Sprites.Square;
        sr.color = new Color(0.1f, 0.1f, 0.1f);
        sr.sortingOrder = 30;
        v.transform.localScale = Vector3.one * 0.45f;
    }

    /// <summary>纯逻辑：某点是否在爆炸范围内。</summary>
    public static bool InBlast(Vector2 center, Vector2 p, float r) => (p - center).sqrMagnitude <= r * r;

    private void Update()
    {
        fuse -= Time.deltaTime;
        if (sr != null)
        {
            float rate = Mathf.Lerp(12f, 3f, fuse / total);
            sr.color = Mathf.Sin(Time.time * rate * Mathf.PI) > 0f ? new Color(1f, 0.25f, 0.15f) : new Color(0.1f, 0.1f, 0.1f);
        }
        if (fuse <= 0f) Explode();
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Camera.main == null) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.7f);
        if (sp.z < 0f) return;
        var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
        GUI.Label(new Rect(at.x - 60, at.y - 20, 120, 40), $"<color=#FF6B4A><b>💣 {Mathf.CeilToInt(Mathf.Max(0f, fuse))}</b></color>", Step1Gui.Text(24, TextAnchor.MiddleCenter, false));
    }

    private void Explode()
    {
        Vector2 c = transform.position;
        int n = Physics2D.OverlapCircleNonAlloc(c, radius, s_hits);
        for (int i = 0; i < n; i++)
        {
            var h = s_hits[i];
            if (h == null) continue;
            var wall = h.GetComponentInParent<CrackedWall>(); if (wall != null) { wall.Break(); continue; }
            var crack = h.GetComponentInParent<CrackFloor>(); if (crack != null) { crack.ShatterFromBlast(); continue; }
            var mario = h.GetComponentInParent<MarioController>();
            if (mario != null)
            {
                var rb = mario.GetComponent<Rigidbody2D>();
                if (rb != null) { var dir = ((Vector2)mario.transform.position - c).normalized; rb.velocity = new Vector2(Mathf.Sign(dir.x == 0 ? 1 : dir.x) * knock, knock * 0.6f); }
                mario.ApplyKnockbackStun(stun);
                BombEvents.RaiseMarioBlasted();
            }
            var crate = h.GetComponentInParent<SceneryProp>();
            if (crate != null && crate.name.StartsWith("Crate")) crate.gameObject.SetActive(false);
        }
        Exploded?.Invoke(c);
        Step1Hint.Show(Step1Text.BombBoom);
        var cam = FindObjectOfType<Step1RoomCamera>();
        if (cam != null) cam.Shake(0.35f, 0.35f);
        Destroy(gameObject);
    }
}

public static class BombEvents
{
    public static event System.Action MarioBlasted;
    public static void RaiseMarioBlasted() => MarioBlasted?.Invoke();
}

/// <summary>S197：运行时生成的简单方块精灵（炸弹、提示用）。</summary>
public static class Step1Sprites
{
    private static Sprite square;
    public static Sprite Square
    {
        get
        {
            if (square != null) return square;
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
            var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return square;
        }
    }
}
