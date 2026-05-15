# MarioTrickster Level Studio 教程与关卡设计指南

本文档是 MarioTrickster 项目关卡设计的**一站式参考手册**。它涵盖三大核心内容：Level Studio（Test Console）的完整使用教程、经典 2D 平台跳跃关卡设计理论，以及项目当前的要素覆盖分析与扩展路线图。文档已根据 Session 26b 至 Session 29 的全部功能迭代进行了同步更新。

如果你只想从“策划怎么最快做关卡、替换商业素材、把新增机制交给 AI 后台承接”的角度上手，优先阅读 [**策划高速关卡生产指南**](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md)。本文件继续作为更完整的 Level Studio 深度手册。

---

## 第一部分：Level Studio (Test Console) 详细教程

Level Studio 是 MarioTrickster 项目中用于快速构建、测试和迭代关卡的统一工作台。在 Unity 编辑器中，可以通过快捷键 `Ctrl+T` (Windows) 或 `Cmd+T` (Mac) 唤出，也可通过菜单栏 `MarioTrickster → Test Console` 打开。该工具分为三大核心选项卡：**Level Builder**、**Teleport** 和 **Cheats**。

### 1. Level Builder & Theming（关卡构建与换肤）

此选项卡是关卡设计的核心区域，支持从白盒原型到最终换肤的完整工作流。

#### 1.1 内置模板快速生成

Level Studio 提供两个内置的 ASCII 模板用于快速搭建基础场景：

**使用方法**：确保处于 **EditMode**（非运行状态），在 "Template" 下拉菜单中选择内置模板（Classic Plains 或 Underground Cavern），点击绿色的 **Generate Whitebox Level** 按钮。系统会在场景中生成一个名为 `AsciiLevel_Root` 的父节点，并根据模板生成所有对应的白盒元素。若需清空当前生成的关卡，点击红色的 **Clear ASCII Level** 按钮。

#### 1.2 Custom Template Editor（自定义模板编辑器 — 三合一核心工具）

这是 S26b 精简后的关卡设计主力工具，将三个功能整合在一个面板中，实现"打字即关卡"的极速工作流。

**三合一功能架构**：

| 功能模块 | 说明 |
|---------|------|
| **字典速查表** | 内嵌 18 种字符映射参考（始终可见），设计时随时查阅 |
| **经典片段库** | 5 个预设片段，点击"追加到文本框"像搭积木一样拼装长关卡 |
| **文本框 + Build** | 粘贴外部 AI 生成的 ASCII / 手动编写 / 片段追加，一键生成关卡 |

**字符映射表（完整字典）**：

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
| `.` | 空气 (Air) | 不生成 | | | |

**经典片段库（5 个核心片段）**：

| 片段名称 | 设计说明 | 适合位置 |
|---------|---------|---------|
| Tutorial Start（教学起点） | 安全起点 → 小跳跃 → 第一个敌人 | 关卡最左侧 |
| Bounce Abyss（弹跳深渊） | 无地面的弹跳平台连跳，掉落即死 | 中段高难度区 |
| Trap Corridor（陷阱走廊） | 地刺+火焰+摆锤组合，需要观察节奏 | 中段考验区 |
| Enemy Gauntlet（敌人混战） | 多层平台上的巡逻怪+弹跳怪 | 中段战斗区 |
| Final Sprint（终点冲刺） | 崩塌平台+移动平台的紧张冲刺 | 关卡末尾 |

**操作流程**：

1. 展开 Custom Template Editor 面板。
2. 从片段库中点击"追加到文本框"，或者直接在文本框中粘贴外部 AI 生成的 ASCII 矩阵。
3. 文本框下方会实时显示当前模板的尺寸（宽 x 高）。
4. 点击绿色的 **Build From Text** 按钮，关卡骨架瞬间生成。
5. 支持"从剪贴板粘贴"按钮一键导入，以及"清空"按钮重置。

> **排版铁律**：空气区域必须用点号 `.` 填充，绝对不能用空格。保持完美矩形网格。第一行 = 最高层，最后一行 = 最低层。

#### 1.3 动态元素调色板 (Element Palette)

在不编写完整 ASCII 模板的情况下，快速向场景中添加单个测试元素。在 Scene 视图中将摄像机移动到目标位置，在 Test Console 中展开 Element Palette，点击对应的元素按钮。元素将自动生成在 Scene 视图的画面中心（使用 `sceneView.pivot` 精确定位），并自动对齐到整数网格。

#### 1.4 主题换肤系统 (Theme System)

白盒逻辑验证通过后，可以一键将美术素材应用到关卡中，实现数据驱动的换肤。

**配置与使用方法**：

1. **创建主题**：在 Project 窗口右键 `Create → MarioTrickster → Level Theme Profile`，或点击 Test Console 中的 **Create New Theme** 按钮。
2. **配置主题**：在 Inspector 中选中该 Profile，为不同的元素拖入对应的 Sprite 素材。支持的元素键名包括：SpikeTrap、FireTrap、PendulumTrap、BouncingEnemy、BouncyPlatform、CollapsingPlatform、OneWayPlatform、MovingPlatform、HiddenPassage、FakeWall、GoalZone、Collectible、SimpleEnemy。如果某个插槽留空，该元素将保持白盒状态，不会报错（null-safe 回退机制）。
3. **应用主题**：将配置好的 Profile 拖入 Test Console 的 "Theme Profile" 槽位，点击 **Apply Theme (with Undo)**。系统会自动遍历 `AsciiLevel_Root` 下的所有元素并替换材质。支持 `Ctrl+Z` 撤销。

### 2. Teleport & Reset（传送与状态管理）

此选项卡专为 PlayMode 下的快速迭代设计，大幅减少测试跑图时间。

**核心功能**：

| 功能 | 说明 |
|------|------|
| Stage Quick Teleport | 点击 Stage 1~9 + GoalZone 按钮，瞬间传送 Mario 和 Trickster 到对应测试区域。传送后相机硬切（SnapToTarget） |
| Custom Teleport | 输入自定义 X/Y 坐标并点击 "Go" 进行精准传送 |
| Revive Mario | 满血复活 Mario |
| Refill Energy | 补满 Trickster 的能量 |
| Reset Elements | 重置关卡中所有可交互元素（恢复崩塌平台、重置陷阱状态等） |

### 3. Global Cheats（全局测试外挂）

此选项卡提供一系列调试开关，帮助开发者绕过限制，专注于特定功能的测试。

| 开关 | 功能 |
|------|------|
| God Mode（无敌） | Mario 不扣血、不死亡 |
| No Cooldown（无冷却） | Trickster 的伪装、扫描、道具操控技能冷却时间清零 |
| Infinite Energy（无限能量） | Trickster 能量锁定为满值 |
| Instant Blend（秒速融入） | Trickster 伪装后无需等待，立即进入完全融入状态 |
| Time Scale | 滑动条或快捷按钮（0.1x ~ 3.0x）调整游戏全局速度 |

> 所有作弊开关默认关闭，每次进入 PlayMode 自动重置，且被 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 宏隔离，不会影响 114 个自动化测试或发布版本。

---

## 第二部分：关卡设计工作流（双路线架构）

MarioTrickster 采用**双路线关卡生产流水线**，实现从灵感搜集到 Unity 白盒测试的全链路闭环。两条路线共享同一套防重机制和 Unity 实操阶段，区别仅在于灵感来源的获取方式。

### 工作流全景

```
路线A: 开新对话 → AI 读防重库 → 全网搜集灵感 → 转译 ASCII → 写入模板 → 更新登记簿 → Git Push
路线B: 截图 → 复制防重摘要 → 发给网页 AI → 拿到 ASCII → 手动更新登记簿
共同:  粘贴进 Unity 大文本框 → 1秒微调 → 注入灵魂 → 一键换肤 → 沉淀片段
```

### 路线 A：全自动流水线（本地 Agent 自动搜网与扩容）

适用于 Manus、Cursor、Windsurf 等具备本地文件读写、联网搜索和 Git 权限的 AI 智能体。换电脑、换账号、换对话窗口时，直接把路线 A 指令发给 AI，它会自动完成搜集、转译、防重归档的全流程。完整指令模板见 `LevelDesign_References/AI_PROMPT_WORKFLOW.md`。

**自动化流水线 5 步**：

| 步骤 | 动作 | 产出 |
|------|------|------|
| 1. 读取防重库 | 克隆仓库，读取 `TEMPLATE_REGISTRY.md` | 理解已有博弈组合和黑名单 |
| 2. 定向搜网 | 全网搜索 2D 平台游戏优秀关卡设计 | 避开黑名单的全新灵感 |
| 3. 转译写入 | 遵守物理法则和字典映射，生成 ASCII 矩阵 | 追加到 `ASCII_Templates.md` |
| 4. 更新登记簿 | 生成语义指纹，更新登记表/黑名单/灵感池/缺失机制 | 更新 `TEMPLATE_REGISTRY.md` |
| 5. Git Push | 提交并推送到云端 | 持久化防重记忆 |

### 路线 B：半自动视觉流水线（网页 AI 看图拆解）

适用于在 B 站、YouTube 或 Pinterest 看到神仙关卡截图时，让网页版 Claude / GPT-4o / Gemini 帮你精准转成 ASCII。操作步骤：先从 `TEMPLATE_REGISTRY.md` 复制快速复制区的防重摘要，然后连同截图一起发给网页 AI（完整咒语见 `AI_PROMPT_WORKFLOW.md`），最后手动将结果写回登记簿并 Git Push。

### Unity 内的极速实操（两条路线共享）

不管用哪条路线拿到了 ASCII 文本，接下来都在 Unity 中执行以下三个阶段：

**第一阶段：文本拼装与"1秒微调"**

在 Unity 按 `Ctrl+T` 唤出 Level Studio，把 ASCII 矩阵粘贴到大文本框中，点击 **Build From Text**，关卡骨架瞬间生成。用片段库追加教学起点区和终点冲刺区，拼出完整流程。发现地刺跳不过去？**绝不要去 Scene 里用鼠标拖拽白盒**，直接在文本框里改字符，点击 Build 秒更新。将调关卡的闭环从 1 分钟缩短到 3 秒钟。

**第二阶段：注入灵魂与换位思考**

对照 AI 给的参数建议，在 Scene 里选中移动平台调整位移、选中摆锤调整幅度。开启全局作弊的"无冷却/无限能量"，用捣蛋者视角逛一圈。思考"如果我是捣蛋者，我要怎么阴人？"如果发现某处对闯关者太安全，就在文本框里加一排摆锤或伪装墙，再次生成。

**第三阶段：资产沉淀与一键换肤**

白盒测试彻底满意后，去 Test Console 的 Theme Profile 槽位拖入配置好的美术包，点击 **Apply Theme**，满屏方块瞬间变成精美完整的游戏画面。`Ctrl+S` 保存场景。把微调出手感极其完美的跳跃组合保存下来，下次让代码 AI 直接硬编码进 Snippet Library，实现全项目一键复用。

---

## 第三部分：关卡模板库概览

项目当前拥有 **8 个经过严格验证的 ASCII 白盒模板**，存储在 `LevelDesign_References/ASCII_Templates.md` 中。每个模板都包含完整的 ASCII 矩阵、博弈解析、动态参数建议和缺失机制分析，可直接复制粘贴到 Unity 的 Custom Template Editor 中一键生成。

### 模板总览

| 编号 | 名称 | 尺寸 | 难度 | 核心博弈 | 灵感来源 |
|------|------|------|------|---------|---------|
| T01 | Spike Abyss & Bait（地刺深渊与诱饵金币） | 25x12 | 中 | 被动风险选择 | Celeste 早期关卡 |
| T02 | Zigzag Climb Tower（Z 字攀爬塔） | 20x16 | 中 | 节奏观察 + 时机起跳 | Mega Man 垂直攀爬 |
| T03 | Platform & Bouncer Duet（移动平台与弹跳怪协奏） | 26x13 | 难 | 三段连锁跳跃 | DKC 矿车 + Mario |
| T04 | Fire Corridor & Pendulum（火焰走廊与摆锤） | 30x9 | 难 | 纯时间窗口通过 | Super Meat Boy |
| T05 | Crumbling Escape（崩塌逃亡） | 25x11 | 难 | 不停跑的时间压力 | Celeste 崩塌追逐段 |
| T06 | Secret Chamber（伪装墙密室） | 25x12 | 易 | 探索发现 | SMB + 银河恶魔城 |
| T07 | Pogo Bounce & Branching Path（踩敌跳板与双路分支） | 30x13 | 中 | 敌人利用 + 路径博弈 | Shovel Knight + Mario |
| T08 | Vertical Descent & Visual Trap（垂直坠井与视觉陷阱） | 13x22 | 难 | 垂直下落 + 视觉欺骗 | Downwell + I Wanna |

### 难度标签定义

| 标签 | 定义 |
|------|------|
| 易 | 新手友好，主要教学或探索，几乎不会死 |
| 中 | 需要一定技巧，可能死 1~3 次 |
| 难 | 需要精确操作和节奏把握，可能死 5~10 次 |
| 极难 | 毒图级别，需要反复练习和肌肉记忆 |

### 推荐拼装顺序

| 关卡类型 | 推荐组合 |
|---------|---------|
| 教学关卡（新手友好） | T06（密室探索）→ T01（地刺深渊） |
| 技巧关卡（中等难度） | T02（Z 字攀爬）→ T07（踩敌跳板与路径分支） |
| 探索关卡（中等难度） | T07（路径分支）→ T06（密室探索） |
| 极限关卡（高难度） | T05（崩塌逃亡）→ T04（火焰走廊） |
| 心理战关卡（难度递进） | T08（垂直坠井与视觉陷阱）→ T03（移动平台协奏） |

---

## 第四部分：关卡设计理论与参考

为了充分利用 Level Studio 构建引人入胜的关卡，本部分整理了业界顶尖 2D 平台跳跃游戏的设计最佳实践。

### 1. 核心设计方法论：GMTK 四步法

任天堂在《超级马里奥 3D 世界》等作品中确立了经典的四步关卡设计法 [1]。在设计 MarioTrickster 的关卡时，应遵循这一节奏：

| 步骤 | 说明 | 示例 |
|------|------|------|
| **Introduce（引入）** | 在绝对安全的环境中展示新机制，即使操作失误也不会受到惩罚 | T07 分支入口前的单个弹跳怪+金币教学区 |
| **Develop（发展）** | 提供第一个真正的挑战，需要运用刚学到的机制 | T07 下路的第一个弹跳怪跨越 |
| **Twist（变化）** | 为机制增加变数，如与其他机制组合 | T07 下路的连续 3 个弹跳怪链 |
| **Conclude（总结）** | 关卡高潮，综合性的终极考验 | T07 下路到达对岸后与上路汇合 |

> T07（踩敌跳板与双路分支）是 GMTK 四步法"渐进揭示"的典型实践：教学区（Introduce）→ 分支选择（Develop）→ 弹跳怪链（Twist）→ 路径汇合（Conclude）。

### 2. 关卡节奏与难度曲线

优秀的平台跳跃游戏不会让难度呈直线上升，而是采用波浪式的节奏 [2]。

**安全区与缓冲**：在经历了一次高强度的 Twist 挑战后，应提供一段相对平静的区域，放置一些收集物（金币），让玩家在心理上得到放松。

**非对称设计的特殊性**：在 MarioTrickster 中，由于 Trickster 玩家的存在，关卡的动态难度极高。因此，基础地形设计必须比传统的单人马里奥游戏更宽容。如果地形本身已经极度硬核，Trickster 的任何干扰都会导致关卡无解。

### 3. 视觉引导与直觉设计

**金币的引导作用**：金币不仅是收集物，更是设计师留给玩家的"隐形路径"。在需要盲跳的地方，用金币标示出安全的抛物线轨迹 [3]。T07 的下路金币形成抛物线引导线，暗示正确的踩踏节奏；T08 的密室金币则奖励勇于探索的玩家。

**信任与欺骗的博弈**：T08（垂直坠井与视觉陷阱）展示了一种高级设计手法——通过伪装墙制造"信任危机"，让玩家对所有平台产生怀疑；再通过隐藏通道提供"反向欺骗"的奖励，训练玩家"大胆尝试"的心态 [4]。

**不对称美学**：避免设计完全对称的房间或结构。不对称的关卡不仅在视觉上更自然，在游玩路线上也更具探索感 [2]。

### 4. 物理法则约束

在设计 ASCII 模板时，必须严格遵守以下物理法则，确保关卡可通关：

| 约束 | 数值 |
|------|------|
| 水平安全跨越 | 4 格（普通玩家可轻松跨越） |
| 水平极限跨越 | 5 格（含 coyote time，需精确操作；物理极限 5.85 格） |
| 垂直安全跳高 | 2 格（普通玩家可轻松达到） |
| 垂直极限跳高 | 2.5 格（PhysicsMetrics.MAX_JUMP_HEIGHT，原地起跳绝对上限） |
| 模板宽度范围 | 20 ~ 30 字符（推荐） |
| 模板高度范围 | 10 ~ 15 行（推荐） |
| 空白填充字符 | `.`（英文点号，绝对禁止空格） |

---

## 第五部分：博弈维度覆盖与灵感池

项目通过 `LevelDesign_References/TEMPLATE_REGISTRY.md` 维护一个"博弈维度灵感池"，追踪 14 个经典 2D 平台跳跃博弈维度的覆盖情况。截至 S29，已覆盖 5 个维度，剩余 9 个待探索。

### 覆盖进展

| 博弈维度 | 状态 | 对应模板 |
|----------|------|---------|
| 路径分支 | 已覆盖 | T07 |
| 敌人利用 | 已覆盖 | T07 |
| 渐进揭示 | 已覆盖 | T07 |
| 垂直下落 | 已覆盖 | T08 |
| 视觉欺骗 | 已覆盖 | T08 |
| 追逐压力 | 未覆盖 | — |
| 视野限制 | 未覆盖 | — |
| 节奏强制 | 未覆盖 | — |
| 环境交互 | 未覆盖 | — |
| 弹射组合 | 未覆盖 | — |
| 对称/镜像 | 未覆盖 | — |
| 重力翻转 | 未覆盖 | — |
| 时间压力 | 未覆盖 | — |
| 风场/气流 | 未覆盖 | — |

### 搜集标准

搜集新关卡灵感时，不止搜集极难毒图。有趣的、精妙的、创意独特的关卡设计同样欢迎。搜集来源包括但不限于：马里奥制造 2 热门关卡、蔚蓝速通路线、I Wanna 系列、空洞骑士白宫、Shovel Knight 宝藏关、Rayman 节奏关卡、VVVVVV 重力翻转、Ori 弹射组合、Downwell 垂直下落等。

---

## 第六部分：项目要素分析与扩展路线图

通过对 MarioTrickster 当前代码库的全局检索（`LevelElementBase` 及 `ControllableLevelElement`），并结合 8 个 ASCII 模板的缺失机制分析，整理出当前项目的要素覆盖情况及未来扩展建议。

### 1. 当前已实现的要素库

项目目前已具备非常扎实的基础框架，涵盖 18 种核心元素（均已在 `AsciiLevelGenerator` 字典中注册）：

| 类别 | 元素 |
|------|------|
| 地形类 | 地面 `#`、平台 `=`、墙壁 `W`、单向平台 `-`、移动平台 `>` |
| 陷阱类 | 地刺 `^`、火焰陷阱 `~`、摆锤 `P` |
| 互动平台 | 弹跳平台 `B`、崩塌平台 `C` |
| 敌人类 | 简单巡逻敌人 `e`、弹跳怪 `E` |
| 隐藏与欺骗 | 伪装墙 `F`、隐藏通道 `H` |
| 其他 | 收集物 `o`、终点 `G`、Mario 出生点 `M`、Trickster 出生点 `T` |

### 2. 缺失机制待办（武器库扩展清单）

以下是在搜集关卡灵感过程中发现的、当前字典中不存在的机制。完整清单统一维护在 `TEMPLATE_REGISTRY.md` 的【缺失机制待办】中。

| 优先级 | 机制 | 建议字符 | 当前暂代 | 新增价值 | 发现来源 |
|--------|------|---------|---------|---------|---------|
| 高 | 旋转锯片 (Circular Saw) | `@` | `~` 火焰 | 圆周运动的动态障碍 | T04 |
| 高 | 传送带 (Conveyor Belt) | `<` | `=` 平台 | 改变水平速度的新博弈 | T03 |
| 高 | 追逐者 (Chaser) | `!` | 无法暂代 | 时间压力和紧迫感 | T05 |
| 高 | 飞行敌人 (Flying Enemy) | `f` | 无法暂代 | 空中威胁 | DesignGuide |
| 中 | 水域/液体 (Water/Lava) | `w` / `L` | 无法暂代 | 改变物理规则的区域环境 | DesignGuide |
| 中 | 钥匙与锁门 (Key & Lock) | `K` / `D` | `F` 伪装墙 | 探索解谜维度 | T06 |
| 中 | 冰面地形 (Ice Surface) | `I` | `=` 平台 | 低摩擦力微操 | DesignGuide |
| 中 | 可破坏方块 (Breakable Block) | `X` | `C` 崩塌平台 | 主动改变地形 | T06 |
| 中 | 检查点 (Checkpoint) | `S` | 无 | 降低长关卡挫败感 | DesignGuide |
| 低 | 冲刺 (Dash) | 角色能力 | 无法暂代 | 提升操作上限 | T01 |
| 低 | 抓墙/滑墙 (Wall Cling) | 角色能力 | `W` + 平台 | 扩展垂直设计空间 | T01 |
| 低 | 梯子 (Ladder) | `\|` | 交错平台 | 垂直移动替代方案 | T02 |
| 低 | 远程攻击 (Shooting) | 角色能力 | 踩踏 | 改变敌人交互方式 | T02 |
| 低 | 弹簧 (Spring) | `s` | `B` 弹跳平台 | 可控弹射，与弹跳怪互补 | T03 |
| 低 | 下砸攻击 (Pogo/Shovel Drop) | 角色能力 | 踩踏 | 主动向下攻击 | T07 |
| 低 | 单向下落平台 (Drop-through) | `v` | `-` 单向平台 | 专为垂直下落关卡设计 | T08 |
| 低 | 视觉提示系统 (Visual Hint) | 参数配置 | 无 | 伪装墙附近的微妙视觉差异 | T08 |

### 3. 基于 Registry 的零代码扩展工作流

S57b 之后，`AsciiLevelGenerator` 已经不再承担普通元素的专属生成逻辑。新增上述要素时，生成器核心代码必须保持不变；唯一允许的扩展路径是“新建逻辑脚本 + 在 `AsciiElementRegistry` 默认表配置一行数据”。详细规范见 `docs/ASCII_ZERO_CODE_ELEMENT_EXTENSION.md`。

| 步骤 | 操作 | 禁止事项 |
|------|------|----------|
| 1 | 编写 `ConveyorBelt.cs` 等逻辑脚本，必要时在脚本内自检依赖组件。 | 禁止在生成器内写元素行为。 |
| 2 | 在 `AsciiElementRegistry.CreateDefaultInstance()` 增加 `AsciiElementEntry`，填写 `componentTypeNames`、`customColliderSize`、`customColliderOffset`、`isTrigger`、Visual 参数。 | 禁止新增 `SpawnConveyorBelt` 或任何普通元素专属 `SpawnXXX` 方法。 |
| 3 | 在 `LevelThemeProfile` 的 `elementSprites` 数组中添加 `elementKey = "ConveyorBelt"` 的配置项。 | 禁止绕过 `elementName` 前缀匹配机制。 |
| 4 | 在 Level Studio 中使用 `<` 字符搭建关卡，并运行模板验证与 PlayMode 测试。 | 禁止通过修改生成循环来适配单个字符。 |

> **零代码新增红线**：普通元素的字符解析、碰撞体尺寸、Trigger 状态、逻辑组件挂载和 Visual 结构均由 Registry 数据驱动。除 `Ground`、`OneWayPlatform` 合并生成以及 `M` / `T` 玩家出生点外，任何新增机制都不得修改 `AsciiLevelGenerator` 核心代码。

> **隐藏机制**：在搜集和拆解过程中，AI 可能会告诉你某游戏有一条传送带极其巧妙，但你的字典里没有。你不需要在做关卡时去死磕生成器代码，只需要把“传送带”记在 `TEMPLATE_REGISTRY.md` 的缺失机制待办里。后续实现时，新建底层逻辑脚本并在 Registry 增加一行配置即可让“打字密码本”扩容。

---

## 第七部分：跨账号防重闭环机制

MarioTrickster 的关卡设计系统通过 Git 仓库中的实体文件实现跨会话、跨账号的"零重复"保障。

### 核心架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Git 仓库 (云端持久化)                      │
│                                                             │
│  TEMPLATE_REGISTRY.md  ← 所有 AI 共享的"外挂大脑（黑名单）"   │
│  ASCII_Templates.md    ← 所有关卡的真相源                     │
│                                                             │
│  新开窗口的 AI 开局强制"吃下"黑名单                            │
│  → 搜集完新关卡后，强制提取"语义指纹"并写回黑名单               │
│  → 自动 Git Push                                            │
│  → 下一个 AI（任何账号）Pull 后看到完整历史                     │
│                                                             │
│  流水的 AI 永远共用铁打的防重库，绝不撞车！                     │
└─────────────────────────────────────────────────────────────┘
```

### 防重三件套

| 文件 | 职责 |
|------|------|
| `TEMPLATE_REGISTRY.md` | 登记簿：完整登记表 + 快速复制区（去重摘要）+ 已探索灵感来源（黑名单）+ 灵感池 + 缺失机制待办 + 语义指纹格式规范 |
| `ASCII_Templates.md` | 真相源：所有 ASCII 模板的完整矩阵 + 博弈解析 + 动态参数建议 + 缺失机制分析 |
| `AI_PROMPT_WORKFLOW.md` | 指令模板：路线 A 全自动指令 + 路线 B 半自动咒语 + Unity 实操三阶段 |

### 去重粒度

按"核心博弈组合"而非"类型"去重。同一类型（如地刺深渊）只要博弈手法不同就不算重复，鼓励同类型的深度探索。语义指纹格式为：`T编号_[核心机制A] + [核心机制B]（= 玩家心理博弈体验）[难度:易/中/难/极难]`。

---

## 参考文献

[1] Game Developer. "Super Mario Bros. 3 Level Design Lessons, Part 1". https://www.gamedeveloper.com/design/super-mario-bros-3-level-design-lessons-part-1

[2] Tadeas Jun. "How to design breathtaking 2D platformer levels". https://www.tadeasjun.com/blog/2d-level-design/

[3] Pinnguaq. "Mario Maker Level Design Basics". https://pinnguaq.com/learn/mario-maker-level-design-basics/

[4] Game Developer. "Deep Dive: Designing the Super Mario Maker-inspired I Wanna Maker". https://www.gamedeveloper.com/design/deep-dive-designing-the-i-super-mario-maker-i--inspired-i-i-wanna-maker-i-

[5] Yacht Club Games. "Specter of Torment Level Design Deep Dive". https://www.yachtclubgames.com/blog/specter-of-torment-level-design-deep-dive-1-5/
