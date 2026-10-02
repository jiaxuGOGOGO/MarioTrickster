// 自动生成：tools_s220/gen_art.py（S220）。改图标：直接改下面的 16×16 字符画，或者用导出的 PNG 模板画好放进 Assets/Resources/OverworldArt/<Key>.png 覆盖。
using System.Collections.Generic;

/// <summary>
/// S220：小镇格子 / 特效的 16×16 像素图标（纯数据，Unity 游戏、大地图工坊、网页设计台共用一份）。
/// 规则：图标只是"看得懂"的占位美术——轮廓一眼认出是什么东西（巨炮像炮、滚石像石头、补心像心），颜色沿用格子表；伤害角标形状 + 颜色双编码（红碎心 = 掉心、黄星 = 晕、蓝水滴 = 变慢），色弱也分得清。
/// 换美术：Unity 菜单 MarioTrickster/Overworld/导出像素图标模板 → 改 PNG → 放进 Assets/Resources/OverworldArt/（同名覆盖，自动设成像素风导入）。
/// 参考：Unity "placeholder asset problem"（占位美术要可替换、同名同尺寸）https://unity.com/blog/placeholder-asset-problem ；色弱友好的形状 + 颜色双编码 https://colorblindgames.com/2023/04/19/universal-colorblind-code/
/// </summary>
public static class OverworldArt
{
    public const int Size = 16;

    /// <summary>调色板：字符 → 颜色（. = 透明）。</summary>
    public static readonly Dictionary<char, float[]> Palette = new Dictionary<char, float[]>
    {
        { 'k', new[] { 0.1f, 0.09f, 0.12f } },
        { 'w', new[] { 1f, 1f, 1f } },
        { 'r', new[] { 0.9f, 0.2f, 0.22f } },
        { 'R', new[] { 0.6f, 0.1f, 0.12f } },
        { 'y', new[] { 1.0f, 0.86f, 0.25f } },
        { 'Y', new[] { 0.85f, 0.6f, 0.12f } },
        { 'b', new[] { 0.3f, 0.6f, 0.95f } },
        { 'B', new[] { 0.16f, 0.34f, 0.7f } },
        { 'g', new[] { 0.4f, 0.75f, 0.35f } },
        { 'G', new[] { 0.2f, 0.45f, 0.2f } },
        { 'n', new[] { 0.62f, 0.42f, 0.22f } },
        { 'N', new[] { 0.4f, 0.26f, 0.14f } },
        { 's', new[] { 0.7f, 0.69f, 0.66f } },
        { 'S', new[] { 0.42f, 0.41f, 0.4f } },
        { 'p', new[] { 0.72f, 0.45f, 1.0f } },
        { 'P', new[] { 0.45f, 0.22f, 0.7f } },
        { 'c', new[] { 0.55f, 0.9f, 1.0f } },
        { 'o', new[] { 0.95f, 0.6f, 0.18f } },
        { 'x', new[] { 0.05f, 0.04f, 0.04f } },
    };

    /// <summary>格子字符 → 图标名（没有的格子只用颜色画）。</summary>
    public static readonly Dictionary<char, string> TileIcon = new Dictionary<char, string>
    {
        { 'K', "GiantCannon" },
        { 'X', "CannonTarget" },
        { 'O', "Boulder" },
        { 'U', "WaterTower" },
        { 'B', "BellTower" }, // S228
        { 'i', "Lamp" },
        { 'n', "BananaPeel" },
        { '?', "PickupBox" },
        { 'c', "Crate" },
        { 'h', "Cave" },
        { 'M', "MarioHome" },
        { 'T', "TricksterSpawn" },
        { '+', "Heart" },
        { '*', "Energy" },
    };

    /// <summary>图标：从上到下 16 行，每行 16 个字符。</summary>
    public static readonly Dictionary<string, string[]> Icons = new Dictionary<string, string[]>
    {
        { "Heart", new[] {
            "................",
            "................",
            "..kkk.....kkk...",
            ".krrrk...krrrk..",
            "krrwrrk.krrrrrk.",
            "krwrrrrkrrrrrrk.",
            "krrrrrrrrrrrrrk.",
            "krrrrrrrrrrrrrk.",
            ".krrrrrrrrrrrk..",
            "..krrrrrrrrrk...",
            "...krrrrrrrk....",
            "....krrrrrk.....",
            ".....krrrk......",
            "......krk.......",
            ".......k........",
            "................",
        } },
        { "HurtBadge", new[] {
            "................",
            "................",
            "..kkk.....kkk...",
            ".krrrk.k.krrrk..",
            "krrwrrkkkrrrrrk.",
            "krwrrrrkkrrrrrk.",
            "krrrrrrrkrrrrrk.",
            "krrrrrrkrrrrrrk.",
            ".krrrrkrrrrrrk..",
            "..krrrrkrrrrk...",
            "...krrrrkrrk....",
            "....krrkrrk.....",
            ".....krrrk......",
            "......krk.......",
            ".......k........",
            "................",
        } },
        { "Energy", new[] {
            "................",
            "................",
            ".......k........",
            "......kpk.......",
            ".....kpwpk......",
            "....kppwppk.....",
            "...kpppwpppk....",
            "..kppppppppppk..",
            ".kpppppppppppppk",
            "..kPPPPPPPPPPk..",
            "...kPPPPPPPPk...",
            "....kPPPPPPk....",
            ".....kPPPPk.....",
            "......kPPk......",
            ".......kk.......",
            "................",
        } },
        { "Bolt", new[] {
            "................",
            "................",
            ".........kkkk...",
            "........kyyk....",
            ".......kyyk.....",
            "......kyyk......",
            ".....kyykkkk....",
            "....kyyyyyyk....",
            "....kkkkyyk.....",
            ".......kyyk.....",
            "......kyyk......",
            ".....kyyk.......",
            ".....kyk........",
            "....kyk.........",
            "....kk..........",
            "................",
        } },
        { "Cloud", new[] {
            "................",
            "................",
            "................",
            ".....kkkk.......",
            "....kssssk.kkk..",
            "..kkssswsskssk..",
            ".ksssssssssssk..",
            "kssssssssssssssk",
            "kSSSSSSSSSSSSSSk",
            ".kSSSSSSSSSSSSk.",
            "..kkkkkkkkkkkk..",
            "....kyk...kyk...",
            "...kyk...kyk....",
            "..kyk...kyk.....",
            "..kk....kk......",
            "................",
        } },
        { "GiantCannon", new[] {
            "................",
            "................",
            ".........kkkkkk.",
            ".......kkSSSSSSk",
            ".....kkSSSSSSSwk",
            "....kSSSSSSSSSSk",
            "...kSSSSSSSSkkk.",
            "..kSSSSSSSSk....",
            "..kSSSSSSSk.....",
            ".knnnkSSkknnk...",
            "knNNNnkknNNNnk..",
            "knNkNnk.knNkNnk.",
            "knNNNnk.knNNNnk.",
            ".knnnk...knnnk..",
            "..kkk.....kkk...",
            "................",
        } },
        { "CannonTarget", new[] {
            "................",
            ".....kkkkkk.....",
            "...kkrrrrrrkk...",
            "..krrwwwwwwrrk..",
            ".krrwrrrrrrwrrk.",
            ".krwrrkkkkrrwrk.",
            "krrwrkwwwwkrwrrk",
            "krwrrkwrrwkrrwrk",
            "krwrrkwrrwkrrwrk",
            "krrwrkwwwwkrwrrk",
            ".krwrrkkkkrrwrk.",
            ".krrwrrrrrrwrrk.",
            "..krrwwwwwwrrk..",
            "...kkrrrrrrkk...",
            ".....kkkkkk.....",
            "................",
        } },
        { "Boulder", new[] {
            "................",
            "................",
            ".....kkkkk......",
            "...kksssssk.....",
            "..ksssswsssk....",
            ".ksssswwssssk...",
            ".kssssssssSSsk..",
            "kssSssssssSsssk.",
            "ksSSssssssssssk.",
            "kssssssSSsssssk.",
            "kssssssSssssSsk.",
            ".kSsssssssssSSk.",
            ".kSSSsssssSSSk..",
            "..kkSSSSSSSkk...",
            "....kkkkkkk.....",
            "................",
        } },
        { "WaterTower", new[] {
            "................",
            "....kkkkkkkk....",
            "...kbbbbbbbbk...",
            "..kbbwbbbbbbbk..",
            "..kbwbbbbbbbbk..",
            "..kbbbbbbbbbbk..",
            "..kBBBBBBBBBBk..",
            "..kkkkkkkkkkkk..",
            "...kn......nk...",
            "...kn.k..k.nk...",
            "...knk....knk...",
            "...kn.k..k.nk...",
            "...knk....knk...",
            "...kn......nk...",
            "..kNk......kNk..",
            "..kk........kk..",
        } },
        { "BellTower", new[] {
            "......kkkk......",
            ".....kNNNNk.....",
            "....kNNNNNNk....",
            "...kkkkkkkkkk...",
            "...kn......nk...",
            "...kn.kkkk.nk...",
            "...kn.kyyk.nk...",
            "...kn.kyyk.nk...",
            "...kn.kYYk.nk...",
            "...kn..kk..nk...",
            "...knnnnnnnnk...",
            "...knNNNNNNnk...",
            "...knNkkkkNnk...",
            "...knNk..kNnk...",
            "...knNk..kNnk...",
            "...kkkk..kkkk...",
        } },
        { "Lamp", new[] {
            "................",
            ".....kkkkk......",
            "....kyyyyyk.....",
            "....kywwyyk.....",
            "....kyyyyyk.....",
            ".....kkkkk......",
            "......kSk.......",
            "......kSk.......",
            "......kSk.......",
            "......kSk.......",
            "......kSk.......",
            "......kSk.......",
            "......kSk.......",
            "....kkSSSkk.....",
            "...kSSSSSSSk....",
            "...kkkkkkkkk....",
        } },
        { "BananaPeel", new[] {
            "................",
            "................",
            "..........kk....",
            ".........kyk....",
            "........kyYk....",
            ".......kyyk.....",
            "..kk..kyyk......",
            ".kyyk.kyyk......",
            "kyyyykyyyk......",
            "kYyyyyyyyyk.....",
            ".kYyyyyyyyyk....",
            "..kYYyyyyyyyk...",
            "...kkYYYYyyyyk..",
            ".....kkkkYYYYk..",
            ".........kkkk...",
            "................",
        } },
        { "PickupBox", new[] {
            "................",
            ".kkkkkkkkkkkkkk.",
            ".kooooooooooook.",
            ".kooooooooooook.",
            ".kooooyyyyooook.",
            ".koooyykkyyoook.",
            ".koooooooyyoook.",
            ".kooooooyyooook.",
            ".koooooyyoooook.",
            ".koooooyyoooook.",
            ".kooooooooooook.",
            ".koooooyyoooook.",
            ".kooooooooooook.",
            ".kooooooooooook.",
            ".kkkkkkkkkkkkkk.",
            "................",
        } },
        { "Crate", new[] {
            "................",
            "................",
            ".kkkkkkkkkkkkkk.",
            ".kNnnnnnnnnnnNk.",
            ".knNnnnnnnnnNnk.",
            ".knnNnnnnnnNnnk.",
            ".knnnNnnnnNnnnk.",
            ".knnnnNnnNnnnnk.",
            ".knnnnnNNnnnnnk.",
            ".knnnnnNNnnnnnk.",
            ".knnnnNnnNnnnnk.",
            ".knnnNnnnnNnnnk.",
            ".knnNnnnnnnNnnk.",
            ".knNnnnnnnnnNnk.",
            ".kkkkkkkkkkkkkk.",
            "................",
        } },
        { "Cave", new[] {
            "................",
            "................",
            "................",
            ".....kkkkkk.....",
            "...kkSSSSSSkk...",
            "..kSSSSSSSSSSk..",
            ".kSSSkkkkkkSSSk.",
            ".kSSkxxxxxxkSSk.",
            "kSSkxxxxxxxxkSSk",
            "kSSkxxxxxxxxkSSk",
            "kSkxxxxxxxxxxkSk",
            "kSkxxxxxxxxxxkSk",
            "kSkxxxxxxxxxxkSk",
            "kSkxxxxxxxxxxkSk",
            "kkkkkkkkkkkkkkkk",
            "................",
        } },
        { "MarioHome", new[] {
            "................",
            "................",
            ".......kk.......",
            "......krrk......",
            ".....krrrrk.....",
            "....krrrrrrk....",
            "...krrrrrrrrk...",
            "..krrrrrrrrrrk..",
            ".kkkkkkkkkkkkkk.",
            "..kwwwwwwwwwwk..",
            "..kwbbwwwwnnwk..",
            "..kwbbwwwwnnwk..",
            "..kwwwwwwwnnwk..",
            "..kwwwwwwwnnwk..",
            "..kkkkkkkkkkkk..",
            "................",
        } },
        { "TricksterSpawn", new[] {
            "................",
            "...kk...........",
            "...kbk..........",
            "...kbbkk........",
            "...kbbbbkk......",
            "...kbbbbbbkk....",
            "...kbbbbbbbbk...",
            "...kbbbbbbkk....",
            "...kbbbbkk......",
            "...kbbkk........",
            "...kSk..........",
            "...kSk..........",
            "...kSk..........",
            "..kSSSk.........",
            ".kkkkkkk........",
            "................",
        } },
        { "StunBadge", new[] {
            "................",
            "................",
            ".......k........",
            "......kyk.......",
            "......kyk.......",
            ".....kyyyk......",
            "kkkkkyyyyykkkkk.",
            ".kyyyyywyyyyyk..",
            "..kyyyyyyyyyk...",
            "...kyyyyyyyk....",
            "...kyyyyyyyk....",
            "..kyyykkkyyyk...",
            "..kyyk...kyyk...",
            ".kyk.......kyk..",
            ".kk.........kk..",
            "................",
        } },
        { "SlowBadge", new[] {
            "................",
            "................",
            ".......k........",
            "......kbk.......",
            "......kbk.......",
            ".....kbbbk......",
            "....kbbbbbk.....",
            "...kbbwbbbbk....",
            "..kbbwbbbbbbk...",
            "..kbwbbbbbbbk...",
            "..kbbbbbbbbbk...",
            "..kbbbbbbbbbk...",
            "...kBbbbbbBk....",
            "....kBBBBBk.....",
            ".....kkkkk......",
            "................",
        } },
    };

    // ═════════ S231：换图自检（美术把 PNG 放进来之前/之后都能查） ═════════
    // 规则来自像素画可读性经验（https://the-pixel.art/articles/pixel-art-character-design/ ）：小图用深色描边、整体颜色少、先看剪影；
    // 尺寸沿用 S220（16 或 32）。只给提示，不拦截（美术可以故意打破）。
    public const int ArtMaxColors = 16;          // 一张图最多几种颜色
    public const float ArtMinDarkOutline = 0.75f; // 外轮廓上深色像素至少占多少
    public const float ArtMinFill = 0.15f, ArtMaxFill = 0.85f; // 剪影：太空 = 看不见，太满 = 方块
    public const float DarkLuma = 0.3f;

    public static float Luma(float r, float g, float b) => 0.299f * r + 0.587f * g + 0.114f * b;

    /// <summary>RGBA 像素（任意顺序，长度 = size²×4）→ 问题列表（空 = 通过）。半透明（0&lt;a&lt;1）也算问题：像素风要么有要么没有。</summary>
    public static List<string> Audit(float[] rgba, int size)
    {
        var issues = new List<string>();
        if (rgba == null || size <= 0 || rgba.Length != size * size * 4) { issues.Add("读不到像素"); return issues; }
        if (size != 16 && size != 32) issues.Add($"尺寸 {size}×{size}（要 16×16 或 32×32）");
        var colors = new HashSet<int>(); int filled = 0, edge = 0, darkEdge = 0, semi = 0;
        bool On(int x, int y) => x >= 0 && y >= 0 && x < size && y < size && rgba[(y * size + x) * 4 + 3] > 0.5f;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int i = (y * size + x) * 4; float a = rgba[i + 3];
            if (a > 0.01f && a < 0.99f) semi++;
            if (a <= 0.5f) continue;
            filled++;
            colors.Add(((int)(rgba[i] * 255) << 16) | ((int)(rgba[i + 1] * 255) << 8) | (int)(rgba[i + 2] * 255));
            if (!On(x - 1, y) || !On(x + 1, y) || !On(x, y - 1) || !On(x, y + 1))
            { edge++; if (Luma(rgba[i], rgba[i + 1], rgba[i + 2]) < DarkLuma) darkEdge++; }
        }
        float fill = filled / (float)(size * size);
        if (semi > 0) issues.Add($"{semi} 个半透明像素（像素风建议只用全透明或不透明）");
        if (filled == 0) { issues.Add("整张图是空的"); return issues; }
        if (colors.Count > ArtMaxColors) issues.Add($"用了 {colors.Count} 种颜色（建议 ≤{ArtMaxColors}，小图颜色多会糊）");
        if (fill < ArtMinFill) issues.Add($"剪影太小（只占 {fill:P0}，小于 {ArtMinFill:P0} 远看会找不到）");
        if (fill > ArtMaxFill) issues.Add($"剪影太满（占 {fill:P0}，像个方块，认不出形状）");
        if (edge > 0 && darkEdge / (float)edge < ArtMinDarkOutline) issues.Add($"外轮廓深色只占 {darkEdge / (float)edge:P0}（建议 ≥{ArtMinDarkOutline:P0}：深色描边让小图在草地/路面上都看得清）");
        return issues;
    }

    public static string IconOf(char tile) => TileIcon.TryGetValue(tile, out var k) ? k : null;

    /// <summary>RGBA 像素（从左下角开始，和 Unity Texture2D.SetPixels 顺序一致）。没有这个图标 = null。</summary>
    public static float[] Pixels(string key)
    {
        if (key == null || !Icons.TryGetValue(key, out var rows)) return null;
        var px = new float[Size * Size * 4];
        for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
        {
            char c = rows[Size - 1 - y][x]; int i = (y * Size + x) * 4;
            if (c == '.' || !Palette.TryGetValue(c, out var col)) continue;
            px[i] = col[0]; px[i + 1] = col[1]; px[i + 2] = col[2]; px[i + 3] = 1f;
        }
        return px;
    }
}
