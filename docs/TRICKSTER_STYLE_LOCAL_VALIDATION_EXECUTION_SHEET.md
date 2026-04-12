# `trickster_style` 本地验证执行速查表

> **说明**：这是基于 `TRICKSTER_STYLE_LOCAL_VALIDATION_WORK_ORDER_2026-04-12.md` 展开的 30 张逐张执行清单。你可以直接复制这里的 Prompt 到 ComfyUI 中跑图，跑完按推荐的文件名保存，最后把结果回传给我。

---

## 1. 必须锁定的全局设置

在开始跑这 30 张图之前，请在 ComfyUI 中把以下参数**固定死，整轮不要动**：

| 参数 | 推荐默认值（如果你没有偏好） | 你的实际值（请记录） |
|---|---|---|
| **大模型 (Checkpoint)** | `sd_xl_base_1.0.safetensors` 或你常用的二次元/游戏底模 | |
| **采样器 (Sampler)** | `euler_ancestral` 或 `dpmpp_2m` | |
| **调度器 (Scheduler)** | `karras` | |
| **步数 (Steps)** | `30` | |
| **CFG Scale** | `7.0` | |
| **分辨率 (Resolution)** | `1024 x 1024` | |
| **全局负面词 (Negative)** | `photorealistic, realistic shading, 3d render, blurry, muddy colors, messy lines, text, watermark, logo, cropped, low contrast, noisy details` | |
| **LoRA 模型** | `MarioTrickster_Style_epoch_10.safetensors` | |

---

## 2. 逐张执行清单 (共 30 张)

### 题材 S：静态资产（地刺）
**固定 Seed**：请随意填一个数字（如 `111111`），这 10 张图**必须用同一个 Seed**。

| 编号 | 模式 | LoRA 权重 | 正向 Prompt (直接复制) | 预期保存文件名 |
|---|---|---|---|---|
| 1 | **B0 (基线)** | `0` (关闭) | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B0_w00_seed[你的seed].png` |
| 2 | **B1 (隐式)** | `0.6` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B1_w06_seed[你的seed].png` |
| 3 | **B1 (隐式)** | `0.8` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B1_w08_seed[你的seed].png` |
| 4 | **B1 (隐式)** | `1.0` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B1_w10_seed[你的seed].png` |
| 5 | **B2 (弱触发)** | `0.6` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background, trickster_style` | `TSVAL_S_B2_w06_seed[你的seed].png` |
| 6 | **B2 (弱触发)** | `0.8` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background, trickster_style` | `TSVAL_S_B2_w08_seed[你的seed].png` |
| 7 | **B2 (弱触发)** | `1.0` | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background, trickster_style` | `TSVAL_S_B2_w10_seed[你的seed].png` |
| 8 | **B3 (强触发)** | `0.6` | `trickster_style, 2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B3_w06_seed[你的seed].png` |
| 9 | **B3 (强触发)** | `0.8` | `trickster_style, 2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B3_w08_seed[你的seed].png` |
| 10 | **B3 (强触发)** | `1.0` | `trickster_style, 2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` | `TSVAL_S_B3_w10_seed[你的seed].png` |

---

### 题材 A：角色动作（跳跃中帧）
**固定 Seed**：请换一个新数字（如 `222222`），这 10 张图**必须用同一个 Seed**。

| 编号 | 模式 | LoRA 权重 | 正向 Prompt (直接复制) | 预期保存文件名 |
|---|---|---|---|---|
| 11 | **B0 (基线)** | `0` (关闭) | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B0_w00_seed[你的seed].png` |
| 12 | **B1 (隐式)** | `0.6` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B1_w06_seed[你的seed].png` |
| 13 | **B1 (隐式)** | `0.8` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B1_w08_seed[你的seed].png` |
| 14 | **B1 (隐式)** | `1.0` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B1_w10_seed[你的seed].png` |
| 15 | **B2 (弱触发)** | `0.6` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background, trickster_style` | `TSVAL_A_B2_w06_seed[你的seed].png` |
| 16 | **B2 (弱触发)** | `0.8` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background, trickster_style` | `TSVAL_A_B2_w08_seed[你的seed].png` |
| 17 | **B2 (弱触发)** | `1.0` | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background, trickster_style` | `TSVAL_A_B2_w10_seed[你的seed].png` |
| 18 | **B3 (强触发)** | `0.6` | `trickster_style, cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B3_w06_seed[你的seed].png` |
| 19 | **B3 (强触发)** | `0.8` | `trickster_style, cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B3_w08_seed[你的seed].png` |
| 20 | **B3 (强触发)** | `1.0` | `trickster_style, cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` | `TSVAL_A_B3_w10_seed[你的seed].png` |

---

### 题材 C：横版场景（一小段平台）
**固定 Seed**：请再换一个新数字（如 `333333`），这 10 张图**必须用同一个 Seed**。

| 编号 | 模式 | LoRA 权重 | 正向 Prompt (直接复制) | 预期保存文件名 |
|---|---|---|---|---|
| 21 | **B0 (基线)** | `0` (关闭) | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B0_w00_seed[你的seed].png` |
| 22 | **B1 (隐式)** | `0.6` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B1_w06_seed[你的seed].png` |
| 23 | **B1 (隐式)** | `0.8` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B1_w08_seed[你的seed].png` |
| 24 | **B1 (隐式)** | `1.0` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B1_w10_seed[你的seed].png` |
| 25 | **B2 (弱触发)** | `0.6` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background, trickster_style` | `TSVAL_C_B2_w06_seed[你的seed].png` |
| 26 | **B2 (弱触发)** | `0.8` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background, trickster_style` | `TSVAL_C_B2_w08_seed[你的seed].png` |
| 27 | **B2 (弱触发)** | `1.0` | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background, trickster_style` | `TSVAL_C_B2_w10_seed[你的seed].png` |
| 28 | **B3 (强触发)** | `0.6` | `trickster_style, 2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B3_w06_seed[你的seed].png` |
| 29 | **B3 (强触发)** | `0.8` | `trickster_style, 2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B3_w08_seed[你的seed].png` |
| 30 | **B3 (强触发)** | `1.0` | `trickster_style, 2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` | `TSVAL_C_B3_w10_seed[你的seed].png` |

---

## 3. 跑完后如何回传

你跑完这 30 张图后，请自己先看一眼，然后**直接复制下面这段话，填好发给我**：

```text
我已经按 `trickster_style` 本地验证工单跑完了。
底模：[你的底模]
采样器：[你的采样器]
Steps / CFG：[你的参数]
Seed：S=[ ] / A=[ ] / C=[ ]
我跑的是：30 张标准版
目前结论：
1. 最佳权重：[0.6 / 0.8 / 1.0]
2. 静态资产表现：[简述，例如：0.8最稳，1.0线稿有点糊]
3. 角色动作表现：[简述，例如：0.8能看，但没加ControlNet肢体有点飘]
4. 横版场景表现：[简述，例如：0.8很好，但1.0出现了海滩]
5. 是否出现污染：[有 / 无；具体是什么，比如红屋顶、沙滩]
6. 我怀疑的专属去污词：[比如 beach, red roof]
```

收到你的回传后，我会立刻帮你把结论落库，并判断是否可以直接进入首批资产的量产。
