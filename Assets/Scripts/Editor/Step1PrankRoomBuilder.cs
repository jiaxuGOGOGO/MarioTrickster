using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 设计宪法第 1 步：一键生成"恶作剧房间"（一间手工房 + 冲冲型马里奥）。
/// 菜单：MarioTrickster/Step 1/Build Prank Room
/// 保存到 Assets/Scenes/Step1_PrankRoom.unity；调参资产 Assets/Resources/Step1/RushMarioTuning.asset。
/// 房间约定：
///   - 一屏大小，默认整屏镜头（Step1RoomCamera 替代跟随马里奥的 CameraController）。
///   - 不放地刺（周期伤害 + 卡住伸出问题）/ 摆锤（常驻危险）/ 弹跳（不可预测）。
///   - 火焰陷阱改为"平时安全"：coolOffDuration 调到极大，只有捣蛋者触发才会喷火（预热闪烁 = 警告）。
///   - 恶作剧方式：火焰、封路拖延 + 火焰、崩塌桥掉坑（坑里有火）、引诱追逐后脱身。
/// </summary>
public static class Step1PrankRoomBuilder
{
    public const string ScenePath = "Assets/Scenes/Step1_PrankRoom.unity";
    public const string TuningAssetPath = "Assets/Resources/Step1/RushMarioTuning.asset";
    /// <summary>火焰陷阱平时的冷却时长（秒）：大到一局内不会自己喷火。</summary>
    public const float IdleSafeFireCoolOff = 100000f;
    public const float TricksterControlRange = 5f;
    /// <summary>
    /// 构建器版本：每次改动房间生成逻辑都 +1。"Play Prank Room" 发现场景里记录的版本更旧就自动重建，
    /// 用户拉取新版本后不需要记得手动 Build。
    /// S181 = 2：每回合机关复位 + H10 无干预检查组件 + 每局问卷。
    /// S182 = 3：干净的中英对照界面（Step1Screen）、双语房间标牌。
    /// S183 = 4：崩塌桥只由玩家触发 + 桥下有人不重生（修"马里奥被关在坑里"）。
    /// S184 = 5：房间扩大为 48×12 三区，加遮挡（高墙 + 箱子）。
    /// </summary>
    public const int BuilderVersion = 5;

    // 行 0 在最上面；世界 y = 高度 - 1 - 行号；地面为 y0..y2，站立层 y3。
    // S184（用户反馈"地图太小、博弈空间不够"）：36×10 → 48×12，分三区（放松区 / 中区 / 宝物区，宪法 P3）：
    //   - 两道高墙 x16/x31 只在地面留 1 格门洞，门洞里是封路墙 '['（关门 = 整条路被截断 3.5 秒）；
    //     高墙挡视线：马里奥在一区看不到二区台子上的你（藏身/换位空间）。
    //   - 地面 1 格高的箱子 '#'（x12、x40）挡低处视线：蹲在箱子后面不会被看见；马里奥会跳过去。
    //   - 崩塌桥 x21..24 + 坑（桥下有人不重生，x25 单向台面可从坑里跳出）。
    //   - 三把火 x9 / x28 / x37（平时安全，只有你能点）。
    public static readonly string[] Room =
    {
        "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
        "W...............W..............W...............W",
        "W...............W..............W...............W",
        "W...............W..............W...............W",
        "W...............W..............W...............W",
        "W...............W..............W...............W",
        "W...............W..............W...............W",
        "W.....----......W.---.....----.W...----........W",
        "W.G.M....~..#...[..T........~..[.....~..#...o..W",
        "W####################CCCC-#####################W",
        "W####################..~..#####################W",
        "W##############################################W"
    };

    public static string RoomAscii => string.Join("\n", Room);

    [MenuItem("MarioTrickster/Step 1/Build Prank Room", false, 10)]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Step 1", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report = Validate(out bool ok);
        if (!ok) { EditorUtility.DisplayDialog("Step 1", "Room template invalid:\n" + report, "OK"); return; }
        Build();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Step1] Prank room saved to " + ScenePath + "\n" + report);
        EditorUtility.DisplayDialog("Step 1", "Prank room built:\n" + ScenePath + "\n\nPress Play. Keys: arrows move, P disguise, L trigger, C camera.", "OK");
    }

    [MenuItem("MarioTrickster/Step 1/Open Prank Room", false, 11)]
    public static void OpenMenu()
    {
        if (!File.Exists(ScenePath)) { BuildMenu(); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }

    /// <summary>S181 一键试玩：没有场景或场景是旧版本 → 自动重建；然后直接进入 Play。</summary>
    [MenuItem("MarioTrickster/Step 1/▶ Play Prank Room", false, 0)]
    public static void PlayMenu()
    {
        if (!PrepareScene()) return;
        PlayerPrefs.DeleteKey(Step1HandsOffCheck.RequestKey);
        EditorApplication.isPlaying = true;
    }

    /// <summary>S181 宪法 H10：自动连跑几局、捣蛋者退场，看马里奥能否自己拿宝回家。结果显示在屏幕上并写 CSV。</summary>
    [MenuItem("MarioTrickster/Step 1/Hands-off Check (H10)", false, 1)]
    public static void HandsOffMenu()
    {
        if (!PrepareScene()) return;
        PlayerPrefs.SetInt(Step1HandsOffCheck.RequestKey, 1);
        PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
    }

    [MenuItem("MarioTrickster/Step 1/Open Playtest Logs Folder", false, 2)]
    public static void OpenLogsMenu()
    {
        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", Step1PlaytestLog.LogFolder);
        Directory.CreateDirectory(folder);
        EditorUtility.RevealInFinder(folder);
    }

    /// <summary>打开恶作剧房间；缺失或版本旧时静默重建。返回 false = 用户取消或出错。</summary>
    public static bool PrepareScene()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Step 1", "Stop Play Mode first.", "OK"); return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EnsureTuningAsset(); // 同时升级旧调参资产（场景不用重建时也要生效）
        if (File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            var marker = Object.FindObjectOfType<Step1RoomReset>();
            if (marker != null && marker.BuiltVersion >= BuilderVersion) return true;
            Debug.Log("[Step1] Prank room scene is from an older build - rebuilding automatically.");
        }
        string report = Validate(out bool ok);
        if (!ok) { EditorUtility.DisplayDialog("Step 1", "Room template invalid:\n" + report, "OK"); return false; }
        Build();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        return true;
    }

    public static string Validate(out bool ok)
    {
        ok = LevelStudioDocument.TryParse(RoomAscii, out var doc, out string error);
        if (!ok) return error;
        error = doc.PlayReadiness();
        if (!string.IsNullOrEmpty(error)) { ok = false; return error; }
        var l1 = AsciiLevelValidator.ValidateTemplate(doc.Grid);
        var l2 = LevelReachabilityAnalyzer.Analyze(doc.Grid);
        ok = l1.errors.Count == 0 && l2.IsReachable;
        return "L1 errors=" + l1.errors.Count + ", warnings=" + l1.warnings.Count + "; L2 reachable=" + l2.IsReachable +
            (l1.errors.Count + l1.warnings.Count > 0 ? "\n" + string.Join("\n", l1.errors.Concat(l1.warnings)) : "");
    }

    public static MarioMindTuningSO EnsureTuningAsset()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<MarioMindTuningSO>(TuningAssetPath);
        if (tuning != null)
        {
            // 旧资产（S180/S181）写入一次 S183 校准值；之后的手动调参不会被覆盖。
            if (tuning.UpgradeData())
            {
                EditorUtility.SetDirty(tuning);
                AssetDatabase.SaveAssets();
                Debug.Log("[Step1] Tuning asset upgraded to data version " + MarioMindTuningSO.CurrentDataVersion);
            }
            return tuning;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Step1")) AssetDatabase.CreateFolder("Assets/Resources", "Step1");
        tuning = ScriptableObject.CreateInstance<MarioMindTuningSO>();
        tuning.dataVersion = MarioMindTuningSO.CurrentDataVersion;
        AssetDatabase.CreateAsset(tuning, TuningAssetPath);
        AssetDatabase.SaveAssets();
        return tuning;
    }

    public static GameObject Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Build only in edit mode");
        var tuning = EnsureTuningAsset();
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = AsciiLevelGenerator.GenerateFromTemplate(RoomAscii, true, false);
        if (root == null) throw new InvalidOperationException("Generator returned no root");
        root.name = "Step1_PrankRoom";
        PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
        GameplayLoopSceneBootstrapper.EnsureGameplayLoopServices(root);
        GameplayLoopSceneBootstrapper.EnsureCombatRoomSemantics(root);

        var mario = Object.FindObjectOfType<MarioController>();
        var trickster = Object.FindObjectOfType<TricksterController>();
        var gm = Object.FindObjectOfType<GameManager>();
        var level = Object.FindObjectOfType<LevelManager>();
        if (mario == null || trickster == null || gm == null) throw new InvalidOperationException("Playable environment incomplete");

        ConfigureRules(gm, tuning);
        ConfigureFireTraps(root);
        ConfigureBridge(root, tuning);
        ConfigureBlockers(root, tuning);
        ConfigureTrickster(trickster);
        ConfigureMario(mario, tuning);
        ConfigureLives(gm.gameObject, tuning, trickster, level != null ? level.TricksterSpawn : null);
        gm.gameObject.AddComponent<Step1PlaytestLog>();
        gm.gameObject.AddComponent<Step1RoomReset>().SetBuiltVersion(BuilderVersion);
        var handsOff = gm.gameObject.AddComponent<Step1HandsOffCheck>();
        var handsOffSo = new SerializedObject(handsOff);
        handsOffSo.FindProperty("tuning").objectReferenceValue = tuning;
        handsOffSo.ApplyModifiedPropertiesWithoutUndo();
        var screen = gm.gameObject.AddComponent<Step1Screen>();
        var screenSo = new SerializedObject(screen);
        screenSo.FindProperty("tuning").objectReferenceValue = tuning;
        screenSo.ApplyModifiedPropertiesWithoutUndo();
        ConfigureCamera(tuning, mario, trickster);
        AddSigns(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return root;
    }

    private static void ConfigureRules(GameManager gm, MarioMindTuningSO tuning)
    {
        var so = new SerializedObject(gm);
        so.FindProperty("useTimer").boolValue = true;
        so.FindProperty("levelTimeLimit").floatValue = tuning.roundTimeLimit;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>火焰陷阱平时不喷火：只有捣蛋者触发才危险（第 1 步 H10：无人干扰时马里奥应能通关）。</summary>
    public static int ConfigureFireTraps(GameObject root)
    {
        int count = 0;
        foreach (var fire in root.GetComponentsInChildren<FireTrap>(true))
        {
            var so = new SerializedObject(fire);
            so.FindProperty("coolOffDuration").floatValue = IdleSafeFireCoolOff;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    /// <summary>
    /// S183：崩塌桥 = 玩家的机关，马里奥踩上去不会自己塌（否则变慢后的马里奥会自己掉坑，违背 H10）；
    /// 桥下有人时推迟重生（H9：不能把马里奥封在坑里）。
    /// </summary>
    public static int ConfigureBridge(GameObject root, MarioMindTuningSO tuning)
    {
        int count = 0;
        foreach (var bridge in root.GetComponentsInChildren<CollapsingPlatform>(true))
        {
            var so = new SerializedObject(bridge);
            so.FindProperty("collapseOnStep").boolValue = false;
            so.FindProperty("waitForClearBelow").boolValue = true;
            so.FindProperty("clearBelowDepth").floatValue = tuning.bridgeRespawnClearDepth;
            so.FindProperty("clearBelowMarginX").floatValue = tuning.bridgeRespawnClearMarginX;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    /// <summary>S183：封路墙挡得更久，让"拦住他"真的有效（数值来自调参资产）。</summary>
    public static int ConfigureBlockers(GameObject root, MarioMindTuningSO tuning)
    {
        int count = 0;
        foreach (var blocker in root.GetComponentsInChildren<ControllableBlocker>(true))
        {
            var so = new SerializedObject(blocker);
            var active = so.FindProperty("activeDuration");
            if (active == null) continue;
            active.floatValue = tuning.blockerActiveSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    private static void ConfigureTrickster(TricksterController trickster)
    {
        var ability = trickster.GetComponent<TricksterAbilitySystem>();
        if (ability == null) return;
        var so = new SerializedObject(ability);
        so.FindProperty("controlRange").floatValue = TricksterControlRange;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMario(MarioController mario, MarioMindTuningSO tuning)
    {
        var driver = mario.gameObject.AddComponent<MarioMindDriver>();
        var so = new SerializedObject(driver);
        so.FindProperty("tuning").objectReferenceValue = tuning;
        so.ApplyModifiedPropertiesWithoutUndo();
        var label = new GameObject("MarioMindLabel");
        label.transform.SetParent(mario.transform, false);
        label.AddComponent<MarioMindLabel>();
        var cone = new GameObject("MarioVisionCone");
        cone.transform.SetParent(mario.transform, false);
        cone.AddComponent<LineRenderer>();
        cone.AddComponent<MarioVisionConeView>();
    }

    private static void ConfigureLives(GameObject managers, MarioMindTuningSO tuning, TricksterController trickster, Transform spawn)
    {
        var lives = managers.AddComponent<TricksterLives>();
        var so = new SerializedObject(lives);
        so.FindProperty("tuning").objectReferenceValue = tuning;
        so.FindProperty("trickster").objectReferenceValue = trickster;
        so.FindProperty("respawnPoint").objectReferenceValue = spawn;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureCamera(MarioMindTuningSO tuning, MarioController mario, TricksterController trickster)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        var follow = cam.GetComponent<CameraController>();
        if (follow != null) follow.enabled = false;
        var room = cam.gameObject.AddComponent<Step1RoomCamera>();
        var so = new SerializedObject(room);
        so.FindProperty("tuning").objectReferenceValue = tuning;
        so.FindProperty("mario").objectReferenceValue = mario.transform;
        so.FindProperty("trickster").objectReferenceValue = trickster.transform;
        // 格子中心在整数坐标，房间外沿 = [-0.5, 宽-0.5] x [-0.5, 高-0.5]
        so.FindProperty("roomBounds").rectValue = new Rect(-0.5f, -0.5f, Room[0].Length, Room.Length);
        so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.position = new Vector3(Room[0].Length * 0.5f - 0.5f, Room.Length * 0.5f - 0.5f, -10f);
        cam.orthographicSize = Room.Length * 0.5f + tuning.cameraPadding;
    }

    private static void AddSigns(GameObject root)
    {
        // S182：只留两块方向牌（中英），玩法说明改为开局帮助页（H 键）。S184：位置从模板里的 G / o 算出，不写死坐标。
        Vector2 exit = CellOf('G'), loot = CellOf('o');
        AddSign(root, exit + new Vector2(1f, SignHeightAboveMarker), "\u2190 出口 EXIT", 0.06f);
        AddSign(root, loot + new Vector2(-1f, SignHeightAboveMarker), "宝物 LOOT \u2192", 0.06f);
    }

    private const float SignHeightAboveMarker = 2.6f;

    /// <summary>模板里某个字符的世界坐标（格子中心）。</summary>
    public static Vector2 CellOf(char c)
    {
        for (int row = 0; row < Room.Length; row++)
        {
            int col = Room[row].IndexOf(c);
            if (col >= 0) return new Vector2(col, Room.Length - 1 - row);
        }
        throw new InvalidOperationException("Room has no '" + c + "'");
    }

    private static void AddSign(GameObject root, Vector2 position, string text, float size)
    {
        var sign = new GameObject("Step1_Sign");
        sign.transform.SetParent(root.transform);
        sign.transform.position = position;
        var label = sign.AddComponent<TextMesh>();
        label.text = text; label.fontSize = 48; label.characterSize = size;
        label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.color = new Color(1f, 1f, 1f, 0.8f);
    }
}
