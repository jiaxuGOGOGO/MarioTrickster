using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 1: Level Design (纯关卡设计 — 布局优先)
    //
    // v2 清爽化重构要点：
    //   - Custom Template Editor 内部精简：字典速查默认折叠、片段库用紧凑卡片
    //   - Quick Whitebox Generator 合并进 Template Editor 区块（减少顶层区块数）
    //   - Gameplay Mechanics 默认折叠（进阶功能，按需展开）
    //   - HelpBox 大幅缩短，详细说明移入 Tooltip
    //   - 所有功能 100% 保留，仅优化呈现层次
    // ═══════════════════════════════════════════════════
    private void DrawLevelDesignTab()
    {
        // ── 区块 1 (最高频): 自定义模板编辑器 ──
        if (LevelStudioStyles.SectionHeader("★ Template Editor", ref showCustomTemplateEditor,
            "ASCII 模板编辑器 + 片段库 + 字典速查 + Quick Whitebox"))
        {
            DrawCustomTemplateSection();
        }

        EditorGUILayout.Space(4);

        // ── 区块 2: 动态元素调色板 ──
        if (LevelStudioStyles.SectionHeader("Element Palette", ref showElementPalette,
            "点击=单个放置到 Scene 中心 | 右键=笔刷模式拖拽绘制"))
        {
            DrawElementPalette();
        }

        EditorGUILayout.Space(4);

        // ── 区块 2.3: 行为配方面板 (GBG-style Fancy Objects) ──
        DrawRecipePanel();

        EditorGUILayout.Space(4);

        // ── 区块 3: Gameplay Mechanics (进阶，默认折叠) ──
        if (LevelStudioStyles.SectionHeader("★ Gameplay Mechanics", ref showGameplayMechanics,
            "附身点网络 / 路线预算 / 机制验证 — 机制驱动的关卡设计"))
        {
            DrawGameplayMechanicsSection();
        }

        EditorGUILayout.Space(4);

        // ── 区块 4: TestSceneBuilder (默认折叠) ──
        if (LevelStudioStyles.SectionHeader("Test Scene Builder", ref showBuilderTools,
            "9-Stage 测试场景 / Validation Scene 一键生成"))
        {
            DrawBuilderToolsSection();
        }

        EditorGUILayout.Space(4);

        // ── 区块 5: 关卡元素集控 (PlayMode) ──
        if (LevelStudioStyles.SectionHeader("Elements Hub", ref showElementsHub,
            "Registry Browser — PlayMode 下查看/操作已注册的关卡元素"))
        {
            DrawElementsHubSection();
        }

        EditorGUILayout.Space(4);

        // ── 区块 6: 测试报告 ──
        if (LevelStudioStyles.SectionHeader("Test Reports", ref showTestReports,
            "运行测试 + 快捷键速查"))
        {
            DrawTestReportsSection();
        }
    }

    // ═══════════════════════════════════════════════════
    // Gameplay Mechanics Section (机制驱动关卡设计)
    // ═══════════════════════════════════════════════════

    private void DrawGameplayMechanicsSection()
    {
        EditorGUILayout.BeginVertical("box");
        LevelStudioStyles.CompactTip("附身点网络 · 路线预算 · 机制验证 — 基于游戏循环的关卡设计工具");

        EditorGUILayout.Space(2);

        // ── 子区块 A: 附身点网络 ──
        showAnchorNetwork = EditorGUILayout.Foldout(showAnchorNetwork, "◆ Possession Anchor Network", true);
        if (showAnchorNetwork)
        {
            DrawAnchorNetworkSubsection();
        }

        EditorGUILayout.Space(2);

        // ── 子区块 B: 路线预算 ──
        showRouteBudget = EditorGUILayout.Foldout(showRouteBudget, "◆ Route Budget", true);
        if (showRouteBudget)
        {
            DrawRouteBudgetSubsection();
        }

        EditorGUILayout.Space(2);

        // ── 子区块 C: 机制验证 ──
        showMechanicsValidation = EditorGUILayout.Foldout(showMechanicsValidation, "◆ Mechanics Validation", true);
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

        // 状态概览（紧凑单行）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"场景附身点: {anchors.Length}", EditorStyles.boldLabel, GUILayout.Width(120));
        GUI.color = LevelStudioStyles.AccentGreen;
        GUILayout.Label($"✔{enabledCount}", GUILayout.Width(40));
        GUI.color = LevelStudioStyles.AccentRed;
        GUILayout.Label($"✖{disabledCount}", GUILayout.Width(40));
        GUI.color = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // 附身点列表
        if (anchors.Length > 0)
        {
            EditorGUILayout.BeginVertical("helpbox");
            foreach (var anchor in anchors)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = anchor.PossessionEnabled ? Color.white : LevelStudioStyles.Muted;
                if (GUILayout.Button(anchor.AnchorId, EditorStyles.miniButtonLeft, GUILayout.Width(140)))
                {
                    Selection.activeGameObject = anchor.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                GUILayout.Label($"({anchor.transform.position.x:F1}, {anchor.transform.position.y:F1})", GUILayout.Width(100));
                GUILayout.Label($"{anchor.DefaultResidueSeconds:F1}s", GUILayout.Width(40));
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            LevelStudioStyles.CompactTip("场景中没有 PossessionAnchor。在带有 IControllableProp 的物体上添加，或用下方按钮快速添加。");
        }

        EditorGUILayout.Space(2);

        // 快捷操作按钮
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("+ 添加附身点", LevelStudioStyles.AccentBlue, 22f))
        {
            AddPossessionAnchorToSelection();
        }
        if (LevelStudioStyles.ColorButton("◎ 高亮全部", LevelStudioStyles.AccentYellow, 22f, 80f))
        {
            HighlightAllAnchorsInScene();
        }
        EditorGUILayout.EndHorizontal();

        // 分布质量提示（紧凑化）
        if (anchors.Length > 0 && anchors.Length < 3)
        {
            LevelStudioStyles.CompactTip("建议 ≥3 个附身点才能支撑连锁和路线预算");
        }
        else if (anchors.Length >= 3)
        {
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

            if (spreadX < 5f)
                LevelStudioStyles.CompactTip($"水平分布过密（{spreadX:F1}格），建议分散到不同路线段");
            else if (spreadY < 2f && anchors.Length > 4)
                LevelStudioStyles.CompactTip("附身点全在同一高度，缺少垂直层次");
            else
                LevelStudioStyles.CompactTip($"✅ 分布合理（水平 {spreadX:F1}格，垂直 {spreadY:F1}格）");
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
            LevelStudioStyles.CompactTip("场景中没有 RouteBudgetService（通常挂在 GameManager 上，白盒阶段可跳过）");
        }
        else
        {
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

            LevelStudioStyles.CompactTip("路线≤2时最多1条降级 | 降级自动恢复 | 降级触发 InterferenceCompensation 补偿");
        }

        EditorGUI.indentLevel--;
    }

    /// <summary>机制验证子区块：一键检查关卡是否满足核心循环要求</summary>
    private void DrawMechanicsValidationSubsection()
    {
        EditorGUI.indentLevel++;

        LevelStudioStyles.CompactTip("检查: ①附身点≥3 ②路线≥2 ③LootObjective+EscapeGate ④AlarmCrisisDirector ⑤分布覆盖");

        EditorGUILayout.Space(2);

        if (LevelStudioStyles.ColorButton("▶ 运行机制验证", LevelStudioStyles.AccentGreen, 26f))
        {
            RunMechanicsValidation();
        }

        GameObject autoFixRoot = GameplayLoopSceneBootstrapper.ResolveActiveLevelRoot();
        if (GameplayLoopSceneBootstrapper.NeedsGameplayLoopAutoFix(autoFixRoot))
        {
            EditorGUILayout.Space(2);
            LevelStudioStyles.CompactTip("检测到缺少 Gameplay Loop 服务，可一键补齐");

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.25f, 0.85f, 0.35f);
            if (GUILayout.Button("Auto-Fix: 补齐 Gameplay Loop 服务", GUILayout.Height(28)))
            {
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Auto-Fix Gameplay Loop Services And Combat Semantics");

                GameObject root = GameplayLoopSceneBootstrapper.ResolveActiveLevelRoot();
                GameplayLoopSceneBootstrapper.EnsureGameplayLoopServices(root);
                GameplayLoopSceneBootstrapper.EnsureCombatRoomSemantics(root);

                Undo.CollapseUndoOperations(undoGroup);
                RunMechanicsValidation();
            }
            GUI.backgroundColor = previousColor;
        }

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

        Undo.SetCurrentGroupName($"Generate Whitebox Level: {templateName}");

        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {templateName}");
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
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

        Undo.SetCurrentGroupName($"Apply Theme: {themeProfile.themeName}");

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer sr in renderers)
        {
            Undo.RecordObject(sr, "Apply Theme Sprite");
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Undo.RecordObject(mainCam, "Apply Theme Camera BG");
        }

        AsciiLevelGenerator.ApplyTheme(themeProfile);
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

    /// <summary>绘制动态元素调色板（v2 紧凑化）</summary>
    private void DrawElementPalette()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        // ── Brush Mode 状态栏（紧凑化） ──
        if (LevelBrushTool.IsActive)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.color = LevelStudioStyles.AccentOrange;
            EditorGUILayout.LabelField($"🖌️ {LevelBrushTool.CurrentBrushName} ({LevelBrushTool.BrushSize}x{LevelBrushTool.BrushSize})", EditorStyles.boldLabel);
            GUI.color = Color.white;
            if (GUILayout.Button("✖", GUILayout.Width(24), GUILayout.Height(18)))
            {
                LevelBrushTool.Deactivate();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            LevelStudioStyles.CompactTip("左键=放置 | 右键=笔刷 | 笔刷中: Shift=橡皮擦, [/]=调大小");
        }

        // 陷阱类
        LevelStudioStyles.SubHeader("Traps");
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Spike", '^', new Color(0.85f, 0.25f, 0.25f));
        DrawPaletteButton("Fire", '~', new Color(1f, 0.5f, 0.1f));
        DrawPaletteButton("Pendulum", 'P', new Color(0.7f, 0.45f, 0.2f));
        DrawPaletteButton("SawBlade", '@', new Color(0.7f, 0.7f, 0.7f));
        EditorGUILayout.EndHorizontal();

        // 平台类
        LevelStudioStyles.SubHeader("Platforms");
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bouncy", 'B', new Color(0.3f, 0.85f, 0.3f));
        DrawPaletteButton("Collapse", 'C', new Color(0.8f, 0.65f, 0.3f));
        DrawPaletteButton("OneWay", '-', new Color(0.5f, 0.75f, 0.9f));
        DrawPaletteButton("Moving", '>', new Color(0.5f, 0.5f, 0.9f));
        DrawPaletteButton("Conveyor", '<', new Color(0.6f, 0.6f, 0.4f));
        EditorGUILayout.EndHorizontal();

        // 敌人类
        LevelStudioStyles.SubHeader("Enemies");
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Bounce", 'E', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Patrol", 'e', new Color(0.9f, 0.2f, 0.6f));
        DrawPaletteButton("Flying", 'f', new Color(0.85f, 0.4f, 0.85f));
        EditorGUILayout.EndHorizontal();

        // 通道/墙壁 + 基础方块（合并为一行组）
        LevelStudioStyles.SubHeader("Blocks & Passages");
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Ground", '#', new Color(0.55f, 0.55f, 0.55f));
        DrawPaletteButton("Platform", '=', new Color(0.7f, 0.7f, 0.7f));
        DrawPaletteButton("Wall", 'W', new Color(0.4f, 0.4f, 0.4f));
        DrawPaletteButton("Breakable", 'X', new Color(0.75f, 0.55f, 0.3f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("FakeWall", 'F', new Color(0.55f, 0.55f, 0.65f));
        DrawPaletteButton("Hidden", 'H', new Color(0.4f, 0.7f, 0.55f));
        DrawPaletteButton("Coin", 'o', new Color(1f, 0.85f, 0.2f));
        DrawPaletteButton("Goal", 'G', new Color(0.2f, 1f, 0.4f));
        DrawPaletteButton("Checkpoint", 'S', new Color(0.2f, 0.8f, 0.9f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>调色板按钮：左键在 Scene 中心生成，右键激活笔刷模式</summary>
    private void DrawPaletteButton(string label, char charKey, Color color)
    {
        GUI.color = color;

        bool isActiveBrush = LevelBrushTool.IsActive && LevelBrushTool.CurrentBrushChar == charKey;
        GUIStyle style = isActiveBrush ? new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold } : GUI.skin.button;
        if (isActiveBrush)
        {
            GUI.color = Color.white;
            GUI.backgroundColor = color;
        }

        Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Height(22));

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && btnRect.Contains(e.mousePosition))
        {
            LevelBrushTool.Activate(label, charKey, color);
            e.Use();
            Repaint();
        }
        else if (GUI.Button(btnRect, label, style))
        {
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

        // [AI防坑警告] 必须使用 sceneView.pivot 而非 camera.transform.position
        Vector3 camCenter = sceneView.pivot;
        camCenter.z = 0;

        int gridX = Mathf.RoundToInt(camCenter.x);
        int gridY = Mathf.RoundToInt(camCenter.y);

        GameObject root = GameObject.Find("AsciiLevel_Root");
        if (root == null)
        {
            root = new GameObject("AsciiLevel_Root");
            Undo.RegisterCreatedObjectUndo(root, "Create ASCII Root");
        }

        string miniTemplate = charKey.ToString();
        GameObject tempRoot = AsciiLevelGenerator.GenerateFromTemplate(miniTemplate, false, true);

        if (tempRoot != null && tempRoot.transform.childCount > 0)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in tempRoot.transform)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                child.position = new Vector3(gridX, gridY, 0);
                child.name = child.name.Replace("_0_0", $"_{gridX}_{gridY}");
                child.parent = root.transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, $"Spawn {label}");
            }

            Object.DestroyImmediate(tempRoot);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Spawned '{label}' at grid ({gridX}, {gridY}).");
        }
    }

    /// <summary>绘制 TestSceneBuilder 工具区块（紧凑化）</summary>
    private void DrawBuilderToolsSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("Build 9-Stage", LevelStudioStyles.AccentGreen, 28f))
        {
            TestSceneBuilder.BuildTestScene();
        }
        if (LevelStudioStyles.ColorButton("Build Validation", LevelStudioStyles.AccentBlue, 28f))
        {
            TestSceneBuilder.BuildValidationScene();
        }
        if (LevelStudioStyles.ColorButton("Clear", LevelStudioStyles.AccentRed, 28f, 55f))
        {
            TestSceneBuilder.ClearTestScene();
        }
        EditorGUILayout.EndHorizontal();

        LevelStudioStyles.CompactTip("Validation Scene 覆盖基础操作/附身/路线/Combo/Heat/Loot-Escape/Scan Wave");

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制关卡元素集控区块</summary>
    private void DrawElementsHubSection()
    {
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        EditorGUILayout.BeginVertical("box");

        int totalCount = LevelElementRegistry.TotalCount;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Registered: {totalCount}", EditorStyles.boldLabel, GUILayout.Width(120));
        if (GUILayout.Button("Reset All", GUILayout.Height(20)))
        {
            LevelElementRegistry.ResetAll();
            Debug.Log("[TestConsole] All elements reset.");
        }
        if (GUILayout.Button("Print", GUILayout.Height(20), GUILayout.Width(50)))
        {
            LevelElementRegistry.DebugPrintSummary();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        elementsScrollPos = EditorGUILayout.BeginScrollView(elementsScrollPos, GUILayout.MinHeight(120));

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
            LevelStudioStyles.CompactTip("无已注册元素。请先生成场景并进入 PlayMode。");
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    /// <summary>绘制测试报告和快捷键区块（紧凑化）</summary>
    private void DrawTestReportsSection()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("EditMode Tests", GUILayout.Height(22)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (EditMode)");
        }
        if (GUILayout.Button("PlayMode Tests", GUILayout.Height(22)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (PlayMode)");
        }
        if (GUILayout.Button("All + Report", GUILayout.Height(22)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Run Tests/Export Full Report (All)");
        }
        EditorGUILayout.EndHorizontal();

        LevelStudioStyles.Separator();

        LevelStudioStyles.CompactTip(
            "<b>Ctrl+T</b> Open Studio | <b>F5</b> Restart | <b>F9</b> NoCooldown | <b>ESC</b> Pause | <b>R</b> Restart | <b>N</b> Next Round");

        EditorGUILayout.EndVertical();
    }


    // ═════════════════════════════════════════════════
    // S26b: Custom Template Editor (三合一 + Quick Whitebox)
    //
    // v2 重构：
    //   - 字典速查默认折叠，用 RichText miniLabel 替代 HelpBox
    //   - 片段库改为紧凑卡片（名称+尺寸+按钮一行搞定）
    //   - Quick Whitebox Generator 合并进此区块底部
    //   - 片段元数据预览保留但更紧凑
    // ═════════════════════════════════════════════════

    /// <summary>绘制自定义模板编辑器（v2 清爽版）</summary>
    private void DrawCustomTemplateSection()
    {
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.BeginVertical("box");

        LevelStudioStyles.CompactTip("粘贴/编写 ASCII 模板一键生成关卡，或从片段库拼装。支持外部 AI 聊天框复制粘贴。");

        // ── 字典速查表（默认折叠，减少视觉噪音） ──
        showCharMapRef = EditorGUILayout.Foldout(showCharMapRef, "字符映射速查", true);
        if (showCharMapRef)
        {
            GUIStyle refStyle = LevelStudioStyles.RichMiniLabel();
            EditorGUILayout.LabelField(
                "<b>#</b>=地面 <b>=</b>=平台 <b>W</b>=墙 <b>.</b>=空气 <b>M</b>=Mario <b>T</b>=Trickster <b>G</b>=终点\n" +
                "<b>^</b>=地刺 <b>~</b>=火焰 <b>P</b>=摆锤 <b>B</b>=弹跳 <b>C</b>=崩塔 <b>-</b>=单向 <b>></b>=移动\n" +
                "<b>E</b>=弹跳怪 <b>e</b>=巡逻怪 <b>f</b>=飞行敌 <b>@</b>=锯片 <b>F</b>=伪装墙 <b>H</b>=隐藏通道\n" +
                "<b>o</b>=金币 <b><</b>=传送带 <b>S</b>=检查点 <b>X</b>=可破坏",
                refStyle);
        }

        EditorGUILayout.Space(2);

        // ── 拼接模式（紧凑化） ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("拼接:", GUILayout.Width(32));
        string[] stitchOptions = { "垂直", "水平" };
        snippetStitchMode = EditorGUILayout.Popup(snippetStitchMode, stitchOptions, GUILayout.Width(60));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // ── 片段库（紧凑卡片模式） ──
        showSnippetLibrary = EditorGUILayout.Foldout(showSnippetLibrary, "经典片段库", true);
        if (showSnippetLibrary)
        {
            DrawCompactSnippetLibrary();
        }

        EditorGUILayout.Space(2);

        // ── 片段元数据预览（紧凑化） ──
        var snippetsForSelection = LevelSnippetLibrary.GetAllSnippets();
        if (snippetsForSelection != null && snippetsForSelection.Count > 0)
        {
            if (selectedSnippetIndex < 0) selectedSnippetIndex = 0;
            if (selectedSnippetIndex >= snippetsForSelection.Count) selectedSnippetIndex = snippetsForSelection.Count - 1;

            string[] snippetNames = snippetsForSelection.Select(s => s.name).ToArray();
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("设计意图:", GUILayout.Width(52));
            selectedSnippetIndex = EditorGUILayout.Popup(selectedSnippetIndex, snippetNames);
            EditorGUILayout.EndHorizontal();

            LevelSnippetLibrary.Snippet selectedSnippet = snippetsForSelection[selectedSnippetIndex];
            if (HasSnippetMetadata(selectedSnippet))
            {
                LevelStudioStyles.CompactTip(BuildSnippetMetadataCompact(selectedSnippet));
            }

            EditorGUILayout.BeginHorizontal();
            if (LevelStudioStyles.ColorButton("追加", LevelStudioStyles.AccentYellow, 20f))
            {
                customAsciiTemplate = StitchSnippet(customAsciiTemplate, selectedSnippet.ascii, snippetStitchMode);
            }
            if (LevelStudioStyles.ColorButton("直接生成", LevelStudioStyles.AccentGreen, 20f))
            {
                GenerateFromCustomTemplate(selectedSnippet.ascii, selectedSnippet.name, true);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(4);

        // ── 模板编辑文本框 ──
        EditorGUILayout.LabelField("模板内容 (每行一层，第一行=最高层):", EditorStyles.boldLabel);
        customAsciiTemplate = EditorGUILayout.TextArea(customAsciiTemplate, LevelStudioStyles.MonoTextArea(),
            GUILayout.MinHeight(120), GUILayout.MaxHeight(350));

        // 统计信息（紧凑单行）
        if (!string.IsNullOrEmpty(customAsciiTemplate))
        {
            string[] lines = customAsciiTemplate.Split('\n');
            int maxWidth = 0;
            foreach (string line in lines)
                if (line.Length > maxWidth) maxWidth = line.Length;
            LevelStudioStyles.MiniHeader($"尺寸: {maxWidth} × {lines.Length} 格");
        }

        // ── 操作按钮行 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(customAsciiTemplate));
        if (LevelStudioStyles.ColorButton("Build (生成关卡)", LevelStudioStyles.AccentGreen, 28f))
        {
            GenerateFromCustomTemplate(customAsciiTemplate, "CustomTemplate");
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("粘贴", GUILayout.Height(28), GUILayout.Width(48)))
        {
            customAsciiTemplate = EditorGUIUtility.systemCopyBuffer;
        }
        if (GUILayout.Button("清空", GUILayout.Height(28), GUILayout.Width(42)))
        {
            customAsciiTemplate = "";
        }
        EditorGUILayout.EndHorizontal();

        // ═══ AI Auto-Healer 按钮行 ═══
        DrawAIHealerButtons();

        LevelStudioStyles.Separator();

        // ── Quick Whitebox Generator（合并进此区块） ──
        LevelStudioStyles.SubHeader("Quick Whitebox");
        EditorGUILayout.BeginHorizontal();
        string[] templateNames = AsciiLevelGenerator.GetBuiltInTemplateNames();
        selectedTemplateIndex = EditorGUILayout.Popup(selectedTemplateIndex, templateNames);
        if (LevelStudioStyles.ColorButton("Generate", LevelStudioStyles.AccentGreen, 22f, 70f))
        {
            GenerateWhiteboxLevel();
        }
        if (LevelStudioStyles.ColorButton("Clear", LevelStudioStyles.AccentRed, 22f, 45f))
        {
            AsciiLevelGenerator.ClearGeneratedLevel();
            Debug.Log("[TestConsole] ASCII level cleared.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUI.EndDisabledGroup();
    }

    /// <summary>紧凑片段库 — 每个片段一行卡片</summary>
    private void DrawCompactSnippetLibrary()
    {
        EditorGUILayout.BeginVertical("helpbox");
        var allSnippets = LevelSnippetLibrary.GetAllSnippets();
        foreach (var snippet in allSnippets)
        {
            EditorGUILayout.BeginHorizontal();

            // 名称 + 尺寸
            EditorGUILayout.LabelField($"{snippet.name}", EditorStyles.boldLabel, GUILayout.Width(110));
            GUILayout.Label($"{snippet.width}×{snippet.height}", EditorStyles.miniLabel, GUILayout.Width(40));

            // 操作按钮（紧凑）
            if (LevelStudioStyles.ColorButton("+", LevelStudioStyles.AccentYellow, 18f, 22f))
            {
                customAsciiTemplate = StitchSnippet(customAsciiTemplate, snippet.ascii, snippetStitchMode);
                Debug.Log($"[TestConsole] Snippet '{snippet.name}' appended.");
            }
            if (LevelStudioStyles.ColorButton("▶", LevelStudioStyles.AccentGreen, 18f, 22f))
            {
                GenerateFromCustomTemplate(snippet.ascii, snippet.name, true);
            }
            if (GUILayout.Button("⎘", GUILayout.Width(22), GUILayout.Height(18)))
            {
                EditorGUIUtility.systemCopyBuffer = snippet.ascii;
                Debug.Log($"[TestConsole] Snippet '{snippet.name}' copied.");
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }

    // ═════════════════════════════════════════════════
    // AI Auto-Healer —— 魔法治愈 + 撤销
    // ═════════════════════════════════════════════════

    private void DrawAIHealerButtons()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(customAsciiTemplate));
        if (LevelStudioStyles.ColorButton("✨ AI 自动修复关卡", LevelStudioStyles.AccentBlue, 24f))
        {
            RunAIHealerAsync();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_backupAsciiTemplate));
        if (GUILayout.Button("↩ 撤销 AI", GUILayout.Height(24), GUILayout.Width(75)))
        {
            customAsciiTemplate = _backupAsciiTemplate;
            _backupAsciiTemplate = "";
            GenerateFromCustomTemplate(customAsciiTemplate, "AI_Healer_Undo");
            Debug.Log("<color=#FFAA00>[AI Auto-Healer] 已撤销 AI 修改，恢复原始关卡。</color>");
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>异步调用 LevelAutoHealer 修复关卡</summary>
    private async void RunAIHealerAsync()
    {
        string diagnosticReport = BuildDiagnosticReportText();
        if (string.IsNullOrEmpty(diagnosticReport))
        {
            Debug.LogWarning("[AI Auto-Healer] 暂无病灶数据！请先在 AI Arena 跑几局自动对战收集数据。");
            return;
        }

        _backupAsciiTemplate = customAsciiTemplate;

        EditorUtility.DisplayProgressBar("AI Auto-Healer", "大模型正在思考如何修复关卡...", 0.5f);

        try
        {
            string newAscii = await LevelAutoHealer.HealAsciiLevelAsync(customAsciiTemplate, diagnosticReport);

            if (!string.IsNullOrEmpty(newAscii))
            {
                customAsciiTemplate = newAscii;
                GenerateFromCustomTemplate(customAsciiTemplate, "AI_Healed_Level", false);
                Debug.Log("<color=#88FF88>[AI Auto-Healer] ✅ 关卡已自动修复并重新生成！点击 [↩ 撤销 AI] 可恢复原始版本。</color>");
                Repaint();
            }
            else
            {
                Debug.LogError("[AI Auto-Healer] AI 返回为空，修复失败。原始关卡未变。");
                _backupAsciiTemplate = "";
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AI Auto-Healer] 修复失败: {ex.Message}");
            _backupAsciiTemplate = "";
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>从 AI Arena 的 AutoTestAnalytics 构建病灶报告文本</summary>
    private string BuildDiagnosticReportText()
    {
        if (_analytics == null || (_analytics.TotalDeaths == 0 && _analytics.TotalStucks == 0))
            return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"对战总局数: {_analytics.TotalMatches}");
        sb.AppendLine($"Mario 胜率: {_analytics.MarioWinRate:F0}% | Trickster 胜率: {_analytics.TricksterWinRate:F0}%");
        sb.AppendLine();

        if (_analytics.TotalDeaths > 0)
        {
            sb.AppendLine($"--- 死亡点 ({_analytics.TotalDeaths} 次) ---");
            var deathGroups = _analytics.DeathPoints
                .GroupBy(d => d.position)
                .OrderByDescending(g => g.Count());
            foreach (var g in deathGroups)
            {
                int cliff = g.Count(d => d.cause == DeathCause.FallOffCliff);
                int trap = g.Count(d => d.cause == DeathCause.TrapKill);
                sb.AppendLine($"  网格 ({g.Key.x}, {g.Key.y}): 死亡 {g.Count()} 次 (坠崖 {cliff} / 机关 {trap})");
            }
        }

        if (_analytics.TotalStucks > 0)
        {
            sb.AppendLine($"--- 卡死点 ({_analytics.TotalStucks} 次) ---");
            var stuckGroups = _analytics.StuckPoints
                .GroupBy(s => s.position)
                .OrderByDescending(g => g.Count());
            foreach (var g in stuckGroups)
            {
                sb.AppendLine($"  网格 ({g.Key.x}, {g.Key.y}): 卡死 {g.Count()} 次");
            }
        }

        return sb.ToString();
    }

    // ═════════════════════════════════════════════════
    // Snippet Metadata Helpers
    // ═════════════════════════════════════════════════

    private bool HasSnippetMetadata(LevelSnippetLibrary.Snippet snippet)
    {
        return snippet != null &&
            (!string.IsNullOrEmpty(snippet.MainRoute) ||
             !string.IsNullOrEmpty(snippet.ShadowRoute) ||
             !string.IsNullOrEmpty(snippet.TrapRoles) ||
             !string.IsNullOrEmpty(snippet.Budget) ||
             !string.IsNullOrEmpty(snippet.TestGoal));
    }

    /// <summary>紧凑版片段元数据（单行 RichText）</summary>
    private string BuildSnippetMetadataCompact(LevelSnippetLibrary.Snippet snippet)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(snippet.MainRoute)) parts.Add($"<b>主线:</b>{snippet.MainRoute}");
        if (!string.IsNullOrEmpty(snippet.ShadowRoute)) parts.Add($"<b>影子:</b>{snippet.ShadowRoute}");
        if (!string.IsNullOrEmpty(snippet.TrapRoles)) parts.Add($"<b>机关:</b>{snippet.TrapRoles}");
        if (!string.IsNullOrEmpty(snippet.Budget)) parts.Add($"<b>预算:</b>{snippet.Budget}");
        if (!string.IsNullOrEmpty(snippet.TestGoal)) parts.Add($"<b>目标:</b>{snippet.TestGoal}");
        return string.Join(" | ", parts);
    }

    private string BuildSnippetMetadataHelp(LevelSnippetLibrary.Snippet snippet)
    {
        string message = "结构化设计意图\n";

        if (!string.IsNullOrEmpty(snippet.MainRoute))
            message += $"\n主路线：{snippet.MainRoute}";
        if (!string.IsNullOrEmpty(snippet.ShadowRoute))
            message += $"\n影子路线：{snippet.ShadowRoute}";
        if (!string.IsNullOrEmpty(snippet.TrapRoles))
            message += $"\n机关角色：{snippet.TrapRoles}";
        if (!string.IsNullOrEmpty(snippet.Budget))
            message += $"\n预算：{snippet.Budget}";
        if (!string.IsNullOrEmpty(snippet.TestGoal))
            message += $"\n测试目标：{snippet.TestGoal}";

        return message;
    }

    /// <summary>从自定义 ASCII 模板生成关卡</summary>
    private void GenerateFromCustomTemplate(string template, string sourceName, bool isSnippet = false)
    {
        if (string.IsNullOrEmpty(template))
        {
            Debug.LogWarning("[TestConsole] Template is empty!");
            return;
        }

        Undo.SetCurrentGroupName($"Generate Level: {sourceName}");

        GameObject root = AsciiLevelGenerator.GenerateFromTemplate(template, true, isSnippet);
        if (root != null)
        {
            Undo.RegisterCreatedObjectUndo(root, $"Generate {sourceName}");
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(root);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[TestConsole] Level '{sourceName}' generated with playable environment.");
        }
    }

    // ═════════════════════════════════════════════════
    // 片段拼接工具方法
    // ═════════════════════════════════════════════════

    /// <summary>
    /// 将新片段拼接到已有模板上。
    /// mode 0 = 垂直拼接（上下堆叠，用 \n\n 分隔）
    /// mode 1 = 水平拼接（左右连接，逐行拼接）
    /// </summary>
    private static string StitchSnippet(string existing, string newSnippet, int mode)
    {
        if (string.IsNullOrEmpty(existing))
            return newSnippet;

        if (mode == 1)
        {
            string[] linesA = existing.Split('\n');
            string[] linesB = newSnippet.Split('\n');

            int maxWidthA = 0;
            for (int i = 0; i < linesA.Length; i++)
            {
                if (linesA[i].Length > maxWidthA)
                    maxWidthA = linesA[i].Length;
            }

            int totalLines = linesA.Length > linesB.Length ? linesA.Length : linesB.Length;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < totalLines; i++)
            {
                string lineA = i < linesA.Length ? linesA[i] : "";
                string lineB = i < linesB.Length ? linesB[i] : "";

                sb.Append(lineA);
                if (lineA.Length < maxWidthA)
                    sb.Append('.', maxWidthA - lineA.Length);
                sb.Append(lineB);

                if (i < totalLines - 1)
                    sb.Append('\n');
            }

            return sb.ToString();
        }
        else
        {
            return existing + "\n\n" + newSnippet;
        }
    }
}
