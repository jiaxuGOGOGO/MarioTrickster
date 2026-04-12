# MarioTrickster 动画管线

> **一键出图，知识驱动，git pull 即同步。**

## 快速开始（三步出图）

```bash
# 1. 环境检查（首次运行）
python setup_check.py

# 2. 单动作生成（自动选参考图和驱动视频）
python run_pipeline.py --action run

# 3. 批量全套动作
python batch_generate.py --ref assets/refs/trickster.png --video-dir assets/videos/
```

管线会自动：
- 从 `assets/refs/` 选参考图，从 `assets/videos/` 选驱动视频
- 如果 `assets/videos/` 为空但 `assets/fbx/` 有 Mixamo FBX，自动调 Blender 渲染
- 从知识库加载最优参数和画风 Prompt（trickster_style 画风）
- 生成后自动质检，不合格自动调参重跑（最多 2 轮，大部分 1 轮通过）

## 文件清单

| 文件 | 用途 |
|------|------|
| `run_pipeline.py` | 主管线：上传→生成→拆帧→去背景→像素化→Sprite Sheet→质检 |
| `batch_generate.py` | 批量生成器：一次跑全套动作 |
| `auto_qc.py` | 自动质检 & 智能调参（知识驱动版） |
| `knowledge_loader.py` | 蒸馏知识加载器 |
| `distilled_knowledge.json` | 蒸馏知识库（画风+动画规则+参数+QC 阈值） |
| `config_manager.py` | 配置管理（防变动架构） |
| `setup_check.py` | 环境检查 & 依赖安装 |
| `blender_render_drive_video.py` | Blender FBX→侧视角 MP4 |
| `fix_clip_vision_name.py` | CLIPVision 模型名修复 |
| `mixamo_presets.json` | Mixamo 动作预设（兼容回退） |
| `wan22_move_mode_api.json` | ComfyUI 工作流模板（API 格式） |
| `wan22_animate_official.json` | ComfyUI 完整工作流（参考用） |

## 知识驱动架构

```
PDF 书籍 → AI 蒸馏 → distilled_knowledge.json → git push
                                                    ↓
你本地 git pull → knowledge_loader.py 热加载 → 管线三点注入
```

### 知识注入点

| 环节 | 函数 | 效果 |
|------|------|------|
| 首次参数 | `get_optimal_params()` | 自动选最优 width/height/steps/cfg/length |
| Prompt 构建 | `enhance_prompt()` | 追加画风锚点+动作 boost+缺陷负向词 |
| 智能质检 | `compute_retune()` | 知识驱动阈值和调参计算 |

### 知识库分区（distilled_knowledge.json）

| 分区 | 内容 | 影响 |
|------|------|------|
| `style` | 画风定义、LoRA 参数、正/负向 Prompt 基底 | 首次出图画风 |
| `animation_rules` | 核心动画规则（对立力量、弧线运动、跟随延迟等） | Prompt 增强 |
| `actions` | 每个动作的最优参数、boost 词、缺陷列表、Mixamo/Blender 预设 | 参数+Prompt+QC |
| `vfx` | 特效规则（烟/火/爆/水） | 特效类动作 |
| `lighting` | 光影材质规则 | Prompt 增强 |
| `qc` | 质检阈值 | 自动质检灵敏度 |
| `retune` | 调参策略 | 智能迭代 |

### 如何添加新知识

上传 PDF 后，AI 蒸馏的结论按分区追加到 `distilled_knowledge.json`。管线下次运行自动热加载，无需改代码。

## 支持的动作类型

| 动作 | Mixamo 搜索 | 最优尺寸 | 关键检查点 |
|------|-------------|----------|-----------|
| idle | Breathing Idle | 480x480 | 微妙呼吸起伏，首尾帧一致 |
| walk | Walking | 480x480 | 脚跟先着地，对侧手臂摆动 |
| run | Running | 480x480 | 有空中帧，前倾，弓形身体 |
| jump | Jump | 480x640 | 预备蹲→起跳→顶点→缓冲 |
| attack_sword | Sword And Shield Slash | 640x480 | 蓄力→弧线挥击→停顿→收势 |
| death | Dying | 640x480 | 受击僵直→加速倒下→触地弹 |
| dash | Sprint | 480x480 | 极端前倾，夸张摆臂 |

## 环境要求

- **Python** 3.10+
- **ComfyUI**（本地运行，默认 127.0.0.1:8188）
- **模型**：WAN 2.2 14B fp8（UNET + VAE + CLIP + CLIPVision + LoRA）
- **自定义节点**：ComfyUI-WanAnimate、ComfyUI-VideoHelperSuite、comfyui_controlnet_aux
- **可选**：Blender 3.x+（FBX 自动渲染）

详细检查：`python setup_check.py`

## 可调参数

| 参数 | 默认 | 说明 |
|------|------|------|
| `--width/--height` | 知识库自动 | 生成分辨率（16 倍数） |
| `--steps` | 知识库自动 | 采样步数 |
| `--seed` | -1 | 随机种子（-1=随机） |
| `--pixel-size` | 4 | 像素化因子 |
| `--palette` | 32 | 调色板颜色数 |
| `--no-bg-remove` | false | 跳过去背景 |

> 不手动指定时，管线根据知识库自动选择每个动作的最优参数。

## 故障排除

| 问题 | 解决 |
|------|------|
| ComfyUI 连接失败 | 确认已启动，`--server` 指定地址 |
| 显存不足 | `--width 320 --height 320` 或用 fp8 模型 |
| 视频闪烁 | `--steps 10` 增加步数 |
| 像素化不理想 | 调整 `--pixel-size`(2-8) 和 `--palette`(16-64) |
| 去背景差 | `pip install rembg[gpu]` 或用绿幕 |
| 知识库未生效 | `python knowledge_loader.py` 验证加载 |
