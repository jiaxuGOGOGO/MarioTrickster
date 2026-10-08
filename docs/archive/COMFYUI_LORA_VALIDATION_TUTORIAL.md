# ComfyUI 零基础操作手册：`trickster_style` LoRA 验证专版

> **说明**：本手册专为 MarioTrickster 项目的 LoRA 本地验证任务编写。即使你之前完全没有用过 ComfyUI，只要按照以下步骤操作，也能顺利跑完 30 张验证图。

---

## 第一部分：环境准备与文件放置

在启动 ComfyUI 之前，你需要确保模型文件放在了正确的文件夹中。ComfyUI 的核心逻辑是“按文件夹找模型”，如果放错位置，节点将无法读取到文件。

**大模型（Checkpoint）**
你需要一个基础的文生图大模型（例如 `sd_xl_base_1.0.safetensors` 或你常用的二次元底模）。请将该文件放入 `ComfyUI/models/checkpoints/` 文件夹中 [1]。

**LoRA 模型**
本次验证的主角是 `MarioTrickster_Style_epoch_10.safetensors`。请将该文件放入 `ComfyUI/models/loras/` 文件夹中 [1]。

放置完毕后，启动 ComfyUI。如果你在启动后才放入文件，请点击右侧控制面板的 **Refresh（刷新）** 按钮，让系统重新扫描文件夹。

---

## 第二部分：搭建基础验证工作流

ComfyUI 是一个基于节点的连线工具。为了完成本次验证，我们需要搭建一个包含 LoRA 的标准文生图工作流。请在空白画布上（可右键选择 `Clear` 清空画布）按以下顺序添加并连接节点。

### 1. 添加核心节点

在画布空白处双击鼠标左键，会弹出一个搜索框。请依次搜索并添加以下 6 个节点：

| 节点中文名（英文搜索词） | 节点作用 |
|---|---|
| **Checkpoint加载器** (`Load Checkpoint`) | 加载基础大模型 |
| **LoRA加载器** (`Load LoRA`) | 加载本次要验证的风格模型 |
| **CLIP文本编码器** (`CLIP Text Encode`) | 需要添加 **两个**，分别用于正向和负向提示词 |
| **空Latent图像** (`Empty Latent Image`) | 定义生成图片的尺寸（分辨率） |
| **K采样器** (`KSampler`) | 核心生成节点，负责去噪出图 |
| **VAE解码** (`VAE Decode`) | 将潜空间数据转换为可见的像素图片 |
| **保存图像** (`Save Image`) | 预览并保存最终生成的图片 |

### 2. 节点连线指南

节点添加完毕后，需要通过拖拽端口上的小圆点进行连线。请严格按照以下路径连接：

**模型与 LoRA 链路**
将 `Load Checkpoint` 的 **MODEL** 端口连到 `Load LoRA` 的 **model** 端口。
将 `Load Checkpoint` 的 **CLIP** 端口连到 `Load LoRA` 的 **clip** 端口。
将 `Load Checkpoint` 的 **VAE** 端口直接连到 `VAE Decode` 的 **vae** 端口（注意：VAE 不经过 LoRA）。

**提示词链路**
将 `Load LoRA` 的 **MODEL** 端口连到 `KSampler` 的 **model** 端口。
将 `Load LoRA` 的 **CLIP** 端口分别连到 **两个** `CLIP Text Encode` 的 **clip** 端口。
将第一个 `CLIP Text Encode`（作为正向提示词）的 **CONDITIONING** 端口连到 `KSampler` 的 **positive** 端口。
将第二个 `CLIP Text Encode`（作为负向提示词）的 **CONDITIONING** 端口连到 `KSampler` 的 **negative** 端口。

**图像生成链路**
将 `Empty Latent Image` 的 **LATENT** 端口连到 `KSampler` 的 **latent_image** 端口。
将 `KSampler` 的 **LATENT** 端口连到 `VAE Decode` 的 **samples** 端口。
将 `VAE Decode` 的 **IMAGE** 端口连到 `Save Image` 的 **images** 端口。

---

## 第三部分：参数设置与锁定

连线完成后，我们需要锁定全局参数，确保 30 张图的测试条件完全一致。

### 1. 锁定全局参数

请在对应的节点中设置以下参数，并在整个验证过程中**不要修改它们**：

| 节点 | 参数名 | 推荐设置值 |
|---|---|---|
| **Load Checkpoint** | ckpt_name | 选择你的大模型（如 `sd_xl_base_1.0.safetensors`） |
| **Empty Latent Image** | width / height | `1024` / `1024`（或你常用的分辨率） |
| **Empty Latent Image** | batch_size | `1`（每次生成一张） |
| **KSampler** | steps | `30` |
| **KSampler** | cfg | `7.0` |
| **KSampler** | sampler_name | `euler_ancestral` 或 `dpmpp_2m` |
| **KSampler** | scheduler | `karras` |
| **CLIP Text Encode (负向)** | 文本框内容 | `photorealistic, realistic shading, 3d render, blurry, muddy colors, messy lines, text, watermark, logo, cropped, low contrast, noisy details` |

### 2. 锁定 Seed（随机种子）

在 `KSampler` 节点中，有一个名为 **seed** 的参数，下方有一个 **control_after_generate** 选项。
为了保证同一题材下的变量唯一，请将 **control_after_generate** 设置为 `fixed`（固定）。这样每次点击生成时，Seed 值都不会改变 [2]。
当你需要切换到下一个题材（例如从静态资产 S 切换到角色动作 A）时，手动修改一下 **seed** 的数字即可。

---

## 第四部分：执行 30 张图的跑图流程

现在工作流已经准备就绪，你可以对照《`trickster_style` 本地验证执行速查表》开始跑图了。

### 1. 如何调整 LoRA 权重

在 `Load LoRA` 节点中，有两个控制权重的参数：**strength_model** 和 **strength_clip**。
在本次验证中，请始终让这两个值保持一致。
- 当表格要求权重为 `0.6` 时，将两者都改为 `0.6`。
- 当表格要求权重为 `0.8` 时，将两者都改为 `0.8`。
- 当表格要求权重为 `1.0` 时，将两者都改为 `1.0`。

### 2. 如何跑 B0（基线/关闭 LoRA）模式

表格中的 B0 模式要求完全不挂载 LoRA。在 ComfyUI 中，最安全的做法是**绕过（Bypass）**节点。
请右键点击 `Load LoRA` 节点，在弹出的菜单中选择 **Bypass**（节点会变成半透明或紫色）。此时 LoRA 将完全失效，模型信号会直接穿过它到达下一步。
跑完 B0 后，再次右键选择 **Bypass** 即可恢复 LoRA 的正常工作。

### 3. 跑图与保存

1. 将速查表中的正向 Prompt 复制到第一个 `CLIP Text Encode` 的文本框中。
2. 确认 LoRA 权重和 Seed 设置正确。
3. 点击右侧控制面板的 **Queue Prompt（添加提示词队列）** 按钮，或使用快捷键 `Ctrl + Enter` 开始生成 [2]。
4. 生成完毕后，图片会显示在 `Save Image` 节点中。右键点击图片，选择 **Save Image（保存图像）**。
5. 保存时，请严格按照速查表中的“预期保存文件名”进行重命名（例如 `TSVAL_S_B2_w08_seed111111.png`）。

重复以上步骤，直到 30 张图全部跑完。

---

## 参考文献

[1] ComfyUI LoRA Example. https://docs.comfy.org/tutorials/basic/lora
[2] ComfyUI Text to Image Workflow. https://docs.comfy.org/tutorials/basic/text-to-image
