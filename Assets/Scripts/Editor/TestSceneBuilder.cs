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
///   - 所有 Inspector 引用自动连线
///   - Ground Layer 自动创建和分配
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
            "可操控道具、终点、死亡区域、敌人、收集物等。\n\n" +
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
        GameObject ground = CreateGround("Ground_Main", new Vector3(0, -1, 0), new Vector2(40, 1), groundLayerIndex);
        
        // 额外平台（高处）
        GameObject platform1 = CreateGround("Ground_Platform_Left", new Vector3(-6, 2, 0), new Vector2(4, 0.5f), groundLayerIndex);
        GameObject platform2 = CreateGround("Ground_Platform_Right", new Vector3(6, 3, 0), new Vector2(4, 0.5f), groundLayerIndex);
        GameObject platform3 = CreateGround("Ground_Platform_High", new Vector3(0, 5, 0), new Vector2(3, 0.5f), groundLayerIndex);

        // 左墙壁（防止角色掉出关卡左侧）
        GameObject wallLeft = CreateGround("Wall_Left", new Vector3(-20.5f, 5, 0), new Vector2(1, 14), groundLayerIndex);
        // 右墙壁
        GameObject wallRight = CreateGround("Wall_Right", new Vector3(20.5f, 5, 0), new Vector2(1, 14), groundLayerIndex);

        // ═══════════════════════════════════════════════════
        // 2. Mario
        // ═══════════════════════════════════════════════════
        GameObject mario = new GameObject("Mario");
        mario.tag = "Player";
        mario.layer = LayerMask.NameToLayer("Default");
        mario.transform.position = new Vector3(-8, 1, 0);

        SpriteRenderer marioSR = mario.AddComponent<SpriteRenderer>();
        marioSR.color = new Color(0.9f, 0.2f, 0.2f); // 红色标识
        marioSR.sortingOrder = 10;

        BoxCollider2D marioCol = mario.AddComponent<BoxCollider2D>();
        marioCol.size = new Vector2(0.8f, 1f);

        Rigidbody2D marioRb = mario.AddComponent<Rigidbody2D>();
        marioRb.gravityScale = 0f;
        marioRb.freezeRotation = true;
        marioRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        MarioController marioCtrl = mario.AddComponent<MarioController>();
        PlayerHealth marioHealth = mario.AddComponent<PlayerHealth>();
        ScanAbility scanAbility = mario.AddComponent<ScanAbility>(); // Session 10: 扫描技能

        // 设置 groundLayer（通过 SerializedObject）
        SetSerializedField(marioCtrl, "groundLayer", groundLayerMask);

        // 创建一个简单的白色方块 Sprite 给 Mario
        AssignDefaultSprite(marioSR, Color.red);

        // ═══════════════════════════════════════════════════
        // 3. Trickster
        // ═══════════════════════════════════════════════════
        GameObject trickster = new GameObject("Trickster");
        trickster.layer = LayerMask.NameToLayer("Default");
        trickster.transform.position = new Vector3(8, 1, 0);

        SpriteRenderer tricksterSR = trickster.AddComponent<SpriteRenderer>();
        tricksterSR.color = new Color(0.2f, 0.4f, 0.9f); // 蓝色标识
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
        EnergySystem energySystem = trickster.AddComponent<EnergySystem>(); // Session 10: 能量系统

        SetSerializedField(tricksterCtrl, "groundLayer", groundLayerMask);

        AssignDefaultSprite(tricksterSR, Color.blue);

        // ═══════════════════════════════════════════════════
        // 4. 管理器
        // ═══════════════════════════════════════════════════
        GameObject managers = new GameObject("Managers");

        GameManager gameManager = managers.AddComponent<GameManager>();
        InputManager inputManager = managers.AddComponent<InputManager>();
        LevelManager levelManager = managers.AddComponent<LevelManager>();

        // 连线 InputManager → 玩家引用
        SetSerializedField(inputManager, "marioController", marioCtrl);
        SetSerializedField(inputManager, "tricksterController", tricksterCtrl);

        // 连线 GameManager → 引用
        SetSerializedField(gameManager, "mario", marioCtrl);
        SetSerializedField(gameManager, "trickster", tricksterCtrl);
        SetSerializedField(gameManager, "marioHealth", marioHealth);
        SetSerializedField(gameManager, "inputManager", inputManager);

        // 出生点
        GameObject marioSpawn = new GameObject("MarioSpawnPoint");
        marioSpawn.transform.position = new Vector3(-8, 1, 0);
        marioSpawn.transform.parent = managers.transform;

        GameObject tricksterSpawn = new GameObject("TricksterSpawnPoint");
        tricksterSpawn.transform.position = new Vector3(8, 1, 0);
        tricksterSpawn.transform.parent = managers.transform;

        SetSerializedField(gameManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(gameManager, "tricksterSpawnPoint", tricksterSpawn.transform);

        // LevelManager 出生点
        SetSerializedField(levelManager, "marioSpawnPoint", marioSpawn.transform);
        SetSerializedField(levelManager, "tricksterSpawnPoint", tricksterSpawn.transform);

        // ═══════════════════════════════════════════════════
        // 5. 相机
        // ═══════════════════════════════════════════════════
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
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
        hazardSR.color = new Color(1f, 0.5f, 0f); // 橙色标识
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
        blockSR.color = new Color(0.8f, 0.6f, 0.2f); // 棕色标识
        blockSR.sortingOrder = 5;
        AssignDefaultSprite(blockSR, new Color(0.8f, 0.6f, 0.2f));

        BoxCollider2D blockCol = block.AddComponent<BoxCollider2D>();
        blockCol.size = new Vector2(1f, 1f);

        ControllableBlock cb = block.AddComponent<ControllableBlock>();

        // ═══════════════════════════════════════════════════
        // 10. 终点区域
        // ═══════════════════════════════════════════════════
        GameObject goal = new GameObject("GoalZone");
        goal.transform.position = new Vector3(18, 1, 0);

        SpriteRenderer goalSR = goal.AddComponent<SpriteRenderer>();
        goalSR.color = new Color(0f, 1f, 0f, 0.5f); // 半透明绿色
        goalSR.sortingOrder = 5;
        AssignDefaultSprite(goalSR, new Color(0f, 1f, 0f, 0.5f));

        BoxCollider2D goalCol = goal.AddComponent<BoxCollider2D>();
        goalCol.size = new Vector2(1f, 3f);
        goalCol.isTrigger = true;

        goal.AddComponent<GoalZone>();

        // ═══════════════════════════════════════════════════
        // 11. 死亡区域（关卡底部）
        // ═══════════════════════════════════════════════════
        GameObject killZone = new GameObject("KillZone");
        killZone.transform.position = new Vector3(0, -8, 0);

        BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
        killCol.size = new Vector2(60, 2);
        killCol.isTrigger = true;

        killZone.AddComponent<KillZone>();

        // ═══════════════════════════════════════════════════
        // 12. 简单敌人
        // ═══════════════════════════════════════════════════
        GameObject enemy = new GameObject("SimpleEnemy");
        enemy.transform.position = new Vector3(3, 0.5f, 0);

        SpriteRenderer enemySR = enemy.AddComponent<SpriteRenderer>();
        enemySR.color = new Color(0.6f, 0f, 0.6f); // 紫色标识
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
            coinSR.color = new Color(1f, 0.85f, 0f); // 金色
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
        breakSR.color = new Color(0.9f, 0.7f, 0.3f); // 砖色
        breakSR.sortingOrder = 5;
        AssignDefaultSprite(breakSR, new Color(0.9f, 0.7f, 0.3f));

        BoxCollider2D breakCol = breakable.AddComponent<BoxCollider2D>();
        breakCol.size = new Vector2(1f, 1f);

        breakable.AddComponent<Breakable>();

        // ═══════════════════════════════════════════════════
        // 15. 场景整理：创建父对象分组
        // ═══════════════════════════════════════════════════
        GameObject levelGeometry = new GameObject("--- Level Geometry ---");
        ground.transform.parent = levelGeometry.transform;
        platform1.transform.parent = levelGeometry.transform;
        platform2.transform.parent = levelGeometry.transform;
        platform3.transform.parent = levelGeometry.transform;
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

        GameObject collectibles = new GameObject("--- Collectibles ---");
        foreach (Collectible c in FindObjectsOfType<Collectible>())
        {
            c.transform.parent = collectibles.transform;
        }

        // ═══════════════════════════════════════════════════
        // 标记场景为已修改
        // ═══════════════════════════════════════════════════
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[TestSceneBuilder] ✅ 测试场景生成完成！\n" +
                  "  - 地面 + 3个高台 + 2面墙壁\n" +
                  "  - Mario (红色, 位置: -8,1)\n" +
                  "  - Trickster (蓝色, 位置: 8,1)\n" +
                  "  - 管理器 (GameManager + InputManager + LevelManager)\n" +
                  "  - 移动平台 (左右移动)\n" +
                  "  - 可操控平台 / 陷阱 / 方块\n" +
                  "  - 终点 (绿色, 位置: 18,1)\n" +
                  "  - 死亡区域 (底部)\n" +
                  "  - 敌人 (紫色) + 5个金币 + 可破坏方块\n" +
                  "  - 相机跟随 Mario\n" +
                  "  - [NEW] Trickster 能量系统 (EnergySystem)\n" +
                  "  - [NEW] Mario 扫描技能 (Q键, ScanAbility)\n\n" +
                  "  ⚠️ 请保存场景：Ctrl+S");

        EditorUtility.DisplayDialog(
            "生成完成",
            "测试场景已生成！\n\n" +
            "接下来请：\n" +
            "1. Ctrl+S 保存场景\n" +
            "2. 点击 Play 运行测试\n" +
            "3. P1: WASD 移动 + Space 跳跃 + Q 扫描\n" +
            "4. P2: 方向键移动 + P 伪装 + L 操控\n\n" +
            "提示：角色目前使用纯色方块，导入素材后替换 Sprite 即可。",
            "好的");
    }

    [MenuItem("MarioTrickster/Clear Test Scene", false, 2)]
    public static void ClearTestScene()
    {
        if (!EditorUtility.DisplayDialog(
            "清空测试场景",
            "这将删除场景中除 Main Camera 和 Directional Light 外的所有对象。\n继续？",
            "清空", "取消"))
        {
            return;
        }

        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj == null) continue;
            if (obj.GetComponent<Camera>() != null) continue;
            if (obj.GetComponent<Light>() != null) continue;
            if (obj.transform.parent != null) continue; // 只删除根对象

            // 移除相机上可能挂载的 CameraController
            CameraController camCtrl = obj.GetComponent<CameraController>();
            if (camCtrl != null) DestroyImmediate(camCtrl);

            if (obj.GetComponent<Camera>() == null && obj.GetComponent<Light>() == null)
            {
                DestroyImmediate(obj);
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TestSceneBuilder] 场景已清空。");
    }

    // ═══════════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════════

    /// <summary>创建地面/平台对象</summary>
    private static GameObject CreateGround(string name, Vector3 position, Vector2 size, int layer)
    {
        GameObject ground = new GameObject(name);
        ground.transform.position = position;
        ground.layer = layer;

        SpriteRenderer sr = ground.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.4f, 0.3f, 0.2f); // 土色
        sr.sortingOrder = GROUND_SORTING_ORDER;
        AssignDefaultSprite(sr, sr.color);

        // 设置缩放以匹配目标大小（默认 Sprite 是 1x1）
        ground.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D col = ground.AddComponent<BoxCollider2D>();
        // Collider 大小为 (1,1)，因为 Scale 已经处理了实际大小
        col.size = Vector2.one;

        return ground;
    }

    /// <summary>创建平台对象（带 Rigidbody2D Kinematic）</summary>
    private static GameObject CreatePlatformObject(string name, Vector3 position, Vector2 size, int layer)
    {
        GameObject platform = new GameObject(name);
        platform.transform.position = position;
        platform.layer = layer;

        SpriteRenderer sr = platform.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.3f, 0.6f, 0.8f); // 浅蓝色
        sr.sortingOrder = 2;
        AssignDefaultSprite(sr, sr.color);

        platform.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D col = platform.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        // Rigidbody2D 由 MovingPlatform/ControllablePlatform 的 RequireComponent 自动添加
        // 这里不手动添加，让组件自己处理

        return platform;
    }

    /// <summary>分配默认白色方块 Sprite</summary>
    private static void AssignDefaultSprite(SpriteRenderer sr, Color color)
    {
        // 使用 Unity 内置的白色方块纹理创建 Sprite
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        sr.color = color;
    }

    /// <summary>确保指定 Layer 存在，返回 Layer 索引</summary>
    private static int EnsureLayerExists(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer != -1) return existingLayer;

        // 查找空闲的 Layer 槽位（8-31 是用户自定义层）
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

    /// <summary>通过 SerializedObject 设置私有 SerializeField 字段</summary>
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
            default:
                Debug.LogWarning($"[TestSceneBuilder] 不支持的属性类型: {prop.propertyType} ({fieldName})");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
