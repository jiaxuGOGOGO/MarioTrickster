using UnityEngine;

/// <summary>
/// 把马里奥的视锥画出来（玩家可读的"他在看哪里"）。只画几何视锥 + 被墙挡住的截断，
/// 与 MarioVision 判定用同一组调参。纯表现。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MarioVisionConeView : MonoBehaviour
{
    [SerializeField] private MarioMindDriver driver;
    [SerializeField] private int rays = 14;

    private LineRenderer line;
    private MarioController mario;

    private void Start()
    {
        if (driver == null) driver = GetComponentInParent<MarioMindDriver>();
        mario = GetComponentInParent<MarioController>();
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true; line.loop = true;
        line.startWidth = line.endWidth = 0.04f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.sortingOrder = 150;
    }

    private void LateUpdate()
    {
        if (driver == null || mario == null || driver.Tuning == null) return;
        var t = driver.Tuning;
        line.enabled = t.showVisionCone;
        if (!line.enabled) return;
        Vector2 eye = MarioVision.EyeOf(mario.transform, t);
        float baseAngle = mario.IsFacingRight ? 0f : 180f;
        line.positionCount = rays + 2;
        line.SetPosition(0, eye);
        for (int i = 0; i <= rays; i++)
        {
            float a = (baseAngle - t.visionHalfAngle + 2f * t.visionHalfAngle * i / rays) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            line.SetPosition(i + 1, eye + dir * ClearDistance(eye, dir, t.visionRange));
        }
        Color c = ColorFor(driver.Mind != null ? driver.Mind.Meter.Level : SuspicionLevel.Calm);
        line.startColor = line.endColor = c;
    }

    private static float ClearDistance(Vector2 eye, Vector2 dir, float range)
    {
        float best = range;
        foreach (var hit in Physics2D.RaycastAll(eye, dir, range))
        {
            var c = hit.collider;
            if (c == null || c.isTrigger || MarioSuspicionTracker.IsOneWayPlatform(c)) continue;
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
