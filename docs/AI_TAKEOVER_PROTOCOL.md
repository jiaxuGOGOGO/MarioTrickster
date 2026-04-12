# MarioTrickster 极简接管协议

> **系统最高权限指令** — 所有接手本项目的 AI 必须严格遵守。

---

## 仓库信息

| 项目 | 值 |
|------|-----|
| 主代码库 | https://github.com/jiaxuGOGOGO/MarioTrickster |
| 美术仓库 | https://github.com/jiaxuGOGOGO/MarioTrickster-Art |
| 美术挂载点 | `Assets/MarioTrickster-Art`（Git Submodule） |

---

## 1. 强制静默读档（构建你的大脑）

- **必读** `SESSION_TRACKER.md`（掌握当前状态、防坑警告和 135 个测试安全网）。
- **必读** `docs/AI_HANDOFF_PROJECT_STATUS_2026-04-12.md`（理解关卡主线与美术支线的关系，这是本项目的最高认知指南）。
- 涉及到关卡时，自查 `LevelStudio_DesignGuide.md`；涉及到美术出图时，自查美术仓库中的 `prompts/PROMPT_RECIPES.md`（路径：`Assets/MarioTrickster-Art/prompts/PROMPT_RECIPES.md`）。

## 2. 绝对禁止"复杂度溢出"（沟通铁律）

- 项目过去沉淀的"双轨架构"、"知识抽屉"、"四区隔离"、"ASCII字典"等上百条复杂规则，是你必须在**后台默默执行**的系统约束。
- **绝不允许**向用户抛出"你要走哪个菜单"、"请你补充规则参数"这种系统黑话，严禁让用户填表。
- 用户只会用**大白话**提需求。你要自己在后台把大白话补全为符合系统规矩的参数、图纸或代码。

## 3. 零摩擦执行与落库

- 遇到规则冲突，你自己按项目历史优先级仲裁（如：物理防滑步 > 空间透视 > 光影）。
- 产出成果后，如果形成可复用结论，你自己默默更新对应文档并 Push。
- 汇报时，只需用一句大白话告诉用户结果，**绝对禁止展示冗长的推导过程**。

---

## 通用续接模板

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
【在这里填入你想干嘛，比如：我想用ASCII拼个带地刺的关卡 / 帮我按 trickster_style 出一组主角跑步的图 / 帮我排查一个Unity报错】
```
