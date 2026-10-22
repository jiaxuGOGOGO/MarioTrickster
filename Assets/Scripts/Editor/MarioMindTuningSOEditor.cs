using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// S237：调参文件（RushMarioTuning）的 Inspector —— 250 个数值按「你想调什么」分 9 组折叠 + 最上面「常用 15 个」+ 搜索 + 只看改过的。
/// 以前：36 个按 Session 起名的小标题从头排到尾，找一个数要翻很久，改过哪些也看不出来。
/// 只换显示方式：字段名、默认值、数据版本都不变（旧资产照常读）；每一项仍是 Unity 自己的 PropertyField（撤销 / 多选都正常）。
/// 改过的数值名字前面有 ●，右边「↺」恢复成默认值。
/// </summary>
[CustomEditor(typeof(MarioMindTuningSO))]
public sealed class MarioMindTuningSOEditor : Editor
{
    static string search = "";
    static bool onlyChanged;
    static readonly Dictionary<string, bool> open = new Dictionary<string, bool>();
    MarioMindTuningSO defaults;
    List<(System.Reflection.FieldInfo field, string header)> fields;

    void OnEnable()
    {
        defaults = CreateInstance<MarioMindTuningSO>();
        fields = TuningGroups.Fields(typeof(MarioMindTuningSO)).Where(f => f.field.Name != "dataVersion").ToList();
    }

    void OnDisable() { if (defaults != null) DestroyImmediate(defaults); }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var t = (MarioMindTuningSO)target;
        EditorGUILayout.HelpBox("马里奥和所有机关的数值都在这一个文件。一次只改一个数，玩两局对比；测试中心 ① 体检会列出你改过哪些、数值之间有没有矛盾。\n● = 你改过（和默认不一样），↺ = 恢复默认。", MessageType.Info);

        var bad = TuningAudit.Check(t).Where(r => !r.ok).ToList();
        if (bad.Count > 0) EditorGUILayout.HelpBox("数值之间有 " + bad.Count + " 处矛盾：\n" + string.Join("\n", bad.Take(4).Select(r => "⚠ " + r.why + "（现在 " + r.detail + "）")), MessageType.Warning);

        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("🔍 搜", search);
            onlyChanged = GUILayout.Toggle(onlyChanged, "只看改过的", EditorStyles.miniButton, GUILayout.Width(80));
        }
        int changed = fields.Count(f => Changed(f.field));
        EditorGUILayout.LabelField($"共 {fields.Count} 项，你改过 {changed} 项 · 数据版本 v{t.dataVersion}", EditorStyles.miniLabel);

        string q = search.Trim().ToLowerInvariant();
        bool filtering = q.Length > 0 || onlyChanged;
        bool Match((System.Reflection.FieldInfo field, string header) f) =>
            (!onlyChanged || Changed(f.field)) &&
            (q.Length == 0 || (f.field.Name + " " + TuningGroups.Tip(f.field) + " " + ObjectNames.NicifyVariableName(f.field.Name)).ToLowerInvariant().Contains(q));

        Section("⭐ 常用 15 个", "试玩反馈里最常想改的", fields.Where(f => TuningGroups.Common.Contains(f.field.Name)).OrderBy(f => System.Array.IndexOf(TuningGroups.Common, f.field.Name)).Where(Match).ToList(), filtering, true);
        foreach (var g in TuningGroups.All)
            Section(g.title, g.why, fields.Where(f => g.headers.Contains(f.header)).Where(Match).ToList(), filtering, false);
        var rest = fields.Where(f => TuningGroups.GroupOf(f.header) == null).Where(Match).ToList();
        if (rest.Count > 0) Section("其他（还没分组）", "AI：在 TuningGroups.All 里给它们找个组", rest, filtering, false);

        serializedObject.ApplyModifiedProperties();
    }

    void Section(string title, string why, List<(System.Reflection.FieldInfo field, string header)> list, bool filtering, bool defaultOpen)
    {
        if (list.Count == 0) return;
        if (!open.TryGetValue(title, out bool o)) o = defaultOpen;
        int n = list.Count(f => Changed(f.field));
        o = EditorGUILayout.BeginFoldoutHeaderGroup(o || filtering, $"{title}（{list.Count}{(n > 0 ? $"，改过 {n}" : "")}）");
        if (!filtering) open[title] = o;
        if (o)
        {
            EditorGUILayout.LabelField(why, EditorStyles.miniLabel);
            foreach (var (field, _) in list)
            {
                var p = serializedObject.FindProperty(field.Name);
                if (p == null) continue;
                bool ch = Changed(field);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string tip = TuningGroups.Tip(field);
                    var label = new GUIContent((ch ? "● " : "") + ObjectNames.NicifyVariableName(field.Name), (tip.Length > 0 ? tip + "\n" : "") + field.Name + "（默认 " + Fmt(field.GetValue(defaults)) + "）");
                    EditorGUILayout.PropertyField(p, label, true);
                    using (new EditorGUI.DisabledScope(!ch))
                        if (GUILayout.Button(new GUIContent("↺", "恢复默认 " + Fmt(field.GetValue(defaults))), EditorStyles.miniButton, GUILayout.Width(22)))
                        {
                            Undo.RecordObject(target, "恢复默认 " + field.Name);
                            field.SetValue(target, field.GetValue(defaults));
                            EditorUtility.SetDirty(target);
                            serializedObject.Update();
                        }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    bool Changed(System.Reflection.FieldInfo f)
    {
        object a = f.GetValue(target), b = f.GetValue(defaults);
        if (a is float fa && b is float fb) return Mathf.Abs(fa - fb) > 1e-4f;
        return !Equals(a, b);
    }

    static string Fmt(object v) => v is float f ? f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : v is bool b ? (b ? "开" : "关") : v?.ToString() ?? "";
}
