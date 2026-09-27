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
