# MarioTrickster 关卡生成、机制落地与 AI 测试闭环对比落地执行研究报告

作者：**Manus AI**  
日期：**2026-05-17**  
审计仓库：`jiaxuGOGOGO/MarioTrickster`  
审计分支与提交：`master @ 1590dd1`  
对比基线：用户上传的《MarioTrickster 双人对抗平台关卡与精准交互改进方案》、项目交接文档、Level Studio 设计指南、游戏循环实施计划与当前 Unity 源码。[1] [2] [3] [4]

## 1. 结论先行

本次核查的核心结论是：**当前项目已经把上传方案中的大部分机制骨架落到了代码与工具链里，但截图中的 `S2_Validation_4_Combat (白盒验证4-实战房)` 还没有做到“一键生成后即可完整验证拿宝撤离、扫描危机、热度怀疑、路线预算与 AI 测试反馈闭环”。** 它目前更接近一个实战房视觉白盒片段，而不是完整 Gameplay Loop 验证场景。[5] [6]

造成这个判断的最关键证据有三条。第一，`S2_Validation_4_Combat` 在片段说明中写的是“放置 `o` LootObjective 目标与 `G` EscapeGate 出口”，但默认 ASCII 字典仍把 `o` 生成成 `Collectible`，把 `G` 生成成 `GoalZone`，因此截图中验证结果提示 `LootObjective` 与 `EscapeGate` 缺失是合理的源码级结果。[5] [7] 第二，Level Studio 生成后自动补齐的 `EnsurePlayableEnvironment` 主要覆盖 Mario、Trickster、基础 Managers、UGUI HUD、Camera 与 KillZone，并没有同时补齐 `AlarmCrisisDirector`、`TricksterHeatMeter`、`MarioSuspicionTracker`、`PropComboTracker`、`RouteBudgetService`、`CounterRevealReward` 等完整游戏循环服务，所以截图中出现游戏循环服务缺失或不可验证也与源码一致。[8] 第三，AI Arena、自动输入、启发式 Bot、自动指标采集与测试报告复制区已经存在，但它们目前仍是“辅助人工把报告交给 AI 修复”的半自动闭环，还不是“AI 自动消费报告、修改代码、回归验证、写回文档”的全自动良性循环。[9] [10] [11]

| 总体模块 | 当前状态 | 完整度判断 | 主要问题 |
|---|---|---:|---|
| Level Studio 关卡片段生成 | 已可生成四段白盒片段和实战房白盒布局 | **中高** | `S2_Validation_4_Combat` 的文案语义与 ASCII 字典实际组件不一致 |
| 核心机关与游戏循环机制 | 热度、怀疑、扫描、封路、队列、反制、路线预算、拿宝撤离等机制类已存在 | **中高** | Level Studio 普通片段不会自动创建完整服务编排 |
| 截图中的机制验证结果 | 验证结果可信，暴露了实战房生成链路缺口 | **高可信** | 不是单纯 UI 误报，而是生成路径未补齐完整 Gameplay Loop |
| AI 测试反馈闭环 | 已有 AI Arena、Bot 输入、指标采集、报告输出 | **中等** | 仍缺标准化 JSON/Markdown 反馈契约与自动回归执行闭环 |
| 上传方案的完整落地程度 | 已落地机制框架，未完全落地验收契约 | **约 60%–70%** | 缺 InteractionLog 八问、房间语义注释、服务补齐器和一键修复回归 |

## 2. 本次核查范围与方法

本次核查没有重新读取用户上传截图文件，只依据用户消息中可见截图内容、上传方案文本、仓库文档与源码进行交叉验证。截图中可见的信息显示，用户正在 Unity Editor 的 Level Studio 面板中生成并运行 `S2_Validation_4_Combat (白盒验证4-实战房)`，右侧“机制验证结果”弹窗显示基础通过项与若干缺失项，缺失项包括 `LootObjective`、`EscapeGate`、`AlarmCrisisDirector`、`TricksterHeatMeter`、`PropComboTracker` 等机制对象或服务。

核查过程按项目历史优先级读取了 `SESSION_TRACKER.md`、`docs/AI_HANDOFF_PROJECT_STATUS_2026-04-12.md`、`LevelStudio_DesignGuide.md` 与 `docs/GAMEPLAY_LOOP_IMPLEMENTATION_PLAN_2026-05-14.md`，并对 `LevelSnippetLibrary.cs`、`AsciiElementRegistry.cs`、`AsciiLevelGenerator.cs`、`PlayableEnvironmentBuilder.cs`、`TestSceneBuilder.cs`、`TestConsoleWindow.AIArena.cs`、`HeuristicBotInputProvider.cs`、`TestReportRunner.cs` 与 Gameplay 机制类进行源码级核查。[2] [3] [4] [5] [7] [8] [9] [10] [11] [12]

> **审计口径说明。** 本报告判断的是“当前仓库代码与工具链是否按方案形成可重复、可验证、可迭代的落地链路”，不是只判断某个类文件是否存在。因此，当机制类已经存在但 Level Studio 生成路径没有自动创建和接线时，本报告会判为“机制存在，但该关卡生成链路未完整落地”。

## 3. 上传方案的关键要求拆解

上传方案的核心思想不是简单增加机关，而是把 MarioTrickster 从普通平台跳跃房间升级为“短局面棋盘”。方案要求每个代表性房间都具备主路线与影子路线、三类决策节点、有限机关预算、可解释交互日志、冻结玩法盒的美术替换流程，以及“演示房—干扰房—反制房—实战房”的四段白盒验证模板。[1]

| 方案要求 | 方案中的验收含义 | 对项目落地的要求 |
|---|---|---|
| 短局面棋盘 | 房间要让 Mario 与 Trickster 都至少有两种有效选择 | Level Studio 片段需要表达 MainRoute、ShadowRoute、TrapRoles、Budget 与 TestGoal |
| 机关五段生命周期 | 机关不是瞬时陷阱，而是有预警、激活、恢复、冷却和证据 | 机关基类、HUD、调试视图与日志必须展示 Phase 和 Recovery |
| 精准交互语义 | 每次争议交互要能回答 Phase、Shape、Priority、Gate、Visual 等问题 | 需要标准 `InteractionLog`，不只是分散 Debug.Log |
| 四段白盒验证 | 新机制必须经过演示房、干扰房、反制房、实战房 | 片段库和验证器要能一键生成、一键验证、一键修复缺项 |
| 美术替换不改玩法盒 | Root/Visual/GameplayBoxes 分离，换皮后盒体差异为 0 | 导入管线需要冻结碰撞盒与 Pivot，并提供验收报告 |
| AI 测试反馈升级 | 自动测试结果要推动关卡、机制和文档持续改进 | AI Arena 输出需要结构化，被 AI 直接用于下一轮修复与回归 |

## 4. 当前已落地的内容

当前仓库并不是空转状态，事实上已经完成了很多上传方案要求的底层铺垫。`LevelSnippetLibrary.cs` 已包含四段白盒验证片段，`S2_Validation_4_Combat` 的 ASCII 布局也确实组合了 `o`、`P`、`]`、`G`、`[`、`^` 等元素，视觉上已经构成“拿宝—压力机关—出口—封路/队列/伤害”的实战房雏形。[5]

Gameplay 机制层也已经有较完整的类群。`TricksterHeatMeter`、`MarioSuspicionTracker`、`AlarmCrisisDirector`、`CounterRevealReward`、`RouteBudgetService`、`LootObjective`、`EscapeGate`、`PropComboTracker`、`HeatSuspicionBridge`、`VisualFeedbackBridge` 等类说明项目已经把热度、怀疑、证据、扫描危机、反制奖励、路线预算、拿宝撤离和反馈桥接拆成了可维护模块。[12]

| 已落地模块 | 代码位置 | 对方案的贡献 | 当前成熟度 |
|---|---|---|---|
| 四段白盒片段库 | `Assets/Scripts/Editor/LevelSnippetLibrary.cs` | 支持“演示房—干扰房—反制房—实战房”工具化生成 | **中高** |
| ASCII 字典驱动生成 | `Assets/Scripts/LevelDesign/AsciiElementRegistry.cs`、`AsciiLevelGenerator.cs` | 用字符表达平台、道具、敌人、机关和目标 | **中高** |
| 可玩环境补齐 | `Assets/Scripts/Editor/PlayableEnvironmentBuilder.cs` | 生成角色、基础 Managers、HUD、Camera、KillZone | **中等** |
| 完整 Gameplay Loop 专用测试场景 | `Assets/Scripts/Editor/TestSceneBuilder.cs` | 构建包含 Loot、Escape、扫描危机等区域的完整验证长场景 | **中高** |
| AI Arena 自动测试 | `Assets/Scripts/Editor/TestConsoleWindow.AIArena.cs` | 提供挂机、采样、战报和自动重开能力 | **中等** |
| 启发式 Bot 输入 | `Assets/Scripts/Core/HeuristicBotInputProvider.cs` | 让 Mario/Trickster 能在测试中被自动托管 | **中等** |
| 测试报告复制区 | `Assets/Scripts/Editor/TestReportRunner.cs` | 将失败测试整理为可发给 AI 的修复提示 | **中等** |
| Gameplay Boxes 可视化 | `Assets/Scripts/Editor/GameplayBoxVisualizer.cs` | 对 Physics、Hurt、Hit、Scan、Reveal、Trigger 等盒体做调试展示 | **中高** |
| 美术导入 Root/Visual 分离 | `Assets/Scripts/Editor/AssetImportPipeline.cs` | 支撑“换皮不改变玩法盒”的方向 | **中等** |

## 5. 截图中 `S2_Validation_4_Combat` 的源码级解释

截图中最重要的异常并不是“关卡没有生成”，而是“关卡生成结果与机制验证目标不一致”。源码显示 `S2_Validation_4_Combat` 的说明写着“放置 `o` LootObjective 目标与 `G` EscapeGate 出口；配合场景 `AlarmCrisisDirector` 扫描波、`]` 队列机关和 `[` 封路机关形成高压局面”。但同一仓库的 ASCII 默认字典中，`G` 的 `elementName` 是 `GoalZone`，组件是 `GoalZone`；`o` 的 `elementName` 是 `Collectible`，组件是 `Collectible`。[5] [7]

这意味着当前 Level Studio 片段虽然在语义说明层把 `o/G` 解释成 Gameplay Loop 里的“拿宝/撤离”，但生成器实际仍按老语义生成“收集物/终点”。因此，如果机制验证器查找 `LootObjective` 与 `EscapeGate`，它自然会失败。这个问题不是用户操作问题，而是**片段说明、ASCII 字典与机制验证器之间的契约漂移**。

| 验证项 | 片段说明期望 | ASCII 实际生成 | 截图验证结果的合理解释 | 修复优先级 |
|---|---|---|---|---|
| `o` | `LootObjective` | `Collectible` | 验证器找不到 `LootObjective` | **P0** |
| `G` | `EscapeGate` | `GoalZone` | 验证器找不到 `EscapeGate` | **P0** |
| `AlarmCrisisDirector` | 场景服务存在 | Level Studio 可玩环境补齐未创建 | 验证器提示缺扫描危机服务 | **P0** |
| `TricksterHeatMeter` | 高压局面热度服务存在 | Level Studio 可玩环境补齐未创建 | 验证器提示缺热度服务 | **P0** |
| `PropComboTracker` | 连续机关干预可计分 | Level Studio 可玩环境补齐未创建 | 验证器提示缺 Combo 服务 | **P1** |
| `[` 与 `]` | 封路/公开队列机关 | 对应类已存在于 `LevelElements/Traps`，生成器可通过类型扫描挂载 | 该部分更可能已生成，但仍依赖服务接线 | **P1** |

`AsciiLevelGenerator` 的组件挂载逻辑会通过字符串组件名在程序集内解析类型，找不到类型时只打印 warning 并跳过。[13] 当前 `ControllableBlocker` 与 `StateQueueTrap` 的类确实存在于 `Assets/Scripts/LevelElements/Traps`，因此 `[` 与 `]` 本身大概率可以挂载成功；但它们要完整体现路线预算、怀疑证据、热度代价和 UI 反馈，仍需要场景中存在对应服务对象。[14] [15]

## 6. 为什么专用 Gameplay Loop 场景比 Level Studio 片段更完整

源码中存在另一个更完整的验证入口：`TestSceneBuilder.cs` 的 Gameplay Loop 验证场景会显式创建 `GL_LootObjective_Main` 并挂载 `LootObjective`，也会创建 `GL_EscapeGate_Main` 并挂载 `EscapeGate`。同时，它把房间分成 Disguise Entry、Counterplay Probe、Route Fork、Combo Heat、Loot → Escape、Scan Crisis + Counterplay 等段落，明显比单个 `S2_Validation_4_Combat` 片段更接近上传方案要求的综合验证场。[6]

这说明项目内部已经有两条并行路线：一条是 Level Studio 片段库，负责快速生成局部白盒房；另一条是 Test Scene Builder，负责搭建完整机制验证长场景。当前缺口在于两条路线没有统一。用户截图触发的是 Level Studio 路线，所以它没有继承 Test Scene Builder 中已经写好的完整 Gameplay Loop 服务与 Loot/Escape 实体生成逻辑。[5] [6] [8]

| 路线 | 入口 | 优点 | 缺点 | 应如何合并 |
|---|---|---|---|---|
| Level Studio 片段 | `LevelSnippetLibrary` + `AsciiLevelGenerator` | 快、轻、适合拼白盒 | 对 Gameplay Loop 服务补齐不足，字符语义有漂移 | 增加实战房后处理与服务补齐器 |
| Test Scene Builder | `TestSceneBuilder` | 完整机制场景、显式创建 Loot/Escape | 不如片段库灵活，难作为日常关卡积木 | 抽出公共 `GameplayLoopSceneBootstrapper` 供两边调用 |

## 7. 与上传方案逐项对比

总体上，项目已经从“概念方案”进入“机制类和工具链部分落地”阶段，但距离方案中的“可解释、可验收、可自动迭代”仍有差距。最明显的不是机制完全没有，而是机制分散存在，缺少统一契约把关卡生成、验证器、AI 测试和文档回写串起来。

| 方案条目 | 当前实现情况 | 完整度 | 判断说明 |
|---|---|---:|---|
| 标准房间语法：主路线、影子路线、节点、预算、测试目标 | 上传方案给出语义注释示例；项目片段库主要是 ASCII 图和描述，还没有标准注释字段 | **50%** | 片段库能表达空间，但机器不可读的 MainRoute/ShadowRoute/Budget/TestGoal 不完整 |
| 压力脉冲节奏 | 四段白盒片段和 Gameplay Loop 长场景已有分段压力设计 | **70%** | 空间节奏存在，但还缺自动度量每段压力峰值与冷却 |
| 机关五段生命周期 | `ControllableLevelElement`、封路与队列机关已有 Windup/Active/Recovery 等状态 | **80%** | 状态机方向正确，但日志和 HUD 还未统一输出五段契约 |
| 机关角色分类与组合预算 | `RouteBudgetService` 与干预补偿类存在 | **65%** | 有预算机制，但 Level Studio 片段还没有硬性预算校验 |
| Mario 与 Trickster 有效选择 | Bot 与片段能提供快走、绕路、扫描、干预等行为 | **60%** | 需要 AI Arena 用数据证明每局面双方有效选择数 ≥ 2 |
| 判定语义分层 | Gameplay Boxes 可视化已落地 | **75%** | 可视化有了，但 Interaction Contract 八问日志未完整落地 |
| 同帧冲突仲裁 | 代码中有局部优先级与门控逻辑 | **45%** | 缺统一仲裁表和争议交互日志 |
| 四段白盒验证法 | 四个片段已存在 | **75%** | 第 4 房实战房说明与实际组件不一致，且服务未一键补齐 |
| 美术替换玩法盒固定 | AssetImportPipeline 有 Root/Visual 和 Pivot 处理 | **65%** | 需要把“盒体差异=0”做成导入验收自动报告 |
| 调试、度量与 QA | Gameplay Boxes、AI Arena、测试报告均存在 | **65%** | 缺标准 InteractionLog 和 AI 自动回归闭环 |
| AI 测试反馈升级良性循环 | 自动测试与报告复制区存在 | **60%** | 还停留在人把报告交给 AI 的半自动阶段 |

## 8. AI 测试闭环的当前能力与缺口

AI 测试方面，项目已经具备几个关键积木。`TestConsoleWindow.AIArena.cs` 提供自动托管、运行状态、战报输出和长期采样入口；`HeuristicBotInputProvider.cs` 支持基于启发式的 Mario/Trickster 自动输入；`AutoTestAnalytics.cs` 负责采集测试指标；`TestReportRunner.cs` 会把失败测试整理成“快速复制区”，提示把内容发送给 AI 修复。[9] [10] [11]

但是，这些积木还没有完全达到上传方案所说的“测试反馈升级良性循环”。真正的闭环应该包含五步：生成关卡、运行 AI 对局、输出结构化指标、AI 根据指标修改关卡/机制、自动回归验证并写回设计结论。当前仓库完成了前三步的一部分，第四步和第五步主要依赖人工调度。[9] [10] [11]

| 闭环环节 | 当前状态 | 需要补齐的执行件 |
|---|---|---|
| 自动生成验证关卡 | Level Studio 与 TestSceneBuilder 均可生成 | 统一实战房生成后处理，保证 Loot/Escape 与服务完整 |
| 自动运行 AI 对局 | AI Arena 与 Bot 输入已存在 | 将 `S2_Validation_4_Combat` 作为固定测试场景加入自动队列 |
| 指标采集 | AutoTestAnalytics 与战报存在 | 输出稳定 JSON/Markdown Schema，包括有效选择数、通关率、争议交互、热度峰值 |
| AI 修复 | TestReportRunner 提供复制给 AI 的文本 | 增加 `AI_FIX_INPUT.md` 与 `AI_REGRESSION_RESULT.md` 固定工件 |
| 回归与文档回写 | 需要人工 | 自动将通过/失败结论写入关卡片段注释和 docs 验证记录 |

## 9. 完整对比落地执行方案

要让当前项目真正完整按照上传方案落地，建议不推翻现有代码，而是做一次小范围但高杠杆的“契约统一”。第一步应修 `S2_Validation_4_Combat` 的生成语义，让它在 Level Studio 里一键生成后就能通过机制验证。第二步应把 TestSceneBuilder 中已经写好的 Gameplay Loop Bootstrap 能力抽成公共服务，供 Level Studio、TestSceneBuilder 和 AI Arena 共用。第三步应补 `InteractionLog` 与 AI 测试结果 Schema，把“能玩”升级为“能解释、能比较、能自动改进”。

| 优先级 | 执行动作 | 具体改法 | 验收标准 |
|---|---|---|---|
| **P0** | 修复 `S2_Validation_4_Combat` 的 `o/G` 语义漂移 | 增加专用后处理：当片段名为 Combat 时，将 `Collectible` 替换/升级为 `LootObjective`，将 `GoalZone` 替换/升级为 `EscapeGate`；或新增专用 ASCII 字符如 `$`/`X` 表达 Loot/Escape | 重新生成实战房后，机制验证不再提示 `LootObjective` 与 `EscapeGate` 缺失 |
| **P0** | 增加 `GameplayLoopSceneBootstrapper` | 抽取 `AlarmCrisisDirector`、`TricksterHeatMeter`、`MarioSuspicionTracker`、`PropComboTracker`、`RouteBudgetService`、`CounterRevealReward`、HUD 等服务创建与接线逻辑 | Level Studio 生成任意四段白盒片段后可一键补齐 Gameplay Loop 服务 |
| **P0** | 机制验证面板增加“一键修复缺项” | 验证器检测缺服务时提供 Auto-Fix 按钮，自动创建服务并重新验证 | 截图中的缺项不再需要人工猜测如何补 |
| **P1** | 标准化片段语义注释 | 为 Snippet 增加 MainRoute、ShadowRoute、TrapRoles、Budget、TestGoal 元数据字段 | AI Arena 可读取元数据判断测试目标 |
| **P1** | 增加 `InteractionLog` | 输出 Phase、Shape、Priority、Gate、Visual、Source、Target、Outcome 等字段 | 争议交互可日志解释率接近方案要求的 95% |
| **P1** | AI Arena 输出结构化报告 | 输出 `auto_test_metrics.json` 与 `auto_test_summary.md` | AI 可直接消费测试结果而非依赖复制粘贴 |
| **P2** | 美术导入盒体冻结验收 | 在 AssetImportPipeline 生成 GameplayBoxDiff 报告 | 换皮后 Physics/Hurt/Hit/Scan 盒体差异为 0 |
| **P2** | 将报告写回文档 | 生成 `docs/validation/S2_Validation_4_Combat_YYYY-MM-DD.md` | 每轮测试有可追溯历史 |

## 10. 推荐的代码结构调整

建议新增一个公共引导器，而不是让 `PlayableEnvironmentBuilder` 继续无限膨胀。`PlayableEnvironmentBuilder` 可以继续负责基础可玩环境，新的 `GameplayLoopSceneBootstrapper` 专门负责玩法循环服务对象。这样可以避免“生成一个普通平台关也强行塞满所有 Gameplay Loop 服务”的复杂度溢出，同时让实战房、完整验证场和 AI Arena 在需要时调用同一套补齐逻辑。

| 新增/修改文件 | 责任 | 说明 |
|---|---|---|
| `Assets/Scripts/Editor/GameplayLoopSceneBootstrapper.cs` | 创建并接线 Gameplay Loop 服务 | 供 Level Studio、TestSceneBuilder、AI Arena 共用 |
| `Assets/Scripts/Editor/LevelSnippetPostProcessor.cs` | 片段级后处理 | 识别 `S2_Validation_4_Combat` 并升级 `o/G` 语义 |
| `Assets/Scripts/Gameplay/InteractionLog.cs` | 标准交互日志 | 用统一 Schema 记录争议交互 |
| `Assets/Scripts/Gameplay/InteractionLogSink.cs` | 日志输出 | 支持 Console、Markdown、JSON 输出 |
| `Assets/Scripts/Editor/AIArenaReportExporter.cs` | AI 测试结构化报告 | 输出供 AI 修复使用的稳定工件 |
| `docs/validation/` | 验证历史 | 保存每次四段白盒和 AI Arena 结果 |

## 11. 对当前截图的最终判定

对截图中这次 `S2_Validation_4_Combat` 运行结果的判定是：**关卡片段本身生成成功，视觉上也已经体现实战房布局；但机制落地验证没有完整通过，原因是 Level Studio 生成链路尚未把该片段升级为真正的 Gameplay Loop 实战房。** 因此，不能把截图理解为“方案已经完整落地”，也不能理解为“项目机制完全没做”。正确理解是：项目已经有机制实现和专用验证场，但 Level Studio 的第 4 白盒片段还没有与这些机制实现完全打通。

| 截图现象 | 本报告解释 | 是否阻塞继续推进 |
|---|---|---|
| 实战房 ASCII 布局可见 | 片段库和 ASCII 生成链路工作正常 | 不阻塞 |
| 弹窗显示基础通过项 | 基础对象与部分机关已生成 | 不阻塞 |
| 弹窗提示 Loot/Escape 缺失 | `o/G` 字典仍是 `Collectible/GoalZone` | 阻塞完整实战房验收 |
| 弹窗提示热度/Combo/扫描服务缺失 | Level Studio 未调用完整 Gameplay Loop 服务补齐 | 阻塞完整机制联调 |
| AI 测试面板存在 | AI 测试工具已接入编辑器 | 不阻塞 |
| AI 测试未直接闭环修复 | 当前仍需人工将报告交给 AI | 阻塞自动化良性循环升级 |

## 12. 建议的下一轮实施顺序

下一轮最应该做的不是继续增加新机关或新美术，而是先把 `S2_Validation_4_Combat` 做成“方案样板房”。这个样板房应该能够被 Level Studio 一键生成，被机制验证器一键检查，被 AI Arena 一键跑若干局，并把结果写成固定报告。只要这个样板房跑通，后续所有关卡、机关与美术替换都能沿用同一套契约。

| 顺序 | 时间预估 | 任务 | 交付物 |
|---|---:|---|---|
| 1 | 0.5–1 天 | 修复 `o/G` 与 Loot/Escape 的生成契约 | `S2_Validation_4_Combat` 机制验证基础通过 |
| 2 | 1–2 天 | 抽出 Gameplay Loop 服务补齐器 | Level Studio 与 TestSceneBuilder 共用 Bootstrapper |
| 3 | 1–2 天 | 给机制验证器加 Auto-Fix 与重验 | 缺项自动创建、验证结果稳定 |
| 4 | 2–3 天 | 增加 InteractionLog 标准日志 | 争议交互可解释报告 |
| 5 | 2–3 天 | AI Arena 输出结构化结果并绑定样板房 | `auto_test_metrics.json` 与 `auto_test_summary.md` |
| 6 | 1–2 天 | 文档回写与验收模板 | `docs/validation/S2_Validation_4_Combat.md` |

## 13. 最终建议

本项目目前最有效的推进方式是**把“已有机制类”收束成“可一键验证的样板房闭环”**。不要重写已存在的 Gameplay Loop，也不要把问题归结为美术或单个截图误报。真正需要修的是生成契约：`S2_Validation_4_Combat` 必须从“看起来像实战房”升级为“生成后对象、服务、日志、AI 测试都能证明它是实战房”。完成这个升级后，上传方案中的短局面棋盘、四段白盒验证、精准交互、AI 测试反馈升级和美术换皮不改玩法盒，才会从分散功能变成一个可复用的生产闭环。

## References

[1]: file:///home/ubuntu/upload/pasted_file_E6J8le_MarioTrickster双人对抗平台关卡与精准交互改进方案V1(1).MD "用户上传方案：MarioTrickster 双人对抗平台关卡与精准交互改进方案"
[2]: ../SESSION_TRACKER.md "MarioTrickster SESSION_TRACKER.md"
[3]: AI_HANDOFF_PROJECT_STATUS_2026-04-12.md "AI_HANDOFF_PROJECT_STATUS_2026-04-12.md"
[4]: ../LevelStudio_DesignGuide.md "LevelStudio_DesignGuide.md"
[5]: ../Assets/Scripts/Editor/LevelSnippetLibrary.cs "LevelSnippetLibrary.cs"
[6]: ../Assets/Scripts/Editor/TestSceneBuilder.cs "TestSceneBuilder.cs"
[7]: ../Assets/Scripts/LevelDesign/AsciiElementRegistry.cs "AsciiElementRegistry.cs"
[8]: ../Assets/Scripts/Editor/PlayableEnvironmentBuilder.cs "PlayableEnvironmentBuilder.cs"
[9]: ../Assets/Scripts/Editor/TestConsoleWindow.AIArena.cs "TestConsoleWindow.AIArena.cs"
[10]: ../Assets/Scripts/Core/HeuristicBotInputProvider.cs "HeuristicBotInputProvider.cs"
[11]: ../Assets/Scripts/Editor/TestReportRunner.cs "TestReportRunner.cs"
[12]: ../Assets/Scripts/Gameplay/ "Gameplay mechanism classes"
[13]: ../Assets/Scripts/LevelDesign/AsciiLevelGenerator.cs "AsciiLevelGenerator.cs"
[14]: ../Assets/Scripts/LevelElements/Traps/ControllableBlocker.cs "ControllableBlocker.cs"
[15]: ../Assets/Scripts/LevelElements/Traps/StateQueueTrap.cs "StateQueueTrap.cs"
