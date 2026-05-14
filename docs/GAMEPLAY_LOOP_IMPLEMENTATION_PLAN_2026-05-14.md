# Gameplay Loop Implementation Plan — 6–9 点落地拆解

作者：Manus AI  
日期：2026-05-14  
适用范围：承接 `docs/ANIMATION_AND_GAMEPLAY_STRATEGY_2026-05-14.md` 第 6–9 点，把“伪装导演循环 + 潜入拿宝撤离 + 预告式危机 + 轻量升级”拆成可按顺序实现的灰盒窄切片。S128 追加核心修正：**Trickster 的移动不是伪装移动，移动是换藏身点；伪装不是走路皮肤，伪装是藏身和出手姿态。**

---

## 1. 总体判断

第 6–9 点不应该一次性做成大系统。当前项目已经有 `TricksterAbilitySystem`、`DisguiseSystem`、`ControllablePropBase`、`GameManager`、`GameUI`、`LevelElementRegistry`、ASCII 关卡生成与视碰分离底座，下一步应当只加**薄层玩法组件**，让现有系统继续工作。

> 最小目标不是“做完整玩法框架”，而是先证明一个 2–5 分钟灰盒切片是否有上瘾节奏：Trickster 暗中移动到藏身点，融入后获得一次操控机会，出手后留下破绽，Mario 通过观察、扫描、踩踏或路线预判反制；如果 Trickster 连续换点操控不同机关，就获得连锁收益，但热度也会升高。

本次修正的重点是解决一个身份逻辑问题：如果 Trickster 与 Mario 在同一张地图里可见移动，并且还能当面变成机关继续移动，观感会显得怪异，Mario 也很容易直接识破。因此后续实现应把 Trickster 拆成两个不同姿态：**Roaming / 换点移动** 与 **Possessing / 附身出手**。前者负责在暗处或后台层移动，后者负责藏进场景物件并发动机关。

| 顺序 | 要解决的问题 | 最小可见结果 | 是否改核心架构 |
|------|--------------|--------------|----------------|
| 第零步 | “当面伪装移动”不合理 | Trickster 只能在 Roaming 时移动，进入附身点后静止融入并出手 | 低侵入，只加状态门禁 |
| 第一步 | 玩家操控机关后没有“连起来”的正反馈 | 屏幕显示 Combo / Chain，连续从不同附身点或机关类型出手有倍率 | 否 |
| 第二步 | 缺少“继续贪还是撤”的压力 | 出现 Heat / Alarm 条，附身和操控越多风险越高 | 否 |
| 第三步 | 胜利目标太单一，只是到终点 | 必须先拿目标物，再从出口撤离 | 否 |
| 第四步 | 关卡还不够像活系统 | 高热度触发预告式扫描、封锁或巡逻危机 | 否 |
| 第五步 | 重玩缺少长期选择 | 通关后给轻量升级或标签强化 | 后置，等循环好玩后再做 |

---

## 2. 第零阶段：先修正 Trickster 的身份模型

第零阶段先不追求新奖励，而是把设定和交互边界立住。Trickster 不是当面换皮的角色，而是一个能钻进场景物件的捣蛋鬼。它可以在地图里移动，但移动姿态应被理解为暗中换点，例如影子、小鬼、墙内通道、管道层、背景层或短暂可见的本体；一旦进入伪装/附身状态，它原则上不能自由移动，只能等待融入、操控附近机关、留下破绽、再撤离。

| 状态 | 规则 | Mario 的反制窗口 | 第一版表现 |
|------|------|------------------|------------|
| `Roaming` | Trickster 可移动、可寻找附身点，但不能直接强操控机关 | 通过路线预判、视线接近或封路逼迫 Trickster | 灰盒小鬼/影子移动 |
| `Blending` | Trickster 正在融入目标物件，不能移动，被打断则失败 | Mario 可扫描、踩踏、攻击可疑物体 | 物体轻微抖动或半透明倒计时 |
| `Possessing` | Trickster 已附身，可获得一次或有限次数操控机会，但仍不能移动 | Mario 可观察出手点、扫描破绽、逼出 Trickster | 物体闪光、眼睛一闪或轮廓发亮 |
| `Revealed` | Trickster 被发现，短时间失去伪装和操控能力 | Mario 获得追击或抢时间窗口 | Trickster 本体弹出/眩晕 |
| `Escaping` | Trickster 从物体脱离，重新进入换点移动 | Mario 可预判下一个藏身点 | 残影或烟雾后退回 Roaming |

| 新增/修改 | 建议位置 | 说明 |
|-----------|----------|------|
| 新增 `PossessionAnchor.cs` | `Assets/Scripts/Gameplay/` 或 `Assets/Scripts/Ability/` | 标记箱子、机关口、砖块、管道、雕像等可附身点，提供 `CanBlend`、`BlendTime`、`LinkedProps`、`Expose()`。 |
| 新增或扩展 Trickster 状态枚举 | `TricksterAbilitySystem.cs` 附近 | 第一版不必重写控制器，只需用 `Roaming / Blending / Possessing / Revealed / Escaping` 控制能否移动、能否操控。 |
| 修改操控入口 | `TricksterAbilitySystem.TryControlProp()` 附近 | 操控机关前先检查当前是否处于 `Possessing`，并检查目标是否来自当前附身点允许的 `LinkedProps` 或范围。 |
| 修改移动门禁 | Trickster 控制脚本或能力系统 | `Blending` / `Possessing` 时禁用自由移动；`Roaming` / `Escaping` 时允许移动但不能强操控。 |
| 增加灰盒破绽提示 | `GameUI` 或临时 Prefab | 融入时显示轻微提示，出手后留下短暂破绽，让 Mario 能判断而不是纯受害。 |

这一阶段的验证标准不是“好不好玩”，而是“逻辑是否顺”。只要玩家能理解 Trickster 必须先换点、再融入、再出手，Mario 能理解可疑物体可以被查，就通过。

---

## 3. 第一阶段：只做“附身操控连锁”

第一阶段的目标是让现有 `TricksterAbilitySystem.OnPropActivated` 不再只是一次性触发机关，而是能被记录成“连续战术动作”。S128 后的连锁不应理解为 Trickster 一边伪装一边跑图，而应理解为它**从一个藏身点出手后撤离，再换到另一个藏身点继续出手**，或者在同一附身点控制不同类型的关联机关。

| 新增/修改 | 建议位置 | 说明 |
|-----------|----------|------|
| 新增 `PropComboTracker.cs` | `Assets/Scripts/Gameplay/` | 订阅场景中 `TricksterAbilitySystem.OnPropActivated`，记录最近一次操控时间、连续次数、最近道具类型、最近附身点。 |
| 修改 `GameUI.cs` | `Assets/Scripts/UI/` | 增加一行灰盒 HUD：`Chain x2 / x3`、剩余连锁窗口、简短提示。 |
| 不修改 `ControllablePropBase` | 保持原样 | 先不要动 Telegraph→Active→Cooldown 状态机，避免破坏现有机关稳定性。 |
| 可选接入 `LevelElementRegistry` | 后续 | 如果道具继承 `ControllableLevelElement`，可以读取 category / tag；否则先用 `PropName` 做临时分类。 |

这一阶段的最小规则建议很简单：在 2.5 秒内连续操控不同机关，连锁数 +1；从不同附身点出手比重复同一附身点收益更高；重复同一个机关也算连锁但收益更低；连锁断掉后回到 x1。这里的重点是让玩家第一次产生“我还想再换点接一下”的感觉，而不是先追求复杂计分公式。

| 验证标准 | 通过条件 |
|----------|----------|
| 机关操控仍然正常 | 原有按键操控、预警、激活、冷却都不受影响。 |
| 附身门禁有效 | Trickster 没有处于 `Possessing` 时不能直接操控机关。 |
| UI 能看见反馈 | 每次成功附身操控后 HUD 能显示当前 Chain。 |
| 有换点动机 | 从不同附身点或不同机关类型出手时反馈明显强于重复同点。 |
| 没有新输入负担 | 玩家不需要学习新按钮，只是多理解“先附身再出手”。 |

---

## 4. 第二阶段：加入 Heat / Alarm 推运气条

第二阶段让连锁不只是奖励，也带来风险。建议先做一个中性的 `TricksterHeatMeter` 或 `AlarmMeter`，不要一开始绑定复杂 AI。热度的职责是制造“继续贪还是撤”的选择。S128 后，热度还承担另一个作用：解释 Trickster 为什么不能无限当面潜伏。附身越久、操控越频繁、同一地点反复出手越多，物件越容易露馅。

| 新增/修改 | 建议位置 | 说明 |
|-----------|----------|------|
| 新增 `TricksterHeatMeter.cs` | `Assets/Scripts/Gameplay/` | 监听 `PropComboTracker` 或 `TricksterAbilitySystem.OnPropActivated`，每次附身和操控增加热度，连锁越高增加越多。 |
| 修改 `PossessionAnchor.cs` | `Assets/Scripts/Gameplay/` | 出手后给当前附身点添加短暂 `Suspicion` 或 `Residue`，让 Mario 可查。 |
| 修改 `GameUI.cs` | `Assets/Scripts/UI/` | 显示 Heat 条和档位文字，例如 `Calm / Suspicious / Alert / Lockdown`。 |
| 修改 `GameManager.ResetRound()` | `Assets/Scripts/Core/` | 回合重置时调用 Heat 重置，或让 Heat 自己订阅 `OnRoundStart`。 |
| 暂不强行惩罚玩家 | 第一版只给反馈 | 先验证心理压力，再加入实质惩罚。 |

建议第一版热度只做四件事：第一，进入附身点增加少量热度；第二，成功操控机关增加更多热度；第三，热度随时间慢慢下降但下降很慢；第四，到达阈值时屏幕提示“警戒升高”，并让可疑附身点更明显。不要一上来就秒杀或强制失败，否则玩家只会保守，不会贪。

| 热度档位 | 推荐效果 | 目的 |
|----------|----------|------|
| 0–30 Calm | 无惩罚，只显示轻微状态 | 让玩家熟悉循环。 |
| 30–60 Suspicious | 附身物体的抖动、闪光、残影变得更明显 | 给 Mario 可读信息。 |
| 60–85 Alert | 出现扫描预告、出口短暂关闭或机关冷却变长 | 制造撤退压力。 |
| 85–100 Lockdown | 触发一次预告式危机，之后热度回落到 60 | 避免直接失败，保留继续玩的机会。 |

---

## 5. 第三阶段：把终点改成“拿宝撤离”

第三阶段把当前 `GoalZone` 的单点胜利改成两步胜利：先拿目标物，再从出口撤离。建议不要删除 `GoalZone`，而是新增 `LootObjective` 和 `EscapeGate`，让普通终点仍然可用于旧关卡。S128 后，这一阶段的关卡结构要服务双方：Mario 的路线从“进危险区拿宝再撤离”展开；Trickster 的路线从“选择藏身点干扰 Mario 的关键路径”展开。

| 新增/修改 | 建议位置 | 说明 |
|-----------|----------|------|
| 新增 `LootObjective.cs` | `Assets/Scripts/Gameplay/` 或 `Assets/Scripts/LevelElements/` | Mario 触碰后获得“目标物已携带”状态，发出 `OnLootCollected`。 |
| 新增 `EscapeGate.cs` | `Assets/Scripts/Gameplay/` 或 `Assets/Scripts/Core/` | Mario 触碰出口时检查是否已拿目标物；拿到则 `GameManager.EndRound("Mario")`，没拿则提示“还没拿目标”。 |
| 修改 `GameUI.cs` | `Assets/Scripts/UI/` | 显示 `Loot: 0/1`、`Escape Ready`。 |
| 后续修改 ASCII Registry | `Assets/Scripts/LevelDesign/` | 等玩法验证后再给目标物、撤离门、附身点分配稳定字符，不要先污染字典。 |

这一阶段的关键不是“多一个收集物”，而是形成路径结构：Mario 要先进危险区，再带着目标回到出口；Trickster 要提前选择藏身点，干扰 Mario 的进入路线、撤退路线或关键跳跃时机。这样热度条才有意义，因为 Trickster 在 Mario 返程时更想连续出手，而 Mario 也会自然产生“继续赶路、停下来查可疑物、还是绕路”的判断。

| 验证标准 | 通过条件 |
|----------|----------|
| 没拿目标不能通关 | 出口给清晰提示，而不是静默失败。 |
| 拿目标后目标 UI 改变 | 玩家明确知道现在该撤离。 |
| Trickster 有干扰空间 | 出口到目标物之间至少有 2 个可附身点和 2 个可操控机关。 |
| Mario 有反制窗口 | 每个致命机关仍保留 Telegraph 预警，每个出手点都有短暂破绽。 |

---

## 6. 第四阶段：加入预告式危机，但只做一类

第四阶段来自策略文档的方案 D。这里不要做完整导演系统，先做一个最小 `AlarmCrisisDirector`，只在 Heat 达到高档时触发一种危机，例如“扫描波即将经过”“出口短暂封锁”“某个平台即将坍塌”。S128 后，最推荐第一类危机仍然是“扫描波”，因为它能自然检查附身点、暴露 Trickster、给 Mario 读局机会，同时不会破坏地形和关卡结构。

| 新增/修改 | 建议位置 | 说明 |
|-----------|----------|------|
| 新增 `AlarmCrisisDirector.cs` | `Assets/Scripts/Gameplay/` | 订阅 `TricksterHeatMeter.OnHeatTierChanged`，高热度时启动危机。 |
| 新增或复用预警可视对象 | 关卡 Prefab / 灰盒物体 | 先用透明红框、倒计时文本或简单闪烁，不追求美术。 |
| 修改 `PossessionAnchor.cs` | `Assets/Scripts/Gameplay/` | 被扫描命中时，如果 Trickster 正在该点 `Blending` / `Possessing`，则进入 `Revealed`。 |
| 修改 `GameUI.cs` | `Assets/Scripts/UI/` | 显示 `Crisis Incoming` 或倒计时。 |
| 后续接 Shader/SEF | 等机制手感稳定后 | 用 Shader 表现扫描、热浪、锁定等状态，不让 Shader 承担规则逻辑。 |

推荐第一类危机做“扫描波”。它不会破坏地形，不需要生成复杂对象，只需要从左到右或从目标区扫过；如果 Mario/Trickster 处在危险区域，则受到暴露、掉血、热度惩罚或短暂禁用伪装。最小版本甚至可以先只做视觉预告和 UI，不做伤害，用来验证节奏。

---

## 7. 第五阶段：再考虑轻量升级，不要提前做成长系统

升级系统应放在核心循环验证之后。原因很简单：如果“附身换点 + 操控连锁 + 热度 + 拿宝撤离”本身不好玩，升级只会掩盖问题；如果它已经好玩，升级才会变成重玩动力。S128 后，升级也应围绕“换点、融入、破绽、扫描抗性”展开，而不是单纯堆数值。

| 升级方向 | 第一版效果 | 为什么适合项目 |
|----------|------------|----------------|
| 控制距离 +1 | Trickster 能从附身点更远处操控机关 | 直接强化现有 `controlRange`。 |
| 融入更快 | `Blending` 等待更短 | 降低等待挫败。 |
| 换点撤离更快 | `Escaping` 到 `Roaming` 更快 | 强化“打一枪换一个地方”的节奏。 |
| 连锁窗口 +0.5s | 更容易打出 x2/x3 | 直接服务新循环。 |
| 热度上限 +10 | 更能贪 | 直接服务推运气。 |
| 扫描抗性一次 | 高热度下容错 | 服务潜行压力。 |
| 标签专精 | 对 `Trap` / `Platform` / `Hidden` 类道具收益更高 | 未来接 `LevelElementRegistry`。 |

第一版升级可以不做存档，只在一局结束后弹出三选一，应用到下一回合。等玩家确认这些选择真的改变打法，再考虑持久化。

---

## 8. 三个关卡原型的具体制作顺序

三个原型应从最小到复杂推进。每个原型都只验证一个新问题，不要把所有内容塞进第一关。S128 后，原型 1 前面需要加一个更小的“附身点教学切片”，先让玩家理解 Trickster 不能当面伪装移动。

| 原型 | 目标 | 必需组件 | 通过条件 |
|------|------|----------|----------|
| 原型 0：附身点教学 | 教会“移动=换点，伪装=藏身出手” | 3 个 `PossessionAnchor`、1 个可操控机关、灰盒破绽提示 | 玩家能理解 Trickster 必须先进入物件再出手，附身时不能乱跑。 |
| 原型 1：附身操控连锁 | 教会从不同藏身点连续出手 | 现有伪装、现有可操控机关、`PropComboTracker` | 玩家能在 30 秒内理解“换点→融入→操控→换点再接”。 |
| 原型 2：拿宝撤离 | 验证进入危险区再撤退 | `LootObjective`、`EscapeGate`、Heat 条 | 玩家拿到目标后会主动考虑撤退路线，Trickster 会选择卡返程点。 |
| 原型 3：高热度贪心局 | 验证推运气是否上瘾 | Combo、Heat、一个预告式扫描危机 | 玩家会在高热度下犹豫是否再打一次连锁或赶紧撤。 |

原型 0 和原型 1 不需要新美术，直接使用灰盒和现有机关。原型 2 才需要一个明显的目标物图标。原型 3 才考虑 Shader/SEF 做热度、扫描、暴露等视觉强化。

---

## 9. 推荐提交拆分

为了避免一次改太多，建议按以下提交顺序推进。每个提交都应该能单独进入 Unity Play Mode 验证。S128 后，原先的 `PropComboTracker` 应后移到第二个功能提交，因为先有附身门禁，连锁才不会变成“当面变装操控”的怪逻辑。

| Commit | 内容 | 验证方式 |
|--------|------|----------|
| 0 | `PossessionAnchor` + Trickster `Roaming / Blending / Possessing / Revealed / Escaping` 门禁 + 3 个灰盒附身点 | Trickster 可移动找点；进入附身后不能移动；附身后才能操控一次。 |
| 1 | `PropComboTracker` + HUD 显示 + 不同附身点/机关类型倍率 | 从两个不同附身点操控两个机关，HUD 出现 Chain。 |
| 2 | `TricksterHeatMeter` + 附身点破绽提示 + HUD 显示 + 回合重置 | 连续附身操控，Heat 上升；可疑物更明显；下一回合归零。 |
| 3 | `LootObjective` + `EscapeGate` | 没拿目标不能通关，拿到后能撤离胜利。 |
| 4 | 第一版灰盒关卡模板 | 2–5 分钟跑通“换点→附身→操控→拿宝→撤离”。 |
| 5 | `AlarmCrisisDirector` 最小扫描预告 | 高热度触发一次可读扫描，命中附身点会暴露 Trickster。 |
| 6 | 动画/Shader 状态信号桥 | Heat、扫描、融入、控制蓄力能驱动视觉特效。 |

---

## 10. 最终执行口径

下一步最建议先实现 **Commit 0 + Commit 1 + 一个灰盒测试场景**。不要先做升级，不要先做完整危机导演，也不要先重写 `SpriteStateAnimator`。只要“移动换点、附身出手、出手留破绽、Mario 可反查”这条逻辑跑顺，Combo 和 Heat 才会成立；如果这条逻辑不顺，后面所有连锁、推运气和拿宝撤离都会显得像硬贴系统。

最终口径应固定为：**Trickster 不是当面变成机关到处走，而是暗中换藏身点；伪装状态下不能自由移动，只能藏、等、出手、露馅、撤离。** 这能同时保留你最初“暗中搞破坏、拖延得分”的方向，也给 Mario 留出观察、扫描、踩踏、绕路和预判的对抗出口。
