using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 一键生成测试场景的 Editor 工具 (Session 16 重构)
/// 
/// 使用方式：Unity 菜单栏 → MarioTrickster → Build Test Scene
/// 
/// 重构原则：
///   - 按 Testing_Guide 测试顺序从左到右排列为闯关关卡
///   - 每个测试区域有醒目的场景指示标签（世界空间 TextMesh）
///   - 区域之间有分隔墙/间距，玩家必须逐区通过
///   - 最大限度方便快捷测试
///
/// 关卡布局（从左到右）：
///   Stage 1: Mario 基础移动与跳跃（测试 1）
///   Stage 2: Trickster 基础移动（测试 2）
///   Stage 3: 移动平台跟随（测试 3）
///   Stage 4: 伪装变身系统（测试 4）
///   Stage 5: 道具操控能力（测试 5）
///   Stage 6: 扫描技能（测试 6）
///   Stage 7: 胜负判定与UI（测试 7）
///   Stage 8: 暂停系统（测试 8）
///   Stage 9: 关卡设计系统（测试 9）— 9 种元素分区展示
///   终点: GoalZone
/// </summary>
public class TestSceneBuilder : Editor
{
    private const string GROUND_LAYER_NAME = "Ground";
    private const int GROUND_SORTING_ORDER = -10;

    // 每个测试区域的宽度和间距
    private const float STAGE_WIDTH = 18f;
    private const float STAGE_GAP = 2f;
    private const float TOTAL_STAGE_UNIT = STAGE_WIDTH + STAGE_GAP; // 20

    [MenuItem("MarioTrickster/Build Test Scene", false, 1)]
    public static void BuildTestScene()
    {
        if (!EditorUtility.DisplayDialog(
            "MarioTrickster - 生成闯关测试场景",
            "这将生成按测试顺序排列的闯关式测试关卡。\n\n" +
            "Stage 1: Mario 基础移动\n" +
            "Stage 2: Trickster 基础移动\n" +
            "Stage 3: 移动平台\n" +
            "Stage 4: 伪装变身\n" +
            "Stage 5: 道具操控\n" +
            "Stage 6: 扫描技能\n" +
            "Stage 7: 胜负判定\n" +
            "Stage 8: 暂停系统\n" +
            "Stage 9: 关卡元素（9种）\n" +
            "终点: GoalZone\n\n" +
            "每个区域都有场景指示标签。\n建议在空白场景中执行。继续？",
            "生成", "取消"))
        {
            return;
        }

        int groundLayerIndex = EnsureLayerExists(GROUND_LAYER_NAME);
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // B028: 创建 Player 和 Trickster 专用 Layer，禁用两者之间的物理碰撞
        // 根因: Mario 和 Trickster 都在 Default Layer，物理碰撞导致 Trickster 卡在 Mario 头上无法跳走
        int playerLayerIndex = EnsureLayerExists("Player");
        int tricksterLayerIndex = EnsureLayerExists("Trickster");
        Physics2D.IgnoreLayerCollision(playerLayerIndex, tricksterLayerIndex, true);

        // ═══════════════════════════════════════════════════
        // 全局地面 — 贯穿整个关卡的长地面
        // ═══════════════════════════════════════════════════
        float totalLength = TOTAL_STAGE_UNIT * 12 + 20f; // 12 个区域 + 终点
        GameObject ground = CreateGround("Ground_Main", new Vector3(totalLength / 2f - 10f, -1, 0), new Vector2(totalLength, 1), groundLayerIndex);

        // 左墙壁
        GameObject wallLeft = CreateGround("Wall_Left", new Vector3(-1.5f, 5, 0), new Vector2(1, 14), groundLayerIndex);
        // 右墙壁
        GameObject wallRight = CreateGround("Wall_Right", new Vector3(totalLength - 8f, 5, 0), new Vector2(1, 14), groundLayerIndex);

        // 死亡区域
        GameObject killZone = new GameObject("KillZone");
        killZone.transform.position = new Vector3(totalLength / 2f, -10, 0);
        BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
        killCol.size = new Vector2(totalLength + 20, 2);
        killCol.isTrigger = true;
        killZone.AddComponent<KillZone>();

        // ═══════════════════════════════════════════════════
        // Mario (起始位置在 Stage 1)
        // ═══════════════════════════════════════════════════
        float marioStartX = 2f;
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.layer = playerLayerIndex; // B028: 使用专用 Player Layer
        mario.transform.position = new Vector3(marioStartX, 1, 0);

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
        // Trickster (起始位置在 Stage 2)
        // ═══════════════════════════════════════════════════
        float tricksterStartX = StageStartX(1) + 5f;
        GameObject trickster = new GameObject("Trickster");
        trickster.layer = tricksterLayerIndex; // B028: 使用专用 Trickster Layer
        trickster.transform.position = new Vector3(tricksterStartX, 1, 0);

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
        // 管理器
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
        marioSpawn.transform.position = new Vector3(marioStartX, 1, 0);
        marioSpawn.transform.parent = managers.transform;

        GameObject tricksterSpawn = new GameObject("TricksterSpawnPoint");
        tricksterSpawn.transform.position = new Vector3(tricksterStartX, 1, 0);
        tricksterSpawn.transform.parent = managers.transform;

        SetSerializedField(gameManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(gameManager, "tricksterSpawnPoint", tricksterSpawn.transform);
        SetSerializedField(levelManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(levelManager, "tricksterSpawnPoint", tricksterSpawn.transform);
        // B027 根本修复: LevelManager.Start() 会调用 CameraController.SetBounds() 覆盖相机边界，
        // 必须同步设置 LevelManager 的边界字段，否则运行时会用默认值 maxX=50 覆盖正确值
        SetSerializedField(levelManager, "levelMinX", -5f);
        SetSerializedField(levelManager, "levelMaxX", totalLength + 10f);
        SetSerializedField(levelManager, "levelMinY", -10f);
        SetSerializedField(levelManager, "levelMaxY", 25f);

        // 相机
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController[] existingCamCtrls = mainCam.GetComponents<CameraController>();
            for (int i = 0; i < existingCamCtrls.Length; i++)
                DestroyImmediate(existingCamCtrls[i]);

            CameraController camCtrl = mainCam.gameObject.AddComponent<CameraController>();
            SetSerializedField(camCtrl, "target", mario.transform);
            // B027: 设置相机边界覆盖整个闯关场景
            // 注意: LevelManager.Start() 会再次调用 SetBounds 覆盖这些值，
            // 因此上方已同步设置 LevelManager 的边界字段
            SetSerializedField(camCtrl, "useBounds", true);
            SetSerializedField(camCtrl, "minX", -5f);
            SetSerializedField(camCtrl, "maxX", totalLength + 10f);
            SetSerializedField(camCtrl, "minY", -5f);
            SetSerializedField(camCtrl, "maxY", 25f);
            mainCam.transform.position = new Vector3(0, 2, -10);
            mainCam.orthographicSize = 7;
        }

        // GameUI
        GameUI[] existingGameUIs = FindObjectsOfType<GameUI>();
        foreach (GameUI existingUI in existingGameUIs)
            DestroyImmediate(existingUI.gameObject);

        GameObject uiObject = new GameObject("GameUI");
        uiObject.transform.parent = managers.transform;
        uiObject.AddComponent<GameUI>();

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 1: Mario 基础移动与跳跃 (测试 1)
        // ═══════════════════════════════════════════════════════════
        float s1 = StageStartX(0);
        GameObject stage1 = new GameObject("--- Stage 1: Mario Movement ---");

        CreateStageSign(s1 + STAGE_WIDTH / 2f, "STAGE 1: MARIO MOVEMENT",
            "WASD Move | Space Jump\nShort/Long press = Low/High jump\nTest air control (A/D in air)",
            stage1.transform);

        // 高低平台供跳跃测试
        GameObject s1_plat1 = CreateGround("S1_Platform_Low", new Vector3(s1 + 6, 1.5f, 0), new Vector2(3, 0.5f), groundLayerIndex);
        s1_plat1.transform.parent = stage1.transform;
        GameObject s1_plat2 = CreateGround("S1_Platform_Mid", new Vector3(s1 + 10, 3f, 0), new Vector2(3, 0.5f), groundLayerIndex);
        s1_plat2.transform.parent = stage1.transform;
        GameObject s1_plat3 = CreateGround("S1_Platform_High", new Vector3(s1 + 14, 5f, 0), new Vector2(3, 0.5f), groundLayerIndex);
        s1_plat3.transform.parent = stage1.transform;

        // 金币引导路径
        for (int i = 0; i < 4; i++)
        {
            GameObject coin = CreateCoin($"S1_Coin_{i}", new Vector3(s1 + 4 + i * 3, 1.5f + i * 1f, 0));
            coin.transform.parent = stage1.transform;
        }

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 2: Trickster 基础移动 (测试 2)
        // ═══════════════════════════════════════════════════════════
        float s2 = StageStartX(1);
        GameObject stage2 = new GameObject("--- Stage 2: Trickster Movement ---");

        CreateStageSign(s2 + STAGE_WIDTH / 2f, "STAGE 2: TRICKSTER MOVEMENT",
            "Arrow Keys Move | Up/RCtrl Jump\nTrickster (blue) starts here\nSlightly slower than Mario",
            stage2.transform);

        GameObject s2_plat1 = CreateGround("S2_Platform_1", new Vector3(s2 + 6, 2f, 0), new Vector2(3, 0.5f), groundLayerIndex);
        s2_plat1.transform.parent = stage2.transform;
        GameObject s2_plat2 = CreateGround("S2_Platform_2", new Vector3(s2 + 12, 3.5f, 0), new Vector2(3, 0.5f), groundLayerIndex);
        s2_plat2.transform.parent = stage2.transform;

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 3: 移动平台跟随 (测试 3)
        // ═══════════════════════════════════════════════════════════
        float s3 = StageStartX(2);
        GameObject stage3 = new GameObject("--- Stage 3: Moving Platform ---");

        CreateStageSign(s3 + STAGE_WIDTH / 2f, "STAGE 3: MOVING PLATFORM",
            "Stand on platform -> ride smoothly\nMove on platform -> no sliding\nJump off -> normal gravity",
            stage3.transform);

        GameObject movingPlatform = CreatePlatformObject("MovingPlatform", new Vector3(s3 + 4, 1.5f, 0), new Vector2(3, 0.4f), groundLayerIndex);
        MovingPlatform mp = movingPlatform.AddComponent<MovingPlatform>();
        SetSerializedField(mp, "pointB", new Vector3(10, 0, 0));
        SetSerializedField(mp, "moveSpeed", 2f);
        movingPlatform.transform.parent = stage3.transform;

        CreateSubLabel(s3 + 4, 0.5f, "<- Moving Platform ->", stage3.transform);

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 4: 伪装变身系统 (测试 4)
        // ═══════════════════════════════════════════════════════════
        float s4 = StageStartX(3);
        GameObject stage4 = new GameObject("--- Stage 4: Disguise System ---");

        CreateStageSign(s4 + STAGE_WIDTH / 2f, "STAGE 4: DISGUISE SYSTEM",
            "P = Toggle Disguise | O/I = Switch Form\nDisguise -> move slow (15%)\nStay still 1.5s -> Blend In\nCooldown after undisguise (2s)\n[F9] Toggle No-Cooldown Mode",
            stage4.transform);

        // 放置一些场景物体供伪装参考
        GameObject s4_brick1 = CreateSceneProp("S4_Brick_1", new Vector3(s4 + 5, 0.5f, 0), new Color(0.6f, 0.4f, 0.2f), new Vector3(1, 1, 1));
        s4_brick1.transform.parent = stage4.transform;
        GameObject s4_brick2 = CreateSceneProp("S4_Brick_2", new Vector3(s4 + 7, 0.5f, 0), new Color(0.6f, 0.4f, 0.2f), new Vector3(1, 1, 1));
        s4_brick2.transform.parent = stage4.transform;
        GameObject s4_pipe = CreateSceneProp("S4_Pipe", new Vector3(s4 + 10, 1f, 0), new Color(0.2f, 0.7f, 0.2f), new Vector3(1.5f, 2f, 1));
        s4_pipe.transform.parent = stage4.transform;

        CreateSubLabel(s4 + 5, -0.3f, "Hide among these props", stage4.transform);

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 5: 道具操控能力 (测试 5)
        // ═══════════════════════════════════════════════════════════
        float s5 = StageStartX(4);
        GameObject stage5 = new GameObject("--- Stage 5: Prop Control ---");

        CreateStageSign(s5 + STAGE_WIDTH / 2f, "STAGE 5: PROP CONTROL",
            "Trickster: Disguise+BlendIn -> L to control\nTelegraph(0.8s) -> Active -> Cooldown\nEnergy cost: Disguise=25, Control=20\n[F9] No-Cooldown for rapid testing\n[S20] Red/Gray visual links + Arrow-key magnetic target switch",
            stage5.transform);

        // 可操控陷阱
        GameObject hazard = new GameObject("ControllableHazard");
        hazard.transform.position = new Vector3(s5 + 5, 0, 0);
        hazard.layer = groundLayerIndex;
        SpriteRenderer hazardSR = hazard.AddComponent<SpriteRenderer>();
        hazardSR.color = new Color(1f, 0.5f, 0f);
        hazardSR.sortingOrder = 5;
        AssignDefaultSprite(hazardSR, new Color(1f, 0.5f, 0f));
        BoxCollider2D hazardCol = hazard.AddComponent<BoxCollider2D>();
        hazardCol.size = new Vector2(1f, 0.5f);
        hazardCol.isTrigger = true;
        hazard.AddComponent<ControllableHazard>();
        hazard.transform.parent = stage5.transform;
        CreateSubLabel(s5 + 5, -0.5f, "Controllable Hazard (L)", stage5.transform);

        // 可操控方块
        GameObject block = new GameObject("ControllableBlock");
        block.transform.position = new Vector3(s5 + 9, 2.5f, 0);
        block.layer = groundLayerIndex;
        SpriteRenderer blockSR = block.AddComponent<SpriteRenderer>();
        blockSR.color = new Color(0.8f, 0.6f, 0.2f);
        blockSR.sortingOrder = 5;
        AssignDefaultSprite(blockSR, new Color(0.8f, 0.6f, 0.2f));
        BoxCollider2D blockCol = block.AddComponent<BoxCollider2D>();
        blockCol.size = new Vector2(1f, 1f);
        block.AddComponent<ControllableBlock>();
        block.transform.parent = stage5.transform;
        CreateSubLabel(s5 + 9, 1.5f, "Controllable Block (L)", stage5.transform);

        // 可操控平台
        GameObject controllablePlatform = CreatePlatformObject("ControllablePlatform", new Vector3(s5 + 13, 2, 0), new Vector2(3, 0.4f), groundLayerIndex);
        ControllablePlatform cp = controllablePlatform.AddComponent<ControllablePlatform>();
        SetSerializedField(cp, "pointB", new Vector3(0, 4, 0));
        SetSerializedField(cp, "normalMoveSpeed", 1.5f);
        controllablePlatform.transform.parent = stage5.transform;
        CreateSubLabel(s5 + 13, 1f, "Controllable Platform (L)", stage5.transform);

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 6: 扫描技能 (测试 6)
        // ═══════════════════════════════════════════════════════════
        float s6 = StageStartX(5);
        GameObject stage6 = new GameObject("--- Stage 6: Scan Ability ---");

        CreateStageSign(s6 + STAGE_WIDTH / 2f, "STAGE 6: SCAN ABILITY",
            "Mario: Q = Scan (8s cooldown)\nRed pulse = Trickster found\nBlue pulse = Not found\nTrickster must be disguised & in range",
            stage6.transform);

        // 放置隐藏物供 Trickster 伪装
        for (int i = 0; i < 3; i++)
        {
            GameObject prop = CreateSceneProp($"S6_HideSpot_{i}", new Vector3(s6 + 5 + i * 4, 0.5f, 0), new Color(0.5f, 0.4f, 0.3f), Vector3.one);
            prop.transform.parent = stage6.transform;
        }
        CreateSubLabel(s6 + 9, -0.3f, "Trickster hides here -> Mario scans (Q)", stage6.transform);

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 7: 胜负判定与UI (测试 7)
        // ═══════════════════════════════════════════════════════════
        float s7 = StageStartX(6);
        GameObject stage7 = new GameObject("--- Stage 7: Win/Lose & UI ---");

        CreateStageSign(s7 + STAGE_WIDTH / 2f, "STAGE 7: WIN/LOSE & UI",
            "Mario reach GoalZone -> Mario Wins\nMario fall to death -> Trickster Wins\nR = Restart | N = Next Round | F5 = Quick Restart\nCheck: Timer, HP, Round display",
            stage7.transform);

        // 简单敌人（用于测试 Mario 死亡）
        GameObject enemy = new GameObject("SimpleEnemy");
        enemy.transform.position = new Vector3(s7 + 6, 0.5f, 0);
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
        enemy.transform.parent = stage7.transform;
        CreateSubLabel(s7 + 6, -0.5f, "Enemy (touch = damage)", stage7.transform);

        // 可破坏方块
        GameObject breakable = new GameObject("BreakableBlock");
        breakable.transform.position = new Vector3(s7 + 10, 3, 0);
        breakable.layer = groundLayerIndex;
        SpriteRenderer breakSR = breakable.AddComponent<SpriteRenderer>();
        breakSR.color = new Color(0.9f, 0.7f, 0.3f);
        breakSR.sortingOrder = 5;
        AssignDefaultSprite(breakSR, new Color(0.9f, 0.7f, 0.3f));
        BoxCollider2D breakCol = breakable.AddComponent<BoxCollider2D>();
        breakCol.size = new Vector2(1f, 1f);
        breakable.AddComponent<Breakable>();
        breakable.transform.parent = stage7.transform;
        CreateSubLabel(s7 + 10, 2f, "Breakable Block", stage7.transform);

        // 金币
        for (int i = 0; i < 3; i++)
        {
            GameObject coin = CreateCoin($"S7_Coin_{i}", new Vector3(s7 + 12 + i * 2, 1.5f, 0));
            coin.transform.parent = stage7.transform;
        }

        // ═══════════════════════════════════════════════════════════
        // ██ STAGE 8: 暂停系统 (测试 8)
        // ═══════════════════════════════════════════════════════════
        float s8 = StageStartX(7);
        GameObject stage8 = new GameObject("--- Stage 8: Pause System ---");

        CreateStageSign(s8 + STAGE_WIDTH / 2f, "STAGE 8: PAUSE SYSTEM",
            "ESC = Pause/Resume\nCheck: Semi-transparent overlay\nCheck: 'PAUSED' text\nAll movement should freeze",
            stage8.transform);

        // ═══════════════════════════════════════════════════════════════
        // ██ STAGE 9: 关卡设计系统 (测试 9) — 分为 9 个子区域
        // ═══════════════════════════════════════════════════════════════
        // Stage 9 需要更大空间，使用 3 个 STAGE_UNIT 的宽度
        float s9 = StageStartX(8);
        float s9SubWidth = 8f; // 每个子区域宽度
        GameObject stage9 = new GameObject("--- Stage 9: Level Elements ---");

        CreateStageSign(s9 + 4f, "STAGE 9: LEVEL ELEMENTS",
            "Test all 9 level elements below\nMario: walk through each sub-area\nTrickster: Disguise+BlendIn -> L to control each\n[F9] No-Cooldown recommended",
            stage9.transform, 24);

        // --- 9A: SpikeTrap (地刺) ---
        float s9a = s9 + 2f;
        CreateSubSign(s9a + s9SubWidth / 2f, "9A: SPIKE TRAP",
            "Mario: touch spikes = damage + safe knockback\nTrickster(L): force extend / change freq\n[S18] Knockback: backward from move dir",
            stage9.transform);

        // 周期地刺
        GameObject spikeParent = new GameObject("SpikeTrap_Periodic");
        spikeParent.transform.position = new Vector3(s9a + 2, -0.3f, 0);
        SpriteRenderer spikeSR = spikeParent.AddComponent<SpriteRenderer>();
        spikeSR.color = new Color(0.7f, 0.1f, 0.1f);
        spikeSR.sortingOrder = 5;
        AssignDefaultSprite(spikeSR, new Color(0.7f, 0.1f, 0.1f));
        spikeParent.transform.localScale = new Vector3(1.5f, 0.4f, 1f);
        BoxCollider2D spikeCol = spikeParent.AddComponent<BoxCollider2D>();
        spikeCol.size = Vector2.one;
        spikeCol.isTrigger = true;
        spikeParent.AddComponent<SpikeTrap>();
        spikeParent.transform.parent = stage9.transform;
        CreateSubLabel(s9a + 2, -1f, "Periodic Spike", stage9.transform);

        // 静态地刺
        GameObject spikeStatic = new GameObject("SpikeTrap_Static");
        spikeStatic.transform.position = new Vector3(s9a + 5, -0.3f, 0);
        SpriteRenderer spikeStaticSR = spikeStatic.AddComponent<SpriteRenderer>();
        spikeStaticSR.color = new Color(0.9f, 0.1f, 0.1f);
        spikeStaticSR.sortingOrder = 5;
        AssignDefaultSprite(spikeStaticSR, new Color(0.9f, 0.1f, 0.1f));
        spikeStatic.transform.localScale = new Vector3(2f, 0.3f, 1f);
        BoxCollider2D spikeStaticCol = spikeStatic.AddComponent<BoxCollider2D>();
        spikeStaticCol.size = Vector2.one;
        spikeStaticCol.isTrigger = true;
        SpikeTrap spikeStaticTrap = spikeStatic.AddComponent<SpikeTrap>();
        SetSerializedField(spikeStaticTrap, "mode", 0);
        spikeStatic.transform.parent = stage9.transform;
        CreateSubLabel(s9a + 5, -1f, "Static Spike", stage9.transform);

        // --- 9B: PendulumTrap (摆锤) ---
        float s9b = s9a + s9SubWidth;
        CreateSubSign(s9b + s9SubWidth / 2f, "9B: PENDULUM TRAP",
            "Mario: touch pendulum = damage + safe knockback\nTrickster(L): increase swing + speed\n[S18] Disguised Trickster can control",
            stage9.transform);

        GameObject pendulum = new GameObject("PendulumTrap");
        pendulum.transform.position = new Vector3(s9b + 4, 6, 0);
        SpriteRenderer pendulumSR = pendulum.AddComponent<SpriteRenderer>();
        pendulumSR.color = new Color(0.6f, 0.4f, 0.2f);
        pendulumSR.sortingOrder = 3;
        AssignDefaultSprite(pendulumSR, new Color(0.6f, 0.4f, 0.2f));
        pendulum.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        pendulum.AddComponent<PendulumTrap>();
        pendulum.transform.parent = stage9.transform;
        CreateSubLabel(s9b + 4, -0.3f, "Pendulum swings above", stage9.transform);

        // --- 9C: FireTrap (火焰) ---
        float s9c = s9b + s9SubWidth;
        CreateSubSign(s9c + s9SubWidth / 2f, "9C: FIRE TRAP",
            "Mario: touch fire = damage + safe knockback\nTrickster(L): force fire / speed up\n[S18] No more fly-away on hit",
            stage9.transform);

        GameObject fireTrap = new GameObject("FireTrap");
        fireTrap.transform.position = new Vector3(s9c + 4, -0.3f, 0);
        SpriteRenderer fireSR = fireTrap.AddComponent<SpriteRenderer>();
        fireSR.color = new Color(1f, 0.4f, 0f);
        fireSR.sortingOrder = 5;
        AssignDefaultSprite(fireSR, new Color(1f, 0.4f, 0f));
        fireTrap.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        BoxCollider2D fireCol = fireTrap.AddComponent<BoxCollider2D>();
        fireCol.size = Vector2.one;
        fireTrap.AddComponent<FireTrap>();
        fireTrap.transform.parent = stage9.transform;
        CreateSubLabel(s9c + 4, -1f, "Fire Trap", stage9.transform);

        // --- 9D: BouncingEnemy (弹跳怪) ---
        float s9d = s9c + s9SubWidth;
        CreateSubSign(s9d + s9SubWidth / 2f, "9D: BOUNCING ENEMY",
            "Mario side-touch = damage + knockback\nMario stomp from above = kill enemy\nTrickster(L): increase bounce + speed",
            stage9.transform);

        GameObject bouncingEnemy = new GameObject("BouncingEnemy");
        bouncingEnemy.transform.position = new Vector3(s9d + 4, 0.5f, 0);
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
        bouncingEnemy.transform.parent = stage9.transform;
        CreateSubLabel(s9d + 4, -0.5f, "Bouncing Enemy (stomp to kill)", stage9.transform);

        // --- 9E: BouncyPlatform (弹跳平台) ---
        float s9e = s9d + s9SubWidth;
        CreateSubSign(s9e + s9SubWidth / 2f, "9E: BOUNCY PLATFORM",
            "Mario: land on any side = bounce in surface normal direction\nBounce force adjustable (bounceForce + bounceForceMultiplier)\nComedy delay(0.25s): freeze + squash animation\nTrickster(L): override direction + increase force during freeze\n[S19] Contact-normal based directional bounce\n[S20] Normal correction for corner collisions\n[S22] Two-phase bounce: PrepareBounce(freeze) → ExecuteBounce(launch)\n     Momentum preservation: airFriction + bounceAirAcceleration\n     Auto-release on landing or wall hit",
            stage9.transform);

        GameObject bouncyPlatform = new GameObject("BouncyPlatform");
        bouncyPlatform.transform.position = new Vector3(s9e + 4, -0.3f, 0);
        bouncyPlatform.layer = groundLayerIndex;
        SpriteRenderer bouncyPlatSR = bouncyPlatform.AddComponent<SpriteRenderer>();
        bouncyPlatSR.color = new Color(0.2f, 0.9f, 0.3f);
        bouncyPlatSR.sortingOrder = 5;
        AssignDefaultSprite(bouncyPlatSR, new Color(0.2f, 0.9f, 0.3f));
        bouncyPlatform.transform.localScale = new Vector3(2f, 0.4f, 1f);
        BoxCollider2D bouncyCol = bouncyPlatform.AddComponent<BoxCollider2D>();
        bouncyCol.size = Vector2.one;
        bouncyPlatform.AddComponent<BouncyPlatform>();
        bouncyPlatform.transform.parent = stage9.transform;
        CreateSubLabel(s9e + 4, -1f, "Bouncy Platform (S22: Two-phase bounce = PrepareBounce + ExecuteBounce + momentum preservation)", stage9.transform);

        // --- 9F: OneWayPlatform (单向平台) ---
        float s9f = s9e + s9SubWidth;
        CreateSubSign(s9f + s9SubWidth / 2f, "9F: ONE-WAY PLATFORM",
            "Jump from below = pass through\nStand on top + press S+Space = drop through\n[S19] S+Jump combo (industry standard, prevents accidental drop)",
            stage9.transform);

        GameObject oneWayPlatform = new GameObject("OneWayPlatform");
        oneWayPlatform.transform.position = new Vector3(s9f + 4, 3, 0);
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
        oneWayPlatform.transform.parent = stage9.transform;
        CreateSubLabel(s9f + 4, 1.8f, "One-Way Platform (jump up / S+Space to drop)", stage9.transform);

        // --- 9G: CollapsingPlatform (崩塌平台) ---
        float s9g = s9f + s9SubWidth;
        CreateSubSign(s9g + s9SubWidth / 2f, "9G: COLLAPSING PLATFORM",
            "Mario/Trickster: stand on it -> shake -> collapse\nRespawns at current position (not initial)\nTrickster(L): instant collapse\n[S18] Both players trigger + position fix",
            stage9.transform);

        GameObject collapsingPlatform = new GameObject("CollapsingPlatform");
        collapsingPlatform.transform.position = new Vector3(s9g + 4, 4, 0);
        collapsingPlatform.layer = groundLayerIndex;
        SpriteRenderer collapseSR = collapsingPlatform.AddComponent<SpriteRenderer>();
        collapseSR.color = new Color(0.8f, 0.6f, 0.2f);
        collapseSR.sortingOrder = 2;
        AssignDefaultSprite(collapseSR, new Color(0.8f, 0.6f, 0.2f));
        collapsingPlatform.transform.localScale = new Vector3(3f, 0.4f, 1f);
        BoxCollider2D collapseCol = collapsingPlatform.AddComponent<BoxCollider2D>();
        collapseCol.size = Vector2.one;
        collapsingPlatform.AddComponent<CollapsingPlatform>();
        collapsingPlatform.transform.parent = stage9.transform;

        // 跳上崩塌平台的辅助台阶
        GameObject s9g_step = CreateGround("S9G_Step", new Vector3(s9g + 2, 2, 0), new Vector2(2, 0.5f), groundLayerIndex);
        s9g_step.transform.parent = stage9.transform;
        CreateSubLabel(s9g + 2, 1f, "Step -> jump to collapsing platform above", stage9.transform);

        // --- 9H: HiddenPassage (隐藏通道) ---
        float s9h = s9g + s9SubWidth;
        CreateSubSign(s9h + s9SubWidth / 2f, "9H: HIDDEN PASSAGE",
            "Mario: walk to entrance + press S = teleport to exit\nAt exit: press S = teleport back to entrance\nBidirectional with cooldown + return trigger zone\nTrickster(L): block the passage\n[S19] Bidirectional passage + TeleportMode state machine",
            stage9.transform);

        // 地下出口区域
        GameObject undergroundGround = CreateGround("S9H_Underground", new Vector3(s9h + 6, -3, 0), new Vector2(6, 0.5f), groundLayerIndex);
        undergroundGround.transform.parent = stage9.transform;

        GameObject passageExit = new GameObject("HiddenPassage_Exit");
        passageExit.transform.position = new Vector3(s9h + 6, -2.5f, 0);
        SpriteRenderer exitSR = passageExit.AddComponent<SpriteRenderer>();
        exitSR.color = new Color(0f, 0.8f, 0.8f, 0.5f);
        exitSR.sortingOrder = 4;
        AssignDefaultSprite(exitSR, new Color(0f, 0.8f, 0.8f, 0.5f));
        passageExit.transform.localScale = new Vector3(1f, 1.5f, 1f);
        passageExit.transform.parent = stage9.transform;
        CreateSubLabel(s9h + 6, -4f, "Exit (underground)", stage9.transform);

        GameObject passageEntrance = new GameObject("HiddenPassage_Entrance");
        passageEntrance.transform.position = new Vector3(s9h + 2, 0, 0);
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
        passageEntrance.transform.parent = stage9.transform;
        CreateSubLabel(s9h + 2, -0.5f, "Entrance (press S to go / S to return)", stage9.transform);

        // --- 9I: FakeWall (伪装墙) ---
        float s9i = s9h + s9SubWidth;
        CreateSubSign(s9i + s9SubWidth / 2f, "9I: FAKE WALL",
            "Mario: walk into wall = pass through (semi-transparent)\nTrickster(L): wall becomes solid\n[S18] Solid duration = activeDuration in Inspector",
            stage9.transform);

        GameObject fakeWall = new GameObject("FakeWall");
        fakeWall.transform.position = new Vector3(s9i + 4, 1, 0);
        fakeWall.layer = groundLayerIndex;
        SpriteRenderer fakeWallSR = fakeWall.AddComponent<SpriteRenderer>();
        fakeWallSR.color = new Color(0.4f, 0.3f, 0.2f);
        fakeWallSR.sortingOrder = 1;
        AssignDefaultSprite(fakeWallSR, new Color(0.4f, 0.3f, 0.2f));
        fakeWall.transform.localScale = new Vector3(1f, 3f, 1f);
        BoxCollider2D fakeWallCol = fakeWall.AddComponent<BoxCollider2D>();
        fakeWallCol.size = Vector2.one;
        fakeWallCol.isTrigger = true;
        fakeWall.AddComponent<FakeWall>();
        fakeWall.transform.parent = stage9.transform;
        CreateSubLabel(s9i + 4, -0.5f, "Fake Wall (walk through it)", stage9.transform);

        // 隐藏空间内的金币（奖励）
        for (int i = 0; i < 2; i++)
        {
            GameObject coin = CreateCoin($"S9I_Coin_{i}", new Vector3(s9i + 5.5f + i, 1.5f, 0));
            coin.transform.parent = stage9.transform;
        }

        // ═══════════════════════════════════════════════════════════
        // ██ 终点: GoalZone
        // ═══════════════════════════════════════════════════════════
        float goalX = s9i + s9SubWidth + 2f;
        GameObject goalParent = new GameObject("--- Goal ---");

        CreateStageSign(goalX, "GOAL!",
            "Mario: reach here to WIN!\nCongratulations on completing all tests!",
            goalParent.transform, 28);

        GameObject goal = new GameObject("GoalZone");
        goal.transform.position = new Vector3(goalX, 1, 0);
        SpriteRenderer goalSR = goal.AddComponent<SpriteRenderer>();
        goalSR.color = new Color(0f, 1f, 0f, 0.5f);
        goalSR.sortingOrder = 3;
        AssignDefaultSprite(goalSR, new Color(0f, 1f, 0f, 0.5f));
        BoxCollider2D goalCol = goal.AddComponent<BoxCollider2D>();
        goalCol.size = new Vector2(1f, 3f);
        goalCol.isTrigger = true;
        goal.AddComponent<GoalZone>();
        goal.transform.parent = goalParent.transform;

        // ═══════════════════════════════════════════════════════════
        // 场景整理
        // ═══════════════════════════════════════════════════════════
        GameObject levelGeometry = new GameObject("--- Level Geometry ---");
        ground.transform.parent = levelGeometry.transform;
        wallLeft.transform.parent = levelGeometry.transform;
        wallRight.transform.parent = levelGeometry.transform;
        killZone.transform.parent = levelGeometry.transform;

        // 标记场景为已修改
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[TestSceneBuilder] ✅ 闯关测试场景生成完成！\n" +
                  "  Stage 1: Mario 基础移动与跳跃\n" +
                  "  Stage 2: Trickster 基础移动\n" +
                  "  Stage 3: 移动平台跟随\n" +
                  "  Stage 4: 伪装变身系统\n" +
                  "  Stage 5: 道具操控能力\n" +
                  "  Stage 6: 扫描技能\n" +
                  "  Stage 7: 胜负判定与UI\n" +
                  "  Stage 8: 暂停系统\n" +
                  "  Stage 9: 关卡元素 (9A-9I)\n" +
                  "  终点: GoalZone\n\n" +
                  "  每个区域都有场景指示标签！\n" +
                  "  [F9] 切换无冷却模式方便测试\n" +
                  "  ⚠️ 请保存场景：Ctrl+S");

        EditorUtility.DisplayDialog(
            "闯关测试场景生成完成",
            "按测试顺序排列的闯关关卡已生成！\n\n" +
            "每个 Stage 都有醒目的标题和操作说明。\n\n" +
            "操作指南：\n" +
            "  P1 (Mario): WASD + Space + Q\n" +
            "  P2 (Trickster): 方向键 + P/O/I + L\n" +
            "  F9: 切换无冷却模式\n" +
            "  F5: 快速重启\n" +
            "  ESC: 暂停\n\n" +
            "请 Ctrl+S 保存后按 Play 开始测试！",
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

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController[] camCtrls = mainCam.GetComponents<CameraController>();
            for (int i = 0; i < camCtrls.Length; i++)
                DestroyImmediate(camCtrls[i]);
        }

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

    /// <summary>计算第 n 个 Stage 的起始 X 坐标 (0-based)</summary>
    private static float StageStartX(int stageIndex)
    {
        return stageIndex * TOTAL_STAGE_UNIT;
    }

    /// <summary>创建大型 Stage 标题标签（世界空间 TextMesh）</summary>
    private static void CreateStageSign(float centerX, string title, string instructions, Transform parent, int titleFontSize = 20)
    {
        // 标题
        GameObject titleObj = new GameObject($"Sign_{title.Replace(" ", "_")}");
        titleObj.transform.position = new Vector3(centerX, 8.5f, 0);
        TextMesh titleTM = titleObj.AddComponent<TextMesh>();
        titleTM.text = title;
        titleTM.fontSize = titleFontSize;
        titleTM.characterSize = 0.2f;
        titleTM.anchor = TextAnchor.MiddleCenter;
        titleTM.alignment = TextAlignment.Center;
        titleTM.color = new Color(1f, 0.9f, 0.2f); // 金黄色标题
        titleTM.fontStyle = FontStyle.Bold;
        titleObj.transform.parent = parent;

        // 说明文字
        GameObject instrObj = new GameObject($"Instr_{title.Replace(" ", "_")}");
        instrObj.transform.position = new Vector3(centerX, 7.2f, 0);
        TextMesh instrTM = instrObj.AddComponent<TextMesh>();
        instrTM.text = instructions;
        instrTM.fontSize = 12;
        instrTM.characterSize = 0.15f;
        instrTM.anchor = TextAnchor.UpperCenter;
        instrTM.alignment = TextAlignment.Center;
        instrTM.color = new Color(0.9f, 0.9f, 0.9f, 0.85f); // 浅白色说明
        instrObj.transform.parent = parent;

        // 背景板（半透明深色）
        GameObject bgObj = new GameObject($"SignBG_{title.Replace(" ", "_")}");
        bgObj.transform.position = new Vector3(centerX, 7.5f, 0.1f); // 稍微靠后
        SpriteRenderer bgSR = bgObj.AddComponent<SpriteRenderer>();
        bgSR.color = new Color(0.1f, 0.1f, 0.2f, 0.6f);
        bgSR.sortingOrder = -5;
        AssignDefaultSprite(bgSR, new Color(0.1f, 0.1f, 0.2f, 0.6f));
        bgObj.transform.localScale = new Vector3(STAGE_WIDTH - 2, 4f, 1f);
        bgObj.transform.parent = parent;
    }

    /// <summary>创建子区域标题标签（用于 Stage 9 的子区域）</summary>
    private static void CreateSubSign(float centerX, string title, string instructions, Transform parent)
    {
        GameObject titleObj = new GameObject($"SubSign_{title.Replace(" ", "_")}");
        titleObj.transform.position = new Vector3(centerX, 6f, 0);
        TextMesh titleTM = titleObj.AddComponent<TextMesh>();
        titleTM.text = title;
        titleTM.fontSize = 16;
        titleTM.characterSize = 0.15f;
        titleTM.anchor = TextAnchor.MiddleCenter;
        titleTM.alignment = TextAlignment.Center;
        titleTM.color = new Color(0.4f, 0.9f, 1f); // 青色子标题
        titleTM.fontStyle = FontStyle.Bold;
        titleObj.transform.parent = parent;

        GameObject instrObj = new GameObject($"SubInstr_{title.Replace(" ", "_")}");
        instrObj.transform.position = new Vector3(centerX, 5.2f, 0);
        TextMesh instrTM = instrObj.AddComponent<TextMesh>();
        instrTM.text = instructions;
        instrTM.fontSize = 10;
        instrTM.characterSize = 0.12f;
        instrTM.anchor = TextAnchor.UpperCenter;
        instrTM.alignment = TextAlignment.Center;
        instrTM.color = new Color(0.8f, 0.8f, 0.8f, 0.75f);
        instrObj.transform.parent = parent;
    }

    /// <summary>创建小型标注标签（元素旁边的说明）</summary>
    private static void CreateSubLabel(float x, float y, string text, Transform parent)
    {
        GameObject labelObj = new GameObject($"Label_{text.Substring(0, Mathf.Min(text.Length, 20))}");
        labelObj.transform.position = new Vector3(x, y, 0);
        TextMesh tm = labelObj.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 10;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
        labelObj.transform.parent = parent;
    }

    /// <summary>创建场景装饰道具（供伪装参考）</summary>
    private static GameObject CreateSceneProp(string name, Vector3 position, Color color, Vector3 scale)
    {
        GameObject prop = new GameObject(name);
        prop.transform.position = position;
        SpriteRenderer sr = prop.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingOrder = 1;
        AssignDefaultSprite(sr, color);
        prop.transform.localScale = scale;
        BoxCollider2D col = prop.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        return prop;
    }

    /// <summary>创建金币</summary>
    private static GameObject CreateCoin(string name, Vector3 position)
    {
        GameObject coin = new GameObject(name);
        coin.transform.position = position;
        SpriteRenderer coinSR = coin.AddComponent<SpriteRenderer>();
        coinSR.color = new Color(1f, 0.85f, 0f);
        coinSR.sortingOrder = 6;
        AssignDefaultSprite(coinSR, new Color(1f, 0.85f, 0f));
        coin.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        BoxCollider2D coinCol = coin.AddComponent<BoxCollider2D>();
        coinCol.isTrigger = true;
        coin.AddComponent<Collectible>();
        return coin;
    }

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
