# MarioTrickster

> **非对称对抗平台跳跃游戏 (Asymmetric Multiplayer Platformer)**
> 
> 一名玩家扮演闯关者（类似马里奥）克服障碍到达终点；另一名玩家扮演捣蛋者，伪装成关卡中的障碍物、地形或怪物阻止闯关者。

---

## 📚 核心协作文档导航 (AI Collaboration Docs)

本项目采用 **6 文档体系 + 联动更新矩阵**，每条信息只有一个“真相源”文档，其他文档只引用不重复。详见 `SESSION_TRACKER.md` §0.3 联动更新矩阵。

| 🎯 你的目标 | 📄 应该看哪个文档？ | 🤖 AI 会看吗？ |
|:---|:---|:---|
| **开启新对话 / 提交测试反馈** | 👉 [**SESSION_TRACKER.md**](./SESSION_TRACKER.md) | **每次对话必读入口** |
| **以后所有更新 / 升级 / 优化对话该怎么开局** | 👉 [**docs/STANDARD_CONVERSATION_PROTOCOL.md**](./docs/STANDARD_CONVERSATION_PROTOCOL.md) | **项目级协作宪章** |
| **直接复制哪一条标准提示词模板** | 👉 [**prompts/STANDARD_PROJECT_PROMPTS.md**](./prompts/STANDARD_PROJECT_PROMPTS.md) | **新对话默认模板库** |
| **纵览全局：设计规划 vs 实现进度** | 👉 [**MASTER_TRACKER.md**](./MASTER_TRACKER.md) | AI 自动同步更新 |
| 查阅所有历史Bug、功能清单、文件结构 | 👉 [**MarioTrickster_Progress_Summary.md**](./MarioTrickster_Progress_Summary.md) | AI 按需深度读取 |
| 怎么在 Unity 里测试？键位是什么？ | 👉 [**MarioTrickster_Testing_Guide.md**](./MarioTrickster_Testing_Guide.md) | 用户测试手册 |
| Git报错了？怎么提问最省积分？ | 👉 [**AI_WORKFLOW.md**](./AI_WORKFLOW.md) | 用户工作流指南 |
| 游戏设计初衷、平衡性、美术风格 | 👉 [**GAME_DESIGN.md**](./GAME_DESIGN.md) | 项目初期/迷失时查阅 |

---

## ⚡ 新对话极速续接与美术白话入口总表

如果你的目标是**换号 / 换窗口后直接续上，不想中途再补条件**，推荐先阅读 [`docs/STANDARD_CONVERSATION_PROTOCOL.md`](./docs/STANDARD_CONVERSATION_PROTOCOL.md)，再从 [`prompts/STANDARD_PROJECT_PROMPTS.md`](./prompts/STANDARD_PROJECT_PROMPTS.md) 里复制对应模板。对于绝大多数“更新 / 升级 / 优化项目”的新对话，**默认优先使用标准模板，而不是临时自由发挥**。

对于一般的继续开发 / 优化 / 修复场景，标准模板已经固化了仓库、路径、分支与读档入口，你通常只需要补 **本次任务** 与 **本次反馈 / 新变化**。但如果是**换号 / 换窗口后的新对话读档续接**，为了避免 AI 读档后在提交、推送或远程校验阶段被中途打断，推荐把**可用 token** 也在开场一并给出。

> **默认极速续接模板**（完整版模板库见 [`prompts/STANDARD_PROJECT_PROMPTS.md`](./prompts/STANDARD_PROJECT_PROMPTS.md)）
>
> ```text
> 我换号了，要继续 MarioTrickster 上次项目。
> token：<你的可用 token>
> 本次任务：[你的当前需求]
> 本次反馈 / 新变化：[如果你对上轮结果有反馈，或本地已有新改动，就写在这里]
> 请严格按 MarioTrickster 标准对话协议执行：先读取 `SESSION_TRACKER.md`，再按任务类型补读相关文档；先判断当前阶段、主阻塞、默认入口与任务边界，再给出分阶段计划；执行时不要无故发散；凡是形成可复用结论的内容都要落库，完成条件允许时再提交并推送。
> ```

| 续接 / 生产入口 | AI 后端默认识别 | 是否必须落库 | 默认落到哪 | 何时自动 push |
|---|---|---|---|---|
| **我想从零开始做这套美术资产，你带我走。** | 阶段判定 / 主路线选择 | **是** | `SESSION_TRACKER.md` | 一旦主路线或“下一步最缺项”明确 |
| **我上传了一本教程，帮我蒸馏进配方库并推送仓库。** | 喂书蒸馏 / 规则入库 | **是** | `prompts/PROMPT_RECIPES.md` + `SESSION_TRACKER.md` | 蒸馏规则完成入库并记录 recap 后 |
| **按这个参考风格，先在本地给我跑 30 张探索图，我拿去炼 LoRA。** | 风格探索 / 训练集积累 | **是** | `SESSION_TRACKER.md` | 出现可复用风格方向、锚点 Seed、负面禁区、筛图结论后 |
| **我要用 Civitai 练这个 LoRA，直接告诉我页面每一项怎么填。** | 在线训练参数填写 / 排障 | **是** | `SESSION_TRACKER.md` | 参数定版，或排障后得到可复用修正方案后 |
| **这个 LoRA 练完了，告诉我怎么在本地验证触发词、权重和污染。** | 本地验证 / 甜区测试 / 污染排查 | **是** | `SESSION_TRACKER.md` + `prompts/PROMPT_RECIPES.md` 顶部名录 | 测出推荐权重、污染症状、专属去污词后 |
| **我炼完 LoRA 了，文件名是 A，触发词是 B。帮我登记。** | LoRA 入库登记 | **是** | `prompts/PROMPT_RECIPES.md` 顶部名录 + `SESSION_TRACKER.md` | 文件名与触发词确认、资产卡生成后 |
| **做一组地刺的静态图。** | 正式量产派单 / 四区图纸生成 | **是** | `SESSION_TRACKER.md`，必要时并入 `prompts/PROMPT_RECIPES.md` 相关区域 | 四区图纸、关键参数、ControlNet 路线、锚点或禁区可复用后 |

> **按 MarioTrickster 当前阶段的默认起步口径**：总入口仍保留，但当前不建议再默认从“我想从零开始做这套美术资产，你带我走”重新开局。现在应优先直接从两个窄入口启动：其一是 `这个 LoRA 练完了，告诉我怎么在本地验证触发词、权重和污染。`，用于突破 `trickster_style` 的主阻塞；其二是 `先继续白盒关卡，不等最终美术。帮我按当前机关目标安排下一个教学段或挑战段。`，用于保证关卡主线持续前进。也就是说，**当前默认顺序是：先验证，再切片量产；同时白盒关卡继续推进。**

| 续接规则 | 现在的默认要求 |
|---|---|
| **一般续接是否默认需要 token** | **不需要**；标准模板下通常补“本次任务 + 本次反馈 / 新变化”即可 |
| **换号 / 换窗口的新对话读档是否建议带 token** | **建议带**；这样 AI 读档后若要提交、推送或远程校验，不会在中途停下再追问 |
| **提交 / 推送是否需要 token** | **通常需要可用认证**；若当前环境认证失效，就必须补 token |
| **完成判定** | **未落库不算完成；需要跨对话稳定续接时，未 push 不算真正存档** |

---

## 🔄 AI 协作工作流图解

```mermaid
graph TD
    A[你开启新对话] -->|发送开场模板| B(SESSION_TRACKER.md)
    B -->|AI 读取 §0 规范 + §1 状态 + §5 待办| C{需要更多上下文?}
    C -->|是| D(Progress_Summary.md)
    C -->|否| E[AI 编写代码]
    D -->|获取 Bug库 + 技术决策 + 文件树| E
    E --> F{修 Bug?}
    F -->|是| G[查 Testing_Guide §4 影响矩阵]
    F -->|否| H[直接开发]
    G --> H
    H -->|git push 前| I[查 SESSION_TRACKER §0.3 联动矩阵]
    I -->|按矩阵更新所有相关文档| J[AI 推送代码 + 文档]
    J -->|包含 MASTER_TRACKER 全局同步| K(Testing_Guide.md)
    K -->|你 pull 并测试| L[填写反馈模板]
    L -->|下次对话发给 AI| A
```

---

## 🚀 快速启动

**如果你是人类开发者：**
1. 克隆本仓库：`git clone https://github.com/jiaxuGOGOGO/MarioTrickster.git`
2. 使用 Unity 2022.3 LTS 打开项目
3. 阅读 [测试指南](./MarioTrickster_Testing_Guide.md) 了解如何一键生成测试场景

**如果你是 AI 助手：**
1. 请先读取 [SESSION_TRACKER.md](./SESSION_TRACKER.md)，再遵守 [标准对话协议](./docs/STANDARD_CONVERSATION_PROTOCOL.md)
2. 新对话优先从 [标准提示词模板库](./prompts/STANDARD_PROJECT_PROMPTS.md) 选择对应模板执行
3. 在积分接近警戒线时，务必优先更新文档并推送代码
4. 推送前必须执行 `SESSION_TRACKER.md` §0.3 联动更新矩阵，确保所有文档同步
