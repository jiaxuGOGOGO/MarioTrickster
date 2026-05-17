#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEditor;

/// <summary>
/// GameplayBoxVisualizer — 上下文感知（Context-Aware）智能降噪 Gameplay Boxes 可视化工具。
///
/// 在 Scene 视图中实时遍历场景并绘制语义 Box：
///   · 白色线框 — 挂载 Rigidbody2D 或属于 Land 层的身体盒 (Solid)
///   · 蓝色线框 — 挂载 PlayerHealth 的受击盒 (HurtBox)
///   · 红色线框 — 挂载 DamageDealer / BaseHazard 的攻击盒 (HitBox)
///   · 青色线框 — 挂载 ScanAbility 的扫描范围
///   · Trap Phase 文字 — ControllablePropBase 物体上方显示当前阶段
///
/// 智能降噪策略：
///   · 全局未被选中的物体仅绘制极低透明度（Alpha=0.1）轮廓
///   · 只有当物体本身就是 Selection.activeGameObject 时，
///     才以高亮不透明颜色绘制其 HitBox/HurtBox
///   · 避免父子层级误触发高亮，保护 Scene 视图视觉心流
///
/// 开关由 GameplayBoxVisualizer.ShowGameplayBoxes / ShowTrapPhase 控制，
/// TestConsoleWindow.Cheats Tab 中提供 Toggle 入口。
///
/// [AI防坑警告]
///   - 全部包裹在 #if UNITY_EDITOR || DEVELOPMENT_BUILD 宏下，Release 包零残留。
///   - 不使用 IMGUI (OnGUI)，Scene 视图绘制使用 Gizmos + Handles。
///   - Land 层为项目实际使用的 Solid 层名称（TagManager.asset 确认）。
/// </summary>
[InitializeOnLoad]
public static class GameplayBoxVisualizer
{
    // ═══════════════════════════════════════════════════
    // 公共开关
    // ═══════════════════════════════════════════════════
    public static bool ShowGameplayBoxes = false;
    public static bool ShowTrapPhase = false;

    // ═══════════════════════════════════════════════════
    // 颜色常量 — 高亮（选中物体）
    // ═══════════════════════════════════════════════════
    private static readonly Color SolidColor = Color.white;
    private static readonly Color HurtBoxColor = new Color(0.3f, 0.5f, 1f, 0.9f);       // 蓝色
    private static readonly Color HitBoxColor = new Color(1f, 0.2f, 0.2f, 0.9f);         // 红色
    private static readonly Color ScanRangeColor = new Color(0f, 1f, 1f, 0.7f);          // 青色

    // ═══════════════════════════════════════════════════
    // 降噪透明度
    // ═══════════════════════════════════════════════════
    private const float DIMMED_ALPHA = 0.1f;

    private static int landLayerIndex = -1;

    static GameplayBoxVisualizer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!ShowGameplayBoxes && !ShowTrapPhase) return;
        if (!EditorApplication.isPlaying) return;

        if (landLayerIndex < 0)
        {
            landLayerIndex = LayerMask.NameToLayer("Land");
        }

        if (ShowGameplayBoxes)
        {
            DrawSolidBoxes();
            DrawHurtBoxes();
            DrawHitBoxes();
            DrawScanRanges();
        }

        if (ShowTrapPhase)
        {
            DrawTrapPhaseLabels();
        }
    }

    // ═══════════════════════════════════════════════════
    // 上下文感知：只认当前真实选中的物体
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 判断给定 GameObject 是否就是当前真实选中的物体。
    /// 用户要求以 Selection.activeGameObject 为唯一高亮信号，避免父子层级误触发。
    /// </summary>
    private static bool IsTrulySelectedObject(GameObject go)
    {
        return go != null && Selection.activeGameObject == go;
    }

    /// <summary>
    /// 根据选中状态返回适当的颜色：真实选中时高亮，未选中时极低透明度。
    /// </summary>
    private static Color GetContextColor(Color baseColor, GameObject go)
    {
        if (IsTrulySelectedObject(go))
        {
            return baseColor;
        }

        return new Color(baseColor.r, baseColor.g, baseColor.b, DIMMED_ALPHA);
    }

    // ═══════════════════════════════════════════════════
    // 白色线框 — Rigidbody2D 或 Land 层
    // ═══════════════════════════════════════════════════
    private static void DrawSolidBoxes()
    {
        // Rigidbody2D 物体
        Rigidbody2D[] bodies = Object.FindObjectsOfType<Rigidbody2D>();
        foreach (Rigidbody2D rb in bodies)
        {
            if (rb.GetComponent<PlayerHealth>() != null) continue;
            if (rb.GetComponent<DamageDealer>() != null) continue;
            if (rb.GetComponent<BaseHazard>() != null) continue;

            Color color = GetContextColor(SolidColor, rb.gameObject);
            DrawCollidersOnObject(rb.gameObject, color);
        }

        // Land 层物体（无 Rigidbody2D 的静态碰撞体）
        if (landLayerIndex >= 0)
        {
            Collider2D[] allColliders = Object.FindObjectsOfType<Collider2D>();
            foreach (Collider2D col in allColliders)
            {
                if (col.gameObject.layer != landLayerIndex) continue;
                if (col.GetComponent<Rigidbody2D>() != null) continue;
                if (col.GetComponent<PlayerHealth>() != null) continue;
                if (col.GetComponent<DamageDealer>() != null) continue;
                if (col.GetComponent<BaseHazard>() != null) continue;

                Color color = GetContextColor(SolidColor, col.gameObject);
                DrawCollider(col, color);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 蓝色线框 — PlayerHealth 受击盒
    // ═══════════════════════════════════════════════════
    private static void DrawHurtBoxes()
    {
        PlayerHealth[] healths = Object.FindObjectsOfType<PlayerHealth>();
        foreach (PlayerHealth health in healths)
        {
            Color color = GetContextColor(HurtBoxColor, health.gameObject);
            DrawCollidersOnObject(health.gameObject, color);
        }
    }

    // ═══════════════════════════════════════════════════
    // 红色线框 — DamageDealer / BaseHazard 攻击盒
    // ═══════════════════════════════════════════════════
    private static void DrawHitBoxes()
    {
        DamageDealer[] dealers = Object.FindObjectsOfType<DamageDealer>();
        foreach (DamageDealer dd in dealers)
        {
            Color color = GetContextColor(HitBoxColor, dd.gameObject);
            DrawCollidersOnObject(dd.gameObject, color);
        }

        BaseHazard[] hazards = Object.FindObjectsOfType<BaseHazard>();
        foreach (BaseHazard hz in hazards)
        {
            if (hz.GetComponent<DamageDealer>() != null) continue;
            Color color = GetContextColor(HitBoxColor, hz.gameObject);
            DrawCollidersOnObject(hz.gameObject, color);
        }
    }

    // ═══════════════════════════════════════════════════
    // 青色线框 — ScanAbility 扫描范围
    // ═══════════════════════════════════════════════════
    private static void DrawScanRanges()
    {
        ScanAbility[] scans = Object.FindObjectsOfType<ScanAbility>();
        foreach (ScanAbility scan in scans)
        {
            Color color = GetContextColor(ScanRangeColor, scan.gameObject);
            Handles.color = color;
            Handles.DrawWireDisc(scan.transform.position, Vector3.forward, scan.ScanRadius);
        }
    }

    // ═══════════════════════════════════════════════════
    // Trap Phase 文字标签
    // ═══════════════════════════════════════════════════
    private static void DrawTrapPhaseLabels()
    {
        ControllablePropBase[] props = Object.FindObjectsOfType<ControllablePropBase>();
        foreach (ControllablePropBase prop in props)
        {
            PropControlState state = prop.GetControlState();
            string stateText = state.ToString();

            Vector3 worldPos = prop.transform.position + Vector3.up * 1.5f;

            // 根据阶段着色
            Color textColor;
            switch (state)
            {
                case PropControlState.Telegraph:
                    textColor = new Color(1f, 0.9f, 0.2f);
                    break;
                case PropControlState.Active:
                    textColor = new Color(1f, 0.3f, 0.2f);
                    break;
                case PropControlState.Recovery:
                    textColor = new Color(0.3f, 0.8f, 1f);
                    break;
                case PropControlState.Cooldown:
                    textColor = new Color(0.6f, 0.6f, 0.6f);
                    break;
                case PropControlState.Exhausted:
                    textColor = new Color(0.4f, 0.4f, 0.4f);
                    break;
                default: // Idle
                    textColor = new Color(0.5f, 1f, 0.5f);
                    break;
            }

            // 智能降噪：未被真实选中的 Trap 标签也降低透明度
            if (!IsTrulySelectedObject(prop.gameObject))
            {
                textColor.a = DIMMED_ALPHA;
            }

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = textColor },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            string label = $"[{prop.PropName}] {stateText}";
            Handles.Label(worldPos, label, style);
        }
    }

    // ═══════════════════════════════════════════════════
    // 辅助方法
    // ═══════════════════════════════════════════════════

    private static void DrawCollidersOnObject(GameObject go, Color color)
    {
        Collider2D[] colliders = go.GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            DrawCollider(col, color);
        }

        // 也检查子物体上的碰撞体（视碰分离架构）
        Collider2D[] childColliders = go.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in childColliders)
        {
            if (col.gameObject == go) continue;
            DrawCollider(col, color);
        }
    }

    private static void DrawCollider(Collider2D col, Color color)
    {
        if (col == null || !col.enabled) return;

        Handles.color = color;

        if (col is BoxCollider2D box)
        {
            DrawBoxCollider(box, color);
        }
        else if (col is CircleCollider2D circle)
        {
            Vector3 center = circle.transform.TransformPoint(circle.offset);
            float radius = circle.radius * Mathf.Max(
                Mathf.Abs(circle.transform.lossyScale.x),
                Mathf.Abs(circle.transform.lossyScale.y));
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }
        else if (col is CapsuleCollider2D capsule)
        {
            Vector3 center = capsule.transform.TransformPoint(capsule.offset);
            Vector2 size = capsule.size;
            size.x *= Mathf.Abs(capsule.transform.lossyScale.x);
            size.y *= Mathf.Abs(capsule.transform.lossyScale.y);
            DrawWireRect(center, size, capsule.transform.rotation, color);
        }
        else if (col is PolygonCollider2D poly)
        {
            for (int pathIdx = 0; pathIdx < poly.pathCount; pathIdx++)
            {
                Vector2[] points = poly.GetPath(pathIdx);
                if (points.Length < 2) continue;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 a = poly.transform.TransformPoint(points[i]);
                    Vector3 b = poly.transform.TransformPoint(points[(i + 1) % points.Length]);
                    Handles.DrawLine(a, b);
                }
            }
        }
    }

    private static void DrawBoxCollider(BoxCollider2D box, Color color)
    {
        Vector3 center = box.transform.TransformPoint(box.offset);
        Vector2 size = box.size;
        size.x *= Mathf.Abs(box.transform.lossyScale.x);
        size.y *= Mathf.Abs(box.transform.lossyScale.y);
        DrawWireRect(center, size, box.transform.rotation, color);
    }

    private static void DrawWireRect(Vector3 center, Vector2 size, Quaternion rotation, Color color)
    {
        Handles.color = color;

        Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        Vector3[] corners = new Vector3[4]
        {
            center + rotation * new Vector3(-halfSize.x, -halfSize.y, 0f),
            center + rotation * new Vector3( halfSize.x, -halfSize.y, 0f),
            center + rotation * new Vector3( halfSize.x,  halfSize.y, 0f),
            center + rotation * new Vector3(-halfSize.x,  halfSize.y, 0f)
        };

        Handles.DrawLine(corners[0], corners[1]);
        Handles.DrawLine(corners[1], corners[2]);
        Handles.DrawLine(corners[2], corners[3]);
        Handles.DrawLine(corners[3], corners[0]);
    }
}
#endif
