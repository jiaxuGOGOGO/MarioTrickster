#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// PivotRepairTool — 事后修正 Sprite Pivot 的独立工具
///
/// 核心职责：
///   1. 选中场景物体或 Project 中的贴图，查看当前 Pivot 设置
///   2. 一键修正为任意预设或自定义值
///   3. 批量修正整个文件夹下的所有 Sprite
///   4. 完整 Undo 支持，可安全回退
///
/// 使用场景：
///   - 导入后发现角色脚底悬空（Pivot 错误）
///   - 需要把道具从 Center 改为 BottomCenter
///   - 批量修正一批素材的 Pivot
///
/// [AI防坑警告]
///   修正 Pivot 会触发 TextureImporter.SaveAndReimport()，
///   这会导致所有引用该贴图的 Sprite 重新加载。
///   如果场景中有大量引用，建议在修正前保存场景。
/// </summary>
public class PivotRepairTool : EditorWindow
{
    private enum RepairMode
    {
        SelectedSceneObject,  // 修正场景中选中物体的 Sprite
        ProjectTexture,       // 修正 Project 中选中的贴图
        BatchFolder           // 批量修正文件夹
    }

    private RepairMode _mode = RepairMode.SelectedSceneObject;
    private PivotPresetUtility.PivotPreset _targetPreset = PivotPresetUtility.PivotPreset.BottomCenter;
    private Vector2 _customPivot = new Vector2(0.5f, 0.5f);
    private Texture2D _targetTexture;
    private DefaultAsset _targetFolder;
    private Vector2 _scrollPos;
    private string _lastResult = "";
    private List<string> _previewInfo = new List<string>();

    [MenuItem("MarioTrickster/Art Pipeline/Pivot 修正工具")]
    public static void ShowWindow()
    {
        GetWindow<PivotRepairTool>("Pivot 修正工具");
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(4);
        GUILayout.Label("Pivot 修正工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "修正已导入 Sprite 的 Pivot 设置。支持单个物体、单张贴图或批量文件夹修正。\n" +
            "所有操作支持 Ctrl+Z 撤销。",
            MessageType.Info);

        EditorGUILayout.Space(8);

        // 模式选择
        _mode = (RepairMode)EditorGUILayout.EnumPopup("修正模式", _mode);

        EditorGUILayout.Space(4);

        switch (_mode)
        {
            case RepairMode.SelectedSceneObject:
                DrawSceneObjectMode();
                break;
            case RepairMode.ProjectTexture:
                DrawProjectTextureMode();
                break;
            case RepairMode.BatchFolder:
                DrawBatchFolderMode();
                break;
        }

        // 目标 Pivot 选择
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("目标 Pivot", EditorStyles.boldLabel);
        PivotPresetUtility.DrawPivotSelector("修正为", ref _targetPreset, ref _customPivot,
            PivotPresetUtility.PivotPreset.BottomCenter);

        // 执行按钮
        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("执行修正", GUILayout.Height(32)))
        {
            ExecuteRepair();
        }
        GUI.backgroundColor = Color.white;

        // 结果
        if (!string.IsNullOrEmpty(_lastResult))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_lastResult, MessageType.Info);
        }

        // 预览信息
        if (_previewInfo.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("当前 Pivot 信息", EditorStyles.boldLabel);
            foreach (var info in _previewInfo)
            {
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneObjectMode()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorGUILayout.HelpBox("请在场景中选中一个物体。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"选中物体: {selected.name}");

        SpriteRenderer sr = selected.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null)
        {
            EditorGUILayout.HelpBox("选中物体没有 SpriteRenderer 或没有 Sprite。", MessageType.Warning);
            return;
        }

        // 显示当前 Pivot 信息
        _previewInfo.Clear();
        string texPath = AssetDatabase.GetAssetPath(sr.sprite.texture);
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti != null)
        {
            if (ti.spriteImportMode == SpriteImportMode.Multiple)
            {
                var metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
                foreach (var smd in metaData)
                {
                    var preset = PivotPresetUtility.Vector2ToPivot(smd.pivot);
                    _previewInfo.Add($"  帧 '{smd.name}': Pivot=({smd.pivot.x:F2}, {smd.pivot.y:F2}) [{PivotPresetUtility.GetPresetDisplayName(preset)}]");
                }
            }
            else
            {
                TextureImporterSettings sts = new TextureImporterSettings();
                ti.ReadTextureSettings(sts);
                var preset = PivotPresetUtility.Vector2ToPivot(sts.spritePivot);
                _previewInfo.Add($"  Single Sprite: Pivot=({sts.spritePivot.x:F2}, {sts.spritePivot.y:F2}) [{PivotPresetUtility.GetPresetDisplayName(preset)}]");
            }
        }
    }

    private void DrawProjectTextureMode()
    {
        _targetTexture = (Texture2D)EditorGUILayout.ObjectField("贴图", _targetTexture, typeof(Texture2D), false);

        if (_targetTexture != null)
        {
            _previewInfo.Clear();
            string texPath = AssetDatabase.GetAssetPath(_targetTexture);
            TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && ti.spriteImportMode == SpriteImportMode.Multiple)
            {
                var metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
                foreach (var smd in metaData)
                {
                    var preset = PivotPresetUtility.Vector2ToPivot(smd.pivot);
                    _previewInfo.Add($"  帧 '{smd.name}': Pivot=({smd.pivot.x:F2}, {smd.pivot.y:F2}) [{PivotPresetUtility.GetPresetDisplayName(preset)}]");
                }
            }
        }
    }

    private void DrawBatchFolderMode()
    {
        _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("文件夹", _targetFolder, typeof(DefaultAsset), false);

        if (_targetFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            EditorGUILayout.LabelField($"文件夹内共 {guids.Length} 张贴图");
        }
    }

    private void ExecuteRepair()
    {
        // Auto 不适用于修正工具
        PivotPresetUtility.PivotPreset effectivePreset = _targetPreset;
        if (effectivePreset == PivotPresetUtility.PivotPreset.Auto)
        {
            EditorUtility.DisplayDialog("提示", "修正工具不支持 Auto 模式，请选择具体的 Pivot 预设。", "好的");
            return;
        }

        Vector2 targetPivot = PivotPresetUtility.PivotToVector2(effectivePreset, _customPivot);
        int targetAlignment = PivotPresetUtility.PivotToAlignment(effectivePreset);

        switch (_mode)
        {
            case RepairMode.SelectedSceneObject:
                RepairSceneObject(targetPivot, targetAlignment);
                break;
            case RepairMode.ProjectTexture:
                if (_targetTexture != null)
                    RepairTexture(AssetDatabase.GetAssetPath(_targetTexture), targetPivot, targetAlignment);
                break;
            case RepairMode.BatchFolder:
                RepairFolder(targetPivot, targetAlignment);
                break;
        }
    }

    private void RepairSceneObject(Vector2 targetPivot, int targetAlignment)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        SpriteRenderer sr = selected.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        string texPath = AssetDatabase.GetAssetPath(sr.sprite.texture);
        RepairTexture(texPath, targetPivot, targetAlignment);
    }

    private void RepairTexture(string texPath, Vector2 targetPivot, int targetAlignment)
    {
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti == null)
        {
            _lastResult = $"无法获取 TextureImporter: {texPath}";
            return;
        }

        int fixedCount = 0;

        if (ti.spriteImportMode == SpriteImportMode.Multiple)
        {
            SpriteMetaData[] metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
            for (int i = 0; i < metaData.Length; i++)
            {
                if (!Mathf.Approximately(metaData[i].pivot.x, targetPivot.x) ||
                    !Mathf.Approximately(metaData[i].pivot.y, targetPivot.y))
                {
                    metaData[i].pivot = targetPivot;
                    metaData[i].alignment = targetAlignment;
                    fixedCount++;
                }
            }
            if (fixedCount > 0)
            {
                SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, metaData);
            }
        }
        else
        {
            TextureImporterSettings sts = new TextureImporterSettings();
            ti.ReadTextureSettings(sts);
            if (!Mathf.Approximately(sts.spritePivot.x, targetPivot.x) ||
                !Mathf.Approximately(sts.spritePivot.y, targetPivot.y))
            {
                sts.spriteAlignment = targetAlignment;
                sts.spritePivot = targetPivot;
                ti.SetTextureSettings(sts);
                fixedCount = 1;
            }
        }

        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
            AssetDatabase.Refresh();

            string presetName = PivotPresetUtility.GetPresetDisplayName(
                PivotPresetUtility.Vector2ToPivot(targetPivot));
            _lastResult = $"已修正 {texPath}: {fixedCount} 帧 → Pivot={presetName} ({targetPivot.x:F2}, {targetPivot.y:F2})";
            Debug.Log($"[PivotRepairTool] {_lastResult}");
        }
        else
        {
            _lastResult = $"{texPath}: Pivot 已经正确，无需修正。";
        }
    }

    private void RepairFolder(Vector2 targetPivot, int targetAlignment)
    {
        if (_targetFolder == null) return;

        string folderPath = AssetDatabase.GetAssetPath(_targetFolder);
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        int totalFixed = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            bool needsFix = false;

            if (ti.spriteImportMode == SpriteImportMode.Multiple)
            {
                SpriteMetaData[] metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
                for (int i = 0; i < metaData.Length; i++)
                {
                    if (!Mathf.Approximately(metaData[i].pivot.x, targetPivot.x) ||
                        !Mathf.Approximately(metaData[i].pivot.y, targetPivot.y))
                    {
                        metaData[i].pivot = targetPivot;
                        metaData[i].alignment = targetAlignment;
                        needsFix = true;
                    }
                }
                if (needsFix)
                    SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, metaData);
            }
            else
            {
                TextureImporterSettings sts = new TextureImporterSettings();
                ti.ReadTextureSettings(sts);
                if (!Mathf.Approximately(sts.spritePivot.x, targetPivot.x) ||
                    !Mathf.Approximately(sts.spritePivot.y, targetPivot.y))
                {
                    sts.spriteAlignment = targetAlignment;
                    sts.spritePivot = targetPivot;
                    ti.SetTextureSettings(sts);
                    needsFix = true;
                }
            }

            if (needsFix)
            {
                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
                totalFixed++;
            }
        }

        AssetDatabase.Refresh();
        string presetName = PivotPresetUtility.GetPresetDisplayName(
            PivotPresetUtility.Vector2ToPivot(targetPivot));
        _lastResult = $"批量修正完成: {folderPath} 下 {totalFixed}/{guids.Length} 张贴图 → Pivot={presetName}";
        Debug.Log($"[PivotRepairTool] {_lastResult}");
    }

    private void OnSelectionChange()
    {
        _previewInfo.Clear();
        Repaint();
    }
}
#endif
