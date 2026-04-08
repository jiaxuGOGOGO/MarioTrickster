using UnityEngine;

/// <summary>
/// 旋转锯片 — 圆周运动的动态障碍 (S56)
///
/// 设计参考：
///   - Super Meat Boy 旋转锯片：固定轨道 + 恒定角速度
///   - Celeste Chapter 3 锯片：圆周运动 + 视觉旋转
///
/// ASCII 字符: '@'
/// 行为: 围绕生成位置做圆周运动，碰到玩家造成伤害（非致命）+ 击退
/// 继承 BaseHazard 获得统一的防刷屏保护
///
/// 参数:
///   - orbitRadius: 圆周运动半径（0 = 原地旋转）
///   - orbitSpeed: 公转角速度（度/秒）
///   - spinSpeed: 自转角速度（度/秒，纯视觉）
///   - sawDamage: 伤害值（默认 1，非致命）
///   - knockbackForce: 击退力度
/// </summary>
public class SawBlade : BaseHazard
{
    [Header("=== 锯片参数 ===")]
    [SerializeField] private float orbitRadius = 1.5f;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField] private float spinSpeed = 360f;
    [SerializeField] private int sawDamage = 1;
    [SerializeField] private float knockbackHorizontal = 5f;
    [SerializeField] private float knockbackVertical = 3f;

    private Vector3 orbitCenter;
    private float currentAngle;
    private Transform visualTransform;

    private void Awake()
    {
        baseDamage = sawDamage;
        orbitCenter = transform.position;

        // 查找 Visual 子节点（视碰分离架构）
        Transform vis = transform.Find("Visual");
        if (vis != null)
            visualTransform = vis;
    }

    private void Update()
    {
        // 公转运动
        if (orbitRadius > 0.01f)
        {
            currentAngle += orbitSpeed * Time.deltaTime;
            float rad = currentAngle * Mathf.Deg2Rad;
            transform.position = orbitCenter + new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                Mathf.Sin(rad) * orbitRadius,
                0f);
        }

        // 自转视觉（仅旋转 Visual 子节点，不影响碰撞体）
        if (visualTransform != null)
        {
            visualTransform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        }
    }

    protected override int GetDamage() => sawDamage;

    protected override void OnHazardTriggered(PlayerHealth health)
    {
        // 非致命伤害
        health.TakeDamage(GetDamage());

        // 击退
        Rigidbody2D targetRb = health.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 knockback = KnockbackHelper.CalcSafeKnockback(
                health.transform, transform, targetRb,
                knockbackHorizontal, knockbackVertical);
            targetRb.AddForce(knockback, ForceMode2D.Impulse);

            // 通知控制器进入击退僵直
            KnockbackHelper.NotifyKnockbackStun(health.gameObject);
        }
    }

    protected override bool ShouldFreezeOnKill() => false;

    /// <summary>关卡重置时恢复到初始位置和角度</summary>
    public void OnLevelReset()
    {
        currentAngle = 0f;
        transform.position = orbitCenter;
        ResetProcessedSet();
    }
}
