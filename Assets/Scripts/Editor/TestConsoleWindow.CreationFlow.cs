using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class TestConsoleWindow
{
    [SerializeField] private bool studioAdvanced;
    [SerializeField] private int studioRole;
    [SerializeField] private string studioBrush = "#";
    [SerializeField] private bool studioSourceVisible;
    [SerializeField] private float studioZoom = 24f;
    private Vector2 studioCanvasScroll;
    private string studioParsedText;
    private LevelStudioDocument studioDocument;
    private string studioParseError;
    private string studioNotice = "";
    private bool studioPainting;
    private Vector2Int studioPreviousCell;
    private int studioUndoGroup;
    private GUIStyle studioCellStyle;
    private GUIStyle studioSourceStyle;
    private string StudioDraftKey => "MarioTrickster.Studio.Draft." + Hash128.Compute(Application.dataPath);
    private static readonly string[] StudioRoles = { "我玩闯关者 / AI 捣蛋", "我玩捣蛋者 / AI 闯关", "本地双人" };
    private static readonly string[] StudioBasicLabels = { "地面", "平台", "闯关者", "捣蛋者", "终点", "地刺", "弹跳", "擦除" };
    private static readonly char[] StudioBasicChars = { '#', '-', 'M', 'T', 'G', '^', 'B', '.' };
    private bool studioAllElements;

    private void RestoreStudioDraft()
    {
        if (string.IsNullOrEmpty(customAsciiTemplate))
            customAsciiTemplate = EditorPrefs.GetString(StudioDraftKey, "");
        if (string.IsNullOrEmpty(studioBrush)) studioBrush = "#";
        studioRole = Mathf.Clamp(studioRole, 0, StudioRoles.Length - 1);
        Undo.undoRedoPerformed += OnStudioUndo;
    }

    private void SaveStudioDraft()
    {
        // Local recovery only. Export writes a portable source file for version control.
        EditorPrefs.SetString(StudioDraftKey, customAsciiTemplate ?? "");
    }

    private void OnStudioUndo()
    {
        studioParsedText = null;
        SaveStudioDraft();
        Repaint();
    }

    private void SetStudioText(string text, string operation)
    {
        if (text == customAsciiTemplate) return;
        Undo.RecordObject(this, operation);
        customAsciiTemplate = text;
        studioParsedText = null;
        SaveStudioDraft();
        Repaint();
    }

    private bool ConfirmStudioReplace()
    {
        return string.IsNullOrWhiteSpace(customAsciiTemplate) || EditorUtility.DisplayDialog(
            "替换画布？", "现有画布会被替换，可用 Ctrl+Z 撤销。重要作品建议先导出。场景不会被修改。", "替换", "取消");
    }

    private void ParseStudioDocument()
    {
        if (studioParsedText == customAsciiTemplate && studioDocument != null) return;
        studioParsedText = customAsciiTemplate;
        LevelStudioDocument.TryParse(customAsciiTemplate, out studioDocument, out studioParseError);
    }

    private void DrawCreationFlow()
    {
        if (studioCellStyle == null)
        {
            studioCellStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 }; // cached
            studioSourceStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false }; // cached
        }
        if (StudioExplorationRunner.Active)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            DrawExplorationPanel();
            EditorGUILayout.EndScrollView();
            return;
        }
        EditorGUILayout.LabelField("搭一点，玩一下，再改一点。", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("目标：一次只验证一个有趣的选择，而不是填满整张地图。", EditorStyles.wordWrappedMiniLabel);
        DrawStudioPlayBar();
        DrawStudioReport();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        DrawExplorationPanel();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("1  从一个小房间开始", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < LevelStudioDocument.StarterNames.Length; i++)
            {
                if (GUILayout.Button(new GUIContent(LevelStudioDocument.StarterNames[i], LevelStudioDocument.StarterGoals[i]), GUILayout.Height(30)) && ConfirmStudioReplace())
                {
                    SetStudioText(LevelStudioDocument.Starter(i), "Choose starter room");
                    studioNotice = LevelStudioDocument.StarterGoals[i];
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("导入 .txt")) ImportStudioText();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(customAsciiTemplate)))
                if (GUILayout.Button("导出作品 .txt")) ExportStudioText();
            if (GUILayout.Button("从剪贴板载入") && ConfirmStudioReplace()) SetStudioText(EditorGUIUtility.systemCopyBuffer, "Paste level");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("草稿自动保存在本机；导出 .txt 才是可分享、可加入 Git 的作品。", EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(studioNotice)) EditorGUILayout.HelpBox(studioNotice, MessageType.Info);

            ParseStudioDocument();
            if (studioDocument != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("2  在画布上直接画", EditorStyles.boldLabel);
                int brushIndex = Array.IndexOf(StudioBasicChars, studioBrush[0]);
                int chosen = GUILayout.SelectionGrid(brushIndex, StudioBasicLabels, 4, GUILayout.Height(52));
                if (chosen >= 0 && chosen != brushIndex) studioBrush = StudioBasicChars[chosen].ToString();
                studioAllElements = EditorGUILayout.Foldout(studioAllElements, "更多机制（从现有元素库读取）");
                if (studioAllElements)
                {
                    foreach (var entry in AsciiElementRegistry.GetDefault().entries)
                    {
                        if (entry == null || entry.AsciiChar == '\0') continue;
                        if (GUILayout.Toggle(studioBrush[0] == entry.AsciiChar,
                            $"{entry.AsciiChar}   {entry.elementName}", EditorStyles.miniButton)) studioBrush = entry.AsciiChar.ToString();
                    }
                }
                EditorGUILayout.LabelField("左键拖画 · 右键 / Shift 擦除 · Alt+点击吸取 · Ctrl+Z 撤销整笔", EditorStyles.wordWrappedMiniLabel);
                studioZoom = EditorGUILayout.Slider("画布缩放", studioZoom, 16f, 36f);
                DrawStudioCanvas();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"{studioDocument.Width} × {studioDocument.Height} 格", EditorStyles.miniLabel);
                if (GUILayout.Button("右侧 +8 格", EditorStyles.miniButton)) ResizeStudio(8, 0);
                if (GUILayout.Button("上方 +4 格", EditorStyles.miniButton)) ResizeStudio(0, 4);
                EditorGUILayout.EndHorizontal();
                string readiness = studioDocument.PlayReadiness();
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(readiness) ? studioDocument.DesignNudge() : readiness,
                    string.IsNullOrEmpty(readiness) ? MessageType.Info : MessageType.Warning);
            }
            else EditorGUILayout.HelpBox(studioParseError ?? "选择一个起步房间。", MessageType.Info);

            studioSourceVisible = EditorGUILayout.Foldout(studioSourceVisible, "ASCII 源码 / 兼容旧作品", true);
            if (studioSourceVisible || (studioDocument == null && !string.IsNullOrEmpty(customAsciiTemplate)))
            {
                string next = EditorGUILayout.TextArea(customAsciiTemplate, studioSourceStyle, GUILayout.MinHeight(130));
                if (next != customAsciiTemplate) SetStudioText(next, "Edit ASCII source");
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void ResizeStudio(int dx, int dy)
    {
        int width = Math.Min(LevelStudioDocument.MaxWidth, studioDocument.Width + dx);
        int height = Math.Min(LevelStudioDocument.MaxHeight, studioDocument.Height + dy);
        SetStudioText(studioDocument.Resize(width, height).Text, "Expand canvas");
    }

    private void DrawStudioCanvas()
    {
        float size = studioZoom;
        float height = Mathf.Min(360f, studioDocument.Height * size + 18f);
        studioCanvasScroll = EditorGUILayout.BeginScrollView(studioCanvasScroll, GUILayout.Height(height));
        Rect canvas = GUILayoutUtility.GetRect(studioDocument.Width * size, studioDocument.Height * size,
            GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        Event e = Event.current;
        int control = GUIUtility.GetControlID("LevelStudioGrid".GetHashCode(), FocusType.Passive);
        var registry = AsciiElementRegistry.GetDefault();
        if (e.type == EventType.Repaint)
        {
            for (int y = 0; y < studioDocument.Height; y++)
                for (int x = 0; x < studioDocument.Width; x++)
                {
                    char c = studioDocument.Cell(x, y);
                    Rect cell = new Rect(canvas.x + x * size, canvas.y + (studioDocument.Height - 1 - y) * size, size - 1, size - 1);
                    var entry = registry.GetEntry(c);
                    Color color = entry != null ? entry.visualColor : new Color(0.14f, 0.17f, 0.21f);
                    color.a = 1f;
                    EditorGUI.DrawRect(cell, color);
                    if (c != '.' && c != '#') GUI.Label(cell, c.ToString(), studioCellStyle);
                }
            var report = LevelStudioPlaySession.Latest;
            if (report.hasResult && report.lastFailed && report.role == studioRole && report.identity == Hash128.Compute(customAsciiTemplate).ToString())
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(report.lastPosition.x), 0, studioDocument.Width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(report.lastPosition.y), 0, studioDocument.Height - 1);
                Rect marker = new Rect(canvas.x + x * size, canvas.y + (studioDocument.Height - 1 - y) * size, size, size);
                EditorGUI.DrawRect(marker, new Color(1f, 0.3f, 0.1f, 0.65f));
                GUI.Label(marker, "!", studioCellStyle);
            }
        }
        var point = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x - canvas.x) / size),
            studioDocument.Height - 1 - Mathf.FloorToInt((e.mousePosition.y - canvas.y) / size));
        bool inside = canvas.Contains(e.mousePosition);
        if (GUI.enabled && inside && e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
        {
            if (e.alt) studioBrush = studioDocument.Cell(point.x, point.y).ToString();
            else
            {
                Undo.IncrementCurrentGroup();
                studioUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Paint level stroke");
                studioPainting = true;
                studioPreviousCell = point;
                GUIUtility.hotControl = control;
                PaintStudioLine(point, e.button == 1 || e.shift);
            }
            e.Use();
        }
        else if (GUI.enabled && studioPainting && e.type == EventType.MouseDrag)
        {
            if (inside) PaintStudioLine(point, e.button == 1 || e.shift);
            e.Use();
        }
        else if (studioPainting && e.rawType == EventType.MouseUp)
        {
            studioPainting = false;
            Undo.CollapseUndoOperations(studioUndoGroup);
            GUIUtility.hotControl = 0;
            SaveStudioDraft();
            e.Use();
        }
        EditorGUILayout.EndScrollView();
    }

    private void PaintStudioLine(Vector2Int point, bool erase)
    {
        Undo.RecordObject(this, "Paint level stroke");
        char value = erase ? '.' : studioBrush[0];
        int steps = Mathf.Max(Mathf.Abs(point.x - studioPreviousCell.x), Mathf.Abs(point.y - studioPreviousCell.y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 1f : i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(studioPreviousCell.x, point.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(studioPreviousCell.y, point.y, t));
            studioDocument.Paint(x, y, value);
        }
        customAsciiTemplate = studioDocument.Text;
        studioParsedText = customAsciiTemplate;
        studioPreviousCell = point;
        Repaint();
    }

    private void DrawStudioPlayBar()
    {
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重新尝试 (F5)", GUILayout.Height(32))) LevelStudioPlaySession.Retry();
            if (GUILayout.Button("返回修改", GUILayout.Height(32))) LevelStudioPlaySession.ReturnToEdit();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("F1 / F2 随时切换人机 · ESC 暂停 · 重试恢复完整场景（需要 Unity 重入 Play）。", EditorStyles.wordWrappedMiniLabel);
            return;
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
        {
            studioRole = EditorGUILayout.Popup("这次我来", studioRole, StudioRoles);
            ParseStudioDocument();
            bool ready = studioDocument != null && string.IsNullOrEmpty(studioDocument.PlayReadiness());
            using (new EditorGUI.DisabledScope(!ready))
                if (GUILayout.Button("搭建并试玩画布", GUILayout.Height(36))) BuildStudioPreview();
            if (GUILayout.Button("试玩当前场景（保留手工摆放 / 美术）"))
            {
                if (UnityEngine.Object.FindObjectOfType<GameManager>() == null ||
                    UnityEngine.Object.FindObjectOfType<MarioController>() == null)
                    studioNotice = "当前场景尚不可玩。请先选起步房间，再点“搭建并试玩画布”。";
                else
                    LevelStudioPlaySession.Begin("scene:" + SceneManager.GetActiveScene().path + ":" + DateTime.UtcNow.Ticks, studioRole);
            }
        }
        EditorGUILayout.LabelField("搭建画布会打开独立练习场；先提示保存当前场景，不覆盖原关卡。场景内的手工修改不会自动回写画布。", EditorStyles.wordWrappedMiniLabel);
    }

    private void BuildStudioPreview()
    {
        if (!LevelStudioPlaySession.CanStart()) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SaveStudioDraft();
        // Never clear or rebuild the user's existing scene. The generated rehearsal is separate.
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        try
        {
            string source = studioDocument.Text;
            var root = AsciiLevelGenerator.GenerateFromTemplate(source, true, false);
            if (root == null) { studioNotice = "生成失败，草稿已保留。请查看 Console。"; return; }
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
            if (Camera.main != null) Camera.main.orthographic = true;
            if (themeProfile != null) AsciiLevelGenerator.ApplyTheme(themeProfile);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            LevelStudioPlaySession.Begin(Hash128.Compute(customAsciiTemplate).ToString(), studioRole);
        }
        catch (Exception ex)
        {
            studioNotice = "生成失败，草稿未丢失：" + ex.Message;
            Debug.LogException(ex);
        }
    }

    private void DrawStudioReport()
    {
        var report = LevelStudioPlaySession.Latest;
        if (report.attempts == 0) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"3  试玩反馈  |  尝试 {report.attempts} · 通过 {report.clears} · 受阻 {report.failures} · 未完成 {report.unfinished}", EditorStyles.wordWrappedMiniLabel);
        if (report.hasResult)
        {
            EditorGUILayout.LabelField($"{report.outcome} · {report.lastSeconds:F1}s · 结束位置 ({report.lastPosition.x:F1}, {report.lastPosition.y:F1})", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(report.reason, EditorStyles.wordWrappedMiniLabel);
            if (report.bestSeconds > 0) EditorGUILayout.LabelField($"本版最快通过：{report.bestSeconds:F1}s（人机切换或作弊会影响数据，不是认证成绩）", EditorStyles.wordWrappedMiniLabel);
            if (report.identity != Hash128.Compute(customAsciiTemplate).ToString() || report.role != studioRole)
                EditorGUILayout.LabelField("这是上一版 / 另一模式 / 当前场景的记录；下次试玩新版画布会单独统计。", EditorStyles.wordWrappedMiniLabel);
            else if (report.lastFailed)
                EditorGUILayout.LabelField("画布 ! 标记本次结束附近；先检查落脚点与观察空间，一次只改一处。", EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    private void ImportStudioText()
    {
        string path = EditorUtility.OpenFilePanel("导入关卡源码", Application.dataPath, "txt");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (new FileInfo(path).Length > 131072) { studioNotice = "文件过大，请拆分关卡。"; return; }
            string text = File.ReadAllText(path);
            if (!LevelStudioDocument.TryParse(text, out _, out string error)) { studioNotice = error; return; }
            if (ConfirmStudioReplace()) { SetStudioText(text, "Import level"); studioNotice = "已导入；原场景未修改。"; }
        }
        catch (Exception ex) { studioNotice = "导入失败：" + ex.Message; }
    }

    private void ExportStudioText()
    {
        string path = EditorUtility.SaveFilePanel("导出关卡源码", Application.dataPath, "MyLevel", "txt");
        if (string.IsNullOrEmpty(path)) return;
        try { File.WriteAllText(path, customAsciiTemplate); studioNotice = "已导出：" + path; AssetDatabase.Refresh(); }
        catch (Exception ex) { studioNotice = "导出失败：" + ex.Message; }
    }
}
