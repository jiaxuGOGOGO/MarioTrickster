#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Asset Import Pipeline — 一站式素材导入工具
/// 
/// 核心职责：
///   将外部购买/下载的美术素材（Sprite Sheet 或单帧图片）通过最少操作
///   转化为项目可用的 Object（带 SpriteRenderer + SpriteEffectController + 正确物理设置），
///   并可一键保存为 Prefab 蓝图，直接拖入关卡使用或送入 Sprite Effect Factory 调效果。
///
/// 工作流：
///   1. 拖入素材（支持单图、Sprite Sheet、文件夹批量）
///   2. 自动识别类型，执行 ART_BIBLE 规范（PPU=32, Point, Uncompressed）
///   3. 若为 Sprite Sheet，自动切片或使用用户指定的行列数
///   4. 选择物理类型（角色/敌人/地形/特效/道具）
///   5. 一键生成场景 Object 或 Prefab 蓝图
///   6. 生成的 Object 自带 SEF Shader + SpriteEffectController，可直接送入效果工厂调参
///
/// [AI防坑警告]
///   本脚本是素材导入管线的唯一入口。PPU/Pivot/FilterMode 的强制值
///   必须与 TA_AssetValidator 和 AI_SpriteSlicer 保持一致。
/// </summary>
public class AssetImportPipeline : EditorWindow
{
    // =========================================================================
    // 菜单入口
    // =========================================================================
    [MenuItem("MarioTrickster/Asset Import Pipeline %#i", false, 200)]
    public static void ShowWindow()
    {
        var win = GetWindow<AssetImportPipeline>("素材导入管线");
        win.minSize = new Vector2(480, 650);
    }

    // =========================================================================
    // 枚举
    // =========================================================================
    private enum AssetPhysicsType
    {
        Character = 0,  // 角色/敌人 → Bottom Center pivot, BoxCollider2D
        Environment = 1, // 地形/平台 → Center pivot, BoxCollider2D, Tiled ready
        Hazard = 2,      // 陷阱/机关 → Center pivot, 可选 trigger collider
        VFX = 3,         // 纯特效 → Center pivot, 无碰撞体
        Prop = 4         // 道具/收集物 → Center pivot, trigger collider
    }

    private enum SliceMode
    {
        Auto = 0,        // 自动检测（基于宽高比）
        Manual = 1,      // 手动指定行列
        Single = 2       // 单帧图片，不切片
    }

    // =========================================================================
    // 状态
    // =========================================================================
    private List<Texture2D> _importTextures = new List<Texture2D>();
    private static readonly string[] SUPPORTED_IMAGE_EXTENSIONS = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };
    private AssetPhysicsType _physicsType = AssetPhysicsType.Environment;
    private SliceMode _sliceMode = SliceMode.Auto;
    private int _manualCols = 8;
    private int _manualRows = 1;
    private bool _generatePrefab = true;
    private bool _attachSEF = true;
    private bool _createSceneObject = true;
    private string _prefabOutputFolder = "Assets/Art/Prefabs";
    private string _spriteOutputFolder = "Assets/Art/Imported";
    private Vector2 _scrollPos;
    private bool _showAdvanced;

    // Pivot 选择
    private PivotPresetUtility.PivotPreset _pivotPreset = PivotPresetUtility.PivotPreset.Auto;
    private Vector2 _customPivot = new Vector2(0.5f, 0.5f);

    // 批量结果
    private List<string> _lastResults = new List<string>();

    // =========================================================================
    // GUI
    // =========================================================================
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawImportSection();
        DrawSettingsSection();
        DrawActionSection();
        DrawResultsSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("Asset Import Pipeline", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "拖入素材 → 自动规范化 → 切片 → 生成 Object/Prefab → 直接送入效果工厂\n" +
            "支持：Project 内贴图、电脑外部图片、Sprite Sheet、批量文件夹拖入",
            MessageType.Info);
        EditorGUILayout.Space(4);
    }

    private void DrawImportSection()
    {
        EditorGUILayout.LabelField("1. 素材输入", EditorStyles.boldLabel);

        // 拖拽区域
        var dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "将素材图片拖到这里（支持多选）", EditorStyles.helpBox);

        HandleDragAndDrop(dropArea);

        // 手动添加
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加贴图", GUILayout.Width(100)))
        {
            string path = EditorUtility.OpenFilePanel("选择素材图片", "Assets", "png,jpg,jpeg,tga,psd");
            if (!string.IsNullOrEmpty(path))
            {
                int before = _importTextures.Count;
                AddTextureFromPath(path, copyExternalToImportFolder: true);
                ReportImportAddResult(_importTextures.Count - before, "添加贴图");
            }
        }
        if (GUILayout.Button("+ 添加文件夹", GUILayout.Width(100)))
        {
            string folder = EditorUtility.OpenFolderPanel("选择素材文件夹", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                int before = _importTextures.Count;
                AddTexturesFromFolder(folder, copyExternalToImportFolder: true);
                ReportImportAddResult(_importTextures.Count - before, "添加文件夹");
            }
        }
        if (_importTextures.Count > 0 && GUILayout.Button("清空列表", GUILayout.Width(80)))
        {
            _importTextures.Clear();
        }
        EditorGUILayout.EndHorizontal();

        // 显示已添加的素材
        if (_importTextures.Count > 0)
        {
            EditorGUILayout.LabelField($"已添加 {_importTextures.Count} 个素材：");
            EditorGUI.indentLevel++;
            for (int i = _importTextures.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                _importTextures[i] = (Texture2D)EditorGUILayout.ObjectField(
                    _importTextures[i], typeof(Texture2D), false, GUILayout.Height(20));
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    _importTextures.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
    }

    private void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("2. 导入设置", EditorStyles.boldLabel);

        _physicsType = (AssetPhysicsType)EditorGUILayout.EnumPopup("物理类型", _physicsType);
        EditorGUILayout.HelpBox(GetPhysicsTypeHint(_physicsType), MessageType.None);

        _sliceMode = (SliceMode)EditorGUILayout.EnumPopup("切片模式", _sliceMode);
        if (_sliceMode == SliceMode.Manual)
        {
            EditorGUI.indentLevel++;
            _manualCols = EditorGUILayout.IntField("列数（帧数/行）", _manualCols);
            _manualRows = EditorGUILayout.IntField("行数", _manualRows);
            EditorGUI.indentLevel--;
        }

        // Pivot 设置
        EditorGUILayout.Space(4);
        {
            var autoResolved = PivotPresetUtility.AutoDetectFromPhysicsType((int)_physicsType);
            PivotPresetUtility.DrawPivotSelector("Pivot 预设", ref _pivotPreset, ref _customPivot, autoResolved);
        }

        EditorGUILayout.Space(4);
        _attachSEF = EditorGUILayout.Toggle("挂载 SEF 效果控制器", _attachSEF);
        _generatePrefab = EditorGUILayout.Toggle("生成 Prefab 蓝图", _generatePrefab);
        _createSceneObject = EditorGUILayout.Toggle("同时创建场景物体", _createSceneObject);

        // 高级选项
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "高级选项");
        if (_showAdvanced)
        {
            EditorGUI.indentLevel++;
            _prefabOutputFolder = EditorGUILayout.TextField("Prefab 输出目录", _prefabOutputFolder);
            _spriteOutputFolder = EditorGUILayout.TextField("素材整理目录", _spriteOutputFolder);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
    }

    private void DrawActionSection()
    {
        EditorGUILayout.LabelField("3. 执行", EditorStyles.boldLabel);

        GUI.enabled = _importTextures.Count > 0;

        if (GUILayout.Button("一键导入并生成 Object", GUILayout.Height(36)))
        {
            ExecuteFullPipeline();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("仅规范化（不生成物体）", GUILayout.Height(28)))
        {
            ExecuteNormalizeOnly();
        }
        if (GUILayout.Button("仅切片（不生成物体）", GUILayout.Height(28)))
        {
            ExecuteSliceOnly();
        }
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.LabelField("已有物体应用素材", EditorStyles.boldLabel);
        if (GUILayout.Button("应用素材到场景中已有物体 (Apply Art to Selected)", GUILayout.Height(28)))
        {
            AssetApplyToSelected.ShowWindow();
        }
        EditorGUILayout.HelpBox(
            "先选中场景中的物体，再用此工具把美术素材“穿”上去\n" +
            "保留已有的行为组件（碰撞/爆炸/伤害等），只替换贴图和效果",
            MessageType.None);

        EditorGUILayout.Space(8);
    }
    private void DrawResultsSection()
    {
        if (_lastResults.Count == 0) return;

        EditorGUILayout.LabelField("执行结果", EditorStyles.boldLabel);
        foreach (var r in _lastResults)
        {
            EditorGUILayout.LabelField(r, EditorStyles.miniLabel);
        }
    }

    // =========================================================================
    // 拖拽处理
    // =========================================================================
    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition)) return;

                bool hasSupportedPayload = DragAndDrop.objectReferences.Any(obj => obj is Texture2D) ||
                                           DragAndDrop.paths.Any(IsSupportedDropPath);
                DragAndDrop.visualMode = hasSupportedPayload ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    int before = _importTextures.Count;

                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D tex)
                        {
                            AddTexture(tex);
                        }
                    }

                    foreach (var path in DragAndDrop.paths)
                    {
                        if (Directory.Exists(path))
                        {
                            AddTexturesFromFolder(path, copyExternalToImportFolder: true);
                        }
                        else
                        {
                            AddTextureFromPath(path, copyExternalToImportFolder: true);
                        }
                    }

                    ReportImportAddResult(_importTextures.Count - before, "拖入素材");
                    Repaint();
                }
                evt.Use();
                break;
        }
    }

    private void AddTexture(Texture2D tex)
    {
        if (tex != null && !_importTextures.Contains(tex))
        {
            _importTextures.Add(tex);
        }
    }

    private void AddTextureFromPath(string path, bool copyExternalToImportFolder)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path) || !IsSupportedImageFile(path)) return;

        string assetPath = ToProjectRelativePath(path);
        if (string.IsNullOrEmpty(assetPath) && copyExternalToImportFolder)
        {
            assetPath = CopyExternalImageIntoProject(path);
        }

        if (string.IsNullOrEmpty(assetPath)) return;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        AddTexture(tex);
    }

    private void AddTexturesFromFolder(string folderPath, bool copyExternalToImportFolder)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

        string assetFolder = ToProjectRelativePath(folderPath);
        if (!string.IsNullOrEmpty(assetFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetFolder });
            foreach (string guid in guids)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                AddTexture(tex);
            }
            return;
        }

        if (!copyExternalToImportFolder) return;
        foreach (string file in Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories))
        {
            AddTextureFromPath(file, copyExternalToImportFolder: true);
        }
    }

    private bool IsSupportedDropPath(string path)
    {
        return Directory.Exists(path) || IsSupportedImageFile(path);
    }

    private bool IsSupportedImageFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return SUPPORTED_IMAGE_EXTENSIONS.Contains(ext);
    }

    private string ToProjectRelativePath(string absoluteOrAssetPath)
    {
        if (string.IsNullOrEmpty(absoluteOrAssetPath)) return null;
        string normalizedPath = absoluteOrAssetPath.Replace('\\', '/');
        string normalizedDataPath = Application.dataPath.Replace('\\', '/');

        if (normalizedPath.StartsWith("Assets/")) return normalizedPath;
        if (normalizedPath == normalizedDataPath) return "Assets";
        if (normalizedPath.StartsWith(normalizedDataPath + "/"))
        {
            return "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
        }
        return null;
    }

    private string CopyExternalImageIntoProject(string sourcePath)
    {
        Directory.CreateDirectory(_spriteOutputFolder);
        string safeName = MakeUniqueAssetPath(Path.Combine(_spriteOutputFolder, Path.GetFileName(sourcePath)).Replace('\\', '/'));
        File.Copy(sourcePath, safeName, overwrite: false);
        AssetDatabase.ImportAsset(safeName, ImportAssetOptions.ForceUpdate);
        return safeName;
    }

    private string MakeUniqueAssetPath(string desiredAssetPath)
    {
        string directory = Path.GetDirectoryName(desiredAssetPath).Replace('\\', '/');
        string fileName = Path.GetFileNameWithoutExtension(desiredAssetPath);
        string extension = Path.GetExtension(desiredAssetPath);
        string candidate = desiredAssetPath;
        int index = 1;
        while (File.Exists(candidate))
        {
            candidate = $"{directory}/{fileName}_{index}{extension}";
            index++;
        }
        return candidate;
    }

    private void ReportImportAddResult(int addedCount, string sourceLabel)
    {
        if (addedCount > 0)
        {
            _lastResults.Insert(0, $"[{sourceLabel}] 已添加 {addedCount} 个素材");
            Debug.Log($"[Asset Import Pipeline] {sourceLabel}: 已添加 {addedCount} 个素材。");
        }
        else
        {
            _lastResults.Insert(0, $"[{sourceLabel}] 没识别到可用图片，请使用 png/jpg/jpeg/tga/psd，或把文件拖到大框里。");
            Debug.LogWarning($"[Asset Import Pipeline] {sourceLabel}: 没识别到可用图片。");
        }
    }

    // =========================================================================
    // 核心管线
    // =========================================================================
    private void ExecuteFullPipeline()
    {
        _lastResults.Clear();
        int successCount = 0;

        foreach (var tex in _importTextures)
        {
            if (tex == null) continue;

            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) continue;

            // Step 1: 规范化导入设置
            NormalizeImportSettings(path);

            // Step 2: 切片
            Sprite[] sprites = SliceTexture(path, tex);
            if (sprites == null || sprites.Length == 0)
            {
                _lastResults.Add($"[跳过] {tex.name}: 切片失败或无有效帧");
                continue;
            }

            // Step 3: 生成 Object / Prefab
            GameObject created = CreateGameObject(tex.name, sprites);
            if (created == null) continue;

            // Step 4: 保存 Prefab
            if (_generatePrefab)
            {
                SaveAsPrefab(created, tex.name);
            }

            // Step 5: 如果不需要场景物体，删除临时物体
            if (!_createSceneObject && _generatePrefab)
            {
                DestroyImmediate(created);
                _lastResults.Add($"[完成] {tex.name}: Prefab 已保存（无场景物体）");
            }
            else
            {
                _lastResults.Add($"[完成] {tex.name}: Object 已生成 + Prefab 已保存");
            }

            successCount++;
        }

        _lastResults.Insert(0, $"=== 导入完成: {successCount}/{_importTextures.Count} 成功 ===");
        AssetDatabase.Refresh();
        Debug.Log($"[Asset Import Pipeline] 批量导入完成: {successCount}/{_importTextures.Count} 成功");
    }

    private void ExecuteNormalizeOnly()
    {
        _lastResults.Clear();
        foreach (var tex in _importTextures)
        {
            if (tex == null) continue;
            string path = AssetDatabase.GetAssetPath(tex);
            NormalizeImportSettings(path);
            _lastResults.Add($"[规范化] {tex.name}: PPU=32, Point, Uncompressed");
        }
        AssetDatabase.Refresh();
    }

    private void ExecuteSliceOnly()
    {
        _lastResults.Clear();
        foreach (var tex in _importTextures)
        {
            if (tex == null) continue;
            string path = AssetDatabase.GetAssetPath(tex);
            NormalizeImportSettings(path);
            Sprite[] sprites = SliceTexture(path, tex);
            int count = sprites != null ? sprites.Length : 0;
            _lastResults.Add($"[切片] {tex.name}: {count} 帧");
        }
        AssetDatabase.Refresh();
    }

    // =========================================================================
    // Step 1: 规范化
    // =========================================================================
    private void NormalizeImportSettings(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        bool dirty = false;

        if (ti.textureType != TextureImporterType.Sprite)
        { ti.textureType = TextureImporterType.Sprite; dirty = true; }

        if (ti.spritePixelsPerUnit != 32)
        { ti.spritePixelsPerUnit = 32; dirty = true; }

        if (!ti.alphaIsTransparency)
        { ti.alphaIsTransparency = true; dirty = true; }

        if (ti.filterMode != FilterMode.Point)
        { ti.filterMode = FilterMode.Point; dirty = true; }

        if (ti.textureCompression != TextureImporterCompression.Uncompressed)
        { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }

        if (!ti.isReadable)
        { ti.isReadable = true; dirty = true; }

        // Mesh Type → Full Rect
        TextureImporterSettings settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            dirty = true;
        }

        if (dirty)
        {
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }
    }

    // =========================================================================
    // Step 2: 切片
    // =========================================================================
    private Sprite[] SliceTexture(string path, Texture2D tex)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;

        int cols, rows;
        DetermineSliceGrid(tex, out cols, out rows);

        if (cols <= 1 && rows <= 1)
        {
            // 单帧，设置为 Single 模式
            ti.spriteImportMode = SpriteImportMode.Single;

            // 设置 Pivot
            TextureImporterSettings sts = new TextureImporterSettings();
            ti.ReadTextureSettings(sts);
            Vector2 basePivot = GetPivotForType(_physicsType);
            int baseAlignment = GetAlignmentForType(_physicsType);
            Rect fullRect = new Rect(0, 0, tex.width, tex.height);
            Vector2 visiblePivot = ComputeVisibleAwarePivot(tex, fullRect, ResolvePivotPresetForType(_physicsType), basePivot);
            sts.spriteAlignment = ArePivotsEqual(visiblePivot, basePivot) ? baseAlignment : (int)SpriteAlignment.Custom;
            sts.spritePivot = visiblePivot;
            ti.SetTextureSettings(sts);

            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();

            // 返回单个 Sprite
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return sprite != null ? new Sprite[] { sprite } : null;
        }

        // 多帧切片
        ti.spriteImportMode = SpriteImportMode.Multiple;

        int frameW = tex.width / cols;
        int frameH = tex.height / rows;
        List<SpriteMetaData> metaList = new List<SpriteMetaData>();

        var resolvedPreset = ResolvePivotPresetForType(_physicsType);
        Vector2 pivot = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
        int alignment = PivotPresetUtility.PivotToAlignment(resolvedPreset);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                SpriteMetaData smd = new SpriteMetaData();
                smd.name = $"{tex.name}_R{r}_F{c}";
                // Unity 的 Rect 原点在左下角，Sprite Sheet 通常从左上角开始
                smd.rect = new Rect(c * frameW, (rows - 1 - r) * frameH, frameW, frameH);
                Vector2 framePivot = ComputeVisibleAwarePivot(tex, smd.rect, resolvedPreset, pivot);
                smd.pivot = framePivot;
                smd.alignment = ArePivotsEqual(framePivot, pivot) ? alignment : (int)SpriteAlignment.Custom;
                metaList.Add(smd);
            }
        }

        SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, metaList);
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();

        // 加载所有切片后的 Sprite
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
        return allAssets.OfType<Sprite>()
            .OrderByDescending(s => s.rect.y)
            .ThenBy(s => s.rect.x)
            .ThenBy(s => s.name)
            .ToArray();
    }

    private void DetermineSliceGrid(Texture2D tex, out int cols, out int rows)
    {
        switch (_sliceMode)
        {
            case SliceMode.Manual:
                cols = Mathf.Max(1, _manualCols);
                rows = Mathf.Max(1, _manualRows);
                break;
            case SliceMode.Single:
                cols = 1;
                rows = 1;
                break;
            case SliceMode.Auto:
            default:
                if (TryDetectSpriteSheetGrid(tex, out cols, out rows)) return;

                // 自动检测：保留旧版宽高比兜底，但优先使用文件名/32px/方格推断，避免角色横条被当成整图单帧。
                float ratio = (float)tex.width / tex.height;
                if (ratio >= 3f)
                {
                    cols = Mathf.RoundToInt(ratio);
                    while (cols > 1 && tex.width % cols != 0) cols--;
                    rows = 1;
                }
                else if (ratio <= 0.33f)
                {
                    cols = 1;
                    rows = Mathf.RoundToInt(1f / ratio);
                    while (rows > 1 && tex.height % rows != 0) rows--;
                }
                else if (ratio > 1.5f)
                {
                    cols = Mathf.Max(1, Mathf.RoundToInt(ratio * 2) / 2);
                    while (cols > 1 && tex.width % cols != 0) cols--;
                    rows = 1;
                }
                else
                {
                    cols = 1;
                    rows = 1;
                }
                break;
        }
    }

    private bool TryDetectSpriteSheetGrid(Texture2D tex, out int cols, out int rows)
    {
        cols = 1;
        rows = 1;
        if (tex == null || tex.width <= 0 || tex.height <= 0) return false;

        Match sizeMatch = Regex.Match(tex.name, @"(?<w>\d{1,4})\s*x\s*(?<h>\d{1,4})", RegexOptions.IgnoreCase);
        if (sizeMatch.Success &&
            int.TryParse(sizeMatch.Groups["w"].Value, out int namedFrameW) &&
            int.TryParse(sizeMatch.Groups["h"].Value, out int namedFrameH) &&
            namedFrameW > 0 && namedFrameH > 0 &&
            tex.width % namedFrameW == 0 && tex.height % namedFrameH == 0)
        {
            cols = tex.width / namedFrameW;
            rows = tex.height / namedFrameH;
            return cols * rows > 1;
        }

        if (tex.height == 32 && tex.width % 32 == 0 && tex.width / 32 > 1)
        {
            cols = tex.width / 32;
            rows = 1;
            return true;
        }

        if (tex.width > tex.height && tex.width % tex.height == 0 && tex.width / tex.height > 1)
        {
            cols = tex.width / tex.height;
            rows = 1;
            return true;
        }

        if (tex.width % 32 == 0 && tex.height % 32 == 0 && (tex.width / 32) * (tex.height / 32) > 1)
        {
            cols = tex.width / 32;
            rows = tex.height / 32;
            return true;
        }

        return false;
    }

    private PivotPresetUtility.PivotPreset ResolvePivotPresetForType(AssetPhysicsType type)
    {
        return PivotPresetUtility.ResolvePreset(_pivotPreset, (int)type, null);
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

    // =========================================================================
    // Step 3: 生成 GameObject
    // =========================================================================
    private GameObject CreateGameObject(string baseName, Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0) return null;

        // 使用项目的 Root + Visual 层级结构（S37 视碰分离架构）
        GameObject root = new GameObject($"{baseName}_Root");
        Undo.RegisterCreatedObjectUndo(root, "Import Asset Object");

        // Visual 子物体
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one;

        // SpriteRenderer on Visual
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[0]; // 默认使用第一帧

        // 商业素材统一分类：角色状态动画优先，普通多帧素材走循环播放器
        ArtAssetClassifier.Classification classification = ArtAssetClassifier.Classify(root, sprites, (int)_physicsType);
        ConfigureSpriteAnimation(visual, sr, sprites, classification);

        // 设置 SEF Shader Material
        if (_attachSEF)
        {
            var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.mainTexture = sprites[0].texture;
                sr.sharedMaterial = mat;

                // 保存材质资产
                EnsureDirectory(_prefabOutputFolder);
                string matPath = $"{_prefabOutputFolder}/{baseName}_SEF_Mat.mat";
                matPath = AssetDatabase.GenerateUniqueAssetPath(matPath);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            // 挂载 SpriteEffectController
            visual.AddComponent<SpriteEffectController>();
        }

        // 根据物理类型设置碰撞体
        SetupPhysics(root, sr);

        // 添加导入标记组件（方便后续工具识别和处理）
        var marker = root.AddComponent<ImportedAssetMarker>();
        marker.sourceSprites = sprites;
        marker.physicsType = (int)_physicsType;
        marker.frameCount = sprites.Length;
        marker.importTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        marker.sourceAssetPath = AssetDatabase.GetAssetPath(sprites[0]);
        ArtAssetClassifier.ApplyToMarker(marker, classification);

        // 选中新创建的物体
        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();

        return root;
    }


    private void ConfigureSpriteAnimation(GameObject visual, SpriteRenderer sr, Sprite[] sprites, ArtAssetClassifier.Classification classification)
    {
        if (visual == null || sr == null) return;

        if (classification != null && classification.IsStateDriven)
        {
            var stateAnimator = visual.GetComponent<SpriteStateAnimator>();
            if (stateAnimator == null) stateAnimator = visual.AddComponent<SpriteStateAnimator>();

            stateAnimator.idle.frames = GetStateFramesOrFallback(classification, SpriteStateAnimator.MotionState.Idle);
            stateAnimator.run.frames = GetStateFramesOrFallback(classification, SpriteStateAnimator.MotionState.Run);
            stateAnimator.jump.frames = GetStateFramesOrFallback(classification, SpriteStateAnimator.MotionState.Jump);
            stateAnimator.fall.frames = GetStateFramesOrFallback(classification, SpriteStateAnimator.MotionState.Fall);
            stateAnimator.idle.frameRate = 6f;
            stateAnimator.run.frameRate = 12f;
            stateAnimator.jump.frameRate = 10f;
            stateAnimator.fall.frameRate = 10f;
            stateAnimator.idle.loop = true;
            stateAnimator.run.loop = true;
            stateAnimator.jump.loop = false;
            stateAnimator.fall.loop = false;
            stateAnimator.playOnStart = true;
            return;
        }

        if (sprites != null && sprites.Length > 1)
        {
            var frameAnimator = visual.GetComponent<SpriteFrameAnimator>();
            if (frameAnimator == null) frameAnimator = visual.AddComponent<SpriteFrameAnimator>();
            frameAnimator.frames = sprites;
            frameAnimator.frameRate = 10f;
            frameAnimator.playOnStart = true;
            frameAnimator.loop = true;
        }
    }

    private Sprite[] GetStateFramesOrFallback(ArtAssetClassifier.Classification classification, SpriteStateAnimator.MotionState state)
    {
        if (TryGetStateFrames(classification, state, out Sprite[] frames))
        {
            return frames;
        }

        switch (state)
        {
            case SpriteStateAnimator.MotionState.Idle:
                // 只有 RUN 时，待机只用第一帧静态兜底，避免站着也一直跑。
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Run, out frames)) return FirstFrameOnly(frames);
                break;

            case SpriteStateAnimator.MotionState.Run:
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Idle, out frames)) return frames;
                break;

            case SpriteStateAnimator.MotionState.Jump:
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Fall, out frames)) return FirstFrameOnly(frames);
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Idle, out frames)) return FirstFrameOnly(frames);
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Run, out frames)) return FirstFrameOnly(frames);
                break;

            case SpriteStateAnimator.MotionState.Fall:
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Jump, out frames)) return FirstFrameOnly(frames);
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Idle, out frames)) return FirstFrameOnly(frames);
                if (TryGetStateFrames(classification, SpriteStateAnimator.MotionState.Run, out frames)) return FirstFrameOnly(frames);
                break;
        }

        return FirstAnyStateFrame(classification);
    }

    private bool TryGetStateFrames(ArtAssetClassifier.Classification classification, SpriteStateAnimator.MotionState state, out Sprite[] frames)
    {
        frames = null;
        return classification != null && classification.stateFrames != null &&
            classification.stateFrames.TryGetValue(state, out frames) && frames != null && frames.Length > 0;
    }

    private Sprite[] FirstFrameOnly(Sprite[] frames)
    {
        return frames != null && frames.Length > 0 && frames[0] != null ? new Sprite[] { frames[0] } : new Sprite[0];
    }

    private Sprite[] FirstAnyStateFrame(ArtAssetClassifier.Classification classification)
    {
        if (classification == null || classification.stateFrames == null) return new Sprite[0];
        foreach (var pair in classification.stateFrames)
        {
            if (pair.Value != null && pair.Value.Length > 0) return FirstFrameOnly(pair.Value);
        }
        return new Sprite[0];
    }

    private void SetupPhysics(GameObject root, SpriteRenderer sr)
    {
        switch (_physicsType)
        {
            case AssetPhysicsType.Character:
                var charCol = root.AddComponent<BoxCollider2D>();
                // [红线保护] 必须引用 PhysicsMetrics 常量，禁止硬编码
                charCol.size = new Vector2(PhysicsMetrics.MARIO_COLLIDER_WIDTH, PhysicsMetrics.MARIO_COLLIDER_HEIGHT);
                charCol.offset = new Vector2(0f, PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y);
                break;

            case AssetPhysicsType.Environment:
                var envCol = root.AddComponent<BoxCollider2D>();
                // 默认 1x1 格大小，用户可后续调整
                envCol.size = new Vector2(1f, 1f);
                break;

            case AssetPhysicsType.Hazard:
                var hazCol = root.AddComponent<BoxCollider2D>();
                hazCol.size = new Vector2(0.9f, 0.35f);
                hazCol.isTrigger = true;
                // 自动挂载 DamageDealer，确保碰撞就能造成伤害
                if (root.GetComponent<DamageDealer>() == null)
                    root.AddComponent<DamageDealer>();
                break;

            case AssetPhysicsType.VFX:
                // 纯特效不加碰撞体
                break;

            case AssetPhysicsType.Prop:
                var propCol = root.AddComponent<BoxCollider2D>();
                propCol.size = new Vector2(0.6f, 0.6f);
                propCol.isTrigger = true;
                break;
        }
    }

    // =========================================================================
    // Step 4: 保存 Prefab
    // =========================================================================
    private void SaveAsPrefab(GameObject obj, string baseName)
    {
        EnsureDirectory(_prefabOutputFolder);
        string prefabPath = $"{_prefabOutputFolder}/{baseName}.prefab";
        prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

        PrefabUtility.SaveAsPrefabAssetAndConnect(obj, prefabPath, InteractionMode.UserAction);
        Debug.Log($"[Asset Import Pipeline] Prefab 已保存: {prefabPath}");
    }

    // =========================================================================
    // 工具方法
    // =========================================================================
    // [AI防坑警告] Pivot 解析统一走 PivotPresetUtility，支持用户手动覆盖。
    // Auto 模式下仍按 physicsType 推断（Character→BottomCenter，其他→Center）。
    private Vector2 GetPivotForType(AssetPhysicsType type)
    {
        var resolved = PivotPresetUtility.ResolvePreset(_pivotPreset, (int)type);
        return PivotPresetUtility.PivotToVector2(resolved, _customPivot);
    }

    private int GetAlignmentForType(AssetPhysicsType type)
    {
        var resolved = PivotPresetUtility.ResolvePreset(_pivotPreset, (int)type);
        return PivotPresetUtility.PivotToAlignment(resolved);
    }

    private string GetPhysicsTypeHint(AssetPhysicsType type)
    {
        switch (type)
        {
            case AssetPhysicsType.Character:
                return "角色/敌人: Bottom Center 重心, 标准角色碰撞体 (0.8x0.95)";
            case AssetPhysicsType.Environment:
                return "地形/平台: Center 重心, 1x1 碰撞体, 支持 Tiled 模式";
            case AssetPhysicsType.Hazard:
                return "陷阱/机关: Center 重心, Trigger 碰撞体 (0.9x0.35)";
            case AssetPhysicsType.VFX:
                return "纯特效: Center 重心, 无碰撞体";
            case AssetPhysicsType.Prop:
                return "道具/收集物: Center 重心, Trigger 碰撞体 (0.6x0.6)";
            default:
                return "";
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}

#endif
