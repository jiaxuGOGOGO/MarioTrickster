# MarioTrickster 策划高速关卡生产指南

作者：**Manus AI**更新时间：2026-05-12

> **一句话定位**：MarioTrickster 当前已经可以支撑“策划专注摆关卡与设计互动，工程与美术接入由工具和 AI 在后台承接”的生产方式；本指南把分散在 Level Studio、素材导入、动画分类、测试工具中的能力合成一条日常可执行流程。[1] [2] [3]

---

## 1. 当前能力判断

从策划生产视角看，项目已经具备第一版完整闭环。关卡可以用 **ASCII 文本与片段库**快速生成；商业素材可以通过 **Asset Import Pipeline** 从外部图片变成可复用 Object/Prefab；已有白盒物体可以通过 **Apply Art to Selected** 直接换皮，并保留碰撞、陷阱、可操控等行为；角色 Sprite Sheet 可以根据 idle/run/jump/fall 命名自动进入状态动画，单独 RUN 也能先测试左右移动；更多商业素材状态会先被记录为语义摘要，供后续敌人、道具、技能特效和潜行系统挂接；测试可以通过 Test Console、Teleport、Cheats 与测试报告快速回归。[1] [2] [4]

| 目标 | 当前是否支持 | 使用入口 | 策划需要做什么 | 系统自动做什么 |
| --- | --- | --- | --- | --- |
| 快速摆关卡 | 支持 | `Ctrl+T → Custom Template Editor` | 改 ASCII 文本、追加片段、点 Build | 生成白盒、补可玩环境、保留 Root/Visual 结构 |
| 单个机关投放 | 支持 | `Ctrl+T → Element Palette` | 选择元素按钮 | 在 Scene 视图中心对齐网格生成 |
| 快速测试关卡 | 支持 | `Teleport / Cheats / Test Reports` | 点传送、开无敌或无冷却、跑测试 | 快速复位、补能量、导出报告 |
| 从零导入商业素材 | 支持 | `Asset Import Pipeline` | 拖图片，选择大类，点导入 | 规范化、切片、生成 Root/Visual、挂 SEF、保存 Prefab |
| 给已有白盒换皮 | 已优化 | `Apply Art to Selected` | 选 Root 或 Visual，拖素材，点应用 | 自动找到 Root，保留行为，只替换视觉、动画和材质 |
| 角色状态动画 | 支持 | 文件命名 + 分类器 | 按 idle/run/jump/fall 命名帧；单 RUN 可先试跑 | 自动挂 `SpriteStateAnimator` 并按运动状态切换，缺状态静态兜底 |
| 普通循环动画 | 支持 | 多帧 Sprite Sheet | 提供多帧素材 | 自动挂 `SpriteFrameAnimator` 循环播放 |
| 未来新增机制 | 支持 JIT 模式 | 给 AI 发大白话需求，或用策划生产助手复制标准请求 | 说“我需要某种机关/技能” | AI 按现有架构补代码、字典、测试与文档 |
| 素材包批量整理 | 已优化 | `Ctrl+T → Art & Effects Hub → 策划生产助手` | 选择素材文件夹，生成语义报告，满意后点击采纳改名，或自动填主题 | 扫描 Sprite、分类、建议命名、经确认批量改名、匹配 Theme Profile 槽位 |

项目目前最适合的工作方式不是在 Scene 里反复拖来拖去，而是先用白盒文本快速试玩法，确认“这一段好不好玩”，再把通过验证的对象换成商业素材或 AI 生成素材。这样可以最大限度减少重复抵消，把用户精力留给关卡节奏、陷阱组合、Trickster 伏击点和机制交互。[1] [5]

---

## 2. 三条日常主流程

### 2.1 做关卡：文字矩阵优先，Scene 拖拽只做局部微调

日常关卡制作的主入口是 `MarioTrickster → Test Console` 或快捷键 `Ctrl+T`。进入后优先使用 **Custom Template Editor**，把 ASCII 关卡矩阵粘进去，或从片段库追加 Tutorial Start、Trap Corridor、Final Sprint 等现有片段，然后点击 **Build From Text** 生成关卡。[1]

| 操作阶段 | 推荐做法 | 不推荐做法 | 原因 |
| --- | --- | --- | --- |
| 初版布局 | 在文本框里改字符 | 在 Scene 里逐块拖白盒 | 文本可复制、可回滚、可让 AI 直接修改 |
| 难度微调 | 改平台间距、陷阱字符、分支位置 | 手动改一堆 Transform | ASCII 能保持矩形结构和防重记忆 |
| 局部体验 | 用 Teleport 跳到对应区域 | 从起点反复跑全关 | 缩短测试时间，避免体力消耗 |
| 视觉替换 | 白盒通过后再换素材 | 一开始就追求精美画面 | 先验证玩法，后验证外观 |

常用字符保持在工作台内可查。最小闭环是：先写 `M`、`T`、地面、障碍和 `G`，点击 Build，按 Play 跑一遍；如果 Mario 跳不过去，就回到文本框改两格；如果 Trickster 没有阴人的机会，就加伪装墙、隐藏通道、摆锤、移动平台或分支路线。[1]

### 2.2 换素材：新对象走导入管线，旧白盒走 Apply Art

商业素材接入分两类。第一类是“我买了一个新地刺/新敌人/新角色，想生成一个可复用蓝图”，这时使用 **Asset Import Pipeline**。第二类是“我已经有一个白盒陷阱/平台/敌人，只想给它换外观”，这时使用 **Apply Art to Selected**。[2]

| 场景 | 入口 | 简化动作 | 结果 |
| --- | --- | --- | --- |
| 从零创建一个新资产 | `Ctrl+Shift+I` 或 Art & Effects Hub | 拖入图片，选择角色/地形/陷阱/特效/道具，点一键导入 | 生成 Root + Visual 对象和 Prefab |
| 把素材穿到已有白盒上 | `Ctrl+Shift+A` 或 Art & Effects Hub | 选中白盒 Root 或 Visual，拖入素材，点应用 | 行为不动，只换贴图、动画、材质 |
| 给素材加效果 | `Ctrl+Shift+Q` | 点描边、闪白、溶解、幽灵等预设 | 写入 SEF 效果并可保存蓝图 |
| 大合集图拆成多个对象 | AI Smart Slicer 或切片工具 | 先裁切，再导入 | 降低手动抠图与命名成本 |
| 素材包命名混乱 / 主题槽太多 | 策划生产助手 | 选文件夹和 Theme，先点语义报告；如果建议命名没问题，再点“采纳建议并批量改名”；需要换主题时点自动填槽 | 输出建议命名，弹窗确认后批量改名，并自动绑定可识别的主题 Sprite |

本轮已经优化 `Apply Art to Selected`：用户点到 Root 或 Visual 都可以，工具会自动把执行目标归一到真正承接行为的 Root。这样即使当前处于 Visual 选择模式，也不会把碰撞、Prefab 标记或行为组件误写到 Visual 子物体上。

### 2.3 增机制：大白话触发，AI 后台补字典、组件、测试和说明

项目定位不是让策划亲自维护复杂代码，而是让策划用大白话描述“我需要怎样的互动”。当关卡设计反复需要某类机关、敌人或角色技能时，再让 AI 按现有架构补机制，而不是提前做大量预见性基建。[5]

| 策划说法 | AI 后台应补齐 | 完成后策划看到的结果 |
| --- | --- | --- |
| “我想要会左右推人的风口” | 新组件、ASCII 字符、调色板按钮、测试、安全注释 | 文本里打新字符即可生成风口 |
| “我想让 Trickster 能短时间反转平台方向” | 能力入口、目标筛选、冷却/能量、反馈效果 | Trickster 按键即可操控对应平台 |
| “我买了一个新怪物素材，希望它能巡逻并可被踩死” | 素材导入、敌人行为模板、动画接入 | 拖入素材后能快速生成可用敌人 |
| “这段关卡太重复，帮我做一个新变体” | 读取模板、防重、生成 ASCII、更新登记 | 新片段可直接粘贴 Build |

策划不需要提供类名、继承关系或参数表。只要描述“它看起来像什么、怎么妨碍 Mario、Trickster 能不能操控、失败时希望玩家看到什么反馈”，AI 就能在后台把它转成组件、字典、测试和文档更新。

---

## 3. 商业素材命名建议

素材命名越接近用途，自动分类越准确。当前分类器会读取目标对象、Sprite 名称、资源路径和物理类型提示，判断素材是角色、背景、平台、陷阱、道具、特效还是 UI，并决定挂状态动画还是循环动画。[4]

| 素材类型 | 推荐命名 | 自动结果 | 注意事项 |
| --- | --- | --- | --- |
| 角色站立 | `hero_idle_00`, `mario_idle_01` | `SpriteStateAnimator.idle` | 完整四状态最标准；单状态也可在角色目标上进入状态动画并用静态帧兜底 |
| 角色奔跑 | `hero_run_00`, `hero_walk_01` | `SpriteStateAnimator.run` | run/walk 都可识别；只有 RUN 时左右移动仍会播放跑步动画 |
| 角色跳跃 | `hero_jump_00`, `hero_fall_00` | jump/fall 状态组 | 缺某组会回退到已有组，不会直接报错 |
| 普通循环物 | `torch_loop_00`, `water_01` | `SpriteFrameAnimator` | 适合火把、水面、传送门、背景循环 |
| 背景 | `forest_background_00`, `sky_bg_01` | `BackgroundLayer` | 已修复 background 被 ground 误判的问题 |
| 陷阱 | `spike_trap`, `sawblade_spin` | `HazardContact` | 贴到已有陷阱时会保留原行为 |
| 收集物 | `coin`, `gem`, `key_pickup` | `PickupConsume` | 后续可接收集反馈和 UI |
| 攻击/受伤/死亡 | `hero_attack_00`, `enemy_hurt_00`, `boss_death_00` | 通用状态摘要 | 先记录到导入元数据，不自动改控制器；等玩法需要时再挂接攻击、受伤或死亡逻辑 |
| 技能/特效释放 | `mage_cast_00`, `slash_impact_00`, `smoke_poof_00` | 特效或状态摘要 | VFX 仍走 OneShot/Loop，角色技能帧先记录为后续技能系统接入依据 |
| 潜行/融入环境 | `trickster_stealth_00`, `blend_wall_00`, `camouflage_00` | 通用状态摘要 | 不替换现有 `DisguiseSystem`，只作为潜行视觉扩展依据 |
| 道具交互阶段 | `trap_telegraph_00`, `switch_active_00`, `device_cooldown_00` | 通用状态摘要 | 对齐可控道具 Idle/Telegraph/Active/Cooldown/Exhausted 阶段，后续按需接行为 |

如果商业素材包命名很乱，推荐先在 **策划生产助手**里生成语义报告。报告会列出分类、动画类型、推荐槽位和建议命名；如果建议符合你的诉求，再点击 **采纳建议并批量改名**。系统会先弹出预览确认，只有确认后才会改 Sprite 切片名或单图资源名；如果你不确认，它只保留建议，不会动原素材。这样后续 AI 和工具都更容易理解素材用途，也减少“导入后再手动纠正”的抵消成本。[2]

---

## 4. 低摩擦工作台说明

Level Studio 已经承担项目日常制作的中心入口。它不是单纯测试窗口，而是策划的“关卡生产驾驶舱”：左手管白盒关卡，右手管素材与效果，下方管测试报告和快速传送。[1]

| 工作台区域 | 用途 | 最常用动作 |
| --- | --- | --- |
| Level Builder | 生成白盒关卡与主题换肤 | Build From Text、Apply Theme |
| Art & Effects Hub | 素材导入、换皮、SEF 效果、批量整理 | 打开导入管线、AI Smart Slicer、策划生产助手、Apply Art、Quick Apply |
| Element Palette | 单个元素投放 | 在 Scene 视图中心生成机关 |
| Teleport | 跳转到局部测试点 | Stage/Goal/Custom Teleport |
| Cheats | 降低重复跑图成本 | God Mode、No Cooldown、Infinite Energy |
| Test Reports | 自动化回归 | Run EditMode、Run PlayMode、Run All |

推荐日常只记住一个入口：`Ctrl+T`。如果不知道下一步去哪，就先回到 Test Console，因为它已经把导入、换皮、效果、关卡、测试入口集中在一起。

---

## 5. 一次完整制作示例

假设你要做一个“地刺诱饵 + 伪装墙伏击 + 终点冲刺”的小关卡，推荐流程如下。

| 步骤 | 你做什么 | 系统做什么 |
| --- | --- | --- |
| 1 | 打开 `Ctrl+T`，在 Custom Template Editor 里拼出包含 `M`、`^`、`F`、`G` 的 ASCII | 生成完整白盒和可玩环境 |
| 2 | Play 后用 Teleport 直接跳到陷阱区 | 省去从起点反复跑图 |
| 3 | 如果 Mario 太容易过，在文本里加分支或移动平台 | 秒级重建关卡 |
| 4 | 确认玩法成立后，选中地刺白盒，打开 Apply Art | 自动保留伤害逻辑，只换视觉 |
| 5 | 拖入商业地刺素材，点应用 | 自动分类、替换 Sprite、挂循环或状态动画 |
| 6 | 用 SEF Quick Apply 给陷阱加危险描边 | 写入 shader 效果，后续可保存 Prefab |
| 7 | 运行 EditMode/PlayMode 测试报告 | 导出失败清单，便于继续交给 AI 修复 |

这个流程的核心是先验证关卡，再接外观。只要白盒玩法没通过，就不要过早花时间精修贴图；只要白盒通过，素材替换就尽量走工具自动承接。

---

## 6. 当前短板已优化到哪里

本轮已经把原先最影响量产效率的三个短板从“后续方向”推进为编辑器内可用入口。新入口叫 **Planner Production Assistant（策划生产助手）**，可从 `MarioTrickster → Planner Production Assistant` 打开，也可在 `Ctrl+T → Art & Effects Hub` 中点击 **策划生产助手**。[6]

| 原短板 | 原影响 | 本轮落地能力 | 现在怎么用 |
| --- | --- | --- | --- |
| **商业素材包命名混乱** | 分类准确率下降 | 素材包语义巡检 + 建议命名报告 + 确认后批量改名 | 选择素材文件夹，点“生成语义报告”，系统列出分类、动画、推荐槽位和建议文件名；确认没问题后点“采纳建议并批量改名” |
| Theme Profile 仍需手动绑定部分 Sprite | 大规模换肤时有重复操作 | 按素材名和分类结果自动填 Theme Profile 槽位 | 选择素材文件夹和目标 Theme，点“按素材名自动填槽”，能识别的槽位自动填入，不清空已有确认内容 |
| 新机制仍需 AI 写代码 | 不是纯编辑器点击即可新增 | 新机制需求模板生成 | 填一句大白话需求，点“复制给外部 AI / Cursor 的实现请求”，自动带上 Root/Visual、ASCII、素材、测试和文档要求 |
| 角色素材只覆盖 idle/run/jump/fall 主状态 | attack/hurt/death/cast/stealth/blend 等商业状态已可识别并记录，但暂不自动驱动玩法 | 通用状态摘要 + 按需挂接 | 新素材包不会因多状态命名崩溃或误覆盖现有控制器；真正攻击、受伤、死亡、潜行视觉播放仍等具体机制需要时接入 |
| 自动化测试无法在沙盒跑 Unity | 需要用户本地 Unity 重跑 | 测试报告继续驱动修复 | 本地 Unity 导出报告后，AI 按报告全局修复并推送 |

策划生产助手的原则是“先安全辅助，再自动落地”。它不会在你没确认时强行改原始素材引用，而是先产出语义报告与建议命名；当你确认建议可采纳后，它可以批量修改 Sprite 切片名或单图资源名。它会自动填 Theme Profile 中能明显识别的槽位，但不会清空你已经确认过的插槽；它也不会把 Unity 编辑器变成不稳定的自动改代码黑盒，而是把机制请求整理成外部更强 AI 可以直接执行的标准任务。

因此，当前结论可以更新为：**项目已经适合回到关卡主线制作，并且素材命名、主题批量绑定和新机制请求三个短板都有了可用入口；下一步真正值得继续优化的是“AI 窗口直接把请求派发给外部智能体并回收结果”。**

---

## 7. 你以后可以直接这样发需求

为了保持低摩擦，后续你不需要讲技术菜单，只要按目标说。

| 你的大白话 | AI 应执行的后台动作 |
| --- | --- |
| “帮我做一段更阴的中段关卡” | 读模板登记、防重、生成 ASCII、写入模板库、给测试建议 |
| “这个商业包里有一堆怪和陷阱，帮我接进项目” | 裁切/命名/导入/分类/Prefab/文档更新 |
| “这个机关我想让 Trickster 可以控制” | 补组件、控制接口、能量/冷却、视觉反馈、测试 |
| “这个测试报告失败了，别只改表面” | 读报告、定位全局原因、修代码、更新 tracker、推送 |
| “给我一个使用教程，不要工程黑话” | 生成面向策划的步骤说明，并链接到项目入口 |

---

## References

[1]: ../LevelStudio_DesignGuide.md "MarioTrickster Level Studio 教程与关卡设计指南"

[2]: ./ASSET_IMPORT_PIPELINE_GUIDE.md "Asset Import Pipeline — 素材导入自动化指南"

[3]: ./ART_PIPELINE_UNIFIED_PLAN_2026-05-12.md "MarioTrickster 商业美术素材统一接入方案"

[4]: ../Assets/Scripts/Editor/ArtAssetClassifier.cs "ArtAssetClassifier — 商业美术素材后台分类器"

[5]: ../SESSION_TRACKER.md "MarioTrickster Session Tracker"

[6]: ../Assets/Scripts/Editor/PlannerProductionAssistant.cs "PlannerProductionAssistant — 策划高速生产助手"
