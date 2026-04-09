# MarioTrickster 提示词配方库 (PROMPT_RECIPES)

> **Validated Prompt Blueprints**
> 本文档记录了已在项目中跑通、符合 `ART_BIBLE.md` 规范的出图配方。

---

## 🏃 角色类 (Entities)

### 1.1 主角 Mario (Modern Pixel Style)
- **资产名称**: `Mario_Idle_Run`
- **目标帧数**: 8 帧 (Run) / 4 帧 (Idle)
- **重心**: Bottom Center (0.5, 0)
- **提示词 (Prompt)**:
  > `Mario character, 2D platformer sprite sheet, pixel art, 32x32 base, running animation, 8 frames, side view, red cap, blue overalls, flat green background, isolated, sharp edges, high contrast --no shading gradients`
- **技术参数**: PPU=32, Filter=Point, Compression=None.

### 1.2 敌人 Trickster (Ghostly Form)
- **资产名称**: `Trickster_Float`
- **目标帧数**: 6 帧 (Floating)
- **重心**: Center (0.5, 0.5)
- **提示词 (Prompt)**:
  > `Ghostly trickster character, 2D platformer sprite, pixel art, floating animation, 6 frames, purple ethereal glow, semi-transparent, flat black background, isolated, sharp pixel edges --no blur`

---

## 🧱 地形与环境 (Environment)

### 2.1 经典砖块 (Classic Block)
- **资产名称**: `Ground_Block_A`
- **适配模式**: Tiled
- **提示词 (Prompt)**:
  > `2D platformer ground block, pixel art, 32x32, stone texture, mossy, seamless tileable, flat background, isolated, sharp edges`

### 2.2 危险地刺 (Spike Trap)
- **资产名称**: `Spike_Trap_Metal`
- **重心**: Bottom Center (0.5, 0)
- **提示词 (Prompt)**:
  > `Sharp metal spikes, 2D platformer hazard, pixel art, 32x32, rusty metal, flat green background, isolated, sharp edges`

---

## ✨ 特效与 UI (VFX & UI)

### 3.1 扫描脉冲 (Scan Pulse)
- **资产名称**: `VFX_Scan_Pulse`
- **重心**: Center (0.5, 0.5)
- **提示词 (Prompt)**:
  > `Circular energy pulse, 2D game VFX, pixel art, expanding ring, cyan glow, 8 frames, flat black background, isolated, sharp edges`

---

## 🛠️ 维护指南 (Maintenance)
1. **出图成功后**: 将 Prompt、帧数、重心设置追加到本文件。
2. **Commit Msg**: `feat(recipes): add [Asset Name] blueprint`
3. **出图失败排查**: 检查是否包含 `--no shading gradients` 后缀，确保背景为纯色。

---
*Last Updated: 2026-04-09 by Manus TA*
