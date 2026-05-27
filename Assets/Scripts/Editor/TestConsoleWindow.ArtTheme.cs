using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 2: Art & Theme (美术与主题 — 视觉层)
    //
    // v2 清爽化重构要点：
    //   - Theme System 区块精简：去掉 HelpBox，Tooltip 替代
    //   - Art & Effects Hub 改为紧凑卡片式步骤引导
    //   - 每步说明用 CompactTip 替代 HelpBox（节省 60%+ 垂直空间）
    //   - Picking 提示已移入顶部工具栏 Tooltip，此处不再重复
    //   - 所有功能 100% 保留
    // ═══════════════════════════════════════════════════
    private void DrawArtThemeTab()
    {
        // ── 区块 1: 主题换肤（最高频操作，始终展开） ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Theme System", EditorStyles.boldLabel);
        LevelStudioStyles.CompactTip("拖入 LevelThemeProfile 一键替换白盒 Sprite，支持 Ctrl+Z 撤销");

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        themeProfile = (LevelThemeProfile)EditorGUILayout.ObjectField(
            "Theme Profile:", themeProfile, typeof(LevelThemeProfile), false);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(themeProfile == null);
        if (LevelStudioStyles.ColorButton("Apply Theme", LevelStudioStyles.AccentBlue, 26f))
        {
            ApplyThemeWithUndo();
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("+ New Theme", GUILayout.Height(26), GUILayout.Width(95)))
        {
            CreateNewThemeProfile();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── 区块 2: Art & Effects Hub（紧凑卡片式） ──
        if (LevelStudioStyles.SectionHeader("★ Art & Effects Hub", ref showArtEffectsHub,
            "素材导入 → 应用到场景 → Shader效果 全流程"))
        {
            DrawArtEffectsHub();
        }
    }

    /// <summary>绘制 Art & Effects Hub —— 紧凑卡片式步骤引导</summary>
    private void DrawArtEffectsHub()
    {
        EditorGUILayout.BeginVertical("box");

        // ── Step 1: 素材导入 ──
        LevelStudioStyles.SubHeader("① Import");
        LevelStudioStyles.CompactTip("新素材导入/AI裁切/批量命名绑定");
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("Import Pipeline", LevelStudioStyles.AccentBlue, 24f))
        {
            AssetImportPipeline.ShowWindow();
        }
        if (LevelStudioStyles.ColorButton("AI Slicer", LevelStudioStyles.AccentBlue, 24f, 80f))
        {
            AI_SmartSlicerWindow.ShowWindow();
        }
        if (LevelStudioStyles.ColorButton("Planner", LevelStudioStyles.AccentBlue, 24f, 70f))
        {
            PlannerProductionAssistant.ShowWindow();
        }
        EditorGUILayout.EndHorizontal();

        LevelStudioStyles.Separator();

        // ── Step 2: 应用素材 ──
        LevelStudioStyles.SubHeader("② Apply");
        LevelStudioStyles.CompactTip("选中白盒物体 → 替换贴图/动画/Material，保留行为组件");
        if (LevelStudioStyles.ColorButton("Apply Art to Selected", LevelStudioStyles.AccentGreen, 24f))
        {
            AssetApplyToSelected.ShowWindow();
        }

        LevelStudioStyles.Separator();

        // ── Step 3: Shader 效果 ──
        LevelStudioStyles.SubHeader("③ Effects");
        LevelStudioStyles.CompactTip("Quick Apply=10个预设一键应用 | 效果工厂=逐项精细调参");
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("SEF Quick Apply", LevelStudioStyles.AccentYellow, 24f))
        {
            SEF_QuickApply.ShowWindow();
        }
        if (LevelStudioStyles.ColorButton("Effect Factory", LevelStudioStyles.AccentPurple, 24f))
        {
            SpriteEffectFactoryWindow.ShowWindow();
        }
        EditorGUILayout.EndHorizontal();

        LevelStudioStyles.Separator();

        // ── Step 4: 快速修复（折叠，低频操作） ──
        LevelStudioStyles.SubHeader("④ Fix & Validate");
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("补 SEF Material", LevelStudioStyles.AccentOrange, 22f))
        {
            FixSEFMaterialForSelection();
        }
        if (GUILayout.Button(new GUIContent("合规巡检", "扫描 Assets/Art/ 确保 PPU=32 / Point / Uncompressed"), GUILayout.Height(22)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Art Pipeline/一键合规巡检 (校验全工程 PPU-Filter-Pivot)");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Pivot 修正工具", "单个/批量修正 Pivot，支持 Ctrl+Z"), GUILayout.Height(22)))
        {
            PivotRepairTool.ShowWindow();
        }
        if (GUILayout.Button(new GUIContent("一键修复 Pivot", "根据目录自动修正全工程 Pivot"), GUILayout.Height(22)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Art Pipeline/一键修复 Pivot (根据目录自动修正)");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>为当前选中物体补上 SEF Material（解决"效果不生效"的常见问题）</summary>
    private void FixSEFMaterialForSelection()
    {
        var selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先在场景中选中物体", "好的");
            return;
        }

        int fixedCount = 0;
        foreach (var go in selected)
        {
            SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in renderers)
            {
                if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                    && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
                    continue;

                var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
                if (shader == null)
                {
                    Debug.LogWarning("[Art Hub] SEF UberSprite shader not found!");
                    return;
                }

                Undo.RecordObject(sr, "Fix SEF Material");
                Material mat = new Material(shader);
                mat.name = $"SEF_{sr.gameObject.name}";
                if (sr.sprite != null)
                    mat.mainTexture = sr.sprite.texture;
                sr.sharedMaterial = mat;
                fixedCount++;

                // 确保有 SpriteEffectController
                if (sr.gameObject.GetComponent<SpriteEffectController>() == null)
                {
                    Undo.AddComponent<SpriteEffectController>(sr.gameObject);
                }
            }
        }

        if (fixedCount > 0)
        {
            SceneView.RepaintAll();
            Debug.Log($"[Art Hub] 已为 {fixedCount} 个 SpriteRenderer 补上 SEF Material");
        }
        else
        {
            Debug.Log("[Art Hub] 所有选中物体已经使用 SEF Material，无需修复");
        }
    }

    /// <summary>为关卡中所有有 Sprite 的物体补上 SEF Material（换肤后自动调用）</summary>
    private void EnsureSEFMaterialForLevel(GameObject root)
    {
        if (root == null) return;
        var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
        if (shader == null) return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
        int count = 0;
        foreach (var sr in renderers)
        {
            if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
                continue;

            if (sr.sprite == null) continue;
            if (sr.sprite.texture != null && sr.sprite.texture.width == 4 && sr.sprite.texture.height == 4)
                continue;

            Material mat = new Material(shader);
            mat.name = $"SEF_{sr.gameObject.name}";
            mat.mainTexture = sr.sprite.texture;
            sr.sharedMaterial = mat;

            if (sr.gameObject.GetComponent<SpriteEffectController>() == null)
            {
                sr.gameObject.AddComponent<SpriteEffectController>();
            }
            count++;
        }
        if (count > 0)
            Debug.Log($"[TestConsole] 换肤后自动为 {count} 个物体补上 SEF Material");
    }
}
