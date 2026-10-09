using UnityEngine;

/// <summary>
/// S199：诱饵（捣蛋者 G 键，每回合 decoysPerRound 次）—— 在原地留下一个"你"的假身，自己可以溜走。
/// 假身会朝你面向的方向慢慢走 decoyWalkSeconds 秒再站住，存在 decoySeconds 秒。
/// 马里奥看见假身 = 当成你（起疑、追它）；**走近到 decoyRevealDistance 内会识破**（"咦是假的！"），并短暂起疑你真身可能在附近。
/// 代价：必须**现形**才能放（伪装时双手被占）；放下后 0.5 秒内你也现形可见。
/// 反制：马里奥的**透视**道具能直接看穿诱饵。
/// 实现（H4 合规）：诱饵是场景里一个真实物体，马里奥只通过 MarioEyes 的"看见"通道感知它（与看见你同一个视锥与遮挡规则），
/// 不会得到任何关于真身位置的信息。
/// </summary>
public class DecoyAbility : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private TricksterController self;
    private GameManager manager;
    private int left;
    public int DecoysLeft => left;
    public static Decoy Active { get; private set; }

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        ResetRound();
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= ResetRound; }
    private void ResetRound() { left = tuning.decoysPerRound; if (Active != null) Destroy(Active.gameObject); Active = null; }

    public static bool CanDecoy(bool disguised, bool shrunk, int left, bool oneAlready) => !disguised && !shrunk && left > 0 && !oneAlready;

    /// <summary>S242：现在按 G 会丢哪种诱饵——装备栏里有形态 = 道具诱饵（伪装中也能丢），否则 = 以前的"假你"。</summary>
    public static bool PropMode => TricksterLoadout.Instance != null && TricksterLoadout.Instance.CurrentChar != '\0';

    private void Update()
    {
        if (Step1QuickTest.NoLimits) left = Mathf.Max(left, 1); // S236：F9 测试
        if (self == null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f) return;
        if (PranksterCannon.TricksterSeated) return; // S240：坐在炮里，其他技能键不生效
        if (TricksterBurrow.BodyBusy) return; // S241：遁地 / 摆荡中，其他技能键不生效
        if (!Step1Keys.Down(KeyCode.G)) return;
        bool shrunk = TricksterKit.Instance != null && TricksterKit.Instance.Shrunk;
        // S242：Shift+G = 强制丢"假你"（装备栏有形态时默认丢道具诱饵）
        bool prop = PropMode && !Step1Keys.Shift();
        if (!Step1Loadout.CanDecoy(prop, self.IsDisguised, shrunk, left, Active != null))
        {
            Step1Hint.Show(self.IsDisguised && !prop ? Step1Text.DecoyNeedUndisguise : left <= 0 ? Step1Text.DecoyNone : Step1Text.DecoyBusy);
            return;
        }
        left--;
        var go = new GameObject(prop ? "TricksterPropDecoy" : "TricksterDecoy");
        go.transform.position = transform.position;
        var d = go.AddComponent<Decoy>();
        if (prop)
        {
            var lo = TricksterLoadout.Instance; char ch = lo.CurrentChar; var look = lo.LookOf(ch);
            d.InitProp(ch, look.sprite, look.color, look.size, look.icon, self.IsFacingRightValue, tuning.decoySeconds + 4f, tuning.propDecoyThrow, tuning.propDecoyWriggleEvery, tuning.decoyRevealDistance);
            var info = ElementCatalog.Get(ch);
            Step1Hint.Show(string.Format(Step1Text.PropDecoyPlaced, info != null ? info.zh : ch.ToString(), left));
        }
        else
        {
            var sr = self.GetComponentInChildren<SpriteRenderer>();
            d.Init(sr, self.IsFacingRightValue, tuning.decoySeconds, tuning.decoyWalkSeconds, tuning.decoyRevealDistance);
            Step1Hint.Show(string.Format(Step1Text.DecoyPlaced, left));
        }
        Active = d;
    }

    public static void Clear(Decoy d) { if (Active == d) Active = null; }
}

/// <summary>S199：诱饵本体（外观复制你的精灵）。</summary>
public class Decoy : MonoBehaviour
{
    private float life, walk, reveal;
    private bool right, revealed;
    private SpriteRenderer sr;
    public bool Revealed => revealed;
    public float RevealDistance => reveal;

    // ── S242：道具诱饵（照抄装备栏当前格的外观；丢出去 → 落地 → 时不时扭一下） ─────────
    private bool isProp; private char propChar; private float age, throwDist, wriggleEvery; private Vector2 start; private Transform visual;
    private const float Flight = 0.45f;
    /// <summary>是不是道具诱饵（看起来是一个普通道具，不是"你"）。</summary>
    public bool IsProp => isProp;
    public char PropChar => propChar;
    /// <summary>此刻在扭（看起来像"会动的道具"——他看见会过来查看）。还在空中飞 = 也算在动。</summary>
    public bool Wriggling => isProp && (age < Flight || Step1Loadout.Wriggling(age, wriggleEvery, Flight + 0.6f));

    public void InitProp(char ch, Sprite sprite, Color color, Vector2 size, Sprite icon, bool facingRight, float seconds, float distance, float every, float revealDistance)
    {
        isProp = true; propChar = ch; life = seconds; right = facingRight; throwDist = distance; wriggleEvery = every; reveal = revealDistance; start = transform.position;
        var v = new GameObject("Visual"); v.transform.SetParent(transform, false); visual = v.transform;
        sr = v.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = 4;
        if (sprite != null) v.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, sprite.bounds.size.x), size.y / Mathf.Max(0.01f, sprite.bounds.size.y), 1f);
        if (icon != null)
        {
            var ic = new GameObject("Icon"); ic.transform.SetParent(transform, false); ic.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            float s = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.9f, 0.55f, 0.95f); ic.transform.localScale = new Vector3(s, s, 1f);
            var isr = ic.AddComponent<SpriteRenderer>(); isr.sprite = icon; isr.sortingOrder = 5;
        }
    }

    public void Init(SpriteRenderer source, bool facingRight, float seconds, float walkSeconds, float revealDistance)
    {
        life = seconds; walk = walkSeconds; right = facingRight; reveal = revealDistance;
        var v = new GameObject("Visual");
        v.transform.SetParent(transform, false);
        sr = v.AddComponent<SpriteRenderer>();
        if (source != null) { sr.sprite = source.sprite; sr.color = source.color; sr.flipX = source.flipX; sr.sortingOrder = source.sortingOrder; v.transform.localScale = source.transform.lossyScale; v.transform.localPosition = source.transform.localPosition; }
    }

    /// <summary>纯逻辑：马里奥离诱饵这么近时识破。</summary>
    public static bool SeenThrough(Vector2 mario, Vector2 decoy, float revealDistance, bool xray) => xray || (mario - decoy).sqrMagnitude <= revealDistance * revealDistance;

    public void Reveal()
    {
        if (revealed) return;
        revealed = true;
        life = Mathf.Min(life, 0.6f);
        Step1Hint.Show(Step1Text.DecoyRevealed, 1.5f);
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (isProp) { PropUpdate(); return; }
        if (walk > 0f && !revealed)
        {
            walk -= Time.deltaTime; transform.position += new Vector3((right ? 1f : -1f) * 1.5f * Time.deltaTime, 0f, 0f);
            var room = BodyUnstick.RoomSize; // S235：诱饵是画面（没有碰撞体），贴着外墙放会走进墙里 / 走出房间 → 夹在房间里（马里奥追它也不会追到墙外）
            if (room.x > 2 && room.y > 2) transform.position = Step1Bounds.ClampInside(transform.position, room.x, room.y, new Vector2(0.4f, 0f));
        }
        if (revealed && sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Clamp01(life / 0.6f));
        if (life <= 0f) { DecoyAbility.Clear(this); Destroy(gameObject); }
    }

    private void PropUpdate()
    {
        age += Time.deltaTime;
        if (age <= Flight + Time.deltaTime)
        {
            Vector2 p = Step1Loadout.ThrowArc(start, right, throwDist, age, Flight);
            // 别丢进墙里：前面是实心就停在墙前
            var hit = Physics2D.Linecast(start + Vector2.up * 0.3f, p + Vector2.up * 0.3f, LayerMask.GetMask("Ground"));
            if (hit.collider != null) p = new Vector2(hit.point.x - (right ? 0.5f : -0.5f), p.y);
            var room = BodyUnstick.RoomSize;
            if (room.x > 2 && room.y > 2) p = Step1Bounds.ClampInside(p, room.x, room.y, new Vector2(0.4f, 0f));
            transform.position = new Vector3(p.x, p.y, 0f);
        }
        if (visual != null)
        {
            float k = Wriggling && age > Flight ? Mathf.Sin(age * 40f) * 8f : 0f; // 扭 = 左右晃 ±8°
            transform.rotation = Quaternion.Euler(0f, 0f, k);
        }
        if (revealed && sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Clamp01(life / 0.6f));
        if (life <= 0f) { DecoyAbility.Clear(this); Destroy(gameObject); }
    }
}
