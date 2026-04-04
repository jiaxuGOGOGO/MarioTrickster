using UnityEngine;

/// <summary>
/// Mario 交互辅助工具 - 处理 Mario 的上下文交互
/// 
/// Session 17 新增:
///   当 Mario 按下 S 键时，按优先级检测并触发交互
///   
/// Session 19 更新（参考行业最佳实践重构）:
///   将交互拆分为两个独立入口，由 InputManager 根据输入组合分别调用：
///   
///   1. HandleDropThrough() — S+Jump 组合键触发
///      检测 Mario 脚下是否有 OneWayPlatform → AllowDropThrough()
///      参考 Super Smash Bros / 行业标准的 Down+Jump 穿越平台做法
///      
///   2. HandlePassageInteraction() — S 键单独触发
///      检测 Mario 是否在 HiddenPassage 触发区或返回触发区内
///      支持双向穿越（入口→出口 和 出口→入口）
///   
///   使用 OverlapBox 检测脚下平台，OverlapCircle 检测周围通道
///   不需要挂载到任何 GameObject，由 InputManager 直接调用
/// </summary>
public static class MarioInteractionHelper
{
    /// <summary>检测半径（单向平台检测范围）</summary>
    private const float PLATFORM_CHECK_DISTANCE = 0.3f;

    /// <summary>隐藏通道检测半径</summary>
    private const float PASSAGE_CHECK_RADIUS = 1.0f;

    /// <summary>
    /// [已废弃] 旧版统一入口，保留向后兼容
    /// Session 19: 建议使用 HandleDropThrough / HandlePassageInteraction 替代
    /// </summary>
    public static void HandleDownInteraction(GameObject marioObj)
    {
        if (marioObj == null) return;

        if (TryDropThroughPlatform(marioObj)) return;
        TryEnterPassage(marioObj);
    }

    /// <summary>
    /// Session 19: 处理单向平台下落（S+Jump 组合键触发）
    /// </summary>
    public static void HandleDropThrough(GameObject marioObj)
    {
        if (marioObj == null) return;
        TryDropThroughPlatform(marioObj);
    }

    /// <summary>
    /// Session 19: 处理隐藏通道传送（S 键单独触发）
    /// 按优先级检测：正向入口 > 返回触发区
    /// </summary>
    public static void HandlePassageInteraction(GameObject marioObj)
    {
        if (marioObj == null) return;

        // 使用一次 OverlapCircle 同时检测入口和返回触发区
        Collider2D[] hits = Physics2D.OverlapCircleAll(marioObj.transform.position, PASSAGE_CHECK_RADIUS);

        // 优先级 1: 正向入口（HiddenPassage 本体）
        foreach (Collider2D hit in hits)
        {
            HiddenPassage passage = hit.GetComponent<HiddenPassage>();
            if (passage != null)
            {
                passage.TryEnterPassage(marioObj);
                Debug.Log($"[MarioInteraction] S键：尝试进入隐藏通道 {hit.gameObject.name}");
                return;
            }
        }

        // 优先级 2: 返回触发区（从出口传回入口）
        foreach (Collider2D hit in hits)
        {
            HiddenPassageReturnTrigger returnTrigger = hit.GetComponent<HiddenPassageReturnTrigger>();
            if (returnTrigger != null && returnTrigger.OwnerPassage != null)
            {
                bool success = returnTrigger.OwnerPassage.TryReturnPassage(marioObj);
                if (success)
                {
                    Debug.Log($"[MarioInteraction] S键：从返回触发区传回入口 {returnTrigger.OwnerPassage.gameObject.name}");
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 检测 Mario 脚下是否有 OneWayPlatform，有则触发下落
    /// </summary>
    private static bool TryDropThroughPlatform(GameObject marioObj)
    {
        BoxCollider2D marioCol = marioObj.GetComponent<BoxCollider2D>();
        if (marioCol == null) return false;

        Vector2 checkPos = (Vector2)marioObj.transform.position
            + Vector2.down * (marioCol.bounds.extents.y + PLATFORM_CHECK_DISTANCE);
        Vector2 checkSize = new Vector2(marioCol.bounds.size.x * 0.8f, PLATFORM_CHECK_DISTANCE * 2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, checkSize, 0f);
        foreach (Collider2D hit in hits)
        {
            OneWayPlatform owp = hit.GetComponent<OneWayPlatform>();
            if (owp != null)
            {
                owp.AllowDropThrough();
                Debug.Log($"[MarioInteraction] S+Jump 组合键：从单向平台 {hit.gameObject.name} 下落");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检测 Mario 是否在 HiddenPassage 触发区内（旧版，供 HandleDownInteraction 使用）
    /// </summary>
    private static bool TryEnterPassage(GameObject marioObj)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(marioObj.transform.position, PASSAGE_CHECK_RADIUS);
        foreach (Collider2D hit in hits)
        {
            HiddenPassage passage = hit.GetComponent<HiddenPassage>();
            if (passage != null)
            {
                passage.TryEnterPassage(marioObj);
                return true;
            }
        }
        return false;
    }
}
