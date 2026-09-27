using System.Collections;
using UnityEngine;

/// <summary>
/// S196：每回合的随机事件（涌现来源之一，宪法：重玩变化来自状态，不来自地图平移）。
///   - 以 preCollapsedWallChance 的概率，本回合开局时某一面裂墙**已经塌了**（像是"上一个越狱犯留下的洞"）——
///     这一局多一条秘密路线，马里奥和你都要重新判断；
///   - 选哪一面由回合种子决定（写进试玩记录，可复现）。
/// 规则：裂墙打开只会多开路（死局检查已按"全部打开"验证过），不会制造死局（H1/H9）。
/// </summary>
public class Step1HakoniwaEvents : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    private GameManager manager;
    public string LastEvent { get; private set; } = "";
    /// <summary>S199：全场警报灯亮着（你的伪装更容易被看穿：马里奥看"像道具的东西"时起疑更快）。</summary>
    public static bool AlarmOn { get; private set; }
    private float alarmStart = -1f, alarmEnd = -1f, roundTime;

    /// <summary>纯逻辑：本回合警报在第几秒开始（-1 = 这局没有警报）。</summary>
    public static float AlarmAt(int seed, float chance, float earliest, float latest)
    {
        if (chance <= 0f) return -1f;
        var rng = new System.Random(seed * 131 + 11);
        if (rng.NextDouble() >= chance) return -1f;
        return earliest + (float)rng.NextDouble() * Mathf.Max(0f, latest - earliest);
    }

    private void Update()
    {
        if (manager != null && manager.CurrentState != GameState.Playing) return;
        roundTime += Time.deltaTime;
        bool on = alarmStart >= 0f && roundTime >= alarmStart && roundTime < alarmEnd;
        if (on && !AlarmOn) Step1Hint.Show(Step1Text.AlarmOn, 2.5f);
        if (!on && AlarmOn) Step1Hint.Show(Step1Text.AlarmOff, 1.5f);
        AlarmOn = on;
    }

    private void OnGUI()
    {
        if (!AlarmOn || Step1HandsOffCheck.IsRunning) return;
        float w = Step1Gui.Begin();
        var c = GUI.color;
        GUI.color = new Color(1f, 0.15f, 0.1f, 0.10f + Mathf.PingPong(Time.time * 1.5f, 0.12f));
        GUI.DrawTexture(new Rect(0, 0, w, Step1Gui.VirtualHeight), Texture2D.whiteTexture);
        GUI.color = c;
        GUI.Label(new Rect(w * 0.5f - 200, 60, 400, 40), "<color=#FF5A4A><b>🚨 警报 ALARM</b></color>", Step1Gui.Text(28, TextAnchor.MiddleCenter, false));
    }

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += Roll;
        Roll();
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= Roll; }

    private void Roll() { StartCoroutine(RollNextFrame()); } // 等回合复位（OnLevelReset）做完

    /// <summary>纯逻辑：给定种子与裂墙数量，本回合塌哪一面（-1 = 不塌）。</summary>
    public static int Pick(int seed, int wallCount, float chance)
    {
        if (wallCount <= 0 || chance <= 0f) return -1;
        var rng = new System.Random(seed * 7919 + 17);
        return rng.NextDouble() < chance ? rng.Next(wallCount) : -1;
    }

    private IEnumerator RollNextFrame()
    {
        yield return null;
        LastEvent = "";
        roundTime = 0f; AlarmOn = false;
        var d0 = FindObjectOfType<MarioMindDriver>();
        int s0 = d0 != null ? d0.RoundSeed : Random.Range(0, 100000);
        alarmStart = AlarmAt(s0, tuning.alarmChance, tuning.alarmEarliest, tuning.alarmLatest);
        alarmEnd = alarmStart + tuning.alarmSeconds;
        var walls = FindObjectsOfType<CrackedWall>();
        System.Array.Sort(walls, (a, b) => a.transform.position.x != b.transform.position.x
            ? a.transform.position.x.CompareTo(b.transform.position.x) : a.transform.position.y.CompareTo(b.transform.position.y));
        var driver = FindObjectOfType<MarioMindDriver>();
        int seed = driver != null ? driver.RoundSeed : Random.Range(0, 100000);
        int pick = Pick(seed, walls.Length, tuning.preCollapsedWallChance);
        if (pick < 0) yield break;
        walls[pick].BreakSilently();
        LastEvent = $"wall@{walls[pick].transform.position.x:F0},{walls[pick].transform.position.y:F0}";
        Step1Hint.Show(Step1Text.PreCollapsedWall, 3f);
    }
}
