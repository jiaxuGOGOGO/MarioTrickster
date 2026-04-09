# MarioTrickster 工业化美术管线操作菜单 (V3.0 抽屉与节点终极版)

> **💡 核心护城河：彻底消灭"本地调参内耗"**
> Manus 是大脑（负责啃书、分抽屉、算节点参数、解决冲突）；你只是无情的渲染终端（复制、填入 ComfyUI、跑图验收）。图坏了绝不自己拉滑块，直接丢给 Manus 重新发牌！

---

## 🛠️ 步骤 0：本地渲染终端准备清单 (仅需做一次)

在给 Manus 发指令前，请确保你的本地厨房已经配齐了以下工具。如果已经搞定，请直接跳到【每日开工启动】。

### 1. 核心引擎与大模型
- **安装 ComfyUI**：推荐下载官方 [ComfyUI Portable (Windows)](https://github.com/comfyanonymous/ComfyUI/releases) 一键解压版。
- **安装 ComfyUI Manager**：在 `custom_nodes` 目录下 `git clone https://github.com/ltdrdata/ComfyUI-Manager`，用于后续一键安装缺失节点。
- **下载大模型 (Checkpoint)**：去 Civitai 下载 `sd_xl_base_1.0.safetensors` 或你喜欢的二次元/手绘风 XL 模型，放入 `models/checkpoints/`。

### 2. 必备插件 (通过 ComfyUI Manager 搜索安装)
- **IPAdapter Plus** (`ComfyUI_IPAdapter_plus`)：用于【菜单 2】的垫图抽卡。需同时下载对应的 CLIP Vision 模型和 IPAdapter XL 权重。
- **ControlNet 节点**：用于【菜单 3】的防滑步骨架锁定。需下载 SDXL 对应的 OpenPose、Canny、Depth 模型放入 `models/controlnet/`。

### 3. 辅助工具链
- **去底工具**：推荐使用 [remove.bg](https://www.remove.bg/) 或 Photoshop 一键抠图，用于将 ComfyUI 出图的纯色背景变为透明 PNG。
- **云端炼丹炉**：注册 [LiblibAI](https://www.liblib.art/) 或 [Civitai](https://civitai.com/) 账号，用于在【菜单 2】收集齐 30 张图后在线训练 LoRA。

---

## 🟢 每日开工启动（每次新开 AI 对话时必发）

每次开启新的对话框时，**必须第一时间**复制以下指令发给 Manus，唤醒底层防撞机制：

**💬 怎么找 Manus (直接复制发给它)：**
> GitHub Token: [填入你的Token]
> 仓库：https://github.com/jiaxuGOGOGO/MarioTrickster
> 
> 请先用 Token 克隆仓库。静默读取 `SESSION_TRACKER.md` 获取当前进度，并强制读取 `prompts/PROMPT_RECIPES.md`。
> 
> 【系统指令：加载 MarioTrickster 工业化美术管线】
> 从现在起，你是本项目的「首席技术美术(TA)」。你已知悉我本地拥有完整的 ComfyUI 环境（含 IPAdapter Plus, ControlNet OpenPose/Canny/Depth, SDXL, LoRA）。
> 
> 针对知识提炼与工单下发，你必须遵守以下铁律：
> 1. 抽屉归档与节点隔离：提取教程时，按「解剖/透视/动画/光影/参数」5个知识抽屉静默写入 `prompts/PROMPT_RECIPES.md`。下发工单时，绝对禁止把所有要求塞进 Prompt，必须拆解映射到具体节点（如：透视交由 ControlNet，画风交由 LoRA，基础渲染交由 Prompt）。
> 2. 冲突熔断原则：当多抽屉知识组合时，永远遵循「物理防滑步 > 空间透视正确 > 光影材质表现」的优先级。有矛盾时自动静默裁剪低优先级约束。
> 3. 拒绝小作文：对话框内仅输出可直接复制到 ComfyUI 的具体节点配置图纸。
> 
> 准备好请回复：“🛠️ 工业化管线就绪。抽屉系统与节点分发规则已加载。请下达指令。”

---

## 📚 菜单 1：喂书蒸馏 (有牛逼教程时随时做)

把找来的杂乱教程扔给 AI，它会自动将知识分拣进不同的"抽屉"，防止后期知识大乱炖。

**💬 怎么找 Manus (直接复制发给它)：**
> "Manus，我刚上传了一份顶级的 [美术教程/设定集]。请启动『领域精细化无损蒸馏』：
> 1. **智能分拣**：判断核心价值，将其严格归入以下 5 个「知识抽屉」中的 1-2 个：
>    - 🧍‍♂️ **[解剖与形态]**（头身比、防断肢、多角度姿势）
>    - 📐 **[透视与物件]**（等距视角、静物结构、平台拼接规律）
>    - 🏃 **[动画与物理]**（关键帧数、运动模糊、防滑步约束）
>    - 🎨 **[光影与材质]**（特定材质画法、边缘光、全局色调）
>    - ⚙️ **[AI 硬核参数]**（CFG 甜区、ControlNet 预处理器建议）
> 2. **标签化提取**：不要写散文。只提炼能在 ComfyUI 中生效的 Tag 短语、数值和节点约束。
> 3. **防污染沉淀**：将提炼的规则，更新到 `prompts/PROMPT_RECIPES.md` 中对应的抽屉标题下。如果不涉及某领域，绝对不要碰那个抽屉！
> 4. **极简汇报**：一句话告诉我更新了哪几个抽屉即可。"

### 🛠️ 你接下来要干嘛：
1. **等待更新**：等 Manus 告诉你哪些抽屉已更新。
2. **应用新规**：在后续执行【菜单 2】或【菜单 3】时，Manus 会自动从这些抽屉里抓取参数，分发到 ComfyUI 的对应节点。
3. **享受成果**：出图质量自动提升，且知识互不干扰。

---

## 🎨 菜单 2：确立画风 (探索期：不用追求单图完美)

在批量做资产前，用垫图快速试错，目标是攒够 30 张同画风图去炼专属 LoRA。

**💬 怎么找 Manus (直接复制发给它)：**
> "Manus，我要确立本项目的基准画风。我上传了一张参考图。请给我派发一张『风格探索工单』。
> 请调取 [光影与材质] 和 [AI 硬核参数] 抽屉。提供极简 Prompt，并为我锁定安全的 KSampler 和 IPAdapter 参数。我会去本地疯狂抽卡，只要风格对味就保存，直到攒够 30 张图集。"

### 🛠️ 你接下来要干嘛 (闭环到 Unity)：
1. **疯狂抽卡**：拿着工单在 ComfyUI 里疯狂抽卡。遇到 80 分的图就存下来。
2. **炼制 LoRA**：凑齐 30 张同风格的图后，去 LiblibAI 等云端平台炼制 `Trickster_Style_LoRA.safetensors`。
3. **装配武器**：将炼好的 LoRA 文件放入本地 ComfyUI 的 `models/loras/` 目录下。从此画风彻底焊死，告别垫图。
4. **宣告进入量产期**：告诉 Manus："LoRA 已就绪，画风彻底焊死，我们进入量产期。"

---

## 🏭 菜单 3：批量生产具体资产 (量产期：靶向抓药 + 节点派发)

出图前强制 Manus 声明拉开了哪几个抽屉，并严格将参数分发给 ComfyUI 的各个节点，彻底杜绝打架。

**💬 怎么找 Manus (直接复制发给它)：**
> "Manus，我要做游戏资产：**【👉 这里填你的需求，例如：主角受击退的 3 帧动画 / 或：静态铁刺陷阱】**。
> 请启动『靶向按需组装』流程，查阅 `prompts/PROMPT_RECIPES.md`：
> 
> 1. **精准抓药声明**：明确告诉我你本次拉开了 5 个抽屉中的哪几个？(注：比如做静态陷阱严禁混入动画规则)。若遇到规则冲突，严格执行优先级熔断机制。
> 2. **输出「节点化」制造工单**：请给我一份可直接在 ComfyUI 本地照抄的 Markdown 工单。严格按以下节点分类输出，绝不把所有东西揉进文本里：
>    - 🖼️ **[Load LoRA 节点]**：提醒我挂载本项目专属 LoRA 及推荐权重。
>    - 📝 **[Text Prompt 节点]**：仅保留 🎨[光影与材质] 相关的极简正向/反向 Tags (<20词)。
>    - 🤖 **[ControlNet 节点]**：根据 🏃[动画] 或 📐[透视] 的要求，明确告诉我该启用哪个模型（OpenPose/Canny/Depth），以及我该去哪找/画一张什么样的底图喂给它。
>    - ⚙️ **[KSampler 节点]**：给出 ⚙️[AI参数] 抽屉里建议的 CFG 和 Denoising 安全数值。
> 绝对不要在对话框写小作文，只给我可以直接 Copy 的节点图纸！"

### 🛠️ 你拿到工单后该怎么做 (端到端闭环)：
1. **纯粹执行**：一字不差按 Manus 给的工单图纸，配置你本地的 ComfyUI 对应节点。
2. **遇错甩锅 (极重要)**：如果图跑出来透视崩了或者多腿了，**绝不自己调参数**！带上坏图告诉 Manus："工单出图失败，现象是XXX，请调整 ControlNet 策略或按熔断原则重新给我发图纸！"
3. **去底透明化**：用 PS 或在线工具（如 remove.bg）将满意图片的纯色背景抠除，保存为透明 PNG。
4. **导入 Unity**：将 PNG 拖入 Unity 的 `Assets/Art/` 对应子目录。
5. **一键切图与锁重心**：选中图片，点击 Unity 菜单 `MarioTrickster/Art Pipeline/一键工业化切图`。选择资产类型，点击切片（自动设 PPU=32、Point 滤镜并锁死防滑步重心）。
6. **换皮测试**：把切好的 Sprite 拖给场景中对应物体的 `Visual` 节点。运行游戏跑一跑。
7. **一键巡检与存档**：点击菜单 `MarioTrickster/Art Pipeline/一键合规巡检`。全绿通过后，告诉 Manus："【某某资产】测试通过，请 Push 代码。"

---

## 📚 附录：高级文档索引

如果你好奇背后的原理，或者想深入修改项目规则，可以看这些文档：

| 你想了解什么 | 去看哪个文档 |
|-------------|-------------|
| **防衰减的原理、为什么必须练 LoRA** | `docs/STYLE_FIDELITY_BEST_PRACTICES.md` |
| **物理尺寸、重心、PPU等硬性红线** | `docs/ART_BIBLE.md` |
| **具体每个实体的 Prompt 配方库** | `prompts/PROMPT_RECIPES.md` |
| **画风一致性的底层技术架构** | `docs/STYLE_CONSISTENCY_ARCHITECTURE.md` |

*Last Updated: 2026-04-09 by Manus TA*
