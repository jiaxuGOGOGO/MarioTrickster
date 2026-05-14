using UnityEngine;

/// <summary>
/// Commit 4：热度 HUD — 灰盒阶段的 OnGUI 显示。
///
/// 显示内容：
///   - Heat 条（颜色随档位变化）
///   - 档位文字（Calm / Suspicious / Alert / LOCKDOWN）
///   - Lockdown 触发时的全屏闪烁提示
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class TricksterHeatHUD : MonoBehaviour
{
    [Header("=== Commit 4 热度 HUD ===")]
    [Tooltip("HUD 位置 X（屏幕比例 0-1）")]
    [SerializeField] private float hudX = 0.02f;

    [Tooltip("HUD 位置 Y（屏幕比例 0-1）")]
    [SerializeField] private float hudY = 0.35f;

    [Tooltip("热度条宽度")]
    [SerializeField] private float barWidth = 150f;

    [Tooltip("热度条高度")]
    [SerializeField] private float barHeight = 16f;

    [Tooltip("Lockdown 闪烁持续时间")]
    [SerializeField] private float lockdownFlashDuration = 2f;

    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    // ── 引用 ──
    private TricksterHeatMeter heatMeter;

    // ── 状态 ──
    private float lockdownFlashTimer;

    // ── 样式缓存 ──
    private bool stylesInitialized;
    private GUIStyle tierStyle;
    private GUIStyle lockdownStyle;

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

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        tierStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        lockdownStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        lockdownStyle.normal.textColor = Color.red;
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        if (heatMeter == null) return;

        InitStylesIfNeeded();

        float x = Screen.width * hudX;
        float y = Screen.height * hudY;

        // 档位标签
        Color tierColor = GetTierColor(heatMeter.CurrentTier);
        tierStyle.normal.textColor = tierColor;
        GUI.Label(new Rect(x, y, 200, 20),
                  $"Heat: {heatMeter.GetTierLabel()} ({heatMeter.Heat:F0}/100)", tierStyle);

        // 热度条背景
        Rect barBg = new Rect(x, y + 22, barWidth, barHeight);
        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        GUI.DrawTexture(barBg, Texture2D.whiteTexture);

        // 热度条填充
        float fill = heatMeter.HeatNormalized;
        Rect barFill = new Rect(x, y + 22, barWidth * fill, barHeight);
        GUI.color = tierColor;
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);

        // 档位分隔线
        GUI.color = new Color(1f, 1f, 1f, 0.4f);
        float s1 = barWidth * 0.30f;
        float s2 = barWidth * 0.60f;
        float s3 = barWidth * 0.85f;
        GUI.DrawTexture(new Rect(x + s1, y + 22, 1, barHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + s2, y + 22, 1, barHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + s3, y + 22, 1, barHeight), Texture2D.whiteTexture);

        GUI.color = Color.white;

        // Lockdown 冷却指示
        if (heatMeter.IsLockdownCooling)
        {
            tierStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(x, y + 42, 200, 18), "[Lockdown cooldown]", tierStyle);
        }

        // Lockdown 全屏闪烁
        if (lockdownFlashTimer > 0f)
        {
            float alpha = Mathf.Clamp01(lockdownFlashTimer / lockdownFlashDuration) * 0.3f;
            GUI.color = new Color(1f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(Screen.width * 0.5f - 100, Screen.height * 0.4f, 200, 40),
                      "LOCKDOWN!", lockdownStyle);
        }
    }

    // ─────────────────────────────────────────────────────
    #region 工具方法

    private Color GetTierColor(TricksterHeatMeter.HeatTier tier)
    {
        switch (tier)
        {
            case TricksterHeatMeter.HeatTier.Calm:
                return new Color(0.3f, 0.8f, 0.3f);
            case TricksterHeatMeter.HeatTier.Suspicious:
                return new Color(1f, 0.8f, 0.2f);
            case TricksterHeatMeter.HeatTier.Alert:
                return new Color(1f, 0.5f, 0.1f);
            case TricksterHeatMeter.HeatTier.Lockdown:
                return new Color(1f, 0.1f, 0.1f);
            default:
                return Color.white;
        }
    }

    private void HandleLockdown()
    {
        lockdownFlashTimer = lockdownFlashDuration;
    }

    #endregion
}
