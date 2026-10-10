using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S235：房间边界纯逻辑（沙盒可验证）。用户实测："捣蛋者掉出游戏范围之外就回不来了，血也没掉，马里奥还在继续"。
///
/// 为什么会掉出去（审计）：
///   1. 房间底下的"深渊"（KillZone）只认 PlayerHealth —— 只有马里奥有，**捣蛋者没有** → 掉下去什么都不发生；而且只管往下掉，左右/上面出去完全没人管；
///   2. BodyUnstick（S209 身体嵌进墙就推出来）按"最短方向"推：人被大炮/炸弹/伪装变大挤进**最外圈墙**时，最短方向可能是**往外** → 被推到房间外；
///   3. 搭建范围检查只要求外圈"实心"，而单向台面 -（能从下面跳穿）、塌桥 C、裂缝地板 x、箱子 c、裂墙 % 等也算实心 → 外圈可能是个会塌/会碎/能穿的口子；
///   4. 房间镜头只在房间里移动 → 掉出去后屏幕上看不见你，也没有任何提示。
///
/// 修法（三道保险，H9 "无卡死"）：
///   A. 预防：构建房间时在外圈外面再围一圈**看不见的墙**（GuardRects），推出墙只会往房间里推（ClampInside）；
///   B. 兜底：每帧检查（IsOut），你出界 = 回出生点 + 掉 fallOutLivesLost 条命（无敌期内不掉；命没了本局马里奥赢，结算写清"你掉出房间"）；
///      马里奥出界 = 放回他的路线上（同卡住救援），不改胜负；
///   C. 提示：工坊检查外圈有"会塌/能穿/能炸"的格子 → 黄色提醒（BorderLeaks）。
/// 只用双方真实位置做裁判（同 TricksterLives），不给马里奥任何信息（H4）。
/// </summary>
public static class Step1Bounds
{
    /// <summary>外圈只算这几种"永远不会开口"的格子：墙 W、地面 #、平台 =。</summary>
    public const string SafeBorder = "W#=";

    /// <summary>判定出界的余量（格）：身体中心越过外圈墙的中线才算出界（正常站在墙边时中心离墙中线 ≥0.9 格，不会误判）。</summary>
    public const float OutMargin = 0.5f;

    /// <summary>看不见的护墙厚度（格）。</summary>
    public const float GuardThickness = 3f;

    /// <summary>房间内部（不含外圈墙）的范围：格子中心在整数坐标，外圈墙中心 x=0 / x=w-1 / y=0 / y=h-1。</summary>
    public static void Interior(int w, int h, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = 0.5f; maxX = w - 1.5f; minY = 0.5f; maxY = h - 1.5f;
    }

    /// <summary>这个点（身体中心）是不是已经在房间外（越过外圈墙中线）。w/h ≤ 2 = 没有房间信息 → 永不判出界。</summary>
    public static bool IsOut(Vector2 p, int w, int h, float margin = OutMargin)
    {
        if (w <= 2 || h <= 2) return false;
        Interior(w, h, out float x0, out float x1, out float y0, out float y1);
        return p.x < x0 - margin || p.x > x1 + margin || p.y < y0 - margin || p.y > y1 + margin;
    }

    /// <summary>把身体中心夹回房间内部（half = 身体半宽/半高；房间太小时取中间）。BodyUnstick 推完后调用：只会往里推，不会往外推。</summary>
    public static Vector2 ClampInside(Vector2 p, int w, int h, Vector2 half)
    {
        if (w <= 2 || h <= 2) return p;
        Interior(w, h, out float x0, out float x1, out float y0, out float y1);
        float lx = x0 + half.x, hx = x1 - half.x, ly = y0 + half.y, hy = y1 - half.y;
        float x = lx > hx ? (x0 + x1) * 0.5f : Mathf.Clamp(p.x, lx, hx);
        float y = ly > hy ? (y0 + y1) * 0.5f : Mathf.Clamp(p.y, ly, hy);
        return new Vector2(x, y);
    }

    /// <summary>外圈外面四条看不见的墙（中心 x, y, 宽, 高）。紧贴外圈墙的外沿（-0.5 / w-0.5），四角重叠，不留缝。</summary>
    public static List<float[]> GuardRects(int w, int h, float t = GuardThickness)
    {
        var l = new List<float[]>();
        if (w <= 0 || h <= 0) return l;
        float left = -0.5f, right = w - 0.5f, bottom = -0.5f, top = h - 0.5f, fullW = w + 2f * t, cx = (left + right) * 0.5f, cy = (bottom + top) * 0.5f, fullH = h + 2f * t;
        l.Add(new[] { left - t * 0.5f, cy, t, fullH });   // 左
        l.Add(new[] { right + t * 0.5f, cy, t, fullH });  // 右
        l.Add(new[] { cx, bottom - t * 0.5f, fullW, t }); // 下
        l.Add(new[] { cx, top + t * 0.5f, fullW, t });    // 上
        return l;
    }

    /// <summary>纯逻辑：点 p 是否被护墙挡住（在某条护墙里）。测试用：房间外一圈的点都应该在护墙里。</summary>
    public static bool InGuard(Vector2 p, int w, int h, float t = GuardThickness)
    {
        foreach (var r in GuardRects(w, h, t))
            if (System.Math.Abs(p.x - r[0]) <= r[2] * 0.5f && System.Math.Abs(p.y - r[1]) <= r[3] * 0.5f) return true;
        return false;
    }

    /// <summary>工坊提醒：外圈上"会塌 / 能穿 / 能炸 / 能推开"的格子（x, y 世界格坐标，y=0 在最下面）。实心但不在 SafeBorder 里的都算。</summary>
    public static List<(int x, int y, char c)> BorderLeaks(IList<string> grid)
    {
        var l = new List<(int, int, char)>();
        if (grid == null || grid.Count == 0) return l;
        int h = grid.Count;
        for (int row = 0; row < h; row++)
        {
            string r = grid[row]; int y = h - 1 - row;
            for (int x = 0; x < r.Length; x++)
            {
                bool edge = row == 0 || row == h - 1 || x == 0 || x == r.Length - 1;
                if (edge && SafeBorder.IndexOf(r[x]) < 0) l.Add((x, y, r[x]));
            }
        }
        return l;
    }

    /// <summary>S235：被控住的人（绳套 / 铁笼 / 炮里装填）离控制点超过 reach + 1.5 格 = 被传送走了（回出生点 / 救援）→ 应该放人，不能拽回来。</summary>
    public static bool Teleported(Vector2 who, Vector2 holdAt, float reach) => (who - holdAt).sqrMagnitude > (reach + 1.5f) * (reach + 1.5f);

    /// <summary>出界时掉几条命（无敌期内 0；剩的命不够就掉到 0）。</summary>
    public static int LivesLost(int lives, int perFall, bool invulnerable) => invulnerable || perFall <= 0 || lives <= 0 ? 0 : System.Math.Min(lives, perFall);
}
