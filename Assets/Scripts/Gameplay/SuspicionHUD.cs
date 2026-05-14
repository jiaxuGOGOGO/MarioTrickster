using UnityEngine;

/// <summary>
/// Commit 1：可疑度 HUD — 在 Mario 视角显示轻提示。
///
/// 显示内容：
///   - 当有锚点达到揭穿阈值时，显示 "Evidence Ready — [Q] to Reveal"
///   - 当 Mario 处于保护窗口时，显示 "PROTECTED"
///   - 当 SilentMark 被动触发时，短暂闪烁标记图标
///
/// 第一版使用 OnGUI 灰盒文字，后续用 Canvas UI 替换。
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class SuspicionHUD : MonoBehaviour
{
    [Header("=== Commit 1 HUD ===")]
    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    private MarioCounterplayProbe probe;
    private SilentMarkSensor sensor;
    private MarioSuspicionTracker tracker;

    // ── 被动标记闪烁 ──
    private float markFlashTimer;
    private const float MarkFlashDuration = 0.6f;

    // ── 缓存样式 ──
    private bool stylesInitialized;
    private GUIStyle evidenceStyle;
    private GUIStyle protectedStyle;
    private GUIStyle markStyle;

    private void Start()
    {
        // 查找 Mario 上的组件
        var mario = FindObjectOfType<MarioController>();
        if (mario != null)
        {
            probe = mario.GetComponent<MarioCounterplayProbe>();
            sensor = mario.GetComponent<SilentMarkSensor>();
        }

        tracker = FindObjectOfType<MarioSuspicionTracker>();

        // 订阅被动标记事件
        if (sensor != null)
        {
            sensor.OnPassiveMark += HandlePassiveMark;
        }
    }

    private void OnDestroy()
    {
        if (sensor != null)
        {
            sensor.OnPassiveMark -= HandlePassiveMark;
        }
    }

    private void Update()
    {
        if (markFlashTimer > 0f)
        {
            markFlashTimer -= Time.deltaTime;
        }
    }

    private void HandlePassiveMark(PossessionAnchor anchor)
    {
        markFlashTimer = MarkFlashDuration;
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        evidenceStyle = new GUIStyle(GUI.skin.label);
        evidenceStyle.fontSize = 14;
        evidenceStyle.fontStyle = FontStyle.Bold;
        evidenceStyle.alignment = TextAnchor.MiddleCenter;
        evidenceStyle.normal.textColor = new Color(0.2f, 0.9f, 1f, 0.9f);

        protectedStyle = new GUIStyle(GUI.skin.label);
        protectedStyle.fontSize = 16;
        protectedStyle.fontStyle = FontStyle.Bold;
        protectedStyle.alignment = TextAnchor.MiddleCenter;
        protectedStyle.normal.textColor = new Color(0.2f, 1f, 0.3f, 0.9f);

        markStyle = new GUIStyle(GUI.skin.label);
        markStyle.fontSize = 12;
        markStyle.alignment = TextAnchor.MiddleCenter;
        markStyle.normal.textColor = new Color(1f, 0.8f, 0f, 0.8f);
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        InitStylesIfNeeded();

        float centerX = Screen.width * 0.5f;
        float topY = 60f;

        // 保护窗口提示（最高优先级）
        if (probe != null && probe.IsProtected)
        {
            GUI.Label(new Rect(centerX - 100, topY, 200, 25), 
                $"PROTECTED {probe.ProtectedTimeRemaining:F1}s", protectedStyle);
            return;
        }

        // 强扫描就绪提示
        if (probe != null && probe.IsStrongScanReady)
        {
            GUI.Label(new Rect(centerX - 120, topY, 240, 25),
                "Evidence Ready \u2014 [Q] to Reveal", evidenceStyle);
        }

        // 被动标记闪烁
        if (markFlashTimer > 0f)
        {
            float alpha = markFlashTimer / MarkFlashDuration;
            markStyle.normal.textColor = new Color(1f, 0.8f, 0f, alpha);
            GUI.Label(new Rect(centerX - 60, topY + 25, 120, 20),
                "\u25c6 Marked", markStyle);
        }
    }
}
