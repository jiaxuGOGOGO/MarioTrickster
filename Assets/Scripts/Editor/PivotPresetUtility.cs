#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// PivotPresetUtility — 统一 Pivot 预设与手动选择工具
///
/// 核心职责：
///   为 AssetImportPipeline、AssetApplyToSelected、AI_SpriteSlicer 提供统一的
///   Pivot 预设枚举、自动推断逻辑和 GUI 绘制方法。
///   参考 Unity Sprite Editor 的 9 宫格 Pivot 选项，同时增加 Auto 模式和 Custom 模式。
///
/// 设计原则：
///   1. Auto 模式按素材分类自动选择（角色→BottomCenter，其他→Center），零摩擦
///   2. 用户可随时手动覆盖为任意预设或自定义坐标
///   3. 所有修改都经过 Undo 系统，可安全回退
///   4. 预设列表与 Unity Sprite Editor 一致，降低学习成本
///
/// [AI防坑警告]
///   1. 修改此文件时必须同步检查 TA_AssetValidator 的校验规则
///   2. Auto 模式的推断逻辑必须与 ArtAssetClassifier 的角色识别保持一致
///   3. 新增预设时必须同步更新 PivotToVector2 和 Vector2ToPivot
/// </summary>
public static class PivotPresetUtility
{
    // =========================================================================
    // 枚举：Pivot 预设（对齐 Unity SpriteAlignment + Auto + Custom）
    // =========================================================================
    public enum PivotPreset
    {
        Auto = -1,           // 按素材分类自动选择
        Center = 0,          // (0.5, 0.5)
        TopLeft = 1,         // (0, 1)
        TopCenter = 2,       // (0.5, 1)
        TopRight = 3,        // (1, 1)
        LeftCenter = 4,      // (0, 0.5)
        RightCenter = 5,     // (1, 0.5)
        BottomLeft = 6,      // (0, 0)
        BottomCenter = 7,    // (0.5, 0)
        BottomRight = 8,     // (1, 0)
        Custom = 9           // 用户自定义 (x, y)
    }

    // =========================================================================
    // 预设 → Vector2 转换
    // =========================================================================
    public static Vector2 PivotToVector2(PivotPreset preset, Vector2 customPivot = default)
    {
        switch (preset)
        {
            case PivotPreset.Center:       return new Vector2(0.5f, 0.5f);
            case PivotPreset.TopLeft:      return new Vector2(0f, 1f);
            case PivotPreset.TopCenter:    return new Vector2(0.5f, 1f);
            case PivotPreset.TopRight:     return new Vector2(1f, 1f);
            case PivotPreset.LeftCenter:   return new Vector2(0f, 0.5f);
            case PivotPreset.RightCenter:  return new Vector2(1f, 0.5f);
            case PivotPreset.BottomLeft:   return new Vector2(0f, 0f);
            case PivotPreset.BottomCenter: return new Vector2(0.5f, 0f);
            case PivotPreset.BottomRight:  return new Vector2(1f, 0f);
            case PivotPreset.Custom:       return customPivot;
            default:                       return new Vector2(0.5f, 0.5f);
        }
    }

    /// <summary>
    /// 将 PivotPreset 转换为 Unity SpriteAlignment 枚举值（int）。
    /// Custom 返回 (int)SpriteAlignment.Custom = 9。
    /// Auto 不应直接调用此方法，应先 Resolve 再转换。
    /// </summary>
    public static int PivotToAlignment(PivotPreset preset)
    {
        switch (preset)
        {
            case PivotPreset.Center:       return (int)SpriteAlignment.Center;
            case PivotPreset.TopLeft:      return (int)SpriteAlignment.TopLeft;
            case PivotPreset.TopCenter:    return (int)SpriteAlignment.TopCenter;
            case PivotPreset.TopRight:     return (int)SpriteAlignment.TopRight;
            case PivotPreset.LeftCenter:   return (int)SpriteAlignment.LeftCenter;
            case PivotPreset.RightCenter:  return (int)SpriteAlignment.RightCenter;
            case PivotPreset.BottomLeft:   return (int)SpriteAlignment.BottomLeft;
            case PivotPreset.BottomCenter: return (int)SpriteAlignment.BottomCenter;
            case PivotPreset.BottomRight:  return (int)SpriteAlignment.BottomRight;
            case PivotPreset.Custom:       return (int)SpriteAlignment.Custom;
            default:                       return (int)SpriteAlignment.Center;
        }
    }

    /// <summary>
    /// 从 Vector2 反推最接近的 PivotPreset。
    /// 用于读取已有 Sprite 的 pivot 并在 UI 中显示正确的选项。
    /// </summary>
    public static PivotPreset Vector2ToPivot(Vector2 pivot)
    {
        if (ApproxEqual(pivot, new Vector2(0.5f, 0.5f))) return PivotPreset.Center;
        if (ApproxEqual(pivot, new Vector2(0f, 1f)))     return PivotPreset.TopLeft;
        if (ApproxEqual(pivot, new Vector2(0.5f, 1f)))   return PivotPreset.TopCenter;
        if (ApproxEqual(pivot, new Vector2(1f, 1f)))     return PivotPreset.TopRight;
        if (ApproxEqual(pivot, new Vector2(0f, 0.5f)))   return PivotPreset.LeftCenter;
        if (ApproxEqual(pivot, new Vector2(1f, 0.5f)))   return PivotPreset.RightCenter;
        if (ApproxEqual(pivot, new Vector2(0f, 0f)))     return PivotPreset.BottomLeft;
        if (ApproxEqual(pivot, new Vector2(0.5f, 0f)))   return PivotPreset.BottomCenter;
        if (ApproxEqual(pivot, new Vector2(1f, 0f)))     return PivotPreset.BottomRight;
        return PivotPreset.Custom;
    }

    // =========================================================================
    // 自动推断：根据目标对象和素材分类决定最佳 Pivot
    // =========================================================================

    /// <summary>
    /// 根据目标 GameObject 自动推断 Pivot 预设。
    /// 优先级：
    ///   1. ImportedAssetMarker.physicsType == 0 (Character) → BottomCenter
    ///   2. 有 MarioController / 有 "Controller" 类型组件 → BottomCenter
    ///   3. 有 BaseHazard / DamageDealer → Center
    ///   4. 默认 → Center
    /// </summary>
    public static PivotPreset AutoDetectPivot(GameObject target)
    {
        if (target == null) return PivotPreset.Center;

        // 检查 ImportedAssetMarker
        var marker = target.GetComponent<ImportedAssetMarker>();
        if (marker != null)
        {
            if (marker.physicsType == 0) return PivotPreset.BottomCenter; // Character
            return PivotPreset.Center;
        }

        // 检查角色控制器组件（MarioController、TricksterController 等）
        if (HasCharacterController(target)) return PivotPreset.BottomCenter;

        // 检查敌人/陷阱组件
        var hazard = target.GetComponentInChildren<BaseHazard>();
        if (hazard != null) return PivotPreset.Center;

        var dealer = target.GetComponentInChildren<DamageDealer>();
        if (dealer != null) return PivotPreset.Center;

        return PivotPreset.Center;
    }

    /// <summary>
    /// 根据 physicsType 整数值推断 Pivot 预设。
    /// 兼容 AssetApplyToSelected 的 GetPhysicsTypeHint 返回值。
    /// -1 表示未知，此时需要结合目标对象进一步推断。
    /// </summary>
    public static PivotPreset AutoDetectFromPhysicsType(int physicsTypeHint, GameObject target = null)
    {
        if (physicsTypeHint == 0) return PivotPreset.BottomCenter; // Character
        if (physicsTypeHint > 0) return PivotPreset.Center;        // Environment/Hazard/VFX/Prop

        // physicsTypeHint == -1: 没有 marker，需要从对象组件推断
        if (target != null) return AutoDetectPivot(target);
        return PivotPreset.Center;
    }

    /// <summary>
    /// 解析 Auto 预设：如果是 Auto，执行自动推断；否则直接返回用户选择。
    /// 这是所有切片/导入代码应该调用的统一入口。
    /// </summary>
    public static PivotPreset ResolvePreset(PivotPreset userChoice, int physicsTypeHint = -1, GameObject target = null)
    {
        if (userChoice != PivotPreset.Auto) return userChoice;
        return AutoDetectFromPhysicsType(physicsTypeHint, target);
    }

    // =========================================================================
    // GUI 绘制：统一的 Pivot 选择 UI
    // =========================================================================

    /// <summary>
    /// 绘制 Pivot 选择 UI，包含预设下拉框 + 9 宫格可视化 + Custom 输入。
    /// 返回用户是否修改了选项。
    /// </summary>
    public static bool DrawPivotSelector(
        string label,
        ref PivotPreset preset,
        ref Vector2 customPivot,
        PivotPreset autoResolvedPreset = PivotPreset.Center)
    {
        bool changed = false;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth));

        // 构建显示名称数组
        string autoLabel = $"Auto ({GetPresetDisplayName(autoResolvedPreset)})";
        string[] displayNames = new string[]
        {
            autoLabel,
            "Center",
            "Top Left",
            "Top Center",
            "Top Right",
            "Left Center",
            "Right Center",
            "Bottom Left",
            "Bottom Center",
            "Bottom Right",
            "Custom"
        };

        // 将枚举映射到下拉索引
        int currentIndex = PresetToDropdownIndex(preset);
        int newIndex = EditorGUILayout.Popup(currentIndex, displayNames);
        if (newIndex != currentIndex)
        {
            preset = DropdownIndexToPreset(newIndex);
            changed = true;
        }
        EditorGUILayout.EndHorizontal();

        // Auto 模式提示
        if (preset == PivotPreset.Auto)
        {
            Vector2 resolved = PivotToVector2(autoResolvedPreset);
            EditorGUILayout.HelpBox(
                $"自动推断: {GetPresetDisplayName(autoResolvedPreset)} ({resolved.x:F1}, {resolved.y:F1})。" +
                "如需手动指定，请从下拉菜单选择其他选项。",
                MessageType.None);
        }

        // Custom 模式输入
        if (preset == PivotPreset.Custom)
        {
            EditorGUI.indentLevel++;
            Vector2 newCustom = EditorGUILayout.Vector2Field("自定义 Pivot (0~1)", customPivot);
            newCustom.x = Mathf.Clamp01(newCustom.x);
            newCustom.y = Mathf.Clamp01(newCustom.y);
            if (newCustom != customPivot)
            {
                customPivot = newCustom;
                changed = true;
            }
            EditorGUILayout.HelpBox(
                "X=0 左边, X=1 右边, Y=0 底部, Y=1 顶部。" +
                "例如角色脚底居中 = (0.5, 0)，道具正中心 = (0.5, 0.5)。",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        // 9 宫格可视化按钮（Custom 模式不显示，因为 Custom 用数字输入）
        // Auto 模式也显示 9 宫格，点击后自动切换为对应预设，方便用户快速覆盖
        if (preset != PivotPreset.Custom)
        {
            PivotPreset beforeGrid = preset;
            DrawPivotGrid(ref preset, ref changed);
            // 如果用户在 Auto 模式下点了 9 宫格，自动切换到对应的具体预设
            if (beforeGrid == PivotPreset.Auto && preset != PivotPreset.Auto)
            {
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// 绘制 9 宫格 Pivot 快捷按钮，参考 Unity Sprite Editor 的布局。
    /// </summary>
    private static void DrawPivotGrid(ref PivotPreset preset, ref bool changed)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        float btnSize = 22f;
        // 3x3 网格
        EditorGUILayout.BeginVertical(GUILayout.Width(btnSize * 3 + 6));

        // Top row
        EditorGUILayout.BeginHorizontal();
        if (DrawPivotButton("↖", preset == PivotPreset.TopLeft, btnSize))     { preset = PivotPreset.TopLeft; changed = true; }
        if (DrawPivotButton("↑", preset == PivotPreset.TopCenter, btnSize))   { preset = PivotPreset.TopCenter; changed = true; }
        if (DrawPivotButton("↗", preset == PivotPreset.TopRight, btnSize))    { preset = PivotPreset.TopRight; changed = true; }
        EditorGUILayout.EndHorizontal();

        // Middle row
        EditorGUILayout.BeginHorizontal();
        if (DrawPivotButton("←", preset == PivotPreset.LeftCenter, btnSize))  { preset = PivotPreset.LeftCenter; changed = true; }
        if (DrawPivotButton("●", preset == PivotPreset.Center, btnSize))      { preset = PivotPreset.Center; changed = true; }
        if (DrawPivotButton("→", preset == PivotPreset.RightCenter, btnSize)) { preset = PivotPreset.RightCenter; changed = true; }
        EditorGUILayout.EndHorizontal();

        // Bottom row
        EditorGUILayout.BeginHorizontal();
        if (DrawPivotButton("↙", preset == PivotPreset.BottomLeft, btnSize))    { preset = PivotPreset.BottomLeft; changed = true; }
        if (DrawPivotButton("↓", preset == PivotPreset.BottomCenter, btnSize))  { preset = PivotPreset.BottomCenter; changed = true; }
        if (DrawPivotButton("↘", preset == PivotPreset.BottomRight, btnSize))   { preset = PivotPreset.BottomRight; changed = true; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static bool DrawPivotButton(string label, bool isActive, float size)
    {
        Color oldBg = GUI.backgroundColor;
        if (isActive) GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        bool clicked = GUILayout.Button(label, GUILayout.Width(size), GUILayout.Height(size));
        GUI.backgroundColor = oldBg;
        return clicked;
    }

    // =========================================================================
    // 辅助方法
    // =========================================================================

    public static string GetPresetDisplayName(PivotPreset preset)
    {
        switch (preset)
        {
            case PivotPreset.Auto:         return "Auto";
            case PivotPreset.Center:       return "Center";
            case PivotPreset.TopLeft:      return "Top Left";
            case PivotPreset.TopCenter:    return "Top Center";
            case PivotPreset.TopRight:     return "Top Right";
            case PivotPreset.LeftCenter:   return "Left Center";
            case PivotPreset.RightCenter:  return "Right Center";
            case PivotPreset.BottomLeft:   return "Bottom Left";
            case PivotPreset.BottomCenter: return "Bottom Center";
            case PivotPreset.BottomRight:  return "Bottom Right";
            case PivotPreset.Custom:       return "Custom";
            default:                       return "Unknown";
        }
    }

    /// <summary>
    /// 公共接口：检测目标是否是玩家角色（供 AutoFitCollider 等外部方法调用）
    /// </summary>
    public static bool HasCharacterControllerPublic(GameObject target)
    {
        return HasCharacterController(target);
    }

    private static bool HasCharacterController(GameObject target)
    {
        if (target == null) return false;

        // 直接检查已知的角色控制器类型
        // 使用 GetComponent 按名称匹配，避免硬依赖具体类
        foreach (var comp in target.GetComponents<MonoBehaviour>())
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName.Contains("MarioController") ||
                typeName.Contains("TricksterController") ||
                typeName.Contains("PlayerController") ||
                typeName.Contains("CharacterController2D") ||
                typeName.Contains("EnemyController"))
            {
                return true;
            }
        }

        // 也检查子物体（视碰分离架构下控制器在 Root，Visual 在子节点）
        foreach (var comp in target.GetComponentsInChildren<MonoBehaviour>())
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName.Contains("MarioController") ||
                typeName.Contains("TricksterController") ||
                typeName.Contains("PlayerController"))
            {
                return true;
            }
        }

        // 检查父物体（用户可能选中了 Visual 子节点）
        if (target.transform.parent != null)
        {
            foreach (var comp in target.transform.parent.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName.Contains("MarioController") ||
                    typeName.Contains("TricksterController") ||
                    typeName.Contains("PlayerController"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int PresetToDropdownIndex(PivotPreset preset)
    {
        switch (preset)
        {
            case PivotPreset.Auto:         return 0;
            case PivotPreset.Center:       return 1;
            case PivotPreset.TopLeft:      return 2;
            case PivotPreset.TopCenter:    return 3;
            case PivotPreset.TopRight:     return 4;
            case PivotPreset.LeftCenter:   return 5;
            case PivotPreset.RightCenter:  return 6;
            case PivotPreset.BottomLeft:   return 7;
            case PivotPreset.BottomCenter: return 8;
            case PivotPreset.BottomRight:  return 9;
            case PivotPreset.Custom:       return 10;
            default:                       return 0;
        }
    }

    private static PivotPreset DropdownIndexToPreset(int index)
    {
        switch (index)
        {
            case 0:  return PivotPreset.Auto;
            case 1:  return PivotPreset.Center;
            case 2:  return PivotPreset.TopLeft;
            case 3:  return PivotPreset.TopCenter;
            case 4:  return PivotPreset.TopRight;
            case 5:  return PivotPreset.LeftCenter;
            case 6:  return PivotPreset.RightCenter;
            case 7:  return PivotPreset.BottomLeft;
            case 8:  return PivotPreset.BottomCenter;
            case 9:  return PivotPreset.BottomRight;
            case 10: return PivotPreset.Custom;
            default: return PivotPreset.Auto;
        }
    }

    private static bool ApproxEqual(Vector2 a, Vector2 b)
    {
        return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
    }
}
#endif
