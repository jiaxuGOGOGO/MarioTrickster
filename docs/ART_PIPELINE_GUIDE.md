# MarioTrickster 美术资产生产管线指南 (K2B-OS GitOps Edition)

> **AI 驱动的 2D 资产全自动流线 (最终完美版)**
> 本文档旨在为 `MarioTrickster` 项目提供一套基于 GitOps 理念、AI 驱动的美术资产生产与管理指南。它整合了项目核心的美术规范 (`ART_BIBLE`)、已验证的提示词配方 (`PROMPT_RECIPES`)，并详细阐述了日常工作流、GitOps 核心指令以及 Unity 自动化工具的使用，确保美术资产生产的效率、一致性与可维护性。

---

## 🏛️ 第零区与第一区：物理基建与画风规范

> **⚠️ 核心规范已抽离至 `docs/ART_BIBLE.md`**
> 
> 为了遵循 DRY (Don't Repeat Yourself) 原则，关于项目的物理度量（PPU=32、视碰分离）、画风目标（纯色背景防毛刺）以及各类资产的重心/适配模式（Tiled/Scaled/SlicedNineSlice），请**统一以 `docs/ART_BIBLE.md` 为唯一真理源**。
> 
> 本指南将专注于**工作流执行**与**工具链使用**。

---

## 🛠️ 第二区：AI 出图与管线红线 (PROMPT_RECIPES 核心)

### 2.1 提示词万能公式 (The Master Formula)
所有用于生成 `MarioTrickster` 美术资产的提示词，必须包含以下“防崩坏”后缀，以确保生成结果符合项目规范并易于后期处理：

> `[Subject], 2D side-scrolling game sprite, pixel art, flat color background, isolated, full body, consistent lighting, high contrast, sharp edges`
> *(注：如果使用 Midjourney，请在末尾加上 `--no shading gradients, --no blur`；如果使用 ComfyUI，请将这些词放入负向提示词节点。)*

- `[Subject]`: 描述你想要生成的具体内容，例如 `Mario character`, `Ghostly trickster`, `stone block` 等。
- `2D side-scrolling game sprite`: 明确指定游戏类型和资产形式。
- `pixel art`: 强制像素艺术风格。
- `flat color background, isolated`: 强制纯色背景且主体独立，便于抠图。
- `full body`: 确保生成完整角色或物体。
- `consistent lighting, high contrast, sharp edges`: 保证视觉质量和风格一致性。
- `--no shading gradients, --no blur`: 负面提示词，避免渐变和模糊，进一步强化像素艺术的清晰度。

### 2.2 动作帧数标准
为确保动画流畅性和文件大小平衡，以下是推荐的动作帧数标准：

| 动作类型 | 推荐帧数 | 说明 |
| :--- | :--- | :--- |
| **Idle (待机)** | 4-6 帧 | 循环动画，表现角色静止时的微小动态。|
| **Run (跑步)** | 8 帧 | 关键动作，必须仔细调整以符合 PPU 步幅，防止角色在移动时出现“滑步”感。|
| **Jump (跳跃)** | 3 阶段 | 包含起跳、空中最高点、下落三个关键阶段的动画。|
| **Attack (攻击)** | 5-8 帧 | 包含前摇、攻击命中、收招等阶段，确保打击感。|

### 2.3 自动化切片红线
- **禁止手动切片**: 所有美术资产的切片操作必须通过 Unity 编辑器中的 `MarioTrickster/Art Pipeline/一键工业化切图 (强制执行 ArtBible 规范)` 菜单项，调用 `AI_SpriteSlicer.cs` 脚本完成。
- **重心死锁**: `AI_SpriteSlicer` 脚本会根据资产类型强制设置 Sprite 的 Pivot。角色/敌人等实体资产的 Pivot 强制设为 `Bottom Center (0.5, 0)`，特效类资产设为 `Center (0.5, 0.5)`。**严禁手动修改此逻辑**，否则会导致动画滑步和物理表现异常。
- **像素完美**: `AI_SpriteSlicer` 脚本会强制将导入的 Sprite 的 Filter Mode 设为 `Point (no filter)`，确保像素艺术的清晰度，避免模糊。

### 2.4 AI 工具最佳实践 (基于社区经验)

> **⚠️ 详细参数已抽离至 `prompts/PROMPT_RECIPES.md`**
> 
> 关于 Midjourney 的具体参数（`--ar`, `--s`, `--c`, `--tile`）以及 ComfyUI 的节点配置（模型、LoRA、采样器、后处理），请直接参考配方库文档。那里记录了针对不同实体类型的具体应用示例。

---

## 🔄 第三区：GitOps 驱动的日常工作流与维护

本项目的核心理念是 **Docs as Code (规则即代码)** 与 **GitOps (基于 Git 的自动化运维)**。GitHub 仓库是所有项目知识的唯一真理源 (Single Source of Truth)，而 AI (Manus) 扮演着“全职技术主美 (TA)”的角色，负责自动化维护和执行规范。

### 3.1 日常工作流
1. **清晨同步**: 每天开始工作前，务必在本地 Git 终端执行 `git pull`，确保获取最新的 `ART_BIBLE.md` 和 `PROMPT_RECIPES.md`。Manus 可能会在夜间更新这些文档。
2. **美术资产生成**: 
   - 根据 `ART_BIBLE.md` 的规范和 `PROMPT_RECIPES.md` 中的示例，在 ComfyUI、Midjourney 或其他 AI 绘画工具中生成美术素材。
   - 严格遵守提示词万能公式，并根据具体资产调整 `[Subject]` 部分。
   - 确保生成图片为纯色背景，便于抠图。
3. **资产导入与切片**: 
   - 将生成的序列帧长图导入 Unity 项目。
   - 使用 Unity 编辑器菜单 `MarioTrickster/Art Pipeline/一键工业化切图 (强制执行 ArtBible 规范)` 运行 `AI_SpriteSlicer` 脚本进行自动化切片。
   - 脚本将强制执行 PPU、Filter Mode、Pivot 等规范，确保资产符合项目要求。
4. **提交与存档**: 
   - 成功生成并切片的美术资产，其对应的提示词、帧数、重心等信息必须追加到 `prompts/PROMPT_RECIPES.md` 中。
   - 提交 Git Commit，Commit Message 遵循 `feat(recipes): add [Asset Name] blueprint` 格式。
   - 更新 `SESSION_TRACKER.md` 中的状态和待办。
   - `git push` 将更改推送到远程仓库。

### 3.2 维护与一致性保障
- **画风一致性检查**: 每次生成新素材后，必须与现有素材放在同一场景中对比。检查色调、对比度、像素颗粒大小是否一致。
- **素材命名规范**: 遵循 `[EntityName]_[Action]_[Direction]` 的格式，例如 `Mario_Run_Right`。
- **版本管理**: 所有素材源文件（如 Aseprite 的 `.ase` 文件）和导出的 PNG 文件都必须纳入 Git 版本控制。

### 3.3 GitOps 核心指令 (AI 与用户协作模式)

#### 3.3.1 创世 Commit (仅在项目初始化时使用)
**核心目的**: 让 AI 帮你搭建项目知识库的基础结构，并写入第一版核心规则。

> **💬 [附带仓库授权/链接 + 上传第 1 本 PDF，发送以下指令]：**
> "Manus，现在正式启动项目知识库的『创世』阶段。我的 GitHub 仓库地址是 `[填入链接]`。 我上传了第一本核心教程 PDF。你的唯一身份是项目的首席技术美术 (TA)。
> 
> **【项目基准红线】**：Unity 2D 视碰分离架构；PPU=32；画风 `[填入你的目标画风]`；强制纯色背景出图防抠图毛刺。
> 
> **请调用你的终端（Bash/Python）执行以下 Git 操作：**
> 
> 1. 读取并蒸馏 PDF，提取绝对不可违背的比例、体块、动作帧数规则。
> 2. Clone 我的仓库，在本地初始化创建 `docs/ART_BIBLE.md` 和 `prompts/PROMPT_RECIPES.md`。
> 3. 将提取的规矩按结构化 Markdown 写入 `ART_BIBLE.md`。
> 4. 执行 Commit 并 Push 到 `master` 分支。Commit Msg 规范：`feat(bible): init v1.0 from [书名]`。
> 5. 成功推送后，向我汇报你初始化的 3 条最核心规则。"

#### 3.3.2 防污染的 PR 合并 (喂新书时使用)
**核心目的**: 在引入新知识时，防止 AI 悄悄修改底层规则。通过 Pull Request (PR) 机制，由用户审批后才能合入主干，确保核心规范的稳定性。

> **💬 [上传新书 PDF，发送以下指令]：**
> "Manus，进入知识库的『增量合并 (Merge)』阶段。我上传了一本新教程 PDF。
> 
> **请严格执行以下 GitOps 流程，绝不允许直接推送到 main 分支：**
> 
> 1. **Fetch**：拉取 `master` 分支最新的 `docs/ART_BIBLE.md`。
> 2. **Diff & Resolve**：对比新书知识与现有宪法。**【绝对红线】**：如果发生规则冲突，必须以『最适合 Unity 横版、最防滑步』为唯一准则强制抉择，绝不缝合！
> 3. **Branch**：基于 `master` 新建一个分支，例如 `feature/merge-[书名]`。
> 4. **Commit & Push**：将吸收新知识后的完整 Markdown 推送到该新分支，并在文档修改处打上 `> [TA 合并注：...]` 解释理由。
> 5. **Pull Request**：调用 GitHub API 创建一个 PR 到 master 分支。在 PR 描述中清晰列出：**“我废弃了新书里的哪些规则？我修改了宪法里的哪些旧红线？”**
> 
> 提交 PR 后把链接发我，我去 Code Review。"

#### 3.3.3 带存档的工单派发 (生产具体图纸时使用)
**核心目的**: 规范化美术资产的生产流程，确保每次出图都遵循 `ART_BIBLE`，并将成功的配方永久存档。

> **💬 [不需要传书，直接发送以下指令]：**
> "Manus，准备派发工单。我要立刻生产一组具体资产：**[填写：例如 Trickster 变身特效 / 主角受击动画]**。
> 
> **请执行流水线操作：**
> 
> 1. **Fetch**：拉取 `master` 分支最新的 `docs/ART_BIBLE.md`。
> 2. **生成蓝图**：基于宪法，输出该动作的总帧数、极值帧切分、建议喂给 ControlNet 的最优模具（如 OpenPose/Canny）、以及带纯色去底和防崩坏命令的英文万能 Prompt 公式。
> 3. **Push 存档**：把这份蓝图直接 Append (追加) 写入 `master` 分支的 `prompts/PROMPT_RECIPES.md`。
> 4. **Commit Msg**：`feat(recipes): add prompt blueprint for [资产名称]`。
> 
> 推送完成后，直接在对话框里把写好的中英文对照 Prompt 发给我，我去本地出图。"

---

## ⚙️ 第四区：项目改造建议 (Unity 自动化脚本)

为了更好地支持上述 GitOps 美术资产生产管线，项目已进行了以下改造：

### 4.1 `AI_SpriteSlicer.cs` (自动化切片母机)

该脚本已添加到 `Assets/Scripts/Editor/AI_SpriteSlicer.cs`，并通过 Unity 编辑器菜单 `MarioTrickster/Art Pipeline/一键工业化切图 (强制执行 ArtBible 规范)` 暴露。它强制执行 `ART_BIBLE` 中定义的所有切片规范，包括 PPU、Filter Mode、Sprite Type、Pivot 等，极大地简化了美术导入流程并确保了规范一致性。

**建议**: 
- 在导入任何新的美术序列帧长图后，都应使用此工具进行切片，而非 Unity 默认的切片功能。
- 熟悉其参数设置（如 `目标帧数 (列)` 和 `资产物理类型`），以适应不同资产的需求。

### 4.2 `docs/ART_BIBLE.md` 与 `prompts/PROMPT_RECIPES.md`

这两个 Markdown 文件已创建并推送到仓库的 `docs/` 和 `prompts/` 目录下。它们是项目美术规范和提示词配方的核心文档。

**建议**: 
- 定期查阅 `ART_BIBLE.md`，确保对项目美术规范有清晰的理解。
- 在生成新资产前，优先查阅 `PROMPT_RECIPES.md`，看是否有类似的已验证配方可供参考或修改。
- 每次成功生成新资产并验证其在游戏中的表现后，务必将对应的提示词和关键参数追加到 `PROMPT_RECIPES.md` 中，形成项目知识积累。

---
*Last Updated: 2026-04-09 by Manus TA*
