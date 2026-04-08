using UnityEngine;
/// <summary>
/// S57: 运行时内存监控 & 低内存防护
///
/// 解决问题：
///   Unity 在系统内存紧张时（如同时运行其他大型程序）会因无法分配内存而崩溃。
///   报错: "Could not allocate memory: System out of memory!"
///   本脚本通过 Application.lowMemory 回调 + 周期性 GC 主动释放内存，
///   降低崩溃概率。
///
/// 功能：
///   1. 注册 Application.lowMemory 回调，系统内存紧张时自动清理
///   2. 周期性调用 Resources.UnloadUnusedAssets() 释放未引用资源
///   3. 低内存时自动降低 QualitySettings 减少 GPU 内存占用
///   4. 在 OnGUI 中显示低内存警告（可选）
///
/// 使用方式：
///   挂载到场景中任意常驻 GameObject 上（推荐挂到 GameManager 同物体）。
///   或通过 [RuntimeInitializeOnLoadMethod] 自动创建。
///
/// 设计原则：
///   - 不修改任何已有脚本的核心逻辑
///   - 仅在系统发出低内存信号时介入
///   - 2D 游戏不需要高质量 3D 渲染设置，主动降级无视觉损失
///
/// 业界参考：
///   - Unity 官方文档: Application.lowMemory
///   - Unity Best Practices: "Call Resources.UnloadUnusedAssets periodically"
///   - Mobile 优化指南: "Register lowMemory callback to release caches"
/// </summary>
public class MemoryGuard : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 配置
    // ═══════════════════════════════════════════════════
    [Header("=== 内存监控配置 ===")]
    [Tooltip("周期性 GC 间隔（秒），0 = 禁用周期性 GC")]
    [SerializeField] private float gcInterval = 120f;

    [Tooltip("是否在低内存时自动降低画质")]
    [SerializeField] private bool autoDowngradeQuality = true;

    [Tooltip("是否显示低内存警告 HUD")]
    [SerializeField] private bool showWarningHUD = true;

    [Tooltip("警告显示持续时间（秒）")]
    [SerializeField] private float warningDuration = 5f;

    // ═══════════════════════════════════════════════════
    // 运行时状态
    // ═══════════════════════════════════════════════════
    private float gcTimer;
    private float warningTimer;
    private int lowMemoryCount;
    private bool isLowMemory;

    // GUIStyle 缓存（P1 合规：不在 OnGUI 中 new）
    private GUIStyle cachedWarningStyle;
    private bool stylesInitialized;

    // 单例（可选，防止重复创建）
    private static MemoryGuard _instance;

    // ═══════════════════════════════════════════════════
    // 自动创建（无需手动挂载）
    // ═══════════════════════════════════════════════════
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;

        // 检查场景中是否已有 MemoryGuard
        _instance = FindObjectOfType<MemoryGuard>();
        if (_instance != null) return;

        GameObject go = new GameObject("[MemoryGuard]");
        _instance = go.AddComponent<MemoryGuard>();
        DontDestroyOnLoad(go);
        Debug.Log("[MemoryGuard] 自动创建内存监控实例。");
    }

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════
    private void Awake()
    {
        // 单例保护
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 注册低内存回调
        Application.lowMemory += OnLowMemory;

        Debug.Log("[MemoryGuard] 内存监控已启动。");
    }

    private void OnDestroy()
    {
        Application.lowMemory -= OnLowMemory;
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // 周期性 GC
        if (gcInterval > 0)
        {
            gcTimer += Time.unscaledDeltaTime;
            if (gcTimer >= gcInterval)
            {
                gcTimer = 0f;
                PerformPeriodicCleanup();
            }
        }

        // 警告倒计时
        if (warningTimer > 0f)
        {
            warningTimer -= Time.unscaledDeltaTime;
            if (warningTimer <= 0f)
            {
                isLowMemory = false;
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // 低内存回调
    // ═══════════════════════════════════════════════════
    /// <summary>
    /// 系统发出低内存警告时的回调。
    /// Unity 在检测到系统内存压力时自动调用。
    /// </summary>
    private void OnLowMemory()
    {
        lowMemoryCount++;
        isLowMemory = true;
        warningTimer = warningDuration;

        Debug.LogWarning($"[MemoryGuard] ⚠️ 系统低内存警告！(第 {lowMemoryCount} 次) 正在紧急释放资源...");

        // 1. 强制 GC
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        // 2. 释放未使用的 Unity 资源
        Resources.UnloadUnusedAssets();

        // 3. 自动降低画质
        if (autoDowngradeQuality)
        {
            DowngradeQuality();
        }

        // 4. 清理 AsciiLevelGenerator 的静态缓存（如果有）
        CleanStaticCaches();

        Debug.LogWarning("[MemoryGuard] 紧急内存释放完成。");
    }

    // ═══════════════════════════════════════════════════
    // 周期性清理
    // ═══════════════════════════════════════════════════
    private void PerformPeriodicCleanup()
    {
        // 轻量级清理：只释放未使用资源，不强制 GC（避免卡顿）
        Resources.UnloadUnusedAssets();
    }

    // ═══════════════════════════════════════════════════
    // 画质降级
    // ═══════════════════════════════════════════════════
    private void DowngradeQuality()
    {
        int current = QualitySettings.GetQualityLevel();
        if (current > 0)
        {
            int newLevel = Mathf.Max(0, current - 1);
            QualitySettings.SetQualityLevel(newLevel, true);
            Debug.LogWarning($"[MemoryGuard] 画质已从 {current} 降至 {newLevel} 以释放 GPU 内存。");
        }
    }

    // ═══════════════════════════════════════════════════
    // 静态缓存清理
    // ═══════════════════════════════════════════════════
    /// <summary>
    /// 清理已知的静态缓存。
    /// 通过反射或公共 API 清理 AsciiLevelGenerator 等的静态 Sprite 缓存。
    /// </summary>
    private void CleanStaticCaches()
    {
        // AsciiLevelGenerator.cachedWhiteSprite 是 private static，
        // 在低内存时不强制清理（它只有 4x4 = 64 bytes，影响极小）。
        // 如果未来有大型静态缓存，在此处添加清理逻辑。
    }

    // ═══════════════════════════════════════════════════
    // 警告 HUD
    // ═══════════════════════════════════════════════════
    private void OnGUI()
    {
        if (!showWarningHUD || !isLowMemory) return;

        InitStylesIfNeeded();

        float alpha = Mathf.Clamp01(warningTimer / 2f); // 最后 2 秒渐隐
        cachedWarningStyle.normal.textColor = new Color(1f, 0.3f, 0.1f, alpha);

        GUI.Label(new Rect(Screen.width / 2 - 200, 60, 400, 30),
            $"⚠ LOW MEMORY - Quality reduced (x{lowMemoryCount})", cachedWarningStyle);
    }

    /// <summary>P1 合规：惰性初始化 GUIStyle</summary>
    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        cachedWarningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        stylesInitialized = true;
    }

    // ═══════════════════════════════════════════════════
    // 公共 API
    // ═══════════════════════════════════════════════════

    /// <summary>手动触发内存清理（供调试或其他脚本调用）</summary>
    public static void ForceCleanup()
    {
        if (_instance != null)
        {
            _instance.OnLowMemory();
        }
        else
        {
            // 即使没有实例也能执行基本清理
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
    }

    /// <summary>低内存事件触发次数</summary>
    public static int LowMemoryEventCount => _instance != null ? _instance.lowMemoryCount : 0;
}
