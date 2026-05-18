# 🎮 MarioTrickster 全管线傻瓜式操作手册

> **“从零代码到全栈大师，只需这一份指南。”**  
> 作者：Manus AI

本项目现已集成多画像 AI、全息战报、大模型问诊、物理防腐层、模板去重、双向 ASCII 烘焙等庞大功能。为了防止功能遗忘，特编写此“保姆级”实操指南。请按以下五大工作流模块对号入座。

---

## 1. 🏗️ 【造图流】：所见即所得的关卡双向搭建

告别“改代码 -> 看效果 -> 盲调”的痛苦，现在你可以像搭积木一样自由捏关卡，并一键固化为代码模板。

### 📌 第一步：怎么从 Snippet 生成基础关卡？
1. 在顶部菜单栏点击：`MarioTrickster -> Level Studio`。
2. 展开面板下方的 **经典片段库 (Snippet Library)**。
3. 点击任意 Snippet 按钮，对应的 ASCII 字符会自动追加到上方的文本框中。
4. 点击 **Generate Whitebox Level**，即可在 Scene 视图中看到生成的白盒关卡。

### 📌 第二步：如何在 Scene 视图里可视化调优？
- **调终点**：在 Scene 视图中选中任意移动平台（`MovingPlatform` 或 `ControllablePlatform`），你会看到一条青色的虚线和一个 **3D 箭头把手**。直接拖拽这个把手，即可可视化调节平台的移动终点（`pointB`）。
- **摆道具**：觉得金币不够？选中场景里的金币，按 `Ctrl+D` 自由复制，拖到你想放的任何地方。所有增删改查完全自由！

### 📌 第三步：如何把调好的场景逆向保存？
当你在 Scene 视图中把关卡捏到完美后：
1. 点击顶部菜单：`MarioTrickster -> Level Builder -> Bake Scene to ASCII (Clipboard)`。
2. 系统会自动扫描场景，把所有的地形、你复制的道具、以及拖拽好的移动平台参数（转为 `# Override` 标签），全部逆向烘焙成 ASCII 文本，并**自动存入你的剪贴板**。
3. 打开你的代码或文档，直接 `Ctrl+V` 粘贴，大功告成！

---

## 2. 🩺 【体检流】：关卡去重与物理死路验证

策划写的关卡模板如果不通怎么办？解法重复怎么办？一键体检帮你把关。

### 📌 如何开始体检？
1. 点击顶部菜单：`MarioTrickster -> AI Arena -> Level Template Validator (QA)`。
2. 点击醒目的蓝色大按钮：**🚀 Run Full Regression & Deduplication**。

### 📌 界面上的四个区块代表什么？
- ❌ **【Block 1: Broken Levels】（红色）**：物理死路。说明这个关卡按照当前的物理红线（跳跃极限）根本无法通关。
- 🔄 **【Block 2: Redundant Templates】（黄色）**：解法重复。说明这几个关卡虽然长得不一样，但通关的“语义指纹”（博弈解法）完全一致，建议精简。
- ⚠️ **【Block 3: Untagged Templates】（灰色）**：缺标签。提醒策划去代码里把 `# Budget`, `# TrapRoles` 等设计意图标签补齐。
- ✅ **【Block 4: Passed Levels】（绿色）**：健康关卡。物理可达且标签规范的完美模板。

### 📌 遇到死路报错怎么修？
在 ❌ Broken Levels 区块中，每个报错条目旁边都有一个 **`[Copy AI Fix Prompt]`** 按钮。
点击它，系统会自动把包含“错误坐标、极限距离、修改建议”的 Prompt 写入剪贴板。你只需把这段话直接发给大模型（如 ChatGPT），它就会帮你改好关卡。

---

## 3. 🤖 【跑测流】：AI 画像互搏与大模型问诊

不需要真人测试，让不同性格的 AI 替你打几百盘，并出具专业诊断报告。

### 📌 第一步：捏人与挂载
1. **捏人**：在 Project 面板找配置文件：`Assets/Scripts/Core/BotPersonaConfigSO.cs`（或者右键 `Create -> MarioTrickster -> Bot Persona` 新建）。在这里你可以调节反应延迟、风险偏好，捏出“手残党”或“贪刀老阴比”。
2. **挂载**：将配置好的 SO 拖给场景中 Mario 或 Trickster 的 `InputProvider`，或者在 Level Studio 的 AI Arena 面板中指定。

### 📌 第二步：挂机与看战报
1. 在 AI Arena 面板点击开始挂机跑测。
2. 跑完后，点击 **Print Match Report** 按钮。
3. 结构化的 JSON 全息战报会自动保存在：`项目根目录/docs/validation/`（或 `reports/ai_arena_reports/`）目录下，文件名为 `MatchReport_xxx.json`。

### 📌 第三步：大模型问诊
1. 点击顶部菜单：`MarioTrickster -> AI Arena -> AI Test Analyst (LLM)`。
2. 在下拉框中选中你刚才生成的 JSON 战报。
3. 点击 **✨ Analyze Selected Report**。系统会直接把战报喂给大模型，几秒钟后，你就能在下方看到大白话的【对局评价】、【致命病灶分析】和【Actionable 修改建议】。

---

## 4. 🎛️ 【调参流】：手感与对抗节奏真理源

想要调节跳跃手感或技能冷却，千万不要去改代码！所有核心参数都已抽离为 SO (ScriptableObject)，并且支持 **PlayMode 实时拖拽生效**。

### 📌 Mario 手感去哪调？
- **路径**：`Assets/Scripts/LevelDesign/PhysicsConfigSO.asset`（如果没有请通过 `Create -> MarioTrickster -> Physics Config` 创建）。
- **管辖范围**：最大速度、加速度、空中摩擦力、重力倍率等纯物理手感。

### 📌 Trickster 节奏去哪调？
- **路径**：`Assets/Resources/GameplayLoopConfig.asset`。
- **管辖范围**：最大能量、变身消耗、操控消耗、热度衰减、扫描范围等核心博弈节奏。

> 💡 **Tip**：在 Unity 运行游戏（PlayMode）时，直接在 Inspector 面板拖动这些 SO 的滑块，游戏里的手感会立刻变化，调到满意为止！

---

## 5. 🛡️ 【防腐流】：美术绝对安全换皮

程序写好的精妙碰撞盒，绝不能被美术切图的尺寸给毁了。

### 📌 美术换皮的绝对安全机制
当你使用顶部菜单 `MarioTrickster -> Apply Art to Selected` 把商业素材“穿”到白盒上时，系统会自动挂载 `SpriteAutoFit` 脚本。
- **防腐原理**：碰撞体尺寸（物理真相）被 `PhysicsMetrics` 死死冻结。视觉 Sprite 只是纯粹的装饰，它会自动拉伸或平铺（Tiled）去适应碰撞体。
- ⚠️ **禁忌**：`SpriteAutoFit` 必须挂在 `Visual` 子节点上，**绝对不能挂在 Root 节点**，否则会破坏物理结构！

### 📌 发版前的强制体检
如果有人手贱动了碰撞盒怎么办？发版打包前，请务必执行：
1. 点击顶部菜单：`MarioTrickster -> Art Pipeline -> Validate Physics Boxes (Anti-Corruption)`。
2. 系统会一键扫描全图的 Mario、Trickster、SpikeTrap、BouncyPlatform 等核心物体。
3. 一旦发现碰撞盒的 size 或 offset 与 `PhysicsMetrics` 的真理值不符，系统会**强制重置**并标脏场景，确保物理红线万无一失！

---
*“愿你的关卡没有 Bug，愿你的 AI 永远聪明。”*
