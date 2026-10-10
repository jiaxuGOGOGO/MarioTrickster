/// <summary>
/// S182：第 1 步所有玩家可见文字（中英对照）集中在这里，便于统一修改；纯逻辑，可测试。
/// </summary>
public static partial class Step1Text
{
    public enum Outcome { MarioEscaped, TricksterCaughtOut, TimeUp, MarioKnockedOut, HandsOffTimeout, Other, /* S235 */ TricksterFellOut, /* S236 */ TricksterSelfHit, /* S245 */ TricksterHazard }

    public const string HandsOffTimeoutReason = "HandsOffTimeout";

    /// <summary>把 GameManager 的胜者+原因翻成玩家能懂的结局。</summary>
    public static Outcome Classify(string winner, string reason)
    {
        reason = reason ?? "";
        if (reason == HandsOffTimeoutReason) return Outcome.HandsOffTimeout;
        if (winner == "Mario" && reason.StartsWith("Trickster fell out")) return Outcome.TricksterFellOut; // S235
        if (winner == "Mario" && reason.StartsWith("Trickster blew")) return Outcome.TricksterSelfHit; // S236
        if (winner == "Mario" && reason.StartsWith("Trickster was worn")) return Outcome.TricksterHazard; // S245：毒池 / 荆棘等素材槽持续掉血掉光
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
            case Outcome.TricksterFellOut: return "你掉出房间，命用完了\nYou fell out of the room — no lives left";
            case Outcome.TricksterSelfHit: return "你被自己的机关炸光了命\nYou blew yourself up — no lives left";
            case Outcome.TricksterHazard: return "你在毒池 / 荆棘里掉光了命\nThe room wore you down — no lives left";
            default: return "本局结束\nRound over";
        }
    }

    public static string MarioStateText(MarioMindState state, bool waiting, bool carryingLoot, float waitSeconds = 0f, bool stunned = false)
    {
        int secs = (int)System.Math.Ceiling(waitSeconds);
        if (waiting) return secs > 0 ? $"{secs} 秒后出发，快去埋伏！（Enter 马上开始） Starts in {secs}s — Enter = go now" : "准备中 Getting ready";
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
        if (intent == "CAREFUL") return "这儿坑过我…\nCAREFUL"; // S225：说出他记得什么（只记得位置，H4）
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

    /// <summary>
    /// S225（Splinter Cell: Blacklist 分层台词 + F.E.A.R. "台词说明意图"）：他为什么起疑，就说为什么。
    /// 同一个原因本局第 1、2 次说具体的，第 3 次起说短的"又来？"（老说同一句会烦）。只换文字，行为不变。
    /// </summary>
    public static string CauseIntent(MarioMindState state, SuspicionCause cause, int timesThisRound)
    {
        if (cause == SuspicionCause.None) return null;
        bool tired = timesThisRound >= 3;
        if (state == MarioMindState.Curious)
        {
            if (tired) return cause == SuspicionCause.Hurt ? "又中招？\nAGAIN?" : "又来？\nAGAIN?";
            switch (cause)
            {
                case SuspicionCause.SawYou: return "那是谁？\nWHO'S THAT?";
                case SuspicionCause.OddProp: return "那东西动了？\nDID IT MOVE?";
                case SuspicionCause.SawTrap: return "机关自己动了？\nWHO DID THAT?";
                case SuspicionCause.Rustle: return "草里有东西？\nIN THE BUSH?";
                case SuspicionCause.Taunt: return "谁在叫我？\nWHO'S THERE?";
                case SuspicionCause.Hurt: return "谁坑我？\nWHO DID THIS?";
            }
        }
        if (state == MarioMindState.Investigating && !tired)
        {
            switch (cause)
            {
                case SuspicionCause.OddProp: return "去看看那东西\nCHECK IT";
                case SuspicionCause.SawTrap: return "去看看机关\nCHECK TRAP";
                case SuspicionCause.Rustle: return "去草丛看看\nCHECK BUSH";
                case SuspicionCause.Taunt: return "去声音那边\nTHAT NOISE";
            }
        }
        return null;
    }

    /// <summary>S225：卡住被挪开后，他头顶说一句（不出戏；顶部的"布局问题"提示照旧，方便你报位置）。</summary>
    public const string StuckRescueHead = "哎呀，脚滑了\nOOPS";

    /// <summary>
    /// S225（樱井政博"消灭无反应"）：按 L 没成功时说清楚为什么。原来原因只发给旧界面，第 1 步房间把旧界面关了 → 按了什么都没有。
    /// 输入 = TricksterController.GetAbilityFailReason 返回的英文原因；不认识的原因也给一句兜底，不会再"没反应"。
    /// </summary>
    public static string AbilityFailZh(string reason)
    {
        reason = reason ?? "";
        if (reason.StartsWith("Too small")) return "缩小时不能触发机关（Z 变回来）  Can't while small";
        if (reason.StartsWith("Must be disguised")) return "要先站到机关旁按 <b>P</b> 伪装，才能按 L  Disguise first (P)";
        if (reason.StartsWith("Stay still")) return "伪装后<b>站着别动</b>一会儿，变实了才能按 L  Stand still to blend in";
        if (reason.StartsWith("Ability not ready")) return "还没准备好，站稳再按  Not ready";
        if (reason.StartsWith("No controls remaining")) return "这次伪装的触发次数用完了：P 取消再伪装  No controls left";
        if (reason.StartsWith("No controllable prop")) return "附近没有能触发的机关：走近一点（5 格内）  No trap nearby";
        if (reason.StartsWith("Possession gate blocked")) return "刚换目标 / 刚被发现，等一下再按  Wait a moment";
        if (reason.StartsWith("No cannonballs")) return "炮弹打完了：按 P 变回来，站在炮口按 ↓ 坐进去，空格把自己打出去  No cannonballs left";
        if (reason.StartsWith("Prop on cooldown")) return "这个机关还在冷却，等它亮起来  Trap cooling down";
        if (reason.StartsWith("Prop already active")) return "这个机关正在动，等它停  Trap already going";
        if (reason.StartsWith("Prop uses exhausted")) return "这个机关这局用完了，换一个  Trap used up";
        if (reason.StartsWith("Prop not ready")) return "这个机关还没准备好  Trap not ready";
        if (reason.StartsWith("Not enough energy")) return "能量不够  Not enough energy";
        if (reason.StartsWith("Busy underground")) return BusyUnder; // S241
        return "现在按不了  Can't right now";
    }

    /// <summary>S225：按 P 没变成伪装时的原因（原来这几种情况直接 return，什么都不显示）。</summary>
    public static string DisguiseFailZh(bool shrunk, bool justCaught, float cooldown, bool noDisguise)
    {
        if (shrunk) return "缩小时不能伪装（Z 变回来）  Can't disguise while small";
        if (justCaught) return "刚被发现，先跑开，等一下才能再伪装  Just spotted — run first";
        if (cooldown > 0f) return $"伪装冷却中，还要 {System.Math.Ceiling(cooldown):0} 秒  Disguise cooling down";
        if (noDisguise) return "这里没有能变的东西  Nothing to disguise as";
        return null;
    }

    public const string ControlsBar = "← → 移动 Move   ↑ 跳 Jump   P 伪装 Disguise   L 触发   B 炸弹   Z 缩小   G 诱饵   F 连锁   T 挑衅   U 遁地   K 蛛丝   ↓ 通风管   |   V 这是什么 Labels   C 镜头 Camera   H 帮助 Help   F8 记反馈   Esc 暂停菜单";

    public const string Help =
        "<b>怎么玩  HOW TO PLAY</b>\n\n" +
        "你 = <color=#6FA8FF><b>蓝色小恶魔</b></color>（捣蛋鬼，有角）   马里奥 = <color=#FF6B6B><b>红帽寻宝人</b></color>（背包）\n" +
        "You = the <color=#6FA8FF>BLUE imp</color> (horns).   Mario = the <color=#FF6B6B>RED-cap</color> treasure hunter.\n" +
        "（调参 artCharacters 关掉 = 回到蓝 / 红方块）\n\n" +
        "马里奥会自己去<b>右边拿宝</b>，再<b>跑回左边出口</b>。\n" +
        "Mario grabs the loot on the RIGHT, then runs back to the exit on the LEFT.\n\n" +
        "<b>你的目标：</b>别让他带宝逃走。用机关坑他，别被他抓到（3 条命）。\n" +
        "<b>Your goal:</b> stop him escaping. Prank him with traps. Don't get caught (3 lives).\n\n" +
        "1. 走到机关旁 → 按 <b>P</b> 伪装，站着别动一会儿    Go next to a trap → <b>P</b> to disguise, stand still\n" +
        "2. 他走近时 → 按 <b>L</b> 触发（火 / 封路墙 / 塌桥）    When he's close → <b>L</b> to trigger\n" +
        "   被火烧到他会晕一下，封路墙能拦住他。   Fire makes him dizzy; the wall blocks him.\n" +
        "   <b>4 秒内连着坑他 = 连招</b>，晕得更久！<b>换不同机关</b>分更高。   Chain within 4s = COMBO. Mix different traps for more points!\n" +
        "   <color=#4FE08C><b>弹簧板</b></color>（绿板+向上箭头）：L 把他弹上天，落地前摆好火 = 浮空连招。  <b>Spring</b>: launch him, then fire where he lands.\n" +
        "   <color=#FFE040><b>香蕉皮</b></color>（地上的香蕉皮）：L 后他踩上去会滑出 3–4 格。  <b>Banana</b>: he slides forward.\n" +
        "   <color=#C8A070><b>裂缝地板</b></color>（宝物旁）：L 打碎，他掉进地下室。  <b>Crack floor</b>: L drops him into the basement.\n\n" +
        "<b>B 炸弹</b>（3 枚，现形才能放）：炸开附近的<b>裂墙</b>/裂缝地板/箱子，炸晕马里奥——但<b>他听得见</b>。  <b>B</b> = bomb (he hears it!)\n" +
        "<b>大炮</b>：伪装控制时 <b>←→</b> 调方向、<b>↑↓</b> 调仰角，<b>L</b> 开炮；没伪装时站在炮口按 <b>↓ 坐进去</b>，方向键瞄准（虚线 = 会飞到哪），<b>空格</b>把自己打出去，落地就能再进（马里奥打完炮弹后也会钻，他要冷却 30 秒）。  Cannon: aim with arrows; Down to sit in, Space to fly\n" +
        "<b>以身入局（连锁）</b>：走到机关旁按 <b>F</b> 编号①②③（Shift+F 一键把周围的机关全编上）→ 按 <b>T</b> 挑衅把他引过来 → 他追你踩到<b>绊线</b>或你按 L 触发任意一环 → 其余几环在他走到时<b>自动接上</b>。  <b>F</b> link traps, <b>T</b> taunt\n" +
        "<b>G 诱饵</b>（每局 1 次，现形才能放）：留个假的你，他会去追——走近会识破。  <b>G</b> = decoy\n" +
        "<b>油桶</b>：被火/炸弹/炮弹点燃后爆炸，还会引爆旁边的油桶（连锁）。  <b>Oil barrel</b>: chain explosions\n" +
        "<b>铁笼</b>：伪装在旁按 L 落下，关住下面的人 3 秒。  <b>Cage</b>: L drops it on whoever is below\n" +
        "<b>绳套</b>：谁踩到谁被倒吊 10 秒——你也会中！  <b>Snare</b>: anyone who steps in hangs for 10s\n" +
        "<b>? 道具箱</b>：谁先碰到归谁，同一个箱子给你和给他效果不同。  <b>?</b> = random pickup for whoever grabs it\n" +
        "<b>看得懂他</b>：视锥里灌黄色 = 他在起疑（灌到你 = 发现你，变红 = 认出你）；蓝色圆圈 = 声音传多远（他在圈里就听见了）。开局等他时按 <b>Enter</b> 马上开始。  Yellow fill = suspicion, blue ring = how far he hears. Enter = start now\n" +
        "<b>Z 缩小</b>（每局 2 次）：钻窄缝、跑得快，但不能伪装/触发机关。  <b>Z</b> = shrink\n" +
        "<b>L 按早了也没关系</b>：他还没走到，机关会<b>先等着</b>（头上沙漏，只有你看得见），他走进来才发动；没等到或没打中 = 退还次数。  Early L = armed, not wasted\n" +
        "<b>光影</b>：白天 → 夜晚 → 雨天 → 雨夜轮换。夜里他只看得见<b>亮处</b>（灯 · 火 · 他的手电筒），暗处只能听你跑步的<b>脚步声</b>（小蓝圈）。你头上写着 暗 / 亮。<b>灯</b>：伪装在旁按 L 灭灯 8 秒。  Night: he only sees lit spots\n" +
        "<b>U 遁地</b>（现形、站在地面 / 草地上）：钻进土里走，看不见你；但<b>裸露的土地</b>白天会拱起土包——草地、下雨、暗处看不见。他<b>踩到土包</b>或<b>扫描</b>会把你逼出来晕 1 秒。  <b>U</b> = burrow\n" +
        "<b>K 蛛丝</b>（现形）：朝斜上方射丝挂在天花板上，<b>←→</b> 荡、<b>↑↓</b> 收放线、<b>K</b> 松手。荡过他头顶时那里<b>亮着他就会察觉</b>，暗着就不会。  <b>K</b> = web swing\n" +
        "<b>通风管</b>：站在管口按 <b>↓</b> 钻到配对的管口（马里奥进不去，但近处听得见咣当声）。  <b>↓</b> on a vent = travel\n" +
        "<b>马里奥会停止时间</b>（每局 1 次）：屏幕边缘变蓝 = 快躲！   Blue edges = Mario is about to stop time!\n" +
        "<b>掉出房间</b>（被炮/炸弹轰出去）= 回出生点、<b>-1 条命</b>。  Falling out of the room = back to start, -1 life\n" +
        "<b>M</b> 或 <b>Tab</b> = 地图图例（只列这个房间有的东西，按 能按 L / 能躲 / 挡路 分三组；小标签只显示你身边的）   <b>M/Tab</b> = legend\n" +
        "<b>捷径门</b>：只能从一边推开，开了就一直开着。  <b>Shortcut door</b>: opens from one side only.\n" +
        "躲在<b>箱子后</b>、<b>草丛里</b>或<b>高墙另一边</b>，他就看不见你。草丛会晃——有时是风。\n" +
        "Hide behind crates, inside bushes, or behind tall walls. Bushes shake — sometimes it's just wind.\n" +
        "每局<b>藏身处和火会随机变化</b>。   Hiding spots and some fires change every round.\n" +
        "马里奥头顶  Above Mario:   <b>?</b> 起疑   <b>!</b> 来查看   <b>!!</b> 看见你在追   <b>?!</b> 追丢了\n" +
        "白色扇形 = 他的视野，墙会挡住。   White cone = his view (walls block it).\n\n" +
        "<b>大房间</b>：镜头会跟着你走（死亡细胞式）；马里奥不在屏幕里时，屏幕边缘的<color=#FF6B6B><b>红箭头</b></color>指着他（带 ? ! 和距离），右上角有<b>小地图</b>。  Big rooms: camera follows you; red edge arrow = Mario off-screen.\n\n" +
        "<color=#BBBBBB>V 显示每个东西是什么 labels    H 关闭帮助 help    C 换镜头 camera    F8 记反馈 feedback    Esc 暂停 pause</color>\n" +
        "<color=#BBBBBB>测试用：F9 技能无限（不算进出口）   F5 马上重开   编辑器里 Ctrl+Alt+H = 开始页（全部功能在哪）</color>";

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
    public const string CannonLoadYou = "坐进大炮！<b>←→</b> 调方向 <b>↑↓</b> 调角度（虚线 = 会飞到哪），<b>空格</b>发射  In the cannon: aim, Space to fire";
    public const string CannonSeatCooldown = "大炮还在冒烟，{0:F1} 秒后能再进  Cannon cooling";
    public const string CannonSeatDisguised = "先按 P 变回来，才能坐进大炮  Undisguise first";
    public const string ControlsBarSeated = "← → 调方向 Turn   ↑ ↓ 调角度 Aim   空格 / L 发射 Fire   |   {0:F0} 秒后自动发射";
    public const string CannonLoadMario = "马里奥钻进了大炮！  Mario jumped into the cannon!";
    public const string SnareMario = "🪢 马里奥被<b>绳套</b>吊起来了！（10 秒）  Mario is snared!";
    // S235：掉出房间（以前掉出去就回不来、血不掉、马里奥照样跑）
    public const string FellOutYou = "⚠ 你掉出房间了 → 回出生点，<b>-{0} 条命</b>  You fell out — back to start, -{0} life";
    public const string FellOutYouSafe = "⚠ 你掉出房间了 → 回出生点（无敌中，不掉命）  You fell out — back to start";
    public const string FellOutMario = "⚠ 马里奥掉出房间了 → 已放回他的路上（布局问题，按 F8 记一下）  Mario fell out — put back (layout bug, press F8)";
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
    // S242：伪装装备栏 / 道具诱饵
    public const string PropDecoyPlaced = "🎭 丢出一个假{0}！它会时不时扭一下引他过去（还剩 {1} 个）  Prop decoy thrown";
    public const string LoadoutPicked = "形态 {0}：{1}";
    public const string LoadoutSampled = "取样！第 {0} 格换成 {1}";
    public const string LoadoutAlready = "已经是{0}了";
    public const string LoadoutNothingNear = "旁边没有能变的东西（站到箱子 / 草丛 / 油桶 / 灯…旁边按 E）";
    public const string LoadoutCaption = "1–N 换形态（伪装中也能换）  E 取样身边的东西  G 丢一个假的";
    public const string AlarmOn = "🚨 <b>警报</b>响了！一段时间内你站着不动也会被怀疑  ALARM — even still disguises look suspicious";
    public const string AlarmOff = "警报解除  Alarm off";
    public const string DoorKicking = "咚咚！马里奥在<b>踢门</b>（2 秒）  Mario is kicking the door!";
    public const string DoorKicked = "马里奥把门踢开了！  Mario kicked the door open!";
    public const string TimeStopWarn = "⏳ 马里奥要<b>停止时间</b>了！快躲（草丛/通风管）  Mario is about to STOP TIME — hide!";
    public const string TimeStopOn = "⏸ 时间静止！你动不了  TIME STOPPED — you're frozen";
    public const string HeardSmash = "哐！<size=20>（马里奥听见了）</size>  CRASH! (Mario heard it)";

    public const string PreCollapsedWall = "这一局有一面<b>裂墙已经塌了</b>——多了一条秘密路线！  A cracked wall has already collapsed this round!";

    public const string CameraSwitched = "镜头：{0}  Camera";
    /// <summary>S207：镜头模式的中文名（C 键切换时的提示）。</summary>
    public static string CameraModeName(Step1CameraMode m)
    {
        switch (m)
        {
            case Step1CameraMode.FrameBoth: return "框住两人 Both";
            case Step1CameraMode.FollowTrickster: return "只跟你 Follow you";
            case Step1CameraMode.SmartFollow: return "智能跟随（死亡细胞式） Smart follow";
            default: return "整个房间 Whole room";
        }
    }

    /// <summary>S226 E9（看情况的按键条）：底部只放"现在按了有用"的键。核心键永远在；炸弹/缩小/诱饵/挑衅/通风管最多再加 3 个，
    /// 只有当下能用才出现（用完、冷却中、伪装中不能用 → 不显示）。全部按键仍在 H 帮助页。</summary>
    public static string ControlsBarFor(bool disguised, bool shrunk, bool canBomb, bool canDecoy, bool canTaunt, bool canShrink, bool nearVent, bool nearCannon = false, bool canBurrow = false, bool canSilk = false, bool burrowed = false, bool swinging = false, bool propDecoy = false)
    {
        // S241：在地下 / 挂在丝上 = 只显示这时有用的键
        if (burrowed) return "← → 地下移动 Move   U 钻出来 Surface   L 触发（不行）   |   H 全部按键 Help   F8 记反馈   Esc 暂停菜单";
        if (swinging) return "← → 荡 Swing   ↑ ↓ 收放线 Reel   K 松手 Release   L 触发（不行）   |   H 全部按键 Help   F8 记反馈   Esc 暂停菜单";
        var extra = new System.Collections.Generic.List<string>();
        if (nearCannon) extra.Add("↓ 坐进大炮 Cannon");
        if (nearVent) extra.Add("↓ 钻通风管 Vent");
        if (canBomb) extra.Add("B 炸弹 Bomb");
        if (canDecoy) extra.Add(propDecoy ? "G 丢假道具 Decoy" : "G 诱饵 Decoy");
        if (canTaunt) extra.Add("T 挑衅 Taunt");
        if (canShrink) extra.Add("Z 缩小 Shrink");
        if (canSilk) extra.Add("K 蛛丝 Silk");
        if (canBurrow) extra.Add("U 遁地 Burrow");
        if (extra.Count > 3) extra.RemoveRange(3, extra.Count - 3);
        string p = disguised ? "P 变回 Undisguise" : shrunk ? "(缩小中 shrunk)" : "P 伪装 Disguise";
        string s = "← → 移动 Move   ↑ 跳 Jump   " + p + "   L 触发 Trigger";
        foreach (var e in extra) s += "   " + e;
        return s + "   |   H 全部按键 Help   F8 记反馈   Esc 暂停菜单";
    }
    public const int ControlsBarMaxItems = 10;

    // ── S241：机关预约 · 光影 · 遁地 · 蛛丝 ─────────────
    public const string ArmWaiting = "⏳ {0} 已预约：他走到就自动发动（最多等 {1:F0} 秒，再按 L = 马上发动）  Armed — fires when he arrives";
    public const string ArmExpired = "⏳ 他没过来，预约作废——次数和能量已退还  He never came — refunded";
    public const string MissRefund = "没坑到他 → {0} 冷却减半，次数退还  Missed — half cooldown, use refunded";
    public const string LightDay = "☀ 白天：哪里都亮，他看得见你";
    public const string LightNight = "🌙 夜晚：他只看得见<b>亮处</b>（灯 · 火 · 手电筒），暗处只能听你的脚步";
    public const string LightRain = "🌧 雨天：遁地的土包看不见，脚步声小一些";
    public const string LightNightRain = "🌧🌙 雨夜：最暗 + 最安静——荡过他头顶的好时候";
    public const string YouInDark = "暗";
    public const string YouInLight = "亮";
    public const string BurrowIn = "⛏ 遁地！裸露的土地上会拱起土包（草地 / 下雨 / 暗处看不见），再按 U 钻出来  Burrowed";
    public const string BurrowOut = "钻出来了  Out of the ground";
    public const string BurrowFlushed = "⚠ 被他逼出地面了！（踩到土包 / 扫描）  Flushed out!";
    public const string BurrowNoGround = "这里不是土地（要站在地面或草地上）  Need dirt or grass under you";
    public const string BurrowBusy = "伪装 / 缩小 / 坐炮 / 荡丝的时候不能遁地  Can't burrow now";
    public const string BurrowCooldown = "刚钻出来，{0:F1} 秒后能再钻  Burrow cooling";
    public const string SilkOn = "🕸 挂上了！←→ 荡  ↑↓ 收放线  K 松手（夜里暗处荡过他头顶 = 不被发现）  Swinging";
    public const string SilkNoAnchor = "斜上方没有能挂的天花板（{0:F0} 格内）  No ceiling to hang from";
    public const string SilkBusy = "伪装 / 坐炮 / 遁地的时候不能射丝  Can't web-swing now";
    public const string SilkCooldown = "蛛丝 {0:F1} 秒后能再射  Silk cooling";
    public const string BusyUnder = "在地下 / 挂在丝上，先 U 钻出来或 K 松手  Underground or swinging";
    public const string LampOff = "💡 灯灭了 {0:F0} 秒  Lamp off";

    public const string Paused = "已暂停  Paused\n<size=22>Esc 继续 Resume</size>";
    // S244：暂停菜单
    public const string PauseKeys = "↑↓ 选   Enter / 空格 确认   数字键直接选   Esc 继续";
    public const string PauseSettingsKeys = "↑↓ 选   ←→ 调   Backspace / 最后一行 返回   Esc 继续游戏";
    public const string PauseArtNextRound = "像素美术：下一局生效（R 重开 / N 下一局）  Applies next round";
    public const string AfterSurvey = "✓ 已保存 Saved\n\n<b>N</b> = 下一局 Next round        <b>R</b> = 从头开始 Restart";
}
