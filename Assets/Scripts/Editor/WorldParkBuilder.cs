using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 为当前世界包构建隔离流程、横版房间和通用探索场景。
/// 不写用户地图/关卡源；构建失败或取消时恢复 Build Settings 与原场景设置。
/// S254：同一套场景生成也用于“构建 Windows 版”（独立 .exe，启动先显示标题页）；构建时显式列出场景，不改 Build Settings。
/// </summary>
public static class WorldParkBuilder
{
    public const string SceneFolder = "Assets/Scenes/WorldPark";
    public const string StoryScenePath = SceneFolder + "/WorldPark_Story.unity";
    /// <summary>Windows 版输出目录（相对项目根，已被 .gitignore 的 /Builds/ 忽略）。</summary>
    public const string WindowsBuildFolder = "Builds/Windows";

    private sealed class ExploreScene
    {
        public string venueId;
        public WorldParkPack.Node node;
        public string key;
        public string path;
    }

    /// <summary>一次场景生成的结果：路由键与场景路径一一对应；requiredScenePaths 第一个是剧情场景。</summary>
    private sealed class Generated
    {
        public WorldParkPack.Pack snapshot;
        public string[] keys, paths, requiredScenePaths;
        public int roomCount, exploreCount;
    }

    [MenuItem("MarioTrickster/开始世界 Play World", false, 0)]
    public static void PlayMain()
    {
        WorldParkWindow.PlayCurrent();
    }

    [MenuItem("MarioTrickster/世界工坊 World Workshop/构建 Windows 版 Build Windows", false, 41)]
    public static void BuildWindowsMenu()
    {
        WorldParkWindow.BuildWindowsCurrent();
    }

    public static string[] ResolveRows(string room)
    {
        string name = (room ?? "").Trim();
        if (name.Length == 0) return null;
        if (name == "默认房间" || name == "default" || name == "Assets/Scenes/Step1_PrankRoom.unity") return Step1PrankRoomBuilder.Room;
        return OverworldBuilder.ResolveRoom(name); // 复用关卡库与内置样板；不读取任意外部路径。
    }

    public static bool RoomExists(string room)
    {
        try
        {
            var rows = ResolveRows(room);
            return rows != null && rows.Length > 0;
        }
        catch { return false; }
    }

    public static WorldParkPack.Pack WithEntry(WorldParkPack.Pack pack, string venue)
    {
        return new WorldParkPack.Pack
        {
            id = pack.id,
            name = pack.name,
            premise = pack.premise,
            v = pack.v,
            entryVenue = venue,
            venues = pack.venues,
            links = pack.links
        };
    }

    /// <summary>只沿当前会场内可执行的分支查找；explore/game 与 room 一样拥有胜负两条出口（game 不需要独立场景）。</summary>
    private static IEnumerable<WorldParkPack.Node> ReachableNodes(WorldParkPack.Venue venue)
    {
        if (venue == null) yield break;
        var queue = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue(venue.entry);
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!seen.Add(id)) continue;
            var node = WorldParkPack.GetNode(venue, id);
            if (node == null) continue;
            yield return node;
            if (node.kind == "room" || node.kind == "explore" || node.kind == "game")
            {
                queue.Enqueue(node.onWin);
                queue.Enqueue(node.onLose);
            }
            else if (node.kind == "story" || node.kind == "reveal")
            {
                queue.Enqueue(node.next);
            }
        }
    }

    private static bool VenueIsReady(WorldParkPack.Pack pack, WorldParkPack.Venue venue)
    {
        if (venue == null || WorldParkPack.Start(WithEntry(pack, venue.id), out string error) == null) return false;
        // explore 的地图、点、线索和出口由核心验证；这里只验证真正会用到的横版房间。
        return ReachableNodes(venue).Where(n => n.kind == "room").All(n => RoomExists(n.room));
    }

    public static bool CheckRunReadiness(WorldParkPack.Pack pack, out string report)
    {
        report = "";
        if (pack == null) { report = "没有世界包。"; return false; }
        if (WorldParkPack.Start(pack, out report) == null) return false;
        var entry = WorldParkPack.GetVenue(pack, pack.entryVenue);
        foreach (var node in ReachableNodes(entry).Where(n => n.kind == "room"))
        {
            try
            {
                var rows = ResolveRows(node.room);
                if (rows == null || rows.Length == 0)
                {
                    report = "找不到房间：" + node.room + "；请先在关卡子工具保存同名关卡。";
                    return false;
                }
                Step1PrankRoomBuilder.ValidateAllVariants(rows, out bool ok);
                if (!ok)
                {
                    report = "房间“" + node.room + "”未通过关卡子工具检查，请修正后再开始世界。";
                    return false;
                }
            }
            catch (Exception ex)
            {
                report = "检查房间失败：" + ex.Message;
                return false;
            }
        }
        report = "入口会场检查通过；探索节点不要求守宝房间或旧小镇日程，场景将在开始时按可达节点生成。";
        return true;
    }

    public static bool TryRun(WorldParkPack.Pack pack, out string report, bool resume = false)
    {
        report = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode) { report = "请先退出 Play。"; return false; }
        if (!CheckRunReadiness(pack, out report)) return false;
        // 恢复文件失败必须在构建/场景改动前返回，不能假装开始了新旅程。
        if (resume && !WorldParkSaveStore.TryLoad(pack, out var restoredPreview, out report)) return false;
        if (!resume && WorldParkSaveStore.Exists(pack) && !EditorUtility.DisplayDialog("开始新旅程", "该世界版本已有检查点。开始新旅程后新的进展将更新检查点（上一文件保留为.previous）。若想继续，请取消后点“继续旅程”。", "开始新旅程", "取消")) { report = "已取消，旧检查点和场景保留。"; return false; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { report = "已取消，未构建场景。"; return false; }

        var originalSetup = EditorSceneManager.GetSceneManagerSetup();
        var originalBuild = EditorBuildSettings.scenes;
        float originalTime = Time.timeScale;
        try
        {
            var g = Generate(pack, resume, false, originalBuild);
            string error;

            var build = EditorBuildSettings.scenes.ToList();
            foreach (string scenePath in g.requiredScenePaths)
            {
                int index = build.FindIndex(scene => scene.path == scenePath);
                if (index < 0) build.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            // S249：世界是主启动体验；保留其他作者场景，但不让旧守宝抢占首场景。
            build.RemoveAll(scene => scene.path == StoryScenePath);
            build.Insert(0, new EditorBuildSettingsScene(StoryScenePath, true));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(StoryScenePath, OpenSceneMode.Single);
            if (!WorldParkRuntime.Begin(g.snapshot, StoryScenePath, "", out error, g.keys, g.paths, resume))
                throw new InvalidOperationException("无法开始会话：" + error);
            EditorApplication.isPlaying = true;
            report = "已生成世界流程、" + g.roomCount + " 张横版房间场景和 " + g.exploreCount + " 张通用探索场景；安全阶段自动保存世界检查点。";
            return true;
        }
        catch (Exception ex)
        {
            WorldParkRuntime.ClearSession();
            EditorBuildSettings.scenes = originalBuild;
            try { EditorSceneManager.RestoreSceneManagerSetup(originalSetup); } catch { }
            Time.timeScale = originalTime;
            report = "世界场景构建失败：" + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 生成本次运行需要的全部场景（剧情 + 可达房间 + 可达探索），返回路由。失败抛异常，由调用方恢复场景设置。
    /// buildSettings 不为 null 时检查作者是否在 Build Settings 里禁用了需要的场景（编辑器试玩用）。
    /// title=true：剧情场景启动先显示标题页（Windows 独立版）。
    /// </summary>
    private static Generated Generate(WorldParkPack.Pack pack, bool resume, bool title, EditorBuildSettingsScene[] buildSettings)
    {
        var snapshot = WorldParkPack.Parse(WorldParkPack.ToJson(pack), out string error);
        if (snapshot == null) throw new InvalidOperationException("无法创建运行快照：" + error);

        var connected = ConnectedVenueIds(snapshot);
        var roomNames = new HashSet<string>(StringComparer.Ordinal);
        var exploreScenes = new List<ExploreScene>();
        foreach (var venue in snapshot.venues ?? new List<WorldParkPack.Venue>())
        {
            if (venue == null || !connected.Contains(venue.id) || !VenueIsReady(snapshot, venue)) continue;
            foreach (var node in ReachableNodes(venue))
            {
                if (node.kind == "room")
                {
                    Step1PrankRoomBuilder.ValidateAllVariants(ResolveRows(node.room), out bool ok);
                    if (!ok) throw new InvalidOperationException("房间未通过变体检查：" + node.room);
                    roomNames.Add((node.room ?? "").Trim());
                }
                else if (node.kind == "explore")
                {
                    string key = WorldParkRuntime.ExploreRouteKey(venue.id, node.id);
                    if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("探索路由键为空：" + venue.id + "/" + node.id);
                    exploreScenes.Add(new ExploreScene
                    {
                        venueId = venue.id,
                        node = node,
                        key = key,
                        path = SceneFolder + "/WorldPark_Explore_" + Hash128.Compute(venue.id + "/" + node.id).ToString() + ".unity"
                    });
                }
            }
        }

        // raw room 名保留旧路由语义；探索永远用 @explore/venue/node，二者不允许冲突。
        string[] roomKeys = roomNames.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        string[] roomPaths = roomKeys.Select(n => SceneFolder + "/WorldPark_Room_" + Hash128.Compute(n).ToString() + ".unity").ToArray();
        exploreScenes = exploreScenes.OrderBy(e => e.key, StringComparer.Ordinal).ToList();
        if (exploreScenes.Select(e => e.key).Distinct(StringComparer.Ordinal).Count() != exploreScenes.Count)
            throw new InvalidOperationException("探索路由键重复，请修正会场或节点 ID。");
        if (roomKeys.Any(key => exploreScenes.Any(explore => string.Equals(key, explore.key, StringComparison.Ordinal))))
            throw new InvalidOperationException("房间名不能与探索路由键冲突。");

        string[] keys = roomKeys.Concat(exploreScenes.Select(e => e.key)).ToArray();
        string[] paths = roomPaths.Concat(exploreScenes.Select(e => e.path)).ToArray();
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
            throw new InvalidOperationException("生成场景路径重复，请修正会场或节点 ID。");

        var requiredScenePaths = new[] { StoryScenePath }.Concat(paths).ToArray();
        if (buildSettings != null)
        {
            var disabled = buildSettings.FirstOrDefault(scene => !scene.enabled && requiredScenePaths.Contains(scene.path));
            if (disabled != null)
                throw new InvalidOperationException("此运行需要的世界场景已被你禁用，请自行启用后再运行：" + disabled.path);
        }

        EnsureFolder();
        string json = WorldParkPack.ToJson(snapshot);
        BuildStory(json, keys, paths, resume, title);
        for (int i = 0; i < roomKeys.Length; i++) BuildRoom(ResolveRows(roomKeys[i]), roomPaths[i], json, keys, paths);
        foreach (var explore in exploreScenes) BuildExplore(explore.path, json, keys, paths);
        return new Generated { snapshot = snapshot, keys = keys, paths = paths, requiredScenePaths = requiredScenePaths, roomCount = roomKeys.Length, exploreCount = exploreScenes.Count };
    }

    /// <summary>默认输出：Builds/Windows/&lt;世界包 id&gt;/&lt;产品名&gt;.exe（项目根下，不进 Git）。</summary>
    public static string DefaultWindowsExe(WorldParkPack.Pack pack)
    {
        string id = pack == null || string.IsNullOrWhiteSpace(pack.id) ? "world" : pack.id.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
        string product = string.IsNullOrWhiteSpace(PlayerSettings.productName) ? "MarioTrickster" : PlayerSettings.productName.Trim();
        foreach (char c in Path.GetInvalidFileNameChars()) product = product.Replace(c, '_');
        string root = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(root, WindowsBuildFolder.Replace('/', Path.DirectorySeparatorChar), id, product + ".exe");
    }

    /// <summary>
    /// S254 构建 Windows 独立版（64 位）：生成世界场景（剧情场景启动先显示标题页），只把这些场景交给构建，不改 Build Settings。
    /// 构建期间临时允许玩家拖动窗口大小，结束后还原 PlayerSettings；结束后恢复原来打开的场景。
    /// interactive=false 用于命令行批处理（不弹保存场景对话框）。
    /// </summary>
    public static bool BuildWindows(WorldParkPack.Pack pack, string exePath, bool interactive, out string report)
    {
        report = "";
        if (EditorApplication.isPlayingOrWillChangePlaymode) { report = "请先退出 Play。"; return false; }
        if (BuildPipeline.isBuildingPlayer) { report = "已经在构建中。"; return false; }
        if (!CheckRunReadiness(pack, out report)) return false;
        if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { report = "已取消，未构建。"; return false; }
        if (string.IsNullOrEmpty(exePath)) exePath = DefaultWindowsExe(pack);

        var originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool resizable = PlayerSettings.resizableWindow;
        try
        {
            var g = Generate(pack, false, true, null);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Path.GetDirectoryName(exePath));
            PlayerSettings.resizableWindow = true;
            var options = new BuildPlayerOptions
            {
                scenes = g.requiredScenePaths,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            BuildReport result = BuildPipeline.BuildPlayer(options);
            var summary = result.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                report = "Windows 版构建失败（" + summary.result + "，错误 " + summary.totalErrors + " 个）：详见 Console 或 Editor.log。";
                return false;
            }
            WriteWindowsReadme(Path.GetDirectoryName(exePath), g.snapshot, Path.GetFileName(exePath));
            report = "已构建 Windows 版：" + exePath + "（" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB，" + g.requiredScenePaths.Length + " 个场景，用时 " + summary.totalTime.TotalSeconds.ToString("0") + " 秒）。双击 exe 先看到标题页。";
            return true;
        }
        catch (Exception ex)
        {
            report = "Windows 版构建失败：" + ex.Message;
            return false;
        }
        finally
        {
            PlayerSettings.resizableWindow = resizable;
            WorldParkRuntime.ClearSession();
            try { if (originalSetup != null && originalSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originalSetup); } catch { }
        }
    }

    private static void WriteWindowsReadme(string folder, WorldParkPack.Pack pack, string exeName)
    {
        try
        {
            string text = "《" + (pack.name ?? pack.id) + "》Windows 版\r\n\r\n"
                + "双击 " + exeName + " 开始：先是标题页（继续旅程 / 开始新旅程 / 设置 / 退出）。\r\n"
                + "操作：WASD / 方向键移动 · E 调查 · N 线索笔记 · 空格 / 回车 继续 · Esc 暂停菜单（设置、重来本会场、回到标题、退出）· Alt+Enter 全屏 / 窗口。\r\n"
                + "设置里可以调界面大小（85%–150%）和音量。\r\n"
                + "存档：会场入场、剧情、揭示、结局时自动保存检查点，存在 %USERPROFILE%\\AppData\\LocalLow\\" + PlayerSettings.companyName + "\\" + PlayerSettings.productName + "\\WorldPark\\。\r\n"
                + "　　　现场探索和博弈的中途不存档；世界内容改了以后，旧检查点不会串用。\r\n"
                + "整个文件夹一起拷给别人就能玩（不要只拷 exe）。\r\n";
            File.WriteAllText(Path.Combine(folder, "先读我.txt"), text, new System.Text.UTF8Encoding(true));
        }
        catch (Exception ex) { Debug.LogWarning("[WorldPark] 写说明文件失败：" + ex.Message); }
    }

    /// <summary>
    /// 命令行批处理入口（根目录 BUILD_WINDOWS_GAME.cmd 调用）：
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod WorldParkBuilder.BuildWindowsBatch [-worldPack 路径] [-worldOut exe路径]
    /// 默认世界：Assets/Resources/WorldPark/MyWorld.worldpark.json（网页设计台写进来的），没有就用主世界 Borderland。
    /// </summary>
    public static void BuildWindowsBatch()
    {
        string packPath = Arg("-worldPack"), outPath = Arg("-worldOut");
        string root = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(packPath))
        {
            string mine = Path.Combine(root, WorldParkWindow.MyWorldAssetPath);
            packPath = File.Exists(mine) ? mine : Path.Combine(root, "Assets/Resources/WorldPark/Borderland.worldpark.json");
        }
        else if (!Path.IsPathRooted(packPath)) packPath = Path.Combine(root, packPath);
        bool ok = false;
        try
        {
            var pack = WorldParkPack.Parse(File.ReadAllText(packPath, System.Text.Encoding.UTF8), out string error);
            if (pack == null) Debug.LogError("[WorldPark] 世界包无法解析：" + packPath + "：" + error);
            else
            {
                ok = BuildWindows(pack, string.IsNullOrEmpty(outPath) ? null : Path.GetFullPath(Path.IsPathRooted(outPath) ? outPath : Path.Combine(root, outPath)), false, out string report);
                if (ok) Debug.Log("[WorldPark] " + report); else Debug.LogError("[WorldPark] " + report);
            }
        }
        catch (Exception ex) { Debug.LogError("[WorldPark] 构建异常：" + ex); }
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    private static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return "";
    }

    private static HashSet<string> ConnectedVenueIds(WorldParkPack.Pack pack)
    {
        var connected = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(pack.entryVenue);
        while (queue.Count > 0)
        {
            string id = queue.Dequeue();
            if (!connected.Add(id)) continue;
            foreach (var link in pack.links ?? new List<WorldParkPack.Link>())
                if (link != null && string.Equals(link.from, id, StringComparison.Ordinal)) queue.Enqueue(link.to);
        }
        return connected;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder("Assets/Scenes", "WorldPark");
    }

    private static void BuildStory(string json, string[] keys, string[] paths, bool resume, bool title)
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("WorldParkRuntime").AddComponent<WorldParkRuntime>()
            .ConfigureScene(WorldParkRuntime.SceneStory, StoryScenePath, "", json, keys, paths, resume, title);
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), StoryScenePath))
            throw new IOException("无法保存世界流程场景。");
    }

    private static void BuildRoom(string[] rows, string path, string json, string[] keys, string[] paths)
    {
        // 不写 Step1CustomRoom.txt、不改 UseCustomKey，也不覆写旧房间场景；临时参数必须恢复。
        string[] old = Step1PrankRoomBuilder.RoomOverride;
        int bombs = Step1PrankRoomBuilder.ExtraBombs;
        try
        {
            Step1PrankRoomBuilder.RoomOverride = rows;
            Step1PrankRoomBuilder.ExtraBombs = 0;
            Step1PrankRoomBuilder.Build();
            new GameObject("WorldParkRuntime").AddComponent<WorldParkRuntime>()
                .ConfigureScene(WorldParkRuntime.SceneRoom, StoryScenePath, "", json, keys, paths);
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path))
                throw new IOException("无法保存隔离房间：" + path);
        }
        finally
        {
            Step1PrankRoomBuilder.RoomOverride = old;
            Step1PrankRoomBuilder.ExtraBombs = bombs;
        }
    }

    private static void BuildExplore(string path, string json, string[] keys, string[] paths)
    {
        // 探索场景只含标准 Camera/AudioListener、世界路由和探索渲染器；不带 OverworldGame、日程、旧自动存档或 AI。
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var runtime = new GameObject("WorldParkRuntime");
        runtime.AddComponent<WorldParkRuntime>()
            .ConfigureScene(WorldParkRuntime.SceneExplore, StoryScenePath, "", json, keys, paths);
        runtime.AddComponent<WorldParkExploreRuntime>(); // 探索组件通过同对象GetComponent取得owner，不可放到另一对象。
        if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path))
            throw new IOException("无法保存通用探索场景：" + path);
    }
}
