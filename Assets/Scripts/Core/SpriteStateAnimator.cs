using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpriteStateAnimator — 商业 2D 角色素材的轻量状态动画播放器。
///
/// 它只负责驱动当前 Visual 节点上的 SpriteRenderer.sprite，不修改 Root 的物理、碰撞、缩放或关卡选择语义。
/// 优先读取 MarioController 暴露的运动状态；没有 MarioController 时回退读取父级 Rigidbody2D。
///
/// Session S-DynState 重构：动态字典架构
///   - 保留 MotionState 枚举（idle/run/jump/fall）用于核心物理状态判定，向后完全兼容。
///   - 新增 List&lt;StateFrames&gt; stateGroups 动态列表，每个 StateFrames 用字符串 tag 标识。
///   - 核心 4 状态自动映射到 stateGroups 中 tag 为 "idle"/"run"/"jump"/"fall" 的条目。
///   - 未来新状态（wallslide/swim/roll 等）只需在 Inspector 点"+"加一行、填 tag、拖帧即可，零代码。
///   - 外部可通过 SetStateByTag("wallslide") 强制切换到任意自定义状态。
///   - EvaluateState() 仍只自动判定核心 4 状态；自定义状态需由外部脚本（如 WallSlideController）主动调用 SetStateByTag。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteStateAnimator : MonoBehaviour
{
    // ── 核心物理状态枚举（向后兼容，不可删除）──────────────────
    public enum MotionState
    {
        Idle,
        Run,
        Jump,
        Fall
    }

    [Serializable]
    public class StateFrames
    {
        [Tooltip("状态标签（小写英文）。核心状态: idle/run/jump/fall。自定义状态: wallslide/swim/roll/crouch 等，随意填写。")]
        public string tag = "idle";
        public Sprite[] frames = new Sprite[0];
        [Min(0.1f)] public float frameRate = 10f;
        public bool loop = true;
    }

    // ── 动态状态帧组列表（Inspector 可编辑）──────────────────
    [Header("状态帧组")]
    [Tooltip("所有动画状态。核心 4 状态 (idle/run/jump/fall) 由物理自动驱动；其他自定义状态由外部脚本调用 SetStateByTag() 驱动。")]
    public List<StateFrames> stateGroups = new List<StateFrames>
    {
        new StateFrames { tag = "idle",  frameRate = 6f,  loop = true  },
        new StateFrames { tag = "run",   frameRate = 12f, loop = true  },
        new StateFrames { tag = "jump",  frameRate = 10f, loop = false },
        new StateFrames { tag = "fall",  frameRate = 10f, loop = false }
    };

    // ── 向后兼容属性（旧代码通过 .idle/.run/.jump/.fall 访问仍然有效）──
    // [AI防坑警告] 这些属性是为了让 AssetApplyToSelected / AssetImportPipeline 等
    // 旧代码中 stateAnimator.idle.frames = ... 的写法继续编译通过。
    // 它们直接映射到 stateGroups 列表中对应 tag 的条目。
    // 如果 stateGroups 中没有对应 tag，会自动创建一个新条目。
    public StateFrames idle { get { return GetOrCreateGroup("idle",  6f,  true);  } }
    public StateFrames run  { get { return GetOrCreateGroup("run",  12f, true);  } }
    public StateFrames jump { get { return GetOrCreateGroup("jump", 10f, false); } }
    public StateFrames fall { get { return GetOrCreateGroup("fall", 10f, false); } }

    [Header("状态判定")]
    [Tooltip("落地时水平速度超过该阈值即播放 run；否则播放 idle。")]
    [Min(0f)] public float runSpeedThreshold = 0.1f;

    [Tooltip("未落地且竖直速度高于该阈值即播放 jump。")]
    public float jumpVelocityThreshold = 0.05f;

    [Tooltip("未落地且竖直速度低于该阈值即播放 fall。")]
    public float fallVelocityThreshold = -0.05f;

    [Tooltip("进入 PlayMode 后自动播放。")]
    public bool playOnStart = true;

    private SpriteRenderer spriteRenderer;
    private MarioController marioController;
    private Rigidbody2D body;
    private string currentTag = "idle";
    private int currentFrame;
    private float timer;
    private bool isPlaying;
    private bool _externalOverride; // 外部脚本是否正在强制控制状态

    // ── 运行时查找缓存（避免每帧遍历列表）──────────────────
    private Dictionary<string, StateFrames> _cache;
    private bool _cacheDirty = true;

    /// <summary>当前活跃的状态标签。</summary>
    public string CurrentTag => currentTag;

    /// <summary>当前活跃的核心物理状态（仅对 idle/run/jump/fall 有效，自定义状态返回 Idle）。</summary>
    public MotionState CurrentState => TagToMotionState(currentTag);

    // ─────────────────────────────────────────────────────
    #region 生命周期

    private void Awake()
    {
        CacheComponents();
        RebuildCache();
        isPlaying = playOnStart;
        SwitchToTag(EvaluateTag(), true);
    }

    private void OnEnable()
    {
        isPlaying = playOnStart;
        CacheComponents();
        RebuildCache();
        SwitchToTag(EvaluateTag(), true);
    }

    private void Update()
    {
        if (!isPlaying) return;
        CacheComponents();

        // 外部强制控制时，不自动切换状态
        if (!_externalOverride)
        {
            string nextTag = EvaluateTag();
            if (nextTag != currentTag)
            {
                SwitchToTag(nextTag, true);
            }
        }

        AdvanceFrame(Time.deltaTime);
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 公共 API

    public void Play()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void Restart()
    {
        timer = 0f;
        currentFrame = 0;
        _externalOverride = false;
        SwitchToTag(EvaluateTag(), true);
        isPlaying = true;
    }

    /// <summary>
    /// 通过核心枚举设置状态（向后兼容）。
    /// </summary>
    public void SetState(MotionState state, bool restart = true)
    {
        _externalOverride = false;
        SwitchToTag(MotionStateToTag(state), restart);
    }

    /// <summary>
    /// 通过字符串标签设置任意状态（新扩展 API）。
    /// 外部脚本（如 WallSlideController）调用此方法强制切换到自定义状态。
    /// 调用后自动判定暂停，直到调用 ReleaseStateOverride() 或 Restart()。
    /// </summary>
    public void SetStateByTag(string tag, bool restart = true)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.ToLowerInvariant();
        _externalOverride = true;
        SwitchToTag(tag, restart);
    }

    /// <summary>
    /// 释放外部强制控制，恢复自动物理状态判定。
    /// </summary>
    public void ReleaseStateOverride()
    {
        _externalOverride = false;
    }

    /// <summary>
    /// 获取指定核心状态的帧数组（向后兼容）。
    /// </summary>
    public Sprite[] GetFrames(MotionState state)
    {
        StateFrames group = GetGroupByTag(MotionStateToTag(state));
        return group != null ? group.frames : new Sprite[0];
    }

    /// <summary>
    /// 获取指定标签的帧数组。
    /// </summary>
    public Sprite[] GetFramesByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return new Sprite[0];
        StateFrames group = GetGroupByTag(tag.ToLowerInvariant());
        return group != null ? group.frames : new Sprite[0];
    }

    /// <summary>
    /// 检查是否存在指定标签的状态帧组且有可用帧。
    /// </summary>
    public bool HasFramesForTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return HasUsableFramesByTag(tag.ToLowerInvariant());
    }

    /// <summary>
    /// 标记缓存需要重建（当外部代码修改了 stateGroups 列表后调用）。
    /// </summary>
    public void MarkCacheDirty()
    {
        _cacheDirty = true;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 内部逻辑

    private void CacheComponents()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (marioController == null) marioController = GetComponentInParent<MarioController>();
        if (body == null) body = GetComponentInParent<Rigidbody2D>();
    }

    private void RebuildCache()
    {
        if (_cache == null)
            _cache = new Dictionary<string, StateFrames>(StringComparer.OrdinalIgnoreCase);
        else
            _cache.Clear();

        if (stateGroups != null)
        {
            foreach (var group in stateGroups)
            {
                if (group == null || string.IsNullOrEmpty(group.tag)) continue;
                string key = group.tag.ToLowerInvariant();
                // 同名 tag 只保留第一个（避免重复）
                if (!_cache.ContainsKey(key))
                    _cache[key] = group;
            }
        }
        _cacheDirty = false;
    }

    /// <summary>
    /// 核心物理状态自动判定（只判定 idle/run/jump/fall）。
    /// 自定义状态不参与自动判定，必须由外部脚本主动调用 SetStateByTag()。
    /// </summary>
    private string EvaluateTag()
    {
        bool grounded = true;
        Vector2 velocity = Vector2.zero;

        if (marioController != null)
        {
            grounded = marioController.IsGrounded;
            velocity = marioController.Velocity;
        }
        else if (body != null)
        {
            velocity = body.velocity;
            grounded = Mathf.Abs(velocity.y) <= Mathf.Max(Mathf.Abs(jumpVelocityThreshold), Mathf.Abs(fallVelocityThreshold));
        }

        if (!grounded)
        {
            if (velocity.y > jumpVelocityThreshold && HasUsableFramesByTag("jump")) return "jump";
            if (velocity.y < fallVelocityThreshold && HasUsableFramesByTag("fall")) return "fall";
            if (velocity.y > 0f && HasUsableFramesByTag("jump")) return "jump";
            if (HasUsableFramesByTag("fall")) return "fall";
        }

        if (Mathf.Abs(velocity.x) > runSpeedThreshold && HasUsableFramesByTag("run")) return "run";
        return "idle";
    }

    private void SwitchToTag(string tag, bool restart)
    {
        currentTag = ResolveTagWithFallback(tag);
        if (restart)
        {
            timer = 0f;
            currentFrame = 0;
        }
        ApplyFrame(currentFrame);
    }

    private string ResolveTagWithFallback(string tag)
    {
        if (HasUsableFramesByTag(tag)) return tag;
        // 核心状态之间的兜底逻辑
        if (tag == "fall" && HasUsableFramesByTag("jump")) return "jump";
        if (tag == "jump" && HasUsableFramesByTag("fall")) return "fall";
        if (tag == "run"  && HasUsableFramesByTag("idle")) return "idle";
        if (HasUsableFramesByTag("idle")) return "idle";
        if (HasUsableFramesByTag("run"))  return "run";
        if (HasUsableFramesByTag("jump")) return "jump";
        if (HasUsableFramesByTag("fall")) return "fall";
        return "idle";
    }

    private void AdvanceFrame(float deltaTime)
    {
        StateFrames group = GetGroupByTag(currentTag);
        if (group == null || group.frames == null || group.frames.Length == 0) return;
        if (group.frames.Length == 1)
        {
            ApplyFrame(0);
            return;
        }

        timer += deltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, group.frameRate);
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;
            if (currentFrame >= group.frames.Length)
            {
                currentFrame = group.loop ? 0 : group.frames.Length - 1;
            }
            ApplyFrame(currentFrame);
        }
    }

    private void ApplyFrame(int index)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        StateFrames group = GetGroupByTag(currentTag);
        if (spriteRenderer == null || group == null || group.frames == null || group.frames.Length == 0) return;

        index = Mathf.Clamp(index, 0, group.frames.Length - 1);
        Sprite frame = group.frames[index];
        if (frame != null) spriteRenderer.sprite = frame;
    }

    private bool HasUsableFramesByTag(string tag)
    {
        StateFrames group = GetGroupByTag(tag);
        return group != null && group.frames != null && group.frames.Length > 0;
    }

    // 向后兼容：MotionState 枚举版本的 HasUsableFrames
    private bool HasUsableFrames(MotionState state)
    {
        return HasUsableFramesByTag(MotionStateToTag(state));
    }

    private StateFrames GetGroupByTag(string tag)
    {
        if (_cacheDirty || _cache == null) RebuildCache();
        if (string.IsNullOrEmpty(tag)) return null;
        _cache.TryGetValue(tag, out StateFrames result);
        return result;
    }

    // 向后兼容：MotionState 枚举版本的 GetGroup
    private StateFrames GetGroup(MotionState state)
    {
        return GetGroupByTag(MotionStateToTag(state));
    }

    /// <summary>
    /// 从 stateGroups 中查找指定 tag 的条目；如果不存在则创建一个新条目并加入列表。
    /// 用于向后兼容属性（.idle/.run/.jump/.fall）。
    /// </summary>
    private StateFrames GetOrCreateGroup(string tag, float defaultFrameRate, bool defaultLoop)
    {
        if (stateGroups != null)
        {
            foreach (var group in stateGroups)
            {
                if (group != null && string.Equals(group.tag, tag, StringComparison.OrdinalIgnoreCase))
                    return group;
            }
        }
        // 不存在则创建
        if (stateGroups == null) stateGroups = new List<StateFrames>();
        var newGroup = new StateFrames { tag = tag, frameRate = defaultFrameRate, loop = defaultLoop };
        stateGroups.Add(newGroup);
        _cacheDirty = true;
        return newGroup;
    }

    #endregion

    // ─────────────────────────────────────────────────────
    #region 工具方法

    /// <summary>核心枚举 → 字符串标签。</summary>
    public static string MotionStateToTag(MotionState state)
    {
        switch (state)
        {
            case MotionState.Run:  return "run";
            case MotionState.Jump: return "jump";
            case MotionState.Fall: return "fall";
            default:               return "idle";
        }
    }

    /// <summary>字符串标签 → 核心枚举（无法映射时返回 Idle）。</summary>
    public static MotionState TagToMotionState(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return MotionState.Idle;
        switch (tag.ToLowerInvariant())
        {
            case "run":  return MotionState.Run;
            case "jump": return MotionState.Jump;
            case "fall": return MotionState.Fall;
            default:     return MotionState.Idle;
        }
    }

    #endregion
}
