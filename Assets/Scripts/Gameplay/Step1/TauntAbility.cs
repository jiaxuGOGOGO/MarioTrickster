using UnityEngine;

/// <summary>
/// S200：挑衅（捣蛋者 T 键）——"以身入局"：故意发出声音把马里奥引过来，让他追着你跑进你布置好的连锁。
/// 规则：必须**现形**（伪装时不能挑衅）；每回合 tauntUses 次，冷却 tauntCooldown 秒。
/// 马里奥只"听见一个位置"（与砸墙/爆炸同一个 NoteNoise 通道，H4），他会转头、过来查看；看见你本人才会追。
/// 代价：你把自己的位置交给了他；被抓扣命。
/// </summary>
public class TauntAbility : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private TricksterController self;
    private GameManager manager;
    private int left; private float cooldown, bubble;
    public static event System.Action<Vector2> Taunted;
    public int TauntsLeft => left;
    public float Cooldown => cooldown;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    public static bool CanTaunt(bool disguised, int left, float cooldown) => !disguised && left > 0 && cooldown <= 0f;

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        self = GetComponent<TricksterController>();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        ResetRound();
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= ResetRound; }
    private void ResetRound() { left = tuning.tauntUses; cooldown = 0f; bubble = 0f; }

    private void Update()
    {
        if (Step1QuickTest.NoLimits) { left = Mathf.Max(left, 1); cooldown = 0f; } // S236：F9 测试
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        if (bubble > 0f) bubble -= Time.deltaTime;
        if (self == null || Step1HandsOffCheck.IsRunning || Step1PlaytestLog.IsTyping || Step1Screen.HelpOpen || Time.timeScale <= 0f) return;
        if (PranksterCannon.TricksterSeated) return; // S240：坐在炮里，其他技能键不生效
        if (TricksterBurrow.BodyBusy) return; // S241：遁地 / 摆荡中，其他技能键不生效
        if (!Step1Keys.Down(KeyCode.T)) return;
        if (!CanTaunt(self.IsDisguised, left, cooldown))
        {
            Step1Hint.Show(self.IsDisguised ? Step1Text.TauntNeedUndisguise : left <= 0 ? Step1Text.TauntNone : Step1Text.TauntCooldown);
            return;
        }
        left--; cooldown = tuning.tauntCooldown; bubble = 1.2f;
        Taunted?.Invoke(transform.position);
        Step1Hint.Show(string.Format(Step1Text.TauntDone, left));
    }

    private void OnGUI()
    {
        if (bubble <= 0f || Step1HandsOffCheck.IsRunning || Camera.main == null) return;
        Step1Gui.Begin();
        float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.4f);
        if (sp.z < 0f) return;
        var at = new Vector2(sp.x / scale, (Screen.height - sp.y) / scale);
        GUI.Label(new Rect(at.x - 110, at.y - 25, 220, 50), "<color=#81D4FA><b>嘿！来抓我呀~</b></color>", Step1Gui.Text(24, TextAnchor.MiddleCenter, false));
    }
}
