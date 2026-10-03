using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S234 台词编辑器（Ctrl+Alt+L）：不用打开 JSON，直接改居民 / 马里奥说的话。
/// 你改的全部存进 Assets/Resources/MyTownStories.json（你的文件，升级补丁永远不改它）；内置 TownStories.json 不动。
/// 每句：中文 / 英文 / 什么时候说 / 谁说 / 条件 / 是不是真心话；"这句什么时候会说？"在当前小镇上彩排 30 天告诉你答案。
/// Play 中保存 → 下一句就用新台词。
/// </summary>
public sealed class TownStoryEditorWindow : EditorWindow
{
    public const string BuiltPath = "Assets/Resources/" + TownStory.ResourceName + ".json";
    public const string UserPath = "Assets/Resources/" + TownStory.UserResourceName + ".json";
    public const string BackupDir = "MarioTricksterBackups/MyTownStories"; // 项目文件夹里、Assets 外面（不会被打进游戏）

    [MenuItem("MarioTrickster/台词编辑器 Lines %&l", false, 4)]
    public static void Open() { var w = GetWindow<TownStoryEditorWindow>("台词编辑器"); w.minSize = new Vector2(620, 420); w.Reload(); }

    // ── 读写（编辑器里所有地方都用这两个：工坊、体检、这个窗口）──
    public static TownStory.Line[] BuiltIn(out string err) { err = ""; return File.Exists(BuiltPath) ? TownStory.Parse(File.ReadAllText(BuiltPath), out err) : TownStory.Default.ToArray(); }
    public static string UserJson() => File.Exists(UserPath) ? File.ReadAllText(UserPath) : "";
    public static TownStory.Line[] Effective(out string err) { var b = BuiltIn(out err); var o = TownStory.ParseOverlay(UserJson(), b, out string ue); if (ue.Length > 0) err = err.Length > 0 ? err + "；" + ue : ue; return TownStory.Merge(b, o); }
    public static TownStory.Line[] Effective() => Effective(out _);
    public static void Save(TownStory.Overlay o)
    {
        if (File.Exists(UserPath)) { Directory.CreateDirectory(BackupDir); File.Copy(UserPath, Path.Combine(BackupDir, "MyTownStories_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json"), true);
            foreach (var f in Directory.GetFiles(BackupDir, "*.json").OrderByDescending(f => f).Skip(20)) File.Delete(f); } // 自动备份，留最近 20 份
        File.WriteAllText(UserPath, TownStory.OverlayToJson(o)); AssetDatabase.ImportAsset(UserPath);
        if (Application.isPlaying) OverworldGame.ReloadStories();
    }

    private TownStory.Line[] builtIn; private TownStory.Overlay mine; private string err = "";
    private string search = ""; private int filter; private Vector2 scroll; private string selId = ""; private string whenHeard = ""; private List<string> issues;
    private static readonly string[] Filters = { "全部", "我改过的", "真心话", "回小镇", "当场喊", "早上", "一天结束", "关掉的" };
    private static readonly string[] Whens = { "back", "witness", "morning", "dayend" };
    private static readonly string[] WhenZh = { "回到小镇时（住户）", "大机关在门口砸中时（住户）", "早上（镇上闲话）", "一天结束（马里奥）" };

    private bool handEdited; private bool pending; private double lastEdit;
    public static int Version; // S234：保存一次 +1（小镇工坊看到就重算"住户与故事"）
    private void OnFocus() { if (!pending) Reload(); } // 网页 / 手改过文件 → 切回来就看到
    private void OnEnable() { EditorApplication.update += Tick; }
    private void OnDisable() { EditorApplication.update -= Tick; if (pending) Commit(); }
    private void Tick() { if (pending && EditorApplication.timeSinceStartup - lastEdit > 0.8) Commit(); } // 停笔 0.8 秒再存（S220 防卡：不在每个按键都写盘）
    private void Reload()
    {
        handEdited = File.Exists(BuiltPath) && BuiltPathEditedByHand();
        builtIn = BuiltIn(out err); mine = TownStory.ParseOverlay(UserJson(), builtIn, out string ue); if (ue.Length > 0) err = ue; issues = null; whenHeard = "";
    }
    private TownStory.Line[] Table => TownStory.Merge(builtIn, mine);
    private bool Changed(string id) => mine.lines.Any(l => l.id == id);
    private void Commit() { pending = false; Save(mine); Version++; issues = null; whenHeard = ""; }
    private void Touch() { pending = true; lastEdit = EditorApplication.timeSinceStartup; issues = null; whenHeard = ""; }

    private int tab; // 0 居民 / 镇上 / 一天结束；1 马里奥中招喊的字
    private void OnGUI()
    {
        if (builtIn == null) Reload();
        tab = GUILayout.Toolbar(tab, new[] { "🏠 居民 / 镇上 / 结束时的话", "🍄 马里奥中招喊的字" });
        if (tab == 1) { MarioReactionEditor.Draw(); return; }
        EditorGUILayout.HelpBox("改这里 = 改你自己的台词文件 MyTownStories.json（升级补丁永远不碰它）。内置台词留着当底子：你改了哪句就换哪句，关掉哪句就不说哪句。" + (Application.isPlaying ? "\n现在在 Play：保存后下一句就用新的。" : ""), MessageType.Info);
        if (err.Length > 0) EditorGUILayout.HelpBox(err, MessageType.Warning);
        if (handEdited)
            if (GUILayout.Button(new GUIContent("⚠ 内置台词文件被直接改过 → 搬到我的台词（推荐）", "以前直接改 TownStories.json 的话，下次升级会被覆盖。点这里把改过的句子搬进 MyTownStories.json，内置文件恢复原样"))) MoveHandEdits();
        var t = Table;
        if (issues == null) issues = TownStory.Validate(t).Where(v => !v.StartsWith("·")).ToList();
        EditorGUILayout.BeginHorizontal();
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        filter = EditorGUILayout.Popup(filter, Filters, GUILayout.Width(90));
        if (GUILayout.Button("＋ 新加一句", GUILayout.Width(90))) AddNew(t);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField($"共 {t.Length} 句（我改过 / 新加 {mine.lines.Count}，关掉 {mine.off.Count}）　{(issues.Count == 0 ? "✓ 没发现写错的地方" : "⚠ " + issues.Count + " 处要看看")}", EditorStyles.miniBoldLabel);
        foreach (var i in issues.Take(4)) EditorGUILayout.LabelField(i, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.BeginHorizontal();
        // 左：列表
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(Mathf.Max(240, position.width * 0.42f)));
        var rows = filter == 7 ? builtIn.Where(l => mine.off.Contains(l.id)) : t.Where(Pass);
        foreach (var l in rows.Take(300))
        {
            string mark = mine.off.Contains(l.id) ? "✕ " : Changed(l.id) ? "✎ " : "";
            var st = new GUIStyle(l.id == selId ? EditorStyles.helpBox : EditorStyles.label) { wordWrap = false, richText = false };
            if (GUILayout.Button($"{mark}{(l.Sincere ? "♥" : "·")} {l.zh}", st)) { selId = l.id; whenHeard = ""; GUI.FocusControl(null); }
        }
        EditorGUILayout.EndScrollView();
        // 右：编辑这一句
        EditorGUILayout.BeginVertical();
        var cur = t.FirstOrDefault(x => x.id == selId) ?? builtIn.FirstOrDefault(x => x.id == selId);
        if (cur == null) EditorGUILayout.LabelField("← 点左边一句来改，或者点\"新加一句\"", EditorStyles.wordWrappedLabel);
        else DrawEditor(cur);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private bool Pass(TownStory.Line l)
    {
        if (search.Length > 0 && !(l.id + l.zh + l.en + string.Join(";", l.needs) + TraitOf(l)).Contains(search)) return false;
        switch (filter)
        {
            case 1: return Changed(l.id); case 2: return l.Sincere; case 3: return l.when == "back"; case 4: return l.when == "witness";
            case 5: return l.when == "morning"; case 6: return l.when == "dayend"; default: return true;
        }
    }
    private static string TraitOf(TownStory.Line l) { var n = l.needs.FirstOrDefault(x => x.StartsWith("trait=")); return n != null ? TownStory.TraitZh(n.Substring(6)) : ""; }

    private void DrawEditor(TownStory.Line cur)
    {
        bool off = mine.off.Contains(cur.id); bool isBuilt = builtIn.Any(b => b.id == cur.id);
        EditorGUILayout.LabelField($"{cur.id}　{(isBuilt ? (Changed(cur.id) ? "（内置，你改过）" : "（内置）") : "（你新加的）")}{(off ? "　已关掉" : "")}", EditorStyles.boldLabel);
        if (off) { if (GUILayout.Button("重新打开这句")) { mine.off.Remove(cur.id); Commit(); } return; }
        var e = TownStory.Clone(cur);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("中文（{name} = 住户名字，{looted} 之类 = 数字）");
        e.zh = EditorGUILayout.TextArea(e.zh, EditorStyles.textArea, GUILayout.MinHeight(44));
        EditorGUILayout.LabelField("英文（可以空着）");
        e.en = EditorGUILayout.TextArea(e.en, EditorStyles.textArea, GUILayout.MinHeight(30));
        int wi = System.Array.IndexOf(Whens, e.when); int nwi = EditorGUILayout.Popup("什么时候说", Mathf.Max(0, wi), WhenZh);
        if (nwi != wi) { e.when = Whens[nwi]; e.who = TownStory.WhoFor(e.when); }
        if (e.who == "door")
        {
            var traits = new[] { "（任何住户）" }.Concat(TownStory.Traits.Select(TownStory.TraitZh)).ToArray();
            var tn = e.needs.FirstOrDefault(n => n.StartsWith("trait=")); int ti = tn == null ? 0 : System.Array.IndexOf(TownStory.Traits, tn.Substring(6)) + 1;
            int nti = EditorGUILayout.Popup("谁说", Mathf.Max(0, ti), traits);
            if (nti != ti) { var rest = e.needs.Where(n => !n.StartsWith("trait=")).ToList(); if (nti > 0) rest.Insert(0, "trait=" + TownStory.Traits[nti - 1]); e.needs = rest.ToArray(); }
        }
        string nd = EditorGUILayout.DelayedTextField(new GUIContent("条件（分号隔开）", "例：result=defended;visits>=3。能用哪些写在下面"), string.Join(";", e.needs));
        e.needs = nd.Split(';').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        EditorGUILayout.LabelField("能用：" + string.Join(" ", TownStory.FactKeys.TryGetValue(e.when, out var ks) ? ks : new string[0]), EditorStyles.wordWrappedMiniLabel);
        bool sincere = EditorGUILayout.Toggle(new GUIContent("真心话 ♥", "真心话：只说一次、要先来往几次，屏幕上停 8 秒"), e.Sincere);
        if (sincere != e.Sincere) { e.tone = sincere ? "sincere" : "comic"; if (sincere) { e.once = true; e.tier = Mathf.Min(e.tier, 1); } }
        e.once = EditorGUILayout.Toggle("只说一次", e.once);
        e.coolDays = EditorGUILayout.IntSlider(new GUIContent("说过后几天内不再说", "冷却"), e.coolDays, 1, 14);
        e.tier = EditorGUILayout.IntPopup("优先级（小的先说）", e.tier, new[] { "0 最优先（大事）", "1 这个人专属", "2 通用" }, new[] { 0, 1, 2 });
        if (EditorGUI.EndChangeCheck() && e.zh.Trim().Length > 0)
        {
            mine.lines.RemoveAll(x => x.id == e.id);
            var b = builtIn.FirstOrDefault(x => x.id == e.id);
            if (b == null || !TownStory.Same(b, e)) mine.lines.Add(e); // 改回和内置一样 = 不再算"改过"
            Touch();
        }
        foreach (var v in TownStory.Validate(new[] { e }).Where(v => !v.StartsWith("·") && v.Contains(e.id))) EditorGUILayout.HelpBox(v, MessageType.Warning);
        if (e.once && HeardBefore(e.id)) EditorGUILayout.HelpBox("你已经在游戏里听过这句（只说一次）。改了也不会再说——想再听一遍：测试中心 → 🏠 忘掉居民记忆。", MessageType.None);
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("这句什么时候会说？", "在当前小镇（没打开就用样板小镇）上彩排 30 天"))) whenHeard = TownStory.WhenHeard(CurrentMap(), Table, e.id, 30);
        if (isBuilt && Changed(e.id) && GUILayout.Button("恢复成内置")) { mine.lines.RemoveAll(x => x.id == e.id); Commit(); }
        if (isBuilt && GUILayout.Button("关掉这句")) { mine.lines.RemoveAll(x => x.id == e.id); mine.off.Add(e.id); Commit(); }
        if (!isBuilt && GUILayout.Button("删掉")) { mine.lines.RemoveAll(x => x.id == e.id); selId = ""; Commit(); }
        EditorGUILayout.EndHorizontal();
        if (whenHeard.Length > 0) EditorGUILayout.HelpBox(whenHeard, MessageType.None);
        EditorGUILayout.LabelField("游戏里看起来：", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(TownStory.Show(e, TownStory.DefaultResident(1), null), EditorStyles.helpBox);
    }

    private static bool HeardBefore(string id) => PlayerPrefs.HasKey(OverworldGame.MemoryKey) && TownStory.MemFromJson(PlayerPrefs.GetString(OverworldGame.MemoryKey)).said.Contains(id);
    private void AddNew(TownStory.Line[] t)
    {
        var l = new TownStory.Line { id = TownStory.NewId(t, "line"), who = "door", when = "back", tier = 1, tone = "comic", coolDays = 3, zh = "（写你想让他说的话）", en = "", needs = new[] { "trait=baker" } };
        mine.lines.Add(l); selId = l.id; filter = 0; search = ""; Commit();
    }
    private static OverworldMap.Map CurrentMap()
    {
        var w = Resources.FindObjectsOfTypeAll<OverworldWorkshopWindow>().FirstOrDefault();
        return w != null && w.CurrentMap != null ? w.CurrentMap : OverworldMap.Parse(OverworldPack.SampleText);
    }
    private static bool BuiltPathEditedByHand() => File.ReadAllText(BuiltPath).Replace("\r", "") != TownStory.ToJson(TownStory.Default);
    private void MoveHandEdits()
    {
        var hand = BuiltIn(out _); var d = TownStory.Diff(TownStory.Default, hand);
        foreach (var l in d.lines) { mine.lines.RemoveAll(x => x.id == l.id); mine.lines.Add(l); }
        foreach (var id in d.off) if (!mine.off.Contains(id)) mine.off.Add(id);
        File.WriteAllText(BuiltPath, TownStory.ToJson(TownStory.Default)); AssetDatabase.ImportAsset(BuiltPath);
        Save(mine); Reload(); ShowNotification(new GUIContent($"搬好了：{d.lines.Count} 句改过的、{d.off.Count} 句关掉的"));
    }
}

/// <summary>S234：马里奥中招时头顶喊的字（MyMarioReactions.json）。只改字：动作、时长、晕多久都锁着（H6 / H9）。</summary>
public static class MarioReactionEditor
{
    public const string UserPath = "Assets/Resources/" + MarioReaction.UserResourceName + ".json";
    static MarioReaction.Beat[] edit; static string again, againEn;
    static readonly Dictionary<string, string> KindZh = new Dictionary<string, string> { { "hurt", "被烧 / 被打到" }, { "trip", "绊倒" }, { "slip", "踩香蕉皮" }, { "launch", "被弹飞" }, { "cage", "被关笼子" }, { "snare", "被套住" }, { "drop", "掉下去" }, { "pit", "掉坑" }, { "stop", "急停" } };
    static void Load()
    {
        string j = System.IO.File.Exists(UserPath) ? System.IO.File.ReadAllText(UserPath) : "";
        edit = MarioReaction.ApplyUserLines((MarioReaction.Beat[])MarioReaction.Default.Clone(), j, out _); MarioReaction.ApplyUserAgain(j); again = MarioReaction.AgainZh; againEn = MarioReaction.AgainEn;
    }
    public static void Draw()
    {
        if (edit == null) Load();
        EditorGUILayout.HelpBox("只改他头顶喊的字。动作、时长、晕多久不能在这里改（同一种坑永远同一种动作；演戏不延长晕眩）。存进你自己的 MyMarioReactions.json。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        for (int i = 0; i < edit.Length; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(KindZh.TryGetValue(edit[i].kind, out var z) ? z : edit[i].kind, GUILayout.Width(110));
            edit[i].zh = EditorGUILayout.TextField(edit[i].zh); edit[i].en = EditorGUILayout.TextField(edit[i].en, GUILayout.Width(120));
            if (GUILayout.Button("↺", GUILayout.Width(24))) { MarioReaction.TryGet(MarioReaction.Default, edit[i].kind, out var d); edit[i].zh = d.zh; edit[i].en = d.en; GUI.changed = true; }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("同一种坑本局第 3 次时喊：", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal(); again = EditorGUILayout.TextField(again); againEn = EditorGUILayout.TextField(againEn, GUILayout.Width(120)); EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            for (int i = 0; i < edit.Length; i++) if (string.IsNullOrWhiteSpace(edit[i].zh)) { MarioReaction.TryGet(MarioReaction.Default, edit[i].kind, out var d); edit[i].zh = d.zh; }
            if (string.IsNullOrWhiteSpace(again)) again = "又是这个？！";
            System.IO.File.WriteAllText(UserPath, MarioReaction.UserToJson(edit, again, againEn)); AssetDatabase.ImportAsset(UserPath);
            MarioReactionView.Reload();
        }
    }
}
