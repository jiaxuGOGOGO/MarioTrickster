
    // ═════════ 查表：哪个格子 / 元素 / 天气用哪张图（纯逻辑，sim 验证）═════════

    /// <summary>满格地面图（能无缝平铺，100% 填满）——OverworldArt.Audit 的"剪影太满"对它们不适用。</summary>
    public static readonly HashSet<string> FullTiles = new HashSet<string> { "TGrass", "TPath", "TRoof", "TWall", "TWater", "TMud" };

    /// <summary>小镇地面层：每格先铺什么（路 / 水 / 泥 / 草）。门、家、出生点底下铺路，其余铺草。</summary>
    public static string GroundKeyOf(char c)
    {
        if (c == '=' || c == 'M' || c == 'T' || OverworldCatalog.IsDoor(c)) return "TPath";
        if (c == 'w') return "TWater";
        if (c == 'g') return "TMud";
        return "TGrass";
    }

    /// <summary>小镇物件层：这一格上面立着什么图（null = 只有地面 / 用原来的图标）。房子最下面一排 = 墙面（带窗和门），上面 = 屋顶。</summary>
    public static string TownKeyOf(char c, bool wallFace)
    {
        switch (c)
        {
            case 'W': return wallFace ? "TWall" : "TRoof";
            case 't': return "TTree";
            case 'f': return "TFence";
            case '"': return "TTallGrass";
            case 'A': return "TMountain";
            case '^': return "THill";
            case 'h': return "TCave";
            case 'M': return "THome";
            case 'T': return "TSpawn";
        }
        return OverworldCatalog.IsDoor(c) ? "TDoor" : null;
    }

    /// <summary>天气粒子：用哪张图、一屏多少个、往哪飘（格/秒）、转不转。晴天 / 赶集 = 没有。</summary>
    public struct Weather { public string key; public int perScreen; public float vx, vy; public bool spin; public float alpha; }
    public static Weather WeatherOf(OverworldEvents.Kind k)
    {
        switch (k)
        {
            case OverworldEvents.Kind.Rain: return new Weather { key = "FxRain", perScreen = 70, vx = -2f, vy = -14f, alpha = 0.7f };
            case OverworldEvents.Kind.Storm: return new Weather { key = "FxRain", perScreen = 110, vx = -5f, vy = -18f, alpha = 0.8f };
            case OverworldEvents.Kind.Acid: return new Weather { key = "FxAcid", perScreen = 60, vx = -1f, vy = -10f, alpha = 0.8f };
            case OverworldEvents.Kind.Wind: return new Weather { key = "FxLeaf", perScreen = 26, vx = 7f, vy = -1.2f, spin = true, alpha = 0.95f };
            case OverworldEvents.Kind.Fog: return new Weather { key = "FxFog", perScreen = 18, vx = 0.6f, vy = 0f, alpha = 0.45f };
        }
        return new Weather { key = null };
    }

    /// <summary>技能 / 道具特效图（炸弹、炮弹、遁地土包、钩爪、挑衅气泡、雷云、水花）。</summary>
    public static readonly string[] SkillFx = { "FxBomb", "FxCannonball", "FxMound", "FxHook", "FxTaunt", "FxStormCloud", "FxSplash", "FxDrop" };

    /// <summary>视差：远景跟镜头走 factor（0 = 钉在天上，1 = 跟地面一起动）。返回远景该往右挪多少（格），保证横向循环不露缝。</summary>
    public static float ParallaxX(float camX, float factor, float stripWidth)
    {
        if (stripWidth <= 0f) return camX;
        float shift = camX * (1f - factor);
        float wrap = shift - stripWidth * (float)System.Math.Floor(shift / stripWidth);
        return wrap;
    }

    /// <summary>长图 → RGBA（左下角开始）。调色板下标字符 '0'..'9' 'a'..，'.' = 透明。</summary>
    public static float[] StripRgba(float[][] pal, string[] rows)
    {
        int h = rows.Length, w = rows[0].Length; var px = new float[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            char c = rows[h - 1 - y][x]; if (c == '.') continue;
            int idx = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'z' ? 10 + c - 'a' : -1;
            if (idx < 0 || idx >= pal.Length) continue;
            int i = (y * w + x) * 4; px[i] = pal[idx][0]; px[i + 1] = pal[idx][1]; px[i + 2] = pal[idx][2]; px[i + 3] = 1f;
        }
        return px;
    }

    /// <summary>任何一张图（房间图标 / 帧 / 图块 / 小镇 / 装饰 / 特效 / 工坊元素）→ 字符画。找不到 = null。</summary>
    public static string[] Rows(string key)
    {
        if (key == null) return null;
        if (Town.TryGetValue(key, out var r)) return r;
        if (Decor.TryGetValue(key, out r)) return r;
        if (Fx.TryGetValue(key, out r)) return r;
        if (Elements.TryGetValue(key, out r)) return r;
        return null;
    }

    /// <summary>所有新图的名字（自检 / 导出模板 / 换图台用）。</summary>
    public static IEnumerable<string> AllKeys()
    {
        foreach (var k in Town.Keys) yield return k;
        foreach (var k in Decor.Keys) yield return k;
        foreach (var k in Fx.Keys) yield return k;
        foreach (var k in Elements.Keys) yield return k;
    }

    // ═════════ 房间装饰（背景层：压暗、没有碰撞体、不挡机关——H6）═════════
    public static readonly string[] WallDecor = { "DecoPainting", "DecoWindow", "DecoBanner", "DecoClock", "DecoShelf", "DecoWeb", "DecoChandelier" };
    public static readonly string[] FloorDecor = { "DecoCandle", "DecoPlant", "DecoArmor", "DecoBarrels", "DecoRug" };
    public static readonly string[] OutdoorWallDecor = { "DecoWeb", "DecoBanner" };
    public static readonly string[] OutdoorFloorDecor = { "DecoPlant", "DecoBarrels" };

    public struct Dress { public int x, y; public string key; }

    static bool SolidCh(char c) => c == '#' || c == '=' || c == 'W' || c == 'v' || c == 'X' || c == 'F';
    static bool AirCh(char c) => c == '.' || c == ' ';
    static uint H(int x, int y, int seed) { unchecked { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h; } }

    /// <summary>
    /// 给房间挑装饰位置（纯函数：同一个房间 + 同一个种子 = 永远同样的摆法）。rows = 房间字符画（第 0 行 = 最上面），返回的 y 是世界坐标（0 = 最下面一行）。
    /// 规则：① 只放在空气格；② 3×3 范围内除了墙 / 地面以外什么都没有（不贴着机关、出生点、宝物、出口）；③ 地上的（烛台 / 盆栽 / 盔甲 / 木桶 / 地毯）脚下必须是地面，
    /// 墙上的（画 / 窗 / 挂旗 / 座钟 / 书架 / 蛛网 / 吊灯）下面至少空 2 格（不挡你看路）；④ 两件装饰至少隔 3 格；⑤ density = 每多少格空气放 1 件的倒数（0 = 不放）。
    /// outdoor = 户外主题：只放蛛网 / 挂旗 / 盆栽 / 木桶（不会在公园里挂吊灯）。
    /// </summary>
    public static List<Dress> Dressing(string[] rows, int seed, float density, bool outdoor)
    {
        var list = new List<Dress>();
        if (rows == null || rows.Length < 3 || density <= 0f) return list;
        int h = rows.Length, w = 0; foreach (var r in rows) w = System.Math.Max(w, r.Length);
        char At(int x, int yTop) => yTop < 0 || yTop >= h || x < 0 || x >= rows[yTop].Length ? 'W' : rows[yTop][x];
        var wall = outdoor ? OutdoorWallDecor : WallDecor; var floor = outdoor ? OutdoorFloorDecor : FloorDecor;
        int air = 0; for (int yt = 0; yt < h; yt++) for (int x = 0; x < w; x++) if (AirCh(At(x, yt))) air++;
        int want = (int)(air * System.Math.Min(1f, density));
        for (int yt = 1; yt < h - 1 && list.Count < want; yt++)
            for (int x = 1; x < w - 1 && list.Count < want; x++)
            {
                if (!AirCh(At(x, yt))) continue;
                bool clean = true;
                for (int dy = -1; dy <= 1 && clean; dy++) for (int dx = -1; dx <= 1; dx++) { char c = At(x + dx, yt + dy); if (!AirCh(c) && !SolidCh(c)) { clean = false; break; } }
                if (!clean) continue;
                bool onFloor = SolidCh(At(x, yt + 1));
                bool high = AirCh(At(x, yt + 1)) && AirCh(At(x, yt + 2));
                if (!onFloor && !high) continue;
                uint hv = H(x, yt, seed);
                if ((hv % 1000) / 1000f > density * 3f) continue; // 撒得开一点（不是从左上角挤满）
                bool near = false; foreach (var d in list) if (System.Math.Abs(d.x - x) < 3 && System.Math.Abs(d.y - (h - 1 - yt)) < 3) { near = true; break; }
                if (near) continue;
                var pool = onFloor ? floor : wall;
                string key = pool[(int)((hv >> 10) % (uint)pool.Length)];
                if (key == "DecoChandelier" && !SolidCh(At(x, yt - 1))) key = "DecoPainting"; // 吊灯要挂在天花板下
                if (key == "DecoWeb" && !(SolidCh(At(x, yt - 1)) && (SolidCh(At(x - 1, yt)) || SolidCh(At(x + 1, yt))))) key = outdoor ? "DecoBanner" : "DecoWindow"; // 蛛网在墙角
                list.Add(new Dress { x = x, y = h - 1 - yt, key = key });
            }
        return list;
    }


    // ═════════ 房间里怎么摆（纯逻辑）═════════
    /// <summary>长条机关：弹跳台 B（2×0.3）、移动平台 &gt;（3×0.4）、传送带 &lt;（1×0.3）——取图的底部一条横向平铺，不压成一团。</summary>
    public static readonly HashSet<string> WideElements = new HashSet<string> { "BouncyPlatform", "MovingPlatform", "ConveyorBelt" };

    /// <summary>换图后多大、往上挪多少（格）。工坊元素：摆锤 / 锯片 / 飞行怪 / 弹跳怪 原色块很小（0.3–0.8 格）→ 至少画 0.8 格，认得出；其余照 Step1Art.PlaceOf。</summary>
    public static float[] PlaceOf(string key, float sx, float sy)
    {
        if (key != null && Elements.ContainsKey(key) && !WideElements.Contains(key))
        {
            float s = Step1Art.FitScale(sx, sy, 0.8f); return new[] { s, Step1Art.FitLift(sy, s) };
        }
        return Step1Art.PlaceOf(key, sx, sy);
    }

    /// <summary>alpha（左下角开始，w×h）里有不透明像素的最低行 y0 和最高行 y1（全空 = y1 &lt; y0）。长条机关取这一段平铺。</summary>
    public static void OpaqueRows(float[] alpha, int w, int h, out int y0, out int y1)
    {
        y0 = h; y1 = -1;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (alpha[y * w + x] > 0.5f) { if (y < y0) y0 = y; if (y > y1) y1 = y; break; }
        // 只要底部那一段：从最低的不透明行往上，最多 8 行（16px 图 = 半格高的条）
        if (y1 >= y0 && y1 - y0 > 7) y1 = y0 + 7;
    }

    /// <summary>房间算不算户外：mode 1 = 老宅，2 = 户外，0 = 自动（公园类主题 + 最上面一排至少 30% 是空的 = 看得见天）。</summary>
    public static bool Outdoor(int mode, string theme, string[] rows)
    {
        if (mode == 1) return false; if (mode == 2) return true;
        bool park = theme == "AmusementPark" || theme == "CityPark" || theme == "MountainPark";
        if (!park || rows == null || rows.Length == 0) return false;
        string top = rows[0]; if (top.Length == 0) return false;
        int open = 0; foreach (char c in top) if (c == '.' || c == ' ') open++;
        return open >= top.Length * 0.3f;
    }

    // ═════════ 覆盖自检：每个东西都有图吗（sim + Unity 菜单都跑）═════════
    /// <summary>不需要图的房间元素：空气 / 空格（什么都不画）、出生点（只是位置）。</summary>
    public static readonly HashSet<string> NoArtElements = new HashSet<string> { "Air", "Space", "MarioSpawn", "TricksterSpawn" };
    static readonly HashSet<string> TerrainElements = new HashSet<string> { "Ground", "Platform", "Wall", "OneWayPlatform" };

    /// <summary>房间元素名（Registry elementName）→ 用哪张图。null = 没图（白盒）。</summary>
    public static string ElementArt(string elementName)
    {
        if (elementName == null) return null;
        if (TerrainElements.Contains(elementName)) return elementName == "OneWayPlatform" ? "Platform" : elementName == "Wall" ? "Wall" : "GroundTop";
        if (Step1Art.Icons.ContainsKey(elementName)) return elementName;
        if (Elements.ContainsKey(elementName)) return elementName;
        return null;
    }

    /// <summary>覆盖报告：缺什么（空 = 全覆盖）。elements = 房间里所有元素名；小镇格子从 OverworldCatalog 读；天气从 OverworldEvents.Kind 读。</summary>
    public static List<string> Coverage(IEnumerable<string> elements)
    {
        var miss = new List<string>();
        foreach (var e in elements)
        {
            if (NoArtElements.Contains(e)) continue;
            if (ArtKitRules.SlotIndex(e) >= 0) continue; // 素材槽：长相来自素材包（没配 = 默认图），不算缺
            string k = ElementArt(e); if (k == null) { miss.Add("房间元素 " + e); continue; }
            if (Step1Art.Icons.ContainsKey(k) || Step1Art.Tiles.ContainsKey(k) || Elements.ContainsKey(k)) continue;
            miss.Add("房间元素 " + e + " → " + k + "（图不存在）");
        }
        foreach (var t in OverworldCatalog.All)
        {
            string k = TownKeyOf(t.c, false) ?? OverworldArt.IconOf(t.c) ?? (t.c == '.' || t.c == '=' || t.c == 'w' || t.c == 'g' ? GroundKeyOf(t.c) : null);
            if (k == null) { miss.Add("小镇格子 " + t.c + " " + t.zh); continue; }
            if (!Town.ContainsKey(k) && !OverworldArt.Icons.ContainsKey(k)) miss.Add("小镇格子 " + t.c + " → " + k + "（图不存在）");
        }
        foreach (OverworldEvents.Kind wk in System.Enum.GetValues(typeof(OverworldEvents.Kind)))
        {
            if (wk == OverworldEvents.Kind.Clear || wk == OverworldEvents.Kind.Market) continue;
            var wf = WeatherOf(wk); if (wf.key == null || !Fx.ContainsKey(wf.key)) miss.Add("天气 " + wk);
        }
        foreach (var s in SkillFx) if (!Fx.ContainsKey(s)) miss.Add("特效 " + s);
        foreach (var d in WallDecor) if (!Decor.ContainsKey(d)) miss.Add("装饰 " + d);
        foreach (var d in FloorDecor) if (!Decor.ContainsKey(d)) miss.Add("装饰 " + d);
        if (FarStrip.Length == 0 || NearStrip.Length == 0) miss.Add("远山 / 近丘");
        return miss;
    }

    /// <summary>S245：小镇地面每格几个像素（16 = 和图块一样清楚）。贴图边长不超过 4096：超大地图自动降到 8 / 4 / 2。</summary>
    public static int GroundPixelsPerCell(int w, int h)
    {
        int n = Size; while (n > 1 && (w * n > 4096 || h * n > 4096)) n /= 2;
        return n;
    }
}
