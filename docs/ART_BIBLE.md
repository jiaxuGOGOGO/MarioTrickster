# MarioTrickster 美术宪法 (ART_BIBLE)

> **Single Source of Truth for Art Assets**
> 本文档定义了项目美术素材的绝对规范。所有 AI 出图、素材导入、切片逻辑必须严格遵守此规范，以确保“视碰分离”架构下的物理一致性。

---

## 🏛️ 第一区：物理基建与度量衡

### 1.1 核心度量 (Physics Truth)
项目采用 **1 Unit = 1 Grid Cell** 的标准。
- **CELL_SIZE**: 1.0 (世界单位)
- **STANDARD_PPU**: 32 (Pixels Per Unit)
- **素材分辨率**: 
  - 单格地形: 32x32 px
  - 角色/敌人: 建议 48x48 px 或 64x64 px (预留动作挥砍空间)，但逻辑中心必须对齐 32x32 基准。

### 1.2 视碰分离 (Visual-Collision Decoupling)
- **碰撞体 (Collision)**: 物理真相，由 `PhysicsMetrics.cs` 锁定。
- **视觉 (Visual)**: 纯装饰，通过 `Visual` 子物体的 `localScale` 适配。
- **黄金法则**: 碰撞体必须略小于视觉 Sprite (宽容度设计)，防止贴墙卡顿和头部擦顶。

---

## 🎨 第二区：画风与视觉规范

### 2.1 目标画风 (Art Style)
- **风格**: 高质量 2D 像素艺术 (High-quality 2D Pixel Art)。
- **色调**: 鲜明、对比度高，区分背景与前景。
- **背景**: 出图必须强制 **纯色背景** (如 #00FF00 绿幕或纯黑)，严禁渐变，防抠图毛刺。

### 2.2 资产分类规范
| 资产类型 | 重心 (Pivot) | 适配模式 (FitMode) | 物理特性 |
| :--- | :--- | :--- | :--- |
| **地形 (Ground/Wall)** | Center (0.5, 0.5) | **Tiled** | 1x1 矩形，无缝拼接 |
| **角色/敌人 (Entity)** | **Bottom Center (0.5, 0)** | **Scaled** | 防滑步锁死，重心在脚底 |
| **UI/边框平台** | Center (0.5, 0.5) | **SlicedNineSlice** | 九宫格拉伸，保持边框不形变 |
| **特效 (VFX)** | Center (0.5, 0.5) | **无 (不挂载 SpriteAutoFit)** | 居中对齐，不考虑地面摩擦 |
| **道具 (Prop)** | Bottom Center (0.5, 0) | **Scaled** | 放置感，与地面贴合 |

---

## 🛠️ 第三区：AI 出图与管线红线

### 3.1 提示词万能公式 (The Master Formula)
所有提示词必须包含以下“防崩坏”后缀：
`[Subject], 2D side-scrolling game sprite, pixel art, flat color background, isolated, full body, consistent lighting, high contrast, sharp edges`

*(注：如果使用 Midjourney，请在末尾加上 `--no shading gradients, --no blur`；如果使用 ComfyUI，请将这些词放入负向提示词节点。)*

### 3.2 ComfyUI 出图参数分轨声明 (防噪点雷区)
- **加速模型专属轨** (LCM/Lightning/Turbo): `Steps: 6-8`, `CFG: 1.5-2.0`
- **标准模型专属轨** (常规 SDXL): `Steps: 25-30`, `CFG: 5.0-7.0`

### 3.3 ControlNet 模具分轨 (防骨折雷区)
- **非生物/硬表面实体** (地形、陷阱等): 使用 **Lineart + Canny** 组合。
- **生物关节角色** (Mario、Trickster、Enemy 等连续动作): **必须强制加入 DWPose / OpenPose 模具！** 仅靠线稿无法锁死人物重心的动态偏移。

### 3.4 动作帧数标准
- **Idle (待机)**: 4-6 帧
- **Run (跑步)**: 8 帧 (必须符合 PPU 步幅，防止滑步)
- **Jump (跳跃)**: 3 阶段 (起跳、最高点、下落)
- **Attack (攻击)**: 5-8 帧 (含前摇与收招)

### 3.5 自动化切片红线
- **禁止手动切片**: 必须使用 `AI_SpriteSlicer.cs` 脚本。
- **重心死锁**: 角色类资产切片时，Pivot 强制设为 `Bottom Center`。
- **像素完美**: Filter Mode 必须设为 `Point (no filter)`。

---

## 🔄 第四区：维护与增量合并 (GitOps)

1. **Fetch**: 每次出图前，先 `git pull` 获取最新宪法。
2. **Diff**: 喂新书/新参考图给 AI 时，若规则冲突，以“最防滑步”为准。
3. **Push**: 成功的出图配方必须追加到 `PROMPT_RECIPES.md`。

---
*Last Updated: 2026-04-09 by Manus TA*
