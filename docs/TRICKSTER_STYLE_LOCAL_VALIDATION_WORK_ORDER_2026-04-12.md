# `trickster_style` 本地验证工单

**项目**：MarioTrickster  
**对象**：`MarioTrickster_Style_epoch_10.safetensors`  
**Trigger Word**：`trickster_style`  
**文档作者**：Manus AI  
**日期**：2026-04-12

---

## 1. 工单目的

这份工单不是为了重新研究风格，也不是为了直接进入大批量正式出图，而是为了先把当前已经训练完成的 `trickster_style` 当作 **V0 候选 LoRA** 做一次工程化本地验收。验收目标只有三个：第一，确认 `trickster_style` 这个触发词在本地到底是否稳定生效；第二，确认 `0.6 / 0.8 / 1.0` 三档权重中哪一档最适合作为当前项目的默认甜区；第三，确认它是否会把参考图中的具体题材脏带进来，尤其是 **海岸 / 沙滩 / 积云 / 海景透视 / 红屋顶** 这类不该被隐式继承的内容。

如果这轮验证通过，就不需要重开整套研究或重训，而是直接进入 **首批命名资产的窄切片量产**。如果验证失败，也不是回到“大而全重来一次”，而是基于失败症状决定是 **补负面词**、**缩权重**、**局部补训** 还是 **小范围重训**。

| 验收问题 | 本轮必须得到的结果 |
|---|---|
| **触发词是否有效** | `trickster_style` 是否能稳定把画风推向项目目标风格，而不是几乎无效或随机漂移 |
| **默认权重是多少** | 在 `0.6 / 0.8 / 1.0` 中，找出当前最适合做默认量产起点的档位 |
| **污染是否可控** | 是否出现与项目无关的题材偷渡，以及它出现在哪些题材上最明显 |
| **能否进入下一阶段** | 能否直接进入首批命名资产量产，还是必须补救后再进 |

---

## 2. 执行原则

这轮验证的核心原则是 **固定变量、只动 LoRA 相关因素**。换句话说，同一类题材内，底模、分辨率、采样器、步数、CFG、Seed、负面词都尽量不动，只改变两件事：**是否挂 LoRA / 如何触发 Trigger / 权重是多少**。只有这样，最后你看到的差异才真的可以归因到 `trickster_style` 本身，而不是被别的变量掩盖。

同时要注意，这轮是 **风格验证工单**，不是最终生产工单。因此首轮不建议混入其他 LoRA，不建议打开高分修复，也不建议先上复杂 ControlNet 去“强行救图”。尤其在角色动作题材里，如果首轮就把姿态控制开得很重，会掩盖 LoRA 自身到底稳不稳。ControlNet 兼容性测试可以做，但应放在第一轮结论之后。

| 项目 | 执行要求 |
|---|---|
| **底模** | 同一轮全部使用同一个底模，不允许中途切换 |
| **Seed** | 同一题材内固定同一个 Seed，不同权重与触发方式共用该 Seed |
| **采样参数** | 采样器、步数、CFG、分辨率整轮保持不变 |
| **其他 LoRA** | 首轮一律关闭，避免风格串味 |
| **ControlNet** | 首轮默认关闭；若角色动作首轮结论不稳定，再追加兼容性补测 |
| **高分修复 / 放大** | 首轮关闭，避免把风格偏差误判成细节问题 |
| **负面词策略** | 首轮只用通用负面词，不先加入“海岸 / 红屋顶”等专属去污词，以便看清真实污染 |

---

## 3. 固定测试设置

你不用为了这轮验证追求“最终最好看”，而是要追求“同条件下可比较”。如果你本机已有一套平时最稳定的基础出图配置，就沿用那一套；没有的话，就按你当前最熟的标准配置来，但整轮不要换。下面这张表是需要你在开始前一次性锁定的字段。

| 字段 | 你要怎么做 |
|---|---|
| **Base Model** | 填你这轮准备统一使用的主模型名称 |
| **Sampler** | 选一个你平时最稳定的采样器，整轮不变 |
| **Steps** | 固定一个步数，不中途加减 |
| **CFG** | 固定一个 CFG，不中途加减 |
| **Resolution** | 静态资产、角色动作、横版场景各自可有固定分辨率，但同类内部不能变 |
| **Seed** | 建议每类题材各固定一个 Seed，例如 `static_seed / action_seed / scene_seed` |
| **Negative Prompt** | 首轮只用通用负面词，不先做专属去污 |
| **LoRA Path** | 确认加载的是 `MarioTrickster_Style_epoch_10.safetensors` |

建议把这组固定设置先记在你自己的本地便签里，后面回传结果时一并发给我。这样我可以更快判断是 LoRA 问题，还是底模与采样配置在放大偏差。

---

## 4. 测试矩阵

本轮建议你先跑 **最小完整执行版 24 张**。它已经足够回答“触发词是否生效、权重甜区在哪里、污染是否明显”这三个核心问题，而且不会重到让你懒得执行。

### 4.1 题材维度

这轮固定只测三类题材，因为它们正好对应项目下一阶段最容易真正接回的三种资产：**静态资产**、**角色动作**、**横版场景**。这样测出来的结论，后面能直接用于首批量产，而不是停留在抽象研究层。

| 题材代号 | 题材类型 | 本轮建议测试对象 | 关注点 |
|---|---|---|---|
| **S** | 静态资产 | `地刺 / 方块 / 机关小物件` 三选一，以你最熟的一种为准 | 线稿清晰度、平涂关系、轮廓可读性 |
| **A** | 角色动作 | `Trickster 角色跳跃中帧` 或 `跑步中帧` 二选一 | 角色轮廓稳定性、肢体是否乱、风格是否压得住人物 |
| **C** | 横版场景 | `一小段可落脚的平台场景`，包含地面、平台、障碍即可 | 地平线是否稳、空间是否可读、是否偷渡参考图题材 |

### 4.2 触发方式维度

为了同时测到“挂了 LoRA 但没怎么触发”“正常触发”和“完全不挂 LoRA”的差异，这轮采用四组模式。这样你最后不仅能知道 **哪档最好看**，还能知道 **Trigger 到底是不是必须写**、**不写会不会也隐式生效**。

| 模式代号 | LoRA 状态 | Trigger 写法 | 目的 |
|---|---|---|---|
| **B0** | 不挂 LoRA | 不写 `trickster_style` | 作为纯底模基线 |
| **B1** | 挂 LoRA | 不写 `trickster_style` | 检查是否存在“隐式泄漏触发” |
| **B2** | 挂 LoRA | Prompt 末尾只写一次 `trickster_style` | 作为弱触发组 |
| **B3** | 挂 LoRA | Prompt 开头写 `trickster_style,`，主体语义不变 | 作为正常触发组 |

### 4.3 权重维度

权重只测三档：`0.6 / 0.8 / 1.0`。这是当前项目已经约定的首轮验证档位，既能看出风格是否发力，也足够观察污染是否随权重放大。

| 权重 | 本轮关注点 |
|---|---|
| **0.6** | 看是否已经能命中风格，同时保持结构稳定 |
| **0.8** | 看是否达到更好的风格命中与可用平衡 |
| **1.0** | 看是否虽然更像，但已经开始污染、压坏结构或题材偷渡 |

### 4.4 推荐总量

| 版本 | 组成 | 总张数 | 说明 |
|---|---|---|---|
| **最小完整执行版** | `3 个题材 × [B0 基线 1 张 + B1/B2/B3 各 3 张]` | **30 张** | 信息最完整，推荐默认执行 |
| **压缩执行版** | `3 个题材 × [B0 1 张 + B2/B3 各 3 张]` | **21 张** | 省时间，但少掉了“挂 LoRA 不写 Trigger”的泄漏判断 |

如果你时间允许，建议直接跑 **30 张标准版**。这轮不是浪费，而是在替后面大批量返工买保险。

---

## 5. 具体 Prompt 工单

下面给你的不是“唯一正确 Prompt”，而是 **验证用工单模板**。你可以按自己本机常用表达微调措辞，但必须保证同一题材里，主体语义不变，只改动 LoRA 状态、Trigger 写法和权重。

### 5.1 通用负面词（首轮）

首轮负面词请只保留通用清理项，不要先把海岸、沙滩、红屋顶这些专属污染词打进去。否则你会提前把污染盖住，测不出真实症状。

> `photorealistic, realistic shading, 3d render, blurry, muddy colors, messy lines, text, watermark, logo, cropped, low contrast, noisy details`

### 5.2 静态资产题材（S）

建议你选 **地刺** 或 **方块** 这种轮廓清楚、便于比较的对象。下面示例以 **地刺** 为准。

| 模式 | 正向 Prompt 模板 |
|---|---|
| **B0** | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` |
| **B1** | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` + 挂 LoRA |
| **B2** | `2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background, trickster_style` |
| **B3** | `trickster_style, 2D platformer spike trap, side view, centered object, clean black outline, flat color blocks, bright saturated palette, simple readable silhouette, game-ready prop, plain background` |

### 5.3 角色动作题材（A）

建议用 **Trickster 跳跃中帧**，因为它既能看角色轮廓，又能提前暴露动作题材是否容易脏掉。首轮先不要上姿态控制。

| 模式 | 正向 Prompt 模板 |
|---|---|
| **B0** | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` |
| **B1** | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` + 挂 LoRA |
| **B2** | `cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background, trickster_style` |
| **B3** | `trickster_style, cartoon trickster character, mid-jump pose, side view, full body, clean black outline, flat color blocks, bright saturated palette, readable silhouette, 2D platformer character frame, plain background` |

### 5.4 横版场景题材（C）

这里不要做大场景，只做 **一小段可落脚横版平台段**。重点不是美术大景，而是看 `trickster_style` 会不会把场景推向参考图里的具体题材叙事。

| 模式 | 正向 Prompt 模板 |
|---|---|
| **B0** | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` |
| **B1** | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` + 挂 LoRA |
| **B2** | `2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background, trickster_style` |
| **B3** | `trickster_style, 2D side-scrolling platformer scene, side view, walkable ground, one raised platform, one simple obstacle, clean black outline, flat color blocks, bright saturated palette, readable gameplay space, simple background` |

---

## 6. 文件命名与记录方式

为了避免跑完之后图片全混在一起，建议你从第一张开始就按统一命名保存。只要命名规整，你回传给我时我可以非常快地做判断，不需要你再重新解释一遍哪张是哪张。

| 字段 | 规则 | 示例 |
|---|---|---|
| **题材** | `S / A / C` | `S` |
| **模式** | `B0 / B1 / B2 / B3` | `B2` |
| **权重** | `w06 / w08 / w10` | `w08` |
| **Seed** | 原样记入文件名 | `seed123456` |
| **最终格式** | `TSVAL_[题材]_[模式]_[权重]_[seed].png` | `TSVAL_S_B2_w08_seed123456.png` |

其中 **B0 不挂 LoRA** 的基线图可以用 `w00` 记名，避免和其他组混淆，例如：`TSVAL_S_B0_w00_seed123456.png`。

---

## 7. 评分方法

这轮不要只凭一句“感觉还行”下结论，而是请你按下面的四个维度给每张图做一个简分。哪怕只是粗打分，也会比纯口述稳定很多。

| 评分维度 | 1 分 | 3 分 | 5 分 |
|---|---|---|---|
| **风格命中率** | 几乎不像项目目标风格 | 有点接近，但不稳定 | 明显命中项目目标风格 |
| **轮廓稳定性** | 结构乱、可读性差 | 基本能看，但边界不稳 | 轮廓清楚、结构稳定、可直接做生产基线 |
| **题材污染** | 明显偷渡无关题材 | 有一点影子，但不严重 | 几乎没有无关题材污染 |
| **可生产性** | 当前不能进入首批资产 | 勉强可试，但风险较高 | 可以直接作为量产起点 |

请特别留意下面这些污染症状。如果它们在 **场景组** 或 **角色组** 里反复出现，就说明 `trickster_style` 不是单纯“风格有点重”，而是已经把参考图具体题材一起学进来了。

| 污染观察项 | 你要看什么 |
|---|---|
| **海岸 / 沙滩偷渡** | 明明没要求海边，却出现沙地、岸线、海天线 |
| **积云 / 海景透视偷渡** | 背景自动长出大云团、海景空间关系 |
| **红屋顶 / 地中海式小屋偷渡** | 场景里平白出现参考图式建筑符号 |
| **色相偏移** | 颜色被强行往参考图固有配色拖走 |
| **角色被场景化** | 本该是干净人物帧，却被塞进强背景叙事 |

---

## 8. 结论判定标准

这轮验证的目的不是追求“满分神图”，而是决定 **是否足够进入下一阶段**。因此结论应按可执行标准做三分法，而不是模糊地说“再看看”。

| 结论 | 判定标准 | 下一步 |
|---|---|---|
| **通过，可进入首批量产** | 至少有一个权重在三类题材中总体表现稳定，且污染可控 | 把该权重记为默认起点，进入首批命名资产窄切片 |
| **条件通过，需要补救后再量产** | 静态资产稳定，但角色或场景污染较高，或需要专属去污词才能稳 | 先补专属负面词，必要时对动作题材补做 ControlNet 兼容测试 |
| **不通过，考虑补训或小范围重训** | 三类题材都不稳，或一上强度就明显题材偷渡、结构崩坏 | 不进入量产，整理失败症状后决定补训 / 重训 |

一个比较实用的默认判据是：**如果 `0.8` 能在静态资产和横版场景中保持较高命中率，并且角色组没有明显炸掉，那么它通常就是最值得优先尝试的默认量产起点。** 但最终仍以你实际跑出来的图为准。

---

## 9. 若出现污染，第二轮补救怎么做

如果第一轮已经看见明显污染，不要立刻宣布整卡报废。更稳妥的做法是：先只拿 **当前最好的一档权重** 做一轮小补测，专门验证专属去污词是否能把污染压下来。这样你可以区分“这是根本性训练失败”，还是“只是缺少专属负面词”。

| 补救项 | 做法 |
|---|---|
| **专属负面词补测** | 在最佳权重上追加：`beach, coast, seaside, shore, ocean horizon, cloud bank, red roof, mediterranean house` |
| **角色动作补测** | 若角色组风格像但肢体飘，再单独加姿态控制做兼容性验证；这不改变第一轮风格结论 |
| **场景补测** | 若场景组题材偷渡明显，优先判断是 LoRA 污染还是场景 Prompt 本身太宽 |

只有当 **补专属负面词也压不住污染**，或者 **稍微提权重就全面串题材**，才更接近“需要补训 / 重训”的级别。

---

## 10. 回传模板

你本机跑完之后，不用重新组织语言，直接按下面这段模板把结果回传给我就可以。我拿到后会直接帮你判定默认权重、整理专属去污词，并判断能否进入首批命名资产量产。

> 我已经按 `trickster_style` 本地验证工单跑完了。  
> 底模：`[你的底模]`  
> 采样器：`[你的采样器]`  
> Steps / CFG：`[你的参数]`  
> Seed：`S=[ ] / A=[ ] / C=[ ]`  
> 我跑的是：`30 张标准版` 或 `21 张压缩版`  
> 目前结论：  
> 1. **最佳权重**：`[0.6 / 0.8 / 1.0]`  
> 2. **静态资产表现**：`[简述]`  
> 3. **角色动作表现**：`[简述]`  
> 4. **横版场景表现**：`[简述]`  
> 5. **是否出现污染**：`[有 / 无；具体是什么]`  
> 6. **我怀疑的专属去污词**：`[词表]`  
> 7. **我附上的样张文件名**：`[列出几张代表图]`

---

## 11. 当前阶段的直接执行结论

对 MarioTrickster 当前阶段来说，这份工单就是你现在最该开始的动作。你不用回到“从零开始”的总入口，也不用先重训一次再说。**先把这张本地验证工单跑完，再决定 `trickster_style` 是直接上首批资产，还是进入补救回合。**

如果你希望把时间控制在最短，我建议你先跑 **静态资产 + 横版场景** 两类的 `B0 / B2 / B3 × 0.6 / 0.8 / 1.0`，快速看风格命中和污染趋势；如果结果看起来靠谱，再补角色动作组。若你愿意一次做完整闭环，那就直接跑 **30 张标准版**，这样后面判断会最干净。

*Last Updated: 2026-04-12 by Manus AI*
