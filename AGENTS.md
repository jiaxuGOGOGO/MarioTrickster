# MarioTrickster：给接手 AI 的入口（本文件会被自动读取）

用户不写代码，是这个独立游戏的作者兼策划；你是他唯一的程序和关卡助手。**所有回复、文档、界面文字都用中文。** 不依赖任何旧账号、旧对话或记忆：本仓库文件就是全部上下文。

## 当前状态（每次交付后更新这一节）

- 当前人工测试：T08e1公告板调查 / Tab线索本 / 关本暂停已由14:07作者“没问题”按整段实践回报通过。继续任务式学习；当前T08e2从驾驶舱“做伏笔 / 反转”选公告板，在玩家先能看见的事实末尾一次粘贴【练习】，只玩此会场验证，再回世界工坊安全条撤销这一次修改并确认标记去掉。AI不修改世界 / 文案，作者练习保留原文，不改真相 / 条件 / 位置、不写Unity。 当前记录见 `docs/guide/GUIDED_TEST_LOG.md`。

- 最新阶段：**S259 逐步人工测试（2026-10-22）**：起初按作者要求逐步人工测试；13:59作者要求加快并以熟悉流畅使用项目为目标，现改为“一轮一个实际任务，先讲用途→连续实践→成果回报；卡住才拆细、修复 / 自动回归 / 原任务复测”，不逐键截图、不重复已通过基础动作，也不批量加功能。当前 T01 首页截图已确认（10-22 10:37），T02a 作者确认已下载 borderland_v1.worldpark.json；T02b 本次世界写入未启用（10:44 截图）；T03 已打开 26×14 候车大厅地图（10:47）；T04a 27×14 预览已确认（10:55）；T04b 作者11:03确认取消成功；T05a作者11:06确认应用后27×14；T05b作者11:12确认原26×14和点位保留；T06a截图确认原命数3（11:16）；T06b命数4且校验已确认；T06c撤销已确认；T07a-F02已复测通过；T07b选鬼已确认；T07c模式已确认；T07d第五格已确认；T07e按文字回4格；T07f按文字已通过；T08a标题页已确认；T08b规则已确认；T08c现场 / 暂停框已确认；T08c2冻结已确认；T08d1向右已确认；T08d上下左右已确认；T08e1调查 / 线索本已确认；T08e2待编辑—试玩—撤销；三处已有点位调整保留；完整测试路线、问题与复测记录、S258 调研来源及后续候选保存在 `docs/guide/GUIDED_TEST_LOG.md`，说明 `docs/step1/S259_GUIDED_TESTING.md`。保留 S258 历史和本机报告 / 笔记；关键知识在仓库文档内，不依赖旧聊天。Windows 仍是正式端；今天的 Unity 可用性需作者确认。起点自动回归退出码 0；本轮增加测试文档并按F02修复点位导航、补可见性回归；不改规则或用户内容，不提交 / 推送。
- 上一阶段：**S258 对照业界做法升级（2026-10-21，说明 `docs/step1/S258_RESEARCH_UPGRADE.md`，调研报告 `outputs/world-cockpit-s258/调研与升级报告.md`）**：用户 17:30 要求“确认是否最优、参考全网资料、按最合适顺序升级”。结论：S257 的方向（图上直接操作、渐进披露、可撤销、按任务导航）与业界一致，但还差三处，按“消除已发生过的事故 → 摩擦最大且两端不一致 → 最后一处手打的空间数据”顺序做完：① Unity 世界工坊“从当前小镇复制地图文本”先弹确认，并有“换回替换前的地图”；② 网页博弈参数从 JSON 改为表单（新文件 `Tools/LevelStudioWeb/worldpark-gameform.js`，字段 / 标签 / 范围照 Unity DrawGameFields，对错仍只由 BorderlandGame.check 判定，每步走 wpPersist 可撤销，JSON 收进高级）；③ 鬼的巡逻路线在点位画布上点（“✎ 鬼的路线”，再点去掉，连线 + 序号，上限 32 与 explore.js 相同）。规则层与世界数据未改。驾驶舱浏览器验收 90 项、表单纯逻辑 61 项、画布 34 项；Unity 引用编译通过，但 Unity 里没人点过新按钮。 上一阶段 **S257 图标驾驶舱与可视化地图创作**（2026-10-21，说明 `docs/step1/S257_COCKPIT_AND_VISUAL_MAP.md`）。S257 图标驾驶舱与可视化地图创作：网页默认驾驶舱，按当前世界显示会场、图标阶段卡、实际成功失败分支和会场之间的门；图标直达地图、点位、规则、博弈、反转、体检与导出。会场地图大画布复用原图块，支持笔刷/矩形/宽高/扩展、地图临时撤销重做；取消不改世界，应用回当前阶段走 wpPersist 并可整步撤销，坐标平移、裁切和可达性由安全校验保护。横版挑战按关卡库房间名称打开子创作并返回原阶段；房间本体另存关卡包，世界包只记名称。Windows 正式端不变；网页不能直接构建 exe。规则层与内置/用户世界数据未改。一键验证 21 步全部 PASS、无 SKIP（地图 69 / 驾驶舱模型 144 断言、浏览器创作 48 项错误 0）。新模块 `worldpark-cockpit.js` / `cockpit.css`、`worldpark-map.js`；`Tools/WorldParkVerify/cockpit-smoke.cjs` 验证隔离浏览器创作流程。**驾驶舱只读现有世界并导航，不建第二份数据来源；地图临时编辑只有应用时才能走 wpPersist；房间包和世界包必须继续分开说明。** 指南第 0a 节与课程已同步。17:15 用户要求审计“是否满足需求、从第一性原理看是否合适”：结论是点名的四件事（地图不靠文本、子创作联动、图标讲运转、驾驶舱导航）网页端已满足；“更方便”部分满足——博弈参数仍是 JSON（3/6 会场的核心）、鬼巡逻路线仍手打坐标、改流程仍回世界工坊、小镇页与会场地图是两套编辑器、Unity 世界工坊没有驾驶舱与安全网且“从当前小镇复制地图文本”仍无确认。审计顺手修 6 处（摆点位放大布局切页残留、页签顺序改为驾驶舱→世界工坊→子创作、安全条吸顶漏一条正文、驾驶舱浏览器验收无浏览器时误报 FAIL 改为 SKIP 且截图改存 .work/worldpark-verify/cockpit/、驾驶舱重复文字首屏 417→376 字、小镇说明改准确）；驾驶舱浏览器验收 57 项错误 0，一键验证 21 步全部 PASS、无 SKIP。报告 outputs/world-cockpit-s257/审计报告.md（不进 Git）；下一步候选按报告第 5 节编号，建议 1 博弈参数表单 → 2 鬼路线图上点 → 6 Unity 端确认/安全网。
- 上一阶段：**S256 世界工坊安全网**（2026-10-21，说明 `docs/step1/S256_WORKSHOP_SAFETY_NET.md`）：用户误点“复制地图”后点 ▶ 没反应、找不到回退、看不懂报错，要求误操作能补救、知道改动影响、低认知负担上手。网页世界工坊新增顶部安全条（↶ 撤销 / ↷ 重做 / 🕘 改动记录 / 刚才改了什么 + 影响 / 显示高级格子，Ctrl+Z / Ctrl+Y，历史存进浏览器）、和内置原版对比与一键恢复、结构预检说人话并带修复按钮、▶ 被拦时就地说明；纯逻辑在 `Tools/LevelStudioWeb/worldpark-history.js`。**以后新增任何会改世界的网页操作都必须走 `wpPersist`，新增校验错误码要在 `WPHistory.explain` 补人话**。规则层未改。上一阶段 **S255 世界工坊上手指南 + 项目上手课程**（`docs/guide/WORLD_WORKSHOP_GUIDE.md`，S256 起第 0 节讲“点错了怎么办”，第 12 节难用之处等用户挑编号；`docs/guide/PROJECT_COURSE.md` 10 课；改界面、会场或能力边界时同步更新这两份）。再上一阶段 **S254 Windows 电脑端 + 关卡创作便利**（说明 `docs/step1/S254_WINDOWS_AND_AUTHORING.md`）。用户最终诉求：实现《弥留之国的爱丽丝》式游戏设计与效果、方便以后自定义新游戏，并用剧情模拟检验体验；S252 起追加“还是要做游戏、不是文字阅读，要代入感和反转”；S254 起追加“**优先 Windows 电脑端（Unity PC）**；Unity 用户自己进去测；设计关卡、网页构思关卡必须方便，让他能集中精力做新关卡或调整好框架”。主体验 = 世界乐园，默认主世界“空白之城”（`Assets/Resources/WorldPark/Borderland.worldpark.json`，六个原创会场，含 ♠5 鬼抓人）：霓虹亮灯入场 → 俯视现场调查 / 躲鬼（`chaser` 点位）/ 全屏博弈桌（`game` 阶段，内核 `BorderlandGame`）→ 字幕卡揭示（反转卡“当时以为 → 其实”）→ 结局章 → 从“门”去下一个会场；检查点可“继续旅程”。两个运行端：Unity（正式端 = Windows 电脑游戏；S254 起可一键构建 exe，有标题页、Esc 菜单、设置、界面缩放、重来本会场；许可证未激活，没人实机玩过）与**网页试玩**（`Tools/LevelStudioWeb/index.html` 顶部 ▶ 网页试玩 或 `#play`）。创作入口：网页“世界乐园”页（模板、复制、▶ 只玩这个会场、会场体检 `venue-check.js`、安全网、🔗 写进 Unity 项目）与 Unity 世界工坊，两边通过「我的世界」`Assets/Resources/WorldPark/MyWorld.worldpark.json`（用户内容）同步。剧情带入模拟器 `Tools/WorldParkVerify/Story` 是体验验收标准（对照 `Borderland.expect.json`，现 46 项）；真实浏览器自动试玩 `Tools/WorldParkVerify/web-play-smoke.cjs` 是画面验收（截图在 `.work/worldpark-verify/web-play/`）。横版房间与小镇地图是子创作工具；旧“幕间乐园” `Demo.worldpark.json` 只是格式样例与回归夹具。
- Git：分支 `genspark_ai_developer`。S248–S254 的世界改动已在 S254 按用户要求（10-20 17:12“然后推送到 GitHub”）本地提交为 `211e7e9`（作者 MarioTrickster AI）；当时本机连不上 github.com，推送留给用户双击 `SAVE_MARIOTRICKSTER.cmd`（它会再加一个检查点提交并一起推送）。是否已推送见 `git status -sb`（ahead 表示还没推上去）。用户已于 10-21 10:36 双击它完成推送（检查点提交 `325c80f`，与 origin 一致）；S255–S256 已在用户的 2026-10-21 16:48 检查点 `6b8e4dc` 存档，开工时该 HEAD 与 origin 一致；S257 本轮改动未提交，下次双击 `SAVE_MARIOTRICKSTER.cmd` 会一起存档并推送（网页 CSS 已补进白名单）。命令环境以另一个 Windows 用户运行，看不到用户的 git 身份与 GitHub 凭据。10-10 的 127 个旧未审计文件（多为 `.meta`，另有 `Step1_PrankRoom.unity`、`RushMarioTuning.asset`）**仍未跟踪，不要混进提交**。以后用户没要求时不要提交或推送。
- 升级前备份（S248 之前的原始项目，已逐文件校验）：`outputs/park-upgrade-20261019/backup/`（zip + git bundle + 恢复说明）。不要删除或改动它。
- Unity 2022.3.61f1 在本机，但批处理被许可证校验阻挡：Unity 原生 EditMode/PlayMode、Play 实机与 Windows 版构建尚未在 S248–S254 跑过（还没有人在 Unity 里实际玩过；解除办法：用户在 Unity Hub 登录 → 偏好设置 → 许可证 → 添加 → 免费个人版）。用户会按 S254 说明末尾的自测清单亲自测；他发回截图或 `Editor.log` 时，优先按反馈修。
- 博弈规则改动同样受第 7 节同步规则约束：`BorderlandGame.cs` 与 `Tools/LevelStudioWeb/borderland.js` 必须同语义，并在 `parity.cjs` 加用例；改了会场或博弈参数要看剧情模拟报告是否仍符合作者预期。
- 演出层 / 博弈桌 / 字幕卡只读会话状态，不推进流程、不改结果；它们的纯逻辑（`WorldParkStageCues`/`WorldParkTableModel`/`WorldParkSceneModel`）由剧情模拟器逐轮核对。网页试玩的规则内核与 C# 同语义且有逐状态对比：`explore.js` ↔ `WorldParkExplore.cs`（`explore-parity.cjs`），`play-model.js` ↔ 三个演出模型（`play-parity.cjs`）；改其中一边必须同改另一边，`play.js` 只做画面、不推进规则。

## 开工顺序

1. 读 `docs/AI_CONTINUE_PACK/WORLD_PARK_CONTINUE.md`（完整接续文档：愿景、用户决策记录、架构、数据格式、同步规则、验证、风险、下一步）。
2. 读 `docs/AI_CONTINUE_PACK/HANDOFF_STATE.md` 顶部和 `SESSION_TRACKER.md` 的“最新 Session”行。
3. 运行一键验证，确认起点是好的：`powershell -ExecutionPolicy Bypass -File Tools\WorldParkVerify\run_all.ps1`
4. 只有任务涉及旧子系统（横版房间机关、马里奥 AI、小镇机关、美术管线、网页小镇等）时，才读 `docs/AI_CONTINUE_PACK/SKILL.md` 及其 references 分册。

## 优先级（冲突时从上往下）

1. 用户在当前对话里的最新要求。
2. `WORLD_PARK_CONTINUE.md` 第 3 节“用户决策记录”（S248–S250 的授权：世界优先、不保留独立旧主模式、可删冗余、二维斜俯视主视角）。
3. `SKILL.md` 与设计宪法里的旧规则：只在它约束的旧子系统内有效（例如“守宝/马里奥 AI”规则只管横版挑战片段，不约束世界层）；其中“顶层菜单恰好 10 个”已被 S249 改为 4 个。

## 红线（任何时候都不做）

- 不执行 `git reset --hard`、`git clean`、`git checkout -- .`、`git stash` 等会丢弃工作副本的命令；不覆盖或删除 `outputs/` 里的备份。
- 不修改用户内容：`Assets/Resources/My*.json`、`Assets/Resources/ArtKit/MyArtKit.asset`、用户自己画的关卡/地图/台词文件。
- 不引入第二份数据来源：功能清单只在 `Assets/Scripts/LevelDesign/FeatureMap.cs`；世界规则 C# 与网页必须同语义（见接续文档第 7 节）。
- 不把“编译通过/纯逻辑通过”说成“Unity 测试通过”或“已试玩”；说明里要分开写已验证和未验证。
- 不另做“升级包”给用户安装：直接改当前目录（用户明确觉得升级包令人困惑）。备份/补丁只在用户要求或高风险操作前做。

## 本机环境要点

- 这台电脑上 git 会报 dubious ownership：所有 git 命令都要加 `-c safe.directory=C:/Users/71034/Documents/Manus/2026-10-10/pAh9th/MarioTrickster-git`（或用户自己执行 `git config --global --add safe.directory <该路径>`）。
- Windows PowerShell 5.1：含中文的 `.ps1` 必须存成 **UTF-8 带 BOM**，否则会乱码甚至解析失败。
- 真实浏览器自动试玩在命令沙箱里要允许本机回环网络（exec `network=true`）；浏览器参数已在脚本里（`--no-sandbox --in-process-gpu`），连不上时该步 SKIP。
- 系统没有 Python：网页构建用工作区便携版 `.work/tools/python-3.12.10/python.exe`（被 Git 忽略；丢了就从 python.org 下载 3.12 embeddable 解压到同一位置）。另有 .NET 8 SDK、Node.js。
- 磁盘上目录名是 `Tools/`（旧文档里常写成 `tools/`，Windows 不区分大小写）。
- 临时文件放 `.work/<任务名>/`；给用户的交付放 `outputs/<任务名>/`。两者都被 Git 忽略。

## 每轮交付前必须做

1. `Tools\WorldParkVerify\run_all.ps1` 全部 PASS（SKIP 要说明原因）。改了世界规则就同时改 C# 与网页，并在 `Tools/WorldParkVerify/parity.cjs` 加用例。
2. 新菜单/按键/网页面板/文档 → 在 `FeatureMap.cs` 登记，并重新生成 `docs/FEATURE_MAP.md`（命令见接续文档第 8 节）。
3. 写 `docs/step1/S2xx_*.md`；更新 `HANDOFF_STATE.md`、`SESSION_TRACKER.md` 最新 Session 行、`START_HERE.md`（入口有变时），以及本文件“当前状态”和 `WORLD_PARK_CONTINUE.md`。
4. 新增 `.cs` 要配稳定唯一 GUID 的 `.meta`。
