using UnityEngine;

/// <summary>
/// S196：捣蛋者新技能"砸墙"（B 键）—— 贴着裂墙 '%' 按 B 把墙砸开。
/// 代价（宪法：每个能力都要有代价）：
///   - 每回合次数有限（wallSmashesPerRound），有冷却；
///   - 必须**不伪装**（现形状态）才能砸；砸墙会发出**响声**：马里奥听得见（声音范围内，隔墙也听得见——声音不是视线），
///     他会起疑并过来查看 → "为了开捷径暴露自己"是一个真正的取舍；
///   - 砸的瞬间短暂硬直（smashRecoverSeconds），不能立刻跑。
/// 规则：H4 马里奥只得到"声音位置"；H6 屏幕字幕"哐！"；数值全在 RushMarioTuning。
/// </summary>
public class WallSmashAbility : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private TricksterController self;
    private int usedThisRound;
    private float cooldown, recover;
    private static readonly Collider2D[] s_hits = new Collider2D[8];
    private GameManager manager;
    public int UsesLeft => Mathf.Max(0, (tuning != null ? tuning.wallSmashesPerRound : 0) - usedThisRound);
    public float Cooldown => Mathf.Max(0f, cooldown);
    public bool Recovering => recover > 0f;
    public static event System.Action<Vector2> SmashNoise;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= ResetRound; }
    private void ResetRound() { usedThisRound = 0; cooldown = 0f; recover = 0f; }

    private void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (recover > 0f) recover -= Time.deltaTime;
        if (Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen) return;
        if (Input.GetKeyDown(KeyCode.B)) TrySmash();
    }

    /// <summary>纯逻辑：能不能砸（供测试）。</summary>
    public static bool CanSmash(bool disguised, int usesLeft, float cooldown) => !disguised && usesLeft > 0 && cooldown <= 0f;

    public bool TrySmash()
    {
        if (self == null || tuning == null) return false;
        if (!CanSmash(self.IsDisguised, UsesLeft, cooldown)) { Step1Hint.Show(self.IsDisguised ? Step1Text.SmashNeedUndisguise : Step1Text.SmashNotReady); return false; }
        Vector2 pos = transform.position;
        int n = Physics2D.OverlapCircleNonAlloc(pos, tuning.wallSmashReach, s_hits);
        CrackedWall best = null; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var w = s_hits[i] != null ? s_hits[i].GetComponentInParent<CrackedWall>() : null;
            if (w == null || w.Broken) continue;
            float d = Vector2.Distance(pos, w.transform.position);
            if (d < bestD) { bestD = d; best = w; }
        }
        if (best == null) { Step1Hint.Show(Step1Text.SmashNoWall); return false; }
        best.Break();
        usedThisRound++;
        cooldown = tuning.wallSmashCooldown;
        recover = tuning.wallSmashRecoverSeconds;
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
        self.Launch(Vector2.zero, tuning.wallSmashRecoverSeconds); // 复用"不受控"通道：短暂硬直
        SmashNoise?.Invoke(best.transform.position);
        return true;
    }
}

/// <summary>S196：屏幕底部的一行提示（砸墙失败原因、听见响声等）。H6：没有声音也看得见。</summary>
public class Step1Hint : MonoBehaviour
{
    private static string text = "";
    private static float until;
    public static void Show(string msg, float seconds = 1.6f) { text = msg; until = Time.unscaledTime + seconds; }

    private void OnGUI()
    {
        if (Time.unscaledTime > until || string.IsNullOrEmpty(text) || Step1HandsOffCheck.IsRunning) return;
        float w = Step1Gui.Begin();
        var r = new Rect(w * 0.5f - 360f, Step1Gui.VirtualHeight - 170f, 720f, 60f);
        Step1Gui.Panel(r, 0.65f);
        GUI.Label(r, text, Step1Gui.Text(26, TextAnchor.MiddleCenter));
    }
}
