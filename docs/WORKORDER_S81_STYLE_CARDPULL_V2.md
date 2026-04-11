# 🎴 工单 S81 (V2 修正版) — 基准画风抽卡（Style LoRA 素材采集）

> **目标**：用 3 张参考图在 ComfyUI 抽出 30 张画风一致的场景图，作为 Style LoRA 训练集。
> **画风定位**：吉卜力背景美术 × 法式BD漫画描边 × 游戏场景俯瞰图
> **版本说明**：本 V2 版本已修复旧版工单中节点缺失、参数名不匹配中文 UI、连线描述抽象等问题，完全适配 RTX 4070 用户的实际 ComfyUI 环境。

---

## 一、画风 DNA 解码（从参考图提取）

| 维度 | 特征值 | 对应配方库规则 |
|---|---|---|
| 线稿 | 粗黑描边(1-2px)、外粗内细、干净单线 | 線の質感表現法則（テレコム） |
| 色彩 | 有限调色板≤15色、大面积平涂、自然系暖色调 | 配色比率法則（吉田）`base_70_accent_5` |
| 光影 | 1-2层简化阴影、阴影偏冷蓝、无高光过曝 | 直接光2色塗り分け法則（吉田）+ anime_cel_hard_edge_flat_color |
| 材质-水 | 半透明青绿+白泡沫线+波纹+可见水底 | 水3要素描写法則（吉田）`fresnel_effect` |
| 材质-岩 | 棕色平涂+深色裂缝线+苔藓 | 岩石材質描写法則（吉田） |
| 材质-植被 | 团状色块+深绿阴影、不画单叶 | 森描写レイヤー法則（吉田） |
| 透视 | 俯瞰/3/4俯瞰/低角度一点透视 | 空間ビート法則（室井） |
| 空气感 | 远景饱和度降低、轻微白雾 | 6種空気遠近法（吉田）`screen_overlay` |

---

## 二、ComfyUI 节点配置图纸（精确到中文 UI 端口）

请在 ComfyUI 画布空白处双击，搜索并拉出以下 **9 个节点**（每个编号对应一个独立节点，必须单独拉出）。不要遗漏任何一个。

### 1. 基础大模型加载
**节点名**：`Checkpoint加载器（简易）` (CheckpointLoaderSimple)
- **模型**：选择 `sd_xl_base_1.0.safetensors`（你本地已有的模型）

### 2. IPAdapter 模型加载器（旧工单漏掉的关键前置）
**节点名**：`IPAdapter Unified Loader` (IPAdapter 统一加载器)
- **preset**：将默认的 `STANDARD (medium strength)` 改为 `PLUS (high strength)`
  - *为什么选 PLUS：因为你本地安装的是 `ip-adapter-plus_sdxl_vit-h.safetensors`，STANDARD 会找 `ip-adapter_sdxl_vit-h.safetensors`（你没有这个文件，会报错）。*
- 其他保持默认即可。
- *说明：这个节点会自动帮你加载配套的 CLIP Vision 和 IPAdapter 模型，省去手动连线的麻烦。*

### 3. 参考图加载（独立节点，不属于 IPAdapter）
**节点名**：`加载图像` (Load Image)
- **image**：点击 `choose file to upload`（或 `选择文件上传`），上传你的第一张参考图。
- *说明：这是一个通用的图片加载节点，和 IPAdapter 是两个完全独立的节点。你需要单独拉出来，然后用连线把它的输出口接到 IPAdapter 的 image 输入口。*

### 4. IPAdapter 应用节点
**节点名**：`IPAdapter` (IPAdapter Advanced)
- **weight (权重)**：将默认的 `1.00` 改为 `0.65`
- **weight_type (权重类型)**：将默认的 `standard` 改为 `linear`
- **start_at (起始步)**：保持 `0.000`
- **end_at (结束步)**：将默认的 `1.000` 改为 `0.850`

### 5. 正面提示词
**节点名**：`CLIP文本编码` (CLIPTextEncode) —— 拉出第一个，作为 **正面条件**
- **文本框填入**：
  ```text
  masterpiece, best quality, anime background art, 
  (thick black outlines:1.3), (hand-drawn ink outlines:1.2), 
  (cel shading:1.2), (flat color fill:1.1), (limited color palette:1.1),
  (simplified shadow 2-tone:1.2), (cool blue shadow:1.1),
  warm natural color scheme, earthy tones, 
  no gradient shading, no realistic rendering,
  game asset background, 2D side-scroller environment,
  top-down view, sandy beach, turquoise ocean waves, white foam lines, scattered rocks, coastal cliffs, green bushes on rocks, water transparency showing seabed
  ```

### 5b. 负面提示词
**节点名**：`CLIP文本编码` (CLIPTextEncode) —— 拉出第二个，作为 **负面条件**
- *说明：和第 5 步是同一种节点，但你需要拉出两个，一个填正向词、一个填反向词，分别接到 K采样器的不同端口。*
- **文本框填入**：
  ```text
  (worst quality:1.4), (low quality:1.4), (normal quality:1.2),
  blurry, gradient shading, realistic, photorealistic, 3D render,
  smooth airbrush, no outlines, soft edges, anti-aliasing,
  text, watermark, signature, UI elements,
  oversaturated, HDR, lens flare, bloom,
  multiple light sources, complex shadows,
  deformed, extra limbs, bad anatomy
  ```

### 6. 画幅设置（旧工单漏掉的关键节点）
**节点名**：`空Latent图像` (EmptyLatentImage)
- **宽度 (width)**：改为 `1024`
- **高度 (height)**：改为 `1536`
- **批次大小 (batch_size)**：保持 `1`

### 7. 核心采样器
**节点名**：`K采样器` (KSampler)
- **种子 (seed)**：保持随机（如果出好图，在控制台看一眼种子记下来）
- **生成后控制 (control_after_generate)**：保持 `randomize`
- **步数 (steps)**：将默认的 `20` 改为 `35`
- **cfg**：将默认的 `8.0` 改为 `6.0`
- **采样器名称 (sampler_name)**：将默认的 `euler` 改为 `euler_ancestral`
- **调度器 (scheduler)**：将默认的 `simple` 改为 `normal`
- **降噪 (denoise)**：保持 `1.00`

### 8. 解码
**节点名**：`VAE解码` (VAEDecode)
- 保持默认即可。

### 9. 保存图像
**节点名**：`保存图像` (SaveImage)
- **文件名前缀 (filename_prefix)**：建议改为 `S81_style_` 方便后续筛选。

---

## 三、傻瓜式连线指南（从左到右）

请严格按照以下“端口对端口”的说明拖拽连线：

| 起点节点 | 起点端口 | 终点节点 | 终点端口 |
|---|---|---|---|
| Checkpoint加载器 | **模型 (MODEL)** | IPAdapter 统一加载器 | **model** |
| Checkpoint加载器 | **CLIP** | CLIP文本编码 (正面) | **clip** |
| Checkpoint加载器 | **CLIP** | CLIP文本编码 (负面) | **clip** |
| Checkpoint加载器 | **VAE** | VAE解码 | **vae** |
| IPAdapter 统一加载器 | **model** | IPAdapter | **model** |
| IPAdapter 统一加载器 | **ipadapter** | IPAdapter | **ipadapter** |
| 加载图像 | **图像 (IMAGE)** | IPAdapter | **image** |
| IPAdapter | **MODEL** | K采样器 | **模型 (model)** |
| CLIP文本编码 (正面) | **条件 (CONDITIONING)** | K采样器 | **正面条件 (positive)** |
| CLIP文本编码 (负面) | **条件 (CONDITIONING)** | K采样器 | **负面条件 (negative)** |
| 空Latent图像 | **LATENT** | K采样器 | **Latent图像 (latent_image)** |
| K采样器 | **LATENT** | VAE解码 | **samples** |
| VAE解码 | **图像 (IMAGE)** | 保存图像 | **images** |

---

## 四、抽卡策略（30 张采集计划）

| 批次 | 参考图 | 场景变体 | 张数 | 操作 |
|---|---|---|---|---|
| 1 | ref_beach_topdown.webp | A-海滩俯瞰 | 5张 | 换 seed 跑 5 次 |
| 2 | ref_beach_topdown.webp | D-森林内部 | 5张 | 换 seed 跑 5 次 |
| 3 | ref_beach_village_3quarter.png | B-海滩村落 | 5张 | 换 seed 跑 5 次 |
| 4 | ref_beach_village_3quarter.png | E-地下洞窟 | 5张 | 换 seed 跑 5 次 |
| 5 | ref_village_road_perspective.webp | C-田园小镇 | 5张 | 换 seed 跑 5 次 |
| 6 | ref_village_road_perspective.webp | F-城堡废墟 | 5张 | 换 seed 跑 5 次 |

*(注：场景变体词已在上述正面提示词中提供 A 组示例，后续批次请替换最后一段场景描述词)*

---

## 五、故障排除（如果图跑出来不对）

| 现象 | 应该调哪个节点的哪个参数 |
|---|---|
| **出图没有描边** | 提高正面提示词中 `(thick black outlines:1.3)` 的权重到 1.5 |
| **色彩太写实/像照片** | K采样器：把 `cfg` 提高到 7.0；正面提示词：加强 `(flat color fill:1.1)` 到 1.3 |
| **画面太糊/有噪点** | K采样器：把 `步数 (steps)` 提高到 40 |
| **参考图的风格太强，压制了提示词** | IPAdapter：把 `weight` 降到 0.50-0.55 |
| **场景内容变成了参考图里的东西** | IPAdapter：把 `end_at` 降低到 0.70（让 AI 在最后 30% 步数自由发挥） |
