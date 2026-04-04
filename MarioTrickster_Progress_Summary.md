# MarioTrickster 项目进度总结

> 更新时间：2026-04-04 (Session 21) | 完整存档文档：功能清单、Bug 库、技术决策、Session 历史
> **AI 入口**：每次新对话优先读取 `SESSION_TRACKER.md`（当前状态 + AI 行为规范 + 回归清单 + 待办队列），需要完整上下文时再读本文件，需要纵览全局时读 `MASTER_TRACKER.md`

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
| 综合调研报告 | ✅ | GAME_DESIGN.md | 游戏概念、核心机制、相机方案、平衡设计、美术管线、引擎选型、开发时间线、竞品分析、行动计划 |
| 全局项目总览 | ✅ | MASTER_TRACKER.md | 4张分类映射矩阵、模块完成度统计、双向追踪指南、演进路线图、AI自动扩展规则 (Session 14 新增) |
| AI协作工作流 | ✅ | AI_WORKFLOW.md | Bug报告模板、功能开发流程、Git命令速查、命名规范、场景沟通模板、最佳实践 |

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
| MarioController.cs | Assets/Scripts/Player/ | ✅ **Session 21 更新** | Tarodev 帧速度架构 + 平台速度注入法跟随。S20: BounceStun。**S21: 新增 SetFrameVelocity() 绝对速度注入 + BounceStun期间跳过 maxSpeed 截断，彻底修复弹射抛物线** |
| PlayerHealth.cs | Assets/Scripts/Player/ | ✅ | 通用生命值/无敌帧/受伤闪烁/死亡事件 |
| TricksterController.cs | Assets/Scripts/Enemy/ | ✅ **Session 20 更新** | 帧速度架构 + 平台速度注入。S20: 新增 OnDirectionInput() 磁吸切换回调 + IsFullyBlended 属性 + 切换防抖冷却(0.2s)。OnGUI 显示伪装系统实时状态 |
| DisguiseSystem.cs | Assets/Scripts/Enemy/ | ✅ **Session 7 更新** | Sprite替换变身/冷却/场景融入/多形态切换；新增 GetDebugStatus() 调试方法 |
| InputManager.cs | Assets/Scripts/Core/ | ✅ **Session 20 更新** | S20: 融入状态下方向键拦截转发为磁吸切换指令(OnDirectionInput)。S19: S+Jump组合键(HandleDropThrough) + S单独(HandlePassageInteraction) |
| GameManager.cs | Assets/Scripts/Core/ | ✅ **Session 12 更新** | 游戏状态/胜负判定/暂停/重启/计时器/单例模式。B020修复：ResetRound 重置 GoalZone.triggered + ControllablePropBase 使用次数。B021修复：移除 RESUMED 提示 |
| CameraController.cs | Assets/Scripts/Camera/ | ✅ **Session 11 重写** | 平滑跟随Mario/前瞻偏移(滞后检测)/渐进式死区/关卡边界限制/相机震动(独立偏移量)。修复B016镜头晃动。 |

### 2.5 辅助脚本（Sprint 1）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| GoalZone.cs | Assets/Scripts/Core/ | ✅ **Session 12 更新** | 终点触发器，Mario碰触后触发胜利。B020修复：新增 ResetTrigger() 方法供多回合重置 |
| KillZone.cs | Assets/Scripts/Core/ | ✅ | 死亡区域（掉落深渊），碰触即死 |
| DamageDealer.cs | Assets/Scripts/Core/ | ✅ **Session 18 更新** | 通用伤害触发器（尖刺/怪物/Hazard伪装），支持击退。S18: 使用 KnockbackHelper 统一击退方向 |
| KnockbackHelper.cs | Assets/Scripts/Core/ | ✅ **Session 18 新增** | 统一击退方向计算工具，根据 Mario 移动方向反向后退，避免反复二次伤害 |
| MarioInteractionHelper.cs | Assets/Scripts/Core/ | ✅ **Session 19 更新** | Mario交互辅助：HandleDropThrough(S+Jump) + HandlePassageInteraction(S单独)，支持双向隐藏通道返回触发区 |
| Collectible.cs | Assets/Scripts/Core/ | ✅ | 可收集物品（金币/回血/加速），带浮动动画 |
| Breakable.cs | Assets/Scripts/Core/ | ✅ | 可破坏方块（砖块/问号砖块），从下方顶撞触发 |
| MovingPlatform.cs | Assets/Scripts/Core/ | ✅ **Session 6 重写** | Kinematic Rigidbody2D + MovePosition() + 速度注入法跟随角色（不使用 SetParent） |
| SimpleEnemy.cs | Assets/Scripts/Enemy/ | ✅ | 简单巡逻敌人，边缘/墙壁检测自动转向，可被踩消灭 |
| GameUI.cs | Assets/Scripts/UI/ | ✅ **Session 16 更新** | 基础HUD（生命值/计时器/回合信息/胜负画面/能量显示）。B024修复：加宽计时器显示区域。B025新增：无冷却模式指示器显示 |
| LevelManager.cs | Assets/Scripts/Core/ | ✅ | 关卡管理（出生点/边界/可伪装对象列表）。注意：Start() 中会调用 CameraController.SetBounds() 覆盖相机边界 |

### 2.6 关卡设计系统（Sprint 2 - Session 15 新增）

**核心机制**: 采用低耦合+高内聚的框架设计，所有关卡元素统一继承 `LevelElementBase` 或 `ControllableLevelElement`，通过 `LevelElementRegistry` 自动注册/注销。支持随时新增/删除元素而不影响其他代码。

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| LevelElementBase.cs | Assets/Scripts/LevelElements/ | ✅ | 关卡元素统一抽象基类，定义分类(Category)和标签(Tags) |
| LevelElementRegistry.cs | Assets/Scripts/LevelElements/ | ✅ | 自动注册中心，提供按分类/标签/距离等多维度查询 |
| ControllableLevelElement.cs | Assets/Scripts/LevelElements/ | ✅ | 桥接基类，连接关卡框架与Trickster操控系统 |
| SpikeTrap.cs | Assets/Scripts/LevelElements/Traps/ | ✅ **Session 18 更新** | 地刺陷阱（静态/周期伸缩，可操控）。S18: KnockbackHelper 统一击退 |
| PendulumTrap.cs | Assets/Scripts/LevelElements/Traps/ | ✅ **Session 18 更新** | 摆锤绳索陷阱（物理摆动，可操控）。S18: KnockbackHelper 统一击退 |
| FireTrap.cs | Assets/Scripts/LevelElements/Traps/ | ✅ **Session 18 更新** | 火焰陷阱（周期喷射，可操控）。S18: 击退方向修正 + KnockbackHelper |
| BouncingEnemy.cs | Assets/Scripts/LevelElements/Traps/ | ✅ **Session 18 更新** | 弹跳小怪物（周期弹跳，可踩踏，可操控）。S18: KnockbackHelper 统一击退 |
| BouncyPlatform.cs | Assets/Scripts/LevelElements/Platforms/ | ✅ **Session 21 更新** | 弹跳平台。S20: 法线修正+BounceStun。**S21: 废弃 AddForce/rb.velocity，改用 SetFrameVelocity() 绝对速度注入；注入前清除旧速度；移除冗余 mario.Bounce() 调用** + 喜剧延迟 + 相机震动 + Trickster可操控 |
| OneWayPlatform.cs | Assets/Scripts/LevelElements/Platforms/ | ✅ **Session 19 更新** | 单向平台：S+Jump组合键下落（行业标准，防止误操作） |
| CollapsingPlatform.cs | Assets/Scripts/LevelElements/Platforms/ | ✅ **Session 18 更新** | 崩塌平台（踩踏后抖动掉落，可操控）。S18: 位置重生修复 + Trickster也能触发 |
| HiddenPassage.cs | Assets/Scripts/LevelElements/HiddenPassages/ | ✅ **Session 19 重写** | 隐藏通道：双向穿越 + TeleportMode状态机 + 返回触发区 + 冷却时间 |
| HiddenPassageReturnTrigger.cs | Assets/Scripts/LevelElements/HiddenPassages/ | ✅ **Session 19 新增** | 隐藏通道返回触发区，在出口位置动态创建，允许玩家按S传回入口 |
| FakeWall.cs | Assets/Scripts/LevelElements/HiddenPassages/ | ✅ | 伪装墙壁（进入后变透明，可操控） |

### 2.7 Trickster 能力系统（Sprint 2 - Session 3 新增）

**核心机制**: Trickster 变身为关卡道具后，可以操控该道具阻碍 Mario。操控采用 **Telegraph（预警）→ Active（爆发）→ Cooldown（冷却）** 三阶段设计，预警阶段给 Mario 反应窗口，Trickster 需要预判操控时机。参考 Crawl 游戏的 Ghost Possess Trap 机制。

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| IControllableProp.cs | Assets/Scripts/Ability/ | ✅ **Session 20 更新** | 可操控道具接口。S20: 新增 SetHighlight(bool) 高亮控制 + GetTransform() 位置获取。原有: PropName/CanBeControlled/OnTricksterActivate/状态查询 |
| ControllablePropBase.cs | Assets/Scripts/Ability/ | ✅ **Session 20 更新** | 抽象基类，封装 Telegraph→Active→Cooldown 状态机、预警闪烁/震动视觉效果、次数限制、回合重置。S20: 实现 SetHighlight(微红脉冲动画) + GetTransform()。B019修复: originalColor→protected |
| TricksterAbilitySystem.cs | Assets/Scripts/Ability/ | ✅ **Session 20 重构** | 核心能力管理器。S20: 视觉连线反馈(红=锁定/灰=备选 LineRenderer对象池) + 目标高亮(微红脉冲) + 方向键磁吸切换 + Gizmo范围可视化 + 动态重绑定 |
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

### 2.8 能量系统与扫描技能（Session 10 新增）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| EnergySystem.cs | Assets/Scripts/Ability/ | ✅ **Session 10 新增** | Trickster 能量系统。变身消耗20、操控道具消耗15，满值100，未变身时恢复8/秒，变身时不恢复。变身期间每秒5点持续消耗（融入后减半）。支持低能量警告、回合重置。 |
| ScanAbility.cs | Assets/Scripts/Ability/ | ✅ **Session 11 更新** | Mario 扫描技能。Q键/手柄东键触发，检测半径5格范围内的伪装Trickster，命中后红色闪烁2秒+头顶警告标记。冷却8秒。修复B015脉冲颜色时序+扫描结果文字提示。 |

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

### 2.9 测试与工具系统（Session 8 新增）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| TestSceneBuilder.cs | Assets/Scripts/Editor/ | ✅ **Session 16 重写** | Editor 菜单工具，一键生成闯关式测试场景。按 Testing_Guide 测试顺序从左到右排列 9 个 Stage + 终点。每个区域有金黄色大标题 + 白色操作说明（TextMesh），Stage 9 内部分 9A-9I 子区域 |
| MarioTrickster.asmdef | Assets/Scripts/ | ✅ **Session 9 新增** | 主项目 Assembly Definition，引用 Unity.InputSystem |
| MarioTrickster.Editor.asmdef | Assets/Scripts/Editor/ | ✅ **Session 9 新增** | Editor 脚本 Assembly Definition |
| ComponentSetupTests.cs | Assets/Tests/EditMode/ | ✅ **Session 12 更新** | EditMode 测试（59 个测试用例）：验证 RequireComponent 自动添加、组件初始状态、PlayerHealth 伤害/治疗/死亡/无敌帧逻辑、DisguiseSystem 初始状态、IControllableProp 接口实现、InputManager 启用/禁用、EnergySystem、ScanAbility、CameraController、GameUI |
| LevelElementTests.cs | Assets/Tests/EditMode/ | ✅ **Session 15 新增** | EditMode 测试（55 个测试用例）：验证关卡设计系统框架核心、所有9种关卡元素、枚举完整性和低耦合验证 |
| GameplayTests.cs | Assets/Tests/PlayMode/ | ✅ **Session 8 新增** | PlayMode 测试（21 个测试用例）：验证 Mario/Trickster 移动/跳跃/重力、伪装系统运行时行为、道具操控状态机（Telegraph→Active→Cooldown）、移动平台运动、GameManager 胜负判定/暂停/回合重置、InputManager 集成 |
| EditModeTests.asmdef | Assets/Tests/EditMode/ | ✅ **Session 8 新增** | EditMode 测试 Assembly Definition |
| PlayModeTests.asmdef | Assets/Tests/PlayMode/ | ✅ **Session 8 新增** | PlayMode 测试 Assembly Definition |

### 2.10 配置文件更新

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
| B014 | ✅ **已修复 (Session 10, B021 更新于 Session 12)** ESC 暂停后再按 ESC 恢复无反馈 | — | 根因：同 B011，暂停状态下 ESC 按键检测被跳过。已修复逻辑。注：Session 10 曾添加"已恢复"提示，Session 12 经用户反馈后移除（B021）。 |
| B015 | ✅ **已修复 (Session 11)** 扫描成功时脉冲颜色首帧错误 + 屏幕下方矛盾提示 | — | 根因：StartPulse() 在 CheckForTrickster() 之前调用，首帧脉冲颜色用上次 tricksterFound 值。修复：先检测再启动脉冲。新增 Mario 下方扫描结果文字提示（区分范围内/外/未伪装）。 |
| B016 | ✅ **已修复已验证 (Session 11+12)** 镜头来回轻微晃动 | — | 根因：Main Camera 上被挂了多个 CameraController 组件，互相覆盖导致晃动。Session 11 修复：重写 CameraController（帧间位置差值）。Session 12 源头修复：TestSceneBuilder Build 前先 GetComponents 清理已有 CameraController，避免重复叠加。 |
| B017 | ✅ **已修复已验证 (Session 11)** 到达终点无胜利判定 | — | Mario 走到绿色方块(GoalZone)后没有触发胜利画面。修复：增加 OnTriggerStay2D 双保险 + 4种Mario检测方式。Console日志确认判定已成功触发。 |
| B018 | ✅ **已修复已验证 (Session 12)** 游戏结束UI未显示 | P0 | 根因：TestSceneBuilder 未创建 GameUI 对象。修复：TestSceneBuilder 新增 GameUI 对象创建。 |
| B019 | ✅ **已修复已验证 (Session 12)** originalColor 序列化冲突 | P0 | 根因：ControllableBlock 和父类 ControllablePropBase 都声明了 private Color originalColor。修复：父类改为 protected，子类移除重复声明。 |
| B020 | ✅ **已修复已验证 (Session 12)** 第二回合终点无反应 | P0 | 根因：GoalZone.triggered 在第一回合设为 true 后，ResetRound 未重置。修复：GoalZone 新增 ResetTrigger() 方法，GameManager.ResetRound() 调用它。 |
| B021 | ✅ **已修复已验证 (Session 12)** RESUMED 恢复提示多余 | P1 | 用户反馈暂停恢复后显示 RESUMED 没必要。已移除 GameManager 和 GameUI 中的恢复提示逻辑。 |
| B022 | ✅ **已修复 (Session 16)** CollapsingPlatform stateTimer 字段隐藏冲突 | P0 | 根因：CollapsingPlatform 第 36 行声明了 `private float stateTimer`，与爷父类 ControllablePropBase 第 52 行的 `protected float stateTimer` 重名，导致 C# 字段隐藏。修复：重命名为 `collapseTimer`，语义更清晰且消除隐藏冲突。 |
| B023 | ✅ **已修复 (Session 16)** 受伤无击退效果（只有红色闪烁+扣血） | P0 | 根因：MarioController/TricksterController 使用 Tarodev 帧速度架构，每帧 FixedUpdate 用 `_frameVelocity` 覆盖 rb.velocity，导致外部 AddForce 产生的击退力在下一帧被覆盖。修复：在两个控制器中添加 knockback stun 机制（ApplyKnockback/isKnockedBack/knockbackTimer），受击期间暂停控制器的速度覆盖。DamageDealer 修改为通知控制器进入 stun 状态。 |
| B024 | ✅ **已修复 (Session 16)** UI时间显示不全（被裁剪） | P1 | 根因：GameUI.cs 中计时器 Rect 宽度不足且 Y 偏移太小。修复：加宽显示区域（120→60）并增加 Y 偏移（8px）。 |
| B025 | ✅ **已实现 (Session 16)** 新增无冷却调试开关 (F9) | P0 | 在 GameManager 中添加 `noCooldownMode` 全局开关，F9 键切换。开启时自动清除 DisguiseSystem、ScanAbility、ControllablePropBase 的冷却。三个系统均新增 ResetCooldown() 方法。GameUI 显示绿色 "[F9] NO COOLDOWN" 指示器。 |
| B026 | ✅ **已实现 (Session 16)** 测试场景重构为闯关形式 + 场景指示标签 | P0 | TestSceneBuilder 完全重写，按 Testing_Guide 测试顺序从左到右排列 9 个 Stage + 终点 GoalZone。每个区域有金黄色大标题 + 白色操作说明（TextMesh），Stage 9 内部分 9A-9I 子区域。 |
| B027 | ✅ **根本修复 (Session 17)** 闯关场景相机边界未设置，Stage 3 后镜头不跟随 | P0 | 根因：`LevelManager.Start()` 在运行时调用 `CameraController.SetBounds()` 用默认值 `levelMaxX=50` 覆盖了 TestSceneBuilder 设置的正确边界 `maxX≈70`。Session 16 仅设置了 CameraController 的值但忽略了 LevelManager 会覆盖。Session 17 修复：在 TestSceneBuilder 中同步设置 LevelManager 的 levelMinX/levelMaxX/levelMinY/levelMaxY 字段。 |
| B028 | ✅ **已修复 (Session 17)** Trickster 跳到 Mario 头上无法跳走（角色碰撞卡死） | P0 | 根因：Mario 和 Trickster 都在 Default Layer，物理碰撞让 Trickster 卡在 Mario 头上，但地面检测 BoxCast 只检测 Ground Layer，所以 `_grounded=false` 无法跳跃。修复：创建 Player/Trickster 专用 Layer，`Physics2D.IgnoreLayerCollision` 禁用两者碰撞，角色互相穿过不会卡住。 |
| B029 | ✅ **已修复 (Session 18)** 伪装后按 L 无法控制平台 + 移动平台上难以融入 | P0 | 修复：TricksterAbilitySystem 加入动态重绑定（每次按L重新搜索最近道具）+ 融入后自动绑定最近道具 + Gizmo范围可视化 |
| B030 | ✅ **已修复 (Session 18)** 火陷阱击退飞出 + 所有陷阱击退方向不合理 | P0 | 修复：创建 KnockbackHelper 统一击退方向计算，改为 Mario 移动方向的反方向后退一小段距离，避免反复二次伤害。影响：FireTrap/SpikeTrap/PendulumTrap/BouncingEnemy/DamageDealer |
| B031 | ✅ **已修复 (Session 18)** 摆锤隐蔽状态下无法 Trickster 控制 | P0 | 修复：TricksterAbilitySystem 融入后自动重新绑定最近道具，不再只在变身瞬间绑定一次 |
| B032 | ✅ **已修复 (Session 18)** 弹跳平台只有宽边有效 + 无喜剧延迟 | P0 | 修复：移除 contact.normal 方向限制，两边都触发弹跳。新增 comedyDelay 参数（默认0.25秒），Mario 落上后先“陷入”压缩，延迟后才弹射。期间 Trickster 可操控增强弹性 |
| B033 | ✅ **已修复 (Session 18)** 单向平台按S不下落 | P0 | 修复：InputManager 新增 S键下蹲交互路由，通过 MarioInteractionHelper 检测脚下 OneWayPlatform 并调用 AllowDropThrough() |
| B034 | ✅ **已修复 (Session 18)** 崩塌平台位置重生错误 + Trickster无法触发 | P0 | 修复：改用 stablePosition（在 Stable 状态持续更新），移动平台后重生在新位置。OnCollisionEnter2D 不再限制只有 MarioController，任何有 Rigidbody2D 的对象从上方踩踏都能触发 |
| B035 | ✅ **已修复 (Session 18)** 隐藏通道按S不传送 | P0 | 修复：InputManager 新增 S键下蹲交互路由，通过 MarioInteractionHelper 检测周围 HiddenPassage 并调用 TryEnterPassage() |
| B036 | ✅ **已修复 (Session 18)** 全局性能优化（D3D11 GPU Timeout 预防） | P0 | 修复：(1) GameUI/ScanAbility/TricksterController 的 OnGUI 中所有 GUIStyle 缓存为类字段，消除每帧 new 分配的 GC 压力；(2) TricksterAbilitySystem.BindNearestProp 改用缓存道具列表，消除 Update 中每帧 FindObjectsByType 场景扫描；(3) GameManager.ClearAllCooldowns 缓存场景对象引用，避免无冷却模式下每帧 3 次 FindObjectsOfType；(4) OnRenderObject GL 绘制添加主相机过滤，避免多相机重复绘制 |
| B037 | ✅ **已优化 (Session 19)** 弹跳平台效果不好，只有宽边有效 | P0 | 优化：重写 BouncyPlatform，使用碰撞接触点法线(contact.normal)确定弹射方向，玩家从任何方向碰撞平台都会沿接触面法线方向弹射。新增 bounceForceMultiplier 可调强度。参考 Sonic 弹簧/rastating 教程的法线方向弹射最佳实践 |
| B038 | ✅ **已优化 (Session 19)** 单向平台单独S键容易误操作 | P0 | 优化：将单向平台下落从单独S键改为S+Jump组合键。参考 Super Smash Bros/Celeste/行业标准的 Down+Jump 穿越平台做法。InputManager 拆分为 HandleDropThrough(S+Jump) 和 HandlePassageInteraction(S单独) |
| B039 | ✅ **已优化 (Session 19)** 隐藏通道只能单向穿越，无法从出口返回 | P0 | 优化：重写 HiddenPassage 支持双向穿越。新增 TeleportMode 状态机(Forward/Return)、返回触发区(HiddenPassageReturnTrigger)、传送冷却时间。出口处自动创建返回触发区，玩家按S即可传回入口 |

### 关于 B002 的修复方法

如果你使用的是 **Unity 2022.3 LTS**，`Rigidbody2D` 的属性名是 `velocity` 而非 `linearVelocity`。当前仓库中所有文件已使用 `velocity`，无需手动替换。

如果你使用的是 **Unity 6.x**，则需要将 `velocity` 替换为 `linearVelocity`。

---

> ⚠️ **注意**：测试操作步骤、测试用例清单、场景搭建说明已移至 `MarioTrickster_Testing_Guide.md`。

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
| 10 | 自动化测试框架 | ✅ **Session 13 更新** | EditMode 59 测试 + PlayMode 21 测试 + TestReportRunner 一键报告工具，覆盖组件依赖/PlayerHealth/伪装系统/道具状态机/胜负判定/暂停/能量系统/扫描技能/相机控制器/GameUI |

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

### Session 21 记录（2026-04-04）

**本次完成功能（BouncyPlatform 弹射抛物线根因修复）：**

| 项目 | 说明 |
|------|------|
| 根因分析 | Session 20 的 BounceStun 仅降低了 acceleration/deceleration 倍率，但 HandleDirection 的 MoveTowards 目标值仍为 maxSpeed(=9)，导致弹射后下一帧水平速度被截断至 maxSpeed，抛物线变成垂直下落的直角三角形 |
| MarioController: SetFrameVelocity() | 新增公开方法，允许外部（BouncyPlatform）绝对注入帧速度。设置 _frameVelocityOverridden 标记，下一次 FixedUpdate 跳过 rb.velocity 读回，直接使用注入速度 |
| MarioController: maxSpeed 截断跳过 | BounceStun 期间 HandleDirection 的 MoveTowards 目标值改为 max(|currentVelocity.x|, maxSpeed)，允许弹射速度超过 maxSpeed 自由飞行 |
| BouncyPlatform: 废弃 AddForce | 喜剧延迟结束后直接调用 mario.SetFrameVelocity(launchVelocity)，替代旧的 rb.velocity 赋值 + mario.Bounce() |
| BouncyPlatform: 清除旧速度 | 碰撞冻结时调用 SetFrameVelocity(Vector2.zero) 清除内部帧速度，防止重力积累污染弹射轨道 |
| TestSceneBuilder: 9E 标签更新 | Stage 9E 标签新增 [S21] SetFrameVelocity absolute injection + maxSpeed bypass |

**修改文件：**
- `MarioController.cs` - 新增 SetFrameVelocity() + _frameVelocityOverridden 标记 + HandleDirection BounceStun 期间跳过 maxSpeed 截断
- `BouncyPlatform.cs` - 废弃 rb.velocity 赋值/mario.Bounce()，改用 SetFrameVelocity() 绝对注入 + 碰撞时清除旧速度
- `TestSceneBuilder.cs` - Stage 9E 标签更新反映 S21 功能

---

### Session 20 记录（2026-04-04）

**本次完成功能（Sprint 2 核心体验升级：Trickster 道具操控系统重构 + BouncyPlatform 物理弹跳优化）：**

| 项目 | 说明 |
|------|------|
| 模块一: 视觉连线反馈 | TricksterAbilitySystem 融入后绘制红/灰连线到范围内所有可操控道具。红色加粗=当前锁定目标，灰色细线=备选目标。使用 LineRenderer 对象池(Awake预实例化)，严禁 Update 中 Instantiate |
| 模块一: 目标高亮反馈 | ControllablePropBase 实现 SetHighlight()，当前锁定目标 Sprite 微红脉冲动画，解锁时恢复原色。Awake 中预缓存颜色 |
| 模块一: 方向键磁吸切换 | 融入状态下 InputManager 拦截方向键，转发为 TricksterController.OnDirectionInput()，按方向切换最近的备选道具。带 0.2s 防抖冷却 |
| 模块一: 接口扩展 | IControllableProp 新增 SetHighlight(bool) + GetTransform()，ControllablePropBase 统一实现 |
| 模块二: 碰撞法线修正 | BouncyPlatform 新增 CalcCorrectedNormal()：将碰撞法线与平台 Up 方向混合(normalBlendWithUp=0.4)，过滤极端 X 轴法线(threshold=0.85) |
| 模块二: 抛物线修复 (BounceStun) | MarioController 新增 ApplyBounceStun()：弹跳后大幅降低横向操控力(accel 12%, decel 8%)，落地自动解除。参考 Celeste/Sonic 弹簧最佳实践 |
| 模块二: 弹射方向优化 | 使用修正后法线做反射(Vector2.Reflect)，与位置偏移方向混合，保证最小向上分量(0.4)防止贴地滑行 |

**修改文件：**
- `IControllableProp.cs` - 新增 SetHighlight(bool) + GetTransform() 接口方法
- `ControllablePropBase.cs` - 实现 SetHighlight(微红脉冲) + GetTransform()，Awake 预缓存颜色
- `TricksterAbilitySystem.cs` - 重构：LineRenderer 对象池 + 红/灰连线 + 方向键磁吸切换 + 目标高亮
- `TricksterController.cs` - 新增 OnDirectionInput() + IsFullyBlended + 切换防抖冷却
- `InputManager.cs` - 融入状态下方向键拦截转发为磁吸切换指令
- `BouncyPlatform.cs` - 碰撞法线修正 + BounceStun 替代 KnockbackStun + 弹射方向优化
- `MarioController.cs` - 新增 ApplyBounceStun() 方法 + BounceStun 状态机
- `TestSceneBuilder.cs` - Stage 5/9E 标签更新反映 S20 功能

---

### Session 19 记录（2026-04-04）

**本次完成功能（参考行业最佳实践优化三大关卡元素）：**

| 项目 | 说明 |
|------|------|
| B037 优化: 弹跳平台法线方向弹射 | 重写 BouncyPlatform，使用碰撞接触点法线(contact.normal)确定弹射方向。玩家从平台任何面碰撞都会沿法线方向弹射。新增 bounceForceMultiplier 可调强度。参考 Sonic 弹簧 + rastating 教程 |
| B038 优化: 单向平台S+Jump组合键 | 将单向平台下落从单独S键改为S+Space组合键，防止误操作。参考 Super Smash Bros/Celeste 的 Down+Jump 行业标准 |
| B039 优化: 隐藏通道双向穿越 | 重写 HiddenPassage 支持双向传送。新增 TeleportMode 状态机(Forward/Return)、返回触发区(HiddenPassageReturnTrigger)、传送冷却时间。出口处自动创建返回触发区 |

**新增文件：**
- `HiddenPassageReturnTrigger.cs` - 隐藏通道返回触发区（出口位置动态创建）

**修改文件：**
- `BouncyPlatform.cs` - 重写：碰撞法线方向弹射 + bounceForceMultiplier 可调强度
- `OneWayPlatform.cs` - 更新注释，配合 S+Jump 组合键
- `HiddenPassage.cs` - 重写：双向穿越 + TeleportMode 状态机 + 返回触发区创建 + 冷却时间
- `InputManager.cs` - 拆分 S+Jump 组合键(HandleDropThrough) 和 S单独(HandlePassageInteraction)
- `MarioInteractionHelper.cs` - 重构：支持双向隐藏通道返回触发区检测
- `TestSceneBuilder.cs` - Stage 9E/9F/9H 标签更新（反映新操作方式）

---

### Session 18 记录（2026-04-04）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| Gizmo 范围可视化 | TricksterAbilitySystem 新增运行时 Gizmo 显示：绿色圆圈显示 controlRange，黄色圆圈显示陷阱伤害范围。调整 controlRange 参数即可控制可操控范围 |
| B029 修复: 伪装后按L无法控制 | 动态重绑定（每次按L重新搜索最近道具）+ 融入后自动绑定最近道具 |
| B030 修复: 火陷阱击退飞出 | 创建 KnockbackHelper 统一击退方向计算，Mario 移动方向反向后退一小段距离。影响：FireTrap/SpikeTrap/PendulumTrap/BouncingEnemy/DamageDealer |
| B031 修复: 摆锤隐蔽状态无法控制 | 融入后自动重新绑定最近道具 |
| B032 修复: 弹跳平台只有宽边有效 | 移除方向限制，两边都触发弹跳。新增 comedyDelay 喜剧延迟（0.25秒），Trickster 可在延迟期间增强弹性 |
| B033 修复: 单向平台按S不下落 | InputManager S键路由 + MarioInteractionHelper 检测脚下 OneWayPlatform |
| B034 修复: 崩塌平台位置重生错误 + Trickster无法触发 | stablePosition 持续更新 + 任何 Rigidbody2D 从上方踩踏都触发 |
| B035 修复: 隐藏通道按S不传送 | InputManager S键路由 + MarioInteractionHelper 检测 HiddenPassage |
| B036 修复: 全局性能优化（D3D11 GPU Timeout 预防） | GUIStyle缓存（GameUI/ScanAbility/TricksterController）+ FindObjectsOfType缓存（TricksterAbilitySystem/GameManager）+ GL绘制主相机过滤 |

**新增文件：**
- `KnockbackHelper.cs` - 统一击退方向计算工具
- `MarioInteractionHelper.cs` - Mario S键下蹲交互辅助

**修改文件：**
- `TricksterAbilitySystem.cs` - Gizmo可视化 + 动态重绑定 + 融入后自动绑定 + **性能优化: 缓存道具列表 + GL主相机过滤**
- `FireTrap.cs` / `SpikeTrap.cs` / `PendulumTrap.cs` / `BouncingEnemy.cs` - KnockbackHelper 统一击退
- `DamageDealer.cs` - KnockbackHelper 统一击退
- `BouncyPlatform.cs` - 两边弹跳 + comedyDelay
- `CollapsingPlatform.cs` - 位置重生修复 + Trickster触发
- `InputManager.cs` - S键下蹲交互路由
- `GameUI.cs` - **性能优化: 10个 GUIStyle 缓存为类字段**
- `ScanAbility.cs` - **性能优化: 3个 GUIStyle 缓存为类字段**
- `TricksterController.cs` - **性能优化: 1个 GUIStyle 缓存为类字段**
- `GameManager.cs` - **性能优化: ClearAllCooldowns 缓存场景对象引用**
- `TestSceneBuilder.cs` - Stage 9 标签更新
- `Testing_Guide.md` - 影响矩阵新增 KnockbackHelper/MarioInteractionHelper/DamageDealer

---

### Session 17 记录（2026-04-03）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| B027 根本修复: 相机边界被 LevelManager 覆盖 | 根因：`LevelManager.Start()` 在运行时调用 `CameraController.SetBounds()` 用默认值 `levelMaxX=50` 覆盖了 TestSceneBuilder 设置的正确边界。Session 16 仅设置了 CameraController 的值但忽略了 LevelManager 会覆盖。修复：在 TestSceneBuilder 中同步设置 LevelManager 的 levelMinX/levelMaxX/levelMinY/levelMaxY 字段 |
| B028 修复: Trickster 跳到 Mario 头上无法跳走 | 根因：Mario 和 Trickster 都在 Default Layer，物理碰撞让 Trickster 卡在 Mario 头上，但地面检测 BoxCast 只检测 Ground Layer，所以 `_grounded=false` 无法跳跃。修复：创建 Player/Trickster 专用 Layer，`Physics2D.IgnoreLayerCollision` 禁用两者碰撞，角色互相穿过不会卡住 |
| B029 分析: 伪装后无法 L 控制平台 + 移动平台上难以融入 | 根因二合一：1) TricksterAbilitySystem 只在变身瞬间绑定最近道具，后续不重新绑定；2) DisguiseSystem 融入检测用世界坐标绝对静止，在移动平台上平台速度被注入角色导致永远无法融入。方案已提出待用户确认：A) 动态重绑定 B) 平台相对静止检测 C) 扩大操控范围 |

**代码统计：**
- 修改 TestSceneBuilder.cs（B027 LevelManager 边界 + B028 Player/Trickster Layer 分离 + 禁用碰撞）
- 更新 SESSION_TRACKER.md、MarioTrickster_Progress_Summary.md、MASTER_TRACKER.md

---

### Session 16 记录（2026-04-03）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| B022 CollapsingPlatform stateTimer 字段隐藏修复 | 重命名为 `collapseTimer`，消除与基类字段隐藏冲突 |
| B023 受伤击退效果修复 | MarioController/TricksterController 添加 knockback stun 机制，受击期间暂停帧速度架构的速度覆盖，让 AddForce 击退力生效。DamageDealer 修改为通知控制器进入 stun 状态 |
| B024 UI时间显示不全修复 | GameUI.cs 加宽计时器显示区域并增加 Y 偏移，防止被裁剪 |
| B025 无冷却调试开关 (F9) | GameManager 新增 `noCooldownMode` 全局开关，F9 键切换。开启时自动清除 DisguiseSystem、ScanAbility、ControllablePropBase 的冷却。三个系统均新增 ResetCooldown() 方法。GameUI 显示绿色指示器 |
| B026 测试场景重构为闯关形式 | TestSceneBuilder 完全重写，按 Testing_Guide 测试顺序从左到右排列 9 个 Stage + 终点 GoalZone。每个区域有金黄色大标题 + 白色操作说明（TextMesh），Stage 9 内部分 9A-9I 子区域 |

**代码统计：**
- 修改 MarioController.cs（添加 ApplyKnockback/isKnockedBack/knockbackTimer）
- 修改 TricksterController.cs（添加 ApplyKnockback/isKnockedBack/knockbackTimer）
- 修改 DamageDealer.cs（击退后通知控制器进入 stun）
- 修改 GameUI.cs（加宽计时器 + 无冷却指示器）
- 修改 GameManager.cs（新增 noCooldownMode + F9 切换 + ClearAllCooldowns）
- 修改 DisguiseSystem.cs、ScanAbility.cs、ControllablePropBase.cs（新增 ResetCooldown）
- 修改 CollapsingPlatform.cs（stateTimer → collapseTimer）
- 重写 TestSceneBuilder.cs（闯关形式 + 场景指示标签 + B027 相机边界设置）
- 新增 SESSION_TRACKER §0.8 测试场景同步规则
- 更新 SESSION_TRACKER.md、MarioTrickster_Progress_Summary.md、MASTER_TRACKER.md

---

### Session 14 记录（2026-04-02）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| MASTER_TRACKER.md 全覆盖全局总览 | 新建全局项目总览文档，将 GAME_DESIGN.md 全部10个章节的每个功能点与实际代码实现、测试状态进行一一映射。包含 4 张分类矩阵（§1.1核心机制、§1.2视角与预警、§1.3平衡性与架构、§1.4美术与音效）、模块完成度统计（含网络联机维度）、双向追踪指南、演进路线图（含 Sprint 4）、AI自动扩展规则（§5）。 |
| 联动矩阵全覆盖升级 | 联动矩阵新增 4 行变更类型：架构重构/底层变更、美术/音效资源接入、网络联机开发、平衡性/数值调整。确保未来任何类型的变更都有对应的联动规则。 |
| AI防错规则强化 | 新增“MASTER_TRACKER 自检”规则：每次新增功能/重构架构/接入资源/调整平衡性后，强制检查并更新 MASTER_TRACKER §1 矩阵和 §2 完成度。 |
| 开场模板升级 | 积分提醒中新增 MASTER_TRACKER.md 同步要求，确保紧急存档时全局状态不丢失。 |
| AI自动扩展规则 | MASTER_TRACKER §5 定义了4条强制规则：新功能自动收录、架构变更追踪、百分比联动、测试状态强绑定。 |

**代码统计：**
- 重写 MASTER_TRACKER.md（从 73 行扩展到全覆盖）
- 更新 SESSION_TRACKER.md（§0.1/§0.3/§0.4/§1/§7/§8）
- 更新 README.md（导航表+工作流图解）
- 更新 AI_WORKFLOW.md（§1.2/§1.4/核心设计描述/紧急存档模板）
- 更新 MarioTrickster_Progress_Summary.md（头部+Session记录）

---

### Session 13 记录（2026-04-02）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| TestReportRunner 测试报告工具 | 新增 Editor 工具脚本 `TestReportRunner.cs`，解决 Unity Test Runner 需要逐个点击才能查看错误详情的问题。提供三个菜单入口（EditMode/PlayMode/All），一键运行所有测试并将完整错误信息（包括堆栈跟踪）导出到 `TestReport.txt`，同时在 Console 中完整打印。报告包含“快速复制区”方便用户直接发给 AI 统一修复。 |
| Editor asmdef 更新 | `MarioTrickster.Editor.asmdef` 添加 `UnityEditor.TestRunner` 和 `UnityEngine.TestRunner` 引用，启用 `overrideReferences` 并添加 `nunit.framework.dll`，以支持 TestRunnerApi 编程调用。 |
| EditMode 测试修复 (16个) | 根因：EditMode 测试中 `AddComponent<T>()` 不会自动调用 `Awake()`，导致依赖 Awake 初始化的组件状态全为默认值。修复：在测试代码中添加 `ForceAwake()` 辅助方法（通过反射调用私有 Awake 方法），在每个需要初始化的测试中统一调用。修复涉及 PlayerHealth(6个)、EnergySystem(7个)、GoalZone/KillZone/MovingPlatform(3个)。 |
| DisguiseSystem 测试修复 (1个) | `GetDebugStatus()` 返回中文“未配置伪装形态”不包含数字 "0"，但测试断言检查的是 `Contains("0")`。修复为 `Contains("未配置") \|\| Contains("0")`。 |
| Testing Guide 章节重组 | 原第三章有 3.1（TestReportRunner）+ 3.2（内置 Test Runner）+ 3.3（EditMode）+ 3.3（PlayMode）结构混乱。重组为 3.1（统一运行方式说明）+ 3.2（EditMode）+ 3.3（PlayMode）+ 3.4（故障排除），消除两种运行方式的误导。 |
| 联动矩阵补强 (3层防护) | §0.3 联动矩阵新增第 7 行“重编号/重组文档章节”，第 4 行“修改文档体系”加 ❗ 提醒。§0.4 防错规则新增第 5 条“章节引用同步检查”，给出 2 条具体 grep 命令覆盖 § 和“第X章”两种引用格式。确保新对话 AI 也能可靠执行跨文档引用同步。 |
| TestReportRunner PlayMode 修复 | 根因：PlayMode 测试触发域重载，非持久化回调实例被销毁，导致 `RunFinished` 不触发、报告不生成。修复：使用 `[InitializeOnLoad]` 静态构造函数注册持久化 `ICallbacks`，域重载后自动重新注册；用 `SessionState` 跟踪运行状态；用 `EditorApplication.delayCall` 延迟弹窗避免被吞。 |
| 全部测试 89/89 通过 | 用户验证确认：手动 9/9 + EditMode 59/59 + PlayMode 21/21 + Testing Guide 3.4 故障排除全部通过。项目进入全绿状态。 |

**代码统计：**
- 新增 TestReportRunner.cs（约 420 行，含 PlayMode 持久化修复）
- 修改 MarioTrickster.Editor.asmdef
- 重写 ComponentSetupTests.cs（添加 ForceAwake 辅助方法，修复 16+1 个失败测试）
- 重组 MarioTrickster_Testing_Guide.md 第三章章节结构
- 补强 SESSION_TRACKER.md §0.3 联动矩阵 + §0.4 防错规则
- 更新 SESSION_TRACKER.md, MarioTrickster_Progress_Summary.md, MarioTrickster_Testing_Guide.md

---

### Session 12 记录（2026-04-02）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| B018 游戏结束UI修复 | 根因：TestSceneBuilder 未创建 GameUI 对象。修复：TestSceneBuilder 新增 GameUI 对象创建（挂载到 Managers）。 |
| B019 originalColor 序列化冲突 | 根因：ControllableBlock 和父类都声明了 private Color originalColor。修复：父类改 protected，子类移除重复声明。 |
| B020 第二回合终点无反应 | 根因：GoalZone.triggered 在第一回合设为 true 后，ResetRound 未重置。修复：GoalZone 新增 ResetTrigger()，GameManager.ResetRound() 调用它。同时重置所有 ControllablePropBase。 |
| B021 移除 RESUMED 提示 | 用户反馈暂停恢复后显示 RESUMED 没必要。已移除 GameManager 和 GameUI 中的恢复提示逻辑。 |
| B016 源头修复 | TestSceneBuilder Build 前先清理已有 CameraController，避免重复叠加。Clear 方法也优化。 |
| 测试用例更新 | 新增 5 个 GameUI EditMode 测试用例。EditMode 测试总计 59 用例。 |

**代码统计：**
- 修改了 GameManager.cs, GameUI.cs, GoalZone.cs, ControllablePropBase.cs, ControllableBlock.cs, TestSceneBuilder.cs, ComponentSetupTests.cs
- 更新了 SESSION_TRACKER.md, MarioTrickster_Progress_Summary.md, MarioTrickster_Testing_Guide.md

**用户验证结果：**
- ✅ 测试 1-8 全部通过，所有 P0 Bug 已修复并验证

---

### Session 11 记录（2026-04-02）

**本次完成功能：**

| 项目 | 说明 |
|------|------|
| B016 镜头晃动修复 | 重写 CameraController.cs。根因三重叠加：(1) LookAhead 依赖 IsMoving 属性在静止时频繁切换；(2) 死区边缘振荡；(3) Shake 协程永久偏移相机。修复：滞后(hysteresis)移动检测 + 渐进式死区(soft dead zone) + 独立震动偏移量 + SmoothDamp 替代 Lerp。 |
| B015 扫描提示修复 | 修复 ScanAbility.cs 脉冲颜色时序问题（先检测再启动脉冲），新增扫描结果文字提示（区分范围内/外/未伪装），新增 OnScanPerformed 事件。 |
| 测试用例更新 | 新增 CameraControllerTests（5个用例）+ ScanAbility_OnScanPerformed 测试。EditMode 测试总计 45+ 用例。 |
| 协作文档体系优化 | 新增 SESSION_TRACKER.md 作为每次对话的快速入口，包含当前状态、本次测试清单、反馈模板、待办队列、协作工作流图。 |

**代码统计：**
- 修改了 CameraController.cs（重写）, ScanAbility.cs, ComponentSetupTests.cs
- 新增了 SESSION_TRACKER.md
- 更新了 MarioTrickster_Progress_Summary.md, MarioTrickster_Testing_Guide.md

**已验证通过：**
- B016 镜头晃动：✅ 删除多余组件后稳定
- B015 扫描提示：✅ 伪装后扫描正常，未伪装显示正确

**待修复项（留给下一个Session）：**
- B018 游戏结束UI未显示：GameManager判定已成功，但GameUI未渲染胜利画面。

---

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
| ComponentSetupTests.cs | EditMode 自动化测试（59 用例）：验证 RequireComponent 自动添加组件、PlayerHealth 完整伤害/治疗/死亡/无敌帧/重置逻辑、DisguiseSystem 初始状态和无配置安全性、MovingPlatform Kinematic 设置、GoalZone/KillZone Trigger 设置、IControllableProp 接口实现、InputManager 启用/禁用、EnergySystem、ScanAbility、CameraController、GameUI |
| GameplayTests.cs | PlayMode 自动化测试（21 用例）：验证 Mario/Trickster 左右移动和停止减速、跳跃物理、重力下落、伪装系统运行时行为和冷却机制、ControllableHazard/Block 的 Telegraph→Active 状态机转换、移动平台两点间运动、GameManager 胜负判定（Mario到达终点/Mario死亡）、暂停/恢复 TimeScale、回合重置恢复生命值、Mario.Die() 禁用控制器、Bounce 弹跳、InputManager 集成 |
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

### Session 14 升级记录（2026-04-02）

**全局总览系统与文档联动体系升级**

| 升级项 | 说明 |
|--------|------|
| MASTER_TRACKER | 新增全局项目总览文档，包含4张分类映射矩阵、模块完成度统计、双向追踪指南、演进路线图和AI自动扩展规则。 |
| 联动矩阵升级 | `SESSION_TRACKER.md` 的联动矩阵从7行扩展到11行，覆盖架构重构、美术接入、网络联机等所有变更类型。 |
| AI 防错规则 | 新增 "MASTER_TRACKER 自检" 强制规则，确保新功能自动收录。 |
| 6文档体系 | 更新 `README.md` 和 `AI_WORKFLOW.md`，将5文档体系升级为6文档联动体系。 |

### Session 15 升级记录（2026-04-02）

**关卡设计系统 (Level Design Framework)**

| 升级项 | 说明 |
|--------|------|
| 框架核心 | 设计低耦合+高内聚的关卡框架，包含 `LevelElementBase`、`LevelElementRegistry` 和 `ControllableLevelElement`。 |
| 关卡元素 | 实现 9 种首批关卡元素：地刺、摆锤、火焰、弹跳怪、弹跳平台、单向平台、崩塌平台、隐藏通道、伪装墙。 |
| 自动化集成 | 更新 `TestSceneBuilder`，自动集成所有新关卡元素。 |
| 自动化测试 | 编写 `LevelElementTests.cs`，包含 55 个 EditMode 测试用例，覆盖框架核心和所有元素。 |

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
│   │   ├── MarioController.cs      ✅ Mario移动/跳跃/平台跟随 (Session 21 更新: SetFrameVelocity+maxSpeed跳过)
│   │   └── PlayerHealth.cs          ✅ 生命值管理
│   ├── Enemy/
│   │   ├── TricksterController.cs   ✅ Trickster控制/平台跟随 (Session 20 更新: 磁吸切换+IsFullyBlended)
│   │   ├── DisguiseSystem.cs        ✅ 伪装/变身系统 (Session 10 更新: 集成 EnergySystem)
│   │   └── SimpleEnemy.cs           ✅ 简单巡逻敌人
│ ├── Scripts/
│   ├── Core/
│   │   ├── GameManager.cs           ✅ 游戏状态/胜负判定 (Session 12 更新: 修复B020/B021)
│   │   ├── InputManager.cs          ✅ 输入分发中心 (Session 20 更新: 融入状态方向键拦截)
│   │   ├── MarioInteractionHelper.cs ✅ Mario交互辅助 (Session 19 更新: 双向通道返回触发区)
│   │   ├── KnockbackHelper.cs       ✅ 统一击退方向计算 (Session 18 新增)
│   │   ├── LevelManager.cs          ✅ 关卡管理器
│   │   ├── GoalZone.cs              ✅ 终点触发器 (Session 12 更新: +ResetTrigger)
│   │   ├── KillZone.cs              ✅ 死亡区域
│   │   ├── DamageDealer.cs          ✅ 伤害触发器 (Session 18 更新: KnockbackHelper统一击退)
│   │   ├── Collectible.cs           ✅ 可收集物品
│   │   ├── Breakable.cs             ✅ 可破坏方块
│   │   ├── MovingPlatform.cs        ✅ 移动平台 (Session 6 重写: 速度注入法)
│   │   └── SpriteAutoFit.cs         ✅ Sprite 自动适配 (Session 7 新增)
│   ├── LevelElements/               ✅ 关卡设计系统 (Session 15 新增)
│   │   ├── LevelElementBase.cs      ✅ 关卡元素统一基类
│   │   ├── LevelElementRegistry.cs  ✅ 自动注册中心
│   │   ├── ControllableLevelElement.cs ✅ 桥接基类
│   │   ├── Traps/                   ✅ 陷阱类
│   │   │   ├── SpikeTrap.cs         ✅ 地刺陷阱 (S18: KnockbackHelper统一击退)
│   │   │   ├── PendulumTrap.cs      ✅ 摆锤绳索陷阱 (S18: KnockbackHelper统一击退)
│   │   │   ├── FireTrap.cs          ✅ 火焰陷阱 (S18: 击退方向修正+KnockbackHelper)
│   │   │   └── BouncingEnemy.cs     ✅ 弹跳小怪物 (S18: KnockbackHelper统一击退)
│   │   ├── Platforms/               ✅ 平台类
│   │   │   ├── BouncyPlatform.cs    ✅ 弹跳平台 (S20: 法线修正+BounceStun抛物线保留)
│   │   │   ├── OneWayPlatform.cs    ✅ 单向平台 (S19: S+Jump组合键)
│   │   │   └── CollapsingPlatform.cs ✅ 崩塌平台 (S18: 位置重生修复+Trickster触发)
│   │   └── HiddenPassages/          ✅ 隐藏通道类
│   │       ├── HiddenPassage.cs     ✅ 隐藏通道 (S19: 双向穿越+TeleportMode)
│   │       ├── HiddenPassageReturnTrigger.cs ✅ 返回触发区 (S19 新增)
│   │       └── FakeWall.cs          ✅ 伪装墙壁
│   ├── Ability/
│   │   ├── IControllableProp.cs     ✅ 可操控道具接口 (S20: +SetHighlight+GetTransform)
│   │   ├── ControllablePropBase.cs  ✅ 操控状态机基类 (S20: +SetHighlight微红脉冲+GetTransform)
│   │   ├── TricksterAbilitySystem.cs ✅ 能力系统管理器 (S20: 视觉连线+磁吸切换+目标高亮)
│   │   ├── EnergySystem.cs          ✅ Trickster 能量系统 (Session 10 新增)
│   │   ├── ScanAbility.cs           ✅ Mario 扫描技能 (Session 11 更新: 修复B015)
│   │   ├── ControllablePlatform.cs  ✅ 可操控移动平台 (Session 6 更新: 速度注入法)
│   │   ├── ControllableHazard.cs    ✅ 可操控危险道具
│   │   └── ControllableBlock.cs     ✅ 可操控方块 (Session 12 更新: 移除重复originalColor)
│   ├── Camera/
│   │   └── CameraController.cs      ✅ 相机跟随逻辑 (Session 11 重写: 修复B016镜头晃动)
│   ├── Editor/
│   │   ├── TestSceneBuilder.cs      ✅ 一键生成测试场景 (Session 12 更新: +GameUI)
│   │   ├── TestReportRunner.cs      ✅ 一键测试报告工具 (Session 13 新增: 完整错误导出)
│   │   └── MarioTrickster.Editor.asmdef ✅ Editor Assembly Definition (Session 13 更新: +TestRunner引用)
│   ├── MarioTrickster.asmdef        ✅ 主项目 Assembly Definition (Session 9 新增)
│   └── UI/
│       └── GameUI.cs                ✅ 基础HUD (Session 12 更新: 增强结束/暂停/失败反馈, 移除RESUMED提示)
├── Tests/
│   ├── EditMode/
│   │   ├── EditModeTests.asmdef     ✅ Assembly Definition (Session 8 新增)
│   │   └── ComponentSetupTests.cs   ✅ 组件依赖/配置验证测试 (Session 12 更新: 59个用例, +GameUI)
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

> ⚠️ **注意**：操作键位参考和手感参数说明已移至 `MarioTrickster_Testing_Guide.md`。
> ⚠️ **注意**：开场模板已移至 `SESSION_TRACKER.md`。
