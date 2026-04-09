# MarioTrickster 美术管线操作菜单 (防荒废工业版 2.0)

> **💡 核心防坑指南：彻底消灭"本地调参疲劳"**
> 多平台联动的死穴是"凭感觉在本地反复调参"。从今天起：**Manus 是大脑（算参数、改配方），你只是无情的渲染终端（复制、粘贴、跑测试）。**
> 只要出图有瑕疵，绝不自己瞎调，直接把现象丢给 Manus 重新要图纸。

---

## 🛠️ 第 0 步：本地渲染终端准备清单 (仅需做一次)

在给 Manus 发指令前，请确保你的本地厨房已经配齐了以下工具。如果已经搞定，请直接跳到【菜单 1】。

### 1. 核心引擎与大模型
- **安装 ComfyUI**：推荐下载官方 [ComfyUI Portable (Windows)](https://github.com/comfyanonymous/ComfyUI/releases) 一键解压版。
- **安装 ComfyUI Manager**：在 `custom_nodes` 目录下 `git clone https://github.com/ltdrdata/ComfyUI-Manager`，用于后续一键安装缺失节点。
- **下载大模型 (Checkpoint)**：去 Civitai 下载 `sd_xl_base_1.0.safetensors` 或你喜欢的二次元/手绘风 XL 模型，放入 `models/checkpoints/`。

### 2. 必备插件 (通过 ComfyUI Manager 搜索安装)
- **IPAdapter Plus** (`ComfyUI_IPAdapter_plus`)：用于【菜单 2】的垫图抽卡。需同时下载对应的 CLIP Vision 模型和 IPAdapter XL 权重。
- **ControlNet 节点**：用于【菜单 3】的防滑步骨架锁定。需下载 SDXL 对应的 OpenPose 和 Canny 模型放入 `models/controlnet/`。

### 3. 辅助工具链
- **去底工具**：推荐使用 [remove.bg](https://www.remove.bg/) 或 Photoshop 一键抠图，用于将 ComfyUI 出图的纯色背景变为透明 PNG。
- **云端炼丹炉**：注册 [LiblibAI](https://www.liblib.art/) 或 [Civitai](https://civitai.com/) 账号，用于在【菜单 2】收集齐 30 张图后在线训练 LoRA。

---

## 菜单 1：喂书蒸馏 —— 建立「参数级配方库」 (有牛逼教程时随时做)

**🤔 怎么让蒸馏效果最大化且不递减？**
绝对不让 AI 总结"优美的提示词"，而是提取书里的【透视、光影法则、像素规律】，直接转译为 **ComfyUI 的底层数学参数** 和 **高权重短语 (Tags)**。

### 💬 怎么找 Manus (直接复制发给它)：
> "Manus，我刚上传了一份顶级的 [美术教程/设定集]。请启动『参数级无损蒸馏』：
> 1. 提纯核心信号：提取该风格最强力的 5-8 个英文触发短词（剔除无效修饰语），并分配权重（如 1.2~1.5）。
> 2. 抓取硬核参数：教程中推荐的 CFG 甜区、Denoising 比例是多少？推荐哪种 ControlNet 预处理器来锁结构？
> 3. 物理碰撞核查：将教程中的动作规范与 `ART_BIBLE.md` 对碰，剔除所有会导致"滑步"的建议。
> 4. 资产沉淀：将上述【触发词配方】和【控制参数建议】直接 Push/PR 更新到 `prompts/PROMPT_RECIPES.md` 中。
> 5. 极简汇报：用一句话告诉我：更新了什么硬控规则。"

### 🛠️ 你接下来要干嘛：
1. **等待更新**：等 Manus 告诉你配方库（`PROMPT_RECIPES.md`）已更新。
2. **应用新规**：在后续执行【菜单 2】或【菜单 3】时，Manus 会自动为你挂载这些大师级的参数红线。
3. **享受成果**：你的出图质量将直接提升，无需自己摸索参数。

---

## 菜单 2：确立画风 (探索期：用 IPAdapter 抽盲盒)

在批量做资产前，我们要用垫图快速试错。不用追求单图完美，我们的目标是攒够 30 张同画风的图去炼专属 LoRA。

### 💬 怎么找 Manus (直接复制发给它)：
> "Manus，我要确立本项目的基准画风。我上传了一张参考图。请根据《最佳实践》的【探索期】工作流，给我派发一张『风格探索工单』。
> 请提供基于 Tag 结构的极简 Prompt，并为我锁定安全的 CFG 和 IPAdapter 参数。我会去本地疯狂抽卡，只要风格对味就保存，直到攒够 30 张图集。"

### 🛠️ 你接下来要干嘛 (闭环到 Unity)：
1. **疯狂抽卡**：拿着工单在 ComfyUI 里疯狂抽卡。遇到 80 分的图就存下来。
2. **炼制 LoRA**：凑齐 30 张同风格的图后，找一个云端赛博丹炉（如 Liblib/炼丹阁）炼制成 `Trickster_Style_LoRA.safetensors`。
3. **装配武器**：将炼好的 LoRA 文件下载并放入你本地 ComfyUI 的 `models/loras/` 目录下。
4. **宣告进入量产期**：告诉 Manus："LoRA 已就绪，画风彻底焊死，我们进入量产期。"

---

## 菜单 3：零调参批量生产 (量产期：日常闭环)

**🤖 防衰减三锁策略**：为了告别盲盒，必须使用：风格锁（专属 LoRA）+ 结构锁（ControlNet）+ 语义锁（极简 Tag 工单）。

### 💬 怎么找 Manus (直接复制发给它)：
> "Manus，当前处于【LoRA 量产期】。我要做游戏资产：【这里填你要做的东西，比如：主角跳跃动画 4 帧】。
> 请查询 `prompts/PROMPT_RECIPES.md`，直接输出一个可直接复制的 Markdown『本地制造工单』。必须包含：
> 1. 🟢 正向提示词（LoRA触发词开头，仅用极简 Tag 描述纯物理结构和光影，<20词）。
> 2. 🔴 反向提示词（防脏污、多肢体）。
> 3. ⚙️ 参数红线锁定（明确写出建议的 CFG 数值、Denoising 比例）。
> 4. 📌 结构防滑步建议（提醒我这一帧需要上传什么样的参考骨架图给 ControlNet）。"

### 🛠️ 你接下来要干嘛 (无脑执行闭环)：
1. **只做粘贴**：按 Manus 的图纸一字不差填入 ComfyUI，一键出图。
2. **报错向上甩锅 (极度重要)**：如果图跑出来"手脚粘连/透视歪了"，**绝对不要自己去调 CFG 或改词**。直接告诉 Manus："工单出图失败，角色双腿粘连，帮我重新算 Prompt 权重或给新参数方案"，由它重新发牌。
3. **去底透明化**：用 PS 或在线工具将满意图片的纯色背景抠除，保存为透明 PNG。
4. **导入 Unity**：将 PNG 拖入 Unity 的 `Assets/Art/` 对应子目录（如 `Characters/` 或 `Environment/`）。
5. **一键切图与锁重心**：选中刚导入的图片，点击 Unity 顶部菜单 `MarioTrickster/Art Pipeline/一键工业化切图`。在弹窗中选择资产类型（角色选 Bottom Center，地形选 Center），点击"一键扣除纯色底并精准切片"。这会自动把 PPU 设为 32，Filter 设为 Point，并锁死防滑步重心。
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
