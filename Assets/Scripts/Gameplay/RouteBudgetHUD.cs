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
    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    [Tooltip("HUD 显示位置偏移（屏幕右侧）")]
    [SerializeField] private float hudX = -260f;

    // ── 引用（Start 时缓存）──
    private RouteBudgetService routeBudget;
    private RepeatInterferenceStack repeatStack;
    private InterferenceCompensationPolicy compensation;
    private CounterRevealReward counterReward;

    // ── 样式（惰性初始化）──
    private GUIStyle headerStyle;
    private GUIStyle normalStyle;
    private GUIStyle warnStyle;
    private GUIStyle boostStyle;
    private bool stylesInitialized;

    private void Start()
    {
        routeBudget = FindObjectOfType<RouteBudgetService>();
        repeatStack = FindObjectOfType<RepeatInterferenceStack>();
        compensation = FindObjectOfType<InterferenceCompensationPolicy>();
        counterReward = FindObjectOfType<CounterRevealReward>();
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = Color.white;

        normalStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        normalStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        warnStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        warnStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);

        boostStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        boostStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        InitStylesIfNeeded();

        float x = Screen.width + hudX;
        float y = 10f;
        float lineH = 18f;

        // ── 路线预算 ──
        GUI.Label(new Rect(x, y, 250, lineH), "=== Route Budget ===", headerStyle);
        y += lineH;

        if (routeBudget != null)
        {
            var routes = routeBudget.GetAllRoutes();
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                GUIStyle style = route.Status == RouteBudgetService.RouteStatus.Available
                    ? normalStyle : warnStyle;
                string statusText = route.Status.ToString();
                if (route.Status != RouteBudgetService.RouteStatus.Available)
                {
                    statusText += $" ({route.RecoveryTimer:F1}s)";
                }
                GUI.Label(new Rect(x, y, 250, lineH),
                    $"  {route.RouteId}: {statusText}", style);
                y += lineH;
            }

            GUI.Label(new Rect(x, y, 250, lineH),
                $"  Fallback: {(routeBudget.HasFallbackRoute() ? "YES" : "NO!")}",
                routeBudget.HasFallbackRoute() ? normalStyle : warnStyle);
            y += lineH;
        }
        else
        {
            GUI.Label(new Rect(x, y, 250, lineH), "  (not found)", normalStyle);
            y += lineH;
        }

        y += 5f;

        // ── Heat ──
        GUI.Label(new Rect(x, y, 250, lineH), "=== Heat ===", headerStyle);
        y += lineH;

        if (repeatStack != null)
        {
            float heat = repeatStack.TotalHeat;
            string tier = GetHeatTier(heat);
            GUIStyle heatStyle = heat > 60f ? warnStyle : normalStyle;
            GUI.Label(new Rect(x, y, 250, lineH),
                $"  Heat: {heat:F0}/100 [{tier}]", heatStyle);
            y += lineH;
        }
        else
        {
            GUI.Label(new Rect(x, y, 250, lineH), "  (not found)", normalStyle);
            y += lineH;
        }

        y += 5f;

        // ── 补偿 / 加速 ──
        GUI.Label(new Rect(x, y, 250, lineH), "=== Compensation ===", headerStyle);
        y += lineH;

        if (compensation != null && compensation.IsProgressBoosted)
        {
            GUI.Label(new Rect(x, y, 250, lineH),
                $"  Boost: {compensation.CurrentProgressMultiplier}x " +
                $"({compensation.ProgressBoostRemaining:F1}s)", boostStyle);
            y += lineH;
        }

        if (counterReward != null)
        {
            if (counterReward.IsRewardBoosted)
            {
                GUI.Label(new Rect(x, y, 250, lineH),
                    $"  Reveal Boost: {counterReward.CurrentRewardMultiplier}x " +
                    $"({counterReward.RewardBoostRemaining:F1}s)", boostStyle);
                y += lineH;
            }

            GUI.Label(new Rect(x, y, 250, lineH),
                $"  Counter-Reveals: {counterReward.TotalCounterReveals}", normalStyle);
            y += lineH;
        }
    }

    private string GetHeatTier(float heat)
    {
        if (heat < 30f) return "Calm";
        if (heat < 60f) return "Suspicious";
        if (heat < 85f) return "Alert";
        return "Lockdown";
    }
}
