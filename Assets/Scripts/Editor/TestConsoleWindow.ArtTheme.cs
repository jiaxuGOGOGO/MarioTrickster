using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 2: Art & Theme (美术与主题 — 视觉层)
    // ═══════════════════════════════════════════════════
    private void DrawArtThemeTab()
    {
        // ── 区块 1: 主题换肤 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Theme System", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "拖入 LevelThemeProfile 一键替换所有白盒元素的 Sprite。\n支持 Ctrl+Z 撤销。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

        themeProfile = (LevelThemeProfile)EditorGUILayout.ObjectField(
            "Theme Profile:", themeProfile, typeof(LevelThemeProfile), false);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 0.8f, 1f);
        EditorGUI.BeginDisabledGroup(themeProfile == null);
        if (GUILayout.Button("Apply Theme (with Undo)", GUILayout.Height(28)))
        {
            ApplyThemeWithUndo();
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;

        if (GUILayout.Button("Create New Theme", GUILayout.Height(28)))
        {
            CreateNewThemeProfile();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── 区块 2: Art & Effects Hub ──
        showArtEffectsHub = EditorGUILayout.Foldout(showArtEffectsHub, "★ Art & Effects Hub (素材导入 + Shader效果)", true, EditorStyles.foldoutHeader);
        if (showArtEffectsHub)
        {
            DrawArtEffectsHub();
        }
    }

    /// <summary>绘制 Art & Effects Hub —— 素材导入与 Shader 效果的统一入口</summary>
    private void DrawArtEffectsHub()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox(
            "统一入口：按顺序操作即可完成「导入素材 → 应用到场景 → 挑选效果」全流程。\n" +
            "① 新素材从零开始：用『素材导入管线』或『AI 智能裁切』\n" +
            "② 素材包命名混乱 / 主题槽批量绑定：用『策划生产助手』\n" +
            "③ 素材穿到已有白盒物体：用『Apply Art to Selected』\n" +
            "④ 给物体加视觉效果（闪白/描边/溶解等）：用『SEF Quick Apply』\n" +
            "⑤ 精细调参（颜色替换/HSV/投影等）：用『效果工厂』",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // 第一步：素材导入管线
        EditorGUILayout.LabelField("① 导入新素材", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.5f, 0.85f, 1f);
        if (GUILayout.Button("打开素材导入管线 (Ctrl+Shift+I)", GUILayout.Height(26)))
        {
            AssetImportPipeline.ShowWindow();
        }
        GUI.color = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("AI 智能裁切", GUILayout.Height(26)))
        {
            AI_SmartSlicerWindow.ShowWindow();
        }
        GUI.color = new Color(0.8f, 1f, 0.75f);
        if (GUILayout.Button("策划生产助手", GUILayout.Height(26)))
        {
            PlannerProductionAssistant.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 第二步：应用素材到已有物体
        EditorGUILayout.LabelField("② 应用素材到已有物体", EditorStyles.boldLabel);
        GUI.color = new Color(0.5f, 1f, 0.6f);
        if (GUILayout.Button("打开 Apply Art to Selected (Ctrl+Shift+A)", GUILayout.Height(26)))
        {
            AssetApplyToSelected.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.HelpBox(
            "先在 Scene 中选中白盒物体；点到 Root 或 Visual 都可以，工具会自动找到真正承接行为的 Root。\n" +
            "系统会保留已有的行为组件（碎裂/爆炸/伤害等），只替换贴图、动画和 Material。",
            MessageType.None);

        EditorGUILayout.Space(4);

        // 第三步：SEF 效果
        EditorGUILayout.LabelField("③ 视觉效果（Shader）", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.85f, 0.4f);
        if (GUILayout.Button("SEF Quick Apply (Ctrl+Shift+Q)", GUILayout.Height(26)))
        {
            SEF_QuickApply.ShowWindow();
        }
        GUI.color = new Color(0.9f, 0.7f, 1f);
        if (GUILayout.Button("效果工厂精细调参 (Ctrl+Shift+E)", GUILayout.Height(26)))
        {
            SpriteEffectFactoryWindow.ShowWindow();
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(
            "Quick Apply: 10个预设一键应用（闪白、描边、溶解、冒险描边、冰冻、像素化…）\n" +
            "效果工厂: 拖入素材 → 颜色拆解 → 逐项调参 → 实时预览",
            MessageType.None);

        EditorGUILayout.Space(4);

        // 第四步：快速修复工具
        EditorGUILayout.LabelField("⚙ 快速修复", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(1f, 0.6f, 0.3f);
        if (GUILayout.Button("选中物体补 SEF Material", GUILayout.Height(24)))
        {
            FixSEFMaterialForSelection();
        }
        GUI.color = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("全工程合规巡检", GUILayout.Height(24)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Art Pipeline/一键合规巡检 (校验全工程 PPU-Filter-Pivot)");
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.6f, 0.9f, 0.6f);
        if (GUILayout.Button("Pivot 修正工具", GUILayout.Height(24)))
        {
            PivotRepairTool.ShowWindow();
        }
        GUI.color = new Color(0.9f, 0.9f, 0.5f);
        if (GUILayout.Button("一键修复全工程 Pivot", GUILayout.Height(24)))
        {
            EditorApplication.ExecuteMenuItem("MarioTrickster/Art Pipeline/一键修复 Pivot (根据目录自动修正)");
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "补 SEF Material: 选中物体后点击，自动把 Sprites/Default 换成 SEF UberSprite，让效果能生效\n" +
            "合规巡检: 扫描 Assets/Art/ 下所有贴图，确保 PPU=32 / Point / Uncompressed\n" +
            "Pivot 修正: 单个物体/单张贴图/批量文件夹修正 Pivot，支持 Ctrl+Z 撤销\n" +
            "一键修复: 根据目录自动修正全工程 Pivot（角色→BottomCenter，地形→Center），跳过用户自定义的 Custom Pivot",
            MessageType.None);

        EditorGUILayout.Space(6);

        // Picking 模式提示
        EditorGUILayout.LabelField("ℹ Picking 模式提示", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(
            "Root 模式（默认）: 点击/框选始终选中 Root，适合摆放关卡和整体移动\n" +
            "Visual 模式: 点击/框选始终选中 Visual 子物体，适合只调外观大小\n" +
            "Size Sync: 调 Visual 大小时自动同步碰撞体，反之亦然\n\n" +
            "★ 给已有白盒换皮时点 Root 或 Visual 都可以；Apply Art 会自动回到 Root，避免把行为写错层。",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    /// <summary>为当前选中物体补上 SEF Material（解决“效果不生效”的常见问题）</summary>
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
            // 跳过已经使用 SEF Material 的
            if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
                && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
                continue;

            // 跳过没有贴图的（白盒状态）
            if (sr.sprite == null) continue;
            // 跳过还是白盒 Sprite 的（未被换肤）
            if (sr.sprite.texture != null && sr.sprite.texture.width == 4 && sr.sprite.texture.height == 4)
                continue;

            Material mat = new Material(shader);
            mat.name = $"SEF_{sr.gameObject.name}";
            mat.mainTexture = sr.sprite.texture;
            sr.sharedMaterial = mat;

            // 确保有 SpriteEffectController
            if (sr.gameObject.GetComponent<SpriteEffectController>() == null)
            {
                sr.gameObject.AddComponent<SpriteEffectController>();
            }
            count++;
        }
        if (count > 0)
            Debug.Log($"[TestConsole] 换肤后自动为 {count} 个物体补上 SEF Material");
    }

    /// <summary>生成白盒关卡</summary>
}
