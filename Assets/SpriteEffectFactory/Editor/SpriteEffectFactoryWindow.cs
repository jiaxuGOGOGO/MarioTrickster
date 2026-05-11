using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sprite Effect Factory — 主编辑器窗口
/// 用户拖入素材 → 自动拆解颜色 → 点选效果 → 实时预览 → 一键应用
/// </summary>
public class SpriteEffectFactoryWindow : EditorWindow
{
    // =========================================================================
    // 菜单入口
    // =========================================================================
    [MenuItem("Window/MarioTrickster/Sprite Effect Factory %#e")]
    public static void ShowWindow()
    {
        var win = GetWindow<SpriteEffectFactoryWindow>("效果工厂");
        win.minSize = new Vector2(420, 600);
    }

    // =========================================================================
    // 状态
    // =========================================================================
    private Sprite _targetSprite;
    private SpriteRenderer _targetRenderer;
    private GameObject _previewGO;
    private Material _previewMat;

    // 颜色拆解结果
    private List<SpriteColorAnalyzer.ColorInfo> _extractedColors = new List<SpriteColorAnalyzer.ColorInfo>();
    private int _selectedColorIndex = -1;
    private Color _replacementColor = Color.white;

    // 效果开关
    private bool _showColorSwap = true;
    private bool _showHitFlash;
    private bool _showOutline;
    private bool _showDissolve;
    private bool _showSilhouette;
    private bool _showHSV;
    private bool _showPixelate;
    private bool _showShadow;

    // 效果参数
    private Color _flashColor = Color.white;
    private float _flashAmount;
    private Color _outlineColor = Color.white;
    private float _outlineThickness = 1f;
    private float _outlineGlow;
    private float _dissolveAmount;
    private float _dissolveEdgeWidth = 0.05f;
    private Color _dissolveEdgeColor = new Color(1f, 0.5f, 0f, 1f);
    private Texture2D _dissolveNoiseTex;
    private Color _silhouetteColor = new Color(0f, 0f, 0f, 0.5f);
    private float _hueShift;
    private float _saturation = 1f;
    private float _brightness = 1f;
    private float _pixelSize = 8f;
    private Color _shadowColor = new Color(0f, 0f, 0f, 0.5f);
    private Vector2 _shadowOffset = new Vector2(0.02f, -0.02f);
    private float _swapTolerance = 0.1f;

    // 颜色替换槽位
    private Color[] _swapFrom = new Color[4];
    private Color[] _swapTo = new Color[4];
    private bool _colorSwapDirty;

    // 第三方Shader探测
    private ShaderBackendType _detectedBackend;
    private List<DetectedProperty> _detectedProps = new List<DetectedProperty>();

    // 滚动
    private Vector2 _scrollPos;

    // =========================================================================
    // GUI
    // =========================================================================
    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawTargetSection();

        if (_targetSprite != null || _targetRenderer != null)
        {
            DrawColorAnalysisSection();
            DrawEffectsSection();
            DrawApplySection();
        }

        EditorGUILayout.EndScrollView();

        // 实时刷新预览
        if (_previewMat != null)
            ApplyToPreviewMaterial();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(8);
        GUILayout.Label("Sprite Effect Factory", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "拖入素材或选中场景中的 SpriteRenderer → 自动拆解颜色 → 调整效果 → 实时预览 → 应用",
            MessageType.Info);
        EditorGUILayout.Space(4);
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField("目标素材", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _targetSprite = (Sprite)EditorGUILayout.ObjectField("拖入 Sprite", _targetSprite, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck() && _targetSprite != null)
        {
            AnalyzeSprite();
        }

        // 也支持直接选中场景中的物体
        if (GUILayout.Button("从场景选中物体获取"))
        {
            var go = Selection.activeGameObject;
            if (go != null)
            {
                _targetRenderer = go.GetComponent<SpriteRenderer>();
                if (_targetRenderer != null)
                {
                    _targetSprite = _targetRenderer.sprite;
                    AnalyzeSprite();
                    DetectExistingShader();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "选中的物体没有 SpriteRenderer 组件", "好的");
                }
            }
        }

        if (_detectedBackend != ShaderBackendType.BuiltIn && _detectedBackend != ShaderBackendType.Custom)
        {
            EditorGUILayout.HelpBox(
                $"检测到第三方 Shader: {_detectedBackend}，已自动适配其属性。",
                MessageType.Info);
        }

        EditorGUILayout.Space(8);
    }

    private void DrawColorAnalysisSection()
    {
        EditorGUILayout.LabelField("颜色拆解", EditorStyles.boldLabel);

        if (_extractedColors.Count == 0)
        {
            EditorGUILayout.HelpBox("未检测到颜色，请确保素材贴图的 Read/Write 已开启。", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField($"共提取 {_extractedColors.Count} 种颜色（点击选中 → 右侧选替换色）");

        // 颜色网格
        int colsPerRow = Mathf.Max(1, (int)(position.width - 40) / 50);
        for (int i = 0; i < _extractedColors.Count; i++)
        {
            if (i % colsPerRow == 0) EditorGUILayout.BeginHorizontal();

            var info = _extractedColors[i];
            bool isSelected = (i == _selectedColorIndex);

            // 绘制颜色块
            var rect = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
            EditorGUI.DrawRect(rect, info.color);

            // 选中高亮
            if (isSelected)
            {
                var border = new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4);
                Handles.DrawSolidRectangleWithOutline(border, Color.clear, Color.yellow);
            }

            // 百分比标签
            var labelRect = new Rect(rect.x, rect.y + 30, rect.width, 14);
            GUI.Label(labelRect, $"{info.percentage:F0}%", EditorStyles.miniLabel);

            // 点击选中
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedColorIndex = i;
                // 自动填入颜色替换槽位
                AutoFillSwapSlot(info.color);
                Event.current.Use();
                Repaint();
            }

            if (i % colsPerRow == colsPerRow - 1 || i == _extractedColors.Count - 1)
                EditorGUILayout.EndHorizontal();
        }

        // 替换色选择器
        if (_selectedColorIndex >= 0 && _selectedColorIndex < _extractedColors.Count)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("选中颜色:", GUILayout.Width(60));
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(24, 18, GUILayout.Width(24)), _extractedColors[_selectedColorIndex].color);
            EditorGUILayout.LabelField("→ 替换为:", GUILayout.Width(60));
            _replacementColor = EditorGUILayout.ColorField(_replacementColor);
            if (GUILayout.Button("确认替换", GUILayout.Width(70)))
            {
                ApplyColorReplacement(_selectedColorIndex, _replacementColor);
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);
    }

    private void DrawEffectsSection()
    {
        EditorGUILayout.LabelField("效果调节", EditorStyles.boldLabel);

        // === 颜色替换 ===
        _showColorSwap = EditorGUILayout.Foldout(_showColorSwap, "颜色替换 (Color Swap)");
        if (_showColorSwap)
        {
            EditorGUI.indentLevel++;
            _swapTolerance = EditorGUILayout.Slider("容差", _swapTolerance, 0f, 0.5f);
            for (int i = 0; i < 4; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"槽位 {i + 1}", GUILayout.Width(50));
                _swapFrom[i] = EditorGUILayout.ColorField(_swapFrom[i]);
                EditorGUILayout.LabelField("→", GUILayout.Width(20));
                _swapTo[i] = EditorGUILayout.ColorField(_swapTo[i]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        // === 受击闪白 ===
        _showHitFlash = EditorGUILayout.Foldout(_showHitFlash, "受击闪白 (Hit Flash)");
        if (_showHitFlash)
        {
            EditorGUI.indentLevel++;
            _flashColor = EditorGUILayout.ColorField("闪白颜色", _flashColor);
            _flashAmount = EditorGUILayout.Slider("强度（拖动预览）", _flashAmount, 0f, 1f);
            EditorGUI.indentLevel--;
        }

        // === 描边 ===
        _showOutline = EditorGUILayout.Foldout(_showOutline, "描边 (Outline)");
        if (_showOutline)
        {
            EditorGUI.indentLevel++;
            _outlineColor = EditorGUILayout.ColorField("描边颜色", _outlineColor);
            _outlineThickness = EditorGUILayout.Slider("粗细", _outlineThickness, 0f, 10f);
            _outlineGlow = EditorGUILayout.Slider("发光强度", _outlineGlow, 0f, 5f);
            EditorGUI.indentLevel--;
        }

        // === 溶解 ===
        _showDissolve = EditorGUILayout.Foldout(_showDissolve, "溶解 (Dissolve)");
        if (_showDissolve)
        {
            EditorGUI.indentLevel++;
            _dissolveNoiseTex = (Texture2D)EditorGUILayout.ObjectField("噪声贴图", _dissolveNoiseTex, typeof(Texture2D), false);
            _dissolveAmount = EditorGUILayout.Slider("溶解进度", _dissolveAmount, 0f, 1f);
            _dissolveEdgeWidth = EditorGUILayout.Slider("边缘宽度", _dissolveEdgeWidth, 0f, 0.2f);
            _dissolveEdgeColor = EditorGUILayout.ColorField("边缘颜色", _dissolveEdgeColor);
            EditorGUI.indentLevel--;
        }

        // === 剪影 ===
        _showSilhouette = EditorGUILayout.Foldout(_showSilhouette, "剪影 (Silhouette)");
        if (_showSilhouette)
        {
            EditorGUI.indentLevel++;
            _silhouetteColor = EditorGUILayout.ColorField("剪影颜色", _silhouetteColor);
            EditorGUI.indentLevel--;
        }

        // === HSV ===
        _showHSV = EditorGUILayout.Foldout(_showHSV, "色相/饱和度/亮度 (HSV)");
        if (_showHSV)
        {
            EditorGUI.indentLevel++;
            _hueShift = EditorGUILayout.Slider("色相偏移", _hueShift, -1f, 1f);
            _saturation = EditorGUILayout.Slider("饱和度", _saturation, 0f, 2f);
            _brightness = EditorGUILayout.Slider("亮度", _brightness, 0f, 2f);
            if (GUILayout.Button("重置 HSV"))
            {
                _hueShift = 0f; _saturation = 1f; _brightness = 1f;
            }
            EditorGUI.indentLevel--;
        }

        // === 像素化 ===
        _showPixelate = EditorGUILayout.Foldout(_showPixelate, "像素化 (Pixelate)");
        if (_showPixelate)
        {
            EditorGUI.indentLevel++;
            _pixelSize = EditorGUILayout.Slider("像素大小", _pixelSize, 1f, 64f);
            EditorGUI.indentLevel--;
        }

        // === 投影 ===
        _showShadow = EditorGUILayout.Foldout(_showShadow, "投影 (Shadow)");
        if (_showShadow)
        {
            EditorGUI.indentLevel++;
            _shadowColor = EditorGUILayout.ColorField("阴影颜色", _shadowColor);
            _shadowOffset = EditorGUILayout.Vector2Field("偏移", _shadowOffset);
            EditorGUI.indentLevel--;
        }

        // === 第三方Shader探测到的额外属性 ===
        if (_detectedProps.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("第三方 Shader 额外属性", EditorStyles.boldLabel);
            foreach (var prop in _detectedProps)
            {
                string label = prop.guessedEffect.HasValue
                    ? $"{ShaderBackendAdapter.GetEffectDisplayName(prop.guessedEffect.Value)} - {prop.displayName}"
                    : prop.displayName;

                switch (prop.propertyType)
                {
                    case SEFPropertyType.Color:
                        prop.colorValue = EditorGUILayout.ColorField(label, prop.colorValue);
                        break;
                    case SEFPropertyType.Range:
                        prop.floatValue = EditorGUILayout.Slider(label, prop.floatValue, prop.rangeMin, prop.rangeMax);
                        break;
                    case SEFPropertyType.Float:
                        prop.floatValue = EditorGUILayout.FloatField(label, prop.floatValue);
                        break;
                    case SEFPropertyType.Toggle:
                        prop.floatValue = EditorGUILayout.Toggle(label, prop.floatValue > 0.5f) ? 1f : 0f;
                        break;
                }
            }
        }

        EditorGUILayout.Space(8);
    }

    private void DrawApplySection()
    {
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("应用到选中物体", GUILayout.Height(32)))
        {
            ApplyToSelectedGameObject();
        }
        if (GUILayout.Button("生成新材质并保存", GUILayout.Height(32)))
        {
            SaveMaterialAsset();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("重置所有效果"))
        {
            ResetAllEffects();
        }
    }

    // =========================================================================
    // 逻辑
    // =========================================================================
    private void AnalyzeSprite()
    {
        if (_targetSprite == null) return;
        _extractedColors = SpriteColorAnalyzer.ExtractColors(_targetSprite, 0.05f, 32);
        _selectedColorIndex = -1;

        // 确保有预览材质
        EnsurePreviewMaterial();
    }

    private void DetectExistingShader()
    {
        if (_targetRenderer == null || _targetRenderer.sharedMaterial == null) return;
        _detectedBackend = ShaderBackendAdapter.DetectBackend(_targetRenderer.sharedMaterial);
        if (_detectedBackend != ShaderBackendType.BuiltIn)
        {
            _detectedProps = ShaderBackendAdapter.ScanMaterialProperties(_targetRenderer.sharedMaterial);
        }
    }

    private void EnsurePreviewMaterial()
    {
        var shader = Shader.Find("MarioTrickster/SEF/UberSprite");
        if (shader == null)
        {
            // 回退到默认 Sprite shader
            shader = Shader.Find("Sprites/Default");
        }
        if (_previewMat == null)
        {
            _previewMat = new Material(shader);
        }
        else
        {
            _previewMat.shader = shader;
        }

        if (_targetSprite != null)
            _previewMat.mainTexture = _targetSprite.texture;
    }

    private void AutoFillSwapSlot(Color fromColor)
    {
        // 找到第一个空槽位或最后一个槽位
        for (int i = 0; i < 4; i++)
        {
            if (_swapFrom[i].a < 0.01f || _swapFrom[i] == Color.clear)
            {
                _swapFrom[i] = fromColor;
                return;
            }
        }
        // 全满，覆盖最后一个
        _swapFrom[3] = fromColor;
    }

    private void ApplyColorReplacement(int colorIndex, Color newColor)
    {
        if (colorIndex < 0 || colorIndex >= _extractedColors.Count) return;
        Color oldColor = _extractedColors[colorIndex].color;

        // 找到对应的 swap 槽位并设置目标色
        for (int i = 0; i < 4; i++)
        {
            if (ColorClose(_swapFrom[i], oldColor, _swapTolerance))
            {
                _swapTo[i] = newColor;
                _colorSwapDirty = true;
                return;
            }
        }

        // 没找到，自动填入
        for (int i = 0; i < 4; i++)
        {
            if (_swapFrom[i].a < 0.01f)
            {
                _swapFrom[i] = oldColor;
                _swapTo[i] = newColor;
                _colorSwapDirty = true;
                return;
            }
        }
    }

    private static bool ColorClose(Color a, Color b, float tol)
    {
        return Mathf.Abs(a.r - b.r) < tol
            && Mathf.Abs(a.g - b.g) < tol
            && Mathf.Abs(a.b - b.b) < tol;
    }

    private void ApplyToPreviewMaterial()
    {
        if (_previewMat == null) return;

        bool hasSwap = _swapFrom.Any(c => c.a > 0.01f);
        SetKeyword(_previewMat, "_COLOR_SWAP", hasSwap);
        SetKeyword(_previewMat, "_HIT_FLASH", _flashAmount > 0.001f);
        SetKeyword(_previewMat, "_OUTLINE", _showOutline);
        SetKeyword(_previewMat, "_DISSOLVE", _showDissolve);
        SetKeyword(_previewMat, "_SILHOUETTE", _showSilhouette);
        SetKeyword(_previewMat, "_HSV_ADJUST", _showHSV);
        SetKeyword(_previewMat, "_PIXELATE", _showPixelate);
        SetKeyword(_previewMat, "_SHADOW", _showShadow);

        _previewMat.SetColor("_SwapColor1From", _swapFrom[0]);
        _previewMat.SetColor("_SwapColor1To", _swapTo[0]);
        _previewMat.SetColor("_SwapColor2From", _swapFrom[1]);
        _previewMat.SetColor("_SwapColor2To", _swapTo[1]);
        _previewMat.SetColor("_SwapColor3From", _swapFrom[2]);
        _previewMat.SetColor("_SwapColor3To", _swapTo[2]);
        _previewMat.SetColor("_SwapColor4From", _swapFrom[3]);
        _previewMat.SetColor("_SwapColor4To", _swapTo[3]);
        _previewMat.SetFloat("_SwapTolerance", _swapTolerance);

        _previewMat.SetColor("_FlashColor", _flashColor);
        _previewMat.SetFloat("_FlashAmount", _flashAmount);
        _previewMat.SetColor("_OutlineColor", _outlineColor);
        _previewMat.SetFloat("_OutlineThickness", _outlineThickness);
        _previewMat.SetFloat("_OutlineGlow", _outlineGlow);
        _previewMat.SetFloat("_DissolveAmount", _dissolveAmount);
        _previewMat.SetFloat("_DissolveEdgeWidth", _dissolveEdgeWidth);
        _previewMat.SetColor("_DissolveEdgeColor", _dissolveEdgeColor);
        if (_dissolveNoiseTex != null)
            _previewMat.SetTexture("_DissolveTex", _dissolveNoiseTex);
        _previewMat.SetColor("_SilhouetteColor", _silhouetteColor);
        _previewMat.SetFloat("_HueShift", _hueShift);
        _previewMat.SetFloat("_Saturation", _saturation);
        _previewMat.SetFloat("_Brightness", _brightness);
        _previewMat.SetFloat("_PixelSize", _pixelSize);
        _previewMat.SetColor("_ShadowColor", _shadowColor);
        _previewMat.SetVector("_ShadowOffset", new Vector4(_shadowOffset.x, _shadowOffset.y, 0, 0));

        // 如果有场景中的目标 renderer，实时刷新
        if (_targetRenderer != null)
        {
            _targetRenderer.sharedMaterial = _previewMat;
            SceneView.RepaintAll();
        }
    }

    private void ApplyToSelectedGameObject()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("提示", "请先在场景中选中一个物体", "好的");
            return;
        }

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            EditorUtility.DisplayDialog("提示", "选中的物体没有 SpriteRenderer", "好的");
            return;
        }

        Undo.RecordObject(sr, "SEF Apply Effects");

        // 设置材质
        EnsurePreviewMaterial();
        ApplyToPreviewMaterial();
        sr.sharedMaterial = _previewMat;

        // 自动挂载 SpriteEffectController
        var ctrl = go.GetComponent<SpriteEffectController>();
        if (ctrl == null)
        {
            ctrl = Undo.AddComponent<SpriteEffectController>(go);
        }

        // 同步参数到 Controller
        ctrl.enableColorSwap = _swapFrom.Any(c => c.a > 0.01f);
        ctrl.swapColor1From = _swapFrom[0]; ctrl.swapColor1To = _swapTo[0];
        ctrl.swapColor2From = _swapFrom[1]; ctrl.swapColor2To = _swapTo[1];
        ctrl.swapColor3From = _swapFrom[2]; ctrl.swapColor3To = _swapTo[2];
        ctrl.swapColor4From = _swapFrom[3]; ctrl.swapColor4To = _swapTo[3];
        ctrl.swapTolerance = _swapTolerance;
        ctrl.flashColor = _flashColor;
        ctrl.enableOutline = _showOutline;
        ctrl.outlineColor = _outlineColor;
        ctrl.outlineThickness = _outlineThickness;
        ctrl.outlineGlow = _outlineGlow;
        ctrl.enableDissolve = _showDissolve;
        ctrl.dissolveNoiseTex = _dissolveNoiseTex;
        ctrl.dissolveEdgeWidth = _dissolveEdgeWidth;
        ctrl.dissolveEdgeColor = _dissolveEdgeColor;
        ctrl.enableSilhouette = _showSilhouette;
        ctrl.silhouetteColor = _silhouetteColor;
        ctrl.enableHSV = _showHSV;
        ctrl.hueShift = _hueShift;
        ctrl.saturation = _saturation;
        ctrl.brightness = _brightness;
        ctrl.enablePixelate = _showPixelate;
        ctrl.pixelSize = _pixelSize;
        ctrl.enableShadow = _showShadow;
        ctrl.shadowColor = _shadowColor;
        ctrl.shadowOffset = _shadowOffset;

        EditorUtility.SetDirty(go);
        Debug.Log("[SEF] 效果已应用到 " + go.name);
    }

    private void SaveMaterialAsset()
    {
        if (_previewMat == null)
        {
            EditorUtility.DisplayDialog("提示", "没有可保存的材质，请先拖入素材并调整效果", "好的");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "保存材质", "SEF_NewMaterial", "mat", "选择保存位置");
        if (string.IsNullOrEmpty(path)) return;

        ApplyToPreviewMaterial();
        var saved = new Material(_previewMat);
        AssetDatabase.CreateAsset(saved, path);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(saved);
        Debug.Log("[SEF] 材质已保存到 " + path);
    }

    private void ResetAllEffects()
    {
        _swapFrom = new Color[4];
        _swapTo = new Color[4];
        _swapTolerance = 0.1f;
        _flashColor = Color.white;
        _flashAmount = 0f;
        _showOutline = false;
        _outlineColor = Color.white;
        _outlineThickness = 1f;
        _outlineGlow = 0f;
        _showDissolve = false;
        _dissolveAmount = 0f;
        _dissolveEdgeWidth = 0.05f;
        _dissolveEdgeColor = new Color(1f, 0.5f, 0f, 1f);
        _showSilhouette = false;
        _silhouetteColor = new Color(0f, 0f, 0f, 0.5f);
        _showHSV = false;
        _hueShift = 0f;
        _saturation = 1f;
        _brightness = 1f;
        _showPixelate = false;
        _pixelSize = 8f;
        _showShadow = false;
        _shadowColor = new Color(0f, 0f, 0f, 0.5f);
        _shadowOffset = new Vector2(0.02f, -0.02f);
        _selectedColorIndex = -1;
        Repaint();
    }

    private static void SetKeyword(Material mat, string keyword, bool enabled)
    {
        if (enabled) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    private void OnDestroy()
    {
        if (_previewMat != null)
            DestroyImmediate(_previewMat);
    }
}
