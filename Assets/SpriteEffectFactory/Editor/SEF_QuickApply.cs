#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// SEF Quick Apply — 效果预设快速应用 + 蓝图保存
///
/// 核心职责：
///   1. 提供常用效果预设（受击闪白、描边高亮、溶解死亡、剪影隐藏等）
///   2. 一键将预设应用到选中物体
///   3. 应用后自动保存为 Prefab 蓝图（带效果参数）
///   4. 与 Asset Import Pipeline 联动：导入的 Object 可直接在此选效果
///
/// 工作流：
///   Asset Import Pipeline 生成 Object → 打开 SEF Quick Apply → 选预设 → 应用 → 自动保存蓝图
///   用户只需关注"选哪个效果好看"，其余全自动。
/// </summary>
public class SEF_QuickApply : EditorWindow
{
    [MenuItem("MarioTrickster/SEF Quick Apply %#q", false, 301)]
    public static void ShowWindow()
    {
        var win = GetWindow<SEF_QuickApply>("效果快速应用");
        win.minSize = new Vector2(380, 500);
    }

    // =========================================================================
    // 预设定义
    // =========================================================================
    private struct EffectPreset
    {
        public string name;
        public string description;
        public System.Action<SpriteEffectController> apply;
    }

    private static readonly EffectPreset[] PRESETS = new EffectPreset[]
    {
        new EffectPreset
        {
            name = "受击闪白 (Hit Flash)",
            description = "被攻击时全身闪白 0.15s，适合所有可受击物体",
            apply = ctrl =>
            {
                ctrl.flashColor = Color.white;
                ctrl.flashAmount = 0f; // 运行时由 PlayHitFlash() 触发
            }
        },
        new EffectPreset
        {
            name = "选中描边 (Outline Highlight)",
            description = "白色描边 + 轻微发光，适合可交互物体/Trickster 伪装目标",
            apply = ctrl =>
            {
                ctrl.enableOutline = true;
                ctrl.outlineColor = new Color(1f, 1f, 1f, 0.9f);
                ctrl.outlineThickness = 1.5f;
                ctrl.outlineGlow = 1.2f;
            }
        },
        new EffectPreset
        {
            name = "危险描边 (Danger Outline)",
            description = "红色描边 + 强发光，适合陷阱/危险物体",
            apply = ctrl =>
            {
                ctrl.enableOutline = true;
                ctrl.outlineColor = new Color(1f, 0.2f, 0.1f, 1f);
                ctrl.outlineThickness = 2f;
                ctrl.outlineGlow = 2.5f;
            }
        },
        new EffectPreset
        {
            name = "溶解死亡 (Dissolve Death)",
            description = "灰度化 → 橙色边缘溶解消失，适合可消灭的敌人",
            apply = ctrl =>
            {
                ctrl.enableDissolve = true;
                ctrl.dissolveAmount = 0f; // 运行时由 PlayDeathSequence() 触发
                ctrl.dissolveEdgeWidth = 0.06f;
                ctrl.dissolveEdgeColor = new Color(1f, 0.5f, 0f, 1f);
            }
        },
        new EffectPreset
        {
            name = "幽灵剪影 (Ghost Silhouette)",
            description = "半透明黑色剪影，适合 Trickster 幽灵形态",
            apply = ctrl =>
            {
                ctrl.enableSilhouette = true;
                ctrl.silhouetteColor = new Color(0.1f, 0f, 0.2f, 0.6f);
            }
        },
        new EffectPreset
        {
            name = "冰冻效果 (Frozen)",
            description = "蓝色色调偏移 + 降饱和度，适合冰冻状态",
            apply = ctrl =>
            {
                ctrl.enableHSV = true;
                ctrl.hueShift = 0.55f;
                ctrl.saturation = 0.4f;
                ctrl.brightness = 1.1f;
            }
        },
        new EffectPreset
        {
            name = "像素化隐藏 (Pixelate Hide)",
            description = "大像素块遮蔽细节，适合隐藏/模糊效果",
            apply = ctrl =>
            {
                ctrl.enablePixelate = true;
                ctrl.pixelSize = 16f;
            }
        },
        new EffectPreset
        {
            name = "投影 (Drop Shadow)",
            description = "右下方黑色半透明投影，增加立体感",
            apply = ctrl =>
            {
                ctrl.enableShadow = true;
                ctrl.shadowColor = new Color(0f, 0f, 0f, 0.4f);
                ctrl.shadowOffset = new Vector2(0.03f, -0.03f);
            }
        },
        new EffectPreset
        {
            name = "全套战斗 (Full Combat)",
            description = "闪白 + 描边 + 溶解 + 投影，适合核心战斗角色",
            apply = ctrl =>
            {
                ctrl.flashColor = Color.white;
                ctrl.enableOutline = true;
                ctrl.outlineColor = Color.white;
                ctrl.outlineThickness = 1f;
                ctrl.outlineGlow = 0.8f;
                ctrl.enableDissolve = true;
                ctrl.dissolveAmount = 0f;
                ctrl.dissolveEdgeWidth = 0.05f;
                ctrl.dissolveEdgeColor = new Color(1f, 0.4f, 0f, 1f);
                ctrl.enableShadow = true;
                ctrl.shadowColor = new Color(0f, 0f, 0f, 0.35f);
                ctrl.shadowOffset = new Vector2(0.02f, -0.02f);
            }
        },
        new EffectPreset
        {
            name = "清除所有效果 (Reset)",
            description = "移除所有效果，恢复原始状态",
            apply = ctrl => ctrl.ResetAllEffects()
        }
    };

    // =========================================================================
    // 状态
    // =========================================================================
    private Vector2 _scrollPos;
    private bool _autoSavePrefab = true;
    private string _prefabFolder = "Assets/Art/Prefabs";
    private int _lastAppliedPreset = -1;

    // =========================================================================
    // GUI
    // =========================================================================
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(8);
        GUILayout.Label("SEF Quick Apply", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选中场景中的物体 → 点击预设 → 效果自动应用 → 自动保存为 Prefab 蓝图\n" +
            "生成的 Prefab 可直接拖入关卡使用，效果参数已内嵌。",
            MessageType.Info);

        EditorGUILayout.Space(4);
        _autoSavePrefab = EditorGUILayout.Toggle("应用后自动保存 Prefab", _autoSavePrefab);
        if (_autoSavePrefab)
        {
            _prefabFolder = EditorGUILayout.TextField("Prefab 目录", _prefabFolder);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("效果预设", EditorStyles.boldLabel);

        // 当前选中物体信息
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorGUILayout.HelpBox("请先在场景中选中一个物体", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"当前选中: {selected.name}");
            var sr = selected.GetComponentInChildren<SpriteRenderer>();
            if (sr == null)
            {
                EditorGUILayout.HelpBox("选中物体没有 SpriteRenderer（检查子物体 Visual）", MessageType.Warning);
            }
        }

        EditorGUILayout.Space(4);

        // 预设按钮
        for (int i = 0; i < PRESETS.Length; i++)
        {
            var preset = PRESETS[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            bool isLast = (i == _lastAppliedPreset);
            GUI.backgroundColor = isLast ? Color.green : Color.white;
            
            if (GUILayout.Button(preset.name, GUILayout.Height(28)))
            {
                ApplyPreset(i);
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField(preset.description, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.Space(8);

        // 手动保存蓝图按钮
        if (GUILayout.Button("手动保存当前物体为 Prefab 蓝图", GUILayout.Height(30)))
        {
            ManualSavePrefab();
        }

        // 打开 Sprite Effect Factory 按钮
        if (GUILayout.Button("打开效果工厂（精细调参）", GUILayout.Height(26)))
        {
            SpriteEffectFactoryWindow.ShowWindow();
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================================
    // 逻辑
    // =========================================================================
    private void ApplyPreset(int index)
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("提示", "请先在场景中选中一个物体", "好的");
            return;
        }

        // 找到 SpriteRenderer（可能在子物体 Visual 上）
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            EditorUtility.DisplayDialog("提示", "选中物体及其子物体中没有 SpriteRenderer", "好的");
            return;
        }

        GameObject visualGO = sr.gameObject;

        // 确保有 SEF Material
        EnsureSEFMaterial(sr);

        // 确保有 SpriteEffectController
        var ctrl = visualGO.GetComponent<SpriteEffectController>();
        if (ctrl == null)
        {
            ctrl = Undo.AddComponent<SpriteEffectController>(visualGO);
        }

        // 应用预设
        Undo.RecordObject(ctrl, $"Apply SEF Preset: {PRESETS[index].name}");
        PRESETS[index].apply(ctrl);

        // [关键修复] 立即同步 keyword + MPB 到 Material，确保编辑器中实时可见
        ctrl.EditorSyncProperties();

        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(sr);
        if (sr.sharedMaterial != null)
            EditorUtility.SetDirty(sr.sharedMaterial);

        _lastAppliedPreset = index;

        Debug.Log($"[SEF Quick Apply] 已应用预设「{PRESETS[index].name}」到 {go.name}");

        // 自动保存 Prefab
        if (_autoSavePrefab && index != PRESETS.Length - 1) // Reset 不保存
        {
            SavePrefabBlueprint(go);
        }

        SceneView.RepaintAll();
    }

    private void EnsureSEFMaterial(SpriteRenderer sr)
    {
        if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
            && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
            return;

        var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader);
        mat.name = $"SEF_{sr.gameObject.name}";
        if (sr.sprite != null)
            mat.mainTexture = sr.sprite.texture;

        // 将 Material 保存为资产，避免每次都创建临时实例
        EnsureDirectory("Assets/Art/Materials/SEF");
        string matPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/Art/Materials/SEF/{mat.name}.mat");
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(sr, "Assign SEF Material");
        sr.sharedMaterial = mat;
        Debug.Log($"[SEF Quick Apply] 已创建并分配 SEF Material: {matPath}");
    }

    private void SavePrefabBlueprint(GameObject go)
    {
        EnsureDirectory(_prefabFolder);

        // 使用根物体（如果选中的是子物体，找到根）
        GameObject root = go;
        if (go.transform.parent != null)
        {
            // 如果有 ImportedAssetMarker，说明父物体是根
            var marker = go.GetComponentInParent<ImportedAssetMarker>();
            if (marker != null)
                root = marker.gameObject;
            else if (go.transform.parent != null)
                root = go.transform.root.gameObject;
        }

        string prefabPath = $"{_prefabFolder}/{root.name}.prefab";

        // 检查是否已经是 Prefab 实例
        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            // 更新现有 Prefab
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            Debug.Log($"[SEF Quick Apply] Prefab 已更新: {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)}");
        }
        else
        {
            // 创建新 Prefab
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
            Debug.Log($"[SEF Quick Apply] Prefab 蓝图已保存: {prefabPath}");
        }
    }

    private void ManualSavePrefab()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("提示", "请先选中物体", "好的");
            return;
        }
        SavePrefabBlueprint(go);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
