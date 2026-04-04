using UnityEngine;

/// <summary>
/// Mario 交互辅助工具 - 处理 S 键（下蹲/交互）的上下文交互
/// 
/// Session 17 新增:
///   当 Mario 按下 S 键时，按优先级检测并触发交互：
///   1. 单向平台下落：检测 Mario 脚下是否有 OneWayPlatform → AllowDropThrough()
///   2. 隐藏通道传送：检测 Mario 是否在 HiddenPassage 触发区内 → TryEnterPassage()
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
    /// 处理 Mario 的下蹲交互（S 键按下时调用）
    /// </summary>
    public static void HandleDownInteraction(GameObject marioObj)
    {
        if (marioObj == null) return;

        // 优先级 1: 单向平台下落
        if (TryDropThroughPlatform(marioObj))
        {
            return; // 成功触发下落，不再检测其他交互
        }

        // 优先级 2: 隐藏通道传送
        TryEnterPassage(marioObj);
    }

    /// <summary>
    /// 检测 Mario 脚下是否有 OneWayPlatform，有则触发下落
    /// </summary>
    private static bool TryDropThroughPlatform(GameObject marioObj)
    {
        BoxCollider2D marioCol = marioObj.GetComponent<BoxCollider2D>();
        if (marioCol == null) return false;

        // 在 Mario 脚下检测
        Vector2 checkPos = (Vector2)marioObj.transform.position + Vector2.down * (marioCol.bounds.extents.y + PLATFORM_CHECK_DISTANCE);
        Vector2 checkSize = new Vector2(marioCol.bounds.size.x * 0.8f, PLATFORM_CHECK_DISTANCE * 2f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(checkPos, checkSize, 0f);
        foreach (Collider2D hit in hits)
        {
            OneWayPlatform owp = hit.GetComponent<OneWayPlatform>();
            if (owp != null)
            {
                owp.AllowDropThrough();
                Debug.Log($"[MarioInteraction] 从单向平台 {hit.gameObject.name} 下落");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 检测 Mario 是否在 HiddenPassage 触发区内，有则尝试传送
    /// </summary>
    private static bool TryEnterPassage(GameObject marioObj)
    {
        // 使用 OverlapCircle 检测周围的 HiddenPassage
        Collider2D[] hits = Physics2D.OverlapCircleAll(marioObj.transform.position, PASSAGE_CHECK_RADIUS);
        foreach (Collider2D hit in hits)
        {
            HiddenPassage passage = hit.GetComponent<HiddenPassage>();
            if (passage != null)
            {
                passage.TryEnterPassage(marioObj);
                Debug.Log($"[MarioInteraction] 尝试进入隐藏通道 {hit.gameObject.name}");
                return true;
            }
        }

        return false;
    }
}
