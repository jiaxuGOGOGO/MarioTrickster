using UnityEngine;

/// <summary>
/// 击退辅助工具 - 统一所有陷阱/敌人的击退方向计算
/// 
/// Session 17 新增:
///   核心原则：击退方向 = Mario 当前移动方向的反方向（后退避开陷阱）
///   如果 Mario 静止，则使用 Mario 相对于伤害源的方向
///   垂直力度保持较小（仅轻微抬起避免卡地面），不会飞天
///   
///   同时负责通知 MarioController / TricksterController 进入 knockback stun
///   
/// 使用方式:
///   所有陷阱/敌人的击退逻辑统一调用此工具类，确保一致行为
/// </summary>
public static class KnockbackHelper
{
    /// <summary>
    /// 计算安全击退力 — 向 Mario 移动方向的反方向后退一小段距离
    /// </summary>
    /// <param name="target">被击退的目标 Transform</param>
    /// <param name="source">伤害源 Transform</param>
    /// <param name="targetRb">目标的 Rigidbody2D（用于读取当前速度判断移动方向）</param>
    /// <param name="horizontalForce">水平击退力度</param>
    /// <param name="verticalForce">垂直击退力度（轻微向上）</param>
    /// <returns>击退力向量（用于 AddForce Impulse）</returns>
    public static Vector2 CalcSafeKnockback(
        Transform target, Transform source, Rigidbody2D targetRb,
        float horizontalForce, float verticalForce)
    {
        // 优先使用目标当前移动方向的反方向
        float moveX = targetRb.velocity.x;
        float knockDirX;

        if (Mathf.Abs(moveX) > 0.1f)
        {
            // Mario 正在移动 → 向移动方向的反方向击退（后退避开陷阱）
            knockDirX = -Mathf.Sign(moveX);
        }
        else
        {
            // Mario 静止 → 使用位置关系判断方向（远离伤害源）
            float diff = target.position.x - source.position.x;
            knockDirX = diff >= 0 ? 1f : -1f;
        }

        return new Vector2(knockDirX * horizontalForce, verticalForce);
    }

    /// <summary>
    /// 通知目标控制器进入 knockback stun（防止帧速度架构覆盖击退力）
    /// </summary>
    public static void NotifyKnockbackStun(Collider2D target)
    {
        MarioController mario = target.GetComponent<MarioController>();
        if (mario != null)
        {
            mario.ApplyKnockbackStun();
        }

        TricksterController trickster = target.GetComponent<TricksterController>();
        if (trickster != null)
        {
            trickster.ApplyKnockbackStun();
        }
    }

    /// <summary>
    /// 通知目标控制器进入 knockback stun（Collision2D 版本）
    /// </summary>
    public static void NotifyKnockbackStun(GameObject target)
    {
        MarioController mario = target.GetComponent<MarioController>();
        if (mario != null)
        {
            mario.ApplyKnockbackStun();
        }

        TricksterController trickster = target.GetComponent<TricksterController>();
        if (trickster != null)
        {
            trickster.ApplyKnockbackStun();
        }
    }
}
