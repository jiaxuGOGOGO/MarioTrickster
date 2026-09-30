using UnityEngine;

/// <summary>
/// S207：大房间小地图（右上角）。镜头看不到整张图时才显示：墙/地面缩略图 + 你（蓝）/ 马里奥（红）/ 宝物（金）/ 出口（绿）+ 当前镜头框（白）。
/// 用户要"像死亡细胞那样"——死亡细胞也是一边跟着人走、一边有小地图。纯显示；缩略图在开局生成一次（不在 Update 里算）。
/// 按 M / Tab 打开图例时小地图一起放大 2 倍（不占新键：N 已是"下一局"）。
/// </summary>
public class Step1MiniMap : MonoBehaviour
{
    private MarioMindTuningSO tuning;
    private Step1RoomCamera roomCam;
    private Transform mario, trickster;
    private Texture2D tex;
    private int gw, gh;
    private Vector2? loot, exit;
    private float refind;

    public void SetTuning(MarioMindTuningSO t) { tuning = t; }

    /// <summary>纯逻辑：模板里某字符的格坐标（x 从左 0，y 从下 0）。</summary>
    public static Vector2? CellOf(string[] rows, char c)
    {
        for (int r = 0; r < rows.Length; r++) { int x = rows[r].IndexOf(c); if (x >= 0) return new Vector2(x, rows.Length - 1 - r); }
        return null;
    }

    /// <summary>纯逻辑：小地图放在右上角，按房间比例缩放到 maxW × maxH 以内（每格至少 1 像素的比例）。</summary>
    public static Rect Layout(float screenW, int cellsW, int cellsH, float maxW, float maxH, float margin)
    {
        float k = Mathf.Min(maxW / Mathf.Max(1, cellsW), maxH / Mathf.Max(1, cellsH));
        float w = cellsW * k, h = cellsH * k;
        return new Rect(screenW - margin - w, margin, w, h);
    }

    /// <summary>纯逻辑：世界坐标（格中心为整数）→ 小地图里的点。</summary>
    public static Vector2 ToMap(Rect map, int cellsW, int cellsH, Vector2 world) =>
        new Vector2(map.x + (world.x + 0.5f) / cellsW * map.width, map.y + (1f - (world.y + 0.5f) / cellsH) * map.height);

    private void Start()
    {
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom;
        if (rows == null || rows.Length == 0) { enabled = false; return; }
        gh = rows.Length; gw = rows[0].Length;
        loot = CellOf(rows, 'o'); exit = CellOf(rows, 'G');
        var reg = AsciiElementRegistry.GetDefault();
        tex = new Texture2D(gw, gh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int r = 0; r < gh; r++)
            for (int x = 0; x < gw; x++)
            {
                char c = x < rows[r].Length ? rows[r][x] : '.';
                Color col = reg.IsSolid(c) ? new Color(0.62f, 0.58f, 0.72f, 0.95f) : c == '-' ? new Color(0.62f, 0.58f, 0.72f, 0.6f) : new Color(0.08f, 0.07f, 0.12f, 0.75f);
                tex.SetPixel(x, gh - 1 - r, col);
            }
        tex.Apply();
        Find();
    }

    private void Find()
    {
        roomCam = FindObjectOfType<Step1RoomCamera>();
        var m = FindObjectOfType<MarioController>(); mario = m != null ? m.transform : null;
        var t = FindObjectOfType<TricksterController>(); trickster = t != null ? t.transform : null;
    }

    private void Update()
    {
        refind -= Time.unscaledDeltaTime;
        if (refind <= 0f && (mario == null || roomCam == null)) { Find(); refind = 1f; }
    }

    private void OnDestroy() { if (tex != null) Destroy(tex); }

    private void OnGUI()
    {
        if (tex == null || tuning == null || !tuning.miniMap || Step1HandsOffCheck.IsRunning || ChainReplay.Playing || Step1Screen.HelpOpen) return;
        if (roomCam == null || roomCam.SeesWholeRoom) return;
        float w = Step1Gui.Begin();
        bool big = Step1MapLegend.Visible;
        var map = Layout(w, gw, gh, big ? 760f : 380f, big ? 300f : 150f, 16f);
        map.y += 150f; // 让开右上角的连锁提示
        Step1Gui.Panel(new Rect(map.x - 6f, map.y - 26f, map.width + 12f, map.height + 32f), 0.6f);
        GUI.Label(new Rect(map.x, map.y - 26f, map.width, 24f), "<size=15><color=#BBBBBB>小地图  Map</color></size>", Step1Gui.Text(15, TextAnchor.MiddleLeft, false));
        GUI.DrawTexture(map, tex);
        var v = roomCam.ViewRect;
        Vector2 a = ToMap(map, gw, gh, new Vector2(v.xMin - 0.5f, v.yMax - 0.5f)), b = ToMap(map, gw, gh, new Vector2(v.xMax - 0.5f, v.yMin - 0.5f));
        var view = Rect.MinMaxRect(Mathf.Max(map.xMin, a.x), Mathf.Max(map.yMin, a.y), Mathf.Min(map.xMax, b.x), Mathf.Min(map.yMax, b.y));
        Outline(view, new Color(1f, 1f, 1f, 0.8f));
        if (exit.HasValue) Dot(map, exit.Value, new Color(0.4f, 1f, 0.5f), 7f);
        if (loot.HasValue && !LootObjective.IsLootCarried) Dot(map, loot.Value, new Color(1f, 0.8f, 0.2f), 7f);
        if (mario != null && mario.gameObject.activeInHierarchy) Dot(map, mario.position, new Color(1f, 0.3f, 0.3f), 9f);
        if (trickster != null && trickster.gameObject.activeInHierarchy) Dot(map, trickster.position, new Color(0.35f, 0.6f, 1f), 9f);
    }

    private void Dot(Rect map, Vector2 world, Color c, float size)
    {
        var p = ToMap(map, gw, gh, world);
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void Outline(Rect r, Color c)
    {
        var old = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1.5f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, 1.5f, r.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
