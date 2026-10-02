using System.Collections.Generic;

/// <summary>
/// S210：大地图的一天（跨场景保存，纯逻辑，沙盒可测）。进房间前记下"哪扇门、怎么进的"，房间打完把结果写回来，回到大地图接着走。
/// 只在内存里（关掉游戏就重新开始一天），不写存档——第 1 步先验证好不好玩。
/// </summary>
public static class OverworldSession
{
    public enum DoorResult { NotYet, Defended, Looted, Missed }

    public static bool Active;
    public static string MapName = "";
    public static string TownScene = "";
    public static double Minute = OverworldMap.DayStart;
    public static readonly Dictionary<int, DoorResult> Results = new Dictionary<int, DoorResult>();
    public static readonly Dictionary<int, string> RoomScenes = new Dictionary<int, string>();
    public static double MarioX, MarioY, TricksterX, TricksterY;
    public static bool HasPositions;
    public static int NextStop;
    public static int PendingDoor;
    public static OverworldMind.DoorOutcome PendingOutcome;
    public static int BonusBombs;
    public static float CarriedSuspicion;
    public static int Caught, TauntsUsed;
    public static float DelayedSeconds;
    /// <summary>今天已经用掉的香蕉皮 / 捡过的道具箱（格子编号 y*W+x）。</summary>
    public static readonly HashSet<int> UsedCells = new HashSet<int>();
    public static bool DayOver;
    // ── S218：大机关 / 天气 / 小镇 ↔ 房间联动 ──
    /// <summary>第几天（同一张小镇按 R 开新的一天 +1；天气由 地图名 + 天数 决定，可复现）。</summary>
    public static int Day = 1;
    /// <summary>今天被大机关改掉的格子（格子编号 → 新字符）。回到小镇场景时重新套上；只活一天。</summary>
    public static readonly Dictionary<int, char> Changed = new Dictionary<int, char>();
    /// <summary>马里奥吃过亏的大机关种类（他自己的经历，H4）。跨天保留，重新进 Play 才忘。</summary>
    public static readonly HashSet<char> MarioWary = new HashSet<char>();
    /// <summary>他上一次被大机关砸中的游戏分钟（进门时还晕着 → 房间开局多等几秒）。</summary>
    public static double LastBigHitMinute = -9999;
    public static float CarriedDaze;
    /// <summary>刚守住的门（回到小镇时，离它最近的一个用过的大机关重新装填）。</summary>
    public static int ReloadDoor;
    public static int BigHits, BestChain;
    /// <summary>S228：他在房间里挨了你的炮 → 回小镇出门时被轰出窗户（门口往外几格，落地晕）。用过就清。</summary>
    public static int WindowFlingDoor;
    public static int WindowFlings, BellRings;
    /// <summary>S219：每门巨炮现在的瞄准（格子编号 → 方向×100 + 距离）。你瞄好了下车，被连锁震响时就打那里；当天有效。</summary>
    public static readonly Dictionary<int, int> CannonAim = new Dictionary<int, int>();
    public static int CannonRides, Lightnings, Mudslides, CaveHops;
    // ── S220：心 / 能量 ──
    public const int MaxHearts = 3, MaxEnergy = 3;
    /// <summary>小镇里的心（马里奥 / 你各 3 颗）。被劈 / 砸 / 冲到掉 1 颗；掉光 = 晕倒几秒、剩 1 颗站起来（一天不会因此结束）。进房间带进去，房间打完回满。</summary>
    public static int MarioHearts = MaxHearts, YouHearts = MaxHearts;
    /// <summary>你的能量（0–3）：捡 * +1，你的机关让他掉心 +1。满了按 Q 召唤雷云。</summary>
    public static int Energy;
    public static int MarioHeartsLost, YouHeartsLost, Kos, Clouds, StormBolts;

    /// <summary>每次进入 Play 都重置（Unity 关了域重载时静态值会留着）。</summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics() { Active = false; MarioWary.Clear(); NewDay("", ""); }

    /// <summary>S224：今天他差点发现你的时刻（一天结束列最接近的 3 次）。</summary>
    public static readonly NearMissLog NearMiss = new NearMissLog();

    public static void NewDay(string map, string town, int day = 1)
    {
        NearMiss.Clear();
        Day = System.Math.Max(1, day); Changed.Clear(); CannonAim.Clear(); CannonRides = Lightnings = Mudslides = CaveHops = 0; MarioHearts = YouHearts = MaxHearts; Energy = 0; MarioHeartsLost = YouHeartsLost = Kos = Clouds = StormBolts = 0; LastBigHitMinute = -9999; CarriedDaze = 0f; ReloadDoor = 0; BigHits = 0; BestChain = 0; WindowFlingDoor = 0; WindowFlings = 0; BellRings = 0;
        MapName = map ?? ""; TownScene = town ?? ""; Minute = OverworldMap.DayStart;
        Results.Clear(); UsedCells.Clear(); DayOver = false; HasPositions = false; NextStop = 0; PendingDoor = 0; BonusBombs = 0; CarriedSuspicion = 0f; Caught = 0; TauntsUsed = 0; DelayedSeconds = 0f;
    }

    /// <summary>房间结束：你赢（时间到 / 马里奥被打倒）= 守住；他赢 = 被偷。</summary>
    public static void RecordRoom(int door, bool playerWon)
    {
        if (door <= 0) return;
        Results[door] = playerWon ? DoorResult.Defended : DoorResult.Looted;
        PendingDoor = 0; BonusBombs = 0; CarriedSuspicion = 0f; CarriedDaze = 0f;
        MarioHearts = YouHearts = MaxHearts; // S220：房间打完两人都回满心（房间里有自己的血量）
        if (playerWon) ReloadDoor = door; // S218：守住一户 → 回到小镇时那户旁边的大机关重新装填
    }

    /// <summary>S228：房间里你的炮打中过他 → 记下这扇门，回到小镇时他从这扇门被轰出来。</summary>
    public static void RecordWindowFling(int door) { if (door > 0) WindowFlingDoor = door; }

    public static void RecordMissed(int door) { if (door > 0) Results[door] = DoorResult.Missed; }

    public static DoorResult ResultOf(int door) => Results.TryGetValue(door, out var r) ? r : DoorResult.NotYet;

    public static int Count(DoorResult r) { int n = 0; foreach (var kv in Results) if (kv.Value == r) n++; return n; }

    /// <summary>一天的评价：守住的门 ≥ 一半 = 你赢这一天。</summary>
    public static bool DayWon(int doorCount) => doorCount > 0 && Count(DoorResult.Defended) * 2 >= doorCount;

    public static string Summary(int doorCount) => Step1Text.OverworldDaySummary(Count(DoorResult.Defended), Count(DoorResult.Looted), Count(DoorResult.Missed), doorCount, Caught, DelayedSeconds, DayWon(doorCount))
        + Step1Text.NearMissLines(NearMiss.Closest(3), NearMiss.NearMisses); // S224：SpyParty 式"事后告诉你他怀疑过你几次"
}

/// <summary>S220：小镇的心带进横版房间（纯逻辑，沙盒可测）。马里奥至少带 overworldRoomHeartFloor 颗（小镇被打得再惨，房间里也要能打）；你的心 = 房间命数（至少 1）。</summary>
public static class OverworldRoomCarry
{
    public static int MarioRoomHearts(MarioMindTuningSO t, int roomMax)
    {
        if (!OverworldSession.Active && OverworldSession.MarioHearts >= OverworldSession.MaxHearts) return roomMax;
        int floor = System.Math.Max(1, t != null ? t.overworldRoomHeartFloor : 2);
        return System.Math.Max(1, System.Math.Min(roomMax, System.Math.Max(floor, OverworldSession.MarioHearts)));
    }
    public static int TricksterRoomLives(int roomMax) => System.Math.Max(1, System.Math.Min(roomMax, OverworldSession.YouHearts));
}
