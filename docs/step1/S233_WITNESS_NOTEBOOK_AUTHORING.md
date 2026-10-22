# S233 恶作剧 → 真情：当场喊、居民笔记本、台词本、灵感骰子、网页读你改过的数值

> 一句话：S232 让小镇"有人住"；S233 让**你的恶作剧被人看见、被人记住、被你自己写进去**——砸中马里奥时旁边那户当场喊一句，按 N 翻开笔记本看谁还差什么，关掉游戏再开居民还记得你；写台词不用碰 JSON，卡住了掷一次灵感骰子。不改任何玩法数值（数值版本仍 26）。

## 0. 一页结论

| # | 做了什么 | 你会看到 | 为什么（调研规则，详见 §2） |
|---|---|---|---|
| E1 | **当场喊**：大机关砸中马里奥 → 离那一下最近（7 格内）的那户人家当场喊一句 | 左下角 3.5 秒短句；同一户一天最多喊一次；在他家门口闹满 3 次，老奶奶 / 小孩 / 守夜人会说一段当场的真心话 | 恶作剧要有"观众"才变成故事（Hades 对玩家行为即时回应；Shadow of Mordor 宿敌系统：记住"发生在谁身上"）；当场的话不兜底 = 宁可不说也不重复 |
| E2 | **居民笔记本**（小镇里按 N） | 每户一行：来往 / 守住 / 被偷 / 没赶上 / 门口大动静；听过的真心话原文 + 第几天；没听过的只说"还差什么"（不剧透原文） | 《姆吉拉的假面》炸弹人笔记本：把"还没完成的人情"变成清单，给"再来一天"一个具体理由 |
| E3 | **居民记忆跨次保留** | 关掉 Unity 再开，居民还记得你；测试中心有"🏠 忘掉居民记忆" | 星露谷 / 动森的好感会存档；只存"来往次数 + 听过哪些"，冷却不存（读档第一天照样有话说） |
| E4 | **台词本**（网页） | 大地图页"✎ 台词本"：搜、改中文、改条件 → 当场检查 → 导出替换 `Assets/Resources/TownStories.json` | Ink / Yarn Spinner 的经验：写作者要在"写的地方"立刻看到错误；导出和 Unity 那份逐字一致（sim 检查） |
| E5 | **台词检查**（Unity 体检 + 小镇工坊 + 网页同一套） | 条件名写错（这句永远不会说）、真心话没勾"只说一次"、真心话没有"要挣"的条件、中文太长读不完 | 规则来自 S232 永久规则 13–15；Luban 式"表就是契约" |
| E6 | **🎲 灵感骰子**（小镇工坊 + 网页） | 一掷 = "门 N 谁家门口｜天气｜大机关 + 小机关｜限制"；一键变成一句新台词模板 | Oblique Strategies / 设计卡片：给一个约束比给一张白纸更容易开始 |
| E7 | **网页读你在 Unity 里手动改过的数值** | 网页 🔗 连上项目文件夹 → 读 `RushMarioTuning.asset`；"📐 数值关系"列出你改过哪些、用你的值重新检查 21 条关系 | S232 E4b；Unity 手册：.asset 是 YAML 文本，单个数值是"两个空格 + 名字: 值"一行 |

刻意没做的见 §3。

## 1. 第一性原理：这次要解决什么

| 用户要的 | 拆到最小 | S232 之后还缺什么 | S233 怎么补 |
|---|---|---|---|
| 恶作剧带来的真情流露 | 恶作剧 → 有人看见 → 有人记住 → 某一天说出心里话 | S232 只在"打完一户回小镇"时说话；**恶作剧当下没有观众** | E1 当场喊（看见）+ witnessed 计数（记住）+ 3 段当场真心话（说出来） |
| 足够多的内容 / 以后更想创作 | 写一句新台词的成本 | 要打开 JSON、记条件名、怕写错 | E4 台词本 + E5 检查 + E6 骰子 → 从"想到"到"游戏里出现"只要改一格文字 |
| 方便我参与测试 | 每次测试都有新东西可期待；不会白玩 | 关掉 Play 就全忘；看不见还差什么 | E2 笔记本 + E3 存档 |
| 网页 / Unity / 控制台数值统一 | 一个数只在一个地方被改 | 网页只看得到默认值 | E7 读 .asset |
| 方便未来大一统关卡 / 陷阱联动 | 数据先能描述，再谈系统 | 已有：门 = 房间锚点、连锁 = 冲击半径 | 本轮评估后**仍不做**（§3），把接入点写清 |

## 2. 资料表

（本轮新来源与借来的规则；全部是读过原文的。）

| 方向 | 来源 | 借来的规则（一句） | 落地 |
|---|---|---|---|
| A 笔记本 | Zelda Dungeon Wiki《Bombers' Notebook》https://www.zeldadungeon.net/wiki/Bombers%27_Notebook | 按"人"记：每人一栏 + 时间/地点；一个人的事全做完 = 一张开心贴纸；3DS 版用"传闻"提示没做完的事，不给结果 | E2 每户一行；没听过的只写"还差什么"；E11 贴纸（候选） |
| A 好感 | Stardew Valley Wiki《Friendship》https://stardewvalleywiki.com/Friendship | 1 颗心 = 250 点；不说话每天掉 2 点；心事件按心数解锁 | 只借"按次数解锁"；**不借**点数和衰减（E10） |
| A 好感 | Nookipedia《Friendship》https://nookipedia.com/wiki/Friendship | 动森 0–255 隐藏值，起点 25；每天第一次说话 +1、帮忙 +13；玩家看不到数字 | 我们反过来：只用玩家自己做过的事，并在笔记本里写明 |
| B 反应式叙事 | Christi Kerr《How the dialogue system in Hades rewards failure》https://www.christi-kerr.com/post/how-the-dialogue-system-in-hades-rewards-failure | 三档：常青 / 针对玩家行为 / 主线（主线压过一切）；对话说完前不重复；死了也有人评论你"刚才怎么死的" | S232 tier 已有；E1 当场评论"刚才那一下" |
| B 记忆 | Game Developer《Designing Shadow of Mordor's Nemesis system》https://www.gamedeveloper.com/design/designing-i-shadow-of-mordor-i-s-nemesis-system | "记住玩家做过的事，并说出来"；死亡不是终点而是故事起点；"玩家做了什么，永远别打断，接着往上搭" | E1 + E3 存档；witnessed 计数 |
| B 程序叙事 | Game Maker's Notebook《Balancing procedural and intimate storytelling in Wildermyth》https://gamemakersnotebook.libsyn.com/balancing-procedural-and-intimate-storytelling-in-wildermyth | 程序生成框架 + 手写的亲密时刻 | 真心话手写、触发条件程序化 |
| C 写作工具 | Yarn Spinner《Storylets and Saliency: a primer》https://docs.yarnspinner.dev/write-yarn-scripts/advanced-scripting/storylets-and-saliency-a-primer | 先过滤掉条件不满足的，再选；默认策略 = "最相关 + 最久没看过 + 同分随机" | 和我们 S232 Pick 一致（有了第二个独立来源） |
| C 写作工具 | Ian Thomas《Storylets explained》https://wildwinter.medium.com/storylets-explained-ff5a24842bd9 | storylet = 前提 + 内容 + 效果；每段自成一块；重复规则：总是 / 从不 / X 次内不重复；先用纸牌原型测 | once / coolDays；witness 不兜底 |
| C 写作工具 | Programming Historian《Interactive text games using Twine》https://programminghistorian.org/en/lessons/interactive-text-games-using-twine | Twine 几分钟就能写出第一个；条件就写在文字旁边 | 台词本：条件和中文在同一行改 |
| C 失败 | PC Gamer《Disco Elysium had so much text it broke the branching narrative software》https://www.pcgamer.com/games/rpg/disco-elysium-had-so-much-text-it-broke-the-branching-narrative-software-we-were-writing-too-much/ | 好工具让人写得太多、来不及编辑 | 检查里加"中文太长"、覆盖率提示；不追求数量 |
| D 灵感 | Stanislav Stankovic《Oblique Strategies for Game Design》https://stanislav-stankovic.medium.com/oblique-strategies-for-game-design-5688e206a90f | 卡住不是没点子，是半成品太多；一张隐晦卡片 = 换个方向 | 骰子的"限制"一栏 |
| D 灵感 | Diegetic Games《Decks of prompts》https://diegeticgames.com/blog/2021/04/12/decks-of-prompts.html | 提示卡可以按顺序也可以洗牌；洗牌 = 每次不同；可以往牌堆里插关键卡 | 骰子按种子洗；"这户今天第 3 次来往"是插入的关键卡 |
| D 灵感 | ABA Games《Joys of small game development: ideas》https://abagames.github.io/joys-of-small-game-development/ideas/ | 随机组合两个不搭的词（"会反弹的导弹""会打架的屋顶"），硬做成游戏 | 机关 × 机关 × 天气 × 人 |
| D 灵感（日） | 2dgames.jp《ゲームアイデアの発想法》https://2dgames.jp/entry/game-idea-creation/ | 曼陀罗九宫格、思维导图、每天一个点子（アイデアマラソン） | 每次打开工坊掷一次 = 每天一个点子 |
| E 数值文件 | Unity 手册《Format of Text Serialized files》https://docs.unity3d.com/6000.0/Documentation/Manual/FormatDescription.html | 每个对象一段 YAML，`---` 开头；字段 = 缩进 + 名字: 值 | `TuningAudit.FromYaml` 只认两格缩进的标量 |
| E 数值文件 | Unity Blog《Understanding Unity's serialization language, YAML》https://unity.com/blog/engine-platform/understanding-unitys-serialization-language-yaml | Unity 用 YAML 子集（不支持注释、空行等）；**手改 .asset 有风险、出错没提示** | 网页**只读不写** |
| F 联动 | LittleBigWiki《Logic》https://littlebigplanet.miraheze.org/wiki/Logic | LBP1 只能用物理件拼与门；LBP2 才有电池 / 与门 / 开关 + 微芯片打包 | E8 冻结理由：先有好用的打包方式再加信号线 |
| F 联动 | Super Mario Wiki《ON/OFF Switch》https://www.mariowiki.com/ON/OFF_Switch | 一个全局开关同时改虚线块、轨道、传送带——一种颜色一种意思 | 将来做 E8 先做"全镇一个开关"，不做自由连线 |
| F 联动 | The Verge《Tears of the Kingdom physics, GDC 2024》https://www.theverge.com/24115898/tears-of-the-kingdom-physics-gameplay-design-gdc-2024 | 一切都走同一套物理，不为单个物件写专门代码 | 永久规则 20：所有"砸中"都走 HitMario |
| F 世界 | Tiled《Worlds》https://doc.mapeditor.org/en/stable/manual/worlds/ | .world = JSON 列出每张地图的文件名 + 像素坐标 | E9 将来的格式 |
| F 世界 | LDtk《World》https://ldtk.io/docs/general/world/ | 四种布局：横排 / 竖排 / 自由 / GridVania（尺寸对齐世界格子） | E9 先用"横排"（一天一张镇） |
| G 失败 | r/gamedesign《Ditch quest logs, replace with just logs》https://www.reddit.com/r/gamedesign/comments/1gqbt6i/ditch_quest_logs_replace_with_just_logs/ | 任务日志变成"购物清单"、跟箭头不看世界；建议只记"发生过什么" | 笔记本记"发生过的事"，不标箭头、不催 |
| G 失败 | Stardew 论坛《Turn off journal notifications》http://forums.stardewvalley.net/threads/turn-off-journal-notifications.10742/ | 一直抖动的 "?" 提示很烦，没法关 | 笔记本**不弹提醒**，按 N 才看 |
| G 失败 | r/AnimalCrossing《Friendship levels》https://www.reddit.com/r/AnimalCrossing/comments/163yhgs/friendship_levels/ | 看不到好感、每天送礼仍然很慢 → 去网上找"刷"的攻略 | E10 不做点数；笔记本写明还差什么 |

没读到（空 / 墙）：GDC Vault 两个视频页（Wildermyth、Nemesis）只有标题；inkle 官网和 GitHub issue 没取到正文。

## 结论（规则）
1. 恶作剧要有观众：当下有人说一句，比事后总结更像"被看见"（Hades / Nemesis）。
2. 记住 → 说出来：存"做过什么"，下次提起（Nemesis "memory"）。
3. 选句 = 先过滤，再挑最具体 + 最久没说（Yarn Spinner 默认策略 = 我们 S232 的规则）。
4. 每段只说一次的真心话要"挣"，并告诉玩家还差什么，但不剧透（炸弹人笔记本"传闻"）。
5. 不用点数、不衰减、不催（星露谷 / 动森的失败点）。
6. 写作者在写的地方立刻看到错误（Twine）；但防止"写太多来不及编辑"（Disco Elysium）。
7. 卡住时给约束，不给白纸（Oblique Strategies / 提示卡 / 随机组合）。
8. .asset 只读不写（Unity：手改没有错误提示）。
9. 联动先做"一种颜色一个全局开关"，后做自由连线（SMM2 → LBP2 的顺序）。
10. 一切砸中都走同一个入口（TotK：不为单个物件写专门代码）。

## 3. E 表（执行情况）

| # | 内容 | 状态 | 接入点 / 理由 |
|---|---|---|---|
| E1 | 当场喊 | **做了** | `TownStory.WitnessOn / Witness / NearestDoor`（纯函数）；`OverworldTown.HitMario` 记 `hitSerial / hitX / hitY / hitKind`（只给居民用，H4）；`OverworldGame` 每帧比对序号 |
| E2 | 居民笔记本 | **做了** | `TownStory.Notebook / NotebookText / NeedHint`；N 键；网页 + 小镇工坊显示"彩排 14 天后的笔记本" |
| E3 | 跨次存档 | **做了** | `TownStory.MemToJson / MemFromJson`；`OverworldGame.LoadMemory / SaveMemory`（PlayerPrefs，每天结束存）；测试中心"忘掉居民记忆" |
| E4 | 网页台词本 | **做了** | `app.js owLinesRender / tsToJson`（= C# `ToJson` 逐字）；改过的存在浏览器里，可一键恢复 |
| E5 | 台词检查 | **做了** | `TownStory.Validate` = 网页 `tsValidate`；体检、小镇工坊、网页三处 |
| E6 | 灵感骰子 | **做了** | `Overworld/IdeaDice.cs` = 网页 `idRoll`；不改地图 |
| E7 | 网页读 .asset | **做了** | `TuningAudit.FromYaml / DiffFromDefault` = 网页 `tuFromYaml / tuDiff / tuApply`；连上文件夹自动读；体检也列"你改过哪些" |
| E8 | 陷阱信号线（传感器 → 与/或/计时 → 机关） | 记录，**不做** | 第 1 步还没通过；现有连锁 = "冲击 1.5 格内"已够表达"一串"；信号线的接入点仍是 `OverworldProps.ChainTargets` |
| E9 | 大一统世界清单（多张小镇 + 跨镇连接） | 记录，**不做** | 192×128 一张小镇够第 1 步用；`CampaignLedger` 已按门汇总，将来 = 多张图的 Ledger 拼接 |
| E10 | 好感度"点数"（星露谷式 250 点 / 心） | **刻意不做** | 失败案例：数值化的好感会变成"刷"（§2 F 表）；我们只用"来往了几次"这种你自己做过的事 |
| E11 | 笔记本里的贴纸 / 完成奖励 | 下一步候选 | 先看你翻不翻笔记本 |

## 4. 统计学确认（怎么验证，不能怎么说）

- **逐字对照**：网页和 C# 在当场喊（6 天 × 每扇门 × 2 张样板图）、笔记本全文、最近的门（含离门 6.9 / 7.1 格的边界点）、灵感骰子、台词检查（含故意写错的 3 句）、导出、读 .asset 上一共对照 **142 行**，全部一致。
- **反向测试 3 个**（故意弄坏 → 必须红）：拆掉"一天一户只喊一次" → 红；网页最近的门少算半格 → 红（第一次没抓到，加了边界点后抓到）；检查漏掉错误条件名 → 红。恢复后全绿。
- **节奏**：样板图 3 天彩排后每天每户门口各砸一次、连续 27 天：喊了 83 句，当场真心话 2 段，**任何一天都不超过 1 段**；当场真心话要求门口闹满 3 次。
- **骰子分布**：200 题里 4 种大机关各占 24–26%（门禁 15–35%）。
- **彩排 30 天**（样板图 4 户，不算当场喊）：真心话 11/18 段；没出现的 7 段 = 画家 / 守夜人（样板图上没这两户）+ 3 段当场真心话（彩排不砸人）。**这是条件设计的结果，不代表玩家会不会感动**——那只能由你试玩判断。
- 不能说："更动人了""更想玩了"。能说："具备了被看见、被记住、能自己写的条件"。

## 5. 自我审计（对照用户这次的每一句）

| 用户的话 | 这次 | 诚实说明 |
|---|---|---|
| 增加内容 / 道具 / 随机性 | 当场喊 12 句（台词共 96 句）；灵感骰子 4×8×7×12 种组合 | 道具本身没加新的（S232 洗牌袋已有）；骰子是"创作的随机"，不是玩法随机 |
| 关卡编辑、大一统、陷阱联动 | 两个编辑器都有骰子、笔记本预览、台词检查 | 大一统 / 信号线评估后仍冻结（E8/E9） |
| 真诚故事 NPC、恶作剧带来的真情 | E1 + 3 段当场真心话 + E2 笔记本 | 台词是我按调研写的起点 |
| 足够多内容让我想创作 | E4 台词本 + E6 骰子 | 好不好用要你写一句试试 |
| 网页 / Unity / 控制台数值统一 | E7 | 网页仍是**只读**（不往 Unity 里写数值，避免两边抢） |
| 调研 + 统计 | §2、§4 | — |

## 6. 永久规则（追加）

16. **当场的话不兜底**：`when=witness` 没有合适的句子就不说；同一户一天最多一次。
17. **存档只存"做过什么"**：来往次数、听过哪些、第几天听到；不存冷却、不存今天的状态。
18. **新台词先过 `TownStory.Validate`**：✗ 和 ⚠ 必须是 0（sim 检查默认表）。
19. **网页只读 Unity 的数值**：读 `.asset` 用 `TuningAudit.FromYaml`（只认两个空格 + 名字: 数字），不写回。
20. 新的"大机关砸中"类事件要算进 `OverworldTown.HitMario`（否则居民看不见）。

## 7. 你怎么用

1. 小镇里：用大机关在别人家门口砸马里奥 → 左下角那户人家当场喊。按 **N** 翻笔记本，再按 N 关上（时间照走）。
2. 写台词：网页"大地图"页 → "✎ 台词本"：搜 `baker` 或 `真心` → 改中文 → 下面会马上说哪里写错 → "⬇ 导出 TownStories.json" → 替换 Unity 项目里的 `Assets/Resources/TownStories.json`。
3. 卡住了：小镇工坊或网页"🎲 灵感骰子"掷一次；"把第一题变成一句新台词"会在台词本里加一句模板。
4. 想从头认识一遍：测试中心（Ctrl+Alt+T）→ "🏠 忘掉居民记忆"。
5. 网页数值：点 🔗 连上 Unity 项目文件夹 → "📐 数值关系"会写"你手动改过 N 个"。

## 8. 下一步候选

1. 笔记本贴纸：一户的真心话全部听完 → 那户门口出现一个小标记（看你翻不翻笔记本再定）。
2. 你写 / 改几句台词后，我按你的语气把面包师、镇长、画家的当场真心话补齐（现在只有老奶奶、小孩、守夜人三户有）。
3. 第 1 步通过后：信号线（E8）和多镇世界清单（E9）。
