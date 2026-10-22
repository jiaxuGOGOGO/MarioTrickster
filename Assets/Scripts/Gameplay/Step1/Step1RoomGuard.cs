using UnityEngine;

/// <summary>
/// S235：房间边界守卫（构建器自动挂在 Managers 上，宪法 H9 "无卡死 / 任何状态都有出路"）。
/// 用户实测："捣蛋者掉出游戏范围之外就回不来了，血也没掉，马里奥还在继续进行"。规则全在 Step1Bounds（纯逻辑，沙盒验证）。
///   1. 开局在外圈墙外面再围一圈看不见的墙（Ground 层、不可炸）→ 大炮 / 炸弹 / 被挤也飞不出去；
///   2. 告诉 BodyUnstick 房间多大 → "嵌进墙里推出来"只会往房间里推；
///   3. 每个物理帧检查：你出界 = 回出生点 + 掉 fallOutLivesLost 条命（无敌期内只回出生点）；命没了本局马里奥赢，结算写"你掉出房间"；
///      马里奥出界 = 放回他的路线上（和卡住救援同一套，记一次 stuck_rescues），不改胜负。
/// 裁判规则（同 TricksterLives）：读双方真实位置，但不给马里奥心智任何信息（H4）。
/// </summary>
public class Step1RoomGuard : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    [SerializeField] private int roomWidth, roomHeight;

    private TricksterController figure;
    private TricksterLives lives;
    private MarioController mario;
    private Step1StuckRescue rescue;
    private GameManager manager;
    private GameObject guards;

    public int TricksterFallsThisRound { get; private set; }
    public int MarioFallsThisRound { get; private set; }
    public static Step1RoomGuard Current { get; private set; }

    public void Configure(MarioMindTuningSO t, int w, int h) { tuning = t; roomWidth = w; roomHeight = h; }

    private void Awake()
    {
        Current = this;
        BodyUnstick.RoomSize = new Vector2Int(roomWidth, roomHeight);
        BuildGuards();
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (BodyUnstick.RoomSize == new Vector2Int(roomWidth, roomHeight)) BodyUnstick.RoomSize = Vector2Int.zero;
        if (manager != null) manager.OnRoundStart -= ResetRound;
    }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        figure = FindObjectOfType<TricksterController>();
        lives = FindObjectOfType<TricksterLives>();
        mario = FindObjectOfType<MarioController>();
        rescue = FindObjectOfType<Step1StuckRescue>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
    }

    private void ResetRound() { TricksterFallsThisRound = 0; MarioFallsThisRound = 0; }

    /// <summary>外圈外面四条看不见的墙（只有碰撞体，没有画面；和地面同一层 → 脚下检测、推出墙都认它）。</summary>
    private void BuildGuards()
    {
        if (guards != null || roomWidth <= 2 || roomHeight <= 2) return;
        guards = new GameObject("Step1_RoomGuards");
        guards.transform.SetParent(transform, false);
        int layer = LayerMask.NameToLayer("Ground");
        foreach (var r in Step1Bounds.GuardRects(roomWidth, roomHeight))
        {
            var g = new GameObject("Guard");
            g.transform.SetParent(guards.transform, false);
            g.transform.position = new Vector3(r[0], r[1], 0f);
            if (layer >= 0) g.layer = layer;
            var col = g.AddComponent<BoxCollider2D>();
            col.size = new Vector2(r[2], r[3]);
        }
    }

    private void FixedUpdate()
    {
        if (roomWidth <= 2 || roomHeight <= 2) return;
        CheckTrickster();
        CheckMario();
    }

    private void CheckTrickster()
    {
        if (figure == null || !figure.gameObject.activeInHierarchy) return;
        if (manager != null && manager.CurrentState != GameState.Playing) return;
        if (!Step1Bounds.IsOut(figure.transform.position, roomWidth, roomHeight)) return;
        Vector2 at = figure.transform.position;
        TricksterFallsThisRound++;
        int lost = lives != null ? lives.FellOut(Mathf.Max(0, tuning.fallOutLivesLost)) : 0;
        if (lives == null) Respawn(); // 没有命数系统的场景：至少放回来
        Step1Hint.Show(lost > 0 ? string.Format(Step1Text.FellOutYou, lost) : Step1Text.FellOutYouSafe, 2.5f);
        Step1Fx.Ring(figure.transform.position, 1.2f, new Color(0.45f, 0.7f, 1f, 1f));
        var cam = Step1RoomCamera.Current; if (cam != null) cam.Shake(0.15f, 0.2f);
        Debug.LogWarning($"[Step1 H9] Trickster left the room at ({at.x:F1},{at.y:F1}) -> back to spawn, lives -{lost}. Room {roomWidth}x{roomHeight}.");
    }

    private void Respawn()
    {
        var level = FindObjectOfType<LevelManager>();
        if (level != null && level.TricksterSpawn != null) figure.transform.position = level.TricksterSpawn.position;
        figure.ResetForNewRound();
    }

    private void CheckMario()
    {
        if (mario == null || !mario.gameObject.activeInHierarchy) return;
        if (manager != null && manager.CurrentState != GameState.Playing) return;
        if (!Step1Bounds.IsOut(mario.transform.position, roomWidth, roomHeight)) return;
        Vector2 at = mario.transform.position;
        MarioFallsThisRound++;
        if (rescue != null) rescue.RescueNow(at);
        else
        {
            mario.transform.position = Step1Bounds.ClampInside(at, roomWidth, roomHeight, new Vector2(0.5f, 0.5f));
            var rb = mario.GetComponent<Rigidbody2D>(); if (rb != null) rb.velocity = Vector2.zero;
        }
        Step1Hint.Show(Step1Text.FellOutMario, 2.5f);
        Debug.LogWarning($"[Step1 H9] Mario left the room at ({at.x:F1},{at.y:F1}) -> put back on his route. This is a layout bug; please report (F8).");
    }
}
