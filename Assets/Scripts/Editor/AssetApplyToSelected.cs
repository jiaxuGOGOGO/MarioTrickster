#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// AssetApplyToSelected — 将商业素材应用到场景中已有物体上
///
/// 核心职责：
///   用户在场景中选中一个已有的 Object（比如一键生成关卡后的展位物体），
///   然后通过此工具将导入的美术素材（Sprite/SpriteSheet）直接"穿"到这个物体上，
///   自动完成：替换贴图 → 配 SEF Material → 挂 SpriteEffectController →
///   根据物体已有组件自动配置行为逻辑（碰撞、爆炸、伤害等）。
///
/// 与 AssetImportPipeline 的区别：
///   - AssetImportPipeline: 从零创建新物体
///   - AssetApplyToSelected: 把美术素材"穿"到已有物体上，保留已有的行为组件和层级结构
///
/// 工作流：
///   一键生成关卡 → 选中展位物体 → 打开此窗口 → 拖入素材 → 点"应用" → 完成
///
/// [AI防坑警告]
///   1. 必须保留物体已有的行为组件（BaseHazard/DamageDealer/ControllableHazard 等）
///   2. 替换贴图时必须走 ART_BIBLE 规范（PPU=32, Point, Uncompressed）
///   3. 如果物体没有 SpriteRenderer，自动在 Visual 子物体上创建
/// </summary>
public class AssetApplyToSelected : EditorWindow
{
    [MenuItem("MarioTrickster/Apply Art to Selected %#a", false, 201)]
    public static void ShowWindow()
    {
        var win = GetWindow<AssetApplyToSelected>("应用素材到选中物体");
        win.minSize = new Vector2(420, 550);
    }

    // =========================================================================
    // 枚举：行为模板
    // =========================================================================
    private enum BehaviorTemplate
    {
        KeepExisting = 0,     // 保留已有组件，不改动
        Hazard_Explosive = 1, // 碰撞爆炸型（DamageDealer + 爆炸特效）
        Hazard_Contact = 2,   // 接触伤害型（DamageDealer）
        Hazard_SawBlade = 3,  // 旋转锯片型（SawBlade）
        Prop_Collectible = 4, // 可收集道具
        AutoDetect = 5        // 根据已有组件自动判断
    }

    private enum ArtInputKind
    {
        None = 0,
        Folder = 1,
        Texture = 2,
        Sprite = 3
    }

    private enum SpriteSheetSliceMode
    {
        AutoDetect = 0,
        ManualGrid = 1,
        ManualCellSize = 2,
        AIBackend = 3
    }

    [Serializable]
    private class AISliceAnalysis
    {
        public string asset_kind;
        public int frame_count;
        public int grid_cols;
        public int grid_rows;
        public int cell_width;
        public int cell_height;
        public int x;
        public int y;
        public int w;
        public int h;
        public string confidence;
        public string reason;
    }

    private struct SlicePlan
    {
        public int cols;
        public int rows;
        public int frameW;
        public int frameH;
        public int x;
        public int yFromTop;
        public int width;
        public int height;
        public string source;

        public int FrameCount => Mathf.Max(0, cols * rows);
        public bool IsValid => cols > 0 && rows > 0 && frameW > 0 && frameH > 0 && width > 0 && height > 0 && FrameCount > 1;
    }

    // =========================================================================
    // 状态
    // =========================================================================
    private DefaultAsset _artFolder;
    private Texture2D _artTexture;
    private Sprite _artSprite;
    private Sprite[] _artSprites = new Sprite[0];
    private ArtInputKind _activeArtInput = ArtInputKind.None;
    private BehaviorTemplate _behaviorTemplate = BehaviorTemplate.AutoDetect;
    private bool _attachSEF = true;
    private bool _autoSavePrefab = true;
    private bool _normalizeSettings = true;
    private string _prefabFolder = "Assets/Art/Prefabs";
    private Vector2 _scrollPos;
    private string _lastResult = "";

    // Sprite Sheet 切片策略
    private SpriteSheetSliceMode _sliceMode = SpriteSheetSliceMode.AutoDetect;
    private int _manualColumns = 1;
    private int _manualRows = 1;
    private int _manualCellWidth = 32;
    private int _manualCellHeight = 32;
    private int _sliceOffsetX = 0;
    private int _sliceOffsetY = 0;
    private bool _showAdvancedSliceArea = false;

    // AI 后台识别配置：复用 AI Smart Slicer 的 EditorPrefs 契约，不进入运行时 Build。
    private string _apiKey = "";
    private string _baseUrl = "https://api.openai.com/v1";
    private string _model = "gpt-4.1-mini";
    private bool _showApiSettings = false;
    private bool _isAiAnalyzing = false;
    private string _aiStatus = "";
    private AISliceAnalysis _aiAnalysis = null;

    // Pivot 选择
    private PivotPresetUtility.PivotPreset _pivotPreset = PivotPresetUtility.PivotPreset.Auto;
    private Vector2 _customPivot = new Vector2(0.5f, 0.5f);

    // 爆炸参数（仅 Hazard_Explosive 模式）
    private int _explosionDamage = 3;
    private float _explosionRadius = 2f;
    private float _explosionForce = 8f;

    // =========================================================================
    // GUI
    // =========================================================================
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(8);
        GUILayout.Label("Apply Art to Selected", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "选中场景中的物体 → 拖入美术素材 → 点击应用\n" +
            "素材会自动替换到物体上，保留已有的行为逻辑（碰撞/爆炸/伤害等）。\n" +
            "如果物体是展位占位符，会自动配置对应的行为组件。",
            MessageType.Info);

        // 当前选中物体
        EditorGUILayout.Space(4);
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorGUILayout.HelpBox("请先在场景中选中一个物体", MessageType.Warning);
        }
        else
        {
            GameObject applyTarget = ResolveApplyTarget(selected);
            EditorGUILayout.LabelField("当前选中:", selected.name, EditorStyles.boldLabel);
            if (applyTarget != selected)
            {
                EditorGUILayout.LabelField("实际换皮对象:", applyTarget.name, EditorStyles.miniBoldLabel);
            }
            DrawExistingComponentsInfo(applyTarget);
        }

        // 素材输入
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("美术素材", EditorStyles.boldLabel);

        DrawArtInputFields();
        ReconcileArtSelection();
        DrawTextureSliceSettings();

        if (_artFolder != null)
        {
            _artSprites = LoadSpritesFromFolder(_artFolder);
            if (_artSprites.Length > 0)
            {
                var preview = ArtAssetClassifier.Classify(selected != null ? ResolveApplyTarget(selected) : null, _artSprites, -1);
                string modeHint = preview.IsStateDriven ? "应用后自动挂 SpriteStateAnimator。" : "应用后按分类结果保持现有行为或挂循环/一次性动画。";
                EditorGUILayout.HelpBox($"已识别文件夹散帧：{_artSprites.Length} 帧；角色={preview.role}；模式={preview.animationMode}；状态={preview.StateSummary}；{modeHint}", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox("文件夹里没有找到 Sprite。请使用 hero_idle_00 / hero_run_00 / hero_jump_00 / hero_fall_00 这类命名。", MessageType.Warning);
            }
        }
        else if (_artTexture != null)
        {
            _artSprites = LoadSpritesFromTexture(_artTexture);
            if (_artSprites.Length > 0)
            {
                var preview = ArtAssetClassifier.Classify(selected != null ? ResolveApplyTarget(selected) : null, _artSprites, -1);
                string frameHint = preview != null && preview.IsStateDriven
                    ? $"已识别角色状态 Sprite Sheet：{_artSprites.Length} 帧；状态={preview.StateSummary}；应用后左右移动会由 SpriteStateAnimator 驱动。"
                    : (_artSprites.Length > 1
                        ? $"已识别 Sprite Sheet：{_artSprites.Length} 帧；角色={preview.role}；模式={preview.animationMode}；状态={preview.StateSummary}；应用后会按分类结果挂循环/一次性动画或保留现有行为。"
                        : GetTextureSingleFrameHint(_artTexture, _artSprites[0]));
                EditorGUILayout.HelpBox(frameHint, MessageType.None);
            }
        }
        else if (_artSprite != null)
        {
            _artSprites = LoadSpritesFromSprite(_artSprite);
            if (_artSprites.Length > 1)
            {
                var preview = ArtAssetClassifier.Classify(selected != null ? ResolveApplyTarget(selected) : null, _artSprites, -1);
                string frameHint = preview != null && preview.IsStateDriven
                    ? $"已识别同图集角色状态帧：{_artSprites.Length} 帧；状态={preview.StateSummary}；应用后左右移动会由 SpriteStateAnimator 驱动。"
                    : $"已识别同图集 Sprite Sheet：{_artSprites.Length} 帧，应用后会自动播放。";
                EditorGUILayout.HelpBox(frameHint, MessageType.None);
            }
        }

        // Pivot 设置
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Pivot 设置", EditorStyles.boldLabel);
        {
            if (selected != null)
            {
                // 有选中物体：根据目标推断最佳 Pivot
                var resolvedTarget = ResolveApplyTarget(selected);
                var autoResolved = PivotPresetUtility.AutoDetectFromPhysicsType(
                    GetPhysicsTypeHint(resolvedTarget), resolvedTarget);
                PivotPresetUtility.DrawPivotSelector("Pivot 预设", ref _pivotPreset, ref _customPivot, autoResolved);
            }
            else
            {
                // 未选中物体：显示默认但加提示
                PivotPresetUtility.DrawPivotSelector("Pivot 预设", ref _pivotPreset, ref _customPivot, PivotPresetUtility.PivotPreset.BottomCenter);
                EditorGUILayout.HelpBox(
                    "ℹ 尚未选中场景物体。Auto 模式会在应用时根据目标物体自动推断（角色→脚底，其他→居中）。\n" +
                    "如需确保特定 Pivot，请手动选择具体预设（如 Bottom Center）。",
                    MessageType.Info);
            }
        }

        // 行为模板
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("行为配置", EditorStyles.boldLabel);
        _behaviorTemplate = (BehaviorTemplate)EditorGUILayout.EnumPopup("行为模板", _behaviorTemplate);
        EditorGUILayout.HelpBox(GetBehaviorHint(_behaviorTemplate), MessageType.None);

        // 爆炸参数
        if (_behaviorTemplate == BehaviorTemplate.Hazard_Explosive)
        {
            EditorGUI.indentLevel++;
            _explosionDamage = EditorGUILayout.IntField("爆炸伤害", _explosionDamage);
            _explosionRadius = EditorGUILayout.FloatField("爆炸半径", _explosionRadius);
            _explosionForce = EditorGUILayout.FloatField("击退力度", _explosionForce);
            EditorGUI.indentLevel--;
        }

        // 选项
        EditorGUILayout.Space(4);
        _normalizeSettings = EditorGUILayout.Toggle("规范化贴图设置 (PPU=32)", _normalizeSettings);
        _attachSEF = EditorGUILayout.Toggle("挂载 SEF 效果控制器", _attachSEF);
        _autoSavePrefab = EditorGUILayout.Toggle("应用后自动保存 Prefab", _autoSavePrefab);
        if (_autoSavePrefab)
        {
            _prefabFolder = EditorGUILayout.TextField("Prefab 目录", _prefabFolder);
        }

        // 执行按钮
        EditorGUILayout.Space(12);
        GUI.enabled = selected != null && (_artSprite != null || _artTexture != null || _artFolder != null);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
        if (GUILayout.Button("应用素材到选中物体", GUILayout.Height(36)))
        {
            ApplyArtToSelected(selected);
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // 结果
        if (!string.IsNullOrEmpty(_lastResult))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_lastResult, MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    // =========================================================================
    // 选择归一：用户可点 Root，也可点 Visual；工具永远落到 Root 执行换皮
    // =========================================================================
    private GameObject ResolveApplyTarget(GameObject selected)
    {
        if (selected == null) return null;

        // [AI防坑警告] Apply Art 的语义是“给已有白盒 Root 换皮”，不是把行为组件写到 Visual 上。
        // 由于 Level Studio 支持 Visual 模式，用户很容易点到 Visual；这里必须后台归一回 Root，避免破坏视碰分离。
        // S123: 不能只认名字叫 "Visual" 的子节点。商业素材导入后 Visual 可能被重命名、套了一层，
        // 或用户直接点到 SpriteRenderer 子节点；只要父级存在 Mario/Trickster 控制器，就必须归一回控制器根节点。
        MarioController marioRoot = selected.GetComponentInParent<MarioController>();
        if (marioRoot != null) return marioRoot.gameObject;

        TricksterController tricksterRoot = selected.GetComponentInParent<TricksterController>();
        if (tricksterRoot != null) return tricksterRoot.gameObject;

        if (selected.name == "Visual" && selected.transform.parent != null)
        {
            return selected.transform.parent.gameObject;
        }

        ImportedAssetMarker marker = selected.GetComponentInParent<ImportedAssetMarker>();
        if (marker != null && marker.transform != selected.transform && selected.GetComponent<SpriteRenderer>() != null)
        {
            return marker.gameObject;
        }

        return selected;
    }

    // =========================================================================
    // 显示已有组件信息
    // =========================================================================
    private void DrawExistingComponentsInfo(GameObject go)
    {
        EditorGUI.indentLevel++;
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var marker = go.GetComponent<ImportedAssetMarker>();
        var hazard = go.GetComponentInChildren<BaseHazard>();
        var dealer = go.GetComponentInChildren<DamageDealer>();
        var ctrl = go.GetComponentInChildren<ControllablePropBase>();
        var sec = go.GetComponentInChildren<SpriteEffectController>();

        string info = "";
        if (sr != null) info += $"SpriteRenderer: {(sr.sprite != null ? sr.sprite.name : "无贴图")}  ";
        if (marker != null) info += $"| 导入标记: {marker.sourceAssetPath}  ";
        if (hazard != null) info += $"| 陷阱: {hazard.GetType().Name}  ";
        if (dealer != null) info += "| DamageDealer  ";
        if (ctrl != null) info += $"| 可操控: {ctrl.GetType().Name}  ";
        if (sec != null) info += "| SEF控制器  ";

        if (string.IsNullOrEmpty(info))
            info = "（空物体，无特殊组件）";

        EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        EditorGUI.indentLevel--;
    }

    private void OnEnable()
    {
        LoadAiPrefs();
    }

    private void DrawArtInputFields()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "角色状态帧文件夹", _artFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            SetArtFolder(newFolder);
        }
        using (new EditorGUI.DisabledScope(_artFolder == null))
        {
            if (GUILayout.Button("清空", GUILayout.Width(52)))
            {
                SetArtFolder(null);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        Texture2D newTexture = (Texture2D)EditorGUILayout.ObjectField(
            "贴图 (Texture2D)", _artTexture, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            SetArtTexture(newTexture);
        }
        using (new EditorGUI.DisabledScope(_artTexture == null))
        {
            if (GUILayout.Button("清空", GUILayout.Width(52)))
            {
                SetArtTexture(null);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        Sprite newSprite = (Sprite)EditorGUILayout.ObjectField(
            "或直接拖 Sprite", _artSprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck())
        {
            SetArtSprite(newSprite);
        }
        using (new EditorGUI.DisabledScope(_artSprite == null))
        {
            if (GUILayout.Button("清空", GUILayout.Width(52)))
            {
                SetArtSprite(null);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SetArtFolder(DefaultAsset folder)
    {
        _artFolder = folder;
        if (folder != null)
        {
            _activeArtInput = ArtInputKind.Folder;
            _artTexture = null;
            _artSprite = null;
        }
        else if (_activeArtInput == ArtInputKind.Folder)
        {
            _activeArtInput = ArtInputKind.None;
        }
        _artSprites = new Sprite[0];
        ResetSliceAnalysis();
    }

    private void SetArtTexture(Texture2D texture)
    {
        _artTexture = texture;
        if (texture != null)
        {
            _activeArtInput = ArtInputKind.Texture;
            _artFolder = null;
            _artSprite = null;
        }
        else if (_activeArtInput == ArtInputKind.Texture)
        {
            _activeArtInput = ArtInputKind.None;
        }
        _artSprites = new Sprite[0];
        ResetSliceAnalysis();
    }

    private void SetArtSprite(Sprite sprite)
    {
        _artSprite = sprite;
        if (sprite != null)
        {
            _activeArtInput = ArtInputKind.Sprite;
            _artFolder = null;
            _artTexture = null;
        }
        else if (_activeArtInput == ArtInputKind.Sprite)
        {
            _activeArtInput = ArtInputKind.None;
        }
        _artSprites = new Sprite[0];
        ResetSliceAnalysis();
    }

    private void ResetSliceAnalysis()
    {
        _aiAnalysis = null;
        _aiStatus = "";
    }

    private void ReconcileArtSelection()
    {
        if (_activeArtInput == ArtInputKind.Folder && _artFolder == null) _activeArtInput = ArtInputKind.None;
        if (_activeArtInput == ArtInputKind.Texture && _artTexture == null) _activeArtInput = ArtInputKind.None;
        if (_activeArtInput == ArtInputKind.Sprite && _artSprite == null) _activeArtInput = ArtInputKind.None;

        // 兼容窗口热重载或历史状态中多个槽同时残留的情况：文件夹优先，其次 Texture2D，最后 Sprite。
        if (_activeArtInput == ArtInputKind.None)
        {
            if (_artFolder != null) _activeArtInput = ArtInputKind.Folder;
            else if (_artTexture != null) _activeArtInput = ArtInputKind.Texture;
            else if (_artSprite != null) _activeArtInput = ArtInputKind.Sprite;
        }

        switch (_activeArtInput)
        {
            case ArtInputKind.Folder:
                _artTexture = null;
                _artSprite = null;
                break;
            case ArtInputKind.Texture:
                _artFolder = null;
                _artSprite = null;
                break;
            case ArtInputKind.Sprite:
                _artFolder = null;
                _artTexture = null;
                break;
        }
    }


    private void DrawTextureSliceSettings()
    {
        if (_artTexture == null) return;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Sprite Sheet 切片", EditorStyles.boldLabel);
        _sliceMode = (SpriteSheetSliceMode)EditorGUILayout.EnumPopup("切片模式", _sliceMode);

        if (TryBuildSlicePlan(_artTexture, GetPhysicsTypeHint(Selection.activeGameObject != null ? ResolveApplyTarget(Selection.activeGameObject) : null), out SlicePlan previewPlan))
        {
            EditorGUILayout.HelpBox($"当前切片计划：{previewPlan.cols} 列 × {previewPlan.rows} 行，单帧 {previewPlan.frameW}×{previewPlan.frameH}，预计 {previewPlan.FrameCount} 帧（{previewPlan.source}）。点击应用时会按该计划写入 Unity Sprite 切片。", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("当前贴图会按单帧处理；如果这是 Sprite Sheet，请选择 AI 后台识别或手动指定网格/单帧尺寸。", MessageType.Warning);
        }

        if (_sliceMode == SpriteSheetSliceMode.ManualGrid)
        {
            EditorGUI.indentLevel++;
            _manualColumns = Mathf.Max(1, EditorGUILayout.IntField("Columns / 列数", _manualColumns));
            _manualRows = Mathf.Max(1, EditorGUILayout.IntField("Rows / 行数", _manualRows));
            if (_artTexture != null)
            {
                int usableW = Mathf.Max(1, _artTexture.width - _sliceOffsetX);
                int usableH = Mathf.Max(1, _artTexture.height - _sliceOffsetY);
                _manualCellWidth = Mathf.Max(1, usableW / _manualColumns);
                _manualCellHeight = Mathf.Max(1, usableH / _manualRows);
                EditorGUILayout.LabelField("推导 Cell Size", $"{_manualCellWidth} × {_manualCellHeight}", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }
        else if (_sliceMode == SpriteSheetSliceMode.ManualCellSize)
        {
            EditorGUI.indentLevel++;
            _manualCellWidth = Mathf.Max(1, EditorGUILayout.IntField("Cell Width", _manualCellWidth));
            _manualCellHeight = Mathf.Max(1, EditorGUILayout.IntField("Cell Height", _manualCellHeight));
            if (_artTexture != null)
            {
                int usableW = Mathf.Max(1, _artTexture.width - _sliceOffsetX);
                int usableH = Mathf.Max(1, _artTexture.height - _sliceOffsetY);
                _manualColumns = Mathf.Max(1, usableW / _manualCellWidth);
                _manualRows = Mathf.Max(1, usableH / _manualCellHeight);
                EditorGUILayout.LabelField("推导 Rows / Columns", $"{_manualColumns} 列 × {_manualRows} 行", EditorStyles.miniLabel);
            }
            EditorGUI.indentLevel--;
        }
        else if (_sliceMode == SpriteSheetSliceMode.AIBackend)
        {
            DrawAiBackendControls();
        }

        _showAdvancedSliceArea = EditorGUILayout.Foldout(_showAdvancedSliceArea, "高级：忽略左上边距 / 只切部分区域");
        if (_showAdvancedSliceArea)
        {
            EditorGUI.indentLevel++;
            _sliceOffsetX = Mathf.Max(0, EditorGUILayout.IntField("Offset X", _sliceOffsetX));
            _sliceOffsetY = Mathf.Max(0, EditorGUILayout.IntField("Offset Y (from top)", _sliceOffsetY));
            EditorGUILayout.HelpBox("用于带外边距或图集边框的商业素材。AI 后台识别成功时会自动填入识别区域，手动模式则默认从该偏移开始切到贴图边缘。", MessageType.None);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawAiBackendControls()
    {
        LoadAiPrefs();

        bool hasConfig = !string.IsNullOrEmpty(_apiKey);
        EditorGUILayout.LabelField($"API 状态: {(hasConfig ? "已配置" : "未配置")}", EditorStyles.miniLabel);
        _showApiSettings = EditorGUILayout.Foldout(_showApiSettings, hasConfig ? "修改 AI API 设置" : "配置 AI API");
        if (_showApiSettings)
        {
            EditorGUI.indentLevel++;
            string newKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            if (newKey != _apiKey)
            {
                _apiKey = newKey;
                EditorPrefs.SetString("AI_SmartSlicer_APIKey", _apiKey);
            }
            string newUrl = EditorGUILayout.TextField("Base URL", _baseUrl);
            if (newUrl != _baseUrl)
            {
                _baseUrl = newUrl;
                EditorPrefs.SetString("AI_SmartSlicer_BaseUrl", _baseUrl);
            }
            string newModel = EditorGUILayout.TextField("Model", _model);
            if (newModel != _model)
            {
                _model = newModel;
                EditorPrefs.SetString("AI_SmartSlicer_Model", _model);
            }
            EditorGUI.indentLevel--;
        }

        using (new EditorGUI.DisabledScope(!hasConfig || _isAiAnalyzing || _artTexture == null))
        {
            if (GUILayout.Button(_isAiAnalyzing ? "AI 正在识别..." : "AI 后台识别切片方式", GUILayout.Height(28)))
            {
                RunAiSliceAnalysis();
            }
        }

        if (!string.IsNullOrEmpty(_aiStatus))
        {
            EditorGUILayout.HelpBox(_aiStatus, _aiStatus.Contains("失败") ? MessageType.Error : MessageType.Info);
        }

        if (_aiAnalysis != null)
        {
            EditorGUILayout.LabelField("AI 判断", $"{_aiAnalysis.grid_cols}×{_aiAnalysis.grid_rows} / Cell {_aiAnalysis.cell_width}×{_aiAnalysis.cell_height} / {_aiAnalysis.confidence}", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(_aiAnalysis.reason))
            {
                EditorGUILayout.LabelField("原因", _aiAnalysis.reason, EditorStyles.wordWrappedMiniLabel);
            }
        }
    }

    private void LoadAiPrefs()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiKey = EditorPrefs.GetString("AI_SmartSlicer_APIKey", "");
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        }
        string savedUrl = EditorPrefs.GetString("AI_SmartSlicer_BaseUrl", "");
        if (!string.IsNullOrEmpty(savedUrl)) _baseUrl = savedUrl;
        string savedModel = EditorPrefs.GetString("AI_SmartSlicer_Model", "");
        if (!string.IsNullOrEmpty(savedModel)) _model = savedModel;
    }


    // =========================================================================
    // 核心逻辑：应用素材到选中物体
    // =========================================================================
    private void ApplyArtToSelected(GameObject target)
    {
        target = ResolveApplyTarget(target);
        if (target == null) return;

        Undo.RegisterCompleteObjectUndo(target, "Apply Art to Selected");

        // Step 1: 规范化贴图 / 文件夹批量贴图，并在真正应用前完成切片
        if (_artFolder != null)
        {
            PrepareFolderTexturesForApply(_artFolder, GetPhysicsTypeHint(target), target);
        }

        if (_artTexture != null)
        {
            if (_normalizeSettings)
            {
                NormalizeTexture(_artTexture);
            }
            AutoSliceTextureSheetIfNeeded(_artTexture, GetPhysicsTypeHint(target), target);
        }

        // Step 1.5: 强制更新 Pivot（无论切片是否被跳过）
        // [AI防坑警告] AutoSliceTextureSheetIfNeeded 在已切片贴图 + AutoDetect 模式下会跳过，
        // 导致用户选择的 Pivot 永远不会写入。此步骤独立于切片逻辑，确保 Pivot 始终生效。
        ApplyPivotToTextureSprites(_artTexture, _artSprite, GetPhysicsTypeHint(target), target);

        // Step 2: 确保 Sprite / Sprite Sheet 帧可用
        Sprite[] spritesToApply = ResolveSpritesForApply();
        if (spritesToApply.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "无法获取有效的 Sprite", "好的");
            return;
        }
        Sprite primarySprite = spritesToApply[0];

        // Step 3: 找到或创建 SpriteRenderer
        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            // 遵循 S37 视碰分离架构：在 Visual 子物体上创建
            Transform visual = target.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visualGO = new GameObject("Visual");
                Undo.RegisterCreatedObjectUndo(visualGO, "Create Visual");
                visualGO.transform.SetParent(target.transform);
                visualGO.transform.localPosition = Vector3.zero;
                visualGO.transform.localScale = Vector3.one;
                visual = visualGO.transform;
            }
            sr = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
        }

        // Step 4: 统一分类，并按素材语义配置循环动画或状态动画
        ArtAssetClassifier.Classification classification = ArtAssetClassifier.Classify(target, spritesToApply, GetPhysicsTypeHint(target));
        Undo.RecordObject(sr, "Replace Sprite");
        sr.sprite = spritesToApply[0];
        ConfigureSpriteAnimation(sr, spritesToApply, classification);

        // Step 5: 配置 SEF Material + Controller
        if (_attachSEF)
        {
            EnsureSEFMaterial(sr);
            var secCtrl = sr.gameObject.GetComponent<SpriteEffectController>();
            if (secCtrl == null)
            {
                secCtrl = Undo.AddComponent<SpriteEffectController>(sr.gameObject);
            }
        }

        // Step 6: 应用行为模板
        ApplyBehaviorTemplate(target, sr, classification);

        // Step 6.5: 角色换皮后再次加固移动控制链路，避免历史错误应用留下 Static/Trigger/Freeze 状态。
        EnsureCharacterControlChain(target);

        // Step 7: 更新 ImportedAssetMarker
        UpdateMarker(target, spritesToApply, classification);

        // Step 8: 自动保存 Prefab
        if (_autoSavePrefab)
        {
            SavePrefab(target);
        }

        // 标记脏
        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(sr);
        SceneView.RepaintAll();

        // Pivot 信息
        var resolvedPivot = PivotPresetUtility.ResolvePreset(
            _pivotPreset, GetPhysicsTypeHint(target), target);
        string pivotHint = $"Pivot: {PivotPresetUtility.GetPresetDisplayName(resolvedPivot)}";
        if (_pivotPreset == PivotPresetUtility.PivotPreset.Custom)
            pivotHint += $" ({_customPivot.x:F2}, {_customPivot.y:F2})";

        string animationHint = classification != null ? $"，素材分类: {classification.role}/{classification.animationMode}" : "";
        _lastResult = spritesToApply.Length > 1
            ? $"已将 [{primarySprite.name}] 等 {spritesToApply.Length} 帧应用到 [{target.name}]，{pivotHint}，已自动配置动画，行为模板: {_behaviorTemplate}{animationHint}"
            : $"已将 [{primarySprite.name}] 应用到 [{target.name}]，{pivotHint}，行为模板: {_behaviorTemplate}{animationHint}";
        Debug.Log($"[AssetApplyToSelected] {_lastResult}");
    }

    // =========================================================================
    // 行为模板应用
    // =========================================================================
    private void ApplyBehaviorTemplate(GameObject target, SpriteRenderer sr, ArtAssetClassifier.Classification classification)
    {
        if (PivotPresetUtility.HasCharacterControllerPublic(target))
        {
            // [AI防坑警告] Apply Art 给 Mario/Trickster 换皮时，只能改 Visual/SpriteStateAnimator。
            // 禁止按商业素材名或已有 Trigger 误套陷阱/道具模板，否则会把角色 Rigidbody 改成 Static、
            // 或把 Collider 改成 Trigger，直接造成“换皮后不能移动”的回归。
            EnsureCharacterControlChain(target);
            Debug.Log($"[AssetApplyToSelected] 角色换皮保护：跳过行为模板和碰撞体自适应，仅更新视觉素材: {target.name}");
            return;
        }

        BehaviorTemplate template = _behaviorTemplate;

        // AutoDetect: 先尊重已有组件；没有明确行为时，再按商业素材分类自动落到道具/陷阱等用途。
        if (template == BehaviorTemplate.AutoDetect)
        {
            template = DetectBehaviorFromExisting(target);
            if (template == BehaviorTemplate.KeepExisting)
            {
                template = ResolveBehaviorTemplateFromClassification(classification);
            }
        }

        switch (template)
        {
            case BehaviorTemplate.KeepExisting:
                // 不改动任何行为组件
                break;

            case BehaviorTemplate.Hazard_Explosive:
                SetupExplosiveHazard(target);
                break;

            case BehaviorTemplate.Hazard_Contact:
                SetupContactHazard(target);
                break;

            case BehaviorTemplate.Hazard_SawBlade:
                SetupSawBlade(target);
                break;

            case BehaviorTemplate.Prop_Collectible:
                SetupCollectibleProp(target);
                break;
        }

        // 确保碰撞体尺寸匹配新贴图
        AutoFitCollider(target, sr);
    }

    private BehaviorTemplate ResolveBehaviorTemplateFromClassification(ArtAssetClassifier.Classification classification)
    {
        if (classification == null) return BehaviorTemplate.KeepExisting;

        switch (classification.runtimeBehavior)
        {
            case ArtAssetClassifier.RuntimeBehavior.HazardContact:
                return BehaviorTemplate.Hazard_Contact;
            case ArtAssetClassifier.RuntimeBehavior.PickupConsume:
                return BehaviorTemplate.Prop_Collectible;
            default:
                return BehaviorTemplate.KeepExisting;
        }
    }

    private void EnsureCharacterControlChain(GameObject target)
    {
        if (target == null || !PivotPresetUtility.HasCharacterControllerPublic(target)) return;

        // S123: 文件夹整组换皮后的“能跳/能动画但不能左右移动”，最常见不是 SpriteStateAnimator，
        // 而是角色根节点被旧模板或误选 Visual 后留下了 Static/Trigger/Freeze、Root 缩放、visualTransform 或 InputManager 引用断链。
        // 本方法只修复“移动控制链路”字段，不根据新贴图重算碰撞体尺寸，继续遵守视碰分离原则。
        Undo.RecordObject(target.transform, "Protect Character Root Transform");
        target.transform.localScale = Vector3.one;

        var rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody2D>(target);
        }
        Undo.RecordObject(rb, "Protect Character Rigidbody");
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.simulated = true;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var box = target.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider2D>(target);
        }
        Undo.RecordObject(box, "Protect Character Collider");
        box.enabled = true;
        box.isTrigger = false;

        // 只在碰撞体明显不可用时兜底恢复标准值；避免每次换皮都挪动物理真相，造成卡地或位移回归。
        bool invalidSize = box.size.x <= 0.01f || box.size.y <= 0.01f;
        if (invalidSize)
        {
            bool isTrickster = HasComponentNamed(target, "TricksterController");
            float stdWidth = isTrickster ? PhysicsMetrics.TRICKSTER_COLLIDER_WIDTH : PhysicsMetrics.MARIO_COLLIDER_WIDTH;
            float stdHeight = isTrickster ? PhysicsMetrics.TRICKSTER_COLLIDER_HEIGHT : PhysicsMetrics.MARIO_COLLIDER_HEIGHT;
            float stdOffsetY = isTrickster ? PhysicsMetrics.TRICKSTER_COLLIDER_OFFSET_Y : PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
            box.size = new Vector2(stdWidth, stdHeight);
            box.offset = new Vector2(0f, stdOffsetY);
        }

        Transform visual = ResolveCharacterVisualTransform(target);
        if (visual != null)
        {
            Undo.RecordObject(visual, "Protect Character Visual Transform");
            visual.localPosition = Vector3.zero;
            if (Mathf.Approximately(visual.localScale.x, 0f) || Mathf.Approximately(visual.localScale.y, 0f))
            {
                visual.localScale = Vector3.one;
            }
        }

        RepairCharacterControllerVisualReferences(target, visual);
        RepairInputManagerCharacterReferences(target);

        EditorUtility.SetDirty(target);
    }

    private Transform ResolveCharacterVisualTransform(GameObject target)
    {
        if (target == null) return null;

        Transform visual = target.transform.Find("Visual");
        if (visual != null) return visual;

        SpriteRenderer sr = target.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null) return sr.transform;

        GameObject visualGO = new GameObject("Visual");
        Undo.RegisterCreatedObjectUndo(visualGO, "Create Character Visual");
        visualGO.transform.SetParent(target.transform);
        visualGO.transform.localPosition = Vector3.zero;
        visualGO.transform.localRotation = Quaternion.identity;
        visualGO.transform.localScale = Vector3.one;
        return visualGO.transform;
    }

    private void RepairCharacterControllerVisualReferences(GameObject target, Transform visual)
    {
        if (target == null || visual == null) return;

        foreach (var controller in target.GetComponents<MonoBehaviour>())
        {
            if (controller == null) continue;
            string typeName = controller.GetType().Name;
            if (typeName != "MarioController" && typeName != "TricksterController") continue;

            Undo.RecordObject(controller, "Repair Character Visual Reference");
            var so = new SerializedObject(controller);
            SerializedProperty visualProp = so.FindProperty("visualTransform");
            if (visualProp != null && visualProp.propertyType == SerializedPropertyType.ObjectReference)
            {
                visualProp.objectReferenceValue = visual;
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(controller);
        }
    }

    private void RepairInputManagerCharacterReferences(GameObject target)
    {
        if (target == null) return;

        MarioController mario = target.GetComponent<MarioController>();
        TricksterController trickster = target.GetComponent<TricksterController>();
        if (mario == null && trickster == null) return;

        foreach (InputManager input in Resources.FindObjectsOfTypeAll<InputManager>())
        {
            if (input == null || EditorUtility.IsPersistent(input)) continue;

            Undo.RecordObject(input, "Repair Character Input Reference");
            if (mario != null) input.SetMarioController(mario);
            if (trickster != null) input.SetTricksterController(trickster);
            EditorUtility.SetDirty(input);
        }
    }

    private bool HasComponentNamed(GameObject target, string typeName)
    {
        if (target == null) return false;
        foreach (var comp in target.GetComponents<MonoBehaviour>())
        {
            if (comp != null && comp.GetType().Name == typeName) return true;
        }
        return false;
    }

    private BehaviorTemplate DetectBehaviorFromExisting(GameObject target)
    {
        // 按优先级检测已有组件
        if (target.GetComponentInChildren<ControllableHazard>() != null)
            return BehaviorTemplate.Hazard_Explosive;
        if (target.GetComponentInChildren<SawBlade>() != null)
            return BehaviorTemplate.Hazard_SawBlade;
        if (target.GetComponentInChildren<BaseHazard>() != null)
            return BehaviorTemplate.Hazard_Contact;
        if (target.GetComponentInChildren<DamageDealer>() != null)
            return BehaviorTemplate.Hazard_Contact;

        // 检查 ImportedAssetMarker 的 physicsType
        var marker = target.GetComponent<ImportedAssetMarker>();
        if (marker != null)
        {
            switch (marker.physicsType)
            {
                case 2: return BehaviorTemplate.Hazard_Contact; // Hazard
                case 4: return BehaviorTemplate.Prop_Collectible; // Prop
            }
        }

        // 检查碰撞体是否是 Trigger
        var col = target.GetComponent<Collider2D>();
        if (col != null && col.isTrigger)
        {
            // Trigger 碰撞体通常是道具或陷阱
            return BehaviorTemplate.Hazard_Contact;
        }

        return BehaviorTemplate.KeepExisting;
    }

    // =========================================================================
    // 各行为模板的具体配置
    // =========================================================================

    /// <summary>
    /// 爆炸型陷阱：碰撞触发 → 造成范围伤害 → 播放爆炸特效 → 自毁
    /// </summary>
    private void SetupExplosiveHazard(GameObject target)
    {
        // 确保有 Collider2D (Trigger)
        var col = target.GetComponent<Collider2D>();
        if (col == null)
        {
            var box = Undo.AddComponent<BoxCollider2D>(target);
            box.isTrigger = true;
            col = box;
        }
        else if (!col.isTrigger)
        {
            Undo.RecordObject(col, "Set Trigger");
            col.isTrigger = true;
        }

        // 确保有 ExplosiveHazard 组件
        var explosive = target.GetComponent<ExplosiveHazard>();
        if (explosive == null)
        {
            explosive = Undo.AddComponent<ExplosiveHazard>(target);
        }
        Undo.RecordObject(explosive, "Configure Explosive");
        explosive.SetExplosionParams(_explosionDamage, _explosionRadius, _explosionForce);

        // 确保有 Rigidbody2D（静态，用于碰撞检测）
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody2D>(target);
        }
        Undo.RecordObject(rb, "Configure RB");
        rb.bodyType = RigidbodyType2D.Static;

        Debug.Log($"[AssetApplyToSelected] 已配置爆炸型陷阱: 伤害={_explosionDamage}, 半径={_explosionRadius}");
    }

    /// <summary>
    /// 接触伤害型：碰到就扣血 + 击退
    /// </summary>
    private void SetupContactHazard(GameObject target)
    {
        // 确保有 Collider2D (Trigger)
        var col = target.GetComponent<Collider2D>();
        if (col == null)
        {
            var box = Undo.AddComponent<BoxCollider2D>(target);
            box.isTrigger = true;
        }
        else if (!col.isTrigger)
        {
            Undo.RecordObject(col, "Set Trigger");
            col.isTrigger = true;
        }

        // 确保有 DamageDealer
        var dealer = target.GetComponent<DamageDealer>();
        if (dealer == null)
        {
            Undo.AddComponent<DamageDealer>(target);
        }
    }

    /// <summary>
    /// 旋转锯片型
    /// </summary>
    private void SetupSawBlade(GameObject target)
    {
        var saw = target.GetComponent<SawBlade>();
        if (saw == null)
        {
            Undo.AddComponent<SawBlade>(target);
        }

        // SawBlade 继承 BaseHazard，自带碰撞检测
        var col = target.GetComponent<Collider2D>();
        if (col == null)
        {
            var circle = Undo.AddComponent<CircleCollider2D>(target);
            circle.isTrigger = true;
        }
    }

    /// <summary>
    /// 可收集道具
    /// </summary>
    private void SetupCollectibleProp(GameObject target)
    {
        var col = target.GetComponent<Collider2D>();
        if (col == null)
        {
            var box = Undo.AddComponent<BoxCollider2D>(target);
            box.isTrigger = true;
            box.size = new Vector2(0.6f, 0.6f);
        }
    }

    // =========================================================================
    // 碰撞体自动适配
    // =========================================================================
    // [AI防坑警告] 角色（MarioController/TricksterController）的碰撞体由 PhysicsMetrics
    // 定义，是物理真相，换皮时绝对不能修改！这是项目的核心理念：
    //   "碰撞体尺寸 = 物理真相，视觉 Sprite = 纯粹装饰"
    // 只有非角色物体（道具、陷阱、环境）才根据 Sprite 尺寸自适应碰撞体。
    private void AutoFitCollider(GameObject target, SpriteRenderer sr)
    {
        if (sr.sprite == null) return;

        // ── 角色保护：检测是否是玩家角色 ──
        bool isCharacter = PivotPresetUtility.HasCharacterControllerPublic(target);
        if (isCharacter)
        {
            // 角色碰撞体是运行时控制链路的一部分；Apply Art 不再在这里重算尺寸/偏移。
            // 只修复会直接导致不能移动的控制字段，其余物理真相交给角色控制器和测试场景维护。
            EnsureCharacterControlChain(target);
            return; // 角色不做 Sprite-based 自适应
        }

        // ── 非角色物体：根据 Sprite 尺寸自适应碰撞体 ──
        var boxCol = target.GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Undo.RecordObject(boxCol, "Auto-fit Collider");
            Vector2 spriteSize = sr.sprite.bounds.size;
            // 碰撞体略小于贴图（90%），避免边缘误触发
            boxCol.size = spriteSize * 0.9f;

            // 根据 Pivot 计算碰撞体偏移，确保碰撞体居中在 Sprite 视觉中心
            // 当 Pivot = Center 时 offset = (0,0)；当 Pivot = BottomCenter 时 offset.y = spriteHeight/2
            var resolvedPreset = PivotPresetUtility.ResolvePreset(_pivotPreset, GetPhysicsTypeHint(target), target);
            Vector2 pivotVec = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
            // Sprite bounds.center 相对于 pivot 的偏移：
            // pivot (0.5, 0.5) → offset (0, 0)
            // pivot (0.5, 0.0) → offset (0, +spriteHeight/2)
            // pivot (0.0, 0.5) → offset (+spriteWidth/2, 0)
            float offsetX = (0.5f - pivotVec.x) * spriteSize.x;
            float offsetY = (0.5f - pivotVec.y) * spriteSize.y;
            boxCol.offset = new Vector2(offsetX, offsetY);

            Debug.Log($"[AssetApplyToSelected] 非角色碰撞体自适应: size={boxCol.size}, " +
                      $"offset={boxCol.offset}, pivot=({pivotVec.x:F2},{pivotVec.y:F2})");
        }

        var circle = target.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Undo.RecordObject(circle, "Auto-fit Collider");
            Vector2 spriteSize = sr.sprite.bounds.size;
            circle.radius = Mathf.Min(spriteSize.x, spriteSize.y) * 0.45f;

            // 同样根据 Pivot 计算偏移
            var resolvedPreset = PivotPresetUtility.ResolvePreset(_pivotPreset, GetPhysicsTypeHint(target), target);
            Vector2 pivotVec = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
            float offsetX = (0.5f - pivotVec.x) * spriteSize.x;
            float offsetY = (0.5f - pivotVec.y) * spriteSize.y;
            circle.offset = new Vector2(offsetX, offsetY);
        }

        // [红线防护] 执行后验证：确保没有意外修改角色碰撞体
        RedLineGuard.ValidateCharacterColliders(autoRepair: true);
    }

    // =========================================================================
    // 工具方法
    // =========================================================================
    private void NormalizeTexture(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
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

        if (dirty)
        {
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }
    }

    private void EnsureSEFMaterial(SpriteRenderer sr)
    {
        if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null
            && sr.sharedMaterial.shader.name == "MarioTrickster/SEF/UberSprite")
            return;

        var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.name = $"SEF_{sr.gameObject.name}";
        if (sr.sprite != null)
            mat.mainTexture = sr.sprite.texture;

        EnsureDirectory("Assets/Art/Materials/SEF");
        string matPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/Art/Materials/SEF/{mat.name}.mat");
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();

        sr.sharedMaterial = mat;
    }

    private string GetTextureSingleFrameHint(Texture2D texture, Sprite sprite)
    {
        if (TryBuildSlicePlan(texture, GetPhysicsTypeHint(Selection.activeGameObject != null ? ResolveApplyTarget(Selection.activeGameObject) : null), out SlicePlan plan))
        {
            return $"检测到未切片 Texture2D Sprite Sheet：预计 {plan.FrameCount} 帧（{plan.cols}x{plan.rows}，单帧 {plan.frameW}x{plan.frameH}，来源={plan.source}）。点击应用时会自动按网格切片，不会按单帧 Sprite 处理。";
        }
        return $"已识别单帧 Sprite: {sprite.name}";
    }

    private void AutoSliceTextureSheetIfNeeded(Texture2D texture, int physicsTypeHint, GameObject target = null)
    {
        if (texture == null) return;

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path)) return;

        Sprite[] existingSprites = LoadSpritesAtPath(path);
        if (existingSprites.Length > 1 && _sliceMode == SpriteSheetSliceMode.AutoDetect) return;

        if (!TryBuildSlicePlan(texture, physicsTypeHint, out SlicePlan plan)) return;

        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = 32;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        List<SpriteMetaData> metaList = new List<SpriteMetaData>();
        var resolvedPreset = GetResolvedPivotPreset(physicsTypeHint, target);
        Vector2 pivot = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
        int alignment = PivotPresetUtility.PivotToAlignment(resolvedPreset);

        for (int r = 0; r < plan.rows; r++)
        {
            for (int c = 0; c < plan.cols; c++)
            {
                SpriteMetaData smd = new SpriteMetaData();
                smd.name = plan.rows == 1 ? $"{texture.name}_F{c}" : $"{texture.name}_R{r}_F{c}";
                smd.rect = new Rect(plan.x + c * plan.frameW, texture.height - plan.yFromTop - (r + 1) * plan.frameH, plan.frameW, plan.frameH);
                Vector2 framePivot = ComputeVisibleAwarePivot(texture, smd.rect, resolvedPreset, pivot);
                smd.pivot = framePivot;
                smd.alignment = ArePivotsEqual(framePivot, pivot) ? alignment : (int)SpriteAlignment.Custom;
                metaList.Add(smd);
            }
        }

        SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, metaList);
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        AssetDatabase.Refresh();
        Debug.Log($"[AssetApplyToSelected] 切片 Texture2D Sprite Sheet: {path} → {plan.FrameCount} 帧 ({plan.cols}x{plan.rows}, {plan.frameW}x{plan.frameH}, source={plan.source})");
    }

    private bool TryBuildSlicePlan(Texture2D texture, int physicsTypeHint, out SlicePlan plan)
    {
        plan = new SlicePlan
        {
            cols = 1,
            rows = 1,
            frameW = texture != null ? texture.width : 0,
            frameH = texture != null ? texture.height : 0,
            x = 0,
            yFromTop = 0,
            width = texture != null ? texture.width : 0,
            height = texture != null ? texture.height : 0,
            source = "single"
        };
        if (texture == null || texture.width <= 0 || texture.height <= 0) return false;

        if (_sliceMode == SpriteSheetSliceMode.ManualGrid)
        {
            int cols = Mathf.Max(1, _manualColumns);
            int rows = Mathf.Max(1, _manualRows);
            int x = Mathf.Clamp(_sliceOffsetX, 0, Mathf.Max(0, texture.width - 1));
            int y = Mathf.Clamp(_sliceOffsetY, 0, Mathf.Max(0, texture.height - 1));
            int areaW = texture.width - x;
            int areaH = texture.height - y;
            if (areaW % cols != 0 || areaH % rows != 0) return false;
            plan = MakeSlicePlan(cols, rows, areaW / cols, areaH / rows, x, y, areaW, areaH, "manual-grid");
            return plan.IsValid;
        }

        if (_sliceMode == SpriteSheetSliceMode.ManualCellSize)
        {
            int cellW = Mathf.Max(1, _manualCellWidth);
            int cellH = Mathf.Max(1, _manualCellHeight);
            int x = Mathf.Clamp(_sliceOffsetX, 0, Mathf.Max(0, texture.width - 1));
            int y = Mathf.Clamp(_sliceOffsetY, 0, Mathf.Max(0, texture.height - 1));
            int areaW = texture.width - x;
            int areaH = texture.height - y;
            if (areaW % cellW != 0 || areaH % cellH != 0) return false;
            plan = MakeSlicePlan(areaW / cellW, areaH / cellH, cellW, cellH, x, y, areaW, areaH, "manual-cell-size");
            return plan.IsValid;
        }

        if (_sliceMode == SpriteSheetSliceMode.AIBackend && _aiAnalysis != null)
        {
            if (TryBuildPlanFromAi(texture, _aiAnalysis, out plan)) return true;
        }

        return TryDetectSpriteSheetGrid(texture, out plan);
    }

    private SlicePlan MakeSlicePlan(int cols, int rows, int frameW, int frameH, int x, int yFromTop, int width, int height, string source)
    {
        return new SlicePlan
        {
            cols = cols,
            rows = rows,
            frameW = frameW,
            frameH = frameH,
            x = x,
            yFromTop = yFromTop,
            width = width,
            height = height,
            source = source
        };
    }

    private bool TryBuildPlanFromAi(Texture2D texture, AISliceAnalysis ai, out SlicePlan plan)
    {
        plan = new SlicePlan();
        if (texture == null || ai == null) return false;

        int cols = Mathf.Max(1, ai.grid_cols);
        int rows = Mathf.Max(1, ai.grid_rows);
        int x = Mathf.Clamp(ai.x, 0, Mathf.Max(0, texture.width - 1));
        int y = Mathf.Clamp(ai.y, 0, Mathf.Max(0, texture.height - 1));
        int areaW = ai.w > 0 ? Mathf.Min(ai.w, texture.width - x) : texture.width - x;
        int areaH = ai.h > 0 ? Mathf.Min(ai.h, texture.height - y) : texture.height - y;
        int frameW = ai.cell_width > 0 ? ai.cell_width : (cols > 0 ? areaW / cols : 0);
        int frameH = ai.cell_height > 0 ? ai.cell_height : (rows > 0 ? areaH / rows : 0);

        if (frameW <= 0 || frameH <= 0) return false;
        if (areaW < frameW * cols) areaW = frameW * cols;
        if (areaH < frameH * rows) areaH = frameH * rows;
        if (x + areaW > texture.width || y + areaH > texture.height) return false;

        plan = MakeSlicePlan(cols, rows, frameW, frameH, x, y, frameW * cols, frameH * rows, "ai-backend");
        return plan.IsValid;
    }

    private bool TryDetectSpriteSheetGrid(Texture2D texture, out SlicePlan plan)
    {
        plan = new SlicePlan();
        if (texture == null || texture.width <= 0 || texture.height <= 0) return false;

        Match sizeMatch = Regex.Match(texture.name, @"(?<w>\d{1,4})\s*x\s*(?<h>\d{1,4})", RegexOptions.IgnoreCase);
        if (sizeMatch.Success &&
            int.TryParse(sizeMatch.Groups["w"].Value, out int namedFrameW) &&
            int.TryParse(sizeMatch.Groups["h"].Value, out int namedFrameH) &&
            namedFrameW > 0 && namedFrameH > 0 &&
            texture.width % namedFrameW == 0 && texture.height % namedFrameH == 0)
        {
            plan = MakeSlicePlan(texture.width / namedFrameW, texture.height / namedFrameH, namedFrameW, namedFrameH, 0, 0, texture.width, texture.height, "filename-size");
            return plan.IsValid;
        }

        if (texture.height == 32 && texture.width % 32 == 0 && texture.width / 32 > 1)
        {
            plan = MakeSlicePlan(texture.width / 32, 1, 32, 32, 0, 0, texture.width, texture.height, "32px-horizontal");
            return true;
        }

        if (texture.width > texture.height && texture.width % texture.height == 0 && texture.width / texture.height > 1)
        {
            plan = MakeSlicePlan(texture.width / texture.height, 1, texture.height, texture.height, 0, 0, texture.width, texture.height, "square-strip");
            return true;
        }

        if (texture.width % 32 == 0 && texture.height % 32 == 0)
        {
            plan = MakeSlicePlan(texture.width / 32, texture.height / 32, 32, 32, 0, 0, texture.width, texture.height, "32px-grid");
            return plan.IsValid;
        }

        return false;
    }

    private async void RunAiSliceAnalysis()
    {
        if (_artTexture == null || string.IsNullOrEmpty(_apiKey)) return;

        _isAiAnalyzing = true;
        _aiStatus = "正在调用 AI 后台识别素材切片方式...";
        _aiAnalysis = null;
        Repaint();

        try
        {
            string base64 = EncodeTextureToBase64(_artTexture);
            if (string.IsNullOrEmpty(base64))
            {
                _aiStatus = "AI 识别失败：无法读取贴图。";
                return;
            }

            string json = await CallSliceVisionAPI(base64, _artTexture.width, _artTexture.height);
            if (string.IsNullOrEmpty(json))
            {
                _aiStatus = "AI 识别失败：没有返回有效结果。";
                return;
            }

            _aiAnalysis = ParseAiSliceAnalysis(json);
            if (_aiAnalysis == null || _aiAnalysis.grid_cols <= 0 || _aiAnalysis.grid_rows <= 0)
            {
                _aiStatus = "AI 识别失败：返回结果无法解析。";
                return;
            }

            if (_aiAnalysis.cell_width <= 0 && _aiAnalysis.grid_cols > 0)
                _aiAnalysis.cell_width = (_aiAnalysis.w > 0 ? _aiAnalysis.w : _artTexture.width) / _aiAnalysis.grid_cols;
            if (_aiAnalysis.cell_height <= 0 && _aiAnalysis.grid_rows > 0)
                _aiAnalysis.cell_height = (_aiAnalysis.h > 0 ? _aiAnalysis.h : _artTexture.height) / _aiAnalysis.grid_rows;
            if (_aiAnalysis.w <= 0) _aiAnalysis.w = _aiAnalysis.cell_width * _aiAnalysis.grid_cols;
            if (_aiAnalysis.h <= 0) _aiAnalysis.h = _aiAnalysis.cell_height * _aiAnalysis.grid_rows;

            _manualColumns = Mathf.Max(1, _aiAnalysis.grid_cols);
            _manualRows = Mathf.Max(1, _aiAnalysis.grid_rows);
            _manualCellWidth = Mathf.Max(1, _aiAnalysis.cell_width);
            _manualCellHeight = Mathf.Max(1, _aiAnalysis.cell_height);
            _sliceOffsetX = Mathf.Max(0, _aiAnalysis.x);
            _sliceOffsetY = Mathf.Max(0, _aiAnalysis.y);

            _aiStatus = $"AI 已识别：{_aiAnalysis.grid_cols} 列 × {_aiAnalysis.grid_rows} 行，单帧 {_aiAnalysis.cell_width}×{_aiAnalysis.cell_height}，置信度={_aiAnalysis.confidence}";
        }
        catch (Exception ex)
        {
            _aiStatus = $"AI 识别失败: {ex.Message}";
            Debug.LogError($"[AssetApplyToSelected] AI slice analysis failed: {ex}");
        }
        finally
        {
            _isAiAnalyzing = false;
            Repaint();
        }
    }

    private string EncodeTextureToBase64(Texture2D tex)
    {
        int maxSize = 2048;
        int targetW = tex.width;
        int targetH = tex.height;
        if (Mathf.Max(targetW, targetH) > maxSize)
        {
            float scale = (float)maxSize / Mathf.Max(targetW, targetH);
            targetW = Mathf.RoundToInt(targetW * scale);
            targetH = Mathf.RoundToInt(targetH * scale);
        }

        RenderTexture rt = RenderTexture.GetTemporary(targetW, targetH, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.Blit(tex, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D readable = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        byte[] pngBytes = readable.EncodeToPNG();
        DestroyImmediate(readable);
        return pngBytes != null && pngBytes.Length > 0 ? Convert.ToBase64String(pngBytes) : "";
    }

    private async Task<string> CallSliceVisionAPI(string base64Image, int origW, int origH)
    {
        string systemPrompt = @"You are a 2D pixel-art sprite-sheet import assistant. Decide how Unity should slice the provided image for immediate animation import.
Return ONLY valid JSON. Choose the single most useful animation group if the image contains multiple objects. If the full image is one sheet, use x=0,y=0,w=image_width,h=image_height. Coordinates use TOP-LEFT origin in the ORIGINAL image size. Prefer exact grid_cols/grid_rows and cell_width/cell_height. If it is one static image, return grid_cols=1,grid_rows=1,frame_count=1.
Schema: {""asset_kind"":""animation|static|mixed_collection"",""frame_count"":8,""grid_cols"":8,""grid_rows"":1,""cell_width"":32,""cell_height"":32,""x"":0,""y"":0,""w"":256,""h"":32,""confidence"":""high|medium|low"",""reason"":""short reason""}";
        string userMsg = $"Analyze this {origW}x{origH} sprite sheet. Return only JSON using the schema, with coordinates in original pixels.";
        string requestBody = $@"{{
  ""model"": ""{_model}"",
  ""messages"": [
    {{""role"": ""system"", ""content"": {EscapeJsonString(systemPrompt)}}},
    {{""role"": ""user"", ""content"": [
      {{""type"": ""text"", ""text"": {EscapeJsonString(userMsg)}}},
      {{""type"": ""image_url"", ""image_url"": {{""url"": ""data:image/png;base64,{base64Image}"", ""detail"": ""high""}}}}
    ]}}
  ],
  ""temperature"": 0.1,
  ""max_tokens"": 1000
}}";

        using (var client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{_baseUrl}/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Debug.LogError($"[AssetApplyToSelected] AI API Error {response.StatusCode}: {error}");
                return null;
            }
            string responseJson = await response.Content.ReadAsStringAsync();
            string result = ExtractContentFromResponse(responseJson);
            if (result != null && result.Contains("```"))
            {
                result = string.Join("\n", result.Split('\n').Where(l => !l.TrimStart().StartsWith("```")));
            }
            return result;
        }
    }

    private AISliceAnalysis ParseAiSliceAnalysis(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        AISliceAnalysis parsed = null;
        try { parsed = JsonUtility.FromJson<AISliceAnalysis>(json); }
        catch { parsed = null; }
        if (parsed == null) parsed = new AISliceAnalysis();
        parsed.asset_kind = ExtractString(json, "asset_kind") ?? parsed.asset_kind;
        parsed.frame_count = ExtractInt(json, "frame_count", parsed.frame_count);
        parsed.grid_cols = ExtractInt(json, "grid_cols", parsed.grid_cols);
        parsed.grid_rows = ExtractInt(json, "grid_rows", parsed.grid_rows);
        parsed.cell_width = ExtractInt(json, "cell_width", parsed.cell_width);
        parsed.cell_height = ExtractInt(json, "cell_height", parsed.cell_height);
        parsed.x = ExtractInt(json, "x", parsed.x);
        parsed.y = ExtractInt(json, "y", parsed.y);
        parsed.w = ExtractInt(json, "w", parsed.w);
        parsed.h = ExtractInt(json, "h", parsed.h);
        parsed.confidence = ExtractString(json, "confidence") ?? parsed.confidence ?? "unknown";
        parsed.reason = ExtractString(json, "reason") ?? parsed.reason ?? "";
        return parsed;
    }

    private string ExtractContentFromResponse(string responseJson)
    {
        int msgIdx = responseJson.IndexOf("\"message\"");
        if (msgIdx < 0) return null;
        int contentIdx = responseJson.IndexOf("\"content\"", msgIdx);
        if (contentIdx < 0) return null;
        int colonIdx = responseJson.IndexOf(':', contentIdx);
        if (colonIdx < 0) return null;
        int start = colonIdx + 1;
        while (start < responseJson.Length && responseJson[start] == ' ') start++;
        if (start >= responseJson.Length || responseJson[start] != '"') return null;
        StringBuilder sb = new StringBuilder();
        int i = start + 1;
        while (i < responseJson.Length)
        {
            if (responseJson[i] == '\\' && i + 1 < responseJson.Length)
            {
                char next = responseJson[i + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); i += 2; break;
                    case '\\': sb.Append('\\'); i += 2; break;
                    case 'n': sb.Append('\n'); i += 2; break;
                    case 'r': sb.Append('\r'); i += 2; break;
                    case 't': sb.Append('\t'); i += 2; break;
                    default: sb.Append(next); i += 2; break;
                }
            }
            else if (responseJson[i] == '"') break;
            else { sb.Append(responseJson[i]); i++; }
        }
        return sb.ToString();
    }

    private static int ExtractInt(string json, string key, int fallback = 0)
    {
        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return fallback;
        int colonIdx = json.IndexOf(':', idx + pattern.Length);
        if (colonIdx < 0) return fallback;
        int start = colonIdx + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
        int end = start;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
        if (end > start && int.TryParse(json.Substring(start, end - start), out int val)) return val;
        return fallback;
    }

    private static string ExtractString(string json, string key)
    {
        string pattern = $"\"{key}\"";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return null;
        int colonIdx = json.IndexOf(':', idx + pattern.Length);
        if (colonIdx < 0) return null;
        int quoteStart = json.IndexOf('"', colonIdx + 1);
        if (quoteStart < 0) return null;
        int quoteEnd = quoteStart + 1;
        while (quoteEnd < json.Length)
        {
            if (json[quoteEnd] == '\\') { quoteEnd += 2; continue; }
            if (json[quoteEnd] == '"') break;
            quoteEnd++;
        }
        return quoteEnd < json.Length ? json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1) : null;
    }

    private static string EscapeJsonString(string s)
    {
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
    }

    // [AI防坑警告] Pivot 解析必须经过 PivotPresetUtility.ResolvePreset，不能硬编码二分法。
    // 用户手动选择优先；Auto 模式会检查 MarioController 等角色组件，避免无 marker 时默认 Center 导致角色悬空。
    private PivotPresetUtility.PivotPreset GetResolvedPivotPreset(int physicsTypeHint, GameObject target = null)
    {
        GameObject context = target != null
            ? ResolveApplyTarget(target)
            : (Selection.activeGameObject != null ? ResolveApplyTarget(Selection.activeGameObject) : null);
        return PivotPresetUtility.ResolvePreset(_pivotPreset, physicsTypeHint, context);
    }

    private Vector2 GetPivotForPhysicsHint(int physicsTypeHint)
    {
        return GetPivotForPhysicsHint(physicsTypeHint, null);
    }

    private Vector2 GetPivotForPhysicsHint(int physicsTypeHint, GameObject target)
    {
        var resolved = GetResolvedPivotPreset(physicsTypeHint, target);
        return PivotPresetUtility.PivotToVector2(resolved, _customPivot);
    }

    private int GetAlignmentForPhysicsHint(int physicsTypeHint)
    {
        return GetAlignmentForPhysicsHint(physicsTypeHint, null);
    }

    private int GetAlignmentForPhysicsHint(int physicsTypeHint, GameObject target)
    {
        var resolved = GetResolvedPivotPreset(physicsTypeHint, target);
        return PivotPresetUtility.PivotToAlignment(resolved);
    }

    /// <summary>
    /// 强制将用户选择的 Pivot 写入贴图的所有 Sprite 帧。
    /// 解决关键 Bug：已切片贴图在 AutoDetect 模式下被跳过切片，导致 Pivot 永远不更新。
    /// 此方法独立于切片流程，无论贴图是否已切片、是单帧还是多帧，都会更新 Pivot。
    /// 支持三种输入：Texture2D、直接拖入的 Sprite、或文件夹。
    /// </summary>
    private void ApplyPivotToTextureSprites(Texture2D texture, Sprite directSprite, int physicsTypeHint, GameObject target)
    {
        // 解析用户选择的 Pivot
        var resolvedPreset = PivotPresetUtility.ResolvePreset(_pivotPreset, physicsTypeHint, target);
        Vector2 newPivot = PivotPresetUtility.PivotToVector2(resolvedPreset, _customPivot);
        int newAlignment = PivotPresetUtility.PivotToAlignment(resolvedPreset);

        Debug.Log($"[AssetApplyToSelected] Pivot 解析: preset={_pivotPreset}, resolved={resolvedPreset}, " +
                  $"pivot=({newPivot.x:F2},{newPivot.y:F2}), alignment={newAlignment}, " +
                  $"physicsTypeHint={physicsTypeHint}, target={( target != null ? target.name : "null")}");

        // 收集所有需要处理的贴图路径
        var texturePaths = new System.Collections.Generic.HashSet<string>();

        if (texture != null)
        {
            string p = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(p)) texturePaths.Add(p);
        }
        if (directSprite != null)
        {
            string p = AssetDatabase.GetAssetPath(directSprite);
            if (!string.IsNullOrEmpty(p)) texturePaths.Add(p);
        }
        if (_artFolder != null)
        {
            string folderPath = AssetDatabase.GetAssetPath(_artFolder);
            if (!string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (string guid in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(p)) texturePaths.Add(p);
                }
            }
        }

        if (texturePaths.Count == 0)
        {
            Debug.LogWarning("[AssetApplyToSelected] Pivot 更新跳过：没有找到要处理的贴图路径");
            return;
        }

        int updatedCount = 0;
        foreach (string path in texturePaths)
        {
            if (ApplyPivotToSingleTexture(path, newPivot, newAlignment, resolvedPreset, target))
                updatedCount++;
        }

        if (updatedCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[AssetApplyToSelected] Pivot 已更新 {updatedCount} 张贴图 → " +
                      $"{PivotPresetUtility.GetPresetDisplayName(resolvedPreset)} ({newPivot.x:F1}, {newPivot.y:F1})");
        }
    }

    /// <summary>
    /// 对单张贴图应用 Pivot 设置。返回是否有修改。
    /// </summary>
    private bool ApplyPivotToSingleTexture(string path, Vector2 newPivot, int newAlignment, PivotPresetUtility.PivotPreset resolvedPreset, GameObject target)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return false;

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool dirty = false;

        if (ti.spriteImportMode == SpriteImportMode.Multiple)
        {
            // 多帧模式：更新每一帧的 pivot 和 alignment。角色底部 Pivot 会自动贴到可见像素脚底，消除透明边距导致的悬空。
            SpriteMetaData[] metas = SpriteSheetDataProviderBridge.GetSpriteMetaData(ti);
            if (metas != null && metas.Length > 0)
            {
                for (int i = 0; i < metas.Length; i++)
                {
                    Vector2 framePivot = ComputeVisibleAwarePivot(texture, metas[i].rect, resolvedPreset, newPivot);
                    int frameAlignment = ArePivotsEqual(framePivot, newPivot) ? newAlignment : (int)SpriteAlignment.Custom;
                    if (metas[i].alignment != frameAlignment || !ArePivotsEqual(metas[i].pivot, framePivot))
                    {
                        metas[i].alignment = frameAlignment;
                        metas[i].pivot = framePivot;
                        dirty = true;
                    }
                }
                if (dirty)
                {
                    SpriteSheetDataProviderBridge.SetSpriteMetaData(ti, metas);
                    Debug.Log($"[AssetApplyToSelected] 多帧 Pivot 已写入: {path}, {metas.Length} 帧, " +
                              $"baseAlignment={newAlignment}, basePivot=({newPivot.x:F2},{newPivot.y:F2})");
                }
            }
            else
            {
                Debug.LogWarning($"[AssetApplyToSelected] 警告: {path} 是 Multiple 模式但没有 spritesheet 数据");
            }
        }
        else
        {
            // 单帧模式：通过 TextureImporterSettings 修改 pivot
            TextureImporterSettings settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            Rect fullRect = texture != null ? new Rect(0, 0, texture.width, texture.height) : new Rect(0, 0, 0, 0);
            Vector2 framePivot = ComputeVisibleAwarePivot(texture, fullRect, resolvedPreset, newPivot);
            int frameAlignment = ArePivotsEqual(framePivot, newPivot) ? newAlignment : (int)SpriteAlignment.Custom;
            if (settings.spriteAlignment != frameAlignment || !ArePivotsEqual(settings.spritePivot, framePivot))
            {
                settings.spriteAlignment = frameAlignment;
                settings.spritePivot = framePivot;
                ti.SetTextureSettings(settings);
                dirty = true;
                Debug.Log($"[AssetApplyToSelected] 单帧 Pivot 已写入: {path}, " +
                          $"alignment={frameAlignment}, pivot=({framePivot.x:F2},{framePivot.y:F2})");
            }
        }

        if (dirty)
        {
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }

        return dirty;
    }

    private void PrepareFolderTexturesForApply(DefaultAsset folder, int physicsTypeHint, GameObject target)
    {
        foreach (string path in GetTexturePathsFromFolder(folder))
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            if (_normalizeSettings)
            {
                NormalizeTexture(tex);
            }
            AutoSliceTextureSheetIfNeeded(tex, physicsTypeHint, target);
        }
        AssetDatabase.Refresh();
    }

    private List<string> GetTexturePathsFromFolder(DefaultAsset folder)
    {
        List<string> paths = new List<string>();
        if (folder == null) return paths;
        string folderPath = AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return paths;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && !paths.Contains(path)) paths.Add(path);
        }

        return paths.OrderBy(path => path, System.StringComparer.OrdinalIgnoreCase).ToList();
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

    private Sprite[] ResolveSpritesForApply()
    {
        if (_artFolder != null)
        {
            Sprite[] sprites = LoadSpritesFromFolder(_artFolder);
            if (sprites.Length > 0) return sprites;
        }

        if (_artTexture != null)
        {
            Sprite[] sprites = LoadSpritesFromTexture(_artTexture);
            if (sprites.Length > 0) return sprites;
        }

        if (_artSprite != null)
        {
            Sprite[] sprites = LoadSpritesFromSprite(_artSprite);
            if (sprites.Length > 0) return sprites;
            return new Sprite[] { _artSprite };
        }

        return new Sprite[0];
    }


    private Sprite[] LoadSpritesFromFolder(DefaultAsset folder)
    {
        if (folder == null) return new Sprite[0];
        string folderPath = AssetDatabase.GetAssetPath(folder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath)) return new Sprite[0];

        List<Sprite> sprites = new List<Sprite>();
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) continue;
            sprites.AddRange(LoadSpritesAtPath(assetPath));
        }

        return sprites
            .Where(sprite => sprite != null)
            .Distinct()
            .OrderBy(sprite => sprite.name, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private Sprite[] LoadSpritesFromTexture(Texture2D texture)
    {
        if (texture == null) return new Sprite[0];
        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path)) return new Sprite[0];
        return LoadSpritesAtPath(path);
    }

    private Sprite[] LoadSpritesFromSprite(Sprite sprite)
    {
        if (sprite == null) return new Sprite[0];
        string path = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(path)) return new Sprite[] { sprite };
        Sprite[] sprites = LoadSpritesAtPath(path);
        return sprites.Length > 0 ? sprites : new Sprite[] { sprite };
    }

    private Sprite[] LoadSpritesAtPath(string path)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderByDescending(s => s.rect.y)
            .ThenBy(s => s.rect.x)
            .ThenBy(s => s.name)
            .ToArray();
        if (sprites.Length > 0) return sprites;

        Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return single != null ? new Sprite[] { single } : new Sprite[0];
    }

    private void ConfigureSpriteAnimation(SpriteRenderer sr, Sprite[] sprites, ArtAssetClassifier.Classification classification)
    {
        if (sr == null) return;

        if (classification != null && classification.IsStateDriven)
        {
            ConfigureSpriteStateAnimator(sr, classification);
            return;
        }

        ConfigureSpriteFrameAnimator(sr, sprites);
    }

    private void ConfigureSpriteStateAnimator(SpriteRenderer sr, ArtAssetClassifier.Classification classification)
    {
        if (sr == null || classification == null) return;

        var loopAnimator = sr.gameObject.GetComponent<SpriteFrameAnimator>();
        if (loopAnimator != null)
        {
            Undo.DestroyObjectImmediate(loopAnimator);
        }

        var stateAnimator = sr.gameObject.GetComponent<SpriteStateAnimator>();
        if (stateAnimator == null)
        {
            stateAnimator = Undo.AddComponent<SpriteStateAnimator>(sr.gameObject);
        }

        Undo.RecordObject(stateAnimator, "Configure Sprite State Animator");
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
        EditorUtility.SetDirty(stateAnimator);
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

    private void ConfigureSpriteFrameAnimator(SpriteRenderer sr, Sprite[] sprites)
    {
        if (sr == null) return;

        var stateAnimator = sr.gameObject.GetComponent<SpriteStateAnimator>();
        if (stateAnimator != null)
        {
            Undo.DestroyObjectImmediate(stateAnimator);
        }

        var animator = sr.gameObject.GetComponent<SpriteFrameAnimator>();

        if (sprites == null || sprites.Length <= 1)
        {
            if (animator != null)
            {
                Undo.DestroyObjectImmediate(animator);
            }
            return;
        }

        if (animator == null)
        {
            animator = Undo.AddComponent<SpriteFrameAnimator>(sr.gameObject);
        }
        Undo.RecordObject(animator, "Configure Sprite Frame Animator");
        animator.frames = sprites;
        animator.frameRate = 10f;
        animator.playOnStart = true;
        animator.loop = true;
        EditorUtility.SetDirty(animator);
    }

    private int GetPhysicsTypeHint(GameObject target)
    {
        var marker = target != null ? target.GetComponent<ImportedAssetMarker>() : null;
        return marker != null ? marker.physicsType : -1;
    }

    private void UpdateMarker(GameObject target, Sprite[] sprites, ArtAssetClassifier.Classification classification)
    {
        if (sprites == null) sprites = new Sprite[0];
        var marker = target.GetComponent<ImportedAssetMarker>();
        if (marker == null)
        {
            marker = Undo.AddComponent<ImportedAssetMarker>(target);
        }
        Undo.RecordObject(marker, "Update Marker");
        marker.sourceSprites = sprites;
        marker.frameCount = sprites.Length;
        marker.importTimestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        marker.sourceAssetPath = sprites.Length > 0 ? AssetDatabase.GetAssetPath(sprites[0]) : "";
        ArtAssetClassifier.ApplyToMarker(marker, classification);

        // [S122修复] 根据分类结果同步设置 physicsType，避免后续操作无法正确推断 Pivot
        // ArtAssetClassifier.ApplyToMarker 不设置 physicsType，这里补充
        if (classification != null)
        {
            switch (classification.role)
            {
                case ArtAssetClassifier.AssetRole.Character:
                    marker.physicsType = 0;
                    break;
                case ArtAssetClassifier.AssetRole.Platform:
                    marker.physicsType = 1;
                    break;
                case ArtAssetClassifier.AssetRole.Hazard:
                    marker.physicsType = 2;
                    break;
                case ArtAssetClassifier.AssetRole.VFX:
                    marker.physicsType = 3;
                    break;
                case ArtAssetClassifier.AssetRole.Prop:
                    marker.physicsType = 4;
                    break;
            }
        }
        else if (PivotPresetUtility.AutoDetectPivot(target) == PivotPresetUtility.PivotPreset.BottomCenter)
        {
            // 没有分类结果但目标是角色（有 MarioController 等），确保 physicsType=0
            marker.physicsType = 0;
        }
    }

    private void SavePrefab(GameObject go)
    {
        EnsureDirectory(_prefabFolder);

        GameObject root = go;
        if (go.transform.parent != null)
        {
            var marker = go.GetComponentInParent<ImportedAssetMarker>();
            if (marker != null)
                root = marker.gameObject;
            else
                root = go.transform.root.gameObject;
        }

        string prefabPath = $"{_prefabFolder}/{root.name}.prefab";

        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            Debug.Log($"[AssetApplyToSelected] Prefab 已更新: {PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)}");
        }
        else
        {
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
            Debug.Log($"[AssetApplyToSelected] Prefab 已保存: {prefabPath}");
        }
    }

    private string GetBehaviorHint(BehaviorTemplate template)
    {
        switch (template)
        {
            case BehaviorTemplate.KeepExisting:
                return "保留物体已有的所有行为组件，只替换贴图和 SEF";
            case BehaviorTemplate.Hazard_Explosive:
                return "碰撞爆炸: 玩家碰到后触发范围爆炸 + 伤害 + 击退 + 自毁特效";
            case BehaviorTemplate.Hazard_Contact:
                return "接触伤害: 碰到就扣血 + 击退（如地刺、火焰）";
            case BehaviorTemplate.Hazard_SawBlade:
                return "旋转锯片: 圆周运动 + 接触伤害 + 击退";
            case BehaviorTemplate.Prop_Collectible:
                return "可收集道具: Trigger 碰撞体，碰到后触发收集逻辑";
            case BehaviorTemplate.AutoDetect:
                return "自动检测: 根据物体已有组件推断行为模板";
            default:
                return "";
        }
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
