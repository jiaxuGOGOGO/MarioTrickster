# MarioTrickster 关卡设计与 Level Studio 手册

> 本文档是 MarioTrickster 项目关卡设计的**一站式参考手册**。它涵盖三大核心内容：Level Studio（Test Console）的完整使用教程、经典 2D 平台跳跃关卡设计理论，以及项目当前的要素覆盖分析与扩展路线图。
> 
> 💡 **提示**：如果你只想从“策划怎么最快做关卡、替换商业素材、把新增机制交给 AI 后台承接”的角度上手，优先阅读 [**策划高速关卡生产指南**](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md)。

---

## 🛠️ 第一部分：Level Studio (Test Console) 详细教程

Level Studio 是用于快速构建、测试和迭代关卡的统一工作台。在 Unity 编辑器中，通过快捷键 `Ctrl+T` (Windows) 或 `Cmd+T` (Mac) 唤出，也可通过菜单栏 `MarioTrickster → Test Console` 打开。

该工具分为三大核心选项卡：**Level Builder**、**Teleport** 和 **Cheats**。

### 🧱 1. Level Builder & Theming（关卡构建与换肤）

此选项卡是关卡设计的核心区域，支持从白盒原型到最终换肤的完整工作流。

<details open>
<summary><b>1.1 内置模板快速生成</b></summary>

Level Studio 提供两个内置的 ASCII 模板用于快速搭建基础场景。

**使用方法**：确保处于 **EditMode**（非运行状态），在 "Template" 下拉菜单中选择内置模板（Classic Plains 或 Underground Cavern），点击绿色的 **Generate Whitebox Level** 按钮。系统会在场景中生成一个名为 `AsciiLevel_Root` 的父节点，并根据模板生成所有对应的白盒元素。若需清空当前生成的关卡，点击红色的 **Clear ASCII Level** 按钮。

</details>

<details open>
<summary><b>1.2 Custom Template Editor（自定义模板编辑器）</b></summary>

这是关卡设计主力工具，将三个功能整合在一个面板中，实现"打字即关卡"的极速工作流。

**三合一功能架构**：
| 功能模块 | 说明 |
|---------|------|
| **字典速查表** | 内嵌 25 种字符映射参考（始终可见），设计时随时查阅 |
| **经典片段库** | 多个预设片段，点击"追加到文本框"像搭积木一样拼装长关卡 |
| **文本框 + Build** | 粘贴外部 AI 生成的 ASCII / 手动编写 / 片段追加，一键生成关卡 |

**字符映射表（核心字典）**：
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
| `[` | 临时封路机关 | 半透明蓝 | `]` | 公开队列机关 | 橙红 |

> ⚠️ **排版铁律**：空气区域必须用点号 `.` 填充，绝对不能用空格。保持完美矩形网格。第一行 = 最高层，最后一行 = 最低层。

</details>

<details open>
<summary><b>1.3 动态元素调色板 (Element Palette)</b></summary>

在不编写完整 ASCII 模板的情况下，快速向场景中添加单个测试元素。在 Scene 视图中将摄像机移动到目标位置，在 Test Console 中展开 Element Palette，点击对应的元素按钮。元素将自动生成在 Scene 视图的画面中心（使用 `sceneView.pivot` 精确定位），并自动对齐到整数网格。

</details>

<details open>
<summary><b>1.4 主题换肤系统 (Theme System)</b></summary>

白盒逻辑验证通过后，可以一键将美术素材应用到关卡中，实现数据驱动的换肤。

**配置与使用方法**：
1. **创建主题**：在 Project 窗口右键 `Create → MarioTrickster → Level Theme Profile`，或点击 Test Console 中的 **Create New Theme** 按钮。
2. **配置主题**：在 Inspector 中选中该 Profile，为不同的元素拖入对应的 Sprite 素材。支持的元素键名包括：SpikeTrap、FireTrap 等。如果某个插槽留空，该元素将保持白盒状态，不会报错。
3. **应用主题**：将配置好的 Profile 拖入 Test Console 的 "Theme Profile" 槽位，点击 **Apply Theme (with Undo)**。系统会自动遍历并替换材质。支持 `Ctrl+Z` 撤销。

</details>

---

### 🚀 2. Teleport & Reset（传送与状态管理）

此选项卡专为 PlayMode 下的快速迭代设计，大幅减少测试跑图时间。

| 功能 | 说明 |
|------|------|
| **Stage Quick Teleport** | 点击 Stage 1~9 + GoalZone 按钮，瞬间传送 Mario 和 Trickster 到对应测试区域。传送后相机硬切（SnapToTarget） |
| **Custom Teleport** | 输入自定义 X/Y 坐标并点击 "Go" 进行精准传送 |
| **Revive Mario** | 满血复活 Mario |
| **Refill Energy** | 补满 Trickster 的能量 |
| **Reset Elements** | 重置关卡中所有可交互元素（恢复崩塌平台、重置陷阱状态等） |

---

### 🕹️ 3. Global Cheats（全局测试外挂）

提供各种修改器功能，用于在不考虑资源限制的情况下测试机制。

| 分类 | 选项 | 功能说明 |
|------|------|----------|
| **Mario** | God Mode | 免疫所有伤害（受击不掉血，但仍会被击退） |
| | Infinite Scan | 扫描技能无冷却时间 |
| **Trickster** | Infinite Energy | 能量锁定在 100，使用技能不消耗 |
| | No Cooldowns | 伪装变身和道具操控无冷却时间 |
| | Instant Possess | 伪装融入场景无需等待 1.5 秒，立即生效 |
| **Global** | Time Scale | 调整游戏运行速度 (0.1x - 3.0x)，用于慢动作观察或加速跳过等待 |

> ℹ️ **注意**：如果当前场景缺少相关的管理器组件，Cheats 选项会被置灰，并显示 `Auto-Fix: Inject Playable Environment` 按钮。点击该按钮可一键自动修复场景依赖。
