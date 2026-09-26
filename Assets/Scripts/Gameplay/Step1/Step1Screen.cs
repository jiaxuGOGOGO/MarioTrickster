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
        HideLegacyHud();
        foreach (var text in FindObjectsOfType<TextMesh>()) Step1Gui.ApplyFont(text);
        var figure = FindObjectOfType<TricksterController>();
        if (figure != null) { figure.SetShowDebugStatus(false); AddYouTag(figure.transform); }
        var scan = FindObjectOfType<ScanAbility>();
        if (scan != null) scan.SetShowScanHints(false);
    }

    private void OnDestroy() { HelpOpen = false; }

    private static void HideLegacyHud()
    {
        var canvas = FindObjectOfType<GlobalGameUICanvas>();
        if (canvas != null) canvas.gameObject.SetActive(false);
        foreach (var hud in FindObjectsOfType<LootEscapeHUD>()) hud.enabled = false;
        foreach (var hud in FindObjectsOfType<SuspicionHUD>()) hud.enabled = false;
    }

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
            if (tuning.showHelpOnStart && !Step1HandsOffCheck.IsRunning) HelpOpen = true;
        }
        if (Step1HandsOffCheck.IsRunning) { HelpOpen = false; return; }

        if (HelpOpen)
        {
            Time.timeScale = 0f; // GameManager.StartGame 会设回 1，这里说明打开期间每帧保持暂停
            if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape)) Close();
            return;
        }
        if (!Step1PlaytestLog.IsTyping && Input.GetKeyDown(KeyCode.H)) HelpOpen = true;
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
            var r = new Rect(w * 0.5f - 600f, h * 0.5f - 380f, 1200f, 760f);
            Step1Gui.Panel(r, 0.92f);
            GUI.Label(new Rect(r.x + 50f, r.y + 36f, r.width - 100f, r.height - 110f), Step1Text.Help, Step1Gui.Text(24));
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
