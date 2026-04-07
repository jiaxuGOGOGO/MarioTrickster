#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════
// PhysicsConfigSOEditor — S52 自定义 Inspector，在底部显示实时推导值
//
// S53 升级：
//   1. 修改 SO 参数后自动刷新 Scene 视图，让 JumpArcVisualizer 实时重绘
//   2. 显示 PhysicsMetrics Facade 联动状态，确认"唯一真理源"已生效
//   3. 显示含 Coyote Time 的最大可跨越间隙
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
        // 检测参数变化
        EditorGUI.BeginChangeCheck();

        // 绘制默认 Inspector（所有 [Range] 滑块）
        DrawDefaultInspector();

        // S53: 参数变化时刷新 Scene 视图，让 JumpArcVisualizer 实时重绘
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        PhysicsConfigSO config = (PhysicsConfigSO)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("实时推导值 (Derived Values)", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true); // 只读

        EditorGUILayout.FloatField("最大跳跃高度 (格)", config.DerivedMaxJumpHeight);
        EditorGUILayout.FloatField("满速平跳距离 (格)", config.DerivedMaxJumpDistance);
        EditorGUILayout.FloatField("短跳最低高度 (格)", config.DerivedMinJumpHeight);
        EditorGUILayout.FloatField("Coyote 额外距离 (格)", config.DerivedCoyoteBonusDistance);
        // S53: 显示含 Coyote 的最大可跨越间隙
        EditorGUILayout.FloatField("最大可跨越间隙 (格)",
            config.DerivedMaxJumpDistance + config.DerivedCoyoteBonusDistance);

        EditorGUI.EndDisabledGroup();

        // S53: Facade 联动状态指示
        EditorGUILayout.Space(5);
        bool isFacadeActive = PhysicsMetrics.ActiveConfig == config;
        if (isFacadeActive)
        {
            EditorGUILayout.HelpBox(
                "S53 唯一真理源已生效：此 SO 的推导值正在驱动 PhysicsMetrics、" +
                "验证器、JumpArcVisualizer 等全项目组件。拖动滑块即可全局同步。",
                MessageType.Info);
        }
        else if (PhysicsMetrics.ActiveConfig != null)
        {
            EditorGUILayout.HelpBox(
                "注意：PhysicsMetrics 当前绑定了另一个 PhysicsConfigSO 实例。" +
                "此 SO 的修改不会影响全局度量。",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "提示：将此 SO 放入 Resources 文件夹并命名为 PhysicsConfig，" +
                "即可自动绑定为 PhysicsMetrics 的唯一真理源。",
                MessageType.Info);
        }

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
