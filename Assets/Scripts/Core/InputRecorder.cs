using UnityEngine;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════
// InputRecorder — 玩家操作录制器（S50: TAS 录播系统）
//
// [AI防坑警告] 此类是 TAS 录播系统的录制端。
// 它在运行时采样 IInputProvider 的所有信号，使用 RLE（Run-Length Encoding）
// 压缩连续相同的输入帧，生成可直接注入 AutomatedInputProvider 的 InputFrame 序列。
//
// 使用方式（运行时）：
//   F10 = 开始/停止录制
//   F11 = 将录制结果序列化为 JSON 并打印到 Console（方便复制为测试用例数据）
//
// RLE 压缩原理：
//   玩家连续多帧按住相同按键组合时，只记录一条 InputFrame + duration。
//   典型压缩比 1:40+（大部分时间输入状态不变）。
//   一分钟录制可能只产生几十条记录，而非 3000+ 原始帧。
//
// 架构关系：
//   InputRecorder（录制） → List<InputFrame> → JSON → AutomatedInputProvider（回放）
//   形成完整的 Record/Replay 闭环。
//
// 依赖：
//   - InputFrame 结构体（定义在 AutomatedInputProvider.cs）
//   - IInputProvider 接口（定义在 IInputProvider.cs）
//   - InputManager（通过 GetCurrentProvider() 获取当前输入源）
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// 玩家操作录制器。
/// 
/// 在运行时采样真实玩家输入，使用 RLE 压缩生成 InputFrame 序列。
/// 录制结果可序列化为 JSON，直接用于 AutomatedInputProvider 回放测试。
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

        // 录制采样（每帧执行）
        if (_isRecording)
        {
            SampleFrame();
        }
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

        Debug.Log("[InputRecorder] ● REC started — Press F10 to stop, F11 to export JSON");
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

        Debug.Log($"[InputRecorder] ■ REC stopped — {_recordedFrames.Count} segments from {TotalLogicalFrames} frames (RLE ratio: {ratio:F1}:1)");
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
