using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

public partial class TestConsoleWindow
{
    // ═══════════════════════════════════════════════════
    // Tab 3: Teleport & Reset (传送与状态管理)
    //
    // v2 清爽化重构要点：
    //   - HelpBox → CompactTip（节省垂直空间）
    //   - Stage 按钮改为 3 列布局（更紧凑）
    //   - Quick Actions 改为单行图标按钮
    //   - 动态锚点按钮改为紧凑单行卡片
    //   - 所有功能 100% 保留
    // ═══════════════════════════════════════════════════
    private void DrawTeleportTab()
    {
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);

        // ── 区块 1: Stage 快速传送（3 列紧凑网格） ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Stage Teleport", EditorStyles.boldLabel);
        LevelStudioStyles.CompactTip("一键传送 Mario+Trickster 到指定 Stage，相机硬切跟随 (PlayMode)");

        for (int i = 0; i < STAGE_NAMES.Length; i += 3)
        {
            EditorGUILayout.BeginHorizontal();
            DrawStageButton(i);
            if (i + 1 < STAGE_NAMES.Length) DrawStageButton(i + 1);
            if (i + 2 < STAGE_NAMES.Length) DrawStageButton(i + 2);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ── 区块 2: 动态锚点（Celeste Debug Map 风格） ──
        showDynamicAnchors = EditorGUILayout.Foldout(showDynamicAnchors,
            "Dynamic Level Anchors", true, EditorStyles.foldoutHeader);
        if (showDynamicAnchors)
        {
            DrawDynamicAnchorsSection();
        }

        EditorGUILayout.Space(4);

        // ── 区块 3: 自定义坐标 + Quick Actions（合并为一行组） ──
        EditorGUILayout.BeginVertical("box");

        // 自定义坐标传送（紧凑单行）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("XY:", GUILayout.Width(22));
        customTeleportX = EditorGUILayout.FloatField(customTeleportX, GUILayout.Width(55));
        customTeleportY = EditorGUILayout.FloatField(customTeleportY, GUILayout.Width(55));
        if (GUILayout.Button("Go", GUILayout.Width(32), GUILayout.Height(20)))
        {
            TeleportBothPlayers(new Vector3(customTeleportX, customTeleportY, 0));
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Quick Actions（紧凑按钮行）
        EditorGUILayout.BeginHorizontal();
        if (LevelStudioStyles.ColorButton("Revive Mario", LevelStudioStyles.AccentGreen, 24f))
        {
            ReviveMario();
        }
        if (LevelStudioStyles.ColorButton("Refill Energy", LevelStudioStyles.AccentBlue, 24f))
        {
            RefillEnergy();
        }
        if (LevelStudioStyles.ColorButton("Reset Elements", LevelStudioStyles.AccentOrange, 24f))
        {
            ResetAllElements();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUI.EndDisabledGroup();
    }

    private void DrawStageButton(int index)
    {
        bool isGoal = index == STAGE_NAMES.Length - 1;
        if (isGoal) GUI.color = new Color(0.5f, 1f, 0.5f);

        // 紧凑化：缩短标签，只显示编号+关键词
        string shortName = STAGE_NAMES[index];
        if (shortName.Length > 20) shortName = shortName.Substring(0, 20) + "…";

        if (GUILayout.Button(new GUIContent(shortName, STAGE_NAMES[index]), GUILayout.Height(26)))
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
    // ═════════════════════════════════════════════════════════

    /// <summary>动态传送锚点数据结构</summary>
    private struct TeleportAnchor
    {
        public string Name;
        public string Category;
        public Vector3 RawPosition;
        public Vector3 SafePosition;
        public bool IsDangerous;
        public Color ButtonColor;
        public Object SourceObject;
    }

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
            if (rec.Component == null || rec.Transform == null) continue;
            if (!POI_CATEGORIES.Contains(rec.Category)) continue;

            bool isDangerous = (rec.Category == ElementCategory.Trap ||
                                rec.Category == ElementCategory.Enemy ||
                                rec.Category == ElementCategory.Hazard);

            Vector3 rawPos = rec.Transform.position;
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

        // ── 源 2: SpawnPoint 标记 ──
        GameObject asciiRoot = GameObject.Find("AsciiLevel_Root");
        if (asciiRoot != null)
        {
            foreach (Transform child in asciiRoot.transform)
            {
                if (child == null) continue;
                if (child.name.StartsWith("MarioSpawn"))
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
                else if (child.name.StartsWith("TricksterSpawn"))
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

        // ── 源 3: GoalZone ──
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

        cachedAnchors.Sort((a, b) =>
        {
            int catCmp = string.Compare(a.Category, b.Category, System.StringComparison.Ordinal);
            return catCmp != 0 ? catCmp : a.RawPosition.x.CompareTo(b.RawPosition.x);
        });

        Debug.Log($"[TestConsole] Dynamic anchors refreshed: {cachedAnchors.Count} POIs found.");
    }

    /// <summary>绘制动态锚点区域 UI（v2 紧凑化）</summary>
    private void DrawDynamicAnchorsSection()
    {
        if (!EditorApplication.isPlaying)
        {
            LevelStudioStyles.CompactTip("进入 PlayMode 后自动扫描场景兴趣点 (POI)");
            return;
        }

        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            RefreshTeleportAnchors();
        }

        // 刷新按钮 + 计数（紧凑单行）
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh", GUILayout.Height(20), GUILayout.Width(60)))
        {
            RefreshTeleportAnchors();
        }
        GUILayout.Label($"{(cachedAnchors != null ? cachedAnchors.Count : 0)} POIs",
            EditorStyles.miniLabel, GUILayout.Width(50));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (cachedAnchors == null || cachedAnchors.Count == 0)
        {
            LevelStudioStyles.CompactTip("场景中未发现可传送的兴趣点");
            return;
        }

        // 紧凑锚点列表
        anchorScrollPos = EditorGUILayout.BeginScrollView(anchorScrollPos, GUILayout.MaxHeight(260));

        string currentCategory = "";
        for (int i = 0; i < cachedAnchors.Count; i++)
        {
            TeleportAnchor anchor = cachedAnchors[i];
            if (anchor.SourceObject == null) continue;

            // 分类标题
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

            // 紧凑单行锚点卡片
            EditorGUILayout.BeginHorizontal();
            GUI.color = anchor.ButtonColor;

            string dangerTag = anchor.IsDangerous ? "↑" : "";
            string btnLabel = $"{dangerTag}{anchor.Name} ({anchor.SafePosition.x:F0},{anchor.SafePosition.y:F0})";

            if (GUILayout.Button(btnLabel, GUILayout.Height(22)))
            {
                TeleportBothPlayers(anchor.SafePosition);
                Debug.Log($"[TestConsole] Teleported to: {anchor.Name} at ({anchor.SafePosition.x:F1}, {anchor.SafePosition.y:F1})");
            }

            GUI.color = Color.white;

            if (GUILayout.Button("F", GUILayout.Width(20), GUILayout.Height(22)))
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

    /// <summary>统计指定分类的有效锚点数量</summary>
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
