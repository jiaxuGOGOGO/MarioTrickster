#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ═══════════════════════════════════════════════════════════════
// PlatformHandlesEditor.cs
// 为 MovingPlatform 和 ControllablePlatform 提供 Scene 视图拖拽手柄，
// 让策划可以直接在场景中可视化拖拽平台终点 (pointB)。
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// MovingPlatform 的 CustomEditor：在 Scene 视图中绘制 pointB 拖拽手柄。
/// </summary>
[CustomEditor(typeof(MovingPlatform)), CanEditMultipleObjects]
public class MovingPlatformHandlesEditor : Editor
{
    private FieldInfo pointBField;

    private void OnEnable()
    {
        pointBField = typeof(MovingPlatform).GetField("pointB",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private void OnSceneGUI()
    {
        if (pointBField == null) return;

        MovingPlatform platform = (MovingPlatform)target;
        Transform t = platform.transform;

        Vector3 pointB = (Vector3)pointBField.GetValue(platform);
        Vector3 worldEndPoint = t.position + pointB;

        // 绘制连接线
        Handles.color = Color.cyan;
        Handles.DrawDottedLine(t.position, worldEndPoint, 4f);

        // 绘制拖拽手柄
        EditorGUI.BeginChangeCheck();
        Vector3 newWorldEndPoint = Handles.PositionHandle(worldEndPoint, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(platform, "Change Point B");
            Vector3 newPointB = newWorldEndPoint - t.position;
            pointBField.SetValue(platform, newPointB);
            EditorUtility.SetDirty(platform);
        }

        // 绘制终点标签
        Handles.color = Color.white;
        Handles.Label(worldEndPoint + Vector3.up * 0.3f, "Point B");
    }
}

/// <summary>
/// ControllablePlatform 的 CustomEditor：在 Scene 视图中绘制 pointB 拖拽手柄。
/// </summary>
[CustomEditor(typeof(ControllablePlatform)), CanEditMultipleObjects]
public class ControllablePlatformHandlesEditor : Editor
{
    private FieldInfo pointBField;

    private void OnEnable()
    {
        pointBField = typeof(ControllablePlatform).GetField("pointB",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private void OnSceneGUI()
    {
        if (pointBField == null) return;

        ControllablePlatform platform = (ControllablePlatform)target;
        Transform t = platform.transform;

        Vector3 pointB = (Vector3)pointBField.GetValue(platform);
        Vector3 worldEndPoint = t.position + pointB;

        // 绘制连接线
        Handles.color = Color.cyan;
        Handles.DrawDottedLine(t.position, worldEndPoint, 4f);

        // 绘制拖拽手柄
        EditorGUI.BeginChangeCheck();
        Vector3 newWorldEndPoint = Handles.PositionHandle(worldEndPoint, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(platform, "Change Point B");
            Vector3 newPointB = newWorldEndPoint - t.position;
            pointBField.SetValue(platform, newPointB);
            EditorUtility.SetDirty(platform);
        }

        // 绘制终点标签
        Handles.color = Color.white;
        Handles.Label(worldEndPoint + Vector3.up * 0.3f, "Point B");
    }
}
#endif
