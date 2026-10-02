# 分册：调研 → 升级

用户经常说"调研综合考虑、全方位优化升级"。做法：
1. 拆 3–5 个子问题（例：同类游戏怎么做？玩家抱怨什么？AI 怎么配合？怎么保证规则？）。
2. `genspark_render_search` 并行搜；用 `web_fetch_urls` **读原文**再下结论（不要只看摘要）。
3. 每条结论写成表格：**来源（带链接）| 借来的规则 | 在本项目的落地**。特别找**差评/痛点**，把它变成设计约束（例：Deception IV 触发时机难 → 自动预判落点）。
4. 只借规则不借素材（名字、美术、关卡原样都不能搬）。
5. 落地后逐条对照宪法 H1–H10 写"规则保障"一节。

已用过的来源（可复用）：
- Deception IV：布置陷阱→引诱→陷阱接力；痛点：时机难、Boss 陷阱透视（https://ricedigital.co.uk/review-deception-iv-blood-ties/ ，https://cogconnected.com/review/deception-iv-blood-ties-ps3-review/）
- 潜行分心工具清单：敲墙引诱、绊线、诱饵、远程引爆（https://critpoints.net/2017/02/01/stealth-game-distraction-tools/）
- Spelunky 元素互相作用 = 涌现（https://critical-gaming.com/blog/2009/2/17/spelunky-a-game-design-gold-mine.html ，https://www.gamedeveloper.com/design/a-spelunky-game-design-analysis-pt-2）
- 格斗游戏连招/顿帧/伤害递减（https://critpoints.net/2016/08/14/stunning-detail/）
- Hitman 关卡设计（https://www.unsupervisednerds.com/reads-full/2020/8/19/level-design-in-hitman）
- 手感（S216）：Smash 击飞 https://www.ssbwiki.com/Knockback 、Vlambeer screenshake https://www.youtube.com/watch?v=AJdEqssNZ-U 、Juice it or lose it https://www.youtube.com/watch?v=Fy0aCDmgnxg 、Eiserloh 相机 https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing
- 魂系箱庭（https://book.leveldesignbook.com/studies/sp/undead-burg ，https://www.pcgamer.com/how-to-design-a-great-metroidvania-map/）

S207 后"关卡设计从哪下手"调研（报告已交付给用户，4 个入门练习关 `入门练习_起承转合4关.levelpack.json` 已用 C# 检查器验证通过）：
- 用户的核心困难 = 空白画布。推荐的顺序：一句话点子 → 从样板改 → 先定 M/o/G 和路线 → 按起承转合分 4 段摆机关，段与段之间留 3–5 格空地 → 每个机关 2–4 格内放一个藏身处 → 按检查提示修改 → Unity 试玩后一次只改一处。
- 什么时候加机制：一句话点子用现有元素摆不出来 → 写 💡 提案；同一个点子有 2 个以上关卡想用 → 改成 ✅ 再实现。
- 来源：任天堂起承转合（https://www.eurogamer.net/how-nintendos-best-mario-levels-were-structured-using-chinese-poetry ）、GDC《Overcoming the Digital Blank Page》（https://www.youtube.com/watch?v=R75g3elj7y4 ）、Celeste GDC（https://www.youtube.com/watch?v=4RlpMhBKNr0 ）、死亡细胞概念图（https://www.gamedeveloper.com/design/building-the-level-design-of-a-procedurally-generated-metroidvania-a-hybrid-approach- ）、Spelunky 先画路线（https://tinysubversions.com/spelunkyGen/ ）、节奏组论文（https://eis.ucsc.edu/papers/smith-fdg-09.pdf ）、马里奥模式论文（http://julian.togelius.com/Dahlskog2013Patterns.pdf ）、Tanagra（https://dl.acm.org/doi/abs/10.1145/1822348.1822376 ）、《Building Blocks of Tabletop Game Design》的 MOV-24 / UNC-01 / UNC-02 / SET-05 / ACT-15 / STR-07。
- S208 已实现：新建关卡向导、8 个模式印章、节奏条、藏身处提示。

## S210 星露谷视角小镇调研
- 星露谷地图层 Back/Buildings/Paths/Front/AlwaysFront；Front 层的树冠盖住北边的人；Warp 门：https://stardewvalleywiki.com/Modding:Maps
- 一天 6:00–2:00，NPC 按日程：https://stardewvalleywiki.com/Day_Cycle
- 塞尔达 II 俯视大地图 + 横版区域：https://zelda.fandom.com/wiki/Zelda_II:_The_Adventure_of_Link
- 潜行：视锥、掩体、分级警戒：https://gamedesignskills.com/game-design/stealth/ 、https://www.gamedeveloper.com/design/stealth-game-design
- 结论：小镇 = 抢时间 + 选埋伏点的前奏，不是第二套战斗；房间规则不变。

## S211 场景切换调研
- https://github.com/mygamedevtools/scene-loader （TransitionAsync：先盖住再加载）
- https://github.com/Lazy-Solutions/AdvancedSceneManager （Loading Screen / Transitions / 常驻场景）
- https://github.com/vimsos/unity-scene-handling （同步 LoadScene 掉帧；异步 + 明确激活）
- https://github.com/starikcetin/Eflatun.SceneReference （场景要登记且勾选；路径引用）
- https://docs.unity3d.com/ScriptReference/AsyncOperation-allowSceneActivation.html

## S212 转场/指引/编辑器调研
- 插件评估：mygamedevtools/scene-loader 4.1.1 是最后支持 2022.3 的版本（MIT）；收益小 + 与直接 LoadScene 混用会记错当前场景 → 不装，只借流程。
- https://developers.meta.com/horizon/blog/avoiding-hitches-when-loading-scenes-in-unity/ （激活卡顿随物体数增长 → 动画单帧封顶）
- https://www.reddit.com/r/Unity3D/comments/m7j2xh/best_way_to_load_a_scene_async_without_stutter/ （用淡出盖住）
- https://www.reddit.com/r/Unity3D/comments/1u5tg2f/made_a_themed_loading_transition_is_it_cool_or/ （S 曲线）
- https://www.reddit.com/r/gamedesign/comments/lh58im/other_ways_to_tell_the_player_where_to_go/ 、https://www.reddit.com/r/Games/comments/29shsy/rgames_mechanic_discussion_breadcrumb_trail/ （边缘箭头、按需面包屑）
- https://github.com/jinincarnate/off-screen-indicator 、https://github.com/prime31/TransitionKit （圆形转场）、https://github.com/deepnight/ldtk （编辑器快捷键/吸管/悬停）

## S218 小镇大机关 / 涌现 / 总管全局调研（报告：repo/docs/step1/S218_BIG_TOWN_PRANKS.md）
- BotW 化学引擎三条规则 + 2D 原型验证乘法：https://www.thumbsticks.com/gdc-17-breath-of-the-wild-science-lies/ ，https://www.thumbsticks.com/gdc-17-breaking-conventions-breath-of-the-wild/
- Into the Breach 完全可预判：https://media.gdcvault.com/gdc2019/presentations/Into%20the%20Breach%20Postmortem%20Final.pdf ，https://www.youtube.com/watch?v=s_I07Iq_2XM
- 输入随机 vs 输出随机：https://www.gamedeveloper.com/design/randomness-and-game-design ；Sid Meier 随机心理：https://www.gamedeveloper.com/game-platforms/gdc-sid-meier-s-lessons-on-gamer-psychology ；XCOM 隐藏修正：https://xcom.fandom.com/wiki/Game_difficulty_(XCOM_2)
- Noita 涌现两面 + 永久卡死：https://www.youtube.com/watch?v=prXuyMCgbTc ，https://www.gamedeveloper.com/game-platforms/road-to-the-igf-nolla-games-i-noita-i-
- Untitled Goose Game（密集危险区 + 空地、单向门）：https://www.youtube.com/watch?v=tA-64QuWgLk ，https://untitledgoosegame.fandom.com/wiki/To-Do_List
- Hitman：https://www.youtube.com/watch?v=sQ0LlNfr8ZI ，https://www.pentadact.com/2016-05-14-rewarding-creative-play-styles-in-hitman/
- 箱庭：https://book.leveldesignbook.com/process/layout/typology/gates ，https://book.leveldesignbook.com/process/scripting/doors ，https://www.gamedeveloper.com/design/outer-wilds-critical-analysis ，https://zelda.fandom.com/wiki/Bombers%27_Notebook
- 编辑器：https://doc.mapeditor.org/en/stable/manual/worlds/ ，https://ldtk.io/docs/general/world/ ，https://github.com/deepnight/ldtk/issues/1160 ，https://github.com/deepnight/ldtk/issues/712 ，http://blog.joelburgess.com/2013/04/skyrims-modular-level-design-gdc-2013.html
- 反面：http://www.megabearsfan.net/post/2016/07/18/How-open-world-games-fail-to-use-space.aspx ，Mario Maker https://www.gamedeveloper.com/design/lessons-of-game-design-learned-from-super-mario-maker
- 结论：夸张 = 放大作用范围，不另起规则；随机只在决定前公布；连锁 + 天气 + 马里奥学习相乘 = 涌现；地形只打开不关死、只活一天。质疑后没做：牛群 / 停电 / 钟楼 / 真物理 / 隐藏保底。

## S219 瞄准炮 / 高地视线 / 天气灾害调研（报告：repo/docs/step1/S219_AIM_CANNON_MOUNTAINS_STORMS.md）
- DK 炮桶三类（自动 / 按键 / 先瞄准）：https://donkeykong.fandom.com/wiki/Barrel_Cannon
- Shadow Tactics 视锥、高地、一致性：https://www.gamedeveloper.com/design/game-design-deep-dive-dynamic-detection-in-i-shadow-tactics-i- ；高物体全挡 / 中等可探头：https://kosmonautblog.wordpress.com/2017/01/09/shadow-tactics-rendering-breakdown/
- BotW 雷暴随机劈金属（反面：被困、抱怨）：https://zelda.fandom.com/wiki/Thunderstorm ，https://www.resetera.com/threads/never-ending-thunderstorm-in-breath-of-the-wild.2457/
- Don't Starve 避雷针（把闪电引到固定点）：https://dontstarve.wiki.gg/wiki/Lightning_Rod/DST ；酸雨改环境：https://dontstarve.wiki.gg/wiki/Acid_Rain
- Into the Breach 潮汐（提前标出、不冲断孤岛）：https://gamefaqs.gamespot.com/pc/205477-into-the-breach/faqs/76363/archive-tidal-waves
- 结论：落点先画出来再发射；召唤型闪电不随机劈；灾害只"打开"地形、只在湿天被冲击触发；天气池跟地图格局走。没做：山崩堵路、随机劈人、酸雨扣血、长按转炮。

## S221 全流程模拟自检（报告：repo/docs/step1/S221_FULL_FLOW_SIM_FIXES.md）
- 做法：写探针（同一份 OverworldTown），量"最长连续定身 / 两次定身之间能动多久 / 拖住秒数 / 自伤次数"，不只看"通过没"。新系统一定让机器人也走一遍（否则没人测过）。
- DbD 生命状态 + 挨打加速：https://deadbydaylight.fandom.com/wiki/Health_States ，https://deadbydaylight.wiki.gg/wiki/Patch_Notes_6.1.X
- 连控：https://www.g2a.com/news/glossary/what-is-stun-lock-in-gaming/ ；起身无敌：https://wiki.supercombo.gg/w/The_Wakeup_Game
- 吃豆人能量豆：https://pacman.fandom.com/wiki/Power_Pellet ；Spy vs Spy 自伤陷阱：https://en.wikipedia.org/wiki/Spy_vs._Spy_(1984_video_game)
- 结论：保护期从站起来算；学会 = 用时间躲（等），不是免疫。

## S222 全项目切碎调研（总方案：repo/docs/step1/S222_RESEARCH_MASTERPLAN.md，§2 有全部来源表，**不要重复调研**）
- 9 块：核心幻想 / AI 可读性 / 关卡 UGC / 小镇 / 喜剧 / 手感 / 美术 / 自定义 / 统计试玩。
- 失败教训：Hello Neighbor（学习看不见）、Neighbours from Hell（固定线不耐玩）、Dreams / Zucconi（编辑器没人用）、Loop Hero / Legend of Keepers（重复）。
- 可读性：Thief 离散警觉、Shadow Tactics 视锥填充、Mark of the Ninja 声音圈、Invisible Inc 被看见不立刻输、SpyParty 解释为什么被抓、Reddit "可读 > 复杂"（Half-Life 包抄被删）。
- 喜剧：Polaris Mechanical Comedy 12 成分（https://polarisgamedesign.com/2025/mechanical-comedy-in-games/）、Bergson 机械镶嵌在活人身上、GDC Comedy Through Patterns、鹅鹅鹅。
- 统计：NIST 推荐 Wilson（https://itl.nist.gov/div898/handbook/prc/section2/prc241.htm）；rule of three → 60 次全对 = 95% 把握失败率 < 5%；试玩 6 人找问题 / 12 人了解玩家 / 100 人量化（https://gamesuserresearch.com/how-many-players-do-i-need-for-a-playtest/）。
- 做法教训：机器人必须先验证"不同种子 → 不同结果"再谈样本数。

## S225 第二轮调研（方案：repo/docs/step1/S225_RESEARCH_V2_EXECUTION_PLAN.md，§2 两轮来源表，**不要重复调研**）
- 结论：房间没有干等问题（17 个房间死区 0%）；真问题是"按了没反应"（L/P 失败静默，已修）和"他只会说嗯？"（已修）。
- 下一步只剩 E6 交互矩阵 / E7 无障碍 / E10 小镇系统可见度（文档或 sim 统计）；E9 情境按键栏要用户确认。
- 搜过但没价值的（别再搜）：NDC 2026、韩国 Inven、知乎新手引导、あつ森落とし穴视频、itch prank 标签（反爬）。

## S226 自我对照
- 不再拿试玩当前提：S225 E5/E6/E7/E9/E10 已完成，结论在 repo/docs/step1/S226_SELF_AUDIT_AND_DEFERRED.md；仍不做的清单在 §1b。不要再调研同一批来源。

## S227
- 阶段 D 由 `Step1ExitReport` 自动出表（体检"第 1 步出口"）。新列加在 CSV 末尾时：同时加进 `Step1ExitReport.Columns`（sim 检查表头一致）。

## S228 调研定值（报告 repo/docs/step1/S228_RESEARCHED_NUMBERS_BELL_WINDOW.md，不要重复调研）
- 反应时间：视觉简单 ≈0.19 s https://pmc.ncbi.nlm.nih.gov/articles/PMC4456887/ ；Hick 定律 a=200ms b=150ms/bit https://reactscore.com/choice-reaction-test/
- 速度：DbD 杀手 110–115% https://deadbydaylight.fandom.com/wiki/Movement_Speeds ；吃豆人 Dossier https://pacman.holenet.info/ ；DbD 木板晕 2 s https://deadbydaylight.wiki.gg/wiki/Pallets
- 视野一致性：Shadow Tactics https://www.gamedeveloper.com/design/game-design-deep-dive-dynamic-detection-in-i-shadow-tactics-i- ；声音只引去看：Mark of the Ninja https://critpoints.net/2015/03/30/stealth-game-spotting-deconstruction/ ；Hitman 噪音气泡 https://www.polygon.com/hitman-distraction-world-of-assassination/ ；被耍过保持警觉 Invisible Inc https://www.gamedeveloper.com/design/lesson-the-problems-of-modern-stealth-design-and-how-invisible-inc-solves-them

## S229 死亡与重开（docs/step1/S229_DEATH_ENDS_DAY.md）
- 快速重开 https://game-design-snacks.fandom.com/wiki/Quick_restarts_keep_the_player_involved. ；Smash 自爆/击杀归属 https://www.ssbwiki.com/Self-destruct ；炸弹人平局 https://bomberman.fandom.com/wiki/Sudden_Death ；Spelunky 公平 = 危险看得见 https://www.gamedeveloper.com/design/a-spelunky-game-design-analysis-pt-2

## S230 调研落地核对表
- docs/step1/S230_RESEARCH_VS_CODE_AUDIT.md §1：每条调研要求 → 代码证据 → 状态。新调研先查这张表，避免重复做或漏做。
