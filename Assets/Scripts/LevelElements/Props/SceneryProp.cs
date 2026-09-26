using UnityEngine;

/// <summary>
/// S187：场景摆件（不可操控，只做"场景阻挡 / 装饰"）。ASCII：'c' 箱子（实心、挡路挡视线）、'b' 草丛（可穿过、挡视线）、'd' 装饰（无碰撞效果）。
/// 设计目的：
///   - 摆放方便：直接在 ASCII 模板里写一个字符，或在 Test Console → Level Builder 的 Scenery 行用笔刷刷；
///   - 换素材方便：名称前缀就是主题键（Crate / Bush / Decor），在 LevelThemeProfile 的 elementSprites 里拖 Sprite 即可，
///     游乐园 / 公园 / 山上公园等主题只换图不改逻辑；
///   - 逻辑最小：本组件只登记到 LevelElementRegistry，便于查询/统计；挡视线由 SightBlocker 标记决定。
/// </summary>
public class SceneryProp : LevelElementBase
{
    private void Awake()
    {
        category = ElementCategory.Misc;
        tags = ElementTag.None;
        if (string.IsNullOrEmpty(elementName) || elementName == "Unknown Element")
        {
            string n = gameObject.name;
            int cut = n.IndexOf('_');
            elementName = cut > 0 ? n.Substring(0, cut) : n;
        }
    }
}
