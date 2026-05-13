using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 关卡编辑器拾取管理器。
///
/// 核心职责（v4）：
///   1. Root 模式：无论点击/框选到 Visual，最终都重定向到 Root，方便移动/旋转。
///   2. Visual 模式：无论点击/框选到 Root，最终都重定向到 Visual，避免 Root + Visual 同时被选中。
///   3. Size Sync 模式：在视碰分离结构下，保持 Visual.localScale 与 Root.BoxCollider2D.size 的
///      原始比例联动；编辑哪一侧，另一侧都按比例同步，且不会改动角色 Root.localScale。
///
/// 设计原则：
///   - 只处理标准的 Root -> Visual 结构。
///   - 不依赖具体元素类型白名单，后续新增机关只要沿用该结构即可自动生效。
///   - Root.localScale 从不参与角色/机关的视碰分离同步，避免破坏 Mario / Trickster 的核心逻辑。
/// </summary>
[InitializeOnLoad]
public static class LevelEditorPickingManager
{
    private const string PREF_KEY = "MarioTrickster_PickingMode";
    private const string PREF_KEY_SIZE_SYNC = "MarioTrickster_PickingSizeSync";

    // 0 = Root 模式（默认）
    // 1 = Visual 模式
    private static bool _isProcessingSelection;

    private struct SizeSyncState
    {
        public Vector3 baseVisualScale;
        public Vector2 baseColliderSize;
        public Vector3 lastVisualScale;
        public Vector2 lastColliderSize;
    }

    private struct VisualColliderPair
    {
        public GameObject root;
        public Transform visual;
        public BoxCollider2D collider;
    }

    private static readonly Dictionary<int, SizeSyncState> SizeSyncStates = new Dictionary<int, SizeSyncState>();

    static LevelEditorPickingManager()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    /// <summary>当前是否为 Root 模式（点击/框选最终都选 Root）。</summary>
    public static bool IsRootMode => EditorPrefs.GetInt(PREF_KEY, 0) == 0;

    /// <summary>当前是否启用视碰尺寸联动。</summary>
    public static bool IsSizeSyncEnabled => EditorPrefs.GetBool(PREF_KEY_SIZE_SYNC, false);

    /// <summary>设置拾取模式。</summary>
    public static void SetMode(bool rootMode)
    {
        EditorPrefs.SetInt(PREF_KEY, rootMode ? 0 : 1);
        OnSelectionChanged();
    }

    /// <summary>设置尺寸联动开关。</summary>
    public static void SetSizeSyncEnabled(bool enabled)
    {
        EditorPrefs.SetBool(PREF_KEY_SIZE_SYNC, enabled);

        if (!enabled)
            SizeSyncStates.Clear();
        else
            CaptureCurrentSelectionState();
    }

    /// <summary>
    /// 保留公共 SyncState 接口以兼容既有调用。
    /// 这里改为刷新当前选择缓存，确保新生成元素立刻接入拾取/尺寸联动。
    /// </summary>
    public static void SyncState()
    {
        CaptureCurrentSelectionState();
    }

    private static void OnSelectionChanged()
    {
        if (_isProcessingSelection)
            return;

        Object[] currentSelection = Selection.objects;
        if (currentSelection == null || currentSelection.Length == 0)
        {
            SizeSyncStates.Clear();
            return;
        }

        bool needRedirection = false;
        HashSet<Object> redirectedSelection = new HashSet<Object>();

        foreach (Object obj in currentSelection)
        {
            if (obj is GameObject go && TryMapSelectionForCurrentMode(go, out GameObject mappedGo))
            {
                redirectedSelection.Add(mappedGo);
                needRedirection |= mappedGo != go;
            }
            else
            {
                redirectedSelection.Add(obj);
            }
        }

        if (!needRedirection)
        {
            CaptureCurrentSelectionState();
            return;
        }

        Object[] finalArray = redirectedSelection.ToArray();
        EditorApplication.delayCall += () =>
        {
            _isProcessingSelection = true;
            Selection.objects = finalArray;
            _isProcessingSelection = false;
            CaptureCurrentSelectionState();
        };
    }

    private static bool TryMapSelectionForCurrentMode(GameObject go, out GameObject mappedGo)
    {
        mappedGo = go;

        if (IsRootMode)
        {
            if (IsVisualNode(go))
            {
                mappedGo = go.transform.parent.gameObject;
                return true;
            }

            return false;
        }

        if (TryGetVisualChild(go, out GameObject visualChild))
        {
            mappedGo = visualChild;
            return true;
        }

        return false;
    }

    private static void OnEditorUpdate()
    {
        if (Application.isPlaying || !IsSizeSyncEnabled)
        {
            if (SizeSyncStates.Count > 0)
                SizeSyncStates.Clear();
            return;
        }

        Dictionary<int, VisualColliderPair> selectedPairs = CollectSelectedPairs();
        PruneSizeSyncStates(selectedPairs);

        foreach (KeyValuePair<int, VisualColliderPair> kv in selectedPairs)
        {
            SyncPairIfNeeded(kv.Key, kv.Value);
        }
    }

    private static Dictionary<int, VisualColliderPair> CollectSelectedPairs()
    {
        Dictionary<int, VisualColliderPair> pairs = new Dictionary<int, VisualColliderPair>();

        foreach (GameObject go in Selection.gameObjects)
        {
            if (!TryGetVisualColliderPair(go, out VisualColliderPair pair))
                continue;

            int rootId = pair.root.GetInstanceID();
            pairs[rootId] = pair;
        }

        return pairs;
    }

    private static bool TryGetVisualColliderPair(GameObject go, out VisualColliderPair pair)
    {
        pair = default;
        if (go == null || !go.scene.IsValid())
            return false;

        GameObject root = null;
        GameObject visual = null;

        if (IsVisualNode(go))
        {
            visual = go;
            root = go.transform.parent != null ? go.transform.parent.gameObject : null;
        }
        else if (TryGetVisualChild(go, out GameObject visualChild))
        {
            root = go;
            visual = visualChild;
        }

        if (root == null || visual == null)
            return false;

        BoxCollider2D collider = root.GetComponent<BoxCollider2D>();
        if (collider == null)
            return false;

        pair = new VisualColliderPair
        {
            root = root,
            visual = visual.transform,
            collider = collider,
        };
        return true;
    }

    private static void CaptureCurrentSelectionState()
    {
        SizeSyncStates.Clear();

        if (!IsSizeSyncEnabled || Application.isPlaying)
            return;

        Dictionary<int, VisualColliderPair> selectedPairs = CollectSelectedPairs();
        foreach (KeyValuePair<int, VisualColliderPair> kv in selectedPairs)
        {
            Transform visual = kv.Value.visual;
            BoxCollider2D collider = kv.Value.collider;
            SizeSyncStates[kv.Key] = new SizeSyncState
            {
                baseVisualScale = visual.localScale,
                baseColliderSize = collider.size,
                lastVisualScale = visual.localScale,
                lastColliderSize = collider.size,
            };
        }
    }

    private static void PruneSizeSyncStates(Dictionary<int, VisualColliderPair> selectedPairs)
    {
        if (SizeSyncStates.Count == 0)
            return;

        List<int> staleKeys = SizeSyncStates.Keys.Where(key => !selectedPairs.ContainsKey(key)).ToList();
        foreach (int key in staleKeys)
            SizeSyncStates.Remove(key);
    }

    private static void SyncPairIfNeeded(int rootId, VisualColliderPair pair)
    {
        if (pair.visual == null || pair.collider == null)
            return;

        // [红线保护] 角色碰撞体不参与 Size Sync，避免意外覆盖 PhysicsMetrics 标准值
        if (pair.root != null && IsCharacterRoot(pair.root))
            return;

        Vector3 currentVisualScale = pair.visual.localScale;
        Vector2 currentColliderSize = pair.collider.size;

        if (!SizeSyncStates.TryGetValue(rootId, out SizeSyncState state))
        {
            SizeSyncStates[rootId] = new SizeSyncState
            {
                baseVisualScale = currentVisualScale,
                baseColliderSize = currentColliderSize,
                lastVisualScale = currentVisualScale,
                lastColliderSize = currentColliderSize,
            };
            return;
        }

        bool visualChanged = !ApproximatelyXY(currentVisualScale, state.lastVisualScale);
        bool colliderChanged = !Approximately(currentColliderSize, state.lastColliderSize);

        if (!visualChanged && !colliderChanged)
            return;

        if (visualChanged && !colliderChanged)
        {
            ApplyColliderFromVisual(pair, state, currentVisualScale);
        }
        else if (colliderChanged && !visualChanged)
        {
            ApplyVisualFromCollider(pair, state, currentColliderSize);
        }
        else
        {
            if (Selection.activeGameObject == pair.visual.gameObject)
                ApplyColliderFromVisual(pair, state, currentVisualScale);
            else
                ApplyVisualFromCollider(pair, state, currentColliderSize);
        }

        state.lastVisualScale = pair.visual.localScale;
        state.lastColliderSize = pair.collider.size;
        SizeSyncStates[rootId] = state;
    }

    private static void ApplyColliderFromVisual(VisualColliderPair pair, SizeSyncState state, Vector3 currentVisualScale)
    {
        Vector2 targetSize = new Vector2(
            state.baseColliderSize.x * SafeRatio(currentVisualScale.x, state.baseVisualScale.x),
            state.baseColliderSize.y * SafeRatio(currentVisualScale.y, state.baseVisualScale.y));

        if (Approximately(pair.collider.size, targetSize))
            return;

        Undo.RecordObject(pair.collider, "Sync Collider Size From Visual");
        pair.collider.size = targetSize;
        EditorUtility.SetDirty(pair.collider);
    }

    private static void ApplyVisualFromCollider(VisualColliderPair pair, SizeSyncState state, Vector2 currentColliderSize)
    {
        float scaleX = state.baseVisualScale.x * SafeRatio(currentColliderSize.x, state.baseColliderSize.x);
        float scaleY = state.baseVisualScale.y * SafeRatio(currentColliderSize.y, state.baseColliderSize.y);

        Vector3 targetScale = new Vector3(scaleX, scaleY, pair.visual.localScale.z);
        if (ApproximatelyXY(pair.visual.localScale, targetScale))
            return;

        Undo.RecordObject(pair.visual, "Sync Visual Scale From Collider");
        pair.visual.localScale = targetScale;
        EditorUtility.SetDirty(pair.visual);
    }

    private static float SafeRatio(float current, float baseline)
    {
        if (Mathf.Approximately(baseline, 0f))
            return 1f;

        return Mathf.Abs(current / baseline);
    }

    private static bool TryGetVisualChild(GameObject root, out GameObject visual)
    {
        visual = null;
        if (root == null || !root.scene.IsValid())
            return false;

        Transform visualTransform = root.transform.Find("Visual");
        if (visualTransform == null)
            return false;

        if (visualTransform.parent != root.transform)
            return false;

        if (visualTransform.GetComponent<SpriteRenderer>() == null)
            return false;

        if (root.GetComponent<Collider2D>() == null)
            return false;

        visual = visualTransform.gameObject;
        return true;
    }

    private static bool IsVisualNode(GameObject go)
    {
        if (go == null || !go.scene.IsValid())
            return false;

        if (go.name != "Visual")
            return false;

        if (go.GetComponent<SpriteRenderer>() == null)
            return false;

        Transform parent = go.transform.parent;
        if (parent == null)
            return false;

        Collider2D rootCollider = parent.GetComponent<Collider2D>();
        if (rootCollider == null)
            return false;

        return parent.Find("Visual") == go.transform;
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    private static bool ApproximatelyXY(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.y - b.y) < 0.0001f;
    }

    /// <summary>
    /// [红线保护] 检测目标是否是角色 Root（MarioController/TricksterController/PlayerController）。
    /// 角色 Root 的碰撞体由 PhysicsMetrics 定义，不应被 Size Sync 修改。
    /// </summary>
    private static bool IsCharacterRoot(GameObject go)
    {
        if (go == null) return false;
        foreach (var comp in go.GetComponents<MonoBehaviour>())
        {
            if (comp == null) continue;
            string typeName = comp.GetType().Name;
            if (typeName.Contains("MarioController") ||
                typeName.Contains("TricksterController") ||
                typeName.Contains("PlayerController"))
            {
                return true;
            }
        }
        return false;
    }
}
