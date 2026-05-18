# 🎮 MarioTrickster 全管线傻瓜式操作手册 (Flight Manual)

欢迎来到 MarioTrickster 工业化管线！本项目集成了多画像 AI、全息战报、大模型问诊、物理防腐层、模板去重、双向 ASCII 烘焙等庞大功能。

为了防止策划和美术在复杂的管线中迷失，本手册作为**唯一真理源**，将手把手教你如何零代码跑通五大核心工作流。

---

## 1. 🏗️ 【造图流】：所见即所得的关卡双向搭建

本工作流允许你在 Scene 视图中像搭积木一样拼关卡，并一键逆向烘焙为 ASCII 文本供 AI 训练。

### 第一步：怎么从 Snippet 生成基础关卡？
1. 点击顶部菜单栏：`MarioTrickster -> Level Studio %t`（或直接按快捷键 `Ctrl+T` / `Cmd+T`）。
2. 在弹出的 **Level Studio** 面板中，找到 **Quick Whitebox Generator** 区块。
3. 在 `Template` 下拉菜单中选择一个你想编辑的关卡模板（Snippet）。
4. 点击绿色的 `Generate Whitebox Level` 按钮。
5. 此时，Scene 视图中会自动生成一个名为 `[Generated_Ascii_Level]` 的白盒关卡。

### 第二步：如何在 Scene 视图里拖拽移动平台的 3D 箭头把手？如何用 Ctrl+D 自由复制道具？
- **拖拽移动平台终点**：在 Scene 视图中选中任意 `MovingPlatform` 或 `ControllablePlatform`，你会看到一条青色的虚线和一个 **3D 箭头把手 (PositionHandle)**。直接拖拽这个把手，即可直观地修改平台的终点（`pointB`），无需手动输入坐标。
- **自由复制道具**：选中场景中的任意陷阱或道具（如 SpikeTrap、SawBlade），按下 `Ctrl+D`（Mac 为 `Cmd+D`）即可原地复制一份，然后拖动到你想要的位置。

### 第三步：点击哪个菜单能把场景逆向烘焙成带 `# Override` 参数重载标签的文本，并存回剪贴板？
1. 关卡编辑满意后，点击顶部菜单栏：`MarioTrickster -> Level Builder -> Bake Scene to ASCII (Clipboard)`。
2. 系统会自动扫描 `[Generated_Ascii_Level]` 下的所有物体，将其转换为 ASCII 字符阵列。
3. **重点**：你刚才拖拽的移动平台终点，会被自动提取为带有 `# Override` 参数重载标签的文本（例如 `# Override_12_5: pointB=3.00,0.00`）。
4. 烘焙结果已**自动存入你的系统剪贴板**，直接 `Ctrl+V` 粘贴到你的代码或文档中即可！

---

## 2. 🩺 【体检流】：关卡去重与物理死路验证

AI 生成的关卡可能会出现死路或语义重复，本工作流教你如何一键体检并让大模型自动修 Bug。

### 打开哪个菜单？
点击顶部菜单栏：`MarioTrickster -> AI Arena -> Level Template Validator (QA)`。

### 界面上的四个区块分别代表什么？
点击面板上方的扫描按钮后，系统会遍历 `LevelSnippetLibrary` 中的所有关卡，并分为四个区块展示：
- ❌ **Broken Levels (物理死路)**：关卡存在物理上绝对无法跳过的死胡同，或者格式严重损坏。
- 🔄 **Redundant Templates (重复模板)**：发现了博弈解法完全一致的冗余关卡（浪费 AI 训练算力）。
- ⚠️ **Untagged Templates (缺标签)**：关卡缺少必要的难度或流派标签。
- ✅ **Passed Levels (健康)**：物理可达且标签规范的完美关卡。

### 遇到死路报错，如何用 `[Copy AI Fix Prompt]` 按钮喂给大模型修复？
1. 在 ❌ **Broken Levels** 区块中，找到报错的关卡。
2. 点击关卡名称旁边的 `[Copy AI Fix Prompt]` 按钮。
3. 系统会自动将包含错误坐标、物理公式推导和修复指令的 Prompt 复制到剪贴板。
4. 将其粘贴给 ChatGPT/Claude，大模型会直接吐出修复后的 ASCII 文本。

---

## 3. 🤖 【跑测流】：AI 画像互搏与大模型问诊

本工作流教你如何配置不同性格的 AI 进行对战，并提取全息战报让大模型进行战术复盘。

### 去哪配置 `BotPersonaConfigSO` 捏出“手残党”和“贪刀老阴比”？怎么挂载到输入系统上？
1. **配置画像**：在 Project 窗口中，右键点击空白处，选择 `Create -> MarioTrickster -> Bot Persona`。这会创建一个 `BotPersonaConfigSO.cs` 的配置文件。在 Inspector 中调整参数，即可捏出“手残党”（反应慢、跳跃犹豫）或“贪刀老阴比”（极度偏好高风险收益）的画像。
2. **挂载画像**：点击顶部菜单栏：`MarioTrickster -> Level Studio %t`，展开 **TestSceneBuilder** 区块。找到 **InputManager** 所在的物体（通常在 Managers 节点下）。在 InputManager 的 Inspector 中，找到 `HybridInputProvider` 相关的配置槽位。将你捏好的 `BotPersonaConfigSO` 拖入 `marioPersona` 和 `tricksterPersona` 字段。

### 挂机跑对战后，去哪个目录找 `MatchReport_xxx.json` 全息战报？
在 Level Studio 的 **AI Arena** 面板中，勾选 `Mario 托管 (F1)` 和 `Trickster 托管 (F2)`，点击 `Start Collecting` 按钮开始对战。挂机结束后，战报会自动保存在项目根目录外的 `reports/ai_arena_reports/` 文件夹中，文件名为 `MatchReport_时间戳.json`。

### 点击哪个菜单打开 `AI Test Analyst (LLM)`？如何选中 JSON 战报并让大模型输出大白话诊断建议？
1. 点击顶部菜单栏：`MarioTrickster -> AI Arena -> AI Test Analyst (LLM)`。
2. 在弹出的面板中，首先在 **Settings** 区块填入你的大模型 API Key 和 Base URL。
3. 在 **File Selection** 区块的 `Battle Report` 下拉菜单中，选中刚刚生成的 JSON 战报。
4. 点击醒目的绿色按钮 `✨ Analyze Selected Report`。
5. 稍等片刻，大模型会在下方输出大白话的诊断建议。

---

## 4. 🎛️ 【调参流】：手感与对抗节奏真理源

所有关于手感和数值的调整，**严禁修改代码**，必须通过以下两个核心 ScriptableObject (SO) 进行，且强调 PlayMode 拖拽滑块实时生效的特性！

### 调 Mario 跑跳手感找哪个 SO？
- **绝对路径**：`Assets/Scripts/LevelDesign/PhysicsConfigSO.cs`（实例通常在 `Assets/Resources/PhysicsConfig.asset`）。
- **能调什么**：最大速度、跳跃初速度、重力加速度、Coyote Time（土狼时间）、Jump Buffer（跳跃缓冲）等。

### 调 Trickster 能量、热度、扫描找哪个 SO？
- **绝对路径**：`Assets/Scripts/LevelDesign/GameplayLoopConfigSO.cs`（实例通常在 `Assets/Resources/GameplayLoopConfig.asset`）。
- **能调什么**：Trickster 的最大能量、变身消耗、Mario 的 Q 扫描半径与冷却、热度 (Heat) 衰减速度、连锁 (Combo) 倍率等。

> 💡 **提示**：在 PlayMode 下，直接在 Inspector 中拖动这两个 SO 的滑块，游戏内的手感和数值会**立刻改变**，无需重启游戏！

---

## 5. 🛡️ 【防腐流】：美术绝对安全换皮

为了防止美术在替换商业素材时意外破坏策划调好的物理碰撞盒，我们引入了“物理防腐层”机制。

### 美术用 `AssetApplyToSelected` 换皮时，物理防腐层是怎么冻结碰撞盒的？为什么 `SpriteAutoFit` 绝对不能挂在 Root 节点？
1. **防腐层原理**：当你选中一个白盒物体并使用顶部菜单栏 `MarioTrickster -> Apply Art to Selected %#a` 换皮时，工具会**冻结**根节点（Root）的 `BoxCollider2D`（即玩法盒，决定了能不能跳过去），并将新贴图挂载到 `Visual` 子节点上。无论图片多大，都不会改变物理判定范围！
2. **致命禁忌**：如果你手动使用 `SpriteAutoFit.cs` 脚本来让图片自适应碰撞盒，**绝对不能**把它挂在带有 `MarioController`、`TricksterController` 或 `LevelElementBase` 的根节点上！一旦挂错，脚本会立刻在 Console 报红拦截：`[防腐层拦截] SpriteAutoFit 严禁修改包含核心物理的 Root 节点的缩放或尺寸！已拦截，请将图片与 SpriteAutoFit 挂载到 Visual 子节点。`

### 发版打包前，点击哪个菜单一键全图扫描，并强制恢复被意外破坏的碰撞红线？
在打包发版前，为了防止有人误操作破坏了碰撞盒，必须执行最后一道防线：
1. 点击顶部菜单栏：`MarioTrickster -> Art Pipeline -> Validate Physics Boxes (Anti-Corruption)`。
2. 系统会全图扫描 Mario、Trickster、SpikeTrap 等核心对象的 `BoxCollider2D`。
3. 如果发现任何尺寸被篡改，系统会**强制将其恢复**为 `PhysicsMetrics` 中定义的真理值，并在 Console 中打印红线修复日志。

---
*文档生成时间：2026-05-18*
*作者：Manus AI 首席技术文档架构师*
