# Commit 0-6 项目落地审计与验证关卡记录（2026-05-14）

本文档记录本次对 S130 Commit 0-6 已落地内容的后台审计结论、风险修复和新增验证关卡入口。该文档面向后续 AI 接手、策划验收和灰盒回归，不改变既有实施方案的核心方向。

## 一、审计结论

Commit 0-6 的总体落地方向与 `docs/GAMEPLAY_LOOP_IMPLEMENTATION_PLAN_2026-05-14.md` 中的玩家体验护栏一致：Trickster 侧从“单次按键干预”升级为带状态风险的暗中附身链路，Mario 侧通过残留、SilentMark、强扫描、路线补偿和扫描波获得反制窗口。整体没有发现需要推翻架构的矛盾设计，但存在三处会影响体验闭环的隐患，已在本次一并修复。

| Commit | 设计目标 | 审计结论 | 本次处理 |
|---|---|---|---|
| Commit 0 | `PossessionAnchor` 与 `Roaming / Blending / Possessing / Revealed / Escaping` 五态门禁 | 主链路成立，能力系统已接入门禁，能阻断非法操控。 | 新增 `TricksterPossessionGate.ForceReveal()`，作为 Mario 反制与危机扫描的统一真实揭穿入口。 |
| Commit 1 | Mario 边跑边积累证据，强扫描可 Counter-Reveal | 证据与事件链成立，但原实现存在“强扫描只发事件、未真实揭穿”的体验风险。 | `MarioCounterplayProbe` 改为调用门禁强制揭穿，再派发奖励/HUD 事件。 |
| Commit 2 | 路线预算、干预补偿、重复干预惩罚 | 系统组件已在测试场景统一挂载，概念与玩家体验护栏一致。 | 新验证关卡加入上下路线与双锚点，便于触发预算、补偿和 HUD 检查。 |
| Commit 3 | 连锁追踪与 Combo HUD | 系统组件与多锚点触发条件成立。 | 新验证关卡加入短间距三锚点，便于连续干预验证 Combo。 |
| Commit 4 | 热度、破绽提示、热度桥接证据衰减与标记速度 | 热度系统成立，但 `SilentMarkSensor` 未消费热度桥接的标记速度倍率。 | `SilentMarkSensor` 现在按 `HeatSuspicionBridge.CurrentMarkSpeedMultiplier` 缩短同锚点被动标记冷却。 |
| Commit 5 | Loot → Escape 胜利链路与高热度延迟出口 | 组件存在，测试覆盖需要更聚焦。 | 新验证关卡加入“先碰出口拒绝、拿 Loot 后通行”的独立段。 |
| Commit 6 | 高热度预告式扫描波危机 | 扫描预告与证据放大成立，但原实现存在“扫描命中只加证据、未真实揭穿”的压力闭环风险；用户 GIF 实测已能看到预警倒计时与扫描线，但命中/揭穿结果不够显眼。 | `AlarmCrisisDirector` 命中附身/潜伏锚点时直接调用门禁强制揭穿；`ScanWaveHUD` 增加命中/揭穿中央横幅，避免测试者只看到扫描线却看不清结果。 |

## 二、玩家体验与策划接受度判断

本次审计的核心判断是：当前项目应继续保持“Trickster 暗中做局、Mario 边跑边读局”的低打断体验，而不是把对抗变成停步读条、频繁菜单确认或复杂参数教学。Commit 0-6 的系统组合在方向上是顺的，但必须确保玩家看到的 HUD、事件和真实状态一致，否则会产生“我明明揭穿了但对方还在控”的信任崩塌。

本次三处修复均围绕这个原则展开：Counter-Reveal 和 Scan Wave 不再只是信息提示，而会真实推动 `TricksterPossessionGate` 进入 `Revealed`；热度升高后，Mario 的被动读局也会实际变快。这样策划在调参时只需要关注半径、阈值、冷却和持续时间，不需要重新解释一套例外规则。

## 三、新增可选验证关卡

新增入口如下，均为可选，不替代原 9-Stage 综合测试场景。

| 入口 | 位置 | 用途 |
|---|---|---|
| `MarioTrickster/Build Commit 0-6 Validation Scene` | Unity 顶部菜单 | 一键生成 Commit0-6 短流程灰盒验证关卡。 |
| `Build Commit0-6 Validation` | `Test Console` 的 Level Builder 区域 | 给策划/测试在工具面板内低摩擦生成同一验证关卡。 |

验证关卡采用从左到右的短流程：`Gate + Evidence`、`Route Budget`、`Combo + Heat`、`Loot → Escape`、`Scan Wave Crisis`。每段都有场景内文字提示，目标是让测试者不需要理解底层系统参数，也能按顺序验证新增功能是否形成完整闭环。

### 首段操控可用性补充修复

用户本地验证时发现第一段蓝色方块按 `L` 无反应，同时 Mario 的 `Q` 扫描日志持续输出 `Trickster not disguised`。静态排查后确认根因不是 `L` 键派发或方块状态机，而是验证关卡生成的 Trickster 没有初始化 `DisguiseSystem.availableDisguises`，导致按 `P` 时 `DisguiseSystem.Disguise()` 直接返回，Trickster 从未进入 `Disguised / FullyBlended` 前置状态，能力系统自然不会激活。现已在测试关卡构建器中为 Trickster 自动注入默认灰盒伪装形态，并将验证场景的融入等待缩短为 `0.35s`、操控半径调整为 `8f`，确保开局附近的第一段锚点可以低摩擦完成 `P → 静止融入 → L` 的完整链路。

### 用户 GIF 实测补充：Combo/Heat/Scan Wave 可见性

用户继续验证第二段 Route Budget 与第三段 Combo/Heat：上下路蓝块均可依次伪装控制，Mario 的 `Q` 扫描可打出 `Trickster Detected`；GIF 中可见 Combo HUD 出现 `Multiplier: 1.00x / 1.15x / 1.50x` 与 `Different anchor` 反馈，说明连续多锚点干预已推动连锁与热度变化。GIF 后段还可见 `SCAN WAVE INCOMING` 倒计时和竖向扫描线，说明 Commit 6 的预警与扫描阶段已被热度链路触发。

本次 GIF 暴露的体验问题不是底层扫描波没有运行，而是扫描线命中/扫过时缺少足够显眼的屏幕结果反馈，测试者很难判断“只是扫过”还是“已经揭穿”。因此已为 `ScanWaveHUD` 订阅 `AlarmCrisisDirector.OnAnchorScanned`，在扫描命中普通锚点时显示 `SCAN HIT: EVIDENCE AMPLIFIED`，在命中当前附身/潜伏锚点并触发真实揭穿时显示 `SCAN HIT: TRICKSTER REVEALED`，把 Commit 6 的命中结果从日志/内部状态提升到屏幕中央反馈。

## 四、本次修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Scripts/Enemy/TricksterPossessionGate.cs` | 增加 `ForceReveal(float bonusDuration, string source)` 公共接口。 |
| `Assets/Scripts/Gameplay/MarioCounterplayProbe.cs` | Counter-Reveal 成功时调用门禁强制揭穿，并保留原奖励/HUD 事件。 |
| `Assets/Scripts/Gameplay/AlarmCrisisDirector.cs` | 扫描波命中正在附身/潜伏的锚点时真实揭穿。 |
| `Assets/Scripts/Gameplay/ScanWaveHUD.cs` | 扫描波命中锚点时显示中央反馈横幅，区分证据放大与 Trickster 真实揭穿。 |
| `Assets/Scripts/Gameplay/SilentMarkSensor.cs` | 被动标记冷却接入热度桥接倍率。 |
| `Assets/Scripts/Editor/TestSceneBuilder.cs` | 新增 Commit0-6 可选验证关卡构建器与灰盒对象生成 helper；补齐 Trickster 默认伪装形态、验证场景融入等待和首段操控半径配置。 |
| `Assets/Scripts/Editor/TestConsoleWindow.cs` | 在 Level Builder 区域新增一键生成 Commit0-6 验证关卡按钮。 |
| `docs/COMMIT0_6_AUDIT_AND_VALIDATION_2026-05-14.md` | 记录本次审计、修复和验证入口。 |

## 五、验证记录

本地环境未安装 Unity Editor，因此无法在沙盒内执行 Unity Test Runner 或真实编辑器编译。本次已完成以下可执行检查：

| 检查项 | 结果 |
|---|---|
| 改动文件大括号平衡静态检查 | 通过 |
| 关键方法与调用链静态断言 | 通过 |
| `git diff --check` | 通过 |
| 首段 `P → 静止融入 → L` 调用链静态复核 | 通过，生成器现在会配置 `availableDisguises`、`blendInTime=0.35f`、`controlRange=8f`。 |
| 用户 GIF 实测：Route Budget / Combo / Heat / Scan Wave | 通过核心触发链：上下路可控、`Q` 可揭穿、Combo 倍率变化、扫描波预警与扫描线可见；已补强扫描命中/揭穿反馈可见性。 |
| Unity 命令行可用性检查 | 沙盒内不可用 |

后续在 Unity 编辑器中建议执行：先通过 `MarioTrickster/Build Commit 0-6 Validation Scene` 生成场景，再运行 `MarioTrickster/Run Tests/Export Full Report (All)` 做完整回归。
