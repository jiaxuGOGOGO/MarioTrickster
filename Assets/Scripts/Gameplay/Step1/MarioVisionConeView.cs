using UnityEngine;

/// <summary>
/// 把马里奥的视锥画出来（玩家可读的"他在看哪里"）。只画几何视锥 + 被墙挡住的截断，
/// 与 MarioVision 判定用同一组调参。纯表现。
/// S224（Shadow Tactics 视锥灌注）：起疑时从眼睛往外灌黄色（认出你 = 红），灌到的长度 = 视野 × 起疑进度，被墙挡住的地方不灌。
/// 只是把已有的 SuspicionMeter 画出来（H4：不新增感知；H6：黄 = ?，红 = !，和头顶标记同色）。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MarioVisionConeView : MonoBehaviour
{
    [SerializeField] private MarioMindDriver driver;
    [SerializeField] private int rays = 14;

    private LineRenderer line;
    private MarioController mario;
    private Mesh fillMesh; private MeshRenderer fillMr;
    private Vector3[] fv; private int[] ftri; private Color[] fcol; private float[] clear;

    private void Start()
    {
        if (driver == null) driver = GetComponentInParent<MarioMindDriver>();
        mario = GetComponentInParent<MarioController>();
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true; line.loop = true;
        line.startWidth = line.endWidth = 0.04f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.sortingOrder = 150;
        var fillGo = new GameObject("VisionConeFill");
        fillGo.transform.SetParent(transform, false);
        fillMesh = new Mesh();
        fillGo.AddComponent<MeshFilter>().sharedMesh = fillMesh;
        fillMr = fillGo.AddComponent<MeshRenderer>();
        fillMr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        fillMr.sortingOrder = 149;
        fv = new Vector3[rays + 2]; ftri = new int[rays * 3]; fcol = new Color[rays + 2]; clear = new float[rays + 1];
        for (int i = 0; i < rays; i++) { ftri[i * 3] = 0; ftri[i * 3 + 1] = i + 2; ftri[i * 3 + 2] = i + 1; }
    }

    private void OnDestroy() { if (fillMesh != null) Destroy(fillMesh); if (fillMr != null && fillMr.sharedMaterial != null) Destroy(fillMr.sharedMaterial); }

    private void LateUpdate()
    {
        if (driver == null || mario == null || driver.Tuning == null) return;
        var t = driver.Tuning;
        line.enabled = t.showVisionCone;
        if (!line.enabled) { if (fillMesh != null) fillMesh.Clear(); return; }
        Vector2 eye = MarioVision.EyeOf(mario.transform, t);
        float baseAngle = mario.IsFacingRight ? 0f : 180f;
        line.positionCount = rays + 2;
        line.SetPosition(0, eye);
        for (int i = 0; i <= rays; i++)
        {
            float a = (baseAngle - t.visionHalfAngle + 2f * t.visionHalfAngle * i / rays) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            clear[i] = ClearDistance(eye, dir, t.visionRange);
            line.SetPosition(i + 1, eye + dir * clear[i]);
        }
        DrawFill(eye, baseAng: baseAngle, t);
        Color c = ColorFor(driver.Mind != null ? driver.Mind.Meter.Level : SuspicionLevel.Calm);
        line.startColor = line.endColor = c;
    }

    private void DrawFill(Vector2 eye, float baseAng, MarioMindTuningSO t)
    {
        if (fillMesh == null) return;
        var meter = driver.Mind != null ? driver.Mind.Meter : null;
        if (!t.visionConeFill || meter == null || meter.Level == SuspicionLevel.Calm) { fillMesh.Clear(); return; }
        // 回到 local（fill 物体挂在本物体下面）
        Vector3 Local(Vector2 w) => transform.InverseTransformPoint(w);
        fv[0] = Local(eye);
        Color c = Step1Readability.FillColor(meter.Level);
        for (int i = 0; i <= rays; i++)
        {
            float a = (baseAng - t.visionHalfAngle + 2f * t.visionHalfAngle * i / rays) * Mathf.Deg2Rad;
            float len = Step1Readability.FillReach(meter.Normalized, clear[i], t.visionRange);
            fv[i + 1] = Local(eye + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len);
        }
        for (int i = 0; i < fcol.Length; i++) fcol[i] = c;
        fillMesh.Clear(); fillMesh.vertices = fv; fillMesh.triangles = ftri; fillMesh.colors = fcol;
    }

    private static readonly RaycastHit2D[] s_hits = new RaycastHit2D[32];
    private static readonly ContactFilter2D s_filter = MakeFilter();
    private static ContactFilter2D MakeFilter() { var f = new ContactFilter2D(); f.NoFilter(); return f; }

    private static float ClearDistance(Vector2 eye, Vector2 dir, float range)
    {
        float best = range;
        // S192 性能：非分配射线（原 RaycastAll 每帧 15 次新建数组）
        int count = Physics2D.Raycast(eye, dir, s_filter, s_hits, range);
        for (int i = 0; i < count; i++)
        {
            var hit = s_hits[i];
            var c = hit.collider;
            if (c == null || MarioSuspicionTracker.IsOneWayPlatform(c)) continue;
            if (c.isTrigger && !SightBlocker.Blocks(c, eye)) continue; // 草丛挡视线（S187）
            if (c.GetComponentInParent<MarioController>() != null || c.GetComponentInParent<TricksterController>() != null) continue;
            if (hit.distance < best) best = hit.distance;
        }
        return best;
    }

    private static Color ColorFor(SuspicionLevel level)
    {
        switch (level)
        {
            case SuspicionLevel.Alert: return new Color(1f, 0.2f, 0.2f, 0.7f);
            case SuspicionLevel.Curious: return new Color(1f, 0.9f, 0.2f, 0.6f);
            default: return new Color(1f, 1f, 1f, 0.25f);
        }
    }
}
