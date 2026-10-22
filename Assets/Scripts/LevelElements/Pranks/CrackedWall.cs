using UnityEngine;

/// <summary>
/// S196：裂墙（ASCII '%'）—— 可破坏墙体。平时是实心墙（挡路、挡视线）。
/// 打破方式（本回合不复原）：
///   1. 捣蛋者的炸弹（B 键，3 枚，会发出**响声**——马里奥听得见，H6 有字幕）；
///   2. 被大炮炮弹打中；
///   3. 马里奥被弹簧板/香蕉皮"撞"过去时的速度足够大（环境连锁 → 涌现）。
/// 箱庭意义：隐藏通路 / 秘密捷径（Dark Souls 的"打墙"、Undead Burg 被木桶挡住的暗道）。
/// 规则：
///   - H1：死局检查把裂墙当"墙"（最坏情况 = 永远不破）；它只会**多开路**，不会堵路 → 不会制造死局；
///   - H4：墙不读捣蛋者信息；马里奥"听见响声"只得到声音位置，和看见草丛晃动同一通道；
///   - H6：碎墙时屏幕显示"哐！"字幕与震屏。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CrackedWall : LevelElementBase
{
    [Tooltip("被撞碎所需速度（格/秒）。弹簧/香蕉皮/人肉炮弹把人撞过来才够")]
    [SerializeField] private float breakSpeed = 9f;

    private BoxCollider2D body;
    private Transform visual;
    private bool broken;
    public bool Broken => broken;
    /// <summary>碎墙事件（位置）。马里奥"听见"它（MarioEyes.NoteNoise）；连招/试玩记录也订阅。</summary>
    public static event System.Action<Vector2> Smashed;

    public void Configure(float speed) { breakSpeed = speed; }

    private void Awake()
    {
        elementName = "裂墙";
        category = ElementCategory.Misc;
        tags = ElementTag.Interactive | ElementTag.OneShot | ElementTag.Resettable | ElementTag.AffectsPhysics;
        description = "可以被砸开 / 炮弹打开 / 高速撞开的墙；本回合不复原";
        body = GetComponent<BoxCollider2D>();
        visual = transform.Find("Visual");
    }

    /// <summary>开局就塌（随机事件）：不发出声音、不计入连招。</summary>
    public void BreakSilently()
    {
        if (broken) return;
        broken = true;
        if (body != null) body.enabled = false;
        if (visual != null) visual.gameObject.SetActive(false);
    }

    public void Break()
    {
        if (broken) return;
        broken = true;
        if (body != null) body.enabled = false;
        if (visual != null) visual.gameObject.SetActive(false);
        Smashed?.Invoke(transform.position);
        Step1Hint.Show(Step1Text.HeardSmash, 1.2f);
        var cam = FindObjectOfType<Step1RoomCamera>();
        if (cam != null) cam.Shake(0.25f, 0.3f);
    }

    private void OnCollisionEnter2D(Collision2D c)
    {
        if (broken || c == null) return;
        if (c.collider.GetComponentInParent<CannonBall>() != null) { Break(); return; }
        if (c.relativeVelocity.magnitude >= breakSpeed &&
            (c.collider.GetComponentInParent<MarioController>() != null || c.collider.GetComponentInParent<TricksterController>() != null))
            Break();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!broken && other != null && other.GetComponentInParent<CannonBall>() != null) Break();
    }

    public override void OnLevelReset()
    {
        broken = false;
        if (body != null) body.enabled = true;
        if (visual != null) visual.gameObject.SetActive(true);
    }
}
