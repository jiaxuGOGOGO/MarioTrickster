using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ASCII 元素注册表 — Data-Driven Registry (Session 46)
///
/// 中心化管理 ASCII 字符到关卡元素的映射关系。
/// 取代 AsciiLevelGenerator.InitCharMap() 中的硬编码字典
/// 和 AsciiLevelValidator 中的硬编码 solidChars/airChars/hazardChars。
///
/// [AI防坑警告] 本项目采用纯白盒生成模式，所有关卡元素由 AsciiLevelGenerator 的
/// Spawn 方法用 new GameObject + SpriteRenderer + BoxCollider2D 动态创建，
/// 不依赖任何 Prefab。Registry 的职责是提供字符分类元数据（IsSolid/IsHazard/JumpBoost），
/// 不是 Prefab 仓库。Inspector 中无需拖入任何 Prefab。
///
/// 设计原则:
///   - ScriptableObject 资产，可在 Inspector 中可视化编辑字符分类
///   - 每个元素定义包含：ASCII字符、元素名、物理属性标记（IsSolid/IsHazard/JumpBoost）
///   - 提供静态默认实例 + 运行时查询 API，供 Generator 和 Validator 使用
///   - 新增元素只需在 Inspector 中 Add 一条记录，无需改代码
///   - 向后兼容：无 Registry 资产时自动使用内置默认定义
///
/// 扩展方式:
///   1. Project 面板右键 → Create → MarioTrickster → Ascii Element Registry
///   2. 在 Inspector 中添加新元素条目（设置字符、名称、IsSolid/IsHazard/JumpBoost）
///   3. AsciiLevelGenerator 和 AsciiLevelValidator 自动识别新字符
/// </summary>
[CreateAssetMenu(fileName = "AsciiElementRegistry", menuName = "MarioTrickster/Ascii Element Registry", order = 101)]
public class AsciiElementRegistry : ScriptableObject
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    [Header("=== ASCII 元素定义列表 ===")]
    [Tooltip("所有 ASCII 字符到关卡元素的映射。\n" +
             "新增元素只需 Add 一条记录，Generator 和 Validator 自动识别。\n" +
             "ℹ️ 本项目采用白盒生成模式，无需配置 Prefab。")]
    public AsciiElementEntry[] entries;

    // ═══════════════════════════════════════════════════
    // 运行时缓存（避免每次查询遍历数组）
    // ═══════════════════════════════════════════════════

    private Dictionary<char, AsciiElementEntry> _charLookup;
    private HashSet<char> _solidCharsCache;
    private HashSet<char> _airCharsCache;
    private HashSet<char> _hazardCharsCache;

    /// <summary>构建/重建运行时查询缓存</summary>
    public void BuildCache()
    {
        _charLookup = new Dictionary<char, AsciiElementEntry>();
        _solidCharsCache = new HashSet<char>();
        _airCharsCache = new HashSet<char>();
        _hazardCharsCache = new HashSet<char>();

        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (_charLookup.ContainsKey(entry.asciiChar))
            {
                Debug.LogWarning($"[AsciiElementRegistry] Duplicate char '{entry.asciiChar}' " +
                                 $"for '{entry.elementName}', skipping.");
                continue;
            }
            _charLookup[entry.asciiChar] = entry;

            if (entry.isSolid)
                _solidCharsCache.Add(entry.asciiChar);
            else
                _airCharsCache.Add(entry.asciiChar);

            if (entry.isHazard)
                _hazardCharsCache.Add(entry.asciiChar);
        }
    }

    private void EnsureCache()
    {
        if (_charLookup == null) BuildCache();
    }

    // ═══════════════════════════════════════════════════
    // 查询 API
    // ═══════════════════════════════════════════════════

    /// <summary>根据 ASCII 字符查询元素定义（null = 未注册）</summary>
    public AsciiElementEntry GetEntry(char c)
    {
        EnsureCache();
        _charLookup.TryGetValue(c, out var entry);
        return entry;
    }

    /// <summary>判断字符是否为实体（玩家可站立）</summary>
    public bool IsSolid(char c)
    {
        EnsureCache();
        return _solidCharsCache.Contains(c);
    }

    /// <summary>判断字符是否为空气（玩家可通过）</summary>
    public bool IsAir(char c)
    {
        EnsureCache();
        return _airCharsCache.Contains(c);
    }

    /// <summary>判断字符是否为危险物</summary>
    public bool IsHazard(char c)
    {
        EnsureCache();
        return _hazardCharsCache.Contains(c);
    }

    /// <summary>获取所有已注册字符的集合</summary>
    public HashSet<char> GetAllRegisteredChars()
    {
        EnsureCache();
        return new HashSet<char>(_charLookup.Keys);
    }

    /// <summary>获取所有实体字符的集合（供 Validator 使用）</summary>
    public HashSet<char> GetSolidChars()
    {
        EnsureCache();
        return new HashSet<char>(_solidCharsCache);
    }

    /// <summary>获取所有危险字符的集合（供 Validator 使用）</summary>
    public HashSet<char> GetHazardChars()
    {
        EnsureCache();
        return new HashSet<char>(_hazardCharsCache);
    }

    // ═══════════════════════════════════════════════════
    // 静态默认实例（向后兼容：无资产时使用内置定义）
    // ═══════════════════════════════════════════════════

    private static AsciiElementRegistry _defaultInstance;

    /// <summary>
    /// 获取默认 Registry 实例。
    /// 优先从 Resources 加载资产；如果不存在，则创建内置默认实例。
    /// 这确保了即使没有创建 ScriptableObject 资产，系统也能正常工作。
    /// </summary>
    public static AsciiElementRegistry GetDefault()
    {
        if (_defaultInstance != null) return _defaultInstance;

        // 尝试从 Resources 加载
        _defaultInstance = Resources.Load<AsciiElementRegistry>("AsciiElementRegistry");
        if (_defaultInstance != null)
        {
            _defaultInstance.BuildCache();
            return _defaultInstance;
        }

        // 创建内置默认实例（与原始硬编码完全一致）
        _defaultInstance = CreateDefaultInstance();
        return _defaultInstance;
    }

    /// <summary>
    /// 创建包含所有内置元素定义的默认实例。
    /// 这些定义与 S44c 之前 AsciiLevelGenerator.InitCharMap() 和
    /// AsciiLevelValidator.solidChars/airChars/hazardChars 中的硬编码完全一致。
    /// </summary>
    public static AsciiElementRegistry CreateDefaultInstance()
    {
        var registry = ScriptableObject.CreateInstance<AsciiElementRegistry>();
        registry.entries = new AsciiElementEntry[]
        {
            // ── 地形（Solid）──
            new AsciiElementEntry { asciiChar = '#', elementName = "Ground",     isSolid = true,  isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = '=', elementName = "Platform",   isSolid = true,  isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'W', elementName = "Wall",       isSolid = true,  isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'B', elementName = "BouncyPlatform",    isSolid = true,  isHazard = false, jumpBoost = 1f },
            new AsciiElementEntry { asciiChar = 'C', elementName = "CollapsingPlatform", isSolid = true,  isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = '-', elementName = "OneWayPlatform",     isSolid = true,  isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'F', elementName = "FakeWall",   isSolid = true,  isHazard = false, jumpBoost = 0f },

            // ── 空气/非实体 ──
            new AsciiElementEntry { asciiChar = '.', elementName = "Air",        isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = ' ', elementName = "Space",      isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'M', elementName = "MarioSpawn", isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'T', elementName = "TricksterSpawn", isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'G', elementName = "GoalZone",   isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'o', elementName = "Collectible", isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'H', elementName = "HiddenPassage", isSolid = false, isHazard = false, jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = '>', elementName = "MovingPlatform", isSolid = false, isHazard = false, jumpBoost = 0f },

            // ── 危险物（Hazard，同时也是非实体/空气）──
            new AsciiElementEntry { asciiChar = '^', elementName = "SpikeTrap",  isSolid = false, isHazard = true,  jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = '~', elementName = "FireTrap",   isSolid = false, isHazard = true,  jumpBoost = 0f },
            new AsciiElementEntry { asciiChar = 'P', elementName = "PendulumTrap", isSolid = false, isHazard = true, jumpBoost = 0f },

            // ── 敌人（非实体，非 hazard — 原始 Validator 中敌人在 airChars 而非 hazardChars）──
            new AsciiElementEntry { asciiChar = 'E', elementName = "BouncingEnemy", isSolid = false, isHazard = false, jumpBoost = 1f },
            new AsciiElementEntry { asciiChar = 'e', elementName = "SimpleEnemy",   isSolid = false, isHazard = false, jumpBoost = 0f },
        };
        registry.BuildCache();
        return registry;
    }

    /// <summary>
    /// 重置静态默认实例缓存（用于测试或热重载）。
    /// </summary>
    public static void ResetDefault()
    {
        _defaultInstance = null;
    }
}

/// <summary>
/// ASCII 元素条目 — 单个字符到关卡元素的映射定义
///
/// [AI防坑警告] 本项目不使用 Prefab，所有元素由 AsciiLevelGenerator 的 Spawn 方法
/// 用 new GameObject 白盒生成。此数据结构仅提供字符分类元数据，不要添加 Prefab 字段。
/// </summary>
[System.Serializable]
public class AsciiElementEntry
{
    [Tooltip("ASCII 字符（在模板中使用的单个字符）")]
    public char asciiChar;

    [Tooltip("元素名称（与 Generator 中 SpawnMap 的 key 一致，如 Ground, SpikeTrap, BouncingEnemy）")]
    public string elementName = "";

    [Tooltip("是否为实体（玩家可站立：地面、平台、墙壁等）")]
    public bool isSolid;

    [Tooltip("是否为危险物（对玩家造成伤害：地刺、火焰、摆锤等）")]
    public bool isHazard;

    [Tooltip("跳跃增益系数（0 = 无增益，1 = 标准弹跳，如弹跳平台B和弹跳怪E）")]
    public float jumpBoost;
}
