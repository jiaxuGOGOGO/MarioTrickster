using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S202：连锁录像回放——**完美连锁**（一次连锁接满 ≥3 环）或**大连招**（≥ replayMinCombo 段）之后，
/// 把最近 replaySeconds 秒用慢动作"幽灵影像"重播一遍（格斗游戏/体育转播的 instant replay；只借规则）。
///
/// 做法（不碰物理、不改胜负、不会卡死）：
///   - 平时每帧把马里奥、你、以及被触发的机关位置记进一个环形缓冲（很小：~5 秒 × 60 帧）；
///   - 触发回放 → 游戏暂停（timeScale = 0，和暂停键一样）→ 在原位置画半透明"影子"按 replaySpeed 倍速重放，
///     屏幕上方"REPLAY ⛓ 完美连锁"、边框变暗、机关触发瞬间闪白；
///   - 结束（或按任意技能键/空格跳过）→ 恢复原来的 timeScale，游戏从暂停处继续。
/// 规则：H4——回放只给玩家看，马里奥 AI 暂停期间不更新；H9——有最长时长，任何按键可跳过；自动检查（H10）期间不回放。
/// 数值：replayEnabled / replaySeconds / replaySpeed / replayMinCombo / replayCooldown（RushMarioTuning）。
/// </summary>
public class ChainReplay : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    public struct Frame { public float t; public Vector2 mario, figure; public bool marioOk, figureOk; }
    public struct Mark { public float t; public Vector2 at; public string label; }

    private readonly List<Frame> frames = new List<Frame>();
    private readonly List<Mark> marks = new List<Mark>();
    private Transform mario, figure;
    private SpriteRenderer marioSr, figureSr;
    private bool playing;
    private float playT, playEnd, restoreScale = 1f, cooldownUntil;
    private string title = "";
    private SpriteRenderer ghostMario, ghostFigure;
    private int flashFrames;
    private Step1Combo combo;
    private GameManager manager;

    public static bool Playing { get; private set; }
    public static event System.Action<string> Started;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    /// <summary>纯逻辑：该不该回放（完美连锁环数 / 连招段数）。</summary>
    public static bool ShouldReplay(bool enabled, int chainSteps, int combo, int minCombo, bool handsOff, bool onCooldown)
        => enabled && !handsOff && !onCooldown && (chainSteps >= 3 || (minCombo > 0 && combo >= minCombo));

    /// <summary>纯逻辑：环形缓冲只留最近 keep 秒。</summary>
    public static int TrimIndex(IList<Frame> fs, float now, float keep)
    {
        int i = 0; while (i < fs.Count && fs[i].t < now - keep) i++; return i;
    }

    /// <summary>纯逻辑：回放时刻 playT 对应的两帧插值。</summary>
    public static Vector2 Sample(IList<Frame> fs, float t, bool marioSide)
    {
        if (fs.Count == 0) return Vector2.zero;
        if (t <= fs[0].t) return marioSide ? fs[0].mario : fs[0].figure;
        for (int i = 1; i < fs.Count; i++)
            if (fs[i].t >= t)
            {
                float k = Mathf.InverseLerp(fs[i - 1].t, fs[i].t, t);
                return marioSide ? Vector2.Lerp(fs[i - 1].mario, fs[i].mario, k) : Vector2.Lerp(fs[i - 1].figure, fs[i].figure, k);
            }
        var last = fs[fs.Count - 1];
        return marioSide ? last.mario : last.figure;
    }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        var m = FindObjectOfType<MarioController>(); if (m != null) { mario = m.transform; marioSr = m.GetComponentInChildren<SpriteRenderer>(); }
        var f = FindObjectOfType<TricksterController>(); if (f != null) { figure = f.transform; figureSr = f.GetComponentInChildren<SpriteRenderer>(); }
        combo = FindObjectOfType<Step1Combo>();
        if (combo != null) combo.ComboRegistered += HandleCombo;
        ChainPlan.LinkFired += HandleLink;
        ChainPlan.PerfectChain += HandlePerfect;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += Clear;
    }

    private void OnDestroy()
    {
        if (combo != null) combo.ComboRegistered -= HandleCombo;
        ChainPlan.LinkFired -= HandleLink;
        ChainPlan.PerfectChain -= HandlePerfect;
        if (manager != null) manager.OnRoundStart -= Clear;
        if (playing) Stop();
    }

    private void Clear() { frames.Clear(); marks.Clear(); if (playing) Stop(); }

    private void HandleLink(IControllableProp p) { var c = p as Component; if (c != null) marks.Add(new Mark { t = Time.time, at = c.transform.position, label = "⛓" }); }

    private void HandleCombo(int n, string kind)
    {
        if (mario != null) marks.Add(new Mark { t = Time.time, at = mario.position, label = n >= 2 ? $"{n}HIT" : "HIT" });
        if (tuning != null && ShouldReplay(tuning.replayEnabled, 0, n, tuning.replayMinCombo, Step1HandsOffCheck.IsRunning, Time.unscaledTime < cooldownUntil))
            Begin(string.Format(Step1Text.ReplayCombo, n));
    }

    private void HandlePerfect(int steps)
    {
        if (tuning != null && ShouldReplay(tuning.replayEnabled, steps, 0, 0, Step1HandsOffCheck.IsRunning, Time.unscaledTime < cooldownUntil))
            Begin(string.Format(Step1Text.ReplayChain, steps));
    }

    private void Update()
    {
        if (playing) { Play(); return; }
        if (mario == null || tuning == null || Time.timeScale <= 0f) return;
        frames.Add(new Frame
        {
            t = Time.time,
            mario = mario.position, marioOk = mario.gameObject.activeInHierarchy,
            figure = figure != null ? (Vector2)figure.position : Vector2.zero, figureOk = figure != null && figure.gameObject.activeInHierarchy
        });
        int cut = TrimIndex(frames, Time.time, tuning.replaySeconds + 0.5f);
        if (cut > 0) frames.RemoveRange(0, cut);
        marks.RemoveAll(k => k.t < Time.time - tuning.replaySeconds - 0.5f);
    }

    private void Begin(string text)
    {
        if (playing || frames.Count < 10) return;
        playing = Playing = true; title = text;
        playEnd = frames[frames.Count - 1].t;
        playT = Mathf.Max(frames[0].t, playEnd - tuning.replaySeconds);
        restoreScale = Time.timeScale > 0.9f ? Time.timeScale : 1f; // 顿帧中触发：恢复到正常速度而不是顿帧速度
        Time.timeScale = 0f;
        ghostMario = MakeGhost("ReplayMario", marioSr, new Color(1f, 0.55f, 0.45f, 0.75f));
        ghostFigure = MakeGhost("ReplayYou", figureSr, new Color(0.55f, 0.8f, 1f, 0.75f));
        Started?.Invoke(text);
    }

    private SpriteRenderer MakeGhost(string n, SpriteRenderer src, Color c)
    {
        var go = new GameObject(n);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = src != null && src.sprite != null ? src.sprite : Step1Sprites.Square;
        r.color = c; r.sortingOrder = 250;
        go.transform.localScale = src != null ? src.transform.lossyScale : Vector3.one * 0.9f;
        return r;
    }

    private void Play()
    {
        float before = playT;
        playT += Time.unscaledDeltaTime * Mathf.Clamp(tuning.replaySpeed, 0.1f, 1f);
        if (ghostMario != null) ghostMario.transform.position = (Vector3)Sample(frames, playT, true) + Vector3.up * 0.0f;
        if (ghostFigure != null) { ghostFigure.transform.position = Sample(frames, playT, false); ghostFigure.enabled = figure != null; }
        foreach (var k in marks) if (k.t > before && k.t <= playT) flashFrames = 6;
        bool skip = Step1Keys.Down(KeyCode.Space) || Step1Keys.Down(KeyCode.B) || Step1Keys.Down(KeyCode.F) || Step1Keys.Down(KeyCode.G) || Step1Keys.Down(KeyCode.T);
        if (playT >= playEnd || skip) Stop();
    }

    private void Stop()
    {
        playing = Playing = false;
        if (ghostMario != null) Destroy(ghostMario.gameObject);
        if (ghostFigure != null) Destroy(ghostFigure.gameObject);
        if (Time.timeScale <= 0f) Time.timeScale = restoreScale;
        cooldownUntil = Time.unscaledTime + (tuning != null ? tuning.replayCooldown : 10f);
    }

    private void OnGUI()
    {
        if (!playing) return;
        float w = Step1Gui.Begin(), h = Step1Gui.VirtualHeight;
        var c = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(0, 0, w, 70), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, h - 70, w, 70), Texture2D.whiteTexture);
        if (flashFrames > 0) { flashFrames--; GUI.color = new Color(1f, 1f, 1f, 0.25f); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); }
        GUI.color = c;
        GUI.Label(new Rect(0, 10, w, 50), $"<color=#FFD54F><b>▶ REPLAY  {title}</b></color>", Step1Gui.Text(34, TextAnchor.MiddleCenter, false));
        float pct = Mathf.InverseLerp(playEnd - tuning.replaySeconds, playEnd, playT);
        GUI.Label(new Rect(0, h - 60, w, 50), $"<color=#CCCCCC>慢动作 ×{tuning.replaySpeed:0.##}   {Mathf.RoundToInt(pct * 100)}%   空格跳过  Space = skip</color>", Step1Gui.Text(20, TextAnchor.MiddleCenter, false));
        // 机关触发点标记
        if (Camera.main == null) return;
        float scale = Mathf.Max(0.1f, Screen.height / h);
        foreach (var k in marks)
        {
            if (k.t > playT) continue;
            Vector3 sp = Camera.main.WorldToScreenPoint((Vector3)k.at + Vector3.up * 1.2f);
            if (sp.z < 0f) continue;
            float age = playT - k.t;
            if (age > 1.5f) continue;
            GUI.Label(new Rect(sp.x / scale - 50, (Screen.height - sp.y) / scale - 20, 100, 40), $"<color=#FFB74D><b>{k.label}</b></color>", Step1Gui.Text(26, TextAnchor.MiddleCenter, false));
        }
    }
}
