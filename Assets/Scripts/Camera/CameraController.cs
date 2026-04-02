using UnityEngine;

/// <summary>
/// 相机控制器 - MVP核心脚本
/// 功能: 平滑跟随Mario，限制在关卡边界内
/// 
/// 设计决策: 
///   - 相机锁定跟随Mario（闯关者视角为主）
///   - Trickster需要在相机视野范围内活动
///   - 后期可升级为Cinemachine实现更高级的效果
///
/// Session 11 修复 B016 镜头来回轻微晃动（第二版）:
///   根因: CameraController 在 LateUpdate 中读取 MarioController.Velocity，
///         但 _frameVelocity 受 FixedUpdate/LateUpdate 频率差异、groundingForce、
///         浮点精度等因素影响，即使 Mario 静止时也在微小值间波动。
///         这些微小波动通过前瞻(LookAhead)和死区(DeadZone)计算被放大，
///         导致相机目标位置每帧有微小差异，SmoothDamp 持续追踪产生可见晃动。
///
///   修复策略: 
///     1. 完全不依赖 Velocity 属性 — 改为直接读取 target.position，
///        用帧间位置差计算平滑速度，天然过滤物理抖动。
///     2. 前瞻(LookAhead)只依赖 IsFacingRight（离散状态，不会抖动），
///        不再依赖连续的速度值。
///     3. 移动检测使用位置差的平滑值 + 滞后阈值，彻底消除边界抖动。
///     4. 移除死区系统 — MVP阶段死区是过度设计，反而引入振荡源。
///        直接用 SmoothDamp 跟随即可，smoothTime 本身就提供了足够的稳定性。
///     5. 对最终位置做微小值截断(snap)，消除亚像素级抖动。
/// 
/// 使用方式: 挂载到Main Camera上，在Inspector中拖入Mario的Transform
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("=== 跟随目标 ===")]
    [SerializeField] private Transform target; // Mario的Transform
    
    [Header("=== 跟随参数 ===")]
    [Tooltip("平滑跟随时间（越小越快跟上，建议 0.15-0.3）")]
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    
    [Header("=== 前瞻（Look Ahead）===")]
    [Tooltip("根据Mario朝向提前偏移相机")]
    [SerializeField] private bool useLookAhead = true;
    [SerializeField] private float lookAheadDistance = 1.5f;
    [Tooltip("前瞻平滑时间（越大越慢，防止转向时抖动）")]
    [SerializeField] private float lookAheadSmoothTime = 0.8f;
    [Tooltip("位置变化低于此值视为静止（不触发前瞻）")]
    [SerializeField] private float movingThreshold = 0.01f;
    
    [Header("=== 关卡边界限制 ===")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 100f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 20f;
    
    // 内部状态
    private MarioController marioController;
    private Vector3 smoothDampVelocity = Vector3.zero;
    
    // 前瞻
    private float currentLookAhead;
    private float lookAheadVelocity;
    
    // 位置差平滑（用于移动检测，替代直接读 Velocity）
    private Vector3 lastTargetPosition;
    private float smoothedSpeed; // 平滑后的水平速度
    private bool isMoving;
    
    // 震动系统（独立偏移量）
    private Vector3 shakeOffset = Vector3.zero;
    
    // 亚像素截断阈值
    private const float SNAP_THRESHOLD = 0.001f;
    
    private void Start()
    {
        // 自动查找Mario
        if (target == null)
        {
            MarioController mario = FindObjectOfType<MarioController>();
            if (mario != null)
            {
                target = mario.transform;
            }
        }
        
        if (target != null)
        {
            marioController = target.GetComponent<MarioController>();
            lastTargetPosition = target.position;
            
            // 初始化相机位置（避免开场时的平滑移动）
            Vector3 startPos = target.position + offset;
            if (useBounds)
            {
                startPos.x = Mathf.Clamp(startPos.x, minX, maxX);
                startPos.y = Mathf.Clamp(startPos.y, minY, maxY);
            }
            transform.position = startPos;
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        // ── 1. 基于位置差计算平滑速度（不依赖 Velocity 属性）──
        // 为什么不用 MarioController.Velocity？
        //   Velocity 是 _frameVelocity，受 FixedUpdate 时序、groundingForce、
        //   平台速度扣除等影响，即使静止时也有微小波动。
        //   而 position 差值天然是帧间实际位移，更稳定。
        Vector3 posDelta = target.position - lastTargetPosition;
        lastTargetPosition = target.position;
        
        float frameSpeed = Mathf.Abs(posDelta.x) / Mathf.Max(Time.deltaTime, 0.001f);
        // 用指数平滑过滤速度，避免单帧突变
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, frameSpeed, Time.deltaTime * 5f);
        
        // ── 2. 滞后移动检测（基于平滑速度）──
        if (!isMoving && smoothedSpeed > 0.5f)
        {
            isMoving = true;
        }
        else if (isMoving && smoothedSpeed < 0.1f)
        {
            isMoving = false;
        }
        
        // ── 3. 计算目标位置 ──
        Vector3 desiredPosition = target.position + offset;
        
        // ── 4. 前瞻偏移（只依赖 IsFacingRight，不依赖连续速度值）──
        if (useLookAhead && marioController != null)
        {
            float targetLookAhead = 0f;
            if (isMoving)
            {
                targetLookAhead = marioController.IsFacingRight ? lookAheadDistance : -lookAheadDistance;
            }
            // 前瞻用独立的 SmoothDamp，慢速过渡
            currentLookAhead = Mathf.SmoothDamp(
                currentLookAhead, targetLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);
            
            desiredPosition.x += currentLookAhead;
        }
        
        // ── 5. 平滑跟随（SmoothDamp）──
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref smoothDampVelocity, smoothTime);
        
        // ── 6. 边界限制 ──
        if (useBounds)
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minX, maxX);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minY, maxY);
        }
        
        // 保持Z轴不变
        smoothedPosition.z = offset.z;
        
        // ── 7. 亚像素截断：消除极微小的位置变化 ──
        // 当相机几乎到达目标时，直接snap到目标，避免SmoothDamp的无限趋近抖动
        if (Mathf.Abs(smoothedPosition.x - desiredPosition.x) < SNAP_THRESHOLD)
            smoothedPosition.x = desiredPosition.x;
        if (Mathf.Abs(smoothedPosition.y - desiredPosition.y) < SNAP_THRESHOLD)
            smoothedPosition.y = desiredPosition.y;
        
        // ── 8. 叠加震动偏移 ──
        transform.position = smoothedPosition + shakeOffset;
    }
    
    #region 公共方法
    
    /// <summary>设置跟随目标</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            marioController = target.GetComponent<MarioController>();
            lastTargetPosition = target.position;
        }
    }
    
    /// <summary>设置关卡边界</summary>
    public void SetBounds(float newMinX, float newMaxX, float newMinY, float newMaxY)
    {
        minX = newMinX;
        maxX = newMaxX;
        minY = newMinY;
        maxY = newMaxY;
    }
    
    /// <summary>立即移动到目标位置（无平滑）</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        
        Vector3 snapPos = target.position + offset;
        if (useBounds)
        {
            snapPos.x = Mathf.Clamp(snapPos.x, minX, maxX);
            snapPos.y = Mathf.Clamp(snapPos.y, minY, maxY);
        }
        snapPos.z = offset.z;
        transform.position = snapPos;
        
        // 重置所有平滑状态
        smoothDampVelocity = Vector3.zero;
        lookAheadVelocity = 0f;
        currentLookAhead = 0f;
        smoothedSpeed = 0f;
        isMoving = false;
        lastTargetPosition = target.position;
    }
    
    /// <summary>相机震动效果（受伤/爆炸时使用）</summary>
    public void Shake(float duration = 0.2f, float magnitude = 0.3f)
    {
        StartCoroutine(ShakeCoroutine(duration, magnitude));
    }
    
    private System.Collections.IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float currentMagnitude = magnitude * (1f - elapsed / duration);
            float x = Random.Range(-1f, 1f) * currentMagnitude;
            float y = Random.Range(-1f, 1f) * currentMagnitude;
            shakeOffset = new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        shakeOffset = Vector3.zero;
    }
    
    #endregion
    
    #region 调试可视化
    
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        
        // 绘制关卡边界
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0);
        Vector3 size = new Vector3(maxX - minX, maxY - minY, 0);
        Gizmos.DrawWireCube(center, size);
    }
    
    #endregion
}
