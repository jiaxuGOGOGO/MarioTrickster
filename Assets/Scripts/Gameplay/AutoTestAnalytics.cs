using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ═══════════════════════════════════════════════════════════════════
// AutoTestAnalytics — AI 自动对战数据收集与战报输出
//
// 职责：
//   1. 监听 GameManager.OnGameOver，累计 Mario / Trickster 胜场
//   2. 监听 GameplayEventBus.OnTrapTriggered，统计各陷阱触发次数
//   3. 记录每局耗时
//   4. 提供 PrintMatchReport() 输出汇总战报到 Unity Console
//   5. [S60] 空间病灶追踪器：
//      - 卡死检测（Stuck Detection）：Mario 连续 3 秒 X 变化 < 0.5f → 强制结束回合
//      - 致命点追踪（Death Heatmap）：记录死亡坐标 + 死因分类
//      - Scene 视图 Gizmos 可视化：死点红球 + 卡死点黄方块
//
// 生命周期：
//   - 由 TestConsoleWindow（AI Auto-Arena 折叠栏）在开启挂机时创建
//   - 关闭挂机时销毁
//   - 不依赖 MonoBehaviour，纯 C# 类，手动订阅/退订事件
//   - [S60] Gizmos 可视化通过独立的 AnalyticsGizmoRenderer（MonoBehaviour）实现
//
// [AI防坑警告]
//   - 坐标记录使用 Vector2Int（ASCII 白盒 1 字符 = 1x1 Unity 单位）
//   - 卡死检测排除终点附近区域，避免误判
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 死亡记录条目。
/// </summary>
public struct DeathRecord
{
    public Vector2Int position;
    public DeathCause cause;
    public int matchIndex;
}

/// <summary>
/// 死因分类。
/// </summary>
public enum DeathCause
{
    /// <summary>Y 坐标低于下限，坠入深渊</summary>
    FallOffCliff,
    /// <summary>被机关/陷阱致死</summary>
    TrapKill
}

/// <summary>
/// 卡死记录条目。
/// </summary>
public struct StuckRecord
{
    public Vector2Int position;
    public int matchIndex;
    public float stuckDuration;
}

/// <summary>
/// AI 自动对战数据收集器。
/// 纯 C# 类，通过事件订阅收集数据，不挂载到 GameObject。
/// </summary>
public class AutoTestAnalytics
{
    // ═══════════════════════════════════════════════════════════
    // 统计数据
    // ═══════════════════════════════════════════════════════════

    private int totalMatches;
    private int marioWins;
    private int tricksterWins;
    private float totalMatchTime;
    private float currentMatchStartTime;
    private bool matchInProgress;

    /// <summary>
    /// 陷阱触发次数统计。Key = source GameObject name，Value = 触发次数。
    /// </summary>
    private Dictionary<string, int> trapTriggerCounts = new Dictionary<string, int>();

    // ═══════════════════════════════════════════════════════════
    // [S60] 空间病灶追踪数据
    // ═══════════════════════════════════════════════════════════

    /// <summary>死亡点列表（含坐标、死因、局号）</summary>
    private List<DeathRecord> deathPoints = new List<DeathRecord>();

    /// <summary>卡死点列表（含坐标、局号、卡死时长）</summary>
    private List<StuckRecord> stuckPoints = new List<StuckRecord>();

    // 卡死检测内部状态
    private MarioController _mario;
    private bool _marioCached;
    private float _lastMarioX;
    private float _stuckTimer;
    private bool _stuckTriggeredThisRound;

    private const float STUCK_THRESHOLD_SECONDS = 3.0f;
    private const float STUCK_THRESHOLD_DISTANCE = 0.5f;
    private const float CLIFF_Y_THRESHOLD = -2.0f;

    // Gizmos 可视化辅助对象
    private AnalyticsGizmoRenderer _gizmoRenderer;

    // ═══════════════════════════════════════════════════════════
    // 公开属性（供 Editor UI 实时显示）
    // ═══════════════════════════════════════════════════════════

    public int TotalMatches => totalMatches;
    public int MarioWins => marioWins;
    public int TricksterWins => tricksterWins;
    public float MarioWinRate => totalMatches > 0 ? (float)marioWins / totalMatches * 100f : 0f;
    public float TricksterWinRate => totalMatches > 0 ? (float)tricksterWins / totalMatches * 100f : 0f;
    public float AverageMatchTime => totalMatches > 0 ? totalMatchTime / totalMatches : 0f;

    /// <summary>死亡点列表（只读副本）</summary>
    public IReadOnlyList<DeathRecord> DeathPoints => deathPoints;

    /// <summary>卡死点列表（只读副本）</summary>
    public IReadOnlyList<StuckRecord> StuckPoints => stuckPoints;

    public int TotalDeaths => deathPoints.Count;
    public int CliffDeaths => deathPoints.Count(d => d.cause == DeathCause.FallOffCliff);
    public int TrapDeaths => deathPoints.Count(d => d.cause == DeathCause.TrapKill);
    public int TotalStucks => stuckPoints.Count;

    // ═══════════════════════════════════════════════════════════
    // 订阅 / 退订
    // ═══════════════════════════════════════════════════════════

    private GameManager _gameManager;
    private bool _subscribed;

    /// <summary>
    /// 开始收集数据。订阅 GameManager 和 GameplayEventBus 事件。
    /// </summary>
    public void StartCollecting(GameManager gm)
    {
        if (_subscribed) StopCollecting();

        _gameManager = gm;
        if (_gameManager == null)
        {
            Debug.LogWarning("[AutoTestAnalytics] GameManager is null, cannot start collecting.");
            return;
        }

        _gameManager.OnGameOver += HandleGameOver;
        _gameManager.OnGameStateChanged += HandleGameStateChanged;
        GameplayEventBus.OnTrapTriggered += HandleTrapTriggered;
        _subscribed = true;

        // 缓存 Mario 引用并订阅死亡事件
        CacheMario();

        // 如果当前已经在 Playing 状态，标记比赛开始
        if (_gameManager.CurrentState == GameState.Playing)
        {
            matchInProgress = true;
            currentMatchStartTime = Time.unscaledTime;
        }

        // 创建 Gizmos 渲染器
        EnsureGizmoRenderer();

        Debug.Log("[AutoTestAnalytics] Data collection started (with spatial defect tracker).");
    }

    /// <summary>
    /// 停止收集数据。退订所有事件。
    /// </summary>
    public void StopCollecting()
    {
        if (!_subscribed) return;

        if (_gameManager != null)
        {
            _gameManager.OnGameOver -= HandleGameOver;
            _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }
        GameplayEventBus.OnTrapTriggered -= HandleTrapTriggered;

        // 退订 Mario 死亡事件
        if (_mario != null)
        {
            _mario.OnDeath -= HandleMarioDeath;
        }

        _subscribed = false;
        matchInProgress = false;
        _marioCached = false;
        _mario = null;

        // 销毁 Gizmos 渲染器
        DestroyGizmoRenderer();

        Debug.Log("[AutoTestAnalytics] Data collection stopped.");
    }

    /// <summary>
    /// 重置所有统计数据。
    /// </summary>
    public void Reset()
    {
        totalMatches = 0;
        marioWins = 0;
        tricksterWins = 0;
        totalMatchTime = 0f;
        currentMatchStartTime = 0f;
        matchInProgress = false;
        trapTriggerCounts.Clear();
        deathPoints.Clear();
        stuckPoints.Clear();
        ResetStuckDetection();

        // 刷新 Gizmos 渲染器数据
        SyncGizmoData();
    }

    // ═══════════════════════════════════════════════════════════
    // [S60] 每帧更新（由 AnalyticsGizmoRenderer.Update 驱动）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 每帧更新卡死检测。由 AnalyticsGizmoRenderer.Update() 调用。
    /// </summary>
    public void Tick(float dt)
    {
        if (!_subscribed || !matchInProgress) return;

        // 惰性缓存 Mario
        if (!_marioCached)
            CacheMario();

        if (_mario == null || !_mario.enabled) return;

        UpdateStuckDetection(dt);
    }

    // ═══════════════════════════════════════════════════════════
    // [S60] 卡死检测 (Stuck Detection)
    // ═══════════════════════════════════════════════════════════

    private void UpdateStuckDetection(float dt)
    {
        if (_stuckTriggeredThisRound) return;

        float currentX = _mario.transform.position.x;
        float deltaX = Mathf.Abs(currentX - _lastMarioX);

        if (deltaX < STUCK_THRESHOLD_DISTANCE * (dt / STUCK_THRESHOLD_SECONDS))
        {
            // X 几乎没动，累加计时器
            _stuckTimer += dt;
        }
        else
        {
            // 有明显移动，重置计时器和基准位置
            _stuckTimer = 0f;
            _lastMarioX = currentX;
        }

        if (_stuckTimer >= STUCK_THRESHOLD_SECONDS)
        {
            // 检查是否在终点附近（排除误判）
            if (IsNearGoal(_mario.transform.position))
            {
                _stuckTimer = 0f;
                return;
            }

            // 确认卡死！
            Vector2Int gridPos = WorldToGrid(_mario.transform.position);
            stuckPoints.Add(new StuckRecord
            {
                position = gridPos,
                matchIndex = totalMatches + 1,
                stuckDuration = _stuckTimer
            });

            _stuckTriggeredThisRound = true;

            Debug.LogWarning($"<color=#FFFF00><b>[\u7a7a\u95f4\u75c5\u7076] \u52a8\u7ebf\u5361\u6b7b\u68c0\u6d4b\uff01\u4f4d\u7f6e: {gridPos} | \u505c\u6ede {_stuckTimer:F1}s</b></color>");

            // 强制结束回合，防止挂机卡死
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EndRound("Trickster");
            }

            SyncGizmoData();
        }
    }

    private void ResetStuckDetection()
    {
        _stuckTimer = 0f;
        _stuckTriggeredThisRound = false;
        if (_mario != null)
            _lastMarioX = _mario.transform.position.x;
    }

    /// <summary>
    /// 检查位置是否在终点（GoalZone 或 EscapeGate）附近 3 格内。
    /// </summary>
    private bool IsNearGoal(Vector3 pos)
    {
        GoalZone goal = Object.FindObjectOfType<GoalZone>();
        if (goal != null && Vector2.Distance(pos, goal.transform.position) < 3f)
            return true;

        EscapeGate gate = Object.FindObjectOfType<EscapeGate>();
        if (gate != null && Vector2.Distance(pos, gate.transform.position) < 3f)
            return true;

        return false;
    }

    // ═══════════════════════════════════════════════════════════
    // [S60] 致命点追踪 (Death Heatmap)
    // ═══════════════════════════════════════════════════════════

    private void HandleMarioDeath()
    {
        if (_mario == null) return;

        Vector3 deathPos = _mario.transform.position;
        Vector2Int gridPos = WorldToGrid(deathPos);

        DeathCause cause = deathPos.y < CLIFF_Y_THRESHOLD
            ? DeathCause.FallOffCliff
            : DeathCause.TrapKill;

        deathPoints.Add(new DeathRecord
        {
            position = gridPos,
            cause = cause,
            matchIndex = totalMatches + 1
        });

        string causeStr = cause == DeathCause.FallOffCliff ? "\u5760\u5d16" : "\u673a\u5173\u81f4\u6b7b";
        Debug.Log($"<color=#FF4444>[\u7a7a\u95f4\u75c5\u7076] Mario \u6b7b\u4ea1\u8bb0\u5f55: {gridPos} ({causeStr})</color>");

        SyncGizmoData();
    }

    // ═══════════════════════════════════════════════════════════
    // 事件处理
    // ═══════════════════════════════════════════════════════════

    private void HandleGameOver(string winner)
    {
        totalMatches++;

        if (winner == "Mario")
            marioWins++;
        else
            tricksterWins++;

        // 记录本局耗时
        if (matchInProgress)
        {
            float matchDuration = Time.unscaledTime - currentMatchStartTime;
            totalMatchTime += matchDuration;
            matchInProgress = false;
        }

        // 重置卡死检测（下一局重新开始）
        ResetStuckDetection();
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Playing && !matchInProgress)
        {
            matchInProgress = true;
            currentMatchStartTime = Time.unscaledTime;

            // 新回合开始，重新缓存 Mario（可能场景重载了）
            _marioCached = false;
            CacheMario();
            ResetStuckDetection();
        }
    }

    private void HandleTrapTriggered(GameplayEventBus.TrapTriggeredPayload payload)
    {
        if (payload == null || payload.source == null) return;

        string sourceId = payload.source.name;
        if (trapTriggerCounts.ContainsKey(sourceId))
            trapTriggerCounts[sourceId]++;
        else
            trapTriggerCounts[sourceId] = 1;
    }

    // ═══════════════════════════════════════════════════════════
    // Mario 引用缓存
    // ═══════════════════════════════════════════════════════════

    private void CacheMario()
    {
        // 退订旧引用
        if (_mario != null)
            _mario.OnDeath -= HandleMarioDeath;

        _mario = Object.FindObjectOfType<MarioController>();
        _marioCached = true;

        if (_mario != null)
        {
            _mario.OnDeath += HandleMarioDeath;
            _lastMarioX = _mario.transform.position.x;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 坐标工具
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 世界坐标转网格坐标（ASCII 白盒 1 字符 = 1x1 Unity 单位）。
    /// </summary>
    private static Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x),
            Mathf.RoundToInt(worldPos.y)
        );
    }

    // ═══════════════════════════════════════════════════════════
    // [S60] Gizmos 可视化
    // ═══════════════════════════════════════════════════════════

    private void EnsureGizmoRenderer()
    {
        if (_gizmoRenderer != null) return;

        var go = new GameObject("[Analytics] Gizmo Renderer");
        go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        _gizmoRenderer = go.AddComponent<AnalyticsGizmoRenderer>();
        _gizmoRenderer.SetAnalytics(this);
    }

    private void DestroyGizmoRenderer()
    {
        if (_gizmoRenderer != null)
        {
            if (_gizmoRenderer.gameObject != null)
                Object.DestroyImmediate(_gizmoRenderer.gameObject);
            _gizmoRenderer = null;
        }
    }

    private void SyncGizmoData()
    {
        if (_gizmoRenderer != null)
            _gizmoRenderer.RefreshData(deathPoints, stuckPoints);
    }

    // ═══════════════════════════════════════════════════════════
    // 战报输出
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 在 Unity Console 输出汇总战报（含空间病灶统计）。
    /// </summary>
    public void PrintMatchReport()
    {
        string deadliestTrap = "N/A";
        int deadliestCount = 0;

        if (trapTriggerCounts.Count > 0)
        {
            var top = trapTriggerCounts.OrderByDescending(kv => kv.Value).First();
            deadliestTrap = top.Key;
            deadliestCount = top.Value;
        }

        // 统计死亡热点（出现次数最多的网格坐标）
        string deathHotspot = "N/A";
        if (deathPoints.Count > 0)
        {
            var hotspot = deathPoints
                .GroupBy(d => d.position)
                .OrderByDescending(g => g.Count())
                .First();
            deathHotspot = $"{hotspot.Key} ({hotspot.Count()}\u6b21)";
        }

        string report =
            "=== \U0001f916 AI \u81ea\u52a8\u5bf9\u6218\u6d4b\u8bd5\u62a5\u544a ===\n" +
            $"\u5bf9\u6218\u603b\u5c40\u6570: {totalMatches} | Mario \u80dc\u7387: {MarioWinRate:F0}% | Trickster \u80dc\u7387: {TricksterWinRate:F0}%\n" +
            $"\u6700\u81f4\u547d\u9677\u9631: {deadliestTrap} (\u89e6\u53d1 {deadliestCount} \u6b21)\n" +
            $"\u5e73\u5747\u5355\u5c40\u8017\u65f6: {AverageMatchTime:F1} \u79d2\n" +
            "--- \u7a7a\u95f4\u75c5\u7076\u7edf\u8ba1 ---\n" +
            $"\u6b7b\u4ea1\u603b\u6b21: {TotalDeaths} (\u5760\u5d16: {CliffDeaths} | \u673a\u5173: {TrapDeaths})\n" +
            $"\u6b7b\u4ea1\u70ed\u70b9: {deathHotspot}\n" +
            $"\u5361\u6b7b\u6b21\u6570: {TotalStucks}";

        if (stuckPoints.Count > 0)
        {
            var stuckHotspot = stuckPoints
                .GroupBy(s => s.position)
                .OrderByDescending(g => g.Count())
                .First();
            report += $" | \u5361\u6b7b\u70ed\u70b9: {stuckHotspot.Key} ({stuckHotspot.Count()}\u6b21)";
        }

        Debug.LogWarning(report);
    }

    /// <summary>
    /// 获取陷阱触发统计的副本（供 Editor UI 显示）。
    /// </summary>
    public Dictionary<string, int> GetTrapStats()
    {
        return new Dictionary<string, int>(trapTriggerCounts);
    }
}

// ═══════════════════════════════════════════════════════════════════
// AnalyticsGizmoRenderer — Scene 视图 Gizmos 可视化
//
// 挂载到隐形 GameObject 上，在 Scene 视图中绘制：
//   - 死亡点：红色半透明球体
//   - 卡死点：黄色方块
// ═══════════════════════════════════════════════════════════════════

public class AnalyticsGizmoRenderer : MonoBehaviour
{
    private AutoTestAnalytics _analytics;
    private List<DeathRecord> _deathPoints = new List<DeathRecord>();
    private List<StuckRecord> _stuckPoints = new List<StuckRecord>();

    public void SetAnalytics(AutoTestAnalytics analytics)
    {
        _analytics = analytics;
    }

    public void RefreshData(List<DeathRecord> deaths, List<StuckRecord> stucks)
    {
        _deathPoints = new List<DeathRecord>(deaths);
        _stuckPoints = new List<StuckRecord>(stucks);
    }

    private void Update()
    {
        // 驱动 AutoTestAnalytics 的每帧卡死检测
        if (_analytics != null)
        {
            _analytics.Tick(Time.deltaTime);
        }
    }

    private void OnDrawGizmos()
    {
        // ── 死亡点：红色半透明球体 ──
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.55f);
        foreach (var death in _deathPoints)
        {
            Vector3 worldPos = new Vector3(death.position.x, death.position.y, 0f);
            Gizmos.DrawSphere(worldPos, 0.4f);
        }

        // ── 卡死点：黄色方块 ──
        Gizmos.color = new Color(1f, 0.95f, 0.1f, 0.6f);
        foreach (var stuck in _stuckPoints)
        {
            Vector3 worldPos = new Vector3(stuck.position.x, stuck.position.y, 0f);
            Gizmos.DrawCube(worldPos, new Vector3(0.8f, 0.8f, 0.1f));
        }

        // ── 标签（仅在有数据时绘制线框辅助） ──
#if UNITY_EDITOR
        // 死亡点线框
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        foreach (var death in _deathPoints)
        {
            Vector3 worldPos = new Vector3(death.position.x, death.position.y, 0f);
            Gizmos.DrawWireSphere(worldPos, 0.5f);
        }

        // 卡死点线框
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        foreach (var stuck in _stuckPoints)
        {
            Vector3 worldPos = new Vector3(stuck.position.x, stuck.position.y, 0f);
            Gizmos.DrawWireCube(worldPos, new Vector3(1f, 1f, 0.1f));
        }
#endif
    }
}
