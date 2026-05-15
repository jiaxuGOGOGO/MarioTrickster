using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GlobalGameUICanvas — 统一 UGUI HUD 根节点。
///
/// 该组件替代旧的 OnGUI 灰盒 HUD，运行时自动构建 Canvas 子层级，集中显示：
/// GameUI 基础信息、Trickster Heat、Scan Wave、Prop Combo 与 Route Budget。
/// 表现层监听 GameplayEventBus，同时对少量仍未进入事件总线的服务保留只读订阅/快照同步。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasScaler))]
[RequireComponent(typeof(GraphicRaycaster))]
public sealed class GlobalGameUICanvas : MonoBehaviour
{
    private const string CanvasName = "GlobalGameUICanvas";
    private const float AbilityFailDisplayDuration = 2f;
    private const float ComboBreakDisplayDuration = 1.5f;
    private const float ComboHitDisplayDuration = 1.2f;
    private const float ScanHitDisplayDuration = 1.5f;
    private const float LockdownFlashDuration = 2f;

    [Header("=== Runtime References ===")]
    [SerializeField] private PlayerHealth marioHealth;
    [SerializeField] private TricksterHeatMeter heatMeter;
    [SerializeField] private AlarmCrisisDirector crisisDirector;
    [SerializeField] private PropComboTracker comboTracker;
    [SerializeField] private RouteBudgetService routeBudget;
    [SerializeField] private RepeatInterferenceStack repeatStack;
    [SerializeField] private InterferenceCompensationPolicy compensation;
    [SerializeField] private CounterRevealReward counterReward;

    private TricksterController registeredTrickster;

    private Canvas canvas;
    private Font defaultFont;

    private Text healthText;
    private Text timerText;
    private Text roundInfoText;
    private Text noCooldownText;
    private Text controlsText;
    private Text abilityFailText;

    private GameObject pauseOverlay;
    private GameObject gameOverOverlay;
    private Image gameOverBanner;
    private Text gameOverTitleText;
    private Text gameOverScoreText;
    private Text gameOverHintText;

    private Text heatText;
    private Text heatCooldownText;
    private Image heatFillImage;
    private Image lockdownFlashImage;
    private Text lockdownText;

    private GameObject scanWarningPanel;
    private Text scanWarningText;
    private Image scanWarningProgressFill;
    private GameObject scanLinePanel;
    private RectTransform scanLineRect;
    private RectTransform scanGlowRect;
    private Image scanLineImage;
    private Image scanGlowImage;
    private GameObject scanProgressPanel;
    private Image scanProgressFill;
    private Text scanProgressText;
    private Text scanCooldownText;
    private GameObject scanHitPanel;
    private Image scanHitBackground;
    private Text scanHitTitleText;
    private Text scanHitDetailText;

    private GameObject comboPanel;
    private Text comboChainText;
    private Text comboMultiplierText;
    private Image comboWindowFill;
    private Text comboHitText;
    private Text comboBreakText;

    private Text routeBudgetText;

    private float abilityFailTimer;
    private string abilityFailMessage = string.Empty;
    private float gameOverBlinkTimer;
    private bool showGameOver;
    private string gameOverWinner = string.Empty;
    private string gameOverMessage = string.Empty;

    private float lockdownFlashTimer;
    private float comboBreakTimer;
    private int lastComboBreakCount;
    private float comboHitTimer;
    private string lastComboHitMessage = string.Empty;
    private float scanHitTimer;
    private bool scanHitWasReveal;
    private string scanHitTitle = string.Empty;
    private string scanHitDetail = string.Empty;
    private float rescanTimer;

    public static GlobalGameUICanvas EnsureInstance(Transform parent = null)
    {
        GlobalGameUICanvas existing = FindObjectOfType<GlobalGameUICanvas>();
        if (existing != null) return existing;

        GameObject go = new GameObject(CanvasName);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.AddComponent<GlobalGameUICanvas>();
    }

    private void Awake()
    {
        ConfigureCanvas();
        BuildHierarchy();
    }

    private void Start()
    {
        ResolveReferences(true);
        SubscribeToGameManager();
        RegisterAbilityFeedback();
        RefreshHealthText();
        RefreshGameFlowText();
        RefreshHeatFromMeter();
        RefreshRouteBudgetText();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSources();
        GameplayEventBus.OnHeatTierChanged -= HandleHeatTierChanged;
        GameplayEventBus.OnCrisisWarning -= HandleCrisisWarning;
        GameplayEventBus.OnResidueSpotted -= HandleResidueSpotted;
        GameplayEventBus.OnTricksterRevealed -= HandleTricksterRevealed;
    }

    private void OnEnable()
    {
        GameplayEventBus.OnHeatTierChanged += HandleHeatTierChanged;
        GameplayEventBus.OnCrisisWarning += HandleCrisisWarning;
        GameplayEventBus.OnResidueSpotted += HandleResidueSpotted;
        GameplayEventBus.OnTricksterRevealed += HandleTricksterRevealed;
    }

    private void OnDisable()
    {
        GameplayEventBus.OnHeatTierChanged -= HandleHeatTierChanged;
        GameplayEventBus.OnCrisisWarning -= HandleCrisisWarning;
        GameplayEventBus.OnResidueSpotted -= HandleResidueSpotted;
        GameplayEventBus.OnTricksterRevealed -= HandleTricksterRevealed;
    }

    private void Update()
    {
        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f)
        {
            ResolveReferences(false);
            rescanTimer = 1f;
        }

        TickTimers();
        RefreshGameFlowText();
        RefreshAbilityFail();
        RefreshPauseAndGameOver();
        RefreshHeatFromMeter();
        RefreshScanWave();
        RefreshCombo();
        RefreshRouteBudgetText();
    }

    private void ConfigureCanvas()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void BuildHierarchy()
    {
        if (transform.Find("HUDRoot") != null) return;

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null) canvasRect = gameObject.AddComponent<RectTransform>();

        RectTransform root = CreatePanel("HUDRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, false);

        // 基础 GameUI
        healthText = CreateText("HealthText", root, "Mario HP: --", 28, Color.red, TextAnchor.MiddleLeft);
        SetRect(healthText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -32f), new Vector2(460f, 48f), new Vector2(0f, 1f));

        timerText = CreateText("TimerText", root, "00:00", 32, Color.white, TextAnchor.MiddleCenter);
        SetRect(timerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(180f, 54f), new Vector2(0.5f, 1f));

        noCooldownText = CreateText("NoCooldownText", root, "[F9] NO COOLDOWN", 18, new Color(0f, 1f, 0.5f, 0.95f), TextAnchor.MiddleCenter);
        SetRect(noCooldownText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(260f, 34f), new Vector2(0.5f, 1f));

        roundInfoText = CreateText("RoundInfoText", root, "Round --", 24, Color.white, TextAnchor.MiddleRight);
        SetRect(roundInfoText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -34f), new Vector2(620f, 48f), new Vector2(1f, 1f));

        controlsText = CreateText("ControlsText", root, "P1(Mario): WASD + Space | P2(Trickster): Arrows + P/O/I/L\nESC: Pause | F5: Restart", 16, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerLeft);
        SetRect(controlsText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(660f, 80f), new Vector2(0f, 0f));

        abilityFailText = CreateText("AbilityFailText", root, string.Empty, 24, new Color(1f, 0.5f, 0.3f), TextAnchor.MiddleCenter);
        Image abilityBg = abilityFailText.gameObject.AddComponent<Image>();
        abilityBg.color = new Color(0f, 0f, 0f, 0.35f);
        abilityBg.raycastTarget = false;
        abilityBg.enabled = false;
        SetRect(abilityFailText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(560f, 54f), new Vector2(0.5f, 0f));

        BuildPauseOverlay(root);
        BuildGameOverOverlay(root);
        BuildHeatPanel(root);
        BuildScanPanels(root);
        BuildComboPanel(root);
        BuildRoutePanel(root);
    }

    private void BuildPauseOverlay(RectTransform root)
    {
        RectTransform panel = CreatePanel("PauseOverlay", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f), true);
        pauseOverlay = panel.gameObject;
        Text title = CreateText("PauseTitle", panel, "PAUSED", 64, Color.yellow, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 54f), new Vector2(0f, 90f), new Vector2(0.5f, 0.5f));
        Text hint = CreateText("PauseHint", panel, "Press ESC to Resume  |  Press F5 to Restart", 28, Color.white, TextAnchor.MiddleCenter);
        SetRect(hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -20f), new Vector2(0f, 52f), new Vector2(0.5f, 0.5f));
        pauseOverlay.SetActive(false);
    }

    private void BuildGameOverOverlay(RectTransform root)
    {
        RectTransform overlay = CreatePanel("GameOverOverlay", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.75f), true);
        gameOverOverlay = overlay.gameObject;
        RectTransform banner = CreatePanel("WinnerBanner", overlay, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(0f, 170f), new Color(0.8f, 0.2f, 0.2f, 0.6f), true);
        gameOverBanner = banner.GetComponent<Image>();
        gameOverTitleText = CreateText("WinnerText", banner, "", 66, Color.yellow, TextAnchor.MiddleCenter);
        SetRect(gameOverTitleText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        gameOverScoreText = CreateText("ScoreText", overlay, "", 30, Color.white, TextAnchor.MiddleCenter);
        SetRect(gameOverScoreText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -100f), new Vector2(0f, 48f), new Vector2(0.5f, 0.5f));
        gameOverHintText = CreateText("HintText", overlay, "Press  R  to Restart   |   Press  N  for Next Round", 26, Color.white, TextAnchor.MiddleCenter);
        SetRect(gameOverHintText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -152f), new Vector2(0f, 48f), new Vector2(0.5f, 0.5f));
        gameOverOverlay.SetActive(false);
    }

    private void BuildHeatPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel("HeatPanel", root, new Vector2(0f, 0.55f), new Vector2(0f, 0.55f), new Vector2(24f, 0f), new Vector2(250f, 88f), new Color(0f, 0f, 0f, 0.25f), true);
        heatText = CreateText("HeatText", panel, "Heat: --", 20, Color.white, TextAnchor.MiddleLeft);
        SetRect(heatText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -20f), new Vector2(-24f, 30f), new Vector2(0f, 1f));
        RectTransform barBg = CreatePanel("HeatBarBG", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -54f), new Vector2(190f, 18f), new Color(0.15f, 0.15f, 0.15f, 0.9f), true);
        heatFillImage = CreatePanel("HeatBarFill", barBg, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 0f), Color.green, true).GetComponent<Image>();
        heatFillImage.rectTransform.pivot = new Vector2(0f, 0.5f);
        heatCooldownText = CreateText("HeatCooldownText", panel, "[Lockdown cooldown]", 16, Color.yellow, TextAnchor.MiddleLeft);
        SetRect(heatCooldownText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(12f, 12f), new Vector2(-24f, 26f), new Vector2(0f, 0f));

        lockdownFlashImage = CreatePanel("LockdownFlash", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0f, 0f, 0f), true).GetComponent<Image>();
        lockdownText = CreateText("LockdownText", lockdownFlashImage.rectTransform, "LOCKDOWN!", 54, Color.red, TextAnchor.MiddleCenter);
        SetRect(lockdownText.rectTransform, new Vector2(0f, 0.55f), new Vector2(1f, 0.55f), Vector2.zero, new Vector2(0f, 80f), new Vector2(0.5f, 0.5f));
        lockdownFlashImage.gameObject.SetActive(false);
    }

    private void BuildScanPanels(RectTransform root)
    {
        scanWarningPanel = CreatePanel("ScanWarningPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(520f, 92f), Color.clear, false).gameObject;
        scanWarningText = CreateText("ScanWarningText", scanWarningPanel.transform, "", 36, new Color(1f, 0.4f, 0.1f), TextAnchor.MiddleCenter);
        SetRect(scanWarningText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 16f), new Vector2(0f, -26f), new Vector2(0.5f, 0.5f));
        RectTransform warnBg = CreatePanel("WarningProgressBG", scanWarningPanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(260f, 10f), new Color(0.2f, 0.2f, 0.2f, 0.7f), true);
        scanWarningProgressFill = CreatePanel("WarningProgressFill", warnBg, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Color(1f, 0.3f, 0.1f, 0.85f), true).GetComponent<Image>();
        scanWarningProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        scanWarningPanel.SetActive(false);

        scanLinePanel = CreatePanel("ScanLinePanel", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear, false).gameObject;
        scanLineRect = CreatePanel("ScanLine", scanLinePanel.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(3f, 1080f), new Color(0.2f, 0.8f, 1f, 0.75f), true);
        scanLineImage = scanLineRect.GetComponent<Image>();
        scanGlowRect = CreatePanel("ScanGlow", scanLinePanel.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 1080f), new Color(0.2f, 0.8f, 1f, 0.15f), true);
        scanGlowImage = scanGlowRect.GetComponent<Image>();
        scanLinePanel.SetActive(false);

        scanProgressPanel = CreatePanel("ScanProgressPanel", root, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0f, -18f), new Vector2(0f, 28f), Color.clear, false).gameObject;
        RectTransform scanBg = CreatePanel("ScanProgressBG", scanProgressPanel.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -20f), new Color(0.1f, 0.1f, 0.1f, 0.6f), true);
        scanProgressFill = CreatePanel("ScanProgressFill", scanBg, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Color(0.2f, 0.8f, 1f, 0.7f), true).GetComponent<Image>();
        scanProgressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        scanProgressText = CreateText("ScanProgressText", scanProgressPanel.transform, "SCANNING...", 18, new Color(0.2f, 0.8f, 1f), TextAnchor.UpperCenter);
        SetRect(scanProgressText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -20f), Vector2.zero, new Vector2(0.5f, 0.5f));
        scanProgressPanel.SetActive(false);

        scanCooldownText = CreateText("ScanCooldownText", root, "Scan cooldown...", 16, new Color(0.5f, 0.8f, 1f, 0.6f), TextAnchor.MiddleRight);
        SetRect(scanCooldownText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -78f), new Vector2(220f, 30f), new Vector2(1f, 1f));
        scanCooldownText.gameObject.SetActive(false);

        RectTransform hit = CreatePanel("ScanHitPanel", root, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(900f, 100f), new Color(0.25f, 0.85f, 1f, 0.9f), true);
        scanHitPanel = hit.gameObject;
        scanHitBackground = hit.GetComponent<Image>();
        scanHitTitleText = CreateText("ScanHitTitle", hit, "", 36, Color.white, TextAnchor.MiddleCenter);
        SetRect(scanHitTitleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        scanHitDetailText = CreateText("ScanHitDetail", hit, "", 20, Color.white, TextAnchor.MiddleCenter);
        SetRect(scanHitDetailText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        scanHitPanel.SetActive(false);
    }

    private void BuildComboPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel("ComboPanel", root, new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(260f, 126f), Color.clear, false);
        comboPanel = panel.gameObject;
        comboChainText = CreateText("ComboChainText", panel, "", 36, Color.yellow, TextAnchor.MiddleCenter);
        SetRect(comboChainText.rectTransform, new Vector2(0f, 0.64f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        comboMultiplierText = CreateText("ComboMultiplierText", panel, "", 22, new Color(1f, 0.8f, 0.2f), TextAnchor.MiddleCenter);
        SetRect(comboMultiplierText.rectTransform, new Vector2(0f, 0.42f), new Vector2(1f, 0.72f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        RectTransform comboBg = CreatePanel("ComboWindowBG", panel, new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f), Vector2.zero, new Vector2(150f, 10f), new Color(0.2f, 0.2f, 0.2f, 0.8f), true);
        comboWindowFill = CreatePanel("ComboWindowFill", comboBg, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Color.green, true).GetComponent<Image>();
        comboWindowFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        comboHitText = CreateText("ComboHitText", panel, "", 18, Color.white, TextAnchor.MiddleCenter);
        SetRect(comboHitText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        comboBreakText = CreateText("ComboBreakText", panel, "", 30, new Color(1f, 0.3f, 0.3f), TextAnchor.MiddleCenter);
        SetRect(comboBreakText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
        comboPanel.SetActive(false);
    }

    private void BuildRoutePanel(RectTransform root)
    {
        RectTransform panel = CreatePanel("RouteBudgetPanel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -112f), new Vector2(330f, 300f), new Color(0f, 0f, 0f, 0.22f), true);
        routeBudgetText = CreateText("RouteBudgetText", panel, "", 16, new Color(0.9f, 0.9f, 0.9f), TextAnchor.UpperLeft);
        routeBudgetText.horizontalOverflow = HorizontalWrapMode.Wrap;
        routeBudgetText.verticalOverflow = VerticalWrapMode.Overflow;
        SetRect(routeBudgetText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, -12f), new Vector2(-24f, -24f), new Vector2(0f, 1f));
    }

    private void ResolveReferences(bool force)
    {
        bool changed = false;
        if (force || marioHealth == null)
        {
            MarioController mario = FindObjectOfType<MarioController>();
            PlayerHealth found = mario != null ? mario.GetComponent<PlayerHealth>() : FindObjectOfType<PlayerHealth>();
            if (found != marioHealth)
            {
                if (marioHealth != null) marioHealth.OnHealthChanged -= HandleHealthChanged;
                marioHealth = found;
                if (marioHealth != null) marioHealth.OnHealthChanged += HandleHealthChanged;
                changed = true;
            }
        }

        if (force || heatMeter == null)
        {
            TricksterHeatMeter found = FindObjectOfType<TricksterHeatMeter>();
            if (found != heatMeter)
            {
                if (heatMeter != null)
                {
                    heatMeter.OnHeatChanged -= HandleHeatChanged;
                    heatMeter.OnLockdownTriggered -= HandleLockdownTriggered;
                }
                heatMeter = found;
                if (heatMeter != null)
                {
                    heatMeter.OnHeatChanged += HandleHeatChanged;
                    heatMeter.OnLockdownTriggered += HandleLockdownTriggered;
                }
                changed = true;
            }
        }

        if (force || crisisDirector == null)
        {
            AlarmCrisisDirector found = FindObjectOfType<AlarmCrisisDirector>();
            if (found != crisisDirector)
            {
                if (crisisDirector != null) crisisDirector.OnAnchorScanned -= HandleAnchorScanned;
                crisisDirector = found;
                if (crisisDirector != null) crisisDirector.OnAnchorScanned += HandleAnchorScanned;
                changed = true;
            }
        }

        if (force || comboTracker == null)
        {
            PropComboTracker found = FindObjectOfType<PropComboTracker>();
            if (found != comboTracker)
            {
                if (comboTracker != null)
                {
                    comboTracker.OnComboBreak -= HandleComboBreak;
                    comboTracker.OnComboHit -= HandleComboHit;
                }
                comboTracker = found;
                if (comboTracker != null)
                {
                    comboTracker.OnComboBreak += HandleComboBreak;
                    comboTracker.OnComboHit += HandleComboHit;
                }
                changed = true;
            }
        }

        routeBudget = routeBudget != null ? routeBudget : FindObjectOfType<RouteBudgetService>();
        repeatStack = repeatStack != null ? repeatStack : FindObjectOfType<RepeatInterferenceStack>();
        compensation = compensation != null ? compensation : FindObjectOfType<InterferenceCompensationPolicy>();
        counterReward = counterReward != null ? counterReward : FindObjectOfType<CounterRevealReward>();

        if (changed)
        {
            RefreshHealthText();
            RefreshHeatFromMeter();
        }
    }

    private void SubscribeToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTimerUpdated += HandleTimerUpdated;
            GameManager.Instance.OnGameOver += HandleGameOver;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    private void UnsubscribeFromSources()
    {
        if (marioHealth != null) marioHealth.OnHealthChanged -= HandleHealthChanged;
        if (heatMeter != null)
        {
            heatMeter.OnHeatChanged -= HandleHeatChanged;
            heatMeter.OnLockdownTriggered -= HandleLockdownTriggered;
        }
        if (crisisDirector != null) crisisDirector.OnAnchorScanned -= HandleAnchorScanned;
        if (comboTracker != null)
        {
            comboTracker.OnComboBreak -= HandleComboBreak;
            comboTracker.OnComboHit -= HandleComboHit;
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTimerUpdated -= HandleTimerUpdated;
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        if (registeredTrickster != null)
        {
            registeredTrickster.OnAbilityFailed -= ShowAbilityFailFeedback;
            registeredTrickster = null;
        }
    }

    private void RegisterAbilityFeedback()
    {
        TricksterController trickster = FindObjectOfType<TricksterController>();
        if (trickster == null || trickster == registeredTrickster) return;
        if (registeredTrickster != null)
        {
            registeredTrickster.OnAbilityFailed -= ShowAbilityFailFeedback;
        }
        registeredTrickster = trickster;
        registeredTrickster.OnAbilityFailed += ShowAbilityFailFeedback;
    }

    public void ShowAbilityFailFeedback(string reason)
    {
        abilityFailMessage = reason;
        abilityFailTimer = AbilityFailDisplayDuration;
    }

    private void TickTimers()
    {
        if (abilityFailTimer > 0f) abilityFailTimer -= Time.unscaledDeltaTime;
        if (showGameOver) gameOverBlinkTimer += Time.unscaledDeltaTime;
        if (lockdownFlashTimer > 0f) lockdownFlashTimer -= Time.deltaTime;
        if (comboBreakTimer > 0f) comboBreakTimer -= Time.deltaTime;
        if (comboHitTimer > 0f) comboHitTimer -= Time.deltaTime;
        if (scanHitTimer > 0f) scanHitTimer -= Time.deltaTime;
    }

    private void RefreshHealthText()
    {
        if (healthText == null) return;
        if (marioHealth == null)
        {
            healthText.text = "Mario HP: --";
            return;
        }

        StringBuilder hearts = new StringBuilder();
        for (int i = 0; i < marioHealth.MaxHealth; i++) hearts.Append(i < marioHealth.CurrentHealth ? "♥ " : "♡ ");
        healthText.text = $"Mario HP: {hearts}";
    }

    private void RefreshGameFlowText()
    {
        if (GameManager.Instance == null) return;
        float time = GameManager.Instance.GameTimer;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = time < 30f ? Color.red : Color.white;
        roundInfoText.text = $"Round {GameManager.Instance.CurrentRound}  |  Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}";
        noCooldownText.gameObject.SetActive(GameManager.Instance.NoCooldownMode);
    }

    private void RefreshAbilityFail()
    {
        bool visible = abilityFailTimer > 0f && !string.IsNullOrEmpty(abilityFailMessage);
        abilityFailText.gameObject.SetActive(visible);
        if (!visible) return;
        float alpha = Mathf.Clamp01(abilityFailTimer / 0.5f);
        abilityFailText.text = abilityFailMessage;
        abilityFailText.color = new Color(1f, 0.5f, 0.3f, alpha);
        Image bg = abilityFailText.GetComponent<Image>();
        if (bg != null)
        {
            bg.enabled = true;
            bg.color = new Color(0f, 0f, 0f, 0.4f * alpha);
        }
    }

    private void RefreshPauseAndGameOver()
    {
        GameState state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Playing;
        pauseOverlay.SetActive(state == GameState.Paused);
        gameOverOverlay.SetActive(showGameOver);
        if (!showGameOver) return;

        gameOverTitleText.text = gameOverMessage;
        gameOverBanner.color = gameOverWinner == "Mario" ? new Color(0.8f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 0.3f, 0.8f, 0.6f);
        if (GameManager.Instance != null)
        {
            gameOverScoreText.text = $"Score: Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}  |  Round {GameManager.Instance.CurrentRound}";
        }
        float blinkAlpha = Mathf.PingPong(gameOverBlinkTimer * 2f, 1f) * 0.6f + 0.4f;
        gameOverHintText.color = new Color(1f, 1f, 1f, blinkAlpha);
    }

    private void RefreshHeatFromMeter()
    {
        if (heatMeter == null)
        {
            heatText.text = "Heat: --";
            heatFillImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            heatCooldownText.gameObject.SetActive(false);
            return;
        }

        Color tierColor = GetTierColor(heatMeter.CurrentTier);
        heatText.text = $"Heat: {heatMeter.GetTierLabel()} ({heatMeter.Heat:F0}/100)";
        heatText.color = tierColor;
        heatFillImage.color = tierColor;
        float width = 190f * Mathf.Clamp01(heatMeter.HeatNormalized);
        heatFillImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        heatCooldownText.gameObject.SetActive(heatMeter.IsLockdownCooling);

        bool flash = lockdownFlashTimer > 0f;
        lockdownFlashImage.gameObject.SetActive(flash);
        if (flash)
        {
            float alpha = Mathf.Clamp01(lockdownFlashTimer / LockdownFlashDuration) * 0.3f;
            lockdownFlashImage.color = new Color(1f, 0f, 0f, alpha);
        }
    }

    private void RefreshScanWave()
    {
        bool warning = false;
        bool scanning = false;
        bool cooldown = false;

        if (crisisDirector != null)
        {
            warning = crisisDirector.CurrentPhase == AlarmCrisisDirector.ScanPhase.Warning;
            scanning = crisisDirector.CurrentPhase == AlarmCrisisDirector.ScanPhase.Scanning;
            cooldown = crisisDirector.CurrentPhase == AlarmCrisisDirector.ScanPhase.Cooldown;

            if (warning)
            {
                float timer = crisisDirector.PhaseTimer;
                float total = Mathf.Max(0.01f, crisisDirector.WarningDuration);
                float progress = Mathf.Clamp01(1f - timer / total);
                scanWarningText.text = $"SCAN WAVE INCOMING: {timer:F1}s";
                scanWarningText.color = new Color(1f, 0.4f, 0.1f, 0.8f + Mathf.PingPong(Time.time * 4f, 1f) * 0.2f);
                scanWarningProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 260f * progress);
            }

            if (scanning)
            {
                float scanX = crisisDirector.ScanX;
                float denom = crisisDirector.ScanEndX - crisisDirector.ScanStartX;
                float progress = Mathf.Abs(denom) > 0.001f ? (scanX - crisisDirector.ScanStartX) / denom : 1f;
                progress = Mathf.Clamp01(progress);
                scanProgressFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Screen.width * 0.6f * progress);
                UpdateScanLinePosition(scanX);
            }
        }

        scanWarningPanel.SetActive(warning);
        scanLinePanel.SetActive(scanning);
        scanProgressPanel.SetActive(scanning);
        scanCooldownText.gameObject.SetActive(cooldown);
        RefreshScanHitFeedback();
    }

    private void UpdateScanLinePosition(float scanX)
    {
        if (Camera.main == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(scanX, 0f, 0f));
        if (screen.z <= 0f) return;
        scanLineRect.anchoredPosition = new Vector2(screen.x, Screen.height * 0.5f);
        scanGlowRect.anchoredPosition = new Vector2(screen.x, Screen.height * 0.5f);
        scanLineRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
        scanGlowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Screen.height);
        scanLineImage.color = new Color(0.2f, 0.8f, 1f, 0.75f);
        scanGlowImage.color = new Color(0.2f, 0.8f, 1f, 0.15f);
    }

    private void RefreshScanHitFeedback()
    {
        bool visible = scanHitTimer > 0f;
        scanHitPanel.SetActive(visible);
        if (!visible) return;
        float alpha = Mathf.Clamp01(scanHitTimer / ScanHitDisplayDuration * 1.4f);
        Color color = scanHitWasReveal ? new Color(1f, 0.18f, 0.18f, 0.95f) : new Color(0.25f, 0.85f, 1f, 0.9f);
        color.a *= alpha;
        scanHitBackground.color = color;
        scanHitTitleText.text = scanHitTitle;
        scanHitDetailText.text = scanHitDetail;
    }

    private void RefreshCombo()
    {
        bool showBreak = comboBreakTimer > 0f;
        bool showActive = comboTracker != null && comboTracker.IsComboActive;
        comboPanel.SetActive(showBreak || showActive);
        if (!comboPanel.activeSelf) return;

        comboBreakText.gameObject.SetActive(showBreak);
        comboChainText.gameObject.SetActive(!showBreak);
        comboMultiplierText.gameObject.SetActive(!showBreak);
        comboWindowFill.transform.parent.gameObject.SetActive(!showBreak);
        comboHitText.gameObject.SetActive(!showBreak && comboHitTimer > 0f && !string.IsNullOrEmpty(lastComboHitMessage));

        if (showBreak)
        {
            float alpha = Mathf.Clamp01(comboBreakTimer / ComboBreakDisplayDuration);
            comboBreakText.text = $"BREAK! x{lastComboBreakCount}";
            comboBreakText.color = new Color(1f, 0.3f, 0.3f, alpha);
            return;
        }

        float progress = comboTracker.ComboWindowMax > 0f ? comboTracker.ComboTimeRemaining / comboTracker.ComboWindowMax : 0f;
        comboChainText.text = $"CHAIN x{comboTracker.ComboCount}";
        comboMultiplierText.text = $"Multiplier: {comboTracker.CurrentMultiplier:F2}x";
        comboWindowFill.color = progress > 0.3f ? Color.green : Color.red;
        comboWindowFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 150f * Mathf.Clamp01(progress));
        if (comboHitText.gameObject.activeSelf)
        {
            float alpha = Mathf.Clamp01(comboHitTimer / ComboHitDisplayDuration);
            comboHitText.text = lastComboHitMessage;
            comboHitText.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void RefreshRouteBudgetText()
    {
        if (routeBudgetText == null) return;
        StringBuilder sb = new StringBuilder(256);
        sb.AppendLine("=== Route Budget ===");
        if (routeBudget != null)
        {
            var routes = routeBudget.GetAllRoutes();
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                string status = route.Status.ToString();
                if (route.Status != RouteBudgetService.RouteStatus.Available) status += $" ({route.RecoveryTimer:F1}s)";
                sb.AppendLine($"  {route.RouteId}: {status}");
            }
            sb.AppendLine($"  Fallback: {(routeBudget.HasFallbackRoute() ? "YES" : "NO!")}");
        }
        else
        {
            sb.AppendLine("  (not found)");
        }

        sb.AppendLine();
        sb.AppendLine("=== Heat ===");
        if (repeatStack != null)
        {
            sb.AppendLine($"  Heat: {repeatStack.TotalHeat:F0}/100 [{GetLegacyHeatTier(repeatStack.TotalHeat)}]");
        }
        else
        {
            sb.AppendLine("  (not found)");
        }

        sb.AppendLine();
        sb.AppendLine("=== Compensation ===");
        if (compensation != null && compensation.IsProgressBoosted)
        {
            sb.AppendLine($"  Boost: {compensation.CurrentProgressMultiplier}x ({compensation.ProgressBoostRemaining:F1}s)");
        }
        if (counterReward != null)
        {
            if (counterReward.IsRewardBoosted)
            {
                sb.AppendLine($"  Reveal Boost: {counterReward.CurrentRewardMultiplier}x ({counterReward.RewardBoostRemaining:F1}s)");
            }
            sb.AppendLine($"  Counter-Reveals: {counterReward.TotalCounterReveals}");
        }
        routeBudgetText.text = sb.ToString();
    }

    private void HandleHealthChanged(int current, int max) => RefreshHealthText();
    private void HandleTimerUpdated(float timeRemaining) => RefreshGameFlowText();
    private void HandleHeatChanged(float heat, float normalized) => RefreshHeatFromMeter();
    private void HandleLockdownTriggered() => lockdownFlashTimer = LockdownFlashDuration;

    private void HandleGameOver(string winner)
    {
        showGameOver = true;
        gameOverWinner = winner;
        gameOverMessage = winner == "Mario" ? "MARIO WINS!" : "TRICKSTER WINS!";
        gameOverBlinkTimer = 0f;
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Playing) showGameOver = false;
    }

    private void HandleHeatTierChanged(GameplayEventBus.HeatTierChangedPayload payload)
    {
        if (payload != null && payload.heatMeter != null && payload.heatMeter != heatMeter)
        {
            if (heatMeter != null)
            {
                heatMeter.OnHeatChanged -= HandleHeatChanged;
                heatMeter.OnLockdownTriggered -= HandleLockdownTriggered;
            }
            heatMeter = payload.heatMeter;
            heatMeter.OnHeatChanged += HandleHeatChanged;
            heatMeter.OnLockdownTriggered += HandleLockdownTriggered;
        }
        RefreshHeatFromMeter();
    }

    private void HandleCrisisWarning(GameplayEventBus.CrisisWarningPayload payload)
    {
        if (payload != null && payload.director != null && crisisDirector != payload.director)
        {
            if (crisisDirector != null) crisisDirector.OnAnchorScanned -= HandleAnchorScanned;
            crisisDirector = payload.director;
            crisisDirector.OnAnchorScanned += HandleAnchorScanned;
        }
    }

    private void HandleResidueSpotted(GameplayEventBus.ResidueSpottedPayload payload)
    {
        string anchorId = payload != null && payload.anchor != null ? payload.anchor.AnchorId : "Unknown Anchor";
        ShowScanHit(false, "SCAN HIT: EVIDENCE AMPLIFIED", $"Anchor {anchorId} evidence increased.");
    }

    private void HandleTricksterRevealed(GameplayEventBus.TricksterRevealedPayload payload)
    {
        ShowScanHit(true, "SCAN HIT: TRICKSTER REVEALED", "Trickster forced into Revealed/Escaping.");
    }

    private void HandleAnchorScanned(PossessionAnchor anchor, bool wasRevealed)
    {
        string anchorId = anchor != null ? anchor.AnchorId : "Unknown Anchor";
        string title = wasRevealed ? "SCAN HIT: TRICKSTER REVEALED" : "SCAN HIT: EVIDENCE AMPLIFIED";
        string detail = wasRevealed ? $"Anchor {anchorId} forced Trickster into Revealed/Escaping." : $"Anchor {anchorId} evidence increased.";
        ShowScanHit(wasRevealed, title, detail);
    }

    private void ShowScanHit(bool wasReveal, string title, string detail)
    {
        scanHitWasReveal = wasReveal;
        scanHitTitle = title;
        scanHitDetail = detail;
        scanHitTimer = ScanHitDisplayDuration;
    }

    private void HandleComboBreak(int count, float multiplier)
    {
        lastComboBreakCount = count;
        comboBreakTimer = ComboBreakDisplayDuration;
    }

    private void HandleComboHit(int count, float hitMult, bool differentAnchor, bool differentProp)
    {
        if (count <= 1) lastComboHitMessage = "Chain started!";
        else if (differentAnchor && differentProp) lastComboHitMessage = "Different anchor + prop!";
        else if (differentAnchor) lastComboHitMessage = "Different anchor!";
        else if (differentProp) lastComboHitMessage = "Different prop!";
        else lastComboHitMessage = "Same point... suspicion rising!";
        comboHitTimer = ComboHitDisplayDuration;
    }

    private static Color GetTierColor(TricksterHeatMeter.HeatTier tier)
    {
        switch (tier)
        {
            case TricksterHeatMeter.HeatTier.Calm: return new Color(0.3f, 0.8f, 0.3f);
            case TricksterHeatMeter.HeatTier.Suspicious: return new Color(1f, 0.8f, 0.2f);
            case TricksterHeatMeter.HeatTier.Alert: return new Color(1f, 0.5f, 0.1f);
            case TricksterHeatMeter.HeatTier.Lockdown: return new Color(1f, 0.1f, 0.1f);
            default: return Color.white;
        }
    }

    private static string GetLegacyHeatTier(float heat)
    {
        if (heat < 30f) return "Calm";
        if (heat < 60f) return "Suspicious";
        if (heat < 85f) return "Alert";
        return "Lockdown";
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Color color, bool withImage)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        SetRect(rt, anchorMin, anchorMax, anchoredPosition, sizeDelta, new Vector2(0.5f, 0.5f));
        if (withImage)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
        return rt;
    }

    private Text CreateText(string name, Transform parent, string text, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text uiText = go.AddComponent<Text>();
        uiText.font = defaultFont;
        uiText.text = text;
        uiText.fontSize = fontSize;
        uiText.fontStyle = FontStyle.Bold;
        uiText.color = color;
        uiText.alignment = alignment;
        uiText.raycastTarget = false;
        return uiText;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;
    }

    public void OnRestartButtonClicked()
    {
        GameManager.Instance?.RestartLevel();
    }

    public void OnNextRoundButtonClicked()
    {
        GameManager.Instance?.ResetRound();
    }

    public void OnMainMenuButtonClicked()
    {
        GameManager.Instance?.ReturnToMainMenu();
    }
}
