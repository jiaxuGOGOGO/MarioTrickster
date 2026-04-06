# 关卡设计系统架构 (Level Design System)

本文档定义了 MarioTrickster 项目的关卡设计系统架构，包括新陷阱、平台、道具和隐藏通道的设计规范，以及它们与 Trickster 机制的联动方式。

## 1. 核心设计理念

关卡设计系统旨在打破传统平台跳跃游戏的静态关卡限制，引入动态的心理博弈和实时策略对抗。通过丰富的关卡元素和 Trickster 的操控机制，每一局游戏都将呈现独特的体验。

### 1.1 渐进式难度与节奏控制
- **教学区**：在安全环境中引入新机制（如单向平台、地刺）。
- **挑战区**：组合多种机制（如移动平台 + 摆锤陷阱），增加难度。
- **缓冲区**：在连续高强度挑战后提供安全区域，放置收集物或隐藏通道入口。

### 1.2 视觉提示与探索奖励
- **视觉线索**：使用颜色、动画或特效暗示危险区域或隐藏通道。
- **探索奖励**：在隐藏路径或高难度区域放置高价值收集物（如额外生命、无敌星）。

## 2. 关卡元素分类与实现规范

### 2.1 平台系统 (Platforms)

平台是关卡的基础结构，提供移动和跳跃的支撑。

| 平台类型 | 描述 | 实现方式 | Trickster 联动 |
| :--- | :--- | :--- | :--- |
| **固体平台 (Solid)** | 静态的地面或墙壁。 | `BoxCollider2D` | 可伪装，不可操控。 |
| **移动平台 (Moving)** | 在两点间往复移动。 | `MovingPlatform.cs` | 可伪装，可操控（改变方向/速度）。 |
| **可破坏平台 (Breakable)** | 被 Mario 顶撞后破坏。 | `Breakable.cs` | 可伪装，不可操控。 |
| **弹跳平台 (Bouncy)** | 玩家踩上后被弹射到高处。 | 新增 `BouncyPlatform.cs` | 可伪装，可操控（改变弹力方向/大小）。 |
| **单向平台 (One-Way)** | 玩家可从下方穿过，落在上方。 | 新增 `OneWayPlatform.cs` | 可伪装，可操控（临时变为固体或取消碰撞）。 |
| **崩塌平台 (Collapsing)** | 玩家踩上后延迟消失。 | 新增 `CollapsingPlatform.cs` | 可伪装，可操控（提前触发崩塌或延长延迟）。 |

### 2.2 陷阱与危险物 (Traps & Hazards)

陷阱是关卡中的主要威胁，要求玩家具备良好的反应和时机把握能力。

| 陷阱类型 | 描述 | 实现方式 | Trickster 联动 |
| :--- | :--- | :--- | :--- |
| **地刺 (Spike Trap)** | 静态或周期性伸缩的尖刺。 | 新增 `SpikeTrap.cs` | 可伪装，可操控（强制伸出/缩回）。 |
| **摆锤绳索 (Pendulum)** | 像糖秋千一样周期性摆动的危险物。 | 新增 `PendulumTrap.cs` | 可伪装，可操控（改变摆动方向/速度）。 |
| **弹跳小怪物 (Bouncing Enemy)** | 原地弹跳的简单敌人。 | 新增 `BouncingEnemy.cs` | 可伪装，可操控（改变弹跳高度/频率）。 |
| **火焰陷阱 (Fire Trap)** | 周期性喷射火焰。 | 新增 `FireTrap.cs` | 可伪装，可操控（强制喷火/停止）。 |

### 2.3 隐藏通道 (Hidden Passages)

隐藏通道鼓励玩家探索，提供捷径或额外奖励。

| 通道类型 | 描述 | 实现方式 | Trickster 联动 |
| :--- | :--- | :--- | :--- |
| **地下隐藏通道 (Underground)** | 类似马里奥的水管，进入后传送到隐藏区域。 | 新增 `HiddenPassage.cs` | 可伪装，可操控（临时关闭入口或改变传送目标）。 |
| **伪装墙壁 (Fake Wall)** | 看起来是墙壁，但玩家可以穿过。 | 新增 `FakeWall.cs` | 可伪装，可操控（变为真实墙壁）。 |

### 2.4 道具与收集物 (Collectibles)

道具提供增益效果，收集物用于计分或解锁内容。

| 道具类型 | 描述 | 实现方式 | Trickster 联动 |
| :--- | :--- | :--- | :--- |
| **金币 (Coin)** | 基础收集物，用于计分。 | `Collectible.cs` | 可伪装，不可操控。 |
| **血包 (HealthPack)** | 恢复生命值。 | `Collectible.cs` | 可伪装，不可操控。 |
| **加速靴 (SpeedBoost)** | 临时增加移动速度。 | 扩展 `Collectible.cs` | 可伪装，不可操控。 |
| **无敌星 (Invincibility)** | 临时无敌状态。 | 扩展 `Collectible.cs` | 可伪装，不可操控。 |
| **钥匙 (Key)** | 用于开启特定的门或隐藏通道。 | 新增 `Key.cs` | 可伪装，可操控（移动位置）。 |

## 3. 关卡参数调整入口导航 (Parameter Tuning Entry Points)

本节梳理项目中调整关卡参数的所有入口，按**集中度从高到低**分为四个层级。设计师可根据需要修改的参数类型快速定位到对应入口。

### 3.1 顶层入口：Level Studio (Test Console)

**唤出方式**：Unity 菜单栏 `MarioTrickster → Test Console`，或快捷键 `Ctrl+T` (Windows) / `Cmd+T` (Mac)。

Level Studio 是项目唯一的集中式关卡构建工具，所有关卡生成操作都从这里发起。它本身不直接暴露移动/旋转/缩放的数值滑块，而是通过以下三个子入口间接控制关卡布局和元素参数。

| 子入口 | 控制范围 | 操作方式 |
| :--- | :--- | :--- |
| **内置模板 / Custom Template Editor** | 所有元素的**位置布局**（网格坐标）、元素种类选择 | 在 ASCII 文本框中编辑字符矩阵，点击 Build From Text 一键生成。改位置优先改文本，不要在 Scene 里拖白盒 |
| **Element Palette（元素调色板）** | 单个元素的**快速生成与摆放** | 点击按钮在 Scene 视图中心生成白盒元素，然后在 Scene 中手动拖拽到目标位置 |
| **Theme System（主题换肤）** | 所有元素的**视觉外观** | 拖入 LevelThemeProfile 资产，点击 Apply Theme 一键替换 Sprite |

### 3.2 全局物理常量：PhysicsMetrics.cs

**文件路径**：`Assets/Scripts/LevelDesign/PhysicsMetrics.cs`

这是整个关卡系统的**物理真相源**，定义了所有碰撞体尺寸、跳跃能力极限和安全约束。任何生成器和编辑器都必须引用此处常量，禁止在其他文件中硬编码物理尺寸。

| 参数类别 | 常量示例 | 说明 |
| :--- | :--- | :--- |
| 网格基准 | `CELL_SIZE = 1f` | 每个 ASCII 字符对应的世界单位，**绝对不可修改** |
| 角色碰撞体 | `MARIO_COLLIDER_WIDTH/HEIGHT` | Mario/Trickster 的碰撞体尺寸（小于视觉 Sprite，提供宽容感） |
| 元素碰撞体 | `BOUNCY_COLLIDER_SIZE`, `MOVING_COLLIDER_SIZE`, `SPIKE_COLLIDER_SIZE` 等 | 各类关卡元素的标准碰撞体尺寸 |
| 跳跃极限 | `MAX_JUMP_HEIGHT = 2.5f`, `MAX_JUMP_DISTANCE = 4.5f` | 基于 MarioController 物理参数公式演算的真实极限 |
| 安全约束 | `ASCII_MAX_GAP = 4`, `BOUNCE_CLEARANCE = 3` | 供 ASCII 验证器和模板设计使用的安全边界 |

> **注意**：修改 MarioController 的 `jumpPower` / `fallAcceleration` / `maxSpeed` 后，必须同步更新 PhysicsMetrics 中的跳跃极限常量。

### 3.3 运行时行为参数：各元素脚本的 Inspector 字段

元素生成后，其**运行时行为参数**（如移动速度、弹跳力度、伤害值、周期时间等）分散在各自的组件脚本中，通过 Unity Inspector 面板调整。以下按类别列出所有可调参数入口。

#### 3.3.1 移动 / 旋转 / 缩放（Transform 相关）

项目中没有专门的"移动/旋转/缩放"集中面板。Transform 调整遵循以下规则：

| 调整目标 | 入口 | 操作方式 |
| :--- | :--- | :--- |
| **元素位置（Position）** | ASCII 文本模板 或 Scene 视图 | 优先在 ASCII 文本中改字符位置；Element Palette 生成的元素可在 Scene 中拖拽 |
| **元素旋转（Rotation）** | Scene 视图 Inspector | 选中元素后在 Inspector 的 Transform 组件中修改 Rotation |
| **视觉缩放（Visual Scale）** | `Visual` 子节点的 `localScale` | 根物体 `localScale` 必须永远是 `(1,1,1)`；视觉缩放由 `Visual` 子节点承担（S37 视碰分离架构） |
| **碰撞体尺寸** | `PhysicsMetrics.cs` | 碰撞体尺寸统一在 PhysicsMetrics 中定义，禁止在 Inspector 中手动修改 |

> **S37 视碰分离铁律**：所有形变动画（Squash/Stretch）只操作 `visualTransform.localScale`，绝对不要修改根物体的 `transform.localScale`。朝向翻转统一使用 `spriteRenderer.flipX`。

#### 3.3.2 弹跳平台高度（BouncyPlatform.cs）

**文件路径**：`Assets/Scripts/LevelElements/Platforms/BouncyPlatform.cs`

弹跳平台是参数最丰富的元素之一，在 Inspector 中选中弹跳平台对象即可调整：

| Inspector 字段 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `bounceForce` | 22 | 基础弹射力度（直接决定弹跳高度） |
| `bounceForceMultiplier` | 1.0 | 弹射力倍率（用于微调） |
| `minBounceForce` | 10 | 最小弹射力 |
| `maxBounceForce` | 50 | 最大弹射力 |
| `superBounceMultiplier` | 1.4 | 按住跳跃键时的大跳倍率 |
| `positionInfluence` | 0.2 | 位置偏移对弹射方向的影响权重（0=纯法线，1=纯位置） |
| `comedyDelay` | 0.15s | 碰撞后蓄力冻结时间 |
| `squashAmount` / `stretchOvershoot` | 0.5 / 1.3 | 挤压拉伸动画参数 |

#### 3.3.3 移动平台（MovingPlatform.cs）

**文件路径**：`Assets/Scripts/Core/MovingPlatform.cs`

| Inspector 字段 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `pointB` | (5, 0, 0) | 终点相对于起点的偏移向量（决定移动方向和距离） |
| `moveSpeed` | 2 | 移动速度 |
| `waitTime` | 0.5s | 到达端点后的等待时间 |
| `startFromB` | false | 是否从 B 点出发 |

#### 3.3.4 陷阱类参数

| 陷阱 | 文件路径 | 核心 Inspector 字段 |
| :--- | :--- | :--- |
| **地刺** | `LevelElements/Traps/SpikeTrap.cs` | `mode`(Static/Periodic), `damage`, `extendedDuration`, `retractedDuration`, `transitionSpeed`, `knockbackForce/UpForce` |
| **火焰** | `LevelElements/Traps/FireTrap.cs` | `damage`, `fireDirection`, `fireLength`, `warmupDuration`, `fireDuration`, `coolOffDuration`, `knockbackForce/UpForce` |
| **摆锤** | `LevelElements/Traps/PendulumTrap.cs` | `ropeLength`, `swingSpeed`, `maxAngle`, `damage`, `hammerRadius`, `knockbackForce/UpForce` |
| **弹跳怪** | `LevelElements/Traps/BouncingEnemy.cs` | `bounceForce`, `bounceInterval`, `horizontalDrift`, `contactDamage`, `canBeStomped`, `stompBounceForce`, `maxHealth` |

#### 3.3.5 其他平台类参数

| 平台 | 文件路径 | 核心 Inspector 字段 |
| :--- | :--- | :--- |
| **崩塌平台** | `LevelElements/Platforms/CollapsingPlatform.cs` | `collapseDelay`, `respawnDelay`, `canRespawn`, `shakeIntensity`, `shakeFrequency` |
| **单向平台** | `LevelElements/Platforms/OneWayPlatform.cs` | `dropThroughDuration` |

#### 3.3.6 隐藏通道与伪装墙参数

| 元素 | 文件路径 | 核心 Inspector 字段 |
| :--- | :--- | :--- |
| **隐藏通道** | `LevelElements/HiddenPassages/HiddenPassage.cs` | `exitPoint`, `isBidirectional`, `teleportDelay`, `teleportCooldown`, `visibility`, `hintDistance`, `requiresKey` |
| **伪装墙** | `LevelElements/HiddenPassages/FakeWall.cs` | `revealAlpha`, `fadeSpeed` |

#### 3.3.7 角色控制参数

| 角色 | 文件路径 | 核心 Inspector 字段 |
| :--- | :--- | :--- |
| **Mario** | `Player/MarioController.cs` | `maxSpeed`(9), `acceleration`(160), `groundDeceleration`(200), `jumpPower`(20), `fallAcceleration`(80), `maxFallSpeed`(40), `jumpEndEarlyGravityModifier`(3), `coyoteTime`(0.15), `jumpBuffer`(0.2), `apexThreshold`(2.0), `apexGravityMultiplier`(0.5) |
| **Trickster** | `Enemy/TricksterController.cs` | `maxSpeed`(8), `acceleration`(140), `jumpPower`(18), `disguisedMoveMultiplier`(0.15), `canJumpWhileDisguised`(false) |

#### 3.3.8 相机与全局系统参数

| 系统 | 文件路径 | 核心 Inspector 字段 |
| :--- | :--- | :--- |
| **相机** | `Camera/CameraController.cs` | `smoothTime`(0.2), `offset`(0,1,-10), `lookAheadDistance`(1.5), `useBounds`, `minX/maxX/minY/maxY` |
| **死亡区域** | `Core/KillZone.cs` | `damage`(999) |
| **可破坏方块** | `Core/Breakable.cs` | `blockType`, `hitsToBreak`, `bumpHeight`, `bumpSpeed` |
| **收集物** | `Core/Collectible.cs` | `type`, `value`, `bobUpDown`, `bobSpeed`, `bobHeight` |

### 3.4 参数调整工作流总结

以下是推荐的参数调整优先级和工作流：

```
┌─────────────────────────────────────────────────────────────────────┐
│  第 1 步：改布局 → ASCII 文本（Level Studio → Custom Template）     │
│  第 2 步：改物理尺寸 → PhysicsMetrics.cs（全局碰撞体常量）          │
│  第 3 步：改行为参数 → 选中元素 → Inspector（各脚本 SerializeField）│
│  第 4 步：改视觉外观 → Theme System（LevelThemeProfile 换肤）       │
└─────────────────────────────────────────────────────────────────────┘
```

> **核心原则**：布局问题回文本改，物理尺寸回 PhysicsMetrics 改，行为参数回 Inspector 改。三者职责分明，互不越界。

## 4. 系统集成与扩展

### 4.1 关卡管理器 (LevelManager)
`LevelManager` 将扩展以支持更多关卡元数据：
- 注册所有陷阱、平台和隐藏通道。
- 管理关卡状态（如重置、检查点）。
- 提供全局查询接口供 Trickster 技能系统使用。

### 4.2 Trickster 技能系统 (TricksterAbilitySystem)
新加入的关卡元素（如地刺、摆锤）必须实现 `IControllableProp` 接口或继承 `ControllablePropBase`，以便 Trickster 能够发现并操控它们。

### 4.3 测试场景构建器 (TestSceneBuilder)
`TestSceneBuilder` 将更新以自动生成包含新陷阱、平台和隐藏通道的测试场景，确保新元素的快速验证。

## 5. 后续开发计划
1. **Sprint 2**：实现基础陷阱（地刺、摆锤）和新平台（弹跳、单向）。
2. **Sprint 3**：实现隐藏通道和高级道具（钥匙、无敌星）。
3. **Sprint 4**：完善关卡编辑器工具，支持可视化配置。
