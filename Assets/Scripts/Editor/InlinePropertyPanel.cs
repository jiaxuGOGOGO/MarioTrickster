using UnityEngine;
using UnityEditor;

/// <summary>
/// InlinePropertyPanel — GBG 风格的 Scene 视图内嵌属性面板
///
/// 核心理念（来自 Game Builder Garage 的 Nodon Settings）：
///   GBG 中点击任何 Nodon 就能直接看到并修改它的关键属性，
///   不需要切换到单独的属性窗口。属性修改后即时生效。
///
/// 在 MarioTrickster 中的映射：
///   选中任何关卡元素后，在 Scene 视图中直接显示该元素的关键参数滑块：
///   - 移动平台：速度、移动距离、等待时间
///   - 传送带：速度、方向
///   - 弹跳平台：弹跳力度
///   - 崩塌平台：崩塌延迟、重生时间
///   - 敌人：移动速度、巡逻范围
///   - PossessionAnchor：附身残留时间、启用/禁用
///
/// 设计原则：
///   - 只显示【最关键】的 2-4 个参数（不是全部 Inspector 属性）
///   - 修改即时生效 + 支持 Undo
///   - 面板跟随选中物体位置
///   - 不干扰正常的 Scene 操作（可通过 F7 开关）
/// </summary>
[InitializeOnLoad]
public static class InlinePropertyPanel
{
    // ═══════════════════════════════════════════════════
    // 状态
    // ═══════════════════════════════════════════════════
    private const string PREF_ENABLED = "MarioTrickster_InlineProp_Enabled";

    public static bool IsEnabled
    {
        get => EditorPrefs.GetBool(PREF_ENABLED, true);
        set => EditorPrefs.SetBool(PREF_ENABLED, value);
    }

    private static float _panelWidth = 220f;
    private static float _panelBaseHeight = 30f;
    private static float _lineHeight = 22f;

    // ═══════════════════════════════════════════════════
    // 初始化
    // ═══════════════════════════════════════════════════
    static InlinePropertyPanel()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // ═══════════════════════════════════════════════════
    // 菜单
    // ═══════════════════════════════════════════════════

    [MenuItem("MarioTrickster/Toggle Inline Properties _F7", false, 102)]
    public static void TogglePanel()
    {
        IsEnabled = !IsEnabled;
        SceneView.RepaintAll();
        Debug.Log($"[InlineProp] 内嵌属性面板: {(IsEnabled ? "开启" : "关闭")}");
    }

    // ═══════════════════════════════════════════════════
    // Scene GUI
    // ═══════════════════════════════════════════════════

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!IsEnabled) return;
        if (EditorApplication.isPlaying) return;

        GameObject selected = Selection.activeGameObject;
        if (selected == null) return;

        // 检查是否是关卡元素（在 AsciiLevel_Root 下或有相关组件）
        if (!IsLevelElement(selected)) return;

        // 获取该元素的可编辑属性
        var properties = GetEditableProperties(selected);
        if (properties == null || properties.Length == 0) return;

        // 计算面板位置（在物体上方）
        Vector3 worldPos = selected.transform.position;
        Vector2 screenPos = HandleUtility.WorldToGUIPoint(worldPos + Vector3.up * 1.5f);

        // 绘制面板
        Handles.BeginGUI();
        DrawPropertyPanel(screenPos, selected, properties);
        Handles.EndGUI();
    }

    // ═══════════════════════════════════════════════════
    // 属性面板绘制
    // ═══════════════════════════════════════════════════

    private static void DrawPropertyPanel(Vector2 screenPos, GameObject target, PropertyDef[] properties)
    {
        float panelHeight = _panelBaseHeight + properties.Length * _lineHeight + 10f;
        Rect panelRect = new Rect(
            screenPos.x - _panelWidth / 2f,
            screenPos.y - panelHeight,
            _panelWidth,
            panelHeight);

        // 确保面板不超出视图
        if (panelRect.x < 5) panelRect.x = 5;
        if (panelRect.y < 5) panelRect.y = 5;

        // 背景
        Color bgColor = new Color(0.15f, 0.15f, 0.2f, 0.92f);
        GUI.color = bgColor;
        GUI.DrawTexture(panelRect, EditorGUIUtility.whiteTexture);
        GUI.color = Color.white;

        // 边框
        Color borderColor = new Color(0.4f, 0.7f, 1f, 0.6f);
        DrawBorder(panelRect, borderColor);

        GUILayout.BeginArea(panelRect);
        GUILayout.Space(4);

        // 标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = new Color(0.7f, 0.9f, 1f) },
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label($"⚙ {GetElementTypeName(target)}", titleStyle);

        GUILayout.Space(2);

        // 属性滑块
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
        };

        foreach (var prop in properties)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(prop.label, labelStyle, GUILayout.Width(80));

            float newValue = prop.currentValue;

            if (prop.isBoolean)
            {
                bool boolVal = prop.currentValue > 0.5f;
                bool newBool = GUILayout.Toggle(boolVal, boolVal ? "ON" : "OFF", GUILayout.Width(100));
                newValue = newBool ? 1f : 0f;
            }
            else
            {
                newValue = GUILayout.HorizontalSlider(prop.currentValue, prop.min, prop.max, GUILayout.Width(80));
                GUILayout.Label($"{newValue:F1}{prop.unit}", labelStyle, GUILayout.Width(40));
            }

            if (!Mathf.Approximately(newValue, prop.currentValue))
            {
                Undo.RecordObject(prop.targetComponent, $"Edit {prop.label}");
                prop.setter(newValue);
                EditorUtility.SetDirty(prop.targetComponent);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    // ═══════════════════════════════════════════════════
    // 属性定义
    // ═══════════════════════════════════════════════════

    private class PropertyDef
    {
        public string label;
        public float currentValue;
        public float min;
        public float max;
        public string unit;
        public bool isBoolean;
        public Component targetComponent;
        public System.Action<float> setter;

        public PropertyDef(string label, float current, float min, float max, string unit,
            Component target, System.Action<float> setter, bool isBoolean = false)
        {
            this.label = label;
            this.currentValue = current;
            this.min = min;
            this.max = max;
            this.unit = unit;
            this.isBoolean = isBoolean;
            this.targetComponent = target;
            this.setter = setter;
        }
    }

    // ═══════════════════════════════════════════════════
    // 属性提取
    // ═══════════════════════════════════════════════════

    private static PropertyDef[] GetEditableProperties(GameObject obj)
    {
        var props = new System.Collections.Generic.List<PropertyDef>();

        // MovingPlatform
        var mp = obj.GetComponent<MovingPlatform>();
        if (mp == null) mp = obj.GetComponentInParent<MovingPlatform>();
        if (mp != null)
        {
            var speedField = mp.GetType().GetField("moveSpeed",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var waitField = mp.GetType().GetField("waitTime",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (speedField != null)
            {
                float speed = (float)speedField.GetValue(mp);
                props.Add(new PropertyDef("速度", speed, 0.5f, 8f, "", mp,
                    v => speedField.SetValue(mp, v)));
            }
            if (waitField != null)
            {
                float wait = (float)waitField.GetValue(mp);
                props.Add(new PropertyDef("等待", wait, 0f, 5f, "s", mp,
                    v => waitField.SetValue(mp, v)));
            }
            return props.ToArray();
        }

        // ControllablePlatform
        var cp = obj.GetComponent<ControllablePlatform>();
        if (cp == null) cp = obj.GetComponentInParent<ControllablePlatform>();
        if (cp != null)
        {
            var speedField = cp.GetType().GetField("normalMoveSpeed",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (speedField != null)
            {
                float speed = (float)speedField.GetValue(cp);
                props.Add(new PropertyDef("速度", speed, 0.5f, 8f, "", cp,
                    v => speedField.SetValue(cp, v)));
            }
            return props.ToArray();
        }

        // ConveyorBelt
        var belt = obj.GetComponent<ConveyorBelt>();
        if (belt == null) belt = obj.GetComponentInParent<ConveyorBelt>();
        if (belt != null)
        {
            var speedField = belt.GetType().GetField("conveyorSpeed",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (speedField != null)
            {
                float speed = (float)speedField.GetValue(belt);
                props.Add(new PropertyDef("速度", speed, -6f, 6f, "", belt,
                    v => speedField.SetValue(belt, v)));
            }
            return props.ToArray();
        }

        // BouncyPlatform
        var bouncy = obj.GetComponent<BouncyPlatform>();
        if (bouncy == null) bouncy = obj.GetComponentInParent<BouncyPlatform>();
        if (bouncy != null)
        {
            var forceField = bouncy.GetType().GetField("bounceForce",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (forceField != null)
            {
                float force = (float)forceField.GetValue(bouncy);
                props.Add(new PropertyDef("弹力", force, 5f, 25f, "", bouncy,
                    v => forceField.SetValue(bouncy, v)));
            }
            return props.ToArray();
        }

        // CollapsingPlatform
        var collapse = obj.GetComponent<CollapsingPlatform>();
        if (collapse == null) collapse = obj.GetComponentInParent<CollapsingPlatform>();
        if (collapse != null)
        {
            var delayField = collapse.GetType().GetField("collapseDelay",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var respawnField = collapse.GetType().GetField("respawnDelay",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (delayField != null)
            {
                float delay = (float)delayField.GetValue(collapse);
                props.Add(new PropertyDef("崩塌延迟", delay, 0.1f, 3f, "s", collapse,
                    v => delayField.SetValue(collapse, v)));
            }
            if (respawnField != null)
            {
                float respawn = (float)respawnField.GetValue(collapse);
                props.Add(new PropertyDef("重生时间", respawn, 1f, 10f, "s", collapse,
                    v => respawnField.SetValue(collapse, v)));
            }
            return props.ToArray();
        }

        // PossessionAnchor
        var anchor = obj.GetComponent<PossessionAnchor>();
        if (anchor == null) anchor = obj.GetComponentInParent<PossessionAnchor>();
        if (anchor != null)
        {
            props.Add(new PropertyDef("启用", anchor.PossessionEnabled ? 1f : 0f, 0f, 1f, "", anchor,
                v => {
                    var enabledProp = anchor.GetType().GetProperty("PossessionEnabled");
                    if (enabledProp != null && enabledProp.CanWrite)
                        enabledProp.SetValue(anchor, v > 0.5f);
                }, true));

            var residueField = anchor.GetType().GetField("defaultResidueSeconds",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (residueField != null)
            {
                float residue = (float)residueField.GetValue(anchor);
                props.Add(new PropertyDef("残留时间", residue, 0.5f, 10f, "s", anchor,
                    v => residueField.SetValue(anchor, v)));
            }
            return props.ToArray();
        }

        return props.ToArray();
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    private static bool IsLevelElement(GameObject obj)
    {
        // 检查是否在 AsciiLevel_Root 下
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.name == "AsciiLevel_Root") return true;
            current = current.parent;
        }

        // 或者有相关组件
        if (obj.GetComponent<MovingPlatform>() != null) return true;
        if (obj.GetComponent<ControllablePlatform>() != null) return true;
        if (obj.GetComponent<ConveyorBelt>() != null) return true;
        if (obj.GetComponent<BouncyPlatform>() != null) return true;
        if (obj.GetComponent<CollapsingPlatform>() != null) return true;
        if (obj.GetComponent<PossessionAnchor>() != null) return true;

        return false;
    }

    private static string GetElementTypeName(GameObject obj)
    {
        if (obj.GetComponent<MovingPlatform>() != null || obj.GetComponentInParent<MovingPlatform>() != null)
            return "移动平台";
        if (obj.GetComponent<ControllablePlatform>() != null || obj.GetComponentInParent<ControllablePlatform>() != null)
            return "可控平台";
        if (obj.GetComponent<ConveyorBelt>() != null || obj.GetComponentInParent<ConveyorBelt>() != null)
            return "传送带";
        if (obj.GetComponent<BouncyPlatform>() != null || obj.GetComponentInParent<BouncyPlatform>() != null)
            return "弹跳平台";
        if (obj.GetComponent<CollapsingPlatform>() != null || obj.GetComponentInParent<CollapsingPlatform>() != null)
            return "崩塌平台";
        if (obj.GetComponent<PossessionAnchor>() != null || obj.GetComponentInParent<PossessionAnchor>() != null)
            return "附身锚点";

        return obj.name;
    }

    private static void DrawBorder(Rect rect, Color color)
    {
        Handles.color = color;
        Vector3[] corners = new Vector3[5]
        {
            new Vector3(rect.xMin, rect.yMin, 0),
            new Vector3(rect.xMax, rect.yMin, 0),
            new Vector3(rect.xMax, rect.yMax, 0),
            new Vector3(rect.xMin, rect.yMax, 0),
            new Vector3(rect.xMin, rect.yMin, 0)
        };
        Handles.DrawPolyLine(corners);
    }
}
