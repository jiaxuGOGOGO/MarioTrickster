/// <summary>S210：大地图（星露谷视角小镇）上所有玩家可见文字。</summary>
public static partial class Step1Text
{
    public const string OverworldHelp =
        "大地图 · 小镇  Town map\n" +
        "马里奥今天按时间表去几户人家偷宝贝（门上的数字 = 顺序，右上角有时间）。\n" +
        "方向键/WASD 走路   P 伪装成木箱（伪装时被看见还在动 = 可疑）\n" +
        "L 靠近地上的香蕉皮（3 格内）让它变滑：闪一下后滑 3 秒，他踩上去晕 1.5 秒   T 挑衅（把他引过来，每天 3 次）\n" +
        "E 在门口按：比他先到 = 埋伏（进屋开打，还多 1 枚炸弹给准备）\n" +
        "他先进门：6 秒内跟进去 = 迟到（照样打，但他不等你）；再晚 = 这户被偷\n" +
        "躲进高草/房子后面他就看不见；晚上 19:00 后他看得近，路灯下除外\n" +
        "? 木盒：捡到 +1 枚炸弹，带进下一个房间   H 关闭/打开说明";

    public static string OverworldClock(string clock, bool night) => night ? $"🌙 {clock}" : $"☀ {clock}";
    public static string OverworldNextDoor(int n, string clock) => $"下一站：门 {n}  {clock}\nNext: door {n}";
    public const string OverworldGoingHome = "他要回家了\nHeading home";
    public const string OverworldAmbushHint = "按 E 进屋埋伏！\nPress E to ambush";
    public const string OverworldLateHint = "他刚进去了！快按 E 跟进\nHe just went in — press E!";
    public const string OverworldTooEarly = "这扇门今天不是他下一站\nNot his next stop";
    public const string OverworldCaught = "被马里奥抓住了！送回出生点\nCaught! Back to start";
    public const string OverworldMissed = "他在里面安心偷完了——这户被偷\nToo late, that house got robbed";
    public const string OverworldPickup = "捡到炸弹 +1（下一个房间用）\n+1 bomb for next room";
    public const string OverworldPeel = "香蕉皮变滑了（3 秒）\nPeel armed (3s)";
    public const string OverworldPeelNo = "附近 3 格内没有能用的香蕉皮\nNo peel within 3";
    public const string OverworldTauntNone = "今天挑衅用完了\nNo taunts left";
    public const string OverworldRoomMissing = "这个房间还没构建：请用菜单 MarioTrickster/Overworld/▶ Play Town 启动\nRoom scene not built";
    // S211：切换黑幕上的标题卡
    public static string OverworldTransitToRoom(int door, string room, OverworldMind.DoorOutcome o) =>
        $"门 {door} · {room}\n" + (o == OverworldMind.DoorOutcome.Ambush ? "埋伏成功！Ambush!" : o == OverworldMind.DoorOutcome.Late ? "迟到了——他不等你 Late!" : "");
    public static string OverworldTransitToTown(string clock) => $"回到小镇  {clock}\nBack to town";
    public static string OverworldTransitNewDay(string town) => $"{town}\n新的一天 06:00  New day";
    public const string OverworldBackToTown = "按 Enter 回到小镇\nPress Enter to return to town";

    public static string OverworldDoorLabel(int n, string clock, OverworldSession.DoorResult r)
    {
        switch (r)
        {
            case OverworldSession.DoorResult.Defended: return $"{n} ✓守住";
            case OverworldSession.DoorResult.Looted: return $"{n} ✗被偷";
            case OverworldSession.DoorResult.Missed: return $"{n} ✗没赶上";
            default: return $"{n} {clock}";
        }
    }

    public static string OverworldDaySummary(int defended, int looted, int missed, int total, int caught, float delayedSeconds, bool won) =>
        (won ? "一天结束 —— 你守住了小镇！\nDay over — YOU WIN!\n" : "一天结束 —— 马里奥满载而归\nDay over — Mario wins\n") +
        $"守住 {defended} / 被偷 {looted} / 没赶上 {missed}（共 {total} 户）\n" +
        $"在镇上被抓 {caught} 次，把他拖住了 {delayedSeconds:0} 秒\n按 R 再来一天";
}
