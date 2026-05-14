using UnityEngine;

/// <summary>
/// Commit 1：残留视觉提示 — 在有残留的锚点上显示灰盒视觉反馈。
///
/// 挂在场景中的 Managers 对象上，订阅 MarioSuspicionTracker.OnResidueGenerated，
/// 在锚点 SpriteRenderer 上叠加短暂的颜色脉冲，让 Mario 能"看见"出手后的痕迹。
///
/// 第一版只做最简单的颜色闪烁，不引入 Shader 或粒子系统。
/// 后续 Commit 8 会用 SEF/Shader 替换为更精致的视觉效果。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class ResidueVisualHint : MonoBehaviour
{
    [Header("=== Commit 1 残留视觉 ===")]
    [Tooltip("残留提示颜色")]
    [SerializeField] private Color residueColor = new Color(0.8f, 0.4f, 1f, 0.6f);

    [Tooltip("脉冲频率")]
    [SerializeField] private float pulseFrequency = 2f;

    private MarioSuspicionTracker tracker;

    // P2 合规：缓存锚点列表，定期刷新，不在 Update 中 FindObjectsOfType
    private PossessionAnchor[] cachedAnchors;
    private float nextRefreshTime;
    private const float RefreshInterval = 2f;

    private void Start()
    {
        tracker = FindObjectOfType<MarioSuspicionTracker>();
        RefreshAnchors();
    }

    private void Update()
    {
        if (tracker == null) return;

        // 定期刷新锚点缓存
        if (Time.time > nextRefreshTime)
        {
            RefreshAnchors();
            nextRefreshTime = Time.time + RefreshInterval;
        }

        if (cachedAnchors == null) return;

        float pulse = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;

        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            PossessionAnchor anchor = cachedAnchors[i];
            if (anchor == null) continue;

            SpriteRenderer sr = anchor.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;

            AnchorSuspicionData data = tracker.GetData(anchor);
            if (data == null || data.Residue < 0.05f)
            {
                // 无残留，不处理（保持当前颜色不强制恢复，避免与其他系统冲突）
                continue;
            }

            // 有残留：根据残留强度和脉冲叠加颜色
            float intensity = data.Residue * pulse;
            sr.color = Color.Lerp(Color.white, residueColor, intensity * 0.5f);
        }
    }

    private void RefreshAnchors()
    {
        cachedAnchors = FindObjectsOfType<PossessionAnchor>();
    }

    private void OnDisable()
    {
        // 清理：恢复所有锚点颜色
        if (cachedAnchors == null) return;
        for (int i = 0; i < cachedAnchors.Length; i++)
        {
            if (cachedAnchors[i] == null) continue;
            SpriteRenderer sr = cachedAnchors[i].GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = Color.white;
            }
        }
    }
}
