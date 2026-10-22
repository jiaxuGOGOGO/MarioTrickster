# Art Pipeline Architecture Notes — 2026-05-12

本文件记录本轮商业素材接入改造前的代码现状，只作为后台工作笔记与后续设计依据。

## 现有入口

`AssetImportPipeline` 负责从零创建素材对象，当前只有五个粗粒度物理类型：`Character / Environment / Hazard / VFX / Prop`。它统一创建 `Root -> Visual` 结构，在 `Visual` 上放置 `SpriteRenderer`，多帧素材直接挂 `SpriteFrameAnimator` 进行单组循环播放，并在 `Root` 上写入 `ImportedAssetMarker`。当前不足是：无法区分主角状态动画、敌人状态动画、触碰爆炸道具、背景层、场景循环动画、一次性 VFX 等真实商业素材用途。

`AssetApplyToSelected` 负责把素材穿到已有对象上，保留已有行为组件。它会在缺失时创建 `Visual`，替换 `SpriteRenderer.sprite`，多帧素材直接挂 `SpriteFrameAnimator` 循环播放，随后根据 `BehaviorTemplate` 或已有组件推断碰撞/伤害/收集类行为。当前不足是：主角素材也会被当作循环播放器，无法根据 `MarioController` 已有的 `IsGrounded / Speed / VerticalSpeed` 等状态自动切换 `idle / run / jump / fall`。

`SpriteFrameAnimator` 是轻量循环帧播放器，只支持一组 `frames`。它适合环境循环动画、火焰、水流、背景装饰等，但不适合作为角色状态动画的统一方案。

`SEF_QuickApply` 提供效果预设快速应用。当前应用预设前不会先清理已有效果，容易出现描边、HSV、阴影、溶解等状态叠加；Reset 预设只调用 `SpriteEffectController.ResetAllEffects()`，没有明确“恢复到应用 SEF 前材质”的一键还原体验。

`SpriteEffectController` 已经具备 `ResetAllEffects()` 与 `EditorSyncProperties()`，其中 keyword 通过 `Material` 同步，数值通过 `MaterialPropertyBlock` 同步。现有能力可复用为“切换前清状态”，但仍需要一个编辑器侧的材质还原/默认快照组件来实现“一键还原”。

`LevelEditorPickingManager` 与 `TestConsoleWindow` 已建立 Root/Visual 选中模式和 Size Sync。关卡编辑器的美术接入原则应继续遵守 Root 保留物理与行为、Visual 承担 Sprite/SEF/动画；给用户的入口应偏向“自动识别用途 + 少量确认”，避免要求用户理解内部分类。

## 关键约束

所有新改造必须保留 S37 视碰分离：Root 的 `localScale` 不作为视觉形变入口，`Visual` 承载 SpriteRenderer、动画、SEF。Mario 的视觉形变已由 `MarioController.visualTransform` 管理，新角色状态动画只能换 sprite/Animator 状态，不得覆盖 Root 或破坏现有 squash/stretch。

Mario 已公开或写入如下动画状态：`IsGrounded`、`Speed`、`VerticalSpeed`、`IsMoving`、`Velocity`。新的状态播放器应优先读 `MarioController`，可平滑映射到 `idle / run / jump / fall`，并允许以后扩展 `hurt / death / climb / swim`。

## 外部调研摘记

Unity 官方动画状态机文档确认：角色或 GameObject 通常有多种动作动画，例如 idle、walk、fall；Mecanim 用状态机组织这些动作，并通过 `Animation Parameters` 在脚本与 Animator Controller 之间传递状态，Transitions 负责在条件满足时切换或混合动画。对本项目而言，这支持“主角素材不能走单组循环播放器，而应由运动状态驱动 idle/run/jump/fall”的设计方向。来源：https://docs.unity3d.com/6000.4/Documentation/Manual/AnimationStateMachines.html

Unity 官方 `MaterialPropertyBlock.Clear()` 文档确认：`Clear()` 用于清除 property block 中的材质属性值，典型用法是复用同一个 block，在每次设置新的 float/vector/color/matrix 前先清空。对 SEF 而言，这支持“切换效果前先清空 MaterialPropertyBlock 覆盖值”的实现，但 shader keyword 与 sharedMaterial 本身仍需要由 `SpriteEffectController.ResetAllEffects()` 或额外材质快照处理。来源：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MaterialPropertyBlock.Clear.html

独立开发教程资料也将 2D Sprite 工作流拆成 sprite sheet、animations、asset management、Unity State Machine、player state switching 与 controller input。对本项目而言，商业素材接入应分为“素材识别/分组”和“运行时状态选择”两层，而不是把所有多帧图都视为同一种循环动画。来源：https://indiegamebuzz.com/create-2d-sprite-based-animation-states-in-unity3d/

Unity 官方 Sprite Atlas 工作流确认：Sprite Atlas 用于把多个 Sprite/Texture2D 打包管理；官方建议按“共同使用场景”拆分较小 Atlas，避免场景只用少量 Sprite 时加载过大的 Atlas。对本项目而言，商业素材导入不应只按图片文件逐个孤立处理，而应在角色、敌人、地形、背景、场景动画、VFX、UI 等用途层面建立稳定分组，后续才能自然扩展 Atlas/主题包。来源：https://docs.unity3d.com/2023.1/Documentation/Manual/SpriteAtlasWorkflow.html

Unity Learn 对 Sprite Animations 的说明确认：Sprite Animation 是面向 2D 资产的 Animation Clip，可由 Sprite Sheet 中按网格排列的一组 Sprite 形成；多帧 Sprite 像翻页书一样依次播放。它还提醒动画帧应均匀排布，否则播放时容易晃动。对本项目而言，单组 `SpriteFrameAnimator` 可继续用于“同一动作连续帧”，但角色素材应先拆为动作组，再由状态切换选择动作组。来源：https://learn.unity.com/tutorial/introduction-to-sprite-animations

OpenGameArt 的 LPC Expanded 素材说明展示了实际商业/开源角色素材包的典型组织方式：同一角色会覆盖 Idle、Sit、Jump、Run、Walk、Combat、Emotes 等多个动画族，并且提供按 animation filter 查找支持动作的能力。对本项目而言，应把 `idle / run / jump / fall / attack / hurt / death` 视为一级分类字段，而不是把它们混在一条帧序列里。来源：https://opengameart.org/content/lpc-expanded-sit-run-jump-more
