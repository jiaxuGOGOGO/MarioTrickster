# LoRA 训练与菜单3无缝衔接指南

> **K2B-OS: Advanced Art Pipeline Extension**
> 本文档定义了 MarioTrickster 项目从「菜单2：风格探索（IPAdapter）」向「菜单3：量产下发（LoRA）」跨越时的**成本预算规划**与**无缝衔接实操指引**。

---

## 1. LoRA 需求盘点与预算规划

基于项目的实体蓝图库（22 个核心实体）和多画风架构规划，我们对 Civitai 线上训练成本（500 Buzz/次）进行了精确的沙盘推演。

**前置条件：**
- **月度预算**：¥88/月（购买 2 次 5000 积分的 Civitai 账号）= **10000 Buzz/月**
- **单次训练成本**：500 Buzz（使用 SDXL 1.0 官方底模）
- **每月可用训练次数**：20 次

### 1.1 核心需求定调（当前阶段：最少 1 个，最多 3 个）

对于当前的 MarioTrickster 项目，**绝不需要为每个怪物或陷阱单独训练 LoRA**。所有的角色、地形、陷阱、交互物都共享一套底层视觉规则（描边、色彩、阴影、材质质感）。

根据管线推进深度，LoRA 需求分为三档：

| 优先级 | LoRA 名称 | 用途与覆盖范围 | 预估迭代次数 | 消耗 Buzz | 占月预算比 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **第一档（当前必须）** | `trickster_style` | 锁定 30 张画风图的全局视觉共性。覆盖当前所有 22 个实体蓝图。 | 3-4 次 | 1500-2000 | 15-20% |
| **第二档（中期推荐）** | `trickster_vfx` | 专门处理火焰、爆炸、雷电等 VFX 资产。因特效视觉语言与实体差异过大，建议剥离。 | 2-4 次 | 1000-2000 | 10-20% |
| **第三档（后期扩展）** | `trickster_retro_8bit` | 配合 `LevelThemeProfile` 实现多画风切换（如 8-bit 复古风皮肤）。 | 4-6 次 | 2000-3000 | 20-30% |

**预算结论**：
每月 10000 Buzz 的预算极其充裕。即使按最奢侈的全量扩展方案（3 个 LoRA + 14 次迭代），也只会用掉 70% 的预算。**当前阶段，你只需要花费 1500-2000 Buzz 训练并调优 `trickster_style` 这一个全局画风 LoRA，即可立即打通菜单3的量产管线。**

---

## 2. 菜单2 → 菜单3 衔接痛点剖析

在之前的流程中，从菜单2（30张画风图出炉）到菜单3（角色量产）之间存在断层，主要痛点在于：
1. **不确定 Civitai 训练参数**，怕浪费 Buzz。
2. **不知道训练完后，ComfyUI 的节点该怎么连**。
3. **不知道提示词（Prompt）该怎么改**。

以下是针对这三个痛点的标准解法。

---

## 3. Civitai 训练实战参数（防浪费指南）

请将菜单2跑出的 30 张最满意的画风图放入 `trickster_style_dataset` 文件夹并压缩为 `.zip`。**不需要打标签（Caption）**，因为我们要模型吸收这 30 张图里的所有全局共性。

在 Civitai 发起训练时，请严格遵守以下参数设置以确保一次成功（或最多微调 1-2 次）：

| 参数名 | 推荐设定值 | 设定理由 |
| :--- | :--- | :--- |
| **Model Type** | `Style` | 极其重要！选 Character 会导致模型过度拟合某个特定角色，导致地形和陷阱出图崩坏。 |
| **Base Model** | `SDXL 1.0` | 保证 500 Buzz 的基础定价。 |
| **Epochs** | `10` | Style LoRA 需要多轮次才能吃透画风。Civitai 会保存每个 Epoch 的版本供你挑选。 |
| **Network Dim (Rank)** | `64` | 像素艺术虽然看似简单，但边缘硬度、色彩块面等特征需要足够的维度来记忆。 |
| **Network Alpha** | `32` | 保持为 Dim 的一半，防止权重爆炸。 |
| **Optimizer** | `Prodigy` 或 `Adafactor` | 适合 Style 训练的自适应优化器。 |
| **Trigger Word** | `trickster_style` | 核心触发词。 |

*注：训练完成后，请下载最后三个 Epoch（如 Epoch 8, 9, 10）的 `.safetensors` 文件，放入本地 ComfyUI 的 `models/loras/` 目录下进行 A/B 测试。*

---

## 4. 菜单3：无缝衔接的管线改造

当你拿到 `trickster_style_e10.safetensors` 后，管线将发生质变。我们将**彻底抛弃菜单2中臃肿的 IPAdapter 和垫图**。

### 4.1 节点架构的物理隔离

在菜单3中，ComfyUI 的节点连线必须遵循以下“十字交叉”双轨架构：

1. **底层骨架（Checkpoint）**：`SDXL 1.0 Base`。
2. **画风注入（Load LoRA 节点）**：串联在 Checkpoint 之后，加载 `trickster_style_e10.safetensors`，权重建议设为 `0.8 - 1.0`。
3. **结构约束（ControlNet 节点）**：如果是角色，走 `DWPose`；如果是硬表面陷阱，走 `Lineart`。
4. **语义交叉（Text Prompt）**：见下文。

### 4.2 提示词（Prompt）的减负与重构

在菜单2中，为了逼出画风，你可能写了大量的 `pixel art, 16-bit, cel shading, thick black outlines, flat colors`。
在菜单3中，这些词**全部删除**。它们已经被固化在 LoRA 里了。

**新版 Prompt 公式：**
`[LoRA 触发词] + [实体蓝图词 (What)] + [技法抽屉词 (How)] + [环境/视角描述]`

**实战对比（以 Mario 为例）：**

*❌ 菜单2（旧版臃肿写法）：*
> 16-bit pixel art, retro game style, cel shading, thick black outlines, flat colors, Mario-like character, red cap, blue overalls, running, dynamic pose, anti-aliasing...

*✅ 菜单3（新版清爽写法）：*
> trickster_style, Mario-like character, red cap, blue overalls, running, contract_stretch_rule, opposite_side_stretch, side_view_depth_enhancement, white background.

### 4.3 衔接启动指令

当你把 LoRA 文件放入本地目录后，只需在对话框中对我发送以下指令，我就会为你下发第一张【菜单3标准工单】：

> **“LoRA 已就绪，文件名为 `trickster_style_e10.safetensors`。请按双轨架构给我下发菜单3的【主角跑动测试工单】。”**

我会严格按照 `WORKORDER_QA_STANDARD.md` 的四区格式，为你输出可直接复制的极简节点图纸。
