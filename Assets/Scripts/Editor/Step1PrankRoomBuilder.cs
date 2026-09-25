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

    // 行 0 在最上面；世界 y = 高度 - 1 - 行号；地面为 y0..y2，坑在 x12..16。
    // x16 的单向台面 "-"：平时可以走过，掉进坑后也能从下面跳穿出来（桥重生后不会把马里奥封死在坑里）。
    public static readonly string[] Room =
    {
        "W..................................W",
        "W..................................W",
        "W..................................W",
        "W..................................W",
        "W.................T................W",
        "W.....---........----....----......W",
        "W.G.M....~..........~[.....~....o..W",
        "W###########CCCC-##################W",
        "W###########.~...##################W",
        "W##################################W"
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
        if (tuning != null) return tuning;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Step1")) AssetDatabase.CreateFolder("Assets/Resources", "Step1");
        tuning = ScriptableObject.CreateInstance<MarioMindTuningSO>();
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
        ConfigureTrickster(trickster);
        ConfigureMario(mario, tuning);
        ConfigureLives(gm.gameObject, tuning, trickster, level != null ? level.TricksterSpawn : null);
        gm.gameObject.AddComponent<Step1PlaytestLog>();
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
        AddSign(root, new Vector2(3f, 8.3f), "< ESCAPE", 0.06f);
        AddSign(root, new Vector2(32f, 8.3f), "LOOT >", 0.06f);
        AddSign(root, new Vector2(17.5f, 8.6f), "STEP 1 - PRANK ROOM\nDisguise (P) next to a prop, trigger it (L). Don't get caught: 3 lives.", 0.05f);
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
