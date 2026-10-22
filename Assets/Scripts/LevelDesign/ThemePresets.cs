using UnityEngine;

/// <summary>
/// S187：主题预设（游乐园 / 公园 / 山上公园）。给未来换背景打底子：
///   - 只生成一份运行时 LevelThemeProfile（颜色 + 空 Sprite 插槽），交给现有的 AsciiLevelGenerator.ApplyTheme 应用；
///   - 美术以后做好素材，只要在 LevelThemeProfile 资产里给对应键（Ground/Wall/Crate/Bush/Decor/Cannon…）拖图，
///     不改任何玩法代码（沿用项目"换肤零代码"原则：空插槽 = 保留白盒，绝不报错）；
///   - 颜色只是白盒配色，用来一眼分出场景风格，不是最终美术。
/// </summary>
public static class ThemePresets
{
    public const string Whitebox = "Whitebox";
    public const string AmusementPark = "AmusementPark";
    public const string CityPark = "CityPark";
    public const string MountainPark = "MountainPark";
    public static readonly string[] All = { Whitebox, AmusementPark, CityPark, MountainPark };

    public static bool IsKnown(string name) => System.Array.IndexOf(All, name) >= 0;

    /// <summary>生成预设主题（Whitebox 返回 null = 不换肤）。</summary>
    public static LevelThemeProfile Create(string name)
    {
        if (!IsKnown(name) || name == Whitebox) return null;
        var p = ScriptableObject.CreateInstance<LevelThemeProfile>();
        p.hideFlags = HideFlags.HideAndDontSave;
        p.themeName = name;
        switch (name)
        {
            case AmusementPark:
                p.themeDescription = "游乐园：彩色地砖、糖果色箱子、旋转木马装饰";
                p.backgroundColor = new Color(0.98f, 0.80f, 0.62f);
                p.groundColor = new Color(0.55f, 0.38f, 0.62f);
                p.wallColor = new Color(0.86f, 0.30f, 0.38f);
                p.platformColor = new Color(0.30f, 0.72f, 0.86f);
                Tint(p, "Crate", new Color(0.98f, 0.62f, 0.20f));
                Tint(p, "Bush", new Color(0.35f, 0.78f, 0.45f, 0.9f));
                Tint(p, "Decor", new Color(1f, 0.85f, 0.35f, 0.9f));
                Tint(p, "Cannon", new Color(0.25f, 0.20f, 0.45f));
                break;
            case CityPark:
                p.themeDescription = "公园：草地、长椅、灌木";
                p.backgroundColor = new Color(0.68f, 0.86f, 0.96f);
                p.groundColor = new Color(0.36f, 0.58f, 0.30f);
                p.wallColor = new Color(0.55f, 0.50f, 0.45f);
                p.platformColor = new Color(0.62f, 0.45f, 0.30f);
                Tint(p, "Crate", new Color(0.55f, 0.38f, 0.22f));
                Tint(p, "Bush", new Color(0.18f, 0.48f, 0.20f, 0.9f));
                Tint(p, "Decor", new Color(0.90f, 0.90f, 0.85f, 0.9f));
                Tint(p, "Cannon", new Color(0.25f, 0.28f, 0.25f));
                break;
            case MountainPark:
                p.themeDescription = "山上公园：石阶、松树、山岩";
                p.backgroundColor = new Color(0.78f, 0.84f, 0.90f);
                p.groundColor = new Color(0.45f, 0.42f, 0.40f);
                p.wallColor = new Color(0.33f, 0.31f, 0.30f);
                p.platformColor = new Color(0.52f, 0.40f, 0.28f);
                Tint(p, "Crate", new Color(0.50f, 0.46f, 0.42f));
                Tint(p, "Bush", new Color(0.12f, 0.36f, 0.22f, 0.9f));
                Tint(p, "Decor", new Color(0.75f, 0.80f, 0.70f, 0.9f));
                Tint(p, "Cannon", new Color(0.22f, 0.22f, 0.24f));
                break;
        }
        return p;
    }

    private static void Tint(LevelThemeProfile p, string key, Color color)
    {
        foreach (var m in p.elementSprites)
            if (m != null && m.elementKey == key) { m.useCustomColor = true; m.customColor = color; return; }
    }
}
