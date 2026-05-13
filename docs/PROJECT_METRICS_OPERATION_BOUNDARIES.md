# MarioTrickster 全局度量衡与操作边界说明

**作者：Manus AI**  
**日期：2026-05-13**  
**适用范围：MarioTrickster Unity 2D 项目的关卡度量、素材换皮、角色碰撞体、跳跃手感与 Root/Visual 视碰分离架构。**

## 一句话结论

MarioTrickster 的基本原则是：**玩法真身在 Root 和 `PhysicsMetrics.cs`，换皮只动 Visual；要改手感走 `PhysicsConfigSO`，不要改全局尺子。**

项目采用 **Root / Visual 视碰分离架构**。Root 是真实玩法对象，承载 Transform 坐标、Collider2D、Rigidbody2D、控制器、伤害与机关逻辑；Visual 子物体只是显示层，承载 SpriteRenderer、动画、材质、视觉缩放和特效。`PhysicsMetrics.cs` 明确将白盒关卡碰撞体定义为物理真相，换素材时只替换视觉，不调整布局；所有关卡元素的物理尺寸都由它统一定义。[1]

> **安全操作公式：** 如果改动会影响 Root 坐标、Root Scale、Collider2D、Rigidbody2D、角色控制器或 `PhysicsMetrics.cs` 常量，它就是玩法级改动；如果只影响 Visual 的 Sprite、动画、材质、视觉缩放、Pivot 或切片，它通常是安全换皮改动。

## 操作边界总览

| 等级 | 能不能动 | 典型对象 | 操作原则 |
|---|---:|---|---|
| 红线 | **不要动** | `CELL_SIZE`、标准 PPU 常量、角色与地形碰撞体常量、Root Scale | 这些是全项目共用尺子，改了不是换皮，而是在改物理宇宙。 |
| 黄线 | **谨慎动** | Root 上的 Collider2D、`PhysicsConfigSO` 手感参数、Visual ↔ Collider Size Sync | 可以调，但每次都要有明确目的，并回归验证移动、跳跃、碰撞和旧关卡。 |
| 绿线 | **可以动** | Visual 的 Sprite、动画、材质、Visual Scale / Local Position、动画 FPS、切片 Pivot | 这些属于显示层，Apply Art 和素材导入管线就是为它们服务的。 |

这张表是后续所有换皮、导入素材、调角色尺寸和调跳跃手感时的判断基准。原则上，商业素材适配应优先走 **Apply Art to Selected**、素材导入管线、Sprite 切片、Pivot、Visual Transform 和动画配置，而不是修改全局度量衡。

## 红线：绝对不要随便动

`CELL_SIZE = 1f` 是整个 ASCII 关卡系统的基石。代码注释明确说明每个 ASCII 字符对应一个世界单位，所有碰撞体、生成坐标和物理计算都基于它；如果需要“看起来更大或更小”，应该调整相机、关卡构图或 Visual 层，而不是修改这个值。[1]

| 红线项 | 当前值或位置 | 为什么不能随便改 | 正确替代做法 |
|---|---|---|---|
| `PhysicsMetrics.CELL_SIZE` | `1f` | 关卡字符、生成坐标、碰撞体尺寸、跳跃距离全都依赖它。 | 调相机、调 Visual、调关卡布局，不改尺子。 |
| `STANDARD_PPU_32` 等 PPU 标准 | 当前工具链主规范为 `32` | PPU 是像素素材映射到世界单位的换算基础，乱改会造成素材尺寸整体漂移。 | 在导入流程里让素材适配规范；特殊素材改导入设置，不改全局常量。 |
| Mario / Trickster 碰撞体常量 | `0.8 x 0.95`，Y Offset `-0.025` | 角色碰撞体故意小于视觉图，防止贴墙卡缝、擦顶断跳，是平台跳跃手感的容错核心。[1] | 视觉不贴合时调 Visual，不优先调角色真实碰撞体。 |
| 地形与机关碰撞体常量 | Block、Spike、Fire、Bouncy、OneWay、Enemy、Goal 等 | ASCII 生成器和验证器按这些尺寸理解“可站、可跳、可碰、可死”。[1] | 新元素新增独立常量；旧元素不要为了某张图临时改。 |
| Root `transform.localScale` | 应保持 `(1,1,1)` | 多处代码明确要求 Root 不参与视觉缩放，防止碰撞、刚体、选择和动画被双重缩放。[2] | 只改 `Visual.localScale` 或 SpriteRenderer 显示方式。 |

尤其是 Mario Root，不要因为换了一套商业角色素材就去拉 Root Scale。Mario Root 是 **玩法真身**，它的 BoxCollider2D、Rigidbody2D、MarioController、PlayerHealth 等共同构成玩家实际控制对象；视觉上的高矮胖瘦应该放在 Visual 子物体上处理。[3]

## 黄线：可以改，但必须谨慎

`PhysicsConfigSO` 是专门给手感调优用的入口。它把 `maxSpeed`、`jumpPower`、`fallAcceleration`、`coyoteTime`、`jumpBuffer` 等参数集中到 ScriptableObject 中，允许运行时通过 Inspector 调整；同时 `PhysicsMetrics` 会通过 Facade 读取这些推导值，让验证器、Scene 辅助线和跳跃跨度同步更新。[4]

| 黄线项 | 什么时候可以调 | 怎么调才安全 | 验证重点 |
|---|---|---|---|
| Mario Root 上的 Collider2D Size / Offset | 只有当“视觉和受击、落地、踩敌判断明显不一致”时 | 小步调整，保留碰撞体小于视觉图的宽容原则 | 是否卡墙、擦顶、踩敌、过窄缝异常。 |
| `PhysicsConfigSO` 跳跃与移动参数 | 想整体改变手感、跳跃高度、水平跨度时 | 走 SO 面板和专用调参路径，不直接改硬编码默认值 | 旧关卡最大间隙、最小跳、Coyote、Jump Buffer 是否仍合理。 |
| Visual ↔ Collider Size Sync | 给非核心角色的机关或道具做“看起来像、摸起来也像”时 | 优先用于普通关卡物件；角色 Root 不参与这种缩放 | 机关触发范围、站立平台宽度、Trigger 是否过大或过小。 |
| 新增关卡元素碰撞体 | 新机制确实需要新尺寸时 | 在 `PhysicsMetrics.cs` 新增独立常量，并让生成器引用它 | 不复用旧常量做临时妥协，避免影响既有关卡。 |

手感参数不是“不能动”，但它属于系统级调整。比如把 `jumpPower` 调高，不只是 Mario 跳得更高，关卡验证器认为的最大跳跃高度、最大水平距离、Coyote 额外距离也会跟着变；这正是 Facade 设计的目的，但也意味着它会影响全部关卡。[1] [4]

## 绿线：换皮和美术层可以放心动

Apply Art 工具正是为绿线区域服务的：选中 Root 或 Visual 都可以，工具会自动归一回 Root，然后只替换 Visual 上的 SpriteRenderer、动画和材质，尽量保留原有行为组件与物理层。如果目标对象没有 SpriteRenderer，工具会在 `Visual` 子物体上创建，而不是把显示层和玩法层混在一起。[5]

| 绿线项 | 可以怎么动 | 风险级别 | 建议工具 |
|---|---|---:|---|
| Visual 的 Sprite / Sprite Sheet | 替换角色、平台、陷阱、道具外观 | 低 | Apply Art to Selected。 |
| Visual 的 `localScale` | 微调显示大小、角色视觉比例、机关外观大小 | 低到中 | Visual 模式或 Apply Art 后手调。 |
| Visual 的 `localPosition` | 调脚底、中心点、头顶视觉对齐 | 低 | Scene 里选 Visual 调整。 |
| `SpriteFrameAnimator` / `SpriteStateAnimator` FPS | 调动画播放快慢 | 低 | Inspector。 |
| Sprite Pivot / 切片方式 | 修正脚底点、中心点、帧格错位 | 低到中 | Apply Art 的 Auto Detect / AI Backend / Manual Grid。 |
| 材质、描边、闪白、溶解等 SEF 效果 | 增强表现，不改碰撞 | 低 | SEF Quick Apply / 效果工厂。 |

这里的关键判断标准是：**只要改动不会改变 Root 的坐标、碰撞体、刚体和控制逻辑，它通常就是安全的美术调整。**如果只是想让一张图看起来不要太大、让角色脚底对齐地面、让动画慢一点，都应该在 Visual、Sprite、Animator、材质或切片设置里解决。

## 背景：为什么项目要这样设计

MarioTrickster 的核心思路是先让关卡和手感在白盒状态下稳定成立，再让商业素材覆盖视觉。这样做的好处是，换图不会让已经验证过的关卡突然变难、平台突然变窄、角色突然卡墙，AI 或人类后续接手时也有一个明确的物理真相源。

更具体地说，`PhysicsMetrics.cs` 管“世界里一格到底多大、角色碰撞体到底多大、各类机关碰撞体到底多大”；`PhysicsConfigSO` 管“Mario 跑多快、跳多高、下落多快、容错时间多长”；Visual 子物体管“长什么样、动画怎么播、特效怎么显示”。三者分工清楚后，Apply Art 才能安全地批量处理商业素材，而不会误伤玩法。

> **推荐心智模型：** Root 是骨架，Collider 是身体边界，PhysicsMetrics 是尺子，PhysicsConfigSO 是手感调音台，Visual 是衣服。换衣服不要锯骨架，调音色不要改尺子。

## 常见需求与正确改法

| 你想做的事 | 应该动哪里 | 不应该动哪里 |
|---|---|---|
| 角色看起来太大或太小 | `Mario/Visual.localScale`、Sprite PPU / Pivot / 切片 | Mario Root Scale、`MARIO_COLLIDER_WIDTH/HEIGHT`。 |
| 脚底没有贴地 | Visual 的 Local Position 或 Sprite Pivot | Root Position、Collider Offset，除非确实是物理脚底错了。 |
| 平台图太宽但站立范围没问题 | Visual Scale 或 Tiled/Sliced 显示 | 平台 Collider Size。 |
| 平台看起来宽，实际站不上去 | 谨慎调 Collider Size，并同步验证 | 不要改 `CELL_SIZE`。 |
| 想让 Mario 跳更高 | `PhysicsConfigSO` 的 `jumpPower` / `fallAcceleration` | 不要改 `MAX_JUMP_HEIGHT` 的回退常量或关卡尺寸。 |
| 想让所有素材统一比例 | 导入设置、Apply Art、Sprite 切片策略 | 不要为某个素材包改全局 PPU 常量。 |
| 想让陷阱看起来更夸张 | Visual Scale、动画、材质、SEF 效果 | 不要扩大伤害 Trigger，除非玩法明确需要。 |
| 想新增一种机关 | 新增元素脚本、Registry、生成器映射、独立碰撞体常量 | 不要复用不匹配的旧常量做临时补丁。 |

## 后续维护守则

后续维护项目时，任何涉及尺寸、坐标、碰撞和跳跃跨度的改动，都应先问一句：**这是视觉问题，还是玩法问题？** 如果是视觉问题，就走 Apply Art、Sprite 导入、Visual Transform、Pivot 和动画；如果是玩法问题，再进入 `PhysicsConfigSO`、Collider2D 或 `PhysicsMetrics.cs` 的受控调整流程。

| 改动类型 | 默认处理路径 | 是否需要回归验证 |
|---|---|---:|
| 单个素材换皮 | Apply Art to Selected → Visual Sprite/Animator/Material | 低强度检查即可。 |
| 角色商业素材整套替换 | Apply Art → 切片/Pivot → Visual Scale/Position | 需要检查站立、跳跃、受伤、朝向和动画状态。 |
| 平台、陷阱、机关视觉替换 | Apply Art → Visual/Tiled/Sliced → 保留 Root Collider | 需要检查站立、触发和伤害边界。 |
| 手感参数调优 | `PhysicsConfigSO` → Scene 辅助线/验证器 → 旧关卡试玩 | 必须回归验证。 |
| 全局常量变更 | 原则上禁止；如确需变更，必须作为架构迁移处理 | 必须全项目回归。 |

## 最终结论

**红线不碰，黄线小步验证，绿线放心换皮。** 后续进行角色、关卡或商业素材换皮时，默认边界应当是：不改 `CELL_SIZE`，不拉 Root Scale，不为了适配图片去改全局碰撞常量，只在 Visual 和导入管线里解决美术问题；只有当目标明确属于玩法调整时，才进入 `PhysicsConfigSO`、Collider2D 或新增 `PhysicsMetrics` 常量的受控流程。

## References

[1]: ../Assets/Scripts/LevelDesign/PhysicsMetrics.cs "PhysicsMetrics.cs — 全局物理度量唯一真相源"  
[2]: ../Assets/Scripts/Editor/LevelEditorPickingManager.cs "LevelEditorPickingManager.cs — Root/Visual 选择与 Size Sync 边界"  
[3]: ../Assets/Scripts/Player/MarioController.cs "MarioController.cs — Mario Root 与 Visual 形变分离"  
[4]: ../Assets/Scripts/LevelDesign/PhysicsConfigSO.cs "PhysicsConfigSO.cs — ScriptableObject 实时手感调参面板"  
[5]: ../Assets/Scripts/Editor/AssetApplyToSelected.cs "AssetApplyToSelected.cs — Apply Art to Selected 换皮工具"
