using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// S203：马里奥性格（宪法第 2 步"重玩变化来自马里奥的性格/目标/学习状态，不来自地图平移"）。
/// 每回合按种子抽一种性格（权重可调、可复现），开局头顶亮出来（H6：你一眼知道这局要怎么坑他）。
///
/// | 性格 | 赶路 | 被坑过的地方 | 道具箱 | 起疑 | 你该怎么坑 |
/// |---|---|---|---|---|---|
/// | 冲冲型 Rush（原来的） | 正常速 | 路过时放慢（S200） | 同层 4 格内顺路捡 | 正常 | 什么都行 |
/// | 谨慎型 Cautious | 稍慢 | **绕开**（寻路把被坑点当禁区；绕不开才硬走，H1/H10） | 不绕路捡 | 更容易起疑 | 同一个坑用不了第二次 → 换招、布新连锁；他绕路时走的是**另一条路**，那边也要有坑 |
/// | 贪财型 Greedy | 正常速 | 不长记性（不放慢） | **一定去抢**：看得见的箱子不限距离、跨层也去（能走到才去，3 秒够不着放弃） | 抢道具时不太起疑 | 在道具箱旁边布连锁 = 必中；但他抢到护盾/加速/透视会反过来克你 |
///
/// 规则：H4——性格只改他**自己**怎么走、怎么反应；感知仍然只有看/听/记。H10——谨慎型绕不开就走原路；贪财型有放弃计时。
/// 参考：吃豆人四只鬼各有性格（追击/埋伏/多变/随性），让玩家要"读"对手而不是背路线（Toru Iwatani）——只借规则。
/// </summary>
public enum MarioPersonalityKind { Rush, Cautious, Greedy }

public static class MarioPersonality
{
    public struct Traits
    {
        public float speedScale;          // 赶路速度倍率（乘在 marioSpeedScale 上）
        public bool avoidHurtSpots;       // 寻路绕开被坑点
        public bool slowNearHurtSpots;    // 路过被坑点放慢
        public float pickupDetour;        // 道具箱绕路距离（格）；< 0 = 不捡
        public bool pickupAnyFloor;       // 跨层也去捡
        public float suspicionScale;      // 起疑速度倍率
        public string zh, en, badge;
    }

    /// <summary>纯逻辑：按权重和种子抽性格（可复现）。权重全为 0 → 冲冲型。</summary>
    public static MarioPersonalityKind Roll(int seed, float rushWeight, float cautiousWeight, float greedyWeight)
    {
        float a = Mathf.Max(0f, rushWeight), b = Mathf.Max(0f, cautiousWeight), c = Mathf.Max(0f, greedyWeight);
        float sum = a + b + c;
        if (sum <= 0f) return MarioPersonalityKind.Rush;
        float r = (float)new System.Random(unchecked(seed * 104729 + 3)).NextDouble() * sum;
        if (r < a) return MarioPersonalityKind.Rush;
        if (r < a + b) return MarioPersonalityKind.Cautious;
        return MarioPersonalityKind.Greedy;
    }

    public static Traits For(MarioPersonalityKind k, MarioMindTuningSO t)
    {
        switch (k)
        {
            case MarioPersonalityKind.Cautious:
                return new Traits { speedScale = t.cautiousTypeSpeedScale, avoidHurtSpots = true, slowNearHurtSpots = true, pickupDetour = -1f, pickupAnyFloor = false, suspicionScale = t.cautiousTypeSuspicionScale, zh = "谨慎型", en = "CAUTIOUS", badge = "🛡" };
            case MarioPersonalityKind.Greedy:
                return new Traits { speedScale = 1f, avoidHurtSpots = false, slowNearHurtSpots = false, pickupDetour = t.greedyPickupDetourCells, pickupAnyFloor = true, suspicionScale = t.greedySuspicionScale, zh = "贪财型", en = "GREEDY", badge = "💰" };
            default:
                return new Traits { speedScale = 1f, avoidHurtSpots = false, slowNearHurtSpots = true, pickupDetour = t.pickupDetourCells, pickupAnyFloor = false, suspicionScale = 1f, zh = "冲冲型", en = "RUSH", badge = "⚡" };
        }
    }

    /// <summary>谨慎型的路点（见 DetourPlanner）。</summary>
    public static Vector2? DetourWaypoint(string[] rows, Vector2 from, Vector2 target, IReadOnlyList<Vector2> hurtSpots, float radius, out bool detoured)
    {
        var w = DetourPlanner.Detour(rows, Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.y), Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.y), from.x, ToCells(hurtSpots), radius, out detoured);
        if (detoured && w.x >= 0) return new Vector2(w.x, w.y);
        return MarioMindDriver.PlanWaypoint(rows, from, target);
    }

    /// <summary>纯逻辑：绕不开时"跳过去"——被坑点就在前面 0.6..1.8 格、同一高度 → 现在起跳。</summary>
    public static bool ShouldHop(IReadOnlyList<Vector2> spots, Vector2 mario, bool facingRight)
    {
        if (spots == null) return false;
        foreach (var s in spots)
        {
            float dx = (s.x - mario.x) * (facingRight ? 1f : -1f);
            if (dx >= 0.6f && dx <= 1.8f && Mathf.Abs(s.y - mario.y) <= 0.6f) return true;
        }
        return false;
    }

    public static List<(int x, int y)> ToCells(IReadOnlyList<Vector2> spots)
    {
        var l = new List<(int, int)>();
        if (spots != null) foreach (var s in spots) l.Add((Mathf.RoundToInt(s.x), Mathf.RoundToInt(s.y)));
        return l;
    }
}
