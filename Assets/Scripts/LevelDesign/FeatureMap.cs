using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// S236：项目功能地图——"这个项目能做什么、在哪打开、创作时怎么用"的唯一来源。
/// Unity 开始页（Ctrl+Alt+H）、网页设计台"功能地图"页、docs/FEATURE_MAP.md 都从这里来（三处永远一致）。
/// 防遗忘：体检（sim S236）要求 —— 每个 MarioTrickster 菜单、每个游戏按键、网页每个面板、docs/step1 每篇说明
/// 都必须被某一条登记；每条登记的"锚点"代码必须还在（功能删了就得把这条也删掉）。新功能不登记 = 体检红。
/// 写法规则：字符串里不要用英文双引号（网页 build.py 按引号解析），需要引号用「」。
/// </summary>
public static class FeatureMap
{
    public sealed class Feature
    {
        public string id, area, tier, name, how, what, create, docs, menu, keys, web, anchorFile, anchorText;
        public int since;
        /// <summary>菜单（可以有几个，用 | 隔开；以 / 结尾 = 整个子菜单）。</summary>
        public IEnumerable<string> Menus => Split(menu, '|');
        public IEnumerable<string> Keys => Split(keys, ' ');
        public IEnumerable<string> WebPanels => Split(web, '|');
        public IEnumerable<string> DocTokens => Split(docs, ' ');
        /// <summary>能直接打开的第一个菜单（不是子菜单）。</summary>
        public string OpenMenu => Menus.FirstOrDefault(m => !m.EndsWith("/"));
    }

    public sealed class Goal { public string title, steps, why; public string[] ids; }

    static IEnumerable<string> Split(string s, char c) => (s ?? "").Split(c).Select(x => x.Trim()).Where(x => x.Length > 0);

    public static readonly string[] Areas = { "① 玩：恶作剧房间", "② 玩：小镇大地图", "③ 做：关卡 · 小镇 · 台词 · 数值", "④ 网页设计台", "⑤ 测试与反馈", "⑥ 美术", "⑦ 给 AI · 换账号", "⑧ 安全网" };
    /// <summary>常用 = 每次都会用；进阶 = 想深挖时用；自动 = 平时不用管，出问题才看；参考 = 读的东西。（S238：「旧工具」一档随旧工具一起删了）</summary>
    public static readonly string[] Tiers = { "常用", "进阶", "自动", "参考" };

    static Feature F(string id, string area, string tier, string name, string how, string what, string create, int since, string docs, string menu, string keys, string web, string anchorFile, string anchorText)
        => new Feature { id = id, area = area, tier = tier, name = name, how = how, what = what, create = create, since = since, docs = docs, menu = menu, keys = keys, web = web, anchorFile = anchorFile, anchorText = anchorText };

    public static readonly Feature[] All =
    {
        // ① 玩：恶作剧房间
        F("room.play", "① 玩：恶作剧房间", "常用", "▶ 试玩恶作剧房间", "测试中心 ② ▶ 试玩房间；或菜单 MarioTrickster → ▶ 试玩房间；开局按 Enter 马上开始", "你是蓝色捣蛋鬼，马里奥（红）自己去右边拿宝再跑回左边出口；你用机关坑他、别被抓 3 次", "画完房间都要来这里玩一局：别人怎么玩、马里奥怎么走，一局就看清", 181, "STEP1_PRANK_ROOM", "MarioTrickster/▶ 试玩房间 Play Room", "Return", "", "Editor/Step1PrankRoomBuilder.cs", "public static void PlayMenu()"),
        F("room.basic", "① 玩：恶作剧房间", "常用", "走 · 跳 · 伪装 · 触发", "← → 走，↑ 跳，P 伪装（站着别动），L 触发身边的机关", "伪装时被他看见还在动会起疑；触发有预警，他走到才有用", "摆机关时想清楚：你躲在哪伪装、他从哪走过来、你按 L 时他在不在范围里", 182, "S209", "", "LeftArrow RightArrow UpArrow P L", "", "Gameplay/Step1/Step1FailFeedback.cs", "Step1Keys.Down(KeyCode.P)"),
        F("room.traps", "① 玩：恶作剧房间", "常用", "机关全家（火 · 封路墙 · 塌桥 · 弹簧 · 香蕉皮 · 裂缝地板 · 油桶 · 铁笼 · 绳套 · 道具箱 · 毒池 · 黏胶 · 捷径门 · 裂墙 · 通风管）", "工坊左边元素栏点一下就能画；每个元素的说明在工坊悬停或元素图例", "每种机关平时安全、你触发才动；会塌会挡的东西体检按最坏情况算，保证马里奥永远有路", "一关主打 1–2 种机关，其余当配角；同一种坑第 3 次他就不上当了", 193, "S193 S199 ELEMENT_LEGEND", "", "", "", "LevelDesign/ElementCatalog.cs", "I('~', \"FireTrap\""),
        F("room.bomb", "① 玩：恶作剧房间", "常用", "B 炸弹 · Z 缩小 · ↓ 通风管", "现形时按 B 放炸弹（3 枚）；Z 缩小 5 秒钻窄缝（2 次）；站在管口按 ↓ 钻到配对的管口", "炸弹能炸裂墙/箱子/裂缝地板、炸晕他，但他听得见；缩小时不能伪装；通风管他进不去但听得见", "裂墙后面藏近路、通风管连上下两层 = 让你有地方逃、有地方埋伏", 197, "S197 S198", "", "B Z DownArrow", "", "Gameplay/Step1/TricksterKit.cs", "Step1Keys.Down(KeyCode.B)"),
        F("room.decoy", "① 玩：恶作剧房间", "常用", "G 诱饵 · 马里奥踢门 · 警报", "现形时按 G 放一个假的你（每局 1 个）", "他会去追诱饵，走近会识破；警报响时站着不动也可疑；他会踢开挡路的门", "把诱饵放在机关前面 = 把他引进坑里", 199, "S199", "", "G", "", "Gameplay/Step1/DecoyAbility.cs", "Step1Keys.Down(KeyCode.G)"),
        F("room.chain", "① 玩：恶作剧房间", "常用", "F 连锁 · T 挑衅 · 绊线（以身入局）", "走到机关旁按 F 编号①②③（Shift+F 一键全编）；T 挑衅把他引过来（3 次）；他踩到绊线 R 或你按 L 任意一环就开始", "一环接一环自动触发；编号只在你附近 / 连锁进行中显示，下一环放大，环与环之间有点线，右上角列出顺序；用过的机关变灰、不能再编号；完美连锁会放一段回放（空格跳过）", "设计一条「他追你 → 踩绊线 → 一串机关」的走廊，诱捕走廊样板就是示范", 200, "S200", "", "F T", "", "Gameplay/Step1/ChainPlan.cs", "Step1Keys.Down(KeyCode.F)"),
        F("room.arm", "① 玩：恶作剧房间", "常用", "按 L 不怕早：机关预约 · 没打中退还", "他还没走到就按 L = 机关进入预约（头顶沙漏），等他走进范围自动发动；再按一次 L = 立刻发动", "预约最多等 5.5 秒；早一点晚一点都有效果（范围放宽一点）；没打中他 = 次数退回、冷却减半；坐炮 / 灯 / 绳套这类一按就生效的不预约", "机关摆在他必经路上，你只管提前按", 241, "S241", "", "L", "", "Ability/TricksterAbilitySystem.cs", "private void ArmProp(IControllableProp prop)"),
        F("room.light", "① 玩：恶作剧房间", "常用", "白天 · 黑夜 · 雨天 · 马里奥的手电筒 · 灯", "每局轮换天气（左上角写着）；你头顶写着 暗 / 亮", "白天他看得见你；夜里他只看得见手电筒照到的、灯 i 照到的、火和爆炸照到的；暗处只能靠听（夜里你跑步有脚步声）；雨天脚步声更小；灯伪装在旁按 L 灭 8 秒", "灯放在他必经的路口，你就得先灭灯再过去", 241, "S241", "", "L", "", "Gameplay/Step1/Step1Lighting.cs", "Mode = Step1Stealth.Resolve(tuning.lightMode, round);"),
        F("room.burrow", "① 玩：恶作剧房间", "进阶", "U 遁地 · 土包 · 被踩出来", "站在地面上（没伪装）按 U 钻进地下，←→ 移动，再按 U 出来（最多 6.5 秒）", "白天裸地上会拱出土包，他看得见；草地 v 上、夜里、雨天看不见土包；他踩到土包或用扫描找到你 = 你被翻出来晕 1 秒", "草地和裸地交替铺 = 有藏有露的遁地路线", 241, "S241", "", "U", "", "Gameplay/Step1/TricksterBurrow.cs", "Step1Keys.Down(KeyCode.U)"),
        F("room.silk", "① 玩：恶作剧房间", "进阶", "K 蛛丝：挂天花板荡过去", "按 K 朝前上方喷丝挂住天花板，自动摆荡；↑↓ 收放丝（收短荡得更快），←→ 加力，再按 K 松手", "最长 7 秒；从他头顶荡过：头顶有光会被发现，暗处不会；你用的时候不能伪装 / 放炸弹", "房间顶上留空 = 给你荡的空间；在两盏灯中间的暗处荡最安全", 241, "S241", "", "K UpArrow DownArrow", "", "Gameplay/Step1/TricksterSilk.cs", "Step1Keys.Down(KeyCode.K)"),
        F("room.cannon", "① 玩：恶作剧房间", "进阶", "大炮：瞄准开炮 · 坐进去自己飞 · 轰出窗户", "伪装在炮旁：←→ 调方向 ↑↓ 调仰角，L 开炮（每局 1 发）；没伪装站在炮口按 ↓ 坐进去，方向键瞄准（虚线 = 会飞到哪），空格发射，落地就能再进", "炮弹能打开裂墙、引爆油桶；坐炮飞约 15 格远；在小镇房间里打中他，回小镇时他会从门口被轰出来", "炮口对着油桶或裂墙 = 一炮两用；两门炮对着放 = 你的空中快线", 187, "S187 S240", "", "UpArrow DownArrow LeftArrow RightArrow Space", "", "LevelElements/Traps/PranksterCannon.cs", "Step1Keys.Down(KeyCode.Space)"),
        F("room.combo", "① 玩：恶作剧房间", "常用", "连招 · 顿帧 · 回放", "任意两下坑到他间隔不到 4 秒 = 连击（换不同机关分更高）；他头顶的黄条 = 还剩多久能接上；弹窗写清楚是哪几下（如 炮弹 → 香蕉皮）；完美连锁放回放，空格 / B / F / G / T 跳过", "有连招计数、顿帧、震屏；同种机关重复用伤害递减；连击和 F 连锁是两回事：连击看时间，连锁看编号", "把 2–3 个不同机关放在 4 秒内能连上的距离；工坊「连招路线」会画给你看", 193, "S202", "", "Space", "", "Gameplay/Step1/ChainReplay.cs", "Step1Keys.Down(KeyCode.Space)"),
        F("room.feel", "① 玩：恶作剧房间", "参考", "手感：弹飞抛物线 · 受伤闪 · 冲击环 · 震屏", "自动，不用操作", "被弹簧/炸弹/炮弄飞有重力抛物线，落地才恢复；连锁有火花线看得出因果", "觉得「生硬、不自然」时截图按 F8，说是哪一下", 216, "S216", "", "", "", "Gameplay/Step1/Step1Feel.cs", "public static class Step1Feel"),
        F("room.mario", "① 玩：恶作剧房间", "常用", "看得懂马里奥（视锥 · ? ! · 声音圈 · 性格 · 中招反应）", "看屏幕：白色扇形 = 视野，黄色灌满 = 起疑，蓝圈 = 声音传多远；头顶 ? 起疑 ! 来查看 !! 追你", "他只凭看见、听见、记住的东西判断（不作弊）；三种性格：冲冲 / 谨慎（绕开吃过亏的地方）/ 贪财（一定去抢道具箱）", "给他留「看起来安全」的近路，坑就埋在那；草丛、箱子、高墙是你的藏身处", 203, "S203 S223", "", "", "", "Gameplay/Step1/MarioPersonality.cs", "enum"),
        F("room.read", "① 玩：恶作剧房间", "常用", "V 这是什么 · M 图例 · C 镜头 · H 帮助 · Esc 暂停", "V 给每个东西标名字；M 或 Tab 地图图例；C 换镜头（大房间跟着你走 + 小地图 + 屏外红箭头）；H 全部按键；Esc 暂停", "底部按键条只显示「现在按了有用」的键", "大房间（长廊远征）用 C 切到「整个房间」看全局", 207, "S207", "", "V M Tab C H Escape", "", "Gameplay/Step1/Step1ElementLabels.cs", "Step1Keys.Down(KeyCode.V)"),
        F("room.timestop", "① 玩：恶作剧房间", "参考", "马里奥的反制：停时间 · 踢门 · 抢道具", "自动；屏幕边缘变蓝 = 他要停时间了，快躲（草丛 / 通风管）", "每种优势都有代价和反制：你有机关，他有停时间、护盾、加速", "做难关时多放草丛和通风管，给你躲停时间的地方", 197, "", "", "", "", "Gameplay/Step1/MarioTimeStop.cs", "public static bool ShouldTrigger("),
        F("room.survey", "① 玩：恶作剧房间", "进阶", "结算问卷 · R 重来 · N 下一局", "一局结束回答 5 道题（Y / N / 数字 1–5，最后一题可写一句话）；R 从头开始，N 下一局", "答案写进试玩记录，测试中心体检自动算「第 1 步出口」和建议", "想认真记感受就关掉快速测试模式；只想反复测就开着（不弹问卷）", 186, "STEP1_PLAYTEST_SHEET", "", "Y N R", "", "Gameplay/Step1/Step1PlaytestLog.cs", "survey.AnswerYesNo(true)"),
        F("room.fallout", "① 玩：恶作剧房间", "自动", "防卡死：卡住救援 · 掉出房间", "自动", "身体卡进墙会推出来（只往里推）；马里奥卡住会被放回路上；你掉出房间 = 回出生点 -1 条命", "看到「马里奥掉出房间 → 已放回」= 布局有口子，按 F8 记一下", 209, "S209 S235", "", "", "", "Gameplay/Step1/Step1RoomGuard.cs", "class Step1RoomGuard"),
        F("room.f9", "① 玩：恶作剧房间", "进阶", "F9 测试：技能无限 · F5 重开", "游戏里按 F9 开 / 关（左上角会写「F9 测试」）；F5 马上重开这一局", "炸弹、缩小、诱饵、挑衅用不完、没冷却——专门用来反复试一个机关；开过 F9 的局不算进第 1 步出口", "调一个坑的位置时开着 F9，连着试十次不用重开", 236, "S236", "", "F9 F5", "", "Gameplay/Step1/Step1QuickTest.cs", "public static bool NoLimits"),
        F("room.feedback", "① 玩：恶作剧房间", "常用", "F8 记反馈（截图 + 当时情况）", "游戏里随时按 F8；出错会自动记", "截图和当时的状态存起来，测试中心 ③ 打包发给 AI", "看到别扭、不懂、卡住的瞬间就按，不用写字", 217, "", "", "F8", "", "Gameplay/Step1/Step1Feedback.cs", "Step1Keys.Down(KeyCode.F8)"),

        // ② 玩：小镇大地图
        F("town.play", "② 玩：小镇大地图", "常用", "▶ 试玩小镇（一天）", "测试中心 ② ▶ 试玩小镇；或小镇工坊 F5；进门 = 进恶作剧房间，打完自动回小镇", "马里奥按时间表去几户人家偷宝贝；你先到门口埋伏，就能在房间里坑他", "一张小镇 = 一天的故事：几扇门、每扇门一个房间、按顺序教新东西", 210, "S210 S211", "MarioTrickster/▶ 试玩小镇 Play Town", "Return", "", "Editor/OverworldBuilder.cs", "public static void PlayMenu()"),
        F("town.basic", "② 玩：小镇大地图", "常用", "小镇操作：走 · 伪装 · 香蕉皮 · 挑衅 · E 埋伏", "方向键 / WASD 走，P 伪装木箱，L 让附近香蕉皮变滑，T 挑衅，E 在门口预约埋伏（坐炮 / 钻山洞也是 E）", "高草和房子后面他看不见；晚上看得近；他先进门 6 秒内跟进 = 迟到", "门前留高草或木箱给你躲；门之间的距离决定你来不来得及", 213, "", "", "W A S D P L T E", "", "Overworld/Runtime/OverworldGame.cs", "door = Step1Keys.Down(KeyCode.E)"),
        F("town.big", "② 玩：小镇大地图", "进阶", "大机关 · 连锁 · 天气（巨炮 · 滚石 · 水塔 · 钟楼 · 山 · 山洞 · 闪电 · 泥石流）", "靠近按 L 发动（每天一次，先闪 1.2 秒）；坐进巨炮按 E，方向键瞄准，L 把自己轰过去", "冲击会震响旁边的下一个 = 连锁；每天早上公布天气（风偏炮弹、雨天水大、雾天他看得近）；他吃过一次亏会躲", "在马里奥必经路口摆一串：钟楼让他停下 → 滚石砸过来 → 水淹路口", 218, "S218 S219 S228", "", "", "", "Overworld/OverworldProps.cs", "public static class OverworldProps"),
        F("town.hearts", "② 玩：小镇大地图", "进阶", "心 · 雷区 · 补心 · 能量 · Q 雷云", "你和他各 3 颗心（左上）；能量满 3 格按 Q 在头顶召唤雷云", "闪电 / 滚石 / 泥石流 / 巨炮打中掉 1 颗心；心掉光这一天马上结束（你赢 / 他赢 / 平局）", "雷区放在他的路线上，补心放在你的逃跑路线上", 220, "S220 S221", "", "Q", "", "Overworld/OverworldStorm.cs", "class OverworldStorm"),
        F("town.guide", "② 玩：小镇大地图", "常用", "指引 · Tab 路线 · M 小地图 · 空格快进 · -/= 镜头", "屏幕边箭头 = 下一扇门；左上算好「你几秒 / 他几秒」；按住 Tab 看路线；M 小地图；按住空格快进（不碰键盘也会自动快进）；- / = 拉远拉近", "等他出门时不用干等", "地图越大越要靠指引；体检会报「干等超过 10 秒」的地方", 212, "S212 S224", "", "Tab M Space Minus Equals", "", "Overworld/OverworldGuide.cs", "public static Race RaceTo("),
        F("town.stories", "② 玩：小镇大地图", "常用", "居民：每户一个人 · 当场喊 · N 笔记本 · 记得你", "打完一户回小镇他说一句；大机关在他家门口砸中马里奥，他当场喊；按 N 翻笔记本（谁说过什么、还差什么）", "常来往才说真心话（每段只说一次），关掉游戏再开也记得", "给每扇门起名字和性格（小镇 .txt 里 # Resident:），台词编辑器写他们的话", 232, "S232 S233", "", "N", "", "Overworld/TownStory.cs", "public sealed class Resident"),
        F("town.dayend", "② 玩：小镇大地图", "常用", "一天结束 · R 第二天", "结算后按 R（被打死会 6 秒后自动重开）", "第二天换天气；记录写进 town_days.csv，体检会算", "连着玩几天看天气和居民台词怎么变", 229, "S229", "", "R", "", "Overworld/Runtime/OverworldGame.cs", "Step1Keys.Down(KeyCode.R) || autoRestart"),
        F("town.link", "② 玩：小镇大地图", "进阶", "小镇 ↔ 房间联动（带炸弹 · 带心 · 晕着进门 · Enter 回小镇）", "自动；房间打完按 Enter（或等几秒）回小镇", "埋伏成功多 1 枚炸弹；道具箱的炸弹带进下一个房间；大机关砸晕他会晕着进门", "设计「先在小镇砸他、再进门坑他」的组合", 215, "", "", "", "", "Overworld/Runtime/OverworldRoomLink.cs", "Step1Keys.Down(KeyCode.Return)"),

        // ③ 做：关卡 · 小镇 · 台词 · 数值
        F("ws.room", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "关卡工坊（画横版房间）", "Ctrl+Alt+W；画笔 B / 矩形 R / 橡皮 E / 吸管 I / 移动 M；右键擦；Ctrl+Z 撤销；▶ 作为第 1 步房间试玩", "边画边检查：红格 = 必须改（悬空、死局、炮口被挡），说明写在格子上", "画完点 ▶ 马上玩；工坊不会让你存一张马里奥过不去的图", 189, "LEVEL_WORKSHOP", "MarioTrickster/Level Workshop (关卡工坊)", "", "", "Editor/LevelWorkshopWindow.cs", "public static void Open()"),
        F("ws.wizard", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "向导 · 模式印章 · 起承转合", "工坊工具栏 向导…（选主角机关 + 时长）/ 印章 ▾（点画布盖一组现成机关）/ 起承转合（看分段）", "向导生成的草稿已经能玩；起承转合 = 教 → 加深 → 意外 → 收尾", "不知道从何下手就用向导，再改最别扭的一处", 208, "S208", "", "", "", "LevelDesign/LevelBlueprint.cs", "public static Draft Wizard("),
        F("ws.samples", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "样板 · 监狱塔 · 箱庭总览", "工坊 样板：两层监狱 / 诱捕走廊 / 长廊远征 / 箱庭监狱；监狱塔…（按层数自动拼）；箱庭总览（每层身份、捷径、环路）", "魂系箱庭：先绕远路，再从另一边打开捷径门", "照着样板改是最快的学法；多层监狱看箱庭总览找「无脑堆层」的问题", 196, "S196", "", "", "", "LevelDesign/FloorStacker.cs", "public static string[] Build("),
        F("ws.checks", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "工坊检查：最坏情况 · 策略模拟 · 连招路线 · 检查轨迹（含临界跳 · 试玩卡点）", "工坊工具栏开关", "最坏情况 = 全塌全挡时出不出得去；策略模拟 = 3 颗炸弹能不能把他困死（会自动加固）；检查轨迹 = 自动检查时他走哪、卡哪 + 黄格 = 只能跳满 2 格才出得去（塌后也算）+ 红叉数字 = 真人试玩时他在这卡过几次（自动记录 + 截图）", "画完一关把这 4 个都开一次；黄格旁边加一级台阶", 202, "S202 S240", "", "", "", "LevelDesign/StrategySim.cs", "public static class StrategySim"),
        F("ws.library", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "关卡库 · 导入导出 · PageUp/PageDown 切关", "工坊 关卡库 ▾（打开 / 存入 / 导入网页关卡包）；◀ ▶ 切关自动存", "关卡存在 Assets/Levels/Library，会跟着 bat 一起传到 GitHub", "每关起个名字存进库，小镇的门就能连它", 206, "S206", "", "", "", "Editor/LevelLibrary.cs", "public static List<(string name, string path, List<char> pending)> List()"),
        F("ws.town", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "小镇工坊（画大地图）", "Ctrl+Alt+O；画笔 B 矩形 R 填充 F 橡皮 E 吸管 I；1–9 放门；↔ 扩展（最大 192×128）；F5 试玩；PageUp/PageDown 切小镇", "右边：门的顺序和时间、一天总览（每扇门主打什么）、居民、天气、大机关连锁", "门上的 ✎ 直接跳到关卡工坊改那间房", 210, "S215 S217", "MarioTrickster/Town Workshop (小镇工坊)", "", "", "Editor/OverworldWorkshopWindow.cs", "public static void Open()"),
        F("ws.townplus", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "小镇工坊进阶：⚡ 关系线 · ⛈ 雷区 · 🤖 模拟玩家玩一天", "小镇工坊工具栏 ⚡ 关系线 / ⛈ 雷区（Z 拖框）；右边 🤖 模拟玩家玩一天", "关系线画出炮→靶心、滚石滚道、水塔范围、连锁；模拟玩家 = 4 种机器人各玩 3 天", "改完一张小镇先跑一次模拟玩家，看会躲的人能不能全埋伏上", 213, "S213", "", "", "", "Overworld/OverworldBots.cs", "public static Report PlayDay("),
        F("ws.lines", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "台词编辑器（居民和马里奥说的话）", "Ctrl+Alt+L；或小镇工坊右边 ✎ 台词编辑器…", "改、新加、关掉任何一句；「这句什么时候会说？」当场彩排；存在你自己的 MyTownStories.json，AI 升级永远不覆盖", "先写每户人家 3 句：好笑的、生气的、真心话", 234, "S234", "MarioTrickster/台词编辑器 Lines", "", "", "Editor/TownStoryEditorWindow.cs", "public static void Open()"),
        F("ws.dice", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "🎲 灵感骰子", "小镇工坊右边 🎲 / 网页大地图页 灵感骰子", "随机给一个「谁家门口 + 天气 + 机关 + 限制」的题目（不改地图）", "卡住时掷一次，照着题目做一个房间或写一句台词", 233, "", "", "", "灵感骰子", "Overworld/IdeaDice.cs", "public static Idea Roll("),
        F("ws.tuning", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "调参（RushMarioTuning）· 数值关系", "开始页点「打开」（或 Project 窗口 Assets/Resources/Step1/RushMarioTuning），在 Inspector 里改：⭐ 常用 15 个 + 9 组折叠、能搜、只看改过的、↺ 恢复默认", "所有数值都在这一个文件（250 项，每项都有中文说明）：马里奥速度、视野、炸弹数、晕几秒、游戏速度…；数值之间有矛盾会直接标黄", "一次只改一个数，玩两局对比；● 标出你改过哪些，改乱了点 ↺；马里奥性格是一个下拉", 238, "S226 S237 S238", "", "", "数值关系", "Editor/MarioMindTuningSOEditor.cs", "[CustomEditor(typeof(MarioMindTuningSO))]"),

        // ④ 网页设计台
        F("web.design", "④ 网页设计台", "常用", "网页：画关卡", "双击安装包里的 MarioTrickster关卡设计台.html（或测试中心 🌐 网页设计台）", "和 Unity 工坊同一套元素和检查；能看「马里奥一趟几秒走到哪个机关」和节奏条；不用开 Unity 也能画", "在外面（没装 Unity 的电脑）想点子、画草图", 204, "S204", "", "", "画关卡|我的关卡|起步|模式印章|元素|当前笔刷|检查结果|马里奥一趟 · 几秒走到哪个机关|节奏 · 紧张和喘气|问题（点一下定位）|这张图的设计意图", "../../tools/LevelStudioWeb/shell.html", "data-page=\"design\""),
        F("web.town", "④ 网页设计台", "常用", "网页：大地图", "网页顶部 大地图", "画小镇、看门的时间表、居民、台词本、天气、伤害一览、一天总览、大机关连锁、马里奥的一天（时间滑条）", "和 Unity 小镇工坊逐字一致，哪边改都行", 210, "", "", "", "大地图|格子|房间门|门（马里奥按时间顺序去）|检查|住户与故事|台词本|雷区|伤害一览|一天总览|大机关 · 连锁|天气|马里奥的一天", "../../tools/LevelStudioWeb/shell.html", "data-page=\"overworld\""),
        F("web.mech", "④ 网页设计台", "进阶", "网页：新机制提案 · 想删掉的旧东西", "网页顶部 新机制提案", "写清新机关是什么、代价、马里奥怎么反制；可以先画进关卡试摆；也能列出想删 / 改的旧东西", "想要新机关时先在这写，再交给 AI", 206, "", "", "", "新机制提案|想删掉 / 改掉的旧东西", "../../tools/LevelStudioWeb/shell.html", "data-page=\"mech\""),
        F("web.handoff", "④ 网页设计台", "常用", "网页：交给 AI（设计单 + 关卡包）", "网页顶部 交给 AI → 复制设计单 / 导出关卡包", "一段文字说清要做什么 + 所有关卡数据，发给 AI 就能升级", "每次想让 AI 做事，带上设计单最省话", 204, "", "", "", "交给 AI", "../../tools/LevelStudioWeb/shell.html", "data-page=\"handoff\""),
        F("web.sync", "④ 网页设计台", "进阶", "网页 ↔ Unity 自动同步 · ▶ 在 Unity 试玩", "网页右上 🔗 连接 Unity 项目（选 MarioTrickster 文件夹，Chrome / Edge）", "网页一改，切到 Unity 自动导入；Unity 存了切回网页自动拿到；覆盖前自动备份", "在网页画、按 ▶ 在 Unity 试玩，来回不用导入导出", 214, "S214", "MarioTrickster/检查与记录 Checks/📂 网页同步收件箱 (Inbox)|MarioTrickster/检查与记录 Checks/📂 网页同步备份 (History)", "", "", "Editor/WebSync.cs", "public const string Inbox"),
        F("web.import", "④ 网页设计台", "进阶", "网页导入（像素图 · 文字 · 数字表）", "网页 画关卡 → 导入（多种格式）", "一张小像素图、一段文字或数字表都能变成关卡（数字要对应到元素）", "在纸上 / 画图软件里画的草图直接导进来", 205, "", "", "", "把数字对应到元素", "../../tools/LevelStudioWeb/shell.html", "id=\"mapDlg\""),
        F("web.map", "④ 网页设计台", "常用", "网页：功能地图", "网页顶部 功能地图", "这一页（和 Unity 开始页同一份）", "忘了某个东西在哪，搜一下", 236, "", "", "", "功能地图", "../../tools/LevelStudioWeb/shell.html", "data-page=\"map\""),

        // ⑤ 测试与反馈
        F("test.start", "⑤ 测试与反馈", "常用", "📖 开始页（功能地图）", "Ctrl+Alt+H；测试中心 / 两个工坊工具栏也有 📖", "最上面「⏱ 上次做到哪」：最近改的关卡 / 小镇（点接着做直接打开）、上次体检 / 测试 / 试玩的结果；下面全部功能按区分好，能搜，「我想…」按目标给出步骤", "每次打开 Unity 先看「上次做到哪」，接着做；升级后看一眼「🆕 最近新增」", 237, "S236 S237 FEATURE_MAP", "MarioTrickster/📖 开始页 Start Here", "", "", "Editor/StartHereWindow.cs", "public static void Open()"),
        F("test.hub", "⑤ 测试与反馈", "常用", "测试中心（一键体检 · 试玩 · 打包反馈）", "Ctrl+Alt+T：① 一键体检 → ② 试玩 → ③ 打包反馈", "不进 Play 就检查全部关卡和小镇、机器人玩一天、数值关系、台词、你的试玩记录；打包截图和记录给 AI", "每次改完东西点一次 ①", 217, "S217", "MarioTrickster/测试中心 Test Hub", "", "", "Editor/TestHubWindow.cs", "public void RunHealthCheck()"),
        F("test.quick", "⑤ 测试与反馈", "常用", "⚡ 快速测试模式", "测试中心顶部勾选", "不弹玩法说明、房间结束不弹问卷（照样记一行）", "反复测时开着，认真玩时关掉", 217, "", "", "", "", "Gameplay/Step1/Step1QuickTest.cs", "public const string Key"),
        F("test.exit", "⑤ 测试与反馈", "参考", "第 1 步出口报告 · 小镇记录", "测试中心 ① 一键体检，往下翻", "从你的试玩记录自动算：连玩几局、坑法几种、还想再来几分、改版前后对比、建议", "玩够 5 局再看，比凭感觉准", 227, "S227", "", "", "", "Gameplay/Step1/Step1ExitReport.cs", "public static string Markdown("),
        F("test.handsoff", "⑤ 测试与反馈", "自动", "马里奥自己跑（H10 自动检查）", "测试中心 🤖 马里奥自己跑；或菜单 检查与记录 → 🤖 马里奥自己跑", "你不操作，连跑几局看马里奥能不能自己拿宝回家（要 ≥95%）", "改了房间结构或马里奥数值后跑一次", 181, "", "MarioTrickster/检查与记录 Checks/🤖 马里奥自己跑 Hands-off (H10)", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void HandsOffMenu()"),
        F("test.probe", "⑤ 测试与反馈", "自动", "陷阱试探（AI 捣蛋者替你坑）", "测试中心 🎯 陷阱试探；或菜单 检查与记录 → 🎯 陷阱试探", "AI 在马里奥走到时触发每个机关，看连起来会不会把他坑死 / 卡住", "机关很多的房间跑一次，再开工坊「检查轨迹」看他卡在哪", 202, "", "MarioTrickster/检查与记录 Checks/🎯 陷阱试探 Trap Probe", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void TrapProbeMenu()"),
        F("test.unit", "⑤ 测试与反馈", "自动", "EditMode 测试 · 测试报告", "测试中心 🧪 跑 EditMode 测试（唯一入口；要连 PlayMode 一起跑：菜单 检查与记录 → 测试报告 全部）", "几百条自动检查，结果写进 TestReport.txt，打包反馈会带上", "每次装完升级包跑一次", 160, "", "MarioTrickster/检查与记录 Checks/测试报告 EditMode|MarioTrickster/检查与记录 Checks/测试报告 PlayMode|MarioTrickster/检查与记录 Checks/测试报告 全部 All|MarioTrickster/检查与记录 Checks/打开上次测试报告", "", "", "Editor/TestReportRunner.cs", "public static void RunEditModeTests()"),
        F("test.files", "⑤ 测试与反馈", "自动", "试玩记录文件夹 · 房间场景", "测试中心 📂；或菜单 检查与记录 → 📂 试玩记录文件夹 / 重建房间场景 / 打开房间场景", "step1_rounds.csv、town_days.csv、F8 截图都在这；场景会自动重建，一般不用手动", "想自己看数据时打开", 181, "", "MarioTrickster/检查与记录 Checks/📂 试玩记录文件夹|MarioTrickster/检查与记录 Checks/重建房间场景 Build Room|MarioTrickster/检查与记录 Checks/打开房间场景 Open Room", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void OpenLogsMenu()"),

        // ⑥ 美术
        F("art.town", "⑥ 美术", "进阶", "小镇像素图标：导出模板 · 检查换上的图", "菜单 美术 → 小镇：导出像素图标模板 / 检查换上的像素图", "每种格子一个 16×16 图标；换图后检查尺寸、颜色、描边", "想换小镇画风时先导出模板给画师", 220, "", "MarioTrickster/美术 Art/小镇：导出像素图标模板（给美术换图）|MarioTrickster/美术 Art/小镇：检查换上的像素图（尺寸、颜色、描边）", "", "", "Editor/OverworldArtTools.cs", "public static void ExportTemplates()"),
        F("art.pipeline", "⑥ 美术", "进阶", "素材导入 · 套用到选中物体 · 智能切图", "菜单 美术 → 素材导入 / 套用到选中物体 / AI 智能切图 / 特效工厂 / 特效快速套用 / 溶解噪声贴图 / 工具", "把商业素材切好套到白盒上，只换外观不动碰撞；特效工厂给角色加描边、溶解、闪白", "玩法定了再换美术（现在都是方块）", 150, "ASSET_IMPORT_PIPELINE_GUIDE", "MarioTrickster/美术 Art/素材导入 Asset Import Pipeline|MarioTrickster/美术 Art/套用到选中物体 Apply Art to Selected|MarioTrickster/美术 Art/AI 智能切图 Smart Slicer|MarioTrickster/美术 Art/工具 Pipeline/|MarioTrickster/美术 Art/特效工厂 Sprite Effect Factory|MarioTrickster/美术 Art/特效快速套用 SEF Quick Apply|MarioTrickster/美术 Art/生成溶解噪声贴图 Dissolve Noise", "", "", "Editor/AssetImportPipeline.cs", "public class AssetImportPipeline"),
        F("art.theme", "⑥ 美术", "进阶", "主题美术检查 · 导出元素图例", "菜单 美术 → 主题美术检查 / 导出元素图例", "检查主题缺哪些图；导出全部元素的说明表", "给画师一张清单：每个元素要什么尺寸", 187, "", "MarioTrickster/美术 Art/主题美术检查 Check Theme Art|MarioTrickster/美术 Art/导出元素图例 Element Legend", "", "", "Editor/ElementLegendExporter.cs", "public static void ExportMenu()"),

        // ⑦ 给 AI · 换账号
        F("ai.pack", "⑦ 给 AI · 换账号", "常用", "安装包 · 接续包（换账号也能接上）", "每次升级的 zip：01 双击 bat 选 Y 上传；02 接续包 .skill 发给新账号的 AI", "AI 不靠记忆，靠接续包和仓库里的文档接着做", "换账号、换电脑时把 .skill 和网页设计单一起发", 200, "DESIGN_CONSTITUTION", "", "", "", "../../docs/AI_CONTINUE_PACK/SKILL.md", "name: mariotrickster-continue"),
        F("ai.research", "⑦ 给 AI · 换账号", "参考", "调研与总方案文档", "docs/step1 里的 S222 / S225 / S230 / S231 / S239 等", "网上资料对照、批评、执行方案；S239 = 按好玩整合、删掉了哪些重复系统以及为什么；每次「继续」AI 都按这些走", "想知道「为什么这样设计」「某个旧系统去哪了」时读", 239, "S222 S225 S230 S231 S239", "", "", "", "../../docs/step1/S222_RESEARCH_MASTERPLAN.md", "S222"),

        // ⑧ 安全网（S238：旧工具已删）
        F("old.redline", "⑧ 安全网", "自动", "红线巡检（碰撞体 / 缩放不许乱改）", "自动；菜单 安全网 → 红线巡检 / 红线自动修复", "防止换美术时把角色碰撞体改坏", "不用管；报警了截图给 AI", 50, "", "MarioTrickster/安全网 Safety/红线巡检 Red Line Check|MarioTrickster/安全网 Safety/红线自动修复 Red Line Auto-Fix|MarioTrickster/安全网 Safety/启用红线自动修复", "", "", "Editor/RedLineGuard.cs", "public static class RedLineGuard"),
    };

    /// <summary>「我想…」：按目标串起几个功能（开始页顶部 / 网页 / 文档同一份）。</summary>
    public static readonly Goal[] Goals =
    {
        G("做一个新房间", "ws.wizard ws.room ws.checks room.play test.hub", "向导选主角机关 → 工坊改最别扭的一处（红格必须改）→ 开最坏情况 / 策略模拟看一眼 → ▶ 试玩 → 体检"),
        G("把房间放进小镇的一天", "ws.library ws.town ws.townplus town.play", "房间存进关卡库 → 小镇工坊放门（1–9）连上它 → 看一天总览 → 🤖 模拟玩家 → F5 试玩"),
        G("让小镇有故事", "town.stories ws.lines ws.dice", "给每扇门起名字和性格 → 台词编辑器每户写 3 句 →「这句什么时候会说？」彩排 → 卡住掷灵感骰子"),
        G("反复试一个坑", "test.quick room.f9 room.feedback", "开快速测试 → 进房间按 F9 技能无限 → 连着试 → 别扭的瞬间按 F8"),
        G("测一轮并交给 AI", "test.hub room.survey test.exit ai.pack", "关掉快速测试，认真玩 5 局答问卷 → ① 一键体检看出口报告 → ③ 打包反馈 → 发给 AI"),
        G("加一个新机关", "web.mech web.handoff ai.pack", "网页 新机制提案 写清代价和反制 → 先画进关卡试摆 → 交给 AI 复制设计单 → 发给 AI"),
        G("在没装 Unity 的电脑上想点子", "web.design web.town web.import web.sync", "双击网页设计台 → 画关卡 / 大地图 → 回到 Unity 电脑连接项目自动同步"),
        G("换画风", "art.town art.theme art.pipeline", "导出像素图标模板 / 元素图例给画师 → 换图后跑检查 → 商业素材走导入流程"),
    };

    static Goal G(string title, string ids, string steps) => new Goal { title = title, ids = ids.Split(' '), steps = steps };

    public static Feature Get(string id) => All.FirstOrDefault(f => f.id == id);

    /// <summary>最近新增（按 since 从新到旧）。</summary>
    public static IEnumerable<Feature> Recent(int n) => All.OrderByDescending(f => f.since).ThenBy(f => f.id).Take(n);

    /// <summary>搜索：名字 / 怎么打开 / 能做什么 / 创作用法 / 按键 / 网页面板里有这个词。</summary>
    public static IEnumerable<Feature> Search(string q)
    {
        q = (q ?? "").Trim().ToLowerInvariant();
        if (q.Length == 0) return All;
        return All.Where(f => (f.name + f.how + f.what + f.create + f.keys + f.web + f.menu + f.id).ToLowerInvariant().Contains(q));
    }

    /// <summary>菜单路径去掉快捷键后缀（%&w 这类）。</summary>
    public static string StripShortcut(string menu)
    {
        int i = menu.LastIndexOf(' ');
        if (i > 0 && i + 1 < menu.Length && "%#&_".IndexOf(menu[i + 1]) >= 0) return menu.Substring(0, i);
        return menu;
    }

    /// <summary>这个菜单有没有被登记（完全一样，或在某条登记的子菜单 xxx/ 下面）。</summary>
    public static bool Covers(string menuPath)
    {
        menuPath = StripShortcut(menuPath);
        return All.Any(f => f.Menus.Any(m => m.EndsWith("/") ? menuPath.StartsWith(m) : m == menuPath));
    }

    public static bool CoversKey(string keyCodeName) => All.Any(f => f.Keys.Contains(keyCodeName));
    public static bool CoversWeb(string panel) => All.Any(f => f.WebPanels.Contains(panel));
    public static bool CoversDoc(string fileName) => All.Any(f => f.DocTokens.Any(t => fileName.StartsWith(t)));

    /// <summary>docs/FEATURE_MAP.md 的全文（体检要求仓库里的文件 = 这个；改了登记就重新生成）。</summary>
    public static string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# MarioTrickster 功能地图");
        sb.AppendLine();
        sb.AppendLine("> 自动生成，别手改：来源 `Assets/Scripts/LevelDesign/FeatureMap.cs`（Unity 开始页 Ctrl+Alt+H、网页设计台「功能地图」页也读它）。");
        sb.AppendLine($"> 共 {All.Length} 项。常用 = 每次都会用；进阶 = 想深挖时用；自动 = 平时不用管；参考 = 读的东西。");
        sb.AppendLine();
        sb.AppendLine("## 我想…");
        sb.AppendLine();
        foreach (var g in Goals)
            sb.AppendLine($"- **{g.title}**：{g.steps}（用到：{string.Join("、", g.ids.Select(i => Get(i)?.name ?? i))}）");
        sb.AppendLine();
        sb.AppendLine("## 🆕 最近新增");
        sb.AppendLine();
        foreach (var f in Recent(8)) sb.AppendLine($"- S{f.since} {f.name}");
        foreach (var area in Areas)
        {
            sb.AppendLine();
            sb.AppendLine("## " + area);
            sb.AppendLine();
            sb.AppendLine("| 功能 | 级别 | 怎么打开 / 按键 | 能做什么 | 创作时怎么用 | 说明文档 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var f in All.Where(x => x.area == area))
                sb.AppendLine($"| **{Cell(f.name)}** | {f.tier} | {Cell(f.how)} | {Cell(f.what)} | {Cell(f.create)} | {Cell(string.Join(" ", f.DocTokens))} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 防遗忘规则（体检 S236 自动查）");
        sb.AppendLine();
        sb.AppendLine("- 每个 MarioTrickster 菜单、每个游戏按键、网页每个面板、docs/step1 每篇说明，都必须被上面某一项登记。");
        sb.AppendLine("- 每一项的「锚点」代码必须还在：功能删了，这一项也要删（不留死链）。");
        sb.AppendLine("- 新功能先想「放进哪一区、是不是常用」；能并进已有一项的就并，别新开一项。");
        return sb.ToString();
    }

    static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");
}
