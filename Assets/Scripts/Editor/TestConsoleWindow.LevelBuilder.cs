using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 1: Level Design (纯关卡设计 — 布局优先)
    // ═══════════════════════════════════════════════════
    private void DrawLevelDesignTab()
    {
        // ── 区块 1 (最高频): 自定义模板编辑器 + 字典速查 + 片段库 ──
        // 设计理念参考 LDtk / Mario Maker：文本/片段优先的快速迭代工作流
        showCustomTemplateEditor = EditorGUILayout.Foldout(showCustomTemplateEditor, "★ Custom Template Editor (自定义模板编辑器)", true, EditorStyles.foldoutHeader);
        if (showCustomTemplateEditor)
        {
            DrawCustomTemplateSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 2: 动态元素调色板 (点击生成到 Scene 中心) ──
        showElementPalette = EditorGUILayout.Foldout(showElementPalette, "Element Palette (点击生成到 Scene 中心)", true, EditorStyles.foldoutHeader);
        if (showElementPalette)
        {
            DrawElementPalette();
        }

        EditorGUILayout.Space(6);

        // ── 区块 2.5: Gameplay Mechanics (机制驱动关卡设计) ──
        showGameplayMechanics = EditorGUILayout.Foldout(showGameplayMechanics, "\u2605 Gameplay Mechanics (\u673a\u5236\u9a71\u52a8\u5173\u5361\u8bbe\u8ba1)", true, EditorStyles.foldoutHeader);
        if (showGameplayMechanics)
        {
            DrawGameplayMechanicsSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 3: ASCII 快速模板生成 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Quick Whitebox Generator", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        // 模板选择
        string[] templateNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Template:", GUILayout.Width(65));
        selectedTemplateIndex = EditorGUILayout.Popup(selectedTemplateIndex, templateNames);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("Generate Whitebox Level", GUILayout.Height(28)))
        {
            GenerateWhiteboxLevel();
        }
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear ASCII Level", GUILayout.Height(28)))
        {
            AsciiLevelGenerator.ClearGeneratedLevel();
            Debug.Log("[TestConsole] ASCII level cleared.");
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        // 字符映射参考
        showCharMapRef = EditorGUILayout.Foldout(showCharMapRef, "Character Map Reference", true);
        if (showCharMapRef)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(AsciiLevelGenerator.GetCharMapReference(), MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 区块 4: TestSceneBuilder 快捷工具 ──
        showBuilderTools = EditorGUILayout.Foldout(showBuilderTools, "TestSceneBuilder (9-Stage Test Scene)", true, EditorStyles.foldoutHeader);
        if (showBuilderTools)
        {
            DrawBuilderToolsSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 5: 关卡元素集控 (PlayMode) ──
        showElementsHub = EditorGUILayout.Foldout(showElementsHub, "Elements Hub (Registry Browser)", true, EditorStyles.foldoutHeader);
        if (showElementsHub)
        {
            DrawElementsHubSection();
        }

        EditorGUILayout.Space(6);

        // ── 区块 6: 测试报告 ──
        showTestReports = EditorGUILayout.Foldout(showTestReports, "Test Reports & Shortcuts", true, EditorStyles.foldoutHeader);
        if (showTestReports)
        {
            DrawTestReportsSection();
        }
    }

    // ═══════════════════════════════════════════════════
    // Gameplay Mechanics Section (机制驱动关卡设计)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 绘制 Gameplay Mechanics 区块 —— 机制驱动的关卡设计工具。
    /// 包含：附身点网络可视化、路线预算配置、机制验证检查。
    /// </summary>
    private void DrawGameplayMechanicsSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "基于游戏循环的关卡设计工具。\n" +
            "• 附身点网络：可视化和管理 Trickster 可附身的机关/道具\n" +
            "• 路线预算：配置 Mario 的上/下路线和护栏规则\n" +
            "• 机制验证：一键检查关卡是否满足核心循环要求",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── 子区块 A: 附身点网络 (Possession Anchor Network) ──
        showAnchorNetwork = EditorGUILayout.Foldout(showAnchorNetwork, "◆ Possession Anchor Network (附身点网络)", true);
        if (showAnchorNetwork)
        {
            DrawAnchorNetworkSubsection();
        }

        EditorGUILayout.Space(4);

        // ── 子区块 B: 路线预算 (Route Budget) ──
        showRouteBudget = EditorGUILayout.Foldout(showRouteBudget, "◆ Route Budget (路线预算配置)", true);
        if (showRouteBudget)
        {
            DrawRouteBudgetSubsection();
        }

        EditorGUILayout.Space(4);

        // ── 子区块 C: 机制验证 (Mechanics Validation) ──
        showMechanicsValidation = EditorGUILayout.Foldout(showMechanicsValidation, "◆ Mechanics Validation (关卡机制验证)", true);
        if (showMechanicsValidation)
        {
            DrawMechanicsValidationSubsection();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>附身点网络子区块：扫描、可视化、快速添加</summary>
    private void DrawAnchorNetworkSubsection()
    {
        EditorGUI.indentLevel++;

        // 扫描场景中的附身点
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        int enabledCount = 0;
        int disabledCount = 0;
        foreach (var a in anchors)
        {
            if (a.PossessionEnabled) enabledCount++;
            else disabledCount++;
        }

        // 状态概览
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"场景附身点: {anchors.Length} 个", EditorStyles.boldLabel);
        GUI.color = new Color(0.4f, 0.9f, 0.4f);
        GUILayout.Label($"✔ 启用 {enabledCount}", GUILayout.Width(80));
        GUI.color = new Color(0.9f, 0.5f, 0.5f);
        GUILayout.Label($"✖ 禁用 {disabledCount}", GUILayout.Width(80));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // 附身点列表
        if (anchors.Length > 0)
        {
            EditorGUILayout.BeginVertical("helpbox");
            foreach (var anchor in anchors)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = anchor.PossessionEnabled ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                if (GUILayout.Button(anchor.AnchorId, EditorStyles.miniButtonLeft, GUILayout.Width(140)))
                {
                    Selection.activeGameObject = anchor.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                GUILayout.Label($"Pos: ({anchor.transform.position.x:F1}, {anchor.transform.position.y:F1})", GUILayout.Width(140));
                GUILayout.Label($"Residue: {anchor.DefaultResidueSeconds:F1}s", GUILayout.Width(100));
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "场景中没有 PossessionAnchor。\n" +
                "在任何带有 IControllableProp 的物体上添加 PossessionAnchor 组件，\n" +
                "或使用下方按钮快速为选中物体添加。",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        // 快捷操作按钮
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.85f, 0.95f);
        if (GUILayout.Button("+ 为选中物体添加 PossessionAnchor", GUILayout.Height(24)))
        {
            AddPossessionAnchorToSelection();
        }
        GUI.color = new Color(0.95f, 0.85f, 0.4f);
        if (GUILayout.Button("◎ 在 Scene 视图高亮所有附身点", GUILayout.Height(24)))
        {
            HighlightAllAnchorsInScene();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // 设计建议
        if (anchors.Length > 0 && anchors.Length < 3)
        {
            EditorGUILayout.HelpBox(
                "⚠️ 建议至少 3 个附身点才能支撑有意义的连锁和路线预算。\n" +
                "当前核心循环：附身 → 操控 → 连锁 → 热度上升 → 扫描危机",
                MessageType.Warning);
        }
        else if (anchors.Length >= 3)
        {
            // 计算附身点分布质量
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var a in anchors)
            {
                Vector3 p = a.transform.position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }
            float spreadX = maxX - minX;
            float spreadY = maxY - minY;

            string quality = "";
            if (spreadX < 5f)
                quality = "⚠️ 附身点水平分布过密（仅 " + spreadX.ToString("F1") + " 格），建议分散到不同路线段";
            else if (spreadY < 2f && anchors.Length > 4)
                quality = "⚠️ 附身点全部在同一高度，缺少垂直层次感";
            else
                quality = "✅ 附身点分布合理（水平 " + spreadX.ToString("F1") + " 格，垂直 " + spreadY.ToString("F1") + " 格）";

            EditorGUILayout.LabelField(quality);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>路线预算子区块：查看/配置路线护栏</summary>
    private void DrawRouteBudgetSubsection()
    {
        EditorGUI.indentLevel++;

        RouteBudgetService routeBudget = Object.FindObjectOfType<RouteBudgetService>();

        if (routeBudget == null)
        {
            EditorGUILayout.HelpBox(
                "场景中没有 RouteBudgetService。\n" +
                "该组件通常挂在 GameManager 上，负责维护 Mario 的路线护栏。\n" +
                "如果还在白盒阶段，可以先跳过。",
                MessageType.Info);
        }
        else
        {
            // 显示 RouteBudgetService 的配置
            EditorGUILayout.LabelField("路线预算服务已激活", EditorStyles.boldLabel);

            SerializedObject so = new SerializedObject(routeBudget);
            so.Update();

            SerializedProperty autoRecovery = so.FindProperty("autoRecoveryTime");
            SerializedProperty maxDegraded = so.FindProperty("maxSimultaneousDegraded");

            if (autoRecovery != null)
                EditorGUILayout.PropertyField(autoRecovery, new GUIContent("自动恢复时间 (s)"));
            if (maxDegraded != null)
                EditorGUILayout.PropertyField(maxDegraded, new GUIContent("最大同时降级数"));

            so.ApplyModifiedProperties();

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "路线预算规则：\n" +
                "• 当总路线 ≤ 2 时，同时最多 1 条被降级\n" +
                "• 降级路线会自动恢复（上方设置的时间）\n" +
                "• 每次降级会通过 InterferenceCompensation 给 Mario 补偿",
                MessageType.None);
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>机制验证子区块：一键检查关卡是否满足核心循环要求</summary>
    private void DrawMechanicsValidationSubsection()
    {
        EditorGUI.indentLevel++;

        EditorGUILayout.HelpBox(
            "核心循环检查清单：\n" +
            "① 附身点 ≥ 3 个（支撑连锁）\n" +
            "② 路线 ≥ 2 条（保证 Mario 永远有路走）\n" +
            "③ 有 LootObjective + EscapeGate（拢宝撤离目标）\n" +
            "④ 有 AlarmCrisisDirector（扫描波危机）\n" +
            "⑤ 附身点分布覆盖多条路线（避免单点刷刷）",
            MessageType.None);

        EditorGUILayout.Space(4);

        GUI.color = new Color(0.4f, 0.9f, 0.7f);
        if (GUILayout.Button("▶ 运行机制验证", GUILayout.Height(28)))
        {
            RunMechanicsValidation();
        }
        GUI.color = Color.white;

        EditorGUI.indentLevel--;
    }

    /// <summary>为选中物体添加 PossessionAnchor 组件</summary>
    private void AddPossessionAnchorToSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("无选中物体", "请先在 Scene 中选中要添加附身点的物体。", "OK");
            return;
        }

        int addedCount = 0;
        foreach (var go in selected)
        {
            if (go.GetComponent<PossessionAnchor>() == null)
            {
                Undo.AddComponent<PossessionAnchor>(go);
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            Debug.Log($"[Level Studio] 已为 {addedCount} 个物体添加 PossessionAnchor");
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        else
        {
            Debug.Log("[Level Studio] 选中物体已全部拥有 PossessionAnchor");
        }
    }

    /// <summary>在 Scene 视图中高亮所有附身点</summary>
    private void HighlightAllAnchorsInScene()
    {
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        if (anchors.Length == 0)
        {
            EditorUtility.DisplayDialog("无附身点", "场景中没有 PossessionAnchor 组件。", "OK");
            return;
        }

        // 选中所有附身点并聚焦
        GameObject[] anchorObjects = new GameObject[anchors.Length];
        for (int i = 0; i < anchors.Length; i++)
        {
            anchorObjects[i] = anchors[i].gameObject;
        }
        Selection.objects = anchorObjects;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"[Level Studio] 已选中并聚焦 {anchors.Length} 个附身点");
    }

    /// <summary>运行机制验证：检查关卡是否满足核心循环要求</summary>
    private void RunMechanicsValidation()
    {
        List<string> passed = new List<string>();
        List<string> warnings = new List<string>();
        List<string> errors = new List<string>();

        // ① 附身点数量
        PossessionAnchor[] anchors = Object.FindObjectsOfType<PossessionAnchor>(true);
        int enabledAnchors = 0;
        foreach (var a in anchors) { if (a.PossessionEnabled) enabledAnchors++; }

        if (enabledAnchors >= 3)
            passed.Add($"① 附身点: {enabledAnchors} 个 (≥ 3 ✅)");
        else if (enabledAnchors > 0)
            warnings.Add($"① 附身点: 仅 {enabledAnchors} 个，建议 ≥ 3 个才能支撑连锁");
        else
            errors.Add("① 附身点: 0 个 — 没有附身点就没有游戏循环");

        // ② 路线预算
        RouteBudgetService routeBudget = Object.FindObjectOfType<RouteBudgetService>();
        if (routeBudget != null)
            passed.Add("② RouteBudgetService ✅");
        else
            warnings.Add("② RouteBudgetService 未找到（可选，但建议配置）");

        // ③ LootObjective + EscapeGate
        var loot = Object.FindObjectOfType<LootObjective>();
        var escape = Object.FindObjectOfType<EscapeGate>();
        if (loot != null && escape != null)
            passed.Add("③ LootObjective + EscapeGate ✅");
        else if (loot == null && escape == null)
            warnings.Add("③ 缺少 LootObjective 和 EscapeGate（拢宝撤离目标）");
        else
            warnings.Add($"③ 缺少 {(loot == null ? "LootObjective" : "EscapeGate")}");

        // ④ AlarmCrisisDirector
        var crisis = Object.FindObjectOfType<AlarmCrisisDirector>();
        if (crisis != null)
            passed.Add("④ AlarmCrisisDirector ✅");
        else
            warnings.Add("④ AlarmCrisisDirector 未找到（可选，但建议配置）");

        // ⑤ 附身点分布质量
        if (enabledAnchors >= 3)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var a in anchors)
            {
                if (!a.PossessionEnabled) continue;
                float x = a.transform.position.x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
            float spread = maxX - minX;
            if (spread >= 8f)
                passed.Add($"⑤ 附身点水平分布: {spread:F1} 格 ✅");
            else
                warnings.Add($"⑤ 附身点水平分布仅 {spread:F1} 格，建议分散到 ≥ 8 格");
        }

        // ⑥ TricksterHeatMeter
        var heat = Object.FindObjectOfType<TricksterHeatMeter>();
        if (heat != null)
            passed.Add("⑥ TricksterHeatMeter ✅");
        else
            warnings.Add("⑥ TricksterHeatMeter 未找到");

        // ⑦ PropComboTracker
        var combo = Object.FindObjectOfType<PropComboTracker>();
        if (combo != null)
            passed.Add("⑦ PropComboTracker ✅");
        else
            warnings.Add("⑦ PropComboTracker 未找到");

        // 结果汇总
        string report = "=== 机制验证报告 ===\n\n";

        if (passed.Count > 0)
        {
            report += "✅ 通过:\n";
            foreach (var p in passed) report += $"  {p}\n";
        }
        if (warnings.Count > 0)
        {
            report += "\n⚠️ 警告:\n";
            foreach (var w in warnings) report += $"  {w}\n";
        }
        if (errors.Count > 0)
        {
            report += "\n❌ 错误:\n";
            foreach (var e in errors) report += $"  {e}\n";
        }

        report += $"\n总计: {passed.Count} 通过 / {warnings.Count} 警告 / {errors.Count} 错误";

        if (errors.Count == 0 && warnings.Count == 0)
            report += "\n\n🎉 关卡完全满足核心循环要求！";
        else if (errors.Count == 0)
            report += "\n\n👍 关卡基本可玩，但建议补全警告项以获得完整体验";

        EditorUtility.DisplayDialog("机制验证结果", report, "OK");
        Debug.Log($"[Level Studio] {report}");
    }


    private void GenerateWhiteboxLevel()
    {
        string template = AsciiLevelGenerator.GetBuiltInTemplate(selectedTemplateIndex);
        string[] names = AsciiLevelGenerator.GetBuiltInTemplateNames();
        string templateName = selectedTemplateIndex < names.Length ? names[selectedTemplateIndex] : "Unknown";

        // 注册 Undo
        Undo.SetCurrentGroupName($"Generate Whitebox Level: {templateName}");

        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {templateName}");

            // S33: 统一调用链 — 与 GenerateFromCustomTemplate 保持一致，
            // 自动补全 Mario/Trickster/Managers/Camera/KillZone，让生成的关卡直接可 Play。
            // 此方法是幂等的：如果场景中已有这些对象则跳过创建。
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);

            // 聚焦到生成的关卡
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[TestConsole] Whitebox level '{templateName}' generated with playable environment.");
        }
    }

    /// <summary>应用主题（支持 Undo）</summary>
    private void ApplyThemeWithUndo()
    {
        if (themeProfile == null) return;

        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            EditorUtility.DisplayDialog("No Level Found",
                "Please generate a whitebox level first before applying a theme.",
                "OK");
            return;
        }

        // 注册 Undo（记录所有子物体的 SpriteRenderer 状态）
        Undo.SetCurrentGroupName($"Apply Theme: {themeProfile.themeName}");

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            Undo.RecordObject(sr, "Apply Theme Sprite");
        }

        // 记录相机背景色
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Apply Theme Camera BG");
        }

        AsciiLevelGenerator.ApplyTheme(themeProfile);

        // 换肤后自动为所有替换了 Sprite 的物体补上 SEF Material，
        // 确保后续 SEF Quick Apply 效果能直接生效，用户无需手动补 Material。
        EnsureSEFMaterialForLevel(root);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[TestConsole] Theme '{themeProfile.themeName}' applied with Undo support + SEF Material.");
    }

    /// <summary>创建新的 Theme Profile 资产</summary>
    private void CreateNewThemeProfile()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Level Theme Profile",
            "NewLevelTheme",
            "asset",
            "Choose where to save the new theme profile");

        if (string.IsNullOrEmpty(path)) return;

        LevelThemeProfile newProfile = ScriptableObject.CreateInstance<LevelThemeProfile>();
        newProfile.themeName = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(newProfile, path);
        AssetDatabase.SaveAssets();

        themeProfile = newProfile;
        EditorGUIUtility.PingObject(newProfile);
        Selection.activeObject = newProfile;

        Debug.Log($"[TestConsole] New theme profile created at: {path}");
    }

    /// <summary>绘制动态元素调色板</summary>
    private void DrawElementPalette()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        // ── Brush Mode 控制栏 ──
        EditorGUILayout.BeginHorizontal();
        if (LevelBrushTool.IsActive)
        {
            GUI.color = new Color(1f, 0.6f, 0.2f);
            EditorGUILayout.LabelField($"🖌️ 笔刷激活: {LevelBrushTool.CurrentBrushName} ({LevelBrushTool.BrushSize}x{LevelBrushTool.BrushSize})", EditorStyles.boldLabel);
            GUI.color = Color.white;
            if (GUILayout.Button("✖ 退出笔刷", GUILayout.Width(80), GUILayout.Height(20)))
            {
                LevelBrushTool.Deactivate();
            }
        }
        else
        {
            EditorGUILayout.LabelField("点击 = 单个放置 | 右键按钮 = 笔刷模式", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "左键点击: 在 Scene 视图中心生成 | 右键点击: 激活笔刷模式（在 Scene 中拖拽绘制）\n" +
            "笔刷模式: 左键拖动=绘制 | Shift=橡皮擦 | [/]=调大小 | 右键/Esc=退出",
            MessageType.Info);

        // 陷阱类
        EditorGUILayout.LabelField("Traps", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Spike Trap", '^', new Color(0.85f, 0.25f, 0.25f));
        DrawPaletteButton("Fire Trap", '~', new Color(1f, 0.5f, 0.1f));
        DrawPaletteButton("Pendulum", 'P', new Color(0.7f, 0.45f, 0.2f));
        DrawPaletteButton("SawBlade", '@', new Color(0.7f, 0.7f, 0.7f));
        EditorGUILayout.EndHorizontal();

        // 平台类
        EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bouncy", 'B', new Color(0.3f, 0.85f, 0.3f));
        DrawPaletteButton("Collapse", 'C', new Color(0.8f, 0.65f, 0.3f));
        DrawPaletteButton("OneWay", '-', new Color(0.5f, 0.75f, 0.9f));
        DrawPaletteButton("Moving", '>', new Color(0.5f, 0.5f, 0.9f));
        DrawPaletteButton("Conveyor", '<', new Color(0.6f, 0.6f, 0.4f));
        EditorGUILayout.EndHorizontal();

        // 敌人类
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bounce Enemy", 'E', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Simple Enemy", 'e', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Flying Enemy", 'f', new Color(0.85f, 0.4f, 0.85f));
        EditorGUILayout.EndHorizontal();

        // 通道/墙壁类
        EditorGUILayout.LabelField("Passages & Walls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Fake Wall", 'F', new Color(0.55f, 0.55f, 0.65f));
        DrawPaletteButton("Hidden Passage", 'H', new Color(0.4f, 0.7f, 0.55f));
        EditorGUILayout.EndHorizontal();

        // 基础方块
        EditorGUILayout.LabelField("Blocks", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Ground", '#', new Color(0.55f, 0.55f, 0.55f));
        DrawPaletteButton("Platform", '=', new Color(0.7f, 0.7f, 0.7f));
        DrawPaletteButton("Wall", 'W', new Color(0.4f, 0.4f, 0.4f));
        DrawPaletteButton("Breakable", 'X', new Color(0.75f, 0.55f, 0.3f));
        EditorGUILayout.EndHorizontal();

        // 其他
        EditorGUILayout.LabelField("Other", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Collectible", 'o', new Color(1f, 0.85f, 0.2f));
        DrawPaletteButton("Goal Zone", 'G', new Color(0.2f, 1f, 0.4f));
        DrawPaletteButton("Checkpoint", 'S', new Color(0.2f, 0.8f, 0.9f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>调色板按钮：左键在 Scene 中心生成，右键激活笔刷模式</summary>
    private void DrawPaletteButton(string label, char charKey, Color color)
    {
        GUI.color = color;

        // 如果该元素当前是笔刷选中状态，显示高亮边框
        bool isActiveBrush = LevelBrushTool.IsActive && LevelBrushTool.CurrentBrushChar == charKey;
        GUIStyle style = isActiveBrush ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } : GUI.skin.button;
        if (isActiveBrush)
        {
            GUI.color = Color.white;
            // 用更亮的背景表示当前笔刷选中
            GUI.backgroundColor = color;
        }

        Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Height(25));

        // 检测右键点击
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && btnRect.Contains(e.mousePosition))
        {
            // 右键：激活笔刷模式
            LevelBrushTool.Activate(label, charKey, color);
            e.Use();
            Repaint();
        }
        else if (GUI.Button(btnRect, label, style))
        {
            // 左键：单个放置（原有行为）
            SpawnElementAtSceneCenter(charKey, label);
        }

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
    }

    /// <summary>在 Scene 视图摄像机中心生成一个元素（对齐网格）</summary>
    private void SpawnElementAtSceneCenter(char charKey, string label)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[TestConsole] No active Scene View found.");
            return;
        }

        // 获取 Scene 视图焦点中心的世界坐标
        // [AI防坑警告] 必须使用 sceneView.pivot 而非 camera.transform.position
        // camera.transform.position 是 Scene 摄像机的 3D 位置（含透视偏移），
        // 在 2D 模式下与画面可视中心存在较大偏差。
        // pivot 才是用户在 Scene 视图中看到的真正焦点中心。
        Vector3 camCenter = sceneView.pivot;
        camCenter.z = 0;

        // 对齐到网格（四舍五入到整数）
        int gridX = Mathf.RoundToInt(camCenter.x);
        int gridY = Mathf.RoundToInt(camCenter.y);

        // 使用 ASCII 生成器的单字符模板来生成
        // 确保有 Root 节点
        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            root = new GameObject("AsciiLevel_Root");
            Undo.RegisterCreatedObjectUndo(root, "Create ASCII Root");
        }

        // 生成单个元素（通过临时模板）
        string miniTemplate = charKey.ToString();
        // isSnippet = true: 单元素放置不需要完整关卡验证（M/G）
        GameObject tempRoot = AsciiLevelGenerator.GenerateFromTemplate(miniTemplate, false, true);

        if (tempRoot != null && tempRoot.transform.childCount > 0)
        {
            // 将生成的子物体移到正确位置并挂到主 Root 下
            List<Transform> children = new List<Transform>();
            foreach (Transform child in tempRoot.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                // 调整位置到 Scene 摄像机中心
                child.position = new Vector3(gridX, gridY, 0);
                child.name = child.name.Replace("_0_0", $"_{gridX}_{gridY}");
                child.parent = root.transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, $"Spawn {label}");
            }

            // 删除临时 Root
            Object.DestroyImmediate(tempRoot);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Spawned '{label}' at grid ({gridX}, {gridY}).");
        }
    }

    /// <summary>绘制 TestSceneBuilder 工具区块</summary>
    private void DrawBuilderToolsSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("9-Stage Test Scene", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("Build Test Scene", GUILayout.Height(32)))
        {
            TestSceneBuilder.BuildTestScene();
        }
        GUI.color = new Color(0.45f, 0.75f, 1f);
        if (GUILayout.Button("Build Validation Scene", GUILayout.Height(32)))
        {
            TestSceneBuilder.BuildValidationScene();
        }
        GUI.color = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear Test Scene", GUILayout.Height(32)))
        {
            TestSceneBuilder.ClearTestScene();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Validation Scene 是整合后的统一验证关卡：覆盖基础操作、附身门禁、路线预算、Combo/Heat、Loot-Escape、Scan Wave 与 Q 揭穿，不替代原 9-Stage。",
            MessageType.None);

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制关卡元素集控区块</summary>
    private void DrawElementsHubSection()
    {
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        int totalCount = LevelElementRegistry.TotalCount;
        EditorGUILayout.LabelField($"Registered Elements: {totalCount}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset All Elements", GUILayout.Height(25)))
        {
            LevelElementRegistry.ResetAll();
            Debug.Log("[TestConsole] All elements reset.");
        }
        if (GUILayout.Button("Print Summary", GUILayout.Height(25)))
        {
            LevelElementRegistry.DebugPrintSummary();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        elementsScrollPos = EditorGUILayout.BeginScrollView(elementsScrollPos, GUILayout.MinHeight(150));

        var stats = LevelElementRegistry.GetCategoryStats();
        foreach (var kvp in stats)
        {
            string catName = kvp.Key.ToString();
            int count = kvp.Value;

            if (!categoryFoldouts.ContainsKey(catName))
                categoryFoldouts[catName] = true;

            categoryFoldouts[catName] = EditorGUILayout.Foldout(categoryFoldouts[catName],
                $"{catName} ({count})", true, EditorStyles.foldoutHeader);

            if (categoryFoldouts[catName])
            {
                EditorGUI.indentLevel++;
                var elements = LevelElementRegistry.GetByCategory(kvp.Key);
                foreach (var elem in elements)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(elem.Name, EditorStyles.linkLabel))
                    {
                        if (elem.Component != null)
                        {
                            Selection.activeGameObject = elem.Component.gameObject;
                            SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"[{elem.Tags}]", EditorStyles.miniLabel, GUILayout.Width(120));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }

        if (totalCount == 0)
        {
            EditorGUILayout.HelpBox(
                "当前没有已注册的关卡元素。\n请先生成测试场景并进入 PlayMode。",
                MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制测试报告和快捷键区块</summary>
    private void DrawTestReportsSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Test Reports", EditorStyles.boldLabel);

        if (GUILayout.Button("Run EditMode Tests", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)");
        }
        if (GUILayout.Button("Run PlayMode Tests", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)");
        }
        if (GUILayout.Button("Run All Tests + Report", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (All)");
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Keyboard Shortcuts", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Ctrl+T       Open Test Console");
        EditorGUILayout.LabelField("F5           Quick Restart Level");
        EditorGUILayout.LabelField("F9           Toggle No Cooldown");
        EditorGUILayout.LabelField("ESC          Pause/Resume");
        EditorGUILayout.LabelField("R            Restart (Round Over)");
        EditorGUILayout.LabelField("N            Next Round (Round Over)");

        EditorGUILayout.EndVertical();
    }


    // ═════════════════════════════════════════════════
    // S26b: Custom Template Editor (三合一: 字典速查 + 片段库追加 + 文本框编辑)
    // ═════════════════════════════════════════════════

    /// <summary>绘制自定义模板编辑器（三合一：字典速查 + 片段库追加 + 文本框 + Build）</summary>
    private void DrawCustomTemplateSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.HelpBox(
            "粘贴或编写 ASCII 模板，一键生成关卡。\n" +
            "可从外部 AI 聊天框复制模板粘贴进来，也可点击下方片段按钮追加拼装。",
            MessageType.Info);

        // ── 字典速查表 ──
        EditorGUILayout.BeginVertical("box");
        GUIStyle refStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
        EditorGUILayout.LabelField("字符映射速查表:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "<b>#</b>=地面  <b>=</b>=平台  <b>W</b>=墙壁  <b>.</b>=空气  <b>M</b>=Mario  <b>T</b>=Trickster  <b>G</b>=终点\n" +
            "<b>^</b>=地刺  <b>~</b>=火焰  <b>P</b>=摆锤  <b>B</b>=弹跳平台  <b>C</b>=崩塔平台  <b>-</b>=单向平台\n" +
            "<b>E</b>=弹跳怪  <b>e</b>=巡逻怪  <b>></b>=移动平台  <b>F</b>=伪装墙  <b>H</b>=隐藏通道  <b>o</b>=金币\n" +
            "<b>@</b>=锯片  <b>f</b>=飞行敌人  <b><</b>=传送带  <b>S</b>=检查点  <b>X</b>=可破坏方块",
            refStyle);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── 片段库 (Snippet Library) ──
        showSnippetLibrary = EditorGUILayout.Foldout(showSnippetLibrary, "经典片段库 (点击追加到下方文本框)", true);
        if (showSnippetLibrary)
        {
            EditorGUILayout.BeginVertical("box");
            var allSnippets = LevelSnippetLibrary.GetAllSnippets();
            foreach (var snippet in allSnippets)
            {
                EditorGUILayout.BeginVertical("box");
                // 标题 + 尺寸
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(snippet.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
                GUILayout.Label($"{snippet.width}×{snippet.height}", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();

                // 说明
                EditorGUILayout.LabelField(snippet.description, EditorStyles.wordWrappedMiniLabel);

                // 操作按钮
                EditorGUILayout.BeginHorizontal();
                GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUILayout.Button("追加到文本框", GUILayout.Height(22)))
                {
                    // 追加到文本框（如果已有内容，用空行分隔）
                    if (!string.IsNullOrEmpty(customAsciiTemplate))
                        customAsciiTemplate += "\n\n";
                    customAsciiTemplate += snippet.ascii;
                    Debug.Log($"[TestConsole] Snippet '{snippet.name}' appended to template editor.");
                }
                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button("直接生成", GUILayout.Height(22)))
                {
                    // S43: 片段直接生成时传递 isSnippet=true，避免验证器误报缺少 M/G
                    GenerateFromCustomTemplate(snippet.ascii, snippet.name, true);
                }
                GUI.color = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button("复制", GUILayout.Height(22), GUILayout.Width(45)))
                {
                    EditorGUIUtility.systemCopyBuffer = snippet.ascii;
                    Debug.Log($"[TestConsole] Snippet '{snippet.name}' copied to clipboard.");
                }
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);

        // ── 模板编辑文本框 ──
        EditorGUILayout.LabelField("模板内容 (每行一层，第一行=最高层):", EditorStyles.boldLabel);
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
            fontSize = 12,
            wordWrap = false
        };
        customAsciiTemplate = EditorGUILayout.TextArea(customAsciiTemplate, textAreaStyle,
            GUILayout.MinHeight(150), GUILayout.MaxHeight(400));

        // 统计信息
        if (!string.IsNullOrEmpty(customAsciiTemplate))
        {
            string[] lines = customAsciiTemplate.Split('\n');
            int maxWidth = 0;
            foreach (string line in lines)
                if (line.Length > maxWidth) maxWidth = line.Length;
            EditorGUILayout.LabelField($"尺寸: {maxWidth} × {lines.Length} 格", EditorStyles.miniLabel);
        }

        // ── 操作按钮行 ──
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.9f, 0.4f);
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(customAsciiTemplate));
        if (GUILayout.Button("Build From Text (生成关卡)", GUILayout.Height(30)))
        {
            GenerateFromCustomTemplate(customAsciiTemplate, "CustomTemplate");
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;

        if (GUILayout.Button("从剪贴板粘贴", GUILayout.Height(30)))
        {
            customAsciiTemplate = EditorGUIUtility.systemCopyBuffer;
            Debug.Log("[TestConsole] Template pasted from clipboard.");
        }

        if (GUILayout.Button("清空", GUILayout.Height(30), GUILayout.Width(50)))
        {
            customAsciiTemplate = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();
    }

    // ═════════════════════════════════════════════════
    // S26b: 通用模板生成方法
    // ═════════════════════════════════════════════════

    /// <summary>从自定义 ASCII 模板生成关卡</summary>
    /// <param name="template">ASCII 模板字符串</param>
    /// <param name="sourceName">模板来源名称（用于日志）</param>
    /// <param name="isSnippet">是否为片段模式（片段不要求 M/G）</param>
    private void GenerateFromCustomTemplate(string template, string sourceName, bool isSnippet = false)
    {
        if (string.IsNullOrEmpty(template))
        {
            Debug.LogWarning("[TestConsole] Template is empty!");
            return;
        }

        Undo.SetCurrentGroupName($"Generate Level: {sourceName}");

        // S43: 传递 isSnippet 参数给生成器，片段模式下验证器不要求 M/G
        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true, isSnippet);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {sourceName}");

            // S31: 自动创建可玩环境 — 让 ASCII 关卡生成后直接可 Play
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);

            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Level '{sourceName}' generated with playable environment.");
        }
    }
}
