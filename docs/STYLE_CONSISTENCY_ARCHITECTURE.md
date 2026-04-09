# 画风一致性与多版本切换架构指南 (Style Consistency & Multi-Style Architecture)

> **K2B-OS: Advanced Art Pipeline Extension**
> 本文档定义了在 AI 驱动的 2D 像素艺术管线中，如何确保生成素材的画风绝对统一，以及如何在 Unity 引擎中优雅地管理和切换多套画风版本。

---

## 1. 画风一致性锁定方案 (Style Consistency Locking)

在扩散模型（Diffusion Models）中，生成结果本质上是概率性的。为了在长周期的游戏开发中保持画风不发生漂移，我们必须采用“三层控制体系”来锁定视觉特征 [1]。

### 1.1 语义锁定层 (Semantic Locking)
文本提示词是控制画风的第一道防线。根据斯坦福大学的研究，增加文本描述的细节和结构化程度，可以显著提升生成结果的一致性 [1]。

- **角色卡 (Character Sheet) 锚定**：为每个核心角色建立固定的特征描述（如发色、服装材质、体型），并在每次生成时**原封不动地放在 Prompt 最前端**。
- **负向提示词护栏**：使用强力的负向提示词（如 `text, watermark, blurry, deformed, realistic, 3d render`）来防止模型偏离像素艺术的轨道 [1]。

### 1.2 结构引导层 (Structural Guidance)
当需要生成同一角色的不同动作或不同场景时，纯文本生成（Txt2Img）的漂移率极高。必须引入图像到图像（Img2Img）和 ControlNet 技术 [1]。

- **概念锚点 (Concept Anchor)**：在项目初期，生成一张完美的“假游戏截图”或“角色基准图”，存入 `Assets/Art/Reference/`。后续所有生成必须将此图作为 IPAdapter 的风格参考输入 [2]。
- **Denoising Strength 甜区**：在进行 Img2Img 动作转换时，重绘幅度（Denoising Strength）应严格控制在 **0.35 - 0.55** 之间。低于此值动作无法改变，高于此值角色特征会丢失 [1]。
- **ControlNet 模具**：对于生物关节角色，强制使用 DWPose 或 OpenPose 锁定骨骼位置；对于硬表面物体，使用 Lineart 和 Canny 锁定轮廓 [1]。

### 1.3 模型适配层 (Model Adaptation)
对于像素艺术，最稳定的一致性保障来自于专门微调的 LoRA 模型 [3]。

- **像素艺术专属 LoRA**：推荐使用 `pixel-art-xl-v1.1` 等经过社区验证的 LoRA 模型，它能从底层权重上限制模型生成非像素风格的内容。
- **Seed 锁定法**：在找到完美的基准角色后，记录其生成的 Seed 值。在后续生成同角色不同动作时，锁定该 Seed 值，可以极大降低面部和体型的漂移率 [4]。

---

## 2. 多画风版本切换架构 (Multi-Style Switching Architecture)

在独立游戏开发中，支持多套画风（如“复古 8-bit”、“高清 16-bit”、“手绘风格”）是一个极具吸引力的特性，但也带来了资产管理和运行时切换的技术挑战 [5]。

### 2.1 资产结构规划
要实现多画风切换，必须在项目初期就规划好并行的资产目录结构。每种画风必须拥有完全独立的贴图文件，但共享相同的命名约定和切片逻辑 [6]。

```text
Assets/Art/
├── Styles/
│   ├── Style_Pixel_16Bit/      ← 默认画风 (当前)
│   │   ├── Characters/
│   │   ├── Environment/
│   │   └── ...
│   ├── Style_Retro_8Bit/       ← 备用画风 A
│   │   ├── Characters/
│   │   ├── Environment/
│   │   └── ...
│   └── Style_HandDrawn/        ← 备用画风 B
│       ├── Characters/
│       ├── Environment/
│       └── ...
```

### 2.2 Unity 运行时切换方案
本项目目前已在 `LevelThemeProfile.cs` 中实现了一套基于 ScriptableObject 的数据驱动换肤系统。这套系统目前主要用于白盒关卡生成时的材质替换，但它正是实现全局多画风切换的完美基建。

#### 方案 A：基于 LevelThemeProfile 的扩展 (当前推荐)
目前 `LevelThemeProfile` 已经定义了 `groundSprite`, `platformSprite`, `marioSprite` 等插槽，以及一个通用的 `elementSprites` 字典。

1. **制作画风 Profile**：为每种画风创建一个 `LevelThemeProfile` 资产（如 `Theme_Pixel_16Bit.asset`, `Theme_Retro_8Bit.asset`）。
2. **填充插槽**：将对应画风目录下的 Sprite 拖入 Profile 的相应插槽中。
3. **运行时应用**：在游戏设置菜单中切换画风时，触发一个全局事件，遍历场景中所有带有特定 Tag 或组件的物体，读取其对应的键名（如 "Mario", "SpikeTrap"），并从当前激活的 `LevelThemeProfile` 中获取新的 Sprite 赋值给 `SpriteRenderer`。

#### 方案 B：SpriteAtlas Variant (进阶方案)
如果项目规模扩大，Draw Call 成为瓶颈，可以引入 Unity 的 Sprite Atlas Variant 技术 [7]。

1. **Master Atlas**：将默认画风（如高清版）打包为一个 Master Sprite Atlas。
2. **Variant Atlas**：为其他画风创建 Variant Sprite Atlas，将其 Master Atlas 指向默认画风的 Atlas。
3. **按需加载**：在运行时，通过 Addressables 系统动态加载并绑定对应的 Variant Atlas，Unity 会自动替换所有引用了该 Atlas 中 Sprite 的渲染器 [8]。

### 2.3 物理与逻辑的绝对解耦
多画风切换能够成立的**绝对前提**是：**视觉表现与物理碰撞必须彻底解耦**。

无论画风如何切换（哪怕从 16x16 的像素图切换到 256x256 的高清手绘图），角色的碰撞体大小（由 `PhysicsMetrics.cs` 定义的 0.8x0.95 units）和重力参数绝对不能改变。所有不同画风的素材，在导入 Unity 后，必须通过 `AI_SpriteSlicer.cs` 强制应用相同的 Pivot（如 Bottom Center）和 PPU（32），然后通过 `SpriteAutoFit` 脚本自动缩放视觉层以匹配物理碰撞体。

---

## 3. 总结与执行建议

1. **日常出图**：严格执行“概念锚点 + 锁定 Seed + 0.35-0.55 Denoising”的流程，确保单套画风内部的绝对一致性。
2. **画风扩展**：当需要引入新画风时，在 `Assets/Art/Styles/` 下建立新目录，使用相同的 Prompt 结构（仅替换风格描述词），批量生成新资产。
3. **引擎实装**：复用并扩展现有的 `LevelThemeProfile` 系统，将其从“编辑器白盒工具”升级为“运行时全局换肤管理器”。

---

## References

[1] Prompting Systems. "Creating Consistent Characters in AI Art: The 2026 Guide." https://prompting.systems/blog/creating-consistent-characters-in-ai-art
[2] ComfyUI Blog. "The Complete Style Transfer Handbook: All in ComfyUI." https://blog.comfy.org/p/the-complete-style-transfer-handbook
[3] KokuTech. "Pixel Art Generation with ComfyUI." https://www.kokutech.com/blog/gamedev/tips/art/pixel-art-generation-with-comfyui
[4] Reddit r/StableDiffusion. "Solved character consistency with locked seeds + prompt engineering." https://www.reddit.com/r/StableDiffusion/comments/1rkc3t9/solved_character_consistency_with_locked_seeds/
[5] Reddit r/gamedev. "How hard it is to swap art styles while the game is running?" https://www.reddit.com/r/gamedev/comments/1jhxmmn/how_hard_it_is_to_swap_art_styles_while_the_game/
[6] Unity Discussions. "How to dynamically switch sprite atlases at runtime?" https://discussions.unity.com/t/how-to-dynamically-switch-sprite-atlases-at-runtime/1646024
[7] Unity Documentation. "Variant Sprite Atlas." https://docs.unity3d.com/2023.2/Documentation/Manual/VariantSpriteAtlas.html
[8] Unity Discussions. "Loading SpriteSheet Sprites With AssetReference." https://discussions.unity.com/t/loading-spritesheet-sprites-with-assetreference/713060
