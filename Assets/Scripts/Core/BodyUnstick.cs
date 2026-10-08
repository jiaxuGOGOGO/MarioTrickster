using UnityEngine;

/// <summary>
/// S209：角色"嵌进墙里"自动推出来（捣蛋者 + 马里奥共用）。
/// 用户截图：捣蛋者跳到墙边伪装 → 伪装形态的碰撞体变大（0.8 → 1.2 格）直接长进了墙里 →
/// 脚下检测把"墙里下一格方块的顶面"当成了地面 → 悬在半空不掉。
/// 同样会让身体变大的还有：缩小术恢复原大、通风管/附身传送、回合出生点。
/// 规则：每个物理帧检查身体是否和实心地形（Ground 层、非触发器、非单向台面、非会动的刚体）重叠超过 minDepth，
///       有就沿最短方向推出去（每帧最多 maxStep），并清掉朝墙的速度。纯物理修正，不读任何对手信息（H4）。
/// </summary>
public static class BodyUnstick
{
    private static readonly Collider2D[] s_hits = new Collider2D[8];

    /// <summary>S235：当前房间大小（格；Step1RoomGuard 开局写入，zero = 不限制）。推出墙之后把身体夹回房间里——
    /// 以前按"最短方向"推：被炮/炸弹/伪装变大挤进最外圈墙时，最短方向可能是往外 → 被推到房间外面回不来（用户实测）。</summary>
    public static Vector2Int RoomSize;

    /// <summary>纯逻辑：重叠深度（distance 为负）超过 minDepth 才推，推的距离不超过 maxStep。返回位移（0 = 不推）。</summary>
    public static Vector2 PushFor(bool overlapped, Vector2 normal, float distance, float minDepth, float maxStep)
    {
        if (!overlapped || distance > -minDepth) return Vector2.zero;
        float d = Mathf.Min(-distance + 0.005f, maxStep);
        return -normal * d; // normal 从身体指向墙；往反方向推
    }

    /// <summary>把 body 从重叠的实心地形里推出来；返回总位移（没重叠 = zero）。</summary>
    public static Vector2 Resolve(Rigidbody2D rb, Collider2D body, LayerMask ground, float minDepth = 0.02f, float maxStep = 0.5f)
    {
        if (rb == null || body == null || !body.enabled) return Vector2.zero;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(ground);
        int n = body.OverlapCollider(filter, s_hits);
        Vector2 total = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            var c = s_hits[i];
            if (c == null || c == body || c.isTrigger) continue;
            if (c.attachedRigidbody != null && (c.attachedRigidbody == rb || c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)) continue;
            var eff = c.GetComponent<PlatformEffector2D>();
            if (c.usedByEffector && eff != null && eff.enabled && eff.useOneWay) continue; // 单向台面允许穿过
            if (Physics2D.GetIgnoreCollision(body, c)) continue;
            var d = body.Distance(c);
            if (!d.isValid) continue;
            var push = PushFor(d.isOverlapped, d.normal, d.distance, minDepth, maxStep);
            if (push == Vector2.zero) continue;
            total += push;
        }
        if (total == Vector2.zero) return total;
        if (total.magnitude > maxStep) total = total.normalized * maxStep;
        if (RoomSize.x > 2 && RoomSize.y > 2) // S235：只往房间里推（外圈墙另一边是房间外）
        {
            var b = body.bounds; Vector2 half = new Vector2(b.extents.x, b.extents.y), centerOff = (Vector2)b.center - rb.position;
            Vector2 c = Step1Bounds.ClampInside(rb.position + centerOff + total, RoomSize.x, RoomSize.y, half);
            total = c - centerOff - rb.position;
        }
        rb.position += total;
        rb.transform.position = rb.position;
        var v = rb.velocity;
        if (total.x != 0f && Mathf.Sign(v.x) != Mathf.Sign(total.x)) v.x = 0f;
        if (total.y != 0f && Mathf.Sign(v.y) != Mathf.Sign(total.y)) v.y = 0f;
        rb.velocity = v;
        return total;
    }
}
