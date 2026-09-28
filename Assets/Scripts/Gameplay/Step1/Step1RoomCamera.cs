using UnityEngine;

/// <summary>
/// 设计宪法第 1 步摄像机（替代跟随马里奥的 CameraController）。
/// 默认 WholeRoom：整间房一屏看完（《地狱邻居》式），玩家同时看得到自己和马里奥。
/// C 键循环：WholeRoom → FrameBoth（同框两人）→ FollowTrickster（跟自己）→ SmartFollow（S207 智能跟随）。
/// S207（用户："关卡能不能像死亡细胞那样不局限于这个小框"）：房间宽于 maxWholeRoomWidth 或高于 maxWholeRoomHeight 时，
/// 开局自动用 SmartFollow——镜头跟着你，朝移动方向多看几格，小跳不晃（上下死区）；马里奥走近时自动拉远把两人都框进来；
/// 马里奥在屏幕外时屏幕边缘有红箭头（Step1OffscreenMarkers），右上角有小地图（Step1MiniMap）。默认 48×12 房间不受影响。
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
    private float shakeAmp, shakeUntil, shakeDuration;
    private Vector3 shakeOffset;
    // S207 智能跟随状态：上下死区里的"跟随高度"、平滑后的前瞻方向
    private float followY = float.NaN, lookDir;
    private Vector2 lastFocus;
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

    /// <summary>S195：房间太高（多层楼/监狱塔）时，"看整个房间"会把人缩成小点 → 自动改为"框住两人"。</summary>
    public static Step1CameraMode AutoMode(Step1CameraMode wanted, Rect room, float maxWholeRoomHeight) =>
        wanted == Step1CameraMode.WholeRoom && maxWholeRoomHeight > 0f && room.height > maxWholeRoomHeight ? Step1CameraMode.FrameBoth : wanted;

    /// <summary>S207：宽房间（或高楼）开局用"大房间镜头"（默认智能跟随）。只在想要整屏、而整屏会把人缩成小点时才换。</summary>
    public static Step1CameraMode AutoMode(Step1CameraMode wanted, Rect room, float maxWholeRoomHeight, float maxWholeRoomWidth, Step1CameraMode bigRoom)
    {
        if (wanted != Step1CameraMode.WholeRoom) return wanted;
        bool tall = maxWholeRoomHeight > 0f && room.height > maxWholeRoomHeight;
        bool wide = maxWholeRoomWidth > 0f && room.width > maxWholeRoomWidth;
        return tall || wide ? bigRoom : wanted;
    }

    /// <summary>S207：当前镜头是不是看不到整个房间（小地图 / 屏外箭头只在这时有意义）。</summary>
    public bool SeesWholeRoom
    {
        get
        {
            if (cam == null) return true;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector2 c = transform.position;
            return c.x - halfW <= roomBounds.xMin + 0.5f && c.x + halfW >= roomBounds.xMax - 0.5f && c.y - halfH <= roomBounds.yMin + 0.5f && c.y + halfH >= roomBounds.yMax - 0.5f;
        }
    }
    public Rect ViewRect { get { if (cam == null) return roomBounds; float hh = cam.orthographicSize, hw = hh * cam.aspect; return new Rect(transform.position.x - hw, transform.position.y - hh, hw * 2f, hh * 2f); } }

    private void Start()
    {
        Mode = AutoMode(Mode, roomBounds, tuning.maxWholeRoomHeight, tuning.maxWholeRoomWidth, tuning.bigRoomCamera);
        if (mario == null) { var m = FindObjectOfType<MarioController>(); if (m != null) mario = m.transform; }
        if (trickster == null) { var t = FindObjectOfType<TricksterController>(); if (t != null) trickster = t.transform; }
        Snap();
    }

    private void Update()
    {
        if (!Step1PlaytestLog.IsTyping && !Step1Screen.HelpOpen && Step1Keys.Down(KeyCode.C))
        {
            Mode = (Step1CameraMode)(((int)Mode + 1) % ModeCount);
            Step1Hint.Show(string.Format(Step1Text.CameraSwitched, Step1Text.CameraModeName(Mode)));
        }
    }

    private void LateUpdate()
    {
        Frame(out Vector2 center, out float size);
        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        Vector3 target = new Vector3(center.x, center.y, transform.position.z);
        Vector3 basePos = transform.position - shakeOffset;
        basePos = Vector3.Lerp(basePos, target, k);
        // S193：连招震屏（真实时间计时，顿帧期间也在抖）
        float left = shakeUntil - Time.unscaledTime;
        shakeOffset = left > 0f && shakeDuration > 0f
            ? (Vector3)(Random.insideUnitCircle * shakeAmp * (left / shakeDuration))
            : Vector3.zero;
        transform.position = basePos + shakeOffset;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, k);
    }

    /// <summary>S193：屏幕震动（幅度按格，随时间衰减）。多次调用取较大的那次。</summary>
    public void Shake(float amplitude, float seconds)
    {
        if (amplitude <= 0f || seconds <= 0f || Step1HandsOffCheck.IsRunning) return;
        if (Time.unscaledTime < shakeUntil && amplitude < shakeAmp) return;
        shakeAmp = amplitude; shakeDuration = seconds; shakeUntil = Time.unscaledTime + seconds;
    }

    public void Snap()
    {
        Frame(out Vector2 center, out float size);
        shakeOffset = Vector3.zero;
        transform.position = new Vector3(center.x, center.y, -10f);
        cam.orthographicSize = size;
    }

    public const int ModeCount = 4;

    /// <summary>S207 纯计算：智能跟随（死亡细胞式）。focus = 你（不在场时 = 马里奥）；other = 另一个人。
    /// 平时一屏 viewHeight 格高、朝移动方向多看 lookAhead 格；另一个人离得 ≤ includeCells 时拉远把两人框进来（最多 maxViewHeight 格高），
    /// 框不下就只跟你（屏外箭头会指出他在哪）。最后夹在房间内，不看房间外。</summary>
    public static void SmartView(Rect room, float aspect, float padding, Vector2 focus, Vector2? other,
        float viewHeight, float maxViewHeight, float includeCells, float lookAhead, out Vector2 center, out float orthoSize, out bool framedBoth)
    {
        aspect = Mathf.Max(0.1f, aspect);
        float half = Mathf.Max(3f, viewHeight * 0.5f);
        center = focus + new Vector2(lookAhead, 0f);
        framedBoth = false;
        if (other.HasValue && Vector2.Distance(focus, other.Value) <= includeCells)
        {
            Vector2 min = Vector2.Min(focus, other.Value) - Vector2.one * 2f, max = Vector2.Max(focus, other.Value) + Vector2.one * 2f;
            float need = Mathf.Max((max.y - min.y) * 0.5f, (max.x - min.x) * 0.5f / aspect);
            if (need <= Mathf.Max(half, maxViewHeight * 0.5f)) { half = Mathf.Max(half, need); center = (min + max) * 0.5f; framedBoth = true; }
        }
        ClampToRoom(room, aspect, padding, ref center, ref half);
        orthoSize = half;
    }

    /// <summary>镜头不超过整个房间；中心夹在房间内（不让看到房间外太多）。</summary>
    public static void ClampToRoom(Rect room, float aspect, float padding, ref Vector2 center, ref float half)
    {
        float roomHalf = Mathf.Max(room.height * 0.5f, room.width * 0.5f / aspect) + padding;
        half = Mathf.Min(half, roomHalf);
        float halfW = half * aspect;
        center.x = room.width * 0.5f + padding <= halfW ? room.center.x : Mathf.Clamp(center.x, room.xMin + halfW - padding, room.xMax - halfW + padding);
        center.y = room.height * 0.5f + padding <= half ? room.center.y : Mathf.Clamp(center.y, room.yMin + half - padding, room.yMax - half + padding);
    }

    /// <summary>S207 纯计算：上下死区——小跳不动镜头，离开死区才跟（只跟超出的那部分）。</summary>
    public static float DeadZoneFollow(float current, float target, float deadZone)
    {
        if (float.IsNaN(current)) return target;
        float d = target - current;
        if (Mathf.Abs(d) <= deadZone) return current;
        return current + d - Mathf.Sign(d) * deadZone;
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
        else if ((mode == Step1CameraMode.FollowTrickster || mode == Step1CameraMode.SmartFollow) && b.HasValue)
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
        // 自动检查时捣蛋者退场（物体被关掉）→ 跟马里奥
        Vector2? b = trickster != null && trickster.gameObject.activeInHierarchy ? (Vector2?)trickster.position : null;
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        if (Mode == Step1CameraMode.SmartFollow && (a.HasValue || b.HasValue))
        {
            Vector2 focus = b ?? a.Value; Vector2? other = b.HasValue ? a : null;
            if (float.IsNaN(followY)) lastFocus = focus;
            // 前瞻：按实际移动方向平滑转向（停下来慢慢回正）
            float vx = Time.unscaledDeltaTime > 0f ? (focus.x - lastFocus.x) / Mathf.Max(0.001f, Time.unscaledDeltaTime) : 0f;
            lastFocus = focus;
            float wantDir = Mathf.Abs(vx) > 0.5f ? Mathf.Sign(vx) : 0f;
            lookDir = Mathf.MoveTowards(lookDir, wantDir, Time.unscaledDeltaTime * 1.5f);
            followY = DeadZoneFollow(followY, focus.y, tuning.followVerticalDeadZone);
            SmartView(roomBounds, aspect, tuning.cameraPadding, new Vector2(focus.x, followY), other,
                tuning.followViewHeight, tuning.followMaxViewHeight, tuning.followIncludeMarioCells, lookDir * tuning.followLookAhead,
                out center, out size, out _);
            return;
        }
        Compute(Mode, roomBounds, aspect, tuning.cameraPadding, tuning.frameBothMinHeight, a, b, out center, out size);
    }
}
