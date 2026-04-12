# `trickster_style` 本地验证执行速查表

> **说明**：这是基于 `TRICKSTER_STYLE_LOCAL_VALIDATION_WORK_ORDER_2026-04-12.md` 展开的 30 张逐张执行清单。你可以直接复制这里的 Prompt 到 ComfyUI 中跑图，跑完把 output 文件夹里的图打包发给我即可。

---

## 1. 必须锁定的全局设置

在开始跑这 30 张图之前，请确认 ComfyUI 中以下参数已**固定死，整轮不要动**。
以下为你的实际工作流配置（已从截图确认）：

| 参数 | 锁定值 |
|---|---|
| **大模型 (Checkpoint)** | `sd_xl_base_1.0.safetensors` |
| **采样器 (Sampler)** | `euler_ancestral` |
| **调度器 (Scheduler)** | `normal` |
| **步数 (Steps)** | `35` |
| **CFG Scale** | `8.0` |
| **分辨率 (Resolution)** | `1024 x 1536`（竖版） |
| **降噪 (Denoise)** | `1.00` |
| **Seed 控制** | `fixed`（生成后控制已锁定） |
| **全局负面词 (Negative)** | `(worst quality:1.4), (low quality:1.4), (normal quality:1.0), blurry, gradient shading, realistic, photorealistic, 3d render, smooth airbrush, no outlines, soft edges, anti-aliasing, text, watermark, signature, UI elements, oversaturated, HDR, lens flare, bloom, multiple light sources, complex shadows, deformed, extra limbs, bad anatomy` |
| **LoRA 模型** | `MarioTrickster_Style_epoch_10.safetensors` |

---

## 2. 文件命名方案（极简版）

**你只需要在每个题材开头改一次 `保存图像` 节点的文件名前缀，之后 ComfyUI 会自动递增编号。**

整轮只需改 **3 次** 前缀：

| 题材 | 前缀改为 | 该题材跑 10 张后自动生成 |
|---|---|---|
| S（静态资产） | `S` | `S_00001_.png` ~ `S_00010_.png` |
| A（角色动作） | `A` | `A_00001_.png` ~ `A_00010_.png` |
| C（横版场景） | `C` | `C_00001_.png` ~ `C_00010_.png` |

我这边通过下面的**出图顺序表**来对应每张图的参数，你只要**严格按顺序跑**就行，不用管文件名。

---

## 3. 逐张执行清单（共 30 张，严格按顺序跑）

### 题材 S：静态资产（地刺）

**开始前**：`保存图像` 节点文件名前缀改为 `S`，K采样器 Seed 填一个数字（如 `111111`），整个 S 题材不要动 Seed。

| 顺序 | 模式 | LoRA 操作 | 权重 | 正向 Prompt（直接复制） |
|---|---|---|---|---|
| 1 | B0 | **Bypass LoRA** | — | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` |
| 2 | B1 | 恢复 LoRA | `0.6` | （同上，不用改 Prompt） |
| 3 | B1 | 只改权重 | `0.8` | （同上） |
| 4 | B1 | 只改权重 | `1.0` | （同上） |
| 5 | B2 | 改权重 + 改 Prompt | `0.6` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background, trickster_style` |
| 6 | B2 | 只改权重 | `0.8` | （同上） |
| 7 | B2 | 只改权重 | `1.0` | （同上） |
| 8 | B3 | 改权重 + 改 Prompt | `0.6` | `trickster_style, 2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` |
| 9 | B3 | 只改权重 | `0.8` | （同上） |
| 10 | B3 | 只改权重 | `1.0` | （同上） |

> **实际操作量**：Prompt 只需要粘贴 3 次（B0/B1 共用一个、B2 一个、B3 一个），权重改 8 次数字，Bypass 操作 2 次（关一次、开一次）。

---

### 题材 A：角色动作（跳跃中帧）

**换题材**：`保存图像` 前缀改为 `A`，K采样器 Seed 换一个新数字（如 `222222`）。

| 顺序 | 模式 | LoRA 操作 | 权重 | 正向 Prompt（直接复制） |
|---|---|---|---|---|
| 1 | B0 | **Bypass LoRA** | — | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` |
| 2 | B1 | 恢复 LoRA | `0.6` | （同上，不用改 Prompt） |
| 3 | B1 | 只改权重 | `0.8` | （同上） |
| 4 | B1 | 只改权重 | `1.0` | （同上） |
| 5 | B2 | 改权重 + 改 Prompt | `0.6` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background, trickster_style` |
| 6 | B2 | 只改权重 | `0.8` | （同上） |
| 7 | B2 | 只改权重 | `1.0` | （同上） |
| 8 | B3 | 改权重 + 改 Prompt | `0.6` | `trickster_style, cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` |
| 9 | B3 | 只改权重 | `0.8` | （同上） |
| 10 | B3 | 只改权重 | `1.0` | （同上） |

---

### 题材 C：横版场景（一小段平台）

**换题材**：`保存图像` 前缀改为 `C`，K采样器 Seed 换一个新数字（如 `333333`）。

| 顺序 | 模式 | LoRA 操作 | 权重 | 正向 Prompt（直接复制） |
|---|---|---|---|---|
| 1 | B0 | **Bypass LoRA** | — | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` |
| 2 | B1 | 恢复 LoRA | `0.6` | （同上，不用改 Prompt） |
| 3 | B1 | 只改权重 | `0.8` | （同上） |
| 4 | B1 | 只改权重 | `1.0` | （同上） |
| 5 | B2 | 改权重 + 改 Prompt | `0.6` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background, trickster_style` |
| 6 | B2 | 只改权重 | `0.8` | （同上） |
| 7 | B2 | 只改权重 | `1.0` | （同上） |
| 8 | B3 | 改权重 + 改 Prompt | `0.6` | `trickster_style, 2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` |
| 9 | B3 | 只改权重 | `0.8` | （同上） |
| 10 | B3 | 只改权重 | `1.0` | （同上） |

---

## 4. 顺序号 → 参数对照表（我用来判定，你不用管）

以下是我收到你的图后用来对照的映射表。你只要保证**每个题材内严格按 1~10 的顺序跑**，我就能自动还原每张图的参数。

| 文件编号 | 模式 | LoRA 权重 | 触发词位置 |
|---|---|---|---|
| `_00001_` | B0 基线 | 关闭 | 无 |
| `_00002_` | B1 隐式 | 0.6 | 无 |
| `_00003_` | B1 隐式 | 0.8 | 无 |
| `_00004_` | B1 隐式 | 1.0 | 无 |
| `_00005_` | B2 弱触发 | 0.6 | 末尾 |
| `_00006_` | B2 弱触发 | 0.8 | 末尾 |
| `_00007_` | B2 弱触发 | 1.0 | 末尾 |
| `_00008_` | B3 强触发 | 0.6 | 开头 |
| `_00009_` | B3 强触发 | 0.8 | 开头 |
| `_00010_` | B3 强触发 | 1.0 | 开头 |

---

## 5. 跑完后如何回传

跑完 30 张图后，你只需要做两件事：

**① 发图片**：把 ComfyUI 的 `output` 文件夹里 S、A、C 开头的图片全部打包发给我。

**② 填 3 个数字**：

```text
Seed：S=[ ] / A=[ ] / C=[ ]
每个题材都严格按 1~10 顺序跑的：是
```

其余所有判定（最佳权重、表现评价、污染检测、去污词推荐）全部由我看图后完成，你不用管。
