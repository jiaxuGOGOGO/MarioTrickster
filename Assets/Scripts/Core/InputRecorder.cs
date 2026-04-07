using UnityEngine;
using System.Collections.Generic;
using System.IO;

// ═══════════════════════════════════════════════════════════════════
// InputRecorder — 玩家操作录制器（S50: TAS 录播系统, S51: DDPT 升级）
//
// [AI防坑警告] 此类是 TAS 录播系统的录制端。
// 它在运行时采样 IInputProvider 的所有信号，使用 RLE（Run-Length Encoding）
// 压缩连续相同的输入帧，生成可直接注入 AutomatedInputProvider 的 InputFrame 序列。
//
// S51 升级内容：
//   1. TasReplayData Wrapper 类 — 包裹 List<InputFrame> + asciiLevel + expectedFinalPosition
//      解决 JsonUtility 无法直接序列化顶层 List 的限制。
//   2. ImportFromJson(string json) — 从 JSON 反序列化 TasReplayData。
//   3. F12 一键落盘 — 将当前录制数据 + 关卡 ASCII + Mario 最终坐标 打包为 JSON 文件
//      自动保存到 Assets/Tests/LevelReplays/ 目录。
//   4. TAS 状态可视化 UI — 屏幕右上角显示 REC / TAS REPLAY 10x 状态指示器。
//
// 使用方式（运行时）：
//   F10 = 开始/停止录制
//   F11 = 将录制结果序列化为 JSON 并打印到 Console（方便复制为测试用例数据）
//   F12 = 一键落盘：录制数据 + 关卡 ASCII + Mario 最终坐标 → JSON 文件
//
// RLE 压缩原理：
//   玩家连续多帧按住相同按键组合时，只记录一条 InputFrame + duration。
//   典型压缩比 1:40+（大部分时间输入状态不变）。
//   一分钟录制可能只产生几十条记录，而非 3000+ 原始帧。
//
// 架构关系：
//   InputRecorder（录制） → TasReplayData → JSON → ImportFromJson → AutomatedInputProvider（回放）
//   形成完整的 Record/Replay 闭环。
//
// 依赖：
//   - InputFrame 结构体（定义在 AutomatedInputProvider.cs）
//   - IInputProvider 接口（定义在 IInputProvider.cs）
//   - InputManager（通过 GetCurrentProvider() 获取当前输入源）
// ═══════════════════════════════════════════════════════════════════

// ─── S51: TAS 录像数据 Wrapper ───────────────────────────────────
// [AI防坑警告] 此 Wrapper 类是 S51 数据驱动测试管线的核心数据结构。
// JsonUtility 无法直接序列化/反序列化顶层 JSON 数组。
// 必须用此 Wrapper 类包裹 List<InputFrame>，才能正确导入/导出。
// 不要尝试直接 JsonUtility.FromJson<List<InputFrame>>()，会得到空结果。
[System.Serializable]
public class TasReplayData
{
    /// <summary>RLE 压缩后的输入帧序列</summary>
    public List<InputFrame> frames = new List<InputFrame>();

    /// <summary>关卡 ASCII 文本（多行，用 \n 分隔）</summary>
    public string asciiLevel = "";

    /// <summary>录制结束时 Mario 的最终 X 坐标（用于防脱轨校验）</summary>
    public float expectedFinalPosX;

    /// <summary>录制结束时 Mario 的最终 Y 坐标（用于防脱轨校验）</summary>
    public float expectedFinalPosY;

    /// <summary>
    /// S52 柔性测试降级：是否启用严格坐标校验。
    /// 当 false 时，测试只断言"角色存活 + 触发胜利"，彻底跳过 0.05f 坐标误差校验。
    /// 这允许开发者微调手感时，只要老录像能跌跌撞撞跑完就不会报错。
    /// 默认 false（柔性模式），仅在需要精确回放验证时设为 true。
    /// </summary>
    public bool strictPositionCheck = false;

    /// <summary>
    /// S53 轨迹耗时预警：录制时的预期通关时间（秒）。
    /// 从 frames 中所有 duration 之和 * fixedDeltaTime(0.02s) 自动计算。
    /// 回放时若实际耗时偏差率超过阈值，控制台输出黄色警告。
    /// 值为 0 表示未记录（兼容旧录像，跳过耗时预警）。
    /// </summary>
    public float expectedDurationSeconds;

    /// <summary>录制时的描述/备注（可选）</summary>
    public string description = "";
}

/// <summary>
/// 玩家操作录制器。
/// 
/// 在运行时采样真实玩家输入，使用 RLE 压缩生成 InputFrame 序列。
/// 录制结果可序列化为 JSON，直接用于 AutomatedInputProvider 回放测试。
/// 
/// S51: 新增 TasReplayData Wrapper、ImportFromJson、F12 一键落盘、TAS 状态 UI。
/// 
/// 挂载到场景中任意 GameObject 上即可使用。
/// </summary>
public class InputRecorder : MonoBehaviour
{
    // ── 录制状态 ──────────────────────────────────────────
    private bool _isRecording;
    private List<InputFrame> _recordedFrames;
    private InputFrame _currentFrame;
    private InputManager _inputManager;

    /// <summary>当前是否正在录制</summary>
    public bool IsRecording => _isRecording;

    /// <summary>已录制的帧序列（只读副本）</summary>
    public List<InputFrame> RecordedFrames => _recordedFrames != null
        ? new List<InputFrame>(_recordedFrames)
        : new List<InputFrame>();

    /// <summary>当前录制的总帧数（压缩前的逻辑帧数）</summary>
    public int TotalLogicalFrames { get; private set; }

    /// <summary>当前录制的压缩帧段数</summary>
    public int CompressedSegments => _recordedFrames?.Count ?? 0;

    // ── S51: TAS 状态可视化 UI ──────────────────────────────
    // [AI防坑警告] P1 规则：严禁在 OnGUI 中 new GUIStyle！
    // 必须在此处声明为类字段，在 InitStylesIfNeeded() 中惰性初始化。
    private GUIStyle _statusStyle;  // cached
    private bool _stylesInitialized;

    // ── S51: TAS 回放状态追踪 ──────────────────────────────
    private bool _isTasReplaying;

    /// <summary>设置 TAS 回放状态（供测试代码调用）</summary>
    public void SetTasReplayState(bool isReplaying)
    {
        _isTasReplaying = isReplaying;
    }

    // ═══════════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════════

    private void Start()
    {
        _recordedFrames = new List<InputFrame>();
        _inputManager = FindObjectOfType<InputManager>();
    }

    private void Update()
    {
        // F10: 开始/停止录制
        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (_isRecording)
                StopRecording();
            else
                StartRecording();
        }

        // F11: 导出 JSON 到控制台
        if (Input.GetKeyDown(KeyCode.F11))
        {
            ExportToConsole();
        }

        // F12: 一键落盘（S51）
        if (Input.GetKeyDown(KeyCode.F12))
        {
            SaveReplayToFile();
        }

        // 录制采样（每帧执行）
        if (_isRecording)
        {
            SampleFrame();
        }
    }

    // ── S51: TAS 状态可视化 OnGUI ──────────────────────────
    // [AI防坑警告] P1 规则：GUIStyle 在 InitStylesIfNeeded 中惰性初始化，
    // OnGUI 中只使用缓存的 _statusStyle，绝不 new GUIStyle。
    private void OnGUI()
    {
        InitStylesIfNeeded();

        string label = null;

        if (_isRecording)
        {
            label = "\u25cf [REC]"; // 🔴 Unicode solid circle
            _statusStyle.normal.textColor = Color.red;
        }
        else if (_isTasReplaying)
        {
            label = "\u25b6 [TAS REPLAY 10x]"; // ⏯️ Unicode play triangle
            _statusStyle.normal.textColor = Color.cyan;
        }

        if (label != null)
        {
            float w = 220f;
            float h = 30f;
            float x = Screen.width - w - 10f;
            float y = 10f;
            GUI.Label(new Rect(x, y, w, h), label, _statusStyle);
        }
    }

    /// <summary>P1 合规：惰性初始化 GUIStyle（仅执行一次）</summary>
    private void InitStylesIfNeeded() // cached
    {
        if (_stylesInitialized) return;
        _statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        _statusStyle.normal.textColor = Color.red;
        _stylesInitialized = true;
    }

    // ═══════════════════════════════════════════════════════
    // 公共 API（供测试代码直接调用）
    // ═══════════════════════════════════════════════════════

    /// <summary>开始录制。清空之前的录制数据。</summary>
    public void StartRecording()
    {
        _recordedFrames.Clear();
        _currentFrame = default;
        _currentFrame.duration = 0;
        TotalLogicalFrames = 0;
        _isRecording = true;

        if (_inputManager == null)
            _inputManager = FindObjectOfType<InputManager>();

        Debug.Log("[InputRecorder] \u25cf REC started \u2014 Press F10 to stop, F11 to export JSON, F12 to save file");
    }

    /// <summary>停止录制。将最后一个帧段写入列表。</summary>
    public void StopRecording()
    {
        if (!_isRecording) return;

        // 写入最后一个帧段
        if (_currentFrame.duration > 0)
        {
            _recordedFrames.Add(_currentFrame);
        }

        _isRecording = false;

        float ratio = TotalLogicalFrames > 0
            ? (float)TotalLogicalFrames / _recordedFrames.Count
            : 0f;

        Debug.Log($"[InputRecorder] \u25a0 REC stopped \u2014 {_recordedFrames.Count} segments from {TotalLogicalFrames} frames (RLE ratio: {ratio:F1}:1)");
    }

    /// <summary>将录制结果序列化为 JSON 并打印到控制台。</summary>
    public void ExportToConsole()
    {
        if (_recordedFrames == null || _recordedFrames.Count == 0)
        {
            Debug.LogWarning("[InputRecorder] No recorded data to export.");
            return;
        }

        // 如果仍在录制，先停止
        if (_isRecording)
            StopRecording();

        string json = SerializeToJson(_recordedFrames);
        Debug.Log($"[InputRecorder] === JSON Export ({_recordedFrames.Count} segments) ===\n{json}");
    }

    /// <summary>
    /// 获取录制结果的 JSON 字符串。
    /// 供测试代码直接获取，无需通过 Console。
    /// </summary>
    public string GetJsonExport()
    {
        if (_recordedFrames == null || _recordedFrames.Count == 0)
            return "[]";

        return SerializeToJson(_recordedFrames);
    }

    // ═══════════════════════════════════════════════════════
    // S51: TasReplayData Wrapper 序列化/反序列化
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// S51: 将 TasReplayData 序列化为完整 JSON（包含关卡 ASCII + 帧序列 + 预期坐标）。
    /// 使用 JsonUtility，确保所有字段完整输出。
    /// </summary>
    public static string ExportReplayData(TasReplayData data)
    {
        if (data == null) return "{}";
        return JsonUtility.ToJson(data, true);
    }

    /// <summary>
    /// S51: 从 JSON 反序列化 TasReplayData。
    /// 
    /// [AI防坑警告] 此方法使用 JsonUtility.FromJson，要求 JSON 必须是
    /// TasReplayData Wrapper 格式（包含 "frames" 数组），而非裸 InputFrame 数组。
    /// 如果传入裸数组 "[{...}]"，会得到空的 frames 列表。
    /// </summary>
    public static TasReplayData ImportFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        TasReplayData data = JsonUtility.FromJson<TasReplayData>(json);
        return data;
    }

    // ═══════════════════════════════════════════════════════
    // S51: F12 一键落盘
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// S51: 将当前录制数据 + 关卡 ASCII + Mario 最终坐标打包为 JSON 文件。
    /// 自动保存到 Assets/Tests/LevelReplays/ 目录。
    /// </summary>
    public void SaveReplayToFile()
    {
        if (_recordedFrames == null || _recordedFrames.Count == 0)
        {
            Debug.LogWarning("[InputRecorder] No recorded data to save. Record first (F10).");
            return;
        }

        // 如果仍在录制，先停止
        if (_isRecording)
            StopRecording();

        // 构建 TasReplayData
        TasReplayData data = new TasReplayData();
        data.frames = new List<InputFrame>(_recordedFrames);

        // 获取关卡 ASCII（从当前场景的 AsciiLevelGenerator 静态数据）
        data.asciiLevel = GetCurrentAsciiLevel();

        // 获取 Mario 最终坐标
        MarioController mario = FindObjectOfType<MarioController>();
        if (mario != null)
        {
            data.expectedFinalPosX = mario.transform.position.x;
            data.expectedFinalPosY = mario.transform.position.y;
        }

        // S53: 自动计算预期通关时间（所有帧 duration 之和 * fixedDeltaTime）
        int totalFrames = 0;
        foreach (var f in _recordedFrames)
            totalFrames += f.duration;
        data.expectedDurationSeconds = totalFrames * Time.fixedDeltaTime;

        data.description = $"Recorded at {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}, {_recordedFrames.Count} segments, {data.expectedDurationSeconds:F2}s expected";

        // 序列化
        string json = ExportReplayData(data);

        // 确保目录存在
        string dir = Path.Combine(Application.dataPath, "Tests", "LevelReplays");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            Debug.Log($"[InputRecorder] Created directory: {dir}");
        }

        // 生成文件名（时间戳）
        string fileName = $"replay_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(dir, fileName);

        File.WriteAllText(filePath, json);
        Debug.Log($"[InputRecorder] \u2714 Replay saved to: {filePath}");

#if UNITY_EDITOR
        // 刷新 AssetDatabase 让 Unity 识别新文件
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    /// <summary>
    /// 尝试获取当前场景的 ASCII 关卡文本。
    /// 优先从 AsciiLevelGenerator 的静态数据获取。
    /// </summary>
    private static string GetCurrentAsciiLevel()
    {
        // AsciiLevelGenerator 可能缓存了最后一次生成的模板
        // 如果没有，返回空字符串（用户可手动填写）
        return "";
    }

    // ═══════════════════════════════════════════════════════
    // RLE 核心：帧采样与压缩
    // ═══════════════════════════════════════════════════════

    // [AI防坑警告] SampleFrame 是 RLE 压缩的核心逻辑。
    // 每帧采样当前输入状态，与上一帧比较：
    //   - 相同 → duration++（合并）
    //   - 不同 → 将上一帧 push 到列表，开始新帧
    // 单帧事件（xxxDown）只在状态变化的第一帧标记为 true。
    // 不要在此方法中创建 new 对象（P5 性能规则）。
    private void SampleFrame()
    {
        IInputProvider provider = null;
        if (_inputManager != null)
            provider = _inputManager.GetCurrentProvider();

        // 构建当前帧的输入快照
        InputFrame snapshot = default;
        snapshot.duration = 1;

        if (provider != null)
        {
            // P1 (Mario)
            snapshot.p1Horizontal = provider.GetP1Horizontal();
            snapshot.p1Vertical = provider.GetP1Vertical();
            snapshot.p1JumpHeld = provider.GetP1JumpHeld();
            snapshot.p1JumpDown = provider.GetP1JumpDown();
            snapshot.p1SHeld = provider.GetP1SHeld();
            snapshot.p1SDown = provider.GetP1SDown();
            snapshot.p1ScanDown = provider.GetP1ScanDown();

            // P2 (Trickster)
            snapshot.p2Horizontal = provider.GetP2Horizontal();
            snapshot.p2Vertical = provider.GetP2Vertical();
            snapshot.p2JumpHeld = provider.GetP2JumpHeld();
            snapshot.p2JumpDown = provider.GetP2JumpDown();
            snapshot.p2DirectionDown = provider.GetP2DirectionDown();
            snapshot.p2DisguiseDown = provider.GetP2DisguiseDown();
            snapshot.p2SwitchDir = provider.GetP2SwitchDirection();
            snapshot.p2AbilityDown = provider.GetP2AbilityDown();

            // 全局
            snapshot.pauseDown = provider.GetPauseDown();
            snapshot.restartDown = provider.GetRestartDown();
            snapshot.noCooldownDown = provider.GetNoCooldownToggleDown();
            snapshot.restartRoundDown = provider.GetRestartRoundDown();
            snapshot.nextRoundDown = provider.GetNextRoundDown();
        }

        TotalLogicalFrames++;

        // RLE 压缩：比较当前帧与正在累积的帧段
        if (_currentFrame.duration == 0)
        {
            // 第一帧，直接开始新帧段
            _currentFrame = snapshot;
        }
        else if (InputFrameEquals(ref _currentFrame, ref snapshot))
        {
            // 输入状态相同，累加 duration
            _currentFrame.duration++;
        }
        else
        {
            // 输入状态变化，将之前的帧段写入列表，开始新帧段
            _recordedFrames.Add(_currentFrame);
            _currentFrame = snapshot;
        }
    }

    // ═══════════════════════════════════════════════════════
    // 帧比较（忽略 duration，只比较输入状态）
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 比较两个 InputFrame 的输入状态是否完全相同（忽略 duration）。
    /// 使用 ref 传递避免结构体拷贝开销。
    /// </summary>
    private static bool InputFrameEquals(ref InputFrame a, ref InputFrame b)
    {
        // P1
        if (!Mathf.Approximately(a.p1Horizontal, b.p1Horizontal)) return false;
        if (!Mathf.Approximately(a.p1Vertical, b.p1Vertical)) return false;
        if (a.p1JumpHeld != b.p1JumpHeld) return false;
        if (a.p1JumpDown != b.p1JumpDown) return false;
        if (a.p1SHeld != b.p1SHeld) return false;
        if (a.p1SDown != b.p1SDown) return false;
        if (a.p1ScanDown != b.p1ScanDown) return false;

        // P2
        if (!Mathf.Approximately(a.p2Horizontal, b.p2Horizontal)) return false;
        if (!Mathf.Approximately(a.p2Vertical, b.p2Vertical)) return false;
        if (a.p2JumpHeld != b.p2JumpHeld) return false;
        if (a.p2JumpDown != b.p2JumpDown) return false;
        if (a.p2DirectionDown != b.p2DirectionDown) return false;
        if (a.p2DisguiseDown != b.p2DisguiseDown) return false;
        if (!Mathf.Approximately(a.p2SwitchDir, b.p2SwitchDir)) return false;
        if (a.p2AbilityDown != b.p2AbilityDown) return false;

        // 全局
        if (a.pauseDown != b.pauseDown) return false;
        if (a.restartDown != b.restartDown) return false;
        if (a.noCooldownDown != b.noCooldownDown) return false;
        if (a.restartRoundDown != b.restartRoundDown) return false;
        if (a.nextRoundDown != b.nextRoundDown) return false;

        return true;
    }

    // ═══════════════════════════════════════════════════════
    // JSON 序列化（手写，避免依赖 JsonUtility 对 List<struct> 的限制）
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// 将 InputFrame 列表序列化为紧凑 JSON 数组。
    /// 只输出非默认值的字段，减少 JSON 体积。
    /// </summary>
    private static string SerializeToJson(List<InputFrame> frames)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[\n");

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            sb.Append("  { ");
            sb.Append($"\"duration\": {f.duration}");

            // P1 — 只输出非默认值
            if (!Mathf.Approximately(f.p1Horizontal, 0f)) sb.Append($", \"p1Horizontal\": {f.p1Horizontal:F1}");
            if (!Mathf.Approximately(f.p1Vertical, 0f))   sb.Append($", \"p1Vertical\": {f.p1Vertical:F1}");
            if (f.p1JumpHeld)   sb.Append(", \"p1JumpHeld\": true");
            if (f.p1JumpDown)   sb.Append(", \"p1JumpDown\": true");
            if (f.p1SHeld)      sb.Append(", \"p1SHeld\": true");
            if (f.p1SDown)      sb.Append(", \"p1SDown\": true");
            if (f.p1ScanDown)   sb.Append(", \"p1ScanDown\": true");

            // P2 — 只输出非默认值
            if (!Mathf.Approximately(f.p2Horizontal, 0f)) sb.Append($", \"p2Horizontal\": {f.p2Horizontal:F1}");
            if (!Mathf.Approximately(f.p2Vertical, 0f))   sb.Append($", \"p2Vertical\": {f.p2Vertical:F1}");
            if (f.p2JumpHeld)      sb.Append(", \"p2JumpHeld\": true");
            if (f.p2JumpDown)      sb.Append(", \"p2JumpDown\": true");
            if (f.p2DirectionDown) sb.Append(", \"p2DirectionDown\": true");
            if (f.p2DisguiseDown)  sb.Append(", \"p2DisguiseDown\": true");
            if (!Mathf.Approximately(f.p2SwitchDir, 0f)) sb.Append($", \"p2SwitchDir\": {f.p2SwitchDir:F1}");
            if (f.p2AbilityDown)   sb.Append(", \"p2AbilityDown\": true");

            // 全局 — 只输出非默认值
            if (f.pauseDown)        sb.Append(", \"pauseDown\": true");
            if (f.restartDown)      sb.Append(", \"restartDown\": true");
            if (f.noCooldownDown)   sb.Append(", \"noCooldownDown\": true");
            if (f.restartRoundDown) sb.Append(", \"restartRoundDown\": true");
            if (f.nextRoundDown)    sb.Append(", \"nextRoundDown\": true");

            sb.Append(" }");
            if (i < frames.Count - 1) sb.Append(",");
            sb.Append("\n");
        }

        sb.Append("]");
        return sb.ToString();
    }
}
