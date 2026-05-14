using UnityEngine;

/// <summary>
/// Commit 5：拢宝撤离 HUD — 灰盒阶段的 OnGUI 显示。
///
/// 显示内容：
///   - Loot 状态（未收集 / 已收集）
///   - Escape 状态（未就绪 / 就绪 / 封锁中）
///   - 收集瞬间的闪烁提示
///
/// 非职责：
///   - 不修改 PhysicsMetrics、碰撞体、重力、MotionState。
///   - 不改 ControllablePropBase 的 Telegraph→Active→Cooldown 状态机。
/// </summary>
public class LootEscapeHUD : MonoBehaviour
{
    [Header("=== Commit 5 拢宝撤离 HUD ===")]
    [Tooltip("HUD 位置 X（屏幕比例 0-1）")]
    [SerializeField] private float hudX = 0.02f;

    [Tooltip("HUD 位置 Y（屏幕比例 0-1）")]
    [SerializeField] private float hudY = 0.55f;

    [Tooltip("收集闪烁持续时间")]
    [SerializeField] private float collectFlashDuration = 1.5f;

    [Tooltip("是否显示 HUD")]
    [SerializeField] private bool showHUD = true;

    // ── 状态 ──
    private float collectFlashTimer;
    private EscapeGate escapeGate;

    // ── 样式缓存 ──
    private bool stylesInitialized;
    private GUIStyle lootStyle;
    private GUIStyle escapeStyle;
    private GUIStyle flashStyle;

    private void Start()
    {
        escapeGate = FindObjectOfType<EscapeGate>();

        LootObjective.OnLootCollected += HandleLootCollected;
    }

    private void OnDestroy()
    {
        LootObjective.OnLootCollected -= HandleLootCollected;
    }

    private void Update()
    {
        if (collectFlashTimer > 0f)
        {
            collectFlashTimer -= Time.deltaTime;
        }
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        lootStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        escapeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft
        };

        flashStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        flashStyle.normal.textColor = Color.yellow;
    }

    private void OnGUI()
    {
        if (!showHUD) return;

        InitStylesIfNeeded();

        float x = Screen.width * hudX;
        float y = Screen.height * hudY;

        // Loot 状态
        if (LootObjective.IsLootCarried)
        {
            lootStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(x, y, 200, 22), "★ Loot: CARRIED", lootStyle);
        }
        else
        {
            lootStyle.normal.textColor = new Color(0.8f, 0.8f, 0.2f);
            GUI.Label(new Rect(x, y, 200, 22), "☆ Loot: NOT COLLECTED", lootStyle);
        }

        // Escape 状态
        y += 24f;
        if (escapeGate != null && escapeGate.IsGateLocked)
        {
            escapeStyle.normal.textColor = Color.red;
            GUI.Label(new Rect(x, y, 250, 20),
                      $"⊘ Gate: LOCKED ({escapeGate.PassDelayRemaining:F1}s)", escapeStyle);
        }
        else if (LootObjective.IsLootCarried)
        {
            escapeStyle.normal.textColor = Color.green;
            GUI.Label(new Rect(x, y, 200, 20), "→ Gate: READY TO ESCAPE", escapeStyle);
        }
        else
        {
            escapeStyle.normal.textColor = Color.gray;
            GUI.Label(new Rect(x, y, 200, 20), "→ Gate: Waiting for loot", escapeStyle);
        }

        // 收集闪烁
        if (collectFlashTimer > 0f)
        {
            float alpha = Mathf.Clamp01(collectFlashTimer / collectFlashDuration);
            GUI.color = new Color(1f, 1f, 0.3f, alpha);
            GUI.Label(new Rect(Screen.width * 0.5f - 100, Screen.height * 0.3f, 200, 35),
                      "LOOT COLLECTED!", flashStyle);
            GUI.color = Color.white;
        }
    }

    private void HandleLootCollected()
    {
        collectFlashTimer = collectFlashDuration;
    }
}
