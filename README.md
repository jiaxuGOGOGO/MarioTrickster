# MarioTrickster

> **一句话定位**：MarioTrickster 是一款非对称对抗平台跳跃游戏，一名玩家扮演闯关者通过关卡，另一名玩家扮演捣蛋者伪装为障碍、地形或怪物来阻止闯关者。[1] [2]

本文档是仓库首页，只负责告诉你“现在应该从哪里开始”。详细进度、AI 接管协议、关卡生产教程、素材导入教程和测试手册分别放在对应的权威文档里，避免同一套规则散落在多个长文档中反复维护。[1] [3]

---

## 1. 先看哪一个文档

| 你的目标 | 权威入口 | 说明 |
| --- | --- | --- |
| 继续开发、让 AI 接手、提交测试反馈 | [SESSION_TRACKER.md](./SESSION_TRACKER.md) | 当前进度、待办队列、防坑规则与推送前更新点都以这里为准。 |
| 新 AI 第一次接管项目 | [docs/AI_TAKEOVER_PROTOCOL.md](./docs/AI_TAKEOVER_PROTOCOL.md) | 极简接管协议，保留“后台读档、少打扰、自动落库”的协作方式。 |
| 做关卡、换素材、接机制 | [docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) | 面向策划的一页式工作流，优先从这里开始日常制作。 |
| 导入商业素材、Sprite Sheet 或角色帧 | [docs/ASSET_IMPORT_PIPELINE_GUIDE.md](./docs/ASSET_IMPORT_PIPELINE_GUIDE.md) | 记录 Asset Import Pipeline、Apply Art、SEF 和 idle/run/jump/fall 状态动画命名规则。 |
| 在 Unity 里验证功能和导出测试报告 | [MarioTrickster_Testing_Guide.md](./MarioTrickster_Testing_Guide.md) | 手动测试、自动测试、回归报告入口都在这里。 |
| 查看设计矩阵与长期状态 | [MASTER_TRACKER.md](./MASTER_TRACKER.md) | 设计规划与实现状态的对照表。 |
| 查历史细节和旧 Bug | [MarioTrickster_Progress_Summary.md](./MarioTrickster_Progress_Summary.md) | 深档案，只有需要追溯长期历史时再读。 |
| 查 Git 常用命令 | [AI_WORKFLOW.md](./AI_WORKFLOW.md) | 已收束为短附录，不再作为协作主入口。 |

> **协作原则**：人类用户只需要用大白话说目标；AI 必须在后台读取必要文档、处理架构约束、更新 `SESSION_TRACKER.md` 并推送，不把内部术语和参数表转嫁给用户。[1] [3]

---

## 2. 当前最短使用路径

| 场景 | 你做什么 | 系统自动承接什么 |
| --- | --- | --- |
| 做一段新关卡 | 打开 Unity 2022.3 LTS，用 `Ctrl+T` 进入 Test Console，再用文本矩阵生成白盒关卡。 | 生成 Root/Visual 分离对象、补齐可玩环境，并保留后续换皮入口。[2] |
| 给白盒换商业素材 | 选中 Root 或 Visual，使用 Apply Art，拖入素材后应用。 | 自动归一到行为 Root，只替换视觉、动画和材质，不破坏碰撞与行为。[2] [4] |
| 导入角色状态动画 | 把帧命名为 `hero_idle_00`、`hero_run_00`、`hero_jump_00`、`hero_fall_00` 这类格式。 | 分类器识别两组以上状态后自动挂 `SpriteStateAnimator`，按角色运动状态切换；只有一组状态时回退为普通循环动画。[4] |
| 导入普通循环动画 | 提供多帧 Sprite Sheet 或散帧，但不要使用多组角色状态命名。 | 自动挂 `SpriteFrameAnimator` 循环播放。[4] |
| 让 AI 增加新机制 | 直接说“我想要一个会怎样互动的机关/敌人/能力”。 | AI 按项目规则补代码、字典、测试和文档，完成后自动提交推送。[1] [3] |

推荐顺序是先把白盒玩法跑通，再接入美术素材。项目的核心目标仍是关卡节奏、Trickster 伏击点和机制互动，美术管线是服务于已验证玩法的支线能力。[2]

---

## 3. 仓库结构

| 仓库 | 地址 | 职责 |
| --- | --- | --- |
| 主仓库 | [MarioTrickster](https://github.com/jiaxuGOGOGO/MarioTrickster) | 游戏逻辑代码、关卡设计、核心配置和主文档。 |
| 美术仓库 | [MarioTrickster-Art](https://github.com/jiaxuGOGOGO/MarioTrickster-Art) | 美术源文件、Sprite 导出、参考图和美术资产资料。 |

美术仓库通过 Git Submodule 挂载在主仓库的 `Assets/MarioTrickster-Art/` 目录下。首次克隆建议使用以下命令，避免遗漏子模块资源。[1]

```bash
git clone --recurse-submodules https://github.com/jiaxuGOGOGO/MarioTrickster.git
```

---

## 4. 开发者快速启动

| 步骤 | 动作 |
| --- | --- |
| 1 | 使用 Unity 2022.3 LTS 打开项目根目录。 |
| 2 | 先读 [SESSION_TRACKER.md](./SESSION_TRACKER.md)，确认当前 Session、阻塞和待办队列。 |
| 3 | 如需做关卡或换素材，读 [策划快速指南](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) 与 [素材导入指南](./docs/ASSET_IMPORT_PIPELINE_GUIDE.md)。 |
| 4 | 修改后按 [测试指南](./MarioTrickster_Testing_Guide.md) 导出报告；推送前更新 `SESSION_TRACKER.md` 的状态总览、回归标记和待办队列。 |

---

## References

[1]: ./SESSION_TRACKER.md "MarioTrickster Session Tracker"
[2]: ./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md "Planner Fast Level Production Guide"
[3]: ./docs/AI_TAKEOVER_PROTOCOL.md "AI Takeover Protocol"
[4]: ./docs/ASSET_IMPORT_PIPELINE_GUIDE.md "Asset Import Pipeline Guide"
