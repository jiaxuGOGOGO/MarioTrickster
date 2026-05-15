#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEditor;

/// <summary>
/// GameplayBoxVisualizer — 防误判 Gameplay Boxes 可视化工具。
///
/// 在 Scene 视图中实时遍历场景并绘制语义 Box：
///   · 白色线框 — 挂载 Rigidbody2D 或属于 Land 层的身体盒 (Solid)
///   · 蓝色线框 — 挂载 PlayerHealth 的受击盒 (HurtBox)
///   · 红色线框 — 挂载 DamageDealer / BaseHazard 的攻击盒 (HitBox)
///   · 青色线框 — 挂载 ScanAbility 的扫描范围
///   · Trap Phase 文字 — ControllablePropBase 物体上方显示当前阶段
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
    // 颜色常量
    // ═══════════════════════════════════════════════════
    private static readonly Color SolidColor = Color.white;
    private static readonly Color HurtBoxColor = new Color(0.3f, 0.5f, 1f, 0.9f);       // 蓝色
    private static readonly Color HitBoxColor = new Color(1f, 0.2f, 0.2f, 0.9f);         // 红色
    private static readonly Color ScanRangeColor = new Color(0f, 1f, 1f, 0.7f);          // 青色
    private static readonly Color TrapPhaseTextBg = new Color(0f, 0f, 0f, 0.6f);

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
    // 白色线框 — Rigidbody2D 或 Land 层
    // ═══════════════════════════════════════════════════
    private static void DrawSolidBoxes()
    {
        // Rigidbody2D 物体
        Rigidbody2D[] bodies = Object.FindObjectsOfType<Rigidbody2D>();
        foreach (Rigidbody2D rb in bodies)
        {
            // 排除已被其他类别覆盖的物体（PlayerHealth / DamageDealer / BaseHazard）
            if (rb.GetComponent<PlayerHealth>() != null) continue;
            if (rb.GetComponent<DamageDealer>() != null) continue;
            if (rb.GetComponent<BaseHazard>() != null) continue;

            DrawCollidersOnObject(rb.gameObject, SolidColor);
        }

        // Land 层物体（无 Rigidbody2D 的静态碰撞体）
        if (landLayerIndex >= 0)
        {
            Collider2D[] allColliders = Object.FindObjectsOfType<Collider2D>();
            foreach (Collider2D col in allColliders)
            {
                if (col.gameObject.layer != landLayerIndex) continue;
                if (col.GetComponent<Rigidbody2D>() != null) continue; // 已在上面处理
                if (col.GetComponent<PlayerHealth>() != null) continue;
                if (col.GetComponent<DamageDealer>() != null) continue;
                if (col.GetComponent<BaseHazard>() != null) continue;

                DrawCollider(col, SolidColor);
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
            DrawCollidersOnObject(health.gameObject, HurtBoxColor);
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
            DrawCollidersOnObject(dd.gameObject, HitBoxColor);
        }

        BaseHazard[] hazards = Object.FindObjectsOfType<BaseHazard>();
        foreach (BaseHazard hz in hazards)
        {
            // 避免与 DamageDealer 重复绘制
            if (hz.GetComponent<DamageDealer>() != null) continue;
            DrawCollidersOnObject(hz.gameObject, HitBoxColor);
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
            Handles.color = ScanRangeColor;
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
                    textColor = new Color(1f, 0.9f, 0.2f); // 黄色
                    break;
                case PropControlState.Active:
                    textColor = new Color(1f, 0.3f, 0.2f); // 红色
                    break;
                case PropControlState.Recovery:
                    textColor = new Color(0.3f, 0.8f, 1f); // 浅蓝
                    break;
                case PropControlState.Cooldown:
                    textColor = new Color(0.6f, 0.6f, 0.6f); // 灰色
                    break;
                case PropControlState.Exhausted:
                    textColor = new Color(0.4f, 0.4f, 0.4f); // 深灰
                    break;
                default: // Idle
                    textColor = new Color(0.5f, 1f, 0.5f); // 绿色
                    break;
            }

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = textColor },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };

            // 绘制带背景的标签
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
            if (col.gameObject == go) continue; // 已在上面处理
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
            // 简化为外接矩形
            Vector3 center = capsule.transform.TransformPoint(capsule.offset);
            Vector2 size = capsule.size;
            size.x *= Mathf.Abs(capsule.transform.lossyScale.x);
            size.y *= Mathf.Abs(capsule.transform.lossyScale.y);
            DrawWireRect(center, size, capsule.transform.rotation, color);
        }
        else if (col is PolygonCollider2D poly)
        {
            // 绘制多边形轮廓
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
