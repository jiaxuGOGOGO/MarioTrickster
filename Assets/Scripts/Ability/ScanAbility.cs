using UnityEngine;

/// <summary>
/// Mario 扫描技能
/// 
/// 设计思路：
///   Mario 可以使用扫描技能来揭示一定范围内伪装的 Trickster。
///   扫描有冷却时间和持续时间，使用后 Trickster 的伪装会被短暂标记。
///   这为 Mario 提供了主动反制手段，增加了双方的博弈深度。
///
/// 扫描效果：
///   - 以 Mario 为圆心，scanRadius 为半径进行扫描
///   - 如果 Trickster 在范围内且处于伪装状态：
///     a) Trickster 的 Sprite 闪烁红色（暴露效果）
///     b) 在 Trickster 头顶显示警告标记
///   - 如果 Trickster 不在范围内或未伪装：无效果但仍消耗冷却
///
/// 扫描脉冲视觉效果：
///   - 按下扫描键后，以 Mario 为中心扩散一个半透明圆环
///   - 圆环颜色：扫描到 Trickster 时红色，否则蓝色
///
/// Session 11 修复：
///   B015 - 扫描成功时屏幕下方错误提示"未在范围内"
///     根因: StartPulse() 在 CheckForTrickster() 之前调用，
///           导致首帧脉冲颜色使用上次的 tricksterFound 值。
///     修复: 先执行检测再启动脉冲，确保颜色正确。
///   新增: OnScanPerformed 事件（供 UI 区分扫描反馈和道具操控反馈）
///
/// Session 18 性能优化：
///   - 所有 GUIStyle 缓存为类字段，消除 OnGUI 每帧 new GUIStyle 的 GC 分配
///   - 惰性初始化（GUI.skin 只在 OnGUI 内有效）
///
/// 使用方式：挂载在 Mario GameObject 上
/// </summary>
public class ScanAbility : MonoBehaviour
{
    [Header("=== 扫描参数 ===")]
    [Tooltip("扫描半径")]
    [SerializeField] private float scanRadius = 5f;
    [Tooltip("扫描冷却时间（秒）")]
    [SerializeField] private float scanCooldown = 8f;
    [Tooltip("扫描暴露持续时间（秒）")]
    [SerializeField] private float revealDuration = 2f;

    [Header("=== 扫描脉冲效果 ===")]
    [Tooltip("脉冲扩散速度")]
    [SerializeField] private float pulseSpeed = 15f;
    [Tooltip("脉冲线条宽度")]
    [SerializeField] private float pulseLineWidth = 0.15f;

    [Header("=== 暴露效果 ===")]
    [Tooltip("暴露时闪烁频率")]
    [SerializeField] private float flashFrequency = 6f;
    [Tooltip("暴露标记颜色")]
    [SerializeField] private Color revealColor = new Color(1f, 0.2f, 0.2f, 0.8f);

    // 状态
    private float cooldownTimer;
    private float revealTimer;
    private bool isRevealing;
    private bool tricksterFound;

    // 脉冲效果状态
    private bool isPulsing;
    private float pulseRadius;
    private float pulseAlpha;

    // 扫描结果文字提示
    private string scanResultText = "";
    private float scanResultTimer;
    private const float ScanResultDisplayDuration = 1.5f;

    // 引用
    private TricksterController targetTrickster;
    private DisguiseSystem targetDisguise;
    private SpriteRenderer targetSpriteRenderer;

    // ── Session 18 性能优化：缓存 GUIStyle ──
    private bool stylesInitialized;
    private GUIStyle cachedReadyStyle;
    private GUIStyle cachedMarkerStyle;
    private GUIStyle cachedResultStyle;

    // 公共属性
    public float CooldownRemaining => cooldownTimer;
    public float CooldownProgress => scanCooldown > 0 ? 1f - (cooldownTimer / scanCooldown) : 1f;
    public bool IsReady => cooldownTimer <= 0f;
    public bool IsRevealing => isRevealing;
    public float ScanRadius => scanRadius;

    // 事件
    public System.Action OnScanActivated;

    /// <summary>调试用：重置冷却时间（由冷却取消开关调用）</summary>
    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }
    public System.Action<bool> OnScanResult; // true = 发现 Trickster
    /// <summary>扫描执行事件（供测试验证）</summary>
    public System.Action OnScanPerformed;

    private void Start()
    {
        // 查找 Trickster（场景中应该只有一个）
        targetTrickster = FindObjectOfType<TricksterController>();
        if (targetTrickster != null)
        {
            targetDisguise = targetTrickster.GetComponent<DisguiseSystem>();
            // S37: 视碰分离 — SpriteRenderer 可能在子物体 Visual 上
            targetSpriteRenderer = targetTrickster.GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        // 冷却计时
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // 暴露效果持续
        if (isRevealing)
        {
            revealTimer -= Time.deltaTime;
            if (revealTimer <= 0f)
            {
                EndReveal();
            }
            else
            {
                UpdateRevealEffect();
            }
        }

        // 脉冲效果更新
        if (isPulsing)
        {
            UpdatePulse();
        }

        // 扫描结果文字倒计时
        if (scanResultTimer > 0f)
        {
            scanResultTimer -= Time.deltaTime;
        }
    }

    #region 扫描触发

    /// <summary>
    /// 执行扫描（由 InputManager 通过 MarioController 调用）
    /// </summary>
    public void ActivateScan()
    {
        if (cooldownTimer > 0f) return;

        // 开始冷却
        cooldownTimer = scanCooldown;

        OnScanActivated?.Invoke();

        // 【B015 修复】先检测再启动脉冲，确保脉冲颜色正确
        tricksterFound = CheckForTrickster();

        // 启动脉冲效果（此时 tricksterFound 已经是正确值）
        StartPulse();

        if (tricksterFound)
        {
            StartReveal();
            scanResultText = "Trickster Detected!";
            Debug.Log("[ScanAbility] Trickster detected! Revealing...");
        }
        else
        {
            // 区分未伪装和不在范围内的情况，给出更精确的提示
            scanResultText = GetScanMissReason();
            Debug.Log($"[ScanAbility] Scan complete - {scanResultText}");
        }

        scanResultTimer = ScanResultDisplayDuration;

        OnScanResult?.Invoke(tricksterFound);
        OnScanPerformed?.Invoke();
    }

    /// <summary>检测 Trickster 是否在扫描范围内且处于伪装状态</summary>
    private bool CheckForTrickster()
    {
        if (targetTrickster == null || targetDisguise == null) return false;
        if (!targetDisguise.IsDisguised) return false;

        float distance = Vector2.Distance(transform.position, targetTrickster.transform.position);
        return distance <= scanRadius;
    }

    /// <summary>获取扫描未命中的具体原因（用于UI提示）</summary>
    private string GetScanMissReason()
    {
        if (targetTrickster == null) return "No target found";
        if (targetDisguise == null) return "No disguise system";
        if (!targetDisguise.IsDisguised) return "Trickster not disguised";

        float distance = Vector2.Distance(transform.position, targetTrickster.transform.position);
        if (distance > scanRadius) return $"Out of range ({distance:F1}m)";

        return "Scan clear";
    }

    #endregion

    #region 暴露效果

    private void StartReveal()
    {
        isRevealing = true;
        revealTimer = revealDuration;
    }

    private void UpdateRevealEffect()
    {
        if (targetSpriteRenderer == null) return;

        // 闪烁效果：在原色和红色之间切换
        float flash = Mathf.Sin(Time.time * flashFrequency * Mathf.PI * 2f);
        if (flash > 0)
        {
            targetSpriteRenderer.color = revealColor;
        }
        else
        {
            targetSpriteRenderer.color = Color.white;
        }
    }

    private void EndReveal()
    {
        isRevealing = false;
        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.color = Color.white;
        }
    }

    #endregion

    #region 脉冲效果

    private void StartPulse()
    {
        isPulsing = true;
        pulseRadius = 0f;
        pulseAlpha = 1f;
    }

    private void UpdatePulse()
    {
        pulseRadius += pulseSpeed * Time.deltaTime;
        pulseAlpha = 1f - (pulseRadius / (scanRadius * 1.2f));

        if (pulseAlpha <= 0f)
        {
            isPulsing = false;
        }
    }

    #endregion

    #region 调试与可视化

    private void OnDrawGizmosSelected()
    {
        // 显示扫描范围
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }

    /// <summary>Session 18: 惰性初始化缓存样式</summary>
    private void InitStylesIfNeeded()
    {
        if (stylesInitialized) return;
        stylesInitialized = true;

        cachedReadyStyle = new GUIStyle(GUI.skin.label);
        cachedReadyStyle.fontSize = 10;
        cachedReadyStyle.alignment = TextAnchor.MiddleCenter;
        cachedReadyStyle.normal.textColor = new Color(0.3f, 0.8f, 1f, 0.7f);

        cachedMarkerStyle = new GUIStyle(GUI.skin.label);
        cachedMarkerStyle.fontSize = 22;
        cachedMarkerStyle.fontStyle = FontStyle.Bold;
        cachedMarkerStyle.alignment = TextAnchor.MiddleCenter;

        cachedResultStyle = new GUIStyle(GUI.skin.label);
        cachedResultStyle.fontSize = 14;
        cachedResultStyle.fontStyle = FontStyle.Bold;
        cachedResultStyle.alignment = TextAnchor.MiddleCenter;
    }

    private void OnGUI()
    {
        if (Camera.main == null) return;

        // Session 18: 惰性初始化缓存样式
        InitStylesIfNeeded();

        // 扫描冷却 UI（Mario 头顶）
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
        if (screenPos.z < 0) return;

        float barWidth = 50f;
        float barHeight = 6f;
        float x = screenPos.x - barWidth / 2f;
        float y = Screen.height - screenPos.y;

        // 冷却条（仅在冷却中显示）
        if (cooldownTimer > 0f)
        {
            // 背景
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(x - 1, y - 1, barWidth + 2, barHeight + 2), Texture2D.whiteTexture);

            // 冷却进度
            GUI.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(x, y, barWidth * CooldownProgress, barHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            // 就绪提示（使用缓存样式）
            GUI.Label(new Rect(x - 10, y - 2, barWidth + 20, 14), "[Q] Scan", cachedReadyStyle);
        }

        // 脉冲效果绘制
        if (isPulsing)
        {
            DrawPulseEffect();
        }

        // 暴露标记（Trickster 头顶的警告）
        if (isRevealing && targetTrickster != null)
        {
            DrawRevealMarker();
        }

        // 扫描结果文字提示（在 Mario 下方显示，与道具操控失败提示区分）
        if (scanResultTimer > 0f)
        {
            DrawScanResultText();
        }
    }

    /// <summary>绘制扫描脉冲圆环效果</summary>
    private void DrawPulseEffect()
    {
        if (Camera.main == null) return;

        Vector3 center = Camera.main.WorldToScreenPoint(transform.position);
        if (center.z < 0) return;

        // 将世界坐标的脉冲半径转换为屏幕像素
        Vector3 edgeWorld = transform.position + Vector3.right * pulseRadius;
        Vector3 edgeScreen = Camera.main.WorldToScreenPoint(edgeWorld);
        float screenRadius = Mathf.Abs(edgeScreen.x - center.x);

        Color pulseColor = tricksterFound
            ? new Color(1f, 0.2f, 0.2f, pulseAlpha * 0.4f)
            : new Color(0.2f, 0.5f, 1f, pulseAlpha * 0.3f);

        // 用多个矩形近似绘制圆环（OnGUI 的限制）
        // S57: 从 36 降到 24 段，减少 OnGUI 中 GUI.DrawTexture 调用次数，降低内存压力
        int segments = 24;
        float angleStep = 360f / segments;
        float lineWidthScreen = pulseLineWidth * (edgeScreen.x - center.x) / Mathf.Max(pulseRadius, 0.01f);
        lineWidthScreen = Mathf.Max(2f, lineWidthScreen);

        GUI.color = pulseColor;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float px = center.x + Mathf.Cos(angle) * screenRadius;
            float py = (Screen.height - center.y) + Mathf.Sin(angle) * screenRadius;
            GUI.DrawTexture(new Rect(px - lineWidthScreen / 2, py - lineWidthScreen / 2, lineWidthScreen, lineWidthScreen), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    /// <summary>在被暴露的 Trickster 头顶绘制警告标记</summary>
    private void DrawRevealMarker()
    {
        if (Camera.main == null || targetTrickster == null) return;

        Vector3 markerPos = Camera.main.WorldToScreenPoint(targetTrickster.transform.position + Vector3.up * 2f);
        if (markerPos.z < 0) return;

        float blinkAlpha = Mathf.Abs(Mathf.Sin(Time.time * flashFrequency * Mathf.PI));

        // Session 18: 使用缓存样式，只更新动态颜色
        cachedMarkerStyle.normal.textColor = new Color(1f, 0.1f, 0.1f, blinkAlpha);

        float mx = markerPos.x;
        float my = Screen.height - markerPos.y;
        GUI.Label(new Rect(mx - 20, my - 15, 40, 30), "!", cachedMarkerStyle);
    }

    /// <summary>绘制扫描结果文字提示（Mario 附近，与道具操控提示区分位置）</summary>
    private void DrawScanResultText()
    {
        if (Camera.main == null) return;

        Vector3 textPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.down * 1.5f);
        if (textPos.z < 0) return;

        float alpha = Mathf.Clamp01(scanResultTimer / 0.5f);

        // Session 18: 使用缓存样式，只更新动态颜色
        if (tricksterFound)
        {
            cachedResultStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, alpha);
        }
        else
        {
            cachedResultStyle.normal.textColor = new Color(0.5f, 0.7f, 1f, alpha);
        }

        float tx = textPos.x;
        float ty = Screen.height - textPos.y;
        GUI.Label(new Rect(tx - 100, ty, 200, 25), scanResultText, cachedResultStyle);
    }

    #endregion
}
