using System;
using UnityEngine;

/// <summary>
/// SpriteStateAnimator — 商业 2D 角色素材的轻量状态动画播放器。
///
/// 它只负责驱动当前 Visual 节点上的 SpriteRenderer.sprite，不修改 Root 的物理、碰撞、缩放或关卡选择语义。
/// 优先读取 MarioController 暴露的运动状态；没有 MarioController 时回退读取父级 Rigidbody2D。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteStateAnimator : MonoBehaviour
{
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
        public MotionState state = MotionState.Idle;
        public Sprite[] frames = new Sprite[0];
        [Min(0.1f)] public float frameRate = 10f;
        public bool loop = true;
    }

    [Header("状态帧组")]
    public StateFrames idle = new StateFrames { state = MotionState.Idle, frameRate = 6f, loop = true };
    public StateFrames run = new StateFrames { state = MotionState.Run, frameRate = 12f, loop = true };
    public StateFrames jump = new StateFrames { state = MotionState.Jump, frameRate = 10f, loop = false };
    public StateFrames fall = new StateFrames { state = MotionState.Fall, frameRate = 10f, loop = false };

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
    private MotionState currentState = MotionState.Idle;
    private int currentFrame;
    private float timer;
    private bool isPlaying;

    public MotionState CurrentState => currentState;

    private void Awake()
    {
        CacheComponents();
        isPlaying = playOnStart;
        SwitchState(EvaluateState(), true);
    }

    private void OnEnable()
    {
        isPlaying = playOnStart;
        CacheComponents();
        SwitchState(EvaluateState(), true);
    }

    private void Update()
    {
        if (!isPlaying) return;
        CacheComponents();

        MotionState nextState = EvaluateState();
        if (nextState != currentState)
        {
            SwitchState(nextState, true);
        }

        AdvanceFrame(Time.deltaTime);
    }

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
        SwitchState(EvaluateState(), true);
        isPlaying = true;
    }

    public void SetState(MotionState state, bool restart = true)
    {
        SwitchState(state, restart);
    }

    public Sprite[] GetFrames(MotionState state)
    {
        StateFrames group = GetGroup(state);
        return group != null ? group.frames : new Sprite[0];
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (marioController == null) marioController = GetComponentInParent<MarioController>();
        if (body == null) body = GetComponentInParent<Rigidbody2D>();
    }

    private MotionState EvaluateState()
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
            if (velocity.y > jumpVelocityThreshold && HasUsableFrames(MotionState.Jump)) return MotionState.Jump;
            if (velocity.y < fallVelocityThreshold && HasUsableFrames(MotionState.Fall)) return MotionState.Fall;
            if (velocity.y > 0f && HasUsableFrames(MotionState.Jump)) return MotionState.Jump;
            if (HasUsableFrames(MotionState.Fall)) return MotionState.Fall;
        }

        if (Mathf.Abs(velocity.x) > runSpeedThreshold && HasUsableFrames(MotionState.Run)) return MotionState.Run;
        return MotionState.Idle;
    }

    private void SwitchState(MotionState state, bool restart)
    {
        currentState = ResolveStateWithFallback(state);
        if (restart)
        {
            timer = 0f;
            currentFrame = 0;
        }
        ApplyFrame(currentFrame);
    }

    private MotionState ResolveStateWithFallback(MotionState state)
    {
        if (HasUsableFrames(state)) return state;
        if (state == MotionState.Fall && HasUsableFrames(MotionState.Jump)) return MotionState.Jump;
        if (state == MotionState.Jump && HasUsableFrames(MotionState.Fall)) return MotionState.Fall;
        if (state == MotionState.Run && HasUsableFrames(MotionState.Idle)) return MotionState.Idle;
        if (HasUsableFrames(MotionState.Idle)) return MotionState.Idle;
        if (HasUsableFrames(MotionState.Run)) return MotionState.Run;
        if (HasUsableFrames(MotionState.Jump)) return MotionState.Jump;
        if (HasUsableFrames(MotionState.Fall)) return MotionState.Fall;
        return MotionState.Idle;
    }

    private void AdvanceFrame(float deltaTime)
    {
        StateFrames group = GetGroup(currentState);
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
        StateFrames group = GetGroup(currentState);
        if (spriteRenderer == null || group == null || group.frames == null || group.frames.Length == 0) return;

        index = Mathf.Clamp(index, 0, group.frames.Length - 1);
        Sprite frame = group.frames[index];
        if (frame != null) spriteRenderer.sprite = frame;
    }

    private bool HasUsableFrames(MotionState state)
    {
        StateFrames group = GetGroup(state);
        return group != null && group.frames != null && group.frames.Length > 0;
    }

    private StateFrames GetGroup(MotionState state)
    {
        switch (state)
        {
            case MotionState.Run: return run;
            case MotionState.Jump: return jump;
            case MotionState.Fall: return fall;
            default: return idle;
        }
    }
}
