#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// PhysicsConfigSOEditor — S52 自定义 Inspector，在底部显示实时推导值
//
// 设计目的：
//   当主理人在 PlayMode 拖动手感滑块时，Inspector 底部实时显示
//   当前参数下的跳跃高度、水平距离等推导值，无需手动计算。
//   这是"所见即所得"调参体验的关键组件。
// ═══════════════════════════════════════════════════════════════════

[CustomEditor(typeof(PhysicsConfigSO))]
public class PhysicsConfigSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认 Inspector（所有 [Range] 滑块）
        DrawDefaultInspector();

        PhysicsConfigSO config = (PhysicsConfigSO)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("实时推导值 (Derived Values)", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true); // 只读

        EditorGUILayout.FloatField("最大跳跃高度 (格)", config.DerivedMaxJumpHeight);
        EditorGUILayout.FloatField("满速平跳距离 (格)", config.DerivedMaxJumpDistance);
        EditorGUILayout.FloatField("短跳最低高度 (格)", config.DerivedMinJumpHeight);
        EditorGUILayout.FloatField("Coyote 额外距离 (格)", config.DerivedCoyoteBonusDistance);

        EditorGUI.EndDisabledGroup();

        // 安全警告
        if (config.DerivedMaxJumpHeight < 1.5f)
        {
            EditorGUILayout.HelpBox(
                "警告：当前跳跃高度不足 1.5 格，可能无法跨越标准高台！",
                MessageType.Warning);
        }

        if (config.DerivedMaxJumpDistance < 3f)
        {
            EditorGUILayout.HelpBox(
                "警告：当前平跳距离不足 3 格，可能无法跨越标准间隙！",
                MessageType.Warning);
        }
    }
}
#endif
