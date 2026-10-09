using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S241：给房间里还是白盒的机关 / 场景物贴上一眼认得出的小图标（Step1Icons：火苗、弹簧、香蕉、铁笼…）。
/// 只改画面：作为 Visual 的兄弟子物体 "Icon" 叠在色块上，不碰碰撞体、不改原色块（H3 判定框不变）。
/// 已经换了主题美术的（Sprite 不是 4×4 白方块）不贴——美术图优先。Resources/Step1Icons/<Key>.png 同名覆盖。
/// 调参 propIcons 关掉 = 全部隐藏。由 Step1Combo 运行时自动挂上（旧场景不用重建）。
/// </summary>
public class Step1PropIcons : MonoBehaviour
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private readonly List<(SpriteRenderer icon, SpriteRenderer body, ControllablePropBase prop)> made = new List<(SpriteRenderer, SpriteRenderer, ControllablePropBase)>();
    private MarioMindTuningSO tuning;

    public static Sprite IconSprite(string key)
    {
        if (key == null) return null;
        if (cache.TryGetValue(key, out var sp) && sp != null) return sp;
        var tex = Resources.Load<Texture2D>("Step1Icons/" + key);
        if (tex == null)
        {
            var px = Step1Icons.Pixels(key); if (px == null) return null;
            tex = new Texture2D(Step1Icons.Size, Step1Icons.Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cols = new Color[Step1Icons.Size * Step1Icons.Size];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
            tex.SetPixels(cols); tex.Apply();
        }
        else tex.filterMode = FilterMode.Point;
        sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        cache[key] = sp; return sp;
    }

    /// <summary>纯逻辑：这个色块还是白盒吗（4×4 白方块 = 生成器的占位图）。</summary>
    public static bool IsWhiteBox(int spriteWidth, int spriteHeight) => spriteWidth <= 4 && spriteHeight <= 4;

    private void Start()
    {
        tuning = MarioMindTuningSO.LoadOrDefault();
        if (tuning == null || !tuning.propIcons) return;
        var root = GameObject.Find("Step1_PrankRoom");
        if (root == null) return;
        foreach (Transform child in root.transform)
        {
            string key = Step1ElementLabels.KeyOf(child.name);
            if (!Step1Icons.Has(key)) continue;
            var visual = child.Find("Visual");
            var vsr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (vsr == null || (vsr.sprite != null && !IsWhiteBox((int)vsr.sprite.rect.width, (int)vsr.sprite.rect.height))) continue;
            var sp = IconSprite(key); if (sp == null) continue;
            var go = new GameObject("Icon");
            go.transform.SetParent(child, false);
            go.transform.localPosition = visual.localPosition + new Vector3(0f, 0f, -0.01f);
            float size = Mathf.Clamp(Mathf.Min(visual.localScale.x, visual.localScale.y) * 0.9f, 0.55f, 0.95f);
            if (key == "BananaPeel" || key == "PoisonPool" || key == "Glue" || key == "Tripwire") size = 0.8f; // 扁的机关：图标照样正方形，看得清
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp; sr.sortingOrder = vsr.sortingOrder + 1;
            made.Add((sr, vsr, child.GetComponent<ControllablePropBase>()));
        }
    }

    /// <summary>纯逻辑：图标颜色——预警时跟着闪（H3：预兆不能被图标盖住），用光了变灰，平时原色。</summary>
    public static Color IconTint(bool telegraph, bool spent, Color body) =>
        telegraph ? new Color(1f, Mathf.Lerp(1f, body.g, 0.8f), Mathf.Lerp(1f, body.b, 0.8f), 1f) : spent ? new Color(0.45f, 0.45f, 0.45f, 0.85f) : Color.white;

    private void Update()
    {
        foreach (var (icon, body, prop) in made)
        {
            if (icon == null || body == null) continue;
            bool tel = prop != null && prop.GetControlState() == PropControlState.Telegraph;
            icon.color = IconTint(tel, prop != null && prop.SpentThisRound, body.color);
        }
    }
}
