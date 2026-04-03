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

    private void OnGUI()
    {
        if (!useOnGUIFallback) return;

        // ===== 样式定义 =====
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;

        GUIStyle bigLabelStyle = new GUIStyle(GUI.skin.label);
        bigLabelStyle.fontSize = 36;
        bigLabelStyle.fontStyle = FontStyle.Bold;
        bigLabelStyle.alignment = TextAnchor.MiddleCenter;

        // ===== HUD 信息 =====

        // 生命值（左上角）
        if (marioHealth != null)
        {
            string hearts = "";
            for (int i = 0; i < marioHealth.MaxHealth; i++)
            {
                hearts += i < marioHealth.CurrentHealth ? "♥ " : "♡ ";
            }
            labelStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(20, 20, 300, 40), $"Mario HP: {hearts}", labelStyle);
        }

        // 计时器（顶部居中）- 非暂停/非结束时显示
        if (GameManager.Instance != null &&
            (GameManager.Instance.CurrentState == GameState.Playing ||
             GameManager.Instance.CurrentState == GameState.Paused))
        {
            float time = GameManager.Instance.GameTimer;
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);

            labelStyle.normal.textColor = time < 30f ? Color.red : Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            // B024: 加宽显示区域，增加 Y 偏移，防止时间文字被裁剪
            GUI.Label(new Rect(Screen.width / 2 - 80, 8, 160, 40), $"{minutes:00}:{seconds:00}", labelStyle);
        }

        // 回合信息（右上角）
        if (GameManager.Instance != null)
        {
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(Screen.width - 320, 20, 300, 40),
                $"Round {GameManager.Instance.CurrentRound}  |  Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}",
                labelStyle);
        }

        // B025: 无冷却模式指示器
        if (GameManager.Instance != null && GameManager.Instance.NoCooldownMode)
        {
            GUIStyle noCdStyle = new GUIStyle(GUI.skin.label);
            noCdStyle.fontSize = 14;
            noCdStyle.fontStyle = FontStyle.Bold;
            noCdStyle.alignment = TextAnchor.MiddleCenter;
            noCdStyle.normal.textColor = new Color(0f, 1f, 0.5f, 0.9f);
            GUI.Label(new Rect(Screen.width / 2 - 100, 42, 200, 25), "[F9] NO COOLDOWN", noCdStyle);
        }

        // ===== 暂停画面 (B014 修复) =====
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused)
        {
            DrawPauseScreen(bigLabelStyle);
        }

        // ===== 游戏结束画面 (B011 修复) =====
        if (showGameOver)
        {
            DrawGameOverScreen(bigLabelStyle);
        }

        // ===== 道具操控失败提示 (B012 修复) =====
        if (abilityFailTimer > 0f)
        {
            DrawAbilityFailFeedback();
        }

        // ===== 操作提示（左下角）=====
        GUIStyle controlStyle = new GUIStyle(GUI.skin.label);
        controlStyle.fontSize = 12;
        controlStyle.normal.textColor = new Color(1, 1, 1, 0.5f);
        GUI.Label(new Rect(20, Screen.height - 80, 400, 60),
            "P1(Mario): WASD + Space | P2(Trickster): Arrows + P/O/I/L\nESC: Pause | F5: Restart",
            controlStyle);
    }

    /// <summary>绘制暂停画面 - 半透明遮罩 + 大号文字 (B014)</summary>
    private void DrawPauseScreen(GUIStyle bigLabelStyle)
    {
        // 半透明深色遮罩
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // 暂停标题
        GUIStyle pauseTitleStyle = new GUIStyle(bigLabelStyle);
        pauseTitleStyle.fontSize = 48;
        pauseTitleStyle.normal.textColor = Color.yellow;
        GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), "PAUSED", pauseTitleStyle);

        // 操作提示
        GUIStyle pauseHintStyle = new GUIStyle(GUI.skin.label);
        pauseHintStyle.fontSize = 20;
        pauseHintStyle.alignment = TextAnchor.MiddleCenter;
        pauseHintStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 40), "Press ESC to Resume  |  Press F5 to Restart", pauseHintStyle);
    }

    /// <summary>绘制游戏结束画面 - 全屏遮罩 + 醒目胜利信息 + 闪烁提示 (B011)</summary>
    private void DrawGameOverScreen(GUIStyle bigLabelStyle)
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
        GUIStyle winnerStyle = new GUIStyle(bigLabelStyle);
        winnerStyle.fontSize = 52;
        winnerStyle.normal.textColor = Color.yellow;

        // 添加阴影效果
        GUI.color = new Color(0, 0, 0, 0.5f);
        GUI.Label(new Rect(3, Screen.height / 2 - 57, Screen.width, 60), gameOverMessage, winnerStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), gameOverMessage, winnerStyle);

        // ---- 比分显示 ----
        if (GameManager.Instance != null)
        {
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label);
            scoreStyle.fontSize = 22;
            scoreStyle.alignment = TextAnchor.MiddleCenter;
            scoreStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 + 5, Screen.width, 35),
                $"Score: Mario {GameManager.Instance.MarioWins} - Trickster {GameManager.Instance.TricksterWins}  |  Round {GameManager.Instance.CurrentRound}",
                scoreStyle);
        }

        // ---- 操作提示（闪烁效果）----
        float blinkAlpha = Mathf.PingPong(gameOverBlinkTimer * 2f, 1f) * 0.6f + 0.4f;
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 20;
        hintStyle.fontStyle = FontStyle.Bold;
        hintStyle.alignment = TextAnchor.MiddleCenter;
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, blinkAlpha);
        GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 40),
            "Press  R  to Restart   |   Press  N  for Next Round", hintStyle);
    }

    /// <summary>绘制道具操控失败提示 (B012)</summary>
    private void DrawAbilityFailFeedback()
    {
        // 渐隐效果
        float alpha = Mathf.Clamp01(abilityFailTimer / 0.5f);

        GUIStyle failStyle = new GUIStyle(GUI.skin.label);
        failStyle.fontSize = 18;
        failStyle.fontStyle = FontStyle.Bold;
        failStyle.alignment = TextAnchor.MiddleCenter;
        failStyle.normal.textColor = new Color(1f, 0.5f, 0.3f, alpha);

        // 背景条
        GUI.color = new Color(0, 0, 0, 0.4f * alpha);
        GUI.DrawTexture(new Rect(Screen.width / 2 - 200, Screen.height - 100, 400, 40), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(Screen.width / 2 - 200, Screen.height - 100, 400, 40), abilityFailMessage, failStyle);
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
