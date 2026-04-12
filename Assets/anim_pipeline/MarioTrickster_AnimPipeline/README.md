# MarioTrickster 自动化动画管线

## 你需要做什么（三步出图）

**第一步：准备素材（约 5 分钟）**

1. 准备一张 Trickster 角色参考图（PNG，正面或侧面均可）
2. 去 [Mixamo](https://www.mixamo.com/) 搜索动作（见下方推荐表），下载 FBX

**第二步：渲染驱动视频（约 2 分钟，可自动化）**

用 Blender 将 FBX 渲染为侧视角 MP4（脚本已提供，一行命令搞定）

**第三步：一键生成（约 3-5 分钟等待）**

```bash
python run_pipeline.py --ref trickster.png --video run_drive.mp4 --action run
```

输出：`output/sprite_sheet.png` + `output/sprite_meta.json`，直接可用。

---

## 文件清单

| 文件 | 用途 |
|------|------|
| `run_pipeline.py` | 核心一键脚本：上传素材 → ComfyUI 生成 → 拆帧 → 去背景 → 像素化 → Sprite Sheet |
| `batch_generate.py` | 批量生成：一次跑全套动作（run/idle/jump/attack/walk/death） |
| `setup_check.py` | 环境检查：一键检测所有依赖是否就绪，并自动生成 `pipeline_config.json` |
| `config_manager.py` | 核心配置管理：统一管理所有路径和模型模糊匹配，实现防变动架构 |
| `knowledge_loader.py` | **蒸馏知识加载器**：统一加载知识库，驱动首次参数优化、Prompt 增强和智能质检 |
| `distilled_knowledge.json` | **蒸馏知识库**：从 PDF 书籍蒸馏的结构化知识（像素规则、构图、画风、动作深度知识） |
| `auto_qc.py` | **智能质检模块**（知识驱动版）：四项检测 + 知识驱动调参，减少迭代次数 |
| `pipeline_config.json` | 自动生成的配置文件，锁定 ComfyUI 路径和生成参数 |
| `blender_render_drive_video.py` | Blender 自动渲染：FBX → 侧视角 MP4 驱动视频 |
| `mixamo_presets.json` | Mixamo 动作预设库：每个动作的选片标准和渲染参数 |
| `wan22_move_mode_api.json` | ComfyUI 工作流 JSON（API 格式，Move 模式） |
| `wan22_animate_official.json` | ComfyUI 官方原版工作流（可直接拖入 ComfyUI 使用） |

---

## 环境准备

### 1. 运行环境检查

```bash
python setup_check.py
```

如果有缺失依赖，运行：

```bash
python setup_check.py --install
```

### 2. ComfyUI 模型下载

将以下模型放入对应目录：

| 模型 | 目录 | 大小 |
|------|------|------|
| `Wan2_2-Animate-14B_fp8_scaled_e4m3fn_KJ_v2.safetensors` | `ComfyUI/models/diffusion_models/` | ~17GB |
| `clip_vision_h.safetensors` (或 `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`) | `ComfyUI/models/clip_vision/` | ~1.2GB |

> **注意**: 如果你的 CLIP Vision 文件名是 `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`，请运行 `python fix_clip_vision_name.py` 将其重命名为 `clip_vision_h.safetensors`。
| `wan_2.1_vae.safetensors` | `ComfyUI/models/vae/` | ~300MB |
| `umt5_xxl_fp8_e4m3fn_scaled.safetensors` | `ComfyUI/models/clip/` | ~5GB |
| `lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors` | `ComfyUI/models/loras/` | ~400MB |

下载源：[Kijai/WanVideo_comfy_fp8_scaled](https://huggingface.co/Kijai/WanVideo_comfy_fp8_scaled) 和 [Comfy-Org/Wan_2.1_ComfyUI_repackaged](https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged)

### 3. ComfyUI 自定义节点

在 ComfyUI Manager 中安装，或手动 git clone 到 `custom_nodes/`：

- [ComfyUI-KJNodes](https://github.com/kijai/ComfyUI-KJNodes)
- [comfyui_controlnet_aux](https://github.com/Fannovel16/comfyui_controlnet_aux)

### 4. 显存要求

| 配置 | 模型版本 | 最低显存 |
|------|----------|----------|
| 推荐 | fp8 量化版 (KJ) | 12GB |
| 可用 | fp8 + 低分辨率 (480x480) | 8GB |
| 最佳 | bf16 原版 | 24GB |

---

## 使用方法

### 方式一：完整自动管线（推荐）

```bash
# 单个动作
python run_pipeline.py \
  --ref trickster.png \
  --video run_drive.mp4 \
  --action run

# 查看所有动作类型和 Mixamo 建议
python run_pipeline.py --list-actions
```

### 方式二：批量生成全套动作

```bash
# 准备视频目录（文件名包含动作关键词即可自动识别）
# mixamo_videos/
#   ├── run.mp4
#   ├── idle.mp4
#   ├── jump.mp4
#   └── attack_sword.mp4

python batch_generate.py \
  --ref trickster.png \
  --video-dir ./mixamo_videos/
```

### 方式三：仅后处理（已有视频）

```bash
# 对已有视频做 拆帧→去背景→像素化→Sprite Sheet
python run_pipeline.py \
  --postprocess-only \
  --video my_animation.mp4 \
  --pixel-size 4 \
  --palette 32
```

### 方式四：Blender 自动渲染驱动视频

```bash
# 单个 FBX
blender --background --python blender_render_drive_video.py -- \
  --input mixamo_run.fbx \
  --output run_drive.mp4 \
  --preset run

# 批量 FBX
blender --background --python blender_render_drive_video.py -- \
  --input-dir ./mixamo_fbx/ \
  --output-dir ./drive_videos/ \
  --auto-detect
```

---

## Mixamo 动作推荐表

以下动作经过动画原理验证，确保有正确的预备动作、跟随动作和重心转移：

| 动作 | Mixamo 搜索词 | 关键检查点 |
|------|---------------|-----------|
| 待机 | `Breathing Idle` | 有微妙呼吸起伏，首尾帧一致可循环 |
| 跑步 | `Running` | 有预备蹲，腿部弯曲大于90度，手臂大幅摆动 |
| 行走 | `Walking` | 脚跟先着地，步伐节奏均匀 |
| 跳跃 | `Jump` | 必须有蹲下→弹起→空中→落地四阶段 |
| 攻击 | `Sword And Shield Slash` | 有蓄力后拉，挥剑轨迹是弧线，击中后有停顿 |
| 死亡 | `Dying` | 有受击僵直，倒下有加速，落地有弹跳 |
| 冲刺 | `Fast Run` | 比跑步更大前倾，手臂幅度更大 |

---

## 防变动架构与配置

本项目采用**防变动架构**，所有路径和模型名称均通过 `pipeline_config.json` 统一管理，不再硬编码。

1. 首次运行 `python setup_check.py` 时，会自动生成 `pipeline_config.json`。
2. 请在 `pipeline_config.json` 中修改 `comfyui_root` 为你的实际 ComfyUI 安装路径（例如 `E:\\ComfyUI\\...\\ComfyUI`）。
3. 模型文件名支持**模糊匹配**（如 `Wan2*Animate*14B*.safetensors`），即使未来模型版本更新，只要关键词匹配即可自动识别，无需修改代码。

## 可调参数

除了在 `pipeline_config.json` 中永久修改默认参数外，你也可以通过命令行临时覆盖：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `--width` / `--height` | 480 | 生成分辨率（必须是16的倍数） |
| `--steps` | 6 | 采样步数（使用加速LoRA时4-6步即可） |
| `--seed` | -1 | 随机种子（-1为随机，固定数值可复现） |
| `--pixel-size` | 4 | 像素化因子（越大像素块越大） |
| `--palette` | 32 | 调色板颜色数（像素风格通常16-64色） |
| `--no-bg-remove` | false | 跳过去背景步骤 |

> **注意**：如果不手动指定参数，管线会根据 `distilled_knowledge.json` 中的蒸馏知识自动选择每个动作的最优参数。例如 `--action jump` 会自动使用 480x640 分辨率和 21 帧。

---

## 蒸馏知识驱动架构

本管线采用**知识驱动架构**，将 PDF 书籍蒸馏的知识结构化存储在 `distilled_knowledge.json` 中，并在管线的三个关键环节自动注入：

### 知识注入点

| 环节 | 注入方式 | 效果 |
|------|----------|------|
| **首次生成参数** | `knowledge_loader.get_optimal_params()` 根据动作类型自动选择最优的 width/height/steps/cfg/length | 首次出图即用最优参数，大幅减少迭代 |
| **Prompt 构建** | `knowledge_loader.enhance_prompt()` 追加动作专属知识和画风锚点到正/负向 Prompt | 生成结果更贴合像素风格和动画原理 |
| **智能质检** | `auto_qc.py` 使用知识库阈值和 `knowledge_loader.compute_retune()` 智能计算调参 | QC 更精准，调参更合理，减少无效迭代 |

### 如何添加新的蒸馏知识

1. 上传 PDF 书籍，蒸馏出结构化结论
2. 按分区格式追加到 `distilled_knowledge.json` 对应数组中
3. 管线下次运行自动生效（热加载，无需重启）

知识库分区说明：

| 分区 | 内容 | 影响 |
|------|------|------|
| `pixel_art_rules` | 像素风格规则（调色板、轮廓线、抖动） | Prompt 负面词 + 后处理参数 |
| `composition_rules` | 构图规则（留白、画布比例） | 首次生成的 width/height |
| `style_consistency` | 画风一致性（色温、饱和度、线条） | Prompt 正/负面锚点 |
| `action_knowledge` | 每个动作的深度知识（关键帧、常见缺陷） | Prompt boost + 最优参数 |
| `generation_presets` | 按动作类型的最优生成参数 | 首次生成参数覆盖 |
| `qc_thresholds` | 质检阈值 | QC 检测灵敏度 |
| `retune_strategy` | 智能调参策略 | QC 失败后的调参幅度 |

---

## 管线架构

```
你的操作          自动化管线                     产出
─────────       ──────────────────────         ──────
                ┌─────────────────────┐
参考图 ───────→ │  上传到 ComfyUI      │
                │                     │
驱动视频 ─────→ │  DWPose 骨骼提取     │
                │  ↓                  │
                │  WAN 2.2 Animate    │
知识库 ───────→ │  (知识增强 Prompt)   │  ← distilled_knowledge.json
                │  (最优首次参数)      │
                │  ↓                  │
                │  视频输出            │
                │  ↓                  │
                │  FFmpeg 拆帧        │
                │  ↓                  │
                │  Rembg 去背景       │
                │  ↓                  │
                │  像素化 + 调色板量化  │         sprite_sheet.png
                │  ↓                  │    ──→  sprite_meta.json
                │  智能质检 (4项)      │    ──→  qc_report.json
                │  ↓                  │
看效果给反馈 ←── │  通过? → 完成        │
                │  未通过? → 知识调参   │
                │  → 最多重跑 1 次     │
                └─────────────────────┘
```

---

## 蒸馏知识在管线中的位置

你之前蒸馏的知识没有浪费，它们已经被编码到管线的不同环节中：

| 蒸馏知识 | 在管线中的位置 | 自动化程度 |
|----------|---------------|-----------|
| 动画原理（挤压拉伸、预备动作） | `mixamo_presets.json` + `distilled_knowledge.json` 中的 action_knowledge | 全自动（首次 Prompt 即注入） |
| 空间透视 | `blender_render_drive_video.py` 中的相机角度和正交投影 | 全自动（预设已内置） |
| 像素风格（抖动、调色板） | `distilled_knowledge.json` 中的 pixel_art_rules + `run_pipeline.py` 后处理 | 全自动（知识驱动参数） |
| Prompt 工程 | `knowledge_loader.enhance_prompt()` 自动增强 | 全自动（知识库热加载） |
| 构图规则 | `distilled_knowledge.json` 中的 composition_rules | 全自动（首次参数优化） |
| 画风一致性 | `distilled_knowledge.json` 中的 style_consistency | 全自动（Prompt 锚点） |

---

## 故障排除

**ComfyUI 连接失败**：确认 ComfyUI 已启动，默认端口 8188。可用 `--server` 指定其他地址。

**显存不足 (OOM)**：降低分辨率 `--width 320 --height 320`，或使用 fp8 量化模型。

**生成的视频闪烁**：这说明 Move 模式工作正常但参数需要调整。尝试增加 `--steps 10` 或调整 seed。

**像素化效果不理想**：调整 `--pixel-size`（2-8）和 `--palette`（16-64）。像素块太大就减小 pixel-size，颜色太少就增加 palette。

**去背景效果差**：安装 GPU 版 rembg（`pip install rembg[gpu]`），或使用绿幕驱动视频配合 `--no-bg-remove`。

**知识库未生效**：确认 `distilled_knowledge.json` 和 `knowledge_loader.py` 在项目根目录。运行 `python knowledge_loader.py` 可验证知识库加载状态。
