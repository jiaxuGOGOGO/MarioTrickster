using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AI Arena structured report exporter.
///
/// Exports AutoTestAnalytics runtime data into repo-root docs/validation files:
///   - auto_test_metrics_YYYY-MM-DD.json: machine-readable structured metrics
///   - auto_test_summary.md: human-readable Markdown summary table
/// </summary>
public static class AIArenaReportExporter
{
    private const string ValidationDirectoryName = "validation";
    private const string SummaryFileName = "auto_test_summary.md";

    /// <summary>
    /// Export the latest AI Auto-Arena match report to JSON and Markdown.
    /// Returns the generated Markdown absolute path so callers can log an opened-link hint.
    /// </summary>
    public static string ExportMatchReport(AutoTestAnalytics analytics)
    {
        if (analytics == null)
        {
            Debug.LogWarning("[AIArenaReportExporter] No analytics instance to export.");
            return null;
        }

        try
        {
            string validationDir = GetValidationDirectory();
            Directory.CreateDirectory(validationDir);

            var reportData = BuildReportData(analytics);
            string dateStamp = DateTime.Now.ToString("yyyy-MM-dd");
            string jsonPath = Path.Combine(validationDir, $"auto_test_metrics_{dateStamp}.json");
            string mdPath = Path.Combine(validationDir, SummaryFileName);

            string json = JsonUtility.ToJson(reportData, true);
            File.WriteAllText(jsonPath, json, Encoding.UTF8);

            string markdown = BuildMarkdownSummary(reportData, jsonPath, mdPath);
            File.WriteAllText(mdPath, markdown, Encoding.UTF8);

            AssetDatabase.Refresh();
            Debug.Log($"[AIArenaReportExporter] Exported AI Arena report:\nJSON: {jsonPath}\nMarkdown: {mdPath}");

            // --- Legacy Export (for AITestAnalystWindow) ---
            // 统一在此调用老逻辑，确保 AI Test Analyst 面板可读取
            analytics.ExportReportToJson();

            return mdPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AIArenaReportExporter] Failed to export report: {e.Message}\n{e.StackTrace}");
            return null;
        }
    }

    private static string GetValidationDirectory()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, "docs", ValidationDirectoryName);
    }

    private static AIArenaReportData BuildReportData(AutoTestAnalytics analytics)
    {
        var trapStats = analytics.GetTrapStats();
        string deadliestTrap = "N/A";
        int deadliestTrapTriggerCount = 0;

        if (trapStats != null && trapStats.Count > 0)
        {
            var topTrap = trapStats.OrderByDescending(kv => kv.Value).First();
            deadliestTrap = topTrap.Key;
            deadliestTrapTriggerCount = topTrap.Value;
        }

        return new AIArenaReportData
        {
            TotalMatches = analytics.TotalMatches,
            MarioWinRate = RoundOneDecimal(analytics.MarioWinRate),
            TricksterWinRate = RoundOneDecimal(analytics.TricksterWinRate),
            AverageMatchTime = RoundOneDecimal(analytics.AverageMatchTime),
            DeadliestTrap = deadliestTrap,
            DeadliestTrapTriggerCount = deadliestTrapTriggerCount,
            DeathPoints = analytics.DeathPoints
                .Select(d => new SpatialPointRecord
                {
                    position = new GridPosition { x = d.position.x, y = d.position.y },
                    cause = FormatDeathCause(d.cause),
                    matchIndex = d.matchIndex,
                    count = 1
                })
                .ToArray(),
            StuckPoints = analytics.StuckPoints
                .Select(s => new SpatialPointRecord
                {
                    position = new GridPosition { x = s.position.x, y = s.position.y },
                    cause = "Stuck",
                    matchIndex = s.matchIndex,
                    stuckDuration = RoundOneDecimal(s.stuckDuration),
                    count = 1
                })
                .ToArray()
        };
    }

    private static string BuildMarkdownSummary(AIArenaReportData data, string jsonPath, string mdPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AI Auto-Arena Structured Test Summary");
        sb.AppendLine();
        sb.AppendLine($"> Generated at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"> JSON metrics: `{ToProjectRelativePath(jsonPath)}`");
        sb.AppendLine();

        sb.AppendLine("## Match Metrics");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| TotalMatches | {data.TotalMatches} |");
        sb.AppendLine($"| MarioWinRate | {data.MarioWinRate:F1}% |");
        sb.AppendLine($"| TricksterWinRate | {data.TricksterWinRate:F1}% |");
        sb.AppendLine($"| AverageMatchTime | {data.AverageMatchTime:F1}s |");
        sb.AppendLine($"| DeadliestTrap | {EscapeMarkdownCell(data.DeadliestTrap)} |");
        sb.AppendLine($"| DeadliestTrapTriggerCount | {data.DeadliestTrapTriggerCount} |");
        sb.AppendLine();

        sb.AppendLine("## Spatial Defects");
        sb.AppendLine();
        sb.AppendLine("| Type | Count |");
        sb.AppendLine("|---|---:|");
        sb.AppendLine($"| DeathPoints | {data.DeathPoints.Length} |");
        sb.AppendLine($"| StuckPoints | {data.StuckPoints.Length} |");
        sb.AppendLine();

        AppendSpatialPointTable(sb, "DeathPoints", data.DeathPoints, includeStuckDuration: false);
        AppendSpatialPointTable(sb, "StuckPoints", data.StuckPoints, includeStuckDuration: true);

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"Markdown file: `{ToProjectRelativePath(mdPath)}`");
        return sb.ToString();
    }

    private static void AppendSpatialPointTable(StringBuilder sb, string title, SpatialPointRecord[] points, bool includeStuckDuration)
    {
        sb.AppendLine($"## {title}");
        sb.AppendLine();

        if (points == null || points.Length == 0)
        {
            sb.AppendLine("No records.");
            sb.AppendLine();
            return;
        }

        if (includeStuckDuration)
        {
            sb.AppendLine("| # | position | cause | matchIndex | stuckDuration |");
            sb.AppendLine("|---:|---|---|---:|---:|");
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                sb.AppendLine($"| {i + 1} | ({p.position.x}, {p.position.y}) | {EscapeMarkdownCell(p.cause)} | {p.matchIndex} | {p.stuckDuration:F1}s |");
            }
        }
        else
        {
            sb.AppendLine("| # | position | cause | matchIndex |");
            sb.AppendLine("|---:|---|---|---:|");
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                sb.AppendLine($"| {i + 1} | ({p.position.x}, {p.position.y}) | {EscapeMarkdownCell(p.cause)} | {p.matchIndex} |");
            }
        }

        sb.AppendLine();
    }

    private static float RoundOneDecimal(float value)
    {
        return Mathf.Round(value * 10f) / 10f;
    }

    private static string FormatDeathCause(DeathCause cause)
    {
        return cause == DeathCause.FallOffCliff ? "FallOffCliff" : "TrapKill";
    }

    private static string EscapeMarkdownCell(string value)
    {
        if (string.IsNullOrEmpty(value)) return "N/A";
        return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    private static string ToProjectRelativePath(string absolutePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string fullPath = Path.GetFullPath(absolutePath);
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            return fullPath;

        return fullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    [Serializable]
    private class AIArenaReportData
    {
        public int TotalMatches;
        public float MarioWinRate;
        public float TricksterWinRate;
        public float AverageMatchTime;
        public string DeadliestTrap;
        public int DeadliestTrapTriggerCount;
        public SpatialPointRecord[] DeathPoints;
        public SpatialPointRecord[] StuckPoints;
    }

    [Serializable]
    private class SpatialPointRecord
    {
        public GridPosition position;
        public string cause;
        public int matchIndex;
        public int count;
        public float stuckDuration;
    }

    [Serializable]
    private class GridPosition
    {
        public int x;
        public int y;
    }
}
