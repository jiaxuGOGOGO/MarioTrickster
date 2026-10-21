> **S254 当前状态（2026-10-21）**：回答“是页游还是文字游戏”——都不是，正式端是 Unity 做的 **Windows 电脑游戏**，网页试玩是同规则的浏览器试玩版。本轮按“优先 Windows 电脑端 + 设计关卡要方便”补齐：一键构建 exe（世界工坊 / 开始页“构建 Windows 版”，Unity 关着时双击 `BUILD_WINDOWS_GAME.cmd`）、标题页、Esc 菜单（设置界面大小 / 音量 / 全屏、重来本会场、回到标题、退出）、界面按分辨率缩放；创作上加会场模板、复制会场、▶ 只玩这个会场、网页“会场体检”红黄绿、网页 ↔ Unity「我的世界」自动同步。一键验证 18 步全部 PASS、无 SKIP；**Unity 原生测试、Unity 实机、Windows 版实际构建都未执行**（许可证），用户自测清单在 `docs/step1/S254_WINDOWS_AND_AUTHORING.md` 末尾。审计报告 `outputs/borderland-audit-s254/AUDIT.md`。
> **S253 当前状态（2026-10-20）**：为了“还是要做游戏、不是文字阅读”且 Unity 许可证仍未激活，本轮做了**网页试玩**：网页设计台 `Tools/LevelStudioWeb/index.html` 顶部 ▶ 网页试玩（或 `index.html#play`）可以在浏览器里完整玩“空白之城”——霓虹亮灯入场、俯视现场走位调查、♠5 鬼巡逻与追逐、全屏博弈桌逐张亮牌、反转卡、GAME CLEAR / OVER 结局与门、旅程总结，安全阶段自动存进度。规则不另写一份：新增网页探索内核 `explore.js`（逐行移植 C# 探索与追逐者，跨端 11 用例 62958 项差异 0）与 `play-model.js`（演出 / 字幕卡 / 博弈桌纯逻辑，8 条旅程 61450 项差异 0）；`play.js` 只做画面。新增**真实浏览器自动试玩** `web-play-smoke.cjs`（无界面 Edge 真实按键走完 4 个会场，含被鬼抓→重来，21 张截图，错误 0），按截图修了 7 处画面问题。一键验证 17 步全部 PASS；Unity 原生测试与 Unity 实机仍受许可证阻挡；网页试玩只经过自动试玩与截图检查，**还没有真人玩过**。细节见 `docs/step1/S253_WEB_PLAY.md`。未commit/push。
> **S252 当前状态（2026-10-20）**：用户要求“还是要做游戏、不是文字阅读，要代入感和反转”。本轮新增黑桃体力会场 ♠5 鬼抓人（探索点位新种类 `chaser`：巡逻、视锥、追逐、搜寻、高草躲藏；C# 内核运行，网页编辑/预检同语义）；演出层（霓虹扑克牌亮灯入场、倒计时、出局激光、GAME CLEAR / GAME OVER 章、程序合成音效）；`game` 阶段改为全屏博弈桌（逐个亮牌、命灯、出局激光）；剧情 / 揭示 / 结局改为全屏字幕卡（揭示时逐张翻开“当时以为 → 其实”反转卡）；三个博弈会场加入场观察线索，六个会场都有反转。一键验证 13 步全部 PASS（探索 99 断言、跨端 61+35 用例 0 差异、剧情模拟 46/46）；Unity 原生测试与 Play 实机仍受许可证阻挡，**还没有人在 Unity 里实际玩过**。细节见 `docs/step1/S252_GAME_FEEL_SPADES.md`。未commit/push。
> **S251 当前状态（2026-10-20）**：按用户最终诉求（实现《弥留之国的爱丽丝》式游戏设计与效果、方便以后自定义新游戏、用剧情模拟检验体验）重构框架：新增世界阶段 `game` 与博弈内核 `BorderlandGame`（数字猜心/看不见的标记/分还是偷；NPC 人格、证词、承诺、命数、出局、规则逐轮追加与决斗规则；C# 与网页 `borderland.js` 同语义）；默认主世界改为原创五会场“空白之城”（`Assets/Resources/WorldPark/Borderland.worldpark.json`）；新增剧情带入模拟器 `Tools/WorldParkVerify/Story`（五种人格只凭公开信息真实走完世界，对照 `Borderland.expect.json` 检验 29 项）。一键验证全部 PASS（含 Unity 引用编译、跨端 51+35 用例 0 差异、剧情模拟 29/29、网页博弈冒烟 12/12）；Unity 原生测试与 Play 实机仍受许可证阻挡未执行。细节见 `docs/step1/S251_BORDERLAND_FRAMEWORK.md`。未commit/push。
> **S250 当前状态（2026-10-19）**：在S249主线上补齐“多会场乐园”的三项基础：Unity与网页的探索点位画布（只接受从出生点T走得到的格子）、作者自定义结局标签（clear/fail兼容，链接按同名标签解锁）、世界检查点（剧情/揭示/结局安全阶段自动保存，按世界内容指纹隔离，开始页与世界工坊“继续旅程”）。默认示例为三会场旅程。细节见 `docs/step1/S250_WORLD_JOURNEY.md`。实际Unity引用编译与纯逻辑/跨端/网页真实点击已通过；Unity原生测试与Play实机仍受许可证阻挡。未commit/push。
> **S249 当前主线（2026-10-19）**：用户已授权不保留独立旧主模式。主视角二维斜俯视，开始世界→现场探索→真实活动结果→反转→结局；横版和地图工坊是子工具。顶层菜单四项。默认雾灯小镇。世界工坊选explore→画当前会场地图→应用回当前草稿→导出→开始世界。旧日程不在默认主会场启动。细节见 `docs/step1/S249_WORLD_MAINLINE.md`。本段优先于下方S248历史“可选独立模式”说明。源码编译/纯逻辑验证不等于Unity Play；原生验收仍受许可证阻碍。未commit/push，旧备份不变。

# 可迁移交接状态

> 新电脑或新 Manus 账号接续时，先读本文件、`SESSION_TRACKER.md` 与 `SKILL.md`。Git 分支和提交是最终事实来源；本文件只描述当前交接状态与验证边界。

## 仓库身份

| 项目 | 值 |
|---|---|
| 规范远端 | `https://github.com/jiaxuGOGOGO/MarioTrickster.git` |
| 规范分支 | `genspark_ai_developer` |
| Unity 版本 | `2022.3.61f1` |
| 已废弃依赖 | 不拉取、不依赖 `MarioTrickster-Art` 子模块 |

## S250 世界旅程（2026-10-19，未提交）

> 本节之前先读 S251（下方紧接的小节是 S250 历史）。

## S254 Windows 电脑端 + 关卡创作便利（2026-10-21）
- 用户要求：10-20 17:12 “是页游还是文字游戏”；对照最终诉求结合全网资料全面审计；**优先 Windows 电脑端（Unity PC）**；全方位验证优化；然后推送 GitHub。17:14 / 10-21 09:45：Unity 不能自己测就把其他都做好，用户自己进去测；前提是设计关卡、网页构思关卡必须方便。
- 新增：`WorldPark/WorldParkUi.cs`（界面缩放、设置、暂停菜单、标题页）、`tools/LevelStudioWeb/venue-check.js`（会场体检）、`Tools/WorldParkVerify/venue-check.cjs`、根目录 `BUILD_WINDOWS_GAME.cmd` / `RUN_UNITY_TESTS.cmd`、`docs/step1/S254_WINDOWS_AND_AUTHORING.md`（含 Unity 自测清单）。
- 修改：运行器（标题页、Esc 菜单、重来本会场、回到标题、结束后回标题、面板缩放）、探索运行器（HUD 缩放、共用暂停菜单）、演出层 / 字幕卡 / 博弈桌（字号随界面大小）、`WorldParkProgress.RetryVenue`（与网页 `retryVenue` 同语义）、构建器（`BuildWindows` / `BuildWindowsBatch`、菜单）、世界工坊（「我的世界」同步、复制会场、模板、只玩这个会场、构建按钮）、开始页、FeatureMap（85 项）、网页世界乐园页（写进 Unity 项目、模板、只玩这个会场、体检卡）、run_all（18 步）。规则内核（BorderlandGame / WorldParkExplore / WorldParkPack）没改。
- 与下方 2026-10-10“不构建 Player”规则的关系：那条针对已撤销的“Test Player + 多种子批量试玩”。S254 的 Windows 构建是用户 17:12 明确要求的正式端，只在作者点按钮 / 双击 `.cmd` 时构建，不做自动批量试玩。
- 验证：run_all 18 步全部 PASS、无 SKIP（检查点 60、会场体检 19/19、剧情模拟 46/46、真实浏览器试玩含创作流程错误 0）。**未验证**：Unity 原生测试、Unity 实机、Windows 版实际构建与运行、两个 `.cmd` 实跑、网页写进 Unity 的端到端。
- Git：S248–S254 的世界改动在 S254 一并提交（下方各节写的“未提交”是当时状态）；10-10 的旧未审计 `.meta`（127 个）仍不混入。

## S253 网页试玩（2026-10-20）
- 用户要求：16:03 / 17:05 “继续”（延续“还是要做游戏，不是文字阅读；增强代入感、要有反转”）。
- 新增：`Tools/LevelStudioWeb/explore.js`（网页探索内核，移植 `WorldParkExplore.cs` + `OverworldMap` 移动/视线/寻路）、`play-model.js`（移植 `WorldParkStageCues`/`WorldParkSceneModel`/`WorldParkTableModel`）、`play.js`（全屏试玩层：画布、输入、合成音效、localStorage 存档）；`Tools/WorldParkVerify/explore-parity.cjs`、`play-parity.cjs`、`web-play-smoke.cjs`。
- 修改：`build.py`（内联新脚本）、`shell.html`（▶ 网页试玩 / ▶ 网页试玩这个世界）、`worldpark-app.js`（按钮与 `#play`）、Snapshot（子命令 `explore`、`views`）、`run_all.ps1`（3 个新步骤）、`WorldParkSceneModel.cs` + `play-model.js`（旅程行不重复牌面，同语义）、FeatureMap（`web.play`，80 项）。
- 验证：run_all 17 步 PASS（探索内核跨端 62958 项 0 差异、试玩画面跨端 61450 项 0 差异、真实浏览器试玩错误 0）；**未验证**：Unity 原生测试与 Unity 实机、真人试玩、触屏。
- 环境：命令沙箱里浏览器需 `--no-sandbox --in-process-gpu --remote-allow-origins=*`，且 exec 要允许本机回环网络；否则该步 SKIP。
## S252 更像游戏：鬼抓人、演出层、博弈桌、字幕卡（2026-10-20，未提交）
- 用户要求：13:52 做黑桃体力会场（鬼抓人）+ 演出（亮灯、倒计时、出局画面与音效）+ 澄清“开始世界”是游戏还是模拟；15:01 “还是要做游戏，不是文字阅读，增强代入感、要有反转；先还原，以后再开拓关卡”。
- 新增：`WorldPark/WorldParkStageCues.cs`、`WorldParkStage.cs`、`WorldParkSfx.cs`（演出层）、`WorldParkTableModel.cs`、`WorldParkTable.cs`（博弈桌）、`WorldParkSceneModel.cs`、`WorldParkScene.cs`（字幕卡），均带唯一 GUID `.meta`。
- 修改：`WorldParkPack.cs` + `worldpark.js`（chaser 点位字段与预检，同语义）、`WorldParkExplore.cs`（追逐者内核 + 三个防冻结修复）、`WorldParkExploreRuntime.cs`、`WorldParkRuntime.cs`（旧文字博弈面板 `DrawGame`/`DrawSeats` 已删除，改为 `DrawGameTable`；新增 `DrawSceneCard`）、`BorderlandGame.cs` + `borderland.js`（RoundRecord 加 picks/marks/target/opponent/winners）、点位画布（Unity + 网页）、主世界 JSON（♠5 + 博弈会场入场线索）、剧情模拟器与作者预期、parity、FeatureMap（79 项）。
- 验证：run_all 13 步 PASS；**未验证**：Unity 原生测试、Play 实机（博弈桌/字幕卡/演出/鬼的画面与手感、音效）、真人试玩。
- 澄清口径：Unity 里是真实可玩的游戏代码，但没人玩过；剧情模拟器是无画面的规则检验，不等于玩。用户激活 Unity 个人许可证后即可亲自走一遍。
## S251 弥留之国式框架（2026-10-20，未提交）

- 用户最终诉求：实现《弥留之国的爱丽丝》式的游戏设计与效果，方便以后自定义新游戏；用剧情模拟检验体验是否达到预期，以此为框架标准。允许删冗余、全方位重构。
- 新增：`WorldPark/BorderlandGame.cs`（博弈内核：beauty/mark/split，人格 player/naive/reasoner/deep/adaptive/liar/loyal/mirror，座位2–8、轮数≤20、命数≤20）、`tools/LevelStudioWeb/borderland.js`（同语义网页内核）、`Resources/WorldPark/Borderland.worldpark.json`（主世界“空白之城”五会场）、`Tools/WorldParkVerify/Story/`（剧情带入模拟器 + 作者预期文件）、`Tools/WorldParkVerify/web-game-smoke.cjs`。
- 修改：世界包新阶段 `game`（`game`/`onWin`/`onLose`，`ResolveGame` 必须正好走完整局）；检查点重放 game 动作；运行器剧情面板逐轮博弈；构建器/世界窗口/网页工坊支持 game；开始世界与网页示例改用主世界；旧“幕间乐园”`Demo.worldpark.json` 降为格式样例与回归夹具。
- 验证：`run_all.ps1` 全部 PASS（Unity引用编译、核心288、探索47、检查点43、旅程212、Unity画布2/2、跨端51+博弈35用例0差异、剧情模拟29/29、网页画布22、网页博弈冒烟12/12、语法、重建、功能地图同步）。
- 未验证：Unity原生EditMode/PlayMode、Play里的博弈面板与探索、Unity窗口博弈字段编辑、真实浏览器点击、真人试玩。旧子系统代码未删除（场景/预制体按GUID引用，需Unity验证后再删）。
- 详细：`docs/step1/S251_BORDERLAND_FRAMEWORK.md`；给用户的报告：`outputs/borderland-framework-20261020/`。

## S250 世界旅程（2026-10-19，未提交，历史）

- 新增：`WorldParkProgress.cs`（检查点模型：记录会话动作，恢复时在同一世界上重放并比对；字符与UTF-8字节双上限；结果允许自定义标签）、`WorldParkSaveStore.cs`（persistentDataPath/WorldPark/<内容指纹>.worldsave.json；临时文件写入+读回校验+替换，旧档保留.previous）、`Editor/WorldParkSpatialEditor.cs`（Unity点位画布）、`tools/LevelStudioWeb/worldpark-spatial.js`（网页点位画布）。
- 修改：会话记录continue/room/observe/explore/travel五类成功动作；运行器在story/reveal/ending自动保存，room/explore中途不存；暂停菜单可手动保存；探索组件与运行器同对象；构建器`TryRun(pack, out report, resume)`失败前不改场景；开始页与世界工坊工具栏“继续旅程”（`WorldParkWindow.ContinueCurrent`），未新增顶层菜单。
- 结局标签由作者定义，`any`仅作链接通配；链接要求来源会场不可能产生的标签时给warn `link_result_unreachable`。C#与网页同规则。
- 默认示例为三会场：雾灯小镇（clear→检修回廊；fail→记录层）、检修回廊（90秒取单返回）、旅程记录层（recorded/unfinished）。
- 验证（当前源码）：实际Unity引用编译三程序集通过；`.work/world-s250-20261019/verify` 212断言含三条真实移动旅程与每阶段存读；检查点harness 43；S249探索harness 47；`parity-s250.cjs` 31用例0差异；网页画布22断言；内置浏览器真实点击画布通过。S248核心harness规则287条通过，末条断言旧示例属过期夹具。
- 未验证：Unity原生EditMode、Play实机行走与存读档、Unity点位画布鼠标操作、真人试玩。下一步见 `docs/step1/S250_WORLD_JOURNEY.md` 的P4–P6。

## S248 世界乐园作者底座（2026-10-19，未提交升级）

- 当前工作副本基线仍是 `ecfd954d26dea9b594698ffff547df770d80a977`，分支 `genspark_ai_developer`；本轮未创建提交、未推送。下面2026-10-10的测试均为历史记录。
- 升级前已保存976文件实际快照ZIP并逐文件SHA-256读回校验，完整Git历史bundle已verify。位置：`outputs/park-upgrade-20261019/backup/`。Windows注册表PlayerPrefs不在项目目录备份范围。
- 新增世界包、会场/阶段/分支/线索/链接作者表单、网页世界页与纯逻辑彩排。Unity开始页“世界乐园”；菜单 `MarioTrickster/检查与记录 Checks/世界乐园 World Park`。没有新增顶层菜单，没有擅自迁移引擎。
- 可选物理接线复用现有关卡库/样板，生成隔离场景；先出示房间提示，真实GameManager结算后由玩家确认推进。已有Build Settings及禁用状态不静默改写，失败恢复。空会话清理不影响旧模式时钟。
- `roomResults` 是房间事实；`venueResults` 是作者定义的会场总体目标。truth仅在已出示线索并进入对应揭示阶段后显示，不进入日志或旧AI。绕过可选线索给警告，不伪造已观察。
- 本轮实际验证：纯逻辑 **289断言通过**；C#/JS同包逐状态 **37用例通过**；实际Unity DLL下运行时/编辑器/测试源码编译 **0错误**，既有警告保留；网页唯一来源重建与内嵌JS语法通过。
- **Unity回归未完成**：唯一恢复性批处理被许可证校验阻挡（No valid Unity Editor license）；未产生新的Unity测试XML。13个新增Test方法只是已编译，不能称Unity通过。历史359/359不能借用。浏览器预览曾加载，交互被阻挡后标签页关闭，停止操作，未计为完整UI验收。
- 近期下一步：先在有有效许可的Unity跑受影响EditMode，再用原有入口做最多三分钟的人工核对（真实两种结果、先提示后进入、确认后切场、退出清空、取消/失败配置保护）。不要启动已撤销的Test Player或长时间多种子跑批。
- P2：把现有小镇接成完整会场、必达剧情/现场线索事件、世界独立存档；P3：选择一类真正不同的目标/角色玩法；P4：世界依赖打包与更完整创作体验。详细：`docs/step1/S248_WORLD_PARK_AUTHORING.md`。
- 本轮交付包与tracked/untracked源码已另存到 `outputs/park-upgrade-20261019/`。存档工具已对白名单新增世界样例/已审计目录meta/S248文档，并跳过可重建WorldPark场景；未替旧123个meta做审计或混入提交。

## 历史基线（2026-10-10）

- Git 基线提交：`49e5b87d142ea144a4336bdb0f661160c6b84f05`（S246）。
- 在 Windows Unity 编辑器中，EditMode：**357 / 357 通过，0 失败，0 跳过**。
- 已验证但仍待用户审阅/提交的最小修改：
  1. `Assets/Tests/EditMode/Step1RushMarioTests.cs`：S240 数据版本断言由严格等于 29 改为不低于 29；
  2. `Assets/Scripts/Gameplay/Step1/Step1Lighting.cs`：移除会误触发隔离检查的实现名称注释。
- Unity 首次导入会生成 121 个未跟踪 `.meta`；它们是独立的仓库卫生项，**不要**与上述两项修改混合提交。下一位维护者应先做 GUID 冲突和引用检查，再单独处理。

## 试玩与地图验证边界（2026-10-10）

- 已尝试并**完整撤销**“独立 Test Player + 多种子批量试玩”的实验：不保留源码、菜单入口、脚本、构建物或运行记录。除非用户明确要求重新评估，不构建 Player 或长时间占用 Unity。
- 现有随机布局已经穷举验证每种组合的可达性；后续随机验证必须按“体验签名”选择少量命名情境与可复现种子，不能只因 seed 数值不同就声称覆盖不同体验。
- 默认流程：静态/纯逻辑审计 → 必要时一次已有 EditMode 回归 → 单一、最多三分钟的人工体验验证。不得把 Editor Test Runner 用作开放式长跑试玩。
- 现有小镇已含 44×28 与 72×40 样板。扩张大地图前，先在 72×40 内验证路线抉择、可读隐蔽区和有余波的世界机关；不要先造更大画布。
- 本次撤销后未重启 Unity。下一次改动游戏规则前，先运行一次既有 EditMode 作为恢复性验收；上面的 357 / 357 是撤销前的已记录基线，不是本次清理后的新测试结果。

## S247 环境真实感修复（2026-10-10，未提交）

- 地面换图按房间字符画判断遮挡：只有“户外 + 露天”的地面画草；室内露出的地面画石板 `StoneTop`；户外但有楼板遮挡，以及被压住的土层，画 `GroundFill`。
- 室内草地 `v` 改画稻草垫 `HayGround`，玩法不变；素材包里自定义的图优先。
- 雨只落在露天的列，停在第一层地面或单向板上；室内不画雨，雨天听觉规则不变。户外挂旗、蛛网必须挂在实心块上。
- 新增 EditMode 测试 `S247_Realistic_Surfaces_Grass_Rain_And_Hangings`。已通过 .NET 编译（0 错误）和纯逻辑实际执行（15/15）；Unity EditMode 回归 359/359 通过（2026-10-10 16:02）。

## S247 关卡编辑适配 + 少字可读（2026-10-10）

- **元素长相唯一来源** `Assets/Scripts/LevelDesign/ElementLook.cs`：每个 ASCII 元素的三色（红=坑他 / 蓝=藏身 / 金=目标 / 灰=地形）、≤4 字动词、一句用法、图标名都只写在这里。作战图 `Step1Glance`、地图图例 `Step1MapLegend`、Unity 关卡工坊、网页设计台全部读它。**以后加新元素：先在 `ElementCatalog` 登记，再在 `ElementLook.cs` 的长相表里加一行**；`ElementLook.Gaps()` 和测试会指出漏了哪一项。
- 图例分组改成跟三色一致（例如油桶会炸 → 归“坑他”），不再出现“颜色说红、图例说藏身”的矛盾。
- Unity 关卡工坊调色板与画布显示像素图标 + 三色底条；网页设计台同样，并提供“画布显示字母”开关对照 ASCII。
- 房间里的“出口 EXIT / 宝物”文字路牌换成图标 + 箭头（`Step1SignIcon`），`BuilderVersion` 升级，旧场景打开时自动重建。
- 新增测试 `S247_ElementLook_SingleSourceIconsAndConsistentGroups`。已通过 .NET 编译（运行时 / 编辑器 / 测试 0 错误）、无 Unity 实际执行（28 个图例条目：0 缺口、0 分组矛盾）、网页数据校验（29 个图标全部可画）；**Unity EditMode 回归 359/359 通过（2026-10-10 16:02）**。
- 存档工具 `save_checkpoint.ps1` 适配：`Assets/Scripts`、`Assets/Tests` 下的新 `.cs`（连同 `.meta`）和网页设计台文件会自动存档；Unity 自动生成的 `Step1_PrankRoom.unity` 与 `Resources/Step1` 自动跳过；其他陌生新文件仍拒绝。Export 模式会把新源码一并复制到 `-new-files` 文件夹。

## 接续前强制检查

```powershell
git status --short --branch
git log -1 --oneline
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\bootstrap_windows.ps1
```

然后在 Unity Hub 打开当前 Git 工作副本并运行 EditMode。新 AI 在实现前必须报告：当前 commit、工作树改动、最近测试、未完成项与风险。

需要迁移前，优先运行 `scripts/save_checkpoint.ps1 -Mode Save -Push`。该工具会创建一个带测试摘要的 Git checkpoint，并排除未经审计的 Unity `.meta`；无法 push 时使用 `-Mode Export` 生成补丁交接。

## 提交纪律

- 每次开发结束，更新本文件中“当前基线”的提交、测试和未完成项。
- 所有可交接成果必须推送 GitHub；未 push 的工作必须保存 `.patch` 或 `.bundle`。
- 不提交 `Library/`、`Temp/`、`Logs/`、用户私有凭据或本机许可证。
- 不把未审计的 Unity 自动生成 `.meta` 混入功能提交。
