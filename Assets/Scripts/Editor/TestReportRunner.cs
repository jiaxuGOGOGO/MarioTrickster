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
/// 工作流程：
///   1. 用户点击菜单运行测试
///   2. 测试完成后自动生成 TestReport.txt
///   3. 用户将 TestReport.txt 内容复制给 AI
///   4. AI 根据完整错误信息统一修复
/// </summary>
[InitializeOnLoad]
public class TestReportRunner
{
    private static readonly string ReportPath = Path.Combine(
        Application.dataPath, "..", "TestReport.txt");

    // ═══════════════════════════════════════════════════════
    // 菜单入口
    // ═══════════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)", false, 100)]
    public static void RunEditModeTests()
    {
        RunTests(TestMode.EditMode, "EditMode");
    }

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)", false, 101)]
    public static void RunPlayModeTests()
    {
        RunTests(TestMode.PlayMode, "PlayMode");
    }

    [MenuItem("MarioTrickster/Run Tests/Export Full Report (All)", false, 102)]
    public static void RunAllTests()
    {
        // 先运行 EditMode，完成后自动运行 PlayMode
        _pendingPlayMode = true;
        RunTests(TestMode.EditMode, "EditMode+PlayMode (Phase 1: EditMode)");
    }

    [MenuItem("MarioTrickster/Open Last Test Report", false, 200)]
    public static void OpenLastReport()
    {
        if (File.Exists(ReportPath))
        {
            EditorUtility.RevealInFinder(ReportPath);
            // 同时在 Console 输出内容
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

    private static bool _pendingPlayMode = false;
    private static string _currentRunLabel = "";

    private static void RunTests(TestMode mode, string label)
    {
        _currentRunLabel = label;

        Debug.Log($"[TestReportRunner] 开始运行 {label} 测试...");

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new TestReportCallbacks(mode, label);
        api.RegisterCallbacks(callbacks);

        var filter = new Filter()
        {
            testMode = mode
        };

        api.Execute(new ExecutionSettings(filter));
    }

    // ═══════════════════════════════════════════════════════
    // 回调处理器：收集所有测试结果
    // ═══════════════════════════════════════════════════════

    private class TestReportCallbacks : ICallbacks
    {
        private readonly TestMode _mode;
        private readonly string _label;
        private readonly List<TestResult> _results = new List<TestResult>();
        private DateTime _startTime;

        public TestReportCallbacks(TestMode mode, string label)
        {
            _mode = mode;
            _label = label;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            _startTime = DateTime.Now;
            _results.Clear();
            Debug.Log($"[TestReportRunner] {_label} 测试开始运行...");
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
            var elapsed = DateTime.Now - _startTime;

            // 生成报告
            GenerateReport(_results, _label, elapsed, _mode);

            // 如果是 "All" 模式的第一阶段（EditMode），继续运行 PlayMode
            if (_pendingPlayMode && _mode == TestMode.EditMode)
            {
                _pendingPlayMode = false;

                // 保存 EditMode 结果，然后运行 PlayMode
                _editModeResults = new List<TestResult>(_results);

                Debug.Log("[TestReportRunner] EditMode 完成，开始运行 PlayMode...");
                RunTests(TestMode.PlayMode, "EditMode+PlayMode (Phase 2: PlayMode)");
                return;
            }

            // 如果有保存的 EditMode 结果，合并生成完整报告
            if (_editModeResults != null && _mode == TestMode.PlayMode)
            {
                var allResults = new List<TestResult>(_editModeResults);
                allResults.AddRange(_results);
                GenerateReport(allResults, "All Tests (EditMode + PlayMode)", elapsed, null);
                _editModeResults = null;
            }
        }
    }

    private static List<TestResult> _editModeResults = null;

    // ═══════════════════════════════════════════════════════
    // 报告生成
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
        sb.AppendLine($"│  总计: {results.Count} 个测试                        │");
        sb.AppendLine($"│  ✅ 通过: {passed.Count}                              │");
        sb.AppendLine($"│  ❌ 失败: {failed.Count}                              │");
        sb.AppendLine($"│  ⏭ 跳过: {skipped.Count}                              │");
        if (other.Count > 0)
            sb.AppendLine($"│  ❓ 其他: {other.Count}                              │");
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
                    // 缩进每一行
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
                    // 只取堆栈的前 3 行（最关键的部分）
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
        File.WriteAllText(ReportPath, report, Encoding.UTF8);

        // 在 Console 中输出完整报告
        if (failed.Count > 0)
        {
            Debug.LogError($"[TestReportRunner] ❌ {failed.Count} 个测试失败！完整报告已保存到 TestReport.txt\n\n{report}");
        }
        else
        {
            Debug.Log($"[TestReportRunner] ✅ 全部 {passed.Count} 个测试通过！报告已保存到 TestReport.txt\n\n{report}");
        }

        // 弹出提示
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
    }
}
