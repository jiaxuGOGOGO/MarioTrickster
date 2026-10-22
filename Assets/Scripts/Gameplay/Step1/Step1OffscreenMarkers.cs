using UnityEngine;

/// <summary>
/// S207：大房间（镜头看不到全图）时，马里奥 / 宝物 / 出口在屏幕外 → 屏幕边缘画箭头指过去（带距离、马里奥头顶的 ? ! !! ?!）。
/// 用户要"像死亡细胞那样不局限于小框"：镜头跟着人走以后，你看不到马里奥 → 必须知道他在哪、有没有起疑（H6：静音也看得懂）。
/// 纯显示，不影响玩法；马里奥侧不读这里（H4 不相关：这是给玩家看的）。自动检查 / 回放时不画。
/// </summary>
public class Step1OffscreenMarkers : MonoBehaviour
{
    private MarioMindTuningSO tuning;
    private Step1RoomCamera roomCam;
    private MarioMindDriver driver;
    private Transform mario, trickster;
    private Vector2? loot, exit;
    private float refind;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom;
        if (rows != null) { loot = Step1MiniMap.CellOf(rows, 'o'); exit = Step1MiniMap.CellOf(rows, 'G'); }
        Find();
    }

    private void Find()
    {
        roomCam = FindObjectOfType<Step1RoomCamera>();
        driver = FindObjectOfType<MarioMindDriver>();
        var m = FindObjectOfType<MarioController>(); mario = m != null ? m.transform : null;
        var t = FindObjectOfType<TricksterController>(); trickster = t != null ? t.transform : null;
    }

    private void Update()
    {
        refind -= Time.unscaledDeltaTime;
        if (refind <= 0f && (mario == null || roomCam == null)) { Find(); refind = 1f; }
    }

    /// <summary>纯逻辑：视口坐标（0..1，z&lt;0 = 在镜头后面）→ 是否在屏幕外。margin = 贴边多少也算"看不清"。</summary>
    public static bool Offscreen(Vector3 viewport, float margin = 0.02f) =>
        viewport.z < 0f || viewport.x < margin || viewport.x > 1f - margin || viewport.y < margin || viewport.y > 1f - margin;

    /// <summary>纯逻辑：屏幕外目标 → 箭头放在屏幕边缘哪一点（视口坐标，沿"屏幕中心 → 目标"方向与边框的交点，留 inset 边距）。</summary>
    public static Vector2 EdgePoint(Vector2 viewport, float inset)
    {
        Vector2 d = viewport - new Vector2(0.5f, 0.5f);
        if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
        float half = 0.5f - inset;
        float k = Mathf.Min(Mathf.Abs(d.x) > 1e-6f ? half / Mathf.Abs(d.x) : float.MaxValue, Mathf.Abs(d.y) > 1e-6f ? half / Mathf.Abs(d.y) : float.MaxValue);
        return new Vector2(0.5f, 0.5f) + d * k;
    }

    /// <summary>纯逻辑：马里奥头顶符号（与头顶文字同一含义，H6 一种信号一种含义）。</summary>
    public static string MarkOf(MarioMindState s)
    {
        switch (s)
        {
            case MarioMindState.Curious: return "?";
            case MarioMindState.Investigating: return "!";
            case MarioMindState.Chasing: return "!!";
            case MarioMindState.Searching: return "?!";
            default: return "";
        }
    }

    private void OnGUI()
    {
        if (tuning == null || !tuning.offscreenMarkers || Step1HandsOffCheck.IsRunning || ChainReplay.Playing || Step1Screen.HelpOpen) return;
        var cam = Camera.main;
        if (cam == null || roomCam == null || roomCam.SeesWholeRoom) return;
        float w = Step1Gui.Begin(), h = Step1Gui.VirtualHeight;
        Vector2 me = trickster != null && trickster.gameObject.activeInHierarchy ? (Vector2)trickster.position : (Vector2)cam.transform.position;
        if (mario != null && mario.gameObject.activeInHierarchy)
        {
            string mark = driver != null && driver.Mind != null ? MarkOf(driver.Mind.State) : "";
            string c = mark == "!!" ? "#FF4040" : mark == "" ? "#FF8A80" : "#FFD54F";
            Draw(cam, mario.position, w, h, $"<color={c}><b>马里奥 {mark}</b></color>", new Color(1f, 0.35f, 0.3f), me);
        }
        if (loot.HasValue && !LootObjective.IsLootCarried) Draw(cam, loot.Value, w, h, "<color=#FFD54F>宝物</color>", new Color(1f, 0.8f, 0.2f), me);
        if (exit.HasValue && LootObjective.IsLootCarried) Draw(cam, exit.Value, w, h, "<color=#9CFF9C>出口</color>", new Color(0.5f, 1f, 0.5f), me);
    }

    private static void Draw(Camera cam, Vector3 world, float w, float h, string label, Color color, Vector2 me)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        if (!Offscreen(vp)) return;
        if (vp.z < 0f) vp = new Vector3(1f - vp.x, 1f - vp.y, 0f);
        Vector2 e = EdgePoint(vp, 0.045f);
        var at = new Vector2(e.x * w, (1f - e.y) * h);
        Vector2 dir = ((Vector2)vp - new Vector2(0.5f, 0.5f)).normalized;
        string arrow = Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? (dir.x > 0 ? "▶" : "◀") : (dir.y > 0 ? "▲" : "▼");
        int dist = Mathf.RoundToInt(Vector2.Distance(me, world));
        var r = new Rect(at.x - 90f, at.y - 26f, 180f, 52f);
        Step1Gui.Panel(r, 0.55f);
        var old = GUI.color; GUI.color = color;
        GUI.Label(new Rect(r.x, r.y, 36f, r.height), arrow, Step1Gui.Text(30, TextAnchor.MiddleCenter, false));
        GUI.color = old;
        GUI.Label(new Rect(r.x + 34f, r.y, r.width - 38f, r.height), $"{label} <size=16><color=#CCCCCC>{dist} 格</color></size>", Step1Gui.Text(20, TextAnchor.MiddleLeft, false));
    }
}
