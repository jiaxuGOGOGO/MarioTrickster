using UnityEngine;

/// <summary>
/// S197：马里奥的"时间静止"（AI 技能，时长/冷却/次数全部可调）。
/// 触发（只基于马里奥自己的感知，H4）：他**正在追你且看得见你**，或刚被你坑中（受伤）后起身时；每回合最多 timeStopUsesPerRound 次。
/// 效果：预警 timeStopWarnSeconds（屏幕边缘蓝光 + 字幕"时间要停了！"，H3/H6）→ 捣蛋者冻结 timeStopSeconds 秒（不能动、不能用技能），
///       马里奥照常行动。冻结期间所有机关暂停吗？不——只冻结捣蛋者，机关照常（你之前放的炸弹照样会炸 → 涌现）。
/// 代价 / 反制：预警期间你可以**钻通风管或躲进草丛**（冻结时他仍要看得见你才能抓）。
/// </summary>
public class MarioTimeStop : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private MarioMindDriver driver;
    private TricksterController figure;
    private Rigidbody2D figureBody;
    private GameManager manager;
    private int used;
    private float warn = -1f, freeze = -1f, cooldown;
    public bool Warning => warn > 0f;
    public bool Frozen => freeze > 0f;
    public int UsesLeft => Mathf.Max(0, tuning != null ? tuning.timeStopUsesPerRound + bonus - used : 0);
    private int bonus;
    public void AddUse() { bonus++; cooldown = Mathf.Min(cooldown, 0f); }
    public static MarioTimeStop Instance { get; private set; }

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }
    private void Awake() { Instance = this; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        driver = FindObjectOfType<MarioMindDriver>();
        figure = FindObjectOfType<TricksterController>();
        figureBody = figure != null ? figure.GetComponent<Rigidbody2D>() : null;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        if (driver != null) driver.Hurt += OnHurt;
    }

    private void OnDestroy()
    {
        if (manager != null) manager.OnRoundStart -= ResetRound;
        if (driver != null) driver.Hurt -= OnHurt;
        if (Instance == this) Instance = null;
    }

    private void ResetRound() { used = 0; bonus = 0; warn = freeze = -1f; cooldown = tuning.timeStopFirstDelay; Unfreeze(); }
    private bool hurtRecently;
    private void OnHurt(MarioMindState s) { hurtRecently = true; }

    /// <summary>纯逻辑：这一刻要不要开始时间静止。</summary>
    public static bool ShouldTrigger(bool chasingAndSees, bool hurtRecently, int usesLeft, float cooldown, bool busy) =>
        !busy && usesLeft > 0 && cooldown <= 0f && (chasingAndSees || hurtRecently);

    private void Update()
    {
        if (tuning == null || driver == null || figure == null || Step1HandsOffCheck.IsRunning || UsesLeft <= 0 && warn <= 0f && freeze <= 0f) return;
        if (manager != null && manager.CurrentState != GameState.Playing) { if (freeze > 0f || warn > 0f) { warn = freeze = -1f; Unfreeze(); } return; } // S236：一局在冻住时结束 → 马上解冻（以前要等下一局才解，结算时你一直是冻着的）
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (warn > 0f)
        {
            warn -= Time.deltaTime;
            if (warn <= 0f) { freeze = tuning.timeStopSeconds; Freeze(); }
            return;
        }
        if (freeze > 0f)
        {
            freeze -= Time.deltaTime;
            if (figureBody != null) figureBody.velocity = Vector2.zero;
            if (freeze <= 0f) Unfreeze();
            return;
        }
        bool chasing = driver.Mind.State == MarioMindState.Chasing && driver.LastOrder.seesQuarry &&
                       Vector2.Distance(driver.transform.position, figure.transform.position) <= tuning.timeStopRange;
        bool stunned = driver.Mind.IsStunned;
        if (ShouldTrigger(chasing, hurtRecently && !stunned, UsesLeft, cooldown, driver.IsWaitingToStart))
        {
            used++; hurtRecently = false; cooldown = tuning.timeStopCooldown;
            warn = tuning.timeStopWarnSeconds;
            Step1Hint.Show(Step1Text.TimeStopWarn, tuning.timeStopWarnSeconds + 0.2f);
        }
        if (!stunned) hurtRecently = false;
    }

    private void Freeze()
    {
        if (figure == null) return;
        figure.ApplyKnockbackStun(tuning.timeStopSeconds);
        figure.AbilitySpeedMultiplier = 0f;
        if (figureBody != null) { figureBody.velocity = Vector2.zero; figureBody.isKinematic = true; }
        Step1Hint.Show(Step1Text.TimeStopOn, tuning.timeStopSeconds);
    }

    private void Unfreeze()
    {
        if (figure == null) return;
        if (figureBody != null) figureBody.isKinematic = false;
        figure.AbilitySpeedMultiplier = TricksterKit.Instance != null && TricksterKit.Instance.Shrunk ? tuning.shrinkSpeedMultiplier : 1f;
    }

    private void OnGUI()
    {
        if (!(Warning || Frozen) || Step1HandsOffCheck.IsRunning) return;
        float w = Step1Gui.Begin();
        float h = Step1Gui.VirtualHeight;
        var c = GUI.color;
        GUI.color = new Color(0.4f, 0.7f, 1f, Frozen ? 0.28f : 0.14f + Mathf.PingPong(Time.time * 2f, 0.14f));
        GUI.DrawTexture(new Rect(0, 0, w, 30), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, h - 30, w, 30), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, 30, h), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(w - 30, 0, 30, h), Texture2D.whiteTexture);
        GUI.color = c;
    }
}
