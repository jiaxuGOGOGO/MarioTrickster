using System;

// 仅供本目录.NET harness编译OverworldMap的未调用旧地图审计分支；不参与Unity工程或运行时。
public static class OverworldProps
{
    public static int Height(char cell) { return cell == '^' ? 1 : 0; }
    public static void CheckCounts(OverworldMap.Map map, Action<string, int, int> error, Action<string, int, int> warning) { }
    public static void CheckReach(OverworldMap.Map map, OverworldMap.Cell home, Action<string, int, int> error, Action<string, int, int> warning) { }
    public static void CheckMountains(OverworldMap.Map map, Action<string, int, int> warning) { }
}

public static class OverworldStorm
{
    public static void Check(OverworldMap.Map map, Action<string, int, int> error, Action<string, int, int> warning, Action<string, int, int> info) { }
}
