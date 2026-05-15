using UnityEngine;

/// <summary>
/// Commit 4：热度 HUD 状态桥接。
///
/// 旧灰盒 OnGUI 显示已迁移到 GlobalGameUICanvas（UGUI）。本组件仅保留 Lockdown 事件订阅
/// 与兼容状态缓存，避免运行时继续执行 IMGUI 绘制。
/// </summary>
public class TricksterHeatHUD : MonoBehaviour
{
    [Header("=== Commit 4 热度 HUD 状态桥接 ===")]
    [Tooltip("Lockdown 闪烁持续时间")]
    [SerializeField] private float lockdownFlashDuration = 2f;

    private TricksterHeatMeter heatMeter;
    private float lockdownFlashTimer;

    private void Start()
    {
        heatMeter = FindObjectOfType<TricksterHeatMeter>();

        if (heatMeter != null)
        {
            heatMeter.OnLockdownTriggered += HandleLockdown;
        }
    }

    private void OnDestroy()
    {
        if (heatMeter != null)
        {
            heatMeter.OnLockdownTriggered -= HandleLockdown;
        }
    }

    private void Update()
    {
        if (lockdownFlashTimer > 0f)
        {
            lockdownFlashTimer -= Time.deltaTime;
        }
    }

    private void HandleLockdown()
    {
        lockdownFlashTimer = lockdownFlashDuration;
    }
}
