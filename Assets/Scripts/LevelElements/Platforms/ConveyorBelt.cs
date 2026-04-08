using UnityEngine;

/// <summary>
/// 传送带 — 改变站立角色水平速度的平台 (S56)
///
/// 设计参考：
///   - Mega Man 系列: 传送带改变移动速度，顺方向加速/逆方向减速
///   - Celeste: 风场区域影响角色水平速度
///
/// ASCII 字符: '<' (向左传送)
/// 行为: 站在传送带上的角色会受到额外的水平推力
///        不继承 BaseHazard（传送带不造成伤害）
///        继承 LevelElementBase 获得自动注册
///
/// 实现方式：
///   使用 OnCollisionStay2D 检测站在上面的角色，
///   通过 Rigidbody2D.AddForce 施加持续推力。
///   这种方式不会覆盖玩家的输入控制，只是叠加一个外力。
/// </summary>
public class ConveyorBelt : LevelElementBase
{
    [Header("=== 传送带参数 ===")]
    [Tooltip("传送速度（正值=向右，负值=向左）")]
    [SerializeField] private float conveyorSpeed = -3f;

    [Tooltip("推力强度（越大越难逆行）")]
    [SerializeField] private float pushForce = 8f;

    protected override void OnEnable()
    {
        elementName = "ConveyorBelt";
        category = ElementCategory.Platform;
        tags = ElementTag.AffectsPhysics;
        base.OnEnable();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 只影响站在上面的角色（接触法线指向上方）
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // 施加水平推力（不覆盖玩家输入，只叠加外力）
                    rb.AddForce(new Vector2(pushForce * Mathf.Sign(conveyorSpeed), 0f));
                }
                break;
            }
        }
    }

    public override void OnLevelReset()
    {
        // 传送带无状态，无需重置
    }
}
