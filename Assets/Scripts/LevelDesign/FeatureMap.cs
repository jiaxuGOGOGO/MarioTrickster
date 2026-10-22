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

    public static readonly string[] Areas = { "① 世界主体验", "② 横版挑战片段", "⑨ 存量小镇内核", "③ 做：关卡 · 小镇 · 台词 · 数值", "④ 网页设计台", "⑤ 测试与反馈", "⑥ 美术", "⑦ 给 AI · 换账号", "⑧ 安全网" };
    /// <summary>常用 = 每次都会用；进阶 = 想深挖时用；自动 = 平时不用管，出问题才看；参考 = 读的东西。（S238：「旧工具」一档随旧工具一起删了）</summary>
    public static readonly string[] Tiers = { "常用", "进阶", "自动", "参考" };

    static Feature F(string id, string area, string tier, string name, string how, string what, string create, int since, string docs, string menu, string keys, string web, string anchorFile, string anchorText)
        => new Feature { id = id, area = area, tier = tier, name = name, how = how, what = what, create = create, since = since, docs = docs, menu = menu, keys = keys, web = web, anchorFile = anchorFile, anchorText = anchorText };

    public static readonly Feature[] All =
    {
        F("world.play", "① 世界主体验", "常用", "开始世界", "开始页 → 开始世界；或菜单开始世界", "默认进入主世界「空白之城」（六个原创会场：♣3 三扇门 → ♦5 数字猜心 / ♥7 看不见的标记 / ♠5 鬼抓人 → ♥9 分还是偷 → K 主办者的房间）；霓虹扑克牌亮灯入场、俯视现场调查与鬼抓人追逐、全屏多人博弈桌、揭示时逐张翻开反转卡、GAME CLEAR / GAME OVER 结局章；多会场按作者链接解锁，失败也能走向不同会场；横版是按需片段", "先围绕可知信息和有效选择设计会场，不预设唯一守宝目标", 252, "S249 S250 S251 S252", "MarioTrickster/开始世界 Play World", "", "", "Editor/WorldParkBuilder.cs", "PlayMain"),
        F("world.journey", "① 世界主体验", "常用", "继续旅程（世界检查点）", "开始页 → 继续旅程；或世界工坊工具栏 继续旅程；暂停菜单可手动保存", "在剧情、揭示、结局等安全阶段自动保存；只认同一版世界内容，改了世界不会串用旧进度；房间/探索中途不存档，避免刷结果", "做多会场旅程时，用继续旅程检查跨会场解锁；想重来就点开始世界（旧档保留为.previous）", 250, "S250", "", "", "", "Editor/WorldParkWindow.cs", "ContinueCurrent"),
        F("world.explore", "① 世界主体验", "常用", "现场调查、线索笔记与鬼抓人", "WASD移动；E靠近调查/选择；N笔记；C镜头；Esc暂停", "真正进入地图、近身且可见才观察线索；出口结果来自活动判定，不是文字彩排；鬼（chaser 点）沿巡逻路线走动、只看见面前扇形，看见你就追、跟丢会去搜寻，站在高草里不动能躲、在草里走动草会晃；限时会场最后几秒大字倒计时", "在世界工坊点位画布上直接点放调查点、出口与鬼（只允许从出生点T走得到的格子）；真相只放揭示阶段；改了鬼的路线就跑剧情带入模拟看“鬼的威胁/追逐张力”", 252, "S249 S250 S252", "", "W A S D E N C Escape", "", "WorldPark/WorldParkExploreRuntime.cs", "RequestInteract"),
        F("world.game", "① 世界主体验", "常用", "多人博弈桌（数字猜心 · 看不见的标记 · 分还是偷）", "开始世界 → 会场里的博弈阶段自动打开全屏博弈桌：滑块/按钮写数字、宣告花色、选分或偷；右上角“规则与记录”随时查完整公开记录；亮牌时点击或空格跳过", "原作式规则博弈：每人有命数，NPC 只按人格和公开信息行动（直觉、推一层、深推、跟随、说谎、老实、以牙还牙）；别人对你说的话显示在他的座位旁，每轮所有人同时亮牌并逐张翻开，桌上标出谁失去命、谁出局（激光）、新规则宣布；数字猜心有人出局后逐轮追加规则，只剩两人时宣布决斗规则；整局结束才提交真实胜负，中途不存档", "先写清公开规则，再挑参加者人格；用剧情带入模拟确认至少一种只用公开信息的打法能活下来、至少一种打法会出局", 252, "S251 S252", "", "Space Return", "", "WorldPark/WorldParkRuntime.cs", "private void DrawGameTable("),
        F("world.stage", "① 世界主体验", "常用", "世界演出（亮灯 · 激光 · 结局章 · 倒计时）", "开始世界后自动出现；模态演出点击或空格跳过", "进入会场时霓虹扑克牌通电亮起并报出会场名与规则要点；活动失败或有人出局时激光落下；结局盖 GAME CLEAR / GAME OVER 章；限时会场最后几秒大字倒计时，被鬼看见时画面边缘闪红；演出只读会话状态，不改任何结果", "会场的花色(suit)与难度决定扑克牌（如 ♦5）和红/蓝色调；想要更紧张就缩短限时", 252, "S252", "", "Space Return", "", "WorldPark/WorldParkStage.cs", "public static void Show("),
        F("world.scene", "① 世界主体验", "常用", "剧情字幕卡与反转卡", "开始世界 → 剧情 / 揭示 / 结局阶段自动全屏；点击显示全部，空格或右下按钮继续；左下“线索本”“旅程”", "电影画幅的全屏字幕卡：正文逐字出现；揭示时把你发现过的线索逐张翻成“当时以为 → 其实”，没发现的只告诉你错过了；结局盖章，下一个会场显示为可点的门（关着的门写明解锁条件）", "线索的 hint 写玩家当时会怎么理解，truth 写反转后的真相；博弈会场没有地图时，用 trigger=stage 的入场观察来埋反转", 252, "S252", "", "Space Return", "", "WorldPark/WorldParkScene.cs", "public static string Draw("),
        // S254 Windows 电脑端 + 创作便利
        F("world.title", "① 世界主体验", "常用", "标题页 · Esc 菜单 · 设置 · 重来本会场", "Windows 版双击 exe 先到标题页（继续旅程 / 开始新旅程 / 设置 / 退出）；游戏中随时按 Esc；GAME OVER 结局底栏有“重来本会场”", "Esc 菜单：继续、设置（界面大小 85%–150%、音量、全屏）、保存检查点（安全阶段）、重来本会场（回到刚进本会场时，别的会场结果保留）、回到标题、退出；界面按 1080p 自动放大，字小就调界面大小；设置存在本机", "试玩时觉得某个会场要重打：Esc → 重来本会场，不用从头玩", 254, "S254", "", "Escape", "", "WorldPark/WorldParkUi.cs", "public static string DrawPauseMenu("),
        F("world.build", "① 世界主体验", "常用", "构建 Windows 版（双击就能玩的 exe）", "世界工坊“构建 Windows 版”/ 开始页同名按钮 / 菜单 世界工坊 → 构建 Windows 版；Unity 关着时双击根目录 BUILD_WINDOWS_GAME.cmd", "把当前世界（优先「我的世界」）连同需要的探索、房间场景打包到 Builds/Windows/<世界id>/，附“先读我.txt”；不改 Build Settings；整个文件夹拷给别人就能玩", "做完一版世界就构建一次，在真正的窗口里玩；需要 Unity 许可证已激活", 254, "S254", "MarioTrickster/世界工坊 World Workshop/构建 Windows 版 Build Windows", "", "", "Editor/WorldParkBuilder.cs", "public static bool BuildWindows("),
        F("world.myworld", "① 世界主体验", "常用", "我的世界（网页 ↔ Unity 同一个文件）", "网页世界页“🔗 写进 Unity 项目”（选一次项目文件夹）；Unity 世界工坊“我的世界”一栏：载入我的世界 / 保存为我的世界", "两边共用 Assets/Resources/WorldPark/MyWorld.worldpark.json：网页每次改动 1 秒后自动写入；Unity 世界工坊在草稿没有未保存改动时自动载入新版本，有改动时只提示、不替换；开始世界与构建 Windows 版都优先用它", "在网页里想关卡、在 Unity 里实际玩：不用再手动导出 / 导入 JSON", 254, "S254", "", "", "🔗 写进 Unity 项目", "Editor/WorldParkWindow.cs", "private void SyncMyWorld("),
        F("world.venuetools", "① 世界主体验", "常用", "会场模板 · 复制会场 · 只玩这个会场", "世界工坊会场区：模板下拉 +“用模板新建会场”“复制此会场”“▶ 只玩这个会场”；网页世界页同名按钮", "把主世界“空白之城”里做好的会场（地图、鬼、线索、反转、博弈参数）整份拷成你的新会场（名字加“（我的改编）”）；只玩一个会场时从它的入口直接开始，有单独的检查点", "调整好框架：先复制再改，原会场不动；改完立刻 ▶ 只玩这个会场", 254, "S254", "", "", "＋ 用模板新建|▶ 只玩这个会场", "Editor/WorldParkWindow.cs", "private void AddFromTemplate("),
        F("web.venuecheck", "④ 网页设计台", "常用", "会场体检（红 / 黄 / 绿即时反馈）", "网页世界页中栏下方“会场体检”卡：每次改动 0.35 秒后自动刷新", "不用开 Unity 就知道这个会场能不能玩、好不好玩：骨架（规则、活动、反转、结局、门）、现场（最快路线 + 读卡时间对比限时；有鬼时真跑一遍“直奔出口”看会不会被抓；鬼比玩家快、出生点太近、路线旁没草）、博弈（无脑出法能不能赢、看穿后能不能赢）", "红色先改；黄色是取舍提醒（知道为什么就可以留着）；改完再看剧情带入模拟确认体验", 254, "S254", "", "", "会场体检", "../../tools/LevelStudioWeb/venue-check.js", "WPCheck"),
        // S256 世界工坊安全网
        F("web.safety", "④ 网页设计台", "常用", "世界工坊安全网（撤销 / 改动记录 / 恢复原版 / 人话报错）", "网页世界页顶部安全条：↶ 撤销 · ↷ 重做 · 🕘 改动记录 · 显示高级格子；Ctrl+Z 撤销、Ctrl+Y 重做（光标在文本框里时是撤销打字）", "每一步改动都进撤销历史（关掉网页再开还在），安全条说清刚才改了什么、必须修的问题数和体检红黄怎么变；主世界会场和模板副本自动对比原版，可一键恢复整个会场或只恢复一个阶段；结构预检写清在哪、为什么、怎么修，带“去修 / 把点挪到最近的空地 / 恢复这个阶段的原版 / 撤销最近一步”按钮；“只玩这个会场”被拦时就在按钮下方说明原因；编号、玩家角色、整张替换地图这类平时用不到的格子默认收起", "误操作了先点 ↶ 撤销；改乱了看“和内置原版相比”一键恢复；红色问题直接点修复按钮；没点“🔗 写进 Unity 项目”前改动只在浏览器里", 256, "S256", "", "", "↶ 撤销|↷ 重做|🕘 改动记录|显示高级格子", "../../tools/LevelStudioWeb/worldpark-history.js", "WPHistory"),
        // S259 任务式实操学习、问题修复与调研知识保留
        F("doc.guidedtest", "④ 网页设计台", "常用", "逐步人工测试记录与调研待办", "docs/guide/GUIDED_TEST_LOG.md（START_HERE.md、指南和课程有链接）", "每轮一个实际任务：先讲用途、连续实践、成果一次回报；基础操作确认后不逐键重复；卡住才拆细定位、修复、自动回归和原任务复测；记录备份与同步、网页创作、真实试玩、子创作、Unity 与 Windows；保留 S258 来源与未做候选，不把自动验证算人工通过", "想边学边用边修时，从 T01 打开网页开始；看不懂和找不到按钮也是反馈", 259, "S259", "", "", "", "../../docs/guide/GUIDED_TEST_LOG.md", "逐步人工测试记录与调研待办"),
        // S255 创作说明
        F("doc.course", "① 世界主体验", "常用", "项目上手课程（10 课：从当玩家到提改进）", "docs/guide/PROJECT_COURSE.md（START_HERE.md 有链接）", "按顺序带你掌握整个项目：项目全景图 → 用两种性格各玩一遍 → 拆 ♣3 → 六个会场各自的设计与反转（剧透）→ 一次只改一处并对照预期 → 用会场设计单做第一个会场 → 看懂会场体检、剧情带入模拟与一键验证保证了什么 → Unity 与 Windows 版自测 → 文件在哪、旧系统是什么 → 四类问题、提问清单、现有积木能做 / 还做不到、反馈模板", "第一次接手、想系统地看懂项目、想知道怎么把“感觉不对”变成改进意见时读；每课有自测题（答案折叠）", 255, "S255", "", "", "", "../../docs/guide/PROJECT_COURSE.md", "项目上手课程"),
        F("doc.worldguide", "④ 网页设计台", "常用", "世界工坊上手指南（带截图）", "docs/guide/WORLD_WORKSHOP_GUIDE.md（START_HERE.md 有链接）", "用 ♣3 三扇门逐格拆解：四层积木（世界 / 会场 / 阶段 / 点位与线索）、页面五个区域、你填的每个格子在游戏里出现在哪、各种阶段怎么填、地图字符表、出口与鬼的参数、线索与反转的出现条件、博弈参数逐项解释、门与结局标签、东西存在哪会不会丢、三个上手练习", "第一次打开世界工坊、看不懂某个格子、想从零做新会场时读；最后一节是待改进的难用之处清单", 255, "S255", "", "", "", "../../docs/guide/WORLD_WORKSHOP_GUIDE.md", "世界工坊上手指南"),

        // ① 玩：恶作剧房间
        F("room.play", "② 横版挑战片段", "常用", "预览横版挑战片段", "世界工坊 → 横版片段 → 预览当前片段；只是片段检查，不是平行主游戏", "你是蓝色捣蛋鬼，马里奥（红）自己去右边拿宝再跑回左边出口；你用机关坑他、别被抓 3 次", "画完房间都要来这里玩一局：别人怎么玩、马里奥怎么走，一局就看清", 181, "STEP1_PRANK_ROOM", "MarioTrickster/世界工坊 World Workshop/横版片段/预览当前片段 Play Room", "Return", "", "Editor/Step1PrankRoomBuilder.cs", "public static void PlayMenu()"),
        F("room.basic", "② 横版挑战片段", "常用", "走 · 跳 · 伪装 · 触发", "← → 走，↑ 跳，P 伪装（站着别动），L 触发身边的机关", "伪装时被他看见还在动会起疑；触发有预警，他走到才有用", "摆机关时想清楚：你躲在哪伪装、他从哪走过来、你按 L 时他在不在范围里", 182, "S209", "", "LeftArrow RightArrow UpArrow P L", "", "Gameplay/Step1/Step1FailFeedback.cs", "Step1Keys.Down(KeyCode.P)"),
        F("room.traps", "② 横版挑战片段", "常用", "机关全家（火 · 封路墙 · 塌桥 · 弹簧 · 香蕉皮 · 裂缝地板 · 油桶 · 铁笼 · 绳套 · 道具箱 · 毒池 · 黏胶 · 捷径门 · 裂墙 · 通风管）", "工坊左边元素栏点一下就能画；每个元素的说明在工坊悬停或元素图例", "每种机关平时安全、你触发才动；会塌会挡的东西体检按最坏情况算，保证马里奥永远有路", "一关主打 1–2 种机关，其余当配角；同一种坑第 3 次他就不上当了", 193, "S193 S199 ELEMENT_LEGEND", "", "", "", "LevelDesign/ElementCatalog.cs", "I('~', \"FireTrap\""),
        F("room.bomb", "② 横版挑战片段", "常用", "B 炸弹 · Z 缩小 · ↓ 通风管", "现形时按 B 放炸弹（3 枚）；Z 缩小 5 秒钻窄缝（2 次）；站在管口按 ↓ 钻到配对的管口", "炸弹能炸裂墙/箱子/裂缝地板、炸晕他，但他听得见；缩小时不能伪装；通风管他进不去但听得见", "裂墙后面藏近路、通风管连上下两层 = 让你有地方逃、有地方埋伏", 197, "S197 S198", "", "B Z DownArrow", "", "Gameplay/Step1/TricksterKit.cs", "Step1Keys.Down(KeyCode.B)"),
        F("room.decoy", "② 横版挑战片段", "常用", "G 诱饵 · 马里奥踢门 · 警报", "现形时按 G 放一个假的你（每局 1 个）", "他会去追诱饵，走近会识破；警报响时站着不动也可疑；他会踢开挡路的门", "把诱饵放在机关前面 = 把他引进坑里", 199, "S199", "", "G", "", "Gameplay/Step1/DecoyAbility.cs", "Step1Keys.Down(KeyCode.G)"),
        F("room.chain", "② 横版挑战片段", "常用", "F 连锁 · T 挑衅 · 绊线（以身入局）", "走到机关旁按 F 编号①②③（Shift+F 一键全编）；T 挑衅把他引过来（3 次）；他踩到绊线 R 或你按 L 任意一环就开始", "一环接一环自动触发；编号只在你附近 / 连锁进行中显示，下一环放大，环与环之间有点线，右上角列出顺序；用过的机关变灰、不能再编号；完美连锁会放一段回放（空格跳过）", "设计一条「他追你 → 踩绊线 → 一串机关」的走廊，诱捕走廊样板就是示范", 200, "S200", "", "F T", "", "Gameplay/Step1/ChainPlan.cs", "Step1Keys.Down(KeyCode.F)"),
        F("room.arm", "② 横版挑战片段", "常用", "按 L 不怕早：机关预约 · 没打中退还", "他还没走到就按 L = 机关进入预约（头顶沙漏），等他走进范围自动发动；再按一次 L = 立刻发动", "预约最多等 5.5 秒；早一点晚一点都有效果（范围放宽一点）；没打中他 = 次数退回、冷却减半；坐炮 / 灯 / 绳套这类一按就生效的不预约", "机关摆在他必经路上，你只管提前按", 241, "S241", "", "L", "", "Ability/TricksterAbilitySystem.cs", "private void ArmProp(IControllableProp prop)"),
        F("room.light", "② 横版挑战片段", "常用", "白天 · 黑夜 · 雨天 · 马里奥的手电筒 · 灯", "每局轮换天气（左上角写着）；你头顶写着 暗 / 亮", "白天他看得见你；夜里他只看得见手电筒照到的、灯 i 照到的、火和爆炸照到的；暗处只能靠听（夜里你跑步有脚步声）；雨天脚步声更小；灯伪装在旁按 L 灭 8 秒", "灯放在他必经的路口，你就得先灭灯再过去", 241, "S241", "", "L", "", "Gameplay/Step1/Step1Lighting.cs", "Mode = Step1Stealth.Resolve(tuning.lightMode, round);"),
        F("room.burrow", "② 横版挑战片段", "进阶", "U 遁地 · 土包 · 被踩出来", "站在地面上（没伪装）按 U 钻进地下，←→ 移动，再按 U 出来（最多 6.5 秒）", "白天裸地上会拱出土包，他看得见；草地 v 上、夜里、雨天看不见土包；他踩到土包或用扫描找到你 = 你被翻出来晕 1 秒", "草地和裸地交替铺 = 有藏有露的遁地路线", 241, "S241", "", "U", "", "Gameplay/Step1/TricksterBurrow.cs", "Step1Keys.Down(KeyCode.U)"),
        F("room.silk", "② 横版挑战片段", "进阶", "K 蛛丝：挂天花板荡过去", "按 K 朝前上方喷丝挂住天花板，自动摆荡；↑↓ 收放丝（收短荡得更快），←→ 加力，再按 K 松手", "最长 7 秒；从他头顶荡过：头顶有光会被发现，暗处不会；你用的时候不能伪装 / 放炸弹", "房间顶上留空 = 给你荡的空间；在两盏灯中间的暗处荡最安全", 241, "S241", "", "K UpArrow DownArrow", "", "Gameplay/Step1/TricksterSilk.cs", "Step1Keys.Down(KeyCode.K)"),
        F("room.cannon", "② 横版挑战片段", "进阶", "大炮：瞄准开炮 · 坐进去自己飞 · 轰出窗户", "伪装在炮旁：←→ 调方向 ↑↓ 调仰角，L 开炮（每局 1 发）；没伪装站在炮口按 ↓ 坐进去，方向键瞄准（虚线 = 会飞到哪），空格发射，落地就能再进", "炮弹能打开裂墙、引爆油桶；坐炮飞约 15 格远；在小镇房间里打中他，回小镇时他会从门口被轰出来", "炮口对着油桶或裂墙 = 一炮两用；两门炮对着放 = 你的空中快线", 187, "S187 S240", "", "UpArrow DownArrow LeftArrow RightArrow Space", "", "LevelElements/Traps/PranksterCannon.cs", "Step1Keys.Down(KeyCode.Space)"),
        F("room.combo", "② 横版挑战片段", "常用", "连招 · 顿帧 · 回放", "任意两下坑到他间隔不到 4 秒 = 连击（换不同机关分更高）；他头顶的黄条 = 还剩多久能接上；弹窗写清楚是哪几下（如 炮弹 → 香蕉皮）；完美连锁放回放，空格 / B / F / G / T 跳过", "有连招计数、顿帧、震屏；同种机关重复用伤害递减；连击和 F 连锁是两回事：连击看时间，连锁看编号", "把 2–3 个不同机关放在 4 秒内能连上的距离；工坊「连招路线」会画给你看", 193, "S202", "", "Space", "", "Gameplay/Step1/ChainReplay.cs", "Step1Keys.Down(KeyCode.Space)"),
        F("room.feel", "② 横版挑战片段", "参考", "手感：弹飞抛物线 · 受伤闪 · 冲击环 · 震屏", "自动，不用操作", "被弹簧/炸弹/炮弄飞有重力抛物线，落地才恢复；连锁有火花线看得出因果", "觉得「生硬、不自然」时截图按 F8，说是哪一下", 216, "S216", "", "", "", "Gameplay/Step1/Step1Feel.cs", "public static class Step1Feel"),
        F("room.mario", "② 横版挑战片段", "常用", "看得懂马里奥（视锥 · ? ! · 声音圈 · 性格 · 中招反应）", "看屏幕：白色扇形 = 视野，黄色灌满 = 起疑，蓝圈 = 声音传多远；头顶 ? 起疑 ! 来查看 !! 追你", "他只凭看见、听见、记住的东西判断（不作弊）；三种性格：冲冲 / 谨慎（绕开吃过亏的地方）/ 贪财（一定去抢道具箱）", "给他留「看起来安全」的近路，坑就埋在那；草丛、箱子、高墙是你的藏身处", 203, "S203 S223", "", "", "", "Gameplay/Step1/MarioPersonality.cs", "enum"),
        F("room.read", "② 横版挑战片段", "常用", "V 这是什么 · M 图例 · C 镜头 · H 帮助 · Esc 暂停", "V 给每个东西标名字；M 或 Tab 地图图例；C 换镜头（大房间跟着你走 + 小地图 + 屏外红箭头）；H 全部按键；Esc 暂停", "底部按键条只显示「现在按了有用」的键", "大房间（长廊远征）用 C 切到「整个房间」看全局", 207, "S207", "", "V M Tab C H Escape", "", "Gameplay/Step1/Step1ElementLabels.cs", "Step1Keys.Down(KeyCode.V)"),
        F("room.timestop", "② 横版挑战片段", "参考", "马里奥的反制：停时间 · 踢门 · 抢道具", "自动；屏幕边缘变蓝 = 他要停时间了，快躲（草丛 / 通风管）", "每种优势都有代价和反制：你有机关，他有停时间、护盾、加速", "做难关时多放草丛和通风管，给你躲停时间的地方", 197, "", "", "", "", "Gameplay/Step1/MarioTimeStop.cs", "public static bool ShouldTrigger("),
        F("room.survey", "② 横版挑战片段", "进阶", "结算问卷 · R 重来 · N 下一局", "一局结束回答 5 道题（Y / N / 数字 1–5，最后一题可写一句话）；R 从头开始，N 下一局", "答案写进试玩记录，测试中心体检自动算「第 1 步出口」和建议", "想认真记感受就关掉快速测试模式；只想反复测就开着（不弹问卷）", 186, "STEP1_PLAYTEST_SHEET", "", "Y N R", "", "Gameplay/Step1/Step1PlaytestLog.cs", "survey.AnswerYesNo(true)"),
        F("room.fallout", "② 横版挑战片段", "自动", "防卡死：卡住救援 · 掉出房间", "自动", "身体卡进墙会推出来（只往里推）；马里奥卡住会被放回路上；你掉出房间 = 回出生点 -1 条命", "看到「马里奥掉出房间 → 已放回」= 布局有口子，按 F8 记一下", 209, "S209 S235", "", "", "", "Gameplay/Step1/Step1RoomGuard.cs", "class Step1RoomGuard"),
        F("room.f9", "② 横版挑战片段", "进阶", "F9 测试：技能无限 · F5 重开", "游戏里按 F9 开 / 关（左上角会写「F9 测试」）；F5 马上重开这一局", "炸弹、缩小、诱饵、挑衅用不完、没冷却——专门用来反复试一个机关；开过 F9 的局不算进第 1 步出口", "调一个坑的位置时开着 F9，连着试十次不用重开", 236, "S236", "", "F9 F5", "", "Gameplay/Step1/Step1QuickTest.cs", "public static bool NoLimits"),
        F("room.feedback", "② 横版挑战片段", "常用", "F8 记反馈（截图 + 最近 25 秒黑匣子）", "游戏里随时按 F8，3 秒内按 1–4 打标签（卡住 / 别扭 / 看不懂 / bug）；卡住、按了没反应、顿卡、红字错误会自动记", "每条 = 缩小的截图 + 黑匣子（最近 25 秒马里奥和你的位置 / 状态 / 按键 + 出事前发生了什么 + 房间快照）；测试中心 ③ 打包时自动总结、控制大小（默认 8MB）", "看到别扭、不懂、卡住的瞬间就按，不用写字", 217, "S217 S242", "", "F8 Alpha1 Alpha2 Alpha3 Alpha4", "", "Gameplay/Step1/Step1Feedback.cs", "Step1Keys.Down(KeyCode.F8)"),
        F("room.loadout", "② 横版挑战片段", "常用", "伪装装备栏：变成房间里的东西 · 1–N 切换 · E 取样", "屏幕底部一排小格 = 你能变的形态（默认 = 这个房间里最多的 3 种东西）；数字键 1–N 换，伪装中也能换；站在箱子 / 草丛 / 油桶 / 灯旁边按 E 把它放进当前格", "变出来的外观和房间里那个一模一样（图 + 颜色 + 大小 + 图标）；伪装中换形态的一瞬间，他正看着你 = 算你动了（会起疑）", "房间里同一种东西摆多几个 = 你藏得更好；调参 disguiseLoadoutSize 改带几种（1–5）", 242, "S242", "", "E", "", "Gameplay/Step1/TricksterLoadout.cs", "Step1Keys.Down(KeyCode.E)"),
        F("room.propdecoy", "② 横版挑战片段", "常用", "G 丢假道具（诱饵 = 你当前的形态）", "装备栏选好形态，按 G 朝前丢出一个假的（伪装中也能丢，每局 2 个）；Shift+G = 丢以前那种假的你", "假道具落地后时不时扭一下：他看见会当成可疑的东西过来查看，走近 2.5 格识破；你可以趁机从另一边溜", "丢在机关前面 = 他走过去查看时正好踩坑", 242, "S242", "", "G", "", "Gameplay/Step1/DecoyAbility.cs", "d.InitProp("),
        F("room.glance", "② 横版挑战片段", "常用", "一目了然：马里奥要走的路 · 埋伏点 · 头顶意图图标", "自动显示（淡淡的路线小点 + 他头顶一个图标）；按 M 打开作战图（路线变亮 + 靶心埋伏点 + 几秒后到 + 身边那个东西的说明卡）", "头顶图标：宝箱 = 去拿宝、门 = 要逃了、? = 起疑、! = 追你、眼 = 找你、星 = 晕了；颜色只有三种：红 = 坑他、蓝 = 躲 / 钻、黄 = 目标", "路线经过的机关就是你的埋伏点——提前站过去伪装好", 242, "S242", "", "M Tab", "", "Gameplay/Step1/Step1GlanceView.cs", "Step1Glance.AmbushesOnRoute("),
        F("room.art", "② 横版挑战片段", "参考", "像素美术：马里奥 = 红帽寻宝人 · 你 = 蓝色小恶魔 · 机关 / 地形 / 背景", "自动（调参 artCharacters / artProps / artTiles / artBackground 分别开关）", "角色站 / 跑 / 跳 / 晕会换帧；火 / 弹簧 / 油桶等整块换成像素图（不再是色块 + 小图标）；地面最上层带草；背景低对比、没描边，不会被当成能站的地方", "想换成自己的图：把同名 16×16 PNG 放进 Assets/Resources/Step1Art/（如 Hero0.png、FireTrap.png）", 243, "S243", "", "", "", "Gameplay/Step1/Step1ArtSkin.cs", "public class Step1ArtSkin"),
        F("room.pause", "② 横版挑战片段", "参考", "暂停菜单：继续 / 重开这一局 / 怎么玩 / 设置 / 回小镇 / 退出", "Esc", "↑↓ 选、Enter 确认、数字键直接选；设置里能调游戏速度、震屏、提示圈、像素美术、开局说明、开局倒计时（改的就是调参文件，下次 Play 还在）", "测试别人的关卡时，可以在设置里把速度调到 ×0.75 慢慢看他怎么走", 244, "S244", "", "Esc ↑ ↓ Enter Backspace 1-6", "", "Gameplay/Step1/Step1PauseMenu.cs", "public class Step1PauseMenu"),
        F("room.rhythm", "② 横版挑战片段", "参考", "节奏提示：开局 3-2-1-开始！· 马里奥拿到宝了！· 最后 10 秒！", "自动（调参 startCountdown / roundBanners 开关）", "屏幕中间大字，1.6 秒后淡出；不挡操作", "想关掉：Esc → 设置 → 开局倒计时；横幅在调参 roundBanners", 244, "S244", "", "", "", "Gameplay/Step1/Step1Rhythm.cs", "public class Step1Rhythm"),
        F("town.savepoints", "⑨ 存量小镇内核", "参考", "进度点：进门 / 房间打完 / 天亮自动存 + 3 个手动存档位", "Esc → 存到存档位…（1-3）；下次 Play 进小镇在继续列表里选", "进门前、房间打完回来、天亮结算后各自动存一次（右上角提示 💾 进度点）；继续列表按时间排，写清楚第几天几点、哪种进度点；旧存档也能读", "想在关键门口留一个保险：进门前 Esc → 存到存档位 2", 245, "S245", "", "Esc 1-3", "", "Overworld/Runtime/TownSaveStore.cs", "public static class TownSaveStore"),
        F("town.save", "⑨ 存量小镇内核", "参考", "小镇存档 + 暂停：Esc 打开菜单（继续 / 现在存档 / 怎么玩 / 重新开始这一天 / 退出）；每 20 秒自动存", "Esc；下次 Play 进小镇会问 继续上次那一天？（Enter 继续 / N 新的一天）", "存的是第几天、几点、每扇门的结果、心、能量、两人站的位置、今天改过的地形；天亮结算后删掉存档", "想从头试新的一天：Esc → 重新开始这一天（会删存档）", 244, "S244", "", "Esc Enter N", "", "Overworld/Runtime/OverworldGame.cs", "public const string SaveKey"),

        // ② 玩：小镇大地图
        F("town.play", "⑨ 存量小镇内核", "常用", "▶ 试玩小镇（一天）", "测试中心 ② ▶ 试玩小镇；或小镇工坊 F5；进门 = 进恶作剧房间，打完自动回小镇", "马里奥按时间表去几户人家偷宝贝；你先到门口埋伏，就能在房间里坑他", "一张小镇 = 一天的故事：几扇门、每扇门一个房间、按顺序教新东西", 210, "S210 S211", "MarioTrickster/世界工坊 World Workshop/地图空间/预览当前地图 Play Map", "Return", "", "Editor/OverworldBuilder.cs", "public static void PlayMenu()"),
        F("town.basic", "⑨ 存量小镇内核", "常用", "小镇操作：走 · 伪装 · 香蕉皮 · 挑衅 · E 埋伏", "方向键 / WASD 走，P 伪装木箱，L 让附近香蕉皮变滑，T 挑衅，E 在门口预约埋伏（坐炮 / 钻山洞也是 E）", "高草和房子后面他看不见；晚上看得近；他先进门 6 秒内跟进 = 迟到", "门前留高草或木箱给你躲；门之间的距离决定你来不来得及", 213, "", "", "W A S D P L T E", "", "Overworld/Runtime/OverworldGame.cs", "door = Step1Keys.Down(KeyCode.E)"),
        F("town.big", "⑨ 存量小镇内核", "进阶", "大机关 · 连锁 · 天气（巨炮 · 滚石 · 水塔 · 钟楼 · 山 · 山洞 · 闪电 · 泥石流）", "靠近按 L 发动（每天一次，先闪 1.2 秒）；坐进巨炮按 E，方向键瞄准，L 把自己轰过去", "冲击会震响旁边的下一个 = 连锁；每天早上公布天气（风偏炮弹、雨天水大、雾天他看得近）；他吃过一次亏会躲", "在马里奥必经路口摆一串：钟楼让他停下 → 滚石砸过来 → 水淹路口", 218, "S218 S219 S228", "", "", "", "Overworld/OverworldProps.cs", "public static class OverworldProps"),
        F("town.hearts", "⑨ 存量小镇内核", "进阶", "心 · 雷区 · 补心 · 能量 · Q 雷云", "你和他各 3 颗心（左上）；能量满 3 格按 Q 在头顶召唤雷云", "闪电 / 滚石 / 泥石流 / 巨炮打中掉 1 颗心；心掉光这一天马上结束（你赢 / 他赢 / 平局）", "雷区放在他的路线上，补心放在你的逃跑路线上", 220, "S220 S221", "", "Q", "", "Overworld/OverworldStorm.cs", "class OverworldStorm"),
        F("town.guide", "⑨ 存量小镇内核", "常用", "指引 · Tab 路线 · M 小地图 · 空格快进 · -/= 镜头", "屏幕边箭头 = 下一扇门；左上算好「你几秒 / 他几秒」；按住 Tab 看路线；M 小地图；按住空格快进（不碰键盘也会自动快进）；- / = 拉远拉近", "等他出门时不用干等", "地图越大越要靠指引；体检会报「干等超过 10 秒」的地方", 212, "S212 S224", "", "Tab M Space Minus Equals", "", "Overworld/OverworldGuide.cs", "public static Race RaceTo("),
        F("town.stories", "⑨ 存量小镇内核", "常用", "居民：每户一个人 · 当场喊 · N 笔记本 · 记得你", "打完一户回小镇他说一句；大机关在他家门口砸中马里奥，他当场喊；按 N 翻笔记本（谁说过什么、还差什么）", "常来往才说真心话（每段只说一次），关掉游戏再开也记得", "给每扇门起名字和性格（小镇 .txt 里 # Resident:），台词编辑器写他们的话", 232, "S232 S233", "", "N", "", "Overworld/TownStory.cs", "public sealed class Resident"),
        F("town.dayend", "⑨ 存量小镇内核", "常用", "一天结束 · R 第二天", "结算后按 R（被打死会 6 秒后自动重开）", "第二天换天气；记录写进 town_days.csv，体检会算", "连着玩几天看天气和居民台词怎么变", 229, "S229", "", "R", "", "Overworld/Runtime/OverworldGame.cs", "Step1Keys.Down(KeyCode.R) || autoRestart"),
        F("town.link", "⑨ 存量小镇内核", "进阶", "小镇 ↔ 房间联动（带炸弹 · 带心 · 晕着进门 · Enter 回小镇）", "自动；房间打完按 Enter（或等几秒）回小镇", "埋伏成功多 1 枚炸弹；道具箱的炸弹带进下一个房间；大机关砸晕他会晕着进门", "设计「先在小镇砸他、再进门坑他」的组合", 215, "", "", "", "", "Overworld/Runtime/OverworldRoomLink.cs", "Step1Keys.Down(KeyCode.Return)"),

        // ③ 做：关卡 · 小镇 · 台词 · 数值
        F("ws.world", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "世界乐园（会场 · 阶段 · 分支 · 线索）", "开始页世界工坊；或世界工坊 → 会场与流程", "自定义世界、会场公开规则、剧情与胜负分支；探索点位画布（点放/移动/删除/撤销）；结局标签可自定义（如 recorded），会场链接按标签解锁；复用关卡库房间", "先画探索空间与近身证据，再连接结果揭示；用自定义结局标签让失败也通往新会场；横版只作为需要纵深的片段", 250, "S248 S250", "MarioTrickster/世界工坊 World Workshop/会场与流程", "", "", "Editor/WorldParkWindow.cs", "public static void Open()"),
        F("ws.room", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "关卡工坊（画横版房间）", "Ctrl+Alt+W；画笔 B / 矩形 R / 橡皮 E / 吸管 I / 移动 M；右键擦；Ctrl+Z 撤销；▶ 作为第 1 步房间试玩", "边画边检查：红格 = 必须改（悬空、死局、炮口被挡），说明写在格子上", "画完点 ▶ 马上玩；工坊不会让你存一张马里奥过不去的图", 189, "LEVEL_WORKSHOP", "MarioTrickster/世界工坊 World Workshop/横版片段/编辑几何 Level Workshop", "", "", "Editor/LevelWorkshopWindow.cs", "public static void Open()"),
        F("ws.wizard", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "向导 · 模式印章 · 起承转合", "工坊工具栏 向导…（选主角机关 + 时长）/ 印章 ▾（点画布盖一组现成机关）/ 起承转合（看分段）", "向导生成的草稿已经能玩；起承转合 = 教 → 加深 → 意外 → 收尾", "不知道从何下手就用向导，再改最别扭的一处", 208, "S208", "", "", "", "LevelDesign/LevelBlueprint.cs", "public static Draft Wizard("),
        F("ws.samples", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "样板 · 监狱塔 · 箱庭总览", "工坊 样板：两层监狱 / 诱捕走廊 / 长廊远征 / 箱庭监狱；监狱塔…（按层数自动拼）；箱庭总览（每层身份、捷径、环路）", "魂系箱庭：先绕远路，再从另一边打开捷径门", "照着样板改是最快的学法；多层监狱看箱庭总览找「无脑堆层」的问题", 196, "S196", "", "", "", "LevelDesign/FloorStacker.cs", "public static string[] Build("),
        F("ws.checks", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "工坊检查：最坏情况 · 策略模拟 · 连招路线 · 检查轨迹（含临界跳 · 试玩卡点）", "工坊工具栏开关", "最坏情况 = 全塌全挡时出不出得去；策略模拟 = 3 颗炸弹能不能把他困死（会自动加固）；检查轨迹 = 自动检查时他走哪、卡哪 + 黄格 = 只能跳满 2 格才出得去（塌后也算）+ 红叉数字 = 真人试玩时他在这卡过几次（自动记录 + 截图）", "画完一关把这 4 个都开一次；黄格旁边加一级台阶", 202, "S202 S240", "", "", "", "LevelDesign/StrategySim.cs", "public static class StrategySim"),
        F("ws.library", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "关卡库 · 导入导出 · PageUp/PageDown 切关", "工坊 关卡库 ▾（打开 / 存入 / 导入网页关卡包）；◀ ▶ 切关自动存", "关卡存在 Assets/Levels/Library，会跟着 bat 一起传到 GitHub", "每关起个名字存进库，小镇的门就能连它", 206, "S206", "", "", "", "Editor/LevelLibrary.cs", "public static List<(string name, string path, List<char> pending)> List()"),
        F("ws.town", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "小镇工坊（画大地图）", "Ctrl+Alt+O；画笔 B 矩形 R 填充 F 橡皮 E 吸管 I；1–9 放门；↔ 扩展（最大 192×128）；F5 试玩；PageUp/PageDown 切小镇", "右边：门的顺序和时间、一天总览（每扇门主打什么）、居民、天气、大机关连锁", "门上的 ✎ 直接跳到关卡工坊改那间房", 210, "S215 S217", "MarioTrickster/世界工坊 World Workshop/地图空间/编辑几何 Town Workshop", "", "", "Editor/OverworldWorkshopWindow.cs", "public static void Open()"),
        F("ws.townplus", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "小镇工坊进阶：⚡ 关系线 · ⛈ 雷区 · 🤖 模拟玩家玩一天", "小镇工坊工具栏 ⚡ 关系线 / ⛈ 雷区（Z 拖框）；右边 🤖 模拟玩家玩一天", "关系线画出炮→靶心、滚石滚道、水塔范围、连锁；模拟玩家 = 4 种机器人各玩 3 天", "改完一张小镇先跑一次模拟玩家，看会躲的人能不能全埋伏上", 213, "S213", "", "", "", "Overworld/OverworldBots.cs", "public static Report PlayDay("),
        F("ws.lines", "③ 做：关卡 · 小镇 · 台词 · 数值", "常用", "台词编辑器（居民和马里奥说的话）", "Ctrl+Alt+L；或小镇工坊右边 ✎ 台词编辑器…", "改、新加、关掉任何一句；「这句什么时候会说？」当场彩排；存在你自己的 MyTownStories.json，AI 升级永远不覆盖", "先写每户人家 3 句：好笑的、生气的、真心话", 234, "S234", "MarioTrickster/世界工坊 World Workshop/对话/存量台词 Lines", "", "", "Editor/TownStoryEditorWindow.cs", "public static void Open()"),
        F("ws.dice", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "🎲 灵感骰子", "小镇工坊右边 🎲 / 网页大地图页 灵感骰子", "随机给一个「谁家门口 + 天气 + 机关 + 限制」的题目（不改地图）", "卡住时掷一次，照着题目做一个房间或写一句台词", 233, "", "", "", "灵感骰子", "Overworld/IdeaDice.cs", "public static Idea Roll("),
        F("ws.tuning", "③ 做：关卡 · 小镇 · 台词 · 数值", "进阶", "调参（RushMarioTuning）· 数值关系", "开始页点「打开」（或 Project 窗口 Assets/Resources/Step1/RushMarioTuning），在 Inspector 里改：⭐ 常用 15 个 + 9 组折叠、能搜、只看改过的、↺ 恢复默认", "所有数值都在这一个文件（250 项，每项都有中文说明）：马里奥速度、视野、炸弹数、晕几秒、游戏速度…；数值之间有矛盾会直接标黄", "一次只改一个数，玩两局对比；● 标出你改过哪些，改乱了点 ↺；马里奥性格是一个下拉", 238, "S226 S237 S238", "", "", "数值关系", "Editor/MarioMindTuningSOEditor.cs", "[CustomEditor(typeof(MarioMindTuningSO))]"),

        // ④ 网页设计台
        F("web.world", "④ 网页设计台", "常用", "网页：世界乐园", "网页顶部 世界乐园；导入导出使用本页独立按钮", "表单编辑会场、阶段、分支与观察/真相；探索地图点位画布（点放、拖动、撤销/重做）；多人博弈阶段参数（与 Unity 同一个内核），彩排时可真实逐轮打一局；自定义结局标签；结构预检和玩家视角彩排；内置示例为主世界「空白之城」；草稿独立存于此浏览器", "导出.worldpark.json，再在Unity世界窗口导入；引用房间的关卡包另行携带；世界包用「写进 Unity 项目」同步；房间另存关卡包", 251, "S248 S250 S251", "", "", "世界乐园|世界与会场|会场设定与阶段分支|会场之间的入口|玩家视角彩排|当前阶段|线索：先观察，再重新解释|结构预检", "../../tools/LevelStudioWeb/shell.html", "data-page=\"worldpark\""),
        F("web.play", "④ 网页设计台", "常用", "网页试玩（在浏览器里真正玩一遍世界）", "网页顶部 ▶ 网页试玩；或世界乐园页 ▶ 网页试玩这个世界；直接打开 index.html#play 玩内置主世界", "全屏游玩，不是文字彩排：霓虹扑克牌亮灯入场、俯视现场走位调查、鬼巡逻与追逐（被看见画面边缘闪红）、全屏博弈桌逐张亮牌、揭示时逐张翻反转卡、GAME CLEAR / GAME OVER 结局章与可走的门；规则内核与演出时序和 Unity 同语义（逐状态跨端对比）；安全阶段自动存进度，标题页可继续旅程", "改完世界草稿直接点 ▶ 试玩，不用开 Unity；想给朋友玩就发 index.html#play；Tab 线索本，Esc 暂停（可重来本会场）", 253, "S253", "", "W A S D E Space Tab Escape", "▶ 网页试玩|▶ 网页试玩这个世界", "../../tools/LevelStudioWeb/shell.html", "id=\"btnPlayWeb\""),
        F("web.design", "④ 网页设计台", "常用", "网页：画关卡", "双击安装包里的 MarioTrickster关卡设计台.html（或测试中心 🌐 网页设计台）", "和 Unity 工坊同一套元素和检查；能看「马里奥一趟几秒走到哪个机关」和节奏条；不用开 Unity 也能画", "在外面（没装 Unity 的电脑）想点子、画草图", 204, "S204", "", "", "画关卡|我的关卡|起步|模式印章|元素|当前笔刷|检查结果|马里奥一趟 · 几秒走到哪个机关|节奏 · 紧张和喘气|问题（点一下定位）|这张图的设计意图", "../../tools/LevelStudioWeb/shell.html", "data-page=\"design\""),
        F("web.town", "④ 网页设计台", "常用", "网页：大地图", "网页顶部 大地图", "画小镇、看门的时间表、居民、台词本、天气、伤害一览、一天总览、大机关连锁、马里奥的一天（时间滑条）", "和 Unity 小镇工坊逐字一致，哪边改都行", 210, "", "", "", "大地图|格子|房间门|门（马里奥按时间顺序去）|检查|住户与故事|台词本|雷区|伤害一览|一天总览|大机关 · 连锁|天气|马里奥的一天", "../../tools/LevelStudioWeb/shell.html", "data-page=\"overworld\""),
        F("web.mech", "④ 网页设计台", "进阶", "网页：新机制提案 · 想删掉的旧东西", "网页顶部 新机制提案", "写清新机关是什么、代价、马里奥怎么反制；可以先画进关卡试摆；也能列出想删 / 改的旧东西", "想要新机关时先在这写，再交给 AI", 206, "", "", "", "新机制提案|想删掉 / 改掉的旧东西", "../../tools/LevelStudioWeb/shell.html", "data-page=\"mech\""),
        F("web.handoff", "④ 网页设计台", "常用", "网页：交给 AI（设计单 + 关卡包）", "网页顶部 交给 AI → 复制设计单 / 导出关卡包", "一段文字说清要做什么 + 所有关卡数据，发给 AI 就能升级", "每次想让 AI 做事，带上设计单最省话", 204, "", "", "", "交给 AI", "../../tools/LevelStudioWeb/shell.html", "data-page=\"handoff\""),
        F("web.sync", "④ 网页设计台", "进阶", "网页 ↔ Unity 自动同步 · ▶ 在 Unity 试玩", "网页右上 🔗 连接 Unity 项目（选 MarioTrickster 文件夹，Chrome / Edge）", "网页一改，切到 Unity 自动导入；Unity 存了切回网页自动拿到；覆盖前自动备份", "在网页画、按 ▶ 在 Unity 试玩，来回不用导入导出", 214, "S214", "MarioTrickster/测试与诊断/检查/📂 网页同步收件箱 (Inbox)|MarioTrickster/测试与诊断/检查/📂 网页同步备份 (History)", "", "", "Editor/WebSync.cs", "public const string Inbox"),
        F("web.import", "④ 网页设计台", "进阶", "网页导入（像素图 · 文字 · 数字表）", "网页 画关卡 → 导入（多种格式）", "一张小像素图、一段文字或数字表都能变成关卡（数字要对应到元素）", "在纸上 / 画图软件里画的草图直接导进来", 205, "", "", "", "把数字对应到元素", "../../tools/LevelStudioWeb/shell.html", "id=\"mapDlg\""),
        F("web.map", "④ 网页设计台", "常用", "网页：功能地图", "网页顶部 功能地图", "这一页（和 Unity 开始页同一份）", "忘了某个东西在哪，搜一下", 236, "", "", "", "功能地图", "../../tools/LevelStudioWeb/shell.html", "data-page=\"map\""),

        F("web.cockpit", "④ 网页设计台", "常用", "综合创作驾驶舱（图标导航）", "双击 Tools/LevelStudioWeb/index.html 默认打开；任意页顶部 驾驶舱", "按当前世界显示会场、阶段、成功失败分支和会场之间的门；图标直达画地图、摆点位、规则、博弈、反转、体检、导出；摆点位先完成布局再直达画布工具栏，避开吸顶安全条，不改变世界；项目全景展示子工具如何应用或引用进世界，再到网页试玩与 Windows 正式游戏", "先选会场，再选想做的事；不用理解每个表单字段；所有旧功能入口保留", 259, "S259", "", "", "驾驶舱", "../../Tools/LevelStudioWeb/shell.html", "id=\"pageCockpit\""),
        F("web.mapedit", "④ 网页设计台", "常用", "会场地图：图上画地形与调整范围", "驾驶舱 画地形 / 改大小；世界工坊探索区 图上画地形 / 改地图大小", "大画布图块笔刷、矩形、宽高与快捷扩展、临时撤销重做；显示原点位；临时预览应用回当前阶段，取消不改；扩图保留原内容，裁掉出生点/点位/巡逻或堵住可达路线时拒绝并说明；应用可撤销", "不用手写 mapText；地图文本仍收在高级格子；运行地图与规则不改", 257, "S257", "", "", "图上画地形 / 改地图大小", "../../Tools/LevelStudioWeb/shell.html", "id=\"wcMapDialog\""),
        F("web.roomlink", "④ 网页设计台", "常用", "横版房间子创作：按引用打开并返回", "世界工坊 横版挑战阶段 → 选关卡库房间 → 打开这个房间子创作 → 应用房间引用并返回", "打开实际引用的房间，顶部显示编辑对象、同名引用影响与返回；世界包只记房间名，不携带房间网格；原阶段被改或删除时拒绝覆盖", "房间另存关卡包或连接 Unity 同步；网页不跑横版物理，正式体验在 Unity 测", 257, "S257", "", "", "应用房间引用并返回", "../../Tools/LevelStudioWeb/shell.html", "id=\"wcRoomReturn\""),
        F("web.gameform", "④ 网页设计台", "常用", "博弈参数表单（玩法 / 轮数 / 参加者）", "驾驶舱 调博弈参数；世界工坊 选中多人博弈阶段", "玩法三选一按钮，只显示该玩法用到的参数（轮数、每人命数、目标比例与追加规则 / 通关所需分数、随机种子）；参加者名字 + 性格下拉、＋ 参加者、✕ 删除（唯一的玩家本人删不掉）；字段、标签、范围与 Unity 世界工坊相同；每次改动即时用共享规则校验并进撤销历史；JSON 收进高级格子", "不用写 JSON；好不好玩用下方彩排打一局或跑剧情模拟", 258, "S258", "", "", "＋ 参加者", "../../Tools/LevelStudioWeb/worldpark-app.js", "data-wpg-mode"),
        F("web.chaserroute", "④ 网页设计台", "常用", "鬼的路线：在图上点巡逻格", "世界工坊探索地图：点选鬼 → ✎ 鬼的路线 → 依次点格子；驾驶舱 摆线索 / 门 / 鬼", "点新格加到路线末尾，再点一次去掉；墙、地图外、出生点走不到的格拒绝并说明；最多 32 格（与运行规则相同）；路线画成连线和序号，虚线表示循环回第 1 格；画布撤销 / 重做；巡逻格文本框仍可精确输入", "不用手打坐标；改完 ▶ 只玩这个会场 看鬼怎么走", 258, "S258", "", "", "✎ 鬼的路线", "../../Tools/LevelStudioWeb/worldpark-spatial.js", "toggleRoute(cell, before)"),
        F("world.mapreplace", "① 世界主体验", "常用", "Unity：整张替换地图前确认 + 一键换回", "Unity 世界工坊 探索阶段“从当前小镇复制地图文本”", "先弹窗说明会整张替换、点位可能落进墙里，确认才替换；替换后出现“换回替换前的地图”一键恢复（窗口开着时有效）；原小镇不改写", "误点了就点“换回替换前的地图”", 258, "S258", "", "", "", "Editor/WorldParkWindow.cs", "mapUndoNode"),

        // ⑤ 测试与反馈
        F("test.start", "⑤ 测试与反馈", "常用", "📖 开始页（功能地图）", "Ctrl+Alt+H；测试中心 / 两个工坊工具栏也有 📖", "最上面「⏱ 上次做到哪」：最近改的关卡 / 小镇（点接着做直接打开）、上次体检 / 测试 / 试玩的结果；下面全部功能按区分好，能搜，「我想…」按目标给出步骤", "每次打开 Unity 先看「上次做到哪」，接着做；升级后看一眼「🆕 最近新增」", 237, "S236 S237 FEATURE_MAP", "MarioTrickster/📖 开始页 Start Here", "", "", "Editor/StartHereWindow.cs", "public static void Open()"),
        F("test.hub", "⑤ 测试与反馈", "常用", "测试中心（一键体检 · 试玩 · 打包反馈）", "Ctrl+Alt+T：① 一键体检 → ② 试玩 → ③ 打包反馈", "不进 Play 就检查全部关卡和小镇、机器人玩一天、数值关系、台词、你的试玩记录；打包截图和记录给 AI", "每次改完东西点一次 ①", 217, "S217", "MarioTrickster/测试与诊断/测试中心 Test Hub", "", "", "Editor/TestHubWindow.cs", "public void RunHealthCheck()"),
        F("test.story", "⑤ 测试与反馈", "常用", "剧情带入模拟（五种玩家真实走完主世界）", "一键验证 Tools\\WorldParkVerify\\run_all.ps1 自动运行；结果在 .work\\worldpark-verify\\story\\模拟报告.md 与 旅程_*.md", "鲁莽、直觉、谨慎、深思、跟风五种玩家只凭公开信息真实移动、躲鬼、调查、博弈、跨会场旅行并读写检查点；输出逐句剧情记录（含【亮灯】【桌面】【反转】【错过】等画面标记）、体验矩阵，并对照作者预期文件检验公平、威胁、规则追加、限时压迫、追逐张力、博弈桌画面、字幕卡与每个会场的反转", "改了会场就跑一次；想规定谁该活谁该死，改 Tools\\WorldParkVerify\\Story\\Borderland.expect.json；设环境变量 STORY_TRACE=1 可输出逐帧追逐轨迹", 252, "S251 S252", "", "", "", "../../Tools/WorldParkVerify/Story/Program.cs", "ReadCharsPerSecond"),
        F("test.quick", "⑤ 测试与反馈", "常用", "⚡ 快速测试模式", "测试中心顶部勾选", "不弹玩法说明、房间结束不弹问卷（照样记一行）", "反复测时开着，认真玩时关掉", 217, "", "", "", "", "Gameplay/Step1/Step1QuickTest.cs", "public const string Key"),
        F("test.exit", "⑤ 测试与反馈", "参考", "第 1 步出口报告 · 小镇记录", "测试中心 ① 一键体检，往下翻", "从你的试玩记录自动算：连玩几局、坑法几种、还想再来几分、改版前后对比、建议", "玩够 5 局再看，比凭感觉准", 227, "S227", "", "", "", "Gameplay/Step1/Step1ExitReport.cs", "public static string Markdown("),
        F("test.handsoff", "⑤ 测试与反馈", "自动", "马里奥自己跑（H10 自动检查）", "测试中心 🤖 马里奥自己跑；或菜单 检查与记录 → 🤖 马里奥自己跑", "你不操作，连跑几局看马里奥能不能自己拿宝回家（要 ≥95%）", "改了房间结构或马里奥数值后跑一次", 181, "", "MarioTrickster/测试与诊断/检查/🤖 马里奥自己跑 Hands-off (H10)", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void HandsOffMenu()"),
        F("test.probe", "⑤ 测试与反馈", "自动", "陷阱试探（AI 捣蛋者替你坑）", "测试中心 🎯 陷阱试探；或菜单 检查与记录 → 🎯 陷阱试探", "AI 在马里奥走到时触发每个机关，看连起来会不会把他坑死 / 卡住", "机关很多的房间跑一次，再开工坊「检查轨迹」看他卡在哪", 202, "", "MarioTrickster/测试与诊断/检查/🎯 陷阱试探 Trap Probe", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void TrapProbeMenu()"),
        F("test.unit", "⑤ 测试与反馈", "自动", "EditMode 测试 · 测试报告", "测试中心 🧪 跑 EditMode 测试（唯一入口；要连 PlayMode 一起跑：菜单 检查与记录 → 测试报告 全部）", "几百条自动检查，结果写进 TestReport.txt，打包反馈会带上", "每次装完升级包跑一次", 160, "", "MarioTrickster/测试与诊断/检查/测试报告 EditMode|MarioTrickster/测试与诊断/检查/测试报告 PlayMode|MarioTrickster/测试与诊断/检查/测试报告 全部 All|MarioTrickster/测试与诊断/检查/打开上次测试报告", "", "", "Editor/TestReportRunner.cs", "public static void RunEditModeTests()"),
        F("test.files", "⑤ 测试与反馈", "自动", "试玩记录文件夹 · 房间场景", "测试中心 📂；或菜单 检查与记录 → 📂 试玩记录文件夹 / 重建房间场景 / 打开房间场景", "step1_rounds.csv、town_days.csv、F8 截图都在这；场景会自动重建，一般不用手动", "想自己看数据时打开", 181, "", "MarioTrickster/测试与诊断/检查/📂 试玩记录文件夹|MarioTrickster/测试与诊断/检查/重建房间场景 Build Room|MarioTrickster/测试与诊断/检查/打开房间场景 Open Room", "", "", "Editor/Step1PrankRoomBuilder.cs", "public static void OpenLogsMenu()"),

        // ⑥ 美术
        F("art.kit", "⑥ 美术", "常用", "🎨 素材包：换画风 + 素材自带互动（毒池掉半格 / 一格自己定）", "菜单 美术 → 🎨 素材包 换图 + 互动；关卡里画 z Z a r = 4 个素材槽", "每张图一个名字：把 Sprite 拖进去，房间、小镇、图例全换成它；同一个名字可以挂互动（每隔几秒掉半格 / 一格、回血、减速、晕，谁会中自己勾）；PNG 按名字放一个文件夹 = 批量导入；导出模板给画师照着画", "换画风：导出模板 → 画师照名字画 → 批量导入；做新陷阱：画一个 z，在素材包里给 ArtSlot1 拖图 + 设成掉一格", 245, "S245", "MarioTrickster/世界工坊 World Workshop/美术/🎨 素材包 换图 + 互动", "", "", "Editor/ArtKitWindow.cs", "public class ArtKitWindow"),
        F("art.world", "⑥ 美术", "参考", "小镇像素美术 · 远山视差 · 天气粒子 · 房间装饰 · 技能特效图", "自动（调参 artTown / artMountains / artWeatherDensity / artDecorDensity / artBackdrop / artSkillFx）", "小镇地面 16 像素一格、房子树山门是像素图、马里奥和你是像素小人；镜头往北看得见远山（两层视差）；下雨 / 暴雨 / 酸雨 / 大风落叶 / 大雾都有粒子；房间背景挂画、窗、烛台等装饰（压暗、没有碰撞体）；户外主题房间有天空和远山；炸弹、炮弹、土包、钩爪、挑衅气泡换成像素图", "想看回色块：调参里关掉对应开关；装饰太多：调小 artDecorDensity", 245, "S245", "", "", "", "Overworld/WorldArt.cs", "public static class WorldArt"),
        F("art.edges", "⑥ 美术", "参考", "小镇过渡边 · 圆角 · 水边 · 脚下影子 · 网页小镇像素画面", "自动（调参 artTown）；网页大地图页 🎨 像素画面 开关", "草长进路 / 泥地是不规则毛边，池塘圆角 + 浅色水边；树、房子、山、机关、人物脚下有影子；网页设计台的小镇和 Unity 用同一套图块和规则", "想看清格子：网页关掉 🎨 像素画面，或 Unity 调参关 artTown", 246, "S246", "", "", "", "Overworld/WorldArt.cs", "public static string GroundPixelKey"),
        F("art.town", "⑥ 美术", "进阶", "小镇像素图标：导出模板 · 检查换上的图", "菜单 美术 → 小镇：导出像素图标模板 / 检查换上的像素图", "每种格子一个 16×16 图标；换图后检查尺寸、颜色、描边", "想换小镇画风时先导出模板给画师", 220, "", "MarioTrickster/世界工坊 World Workshop/美术/小镇：导出像素图标模板（给美术换图）|MarioTrickster/世界工坊 World Workshop/美术/小镇：检查换上的像素图（尺寸、颜色、描边）", "", "", "Editor/OverworldArtTools.cs", "public static void ExportTemplates()"),
        F("art.pipeline", "⑥ 美术", "进阶", "素材导入 · 套用到选中物体 · 智能切图", "菜单 美术 → 素材导入 / 套用到选中物体 / AI 智能切图 / 特效工厂 / 特效快速套用 / 溶解噪声贴图 / 工具", "把商业素材切好套到白盒上，只换外观不动碰撞；特效工厂给角色加描边、溶解、闪白", "玩法定了再换美术（现在都是方块）", 150, "ASSET_IMPORT_PIPELINE_GUIDE", "MarioTrickster/世界工坊 World Workshop/美术/素材导入 Asset Import Pipeline|MarioTrickster/世界工坊 World Workshop/美术/套用到选中物体 Apply Art to Selected|MarioTrickster/世界工坊 World Workshop/美术/AI 智能切图 Smart Slicer|MarioTrickster/世界工坊 World Workshop/美术/工具 Pipeline/|MarioTrickster/世界工坊 World Workshop/美术/特效工厂 Sprite Effect Factory|MarioTrickster/世界工坊 World Workshop/美术/特效快速套用 SEF Quick Apply|MarioTrickster/世界工坊 World Workshop/美术/生成溶解噪声贴图 Dissolve Noise", "", "", "Editor/AssetImportPipeline.cs", "public class AssetImportPipeline"),
        F("art.theme", "⑥ 美术", "进阶", "主题美术检查 · 导出元素图例", "菜单 美术 → 主题美术检查 / 导出元素图例", "检查主题缺哪些图；导出全部元素的说明表", "给画师一张清单：每个元素要什么尺寸", 187, "", "MarioTrickster/世界工坊 World Workshop/美术/主题美术检查 Check Theme Art|MarioTrickster/世界工坊 World Workshop/美术/导出元素图例 Element Legend", "", "", "Editor/ElementLegendExporter.cs", "public static void ExportMenu()"),

        // ⑦ 给 AI · 换账号
        F("ai.pack", "⑦ 给 AI · 换账号", "常用", "安装包 · 接续包（换账号也能接上）", "每次升级的 zip：01 双击 bat 选 Y 上传；02 接续包 .skill 发给新账号的 AI", "AI 不靠记忆，靠接续包和仓库里的文档接着做", "换账号、换电脑时把 .skill 和网页设计单一起发", 200, "DESIGN_CONSTITUTION", "", "", "", "../../docs/AI_CONTINUE_PACK/SKILL.md", "name: mariotrickster-continue"),
        F("ai.research", "⑦ 给 AI · 换账号", "参考", "调研与总方案文档", "docs/step1 里的 S222 / S225 / S230 / S231 / S239 等", "网上资料对照、批评、执行方案；S239 = 按好玩整合、删掉了哪些重复系统以及为什么；每次「继续」AI 都按这些走", "想知道「为什么这样设计」「某个旧系统去哪了」时读", 239, "S222 S225 S230 S231 S239", "", "", "", "../../docs/step1/S222_RESEARCH_MASTERPLAN.md", "S222"),

        // ⑧ 安全网（S238：旧工具已删）
        F("old.redline", "⑧ 安全网", "自动", "红线巡检（碰撞体 / 缩放不许乱改）", "自动；菜单 安全网 → 红线巡检 / 红线自动修复", "防止换美术时把角色碰撞体改坏", "不用管；报警了截图给 AI", 50, "", "MarioTrickster/测试与诊断/安全/红线巡检 Red Line Check|MarioTrickster/测试与诊断/安全/红线自动修复 Red Line Auto-Fix|MarioTrickster/测试与诊断/安全/启用红线自动修复", "", "", "Editor/RedLineGuard.cs", "public static class RedLineGuard"),
    };

    /// <summary>「我想…」：按目标串起几个功能（开始页顶部 / 网页 / 文档同一份）。</summary>
    public static readonly Goal[] Goals =
    {
        G("在图上改地图，用驾驶舱找到创作入口", "web.cockpit web.mapedit web.roomlink web.safety web.play", "驾驶舱选会场 → 画地形 / 改大小 → 应用回当前阶段 → 摆点位 → 查看体检 → 只玩这个会场；不满意就撤销；横版阶段按房间引用打开子工具并返回"),
        G("看懂整个项目，学会发现问题、提改进", "doc.course doc.worldguide web.play test.story", "按顺序上 docs/guide/PROJECT_COURSE.md 的 10 课：看全景图 → 用两种性格各玩一遍并填记录表 → 对照指南拆 ♣3 → 读六个会场的设计 → 一次只改一处、对照预期 → 用会场设计单做第一个会场 → 看懂检验工具 → Unity 自测 → 认识项目全貌 → 按反馈模板把问题发给 AI"),
        G("我点错了 / 改乱了，想退回去", "web.safety web.world doc.worldguide", "先点安全条的 ↶ 撤销（Ctrl+Z），一步不够就打开“🕘 改动记录”点“退回到这一步之前”→ 主世界会场改乱了：中栏“和内置原版相比”点 ↺ 恢复（或右栏“只恢复这个阶段”）→ 有红色问题：结构预检里点修复按钮 → 没点“🔗 写进 Unity 项目”之前，Unity 完全不受影响"),
        G("做一个弥留之国式的生存游戏会场", "world.game world.explore ws.world web.world test.story", "写一句公开规则和花色难度 → 选玩法（现场调查 / 数字猜心 / 标记 / 分还是偷）→ 挑参加者人格 → 把真相藏进可近身观察的线索，揭示阶段再重新解释 → 跑剧情带入模拟看每种玩家的结局 → 开始世界亲自打一遍"),
        G("在好框架上改出一个新会场", "world.venuetools web.venuecheck world.myworld ws.world web.play", "世界工坊或网页世界页选一个主世界会场当模板 →“用模板新建”（或“复制此会场”）→ 改规则、地图、鬼、线索 → 看会场体检：红色必改、黄色知道原因 → ▶ 只玩这个会场 → 在会场链接里接上门"),
        G("把世界做成 Windows 游戏给别人玩", "world.myworld world.build world.title ws.world", "网页“🔗 写进 Unity 项目”或世界工坊“保存为我的世界”→“构建 Windows 版”（Unity 关着时双击 BUILD_WINDOWS_GAME.cmd）→ 打开 Builds/Windows 里的 exe，标题页开始 → 整个文件夹拷给别人"),
        G("创作一个可自定义的世界乐园", "ws.world web.world web.play world.journey ws.library ws.town", "定义世界与会场 → 在点位画布放调查点与出口 → 编排阶段、胜负分支与线索 → 用自定义结局标签连接会场 → 玩家视角彩排 → ▶ 网页试玩真正走一遍 → 开始世界/继续旅程在 Unity 里实际走一遍"),
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
