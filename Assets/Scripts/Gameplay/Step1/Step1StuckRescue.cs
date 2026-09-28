using UnityEngine;

/// <summary>
/// S189：运行时防卡死救援（宪法 H9 的最后一道保险）。
/// 编辑器里的死局分析只能查"静态布局"，实际物理（击退、塌桥、封路墙挤压、同时触发几个机关）仍可能让马里奥卡住。
/// 规则：游戏进行中、马里奥不在"被坑晕 / 起步等待"状态，却在 stuckSeconds 秒内几乎没动 → 判定卡住：
///   1. 屏幕提示"马里奥卡住了，已救出"，并写入试玩记录（stuck_rescues 列），方便你告诉我是哪里的布局有问题；
///   2. 把他挪到最近的一个"能到达出口"的安全站位（来自 LevelDeadlockAnalyzer 同一套可达性，不是随便传送）。
/// 不给马里奥任何关于捣蛋者的信息（H4）；不改变胜负判定。卡死本身就是布局 bug，救援只是让测试能继续。
/// </summary>
public class Step1StuckRescue : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    [SerializeField] private string[] roomGrid = new string[0];

    private MarioMindDriver driver;
    private MarioController mario;
    private GameManager manager;
    private Vector2 anchor;
    private float still;
    private float flashUntil;
    private System.Collections.Generic.HashSet<int> safeCells;
    private string safeSource;

    public int RescuesThisRound { get; private set; }
    public Vector2 LastStuckAt { get; private set; }

    public void Configure(MarioMindTuningSO t, string[] grid) { tuning = t; roomGrid = grid ?? new string[0]; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        driver = FindObjectOfType<MarioMindDriver>();
        mario = driver != null ? driver.GetComponent<MarioController>() : null;
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += ResetRound;
        ResetRound();
        SafeCells(); // S195：开局就算好（高楼房间第一次救援时不再卡一下）
    }

    private void OnDestroy() { if (manager != null) manager.OnRoundStart -= ResetRound; }

    private float bestDist; private bool hasBest;

    /// <summary>纯逻辑（测试用）：给定一串"到目标距离"采样与时间间隔，多少秒后判定卡住（-1 = 不卡）。</summary>
    public static float SecondsUntilStuck(float[] distances, float dt, float stuckSeconds, float progressCells)
    {
        float best = float.MaxValue, still = 0f;
        for (int i = 0; i < distances.Length; i++)
        {
            if (distances[i] < best - progressCells) { best = distances[i]; still = 0f; continue; }
            still += dt;
            if (still >= stuckSeconds) return i * dt;
        }
        return -1f;
    }

    private void ResetRound()
    {
        RescuesThisRound = 0; still = 0f; hasBest = false;
        if (mario != null) anchor = mario.transform.position;
    }

    private void Update()
    {
        if (mario == null || manager == null || manager.CurrentState != GameState.Playing) { still = 0f; return; }
        // 只在"赶路"（去拿宝/回出口，一定有目标）时判定；起疑、查看、找人时站着不动是正常表演，不算卡住。
        if (driver.IsWaitingToStart || driver.Mind.IsStunned || driver.Mind.IsGlancing || driver.Mind.Dodging || ChainReplay.Playing || driver.Mind.State != MarioMindState.Running)
        { still = 0f; hasBest = false; anchor = mario.transform.position; return; }
        Vector2 pos = mario.transform.position;
        // S198：原来"离锚点超过 0.6 格就重置" → 来回跳（每次移动 1–2 格）永远不算卡住（用户截图：出口下方来回跳）。
        // 改为"没有进展"：到目标的距离 stuckSeconds 秒内没有缩短 stuckProgressCells 格 = 卡住（来回跳、原地跳都算）。
        Vector2? goal = driver.CurrentGoal();
        if (goal.HasValue)
        {
            float d = Vector2.Distance(pos, goal.Value);
            if (!hasBest || d < bestDist - tuning.stuckProgressCells) { bestDist = d; hasBest = true; still = 0f; anchor = pos; return; }
        }
        else if ((pos - anchor).sqrMagnitude > tuning.stuckMoveEpsilon * tuning.stuckMoveEpsilon) { anchor = pos; still = 0f; return; }
        still += Time.deltaTime;
        if (still < tuning.stuckSeconds) return;
        hasBest = false;
        Rescue(pos);
    }

    private void Rescue(Vector2 pos)
    {
        still = 0f;
        RescuesThisRound++;
        LastStuckAt = pos;
        // S209：先沿"去目标的路线"往前放（越过卡住的地方）；原来只放到最近的安全格 = 常常就是卡住的那一格旁边 → 又卡住，循环（用户截图）
        Vector2? goal = driver.CurrentGoal();
        Vector2? ahead = goal.HasValue ? RescueAlongRoute(roomGrid, pos, goal.Value, SafeCells()) : null;
        Vector2 target = ahead ?? NearestSafe(pos);
        mario.transform.position = target;
        var rb = mario.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
        anchor = target;
        hasBest = false;
        flashUntil = Time.time + 2.5f;
        Debug.LogWarning($"[Step1 H9] Mario stuck at ({pos.x:F1},{pos.y:F1}) for {tuning.stuckSeconds}s -> rescued to ({target.x:F1},{target.y:F1}). This is a layout bug; please report the spot.");
    }

    /// <summary>
    /// S209 纯逻辑：沿马里奥去 goal 的规划路线，找第一个离卡住点 ≥ 2 格、而且"能到出口"的站位（safe = null 时不过滤）。
    /// 找不到返回 null（用最近安全格兜底）。只用地形与马里奥自己的目标（H4）。
    /// </summary>
    public static Vector2? RescueAlongRoute(string[] grid, Vector2 pos, Vector2 goal, System.Collections.Generic.ICollection<int> safe)
    {
        if (grid == null || grid.Length == 0) return null;
        var reg = AsciiElementRegistry.GetDefault();
        var solid = reg.GetSolidChars(); var hazard = reg.GetHazardChars();
        var from = new LevelPathPlanner.Cell(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
        foreach (int dx in new[] { 0, -1, 1 })
        {
            var c = LevelPathPlanner.Settle(grid, new LevelPathPlanner.Cell(from.x + dx, from.y), solid, hazard);
            if (c.x >= 0) { from = c; break; }
        }
        var path = LevelPathPlanner.Path(grid, from, new LevelPathPlanner.Cell(Mathf.RoundToInt(goal.x), Mathf.RoundToInt(goal.y)));
        if (path == null || path.Count < 2) return null;
        for (int i = 1; i < path.Count; i++)
        {
            var c = path[i];
            if (Mathf.Abs(c.x - pos.x) + Mathf.Abs(c.y - pos.y) < 2f && i < path.Count - 1) continue;
            if (safe != null && safe.Count > 0 && !safe.Contains(LevelReachabilityAnalyzer.CellKey(c.x, c.y))) continue;
            return new Vector2(c.x, c.y);
        }
        return null;
    }

    /// <summary>最近的"能到出口"的站位中心（世界坐标 = 格坐标，角色站在格内）。没有分析数据时原地抬高半格。</summary>
    public Vector2 NearestSafe(Vector2 pos)
    {
        var cells = SafeCells();
        if (cells == null || cells.Count == 0) return pos + Vector2.up * 0.5f;
        float best = float.MaxValue; Vector2 bestPos = pos;
        foreach (int key in cells)
        {
            var c = new Vector2(key / 100000, key % 100000);
            float d = (c - pos).sqrMagnitude;
            if (d > 0.25f && d < best) { best = d; bestPos = c; }
        }
        return bestPos;
    }

    private System.Collections.Generic.HashSet<int> SafeCells()
    {
        if (roomGrid == null || roomGrid.Length == 0) return null;
        string text = string.Join("\n", roomGrid);
        if (safeCells != null && safeSource == text) return safeCells;
        safeSource = text;
        safeCells = new System.Collections.Generic.HashSet<int>();
        int exitX = -1, exitY = -1;
        for (int row = 0; row < roomGrid.Length; row++)
        {
            int col = roomGrid[row].IndexOf('G');
            if (col >= 0) { exitX = col; exitY = roomGrid.Length - 1 - row; }
        }
        if (exitX < 0) return safeCells;
        // 能到达出口的格 = 从出口反查代价太高；直接用"从出口出发可达"的格（地面大多是双向的），再过滤"从该格能到出口"。
        // S195 性能（高楼房间 300+ 格时原来要 ~0.4s）：同 S192 的严格剪枝——
        // 若 B ∈ Reach(A)：A 能到出口不代表 B 能，但 A 到不了出口 ⇒ B 也到不了；B 能到 A 且 A 能到出口 ⇒ 先不推断，只剪"到不了"的一侧。
        int exitKey = LevelReachabilityAnalyzer.CellKey(exitX, exitY);
        var dead = new System.Collections.Generic.HashSet<int>();
        foreach (int key in LevelReachabilityAnalyzer.ReachableFrom(text, exitX, exitY))
        {
            if (dead.Contains(key)) continue;
            int x = key / 100000, y = key % 100000;
            var reach = LevelReachabilityAnalyzer.ReachableFrom(text, x, y);
            if (reach.Contains(exitKey)) safeCells.Add(key);
            else dead.UnionWith(reach);
        }
        return safeCells;
    }

    private void OnGUI()
    {
        if (Time.time > flashUntil) return;
        float w = Step1Gui.Begin();
        var r = new Rect(w * 0.5f - 330f, 140f, 660f, 70f);
        Step1Gui.Panel(r, 0.85f);
        GUI.Label(r, "<color=#FFB347><b>马里奥卡住了，已把他挪到最近的安全位置</b>\nMario got stuck — moved to the nearest safe spot (layout bug, please report)</color>",
            Step1Gui.Text(20, TextAnchor.MiddleCenter));
    }
}
