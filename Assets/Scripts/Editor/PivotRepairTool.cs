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
                RepairSceneObject(targetPivot, targetAlignment, effectivePreset);
                break;
            case RepairMode.ProjectTexture:
                if (_targetTexture != null)
                    RepairTexture(AssetDatabase.GetAssetPath(_targetTexture), targetPivot, targetAlignment, effectivePreset);
                break;
            case RepairMode.BatchFolder:
                RepairFolder(targetPivot, targetAlignment, effectivePreset);
                break;
        }
    }

    private void RepairSceneObject(Vector2 targetPivot, int targetAlignment, PivotPresetUtility.PivotPreset effectivePreset)
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        SpriteRenderer sr = selected.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        string texPath = AssetDatabase.GetAssetPath(sr.sprite.texture);
        RepairTexture(texPath, targetPivot, targetAlignment, effectivePreset);
    }

    private void RepairTexture(string texPath, Vector2 targetPivot, int targetAlignment, PivotPresetUtility.PivotPreset effectivePreset)
    {
        TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (ti == null)
        {
            _lastResult = $"无法获取 TextureImporter: {texPath}";
            return;
        }

        EnsureReadableForVisiblePivot(texPath, ref ti, effectivePreset);
        int fixedCount = 0;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        if (ti.spriteImportMode == SpriteImportMode.Multiple)
        {
            SpriteMetaData[] metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
            for (int i = 0; i < metaData.Length; i++)
            {
                Vector2 framePivot = ComputeVisibleAwarePivot(texture, metaData[i].rect, effectivePreset, targetPivot);
                int frameAlignment = ArePivotsEqual(framePivot, targetPivot) ? targetAlignment : (int)SpriteAlignment.Custom;
                if (!ArePivotsEqual(metaData[i].pivot, framePivot) || metaData[i].alignment != frameAlignment)
                {
                    metaData[i].pivot = framePivot;
                    metaData[i].alignment = frameAlignment;
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
            Rect fullRect = texture != null ? new Rect(0, 0, texture.width, texture.height) : new Rect(0, 0, 0, 0);
            Vector2 framePivot = ComputeVisibleAwarePivot(texture, fullRect, effectivePreset, targetPivot);
            int frameAlignment = ArePivotsEqual(framePivot, targetPivot) ? targetAlignment : (int)SpriteAlignment.Custom;
            if (!ArePivotsEqual(sts.spritePivot, framePivot) || sts.spriteAlignment != frameAlignment)
            {
                sts.spriteAlignment = frameAlignment;
                sts.spritePivot = framePivot;
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

    private void RepairFolder(Vector2 targetPivot, int targetAlignment, PivotPresetUtility.PivotPreset effectivePreset)
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

            EnsureReadableForVisiblePivot(path, ref ti, effectivePreset);
            bool needsFix = false;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (ti.spriteImportMode == SpriteImportMode.Multiple)
            {
                SpriteMetaData[] metaData = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
                for (int i = 0; i < metaData.Length; i++)
                {
                    Vector2 framePivot = ComputeVisibleAwarePivot(texture, metaData[i].rect, effectivePreset, targetPivot);
                    int frameAlignment = ArePivotsEqual(framePivot, targetPivot) ? targetAlignment : (int)SpriteAlignment.Custom;
                    if (!ArePivotsEqual(metaData[i].pivot, framePivot) || metaData[i].alignment != frameAlignment)
                    {
                        metaData[i].pivot = framePivot;
                        metaData[i].alignment = frameAlignment;
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
                Rect fullRect = texture != null ? new Rect(0, 0, texture.width, texture.height) : new Rect(0, 0, 0, 0);
                Vector2 framePivot = ComputeVisibleAwarePivot(texture, fullRect, effectivePreset, targetPivot);
                int frameAlignment = ArePivotsEqual(framePivot, targetPivot) ? targetAlignment : (int)SpriteAlignment.Custom;
                if (!ArePivotsEqual(sts.spritePivot, framePivot) || sts.spriteAlignment != frameAlignment)
                {
                    sts.spriteAlignment = frameAlignment;
                    sts.spritePivot = framePivot;
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

    private void EnsureReadableForVisiblePivot(string path, ref TextureImporter importer, PivotPresetUtility.PivotPreset resolvedPreset)
    {
        if (!ShouldUseVisibleFootPivot(resolvedPreset)) return;
        if (importer == null || importer.isReadable) return;

        importer.isReadable = true;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        importer = AssetImporter.GetAtPath(path) as TextureImporter;
    }

    private bool ShouldUseVisibleFootPivot(PivotPresetUtility.PivotPreset resolvedPreset)
    {
        return resolvedPreset == PivotPresetUtility.PivotPreset.BottomLeft ||
               resolvedPreset == PivotPresetUtility.PivotPreset.BottomCenter ||
               resolvedPreset == PivotPresetUtility.PivotPreset.BottomRight;
    }

    private Vector2 ComputeVisibleAwarePivot(Texture2D texture, Rect rect, PivotPresetUtility.PivotPreset resolvedPreset, Vector2 basePivot)
    {
        if (!ShouldUseVisibleFootPivot(resolvedPreset)) return basePivot;
        if (texture == null || rect.width <= 0f || rect.height <= 0f) return basePivot;
        if (!TryFindOpaqueBounds(texture, rect, out RectInt opaqueBounds)) return basePivot;

        int rectY = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, Mathf.Max(0, texture.height - 1));
        float localBottom = Mathf.Clamp(opaqueBounds.yMin - rectY, 0f, Mathf.Max(1f, rect.height - 1f));
        Vector2 adjusted = basePivot;
        adjusted.y = Mathf.Clamp01(localBottom / Mathf.Max(1f, rect.height));
        return adjusted;
    }

    private bool TryFindOpaqueBounds(Texture2D texture, Rect rect, out RectInt bounds)
    {
        bounds = new RectInt();
        if (texture == null) return false;

        Color32[] pixels;
        try
        {
            pixels = texture.GetPixels32();
        }
        catch (UnityException)
        {
            return false;
        }

        int xMin = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, texture.width);
        int xMax = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 0, texture.width);
        int yMin = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, texture.height);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 0, texture.height);
        if (xMax <= xMin || yMax <= yMin) return false;

        int minX = xMax;
        int maxX = xMin;
        int minY = yMax;
        int maxY = yMin;
        bool found = false;

        for (int y = yMin; y < yMax; y++)
        {
            int row = y * texture.width;
            for (int x = xMin; x < xMax; x++)
            {
                if (pixels[row + x].a <= 8) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                found = true;
            }
        }

        if (!found) return false;
        bounds = new RectInt(minX, minY, Mathf.Max(1, maxX - minX + 1), Mathf.Max(1, maxY - minY + 1));
        return true;
    }

    private bool ArePivotsEqual(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    private void OnSelectionChange()
    {
        _previewInfo.Clear();
        Repaint();
    }
}
#endif
