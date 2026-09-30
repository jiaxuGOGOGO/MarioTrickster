using UnityEditor;
using UnityEngine;

/// <summary>S206：编辑器里的一行输入框（Unity 没有自带的）。</summary>
public class LevelNameDialog : EditorWindow
{
    private string title2, message, value;
    private bool done, ok;

    public static string Ask(string title, string message, string initial)
    {
        var w = CreateInstance<LevelNameDialog>();
        w.titleContent = new GUIContent(title); w.title2 = title; w.message = message; w.value = initial ?? "";
        w.minSize = w.maxSize = new Vector2(360, 110);
        w.ShowModalUtility();
        return w.ok ? w.value.Trim() : null;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(message);
        GUI.SetNextControlName("in");
        value = EditorGUILayout.TextField(value);
        EditorGUI.FocusTextInControl("in");
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return) { ok = true; Close(); }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) Close();
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("取消", GUILayout.Width(70))) Close();
        if (GUILayout.Button("确定", GUILayout.Width(70))) { ok = true; Close(); }
        EditorGUILayout.EndHorizontal();
    }
}
