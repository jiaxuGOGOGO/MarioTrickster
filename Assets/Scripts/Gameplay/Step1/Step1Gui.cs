using UnityEngine;

/// <summary>
/// S182：第 1 步统一界面工具（纯表现）。
/// - 中文字体：运行时从系统字体创建（微软雅黑/黑体/苹方/Noto），避免内置 Arial 显示不了中文。
/// - 统一在 1920×1080 虚拟画布里排版，按屏幕高度缩放，任何窗口大小都一样清楚。
/// - 深色半透明面板 + 大字号，只放必要信息。
/// </summary>
public static class Step1Gui
{
    public const float VirtualHeight = 1080f;
    private static readonly string[] CjkFonts = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" };
    private static Font font;
    private static Texture2D panelTex;
    private static GUIStyle panel;

    public static Font Font
    {
        get
        {
            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont(CjkFonts, 32); }
                catch { font = null; }
            }
            return font;
        }
    }

    /// <summary>开始一帧 OnGUI：设置缩放矩阵，返回虚拟画布宽度。</summary>
    public static float Begin()
    {
        float s = Mathf.Max(0.1f, Screen.height / VirtualHeight);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        return Screen.width / s;
    }

    public static GUIStyle Text(int size, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true)
    {
        var st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap, richText = true };
        if (Font != null) st.font = Font;
        st.normal.textColor = Color.white;
        return st;
    }

    public static void Panel(Rect r, float alpha = 0.78f)
    {
        if (panelTex == null)
        {
            panelTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panelTex.SetPixel(0, 0, Color.white); panelTex.Apply();
        }
        if (panel == null) panel = new GUIStyle { normal = { background = panelTex } };
        Color old = GUI.color;
        GUI.color = new Color(0.05f, 0.06f, 0.10f, alpha);
        GUI.Box(r, GUIContent.none, panel);
        GUI.color = old;
    }

    /// <summary>给 TextMesh 换成能显示中文的字体。</summary>
    public static void ApplyFont(TextMesh text)
    {
        if (text == null || Font == null) return;
        text.font = Font;
        var renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sharedMaterial = Font.material;
    }
}
