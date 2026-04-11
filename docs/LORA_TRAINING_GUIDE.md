# LoRA 训练与菜单3衔接指南

本文档对比了当前主流平台的 LoRA 训练成本，并提供了从 30 张画风样本图到 ComfyUI 菜单3 批量出图的完整操作指引。

## 一、训练平台成本对比 (2026年4月数据)

在选择训练平台时，核心考量在于你的使用频率。LiblibAI 和 Civitai 采用了完全不同的计费逻辑。

| 平台 | 计费模式 | 单次训练成本 | 免费额度与门槛 | 适用人群 |
|---|---|---|---|---|
| **Civitai** | 按次付费 (Buzz) | 500 Buzz (约 ¥3.5) [1] | 每日做任务可领 200+ 免费 Buzz [2] | **偶尔训练 1-2 个模型的人**（甚至可以白嫖） |
| **LiblibAI** | 会员制 + 积分 | 30-50 积分 (约 ¥3-5) [3] | 必须开通会员（最低 ¥399/年）才能训练 [4] | **高频训练的专业创作者** |

**结论与建议：**
对于当前项目（只需要训练 1 个全局 Style LoRA），**强烈建议使用 Civitai**。你只需要注册账号，利用初始赠送的 Buzz 或做两天日常任务，就能**完全免费**完成这次训练。而 LiblibAI 虽然单次只要 3 块钱，但门槛是必须先充值 399 元的年费会员。

---

## 二、Civitai 免费训练操作指引

请按照以下步骤，将你跑出的 30 张图炼制成专属的 Style LoRA。

### 1. 准备数据集
1. 在你的电脑上新建一个文件夹，命名为 `trickster_style_dataset`。
2. 把你刚才在 ComfyUI 跑出来的 30 张最满意的画风图放进去。
3. **不需要打标签（Caption）**：因为我们训练的是全局画风（Style LoRA），不是特定角色。不打标签会让模型把这 30 张图里的所有共性（描边、色彩、阴影）全部吸收到一个触发词里。
4. 把这个文件夹压缩成一个 `.zip` 文件。

### 2. 在 Civitai 发起训练
1. 访问 [Civitai 官网](https://civitai.com/) 并注册/登录账号。
2. 点击右上角的 **Create** 按钮，选择 **Train a LoRA**。
3. **模型类型**：选择 **Style**（非常重要，不要选 Character）。
4. **上传数据**：把刚才的 `.zip` 压缩包拖拽上传。
5. **基础模型 (Base Model)**：选择 **SDXL 1.0**（这会花费 500 Buzz，如果你选其他自定义模型会涨到 1000 Buzz）。

### 3. 关键训练参数设置
在 Training Parameters 区域，修改以下核心参数以保证画风学习效果：

| 参数名 | 推荐值 | 说明 |
|---|---|---|
| **Epochs** | 10 | 训练轮数。Style LoRA 需要多跑几轮才能吃透画风。 |
| **Network Dim (Rank)** | 32 或 64 | 决定模型能记住多少细节。画风比较简单选 32，细节多选 64。 |
| **Network Alpha** | 16 或 32 | 通常设置为 Dim 的一半。 |
| **Trigger Word** | `trickster_style` | 触发词。以后在提示词里写这个词，就能召唤出你的画风。 |

设置完毕后，点击 **Submit** 提交训练。通常需要等待 30-60 分钟。

### 4. 下载与测试
训练完成后，Civitai 会生成 10 个 Epoch 的模型文件（对应你设置的 10 轮）。
1. 下载最后三个 Epoch 的文件（比如 Epoch 8, 9, 10）。
2. 把它们放到你本地 ComfyUI 的 `models/loras/` 目录下。

---

## 三、如何衔接菜单3（批量出图）

当你的 LoRA 文件（例如 `trickster_style_e10.safetensors`）放入 `models/loras/` 目录后，你就可以正式进入菜单3的生产环节了。

### 1. 菜单3 的核心变化
在菜单3的工单中，我们将**彻底抛弃 IPAdapter 和参考图**。取而代之的是一个极其干净的管线：

**底模 (SDXL) → 加载 LoRA (你的画风) → 提示词 (实体蓝图) → 出图**

### 2. 提示词的写法变化
你不再需要写那一大堆冗长的画风描述词（如 `cel shading`, `thick black outlines`），因为这些特征已经全部被压缩进了你的 LoRA 里。

**新的提示词公式：**
`[触发词] + [实体蓝图词] + [动作/环境描述]`

**示例：**
`trickster_style, 1boy, mario, red hat, blue overalls, running in a green forest, top-down view`

### 3. 下一步行动
当你把训练好的 LoRA 文件放进 ComfyUI 目录后，请回复我：
> “LoRA 已就绪，文件名为 `trickster_style_e10.safetensors`。请给我下发菜单3的【角色测试工单】。”

我会为你生成一份全新的、带有 `Load LoRA` 节点的极简版 ComfyUI 连线图纸。

---

## 参考资料
[1] Civitai Education. "SDXL LoRAs Training Guide - CivitAI Trainer". https://civitai.com/articles/14024/sdxl-loras-training-guide-civitai-trainer
[2] Civitai Education. "A guide to earning your 200+ daily (blue) buzz". https://civitai.com/articles/10006/a-guide-to-earning-your-200-daily-blue-buzz
[3] Flowith Blog. "Liblib.art 2026 FAQ: Model Upload, LoRA Training, Copyright Policy, and API Access Explained". https://flowith.io/blog/liblib-art-2026-faq-model-upload-lora-training-copyright-api
[4] 知乎. "LiblibAI哩布哩布AI：满血不收费的ai生图真的来了！". https://zhuanlan.zhihu.com/p/687566650
