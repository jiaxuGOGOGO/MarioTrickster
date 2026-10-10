using System;
using UnityEngine;

/// <summary>
/// S187：草丛沙沙响（可见信号，无声也能读懂——宪法 H6）。
///   - 有人（捣蛋者）在里面移动 → 晃动；
///   - 偶尔起风 → 同样的晃动（"否认空间"：讨论稿 v0.3 §3.3.1 第 5 条，引 BB p.276：10–25% 的动静与捣蛋者无关）。
///     两种晃动外观完全一样，马里奥分不出来，只能起疑去看——这就是随机带来的涌现。
///   - 马里奥自己钻草丛引起的晃动不广播给他（他知道是自己）。
/// 马里奥只通过 MarioEyes 看到晃动的"位置"，永远不知道原因（H4）。
/// </summary>
public class RustleOnPass : MonoBehaviour
{
    /// <summary>草丛晃了（只给位置，不给原因）。</summary>
    public static event Action<Transform> Rustled;

    [Tooltip("里面的人移动速度超过此值才晃（格/秒）")]
    [SerializeField] private float moveThreshold = 0.6f;
    [Tooltip("两次晃动最短间隔（秒）")]
    [SerializeField] private float rustleCooldown = 0.8f;
    [Tooltip("晃动表现时长（秒）")]
    [SerializeField] private float rustleSeconds = 0.5f;
    [Tooltip("起风：两次随机晃动之间的最短/最长秒数（≤0 关闭起风）")]
    [SerializeField] private float windMinSeconds = 7f;
    [SerializeField] private float windMaxSeconds = 16f;

    private Transform visual;
    private Vector3 visualBase;
    private float cooldown, shake, windTimer;
    private System.Random dice;

    public void ConfigureWind(float min, float max, int seed)
    {
        windMinSeconds = min; windMaxSeconds = max;
        dice = new System.Random(seed);
        ScheduleWind();
    }

    private void Awake()
    {
        visual = transform.Find("Visual");
        if (visual != null) visualBase = visual.localPosition;
        if (dice == null) dice = new System.Random(Mathf.RoundToInt(transform.position.x * 73 + transform.position.y * 131));
        ScheduleWind();
    }

    private void ScheduleWind()
    {
        windTimer = windMaxSeconds > 0f ? NextWind(dice, windMinSeconds, windMaxSeconds) : float.PositiveInfinity;
    }

    /// <summary>纯函数：下一次起风的间隔（供测试）。</summary>
    public static float NextWind(System.Random rng, float min, float max)
    {
        if (max <= 0f) return float.PositiveInfinity;
        min = Mathf.Max(0.1f, Mathf.Min(min, max));
        return min + (float)rng.NextDouble() * (max - min);
    }

    private void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        windTimer -= Time.deltaTime;
        if (windTimer <= 0f) { ScheduleWind(); Rustle(true); }

        if (visual != null)
        {
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                float s = Mathf.Sin(Time.time * 40f) * 0.06f;
                visual.localPosition = visualBase + new Vector3(s, 0f, 0f);
                if (shake <= 0f) visual.localPosition = visualBase;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (cooldown > 0f || other == null || other.attachedRigidbody == null) return;
        if (other.attachedRigidbody.velocity.magnitude < moveThreshold) return;
        bool isMario = other.GetComponentInParent<MarioController>() != null;
        Rustle(false, !isMario);
    }

    private void Rustle(bool wind, bool broadcast = true)
    {
        if (cooldown > 0f) return;
        cooldown = rustleCooldown;
        shake = rustleSeconds;
        if (wind || broadcast) Rustled?.Invoke(transform);
    }
}
