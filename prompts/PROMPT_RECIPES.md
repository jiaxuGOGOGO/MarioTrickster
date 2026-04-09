# 🎨 MarioTrickster 提示词与节点配方库 (PROMPT_RECIPES)

> **⚠️ 工业化管线 V4.0 专属核心知识库 (双轨架构)**
> 本文档是 Manus TA 的"大脑"。
> **上半部（技法库）**：管 How（怎么画），通过蒸馏教程更新。
> **下半部（实体库）**：管 What（画什么），新增怪物/物件时更新。
> 出图时，Manus 将把两部分【十字交叉】，分发到你的 ComfyUI 节点中。

---

## 📚 上半部：通用技法库 (5 个知识抽屉)

### 🗄️ 抽屉 1：🧍‍♂️ [解剖与形态]
*(记录：头身比、防断肢、多角度姿势)*
- **通用防畸变负面 (Text Prompt)**：`deformed, extra limbs, bad anatomy, missing fingers, text, watermark`

### 🗄️ 抽屉 2：📐 [透视与物件]
*(记录：等距视角、静物结构、平台拼接规律)*
- **横版跳跃视角锁定 (Text Prompt)**：`side-scrolling platformer, side view`
- **防视角偏移负面 (Text Prompt)**：`isometric, top-down view, 3d render, perspective`
- **地形无缝拼接规则**：基础地面必须为 `32x32`，平台必须水平方向无缝拼接。
- **物件结构模具 (ControlNet)**：静态地形、陷阱、交互物 **强制启用 `Lineart` 或 `Canny` 预处理器**，锁定边缘轮廓。

### 🗄️ 抽屉 3：🏃 [动画与物理]
*(记录：关键帧数、运动模糊、防滑步约束)*
- **通用防滑步原则**：角色/敌人重心锁死在 **Bottom Center (0.5, 0)**。地形/陷阱重心通常为 **Center (0.5, 0.5)**。
- **动画结构模具 (ControlNet)**：所有带连续动作的生物关节角色（跑、跳、受击）**强制启用 `DWPose` / `OpenPose` 预处理器**！
- **基准帧数**：跑步 8 帧，待机/行走/飞行 4 帧，跳跃 3 帧。

### 🗄️ 抽屉 4：🎨 [光影与材质]
*(记录：特定材质画法、边缘光、全局色调、专属 LoRA 触发词)*
- **探索期基准画风**：
  - 正向：`(high definition pixel art:1.3), (hand-drawn dark outlines:1.2), (warm pastoral atmosphere:1.1), crisp edges, no anti-aliasing`
  - 负向：`blur, gradient, realistic, UI`
- **默认像素风后处理**：生成后必须使用 `nearest-neighbor` 算法缩放至目标尺寸。

### 🗄️ 抽屉 5：⚙️ [AI 硬核参数]
*(记录：CFG 甜区、Denoising 比例、采样器建议)*
- **探索期垫图抽卡 (IPAdapter)**：权重建议 `0.6 - 0.8`。
- **标准 SDXL 节点配置 (KSampler)**：
  - 大模型：`sd_xl_base_1.0.safetensors`
  - 步数 (Steps)：`30 - 40`
  - 提示词引导系数 (CFG)：`5.0 - 7.0`
  - 采样器：`euler_ancestral`，调度器：`normal`

---

## 👾 下半部：实体蓝图库 (What)

> 这里只记录每个实体的**纯视觉特征词（长什么样）**。出图时，Manus 会自动为你叠加【上半部】的透视和光影技法。

### 🏃 角色与敌人 (Entities)
| 实体名称 | 视觉特征词 (Visual Tags) |
| :--- | :--- |
| **Mario** (主角) | `Mario-like character, red cap, blue overalls` |
| **Trickster** (幽灵形态) | `Ghostly trickster character, purple ethereal glow, semi-transparent body` |
| **SimpleEnemy** (基础巡逻怪) | `Cute slime monster, green color` |
| **BouncingEnemy** (弹跳怪) | `Spring-loaded robot enemy, metallic texture` |
| **FlyingEnemy** (飞行怪) | `Bat creature, flapping wings, dark purple` |

### 🧱 地形与平台 (Environment & Platforms)
| 实体名称 | 视觉特征词 (Visual Tags) |
| :--- | :--- |
| **Ground / Wall** (基础地形) | `stone texture, mossy` |
| **Platform** (普通平台) | `wooden planks texture` |
| **OneWayPlatform** (单向平台) | `thin platform, metal grating texture` |
| **BouncyPlatform** (弹跳平台) | `bouncy mushroom platform, rubbery texture` |
| **CollapsingPlatform** (崩塌平台) | `cracked stone platform, crumbling texture` |
| **MovingPlatform** (移动平台) | `mechanical moving platform, metallic texture with yellow caution stripes` |
| **ConveyorBelt** (传送带) | `conveyor belt segment, industrial mechanical texture` |
| **BreakableBlock** (可破坏方块) | `fragile brick block, cracked terracotta texture` |
| **FakeWall** (伪装墙) | `illusion wall block, slightly faded stone texture` |

### ⚠️ 陷阱与机关 (Hazards & Mechanics)
| 实体名称 | 视觉特征词 (Visual Tags) |
| :--- | :--- |
| **SpikeTrap** (地刺) | `Sharp metal spikes, rusty metal, pointing upwards` |
| **FireTrap** (火焰陷阱) | `Roaring campfire flame, bright orange and yellow` |
| **PendulumTrap** (摆锤) | `Heavy spiked iron ball, dark metal texture` |
| **SawBlade** (旋转锯片) | `Circular mechanical saw blade, spinning motion blur effect, silver metal` |

### ✨ 交互物与 UI (Interactables & UI)
| 实体名称 | 视觉特征词 (Visual Tags) |
| :--- | :--- |
| **Collectible** (收集物/金币) | `Shiny gold coin, bright yellow` |
| **Checkpoint** (检查点/旗帜) | `Red checkpoint flag on a pole` |
| **GoalZone** (终点门) | `Ornate wooden door, glowing magical aura` |
| **HiddenPassage** (隐藏通道入口) | `Dark mysterious cave entrance, stone archway` |

*Last Updated: 2026-04-09 by Manus TA*
