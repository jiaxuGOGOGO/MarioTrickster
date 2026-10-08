using UnityEngine;

/// <summary>
/// S182：第 1 步房间的"干净屏幕"（纯表现，不影响任何玩法/马里奥决策）。
/// - 关掉旧的调试/多系统 HUD（热度、补偿、旧结算横幅、拿宝提示、伪装调试行、[Q] Scan 字样），只留第 1 步需要的。
/// - 世界文字换成能显示中文的字体；捣蛋者头顶标"你 YOU"，一眼分清谁是谁。
/// - 开局显示中英对照的玩法说明并暂停，按任意键开始；H 随时打开/关闭；Esc 暂停提示。
/// </summary>
public class Step1Screen : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;

    public static bool HelpOpen { get; private set; }
    private bool firstFrame = true;
    private GameManager manager;

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        foreach (var text in FindObjectsOfType<TextMesh>()) Step1Gui.ApplyFont(text);
        var figure = FindObjectOfType<TricksterController>();
        if (figure != null) { figure.SetShowDebugStatus(false); AddYouTag(figure.transform); }
        var scan = FindObjectOfType<ScanAbility>();
        if (scan != null) scan.SetShowScanHints(false);
    }

    private void OnDestroy() { HelpOpen = false; }

    private static void AddYouTag(Transform figure)
    {
        var go = new GameObject("Step1_YouTag");
        go.transform.SetParent(figure, false);
        go.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var text = go.AddComponent<TextMesh>();
        text.text = "你 YOU\n▼"; text.fontSize = 48; text.characterSize = 0.045f;
        text.anchor = TextAnchor.LowerCenter; text.alignment = TextAlignment.Center;
        text.color = new Color(0.45f, 0.7f, 1f);
        Step1Gui.ApplyFont(text);
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) r.sortingOrder = 210;
    }

    private void Update()
    {
        if (firstFrame)
        {
            firstFrame = false;
            if (tuning.showHelpOnStart && !Step1QuickTest.On && !Step1HandsOffCheck.IsRunning && !(OverworldSession.Active && OverworldSession.Results.Count > 0)) HelpOpen = true; // S210：小镇一天里第 2 个房间起不再弹说明；S217：快速测试模式不弹
        }
        if (Step1HandsOffCheck.IsRunning) { HelpOpen = false; return; }

        if (HelpOpen)
        {
            Time.timeScale = 0f; // GameManager.StartGame 会设回 1，这里说明打开期间每帧保持暂停
            if (Step1Keys.AnyDown() && !Step1Keys.Down(KeyCode.Escape)) Close(); // S217：两套输入都读（以前只读旧输入 → 有的机器说明关不掉、整局停住）
            return;
        }
        if (!Step1PlaytestLog.IsTyping && Step1Keys.Down(KeyCode.H)) HelpOpen = true;
        ApplyRoomSpeed();
    }

    /// <summary>S226 E7（无障碍：游戏速度）：调参 roomGameSpeed（0.5~1，默认 1 = 不变）。只在正常进行中套用；
    /// 暂停、说明、顿帧、连锁回放各自管 timeScale，结束后回到 1，下一帧这里再套回去。</summary>
    private void ApplyRoomSpeed()
    {
        float sp = MarioMindTuningSO.ClampRoomSpeed(tuning.roomGameSpeed);
        if (sp >= 1f || HelpOpen || ChainReplay.Playing) return;
        if (manager == null || manager.CurrentState != GameState.Playing) return;
        if (Step1Hitstop.Instance != null && Step1Hitstop.Instance.Active) return;
        if (Mathf.Approximately(Time.timeScale, 1f)) Time.timeScale = sp;
    }

    private void Close()
    {
        HelpOpen = false;
        bool paused = manager != null && manager.CurrentState == GameState.Paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    private void OnGUI()
    {
        GUI.depth = -10;
        if (Step1HandsOffCheck.IsRunning) return;
        float w = Step1Gui.Begin();
        float h = Step1Gui.VirtualHeight;

        if (HelpOpen)
        {
            var r = new Rect(w * 0.5f - 620f, h * 0.5f - 470f, 1240f, 940f);
            Step1Gui.Panel(r, 0.92f);
            GUI.Label(new Rect(r.x + 40f, r.y + 28f, r.width - 80f, r.height - 100f), Step1Text.Help, Step1Gui.Text(22));
            GUI.Label(new Rect(r.x, r.yMax - 70f, r.width, 50f), "<b>按任意键开始  Press any key to start</b>",
                Step1Gui.Text(28, TextAnchor.MiddleCenter));
            return;
        }

        if (manager != null && manager.CurrentState == GameState.Paused)
        {
            var r = new Rect(w * 0.5f - 260f, h * 0.5f - 80f, 520f, 160f);
            Step1Gui.Panel(r);
            GUI.Label(r, Step1Text.Paused, Step1Gui.Text(34, TextAnchor.MiddleCenter));
        }
    }
}
