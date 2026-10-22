using UnityEngine;

/// <summary>
/// S197：限制行动的地形（数据驱动，一个脚本两种用法）：
///   'w' 毒池：踩进去**减速 + 每 poisonTickSeconds 让马里奥晕一小下**（不扣命，H9：一定能走出来；池子不超过 3 格宽，测试校验）。
///            马里奥的寻路把毒池当"能走但尽量绕开"（可达性按能站算，所以不会制造死局）。捣蛋者踩进去同样减速（公平）。
///   'g' 黏胶：踩上去**移动变慢、跳不高**（jumpScale），离开就恢复。适合放在楼梯口前 → 让你的伏击窗口变长。
/// 两者都可站、不是实心、不算危险（不致死），因此可达性/死局分析不受影响；限制只影响"多快"，不影响"能不能"。
/// 参考：Spelunky 的蛛网 / 各类平台游戏的沼泽与粘液（只借规则）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SlowTerrain : LevelElementBase
{
    public enum Kind { Poison, Glue }
    [SerializeField] private Kind kind = Kind.Glue;
    [SerializeField] private float speedScale = 0.5f;
    [SerializeField] private float jumpScale = 0.6f;
    [SerializeField] private float poisonTickSeconds = 1.2f;
    [SerializeField] private float poisonStunSeconds = 0.35f;
    private float tick;

    public Kind TerrainKind => kind;
    public static int MarioInsideCount { get; private set; }
    public static float CurrentMarioSpeedScale { get; private set; } = 1f;
    public static event System.Action MarioPoisoned;

    public void Configure(float speed, float jump, float tickSeconds, float stunSeconds) { speedScale = speed; jumpScale = jump; poisonTickSeconds = tickSeconds; poisonStunSeconds = stunSeconds; }

    private void Awake()
    {
        if (GetComponent<PoisonMarker>() != null || gameObject.name.StartsWith("PoisonPool")) kind = Kind.Poison;
        elementName = kind == Kind.Poison ? "毒池" : "黏胶";
        category = ElementCategory.Misc;
        tags = ElementTag.AffectsPhysics;
        description = kind == Kind.Poison ? "减速并周期性让人晕一下（不致死）" : "减速、跳不高";
        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) { if (other != null && other.GetComponentInParent<MarioController>() != null) { MarioInsideCount++; tick = poisonTickSeconds * 0.5f; } Apply(other, true); }
    private void OnTriggerExit2D(Collider2D other) { if (other != null && other.GetComponentInParent<MarioController>() != null) MarioInsideCount = Mathf.Max(0, MarioInsideCount - 1); Apply(other, false); }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (kind != Kind.Poison || other == null) return;
        var mario = other.GetComponentInParent<MarioController>();
        if (mario == null) return;
        tick -= Time.fixedDeltaTime;
        if (tick > 0f) return;
        tick = poisonTickSeconds;
        mario.ApplyKnockbackStun(poisonStunSeconds);
        MarioPoisoned?.Invoke();
    }

    private void Apply(Collider2D other, bool entering)
    {
        if (other == null) return;
        var t = other.GetComponentInParent<TricksterController>();
        if (t != null) t.AbilitySpeedMultiplier = entering ? speedScale : (TricksterKit.Instance != null && TricksterKit.Instance.Shrunk ? MarioMindTuningSO.LoadOrDefault().shrinkSpeedMultiplier : 1f);
        if (other.GetComponentInParent<MarioController>() != null) CurrentMarioSpeedScale = MarioInsideCount > 0 ? speedScale : 1f;
    }

    public override void OnLevelReset() { MarioInsideCount = 0; CurrentMarioSpeedScale = 1f; }
}

/// <summary>标记组件：这块 SlowTerrain 是毒池（Registry 的 componentTypeNames 挂上，零代码扩展）。</summary>
public class PoisonMarker : MonoBehaviour { }
