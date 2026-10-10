using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S245：素材包——"名字 → 一张 Sprite（+ 可选动画帧）+ 可选互动"。放在 Assets/Resources/ArtKit/MyArtKit.asset（菜单 MarioTrickster → 美术 Art → 🎨 素材包 换图 + 互动 会自动建）。
/// 游戏里所有像素图都先查这里（Step1ArtSkin.Get），所以换一套画风 = 在素材包里把图拖进对应名字的格子（或者把 PNG 按名字丢进一个文件夹点"批量导入"）。
/// 用户写的内容：这个资产永远不进补丁（和 MyTownStories.json 一样）。纯逻辑在 ArtKitRules。
/// </summary>
[CreateAssetMenu(fileName = "MyArtKit", menuName = "MarioTrickster/素材包 Art Kit")]
public class ArtKitSO : ScriptableObject
{
    public const string ResourcePath = "ArtKit/MyArtKit";

    [System.Serializable]
    public class Entry
    {
        [Tooltip("名字（和内置图同名，例如 PoisonPool、TTree、DecoPainting、ArtSlot1）")] public string key = "";
        [Tooltip("换上的图（像素图建议 16×16 或 32×32；Filter Mode = Point）")] public Sprite sprite;
        [Tooltip("可选：动画帧（填了就按 fps 循环播放，第一帧 = 上面的 sprite 也可以不填）")] public Sprite[] frames;
        [Tooltip("动画每秒几帧")] [Range(1f, 24f)] public float fps = 6f;
        [Tooltip("素材槽 z Z a r 在关卡工坊里显示的名字（可不填）")] public string displayName = "";
        [Tooltip("进入范围的互动（掉半格 / 一格心、回血、减速、晕）。不勾 enabled = 只换长相")] public ArtKitRules.Behavior behavior = ArtKitRules.Behavior.None;
    }

    public List<Entry> entries = new List<Entry>();

    private Dictionary<string, Entry> map;
    public Entry Find(string key)
    {
        if (map == null || map.Count != entries.Count) { map = new Dictionary<string, Entry>(); foreach (var e in entries) if (e != null && !string.IsNullOrEmpty(e.key)) map[e.key] = e; }
        return key != null && map.TryGetValue(key, out var r) ? r : null;
    }
    public void Invalidate() { map = null; }

    private void OnValidate() { map = null; ArtKit.Reload(); }
}

/// <summary>S245：素材包的全局入口（运行时 / 编辑器都能用）。没有素材包资产 = 全部用内置图 + 素材槽默认互动。</summary>
public static class ArtKit
{
    private static ArtKitSO kit; private static bool loaded;
    public static ArtKitSO Current { get { if (!loaded) { loaded = true; kit = Resources.Load<ArtKitSO>(ArtKitSO.ResourcePath); } return kit; } }
    public static void Reload() { loaded = false; kit = null; Step1ArtSkin.ClearCache(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { loaded = false; kit = null; }

    public static Sprite SpriteOf(string key) { var e = Current != null ? Current.Find(key) : null; return e != null ? e.sprite : null; }
    public static Sprite[] FramesOf(string key, out float fps) { var e = Current != null ? Current.Find(key) : null; fps = e != null ? e.fps : 6f; return e != null && e.frames != null && e.frames.Length > 0 ? e.frames : null; }

    /// <summary>这个名字的互动：素材包里配了就用素材包的；素材槽没配 = 默认（毒池 / 荆棘 / 回血泉 / 蛛网）；其他 = 没有互动。</summary>
    public static ArtKitRules.Behavior BehaviorOf(string key)
    {
        var e = Current != null ? Current.Find(key) : null;
        int slot = ArtKitRules.SlotIndex(key);
        if (e != null) return e.behavior.enabled ? ArtKitRules.Clamp(e.behavior) : ArtKitRules.Behavior.None; // 素材包里有这一行 = 完全听它的（取消勾选 = 关掉互动）
        if (slot >= 0) return ArtKitRules.SlotDefault(slot); // 素材槽没配过 = 默认互动
        return ArtKitRules.Behavior.None;
    }

    public static string SlotName(int i) { var e = Current != null ? Current.Find(ArtKitRules.SlotKey(i)) : null; return e != null && !string.IsNullOrEmpty(e.displayName) ? e.displayName : ArtKitRules.SlotDefaultName[i]; }
}
