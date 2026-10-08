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

    private void Update()
    {
        if (Step1QuickTest.NoLimits) left = Mathf.Max(left, 1); // S236：F9 测试
        if (self == null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f) return;
        if (!Step1Keys.Down(KeyCode.G)) return;
        bool shrunk = TricksterKit.Instance != null && TricksterKit.Instance.Shrunk;
        if (!CanDecoy(self.IsDisguised, shrunk, left, Active != null))
        {
            Step1Hint.Show(self.IsDisguised ? Step1Text.DecoyNeedUndisguise : left <= 0 ? Step1Text.DecoyNone : Step1Text.DecoyBusy);
            return;
        }
        left--;
        var go = new GameObject("TricksterDecoy");
        go.transform.position = transform.position;
        var d = go.AddComponent<Decoy>();
        var sr = self.GetComponentInChildren<SpriteRenderer>();
        d.Init(sr, self.IsFacingRightValue, tuning.decoySeconds, tuning.decoyWalkSeconds, tuning.decoyRevealDistance);
        Active = d;
        Step1Hint.Show(string.Format(Step1Text.DecoyPlaced, left));
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
        if (walk > 0f && !revealed)
        {
            walk -= Time.deltaTime; transform.position += new Vector3((right ? 1f : -1f) * 1.5f * Time.deltaTime, 0f, 0f);
            var room = BodyUnstick.RoomSize; // S235：诱饵是画面（没有碰撞体），贴着外墙放会走进墙里 / 走出房间 → 夹在房间里（马里奥追它也不会追到墙外）
            if (room.x > 2 && room.y > 2) transform.position = Step1Bounds.ClampInside(transform.position, room.x, room.y, new Vector2(0.4f, 0f));
        }
        if (revealed && sr != null) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, Mathf.Clamp01(life / 0.6f));
        if (life <= 0f) { DecoyAbility.Clear(this); Destroy(gameObject); }
    }
}
