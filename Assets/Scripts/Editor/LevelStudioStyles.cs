using UnityEngine;
using UnityEditor;

/// <summary>
/// Level Studio 统一视觉语言工具类。
/// 
/// 设计原则（参考 LDtk / Mario Maker / Progressive Disclosure）：
///   1. 渐进式信息披露 — 默认只展示核心操作，细节按需展开
///   2. 视觉降噪 — 用 Tooltip 替代长文 HelpBox，减少垂直空间占用
///   3. 统一色彩语义 — 绿=生成/确认，蓝=信息/导入，橙=警告/AI，红=危险/清除
///   4. 紧凑卡片布局 — 片段库等列表项用单行卡片替代多行展开
///   5. 分级标题 — SectionHeader / SubHeader / MiniHeader 三级层次
/// </summary>
public static class LevelStudioStyles
{
    // ═══════════════════════════════════════════════════
    // 语义色彩系统
    // ═══════════════════════════════════════════════════
    public static readonly Color AccentGreen   = new Color(0.4f, 0.9f, 0.4f);   // 生成、确认、通过
    public static readonly Color AccentBlue    = new Color(0.5f, 0.85f, 1f);    // 信息、导入、导航
    public static readonly Color AccentOrange  = new Color(1f, 0.6f, 0.2f);     // 警告、AI 功能
    public static readonly Color AccentRed     = new Color(1f, 0.45f, 0.45f);   // 危险、清除、删除
    public static readonly Color AccentYellow  = new Color(1f, 0.85f, 0.3f);    // 追加、收藏、高亮
    public static readonly Color AccentPurple  = new Color(0.7f, 0.6f, 1f);     // 高级工具、效果
    public static readonly Color Muted         = new Color(0.7f, 0.7f, 0.7f);   // 次要、禁用态
    public static readonly Color BgDark        = new Color(0.18f, 0.18f, 0.18f);

    // ═══════════════════════════════════════════════════
    // 布局辅助
    // ═══════════════════════════════════════════════════

    /// <summary>绘制带图标的分区标题（一级标题，用于 Tab 内主要区块）</summary>
    public static bool SectionHeader(string title, ref bool foldout, string tooltip = null)
    {
        EditorGUILayout.Space(2);
        var content = tooltip != null ? new GUIContent(title, tooltip) : new GUIContent(title);
        foldout = EditorGUILayout.Foldout(foldout, content, true, EditorStyles.foldoutHeader);
        return foldout;
    }

    /// <summary>绘制紧凑型子标题（二级标题，用于区块内分组）</summary>
    public static void SubHeader(string title)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    /// <summary>绘制迷你标签（三级标题，用于行内分类）</summary>
    public static void MiniHeader(string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
    }

    /// <summary>紧凑提示条 — 用单行 miniLabel 替代多行 HelpBox，大幅降低垂直空间</summary>
    public static void CompactTip(string text)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            richText = true,
            normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
        };
        EditorGUILayout.LabelField(text, style);
    }

    /// <summary>紧凑信息条 — 比 HelpBox 更节省空间的信息提示</summary>
    public static void InfoStrip(string text)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.5f, 0.7f, 0.9f, 0.6f);
        EditorGUILayout.BeginHorizontal("helpbox");
        GUI.color = prev;
        var style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
        EditorGUILayout.LabelField(text, style);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>带颜色的操作按钮</summary>
    public static bool ColorButton(string label, Color color, float height = 26f, float width = 0f)
    {
        var prev = GUI.color;
        GUI.color = color;
        bool clicked;
        if (width > 0)
            clicked = GUILayout.Button(label, GUILayout.Height(height), GUILayout.Width(width));
        else
            clicked = GUILayout.Button(label, GUILayout.Height(height));
        GUI.color = prev;
        return clicked;
    }

    /// <summary>分隔线</summary>
    public static void Separator()
    {
        EditorGUILayout.Space(2);
        var rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(2);
    }

    /// <summary>获取等宽字体文本框样式</summary>
    public static GUIStyle MonoTextArea()
    {
        return new GUIStyle(EditorStyles.textArea)
        {
            font = Font.CreateDynamicFontFromOSFont("Courier New", 12),
            fontSize = 12,
            wordWrap = false
        };
    }

    /// <summary>获取富文本迷你标签样式</summary>
    public static GUIStyle RichMiniLabel()
    {
        return new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
    }
}
