# Trickster 角色跑步动画出图工单

## 一、工单目标与接回定义

本工单旨在通过 ComfyUI 生成 Trickster 角色的 8 帧跑步循环动画（Run Cycle），并确保输出结果能够直接通过项目内置的 `AI_SpriteSlicer` 工具一键导入 Unity。

### 资产接回定义（必须严格遵守）
| 字段 | 规范要求 |
|---|---|
| **目标槽位** | Trickster 角色 Animator Controller 的 `Run` 状态 |
| **目录位置** | `Assets/Sprites/Characters/Trickster/` |
| **命名规则** | `trickster_run_sheet_v01.png`（横排 8 帧长图） |
| **导入参数** | PPU=32，Pivot=Bottom Center，Filter=Point（**由 AI_SpriteSlicer 自动设置，禁止手动修改**） |
| **废弃条件** | 出现严重滑步、帧间风格剧烈漂移、比例不匹配、非纯色背景 |

---

## 二、工作流搭建指南（IPAdapter + ControlNet）

为了保证 8 帧动画中角色的服装、武器和特征高度一致，我们将采用 **IPAdapter（锁定角色特征） + ControlNet OpenPose（锁定跑步姿态） + LoRA（锁定画风）** 的组合工作流。

### 1. 核心节点连线
请在现有的文生图工作流基础上，添加并连接以下节点：

1. **恢复 IPAdapter**：取消之前 Bypass 的 `IPAdapter Unified Loader` 和 `IPAdapter Advanced` 节点。
2. **加载参考图**：在 `加载图像` 节点中，上传 Trickster 的角色参考图（红冠头盔、持矛盾的女战士）。
3. **添加 ControlNet**：
   - 新建 `Load ControlNet Model` 节点，选择 OpenPose 模型（如 `control-lora-openposeXL`）。
   - 新建 `ControlNet Apply (Advanced)` 节点。
   - 将正向/负向 Prompt 连入 ControlNet Apply，再输出给 KSampler。
4. **加载骨架图**：新建一个 `加载图像` 节点，用于逐帧上传我为你准备的 8 张 OpenPose 骨架图，并连入 ControlNet Apply 的 `image` 输入。

### 2. 全局参数设置
| 参数项 | 设定值 | 说明 |
|---|---|---|
| **大模型 (Checkpoint)** | `sd_xl_base_1.0.safetensors` | 保持与验证时一致 |
| **LoRA 模型** | `MarioTrickster_Style_epoch_10` | 权重设为 **0.6**（角色类推荐权重） |
| **IPAdapter 权重** | `0.7` ~ `0.8` | 确保角色特征（红冠、矛、盾）被稳定注入 |
| **ControlNet 权重** | `1.0` | 必须强控制，防止动作变形 |
| **分辨率** | `512 x 768` | 竖版比例，适合站立/跑步角色 |
| **采样参数** | Steps: 35, CFG: 8.0, Sampler: euler_ancestral | 沿用你已调好的参数 |
| **种子 (Seed)** | **固定 (Fixed)** | 8 帧必须使用同一个 Seed，最大程度减少帧间闪烁 |

---

## 三、提示词配方

### 正向提示词 (Positive Prompt)
> trickster_style, 1girl, spartan warrior, red crested helmet, dark armor, leather skirt, holding spear in right hand, holding round shield in left hand, green eyes, black short hair, running pose, side view, looking right, 2D side-scrolling game sprite, pixel art, flat color background, isolated, full body, consistent lighting, high contrast, sharp edges

### 负向提示词 (Negative Prompt)
> worst quality, blurry, photorealistic, 3d render, shading gradients, isometric, top-down view, bird eye view, multiple girls, deformed limbs, bad anatomy, missing spear, missing shield

*(注：必须包含 `flat color background` 以确保背景纯净，方便后续一键去背。)*

---

## 四、逐帧执行清单

我已经为你生成了 8 张标准的 OpenPose 跑步骨架图（`run_pose_f01.png` 到 `run_pose_f08.png`）。请按以下步骤逐帧出图：

1. **锁定 Seed**：在 KSampler 中随便填一个数字，并将模式设为 `fixed`。
2. **跑第 1 帧**：在 ControlNet 的图像输入节点中，加载 `run_pose_f01.png`，点击出图，保存为 `frame_1.png`。
3. **跑第 2 帧**：将图像替换为 `run_pose_f02.png`，**不要改 Seed**，点击出图，保存为 `frame_2.png`。
4. **循环执行**：重复上述操作，直到跑完 8 帧。

---

## 五、后期处理与导入 Unity

跑完 8 张图后，你需要将它们拼接并导入 Unity：

1. **拼图**：使用 Photoshop 或在线拼图工具，将 8 张图横向拼接成一张长图（Sprite Sheet），命名为 `trickster_run_sheet_v01.png`。
2. **导入 Unity**：将长图拖入 Unity 项目的 `Assets/Sprites/Characters/Trickster/` 目录。
3. **一键切片**：在 Unity 顶部菜单栏点击 `MarioTrickster -> Art Pipeline -> 一键工业化切图`。
4. **参数填写**：
   - 拖入你的长图
   - 目标帧数填 `8`
   - 资产物理类型选 `实体角色/敌人 (Bottom Center 防滑步)`
5. **执行**：点击 `🔥 一键扣除纯色底并精准切片`。脚本会自动完成去背、设 PPU=32、锁死脚底重心、并切成 8 帧。
6. **创建动画**：在 Project 窗口选中切好的 8 帧，拖入场景中的 Trickster 角色，保存为 `Run.anim`。

完成以上步骤后，角色的跑步动画就正式接入游戏了。如果在出图过程中发现某帧的武器丢失或变形，可以单独调整该帧的 IPAdapter 权重或 Prompt 重新生成。
