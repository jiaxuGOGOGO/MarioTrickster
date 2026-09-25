using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 设计宪法 v1.0 开发顺序第 0 步：只保留 跑+扫描+附身+触发（+残留）。
/// 扩展系统代码保留但默认不进场景；按宪法顺序加回时显式关闭 CoreLoopOnly。
/// </summary>
public class Step0CoreLoopTests
{
    [Test]
    public void CoreLoopIsDefaultAndListsNineExtendedSystems()
    {
        Assert.IsTrue(GameplayLoopSceneBootstrapper.CoreLoopOnly, "第 0 步默认必须是核心循环");
        CollectionAssert.AllItemsAreUnique(GameplayLoopSceneBootstrapper.ExtendedSystems);
        Assert.AreEqual(9, GameplayLoopSceneBootstrapper.ExtendedSystems.Length);
        CollectionAssert.DoesNotContain(GameplayLoopSceneBootstrapper.ExtendedSystems, typeof(MarioSuspicionTracker));
        CollectionAssert.DoesNotContain(GameplayLoopSceneBootstrapper.ExtendedSystems, typeof(ResidueVisualHint));
        CollectionAssert.DoesNotContain(GameplayLoopSceneBootstrapper.ExtendedSystems, typeof(SuspicionHUD));
        CollectionAssert.DoesNotContain(GameplayLoopSceneBootstrapper.ExtendedSystems, typeof(LootEscapeHUD));
    }

    [Test]
    public void RemoveExtendedSystemsKeepsCoreComponents()
    {
        var managers = new GameObject("Step0Managers");
        try
        {
            managers.AddComponent<MarioSuspicionTracker>();
            managers.AddComponent<ResidueVisualHint>();
            foreach (var type in GameplayLoopSceneBootstrapper.ExtendedSystems) managers.AddComponent(type);
            GameplayLoopSceneBootstrapper.RemoveExtendedSystems(managers);
            foreach (var type in GameplayLoopSceneBootstrapper.ExtendedSystems)
                Assert.IsNull(managers.GetComponent(type), type.Name + " 应在第 0 步被移除");
            Assert.IsNotNull(managers.GetComponent<MarioSuspicionTracker>());
            Assert.IsNotNull(managers.GetComponent<ResidueVisualHint>());
        }
        finally { Object.DestroyImmediate(managers); }
    }

    [Test]
    public void SceneBuildersRouteExtendedSystemsThroughTheSwitch()
    {
        string builder = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Editor/TestSceneBuilder.cs"));
        string boot = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Editor/GameplayLoopSceneBootstrapper.cs"));
        foreach (string name in new[] { "RouteBudgetService", "InterferenceCompensationPolicy", "RepeatInterferenceStack",
            "CounterRevealReward", "PropComboTracker", "TricksterHeatMeter", "HeatBreachHint", "HeatSuspicionBridge", "AlarmCrisisDirector" })
            StringAssert.DoesNotContain($"managers.AddComponent<{name}>()", builder, name + " 不能绕过 CoreLoopOnly 直接装入");
        StringAssert.Contains("AddExtendedSystemsUnlessCoreLoop(managers);", builder);
        StringAssert.Contains("if (CoreLoopOnly)", boot);
        StringAssert.Contains("RemoveExtendedSystems(managers);", boot);
    }
}
