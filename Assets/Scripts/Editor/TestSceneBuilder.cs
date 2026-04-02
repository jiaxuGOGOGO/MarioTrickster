using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键生成测试场景的 Editor 工具
/// 
/// 使用方式：Unity 菜单栏 → MarioTrickster → Build Test Scene
/// 
/// 自动创建：
///   - 地面平台（长条 BoxCollider2D）
///   - Mario 角色（完整组件挂载）
///   - Trickster 角色（完整组件挂载 + 伪装系统 + 能力系统）
///   - 管理器对象（GameManager + InputManager + LevelManager）
///   - 移动平台（MovingPlatform）
///   - 可操控平台（ControllablePlatform）
///   - 可操控陷阱（ControllableHazard）
///   - 可操控方块（ControllableBlock）
///   - 终点区域（GoalZone）
///   - 死亡区域（KillZone）
///   - 简单敌人（SimpleEnemy）
///   - 可收集物品（Collectible）
///   - 可破坏方块（Breakable）
///   - 相机跟随（CameraController）
///   - 游戏UI（GameUI - HUD/胜负画面/暂停画面）
///   - [Session 15 新增] 关卡设计系统元素:
///     - 地刺陷阱（SpikeTrap）
///     - 摆锤绳索（PendulumTrap）
///     - 火焰陷阱（FireTrap）
///     - 弹跳怪物（BouncingEnemy）
///     - 弹跳平台（BouncyPlatform）
///     - 单向平台（OneWayPlatform）
///     - 崩塌平台（CollapsingPlatform）
///     - 隐藏通道（HiddenPassage）
///     - 伪装墙壁（FakeWall）
///   - 所有 Inspector 引用自动连线
///   - Ground Layer 自动创建和分配
/// 
/// 低耦合设计：所有新增关卡元素通过 LevelElementRegistry 自动注册，
/// 删除任何元素脚本不会导致编译错误（只需同步删除本文件中对应的 Create 方法调用）。
/// </summary>
public class TestSceneBuilder : Editor
{
    private const string GROUND_LAYER_NAME = "Ground";
    private const int GROUND_SORTING_ORDER = -10;

    [MenuItem("MarioTrickster/Build Test Scene", false, 1)]
    public static void BuildTestScene()
    {
        // 确认操作
        if (!EditorUtility.DisplayDialog(
            "MarioTrickster - 生成测试场景",
            "这将在当前场景中生成完整的测试关卡。\n\n" +
            "包含：地面、Mario、Trickster、管理器、移动平台、\n" +
            "可操控道具、终点、死亡区域、敌人、收集物、\n" +
            "[新] 地刺/摆锤/火焰/弹跳怪/弹跳平台/单向平台/崩塌平台/隐藏通道/伪装墙 等。\n\n" +
            "建议在空白场景中执行。继续？",
            "生成", "取消"))
        {
            return;
        }

        // 确保 Ground Layer 存在
        int groundLayerIndex = EnsureLayerExists(GROUND_LAYER_NAME);
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // ═══════════════════════════════════════════════════
        // 1. 地面
        // ═══════════════════════════════════════════════════
        GameObject ground = CreateGround("Ground_Main", new Vector3(0, -1, 0), new Vector2(50, 1), groundLayerIndex);
        
        // 额外平台（高处）
        GameObject platform1 = CreateGround("Ground_Platform_Left", new Vector3(-6, 2, 0), new Vector2(4, 0.5f), groundLayerIndex);
        GameObject platform2 = CreateGround("Ground_Platform_Right", new Vector3(6, 3, 0), new Vector2(4, 0.5f), groundLayerIndex);
        GameObject platform3 = CreateGround("Ground_Platform_High", new Vector3(0, 5, 0), new Vector2(3, 0.5f), groundLayerIndex);
        // Session 15: 额外平台用于测试新关卡元素
        GameObject platform4 = CreateGround("Ground_Platform_Far", new Vector3(14, 2, 0), new Vector2(5, 0.5f), groundLayerIndex);
        GameObject platform5 = CreateGround("Ground_Platform_Underground", new Vector3(-10, -3, 0), new Vector2(6, 0.5f), groundLayerIndex);

        // 左墙壁（防止角色掉出关卡左侧）
        GameObject wallLeft = CreateGround("Wall_Left", new Vector3(-25.5f, 5, 0), new Vector2(1, 14), groundLayerIndex);
        // 右墙壁
        GameObject wallRight = CreateGround("Wall_Right", new Vector3(25.5f, 5, 0), new Vector2(1, 14), groundLayerIndex);

        // ═══════════════════════════════════════════════════
        // 2. Mario
        // ═══════════════════════════════════════════════════
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.layer = LayerMask.NameToLayer("Default");
        mario.transform.position = new Vector3(-8, 1, 0);

        SpriteRenderer marioSR = mario.AddComponent<SpriteRenderer>();
        marioSR.color = new Color(0.9f, 0.2f, 0.2f);
        marioSR.sortingOrder = 10;

        BoxCollider2D marioCol = mario.AddComponent<BoxCollider2D>();
        marioCol.size = new Vector2(0.8f, 1f);

        Rigidbody2D marioRb = mario.AddComponent<Rigidbody2D>();
        marioRb.gravityScale = 0f;
        marioRb.freezeRotation = true;
        marioRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        MarioController marioCtrl = mario.AddComponent<MarioController>();
        PlayerHealth marioHealth = mario.AddComponent<PlayerHealth>();
        ScanAbility scanAbility = mario.AddComponent<ScanAbility>();

        SetSerializedField(marioCtrl, "groundLayer", groundLayerMask);
        AssignDefaultSprite(marioSR, Color.red);

        // ═══════════════════════════════════════════════════
        // 3. Trickster
        // ═══════════════════════════════════════════════════
        GameObject trickster = new GameObject("Trickster");
        trickster.layer = LayerMask.NameToLayer("Default");
        trickster.transform.position = new Vector3(8, 1, 0);

        SpriteRenderer tricksterSR = trickster.AddComponent<SpriteRenderer>();
        tricksterSR.color = new Color(0.2f, 0.4f, 0.9f);
        tricksterSR.sortingOrder = 10;

        BoxCollider2D tricksterCol = trickster.AddComponent<BoxCollider2D>();
        tricksterCol.size = new Vector2(0.8f, 1f);

        Rigidbody2D tricksterRb = trickster.AddComponent<Rigidbody2D>();
        tricksterRb.gravityScale = 0f;
        tricksterRb.freezeRotation = true;
        tricksterRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        TricksterController tricksterCtrl = trickster.AddComponent<TricksterController>();
        DisguiseSystem disguiseSystem = trickster.AddComponent<DisguiseSystem>();
        TricksterAbilitySystem abilitySystem = trickster.AddComponent<TricksterAbilitySystem>();
        EnergySystem energySystem = trickster.AddComponent<EnergySystem>();

        SetSerializedField(tricksterCtrl, "groundLayer", groundLayerMask);
        AssignDefaultSprite(tricksterSR, Color.blue);

        // ═══════════════════════════════════════════════════
        // 4. 管理器
        // ═══════════════════════════════════════════════════
        GameObject managers = new GameObject("Managers");

        GameManager gameManager = managers.AddComponent<GameManager>();
        InputManager inputManager = managers.AddComponent<InputManager>();
        LevelManager levelManager = managers.AddComponent<LevelManager>();

        SetSerializedField(inputManager, "marioController", marioCtrl);
        SetSerializedField(inputManager, "tricksterController", tricksterCtrl);
        SetSerializedField(gameManager, "mario", marioCtrl);
        SetSerializedField(gameManager, "trickster", tricksterCtrl);
        SetSerializedField(gameManager, "marioHealth", marioHealth);
        SetSerializedField(gameManager, "inputManager", inputManager);

        GameObject marioSpawn = new GameObject("MarioSpawnPoint");
        marioSpawn.transform.position = new Vector3(-8, 1, 0);
        marioSpawn.transform.parent = managers.transform;

        GameObject tricksterSpawn = new GameObject("TricksterSpawnPoint");
        tricksterSpawn.transform.position = new Vector3(8, 1, 0);
        tricksterSpawn.transform.parent = managers.transform;

        SetSerializedField(gameManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(gameManager, "tricksterSpawnPoint", tricksterSpawn.transform);
        SetSerializedField(levelManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(levelManager, "tricksterSpawnPoint", tricksterSpawn.transform);

        // ═══════════════════════════════════════════════════
        // 5. 相机
        // ═══════════════════════════════════════════════════
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController[] existingCamCtrls = mainCam.GetComponents<CameraController>();
            for (int i = 0; i < existingCamCtrls.Length; i++)
                DestroyImmediate(existingCamCtrls[i]);

            CameraController camCtrl = mainCam.gameObject.AddComponent<CameraController>();
            SetSerializedField(camCtrl, "target", mario.transform);
            mainCam.transform.position = new Vector3(0, 2, -10);
            mainCam.orthographicSize = 7;
        }

        // ═══════════════════════════════════════════════════
        // 6. 移动平台
        // ═══════════════════════════════════════════════════
        GameObject movingPlatform = CreatePlatformObject("MovingPlatform", new Vector3(-3, 1.5f, 0), new Vector2(3, 0.4f), groundLayerIndex);
        MovingPlatform mp = movingPlatform.AddComponent<MovingPlatform>();
        SetSerializedField(mp, "pointB", new Vector3(6, 0, 0));
        SetSerializedField(mp, "moveSpeed", 2f);

        // ═══════════════════════════════════════════════════
        // 7. 可操控平台
        // ═══════════════════════════════════════════════════
        GameObject controllablePlatform = CreatePlatformObject("ControllablePlatform", new Vector3(12, 2, 0), new Vector2(3, 0.4f), groundLayerIndex);
        ControllablePlatform cp = controllablePlatform.AddComponent<ControllablePlatform>();
        SetSerializedField(cp, "pointB", new Vector3(0, 4, 0));
        SetSerializedField(cp, "normalMoveSpeed", 1.5f);

        // ═══════════════════════════════════════════════════
        // 8. 可操控陷阱
        // ═══════════════════════════════════════════════════
        GameObject hazard = new GameObject("ControllableHazard");
        hazard.transform.position = new Vector3(4, 0, 0);
        hazard.layer = groundLayerIndex;

        SpriteRenderer hazardSR = hazard.AddComponent<SpriteRenderer>();
        hazardSR.color = new Color(1f, 0.5f, 0f);
        hazardSR.sortingOrder = 5;
        AssignDefaultSprite(hazardSR, new Color(1f, 0.5f, 0f));

        BoxCollider2D hazardCol = hazard.AddComponent<BoxCollider2D>();
        hazardCol.size = new Vector2(1f, 0.5f);
        hazardCol.isTrigger = true;

        ControllableHazard ch = hazard.AddComponent<ControllableHazard>();

        // ═══════════════════════════════════════════════════
        // 9. 可操控方块
        // ═══════════════════════════════════════════════════
        GameObject block = new GameObject("ControllableBlock");
        block.transform.position = new Vector3(-4, 2.5f, 0);
        block.layer = groundLayerIndex;

        SpriteRenderer blockSR = block.AddComponent<SpriteRenderer>();
        blockSR.color = new Color(0.8f, 0.6f, 0.2f);
        blockSR.sortingOrder = 5;
        AssignDefaultSprite(blockSR, new Color(0.8f, 0.6f, 0.2f));

        BoxCollider2D blockCol = block.AddComponent<BoxCollider2D>();
        blockCol.size = new Vector2(1f, 1f);

        ControllableBlock cb = block.AddComponent<ControllableBlock>();

        // ═══════════════════════════════════════════════════
        // 10. 终点区域
        // ═══════════════════════════════════════════════════
        GameObject goal = new GameObject("GoalZone");
        goal.transform.position = new Vector3(22, 1, 0);

        SpriteRenderer goalSR = goal.AddComponent<SpriteRenderer>();
        goalSR.color = new Color(0f, 1f, 0f, 0.5f);
        goalSR.sortingOrder = 3;
        AssignDefaultSprite(goalSR, new Color(0f, 1f, 0f, 0.5f));

        BoxCollider2D goalCol = goal.AddComponent<BoxCollider2D>();
        goalCol.size = new Vector2(1f, 3f);
        goalCol.isTrigger = true;

        goal.AddComponent<GoalZone>();

        // ═══════════════════════════════════════════════════
        // 11. 死亡区域（关卡底部）
        // ═══════════════════════════════════════════════════
        GameObject killZone = new GameObject("KillZone");
        killZone.transform.position = new Vector3(0, -10, 0);

        BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
        killCol.size = new Vector2(80, 2);
        killCol.isTrigger = true;

        killZone.AddComponent<KillZone>();

        // ═══════════════════════════════════════════════════
        // 12. 简单敌人
        // ═══════════════════════════════════════════════════
        GameObject enemy = new GameObject("SimpleEnemy");
        enemy.transform.position = new Vector3(3, 0.5f, 0);

        SpriteRenderer enemySR = enemy.AddComponent<SpriteRenderer>();
        enemySR.color = new Color(0.6f, 0f, 0.6f);
        enemySR.sortingOrder = 8;
        AssignDefaultSprite(enemySR, new Color(0.6f, 0f, 0.6f));

        BoxCollider2D enemyCol = enemy.AddComponent<BoxCollider2D>();
        enemyCol.size = new Vector2(0.8f, 0.8f);

        Rigidbody2D enemyRb = enemy.AddComponent<Rigidbody2D>();
        enemyRb.gravityScale = 1f;
        enemyRb.freezeRotation = true;

        enemy.AddComponent<SimpleEnemy>();
        enemy.AddComponent<DamageDealer>();

        // ═══════════════════════════════════════════════════
        // 13. 可收集物品
        // ═══════════════════════════════════════════════════
        for (int i = 0; i < 5; i++)
        {
            GameObject coin = new GameObject($"Coin_{i}");
            coin.transform.position = new Vector3(-5 + i * 3, 1.5f, 0);

            SpriteRenderer coinSR = coin.AddComponent<SpriteRenderer>();
            coinSR.color = new Color(1f, 0.85f, 0f);
            coinSR.sortingOrder = 6;
            AssignDefaultSprite(coinSR, new Color(1f, 0.85f, 0f));
            coin.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            BoxCollider2D coinCol = coin.AddComponent<BoxCollider2D>();
            coinCol.isTrigger = true;

            coin.AddComponent<Collectible>();
        }

        // ═══════════════════════════════════════════════════
        // 14. 可破坏方块
        // ═══════════════════════════════════════════════════
        GameObject breakable = new GameObject("BreakableBlock");
        breakable.transform.position = new Vector3(0, 3, 0);
        breakable.layer = groundLayerIndex;

        SpriteRenderer breakSR = breakable.AddComponent<SpriteRenderer>();
        breakSR.color = new Color(0.9f, 0.7f, 0.3f);
        breakSR.sortingOrder = 5;
        AssignDefaultSprite(breakSR, new Color(0.9f, 0.7f, 0.3f));

        BoxCollider2D breakCol = breakable.AddComponent<BoxCollider2D>();
        breakCol.size = new Vector2(1f, 1f);

        breakable.AddComponent<Breakable>();

        // ═══════════════════════════════════════════════════
        // 15. 游戏UI
        // ═══════════════════════════════════════════════════
        GameUI[] existingGameUIs = FindObjectsOfType<GameUI>();
        foreach (GameUI existingUI in existingGameUIs)
            DestroyImmediate(existingUI.gameObject);

        GameObject uiObject = new GameObject("GameUI");
        uiObject.transform.parent = managers.transform;
        GameUI gameUI = uiObject.AddComponent<GameUI>();

        // ═════════════════════════════════════════════════════════
        // ██ Session 15: 关卡设计系统新增元素 ██
        // ═════════════════════════════════════════════════════════

        // ═══════════════════════════════════════════════════
        // 16. 地刺陷阱 (SpikeTrap) - 周期伸缩
        // ═══════════════════════════════════════════════════
        GameObject spikeParent = new GameObject("SpikeTrap_Periodic");
        spikeParent.transform.position = new Vector3(-2, -0.3f, 0);

        SpriteRenderer spikeSR = spikeParent.AddComponent<SpriteRenderer>();
        spikeSR.color = new Color(0.7f, 0.1f, 0.1f);
        spikeSR.sortingOrder = 5;
        AssignDefaultSprite(spikeSR, new Color(0.7f, 0.1f, 0.1f));
        spikeParent.transform.localScale = new Vector3(1.5f, 0.4f, 1f);

        BoxCollider2D spikeCol = spikeParent.AddComponent<BoxCollider2D>();
        spikeCol.size = Vector2.one;
        spikeCol.isTrigger = true;

        spikeParent.AddComponent<SpikeTrap>();

        // 静态地刺
        GameObject spikeStatic = new GameObject("SpikeTrap_Static");
        spikeStatic.transform.position = new Vector3(10, -0.3f, 0);

        SpriteRenderer spikeStaticSR = spikeStatic.AddComponent<SpriteRenderer>();
        spikeStaticSR.color = new Color(0.9f, 0.1f, 0.1f);
        spikeStaticSR.sortingOrder = 5;
        AssignDefaultSprite(spikeStaticSR, new Color(0.9f, 0.1f, 0.1f));
        spikeStatic.transform.localScale = new Vector3(2f, 0.3f, 1f);

        BoxCollider2D spikeStaticCol = spikeStatic.AddComponent<BoxCollider2D>();
        spikeStaticCol.size = Vector2.one;
        spikeStaticCol.isTrigger = true;

        SpikeTrap spikeStaticTrap = spikeStatic.AddComponent<SpikeTrap>();
        // 设置为静态模式
        SetSerializedField(spikeStaticTrap, "mode", 0); // SpikeMode.Static = 0

        // ═══════════════════════════════════════════════════
        // 17. 摆锤绳索 (PendulumTrap)
        // ═══════════════════════════════════════════════════
        GameObject pendulum = new GameObject("PendulumTrap");
        pendulum.transform.position = new Vector3(2, 6, 0);

        SpriteRenderer pendulumSR = pendulum.AddComponent<SpriteRenderer>();
        pendulumSR.color = new Color(0.6f, 0.4f, 0.2f);
        pendulumSR.sortingOrder = 3;
        AssignDefaultSprite(pendulumSR, new Color(0.6f, 0.4f, 0.2f));
        pendulum.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        pendulum.AddComponent<PendulumTrap>();

        // ═══════════════════════════════════════════════════
        // 18. 火焰陷阱 (FireTrap)
        // ═══════════════════════════════════════════════════
        GameObject fireTrap = new GameObject("FireTrap");
        fireTrap.transform.position = new Vector3(8, -0.3f, 0);

        SpriteRenderer fireSR = fireTrap.AddComponent<SpriteRenderer>();
        fireSR.color = new Color(1f, 0.4f, 0f);
        fireSR.sortingOrder = 5;
        AssignDefaultSprite(fireSR, new Color(1f, 0.4f, 0f));
        fireTrap.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        BoxCollider2D fireCol = fireTrap.AddComponent<BoxCollider2D>();
        fireCol.size = Vector2.one;

        fireTrap.AddComponent<FireTrap>();

        // ═══════════════════════════════════════════════════
        // 19. 弹跳怪物 (BouncingEnemy)
        // ═══════════════════════════════════════════════════
        GameObject bouncingEnemy = new GameObject("BouncingEnemy");
        bouncingEnemy.transform.position = new Vector3(-5, 0.5f, 0);

        SpriteRenderer bounceSR = bouncingEnemy.AddComponent<SpriteRenderer>();
        bounceSR.color = new Color(0.8f, 0.2f, 0.8f);
        bounceSR.sortingOrder = 8;
        AssignDefaultSprite(bounceSR, new Color(0.8f, 0.2f, 0.8f));
        bouncingEnemy.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        BoxCollider2D bounceCol = bouncingEnemy.AddComponent<BoxCollider2D>();
        bounceCol.size = Vector2.one;

        Rigidbody2D bounceRb = bouncingEnemy.AddComponent<Rigidbody2D>();
        bounceRb.gravityScale = 2f;
        bounceRb.freezeRotation = true;

        bouncingEnemy.AddComponent<BouncingEnemy>();

        // ═══════════════════════════════════════════════════
        // 20. 弹跳平台 (BouncyPlatform)
        // ═══════════════════════════════════════════════════
        GameObject bouncyPlatform = new GameObject("BouncyPlatform");
        bouncyPlatform.transform.position = new Vector3(16, -0.3f, 0);
        bouncyPlatform.layer = groundLayerIndex;

        SpriteRenderer bouncyPlatSR = bouncyPlatform.AddComponent<SpriteRenderer>();
        bouncyPlatSR.color = new Color(0.2f, 0.9f, 0.3f);
        bouncyPlatSR.sortingOrder = 5;
        AssignDefaultSprite(bouncyPlatSR, new Color(0.2f, 0.9f, 0.3f));
        bouncyPlatform.transform.localScale = new Vector3(2f, 0.4f, 1f);

        BoxCollider2D bouncyCol = bouncyPlatform.AddComponent<BoxCollider2D>();
        bouncyCol.size = Vector2.one;

        bouncyPlatform.AddComponent<BouncyPlatform>();

        // ═══════════════════════════════════════════════════
        // 21. 单向平台 (OneWayPlatform)
        // ═══════════════════════════════════════════════════
        GameObject oneWayPlatform = new GameObject("OneWayPlatform");
        oneWayPlatform.transform.position = new Vector3(-8, 3, 0);
        oneWayPlatform.layer = groundLayerIndex;

        SpriteRenderer oneWaySR = oneWayPlatform.AddComponent<SpriteRenderer>();
        oneWaySR.color = new Color(0.5f, 0.8f, 1f);
        oneWaySR.sortingOrder = 2;
        AssignDefaultSprite(oneWaySR, new Color(0.5f, 0.8f, 1f));
        oneWayPlatform.transform.localScale = new Vector3(3f, 0.3f, 1f);

        BoxCollider2D oneWayCol = oneWayPlatform.AddComponent<BoxCollider2D>();
        oneWayCol.size = Vector2.one;

        oneWayPlatform.AddComponent<PlatformEffector2D>();
        oneWayPlatform.AddComponent<OneWayPlatform>();

        // ═══════════════════════════════════════════════════
        // 22. 崩塌平台 (CollapsingPlatform)
        // ═══════════════════════════════════════════════════
        GameObject collapsingPlatform = new GameObject("CollapsingPlatform");
        collapsingPlatform.transform.position = new Vector3(18, 4, 0);
        collapsingPlatform.layer = groundLayerIndex;

        SpriteRenderer collapseSR = collapsingPlatform.AddComponent<SpriteRenderer>();
        collapseSR.color = new Color(0.8f, 0.6f, 0.2f);
        collapseSR.sortingOrder = 2;
        AssignDefaultSprite(collapseSR, new Color(0.8f, 0.6f, 0.2f));
        collapsingPlatform.transform.localScale = new Vector3(3f, 0.4f, 1f);

        BoxCollider2D collapseCol = collapsingPlatform.AddComponent<BoxCollider2D>();
        collapseCol.size = Vector2.one;

        collapsingPlatform.AddComponent<CollapsingPlatform>();

        // ═══════════════════════════════════════════════════
        // 23. 隐藏通道 (HiddenPassage) - 入口+出口
        // ═══════════════════════════════════════════════════
        // 出口点（地下区域）
        GameObject passageExit = new GameObject("HiddenPassage_Exit");
        passageExit.transform.position = new Vector3(-10, -2.5f, 0);

        // 入口
        GameObject passageEntrance = new GameObject("HiddenPassage_Entrance");
        passageEntrance.transform.position = new Vector3(-12, 0, 0);

        SpriteRenderer passageSR = passageEntrance.AddComponent<SpriteRenderer>();
        passageSR.color = new Color(0f, 0.8f, 0.8f, 0.3f);
        passageSR.sortingOrder = 4;
        AssignDefaultSprite(passageSR, new Color(0f, 0.8f, 0.8f, 0.3f));
        passageEntrance.transform.localScale = new Vector3(1f, 1.5f, 1f);

        BoxCollider2D passageCol = passageEntrance.AddComponent<BoxCollider2D>();
        passageCol.size = Vector2.one;
        passageCol.isTrigger = true;

        HiddenPassage hp = passageEntrance.AddComponent<HiddenPassage>();
        SetSerializedField(hp, "exitPoint", passageExit.transform);

        // 出口也加视觉标识
        SpriteRenderer exitSR = passageExit.AddComponent<SpriteRenderer>();
        exitSR.color = new Color(0f, 0.8f, 0.8f, 0.5f);
        exitSR.sortingOrder = 4;
        AssignDefaultSprite(exitSR, new Color(0f, 0.8f, 0.8f, 0.5f));
        passageExit.transform.localScale = new Vector3(1f, 1.5f, 1f);

        // ═══════════════════════════════════════════════════
        // 24. 伪装墙壁 (FakeWall)
        // ═══════════════════════════════════════════════════
        GameObject fakeWall = new GameObject("FakeWall");
        fakeWall.transform.position = new Vector3(20, 1, 0);
        fakeWall.layer = groundLayerIndex;

        SpriteRenderer fakeWallSR = fakeWall.AddComponent<SpriteRenderer>();
        fakeWallSR.color = new Color(0.4f, 0.3f, 0.2f); // 与地面同色，伪装效果
        fakeWallSR.sortingOrder = 1;
        AssignDefaultSprite(fakeWallSR, new Color(0.4f, 0.3f, 0.2f));
        fakeWall.transform.localScale = new Vector3(1f, 3f, 1f);

        BoxCollider2D fakeWallCol = fakeWall.AddComponent<BoxCollider2D>();
        fakeWallCol.size = Vector2.one;
        fakeWallCol.isTrigger = true;

        fakeWall.AddComponent<FakeWall>();

        // ═════════════════════════════════════════════════════════
        // 场景整理：创建父对象分组
        // ═════════════════════════════════════════════════════════
        GameObject levelGeometry = new GameObject("--- Level Geometry ---");
        ground.transform.parent = levelGeometry.transform;
        platform1.transform.parent = levelGeometry.transform;
        platform2.transform.parent = levelGeometry.transform;
        platform3.transform.parent = levelGeometry.transform;
        platform4.transform.parent = levelGeometry.transform;
        platform5.transform.parent = levelGeometry.transform;
        wallLeft.transform.parent = levelGeometry.transform;
        wallRight.transform.parent = levelGeometry.transform;

        GameObject interactables = new GameObject("--- Interactables ---");
        movingPlatform.transform.parent = interactables.transform;
        controllablePlatform.transform.parent = interactables.transform;
        hazard.transform.parent = interactables.transform;
        block.transform.parent = interactables.transform;
        goal.transform.parent = interactables.transform;
        killZone.transform.parent = interactables.transform;
        enemy.transform.parent = interactables.transform;
        breakable.transform.parent = interactables.transform;

        // Session 15: 关卡设计系统分组
        GameObject levelElements = new GameObject("--- Level Elements (Session 15) ---");

        GameObject trapsGroup = new GameObject("Traps");
        trapsGroup.transform.parent = levelElements.transform;
        spikeParent.transform.parent = trapsGroup.transform;
        spikeStatic.transform.parent = trapsGroup.transform;
        pendulum.transform.parent = trapsGroup.transform;
        fireTrap.transform.parent = trapsGroup.transform;
        bouncingEnemy.transform.parent = trapsGroup.transform;

        GameObject platformsGroup = new GameObject("Platforms");
        platformsGroup.transform.parent = levelElements.transform;
        bouncyPlatform.transform.parent = platformsGroup.transform;
        oneWayPlatform.transform.parent = platformsGroup.transform;
        collapsingPlatform.transform.parent = platformsGroup.transform;

        GameObject hiddenGroup = new GameObject("HiddenPassages");
        hiddenGroup.transform.parent = levelElements.transform;
        passageEntrance.transform.parent = hiddenGroup.transform;
        passageExit.transform.parent = hiddenGroup.transform;
        fakeWall.transform.parent = hiddenGroup.transform;

        GameObject collectibles = new GameObject("--- Collectibles ---");
        foreach (Collectible c in FindObjectsOfType<Collectible>())
            c.transform.parent = collectibles.transform;

        // ═══════════════════════════════════════════════════
        // 标记场景为已修改
        // ═══════════════════════════════════════════════════
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[TestSceneBuilder] ✅ 测试场景生成完成！\n" +
                  "  - 地面 + 5个平台 + 2面墙壁\n" +
                  "  - Mario (红色, 位置: -8,1)\n" +
                  "  - Trickster (蓝色, 位置: 8,1)\n" +
                  "  - 管理器 (GameManager + InputManager + LevelManager)\n" +
                  "  - 移动平台 / 可操控平台 / 陷阱 / 方块\n" +
                  "  - 终点 / 死亡区域 / 敌人 / 金币 / 可破坏方块\n" +
                  "  - [新] 地刺陷阱 x2 (周期+静态)\n" +
                  "  - [新] 摆锤绳索 (糖秋千式)\n" +
                  "  - [新] 火焰陷阱 (周期喷射)\n" +
                  "  - [新] 弹跳怪物 (可踩踏消灭)\n" +
                  "  - [新] 弹跳平台 (蘑菇弹簧)\n" +
                  "  - [新] 单向平台 (可从下方穿过)\n" +
                  "  - [新] 崩塌平台 (踩上后崩塌)\n" +
                  "  - [新] 隐藏通道 (入口→地下出口)\n" +
                  "  - [新] 伪装墙壁 (可穿过的假墙)\n" +
                  "  - 相机跟随 Mario / GameUI\n" +
                  "  - LevelElementRegistry 自动管理所有新元素\n\n" +
                  "  ⚠️ 请保存场景：Ctrl+S");

        EditorUtility.DisplayDialog(
            "生成完成",
            "测试场景已生成！\n\n" +
            "接下来请：\n" +
            "1. Ctrl+S 保存场景\n" +
            "2. 点击 Play 运行测试\n" +
            "3. P1: WASD 移动 + Space 跳跃 + Q 扫描\n" +
            "4. P2: 方向键移动 + P 伪装 + L 操控\n\n" +
            "[Session 15 新增]\n" +
            "- 地刺/摆锤/火焰陷阱（自动运行）\n" +
            "- 弹跳怪物（可踩踏消灭）\n" +
            "- 弹跳/单向/崩塌平台\n" +
            "- 隐藏通道 + 伪装墙壁\n" +
            "- 所有新元素可被Trickster操控（L键）",
            "好的");
    }

    [MenuItem("MarioTrickster/Build Test Scene", true)]
    public static bool ValidateBuildTestScene()
    {
        return !EditorApplication.isPlaying;
    }

    [MenuItem("MarioTrickster/Clear Test Scene", false, 2)]
    public static void ClearTestScene()
    {
        if (!EditorUtility.DisplayDialog(
            "清空测试场景",
            "这将删除场景中除 Main Camera 和 Directional Light 外的所有对象。\n" +
            "同时清空 LevelElementRegistry。\n继续？",
            "清空", "取消"))
        {
            return;
        }

        // 清理相机上的 CameraController
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController[] camCtrls = mainCam.GetComponents<CameraController>();
            for (int i = 0; i < camCtrls.Length; i++)
                DestroyImmediate(camCtrls[i]);
        }

        // 清空 Registry
        LevelElementRegistry.Clear();

        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj == null) continue;
            if (obj.GetComponent<Camera>() != null) continue;
            if (obj.GetComponent<Light>() != null) continue;
            if (obj.transform.parent != null) continue;

            DestroyImmediate(obj);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneBuilder] 场景已清空（含 LevelElementRegistry）。");
    }

    // ═══════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════

    private static GameObject CreateGround(string name, Vector3 position, Vector2 size, int layer)
    {
        GameObject ground = new GameObject(name);
        ground.transform.position = position;
        ground.layer = layer;

        SpriteRenderer sr = ground.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.4f, 0.3f, 0.2f);
        sr.sortingOrder = GROUND_SORTING_ORDER;
        AssignDefaultSprite(sr, sr.color);

        ground.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D col = ground.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        return ground;
    }

    private static GameObject CreatePlatformObject(string name, Vector3 position, Vector2 size, int layer)
    {
        GameObject platform = new GameObject(name);
        platform.transform.position = position;
        platform.layer = layer;

        SpriteRenderer sr = platform.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.3f, 0.6f, 0.8f);
        sr.sortingOrder = 2;
        AssignDefaultSprite(sr, sr.color);

        platform.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D col = platform.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        return platform;
    }

    private static void AssignDefaultSprite(SpriteRenderer sr, Color color)
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        sr.color = color;
    }

    private static int EnsureLayerExists(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer != -1) return existingLayer;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[TestSceneBuilder] 已创建 Layer: {layerName} (index: {i})");
                return i;
            }
        }

        Debug.LogError("[TestSceneBuilder] 无法创建 Layer：所有自定义 Layer 槽位已满！");
        return 0;
    }

    private static void SetSerializedField(Object target, string fieldName, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[TestSceneBuilder] 找不到字段: {target.GetType().Name}.{fieldName}");
            return;
        }

        switch (prop.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                prop.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.Float:
                prop.floatValue = (float)value;
                break;
            case SerializedPropertyType.Integer:
                prop.intValue = (int)value;
                break;
            case SerializedPropertyType.Boolean:
                prop.boolValue = (bool)value;
                break;
            case SerializedPropertyType.String:
                prop.stringValue = (string)value;
                break;
            case SerializedPropertyType.Vector3:
                prop.vector3Value = (Vector3)value;
                break;
            case SerializedPropertyType.Vector2:
                prop.vector2Value = (Vector2)value;
                break;
            case SerializedPropertyType.LayerMask:
                prop.intValue = (int)(LayerMask)value;
                break;
            case SerializedPropertyType.Enum:
                prop.enumValueIndex = (int)value;
                break;
            default:
                Debug.LogWarning($"[TestSceneBuilder] 不支持的属性类型: {prop.propertyType} ({fieldName})");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
