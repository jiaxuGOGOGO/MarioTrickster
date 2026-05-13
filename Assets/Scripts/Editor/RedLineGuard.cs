#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RedLineGuard — 红线防护系统
///
/// 核心职责：
///   从代码层面硬性阻止任何工具或脚本修改 PhysicsMetrics 红线值。
///   在编辑器中实时巡检角色碰撞体、Root Scale、CELL_SIZE/PPU 引用，
///   发现违规立即警告并提供一键修复。
///
/// 防护范围（红线）：
///   1. 角色碰撞体（Mario/Trickster）的 size 和 offset 必须匹配 PhysicsMetrics
///   2. Root 物体的 localScale 必须保持 (1,1,1)
///   3. CELL_SIZE 和 PPU 常量不可被运行时修改（编译期保证：const）
///
/// 触发时机：
///   - Scene 保存前自动巡检（EditorSceneManager.sceneSaving）
///   - 进入 Play Mode 前自动巡检（EditorApplication.playModeStateChanged）
///   - 手动菜单触发（MarioTrickster → 红线巡检）
///   - Apply Art 等工具执行后可调用 ValidateCharacterColliders()
///
/// 设计原则：
///   1. 零摩擦：自动运行，不需要用户主动操作
///   2. 不阻塞：只警告+自动修复，不弹 Dialog 打断工作流
///   3. 可回退：所有自动修复都经过 Undo 系统
///   4. 透明：所有检测和修复都有 Console 日志
///
/// [AI防坑警告]
///   1. 此脚本不能引用任何可能被删除的组件类型，使用字符串匹配
///   2. 碰撞体标准值必须从 PhysicsMetrics 读取，不能硬编码
///   3. 不要在 OnEditorUpdate 中做重量级检查，只在关键时机触发
/// </summary>
[InitializeOnLoad]
public static class RedLineGuard
{
    // =========================================================================
    // 配置
    // =========================================================================

    private const string LOG_PREFIX = "[RedLineGuard]";

    /// <summary>是否启用自动修复（关闭则只警告不修复）</summary>
    private static bool AutoRepairEnabled
    {
        get => EditorPrefs.GetBool("RedLineGuard_AutoRepair", true);
        set => EditorPrefs.SetBool("RedLineGuard_AutoRepair", value);
    }

    /// <summary>碰撞体值的容差（小于此值视为匹配）</summary>
    private const float TOLERANCE = 0.01f;

    // =========================================================================
    // 初始化：注册事件钩子
    // =========================================================================

    static RedLineGuard()
    {
        // Scene 保存前巡检
        EditorSceneManager.sceneSaving += OnSceneSaving;

        // 进入 Play Mode 前巡检
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    // =========================================================================
    // 菜单入口
    // =========================================================================

    [MenuItem("MarioTrickster/红线巡检 (Red Line Check)", false, 2000)]
    public static void MenuRunFullCheck()
    {
        int issues = RunFullCheck(autoRepair: false);
        if (issues == 0)
        {
            Debug.Log($"{LOG_PREFIX} ✅ 红线巡检通过，没有发现违规。");
        }
        else
        {
            Debug.LogWarning($"{LOG_PREFIX} ⚠️ 发现 {issues} 个红线违规，详见上方日志。");
        }
    }

    [MenuItem("MarioTrickster/红线自动修复 (Red Line Auto-Fix)", false, 2001)]
    public static void MenuRunAutoFix()
    {
        int issues = RunFullCheck(autoRepair: true);
        if (issues == 0)
        {
            Debug.Log($"{LOG_PREFIX} ✅ 红线巡检通过，没有需要修复的问题。");
        }
        else
        {
            Debug.Log($"{LOG_PREFIX} ✅ 已自动修复 {issues} 个红线违规。");
        }
    }

    [MenuItem("MarioTrickster/红线防护设置/启用自动修复", false, 2010)]
    private static void ToggleAutoRepair()
    {
        AutoRepairEnabled = !AutoRepairEnabled;
        Debug.Log($"{LOG_PREFIX} 自动修复已{(AutoRepairEnabled ? "启用" : "禁用")}");
    }

    [MenuItem("MarioTrickster/红线防护设置/启用自动修复", true)]
    private static bool ToggleAutoRepairValidate()
    {
        Menu.SetChecked("MarioTrickster/红线防护设置/启用自动修复", AutoRepairEnabled);
        return true;
    }

    // =========================================================================
    // 事件钩子
    // =========================================================================

    private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        int issues = RunFullCheck(autoRepair: AutoRepairEnabled);
        if (issues > 0 && AutoRepairEnabled)
        {
            Debug.Log($"{LOG_PREFIX} Scene 保存前自动修复了 {issues} 个红线违规。");
        }
        else if (issues > 0)
        {
            Debug.LogWarning($"{LOG_PREFIX} ⚠️ Scene 保存时发现 {issues} 个红线违规！请手动检查。");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;

        int issues = RunFullCheck(autoRepair: AutoRepairEnabled);
        if (issues > 0 && AutoRepairEnabled)
        {
            Debug.Log($"{LOG_PREFIX} Play Mode 前自动修复了 {issues} 个红线违规。");
        }
        else if (issues > 0)
        {
            Debug.LogError($"{LOG_PREFIX} ❌ 进入 Play Mode 前发现 {issues} 个红线违规！" +
                           "建议修复后再运行，否则物理行为可能异常。");
        }
    }

    // =========================================================================
    // 核心巡检逻辑
    // =========================================================================

    /// <summary>
    /// 执行完整的红线巡检。返回发现的违规数量。
    /// </summary>
    /// <param name="autoRepair">是否自动修复违规</param>
    /// <returns>违规数量（如果 autoRepair=true，返回的是修复前的违规数）</returns>
    public static int RunFullCheck(bool autoRepair = false)
    {
        int totalIssues = 0;

        totalIssues += CheckCharacterColliders(autoRepair);
        totalIssues += CheckRootScales(autoRepair);
        totalIssues += CheckSizeSyncOnCharacters(autoRepair);
        totalIssues += CheckCharacterVisualOffset(autoRepair);

        return totalIssues;
    }

    /// <summary>
    /// 公共接口：验证所有角色碰撞体是否匹配 PhysicsMetrics 标准值。
    /// 供 AutoFitCollider、Apply Art 等工具在执行后调用。
    /// </summary>
    public static int ValidateCharacterColliders(bool autoRepair = false)
    {
        return CheckCharacterColliders(autoRepair);
    }

    // =========================================================================
    // 检查 1：角色碰撞体
    // =========================================================================

    private static int CheckCharacterColliders(bool autoRepair)
    {
        int issues = 0;

        // 查找场景中所有 BoxCollider2D
        BoxCollider2D[] allColliders = Object.FindObjectsOfType<BoxCollider2D>(true);

        foreach (BoxCollider2D col in allColliders)
        {
            if (col == null || col.gameObject == null) continue;

            // 检测是否是角色
            CharacterType charType = DetectCharacterType(col.gameObject);
            if (charType == CharacterType.None) continue;

            // 获取标准值
            float stdWidth, stdHeight, stdOffsetY;
            string charName;
            GetStandardCollider(charType, out stdWidth, out stdHeight, out stdOffsetY, out charName);

            // 检查 size
            bool sizeWrong = Mathf.Abs(col.size.x - stdWidth) > TOLERANCE ||
                             Mathf.Abs(col.size.y - stdHeight) > TOLERANCE;
            bool offsetWrong = Mathf.Abs(col.offset.y - stdOffsetY) > TOLERANCE;

            if (sizeWrong || offsetWrong)
            {
                issues++;
                string objPath = GetGameObjectPath(col.gameObject);

                if (autoRepair)
                {
                    Undo.RecordObject(col, "RedLineGuard: Restore Character Collider");
                    col.size = new Vector2(stdWidth, stdHeight);
                    col.offset = new Vector2(0f, stdOffsetY);
                    EditorUtility.SetDirty(col);
                    Debug.LogWarning($"{LOG_PREFIX} 🔧 已修复 {charName} 碰撞体: {objPath}\n" +
                                     $"  size: ({col.size.x:F3}, {col.size.y:F3}) → ({stdWidth}, {stdHeight})\n" +
                                     $"  offset.y: {col.offset.y:F3} → {stdOffsetY}");
                }
                else
                {
                    Debug.LogError($"{LOG_PREFIX} ❌ {charName} 碰撞体违规: {objPath}\n" +
                                   $"  当前 size: ({col.size.x:F3}, {col.size.y:F3}), 标准: ({stdWidth}, {stdHeight})\n" +
                                   $"  当前 offset.y: {col.offset.y:F3}, 标准: {stdOffsetY}\n" +
                                   $"  使用 MarioTrickster → 红线自动修复 可一键修正。");
                }
            }
        }

        return issues;
    }

    // =========================================================================
    // 检查 2：Root Scale
    // =========================================================================

    private static int CheckRootScales(bool autoRepair)
    {
        int issues = 0;

        // 查找所有有 BoxCollider2D 的物体（Root 层）
        // 只检查有角色控制器的 Root
        MonoBehaviour[] allBehaviours = Object.FindObjectsOfType<MonoBehaviour>(true);

        HashSet<GameObject> checkedRoots = new HashSet<GameObject>();

        foreach (MonoBehaviour mb in allBehaviours)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;

            bool isCharacterRoot = typeName.Contains("MarioController") ||
                                   typeName.Contains("TricksterController") ||
                                   typeName.Contains("PlayerController");

            if (!isCharacterRoot) continue;

            GameObject root = mb.gameObject;
            if (checkedRoots.Contains(root)) continue;
            checkedRoots.Add(root);

            Vector3 scale = root.transform.localScale;
            bool scaleWrong = Mathf.Abs(scale.x - 1f) > TOLERANCE ||
                              Mathf.Abs(scale.y - 1f) > TOLERANCE ||
                              Mathf.Abs(scale.z - 1f) > TOLERANCE;

            if (scaleWrong)
            {
                issues++;
                string objPath = GetGameObjectPath(root);

                if (autoRepair)
                {
                    Undo.RecordObject(root.transform, "RedLineGuard: Restore Root Scale");
                    root.transform.localScale = Vector3.one;
                    EditorUtility.SetDirty(root);
                    Debug.LogWarning($"{LOG_PREFIX} 🔧 已修复角色 Root Scale: {objPath}\n" +
                                     $"  ({scale.x:F3}, {scale.y:F3}, {scale.z:F3}) → (1, 1, 1)\n" +
                                     $"  如需调整视觉大小，请修改 Visual 子物体的 localScale。");
                }
                else
                {
                    Debug.LogError($"{LOG_PREFIX} ❌ 角色 Root Scale 违规: {objPath}\n" +
                                   $"  当前: ({scale.x:F3}, {scale.y:F3}, {scale.z:F3}), 标准: (1, 1, 1)\n" +
                                   $"  角色 Root 不应参与视觉缩放，请调整 Visual 子物体的 localScale。");
                }
            }
        }

        return issues;
    }

    // =========================================================================
    // 检查 3：Size Sync 不应作用于角色
    // =========================================================================

    private static int CheckSizeSyncOnCharacters(bool autoRepair)
    {
        // Size Sync（LevelEditorPickingManager）是黄线功能，
        // 但如果它意外修改了角色碰撞体，就触碰了红线。
        // 这里不直接检查 Size Sync 的开关状态，而是通过检查 1 和 2 间接保护。
        // 如果角色碰撞体被 Size Sync 改了，检查 1 会捕获并修复。
        return 0;
    }

    // =========================================================================
    // 检查 4：角色 Visual 子节点 Y 偏移（视碰对齐 — 防止悬空回归）
    // =========================================================================

    private static int CheckCharacterVisualOffset(bool autoRepair)
    {
        int issues = 0;

        MonoBehaviour[] allBehaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
        HashSet<GameObject> checkedRoots = new HashSet<GameObject>();

        foreach (MonoBehaviour mb in allBehaviours)
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;

            bool isMario = typeName.Contains("MarioController");
            bool isTrickster = typeName.Contains("TricksterController");
            if (!isMario && !isTrickster) continue;

            GameObject root = mb.gameObject;
            if (checkedRoots.Contains(root)) continue;
            checkedRoots.Add(root);

            Transform visual = root.transform.Find("Visual");
            if (visual == null)
            {
                // 没有 Visual 子节点，跳过（可能是旧场景结构）
                continue;
            }

            float expectedY = isTrickster
                ? PhysicsMetrics.TRICKSTER_VISUAL_OFFSET_Y
                : PhysicsMetrics.MARIO_VISUAL_OFFSET_Y;

            if (Mathf.Abs(visual.localPosition.y - expectedY) > TOLERANCE)
            {
                issues++;
                string objPath = GetGameObjectPath(root);

                if (autoRepair)
                {
                    Undo.RecordObject(visual, "RedLineGuard: Fix Visual Offset (Anti-Hover)");
                    Vector3 pos = visual.localPosition;
                    pos.y = expectedY;
                    visual.localPosition = pos;
                    EditorUtility.SetDirty(visual.gameObject);
                    Debug.LogWarning($"{LOG_PREFIX} 🔧 已修复角色 Visual 偏移(防悬空): {objPath}\n" +
                                     $"  Visual.localPosition.y: {visual.localPosition.y:F3} → {expectedY}");
                }
                else
                {
                    Debug.LogError($"{LOG_PREFIX} ❌ 角色 Visual 偏移违规(会导致悬空): {objPath}\n" +
                                   $"  当前 Visual.localPosition.y: {visual.localPosition.y:F3}, 标准: {expectedY}\n" +
                                   $"  使用 MarioTrickster → 红线自动修复 可一键修正。");
                }
            }
        }

        return issues;
    }

    // =========================================================================
    // 辅助方法
    // =========================================================================

    private enum CharacterType { None, Mario, Trickster }

    private static CharacterType DetectCharacterType(GameObject go)
    {
        if (go == null) return CharacterType.None;

        // 检查自身和父物体上的控制器组件
        GameObject[] targets = go.transform.parent != null
            ? new[] { go, go.transform.parent.gameObject }
            : new[] { go };

        foreach (GameObject target in targets)
        {
            foreach (var comp in target.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName.Contains("MarioController") || typeName.Contains("PlayerController"))
                    return CharacterType.Mario;
                if (typeName.Contains("TricksterController"))
                    return CharacterType.Trickster;
            }
        }

        return CharacterType.None;
    }

    private static void GetStandardCollider(CharacterType type,
        out float width, out float height, out float offsetY, out string name)
    {
        switch (type)
        {
            case CharacterType.Trickster:
                width = PhysicsMetrics.TRICKSTER_COLLIDER_WIDTH;
                height = PhysicsMetrics.TRICKSTER_COLLIDER_HEIGHT;
                offsetY = PhysicsMetrics.TRICKSTER_COLLIDER_OFFSET_Y;
                name = "Trickster";
                return;
            default:
                width = PhysicsMetrics.MARIO_COLLIDER_WIDTH;
                height = PhysicsMetrics.MARIO_COLLIDER_HEIGHT;
                offsetY = PhysicsMetrics.MARIO_COLLIDER_OFFSET_Y;
                name = "Mario";
                return;
        }
    }

    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}
#endif
