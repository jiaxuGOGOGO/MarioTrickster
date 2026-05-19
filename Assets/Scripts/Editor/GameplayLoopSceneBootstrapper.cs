using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// S53 微创旁路补齐器：只在 Editor 层为 Level Studio 生成的白盒场景补齐 Gameplay Loop 服务与实战房语义。
/// 不修改 AsciiElementRegistry，不新增 ASCII 字符，不改碰撞尺寸；形变仍由各对象现有 visual/localScale 负责。
/// </summary>
public static class GameplayLoopSceneBootstrapper
{
    private const string ManagersName = "Managers";
    private const string GameplayLoopManagersName = "Managers_GameplayLoop";
    private const string DefaultMarioSpawnName = "MarioSpawnPoint";
    private const string DefaultTricksterSpawnName = "TricksterSpawnPoint";

    /// <summary>
    /// 为当前 root 补齐 Gameplay Loop 所需服务。该方法只做“缺什么补什么”，不会重建现有核心对象。
    /// </summary>
    public static void EnsureGameplayLoopServices(GameObject root)
    {
        root = ResolveRoot(root);
        GameObject managers = FindOrCreateManagers(root);

        // TestSceneBuilder 中 Managers_GameplayLoop 的服务清单：按原顺序补齐，避免重构底层生命周期。
        EnsureComponent<MarioSuspicionTracker>(managers);
        EnsureComponent<ResidueVisualHint>(managers);
        EnsureComponent<SuspicionHUD>(managers);
        EnsureComponent<RouteBudgetService>(managers);
        EnsureComponent<InterferenceCompensationPolicy>(managers);
        EnsureComponent<RepeatInterferenceStack>(managers);
        EnsureComponent<CounterRevealReward>(managers);
        EnsureComponent<PropComboTracker>(managers);
        EnsureComponent<TricksterHeatMeter>(managers);
        EnsureComponent<HeatBreachHint>(managers);
        EnsureComponent<HeatSuspicionBridge>(managers);
        EnsureComponent<AlarmCrisisDirector>(managers);
        EnsureComponent<LootEscapeHUD>(managers);

        GameManager gameManager = EnsureComponent<GameManager>(managers);
        InputManager inputManager = EnsureComponent<InputManager>(managers);
        LevelManager levelManager = EnsureComponent<LevelManager>(managers);

        BindCoreManagers(root, managers, gameManager, inputManager, levelManager);
        EnsureGlobalGameUICanvas(managers.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[GameplayLoopSceneBootstrapper] Gameplay Loop services ensured.");
    }

    /// <summary>
    /// 将 Level Studio 实战房中由既有 ASCII 字典生成的普通收集/终点语义，后处理为 Gameplay Loop 语义。
    /// </summary>
    public static void EnsureCombatRoomSemantics(GameObject root)
    {
        root = ResolveRoot(root);

        int lootFixCount = 0;
        Collectible[] collectibles = root.GetComponentsInChildren<Collectible>(true);
        foreach (Collectible collectible in collectibles)
        {
            if (collectible == null) continue;
            GameObject go = collectible.gameObject;
            EnsureTriggerBoxCollider(go);
            if (go.GetComponent<LootObjective>() == null)
            {
                Undo.AddComponent<LootObjective>(go);
                lootFixCount++;
            }
            Undo.DestroyObjectImmediate(collectible);
            if (!go.name.Contains("LootObjective"))
                go.name = $"LootObjective_{go.name}";
        }

        int gateFixCount = 0;
        GoalZone[] goalZones = root.GetComponentsInChildren<GoalZone>(true);
        foreach (GoalZone goalZone in goalZones)
        {
            if (goalZone == null) continue;
            GameObject go = goalZone.gameObject;
            EnsureTriggerBoxCollider(go);
            if (go.GetComponent<EscapeGate>() == null)
            {
                Undo.AddComponent<EscapeGate>(go);
                gateFixCount++;
            }
            Undo.DestroyObjectImmediate(goalZone);
            if (!go.name.Contains("EscapeGate"))
                go.name = $"EscapeGate_{go.name}";
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[GameplayLoopSceneBootstrapper] Combat semantics ensured. LootObjective added: {lootFixCount}, EscapeGate added: {gateFixCount}.");
    }

    /// <summary>
    /// 供 Level Studio 面板判断是否显示 Auto-Fix 按钮。
    /// </summary>
    public static bool NeedsGameplayLoopAutoFix(GameObject root)
    {
        root = root != null ? root : ResolveActiveLevelRoot();
        if (root == null) return false;

        bool hasCombatSemantics = root.GetComponentInChildren<Collectible>(true) != null
            || root.GetComponentInChildren<GoalZone>(true) != null
            || FindComponentInScope<LootObjective>(root) != null
            || FindComponentInScope<EscapeGate>(root) != null;

        if (!hasCombatSemantics) return false;

        if (FindComponentInScope<RouteBudgetService>(root) == null) return true;
        if (FindComponentInScope<TricksterHeatMeter>(root) == null) return true;
        if (FindComponentInScope<AlarmCrisisDirector>(root) == null) return true;
        if (FindComponentInScope<PropComboTracker>(root) == null) return true;
        if (FindComponentInScope<LootObjective>(root) == null && root.GetComponentInChildren<Collectible>(true) != null) return true;
        if (FindComponentInScope<EscapeGate>(root) == null && root.GetComponentInChildren<GoalZone>(true) != null) return true;
        return false;
    }

    /// <summary>
    /// 优先返回 Level Studio 生成根节点；找不到时返回 null，避免面板绘制阶段误选普通场景根对象。
    /// </summary>
    public static GameObject ResolveActiveLevelRoot()
    {
        GameObject asciiRoot = GameObject.Find("AsciiLevelRoot");
        if (asciiRoot != null) return asciiRoot;

        GameObject generatedRoot = GameObject.Find("GeneratedLevelRoot");
        if (generatedRoot != null) return generatedRoot;

        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name.Contains("Level"))
                return roots[i];
        }
        return null;
    }

    private static GameObject ResolveRoot(GameObject root)
    {
        if (root != null) return root;
        root = ResolveActiveLevelRoot();
        if (root != null) return root;

        GameObject fallback = new GameObject("AsciiLevelRoot");
        Undo.RegisterCreatedObjectUndo(fallback, "Create Gameplay Loop Root");
        return fallback;
    }

    private static GameObject FindOrCreateManagers(GameObject root)
    {
        GameObject managers = FindChildByExactName(root, ManagersName);
        if (managers == null)
            managers = FindChildByExactName(root, GameplayLoopManagersName);
        if (managers == null)
        {
            GameManager existingGameManager = Object.FindObjectOfType<GameManager>();
            if (existingGameManager != null)
                managers = existingGameManager.gameObject;
        }
        if (managers != null)
        {
            if (managers.transform.parent == null && root != null)
                Undo.SetTransformParent(managers.transform, root.transform, "Parent Managers To Root");
            return managers;
        }

        managers = new GameObject(ManagersName);
        Undo.RegisterCreatedObjectUndo(managers, "Create Gameplay Loop Managers");
        if (root != null)
            Undo.SetTransformParent(managers.transform, root.transform, "Parent Gameplay Loop Managers");
        return managers;
    }

    private static GameObject FindChildByExactName(GameObject root, string childName)
    {
        if (root == null) return GameObject.Find(childName);
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i].gameObject;
        }
        return GameObject.Find(childName);
    }

    private static T EnsureComponent<T>(GameObject host) where T : Component
    {
        T component = host.GetComponent<T>();
        if (component != null) return component;
        return Undo.AddComponent<T>(host);
    }

    private static T FindComponentInScope<T>(GameObject root) where T : Component
    {
        if (root != null)
        {
            T scoped = root.GetComponentInChildren<T>(true);
            if (scoped != null) return scoped;
        }
        return Object.FindObjectOfType<T>();
    }

    private static void BindCoreManagers(GameObject root, GameObject managers, GameManager gameManager, InputManager inputManager, LevelManager levelManager)
    {
        MarioController mario = FindComponentInScope<MarioController>(root);
        TricksterController trickster = FindComponentInScope<TricksterController>(root);
        PlayerHealth marioHealth = mario != null ? mario.GetComponent<PlayerHealth>() : FindComponentInScope<PlayerHealth>(root);

        if (inputManager != null)
        {
            if (mario != null) SetSerializedField(inputManager, "marioController", mario);
            if (trickster != null) SetSerializedField(inputManager, "tricksterController", trickster);
        }

        Transform marioSpawn = FindOrCreateSpawn(root, managers, DefaultMarioSpawnName, mario != null ? mario.transform.position : new Vector3(-4f, 1f, 0f));
        Transform tricksterSpawn = FindOrCreateSpawn(root, managers, DefaultTricksterSpawnName, trickster != null ? trickster.transform.position : new Vector3(-2f, 1f, 0f));

        if (gameManager != null)
        {
            if (mario != null) SetSerializedField(gameManager, "mario", mario);
            if (trickster != null) SetSerializedField(gameManager, "trickster", trickster);
            if (marioHealth != null) SetSerializedField(gameManager, "marioHealth", marioHealth);
            if (inputManager != null) SetSerializedField(gameManager, "inputManager", inputManager);
            SetSerializedField(gameManager, "marioSpawnPoint", marioSpawn);
            SetSerializedField(gameManager, "tricksterSpawnPoint", tricksterSpawn);
        }

        if (levelManager != null)
        {
            SetSerializedField(levelManager, "marioSpawnPoint", marioSpawn);
            SetSerializedField(levelManager, "tricksterSpawnPoint", tricksterSpawn);
            Bounds bounds = CalculateSceneBounds(root);
            SetSerializedField(levelManager, "levelMinX", bounds.min.x - 4f);
            SetSerializedField(levelManager, "levelMaxX", bounds.max.x + 4f);
            SetSerializedField(levelManager, "levelMinY", bounds.min.y - 8f);
            SetSerializedField(levelManager, "levelMaxY", bounds.max.y + 8f);
        }
    }

    private static Transform FindOrCreateSpawn(GameObject root, GameObject managers, string spawnName, Vector3 fallbackPosition)
    {
        GameObject spawn = FindChildByExactName(root, spawnName);

        // [BugFix] Fallback: Generator 创建的对象名为 "MarioSpawn_x_y" 而非 "MarioSpawnPoint"。
        // 如果精确名找不到，用前缀匹配查找（去掉 "Point" 后缀）。
        if (spawn == null)
        {
            string prefix = spawnName.Replace("Point", ""); // "MarioSpawnPoint" -> "MarioSpawn"
            spawn = FindChildByPrefix(root, prefix);
        }

        if (spawn == null)
        {
            spawn = new GameObject(spawnName);
            Undo.RegisterCreatedObjectUndo(spawn, $"Create {spawnName}");
            spawn.transform.position = fallbackPosition;
        }
        if (spawn.transform.parent == null && managers != null)
            Undo.SetTransformParent(spawn.transform, managers.transform, $"Parent {spawnName}");
        return spawn.transform;
    }

    /// <summary>按前缀查找子对象（兼容 Generator 命名格式）</summary>
    private static GameObject FindChildByPrefix(GameObject root, string prefix)
    {
        if (root != null)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name.StartsWith(prefix))
                    return children[i].gameObject;
            }
        }
        // 场景级查找
        foreach (GameObject obj in Object.FindObjectsOfType<GameObject>())
        {
            if (obj.name.StartsWith(prefix))
                return obj;
        }
        return null;
    }

    private static Bounds CalculateSceneBounds(GameObject root)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Object.FindObjectsOfType<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private static void EnsureTriggerBoxCollider(GameObject go)
    {
        BoxCollider2D collider = go.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider2D>(go);
        collider.isTrigger = true;
    }

    private static void EnsureGlobalGameUICanvas(Transform parent)
    {
        GlobalGameUICanvas existing = Object.FindObjectOfType<GlobalGameUICanvas>();
        if (existing != null) return;

        GameObject prefab = GlobalGameUICanvasPrefabBuilder.EnsurePrefabAsset(false);
        GameObject uiObject = prefab != null
            ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
            : new GameObject("GlobalGameUICanvas", typeof(RectTransform));

        if (uiObject == null) return;
        Undo.RegisterCreatedObjectUndo(uiObject, "Create Global Game UI Canvas");
        if (uiObject.GetComponent<GlobalGameUICanvas>() == null)
            Undo.AddComponent<GlobalGameUICanvas>(uiObject);
        if (parent != null)
            Undo.SetTransformParent(uiObject.transform, parent, "Parent Global Game UI Canvas");
    }

    private static void SetSerializedField(Object target, string fieldName, object value)
    {
        if (target == null) return;

        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"[GameplayLoopSceneBootstrapper] Field not found: {target.GetType().Name}.{fieldName}");
            return;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = value as Object;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = value is float f ? f : 0f;
                break;
            case SerializedPropertyType.Integer:
                property.intValue = value is int i ? i : 0;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = value is bool b && b;
                break;
            case SerializedPropertyType.String:
                property.stringValue = value as string;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = value is Vector3 v3 ? v3 : Vector3.zero;
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = value is Vector2 v2 ? v2 : Vector2.zero;
                break;
            case SerializedPropertyType.LayerMask:
                property.intValue = value is LayerMask mask ? mask.value : 0;
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = value is int enumIndex ? enumIndex : property.enumValueIndex;
                break;
            default:
                Debug.LogWarning($"[GameplayLoopSceneBootstrapper] Unsupported field type: {property.propertyType} ({fieldName})");
                break;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
