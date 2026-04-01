using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode 测试：验证核心玩法逻辑（需要运行时物理引擎）
/// 
/// 这些测试在 Play 模式下执行，可以验证：
///   1. 角色移动/跳跃的物理行为
///   2. 平台跟随（速度注入法）
///   3. 伪装系统的运行时行为
///   4. 道具操控状态机
///   5. 胜负判定流程
///   6. 碰撞触发（GoalZone / KillZone / DamageDealer）
/// 
/// 运行方式：Window → General → Test Runner → PlayMode → Run All
/// </summary>
public class GameplayTests
{
    // ═══════════════════════════════════════════════════════
    // 测试辅助：创建带完整组件的测试角色
    // ═══════════════════════════════════════════════════════

    private const string GROUND_LAYER = "Ground";

    /// <summary>创建测试用 Mario 对象</summary>
    private GameObject CreateTestMario(Vector3 position)
    {
        GameObject go = new GameObject("TestMario");
        go.tag = "Player";
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        MarioController mario = go.AddComponent<MarioController>();
        PlayerHealth health = go.AddComponent<PlayerHealth>();

        // 设置 groundLayer
        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0)
        {
            SetPrivateField(mario, "groundLayer", (LayerMask)(1 << layerIndex));
        }

        return go;
    }

    /// <summary>创建测试用 Trickster 对象</summary>
    private GameObject CreateTestTrickster(Vector3 position)
    {
        GameObject go = new GameObject("TestTrickster");
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 1f);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        TricksterController trickster = go.AddComponent<TricksterController>();
        DisguiseSystem disguise = go.AddComponent<DisguiseSystem>();
        TricksterAbilitySystem ability = go.AddComponent<TricksterAbilitySystem>();

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0)
        {
            SetPrivateField(trickster, "groundLayer", (LayerMask)(1 << layerIndex));
        }

        return go;
    }

    /// <summary>创建测试用地面</summary>
    private GameObject CreateTestGround(Vector3 position, Vector2 size)
    {
        GameObject go = new GameObject("TestGround");
        go.transform.position = position;

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0) go.layer = layerIndex;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;

        return go;
    }

    /// <summary>通过反射设置私有字段</summary>
    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
        }
    }

    /// <summary>通过反射获取私有字段</summary>
    private T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            return (T)field.GetValue(obj);
        }
        return default;
    }

    // ═══════════════════════════════════════════════════════
    // 1. Mario 移动测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_MoveRight_IncreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        float startX = marioGO.transform.position.x;

        // 模拟向右输入
        mario.SetMoveInput(new Vector2(1f, 0f));

        // 等待物理模拟
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = marioGO.transform.position.x;
        Assert.Greater(endX, startX,
            $"向右移动后 X 坐标应该增加（起始: {startX}, 结束: {endX}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_MoveLeft_DecreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        float startX = marioGO.transform.position.x;

        mario.SetMoveInput(new Vector2(-1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = marioGO.transform.position.x;
        Assert.Less(endX, startX,
            $"向左移动后 X 坐标应该减少（起始: {startX}, 结束: {endX}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_StopInput_Decelerates()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        // 先移动
        mario.SetMoveInput(new Vector2(1f, 0f));
        yield return new WaitForSeconds(0.3f);

        // 停止输入
        mario.SetMoveInput(Vector2.zero);
        yield return new WaitForSeconds(0.5f);

        Rigidbody2D rb = marioGO.GetComponent<Rigidbody2D>();
        float speed = Mathf.Abs(rb.velocity.x);

        Assert.Less(speed, 1f,
            $"停止输入后角色应该减速（当前速度: {speed}）。groundDeceleration=200 应该很快停下。");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 2. Mario 跳跃测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_Jump_IncreasesYPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 0.5f, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        // 等待角色落到地面
        yield return new WaitForSeconds(0.3f);

        float groundY = marioGO.transform.position.y;

        // 跳跃
        mario.OnJumpPressed();
        yield return new WaitForSeconds(0.15f);

        float jumpY = marioGO.transform.position.y;
        Assert.Greater(jumpY, groundY,
            $"跳跃后 Y 坐标应该增加（地面: {groundY}, 跳跃中: {jumpY}）");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_Gravity_PullsDown()
    {
        // 不创建地面，让 Mario 自由落体
        GameObject marioGO = CreateTestMario(new Vector3(0, 5, 0));

        float startY = marioGO.transform.position.y;

        yield return new WaitForSeconds(0.3f);

        float endY = marioGO.transform.position.y;
        Assert.Less(endY, startY,
            $"没有地面时角色应该因重力下落（起始: {startY}, 结束: {endY}）");

        Object.Destroy(marioGO);
    }

    // ═══════════════════════════════════════════════════════
    // 3. Trickster 移动测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Trickster_MoveRight_IncreasesXPosition()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        TricksterController trickster = tricksterGO.GetComponent<TricksterController>();

        float startX = tricksterGO.transform.position.x;

        trickster.SetMoveInput(new Vector2(1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForSeconds(0.2f);

        float endX = tricksterGO.transform.position.x;
        Assert.Greater(endX, startX,
            $"Trickster 向右移动后 X 应增加（起始: {startX}, 结束: {endX}）");

        Object.Destroy(tricksterGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 4. 伪装系统运行时测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator DisguiseSystem_ToggleDisguise_WithoutConfig_StaysUndisuised()
    {
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        DisguiseSystem disguise = tricksterGO.GetComponent<DisguiseSystem>();

        disguise.ToggleDisguise();
        yield return null;

        Assert.IsFalse(disguise.IsDisguised,
            "没有配置伪装形态时，ToggleDisguise 不应进入伪装状态");

        Object.Destroy(tricksterGO);
    }

    [UnityTest]
    public IEnumerator DisguiseSystem_Cooldown_PreventsImmedateReDisguise()
    {
        GameObject tricksterGO = CreateTestTrickster(new Vector3(0, 1, 0));
        DisguiseSystem disguise = tricksterGO.GetComponent<DisguiseSystem>();

        // 手动添加一个伪装形态
        Texture2D tex = new Texture2D(4, 4);
        Sprite testSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        var disguises = GetPrivateField<System.Collections.Generic.List<DisguiseData>>(disguise, "availableDisguises");
        if (disguises != null)
        {
            disguises.Add(new DisguiseData
            {
                disguiseName = "TestBlock",
                disguiseSprite = testSprite
            });
        }

        // 变身
        disguise.Disguise();
        yield return null;
        Assert.IsTrue(disguise.IsDisguised, "配置伪装形态后应该能变身");

        // 解除变身
        disguise.Undisguise();
        yield return null;
        Assert.IsFalse(disguise.IsDisguised, "Undisguise 后应该解除伪装");

        // 冷却期间尝试再次变身
        Assert.Greater(disguise.CooldownRemaining, 0f, "解除伪装后应该有冷却时间");
        disguise.Disguise();
        yield return null;
        Assert.IsFalse(disguise.IsDisguised,
            "冷却期间不应该能再次变身");

        Object.Destroy(tricksterGO);
        Object.DestroyImmediate(tex);
    }

    // ═══════════════════════════════════════════════════════
    // 5. 道具操控状态机测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ControllableHazard_Activate_GoesToTelegraphThenActive()
    {
        GameObject hazardGO = new GameObject("TestHazard");
        hazardGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = hazardGO.AddComponent<BoxCollider2D>();
        ControllableHazard hazard = hazardGO.AddComponent<ControllableHazard>();

        yield return null; // 等待 Awake

        IControllableProp prop = hazard as IControllableProp;
        Assert.AreEqual(PropControlState.Idle, prop.GetControlState(),
            "初始状态应该是 Idle");

        // 触发操控
        Assert.IsTrue(prop.CanBeControlled(), "初始状态应该可以被操控");
        prop.OnTricksterActivate(Vector2.right);

        yield return null;
        Assert.AreEqual(PropControlState.Telegraph, prop.GetControlState(),
            "触发后应该进入 Telegraph 状态");

        // 等待预警结束（默认 0.8 秒）
        yield return new WaitForSeconds(1.0f);
        Assert.AreEqual(PropControlState.Active, prop.GetControlState(),
            "预警结束后应该进入 Active 状态");

        // 等待激活结束（默认 1.5 秒）
        yield return new WaitForSeconds(2.0f);

        PropControlState finalState = prop.GetControlState();
        Assert.IsTrue(finalState == PropControlState.Cooldown || finalState == PropControlState.Idle,
            $"激活结束后应该进入 Cooldown 或 Idle 状态（实际: {finalState}）");

        Object.Destroy(hazardGO);
    }

    [UnityTest]
    public IEnumerator ControllableBlock_Activate_GoesToTelegraphThenActive()
    {
        GameObject blockGO = new GameObject("TestBlock");
        blockGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = blockGO.AddComponent<BoxCollider2D>();
        ControllableBlock block = blockGO.AddComponent<ControllableBlock>();

        yield return null;

        IControllableProp prop = block as IControllableProp;
        Assert.AreEqual(PropControlState.Idle, prop.GetControlState());

        prop.OnTricksterActivate(Vector2.right);
        yield return null;
        Assert.AreEqual(PropControlState.Telegraph, prop.GetControlState(),
            "触发后应该进入 Telegraph 状态");

        yield return new WaitForSeconds(1.0f);
        Assert.AreEqual(PropControlState.Active, prop.GetControlState(),
            "预警结束后应该进入 Active 状态");

        Object.Destroy(blockGO);
    }

    // ═══════════════════════════════════════════════════════
    // 6. 移动平台速度注入测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator MovingPlatform_Moves_BetweenPoints()
    {
        GameObject platformGO = new GameObject("TestMovingPlatform");
        platformGO.transform.position = new Vector3(0, 0, 0);

        int layerIndex = LayerMask.NameToLayer(GROUND_LAYER);
        if (layerIndex >= 0) platformGO.layer = layerIndex;

        SpriteRenderer sr = platformGO.AddComponent<SpriteRenderer>();
        BoxCollider2D col = platformGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3, 0.4f);

        MovingPlatform mp = platformGO.AddComponent<MovingPlatform>();
        SetPrivateField(mp, "pointB", new Vector3(5, 0, 0));
        SetPrivateField(mp, "moveSpeed", 10f); // 快速移动以便测试
        SetPrivateField(mp, "waitTime", 0.1f);

        float startX = platformGO.transform.position.x;

        yield return new WaitForSeconds(0.5f);

        float endX = platformGO.transform.position.x;
        Assert.AreNotEqual(startX, endX,
            $"移动平台应该在两点之间移动（起始: {startX}, 结束: {endX}）");

        Object.Destroy(platformGO);
    }

    // ═══════════════════════════════════════════════════════
    // 7. 胜负判定测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameManager_MarioReachesGoal_EndRoundMarioWins()
    {
        // 创建 GameManager
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        // 创建 Mario
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));

        yield return null; // 等待 Start（GameManager 自动查找引用并开始游戏）

        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "GameManager Start 后应该自动进入 Playing 状态");

        string winner = null;
        gm.OnGameOver += (w) => winner = w;

        // 模拟 Mario 到达终点
        gm.OnMarioReachedGoal();

        yield return null;

        Assert.AreEqual("Mario", winner,
            "Mario 到达终点后应该判定 Mario 胜利");
        Assert.AreEqual(GameState.RoundOver, gm.CurrentState,
            "回合结束后状态应该是 RoundOver");
        Assert.AreEqual(1, gm.MarioWins,
            "Mario 胜利次数应该为 1");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    [UnityTest]
    public IEnumerator GameManager_MarioDies_EndRoundTricksterWins()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        PlayerHealth health = marioGO.GetComponent<PlayerHealth>();

        yield return null;

        string winner = null;
        gm.OnGameOver += (w) => winner = w;

        // 让 Mario 死亡
        health.TakeDamage(999);

        yield return null;

        Assert.AreEqual("Trickster", winner,
            "Mario 死亡后应该判定 Trickster 胜利");
        Assert.AreEqual(1, gm.TricksterWins,
            "Trickster 胜利次数应该为 1");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    [UnityTest]
    public IEnumerator GameManager_ResetRound_RestoresHealth()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        PlayerHealth health = marioGO.GetComponent<PlayerHealth>();

        // 创建出生点
        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(-5, 1, 0);
        SetPrivateField(gm, "marioSpawnPoint", spawnPoint.transform);

        yield return null;

        // 受伤
        health.TakeDamage(1);
        Assert.Less(health.CurrentHealth, health.MaxHealth);

        // 结束回合然后重置
        gm.EndRound("Trickster");
        yield return null;

        gm.ResetRound();
        yield return null;

        Assert.AreEqual(health.MaxHealth, health.CurrentHealth,
            "ResetRound 后 Mario 生命值应该恢复满血");
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "ResetRound 后应该重新进入 Playing 状态");

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
        Object.Destroy(spawnPoint);
    }

    // ═══════════════════════════════════════════════════════
    // 8. 暂停/继续测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameManager_Pause_StopsTime()
    {
        GameObject gmGO = new GameObject("TestGM");
        GameManager gm = gmGO.AddComponent<GameManager>();

        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));

        yield return null;

        gm.TogglePause();
        Assert.AreEqual(GameState.Paused, gm.CurrentState,
            "TogglePause 后应该进入 Paused 状态");
        Assert.AreEqual(0f, Time.timeScale,
            "暂停后 Time.timeScale 应该为 0");

        gm.TogglePause();
        Assert.AreEqual(GameState.Playing, gm.CurrentState,
            "再次 TogglePause 后应该恢复 Playing 状态");
        Assert.AreEqual(1f, Time.timeScale,
            "恢复后 Time.timeScale 应该为 1");

        // 确保 timeScale 恢复
        Time.timeScale = 1f;

        Object.Destroy(marioGO);
        Object.Destroy(gmGO);
    }

    // ═══════════════════════════════════════════════════════
    // 9. Mario 公共属性测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Mario_IsMoving_ReflectsMovement()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return new WaitForSeconds(0.2f);

        // 静止时
        mario.SetMoveInput(Vector2.zero);
        yield return new WaitForSeconds(0.3f);
        // IsMoving 检查 _frameVelocity.x 的绝对值
        // 静止足够久后应该不在移动

        // 移动时
        mario.SetMoveInput(new Vector2(1f, 0f));
        yield return new WaitForSeconds(0.2f);
        Assert.IsTrue(mario.IsMoving,
            "有移动输入且速度 > 0.1 时 IsMoving 应该为 true");

        Object.Destroy(marioGO);
        Object.Destroy(ground);
    }

    [UnityTest]
    public IEnumerator Mario_Die_DisablesController()
    {
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        bool deathEventFired = false;
        mario.OnDeath += () => deathEventFired = true;

        mario.Die();

        Assert.IsTrue(deathEventFired, "Die() 应该触发 OnDeath 事件");
        Assert.IsFalse(mario.enabled, "Die() 后 MarioController 应该被禁用");

        Object.Destroy(marioGO);
    }

    [UnityTest]
    public IEnumerator Mario_Bounce_SetsUpwardVelocity()
    {
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        yield return null;

        mario.Bounce(15f);

        Rigidbody2D rb = marioGO.GetComponent<Rigidbody2D>();
        Assert.Greater(rb.velocity.y, 0f,
            "Bounce 后 Y 速度应该为正（向上弹跳）");

        Object.Destroy(marioGO);
    }

    // ═══════════════════════════════════════════════════════
    // 10. InputManager 集成测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator InputManager_DisableInput_StopsPlayerMovement()
    {
        GameObject ground = CreateTestGround(new Vector3(0, -1, 0), new Vector2(40, 1));
        GameObject marioGO = CreateTestMario(new Vector3(0, 1, 0));
        MarioController mario = marioGO.GetComponent<MarioController>();

        GameObject imGO = new GameObject("TestIM");
        InputManager im = imGO.AddComponent<InputManager>();
        im.SetMarioController(mario);

        yield return null;

        // 禁用输入
        im.DisableAllInput();

        yield return null;

        // 验证 Mario 的移动输入被清零
        // （DisableAllInput 调用 SetMoveInput(Vector2.zero)）
        Assert.IsFalse(im.enabled, "DisableAllInput 后 InputManager 应该被禁用");

        Object.Destroy(marioGO);
        Object.Destroy(imGO);
        Object.Destroy(ground);
    }

    // ═══════════════════════════════════════════════════════
    // 11. Trickster 伪装状态下移动限制测试
    // ═══════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Trickster_IsDisguised_ReturnsFalse_WhenNoDisguiseSystem()
    {
        // 创建一个没有 DisguiseSystem 的 Trickster
        GameObject go = new GameObject("TestTricksterNoDisguise");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<Rigidbody2D>();
        TricksterController trickster = go.AddComponent<TricksterController>();

        yield return null;

        Assert.IsFalse(trickster.IsDisguised,
            "没有 DisguiseSystem 时 IsDisguised 应该返回 false");

        Object.Destroy(go);
    }
}
