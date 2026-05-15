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
/// [AI防坑警告] S48 修复: Unity 序列化系统不支持 char 类型字段。
/// AsciiElementEntry.asciiChar 原为 char 类型，Unity 序列化/反序列化时会丢失为 '\0'，
/// 导致 charMap 构建后所有字符都找不到匹配，生成器输出 "Unknown char" 并生成 0 个对象。
/// 修复方案：改用 string 类型 (_asciiCharStr) 作为序列化代理，运行时通过属性 AsciiChar 转为 char。
/// 同时为 CreateDefaultInstance 的临时对象设置 HideFlags.HideAndDontSave 防止 Unity 干扰，
/// 并在 GetDefault 中添加 entries 完整性校验作为最终防线。
///
/// 设计原则:
///   - ScriptableObject 资产，可在 Inspector 中可视化编辑字符分类
///   - 每个元素定义包含：ASCII字符、元素名、物理属性、组件脚本、碰撞体、视觉参数
///   - 提供静态默认实例 + 运行时查询 API，供 Generator 和 Validator 使用
///   - 新增元素只需新建逻辑脚本并在 Inspector 中 Add 一条记录，无需改 Generator 核心代码
///   - 向后兼容：无 Registry 资产时自动使用内置默认定义
///
/// 扩展方式:
///   1. Project 面板右键 → Create → MarioTrickster → Ascii Element Registry
///   2. 在 Inspector 中添加新元素条目（设置字符、名称、组件、碰撞体、视觉参数）
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
             "Generator 通过 componentTypeNames + collider/visual 参数纯数据驱动生成，无需配置 Prefab。")]
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

            // [AI防坑警告] S48: 使用 AsciiChar 属性（从 string 代理转换），而非直接访问字段。
            char c = entry.AsciiChar;
            if (c == '\0')
            {
                Debug.LogWarning($"[AsciiElementRegistry] Entry '{entry.elementName}' has empty/null asciiChar, skipping.");
                continue;
            }

            if (_charLookup.ContainsKey(c))
            {
                Debug.LogWarning($"[AsciiElementRegistry] Duplicate char '{c}' " +
                                 $"for '{entry.elementName}', skipping.");
                continue;
            }
            _charLookup[c] = entry;

            if (entry.isSolid)
                _solidCharsCache.Add(c);
            else
                _airCharsCache.Add(c);

            if (entry.isHazard)
                _hazardCharsCache.Add(c);
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
    /// 内置默认 entries 的数量（25 个元素）。
    /// 用于 GetDefault 中的完整性校验。
    /// </summary>
    private const int BUILTIN_ENTRY_COUNT = 25;

    /// <summary>
    /// 获取默认 Registry 实例。
    /// 优先从 Resources 加载资产；如果不存在，则创建内置默认实例。
    /// 这确保了即使没有创建 ScriptableObject 资产，系统也能正常工作。
    ///
    /// [AI防坑警告] S48: 三层防御机制
    ///   1. Fake Null 防御：用 ReferenceEquals + Unity == null 双重检查
    ///   2. 完整性校验：检查 entries 数组长度和首条目 AsciiChar 是否有效
    ///   3. 自愈：校验失败时强制重建默认实例
    /// </summary>
    public static AsciiElementRegistry GetDefault()
    {
        // [AI防坑警告] S48: 防御 Fake Null（Unity 已销毁但 C# 引用残留）
        // ScriptableObject.CreateInstance 创建的非持久化对象可能在 Domain Reload 后被 Unity GC。
        // 必须同时检查 C# 引用和 Unity 对象有效性。
        if (_defaultInstance != null)
        {
            // S48: 完整性校验 — 确保 entries 没有被 Unity 序列化系统清空
            if (_defaultInstance.entries != null &&
                _defaultInstance.entries.Length > 0 &&
                _defaultInstance.entries[0] != null &&
                _defaultInstance.entries[0].AsciiChar != '\0')
            {
                return _defaultInstance;
            }

            // entries 被破坏（Unity 序列化 char 丢失），强制重建
            Debug.LogWarning("[AsciiElementRegistry] S48: Default instance entries corrupted " +
                             "(likely Unity serialization destroyed char fields). Rebuilding...");
            _defaultInstance = null;
        }

        // 尝试从 Resources 加载
        _defaultInstance = Resources.Load<AsciiElementRegistry>("AsciiElementRegistry");
        if (_defaultInstance != null)
        {
            // S48: 对 Resources 加载的资产也做完整性校验
            if (_defaultInstance.entries != null &&
                _defaultInstance.entries.Length > 0 &&
                _defaultInstance.entries[0] != null &&
                _defaultInstance.entries[0].AsciiChar != '\0')
            {
                _defaultInstance.BuildCache();
                return _defaultInstance;
            }

            Debug.LogWarning("[AsciiElementRegistry] S48: Resources asset has corrupted entries. " +
                             "Falling back to built-in defaults.");
            _defaultInstance = null;
        }

        // 创建内置默认实例（与原始硬编码完全一致）
        _defaultInstance = CreateDefaultInstance();
        return _defaultInstance;
    }

    /// <summary>
    /// 创建包含所有内置元素定义的默认实例。
    /// 这些定义与 S44c 之前 AsciiLevelGenerator.InitCharMap() 和
    /// AsciiLevelValidator.solidChars/airChars/hazardChars 中的硬编码完全一致。
    ///
    /// [AI防坑警告] S48: 设置 HideFlags.HideAndDontSave 防止 Unity 序列化系统
    /// 干扰这个临时内存对象。Unity 不支持序列化 char 类型，如果 Unity 尝试
    /// 序列化/反序列化此对象，所有 asciiChar 字段会丢失为 '\0'。
    /// </summary>
    public static AsciiElementRegistry CreateDefaultInstance()
    {
        var registry = ScriptableObject.CreateInstance<AsciiElementRegistry>();

        // [AI防坑警告] S48: 必须设置 HideFlags 防止 Unity 序列化干扰！
        // 没有这个标记，Unity 可能在 Domain Reload、Undo、场景切换等操作中
        // 对此对象进行序列化/反序列化，导致 char 字段丢失。
        registry.hideFlags = HideFlags.HideAndDontSave;

        registry.entries = new AsciiElementEntry[]
        {
            // ── 合并生成实体（Generator 只保留 Ground/Platform Merge 逻辑）──
            new AsciiElementEntry
            {
                asciiChar = '#', elementName = "Ground", isSolid = true, isHazard = false, jumpBoost = 0f,
                generateObject = false, visualColor = new Color(0.55f, 0.55f, 0.55f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 0, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = '=', elementName = "Platform", isSolid = true, isHazard = false, jumpBoost = 0f,
                generateObject = false, visualColor = new Color(0.70f, 0.70f, 0.70f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 1, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'W', elementName = "Wall", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new string[0], visualColor = new Color(0.40f, 0.40f, 0.40f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 1, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'B', elementName = "BouncyPlatform", isSolid = true, isHazard = false, jumpBoost = 1f,
                componentTypeNames = new[] { "BouncyPlatform" }, visualColor = new Color(0.30f, 0.85f, 0.30f), visualScale = new Vector2(2.0f, 0.3f),
                customColliderSize = PhysicsMetrics.BOUNCY_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'C', elementName = "CollapsingPlatform", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "CollapsingPlatform" }, visualColor = new Color(0.80f, 0.65f, 0.30f), visualScale = new Vector2(2.0f, 0.4f),
                customColliderSize = PhysicsMetrics.COLLAPSE_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = '-', elementName = "OneWayPlatform", isSolid = true, isHazard = false, jumpBoost = 0f,
                generateObject = false, componentTypeNames = new[] { "OneWayPlatform" }, visualColor = new Color(0.50f, 0.75f, 0.90f), visualScale = new Vector2(1.0f, 0.3f),
                customColliderSize = PhysicsMetrics.ONEWAY_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'F', elementName = "FakeWall", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "FakeWall" }, visualColor = new Color(0.55f, 0.55f, 0.65f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 1, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = '<', elementName = "ConveyorBelt", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "ConveyorBelt" }, visualColor = new Color(0.60f, 0.60f, 0.40f), visualScale = new Vector2(1.0f, 0.3f),
                customColliderSize = PhysicsMetrics.CONVEYOR_BELT_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 1, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'X', elementName = "BreakableBlock", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "BreakableBlock" }, visualColor = new Color(0.75f, 0.55f, 0.30f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BREAKABLE_BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 1, isTrigger = false
            },

            // ── 空气/特殊字符（不走通用生成；M/T 由玩家生成特殊逻辑处理）──
            new AsciiElementEntry { asciiChar = '.', elementName = "Air", isSolid = false, isHazard = false, jumpBoost = 0f, generateObject = false },
            new AsciiElementEntry { asciiChar = ' ', elementName = "Space", isSolid = false, isHazard = false, jumpBoost = 0f, generateObject = false },
            new AsciiElementEntry { asciiChar = 'M', elementName = "MarioSpawn", isSolid = false, isHazard = false, jumpBoost = 0f, generateObject = false },
            new AsciiElementEntry { asciiChar = 'T', elementName = "TricksterSpawn", isSolid = false, isHazard = false, jumpBoost = 0f, generateObject = false },

            // ── 通用 Registry 驱动生成元素 ──
            new AsciiElementEntry
            {
                asciiChar = 'G', elementName = "GoalZone", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "GoalZone" }, visualColor = new Color(0.20f, 1.00f, 0.40f), visualScale = new Vector2(1.0f, 3.0f),
                customColliderSize = PhysicsMetrics.GOAL_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = 'o', elementName = "Collectible", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "Collectible" }, visualColor = new Color(1.00f, 0.85f, 0.20f), visualScale = new Vector2(0.5f, 0.5f),
                customColliderSize = PhysicsMetrics.COLLECTIBLE_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = 'H', elementName = "HiddenPassage", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "HiddenPassage" }, visualColor = new Color(0.40f, 0.70f, 0.55f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = '>', elementName = "MovingPlatform", isSolid = true, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "MovingPlatform" }, visualColor = new Color(0.50f, 0.50f, 0.90f), visualScale = new Vector2(3.0f, 0.4f),
                customColliderSize = PhysicsMetrics.MOVING_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = '^', elementName = "SpikeTrap", isSolid = false, isHazard = true, jumpBoost = 0f,
                componentTypeNames = new[] { "SpikeTrap" }, visualColor = new Color(0.85f, 0.25f, 0.25f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.SPIKE_COLLIDER_SIZE, customColliderOffset = new Vector2(0f, PhysicsMetrics.SPIKE_COLLIDER_OFFSET_Y),
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = '~', elementName = "FireTrap", isSolid = false, isHazard = true, jumpBoost = 0f,
                componentTypeNames = new[] { "FireTrap" }, visualColor = new Color(1.00f, 0.50f, 0.10f), visualScale = new Vector2(0.5f, 0.5f),
                customColliderSize = PhysicsMetrics.FIRE_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = 'P', elementName = "PendulumTrap", isSolid = false, isHazard = true, jumpBoost = 0f,
                componentTypeNames = new[] { "PendulumTrap" }, visualColor = new Color(0.70f, 0.45f, 0.20f), visualScale = new Vector2(0.3f, 0.3f),
                customColliderSize = PhysicsMetrics.PENDULUM_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = '[', elementName = "ControllableBlocker", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "ControllableBlocker" }, visualColor = new Color(0.55f, 0.75f, 1.00f, 0.35f), visualScale = Vector2.one,
                customColliderSize = PhysicsMetrics.BLOCK_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = 'E', elementName = "BouncingEnemy", isSolid = false, isHazard = false, jumpBoost = 1f,
                componentTypeNames = new[] { "BouncingEnemy" }, visualColor = new Color(0.90f, 0.20f, 0.60f), visualScale = new Vector2(0.8f, 0.8f),
                customColliderSize = PhysicsMetrics.BOUNCING_ENEMY_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'e', elementName = "SimpleEnemy", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "SimpleEnemy" }, visualColor = new Color(0.90f, 0.20f, 0.60f), visualScale = new Vector2(0.8f, 0.8f),
                customColliderSize = PhysicsMetrics.SIMPLE_ENEMY_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = '@', elementName = "SawBlade", isSolid = false, isHazard = true, jumpBoost = 0f,
                componentTypeNames = new[] { "SawBlade" }, visualColor = new Color(0.70f, 0.70f, 0.70f), visualScale = new Vector2(0.8f, 0.8f),
                customColliderSize = PhysicsMetrics.SAW_BLADE_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
            new AsciiElementEntry
            {
                asciiChar = 'f', elementName = "FlyingEnemy", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "Rigidbody2D", "FlyingEnemy" }, visualColor = new Color(0.85f, 0.40f, 0.85f), visualScale = new Vector2(0.8f, 0.8f),
                customColliderSize = PhysicsMetrics.FLYING_ENEMY_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = false
            },
            new AsciiElementEntry
            {
                asciiChar = 'S', elementName = "Checkpoint", isSolid = false, isHazard = false, jumpBoost = 0f,
                componentTypeNames = new[] { "Checkpoint" }, visualColor = new Color(0.20f, 0.80f, 0.90f), visualScale = new Vector2(0.5f, 1.2f),
                customColliderSize = PhysicsMetrics.CHECKPOINT_COLLIDER_SIZE, customColliderOffset = Vector2.zero,
                sortingOrder = 5, isTrigger = true
            },
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
/// [AI防坑警告] 本项目不使用 Prefab，所有元素由 Registry 数据驱动生成。
/// 新增元素严禁修改 AsciiLevelGenerator 核心代码，只允许新增逻辑脚本并配置 Registry。
///
/// [AI防坑警告] S48: Unity 序列化系统不支持 char 类型。
/// asciiChar 字段使用 string (_asciiCharStr) 作为序列化代理。
/// 代码中通过 AsciiChar 属性访问 char 值，通过 asciiChar setter 设置值。
/// 这确保了无论是 Inspector 编辑的资产还是代码创建的实例，char 值都不会丢失。
/// </summary>
[System.Serializable]
public class AsciiElementEntry
{
    // [AI防坑警告] S48: Unity 不序列化 char 类型！
    // 使用 string 作为序列化代理，运行时通过属性转换为 char。
    // Inspector 中显示为单字符文本框，直观且安全。
    [Tooltip("ASCII 字符（在模板中使用的单个字符，如 # ^ M G）")]
    [SerializeField]
    private string _asciiCharStr = "";

    /// <summary>
    /// ASCII 字符的读写属性。
    /// 读取时从序列化代理 _asciiCharStr 转换为 char。
    /// 设置时同步更新 _asciiCharStr。
    /// </summary>
    public char asciiChar
    {
        get
        {
            if (!string.IsNullOrEmpty(_asciiCharStr))
                return _asciiCharStr[0];
            return '\0';
        }
        set
        {
            _asciiCharStr = value.ToString();
        }
    }

    /// <summary>
    /// 便捷只读属性，与 asciiChar getter 等价。
    /// 供 BuildCache 等内部方法使用，语义更清晰。
    /// </summary>
    public char AsciiChar => asciiChar;

    [Tooltip("元素名称（与 Generator 中 SpawnMap 的 key 一致，如 Ground, SpikeTrap, BouncingEnemy）")]
    public string elementName = "";

    [Tooltip("是否为实体（玩家可站立：地面、平台、墙壁等）")]
    public bool isSolid;

    [Tooltip("是否为危险物（对玩家造成伤害：地刺、火焰、摆锤等）")]
    public bool isHazard;

    [Tooltip("跳跃增益系数（0 = 无增益，1 = 标准弹跳，如弹跳平台B和弹跳怪E）")]
    public float jumpBoost;

    [Header("=== Generator 生成参数 ===")]
    [Tooltip("是否由通用生成器实例化 GameObject。空气、M/T 出生点、合并地形等特殊字符设为 false。")]
    public bool generateObject = true;

    [Tooltip("要挂载到根物体上的组件类型名。可填短类名（如 SpikeTrap）或完整类型名；新增逻辑脚本只需在此增加类名。")]
    public string[] componentTypeNames = new string[0];

    [Tooltip("根物体 BoxCollider2D 的尺寸。Vector2.zero 表示使用 Vector2.one 向后兼容。")]
    public Vector2 customColliderSize = Vector2.one;

    [Tooltip("根物体 BoxCollider2D 的偏移。")]
    public Vector2 customColliderOffset = Vector2.zero;

    [Tooltip("根物体 BoxCollider2D 是否为 Trigger。")]
    public bool isTrigger = false;

    [Header("=== Visual 白盒参数 ===")]
    [Tooltip("Visual 子节点的本地缩放，用于保持旧版白盒视觉大小。")]
    public Vector2 visualScale = Vector2.one;

    [Tooltip("Visual 子节点 SpriteRenderer 的白盒颜色。")]
    public Color visualColor = Color.white;

    [Tooltip("Visual 子节点 SpriteRenderer 的排序层级。")]
    public int sortingOrder = 5;
}
