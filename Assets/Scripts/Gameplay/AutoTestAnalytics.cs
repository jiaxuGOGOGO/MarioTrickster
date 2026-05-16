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
//
// 生命周期：
//   - 由 TestConsoleWindow（AI Auto-Arena 折叠栏）在开启挂机时创建
//   - 关闭挂机时销毁
//   - 不依赖 MonoBehaviour，纯 C# 类，手动订阅/退订事件
// ═══════════════════════════════════════════════════════════════════

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
    // 公开属性（供 Editor UI 实时显示）
    // ═══════════════════════════════════════════════════════════

    public int TotalMatches => totalMatches;
    public int MarioWins => marioWins;
    public int TricksterWins => tricksterWins;
    public float MarioWinRate => totalMatches > 0 ? (float)marioWins / totalMatches * 100f : 0f;
    public float TricksterWinRate => totalMatches > 0 ? (float)tricksterWins / totalMatches * 100f : 0f;
    public float AverageMatchTime => totalMatches > 0 ? totalMatchTime / totalMatches : 0f;

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

        // 如果当前已经在 Playing 状态，标记比赛开始
        if (_gameManager.CurrentState == GameState.Playing)
        {
            matchInProgress = true;
            currentMatchStartTime = Time.unscaledTime;
        }

        Debug.Log("[AutoTestAnalytics] Data collection started.");
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
        _subscribed = false;
        matchInProgress = false;

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
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Playing && !matchInProgress)
        {
            matchInProgress = true;
            currentMatchStartTime = Time.unscaledTime;
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
    // 战报输出
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 在 Unity Console 输出汇总战报。
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

        string report =
            "=== \U0001f916 AI \u81ea\u52a8\u5bf9\u6218\u6d4b\u8bd5\u62a5\u544a ===\n" +
            $"\u5bf9\u6218\u603b\u5c40\u6570: {totalMatches} | Mario \u80dc\u7387: {MarioWinRate:F0}% | Trickster \u80dc\u7387: {TricksterWinRate:F0}%\n" +
            $"\u6700\u81f4\u547d\u9677\u9631: {deadliestTrap} (\u89e6\u53d1 {deadliestCount} \u6b21)\n" +
            $"\u5e73\u5747\u5355\u5c40\u8017\u65f6: {AverageMatchTime:F1} \u79d2";

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
