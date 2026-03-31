# MarioTrickster 项目进度总结

> 更新时间：2026-04-01 | 单一真相源：AI 新对话时自动读取本文件获取完整上下文

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

### 2.4 MVP 核心脚本（Sprint 1 进行中）

| 脚本 | 路径 | 状态 | 说明 |
|------|------|------|------|
| MarioController.cs | Assets/Scripts/Player/ | ✅ | 完整移动/跳跃（coyote time + buffered jump）/地面检测 |
| PlayerHealth.cs | Assets/Scripts/Player/ | ✅ | 通用生命值/无敌帧/受伤闪烁/死亡事件 |
| TricksterController.cs | Assets/Scripts/Enemy/ | ✅ | 伪装者基础控制（移动/跳跃/与DisguiseSystem协作） |
| DisguiseSystem.cs | Assets/Scripts/Enemy/ | ✅ | Sprite替换变身/冷却/场景融入/多形态切换 |
| InputManager.cs | Assets/Scripts/Core/ | ⚬ 待开发 | 本地双人输入管理 |
| GameManager.cs | Assets/Scripts/Core/ | ⚬ 待开发 | 游戏状态/胜负判定 |
| CameraController.cs | Assets/Scripts/Camera/ | ⚬ 待开发 | 相机跟随逻辑 |

---

## 三、已知未修复的 Bug

已完成的 4 个脚本尚未在 Unity 中实际测试，可能存在未发现的问题。待在 Unity 中挂载到 GameObject 并运行后确认。

---

## 四、下一步开发计划（MVP 优先级排序）

### 第一优先级：核心可玩（Sprint 1，预计 1-2 周）

| 序号 | 任务 | 参考来源 | 说明 |
|------|------|---------|------|
| 1 | Mario 玩家控制器 | Ultimate-2D-Controller + zigurous教程 | 移动、跳跃、coyote time、buffered jump、地面检测 |
| 2 | 本地双人输入系统 | InputSystem_Warriors + local-multiplayer-unity | Player1 WASD/方向键，Player2 另一组按键或手柄 |
| 3 | Trickster 基础控制器 | 自研 | 基础移动 + 变身按键触发 |
| 4 | 伪装/变身核心机制 | Among-Us-Imposter + PropHunt概念 | 切换Sprite为障碍物/怪物外观，静止时完全融入场景 |
| 5 | 基础关卡Tilemap | zigurous教程 + Pixel Adventure素材 | 一个可通关的测试关卡 |
| 6 | 胜负判定 | 自研 | Mario到达终点=Mario胜，Mario死亡=Trickster胜 |

### 第二优先级：游戏体验（Sprint 2，预计 1-2 周）

| 序号 | 任务 | 说明 |
|------|------|------|
| 7 | 相机系统 | 跟随Mario，Trickster在视野外有限制/提示 |
| 8 | Trickster特殊能力 | 放置假障碍物、触发陷阱、召唤AI怪物 |
| 9 | 基础UI | 生命值、计时器、角色提示 |
| 10 | 音效集成 | 跳跃/死亡/变身/胜利音效 |

### 第三优先级：打磨（Sprint 3）

| 序号 | 任务 | 说明 |
|------|------|------|
| 11 | 平衡性调整 | 变身冷却、能力限制、关卡难度 |
| 12 | 多关卡 | 2-3个不同主题关卡 |
| 13 | 开始/结束界面 | 主菜单、角色选择、结算画面 |

---

## 五、关键技术决策记录

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 引擎 | Unity 2022.3 LTS | 稳定性最佳，2D支持成熟，教程资源丰富 |
| 输入系统 | Unity New Input System | 原生支持本地多人、设备热插拔、按键重绑定 |
| 玩家控制器架构 | 状态机模式 | 参考Momentum项目，便于扩展变身/伪装状态 |
| 伪装机制实现 | Sprite替换 + Collider切换 | 变身时替换SpriteRenderer的sprite，同时切换碰撞体形状匹配伪装对象 |
| 关卡构建 | Unity Tilemap | 参考zigurous教程，快速搭建2D关卡 |
| 相机方案 | Cinemachine跟随Mario | 相机锁定Mario，Trickster需在相机视野范围内活动 |
| 美术风格 | 16-bit像素风 | 使用itch.io免费CC0素材（Pixel Adventure为主），后期可用本地AI工具生成补充素材 |
| 网络架构 | 暂不实现 | MVP阶段仅本地多人，后期扩展可考虑Unity Netcode |
| 项目结构 | 按功能模块分文件夹 | Assets/Scripts/{Player, Enemy, Core, UI, Camera} |

---

## 六、项目文件结构规划

```
Assets/
├── Scripts/
│   ├── Player/
│   │   ├── MarioController.cs      # Mario移动/跳跃控制
│   │   └── PlayerHealth.cs          # 生命值管理
│   ├── Enemy/
│   │   ├── TricksterController.cs   # Trickster基础控制
│   │   ├── DisguiseSystem.cs        # 伪装/变身系统
│   │   └── TricksterAbilities.cs    # 特殊能力（陷阱/召唤）
│   ├── Core/
│   │   ├── GameManager.cs           # 游戏状态/胜负判定
│   │   ├── InputManager.cs          # 双人输入管理
│   │   └── LevelManager.cs          # 关卡加载/重置
│   ├── Camera/
│   │   └── CameraController.cs      # 相机跟随逻辑
│   └── UI/
│       └── GameUI.cs                # 基础HUD
├── Prefabs/
│   ├── Mario.prefab
│   ├── Trickster.prefab
│   └── DisguiseObjects/             # 可伪装的对象预制体
├── Sprites/                          # 从itch.io下载的素材
├── Tilemaps/                         # 关卡地块
├── Scenes/
│   ├── MainMenu.unity
│   └── Level_01.unity
└── InputActions/
    └── GameControls.inputactions     # Input System配置
```

---

## 七、新对话开场模板

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
