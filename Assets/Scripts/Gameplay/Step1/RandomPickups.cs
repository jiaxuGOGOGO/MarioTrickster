using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S198：随机道具箱（反转变数 / 涌现）。每回合开局在房间里若干个"道具点"（ASCII '?'）里随机刷出 pickupsPerRound 个道具，
/// **谁先碰到归谁**（马里奥和捣蛋者都能捡），效果立即生效：
///   - 给捣蛋者：+1 炸弹 / +1 缩小 / 刷新大炮冷却 / 8 秒"隐身"（马里奥看不见站着不动的你，移动时照样看得见）；
///   - 给马里奥：+1 次时间静止 / 8 秒"加速" / 下一次被坑免疫（护盾）/ 5 秒"透视"（看穿伪装）。
/// 反转：同一个箱子，你捡到是好东西，他捡到可能让局势翻盘 → 双方都要抢（Mario Kart 道具箱的规则，只借规则）。
/// 规则：H4——马里奥 AI 只会"路过顺手捡"（道具点在他路线 2 格内才绕），不会读取你的位置；H3——箱子一直可见、捡到时全屏字幕；
///       数值全部在 RushMarioTuning；种子 = 回合种子（可复现）。
/// </summary>
public class RandomPickups : MonoBehaviour
{
    public enum Kind { Bomb, Shrink, CannonReady, Invisible, TimeStop, Speed, Shield, XRay }
    public static readonly Kind[] ForTrickster = { Kind.Bomb, Kind.Shrink, Kind.CannonReady, Kind.Invisible };
    public static readonly Kind[] ForMario = { Kind.TimeStop, Kind.Speed, Kind.Shield, Kind.XRay };

    [SerializeField] private MarioMindTuningSO tuning;
    private GameManager manager;
    private readonly List<PickupSpot> spots = new List<PickupSpot>();
    public static float TricksterInvisibleUntil, MarioSpeedUntil, MarioXRayUntil;
    public static bool MarioShield;
    public static string LastPickup { get; private set; } = "";

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += Roll;
        spots.AddRange(FindObjectsOfType<PickupSpot>());
        spots.Sort((a, b) => a.transform.position.x != b.transform.position.x ? a.transform.position.x.CompareTo(b.transform.position.x) : a.transform.position.y.CompareTo(b.transform.position.y));
        Roll();
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= Roll; }

    /// <summary>纯逻辑：本回合哪些道具点亮起（下标）与各自内容。可复现。</summary>
    public static List<(int spot, int kind)> Pick(int seed, int spotCount, int count)
    {
        var rng = new System.Random(seed * 31 + 7);
        var order = new List<int>(); for (int i = 0; i < spotCount; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        var result = new List<(int, int)>();
        for (int i = 0; i < Mathf.Min(count, spotCount); i++) result.Add((order[i], rng.Next(8)));
        return result;
    }

    private void Roll()
    {
        TricksterInvisibleUntil = MarioSpeedUntil = MarioXRayUntil = 0f; MarioShield = false; LastPickup = "";
        foreach (var s in spots) s.SetLive(false);
        if (tuning.pickupsPerRound <= 0 || spots.Count == 0) return;
        var driver = FindObjectOfType<MarioMindDriver>();
        int seed = driver != null ? driver.RoundSeed : Random.Range(0, 100000);
        foreach (var (spot, kind) in Pick(seed, spots.Count, tuning.pickupsPerRound)) spots[spot].SetLive(true, kind);
    }

    /// <summary>道具的名字：捡到的人不同，同一个箱子效果不同（kind 0..7 → 给捣蛋者 kind%4 / 给马里奥 kind%4）。</summary>
    public static Kind Resolve(int kind, bool trickster) => trickster ? ForTrickster[kind % 4] : ForMario[kind % 4];

    public static void Grant(int kind, bool trickster, MarioMindTuningSO t)
    {
        var k = Resolve(kind, trickster);
        float now = Time.time;
        switch (k)
        {
            case Kind.Bomb: TricksterKit.Instance?.AddBombs(1); break;
            case Kind.Shrink: TricksterKit.Instance?.AddShrinks(1); break;
            case Kind.CannonReady: foreach (var c in PranksterCannon.All) c.ResetLaunchCooldown(); break;
            case Kind.Invisible: TricksterInvisibleUntil = now + t.pickupEffectSeconds; break;
            case Kind.TimeStop: MarioTimeStop.Instance?.AddUse(); break;
            case Kind.Speed: MarioSpeedUntil = now + t.pickupEffectSeconds; break;
            case Kind.Shield: MarioShield = true; break;
            case Kind.XRay: MarioXRayUntil = now + t.pickupEffectSeconds * 0.6f; break;
        }
        LastPickup = (trickster ? "T:" : "M:") + k;
        Step1Hint.Show((trickster ? "你捡到了 " : "马里奥捡到了 ") + Name(k), 2.5f);
    }

    public static string Name(Kind k)
    {
        switch (k)
        {
            case Kind.Bomb: return "💣 +1 炸弹";
            case Kind.Shrink: return "🔻 +1 缩小";
            case Kind.CannonReady: return "🎯 大炮冷却刷新";
            case Kind.Invisible: return "👻 隐身（站着不动时看不见）";
            case Kind.TimeStop: return "⏳ +1 时间静止";
            case Kind.Speed: return "👟 加速";
            case Kind.Shield: return "🛡 护盾（下一次被坑免疫）";
            default: return "👁 透视（看穿伪装）";
        }
    }
}

/// <summary>S198：道具点（ASCII '?'）。亮起时显示问号方块；谁先碰到归谁，本回合只能捡一次。</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class PickupSpot : LevelElementBase
{
    private bool live;
    private int kind;
    private static readonly List<PickupSpot> all = new List<PickupSpot>();
    /// <summary>S202：所有道具点（马里奥眼睛只看亮着的）。</summary>
    public static IReadOnlyList<PickupSpot> All => all;
    protected override void OnEnable() { base.OnEnable(); if (!all.Contains(this)) all.Add(this); }
    protected override void OnDisable() { base.OnDisable(); all.Remove(this); }
    private Transform visual;
    public bool Live => live;

    private void Awake()
    {
        elementName = "道具箱";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive | ElementTag.Resettable;
        description = "随机道具：你和马里奥谁先碰到归谁，同一个箱子对两人效果不同";
        GetComponent<BoxCollider2D>().isTrigger = true;
        visual = transform.Find("Visual");
    }

    public void SetLive(bool on, int k = 0)
    {
        live = on; kind = k;
        if (visual != null) visual.gameObject.SetActive(on);
    }

    private void Update()
    {
        if (live && visual != null) visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.08f, 0f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!live || other == null) return;
        bool t = other.GetComponentInParent<TricksterController>() != null;
        bool m = !t && other.GetComponentInParent<MarioController>() != null;
        if (!t && !m) return;
        SetLive(false);
        RandomPickups.Grant(kind, t, MarioMindTuningSO.LoadOrDefault());
    }

    public override void OnLevelReset() { }
}
