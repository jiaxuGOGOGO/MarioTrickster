/// <summary>S210：大地图（星露谷视角小镇）上所有玩家可见文字。</summary>
public static partial class Step1Text
{
    public const string OverworldHelp =
        "大地图 · 小镇  Town map\n" +
        "马里奥今天按时间表去几户人家偷宝贝（门上的数字 = 顺序，右上角有时间）。\n" +
        "方向键/WASD 走路   P 伪装成木箱（伪装时被看见还在动 = 可疑）\n" +
        "L 靠近地上的香蕉皮（3 格内）让它变滑：闪一下后滑 3 秒，他踩上去晕 1.5 秒   T 挑衅（把他引过来，每天 3 次）\n" +
        "E 埋伏：先到门口躲好（高草 / P 伪装），等他走到离门 8 格内再按 E（进屋开打，还多 1 枚炸弹）；按住空格快进等他\n" +
        "他先进门：6 秒内跟进去 = 迟到（照样打，但他不等你）；再晚 = 这户被偷\n" +
        "躲进高草/房子后面他就看不见；晚上 19:00 后他看得近，路灯下除外\n" +
        "? 木盒：捡到 +1 枚炸弹，带进下一个房间\n" +
        "大机关（L 发动，每天一次，先闪 1.2 秒再动）：巨炮 K 把炮口里的人轰到靶心 X；滚石 O 一路撞碎木箱栅栏；水塔 U 把路口淹成泥地。冲击会震响 1.5 格内的下一个 = 连锁。他吃过一次亏就会躲\n" +
        "巨炮：走到炮旁按 E 坐进去，方向键瞄准（落点画在地上），L 把自己轰过去；他也会坐炮抄近路——红圈是他的落点，跑过去按 L 拨歪\n" +
        "山丘 ^ 挡住平地上的视线（躲在后面），站上去看得远、L 够得远；山洞 h 两个一对，钻进去按 E 从另一头出来\n" +
        "每天早上公布天气：大风吹偏炮弹、雨天水更大、雾天他看得近、赶集日他晚出门；雷雨在路灯旁 L 召唤闪电；酸雨高草全枯；下雨天震到山坡 = 泥石流\n" +
        "心：你和马里奥各 3 颗（左上）。被闪电 / 滚石 / 泥石流 / 被巨炮轰 打中 = 掉 1 颗心 + 晕 2 秒（身体一闪一闪 = 保护期：晕完再闪 1.5 秒，这期间什么都打不到你，赶紧跑）；香蕉皮只滑倒、水淹只变慢。心掉光 = 晕倒 3 秒、剩 1 颗站起来\n" +
        "雷区（地图上的蓝色虚线框）：每隔一会儿同时劈几道闪电——地上先闪 1.2 秒，变白 = 马上劈，十字形 1 格。+ 补心，* 能量（你的机关让他掉心也 +1）\n" +
        "能量满 3 格按 Q：在你头顶召唤雷云（停在原地 9 秒，冲着云里的他劈，但也会随机劈——快逃出来，别劈到自己）\n" +
        "指引：屏幕边上的箭头 = 下一扇门；左上会算好\"你几秒 / 他几秒\"来不来得及；按住 Tab 看去门的路线，M 小地图   H 关闭/打开说明";

    // S217：测试不卡人——底部常驻按键、等他出门的提示、没点游戏窗口的提醒、快速测试、F8 反馈
    public const string OverworldControlsBar = "方向键/WASD 走   P 伪装   L 香蕉皮/大机关   T 挑衅   Q 雷云(能量满)   E 门口埋伏/坐炮/钻洞   空格(按住) 快进   Tab 路线   M 小地图   - / = 镜头远近   H 说明   F8 记反馈";
    public const string OverworldHelpClose = "按任意键关闭说明（H 随时再打开）  Press any key";
    public static string OverworldWaitDepart(int door, string clock, double realSeconds) =>
        $"马里奥 {clock} 才出门去门 {door}（还有约 {realSeconds:0} 秒）\n先去门口躲好，或按住空格快进";
    public const string ClickGameWindow = "先用鼠标点一下游戏画面，键盘才有反应\nClick the Game view first";
    public const string QuickTestRoundOver = "快速测试模式：不弹问卷（F8 随手记反馈）\n\n<b>N</b> = 下一局 Next round        <b>R</b> = 从头开始 Restart";
    public static string FeedbackSaved(int n) => $"✓ 已记下第 {n} 条反馈（截图 + 当时情况）\n测试中心 → 打包反馈 发给 AI";
    public const string FeedbackError = "⚠ 刚出了一个错误，已自动记进反馈（不用截图）";

    public static string OverworldClock(string clock, bool night) => night ? $"🌙 {clock}" : $"☀ {clock}";
    public static string OverworldNextDoor(int n, string clock) => $"下一站：门 {n}  {clock}\nNext: door {n}";
    public const string OverworldGoingHome = "他要回家了\nHeading home";
    public const string OverworldAmbushHint = "他快到了——按 E 进屋埋伏！\nPress E to ambush";
    // S213：埋伏要等他走近
    public const string OverworldAmbushWait = "他还远：先躲好（高草 / 按 P 伪装），等他走近再按 E\nWait for him — hide first";
    public const string OverworldSpotted = "他盯上你了！先甩掉他（躲进高草 / 绕到房子后面），再回来埋伏\nHe spotted you — lose him first";
    public static string OverworldAmbushCountdown(int steps, int need, bool disguised) =>
        (steps < 0 ? "他还没出发" : $"他还有 {steps} 格（{need} 格内按 E 埋伏）") + (disguised ? "\n伪装中，别动" : "\n先躲进高草或按 P 伪装；按住空格快进");
    public const string OverworldLateHint = "他刚进去了！快按 E 跟进\nHe just went in — press E!";
    public const string OverworldTooEarly = "这扇门今天不是他下一站\nNot his next stop";
    public const string OverworldCaught = "被马里奥抓住了！送回出生点\nCaught! Back to start";
    /// <summary>S222：被抓时多一行"他是怎么注意到你的"（SpyParty：解释被抓的原因 → 下次知道怎么躲）。</summary>
    public static string OverworldCaughtWhy(OverworldMap.SeenWhy why)
    {
        switch (why)
        {
            case OverworldMap.SeenWhy.Near: return OverworldCaught + "\n原因：贴得太近，背后也能察觉 Too close";
            case OverworldMap.SeenWhy.GrassClose: return OverworldCaught + "\n原因：草里也挡不住贴身的他 Grass can't hide you up close";
            case OverworldMap.SeenWhy.Lamp: return OverworldCaught + "\n原因：你站在路灯下，夜里照样看得远 You stood under a lamp";
            case OverworldMap.SeenWhy.DisguiseMoved: return OverworldCaught + "\n原因：伪装成木箱时还在动 The crate moved";
            case OverworldMap.SeenWhy.Rustle: return OverworldCaught + "\n原因：你在高草里走动，草晃了 The grass rustled";
            case OverworldMap.SeenWhy.Taunt: return OverworldCaught + "\n原因：挑衅把他引过来了 Your taunt drew him in";
            default: return OverworldCaught + "\n原因：你在他正前方的视线里 In plain sight";
        }
    }
    public const string OverworldMissed = "他在里面安心偷完了——这户被偷\nToo late, that house got robbed";
    // S218：大机关 / 天气 / 联动
    public const string OverworldBigArmed = "大机关预警中……（1.2 秒后发动，红格 = 危险）\nBig prank armed";
    public const string OverworldBigHit = "砸中了！他晕 2 秒——这时进门，房间开局他还晕着\nDirect hit!";
    public const string OverworldBigChain = "连锁！冲击震响了下一个大机关\nChain reaction!";
    public const string OverworldBigReloaded = "守住了一户：旁边的大机关重新装填好了\nBig prank reloaded";
    public const string OverworldBigSelf = "被自己的滚石碾到了！晕 2 秒\nFlattened by your own boulder";
    public const string OverworldBigStuck = "这个机关现在用不了（今天用过 / 四面堵死）\nCan't use that now";
    // S219：坐炮瞄准 / 马里奥坐炮 / 闪电 / 泥石流 / 山洞
    public const string OverworldCannonSeat = "坐进巨炮了！方向键瞄准（落点画在地上），L 发射，E 下来\nIn the cannon: arrows aim, L fire, E exit";
    public const string OverworldCannonBar = "巨炮里：方向键 瞄准（顺着炮管 = 远，反着 = 近，横着 = 转向）   L 发射   E 下来（瞄好的留着）   坐太久自动发射";
    public static string OverworldCannonAim(string dir, int dist, int x, int y, bool ok, float left) =>
        (ok ? $"往{dir} {dist} 格 → 落点 ({x},{y})  L 发射" : $"往{dir} {dist} 格 → 落点 ({x},{y}) 走不回家，不能打") + $"\n还能坐 {System.Math.Max(0f, left):0} 秒";
    public const string OverworldCannonBadAim = "这一炮落点走不回马里奥的家（被轰过去会困住）：换个方向或距离\nBad landing — re-aim";
    public const string OverworldCannonTamper = "拨歪了他的炮管！他会飞错地方、落地晕一下\nYou knocked his aim!";
    public const string OverworldMarioRides = "马里奥坐进巨炮抄近路了！红圈 = 他的落点，跑过去按 L 拨歪炮管\nMario is taking the cannon!";
    public const string OverworldLightning = "闪电劈下来了！旁边的人都晕了\nLightning!";
    public const string OverworldMudslide = "泥石流！山坡冲下来一片泥地\nMudslide!";
    public const string OverworldCaveHop = "从山洞另一头钻出来了\nThrough the cave";
    // S220：心 / 能量 / 雷区 / 雷云
    public const string OverworldYouHurt = "你被打中了！掉 1 颗心，晕 2 秒\nYou got hit (-1 heart)";
    public const string OverworldYouKO = "你的心掉光了！晕倒 3 秒，剩 1 颗心站起来\nKnocked out!";
    public const string OverworldMarioKO = "马里奥的心掉光了！他晕倒 3 秒（进门时他只带 2 颗心）\nMario knocked out!";
    public const string OverworldHeal = "捡到补心 +1\n+1 heart";
    public const string OverworldMarioHeal = "马里奥路过捡了补心 +1\nMario healed";
    public const string OverworldEnergyUp = "能量 +1（满 3 格按 Q 召唤雷云）\n+1 energy";
    public const string OverworldEnergyFull = "能量满了！按 Q 在头顶召唤雷云（会劈到你自己，召完快跑）\nEnergy full — press Q";
    public const string OverworldCloud = "雷云来了！它停在这里不动——快跑出圈，让他走进来\nStorm cloud summoned — get out!";
    public const string OverworldCloudLow = "能量不够（要满 3 格）：捡 * 或者用机关让他掉心\nNot enough energy";
    public const string OverworldStormBolt = "雷区劈下来了！地上闪光 = 马上劈，变白就跑\nLightning zone!";
    public static string OverworldRoomCarry(int mario, int you) => $"带着心进门：马里奥 {mario} ❤  你 {you} 条命\nCarried hearts";
    public static string OverworldHearts(int mario, int you, int energy) => $"马里奥 {new string('♥', mario)}{new string('♡', System.Math.Max(0, 3 - mario))}   你 {new string('♥', you)}{new string('♡', System.Math.Max(0, 3 - you))}   能量 {new string('◆', energy)}{new string('◇', System.Math.Max(0, 3 - energy))}" + (energy >= 3 ? "  Q!" : "");
    public const string OverworldCaveNoExit = "这个山洞没有另一头（山洞要两个一对）\nDead-end cave";
    public static string OverworldWeather(int day, string zh) => $"第 {day} 天  {zh}\nDay {day}";
    public static string OverworldWeatherShort(int day, int kind) => $"D{day} " + (kind == 1 ? "🌬" : kind == 2 ? "🌧" : kind == 3 ? "🌫" : kind == 4 ? "🧺" : kind == 5 ? "⛈" : kind == 6 ? "☂" : "☀");
    public static string OverworldRoomDazed(float s) => $"他在小镇被砸晕了，进来还晕着（多等 {s:0} 秒）\nStill dazed from town";
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
    public static string OverworldTransitRetry(int door) => $"门 {door} · 重来一次\nRetry room";
    // S212：地图指引
    public static string OverworldRace(OverworldGuide.Race r)
    {
        switch (r.verdict)
        {
            case OverworldGuide.Verdict.Ahead: return $"你 {r.you:0} 秒 / 他 {r.him:0} 秒  ✓ 来得及埋伏";
            case OverworldGuide.Verdict.Tight: return $"你 {r.you:0} 秒 / 他 {r.him:0} 秒  ⚠ 很紧，快走！";
            case OverworldGuide.Verdict.Behind: return $"你 {r.you:0} 秒 / 他 {r.him:0} 秒  ✗ 追不上——香蕉皮/挑衅拖住他";
            case OverworldGuide.Verdict.Inside: return "他已经进门了！快按 E 跟进";
            default: return "";
        }
    }
    /// <summary>门头上的倒计时：realSeconds = 现实里还有几秒他出发去这扇门（≤0 = 已经在路上）。</summary>
    public static string OverworldDoorCountdown(int n, double realSeconds) => realSeconds > 0.5 ? $"门 {n} · {realSeconds:0} 秒后出发" : $"门 {n} · 他在路上";
    public const string OverworldGuideKeys = "Tab 路线   M 小地图   空格 快进   H 说明";
    public const string OverworldFastForward = ">> 快进 ×4（有动静自动停）";
    public const string OverworldMinimapTitle = "小地图（M 关闭）  蓝 = 你  红 = 马里奥  亮粉 = 下一扇门";
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
