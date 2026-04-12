# MarioTrickster 美术资产独立仓库分离方案

## 一、 背景与动因

在近期的美术落地测试中，通过对 13 帧 Trickster 角色跑步动画进行质量检查，我们发现当前基于 AI 图像生成（IPAdapter + ControlNet + LoRA）的逐帧生成管线存在严重的系统性瓶颈。核心表现为帧间角色外观、比例、服装颜色及武器形态完全无法保持一致（例如：第 1 帧为绿衣林克风弓箭手，第 12 帧突变为持镰刀黑发角色）。同时，背景颜色也无法统一，无法直接作为游戏 Sprite 使用。

鉴于上述质量崩溃问题，当前的工作流无法产出可用于游戏的连贯角色动画。为解决此问题，需要引入更专业的美术制作工具链（如 Aseprite 像素动画或 Live2D 骨骼动画）。同时，根据 Reddit 和 GitHub 社区的游戏开发最佳实践，将美术源文件及大体积二进制资产从主代码仓库中剥离，建立独立的美术资产仓库（Art Project），是大型团队或长线项目的标准做法 [1] [2]。此举不仅能有效控制主仓库体积，还能实现程序与美术工作的解耦，避免复杂的合并冲突。

## 二、 社区最佳实践参考

在游戏开发社区中，管理大型二进制美术资产主要有以下几种策略，其中 Git Submodule 是被广泛验证和推荐的方案：

### 1. 方案对比与选择

**Git Submodule（推荐方案）**
该方案允许在一个 Git 仓库中嵌套另一个 Git 仓库。主项目（Game Project）通过特定的 Commit Hash 引用美术项目（Art Project）。
优势在于：主仓库保持轻量，代码与美术历史完全隔离；美术人员可以在独立仓库中自由提交大体积文件（如 PSD、Blend、高精度贴图），而不会污染代码库的历史记录 [3]。同时，通过结合 Git LFS（Large File Storage），可以高效管理二进制文件。
劣势在于：需要开发人员熟悉额外的 Submodule 生命周期命令（如 `git submodule init` 和 `git submodule update`）[4]。

**Git Subtree**
该方案将另一个仓库的历史记录合并到当前仓库中。虽然对外部使用者透明，无需额外命令即可拉取完整代码，但会导致主仓库体积随美术资产的增加而迅速膨胀，且将修改推送回美术子仓库的操作较为繁琐 [5]。因此，不推荐用于大体积美术资产的管理。

**Unity UPM / 自定义包机制**
对于 Unity 项目，可以将美术资产打包为自定义的 UPM（Unity Package Manager）包，并通过 Git URL 引入 [6]。这在资产复用和分发上表现优异，但对于频繁迭代的开发期项目，更新流程略显冗长。由于本项目基于 Phaser.js（非 Unity），此方案不适用。

综合考虑，**Git Submodule + Git LFS** 是本项目进行美术资产分离的最佳路径。

## 三、 目标架构与目录切割设计

根据目前的目录结构，我们将把所有与美术直接相关的目录、文档和参考资料剥离到独立仓库 `tyu` 中。

### 1. 仓库职责划分

| 仓库名称 | GitHub 地址 | 核心职责 | 包含内容 |
|---------|------------|----------|---------|
| **主仓库** (Game Project) | jiaxuGOGOGO/MarioTrickster | 游戏逻辑代码、关卡设计、核心配置 | `Assets/Scripts/`, `Assets/Scenes/`, `Assets/Tests/`, 核心设计文档 |
| **美术仓库** (Art Project) | jiaxuGOGOGO/tyu | 美术源文件、最终导出的 Sprite、参考图、美术管线文档 | `Assets/Art/`, `pose_references/`, `prompts/`, `research/`, 美术相关 `docs/` |

### 2. 迁移范围清单

需要从 MarioTrickster 主仓库完整迁移（含 Git 历史）到 tyu 仓库的目录及文件如下：

- `Assets/Art/` (含所有子目录及占位符)
- `pose_references/` (姿势参考图)
- `prompts/` (AI 提示词工程文件)
- `research/` (美术与动画研究笔记)
- `docs/ART_BIBLE.md`
- `docs/ART_PIPELINE_GUIDE.md`
- `docs/ART_WORKFLOW_ADJUSTMENT_MEMO_2026-04-12.md`
- `docs/PIPELINE_ALIGNMENT_AND_ART_LANDING_PLAYBOOK.md`
- `docs/TRICKSTER_RUN_ANIMATION_WORK_ORDER.md`
- `docs/TRICKSTER_STYLE_LOCAL_VALIDATION_EXECUTION_SHEET.md`
- `docs/TRICKSTER_STYLE_LOCAL_VALIDATION_WORK_ORDER_2026-04-12.md`

## 四、 执行路径：无损历史迁移方案

为了保留这些美术资产的修改历史，我们将采用 GitHub 官方推荐的 `git-filter-repo` 工具进行拆分 [7]。

### 步骤 1：准备工作与美术仓库初始化
1. 确保本地安装 Python 和 `git-filter-repo`。
2. 克隆 MarioTrickster 的全新裸副本用于过滤。
3. 编写并应用路径过滤规则，保留上述指定的目录和文件。

### 步骤 2：推送到 tyu 仓库
1. 将过滤后的本地仓库关联到目标空仓库 `https://github.com/jiaxuGOGOGO/tyu.git`。
2. 推送代码及历史记录到 `tyu` 仓库的 `main` 分支。

### 步骤 3：清理主仓库并建立 Submodule 关联
1. 在原 MarioTrickster 仓库中，删除已迁移的目录和文件。
2. 提交清理操作的 Commit。
3. 将 `tyu` 仓库作为 Submodule 添加到 MarioTrickster 仓库的 `Assets/Art_Submodule`（或其他指定挂载点）目录下。
4. 配置构建脚本或软链接，使游戏引擎能够正确读取 Submodule 中的资产。

## 五、 未来工作流与接口约定

分离后，程序与美术的协作将遵循以下新工作流：

1. **美术开发（在 tyu 仓库）**：
   美术人员在 tyu 仓库中进行创作，源文件（如 .ase, .psd）存放在 `Sources/` 目录，导出用于游戏的成品文件（如 .png, .json）存放在 `Exports/` 目录。所有修改直接提交并推送到 tyu 仓库。

2. **程序集成（在 MarioTrickster 仓库）**：
   开发人员在主仓库中运行 `git submodule update --remote` 拉取最新的美术成品资产。主仓库的 Git 状态会记录当前引用的 tyu 仓库的 Commit Hash，确保版本的一致性和可回溯性。

3. **CI/CD 衔接**：
   若项目配置了自动构建流水线，需确保构建脚本在拉取主仓库代码后，执行 `git submodule update --init --recursive`，以确保打包时包含完整的美术资产。

---

## 参考文献

[1] u/PuffThePed. "How you do separate your assets from the main project?". Reddit r/Unity3D. https://www.reddit.com/r/Unity3D/comments/1ftsnzs/how_you_do_separate_your_assets_from_the_main/

[2] 3tt07kjt. "Handling Binary Game Assets in Git Repo". Reddit r/gamedev. https://www.reddit.com/r/gamedev/comments/78hrz2/handling_binary_game_assets_in_git_repo/

[3] Spritan. "Git Submodule vs Git Subtree — Quick Technical Overview". Medium. https://medium.com/@Spritan/git-submodule-vs-git-subtree-quick-technical-overview-92ae10119145

[4] Anchorpoint. "Submodules - Docs". https://docs.anchorpoint.app/git/submodules/

[5] GitProtect.io. "Managing Git Projects: Git Subtree vs. Submodule". https://gitprotect.io/blog/managing-git-projects-git-subtree-vs-submodule/

[6] Unity Documentation. "Package development workflow". https://docs.unity3d.com/6000.4/Documentation/Manual/CustomPackages.html

[7] GitHub Docs. "Splitting a subfolder out into a new repository". https://docs.github.com/en/get-started/using-git/splitting-a-subfolder-out-into-a-new-repository
