using UnityEngine;

/// <summary>
/// Commit 6：扫描波 HUD 状态桥接。
///
/// 旧灰盒 OnGUI 显示已迁移到 GlobalGameUICanvas（UGUI）。本组件仅保留扫描波命中反馈的
/// 事件订阅与状态缓存职责，避免运行时继续执行 IMGUI 绘制。
/// </summary>
public class ScanWaveHUD : MonoBehaviour
{
    [Header("=== Commit 6 扫描波 HUD 状态桥接 ===")]
    [Tooltip("扫描波命中后屏幕反馈的持续时间")]
    [SerializeField] private float hitFeedbackDuration = 1.5f;

    // ── 引用 ──
    private AlarmCrisisDirector crisisDirector;

    // ── 命中反馈状态（供调试/兼容旧组件序列化语义）──
    private float hitFeedbackTimer;
    private string hitFeedbackTitle = string.Empty;
    private string hitFeedbackDetail = string.Empty;
    private bool hitFeedbackWasReveal;

    private void Start()
    {
        crisisDirector = FindObjectOfType<AlarmCrisisDirector>();
        if (crisisDirector != null)
        {
            crisisDirector.OnAnchorScanned += HandleAnchorScanned;
        }
    }

    private void OnDestroy()
    {
        if (crisisDirector != null)
        {
            crisisDirector.OnAnchorScanned -= HandleAnchorScanned;
        }
    }

    private void Update()
    {
        if (hitFeedbackTimer > 0f)
        {
            hitFeedbackTimer -= Time.deltaTime;
        }
    }

    private void HandleAnchorScanned(PossessionAnchor anchor, bool wasRevealed)
    {
        hitFeedbackWasReveal = wasRevealed;
        hitFeedbackTitle = wasRevealed ? "SCAN HIT: TRICKSTER REVEALED" : "SCAN HIT: EVIDENCE AMPLIFIED";
        string anchorId = anchor != null ? anchor.AnchorId : "Unknown Anchor";
        hitFeedbackDetail = wasRevealed ? $"Anchor {anchorId} forced Trickster into Revealed/Escaping." : $"Anchor {anchorId} evidence increased.";
        hitFeedbackTimer = hitFeedbackDuration;
    }
}
