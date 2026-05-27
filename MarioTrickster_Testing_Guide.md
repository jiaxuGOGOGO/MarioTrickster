# MarioTrickster 测试指南

> 本文档覆盖：手动 Play 测试、自动化 Test Runner 测试、伪装系统 Sprite 配置指引、调试信息说明、**回归影响矩阵与修复流程**、**工具集使用指南**、**用户反馈模板**。
> 更新时间：2026-05-17

---

## 🛠️ 一、准备工作

<details open>
<summary><b>1.1 生成测试场景</b></summary>

1. **File → New Scene → Basic 2D**（创建空白场景）
2. 菜单栏 **MarioTrickster → Test Console** (或快捷键 `Ctrl+T`) 打开工具窗口
3. 在 **Level Builder** Tab 下点击 **Generate Whitebox Level** 生成白盒关卡，或点击 **Build Test Scene** 生成标准测试关卡
4. **Ctrl+S** 保存场景（建议命名为 `TestScene`）

</details>

<details open>
<summary><b>1.2 配置伪装系统 Sprite（必须手动完成）</b></summary>

TestSceneBuilder 生成的 Trickster 已经挂载了 `DisguiseSystem`，但 **Available Disguises 列表是空的**。你需要手动添加至少一个伪装形态才能测试伪装功能。

**步骤：**
1. 在 Hierarchy 中选中 **Trickster** 对象
2. 在 Inspector 中找到 **Disguise System** 组件
3. 展开 **Available Disguises** 列表
4. 点击 **+** 按钮添加一个元素

**每个伪装形态需要填写：**

| 字段 | 说明 | 示例值 |
|------|------|--------|
| Disguise Name | 伪装名称（调试显示用） | `Brick Block` |
| Disguise Sprite | **必填！** 变身后显示的 Sprite | 见下方获取方式 |
| Icon Sprite | UI图标（可选，暂不需要） | 留空 |
| Custom Collider Size | 变身后碰撞体大小，(0,0)=不调整 | `(1, 1)` |
| Custom Collider Offset | 变身后碰撞体偏移，(0,0)=不调整 | `(0, 0)` |
| Custom Scale | 变身后缩放，(0,0,0)=不调整 | `(1, 1, 1)` |
| Type | 伪装类型 | `Static` |

</details>

<details>
<summary><b>1.3 获取 Sprite 的三种方式 (点击展开)</b></summary>

**方式一：使用 Unity 内置 Sprite（最快，推荐测试用）**
1. 在 Inspector 的 **Disguise Sprite** 字段右侧点击 **圆形选择器**（⊙）
2. 在弹出的 Sprite 选择窗口中搜索 `Knob` 或 `UISprite` 或 `Background`
3. 选择任意一个 Unity 内置 Sprite 即可（形状不重要，测试用）

**方式二：快速创建纯色 Sprite（推荐）**
1. 在 Project 窗口右键 → **Create → 2D → Sprites → Square**
2. Unity 会创建一个白色方块 Sprite
3. 将它拖入 **Disguise Sprite** 字段
4. 可以多创建几个不同形状（Square / Circle / Triangle）作为不同伪装形态

**方式三：导入外部像素素材（正式开发用）**
1. 从 [Pixel Adventure](https://pixelfrog-assets.itch.io/pixel-adventure-1) 下载免费素材包
2. 解压后将文件夹拖入 Unity 的 `Assets/Sprites/` 目录
3. 选择素材中的砖块/管道/怪物等 Sprite 作为伪装形态

</details>

<details open>
<summary><b>1.4 推荐的伪装配置（添加 3 个形态）</b></summary>

| 序号 | Disguise Name | Sprite | Collider Size | Type |
|------|--------------|--------|---------------|------|
| 0 | Brick Block | Square Sprite | (1, 1) | Static |
| 1 | Spike Trap | Triangle Sprite | (1, 0.5) | Hazard |
| 2 | Slime Enemy | Circle Sprite | (0.8, 0.8) | Enemy |

配置完成后，屏幕右上角的调试信息应该从 `❌ 未配置伪装形态` 变为 `✅ 就绪: 当前选中 [Brick Block]，按 P 伪装`。

</details>

---

## 🎮 二、手动 Play 测试（按 Play 后操作）

点击 Unity 的 **Play** 按钮进入游戏，按以下顺序逐项测试。

<details>
<summary><b>测试 1-3：基础移动与平台 (点击展开)</b></summary>

### 测试 1：Mario 基础移动与跳跃 ✅ 已完成
| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 向右移动 | D | Mario（红色方块）向右移动，起步迅速 |
| 向左移动 | A | Mario 向左移动 |
| 松开移动键 | 松开 A/D | Mario 立即停止，无打滑感 |
| 跳跃 | Space | Mario 向上弹起，有明显高度 |
| 短按跳跃 | 快速点按 Space | 跳跃高度较低（提前松开减少高度） |
| 长按跳跃 | 长按 Space | 跳跃高度更高 |
| 空中移动 | 空中按 A/D | 可以在空中微调方向（有空中滑行感） |

**通过标准**：移动流畅、停止果断、跳跃手感好、无卡顿。

### 测试 2：Trickster 基础移动 ✅ 已完成
| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 向右移动 | → 方向键 | Trickster（蓝色方块）向右移动 |
| 向左移动 | ← 方向键 | Trickster 向左移动 |
| 跳跃 | ↑ 方向键 / 右Ctrl | Trickster 跳跃 |

**通过标准**：移动手感与 Mario 类似但略慢（maxSpeed 8 vs 9）。

### 测试 3：移动平台跟随 ✅ 已完成
| 操作 | 预期结果 |
|------|----------|
| 让 Mario 站上移动平台（场景中左右移动的浅色平台） | Mario 随平台平稳移动，不被甩飞 |
| 在平台上按 A/D | 可以在平台上自由走动 |
| 从平台上跳离 | 恢复正常重力，不会突然加速 |

**通过标准**：角色随平台平稳移动，无甩飞、无打滑、无速度累积。

</details>

<details open>
<summary><b>测试 4-5：伪装与操控能力</b></summary>

### 测试 4：伪装变身系统（需要先完成 1.2 Sprite 配置） ✅ 已完成
| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 查看调试信息 | — | 屏幕右上角显示 `✅ 就绪: 当前选中 [Brick Block]，按 P 伪装` |
| 切换伪装形态 | O（下一个）/ I（上一个） | 调试信息中的形态名称切换 |
| 执行伪装 | P | Trickster 外观变为选中的 Sprite，碰撞体大小改变 |
| 伪装后移动 | 方向键 | 移动速度大幅降低（仅 15% 正常速度） |
| 伪装后静止等待 | 不按任何键，等 1.5 秒 | 调试信息显示 `(已融入)`，Trickster 的 Sorting Order 变为 0（与场景物体同层） |
| 移动打破融入 | 按方向键 | 调试信息变回 `(未融入)` |
| 解除伪装 | 再按 P | Trickster 恢复原始蓝色方块外观 |
| 冷却检查 | 解除后立即按 P | 调试信息显示 `⏳ 伪装冷却中: X.Xs`，无法立即再次伪装 |
| 冷却结束后 | 等待 2 秒后按 P | 可以再次伪装 |

**通过标准**：伪装/解除正常切换、冷却机制生效、融入检测正常。

### 测试 5：道具操控能力（需要先完成伪装并融入） 🔄 待回归 (S20 重构)
**前置条件**：Trickster 已伪装且已完全融入场景（静止 1.5 秒以上）。

| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 走到可操控道具附近（橙色陷阱或棕色方块） | 方向键 | 靠近道具（2 格范围内） |
| 伪装并等待融入 | P → 等 1.5 秒 | 调试信息显示 `(已融入)`，消耗 25 能量 |
| **[S20] 观察视觉连线** | — | 融入后屏幕出现连线：**红色加粗线**连向当前锁定目标，**灰色细线**连向其他备选目标。锁定目标微红脉冲高亮 |
| **[S20] 方向键磁吸切换** | 方向键 | 按方向键时 Trickster **不移动**，而是红线磁吸转移到该方向最近的备选道具，原目标变灰线 + 解除高亮 |
| **[S20] 操控红线目标** | L | 仅触发红线连接的道具（当前锁定目标），进入预警阶段（闪烁变红 + 震动），消耗 20 能量 |
| 观察爆发阶段 | — | 预警结束后，道具执行阻碍动作（陷阱伸出尖刺/方块消失等） |
| 观察冷却阶段 | 再按 L | 道具处于冷却中，无法立即再次触发 |
| **[S20] 移动后连线消失** | 方向键移动 | 移动后脱离融入状态，所有连线立即消失，目标高亮解除 |
| 能量不足测试 | 连续多次操控 | 能量不足时按 L，屏幕提示“能量不足”，无法操控 |

**通过标准**：融入后红/灰连线正确显示；方向键磁吸切换目标；按L仅触发红线目标；移动后连线消失；Telegraph→Active→Cooldown 三阶段流程正常。

</details>

<details>
<summary><b>测试 6-8：技能、镜头与系统机制 (点击展开)</b></summary>

### 测试 6：扫描技能 (Session 11 修复 B015) ✅ 已通过
| 操作 | 按键 | 预期结果 |
|------|------|----------|
| Mario 靠近伪装的 Trickster | WASD | 靠近至 5 格范围内 |
| 发动扫描（范围内） | Q | **红色**脉冲圆环（从第一帧就是红色），Mario 下方显示 "Trickster Detected!" |
| 观察扫描结果 | — | Trickster 闪烁红色 + 头顶 `!` 警告标记，持续 2 秒 |
| 发动扫描（范围外） | Q | **蓝色**脉冲圆环，Mario 下方显示 "Out of range (Xm)" |
| Trickster 未伪装时扫描 | Q | 蓝色脉冲，显示 "Trickster not disguised" |
| 观察冷却阶段 | 再按 Q | 技能处于冷却中（8秒），无法立即再次触发 |
| **确认无矛盾提示** | — | 屏幕下方**不应再出现**"未在范围内"等旧的矛盾提示 |

**通过标准**：脉冲颜色从第一帧就正确（红=检测到，蓝=未检测到），扫描结果文字清晰，无矛盾提示。

### 测试 6.5：镜头系统 (Session 11 修复 B016) ✅ 已通过
| 操作 | 预期结果 |
|------|----------|
| Mario 完全静止 | 镜头完全静止，无任何晃动 |
| Mario 向右跑然后松开按键 | 镜头平滑减速停止，无回弹/抖动 |
| Mario 左右快速切换方向 | 镜头平滑过渡，无突变/抽搐 |
| Mario 跳跃落地 | 镜头平滑跟随，无突变 |
| Mario 移动到关卡边界 | 镜头被限制在边界内，不会超出 |
| 长时间挂机观察 | 镜头保持绝对静止，不会漂移 |

**通过标准**：所有场景下镜头平滑跟随，无任何可见晃动/抖动/回弹。

### 测试 7：胜负判定与UI显示 (Session 12 修复 B018) ✅ 已通过
| 操作 | 预期结果 |
|------|----------|
| 控制 Mario 走到右侧终点（绿色半透明区域） | 触发 Mario 胜利，屏幕显示半透明黑色遮罩 + 红色横幅 + 黄色大字 "MARIO WINS!" + 比分 + 闪烁提示 "Press R to Restart \| Press N for Next Round" |
| 按 R 重启 | 场景重置，胜利画面消失 |
| 按 N 下一回合 | 回合数+1，位置重置，继续游戏 |
| 控制 Mario 掉入底部深渊（跳出地面边缘向下掉） | 触发 Mario 死亡，屏幕显示蓝色横幅 + "TRICKSTER WINS!" |
| 按 F5 快速重启 | 场景完全重置 |

**通过标准**：胜负判定正确触发、胜利/失败画面正确显示（半透明遮罩+大字+比分+闪烁提示）、重启功能正常。

> ℹ️ **B018 修复说明**：如果你之前用 TestSceneBuilder 生成的场景没有显示胜利画面，请重新执行 **MarioTrickster → Clear Test Scene** 然后 **MarioTrickster → Build Test Scene** 重新生成场景，新版本已自动包含 GameUI 组件。如果你使用的是手动搭建的场景（如 mario.unity），请手动在任意 GameObject 上添加 `GameUI` 组件。

### 测试 8：暂停系统 ✅ 已通过
| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 暂停 | ESC | 游戏暂停（Time.timeScale = 0），屏幕显示半透明遮罩和"PAUSED" |
| 恢复 | 再按 ESC | 游戏恢复正常，直接回到游戏画面（无额外提示） |

</details>

<details open>
<summary><b>测试 9-10：关卡设计系统与元素明细</b></summary>

### 测试 9：关卡设计系统 (Session 15 新增) ⬜ 待测试
**前置条件**：使用 `MarioTrickster → Build Test Scene` 重新生成测试场景，场景中会自动包含所有 9 种新关卡元素。

### 测试 10：Level Studio 关卡工坊与动态锚点 (S26b-S33 新增) ⬜ 待测试
**前置条件**：打开 `MarioTrickster → Test Console` (Ctrl+T)。

| 功能模块 | 操作 | 预期结果 |
|----------|------|----------|
| **ASCII 模板生成** | 在 Level Builder Tab 的文本框中输入模板（或从 Snippet Library 追加），点击 `Build From Text` | 场景中生成对应关卡，且**自动补全可玩环境**（Mario、Trickster、GameManager 等），可直接按 Play 游玩。 |
| **物理验证器** | 输入一个包含超过 4 格宽间雙或超过 2.5 格高平台的 ASCII 模板并 Build | Console 输出 `AsciiLevelValidator` 的警告/错误信息，但不阻止生成。 |
| **跳跃抛物线** | 在 Scene 视图中选中 Mario | 实时显示绿色（最高跳）、蓝色（极限远跳）、黄色（短跳）抛物线及网格刻度，无需运行游戏。 |
| **半重力跳跃顶点** | 运行游戏，长按跳跃键 | Mario 在跳跃最高点附近时，重力减半，滞空时间略微延长，弧线更平缓。 |
| **动态锚点传送** | 切换到 Teleport Tab，展开 `Dynamic Anchors` | 自动列出当前场景中的 SpawnPoint、GoalZone、Trap、Enemy 等兴趣点。点击 `[F]` 可在 Scene 视图聚焦，点击按钮可传送。 |
| **Cheats 缺失检测** | 在空场景中打开 Cheats Tab | 所有作弊选项置灰，提示缺少组件，并显示 `Auto-Fix: Inject Playable Environment` 按钮。点击后自动修复并可用。 |
| **主题换肤** | 创建 `Level Theme Profile`，配置 Sprite，拖入 Level Builder 的 Theme 槽位并点击 Apply | 场景中的白盒元素被替换为配置的 Sprite，碰撞体大小保持不变（视碰分离）。 |

**通过标准**：ASCII 生成的关卡可直接 Play；抛物线在 Scene 视图正确显示；动态锚点能扫描到场景元素；Cheats 缺失检测和 Auto-Fix 正常工作；主题换肤不影响碰撞体。

### 测试 9 关卡元素明细表

| 元素 | 角色 | 操作 | 预期结果 |
|------|------|------|----------|
| **SpikeTrap** (地刺) | Mario | 碰到伸出的地刺 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 地刺强制伸出或切换伸缩频率 |
| **PendulumTrap** (摆锤) | Mario | 碰到摆动的锤头 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 摆幅增大、速度加快 |
| **FireTrap** (火焰) | Mario | 碰到喷射的火焰 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 强制喷射火焰或加速喷射频率 |
| **BouncingEnemy** (弹跳怪) | Mario | 从侧面碰到怪物 | 受到伤害并被击退 |
| | Mario | 从上方踩踏怪物 | 怪物被消灭，Mario 获得向上弹力 |
| | Trickster | 伪装融入后按 L 操控 | 怪物弹跳高度增加、速度加快 |
| **BouncyPlatform** (弹跳平台) | Mario | 从任意方向碰撞平台 | 修正后法线方向弹射 + 完整抛物线 + 蓄力冻结(~0.25s) + 挤压/拉伸动画 + 相机震动 |
| | Trickster | 伪装融入后按 L 操控 | 平台的弹射力大幅增加，可覆盖弹射方向 |
| **OneWayPlatform** (单向平台) | Mario | 从平台下方往上跳 | 可以穿过平台并落在上面 |
| | Mario | 站在平台上按 S+Space | 从平台上方穿过落下 |
| | Mario | 站在平台上单独按 S | 不会落下（防止误操作） |
| **CollapsingPlatform** (崩塌平台) | Mario | 站上平台 | 平台开始抖动，短时间后掉落，几秒后自动重生 |
| | Trickster | 伪装融入后按 L 操控 | 平台立即崩塌掉落 |
| **HiddenPassage** (隐藏通道) | Mario | 走到入口处按 S | 被传送到出口位置 |
| | Mario | 在出口处按 S | 被传回入口位置（双向穿越） |
| | Mario | 传送后立即再按 S | 冷却时间内无法重复传送 |
| | Trickster | 伪装融入后按 L 操控 | 通道被封锁，Mario 无法进入 |
| **FakeWall** (伪装墙) | Mario | 走进看起来像墙的区域 | 墙壁变半透明，显示内部隐藏空间 |
| | Trickster | 伪装融入后按 L 操控 | 伪装墙变为真实物理墙壁，阻挡 Mario |

</details>

---

## 🤖 三、自动化 Test Runner 测试 (EditMode & PlayMode)

<details open>
<summary><b>如何运行自动化测试</b></summary>

本项目已配置了基于 NUnit 的自动化测试框架。每次修改核心逻辑后，**必须**运行这些测试以防止回归。

**操作步骤：**
1. 在 Unity 菜单栏选择 **Window → General → Test Runner**
2. 确保勾选右上角的 **EditMode** 或 **PlayMode**
3. 点击左上角的 **Run All**
4. 观察测试结果：
   - 绿色勾 ✅ 表示通过
   - 红色叉 ❌ 表示失败（点击失败的测试可以在下方看到详细的错误堆栈）

</details>

<details>
<summary><b>EditMode 与 PlayMode 测试列表 (点击展开)</b></summary>

**EditMode 测试列表（不需要进入游戏即可运行，速度快）：**
*   `MarioControllerTests`：测试 Mario 的初始化和基础状态
*   `TricksterControllerTests`：测试 Trickster 的初始化和基础状态
*   `GameManagerTests`：测试 GameManager 的单例和基础状态
*   `InputManagerTests`：测试输入管理器的按键映射
*   `DisguiseSystemTests`：测试伪装系统的冷却、消耗、状态切换逻辑
*   `AbilitySystemTests`：测试技能基类、能量消耗、冷却机制
*   `ScanAbilityTests`：测试扫描技能的范围检测逻辑
*   `CameraSystemTests`：测试相机系统的边界限制和跟随逻辑

**PlayMode 测试列表（需要自动进入游戏并模拟运行，速度较慢）：**
*   `MarioMovementPlayTests`：模拟按键测试 Mario 的实际移动、跳跃物理效果
*   `TricksterMovementPlayTests`：模拟按键测试 Trickster 的实际移动效果
*   `DisguiseSystemPlayTests`：模拟按键测试伪装后的速度变化和融入场景的物理效果
*   `MovingPlatformPlayTests`：模拟测试角色在移动平台上的跟随效果

</details>

---

## 🐞 四、常见问题与调试信息

<details open>
<summary><b>调试信息解读</b></summary>

屏幕右上角的调试信息提供了游戏内部状态的实时反馈，帮助定位问题：

| 状态 | 说明 |
|------|------|
| `❌ 未配置伪装形态` | 需要在 Trickster 的 Disguise System 组件中添加形态 |
| `✅ 就绪: 当前选中 [X]` | 伪装系统准备就绪，可以按 P 变身 |
| `(未融入)` | Trickster 已变身，但还在移动或刚停止，未与场景融为一体 |
| `(已融入)` | Trickster 变身后静止超过 1.5 秒，已完全融入场景 |
| `⏳ 伪装冷却中: X.Xs` | 解除伪装后，需要等待冷却结束才能再次变身 |
| `[能量不足]` | 尝试执行需要能量的操作，但能量条为空 |

</details>

---

## 📈 五、回归影响矩阵与修复流程

<details open>
<summary><b>如何使用影响矩阵</b></summary>

当修改一个模块时，可能会意外破坏其他模块。使用此矩阵确定需要重点测试的范围。

| 修改的模块 | 必须回归测试的关联模块 | 可能的影响 |
|------------|----------------------|------------|
| **MarioController** (移动/跳跃) | 移动平台 (MovingPlatform) | 修改速度或重力可能导致 Mario 无法平稳站在移动平台上，或者被甩飞。 |
| **TricksterController** (移动) | 伪装系统 (DisguiseSystem) | 修改基础移动可能影响伪装后的减速逻辑，导致伪装时移动过快或无法移动。 |
| **DisguiseSystem** (伪装) | 碰撞检测 (Layer/Tag) | 伪装会改变 Collider 大小和图层，可能导致 Trickster 掉出地图或被错误检测。 |
| **InputManager** (输入) | 所有角色控制、UI 暂停 | 修改按键映射可能导致某些操作失效或冲突。 |
| **GameManager** (游戏状态) | 胜负判定、暂停系统 | 修改状态机可能导致游戏无法正常结束或暂停失效。 |

</details>

---

## 📝 六、用户反馈模板

当发现 Bug 或有新需求时，请使用以下模板向 AI 提交反馈，以提高解决效率。

```markdown
### 问题描述
[一句话描述问题，例如：Mario 在冰雪关卡向左走时会穿墙]

### 重现步骤
1. [步骤 1，例如：生成 Classic Plains 测试关卡]
2. [步骤 2，例如：控制 Mario 走到坐标 (X, Y) 的墙壁处]
3. [步骤 3，例如：按住 A 键不放]

### 预期结果
[例如：Mario 应该被墙壁挡住]

### 实际结果
[例如：Mario 穿过了墙壁并掉出了地图]

### 补充信息
* 发生问题的对象/预制体：[例如：Mario 角色]
* 报错信息 (Console)：[如果有，请粘贴]
* 尝试过的解决办法：[例如：重新生成关卡，但问题依旧]
```
