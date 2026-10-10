using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S243：把 Step1Art（AI 生成的原创像素素材）穿到房间上——只换外观，不碰碰撞体 / 判定 / 玩法（H3）。
/// ① 角色：马里奥 = 红帽寻宝人、捣蛋者 = 蓝色小恶魔，站 / 跑 / 跳 / 晕 自动换帧。伪装中不动捣蛋者的图（DisguiseSystem 在管）。
/// ② 机关 / 道具：还是白盒（4×4 白方块）的整块换成像素图，颜色恢复白色（原来靠色块颜色认东西，现在靠形状）；换图后原来叠在头上的小图标隐藏，免得一个东西两张图。
/// ③ 地形：地面 / 墙 / 单向板平铺像素图块（最上层带草）。
/// ④ 背景：房间后面一张 64×36 低对比图，无描边、明度居中——主层的深描边才最显眼（参考见 Step1Art）。
/// 每项可在调参 artCharacters / artProps / artTiles / artBackground 里单独关掉；S244 动作画面（扬尘 / 落地压扁 / 晕星星 / 变身烟）= artJuice。Resources/Step1Art/&lt;Key&gt;.png 放同名图 = 覆盖。
/// 由房间生成器挂到 GameManager 上，并且在所有脚本的 Awake 之前跑（DefaultExecutionOrder -1000）：机关 / 角色在 Awake 里记"原来的颜色"，
/// 先换好图、把颜色改白，它们记下的就是白色 → 预警闪烁、用光变灰、受伤闪都照常，不会把像素图染成红块。BuilderVersion 24 → ▶ 试玩会自动重建一次场景。H4：这里只读马里奥的移动状态拿来选帧，不改任何决策。
/// </summary>
[DefaultExecutionOrder(-1000)]
public class Step1ArtSkin : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private MarioMindTuningSO tuning;
    private MarioController mario; private SpriteRenderer marioSr;
    private TricksterController you; private SpriteRenderer youSr;
    private float poofUntil;
    private bool youWasDisguised;
    // S244：动作的"手感画面"（纯外观）：跑步扬尘、落地扬尘 + 压扁回弹、被晕头上转星星、变身冒烟粒子
    private Rigidbody2D youRb;
    private float marioDustAt, youDustAt, marioFall, youFall, squashT = -1f;
    private bool marioWasGrounded = true, youWasGrounded = true;
    private Transform stars; private float starsSpin;
    private Vector3 squashBase = Vector3.one; private Transform squashFor;

    /// <summary>16 像素 = 1 格；pivot 在脚底（角色）或正中（其他）。</summary>
    public static Sprite Get(string key, bool feetPivot)
    {
        string ck = key + (feetPivot ? "#feet" : "");
        if (cache.TryGetValue(ck, out var sp) && sp != null) return sp;
        // S245：① 素材包（MarioTrickster → 美术 Art → 🎨 素材包）里拖了图 = 用它（换画风不改代码、不改关卡）
        var kitSp = ArtKit.SpriteOf(key);
        if (kitSp != null) { if (kitSp.texture != null) kitSp.texture.filterMode = FilterMode.Point; cache[ck] = kitSp; return kitSp; }
        // ② Resources/Step1Art/<名字>.png 同名覆盖（S243 起）；小镇图块也认 Resources/OverworldArt/<名字>.png
        Texture2D tex = Resources.Load<Texture2D>("Step1Art/" + key);
        if (tex == null && key.Length > 1 && key[0] == 'T' && WorldArt.Town.ContainsKey(key)) tex = Resources.Load<Texture2D>("OverworldArt/" + key);
        if (tex == null)
        {
            string[] rows = null;
            if (Step1Art.Frames.TryGetValue(key, out var f)) rows = f;
            else if (Step1Art.Tiles.TryGetValue(key, out var t)) rows = t;
            else if (Step1Art.Icons.TryGetValue(key, out var ic)) rows = ic;
            else rows = WorldArt.Rows(key); // S245：小镇 / 装饰 / 天气技能 / 工坊元素
            if (rows == null && OverworldArt.Icons.TryGetValue(key, out var ow)) rows = ow;
            if (rows == null && ArtKitRules.SlotIndex(key) >= 0) return Get(ArtKitRules.SlotDefaultArt[ArtKitRules.SlotIndex(key)], feetPivot); // 素材槽没配图 = 用默认长相
            if (rows == null) return null;
            int w = rows[0].Length, h = rows.Length;
            var px = Step1Art.Rgba(rows);
            tex = MakeTex(w, h, px);
        }
        tex.filterMode = FilterMode.Point;
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), feetPivot ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f), Step1Art.Size, 0, SpriteMeshType.FullRect); // FullRect：地形要平铺（Tiled）
        cache[ck] = sp; return sp;
    }

    /// <summary>S245：技能特效图（炸弹 / 炮弹 / 土包 / 钩爪 / 挑衅气泡）。调参 artSkillFx 关了 = null（用回原来的色块）。</summary>
    public static Sprite SkillSprite(string key) { var t = MarioMindTuningSO.LoadOrDefault(); return t != null && t.artSkillFx ? Get(key, false) : null; }

    /// <summary>S245：素材包改了 / 重新加载 → 清掉缓存（下次 Get 重新取）。</summary>
    public static void ClearCache() { cache.Clear(); }

    private static Texture2D MakeTex(int w, int h, float[] px)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var cols = new Color[w * h];
        for (int i = 0; i < cols.Length; i++) cols[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
        tex.SetPixels(cols); tex.Apply();
        return tex;
    }

    private void Awake()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        if (tuning == null) return;
        mario = FindObjectOfType<MarioController>();
        you = FindObjectOfType<TricksterController>();
        if (you != null) youRb = you.GetComponent<Rigidbody2D>();
        if (tuning.artCharacters)
        {
            marioSr = SkinCharacter(mario != null ? mario.transform : null, "Hero0");
            youSr = SkinCharacter(you != null ? you.transform : null, "Imp0");
        }
        var root = GameObject.Find("Step1_PrankRoom");
        if (root != null)
        {
            if (tuning.artTiles) SkinTiles(root.transform);
            if (tuning.artProps) SkinProps(root.transform);
            ArtKitZone.AttachAll(root.transform); // S245：素材包里给任何元素配了互动 → 自动挂上（不只 z Z a r 四个槽）
        }
        if (tuning.artBackground) BuildBackground();
        if (tuning.artDecorDensity > 0f) BuildDecor(); // S245：房间装饰（背景层）
    }

    /// <summary>S245：房间装饰（画 / 窗 / 挂旗 / 座钟 / 书架 / 蛛网 / 吊灯 / 烛台 / 盆栽 / 盔甲 / 木桶 / 地毯）——背景层：压暗 35%、排在地形后面、没有碰撞体（H6：主层的东西才显眼，不挡机关）。
    /// 位置 = WorldArt.Dressing（纯函数：同一个房间每次都一样；离机关 / 出生点 / 宝物至少 1 格）。</summary>
    private void BuildDecor()
    {
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom; if (rows == null) return;
        bool outdoor = WorldArt.Outdoor(tuning.artBackdrop, tuning.themePreset, rows);
        var root = new GameObject("S245_Decor"); root.transform.SetParent(transform, false);
        int seed = 0; foreach (var r in rows) foreach (char c in r) seed = seed * 31 + c;
        foreach (var d in WorldArt.Dressing(rows, seed, tuning.artDecorDensity, outdoor))
        {
            var sp = Get(d.key, false); if (sp == null) continue;
            var go = new GameObject(d.key); go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(d.x, d.y, 4f);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = -60; sr.color = new Color(0.65f, 0.65f, 0.72f, 1f); // 压暗 = 背景
        }
    }

    private void Update()
    {
        if (animated.Count == 0) return;
        for (int i = 0; i < animated.Count; i++) { var a = animated[i]; if (a.sr == null) continue; var f = a.frames[(int)(Time.time * a.fps) % a.frames.Length]; if (f != null && a.sr.sprite != f) a.sr.sprite = f; }
    }

    /// <summary>角色：白方块 → 第一帧，颜色改白（保留透明度，受伤闪烁照常）。DisguiseSystem 记的"原图"一起换掉，变回来不会变回方块。</summary>
    private SpriteRenderer SkinCharacter(Transform who, string key)
    {
        if (who == null) return null;
        var sr = who.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null || !Step1PropIcons.IsWhiteBox((int)sr.sprite.rect.width, (int)sr.sprite.rect.height)) return null; // 已经换过美术的不动
        var sp = Get(key, false); if (sp == null) return null;
        // 原来：4×4 白图 = 1 格、pivot 在中间。新图 16px = 1 格、pivot 也在中间 → 和原来的方块占同一块地方（碰撞体 / 视碰对齐都不变）
        sr.sprite = sp;
        var c = sr.color; sr.color = new Color(1f, 1f, 1f, c.a); // PlayerHealth / DisguiseSystem 在 Awake 里才记原图和原色 → 记下的就是新图 + 白色
        return sr;
    }

    private void SkinTiles(Transform root)
    {
        // S247：按房间字符画判断每条地面头顶是露天 / 有楼板 / 被埋住，并区分室内外——草只长在户外露天的地面上。
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom;
        bool outdoor = WorldArt.Outdoor(tuning.artBackdrop, tuning.themePreset, rows);
        var registry = AsciiElementRegistry.GetDefault();
        System.Func<char, bool> isSolid = c => { var e = registry != null ? registry.GetEntry(c) : null; return e != null ? e.isSolid : (c == '#' || c == '=' || c == 'W'); };
        var solid = new HashSet<Vector2Int>();
        foreach (Transform ch in root)
        {
            string n = ch.name;
            if (!(n.StartsWith("Ground_") || n.StartsWith("Wall_") || n.StartsWith("Platform_"))) continue;
            var col = ch.GetComponent<BoxCollider2D>(); if (col == null) continue;
            int w = Mathf.Max(1, Mathf.RoundToInt(col.size.x)); int x0 = Mathf.RoundToInt(ch.position.x - (w - 1) * 0.5f), y = Mathf.RoundToInt(ch.position.y);
            for (int i = 0; i < w; i++) solid.Add(new Vector2Int(x0 + i, y));
        }
        foreach (Transform ch in root)
        {
            var vis = ch.Find("Visual"); if (vis == null) continue;
            var sr = vis.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || !Step1PropIcons.IsWhiteBox((int)sr.sprite.rect.width, (int)sr.sprite.rect.height)) continue;
            var col = ch.GetComponent<BoxCollider2D>();
            int w = col != null ? Mathf.Max(1, Mathf.RoundToInt(col.size.x)) : 1;
            int x0 = Mathf.RoundToInt(ch.position.x - (w - 1) * 0.5f), y = Mathf.RoundToInt(ch.position.y);
            bool air = false; for (int i = 0; i < w; i++) if (!solid.Contains(new Vector2Int(x0 + i, y + 1))) air = true;
            string key = rows != null
                ? Step1Art.TileFor(ch.name, Step1Art.StripCover(rows, x0, w, y, isSolid), outdoor)
                : Step1Art.TileFor(ch.name, air); // 找不到房间字符画（旧场景）= 旧规则
            if (key == null) continue;
            var sp = Get(key, false); if (sp == null) continue;
            // 平铺：Tiled 模式下 size = 世界尺寸、localScale 必须是 1（Destructible 炸开时按 localScale 切块 → 切块也用同一张图平铺）
            var ls = vis.localScale;
            sr.sprite = sp; sr.color = Color.white; sr.drawMode = SpriteDrawMode.Tiled;
            if (key == "Platform")
            {
                // S244：单向板以前一直是 1 格 × 0.3 的蓝色细条（S243 漏了：名字是 OneWayPlatform_ 开头）。换成 16×8 的浅色木板（0.5 格高，和土色地面拉开），
                // 上表面保持不变（= 碰撞体顶），多出来的厚度往下长——脚踩的地方一点没变（H3）。
                float newH = sp.bounds.size.y;
                sr.size = new Vector2(Mathf.Abs(ls.x), newH);
                vis.localPosition = vis.localPosition + new Vector3(0f, Step1Art.TopKeep(Mathf.Abs(ls.y), newH), 0f);
            }
            else sr.size = new Vector2(Mathf.Abs(ls.x), Mathf.Abs(ls.y));
            vis.localScale = Vector3.one;
        }
    }

    private void SkinProps(Transform root)
    {
        bool outdoor = WorldArt.Outdoor(tuning.artBackdrop, tuning.themePreset, Step1PrankRoomBuilderBridge.CurrentRoom); // S247：室内草地 v 画成稻草垫
        foreach (Transform ch in root)
        {
            string key = Step1ElementLabels.KeyOf(ch.name);
            if (Step1Art.IsTerrain(ch.name)) continue;
            string art = Step1Art.Icons.ContainsKey(key) || WorldArt.Elements.ContainsKey(key) || ArtKitRules.SlotIndex(key) >= 0 || ArtKit.SpriteOf(key) != null ? key : null; // S245：+11 个工坊元素 + 素材槽 + 素材包里的任何名字
            if (art == null) continue;
            if (ArtKit.SpriteOf(art) == null) art = Step1Art.PropArtFor(art, outdoor); // 素材包里自己配了图 = 尊重它
            var vis = ch.Find("Visual"); if (vis == null) continue;
            var sr = vis.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null || !Step1PropIcons.IsWhiteBox((int)sr.sprite.rect.width, (int)sr.sprite.rect.height)) continue;
            var sp = Get(art, false); if (sp == null) continue;
            // 白盒 = 1 格 × visualScale；像素图 16px = 1 格 → localScale 不变，图就按原来那块的大小显示（扁的机关会被压扁成同样的扁条）
            var ls = vis.localScale;
            if (WorldArt.WideElements.Contains(art) && Mathf.Abs(ls.x) > Mathf.Abs(ls.y) * 1.6f) { SkinWide(vis, sr, art, ls); continue; } // S245：长条机关（弹跳台 / 移动平台 / 传送带）平铺，不压成一团
            var place = WorldArt.PlaceOf(art, ls.x, ls.y); float s = place[0]; // S244：出口 = 1.5 格高的门、宝物 0.9 格（以前出口是一根绿色长条、宝物是小黄方块）
            sr.sprite = sp; sr.color = Color.white;
            var cannon = ch.GetComponent<PranksterCannon>(); if (cannon != null) sr.flipX = !cannon.FacingRight; // 图里炮口朝右
            vis.localScale = new Vector3(s, s, 1f);
            if (art == "GoalZone" || art == "Collectible") vis.localPosition = new Vector3(vis.localPosition.x, place[1], vis.localPosition.z); // 出口 / 宝物：相对物体中心摆（原色块是 1×3 的长条 / 0.5 的小方块）
            else vis.localPosition = vis.localPosition + new Vector3(0f, place[1], 0f);
            // 头上的小图标（Step1PropIcons）只贴白盒 → 这里换了图，它自己就不贴了，不会一个东西两张图
            var frames = ArtKit.FramesOf(art, out float fps); if (frames != null) animated.Add(new Anim { sr = sr, frames = frames, fps = fps }); // S245：素材包里给了动画帧
        }
    }

    // S245：素材包动画（每个物体按自己的帧循环）
    private struct Anim { public SpriteRenderer sr; public Sprite[] frames; public float fps; }
    private readonly List<Anim> animated = new List<Anim>();

    /// <summary>S245：长条机关：取图的底部非空那一条，横向平铺成原来的宽度（高度 = 原色块高度，底边不变）。碰撞体不动（H3）。</summary>
    private void SkinWide(Transform vis, SpriteRenderer sr, string art, Vector3 ls)
    {
        var band = WideSprite(art); if (band == null) return;
        float w = Mathf.Abs(ls.x), h = Mathf.Max(Mathf.Abs(ls.y), band.bounds.size.y);
        sr.sprite = band; sr.color = Color.white; sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(w, h);
        vis.localPosition = vis.localPosition + new Vector3(0f, (h - Mathf.Abs(ls.y)) * 0.5f, 0f);
        vis.localScale = Vector3.one;
    }

    private static Sprite WideSprite(string key)
    {
        string ck = key + "#wide"; if (cache.TryGetValue(ck, out var sp) && sp != null) return sp;
        var full = Get(key, false); if (full == null || full.texture == null) return null;
        var tex = full.texture; int y0, y1;
        if (!tex.isReadable) { cache[ck] = full; return full; }
        var px = tex.GetPixels(); var alpha = new float[px.Length]; for (int i = 0; i < px.Length; i++) alpha[i] = px[i].a;
        WorldArt.OpaqueRows(alpha, tex.width, tex.height, out y0, out y1);
        if (y1 < y0) { cache[ck] = full; return full; }
        sp = Sprite.Create(tex, new Rect(0, y0, tex.width, y1 - y0 + 1), new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
        cache[ck] = sp; return sp;
    }

    private void BuildBackground()
    {
        var rows = Step1PrankRoomBuilderBridge.CurrentRoom;
        if (rows == null || rows.Length == 0) return;
        int h = rows.Length, w = 0; foreach (var r in rows) w = Mathf.Max(w, r.Length);
        var tex = MakeTex(Step1Art.Background[0].Length, Step1Art.Background.Length, Step1Art.BackgroundRgba());
        var go = new GameObject("S243_Background");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(w * 0.5f - 0.5f, h * 0.5f - 0.5f, 5f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);
        sr.sortingOrder = -100;
        float k = Mathf.Max((w + 2f) / tex.width, (h + 2f) / tex.height); // 盖满整个房间（多 1 格边）
        go.transform.localScale = new Vector3(k, k, 1f);
        if (WorldArt.Outdoor(tuning.artBackdrop, tuning.themePreset, rows)) // S245：户外房间 = 天空 + 远山 + 近丘（远处的山脉）
        {
            sr.color = new Color(0.62f, 0.8f, 0.95f);
            Strip("S245_FarMountains", WorldArt.FarPalette, WorldArt.FarStrip, w, h * 0.35f, -95, 4.9f);
            Strip("S245_NearHills", WorldArt.NearPalette, WorldArt.NearStrip, w, h * 0.2f, -90, 4.8f);
        }
        var cam = Camera.main; if (cam != null) cam.backgroundColor = new Color(0.16f, 0.16f, 0.22f);
    }

    /// <summary>S245：一条横向循环的远景（远山 / 近丘），贴在房间底部偏上；Tiled 铺满宽度。</summary>
    private void Strip(string name, float[][] pal, string[] rows, int roomW, float baseY, int order, float z)
    {
        var tex = MakeTex(rows[0].Length, rows.Length, WorldArt.StripRgba(pal, rows)); tex.wrapMode = TextureWrapMode.Repeat;
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), WorldArt.Size, 0, SpriteMeshType.FullRect);
        sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(roomW + 4f, rows.Length / (float)WorldArt.Size); sr.sortingOrder = order;
        go.transform.position = new Vector3(roomW * 0.5f - 0.5f, baseY, z);
    }

    private void LateUpdate()
    {
        if (tuning == null || !tuning.artCharacters) return;
        if (marioSr != null && mario != null)
        {
            var pose = Step1Art.PoseOf(mario.IsGrounded, mario.Speed, mario.IsStunned, Time.time);
            var sp = Get(Step1Art.FrameKey(true, pose), false); if (sp != null && marioSr.sprite != sp) marioSr.sprite = sp;
            Juice(mario.transform, mario.IsGrounded, mario.Speed, ref marioDustAt, ref marioWasGrounded, ref marioFall, null); // 马里奥落地压扁 MarioController 自己做了，这里只补扬尘
            StunStars(mario.IsStunned);
        }
        if (youSr != null && you != null)
        {
            bool dis = you.IsDisguised;
            if (dis != youWasDisguised) { poofUntil = Time.time + 0.25f; youWasDisguised = dis; if (tuning.artJuice) Step1Fx.Burst(you.transform.position, 8, new Color(0.92f, 0.92f, 1f, 0.9f), 3.5f, Vector2.up, 360f, 2f, 0.2f, 0.35f); } // 变身 / 现形的一瞬间冒一下烟
            if (dis) return; // 伪装中 = DisguiseSystem 在画那个东西
            float ys = youRb != null ? Mathf.Abs(youRb.velocity.x) : Mathf.Abs(you.MoveInput.x) * 4f; // S244：用真实速度选帧（以前用按键 ×2，跑步换帧和移动对不上）
            var pose = Step1Art.ImpPose(Time.time < poofUntil, you.IsGrounded, ys, Time.time); // 小恶魔第 5 帧 = 变身烟，被晕时不用它（免得看成在变身）
            var sp = Get(Step1Art.FrameKey(false, pose), false);
            if (sp != null && youSr.sprite != sp) { youSr.sprite = sp; youSr.color = new Color(1f, 1f, 1f, youSr.color.a); }
            Juice(you.transform, you.IsGrounded, ys, ref youDustAt, ref youWasGrounded, ref youFall, youSr.transform);
        }
    }

    /// <summary>S244：跑步每 0.22 秒脚下一小团灰；从空中落地按下落速度扬尘；squashOn 不为空 = 顺便做落地压扁回弹（0.12 秒，只改外观节点的缩放，碰撞体不动）。</summary>
    private void Juice(Transform who, bool grounded, float speedX, ref float dustAt, ref bool wasGrounded, ref float fall, Transform squashOn)
    {
        if (!tuning.artJuice || who == null) { wasGrounded = grounded; return; }
        var rb = who.GetComponent<Rigidbody2D>();
        if (!grounded && rb != null) fall = Mathf.Min(fall, rb.velocity.y);
        float land = Step1Art.LandDust(wasGrounded, grounded, fall);
        Vector2 feet = (Vector2)who.position + Vector2.down * 0.5f;
        if (land > 0f) { Step1Fx.Dust(feet, land); if (squashOn != null && squashT < 0f) { squashT = 0f; squashBase = squashOn.localScale; squashFor = squashOn; } }
        if (grounded) fall = 0f;
        float every = Step1Art.DustEvery(grounded, speedX);
        if (every > 0f && Time.time >= dustAt) { dustAt = Time.time + every; Step1Fx.Burst(feet, 2, new Color(0.86f, 0.8f, 0.68f, 0.7f), 1.6f, Vector2.up, 70f, 4f, 0.1f, 0.25f); }
        wasGrounded = grounded;
        if (squashT >= 0f && squashFor != null)
        {
            squashT += Time.deltaTime; float u = Mathf.Clamp01(squashT / 0.12f);
            float k = Step1Feel.SmoothStep01(u); // 压扁 → 回弹
            squashFor.localScale = new Vector3(squashBase.x * Mathf.Lerp(1.2f, 1f, k), squashBase.y * Mathf.Lerp(0.8f, 1f, k), squashBase.z);
            if (u >= 1f) { squashFor.localScale = squashBase; squashT = -1f; }
        }
    }

    /// <summary>S244：马里奥被晕时头上转三颗星（以前只换一帧"晕"图，远看分不清）。</summary>
    private void StunStars(bool stunned)
    {
        if (!tuning.artJuice) return;
        if (stars == null && mario != null)
        {
            var go = new GameObject("S244_StunStars"); go.transform.SetParent(mario.transform, false); go.transform.localPosition = new Vector3(0f, 0.75f, 0f); stars = go.transform;
            var sp = Get("BadgeStar", false);
            for (int i = 0; i < 3; i++)
            {
                var st = new GameObject("star" + i); st.transform.SetParent(stars, false); st.transform.localScale = Vector3.one * 0.35f;
                var r = st.AddComponent<SpriteRenderer>(); r.sprite = sp; r.sortingOrder = 220;
            }
        }
        if (stars == null) return;
        if (stars.gameObject.activeSelf != stunned) stars.gameObject.SetActive(stunned);
        if (!stunned) return;
        starsSpin += Time.deltaTime * 4f;
        for (int i = 0; i < stars.childCount; i++)
        {
            float a = starsSpin + i * 2.094f; // 三颗星 120° 一颗
            stars.GetChild(i).localPosition = new Vector3(Mathf.Cos(a) * 0.4f, Mathf.Sin(a) * 0.12f, 0f);
        }
    }
}
