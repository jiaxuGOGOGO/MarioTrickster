/// <summary>
/// S182：第 1 步所有玩家可见文字（中英对照）集中在这里，便于统一修改；纯逻辑，可测试。
/// </summary>
public static class Step1Text
{
    public enum Outcome { MarioEscaped, TricksterCaughtOut, TimeUp, MarioKnockedOut, HandsOffTimeout, Other }

    public const string HandsOffTimeoutReason = "HandsOffTimeout";

    /// <summary>把 GameManager 的胜者+原因翻成玩家能懂的结局。</summary>
    public static Outcome Classify(string winner, string reason)
    {
        reason = reason ?? "";
        if (reason == HandsOffTimeoutReason) return Outcome.HandsOffTimeout;
        if (winner == "Mario") return reason.StartsWith("Trickster caught") ? Outcome.TricksterCaughtOut : Outcome.MarioEscaped;
        if (winner == "Trickster")
        {
            if (reason.StartsWith("Time ran out")) return Outcome.TimeUp;
            if (reason.StartsWith("Health depleted")) return Outcome.MarioKnockedOut;
        }
        return Outcome.Other;
    }

    public static bool PlayerWon(Outcome o) => o == Outcome.TimeUp || o == Outcome.MarioKnockedOut;

    public static string Headline(Outcome o)
    {
        switch (o)
        {
            case Outcome.MarioEscaped: return "马里奥带着宝物逃走了\nMario escaped with the loot";
            case Outcome.TricksterCaughtOut: return "你被抓满 3 次\nYou were caught 3 times";
            case Outcome.TimeUp: return "时间到，马里奥没逃出去 —— 你赢了！\nTime's up, Mario didn't escape — YOU WIN!";
            case Outcome.MarioKnockedOut: return "马里奥被机关打倒 —— 你赢了！\nMario was knocked out — YOU WIN!";
            case Outcome.HandsOffTimeout: return "马里奥卡住了（超时）\nMario got stuck (timeout)";
            default: return "本局结束\nRound over";
        }
    }

    public static string MarioStateText(MarioMindState state, bool waiting, bool carryingLoot, float waitSeconds = 0f, bool stunned = false)
    {
        int secs = (int)System.Math.Ceiling(waitSeconds);
        if (waiting) return secs > 0 ? $"{secs} 秒后出发，快去埋伏！ Starts in {secs}s — get ready!" : "准备中 Getting ready";
        if (stunned) return "被坑晕了！ Dizzy!";
        switch (state)
        {
            case MarioMindState.Curious: return "? 起疑了 Suspicious";
            case MarioMindState.Investigating: return "! 过去查看 Checking it out";
            case MarioMindState.Chasing: return "!! 看见你了，在追你！ Chasing you!";
            case MarioMindState.Searching: return "?! 追丢了，在找 Lost you, searching";
            default: return carryingLoot ? "拿到宝了，跑回出口 Running home" : "去右边拿宝 Going for the loot";
        }
    }

    /// <summary>马里奥头顶的短句（两行，中文在上）。</summary>
    public static string HeadIntent(MarioMindState state, string intent)
    {
        if (intent == "READY...") return "准备\nREADY";
        if (intent == "DIZZY") return "晕了\nDIZZY";
        if (intent == "LOOK BACK") return "回头看\nLOOK";
        if (intent == "CAREFUL") return "小心点\nCAREFUL";
        if (intent == "DODGE") return "要炸了！\nDODGE";
        if (intent == "GRAB") return "捡道具\nGRAB";
        if (intent == "DETOUR") return "绕开\nDETOUR";
        if (intent == "HOP") return "跳过去\nHOP";
        switch (state)
        {
            case MarioMindState.Curious: return "嗯？\nHUH?";
            case MarioMindState.Investigating: return "去看看\nCHECK";
            case MarioMindState.Chasing: return "站住！\nSTOP!";
            case MarioMindState.Searching: return "人呢？\nWHERE?";
            default: return intent == "RUN HOME" ? "回出口\nHOME" : "拿宝\nLOOT";
        }
    }

    public const string ControlsBar = "← → 移动 Move   ↑ 跳 Jump   P 伪装 Disguise   L 触发   B 炸弹   Z 缩小   G 诱饵   F 连锁   T 挑衅   ↓ 通风管   |   V 这是什么 Labels   H 帮助 Help   Esc 暂停";

    public const string Help =
        "<b>怎么玩  HOW TO PLAY</b>\n\n" +
        "你 = <color=#6FA8FF><b>蓝色方块</b></color>（捣蛋鬼）   马里奥 = <color=#FF6B6B><b>红色方块</b></color>\n" +
        "You = the <color=#6FA8FF>BLUE</color> block.   Mario = the <color=#FF6B6B>RED</color> block.\n\n" +
        "马里奥会自己去<b>右边拿宝</b>，再<b>跑回左边出口</b>。\n" +
        "Mario grabs the loot on the RIGHT, then runs back to the exit on the LEFT.\n\n" +
        "<b>你的目标：</b>别让他带宝逃走。用机关坑他，别被他抓到（3 条命）。\n" +
        "<b>Your goal:</b> stop him escaping. Prank him with traps. Don't get caught (3 lives).\n\n" +
        "1. 走到机关旁 → 按 <b>P</b> 伪装，站着别动一会儿    Go next to a trap → <b>P</b> to disguise, stand still\n" +
        "2. 他走近时 → 按 <b>L</b> 触发（火 / 封路墙 / 塌桥）    When he's close → <b>L</b> to trigger\n" +
        "   被火烧到他会晕一下，封路墙能拦住他。   Fire makes him dizzy; the wall blocks him.\n" +
        "   <b>4 秒内连着坑他 = 连招</b>，晕得更久！<b>换不同机关</b>分更高。   Chain within 4s = COMBO. Mix different traps for more points!\n" +
        "   <color=#4FE08C><b>弹簧板</b></color>（绿地砖）：L 把他弹上天，落地前摆好火 = 浮空连招。  <b>Spring</b>: launch him, then fire where he lands.\n" +
        "   <color=#FFE040><b>香蕉皮</b></color>（地上黄条）：L 后他踩上去会滑出 3–4 格。  <b>Banana</b>: he slides forward.\n" +
        "   <color=#C8A070><b>裂缝地板</b></color>（宝物旁）：L 打碎，他掉进地下室。  <b>Crack floor</b>: L drops him into the basement.\n\n" +
        "<b>B 炸弹</b>（3 枚，现形才能放）：炸开附近的<b>裂墙</b>/裂缝地板/箱子，炸晕马里奥——但<b>他听得见</b>。  <b>B</b> = bomb (he hears it!)\n" +
        "<b>大炮</b>：伪装控制时 <b>←→</b> 调方向、<b>↑↓</b> 调仰角，<b>L</b> 开炮；炮弹打完后你和马里奥都能钻进去把自己打出去（冷却 30 秒）。  Cannon: aim with arrows\n" +
        "<b>以身入局（连锁）</b>：走到机关旁按 <b>F</b> 编号①②③（Shift+F 一键把周围的机关全编上）→ 按 <b>T</b> 挑衅把他引过来 → 他追你踩到<b>绊线</b>或你按 L 触发任意一环 → 其余几环在他走到时<b>自动接上</b>。  <b>F</b> link traps, <b>T</b> taunt\n" +
        "<b>G 诱饵</b>（每局 1 次，现形才能放）：留个假的你，他会去追——走近会识破。  <b>G</b> = decoy\n" +
        "<b>油桶</b>：被火/炸弹/炮弹点燃后爆炸，还会引爆旁边的油桶（连锁）。  <b>Oil barrel</b>: chain explosions\n" +
        "<b>铁笼</b>：伪装在旁按 L 落下，关住下面的人 3 秒。  <b>Cage</b>: L drops it on whoever is below\n" +
        "<b>绳套</b>：谁踩到谁被倒吊 10 秒——你也会中！  <b>Snare</b>: anyone who steps in hangs for 10s\n" +
        "<b>? 道具箱</b>：谁先碰到归谁，同一个箱子给你和给他效果不同。  <b>?</b> = random pickup for whoever grabs it\n" +
        "<b>Z 缩小</b>（每局 2 次）：钻窄缝、跑得快，但不能伪装/触发机关。  <b>Z</b> = shrink\n" +
        "<b>通风管</b>：站在管口按 <b>↓</b> 钻到配对的管口（马里奥进不去，但近处听得见咣当声）。  <b>↓</b> on a vent = travel\n" +
        "<b>马里奥会停止时间</b>（每局 1 次）：屏幕边缘变蓝 = 快躲！   Blue edges = Mario is about to stop time!\n" +
        "<b>M</b> 或 <b>Tab</b> = 地图图例（哪些是墙、哪些能炸）   <b>M/Tab</b> = legend\n" +
        "<b>捷径门</b>：只能从一边推开，开了就一直开着。  <b>Shortcut door</b>: opens from one side only.\n" +
        "<b>大炮</b>（深色方块）：伪装在旁边按 L 开一炮（每局 1 发）；打完后<b>站进炮口</b>会把你打飞出去逃跑。\n" +
        "<b>Cannon</b>: disguise next to it, L = fire (1 shot/round). Then stand inside it to launch yourself away.\n" +
        "躲在<b>箱子后</b>、<b>草丛里</b>或<b>高墙另一边</b>，他就看不见你。草丛会晃——有时是风。\n" +
        "Hide behind crates, inside bushes, or behind tall walls. Bushes shake — sometimes it's just wind.\n" +
        "每局<b>藏身处和火会随机变化</b>。   Hiding spots and some fires change every round.\n" +
        "马里奥头顶  Above Mario:   <b>?</b> 起疑   <b>!</b> 来查看   <b>!!</b> 看见你在追   <b>?!</b> 追丢了\n" +
        "白色扇形 = 他的视野，墙会挡住。   White cone = his view (walls block it).\n\n" +
        "<color=#BBBBBB>V 显示每个东西是什么 labels    H 关闭帮助 help    C 换镜头 camera</color>";

    public const string BombNeedUndisguise = "要先<b>现形</b>（P 取消伪装）才能放炸弹  Undisguise to place a bomb";
    public const string BombWhileSmall = "缩小时拿不动炸弹  Can't bomb while small";
    public const string BombNone = "炸弹用完了  No bombs left";
    public const string BombCooldown = "炸弹冷却中  Bomb cooling down";
    public const string BombPlaced = "💣 放下炸弹！快跑（还剩 {0} 枚）  Bomb placed! ({0} left)";
    public const string BombBoom = "轰！<size=20>（马里奥听见了）</size>  BOOM! (Mario heard it)";
    public const string ShrinkOn = "缩小！{0:F0} 秒（还剩 {1} 次）——能钻窄缝，但不能伪装/触发机关  Shrunk!";
    public const string ShrinkNone = "缩小次数用完了  No shrinks left";
    public const string ShrinkBlocked = "头顶太低，变不回去  No room to grow";
    public const string VentIn = "钻进通风管…  Into the vent…";
    public const string VentOut = "从通风管出来！  Out of the vent!";
    public const string VentNoMate = "这个通风管没有配对的出口  This vent has no pair";
    public const string CannonLoadYou = "进炮！<b>↑↓</b> 调角度 <b>←→</b> 调方向，马上发射  In the cannon: aim!";
    public const string CannonLoadMario = "马里奥钻进了大炮！  Mario jumped into the cannon!";
    public const string SnareMario = "🪢 马里奥被<b>绳套</b>吊起来了！（10 秒）  Mario is snared!";
    public const string SnareYou = "🪢 你踩到<b>绳套</b>被吊起来了！  You got snared!";
    public const string ShieldBlocked = "🛡 马里奥的护盾挡住了这一下  Shield blocked it";
    public const string ChainNoneNear = "附近没有能编进连锁的机关（走到机关旁再按 F）  No trap nearby";
    public const string ChainFull = "连锁最多 {0} 环  Chain is full";
    public const string ChainAdded = "⛓ 编入连锁 第 {0} 环：{1}  Linked #{0}";
    public const string ChainRemoved = "⛓ 取消：{0}  Unlinked";
    public const string ChainAuto = "⛓ 一键布置：周围 {0} 个机关按远近编号  Auto-linked {0}";
    public const string ChainStarted = "⛓ 绊线！连锁启动  Chain started!";
    public const string ChainPerfect = "⛓⛓⛓ 完美连锁！  PERFECT CHAIN!";
    public const string TauntNeedUndisguise = "要先<b>现形</b>才能挑衅  Undisguise to taunt";
    public const string TauntNone = "挑衅用完了  No taunts left";
    public const string TauntCooldown = "挑衅冷却中  Taunt cooling down";
    public const string TauntDone = "📣 挑衅！他听见了你的位置（还剩 {0} 次）  Taunted!";
    public const string TripwireHit = "马里奥被<b>绊线</b>绊了一下！  Mario tripped!";
    public const string PersonalityHint = "这局马里奥是 <b>{0}</b>：{1}";
    public const string CautiousTip = "被坑过的地方他会<b>绕开</b>走别的路 → 同一个坑别指望第二次，另一条路上也要布置  Cautious: avoids spots where he got hurt";
    public const string GreedyTip = "看见道具箱<b>一定去抢</b>（跨层也去）→ 在箱子旁边布连锁；但他抢到护盾/加速会克你  Greedy: always goes for pickups";
    public const string ReplayChain = "⛓ 完美连锁 ×{0}  PERFECT CHAIN";
    public const string ReplayCombo = "{0} 连击  {0}-HIT COMBO";
    public const string ProbeBanner = "🧪 陷阱试探：AI 捣蛋者按固定策略坑马里奥（你不用操作）  Trap probe";
    public const string BarrelBoom = "🛢 油桶爆炸！  OIL BARREL BOOM!";
    public const string CageMario = "🔒 马里奥被<b>铁笼</b>关住了！（3 秒）  Mario is caged!";
    public const string CageYou = "🔒 你被<b>铁笼</b>关住了！  You got caged!";
    public const string DecoyNeedUndisguise = "要先<b>现形</b>才能放诱饵  Undisguise to drop a decoy";
    public const string DecoyNone = "诱饵用完了  No decoys left";
    public const string DecoyBusy = "已经有一个诱饵了  A decoy is already out";
    public const string DecoyPlaced = "🎭 放下诱饵！快溜（还剩 {0} 个）  Decoy placed! ({0} left)";
    public const string DecoyRevealed = "马里奥识破了诱饵！  Mario saw through the decoy!";
    public const string AlarmOn = "🚨 <b>警报</b>响了！一段时间内你站着不动也会被怀疑  ALARM — even still disguises look suspicious";
    public const string AlarmOff = "警报解除  Alarm off";
    public const string DoorKicking = "咚咚！马里奥在<b>踢门</b>（2 秒）  Mario is kicking the door!";
    public const string DoorKicked = "马里奥把门踢开了！  Mario kicked the door open!";
    public const string TimeStopWarn = "⏳ 马里奥要<b>停止时间</b>了！快躲（草丛/通风管）  Mario is about to STOP TIME — hide!";
    public const string TimeStopOn = "⏸ 时间静止！你动不了  TIME STOPPED — you're frozen";
    public const string HeardSmash = "哐！<size=20>（马里奥听见了）</size>  CRASH! (Mario heard it)";

    public const string PreCollapsedWall = "这一局有一面<b>裂墙已经塌了</b>——多了一条秘密路线！  A cracked wall has already collapsed this round!";

    public const string Paused = "已暂停  Paused\n<size=22>Esc 继续 Resume</size>";
    public const string AfterSurvey = "✓ 已保存 Saved\n\n<b>N</b> = 下一局 Next round        <b>R</b> = 从头开始 Restart";
}
