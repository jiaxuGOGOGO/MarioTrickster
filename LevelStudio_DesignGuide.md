# MarioTrickster Level Studio 教程与关卡设计指南

本文档旨在为 MarioTrickster 项目的关卡设计师和开发者提供详尽的 **Test Console (Level Studio)** 使用教程，并结合经典的 2D 平台跳跃游戏设计理论，提供一套可操作的关卡设计与改进指南。

---

## 第一部分：Level Studio (Test Console) 详细教程

Level Studio 是 MarioTrickster 项目中用于快速构建、测试和迭代关卡的统一工作台。在 Unity 编辑器中，可以通过快捷键 `Ctrl+T` (Windows) 或 `Cmd+T` (Mac) 唤出，或者通过菜单栏 `MarioTrickster → Test Console` 打开。

该工具分为三大核心选项卡：**Level Builder**、**Teleport** 和 **Cheats**。

### 1. Level Builder & Theming (关卡构建与换肤)

此选项卡是关卡设计的核心区域，支持从白盒原型到最终换肤的完整工作流。

#### 1.1 ASCII 关卡模板生成器 (ASCII Level Generator)
**功能目的**：允许设计师通过简单的文本字符快速生成关卡白盒原型，专注于测试跳跃距离、陷阱时机等核心逻辑，而不被美术细节干扰。

**使用方法**：
- 确保处于 **EditMode**（非运行状态）。
- 在 "Template" 下拉菜单中选择内置模板（如 Classic Plains 或 Underground Cavern）。
- 点击绿色的 **Generate Whitebox Level** 按钮。系统会在场景中生成一个名为 `AsciiLevel_Root` 的父节点，并根据模板生成所有对应的白盒元素。
- 若需清空当前生成的关卡，点击红色的 **Clear ASCII Level** 按钮。

**支持的字符映射表**：
| 字符 | 元素类型 | 颜色标识 | 字符 | 元素类型 | 颜色标识 |
|:---:|:---|:---|:---:|:---|:---|
| `#` | 实心地面 (Ground) | 中灰 | `B` | 弹跳平台 (Bouncy) | 绿色 |
| `=` | 平台 (Platform) | 浅灰 | `C` | 崩塌平台 (Collapse) | 土黄 |
| `W` | 墙壁 (Wall) | 深灰 | `-` | 单向平台 (OneWay) | 浅蓝 |
| `M` | Mario 出生点 | 红色 | `>` | 移动平台 (Moving) | 蓝紫 |
| `T` | Trickster 出生点 | 紫色 | `E` | 弹跳怪 (BounceEnemy) | 粉红 |
| `G` | 终点 (GoalZone) | 亮绿 | `e` | 简单敌人 (SimpleEnemy) | 粉红 |
| `^` | 地刺 (SpikeTrap) | 红色 | `F` | 伪装墙 (FakeWall) | 蓝灰 |
| `~` | 火焰陷阱 (FireTrap) | 橙色 | `H` | 隐藏通道 (Passage) | 青绿 |
| `P` | 摆锤 (Pendulum) | 棕色 | `o` | 收集物 (Coin) | 金色 |

#### 1.2 动态元素调色板 (Element Palette)
**功能目的**：在不编写完整 ASCII 模板的情况下，快速向场景中添加单个测试元素。

**使用方法**：
- 在 Scene 视图中，将摄像机移动到你希望放置元素的位置。
- 在 Test Console 中展开 **Element Palette**。
- 点击对应的元素按钮（如 "Spike Trap" 或 "Bouncy"）。
- 元素将自动生成在 Scene 摄像机中心，并自动对齐到整数网格。生成后可手动拖拽微调。

#### 1.3 主题换肤系统 (Theme System)
**功能目的**：在白盒逻辑验证通过后，一键将美术素材应用到关卡中，实现数据驱动的换肤。

**配置与使用方法**：
1. **创建主题**：点击 **Create New Theme** 按钮，选择保存路径，生成一个 `LevelThemeProfile` 资产文件。
2. **配置主题**：在 Inspector 中选中该 Profile，为不同的元素（如 Ground, SpikeTrap, BouncyPlatform）拖入对应的 Sprite 素材。如果某个插槽留空，该元素将保持白盒状态，不会报错。
3. **应用主题**：将配置好的 Profile 拖入 Test Console 的 "Theme Profile" 槽位，点击 **Apply Theme (with Undo)**。系统会自动遍历 `AsciiLevel_Root` 下的所有元素并替换材质。支持 `Ctrl+Z` 撤销。

### 2. Teleport & Reset (传送与状态管理)

此选项卡专为 PlayMode 下的快速迭代设计，大幅减少测试跑图时间。

**核心功能**：
- **Stage Quick Teleport**：点击对应的 Stage 按钮（如 "Stage 4: Disguise System"），可将 Mario 和 Trickster 瞬间传送到该测试区域。**注意**：传送后相机会执行硬切（SnapToTarget），不会缓慢滑动，确保立即进入测试状态。
- **Custom Teleport**：输入自定义的 X/Y 坐标并点击 "Go" 进行精准传送。
- **Quick Actions**：
  - **Revive Mario**：满血复活 Mario。
  - **Refill Energy**：补满 Trickster 的能量。
  - **Reset Elements**：重置关卡中所有可交互元素（如恢复崩塌平台、重置陷阱状态）。

### 3. Global Cheats (全局测试外挂)

此选项卡提供了一系列强大的调试开关，帮助开发者绕过限制，专注于特定功能的测试。

**核心开关**：
- **God Mode (无敌)**：Mario 不扣血、不死亡。
- **No Cooldown (无冷却)**：Trickster 的伪装、扫描、道具操控技能冷却时间清零。
- **Infinite Energy (无限能量)**：Trickster 能量锁定为满值。
- **Instant Blend (秒速融入)**：Trickster 伪装后无需等待，立即进入完全融入状态。
- **Time Scale**：通过滑动条或快捷按钮（0.1x ~ 3.0x）调整游戏全局速度，便于观察复杂的物理碰撞或快速跳过等待。

*注：所有作弊开关默认关闭，每次进入 PlayMode 自动重置，且被宏隔离，不会影响自动化测试或发布版本。*

---

## 第二部分：关卡设计理论与参考

为了充分利用 Level Studio 构建引人入胜的关卡，我们整理了业界顶尖 2D 平台跳跃游戏（如《超级马里奥》系列和《蔚蓝 Celeste》）的设计最佳实践。

### 1. 核心设计方法论：GMTK 四步法

任天堂在《超级马里奥 3D 世界》等作品中确立了经典的四步关卡设计法 [1]。在设计 MarioTrickster 的关卡时，应遵循这一节奏：

1. **Introduce（引入）**：在一个绝对安全的环境中向玩家展示新机制（如弹跳平台）。即使玩家操作失误，也不会受到惩罚。
2. **Develop（发展）**：提供第一个真正的挑战。玩家需要运用刚刚学到的机制来跨越障碍或击败敌人。
3. **Twist（变化）**：为机制增加变数。例如，将弹跳平台与火焰陷阱结合，或者要求在移动平台上进行弹跳。
4. **Conclude（总结）**：关卡的高潮部分，通常是一次综合性的终极考验，要求玩家完美掌握该机制。

### 2. 关卡节奏与难度曲线

优秀的平台跳跃游戏不会让难度呈直线上升，而是采用波浪式的节奏 [2]。
- **安全区与缓冲**：在经历了一次高强度的 "Twist" 挑战后，应提供一段相对平静的区域，放置一些收集物（金币），让玩家在心理上得到放松。
- **非对称设计的特殊性**：在 MarioTrickster 中，由于 Trickster 玩家的存在，关卡的动态难度极高。因此，**基础地形设计必须比传统的单人马里奥游戏更宽容**。如果地形本身已经极度硬核，Trickster 的任何干扰都会导致关卡无解。

### 3. 视觉引导与直觉设计

- **金币的引导作用**：金币不仅是收集物，更是设计师留给玩家的"隐形路径"。在需要盲跳（Leap of Faith）的地方，用金币标示出安全的抛物线轨迹 [3]。
- **不对称美学**：避免设计完全对称的房间或结构。不对称的关卡不仅在视觉上更自然，在游玩路线上也更具探索感 [2]。

---

## 第三部分：项目要素分析与改进建议

通过对 MarioTrickster 当前代码库的全局检索（`LevelElementBase` 及 `ControllableLevelElement`），我们对比了经典平台跳跃游戏的要素清单 [4]，得出了当前项目的要素覆盖情况及未来扩展建议。

### 1. 当前已实现的要素库

项目目前已具备非常扎实的基础框架，涵盖了 20 种核心元素：
- **地形类**：地面、平台、墙壁、单向平台、移动平台。
- **陷阱类**：地刺、火焰陷阱、摆锤。
- **互动平台**：弹跳平台、崩塌平台。
- **敌人类**：简单巡逻敌人、弹跳怪。
- **隐藏与欺骗**：伪装墙、隐藏通道。
- **其他**：收集物（金币）、终点区域。

### 2. 缺失的经典要素识别（建议新增）

为了进一步提升关卡的丰富度和 Trickster 的策略空间，建议在后续 Sprint 中优先实现以下机制：

#### 高优先级（显著扩展核心玩法）
1. **传送带 (Conveyor Belts)**：改变玩家的地面移动速度。Trickster 如果能操控传送带的方向，将产生极佳的干扰效果。
2. **旋转锯片/激光 (Rotating Saws/Lasers)**：经典的周期性致命障碍。相比于静态地刺，动态的锯片对玩家的跳跃时机要求更高。
3. **飞行敌人 (Flying Enemies)**：目前项目中的敌人（SimpleEnemy, BouncingEnemy）均受重力影响。缺乏如帕拉栗子（Paragoomba）或炮弹飞箱（Bullet Bill）这样能对空中 Mario 构成威胁的元素。
4. **水域/液体环境 (Water/Lava)**：改变物理规则的区域。水下关卡会改变跳跃手感，而岩浆则是经典的即死区域。

#### 中优先级（增加解谜与探索深度）
5. **钥匙与锁门 (Keys & Locked Doors)**：强制玩家探索关卡的特定区域，打破纯线性的过关流程。
6. **冰面地形 (Ice Surfaces)**：低摩擦力地面，增加移动惯性，考验玩家的微操。
7. **检查点 (Checkpoints)**：随着关卡长度的增加，中途存档点将变得不可或缺，以降低玩家的挫败感。
8. **可破坏方块 (Destructible Blocks)**：允许 Mario 通过顶撞或下落砸碎的方块，可用于隐藏道具或开辟新路径。

### 3. 基于 Level Studio 的扩展工作流

得益于 `AsciiLevelGenerator` 的字典映射设计，新增上述要素的开发成本极低。以新增"传送带"为例，工作流如下：
1. 编写 `ConveyorBelt` 脚本，继承 `ControllableLevelElement`。
2. 在 `AsciiLevelGenerator.InitCharMap()` 中添加一行映射，例如：`charMap['<'] = SpawnConveyorBelt;`。
3. 在 `LevelThemeProfile` 的 `elementSprites` 数组中添加 `elementKey = "ConveyorBelt"` 的配置项。
4. 设计师即可立即在 Level Studio 中使用 `<` 字符进行关卡搭建和换肤。

---

## 参考文献

[1] Game Developer. "Super Mario Bros. 3 Level Design Lessons, Part 1". https://www.gamedeveloper.com/design/super-mario-bros-3-level-design-lessons-part-1
[2] Tadeas Jun. "How to design breathtaking 2D platformer levels". https://www.tadeasjun.com/blog/2d-level-design/
[3] Pinnguaq. "Mario Maker Level Design Basics". https://pinnguaq.com/learn/mario-maker-level-design-basics/
[4] GitHub Gist. "Collection of obstacle ideas for platformer games". https://gist.github.com/nezvers/65d00452743708f8dadd43a09cc96eea
