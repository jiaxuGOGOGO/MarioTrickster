# 关卡度量转译与视碰分离技术方案 (Physics Metrics & Visual-Collision Decoupling)

## 1. 核心理念与业界背景

在 2D 平台跳跃游戏开发中，"关卡度量转译 (Level Metrics Translation)" 与 "视碰彻底分离 (Visual-Collision Decoupling)" 是构建工业化关卡管线的两大基石。

**关卡度量转译**指的是将玩家的物理能力（跳跃高度、水平速度、滞空时间）精确转化为关卡设计中的网格尺寸（如：最大可跨越间隙为 4 格，最高可跳跃台阶为 2.5 格）。这使得关卡设计师（或 AI 生成器）可以在纯文本或白盒阶段，就确保关卡的"物理正确性"，避免出现死路或不可逾越的障碍 [1]。

**视碰彻底分离**则是指物理碰撞体（Hitbox/Hurtbox）与视觉表现（Sprite/Animation）的完全解耦。碰撞体尺寸代表了游戏的"物理真相"，它应该基于手感和容错率进行精心调优，并在整个开发周期中保持绝对稳定。当美术替换视觉素材时，绝不能影响物理碰撞体的尺寸，从而保证关卡布局的稳定性 [2]。

业界顶尖的平台跳跃游戏（如《蔚蓝 Celeste》、《空洞骑士 Hollow Knight》、《超级马里奥世界 Super Mario World》）均深度应用了这些理念。例如，《蔚蓝》的开发者 Maddy Thorson 曾详细分享过游戏中的 10 大容错机制，其中就包括"角色碰撞体小于视觉 Sprite"以及"半重力跳跃顶点"等设计 [3]。

## 2. 物理度量体系 (Physics Metrics)

本项目通过 `PhysicsMetrics.cs` 建立了一个全局的物理度量常量中心。所有的生成器、编辑器脚本和运行时逻辑，都必须引用此处的常量，以确保物理真相的唯一性。

### 2.1 角色物理能力演算

基于 `MarioController` 的物理参数，我们通过运动学公式精确推导出了角色的跳跃极限：

*   **重力加速度 (fallAcceleration)**: 80 units/s²
*   **跳跃初速度 (jumpPower)**: 20 units/s
*   **最大水平速度 (maxSpeed)**: 9 units/s
*   **Coyote Time**: 0.15s

**公式推导**：
*   **原地最高跳跃高度 (H_max)** = $v^2 / (2g)$ = $20^2 / (2 \times 80)$ = **2.5 格**
*   **满速平跳最大水平距离 (D_max)** = $maxSpeed \times 2 \times (v/g)$ = $9 \times 0.5$ = **4.5 格**
*   **Coyote Time 额外水平距离 (D_coyote)** = $maxSpeed \times coyoteTime$ = $9 \times 0.15$ = **1.35 格**
*   **含 Coyote Time 的最大可跨越间隙** = 4.5 + 1.35 = **5.85 格**

### 2.2 关卡设计安全约束

为了确保 AI 生成的 ASCII 模板绝对可行，我们制定了以下安全约束：

*   **安全间隙上限**：4 格（不含 Coyote Time，保留充足容错空间）
*   **安全高台上限**：2 格
*   **极限高台**：2.5 格（等于 MAX_JUMP_HEIGHT，需要精确操作，仅在最高难度出现）

## 3. 视碰分离与碰撞体黄金比例

在 `PhysicsMetrics.cs` 中，我们定义了所有关卡元素的标准碰撞体尺寸。这些尺寸经过精心调优，以提供最佳的平台跳跃手感。

### 3.1 角色碰撞体 (The Generous Hitbox)

角色的碰撞体必须**小于**视觉 Sprite。这是顶级平台跳跃手感的核心秘密：
*   **宽度 (0.8f)**：防止贴墙下落时卡在砖块接缝（浮点精度问题）。
*   **高度 (0.95f)**：防止起跳时头部微擦天花板导致跳跃被截断。

这种设计在视觉上营造了一种"明明碰到了但物理上没判定"的宽容感，极大地提升了玩家的爽快感 [3]。

### 3.2 陷阱与环境碰撞体

*   **地刺 (SpikeTrap)**：碰撞体尺寸为 `(0.9f, 0.35f)`，且向下偏移。视觉上是尖刺，但物理碰撞体更小，给予玩家"差一点就碰到"的宽容感。
*   **火焰 (FireTrap)**：碰撞体尺寸为 `(0.6f, 0.6f)`，比视觉表现小，提供容错。
*   **地面/墙壁 (Block)**：死死锁定 `1x1`，绝不修改。

## 4. 换肤系统与 SpriteAutoFit

为了在换肤时保持物理真相不变，我们重写了 `SpriteAutoFit.cs`，并将其集成到 `AsciiLevelGenerator.cs` 的换肤流程中。

### 4.1 Tiled 渲染模式

对于地面、墙壁、平台等可重复贴图的地形元素，我们强制使用 `SpriteDrawMode.Tiled` 模式。
*   **原理**：Sprite 以原始尺寸（基于 PPU）平铺填满碰撞体区域。
*   **优势**：换素材时不会拉伸变形，保持像素完美。`transform.localScale` 始终锁定为 `(1,1,1)`。

### 4.2 自动挂载逻辑

在 `AsciiLevelGenerator.ApplyTheme()` 中，当为地形元素应用新主题时，系统会自动为其挂载 `SpriteAutoFit` 组件，并设置为 `Tiled` 模式。这确保了无论美术提供什么尺寸的素材，只要 PPU 设置正确，关卡的物理布局和视觉表现都能完美契合。

## 5. 半重力跳跃顶点 (Halved-Gravity Jump Peak)

参考《蔚蓝》的设计，我们在 `MarioController` 和 `TricksterController` 中引入了半重力跳跃顶点机制 [3]。

*   **触发条件**：当玩家长按跳跃键，且垂直速度的绝对值小于阈值（`apexThreshold = 2.0f`）时。
*   **效果**：重力加速度减半（`apexGravityMultiplier = 0.5f`）。
*   **目的**：使跳跃弧线在顶部变得更加平缓，给予玩家更多的空中滞留时间，以便更精确地调整落点。这不仅提升了操作手感，也让跳跃看起来更加优雅。

## 6. 跳跃抛物线可视化 (JumpArcVisualizer)

为了让关卡设计师（和 AI）在编辑器中直观地验证跳跃可行性，我们开发了 `JumpArcVisualizer.cs` 工具。

该工具挂载在玩家角色上，利用 `OnDrawGizmos` 在 Scene 视图中实时绘制跳跃轨迹：
*   **绿色弧线**：原地最高跳轨迹（垂直极限）。
*   **蓝色弧线**：极限远跳轨迹（水平极限）。
*   **黄色弧线**：短跳轨迹（立即松开跳跃键）。
*   **红色虚线**：最大高度和最大距离的标注线。

这些弧线精确模拟了控制器的物理逻辑（包括半重力顶点效果）。设计师只需在 Scene 视图中拖动角色，即可直观地判断前方的间隙或高台是否可跨越，无需频繁运行游戏进行测试。这极大地加速了"白盒锁死物理真相，一键换肤纯净包浆"的工业化管线。

## 7. ASCII 模板物理验证器 (AsciiLevelValidator)

为了在关卡生成前就发现物理死路，我们开发了 `AsciiLevelValidator.cs`。它在 `AsciiLevelGenerator.GenerateFromTemplate()` 中自动调用，对 ASCII 模板进行以下检查：

*   **Mario 出生点检查**：确认 `M` 字符存在且脚下有地面。
*   **终点检查**：确认 `G` 字符存在。
*   **水平间隙扫描**：检测连续空白间隙是否超过安全上限（`ASCII_MAX_GAP = 4`）或物理极限（`MAX_GAP_WITH_COYOTE = 5.85`）。
*   **垂直高台扫描**：检测需要跳跃才能到达的平台是否超过跳跃极限。
*   **踏脚石检查**：对于超过安全高度但未超过极限的高台，检查附近是否有可作为踏脚石的中间平台。

验证结果分三级：
*   **❌ ERROR**：物理死路（不可跨越的间隙、不可到达的平台）。关卡仍会生成，但会在 Console 中输出红色错误。
*   **⚠️ WARNING**：需要精确操作的极限跳跃。设计师可能是故意的，但需确认。
*   **ℹ️ INFO**：关卡尺寸、物理极限等参考信息。

## 8. 扩展指南：添加新元素类型

当需要添加新的关卡元素类型（如新陷阱、新平台、新敌人）时，请按以下 6 步操作：

| 步骤 | 文件 | 操作 |
|------|------|------|
| 1 | `PhysicsMetrics.cs` | 添加碰撞体常量 `NEW_ELEMENT_COLLIDER_SIZE` |
| 2 | `AsciiLevelGenerator.cs` | 在 `InitCharMap()` 中注册新字符映射 |
| 3 | `AsciiLevelGenerator.cs` | 添加 `SpawnNewElement()` 方法，碰撞体引用 PhysicsMetrics |
| 4 | `AsciiLevelValidator.cs` | 在 `solidChars` 或 `airChars` 中注册新字符 |
| 5 | `LevelThemeProfile.cs` | 在 `elementSprites` 中添加主题插槽 |
| 6 | `AI_PROMPT_WORKFLOW.md` | 更新 ASCII 字符表和使用示例 |

**设计原则**：
*   危险物碰撞体应小于视觉（宽容感）。
*   平台类碰撞体宽度应 >= 1.5 格（给玩家落脚空间）。
*   新增的跳跃能力修改必须同步更新 PhysicsMetrics 的跳跃极限常量。
*   碰撞体尺寸必须在 PhysicsMetrics 中定义，禁止在 Spawn 方法中硬编码。

## References

[1] Brazmogu. (2020). Physics for Game Dev: A Platformer Physics Cheatsheet. Medium.
[2] Hamaluik, Luke. (2019). Super Mario World Physics.
[3] Thorson, Maddy. (2019). Celeste and Forgiveness. Maddy Makes Games.
