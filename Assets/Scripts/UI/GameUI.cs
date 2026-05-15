using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏 HUD 兼容脚本。
///
/// 标准运行时 HUD 已迁移到 GlobalGameUICanvas（UGUI Prefab）。本脚本仅保留旧 Canvas Text 绑定、
/// GameManager / PlayerHealth 事件同步和按钮回调 API，避免继续使用 OnGUI 后备绘制。
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("=== UI 引用（可选，运行时 HUD 已迁移至 GlobalGameUICanvas）===")]
    [SerializeField] private Text healthText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text roundInfoText;
    [SerializeField] private Text gameOverText;
    [SerializeField] private GameObject gameOverPanel;

    [Header("=== 引用 ===")]
    [SerializeField] private PlayerHealth marioHealth;

    private string gameOverMessage = string.Empty;
    private bool showGameOver;
    private string gameOverWinner = string.Empty;

    private string abilityFailMessage = string.Empty;
    private float abilityFailTimer;
    private const float AbilityFailDisplayDuration = 2f;

    private TricksterController registeredTrickster;

    private void Start()
    {
        if (marioHealth == null)
        {
            MarioController mario = FindObjectOfType<MarioController>();
            if (mario != null)
            {
                marioHealth = mario.GetComponent<PlayerHealth>();
            }
        }

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

        RegisterAbilityFeedback();

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

        if (registeredTrickster != null)
        {
            registeredTrickster.OnAbilityFailed -= ShowAbilityFailFeedback;
            registeredTrickster = null;
        }
    }

    private void Update()
    {
        if (abilityFailTimer > 0f)
        {
            abilityFailTimer -= Time.unscaledDeltaTime;
        }
    }

    #region 道具操控失败反馈 (B012)

    private void RegisterAbilityFeedback()
    {
        TricksterAbilitySystem abilitySystem = FindObjectOfType<TricksterAbilitySystem>();
        if (abilitySystem == null)
        {
            return;
        }

        registeredTrickster = FindObjectOfType<TricksterController>();
        if (registeredTrickster != null)
        {
            registeredTrickster.OnAbilityFailed += ShowAbilityFailFeedback;
        }
    }

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

    #region 按钮回调（供 Canvas Button 使用）

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
