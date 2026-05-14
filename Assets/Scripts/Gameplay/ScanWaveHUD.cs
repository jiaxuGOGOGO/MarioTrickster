using UnityEngine;

/// <summary>
/// Commit 6：扫描波 HUD — 灰盒阶段的 OnGUI 显示。
///
/// 显示内容：
///   - 预告倒计时（Warning 阶段）
///   - 扫描波进度条（Scanning 阶段）
///   - 扫描波视觉线（世界空间竖线）
///   - 冷却指示（Cooldown 阶段）
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class ScanWaveHUD : MonoBehaviour
{
    [Header("=== Commit 6 扫描波 HUD ===")]
    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    [Tooltip("扫描波线颜色")]
    [SerializeField] private Color scanLineColor = new Color(0.2f, 0.8f, 1f, 0.7f);

    [Tooltip("预告闪烁颜色")]
    [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.1f, 0.8f);

    // ── 引用 ──
    private AlarmCrisisDirector crisisDirector;

    // ── 样式缓存 ──
    private bool stylesInitialized;
    private GUIStyle warningStyle;
    private GUIStyle progressStyle;
    private GUIStyle cooldownStyle;

    // ── 扫描线纹理 ──
    private Texture2D scanLineTex;

    private void Start()
    {
        crisisDirector = FindObjectOfType<AlarmCrisisDirector>();

        // 创建扫描线纹理
        scanLineTex = new Texture2D(1, 1);
        scanLineTex.SetPixel(0, 0, Color.white);
        scanLineTex.Apply();
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        warningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        progressStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        cooldownStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleRight
        };
        cooldownStyle.normal.textColor = new Color(0.5f, 0.8f, 1f, 0.6f);
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        if (crisisDirector == null) return;

        InitStylesIfNeeded();

        switch (crisisDirector.CurrentPhase)
        {
            case AlarmCrisisDirector.ScanPhase.Warning:
                DrawWarning();
                break;
            case AlarmCrisisDirector.ScanPhase.Scanning:
                DrawScanning();
                break;
            case AlarmCrisisDirector.ScanPhase.Cooldown:
                DrawCooldown();
                break;
        }
    }

    private void DrawWarning()
    {
        float timer = crisisDirector.PhaseTimer;
        float total = crisisDirector.WarningDuration;
        float flash = Mathf.PingPong(Time.time * 4f, 1f);

        // 全屏闪烁边框
        GUI.color = new Color(warningColor.r, warningColor.g, warningColor.b, flash * 0.3f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, 4), scanLineTex);
        GUI.DrawTexture(new Rect(0, Screen.height - 4, Screen.width, 4), scanLineTex);
        GUI.DrawTexture(new Rect(0, 0, 4, Screen.height), scanLineTex);
        GUI.DrawTexture(new Rect(Screen.width - 4, 0, 4, Screen.height), scanLineTex);

        // 中央预告文字
        GUI.color = new Color(1f, 0.4f, 0.1f, 0.8f + flash * 0.2f);
        warningStyle.normal.textColor = GUI.color;
        GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 40),
                  $"⚠ SCAN WAVE INCOMING: {timer:F1}s ⚠", warningStyle);

        // 进度条
        GUI.color = Color.white;
        float barW = 200f;
        float barH = 8f;
        float barX = Screen.width * 0.5f - barW * 0.5f;
        float barY = Screen.height * 0.15f + 45f;
        float progress = 1f - (timer / total);

        // 背景
        GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), scanLineTex);

        // 填充
        GUI.color = warningColor;
        GUI.DrawTexture(new Rect(barX, barY, barW * progress, barH), scanLineTex);

        GUI.color = Color.white;
    }

    private void DrawScanning()
    {
        if (Camera.main == null) return;

        float scanX = crisisDirector.ScanX;
        float startX = crisisDirector.ScanStartX;
        float endX = crisisDirector.ScanEndX;

        // 世界坐标转屏幕坐标画扫描线
        Vector3 worldBottom = new Vector3(scanX, -10f, 0f);
        Vector3 worldTop = new Vector3(scanX, 30f, 0f);
        Vector3 screenBottom = Camera.main.WorldToScreenPoint(worldBottom);
        Vector3 screenTop = Camera.main.WorldToScreenPoint(worldTop);

        if (screenBottom.z > 0)
        {
            float sx = screenBottom.x;
            float lineWidth = 3f;

            // 扫描线
            GUI.color = scanLineColor;
            GUI.DrawTexture(new Rect(sx - lineWidth * 0.5f, 0, lineWidth, Screen.height), scanLineTex);

            // 扫描线光晕
            GUI.color = new Color(scanLineColor.r, scanLineColor.g, scanLineColor.b, 0.15f);
            GUI.DrawTexture(new Rect(sx - 15f, 0, 30f, Screen.height), scanLineTex);
        }

        // 顶部进度条
        float progress = (scanX - startX) / (endX - startX);
        progress = Mathf.Clamp01(progress);

        float barW = Screen.width * 0.6f;
        float barH = 6f;
        float barX = Screen.width * 0.2f;
        float barY = 10f;

        GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);
        GUI.DrawTexture(new Rect(barX, barY, barW, barH), scanLineTex);

        GUI.color = scanLineColor;
        GUI.DrawTexture(new Rect(barX, barY, barW * progress, barH), scanLineTex);

        // 标签
        progressStyle.normal.textColor = scanLineColor;
        GUI.Label(new Rect(barX, barY + barH + 2, barW, 18), "SCANNING...", progressStyle);

        GUI.color = Color.white;
    }

    private void DrawCooldown()
    {
        // 右上角小字显示冷却
        float x = Screen.width - 160f;
        float y = 10f;
        GUI.Label(new Rect(x, y, 150, 18), "Scan cooldown...", cooldownStyle);
    }
}
