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
