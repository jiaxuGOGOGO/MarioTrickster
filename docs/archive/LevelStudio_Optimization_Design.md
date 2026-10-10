# MarioTrickster 关卡编辑器 (Level Studio) 优化设计方案

**作者**: Manus AI
**日期**: 2026-05-14

## 1. 现状分析与优化目标

当前 MarioTrickster 项目的关卡编辑器（`TestConsoleWindow.cs`）虽然功能全面，但它更像是一个“测试控制台”而非纯粹的“关卡设计工具”。它将关卡生成、主题应用、作弊开关、运行时状态监控等功能全部揉合在一个窗口中，导致界面冗长，设计师在进行关卡布局时需要频繁上下滚动。

根据 `LEVEL_DESIGN_SYSTEM.md` 的规范，关卡设计的核心工作流是：
1. 改布局 → ASCII 文本（Level Studio → Custom Template）
2. 改物理尺寸 → PhysicsMetrics.cs
3. 改行为参数 → 选中元素 → Inspector
4. 改视觉外观 → Theme System

**优化目标**：
将现有的 `TestConsoleWindow` 拆分或重构，提取出一个专注于**关卡布局与元素摆放**的独立编辑器（或优化现有布局），引入业界优秀的 2D 关卡编辑器 UX 设计理念，提升设计师的工作效率。

## 2. 业界优秀 2D 关卡编辑器 UX 设计参考

在设计新的关卡编辑器时，我们参考了业界顶尖的 2D 平台跳跃游戏和关卡编辑器的设计理念：

### 2.1 模块化与拖拽式工作流 (Drag-and-Drop & Grid Snapping)
现代 2D 关卡编辑器（如 LDtk [1]）强调直观的拖拽式操作和网格对齐。设计师可以直接从调色板中拖拽元素到场景中，元素会自动对齐到网格。这比纯文本编辑或点击按钮在屏幕中心生成元素更加直观高效。

### 2.2 视觉清晰度与层级管理 (Visual Clarity & Layers)
《Super Mario Maker》的成功在于其极简的 UI 和清晰的视觉反馈 [2]。它通过限制玩家的选择来引导设计，同时提供即时的视觉反馈。在我们的编辑器中，应该将“布局层”（白盒）和“视觉层”（Theme）明确区分，在布局阶段专注于碰撞和机制。

### 2.3 统一的主题与机制 (Harmony in Design)
优秀的关卡设计（如《Celeste》和《Super Mario Bros.》）强调机制的统一性 [3]。编辑器应该提供“片段库”（Snippet Library）功能，允许设计师快速拼装经过验证的机制组合，而不是从零开始逐个放置元素。

## 3. 优化方案架构设计

基于上述参考和项目现状，我们提出以下优化方案：重构 `TestConsoleWindow.cs`，将其改名为 `LevelStudioWindow.cs`（或在内部进行彻底的 Tab 化重构），使其真正成为一个“Level Studio”。

### 3.1 界面布局重构：严格的 Tab 分离

将现有的长列表界面重构为三个清晰的 Tab 页，彻底分离“设计”、“测试”和“美术”工作流：

| Tab 名称 | 核心功能 | 目标用户 |
| :--- | :--- | :--- |
| **1. Level Builder (关卡构建)** | ASCII 模板编辑、片段库、元素调色板（支持 Scene 视图拖拽或点击生成） | 关卡设计师 |
| **2. Art & Theme (美术与主题)** | 主题应用、SEF 材质修复、视觉模式切换 | 美术/技术美术 |
| **3. Test & Cheats (测试与作弊)** | 动态锚点传送、无敌/无限能量开关、时间缩放、运行时状态监控 | QA/测试人员 |

### 3.2 核心功能优化：Level Builder Tab

在 **Level Builder** Tab 中，我们将集中优化关卡设计师的体验：

1. **置顶 Custom Template Editor**：将 ASCII 文本编辑器和片段库（Snippet Library）移到最上方，因为这是最高频的操作。
2. **优化 Element Palette (元素调色板)**：
   - 保持现有的分类（Traps, Platforms, Enemies 等）。
   - 增加 Scene 视图交互提示：明确告知设计师可以通过点击按钮在 Scene 中心生成，然后手动拖拽。
   - （可选进阶）结合 Unity 的 `SceneView.duringSceneGui`，实现真正的拖拽笔刷功能（由于复杂度限制，本次迭代先优化 UI 布局，保留现有生成逻辑）。
3. **整合 9-Stage Test Scene**：将完整的测试场景生成按钮作为快捷工具放在底部。

### 3.3 交互体验优化

- **视觉降噪**：在 Level Builder 模式下，隐藏不必要的警告和提示，专注于布局。
- **一键可玩 (Playable Environment)**：保留现有的 `EnsurePlayableEnvironment` 逻辑，确保设计师生成的任何白盒关卡都可以立即点击 Play 进行测试。

## 4. Gameplay Mechanics 区块（机制驱动关卡设计）

基于项目已实现的 Commit 0–6 核心循环（附身 → 操控 → 连锁 → 热度 → 扫描危机 → 拢宝撤离），在 Level Design Tab 中新增专门服务于机制的设计工具：

### 4.1 Possession Anchor Network (附身点网络)

| 功能 | 说明 |
| :--- | :--- |
| 场景扫描 | 实时显示当前场景中所有 PossessionAnchor 的数量、启用/禁用状态 |
| 附身点列表 | 点击即可跳转到对应物体，显示位置和残留时间 |
| 分布质量分析 | 自动计算水平/垂直分布，警告过密或缺层次 |
| 快捷添加 | 一键为选中物体添加 PossessionAnchor 组件 |
| Scene 高亮 | 选中并聚焦所有附身点，方便全局审视 |

### 4.2 Route Budget (路线预算配置)

直接在编辑器中暴露 RouteBudgetService 的核心参数（自动恢复时间、最大同时降级数），并配合规则说明，让设计师无需进入 Inspector 即可调整路线护栏。

### 4.3 Mechanics Validation (机制验证)

一键检查关卡是否满足核心循环要求，检查项包括：

1. 附身点 ≥ 3 个（支撑连锁）
2. RouteBudgetService 存在（路线护栏）
3. LootObjective + EscapeGate（拢宝撤离目标）
4. AlarmCrisisDirector（扫描波危机）
5. 附身点分布质量（水平 ≥ 8 格）
6. TricksterHeatMeter（热度系统）
7. PropComboTracker（连锁系统）

结果以对话框形式展示，分为“通过/警告/错误”三级，同时输出到 Console。

## 5. 实施计划

1. **代码重构**：修改 `TestConsoleWindow.cs`，引入 `GUILayout.Toolbar` 实现 Tab 切换。
2. **逻辑迁移**：将现有的 UI 绘制方法（如 `DrawCustomTemplateSection`, `DrawElementPalette`, `DrawCheatsTab`）分配到对应的 Tab 中。
3. **清理冗余**：移除重复的提示信息，精简界面。

---

## 参考资料

[1] LDtk: A modern 2D level editor. https://ldtk.io/
[2] What Super Mario Maker can teach us about user experience (UX).
[3] Lessons of Game Design learned from Super Mario Maker. https://www.gamedeveloper.com/design/lessons-of-game-design-learned-from-super-mario-maker
