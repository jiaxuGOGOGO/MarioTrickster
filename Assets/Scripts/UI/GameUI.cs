using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏HUD界面 - MVP基础版
/// 功能: 显示Mario生命值、计时器、回合信息、胜负结果
/// 使用方式: 挂载到Canvas上，在Inspector中拖入UI元素引用
/// 
/// MVP阶段使用Unity内置UI(UGUI)，后期可升级为更精美的UI
/// 如果场景中没有对应的UI元素，脚本会用OnGUI作为后备显示
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
        gameOverMessage = $"{winner} Wins!";

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

        // 样式
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 20;
        labelStyle.fontStyle = FontStyle.Bold;

        GUIStyle bigLabelStyle = new GUIStyle(GUI.skin.label);
        bigLabelStyle.fontSize = 36;
        bigLabelStyle.fontStyle = FontStyle.Bold;
        bigLabelStyle.alignment = TextAnchor.MiddleCenter;

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

        // 计时器（顶部居中）
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
        {
            float time = GameManager.Instance.GameTimer;
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);

            labelStyle.normal.textColor = time < 30f ? Color.red : Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(Screen.width / 2 - 50, 20, 100, 40), $"{minutes:00}:{seconds:00}", labelStyle);
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

        // 游戏状态提示
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                bigLabelStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 80), "PAUSED\nPress ESC to resume", bigLabelStyle);
            }
        }

        // 游戏结束画面
        if (showGameOver)
        {
            // 半透明背景
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 胜利信息
            bigLabelStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), gameOverMessage, bigLabelStyle);

            // 操作提示
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 18;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 40), "Press R to Restart  |  Press N for Next Round", hintStyle);

            // 按键检测
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.R)
                {
                    GameManager.Instance?.RestartLevel();
                }
                else if (Event.current.keyCode == KeyCode.N)
                {
                    GameManager.Instance?.ResetRound();
                }
            }
        }

        // 操作提示（左下角）
        GUIStyle controlStyle = new GUIStyle(GUI.skin.label);
        controlStyle.fontSize = 12;
        controlStyle.normal.textColor = new Color(1, 1, 1, 0.5f);
        GUI.Label(new Rect(20, Screen.height - 80, 400, 60),
            "P1(Mario): WASD + Space | P2(Trickster): Arrows + RCtrl + RShift\nESC: Pause | F5: Restart",
            controlStyle);
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
