using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S202：陷阱试探（"调试关卡 AI 试探模拟"升级）——自动检查的第二种模式。
/// 普通自动检查（H10）让捣蛋者退场，只能证明"没人捣乱时能通关"；它答不了"机关连起来坑他，会不会把他坑死、坑到卡住"。
/// 试探模式：捣蛋者留在原地、**不伪装不移动**（马里奥照常能看见他 → 起疑/追他，这正是最坏情况之一），
/// 本组件扮演"完美时机的对手"：马里奥走到每个机关前（用 ChainPlan.ShouldFire 同一套预判），直接让机关走预警→触发流程。
/// 每个机关每局最多触发 probeUsesPerTrap 次（默认 1）；绳套/铁笼这种控人机关也照触发。
/// 结果（通关 / 被坑倒 / 卡住 / 救援次数 / 被坑次数）写进同一个 step1_handsoff.csv（mode=probe），屏幕同样显示。
/// 规则：H4——这是测试替身，只用马里奥位置（捣蛋者这边的视角），马里奥 AI 一点也拿不到；H3——触发走机关自己的预警。
/// </summary>
public class Step1TrapProbe : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private readonly Dictionary<ControllablePropBase, int> used = new Dictionary<ControllablePropBase, int>();
    private ControllablePropBase[] props = new ControllablePropBase[0];
    private Rigidbody2D body;
    private GameManager manager;
    private float rescan;
    public int FiredThisRound { get; private set; }
    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    /// <summary>纯逻辑：这个机关这一局还能不能被试探触发。</summary>
    public static bool CanProbe(int usedTimes, int maxUses, bool controllable) => controllable && usedTimes < Mathf.Max(1, maxUses);

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        body = GetComponent<Rigidbody2D>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        ResetRound();
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= ResetRound; }
    private void ResetRound() { used.Clear(); FiredThisRound = 0; props = FindObjectsOfType<ControllablePropBase>(); }

    private void Update()
    {
        if (manager != null && manager.CurrentState != GameState.Playing) return;
        rescan -= Time.deltaTime;
        if (rescan <= 0f) { rescan = 2f; props = FindObjectsOfType<ControllablePropBase>(); }
        Vector2 mp = transform.position, mv = body != null ? body.velocity : Vector2.zero;
        foreach (var p in props)
        {
            if (p == null || !p.isActiveAndEnabled || p is SnareTrap) continue;
            used.TryGetValue(p, out int n);
            if (!CanProbe(n, tuning.probeUsesPerTrap, p.CanBeControlled())) continue;
            var cannon = p as PranksterCannon;
            bool go = cannon != null
                ? ChainPlan.ShouldFireCannon(mp, p.transform.position, cannon.FacingRight, tuning.chainCannonRange)
                : ChainPlan.ShouldFire(mp, mv, p.transform.position, p.GetTelegraphDuration(), tuning.chainFireTolerance);
            if (!go) continue;
            p.OnTricksterActivate(mp.x < p.transform.position.x ? Vector2.left : Vector2.right);
            used[p] = n + 1; FiredThisRound++;
            break;
        }
    }

    private void OnGUI()
    {
        if (!Step1HandsOffCheck.ProbeMode) return;
        float w = Step1Gui.Begin();
        GUI.Label(new Rect(20, Step1Gui.VirtualHeight - 60, w - 40, 40), $"<color=#FFD54F>{Step1Text.ProbeBanner}   已触发 {FiredThisRound}</color>", Step1Gui.Text(20, TextAnchor.MiddleLeft, false));
    }
}
