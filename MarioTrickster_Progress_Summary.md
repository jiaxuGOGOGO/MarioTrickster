# MarioTrickster 项目进度总结

> 更新时间：2026-04-02 (Session 10) | 单一真相源：AI 新对话时自动读取本文件获取完整上下文

---

## 一、项目概述

**MarioTrickster** 是一款非对称双人本地对抗平台跳跃游戏。玩家1操控Mario（闯关者），玩家2操控Trickster（伪装者，可变身为障碍物/怪物阻止Mario）。引擎为 Unity 2022.3 LTS，使用 2D Core 模板。

| 项目信息 | 详情 |
|---------|------|
| 引擎 | Unity 2022.3 LTS (2D Core) |
| IDE | JetBrains Rider |
| 语言 | C# |
| 版本控制 | Git + GitHub |
| 仓库地址 | https://github.com/jiaxuGOGOGO/MarioTrickster |
| 本地路径 | E:\unity project\exercise1\MarioTrickster |
| 文档工具 | Obsidian（使用 `[[#heading]]` 链接格式） |

---

## 二、已完成的功能清单

### 2.1 基础设施（全部完成）

| 功能 | 状态 | 说明 |
|------|------|------|
| Unity 项目创建 | ✅ | 2D Core 模板，已初始化 |
| GitHub 仓库连接 | ✅ | 已推送初始提交 |
| .gitignore 配置 | ✅ | Unity 专用配置 |
| Git 工作流建立 | ✅ | add → commit → push 流程确认可用 |

### 2.2 设计文档（全部完成）

| 文档 | 状态 | 位置 | 内容概要 |
|------|------|------|---------|
| 综合调研报告 | ✅ | /home/ubuntu/Mario_Asymmetric_Research.md | 游戏概念、核心机制、相机方案、平衡设计、美术管线、引擎选型、开发时间线、竞品分析、行动计划 |
| AI协作工作流 | ✅ | /home/ubuntu/AI_Collaboration_Workflow.md | 9章：对话模板、Bug报告模板、功能开发流程、Git命令参考、Unity项目结构标准、场景通信模板、最佳实践、里程碑清单 |

### 2.3 开源资源调研（全部完成）

已完成对 itch.io 和 GitHub 的系统性搜索，找到以下高价值参考：

**GitHub 开源项目（可直接借鉴代码）：**

| 项目 | Stars | 许可证 | 用途 | URL |
|------|-------|--------|------|-----|
| zigurous/unity-super-mario-tutorial | 185 | — | Mario完整复刻，物理/关卡/手感参考 | https://github.com/zigurous/unity-super-mario-tutorial |
| Matthew-J-Spencer/Ultimate-2D-Controller | 2.1k | MIT | 最佳2D控制器（coyote time/buffered jump等） | https://github.com/Matthew-J-Spencer/Ultimate-2D-Controller |
| ntrkd/Momentum | 15 | MIT | 状态机架构2D控制器，适合扩展变身状态 | https://github.com/networkydev/Momentum |
| UnityTechnologies/InputSystem_Warriors | 615 | — | Unity官方本地多人输入系统示例 | https://github.com/UnityTechnologies/InputSystem_Warriors |
| molleindustria/local-multiplayer-unity | 2 | MIT | 简洁本地多人模板（输入/玩家/分屏管理） | https://github.com/molleindustria/local-multiplayer-unity |
| FadrikAlexander/Among-Us-Imposter-Recreation | 32 | — | 伪装者机制复刻（移动/击杀/碰撞/通风管/破坏） | https://github.com/FadrikAlexander/Among-Us-Imposter-Recreation |
| nicholas-maltbie/PropHunt | 12 | MIT | PropHunt变身为道具的机制参考 | https://github.com/nicholas-maltbie/PropHunt |
| Naphann/Trap-Master | — | — | Trap Master: 陷阱操控机制参考（Session 3 新增调研） | https://github.com/Naphann/Trap-Master |

**itch.io 免费像素素材（可直接使用）：**

| 素材包 | 许可证 | 内容 | URL |
|--------|--------|------|-----|
| Pixel Frog - Pixel Adventure 1 & 2 | CC0 | 角色/物体/地块/道具/20种敌人 | https://pixelfrog-assets.itch.io/pixel-adventure-1 |
| Block Land 16x16 | CC0 | Mario/Minecraft风格地块集 | itch.io 搜索 "Block Land" |
| JuhoSprite Mario World风格 | — | 角色/敌人/3个世界 | https://juhosprite.itch.io/ |
| Brackeys' Platformer Bundle | — | 角色/地块/音效/音乐 | https://brackeysgames.itch.io/brackeys-platformer-bundle |
| Sunny Land (ansimuz) | — | 完整2D平台跳跃素材集 | https://ansimuz.itch.io/sunny-land-pixel-game-art |
| Monsters Creatures Fantasy | — | 怪物/敌人角色素材 | https://luizmelo.itch.io/monsters-creatures-fantasy |

**itch.io 游戏玩法参考：**

| 游戏 | 类型 | 参考价值 |
|------|------|---------|
| Panoptic | 1v1非对称VR vs PC | 非对称视角设计 |
| Daedalus Versus Minotaur | 非对称迷宫 | 一人设障一人闯关 |
| Monica and the Monster Cloak | 变身为怪物平台跳跃 | 最接近"伪装为怪物"的核心机制 |
| MechShift | 变形机甲平台跳跃 | 变形机制在平台跳跃中的应用 |
| Run like Hell | 非对称合作跑酷 | 平台跳跃+非对称 |
| Crawl | 非对称地牢 | Ghost 操控陷阱/怪物阻碍英雄（Session 3 新增参考） |

---

### 2.4 MVP 核心脚本（Sprint 1 全部完成）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| MarioController.cs | Assets/Scripts/Player/ | ✅ **Session 6 更新** | Tarodev 帧速度架构 + 平台速度注入法跟随 + 手感调优（acceleration=160, groundDeceleration=200） |
| PlayerHealth.cs | Assets/Scripts/Player/ | ✅ | 通用生命值/无敌帧/受伤闪烁/死亡事件 |
| TricksterController.cs | Assets/Scripts/Enemy/ | ✅ **Session 7 更新** | 与 MarioController 一致的帧速度架构 + 平台速度注入 + 手感调优；OnGUI 显示伪装系统实时状态 |
| DisguiseSystem.cs | Assets/Scripts/Enemy/ | ✅ **Session 7 更新** | Sprite替换变身/冷却/场景融入/多形态切换；新增 GetDebugStatus() 调试方法 |
| InputManager.cs | Assets/Scripts/Core/ | ✅ **Session 7 更新** | 修复 OnJumpReleased 每帧触发问题，用 wasJumpHeld 状态机精确检测按下/松开事件；P2 键位改为 POIL 方案 |
| GameManager.cs | Assets/Scripts/Core/ | ✅ **Session 10 更新** | 游戏状态/胜负判定/暂停/重启/计时器/单例模式（修复按键检测逻辑） |
| CameraController.cs | Assets/Scripts/Camera/ | ✅ | 平滑跟随Mario/前瞻偏移/死区/关卡边界限制/相机震动 |

### 2.5 辅助脚本（Sprint 1）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| GoalZone.cs | Assets/Scripts/Core/ | ✅ | 终点触发器，Mario碰触后触发胜利 |
| KillZone.cs | Assets/Scripts/Core/ | ✅ | 死亡区域（掉落深渊），碰触即死 |
| DamageDealer.cs | Assets/Scripts/Core/ | ✅ | 通用伤害触发器（尖刺/怪物/Hazard伪装），支持击退 |
| Collectible.cs | Assets/Scripts/Core/ | ✅ | 可收集物品（金币/回血/加速），带浮动动画 |
| Breakable.cs | Assets/Scripts/Core/ | ✅ | 可破坏方块（砖块/问号砖块），从下方顶撞触发 |
| MovingPlatform.cs | Assets/Scripts/Core/ | ✅ **Session 6 重写** | Kinematic Rigidbody2D + MovePosition() + 速度注入法跟随角色（不使用 SetParent） |
| SimpleEnemy.cs | Assets/Scripts/Enemy/ | ✅ | 简单巡逻敌人，边缘/墙壁检测自动转向，可被踩消灭 |
| GameUI.cs | Assets/Scripts/UI/ | ✅ **Session 10 更新** | 基础HUD（生命值/计时器/回合信息/胜负画面/能量显示），增强了游戏结束/暂停/操控失败反馈 |
| LevelManager.cs | Assets/Scripts/Core/ | ✅ | 关卡管理（出生点/边界/可伪装对象列表） |

### 2.6 Trickster 能力系统（Sprint 2 - Session 3 新增）

**核心机制**: Trickster 变身为关卡道具后，可以操控该道具阻碍 Mario。操控采用 **Telegraph（预警）→ Active（爆发）→ Cooldown（冷却）** 三阶段设计，预警阶段给 Mario 反应窗口，Trickster 需要预判操控时机。参考 Crawl 游戏的 Ghost Possess Trap 机制。

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| IControllableProp.cs | Assets/Scripts/Ability/ | ✅ | 可操控道具接口（PropName/CanBeControlled/OnTricksterActivate/状态查询） |
| ControllablePropBase.cs | Assets/Scripts/Ability/ | ✅ | 抽象基类，封装 Telegraph→Active→Cooldown 状态机、预警闪烁/震动视觉效果、次数限制、回合重置 |
| TricksterAbilitySystem.cs | Assets/Scripts/Ability/ | ✅ | 核心能力管理器，检测伪装状态、绑定/检测附近道具、处理操控输入、管理操控次数/时间限制 |
| ControllablePlatform.cs | Assets/Scripts/Ability/ | ✅ **Session 6 更新** | 可操控移动平台，4种模式：Rush/Drop/Reverse/Stop + 速度注入法跟随 |
| ControllableHazard.cs | Assets/Scripts/Ability/ | ✅ | 可操控危险道具，4种模式：Spike/Expand/Burst/Directional |
| ControllableBlock.cs | Assets/Scripts/Ability/ | ✅ | 可操控方块，3种模式：Vanish/Slide/Bounce |

**能力系统调用关系**:
```
InputManager (右Alt/手柄Y)
  → TricksterController.OnAbilityPressed()
    → TricksterAbilitySystem.OnAbilityPressed()
      → 检查 DisguiseSystem.IsDisguised && IsFullyBlended
      → 查找 IControllableProp (绑定/就近模式)
      → IControllableProp.OnTricksterActivate(direction)
        → ControllablePropBase 状态机: Telegraph → Active → Cooldown
          → 子类实现具体效果 (ControllablePlatform/Hazard/Block)
```

**可配置参数（全部通过 Inspector 暴露）**:
- 预警时长（telegraphDuration）: 默认 0.8 秒
- 激活持续时长（activeDuration）: 默认 1.5 秒
- 冷却时间（cooldownDuration）: 默认 3 秒
- 最大操控次数（maxUses）: -1 = 无限
- 操控检测半径（controlRange）: 默认 2
- 单次变身最大操控总次数（maxControlsPerDisguise）: -1 = 无限
- 操控持续时间限制（controlTimeLimit）: 0 = 无限

### 2.7 能量系统与扫描技能（Session 10 新增）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| EnergySystem.cs | Assets/Scripts/Ability/ | ✅ **Session 10 新增** | Trickster 能量系统。变身消耗20、操控道具消耗15，满值100，未变身时恢复8/秒，变身时不恢复。变身期间每秒5点持续消耗（融入后减半）。支持低能量警告、回合重置。 |
| ScanAbility.cs | Assets/Scripts/Ability/ | ✅ **Session 10 新增** | Mario 扫描技能。Q键/手柄东键触发，检测半径5格范围内的伪装Trickster，命中后红色闪烁2秒+头顶警告标记。冷却8秒。 |

**能量系统参数（通过 Inspector 暴露）**:

| 参数 | 默认值 | 说明 |
|------|--------|------|
| maxEnergy | 100 | 最大能量值 |
| disguiseCost | 20 | 变身消耗的固定能量 |
| controlCost | 15 | 操控道具消耗的固定能量 |
| disguiseDrainPerSecond | 5 | 变身期间每秒持续消耗 |
| blendedDrainMultiplier | 0.5 | 完全融入后维持消耗倍率 |
| regenPerSecond | 8 | 未变身时每秒恢复量 |
| disguisedRegenMultiplier | 0 | 变身时能量恢复倍率（0=不恢复） |
| regenDelayAfterControl | 2 | 操控道具后恢复延迟（秒） |
| lowEnergyThreshold | 0.25 | 低能量警告阈值（比例，即 25%） |

**扫描技能参数（通过 Inspector 暴露）**:

| 参数 | 默认值 | 说明 |
|------|--------|------|
| scanRadius | 5 | 扫描检测半径（格） |
| scanCooldown | 8 | 冷却时间（秒） |
| revealDuration | 2 | 揭示持续时间（秒） |
| pulseExpandTime | 0.3 | 脉冲扩散时间（秒） |

### 2.8 测试与工具系统（Session 8 新增）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| TestSceneBuilder.cs | Assets/Scripts/Editor/ | ✅ **Session 8 新增** | Editor 菜单工具，一键生成完整测试场景（地面/平台/角色/管理器/道具/终点/死亡区域/敌人/金币/相机），所有 Inspector 引用自动连线，Ground Layer 自动创建 |
| MarioTrickster.asmdef | Assets/Scripts/ | ✅ **Session 9 新增** | 主项目 Assembly Definition，引用 Unity.InputSystem |
| MarioTrickster.Editor.asmdef | Assets/Scripts/Editor/ | ✅ **Session 9 新增** | Editor 脚本 Assembly Definition |
| ComponentSetupTests.cs | Assets/Tests/EditMode/ | ✅ **Session 8 新增** | EditMode 测试（30+ 测试用例）：验证 RequireComponent 自动添加、组件初始状态、PlayerHealth 伤害/治疗/死亡/无敌帧逻辑、DisguiseSystem 初始状态、IControllableProp 接口实现、InputManager 启用/禁用 |
| GameplayTests.cs | Assets/Tests/PlayMode/ | ✅ **Session 8 新增** | PlayMode 测试（20+ 测试用例）：验证 Mario/Trickster 移动/跳跃/重力、伪装系统运行时行为、道具操控状态机（Telegraph→Active→Cooldown）、移动平台运动、GameManager 胜负判定/暂停/回合重置、InputManager 集成 |
| EditModeTests.asmdef | Assets/Tests/EditMode/ | ✅ **Session 8 新增** | EditMode 测试 Assembly Definition |
| PlayModeTests.asmdef | Assets/Tests/PlayMode/ | ✅ **Session 8 新增** | PlayMode 测试 Assembly Definition |

### 2.9 配置文件更新

| 文件 | 状态 | 说明 |
|------|------|------|
| Packages/manifest.json | ✅ **更新** | 新增 com.unity.inputsystem (1.7.0) 和 com.unity.cinemachine (2.9.7) 包依赖 |
| Assets/InputActions/GameControls.inputactions | ✅ **新增** | Unity Input System 配置文件（Player1/Player2/UI三个ActionMap） |

---

## 三、已知未修复的 Bug / 待确认事项

| 编号 | 描述 | 优先级 | 备注 |
|------|------|--------|------|
| B001 | ✅ **已解决 (Session 5)** 实际运行测试已完成，基础移动/跳跃/平台跟随均验证通过 | — | — |
| B002 | ✅ **已修复 (Session 4)** 全局替换 `linearVelocity` → `velocity`，适配 Unity 2022.3 | — | — |
| B003 | InputManager.cs 中 Player1 使用 WASD，Player2 使用方向键，但 MarioController 原代码中也绑定了方向键 | 低 | InputManager 已接管输入分发，MarioController 不直接读取键盘，所以不冲突。可关闭。 |
| B004 | ✅ **已解决 (Session 8)** 场景 SampleScene.unity 是空白模板 → 已创建 TestSceneBuilder 一键生成测试场景 | — | 用户在 Unity 中执行菜单 MarioTrickster → Build Test Scene 即可 |
| B005 | ✅ **已修复 (Session 4)** Mario/Trickster 无法移动 | — | 根因：Inspector 中未拖入引用 |
| B006 | ✅ **已修复 (Session 6)** 平台跟随问题 | — | 根因：SetParent 与 Dynamic Rigidbody2D 冲突。已改为速度注入法。 |
| B007 | ✅ **已修复 (Session 6)** 站上平台后角色被甩飞 | — | 根因：平台速度每帧累积。修复：读回 rb.velocity 时减去上一帧平台速度 `_lastPlatformVelocity`。 |
| B008 | ✅ **已修复 (Session 6)** 角色移动有打滑感 | — | 根因：groundDeceleration 过低(60)。修复：Mario 调至 200，Trickster 调至 200。 |
| B011 | ✅ **已修复 (Session 10)** 游戏结束后缺少明显的屏幕提示和重启引导 | — | 根因：GameManager.Update() 第一行直接 return 导致无法检测按键。已将按键检测移至状态检查之前，并增强了 GameUI 的结束画面。 |
| B012 | ✅ **已修复 (Session 10)** 道具操控失败时无提示 | — | 根因：操控失败仅有 Debug.Log。已在 TricksterController 添加 OnAbilityFailed 事件，GameUI 接收并显示失败原因（如"不在范围内"、"能量不足"）。 |
| B013 | ✅ **已修复 (Session 9)** TestSceneBuilder 中 LevelManager 字段名错误 | — | — |
| B014 | ✅ **已修复 (Session 10)** ESC 暂停后再按 ESC 恢复无反馈 | — | 根因：同 B011，暂停状态下 ESC 按键检测被跳过。已修复逻辑，并在 GameUI 添加了"已恢复"的短暂提示。 |
| B015 | ⚠️ **待修复 (Session 10)** 测试 6 扫描技能时，Trickster 在范围内且头顶出现警告标记，但屏幕下方错误提示"未在范围内" | 中 | 待排查 GameUI 或 ScanAbility 提示逻辑冲突 |

### 关于 B002 的修复方法

如果你使用的是 **Unity 2022.3 LTS**，`Rigidbody2D` 的属性名是 `velocity` 而非 `linearVelocity`。当前仓库中所有文件已使用 `velocity`，无需手动替换。

如果你使用的是 **Unity 6.x**，则需要将 `velocity` 替换为 `linearVelocity`。

---

## 四、本地操作指南（每次 AI 提交后必看）

每次 AI 完成代码编写并推送到 GitHub 后，请在本地执行以下步骤进行测试和验证：

### 1. 拉取最新代码
打开命令行工具（CMD/PowerShell/Git Bash），执行：
```cmd
cd /d "E:\unity project\exercise1\MarioTrickster"
git pull
```

### 2. Unity 环境配置（首次拉取时）
1. 打开 Unity 项目，等待 Input System 和 Cinemachine 包自动安装。
2. 如果弹出 "Enable New Input System" 提示，点击 **Yes** 并重启 Unity。
3. 如果控制台出现 `linearVelocity` 相关的编译错误，请参考上方 **B002 修复方法** 进行全局替换。

### 3. 一键生成测试场景（Session 8 新增）

**推荐方式：使用 TestSceneBuilder 自动生成**

1. 在 Unity 中新建一个空白场景（File → New Scene → Basic 2D）
2. 点击菜单栏 **MarioTrickster → Build Test Scene**
3. 在弹出的确认对话框中点击 **生成**
4. 按 **Ctrl+S** 保存场景（建议命名为 `TestScene`）
5. 点击 **Play** 即可开始测试

**自动生成的内容包括：**
- 主地面（40格宽）+ 3个高台 + 2面侧墙
- Mario（红色方块，位置 -8,1）+ Trickster（蓝色方块，位置 8,1）
- 管理器（GameManager + InputManager + LevelManager，引用已自动连线）
- 移动平台（左右移动）+ 可操控平台（上下移动）
- 可操控陷阱（橙色）+ 可操控方块（棕色）
- 终点区域（绿色半透明，位置 18,1）
- 死亡区域（关卡底部）
- 简单敌人（紫色）+ 5个金币 + 可破坏方块
- 相机跟随 Mario
- Ground Layer 自动创建和分配

**清空场景：** 菜单栏 MarioTrickster → Clear Test Scene

### 4. 运行自动化测试（Session 8 新增）

**EditMode 测试（不需要运行游戏）：**
1. 打开 Window → General → Test Runner
2. 切换到 **EditMode** 标签
3. 点击 **Run All**
4. 验证所有 30+ 测试用例全部通过（绿色）

**PlayMode 测试（需要运行游戏）：**
1. 在 Test Runner 中切换到 **PlayMode** 标签
2. 点击 **Run All**
3. Unity 会自动进入 Play 模式执行测试
4. 验证所有 20+ 测试用例全部通过

**测试覆盖范围：**

| 测试类别 | 测试内容 | 测试数量 |
|----------|----------|----------|
| 组件依赖 | RequireComponent 自动添加 Rigidbody2D/BoxCollider2D | 8 |
| 公共接口 | SetMoveInput/OnJumpPressed/SetPlatformVelocity 等方法存在性 | 6 |
| PlayerHealth | 伤害/治疗/死亡事件/无敌帧/ResetHealth | 8 |
| DisguiseSystem | 初始状态/无配置时安全性/调试信息 | 3 |
| IControllableProp | 接口实现/初始状态 | 3 |
| InputManager | 启用/禁用输入 | 2 |
| Mario 移动 | 左右移动/停止减速/重力下落 | 4 |
| Mario 跳跃 | 跳跃高度/Bounce 弹跳 | 2 |
| Mario 状态 | IsMoving/Die 禁用控制器 | 2 |
| Trickster 移动 | 移动/伪装状态 | 2 |
| 伪装系统运行时 | 无配置安全性/冷却阻止重复变身 | 2 |
| 道具状态机 | Hazard/Block 的 Telegraph→Active 转换 | 2 |
| 移动平台 | 两点间移动 | 1 |
| 胜负判定 | Mario到达终点/Mario死亡/回合重置 | 3 |
| 暂停系统 | 暂停/恢复 TimeScale | 1 |
| InputManager 集成 | 禁用输入停止移动 | 1 |
| 能量系统 (Session 10) | 消耗/恢复/重置/低能量状态 | 9 |
| 扫描技能 (Session 10) | 冷却/事件触发/范围检测 | 5 |

### 5. 手动场景搭建（备选方案）
如果不使用 TestSceneBuilder，也可以手动搭建：

**A. 核心管理器（空 GameObject）**
- 创建空 GameObject 命名为 `Managers`
- 挂载 `GameManager.cs`
- 挂载 `InputManager.cs`
- 挂载 `LevelManager.cs`

**B. 玩家角色**
- **Mario**: 挂载 `MarioController.cs` + `PlayerHealth.cs` + **`ScanAbility.cs`** + `Rigidbody2D` + `BoxCollider2D`
  - Rigidbody2D: Body Type = Dynamic, Gravity Scale = 0（重力由代码自管）, Freeze Rotation Z = ✅
  - MarioController Inspector: 设置 groundLayer（必须！否则跳跃不工作）
- **Trickster**: 挂载 `TricksterController.cs` + `DisguiseSystem.cs` + **`TricksterAbilitySystem.cs`** + **`EnergySystem.cs`** + `Rigidbody2D` + `BoxCollider2D`
  - Rigidbody2D 配置同 Mario

**C. 关卡元素**
- **终点**: 挂载 `GoalZone.cs` + `BoxCollider2D` (勾选 IsTrigger)
- **深渊**: 挂载 `KillZone.cs` + `BoxCollider2D` (勾选 IsTrigger)
- **移动平台**: 挂载 `MovingPlatform.cs`（自动添加 Rigidbody2D + BoxCollider2D，自动配置为 Kinematic）
- **可操控平台**: 挂载 `ControllablePlatform.cs`（同上）
- **可操控陷阱**: 挂载 `ControllableHazard.cs` + `BoxCollider2D`
- **可操控方块**: 挂载 `ControllableBlock.cs` + `BoxCollider2D`

**D. 相机**
- 在 Main Camera 上挂载 `CameraController.cs`，并将 Mario 拖入其 Target 槽位。

**E. Inspector 引用连线（关键！）**
- `InputManager` → 拖入 Mario 和 Trickster 引用
- `GameManager` → 拖入 Mario、Trickster、PlayerHealth 引用
- `LevelManager` → 设置出生点 Transform 和关卡边界

### 6. 玩法测试流程（请按以下顺序测试）

**第一步：基础移动与跳跃测试**
1. 运行游戏。
2. **P1 (Mario)**: 使用 `WASD` 移动，`Space` 跳跃。确认移动流畅、停止果断（无打滑）。
3. **P2 (Trickster)**: 使用 `方向键` 移动。确认移动正常。

**第二步：移动平台跟随测试**
1. 让 Mario 站上移动平台，确认角色随平台平稳移动（不被甩飞、不打滑）。
2. 在平台上按 A/D，确认可以在平台上自由走动。
3. 跳离平台，确认恢复正常重力。

**第三步：伪装变身系统测试**
1. 控制 Trickster 走到一个可伪装对象（如平台或陷阱）附近。
2. 按下 `P` 键，确认 Trickster 成功变身为该对象的外观。
3. 停止移动，等待几秒钟，确认 Trickster "完全融入场景"。

**第四步：道具操控能力测试**
1. 在 Trickster 处于"完全融入场景"的伪装状态下。
2. 按下 `L` 键触发操控能力。
3. **观察预警阶段**：确认道具是否出现闪烁变红和震动效果（持续约0.8秒）。
4. **观察爆发阶段**：预警结束后，确认道具是否执行了对应的阻碍动作。
5. **观察冷却阶段**：确认触发后是否进入冷却，短时间内无法再次触发。

**第五步：能量系统与扫描技能测试 (Session 10 新增)**
1. **Trickster 能量消耗**：变身（消耗25）和操控道具（消耗20），观察右上角调试信息中的能量值变化。
2. **能量恢复**：静止不动，观察能量是否随时间自然恢复（2/秒）。
3. **Mario 扫描技能**：控制 Mario 靠近伪装状态的 Trickster，按下 `Q` 键。
4. **扫描效果**：确认是否出现蓝色/红色脉冲圆环，Trickster 是否闪烁红色并在头顶显示 `!` 警告标记。

**第六步：胜负判定测试**
1. 控制 Mario 碰到深渊 (KillZone) 或危险陷阱 (DamageDealer)，确认是否触发死亡和回合重置。
2. 控制 Mario 走到终点旗帜 (GoalZone)，确认是否触发胜利画面。
3. 确认游戏结束后屏幕是否有明显的"ROUND OVER"提示和重启引导。

---

## 五、下一步开发计划（MVP 优先级排序）

### 第一优先级：场景搭建与核心验证（Sprint 1 收尾）

| 序号 | 任务 | 状态 | 说明 |
|------|------|------|------|
| 1 | 在Unity中创建测试场景 | ✅ **Session 8 完成** | TestSceneBuilder 一键生成完整测试关卡（菜单 MarioTrickster → Build Test Scene） |
| 2 | 下载并导入像素素材 | ⚬ 待做 | 从 Pixel Adventure 或 Block Land 下载素材包 |
| 3 | 创建Mario预制体 | ✅ 已有场景对象 | 挂载 MarioController + PlayerHealth + Rigidbody2D + BoxCollider2D（已在场景中配置） |
| 4 | 创建Trickster预制体 | ✅ 已有场景对象 | 挂载 TricksterController + DisguiseSystem + TricksterAbilitySystem |
| 5 | 配置InputManager引用 | ✅ **Session 8 自动化** | TestSceneBuilder 自动连线所有 Inspector 引用 |
| 6 | 配置相机 | ✅ **Session 8 自动化** | TestSceneBuilder 自动挂载 CameraController 并设置跟随 Mario |
| 7 | 放置GoalZone和KillZone | ✅ **Session 8 自动化** | TestSceneBuilder 自动放置终点（绿色，位置18,1）和死亡区域（底部） |
| 8 | 放置可操控道具 | ✅ **Session 8 自动化** | TestSceneBuilder 自动放置 ControllablePlatform/Hazard/Block |
| 9 | **核心玩法验证** | 🔄 进行中 | 手动 Play 测试已完成（见下方测试结果），待运行 Test Runner 自动化测试 |
| 10 | **自动化测试框架** | ✅ **Session 10 更新** | EditMode 40+ 测试 + PlayMode 20+ 测试，覆盖组件依赖/PlayerHealth/伪装系统/道具状态机/胜负判定/暂停/能量系统/扫描技能 |

### 第二优先级：游戏体验（Sprint 2，预计 1-2 周）

| 序号 | 任务 | 说明 |
|------|------|------|
| 11 | 基础 UI 系统升级 | 使用 Canvas UI 替代 OnGUI，提供更美观的 HUD 和提示 |
| 12 | 音效集成 | 跳跃/死亡/变身/操控道具预警音效/胜利音效 |
| 13 | 升级为Cinemachine | 已添加包依赖，替换CameraController为Cinemachine Virtual Camera |
| 14 | 更多可操控道具类型 | 如：传送门、风扇（改变风向）、开关门等 |

### 第三优先级：打磨（Sprint 3）

| 序号 | 任务 | 说明 |
|------|------|------|
| 15 | 平衡性调整 | 变身冷却、操控次数/冷却、预警时长、关卡难度 |
| 16 | 多关卡 | 2-3个不同主题关卡 |
| 17 | 开始/结束界面 | 主菜单、角色选择、结算画面 |

---

## 六、Session 历史记录

### Session 10 记录（2026-04-02）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| Bug 修复 | 修复了 B011（游戏结束按键失效/提示不明显）、B012（道具操控失败无反馈）、B014（暂停恢复按键失效/无反馈） |
| 能量系统 (EnergySystem) | 为 Trickster 引入能量系统。变身和操控道具需要消耗能量，能量会随时间自然恢复。增加了资源管理的博弈深度。 |
| 扫描技能 (ScanAbility) | 为 Mario 引入主动扫描技能（Q键/手柄东键）。使用后短暂暴露范围内的伪装 Trickster（红色闪烁+头顶警告标记），有冷却时间。 |
| 测试用例更新 | 在 ComponentSetupTests.cs 中新增了 EnergySystem 和 ScanAbility 的 EditMode 测试用例（14个新用例）。 |
| TestSceneBuilder 更新 | 更新了一键生成测试场景工具，自动为 Trickster 挂载 EnergySystem，为 Mario 挂载 ScanAbility。 |

**代码统计：**
- 修改了 GameManager.cs, GameUI.cs, TricksterController.cs, DisguiseSystem.cs, TricksterAbilitySystem.cs, InputManager.cs, TestSceneBuilder.cs, ComponentSetupTests.cs
- 新增了 EnergySystem.cs, ScanAbility.cs

---

### Session 9 记录（2026-04-01）

**手动 Play 测试结果（用户在 Unity 中实际测试）：**

| 测试项 | 结果 | 备注 |
|----------|------|------|
| Mario 移动/跳跃 | ✅ 正常 | WASD 移动流畅，Space 跳跃正常 |
| Trickster 移动 | ✅ 正常 | 方向键移动正常 |
| 移动平台跟随 | ✅ 正常 | 站上平台不会被甩飞 |
| 伪装变身 | ✅ 正常 | P 伪装/解除，O/I 切换形态，静止后融入 |
| 道具操控（L 键） | ✅ 基本正常 | 靠近陷阱按 L 有反馈（预警+爆发），但失败时无提示（B012） |
| 胜负判定 | ✅ 正常 | Mario 死亡触发 Trickster 胜利，但结束提示不够明显（B011） |
| ESC 暂停 | ⚠️ 部分问题 | 暂停有效，但恢复时无视觉反馈（B014） |

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| Assembly Definition 体系建立 | 创建 MarioTrickster.asmdef（主项目）+ MarioTrickster.Editor.asmdef（Editor），修复 EditMode/PlayMode 测试 asmdef 引用，添加 Unity.InputSystem 包引用。解决了 136+4 个编译错误 |
| TestSceneBuilder 字段名修复 | 修复 LevelManager 出生点字段名错误（marioSpawn→marioSpawnPoint, tricksterSpawn→tricksterSpawnPoint） |
| 完整测试指南文档 | 新增 MarioTrickster_Testing_Guide.md：手动 Play 测试 7 项操作步骤、EditMode/PlayMode 自动化测试说明、伪装系统 Sprite 配置指引（3 种获取方式）、操作键位速查表、调试信息说明 |

**解决的问题：**

| 编号 | 描述 | 解决方案 |
|------|------|----------|
| — | 测试脚本编译错误 CS0246（136 个错误） | 创建 MarioTrickster.asmdef + MarioTrickster.Editor.asmdef，测试 asmdef 添加主项目引用 |
| — | InputManager 编译错误 CS0234/CS0246（4 个错误） | MarioTrickster.asmdef 添加 Unity.InputSystem 引用 |
| B013 | TestSceneBuilder LevelManager 字段名错误 | marioSpawn→marioSpawnPoint, tricksterSpawn→tricksterSpawnPoint |

**新发现 Bug：**

| 编号 | 描述 | 优先级 |
|------|------|--------|
| B011 | 游戏结束后缺少明显提示，玩家以为角色卡住（实际是游戏已结束，输入被禁用） | 高 |
| B012 | 道具操控（L 键）成功时有反馈，但失败时无提示（不在范围/未满足前置条件） | 中 |
| B014 | ESC 暂停后恢复无视觉反馈，玩家不确定是否已恢复 | 中 |

**新增文件：**

| 文件 | 路径 | 说明 |
|------|------|------|
| MarioTrickster.asmdef | Assets/Scripts/ | 主项目 Assembly Definition |
| MarioTrickster.Editor.asmdef | Assets/Scripts/Editor/ | Editor 脚本 Assembly Definition |
| MarioTrickster_Testing_Guide.md | 仓库根目录 | 完整测试指南文档 |

**代码统计：**
- 项目总计 26 个 .cs 文件 + 4 个 .asmdef 文件
- 本次新增 2 个 .asmdef + 1 个 .md 文档，修改 2 个 .asmdef + 1 个 .cs

**决策变更：无**

---

### Session 8 记录（2026-04-01）

**新完成功能：**

| 项目 | 说明 |
|------|------|
| TestSceneBuilder.cs | Editor 菜单工具（MarioTrickster → Build Test Scene），一键在空白场景中生成完整测试关卡。自动创建地面/高台/墙壁/Mario/Trickster/管理器/移动平台/可操控道具/终点/死亡区域/敌人/金币/可破坏方块/相机跟随，所有 Inspector 引用自动连线，Ground Layer 自动创建分配。另有 Clear Test Scene 菜单清空场景。 |
| ComponentSetupTests.cs | EditMode 自动化测试（30+ 用例）：验证 RequireComponent 自动添加组件、PlayerHealth 完整伤害/治疗/死亡/无敌帧/重置逻辑、DisguiseSystem 初始状态和无配置安全性、MovingPlatform Kinematic 设置、GoalZone/KillZone Trigger 设置、IControllableProp 接口实现、InputManager 启用/禁用 |
| GameplayTests.cs | PlayMode 自动化测试（20+ 用例）：验证 Mario/Trickster 左右移动和停止减速、跳跃物理、重力下落、伪装系统运行时行为和冷却机制、ControllableHazard/Block 的 Telegraph→Active 状态机转换、移动平台两点间运动、GameManager 胜负判定（Mario到达终点/Mario死亡）、暂停/恢复 TimeScale、回合重置恢复生命值、Mario.Die() 禁用控制器、Bounce 弹跳、InputManager 集成 |
| 语法静态分析 | 26/26 .cs 文件全部通过括号匹配/using格式/region配对检查，0 错误 0 警告 |
| 跨文件引用检查 | 所有 AddComponent 引用、关键方法调用（32个）、公共属性（16个）全部验证通过 |

**解决的问题：**

| 编号 | 描述 | 解决方案 |
|------|------|----------|
| B004 | 场景 SampleScene.unity 是空白模板 | 创建 TestSceneBuilder 一键生成测试场景，用户无需手动搭建 |

**新增文件：**

| 文件 | 路径 | 说明 |
|------|------|------|
| TestSceneBuilder.cs | Assets/Scripts/Editor/ | 一键生成/清空测试场景的 Editor 工具 |
| ComponentSetupTests.cs | Assets/Tests/EditMode/ | EditMode 自动化测试 |
| GameplayTests.cs | Assets/Tests/PlayMode/ | PlayMode 自动化测试 |
| EditModeTests.asmdef | Assets/Tests/EditMode/ | EditMode 测试 Assembly Definition |
| PlayModeTests.asmdef | Assets/Tests/PlayMode/ | PlayMode 测试 Assembly Definition |

**代码统计：**
- 项目总计 26 个 .cs 文件，6370 行代码
- 本次新增 3 个 .cs 文件，约 1750 行代码

**决策变更：无**

---

### Session 7 记录（2026-04-01）

**新完成功能：**

| 项目 | 说明 |
|------|------|
| P2 键位优化 | 伪装键 右Shift→**P**，切换形态 Enter/Backspace→**O/I**，操控道具 右Alt→**L**。新键位全部集中在右手字母区，与方向键操作更协调 |
| 伪装调试显示 | TricksterController 新增 `OnGUI`，运行时屏幕右上角实时显示伪装系统状态（未配置/就绪/已伪装/冷却中），方便排查配置问题 |
| DisguiseSystem 调试接口 | 新增 `GetDebugStatus()` 方法，返回人类可读的伪装状态字符串，供 OnGUI 调用 |
| SpriteAutoFit 脚本 | 新增通用工具脚本，挂到有 SpriteRenderer + BoxCollider2D 的物体上，自动将 Sprite 缩放填满 Collider 大小。支持编辑器实时预览（`[ExecuteInEditMode]`） |
| 场景搭建指导 | 完成了 ControllablePlatform/Hazard 的 Sprite 配置、Tiled 模式、Scale 调整等场景搭建实操指导 |

**新发现问题：**

| 编号 | 描述 | 状态 |
|------|------|------|
| B009 | DisguiseSystem 的 Available Disguises 列表为空时按 P 无任何反应，用户不知道原因 | ✅ 已通过调试显示解决（屏幕右上角会提示"未配置伪装形态"） |
| B010 | SpriteAutoFit 脚本使用 `[ExecuteInEditMode]` 持续修改 Scale，与手动调整 Scale 冲突 | ✅ 已告知用户：使用 SpriteAutoFit 时不要手动改 Scale；或改用 Tiled 模式 + 手动设置 Size |

**决策变更：无**

---

### Session 6 修复记录（2026-04-01）

**平台跟随系统彻底重写（速度注入法）：**

| 修复项 | 说明 |
|--------|------|
| B006 平台跟随 | 废弃 SetParent 方案，改用速度注入法。平台每帧计算 `_currentVelocity`，通过 `SetPlatformVelocity()` 注入站在上面的角色。角色在 FixedUpdate 最后叠加平台速度。 |
| B007 被甩飞 | 读回 `rb.velocity` 时减去 `_lastPlatformVelocity`（上一帧注入的平台速度），避免每帧累积。 |
| B008 打滑感 | `groundDeceleration` 从 60 调至 200，`acceleration` 从 120 调至 160（Mario）/ 140（Trickster）。空中减速度保持 30 不变。 |

**平台跟随架构（速度注入法）：**
```
// 平台端（MovingPlatform / ControllablePlatform）
[DefaultExecutionOrder(-10)]  // 先于角色控制器执行
FixedUpdate() {
    prevPos = rb.position;
    rb.MovePosition(newPos);
    _currentVelocity = (newPos - prevPos) / Time.fixedDeltaTime;
    foreach rider: rider.SetPlatformVelocity(_currentVelocity);
}

// 角色端（MarioController / TricksterController）
FixedUpdate() {
    _frameVelocity = rb.velocity - _lastPlatformVelocity;  // 扣除上帧平台速度
    HandleDirection / HandleJump / HandleGravity;
    platformVelThisFrame = _onPlatform ? _platformVelocity : Vector2.zero;
    _frameVelocity += platformVelThisFrame;
    rb.velocity = _frameVelocity;
    _lastPlatformVelocity = platformVelThisFrame;  // 记录供下帧扣除
    _onPlatform = false;
}
```

### Session 5 升级记录（2026-04-01）

**系统性改造：对照参考项目全面升级核心控制器**

| 文件 | 升级内容 |
|------|----------|
| `MarioController.cs` | 引入 Tarodev 帧速度累积架构；重力自管；BoxCast 地面检测；Coyote Time(0.15s)；跳跃缓冲(0.2s)；OnValidate 警告 |
| `TricksterController.cs` | 与 MarioController 完全一致的帧速度架构；同样支持 Coyote Time + 跳跃缓冲 |
| `InputManager.cs` | 修复 OnJumpReleased 每帧触发问题；用 `wasJumpHeld` 状态机精确检测事件；精简代码 |

**核心架构变化（帧速度累积）**：
```
// 旧方案（多处赋值冲突）
rb.velocity = new Vector2(inputSpeed, rb.velocity.y);  // 移动
rb.velocity = new Vector2(rb.velocity.x, jumpForce);  // 跳跃

// 新方案（Tarodev 架构，一帧内累积，最后一次写入）
_frameVelocity = rb.velocity;
HandleDirection();   // 修改 _frameVelocity.x
HandleJump();        // 修改 _frameVelocity.y
HandleGravity();     // 修改 _frameVelocity.y
rb.velocity = _frameVelocity;  // 只写一次
```

---

## 七、关键技术决策记录

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 引擎 | Unity 2022.3 LTS | 稳定性最佳，2D支持成熟，教程资源丰富 |
| 输入系统 | Unity New Input System + 自定义InputManager | 原生支持本地多人、设备热插拔；InputManager统一分发输入给两个玩家 |
| 玩家控制器架构 | Tarodev 帧速度累积 | 参考 Ultimate-2D-Controller，一帧内累积所有速度变化，最后一次写入 rb.velocity |
| 伪装机制实现 | Sprite替换 + Collider切换 | 变身时替换SpriteRenderer的sprite，同时切换碰撞体形状匹配伪装对象 |
| **平台跟随方案** | **速度注入法（不使用 SetParent）** | **Dynamic Rigidbody2D 的 rb.velocity 是世界坐标系绝对速度，SetParent 改变 Transform 层级但物理引擎不理解层级关系。正确做法：平台每帧把速度注入角色，角色在 FixedUpdate 叠加。** |
| **道具操控机制** | **接口(IControllableProp) + 抽象基类(ControllablePropBase) + 三阶段状态机** | **参考 Crawl 游戏的 Ghost Possess Trap。Telegraph→Active→Cooldown 三阶段设计，预警给 Mario 反应窗口，提高博弈深度。接口+基类架构便于扩展新道具类型** |
| **测试策略** | **EditMode + PlayMode 双层自动化测试** | **EditMode 验证组件依赖和静态逻辑（不需要物理引擎），PlayMode 验证运行时行为（移动/跳跃/碰撞/状态机）。TestSceneBuilder 解决场景搭建自动化。** |
| 关卡构建 | Unity Tilemap | 参考zigurous教程，快速搭建2D关卡 |
| 相机方案 | 自定义CameraController（后期升级Cinemachine） | MVP阶段用简单脚本，已预装Cinemachine包 |
| 游戏管理 | 单例GameManager | 管理游戏状态、胜负判定、暂停、重启 |
| UI方案 | OnGUI后备 + UGUI Canvas | MVP阶段OnGUI快速显示，后期升级为Canvas UI |
| 美术风格 | 16-bit像素风 | 使用itch.io免费CC0素材（Pixel Adventure为主），后期可用本地AI工具生成补充素材 |
| 网络架构 | 暂不实现 | MVP阶段仅本地多人，后期扩展可考虑Unity Netcode |
| 项目结构 | 按功能模块分文件夹 | Assets/Scripts/{Player, Enemy, Core, UI, Camera, Ability, Editor} + Assets/Tests/{EditMode, PlayMode} |

---

## 七-A、已知技术债（MVP 阶段可接受，后期需处理）

| 编号 | 位置 | 描述 | 影响 | 建议处理时机 |
|------|------|------|------|-------------|
| TD001 ✅ | `MovingPlatform.cs` | **已解决（Session 6）**：改用 Kinematic Rigidbody2D + `MovePosition()` + 速度注入法。 | — | 已完成 |
| TD002 | `MarioController.Die()` / `TricksterController.Die()` | `Die()` 调用后立即 `enabled = false`，没有死亡动画播放窗口。 | 以后加死亡动画时需改为协程或状态机控制禁用时机 | Sprint 2 添加动画系统时 |

---

## 八、项目文件结构（当前实际状态）

```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── MarioController.cs      ✅ Mario移动/跳跃/平台跟随 (Session 6 更新)
│   │   └── PlayerHealth.cs          ✅ 生命值管理
│   ├── Enemy/
│   │   ├── TricksterController.cs   ✅ Trickster控制/平台跟随 (Session 10 更新: OnAbilityFailed 事件)
│   │   ├── DisguiseSystem.cs        ✅ 伪装/变身系统 (Session 10 更新: 集成 EnergySystem)
│   │   └── SimpleEnemy.cs           ✅ 简单巡逻敌人
│   ├── Core/
│   │   ├── GameManager.cs           ✅ 游戏状态/胜负判定 (Session 10 更新: 修复按键检测)
│   │   ├── InputManager.cs          ✅ 双人输入管理 (Session 10 更新: 添加扫描按键)
│   │   ├── LevelManager.cs          ✅ 关卡管理
│   │   ├── GoalZone.cs              ✅ 终点触发器
│   │   ├── KillZone.cs              ✅ 死亡区域
│   │   ├── DamageDealer.cs          ✅ 通用伤害触发器
│   │   ├── Collectible.cs           ✅ 可收集物品
│   │   ├── Breakable.cs             ✅ 可破坏方块
│   │   ├── MovingPlatform.cs        ✅ 移动平台 (Session 6 重写: 速度注入法)
│   │   └── SpriteAutoFit.cs         ✅ Sprite 自动适配 (Session 7 新增)
│   ├── Ability/
│   │   ├── IControllableProp.cs     ✅ 可操控道具接口
│   │   ├── ControllablePropBase.cs  ✅ 操控状态机基类
│   │   ├── TricksterAbilitySystem.cs ✅ 能力系统管理器
│   │   ├── EnergySystem.cs          ✅ Trickster 能量系统 (Session 10 新增)
│   │   ├── ScanAbility.cs           ✅ Mario 扫描技能 (Session 10 新增)
│   │   ├── ControllablePlatform.cs  ✅ 可操控移动平台 (Session 6 更新: 速度注入法)
│   │   ├── ControllableHazard.cs    ✅ 可操控危险道具
│   │   └── ControllableBlock.cs     ✅ 可操控方块
│   ├── Camera/
│   │   └── CameraController.cs      ✅ 相机跟随逻辑
│   ├── Editor/
│   │   ├── TestSceneBuilder.cs      ✅ 一键生成测试场景 (Session 8 新增)
│   │   └── MarioTrickster.Editor.asmdef ✅ Editor Assembly Definition (Session 9 新增)
│   ├── MarioTrickster.asmdef        ✅ 主项目 Assembly Definition (Session 9 新增)
│   └── UI/
│       └── GameUI.cs                ✅ 基础HUD (Session 10 更新: 增强结束/暂停/失败反馈)
├── Tests/
│   ├── EditMode/
│   │   ├── EditModeTests.asmdef     ✅ Assembly Definition (Session 8 新增)
│   │   └── ComponentSetupTests.cs   ✅ 组件依赖/配置验证测试 (Session 8 新增)
│   └── PlayMode/
│       ├── PlayModeTests.asmdef     ✅ Assembly Definition (Session 8 新增)
│       └── GameplayTests.cs         ✅ 核心玩法自动化测试 (Session 8 新增)
├── InputActions/
│   └── GameControls.inputactions    ✅ Input System配置
├── Scenes/
│   └── SampleScene.unity            （使用 TestSceneBuilder 一键生成测试关卡）
├── Prefabs/                          ⬜ 待创建
├── Sprites/                          ⬜ 待导入素材
└── Tilemaps/                         ⬜ 待创建
```

---

## 九、操作键位参考

| 操作 | Player1 (Mario) | Player2 (Trickster) |
|------|-----------------|---------------------|
| 移动 | WASD | 方向键 |
| 跳跃 | Space | 上方向键 / 右Ctrl / 小键盘0 |
| **扫描技能** | **Q / 手柄东键** | — |
| 伪装/取消伪装 | — | **P** |
| 切换伪装形态（下一个） | — | **O** |
| 切换伪装形态（上一个） | — | **I** |
| **操控道具** | — | **L / 手柄Y** |
| 暂停 | ESC | ESC |
| 快速重启 | F5 | F5 |
| 手柄支持 | 第1个手柄 | 第2个手柄 |

### 能量系统参数（可在 Inspector 运行时调整）

| 参数 | 默认值 | 说明 |
|------|--------|------|
| maxEnergy | 100 | 最大能量 |
| regenRate | 2 | 每秒自然恢复量 |
| disguiseCost | 25 | 变身能量消耗 |
| abilityCost | 20 | 操控道具能量消耗 |
| lowEnergyThreshold | 20 | 低能量警告阈值 |

### 移动手感参数（可在 Inspector 运行时调整）

| 参数 | Mario | Trickster | 说明 |
|------|-------|-----------|------|
| maxSpeed | 9 | 8 | 最大水平速度 |
| acceleration | 160 | 140 | 地面加速度（越大起步越快） |
| groundDeceleration | 200 | 200 | 地面减速度（越大停止越果断，消除打滑） |
| airDeceleration | 30 | 30 | 空中减速度（保持较小以保留空中滑行感） |
| jumpPower | 20 | 18 | 跳跃初速度 |
| coyoteTime | 0.15 | 0.15 | 离开平台边缘后仍可跳跃的宽限时间 |
| jumpBuffer | 0.2 | 0.2 | 落地前提前按跳跃的缓冲时间 |

---

## 十、新对话开场模板

每次开新对话时，只需复制以下内容（不到10行），AI 会自动从仓库读取本文件获取完整上下文：

```
GitHub Token: ghp_你的token
仓库：https://github.com/jiaxuGOGOGO/MarioTrickster

请先用 Token 克隆仓库，读取根目录的以下文件获取完整项目上下文：
1. MarioTrickster_Progress_Summary.md（进度/决策/文件结构/参考项目/开发计划）
2. 与 AI 高效协作开发工作流指南.md（协作规范/模板/Git流程）

本次任务：[在这里写你要做的事情]

积分提醒：请在我积分接近300时暂停，优先存档推送。
```
