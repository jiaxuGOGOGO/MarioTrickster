# MarioTrickster

> **非对称对抗平台跳跃游戏 (Asymmetric Multiplayer Platformer)**
> 
> 一名玩家扮演闯关者（类似马里奥）克服障碍到达终点；另一名玩家扮演捣蛋者，伪装成关卡中的障碍物、地形或怪物阻止闯关者。

---

## 📚 核心协作文档导航 (AI Collaboration Docs)

| 🎯 你的目标 | 📄 应该看哪个文档？ | 🤖 说明 |
|:---|:---|:---|
| **开启新对话 / 提交测试反馈** | 👉 [**SESSION_TRACKER.md**](./SESSION_TRACKER.md) | **每次对话必读入口** |
| **新 AI 接管本项目的唯一协议** | 👉 [**docs/AI_TAKEOVER_PROTOCOL.md**](./docs/AI_TAKEOVER_PROTOCOL.md) | **极简接管协议 + 通用续接模板** |
| **纵览全局：设计规划 vs 实现进度** | 👉 [**MASTER_TRACKER.md**](./MASTER_TRACKER.md) | AI 自动同步更新 |
| 查阅所有历史Bug、功能清单、文件结构 | 👉 [**MarioTrickster_Progress_Summary.md**](./MarioTrickster_Progress_Summary.md) | AI 按需深度读取 |
| **策划怎么高效做关卡、换素材、接机制？** | 👉 [**docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md**](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) | **功能介绍 + 使用教程** |
| 怎么在 Unity 里测试？键位是什么？ | 👉 [**MarioTrickster_Testing_Guide.md**](./MarioTrickster_Testing_Guide.md) | 用户测试手册 |
| Git报错了？怎么提问最省积分？ | 👉 [**AI_WORKFLOW.md**](./AI_WORKFLOW.md) | 用户工作流指南 |
| 游戏设计初衷、平衡性、美术风格 | 👉 [**GAME_DESIGN.md**](./GAME_DESIGN.md) | 项目初期/迷失时查阅 |

---

## ⚡ 新对话极速续接

换号 / 换窗口后直接续上，只需复制下面的模板发给 AI：

```text
【系统最高权限指令：MarioTrickster 极简接管协议】

主代码库：https://github.com/jiaxuGOGOGO/MarioTrickster
美术仓库：https://github.com/jiaxuGOGOGO/MarioTrickster-Art
Token：[按需填写，如需推代码则填]

作为接手本项目的 AI，你必须严格遵守以下"隐形后台"原则：

1. 强制静默读档（构建你的大脑）：
   - 必读 `SESSION_TRACKER.md`（掌握当前状态、防坑警告和 135 个测试安全网）。
   - 必读 `docs/AI_HANDOFF_PROJECT_STATUS_2026-04-12.md`（理解关卡主线与美术支线的关系，这是本项目的最高认知指南）。
   - 涉及到关卡时，自查 `LevelStudio_DesignGuide.md`；涉及到美术出图时，自查 `prompts/PROMPT_RECIPES.md`。

2. 绝对禁止"复杂度溢出"（沟通铁律）：
   - 项目过去沉淀的"双轨架构"、"知识抽屉"、"四区隔离"、"ASCII字典"等上百条复杂规则，是你必须在【后台默默执行】的系统约束。
   - 绝不允许向我（用户）抛出"你要走哪个菜单"、"请你补充规则参数"这种系统黑话，严禁让我填表。
   - 我只会用【大白话】向你提需求。你要自己在后台把我的白话补全为符合系统规矩的参数、图纸或代码。

3. 零摩擦执行与落库：
   - 遇到规则冲突，你自己按项目历史优先级仲裁（如：物理防滑步 > 空间透视 > 光影）。
   - 产出成果后，如果形成可复用结论，你自己默默更新对应文档并 Push。
   - 汇报时，只需用一句大白话告诉我结果，绝对禁止展示冗长的推导过程。

我本次的大白话需求是：
【在这里填入你想干嘛】
```

| 续接规则 | 要求 |
|---|---|
| **一般续接是否需要 token** | 不需要；补"大白话需求"即可 |
| **换号 / 换窗口是否建议带 token** | 建议带；AI 读档后若要推送不会中途停下追问 |
| **完成判定** | 未落库不算完成；需要跨对话稳定续接时，未 push 不算真正存档 |

---

## 🏗️ 仓库架构

| 仓库 | 地址 | 职责 |
|------|------|------|
| **主仓库** (Game Project) | [MarioTrickster](https://github.com/jiaxuGOGOGO/MarioTrickster) | 游戏逻辑代码、关卡设计、核心配置 |
| **美术仓库** (Art Project) | [MarioTrickster-Art](https://github.com/jiaxuGOGOGO/MarioTrickster-Art) | 美术源文件、Sprite 导出、参考图、美术管线文档 |

美术仓库通过 Git Submodule 挂载在主仓库的 `Assets/MarioTrickster-Art/` 目录下。

---

## 🚀 快速启动

**如果你是人类开发者：**
1. 克隆本仓库：`git clone --recurse-submodules https://github.com/jiaxuGOGOGO/MarioTrickster.git`
2. 使用 Unity 2022.3 LTS 打开项目
3. 阅读 [测试指南](./MarioTrickster_Testing_Guide.md) 了解如何一键生成测试场景

**如果你是 AI 助手：**
1. 先读 [SESSION_TRACKER.md](./SESSION_TRACKER.md)，再读 [极简接管协议](./docs/AI_TAKEOVER_PROTOCOL.md)
2. 用户说大白话，你在后台默默执行所有系统规则
3. 推送前必须执行 `SESSION_TRACKER.md` §0 中的日常必做三处更新
