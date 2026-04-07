using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏管理器 - MVP核心脚本
/// 功能: 游戏状态管理、胜负判定、关卡流程控制
/// 
/// 胜负条件:
///   Mario 到达终点（触碰GoalZone） → Mario 胜利
///   Mario 生命值归零            → Trickster 胜利
///   计时器归零（可选）           → Trickster 胜利
/// 
/// 使用方式: 挂载到场景中的空GameObject上，设为单例
///
/// Session 16 更新:
///   B025 - 添加冷却取消调试开关（F9 键切换），方便测试时快速重复操控
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("=== 玩家引用 ===")]
    [SerializeField] private MarioController mario;
    [SerializeField] private TricksterController trickster;
    [SerializeField] private PlayerHealth marioHealth;

    [Header("=== 系统引用 ===")]
    [SerializeField] private InputManager inputManager;

    [Header("=== 计时器（可选）===")]
    [SerializeField] private bool useTimer = true;
    [SerializeField] private float levelTimeLimit = 120f; // 秒

    [Header("=== 重生设置 ===")]
    [SerializeField] private Transform marioSpawnPoint;
    [SerializeField] private Transform tricksterSpawnPoint;
    [SerializeField] private float respawnDelay = 2f;

    [Header("=== 关卡设置 ===")]
    [SerializeField] private string nextLevelScene = "";
    [SerializeField] private string mainMenuScene = "MainMenu";

    // 游戏状态
    private GameState currentState = GameState.WaitingToStart;
    private float gameTimer;
    private int marioWins;
    private int tricksterWins;
    private int currentRound = 1;

    // 恢复提示已移除（用户反馈不需要）

    // B025: 冷却取消调试开关
    private bool noCooldownMode = false;
    public bool NoCooldownMode => noCooldownMode;

    // 公共属性
    public GameState CurrentState => currentState;
    public float GameTimer => gameTimer;
    public float TimerProgress => useTimer && levelTimeLimit > 0 ? gameTimer / levelTimeLimit : 1f;
    public int MarioWins => marioWins;
    public int TricksterWins => tricksterWins;
    public int CurrentRound => currentRound;
    public bool ShowResumedHint => false; // 已移除恢复提示功能

    // 事件
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<float> OnTimerUpdated; // 剩余时间
    public System.Action<string> OnGameOver; // 胜利者名称
    public System.Action OnRoundStart;

    private void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 自动查找引用（如果Inspector中未设置）
        AutoFindReferences();

        // 注册事件
        if (marioHealth != null)
        {
            marioHealth.OnDeath += OnMarioDeath;
        }

        // 初始化计时器
        gameTimer = levelTimeLimit;

        // 自动开始游戏（MVP阶段简化流程）
        StartGame();
    }

    private void Update()
    {
        // ===== 全局按键检测（不受游戏状态限制）=====
        // S49: 通过 InputManager 的 IInputProvider 读取全局按键，
        // 实现自动化测试时可注入虚拟全局按键。
        // 回退兜底：如果 inputManager 未设置，仍使用 Input.GetKeyDown（安全降级）。
        IInputProvider gip = inputManager != null ? inputManager.GetCurrentProvider() : null;

        // 暂停/恢复键 - 在 Playing 和 Paused 状态下都可用
        bool pauseDown = gip != null ? gip.GetPauseDown() : Input.GetKeyDown(KeyCode.Escape);
        if (pauseDown)
        {
            if (currentState == GameState.Playing || currentState == GameState.Paused)
            {
                TogglePause();
            }
        }

        // 快速重启 - 任何状态下都可用
        bool restartDown = gip != null ? gip.GetRestartDown() : Input.GetKeyDown(KeyCode.F5);
        if (restartDown)
        {
            RestartLevel();
        }

        // B025: 冷却取消开关 (F9 键切换)
        bool noCooldownDown = gip != null ? gip.GetNoCooldownToggleDown() : Input.GetKeyDown(KeyCode.F9);
        if (noCooldownDown)
        {
            ToggleNoCooldown();
        }

        // B025: 无冷却模式下每帧清除所有冷却
        if (noCooldownMode && currentState == GameState.Playing)
        {
            ClearAllCooldowns();
        }

        // 回合结束后的按键检测
        if (currentState == GameState.RoundOver)
        {
            bool restartRound = gip != null ? gip.GetRestartRoundDown() : Input.GetKeyDown(KeyCode.R);
            bool nextRound = gip != null ? gip.GetNextRoundDown() : Input.GetKeyDown(KeyCode.N);
            if (restartRound)
            {
                RestartLevel();
            }
            else if (nextRound)
            {
                ResetRound();
            }
        }

        // 恢复提示已移除（用户反馈不需要）

        // ===== 以下逻辑仅在 Playing 状态下执行 =====
        if (currentState != GameState.Playing) return;

        // 更新计时器
        if (useTimer)
        {
            gameTimer -= Time.deltaTime;
            OnTimerUpdated?.Invoke(gameTimer);

            if (gameTimer <= 0)
            {
                gameTimer = 0;
                // 时间到，Trickster胜利
                EndRound("Trickster");
            }
        }
    }

    private void OnDestroy()
    {
        if (marioHealth != null)
        {
            marioHealth.OnDeath -= OnMarioDeath;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region 游戏流程

    /// <summary>开始游戏</summary>
    public void StartGame()
    {
        SetGameState(GameState.Playing);
        gameTimer = levelTimeLimit;

        if (inputManager != null)
            inputManager.EnableAllInput();

        OnRoundStart?.Invoke();

        Debug.Log("[GameManager] 游戏开始！");
    }

    /// <summary>结束回合</summary>
    public void EndRound(string winner)
    {
        if (currentState != GameState.Playing) return;

        SetGameState(GameState.RoundOver);

        // 禁用输入
        if (inputManager != null)
            inputManager.DisableAllInput();

        // 记录胜利
        if (winner == "Mario")
        {
            marioWins++;
            Debug.Log($"[GameManager] Mario 胜利！(总计: Mario {marioWins} - Trickster {tricksterWins})");
        }
        else
        {
            tricksterWins++;
            Debug.Log($"[GameManager] Trickster 胜利！(总计: Mario {marioWins} - Trickster {tricksterWins})");
        }

        OnGameOver?.Invoke(winner);
    }

    /// <summary>Mario到达终点时调用</summary>
    public void OnMarioReachedGoal()
    {
        EndRound("Mario");
    }

    /// <summary>Mario死亡回调</summary>
    private void OnMarioDeath()
    {
        Debug.Log("[GameManager] Mario 死亡！");

        // 通知Mario控制器执行死亡动画
        if (mario != null)
        {
            mario.Die();
        }

        EndRound("Trickster");
    }

    #endregion

    #region 暂停

    public void TogglePause()
    {
        if (currentState == GameState.Playing)
        {
            SetGameState(GameState.Paused);
            Time.timeScale = 0f;

            if (inputManager != null)
                inputManager.DisableAllInput();

            Debug.Log("[GameManager] 游戏暂停");
        }
        else if (currentState == GameState.Paused)
        {
            SetGameState(GameState.Playing);
            Time.timeScale = 1f;

            if (inputManager != null)
                inputManager.EnableAllInput();

            Debug.Log("[GameManager] 游戏继续");
        }
    }

    #endregion

    #region 关卡管理

    /// <summary>重启当前关卡</summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>加载下一关</summary>
    public void LoadNextLevel()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(nextLevelScene))
        {
            SceneManager.LoadScene(nextLevelScene);
        }
        else
        {
            // 没有下一关，重新开始当前关
            RestartLevel();
        }
    }

    /// <summary>返回主菜单</summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    /// <summary>重置回合（不重新加载场景）</summary>
    public void ResetRound()
    {
        currentRound++;

        // 重置Mario位置和状态
        if (mario != null && marioSpawnPoint != null)
        {
            mario.transform.position = marioSpawnPoint.position;
            mario.enabled = true;
        }

        if (marioHealth != null)
        {
            marioHealth.ResetHealth();
        }

        // 重置Trickster位置和状态
        if (trickster != null && tricksterSpawnPoint != null)
        {
            trickster.transform.position = tricksterSpawnPoint.position;
        }

        // 重置场景中的 GoalZone 触发状态（修复 B020：第二回合终点无反应）
        GoalZone[] goalZones = FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            gz.ResetTrigger();
        }

        // 重置可操控道具的使用次数
        ControllablePropBase[] props = FindObjectsOfType<ControllablePropBase>();
        foreach (ControllablePropBase prop in props)
        {
            prop.ResetUses();
        }

        Debug.Log($"[GameManager] 回合 {currentRound} 开始，所有状态已重置");

        // 重新开始
        StartGame();
    }

    #endregion

    #region B025: 冷却取消调试开关

    /// <summary>切换无冷却模式</summary>
    private void ToggleNoCooldown()
    {
        noCooldownMode = !noCooldownMode;
        Debug.Log($"[GameManager] 无冷却模式: {(noCooldownMode ? "ON" : "OFF")}");
        if (noCooldownMode)
        {
            ClearAllCooldowns();
        }
    }

    // Session 18 性能优化：缓存场景对象引用，避免每帧 3 次 FindObjectsOfType 场景扫描
    private DisguiseSystem[] cachedDisguises;
    private ScanAbility[] cachedScans;
    private ControllablePropBase[] cachedProps;
    private bool cooldownCacheDirty = true;

    /// <summary>刷新冷却缓存（场景重建或首次启用时调用）</summary>
    private void RefreshCooldownCache()
    {
        cachedDisguises = FindObjectsOfType<DisguiseSystem>();
        cachedScans = FindObjectsOfType<ScanAbility>();
        cachedProps = FindObjectsOfType<ControllablePropBase>();
        cooldownCacheDirty = false;
    }

    /// <summary>清除场景中所有技能/道具的冷却（Session 18: 使用缓存）</summary>
    private void ClearAllCooldowns()
    {
        if (cooldownCacheDirty || cachedDisguises == null)
        {
            RefreshCooldownCache();
        }

        // 清除伪装系统冷却
        foreach (DisguiseSystem ds in cachedDisguises)
        {
            if (ds != null) ds.ResetCooldown();
        }

        // 清除扫描技能冷却
        foreach (ScanAbility sa in cachedScans)
        {
            if (sa != null) sa.ResetCooldown();
        }

        // 清除可操控道具冷却
        foreach (ControllablePropBase prop in cachedProps)
        {
            if (prop != null) prop.ResetCooldown();
        }
    }

    #endregion

    #region 内部方法

    private void SetGameState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        OnGameStateChanged?.Invoke(currentState);
    }

    private void AutoFindReferences()
    {
        if (mario == null)
            mario = FindObjectOfType<MarioController>();

        if (trickster == null)
            trickster = FindObjectOfType<TricksterController>();

        if (marioHealth == null && mario != null)
            marioHealth = mario.GetComponent<PlayerHealth>();

        if (inputManager == null)
            inputManager = FindObjectOfType<InputManager>();
    }

    #endregion
}

/// <summary>游戏状态枚举</summary>
public enum GameState
{
    WaitingToStart,  // 等待开始
    Playing,         // 游戏中
    Paused,          // 暂停
    RoundOver,       // 回合结束
    GameOver         // 游戏结束（多回合制时）
}
