using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// PlayableEnvironmentBuilder — TestConsole ASCII 关卡生成后的可玩环境补全工具。
///
/// 该类集中负责补齐 Mario、Trickster、Managers、Camera 与 KillZone 等运行时依赖，
/// 使 TestConsoleWindow 主体保持为编辑器路由与 Tab UI，不再承载冗长场景构建细节。
/// </summary>
public static class PlayableEnvironmentBuilder
{
    // ═══════════════════════════════════════════════════════════
    // S31: 自动创建可玩环境
    // 当 ASCII 模板生成关卡后，自动补全 Mario / Trickster / Managers / Camera / KillZone，
    // 使关卡可以直接按 Play 运行，与 Build Test Scene 体验一致。
    // 如果场景中已有这些对象（例如在 TestScene 中追加模板），则跳过创建，避免重复。
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 确保场景中存在完整的可玩环境。
    /// 检测 Mario / Trickster / Managers / Camera / KillZone，缺什么补什么。
    /// </summary>
    public static void EnsurePlayableEnvironment(GameObject asciiRoot)
    {
        // --- 查找 ASCII 关卡中的 SpawnPoint ---
        Transform marioSpawnT = null;
        Transform tricksterSpawnT = null;
        float levelWidth = 0f;
        float levelHeight = 0f;

        foreach (Transform child in asciiRoot.transform)
        {
            if (child.name.StartsWith("MarioSpawnPoint"))
                marioSpawnT = child;
            else if (child.name.StartsWith("TricksterSpawnPoint"))
                tricksterSpawnT = child;

            // 计算关卡边界
            float x = child.position.x;
            float y = child.position.y;
            if (x > levelWidth) levelWidth = x;
            if (y > levelHeight) levelHeight = y;
        }

        // 默认出生位置（如果模板中没有 M/T 字符）
        Vector3 marioSpawnPos = marioSpawnT != null ? marioSpawnT.position : new Vector3(2f, 2f, 0f);
        Vector3 tricksterSpawnPos = tricksterSpawnT != null ? tricksterSpawnT.position : marioSpawnPos + new Vector3(1f, 0f, 0f);

        // 关卡边界（留余量）
        float boundMinX = -3f;
        float boundMaxX = levelWidth + 5f;
        float boundMinY = -10f;
        float boundMaxY = levelHeight + 10f;

        // --- 确保 Ground Layer 存在 ---
        int groundLayerIndex = LayerMask.NameToLayer("Ground");
        if (groundLayerIndex == -1) groundLayerIndex = 0;
        LayerMask groundLayerMask = 1 << groundLayerIndex;

        // --- 确保 Player / Trickster Layer 存在（B028 兼容）---
        int playerLayerIndex = EnsureLayerForPlayable("Player");
        int tricksterLayerIndex = EnsureLayerForPlayable("Trickster");
        Physics2D.IgnoreLayerCollision(playerLayerIndex, tricksterLayerIndex, true);

        // ═══════════════════════════════════════════════════
        // Mario
        // ═══════════════════════════════════════════════════
        MarioController marioCtrl = Object.FindObjectOfType<MarioController>();
        PlayerHealth marioHealth = null;
        GameObject mario;

        if (marioCtrl == null)
        {
            mario = new GameObject("Mario");
            mario.tag = "Player";
            mario.layer = playerLayerIndex;
            mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;

            // S37: 视碰分离 — 创建 Visual 子节点承载 SpriteRenderer
            GameObject marioVisual = new GameObject("Visual");
            marioVisual.transform.SetParent(mario.transform, false);
            // S-Fix: 视碰对齐 — Visual 下移使 Sprite 底边对齐碰撞体底边，消除悬空
            marioVisual.transform.localPosition = new Vector3(0f, PhysicsMetrics.MARIO_VISUAL_OFFSET_Y, 0f);

            SpriteRenderer marioSR = marioVisual.AddComponent<SpriteRenderer>();
            marioSR.color = new Color(0.9f, 0.2f, 0.2f);
            marioSR.sortingOrder = 10;
            AssignDefaultSpriteForPlayable(marioSR, marioSR.color);

            BoxCollider2D marioCol = mario.AddComponent<BoxCollider2D>();
            marioCol.size = new Vector2(PhysicsMetrics.MARIO_COLLIDER_WIDTH, PhysicsMetrics.MARIO_COLLIDER_HEIGHT);
            marioCol.offset = new Vector2(0f, PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y);

            Rigidbody2D marioRb = mario.AddComponent<Rigidbody2D>();
            marioRb.gravityScale = 0f; // MarioController 自行管理重力
            marioRb.freezeRotation = true;
            marioRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            marioCtrl = mario.AddComponent<MarioController>();
            marioCtrl.visualTransform = marioVisual.transform; // S37: 赋值视觉代理节点
            marioHealth = mario.AddComponent<PlayerHealth>();
            mario.AddComponent<ScanAbility>();

            SetSerializedFieldForPlayable(marioCtrl, "groundLayer", groundLayerMask);

            // Session 32: 自动挂载跳跃抛物线可视化工具
            mario.AddComponent<JumpArcVisualizer>();

            Undo.RegisterCreatedObjectUndo(mario, "Create Mario");
            Debug.Log("[TestConsole] Created Mario at " + mario.transform.position);
        }
        else
        {
            mario = marioCtrl.gameObject;
            marioHealth = mario.GetComponent<PlayerHealth>();
            // 将已有 Mario 传送到新关卡的出生点
            mario.transform.position = marioSpawnPos + Vector3.up * 0.5f;
            // 修复已有 Mario 的 groundLayer 未设置问题（消除黄色警告）
            SetSerializedFieldForPlayable(marioCtrl, "groundLayer", groundLayerMask);
        }

        // ═════════════════════════════════════════════════
        // Trickster
        // ═══════════════════════════════════════════════════
        TricksterController tricksterCtrl = Object.FindObjectOfType<TricksterController>();
        GameObject trickster;

        if (tricksterCtrl == null)
        {
            trickster = new GameObject("Trickster");
            trickster.layer = tricksterLayerIndex;
            trickster.transform.position = tricksterSpawnPos + Vector3.up * 0.5f;

            // S37: 视碰分离 — 创建 Visual 子节点承载 SpriteRenderer
            GameObject tricksterVisual = new GameObject("Visual");
            tricksterVisual.transform.SetParent(trickster.transform, false);
            // S-Fix: 视碰对齐 — Visual 下移使 Sprite 底边对齐碰撞体底边，消除悬空
            tricksterVisual.transform.localPosition = new Vector3(0f, PhysicsMetrics.TRICKSTER_VISUAL_OFFSET_Y, 0f);

            SpriteRenderer tricksterSR = tricksterVisual.AddComponent<SpriteRenderer>();
            tricksterSR.color = new Color(0.2f, 0.4f, 0.9f);
            tricksterSR.sortingOrder = 10;
            AssignDefaultSpriteForPlayable(tricksterSR, tricksterSR.color);

            BoxCollider2D tricksterCol = trickster.AddComponent<BoxCollider2D>();
            tricksterCol.size = new Vector2(PhysicsMetrics.TRICKSTER_COLLIDER_WIDTH, PhysicsMetrics.TRICKSTER_COLLIDER_HEIGHT);
            tricksterCol.offset = new Vector2(0f, PhysicsMetrics.TRICKSTER_COLLIDER_OFFSET_Y);

            Rigidbody2D tricksterRb = trickster.AddComponent<Rigidbody2D>();
            tricksterRb.gravityScale = 0f;
            tricksterRb.freezeRotation = true;
            tricksterRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            tricksterCtrl = trickster.AddComponent<TricksterController>();
            tricksterCtrl.visualTransform = tricksterVisual.transform; // S37: 赋值视觉代理节点
            DisguiseSystem disguiseSystem = trickster.AddComponent<DisguiseSystem>();
            trickster.AddComponent<TricksterAbilitySystem>();
            trickster.AddComponent<EnergySystem>();

            // S60-Fix: 自动配置默认伪装形态，消除 "未配置伪装形态" 警告
            ConfigureDefaultDisguisesForPlayable(disguiseSystem);

            SetSerializedFieldForPlayable(tricksterCtrl, "groundLayer", groundLayerMask);

            Undo.RegisterCreatedObjectUndo(trickster, "Create Trickster");
            Debug.Log("[TestConsole] Created Trickster at " + trickster.transform.position);
        }
        else
        {
            trickster = tricksterCtrl.gameObject;
            trickster.transform.position = tricksterSpawnPos + Vector3.up * 0.5f;
            // 修复已有 Trickster 的 groundLayer 未设置问题（消除黄色警告）
            SetSerializedFieldForPlayable(tricksterCtrl, "groundLayer", groundLayerMask);
        }

        // ═══════════════════════════════════════════════════
        // Managers (GameManager + InputManager + LevelManager)
        // ═══════════════════════════════════════════════════
        GameManager gameManager = Object.FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            GameObject managers = new GameObject("Managers");

            gameManager = managers.AddComponent<GameManager>();
            InputManager inputManager = managers.AddComponent<InputManager>();
            LevelManager levelManager = managers.AddComponent<LevelManager>();

            // 连线 InputManager
            SetSerializedFieldForPlayable(inputManager, "marioController", marioCtrl);
            SetSerializedFieldForPlayable(inputManager, "tricksterController", tricksterCtrl);

            // 连线 GameManager
            SetSerializedFieldForPlayable(gameManager, "mario", marioCtrl);
            SetSerializedFieldForPlayable(gameManager, "trickster", tricksterCtrl);
            if (marioHealth != null)
                SetSerializedFieldForPlayable(gameManager, "marioHealth", marioHealth);
            SetSerializedFieldForPlayable(gameManager, "inputManager", inputManager);

            // SpawnPoint（使用 ASCII 模板中的位置或默认位置）
            GameObject marioSP = marioSpawnT != null ? marioSpawnT.gameObject : new GameObject("MarioSpawnPoint");
            GameObject tricksterSP = tricksterSpawnT != null ? tricksterSpawnT.gameObject : new GameObject("TricksterSpawnPoint");
            if (marioSpawnT == null)
            {
                marioSP.transform.position = marioSpawnPos;
                marioSP.transform.parent = managers.transform;
            }
            if (tricksterSpawnT == null)
            {
                tricksterSP.transform.position = tricksterSpawnPos;
                tricksterSP.transform.parent = managers.transform;
            }

            SetSerializedFieldForPlayable(gameManager, "marioSpawnPoint", marioSP.transform);
            SetSerializedFieldForPlayable(gameManager, "tricksterSpawnPoint", tricksterSP.transform);
            SetSerializedFieldForPlayable(levelManager, "marioSpawnPoint", marioSP.transform);
            SetSerializedFieldForPlayable(levelManager, "tricksterSpawnPoint", tricksterSP.transform);

            // 关卡边界
            SetSerializedFieldForPlayable(levelManager, "levelMinX", boundMinX);
            SetSerializedFieldForPlayable(levelManager, "levelMaxX", boundMaxX);
            SetSerializedFieldForPlayable(levelManager, "levelMinY", boundMinY);
            SetSerializedFieldForPlayable(levelManager, "levelMaxY", boundMaxY);

            Undo.RegisterCreatedObjectUndo(managers, "Create Managers");
            Debug.Log("[TestConsole] Created Managers (GameManager + InputManager + LevelManager)");
        }

        // UGUI HUD：优先实例化标准 GlobalGameUICanvas Prefab，替代旧 OnGUI 灰盒 HUD。
        EnsureGlobalGameUICanvas(gameManager != null ? gameManager.transform : null);

        // ═══════════════════════════════════════════════════
        // Camera
        // ═══════════════════════════════════════════════════
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            CameraController camCtrl = mainCam.GetComponent<CameraController>();
            if (camCtrl == null)
            {
                camCtrl = mainCam.gameObject.AddComponent<CameraController>();
                Undo.RegisterCreatedObjectUndo(camCtrl, "Add CameraController");
            }

            SetSerializedFieldForPlayable(camCtrl, "target", mario.transform);
            SetSerializedFieldForPlayable(camCtrl, "useBounds", true);
            SetSerializedFieldForPlayable(camCtrl, "minX", boundMinX);
            SetSerializedFieldForPlayable(camCtrl, "maxX", boundMaxX);
            SetSerializedFieldForPlayable(camCtrl, "minY", boundMinY - 5f);
            SetSerializedFieldForPlayable(camCtrl, "maxY", boundMaxY);

            // 将相机移到 Mario 位置
            mainCam.transform.position = new Vector3(marioSpawnPos.x, marioSpawnPos.y + 2f, -10f);
            mainCam.orthographicSize = 7;
        }

        // ═══════════════════════════════════════════════════
        // KillZone（底部死亡区域）
        // ═══════════════════════════════════════════════════
        KillZone existingKillZone = Object.FindObjectOfType<KillZone>();
        if (existingKillZone == null)
        {
            GameObject killZone = new GameObject("KillZone");
            killZone.transform.position = new Vector3(levelWidth / 2f, -8f, 0);
            BoxCollider2D killCol = killZone.AddComponent<BoxCollider2D>();
            killCol.size = new Vector2(levelWidth + 30f, 2f);
            killCol.isTrigger = true;
            KillZone kz = killZone.AddComponent<KillZone>();
            kz.SetFallbackY(-13f); // S48b: Y 坐标兜底阈值（KillZone 在 y=-8，再留 5 格余量）

            Undo.RegisterCreatedObjectUndo(killZone, "Create KillZone");
            Debug.Log("[TestConsole] Created KillZone below level");
        }

        Debug.Log($"[TestConsole] ✅ Playable environment ready! Mario at {mario.transform.position}, bounds: X[{boundMinX},{boundMaxX}] Y[{boundMinY},{boundMaxY}]");

        // S41: 补全环境后同步 Picking 状态（Mario/Trickster 的 Visual 子节点在此方法中创建）
        LevelEditorPickingManager.SyncState();
    }

    // ═══════════════════════════════════════════════════
    // EnsurePlayableEnvironment 辅助方法
    // ═══════════════════════════════════════════════════

    /// <summary>确保标准 UGUI HUD Canvas 存在。</summary>
    private static void EnsureGlobalGameUICanvas(Transform parent)
    {
        GlobalGameUICanvas existing = Object.FindObjectOfType<GlobalGameUICanvas>();
        if (existing != null) return;

        GameObject prefab = GlobalGameUICanvasPrefabBuilder.EnsurePrefabAsset();
        GameObject uiObject = prefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
            : new GameObject("GlobalGameUICanvas");

        if (uiObject.GetComponent<GlobalGameUICanvas>() == null)
        {
            uiObject.AddComponent<GlobalGameUICanvas>();
        }

        if (parent != null)
        {
            uiObject.transform.SetParent(parent, false);
        }

        Undo.RegisterCreatedObjectUndo(uiObject, "Create GlobalGameUICanvas");
        Debug.Log("[TestConsole] Created GlobalGameUICanvas UGUI HUD");
    }

    /// <summary>确保 Layer 存在，不存在则创建</summary>
    private static int EnsureLayerForPlayable(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing != -1) return existing;

        SerializedObject tagManager = new SerializedObject(
            UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[TestConsole] Created Layer: {layerName} (index: {i})");
                return i;
            }
        }

        Debug.LogError($"[TestConsole] Cannot create Layer '{layerName}': all custom layer slots are full!");
        return 0;
    }

    // ═══════════════════════════════════════════════════
    // S60-Fix: 配置默认伪装形态
    // 与 TestSceneBuilder.ConfigureDefaultTricksterDisguises 逻辑一致，
    // 确保 Custom Template Editor 生成的关卡也能正常使用 Trickster 伪装功能。
    // ═══════════════════════════════════════════════════

    private static void ConfigureDefaultDisguisesForPlayable(DisguiseSystem disguiseSystem)
    {
        if (disguiseSystem == null) return;

        SerializedObject so = new SerializedObject(disguiseSystem);
        SerializedProperty disguises = so.FindProperty("availableDisguises");
        SerializedProperty currentIndex = so.FindProperty("currentDisguiseIndex");

        if (disguises == null)
        {
            Debug.LogWarning("[PlayableEnvironmentBuilder] 找不到 DisguiseSystem.availableDisguises，无法自动配置伪装。");
            return;
        }

        // 只在列表为空时配置（避免覆盖用户手动设置）
        if (disguises.arraySize > 0)
        {
            so.Dispose();
            return;
        }

        disguises.ClearArray();
        disguises.arraySize = 2;

        // 伪装形态 1: 蓝色方块
        ConfigureDisguiseEntryForPlayable(
            disguises.GetArrayElementAtIndex(0),
            "Default Blue Block",
            CreateSolidColorSpriteForPlayable(new Color(0.25f, 0.55f, 1f)),
            new Vector2(1.2f, 1.2f),
            Vector2.zero,
            new Vector3(1.2f, 1.2f, 1f),
            0); // DisguiseType.Static

        // 伪装形态 2: 细长平台
        ConfigureDisguiseEntryForPlayable(
            disguises.GetArrayElementAtIndex(1),
            "Default Slim Platform",
            CreateSolidColorSpriteForPlayable(new Color(0.35f, 0.85f, 1f)),
            new Vector2(2.4f, 0.5f),
            Vector2.zero,
            new Vector3(2.4f, 0.5f, 1f),
            0); // DisguiseType.Static

        if (currentIndex != null)
            currentIndex.intValue = 0;

        so.ApplyModifiedProperties();
        Debug.Log("[PlayableEnvironmentBuilder] ✅ 已为 Trickster 配置 2 个默认伪装形态。");
    }

    private static void ConfigureDisguiseEntryForPlayable(
        SerializedProperty entry,
        string disguiseName,
        Sprite sprite,
        Vector2 colliderSize,
        Vector2 colliderOffset,
        Vector3 customScale,
        int typeIndex)
    {
        if (entry == null) return;

        entry.FindPropertyRelative("disguiseName").stringValue = disguiseName;
        entry.FindPropertyRelative("disguiseSprite").objectReferenceValue = sprite;
        entry.FindPropertyRelative("iconSprite").objectReferenceValue = sprite;
        entry.FindPropertyRelative("customColliderSize").vector2Value = colliderSize;
        entry.FindPropertyRelative("customColliderOffset").vector2Value = colliderOffset;
        entry.FindPropertyRelative("customScale").vector3Value = customScale;
        entry.FindPropertyRelative("type").enumValueIndex = typeIndex;
    }

    private static Sprite CreateSolidColorSpriteForPlayable(Color color)
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    /// <summary>为可玩环境对象分配默认白盒 Sprite</summary>
    private static void AssignDefaultSpriteForPlayable(SpriteRenderer sr, Color color)
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

    /// <summary>通过 SerializedObject 设置字段值（与 TestSceneBuilder 同逻辑）</summary>
    private static void SetSerializedFieldForPlayable(Object target, string fieldName, object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(fieldName);

        if (prop == null)
        {
            Debug.LogWarning($"[TestConsole] Field not found: {target.GetType().Name}.{fieldName}");
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
                Debug.LogWarning($"[TestConsole] Unsupported property type: {prop.propertyType} ({fieldName})");
                break;
        }

        so.ApplyModifiedProperties();
    }
}
