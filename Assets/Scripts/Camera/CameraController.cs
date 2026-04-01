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
/// 使用方式: 挂载到Main Camera上，在Inspector中拖入Mario的Transform
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("=== 跟随目标 ===")]
    [SerializeField] private Transform target; // Mario的Transform
    
    [Header("=== 跟随参数 ===")]
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);
    
    [Header("=== 前瞻（Look Ahead）===")]
    [Tooltip("根据Mario移动方向提前偏移相机")]
    [SerializeField] private bool useLookAhead = true;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSpeed = 3f;
    
    [Header("=== 关卡边界限制 ===")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 100f;
    [SerializeField] private float minY = -5f;
    [SerializeField] private float maxY = 20f;
    
    [Header("=== 死区（Dead Zone）===")]
    [Tooltip("目标在此范围内移动时相机不跟随")]
    [SerializeField] private float deadZoneWidth = 0.5f;
    [SerializeField] private float deadZoneHeight = 0.3f;
    
    // 内部状态
    private float currentLookAhead;
    private MarioController marioController;
    private Vector3 velocity = Vector3.zero; // 用于SmoothDamp
    
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
        
        // 前瞻偏移
        if (useLookAhead && marioController != null)
        {
            float targetLookAhead = 0f;
            if (marioController.IsMoving)
            {
                targetLookAhead = marioController.IsFacingRight ? lookAheadDistance : -lookAheadDistance;
            }
            currentLookAhead = Mathf.Lerp(currentLookAhead, targetLookAhead, lookAheadSpeed * Time.deltaTime);
            desiredPosition.x += currentLookAhead;
        }
        
        // 死区检测
        Vector3 diff = desiredPosition - transform.position;
        if (Mathf.Abs(diff.x) < deadZoneWidth)
        {
            desiredPosition.x = transform.position.x;
        }
        if (Mathf.Abs(diff.y) < deadZoneHeight)
        {
            desiredPosition.y = transform.position.y;
        }
        
        // 平滑跟随
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // 边界限制
        if (useBounds)
        {
            smoothedPosition.x = Mathf.Clamp(smoothedPosition.x, minX, maxX);
            smoothedPosition.y = Mathf.Clamp(smoothedPosition.y, minY, maxY);
        }
        
        // 保持Z轴不变
        smoothedPosition.z = offset.z;
        
        transform.position = smoothedPosition;
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
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            
            transform.position += new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
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
        
        // 绘制死区
        if (Application.isPlaying && target != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneWidth * 2, deadZoneHeight * 2, 0));
        }
    }
    
    #endregion
}
