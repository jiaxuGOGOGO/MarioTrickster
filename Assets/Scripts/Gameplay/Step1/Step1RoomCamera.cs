using UnityEngine;

/// <summary>
/// 设计宪法第 1 步摄像机（替代跟随马里奥的 CameraController）。
/// 默认 WholeRoom：整间房一屏看完（《地狱邻居》式），玩家同时看得到自己和马里奥。
/// C 键循环：WholeRoom → FrameBoth（同框两人）→ FollowTrickster（跟自己）。
/// </summary>
[RequireComponent(typeof(Camera))]
public class Step1RoomCamera : MonoBehaviour
{
    [SerializeField] private MarioMindTuningSO tuning;
    [SerializeField] private Rect roomBounds = new Rect(0f, 0f, 36f, 10f);
    [SerializeField] private Transform mario;
    [SerializeField] private Transform trickster;
    [SerializeField] private float smoothing = 6f;

    private Camera cam;
    public Step1CameraMode Mode { get; private set; }
    public Rect RoomBounds => roomBounds;

    public void Configure(Rect bounds, Transform marioTransform, Transform tricksterTransform)
    {
        roomBounds = bounds; mario = marioTransform; trickster = tricksterTransform;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        if (tuning == null) tuning = MarioMindTuningSO.LoadOrDefault();
        Mode = tuning.cameraMode;
        var follow = GetComponent<CameraController>();
        if (follow != null) follow.enabled = false;
    }

    private void Start()
    {
        if (mario == null) { var m = FindObjectOfType<MarioController>(); if (m != null) mario = m.transform; }
        if (trickster == null) { var t = FindObjectOfType<TricksterController>(); if (t != null) trickster = t.transform; }
        Snap();
    }

    private void Update()
    {
        if (!Step1PlaytestLog.IsTyping && Input.GetKeyDown(KeyCode.C)) Mode = (Step1CameraMode)(((int)Mode + 1) % 3);
    }

    private void LateUpdate()
    {
        Frame(out Vector2 center, out float size);
        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        Vector3 target = new Vector3(center.x, center.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, target, k);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, k);
    }

    public void Snap()
    {
        Frame(out Vector2 center, out float size);
        transform.position = new Vector3(center.x, center.y, -10f);
        cam.orthographicSize = size;
    }

    /// <summary>纯计算：给定模式算出镜头中心与正交尺寸。</summary>
    public static void Compute(Step1CameraMode mode, Rect room, float aspect, float padding, float minHeight,
        Vector2? a, Vector2? b, out Vector2 center, out float orthoSize)
    {
        aspect = Mathf.Max(0.1f, aspect);
        Rect view = room;
        if (mode == Step1CameraMode.FrameBoth && a.HasValue && b.HasValue)
        {
            Vector2 min = Vector2.Min(a.Value, b.Value) - Vector2.one * 2f;
            Vector2 max = Vector2.Max(a.Value, b.Value) + Vector2.one * 2f;
            view = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        else if (mode == Step1CameraMode.FollowTrickster && b.HasValue)
        {
            view = new Rect(b.Value.x - minHeight * aspect * 0.5f, b.Value.y - minHeight * 0.5f, minHeight * aspect, minHeight);
        }
        float half = Mathf.Max(view.height * 0.5f, view.width * 0.5f / aspect) + padding;
        if (mode != Step1CameraMode.WholeRoom) half = Mathf.Max(half, minHeight * 0.5f);
        float roomHalf = Mathf.Max(room.height * 0.5f, room.width * 0.5f / aspect) + padding;
        half = Mathf.Min(half, roomHalf);
        center = view.center;
        // 不让镜头看到房间外太多：中心夹在房间内
        float halfW = half * aspect;
        center.x = room.width * 0.5f + padding <= halfW ? room.center.x : Mathf.Clamp(center.x, room.xMin + halfW - padding, room.xMax - halfW + padding);
        center.y = room.height * 0.5f + padding <= half ? room.center.y : Mathf.Clamp(center.y, room.yMin + half - padding, room.yMax - half + padding);
        orthoSize = half;
    }

    private void Frame(out Vector2 center, out float size)
    {
        Vector2? a = mario != null ? (Vector2?)mario.position : null;
        Vector2? b = trickster != null ? (Vector2?)trickster.position : null;
        Compute(Mode, roomBounds, cam != null ? cam.aspect : 16f / 9f, tuning.cameraPadding, tuning.frameBothMinHeight, a, b, out center, out size);
    }
}
