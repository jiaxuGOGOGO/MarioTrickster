# Commit 0→1→2 全方位审计报告

> **审计日期**：2026-05-14  
> **审计范围**：Commit 0（PossessionAnchor + TricksterPossessionGate + TricksterPossessionState）、Commit 1（AnchorSuspicionData + MarioSuspicionTracker + SilentMarkSensor + MarioCounterplayProbe + ResidueVisualHint + SuspicionHUD）、Commit 2（RouteBudgetService + InterferenceCompensationPolicy + RepeatInterferenceStack + CounterRevealReward + RouteBudgetHUD）  
> **审计维度**：红线合规、事件接入正确性、回合重置完整性、性能隐患、关卡编辑器兼容性、未来 Commit 3→6 阻碍评估

---

## 1. 红线合规 ✅ 全部通过

| 红线项 | 检查结果 |
|--------|----------|
| **PhysicsMetrics** | 0→2 所有 14 个脚本中无任何代码引用 `PhysicsMetrics`、`PhysicsConfigSO`，仅在注释中声明"不修改"。 |
| **碰撞体 (Collider2D)** | 无任何 `.size`、`.offset`、`AddComponent<Collider2D>` 调用。 |
| **重力 (gravityScale)** | 无引用。 |
| **MotionState 枚举** | 无引用。 |
| **ControllablePropBase 状态机** | 无任何 `Telegraph`/`Active`/`Cooldown` 方法调用或状态写入。`SilentMarkSensor.markCooldown` 是自身冷却变量，与 Prop 状态机无关。 |

**结论**：红线零触碰，合规。

---

## 2. 事件接入正确性 ✅ 全部通过

| 系统 | 接入事件 | 方向 |
|------|----------|------|
| TricksterPossessionGate | `DisguiseSystem.OnDisguiseChanged`、`TricksterAbilitySystem.OnPropBound/Unbound/Activated` | 只读监听 |
| MarioSuspicionTracker | `TricksterAbilitySystem.OnPropActivated`、`TricksterPossessionGate.OnStateChanged/OnAnchorChanged` | 只读监听 |
| SilentMarkSensor | 无外部事件订阅，自身每帧轮询 Tracker 数据 | 只读 |
| MarioCounterplayProbe | `ScanAbility.OnScanActivated`、`MarioSuspicionTracker.OnRevealReady` | 只读监听 |
| RouteBudgetService | `TricksterAbilitySystem.OnPropActivated` | 只读监听 |
| InterferenceCompensationPolicy | `RouteBudgetService.OnRouteDegraded`、`TricksterAbilitySystem.OnPropActivated` | 只读监听 |
| RepeatInterferenceStack | `TricksterAbilitySystem.OnPropActivated` | 只读监听 |
| CounterRevealReward | `MarioCounterplayProbe.OnCounterReveal` | 只读监听 |

**关键验证**：所有事件源（`OnPropActivated`、`OnScanActivated`、`OnDisguiseChanged`、`OnRoundStart`）均已在对应源文件中确认存在且正确触发。无反向控制（新系统不调用能力系统的 public 方法来修改行为）。

---

## 3. 回合重置完整性 ✅ 全部通过

| 系统 | 订阅 OnRoundStart | 重置内容 |
|------|-------------------|----------|
| MarioSuspicionTracker | ✅ | 所有 AnchorSuspicionData.Reset()（Suspicion/Residue/Mark/Evidence 归零） |
| RouteBudgetService | ✅ | 所有路线恢复 Available，RecoveryTimer 归零 |
| InterferenceCompensationPolicy | ✅ | progressBoostTimer 归零 |
| RepeatInterferenceStack | ✅ | records.Clear()、totalHeat 归零 |
| CounterRevealReward | ✅ | rewardBoostTimer 归零、totalCounterReveals 归零 |
| SilentMarkSensor | ❌ 未订阅 | 冷却 Dictionary 未清空 |
| MarioCounterplayProbe | ❌ 未订阅 | protectedWindowTimer 和 isStrongScanReady 未重置 |

**隐患 A（低风险）**：`SilentMarkSensor.lastMarkTime` 字典在回合重置后不清空。影响：新回合前几秒内，旧回合已标记的锚点仍在冷却中，可能延迟 1-3 秒才能重新标记。实际影响极小（冷却只有 3 秒），但语义上不干净。

**隐患 B（低风险）**：`MarioCounterplayProbe.protectedWindowTimer` 不重置。影响：如果回合在保护窗口内结束并立即重开，新回合前 1-2 秒内 Mario 仍显示 "PROTECTED"。实际影响极小（保护窗口只有 2 秒）。

**建议修复**：在两个组件中各加一行 `OnRoundStart` 订阅和重置逻辑。优先级：低，不阻塞灰盒验证。

---

## 4. 性能隐患

| 项目 | 严重度 | 说明 |
|------|--------|------|
| `ResidueVisualHint.Update()` 中 `GetComponentInChildren<SpriteRenderer>` 每帧对每个有残留的锚点调用 | **中** | 灰盒阶段锚点数 < 10 无感知影响；正式关卡若锚点 > 30 应缓存 SpriteRenderer 引用。 |
| `MarioCounterplayProbe.UpdateStrongScanReadiness()` 每帧遍历 `dataMap` 全部条目 | **低** | 灰盒阶段 dataMap 条目 < 10，O(N) 遍历无压力。 |
| `MarioSuspicionTracker.Update()` 每帧 `foreach (var kvp in dataMap)` | **低** | Dictionary foreach 在 Mono/IL2CPP 上会产生微量 GC（enumerator boxing），但条目极少时可忽略。正式版可改为缓存 List 遍历。 |
| `RouteBudgetService.GetAllRoutes()` 调用 `AsReadOnly()` | **低** | `AsReadOnly()` 每次调用创建一个 `ReadOnlyCollection` 包装对象。但只在 HUD OnGUI 和 CounterReveal 事件中调用（非每帧热路径），可接受。 |

**结论**：灰盒阶段无性能瓶颈。进入正式关卡前建议缓存 `ResidueVisualHint` 中的 SpriteRenderer 引用。

---

## 5. 关卡编辑器兼容性 ✅ 无阻碍

| 检查项 | 结果 |
|--------|------|
| LevelEditorPickingManager | 不引用任何 Commit 0→2 类型，无冲突 |
| LevelElementRegistry | 不引用 PossessionAnchor 或 RouteBudgetService，无冲突 |
| AsciiLevelGenerator / Validator | 不引用新系统，无冲突 |
| Level Studio / Custom Template Editor | 不引用新系统，无冲突 |
| TestSceneBuilder | 正确挂载所有新组件，编辑器菜单可正常生成测试场景 |
| PossessionAnchor 对关卡编辑的影响 | PossessionAnchor 是轻量元数据组件（[DisallowMultipleComponent]），不影响碰撞体、物理或 Visual 节点；关卡编辑器的 Size Sync、Root/Visual 选取、Apply Art 均不受影响 |

**结论**：新系统完全独立于关卡编辑工具链，不会阻塞关卡设计工作流。

---

## 6. 未来 Commit 3→6 兼容性评估 ✅ 无阻碍

| 未来 Commit | 需要的接口 | 当前是否已预留 |
|-------------|-----------|---------------|
| Commit 3（TricksterHeatMeter / Combo 系统） | 读取 `RepeatInterferenceStack.TotalHeat`、`OnHeatChanged` 事件 | ✅ 已暴露 |
| Commit 4（Trickster 收益递减可视化） | 读取 `RepeatInterferenceStack.GetDiminishFactor(key)` | ✅ 已暴露 |
| Commit 5（拿宝撤离 / GoalZone 扩展） | 读取 `RouteBudgetService.HasFallbackRoute()`、`InterferenceCompensationPolicy.CurrentProgressMultiplier` | ✅ 已暴露 |
| Commit 6（状态信号 / 信息可读性） | 读取 `TricksterPossessionGate.CurrentState`、`MarioSuspicionTracker` 事件 | ✅ 已暴露 |

**关键设计决策**：
- `RouteBudgetService` 的 `FindNearestRoute()` 目前用 Y 坐标简单判断上下路。后续关卡如果有更复杂的路线拓扑，需要替换为从 `LevelElementRegistry` 读取路线归属。这是**预期的扩展点**，不是设计缺陷。
- `CounterRevealReward` 的"冻结锚点"目前通过拉满 Suspicion 实现。后续如果需要真正的 `IsFrozen` 标记（阻止 Trickster 绑定），可在 `PossessionAnchor` 上加一个 bool 字段，`CanBePossessed()` 中检查即可。这也是预期扩展点。

---

## 7. 总结

| 维度 | 状态 |
|------|------|
| 红线合规 | ✅ 零触碰 |
| 事件接入 | ✅ 纯监听，无反控 |
| 回合重置 | ⚠️ 两处低风险遗漏（SilentMarkSensor + MarioCounterplayProbe） |
| 性能 | ✅ 灰盒无瓶颈，1 处中期优化建议 |
| 关卡编辑器 | ✅ 完全独立，无阻碍 |
| 未来兼容 | ✅ 接口已预留，扩展点明确 |

**最终判定**：Commit 0→1→2 实现质量合格，无阻塞性隐患。两处低风险回合重置遗漏建议在灰盒验证前顺手修复（各加 3 行代码），但不修也不影响核心玩法验证。可以安全进入灰盒体验验证阶段。
