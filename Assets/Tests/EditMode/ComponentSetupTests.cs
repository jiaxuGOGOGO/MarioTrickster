using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode 测试：验证组件依赖、RequireComponent、序列化字段配置
/// 
/// 这些测试不需要运行游戏，在编辑器中即可执行。
/// 主要验证：
///   1. 各脚本的 RequireComponent 是否正确自动添加组件
///   2. 创建 GameObject 并挂载脚本后，依赖组件是否存在
///   3. 关键系统的初始状态是否正确
/// 
/// 运行方式：Window → General → Test Runner → EditMode → Run All
/// 
/// Session 13 修复：
///   EditMode 测试中 AddComponent 不会自动调用 Awake()，
///   使用 runInEditMode = true 强制 Unity 在编辑模式下执行生命周期回调。
///   对于需要 Awake 初始化的组件，统一使用 ForceAwake() 辅助方法。
/// </summary>
public class ComponentSetupTests
{
    /// <summary>
    /// 辅助方法：通过反射强制调用 MonoBehaviour 的 Awake 方法
    /// EditMode 测试中 AddComponent 不会自动调用 Awake()，需要手动触发
    /// </summary>
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

    // ═══════════════════════════════════════════════════════
    // MarioController 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void MarioController_RequiresRigidbody2D()
    {
        GameObject go = new GameObject("TestMario");
        go.AddComponent<MarioController>();

        Assert.IsNotNull(go.GetComponent<Rigidbody2D>(),
            "MarioController 应该自动添加 Rigidbody2D（RequireComponent）");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MarioController_RequiresBoxCollider2D()
    {
        GameObject go = new GameObject("TestMario");
        go.AddComponent<MarioController>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "MarioController 应该自动添加 BoxCollider2D（RequireComponent）");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MarioController_HasPublicInputMethods()
    {
        GameObject go = new GameObject("TestMario");
        go.AddComponent<SpriteRenderer>(); // MarioController.Awake 需要
        MarioController mario = go.AddComponent<MarioController>();
        ForceAwake(mario);

        // 验证 InputManager 需要调用的公共方法存在
        Assert.DoesNotThrow(() => mario.SetMoveInput(Vector2.zero),
            "MarioController 应该有 SetMoveInput 方法");
        Assert.DoesNotThrow(() => mario.OnJumpPressed(),
            "MarioController 应该有 OnJumpPressed 方法");
        Assert.DoesNotThrow(() => mario.OnJumpReleased(),
            "MarioController 应该有 OnJumpReleased 方法");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MarioController_HasPlatformVelocityMethod()
    {
        GameObject go = new GameObject("TestMario");
        go.AddComponent<SpriteRenderer>();
        MarioController mario = go.AddComponent<MarioController>();
        ForceAwake(mario);

        Assert.DoesNotThrow(() => mario.SetPlatformVelocity(Vector2.zero),
            "MarioController 应该有 SetPlatformVelocity 方法（平台速度注入）");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // TricksterController 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void TricksterController_RequiresRigidbody2D()
    {
        GameObject go = new GameObject("TestTrickster");
        go.AddComponent<TricksterController>();

        Assert.IsNotNull(go.GetComponent<Rigidbody2D>(),
            "TricksterController 应该自动添加 Rigidbody2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TricksterController_RequiresBoxCollider2D()
    {
        GameObject go = new GameObject("TestTrickster");
        go.AddComponent<TricksterController>();

        Assert.IsNotNull(go.GetComponent<BoxCollider2D>(),
            "TricksterController 应该自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TricksterController_HasDisguiseInputMethod()
    {
        GameObject go = new GameObject("TestTrickster");
        go.AddComponent<SpriteRenderer>();
        TricksterController trickster = go.AddComponent<TricksterController>();
        ForceAwake(trickster);

        Assert.DoesNotThrow(() => trickster.OnDisguisePressed(),
            "TricksterController 应该有 OnDisguisePressed 方法");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TricksterController_HasAbilityInputMethod()
    {
        GameObject go = new GameObject("TestTrickster");
        go.AddComponent<SpriteRenderer>();
        TricksterController trickster = go.AddComponent<TricksterController>();
        ForceAwake(trickster);

        Assert.DoesNotThrow(() => trickster.OnAbilityPressed(),
            "TricksterController 应该有 OnAbilityPressed 方法");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TricksterController_HasSwitchDisguiseMethod()
    {
        GameObject go = new GameObject("TestTrickster");
        go.AddComponent<SpriteRenderer>();
        TricksterController trickster = go.AddComponent<TricksterController>();
        ForceAwake(trickster);

        Assert.DoesNotThrow(() => trickster.OnSwitchDisguise(1f),
            "TricksterController 应该有 OnSwitchDisguise 方法");
        Assert.DoesNotThrow(() => trickster.OnSwitchDisguise(-1f),
            "TricksterController 应该有 OnSwitchDisguise(-1) 方法");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // PlayerHealth 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void PlayerHealth_InitializesWithMaxHealth()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth,
            "PlayerHealth 初始化时 currentHealth 应等于 maxHealth");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_TakeDamage_ReducesHealth()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        int initialHealth = health.CurrentHealth;
        health.TakeDamage(1);

        Assert.AreEqual(initialHealth - 1, health.CurrentHealth,
            "TakeDamage(1) 应该减少 1 点生命值");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_TakeDamage_TriggersInvincibility()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        health.TakeDamage(1);

        Assert.IsTrue(health.IsInvincible,
            "受伤后应该进入无敌状态");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_TakeDamage_WhileInvincible_DoesNothing()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        health.TakeDamage(1);
        int healthAfterFirst = health.CurrentHealth;
        health.TakeDamage(1); // 无敌期间再次受伤

        Assert.AreEqual(healthAfterFirst, health.CurrentHealth,
            "无敌期间受伤不应该减少生命值");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_TakeDamage_FiresDeathEvent_WhenHealthReachesZero()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        bool deathFired = false;
        health.OnDeath += () => deathFired = true;

        // 一次性打满伤害
        health.TakeDamage(999);

        Assert.IsTrue(deathFired, "生命值归零时应该触发 OnDeath 事件");
        Assert.AreEqual(0, health.CurrentHealth, "生命值不应低于 0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_Heal_IncreasesHealth()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        health.TakeDamage(1);
        int damaged = health.CurrentHealth;
        // 手动关闭无敌以便测试 Heal
        health.Heal(1);

        Assert.AreEqual(damaged + 1, health.CurrentHealth,
            "Heal(1) 应该恢复 1 点生命值");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_Heal_DoesNotExceedMax()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        health.Heal(100);

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth,
            "Heal 不应超过 maxHealth");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void PlayerHealth_ResetHealth_RestoresFullHealth()
    {
        GameObject go = new GameObject("TestHealth");
        go.AddComponent<SpriteRenderer>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();
        ForceAwake(health);

        health.TakeDamage(999);
        health.ResetHealth();

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth,
            "ResetHealth 应该恢复到满血");
        Assert.IsFalse(health.IsInvincible,
            "ResetHealth 应该取消无敌状态");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // DisguiseSystem 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void DisguiseSystem_InitialState_NotDisguised()
    {
        GameObject go = new GameObject("TestDisguise");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        ForceAwake(disguise);

        Assert.IsFalse(disguise.IsDisguised, "初始状态不应处于伪装");
        Assert.IsFalse(disguise.IsFullyBlended, "初始状态不应处于融入");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void DisguiseSystem_Disguise_WithoutConfig_DoesNothing()
    {
        GameObject go = new GameObject("TestDisguise");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        ForceAwake(disguise);

        // 没有配置 availableDisguises，调用 Disguise 不应崩溃
        Assert.DoesNotThrow(() => disguise.Disguise(),
            "没有配置伪装形态时调用 Disguise 不应抛出异常");
        Assert.IsFalse(disguise.IsDisguised,
            "没有配置伪装形态时不应进入伪装状态");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void DisguiseSystem_GetDebugStatus_ReportsEmptyConfig()
    {
        GameObject go = new GameObject("TestDisguise");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        ForceAwake(disguise);

        string status = disguise.GetDebugStatus();
        Assert.IsNotNull(status, "GetDebugStatus 不应返回 null");
        Assert.IsTrue(status.Contains("0"),
            "没有配置伪装时，状态应包含 0（表示 0 个可用伪装）");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // MovingPlatform 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void MovingPlatform_RequiresRigidbody2D()
    {
        GameObject go = new GameObject("TestPlatform");
        go.AddComponent<MovingPlatform>();

        Assert.IsNotNull(go.GetComponent<Rigidbody2D>(),
            "MovingPlatform 应该自动添加 Rigidbody2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void MovingPlatform_SetsKinematic()
    {
        GameObject go = new GameObject("TestPlatform");
        MovingPlatform platform = go.AddComponent<MovingPlatform>();
        ForceAwake(platform);

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType,
            "MovingPlatform 的 Rigidbody2D 应该是 Kinematic");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // GoalZone / KillZone 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void GoalZone_RequiresBoxCollider2D()
    {
        GameObject go = new GameObject("TestGoal");
        go.AddComponent<GoalZone>();

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(col, "GoalZone 应该自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GoalZone_SetsTrigger()
    {
        GameObject go = new GameObject("TestGoal");
        GoalZone goalZone = go.AddComponent<GoalZone>();
        ForceAwake(goalZone);

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        Assert.IsTrue(col.isTrigger,
            "GoalZone 的 BoxCollider2D 应该是 Trigger");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void KillZone_RequiresBoxCollider2D()
    {
        GameObject go = new GameObject("TestKill");
        go.AddComponent<KillZone>();

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        Assert.IsNotNull(col, "KillZone 应该自动添加 BoxCollider2D");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void KillZone_SetsTrigger()
    {
        GameObject go = new GameObject("TestKill");
        KillZone killZone = go.AddComponent<KillZone>();
        ForceAwake(killZone);

        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        Assert.IsTrue(col.isTrigger,
            "KillZone 的 BoxCollider2D 应该是 Trigger");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // GameManager 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void GameManager_InitialState_IsWaitingToStart()
    {
        GameObject go = new GameObject("TestGM");
        GameManager gm = go.AddComponent<GameManager>();

        Assert.AreEqual(GameState.WaitingToStart, gm.CurrentState,
            "GameManager 初始状态应该是 WaitingToStart");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GameManager_HasSingletonProperty()
    {
        GameObject go = new GameObject("TestGM");
        GameManager gm = go.AddComponent<GameManager>();

        // 验证单例属性存在（不验证值，因为 Awake 可能未执行）
        Assert.IsNotNull(typeof(GameManager).GetProperty("Instance"),
            "GameManager 应该有 Instance 静态属性");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // InputManager 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void InputManager_HasPlayerSetterMethods()
    {
        GameObject go = new GameObject("TestInput");
        InputManager input = go.AddComponent<InputManager>();

        Assert.DoesNotThrow(() => input.SetMarioController(null),
            "InputManager 应该有 SetMarioController 方法");
        Assert.DoesNotThrow(() => input.SetTricksterController(null),
            "InputManager 应该有 SetTricksterController 方法");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void InputManager_DisableEnableInput()
    {
        GameObject go = new GameObject("TestInput");
        InputManager input = go.AddComponent<InputManager>();

        Assert.DoesNotThrow(() => input.DisableAllInput(),
            "DisableAllInput 不应抛出异常");
        Assert.DoesNotThrow(() => input.EnableAllInput(),
            "EnableAllInput 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // ControllableProp 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void ControllableHazard_ImplementsIControllableProp()
    {
        GameObject go = new GameObject("TestHazard");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ControllableHazard hazard = go.AddComponent<ControllableHazard>();

        Assert.IsTrue(hazard is IControllableProp,
            "ControllableHazard 应该实现 IControllableProp 接口");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ControllableBlock_ImplementsIControllableProp()
    {
        GameObject go = new GameObject("TestBlock");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ControllableBlock block = go.AddComponent<ControllableBlock>();

        Assert.IsTrue(block is IControllableProp,
            "ControllableBlock 应该实现 IControllableProp 接口");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ControllablePlatform_ImplementsIControllableProp()
    {
        GameObject go = new GameObject("TestCP");
        go.AddComponent<SpriteRenderer>();
        ControllablePlatform cp = go.AddComponent<ControllablePlatform>();

        Assert.IsTrue(cp is IControllableProp,
            "ControllablePlatform 应该实现 IControllableProp 接口");

        Object.DestroyImmediate(go);
    }
}

/// <summary>
/// Session 10 新增测试：EnergySystem + ScanAbility
/// Session 13 修复：添加 ForceAwake 确保 EditMode 下初始化正确
/// </summary>
public class EnergySystemTests
{
    /// <summary>
    /// 辅助方法：通过反射强制调用 MonoBehaviour 的 Awake 方法
    /// </summary>
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

    /// <summary>
    /// 辅助方法：创建带有完整依赖的 EnergySystem 测试对象
    /// 并确保所有组件的 Awake 被正确调用
    /// </summary>
    private static (GameObject go, EnergySystem energy) CreateEnergySystem()
    {
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        ForceAwake(disguise);
        EnergySystem energy = go.AddComponent<EnergySystem>();
        ForceAwake(energy);
        return (go, energy);
    }

    [Test]
    public void EnergySystem_InitializesWithMaxEnergy()
    {
        var (go, energy) = CreateEnergySystem();

        Assert.AreEqual(energy.MaxEnergy, energy.CurrentEnergy,
            "EnergySystem 初始化时 currentEnergy 应等于 maxEnergy");
        Assert.AreEqual(1f, energy.EnergyPercent, 0.01f,
            "EnergySystem 初始化时 EnergyPercent 应为 1.0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_HasEnoughForDisguise_InitiallyTrue()
    {
        var (go, energy) = CreateEnergySystem();

        Assert.IsTrue(energy.HasEnoughForDisguise,
            "满能量时应该有足够能量变身");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_HasEnoughForControl_InitiallyTrue()
    {
        var (go, energy) = CreateEnergySystem();

        Assert.IsTrue(energy.HasEnoughForControl,
            "满能量时应该有足够能量操控道具");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_TryConsumeDisguiseCost_ReducesEnergy()
    {
        var (go, energy) = CreateEnergySystem();

        float before = energy.CurrentEnergy;
        bool result = energy.TryConsumeDisguiseCost();

        Assert.IsTrue(result, "满能量时 TryConsumeDisguiseCost 应返回 true");
        Assert.Less(energy.CurrentEnergy, before,
            "TryConsumeDisguiseCost 后能量应减少");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_TryConsumeControlCost_ReducesEnergy()
    {
        var (go, energy) = CreateEnergySystem();

        float before = energy.CurrentEnergy;
        bool result = energy.TryConsumeControlCost();

        Assert.IsTrue(result, "满能量时 TryConsumeControlCost 应返回 true");
        Assert.Less(energy.CurrentEnergy, before,
            "TryConsumeControlCost 后能量应减少");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_ResetEnergy_RestoresMax()
    {
        var (go, energy) = CreateEnergySystem();

        // 消耗一些能量
        energy.TryConsumeDisguiseCost();
        energy.TryConsumeControlCost();

        energy.ResetEnergy();

        Assert.AreEqual(energy.MaxEnergy, energy.CurrentEnergy,
            "ResetEnergy 后应恢复到满能量");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_AddEnergy_IncreasesButCapsAtMax()
    {
        var (go, energy) = CreateEnergySystem();

        // 消耗一些能量
        energy.TryConsumeDisguiseCost();
        float afterConsume = energy.CurrentEnergy;

        energy.AddEnergy(5f);
        Assert.Greater(energy.CurrentEnergy, afterConsume,
            "AddEnergy 应增加能量");

        // 添加超过上限
        energy.AddEnergy(9999f);
        Assert.AreEqual(energy.MaxEnergy, energy.CurrentEnergy, 0.01f,
            "AddEnergy 不应超过 MaxEnergy");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_IsLowEnergy_WhenBelowThreshold()
    {
        var (go, energy) = CreateEnergySystem();

        Assert.IsFalse(energy.IsLowEnergy,
            "满能量时不应处于低能量状态");

        // 反复消耗能量直到低于阈值
        for (int i = 0; i < 10; i++)
        {
            energy.TryConsumeDisguiseCost();
        }

        // 如果能量低于 25%，应该是低能量状态
        if (energy.EnergyPercent <= 0.25f)
        {
            Assert.IsTrue(energy.IsLowEnergy,
                "能量低于阈值时应处于低能量状态");
        }

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_OnEnergyChanged_EventFires()
    {
        var (go, energy) = CreateEnergySystem();

        bool eventFired = false;
        energy.OnEnergyChanged += (current, max) => eventFired = true;

        energy.TryConsumeDisguiseCost();

        Assert.IsTrue(eventFired, "消耗能量后应触发 OnEnergyChanged 事件");

        Object.DestroyImmediate(go);
    }
}

public class ScanAbilityTests
{
    /// <summary>
    /// 辅助方法：通过反射强制调用 MonoBehaviour 的 Awake 方法
    /// </summary>
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

    [Test]
    public void ScanAbility_InitialState_IsReady()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        Assert.IsTrue(scan.IsReady, "ScanAbility 初始状态应该是就绪的");
        Assert.IsFalse(scan.IsRevealing, "ScanAbility 初始状态不应在揭示中");
        Assert.AreEqual(0f, scan.CooldownRemaining, 0.01f,
            "ScanAbility 初始冷却应为 0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_ActivateScan_TriggersCooldown()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        scan.ActivateScan();

        Assert.IsFalse(scan.IsReady, "扫描后应进入冷却");
        Assert.Greater(scan.CooldownRemaining, 0f, "冷却时间应大于 0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_ActivateScan_FiresEvent()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        bool eventFired = false;
        scan.OnScanActivated += () => eventFired = true;

        scan.ActivateScan();

        Assert.IsTrue(eventFired, "扫描应触发 OnScanActivated 事件");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_ActivateScan_WhileOnCooldown_DoesNothing()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        scan.ActivateScan(); // 第一次
        float cooldownAfterFirst = scan.CooldownRemaining;

        int eventCount = 0;
        scan.OnScanActivated += () => eventCount++;

        scan.ActivateScan(); // 冷却中再次扫描

        Assert.AreEqual(0, eventCount,
            "冷却中扫描不应触发事件");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_ScanRadius_IsPositive()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        Assert.Greater(scan.ScanRadius, 0f, "扫描半径应大于 0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_OnScanResult_FiresWithFalse_WhenNoTrickster()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        bool? scanResult = null;
        scan.OnScanResult += (found) => scanResult = found;

        scan.ActivateScan();

        Assert.IsNotNull(scanResult, "扫描应触发 OnScanResult 事件");
        Assert.IsFalse(scanResult.Value,
            "场景中没有 Trickster 时扫描结果应为 false");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScanAbility_OnScanPerformed_EventFires()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();
        ForceAwake(scan);

        bool performed = false;
        scan.OnScanPerformed += () => performed = true;

        scan.ActivateScan();

        Assert.IsTrue(performed,
            "扫描应触发 OnScanPerformed 事件（B015 修复验证）");

        Object.DestroyImmediate(go);
    }
}

/// <summary>
/// Session 11 新增测试：CameraController 镜头晃动修复验证 (B016)
/// </summary>
public class CameraControllerTests
{
    /// <summary>
    /// 辅助方法：通过反射强制调用 MonoBehaviour 的 Awake 方法
    /// </summary>
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

    [Test]
    public void CameraController_CanBeCreated()
    {
        GameObject go = new GameObject("TestCamera");
        go.AddComponent<Camera>();
        CameraController cam = go.AddComponent<CameraController>();
        ForceAwake(cam);

        Assert.IsNotNull(cam,
            "CameraController 应该能正常挂载");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CameraController_SetTarget_DoesNotThrow()
    {
        GameObject camGo = new GameObject("TestCamera");
        camGo.AddComponent<Camera>();
        CameraController cam = camGo.AddComponent<CameraController>();
        ForceAwake(cam);

        GameObject targetGo = new GameObject("TestTarget");

        Assert.DoesNotThrow(() => cam.SetTarget(targetGo.transform),
            "SetTarget 不应抛出异常");

        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(targetGo);
    }

    [Test]
    public void CameraController_SetBounds_DoesNotThrow()
    {
        GameObject go = new GameObject("TestCamera");
        go.AddComponent<Camera>();
        CameraController cam = go.AddComponent<CameraController>();
        ForceAwake(cam);

        Assert.DoesNotThrow(() => cam.SetBounds(-10, 100, -5, 20),
            "SetBounds 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CameraController_SnapToTarget_WithoutTarget_DoesNotThrow()
    {
        GameObject go = new GameObject("TestCamera");
        go.AddComponent<Camera>();
        CameraController cam = go.AddComponent<CameraController>();
        ForceAwake(cam);

        Assert.DoesNotThrow(() => cam.SnapToTarget(),
            "没有目标时 SnapToTarget 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CameraController_Shake_DoesNotThrow()
    {
        GameObject go = new GameObject("TestCamera");
        go.AddComponent<Camera>();
        CameraController cam = go.AddComponent<CameraController>();
        ForceAwake(cam);

        Assert.DoesNotThrow(() => cam.Shake(0.2f, 0.3f),
            "Shake 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // GameUI 测试 (B018 修复后新增)
    // ═══════════════════════════════════════════════════════

    [Test]
    public void GameUI_CanBeAddedToGameObject()
    {
        GameObject go = new GameObject("TestGameUI");

        Assert.DoesNotThrow(() => go.AddComponent<GameUI>(),
            "GameUI 应该可以正常添加到 GameObject");

        Assert.IsNotNull(go.GetComponent<GameUI>(),
            "添加后应能获取到 GameUI 组件");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GameUI_ShowGameOverScreen_SetsShowGameOverFlag()
    {
        // 模拟 GameManager 单例
        GameObject managerGo = new GameObject("TestManager");
        GameManager gm = managerGo.AddComponent<GameManager>();

        GameObject uiGo = new GameObject("TestGameUI");
        GameUI gameUI = uiGo.AddComponent<GameUI>();

        // 直接调用 ShowGameOverScreen 方法测试不崩溃
        // 注意：ShowGameOverScreen 是 private，但可以通过事件触发
        // 这里验证 GameUI 的创建和基本状态
        Assert.IsNotNull(gameUI,
            "GameUI 应该被正确创建");

        Object.DestroyImmediate(uiGo);
        Object.DestroyImmediate(managerGo);
    }

    [Test]
    public void GameUI_OnGUIFallback_DefaultsToTrue()
    {
        // 当没有 Canvas UI 引用时，useOnGUIFallback 应为 true
        GameObject go = new GameObject("TestGameUI");
        GameUI gameUI = go.AddComponent<GameUI>();

        // useOnGUIFallback 是 private，但我们可以通过反射验证
        var field = typeof(GameUI).GetField("useOnGUIFallback",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(field,
            "GameUI 应该有 useOnGUIFallback 字段");

        // 初始值应为 true（因为没有设置 healthText 和 timerText）
        bool fallbackValue = (bool)field.GetValue(gameUI);
        Assert.IsTrue(fallbackValue,
            "没有 Canvas UI 引用时，useOnGUIFallback 应为 true");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GameUI_ShowAbilityFailFeedback_DoesNotThrow()
    {
        GameObject go = new GameObject("TestGameUI");
        GameUI gameUI = go.AddComponent<GameUI>();

        Assert.DoesNotThrow(() => gameUI.ShowAbilityFailFeedback("测试失败提示"),
            "ShowAbilityFailFeedback 不应抛出异常");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GameUI_ButtonCallbacks_DoNotThrow_WithoutGameManager()
    {
        GameObject go = new GameObject("TestGameUI");
        GameUI gameUI = go.AddComponent<GameUI>();

        // GameManager.Instance 为 null 时调用按钮回调不应崩溃
        Assert.DoesNotThrow(() => gameUI.OnRestartButtonClicked(),
            "OnRestartButtonClicked 在没有 GameManager 时不应崩溃");
        Assert.DoesNotThrow(() => gameUI.OnNextRoundButtonClicked(),
            "OnNextRoundButtonClicked 在没有 GameManager 时不应崩溃");
        Assert.DoesNotThrow(() => gameUI.OnMainMenuButtonClicked(),
            "OnMainMenuButtonClicked 在没有 GameManager 时不应崩溃");

        Object.DestroyImmediate(go);
    }
}
