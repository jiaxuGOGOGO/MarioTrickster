# MarioTrickster 提示词与节点配方库 (PROMPT_RECIPES)

> **⚠️ 工业化管线 V3.0 专属核心知识库**
> 本文档是 Manus TA 的"大脑"。所有通过【菜单 1：喂书蒸馏】学到的知识，都将严格按以下「5 个知识抽屉」归档。下发工单时，Manus 将从这里抓取参数并分发给 ComfyUI 的对应节点。

---

## 🗄️ 抽屉 1：🧍‍♂️ [解剖与形态]
*(记录：头身比、防断肢、多角度姿势、角色特征)*

- **Mario 主角基准**：`(red cap:1.2), (blue overalls:1.2), side view, 2D platformer character`
- **Trickster 幽灵基准**：`(semi-transparent body:1.3), floating, purple ethereal glow`
- **通用防畸变负面 (Text Prompt)**：`deformed, extra limbs, bad anatomy, missing fingers, text, watermark`

---

## 🗄️ 抽屉 2：📐 [透视与物件]
*(记录：等距视角、静物结构、平台拼接规律)*

- **横版跳跃视角锁定 (Text Prompt)**：`side-scrolling platformer, side view`
- **防视角偏移负面 (Text Prompt)**：`isometric, top-down view, 3d render, perspective`
- **地形无缝拼接规则**：
  - 基础地面 (Ground)：必须为 `32x32`，生成时需附带 `--tile` (Midjourney) 或使用 ComfyUI 的 Seamless 节点。
  - 平台 (Platform)：必须水平方向无缝拼接。
- **物件结构模具 (ControlNet)**：
  - 静态地形、陷阱、交互物：**强制启用 `Lineart` 或 `Canny` 预处理器**，锁定边缘轮廓。

---

## 🗄️ 抽屉 3：🏃 [动画与物理]
*(记录：关键帧数、运动模糊、防滑步约束)*

- **通用防滑步原则**：
  - 所有角色/敌人：切图时重心必须锁死在 **Bottom Center (0.5, 0)**。
  - 所有地形/陷阱：切图时重心通常为 **Center (0.5, 0.5)**。
- **动画结构模具 (ControlNet)**：
  - 所有带连续动作的生物关节角色（跑、跳、受击）：**强制启用 `DWPose` / `OpenPose` 预处理器**！仅靠线稿无法锁死人物重心的动态偏移，会导致帧间骨折或滑步。
- **基准帧数**：
  - 跑步 (Run)：8 帧
  - 待机 (Idle) / 行走 (Walk) / 飞行 (Fly)：4 帧
  - 跳跃 (Jump)：3 帧 (起跳、伸展、下落)

---

## 🗄️ 抽屉 4：🎨 [光影与材质]
*(记录：特定材质画法、边缘光、全局色调、专属 LoRA 触发词)*

- **探索期基准画风 (参考图提取)**：
  - 正向：`(high definition pixel art:1.3), (hand-drawn dark outlines:1.2), (warm pastoral atmosphere:1.1), crisp edges, no anti-aliasing`
  - 负向：`blur, gradient, realistic, UI`
- **默认像素风后处理**：生成后必须使用 `nearest-neighbor` 算法缩放至目标尺寸，保持像素完美。

---

## 🗄️ 抽屉 5：⚙️ [AI 硬核参数]
*(记录：CFG 甜区、Denoising 比例、采样器建议)*

- **探索期垫图抽卡 (IPAdapter)**：
  - 权重建议：`0.6 - 0.8` (吸取色彩和描边，但不完全复制结构)
- **标准 SDXL 节点配置 (KSampler)**：
  - 大模型：`sd_xl_base_1.0.safetensors` 或 `Animagine XL`
  - 步数 (Steps)：`30 - 40`
  - 提示词引导系数 (CFG)：`5.0 - 7.0` (甜区，太高会烧图，太低会模糊)
  - 采样器：`euler_ancestral`
  - 调度器：`normal`
- **加速模型专属轨 (LCM/Turbo)**：
  - 步数 (Steps)：`6 - 8`
  - CFG：`1.5 - 2.0`
  - 采样器：`lcm`

---

## 🎯 附：探索期抽卡起点 (Exploration Base Prompt)

> **⚠️ 菜单 2 专用：第一张风格探索图纸**
> 目标：拿着这个 Prompt 疯狂抽卡，直到攒够 30 张满意的图去炼专属 Style LoRA。

**[Text Prompt 节点]**
`A 2D side-scrolling platformer game mockup screenshot, (high definition pixel art:1.3), (hand-drawn dark outlines:1.2), (warm pastoral atmosphere:1.1). A small adventurer character standing on a floating stone platform. Lush green grass with varied shades and small flowers. Detailed wooden textures and stone walls in the background. Soft top-left lighting, dark semi-transparent shadows. Crisp edges, no anti-aliasing.`

**[Negative Prompt 节点]**
`text, watermark, blurry, deformed, realistic, 3d render, gradient, isometric, top-down view, UI`

**[KSampler 节点]**
- Steps: `35`
- CFG: `7.0`
- Sampler: `euler_ancestral`

**[IPAdapter 节点]**
- 放入你提供的参考图，Weight 设为 `0.7`。

*Last Updated: 2026-04-09 by Manus TA*
