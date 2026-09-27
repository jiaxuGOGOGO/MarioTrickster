using System;
using System.Collections.Generic;
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
    /// S185 = 6：连招系统（Step1Combo）+ 伪装融入时间来自调参。
    /// S187 = 7：大炮（K/k）+ 场景摆件（箱子/草丛）+ 每回合随机布局 + 游乐园主题配色。
    /// S189 = 9：死局分析进构建检查 + 运行时防卡死救援 + 支持关卡工坊的自定义房间。
    /// S188 = 8：摆放检查（ElementCatalog）+ V 键元素标签 + 构建时移除第 1 步不用的旧系统（减法）；宝物区炮口让开 3 格。
    /// </summary>
    /// S193 = 10：连招手感（顿帧/震屏/段位/递减硬直）+ 弹簧板 J + 裂缝地板 x（房间加入一个浅地下室）+ 香蕉皮 n。
    /// </summary>
    public const int BuilderVersion = 10;
    /// <summary>

    // 行 0 在最上面；世界 y = 高度 - 1 - 行号；地面为 y0..y2，站立层 y3。
    // S184（用户反馈"地图太小、博弈空间不够"）：36×10 → 48×12，分三区（放松区 / 中区 / 宝物区，宪法 P3）：
    //   - 两道高墙 x16/x31 只在地面留 1 格门洞，门洞里是封路墙 '['（关门 = 整条路被截断 3.5 秒）；
    //     高墙挡视线：马里奥在一区看不到二区台子上的你（藏身/换位空间）。
    //   - 地面 1 格高的箱子 '#'（x12、x40）挡低处视线：蹲在箱子后面不会被看见；马里奥会跳过去。
    //   - 崩塌桥 x21..24 + 坑（桥下有人不重生，x25 单向台面可从坑里跳出）。
    //   - 三把火 x9 / x28 / x37（平时安全，只有你能点）。
    //   S187：数字是"随机槽位"（见 Step1Layout.Slots）：1 = 箱子或草丛，2 = 草丛或空，3 = 火或空；
    //   K = 朝右大炮（二区，捣蛋者出生点旁），k = 朝左大炮（宝物区）；b = 固定草丛。
    //   每回合按种子重新生成一次房间布局（可达性对每个组合都有测试）。
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
        "W.G.M..2.~..1...[.K.T...n..b3..[..1..~2...k.o..W",
        "W############J#######CCCC-#############xxx#####W",
        "W####################..~..#############.-.#####W",
        "W##############################################W"
    };

    // ── S189：自定义房间（关卡工坊"作为第 1 步房间试玩"写入）──────────────
    public const string CustomRoomPath = "Assets/Levels/Step1CustomRoom.txt";
    public const string UseCustomKey = "MarioTrickster.Step1.UseCustomRoom";

    public static bool UseCustomRoom
    {
        get => EditorPrefs.GetBool(UseCustomKey, false) && File.Exists(CustomRoomPath);
        set => EditorPrefs.SetBool(UseCustomKey, value);
    }

    /// <summary>当前要构建的房间：启用了自定义房间且文件存在 → 自定义；否则默认 Room。</summary>
    public static string[] Current
    {
        get
        {
            if (!UseCustomRoom) return Room;
            var rows = File.ReadAllText(CustomRoomPath).Replace("\r", "").Split('\n')
                .Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
            return rows.Length > 0 ? rows : Room;
        }
    }

    public static string RoomHash(string[] room) => Hash128.Compute(string.Join("\n", room)).ToString();

    public static void SaveCustomRoom(string[] rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CustomRoomPath));
        File.WriteAllText(CustomRoomPath, "# Step1 custom room (Level Workshop). Digits 1/2/3 = random slots.\n" + string.Join("\n", rows) + "\n");
        AssetDatabase.ImportAsset(CustomRoomPath);
    }

    /// <summary>槽位全部当空气（生成主体用）。</summary>
    public static string BaseAscii => BaseAsciiOf(Room);

    public static string BaseAsciiOf(string[] room)
    {
        {
            var rows = new string[room.Length];
            for (int r = 0; r < room.Length; r++)
            {
                var row = room[r].ToCharArray();
                for (int c = 0; c < row.Length; c++) if (Step1Layout.Slots.ContainsKey(row[c])) row[c] = '.';
                rows[r] = new string(row);
            }
            return string.Join("\n", rows);
        }
    }

    public struct VariantSlot { public Vector2Int cell; public string options; public GameObject[] objects; }

    /// <summary>在每个槽位上生成全部候选元素（行优先，与 Step1Layout.Resolve 同序）。走生成器的片段模式，不改生成器核心。</summary>
    private static List<VariantSlot> SpawnVariantSlots(GameObject root, string[] room)
    {
        var result = new List<VariantSlot>();
        for (int r = 0; r < room.Length; r++)
            for (int c = 0; c < room[r].Length; c++)
            {
                if (!Step1Layout.Slots.TryGetValue(room[r][c], out string options)) continue;
                var cell = new Vector2Int(c, room.Length - 1 - r);
                var objects = new GameObject[options.Length];
                for (int k = 0; k < options.Length; k++)
                {
                    if (options[k] == '.') continue;
                    var temp = AsciiLevelGenerator.GenerateFromTemplate(options[k].ToString(), false, true);
                    if (temp == null) continue;
                    if (temp.transform.childCount > 0)
                    {
                        var child = temp.transform.GetChild(0);
                        child.position = new Vector3(cell.x, cell.y, 0f);
                        child.name = child.name.Replace("_0_0", $"_{cell.x}_{cell.y}");
                        child.SetParent(root.transform, true);
                        objects[k] = child.gameObject;
                    }
                    Object.DestroyImmediate(temp);
                }
                result.Add(new VariantSlot { cell = cell, options = options, objects = objects });
            }
        return result;
    }

    /// <summary>
    /// S188 减法：第 1 步房间不用的旧系统在构建时直接移除（代码保留给其他场景，这里不再运行、不再占屏幕）。
    /// 旧 UGUI 总 HUD、旧起疑/拿宝 HUD、第 0 步锚点起疑追踪与残留提示——第 1 步的马里奥只用 MarioEyes/RushMarioMind。
    /// </summary>
    public static readonly System.Type[] Step1Unused =
    {
        typeof(SuspicionHUD), typeof(LootEscapeHUD), typeof(ResidueVisualHint), typeof(MarioSuspicionTracker)
    };

    public static int StripUnusedLegacy(GameObject managers)
    {
        int removed = 0;
        foreach (var type in Step1Unused)
            foreach (var c in Object.FindObjectsOfType(type))
            { Object.DestroyImmediate(c); removed++; }
        foreach (var canvas in Object.FindObjectsOfType<GlobalGameUICanvas>())
        { Object.DestroyImmediate(canvas.gameObject); removed++; }
        return removed;
    }

    /// <summary>S187：大炮每回合炮弹数来自调参数据。</summary>
    public static int ConfigureCannons(GameObject root, MarioMindTuningSO tuning)
    {
        int count = 0;
        foreach (var cannon in root.GetComponentsInChildren<PranksterCannon>(true))
        {
            var so = new SerializedObject(cannon);
            so.FindProperty("shotsPerRound").intValue = Mathf.Max(0, tuning.cannonShotsPerRound);
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        return count;
    }

    /// <summary>用于验证 / 标牌 / 相机的"代表布局"（种子 0）。</summary>
    public static string[] ResolvedRoom(int seed) => Step1Layout.Resolve(Room, seed);

    public static string RoomAscii => string.Join("\n", ResolvedRoom(0));
    public static string RoomAsciiFor(int seed) => string.Join("\n", ResolvedRoom(seed));

    [MenuItem("MarioTrickster/Step 1/Build Prank Room", false, 10)]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Step 1", "Stop Play Mode first.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string report = ValidateAllVariants(Current, out bool ok);
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
            if (marker != null && marker.BuiltVersion >= BuilderVersion &&
                marker.BuiltTheme == EnsureTuningAsset().themePreset &&
                marker.BuiltRoomHash == RoomHash(Current)) return true;
            Debug.Log("[Step1] Prank room scene is from an older build - rebuilding automatically.");
        }
        string report = ValidateAllVariants(Current, out bool ok);
        if (!ok) { EditorUtility.DisplayDialog("Step 1", "Room invalid (open Level Workshop to see where):\n" + report, "OK"); return false; }
        Build();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();
        return true;
    }

    public static string Validate(out bool ok) => Validate(RoomAscii, out ok);

    /// <summary>S187：把随机槽位的每一种组合都验证一遍（H1：任何一局都可达）。</summary>
    public static string ValidateAllVariants(out bool ok) => ValidateAllVariants(Room, out ok);

    public static string ValidateAllVariants(string[] room, out bool ok)
    {
        var Room = room; // 局部名沿用，便于阅读
        var options = Step1Layout.SlotOptions(Room);
        int total = 1;
        foreach (var o in options) total *= Mathf.Max(1, o.Length);
        for (int n = 0; n < total; n++)
        {
            int rest = n, k = 0;
            var rows = new string[Room.Length];
            for (int r = 0; r < Room.Length; r++)
            {
                var row = Room[r].ToCharArray();
                for (int c = 0; c < row.Length; c++)
                    if (Step1Layout.Slots.TryGetValue(row[c], out string o))
                    { row[c] = o[rest % o.Length]; rest /= o.Length; k++; }
                rows[r] = new string(row);
            }
            string report = Validate(string.Join("\n", rows), out ok);
            if (!ok) return "variant #" + n + ": " + report;
        }
        ok = true;
        return "all " + total + " layout variants valid";
    }

    public static string Validate(string ascii, out bool ok)
    {
        ok = LevelStudioDocument.TryParse(ascii, out var doc, out string error);
        if (!ok) return error;
        error = doc.PlayReadiness();
        if (!string.IsNullOrEmpty(error)) { ok = false; return error; }
        var l1 = AsciiLevelValidator.ValidateTemplate(doc.Grid);
        var l2 = LevelReachabilityAnalyzer.Analyze(doc.Grid);
        // S188：元素说明书的摆放规则（脚下实心、唯一、炮口留空、第 1 步可用）
        var placement = ElementCatalog.PlacementIssues(doc.Grid.Split('\n'), true, AsciiElementRegistry.GetDefault().IsSolid);
        // S189：死局分析（拿宝往返 + 塌桥塌掉后还能不能出去）
        var deadlock = LevelDeadlockAnalyzer.Analyze(doc.Grid.Split('\n'));
        ok = l1.errors.Count == 0 && l2.IsReachable && placement.Count == 0 && !deadlock.HasErrors;
        if (placement.Count > 0) return "Placement: " + string.Join("\n", placement);
        if (deadlock.HasErrors) return "Deadlock: " + string.Join("\n", deadlock.issues.Where(i => i.severity == LevelDeadlockAnalyzer.Severity.Error).Take(5));
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
        // S187：槽位先当空气生成主体，再把每个槽位的所有候选元素摆上（运行时 Step1LayoutVariants 每回合只激活一个）
        var room = Current;
        var root = AsciiLevelGenerator.GenerateFromTemplate(BaseAsciiOf(room), true, false);
        if (root == null) throw new InvalidOperationException("Generator returned no root");
        var variantSlots = SpawnVariantSlots(root, room);
        var theme = ThemePresets.Create(tuning.themePreset);
        if (theme != null) AsciiLevelGenerator.ApplyTheme(theme); // 只换配色/图；空插槽保留白盒
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
        ConfigurePranks(root, tuning);
        ConfigureTrickster(trickster, tuning);
        ConfigureMario(mario, tuning);
        ConfigureLives(gm.gameObject, tuning, trickster, level != null ? level.TricksterSpawn : null);
        gm.gameObject.AddComponent<Step1PlaytestLog>();
        var marker = gm.gameObject.AddComponent<Step1RoomReset>();
        marker.SetBuiltVersion(BuilderVersion);
        marker.SetBuiltTheme(tuning.themePreset);
        marker.SetBuiltRoomHash(RoomHash(room));
        var rescue = gm.gameObject.AddComponent<Step1StuckRescue>();
        rescue.Configure(tuning, Step1Layout.Resolve(room, 0).Select(r => r.Replace('1', '.')).ToArray());
        var variants = gm.gameObject.AddComponent<Step1LayoutVariants>();
        variants.SetTuning(tuning);
        foreach (var slot in variantSlots) variants.AddSlot(slot.cell, slot.options, slot.objects);
        ConfigureCannons(root, tuning);
        var handsOff = gm.gameObject.AddComponent<Step1HandsOffCheck>();
        var handsOffSo = new SerializedObject(handsOff);
        handsOffSo.FindProperty("tuning").objectReferenceValue = tuning;
        handsOffSo.ApplyModifiedPropertiesWithoutUndo();
        gm.gameObject.AddComponent<Step1ElementLabels>();
        StripUnusedLegacy(gm.gameObject);
        var combo = gm.gameObject.AddComponent<Step1Combo>();
        var comboSo = new SerializedObject(combo);
        comboSo.FindProperty("tuning").objectReferenceValue = tuning;
        comboSo.ApplyModifiedPropertiesWithoutUndo();
        var screen = gm.gameObject.AddComponent<Step1Screen>();
        var screenSo = new SerializedObject(screen);
        screenSo.FindProperty("tuning").objectReferenceValue = tuning;
        screenSo.ApplyModifiedPropertiesWithoutUndo();
        ConfigureCamera(tuning, mario, trickster, room);
        AddSigns(root, room);
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

    /// <summary>S193：弹簧板 / 裂缝地板的数值来自调参资产（宪法 §6）。</summary>
    public static int ConfigurePranks(GameObject root, MarioMindTuningSO tuning)
    {
        int count = 0;
        foreach (var spring in root.GetComponentsInChildren<SpringPad>(true))
        {
            spring.Configure(tuning.springLaunchSpeed, tuning.springForwardPush, tuning.springAirStunSeconds, tuning.springTelegraphSeconds, tuning.springActiveSeconds);
            EditorUtility.SetDirty(spring); count++;
        }
        foreach (var peel in root.GetComponentsInChildren<BananaPeel>(true))
        {
            peel.Configure(tuning.bananaSlideSpeed, tuning.bananaSlipSeconds, tuning.bananaTelegraphSeconds, tuning.bananaActiveSeconds);
            EditorUtility.SetDirty(peel); count++;
        }
        foreach (var crack in root.GetComponentsInChildren<CrackFloor>(true)) { crack.Configure(tuning.crackTelegraphSeconds); EditorUtility.SetDirty(crack); count++; }
        return count;
    }

    private static void ConfigureTrickster(TricksterController trickster, MarioMindTuningSO tuning)
    {
        var ability = trickster.GetComponent<TricksterAbilitySystem>();
        if (ability == null) return;
        var so = new SerializedObject(ability);
        so.FindProperty("controlRange").floatValue = TricksterControlRange;
        so.ApplyModifiedPropertiesWithoutUndo();
        var disguise = trickster.GetComponent<DisguiseSystem>();
        if (disguise != null)
        {
            var dso = new SerializedObject(disguise);
            dso.FindProperty("blendInTime").floatValue = tuning.disguiseBlendSeconds;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }
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

    private static void ConfigureCamera(MarioMindTuningSO tuning, MarioController mario, TricksterController trickster, string[] room)
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        var follow = cam.GetComponent<CameraController>();
        if (follow != null) follow.enabled = false;
        var roomCam = cam.gameObject.AddComponent<Step1RoomCamera>();
        var so = new SerializedObject(roomCam);
        so.FindProperty("tuning").objectReferenceValue = tuning;
        so.FindProperty("mario").objectReferenceValue = mario.transform;
        so.FindProperty("trickster").objectReferenceValue = trickster.transform;
        // 格子中心在整数坐标，房间外沿 = [-0.5, 宽-0.5] x [-0.5, 高-0.5]
        so.FindProperty("roomBounds").rectValue = new Rect(-0.5f, -0.5f, room[0].Length, room.Length);
        so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.position = new Vector3(room[0].Length * 0.5f - 0.5f, room.Length * 0.5f - 0.5f, -10f);
        cam.orthographicSize = room.Length * 0.5f + tuning.cameraPadding;
    }

    private static void AddSigns(GameObject root, string[] room)
    {
        // S182：只留两块方向牌（中英），玩法说明改为开局帮助页（H 键）。S184：位置从模板里的 G / o 算出，不写死坐标。
        Vector2 exit = CellOf(room, 'G'), loot = CellOf(room, 'o');
        AddSign(root, exit + new Vector2(1f, SignHeightAboveMarker), "\u2190 出口 EXIT", 0.06f);
        AddSign(root, loot + new Vector2(-1f, SignHeightAboveMarker), "宝物 LOOT \u2192", 0.06f);
    }

    private const float SignHeightAboveMarker = 2.6f;

    /// <summary>模板里某个字符的世界坐标（格子中心）。</summary>
    public static Vector2 CellOf(char c) => CellOf(Room, c);

    public static Vector2 CellOf(string[] Room, char c)
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
