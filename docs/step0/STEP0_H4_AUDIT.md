# 第 0 步：H4「不偷看」审计报告 + AI 任务简报

分支：`step0-h4-perception-audit`（本地，基于 `fd9f505`，**未推送**）
依据：`DESIGN_CONSTITUTION_v1.0.md` H4 / H5 / 开发顺序第 0 步

---

## 一、审计结论

马里奥的**决策代码本身是干净的**：
- `HeuristicBotInputProvider.UpdateMarioBrain`
- `HasActionableScanCue`
- `GuidedBot.UpdateMarioBrain` 及其辅助方法
- `SilentMarkSensor.Update`

以上都没有直接读取附身状态、捣蛋者坐标或伪装状态。
**问题出在它们读取的"可疑度数据"本身被污染了**：写入方在马里奥看不见时也往真实锚点上加分。

### 泄露清单

| # | 位置 | 问题 | 严重度 | 处理 |
|---|---|---|---|---|
| L1 | `MarioSuspicionTracker.HandlePossessionStateChanged` | 捣蛋者一附身，就给**真实锚点**加 10 可疑度，不管马里奥在哪。数据最终流入 `GetRevealReadyAnchors` 和扫描决策。 | **高（违反 H4）** | 已修：必须目击才加 |
| L2 | `MarioSuspicionTracker.HandlePropActivated` | 出手时给**具体锚点**加 25（复用 ×1.8）可疑度 + 1 层证据，即使隔着墙。 | **高** | 已修：必须目击才加可疑度/证据；残留照常生成（残留是世界里的可见痕迹，走近才能发现） |
| L3 | `InterferenceCompensationPolicy.HandlePropActivated` | 出手额外加可疑度，没有目击判定。 | 中 | 已修 |
| L4 | `RepeatInterferenceStack` | 重复出手额外加可疑度，没有目击判定。 | 中 | 已修 |
| L5 | `HeuristicBotInputProvider.HasNearbyAnchor` → `CanBePossessed()` | 能读到锚点的冷却和剩余次数（对马里奥不可见）。 | 低 | 未改，建议第 1 步换成"锚点在视线内" |
| L6 | `InterferenceCompensationPolicy.HandleRouteDegraded` | 路线被降级时给来源锚点加残留和证据。 | 低（封路本身可见） | 未改，待定 |
| L7 | `AlarmCrisisDirector` 扫描波 | 读 `CurrentAnchor` 判定命中。 | 合规 | 属于 H5 扫描真值，保留 |
| L8 | `StateQueueTrap` 强制跳过惩罚 | 捣蛋者主动的高代价操作。 | 合规 | 保留（可视为"动静"） |

### 修复方式（最小改动）
- `MarioSuspicionTracker` 新增：
  - `requireMarioWitness`（默认开）、`witnessRange = 8`。
  - 纯函数 `CanWitness(viewer, target, range, targetRoot)`：检查距离和实体遮挡，忽略触发器、马里奥、捣蛋者和目标自身。
  - 公共方法 `IsWitnessedByMario(anchor)`，供其他写入方调用。
- 附身、出手、补偿、重复惩罚这 4 处写入，都先经过目击判定。
- 旧行为可用 `requireMarioWitness = false` 一键恢复，方便对照。

### 已知取舍
- 目击判定暂未做**视野扇形**（只判距离和遮挡）。宪法要求"扇形+距离+遮挡"，扇形留到第 1 步，和性格参数一起做。
- 马里奥变"更瞎"后，**H10（无干预通关率 ≥95%）不受影响**，但扫描命中率会下降。这是预期效果，第 1 步再调。

---

## 二、测试：`Assets/Tests/EditMode/H4PerceptionHonestyTests.cs`

| 测试 | 守什么 |
|---|---|
| `HeuristicMarioBrainNeverReadsPossessionTruth` | 启发式大脑和扫描判断不出现禁读 API |
| `GuidedMarioBrainNeverReadsPossessionTruth` | 实验用 GuidedBot 的马里奥部分同上 |
| `PassiveSensorOnlyReadsWorldEvidence` | 被动感知只读世界证据 |
| `TrackerAddsSuspicionOnlyAfterWitnessCheck` | 加分必须在目击判定之后，且门禁默认开启 |
| `OtherSuspicionWritersAskTrackerForWitness` | 其他写入方也必须先问目击 |
| `WitnessRequiresRangeAndClearLine` | 超距、墙后、NaN 看不见；触发器不挡视线 |

禁读词：`TricksterPossessionGate`、`CurrentAnchor`、`IsHiddenAndArmed`、`IsFullyBlended`、`DisguiseSystem`、`opponentGate`、`opponentDisguise`、`_gate`、`_trickster.transform`、`tricksterPos`、`TricksterPossessionState`

**已在沙箱验证：**
- 用 Python 模拟全部字符串断言：通过。
- 变异测试：把 L1 的修复删掉后，测试能抓到。
- 仓库 `syntax_check.py`：0 错误。

**未验证：** Unity 编译和 EditMode 实跑（沙箱没有 Unity）。请在 Unity → Test Runner → EditMode 里跑 `H4PerceptionHonestyTests`，以及已有的 `ExplorationIntegrationContractTests`。

---

## 三、第 0 步剩余：关掉多余系统（尚未动手）

宪法规定只保留 **跑 + 扫描 + 附身 + 触发**。`GameplayLoopSceneBootstrapper.EnsureGameplayLoopServices` 当前会装 13 个服务：

| 系统 | 建议 |
|---|---|
| MarioSuspicionTracker、SilentMarkSensor、ResidueVisualHint | **保留**（残留是"第一个加回"的系统，追踪器是扫描的数据源） |
| SuspicionHUD | 保留（H3：起疑必须可见） |
| LootEscapeHUD | 保留（胜负条件） |
| TricksterHeatMeter、HeatBreachHint、HeatSuspicionBridge、AlarmCrisisDirector | **关** |
| RouteBudgetService、InterferenceCompensationPolicy、RepeatInterferenceStack | **关** |
| CounterRevealReward、PropComboTracker | **关**（连击在第 3 步以后再加回） |

做法建议：**不删代码**，给引导器加一个 `CoreLoopOnly` 开关（默认开），跳过"关"列的组件。再加一条测试：核心模式下场景里不存在这些组件。

---

## 四、给下一个 AI 会话的任务简报（可直接粘贴）

> 你在 MarioTrickster（Unity 2022.3）分支 `step0-h4-perception-audit` 上工作。最高准则是 `DESIGN_CONSTITUTION_v1.0.md`，冲突时以它为准。
> **目标**：完成开发顺序第 0 步——只保留 跑+扫描+附身+触发（+残留），其余系统关闭但不删除。
> **任务**：
> 1. 在 `GameplayLoopSceneBootstrapper` 与 `TestSceneBuilder` 中加入 `CoreLoopOnly`（默认 true），跳过：TricksterHeatMeter、HeatBreachHint、HeatSuspicionBridge、AlarmCrisisDirector、RouteBudgetService、InterferenceCompensationPolicy、RepeatInterferenceStack、CounterRevealReward、PropComboTracker。
> 2. 所有引用这些组件的代码必须能容忍它们不存在（已有 `FindObjectOfType` + null 检查，逐个确认）。
> 3. 新增 EditMode 测试：CoreLoopOnly 下这些组件不存在；关闭时恢复原样。
> 4. 不得修改 `H4PerceptionHonestyTests` 的断言；任何马里奥感知改动必须让它继续通过。
> **禁止**：改物理手感参数；马里奥侧读取附身真值；新增系统。
> **完成标准**：Unity 编译 0 错误；EditMode 全绿；用户实玩一局确认"跑、扫描、附身、触发"都正常。
> **汇报**：改了哪些文件、每个关掉的系统是否存在空引用风险、没能验证的项目。
