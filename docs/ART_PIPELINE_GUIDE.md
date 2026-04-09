# 🎨 MarioTrickster 工业化美术管线操作菜单 (V4.0 双轨架构与节点分发版)

> **💡 核心护城河：彻底消灭"本地调参内耗"**
> 实体管 What，抽屉管 How。Manus 是大脑（啃书定规则、记设定、算节点参数）；你只是无情的渲染终端（复制、填入 ComfyUI、跑图验收）。图坏了绝不自己拉滑块，直接丢给 Manus 重新发牌！

---

## 🛠️ 步骤 0：本地渲染终端准备清单 (仅需做一次)

在给 Manus 发指令前，请确保你的本地厨房已经配齐了以下工具。如果已经搞定，请直接跳到【每日开工启动】。本指南基于 2026 年最新稳定版环境编写，针对 **RTX 4070 (12GB VRAM)** 显卡优化。

> **📂 路径约定**
> 本文档中所有路径以 `{ComfyUI根目录}` 代替你实际的安装位置。请根据你的实际情况替换，例如：
> - 如果你解压到了 `E:\ComfyUI\ComfyUI_windows_portable_nvidia\ComfyUI_windows_portable`，那么 `{ComfyUI根目录}` = 这个路径
> - 如果你解压到了 `D:\ComfyUI_windows_portable`，那么 `{ComfyUI根目录}` = `D:\ComfyUI_windows_portable`

> **💻 你的硬件配置评估**
> RTX 4070 拥有 12GB VRAM，完全满足 SDXL 模型的运行需求。实测可以在 1024×1024 分辨率下流畅跑图，同时叠加 IPAdapter + ControlNet 也不会爆显存。是运行本项目全部管线的理想配置。

### 0. 前置工具（确认已安装）

在开始安装 ComfyUI 之前，请确保以下两个基础工具已安装到你的 Windows 系统中。

| 工具 | 用途 | 下载地址 | 安装说明 | ✅ 已装？如何确认 |
|------|------|----------|----------|------------------|
| **7-Zip** | 解压 ComfyUI 的 `.7z` 压缩包 | [7-zip.org](https://www.7-zip.org/) | 下载 64-bit Windows x64 版本，双击安装，一路下一步即可 | 右键任意文件，弹出菜单中能看到 `7-Zip` 选项就说明已装，跳过 |
| **Git** | 克隆 ComfyUI Manager 和插件 | [git-scm.com](https://git-scm.com/downloads) | 下载 Windows 版，安装时全部保持默认选项即可 | 按 `Win + R` 输入 `cmd` 回车打开黑框，输入 `git --version` 回车。如果显示类似 `git version 2.xx.x` 的文字就说明已装，跳过。如果显示 `不是内部或外部命令` 则需要安装 |

### 1. 核心引擎与大模型

**1.1 安装 ComfyUI (Portable 版)**

> **✅ 已装确认**：如果你电脑里已经有 `ComfyUI_windows_portable` 文件夹，且双击 `run_nvidia_gpu.bat` 能正常打开网页，请直接跳过本节。

推荐使用官方的 Windows 一键解压版，它自带了独立的 Python 3.13 和 CUDA 13.0 环境，无需配置复杂的系统依赖。你的 RTX 4070 完美支持 CUDA 13.0。

*   **下载地址**：前往 [GitHub Releases](https://github.com/Comfy-Org/ComfyUI/releases) 或 [ComfyUI 官方下载页](https://www.comfy.org/download)，下载 **`ComfyUI_windows_portable_nvidia.7z`**（不带 cu126 后缀的默认版，约 1.77GB）。

    > ⚠️ **不要下载** `ComfyUI_windows_portable_nvidia_cu126.7z`。那是给老显卡/老驱动的降级兼容包。你的 RTX 4070 用默认版性能更好。
*   **解压与启动**：
    1.  右键点击下载的 `.7z` 文件 → 选择 `7-Zip` → `解压到“ComfyUI_windows_portable_nvidia\”`。
    2.  将解压出的文件夹移动到剩余空间大于 **50GB** 的固态硬盘（如 `E:\ComfyUI\` 下）。避免放在桌面或含中文的路径下。解压后的文件夹路径即为你的 `{ComfyUI根目录}`。
    3.  进入文件夹，双击运行 `run_nvidia_gpu.bat`。
    4.  首次启动会自动下载必要组件，可能需要 2–5 分钟。完成后控制台会显示 `To see the GUI go to: http://127.0.0.1:8188`，浏览器会自动打开。
*   **硬件要求**：

| 项目 | 最低要求 | 你的配置 (RTX 4070) | 备注 |
|------|----------|----------------------|------|
| GPU | NVIDIA 2018年+ | RTX 4070 (Ada Lovelace) | 完美支持 |
| VRAM | 8GB | 12GB | SDXL 模型舒适运行，叠加 ControlNet + IPAdapter 无压力 |
| 系统内存 | 16GB | 建议 32GB | 内存不足时模型加载会明显变慢 |
| 硬盘空间 | 50GB | 建议预留 80GB+ | ComfyUI 本体 ~2GB，模型文件会占大量空间 |
| 显卡驱动 | 最新稳定版 | 前往 [NVIDIA 驱动下载](https://www.nvidia.com/download/index.aspx) 更新 | 驱动过旧可能导致 CUDA 13.0 无法启动 |

**1.2 安装 ComfyUI Manager**

> **✅ 已装确认**：启动 ComfyUI 后，如果网页右下角菜单面板里有 `Manager` 按钮，请直接跳过本节。

这是 ComfyUI 的"应用商店"，用于一键安装缺失节点和更新插件。

*   **安装方法**：打开命令提示符（按 `Win + R`，输入 `cmd`，回车），依次执行以下命令：
    ```bash
    cd /d {ComfyUI根目录}\ComfyUI\custom_nodes
    git clone https://github.com/ltdrdata/ComfyUI-Manager.git
    ```
    > ℹ️ 如果提示 `git 不是内部或外部命令`，说明 Git 未安装或未加入系统 PATH。请回到步骤 0 安装 Git。
*   **验证**：关闭 ComfyUI 控制台黑框，重新双击 `run_nvidia_gpu.bat` 启动。在浏览器界面右下角的菜单面板中，你应该能看到新增的 `Manager` 按钮。点击它就能看到插件管理界面。

**1.3 下载大模型 (Checkpoint)**

> **✅ 已装确认**：检查 `ComfyUI\models\checkpoints\` 目录下是否已有 `sd_xl_base_1.0.safetensors` 或其他 SDXL 模型（文件大小通常在 6GB 以上）。

本项目基于 SDXL 架构，你需要下载基础模型或风格化微调模型。单个 Checkpoint 文件约 6–7GB。

| 模型名称 | 类型 | 文件大小 | 下载地址 | 说明 |
|----------|------|----------|----------|------|
| `sd_xl_base_1.0.safetensors` | 官方底模 | ~6.9GB | [HuggingFace 直链](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors) | 必装。所有 SDXL 工作流的基础 |
| `Animagine XL V3` | 二次元微调 | ~6.5GB | [Civitai 搜索](https://civitai.com/models/260267) | 推荐。动漫/手绘风格效果极佳 |
| `AAM XL AnimeMix` | 二次元微调 | ~6.5GB | [Civitai 搜索](https://civitai.com/) | 备选。另一种动漫风格取向 |

*   **放置路径**：将下载的 `.safetensors` 文件放入 `{ComfyUI根目录}\ComfyUI\models\checkpoints\` 目录下。
*   **下载提示**：文件较大，建议使用迅雷或 IDM 等下载工具加速。HuggingFace 直链可直接粘贴到下载工具中。
*   **放完后验证**：
    1.  如果 ComfyUI 正在运行，关闭控制台黑框，重新双击 `run_nvidia_gpu.bat` 启动（新放入的模型需要重启才能识别）。
    2.  在浏览器界面中，在画布空白处双击 → 搜索 `Load Checkpoint` → 拖出节点。
    3.  点击节点上的模型下拉菜单，应该能看到你刚放入的 `sd_xl_base_1.0.safetensors`。如果看到了，说明模型已正确识别，本步完成。
    4.  如果下拉菜单为空，请检查文件是否放对了目录（必须是 `models\checkpoints\` 而不是 `models\` 根目录）。

### 2. 必备插件与模型配置

点击右下角的 `Manager` → `Custom Nodes Manager`，搜索并安装以下核心插件。安装完成后，点击 `Restart` 重启 ComfyUI。

**2.1 IPAdapter Plus (垫图抽卡核心)**

> **✅ 已装确认**：
> 1. 插件：在 ComfyUI 画布空白处双击，搜索 `IPAdapter Advanced`，能搜到说明插件已装。
> 2. 模型：检查 `models\clip_vision\` 下是否有 `CLIP-ViT-H-14...`，以及 `models\ipadapter\` 下是否有 `ip-adapter-plus_sdxl...`。

用于将参考图的风格或角色特征完美迁移到新图中，相当于“一张图版的 LoRA”。

*   **插件安装**：在 Manager 中搜索 `ComfyUI_IPAdapter_plus` 并安装（作者：cubiq）。
*   **下载配套模型**：插件本身只是代码，还需要下载以下模型文件才能工作：

| 模型文件 | 放置目录 | 文件大小 | 下载地址 |
|----------|----------|----------|----------|
| `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | `ComfyUI\models\clip_vision\` | ~3.9GB | [HuggingFace](https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors)，下载后重命名为此文件名 |
| `ip-adapter-plus_sdxl_vit-h.safetensors` | `ComfyUI\models\ipadapter\` | ~848MB | [HuggingFace SDXL 目录](https://huggingface.co/h94/IP-Adapter/tree/main/sdxl_models) |
| `ip-adapter-plus-face_sdxl_vit-h.safetensors` | `ComfyUI\models\ipadapter\` | ~848MB | 同上 |

> ℹ️ `ipadapter` 文件夹需要手动创建：在 `ComfyUI\models\` 目录下新建名为 `ipadapter` 的文件夹。

**2.2 ControlNet 节点 (防滑步与透视锁定)**

> **✅ 已装确认**：
> 1. 插件：画布空白处双击，搜索 `DWPreprocessor`，能搜到说明预处理器已装。
> 2. 模型：检查 `models\controlnet\` 下是否有 `diffusion_pytorch_model_promax.safetensors`。

用于锁定角色的骨架姿势（OpenPose/DWPose）或场景的边缘轮廓（Canny/Depth/Lineart），防止 AI 生成时肢体崩坏或透视走样。

*   **预处理器插件**：在 Manager 中搜索 `ComfyUI's ControlNet Auxiliary Preprocessors`（作者：Fannovel16）并安装。这个插件包含了提取骨架和线稿所需的全部算法（DWPose、Canny、Depth、Lineart 等），首次使用时会自动下载对应的小型检测模型。
*   **下载 ControlNet 模型**：

| 方案 | 模型文件 | 大小 | 下载地址 | 说明 |
|------|----------|------|----------|------|
| **推荐：统一版 (ProMax)** | `diffusion_pytorch_model_promax.safetensors` | ~6.6GB | [HuggingFace (xinsir)](https://huggingface.co/xinsir/controlnet-union-sdxl-1.0/tree/main) | 一个模型支持 12 种控制类型（OpenPose/Canny/Depth/Lineart/Scribble 等），省去下载十几个单体模型的麻烦 |
| 备选：分体版 | `diffusers_xl_canny_full.safetensors` | ~2.5GB | [HuggingFace (lllyasviel)](https://huggingface.co/lllyasviel/sd_control_collection/tree/main) | 仅 Canny 边缘检测 |
| 备选：分体版 | `diffusers_xl_depth_full.safetensors` | ~2.5GB | 同上 | 仅深度图控制 |
| 备选：分体版 | `thibaud_xl_openpose.safetensors` | ~2.5GB | 同上 | 仅骨架姿势控制 |

*   **放置路径**：将下载的模型文件放入 `{ComfyUI根目录}\ComfyUI\models\controlnet\` 目录。

> 💡 **RTX 4070 用户建议**：直接下载 ProMax 统一版即可。12GB 显存跑它完全没问题，而且一个模型就能覆盖本项目所有用到的控制类型（骨架、线稿、深度）。

### 3. 辅助工具链

**3.1 去底工具（生成透明 PNG 导入 Unity）**

> **✅ 已装确认**：在 ComfyUI 画布双击搜索 `RMBG`，能搜到 `RMBG Node` 说明已装。

废弃在线服务（如 remove.bg），全面转向 **ComfyUI 内置全自动去底**。出图即透明 PNG，零成本、无限量、不离开工作流。

*   **插件安装**：在 Manager 中搜索 `ComfyUI-RMBG`（作者：1038lab）并安装。这是一个集成了目前所有最强开源去底模型的超级节点。
*   **模型选择（节点内切换）**：
    *   **BiRefNet**：目前（2025-2026）公认的开源去底 SOTA（State of the Art）。对毛发、半透明材质（如玻璃、特效）的边缘保留极好，实测 500 张商品图毛发精度达 94%。RTX 4070 跑它只需 ~1.4 秒。
    *   **RMBG-2.0**：Bria AI 最新开源模型，性能与 BiRefNet 接近（~1.2 秒），是极佳的备选。
    *   *注：首次在节点中选择模型并运行时，ComfyUI 会自动从 HuggingFace 下载模型文件到 `models/RMBG/` 目录，无需手动下载。*
*   **像素风特化备选**：如果你的游戏是纯像素风（Pixel Art），建议在 Manager 中额外安装 `ComfyUI-TransparencyBackgroundRemover`（作者：Limbicnation）。它专门针对游戏精灵图优化，能完美保留锐利的像素边缘而不产生模糊过渡。
    *   **安装注意**：安装后，需要进入 `ComfyUI\custom_nodes\ComfyUI-TransparencyBackgroundRemover` 目录，双击运行 `install.bat` 安装 `scikit-learn` 依赖。如果不装，节点也能跑，但精度会从 100% 降到 85-90%。
    *   **节点名称**：在画布双击搜索 `TransparencyBackgroundRemover`。
    *   **像素风最佳参数**：
        *   `edge_detection_mode`: 选 `PIXEL_ART`（或 `AUTO`）
        *   `tolerance`: 调低到 `10-20`（背景颜色容差）
        *   `edge_sensitivity`: 调高到 `0.9-1.0`（保留更多边缘细节）
        *   `dither_handling`: 设为 `True`（专门处理像素风的抖动图案）
        *   `scaling_method`: 必须选 `NEAREST`（最近邻插值，防止像素变糊）
        *   `output_size`: 选 8 的倍数（如 `256x256` 或 `512x512`）

**3.2 云端炼丹炉 (LoRA 训练)**

当你在探索期积累了 30 张以上满意的同风格图片后，可以注册 [LiblibAI](https://www.liblib.art/)（国内访问友好）或 [Civitai](https://civitai.com/) 账号。使用它们的在线训练服务（Civitai SDXL LoRA 训练起步价 500 Buzz ≈ $5），上传你的图片集，训练一个专属的 Style LoRA，从而在后续量产中彻底固化画风。

### 4. 安装完成自检清单

全部安装完成后，你的文件目录应该长这样：

```
{ComfyUI根目录}\
├─ ComfyUI\
│   ├─ models\
│   │   ├─ checkpoints\
│   │   │   └─ sd_xl_base_1.0.safetensors          (~6.9GB)
│   │   ├─ clip_vision\
│   │   │   └─ CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors  (~3.9GB)
│   │   ├─ controlnet\
│   │   │   └─ diffusion_pytorch_model_promax.safetensors   (~6.6GB)
│   │   ├─ ipadapter\                          ← 手动创建
│   │   │   ├─ ip-adapter-plus_sdxl_vit-h.safetensors       (~848MB)
│   │   │   └─ ip-adapter-plus-face_sdxl_vit-h.safetensors  (~848MB)
│   │   └─ loras\                              ← 后续放自己训练的 Style LoRA
│   ├─ custom_nodes\
│   │   ├─ ComfyUI-Manager\                    ← git clone 安装
│   │   ├─ ComfyUI_IPAdapter_plus\              ← Manager 一键安装
│   │   ├─ comfyui_controlnet_aux\              ← Manager 一键安装
│   │   ├─ ComfyUI-RMBG\                        ← Manager 一键安装（去底主力）
│   │   └─ ComfyUI-TransparencyBackgroundRemover\  ← 可选，像素风专用
│   └─ ...
├─ python_embeded\                         ← 自带 Python，勿动
├─ run_nvidia_gpu.bat                      ← 双击启动
└─ run_cpu.bat
```

**模型文件总占空间约 20GB**（底模 6.9 + CLIP 3.9 + ControlNet 6.6 + IPAdapter 1.7）。加上 ComfyUI 本体和插件，总共预留 **30GB** 即可开工。

### 5. 常见问题排查

| 现象 | 可能原因 | 解决方法 |
|------|----------|----------|
| 双击 `run_nvidia_gpu.bat` 闪退 | 显卡驱动过旧，不支持 CUDA 13.0 | 前往 [NVIDIA 官网](https://www.nvidia.com/download/index.aspx) 更新到最新驱动 |
| 控制台报错 `torch not compiled with CUDA` | 下载了错误的 Portable 版本 | 确认下载的是 `nvidia` 版而非 `cpu` 版 |
| 启动后浏览器白屏 | 页面未加载完成 | 等待 30 秒后刷新，或手动访问 `http://127.0.0.1:8188` |
| Manager 按钮不出现 | ComfyUI-Manager 未正确克隆 | 检查 `custom_nodes\ComfyUI-Manager\` 文件夹是否存在且非空 |
| 跑图时报 `CUDA out of memory` | 生成分辨率过高或同时加载模型过多 | RTX 4070 建议生成分辨率不超过 1024×1024；关闭其他占用显存的程序 |
| IPAdapter 报错 `ClipVision model not found` | CLIP Vision 模型文件名不对 | 确认文件名为 `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`（注意大小写和连字符） |
| ControlNet 出图全黑或无效果 | 选错了控制类型 | ProMax 统一版需要配合 `SetUnionControlNetType` 节点指定控制类型（如 openpose/canny/depth） |

---

## 🟢 每日开工启动（每次新开 AI 对话时必发）

**💬 怎么找 Manus (直接复制发给它)：**
> GitHub Token: [填入你的Token]
> 仓库：https://github.com/jiaxuGOGOGO/MarioTrickster
> 
> 请先用 Token 克隆仓库。静默读取 `SESSION_TRACKER.md` 获取当前进度，并强制读取 `prompts/PROMPT_RECIPES.md`。
> 
> 【系统指令：加载 MarioTrickster 工业化美术管线】
> 从现在起，你是本项目的「首席技术美术(TA)」。你已知悉我本地拥有完整的 ComfyUI 环境。
> 
> 针对配方库维护与工单下发，你必须遵守【双轨架构铁律】：
> 1. 上半部（技法库）：提取教程时，按「解剖/透视/动画/光影/参数」5个知识抽屉提取通用法则，静默写入配方库。
> 2. 下半部（实体库）：当我确立新角色、新陷阱的长相时，将基础视觉 Tags 按「角色/地形/陷阱/交互物」写入实体蓝图库。
> 3. 十字交叉出图：下发工单时，必须调取【实体蓝图的基础词】叠加【知识抽屉的技法词】，并按本地节点（LoRA/Prompt/ControlNet/KSampler）物理隔离分发。绝不全揉进文本。
> 4. 冲突熔断原则：当规则冲突时，永远遵循「物理防滑步 > 空间透视正确 > 光影材质表现」的优先级。同层级知识冲突时，触发【A/B实测仲裁】与【场景分治】机制。
> 5. 拒绝小作文：对话框内仅输出可直接复制到 ComfyUI 的具体节点配置图纸。
> 
> 准备好请回复：“🛠️ 工业管线就绪。双轨架构（技法抽屉 × 实体蓝图）已加载。请下达指令。”

---

## 📚 菜单 1：喂书蒸馏 (更新上半部：通用技法抽屉)

把找来的杂乱教程扔给 AI，它会自动将知识分拣进不同的"技法抽屉"。**遇到同抽屉内的新旧知识打架时，AI 会启动冲突仲裁。**

**💬 怎么找 Manus (直接复制发给它)：**
> "Manus，我刚上传了一份顶级的 [美术教程/设定集]。请启动『领域精细化无损蒸馏』：
> 1. 判断核心价值，严格归入 5 个「知识抽屉」的 1-2 个：🧍‍♂️[解剖与形态] / 📐[透视与物件] / 🏃[动画与物理] / 🎨[光影与材质] / ⚙️[AI 硬核参数]。
> 2. **冲突仲裁**：如果新知识与抽屉内现有规则矛盾（例如：A说正侧视，B说3/4视）：
>    - 若跨优先级，高优先级（如防滑步）直接秒杀低优先级。
>    - 若同优先级，请执行**场景分治**（例如：静态物件用规则A，动态角色用规则B，打上标签共存）。
>    - 若无法分治，请向我派发**【A/B实测工单】**，让我各出3张图进Unity跑一下，谁看着舒服留谁。
> 3. 只提炼能生效的 Tag、数值和节点约束。绝不写散文。将提炼的规则更新到 `prompts/PROMPT_RECIPES.md`，并在被否决的旧规则旁标注废弃原因。"

---

## 👾 菜单 2：新增设定/确立画风 (更新下半部：实体蓝图库)

当你想出一个新怪物，或者想确立全局画风时使用。

**💬 怎么找 Manus (直接复制发给它)：**
> **情况 A (加新怪/新物件)**："Manus，我们游戏里新增了一个资产设定：**【👉比如：一种带毒的飞行眼球怪】**。请提炼它最核心的视觉特征 Tags，归类到 `prompts/PROMPT_RECIPES.md` 下半部对应的实体分类（角色/地形/陷阱/交互物）中。以后做它必须调用此蓝图。"
>
> **情况 B (找参考图抽卡炼全局画风 LoRA)**："Manus，我要确立基准画风。我上传了参考图。请调取[光影]与[AI参数]抽屉，给我出卡工单。我会在本地抽够 30 张去炼全局 Style LoRA。"

---

## 🏭 菜单 3：批量生产资产 (量产期：实体蓝图 × 知识抽屉 = 节点发牌)

做具体动画或静物时，强制 Manus 把"长什么样（实体）"和"该怎么画（技法）"完美拼合，并分发到不同节点。

**💬 怎么找 Manus (直接复制发给它)：**
> "Manus，我要做游戏资产：**【👉 需求：例如，[交互物] 木制宝箱开启的 3 帧动画 / 或 [陷阱] 静态地刺】**。
> 请启动『十字双轨组装』流程，查阅 `prompts/PROMPT_RECIPES.md`：
> 
> 1. **双轨抓药声明**：明确告诉我你本次提取了下半部的哪个【实体蓝图】（长什么样）？并且拉开了上半部的哪几个【知识抽屉】（怎么画）？(注：比如做静态陷阱严禁混入动画规则，遇冲突执行熔断)。
> 2. **输出「节点化」制造工单**：请给我一份可直接在 ComfyUI 本地照抄的 Markdown 工单。严格按以下节点分类输出：
>    - 🖼️ **[Load LoRA 节点]**：项目专属 LoRA 的名称及推荐权重。
>    - 📝 **[Text Prompt 节点]**：将【实体蓝图特征词】与 🎨[光影抽屉词] 融合。要求极简 Tags (<20词)，带上反向防错词。
>    - 🤖 **[ControlNet 节点]**：根据 🏃[动画] 或 📐[透视] 抽屉的约束，明确告诉我该启用哪个模型（OpenPose/Canny/Depth），以及我该准备什么样的参考底图喂给它防崩坏。
>    - ⚙️ **[KSampler 节点]**：根据 ⚙️[AI参数] 抽屉，给出建议的 CFG 和 Denoising 安全数值。
> 绝对不要在对话框写小作文，只给我可以直接 Copy 的节点图纸！"

### 🛠️ 你拿到工单后该怎么做 (端到端闭环)：
1. **纯粹执行**：一字不差按 Manus 给的节点图纸配置 ComfyUI。
2. **遇错甩锅 (极重要)**：图跑出来透视崩了或者肢体错了，**绝不自己调参数**！带上坏图告诉 Manus："工单出图失败，现象是XXX，请调整 ControlNet 策略或按熔断原则重新发图纸！"
3. **切图入库**：满意后去底，拖进 Unity 点 `一键工业化切图`，跑测试验收。

---

## 🌟 为什么加上这个最终拼图后，管线真正做到了"天下无敌"？

在这个最终体系下，你的 GitHub 仓库就像一个会**"自动复利"**的超级大脑：

1. **无限扩展不乱套**：明天你要加个新怪物"食人花"，Manus 只会在【实体库-角色】里记下"绿色植物、长满獠牙"。但它出图时，会自动继承之前你喂给它的所有透视和光影抽屉里的顶级知识！
2. **全军自动升级**：后天你喂给 Manus 一本《金属材质渲染指南》，它更新了【知识抽屉-光影】。结果就是，你所有的宝箱、铁刺陷阱、主角盔甲，在下一次出图时，自动全军升级了完美的金属质感！

**实体设定（数据）与 绘画规则（算法）彻底解耦，互相复用，绝不串味。这就是真正的 3A 级美术工业流水线！**

*Last Updated: 2026-04-09 by Manus TA*
