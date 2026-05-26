using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// TestConsoleWindow.RecipePanel — 行为配方面板（集成到 Level Design Tab）
///
/// GBG 风格的"Fancy Objects"面板：
///   一键放置带完整行为的复合结构，不需要逐个元素拼装。
/// </summary>
public partial class TestConsoleWindow : EditorWindow
{
    // ═══════════════════════════════════════════════════
    // Recipe Panel 状态
    // ═══════════════════════════════════════════════════
    private bool showRecipePanel = true;
    private int selectedRecipeCategory = 0;
    private Vector2 recipeScrollPos;

    private readonly string[] recipeCategoryNames = { "🔴 挑战", "🟢 机关", "🔵 路线", "🟠 场景" };
    private readonly Color[] recipeCategoryColors = {
        new Color(1f, 0.4f, 0.3f),
        new Color(0.3f, 0.9f, 0.4f),
        new Color(0.3f, 0.6f, 1f),
        new Color(1f, 0.7f, 0.2f)
    };

    /// <summary>绘制行为配方面板（在 DrawLevelDesignTab 中调用）</summary>
    private void DrawRecipePanel()
    {
        EditorGUILayout.Space(4);

        // 折叠标题
        EditorGUILayout.BeginHorizontal();
        showRecipePanel = EditorGUILayout.Foldout(showRecipePanel, "📦 行为配方 (一键放置复合结构)", true);
        if (showRecipePanel)
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("打开向导", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                SmartSnippetWizard.ShowWindow();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!showRecipePanel) return;

        EditorGUILayout.BeginVertical("box");

        // 分类选择
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < recipeCategoryNames.Length; i++)
        {
            GUI.color = (i == selectedRecipeCategory) ?
                recipeCategoryColors[i] :
                new Color(recipeCategoryColors[i].r * 0.5f, recipeCategoryColors[i].g * 0.5f, recipeCategoryColors[i].b * 0.5f);

            GUIStyle catStyle = (i == selectedRecipeCategory) ?
                new GUIStyle(EditorStyles.toolbarButton) { fontStyle = FontStyle.Bold } :
                EditorStyles.toolbarButton;

            if (GUILayout.Button(recipeCategoryNames[i], catStyle))
            {
                selectedRecipeCategory = i;
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // 获取当前分类的配方
        var category = (BehaviorRecipeLibrary.RecipeCategory)selectedRecipeCategory;
        var recipes = BehaviorRecipeLibrary.GetByCategory(category);

        // 配方网格
        recipeScrollPos = EditorGUILayout.BeginScrollView(recipeScrollPos, GUILayout.MaxHeight(200));

        for (int i = 0; i < recipes.Count; i++)
        {
            var recipe = recipes[i];
            DrawRecipeCard(recipe, i);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制单个配方卡片</summary>
    private void DrawRecipeCard(BehaviorRecipeLibrary.BehaviorRecipe recipe, int index)
    {
        EditorGUILayout.BeginVertical("helpBox");

        EditorGUILayout.BeginHorizontal();

        // 配方名称 + 描述
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(recipe.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(recipe.tooltip, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField($"尺寸: {recipe.width}×{recipe.height}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // 操作按钮
        EditorGUILayout.BeginVertical(GUILayout.Width(80));

        GUI.color = recipeCategoryColors[selectedRecipeCategory];
        if (GUILayout.Button("放置", GUILayout.Height(20)))
        {
            PlaceRecipe(recipe);
        }

        GUI.color = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("预览", GUILayout.Height(20)))
        {
            PreviewRecipe(recipe);
        }
        GUI.color = Color.white;

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>放置配方到场景</summary>
    private void PlaceRecipe(BehaviorRecipeLibrary.BehaviorRecipe recipe)
    {
        Undo.SetCurrentGroupName($"Place Recipe: {recipe.name}");

        // 使用 ASCII 模板生成
        GameObject generated = AsciiLevelGenerator.GenerateFromTemplate(recipe.asciiTemplate, false, true);
        if (generated == null)
        {
            Debug.LogWarning($"[RecipePanel] 配方 '{recipe.name}' 生成失败。");
            return;
        }

        // 放到 Scene 视图中心
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 pivot = sv.pivot;
            pivot.z = 0;
            generated.transform.position = new Vector3(
                Mathf.RoundToInt(pivot.x),
                Mathf.RoundToInt(pivot.y), 0);
        }

        // 挂到主 Root
        GameObject mainRoot = GameObject.Find("AsciiLevel_Root");
        if (mainRoot != null)
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in generated.transform)
                children.Add(child);
            foreach (Transform child in children)
            {
                child.parent = mainRoot.transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, $"Recipe: {recipe.name}");
            }
            Object.DestroyImmediate(generated);
        }
        else
        {
            generated.name = "AsciiLevel_Root";
            PlayableEnvironmentBuilder.EnsurePlayableEnvironment(generated);
            Undo.RegisterCreatedObjectUndo(generated, $"Recipe: {recipe.name}");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"[RecipePanel] 已放置配方: {recipe.name}");
    }

    /// <summary>预览配方（显示 ASCII 预览对话框）</summary>
    private void PreviewRecipe(BehaviorRecipeLibrary.BehaviorRecipe recipe)
    {
        string preview = $"配方: {recipe.name}\n";
        preview += $"描述: {recipe.description}\n";
        preview += $"尺寸: {recipe.width} × {recipe.height}\n\n";
        preview += "ASCII 模板:\n";
        preview += "─────────────────\n";
        preview += recipe.asciiTemplate;
        preview += "\n─────────────────\n\n";

        if (recipe.parameters.Length > 0)
        {
            preview += "可调参数:\n";
            foreach (var p in recipe.parameters)
            {
                preview += $"  • {p.label}: {p.defaultValue} ({p.min}~{p.max} {p.unit})\n";
            }
        }

        EditorUtility.DisplayDialog($"配方预览: {recipe.name}", preview, "OK");
    }
}
