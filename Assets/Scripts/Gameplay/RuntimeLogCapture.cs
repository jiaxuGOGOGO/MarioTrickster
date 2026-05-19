using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;

// ═══════════════════════════════════════════════════════════════════
// RuntimeLogCapture — 运行时全量日志捕获器
//
// 职责：
//   在 AI 自动测试期间，捕获 Unity Console 的所有日志输出
//   （Debug.Log / LogWarning / LogError / Exception），
//   按时间戳存储，供战报导出时附带完整运行时上下文。
//
// 设计原则：
//   - 使用 Application.logMessageReceived 钩子，零侵入捕获
//   - 环形缓冲区设计，防止长时间挂机导致内存溢出
//   - 分级存储：Info / Warning / Error+Exception 分别计数
//   - 导出时可选择全量或仅 Warning+Error
//
// 使用方式：
//   var capture = new RuntimeLogCapture(maxEntries: 2000);
//   capture.StartCapture();
//   // ... 运行测试 ...
//   capture.StopCapture();
//   string log = capture.ExportAsText();
//   List<LogEntry> entries = capture.GetEntries();
//
// [AI防坑警告]
//   - 必须在不需要时调用 StopCapture() 解除钩子，否则会内存泄漏
//   - logMessageReceived 在主线程回调，不会有线程安全问题
//   - 环形缓冲区满时自动丢弃最早的条目
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 运行时全量日志捕获器。
/// 通过 Application.logMessageReceived 钩子捕获所有 Console 输出。
/// </summary>
public class RuntimeLogCapture
{
    // ═══════════════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════════════

    [Serializable]
    public struct LogEntry
    {
        public float timestamp;
        public string level;
        public string message;
        public string stackTrace;
    }

    // ═══════════════════════════════════════════════════════════
    // 配置与状态
    // ═══════════════════════════════════════════════════════════

    private readonly int _maxEntries;
    private readonly List<LogEntry> _entries;
    private bool _isCapturing;
    private float _startTime;

    // ── 统计计数 ──
    private int _infoCount;
    private int _warningCount;
    private int _errorCount;

    // ═══════════════════════════════════════════════════════════
    // 公开属性
    // ═══════════════════════════════════════════════════════════

    /// <summary>是否正在捕获</summary>
    public bool IsCapturing => _isCapturing;

    /// <summary>已捕获的日志总条数</summary>
    public int TotalCount => _entries.Count;

    /// <summary>Info 级别日志数</summary>
    public int InfoCount => _infoCount;

    /// <summary>Warning 级别日志数</summary>
    public int WarningCount => _warningCount;

    /// <summary>Error + Exception 级别日志数</summary>
    public int ErrorCount => _errorCount;

    // ═══════════════════════════════════════════════════════════
    // 构造
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 创建日志捕获器。
    /// </summary>
    /// <param name="maxEntries">环形缓冲区最大容量（默认 2000 条）</param>
    public RuntimeLogCapture(int maxEntries = 2000)
    {
        _maxEntries = maxEntries;
        _entries = new List<LogEntry>(maxEntries);
    }

    // ═══════════════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════════════

    /// <summary>开始捕获日志</summary>
    public void StartCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;
        _startTime = Time.realtimeSinceStartup;
        Application.logMessageReceived += OnLogReceived;
    }

    /// <summary>停止捕获日志</summary>
    public void StopCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        Application.logMessageReceived -= OnLogReceived;
    }

    /// <summary>清空所有已捕获的日志</summary>
    public void Clear()
    {
        _entries.Clear();
        _infoCount = 0;
        _warningCount = 0;
        _errorCount = 0;
    }

    // ═══════════════════════════════════════════════════════════
    // 数据访问
    // ═══════════════════════════════════════════════════════════

    /// <summary>获取所有已捕获的日志条目（只读副本）</summary>
    public List<LogEntry> GetEntries()
    {
        return new List<LogEntry>(_entries);
    }

    /// <summary>
    /// 获取指定级别以上的日志条目。
    /// </summary>
    /// <param name="minLevel">最低级别："Info" / "Warning" / "Error"</param>
    public List<LogEntry> GetEntries(string minLevel)
    {
        if (string.Equals(minLevel, "Info", StringComparison.OrdinalIgnoreCase))
            return new List<LogEntry>(_entries);

        bool includeWarning = string.Equals(minLevel, "Warning", StringComparison.OrdinalIgnoreCase);
        var filtered = new List<LogEntry>();
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.level == "Error" || entry.level == "Exception")
            {
                filtered.Add(entry);
            }
            else if (includeWarning && entry.level == "Warning")
            {
                filtered.Add(entry);
            }
        }
        return filtered;
    }

    /// <summary>
    /// 导出为纯文本格式（适合直接写入文件或附加到战报）。
    /// </summary>
    /// <param name="minLevel">最低级别："Info" / "Warning" / "Error"</param>
    /// <param name="maxLines">最大输出行数（0 = 不限制）</param>
    public string ExportAsText(string minLevel = "Info", int maxLines = 0)
    {
        var entries = GetEntries(minLevel);
        var sb = new StringBuilder();
        sb.AppendLine($"=== Runtime Log Capture ===");
        sb.AppendLine($"Total: {_entries.Count} | Info: {_infoCount} | Warning: {_warningCount} | Error: {_errorCount}");
        sb.AppendLine($"Capture duration: {(_isCapturing ? Time.realtimeSinceStartup - _startTime : 0f):F1}s");
        sb.AppendLine("---");

        int count = maxLines > 0 ? Math.Min(entries.Count, maxLines) : entries.Count;
        for (int i = 0; i < count; i++)
        {
            var e = entries[i];
            sb.AppendLine($"[{e.timestamp:F2}s] [{e.level}] {e.message}");
            if (e.level == "Error" || e.level == "Exception")
            {
                if (!string.IsNullOrEmpty(e.stackTrace))
                    sb.AppendLine($"    StackTrace: {e.stackTrace.Replace("\n", "\n    ")}");
            }
        }

        if (maxLines > 0 && entries.Count > maxLines)
            sb.AppendLine($"... ({entries.Count - maxLines} more entries omitted)");

        return sb.ToString();
    }

    /// <summary>
    /// 导出为 JSON 数组格式的字符串（供 LLM 分析）。
    /// 仅包含 Warning + Error 级别，避免 token 浪费。
    /// </summary>
    public string ExportAsJsonArray()
    {
        var entries = GetEntries("Warning");
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(",");
            var e = entries[i];
            sb.Append("{");
            sb.Append($"\"t\":{e.timestamp:F2},");
            sb.Append($"\"level\":\"{EscapeJson(e.level)}\",");
            sb.Append($"\"msg\":\"{EscapeJson(e.message)}\"");
            if (e.level == "Error" || e.level == "Exception")
            {
                sb.Append($",\"stack\":\"{EscapeJson(e.stackTrace ?? "")}\"");
            }
            sb.Append("}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════
    // 内部实现
    // ═══════════════════════════════════════════════════════════

    private void OnLogReceived(string message, string stackTrace, LogType type)
    {
        string level;
        switch (type)
        {
            case LogType.Error:
                level = "Error";
                _errorCount++;
                break;
            case LogType.Exception:
                level = "Exception";
                _errorCount++;
                break;
            case LogType.Warning:
                level = "Warning";
                _warningCount++;
                break;
            case LogType.Assert:
                level = "Assert";
                _errorCount++;
                break;
            default:
                level = "Info";
                _infoCount++;
                break;
        }

        var entry = new LogEntry
        {
            timestamp = Time.realtimeSinceStartup - _startTime,
            level = level,
            message = message,
            stackTrace = (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                ? stackTrace : null
        };

        // 环形缓冲区：满了就移除最早的
        if (_entries.Count >= _maxEntries)
            _entries.RemoveAt(0);

        _entries.Add(entry);
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
