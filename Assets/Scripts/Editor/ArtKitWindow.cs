using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S245：🎨 素材包 换图 + 互动（MarioTrickster → 美术 Art）。
/// 用户 S245：「以后我有其他画风参考的替换美术资产 … 在关卡编辑的时候直接替换成美术素材；毒池可以让进入范围的角色持续掉血，掉半格或一格自己设定」。
/// ① 左边：全部名字按组列出（房间元素 / 角色帧 / 地形 / 小镇 / 装饰 / 特效 / 徽章 / 素材槽），每个名字一张预览（内置图 or 你换上的图）；
/// ② 右边：选中一个名字 → 把 Sprite 拖进去（立刻生效，房间 / 小镇 / 图例都用它）+ 可选动画帧 + 互动（掉半格 / 一格、回血、减速、晕、谁会中）；
/// ③ 批量导入：选一个文件夹，PNG 按文件名对上名字（PoisonPool.png → PoisonPool），自动设成像素图（Point、不压缩）；
/// ④ 导出模板：把所有内置图导出成 PNG 给画师照着画（Assets/Art/Templates）；
/// ⑤ 场景里选中一个元素 → "跳到选中物体" 直接定位到它的名字。
/// 素材包资产 Assets/Resources/ArtKit/MyArtKit.asset 是你的内容，补丁永远不覆盖它。纯逻辑在 ArtKitRules（sim 验证）。
/// </summary>
public class ArtKitWindow : EditorWindow
{
    public const string AssetPath = "Assets/Resources/ArtKit/MyArtKit.asset";
    public const string TemplateFolder = "Assets/Art/Templates";

    private ArtKitSO kit;
    private Vector2 leftScroll, rightScroll;
    private string selected = "ArtSlot1";
    private string filter = "";
    private readonly Dictionary<string, bool> fold = new Dictionary<string, bool>();
    private readonly Dictionary<string, Texture2D> preview = new Dictionary<string, Texture2D>();

    [MenuItem("MarioTrickster/美术 Art/🎨 素材包 换图 + 互动", false, 39)]
    public static void Open()
    {
        var w = GetWindow<ArtKitWindow>("🎨 素材包");
        w.minSize = new Vector2(720, 460);
        w.kit = LoadOrCreate();
    }

    /// <summary>找素材包；没有就建一个（把 4 个素材槽的默认互动先填好，打开就能改）。</summary>
    public static ArtKitSO LoadOrCreate()
    {
        var k = AssetDatabase.LoadAssetAtPath<ArtKitSO>(AssetPath);
        if (k != null) return k;
        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        k = CreateInstance<ArtKitSO>();
        for (int i = 0; i < ArtKitRules.SlotChars.Length; i++)
            k.entries.Add(new ArtKitSO.Entry { key = ArtKitRules.SlotKey(i), displayName = ArtKitRules.SlotDefaultName[i], behavior = ArtKitRules.SlotDefault(i) });
        AssetDatabase.CreateAsset(k, AssetPath); AssetDatabase.SaveAssets();
        ArtKit.Reload();
        return k;
    }

    /// <summary>所有能换的名字：房间元素图标 + 地形 + 角色帧 + 小镇 / 装饰 / 特效 / 工坊元素 + 4 个素材槽 + 素材包里另外加的名字。</summary>
    public static List<string> AllKeys(ArtKitSO k)
    {
        var set = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var key in Step1Art.Icons.Keys) set.Add(key);
        foreach (var key in Step1Art.Tiles.Keys) set.Add(key);
        foreach (var key in Step1Art.Frames.Keys) set.Add(key);
        foreach (var key in WorldArt.AllKeys()) set.Add(key);
        for (int i = 0; i < ArtKitRules.SlotChars.Length; i++) set.Add(ArtKitRules.SlotKey(i));
        if (k != null) foreach (var e in k.entries) if (e != null && !string.IsNullOrEmpty(e.key)) set.Add(e.key);
        return set.ToList();
    }

    private void OnEnable() { if (kit == null) kit = AssetDatabase.LoadAssetAtPath<ArtKitSO>(AssetPath); }

    private void OnGUI()
    {
        if (kit == null) { if (GUILayout.Button("建一个素材包（Assets/Resources/ArtKit/MyArtKit.asset）", GUILayout.Height(40))) kit = LoadOrCreate(); return; }
        Toolbar();
        EditorGUILayout.BeginHorizontal();
        LeftList();
        RightPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void Toolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("📂 批量导入文件夹", EditorStyles.toolbarButton)) ImportFolder();
        if (GUILayout.Button("📤 导出模板 PNG", EditorStyles.toolbarButton)) ExportTemplates();
        if (GUILayout.Button("🎯 跳到选中物体", EditorStyles.toolbarButton)) JumpToSelection();
        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton)) { ArtKit.Reload(); preview.Clear(); }
        GUILayout.FlexibleSpace();
        filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField, GUILayout.Width(180));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("把图拖到右边的格子 = 全游戏换成这张（房间、小镇、图例都认）。关卡里画 z Z a r = 4 个素材槽，长相和互动都在这里定（默认：毒池掉半格 / 荆棘掉一格 / 回血泉 / 蛛网减速）。任何元素也能勾上互动，例如让内置毒池掉一格。", MessageType.Info);
    }

    private void LeftList()
    {
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.Width(300));
        foreach (var g in AllKeys(kit).Where(k => string.IsNullOrEmpty(filter) || k.ToLowerInvariant().Contains(filter.ToLowerInvariant())).GroupBy(ArtKitRules.GroupOf).OrderBy(g => g.Key.StartsWith("素材槽") ? 0 : 1))
        {
            if (!fold.TryGetValue(g.Key, out bool open)) open = g.Key.StartsWith("素材槽");
            open = EditorGUILayout.Foldout(open, $"{g.Key}（{g.Count()}）", true); fold[g.Key] = open;
            if (!open) continue;
            foreach (var key in g)
            {
                var e = kit.Find(key);
                string mark = e != null && e.sprite != null ? "🖼 " : "";
                if (e != null && e.behavior.enabled) mark += "⚡ ";
                EditorGUILayout.BeginHorizontal();
                var tex = Preview(key);
                GUILayout.Label(tex, GUILayout.Width(24), GUILayout.Height(24));
                var style = key == selected ? EditorStyles.boldLabel : EditorStyles.label;
                if (GUILayout.Button(mark + key, style)) { selected = key; GUI.FocusControl(null); }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void RightPanel()
    {
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.Label(selected, EditorStyles.largeLabel);
        int slot = ArtKitRules.SlotIndex(selected);
        if (slot >= 0) EditorGUILayout.HelpBox($"素材槽 {slot + 1}：关卡字符 '{ArtKitRules.SlotChars[slot]}'（最多 3 格宽，能走出来）。", MessageType.None);
        var big = Preview(selected); if (big != null) GUILayout.Label(big, GUILayout.Width(96), GUILayout.Height(96));

        var e = kit.Find(selected);
        var sp = (Sprite)EditorGUILayout.ObjectField("换上的图", e != null ? e.sprite : null, typeof(Sprite), false);
        if (sp != (e != null ? e.sprite : null)) { e = Ensure(selected); Undo.RecordObject(kit, "换图"); e.sprite = sp; if (sp != null) MakePixel(AssetDatabase.GetAssetPath(sp)); Changed(); }
        if (e == null) { if (GUILayout.Button("➕ 给这个名字加互动 / 动画帧")) { e = Ensure(selected); Changed(); } EditorGUILayout.EndScrollView(); return; }

        var so = new SerializedObject(kit);
        int idx = kit.entries.IndexOf(e);
        var prop = so.FindProperty("entries").GetArrayElementAtIndex(idx);
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("frames"), new GUIContent("动画帧（可选）"), true);
        EditorGUILayout.PropertyField(prop.FindPropertyRelative("fps"), new GUIContent("每秒几帧"));
        if (slot >= 0) EditorGUILayout.PropertyField(prop.FindPropertyRelative("displayName"), new GUIContent("工坊里显示的名字"));
        so.ApplyModifiedProperties();

        GUILayout.Space(8);
        GUILayout.Label("互动（进入范围的人）", EditorStyles.boldLabel);
        var b = e.behavior;
        EditorGUI.BeginChangeCheck();
        b.enabled = EditorGUILayout.Toggle("有互动", b.enabled);
        using (new EditorGUI.DisabledScope(!b.enabled))
        {
            int mode = b.damageHalves > 0 ? 0 : b.damageHalves < 0 ? 1 : 2;
            mode = EditorGUILayout.Popup("效果", mode, new[] { "掉血", "回血", "不掉也不回" });
            int amount = Mathf.Max(1, Mathf.Abs(b.damageHalves));
            if (mode != 2) amount = EditorGUILayout.IntPopup("每次多少", Mathf.Min(amount, ArtKitRules.MaxHalves), new[] { "半格", "一格", "一格半", "两格", "两格半", "三格" }, new[] { 1, 2, 3, 4, 5, 6 });
            b.damageHalves = mode == 0 ? amount : mode == 1 ? -amount : 0;
            b.tickSeconds = EditorGUILayout.Slider("每隔几秒", b.tickSeconds, ArtKitRules.MinTick, 5f);
            b.speedScale = EditorGUILayout.Slider("速度倍率（1 = 不减速）", b.speedScale, ArtKitRules.MinSpeed, 1f);
            b.stunSeconds = EditorGUILayout.Slider("每次晕几秒", b.stunSeconds, 0f, ArtKitRules.MaxStun);
            b.hitsMario = EditorGUILayout.Toggle("马里奥会中", b.hitsMario);
            b.hitsYou = EditorGUILayout.Toggle("你会中", b.hitsYou);
        }
        if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(kit, "改互动"); e.behavior = ArtKitRules.Clamp(b); Changed(); }
        EditorGUILayout.HelpBox(ArtKitRules.Describe(e.behavior), MessageType.None);
        if (slot >= 0 && GUILayout.Button("恢复这个槽的默认互动")) { Undo.RecordObject(kit, "恢复默认"); e.behavior = ArtKitRules.SlotDefault(slot); Changed(); }

        GUILayout.Space(8);
        if (GUILayout.Button("🗑 删掉这一行（回到内置图 / 默认互动）")) { Undo.RecordObject(kit, "删掉"); kit.entries.Remove(e); Changed(); }
        EditorGUILayout.EndScrollView();
    }

    private ArtKitSO.Entry Ensure(string key)
    {
        var e = kit.Find(key); if (e != null) return e;
        e = new ArtKitSO.Entry { key = key };
        int slot = ArtKitRules.SlotIndex(key);
        if (slot >= 0) { e.behavior = ArtKitRules.SlotDefault(slot); e.displayName = ArtKitRules.SlotDefaultName[slot]; }
        Undo.RecordObject(kit, "加一行"); kit.entries.Add(e); kit.Invalidate();
        return e;
    }

    private void Changed() { kit.Invalidate(); EditorUtility.SetDirty(kit); AssetDatabase.SaveAssets(); ArtKit.Reload(); preview.Remove(selected); Repaint(); }

    /// <summary>预览：素材包里换的图（贴图能读就裁出来）；否则内置字符画。</summary>
    private Texture2D Preview(string key)
    {
        if (preview.TryGetValue(key, out var t) && t != null) return t;
        var e = kit != null ? kit.Find(key) : null;
        if (e != null && e.sprite != null) t = AssetPreview.GetAssetPreview(e.sprite) ?? e.sprite.texture;
        if (t == null)
        {
            string[] rows = null;
            if (Step1Art.Frames.TryGetValue(key, out var f)) rows = f;
            else if (Step1Art.Tiles.TryGetValue(key, out var tl)) rows = tl;
            else if (Step1Art.Icons.TryGetValue(key, out var ic)) rows = ic;
            else rows = WorldArt.Rows(key);
            int slot = ArtKitRules.SlotIndex(key);
            if (rows == null && slot >= 0) rows = WorldArt.Rows(ArtKitRules.SlotDefaultArt[slot]) ?? (Step1Art.Icons.TryGetValue(ArtKitRules.SlotDefaultArt[slot], out var d) ? d : null);
            if (rows != null) t = FromRows(rows);
        }
        if (t != null) preview[key] = t;
        return t;
    }

    private static Texture2D FromRows(string[] rows)
    {
        var px = Step1Art.Rgba(rows); if (px == null) return null;
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        var cols = new Color[w * h]; for (int i = 0; i < cols.Length; i++) cols[i] = new Color(px[i * 4], px[i * 4 + 1], px[i * 4 + 2], px[i * 4 + 3]);
        tex.SetPixels(cols); tex.Apply(); return tex;
    }

    /// <summary>像素图导入设置：Sprite、Point、不压缩、可读（小镇地面要采样像素）。</summary>
    public static void MakePixel(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) return;
        bool dirty = ti.textureType != TextureImporterType.Sprite || ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed || !ti.isReadable || ti.mipmapEnabled;
        if (!dirty) return;
        ti.textureType = TextureImporterType.Sprite; ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.isReadable = true; ti.mipmapEnabled = false;
        ti.SaveAndReimport();
    }

    /// <summary>批量导入：选一个 Assets 里的文件夹，PNG 文件名对上名字就挂上（PoisonPool.png、TTree_v2.png …）。</summary>
    private void ImportFolder()
    {
        string abs = EditorUtility.OpenFolderPanel("选放 PNG 的文件夹（必须在 Assets 里）", "Assets", "");
        if (string.IsNullOrEmpty(abs)) return;
        string root = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
        abs = abs.Replace('\\', '/');
        if (!abs.StartsWith(root + "/Assets")) { EditorUtility.DisplayDialog("素材包", "文件夹要在项目的 Assets 里面（先把图拖进 Unity）。", "好"); return; }
        var keys = AllKeys(kit); int hit = 0; var miss = new List<string>();
        foreach (var file in Directory.GetFiles(abs, "*.png", SearchOption.AllDirectories))
        {
            string rel = file.Replace('\\', '/').Substring(root.Length + 1);
            string key = ArtKitRules.MatchFile(Path.GetFileName(file), keys);
            if (key == null) { miss.Add(Path.GetFileName(file)); continue; }
            MakePixel(rel);
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(rel); if (sp == null) { miss.Add(Path.GetFileName(file)); continue; }
            var e = Ensure(key); e.sprite = sp; hit++;
        }
        Changed();
        EditorUtility.DisplayDialog("素材包 · 批量导入", $"挂上 {hit} 张。" + (miss.Count > 0 ? $"\n对不上名字的 {miss.Count} 张（文件名要和名字一样，例如 PoisonPool.png）：\n" + string.Join("\n", miss.Take(20)) : ""), "好");
    }

    /// <summary>导出全部内置图（16×16 / 长图）成 PNG：画师照着尺寸和名字画，画好丢回一个文件夹点"批量导入"。</summary>
    public static void ExportTemplates()
    {
        Directory.CreateDirectory(TemplateFolder); int n = 0;
        void Save(string key, string[] rows)
        {
            if (rows == null) return; var t = FromRows(rows); if (t == null) return;
            File.WriteAllBytes($"{TemplateFolder}/{key}.png", t.EncodeToPNG()); n++;
        }
        foreach (var kv in Step1Art.Icons) Save(kv.Key, kv.Value);
        foreach (var kv in Step1Art.Tiles) Save(kv.Key, kv.Value);
        foreach (var kv in Step1Art.Frames) Save(kv.Key, kv.Value);
        foreach (var key in WorldArt.AllKeys()) Save(key, WorldArt.Rows(key));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("素材包 · 导出模板", $"导出 {n} 张到 {TemplateFolder}。照着名字和尺寸画（可以放大成 32×32，保持比例），画好放一个文件夹 → 批量导入。", "好");
    }

    private void JumpToSelection()
    {
        var go = Selection.activeGameObject; if (go == null) return;
        var t = go.transform;
        while (t != null) { string k = Step1ElementLabels.KeyOf(t.name); if (!string.IsNullOrEmpty(k) && AllKeys(kit).Contains(k)) { selected = k; filter = ""; fold[ArtKitRules.GroupOf(k)] = true; Repaint(); return; } t = t.parent; }
        ShowNotification(new GUIContent("选中的物体不是房间元素（名字应像 PoisonPool_3_2）"));
    }
}
