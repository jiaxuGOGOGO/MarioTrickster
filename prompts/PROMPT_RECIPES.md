# MarioTrickster 提示词配方库 (PROMPT_RECIPES)

> **Validated Prompt Blueprints for All Entities**
> 本文档记录了基于项目规范设计的推荐出图配方（待实际验证后标记为 ✅）。它涵盖了项目当前支持的所有 20+ 种实体类型，并提供了针对主流 AI 工具（Midjourney、ComfyUI）的具体参数建议。

---

## 🛠️ AI 工具通用参数设置

### 1. Midjourney (v6/v7) 最佳实践
在使用 Midjourney 生成像素艺术时，除了核心提示词外，必须附加以下参数以确保风格一致性和可用性：
- **宽高比 (`--ar`)**: 对于单帧素材使用 `--ar 1:1`。对于序列帧（Sprite Sheet），根据帧数使用 `--ar 4:1` (4帧) 或 `--ar 8:1` (8帧)。
- **风格化 (`--s`)**: 像素艺术需要较低的风格化以保持边缘锐利。建议设置 `--s 50` 到 `--s 100`。
- **混乱度 (`--c`)**: 为了保持角色在不同动作间的一致性，使用较低的混乱度 `--c 0` 到 `--c 10`。
- **平铺 (`--tile`)**: 仅在生成地形（Ground、Wall）等需要无缝拼接的素材时使用。
- **负面提示 (`--no`)**: 必须包含 `--no blur, gradient, shading, 3d, realistic, text, watermark`。

### 2. ComfyUI (SDXL + Pixel Art LoRA) 最佳实践
基于社区验证的 ComfyUI 工作流，推荐以下节点配置：
- **大模型**: `sd_xl_base_1.0.safetensors`
- **LoRA**: `pixel-art-xl-v1.1.safetensors` (强度: Model 1.2, CLIP 1.0)
- **采样器 (KSampler)**: Steps: 8, CFG: 1.5, Sampler: `lcm`, Scheduler: `normal`
- **正向提示词后缀**: `(flat shading:1.2), (minimalist:1.4)`
- **负向提示词**: `text, watermark, blurry, deformed, depth of field, realistic, 3d render, frame`
- **后处理**: 生成 512x512 图像后，使用 ImageMagick 的 `nearest-neighbor` 算法缩放至目标尺寸（如 32x32 或 64x64），以保持像素完美。

---

## 🏃 角色与敌人 (Entities)

所有角色和敌人必须遵循 **Bottom Center (0.5, 0)** 的重心规范，以防止滑步。

| 实体名称 | 资产类型 | 目标帧数 | 提示词配方 (Prompt Recipe) |
| :--- | :--- | :--- | :--- |
| **Mario** (主角) | Scaled | 8 (Run), 4 (Idle) | `Mario-like character, 2D platformer sprite sheet, pixel art, running animation sequence, side view, red cap, blue overalls, flat green background, isolated, sharp edges, high contrast --ar 8:1 --s 50 --no blur, gradient` |
| **Trickster** (幽灵形态) | Scaled | 6 (Float) | `Ghostly trickster character, 2D platformer sprite sheet, pixel art, floating animation, purple ethereal glow, semi-transparent body, flat black background, isolated, sharp pixel edges --ar 6:1 --s 80 --no blur, realistic` |
| **SimpleEnemy** (基础巡逻怪) | Scaled | 4 (Walk) | `Cute slime monster, 2D platformer enemy sprite sheet, pixel art, walking animation, side view, green color, flat pink background, isolated, sharp edges --ar 4:1 --s 50 --no blur, gradient` |
| **BouncingEnemy** (弹跳怪) | Scaled | 3 (Jump) | `Spring-loaded robot enemy, 2D platformer sprite sheet, pixel art, jumping animation (squat, extend, fall), side view, metallic texture, flat blue background, isolated --ar 3:1 --s 60 --no blur, realistic` |
| **FlyingEnemy** (飞行怪) | Scaled | 4 (Fly) | `Bat creature, 2D platformer enemy sprite sheet, pixel art, flying animation, flapping wings, side view, dark purple, flat yellow background, isolated --ar 4:1 --s 50 --no blur, gradient` |

---

## 🧱 地形与平台 (Environment & Platforms)

地形元素通常需要无缝拼接，必须遵循 **Center (0.5, 0.5)** 重心和 **Tiled** 适配模式。

| 实体名称 | 资产类型 | 适配模式 | 提示词配方 (Prompt Recipe) |
| :--- | :--- | :--- | :--- |
| **Ground / Wall** (基础地形) | Tiled | Tiled | `2D platformer ground block, pixel art, 32x32, stone texture, mossy, seamless tileable, flat background, isolated, sharp edges --tile --s 50 --no blur, gradient` |
| **Platform** (普通平台) | Tiled | Tiled | `2D platformer floating platform block, pixel art, 32x32, wooden planks texture, seamless tileable horizontally, flat background, isolated --tile --s 50 --no blur` |
| **OneWayPlatform** (单向平台) | Tiled | Tiled | `2D platformer thin one-way platform, pixel art, 32x8, metal grating texture, seamless tileable horizontally, flat background, isolated --s 50 --no blur` |
| **BouncyPlatform** (弹跳平台) | Scaled | Scaled | `2D platformer bouncy mushroom platform, pixel art, 32x32, rubbery texture, flat background, isolated, sharp edges --s 60 --no blur` |
| **CollapsingPlatform** (崩塌平台) | Scaled | Scaled | `2D platformer cracked stone platform, pixel art, 32x32, crumbling texture, flat background, isolated, sharp edges --s 50 --no blur` |
| **MovingPlatform** (移动平台) | Scaled | Scaled | `2D platformer mechanical moving platform, pixel art, 80x13, metallic texture with yellow caution stripes, flat background, isolated --s 60 --no blur` |
| **ConveyorBelt** (传送带) | Tiled | Tiled | `2D platformer conveyor belt segment, pixel art, 32x16, industrial mechanical texture, seamless tileable horizontally, flat background, isolated --tile --s 50 --no blur` |
| **BreakableBlock** (可破坏方块) | Tiled | Tiled | `2D platformer fragile brick block, pixel art, 32x32, cracked terracotta texture, flat background, isolated, sharp edges --s 50 --no blur` |
| **FakeWall** (伪装墙) | Tiled | Tiled | `2D platformer illusion wall block, pixel art, 32x32, slightly faded stone texture, seamless tileable, flat background, isolated --tile --s 50 --no blur` |

---

## ⚠️ 陷阱与机关 (Hazards & Mechanics)

陷阱的碰撞体通常小于视觉尺寸，以提供玩家容错空间。

| 实体名称 | 资产类型 | 重心 | 提示词配方 (Prompt Recipe) |
| :--- | :--- | :--- | :--- |
| **SpikeTrap** (地刺) | Scaled | Bottom Center | `Sharp metal spikes, 2D platformer hazard, pixel art, 32x32, rusty metal, pointing upwards, flat green background, isolated, sharp edges --s 50 --no blur` |
| **FireTrap** (火焰陷阱) | Scaled | Bottom Center | `Roaring campfire flame, 2D platformer hazard sprite sheet, pixel art, 4 frames animation, bright orange and yellow, flat black background, isolated --ar 4:1 --s 80 --no blur` |
| **PendulumTrap** (摆锤) | Scaled | Center | `Heavy spiked iron ball, 2D platformer hazard, pixel art, 32x32, dark metal texture, flat white background, isolated, sharp edges --s 60 --no blur` |
| **SawBlade** (旋转锯片) | Scaled | Center | `Circular mechanical saw blade, 2D platformer hazard, pixel art, 32x32, spinning motion blur effect, silver metal, flat green background, isolated --s 70 --no blur` |

---

## ✨ 交互物与 UI (Interactables & UI)

| 实体名称 | 资产类型 | 重心 | 提示词配方 (Prompt Recipe) |
| :--- | :--- | :--- | :--- |
| **Collectible** (收集物/金币) | Scaled | Center | `Shiny gold coin, 2D platformer collectible sprite sheet, pixel art, 4 frames spinning animation, bright yellow, flat black background, isolated --ar 4:1 --s 60 --no blur` |
| **Checkpoint** (检查点/旗帜) | Scaled | Bottom Center | `Red checkpoint flag on a pole, 2D platformer object sprite sheet, pixel art, 4 frames waving animation, flat blue background, isolated --ar 4:1 --s 50 --no blur` |
| **GoalZone** (终点门) | Scaled | Bottom Center | `Ornate wooden door, 2D platformer level exit, pixel art, 32x64, glowing magical aura, flat green background, isolated, sharp edges --s 80 --no blur` |
| **HiddenPassage** (隐藏通道入口) | Scaled | Bottom Center | `Dark mysterious cave entrance, 2D platformer object, pixel art, 32x32, stone archway, flat pink background, isolated --s 60 --no blur` |

---
*Last Updated: 2026-04-09 by Manus TA*
