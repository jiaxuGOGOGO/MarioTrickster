using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 2: 传送与状态管理
    // ═══════════════════════════════════════════════════
    private void DrawTeleportTab()
    {
        EditorGUILayout.LabelField("Stage Quick Teleport", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "一键将 Mario + Trickster 传送到指定 Stage，相机硬切跟随。\n仅在 PlayMode 下可用。",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        // Stage 按钮网格（2 列布局）
        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < STAGE_NAMES.Length; i += 2)
        {
            EditorGUILayout.BeginHorizontal();
            DrawStageButton(i);
            if (i + 1 < STAGE_NAMES.Length)
            {
                DrawStageButton(i + 1);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // ═══════════════════════════════════════════════════
        // S33: 动态锚点系统 — 自动扫描场景中的兴趣点 (POI)
        // 设计理念（参考 Celeste Debug Map）：
        //   - 不硬编码任何坐标，通过“场景自省”动态发现传送目标
        //   - 优先从 LevelElementRegistry 查询（已有 Fake Null 防御）
        //   - 补充扫描 SpawnPoint、GoalZone 等非 Registry 对象
        //   - 白名单过滤：仅保留有调试价值的 POI，剔除纯静态地形噪声
        //   - 危险对象自动叠加安全传送偏移量
        // ═══════════════════════════════════════════════════
        showDynamicAnchors = EditorGUILayout.Foldout(showDynamicAnchors,
            "Dynamic Level Anchors (动态关卡锚点)", true, EditorStyles.foldoutHeader);
        if (showDynamicAnchors)
        {
            DrawDynamicAnchorsSection();
        }

        EditorGUILayout.Space(8);

        // 自定义坐标传送
        EditorGUILayout.LabelField("Custom Teleport", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        customTeleportX = EditorGUILayout.FloatField("X", customTeleportX);
        customTeleportY = EditorGUILayout.FloatField("Y", customTeleportY);
        if (GUILayout.Button("Go", GUILayout.Width(40)))
        {
            TeleportBothPlayers(new Vector3(customTeleportX, customTeleportY, 0));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // 角色状态快速操作
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Revive Mario\n(满血复活)", GUILayout.Height(40)))
        {
            ReviveMario();
        }
        if (GUILayout.Button("Refill Energy\n(补满能量)", GUILayout.Height(40)))
        {
            RefillEnergy();
        }
        if (GUILayout.Button("Reset Elements\n(重置关卡)", GUILayout.Height(40)))
        {
            ResetAllElements();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawStageButton(int index)
    {
        bool isGoal = index == STAGE_NAMES.Length - 1;
        if (isGoal) GUI.color = new Color(0.5f, 1f, 0.5f);

        if (GUILayout.Button(STAGE_NAMES[index], GUILayout.Height(30)))
        {
            TeleportToStage(index);
        }

        if (isGoal) GUI.color = Color.white;
    }


    // ═══════════════════════════════════════════════════
    // 传送逻辑
    // ═══════════════════════════════════════════════════

    // [AI防坑警告] 传送后必须调用 CameraController.SnapToTarget() 实现相机硬切！
    // 绝对不能让相机花 5 秒钟缓慢滑动过去，这是核心红线。
    // SnapToTarget() 会重置 smoothDampVelocity、lookAheadVelocity、currentLookAhead、
    // smoothedSpeed、isMoving、lastTargetPosition 等所有平滑状态。

    /// <summary>传送到指定 Stage（0-based index，最后一个为 GoalZone）</summary>
    private void TeleportToStage(int stageIndex)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        Vector3 targetPos;

        if (stageIndex < 9)
        {
            float stageStartX = stageIndex * TOTAL_STAGE_UNIT;
            targetPos = new Vector3(stageStartX + 3f, 1f, 0f);
        }
        else
        {
            float s9 = 8 * TOTAL_STAGE_UNIT;
            float s9SubWidth = 8f;
            float goalX = s9 + 9 * s9SubWidth + 2f;
            targetPos = new Vector3(goalX - 3f, 1f, 0f);
        }

        TeleportBothPlayers(targetPos);

        Debug.Log($"[TestConsole] Teleported to {STAGE_NAMES[stageIndex]} at ({targetPos.x:F1}, {targetPos.y:F1})");
    }

    // [AI防坑警告] 此方法末尾的 SnapToTarget() 调用是核心红线，绝对不能删除！
    // 没有它，传送后相机会花 5 秒慢飘过去，严重浪费测试时间。
    /// <summary>将 Mario 和 Trickster 传送到指定位置，相机硬切</summary>
    private void TeleportBothPlayers(Vector3 position)
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        // 传送 Mario
        if (cachedMario != null)
        {
            cachedMario.transform.position = position;
            Rigidbody2D marioRb = cachedMario.GetComponent<Rigidbody2D>();
            if (marioRb != null) marioRb.velocity = Vector2.zero;
        }

        // 传送 Trickster（偏移 2 格，避免重叠）
        if (cachedTrickster != null)
        {
            cachedTrickster.transform.position = position + Vector3.right * 2f;
            Rigidbody2D tricksterRb = cachedTrickster.GetComponent<Rigidbody2D>();
            if (tricksterRb != null) tricksterRb.velocity = Vector2.zero;
        }

        // [AI防坑警告] 相机硬切 — 核心红线，绝对不能删除或改为平滑跟随！
        if (cachedCamera != null)
        {
            cachedCamera.SnapToTarget();
        }
    }

    // ═══════════════════════════════════════════════════
    // 角色状态操作
    // ═══════════════════════════════════════════════════

    private void ReviveMario()
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        if (cachedMarioHealth != null)
        {
            cachedMarioHealth.ResetHealth();
            Debug.Log("[TestConsole] Mario revived (full HP).");
        }

        if (cachedMario != null)
        {
            cachedMario.enabled = true;
            // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
            SpriteRenderer sr = cachedMario.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
        }
    }

    private void RefillEnergy()
    {
        if (!EditorApplication.isPlaying) return;
        EnsureCache();

        if (cachedEnergy != null)
        {
            cachedEnergy.ResetEnergy();
            Debug.Log("[TestConsole] Trickster energy refilled.");
        }
    }

    private void ResetAllElements()
    {
        if (!EditorApplication.isPlaying) return;

        LevelElementRegistry.ResetAll();

        GoalZone[] goalZones = Object.FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            gz.ResetTrigger();
        }

        ControllablePropBase[] props = Object.FindObjectsOfType<ControllablePropBase>();
        foreach (ControllablePropBase prop in props)
        {
            prop.ResetUses();
        }

        Debug.Log("[TestConsole] All level elements reset.");
    }


    // ═════════════════════════════════════════════════════════
    // S33: 动态锚点系统 (Dynamic Teleport Anchors)
    //
    // 设计背景：
    //   Level Builder 生成的 ASCII 关卡布局不固定，无法像 TestSceneBuilder 的
    //   9-Stage 那样硬编码传送坐标。参考 Celeste Debug Map 的“场景自省”理念，
    //   通过运行时扫描自动发现关卡中的兴趣点 (POI)，动态生成传送按钮。
    //
    // 核心原则：
    //   1. 优先从 LevelElementRegistry 查询（已有 Fake Null 防御）
    //   2. 补充扫描 SpawnPoint、GoalZone 等非 Registry 对象
    //   3. 白名单过滤：仅保留有调试价值的 POI，剔除纯静态地形噪声
    //   4. 危险对象自动叠加安全传送偏移量 (Vector3.up * 2f)
    //   5. [System.NonSerialized] 缓存 + 懒加载，Domain Reload 安全
    //   6. Fake Null 防御：遍历时跳过已销毁对象
    //   7. ScrollView 限制最大高度，防止大量元素撑爆窗口
    // ═════════════════════════════════════════════════════════

    /// <summary>动态传送锚点数据结构</summary>
    private struct TeleportAnchor
    {
        public string Name;           // 显示名称
        public string Category;       // 分组名称
        public Vector3 RawPosition;   // 原始坐标
        public Vector3 SafePosition;  // 安全传送坐标（危险对象已叠加偏移）
        public bool IsDangerous;      // 是否为危险对象
        public Color ButtonColor;     // 按钮颜色
        public Object SourceObject;   // 源对象引用（用于 Fake Null 检测）
    }

    // ─────────────────────────────────────────────────────────
    // POI 白名单：仅保留有调试价值的分类
    // 剔除 Platform 和 Misc — 这些多为纯静态地形，传送过去没有调试意义
    // ─────────────────────────────────────────────────────────
    private static readonly HashSet<ElementCategory> POI_CATEGORIES = new HashSet<ElementCategory>
    {
        ElementCategory.Trap,
        ElementCategory.Enemy,
        ElementCategory.Hazard,
        ElementCategory.HiddenPassage,
        ElementCategory.Collectible,
        ElementCategory.Checkpoint
    };

    /// <summary>刷新动态锚点缓存</summary>
    private void RefreshTeleportAnchors()
    {
        cachedAnchors = new List<TeleportAnchor>();

        // ── 源 1: LevelElementRegistry 查询（白名单过滤） ──
        foreach (var rec in LevelElementRegistry.GetAll())
        {
            // Fake Null 防御：跳过已销毁的对象
            if (rec.Component == null || rec.Transform == null) continue;

            // 白名单过滤：仅保留 POI 分类
            if (!POI_CATEGORIES.Contains(rec.Category)) continue;

            bool isDangerous = (rec.Category == ElementCategory.Trap ||
                                rec.Category == ElementCategory.Enemy ||
                                rec.Category == ElementCategory.Hazard);

            Vector3 rawPos = rec.Transform.position;
            // 安全传送偏移：危险对象在目标上方 2 个单位，避免落地瞬间触发受击
            Vector3 safePos = isDangerous ? rawPos + Vector3.up * 2f : rawPos + Vector3.up * 0.5f;

            Color btnColor;
            switch (rec.Category)
            {
                case ElementCategory.Trap:           btnColor = new Color(1f, 0.4f, 0.4f); break;
                case ElementCategory.Enemy:          btnColor = new Color(1f, 0.5f, 0.3f); break;
                case ElementCategory.Hazard:         btnColor = new Color(1f, 0.3f, 0.5f); break;
                case ElementCategory.HiddenPassage:  btnColor = new Color(0.6f, 0.4f, 1f); break;
                case ElementCategory.Collectible:    btnColor = new Color(1f, 0.9f, 0.3f); break;
                case ElementCategory.Checkpoint:     btnColor = new Color(0.3f, 1f, 0.5f); break;
                default:                             btnColor = Color.white; break;
            }

            cachedAnchors.Add(new TeleportAnchor
            {
                Name = rec.Name,
                Category = rec.Category.ToString(),
                RawPosition = rawPos,
                SafePosition = safePos,
                IsDangerous = isDangerous,
                ButtonColor = btnColor,
                SourceObject = rec.Component
            });
        }

        // ── 源 2: SpawnPoint 标记（非 Registry 对象） ──
        GameObject asciiRoot = GameObject.Find("AsciiLevel_Root");
        if (asciiRoot != null)
        {
            foreach (Transform child in asciiRoot.transform)
            {
                if (child == null) continue;
                if (child.name.StartsWith("MarioSpawnPoint"))
                {
                    cachedAnchors.Add(new TeleportAnchor
                    {
                        Name = "Mario Spawn",
                        Category = "Spawn",
                        RawPosition = child.position,
                        SafePosition = child.position + Vector3.up * 0.5f,
                        IsDangerous = false,
                        ButtonColor = new Color(0.2f, 0.8f, 0.2f),
                        SourceObject = child.gameObject
                    });
                }
                else if (child.name.StartsWith("TricksterSpawnPoint"))
                {
                    cachedAnchors.Add(new TeleportAnchor
                    {
                        Name = "Trickster Spawn",
                        Category = "Spawn",
                        RawPosition = child.position,
                        SafePosition = child.position + Vector3.up * 0.5f,
                        IsDangerous = false,
                        ButtonColor = new Color(0.3f, 0.7f, 1f),
                        SourceObject = child.gameObject
                    });
                }
            }
        }

        // ── 源 3: GoalZone（非 Registry 对象） ──
        GoalZone[] goalZones = Object.FindObjectsOfType<GoalZone>();
        foreach (GoalZone gz in goalZones)
        {
            if (gz == null) continue;
            cachedAnchors.Add(new TeleportAnchor
            {
                Name = "GoalZone",
                Category = "Goal",
                RawPosition = gz.transform.position,
                SafePosition = gz.transform.position + Vector3.left * 2f + Vector3.up * 0.5f,
                IsDangerous = false,
                ButtonColor = new Color(0.5f, 1f, 0.5f),
                SourceObject = gz
            });
        }

        // 按分类名称 + X 坐标排序，保证 UI 稳定
        cachedAnchors.Sort((a, b) =>
        {
            int catCmp = string.Compare(a.Category, b.Category, System.StringComparison.Ordinal);
            return catCmp != 0 ? catCmp : a.RawPosition.x.CompareTo(b.RawPosition.x);
        });

        Debug.Log($"[TestConsole] Dynamic anchors refreshed: {cachedAnchors.Count} POIs found.");
    }

    /// <summary>绘制动态锚点区域 UI</summary>
    private void DrawDynamicAnchorsSection()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "动态锚点仅在 PlayMode 下可用。\n进入 PlayMode 后自动扫描场景中的兴趣点。",
                MessageType.Info);
            return;
        }

        // S33: 懒加载/状态校验拦截（审计意见第 5 点）
        // Domain Reload 后 [System.NonSerialized] 字段会被清空，
        // 在绘制入口做懒加载检查，确保从编辑态进入运行态时自动完成首次扫描。
        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            RefreshTeleportAnchors();
        }

        // 手动刷新按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Anchors", GUILayout.Height(22)))
        {
            RefreshTeleportAnchors();
        }
        EditorGUILayout.LabelField($"{(cachedAnchors != null ? cachedAnchors.Count : 0)} POIs",
            EditorStyles.miniLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            EditorGUILayout.HelpBox("场景中未发现可传送的兴趣点。", MessageType.Info);
            return;
        }

        // S33: ScrollView 限制最大高度（审计意见第 2 点）
        // 防止大量元素撑爆窗口，底部的 Custom Teleport 和 Quick Actions 始终可达。
        anchorScrollPos = EditorGUILayout.BeginScrollView(anchorScrollPos,
            GUILayout.MaxHeight(300));

        // 按分类分组显示（Foldout 折叠）
        string currentCategory = "";
        for (int i = 0; i < cachedAnchors.Count; i++)
        {
            TeleportAnchor anchor = cachedAnchors[i];

            // S33: Fake Null 防御（审计意见第 1 点）
            // 游玩过程中敌人被踩死、一次性陷阱被 Destroy 后，
            // 缓存列表中的引用在 Unity 底层会变成 null。
            // 必须在访问其属性前检查，否则会抛 MissingReferenceException。
            if (anchor.SourceObject == null) continue;

            // 分类标题 + Foldout
            if (anchor.Category != currentCategory)
            {
                currentCategory = anchor.Category;
                if (!anchorCategoryFoldouts.ContainsKey(currentCategory))
                    anchorCategoryFoldouts[currentCategory] = true;
                anchorCategoryFoldouts[currentCategory] = EditorGUILayout.Foldout(
                    anchorCategoryFoldouts[currentCategory],
                    $"{currentCategory} ({CountAnchorsInCategory(currentCategory)})",
                    true, EditorStyles.foldoutHeader);
            }

            if (!anchorCategoryFoldouts.ContainsKey(currentCategory) ||
                !anchorCategoryFoldouts[currentCategory])
                continue;

            // 绘制传送按钮
            EditorGUILayout.BeginHorizontal();
            GUI.color = anchor.ButtonColor;

            string dangerTag = anchor.IsDangerous ? " [SAFE+2]" : "";
            string btnLabel = $"{anchor.Name}{dangerTag}\n({anchor.SafePosition.x:F1}, {anchor.SafePosition.y:F1})";

            if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
            {
                TeleportBothPlayers(anchor.SafePosition);
                Debug.Log($"[TestConsole] Teleported to dynamic anchor: {anchor.Name} " +
                          $"at ({anchor.SafePosition.x:F1}, {anchor.SafePosition.y:F1})" +
                          (anchor.IsDangerous ? " [safe offset applied]" : ""));
            }

            GUI.color = Color.white;

            // 聚焦按钮：在 Scene View 中定位到该元素
            if (GUILayout.Button("F", GUILayout.Width(22), GUILayout.Height(32)))
            {
                if (anchor.SourceObject is Component comp && comp != null)
                {
                    Selection.activeGameObject = comp.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
                else if (anchor.SourceObject is GameObject go && go != null)
                {
                    Selection.activeGameObject = go;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>统计指定分类的有效锚点数量（跳过已销毁对象）</summary>
    private int CountAnchorsInCategory(string category)
    {
        if (cachedAnchors == null) return 0;
        int count = 0;
        foreach (var a in cachedAnchors)
        {
            if (a.Category == category && a.SourceObject != null)
                count++;
        }
        return count;
    }
}
