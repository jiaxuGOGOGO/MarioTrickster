using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S241：房间光影（白天 / 夜晚 / 雨天 / 雨夜 + 马里奥的手电筒）。判断全在 Step1Stealth（纯逻辑），这里只做：
///   ① 每局按调参 lightMode 决定模式（Auto = 第 1 局白天，之后轮换）；
///   ② 收集光源（灯 'i'、正在喷的火、冒烟的炸弹、点燃的油桶）→ IsLit(点) 供 MarioEyes / 遁地土包使用；
///   ③ 画面：低分辨率暗度贴图（你永远看得见整个房间，只是暗处蒙灰）、雨丝、你头上的 暗 / 亮、左上角模式提示。
/// 没有这个组件时（旧场景、编辑器测试）IsLit 一律返回 true = 行为和以前完全一样。
/// H4：马里奥那边只问"这个点亮不亮"（地形 + 光源 + 他自己的手电筒），从不问"你在哪"。
/// 参考：Mark of the Ninja（光二值、你看得见他的视野）、Among Us 关灯（视野缩小）。只借规则，不借素材。
/// 由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class Step1Lighting : MonoBehaviour
{
    public static Step1Lighting Current { get; private set; }
    public Step1LightMode Mode { get; private set; } = Step1LightMode.Day;
    public bool Dark => Step1Stealth.IsDark(Mode);
    public bool Rain => Step1Stealth.IsRain(Mode);

    private MarioMindTuningSO tuning;
    private GameManager manager;
    private MarioController mario;
    private TricksterController you;
    private string[] grid;
    private int w, h;
    private readonly List<Step1Stealth.Light> lights = new List<Step1Stealth.Light>();
    private float lightsAge = 99f;
    private Texture2D shade; private SpriteRenderer shadeSr; private Color32[] shadePx;
    private float shadeAge = 99f;
    private readonly List<Transform> drops = new List<Transform>();
    private float bannerUntil;
    private int round;

    /// <summary>这一点亮不亮（没有光影组件 = 永远亮）。</summary>
    public static bool IsLit(Vector2 p)
    {
        var c = Current;
        return c == null || c.LitAt(p);
    }

    /// <summary>马里奥（或任何人）能不能看清这一点：亮，或者贴身。</summary>
    public static bool Visible(Vector2 eye, Vector2 p)
    {
        var c = Current;
        if (c == null) return true;
        return Step1Stealth.Visible(c.LitAt(p), Vector2.Distance(eye, p), c.tuning.darkSeeRadius);
    }

    public static bool Raining => Current != null && Current.Rain;

    private void Awake() { Current = this; }
    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (manager != null) manager.OnRoundStart -= NewRound;
    }

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        manager = GameManager.Instance;
        if (manager != null) manager.OnRoundStart += NewRound;
        mario = FindObjectOfType<MarioController>();
        you = FindObjectOfType<TricksterController>();
        if (you != null) // S241：遁地 / 蛛丝 / 脚步挂到捣蛋者身上（旧场景不用重建）
        {
            if (you.GetComponent<TricksterBurrow>() == null) you.gameObject.AddComponent<TricksterBurrow>();
            if (you.GetComponent<TricksterSilk>() == null) you.gameObject.AddComponent<TricksterSilk>();
            if (you.GetComponent<TricksterFootsteps>() == null) you.gameObject.AddComponent<TricksterFootsteps>();
            if (you.GetComponent<TricksterLoadout>() == null && you.GetComponent<DisguiseSystem>() != null) you.gameObject.AddComponent<TricksterLoadout>(); // S242：伪装装备栏
        }
        var src = Step1PrankRoomBuilderBridge.CurrentRoom; // 复制一份再去槽位（别改共享的房间表）
        grid = src != null ? new string[src.Length] : null;
        if (grid != null) for (int i = 0; i < grid.Length; i++) grid[i] = Step1Layout.StripSlots(src[i]);
        h = grid != null ? grid.Length : 0; w = 0; if (grid != null) foreach (var r in grid) w = Mathf.Max(w, r.Length);
        round = manager != null ? Mathf.Max(1, manager.CurrentRound) : 1;
        Mode = Step1Stealth.Resolve(tuning.lightMode, round);
        BuildShade();
        bannerUntil = Time.time + 4f;
    }

    private void NewRound()
    {
        round++;
        if (manager != null && manager.CurrentRound > 0) round = manager.CurrentRound;
        Mode = Step1Stealth.Resolve(tuning.lightMode, round);
        bannerUntil = Time.time + 4f; lightsAge = 99f; shadeAge = 99f;
    }

    private bool FlashlightOn => Dark && mario != null;

    private bool LitAt(Vector2 p)
    {
        if (!Dark) return true;
        if (lightsAge > 0.1f) CollectLights();
        Vector2 eye = mario != null ? (Vector2)mario.transform.position + Vector2.up * tuning.eyeHeight : p;
        bool fr = mario == null || mario.IsFacingRight;
        return Step1Stealth.Lit(true, p, lights, FlashlightOn, eye, fr, tuning.flashlightRange, tuning.flashlightHalfAngle, grid);
    }

    private void CollectLights()
    {
        lightsAge = 0f; lights.Clear();
        foreach (var l in RoomLamp.All) if (l != null && l.On) lights.Add(new Step1Stealth.Light(l.transform.position, tuning.lampRadius));
        foreach (var f in FireTrapCache.All) if (f != null && f.isActiveAndEnabled && f.IsFiring) lights.Add(new Step1Stealth.Light(f.transform.position, tuning.fireLightRadius));
        foreach (var b in TricksterBomb.Live) if (b != null) lights.Add(new Step1Stealth.Light(b.transform.position, tuning.fireLightRadius));
        foreach (var o in OilBarrel.All) if (o != null && o.Lit && !o.Exploded) lights.Add(new Step1Stealth.Light(o.transform.position, tuning.fireLightRadius));
    }

    private void Update()
    {
        lightsAge += Time.deltaTime; shadeAge += Time.deltaTime;
        if (Dark && shadeAge > 0.08f) { shadeAge = 0f; RepaintShade(); }
        if (shadeSr != null) shadeSr.enabled = Dark && !Step1HandsOffCheck.IsRunning;
        UpdateRain();
    }

    // ── 画面：暗度贴图（每格 2×2 像素，最近邻放大 = 像素风的光斑）────────
    private const int Sub = 2;
    private void BuildShade()
    {
        if (w <= 0 || h <= 0) return;
        shade = new Texture2D(w * Sub, h * Sub, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        shadePx = new Color32[w * Sub * h * Sub];
        var go = new GameObject("S241_NightShade");
        go.transform.SetParent(transform, false);
        // 网格 (x,y) 中心在世界 (x,y)；贴图左下角 = (-0.5,-0.5)
        go.transform.position = new Vector3(w * 0.5f - 0.5f, h * 0.5f - 0.5f, -1f);
        shadeSr = go.AddComponent<SpriteRenderer>();
        shadeSr.sprite = Sprite.Create(shade, new Rect(0, 0, w * Sub, h * Sub), new Vector2(0.5f, 0.5f), Sub);
        shadeSr.sortingOrder = 40;
        shadeSr.enabled = false;
    }

    private void RepaintShade()
    {
        if (shade == null) return;
        byte dark = (byte)(Mathf.Clamp01(tuning.nightDarkness) * 255f);
        for (int y = 0; y < h * Sub; y++)
            for (int x = 0; x < w * Sub; x++)
            {
                var p = new Vector2((x + 0.5f) / Sub - 0.5f, (y + 0.5f) / Sub - 0.5f);
                bool lit = LitAt(p);
                shadePx[y * w * Sub + x] = lit ? new Color32(0, 0, 0, 0) : new Color32(6, 8, 24, dark);
            }
        shade.SetPixels32(shadePx); shade.Apply(false);
    }

    // ── 雨丝（纯画面）──────────────────────────────
    private void UpdateRain()
    {
        bool on = Rain && w > 0 && LaunchFeel.fx && !Step1HandsOffCheck.IsRunning;
        int want = on ? Mathf.Clamp(w * 2, 20, 120) : 0;
        while (drops.Count < want)
        {
            var d = new GameObject("S241_Rain");
            d.transform.SetParent(transform, false);
            var sr = d.AddComponent<SpriteRenderer>(); sr.sprite = Step1Sprites.Square; sr.color = new Color(0.6f, 0.75f, 1f, 0.45f); sr.sortingOrder = 41;
            d.transform.localScale = new Vector3(0.05f, 0.45f, 1f);
            d.transform.position = new Vector3(Random.Range(0f, w), Random.Range(0f, h), -1f);
            drops.Add(d.transform);
        }
        while (drops.Count > want) { var d = drops[drops.Count - 1]; drops.RemoveAt(drops.Count - 1); if (d != null) Destroy(d.gameObject); }
        foreach (var d in drops)
        {
            if (d == null) continue;
            var p = d.position; p.y -= 14f * Time.deltaTime; p.x -= 2f * Time.deltaTime;
            if (p.y < -1f) { p.y = h; p.x = Random.Range(0f, w); }
            d.position = p;
        }
    }

    private void OnGUI()
    {
        if (Step1HandsOffCheck.IsRunning || Step1Screen.HelpOpen) return;
        float sw = Step1Gui.Begin();
        if (Time.time < bannerUntil)
        {
            string msg = Mode == Step1LightMode.Night ? Step1Text.LightNight : Mode == Step1LightMode.Rain ? Step1Text.LightRain : Mode == Step1LightMode.NightRain ? Step1Text.LightNightRain : Step1Text.LightDay;
            var r = new Rect(sw * 0.5f - 330f, 110f, 660f, 40f);
            Step1Gui.Panel(r, 0.7f);
            GUI.Label(r, msg, Step1Gui.Text(20, TextAnchor.MiddleCenter, false));
        }
        else
        {
            GUI.Label(new Rect(20, 60, 200, 28), $"<color=#CCCCCC>{Step1Stealth.ModeName(Mode)}</color>", Step1Gui.Text(18));
        }
        // 你头上：暗 / 亮（只在夜里有意义）
        if (Dark && you != null && Camera.main != null && you.isActiveAndEnabled)
        {
            float scale = Mathf.Max(0.1f, Screen.height / Step1Gui.VirtualHeight);
            Vector3 sp = Camera.main.WorldToScreenPoint(you.transform.position + Vector3.up * 1.25f);
            if (sp.z >= 0f)
            {
                bool lit = LitAt(you.transform.position);
                var r = new Rect(sp.x / scale - 18f, (Screen.height - sp.y) / scale - 14f, 36f, 26f);
                Step1Gui.Panel(r, 0.55f);
                GUI.Label(r, lit ? $"<color=#FFE070>{Step1Text.YouInLight}</color>" : $"<color=#8FA0FF>{Step1Text.YouInDark}</color>", Step1Gui.Text(16, TextAnchor.MiddleCenter, false));
            }
        }
    }
}
