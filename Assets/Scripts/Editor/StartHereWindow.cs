using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S236：📖 开始页（Ctrl+Alt+H）。用户诉求："全局把握项目能做什么、怎么设计创作、别让功能做了却被遗忘在角落"。
/// 内容全部来自 FeatureMap（网页"功能地图"页、docs/FEATURE_MAP.md 同一份）。这里只做三件事：
/// ① 「我想…」按目标给步骤 ② 全部功能按区 / 级别分组、能搜 ③ 每一项点「打开」直接到那个窗口 / 菜单。
/// 只读，不改任何关卡和数值。第一次打开 Unity（或升级后版本变了）会自动弹一次，之后不打扰。
/// </summary>
public sealed class StartHereWindow : EditorWindow
{
    private const string SeenKey = "MarioTrickster.StartHere.SeenCount";
    private Vector2 scroll;
    private string search = "";
    private string tier = "全部";
    private bool showGoals = true, showRecent = true;

    [MenuItem("MarioTrickster/📖 开始页 Start Here %&h", false, 0)]
    public static void Open()
    {
        var w = GetWindow<StartHereWindow>("开始页");
        w.minSize = new Vector2(640, 480);
        EditorPrefs.SetInt(SeenKey, FeatureMap.All.Length);
    }

    /// <summary>功能地图有新东西（数量变了）→ 编辑器启动后自动弹一次（升级后提醒"多了什么"）。</summary>
    [InitializeOnLoadMethod]
    private static void OpenOnFirstLaunch()
    {
        if (Application.isBatchMode) return;
        if (EditorPrefs.GetInt(SeenKey, 0) == FeatureMap.All.Length) return;
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Open(); };
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"MarioTrickster 能做什么（{FeatureMap.All.Length} 项，和网页设计台「功能地图」同一份）", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.6f, 0.9f, 1f);
            if (GUILayout.Button("🧪 测试中心", GUILayout.Height(26))) TestHubWindow.Open();
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("▶ 试玩房间", GUILayout.Height(26))) EditorApplication.delayCall += Step1PrankRoomBuilder.PlayMenu;
            if (GUILayout.Button("▶ 试玩小镇", GUILayout.Height(26))) EditorApplication.delayCall += OverworldBuilder.PlayMenu;
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("🏠 关卡工坊", GUILayout.Height(26))) LevelWorkshopWindow.Open();
            if (GUILayout.Button("🏘 小镇工坊", GUILayout.Height(26))) OverworldWorkshopWindow.Open();
            if (GUILayout.Button("✎ 台词", GUILayout.Height(26))) TownStoryEditorWindow.Open();
            if (GUILayout.Button("🌐 网页", GUILayout.Height(26))) OpenWeb();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("🔍 搜", search);
            var tiers = new[] { "全部" }.Concat(FeatureMap.Tiers).ToArray();
            int ti = Mathf.Max(0, System.Array.IndexOf(tiers, tier));
            tier = tiers[GUILayout.Toolbar(ti, tiers, GUILayout.Width(330))];
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        bool filtering = search.Trim().Length > 0 || tier != "全部";
        if (!filtering)
        {
            showGoals = EditorGUILayout.Foldout(showGoals, "我想…（按目标走，点名字打开）", true, EditorStyles.foldoutHeader);
            if (showGoals)
                foreach (var g in FeatureMap.Goals)
                {
                    EditorGUILayout.LabelField("▸ " + g.title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(g.steps, EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(14);
                        foreach (var id in g.ids)
                        {
                            var f = FeatureMap.Get(id); if (f == null) continue;
                            if (GUILayout.Button(new GUIContent(Short(f.name), f.how), EditorStyles.miniButton, GUILayout.MaxWidth(150))) Go(f);
                        }
                        GUILayout.FlexibleSpace();
                    }
                }
            EditorGUILayout.Space();
            showRecent = EditorGUILayout.Foldout(showRecent, "🆕 最近新增", true, EditorStyles.foldoutHeader);
            if (showRecent) foreach (var f in FeatureMap.Recent(6)) Row(f, true);
        }

        var hits = FeatureMap.Search(search).Where(f => tier == "全部" || f.tier == tier).ToList();
        if (filtering) EditorGUILayout.LabelField($"找到 {hits.Count} 项", EditorStyles.miniBoldLabel);
        foreach (var area in FeatureMap.Areas)
        {
            var list = hits.Where(f => f.area == area).ToList();
            if (list.Count == 0) continue;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(area, EditorStyles.boldLabel);
            foreach (var f in list) Row(f, false);
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("防遗忘：每个菜单、游戏按键、网页面板、说明文档都必须登记在这里（体检自动查）。看到某项不懂 → 点「文档」或按 F8 问 AI。", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void Row(FeatureMap.Feature f, bool compact)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(new GUIContent($"{f.name}   <{f.tier}>", f.id), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("怎么打开：" + f.how, EditorStyles.wordWrappedMiniLabel);
                if (!compact)
                {
                    EditorGUILayout.LabelField("能做什么：" + f.what, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.LabelField("创作时：" + f.create, EditorStyles.wordWrappedMiniLabel);
                }
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(64)))
            {
                if (CanGo(f) && GUILayout.Button("打开", EditorStyles.miniButton)) Go(f);
                var doc = DocPath(f);
                if (doc != null && GUILayout.Button("文档", EditorStyles.miniButton)) EditorUtility.OpenWithDefaultApp(doc);
            }
        }
    }

    static string Short(string s) { int i = s.IndexOfAny(new[] { '（', '(', '·' }); return (i > 0 ? s.Substring(0, i) : s).Trim(); }

    static bool CanGo(FeatureMap.Feature f) => f.OpenMenu != null || f.WebPanels.Any() || f.id == "ws.tuning";

    /// <summary>打开这一项：有菜单走菜单（和你自己点菜单一样）；网页面板 → 打开网页；调参 → 选中调参文件。</summary>
    static void Go(FeatureMap.Feature f)
    {
        if (f.id == "ws.tuning") { var t = Step1PrankRoomBuilder.EnsureTuningAsset(); Selection.activeObject = t; EditorGUIUtility.PingObject(t); return; }
        if (f.OpenMenu != null) { var m = f.OpenMenu; EditorApplication.delayCall += () => { if (!EditorApplication.ExecuteMenuItem(m)) Debug.LogWarning("[开始页] 菜单不存在：" + m); }; return; }
        if (f.WebPanels.Any()) OpenWeb();
    }

    public static void OpenWeb()
    {
        var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "tools", "LevelStudioWeb", "index.html"));
        if (System.IO.File.Exists(p)) Application.OpenURL("file:///" + p.Replace('\\', '/'));
        else EditorUtility.DisplayDialog("网页设计台", "找不到 tools/LevelStudioWeb/index.html。\n安装包 04_关卡设计台网页 里的 html 双击也一样。", "好");
    }

    static string DocPath(FeatureMap.Feature f)
    {
        var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
        foreach (var t in f.DocTokens)
            foreach (var dir in new[] { "docs/step1", "docs" })
            {
                var d = System.IO.Path.Combine(root, dir);
                if (!System.IO.Directory.Exists(d)) continue;
                var hit = System.IO.Directory.GetFiles(d, t + "*.md").OrderBy(x => x.Length).FirstOrDefault();
                if (hit != null) return hit;
            }
        return null;
    }
}
