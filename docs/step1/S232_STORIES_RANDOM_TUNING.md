# S232 第四轮调研 · 小镇居民的真心话 · 道具箱洗牌袋 · 两个编辑器数值统一

> 用户（S232）：结合之前所有调研，给项目升级：内容、道具、随机性、关卡编辑（以后做大一统关卡、和大地图联动、陷阱联动），尽量方便以后做游戏、冒出创意。**希望项目承载一些真诚的故事：NPC 的呈现，或者游戏过程中的演出和恶搞带来的真情流露**，让项目更丰富；内容要多到让我以后有尝试创作的欲望，这样才方便我参与测试——如果现在不是这样，我可能也不愿去测试了。网页版和 Unity 里的关卡编辑、控制台的数值设定要更统一、科学、合理。用第一性原理细致拆解，不限语言调研，结合统计学迭代，最后执行。先规划并执行，不考虑还没测试的手感。
>
> 前三轮已读的约 177 个来源不再重复（见 `D231/03_说明文档/调研来源总表.csv`）。本轮 54 个新来源全部读过原文：日文 3、日文英译 2、中文 2、韩文 1、法文 1、德文 1，其余英文。宪法 H1–H10 照旧优先。

---

## 0. 一页结论

1. **第一性原理：你愿意回来测试，靠的不是机关更多，而是"今天会发生什么我不知道"和"这些人记得我"。** 项目里的机关、天气、大机关已经很多（前三轮都在补），**唯一完全没有的是"人"**：小镇有房子、有门、有马里奥，但门里没住人，打完一户什么都不会留下。调研里最强的三条（Hades、MOTHER、动物森友会）都指向同一件事：**故事来自"世界记得你做过什么"，而且可以一句一句往里加，不用重写系统。**
2. **所以 S232 做的是一个"台词引擎"，不是一个新玩法。** 每扇门住一个人（名字 + 6 种性格之一），打完那一户，回到小镇时他说一句；早上镇上的人议论昨天的事；一天结束马里奥自己说一句。**常来往几次以后才会说真心话**（每段只说一次，结算显示"真心话 x/15 段"）。它**不改任何玩法数值、不碰马里奥 AI（H4）**，所以不需要先测手感，也不会破坏宪法。
3. **台词是一个文本文件（`Assets/Resources/TownStories.json`）**——你以后想加一句、改一句、给某个人加一段故事线，直接改文字，Unity 和网页都会用同一份。这就是"方便未来的我参与创作"的那一部分：**写一句话 = 加一个内容。**
4. **随机性只加"先告诉你"的那种。** 道具箱 `?` 以前永远是"+1 炸弹"，现在是洗牌袋（俄罗斯方块 7-bag 规则）：炸弹 2、能量 1、挑衅 1、补心 1 一袋，抽完再洗；**早上 06:00 就公布今天每个箱子里是什么**（Keith Burgun：先知道再决定 = 输入随机，不让人觉得被骗）。第 1 天全是炸弹（先学规则，和"第 1 天晴天"一样）。
5. **两个编辑器的数值统一了一半，另一半有了检查。** 以前网页里自己写着"马里奥跑速 4.95、小镇速度 3.4/5.0……"，Unity 改了网页不知道。现在 **网页直接读 Unity 调参文件的默认值**（246 个），并用同一张"数值关系表"（21 条，例如"追你的速度 < 你的速度"、"晚上视野 < 白天视野"、"预警 ≥ 0.8 秒"）同时在 Unity 体检和网页里检查。
6. **还有一半没做，要说清楚：** 你在 Unity Inspector 里**手动改过**的数值（存在 `RushMarioTuning.asset` 里），网页看不到（网页只看代码里的默认值）；陷阱"信号线"（LittleBigPlanet 式）、"元素 × 材质"矩阵（塞尔达旷野之息）、大一统世界清单（Tiled `.world` / LDtk GridVania）这三样是**大系统**，第 1 步通过前不加（§3 E 表写了为什么、怎么接）。

---

## 1. 第一性原理拆解：用户要的到底是什么

| 用户原话 | 拆到最底 | 现在的项目缺什么 | 本轮怎么处理 |
|---|---|---|---|
| "真诚故事、NPC 呈现、真情流露" | 有人记得你做过什么，并且在**合适的时候**说一句不搞笑的话 | 小镇没有人；门只是门 | **E1 已做**：住户 + 台词引擎 + 真心话节奏 |
| "恶搞带来的真情流露" | 笑和真情的**节奏**：先笑几次，再一句真的，马上用玩笑收尾 | 只有马里奥中招的台词（全是好笑的） | **E1 已做**：每段真心话 = 小伤口 + 一个玩笑；真心话之间至少 3 句好笑的；一天最多 1 段 |
| "足够多的内容让我有尝试创作欲望" | **加内容的成本**要低到"写一句话"的程度 | 加任何东西都要写代码 | **E1/E2 已做**：台词 = 文本文件；网页 / Unity 都能改住户名和性格；彩排 14 天预览马上看到效果 |
| "道具、随机性" | 每天不一样，但不能让人觉得"运气害我输了" | 道具箱永远是 +1 炸弹 | **E3 已做**：洗牌袋 + 早上公布 |
| "关卡编辑统一科学合理" | 同一个数只在一个地方写；数和数之间的关系有人检查 | 网页另写了一份速度；没有关系检查 | **E4 已做**：网页读 Unity 默认值；21 条关系两边同查 |
| "大一统关卡、大地图联动、陷阱联动" | 房间之间、房间和小镇之间能互相影响 | 已有：小镇心带进房间、砸晕带进房间、房间炮轰出窗户、守住一户大机关重新装填 | **E5–E7 记录**：信号线 / 元素矩阵 / 世界清单是大系统 → 冻结（§3） |
| "否则我不愿意测试" | 测试本身要有收获感 | 测完只看到输赢 | **E1**：结算多了"马里奥的一句 + 真心话 x/15"；**E3**：早上看道具箱公告再出门 |

---

## 2. 资料表（本轮全部新来源；来源 | 借来的规则 | 落地）

### A. 故事片段 / 动态对白
| 来源 | 规则 | 落地 |
|---|---|---|
| Emily Short《Storylets: You Want Them》https://emshort.blog/2019/11/29/storylets-you-want-them/ | 一段内容 = 前提 + 效果；按"世界状态"触发，不按"你走过的路线"（否则以后加不了新内容） | `TownStory.Line.needs` 只看 visits / defended / looted / weather / day |
| Emily Short《Beyond Branching》https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/ | 先放宽泛的默认句，再逐步加更具体的；不需要每种情况都覆盖 | `any_*` 通用兜底句 + 每种性格专属句 |
| Elan Ruskin（Valve）GDC 2012 动态对白（YouTube `tAbBID3N64A` 原文字幕） | **条件越多越具体 → 分越高**（他们试过很多打分法，最简单的最好）；同分随机；说过写回记忆 + 过期时间，防止同一个梗太密 | `TownStory.Pick`：tier → 条件数 → 最久没说 → 哈希；`coolDays` 3 天 |
| Robert Yang《Rule Databases for Contextual Narrative》https://www.blog.radiator.debacle.us/2012/07/rule-databases-for-contextual-narrative.html | 独立游戏 ≤1000 条规则直接线性扫描就够；"这变成了写作问题，不再是编程问题" | 线性扫描；台词进 JSON |
| Yarn Spinner《Storylets and Saliency》https://docs.yarnspinner.dev/write-yarn-scripts/advanced-scripting/storylets-and-saliency-a-primer | 内置策略"最好的里面最久没看过的" | 同分时按"最久没说"排 |
| People Make Games《Hades 对白系统》（YouTube `bwdYL0KFA_U`） | 三档优先级：刚发生的大事 > 这个人的故事 > 闲话池 | `tier` 0/1/2 |
| Korb & Kasavin GDC 2021《Hades 对白》（YouTube `m5KJSAj4afg`） | **每次失败故事也往前走一步**，失败就不那么疼 | 被偷 / 没赶上都有专属话；"一天结束"输了也有一句 |
| Weather Factory《From QBN to resource narratives》https://weatherfactory.biz/qbn-to-resource-narratives/ | 所有变量一视同仁会抹掉区别；资源少、能重复获得时故事最有力 | 只记 4 种计数（来往 / 守住 / 被偷 / 没赶上），不做万能变量袋 |
| Wildermyth 播客（YouTube `T4qSLgKg3KU`） | 发生在角色身上的事要记下来、尽量画出来；**把主角行为限制在一个窄带里**（他们只写得出英雄的后果） | 捣蛋鬼"调皮但不残忍"：台词里没有真的伤害；记录到 §6 永久规则 |

### B. 恶作剧 NPC / 真诚的 NPC
| 来源 | 规则 | 落地 |
|---|---|---|
| House House《Road to the IGF: Untitled Goose Game》https://www.gamedeveloper.com/game-platforms/road-to-the-igf-house-house-s-i-untitled-goose-game-i- | 先列"恶作剧清单"，再问"需要什么样的角色来接住这些恶作剧" | 6 种性格各自有"被偷 / 守住 / 没赶上"的专属反应 |
| House House GDC 2021《Google Maps, Not Greyboxes》（YouTube `cCsMz5tUXmc`） | 熟悉的东西（晾衣绳、窗台上的派）不用解释，玩家自己就懂；还能控制笑点时机 | 台词里用日常物件（法棍、拖鞋、拐杖、戒指盒） |
| 糸井重里『MOTHER3 の気持ち』09（日文）https://www.1101.com/mother_project/entry/archives/MOTHER3/kimochi/09.html | 「人の心にちょっと引っかき傷をつけて、冗談のひとつでもつけ加えて、『平気、平気！』」 | **真心话模板**：一句小伤口 + 结尾一个玩笑（"——别告诉别人我哭了"） |
| 糸井重里 1996 访谈（日文）https://www.charapit.com/mother/interview/199611.htm | "游戏细节就是一切"；不要从主题出发做游戏 | 不写"主题"，只写具体的人和物 |
| shmuplations《MOTHER2 开发访谈》https://shmuplations.com/earthbound/ | 因为玩了那么久，"爱""正义"这种用旧了的词在游戏里会重新活过来 | 真心话要"挣"：`visits>=3` / `defended>=2` 才出现，不在头两天 |
| 電ファミ『moon』座谈（日文）https://news.denfaminicogamer.jp/interview/171030 | 反英雄：成长靠"爱"不靠战斗；镇民来自对日常人的夸张观察；少写文档，直接放进游戏里试 | 马里奥也是镇上的人：他也有真心话（"我只是想让大家记得有我这个人"） |
| Zelda Dungeon《Bombers' Notebook》https://www.zeldadungeon.net/wiki/Bombers%27_Notebook | 有日程的支线必须有一本"笔记本"告诉玩家什么时候去哪 | 结算显示"真心话 x/15 段"（轻量版）；完整笔记本 → E8 |
| Aonuma 谈《姆吉拉的假面》https://gameluster.com/aonuma-on-how-nintendo-makes-zelda-dungeons-majoras-masks-melancholy-tone-anju-and-kafei-quest/ | 日常（婚礼）+ 灾难同时发生 → 让人想救这个世界；**最感人的支线太难懂** = 失败 | 台词只靠"你常来"触发，不需要玩家知道隐藏条件 |
| shmuplations《动物森友会访谈》https://shmuplations.com/animalcrossing/ | 台词是"和真人对话的练习"；故意留一点怪 / 一点暗的笑话，孩子得去问妈妈 | 少量略怪的句子（"我决定了，长大当马里奥"） |
| Polygon《动森本地化》https://www.polygon.com/animal-crossing/484661/animal-crossing-localization-book-excerpt/ | 翻译要写"这句话真正的意思"，然后重写，不直译 | `Line.note` 字段（写这句笑点在哪）；中英文各写各的 |
| Kill Screen《Undertale 的幽默》https://www.killscreen.com/behind-humor-toby-foxs-undertale/ | 每个转折先郑重铺垫，再用笑点化解；把沉重的部分夹在大口的幽默中间 | `SincereEvery = 3`：真心话之间至少 3 句好笑的 |
| The Mary Sue 访谈 Toby Fox https://www.themarysue.com/interview-undertale-game-creator-toby-fox/ | 让每个怪物都像一个人；"最重要的东西不能用数字表示" | 每种性格的反应动词不同；真心话不显示加了几颗心 |
| Decisions & Revisions《Night in the Woods 对白》https://decisionsandrevisions.substack.com/p/night-in-the-woods-writing-better-dialogue | 角色之间的背景对话让世界大于主角 | 早上"镇上的闲话"（`who=town`）：镇上的人议论昨天 |
| Naver 博客：NDC 2019《洛奇剧情策划》（韩文）https://m.blog.naver.com/jyc9503/222403089667 | 「몰입하게 만드는 것은 작은 디테일」；每次说话都不一样，按进度 / 经历 / 持有物变 | 按 visits / looted / weather 变 |

### C. 随机性
| 来源 | 规则 | 落地 |
|---|---|---|
| Keith Burgun《Randomness and Game Design》https://www.gamedeveloper.com/design/randomness-and-game-design | 输出随机（决定之后掷骰子）会"注入假信号、拖慢学习"；输入随机（决定之前就知道）好 | 道具箱 **早上公布**；捡到的效果确定 |
| Keith Burgun《Random value absorption thresholds》http://keithburgun.net/good-randomness/ | 给随机收益设上限，防止滚雪球 | 炸弹仍 ≤3、心 ≤3、能量 ≤3；满了提示"浪费了" |
| Tetris Wiki《Random Generator》https://tetris.wiki/Random_Generator | 7-bag：每袋每种一个 → 同一种最多隔 12 个就来；袋子越大越允许连续 | `OverworldPickupBag.Bag` 5 个一袋 |
| Dota 2 Wiki《Pseudo-Random Distribution》https://dota2.fandom.com/wiki/Random_Distribution | 概率随失败次数线性增加、成功清零（25% → C = 8.475%，最多 12 次） | 记录：以后加"稀有道具"用这个（E9） |
| Fire Emblem《True Hit》https://serenesforest.net/general/true-hit/ | 两个随机数取平均：显示 80% 实际 92% | 记录：本项目不显示百分比（H6 不骗人） |
| Jake Solomon 谈 XCOM 2 https://www.gamedeveloper.com/design/jake-solomon-explains-the-careful-use-of-randomness-in-i-xcom-2-i- | 玩家把 85% 读成"不该失手" | 同上：不显示概率，只显示"今天箱子里是什么" |
| GameCodeClub（法文）https://www.gamecodeclub.com/docs/game-design/game-design-du-hasard-et-de-laleatoire-eviter-le-sentiment-dinjustice | 保底：连败后必中；记一张"已发生"清单，别连着出两次一样的 | 洗牌袋本身就是保底；台词冷却 = 不连着说同一句 |
| Spelunky 每日挑战 https://www.gamedeveloper.com/design/the-understated-genius-of-the-i-spelunky-i-daily-challenge | 所有人同一个种子、只有一次机会 → 玩得更慢更有算计 | 已有：地图名 + 天数 = 种子（天气 / 道具箱 / 台词彩排都可复现）；每日挑战排行 → E9 |
| Derek Yu GDC 2021（howtomarketagame 摘要）https://howtomarketagame.com/2021/07/22/gdc-2021-one-more-run-the-making-of-spelunky-2-with-derek-yu/ | 敌人和玩家规则一样 → 组合会让人惊喜 | 已有：大机关对你和马里奥一视同仁（S218–S220） |
| Ludologie（德文）https://www.ludologie.de/blog/artikel/news/was-spiel-und-zufall-mit-unserem-leben-zu-tun-haben | 游戏把偶然变得"可理解、可承受" | 天气 / 道具箱都早上公布 |

### D. 关卡编辑 / 世界联动 / 陷阱联动
| 来源 | 规则 | 落地 |
|---|---|---|
| 塞尔达旷野之息 GDC 2017（Thumbsticks 两篇）https://www.thumbsticks.com/gdc-17-breaking-conventions-breath-of-the-wild/ 、https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies/ | "乘法玩法"：元素能改材质、元素能互相改，**材质之间不能直接互相改**；先在 2D 原型里只给一个目标、不做谜题 | E6 记录：元素 × 材质矩阵 |
| Tiled《Working with Worlds》https://doc.mapeditor.org/en/stable/manual/worlds/ | 一个 `.world` 清单记每张图在世界里的位置；拆文件 = 省内存、少冲突 | E7 记录：大一统世界清单 |
| LDtk《World layout》https://ldtk.io/docs/game-dev/json-overview/world-layout/ | GridVania：房间吸附到世界网格，邻居自动算 | E7 记录 |
| LDtk《Entity fields》https://ldtk.io/docs/game-dev/json-overview/entity-fields/ | 每个参数有定义（类型、最小、最大、默认），实例只存值；超范围自动夹住 | **E4 已做一半**：网页读 C# 的 `[Range]` 和 `[Tooltip]` |
| 超级马里奥创作家 2《Super Worlds》https://super-mario-maker-2-wiki.fandom.com/wiki/Super_Worlds | 一个世界最多 5 关（4 + 城堡）、最多 3 个奖励屋、两根管子互连；不是每关都必须打 | E7 记录：小镇一片区的预算 |
| LittleBigPlanet《Logic》https://littlebigplanet.fandom.com/wiki/Logic | 与 / 或 / 非 / 计时 / 计数 / 随机 / 顺序器 + "微芯片"打包 | E5 记录：陷阱信号线 |
| Media Molecule 访谈 https://www.gamedeveloper.com/design/interview-media-molecule-and-the-evolution-of-i-littlebigplanet-i- | 每关一句"钩子"（用哪个机关、想展示什么） | 已有：一天总览的"主角机关"（S215） |
| Hollow Knight 地图设计 https://www.gamedeveloper.com/design/how-the-i-hollow-knight-i-devs-mapped-out-their-metroidvania- | 原计划随机生成地图 → "头疼"，放弃，改成凭直觉手搭 | 小镇手画，只随机房间里的东西（已是如此） |

### E. 调参科学 / 统计
| 来源 | 规则 | 落地 |
|---|---|---|
| Ian Schreiber《Game Balance Concepts》Level 2 https://gamebalanceconcepts.wordpress.com/2010/07/14/level-2-numeric-relationships/ | 数值之间的关系比单个数值重要 | **E4**：`TuningAudit.Rules` 21 条 |
| Luban（中文）https://www.datable.cn/docs/intro | 一套类型描述配置结构，校验后导出多端；**结构是契约：数据不符合应报错，而不是改定义** | 网页从 C# 生成，不手写第二份 |
| 网易雷火策划表工具（知乎，中文）https://zhuanlan.zhihu.com/p/26001757937 | 痛点：填表时不能实时检查；解决：实时检查 + 错误标在表上 + 提交后报警 | 网页"📐 数值关系"面板 + Unity 体检 + sim 门槛 |
| Slay the Spire GDC 2019（YouTube `7rqfbvnO_H0`） | 目标不是每张牌一样强，而是每张都"有时候值得拿"；**两个重度测试者 = 平均数其实只在采样那两个人**；按获得时机分开看 | §4 统计：单人数据不能当"大家"的结论 |
| Evan Miller《Wilson 区间》https://www.evanmiller.org/how-not-to-sort-by-average-rating.html | 小样本比例用 Wilson 下限 | 已有（S222 起） |
| Evan Miller《Simple Sequential A/B》https://www.evanmiller.org/sequential-ab-testing.html | 可以边看边停的做法：差值到 2√N 停 | §4：只用于机器人模拟 |
| Evan Miller《样本量计算器》https://www.evanmiller.org/ab-testing/sample-size.html | 最小可检测效果 | §4 |
| NN/g《只需要 5 个用户》https://www.nngroup.com/articles/why-you-only-need-to-test-with-5-users/ | 找问题 5 人约 85%；**估比例不能用 5 人** | §4 |
| Valve 用户研究（Steve Bromley 转引 Mike Ambinder）https://www.stevebromley.com/blog/2011/09/01/valves-philosophy-with-user-research-in-games-habe-newell-and-mike-ambinder/ | "设计是假设，试玩是验证假设的实验" | §4：每次改动写一行假设 |
| Mark Cerny Method 摘要 https://iterative.co.nz/mark-cerny-method | 先做出一个"可以发布"的样本再量产 | 维持"第 1 步出口"门槛 |

### F. 失败案例
| 案例 | 出了什么问题 | 我们怎么避开 |
|---|---|---|
| 《上古卷轴 4》Radiant AI https://blog.paavo.me/radiant-ai/ | NPC 自由追求目标 → 杀了任务商人、守卫去打猎、村民抢光商店；玩家大多根本看不见这些行为 | 居民**只说话、不行动**，不能改任何玩法状态 |
| 《辐射 4》无限任务 https://www.forbes.com/sites/insertcoin/2016/01/11/how-to-fix-fallout-4s-maddening-never-ending-radiant-quests/ | NPC 90% 的话是在派任务 → 角色被毁 | 没有"派任务"的台词；真心话每段只说一次 |
| 《姆吉拉的假面》阿奇与卡菲（N64） | 最感人的支线最难懂 | 触发条件只有"常来" |
| Slay the Spire 早期数据 | 某张牌看起来强只因为都在后期拿到；两个人刷爆平均数 | §4 |
| Hollow Knight 随机地图 | 放弃 | 小镇不随机生成 |

---

## 3. E 表（执行情况）

| # | 内容 | 状态 | 文件 / 接线 | 为什么做 / 为什么不做 |
|---|---|---|---|---|
| **E1** | 小镇居民的话：住户 + 故事片段引擎（三档优先级、条件越多越优先、冷却 3 天、真心话只说一次、真心话之间 ≥3 句好笑的、一天最多 1 段）；三个时机：早上闲话 / 回到小镇那户人家 / 一天结束马里奥 | **已做** | `Overworld/TownStory.cs`；数据 `Assets/Resources/TownStories.json`（84 句：通用 9、6 种性格各 6 句专属好笑 + 2 段真心、早上 12、结束 15；真心话共 15 段）；`OverworldSession.RecordRoom/RecordMissed → TownStory.Record`；`OverworldGame` 左下角显示（好笑 5 秒、真心 8 秒，现实秒）；结算加"马里奥的一句 + 真心话 x/15" | 用户最核心的需求；不改玩法、H4 隔离 |
| **E2** | 住户编辑：Unity 小镇工坊"🏠 住户与故事"（名字 + 性格下拉 + 彩排 14 天 + 覆盖率 + 改台词按钮）；网页大地图页同一块；地图文本 `# Resident: 门 \| 名字 \| 性格`，关卡包 `residents` | **已做** | `OverworldMap.Resident/ParseResident/ResidentText`；网页 `owParseResident/owResRender`；旧地图不写住户 = 默认住户、输出不变 | 让"加内容 = 写一句话" |
| **E3** | 道具箱洗牌袋：炸弹 2 / 能量 1 / 挑衅 1 / 补心 1 一袋；第 1 天全是炸弹；早上公布；满了提示浪费 | **已做** | `Overworld/OverworldPickupBag.cs`；`OverworldTown.Pickup`；网页 `owBagOf/owBagPreview`；工坊和网页显示 7 天预览 | 输入随机 + 有上限（Burgun） |
| **E4** | 两个编辑器数值统一：网页读 Unity 默认值（246 个，含 Tooltip / Range）；数值关系表 21 条，Unity 体检 + 网页面板 + sim 三处同查 | **已做** | `Gameplay/Step1/TuningAudit.cs`；`build.py` 生成 `TUNING` / `TUNING_RULES`；`logic.js` 跑速 / 开局等待、`overworld.js` 小镇速度改成读 `TUNING` | 一个数只写一次（Luban） |
| E4b | 网页看到你在 Inspector **手动改过**的值 | 不做（这次） | 需要网页读 `RushMarioTuning.asset`（YAML）；网页已能连 Unity 文件夹（S214 同步），下一步可做 | 先把"默认值一份"做对 |
| E5 | 陷阱信号线（LittleBigPlanet：传感器 / 与 / 或 / 计时 / 计数 / 随机袋 / 顺序器 + 打包成"装置"） | 记录 | 接入点：`ChainPlan`（房间连锁）和 `OverworldProps.ChainTargets`（小镇连锁）已经是"冲击 → 半径内触发"，信号线 = 把"半径"换成"连线" | 新系统，第 1 步通过前冻结 |
| E6 | 元素 × 材质矩阵（旷野之息：火 / 水 / 风 / 电 / 滑 × 木 / 绳 / 铁 / 弹簧 / 香蕉 / 箱子 / 马里奥） | 记录 | S226 E6 已有"交互矩阵"文档；做成数据表 = 一张 CSV，两个编辑器读 | 同上 |
| E7 | 大一统世界清单（Tiled `.world` / LDtk GridVania：每个房间在世界里的位置 + 邻居自动连）+ 片区预算（≤4 房间 + 1 大机关 + ≤3 奖励屋，马里奥创作家 2） | 记录 | 小镇已是"世界"，门 = 房间锚点；`CampaignLedger` 已按门顺序汇总 → 世界清单 = 多张小镇 + 门之间的跨镇连接 | 同上；而且 192×128 小镇目前够用 |
| E8 | 居民笔记本（Bombers' Notebook）：每户的时间线 + 已解锁真心话 + 完成贴纸 | 下一步候选 | `TownStory.Mem` 已经记了全部数据，只差一页画面 | 等用户先看到 E1 再决定要不要 |
| E9 | 每日挑战（同种子、一次机会）、PRD 稀有道具 | 记录 | 种子已可复现 | 第 2 步 |
| E10 | 居民真的"做事"（Radiant AI 式日程） | **刻意不做** | — | 失败案例：玩家看不见、还会破坏玩法 |

---

## 4. 统计学确认（这次改动怎么验证、不能怎么说）

1. **台词引擎不需要真人数据就能验证"有没有重复、有没有空档、真心话节奏对不对"**：用确定性彩排（`TownStory.Rehearse`：每扇门每天的结果按哈希定，守 40% / 被偷 40% / 没赶上 20%）。三张样板图 × 30 天 = 每张 180 句：**3 天内重复 0–1 次、没话说 0 次、真心话 11 段、第一次真心话在第 3–5 天，头两天一句都没有**。sim 门槛把这些写成硬规则（重复率 ≤ 2%、无空档、一天最多 1 段、间隔 ≥3）。
2. **"真心话让人更想再玩"现在是假设，不是结论。** 按 Valve 的做法写成假设：*加了居民的话以后，「还想再来」中位数上升。* 验证：S231 的改版对比（每边 ≥5 局，A12）。**注意**：这次数值版本没变（26），因为没改任何玩法数值——要比较，体检会把 S232 前后的局都算 v26。如果你想严格比较，玩之前告诉我，我把版本号 +1（只是标记，不改数值）。
3. **只有你一个人在测**：Slay the Spire 的教训是"两个重度测试者 = 平均数只在采样那两个人"。所以体检里的所有比例只能说"对你来说"，不能说"玩家会"。找问题（看不懂、读不完）5 个人就够（NN/g，约 85%）；估比例（"30% 的人笑了"）5 个人远远不够（10 人里 7 人 → Wilson 95% 区间 40%–89%）。
4. **洗牌袋的分布是精确的，不是统计出来的**：每 5 个一袋，炸弹恰好 2 个 → 长期 40%；sim 跑了 300 个箱子确认 40%，并检查每一袋都齐四种。
5. **数值关系表不是"好不好玩"的证明**，只是"有没有互相打架"。默认值 21 条全过；反向测试：把"追你的速度"调得比你快 → 立刻报。

---

## 5. 自我审计

| 检查 | 结果 |
|---|---|
| H4：马里奥 AI 会不会读到居民 / 道具箱？ | sim + EditMode 检查 `OverworldMind / RushMarioMind / MarioMindDriver / SuspicionMeter` 里不出现 `TownStory` / `OverworldPickupBag` |
| H1/H10：会不会让马里奥过不了？ | 居民只说话；道具箱只给你东西、上限不变（炸弹 ≤3 和 S214 房间加固按 3 颗算一致） |
| H6：同一个信号同一种意思？ | 居民的话在左下角（不和中间的玩法提示抢位置），真心话框颜色暖一点 |
| H9：会不会卡住？ | 台词框按现实秒自动消失；结算 6 秒自动重开不变 |
| 两份数据会不会对不上？ | `TownStories.json` 和 `TownStory.Default` 逐字对照；网页 448 行逐字对照（彩排、洗牌袋、覆盖率、数值关系、跑速、住户往返） |
| 反向测试 | 拆掉"房间结果记进居民记忆"→ 红；把选句改成"条件少的优先"（网页没改）→ 红；改洗牌袋（网页没改）→ 红；恢复 → 绿 |
| 性能 | 工坊彩排只在改地图后算一次（S220 防卡规则：OnGUI 不做全图计算）；游戏里每次只挑一句（线性扫描 84 句） |
| 旧存档 / 旧地图 | 没写住户 = 默认住户；地图文本和关卡包只在写了住户时才多一行 / 一个字段 |
| 有没有夸大 | 没说"更好玩"；台词好不好笑、真心话动不动人，要你读了才知道 |
| 我写的台词 | 全是我按调研规则写的**起点**，不是定稿。最该你改的：真心话 15 段（你最知道什么能打动你） |

---

## 6. 永久规则（追加）

11. **居民只说话，不行动。** 不许让居民改地形、挡路、追人、给马里奥报信（Radiant AI 的教训 + H4）。
12. **捣蛋鬼调皮但不残忍。** 台词里不出现真的伤害、真的失去（Wildermyth：只写得出英雄后果）。
13. **真心话要挣、只说一次、后面跟一个玩笑。** 新加真心话必须有 `visits>=` 或 `defended>=` 条件、`once: true`、`tone: sincere`（sim 检查）。
14. **新数值如果和别的数值有"必须大于 / 小于"的关系，在 `TuningAudit.Rules` 加一行**（网页自动跟上）。
15. **网页不再手写任何 Unity 已有的数值**：要用就从 `TUNING`（build.py 读 MarioMindTuningSO.cs）拿。

---

## 7. 你怎么用

- **看效果**：Unity 小镇工坊（Ctrl+Alt+O）右边"🏠 住户与故事"：每扇门的名字和性格，下面是"彩排 14 天"——不进游戏就能看到会听到哪些话、第几天有人说真心话。网页大地图页同一块。
- **改名字 / 性格**：直接在那一栏改（两边都会存进地图文件）。
- **改台词 / 加台词**：点"改台词…"打开 `Assets/Resources/TownStories.json`。一句的样子：
  `{"id": "baker_def_3", "who": "door", "when": "back", "tier": 1, "tone": "comic", "coolDays": 3, "needs": ["trait=baker", "result=defended"], "zh": "中文", "en": "English"}`
  - `when`：`morning` 早上 / `back` 回到小镇 / `dayend` 一天结束；`who`：`door` 住户 / `town` 镇上闲话 / `mario` 马里奥
  - `needs` 能用：`trait`（baker 面包师 granny 老奶奶 kid 小孩 mayor 镇长 painter 画家 guard 守夜人）、`result`（defended / looted / missed）、`visits>=3`、`defended>=2`、`looted>=2`、`weather`（clear wind rain fog market storm acid）、`day>=4`、`outcome`（won / lost / draw）、`chain>=3` 等
  - 写错一句不会让游戏坏：体检会告诉你哪一句、哪里错，其余照常
  - `{name}` 会换成住户名字，`{looted}` 换成被偷次数
- **在游戏里**：从门里打完回到小镇，左下角那户人家说一句；早上出门有天气 + 今天道具箱里是什么 + 镇上的闲话；一天结束有马里奥的一句和"真心话 x/15 段"。

## 8. 下一步候选（等你看过再选）

1. **居民笔记本**（E8）：一页列出每户人家来往几次、解锁了哪几段真心话（数据已有，只差画面）。
2. **网页读你手动改过的数值**（E4b）：网页连上 Unity 文件夹后读 `RushMarioTuning.asset`，两边真正只有一份。
3. 你改了 / 加了台词之后，我按你的风格补齐其余住户。
