# 🎴 工单 S81 — 基准画风抽卡（Style LoRA 素材采集）

> **目标**：用 3 张参考图在 ComfyUI 抽出 30 张画风一致的场景图，作为 Style LoRA 训练集。
> **画风定位**：吉卜力背景美术 × 法式BD漫画描边 × 游戏场景俯瞰图

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

## 二、ComfyUI 节点配置图纸

### 节点 1：CheckpointLoader

```
模型：animagine-xl-3.1.safetensors
     （备选：counterfeitXL_v25.safetensors / kohaku-xl-delta-v1.safetensors）
VAE：sdxl_vae.safetensors（内置即可）
```

> **选模理由**：参考图是日系动画背景风，animagine-xl-3.1 对动画场景的理解最强。如果你本地没有这个模型，用 counterfeitXL 或 kohaku-xl-delta 也可以。

---

### 节点 2：IPAdapter（垫图锁风格）

```
模型：ip-adapter-plus_sdxl_vit-h.safetensors
权重（weight）：0.65
权重类型：linear
起始步（start_at）：0.0
结束步（end_at）：0.85
```

> **输入图片**：3 张参考图轮流喂入。每张参考图跑 10 张 = 共 30 张。
> **配方库依据**：探索期垫图抽卡权重 `0.6-0.8`，取 0.65 偏保守，给 Prompt 留足发挥空间。

---

### 节点 3：CLIPTextEncode（正向提示词）

**通用画风锚定词（每张图都带，不要改）：**

```
masterpiece, best quality, anime background art, 
(thick black outlines:1.3), (hand-drawn ink outlines:1.2), 
(cel shading:1.2), (flat color fill:1.1), (limited color palette:1.1),
(simplified shadow 2-tone:1.2), (cool blue shadow:1.1),
warm natural color scheme, earthy tones, 
no gradient shading, no realistic rendering,
game asset background, 2D side-scroller environment
```

**场景变体词（每张图换一组，共 6 组 × 5 张 = 30 张）：**

| 变体 | 追加 Prompt | 对应游戏场景 |
|---|---|---|
| A-海滩俯瞰 | `top-down view, sandy beach, turquoise ocean waves, white foam lines, scattered rocks, coastal cliffs, green bushes on rocks, water transparency showing seabed` | World 1 海滩关 |
| B-海滩村落 | `3/4 top-down view, seaside village, wooden houses with tile roofs, rocky shoreline, clear shallow water, boulders in water, tropical vegetation` | World 1 村庄区 |
| C-田园小镇 | `one-point perspective road, rural village, red roof houses, green hills, cumulus clouds in blue sky, yellow car on road, European countryside` | World 2 田园关 |
| D-森林内部 | `dense forest interior, dappled sunlight on ground, layered tree canopy, moss-covered rocks, fallen logs, forest path, atmospheric depth` | World 3 森林关 |
| E-地下洞窟 | `underground cave, stalactites, glowing crystals, underground river, wet rock surface, dim torch light, cave entrance light beam` | World 4 洞窟关 |
| F-城堡废墟 | `ruined castle walls, overgrown ivy, broken stone stairs, sunset golden hour light, long shadows, crumbling tower, medieval architecture` | World 5 城堡关 |

---

### 节点 4：CLIPTextEncode（负向提示词）

```
(worst quality:1.4), (low quality:1.4), (normal quality:1.2),
blurry, gradient shading, realistic, photorealistic, 3D render,
smooth airbrush, no outlines, soft edges, anti-aliasing,
text, watermark, signature, UI elements,
oversaturated, HDR, lens flare, bloom,
multiple light sources, complex shadows,
deformed, extra limbs, bad anatomy
```

---

### 节点 5：KSampler

```
seed：随机（每张图不同，满意的记录下来）
steps：35
CFG：6.0
采样器（sampler）：euler_ancestral
调度器（scheduler）：normal
Denoising：1.0（纯文生图模式）
```

> **配方库依据**：AI参数抽屉 `steps 30-40 / CFG 5.0-7.0 / euler_ancestral + normal`

---

### 节点 6：EmptyLatentImage

```
宽度（width）：1024
高度（height）：1536
batch_size：1
```

> **配方库依据**：フレーム宽高比テーブル，竖版 2:3 比例适合横版游戏场景的纵深表现。
> 如果要做纯横版背景，改为 `1536 × 1024`（3:2 横版）。

---

## 三、连线顺序

```
CheckpointLoader ──→ KSampler (model)
                 ──→ CLIPTextEncode+ (clip)
                 ──→ CLIPTextEncode- (clip)

IPAdapter ──→ KSampler (model，接在 CheckpointLoader 之后)
  ↑ 输入图片：Load Image（3张参考图轮流换）

CLIPTextEncode+ ──→ KSampler (positive)
CLIPTextEncode- ──→ KSampler (negative)
EmptyLatentImage ──→ KSampler (latent_image)

KSampler ──→ VAEDecode ──→ SaveImage
```

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

**质检标准（每张图必须满足）：**

| 检查项 | 合格标准 | 不合格处理 |
|---|---|---|
| 描边 | 可见粗黑轮廓线 | 换 seed 重跑 |
| 色彩 | 平涂为主、无渐变过渡 | 提高 `cel shading` 权重到 1.4 |
| 阴影 | 最多 2 层、偏冷色 | 在负向加 `multiple shadow layers` |
| 透视 | 无明显崩坏 | 降低 IPAdapter 权重到 0.55 |
| 画风一致性 | 与参考图风格匹配度 ≥ 80% | 调整 IPAdapter 权重 ±0.05 |

---

## 五、满意图处理流程

1. 每张满意的图 → 记录 seed 到下方表格
2. 30 张凑齐后 → 保存到 `Assets/Art/Reference/LoRA_TrainingSet/`
3. 上传到 LiblibAI / Kohya 训练 Style LoRA
4. 训练参数（预设）：`network_dim=32, network_alpha=16, lr=1e-4, epochs=10-15`
5. 训好的 LoRA → 保存到 ComfyUI `models/loras/` 目录

**Seed 记录表：**

| 编号 | 批次 | 场景 | Seed | 满意度 | 备注 |
|---|---|---|---|---|---|
| 01 | 1 | A-海滩 | __________ | ⭐⭐⭐⭐⭐ | |
| 02 | 1 | A-海滩 | __________ | ⭐⭐⭐⭐⭐ | |
| ... | ... | ... | ... | ... | ... |
| 30 | 6 | F-城堡 | __________ | ⭐⭐⭐⭐⭐ | |

---

## 六、故障排除

| 问题 | 原因 | 解决方案 |
|---|---|---|
| 出图没有描边 | 模型不擅长描边 | 提高 `thick black outlines` 到 1.5；或换 counterfeitXL |
| 色彩太写实 | CFG 太低或模型偏写实 | CFG 提到 7.0；加强 `flat color fill` 到 1.3 |
| 画面太糊 | steps 不够 | steps 提到 40 |
| IPAdapter 太强压制 Prompt | 权重太高 | 降到 0.50-0.55 |
| 场景内容与 Prompt 不符 | IPAdapter 覆盖了场景描述 | 降低 `end_at` 到 0.70 |
