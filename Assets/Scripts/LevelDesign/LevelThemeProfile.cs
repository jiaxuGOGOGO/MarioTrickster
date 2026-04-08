using UnityEngine;

/// <summary>
/// 关卡主题配置文件 — 数据驱动的换肤系统
/// 
/// 使用方式:
///   1. 在 Project 面板右键 → Create → MarioTrickster → Level Theme Profile
///   2. 在 Inspector 中拖入各插槽的 Sprite（留空 = 保留白盒原样）
///   3. 在 Test Console → Level Builder Tab 中拖入此 Profile，点击 "Apply Theme"
///
/// 设计原则:
///   - 所有 Sprite 插槽允许为 null（Fallback 安全：空插槽保留白盒材质，绝不抛 NRE）
///   - 通过 ElementCategory 枚举与 LevelElementRegistry 解耦
///   - 新增陷阱/平台类型时，只需在 elementSprites 数组中 Add 一条映射即可
///   - 不强耦合任何具体的陷阱类，换肤逻辑通过 SpriteRenderer 通用替换
///
/// Session 25: 关卡工坊新增
/// </summary>
[CreateAssetMenu(fileName = "NewLevelTheme", menuName = "MarioTrickster/Level Theme Profile", order = 100)]
public class LevelThemeProfile : ScriptableObject
{
    [Header("=== 主题基本信息 ===")]
    [Tooltip("主题名称（显示在编辑器面板中）")]
    public string themeName = "New Theme";

    [Tooltip("主题描述")]
    [TextArea(2, 4)]
    public string themeDescription = "";

    [Header("=== 背景 ===")]
    [Tooltip("背景颜色（应用到 Camera 的 Background Color）")]
    public Color backgroundColor = new Color(0.53f, 0.81f, 0.92f); // 天蓝色默认

    [Header("=== 地形 Tile 映射 ===")]
    [Tooltip("地面方块的 Sprite（null = 保留白盒）")]
    public Sprite groundSprite;

    [Tooltip("地面方块的颜色覆盖（仅在 groundSprite 为 null 时使用）")]
    public Color groundColor = new Color(0.4f, 0.3f, 0.2f);

    [Tooltip("平台方块的 Sprite（null = 保留白盒）")]
    public Sprite platformSprite;

    [Tooltip("平台方块的颜色覆盖")]
    public Color platformColor = new Color(0.3f, 0.6f, 0.8f);

    [Tooltip("墙壁方块的 Sprite（null = 保留白盒）")]
    public Sprite wallSprite;

    [Tooltip("墙壁方块的颜色覆盖")]
    public Color wallColor = new Color(0.35f, 0.25f, 0.15f);

    [Header("=== 动态关卡元素 Sprite 映射 ===")]
    [Tooltip("按名称键映射的元素 Sprite 列表。\n" +
             "键名约定: SpikeTrap, FireTrap, PendulumTrap, BouncingEnemy,\n" +
             "BouncyPlatform, CollapsingPlatform, OneWayPlatform, MovingPlatform,\n" +
             "HiddenPassage, FakeWall, GoalZone, Collectible, SimpleEnemy\n" +
             "留空 Sprite = 保留白盒原样")]
    public ElementSpriteMapping[] elementSprites = new ElementSpriteMapping[]
    {
        new ElementSpriteMapping { elementKey = "SpikeTrap" },
        new ElementSpriteMapping { elementKey = "FireTrap" },
        new ElementSpriteMapping { elementKey = "PendulumTrap" },
        new ElementSpriteMapping { elementKey = "BouncingEnemy" },
        new ElementSpriteMapping { elementKey = "BouncyPlatform" },
        new ElementSpriteMapping { elementKey = "CollapsingPlatform" },
        new ElementSpriteMapping { elementKey = "OneWayPlatform" },
        new ElementSpriteMapping { elementKey = "MovingPlatform" },
        new ElementSpriteMapping { elementKey = "HiddenPassage" },
        new ElementSpriteMapping { elementKey = "FakeWall" },
        new ElementSpriteMapping { elementKey = "GoalZone" },
        new ElementSpriteMapping { elementKey = "Collectible" },
        new ElementSpriteMapping { elementKey = "SimpleEnemy" },
        // S56 新增元素
        new ElementSpriteMapping { elementKey = "SawBlade" },
        new ElementSpriteMapping { elementKey = "FlyingEnemy" },
        new ElementSpriteMapping { elementKey = "ConveyorBelt" },
        new ElementSpriteMapping { elementKey = "Checkpoint" },
        new ElementSpriteMapping { elementKey = "BreakableBlock" },
    };

    [Header("=== 角色 ===")]
    [Tooltip("Mario 的 Sprite（null = 保留白盒）")]
    public Sprite marioSprite;

    [Tooltip("Mario 的颜色覆盖")]
    public Color marioColor = new Color(0.9f, 0.2f, 0.2f);

    [Tooltip("Trickster 的 Sprite（null = 保留白盒）")]
    public Sprite tricksterSprite;

    [Tooltip("Trickster 的颜色覆盖")]
    public Color tricksterColor = new Color(0.5f, 0.2f, 0.8f);

    // ═══════════════════════════════════════════════════
    // 查询接口
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 根据元素键名获取对应的 Sprite（null 安全）
    /// </summary>
    public Sprite GetElementSprite(string elementKey)
    {
        if (elementSprites == null) return null;
        for (int i = 0; i < elementSprites.Length; i++)
        {
            if (elementSprites[i].elementKey == elementKey)
                return elementSprites[i].sprite;
        }
        return null;
    }

    /// <summary>
    /// 根据元素键名获取对应的颜色覆盖（如果有自定义颜色则返回，否则返回 null）
    /// </summary>
    public Color? GetElementColor(string elementKey)
    {
        if (elementSprites == null) return null;
        for (int i = 0; i < elementSprites.Length; i++)
        {
            if (elementSprites[i].elementKey == elementKey && elementSprites[i].useCustomColor)
                return elementSprites[i].customColor;
        }
        return null;
    }
}

/// <summary>
/// 元素 Sprite 映射条目
/// 通过 elementKey 字符串与具体元素类型解耦
/// </summary>
[System.Serializable]
public class ElementSpriteMapping
{
    [Tooltip("元素键名（与 GameObject 名称或类型名匹配）")]
    public string elementKey = "";

    [Tooltip("替换用的 Sprite（null = 保留白盒原样）")]
    public Sprite sprite;

    [Tooltip("是否使用自定义颜色覆盖")]
    public bool useCustomColor = false;

    [Tooltip("自定义颜色（仅在 useCustomColor 为 true 时生效）")]
    public Color customColor = Color.white;
}
