using UnityEngine;

/// <summary>
/// S193：香蕉皮（ASCII 'n'）—— 玩家机关，"打乱落点"的中段招（格斗游戏里的 pushback / 滑步）。
/// 平时是地上一块不起眼的装饰（可穿过、不挡视线、安全）。捣蛋者伪装在旁按 L → 预警（闪黄）→ 激活窗口内踩上去的马里奥
/// 会朝他当前走的方向**滑出去**（空中/地面都失控 slipSeconds 秒），滑行距离可预测 → 把他滑进火/裂缝/封路墙前。
/// 规则约束：
///   - H3：有预警；只在激活窗口内生效，判定框 = 皮本身；
///   - H4：不读取捣蛋者信息；不伤人，只改速度；
///   - H8：滑行距离 = slideSpeed × slipSeconds（测试校验在 2–5 格之间，可读可预判）；
///   - H9：滑行结束后马里奥恢复控制，一次激活只滑一次。
/// 参考：马里奥赛车的香蕉皮（只借"踩到就打滑"的规则，不借素材）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BananaPeel : ControllableLevelElement
{
    [Header("=== 香蕉皮 ===")]
    [Tooltip("滑行速度（格/秒）")]
    [SerializeField] private float slideSpeed = 7f;
    [Tooltip("失控时间（秒）")]
    [SerializeField] private float slipSeconds = 0.5f;

    private bool slippedThisActivation;
    public event System.Action<MarioController> Slipped;

    public void Configure(float speed, float seconds, float telegraph, float activeWindow)
    {
        slideSpeed = speed; slipSeconds = seconds;
        telegraphDuration = Mathf.Max(0.1f, telegraph); activeDuration = Mathf.Max(0.1f, activeWindow);
    }

    protected override void Awake()
    {
        propName = "香蕉皮";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.Interactive | ElementTag.Resettable | ElementTag.AffectsPhysics;
        elementDescription = "捣蛋者按 L 后，踩上去的马里奥会滑出去（打乱落点）";
        base.Awake();
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
    }

    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }
    protected override void OnActivate(Vector2 direction) { slippedThisActivation = false; }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (currentState != PropControlState.Active || slippedThisActivation || other == null) return;
        var mario = other.GetComponentInParent<MarioController>();
        if (mario == null) return;
        var rb = mario.GetComponent<Rigidbody2D>();
        if (rb == null) return;
        slippedThisActivation = true;
        rb.velocity = SlideVelocity(mario.IsFacingRight, slideSpeed);
        mario.ApplyKnockbackStun(slipSeconds);
        Slipped?.Invoke(mario);
        BananaPeelEvents.RaiseSlipped();
    }

    /// <summary>纯计算：滑行速度（略带一点向上，避免卡在地面接缝）。</summary>
    public static Vector2 SlideVelocity(bool facingRight, float speed) => new Vector2((facingRight ? 1f : -1f) * Mathf.Max(0f, speed), 1.5f);

    /// <summary>纯计算：大约滑多远（格）。</summary>
    public static float SlideDistance(float speed, float seconds) => Mathf.Max(0f, speed) * Mathf.Max(0f, seconds);
}
