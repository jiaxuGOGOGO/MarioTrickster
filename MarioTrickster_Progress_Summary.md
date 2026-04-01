# MarioTrickster 项目进度总结

> 更新时间：2026-04-01 (Session 2) | 单一真相源：AI 新对话时自动读取本文件获取完整上下文

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

---

### 2.4 MVP 核心脚本（Sprint 1 大部分完成）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| MarioController.cs | Assets/Scripts/Player/ | ✅ | 完整移动/跳跃（coyote time + buffered jump）/地面检测 |
| PlayerHealth.cs | Assets/Scripts/Player/ | ✅ | 通用生命值/无敌帧/受伤闪烁/死亡事件 |
| TricksterController.cs | Assets/Scripts/Enemy/ | ✅ | 伪装者基础控制（移动/跳跃/与DisguiseSystem协作） |
| DisguiseSystem.cs | Assets/Scripts/Enemy/ | ✅ | Sprite替换变身/冷却/场景融入/多形态切换（已修复类型转换Bug） |
| InputManager.cs | Assets/Scripts/Core/ | ✅ **新增** | 本地双人输入管理（P1:WASD+Space, P2:方向键+RCtrl+RShift+Enter, 支持手柄） |
| GameManager.cs | Assets/Scripts/Core/ | ✅ **新增** | 游戏状态/胜负判定/暂停/重启/计时器/单例模式 |
| CameraController.cs | Assets/Scripts/Camera/ | ✅ **新增** | 平滑跟随Mario/前瞻偏移/死区/关卡边界限制/相机震动 |

### 2.5 辅助脚本（Sprint 1 新增）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| GoalZone.cs | Assets/Scripts/Core/ | ✅ **新增** | 终点触发器，Mario碰触后触发胜利 |
| KillZone.cs | Assets/Scripts/Core/ | ✅ **新增** | 死亡区域（掉落深渊），碰触即死 |
| DamageDealer.cs | Assets/Scripts/Core/ | ✅ **新增** | 通用伤害触发器（尖刺/怪物/Hazard伪装），支持击退 |
| Collectible.cs | Assets/Scripts/Core/ | ✅ **新增** | 可收集物品（金币/回血/加速），带浮动动画 |
| Breakable.cs | Assets/Scripts/Core/ | ✅ **新增** | 可破坏方块（砖块/问号砖块），从下方顶撞触发 |
| MovingPlatform.cs | Assets/Scripts/Core/ | ✅ **新增** | 移动平台，两点间来回移动，角色站上跟随 |
| SimpleEnemy.cs | Assets/Scripts/Enemy/ | ✅ **新增** | 简单巡逻敌人，边缘/墙壁检测自动转向，可被踩消灭 |
| GameUI.cs | Assets/Scripts/UI/ | ✅ **新增** | 基础HUD（生命值/计时器/回合信息/胜负画面），OnGUI后备显示 |
| LevelManager.cs | Assets/Scripts/Core/ | ✅ **新增** | 关卡管理（出生点/边界/可伪装对象列表） |

### 2.6 配置文件更新

| 文件 | 状态 | 说明 |
|------|------|------|
| Packages/manifest.json | ✅ **更新** | 新增 com.unity.inputsystem (1.7.0) 和 com.unity.cinemachine (2.9.7) 包依赖 |
| Assets/InputActions/GameControls.inputactions | ✅ **新增** | Unity Input System 配置文件（Player1/Player2/UI三个ActionMap） |

---

## 三、已知未修复的 Bug / 待确认事项

| 编号 | 描述 | 优先级 | 备注 |
|------|------|--------|------|
| B001 | 所有脚本已通过离线语法验证（dotnet build），但尚未在Unity中实际运行测试 | 中 | 需要在Unity中挂载到GameObject并运行 |
| B002 | MarioController.cs 使用 `rb.linearVelocity`，这是 Unity 6 的 API。Unity 2022.3 使用 `rb.velocity` | 高 | **如果在 Unity 2022.3 中编译报错，需要全局替换 `linearVelocity` → `velocity`** |
| B003 | InputManager.cs 中 Player1 使用 WASD，Player2 使用方向键，但 MarioController 原代码中也绑定了方向键 | 中 | InputManager 已接管输入分发，MarioController 不直接读取键盘，所以不冲突 |
| B004 | 场景 SampleScene.unity 是空白模板，需要手动搭建测试场景 | 高 | 见下方"下一步计划" |

### 关于 B002 的修复方法

如果你使用的是 **Unity 2022.3 LTS**，`Rigidbody2D` 的属性名是 `velocity` 而非 `linearVelocity`。需要在以下文件中全局替换：

```
MarioController.cs     → rb.linearVelocity → rb.velocity
TricksterController.cs → rb.linearVelocity → rb.velocity
SimpleEnemy.cs         → rb.velocity（已使用正确的API）
```

在 Rider 中操作：`Ctrl+Shift+R` → 查找 `rb.linearVelocity` → 替换为 `rb.velocity` → 全部替换。

如果你使用的是 **Unity 6.x**，则 `linearVelocity` 是正确的，无需修改。

---

## 四、下一步开发计划（MVP 优先级排序）

### 第一优先级：场景搭建与核心验证（Sprint 1 收尾）

| 序号 | 任务 | 状态 | 说明 |
|------|------|------|------|
| 1 | 在Unity中创建测试场景 | ⚬ 待做 | 用Tilemap搭建一个简单的测试关卡（地面+平台+终点+深渊） |
| 2 | 下载并导入像素素材 | ⚬ 待做 | 从 Pixel Adventure 或 Block Land 下载素材包 |
| 3 | 创建Mario预制体 | ⚬ 待做 | 挂载 MarioController + PlayerHealth + Rigidbody2D + BoxCollider2D |
| 4 | 创建Trickster预制体 | ⚬ 待做 | 挂载 TricksterController + DisguiseSystem + Rigidbody2D + BoxCollider2D |
| 5 | 创建管理器对象 | ⚬ 待做 | 空GameObject挂载 GameManager + InputManager + LevelManager |
| 6 | 配置相机 | ⚬ 待做 | Main Camera挂载 CameraController，设置跟随Mario |
| 7 | 放置GoalZone和KillZone | ⚬ 待做 | 终点旗帜 + 关卡底部死亡区域 |
| 8 | **核心玩法验证** | ⚬ 待做 | 两人操作测试：Mario能跑能跳，Trickster能变身，胜负判定正常 |

### 第二优先级：游戏体验（Sprint 2，预计 1-2 周）

| 序号 | 任务 | 说明 |
|------|------|------|
| 9 | Trickster特殊能力 | 放置假障碍物、触发陷阱、召唤AI怪物 |
| 10 | 音效集成 | 跳跃/死亡/变身/胜利音效 |
| 11 | 升级为Cinemachine | 已添加包依赖，替换CameraController为Cinemachine Virtual Camera |

### 第三优先级：打磨（Sprint 3）

| 序号 | 任务 | 说明 |
|------|------|------|
| 12 | 平衡性调整 | 变身冷却、能力限制、关卡难度 |
| 13 | 多关卡 | 2-3个不同主题关卡 |
| 14 | 开始/结束界面 | 主菜单、角色选择、结算画面 |

---

## 五、关键技术决策记录

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 引擎 | Unity 2022.3 LTS | 稳定性最佳，2D支持成熟，教程资源丰富 |
| 输入系统 | Unity New Input System + 自定义InputManager | 原生支持本地多人、设备热插拔；InputManager统一分发输入给两个玩家 |
| 玩家控制器架构 | 状态机模式 | 参考Momentum项目，便于扩展变身/伪装状态 |
| 伪装机制实现 | Sprite替换 + Collider切换 | 变身时替换SpriteRenderer的sprite，同时切换碰撞体形状匹配伪装对象 |
| 关卡构建 | Unity Tilemap | 参考zigurous教程，快速搭建2D关卡 |
| 相机方案 | 自定义CameraController（后期升级Cinemachine） | MVP阶段用简单脚本，已预装Cinemachine包 |
| 游戏管理 | 单例GameManager | 管理游戏状态、胜负判定、暂停、重启 |
| UI方案 | OnGUI后备 + UGUI Canvas | MVP阶段OnGUI快速显示，后期升级为Canvas UI |
| 美术风格 | 16-bit像素风 | 使用itch.io免费CC0素材（Pixel Adventure为主），后期可用本地AI工具生成补充素材 |
| 网络架构 | 暂不实现 | MVP阶段仅本地多人，后期扩展可考虑Unity Netcode |
| 项目结构 | 按功能模块分文件夹 | Assets/Scripts/{Player, Enemy, Core, UI, Camera} |

---

## 六、项目文件结构（当前实际状态）

```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── MarioController.cs      ✅ Mario移动/跳跃控制
│   │   └── PlayerHealth.cs          ✅ 生命值管理
│   ├── Enemy/
│   │   ├── TricksterController.cs   ✅ Trickster基础控制
│   │   ├── DisguiseSystem.cs        ✅ 伪装/变身系统
│   │   ├── SimpleEnemy.cs           ✅ 简单巡逻敌人
│   │   └── TricksterAbilities.cs    ⬜ 待开发（特殊能力）
│   ├── Core/
│   │   ├── GameManager.cs           ✅ 游戏状态/胜负判定
│   │   ├── InputManager.cs          ✅ 双人输入管理
│   │   ├── LevelManager.cs          ✅ 关卡管理
│   │   ├── GoalZone.cs              ✅ 终点触发器
│   │   ├── KillZone.cs              ✅ 死亡区域
│   │   ├── DamageDealer.cs          ✅ 通用伤害触发器
│   │   ├── Collectible.cs           ✅ 可收集物品
│   │   ├── Breakable.cs             ✅ 可破坏方块
│   │   └── MovingPlatform.cs        ✅ 移动平台
│   ├── Camera/
│   │   └── CameraController.cs      ✅ 相机跟随逻辑
│   └── UI/
│       └── GameUI.cs                ✅ 基础HUD
├── InputActions/
│   └── GameControls.inputactions    ✅ Input System配置
├── Scenes/
│   └── SampleScene.unity            （空白模板，需搭建）
├── Prefabs/                          ⬜ 待创建
├── Sprites/                          ⬜ 待导入素材
└── Tilemaps/                         ⬜ 待创建
```

---

## 七、操作键位参考

| 操作 | Player1 (Mario) | Player2 (Trickster) |
|------|-----------------|---------------------|
| 移动 | WASD | 方向键 |
| 跳跃 | Space | 右Ctrl |
| 伪装/取消伪装 | — | 右Shift |
| 切换伪装形态（下一个） | — | Enter |
| 切换伪装形态（上一个） | — | Backspace |
| 暂停 | ESC | ESC |
| 快速重启 | F5 | F5 |
| 手柄支持 | 第1个手柄 | 第2个手柄 |

---

## 八、新对话开场模板

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

> **说明**：详细的开场模板使用指南见「与 AI 高效协作开发工作流指南.md」的 1.1 章节。
