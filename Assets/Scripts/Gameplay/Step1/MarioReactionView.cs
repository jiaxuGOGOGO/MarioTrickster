using UnityEngine;

/// <summary>
/// S223：把 MarioReaction 表演出来（纯画面）。挂在马里奥身上，由 Step1Combo.ComboRegistered（已有的"坑到了"事件）驱动。
/// 只动外观节点 MarioController.visualTransform 的旋转/位置/缩放，演完原样还回去；碰撞体、速度、晕眩、AI 都不碰（H4/H9）。
/// 头顶台词由 MarioMindLabel 读 CurrentLine 显示（同一个位置，不另起一行字）。
/// </summary>
public class MarioReactionView : MonoBehaviour
{
    public const string ResourceName = "MarioReactions";

    private static MarioReaction.Beat[] table;
    public static void Reload() { table = null; } // S234：台词编辑器保存后
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetTable() { table = null; }
    public static MarioReaction.Beat[] Table
    {
        get
        {
            if (table != null) return table;
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null)
            {
                table = MarioReaction.Parse(asset.text, out string err);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning("[MarioReactions] " + err + "（用默认值补上）");
            }
            else table = (MarioReaction.Beat[])MarioReaction.Default.Clone();
            var mine = Resources.Load<TextAsset>(MarioReaction.UserResourceName); // S234：你改的字
            if (mine != null) { table = MarioReaction.ApplyUserLines(table, mine.text, out string ue); MarioReaction.ApplyUserAgain(mine.text); if (ue.Length > 0) Debug.LogWarning("[MyMarioReactions] " + ue); }
            else MarioReaction.ApplyUserAgain("");
            return table;
        }
    }

    private MarioController mario;
    private Transform visual;
    private Vector3 baseScale, basePos;
    private Quaternion baseRot;
    private Step1Combo combo;
    private MarioReaction.Beat beat;
    private float t = -1f;
    private int nth = 1;
    private readonly System.Collections.Generic.Dictionary<string, int> timesThisRound = new System.Collections.Generic.Dictionary<string, int>(); // S231：同一种坑本局第几次

    public bool Playing => t >= 0f && t < beat.Total;
    public string CurrentLine => Playing ? MarioReaction.Line(beat, nth) : "";

    private void Start()
    {
        mario = GetComponent<MarioController>();
        visual = mario != null && mario.visualTransform != null ? mario.visualTransform : null;
        if (visual == transform) visual = null; // 外观和身体是同一个节点时不演（不能动碰撞体）
        if (visual != null) { baseScale = visual.localScale; basePos = visual.localPosition; baseRot = visual.localRotation; }
        combo = FindObjectOfType<Step1Combo>();
        if (combo != null) combo.ComboRegistered += HandleCombo;
        if (GameManager.Instance != null) GameManager.Instance.OnRoundStart += NewRound;
    }

    private void OnDestroy()
    {
        if (combo != null) combo.ComboRegistered -= HandleCombo;
        if (GameManager.Instance != null) GameManager.Instance.OnRoundStart -= NewRound;
    }

    private void HandleCombo(int n, string kind)
    {
        if (!MarioReaction.TryGet(Table, kind, out var next)) return;
        bool chaining = Playing;
        beat = next;
        timesThisRound.TryGetValue(kind, out int seen); nth = seen + 1; timesThisRound[kind] = nth;
        t = MarioReaction.StartTime(beat, chaining);
    }

    private void NewRound() { timesThisRound.Clear(); nth = 1; Stop(); }

    public void Stop()
    {
        t = -1f;
        if (visual != null) { visual.localScale = baseScale; visual.localPosition = basePos; visual.localRotation = baseRot; }
    }

    private void LateUpdate()
    {
        if (t < 0f || visual == null) return;
        t += Time.deltaTime;
        if (t >= beat.Total) { Stop(); return; }
        var f = MarioReaction.Sample(beat, t, mario != null && !mario.IsFacingRight ? -1f : 1f);
        visual.localScale = new Vector3(baseScale.x * f.sx, baseScale.y * f.sy, baseScale.z);
        // 绕脚底转（外观节点的原点在身体中心，抬高半个身位补偿）
        float half = 0.5f * Mathf.Abs(baseScale.y);
        float rad = f.rotDeg * Mathf.Deg2Rad;
        var pivot = new Vector3(-Mathf.Sin(rad) * half, half - Mathf.Cos(rad) * half, 0f);
        visual.localPosition = basePos + new Vector3(f.dx, f.dy, 0f) - pivot;
        visual.localRotation = baseRot * Quaternion.Euler(0f, 0f, f.rotDeg);
    }
}
