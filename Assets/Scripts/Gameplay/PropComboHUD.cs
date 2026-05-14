using UnityEngine;

/// <summary>
/// Commit 3：连锁 HUD — 灰盒阶段的 OnGUI 显示。
///
/// 显示内容：
///   - 当前 Chain 数和倍率
///   - 连锁窗口剩余时间（进度条）
///   - 不同锚点/不同机关的加成提示
///   - 连锁断裂时的短暂提示
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class PropComboHUD : MonoBehaviour
{
    [Header("=== Commit 3 连锁 HUD ===")]
    [Tooltip("HUD 显示位置 X（屏幕比例 0-1）")]
    [SerializeField] private float hudX = 0.5f;

    [Tooltip("HUD 显示位置 Y（屏幕比例 0-1）")]
    [SerializeField] private float hudY = 0.15f;

    [Tooltip("连锁断裂提示持续时间")]
    [SerializeField] private float breakMessageDuration = 1.5f;

    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    // ── 引用 ──
    private PropComboTracker comboTracker;

    // ── 状态 ──
    private float breakMessageTimer;
    private int lastBreakCount;
    private float lastBreakMultiplier;
    private string lastHitMessage;
    private float hitMessageTimer;

    // ── 样式缓存 ──
    private bool stylesInitialized;
    private GUIStyle chainStyle;
    private GUIStyle multiplierStyle;
    private GUIStyle breakStyle;
    private GUIStyle hitStyle;

    private void Start()
    {
        comboTracker = FindObjectOfType<PropComboTracker>();

        if (comboTracker != null)
        {
            comboTracker.OnComboBreak += HandleComboBreak;
            comboTracker.OnComboHit += HandleComboHit;
        }
    }

    private void OnDestroy()
    {
        if (comboTracker != null)
        {
            comboTracker.OnComboBreak -= HandleComboBreak;
            comboTracker.OnComboHit -= HandleComboHit;
        }
    }

    private void Update()
    {
        if (breakMessageTimer > 0f)
        {
            breakMessageTimer -= Time.deltaTime;
        }

        if (hitMessageTimer > 0f)
        {
            hitMessageTimer -= Time.deltaTime;
        }
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        chainStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        chainStyle.normal.textColor = Color.yellow;

        multiplierStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };
        multiplierStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);

        breakStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        breakStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

        hitStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter
        };
        hitStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (!showHUD) return;
        if (comboTracker == null) return;

        InitStylesIfNeeded();

        float cx = Screen.width * hudX;
        float cy = Screen.height * hudY;

        // 连锁断裂提示
        if (breakMessageTimer > 0f)
        {
            float alpha = Mathf.Clamp01(breakMessageTimer / breakMessageDuration);
            GUI.color = new Color(1f, 0.3f, 0.3f, alpha);
            GUI.Label(new Rect(cx - 100, cy - 20, 200, 30), $"BREAK! x{lastBreakCount}", breakStyle);
            GUI.color = Color.white;
            return;
        }

        // 活跃连锁显示
        if (comboTracker.IsComboActive)
        {
            // Chain 数
            GUI.Label(new Rect(cx - 80, cy - 30, 160, 40),
                      $"CHAIN x{comboTracker.ComboCount}", chainStyle);

            // 倍率
            GUI.Label(new Rect(cx - 80, cy + 10, 160, 25),
                      $"Multiplier: {comboTracker.CurrentMultiplier:F2}x", multiplierStyle);

            // 连锁窗口进度条
            float progress = comboTracker.ComboTimeRemaining / comboTracker.ComboWindowMax;
            Rect barBg = new Rect(cx - 60, cy + 38, 120, 8);
            Rect barFill = new Rect(cx - 60, cy + 38, 120 * progress, 8);

            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            GUI.DrawTexture(barBg, Texture2D.whiteTexture);
            GUI.color = progress > 0.3f ? Color.green : Color.red;
            GUI.DrawTexture(barFill, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 最近一击提示
            if (hitMessageTimer > 0f && !string.IsNullOrEmpty(lastHitMessage))
            {
                float hitAlpha = Mathf.Clamp01(hitMessageTimer / 1f);
                GUI.color = new Color(1f, 1f, 1f, hitAlpha);
                GUI.Label(new Rect(cx - 100, cy + 52, 200, 20), lastHitMessage, hitStyle);
                GUI.color = Color.white;
            }
        }
    }

    // ─────────────────────────────────────────────────────
    #region 事件处理

    private void HandleComboBreak(int count, float multiplier)
    {
        lastBreakCount = count;
        lastBreakMultiplier = multiplier;
        breakMessageTimer = breakMessageDuration;
    }

    private void HandleComboHit(int count, float hitMult, bool diffAnchor, bool diffProp)
    {
        if (count <= 1)
        {
            lastHitMessage = "Chain started!";
        }
        else if (diffAnchor && diffProp)
        {
            lastHitMessage = "Different anchor + prop!";
        }
        else if (diffAnchor)
        {
            lastHitMessage = "Different anchor!";
        }
        else if (diffProp)
        {
            lastHitMessage = "Different prop!";
        }
        else
        {
            lastHitMessage = "Same point... suspicion rising!";
        }

        hitMessageTimer = 1.2f;
    }

    #endregion
}
