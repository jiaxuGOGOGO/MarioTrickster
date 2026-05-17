# MarioTrickster 终极操作手册 (User Manual)

> **全景定位与纪律**：本项目是一个**数据驱动的 2D 非对称自动化测试母机**。
> **不可妥协红线**：
> - **S53（无感基建/真理源）**：设计优先，架构隐形。所有参数必须从 `GameplayLoopConfigSO` 唯一真理源读取，禁止在各处散落硬编码。
> - **S37（视碰分离）**：物理碰撞（Root）与视觉表现（Visual）严格分离，换皮绝不可破坏底层物理盒与逻辑组件。

---

## 1. 关卡极速构建流 (Quick Start)

通过 `Level Studio`，策划可以像搭积木一样快速构建并验证关卡。

**操作步骤：**
1. **打开 Level Studio**：点击顶部菜单栏 `MarioTrickster -> Level Studio (Ctrl+T)`。
2. **选择关卡片段**：在 `Level Design` 页签下，找到 `Quick Whitebox Generator` 区块。在下拉菜单中选择一个 `Combat Snippet`（例如 `S2_Validation_4_Combat`）。
3. **查看设计意图**：选中片段后，上方会出现黄色的 HelpBox，展示该片段的 **Budget（路线预算）** 和 **TestGoal（测试目标）** 等元数据，确保生成前了解设计意图。
4. **生成白盒**：点击绿色的 `[Generate Whitebox Level]` 按钮，场景中心会立刻生成由 ASCII 字典驱动的白盒关卡。
5. **一键升级实战房**：如果关卡缺少完整的 Gameplay Loop 服务（如热度、扫描波、撤离门），点击下方醒目的绿色按钮 `[🛠️ Auto-Fix: 一键补齐 Gameplay Loop 服务与实战语义]`。
   - **发生现象**：系统会自动在后台注入 `RouteBudgetService`、`TricksterHeatMeter`、`AlarmCrisisDirector` 等核心服务，并将旧的 `Collectible/GoalZone` 升级为拥有『拿宝撤离、路线预算、扫描波危机』的实战语义，全程不破坏原有 ASCII 字典与物理结构。

---

## 2. AI 自动化与大模型调优流

利用 AI 双机托管，策划可以快速收集大量对局数据，并借助外部大语言模型（LLM）进行参数调优。

**操作步骤：**
1. **开启 AI 托管**：在 Play Mode 下，打开 `Level Studio` 的 `Cheats` 页签，滚动到底部找到 `🤖 AI Auto-Arena (自动挂机角斗场)`。
2. **双机接管**：勾选 `Mario 托管 (F1)` 和 `Trickster 托管 (F2)`（或在游戏内直接按 F1/F2）。
3. **加速挂机**：将 `Time Scale` 滑块拉到 `5x`，并勾选 `Auto Restart`，让 AI 自动进行多轮回合对抗。
4. **收集数据**：点击 `[Start Collecting]` 开始记录胜率、死亡坐标、卡死点等数据。
5. **导出战报**：挂机一段时间后，点击黄色的 `[Print Match Report]` 按钮。
   - **发生现象**：系统会在 `docs/validation/` 目录下生成一份 `auto_test_metrics_YYYY-MM-DD.json` 结构化战报和一份 `auto_test_summary.md` 摘要，并在 Console 打印绝对路径。

### 💡 LLM 调优提示词模板示例

你可以将导出的 JSON 文件内容复制，并配合以下提示词发给 Claude 或 ChatGPT，获取调优建议：

> **提示词模板：**
> "这是一份 2D 非对称对抗游戏（Mario vs Trickster）的 AI 自动对战测试报告 JSON 数据。
> 
> ```json
> [在此粘贴 auto_test_metrics_YYYY-MM-DD.json 的内容]
> ```
> 
> 请帮我分析这份数据，并给出具体的参数调整建议：
> 1. 观察 `MarioWinRate` 和 `TricksterWinRate`，目前平衡性偏向哪一方？
> 2. 查看 `DeadliestTrap`（最致命陷阱）和 `DeathPoints`（死亡坐标），Mario 主要死在哪里？是因为 `TrapKill` 还是 `FallOffCliff`？
> 3. 基于以上死因，我该如何调整参数？（例如：如果是被扫描波配合陷阱击杀，我该调长 `AlarmCrisisDirector` 的扫描冷却，还是削弱 `DeadliestTrap` 的伤害/判定范围？）请给出具体的修改方向。"

---

## 3. 现场调试与防误判指南

为了在复杂的实战房中保护设计心流，系统提供了强大的黑匣子日志和智能降噪可视化。

### 交互黑匣子 (Interaction Log)
- **怎么读**：在游戏运行时的左下角 HUD 中，有一个 `Interaction Log` 面板。它记录了最近的交互事件，包含八要素：`[时间戳] 交互类型(Hit/Reveal/Loot) 阶段(Windup/Active/Recovery) Source=来源 Target=目标 Result=结果`。
- **作用**：当发生“莫名其妙的掉血”或“突然被揭穿”时，无需打断点，直接看日志即可知道是哪个机关在哪个阶段（如前摇 Windup 还是激活 Active）造成了判定。

### 智能降噪可视化 (Gameplay Box Visualizer)
- **开启方式**：在 `Cheats` 页签勾选 `Show Gameplay Boxes` 和 `Show Trap Phase`。
- **智能降噪**：为了避免满屏线框干扰视线，系统采用了“智能降噪”策略。
  - **未选中时**：所有碰撞盒（白）、受击盒（蓝）、攻击盒（红）、扫描范围（青）以及机关阶段标签，都会以极低的透明度（Alpha=0.1）显示。
  - **点击选中 (Selection)**：当你用鼠标在 Scene 视图中**精确选中**某个物体时，**只有该物体**的线框和标签会恢复 100% 不透明度高亮显示。
- **发生现象**：点击陷阱 -> 陷阱的红色 HitBox 瞬间亮起，周围环境依然保持暗淡，完美保护你的设计心流，防止误判。

---

## 4. 安全换皮管线

验证完白盒关卡后，美术和策划可以在不破坏任何物理盒子的前提下，为关卡穿上精美的外衣。

**操作步骤：**
1. **打开 Art & Theme Hub**：在 `Level Studio` 中切换到 `Art & Theme` 页签。
2. **全局主题替换**：如果已经配置好了 `LevelThemeProfile`，直接将其拖入槽位，点击 `[Apply Theme (with Undo)]`，全关卡的白盒方块会瞬间替换为主题贴图。
3. **局部精细换皮 (Apply Art to Selected)**：
   - 在 Scene 视图中选中一个白盒物体（**点到 Root 节点或 Visual 子节点都可以**）。
   - 点击 `[打开 Apply Art to Selected (Ctrl+Shift+A)]`。
   - 在弹出的窗口中拖入美术素材，点击应用。
   - **发生现象**：工具会自动向上追溯找到真正承接行为的 Root 节点，**绝对保留**已有的行为组件（如伤害、碰撞、脚本），仅仅替换或新增 Visual 层级的 Sprite、Animator 和 Material。
4. **添加 Shader 效果**：点击 `[SEF Quick Apply]`，可以一键为选中的物体添加闪白、描边、溶解等高级视觉效果。如果效果未生效，点击 `[选中物体补 SEF Material]` 即可一键修复。
