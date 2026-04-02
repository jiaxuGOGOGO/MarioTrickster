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
/// Session 11 修复：
///   - B016 镜头来回轻微晃动
///     根因1: LookAhead 依赖 IsMoving 属性，角色静止时 _frameVelocity 因 groundingForce
///            和物理微扰在 0 附近波动，导致 IsMoving 频繁切换，前瞻目标来回跳变。
///            修复: 使用独立的速度阈值 + 滞后(hysteresis)判断移动状态，避免边界抖动。
///     根因2: 死区(DeadZone)逻辑在边缘处振荡——目标位置刚好在死区边界时，
///            每帧在"跟随"和"不跟随"之间切换。
///            修复: 使用渐进式死区(smooth dead zone)，在死区内外之间平滑过渡。
///     根因3: Shake 协程直接 += offset 到 transform.position，没有恢复基准位置，
///            导致震动后相机永久偏移。
///            修复: Shake 在独立的偏移量上工作，LateUpdate 统一叠加。
///     额外优化: 使用 SmoothDamp 替代 Lerp，避免 Lerp 的帧率依赖问题。
/// 
/// 使用方式: 挂载到Main Camera上，在Inspector中拖入Mario的Transform
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("=== 跟随目标 ===")]
    [SerializeField] private Transform target; // Mario的Transform
    
    [Header("=== 跟随参数 ===")]
    [Tooltip("平滑跟随时间（越小越快跟上，建议 0.1-0.3）")]
    [SerializeField] private float smoothTime = 0.15f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    
    [Header("=== 前瞻（Look Ahead）===")]
    [Tooltip("根据Mario移动方向提前偏移相机")]
    [SerializeField] private bool useLookAhead = true;
    [SerializeField] private float lookAheadDistance = 2f;
    [Tooltip("前瞻平滑时间（越大越慢，减少抖动）")]
    [SerializeField] private float lookAheadSmoothTime = 0.5f;
    [Tooltip("判定为移动的速度阈值")]
    [SerializeField] private float moveThreshold = 0.5f;
    [Tooltip("判定为停止的速度阈值（低于移动阈值，形成滞后区间防抖动）")]
    [SerializeField] private float stopThreshold = 0.2f;
    
    [Header("=== 关卡边界限制 ===")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 100f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 20f;
    
    [Header("=== 死区（Dead Zone）===")]
    [Tooltip("目标在此范围内移动时相机不跟随（内半径）")]
    [SerializeField] private float deadZoneWidth = 0.5f;
    [SerializeField] private float deadZoneHeight = 0.3f;
    [Tooltip("死区外半径（内外之间为渐进过渡区，消除边缘振荡）")]
    [SerializeField] private float deadZoneSoftWidth = 1.0f;
    [SerializeField] private float deadZoneSoftHeight = 0.6f;
    
    // 内部状态
    private float currentLookAhead;
    private float lookAheadVelocity; // SmoothDamp 用
    private MarioController marioController;
    private Vector3 smoothDampVelocity = Vector3.zero; // SmoothDamp 用
    private bool isMoving; // 带滞后的移动状态
    
    // 震动系统（独立偏移量，不污染主位置）
    private Vector3 shakeOffset = Vector3.zero;
    private bool isShaking;
    
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
        
        // 计算目标位置
        Vector3 desiredPosition = target.position + offset;
        
        // ── 前瞻偏移（带滞后的移动检测 + SmoothDamp 平滑）──
        if (useLookAhead && marioController != null)
        {
            // 滞后(hysteresis)移动检测：
            //   从静止→移动：速度需超过 moveThreshold
            //   从移动→静止：速度需低于 stopThreshold
            //   两个阈值之间的区间避免了边界抖动
            float absSpeed = Mathf.Abs(marioController.Velocity.x);
            if (!isMoving && absSpeed > moveThreshold)
            {
                isMoving = true;
            }
            else if (isMoving && absSpeed < stopThreshold)
            {
                isMoving = false;
            }
            
            float targetLookAhead = 0f;
            if (isMoving)
            {
                targetLookAhead = marioController.IsFacingRight ? lookAheadDistance : -lookAheadDistance;
            }
            
            // 使用 SmoothDamp 而非 Lerp，确保帧率无关的平滑过渡
            currentLookAhead = Mathf.SmoothDamp(currentLookAhead, targetLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);
            desiredPosition.x += currentLookAhead;
        }
        
        // ── 渐进式死区（Soft Dead Zone）──
        // 死区内(innerWidth/Height)：完全不跟随（权重=0）
        // 死区外(softWidth/Height)：完全跟随（权重=1）
        // 中间区域：线性插值过渡，消除边缘振荡
        Vector3 diff = desiredPosition - transform.position;
        
        float followX = CalculateSoftDeadZone(diff.x, deadZoneWidth, deadZoneSoftWidth);
        float followY = CalculateSoftDeadZone(diff.y, deadZoneHeight, deadZoneSoftHeight);
        
        // 应用死区权重：权重为0时保持当前位置，权重为1时完全跟随
        Vector3 deadZoneAdjusted = new Vector3(
            Mathf.Lerp(transform.position.x, desiredPosition.x, followX),
            Mathf.Lerp(transform.position.y, desiredPosition.y, followY),
            desiredPosition.z
        );
        
        // ── 平滑跟随（SmoothDamp 替代 Lerp）──
        // SmoothDamp 的优势：
        //   1. 帧率无关（Lerp * deltaTime 在不同帧率下表现不一致）
        //   2. 自然的加减速曲线
        //   3. smoothTime 参数直观（约为到达目标的时间）
        Vector3 smoothedPosition = Vector3.SmoothDamp(
            transform.position, deadZoneAdjusted, ref smoothDampVelocity, smoothTime);
        
        // ── 边界限制 ──
        if (useBounds)
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minX, maxX);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minY, maxY);
        }
        
        // 保持Z轴不变
        smoothedPosition.z = offset.z;
        
        // ── 叠加震动偏移（独立于主位置计算）──
        transform.position = smoothedPosition + shakeOffset;
    }
    
    /// <summary>
    /// 计算渐进式死区的跟随权重
    /// </summary>
    /// <param name="diff">目标与当前位置的差值</param>
    /// <param name="innerSize">死区内半径（完全不跟随）</param>
    /// <param name="outerSize">死区外半径（完全跟随）</param>
    /// <returns>0~1 的跟随权重</returns>
    private float CalculateSoftDeadZone(float diff, float innerSize, float outerSize)
    {
        float absDiff = Mathf.Abs(diff);
        
        if (absDiff <= innerSize)
        {
            return 0f; // 死区内：不跟随
        }
        
        if (absDiff >= outerSize)
        {
            return 1f; // 死区外：完全跟随
        }
        
        // 过渡区：线性插值
        return (absDiff - innerSize) / (outerSize - innerSize);
    }
    
    #region 公共方法
    
    /// <summary>设置跟随目标</summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            marioController = target.GetComponent<MarioController>();
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
        
        // 重置平滑状态，避免 SnapToTarget 后的回弹
        smoothDampVelocity = Vector3.zero;
        lookAheadVelocity = 0f;
    }
    
    /// <summary>相机震动效果（受伤/爆炸时使用）</summary>
    public void Shake(float duration = 0.2f, float magnitude = 0.3f)
    {
        StartCoroutine(ShakeCoroutine(duration, magnitude));
    }
    
    /// <summary>
    /// 震动协程 - 在独立的 shakeOffset 上工作
    /// LateUpdate 统一叠加 shakeOffset 到最终位置，
    /// 避免直接修改 transform.position 导致永久偏移。
    /// </summary>
    private System.Collections.IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            // 衰减震动强度
            float currentMagnitude = magnitude * (1f - elapsed / duration);
            
            float x = Random.Range(-1f, 1f) * currentMagnitude;
            float y = Random.Range(-1f, 1f) * currentMagnitude;
            
            shakeOffset = new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // 震动结束，清零偏移
        shakeOffset = Vector3.zero;
        isShaking = false;
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
        
        // 绘制死区（内区域 + 外区域）
        if (Application.isPlaying && target != null)
        {
            // 内死区（完全不跟随）
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneWidth * 2, deadZoneHeight * 2, 0));
            
            // 外死区（过渡区边界）
            Gizmos.color = new Color(0, 1, 0, 0.15f);
            Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneSoftWidth * 2, deadZoneSoftHeight * 2, 0));
        }
    }
    
    #endregion
}
