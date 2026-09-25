using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 设计宪法 H4「AI 不作弊」：马里奥只能凭视线、可见痕迹、扫描来知道捣蛋者在哪。
/// 这组测试守住两件事：
///   1. Mario 决策代码里不出现直接读取附身真值的 API（静态源码契约）。
///   2. 可疑度/证据只在 Mario 目击时才增加（目击判定 + 调用顺序契约）。
/// 注：沙箱无 Unity，本文件需在 Unity Test Runner (EditMode) 中实跑确认。
/// </summary>
public class H4PerceptionHonestyTests
{
    static readonly string[] ForbiddenTruthReads =
    {
        "TricksterPossessionGate", "CurrentAnchor", "IsHiddenAndArmed", "IsFullyBlended",
        "DisguiseSystem", "opponentGate", "opponentDisguise", "_gate", "_trickster.transform",
        "tricksterPos", "TricksterPossessionState"
    };

    static string Read(string relative) => File.ReadAllText(Path.Combine(Application.dataPath, relative));

    static string Slice(string source, string startMarker, string endMarker)
    {
        int a = source.IndexOf(startMarker);
        Assert.GreaterOrEqual(a, 0, "marker not found: " + startMarker);
        int b = source.IndexOf(endMarker, a + startMarker.Length);
        Assert.Greater(b, a, "end marker not found: " + endMarker);
        return source.Substring(a, b - a);
    }

    static void AssertNoTruthReads(string region, string label)
    {
        foreach (string token in ForbiddenTruthReads)
            StringAssert.DoesNotContain(token, region, $"[H4] {label} 读取了捣蛋者真值：{token}");
    }

    [Test]
    public void HeuristicMarioBrainNeverReadsPossessionTruth()
    {
        string src = Read("Scripts/Core/HeuristicBotInputProvider.cs");
        AssertNoTruthReads(Slice(src, "protected virtual void UpdateMarioBrain", "void UpdateTricksterBrain"), "HeuristicBot.UpdateMarioBrain");
        AssertNoTruthReads(Slice(src, "private bool HasActionableScanCue", "public void InvalidateCache"), "HeuristicBot.HasActionableScanCue");
    }

    [Test]
    public void GuidedMarioBrainNeverReadsPossessionTruth()
    {
        string src = Read("Scripts/Editor/ExplorationTrialObserver.cs");
        AssertNoTruthReads(Slice(src, "protected override void UpdateMarioBrain", "\n    }\n}"), "GuidedBot.UpdateMarioBrain");
        AssertNoTruthReads(Slice(src, "private void UpdateJunctionRunner", "public GuidedBot("), "GuidedBot.UpdateJunctionRunner");
        AssertNoTruthReads(Slice(src, "private bool ApplyPublicQueueInput", "private bool ApplyWallTactics"), "GuidedBot.ApplyPublicQueueInput");
        AssertNoTruthReads(Slice(src, "private bool ApplyWallTactics", "private bool ApplySolidStepExit"), "GuidedBot.ApplyWallTactics");
    }

    [Test]
    public void PassiveSensorOnlyReadsWorldEvidence()
    {
        string src = Read("Scripts/Gameplay/SilentMarkSensor.cs");
        AssertNoTruthReads(Slice(src, "private void Update()", "private void RefreshAnchors"), "SilentMarkSensor.Update");
    }

    [Test]
    public void TrackerAddsSuspicionOnlyAfterWitnessCheck()
    {
        string src = Read("Scripts/Gameplay/MarioSuspicionTracker.cs");

        string activation = Slice(src, "private void HandlePropActivated", "private void HandlePossessionStateChanged");
        int witness = activation.IndexOf("MarioWitnesses(anchor)");
        Assert.Greater(witness, 0, "[H4] 出手处理必须先做目击判定");
        Assert.Greater(activation.IndexOf("data.AddSuspicion"), witness, "[H4] 出手可疑度必须在目击判定之后");
        Assert.Greater(activation.IndexOf("data.AddEvidence"), witness, "[H4] 出手证据必须在目击判定之后");

        string possession = Slice(src, "private void HandlePossessionStateChanged", "private void HandleAnchorChanged");
        StringAssert.Contains("anchor != null && MarioWitnesses(anchor)", possession, "[H4] 附身可疑度必须以目击为前提");

        StringAssert.Contains("requireMarioWitness = true", src, "[H4] 目击门禁默认必须开启");
    }

    [Test]
    public void OtherSuspicionWritersAskTrackerForWitness()
    {
        string comp = Slice(Read("Scripts/Gameplay/InterferenceCompensationPolicy.cs"), "private void HandlePropActivated", "data.AddSuspicion");
        StringAssert.Contains("IsWitnessedByMario(anchor)", comp);
        string stack = Read("Scripts/Gameplay/RepeatInterferenceStack.cs");
        StringAssert.Contains("suspicionTracker.IsWitnessedByMario(anchor)", stack);
    }

    [Test]
    public void WitnessRequiresRangeAndClearLine()
    {
        var target = new GameObject("H4Target");
        var wall = new GameObject("H4Wall");
        try
        {
            Vector2 viewer = new Vector2(47000, 1);
            target.transform.position = new Vector3(47004, 1, 0);
            target.AddComponent<BoxCollider2D>(); // 目标自身碰撞体不算遮挡
            Physics2D.SyncTransforms();
            Assert.IsTrue(MarioSuspicionTracker.CanWitness(viewer, target.transform.position, 8f, target.transform));

            Assert.IsFalse(MarioSuspicionTracker.CanWitness(viewer, target.transform.position, 3f, target.transform), "超出距离不能目击");

            wall.transform.position = new Vector3(47002, 1, 0);
            var body = wall.AddComponent<BoxCollider2D>(); body.size = new Vector2(0.2f, 3f);
            Physics2D.SyncTransforms();
            Assert.IsFalse(MarioSuspicionTracker.CanWitness(viewer, target.transform.position, 8f, target.transform), "实体墙后不能目击");

            body.isTrigger = true; Physics2D.SyncTransforms();
            Assert.IsTrue(MarioSuspicionTracker.CanWitness(viewer, target.transform.position, 8f, target.transform), "触发器不挡视线");

            Assert.IsFalse(MarioSuspicionTracker.CanWitness(new Vector2(float.NaN, 1), target.transform.position, 8f, target.transform));
        }
        finally { Object.DestroyImmediate(target); Object.DestroyImmediate(wall); }
    }
}
