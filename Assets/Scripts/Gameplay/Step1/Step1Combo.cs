using System;
using UnityEngine;

/// <summary>
/// S185：连招计数（纯逻辑，可测试）。两次"坑到马里奥"间隔 ≤ window 秒 → 连招 +1，否则重新从 1 开始。
/// </summary>
public sealed class Step1ComboCounter
{
    private float lastHit = float.NegativeInfinity;
    public float Window { get; set; }
    public int Count { get; private set; }
    public int Max { get; private set; }
    public float LastHitTime => lastHit;

    public Step1ComboCounter(float window) { Window = window; }

    public int Register(float time)
    {
        Count = time - lastHit <= Window ? Count + 1 : 1;
        lastHit = time;
        if (Count > Max) Max = Count;
        return Count;
    }

    public void Reset() { lastHit = float.NegativeInfinity; Count = 0; Max = 0; }
}

/// <summary>
/// S185：第 1 步连招系统（用户反馈"希望陷阱能连招，单个陷阱反制马里奥不丝滑"）。
/// 坑到 = 被机关烧到 / 被机关挡停（封路墙或正在喷的火）/ 掉进坑里。
/// 连招 ≥2：被烧到时额外多晕 comboBonusStunSeconds×(连招-1)，上限 maxStunSeconds；马里奥头顶弹出"连招 x2!"。
/// 只读取马里奥自身状态与机关事件，不给马里奥任何关于捣蛋者的信息（H4 不受影响）。
/// </summary>
public class Step1Combo : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;

    public event Action<int, string> ComboRegistered;
    public Step1ComboCounter Counter { get; private set; }
    public int MaxThisRound => Counter != null ? Counter.Max : 0;

    private MarioMindDriver driver;
    private MarioController mario;
    private GameManager manager;
    private bool wasWaiting, wasInPit;
    private float lastStopAt = float.NegativeInfinity;
    private float floorY = float.NaN;
    private float flashUntil;
    private string flashText = "";

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        Counter = new Step1ComboCounter(tuning.comboWindowSeconds);
        driver = FindObjectOfType<MarioMindDriver>();
        mario = driver != null ? driver.GetComponent<MarioController>() : null;
        if (driver != null) driver.Hurt += HandleHurt;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
    }

    private void OnDestroy()
    {
        if (driver != null) driver.Hurt -= HandleHurt;
        if (manager != null) manager.OnRoundStart -= ResetRound;
    }

    private void ResetRound()
    {
        Counter?.Reset();
        wasWaiting = wasInPit = false; lastStopAt = float.NegativeInfinity; floorY = float.NaN; flashUntil = 0f;
    }

    private int Register(string kind)
    {
        Counter.Window = tuning.comboWindowSeconds;
        int n = Counter.Register(Time.time);
        if (n >= 2) { flashText = $"连招 x{n}!\nCOMBO x{n}!"; flashUntil = Time.time + tuning.comboFlashSeconds; }
        ComboRegistered?.Invoke(n, kind);
        return n;
    }

    private void HandleHurt(MarioMindState state)
    {
        int n = Register("hurt");
        if (n >= 2 && driver != null && driver.Mind != null)
            driver.Mind.ExtendStun(tuning.comboBonusStunSeconds * (n - 1), tuning.maxStunSeconds);
    }

    private void Update()
    {
        if (driver == null || mario == null || manager == null || manager.CurrentState != GameState.Playing) return;
        if (driver.IsWaitingToStart) { floorY = float.NaN; return; }

        var bot = driver.Bot;
        bool waiting = bot != null && bot.IsWaitingForTrap;
        if (waiting && !wasWaiting && Time.time - lastStopAt >= tuning.stopDebounceSeconds) { lastStopAt = Time.time; Register("stop"); }
        wasWaiting = waiting;

        // 掉坑：第一次站稳的高度 = 地面；之后站在比地面低半格以下 = 掉进坑里
        if (mario.IsGrounded)
        {
            float y = mario.transform.position.y;
            if (float.IsNaN(floorY)) floorY = y;
            bool inPit = y < floorY - 0.5f;
            if (inPit && !wasInPit) Register("pit");
            wasInPit = inPit;
        }
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen || Time.time > flashUntil || mario == null || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(mario.transform.position + Vector3.up * 3.2f);
        if (sp.z < 0f) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
        var r = new Rect(at.x - 170f, at.y - 45f, 340f, 90f);
        Step1Gui.Panel(r, 0.7f);
        GUI.Label(r, $"<color=#FFD24A><b>{flashText}</b></color>", Step1Gui.Text(32, TextAnchor.MiddleCenter));
    }
}
