using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S200：连锁编排（捣蛋者 F 键）——"以身入局"玩法的核心：**提前布置**一条连锁陷阱，再把马里奥引进来。
/// 用法：走到你的机关旁按 F → 它被编上号（①②③④，最多 maxChainLinks 个）；再按一次 F 取消。
/// 启动连锁（任一）：你按 L 触发了编号里的任意一个 / 马里奥踩到绊线 R。
/// 连锁进行中（chainLiveSeconds 秒，每触发一环就续时）：其余已编号机关会在**马里奥刚好走到时**自动触发——
///   按"他现在的位置 + 速度 × 该机关预警时长"预判落点（ShouldFire），所以火/塌桥/弹簧正好接上。
/// 规则：H3——自动触发走机关自己的预警（OnTricksterActivate → Telegraph），不跳过预警；
///       H4——这是**捣蛋者这边**的工具（读马里奥位置是你的视角），马里奥不会从这里得到任何你的信息；
///       他只能在"亲眼看见机关动了"时起疑（LinkFired → MarioEyes.NotePropActivated，同一视锥规则）；
///       代价——编号时"咔哒"一声（很近才听得见），被他听见会过来查看。
/// 参考：Deception IV（先布置陷阱再引敌人进来，陷阱互相接力成连招）——只借规则，不借素材。
/// </summary>
public class ChainPlan : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private readonly List<ControllablePropBase> links = new List<ControllablePropBase>();
    private readonly HashSet<ControllablePropBase> fired = new HashSet<ControllablePropBase>();
    private TricksterController self;
    private TricksterAbilitySystem abilities;
    private GameManager manager;
    private Transform mario;
    private Rigidbody2D marioBody;
    private float liveUntil = -1f, flashUntil;
    private string flash = "";
    private int step;

    public static ChainPlan Instance { get; private set; }
    public static event System.Action<IControllableProp> LinkFired;
    public static event System.Action<Vector2> Clicked;
    public IReadOnlyList<ControllablePropBase> Links => links;
    public bool Live => Time.time < liveUntil;
    public int Step => step;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        abilities = self != null ? self.AbilitySystem : null;
        if (abilities != null) abilities.OnPropActivated += HandleManualActivate;
        Tripwire.Tripped += HandleTripped;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        var m = FindObjectOfType<MarioController>();
        if (m != null) { mario = m.transform; marioBody = m.GetComponent<Rigidbody2D>(); }
    }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnPropActivated -= HandleManualActivate;
        Tripwire.Tripped -= HandleTripped;
        if (manager != null) manager.OnRoundStart -= ResetRound;
        if (Instance == this) Instance = null;
    }

    private void ResetRound() { links.Clear(); fired.Clear(); liveUntil = -1f; step = 0; }

    // ── 纯逻辑（可测试）────────────────────────────────
    /// <summary>编号/取消：已在表里 → 取消（返回 -1）；不在且没满 → 加到末尾（返回序号 0..）；满了 → 返回 -2。</summary>
    public static int Toggle<T>(List<T> list, T item, int max) where T : class
    {
        int i = list.IndexOf(item);
        if (i >= 0) { list.RemoveAt(i); return -1; }
        if (list.Count >= Mathf.Max(1, max)) return -2;
        list.Add(item); return list.Count - 1;
    }

    /// <summary>预判：马里奥 telegraph 秒后大约在哪；落点离机关 ≤ tolerance（水平）且高度差 ≤ 1.5 → 现在就该触发。</summary>
    public static bool ShouldFire(Vector2 mario, Vector2 velocity, Vector2 trap, float telegraph, float tolerance)
    {
        if (Mathf.Abs(mario.y - trap.y) > 1.6f) return false;
        float predicted = mario.x + velocity.x * Mathf.Max(0f, telegraph);
        // 已经冲过去（背离机关）的不再触发
        if (Mathf.Abs(mario.x - trap.x) > tolerance && Mathf.Sign(trap.x - mario.x) != Mathf.Sign(velocity.x) && Mathf.Abs(velocity.x) > 0.1f) return false;
        return Mathf.Abs(predicted - trap.x) <= tolerance || Mathf.Abs(mario.x - trap.x) <= tolerance * 0.5f;
    }

    /// <summary>大炮：马里奥在炮口前方 range 格内、同一高度（±1）→ 开炮。</summary>
    public static bool ShouldFireCannon(Vector2 mario, Vector2 cannon, bool faceRight, float range)
    {
        float dx = (mario.x - cannon.x) * (faceRight ? 1f : -1f);
        return dx > 0.5f && dx <= range && Mathf.Abs(mario.y - cannon.y) <= 1f;
    }

    /// <summary>
    /// S200 一键布置（Shift+F）：把你身边 range 格内的机关按"离你由近到远"自动编号（最多 max 个）。
    /// 纯逻辑：输入各机关的横坐标，返回编号顺序（下标）。
    /// </summary>
    public static List<int> AutoOrder(IList<Vector2> traps, Vector2 you, float range, int max)
    {
        var idx = new List<int>();
        for (int i = 0; i < traps.Count; i++) if (Vector2.Distance(traps[i], you) <= range) idx.Add(i);
        idx.Sort((a, b) => Vector2.Distance(traps[a], you).CompareTo(Vector2.Distance(traps[b], you)));
        if (idx.Count > max) idx.RemoveRange(max, idx.Count - max);
        return idx;
    }

    private void AutoArrange()
    {
        var all = new List<ControllablePropBase>(); var pos = new List<Vector2>();
        foreach (var p in FindObjectsOfType<ControllablePropBase>()) if (Linkable(p) && p.isActiveAndEnabled) { all.Add(p); pos.Add(p.transform.position); }
        links.Clear(); fired.Clear();
        foreach (int i in AutoOrder(pos, transform.position, tuning.chainAutoRange, tuning.maxChainLinks)) links.Add(all[i]);
        Step1Hint.Show(links.Count > 0 ? string.Format(Step1Text.ChainAuto, links.Count) : Step1Text.ChainNoneNear);
        if (links.Count > 0) Clicked?.Invoke(transform.position);
    }

    /// <summary>可以编号的机关：玩家机关（绳套除外：它的 L 是"上弦/放人"，不适合接力）。</summary>
    public static bool Linkable(ControllablePropBase p) => p != null && !(p is SnareTrap);

    // ── 运行 ───────────────────────────────────────────
    private void Update()
    {
        if (self == null || tuning == null) return;
        links.RemoveAll(l => l == null);
        bool inputOk = !Step1HandsOffCheck.IsRunning && !Step1PlaytestLog.IsTyping && !Step1Screen.HelpOpen && Time.timeScale > 0f;
        if (inputOk && Step1Keys.Down(KeyCode.F)) { if (Step1Keys.Shift()) AutoArrange(); else TryToggleNearest(); }
        if (!Live || mario == null) return;
        Vector2 mp = mario.position, mv = marioBody != null ? marioBody.velocity : Vector2.zero;
        foreach (var l in links)
        {
            if (fired.Contains(l) || !l.CanBeControlled()) continue;
            var cannon = l as PranksterCannon;
            bool go = cannon != null
                ? ShouldFireCannon(mp, l.transform.position, cannon.FacingRight, tuning.chainCannonRange)
                : ShouldFire(mp, mv, l.transform.position, l.GetTelegraphDuration(), tuning.chainFireTolerance);
            if (!go) continue;
            Fire(l, true);
            break; // 一帧只接一环，节奏清楚
        }
        if (fired.Count >= links.Count && links.Count > 0) Finish();
    }

    private void TryToggleNearest()
    {
        ControllablePropBase best = null; float bestD = tuning.chainArmRange;
        foreach (var p in FindObjectsOfType<ControllablePropBase>())
        {
            if (!Linkable(p) || !p.isActiveAndEnabled) continue;
            float d = Vector2.Distance(p.transform.position, transform.position);
            if (d <= bestD) { bestD = d; best = p; }
        }
        if (best == null) { Step1Hint.Show(Step1Text.ChainNoneNear); return; }
        int r = Toggle(links, best, tuning.maxChainLinks);
        if (r == -2) { Step1Hint.Show(string.Format(Step1Text.ChainFull, tuning.maxChainLinks)); return; }
        Step1Hint.Show(r >= 0 ? string.Format(Step1Text.ChainAdded, r + 1, best.PropName) : string.Format(Step1Text.ChainRemoved, best.PropName));
        Clicked?.Invoke(best.transform.position); // 代价：咔哒一声（马里奥很近才听得见）
    }

    private void HandleManualActivate(IControllableProp prop)
    {
        var p = prop as ControllablePropBase;
        if (p == null || !links.Contains(p)) return;
        fired.Add(p); GoLive(); Announce();
    }

    private void HandleTripped(Vector2 at)
    {
        if (links.Count == 0) return;
        GoLive();
        flash = Step1Text.ChainStarted; flashUntil = Time.time + 1.2f;
    }

    private void GoLive() { if (!Live) step = 0; liveUntil = Time.time + tuning.chainLiveSeconds; }

    private void Fire(ControllablePropBase l, bool auto)
    {
        l.OnTricksterActivate(mario != null && mario.position.x < l.transform.position.x ? Vector2.left : Vector2.right);
        fired.Add(l);
        LinkFired?.Invoke(l);
        liveUntil = Time.time + tuning.chainLiveSeconds;
        Announce();
    }

    private void Announce()
    {
        step++;
        flash = $"⛓ 连锁 {step}/{links.Count}";
        flashUntil = Time.time + 1f;
        if (Step1Hitstop.Instance != null) Step1Hitstop.Instance.Request(tuning.chainHitstopSeconds, 0.25f);
    }

    private void Finish()
    {
        if (step >= 3) { flash = Step1Text.ChainPerfect; flashUntil = Time.time + 1.6f; }
        links.RemoveAll(l => fired.Contains(l));
        fired.Clear(); liveUntil = -1f;
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Camera.main == null) return;
        float w = Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        var style = Step1Gui.Text(26, TextAnchor.MiddleCenter, false);
        for (int i = 0; i < links.Count; i++)
        {
            var l = links[i]; if (l == null) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint(l.transform.position + Vector3.up * 1.1f);
            if (sp.z < 0f) continue;
            var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
            string col = fired.Contains(l) ? "#888888" : Live ? "#FF7043" : "#FFD54F";
            GUI.Label(new Rect(at.x - 30, at.y - 20, 60, 40), $"<color={col}><b>{Badge(i)}</b></color>", style);
        }
        if (links.Count > 0)
            GUI.Label(new Rect(w - 380, 110, 360, 30), Live ? $"<color=#FF7043><b>⛓ 连锁进行中</b> {fired.Count}/{links.Count}</color>" : $"<color=#FFD54F>⛓ 连锁已布置 {links.Count} 环（L 或绊线启动）</color>", Step1Gui.Text(18, TextAnchor.MiddleRight, false));
        if (Time.time < flashUntil)
            GUI.Label(new Rect(w * 0.5f - 300, 150, 600, 50), $"<color=#FFB74D><b>{flash}</b></color>", Step1Gui.Text(34, TextAnchor.MiddleCenter, false));
    }

    public static string Badge(int i) => i >= 0 && i < 9 ? ((char)('①' + i)).ToString() : (i + 1).ToString();
}
