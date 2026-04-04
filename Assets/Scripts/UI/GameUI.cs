using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏HUD界面 - 增强版
/// 功能: 显示Mario生命值、计时器、回合信息、胜负结果、暂停/恢复反馈、道具操控反馈
/// 使用方式: 挂载到Canvas上，在Inspector中拖入UI元素引用
/// 
/// 如果场景中没有对应的UI元素，脚本会用OnGUI作为后备显示
/// 
/// Bug修复记录:
///   B011 - 游戏结束画面增强：全屏遮罩 + 大号胜利文字 + 闪烁提示 + Input.GetKeyDown 检测
///   B014 - 暂停反馈：暂停时显示半透明遮罩（恢复提示已根据用户反馈移除）
///   B012 - 道具操控失败反馈：屏幕下方显示失败原因提示（自动消失）
///   B024 - 计时器显示区域加宽，防止文字被裁剪
/// 
/// Session 18 性能优化:
///   - 所有 GUIStyle 缓存为类字段，消除 OnGUI 每帧 new GUIStyle 的 GC 分配
///   - GUIStyle 在首次 OnGUI 调用时惰性初始化（GUI.skin 只在 OnGUI 内有效）
///   - 减少不必要的 GUI.DrawTexture 调用
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("=== UI 引用（可选，没有则用OnGUI后备）===")]
    [SerializeField] private Text healthText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text roundInfoText;
    [SerializeField] private Text gameOverText;
    [SerializeField] private GameObject gameOverPanel;

    [Header("=== 引用 ===")]
    [SerializeField] private PlayerHealth marioHealth;

    // 内部状态
    private bool useOnGUIFallback = true;
    private string gameOverMessage = "";
    private bool showGameOver = false;
    private string gameOverWinner = "";

    // B012: 道具操控失败反馈
    private string abilityFailMessage = "";
    private float abilityFailTimer;
    private const float AbilityFailDisplayDuration = 2f;

    // B011: 游戏结束闪烁效果
    private float gameOverBlinkTimer;

    // ── Session 18 性能优化：缓存 GUIStyle ──
    private bool stylesInitialized;
    private GUIStyle cachedLabelStyle;
    private GUIStyle cachedBigLabelStyle;
    private GUIStyle cachedNoCdStyle;
    private GUIStyle cachedControlStyle;
    private GUIStyle cachedPauseTitleStyle;
    private GUIStyle cachedPauseHintStyle;
    private GUIStyle cachedWinnerStyle;
    private GUIStyle cachedScoreStyle;
    private GUIStyle cachedHintStyle;
    private GUIStyle cachedFailStyle;

    private void Start()
    {
        // 检查是否有UI引用
        useOnGUIFallback = (healthText == null && timerText == null);

        // 自动查找Mario的Health
        if (marioHealth == null)
        {
            MarioController mario = FindObjectOfType<MarioController>();
            if (mario != null)
            {
                marioHealth = mario.GetComponent<PlayerHealth>();
            }
        }

        // 注册事件
        if (marioHealth != null)
        {
            marioHealth.OnHealthChanged += UpdateHealthDisplay;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTimerUpdated += UpdateTimerDisplay;
            GameManager.Instance.OnGameOver += ShowGameOverScreen;
            GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
        }

        // 注册 Trickster 能力系统的失败反馈
        RegisterAbilityFeedback();

        // 隐藏游戏结束面板
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (marioHealth != null)
        {
            marioHealth.OnHealthChanged -= UpdateHealthDisplay;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnTimerUpdated -= UpdateTimerDisplay;
            GameManager.Instance.OnGameOver -= ShowGameOverScreen;
            GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
        }
    }

    private void Update()
    {
        // 道具操控失败提示倒计时
        if (abilityFailTimer > 0f)
        {
            abilityFailTimer -= Time.unscaledDeltaTime;
        }

        // 游戏结束闪烁计时
        if (showGameOver)
        {
            gameOverBlinkTimer += Time.unscaledDeltaTime;
        }
    }

    #region 道具操控失败反馈 (B012)

    /// <summary>注册 Trickster 能力系统的反馈事件</summary>
    private void RegisterAbilityFeedback()
    {
        TricksterAbilitySystem abilitySystem = FindObjectOfType<TricksterAbilitySystem>();
        if (abilitySystem != null)
        {
            // 通过 TricksterController 的 OnAbilityFailed 事件获取失败信息
            TricksterController trickster = FindObjectOfType<TricksterController>();
            if (trickster != null)
            {
                trickster.OnAbilityFailed += ShowAbilityFailFeedback;
            }
        }
    }

    /// <summary>显示道具操控失败提示</summary>
    public void ShowAbilityFailFeedback(string reason)
    {
        abilityFailMessage = reason;
        abilityFailTimer = AbilityFailDisplayDuration;
    }

    #endregion

    #region UI 更新

    private void UpdateHealthDisplay(int current, int max)
    {
        if (healthText != null)
        {
            healthText.text = $"HP: {current}/{max}";
        }
    }

    private void UpdateTimerDisplay(float timeRemaining)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void ShowGameOverScreen(string winner)
    {
        showGameOver = true;
        gameOverWinner = winner;
        gameOverMessage = winner == "Mario" ? "MARIO WINS!" : "TRICKSTER WINS!";
        gameOverBlinkTimer = 0f;

        if (gameOverText != null)
        {
            gameOverText.text = gameOverMessage;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Debug.Log($"[GameUI] {gameOverMessage}");
    }

    private void OnGameStateChanged(GameState newState)
    {
        if (newState == GameState.Playing)
        {
            showGameOver = false;
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }
    }

    #endregion

    #region OnGUI 后备显示（无Canvas时使用）

    /// <summary>
    /// Session 18 性能优化：惰性初始化所有 GUIStyle（只在首次 OnGUI 调用时创建一次）
    /// GUI.skin 只在 OnGUI 回调内有效，所以不能在 Awake/Start 中初始化
    /// </summary>
    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        cachedLabelStyle = new GUIStyle(GUI.skin.label);
        cachedLabelStyle.fontSize = 20;
        cachedLabelStyle.fontStyle = FontStyle.Bold;

        cachedBigLabelStyle = new GUIStyle(GUI.skin.label);
        cachedBigLabelStyle.fontSize = 36;
        cachedBigLabelStyle.fontStyle = FontStyle.Bold;
        cachedBigLabelStyle.alignment = TextAnchor.MiddleCenter;

        cachedNoCdStyle = new GUIStyle(GUI.skin.label);
        cachedNoCdStyle.fontSize = 14;
        cachedNoCdStyle.fontStyle = FontStyle.Bold;
        cachedNoCdStyle.alignment = TextAnchor.MiddleCenter;
        cachedNoCdStyle.normal.textColor = new Color(0f, 1f, 0.5f, 0.9f);

        cachedControlStyle = new GUIStyle(GUI.skin.label);
        cachedControlStyle.fontSize = 12;
        cachedControlStyle.normal.textColor = new Color(1, 1, 1, 0.5f);

        cachedPauseTitleStyle = new GUIStyle(cachedBigLabelStyle);
        cachedPauseTitleStyle.fontSize = 48;
        cachedPauseTitleStyle.normal.textColor = Color.yellow;

        cachedPauseHintStyle = new GUIStyle(GUI.skin.label);
        cachedPauseHintStyle.fontSize = 20;
        cachedPauseHintStyle.alignment = TextAnchor.MiddleCenter;
        cachedPauseHintStyle.normal.textColor = Color.white;

        cachedWinnerStyle = new GUIStyle(cachedBigLabelStyle);
        cachedWinnerStyle.fontSize = 52;
        cachedWinnerStyle.normal.textColor = Color.yellow;

        cachedScoreStyle = new GUIStyle(GUI.skin.label);
        cachedScoreStyle.fontSize = 22;
        cachedScoreStyle.alignment = TextAnchor.MiddleCenter;
        cachedScoreStyle.normal.textColor = Color.white;

        cachedHintStyle = new GUIStyle(GUI.skin.label);
        cachedHintStyle.fontSize = 20;
        cachedHintStyle.fontStyle = FontStyle.Bold;
        cachedHintStyle.alignment = TextAnchor.MiddleCenter;

        cachedFailStyle = new GUIStyle(GUI.skin.label);
        cachedFailStyle.fontSize = 18;
        cachedFailStyle.fontStyle = FontStyle.Bold;
        cachedFailStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void OnGUI()
    {
        if (!useOnGUIFallback) return;

        // Session 18: 惰性初始化缓存样式（只执行一次）
        InitStylesIfNeeded();

        // ===== HUD 信息 =====

        // 生命值（左上角）
        if (marioHealth != null)
        {
            string hearts = "";
            for (int i = 0; i < marioHealth.MaxHealth; i++)
            {
                hearts += i < marioHealth.CurrentHealth ? "♥ " : "♡ ";
            }
            cachedLabelStyle.normal.textColor = Color.red;
            cachedLabelStyle.alignment = TextAnchor.MiddleLeft;
            GUI.Label(new Rect(20, 20, 300, 40), $"Mario HP: {hearts}", cachedLabelStyle);
        }

        // 计时器（顶部居中）- 非暂停/非结束时显示
        if (GameManager.Instance != null &&
            (GameManager.Instance.CurrentState == GameState.Playing ||
             GameManager.Instance.CurrentState == GameState.Paused))
        {
            float time = GameManager.Instance.GameTimer;
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);

            cachedLabelStyle.normal.textColor = time < 30f ? Color.red : Color.white;
            cachedLabelStyle.alignment = TextAnchor.MiddleCenter;
            // B024: 加宽显示区域，增加 Y 偏移，防止时间文字被裁剪
            GUI.Label(new Rect(Screen.width / 2 - 80, 8, 160, 40), $"{minutes:00}:{seconds:00}", cachedLabelStyle);
        }

        // 回合信息（右上角）
        if (GameManager.Instance != null)
        {
            cachedLabelStyle.normal.textColor = Color.white;
            cachedLabelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(Screen.width - 320, 20, 300, 40),
                $"Round {GameManager.Instance.CurrentRound}  |  Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}",
                cachedLabelStyle);
        }

        // B025: 无冷却模式指示器
        if (GameManager.Instance != null && GameManager.Instance.NoCooldownMode)
        {
            GUI.Label(new Rect(Screen.width / 2 - 100, 42, 200, 25), "[F9] NO COOLDOWN", cachedNoCdStyle);
        }

        // ===== 暂停画面 (B014 修复) =====
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
        {
            DrawPauseScreen();
        }

        // ===== 游戏结束画面 (B011 修复) =====
        if (showGameOver)
        {
            DrawGameOverScreen();
        }

        // ===== 道具操控失败提示 (B012 修复) =====
        if (abilityFailTimer > 0f)
        {
            DrawAbilityFailFeedback();
        }

        // ===== 操作提示（左下角）=====
        GUI.Label(new Rect(20, Screen.height - 80, 400, 60),
            "P1(Mario): WASD + Space | P2(Trickster): Arrows + P/O/I/L\nESC: Pause | F5: Restart",
            cachedControlStyle);
    }

    /// <summary>绘制暂停画面 - 半透明遮罩 + 大号文字 (B014)</summary>
    private void DrawPauseScreen()
    {
        // 半透明深色遮罩
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 暂停标题
        GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), "PAUSED", cachedPauseTitleStyle);

        // 操作提示
        GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 40), "Press ESC to Resume  |  Press F5 to Restart", cachedPauseHintStyle);
    }

    /// <summary>绘制游戏结束画面 - 全屏遮罩 + 醒目胜利信息 + 闪烁提示 (B011)</summary>
    private void DrawGameOverScreen()
    {
        // ---- 全屏半透明遮罩 ----
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ---- 胜利者横幅背景 ----
        Color bannerColor = gameOverWinner == "Mario" ? new Color(0.8f, 0.2f, 0.2f, 0.6f) : new Color(0.2f, 0.3f, 0.8f, 0.6f);
        GUI.color = bannerColor;
        GUI.DrawTexture(new Rect(0, Screen.height / 2 - 80, Screen.width, 160), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ---- 胜利信息（大号文字）----
        // 添加阴影效果
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.Label(new Rect(3, Screen.height / 2 - 57, Screen.width, 60), gameOverMessage, cachedWinnerStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), gameOverMessage, cachedWinnerStyle);

        // ---- 比分显示 ----
        if (GameManager.Instance != null)
        {
            GUI.Label(new Rect(0, Screen.height / 2 + 5, Screen.width, 35),
                $"Score: Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}  |  Round {GameManager.Instance.CurrentRound}",
                cachedScoreStyle);
        }

        // ---- 操作提示（闪烁效果）----
        float blinkAlpha = Mathf.PingPong(gameOverBlinkTimer * 2f, 1f) * 0.6f + 0.4f;
        cachedHintStyle.normal.textColor = new Color(1f, 1f, 1f, blinkAlpha);
        GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 40),
            "Press  R  to Restart   |   Press  N  for Next Round", cachedHintStyle);
    }

    /// <summary>绘制道具操控失败提示 (B012)</summary>
    private void DrawAbilityFailFeedback()
    {
        // 渐隐效果
        float alpha = Mathf.Clamp01(abilityFailTimer / 0.5f);

        cachedFailStyle.normal.textColor = new Color(1f, 0.5f, 0.3f, alpha);

        // 背景条
        GUI.color = new Color(0, 0, 0, 0.4f * alpha);
        GUI.DrawTexture(new Rect(Screen.width / 2 - 200, Screen.height - 100, 400, 40), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height - 100, 400, 40), abilityFailMessage, cachedFailStyle);
    }

    #endregion

    #region 按钮回调（供Canvas Button使用）

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

    #endregion
}
