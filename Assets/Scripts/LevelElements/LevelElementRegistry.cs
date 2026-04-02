using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 关卡元素注册中心 - 低耦合框架的核心枢纽
/// 
/// 设计理念:
///   - 静态注册表，所有关卡元素自动注册/注销
///   - 支持两种注册路径:
///     1. LevelElementBase 子类 → OnEnable 时自动注册
///     2. ControllableLevelElement 子类 → 通过 RegisterControllable 注册
///   - 删除任何元素脚本不会导致编译错误或运行时异常
///   - 提供按分类、标签、距离等多维度查询接口
///   - 关卡重置时批量通知所有已注册元素
/// 
/// 扩展指南（给未来AI/开发者）:
///   新增关卡元素时，只需让脚本继承 LevelElementBase 或 ControllableLevelElement，
///   Registry 会自动发现并管理，无需修改本文件。
/// 
/// Session 15: 关卡设计系统框架新增
/// </summary>
public static class LevelElementRegistry
{
    // ── 数据结构 ─────────────────────────────────────────

    /// <summary>统一的元素记录（同时支持两种注册路径）</summary>
    public class ElementRecord
    {
        public int InstanceId;
        public string Name;
        public ElementCategory Category;
        public ElementTag Tags;
        public string Description;
        public MonoBehaviour Component;      // 实际的MonoBehaviour组件
        public LevelElementBase AsBase;      // 如果是LevelElementBase子类则非null
        public ControllableLevelElement AsControllable; // 如果是ControllableLevelElement子类则非null

        public Transform Transform => Component != null ? Component.transform : null;
        public bool HasTag(ElementTag tag) => (Tags & tag) != 0;
    }

    // 核心存储
    private static readonly Dictionary<int, ElementRecord> _records = new Dictionary<int, ElementRecord>();
    private static readonly Dictionary<ElementCategory, HashSet<int>> _categoryIndex = new Dictionary<ElementCategory, HashSet<int>>();

    // ── 事件 ─────────────────────────────────────────────
    public static event System.Action<ElementRecord> OnElementRegistered;
    public static event System.Action<ElementRecord> OnElementUnregistered;

    // ── 统计 ─────────────────────────────────────────────
    public static int TotalCount => _records.Count;

    // ══════════════════════════════════════════════════════
    // 注册路径 1: LevelElementBase 子类
    // ══════════════════════════════════════════════════════

    public static void Register(LevelElementBase element)
    {
        if (element == null) return;
        int id = element.GetInstanceID();
        if (_records.ContainsKey(id)) return;

        var record = new ElementRecord
        {
            InstanceId = id,
            Name = element.ElementName,
            Category = element.Category,
            Tags = element.Tags,
            Description = element.Description,
            Component = element,
            AsBase = element,
            AsControllable = null
        };

        AddRecord(id, record);
    }

    public static void Unregister(LevelElementBase element)
    {
        if (element == null) return;
        RemoveRecord(element.GetInstanceID());
    }

    // ══════════════════════════════════════════════════════
    // 注册路径 2: ControllableLevelElement 子类
    // ══════════════════════════════════════════════════════

    public static void RegisterControllable(ControllableLevelElement element, string name,
        ElementCategory category, ElementTag tags, string description)
    {
        if (element == null) return;
        int id = element.GetInstanceID();
        if (_records.ContainsKey(id)) return;

        var record = new ElementRecord
        {
            InstanceId = id,
            Name = name,
            Category = category,
            Tags = tags,
            Description = description,
            Component = element,
            AsBase = null,
            AsControllable = element
        };

        AddRecord(id, record);
    }

    public static void UnregisterControllable(ControllableLevelElement element)
    {
        if (element == null) return;
        RemoveRecord(element.GetInstanceID());
    }

    // ══════════════════════════════════════════════════════
    // 内部注册/注销
    // ══════════════════════════════════════════════════════

    private static void AddRecord(int id, ElementRecord record)
    {
        _records[id] = record;

        if (!_categoryIndex.ContainsKey(record.Category))
            _categoryIndex[record.Category] = new HashSet<int>();
        _categoryIndex[record.Category].Add(id);

        OnElementRegistered?.Invoke(record);
        Debug.Log($"[Registry] + [{record.Category}] {record.Name} (总计:{_records.Count})");
    }

    private static void RemoveRecord(int id)
    {
        if (!_records.TryGetValue(id, out ElementRecord record)) return;

        _records.Remove(id);
        if (_categoryIndex.ContainsKey(record.Category))
        {
            _categoryIndex[record.Category].Remove(id);
            if (_categoryIndex[record.Category].Count == 0)
                _categoryIndex.Remove(record.Category);
        }

        OnElementUnregistered?.Invoke(record);
        Debug.Log($"[Registry] - [{record.Category}] {record.Name} (剩余:{_records.Count})");
    }

    // ══════════════════════════════════════════════════════
    // 查询接口
    // ══════════════════════════════════════════════════════

    /// <summary>获取所有已注册元素记录</summary>
    public static IReadOnlyCollection<ElementRecord> GetAll() => _records.Values;

    /// <summary>按分类获取</summary>
    public static List<ElementRecord> GetByCategory(ElementCategory category)
    {
        var result = new List<ElementRecord>();
        if (!_categoryIndex.ContainsKey(category)) return result;
        foreach (int id in _categoryIndex[category])
        {
            if (_records.TryGetValue(id, out ElementRecord rec) && rec.Component != null)
                result.Add(rec);
        }
        return result;
    }

    /// <summary>按标签获取</summary>
    public static List<ElementRecord> GetByTag(ElementTag tag)
    {
        var result = new List<ElementRecord>();
        foreach (var rec in _records.Values)
        {
            if (rec.Component != null && rec.HasTag(tag))
                result.Add(rec);
        }
        return result;
    }

    /// <summary>按分类+标签组合查询</summary>
    public static List<ElementRecord> GetByCategoryAndTag(ElementCategory category, ElementTag tag)
    {
        var result = new List<ElementRecord>();
        if (!_categoryIndex.ContainsKey(category)) return result;
        foreach (int id in _categoryIndex[category])
        {
            if (_records.TryGetValue(id, out ElementRecord rec) && rec.Component != null && rec.HasTag(tag))
                result.Add(rec);
        }
        return result;
    }

    /// <summary>获取指定位置附近的元素</summary>
    public static List<ElementRecord> GetNearby(Vector3 position, float radius)
    {
        var result = new List<ElementRecord>();
        float radiusSqr = radius * radius;
        foreach (var rec in _records.Values)
        {
            if (rec.Transform != null)
            {
                if ((rec.Transform.position - position).sqrMagnitude <= radiusSqr)
                    result.Add(rec);
            }
        }
        return result;
    }

    /// <summary>获取所有可操控元素</summary>
    public static List<ControllableLevelElement> GetAllControllable()
    {
        var result = new List<ControllableLevelElement>();
        foreach (var rec in _records.Values)
        {
            if (rec.AsControllable != null)
                result.Add(rec.AsControllable);
        }
        return result;
    }

    /// <summary>获取各分类的元素数量统计</summary>
    public static Dictionary<ElementCategory, int> GetCategoryStats()
    {
        var stats = new Dictionary<ElementCategory, int>();
        foreach (var kvp in _categoryIndex)
            stats[kvp.Key] = kvp.Value.Count;
        return stats;
    }

    // ══════════════════════════════════════════════════════
    // 批量操作
    // ══════════════════════════════════════════════════════

    /// <summary>关卡重置：通知所有已注册元素恢复初始状态</summary>
    public static void ResetAll()
    {
        foreach (var rec in _records.Values)
        {
            if (rec.AsBase != null) rec.AsBase.OnLevelReset();
            else if (rec.AsControllable != null) rec.AsControllable.OnLevelReset();
        }
        Debug.Log($"[Registry] 全部重置，共 {_records.Count} 个元素");
    }

    /// <summary>清空注册表（场景切换时调用）</summary>
    public static void Clear()
    {
        _records.Clear();
        _categoryIndex.Clear();
        Debug.Log("[Registry] 注册表已清空");
    }

    /// <summary>输出注册表摘要</summary>
    public static void DebugPrintSummary()
    {
        string s = $"[Registry] === 摘要 (共 {_records.Count}) ===\n";
        foreach (var kvp in _categoryIndex)
            s += $"  {kvp.Key}: {kvp.Value.Count}\n";
        Debug.Log(s);
    }
}
