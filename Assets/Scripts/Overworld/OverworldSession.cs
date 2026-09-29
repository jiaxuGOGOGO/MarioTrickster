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

    /// <summary>每次进入 Play 都重置（Unity 关了域重载时静态值会留着）。</summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetStatics() { Active = false; NewDay("", ""); }

    public static void NewDay(string map, string town)
    {
        MapName = map ?? ""; TownScene = town ?? ""; Minute = OverworldMap.DayStart;
        Results.Clear(); UsedCells.Clear(); DayOver = false; HasPositions = false; NextStop = 0; PendingDoor = 0; BonusBombs = 0; CarriedSuspicion = 0f; Caught = 0; TauntsUsed = 0; DelayedSeconds = 0f;
    }

    /// <summary>房间结束：你赢（时间到 / 马里奥被打倒）= 守住；他赢 = 被偷。</summary>
    public static void RecordRoom(int door, bool playerWon)
    {
        if (door <= 0) return;
        Results[door] = playerWon ? DoorResult.Defended : DoorResult.Looted;
        PendingDoor = 0; BonusBombs = 0; CarriedSuspicion = 0f;
    }

    public static void RecordMissed(int door) { if (door > 0) Results[door] = DoorResult.Missed; }

    public static DoorResult ResultOf(int door) => Results.TryGetValue(door, out var r) ? r : DoorResult.NotYet;

    public static int Count(DoorResult r) { int n = 0; foreach (var kv in Results) if (kv.Value == r) n++; return n; }

    /// <summary>一天的评价：守住的门 ≥ 一半 = 你赢这一天。</summary>
    public static bool DayWon(int doorCount) => doorCount > 0 && Count(DoorResult.Defended) * 2 >= doorCount;

    public static string Summary(int doorCount) => Step1Text.OverworldDaySummary(Count(DoorResult.Defended), Count(DoorResult.Looted), Count(DoorResult.Missed), doorCount, Caught, DelayedSeconds, DayWon(doorCount));
}
