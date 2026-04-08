using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 传送带 — 改变站立角色水平速度的平台 (S56, S57b修复)
///
/// 设计参考：
///   - Mega Man 系列: 传送带改变移动速度，顺方向加速/逆方向减速
///   - Celeste: 风场区域影响角色水平速度
///
/// ASCII 字符: '&lt;' (向左传送)
/// 行为: 站在传送带上的角色会受到额外的水平推力
///        不继承 BaseHazard（传送带不造成伤害）
///        继承 LevelElementBase 获得自动注册
///
/// // [AI防坑警告] 传送带必须通过 SetPlatformVelocity() 注入速度，
/// // 不能用 AddForce！MarioController.HandleDirection() 每帧覆写 velocity.x，
/// // AddForce 的效果会在同一帧被抹掉。SetPlatformVelocity 注入的速度
/// // 在 HandleDirection 之后叠加，不会被覆盖。
///
/// 实现方式（S57b修复）：
///   与 MovingPlatform 相同的速度注入模式：
///   - OnCollisionStay2D 检测站在上面的角色
///   - 通过 SetPlatformVelocity() 注入传送带水平速度
///   - 角色控制器在 FixedUpdate 末尾叠加平台速度
///   - [DefaultExecutionOrder(-10)] 确保先于角色控制器执行
/// </summary>
[DefaultExecutionOrder(-10)]
public class ConveyorBelt : LevelElementBase
{
    [Header("=== 传送带参数 ===")]
    [Tooltip("传送速度（正值=向右，负值=向左）")]
    [SerializeField] private float conveyorSpeed = -3f;

    // 当前站在传送带上的角色列表
    private readonly HashSet<GameObject> _riders = new HashSet<GameObject>();

    protected override void OnEnable()
    {
        elementName = "ConveyorBelt";
        category = ElementCategory.Platform;
        tags = ElementTag.AffectsPhysics;
        base.OnEnable();
    }

    private void FixedUpdate()
    {
        // 将传送带速度注入所有站在上面的角色
        Vector2 conveyorVelocity = new Vector2(conveyorSpeed, 0f);

        foreach (GameObject rider in _riders)
        {
            if (rider == null) continue;

            MarioController mario = rider.GetComponent<MarioController>();
            if (mario != null)
            {
                mario.SetPlatformVelocity(conveyorVelocity);
                continue;
            }

            TricksterController trickster = rider.GetComponent<TricksterController>();
            if (trickster != null)
            {
                trickster.SetPlatformVelocity(conveyorVelocity);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsRidingFromAbove(collision))
            _riders.Add(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Stay 确保角色在传送带上持续被识别
        if (IsRidingFromAbove(collision))
            _riders.Add(collision.gameObject);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _riders.Remove(collision.gameObject);
    }

    /// <summary>角色是否从传送带上方站立（避免侧面碰撞也触发传送）</summary>
    private bool IsRidingFromAbove(Collision2D col)
    {
        foreach (ContactPoint2D contact in col.contacts)
        {
            if (contact.normal.y < -0.5f) return true;
        }
        return false;
    }

    public override void OnLevelReset()
    {
        _riders.Clear();
    }
}
