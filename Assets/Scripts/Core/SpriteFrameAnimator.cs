using UnityEngine;

/// <summary>
/// SpriteFrameAnimator — 轻量 Sprite Sheet 帧动画播放器
///
/// 用于 Asset Import Pipeline / Apply Art to Selected 自动生成的多帧素材。
/// 它只驱动同物体上的 SpriteRenderer.sprite，不依赖 Animator Controller，
/// 避免给用户增加额外配置步骤。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameAnimator : MonoBehaviour
{
    [Tooltip("按播放顺序排列的 Sprite 帧")]
    public Sprite[] frames;

    [Tooltip("每秒播放帧数")]
    [Min(0.1f)]
    public float frameRate = 10f;

    [Tooltip("进入 PlayMode 后自动播放")]
    public bool playOnStart = true;

    [Tooltip("循环播放")]
    public bool loop = true;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;
    private bool isPlaying;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        isPlaying = playOnStart;
        ApplyFrame(0);
    }

    private void OnEnable()
    {
        isPlaying = playOnStart;
    }

    private void Update()
    {
        if (!isPlaying || frames == null || frames.Length <= 1 || spriteRenderer == null) return;

        timer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(0.1f, frameRate);
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    isPlaying = false;
                }
            }

            ApplyFrame(currentFrame);
        }
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
        currentFrame = 0;
        timer = 0f;
        ApplyFrame(0);
        isPlaying = true;
    }

    private void ApplyFrame(int index)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || frames == null || frames.Length == 0) return;

        index = Mathf.Clamp(index, 0, frames.Length - 1);
        if (frames[index] != null)
        {
            spriteRenderer.sprite = frames[index];
        }
    }
}
