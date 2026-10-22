using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>世界乐园主会话；探索与横版片段都提交真实活动结果，旧守宝只作为片段适配器。</summary>
public sealed class WorldParkRuntime : MonoBehaviour
{
    public const string SceneStory = "story", SceneRoom = "room", SceneExplore = "explore";
    public static WorldParkPack.Session ActiveSession => session;
    public static string ExploreRouteKey(string venue, string node) => "@explore/"+venue+"/"+node;
    [SerializeField] private string sceneRole, storyScenePath, roomScenePath;
    [SerializeField] private string[] roomKeys, roomPaths;
    [SerializeField, TextArea(3, 12)] private string packJson;
    [SerializeField] private bool resumeAtLaunch;
    // S254 Windows 独立版：剧情场景启动时先显示标题页（继续旅程 / 开始新旅程 / 设置 / 退出）。编辑器“开始世界”生成的场景为 false，直接开始。
    [SerializeField] private bool titleAtLaunch;
    private static string checkpointMessage = "";
    private static WorldParkPack.Session session;
    private static string configuredStoryScene, fault, lastLocalResult;
    private static readonly Dictionary<string, string> routes = new Dictionary<string, string>(StringComparer.Ordinal);
    private static bool ended;
    // S254 标题页与暂停菜单：titleRequested = “回到标题”后重新载入剧情场景时显示标题页。
    private static bool titleRequested, menuOpen, menuSettings, titleSettings, titleConfirmNew;
    private static string titleMessage = "";
    private bool showingTitle, titleHasSave;
    private string titleSaveLine = "";
    private WorldParkPack.Pack titlePack;
    private GameManager subscribedManager;
    private bool roomOutcomeHandled, roomWon;
    private Vector2 panelScroll;
    private GUIStyle panelStyle, wrapStyle, titleStyle, smallStyle;
    // S251 博弈阶段：选择只在内存里逐轮累积；整局结束并确认后才 ResolveGame（与检查点规则一致：中途不存档）。
    private static readonly List<string> gameChoices = new List<string>();
    private static string gameKey = "", gameNumber = "", gameError = "";
    // S252 演出：每个会场只亮灯一次、每个结局只盖一次章（继续旅程时会重新亮灯）。
    private static string announcedVenue = "", announcedEnding = "";
    // S252 博弈桌：亮牌节奏（unscaledTime）、提交前的状态（算本轮得失）、等亮牌演完再放的出局激光与结果演出。
    private static float revealAt = -1f;
    private static BorderlandGame.State gameBefore;
    private static readonly List<string> pendingOut = new List<string>();
    private static WorldParkStageCues.Cue pendingActivity;
    private static int flipSeen;
    private static bool resultSounded;
    // S252 剧情字幕卡：当前阶段从何时开始出字（unscaledTime；亮灯/盖章演出期间不计时）、已为几张反转卡放过音效。
    private static string sceneKey = "";
    private static float sceneAt;
    private static int twistsSounded;

    public void ConfigureScene(string role, string storyPath, string roomPath, string json, string[] keys = null, string[] paths = null, bool resume = false, bool title = false)
    {
        resumeAtLaunch = resume;
        titleAtLaunch = title;
        sceneRole = role ?? ""; storyScenePath = storyPath ?? ""; roomScenePath = roomPath ?? ""; packJson = json ?? "";
        roomKeys = keys == null ? new string[0] : (string[])keys.Clone();
        roomPaths = paths == null ? new string[0] : (string[])paths.Clone();
    }

    public static bool Begin(WorldParkPack.Pack pack, string storyPath, string roomPath, out string error, string[] keys = null, string[] paths = null, bool resume = false)
    {
        ClearSession();
        if (resume)
        {
            if (!WorldParkSaveStore.TryLoad(pack, out session, out error)) return false;
            checkpointMessage = error;
        }
        else session = WorldParkPack.Start(pack, out error);
        if (session == null) return false;
        configuredStoryScene = storyPath ?? "";
        if (!SetRoutes(roomPath, keys, paths, out error)) { ClearSession(); return false; }
        return true;
    }

    private static bool SetRoutes(string fallback, string[] keys, string[] paths, out string error)
    {
        routes.Clear(); error = "";
        if ((keys == null ? 0 : keys.Length) != (paths == null ? 0 : paths.Length)) { error = "房间路由长度不一致，请重新生成世界场景。"; return false; }
        if (keys != null) for (int i = 0; i < keys.Length; i++)
        {
            string key = (keys[i] ?? "").Trim(), path = paths[i] ?? "";
            if (key.Length == 0 || path.Length == 0 || routes.ContainsKey(key)) { error = "房间路由为空或重复，请重新生成世界场景。"; return false; }
            routes.Add(key, path);
        }
        if (routes.Count == 0 && !string.IsNullOrEmpty(fallback)) routes.Add("默认房间", fallback);
        return true;
    }

    public static void ClearSession()
    {
        bool owned = session != null || ended || !string.IsNullOrEmpty(configuredStoryScene) || !string.IsNullOrEmpty(fault);
        session = null; configuredStoryScene = fault = lastLocalResult = checkpointMessage = ""; routes.Clear(); ended = false;
        ResetGameTable("");
        sceneKey = ""; twistsSounded = 0;
        announcedVenue = announcedEnding = "";
        menuOpen = menuSettings = false;
        WorldParkStage.ClearAll();
        if (owned) Time.timeScale = 1f;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRunState() { ClearSession(); titleRequested = titleSettings = titleConfirmNew = false; titleMessage = ""; }
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void InstallPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) ClearSession();
    }
#endif
    private void Start()
    {
        if (sceneRole == SceneStory && session == null && (titleAtLaunch || titleRequested)) { ShowTitle(); return; }
        if (!EnsureSession(out string error)) { SetFault(error); return; }
        if (sceneRole == SceneRoom)
        {
            if (session.Current == null || session.Current.kind != "room") { SetFault("房间场景的当前阶段不是room；已阻止错误运行。"); return; }
            SubscribeRoomResult(); Time.timeScale = 1f;
        }
        else if (sceneRole == SceneExplore)
        {
            if (session.Current == null || session.Current.kind != "explore") { SetFault("探索场景与当前阶段不一致，请从开始世界运行。"); return; }
            Time.timeScale = 1f;
        }
        else
        {
            SaveCheckpoint();
            NavigateCurrent();
        }
    }
    private void Update()
    {
        if (sceneRole == SceneRoom && subscribedManager == null && session != null && string.IsNullOrEmpty(fault)) SubscribeRoomResult();
        if (sceneRole == SceneStory || !string.IsNullOrEmpty(fault)) Time.timeScale = 0f;
        AnnounceStage();
    }

    /// <summary>演出触发只读会话状态：换会场 → 霓虹扑克牌亮灯；到结局 → GAME CLEAR / GAME OVER / 旅程终点。</summary>
    private static void AnnounceStage()
    {
        if (session == null || ended || !string.IsNullOrEmpty(fault)) return;
        WorldParkPack.Venue v = session.Venue; WorldParkPack.Node n = session.Current;
        if (v == null || n == null) return;
        if (announcedVenue != session.venueId)
        {
            announcedVenue = session.venueId; announcedEnding = "";
            WorldParkStage.Show(WorldParkStageCues.Entrance(v));
        }
        if (n.kind == "ending")
        {
            string key = session.venueId + "/" + n.id;
            if (announcedEnding != key) { announcedEnding = key; WorldParkStage.Show(WorldParkStageCues.Ending(v, n)); }
        }
    }
    private bool EnsureSession(out string error)
    {
        error = "";
        if (ended) { error = "本次世界会话已结束，请退出Play后重新运行。"; return false; }
        if (session != null) return true;
        var pack = WorldParkPack.Parse(packJson, out error);
        if (pack == null) { error = "场景中的世界包无法解析：" + error; return false; }
        // Domain Reload后从场景快照的显式选择恢复，坏检查点不会偷偷降级新旅程。
        if (resumeAtLaunch && sceneRole == SceneStory)
        {
            if (!WorldParkSaveStore.TryLoad(pack, out session, out error)) return false;
            checkpointMessage = error;
        }
        else session = WorldParkPack.Start(pack, out error);
        if (session == null) return false;
        configuredStoryScene = storyScenePath ?? "";
        if (!SetRoutes(roomScenePath, roomKeys, roomPaths, out error)) { session = null; return false; }
        return true;
    }
    private void SubscribeRoomResult()
    {
        if (subscribedManager != null || GameManager.Instance == null) return;
        subscribedManager = GameManager.Instance;
        subscribedManager.OnGameOver += OnLocalRoomGameOver;
        GameManager.BlockRoundOverKeys = BlockResultKeys;
        GameManager.RestartOverride = RestartRoom;
    }
    private bool BlockResultKeys() { return roomOutcomeHandled || !string.IsNullOrEmpty(fault); }
    private bool RestartRoom()
    {
        if (roomOutcomeHandled || !string.IsNullOrEmpty(fault) || SceneTransit.Busy) return true;
        GoTo(gameObject.scene.path); return true;
    }
    private void OnDisable()
    {
        if (subscribedManager != null) subscribedManager.OnGameOver -= OnLocalRoomGameOver;
        subscribedManager = null;
        if (GameManager.BlockRoundOverKeys == (Func<bool>)BlockResultKeys) GameManager.BlockRoundOverKeys = null;
        if (GameManager.RestartOverride == (Func<bool>)RestartRoom) GameManager.RestartOverride = null;
        if (Application.isPlaying) Time.timeScale = 1f;
    }
    private void OnApplicationQuit() { ClearSession(); }

    // [AI防坑警告] OnGameOver仍在分发给旧统计/反馈监听器：这里只锁存本地结果，不得在回调内加载场景。
    private void OnLocalRoomGameOver(string winner)
    {
        if (roomOutcomeHandled || session == null || session.Current == null || session.Current.kind != "room") return;
        var outcome = Step1Text.Classify(winner, subscribedManager != null ? subscribedManager.LastRoundReason : "");
        roomWon = Step1Text.PlayerWon(outcome);
        roomOutcomeHandled = true;
        lastLocalResult = Step1Text.Headline(outcome);
    }
    private void ContinueFromResult()
    {
        if (!roomOutcomeHandled || session == null) return;
        if (!session.ResolveRoom(roomWon, out string error)) { SetFault("真实房间结果无法转入流程：" + error); return; }
        SaveCheckpoint();
        GoTo(configuredStoryScene); // 先离开已结算的房间，再由故事场景定位后续阶段（可再次进入任意关联房间）。
    }
    public void CompleteExplore(bool won, string detail)
    {
        if (session == null || session.Current == null || session.Current.kind != "explore") return;
        if (!session.ResolveExplore(won,out string error)) { SetFault(error); return; }
        lastLocalResult = "探索活动："+(won ? "达成" : "未达成")+"。"+detail;
        SaveCheckpoint();
        GoTo(configuredStoryScene);
    }

    // ───────────────────────── S254 标题页 / 重来本会场 / 回到标题 ─────────────────────────

    private void ShowTitle()
    {
        showingTitle = true; titleSettings = titleConfirmNew = false;
        Time.timeScale = 0f;
        titlePack = WorldParkPack.Parse(packJson, out string error);
        if (titlePack == null) { showingTitle = false; SetFault("场景中的世界包无法解析：" + error); return; }
        titleHasSave = WorldParkSaveStore.Exists(titlePack);
        titleSaveLine = "";
        if (titleHasSave)
        {
            if (WorldParkSaveStore.TryLoad(titlePack, out WorldParkPack.Session saved, out string message))
                titleSaveLine = "上次停在：" + (saved.Venue.name ?? saved.venueId) + " · " + (saved.Current.name ?? saved.nodeId);
            else titleSaveLine = message;
        }
    }

    private void DrawTitlePage()
    {
        string a = WorldParkUi.DrawTitle(titlePack == null ? "" : titlePack.name, titlePack == null ? "" : titlePack.premise, titleHasSave, titleSaveLine, titleMessage, ref titleSettings, ref titleConfirmNew);
        if (a == WorldParkUi.NewJourney) StartFromTitle(false);
        else if (a == WorldParkUi.ContinueJourney) StartFromTitle(true);
        else if (a == WorldParkUi.Quit) WorldParkUi.QuitGame();
    }

    private void StartFromTitle(bool resume)
    {
        ClearSession();
        resumeAtLaunch = resume; // 只改本场景运行中的副本，不写回场景文件
        titleRequested = false; titleMessage = "";
        if (!EnsureSession(out string error))
        {
            session = null; titleMessage = (resume ? "没能继续旅程：" : "没能开始旅程：") + error;
            ShowTitle(); return;
        }
        showingTitle = false;
        SaveCheckpoint();
        NavigateCurrent();
    }

    /// <summary>回到标题页：丢掉内存里的会话（检查点文件不动），重新载入剧情场景并显示标题。探索与博弈中途的进度不保存。</summary>
    public void ReturnToTitle()
    {
        string story = !string.IsNullOrEmpty(configuredStoryScene) ? configuredStoryScene : storyScenePath;
        ClearSession();
        titleRequested = true; titleMessage = "";
        GoTo(story);
    }

    /// <summary>
    /// 重来本会场：回到“刚进入当前会场”时（WorldParkProgress.RetryVenue，与网页试玩同语义），重新亮灯入场。
    /// 本会场里已发现的线索与活动结果撤销；其他会场的结果保留。会在下一个安全阶段写入检查点。
    /// </summary>
    public void RestartVenue()
    {
        if (session == null) return;
        var fresh = WorldParkProgress.RetryVenue(session, out string error);
        if (fresh == null) { WorldParkStage.Banner("不能重来本会场：" + error, 3f); return; }
        session = fresh;
        ResetGameTable("");
        lastLocalResult = ""; sceneKey = ""; twistsSounded = 0; announcedVenue = announcedEnding = "";
        menuOpen = menuSettings = false;
        WorldParkStage.ClearAll();
        GoTo(configuredStoryScene); // 剧情场景 Start 会存检查点并定位到本会场入口（探索入口会再进探索场景）
    }

    /// <summary>剧情场景 / 博弈桌的 Esc 菜单。</summary>
    private void DrawStoryMenu()
    {
        var v = session.Venue; var n = session.Current;
        bool safe = n.kind == "story" || n.kind == "reveal" || n.kind == "ending";
        string note = n.kind == "game" ? "博弈进行中：重来本会场或回到标题会丢掉这局已经出过的手（博弈中途不存档）。"
            : safe ? "当前阶段可以保存检查点；回到标题后可以“继续旅程”。" : "";
        string a = WorldParkUi.DrawPauseMenu((v.name ?? v.id) + " · " + (n.name ?? n.id), note, safe, true, ref menuSettings);
        if (a == WorldParkUi.Resume) { menuOpen = menuSettings = false; }
        else if (a == WorldParkUi.Save) { SaveCheckpoint(); WorldParkStage.Banner(checkpointMessage, 2.5f); menuOpen = false; }
        else if (a == WorldParkUi.Retry) RestartVenue();
        else if (a == WorldParkUi.Title) { if (safe) SaveCheckpoint(); ReturnToTitle(); }
        else if (a == WorldParkUi.Quit) { if (safe) SaveCheckpoint(); WorldParkUi.QuitGame(); }
    }

    private void EndJourney()
    {
        if (!SaveCheckpoint()) { WorldParkStage.Banner("检查点没有保存成功：" + checkpointMessage, 3f); return; }
        ReturnToTitle();
    }

    private bool GoTo(string path)
    {
        if (string.IsNullOrEmpty(path) || SceneUtility.GetBuildIndexByScenePath(path) < 0) { SetFault("场景不存在或未启用，请重新运行构建：" + path); return false; }
        Time.timeScale = 1f;
        try { SceneManager.LoadScene(path); return true; }
        catch (Exception ex) { SetFault("场景切换失败：" + ex.Message); return false; }
    }
    private void NavigateCurrent()
    {
        if (session == null || session.Current == null || ended) { SetFault("世界会话没有当前阶段。"); return; }
        var node = session.Current;
        if (node.kind == "explore")
        {
            string key = ExploreRouteKey(session.venueId,node.id);
            if (!routes.TryGetValue(key,out string explorePath)) { SetFault("当前探索会场没有生成场景，请在世界工坊重新开始。"); return; }
            if (sceneRole != SceneExplore) GoTo(explorePath);
            return;
        }
        if (node.kind == "room")
        {
            if (!routes.TryGetValue((node.room ?? "").Trim(), out string path)) { SetFault("此房间没有对应构建场景：" + node.room); return; }
            // 玩家先在流程场景读到当前房间的公开信息/已观察提示，再主动进入物理场景。
            if (sceneRole == SceneStory) { Time.timeScale = 0f; return; }
            GoTo(path); return;
        }
        if (node.kind == "story" || node.kind == "reveal" || node.kind == "ending" || node.kind == "game")
        {
            if (sceneRole != SceneStory) GoTo(configuredStoryScene);
            else Time.timeScale = 0f;
            return;
        }
        SetFault("阶段“" + node.id + "”尚无玩法适配器；草稿不会被当成已通关。");
    }
    private void ContinueNarrative()
    {
        if (session == null) return;
        if (!session.Continue(out string error)) { SetFault(error); return; }
        SaveCheckpoint();
        NavigateCurrent();
    }
    private static bool VenueHasScenes(WorldParkPack.Venue v)
    {
        if (v == null || (v.role != "trickster" && v.role != "player")) return false;
        var pending = new Queue<string>(); var seen = new HashSet<string>(); pending.Enqueue(v.entry);
        while (pending.Count > 0)
        {
            string id = pending.Dequeue(); if (!seen.Add(id)) continue;
            var n = WorldParkPack.GetNode(v, id); if (n == null) return false;
            if (n.kind == "room") { if (!routes.ContainsKey((n.room ?? "").Trim())) return false; pending.Enqueue(n.onWin); pending.Enqueue(n.onLose); }
            else if (n.kind == "explore") { if (!routes.ContainsKey(ExploreRouteKey(v.id,n.id))) return false; pending.Enqueue(n.onWin); pending.Enqueue(n.onLose); }
            else if (n.kind == "game") { pending.Enqueue(n.onWin); pending.Enqueue(n.onLose); }
            else if (n.kind == "story" || n.kind == "reveal") pending.Enqueue(n.next);
            else if (n.kind != "ending") return false;
        }
        return true;
    }
    private void Travel(string id)
    {
        if (session == null || !VenueHasScenes(WorldParkPack.GetVenue(session.pack, id))) { SetFault("该会场缺少已构建的玩法或房间，请回世界窗口检查。"); return; }
        if (!session.Travel(id, out string error)) { SetFault(error); return; }
        SaveCheckpoint();
        NavigateCurrent();
    }
    public static string CheckpointMessage => checkpointMessage;

    public static bool SaveCheckpoint()
    {
        if (session == null || session.Current == null) return false;
        string kind = session.Current.kind;
        if (kind != "story" && kind != "reveal" && kind != "ending") return false;
        bool saved = WorldParkSaveStore.TrySave(session, out checkpointMessage);
        if (!saved) Debug.LogWarning("[WorldPark] " + checkpointMessage);
        return saved;
    }

    public void DrawJourneySummary(GUIStyle textStyle)
    {
        if (session == null) return;
        GUILayout.Label("乐园旅程 · " + session.pack.name, textStyle);
        foreach (var venue in session.pack.venues)
        {
            string result;
            string state = venue.id == session.venueId ? "当前" : "未到达";
            if (session.results.TryGetValue(venue.id, out result)) state = result == "clear" ? "通过" : result == "fail" ? "未通过（事实保留）" : result;
            else if (session.CanTravel(venue.id)) state = "可前往";
            GUILayout.Label(venue.name + " — " + state, textStyle);
        }
        GUILayout.Label(checkpointMessage, textStyle);
        GUILayout.Label("只保存会场入场/剧情/揭示/结局检查点；活动中途位置与计时不存。继续后从上个安全阶段重进，不把未确认结果写成已通过。", textStyle);
    }

    private static void SetFault(string message)
    {
        fault = string.IsNullOrEmpty(message) ? "世界运行已拦截。" : message;
        Time.timeScale = 0f; Debug.LogWarning("[WorldPark] " + fault);
    }
    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(18,18,14,14) };
        wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
        titleStyle = new GUIStyle(wrapStyle) { fontStyle = FontStyle.Bold, fontSize = 19 };
        smallStyle = new GUIStyle(wrapStyle) { fontSize = 12 };
    }
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (showingTitle && sceneRole == SceneStory && string.IsNullOrEmpty(fault)) { GUI.depth = -100; DrawTitlePage(); return; }
        if (!Application.isPlaying || ((sceneRole == SceneRoom && !roomOutcomeHandled || sceneRole == SceneExplore) && string.IsNullOrEmpty(fault))) return;
        EnsureStyles(); GUI.depth = -100;
        // S254 Esc 菜单：剧情字幕卡与博弈桌（全屏层）共用。菜单打开时下面只在重绘事件里画：看得见、不接收输入。
        bool full = string.IsNullOrEmpty(fault) && !ended && sceneRole == SceneStory && session != null && session.Current != null;
        Event e = Event.current;
        if (full && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && !WorldParkStage.Busy)
        {
            if (menuOpen && menuSettings) menuSettings = false; else { menuOpen = !menuOpen; menuSettings = false; }
            e.Use();
        }
        if (!full) menuOpen = false;
        bool live = !menuOpen || e.type == EventType.Repaint;
        if (string.IsNullOrEmpty(fault) && !ended && sceneRole != SceneRoom && session != null && session.Current != null && session.Current.kind == "game")
        {
            if (live) DrawGameTable(session.Venue, session.Current);
            if (menuOpen && session != null) DrawStoryMenu();
            return;
        }
        if (full)
        {
            string k = session.Current.kind;
            if (k == "story" || k == "reveal" || k == "ending" || k == "room")
            {
                if (live) DrawSceneCard();
                if (menuOpen && session != null) DrawStoryMenu();
                return;
            }
        }
        Matrix4x4 hudMatrix = WorldParkUi.BeginHud(out float hudW, out float hudH);
        GUILayout.BeginArea(new Rect(24,24,Mathf.Max(220,Mathf.Min(hudW-48,760)),Mathf.Max(160,hudH-48)),panelStyle);
        panelScroll = GUILayout.BeginScrollView(panelScroll);
        GUILayout.Label("世界乐园 · 可接续的旅程",titleStyle);
        if (ended) GUILayout.Label("本次运行已结束。已保存的安全检查点仍保留；下次可继续旅程。",wrapStyle);
        else if (!string.IsNullOrEmpty(fault))
        {
            GUILayout.Label("运行已拦截："+fault+"\n作者数据没有被改写。退出Play后在世界工坊修正当前会场（独立版：回到标题或退出游戏）。",wrapStyle);
            if (sceneRole == SceneStory || !string.IsNullOrEmpty(storyScenePath)) if (GUILayout.Button("回到标题", GUILayout.Height(32))) ReturnToTitle();
            if (GUILayout.Button(Application.isEditor ? "退出 Play" : "退出游戏", GUILayout.Height(32))) WorldParkUi.QuitGame();
        }
        else if (sceneRole == SceneRoom && roomOutcomeHandled)
        {
            GUILayout.Label("本回合真实结果："+lastLocalResult,wrapStyle);
            GUILayout.Label("该结果按原守宝规则产生，剧情不会重新判胜负。",wrapStyle);
            if (GUILayout.Button("确认结果，继续世界流程",GUILayout.Height(34))) ContinueFromResult();
        }
        else if (session != null && session.Current != null)
        {
            var v = session.Venue; var n = session.Current;
            GUILayout.Label((v.name ?? v.id)+" / "+(n.name ?? n.id),titleStyle);
            if (!string.IsNullOrEmpty(v.rules)) GUILayout.Label("公开条款："+v.rules,wrapStyle);
            if (!string.IsNullOrEmpty(lastLocalResult)) GUILayout.Label("上个活动实际结果："+lastLocalResult,smallStyle);
            GUILayout.Space(8); GUILayout.Label(n.text ?? "",wrapStyle);
            foreach (var c in v.clues)
            {
                string key = v.id+"/"+c.id;
                if (session.found.Contains(key)) GUILayout.Label("已观察 · "+c.name+"："+c.hint,wrapStyle);
                if (session.revealed.Contains(key)) GUILayout.Label("重新解释 · "+c.name+"："+c.truth,wrapStyle);
            }
            if (n.kind == "story" || n.kind == "reveal")
            {
                if (GUILayout.Button("继续",GUILayout.Height(34))) ContinueNarrative();
            }
            else if (n.kind == "room")
            {
                GUILayout.Label("这里仅给出当前可知信息；进入后沿用原房间准备/开始流程。",smallStyle);
                if (GUILayout.Button("读完提示，进入房间",GUILayout.Height(34)))
                {
                    if (routes.TryGetValue((n.room ?? "").Trim(),out string roomPath)) GoTo(roomPath);
                    else SetFault("房间场景缺失，请重新构建："+n.room);
                }
            }
            else if (n.kind == "ending")
            {
                GUILayout.Label("本场结算："+n.result+"（作者定义的会场结果；不覆盖实际房间结果）",wrapStyle);
                foreach (var l in session.pack.links)
                {
                    if (l.from != session.venueId) continue;
                    var target = WorldParkPack.GetVenue(session.pack,l.to);
                    bool enabled = session.CanTravel(l.to) && VenueHasScenes(target);
                    using (new GUIEnabledScope(enabled)) if (GUILayout.Button("前往 "+(target == null ? l.to : target.name)+(enabled ? "" : "（条件未满足 / 待适配）"))) Travel(l.to);
                }
                if (GUILayout.Button("保存检查点，回到标题")) EndJourney();
            }
            else if (n.kind == "game") GUILayout.Label("博弈桌只在世界剧情场景里打开。", wrapStyle);
        }
        GUILayout.Space(8);
        DrawJourneySummary(smallStyle);
        if (session != null && session.Current != null && (session.Current.kind == "story" || session.Current.kind == "reveal" || session.Current.kind == "ending"))
            if (GUILayout.Button("保存当前检查点")) SaveCheckpoint();
        GUILayout.EndScrollView(); GUILayout.EndArea();
        GUI.matrix = hudMatrix;
    }
    private sealed class GUIEnabledScope : IDisposable
    {
        private readonly bool old;
        public GUIEnabledScope(bool enabled) { old = GUI.enabled; GUI.enabled = old && enabled; }
        public void Dispose() { GUI.enabled = old; }
    }

    /// <summary>
    /// S252 剧情 / 揭示 / 结局 / 房间前：全屏字幕卡（WorldParkSceneModel 排版，WorldParkScene 绘制）。
    /// 流程推进仍走原来的 ContinueNarrative / Travel / SaveCheckpoint / GoTo，规则与存档语义不变。
    /// </summary>
    private void DrawSceneCard()
    {
        var sc = WorldParkSceneModel.Build(session, VenueHasScenes);
        string key = session.venueId + "/" + session.nodeId;
        float now = Time.unscaledTime;
        if (key != sceneKey) { sceneKey = key; sceneAt = now; twistsSounded = 0; WorldParkScene.showNotes = WorldParkScene.showJourney = false; }
        if (WorldParkStage.Busy) sceneAt = now; // 亮灯 / 盖章演完再开始出字
        float t = now - sceneAt;
        int shown = 0;
        for (int i = 0; i < sc.twists.Count; i++) if (t >= WorldParkSceneModel.TwistAt(sc, i)) shown++;
        if (shown > twistsSounded) { twistsSounded = shown; WorldParkSfx.Play("beep_last", 0.5f); }
        // S254：GAME OVER 结局底栏多一个“重来本会场”（与网页试玩一致）。
        WorldParkScene.offerRetry = session.Current.kind == "ending" && WorldParkStageCues.Ending(session.Venue, session.Current).kind == "gameover";
        string action = WorldParkScene.Draw(sc, t, lastLocalResult, checkpointMessage);
        if (action == null) return;
        if (action == WorldParkScene.Skip) { sceneAt = now - (float)WorldParkSceneModel.ReadyAt(sc) - 0.01f; return; }
        if (action == WorldParkScene.Save) { SaveCheckpoint(); return; }
        if (action == WorldParkScene.End) { EndJourney(); return; }
        if (action == WorldParkScene.Retry) { RestartVenue(); return; }
        if (action.StartsWith(WorldParkScene.TravelPrefix, StringComparison.Ordinal)) { Travel(action.Substring(WorldParkScene.TravelPrefix.Length)); return; }
        if (action != WorldParkScene.Continue) return;
        var n = session.Current;
        if (n.kind == "room")
        {
            if (routes.TryGetValue((n.room ?? "").Trim(), out string roomPath)) GoTo(roomPath);
            else SetFault("房间场景缺失，请重新构建：" + n.room);
        }
        else ContinueNarrative();
    }

    /// <summary>
    /// S252 博弈阶段：全屏博弈桌（WorldParkTableModel 排桌，WorldParkTable 绘制）。
    /// 选择仍只在内存里逐轮累积；整局结束并确认后才 ResolveGame（与检查点规则一致：中途不存档）。
    /// 提交后先逐个亮牌、再出结算字，出局激光与通过/出局演出等亮牌演完才放。
    /// </summary>
    private void DrawGameTable(WorldParkPack.Venue v, WorldParkPack.Node n)
    {
        string key = v.id + "/" + n.id;
        if (gameKey != key) ResetGameTable(key);
        var st = BorderlandGame.Run(n.game, gameChoices);
        if (!string.IsNullOrEmpty(st.error)) { SetFault("博弈数据有误：" + st.error); return; }
        float now = Time.unscaledTime;
        var view = WorldParkTableModel.Build(n.game, gameBefore, st, revealAt < 0f ? -1.0 : now - revealAt);
        if (view.revealing && view.flipped > flipSeen) { flipSeen = view.flipped; WorldParkSfx.Play("beep", 0.4f); }
        if (view.resultShown && !resultSounded && revealAt >= 0f)
        {
            resultSounded = true;
            var rec = st.records.Count > 0 ? st.records[st.records.Count - 1] : null;
            WorldParkSfx.Play(rec != null && rec.correct ? "pass" : "alarm", 0.6f);
        }
        if ((view.resultShown || !view.revealing) && pendingOut.Count > 0)
        {
            foreach (string name in pendingOut) WorldParkStage.Show(WorldParkStageCues.Eliminated(name));
            pendingOut.Clear();
        }
        if (!view.revealing && pendingActivity != null) { WorldParkStage.Show(pendingActivity); pendingActivity = null; }
        string action = WorldParkTable.Draw(view, WorldParkStageCues.Card(v), v.name ?? v.id, WorldParkStageCues.Tone(v) == "red", v.rules, st.transcript, ref gameNumber);
        if (!string.IsNullOrEmpty(gameError)) { WorldParkStage.Banner(gameError, 2.5f); gameError = ""; }
        if (action == null) return;
        if (action == WorldParkTable.Skip)
        {
            // 第一次跳到结算字，第二次跳过结算停留。
            double jumpTo = view.phase == "reveal" ? view.revealLength - WorldParkTableModel.ResultHold : view.revealLength + 0.01;
            revealAt = now - (float)jumpTo;
        }
        else if (action == WorldParkTable.Confirm) ConfirmGame(st);
        else SubmitGameChoice(n, action);
    }

    private static void ResetGameTable(string key)
    {
        gameKey = key ?? ""; gameChoices.Clear(); gameNumber = gameError = "";
        revealAt = -1f; gameBefore = null; pendingOut.Clear(); pendingActivity = null; flipSeen = 0; resultSounded = false;
    }

    private void ConfirmGame(BorderlandGame.State st)
    {
        if (!session.ResolveGame(new List<string>(gameChoices), out string error)) { SetFault("博弈结果无法转入流程：" + error); return; }
        lastLocalResult = "博弈：" + (st.won ? "通过" : "失败") + "（" + st.round + " 轮）";
        ResetGameTable("");
        SaveCheckpoint();
        NavigateCurrent();
    }

    private void SubmitGameChoice(WorldParkPack.Node n, string choice)
    {
        var before = BorderlandGame.Run(n.game, gameChoices);
        var trial = new List<string>(gameChoices) { (choice ?? "").Trim() };
        var st = BorderlandGame.Run(n.game, trial);
        if (!string.IsNullOrEmpty(st.error)) { gameError = "这个选择无效：" + st.error; return; }
        gameChoices.Add((choice ?? "").Trim());
        gameNumber = gameError = "";
        WorldParkSfx.Play("beep", 0.5f);
        gameBefore = before; revealAt = Time.unscaledTime; flipSeen = 0; resultSounded = false;
        pendingOut.Clear();
        pendingOut.AddRange(WorldParkStageCues.NewlyEliminated(n.game, before, st));
        pendingActivity = st.finished ? WorldParkStageCues.Activity(session == null ? null : session.Venue, st.won, st.won ? "博弈通过" : "你输掉了这场博弈") : null;
    }
}
