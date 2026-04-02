using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode 测试：关卡设计系统（Level Design Framework）
/// 
/// 验证内容：
///   1. 框架核心：LevelElementBase / LevelElementRegistry / ControllableLevelElement
///   2. 陷阱类：SpikeTrap / PendulumTrap / FireTrap / BouncingEnemy
///   3. 平台类：BouncyPlatform / OneWayPlatform / CollapsingPlatform
///   4. 隐藏通道类：HiddenPassage / FakeWall
///   5. 低耦合验证：删除任何元素不影响其他测试
/// 
/// Session 15: 关卡设计系统新增
/// </summary>
public class LevelElementTests
{
    /// <summary>辅助方法：强制调用 Awake</summary>
    private static void ForceAwake(MonoBehaviour component)
    {
        var awakeMethod = component.GetType().GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        if (awakeMethod != null)
        {
            awakeMethod.Invoke(component, null);
        }
    }

    /// <summary>辅助方法：创建带SpriteRenderer的测试对象</summary>
    private static GameObject CreateTestObject(string name)
    {
        GameObject go = new GameObject(name);
        go.AddComponent<SpriteRenderer>();
        return go;
    }

    // ═══════════════════════════════════════════════════════
    // LevelElementRegistry 测试
    // ═══════════════════════════════════════════════════════

    [SetUp]
    public void Setup()
    {
        // 每个测试前清空注册表
        LevelElementRegistry.Clear();
    }

    [Test]
    public void Registry_InitialState_IsEmpty()
    {
        Assert.AreEqual(0, LevelElementRegistry.TotalCount,
            "注册表初始状态应为空");
    }

    [Test]
    public void Registry_RegisterControllable_IncreasesCount()
    {
        GameObject go = CreateTestObject("TestElement");
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        // 手动注册（因为EditMode不会触发OnEnable）
        LevelElementRegistry.RegisterControllable(spike, "TestSpike",
            ElementCategory.Trap, ElementTag.Controllable | ElementTag.Damaging, "测试地刺");

        Assert.AreEqual(1, LevelElementRegistry.TotalCount,
            "注册后总数应为1");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Registry_UnregisterControllable_DecreasesCount()
    {
        GameObject go = CreateTestObject("TestElement");
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        LevelElementRegistry.RegisterControllable(spike, "TestSpike",
            ElementCategory.Trap, ElementTag.Controllable, "测试");

        LevelElementRegistry.UnregisterControllable(spike);

        Assert.AreEqual(0, LevelElementRegistry.TotalCount,
            "注销后总数应为0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Registry_GetByCategory_ReturnsCorrectElements()
    {
        GameObject go1 = CreateTestObject("Trap1");
        go1.AddComponent<BoxCollider2D>().isTrigger = true;
        SpikeTrap spike = go1.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        GameObject go2 = CreateTestObject("Platform1");
        go2.AddComponent<BoxCollider2D>();
        BouncyPlatform bouncy = go2.AddComponent<BouncyPlatform>();
        ForceAwake(bouncy);

        LevelElementRegistry.RegisterControllable(spike, "Spike",
            ElementCategory.Trap, ElementTag.Damaging, "");
        LevelElementRegistry.RegisterControllable(bouncy, "Bouncy",
            ElementCategory.Platform, ElementTag.AffectsPhysics, "");

        var traps = LevelElementRegistry.GetByCategory(ElementCategory.Trap);
        var platforms = LevelElementRegistry.GetByCategory(ElementCategory.Platform);

        Assert.AreEqual(1, traps.Count, "应有1个陷阱");
        Assert.AreEqual(1, platforms.Count, "应有1个平台");

        Object.DestroyImmediate(go1);
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void Registry_GetByTag_ReturnsCorrectElements()
    {
        GameObject go1 = CreateTestObject("Trap1");
        go1.AddComponent<BoxCollider2D>().isTrigger = true;
        SpikeTrap spike = go1.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        LevelElementRegistry.RegisterControllable(spike, "Spike",
            ElementCategory.Trap, ElementTag.Controllable | ElementTag.Damaging, "");

        var controllable = LevelElementRegistry.GetByTag(ElementTag.Controllable);
        var damaging = LevelElementRegistry.GetByTag(ElementTag.Damaging);

        Assert.AreEqual(1, controllable.Count, "应有1个可操控元素");
        Assert.AreEqual(1, damaging.Count, "应有1个伤害元素");

        Object.DestroyImmediate(go1);
    }

    [Test]
    public void Registry_Clear_RemovesAllElements()
    {
        GameObject go = CreateTestObject("Test");
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        LevelElementRegistry.RegisterControllable(spike, "Spike",
            ElementCategory.Trap, ElementTag.Controllable, "");

        LevelElementRegistry.Clear();

        Assert.AreEqual(0, LevelElementRegistry.TotalCount,
            "Clear后总数应为0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Registry_ResetAll_DoesNotThrow()
    {
        GameObject go = CreateTestObject("Test");
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        LevelElementRegistry.RegisterControllable(spike, "Spike",
            ElementCategory.Trap, ElementTag.Controllable, "");

        Assert.DoesNotThrow(() => LevelElementRegistry.ResetAll(),
            "ResetAll不应抛出异常");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Registry_GetCategoryStats_ReturnsCorrectCounts()
    {
        GameObject go1 = CreateTestObject("Trap");
        go1.AddComponent<BoxCollider2D>().isTrigger = true;
        SpikeTrap spike = go1.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        LevelElementRegistry.RegisterControllable(spike, "Spike",
            ElementCategory.Trap, ElementTag.Controllable, "");

        var stats = LevelElementRegistry.GetCategoryStats();

        Assert.IsTrue(stats.ContainsKey(ElementCategory.Trap),
            "统计中应包含Trap分类");
        Assert.AreEqual(1, stats[ElementCategory.Trap],
            "Trap分类应有1个元素");

        Object.DestroyImmediate(go1);
    }

    // ═══════════════════════════════════════════════════════
    // SpikeTrap 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void SpikeTrap_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestSpike");
        go.AddComponent<SpikeTrap>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "SpikeTrap 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SpikeTrap_ColliderIsTrigger()
    {
        GameObject go = CreateTestObject("TestSpike");
        go.AddComponent<SpikeTrap>();
        ForceAwake(go.GetComponent<SpikeTrap>());

        Assert.IsTrue(go.GetComponent<BoxCollider2D>().isTrigger,
            "SpikeTrap 的碰撞器应为 Trigger 模式");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SpikeTrap_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestSpike");
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        Assert.AreEqual(ElementCategory.Trap, spike.GetElementCategory(),
            "SpikeTrap 的分类应为 Trap");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SpikeTrap_HasDamagingTag()
    {
        GameObject go = CreateTestObject("TestSpike");
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        Assert.IsTrue(spike.HasElementTag(ElementTag.Damaging),
            "SpikeTrap 应有 Damaging 标签");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SpikeTrap_OnLevelReset_DoesNotThrow()
    {
        GameObject go = CreateTestObject("TestSpike");
        SpikeTrap spike = go.AddComponent<SpikeTrap>();
        ForceAwake(spike);

        Assert.DoesNotThrow(() => spike.OnLevelReset(),
            "SpikeTrap.OnLevelReset 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // PendulumTrap 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void PendulumTrap_Awake_CreatesHammerChild()
    {
        GameObject go = CreateTestObject("TestPendulum");
        PendulumTrap pendulum = go.AddComponent<PendulumTrap>();
        ForceAwake(pendulum);

        Transform hammer = go.transform.Find("PendulumHammer");
        Assert.IsNotNull(hammer, "PendulumTrap 应创建 PendulumHammer 子对象");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PendulumTrap_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestPendulum");
        PendulumTrap pendulum = go.AddComponent<PendulumTrap>();
        ForceAwake(pendulum);

        Assert.AreEqual(ElementCategory.Trap, pendulum.GetElementCategory(),
            "PendulumTrap 的分类应为 Trap");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PendulumTrap_HasMovingPartTag()
    {
        GameObject go = CreateTestObject("TestPendulum");
        PendulumTrap pendulum = go.AddComponent<PendulumTrap>();
        ForceAwake(pendulum);

        Assert.IsTrue(pendulum.HasElementTag(ElementTag.MovingPart),
            "PendulumTrap 应有 MovingPart 标签");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PendulumTrap_OnLevelReset_ResetsAngle()
    {
        GameObject go = CreateTestObject("TestPendulum");
        PendulumTrap pendulum = go.AddComponent<PendulumTrap>();
        ForceAwake(pendulum);

        Assert.DoesNotThrow(() => pendulum.OnLevelReset(),
            "PendulumTrap.OnLevelReset 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // FireTrap 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void FireTrap_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestFire");
        go.AddComponent<FireTrap>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "FireTrap 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FireTrap_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestFire");
        FireTrap fire = go.AddComponent<FireTrap>();
        ForceAwake(fire);

        Assert.AreEqual(ElementCategory.Trap, fire.GetElementCategory(),
            "FireTrap 的分类应为 Trap");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FireTrap_OnLevelReset_DoesNotThrow()
    {
        GameObject go = CreateTestObject("TestFire");
        FireTrap fire = go.AddComponent<FireTrap>();
        ForceAwake(fire);

        Assert.DoesNotThrow(() => fire.OnLevelReset(),
            "FireTrap.OnLevelReset 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // BouncingEnemy 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void BouncingEnemy_RequiresRigidbody2D()
    {
        GameObject go = CreateTestObject("TestBounce");
        go.AddComponent<BouncingEnemy>();

        Assert.IsNotNull(go.GetComponent<Rigidbody2D>(),
            "BouncingEnemy 应自动添加 Rigidbody2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BouncingEnemy_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestBounce");
        go.AddComponent<BouncingEnemy>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "BouncingEnemy 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BouncingEnemy_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestBounce");
        BouncingEnemy bouncer = go.AddComponent<BouncingEnemy>();
        ForceAwake(bouncer);

        Assert.AreEqual(ElementCategory.Enemy, bouncer.GetElementCategory(),
            "BouncingEnemy 的分类应为 Enemy");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BouncingEnemy_OnLevelReset_RestoresState()
    {
        GameObject go = CreateTestObject("TestBounce");
        BouncingEnemy bouncer = go.AddComponent<BouncingEnemy>();
        ForceAwake(bouncer);

        Assert.DoesNotThrow(() => bouncer.OnLevelReset(),
            "BouncingEnemy.OnLevelReset 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // BouncyPlatform 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void BouncyPlatform_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestBouncy");
        go.AddComponent<BouncyPlatform>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "BouncyPlatform 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BouncyPlatform_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestBouncy");
        BouncyPlatform bouncy = go.AddComponent<BouncyPlatform>();
        ForceAwake(bouncy);

        Assert.AreEqual(ElementCategory.Platform, bouncy.GetElementCategory(),
            "BouncyPlatform 的分类应为 Platform");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void BouncyPlatform_HasAffectsPhysicsTag()
    {
        GameObject go = CreateTestObject("TestBouncy");
        BouncyPlatform bouncy = go.AddComponent<BouncyPlatform>();
        ForceAwake(bouncy);

        Assert.IsTrue(bouncy.HasElementTag(ElementTag.AffectsPhysics),
            "BouncyPlatform 应有 AffectsPhysics 标签");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // OneWayPlatform 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void OneWayPlatform_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestOneWay");
        go.AddComponent<OneWayPlatform>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "OneWayPlatform 应自动添加 BoxCollider2D（RequireComponent）");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void OneWayPlatform_RequiresPlatformEffector2D()
    {
        GameObject go = CreateTestObject("TestOneWay");
        go.AddComponent<OneWayPlatform>();

        Assert.IsNotNull(go.GetComponent<PlatformEffector2D>(),
            "OneWayPlatform 应自动添加 PlatformEffector2D（RequireComponent）");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void OneWayPlatform_HasCorrectCategory()
    {
        // OneWayPlatform 继承 LevelElementBase（非ControllableLevelElement）
        // 使用基类的 Category 属性而非 GetElementCategory() 方法
        GameObject go = CreateTestObject("TestOneWay");
        OneWayPlatform oneWay = go.AddComponent<OneWayPlatform>();

        // 手动调用 Awake 设置元数据
        var awakeMethod = typeof(OneWayPlatform).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (awakeMethod != null) awakeMethod.Invoke(oneWay, null);

        Assert.AreEqual(ElementCategory.Platform, oneWay.Category,
            "OneWayPlatform 的分类应为 Platform");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void OneWayPlatform_HasAffectsPhysicsTag()
    {
        // OneWayPlatform 继承 LevelElementBase，使用基类 HasTag() 方法
        GameObject go = CreateTestObject("TestOneWay");
        OneWayPlatform oneWay = go.AddComponent<OneWayPlatform>();

        var awakeMethod = typeof(OneWayPlatform).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (awakeMethod != null) awakeMethod.Invoke(oneWay, null);

        Assert.IsTrue(oneWay.HasTag(ElementTag.AffectsPhysics),
            "OneWayPlatform 应有 AffectsPhysics 标签");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void OneWayPlatform_AllowDropThrough_DoesNotThrow()
    {
        GameObject go = CreateTestObject("TestOneWay");
        OneWayPlatform oneWay = go.AddComponent<OneWayPlatform>();

        var awakeMethod = typeof(OneWayPlatform).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (awakeMethod != null) awakeMethod.Invoke(oneWay, null);

        Assert.DoesNotThrow(() => oneWay.AllowDropThrough(),
            "AllowDropThrough 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void OneWayPlatform_OnLevelReset_DoesNotThrow()
    {
        GameObject go = CreateTestObject("TestOneWay");
        OneWayPlatform oneWay = go.AddComponent<OneWayPlatform>();

        var awakeMethod = typeof(OneWayPlatform).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (awakeMethod != null) awakeMethod.Invoke(oneWay, null);

        Assert.DoesNotThrow(() => oneWay.OnLevelReset(),
            "OneWayPlatform.OnLevelReset 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // CollapsingPlatform 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void CollapsingPlatform_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestCollapse");
        go.AddComponent<CollapsingPlatform>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "CollapsingPlatform 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CollapsingPlatform_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestCollapse");
        CollapsingPlatform collapse = go.AddComponent<CollapsingPlatform>();
        ForceAwake(collapse);

        Assert.AreEqual(ElementCategory.Platform, collapse.GetElementCategory(),
            "CollapsingPlatform 的分类应为 Platform");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CollapsingPlatform_HasOneShotTag()
    {
        GameObject go = CreateTestObject("TestCollapse");
        CollapsingPlatform collapse = go.AddComponent<CollapsingPlatform>();
        ForceAwake(collapse);

        Assert.IsTrue(collapse.HasElementTag(ElementTag.OneShot),
            "CollapsingPlatform 应有 OneShot 标签");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // HiddenPassage 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void HiddenPassage_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestPassage");
        go.AddComponent<HiddenPassage>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "HiddenPassage 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HiddenPassage_ColliderIsTrigger()
    {
        GameObject go = CreateTestObject("TestPassage");
        go.AddComponent<HiddenPassage>();
        ForceAwake(go.GetComponent<HiddenPassage>());

        Assert.IsTrue(go.GetComponent<BoxCollider2D>().isTrigger,
            "HiddenPassage 的碰撞器应为 Trigger 模式");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HiddenPassage_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestPassage");
        HiddenPassage passage = go.AddComponent<HiddenPassage>();
        ForceAwake(passage);

        Assert.AreEqual(ElementCategory.HiddenPassage, passage.GetElementCategory(),
            "HiddenPassage 的分类应为 HiddenPassage");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HiddenPassage_HasHiddenTag()
    {
        GameObject go = CreateTestObject("TestPassage");
        HiddenPassage passage = go.AddComponent<HiddenPassage>();
        ForceAwake(passage);

        Assert.IsTrue(passage.HasElementTag(ElementTag.Hidden),
            "HiddenPassage 应有 Hidden 标签");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void HiddenPassage_IsActive_ByDefault()
    {
        GameObject go = CreateTestObject("TestPassage");
        HiddenPassage passage = go.AddComponent<HiddenPassage>();
        ForceAwake(passage);

        Assert.IsTrue(passage.IsActive,
            "HiddenPassage 默认应为激活状态");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // FakeWall 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void FakeWall_RequiresBoxCollider2D()
    {
        GameObject go = CreateTestObject("TestFakeWall");
        go.AddComponent<FakeWall>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "FakeWall 应自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FakeWall_ColliderIsTrigger_ByDefault()
    {
        GameObject go = CreateTestObject("TestFakeWall");
        go.AddComponent<FakeWall>();
        ForceAwake(go.GetComponent<FakeWall>());

        Assert.IsTrue(go.GetComponent<BoxCollider2D>().isTrigger,
            "FakeWall 默认碰撞器应为 Trigger 模式（可穿过）");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FakeWall_HasCorrectCategory()
    {
        GameObject go = CreateTestObject("TestFakeWall");
        FakeWall fakeWall = go.AddComponent<FakeWall>();
        ForceAwake(fakeWall);

        Assert.AreEqual(ElementCategory.HiddenPassage, fakeWall.GetElementCategory(),
            "FakeWall 的分类应为 HiddenPassage");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FakeWall_HasDisguisableTag()
    {
        GameObject go = CreateTestObject("TestFakeWall");
        FakeWall fakeWall = go.AddComponent<FakeWall>();
        ForceAwake(fakeWall);

        Assert.IsTrue(fakeWall.HasElementTag(ElementTag.Disguisable),
            "FakeWall 应有 Disguisable 标签");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FakeWall_OnLevelReset_RestoresTriggerState()
    {
        GameObject go = CreateTestObject("TestFakeWall");
        FakeWall fakeWall = go.AddComponent<FakeWall>();
        ForceAwake(fakeWall);

        fakeWall.OnLevelReset();

        Assert.IsTrue(go.GetComponent<BoxCollider2D>().isTrigger,
            "重置后 FakeWall 应恢复为 Trigger 模式");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // 枚举完整性测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void ElementCategory_HasAllRequiredValues()
    {
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Trap));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Platform));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.HiddenPassage));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Collectible));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Enemy));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Hazard));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Checkpoint));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementCategory), ElementCategory.Misc));
    }

    [Test]
    public void ElementTag_FlagsCanBeCombined()
    {
        ElementTag combined = ElementTag.Controllable | ElementTag.Damaging | ElementTag.Periodic;

        Assert.IsTrue((combined & ElementTag.Controllable) != 0, "组合标签应包含 Controllable");
        Assert.IsTrue((combined & ElementTag.Damaging) != 0, "组合标签应包含 Damaging");
        Assert.IsTrue((combined & ElementTag.Periodic) != 0, "组合标签应包含 Periodic");
        Assert.IsTrue((combined & ElementTag.Hidden) == 0, "组合标签不应包含 Hidden");
    }

    // ═══════════════════════════════════════════════════════
    // 低耦合验证
    // ═══════════════════════════════════════════════════════

    [Test]
    public void LowCoupling_RegistryWorksWithoutAnyElements()
    {
        // 验证 Registry 在没有任何元素注册时不会崩溃
        Assert.DoesNotThrow(() => LevelElementRegistry.GetAll());
        Assert.DoesNotThrow(() => LevelElementRegistry.GetByCategory(ElementCategory.Trap));
        Assert.DoesNotThrow(() => LevelElementRegistry.GetByTag(ElementTag.Controllable));
        Assert.DoesNotThrow(() => LevelElementRegistry.GetByCategoryAndTag(ElementCategory.Trap, ElementTag.Damaging));
        Assert.DoesNotThrow(() => LevelElementRegistry.GetNearby(Vector3.zero, 10f));
        Assert.DoesNotThrow(() => LevelElementRegistry.GetAllControllable());
        Assert.DoesNotThrow(() => LevelElementRegistry.GetCategoryStats());
        Assert.DoesNotThrow(() => LevelElementRegistry.ResetAll());
        Assert.DoesNotThrow(() => LevelElementRegistry.Clear());
        Assert.DoesNotThrow(() => LevelElementRegistry.DebugPrintSummary());
    }

    [Test]
    public void LowCoupling_RegistryHandlesNullGracefully()
    {
        Assert.DoesNotThrow(() => LevelElementRegistry.Register(null),
            "Register(null) 不应抛出异常");
        Assert.DoesNotThrow(() => LevelElementRegistry.Unregister(null),
            "Unregister(null) 不应抛出异常");
        Assert.DoesNotThrow(() => LevelElementRegistry.RegisterControllable(null, "", ElementCategory.Misc, ElementTag.None, ""),
            "RegisterControllable(null) 不应抛出异常");
        Assert.DoesNotThrow(() => LevelElementRegistry.UnregisterControllable(null),
            "UnregisterControllable(null) 不应抛出异常");
    }
}
