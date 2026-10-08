using UnityEngine;

/// <summary>
/// S199：铁笼（ASCII 'Q'）—— 监狱题材的"抓投"控制技。平时笼子悬在头顶（看得见，H3）。
/// 捣蛋者伪装在旁按 L → 预警（笼子晃 + 闪）→ 落下：正下方 1 格宽内的马里奥被关 cageSeconds 秒（默认 3，可配置），然后**自动打开**（H9）。
/// 被关期间马里奥不能动，但**起疑值上升**（他知道有人在搞鬼）；笼子能被炸弹/油桶炸开（提前放人——炸他自己也会伤到他）。
/// 一回合每个笼子用一次（本回合落下后不再回升），回合重置复原。
/// 规则：H4——只判"笼子正下方有没有人"，不读捣蛋者信息；你自己站在下面也会被关（公平）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class IronCage : ControllableLevelElement
{
    [SerializeField] private float holdSeconds = 3f;
    [SerializeField] private float dropHeight = 2f;
    private Transform visual;
    private Vector3 visualHome;
    private Transform prisoner;
    private float timer;
    private bool used;
    private static readonly Collider2D[] s_hits = new Collider2D[8];
    public bool Holding => prisoner != null;
    public bool Used => used;
    public static event System.Action<MarioController> MarioCaged;

    public void Configure(float seconds) { holdSeconds = Mathf.Max(0.5f, seconds); }

    protected override void Awake()
    {
        propName = "铁笼";
        elementCategory = ElementCategory.Trap;
        elementTags = ElementTag.Controllable | ElementTag.OneShot | ElementTag.Resettable;
        elementDescription = "按 L 从头顶落下，关住下面的人 3 秒后自动打开";
        base.Awake();
        GetComponent<BoxCollider2D>().isTrigger = true;
        visual = transform.Find("Visual");
        if (visual != null) { visualHome = new Vector3(0f, dropHeight, 0f); visual.localPosition = visualHome; }
    }

    protected override bool ExtraControlCondition() => !used;
    protected override void OnTelegraphStart() { }
    protected override void OnTelegraphEnd() { }
    protected override void OnActiveEnd() { }

    protected override void OnActivate(Vector2 direction)
    {
        if (used) return;
        used = true;
        dropT = 0f; // S216：笼子加速落下（0.12 秒），不是瞬移
        int n = Physics2D.OverlapBoxNonAlloc(transform.position, new Vector2(0.9f, 1f), 0f, s_hits);
        for (int i = 0; i < n; i++)
        {
            var h = s_hits[i];
            if (h == null || h.attachedRigidbody == null) continue;
            var mario = h.GetComponentInParent<MarioController>();
            var figure = mario == null ? h.GetComponentInParent<TricksterController>() : null;
            if (mario == null && figure == null) continue;
            prisoner = mario != null ? mario.transform : figure.transform;
            timer = holdSeconds;
            if (mario != null) { mario.ApplyKnockbackStun(holdSeconds); MarioCaged?.Invoke(mario); Step1Hint.Show(Step1Text.CageMario, 2f); }
            else { figure.ApplyKnockbackStun(holdSeconds); Step1Hint.Show(Step1Text.CageYou, 2f); }
            break;
        }
    }

    private float dropT = -1f;
    private const float DropTime = 0.12f;
    protected override void Update()
    {
        base.Update();
        if (dropT < 0f || visual == null) return;
        dropT += Time.deltaTime;
        visual.localPosition = Vector3.Lerp(visualHome, Vector3.zero, Step1Feel.DropProgress(dropT, DropTime));
        if (dropT >= DropTime)
        {
            dropT = -1f;
            Step1Fx.Dust((Vector2)transform.position + Vector2.down * 0.5f, 1.2f);
            var cam = Step1RoomCamera.Current; if (cam != null) cam.Shake(0.12f, 0.15f);
        }
    }

    private void FixedUpdate()
    {
        if (prisoner == null) return;
        timer -= Time.fixedDeltaTime;
        if (Step1Bounds.Teleported(prisoner.position, transform.position, 1.5f)) { Release(); return; } // S235：被传送走（回出生点 / 救援）= 放人，不再拽回笼子
        var p = prisoner.position; p.x = transform.position.x; prisoner.position = p; // 关在笼里：水平锁定
        var rb = prisoner.GetComponent<Rigidbody2D>(); if (rb != null) rb.velocity = new Vector2(0f, Mathf.Min(0f, rb.velocity.y));
        if (timer <= 0f) Release();
    }

    /// <summary>被炸开：立即放人，笼子毁掉。</summary>
    public void BreakOpen() { Release(); if (visual != null) visual.gameObject.SetActive(false); used = true; }

    private void Release() { prisoner = null; if (visual != null && visual.gameObject.activeSelf) visual.localPosition = Vector3.zero; }

    public override void OnLevelReset()
    {
        base.OnLevelReset();
        prisoner = null; used = false; dropT = -1f;
        if (visual != null) { visual.gameObject.SetActive(true); visual.localPosition = visualHome; }
    }
}
