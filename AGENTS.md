# MarioTrickster：给接手 AI 的入口（本文件会被自动读取）

用户不写代码，是这个独立游戏的作者兼策划；你是他唯一的程序和关卡助手。**所有回复、文档、界面文字都用中文。** 不依赖任何旧账号、旧对话或记忆：本仓库文件就是全部上下文。

## 当前状态（每次交付后更新这一节）

- 最新阶段：**S254 Windows 电脑端 + 关卡创作便利**（2026-10-21，说明 `docs/step1/S254_WINDOWS_AND_AUTHORING.md`）。用户最终诉求：实现《弥留之国的爱丽丝》式游戏设计与效果、方便以后自定义新游戏，并用剧情模拟检验体验；S252 起追加“还是要做游戏、不是文字阅读，要代入感和反转”；S254 起追加“**优先 Windows 电脑端（Unity PC）**；Unity 用户自己进去测；设计关卡、网页构思关卡必须方便，让他能集中精力做新关卡或调整好框架”。主体验 = 世界乐园，默认主世界“空白之城”（`Assets/Resources/WorldPark/Borderland.worldpark.json`，六个原创会场，含 ♠5 鬼抓人）：霓虹亮灯入场 → 俯视现场调查 / 躲鬼（`chaser` 点位）/ 全屏博弈桌（`game` 阶段，内核 `BorderlandGame`）→ 字幕卡揭示（反转卡“当时以为 → 其实”）→ 结局章 → 从“门”去下一个会场；检查点可“继续旅程”。两个运行端：Unity（正式端 = Windows 电脑游戏；S254 起可一键构建 exe，有标题页、Esc 菜单、设置、界面缩放、重来本会场；许可证未激活，没人实机玩过）与**网页试玩**（`Tools/LevelStudioWeb/index.html` 顶部 ▶ 网页试玩 或 `#play`）。创作入口：网页“世界乐园”页（模板、复制、▶ 只玩这个会场、会场体检 `venue-check.js`、🔗 写进 Unity 项目）与 Unity 世界工坊，两边通过「我的世界」`Assets/Resources/WorldPark/MyWorld.worldpark.json`（用户内容）同步。剧情带入模拟器 `Tools/WorldParkVerify/Story` 是体验验收标准（对照 `Borderland.expect.json`，现 46 项）；真实浏览器自动试玩 `Tools/WorldParkVerify/web-play-smoke.cjs` 是画面验收（截图在 `.work/worldpark-verify/web-play/`）。横版房间与小镇地图是子创作工具；旧“幕间乐园” `Demo.worldpark.json` 只是格式样例与回归夹具。
- Git：分支 `genspark_ai_developer`。S248–S254 的世界改动已在 S254 按用户要求（10-20 17:12“然后推送到 GitHub”）提交，是否已推送见 `git status -sb`（ahead 表示还没推上去）。10-10 的 127 个旧未审计文件（多为 `.meta`，另有 `Step1_PrankRoom.unity`、`RushMarioTuning.asset`）**仍未跟踪，不要混进提交**。以后用户没要求时不要提交或推送。
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
