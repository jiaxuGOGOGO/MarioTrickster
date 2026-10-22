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
    private Vector2 LastRescueFrom = new Vector2(-999f, -999f);

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
    // S240：总兜底——到目标没进展的总时间（晕、东张西望、躲闪都照算；只有不在赶路时清零）
    private float hardStill;

    /// <summary>S240：卡住记录文件（每次救援追加一行），工坊"检查轨迹"读它用红叉标出卡点。</summary>
    public const string StuckLogFile = "step1_stuck.txt";

    /// <summary>
    /// S240 纯逻辑：一帧的卡住计时怎么走。
    ///   notRunning（起疑/追人/找人/起步等待）→ 两个计时都清零（他本来就该停）；
    ///   paused（被晕 / 东张西望 / 躲闪 / 回放）→ 普通计时**暂停不清零**（以前清零 = 坑底被火反复烧就永远凑不满 6 秒），总兜底照走；
    ///   progressed（到目标近了 progressCells 格）→ 两个都清零；其余 → 两个都 +dt。
    /// 返回 (still, hardStill, 该救了吗)。
    /// </summary>
    public static (float still, float hard, bool rescue) Tick(float still, float hard, float dt, bool notRunning, bool paused, bool progressed, float stuckSeconds, float hardCap)
    {
        if (notRunning || progressed) return (0f, 0f, false);
        hard += dt;
        if (!paused) still += dt;
        bool go = still >= stuckSeconds || (hardCap > 0f && hard >= hardCap);
        return go ? (0f, 0f, true) : (still, hard, false);
    }

    /// <summary>S240 纯逻辑：一行卡住记录 "R,房间指纹,x,y,原因"。</summary>
    public static string StuckLine(string roomHash, Vector2 at, string why) =>
        "R," + (roomHash ?? "") + "," + Mathf.RoundToInt(at.x) + "," + Mathf.RoundToInt(at.y) + "," + (why ?? "").Replace(",", ";");

    /// <summary>S240 纯逻辑：读卡住记录，只要这张图的（指纹对得上），返回 格→次数（格键 = x*1000+y，与轨迹文件一致）。</summary>
    public static System.Collections.Generic.Dictionary<int, int> ParseStuckLog(string text, string roomHash)
    {
        var res = new System.Collections.Generic.Dictionary<int, int>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var p = raw.Trim().Split(',');
            if (p.Length < 4 || p[0] != "R" || p[1] != roomHash) continue;
            if (!int.TryParse(p[2], out int x) || !int.TryParse(p[3], out int y)) continue;
            int k = x * 1000 + y; res[k] = res.TryGetValue(k, out int n) ? n + 1 : 1;
        }
        return res;
    }

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
        RescuesThisRound = 0; LastRescueFrom = new Vector2(-999f, -999f); still = 0f; hardStill = 0f; hasBest = false;
        if (mario != null) anchor = mario.transform.position;
    }

    private void Update()
    {
        if (mario == null || manager == null || manager.CurrentState != GameState.Playing) { still = 0f; hardStill = 0f; return; }
        Vector2 pos = mario.transform.position;
        // 只在"赶路"（去拿宝/回出口，一定有目标）时判定；起疑、查看、找人时站着不动是正常表演，不算卡住。
        var st = driver.Mind.State;
        // S241：起疑 ? / 查看 = 暂停（不清零）。用户反馈包：卡在门洞边 45 秒，状态在"赶路 ↔ 起疑"之间来回跳——
        // 以前每次起疑都清零、切回赶路第一帧又算"有进展" → 永远到不了救援。现在只有追人 / 找人 / 起步等待才清零。
        bool looking = st == MarioMindState.Curious || st == MarioMindState.Investigating;
        bool notRunning = driver.IsWaitingToStart || (st != MarioMindState.Running && !looking);
        // S240：被晕 / 东张西望 / 躲闪 / 回放 = 暂停（不清零）。以前这里清零 → 坑底有火反复烧、或者老在东张西望，永远凑不满 6 秒（用户截图：一直卡着）
        bool paused = looking || driver.Mind.IsStunned || driver.Mind.IsGlancing || driver.Mind.Dodging || ChainReplay.Playing;
        if (notRunning) { hasBest = false; anchor = pos; }
        // S198："没有进展"：到目标的距离 stuckSeconds 秒内没有缩短 stuckProgressCells 格 = 卡住（来回跳、原地跳都算）。
        bool progressed = false;
        Vector2? goal = driver.CurrentGoal();
        if (!notRunning && !looking)
        {
            if (goal.HasValue)
            {
                float d = Vector2.Distance(pos, goal.Value);
                if (!hasBest || d < bestDist - tuning.stuckProgressCells) { bestDist = d; hasBest = true; anchor = pos; progressed = true; }
            }
            else if ((pos - anchor).sqrMagnitude > tuning.stuckMoveEpsilon * tuning.stuckMoveEpsilon) { anchor = pos; progressed = true; }
        }
        var r = Tick(still, hardStill, Time.deltaTime, notRunning, paused, progressed, tuning.stuckSeconds, tuning.stuckHardCapSeconds);
        still = r.still; hardStill = r.hard;
        if (!r.rescue) return;
        hasBest = false;
        Rescue(pos, paused ? "兜底(" + (looking ? "起疑" : driver.Mind.IsStunned ? "晕" : driver.Mind.IsGlancing ? "张望" : "躲闪") + ")" : "没进展");
    }

    /// <summary>S235：房间守卫发现马里奥出界 → 立即救回（同一套"沿路线往前放 / 最近安全格"，也记一次 stuck_rescues）。</summary>
    public void RescueNow(Vector2 pos) { if (mario != null && driver != null) Rescue(pos, "掉出房间"); }

    private void Rescue(Vector2 pos, string why = "")
    {
        still = 0f; hardStill = 0f;
        RescuesThisRound++;
        LastStuckAt = pos;
        // S209：先沿"去目标的路线"往前放（越过卡住的地方）；原来只放到最近的安全格 = 常常就是卡住的那一格旁边 → 又卡住，循环（用户截图）
        // S240：安全格来自静态地图——塌掉的桥、碎掉的地板在静态图里还"在"。放之前用物理查一下脚下真的有地面，没有就换下一个。
        Vector2? goal = driver.CurrentGoal();
        Vector2 target = pos + Vector2.up * 0.5f; bool found = false;
        // S241：这局在附近（3 格内）已经救过一次又卡住 = 这里是个坑，放到路线更前面（至少 5 格）。
        bool repeat = RescuesThisRound > 1 && (LastRescueFrom - pos).sqrMagnitude < 9f;
        LastRescueFrom = pos;
        foreach (var c in RescueCandidates(roomGrid, pos, goal, SafeCells(), repeat ? 5f : 2f))
            if (HasGroundNow(c)) { target = c; found = true; break; }
        if (!found) target = NearestSafe(pos);
        Report(pos, target, why);
        mario.transform.position = target;
        var rb = mario.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;
        anchor = target;
        hasBest = false;
        flashUntil = Time.time + 2.5f;
        MarioMindLabel.RaiseRescued(); // S225：头顶说一句"哎呀，脚滑了"（不出戏；不改救援规则）
        Debug.LogWarning($"[Step1 H9] Mario stuck at ({pos.x:F1},{pos.y:F1}) for {tuning.stuckSeconds}s -> rescued to ({target.x:F1},{target.y:F1}). This is a layout bug; please report the spot.");
    }

    /// <summary>S240：此刻（物理上）这个格子脚下有没有地面、身体位置是不是空的。塌桥 / 碎地板 / 关上的门都按真实状态算。</summary>
    private static bool HasGroundNow(Vector2 cell)
    {
        int mask = LayerMask.GetMask("Ground");
        if (mask == 0) return true;
        var under = Physics2D.OverlapBox(cell + new Vector2(0f, -0.55f), new Vector2(0.6f, 0.2f), 0f, mask);
        var body = Physics2D.OverlapBox(cell + new Vector2(0f, 0.1f), new Vector2(0.5f, 0.6f), 0f, mask);
        bool ground = under != null && !under.isTrigger;
        bool blocked = body != null && !body.isTrigger && !SightLine.IsOneWayPlatform(body);
        return ground && !blocked;
    }

    /// <summary>
    /// S240 纯逻辑：救援候选位置，按优先级排好：先是路线上往前的格（离卡点 ≥2 格、能到出口），再是离卡点由近到远的安全格（最多 12 个）。
    /// 调用方逐个用物理检查（脚下真有地面）挑第一个。
    /// </summary>
    public static System.Collections.Generic.List<Vector2> RescueCandidates(string[] grid, Vector2 pos, Vector2? goal, System.Collections.Generic.ICollection<int> safe, float minAhead = 2f)
    {
        var res = new System.Collections.Generic.List<Vector2>();
        if (grid != null && grid.Length > 0 && goal.HasValue)
        {
            var reg = AsciiElementRegistry.GetDefault();
            var solid = reg.GetSolidChars(); var hazard = reg.GetHazardChars();
            var from = new LevelPathPlanner.Cell(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
            foreach (int dx in new[] { 0, -1, 1 })
            {
                var c = LevelPathPlanner.Settle(grid, new LevelPathPlanner.Cell(from.x + dx, from.y), solid, hazard);
                if (c.x >= 0) { from = c; break; }
            }
            var path = LevelPathPlanner.Path(grid, from, new LevelPathPlanner.Cell(Mathf.RoundToInt(goal.Value.x), Mathf.RoundToInt(goal.Value.y)));
            if (path != null)
                for (int i = 1; i < path.Count; i++)
                {
                    var c = path[i];
                    if (Mathf.Abs(c.x - pos.x) + Mathf.Abs(c.y - pos.y) < minAhead && i < path.Count - 1) continue;
                    if (safe != null && safe.Count > 0 && !safe.Contains(LevelReachabilityAnalyzer.CellKey(c.x, c.y))) continue;
                    res.Add(new Vector2(c.x, c.y));
                }
        }
        if (safe != null)
        {
            var near = new System.Collections.Generic.List<Vector2>();
            foreach (int key in safe) { var c = new Vector2(key / 100000, key % 100000); if ((c - pos).sqrMagnitude > 0.25f) near.Add(c); }
            near.Sort((a, b) => (a - pos).sqrMagnitude.CompareTo((b - pos).sqrMagnitude));
            for (int i = 0; i < near.Count && i < 12; i++) if (!res.Contains(near[i])) res.Add(near[i]);
        }
        return res;
    }

    private float lastShotAt = -999f;
    /// <summary>S240：每次救援记一行（哪张图、哪一格、为什么）+ 自动截图（30 秒内最多一张），工坊"检查轨迹"会用红叉标出来。</summary>
    private void Report(Vector2 pos, Vector2 target, string why)
    {
        if (tuning != null && !tuning.stuckAutoReport) return;
        try
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", Step1PlaytestLog.LogFolder);
            System.IO.Directory.CreateDirectory(folder);
            string hash = StrategySim.Hash(string.Join("\n", Step1PrankRoomBuilderBridge.CurrentRoom ?? roomGrid ?? new string[0])); // 与"检查轨迹"同一个指纹
            string state = driver != null && driver.Mind != null ? driver.Mind.State.ToString() : "";
            System.IO.File.AppendAllText(System.IO.Path.Combine(folder, StuckLogFile),
                StuckLine(hash, pos, why + " " + state + " -> " + Mathf.RoundToInt(target.x) + ";" + Mathf.RoundToInt(target.y) + " " + System.DateTime.Now.ToString("MM-dd HH:mm:ss")) + "\n");
            if (Time.unscaledTime - lastShotAt > 30f) { lastShotAt = Time.unscaledTime; Step1Feedback.CaptureNote("马里奥卡住被救（" + why + "）在 (" + pos.x.ToString("0.0") + "," + pos.y.ToString("0.0") + ")"); }
        }
        catch (System.Exception e) { Debug.LogWarning("[Step1 H9] could not write stuck log: " + e.Message); }
    }

    /// <summary>
    /// S209 纯逻辑：沿马里奥去 goal 的规划路线，找第一个离卡住点 ≥ 2 格、而且"能到出口"的站位（safe = null 时不过滤）。
    /// 找不到返回 null（用最近安全格兜底）。只用地形与马里奥自己的目标（H4）。
    /// </summary>
    public static Vector2? RescueAlongRoute(string[] grid, Vector2 pos, Vector2 goal, System.Collections.Generic.ICollection<int> safe, float minAhead = 2f)
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
            if (Mathf.Abs(c.x - pos.x) + Mathf.Abs(c.y - pos.y) < minAhead && i < path.Count - 1) continue;
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
