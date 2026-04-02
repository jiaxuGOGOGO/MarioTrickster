using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// 自定义测试报告运行器
/// 
/// 功能：一键运行所有测试，将完整错误信息导出到文本文件和 Console
/// 解决问题：Unity Test Runner 默认需要逐个点击才能查看错误详情
/// 
/// 使用方式：
///   菜单栏 → MarioTrickster → Run Tests → Export Full Report (EditMode)
///   菜单栏 → MarioTrickster → Run Tests → Export Full Report (PlayMode)
///   菜单栏 → MarioTrickster → Run Tests → Export Full Report (All)
///   菜单栏 → MarioTrickster → Open Last Test Report
/// 
/// 输出文件：项目根目录/TestReport.txt
/// 
/// 修复记录：
///   Session 13: 初版创建
///   Session 13b: 修复 PlayMode 报告不生成的问题
///     根因：PlayMode 测试会触发 Play 模式切换，导致非持久化的回调实例被销毁
///     修复：使用 [InitializeOnLoad] + 静态构造函数注册持久化回调（ICallbacks），
///           通过 SessionState 跟踪运行状态，确保域重载后回调仍然存活
/// </summary>
[InitializeOnLoad]
public class TestReportRunner
{
    private static readonly string ReportPath = Path.Combine(
        Application.dataPath, "..", "TestReport.txt");

    // SessionState keys（在域重载后保持状态）
    private const string KEY_IS_RUNNING = "TestReportRunner_IsRunning";
    private const string KEY_RUN_LABEL = "TestReportRunner_RunLabel";
    private const string KEY_RUN_MODE = "TestReportRunner_RunMode";
    private const string KEY_PENDING_PLAYMODE = "TestReportRunner_PendingPlayMode";

    // 持久化回调实例（通过静态构造函数注册，域重载后重新注册）
    private static PersistentTestCallbacks _callbacks;

    // ═══════════════════════════════════════════════════════
    // 静态构造函数：每次域重载都会执行，确保回调始终注册
    // ═══════════════════════════════════════════════════════

    static TestReportRunner()
    {
        _callbacks = new PersistentTestCallbacks();
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(_callbacks);
    }

    // ═══════════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)", false, 100)]
    public static void RunEditModeTests()
    {
        RunTests(TestMode.EditMode, "EditMode", false);
    }

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)", false, 101)]
    public static void RunPlayModeTests()
    {
        RunTests(TestMode.PlayMode, "PlayMode", false);
    }

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (All)", false, 102)]
    public static void RunAllTests()
    {
        RunTests(TestMode.EditMode, "EditMode+PlayMode (Phase 1: EditMode)", true);
    }

    [MenuItem("MarioTrickster/Open Last Test Report", false, 200)]
    public static void OpenLastReport()
    {
        if (File.Exists(ReportPath))
        {
            EditorUtility.RevealInFinder(ReportPath);
            string content = File.ReadAllText(ReportPath);
            Debug.Log("═══ TestReport.txt 内容 ═══\n" + content);
        }
        else
        {
            EditorUtility.DisplayDialog("测试报告",
                "TestReport.txt 不存在。\n请先运行测试：MarioTrickster → Run Tests",
                "确定");
        }
    }

    // ═══════════════════════════════════════════════════════
    // 测试运行逻辑
    // ═══════════════════════════════════════════════════════

    private static void RunTests(TestMode mode, string label, bool pendingPlayMode)
    {
        // 通过 SessionState 保存运行状态（域重载后仍可读取）
        SessionState.SetBool(KEY_IS_RUNNING, true);
        SessionState.SetString(KEY_RUN_LABEL, label);
        SessionState.SetInt(KEY_RUN_MODE, (int)mode);
        SessionState.SetBool(KEY_PENDING_PLAYMODE, pendingPlayMode);

        Debug.Log($"[TestReportRunner] 开始运行 {label} 测试...");

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter()
        {
            testMode = mode
        };
        api.Execute(new ExecutionSettings(filter));
    }

    // ═══════════════════════════════════════════════════════
    // 持久化回调处理器
    // 通过静态构造函数注册，域重载后自动重新注册
    // ═══════════════════════════════════════════════════════

    private class PersistentTestCallbacks : ICallbacks
    {
        private readonly List<TestResult> _results = new List<TestResult>();
        private DateTime _startTime;

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _startTime = DateTime.Now;
            _results.Clear();

            string label = SessionState.GetString(KEY_RUN_LABEL, "Unknown");
            Debug.Log($"[TestReportRunner] {label} 测试开始运行...");
        }

        public void TestStarted(ITestAdaptor test)
        {
            // 不需要处理
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            // 只收集叶子节点（实际的测试方法，不是测试类/套件）
            if (!result.HasChildren)
            {
                _results.Add(new TestResult
                {
                    FullName = result.FullName,
                    Name = result.Name,
                    ResultState = result.ResultState,
                    TestStatus = result.TestStatus,
                    Message = result.Message,
                    StackTrace = result.StackTrace,
                    Duration = result.Duration,
                    Output = result.Output
                });
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            // 检查是否是我们触发的运行
            bool isOurRun = SessionState.GetBool(KEY_IS_RUNNING, false);
            if (!isOurRun) return;

            var elapsed = DateTime.Now - _startTime;
            string label = SessionState.GetString(KEY_RUN_LABEL, "Unknown");
            int modeInt = SessionState.GetInt(KEY_RUN_MODE, 0);
            bool pendingPlayMode = SessionState.GetBool(KEY_PENDING_PLAYMODE, false);
            TestMode mode = (TestMode)modeInt;

            Debug.Log($"[TestReportRunner] {label} 运行完成，收集到 {_results.Count} 个测试结果");

            // 生成报告
            GenerateReport(new List<TestResult>(_results), label, elapsed, mode);

            // 如果是 "All" 模式的第一阶段（EditMode），继续运行 PlayMode
            if (pendingPlayMode && mode == TestMode.EditMode)
            {
                // 保存 EditMode 结果到临时文件
                SaveTempResults(_results);

                Debug.Log("[TestReportRunner] EditMode 完成，开始运行 PlayMode...");
                RunTests(TestMode.PlayMode, "EditMode+PlayMode (Phase 2: PlayMode)", false);
                return;
            }

            // 如果有保存的 EditMode 临时结果，合并生成完整报告
            var editModeResults = LoadTempResults();
            if (editModeResults != null && mode == TestMode.PlayMode)
            {
                var allResults = new List<TestResult>(editModeResults);
                allResults.AddRange(_results);
                GenerateReport(allResults, "All Tests (EditMode + PlayMode)", elapsed, null);
                ClearTempResults();
            }

            // 清除运行状态
            SessionState.SetBool(KEY_IS_RUNNING, false);
        }
    }

    // ═══════════════════════════════════════════════════════
    // 临时结果存储（用于 All 模式的 EditMode→PlayMode 合并）
    // ═══════════════════════════════════════════════════════

    private static readonly string TempResultsPath = Path.Combine(
        Application.dataPath, "..", "Temp", "TestReportRunner_EditModeResults.json");

    private static void SaveTempResults(List<TestResult> results)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var r in results)
            {
                // 简单的行分隔格式存储
                sb.AppendLine($"RESULT_START");
                sb.AppendLine($"FullName={r.FullName}");
                sb.AppendLine($"Name={r.Name}");
                sb.AppendLine($"ResultState={r.ResultState}");
                sb.AppendLine($"TestStatus={(int)r.TestStatus}");
                sb.AppendLine($"Duration={r.Duration}");
                sb.AppendLine($"Message={EscapeNewlines(r.Message)}");
                sb.AppendLine($"StackTrace={EscapeNewlines(r.StackTrace)}");
                sb.AppendLine($"Output={EscapeNewlines(r.Output)}");
                sb.AppendLine($"RESULT_END");
            }
            File.WriteAllText(TempResultsPath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TestReportRunner] 保存临时结果失败: {e.Message}");
        }
    }

    private static List<TestResult> LoadTempResults()
    {
        if (!File.Exists(TempResultsPath)) return null;

        try
        {
            var results = new List<TestResult>();
            var lines = File.ReadAllLines(TempResultsPath);
            TestResult current = default;
            bool inResult = false;

            foreach (var line in lines)
            {
                if (line == "RESULT_START")
                {
                    current = new TestResult();
                    inResult = true;
                }
                else if (line == "RESULT_END" && inResult)
                {
                    results.Add(current);
                    inResult = false;
                }
                else if (inResult)
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string key = line.Substring(0, eq);
                        string val = line.Substring(eq + 1);
                        switch (key)
                        {
                            case "FullName": current.FullName = val; break;
                            case "Name": current.Name = val; break;
                            case "ResultState": current.ResultState = val; break;
                            case "TestStatus": current.TestStatus = (TestStatus)int.Parse(val); break;
                            case "Duration": current.Duration = double.Parse(val); break;
                            case "Message": current.Message = UnescapeNewlines(val); break;
                            case "StackTrace": current.StackTrace = UnescapeNewlines(val); break;
                            case "Output": current.Output = UnescapeNewlines(val); break;
                        }
                    }
                }
            }
            return results.Count > 0 ? results : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TestReportRunner] 加载临时结果失败: {e.Message}");
            return null;
        }
    }

    private static void ClearTempResults()
    {
        if (File.Exists(TempResultsPath))
            File.Delete(TempResultsPath);
    }

    private static string EscapeNewlines(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string UnescapeNewlines(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // 简单反转义
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char next = s[i + 1];
                if (next == 'n') { sb.Append('\n'); i++; }
                else if (next == 'r') { sb.Append('\r'); i++; }
                else if (next == '\\') { sb.Append('\\'); i++; }
                else sb.Append(s[i]);
            }
            else
            {
                sb.Append(s[i]);
            }
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════════

    private struct TestResult
    {
        public string FullName;
        public string Name;
        public string ResultState;
        public TestStatus TestStatus;
        public string Message;
        public string StackTrace;
        public double Duration;
        public string Output;
    }

    // ═══════════════════════════════════════════════════════
    // 报告生成
    // ═══════════════════════════════════════════════════════

    private static void GenerateReport(List<TestResult> results, string label,
        TimeSpan elapsed, TestMode? mode)
    {
        var sb = new StringBuilder();
        var passed = new List<TestResult>();
        var failed = new List<TestResult>();
        var skipped = new List<TestResult>();
        var other = new List<TestResult>();

        foreach (var r in results)
        {
            switch (r.TestStatus)
            {
                case TestStatus.Passed:
                    passed.Add(r);
                    break;
                case TestStatus.Failed:
                    failed.Add(r);
                    break;
                case TestStatus.Skipped:
                    skipped.Add(r);
                    break;
                default:
                    other.Add(r);
                    break;
            }
        }

        // ── 报告头部 ──
        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║          MarioTrickster 测试报告 (Test Report)              ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"  测试类型: {label}");
        sb.AppendLine($"  运行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  耗时: {elapsed.TotalSeconds:F1} 秒");
        sb.AppendLine();

        // ── 统计摘要 ──
        sb.AppendLine("┌─────────────────────────────────────────┐");
        sb.AppendLine($"│  总计: {results.Count} 个测试");
        sb.AppendLine($"│  ✅ 通过: {passed.Count}");
        sb.AppendLine($"│  ❌ 失败: {failed.Count}");
        sb.AppendLine($"│  ⏭ 跳过: {skipped.Count}");
        if (other.Count > 0)
            sb.AppendLine($"│  ❓ 其他: {other.Count}");
        sb.AppendLine("└─────────────────────────────────────────┘");
        sb.AppendLine();

        // ── 失败测试详情（核心部分）──
        if (failed.Count > 0)
        {
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  ❌ 失败测试详情 ({failed.Count} 个)");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            for (int i = 0; i < failed.Count; i++)
            {
                var f = failed[i];
                sb.AppendLine($"──── 失败 #{i + 1}: {f.FullName} ────");
                sb.AppendLine($"  状态: {f.ResultState}");
                sb.AppendLine($"  耗时: {f.Duration:F3}s");

                if (!string.IsNullOrEmpty(f.Message))
                {
                    sb.AppendLine($"  错误信息:");
                    foreach (var line in f.Message.Split('\n'))
                    {
                        sb.AppendLine($"    {line.TrimEnd()}");
                    }
                }

                if (!string.IsNullOrEmpty(f.StackTrace))
                {
                    sb.AppendLine($"  堆栈跟踪:");
                    foreach (var line in f.StackTrace.Split('\n'))
                    {
                        sb.AppendLine($"    {line.TrimEnd()}");
                    }
                }

                if (!string.IsNullOrEmpty(f.Output))
                {
                    sb.AppendLine($"  输出:");
                    foreach (var line in f.Output.Split('\n'))
                    {
                        sb.AppendLine($"    {line.TrimEnd()}");
                    }
                }

                sb.AppendLine();
            }
        }

        // ── 跳过的测试 ──
        if (skipped.Count > 0)
        {
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  ⏭ 跳过的测试 ({skipped.Count} 个)");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var s in skipped)
            {
                sb.AppendLine($"  - {s.FullName}");
                if (!string.IsNullOrEmpty(s.Message))
                    sb.AppendLine($"    原因: {s.Message.Trim()}");
            }
            sb.AppendLine();
        }

        // ── 通过的测试列表 ──
        sb.AppendLine("══════════════════════════════════════════════════════════════");
        sb.AppendLine($"  ✅ 通过的测试 ({passed.Count} 个)");
        sb.AppendLine("══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        foreach (var p in passed)
        {
            sb.AppendLine($"  ✅ {p.FullName} ({p.Duration:F3}s)");
        }
        sb.AppendLine();

        // ── 其他状态 ──
        if (other.Count > 0)
        {
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  ❓ 其他状态 ({other.Count} 个)");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            foreach (var o in other)
            {
                sb.AppendLine($"  - {o.FullName}: {o.ResultState}");
                if (!string.IsNullOrEmpty(o.Message))
                    sb.AppendLine($"    信息: {o.Message.Trim()}");
            }
            sb.AppendLine();
        }

        // ── 快速复制区（给 AI 看的精简版）──
        if (failed.Count > 0)
        {
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine("  📋 快速复制区（发给 AI 修复用）");
            sb.AppendLine("══════════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("请将以下内容复制发送给 AI：");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine($"测试报告 - {failed.Count} 个失败 / {passed.Count} 个通过 / {results.Count} 个总计");
            sb.AppendLine();

            for (int i = 0; i < failed.Count; i++)
            {
                var f = failed[i];
                sb.AppendLine($"[失败 {i + 1}] {f.FullName}");
                if (!string.IsNullOrEmpty(f.Message))
                    sb.AppendLine($"  错误: {f.Message.Trim()}");
                if (!string.IsNullOrEmpty(f.StackTrace))
                {
                    var stackLines = f.StackTrace.Split('\n');
                    int maxLines = Math.Min(3, stackLines.Length);
                    for (int j = 0; j < maxLines; j++)
                    {
                        if (!string.IsNullOrWhiteSpace(stackLines[j]))
                            sb.AppendLine($"  堆栈: {stackLines[j].Trim()}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("```");
            sb.AppendLine();
        }

        // ── 报告尾部 ──
        sb.AppendLine("══════════════════════════════════════════════════════════════");
        sb.AppendLine("  报告生成完毕");
        sb.AppendLine($"  文件位置: {ReportPath}");
        sb.AppendLine("══════════════════════════════════════════════════════════════");

        // 写入文件
        string report = sb.ToString();
        try
        {
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            Debug.Log($"[TestReportRunner] 报告已写入: {ReportPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TestReportRunner] 写入报告文件失败: {e.Message}");
        }

        // 在 Console 中输出完整报告
        if (failed.Count > 0)
        {
            Debug.LogError($"[TestReportRunner] ❌ {failed.Count} 个测试失败！完整报告已保存到 TestReport.txt\n\n{report}");
        }
        else
        {
            Debug.Log($"[TestReportRunner] ✅ 全部 {passed.Count} 个测试通过！报告已保存到 TestReport.txt\n\n{report}");
        }

        // 延迟弹出提示（避免在 PlayMode 退出过程中弹窗被吞）
        EditorApplication.delayCall += () =>
        {
            if (failed.Count > 0)
            {
                EditorUtility.DisplayDialog("测试完成",
                    $"❌ {failed.Count} 个测试失败，{passed.Count} 个通过\n\n" +
                    $"完整报告已保存到：\n{ReportPath}\n\n" +
                    "请将 TestReport.txt 内容复制发送给 AI 统一修复。\n" +
                    "也可以在 Console 中查看完整输出。",
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("测试完成",
                    $"✅ 全部 {passed.Count} 个测试通过！\n\n" +
                    $"报告已保存到：\n{ReportPath}",
                    "确定");
            }
        };
    }
}
