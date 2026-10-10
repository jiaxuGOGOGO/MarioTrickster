using UnityEngine;

/// <summary>
/// S225（樱井政博"ノーリアクションを排除せよ"——按了就要有反应）：按 L / P 没成功时，屏幕底部说清楚为什么。
/// 原来：L 的失败原因只发给旧界面（GameUI / GlobalGameUICanvas），第 1 步房间把它们关了 → 按了什么都没有；
///       P 在冷却 / 刚被发现 / 缩小时直接 return，也什么都没有。
/// 这里只"听"按键结果并显示文字：不调用任何技能、不改冷却/能量/判定（纯表现）。由 Step1Combo 运行时自动挂上。
/// </summary>
public class Step1FailFeedback : MonoBehaviour
{
    private TricksterController self;
    private DisguiseSystem disguise;
    private float lastShown = -10f;
    private bool wasDisguised; // 上一帧是否伪装着（取消伪装会开始冷却，不能误报"冷却中"）

    private void Start()
    {
        self = FindObjectOfType<TricksterController>();
        if (self == null) return;
        disguise = self.GetComponent<DisguiseSystem>();
        self.OnAbilityFailed += OnAbilityFailed;
        if (disguise != null) disguise.OnDisguiseFailed += OnAbilityFailed;
    }

    private void OnDestroy()
    {
        if (self != null) self.OnAbilityFailed -= OnAbilityFailed;
        if (disguise != null) disguise.OnDisguiseFailed -= OnAbilityFailed;
    }

    private void OnAbilityFailed(string reason)
    {
        if (Step1HandsOffCheck.IsRunning) return;
        Step1Hint.Show(Step1Text.AbilityFailZh(reason)); lastShown = Time.unscaledTime;
    }

    private void LateUpdate() { if (self != null) wasDisguised = self.IsDisguised; }

    private void Update()
    {
        if (self == null || disguise == null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f) return;
        if (!Step1Keys.Down(KeyCode.P)) return;
        // P 只在"本来是要变身"时才可能静默失败（已经伪装着按 P = 取消伪装，总会成功）
        if (wasDisguised || self.IsDisguised) return;
        var ab = self.AbilitySystem;
        bool caught = ab != null && (ab.PossessionState == TricksterPossessionState.Revealed || ab.PossessionState == TricksterPossessionState.Escaping);
        string why = Step1Text.DisguiseFailZh(TricksterKit.BlocksPranks, caught, disguise.CooldownRemaining, false);
        if (why != null && Time.unscaledTime - lastShown > 0.2f) Step1Hint.Show(why);
    }
}
