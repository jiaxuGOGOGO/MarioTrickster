using UnityEngine;

/// <summary>
/// Commit 2：路线预算 / Heat / 补偿状态的灰盒 HUD。
///
/// 显示内容：
///   - 各路线状态（Available / Degraded / Blocked）和恢复倒计时
///   - 当前 Heat 值和档位
///   - 补偿加速状态
///   - Counter-Reveal 奖励状态
///
/// 非职责：
///   - 不修改任何游戏逻辑，纯显示。
/// </summary>
public class RouteBudgetHUD : MonoBehaviour
{
    [Header("=== Commit 2 HUD ===")]
    // ── 引用（Start 时缓存）──
    private RouteBudgetService routeBudget;
    private RepeatInterferenceStack repeatStack;
    private InterferenceCompensationPolicy compensation;
    private CounterRevealReward counterReward;

    private void Start()
    {
        routeBudget = FindObjectOfType<RouteBudgetService>();
        repeatStack = FindObjectOfType<RepeatInterferenceStack>();
        compensation = FindObjectOfType<InterferenceCompensationPolicy>();
        counterReward = FindObjectOfType<CounterRevealReward>();
    }


    private string GetHeatTier(float heat)
    {
        if (heat < 30f) return "Calm";
        if (heat < 60f) return "Suspicious";
        if (heat < 85f) return "Alert";
        return "Lockdown";
    }
}
