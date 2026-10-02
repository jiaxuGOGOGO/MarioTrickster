# 分册：马里奥 AI

## 架构（改之前先懂）
```
MarioEyes（唯一允许"看"捣蛋者的类，只看外观）──► MarioPercept（这一帧他知道的全部）
      ▲ 声音入口 NoteNoise/NoteNoiseNear/NoteTaunt(Vector2)          │
      │ 机关动了 NotePropActivated                                    ▼
MarioMindDriver（接线、速度、楼层寻路）◄── MarioOrder ◄── RushMarioMind（纯逻辑状态机）
      │                                                              │ SuspicionMeter（H2：? 后才能 !）
      ▼ HeuristicBotInputProvider（移动执行：ExplorationTarget / HoldStill / AuthoredRouteTarget）
```
状态：Running（拿宝/回家）→ Curious `?` → Investigating `!`（走过去扫描）→ Chasing `!!`（看见本体）→ Searching `?!` → Running。

## 规则
- **新感知 = 新通道**：在 `MarioPercept` 加字段 → 在 `MarioEyes` 填（看的用 `MarioVision.CanSee`，听的只收 `Vector2`）→ 在 `RushMarioMind.Tick` 用。**绝不**在 Mind/Driver 里 FindObjectOfType 捣蛋者状态。
- **新事件源**接到 Driver.Start 订阅、OnDestroy 退订（参考 `TauntAbility.Taunted += eyes.NoteTaunt`）。
- 抓人由裁判 `TricksterLives.TryCatch` 用**真实位置**判定（抓诱饵抓不到人）。
- **学习层**（S200）：`RushMarioMind.RememberHurt / NearHurtSpot / Cautious`，只记他自己被坑的位置，只减速（`cautiousSpeedScale`），每回合清空。继续做学习时：同样只能用他自己的经历，且不能让 H10 下降（不许停住、不许绕进死路）。
- 速度：`hybrid.Bot.MarioSpeedScale = 基础 × 回合浮动 × 地形 × 道具 × 小心`——新倍率乘在这一行。
- 被晕：`Mind.ExtendStun` / `HoldStill`；晕要有上限 `maxStunSeconds`（H9）。
- 楼层寻路：`LevelPathPlanner` + `MarioMindDriver.PlanWaypoint`；往上的路点必须 `AuthoredRouteTarget`（修过"来回跳"）。
- 卡住判定 = 一段时间内**没有朝目标前进**（`Step1StuckRescue.SecondsUntilStuck`，`stuckProgressCells`），不是"没动"。
- 头顶意图文字：`order.intent` → `Step1Text.HeadIntent`（新 intent 在那里加一行中英文）。

## S202 新增感知
`dangerPos/dangerRadius`（看见冒烟炸弹/点着油桶 → `DodgeTarget` 退到圈外）、`seesPickup/pickupPos`（→ `WorthPickup` 顺路捡，3 秒够不着放弃）。新的"临时绕行"行为都要有**放弃计时**并在 `Step1StuckRescue` 豁免，否则会被误判卡住。
验证 AI 用两种自动检查：Hands-off（无人捣乱，H10）+ Trap Probe（机关按最佳时机全触发，看会不会坑死/卡住）。

## S203 性格层
`MarioPersonality.For(kind)` 返回特质（速度、绕被坑点、捡道具距离/跨层、起疑倍率）。新性格 = 在 enum + For() 加一项 + Tuning 权重；**行为差异必须通过特质实现**，不要在各处写 `if (Personality == X)`。自动检查按 `CycledPersonality` 轮流测每种性格，任何一种过不了 H10 都不许交付。绕路走 `DetourPlanner`（纯逻辑、沙盒可测），绕不开 → 跳（`HeuristicBot.JumpRequest`）→ 原路。

## 必测
- 新行为的纯逻辑测试（用 `new RushMarioMind(Tuning())` + 构造 `MarioPercept` 逐帧 Tick）。
- `MindAndDriverNeverReadTricksterTruth` 必须仍过（禁用词见 SKILL.md H4）。
- H2：任何来源都先进 `?`。

## S213 小镇 AI 改动先跑玩家模拟
- 改 OverworldMind / OverworldTown 后看 verify 里 S213 那行：会躲的玩家要全埋伏、站着不躲不能全胜、反应慢的 3 天被抓 ≤3、乱按 100 天全部结束。

## S223 中招反应（纯画面）
- `MarioReaction`（纯逻辑：表 / Sample 三段 / Parse·ToJson）+ `MarioReactionView`（挂马里奥，只动 `MarioController.visualTransform`，由 `Step1Combo.ComboRegistered(n, kind)` 驱动）。
- 新的"坑到了"事件 kind → 必须在 `MarioReaction.Default` 和 `Assets/Resources/MarioReactions.json` 各加一行（sim 对照两者），并在 sim S223 的 stun 字典里写它本来晕几秒。
- 禁止在 MarioReactionView 里出现 ApplyKnockbackStun / ExtendStun / velocity / MarioSpeedScale / TricksterController / Rigidbody。

## S224 可读性（纯画面，不改感知）
- 视锥灌注：房间 `MarioVisionConeView.DrawFill`、小镇 `OverworldGame.UpdateCone` 的 fillMesh；长度 `Step1Readability.FillReach(Meter.Normalized, 被墙挡住的距离, 视野)`。
- 声音圈：房间 `Step1SoundRings` 订阅 Taunted/Exploded/Smashed/Clanged/Clicked（和 MarioMindDriver 喂给耳朵的同一组）；小镇 `OverworldTown.sounds`（Noise() 与挑衅时加）。新增会被听见的声音 = 两处都加，并且半径走 `Step1Readability`。
- 差点被发现：`NearMissLog`（小镇 `OverworldSession.NearMiss`，在 Tick 末尾 Feed、Caught() 里记被抓）。

## S225 起疑原因台词
- `SuspicionCause` 只能由 `RushMarioMind.StrongestCause(...)` 从 MarioPercept 的布尔量得出（H4）；新感知来源 = 枚举加一项 + `Step1Text.CauseIntent` 加一句 + **sim/FakeUnity.cs 的同名枚举同步**。
- 台词分层：同一原因本局第 1–2 次具体句，第 3 次起短句（Blacklist）。只换字，不改行为。
