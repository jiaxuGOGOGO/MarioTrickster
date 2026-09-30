using UnityEngine;

/// <summary>
/// 设计宪法 H4：马里奥唯一的"看"。视锥（朝向 + 半角）+ 距离 + 遮挡，三者同时满足才算看见。
/// 贴身 nearSenseRadius 内不看朝向（仍需无遮挡）。纯函数，不接触任何捣蛋者状态。
/// </summary>
public static class MarioVision
{
    /// <summary>几何视锥判定（不含遮挡），可在 EditMode 直接测试。</summary>
    public static bool InCone(Vector2 eye, bool facingRight, Vector2 target, float range, float halfAngleDeg, float nearRadius)
    {
        Vector2 d = target - eye;
        float dist = d.magnitude;
        if (float.IsNaN(dist) || dist > range) return false;
        if (dist <= nearRadius) return true;
        Vector2 forward = facingRight ? Vector2.right : Vector2.left;
        return Vector2.Angle(forward, d) <= halfAngleDeg;
    }

    /// <summary>视锥 && 无遮挡（遮挡规则复用第 0 步的 MarioSuspicionTracker.CanWitness）。</summary>
    public static bool CanSee(Vector2 eye, bool facingRight, Vector2 target, Transform targetRoot, MarioMindTuningSO t)
    {
        if (!InCone(eye, facingRight, target, t.visionRange, t.visionHalfAngle, t.nearSenseRadius)) return false;
        return MarioSuspicionTracker.CanWitness(eye, target, t.visionRange, targetRoot);
    }

    public static Vector2 EyeOf(Transform mario, MarioMindTuningSO t) => (Vector2)mario.position + Vector2.up * t.eyeHeight;
}
