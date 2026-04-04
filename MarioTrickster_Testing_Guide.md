# MarioTrickster 测试指南

> 本文档覆盖五部内容：手动 Play 测试、自动化 Test Runner 测试、伪装系统 Sprite 配置指引、调试信息说明、**回归影响矩阵与修复流程**。
> 更新时间：2026-04-02 (Session 13)

---

## 一、准备工作

### 1.1 生成测试场景

1. **File → New Scene → Basic 2D**（创建空白场景）
2. 菜单栏 **MarioTrickster → Build Test Scene**
3. 弹出确认对话框，点击 **生成**
4. **Ctrl+S** 保存场景（建议命名为 `TestScene`）

### 1.2 配置伪装系统 Sprite（必须手动完成）

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

### 1.3 获取 Sprite 的三种方式

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

### 1.4 推荐的伪装配置（添加 3 个形态）

| 序号 | Disguise Name | Sprite | Collider Size | Type |
|------|--------------|--------|---------------|------|
| 0 | Brick Block | Square Sprite | (1, 1) | Static |
| 1 | Spike Trap | Triangle Sprite | (1, 0.5) | Hazard |
| 2 | Slime Enemy | Circle Sprite | (0.8, 0.8) | Enemy |

配置完成后，屏幕右上角的调试信息应该从 `❌ 未配置伪装形态` 变为 `✅ 就绪: 当前选中 [Brick Block]，按 P 伪装`。

---

## 二、手动 Play 测试（按 Play 后操作）

点击 Unity 的 **Play** 按钮进入游戏，按以下顺序逐项测试。

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

### 测试 5：道具操控能力（需要先完成伪装并融入） ✅ 已完成

**前置条件**：Trickster 已伪装且已完全融入场景（静止 1.5 秒以上）。

| 操作 | 按键 | 预期结果 |
|------|------|----------|
| 走到可操控道具附近（橙色陷阱或棕色方块） | 方向键 | 靠近道具（2 格范围内） |
| 伪装并等待融入 | P → 等 1.5 秒 | 调试信息显示 `(已融入)`，消耗 25 能量 |
| 触发操控 | L | 附近的道具进入 **预警阶段**（闪烁变红 + 震动，持续约 0.8 秒），消耗 20 能量 |
| 观察爆发阶段 | — | 预警结束后，道具执行阻碍动作（陷阱伸出尖刺/方块消失等） |
| 观察冷却阶段 | 再按 L | 道具处于冷却中，无法立即再次触发 |
| 能量不足测试 | 连续多次操控 | 能量不足时按 L，屏幕提示"能量不足"，无法操控 |

**通过标准**：Telegraph→Active→Cooldown 三阶段流程正常，能量消耗和提示正常。

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

---

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

### 测试 9：关卡设计系统 (Session 15 新增) ⬜ 待测试

**前置条件**：使用 `MarioTrickster → Build Test Scene` 重新生成测试场景，场景中会自动包含所有 9 种新关卡元素。

| 元素 | 角色 | 操作 | 预期结果 |
|------|------|------|----------|
| **SpikeTrap** (地刺) | Mario | 碰到伸出的地刺 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 地刺强制伸出或切换伸缩频率 |
| **PendulumTrap** (摆锤) | Mario | 碰到摆动的锤头 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 摆锤摆幅增大、速度加快 |
| **FireTrap** (火焰) | Mario | 碰到喷射的火焰 | 受到伤害并被击退 |
| | Trickster | 伪装融入后按 L 操控 | 强制喷射火焰或加速喷射频率 |
| **BouncingEnemy** (弹跳怪) | Mario | 从侧面碰到怪物 | 受到伤害并被击退 |
| | Mario | 从上方踩踏怪物 | 怪物被消灭，Mario 获得向上弹力 |
| | Trickster | 伪装融入后按 L 操控 | 怪物弹跳高度增加、速度加快 |
| **BouncyPlatform** (弹跳平台) | Mario | 跳到平台上方 | 自动被向上弹射，平台播放挤压动画 |
| | Trickster | 伪装融入后按 L 操控 | 平台的弹射力大幅增加 |
| **OneWayPlatform** (单向平台) | Mario | 从平台下方往上跳 | 可以穿过平台并落在上面 |
| | Mario | 站在平台上按 S (下蹲) | 从平台上方穿过落下 |
| **CollapsingPlatform** (崩塌平台) | Mario | 站上平台 | 平台开始抖动，短时间后掉落，几秒后自动重生 |
| | Trickster | 伪装融入后按 L 操控 | 平台立即崩塌掉落 |
| **HiddenPassage** (隐藏通道) | Mario | 走到入口处按 S (下蹲) | 被传送到出口位置 |
| | Trickster | 伪装融入后按 L 操控 | 通道被封锁，Mario 无法进入 |
| **FakeWall** (伪装墙) | Mario | 走进看起来像墙的区域 | 墙壁变半透明，显示内部隐藏空间 |
| | Trickster | 伪装融入后按 L 操控 | 伪装墙变为真实物理墙壁，阻挡 Mario |

---

## 三、自动化 Test Runner 测试

### 3.1 运行方式说明

所有自动化测试都通过 **Unity 内置 Test Runner** 运行：

> **打开方式**：`Window → General → Test Runner`

内置 Test Runner 分为两个标签：**EditMode**（不需要运行游戏）和 **PlayMode**（自动进入 Play 模式）。切换到对应标签后点击 **Run All** 即可。

**需要导出完整报告给 AI 修复时**，使用 TestReportRunner 工具（Session 13 新增）：

| 菜单路径 | 功能 |
|---------|------|
| `MarioTrickster → Run Tests → Export Full Report (EditMode)` | 运行所有 EditMode 测试并导出报告 |
| `MarioTrickster → Run Tests → Export Full Report (PlayMode)` | 运行所有 PlayMode 测试并导出报告 |
| `MarioTrickster → Run Tests → Export Full Report (All)` | 运行所有测试并导出合并报告 |
| `MarioTrickster → Open Last Test Report` | 打开上次生成的报告文件 |

TestReportRunner 与内置 Test Runner 运行的是**同一套测试**，区别仅在于它会将所有错误一次性导出到 `TestReport.txt`（项目根目录），包含“快速复制区”方便直接发给 AI 统一修复。

### 3.2 EditMode 测试（不需要运行游戏）

这些测试在编辑器中直接执行，验证代码结构和静态逻辑：

| 测试类别 | 测试名称 | 验证内容 |
|----------|----------|----------|
| **MarioController** | MarioController_RequiresRigidbody2D | 挂载 MarioController 时自动添加 Rigidbody2D |
| | MarioController_RequiresBoxCollider2D | 挂载时自动添加 BoxCollider2D |
| | MarioController_HasPublicInputMethods | SetMoveInput/OnJumpPressed/OnJumpReleased 方法存在且可调用 |
| | MarioController_HasPlatformVelocityMethod | SetPlatformVelocity/ClearPlatformVelocity 方法存在 |
| **TricksterController** | TricksterController_RequiresRigidbody2D | 挂载时自动添加 Rigidbody2D |
| | TricksterController_RequiresBoxCollider2D | 挂载时自动添加 BoxCollider2D |
| | TricksterController_HasDisguiseInputMethod | OnDisguisePressed 方法存在且可调用 |
| | TricksterController_HasAbilityInputMethod | OnAbilityPressed 方法存在且可调用 |
| | TricksterController_HasSwitchDisguiseMethod | SwitchDisguise 方法存在且可调用 |
| **PlayerHealth** | PlayerHealth_InitializesWithMaxHealth | 初始生命值 = maxHealth (3) |
| | PlayerHealth_TakeDamage_ReducesHealth | 受伤后生命值减少 |
| | PlayerHealth_TakeDamage_TriggersInvincibility | 受伤后进入无敌帧 |
| | PlayerHealth_TakeDamage_WhileInvincible_DoesNothing | 无敌期间受伤无效 |
| | PlayerHealth_TakeDamage_FiresDeathEvent_WhenHealthReachesZero | 生命值归零时触发 OnDeath 事件 |
| | PlayerHealth_Heal_IncreasesHealth | 治疗后生命值增加 |
| | PlayerHealth_Heal_DoesNotExceedMax | 治疗不会超过最大生命值 |
| | PlayerHealth_ResetHealth_RestoresFullHealth | ResetHealth 恢复满血并清除无敌状态 |
| **DisguiseSystem** | DisguiseSystem_InitialState_NotDisguised | 初始状态未伪装、未融入 |
| | DisguiseSystem_Disguise_WithoutConfig_DoesNothing | 无配置时调用 Disguise() 不会崩溃 |
| | DisguiseSystem_GetDebugStatus_ReportsEmptyConfig | 无配置时 GetDebugStatus 返回提示信息 |
| **MovingPlatform** | MovingPlatform_RequiresRigidbody2D | 挂载时自动添加 Rigidbody2D |
| | MovingPlatform_SetsKinematic | Rigidbody2D 自动设为 Kinematic |
| **GoalZone/KillZone** | GoalZone_RequiresBoxCollider2D | GoalZone 挂载时自动添加 BoxCollider2D |
| | GoalZone_SetsTrigger | GoalZone 的 BoxCollider2D 自动设为 Trigger |
| | KillZone_RequiresBoxCollider2D | KillZone 挂载时自动添加 BoxCollider2D |
| | KillZone_SetsTrigger | KillZone 的 BoxCollider2D 自动设为 Trigger |
| **GameManager** | GameManager_InitialState_IsWaitingToStart | 初始状态为 WaitingToStart |
| | GameManager_HasSingletonProperty | 单例模式可正常访问 |
| **InputManager** | InputManager_HasPlayerSetterMethods | 可以设置 Mario/Trickster 控制器引用 |
| | InputManager_DisableEnableInput | EnableAllInput/DisableAllInput 正常工作 |
| **IControllableProp** | ControllableHazard_ImplementsIControllableProp | ControllableHazard 实现 IControllableProp 接口 |
| | ControllableBlock_ImplementsIControllableProp | ControllableBlock 实现 IControllableProp 接口 |
| | ControllablePlatform_ImplementsIControllableProp | ControllablePlatform 实现 IControllableProp 接口 |
| **EnergySystem** | EnergySystem_InitializesWithMaxEnergy | 初始能量 = maxEnergy (100) |
| | EnergySystem_HasEnoughForDisguise_InitiallyTrue | 初始时能量足够变身 |
| | EnergySystem_HasEnoughForControl_InitiallyTrue | 初始时能量足够操控 |
| | EnergySystem_TryConsumeDisguiseCost_ReducesEnergy | 变身消耗后能量减少 |
| | EnergySystem_TryConsumeControlCost_ReducesEnergy | 操控消耗后能量减少 |
| | EnergySystem_ResetEnergy_RestoresMax | 重置后能量恢复满值 |
| | EnergySystem_AddEnergy_IncreasesButCapsAtMax | 添加能量不超过上限 |
| | EnergySystem_IsLowEnergy_WhenBelowThreshold | 低于阈值时 IsLowEnergy=true |
| | EnergySystem_OnEnergyChanged_EventFires | 能量变化时触发事件 |
| **ScanAbility** | ScanAbility_InitialState_IsReady | 初始状态 IsReady=true |
| | ScanAbility_ActivateScan_TriggersCooldown | 扫描后进入冷却 |
| | ScanAbility_ActivateScan_FiresEvent | 扫描触发 OnScanActivated 事件 |
| | ScanAbility_ActivateScan_WhileOnCooldown_DoesNothing | 冷却中扫描无效 |
| | ScanAbility_ScanRadius_IsPositive | 扫描半径 > 0 |
| | ScanAbility_OnScanResult_FiresWithFalse_WhenNoTrickster | 无 Trickster 时扫描结果为 false |
| | ScanAbility_OnScanPerformed_EventFires | 扫描触发 OnScanPerformed 事件 (B015 修复验证) |
| **CameraController** | CameraController_CanBeCreated | CameraController 能正常挂载 |
| | CameraController_SetTarget_DoesNotThrow | SetTarget 不抛异常 |
| | CameraController_SetBounds_DoesNotThrow | SetBounds 不抛异常 |
| | CameraController_SnapToTarget_WithoutTarget_DoesNotThrow | 无目标时 SnapToTarget 不崩溃 |
| | CameraController_Shake_DoesNotThrow | Shake 不抛异常 |
| **GameUI** | GameUI_CanBeAddedToGameObject | GameUI 能正常添加到 GameObject |
| | GameUI_ShowGameOverScreen_SetsShowGameOverFlag | GameUI 创建和基本状态正确 |
| | GameUI_OnGUIFallback_DefaultsToTrue | 无 Canvas 时 OnGUI 后备默认启用 |
| | GameUI_ShowAbilityFailFeedback_DoesNotThrow | 失败提示不抛异常 |
| | GameUI_ButtonCallbacks_DoNotThrow_WithoutGameManager | 无 GameManager 时按钮回调不崩溃 |

**预期结果**：全部绿色通过（59 个测试用例）。

### 3.3 PlayMode 测试（需要进入 Play 模式）

切换到 **PlayMode** 标签 → 点击 **Run All**

Unity 会自动进入 Play 模式执行测试，验证运行时行为：

| 测试类别 | 测试名称 | 验证内容 |
|----------|----------|----------|
| **Mario 移动** | Mario_MoveRight_IncreasesXPosition | 给右方向输入后，Mario X 坐标增加 |
| | Mario_MoveLeft_DecreasesXPosition | 给左方向输入后，Mario X 坐标减少 |
| | Mario_StopInput_Decelerates | 松开输入后，Mario 减速停止 |
| | Mario_Gravity_PullsDown | 无地面时，Mario 因重力下落 |
| **Mario 跳跃** | Mario_Jump_IncreasesYPosition | 按跳跃后，Mario Y 坐标增加 |
| | Mario_Bounce_SetsUpwardVelocity | 调用 Bounce() 后，Mario 获得向上速度 |
| **Mario 状态** | Mario_IsMoving_ReflectsMovement | 有输入时 IsMoving=true，无输入时 IsMoving=false |
| | Mario_Die_DisablesController | 调用 Die() 后控制器被禁用 |
| **Trickster** | Trickster_MoveRight_IncreasesXPosition | Trickster 向右移动，X 坐标增加 |
| | Trickster_IsDisguised_ReturnsFalse_WhenNoDisguiseSystem | 无 DisguiseSystem 时 IsDisguised=false |
| **伪装系统** | DisguiseSystem_ToggleDisguise_WithoutConfig_StaysUndisuised | 运行时无配置不崩溃，保持未伪装 |
| | DisguiseSystem_Cooldown_PreventsImmedateReDisguise | 解除伪装后冷却期间无法再次伪装 |
| **道具状态机** | ControllableHazard_Activate_GoesToTelegraphThenActive | 触发后从 Idle→Telegraph→Active 正确转换 |
| | ControllableBlock_Activate_GoesToTelegraphThenActive | 同上 |
| **移动平台** | MovingPlatform_Moves_BetweenPoints | 平台在两点间来回移动 |
| **胜负判定** | GameManager_MarioReachesGoal_EndRoundMarioWins | Mario 碰到 GoalZone 后触发 EndRound，Mario 胜利 |
| | GameManager_MarioDies_EndRoundTricksterWins | Mario 死亡后触发 EndRound，Trickster 胜利 |
| | GameManager_ResetRound_RestoresHealth | 回合重置后 Mario 恢复满血 |
| **暂停** | GameManager_Pause_StopsTime | 暂停时 TimeScale=0，恢复时 TimeScale=1 |
| **InputManager** | InputManager_DisableInput_StopsPlayerMovement | 禁用输入后 Mario 不再移动 |

**预期结果**：全部绿色通过（21 个测试用例）。

### 3.4 测试失败怎么办？

| 情况 | 可能原因 | 解决方法 |
|------|----------|----------|
| EditMode 全部失败 | Assembly 引用问题 | 确认 `git pull` 后 Unity 重新编译无错误 |
| 个别 PlayMode 失败 | 物理帧时序差异 | 再运行一次，偶发的帧时序问题可忽略 |
| DisguiseSystem 测试失败 | 测试自带模拟数据，不依赖场景配置 | 检查 Console 错误信息 |
| 大量红色失败 | 编译错误导致连锁失败 | 先解决 Console 中的编译错误 |

---

## 四、回归影响矩阵与修复流程

> 解决的问题：修复 Bug 时修改的代码可能影响已通过或未测试的其他功能，导致“修了一个 Bug，引入两个新 Bug”。

### 4.1 影响矩阵：脚本 → 测试项依赖关系

下表定义了每个核心脚本被修改时，哪些手动测试项需要回归验证：

| 修改的脚本 | 直接影响的测试项 | 间接可能影响 | 说明 |
|------------|----------------|------------|------|
| MarioController.cs | 测试 1、6.5 | 测试 3、7 | 移动/跳跃改变影响平台跟随和终点触发 |
| TricksterController.cs | 测试 2 | 测试 4、5、6 | 移动改变影响伪装/操控/扫描检测 |
| PlayerHealth.cs | 测试 7 | 测试 8 | 生命值影响胜负判定和重置 |
| DisguiseSystem.cs | 测试 4 | 测试 5、6 | 伪装影响操控前置条件和扫描检测 |
| GameManager.cs | 测试 7、8 | 测试 1-6 | 状态机/重置/暂停影响全局行为 |
| GameUI.cs | 测试 7、8 | 测试 5 | UI显示影响胜负画面和能量不足提示 |
| GoalZone.cs | 测试 7 | — | 终点触发逻辑相对独立 |
| KillZone.cs | 测试 7 | — | 死亡区域逻辑相对独立 |
| InputManager.cs | 测试 1、2 | 测试 4、5、6、8 | 输入映射影响所有操作 |
| CameraController.cs | 测试 6.5 | — | 镜头逻辑独立，不影响游戏玩法 |
| MovingPlatform.cs | 测试 3 | — | 平台逻辑独立 |
| EnergySystem.cs | 测试 5 | 测试 4 | 能量影响操控和变身 |
| ScanAbility.cs | 测试 6 | — | 扫描逻辑独立 |
| ControllablePropBase.cs | 测试 5 | — | 道具基类影响所有道具行为 |
| ControllableBlock/Hazard/Platform.cs | 测试 5 | — | 具体道具效果 |
| LevelElementBase.cs 等关卡框架 | 测试 9 | 测试 5 | 关卡元素基础逻辑 |
| SpikeTrap.cs 等 9 种具体元素 | 测试 9 | — | 具体关卡元素表现 |
| KnockbackHelper.cs | 测试 7、9 | 测试 1、2 | Session 18: 统一击退方向计算，影响所有伤害源 |
| MarioInteractionHelper.cs | 测试 9 | — | Session 18: S键下蹲交互路由（单向平台下落+隐藏通道传送） |
| DamageDealer.cs | 测试 7、9 | — | Session 18: 使用 KnockbackHelper 统一击退 |
| TestSceneBuilder.cs | **测试 1-9 全部** | — | 场景生成影响所有测试的前置条件 |

### 4.2 AI 修复 Bug 标准流程

AI 每次修复 Bug 时，**必须**执行以下流程：

1. **修复代码**：实施 Bug 修复
2. **查询影响矩阵**：根据修改的脚本，查找“直接影响”和“间接可能影响”的测试项
3. **生成回归清单**：在 SESSION_TRACKER.md 的“回归验证清单”中标记需要重测的项目
4. **更新测试进度**：将受影响的已通过测试项从 ✅ 改为 🔄（需回归验证）
5. **通知用户**：在修复报告中明确列出回归验证清单

### 4.3 用户测试流程（更新后）

```text
1. git pull 拉取修复
2. 查看 SESSION_TRACKER.md 第 3 节“测试进度总览”
3. 优先测试当前测试项（按顺序）
4. 然后测试所有标记为 🔄 的回归项（只需快速验证核心功能，不需全量重测）
5. 在反馈模板中填写所有结果（含回归项）
6. 自动化测试作为最终安全网：EditMode + PlayMode 全部 Run All
```

### 4.4 回归验证简化规则

回归验证不需要重复完整测试流程，只需快速确认核心功能未被破坏：

| 测试项 | 回归验证简化操作（约30秒） |
|--------|----------------------------|
| 测试 1 | Mario WASD 移动 + Space 跳跃，确认手感正常 |
| 测试 2 | Trickster 方向键移动 + 跳跃，确认正常 |
| 测试 3 | Mario 站上平台，确认不被甩飞 |
| 测试 4 | 按 P 伪装→再按 P 解除，确认外观切换正常 |
| 测试 5 | 伪装融入后按 L，确认道具有反应 |
| 测试 6 | 按 Q 扫描，确认脉冲出现且颜色正确 |
| 测试 6.5 | Mario 左右跑动，确认镜头平滑无晃动 |
| 测试 7 | Mario 到终点，确认胜利画面显示 |
| 测试 8 | ESC 暂停→ESC 恢复，确认正常 |
| 测试 9 | 快速触发一次地刺伤害、踩一次弹跳怪、穿一次单向平台 |

### 4.5 实例：Session 12 的回归影响分析

以 Session 12 为例，展示影响矩阵如何工作：

| 修复 | 修改的脚本 | 直接影响 | 间接影响 | 实际回归需求 |
|------|----------|---------|---------|----------|
| B018 GameUI缺失 | TestSceneBuilder.cs | 测试 1-8 全部 | — | 需要 Clear+Build 后全部重测 |
| B019 序列化冲突 | ControllablePropBase.cs | 测试 5 | — | 快速验证道具操控 |
| B020 终点重置 | GoalZone.cs, GameManager.cs | 测试 7 | 测试 8 | 多回合终点测试 |
| B021 RESUMED移除 | GameManager.cs, GameUI.cs | 测试 7、8 | 测试 5 | 暂停恢复 + 胜负UI |

用户实际反馈：测试 1-8 全部通过，证明回归无问题。

---

## 五、操作键位速查表

| 操作 | Player1 (Mario) | Player2 (Trickster) |
|------|-----------------|---------------------|
| 移动 | WASD | 方向键 |
| 跳跃 | Space | ↑ / 右Ctrl / 小键盘0 |
| **扫描技能** | **Q / 手柄东键** | — |
| 伪装/取消伪装 | — | **P** |
| 切换伪装形态（下一个） | — | **O** |
| 切换伪装形态（上一个） | — | **I** |
| 操控道具 | — | **L** |
| 暂停 | ESC | ESC |
| 快速重启 | F5 | F5 |
| **回合结束后重启** | **R** | **R** |
| **回合结束后下一回合** | **N** | **N** |

---

## 六、调试信息说明

运行游戏后，屏幕右上角会显示 Trickster 的伪装系统状态和能量信息：

### 5.1 伪装系统状态

| 显示内容 | 含义 | 需要做什么 |
|----------|------|------------|
| `❌ 未配置伪装形态！` | Available Disguises 列表为空 | 按照 1.2 节添加伪装形态 |
| `❌ 伪装形态[0]的 Disguise Sprite 未设置！` | 添加了形态但没设置 Sprite | 给 Disguise Sprite 字段拖入一个 Sprite |
| `✅ 就绪: 当前选中 [Brick Block]，按 P 伪装` | 配置正确，可以伪装 | 按 P 开始伪装 |
| `✅ 已伪装为: Brick Block (未融入)` | 已伪装但还在移动 | 停止移动等待 1.5 秒 |
| `✅ 已伪装为: Brick Block (已融入)` | 已完全融入场景 | 可以按 L 操控道具 |
| `⏳ 伪装冷却中: 1.5s` | 解除伪装后的冷却期 | 等待冷却结束 |

### 5.2 能量系统状态 (Session 10 新增)

| 显示内容 | 含义 |
|----------|------|
| `Energy: 100/100` | 当前能量值 / 最大能量值 |
| `Energy: 55/100` | 变身或操控后能量减少，正在自然恢复 |
| `⚠️ LOW ENERGY` | 能量低于 20，变身和操控可能受限 |
| 屏幕中央“能量不足”提示 | 尝试操控或变身但能量不够 | 等待能量自然恢复后再试 |

### 5.3 扫描技能状态 (Session 10 新增)

| 视觉效果 | 含义 |
|----------|------|
| 蓝色脉冲圆环扩散 | Mario 发动扫描，未检测到 Trickster |
| 红色脉冲圆环扩散 | Mario 发动扫描，检测到了伪装的 Trickster |
| Trickster 红色闪烁 + 头顶 `!` | Trickster 被扫描揭示，持续 2 秒 |
| 按 Q 无反应 | 扫描技能处于冷却中（8秒） |

---

## 七、测试进度总览

> 更新时间：2026-04-02 (Session 13)

| 测试项 | 状态 | 备注 |
|----------|------|------|
| 测试 1：Mario 基础移动与跳跃 | ✅ 已通过 | 移动流畅、跳跃手感正常 |
| 测试 2：Trickster 基础移动 | ✅ 已通过 | 移动正常 |
| 测试 3：移动平台跟随 | ✅ 已通过 | 平台跟随平稳，无甩飞 |
| 测试 4：伪装变身系统 | ✅ 已通过 | 伪装/解除/冷却/融入均正常 |
| 测试 5：道具操控能力 | ✅ 已通过 | Telegraph→Active→Cooldown 流程正常 |
| 测试 6：扫描技能 | ✅ 已通过 | 脉冲颜色时序正常，扫描结果文字提示正常 |
| 测试 6.5：镜头系统 | ✅ 已通过 | 平滑跟随，无晃动 |
| 测试 7：胜负判定与UI | ✅ 已通过 | 多回合胜利/失败画面正常显示（B018/B020修复） |
| 测试 8：暂停系统 | ✅ 已通过 | ESC 暂停显示遮罩+PAUSED，恢复时直接回到游戏（B021修复） |
| 测试 9：关卡设计系统 | ⬜ 待测试 | 9 种新元素的物理交互与 Trickster 操控 |
| EditMode 自动化测试 | ✅ 已通过 | 114 个用例全部通过 (Session 15 新增 55 个关卡测试) |
| PlayMode 自动化测试 | ✅ 已通过 | 21 个用例全部通过 |

**手动测试进度：9/10 通过（测试 9 待测）。**
