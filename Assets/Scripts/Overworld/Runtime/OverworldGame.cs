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

    // ── 状态（S213：规则全部在纯逻辑 OverworldTown 里；这里只读键盘、画画面、切场景）──
    private OverworldTown town;
    private OverworldMap.Map map;
    private MarioMindTuningSO tuning;
    private bool helpOpen;
    private string hint = ""; private float hintUntil;
    private static bool helpSeenThisPlay;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() { helpSeenThisPlay = false; }

    // 旧字段名保留为属性（画面代码不用改）
    private OverworldWalker mario => town.mario;
    private double tx => town.tx;
    private double ty => town.ty;
    private bool disguised => town.disguised;
    private float frozen => town.frozen;
    private bool marioInside => town.marioInside;
    private bool dayOver => town.dayOver;
    private OverworldOrder lastOrder => town.lastOrder;
    private List<OverworldMap.Door> stops => town.stops;
    private Dictionary<int, OverworldMap.Cell> doorCells => town.doorCells;
    private List<OverworldMap.Cell> lamps => town.lamps;
    private Dictionary<int, Vector2> peels => town.peels;
    private OverworldMap.Door NextStop => town.NextStop;

    // ── 画面 ─────────────────────────────
    private Sprite square;
    private Transform marioGo, trickGo, trickCrate;
    private SpriteRenderer marioSr, trickSr, crateSr;
    private readonly Dictionary<int, SpriteRenderer> tileSr = new Dictionary<int, SpriteRenderer>();
    private Camera cam;
    private Mesh coneMesh, fillMesh;
    private MeshRenderer fillMr;
    private Texture2D nightTex;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        map = OverworldMap.Parse(mapText);
        if (map.W == 0) { enabled = false; Debug.LogError("[Overworld] mapText 为空"); return; }
        string townScene = gameObject.scene.path; // S211：完整路径（切换用）；S212：用自己所在的场景，不依赖"当前激活场景"
        if (!OverworldSession.Active || OverworldSession.MapName != map.name || OverworldSession.TownScene != townScene) OverworldSession.NewDay(map.name, townScene);
        OverworldSession.Active = true;
        for (int i = 0; i < doorNumbers.Length && i < doorScenes.Length; i++) OverworldSession.RoomScenes[doorNumbers[i]] = doorScenes[i];
        town = new OverworldTown(map, tuning, n => OverworldSession.RoomScenes.TryGetValue(n, out var sc) && SceneTransit.CanLoad(sc));
        town.autoFastIdleSeconds = tuning.overworldAutoFastIdleSeconds; // S224：不碰键盘 1.5 秒自动快进
        map = town.map; // S218：小镇自己那份（今天被大机关改过的地形、赶集日的新时间都在里面）
        Time.timeScale = 1f; // S217：从暂停中的房间/测试回来也不会"画面不动"
        helpOpen = !helpSeenThisPlay && !Step1QuickTest.On; helpSeenThisPlay = true; // S217：快速测试模式不弹说明
        BuildVisuals();
        UpdateVisuals(); SnapCamera(); // S212：镜头直接就位（以前从默认位置滑过来，揭幕时画面在"飘"）
        SceneTransit.RevealAt(new Vector3((float)tx, (float)ty, 0), cam); // 转场的圆在你身上展开
        // S218：早上公布今天的天气（输入随机：做决定之前就知道）；刚守住一户 → 旁边的大机关重新装填
        if (OverworldSession.Minute <= OverworldMap.DayStart + 1) Hint(Step1Text.OverworldWeather(OverworldSession.Day, OverworldEvents.Zh(town.weather)), 5f);
        else if (town.reloaded.HasValue) Hint(Step1Text.OverworldBigReloaded, 3f);
        Step1Feedback.Context = () => $"小镇 {map.name} {OverworldMap.Clock(OverworldSession.Minute)} 下一扇门 {(NextStop != null ? NextStop.n.ToString() : "-")} 你({tx:0.0},{ty:0.0}) 马里奥({mario.x:0.0},{mario.y:0.0}) {town.mind.State}";
    }

    // ═════════════════════ 每帧 ═════════════════════
    private void OnDestroy() { Step1Feedback.Context = null; }

    private void Update()
    {
        if (SceneTransit.Busy) { UpdateVisuals(); return; } // S211：切换中（黑幕）不走时间、不吃按键
        // S217：说明面板按任意键关闭（以前只有 H 能关，而且说明开着时时间、走路全停 → "画面固定不动、控制不了"）
        if (helpOpen) { if (Step1Keys.AnyDown()) helpOpen = false; UpdateVisuals(); return; }
        if (Step1Keys.Down(KeyCode.H)) { helpOpen = true; UpdateVisuals(); return; }
        ZoomKeys();
        if (dayOver)
        {
            if (Step1Keys.Down(KeyCode.R))
            {
                OverworldSession.NewDay(map.name, OverworldSession.TownScene, OverworldSession.Day + 1); // S218：第 N+1 天（天气换了）
                if (!SceneTransit.Go(OverworldSession.TownScene, Step1Text.OverworldTransitNewDay(map.name))) SceneManager.LoadScene(OverworldSession.TownScene);
            }
            UpdateVisuals(); return;
        }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); // S217：不受 timeScale 影响（小镇没有暂停）
        var input = new OverworldTown.Input
        {
            h = (Step1Keys.Held(KeyCode.RightArrow) || Step1Keys.Held(KeyCode.D) ? 1f : 0f) - (Step1Keys.Held(KeyCode.LeftArrow) || Step1Keys.Held(KeyCode.A) ? 1f : 0f),
            v = (Step1Keys.Held(KeyCode.UpArrow) || Step1Keys.Held(KeyCode.W) ? 1f : 0f) - (Step1Keys.Held(KeyCode.DownArrow) || Step1Keys.Held(KeyCode.S) ? 1f : 0f),
            disguise = Step1Keys.Down(KeyCode.P), peel = Step1Keys.Down(KeyCode.L), taunt = Step1Keys.Down(KeyCode.T), door = Step1Keys.Down(KeyCode.E), weather = Step1Keys.Down(KeyCode.Q),
            fastForward = Step1Keys.Held(KeyCode.Space), // S213：等他出发时按住空格快进（有事发生自动恢复）
            // S219：坐在炮里按一下方向 = 瞄准一步（右 左 上 下）
            aim = Step1Keys.Down(KeyCode.RightArrow) || Step1Keys.Down(KeyCode.D) ? 1 : Step1Keys.Down(KeyCode.LeftArrow) || Step1Keys.Down(KeyCode.A) ? 2 : Step1Keys.Down(KeyCode.UpArrow) || Step1Keys.Down(KeyCode.W) ? 3 : Step1Keys.Down(KeyCode.DownArrow) || Step1Keys.Down(KeyCode.S) ? 4 : 0,
        };
        town.Tick(dt, input);
        if (town.hint != OverworldTown.Note.None) Hint(town.hint == OverworldTown.Note.Caught ? Step1Text.OverworldCaughtWhy(town.caughtWhy) : NoteText(town.hint), town.hintSeconds); // S222：被抓说明原因
        BigFx();
        if (tuning.soundRings) foreach (var so in town.sounds) Step1Fx.SoundRing(new Vector2(so.x, so.y), so.z); // S224：声音圈 = 他真实的听力范围
        if (town.wantsEnter)
        {
            var d = town.enterDoor; var c = doorCells[d.n];
            // S211：淡出 → 标题卡 → 后台加载 → 淡入（SceneTransit）；S212：圆从这扇门收拢
            if (!SceneTransit.Go(OverworldSession.RoomScenes[d.n], Step1Text.OverworldTransitToRoom(d.n, d.room, town.enterOutcome), new Vector3(c.x + 0.5f, c.y + 0.75f, 0)))
                SceneManager.LoadScene(OverworldSession.RoomScenes[d.n]);
        }
        UpdateVisuals();
    }

    private static string NoteText(OverworldTown.Note n)
    {
        switch (n)
        {
            case OverworldTown.Note.TauntNone: return Step1Text.OverworldTauntNone;
            case OverworldTown.Note.Pickup: return Step1Text.OverworldPickup;
            case OverworldTown.Note.PeelNo: return Step1Text.OverworldPeelNo;
            case OverworldTown.Note.Peel: return Step1Text.OverworldPeel;
            case OverworldTown.Note.TooEarly: return Step1Text.OverworldTooEarly;
            case OverworldTown.Note.RoomMissing: return Step1Text.OverworldRoomMissing;
            case OverworldTown.Note.Missed: return Step1Text.OverworldMissed;
            case OverworldTown.Note.LateHint: return Step1Text.OverworldLateHint;
            case OverworldTown.Note.Caught: return Step1Text.OverworldCaught;
            case OverworldTown.Note.AmbushWait: return Step1Text.OverworldAmbushWait;
            case OverworldTown.Note.Spotted: return Step1Text.OverworldSpotted;
            case OverworldTown.Note.BigArmed: return Step1Text.OverworldBigArmed;
            case OverworldTown.Note.BigHit: return Step1Text.OverworldBigHit;
            case OverworldTown.Note.BigChain: return Step1Text.OverworldBigChain;
            case OverworldTown.Note.BigReloaded: return Step1Text.OverworldBigReloaded;
            case OverworldTown.Note.BigSelf: return Step1Text.OverworldBigSelf;
            case OverworldTown.Note.BigStuck: return Step1Text.OverworldBigStuck;
            case OverworldTown.Note.CannonSeat: return Step1Text.OverworldCannonSeat;
            case OverworldTown.Note.CannonBadAim: return Step1Text.OverworldCannonBadAim;
            case OverworldTown.Note.CannonTamper: return Step1Text.OverworldCannonTamper;
            case OverworldTown.Note.MarioRides: return Step1Text.OverworldMarioRides;
            case OverworldTown.Note.Lightning: return Step1Text.OverworldLightning;
            case OverworldTown.Note.Mudslide: return Step1Text.OverworldMudslide;
            case OverworldTown.Note.CaveHop: return Step1Text.OverworldCaveHop;
            case OverworldTown.Note.CaveNoExit: return Step1Text.OverworldCaveNoExit;
            case OverworldTown.Note.YouHurt: return Step1Text.OverworldYouHurt;
            case OverworldTown.Note.YouKO: return Step1Text.OverworldYouKO;
            case OverworldTown.Note.MarioKO: return Step1Text.OverworldMarioKO;
            case OverworldTown.Note.Heal: return Step1Text.OverworldHeal;
            case OverworldTown.Note.MarioHeal: return Step1Text.OverworldMarioHeal;
            case OverworldTown.Note.EnergyUp: return Step1Text.OverworldEnergyUp;
            case OverworldTown.Note.EnergyFull: return Step1Text.OverworldEnergyFull;
            case OverworldTown.Note.Cloud: return Step1Text.OverworldCloud;
            case OverworldTown.Note.CloudLow: return Step1Text.OverworldCloudLow;
            case OverworldTown.Note.StormBolt: return Step1Text.OverworldStormBolt;
            case OverworldTown.Note.AmbushArmed: return Step1Text.OverworldAmbushArmed;
            default: return "";
        }
    }

    private OverworldMap.SightRules Sight() => town.Sight();
    private static float Dist(double ax, double ay, double bx, double by) => OverworldTown.Dist(ax, ay, bx, by);
    private void Hint(string s, float secs = 2f) { hint = s; hintUntil = Time.unscaledTime + secs; }

    // ═════════════════════ 画面 ═════════════════════
    private static int Order(double y) => 1000 - (int)(y * 10); // 越靠下越在前（星露谷式 y 排序）

    private void BuildVisuals()
    {
        var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
        square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        var root = new GameObject("Town").transform;
        // S217：地面整张一张贴图（1 像素 = 1 格）。以前每格一个物体，192×128 的大世界会有 2 万多个物体、进场景卡。
        var groundPx = new Color[map.W * map.H];

        for (int y = 0; y < map.H; y++)
            for (int x = 0; x < map.W; x++)
            {
                char c = map.At(x, y);
                var t = OverworldCatalog.Get(c) ?? OverworldCatalog.Get('.');
                // 地面层（Back）：路/水/泥/草本身；其余一律铺草
                bool ground = c == '.' || c == '=' || c == 'w' || c == 'g' || c == '"' || c == '^' || c == 'h';
                var gt = ground ? t : OverworldCatalog.Get(c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c) ? '=' : '.');
                var gc = new Color(gt.r, gt.g, gt.b); if ((x + y) % 2 == 0) gc *= 0.96f; // 棋盘微差，看得出格子
                groundPx[y * map.W + x] = new Color(gc.r, gc.g, gc.b, 1f);
                if (c == '"') { var tall = Quad(root, "grass", x + 0.5f, y + 0.6f, 1f, 1.1f, new Color(t.r, t.g, t.b, 0.88f), 3000); tall.name = "TallGrass"; Keep(x, y, tall); }
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
                // S219 山地：山 = 高一截的灰石块（越靠上越亮 = 看得出坡）；山丘 = 地面色 + 一道亮边；山洞 = 黑色半圆洞口
                else if (c == 'A') { Quad(root, "mountain", x + 0.5f, y + 0.7f, 1.05f, 1.4f, new Color(t.r, t.g, t.b) * (map.At(x, y + 1) == 'A' ? 1.08f : 0.95f), Order(y)); if (map.At(x, y + 1) != 'A') Quad(root, "snow", x + 0.5f, y + 1.3f, 0.7f, 0.2f, new Color(0.92f, 0.92f, 0.95f), Order(y) + 1); }
                else if (c == '^') Keep(x, y, Quad(root, "hillTop", x + 0.5f, y + 0.8f, 0.9f, 0.18f, new Color(t.r * 1.25f, t.g * 1.2f, t.b * 1.1f), -1600));
                else if (c == 'h') Quad(root, "caveMouth", x + 0.5f, y + 0.55f, 0.8f, 0.7f, new Color(0.05f, 0.04f, 0.04f), Order(y) + 1);
                else if (c == 'c') Keep(x, y, Quad(root, "crate", x + 0.5f, y + 0.55f, 0.85f, 0.9f, new Color(t.r, t.g, t.b), Order(y)));
                else if (c == 'f') Keep(x, y, Quad(root, "fence", x + 0.5f, y + 0.45f, 1f, 0.5f, new Color(t.r, t.g, t.b), Order(y)));
                // S218 大机关：比房子矮一点、比木箱大一圈（一眼看出"这是小镇级的东西"）
                else if (c == 'K') { Keep(x, y, Quad(root, "cannonBase", x + 0.5f, y + 0.4f, 1.3f, 0.8f, new Color(0.25f, 0.2f, 0.16f), Order(y))); if (OverworldProps.Aim(map, new OverworldMap.Cell(x, y), out _, out int kd, out _)) Keep(x, y, Quad(root, "barrel", x + 0.5f + OverworldProps.DX[kd] * 0.55f, y + 0.6f + OverworldProps.DY[kd] * 0.4f, OverworldProps.DX[kd] != 0 ? 1.2f : 0.55f, OverworldProps.DX[kd] != 0 ? 0.55f : 1.2f, new Color(t.r, t.g, t.b), Order(y) + 1)); }
                else if (c == 'O') Keep(x, y, Quad(root, "boulder", x + 0.5f, y + 0.6f, 1.25f, 1.2f, new Color(t.r, t.g, t.b), Order(y)));
                else if (c == 'U') { Keep(x, y, Quad(root, "towerLegs", x + 0.5f, y + 0.5f, 0.9f, 1f, new Color(0.45f, 0.32f, 0.2f), Order(y))); Keep(x, y, Quad(root, "tank", x + 0.5f, y + 1.35f, 1.4f, 1f, new Color(t.r, t.g, t.b), Order(y) + 1)); }
                else if (c == 'X') tileSr[y * map.W + x] = Quad(root, "target", x + 0.5f, y + 0.5f, 0.8f, 0.8f, new Color(t.r, t.g, t.b, 0.8f), -1500);
                else if (c == 'i')
                {
                    Quad(root, "post", x + 0.5f, y + 0.6f, 0.2f, 1.2f, new Color(0.25f, 0.25f, 0.28f), Order(y));
                    tileSr[y * map.W + x] = Quad(root, "light", x + 0.5f, y + 1.2f, 0.45f, 0.35f, new Color(t.r, t.g, t.b), Order(y) + 1);
                }
                else if (c == 'n' || c == '?') tileSr[y * map.W + x] = Quad(root, c == 'n' ? "peel" : "box", x + 0.5f, y + 0.5f, 0.55f, 0.45f, new Color(t.r, t.g, t.b), -1500);
                else if (OverworldCatalog.IsDoor(c)) tileSr[y * map.W + x] = Quad(root, "door" + c, x + 0.5f, y + 0.75f, 0.7f, 0.5f, new Color(t.r, t.g, t.b), Order(y + 1) + 1);
                else if (c == 'M') Quad(root, "home", x + 0.5f, y + 0.75f, 0.7f, 0.5f, new Color(t.r, t.g, t.b), Order(y + 1) + 1);
                else if (c == '+' || c == '*') tileSr[y * map.W + x] = Icon(root, OverworldArt.IconOf(c), x + 0.5f, y + 0.5f, 0.8f, -1500); // S220 补心 / 能量
                // S220：像素图标叠在原来的色块上（一眼认出是什么；美术换图 = 同名 PNG 覆盖）
                string ik = OverworldArt.IconOf(c);
                if (ik != null && c != '+' && c != '*' && c != 'i')
                {
                    var icon = Icon(root, ik, x + 0.5f, y + 0.55f, c == 'K' || c == 'O' || c == 'U' ? 1.15f : 0.85f, c == 'X' || c == 'n' || c == '?' ? -1450 : Order(y) + 2);
                    if (icon != null)
                    {
                        if (c == 'n' || c == '?' || c == 'X') { if (tileSr.TryGetValue(y * map.W + x, out var under)) under.enabled = false; tileSr[y * map.W + x] = icon; }
                        else Keep(x, y, icon);
                    }
                }
            }

        groundTex = null;
        var gtex = new Texture2D(map.W, map.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        gtex.SetPixels(groundPx); gtex.Apply(); groundTex = gtex;
        var ggo = new GameObject("Ground"); ggo.transform.SetParent(root, false);
        var gsr = ggo.AddComponent<SpriteRenderer>();
        gsr.sprite = Sprite.Create(gtex, new Rect(0, 0, map.W, map.H), Vector2.zero, 1f); gsr.sortingOrder = -2000;

        marioGo = new GameObject("OverworldMario").transform;
        marioSr = Quad(marioGo, "body", 0, 0.1f, 0.65f, 0.85f, new Color(0.9f, 0.18f, 0.16f), 0); marioSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        trickGo = new GameObject("OverworldTrickster").transform;
        trickSr = Quad(trickGo, "body", 0, 0.1f, 0.6f, 0.8f, new Color(0.22f, 0.4f, 0.92f), 0); trickSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        var ct = OverworldCatalog.Get('c');
        crateSr = Quad(trickGo, "crate", 0, 0.05f, 0.85f, 0.9f, new Color(ct.r, ct.g, ct.b), 0); crateSr.transform.localPosition = new Vector3(0, 0.05f, 0);
        trickCrate = crateSr.transform;
        // S217：头顶名字，一眼分清谁是你（蓝）谁是马里奥（红）
        youTag = Tag(trickGo, "你 YOU", new Color(0.55f, 0.78f, 1f));
        marioTag = Tag(marioGo, "马里奥", new Color(1f, 0.55f, 0.5f));

        var coneGo = new GameObject("VisionCone");
        coneMesh = new Mesh();
        coneGo.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        var mr = coneGo.AddComponent<MeshRenderer>();
        var sh = Shader.Find("Sprites/Default");
        if (sh != null) mr.sharedMaterial = new Material(sh) { color = new Color(1f, 0.95f, 0.4f, 0.16f) };
        mr.sortingOrder = 2500;
        // S224：视锥里按起疑程度灌注（Shadow Tactics），叠在视锥上
        var fillGo = new GameObject("VisionConeFill");
        fillMesh = new Mesh(); fillGo.AddComponent<MeshFilter>().sharedMesh = fillMesh;
        fillMr = fillGo.AddComponent<MeshRenderer>();
        if (sh != null) fillMr.sharedMaterial = new Material(sh) { color = Color.white };
        fillMr.sortingOrder = 2501;

        nightTex = new Texture2D(1, 1); nightTex.SetPixel(0, 0, Color.white); nightTex.Apply();

        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); }
        cam.orthographic = true; cam.orthographicSize = Mathf.Min(zoom, map.H / 2f);
        cam.backgroundColor = new Color(0.12f, 0.2f, 0.12f); cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private TextMesh youTag, marioTag;

    // ═════════ S220：像素图标（OverworldArt；Resources/OverworldArt/<名字>.png 同名覆盖）═════════
    private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();
    public static Sprite IconSprite(string key)
    {
        if (key == null) return null;
        if (iconCache.TryGetValue(key, out var sp) && sp != null) return sp;
        var tex = Resources.Load<Texture2D>("OverworldArt/" + key); // 美术换图
        if (tex == null)
        {
            var px = OverworldArt.Pixels(key); if (px == null) return null;
            tex = new Texture2D(OverworldArt.Size, OverworldArt.Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[OverworldArt.Size * OverworldArt.Size];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
            tex.SetPixels(cols); tex.Apply();
        }
        else tex.filterMode = FilterMode.Point;
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        iconCache[key] = sp; return sp;
    }
    private SpriteRenderer Icon(Transform parent, string key, float x, float y, float size, int order)
    {
        var sp = IconSprite(key); if (sp == null) return null;
        var sr = Quad(parent, "icon_" + key, x, y, size, size, Color.white, order); sr.sprite = sp; return sr;
    }

    private readonly List<SpriteRenderer> strikeSr = new List<SpriteRenderer>();
    private SpriteRenderer cloudSr;
    private struct Pop { public Vector3 at; public int kind; public float until; }
    private readonly List<Pop> pops = new List<Pop>();
    /// <summary>闪电预警（十字格闪蓝，最后 0.3 秒变白 + 头上一个闪电图标）、雷云（停在原地的灰云 + 范围圈）、受伤角标飘字。纯画面（H4）。</summary>
    private void StormVisuals()
    {
        int n = 0;
        foreach (var s in town.strikes)
        {
            float k = 1f - s.t / Mathf.Max(0.01f, s.total);
            foreach (var c in OverworldStorm.Plus(map, s.c))
            {
                if (n >= strikeSr.Count) strikeSr.Add(Quad(null, "boltWarn", 0, 0, 0.92f, 0.92f, Color.white, -1390));
                var sr = strikeSr[n++]; sr.sprite = square; sr.enabled = true; sr.transform.position = new Vector3(c.x + 0.5f, c.y + 0.5f, 0); sr.transform.localScale = new Vector3(0.92f, 0.92f, 1);
                float blink = Mathf.PingPong(Time.time * (5f + 14f * k), 1f);
                sr.color = s.White ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.35f, 0.75f, 1f, 0.2f + 0.4f * blink);
            }
            if (n >= strikeSr.Count) strikeSr.Add(Quad(null, "boltIcon", 0, 0, 1, 1, Color.white, 3990));
            var ic = strikeSr[n++]; ic.sprite = IconSprite("Bolt"); ic.enabled = true; ic.color = Color.white;
            ic.transform.position = new Vector3(s.c.x + 0.5f, s.c.y + 1.4f + 0.15f * Mathf.Sin(Time.time * 12f), 0); ic.transform.localScale = new Vector3(0.9f, 0.9f, 1);
        }
        for (int i = n; i < strikeSr.Count; i++) strikeSr[i].enabled = false;
        if (town.cloud != null)
        {
            if (cloudSr == null) { cloudSr = Quad(null, "stormCloud", 0, 0, 1, 1, Color.white, 4050); cloudSr.sprite = IconSprite("Cloud"); }
            float r = tuning.overworldCloudRadius; cloudSr.enabled = true; float left = tuning.overworldCloudSeconds - town.cloud.t; // S222：最后 1.5 秒闪烁 = 快散了（Telegraph：结束也要预告）
            cloudSr.color = new Color(1f, 1f, 1f, left < 1.5f ? 0.35f + 0.5f * Mathf.PingPong(Time.time * 6f, 1f) : 0.85f);
            cloudSr.transform.position = new Vector3((float)town.cloud.x, (float)town.cloud.y + 2.2f, 0); cloudSr.transform.localScale = new Vector3(r * 1.6f, r * 1.6f, 1);
            if (Time.frameCount % 20 == 0) Step1Fx.Ring(new Vector2((float)town.cloud.x, (float)town.cloud.y), r, new Color(0.6f, 0.75f, 1f, 0.6f)); // 范围圈
        }
        else if (cloudSr != null) cloudSr.enabled = false;
        foreach (var h in town.hurts) pops.Add(new Pop { at = new Vector3(h.x, h.y + 1.1f, 0), kind = h.kind, until = Time.unscaledTime + 1.1f });
        pops.RemoveAll(p => Time.unscaledTime > p.until);
    }
    private static readonly string[] PopIcon = { "HurtBadge", "StunBadge", "SlowBadge", "Heart", "Energy" };
    private static readonly string[] PopText = { "-1❤", "晕", "慢", "+1❤", "+1◆" };
    private GUIStyle popStyle, hudStyle;
    private void StormGUI()
    {
        if (popStyle == null) { popStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; hudStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft }; }
        foreach (var p in pops)
        {
            float age = 1.1f - (p.until - Time.unscaledTime);
            var sp = cam.WorldToScreenPoint(p.at + Vector3.up * age * 0.8f); if (sp.z < 0) continue;
            var tex = IconSprite(PopIcon[p.kind])?.texture; float y = Screen.height - sp.y;
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01(2f - age * 1.8f));
            if (tex != null) GUI.DrawTexture(new Rect(sp.x - 28, y - 12, 24, 24), tex);
            popStyle.normal.textColor = p.kind == 0 ? new Color(1f, 0.35f, 0.35f) : p.kind == 1 ? Color.yellow : p.kind == 2 ? new Color(0.5f, 0.75f, 1f) : p.kind == 3 ? new Color(0.5f, 1f, 0.6f) : new Color(0.8f, 0.6f, 1f);
            GUI.Label(new Rect(sp.x - 2, y - 14, 80, 28), PopText[p.kind], popStyle);
        }
        GUI.color = Color.white;
        // 左上：心 + 能量（图标画出来，不靠字体有没有 ♥）
        var rect = new Rect(10, 126, 340, 34); GUI.Box(rect, "", hudStyle);
        var heart = IconSprite("Heart")?.texture; var en = IconSprite("Energy")?.texture;
        float x = 16; GUI.Label(new Rect(x, 130, 60, 26), "马里奥", Step1Gui.Text(13)); x += 52;
        for (int i = 0; i < OverworldSession.MaxHearts; i++) { GUI.color = i < OverworldSession.MarioHearts ? Color.white : new Color(1, 1, 1, 0.18f); if (heart != null) GUI.DrawTexture(new Rect(x, 131, 22, 22), heart); x += 22; }
        GUI.color = Color.white; x += 10; GUI.Label(new Rect(x, 130, 30, 26), "你", Step1Gui.Text(13)); x += 22;
        for (int i = 0; i < OverworldSession.MaxHearts; i++) { GUI.color = i < OverworldSession.YouHearts ? Color.white : new Color(1, 1, 1, 0.18f); if (heart != null) GUI.DrawTexture(new Rect(x, 131, 22, 22), heart); x += 22; }
        x += 10;
        for (int i = 0; i < OverworldSession.MaxEnergy; i++) { GUI.color = i < OverworldSession.Energy ? Color.white : new Color(1, 1, 1, 0.18f); if (en != null) GUI.DrawTexture(new Rect(x, 131, 22, 22), en); x += 22; }
        GUI.color = Color.white;
        if (OverworldSession.Energy >= OverworldSession.MaxEnergy) GUI.Label(new Rect(x + 4, 130, 60, 26), "Q!", Step1Gui.Text(15));
    }

    // ═════════════════════ S218 大机关画面（纯画面：不加碰撞体、不影响他——H4）═════════════════════
    private Texture2D groundTex;
    private readonly Dictionary<int, List<GameObject>> cellGo = new Dictionary<int, List<GameObject>>();
    private readonly List<SpriteRenderer> dangerSr = new List<SpriteRenderer>();
    private SpriteRenderer boulderSr, shellSr;
    private void Keep(int x, int y, SpriteRenderer sr) { int id = y * map.W + x; if (!cellGo.TryGetValue(id, out var l)) cellGo[id] = l = new List<GameObject>(); l.Add(sr.gameObject); }

    private void BigFx()
    {
        foreach (int id in town.changedCells)
        {
            int x = id % map.W, y = id / map.W; char c = map.At(x, y);
            if (cellGo.TryGetValue(id, out var l)) { foreach (var go in l) if (go != null) go.SetActive(false); cellGo.Remove(id); }
            if (groundTex != null) { var t = OverworldCatalog.Get(c) ?? OverworldCatalog.Get('.'); var gc = new Color(t.r, t.g, t.b); if ((x + y) % 2 == 0) gc *= 0.96f; groundTex.SetPixel(x, y, new Color(gc.r, gc.g, gc.b, 1f)); }
        }
        if (town.changedCells.Count > 0) { if (groundTex != null) groundTex.Apply(); minimap = null; } // 小地图下次打开重画
        foreach (var bo in town.bolts) // S219 闪电：一道竖着的白光 + 全屏闪一下（纯画面）
        {
            var bolt = Quad(null, "bolt", bo.x, bo.y + 6f, 0.35f, 12f, new Color(1f, 1f, 0.85f, 0.95f), 4100); Destroy(bolt.gameObject, 0.18f);
            flashUntil = Time.unscaledTime + 0.12f; shake = Mathf.Max(shake, 0.35f);
        }
        foreach (var im in town.impacts)
        {
            Step1Fx.Ring(new Vector2(im.x, im.y), im.z, new Color(1f, 0.85f, 0.4f, 0.9f)); // 冲击环 = 真实的连锁范围（1.5 格）/ 淹没范围
            Step1Fx.Burst(new Vector2(im.x, im.y), 10, new Color(0.7f, 0.6f, 0.45f), 5f, Vector2.zero);
            if (cam != null && im.z >= 1f) shake = Mathf.Max(shake, 0.25f);
        }
    }

    private float shake, flashUntil;
    private readonly List<SpriteRenderer> aimSr = new List<SpriteRenderer>();

    /// <summary>S219：瞄准的落点（你坐炮 = 绿圈 / 打不了 = 红叉；他坐炮 = 红圈，你可以跑过去按 L 拨歪）+ 炮管转向。</summary>
    private void AimVisuals()
    {
        int n = 0;
        foreach (var a in town.Aims())
        {
            Color col = a.mario ? new Color(1f, 0.2f, 0.2f, 0.85f) : a.ok ? new Color(0.3f, 1f, 0.45f, 0.85f) : new Color(1f, 0.3f, 0.2f, 0.85f);
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 10f);
            for (int k = 0; k < 2; k++)
            {
                if (n >= aimSr.Count) aimSr.Add(Quad(null, "aim", 0, 0, 1, 1, col, -1300));
                var sr = aimSr[n++]; sr.enabled = true; sr.color = k == 0 ? col : new Color(col.r, col.g, col.b, 0.25f);
                sr.transform.position = new Vector3(a.land.x + 0.5f, a.land.y + 0.5f, 0); sr.transform.localScale = k == 0 ? new Vector3(0.35f, 0.35f, 1) : new Vector3(1.3f * pulse, 1.3f * pulse, 1);
            }
            // 炮口到落点的虚线（每 1.5 格一个点）
            float lx = a.land.x + 0.5f - (a.k.x + 0.5f), ly = a.land.y + 0.5f - (a.k.y + 0.5f), len = Mathf.Sqrt(lx * lx + ly * ly);
            for (float d = 1.5f; d < len - 0.5f && n < 200; d += 1.5f)
            {
                if (n >= aimSr.Count) aimSr.Add(Quad(null, "aimDot", 0, 0, 0.18f, 0.18f, col, 3950));
                var sr = aimSr[n++]; sr.enabled = true; sr.color = new Color(col.r, col.g, col.b, 0.6f); float u = d / len, arc = 4f * u * (1f - u) * Mathf.Min(4f, len * 0.25f);
                sr.transform.position = new Vector3(a.k.x + 0.5f + lx * u, a.k.y + 0.5f + ly * u + arc, 0); sr.transform.localScale = new Vector3(0.18f, 0.18f, 1);
            }
            // 炮管跟着转
            if (cellGo.TryGetValue(a.k.y * map.W + a.k.x, out var gos)) foreach (var go in gos) if (go != null && go.name == "barrel")
            {
                bool hz = OverworldProps.DX[a.dir] != 0;
                go.transform.localPosition = new Vector3(a.k.x + 0.5f + OverworldProps.DX[a.dir] * 0.55f, a.k.y + 0.6f + OverworldProps.DY[a.dir] * 0.4f, 0);
                go.transform.localScale = new Vector3(hz ? 1.2f : 0.55f, hz ? 0.55f : 1.2f, 1); baseScale.Remove(go);
            }
        }
        for (int i = n; i < aimSr.Count; i++) aimSr[i].enabled = false;
    }
    private readonly Dictionary<GameObject, Vector3> baseScale = new Dictionary<GameObject, Vector3>();

    private void BigVisuals()
    {
        // 预警：危险格闪红（巨炮炮口 / 滚石滚道），机关本身抖
        int n = 0;
        foreach (var b in town.active)
        {
            if (b.fuse > 0f && cellGo.TryGetValue(b.c.y * map.W + b.c.x, out var gos))
            {
                float k = 1f - b.fuse / Mathf.Max(0.01f, b.fuseTotal), pulse = 1f + Mathf.Abs(Mathf.Sin(Time.time * (10f + 25f * k))) * 0.12f * (0.3f + k); // 越来越急（S216 预警规则）
                foreach (var go in gos) if (go != null) { if (!baseScale.TryGetValue(go, out var bs)) baseScale[go] = bs = go.transform.localScale; go.transform.localScale = bs * pulse; }
            }
            if (b.kind == 'U' && b.fuse <= 0f) continue;
            foreach (var c in town.Danger(b))
            {
                if (n >= dangerSr.Count) dangerSr.Add(Quad(null, "danger", 0, 0, 0.9f, 0.9f, Color.red, -1400));
                var sr = dangerSr[n++]; sr.enabled = true; sr.transform.position = new Vector3(c.x + 0.5f, c.y + 0.5f, 0);
                float blink = b.fuse > 0f ? Mathf.PingPong(Time.time * (6f + 10f * (1f - b.fuse / Mathf.Max(0.01f, b.fuseTotal))), 1f) : 0.6f;
                sr.color = new Color(1f, 0.15f, 0.1f, 0.2f + 0.35f * blink);
            }
            if (b.kind == 'U' && b.fuse > 0f) foreach (var c in OverworldProps.Flood(map, b.c, OverworldProps.FloodRadius + (town.weather.kind == OverworldEvents.Kind.Rain ? 1 : 0)))
            {
                if (n >= dangerSr.Count) dangerSr.Add(Quad(null, "danger", 0, 0, 0.9f, 0.9f, Color.red, -1400));
                var sr = dangerSr[n++]; sr.enabled = true; sr.transform.position = new Vector3(c.x + 0.5f, c.y + 0.5f, 0); sr.color = new Color(0.3f, 0.55f, 1f, 0.35f);
            }
        }
        for (int i = n; i < dangerSr.Count; i++) dangerSr[i].enabled = false;
        // 正在滚的石头
        OverworldTown.Big roll = town.active.Find(b => b.rolling);
        if (roll != null) { if (boulderSr == null) boulderSr = Quad(null, "rollingBoulder", 0, 0, 1.25f, 1.2f, new Color(0.56f, 0.53f, 0.49f), 0); boulderSr.enabled = true; boulderSr.transform.position = new Vector3((float)roll.rx, (float)roll.ry + 0.1f, 0); boulderSr.transform.Rotate(0, 0, -720f * Time.deltaTime); boulderSr.sortingOrder = Order(roll.ry) + 3; }
        else if (boulderSr != null) boulderSr.enabled = false;
        // 炮弹 / 被轰飞的人：抛物线（高度只是画面，落点是纯逻辑算好的）
        OverworldTown.Flight shellF = town.flights.Find(f => f.shell);
        if (shellF != null) { if (shellSr == null) shellSr = Quad(null, "shell", 0, 0, 0.5f, 0.5f, new Color(0.15f, 0.15f, 0.18f), 3900); shellSr.enabled = true; shellSr.transform.position = new Vector3((float)shellF.X, (float)shellF.Y + shellF.Arc * 4f, 0); }
        else if (shellSr != null) shellSr.enabled = false;
        foreach (var f in town.flights)
        {
            if (f.mario && marioSr != null) marioSr.transform.localPosition = new Vector3(0, 0.1f + f.Arc * 4f, 0);
            if (f.you && trickSr != null) trickSr.transform.localPosition = new Vector3(0, 0.1f + f.Arc * 4f, 0);
        }
        if (!town.marioFlying && marioSr != null) marioSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        if (!town.youFlying && trickSr != null) trickSr.transform.localPosition = new Vector3(0, 0.1f, 0);
        // 靶心：巨炮在预警时，它的靶心也闪
        foreach (var b in town.active) if (b.kind == 'K' && b.fuse > 0f && OverworldProps.Aim(map, b.c, out var tg, out _, out _) && tileSr.TryGetValue(tg.y * map.W + tg.x, out var tsr)) tsr.color = Mathf.PingPong(Time.time * 8f, 1f) > 0.5f ? Color.white : new Color(0.93f, 0.36f, 0.3f);
    }
    private static float zoom = 7.5f; // S217：- / = 缩放镜头（这次 Play 里记住）
    public const float MinZoom = 4f, MaxZoom = 20f;

    private TextMesh Tag(Transform parent, string text, Color c)
    {
        var go = new GameObject("Tag"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 0.75f, 0);
        var t = go.AddComponent<TextMesh>(); t.text = text; t.fontSize = 48; t.characterSize = 0.05f; t.anchor = TextAnchor.LowerCenter; t.alignment = TextAlignment.Center; t.color = c;
        Step1Gui.ApplyFont(t);
        var r = go.GetComponent<MeshRenderer>(); if (r != null) r.sortingOrder = 4000;
        return t;
    }

    private void ZoomKeys()
    {
        float z = zoom;
        if (Step1Keys.Held(KeyCode.Minus)) z += 8f * Time.unscaledDeltaTime;
        if (Step1Keys.Held(KeyCode.Equals)) z -= 8f * Time.unscaledDeltaTime;
        z = Mathf.Clamp(z, MinZoom, Mathf.Max(MinZoom, Mathf.Min(MaxZoom, map.H / 2f)));
        if (!Mathf.Approximately(z, zoom) && cam != null) { zoom = z; cam.orthographicSize = z; }
    }

    private SpriteRenderer Quad(Transform parent, string name, float x, float y, float w, float h, Color c, int order)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
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
        if (marioTag != null) marioTag.gameObject.SetActive(marioSr.enabled);
        if (youTag != null) youTag.gameObject.SetActive(!disguised); // 伪装时不挂名字（名字是给你看的，不影响他——H4）
        trickGo.position = new Vector3((float)tx, (float)ty, 0);
        trickSr.enabled = !disguised; crateSr.enabled = disguised;
        trickSr.sortingOrder = crateSr.sortingOrder = Order(ty) + 2;
        if (frozen > 0f || town.YouGrace > 0f) trickSr.color = Color.Lerp(new Color(0.22f, 0.4f, 0.92f), Color.white, Mathf.PingPong(Time.time * (frozen > 0f ? 6f : 10f), 1f)); // S221：站起来后的保护期也闪（闪 = 打不到你）
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
            else if (c == '?' || c == '+' || c == '*') kv.Value.enabled = !OverworldSession.UsedCells.Contains(kv.Key);
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
        BigVisuals();
        AimVisuals();
        StormVisuals();
        // S220：受伤无敌期间闪一闪（看得出"刚挨了一下，现在不会再掉心"）
        if (marioSr != null && town.MarioGrace > 0f && !marioInside) marioSr.color = Color.Lerp(new Color(0.9f, 0.18f, 0.16f), Color.white, Mathf.PingPong(Time.time * 8f, 1f) * 0.6f); else if (marioSr != null) marioSr.color = new Color(0.9f, 0.18f, 0.16f);
        // S219：坐在炮里 = 你藏在炮身里（看不见人，只露一个 "你" 字）；他坐炮 = 他也藏进去
        if (town.Seated) { trickSr.enabled = false; crateSr.enabled = false; }
        if (town.MarioSeated) marioSr.enabled = false;

        // 镜头跟你，夹在地图内。S212：按时间平滑（和帧率无关），切换中直接跟住
        var goal = CameraGoal();
        float k = SceneTransit.Busy ? 1f : 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, goal, k);
        if (shake > 0f) { shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime); cam.transform.position += (Vector3)(Random.insideUnitCircle * shake * 0.6f); } // 纯画面
        GuideTick();
    }

    private Vector3 CameraGoal()
    {
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
        float cx = Mathf.Clamp((float)tx, Mathf.Min(halfW, map.W / 2f), Mathf.Max(map.W - halfW, map.W / 2f));
        float cy = Mathf.Clamp((float)ty, Mathf.Min(halfH, map.H / 2f), Mathf.Max(map.H - halfH, map.H / 2f));
        return new Vector3(cx, cy, -10f);
    }

    private void SnapCamera() { if (cam != null) cam.transform.position = CameraGoal(); }

    // ═════════════════════ S212 地图指引 ═════════════════════
    private OverworldGuide.Race race;
    private float raceAt = -1f;
    private List<OverworldMap.Cell> trail;
    private bool showTrail, showMinimap;
    private Texture2D minimap;

    private void GuideTick()
    {
        if (map == null || dayOver) return;
        showTrail = Step1Keys.Held(KeyCode.Tab) && !helpOpen;
        if (Step1Keys.Down(KeyCode.M) && !SceneTransit.Busy) showMinimap = !showMinimap;
        if (Time.unscaledTime < raceAt) return;
        raceAt = Time.unscaledTime + 0.25f; // 4 次/秒就够（寻路很便宜，但没必要每帧）
        var next = NextStop;
        if (next == null || !doorCells.TryGetValue(next.n, out var dc)) { race = default; trail = null; return; }
        var r = OverworldBuilderRules();
        race = OverworldGuide.RaceTo(map, r, dc, next.minute, OverworldSession.Minute, tx, ty, mario.x, mario.y, marioInside);
        trail = OverworldMap.Path(map, OverworldGuide.Near(map, tx, ty), dc);
    }

    private OverworldMap.Rules OverworldBuilderRules() => town.Rules;

    private void GuideGUI(GUIStyle box)
    {
        if (dayOver || helpOpen) return;
        var next = NextStop;
        // 赛跑提示（左上"下一站"下面）
        string rl = Step1Text.OverworldRace(race);
        if (rl.Length > 0)
        {
            var old = GUI.backgroundColor;
            GUI.backgroundColor = race.verdict == OverworldGuide.Verdict.Ahead ? new Color(0.5f, 1f, 0.5f) : race.verdict == OverworldGuide.Verdict.Behind ? new Color(1f, 0.5f, 0.5f) : new Color(1f, 0.9f, 0.4f);
            GUI.Box(new Rect(10, 66, 340, 28), rl, box);
            GUI.backgroundColor = old;
        }
        GUI.Label(new Rect(10, 98, 340, 22), Step1Text.OverworldGuideKeys, Step1Gui.Text(13));
        if (town.timeScale > 1f) GUI.Box(new Rect(Screen.width / 2f - 230, 14, 460, 30), Step1Keys.Held(KeyCode.Space) ? Step1Text.OverworldFastForward : Step1Text.OverworldAutoFast, box);
        if (next == null || !doorCells.TryGetValue(next.n, out var dc)) return;
        // 门头上的倒计时（门在屏幕里才画）
        foreach (var d in stops)
        {
            if (OverworldSession.ResultOf(d.n) != OverworldSession.DoorResult.NotYet || !doorCells.TryGetValue(d.n, out var c)) continue;
            var sp = cam.WorldToScreenPoint(new Vector3(c.x + 0.5f, c.y + 1.3f, 0));
            if (sp.z < 0 || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) continue;
            double secs = (d.minute - OverworldSession.Minute) / Mathf.Max(0.01f, tuning.overworldMinutesPerSecond);
            string label = d.n == next.n ? Step1Text.OverworldDoorCountdown(d.n, secs) : "门 " + d.n + " · " + OverworldMap.Clock(d.minute);
            GUI.Label(new Rect(sp.x - 80, Screen.height - sp.y - 12, 160, 24), label, Step1Gui.Text(d.n == next.n ? 15 : 12, TextAnchor.MiddleCenter));
        }
        // 屏幕边缘箭头 → 下一扇门（门在屏幕里就不画）
        var vp = cam.WorldToViewportPoint(new Vector3(dc.x + 0.5f, dc.y + 0.5f, 0));
        var a = OverworldGuide.EdgeArrow(vp.x, vp.y, 0.06f, vp.z < 0);
        if (!a.onScreen)
        {
            var at = new Vector2(a.x * Screen.width, (1f - a.y) * Screen.height);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(-a.angleDeg, at);
            if (arrowTex == null) arrowTex = MakeArrow(32); // 自己画的三角（默认字体不一定有 ➤ 这个字）
            GUI.color = race.verdict == OverworldGuide.Verdict.Behind ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.55f, 0.85f);
            GUI.DrawTexture(new Rect(at.x - 18, at.y - 18, 36, 36), arrowTex);
            GUI.matrix = m;
            GUI.color = Color.white;
            GUI.Label(new Rect(at.x - 30, at.y + (a.y < 0.5f ? -44 : 18), 60, 22), "门 " + next.n, Step1Gui.Text(13, TextAnchor.MiddleCenter));
        }
        // 按住 Tab：面包屑（你 → 下一扇门的最短路）
        if (showTrail && trail != null)
        {
            GUI.color = new Color(1f, 0.55f, 0.85f, 0.9f);
            for (int i = 1; i < trail.Count; i += 1)
            {
                var p = cam.WorldToScreenPoint(new Vector3(trail[i].x + 0.5f, trail[i].y + 0.5f, 0));
                float s = i == trail.Count - 1 ? 14f : 7f;
                GUI.DrawTexture(new Rect(p.x - s / 2, Screen.height - p.y - s / 2, s, s), nightTex);
            }
            GUI.color = Color.white;
        }
        if (showMinimap) MinimapGUI(dc);
    }

    private Texture2D arrowTex;
    /// <summary>朝右的实心三角（白色，画的时候用 GUI.color 染色），边缘抗锯齿。</summary>
    private static Texture2D MakeArrow(int n)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = (x + 0.5f) / n, v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f; // v：离中线多远 0..1
            float edge = (1f - u) - v * 0.95f;                                    // 三角：尖在右边
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge * n * 0.5f) * (u > 0.08f ? 1f : 0f));
        }
        t.SetPixels(px); t.Apply();
        return t;
    }

    private void MinimapGUI(OverworldMap.Cell dc)
    {
        if (minimap == null)
        {
            minimap = new Texture2D(map.W, map.H) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < map.H; y++) for (int x = 0; x < map.W; x++)
            {
                var t = OverworldCatalog.Get(map.At(x, y)) ?? OverworldCatalog.Get('.');
                minimap.SetPixel(x, y, new Color(t.r, t.g, t.b));
            }
            minimap.Apply();
        }
        float cell = Mathf.Floor(Mathf.Min(Screen.width * 0.6f / map.W, Screen.height * 0.6f / map.H));
        float w = cell * map.W, h = cell * map.H, x0 = (Screen.width - w) / 2f, y0 = (Screen.height - h) / 2f;
        GUI.Box(new Rect(x0 - 8, y0 - 32, w + 16, h + 40), Step1Text.OverworldMinimapTitle);
        GUI.DrawTexture(new Rect(x0, y0, w, h), minimap);
        void Dot(double wx, double wy, Color c, float s)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(x0 + (float)wx * cell - s / 2, y0 + (map.H - (float)wy) * cell - s / 2, s, s), nightTex);
        }
        Dot(dc.x + 0.5, dc.y + 0.5, new Color(1f, 0.4f, 0.8f), cell * 1.6f);
        if (!marioInside) Dot(mario.x, mario.y, new Color(0.95f, 0.15f, 0.15f), cell * 1.2f);
        Dot(tx, ty, new Color(0.25f, 0.5f, 1f), cell * 1.2f);
        GUI.color = Color.white;
    }

    private void UpdateCone()
    {
        if (coneMesh == null) return;
        coneMesh.Clear(); if (fillMesh != null) fillMesh.Clear();
        if (marioInside || dayOver) return;
        float fillN = tuning.visionConeFill && town.mind.Meter.Level != SuspicionLevel.Calm ? town.mind.Meter.Normalized : 0f;

        var r = Sight();
        bool night = r.night;
        float range = (float)(night ? System.Math.Min(r.range, r.nightRange) : r.range);
        float baseAng = Mathf.Atan2((float)mario.fy, (float)mario.fx), half = (float)r.halfAngleDeg * Mathf.Deg2Rad;
        const int seg = 18;
        var v = new Vector3[seg + 2]; var tri = new int[seg * 3];
        Vector3[] fv = fillN > 0f && fillMesh != null ? new Vector3[seg + 2] : null;
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
            if (fv != null) { float fl = Step1Readability.FillReach(fillN, len, range); fv[i + 1] = new Vector3((float)mario.x + dx * fl, (float)mario.y + dy * fl, 0); }
            if (i < seg) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 2; tri[i * 3 + 2] = i + 1; }
        }
        coneMesh.vertices = v; coneMesh.triangles = tri;
        if (fv != null)
        {
            fv[0] = v[0]; fillMesh.vertices = fv; fillMesh.triangles = tri;
            var fc = Step1Readability.FillColor(town.mind.Meter.Level); var cols = new Color[fv.Length]; for (int k = 0; k < cols.Length; k++) cols[k] = fc; fillMesh.colors = cols;
        }
    }

    private GUIStyle boxStyle, bigStyle, markStyle;
    private void OnGUI()
    {
        if (map == null || cam == null) return;
        bool night = OverworldSession.Minute >= OverworldMap.NightStart;
        if (Time.unscaledTime < flashUntil) { GUI.color = new Color(1f, 1f, 0.9f, 0.55f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), nightTex); GUI.color = Color.white; }
        var wk = town.weather.kind;
        if (wk == OverworldEvents.Kind.Rain || wk == OverworldEvents.Kind.Storm || wk == OverworldEvents.Kind.Acid) { GUI.color = wk == OverworldEvents.Kind.Acid ? new Color(0.45f, 0.9f, 0.2f, 0.12f) : new Color(0.3f, 0.4f, 0.6f, wk == OverworldEvents.Kind.Storm ? 0.22f : 0.12f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), nightTex); GUI.color = Color.white; }
        if (night) { GUI.color = new Color(0.05f, 0.08f, 0.25f, 0.35f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), nightTex); GUI.color = Color.white; }

        // S220：样式只建一次（OnGUI 每帧会跑好几次，每次 new GUIStyle = 每帧几十次分配 → 卡顿）
        if (boxStyle == null) { boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft, wordWrap = true }; bigStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true }; markStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; }
        var box = boxStyle; var big = bigStyle;
        // 右上：时钟 + 门列表
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Step1Text.OverworldClock(OverworldMap.Clock(OverworldSession.Minute), night) + "  " + Step1Text.OverworldWeatherShort(OverworldSession.Day, (int)town.weather.kind));
        foreach (var d in stops) sb.AppendLine(Step1Text.OverworldDoorLabel(d.n, OverworldMap.Clock(d.minute), OverworldSession.ResultOf(d.n)));
        sb.Append($"💣 +{OverworldSession.BonusBombs}   T ×{Mathf.Max(0, tuning.overworldTaunts - OverworldSession.TauntsUsed)}");
        GUI.Box(new Rect(Screen.width - 230, 10, 220, 40 + stops.Count * 22 + 22), sb.ToString(), box);
        // 左上：下一站
        var next = NextStop;
        GUI.Box(new Rect(10, 10, 280, 52), next != null ? Step1Text.OverworldNextDoor(next.n, OverworldMap.Clock(next.minute)) : Step1Text.OverworldGoingHome, box);
        // 马里奥头上的标记（? / ! / !! / ?! / OUCH!）
        if (!marioInside && !dayOver && !string.IsNullOrEmpty(lastOrder.mark))
        {
            var sp = cam.WorldToScreenPoint(new Vector3((float)mario.x, (float)mario.y + 1f, 0));
            var ms = markStyle;
            ms.normal.textColor = lastOrder.mark.StartsWith("!") ? Color.red : Color.yellow;
            GUI.Label(new Rect(sp.x - 50, Screen.height - sp.y - 20, 100, 40), lastOrder.mark, ms);
        }
        // 门口提示
        if (next != null && doorCells.TryGetValue(next.n, out var dc) && Dist(dc.x + 0.5, dc.y + 0.5, tx, ty) <= 1.3f && !dayOver)
            GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height - 100, 360, 56), marioInside ? Step1Text.OverworldLateHint : town.AmbushReady ? Step1Text.OverworldAmbushHint : Step1Text.OverworldAmbushCountdown(town.MarioStepsToDoor, tuning.overworldAmbushSteps, disguised), big);
        GuideGUI(box);
        if (!dayOver && !helpOpen) StormGUI();
        if (Time.unscaledTime < hintUntil) GUI.Box(new Rect(Screen.width / 2f - 220, 70, 440, 56), hint, big);
        // S217：底部常驻按键条 + 等他出门的提示 + 没点游戏窗口的提醒
        if (!helpOpen && !dayOver) GUI.Box(new Rect(0, Screen.height - 30, Screen.width, 30), town.Seated ? Step1Text.OverworldCannonBar : Step1Text.OverworldControlsBar, Step1Gui.Text(14, TextAnchor.MiddleCenter, false));
        if (town.Seated && !dayOver) { var a = town.Aims()[0]; GUI.Box(new Rect(Screen.width / 2f - 220, Screen.height - 94, 440, 56), Step1Text.OverworldCannonAim(OverworldProps.DirZh[town.seat.dir], town.seat.dist, a.land.x, a.land.y, a.ok, tuning.overworldCannonSeatSeconds - town.seat.t), big); }
        if (!helpOpen && !dayOver && next != null && OverworldSession.Minute < next.minute && !marioInside && !town.NearDoor(next.n))
        {
            double wait = (next.minute - OverworldSession.Minute) / Mathf.Max(0.01f, tuning.overworldMinutesPerSecond);
            GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height - 94, 460, 56), Step1Text.OverworldWaitDepart(next.n, OverworldMap.Clock(next.minute), wait), big);
        }
        if (helpOpen) GUI.Box(new Rect(Screen.width / 2f - 360, Screen.height / 2f - 210, 720, 420), Step1Text.OverworldHelp + "\n\n" + Step1Text.OverworldHelpClose, box);
        if (!Application.isFocused) GUI.Box(new Rect(Screen.width / 2f - 260, Screen.height / 2f - 40, 520, 80), Step1Text.ClickGameWindow, big);
        if (dayOver) GUI.Box(new Rect(Screen.width / 2f - 280, Screen.height / 2f - 90, 560, 180), OverworldSession.Summary(stops.Count), big);
    }
}
