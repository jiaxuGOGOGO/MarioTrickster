# S222 全项目切碎调研 · 内外对照 · 总执行方案

> 用户原话（摘要）：把项目切碎，去网上（itch.io / GitHub / Reddit / GDC / 论文 / 书）找一切相关资料，单独研究也合起来研究；再用外部视角回看项目（美术、机制、玩法、人物物理交互、画面交互），参照成功和失败的经验质疑反思，形成**以后不用反复大修大改**的细致执行方案，把好的参考直接放进方案；先执行一部分，再去新网站复查，用统计学确认，最后详细自我审计。
>
> 本文是**以后每次开工先读的总方案**。每一阶段写明：做什么 → 参考（已读过原文，不用再调研）→ 验收标准 → 证伪条件 → 不做什么。宪法 H1–H10、三层判定、开发顺序优先于本文。

---

## 0. 一页结论

1. **核心问题不是"缺功能"，是"没被真人验证"。** 宪法第 5 节写着"第 1 步退出条件：连玩 20 局仍想玩 + ≥3 种坑法，做不到就停下，不加系统"。S210–S221 加了小镇、天气、大炮、山地、心、雷云——全都在第 1 步还没通过时加的。外部经验（Hello Neighbor、Dreams、Neighbours from Hell）都说明：系统越多，越要早点让真人玩。**接下来 3 个阶段先补验证，不再横向加系统。**
2. **自动测试以前把同一个样本当成了 N 个样本。** S222 审计发现：机器人除"乱按 / 反应慢"外完全确定，"会躲的 12/12" = 同一天复制 12 遍，**统计上只有 1 个样本**。已修：机器人"手抖模式" + 每个样板镇 60 天 + Wilson 95% 下限（见 §5）。
3. **被抓要说"凭什么"。** SpyParty、Invisible Inc、Shadow Tactics 的共同点：玩家被发现时能看懂原因。宪法第 3 层要记"被抓服不服气"，但游戏以前只说"被抓了"。已做：被抓提示多一行原因（视线里 / 路灯下 / 贴身 / 草晃了 / 伪装还在动 / 挑衅引来的）。
4. **好笑来自"他的反应"，而现在他只有一句"被坑晕了！"。** 鹅鹅鹅、Polaris《Mechanical Comedy》、Bergson"机械镶嵌在活人身上"都指向同一件事：受害者要有夸张、可预期又可打断的反应。这是阶段 B 的唯一主任务。
5. **美术是占位。** 只有 4 张导入图，其余约 30 处代码画方块。美术放在核心验证之后（阶段 D），但现在就把"规格"定死，免得以后返工（§4.7）。

---

## 1. 项目切成 9 块（每块单独研究，再合起来）

| # | 块 | 现状（S221 代码） | 最大风险 |
|---|---|---|---|
| F1 | 核心幻想：捣蛋者 vs 冲冲马里奥 | 房间：布置→引诱→连锁；评分 Step1Combo.Score | 没人连玩过 20 局 |
| F2 | AI 可读性 / 感知 / 学习 | 起疑表 ?→!→!!→?!；H4 只走视线/声音；吃过亏会躲 | 学习"看不出来" = Hello Neighbor 的坑 |
| F3 | 关卡与 UGC（网页设计台 + 工坊） | 向导、印章、节奏条、自动检查 | 空白画布、没人玩别人的关 |
| F4 | 小镇（大地图） | 日程、埋伏、伪装、高草、路灯、天气、大炮、山、心、雷云 | 规模膨胀、和房间脱节 |
| F5 | 涌现与喜剧 | 连锁、天气乘法、自伤 | 反应太单薄，笑点少 |
| F6 | 手感与物理 | 击飞、顿帧、震屏、保护期 | 只在沙盒数字上验证 |
| F7 | 美术与画面交互 | 4 张图 + 代码方块 + 18 个像素图标 | 占位太久，审美判断无法做 |
| F8 | 自定义（性格、规则、关卡包） | 3 种性格、关卡包导入导出、调参 SO | 自定义太多 = 没人用 |
| F9 | 统计与试玩方法 | sim 体检 + 机器人 + 20 局表 + miniPXI | 样本不独立、真人样本太少 |

---

## 2. 外部资料（已读原文，按块整理）

> 格式：来源 | 借来的规则 | 在本项目的落地。只借规则，不借名字/美术/关卡。

### F1 核心幻想 / 同类游戏（成功与失败）

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Untitled Goose Game：统一主题](https://www.gamedeveloper.com/design/untitled-goose-game-the-importance-of-having-a-unified-theme) · [IGF 访谈](https://www.gamedeveloper.com/game-platforms/road-to-the-igf-house-house-s-i-untitled-goose-game-i-) | 每个新东西先问"这符合'坏鹅捣乱'吗"；受害者的反应才是奖励 | 新机制提案多一条必答："他中招的样子是什么？" |
| [Neighbours from Hell 评论](https://gamefaqs.gamespot.com/pc/915237-neighbors-from-hell/reviews/159150) · [Back from Hell 评测](https://culturedvultures.com/neighbours-back-from-hell-ps4-review/) | **失败**：恶作剧是一条固定线，玩一次就没了；"潜力没发挥" | 每关 ≥3 种坑法（P1）+ 性格换着来；不做"唯一正解"的关 |
| [Deception IV 评测](https://gamecritics.com/brad-gallaway/deception-iv-blood-ties-review/) | 陷阱接力好玩，但时机太难 → 挫败 | 已有：落点预画、连锁检查；继续保持"先看到再触发" |
| [Legend of Keepers 评测](https://gamecritics.com/brad-gallaway/legend-of-keepers-career-of-a-dungeon-manager-review/) | **失败**：管理层重复，后期每局一样 | 小镇不追求"一局更长"，追求"每天不一样"（性格/天气/学习） |
| [Loop Hero 复盘](https://www.gamedeveloper.com/design/postmortem-loop-hero) · ["卡在重复里"](https://game-wisdom.com/analysis/loop-hero) | 间接控制可行；但缺少中途决策会重复 | 宪法支柱 3"一直有事做"：死区 < 15%（P4）必须测 |
| [Spy vs Spy 陷阱与解法](https://www.apl2bits.net/2019/07/08/mad-magazine-spy-vs-spy/) | 每个陷阱都有对应的"解药"，自伤也好笑 | 已有：自伤（滚石碾自己）；保持"每种坑都有一种躲法"（A2） |
| [Hello Neighbor 设计访谈](https://www.gamedeveloper.com/design/designing-a-domestic-hunter-killer-thriller-the-i-hello-neighbor-i-way) · [失败复盘](https://unwinnable.com/2022/01/21/goodbye-and-good-riddance-to-hello-neighbor/) | **失败**：宣传"会学习的 AI"，玩家感觉不到/觉得是 bug；没有引导 | **学习必须写在他头上**（已有 WARY / WAIT 标记）；不对外宣称"AI 会学习"，只说"吃过亏的他会绕开" |

### F2 AI 可读性 / 感知

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Thief 感知系统](https://www.gamedeveloper.com/programming/building-an-ai-sensory-system-examining-the-design-of-i-thief-the-dark-project-i-) · [Thief 复盘](https://www.gamedeveloper.com/design/postmortem-i-thief-the-dark-project-i-) | 警觉是**离散档位**（有人？在哪？是谁？），每档有明确表演 | 已对齐：?（有动静）→ !（去查）→ !!（认出你）→ ?!（跟丢） |
| [Shadow Tactics 动态侦测](https://www.gamedeveloper.com/design/game-design-deep-dive-dynamic-detection-in-i-shadow-tactics-i-) | 视锥里黄色从眼睛往你这边"灌"，灌到你=被发现；规则对所有敌人一致 | 阶段 C：起疑值画成视锥里的填充（不是新规则，只是把 SuspicionMeter 画出来） |
| [Mark of the Ninja 声音可视化](https://designingsound.org/2013/06/17/the-dynamics-of-mark-of-the-ninja/) | 声音 = 画面上的圆圈，静音也能读（H6） | 已有：雷云范围圈；阶段 C：挑衅/草晃也画圈 |
| [Invisible Inc 为什么好](https://www.gamedeveloper.com/design/lesson-the-problems-of-modern-stealth-design-and-how-invisible-inc-solves-them) · [Pentadact 分析](https://www.pentadact.com/2014-12-29-what-works-and-why-invisible-inc/) | 被看见不是立刻输；信息全公开，失败是"我的决定" | 已有：被抓回出生点不结束一天；**S222 被抓说原因** |
| [SpyParty 的 AI（Chris Hecker）](https://www.gamedeveloper.com/design/ai-hiding-among-ai-chris-hecker-on-i-spyparty-s-i-devious-single-player) | 给 AI 每个嫌疑对象一个"信心值"，会**钻牛角尖认错人**；事后告诉玩家"他为什么开枪" | **S222 已做"被抓原因"**；阶段 C：一天结束时列"他今天怀疑过你的 3 个时刻" |
| [Alien Isolation 的 AI](https://www.gamedeveloper.com/design/the-perfect-organism-the-ai-of-alien-isolation) | 导演 + 威胁值；行为**按时间解锁**，玩家感到"它学会了" | 宪法第 3 步"学习（行为解锁式）"正是这个；不做神经网络 |
| [Reddit：更复杂的 AI 更好玩吗](https://www.reddit.com/r/gamedesign/comments/119yejw/is_a_more_complex_ai_actually_more_fun/) | Half-Life 包抄被删，因为玩家看起来像"瞬移"；"智胜一个简单 AI 比看不懂复杂 AI 爽" | AI 每个新动作都要有头顶标记（H6）；看不懂的聪明 = 删 |
| [Wayline：潜行 AI 可预测问题](https://www.wayline.io/blog/predictable-problem-stealth-game-ai-overhaul) | 提议用性格、不完美信息；（也提议强化学习——**不采纳**：不可读、不可测） | 性格差异已有（A6）；不完美信息 = H4 |
| [Hitman 社交信号与空间](https://www.gamedeveloper.com/design/mapping-out-the-subtle-social-cues-throughout-i-hitman-i-s-level-design) | 公开 / 私密 / 禁区三种空间，玩家一眼看出"这里能不能待" | 阶段 D 美术：地面颜色区分"他常走的路 / 安全区 / 他家门口" |

### F3 关卡与 UGC

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Levelhead："玩的问题"](https://www.bscotch.net/post/levelhead-solving-the-play-problem-for-user-generated-content) | UGC 最大的问题不是做关，是**没人玩**；要有推荐和"值得玩"的标记 | 关卡包里加"作者自己通关过"标记（类似马造 Clear Check）——阶段 E |
| [马造 2 Clear Check](https://supermariomaker2.fandom.com/wiki/Clear_Check) | 上传前作者必须自己通关 | 我们的版本：**马里奥 AI 自动通关检查**（已有 H10 检查）+ 作者坑到他 ≥1 次 |
| [马造的设计课](https://www.gamedeveloper.com/design/lessons-of-game-design-learned-from-super-mario-maker) | 玩家做的关常常"恶意"或"无聊"；好关有教学→变化→考试 | 起承转合向导（S208）已对齐 |
| [Zucconi：关卡编辑器复盘](https://www.alanzucconi.com/2015/09/23/the-ugc-dilemma-post-mortem-of-a-level-editor/) | **失败**：编辑器花掉大部分开发时间，却只有极少数玩家用 | **编辑器冻结新功能**，只修 bug，直到第 1 步通过 |
| [Dreams 失败的 50 个原因](https://www.reddit.com/r/PS4Dreams/comments/1c0oc2p/50_reasons_why_dreams_failed/) | **失败**：工具太强、目标不清、作品找不到观众 | 同上：不做"万能编辑器" |
| [LDtk 自动图层](https://ldtk.io/docs/general/auto-layers/) | 作者只画"这里是墙"，贴图按规则自动铺 | 阶段 D：美术进来时用"逻辑格 → 自动贴图"，关卡文本格式不变（防大改） |

### F4 小镇 / 系统规模

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Rain World 生态](https://www.gamedeveloper.com/design/crafting-the-complex-chaotic-ecosystem-of-i-rain-world-i-) | 生态好看但新手看不懂 → 评价两极 | 小镇新东西必须"一句话说清 + 头顶标记" |
| [Escapists 2：日程与荒诞](https://www.gamedeveloper.com/design/boosting-complexity-and-absurdity-of-prison-breaks-in-i-the-escapists-2-i-) | 固定日程让玩家能计划；荒诞来自日程被打乱 | 已对齐（门时间表）；保持"日程公开" |
| [Spelunky 店主](https://www.pcgamer.com/great-moments-in-pc-gaming-killing-spelunkys-shopkeeper/) | 一个强烈反应的 NPC 能撑起无数故事 | 马里奥本人就是"店主"：他的反应 > 镇上的新机关 |

### F5 喜剧

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Polaris《Mechanical Comedy in Games》2025](https://polarisgamedesign.com/2025/mechanical-comedy-in-games/) | 12 个成分：涌现物理、操作复杂、道具、即兴、类型素养、**戏剧反讽**（观众知道而角色不知道）、**间接控制独立角色**、动词少、目标不对称、并置、黑色幽默 | 我们已有 6 个（间接控制、目标不对称、道具、涌现、动词少、戏剧反讽）；缺的是**"角色的反应表演"** → 阶段 B |
| [Bergson《笑》](https://www.gutenberg.org/files/4352/4352-h/4352-h.htm) | 好笑 = "机械镶嵌在活人身上"：一个人像机器一样重复、僵硬 | 马里奥"冲冲型"按日程走 = 天然的机械感；**他中招后应该"僵住—重复—恢复"** |
| [GDC《Comedy Through Patterns》](https://gdcvault.com/play/1035068/Independent-Games-Summit-Comedy-Through) | 先建立模式，再打破 | 他的反应要**可预期**（同一种坑 = 同一种反应，H6），打破来自连锁 |
| [鹅鹅鹅的声音设计（FMOD）](https://www.fmod.com/blog/untitled-goose-game-interview) | 音乐跟着动作变化，钢琴随行为加快 | 阶段 D：起疑档位驱动音乐层（不新增规则） |
| 动画"take"（[School of Motion](https://schoolofmotion.com/blog/how-to-animate-character-takes)） | 准备—停顿—爆发；笑点在停顿 | 阶段 B 反应动画的三段时长参数 |
| [Peggle 极限狂热（TCRF）](https://tcrf.net/Prerelease:Peggle_Deluxe_(Windows,_Mac_OS_X)) | 夸张庆祝让一次成功"值得炫耀" | 连锁 ≥3 时的庆祝（只在房间里，不打断操作） |

### F6 手感与物理

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [樱井：顿帧](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/) · [critpoints 顿帧](https://critpoints.net/2017/05/17/hitstophitfreezehitlaghitpausehitshit/) | 两边一起停；越重的打击停越久 | 已有；保持"按伤害分档" |
| [Reddit：别再用顿帧了](https://www.reddit.com/r/gamedesign/comments/1v5p6ew/please_stop_utilizing_hitstop/) | **反面**：顿帧太多 = 拖沓 | 顿帧只给马里奥中招，不给你走路被碰 |
| [冲击感 19 特征论文](https://arxiv.org/html/2208.06155v3) | 好评游戏比差评游戏在"打击反馈特征数"上多；最常见：顿帧、粒子、震屏、音效、击退 | 阶段 B 的反应表 = 每种坑选 4–6 个特征，不全上 |
| [Celeste 宽容机制](https://maddythorson.medium.com/celeste-forgiveness-31e4a40399f1) | 土狼时间、跳跃缓冲：让"差一点"算你成功 | 你（捣蛋者）的操作也该宽容：按 E 埋伏的"差几格"可放宽（阶段 C 小改） |
| [攻击预警设计](https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing) | 开始和**结束**都要预告 | **S222 雷云最后 1.5 秒闪烁** |
| [确定性回放](https://www.gamedeveloper.com/design/instant-replay-building-a-game-engine-with-reproducible-behavior) | 同种子 + 同输入 = 同结果，就能做回放 | H8 已要求；回放是宪法第 3 步 |

### F7 美术与画面交互

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [Slynyrd 俯视角色动画](https://www.slynyrd.com/blog/2025/3/24/pixelblog-55-top-down-character-animation) · [俯视精灵设计](https://www.sandromaglione.com/articles/pixel-art-top-down-game-sprite-design-and-animation) | 4 方向 × 走 4 帧起步；轮廓先于细节 | 美术规格 §4.7 |
| [Y 排序：按脚底排](https://itsfuad.medium.com/y-sorting-in-2d-games-2983dc745275) | 锚点放脚底，sortingOrder 按 y | 已有 `Order(y)`；规定所有新精灵锚点 = 脚底 |
| [Celeste 图块](https://aran.ink/posts/celeste-tilesets) · [LDtk 自动图层](https://ldtk.io/docs/general/auto-layers/) | 规则自动拼边 | 阶段 D |
| [Lospec 彩色描边](https://lospec.com/articles/pixel-art-outlines-part-2-using-color/) · [Lospec 调色板](https://lospec.com/palette-list/tag/game) | 有限调色板 + 角色描边比背景深 | 角色/危险/背景三组颜色分开 |
| [危险颜色](https://note.com/darkangels_417/n/nb520b22d60f7?hl=en) | 红/橙 = 危险，全局只一种意思 | 已对齐 H6（红格 = 危险）；美术禁止把红用在装饰上 |

### F8 / F9 自定义、统计与试玩

| 来源 | 借来的规则 | 落地 |
|---|---|---|
| [试玩需要几个人](https://gamesuserresearch.com/how-many-players-do-i-need-for-a-playtest/) | 找问题 6 人；了解玩家 12 人；量化意见 100 人 | 第 1 步 = 你自己 20 局（找问题）；陌生人测 3–5 人 → 至少 6 人更稳 |
| [程序化人格（MCTS）论文](https://antoniosliapis.com/papers/automated_playtesting_with_procedural_personas_through_mcts_with_evolved_heuristics.pdf) | 用不同目标的机器人模拟不同玩家（冲刺型/杀怪型/收集型/完美型） | 已有 7 种机器人；**S222 加手抖模式让样本独立** |
| [NIST：比例置信区间](https://itl.nist.gov/div898/handbook/prc/section2/prc241.htm) | 推荐 Wilson 区间（小样本也可靠、下限不会为负） | sim 门槛改用 Wilson 下限 |
| [Rule of three](https://en.wikipedia.org/wiki/Rule_of_three_(statistics)) | n 次 0 失败 → 95% 把握失败率 < 3/n；要 < 5% 需要 60 次 | H10 门槛：**每镇 60 天** |
| [Wilson vs Wald 比较（arXiv 2025）](https://arxiv.org/html/2508.10223v1) | Wald 区间在小样本、接近 0/1 时严重失真 | 不用"成功率 ± 误差"那种写法 |
| [AI 试玩能做与不能做](https://vgm.co/blog/what-ai-playtesting-can-and-can-t-do-and-where-real-players-still-win) | 机器人能找 bug 和卡死，不能判断好不好玩 | 与宪法一致：AI 不宣称"更好玩" |

**二轮查漏（新网站）：** itch.io 的 asymmetric / pranks 标签页、GitHub 俯视视锥项目（[aleverdes/unity-top-down-vision](https://github.com/aleverdes/unity-top-down-vision)、[StealthCastle](https://github.com/macdude95/StealthCastle)）、Reddit r/gamedesign、NIST、arXiv。**没有找到"捣蛋者坑 AI 主角"的直接竞品**（itch.io pranks 标签页抓取只返回了导航，内容为空，算作"未能核实"）。新资料没有推翻前面的结论，只补了统计方法和"可读 > 复杂"的佐证 → 判断：资料够了，再搜改变结论的可能性低。

---

## 3. 合起来研究：三条交叉结论

1. **可读性 × 喜剧 × 学习 是一件事。** SpyParty（解释）+ Hello Neighbor（失败：学习看不见）+ Bergson（机械感）→ 马里奥的每个"状态变化"都要有**表演**：中招有反应、学会有标记、抓你有理由。三者共用头顶标记和 Step1Text，不另起系统。
2. **UGC × 规模 × 验证。** Zucconi、Dreams、Levelhead 都说：工具再强，没人玩就白做。→ 编辑器和小镇**冻结横向扩展**，先让"一个房间 + 一个小镇样板"被真人玩。
3. **统计 × 机器人 × 真人。** 机器人只能证明"不卡、不作弊、规则成立"（第 1 层），并且必须样本独立才算数；"好玩"只能来自第 3 层。→ 每次改动后：sim 统计门槛全绿 → 你玩 → 填表。

---

## 4. 执行方案（按顺序，不跳步）

> 每阶段 = 一个假设 + 一个主变量（宪法第 6 节）。前一阶段的验收没通过，不开下一阶段。

### 阶段 A（S222，已完成）：让"测试"和"被抓"变得可信
- 做了：机器人手抖模式 + 统计门槛；被抓说原因；雷云结束预告；修正进度表顶行。
- 验收：sim 全绿（见 §5）。**你需要做**：Unity 里跑 EditMode 测试；玩小镇被抓 3 次，看原因是否和你的感觉一致。
- 证伪：你觉得"原因说得不对"≥1 次 → 记下来，下一轮修判定顺序（只改呈现，不改数值）。

### 阶段 B（S223 已实现，等你试玩验收）：马里奥的"中招反应"（第 1 步的核心补课）
- 假设：他中招时有三段式反应（愣住 0.2 秒 → 夸张动作 → 恢复），你会更想再坑他一次。
- 做法（只加呈现，不改规则）：
  - 反应表放数据文件（宪法：参数不写死）：每种坑 × {停顿秒数, 动作, 头顶字, 音效名, 震屏档}。
  - 同一种坑永远同一种反应（H6）；连锁时反应叠加，不打断（Comedy Through Patterns）。
  - 反应期间不额外延长晕眩（不改 H9 时限）。
- 参考：§2 F5、F6（樱井、19 特征、School of Motion take）。
- 验收：sim 证明反应不改晕眩时长；你玩 5 局，在表格里记"有没有笑出来"。
- 证伪：5 局里 0 次笑/觉得拖沓 → 缩短停顿，不加更多特效。
- 不做：新机关、新天气、新性格。

### 阶段 C（S224 已实现，并先做了'少等待'）：可读性收尾（把已有的数字画出来）
- 视锥里的起疑填充（Shadow Tactics）；挑衅/草晃画声音圈（Mark of the Ninja）；一天结束列"他怀疑过你的 3 个时刻"（SpyParty）。
- 验收：A1"暂停时能猜到他下一步"自己测 10 次 ≥7 次。
- 不做：新感知通道（H4 不变）。

### 阶段 D：第 1 步出口验证（必须是你玩）
- 连玩 20 局 + ≥3 种坑法（宪法退出条件）；每周一次 miniPXI；H10 不碰键盘检查。
- 统计说明：你手动看 5 局 H10 全过，Wilson 下限只有 0.57，**只能说明"没有明显问题"**；正式的 95% 由 sim 的 60 天/镇承担。
- 通过 → 进入第 2 步（3 个手工房间 + 谨慎/贪财型）。不通过 → 停下来重审核心动作。

### 阶段 E（第 1 步通过后才开始）：美术与 UGC 收口
- 美术规格（现在就定，以后不改）：
  - 格子 1 单位 = 16px；角色 16×24，锚点脚底；4 方向 × 待机 2 / 走 4 / 中招 3 帧。
  - 调色板 ≤ 32 色；红/橙只给危险；角色描边比背景深一档。
  - 文件名沿用 Resources/OverworldArt 同名覆盖（S220 已有），**换图不改代码**。
  - 地形用"逻辑格 → 自动贴图"（LDtk 思路），关卡文本格式不变。
- UGC：关卡包加"AI 通关过 + 作者坑到过他"标记（马造 Clear Check）。

### 永久规则（防止以后大修）
1. 新系统必须过：支柱 ≥2 条 + "他中招/学会的样子是什么" + 消融测试。
2. 新东西先改呈现、后改数值；先加代价、不先禁用（宪法原文）。
3. sim 门槛只许加不许降（"时间不够时砍功能，不降阈值"）。
4. 任何"成功率"写法都要带样本数和 Wilson 区间。

---

## 5. 统计学自查（S222 实测）

**审计发现：** 旧的机器人除"乱按 / 反应慢"外，每个种子玩出完全一样的一天（8 个种子 = 1 种结果）。S213 写的"会躲 12/12"在统计上等于 **1 个样本**。

**修法：** `OverworldBots.PlayDay(..., humanNoise: true)`：每次换目标先愣 0.15–0.6 秒、偶尔走神 0.2–0.5 秒、按 E 大约每 3 帧按到一次。默认关（老测试不变）。

**结果（每镇 60 天，第 1–4 天轮流）：**

| 样板镇 | 会躲的玩家：全埋伏天数 | Wilson 95% 区间 | 不同用时（独立性检查） |
|---|---|---|---|
| 小镇 | 60/60 | [0.940, 1.00] | 14 种 |
| 大镇 | 60/60 | [0.940, 1.00] | 15 种 |
| 山镇 | 60/60 | [0.940, 1.00] | 14 种 |
| 雷镇 | 60/60 | [0.940, 1.00] | 30 种 |
| 小镇·不躲（跟箭头） | 0/60 | [0, 0.06] | —— |

补充探针（不进门槛，记录用）：反应慢的玩家 56–57/60 天全埋伏（约 95%）；贪道具的玩家 0/60（每天被抓约 2.6 次）；雷镇捣蛋型 59/60（第 49 号种子：17:49 被抓 → 第 4 扇门迟到，不是卡死）。

**解读：** "会躲就赢、不躲就输"在 4 个样板镇都成立，95% 把握失败率 < 6%。**这只证明规则成立，不证明好玩**（第 3 层）。

---

## 6. 自我审计（诚实清单）

| # | 问题 | 严重度 | 处理 |
|---|---|---|---|
| 1 | S210–S221 在第 1 步未通过时持续加系统（**更正**：用户 S195 已明确"忽略连玩 20 局门槛"，所以不算违规；但"没人真玩过"的风险仍在） | 中 | 阶段 B–D 冻结横向扩展；S224 用户要求"先做得更值得玩再试玩" → 先砍等待、再画可读性 |
| 2 | 机器人样本不独立，旧报告里的 "N/N" 夸大了证据 | 高 | 已修（§5）；旧数字保留但不再引用为统计证据 |
| 3 | 进度表顶行停在 S219 | 中 | 已修 |
| 4 | 房间里的马里奥（RushMarioMind）没有同样的统计门槛 | 中 | 记为待办：房间 sim 也加手抖 + 60 次（阶段 B 一起做，不单独开） |
| 5 | 被抓原因的判定顺序（伪装 > 草 > 贴身 > 路灯 > 视线）是我定的，可能和你的直觉不同 | 低 | 阶段 A 证伪条件 |
| 6 | 机器人"手抖"参数是拍的，不是从真人数据来的 | 低 | 你的试玩日志有数据后再校准；现在只用来保证样本独立 |
| 7 | itch.io 标签页抓取为空，竞品扫描不完整 | 低 | 已注明；结论不依赖它 |
| 8 | 所有手感/视觉改动在沙盒里看不到 | —— | 必须你在 Unity 里看；我不宣称"更好看/更好玩" |

**规则保障（H1–H10）：** H4——被抓原因只读 CanSee 同一组输入，不新增感知；H6——原因文字是一种信号一种意思；H9——雷云闪烁只是画面，时长不变；H10——统计门槛每镇 60 天全埋伏、全部结束；其余无改动。

---

## 7. S224 追加（用户："有些我都没耐心试玩下去，先做得更值得我玩"）

- 更正 §0 第 1 条与 §6 第 1 条：S195 用户已明确忽略"连玩 20 局"门槛，不是违规；阶段 D 的"20 局"改为"你觉得值得玩时再填表"，不是开工前提。
- 新发现：小镇不快进时 81% 时间是死区（P4 要求 < 15%）→ 先做"少等待"，再做阶段 C。详见 `docs/step1/S224_LESS_WAITING_AND_READABILITY.md`。
- 阶段 C 三件事已全部实现；验收"暂停时能猜到他下一步 ≥7/10"仍需你玩。
