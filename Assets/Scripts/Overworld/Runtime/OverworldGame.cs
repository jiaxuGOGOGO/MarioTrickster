using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// S210：星露谷视角的小镇（大地图）。整张图在运行时由 mapText 生成（和横版房间一样"文字画关卡"，不依赖美术资源）。
/// 分层参考星露谷地图：地面（Back）→ 房屋/树/木箱（Buildings，按 y 排序，越靠下越在前）→ 高草盖在人物上面（Front）。
/// 马里奥：按日程去各扇门（OverworldWalker 寻路 + OverworldMind 起疑表），只能通过这里组装的"眼睛/耳朵"知道你（H4）。
/// 你：方向键走、P 伪装木箱、L 让附近香蕉皮变滑、T 挑衅、E 在门口埋伏/跟进 → 加载那个房间场景。
/// </summary>
public sealed class OverworldGame : MonoBehaviour
{
    [TextArea(6, 30)] public string mapText = "";
    public int[] doorNumbers = new int[0];
    public string[] doorScenes = new string[0];

    // ── 状态 ─────────────────────────────
    private OverworldMap.Map map;
    private MarioMindTuningSO tuning;
    private OverworldMind mind;
    private OverworldWalker mario;
    private double tx, ty;                  // 你
    private bool disguised, lastMoved;
    private float frozen;                   // 被抓后的定身
    private readonly List<OverworldMap.Door> stops = new List<OverworldMap.Door>();
    private readonly Dictionary<int, OverworldMap.Cell> doorCells = new Dictionary<int, OverworldMap.Cell>();
    private List<OverworldMap.Cell> lamps = new List<OverworldMap.Cell>();
    private OverworldMap.Cell home, spawn;
    private bool marioInside;               // 他进门了（等你跟进，或者超时算被偷）
    private float insideSeconds;
    private bool goingHome, dayOver, helpOpen;
    private OverworldOrder lastOrder;
    private string hint = ""; private float hintUntil;
    private static bool helpSeenThisPlay;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { helpSeenThisPlay = false; }

    // 香蕉皮：格子编号 → (闪烁剩余, 生效剩余)
    private readonly Dictionary<int, Vector2> peels = new Dictionary<int, Vector2>();
    private bool tauntedThisFrame;

    // ── 画面 ─────────────────────────────
    private Sprite square;
    private Transform marioGo, trickGo, trickCrate;
    private SpriteRenderer marioSr, trickSr, crateSr;
    private readonly Dictionary<int, SpriteRenderer> tileSr = new Dictionary<int, SpriteRenderer>();
    private Camera cam;
    private Mesh coneMesh;
    private Texture2D nightTex;

    public const float PeelFlashSeconds = 0.5f, PeelActiveSeconds = 3f;
    public const int AmbushBonusBombs = 1, MaxBonusBombs = 3;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        map = OverworldMap.Parse(mapText);
        if (map.W == 0) { enabled = false; Debug.LogError("[Overworld] mapText 为空"); return; }
        string town = SceneManager.GetActiveScene().name;
        if (!OverworldSession.Active || OverworldSession.MapName != map.name || OverworldSession.TownScene != town) OverworldSession.NewDay(map.name, town);
        OverworldSession.Active = true;
        for (int i = 0; i < doorNumbers.Length && i < doorScenes.Length; i++) OverworldSession.RoomScenes[doorNumbers[i]] = doorScenes[i];

        home = OverworldMap.Find(map, 'M')[0];
        var ts = OverworldMap.Find(map, 'T'); spawn = ts.Count > 0 ? ts[0] : home;
        lamps = OverworldMap.Find(map, 'i');
        foreach (var d in map.doors.OrderBy2()) { var c = OverworldMap.Find(map, (char)('0' + d.n)); if (c.Count == 1) { stops.Add(d); doorCells[d.n] = c[0]; } }

        mind = new OverworldMind(tuning);
        if (OverworldSession.HasPositions)
        {
            mario = new OverworldWalker(OverworldSession.MarioX, OverworldSession.MarioY);
            tx = OverworldSession.TricksterX; ty = OverworldSession.TricksterY;
        }
        else
        {
            mario = new OverworldWalker(home.x + 0.5, home.y + 0.5);
            tx = spawn.x + 0.5; ty = spawn.y + 0.5;
        }
        dayOver = OverworldSession.DayOver;
        helpOpen = !helpSeenThisPlay; helpSeenThisPlay = true;
        BuildVisuals();
    }

    // ═════════════════════ 每帧 ═════════════════════
    private void Update()
    {
        if (Step1Keys.Down(KeyCode.H)) helpOpen = !helpOpen;
        if (dayOver)
        {
            if (Step1Keys.Down(KeyCode.R)) { OverworldSession.NewDay(map.name, OverworldSession.TownScene); SceneManager.LoadScene(OverworldSession.TownScene); }
            UpdateVisuals(); return;
        }
        if (helpOpen) { UpdateVisuals(); return; }
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        OverworldSession.Minute += dt * tuning.overworldMinutesPerSecond;

        TricksterUpdate(dt);
        TickPeels(dt);
        MarioUpdate(dt);

        if (OverworldSession.Minute >= OverworldMap.DayEnd) EndDay();
        UpdateVisuals();
    }

    private void TricksterUpdate(float dt)
    {
        tauntedThisFrame = false;
        if (frozen > 0f) { frozen -= dt; lastMoved = false; return; }
        float h = (Step1Keys.Held(KeyCode.RightArrow) || Step1Keys.Held(KeyCode.D) ? 1f : 0f) - (Step1Keys.Held(KeyCode.LeftArrow) || Step1Keys.Held(KeyCode.A) ? 1f : 0f);
        float v = (Step1Keys.Held(KeyCode.UpArrow) || Step1Keys.Held(KeyCode.W) ? 1f : 0f) - (Step1Keys.Held(KeyCode.DownArrow) || Step1Keys.Held(KeyCode.S) ? 1f : 0f);
        var dir = new Vector2(h, v); if (dir.sqrMagnitude > 1f) dir.Normalize();
        double sp = tuning.overworldTricksterSpeed * OverworldMap.SpeedFactor(Here(tx, ty)) * (disguised ? 0.6 : 1.0) * dt;
        var (nx, ny) = OverworldMap.Move(map, tx, ty, dir.x * sp, dir.y * sp);
        lastMoved = (nx - tx) * (nx - tx) + (ny - ty) * (ny - ty) > 1e-8;
        tx = nx; ty = ny;

        if (Step1Keys.Down(KeyCode.P)) disguised = !disguised;
        if (Step1Keys.Down(KeyCode.L)) TryPeel();
        if (Step1Keys.Down(KeyCode.T))
        {
            if (OverworldSession.TauntsUsed >= tuning.overworldTaunts) Hint(Step1Text.OverworldTauntNone);
            else { OverworldSession.TauntsUsed++; tauntedThisFrame = true; }
        }
        // 道具箱
        int cx = (int)System.Math.Floor(tx), cy = (int)System.Math.Floor(ty), id = cy * map.W + cx;
        if (map.At(cx, cy) == '?' && !OverworldSession.UsedCells.Contains(id))
        {
            OverworldSession.UsedCells.Add(id);
            OverworldSession.BonusBombs = Mathf.Min(MaxBonusBombs, OverworldSession.BonusBombs + 1);
            Hint(Step1Text.OverworldPickup);
        }
        if (Step1Keys.Down(KeyCode.E)) TryDoor();
    }

    private void TryPeel()
    {
        int best = -1; double bd = tuning.overworldPrankRange * tuning.overworldPrankRange;
        foreach (var c in OverworldMap.Find(map, 'n'))
        {
            int id = c.y * map.W + c.x;
            if (OverworldSession.UsedCells.Contains(id) || peels.ContainsKey(id)) continue;
            double dx = c.x + 0.5 - tx, dy = c.y + 0.5 - ty, d = dx * dx + dy * dy;
            if (d <= bd) { bd = d; best = id; }
        }
        if (best < 0) { Hint(Step1Text.OverworldPeelNo); return; }
        peels[best] = new Vector2(PeelFlashSeconds, PeelActiveSeconds);
        Hint(Step1Text.OverworldPeel);
    }

    private void TickPeels(float dt)
    {
        var keys = new List<int>(peels.Keys);
        foreach (var k in keys)
        {
            var p = peels[k];
            if (p.x > 0f) p.x -= dt; else p.y -= dt;
            if (p.y <= 0f) { peels.Remove(k); OverworldSession.UsedCells.Add(k); }
            else peels[k] = p;
        }
    }

    private OverworldMap.Door NextStop => OverworldSession.NextStop < stops.Count ? stops[OverworldSession.NextStop] : null;

    private void TryDoor()
    {
        foreach (var kv in doorCells)
        {
            double dx = kv.Value.x + 0.5 - tx, dy = kv.Value.y + 0.5 - ty;
            if (dx * dx + dy * dy > 1.3 * 1.3) continue;
            var next = NextStop;
            if (next == null || next.n != kv.Key) { Hint(Step1Text.OverworldTooEarly); return; }
            var outcome = OverworldMind.AtDoor(!marioInside, insideSeconds, tuning.overworldLateWindowSeconds);
            if (outcome == OverworldMind.DoorOutcome.Missed) return; // 超时的已在 MarioUpdate 里处理
            EnterRoom(next, outcome);
            return;
        }
    }

    private void EnterRoom(OverworldMap.Door d, OverworldMind.DoorOutcome outcome)
    {
        if (!OverworldSession.RoomScenes.TryGetValue(d.n, out var scene) || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        { Hint(Step1Text.OverworldRoomMissing); return; }
        var c = doorCells[d.n];
        OverworldSession.PendingDoor = d.n;
        OverworldSession.PendingOutcome = outcome;
        if (outcome == OverworldMind.DoorOutcome.Ambush) OverworldSession.BonusBombs = Mathf.Min(MaxBonusBombs, OverworldSession.BonusBombs + AmbushBonusBombs);
        OverworldSession.CarriedSuspicion = OverworldMind.CarriedSuspicion(mind.State == OverworldMarioState.InRoom ? OverworldMarioState.Walking : mind.State, mind.Meter.Value, tuning.curiousThreshold);
        OverworldSession.Minute = System.Math.Max(OverworldSession.Minute, d.minute) + tuning.overworldVisitMinutes;
        OverworldSession.DelayedSeconds += mind.DelayedSeconds;
        OverworldSession.NextStop++;
        // 出门后两人都站在门口前一格（门下方能走就站下方）
        var below = OverworldMap.Walkable(map, c.x, c.y - 1) ? new OverworldMap.Cell(c.x, c.y - 1) : c;
        OverworldSession.MarioX = c.x + 0.5; OverworldSession.MarioY = c.y + 0.5;
        OverworldSession.TricksterX = below.x + 0.5; OverworldSession.TricksterY = below.y + 0.5;
        OverworldSession.HasPositions = true;
        SceneManager.LoadScene(scene);
    }

    private void MarioUpdate(float dt)
    {
        var next = NextStop;
        if (marioInside)
        {
            insideSeconds += dt;
            if (next != null && insideSeconds > tuning.overworldLateWindowSeconds)
            {
                // 你没跟进来：他安心偷完出门
                OverworldSession.RecordMissed(next.n);
                OverworldSession.Minute += tuning.overworldVisitMinutes;
                OverworldSession.NextStop++;
                marioInside = false; insideSeconds = 0f;
                mind.SetInRoom(false);
                Hint(Step1Text.OverworldMissed, 3f);
            }
            return;
        }

        // 日程目标
        Vector2? schedule = null; OverworldMap.Cell goalCell = home;
        if (next != null)
        {
            if (OverworldSession.Minute >= next.minute) { goalCell = doorCells[next.n]; schedule = Center(goalCell); }
        }
        else { goingHome = true; goalCell = home; schedule = Center(home); }

        // 眼睛 / 耳朵（H4：只看你的外观和位置）
        var r = Sight();
        bool sees = OverworldMap.CanSee(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r);
        var p = new OverworldPercept
        {
            marioPos = new Vector2((float)mario.x, (float)mario.y),
            seesFigure = sees,
            figurePos = new Vector2((float)tx, (float)ty),
            figureLooksLikeProp = disguised,
            figureMoving = lastMoved,
            sawRustle = !sees && lastMoved && OverworldMap.SeesRustle(map, lamps, mario.x, mario.y, mario.fx, mario.fy, tx, ty, r),
            rustlePos = new Vector2((float)tx, (float)ty),
            heardTaunt = tauntedThisFrame && Dist(mario.x, mario.y, tx, ty) <= tuning.overworldVisionRange * 1.5f,
            tauntPos = new Vector2((float)tx, (float)ty),
            scheduleTarget = schedule,
        };
        // 香蕉皮
        int mid = (int)System.Math.Floor(mario.y) * map.W + (int)System.Math.Floor(mario.x);
        if (peels.TryGetValue(mid, out var peel) && peel.x <= 0f) { p.slipped = true; peels.Remove(mid); OverworldSession.UsedCells.Add(mid); }

        var o = mind.Tick(dt, p);
        lastOrder = o;
        if (o.target.HasValue)
        {
            var tc = new OverworldMap.Cell(Mathf.FloorToInt(o.target.Value.x), Mathf.FloorToInt(o.target.Value.y));
            if (!OverworldMap.Walkable(map, tc.x, tc.y) && !OverworldCatalog.IsDoor(map.At(tc.x, tc.y))) tc = goalCell;
            mario.SetGoal(map, tc);
            float speed = o.state == OverworldMarioState.Chasing ? tuning.overworldChaseSpeed : tuning.overworldMarioSpeed;
            mario.Step(map, speed, dt);
        }
        if (o.tryCatch) Caught();

        // 到门口 / 到家
        if (o.state == OverworldMarioState.Walking && mario.Arrived && schedule.HasValue && mario.Goal.Equals(goalCell))
        {
            if (next != null) { marioInside = true; insideSeconds = 0f; mind.SetInRoom(true); Hint(Step1Text.OverworldLateHint, tuning.overworldLateWindowSeconds); }
            else EndDay();
        }
    }

    private void Caught()
    {
        OverworldSession.Caught++;
        tx = spawn.x + 0.5; ty = spawn.y + 0.5; disguised = false;
        frozen = tuning.overworldCaughtPenaltySeconds;
        mind.OnCaught();
        Hint(Step1Text.OverworldCaught, 2.5f);
    }

    private void EndDay()
    {
        if (dayOver) return;
        dayOver = true; OverworldSession.DayOver = true;
        OverworldSession.DelayedSeconds += mind.DelayedSeconds;
        if (!goingHome) mind.SetHome();
    }

    private OverworldMap.SightRules Sight() => new OverworldMap.SightRules
    {
        range = tuning.overworldVisionRange, nightRange = tuning.overworldNightVisionRange, halfAngleDeg = tuning.overworldVisionHalfAngle,
        nearRadius = tuning.overworldNearSense, grassRadius = tuning.overworldGrassSeeRadius, lampRadius = tuning.overworldLampRadius,
        night = OverworldSession.Minute >= OverworldMap.NightStart,
    };

    private char Here(double x, double y) => map.At((int)System.Math.Floor(x), (int)System.Math.Floor(y));
    private static Vector2 Center(OverworldMap.Cell c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
    private static float Dist(double ax, double ay, double bx, double by) => (float)System.Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    private void Hint(string s, float secs = 2f) { hint = s; hintUntil = Time.unscaledTime + secs; }

    // ═════════════════════ 画面 ═════════════════════
    private static int Order(double y) => 1000 - (int)(y * 10); // 越靠下越在前（星露谷式 y 排序）

    private void BuildVisuals()
    {
        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
        square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        var root = new GameObject("Town").transform;

        for (int y = 0; y < map.H; y++)
            for (int x = 0; x < map.W; x++)
            {
                char c = map.At(x, y);
                var t = OverworldCatalog.Get(c) ?? OverworldCatalog.Get('.');
                // 地面层（Back）：路/水/泥/草本身；其余一律铺草
                bool ground = c == '.' || c == '=' || c == 'w' || c == 'g' || c == '"';
                var gt = ground ? t : OverworldCatalog.Get(c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c) ? '=' : '.');
                var g = Quad(root, "g", x + 0.5f, y + 0.5f, 1f, 1f, new Color(gt.r, gt.g, gt.b), -2000);
                if ((x + y) % 2 == 0) g.color *= 0.96f; // 棋盘微差，看得出格子
                if (c == '"') { var tall = Quad(root, "grass", x + 0.5f, y + 0.6f, 1f, 1.1f, new Color(t.r, t.g, t.b, 0.88f), 3000); tall.name = "TallGrass"; }
                else if (c == 'W')
                {
                    bool wallFace = map.At(x, y - 1) != 'W'; // 最下面一排 = 墙面，上面 = 屋顶
                    Quad(root, "house", x + 0.5f, y + 0.5f, 1f, 1f, wallFace ? new Color(t.r * 0.8f, t.g * 0.75f, t.b * 0.7f) : new Color(0.72f, 0.3f, 0.25f), Order(y));
                }
                else if (c == 't')
                {
                    Quad(root, "trunk", x + 0.5f, y + 0.35f, 0.3f, 0.7f, new Color(0.4f, 0.26f, 0.14f), Order(y) - 1);
                    Quad(root, "crown", x + 0.5f, y + 1.0f, 1.2f, 1.1f, new Color(t.r, t.g, t.b), Order(y)); // 树冠越过上一格：你在它上方时被树冠盖住（星露谷 Front）
                }
                else if (c == 'c') Quad(root, "crate", x + 0.5f, y + 0.55f, 0.85f, 0.9f, new Color(t.r, t.g, t.b), Order(y));
                else if (c == 'f') Quad(root, "fence", x + 0.5f, y + 0.45f, 1f, 0.5f, new Color(t.r, t.g, t.b), Order(y));
                else if (c == 'i')
                {
                    Quad(root, "post", x + 0.5f, y + 0.6f, 0.2f, 1.2f, new Color(0.25f, 0.25f, 0.28f), Order(y));
                    tileSr[y * map.W + x] = Quad(root, "light", x + 0.5f, y + 1.2f, 0.45f, 0.35f, new Color(t.r, t.g, t.b), Order(y) + 1);
                }
                else if (c == 'n' || c == '?') tileSr[y * map.W + x] = Quad(root, c == 'n' ? "peel" : "box", x + 0.5f, y + 0.5f, 0.55f, 0.45f, new Color(t.r, t.g, t.b), -1500);
                else if (OverworldCatalog.IsDoor(c)) tileSr[y * map.W + x] = Quad(root, "door" + c, x + 0.5f, y + 0.75f, 0.7f, 0.5f, new Color(t.r, t.g, t.b), Order(y + 1) + 1);
                else if (c == 'M') Quad(root, "home", x + 0.5f, y + 0.75f, 0.7f, 0.5f, new Color(t.r, t.g, t.b), Order(y + 1) + 1);
            }

        marioGo = new GameObject("OverworldMario").transform;
        marioSr = Quad(marioGo, "body", 0, 0.1f, 0.65f, 0.85f, new Color(0.9f, 0.18f, 0.16f), 0); marioSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        trickGo = new GameObject("OverworldTrickster").transform;
        trickSr = Quad(trickGo, "body", 0, 0.1f, 0.6f, 0.8f, new Color(0.22f, 0.4f, 0.92f), 0); trickSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        var ct = OverworldCatalog.Get('c');
        crateSr = Quad(trickGo, "crate", 0, 0.05f, 0.85f, 0.9f, new Color(ct.r, ct.g, ct.b), 0); crateSr.transform.localPosition = new Vector3(0, 0.05f, 0);
        trickCrate = crateSr.transform;

        var coneGo = new GameObject("VisionCone");
        coneMesh = new Mesh();
        coneGo.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        var mr = coneGo.AddComponent<MeshRenderer>();
        var sh = Shader.Find("Sprites/Default");
        if (sh != null) mr.sharedMaterial = new Material(sh) { color = new Color(1f, 0.95f, 0.4f, 0.16f) };
        mr.sortingOrder = 2500;

        nightTex = new Texture2D(1, 1); nightTex.SetPixel(0, 0, Color.white); nightTex.Apply();

        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); }
        cam.orthographic = true; cam.orthographicSize = Mathf.Min(7.5f, map.H / 2f);
        cam.backgroundColor = new Color(0.12f, 0.2f, 0.12f); cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private SpriteRenderer Quad(Transform parent, string name, float x, float y, float w, float h, Color c, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, y, 0);
        go.transform.localScale = new Vector3(w, h, 1);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square; sr.color = c; sr.sortingOrder = order;
        return sr;
    }

    private void UpdateVisuals()
    {
        if (marioGo == null) return;
        marioGo.position = new Vector3((float)mario.x, (float)mario.y, 0);
        marioSr.sortingOrder = Order(mario.y) + 2;
        marioSr.enabled = !marioInside && !dayOver;
        trickGo.position = new Vector3((float)tx, (float)ty, 0);
        trickSr.enabled = !disguised; crateSr.enabled = disguised;
        trickSr.sortingOrder = crateSr.sortingOrder = Order(ty) + 2;
        if (frozen > 0f) trickSr.color = Color.Lerp(new Color(0.22f, 0.4f, 0.92f), Color.white, Mathf.PingPong(Time.time * 6f, 1f));
        else trickSr.color = new Color(0.22f, 0.4f, 0.92f);

        // 香蕉皮 / 道具箱 / 门的状态
        foreach (var kv in tileSr)
        {
            int x = kv.Key % map.W, y = kv.Key / map.W; char c = map.At(x, y);
            if (c == 'n')
            {
                bool used = OverworldSession.UsedCells.Contains(kv.Key);
                kv.Value.enabled = !used;
                if (peels.TryGetValue(kv.Key, out var p)) kv.Value.color = p.x > 0f ? (Mathf.PingPong(Time.time * 12f, 1f) > 0.5f ? Color.white : Color.yellow) : new Color(1f, 0.6f, 0.1f);
            }
            else if (c == '?') kv.Value.enabled = !OverworldSession.UsedCells.Contains(kv.Key);
            else if (c == 'i') kv.Value.color = OverworldSession.Minute >= OverworldMap.NightStart ? new Color(1f, 0.95f, 0.6f) : new Color(0.6f, 0.58f, 0.5f);
            else if (OverworldCatalog.IsDoor(c))
            {
                var res = OverworldSession.ResultOf(c - '0');
                var next = NextStop;
                kv.Value.color = res == OverworldSession.DoorResult.Defended ? new Color(0.3f, 0.85f, 0.4f)
                    : res != OverworldSession.DoorResult.NotYet ? new Color(0.35f, 0.35f, 0.35f)
                    : next != null && next.n == c - '0' ? Color.Lerp(new Color(0.88f, 0.32f, 0.62f), Color.white, Mathf.PingPong(Time.time * 2f, 0.6f)) : new Color(0.88f, 0.32f, 0.62f);
            }
        }
        UpdateCone();

        // 镜头跟你，夹在地图内
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float cx = Mathf.Clamp((float)tx, Mathf.Min(halfW, map.W / 2f), Mathf.Max(map.W - halfW, map.W / 2f));
        float cy = Mathf.Clamp((float)ty, Mathf.Min(halfH, map.H / 2f), Mathf.Max(map.H - halfH, map.H / 2f));
        cam.transform.position = Vector3.Lerp(cam.transform.position, new Vector3(cx, cy, -10f), 0.2f);
    }

    private void UpdateCone()
    {
        if (coneMesh == null) return;
        coneMesh.Clear();
        if (marioInside || dayOver) return;
        var r = Sight();
        bool night = r.night;
        float range = (float)(night ? System.Math.Min(r.range, r.nightRange) : r.range);
        float baseAng = Mathf.Atan2((float)mario.fy, (float)mario.fx), half = (float)r.halfAngleDeg * Mathf.Deg2Rad;
        const int seg = 18;
        var v = new Vector3[seg + 2]; var tri = new int[seg * 3];
        v[0] = new Vector3((float)mario.x, (float)mario.y, 0);
        for (int i = 0; i <= seg; i++)
        {
            float a = baseAng - half + 2f * half * i / seg, len = 0f;
            float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
            while (len < range)
            {
                float nl = len + 0.25f; double px = mario.x + dx * nl, py = mario.y + dy * nl;
                if (OverworldCatalog.BlocksSight(map.At((int)System.Math.Floor(px), (int)System.Math.Floor(py)))) break;
                len = nl;
            }
            v[i + 1] = new Vector3((float)mario.x + dx * len, (float)mario.y + dy * len, 0);
            if (i < seg) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 2; tri[i * 3 + 2] = i + 1; }
        }
        coneMesh.vertices = v; coneMesh.triangles = tri;
    }

    private void OnGUI()
    {
        if (map == null || cam == null) return;
        bool night = OverworldSession.Minute >= OverworldMap.NightStart;
        if (night) { GUI.color = new Color(0.05f, 0.08f, 0.25f, 0.35f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), nightTex); GUI.color = Color.white; }

        var box = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, wordWrap = true };
        var big = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        // 右上：时钟 + 门列表
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Step1Text.OverworldClock(OverworldMap.Clock(OverworldSession.Minute), night));
        foreach (var d in stops) sb.AppendLine(Step1Text.OverworldDoorLabel(d.n, OverworldMap.Clock(d.minute), OverworldSession.ResultOf(d.n)));
        sb.Append($"💣 +{OverworldSession.BonusBombs}   T ×{Mathf.Max(0, tuning.overworldTaunts - OverworldSession.TauntsUsed)}");
        GUI.Box(new Rect(Screen.width - 210, 10, 200, 40 + stops.Count * 22 + 22), sb.ToString(), box);
        // 左上：下一站
        var next = NextStop;
        GUI.Box(new Rect(10, 10, 280, 52), next != null ? Step1Text.OverworldNextDoor(next.n, OverworldMap.Clock(next.minute)) : Step1Text.OverworldGoingHome, box);
        // 马里奥头上的标记（? / ! / !! / ?! / OUCH!）
        if (!marioInside && !dayOver && !string.IsNullOrEmpty(lastOrder.mark))
        {
            var sp = cam.WorldToScreenPoint(new Vector3((float)mario.x, (float)mario.y + 1f, 0));
            var ms = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ms.normal.textColor = lastOrder.mark.StartsWith("!") ? Color.red : Color.yellow;
            GUI.Label(new Rect(sp.x - 50, Screen.height - sp.y - 20, 100, 40), lastOrder.mark, ms);
        }
        // 门口提示
        if (next != null && doorCells.TryGetValue(next.n, out var dc) && Dist(dc.x + 0.5, dc.y + 0.5, tx, ty) <= 1.3f && !dayOver)
            GUI.Box(new Rect(Screen.width / 2f - 160, Screen.height - 100, 320, 56), marioInside ? Step1Text.OverworldLateHint : Step1Text.OverworldAmbushHint, big);
        if (Time.unscaledTime < hintUntil) GUI.Box(new Rect(Screen.width / 2f - 220, 70, 440, 56), hint, big);
        if (helpOpen) GUI.Box(new Rect(Screen.width / 2f - 330, Screen.height / 2f - 150, 660, 300), Step1Text.OverworldHelp, box);
        if (dayOver) GUI.Box(new Rect(Screen.width / 2f - 280, Screen.height / 2f - 90, 560, 180), OverworldSession.Summary(stops.Count), big);
    }
}
