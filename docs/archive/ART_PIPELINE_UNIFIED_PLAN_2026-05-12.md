# MarioTrickster 商业美术素材统一接入方案（2026-05-12）

作者：**Manus AI**

## 目标与边界

本方案面向 MarioTrickster 当前已经形成的 **Root/Visual 视碰分离**、关卡编辑器 Root 选中习惯、`ImportedAssetMarker` 元数据、`SpriteFrameAnimator` 循环帧播放器与 Sprite Effect Factory（SEF）材质体系。核心目标不是增加一个复杂菜单，而是在后台把商业素材自动归到正确去处，使用户只在“素材属于谁、是否需要覆盖锁定效果”这类真正需要审美判断的位置介入。

Unity 官方动画状态机文档说明，角色或 GameObject 常常具有 idle、walk、fall 等多种动作，状态机会播放当前动作并决定下一个动作；`Animation Parameters` 用于脚本与 Animator Controller 通信，Transitions 用于条件触发动画切换。[1] 这与本项目 `MarioController` 已输出 `IsGrounded`、`Speed`、`VerticalSpeed` 的事实高度吻合。因此，主角和其他带运动语义的角色素材，不能再只走单组循环播放器，而应优先走 **状态动画分组**。

## 统一分类模型

商业素材导入的第一步不是“播放”，而是“判断用途”。新的后台分类模型将素材分为四层：资产角色、动画语义、运行行为、视觉效果策略。`ImportedAssetMarker` 继续作为轻量数据容器，但字段扩展为向后兼容的字符串/枚举式标记，不改变现有对象层级。

| 层级 | 典型取值 | 作用 | 默认自动判断依据 |
|---|---:|---|---|
| 资产角色 | Character、Enemy、Prop、Collectible、Hazard、Platform、Background、SceneAnimation、VFX、UI | 决定组件模板与 Root/Visual 责任边界 | 文件名、文件夹名、当前 `physicsType`、Sprite 数量、目标对象组件 |
| 动画语义 | idle、run、jump、fall、walk、attack、hurt、death、loop、once、none | 决定是否进入状态动画或循环动画 | 文件名 token、Sprite 名 token、帧组数量 |
| 运行行为 | PlayerStateDriven、PhysicsProp、PickupConsume、HazardContact、BackgroundLayer、AmbientLoop | 决定添加/保留哪些运行时组件 | 分类结果与当前对象已有组件 |
| 视觉效果策略 | Preserve、ClearBeforeApply、ResetToDefault、Locked | 决定 SEF 预设切换是否先清状态、是否允许覆盖锁定效果 | Quick Apply 设置与 marker 锁定字段 |

Unity Learn 对 Sprite Animation 的教程说明，Sprite Sheet 中的 Sprites 通常按网格排列并编译为 Animation Clip，像翻页书一样依次播放；如果帧排列不均匀会出现播放晃动。[2] 因此，单组 `SpriteFrameAnimator` 继续适用于 **同一动作连续帧**，但当素材名或 Sprite 名出现 `idle/run/jump/fall` 这类状态语义时，系统必须先分组，再由状态播放器选择当前组。

## 角色状态动画路径

新增 `SpriteStateAnimator`，运行时直接读取角色运动状态并选择帧组。它不替代 Unity Animator，而是作为本项目商业素材快速接入的轻量状态播放器，避免临时自动生成 Animator Controller 造成不可控复杂度。播放器优先从 `MarioController` 的公开属性读取 `Speed`、`VerticalSpeed`、`IsGrounded`，也可回退读取 `Rigidbody2D`。状态优先级为 `jump/fall > run > idle`，其中纵向速度大于阈值且未落地进入 jump，纵向速度小于负阈值且未落地进入 fall，水平速度超过阈值进入 run，否则 idle。

| 状态 | 触发逻辑 | 需要的素材组 | 缺失时回退 |
|---|---|---|---|
| idle | 落地且水平速度低于阈值 | idle | 第一组或 sourceSprites |
| run | 落地且水平速度高于阈值 | run/walk | idle 或第一组 |
| jump | 未落地且上升速度高于阈值 | jump | fall、idle 或第一组 |
| fall | 未落地且下降速度低于阈值 | fall | jump、idle 或第一组 |

该播放器只挂在 Visual 节点，Root 继续保持物理、碰撞、寻路与关卡选择语义。它不会改变 Root.localScale，从而遵守 LevelStudio 中“Root 是逻辑实体、Visual 是视觉表现”的规则。

## 道具、背景、场景动画与特效路径

对于道具、陷阱、背景与场景动画，分类器根据 `physicsType` 与命名进行默认归档。`pickup/collect/coin/gem/key` 进入 PickupConsume，保留触碰后消失或触发逻辑的扩展点；`hazard/spike/fire/explosion/bomb` 进入 HazardContact；`bg/background/parallax/cloud/mountain` 进入 BackgroundLayer；`water/lava/torch/portal/ambient` 进入 AmbientLoop；`fx/vfx/explosion/smoke/spark` 进入 VFX。

Unity Sprite Atlas 官方工作流建议把 Sprite 按共同使用场景拆分成较小 Atlas，以避免加载包含大量未用纹理的大 Atlas。[3] 本项目本次先不自动创建 Atlas，但分类字段会为后续“一键主题包/Atlas 生成”留出稳定数据结构。

## SEF Quick Apply 改造

SEF 当前能应用预设，也已有 `SpriteEffectController.ResetAllEffects()`。缺口在于两个体验点：第一，切换预设前如果不先清除旧状态，可能出现旧 shader keyword、MaterialPropertyBlock 或控制器字段残留；第二，用户需要一键回到普通 Sprite 默认效果，且不能误保存 Reset 为 Prefab 风格。

Unity 官方 `MaterialPropertyBlock.Clear()` 文档说明，`Clear()` 会清除 property block 中的材质属性值，典型用法是复用同一个 block 并在设置新属性前清空。[4] 因此 Quick Apply 的新规则是：应用非 Reset 预设前先调用 `ResetAllEffects()`，再应用新 preset，最后调用 `EditorSyncProperties()`；Reset 按钮明确命名为“一键还原默认外观”，只负责恢复默认并刷新，不自动保存 Prefab。这样既保证效果不叠加，也保留用户继续手动保存默认蓝图的可能。

| 操作 | 新行为 | 保护边界 |
|---|---|---|
| 点击任意效果预设 | 先清空所有 SEF 字段和运行时覆盖，再应用目标预设 | 不改变 Root/Visual 层级 |
| 点击一键还原默认外观 | 调用控制器 Reset + 同步，清除最后应用标记 | 不自动保存 Prefab |
| 自动保存 Prefab | 仅非 Reset 预设保存 | 避免把“还原”误写成风格蓝图 |
| 锁定效果 | 如果 marker 标记 VisualEffectLocked，则 Quick Apply 提示并默认不覆盖 | 用户可显式解锁后再应用 |

## References

[1]: https://docs.unity3d.com/6000.4/Documentation/Manual/AnimationStateMachines.html "Unity Manual: Animation state machine"
[2]: https://learn.unity.com/tutorial/introduction-to-sprite-animations "Unity Learn: Introduction to Sprite Animations"
[3]: https://docs.unity3d.com/2023.1/Documentation/Manual/SpriteAtlasWorkflow.html "Unity Manual: Sprite Atlas workflow"
[4]: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MaterialPropertyBlock.Clear.html "Unity Scripting API: MaterialPropertyBlock.Clear"
