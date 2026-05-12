# MarioTrickster 商业素材状态适配审计与低风险扩展方案（2026-05-13）

作者：**Manus AI**

## 审计结论

本轮审计确认，项目当前已经具备较稳定的商业素材接入底座：`ArtAssetClassifier` 负责后台识别素材角色与状态，`ImportedAssetMarker` 负责保存向后兼容的字符串元数据，`SpriteFrameAnimator` 承接普通循环帧动画，`SpriteStateAnimator` 承接主角的 `idle/run/jump/fall` 轻量运动状态动画，`DisguiseSystem` 和 `ControllablePropBase` 分别承接潜行融入环境与可控道具交互状态。现有功能不应被替换为复杂 Animator Controller 或大规模运行时重构，因为这会破坏当前 Root/Visual 视碰分离与关卡编辑器工作流。

本轮低风险优化的边界是：**扩展商业素材状态识别和元数据记录，不强行承诺所有新状态都已经有运行时行为**。主角当前可自动播放的运动状态仍以 `idle/run/jump/fall` 为核心；而攻击、受伤、死亡、释放技能、潜行、融入环境、道具预警/激活/冷却、敌人巡逻/追击等商业素材状态，先进入统一语义识别与 Marker 记录，供后续挂接具体系统使用。这样以后新素材包即使包含更多状态，也不会因为多出 `attack/hurt/death/cast/stealth/blend` 等命名而误判、崩溃或覆盖现有逻辑。

| 审计对象 | 当前事实 | 本轮安全处理 |
|---|---|---|
| 主角动画 | `SpriteStateAnimator` 只消费 `MarioController` 暴露的落地、速度、纵向速度、朝向信号 | 保持 `idle/run/jump/fall` 运行时不变，允许单 RUN 和完整四状态继续工作 |
| 未来角色/敌人状态 | 分类器原本主要识别核心运动状态 | 扩展为通用商业状态语义表，把攻击、受伤、死亡、技能、潜行、伪装等写入元数据 |
| 道具交互 | `ControllablePropBase` 已有 `Idle/Telegraph/Active/Cooldown/Exhausted` 交互状态机 | 识别 `telegraph/active/cooldown/exhausted` 等素材命名，作为后续道具皮肤与特效对接依据 |
| 潜行融入环境 | `DisguiseSystem` 用 `IsDisguised/IsFullyBlended` 和排序层变化表达潜行融入 | 识别 `stealth/disguise/blend/camouflage` 等命名，不改变运行时潜行逻辑 |
| 技能和特效 | SEF 与 VFX 路径已存在，攻击/释放/命中多为视觉层扩展点 | 识别 `cast/skill/spell/slash/impact/explosion` 等状态，VFX 继续走 OneShot/Loop 兼容路径 |
| 文档方向 | 统一方案文档已提出四层分类模型，但代码只落地一部分 | 同步导入指南与审计文档，明确“识别优先、运行时逐步挂接”的边界 |

## 执行策略

本轮不会改动 `MarioController`、`DisguiseSystem`、`TricksterAbilitySystem`、`ControllablePropBase` 等核心运行时控制器，以避免引入移动、碰撞、潜行、道具控制方面的回归。优化集中在 Editor 侧分类、元数据、提示和测试层：分类器增加通用状态语义识别；Marker 继续用字符串字段保存状态，不引入新的强类型依赖；Apply Art 面板显示更完整的状态摘要；回归测试覆盖完整四状态、单 RUN 主角、商业攻击/受伤/死亡状态、潜行/伪装状态、道具交互状态与 VFX 一次性状态。

| 状态族 | 典型命名 | 自动处理方式 |
|---|---|---|
| 核心运动状态 | `idle`, `run`, `walk`, `jump`, `fall` | 主角可进入 `SpriteStateAnimator`；缺失状态使用静态帧兜底 |
| 战斗/受击状态 | `attack`, `cast`, `hurt`, `hit`, `death`, `die` | 记录到 Marker 的通用状态摘要；不自动改变主角控制器 |
| 潜行/伪装状态 | `stealth`, `sneak`, `disguise`, `blend`, `camouflage` | 记录为未来 `DisguiseSystem` 视觉对接依据；不替换现有潜行逻辑 |
| 道具交互状态 | `telegraph`, `active`, `cooldown`, `exhausted`, `trigger` | 记录为可控道具/机关皮肤状态；不自动创建新玩法脚本 |
| 特效状态 | `vfx`, `fx`, `impact`, `explosion`, `poof`, `smoke` | 保持 VFX/OneShot 或 Loop 现有路径，增强命名识别 |

## 风险边界

为了保证“其他不变”，本轮所有新增状态都遵循保守原则：**识别多状态不等于自动启用新玩法**。只有 `idle/run/jump/fall` 会被映射到当前可运行的 `SpriteStateAnimator.MotionState`；其他状态保存为字符串语义，用于面板提示、Prefab 元数据、后续系统挂接和文档一致性。若未来要让 `attack/hurt/death/cast/stealth/blend` 真正参与运行时播放，需要在对应控制器中暴露稳定信号，再由新的状态播放器或现有系统显式消费。

## References

[1]: https://docs.unity3d.com/Manual/class-AnimatorController.html "Unity Manual: Animator Controller"
[2]: https://docs.unity3d.com/Manual/SpriteEditor.html "Unity Manual: Sprite Editor"
