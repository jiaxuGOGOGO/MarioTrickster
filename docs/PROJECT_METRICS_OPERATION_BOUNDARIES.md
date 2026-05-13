# MarioTrickster Unity 实操度量衡与操作边界指南

**作者：Manus AI**  
**日期：2026-05-13**  
**适用范围：MarioTrickster Unity 2D 项目的关卡度量、素材换皮、角色碰撞体、跳跃手感、素材导入与 Root/Visual 视碰分离架构。**

> 本文档面向在 Unity Editor 中实际使用、换皮、摆关卡、调手感和排查碰撞问题的开发者。核心原则只有一句：**Root 是玩法真身，Visual 是显示外壳；手感走 PhysicsConfigSO，关卡尺子走 PhysicsMetrics；不要为了某张图去改整个世界。**

## 1. 先建立一个直观心智模型

MarioTrickster 目前采用 **Root / Visual 视碰分离**。在 Hierarchy 中看到一个关卡物体时，通常真正承载玩法的是父物体 Root，显示图像的是子物体 `Visual`。Root 上放 `Transform.position`、`BoxCollider2D`、`Rigidbody2D`、控制脚本、伤害脚本、平台脚本和机关脚本；Visual 上放 `SpriteRenderer`、动画播放器、材质和视觉缩放。[1]

| 层级 | 在 Unity 里通常看到什么 | 它负责什么 | 你能不能随便改 |
|---|---|---|---|
| Root | 父物体，例如 `Mario`、`BouncyPlatform_12_3`、`SpikeTrap_5_0` | 真实坐标、碰撞体、刚体、脚本、关卡逻辑 | **不能当作美术缩放层使用**。移动和摆放可以动 Position，但不要用 Scale 解决图片大小问题。 |
| Visual | 子物体，通常命名为 `Visual`，带 `SpriteRenderer` | 贴图、动画、特效、视觉大小、视觉偏移 | **主要美术调整入口**。换图、调显示大小、调脚底对齐，优先动这里。 |
| PhysicsMetrics | `Assets/Scripts/LevelDesign/PhysicsMetrics.cs` | 全项目格子尺寸、角色/平台/陷阱/道具碰撞尺寸、跳跃跨度 Facade | **系统级真理源**。只有确认要改变物理规则时才改。 |
| PhysicsConfigSO | `Assets/Resources/PhysicsConfig.asset`，类型为 `PhysicsConfigSO` | Mario 移动、跳跃、重力、Coyote Time、Jump Buffer 等手感参数 | **推荐调手感入口**。可以在 Inspector 中调，但要按推导值验证关卡跨度。 |

在 Unity 中最常见的错误是：看见图太大或太小，就直接选中父物体拉 `Scale`。这个项目里不要这样做。**父物体 Scale 会把碰撞体、刚体、脚本空间和视觉一起缩放，等于改变玩法；Visual Scale 只改变显示，才是换皮和美术适配的正确位置。**

## 2. 日常在 Unity 中应该怎么操作

### 2.1 给已有白盒物体换素材

在 Scene 里选中白盒物体，然后打开 `MarioTrickster/Apply Art to Selected`，也可以使用 Art Hub 中的 **Apply Art to Selected** 入口。选中 Root 或点到 Visual 都可以，工具会自动归一回 Root 执行换皮。它的语义是：**保留已有行为组件，只替换贴图、动画和材质**。[5]

| 你在 Unity 里想做的事 | 正确操作 | 不要这样做 |
|---|---|---|
| 给平台、陷阱、敌人换图片 | 选中场景中的已有白盒物体，使用 Apply Art to Selected。 | 删除原物体后拖入纯图片 Prefab，再手动补脚本。 |
| 图片看起来太大 | 选中 `Visual`，调整 `Visual.localScale`，或修正 Sprite PPU / Pivot / 切片。 | 拉 Root 的 `Scale`。 |
| 图片脚底没有贴地 | 调整 `Visual.localPosition.y` 或 Sprite Pivot。 | 改 Root Position 或 Collider Offset，除非实际落地点真的错。 |
| 想让动画变快或变慢 | 调 `SpriteFrameAnimator` / `SpriteStateAnimator` 的 FPS。 | 通过改物理参数来迁就动画。 |
| 想加闪白、描边、溶解等效果 | 给 Visual 上的 `SpriteRenderer` 使用 SEF 材质或相关快速工具。 | 把视觉特效组件挂到 Root 后又依赖 Root 缩放。 |

换皮后先进入 Play Mode 走一遍：Mario 是否还能站上平台，尖刺/火焰是否还有合理容错，敌人是否仍能巡逻，收集物是否可拾取。只要这些行为没有变，说明换皮没有误伤玩法层。

### 2.2 在 Scene 中移动、摆放、微调大小

项目的编辑器工具提供了 Picking 模式。**Root 模式**适合移动、旋转、批量摆放关卡；**Visual 模式**适合只调外观；**Size Sync** 会在 Visual 大小和 Root `BoxCollider2D.size` 之间做联动。[2]

| 模式 | 适合场景 | 使用建议 |
|---|---|---|
| Root 模式 | 搭关卡、移动平台、调整机关位置、整体搬动物体 | 默认使用。此时选中的是玩法真身，改 Position 是合理操作。 |
| Visual 模式 | 图片大小、角色外观比例、平台视觉厚度、脚底对齐 | 只改显示，不改物理。大多数美术问题都在这里解决。 |
| Size Sync | 普通机关或道具需要“看起来多大，摸起来也多大” | 谨慎使用。它会同步碰撞体尺寸，不建议随手用于 Mario/Trickster 这类核心角色。 |

判断是否可以开 Size Sync 的方法很简单：如果这个物体的碰撞范围本来就应该跟视觉范围一致，例如大号收集物、大号触发器、某个非标准机关，可以开；如果它依赖固定手感，例如 Mario、Trickster、标准砖块、标准跳台、标准尖刺，优先不要开。

### 2.3 调 Mario 的跳跃、速度和手感

如果你的目标是“Mario 跳高一点、跑快一点、短跳更明显、边缘跳更宽容”，不要改 `PhysicsMetrics.cs` 里的回退常量，也不要改关卡格子大小。正确入口是 `PhysicsConfigSO`。[4]

推荐路径是：在 Project 面板找到 `Assets/Resources/PhysicsConfig.asset`。如果没有，就在 `Assets/Resources/` 下右键创建 `Create → MarioTrickster → Physics Config`，并确保资产名为 `PhysicsConfig`。MarioController 会优先读取该 SO，`PhysicsMetrics` 也会通过 Facade 读取它的推导值，让验证器、跳跃辅助线和关卡跨度同步更新。[3] [4]

| 目标 | 主要参数 | 判断依据 | 改完看哪里 |
|---|---|---|---|
| 跳得更高 | `jumpPower` 增大，或 `fallAcceleration` 减小 | `DerivedMaxJumpHeight = jumpPower² / (2 × fallAcceleration)` | Inspector 底部推导值、Scene 里的 JumpArcVisualizer、旧关卡高台。 |
| 平跳距离更远 | `maxSpeed` 增大，或 `jumpPower / fallAcceleration` 增大 | `DerivedMaxJumpDistance = maxSpeed × 2 × jumpPower / fallAcceleration` | 最大间隙、移动平台、敌人区节奏。 |
| 短跳更短更利落 | `jumpEndEarlyGravityModifier` 增大 | `DerivedMinJumpHeight` 变小 | 小跳过低矮障碍、连跳节奏。 |
| 离开边缘还能跳 | `coyoteTime` 增大 | `DerivedCoyoteBonusDistance = maxSpeed × coyoteTime` | 悬崖边缘、连续平台边缘、最大含 Coyote 间隙。 |
| 落地前提前按跳更舒服 | `jumpBuffer` 增大 | 体验依据为提前输入容错，不直接改变跳跃跨度 | 连续跳平台、弹跳平台落点。 |
| 下落更快、更有重量 | `fallAcceleration` 增大，`maxFallSpeed` 配合调整 | 上升/下落节奏和最大高度都会变 | 高台落下、陷阱区反应时间、相机跟随。 |

手感调整的最低验证标准是：至少跑一遍基础平地、最大横向间隙、最高跳台、短跳障碍、边缘 Coyote 跳、弹跳平台和陷阱区。因为这些参数不是只影响 Mario 手感，也会改变关卡验证器认为“能不能跳过去”的标准。

## 3. 红线：平时不要动，但真要全局调整时按流程来

“红线不要动”不是说永远不能改，而是说它们不是日常美术或局部调参入口。只有当你明确要改变项目的全局物理约定、素材导入规范或玩法尺度时，才可以在分支中改，并且要有依据、有迁移、有回归验证。

### 3.1 `CELL_SIZE`：一格等于多少世界单位

`PhysicsMetrics.CELL_SIZE = 1f` 表示 ASCII 关卡中的一个字符等于 Unity 世界中的 1 个单位。AsciiLevelGenerator 的生成坐标、连续方块合并、平台宽度、出生点、碰撞体尺寸和跳跃跨度全部建立在这个假设上。[1]

| 常见诉求 | 不要做 | 正确做法 |
|---|---|---|
| 觉得整个关卡看起来太大或太小 | 改 `CELL_SIZE` | 调 Camera Orthographic Size、Cinemachine framing、Game 视图分辨率或美术显示比例。 |
| 想让素材显示成更大颗粒 | 改 `CELL_SIZE` | 调 Sprite PPU、Visual Scale、切片尺寸或主题图。 |
| 想做一个完全不同尺度的新项目 | 直接在主分支改 `CELL_SIZE` | 新建迁移分支，评估所有 ASCII 模板、生成器、碰撞体、相机、跳跃参数和测试。 |

如果确实要全局改 `CELL_SIZE`，调整位置是 `Assets/Scripts/LevelDesign/PhysicsMetrics.cs`。但这不是一次常量替换，而是一次世界尺度迁移。调整依据必须是新的设计单位定义，例如“1 个 ASCII 字符必须等于 0.5 Unity units，因为要兼容某套既有关卡坐标系统”。改完必须重新生成所有 ASCII 关卡，检查 Root 坐标、合并平台宽度、出生点、相机边界、JumpArcVisualizer、最大跳跃距离、平台缝隙、敌人巡逻和所有碰撞体。

**结论：在当前项目中，`CELL_SIZE` 应视为锁死。只要目的不是重建整个世界尺度，就不要改。**

### 3.2 Root `localScale`：玩法真身的缩放

Root 的 `localScale` 应保持 `(1, 1, 1)`。这是项目里最容易被 Unity 用户误改的地方，因为 Scene 视图的缩放工具很顺手，但它会连碰撞体一起缩放。[2] [3]

| 如果你想 | 应该改 | 不应该改 |
|---|---|---|
| 角色看起来更高 | `Mario/Visual.localScale.y` 或 Sprite Pivot/PPU | `Mario.transform.localScale` |
| 平台视觉更宽 | `Platform/Visual.localScale.x` 或 SpriteRenderer Tiled/Sliced Size | `Platform.transform.localScale.x` |
| 陷阱看起来更夸张 | Visual Scale、动画、材质 | Root Scale |
| 让触发范围真的变大 | Root 上的 `BoxCollider2D.size`，并记录原因 | Root Scale |

只有一种情况下可以处理 Root Scale：你接手了历史遗留物体，Root Scale 已经不是 `(1,1,1)`，需要做“烘焙迁移”。迁移方式不是继续使用这个 Scale，而是把缩放折算到 Visual Scale 和 Collider Size 中，然后把 Root Scale 重置为 `(1,1,1)`。迁移后需要验证该物体的 Position、Collider、脚本行为和 Prefab Override。

### 3.3 角色碰撞体尺寸：Mario / Trickster 的宽容手感

Mario 和 Trickster 的碰撞体尺寸故意小于视觉图。当前核心参数在 `PhysicsMetrics.cs` 中，例如 Mario 宽度约 `0.8`，高度约 `0.95`，并带有轻微 Y Offset。这个设计是为了让玩家贴墙、擦顶、跳缝时更宽容。[1]

| 现象 | 优先判断 | 推荐处理 |
|---|---|---|
| 图片头顶高出碰撞体 | 这通常是正常宽容设计 | 调 Visual Pivot / Local Position，不急着加高 Collider。 |
| 明明踩到平台却判定没站上 | 可能是碰撞体脚底或地面检测问题 | 先看 Ground Detection、Collider Offset、平台 Collider，再决定是否改。 |
| 经常卡砖缝或擦墙卡住 | 碰撞体可能太宽，或 Root Scale 被误改 | 检查 Root Scale 是否为 1，再小步调整角色 Collider。 |
| 玩家觉得“视觉碰到陷阱但没死” | 这可能是刻意的玩家宽容 | 不要因为单张尖刺图去放大角色受击体。 |

如果确实要全局调整角色碰撞体，位置是 `PhysicsMetrics.cs` 的 Mario/Trickster Collider 常量，并要检查创建 Mario/Trickster 的脚本或 Prefab 是否引用这些常量。调整依据不能是“新图看起来不贴”，而应是 Play Mode 中反复出现的真实玩法问题，例如长期卡墙、落地误判、头顶擦碰导致跳跃中断。改完必须验证贴墙下落、贴顶跳、窄缝通过、踩敌、受击、Coyote 跳和所有已有教程关卡。

### 3.4 标准地形、平台、陷阱、道具碰撞体

地形、平台、陷阱和道具的标准尺寸由 `PhysicsMetrics.cs` 统一管理。AsciiLevelGenerator 在生成不同元素时，会把这些标准尺寸写入对应 Root 的 `BoxCollider2D`。这意味着：你改一个常量，会影响所有由该元素生成的旧关卡。[1]

| 元素类型 | 当前设计意图 | 什么时候才考虑全局改 |
|---|---|---|
| Ground / Breakable Block | 1x1 网格对齐，连续方块必须无缝 | 只有项目决定改变基本砖块规则时。 |
| OneWay / Bouncy / Collapse Platform | 宽度按 ASCII 连续段或标准格推导，高度较薄 | 只有平台站立手感整体错误时，不为单张平台图改。 |
| Spike / Fire / Saw / Pendulum | 碰撞体比视觉小，给玩家容错 | 只有死亡判定长期过松或过紧，并经关卡验证后。 |
| Collectible / Checkpoint / Goal | Trigger 可略大，便于触发 | 只有触发体验普遍不稳定时。 |
| Enemy | 碰撞体一般略小于视觉，保证踩踏和接触稳定 | 只有踩头、撞墙、巡逻、受击判定有系统性问题时。 |

如果只是某一个场景里某个机关需要特殊大小，不要改 `PhysicsMetrics` 全局常量。可以在该物体实例上调整 `BoxCollider2D.size`，并在命名、备注或 Prefab 中明确这是特殊机关。如果这种特殊规则会复用很多次，应新增一个独立元素类型或新常量，而不是复用旧常量做临时妥协。

### 3.5 PPU 与素材导入规范

PPU 决定像素图片如何映射到 Unity 世界单位。项目中保留了 `STANDARD_PPU_16`、`STANDARD_PPU_32` 等标准，当前工具链倾向按素材像素格来规范导入。它与 `CELL_SIZE` 不是一回事：**PPU 解决图片显示尺寸，CELL_SIZE 解决关卡物理网格。**[1]

| 场景 | 正确判断 | 正确处理 |
|---|---|---|
| 32x32 单格砖块导入后刚好占 1 格 | PPU 通常应为 32 | 让导入管线按规范设置 PPU。 |
| 16x16 像素素材包 | 可能用 PPU 16，也可能统一转到项目规范 | 先决定这套素材的一格像素基准，再导入。 |
| 某张 Boss 图很大 | 它不是 1 格素材 | 不要改全局 PPU，单独设置 Sprite、Visual Scale 或 Prefab。 |
| 主题素材整体比例不一致 | 是素材规范问题，不是物理问题 | 在导入预处理、切片、Pivot、Visual 层解决。 |

如果确实要调整全项目素材规范，应从导入管线和美术规范文档入手，而不是为了图片大小去改 `CELL_SIZE` 或碰撞体。调整依据应该是整套素材的像素网格，例如“本素材包所有地块均为 48x48，一格等于 48 像素”。改完要抽样检查单格砖块、长条平台、角色、敌人、道具、动画切片和主题替换效果。

## 4. 黄线：可以改，但要带着目标和验证改

黄线项不是禁止，而是需要知道自己在改玩法。每次调整前先回答三个问题：**我要解决的具体问题是什么？这个改动影响单个物体还是全项目？我用什么场景验证它没有破坏旧关卡？**

| 黄线项 | Unity 入口 | 合理使用场景 | 必须验证 |
|---|---|---|---|
| Root `BoxCollider2D.size` / `offset` | Inspector 中 Root 的 Collider | 某个机关真实触发范围需要调整；特殊平台站立范围需要变化。 | 站立、穿越、触发、受击、与邻近地形拼接。 |
| PhysicsConfigSO | `Assets/Resources/PhysicsConfig.asset` Inspector | 全局移动/跳跃/重力手感调优。 | 推导跳高、推导横距、旧关卡最大间隙、短跳、Coyote、Jump Buffer。 |
| Size Sync | Picking 工具栏 | 普通道具/机关需要视觉和碰撞同步缩放。 | 是否误改了角色或标准地形；Collider 是否过大/过小。 |
| 新元素的 PhysicsMetrics 常量 | `PhysicsMetrics.cs` 新增独立常量 | 新机关有稳定复用规则。 | 生成器、主题替换、测试场景、旧元素不受影响。 |
| SpriteAutoFit 模式 | Visual 上的 `SpriteAutoFit` | 地形平铺、九宫格平台、独立角色缩放。 | Tiled/Sliced 是否变形，Scale 是否符合预期。 |

黄线调整建议在单独提交中完成，并写清楚原因。不要把“换图”“调手感”“改碰撞体”“改关卡生成规则”混在一个提交里，否则后续很难判断是哪一步造成了退化。

## 5. 绿线：日常可以放心做的美术与体验调整

绿线项主要影响显示，不改变玩法真身。它们是 Unity 使用中最常见、最安全的操作。

| 绿线项 | Unity 入口 | 说明 |
|---|---|---|
| 替换 Sprite / Sprite Sheet | Apply Art to Selected、Asset Import Pipeline | 保留 Root 行为，只换 Visual 图像。 |
| 调 Visual Scale | 选中 `Visual` 子物体 | 解决外观太大、太小、太胖、太瘦。 |
| 调 Visual Local Position | 选中 `Visual` 子物体 | 解决脚底、中心点、头顶视觉对齐。 |
| 调 Sprite Pivot | Sprite Editor / 导入设置 | 适合从源头修正脚底点和帧中心。 |
| 调动画 FPS / 帧序列 | SpriteFrameAnimator / SpriteStateAnimator | 改表现节奏，不改物理。 |
| 调材质和 SEF 效果 | Visual 的 SpriteRenderer Material | 闪白、描边、溶解、受击反馈等。 |
| 调 Sorting Layer / Order | Visual 的 SpriteRenderer | 解决遮挡关系。 |

绿线调整也要 Play Mode 快速看一眼，但通常不需要跑完整物理回归。只要没有改 Root、Collider、Rigidbody 和 PhysicsConfigSO，风险较低。

## 6. 问题导向速查表

| 你遇到的问题 | 第一反应应该检查 | 推荐修复路径 |
|---|---|---|
| “换图后角色变大，碰撞也怪了” | Root Scale 是否被改过 | 把 Root Scale 还原为 1；把大小迁移到 Visual Scale。 |
| “Mario 明明脚踩地面但看起来悬空” | Visual Pivot / Local Position | 调 `Mario/Visual.localPosition.y` 或 Sprite Pivot。 |
| “Mario 跳不过以前能跳过的缝” | PhysicsConfigSO 是否改过；DerivedMaxJumpDistance 是否变小 | 恢复或重新调 `maxSpeed`、`jumpPower`、`fallAcceleration`。 |
| “Mario 跳得太飘” | `fallAcceleration`、`apexGravityMultiplier`、`maxFallSpeed` | 先调 PhysicsConfigSO，不改关卡格子。 |
| “平台图很宽，但只能站中间一点” | Visual 和 Collider 是否不一致 | 如果这是普通平台实例，可调 Collider 或开 Size Sync；如果是标准平台，先确认是否生成规则正确。 |
| “尖刺看起来碰到了但没死” | 是否属于宽容碰撞设计 | 先不要改；只有死亡判定普遍过松时再调 Spike/Saw 等碰撞体。 |
| “收集物很难捡到” | Collectible Collider / Trigger 范围 | 可以小幅增大该实例或全局 Collectible 尺寸，但要验证密集收集物。 |
| “主题替换后地块变形” | SpriteAutoFit 模式、PPU、Sprite Mesh Type | 地形优先 Tiled，平台可考虑 Sliced，角色/敌人用 Visual Scale。 |
| “Scene 点选总是点到图片，不好移动物体” | Picking 模式 | 切到 Root 模式。 |
| “我只想调图片大小，但怕改到碰撞” | Picking 模式和 Inspector 当前对象 | 切到 Visual 模式，确认 Inspector 选中的是 `Visual`。 |

## 7. 如果必须全局调整，按这个决策流程执行

全局调整前，不要直接改代码。先按以下流程判断。

| 步骤 | 要回答的问题 | 如果答案不清楚 |
|---|---|---|
| 1. 定义问题 | 这是视觉问题、手感问题、碰撞问题，还是关卡尺度问题？ | 先做最小复现场景，不要全局改。 |
| 2. 选择入口 | 视觉走 Visual，手感走 PhysicsConfigSO，碰撞规则走 PhysicsMetrics，导入比例走 PPU/导入管线。 | 优先选择影响范围最小的入口。 |
| 3. 明确依据 | 是基于目标跳高/横距公式、素材像素网格、玩家测试反馈，还是关卡验证失败？ | 没有依据就不要改红线。 |
| 4. 建分支 | 是否会影响旧关卡或所有同类元素？ | 会影响就单独建分支和单独提交。 |
| 5. 小步改动 | 一次只改一个变量或一组强相关变量。 | 不要混合换皮、碰撞、手感和生成器调整。 |
| 6. 回归验证 | 是否跑过基础移动、最大跳、短跳、陷阱、平台、收集物、旧关卡？ | 没跑完不要合并。 |
| 7. 写记录 | 文档或提交信息是否说明了“为什么改、影响什么、怎么验证”？ | 没记录的红线改动等于技术债。 |

推荐把全局调整分成以下几类提交：`physics-config` 手感调整、`metrics` 碰撞规则调整、`art-import` 素材导入规范调整、`level-generation` 关卡生成规则调整。这样后续出现问题时可以快速回滚或定位。

## 8. 典型安全工作流

### 8.1 换一套 Mario 商业素材

第一步，用导入管线或 Apply Art 把素材应用到 Mario 的 Visual。第二步，检查 Visual 的 Pivot、Scale、Local Position，让脚底贴地、头顶合理。第三步，在 Play Mode 中测试站立、跑动、跳跃、短跳、受击、死亡和弹跳平台。第四步，如果只是视觉不贴合，继续调 Visual；只有在真实碰撞长期误判时，才考虑角色 Collider。

### 8.2 调整全局跳跃手感

第一步，打开 `Assets/Resources/PhysicsConfig.asset`。第二步，根据目标先设定设计值，例如“最高跳约 2.8 格”“满速平跳约 5 格”“Coyote 额外距离约 1 格”。第三步，通过 Inspector 中的推导值反推 `jumpPower`、`fallAcceleration`、`maxSpeed` 和 `coyoteTime`。第四步，看 JumpArcVisualizer 是否符合预期。第五步，跑旧关卡最大间隙和教程关卡。不要直接改 `PhysicsMetrics` 的默认回退值来调手感。

### 8.3 新增一个标准机关

第一步，保持 Root / Visual 结构：行为脚本和 Collider 在 Root，SpriteRenderer 在 Visual。第二步，如果该机关有稳定物理尺寸，在 `PhysicsMetrics.cs` 新增独立常量，不复用无关旧常量。第三步，在 AsciiElementRegistry / AsciiLevelGenerator 中建立字符映射和生成逻辑。第四步，让主题系统或 Apply Art 可以替换 Visual。第五步，补测试场景，验证旧元素不受影响。

### 8.4 某个素材包整体比例不对

第一步，先确认素材包的像素网格，例如 16x16、32x32 或 48x48。第二步，调整导入 PPU、切片网格和 Pivot。第三步，只在必要时调整 Visual Scale。第四步，不要因为素材包比例问题修改 `CELL_SIZE`、角色 Collider 或平台 Collider。素材比例是导入问题，不是物理世界问题。

## 9. 最终原则

只要你是在 Unity 中做日常内容生产，优先级应当是：**先 Visual，后 Collider；先 PhysicsConfigSO，后 PhysicsMetrics；先局部实例，后全局常量；先验证依据，后红线改动。**

如果一个问题能通过 Visual、Sprite 导入、Pivot、动画或材质解决，就不要碰 Root 和 PhysicsMetrics。如果一个问题是手感问题，就去 PhysicsConfigSO 调，并观察推导值。如果一个问题必须改变全局碰撞规则，就在 `PhysicsMetrics.cs` 中小步修改、记录依据并完整回归。这样项目才能在继续换皮、扩关、加机关时保持稳定。

## References

[1]: ../Assets/Scripts/LevelDesign/PhysicsMetrics.cs "PhysicsMetrics.cs — 全局物理度量唯一真相源"  
[2]: ../Assets/Scripts/Editor/LevelEditorPickingManager.cs "LevelEditorPickingManager.cs — Root/Visual 选择与 Size Sync 边界"  
[3]: ../Assets/Scripts/Player/MarioController.cs "MarioController.cs — Mario Root 与 Visual 形变分离"  
[4]: ../Assets/Scripts/LevelDesign/PhysicsConfigSO.cs "PhysicsConfigSO.cs — ScriptableObject 实时手感调参面板"  
[5]: ../Assets/Scripts/Editor/AssetApplyToSelected.cs "AssetApplyToSelected.cs — Apply Art to Selected 换皮工具"
