#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

    // =========================================================================
    // 状态
    // =========================================================================
    private DefaultAsset _artFolder;
    private Texture2D _artTexture;
    private Sprite _artSprite;
    private Sprite[] _artSprites = new Sprite[0];
    private BehaviorTemplate _behaviorTemplate = BehaviorTemplate.AutoDetect;
    private bool _attachSEF = true;
    private bool _autoSavePrefab = true;
    private bool _normalizeSettings = true;
    private string _prefabFolder = "Assets/Art/Prefabs";
    private Vector2 _scrollPos;
    private string _lastResult = "";

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

        _artFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "角色状态帧文件夹", _artFolder, typeof(DefaultAsset), false);
        _artTexture = (Texture2D)EditorGUILayout.ObjectField(
            "贴图 (Texture2D)", _artTexture, typeof(Texture2D), false);
        _artSprite = (Sprite)EditorGUILayout.ObjectField(
            "或直接拖 Sprite", _artSprite, typeof(Sprite), false);

        if (_artFolder != null)
        {
            _artSprites = LoadSpritesFromFolder(_artFolder);
            if (_artSprites.Length > 0)
            {
                _artSprite = _artSprites[0];
                var preview = ArtAssetClassifier.Classify(selected != null ? ResolveApplyTarget(selected) : null, _artSprites, -1);
                EditorGUILayout.HelpBox($"已识别文件夹散帧：{_artSprites.Length} 帧；状态={preview.StateSummary}；应用后自动挂 SpriteStateAnimator。", MessageType.None);
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
                _artSprite = _artSprites[0];
                var preview = ArtAssetClassifier.Classify(selected != null ? ResolveApplyTarget(selected) : null, _artSprites, -1);
                string frameHint = preview != null && preview.IsStateDriven
                    ? $"已识别角色状态 Sprite Sheet：{_artSprites.Length} 帧；状态={preview.StateSummary}；应用后左右移动会由 SpriteStateAnimator 驱动。"
                    : (_artSprites.Length > 1
                        ? $"已识别 Sprite Sheet：{_artSprites.Length} 帧，应用后会自动挂 SpriteFrameAnimator 播放。"
                        : $"已识别单帧 Sprite: {_artSprite.name}");
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

    // =========================================================================
    // 核心逻辑：应用素材到选中物体
    // =========================================================================
    private void ApplyArtToSelected(GameObject target)
    {
        target = ResolveApplyTarget(target);
        if (target == null) return;

        Undo.RegisterCompleteObjectUndo(target, "Apply Art to Selected");

        // Step 1: 规范化贴图
        if (_normalizeSettings && _artTexture != null)
        {
            NormalizeTexture(_artTexture);
        }

        // Step 2: 确保 Sprite / Sprite Sheet 帧可用
        Sprite[] spritesToApply = ResolveSpritesForApply();
        if (spritesToApply.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "无法获取有效的 Sprite", "好的");
            return;
        }
        _artSprite = spritesToApply[0];

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

        string animationHint = classification != null ? $"，素材分类: {classification.role}/{classification.animationMode}" : "";
        _lastResult = spritesToApply.Length > 1
            ? $"已将 [{_artSprite.name}] 等 {spritesToApply.Length} 帧应用到 [{target.name}]，已自动配置动画，行为模板: {_behaviorTemplate}{animationHint}"
            : $"已将 [{_artSprite.name}] 应用到 [{target.name}]，行为模板: {_behaviorTemplate}{animationHint}";
        Debug.Log($"[AssetApplyToSelected] {_lastResult}");
    }

    // =========================================================================
    // 行为模板应用
    // =========================================================================
    private void ApplyBehaviorTemplate(GameObject target, SpriteRenderer sr, ArtAssetClassifier.Classification classification)
    {
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
    private void AutoFitCollider(GameObject target, SpriteRenderer sr)
    {
        if (sr.sprite == null) return;

        var box = target.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Undo.RecordObject(box, "Auto-fit Collider");
            // 基于 Sprite 的实际尺寸（考虑 PPU）
            Vector2 spriteSize = sr.sprite.bounds.size;
            // 碰撞体略小于贴图（90%），避免边缘误触发
            box.size = spriteSize * 0.9f;
            box.offset = Vector2.zero;
        }

        var circle = target.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Undo.RecordObject(circle, "Auto-fit Collider");
            Vector2 spriteSize = sr.sprite.bounds.size;
            circle.radius = Mathf.Min(spriteSize.x, spriteSize.y) * 0.45f;
        }
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
