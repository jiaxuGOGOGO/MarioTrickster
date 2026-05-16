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

            Debug.LogWarning($"<color=#FFFF00><b>[空间病灶] 动线卡死检测！位置: {gridPos} | 停滞 {_stuckTimer:F1}s</b></color>");

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

        string causeStr = cause == DeathCause.FallOffCliff ? "坠崖" : "机关致死";
        Debug.Log($"<color=#FF4444>[空间病灶] Mario 死亡记录: {gridPos} ({causeStr})</color>");

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
    /// 在 Unity Console 输出汇总战报（含空间病灶统计 + 大白话 ASCII 坐标修改建议）。
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

        // ── 基础战报 ──
        string report =
            "=== \U0001f916 AI 自动对战测试报告 ===\n" +
            $"对战总局数: {totalMatches} | Mario 胜率: {MarioWinRate:F0}% | Trickster 胜率: {TricksterWinRate:F0}%\n" +
            $"最致命陷阱: {deadliestTrap} (触发 {deadliestCount} 次)\n" +
            $"平均单局耗时: {AverageMatchTime:F1} 秒\n" +
            "--- 空间病灶统计 ---\n" +
            $"死亡总次: {TotalDeaths} (坠崖: {CliffDeaths} | 机关: {TrapDeaths}) | 卡死次数: {TotalStucks}";

        Debug.LogWarning(report);

        // ── 大白话病灶诊断 ──
        PrintDefectDiagnosis();
    }

    /// <summary>
    /// 汇总所有死亡点 + 卡死点的频次，取前 2 个高频病灶，
    /// 输出带 RichText 的大白话 ASCII 坐标修改建议。
    /// </summary>
    private void PrintDefectDiagnosis()
    {
        // 合并所有病灶点（死亡 + 卡死）到统一列表
        var allDefects = new List<DefectEntry>();

        // 按坐标聚合死亡点
        if (deathPoints.Count > 0)
        {
            var deathGroups = deathPoints
                .GroupBy(d => d.position)
                .OrderByDescending(g => g.Count());

            foreach (var g in deathGroups)
            {
                int cliffCount = g.Count(d => d.cause == DeathCause.FallOffCliff);
                int trapCount = g.Count(d => d.cause == DeathCause.TrapKill);
                DefectType dtype = cliffCount >= trapCount ? DefectType.DeathCliff : DefectType.DeathTrap;

                allDefects.Add(new DefectEntry
                {
                    position = g.Key,
                    count = g.Count(),
                    type = dtype,
                    cliffCount = cliffCount,
                    trapCount = trapCount
                });
            }
        }

        // 按坐标聚合卡死点
        if (stuckPoints.Count > 0)
        {
            var stuckGroups = stuckPoints
                .GroupBy(s => s.position)
                .OrderByDescending(g => g.Count());

            foreach (var g in stuckGroups)
            {
                allDefects.Add(new DefectEntry
                {
                    position = g.Key,
                    count = g.Count(),
                    type = DefectType.Stuck,
                    cliffCount = 0,
                    trapCount = 0
                });
            }
        }

        if (allDefects.Count == 0)
        {
            Debug.Log("<color=#88FF88>[关卡病灶诊断] 未发现高危区域，关卡设计良好！</color>");
            return;
        }

        // 按频次降序排列，取前 2 个
        var topDefects = allDefects.OrderByDescending(d => d.count).Take(2).ToList();

        string header = "<color=yellow><b>[关卡病灶诊断] 发现高危区域！</b></color>";
        Debug.LogWarning(header);

        for (int i = 0; i < topDefects.Count; i++)
        {
            var defect = topDefects[i];
            int col = defect.position.x;
            int row = defect.position.y;

            string diagnosis = FormatDefectDiagnosis(i + 1, defect, col, row);
            Debug.LogWarning(diagnosis);
        }
    }

    /// <summary>
    /// 格式化单个病灶的大白话诊断文本。
    /// </summary>
    private string FormatDefectDiagnosis(int index, DefectEntry defect, int col, int row)
    {
        string posLabel = $"(X: {col}, Y: {row})";
        string asciiHint = $"【第 {col} 列，从下往上数 第 {row} 行】（最底层地面为第0行）";

        string result;

        switch (defect.type)
        {
            case DefectType.DeathCliff:
                result =
                    $"<color=#FF6666><b>病灶 {index}: Mario 在网格 {posLabel} 连续死亡 {defect.count} 次。</b></color>\n" +
                    $"  ➜️ 对应 ASCII 文本定位：{asciiHint}。\n" +
                    $"  \U0001f4a1 <color=yellow><b>诊断与建议：</b></color>\n" +
                    $"  - 死于坠崖 {defect.cliffCount} 次：此处水平跨度可能 > 4格（突破了 MAX_JUMP_DISTANCE=4.5 的极限）。\n" +
                    $"    建议去 Custom Template Editor 该位置插入 '=' (平台) 或 'B' (弹跳板)。\n" +
                    $"  - 死于机关 {defect.trapCount} 次：机关预警可能过短，建议按 Ctrl+T 在 Game Loop Tuning 拉长其 Windup 滑块。";
                break;

            case DefectType.DeathTrap:
                result =
                    $"<color=#FF6666><b>病灶 {index}: Mario 在网格 {posLabel} 连续死亡 {defect.count} 次。</b></color>\n" +
                    $"  ➜️ 对应 ASCII 文本定位：{asciiHint}。\n" +
                    $"  \U0001f4a1 <color=yellow><b>诊断与建议：</b></color>\n" +
                    $"  - 死于机关 {defect.trapCount} 次：机关预警可能过短，建议按 Ctrl+T 在 Game Loop Tuning 拉长其 Windup 滑块。\n" +
                    $"  - 死于坠崖 {defect.cliffCount} 次：若存在坠崖，去 Custom Template Editor 插入 '=' (平台) 或 'B' (弹跳板)。";
                break;

            case DefectType.Stuck:
                result =
                    $"<color=#FFFF00><b>病灶 {index}: Mario 在网格 {posLabel} 卡死 {defect.count} 次。</b></color>\n" +
                    $"  ➜️ 对应 ASCII 文本定位：{asciiHint}。\n" +
                    $"  \U0001f4a1 <color=yellow><b>诊断与建议：</b></color>\n" +
                    $"  - 此处可能有无法逾越的高墙（落差 > 2.5格，突破了 MAX_JUMP_HEIGHT=2.5 的极限）。\n" +
                    $"    请去 Custom Template Editor 用 '-' 单向平台降低阶梯高度，或挖空墙壁创建通路。\n" +
                    $"  - 也可能是死胡同地形（左右都是墙），建议检查该列前后 2~3 格是否有通路。";
                break;

            default:
                result = "";
                break;
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // 病灶诊断辅助类型
    // ═══════════════════════════════════════════════════════════

    private enum DefectType
    {
        DeathCliff,
        DeathTrap,
        Stuck
    }

    private struct DefectEntry
    {
        public Vector2Int position;
        public int count;
        public DefectType type;
        public int cliffCount;
        public int trapCount;
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
// AnalyticsGizmoRenderer — Scene 视图热力图可视化 (S60 升级版)
//
// 挂载到隐形 GameObject 上，职责：
//   1. 驱动 AutoTestAnalytics.Tick()（每帧卡死检测）
//   2. 通过 SceneView.duringSceneGui 在 Scene 视图绘制热力图：
//      - 死亡点：半透明红色球体 (Handles.SphereHandleCap)
//        同一网格多次死亡 → Alpha 叠加（越死越红）
//        上方 Handles.Label 显示死亡次数 + 死因
//      - 卡死点：黄色警告方块
//   3. 受 ShowTestHeatmap 静态开关控制（由 AIArena Toggle 切换）
//
// [AI防坑警告]
//   - 全部 Editor 绘制逻辑包裹在 #if UNITY_EDITOR 下，Release 零残留
//   - 使用 SceneView.duringSceneGui（与 GameplayBoxVisualizer 同模式）
//   - 不修改任何 transform，纯可视化
// ═══════════════════════════════════════════════════════════════════

public class AnalyticsGizmoRenderer : MonoBehaviour
{
    /// <summary>
    /// 全局开关：是否在 Scene 视图显示测试热力图。
    /// 由 TestConsoleWindow.AIArena 面板的 Toggle 控制。
    /// </summary>
    public static bool ShowTestHeatmap = true;

    private AutoTestAnalytics _analytics;
    private List<DeathRecord> _deathPoints = new List<DeathRecord>();
    private List<StuckRecord> _stuckPoints = new List<StuckRecord>();

    // 聚合后的热力图数据（按网格坐标合并）
    private Dictionary<Vector2Int, HeatmapCell> _deathHeatmap = new Dictionary<Vector2Int, HeatmapCell>();
    private Dictionary<Vector2Int, int> _stuckHeatmap = new Dictionary<Vector2Int, int>();

    private struct HeatmapCell
    {
        public int totalCount;
        public int cliffCount;
        public int trapCount;
    }

    public void SetAnalytics(AutoTestAnalytics analytics)
    {
        _analytics = analytics;
    }

    public void RefreshData(List<DeathRecord> deaths, List<StuckRecord> stucks)
    {
        _deathPoints = new List<DeathRecord>(deaths);
        _stuckPoints = new List<StuckRecord>(stucks);
        RebuildHeatmaps();
    }

    private void RebuildHeatmaps()
    {
        // 聚合死亡点
        _deathHeatmap.Clear();
        foreach (var d in _deathPoints)
        {
            if (!_deathHeatmap.TryGetValue(d.position, out var cell))
            {
                cell = new HeatmapCell();
            }
            cell.totalCount++;
            if (d.cause == DeathCause.FallOffCliff)
                cell.cliffCount++;
            else
                cell.trapCount++;
            _deathHeatmap[d.position] = cell;
        }

        // 聚合卡死点
        _stuckHeatmap.Clear();
        foreach (var s in _stuckPoints)
        {
            if (!_stuckHeatmap.ContainsKey(s.position))
                _stuckHeatmap[s.position] = 0;
            _stuckHeatmap[s.position]++;
        }
    }

    private void Update()
    {
        // 驱动 AutoTestAnalytics 的每帧卡死检测
        if (_analytics != null)
        {
            _analytics.Tick(Time.deltaTime);
        }
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        UnityEditor.SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnDestroy()
    {
        UnityEditor.SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        if (!ShowTestHeatmap) return;
        if (!Application.isPlaying) return;

        DrawDeathHeatmap();
        DrawStuckMarkers();
    }

    private void DrawDeathHeatmap()
    {
        foreach (var kvp in _deathHeatmap)
        {
            Vector2Int gridPos = kvp.Key;
            HeatmapCell cell = kvp.Value;

            Vector3 worldPos = new Vector3(gridPos.x, gridPos.y, 0f);

            // Alpha 随死亡次数叠加：基础 0.3，每次 +0.1，上限 0.95
            float alpha = Mathf.Clamp(0.3f + cell.totalCount * 0.1f, 0.3f, 0.95f);
            // 球体大小随次数增大：基础 0.3，每次 +0.05，上限 0.8
            float radius = Mathf.Clamp(0.3f + cell.totalCount * 0.05f, 0.3f, 0.8f);

            // 绘制半透明红色球体
            UnityEditor.Handles.color = new Color(1f, 0.1f, 0.1f, alpha);
            UnityEditor.Handles.SphereHandleCap(
                0,
                worldPos,
                Quaternion.identity,
                radius * 2f, // SphereHandleCap size = diameter
                EventType.Repaint
            );

            // 绘制标签：死亡次数 + 死因分布
            string causeDetail = "";
            if (cell.cliffCount > 0)
                causeDetail += $"坠崖: {cell.cliffCount}次";
            if (cell.trapCount > 0)
            {
                if (causeDetail.Length > 0) causeDetail += " | ";
                causeDetail += $"机关: {cell.trapCount}次";
            }

            string label = $"☠ {cell.totalCount}次\n{causeDetail}";

            GUIStyle labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.3f, 0.3f, 1f) },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };

            Vector3 labelPos = worldPos + Vector3.up * (radius + 0.4f);
            UnityEditor.Handles.Label(labelPos, label, labelStyle);
        }
    }

    private void DrawStuckMarkers()
    {
        foreach (var kvp in _stuckHeatmap)
        {
            Vector2Int gridPos = kvp.Key;
            int count = kvp.Value;

            Vector3 worldPos = new Vector3(gridPos.x, gridPos.y, 0f);

            // Alpha 随卡死次数叠加
            float alpha = Mathf.Clamp(0.4f + count * 0.15f, 0.4f, 0.95f);
            float size = Mathf.Clamp(0.6f + count * 0.1f, 0.6f, 1.2f);

            // 绘制黄色方块
            UnityEditor.Handles.color = new Color(1f, 0.9f, 0.1f, alpha);
            UnityEditor.Handles.CubeHandleCap(
                0,
                worldPos,
                Quaternion.identity,
                size,
                EventType.Repaint
            );

            // 绘制警告标签
            string label = $"⚠ 卡死 {count}次";

            GUIStyle labelStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(1f, 0.95f, 0.2f, 1f) },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };

            Vector3 labelPos = worldPos + Vector3.up * (size * 0.5f + 0.4f);
            UnityEditor.Handles.Label(labelPos, label, labelStyle);
        }
    }
#endif
}
