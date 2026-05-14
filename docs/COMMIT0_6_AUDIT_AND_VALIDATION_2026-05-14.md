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
| Commit 6 | 高热度预告式扫描波危机 | 扫描预告与证据放大成立，但原实现存在“扫描命中只加证据、未真实揭穿”的压力闭环风险。 | `AlarmCrisisDirector` 命中附身/潜伏锚点时直接调用门禁强制揭穿。 |

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

## 四、本次修改文件

| 文件 | 变更摘要 |
|---|---|
| `Assets/Scripts/Enemy/TricksterPossessionGate.cs` | 增加 `ForceReveal(float bonusDuration, string source)` 公共接口。 |
| `Assets/Scripts/Gameplay/MarioCounterplayProbe.cs` | Counter-Reveal 成功时调用门禁强制揭穿，并保留原奖励/HUD 事件。 |
| `Assets/Scripts/Gameplay/AlarmCrisisDirector.cs` | 扫描波命中正在附身/潜伏的锚点时真实揭穿。 |
| `Assets/Scripts/Gameplay/SilentMarkSensor.cs` | 被动标记冷却接入热度桥接倍率。 |
| `Assets/Scripts/Editor/TestSceneBuilder.cs` | 新增 Commit0-6 可选验证关卡构建器与灰盒对象生成 helper。 |
| `Assets/Scripts/Editor/TestConsoleWindow.cs` | 在 Level Builder 区域新增一键生成 Commit0-6 验证关卡按钮。 |
| `docs/COMMIT0_6_AUDIT_AND_VALIDATION_2026-05-14.md` | 记录本次审计、修复和验证入口。 |

## 五、验证记录

本地环境未安装 Unity Editor，因此无法在沙盒内执行 Unity Test Runner 或真实编辑器编译。本次已完成以下可执行检查：

| 检查项 | 结果 |
|---|---|
| 改动文件大括号平衡静态检查 | 通过 |
| 关键方法与调用链静态断言 | 通过 |
| `git diff --check` | 通过 |
| Unity 命令行可用性检查 | 沙盒内不可用 |

后续在 Unity 编辑器中建议执行：先通过 `MarioTrickster/Build Commit 0-6 Validation Scene` 生成场景，再运行 `MarioTrickster/Run Tests/Export Full Report (All)` 做完整回归。
