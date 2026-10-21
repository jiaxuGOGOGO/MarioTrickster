# 世界乐园接续文档（S248–S254 完整上下文）

> 写给换账号/新对话后接手的 AI 和用户本人。读完这一份就能接着升级，不需要任何旧聊天记录。
> 最后更新：2026-10-21，S254 完成后。每轮交付都要更新第 1、3、9、10 节。

## 1. 一句话现状

项目已经从“横版捣蛋 + 小镇日程”改造成**弥留之国式的世界乐园**：玩家进入由多个会场组成的世界，每个会场有公开规则、花色与难度；在俯视地图里限时调查线索、选择出口、躲避会巡逻和追人的“鬼”，或在全屏博弈桌上与多个有人格的 NPC 进行多人博弈（数字猜心、看不见的标记、分还是偷；有命数、出局、随出局追加的规则）；结果触发反转（揭示时把你发现过的线索逐张翻成“当时以为 → 其实”）和结局（GAME CLEAR / GAME OVER 章），再按作者设定的链接进入下一个会场，最后到达主办者的房间选择离开或留下；进度可以保存并继续。S252 起有演出层：霓虹扑克牌亮灯入场、倒计时、出局激光、程序合成音效、电影画幅的剧情字幕卡。默认主世界是原创六会场“空白之城”。世界、会场、阶段、线索、出口、追逐者、博弈参数、结局标签全部可以在 Unity 世界工坊和网页设计台里自定义。**剧情带入模拟器**让五种人格的模拟玩家只凭公开信息真实走完世界，并对照作者预期检验体验，这是 S251 起的体验验收标准。S253 起有第二个运行端**网页试玩**（网页设计台顶部 ▶ 网页试玩，或 `index.html#play`）：不用 Unity 就能在浏览器里完整玩一遍，规则内核与 C# 逐状态一致，并由真实浏览器自动试玩脚本截图检验。**S254 起正式端定位为 Windows 电脑游戏**：一键构建 exe（世界工坊 / 开始页“构建 Windows 版”，或 Unity 关着时双击 `BUILD_WINDOWS_GAME.cmd`），启动先到标题页，游戏中 Esc 菜单（设置界面大小 / 音量 / 全屏、重来本会场、回到标题、退出），界面按分辨率自动缩放；创作上有会场模板、复制会场、▶ 只玩这个会场、网页“会场体检”红黄绿即时反馈，网页与 Unity 通过同一个「我的世界」文件（`Assets/Resources/WorldPark/MyWorld.worldpark.json`）自动同步。S248–S254 改动的提交状态见 `AGENTS.md`“当前状态”；Unity 原生测试与 Play 实机、Windows 版实际构建受许可证阻挡，**还没有人在 Unity 里实际玩过**（自测清单在 `docs/step1/S254_WINDOWS_AND_AUTHORING.md` 末尾）；网页试玩也**还没有真人玩过**。

## 2. 最终目标与设计原则

用户的最终目的（原话要点）：建立一个以漫画《弥留之国的爱丽丝》式世界观为灵感的“乐园”，并且像现在的项目一样可以自定义，从而有更大的创作空间、更少的限制、发挥更多创意。S251（2026-10-20）进一步明确：**要实现原作的游戏设计和那种效果，方便以后自定义创造类似的新游戏；现有项目要能最方便地还原漫画/电视剧里的效果；并且要真实地模拟剧情去测试体验是否达到预期，以此为标准构建框架和主体内容，再考虑开发新的关卡内容。**

由此推出的设计原则（第一性原理，后续改动都用它们检验）：

1. **会场是基本单位。** 一个会场 = 一段有公开规则、可获取信息、有效选择和承担后果的体验。世界 = 会场 + 会场之间的链接。
2. **先有信息，再有选择。** 线索必须在选择之前可以被玩家真实获得（走近、看见、调查），真相放在揭示阶段，不提前泄露。
3. **选择要有后果，失败也是剧情。** 结果不只有过关/失败；作者可自定义结局标签，失败可以通往不同的会场。
4. **结果来自真实活动，不是文字彩排。** 探索出口的胜负由实际移动与交互决定；房间结果由横版对局决定。
5. **创作摩擦越低越好。** 能在图上点就不要手写坐标；坏数据要在编辑时就被拦住并说明原因。
6. **原创内容。** 题材元素（如“花色”“签证”“难度”）只作为作者可自由填写的字段，不硬编码原作的角色、名称或规则；默认示例全部是原创内容。
7. **规则只有一份语义。** C# 与网页对同一份世界包、同一串操作必须得到完全相同的状态（有跨端对比测试守护）。
8. **体验要能被检验。** 每个会场都要回答“谁该活、谁该死、为什么”，写进作者预期文件，由剧情带入模拟器用真实内核检验；至少一种只用公开信息的策略能活（公平），至少一种策略会出局（威胁）。逻辑测试通过不等于体验达标。
9. **NPC 只按人格与公开信息行动。** 博弈结果必须确定、可复现、可推理；不让大模型参与胜负判定（研究表明大模型难以跨回合保持一致策略），大模型最多用于台词的“味道”。

## 3. 用户决策记录（按时间，均为 2026-10-19）

| 时间 | 用户要求 | 结果与产物 |
| --- | --- | --- |
| 11:50 | 提交《弥留之国改造方案》，要求从第一性原理审计其关卡设计与编辑游戏性 | 审计报告：`outputs/borderland-audit-20261019/` |
| 13:48 | 当前项目可作为弥留之国的一关（横版小镇），研究漫画设定后再决定最恰当的升级；开拓更多反转关卡 | 漫画研究与“小镇即会场”方案：`outputs/borderland-manga-concept-20261019/` |
| 14:05 | 最终目的：建立漫画式世界观乐园并可自定义；分阶段改造；**先备份再升级** | S248 世界乐园作者底座；升级前完整备份 `outputs/park-upgrade-20261019/backup/` |
| 15:13 | **不需要保持独立旧模式**，已备份就大胆升级、剔除冗余；评估视角（横版/俯视），弊大于利就换 | S249：世界成为唯一主体验，主视角定为**二维斜俯视**，横版只作为需要纵深时的挑战片段；顶层菜单精简为 4 个；旧日程守宝不再默认启动 |
| 16:22 | 问“当前目录是否已升级、升级包干什么用” | 已说明当前目录即已升级；升级包只是迁移/回退用。**此后不再另做升级包，直接改当前目录** |
| 16:26 | 从最终目的出发继续升级完善 | S250：点位画布、自定义结局标签、世界检查点与继续旅程、三会场示例 |
| 17:12 | 将来可能换账号，但仍在本地这个位置继续升级，要完整文件方便接续 | 本文件、根目录 `AGENTS.md`、`Tools/WorldParkVerify/` 一键验证 |
| 2026-10-20 10:52 | 最终诉求：实现弥留之国的游戏设计与效果、方便以后自定义类似新游戏；用真实剧情模拟检验体验，以此为标准构建框架和主体内容；从第一性原理拆解原作与项目，允许删除冗余、全方位重构；搜集全网资料 | S251：博弈内核与 `game` 阶段、主世界“空白之城”、剧情带入模拟器；报告 `outputs/borderland-framework-20261020/`；技术说明 `docs/step1/S251_BORDERLAND_FRAMEWORK.md` |
| 2026-10-20 13:52 | 做黑桃体力类会场（鬼抓人）；补演出（亮灯入场、倒计时、出局画面和音效）；澄清“开始世界”是游戏还是模拟 | S252：追逐者点位与内核、♠5 鬼抓人、演出层（`WorldParkStage*`/`WorldParkSfx`）；澄清见 `docs/step1/S252_GAME_FEEL_SPADES.md` 第 2 节 |
| 2026-10-20 15:01 | **还是要做游戏，不是文字阅读**；增强代入感、要有反转；先还原（不必完美），以后再开拓新关卡 | S252：全屏博弈桌（逐个亮牌、命灯、出局激光）、剧情字幕卡与反转卡、三个博弈会场的入场观察线索；剧情模拟加博弈桌/字幕卡/每会场反转检验 |
| 2026-10-20 16:03 / 17:05 | “继续”（延续上一条） | S253：网页试玩（Unity 许可证未激活时也能真正玩到）、网页探索内核与试玩模型的跨端对比、真实浏览器自动试玩与截图修正；见 `docs/step1/S253_WEB_PLAY.md` |
| 2026-10-20 17:12 | 问“是页游还是文字游戏”；对照最终诉求结合全网资料全面审计；**优先 Windows 电脑端（Unity PC）**，其他后续；全方位验证优化；然后推送 GitHub | S254 审计 `outputs/borderland-audit-s254/AUDIT.md`；Windows 版构建、标题页、Esc 菜单、设置、界面缩放、重来本会场 |
| 2026-10-20 17:14 / 10-21 09:45 | Unity 不能自己进去测就把其他都做好，**用户自己进去测**；前提是设计关卡、网页构思关卡必须方便，能集中精力做新关卡或调整好框架 | S254：会场模板、复制会场、只玩这个会场、会场体检、网页 ↔ Unity「我的世界」自动同步、Unity 自测清单；见 `docs/step1/S254_WINDOWS_AND_AUTHORING.md` |

视角决策依据（S249）：俯视能同时看见路线、遮挡、线索和出口，适合“观察 → 推理 → 选择”的会场；横版擅长跳跃和纵深陷阱，保留为片段（`room` 阶段）。详见 `docs/step1/S249_WORLD_MAINLINE.md`。

## 4. 用户怎么用（当前入口）

- Unity 菜单顶层 4 项：`📖 开始页 Start Here`（Ctrl+Alt+H）、`开始世界 Play World`、`世界工坊 World Workshop`、`测试与诊断`。S254 在世界工坊子菜单加了 `构建 Windows 版 Build Windows`（顶层仍是 4 项）。
- 开始页主按钮：**开始世界**、**继续旅程**、**世界工坊**、网页创作、测试与诊断；第二行显示「我的世界」状态和 **构建 Windows 版**。
- 开始世界：世界工坊窗口里没有草稿时优先读「我的世界」`Assets/Resources/WorldPark/MyWorld.worldpark.json`（网页“🔗 写进 Unity 项目”或世界工坊“保存为我的世界”写入），没有才读主世界 `Borderland.worldpark.json`（“空白之城”）；两个内置世界（主世界与旧示例 `Demo.worldpark.json`）在窗口里禁止被覆盖导出。
- 世界工坊（`Editor/WorldParkWindow.cs`）：编辑世界/会场/阶段/线索/链接；explore 阶段下方是点位画布；game 阶段可编辑玩法、轮数、命数、比例、目标分、种子、追加规则开关与参加者人格（内核检查实时提示）；可打开地图子工具画会场地图并应用回草稿；导入/导出 `.worldpark.json`；工具栏“继续旅程”。S254：工具栏下“我的世界”一栏（● 未保存提示、载入我的世界、保存为我的世界、网页设计台、构建 Windows 版；网页写进了新版本时，草稿没改动就自动载入，有改动就黄色提示）；会场区“复制此会场”、模板下拉 +“用模板新建会场”、“▶ 只玩这个会场”。
- Windows 版（S254）：`Builds/Windows/<世界id>/MarioTrickster.exe` + `先读我.txt`；启动标题页（继续旅程 / 开始新旅程 / 设置 / 退出）；Alt+Enter 全屏；存档在 `%USERPROFILE%\AppData\LocalLow\<公司>\<产品>\WorldPark\`。命令行：`BUILD_WINDOWS_GAME.cmd`（`-worldPack`、`-worldOut` 可选）；原生测试：`RUN_UNITY_TESTS.cmd`。
- 游戏内按键（探索）：WASD 移动、E 靠近调查/选择出口、N 线索笔记、C 镜头、Esc 菜单（S254 起剧情 / 博弈 / 探索都能按：继续、设置、保存检查点（安全阶段）、重来本会场、回到标题、退出）。GAME OVER 结局底栏有“重来本会场”；结局“保存检查点，回到标题”。
- 博弈阶段（game，S252 起是全屏博弈桌）：滑块写数字 / 点四种花色之一 / 点分或偷；别人对你说的话显示在他的座位旁；提交后所有人同时亮牌、逐张翻开，标出谁失去命、谁出局（激光）、新规则；右上“规则与记录”随时查完整公开记录；点击或空格跳过亮牌；整局结束后确认才提交并自动存检查点。
- 剧情 / 揭示 / 结局 / 进入房间前（S252 起是全屏字幕卡）：正文逐字出现（点击显示全部），揭示时反转卡逐张滑入，结局盖章并把下一个会场显示成可点的门（关着的门写明条件）；左下“线索本”“旅程”，空格或右下按钮继续。
- 探索里的鬼（S252）：看得见视锥；被看见会响警报、画面边缘闪红；站在高草里不动可以躲；限时最后几秒大字倒计时。
- 网页设计台：`Tools/LevelStudioWeb/index.html`（构建生成的单文件）→ “世界乐园”页，功能同 Unity 世界工坊（含点位画布、博弈参数 JSON 编辑；彩排可真实逐轮打一局），内置示例为主世界。S254：“🔗 写进 Unity 项目”（选一次项目文件夹后每次改动 1 秒后自动写「我的世界」）、模板下拉 +“＋ 用模板新建”、“▶ 只玩这个会场”、右侧“会场体检”卡（`venue-check.js`，红 / 黄 / 绿）。
- **网页试玩（S253）**：双击 `Tools/LevelStudioWeb/index.html` → 顶部 **▶ 网页试玩**（玩当前世界草稿；没改过就是“空白之城”），或“世界乐园”页 **▶ 网页试玩这个世界**；`index.html#play` 直接玩内置主世界。WASD / 方向键移动，E 调查 / 开门，Tab 线索本 / 博弈记录，Esc 暂停（继续、重来本会场、回到标题、退出），顶栏声音开关；剧情 / 揭示 / 结局阶段自动存进度到本浏览器，标题页可继续旅程。只支持键盘 + 鼠标。
- 剧情带入模拟：一键验证会自动运行；单独运行 `dotnet run --project Tools\WorldParkVerify\Story\WorldParkStory.csproj`；报告在 `.work/worldpark-verify/story/模拟报告.md` 与 `旅程_*.md`；作者预期在 `Tools/WorldParkVerify/Story/Borderland.expect.json`（人格、路线偏好、每会场预期结果与理由、旅程终点、结局多样下限）。

## 5. 架构地图

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/WorldPark/WorldParkPack.cs` | 世界包数据模型、严格 JSON 解析（`Parse`/`ToJson`）、结构预检 `Validate`、会话 `Start` 与会话操作 `Continue`/`ResolveRoom`/`ObserveClue`/`ResolveExplore`/`Travel`/`CanTravel`。纯 C#，不依赖 Unity |
| `Assets/Scripts/WorldPark/WorldParkExplore.cs` | 探索内核：网格移动、碰撞、近身与视线判定、调查与出口交互、计时。纯 C# |
| `Assets/Scripts/WorldPark/BorderlandGame.cs` | S251 博弈内核：`Validate(def)`、`Run(def, choices)`（choices 少于所需轮数时返回下一轮公开信息）；beauty/mark/split 三模式、八种人格、命数与出局、规则追加、逐轮事实（供模拟器统计）。纯 C#，整数 + 固定伪随机 |
| `Assets/Scripts/WorldPark/WorldParkProgress.cs` | 世界检查点：内容指纹 `Fingerprint`、保存 `Save` 与恢复（重放动作并比对状态）、大小/深度上限。纯 C# |
| `Assets/Scripts/WorldPark/WorldParkSaveStore.cs` | 检查点文件读写：`persistentDataPath/WorldPark/<指纹>.worldsave.json`，临时文件 + 读回校验 + 替换，旧档 `.previous` |
| `Assets/Scripts/WorldPark/WorldParkRuntime.cs` | Unity 运行器：世界会话生命周期、场景切换、房间/探索结果回收、全屏博弈桌 `DrawGameTable`（亮牌演完再放激光与结果，整局结束才提交）、全屏字幕卡 `DrawSceneCard`、入场亮灯/结局盖章 `AnnounceStage`、安全阶段自动存档、暂停菜单 |
| `Assets/Scripts/WorldPark/WorldParkExploreRuntime.cs` | Unity 探索渲染与输入：俯视画面、建筑遮挡、交互提示、鬼的视锥与“!/?”、脚步声、倒计时与警报（与运行器同一 GameObject） |
| `Assets/Scripts/WorldPark/WorldParkStageCues.cs` / `WorldParkStage.cs` / `WorldParkSfx.cs` | S252 演出层：纯逻辑演出提示（亮灯、倒计时、激光、结局章）/ IMGUI 叠加层 / 程序合成音效 |
| `Assets/Scripts/WorldPark/WorldParkTableModel.cs` / `WorldParkTable.cs` | S252 博弈桌：纯逻辑排版（座位、气泡、亮牌时间轴、扣命、出局、结算字）/ IMGUI 绘制 |
| `Assets/Scripts/WorldPark/WorldParkSceneModel.cs` / `WorldParkScene.cs` | S252 剧情字幕卡：纯逻辑（反转卡、错过的线索、线索本、门、出字时间轴）/ IMGUI 绘制 |
| `Assets/Scripts/Editor/WorldParkBuilder.cs` | 生成隔离的世界场景并运行：`PlayMain`、`TryRun(pack, out report, resume)`、`CheckRunReadiness`；失败前不改场景与 Build Settings |
| `Assets/Scripts/Editor/WorldParkMainline.cs` | 用现有地图预览世界探索 |
| `Assets/Scripts/Editor/WorldParkWindow.cs` | 世界工坊窗口：`Open`、`PlayCurrent`、`ContinueCurrent` |
| `Assets/Scripts/Editor/WorldParkSpatialEditor.cs` | Unity 点位画布：放置/移动/删除/撤销重做，出生点可达性检查 |
| `Assets/Scripts/Editor/StartHereWindow.cs` | 开始页（主按钮入口） |
| `Assets/Scripts/LevelDesign/FeatureMap.cs` | “项目能做什么”的唯一来源 → 生成 `docs/FEATURE_MAP.md` |
| `Assets/Resources/WorldPark/Borderland.worldpark.json` | **主世界“空白之城”**：♣3 三扇门（35 秒探索）→ ♦5 数字猜心 / ♥7 看不见的标记 / ♠5 鬼抓人（45 秒追逐）→ ♥9 分还是偷 → K 主办者的房间。数据唯一来源是这个 JSON（生成脚本只是本机草稿 `.work/borderland-framework-20261020/make-world.cjs`，被 Git 忽略；之后请直接用世界工坊改 JSON） |
| `Assets/Resources/WorldPark/Demo.worldpark.json` | 旧“幕间乐园”三会场示例：格式样例与 S248–S250 回归夹具，不再是默认世界 |
| `Tools/LevelStudioWeb/worldpark.js` | 网页版世界规则（与 `WorldParkPack.cs` 同语义） |
| `Tools/LevelStudioWeb/borderland.js` | 网页版博弈内核（与 `BorderlandGame.cs` 逐字同语义；`build.py` 把它内联在 `worldpark.js` 之前） |
| `Tools/LevelStudioWeb/worldpark-app.js` | 网页世界工坊表单与彩排；S253 起绑定 ▶ 网页试玩入口与 `#play` |
| `Tools/LevelStudioWeb/explore.js` | S253 网页探索内核（全局 `WPExplore`）：逐行移植 `WorldParkExplore.cs` 与 `OverworldMap` 的移动、碰撞、视线、寻路、追逐者 |
| `Tools/LevelStudioWeb/play-model.js` | S253 试玩纯逻辑（全局 `WPPlayModel`：`Cues`/`Scene`/`Table`），移植三个 C# 演出模型 |
| `Tools/LevelStudioWeb/play.js` | S253 网页试玩层（全局 `WPPlay.open(json, opts)`）：标题、入场、字幕卡、探索画布、博弈桌、结局门、总结；合成音效；localStorage 存档。只做画面，不推进规则 |
| `Tools/LevelStudioWeb/worldpark-spatial.js` | 网页点位画布（含 `.test.js`） |
| `Tools/LevelStudioWeb/build.py` | 由 `shell.html` + 各 js 生成 `index.html`（不要手改 index.html） |
| `Assets/Tests/EditMode/WorldPark*Tests.cs` | Unity EditMode 回归（Pack/Explore/Journey/Progress/Spatial/Editor） |
| `Tools/WorldParkVerify/` | 不需要 Unity 的一键验证（见第 8 节） |
| `Tools/WorldParkVerify/Story/` | 剧情带入模拟器（`Program.cs`）与作者预期（`Borderland.expect.json`） |
| `Tools/WorldParkVerify/explore-parity.cjs` / `play-parity.cjs` | S253 跨端对比：网页探索内核 ↔ C#（Snapshot `explore` 子命令）；网页试玩视图 ↔ C# 三个演出模型（Snapshot `views` 子命令） |
| `Tools/WorldParkVerify/web-play-smoke.cjs` | S253 真实浏览器自动试玩：无界面 Edge/Chrome + 调试协议，真实按键走完主世界并截图 |

旧子系统（横版房间、马里奥 AI、小镇机关、台词、美术）仍在，作为子工具与片段使用，规则见 `docs/AI_CONTINUE_PACK/SKILL.md` 与 `docs/FEATURE_MAP.md`。

## 6. 世界包格式（`.worldpark.json`，版本 `v: 1`）

- **Pack**：`id`、`name`、`premise`（世界设定）、`entryVenue`、`venues[]`、`links[]`。
- **Venue**：`id`、`name`、`setting`、`role`（`trickster` 或 `player`）、`suit`（自由文字）、`difficulty`、`rules`（公开规则）、`entry`（首个阶段）、`nodes[]`、`clues[]`。
- **Node（阶段）**：`id`、`kind`、`name`、`text`，按类型使用：
  - `story` / `reveal`：`next`；
  - `room`（横版片段）：`room`（关卡库名称）、`onWin`、`onLose`；
  - `explore`（俯视探索）：`mapText`（与小镇工坊同一文字地图格式，`T` = 唯一出生点）、`timeout`（秒，0 = 不限时）、`points[]`、`onWin`、`onLose`；
  - `game`（多人博弈，S251）：`game`（见下方 GameDef）、`onWin`、`onLose`；会话用 `ResolveGame(choices)` 结算，choices 必须正好走完整局；
  - `ending`：`result`（**作者自定义标签**，`clear`/`fail` 兼容；不能为空，不能是 `any`）；
  - `concept`：新玩法草稿，可保存但不能运行。
- **GameDef**：`mode`（`beauty` 数字猜心 / `mark` 看不见的标记 / `split` 分还是偷）、`rounds`（1–20）、`lives`（1–20）、`ratio`（beauty：目标 = 平均 × ratio%）、`goal`（split：目标分）、`seed`（固定伪随机种子）、`escalate`（beauty：1 = 有人出局后每轮追加一条规则，只剩两人时宣布决斗规则）、`seats[]`（2–8 个 `{id, name, persona}`，恰好一个 `persona: player`；人格：`naive` 直觉、`reasoner` 推一层、`deep` 深推、`adaptive` 跟随、`liar` 说谎、`loyal` 老实、`mirror` 以牙还牙）。玩家选择：beauty 为 0–100 的整数字符串，mark 为 `♠`/`♥`/`♦`/`♣`，split 为 `split`/`steal`；检查点动作为 `{kind: "game", value: "选择1|选择2|…"}`（逐轮选择用 `|` 连接，1–4096 字符）。mark/split 预检要求至少一个会欺骗的 NPC（否则没有“看穿”的空间）。
- **ExplorePoint**：`id`、`kind`（`clue` / `exit` / `chaser`）、`label`、`x`、`y`（x 从左 0，y 从下 0，必须是从 T 走得到的可通行格）、`clue`（关联线索）、`outcome`（出口：`win` / `lose`）、`requires[]`（出口需要先发现的线索 id）。**chaser（S252，鬼）** 另有 `speed`（十分之一格/秒，0 = 默认 34，允许 10–80；巡逻 = 追速 × 0.6）、`sight`（格，0 = 默认 7，允许 1–20，视锥半角 60°）、`route[]`（`"x,y"` 巡逻点 ≤32，须可通行且 T 可达；空 = 原地守、每 1.5 秒转 90°），这三个字段只在 chaser 上导出；每阶段最多 4 个鬼；离 T 少于 4 格给警告；抓捕半径 0.6 格。
- **Clue**：`id`、`name`、`hint`（玩家可见；写成玩家当时会怎么理解）、`truth`（揭示时才显示；写成反转后的真相）、`trigger`（`stage` = 到达阶段即发现；`interact` = 必须在 explore 阶段现场调查）、`foundAt`、`revealAt`。揭示阶段的字幕卡只把“已发现且 revealAt 指向本阶段”的线索翻成反转卡；没发现的只显示名字（“你错过了”）。博弈会场没有地图时，用 `stage` 线索（`foundAt` = 入场阶段）埋反转。
- **Link**：`from`、`to`、`requires`（来源会场的结局标签，或 `any`）。来源会场永远产生不了该标签时给警告 `link_result_unreachable`。
- 上限：JSON 262144 字符；会场 64；每会场阶段 256、线索 256；链接 256；总阶段 512；每阶段点位 256；一次会话 256 步。检查点：动作 2048、文件 262144 字符。

## 7. 同步规则（改规则时必须遵守）

1. 世界规则的任何改动（解析、预检、会话语义、诊断码）必须同时改 `WorldParkPack.cs` 和 `worldpark.js`，并在 `Tools/WorldParkVerify/parity.cjs` 加覆盖它的用例，跨端差异必须为 0。
1b. 博弈内核的任何改动必须同时改 `BorderlandGame.cs` 和 `borderland.js`，在 `parity.cjs` 加逐轮用例；改了内核或会场参数后，剧情带入模拟要重新通过（或经用户同意修改 `Borderland.expect.json`）。
2. 点位放置规则（可通行、出生点可达、坐标系）Unity 画布与网页画布必须一致，两边测试都要加。
2b. 探索与追逐者的运行（移动、碰撞、视线、巡逻、追逐、搜寻、抓捕）在 C# `WorldParkExplore.cs` 与网页 `explore.js` 两端，**必须同语义**（S253 起；此前“只在 C# 运行”的约定作废）。改任何一边都要同改另一边，在 `Tools/WorldParkVerify/Explore` 加断言、在 `explore-parity.cjs` 加用例（差异必须为 0），并重跑剧情模拟看“鬼的威胁 / 追逐张力”。
2c. 演出层、博弈桌、字幕卡只读会话状态，不得推进流程或改结果；它们的纯逻辑部分（`*Cues`/`*Model`）由剧情模拟器逐轮核对，改了要保持模拟器检验通过。
2d. 网页 `play-model.js` 与 C# `WorldParkStageCues.cs`/`WorldParkSceneModel.cs`/`WorldParkTableModel.cs` 同语义（`play-parity.cjs` 守护）；`play.js` 只管画面、输入、音效与存档，可以只改网页排版（例如座位以头像中心对齐），但不得改模型给出的内容与时序。改了试玩画面要跑 `web-play-smoke.cjs` 并看截图。
3. 检查点只在 `story`/`reveal`/`ending` 保存，`room`/`explore` 进行中不保存（防止读档刷结果）。改存档格式要升 `WorldParkProgress.Version` 并保持旧档“拒绝而不覆盖”。
4. 新菜单只能放进现有顶层（当前 4 个）；新功能登记到 `FeatureMap.cs` 并重新生成文档。
5. 网页只改源文件（`shell.html`、`*.js`），再运行 `build.py` 生成 `index.html`。

## 8. 验证

一键（仓库根目录，不需要 Unity 许可证）：

```powershell
powershell -ExecutionPolicy Bypass -File Tools\WorldParkVerify\run_all.ps1
```

| 步骤 | 内容 | S254 结果（2026-10-21 10:10） |
| --- | --- | --- |
| Unity引用编译 | 用 Unity 真实程序集引用编译运行时/编辑器/测试三程序集（含全部 WorldPark 文件） | PASS |
| S248核心规则 | 解析、预检、分支、揭示、链接、上限 | 288 断言 PASS |
| S249探索与真实碰撞 | 探索内核与默认地图实走；S252 起含追逐者（巡逻、视野、草丛、搜寻、抓捕、防冻结） | 99 PASS |
| S250检查点模块 | 篡改、超限、重复事件、活动中途拒存；S254 起含重来本会场 `RetryVenue` | 60 PASS |
| S250三会场旅程实走 | 3 条真实移动旅程，每个安全阶段存读并比对 | 212 PASS |
| S250 Unity点位画布规则 | 编译真实 `WorldParkSpatialEditor.cs` 运行其 EditMode 用例 | 2/2 PASS |
| 网页↔C#跨端一致 | 同包同动作逐状态对比（含博弈逐轮翻牌数据） | 61 世界用例 + 35 博弈用例，0 差异 |
| S253网页探索内核跨端 | 同包同输入逐步对比网页 `explore.js` 与 C# 探索内核（含鬼的巡逻、追逐、搜寻、抓捕、无效输入） | 11 用例 2262 步 62958 项，0 差异 |
| S253网页试玩画面跨端 | 8 条自动旅程覆盖 6 个会场，逐项对比网页与 C# 的演出提示、字幕卡、博弈桌视图 | 61450 项，0 差异 |
| S254会场体检 | `venue-check.cjs`：主世界 6 会场必须红 0；5 个坏样例（限时 3 秒、鬼放角落、全老实人、目标 999、无规则无揭示）必须被抓到 | 19/19，红 0 黄 0 绿 20 |
| S251剧情带入模拟 | 五人格全程旅程 + 30 场单会场挑战，对照作者预期、公平、威胁、规则生效、限时压迫、鬼的威胁、追逐张力、结局多样、检查点一致；S252 起还核对博弈桌每轮画面、字幕卡内容、每个会场的反转 | 46/46 PASS |
| 网页博弈彩排冒烟 | 用桩 DOM 加载真实网页脚本，逐轮打完 ♦5 并提交，检查 ♥7/♥9 按钮与坏 JSON 防护；S254 起含体检面板与模板下拉 | 14/14 PASS |
| 网页点位画布 / 语法 / 重建 | 22 断言、node 语法、重建 index.html 并检查内联脚本 | PASS |
| S253真实浏览器试玩 | 无界面 Edge 真实按键走完 ♣3→♠5（先被鬼抓→GAME OVER→重来）→♥9→K→总结；S254 起再走创作流程（会场体检、模板新建 `v_chase_my`、只玩这个会场）；收集脚本错误、截图；无浏览器 / 连不上调试端口时 SKIP | 4 会场 + 创作流程、错误 0 |
| 功能地图文档同步 | `docs/FEATURE_MAP.md` 是否与 `FeatureMap.cs` 一致（85 项） | PASS |

结果写入 `.work/worldpark-verify/last-run.txt`。重新生成功能地图文档：

```powershell
dotnet run --project Tools\WorldParkVerify\Snapshot\WorldParkSnapshot.csproj -- map docs\FEATURE_MAP.md
```

一键验证**不包含**：Unity 原生 EditMode/PlayMode、Play 实机行走与存读档、博弈桌/字幕卡/演出/标题页/Esc 菜单的实际屏幕排版与音效、Windows 版实际构建与运行、Unity 编辑器里的真实鼠标操作、网页“写进 Unity 项目”的文件夹授权、真人试玩。能打开 Unity 时：按 `docs/step1/S254_WINDOWS_AND_AUTHORING.md` 末尾的自测清单走一遍（Unity 关着时可先双击 `RUN_UNITY_TESTS.cmd` 跑原生 EditMode）。网页可在浏览器打开 `Tools/LevelStudioWeb/index.html` 实测；S253 起单独运行真实浏览器自动试玩：`node Tools\WorldParkVerify\web-play-smoke.cjs`（截图在 `.work/worldpark-verify/web-play/`；在受限命令环境里要允许本机回环网络）。剧情模拟的逐帧追逐轨迹：设环境变量 `STORY_TRACE=1` 再运行模拟器，输出到 `.work/worldpark-verify/story/trace/`。

旧的 `docs/AI_CONTINUE_PACK/scripts/verify.sh` + `scripts/sim/Check.cs` 是早期云端沙盒用的整体检查，S248 后未在本机运行，可能含已被取代的旧断言（例如顶层菜单数量）；世界层以 `Tools/WorldParkVerify` 为准。

## 9. 已知风险与限制

1. **提交状态以 `AGENTS.md`“当前状态”为准**（S254 按用户 17:12 要求推送 GitHub）。不要做丢弃工作副本的 Git 操作。以后要上 GitHub 时，请用户双击 `SAVE_MARIOTRICKSTER.cmd`（它会列出需审阅的新文件），或经用户同意后提交。Git 里同时有大写 `Tools/`（3 个旧 Python 脚本 + S254 起的 `Tools/WorldParkVerify/`）和小写 `tools/LevelStudioWeb/` 两种写法（磁盘上都是 `Tools/`，Windows 不区分大小写，没有影响；GitHub 网页上会显示成两个目录）。往网页设计台加新文件时用 `tools/LevelStudioWeb/...` 路径 `git add`，保持同一目录。
2. 工作副本里约 123 个 Unity 早期自动生成的旧 `.meta` 未经审计，存档工具会提示，不要混进世界改动的提交。
3. Unity 批处理被许可证校验阻挡（2026-10-20 15:05 再试仍是 “No valid Unity Editor license found”）；S248–S253 的 Unity 原生测试与实机都未执行。解除办法：用户在 Unity Hub 登录 → 偏好设置 → 许可证 → 添加 → 免费个人版。
4. 世界包的网页 ↔ Unity 同步只走「我的世界」一个文件（S254）；世界包引用的房间/地图仍走旧的关卡同步（Inbox）或另行携带（世界分享打包是 P5）。网页“写进 Unity 项目”需要浏览器文件夹授权（Chrome / Edge），无法自动化测试。
5. `concept` 阶段（如列车类新玩法）只能保存为草稿，还不能运行。
6. Unity 生成的 `*.csproj` 可能不含新文件，所以编译检查用 `-IncludeWorldPark` 额外加入；Unity 打开后会自行重建它们。
7. 剧情模拟的人格是手写策略，不是真人也不是大模型；它检验的是公平、威胁、规则生效、时间压力、结局多样等代理指标，不能直接衡量“好不好玩”。♦5 只有 1/5 存活，难度是否过高要等真人试玩判断。
8. S252 的 Unity 博弈桌、字幕卡、演出层与鬼的画面，以及 S254 的标题页、Esc 菜单、设置、界面缩放，都是 IMGUI + 程序合成音效，只经过编译与纯逻辑核对，**没有在屏幕上看过**（S253 的网页试玩用同一套模型做了 HTML 画面并截图检查过，可作为 Unity 排版的参照，但两端画面代码不同）；排版、字号、颜色、音量可能需要按真机截图调整。Unity 官方不建议用 IMGUI 做正式玩家界面，长期应换 uGUI / UI Toolkit（P12）。博弈桌还没有作答倒计时；没有专门美术。
8d. Windows 版构建（`WorldParkBuilder.BuildWindows`）没有实际跑过：可能遇到首次构建很慢、批处理需要许可证；项目已在 Unity 打开时 `.cmd` 会提示改用编辑器内按钮。独立版的“重来本会场 / 回到标题”靠 `SceneManager.LoadScene` 重新载入剧情场景并按静态会话恢复，编辑器 Play 与独立版应一致，但都未实测。
8b. 剧情模拟里 evade（边躲边找）人格会“看 1 秒记住巡逻圈”，真人未必这么冷静；♠5 的鬼速 3.4 格/秒与 45 秒限时是否太难/太易要等真人试玩。
8c. 网页试玩只经过自动试玩与截图检查，**没有真人玩过**；只支持键盘 + 鼠标（无触屏）；存档在浏览器 localStorage，清浏览器数据会丢；音效是程序合成。
9. 旧子系统（横版守宝、马里奥 AI、小镇日程、旧云端检查脚本）未删除：Unity 场景/预制体按 GUID 引用脚本，删除前必须在能运行 Unity 的环境确认无引用。

## 10. 下一阶段路线（建议顺序）

**P4 新的会场目标类型。** S251 已完成（`game` 阶段：数字猜心、看不见的标记、分还是偷）。

**P5 世界分享。** 把世界包与引用的关卡、地图、素材打包导出/导入，导入时校验依赖完整、不覆盖用户已有内容。验收：在一台干净副本上导入后能直接开始世界。

**P6 真人试玩（当前最优先）。** S253 起可以先在网页试玩里由用户亲自走完“空白之城”，记录卡住、看不懂、太难或太易之处并写回 `Borderland.expect.json`。之后在可运行 Unity 的环境跑 EditMode/PlayMode，实际走完“空白之城”六会场与存读档，重点看博弈桌与字幕卡的排版是否易读、♠5 的鬼与限时、♦5 难度；根据试玩记录调整节奏、检查点时机和提示强度，并把真人结果写回 `Borderland.expect.json`。

**P7 黑桃·体力类会场。** S252 已完成（追逐者点位 + ♠5 鬼抓人；剧情模拟已能区分“只跑不看”被抓与“边躲边找”通关）。后续可加：鬼会听脚步声、多层楼、可关的门、手电筒。

**P8 演出层。** S252 已完成第一版（亮灯入场、倒计时、出局激光、结局章、程序音效、全屏博弈桌、剧情字幕卡与反转卡）。第二轮：博弈桌作答倒计时、入场过场镜头、出局时座位灯熄灭动画、专门美术与字体，按真机截图调整。

**P9 更多博弈积木。** 问卷式信任定价、独房式多轮问答、梅花团队资源分配；每加一种都要在剧情模拟里加人格与预期。

**P10 冗余删除。** 在 Unity 可运行后，确认旧子系统无场景引用，再成批删除并重跑验证（用户已授权删除冗余）。

**P11 签证天数（S254 审计第一条规则缺口）。** 原作核心压力源：跨会场的生存倒计时、通关按牌面数字加天数、归零即激光。建议做成世界包里可选的一项（关掉就是现在的玩法）；C# 与网页同改并加 parity 用例，剧情模拟加“签证压力”检验。改规则前先向用户确认。

**P12 Windows 版打磨。** 依用户自测反馈调整标题页 / 菜单 / 字幕卡排版；鬼的“最后看见你的位置”残影；Unity 世界工坊里也显示会场体检；试玩后“改完从刚才的位置继续”；运行时界面换 uGUI / UI Toolkit，加分辨率列表、手柄、界面放大到 200%。

## 11. 新对话开工模板

1. 读根目录 `AGENTS.md` → 本文件 → `HANDOFF_STATE.md` 顶部。
2. 运行：`git -c safe.directory=C:/Users/71034/Documents/Manus/2026-10-10/pAh9th/MarioTrickster-git status --short --branch` 与 `... log -1 --oneline`，再跑一键验证。
3. 向用户简短汇报：当前阶段、验证结果、未验证项、建议的下一步；然后按用户要求实施。
4. 交付前按 `AGENTS.md`“每轮交付前必须做”收尾。

## 12. 备份与回退

- S248 之前的原始项目：`outputs/park-upgrade-20261019/backup/`（`MarioTrickster_before_world_park_20261019.zip`、`.bundle`、`备份恢复说明.md`、`backup-receipt.json`）。
- 各阶段文档：`docs/step1/S248_WORLD_PARK_AUTHORING.md`、`S249_WORLD_MAINLINE.md`、`S250_WORLD_JOURNEY.md`、`S251_BORDERLAND_FRAMEWORK.md`、`S252_GAME_FEEL_SPADES.md`、`S253_WEB_PLAY.md`。
- S253 过程记录（本机）：`.work/borderland-framework-20261020/PLAN-S253.md`（含一次性修补脚本 `fix-play-*.cjs`、`fix-smoke-*.cjs`）；给用户的说明：`outputs/borderland-framework-20261020/S253_网页试玩.md`，截图副本 `outputs/borderland-framework-20261020/网页试玩截图-S253/`。
- S252 过程记录（本机）：`.work/borderland-framework-20261020/PLAN-S252.md`；给用户的说明：`outputs/borderland-framework-20261020/S252_更像游戏的一轮.md`，剧情模拟副本 `outputs/borderland-framework-20261020/剧情模拟-S252/`。
- S251 研究笔记（资料来源摘录，本机）：`.work/borderland-framework-20261020/research-notes.md`；给用户的报告：`outputs/borderland-framework-20261020/弥留之国框架重构_第一性原理拆解与首版成果.md`。
- 过程资料（被 Git 忽略，仅本机）：`.work/park-upgrade-20261019/`、`.work/world-mainline-20261019/`、`.work/world-s250-20261019/`、`.work/continue-pack-20261019/`（其中 `make-verify-kit.py` 记录了验证工具如何从过程资料整理而来）。
