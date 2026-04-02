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
/// </summary>
public class ComponentSetupTests
{
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

        // Awake 在 AddComponent 时自动调用
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

        string status = disguise.GetDebugStatus();
        Assert.IsTrue(status.Contains("未配置"),
            "没有配置伪装形态时，GetDebugStatus 应该提示未配置");

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
        go.AddComponent<MovingPlatform>();

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
        go.AddComponent<GoalZone>();

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
        go.AddComponent<KillZone>();

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
        // 验证 Instance 属性存在
        Assert.DoesNotThrow(() =>
        {
            var _ = GameManager.Instance;
        }, "GameManager 应该有 Instance 静态属性");
    }

    // ═══════════════════════════════════════════════════════
    // InputManager 测试
    // ═══════════════════════════════════════════════════════

    [Test]
    public void InputManager_HasPlayerSetterMethods()
    {
        GameObject go = new GameObject("TestIM");
        InputManager im = go.AddComponent<InputManager>();

        // 验证运行时设置玩家引用的方法存在
        Assert.DoesNotThrow(() => im.SetMarioController(null),
            "InputManager 应该有 SetMarioController 方法");
        Assert.DoesNotThrow(() => im.SetTricksterController(null),
            "InputManager 应该有 SetTricksterController 方法");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void InputManager_DisableEnableInput()
    {
        GameObject go = new GameObject("TestIM");
        InputManager im = go.AddComponent<InputManager>();

        Assert.DoesNotThrow(() => im.DisableAllInput(),
            "InputManager.DisableAllInput 不应抛出异常");
        Assert.IsFalse(im.enabled,
            "DisableAllInput 后 InputManager 应该被禁用");

        Assert.DoesNotThrow(() => im.EnableAllInput(),
            "InputManager.EnableAllInput 不应抛出异常");
        Assert.IsTrue(im.enabled,
            "EnableAllInput 后 InputManager 应该被启用");

        Object.DestroyImmediate(go);
    }

    // ═══════════════════════════════════════════════════════
    // ControllableProp 接口测试
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

        IControllableProp prop = hazard as IControllableProp;
        Assert.IsNotNull(prop.PropName, "PropName 不应为 null");
        Assert.AreEqual(PropControlState.Idle, prop.GetControlState(),
            "初始状态应该是 Idle");

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
/// </summary>
public class EnergySystemTests
{
    [Test]
    public void EnergySystem_InitializesWithMaxEnergy()
    {
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

        Assert.AreEqual(energy.MaxEnergy, energy.CurrentEnergy,
            "EnergySystem 初始化时 currentEnergy 应等于 maxEnergy");
        Assert.AreEqual(1f, energy.EnergyPercent, 0.01f,
            "EnergySystem 初始化时 EnergyPercent 应为 1.0");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_HasEnoughForDisguise_InitiallyTrue()
    {
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

        Assert.IsTrue(energy.HasEnoughForDisguise,
            "满能量时应该有足够能量变身");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_HasEnoughForControl_InitiallyTrue()
    {
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

        Assert.IsTrue(energy.HasEnoughForControl,
            "满能量时应该有足够能量操控道具");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void EnergySystem_TryConsumeDisguiseCost_ReducesEnergy()
    {
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

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
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

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
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

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
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

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
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

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
        GameObject go = new GameObject("TestEnergy");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<DisguiseSystem>();
        EnergySystem energy = go.AddComponent<EnergySystem>();

        bool eventFired = false;
        energy.OnEnergyChanged += (current, max) => eventFired = true;

        energy.TryConsumeDisguiseCost();

        Assert.IsTrue(eventFired, "消耗能量后应触发 OnEnergyChanged 事件");

        Object.DestroyImmediate(go);
    }
}

public class ScanAbilityTests
{
    [Test]
    public void ScanAbility_InitialState_IsReady()
    {
        GameObject go = new GameObject("TestScan");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        ScanAbility scan = go.AddComponent<ScanAbility>();

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

        bool? scanResult = null;
        scan.OnScanResult += (found) => scanResult = found;

        scan.ActivateScan();

        Assert.IsNotNull(scanResult, "扫描应触发 OnScanResult 事件");
        Assert.IsFalse(scanResult.Value,
            "场景中没有 Trickster 时扫描结果应为 false");

        Object.DestroyImmediate(go);
    }
}
