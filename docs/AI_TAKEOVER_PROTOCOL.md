# AI Takeover Protocol

> **系统最高权限指令**：所有接手 MarioTrickster 的 AI 必须先在后台读档、判断任务类型、自动补齐执行参数，并在完成后把可复用结论落库推送。不要把项目内部复杂规则抛给用户。

---

## 1. 仓库信息

| 项目 | 值 |
| --- | --- |
| 主代码库 | `https://github.com/jiaxuGOGOGO/MarioTrickster` |
| 美术仓库 | `https://github.com/jiaxuGOGOGO/MarioTrickster-Art` |
| 美术挂载点 | `Assets/MarioTrickster-Art`（Git Submodule） |
| 默认分支 | `master` |

---

## 2. 强制静默读档

AI 接手后必须先读以下文件，再开始执行。读档动作在后台完成，不需要用户确认，不需要让用户选择路线。

| 必读顺序 | 文件 | 用途 |
| --- | --- | --- |
| 1 | `SESSION_TRACKER.md` | 当前状态、防坑警告、测试安全网、待办队列和本次落库位置。 |
| 2 | `docs/AI_HANDOFF_PROJECT_STATUS_2026-04-12.md` | 项目的最高认知指南：关卡主线优先，美术支线服务玩法验证。 |
| 3 | `README.md` | 面向人的清晰入口，确认用户和 AI 应看哪些文档。 |
| 4 | `docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md` | 日常关卡、换素材、批量素材整理和机制扩展的主工作流。 |

涉及具体方向时再补读专项文档。

| 任务类型 | 补读文件 |
| --- | --- |
| 关卡、ASCII、Level Studio | `LevelStudio_DesignGuide.md` |
| 素材导入、换皮、Sprite Sheet、角色状态动画 | `docs/ASSET_IMPORT_PIPELINE_GUIDE.md` |
| 美术出图、提示词、风格一致性 | `Assets/MarioTrickster-Art/prompts/PROMPT_RECIPES.md` |
| Git 操作、pull/push、代理、用户本地报错反馈 | `AI_WORKFLOW.md` |

---

## 3. 后台执行原则

| 原则 | 执行要求 |
| --- | --- |
| 复杂规则后台化 | 双轨架构、知识抽屉、四区隔离、ASCII 字典、测试矩阵等规则由 AI 自己执行，不向用户解释成负担。 |
| 大白话输入 | 用户只需要描述想要什么，AI 自行转译成代码、参数、图纸、命名规则或测试项。 |
| 关卡主线优先 | 冲突时优先保证玩法、物理、碰撞、可测试性，再处理空间透视、光影与美术精修。 |
| 小步落地 | 先补能跑通的最小闭环，再在已有架构上扩展，避免一次性引入过量抽象。 |
| 可复用即落库 | 形成稳定结论时更新对应文档、Tracker、测试说明，并提交推送。 |
| 汇报极简 | 面向用户只说结果，不展示冗长推导。 |

---

## 4. 当前文档地图

| 你要解决的问题 | 看哪里 |
| --- | --- |
| “项目现在到哪了，下一步做什么？” | `SESSION_TRACKER.md` |
| “新 AI 怎么接手？” | 本文与 `SESSION_TRACKER.md` |
| “我作为用户从哪里开始？” | `README.md` |
| “怎么做关卡、换素材、让 AI 接机制？” | `docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md` |
| “角色 idle/run/jump/fall 动画怎么命名并自动挂载？” | `docs/ASSET_IMPORT_PIPELINE_GUIDE.md` |
| “Git 拉取、冲突、代理怎么处理？” | `AI_WORKFLOW.md` |

---

## 5. 通用续接模板

```text
【系统最高权限指令：MarioTrickster 极简接管协议】

主代码库：https://github.com/jiaxuGOGOGO/MarioTrickster
美术仓库：https://github.com/jiaxuGOGOGO/MarioTrickster-Art
Token：[按需填写，如需推代码则填]

作为接手本项目的 AI，你必须严格遵守以下“隐形后台”原则：

1. 强制静默读档：必读 SESSION_TRACKER.md 与 docs/AI_HANDOFF_PROJECT_STATUS_2026-04-12.md；涉及关卡读 LevelStudio_DesignGuide.md；涉及美术出图读 Assets/MarioTrickster-Art/prompts/PROMPT_RECIPES.md；涉及素材导入或状态动画读 docs/ASSET_IMPORT_PIPELINE_GUIDE.md。

2. 禁止复杂度溢出：项目内部架构、测试矩阵、ASCII 字典、文档联动规则全部后台执行，不要让我填系统参数或选择技术菜单。

3. 零摩擦执行与落库：遇到规则冲突自行按项目历史优先级仲裁；形成可复用结论就更新文档并推送；最终只用一句大白话告诉我结果。

我本次的大白话需求是：
【在这里写你想做什么】

（可选补充：如果涉及新机制与动画关联，请附加以下提示词）
【我已经用 Apply Art 导入了带有 [swim/wallslide/roll等] 关键词的素材，SpriteStateAnimator 里已经有这个状态帧组了。现在请帮我写一段新机制代码（比如碰到水体触发游泳），并在代码里用 `SetStateByTag("状态名")` 和 `ReleaseStateOverride()` 把它和动画关联起来。】
```
