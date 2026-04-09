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

### 3.1 日常工作流 (端到端闭环)

这套管线需要你（作为执行者）和 AI（作为技术主美）紧密配合。以下是生产一个新资产的完整闭环：

#### Step 1: 向 AI 发起需求 (指令 3)
* **你**：复制并发送 `指令 3`（见下文 3.4 节），告诉 AI 你要生产什么（例如："主角跑步"）。
* **AI**：会去查阅 `ART_BIBLE.md`，算好跑步需要几帧、极值帧在哪里，然后为你生成专属的中英文 Prompt 和参数建议，并追加到 `PROMPT_RECIPES.md` 存档。

#### Step 2: 本地出图与反馈循环
* **你**：拿着 AI 给的 Prompt 和参数，去本地的 ComfyUI 或 Midjourney 出图。记得喂入 `Reference_Anchor.png`（概念锚点）作为风格参考。
* **你**：如果出图不满意（比如：腿画歪了、颜色不对、没有纯色背景），**把问题或者有瑕疵的图发回给 AI**。
* **AI**：会根据你的反馈，帮你修改 Prompt、调整权重、或者提供 ControlNet 建议，直到你本地出图满意为止。

#### Step 3: 导入 Unity 与自动化处理
* **你**：把最终满意的图（去底后的 PNG）拖入 Unity 的 `Assets/Art/` 对应子目录（如 `Characters/`）。
* **Unity (自动)**：`TA_AssetValidator` 脚本会瞬间介入，自动把图片的 PPU 设为 32、Filter 设为 Point、关闭压缩。
* **你**：点击顶部菜单 `MarioTrickster/Art Pipeline/一键工业化切图`。
* **Unity (自动)**：`AI_SpriteSlicer` 脚本会自动帮你把图切成帧序列，并强制锁定正确的重心（Pivot）。

#### Step 4: 验证与最终存档
* **你**：把切好的帧拖进场景，看看跑起来滑不滑步。
* **你**：点击菜单 `MarioTrickster/Art Pipeline/一键合规巡检`。如果全绿通过，说明资产完全合法。
* **你**：告诉 AI："资产测试通过，请更新 SESSION_TRACKER 并 Push 代码"。
* **AI**：完成最终的 Git 提交，这个资产就正式进入你的游戏了！

### 3.2 维护与一致性保障
- **画风一致性检查**: 每次生成新素材后，必须与现有素材放在同一场景中对比。检查色调、对比度、像素颗粒大小是否一致。
- **素材命名规范**: 遵循 `[EntityName]_[Action]_[Direction]` 的格式，例如 `Mario_Run_Right`。
- **版本管理**: 所有素材源文件（如 Aseprite 的 `.ase` 文件）和导出的 PNG 文件都必须纳入 Git 版本控制。

### 3.3 商业价值远景 (Gumroad 资产包)
当你把这个游戏做完时，你 GitHub 仓库里的这套被 AI 迭代了无数个版本、跑通了所有 Bug 的 `ART_BIBLE.md` 和提示词配方库，就是一套**极其昂贵的《2D 游戏 AI 工业母机白皮书与资产包》**。这完美回到了 K2B-OS 的初衷：你不仅做出了游戏，还能把这套“会下金蛋的管线”打包在 Gumroad 上出售！

### 3.4 快捷指令速查卡 (大白话版)

> **💡 锚点不是枷锁，是导航仪**
> 很多人会问："如果我定了概念锚点，以后还能换画风吗？"
> **答案是：随时可以换！** 你的项目是"视碰分离"架构（碰撞体大小是代码锁死的），所以视觉素材只是"皮肤"。如果你看腻了现在的风格，只需用指令 4 重新确立一个新锚点，后续出图跟着新锚点走就行，物理手感完全不受影响。

为了让你能立刻开始工作，我准备了四个最常用的指令。**直接复制发给我就行。**

#### 指令 1：确立概念锚点（最优先，只做一次）
**🤔 为什么要做这个？**
在批量出图前，我们需要先定一张"样板间照片"（比如：马里奥站在草地上，背景是蓝天）。这张图会决定整个游戏的色调、像素颗粒感和光影。后续所有出图都会参考这张图，确保画风统一。

**📦 你需要准备什么？**
你可以准备一张你喜欢的参考图发给我，或者直接告诉我你想要的风格（比如"暖色调复古像素"、"赛博朋克霓虹风"）。

**💬 复制发给我的指令：**
> "Manus，我要确立本项目的基准画风。我[上传了一张参考图 / 想要的风格是 XXX]。请根据我的偏好，给我派发一张『概念锚点 Mockup』工单，包含主角和基础地形。请输出中英文 Prompt 和 ComfyUI 参数建议。我出图满意后，会将图存入 `Assets/Art/Reference/Reference_Anchor.png`。"

#### 指令 2：喂书蒸馏（有新教程时执行）
**🤔 为什么要做这个？**
如果你在网上看到一本很牛的美术教程或设定集，你可以发给我。我会把里面好的参数和技巧"抄"下来，补充到我们的规范里。但我会把关，绝不吸收会破坏物理手感（比如导致滑步）的错误规则。

**📦 你需要准备什么？**
一本 PDF 教程、一篇文章截图、或一组优秀的参数参考。

**💬 复制发给我的指令：**
> "Manus，我上传了一本新教程/设定集。请执行『防污染 PR 合并』流程：提取其中的优秀 Prompt 和参数，对比 `ART_BIBLE`，以『最防滑步』为唯一准则解决冲突，绝不缝合。然后发 PR 给我 Review。"

#### 指令 3：批量生产具体资产（日常出图用）
**🤔 为什么要做这个？**
当锚点确立后，你需要开始真正生产游戏里的各种东西了（比如"主角跑步"、"弹跳怪受击"）。这个指令会让 AI 帮你算好这个动作需要几帧、怎么写 Prompt，并自动存档到配方库里。

**📦 你需要准备什么？**
明确你要做什么资产，以及之前保存好的 `Reference_Anchor.png` 锚点图。

**💬 复制发给我的指令：**
> "Manus，我要生产一组具体资产：[填写资产名称，如 弹跳怪跳跃]。请执行『带存档的工单派发』流程：拉取最新宪法，输出蓝图（帧数/极值帧/ControlNet模具/中英对照Prompt），追加到 `PROMPT_RECIPES.md` 并 Push 存档。然后把 Prompt 发给我。"

#### 指令 4：整体画风推翻重来（换皮用）
**🤔 为什么要做这个？**
如果你做了一半觉得现在的画风不好看，或者想做一个"里世界"的平行画风（比如从像素风换成手绘风），你可以用这个指令。

**📦 你需要准备什么？**
一张新的风格参考图，或者告诉我想换成什么风格。

**💬 复制发给我的指令：**
> "Manus，我觉得现在的画风不合适，我想整体替换为[上传参考图 / 描述新风格]。请执行『多画风版本切换』流程：
> 1. 帮我生成一张全新的『概念锚点 Mockup』工单
> 2. 指导我如何在 Unity 中使用 `LevelThemeProfile` 创建一套新的画风配置，确保不破坏旧的关卡和代码
> 3. 更新 `PROMPT_RECIPES.md` 中的基准 Prompt"

### 3.5 GitOps 核心指令详解 (底层逻辑)

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

## ⚙️ 第四区：项目基建与自动化工具链

### 4.1 资产目录结构 (Assets/Art/)
所有美术素材必须导入到以下标准化目录，`TA_AssetValidator` 的事前拦截仅对 `Assets/Art/` 目录生效：

```
Assets/Art/
├── Reference/     ← 存放概念锚点 (Mockup 截图)，作为后续出图的 IPAdapter 风格参考
├── Characters/    ← Mario, Trickster (校验 Pivot.y == 0)
├── Enemies/       ← SimpleEnemy, BouncingEnemy, FlyingEnemy (校验 Pivot.y == 0)
├── Environment/   ← Ground, Wall, Platform, OneWay, Conveyor... (校验 Pivot.y == 0.5)
├── Hazards/       ← SpikeTrap, FireTrap, SawBlade, Pendulum... (校验 Pivot.y == 0.5)
├── UI/            ← 界面元素、HUD
└── VFX/           ← 粒子特效、光效
```

### 4.2 `TA_AssetValidator.cs` (双重防御塔)

该脚本位于 `Assets/Scripts/Editor/TA_AssetValidator.cs`，提供两层防御：

**防御塔 1 — 事前拦截 (AssetPostprocessor.OnPreprocessTexture)**：当任何新图片被拖入 `Assets/Art/` 目录时，自动强制执行：PPU=32、AlphaIsTransparency=ON、FilterMode=Point、Compression=Uncompressed、MeshType=FullRect。不给人类犯错的机会。

**防御塔 2 — 主动扫描 (MenuItem “一键合规巡检”)**：通过菜单 `MarioTrickster/Art Pipeline/一键合规巡检` 触发，扫描 `Assets/Art/` 下所有贴图，校验 PPU、FilterMode、切片 Pivot。如有违规，报红错截停。

### 4.3 `AI_SpriteSlicer.cs` (自动化切片母机)

通过菜单 `MarioTrickster/Art Pipeline/一键工业化切图` 触发。支持三种资产类型：实体角色/敌人 (Bottom Center)、纯特效 VFX (Center)、地形/平台 (Center)。强制执行 PPU=32、Point 滤镜、无压缩。

### 4.4 工具链协作流程

```
拖入图片 → [TA_AssetValidator 事前拦截] → 自动强制 PPU/Filter/Alpha
    ↓
使用 AI_SpriteSlicer 切片 → 强制 Pivot + 帧切割
    ↓
点击“一键合规巡检” → [TA_AssetValidator 主动扫描] → 全工程校验
    ↓
通过 → git commit + push
```

---
*Last Updated: 2026-04-09 by Manus TA*
