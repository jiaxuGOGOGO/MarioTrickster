# S254 Windows 电脑端 + 关卡创作便利（2026-10-20 17:12 起 → 2026-10-21 完成）

## 用户要求（原话要点）

- 2026-10-20 17:12：“所以是个页游还是文字游戏”；根据最终诉求对照项目、结合全网参考资料全面审计；**优先考虑 Windows 电脑端运行（Unity PC Windows 端开发）**，其他后续；全方位验证测试并优化；然后推送到 GitHub。
- 17:14 / 2026-10-21 09:45：Unity 如果不能自己进去测试，就把其他都做好、优化好，用户自己进去测；**前提：设计关卡、在网页里构思关卡必须方便，让用户能集中精力设计新关卡，或调整已有的好框架关卡。**

回答“页游还是文字游戏”：都不是。正式端是 **Unity 做的 Windows 电脑游戏**（俯视走位调查、鬼抓人追逐、全屏博弈桌、字幕卡反转），S254 起可以一键打包成双击就能玩的 exe；网页试玩是同一套规则在浏览器里的试玩版，方便不开 Unity 时构思和试关卡。

## 本轮做了什么

### A. 创作便利（用户第一优先）

| 功能 | 网页设计台（世界乐园页） | Unity 世界工坊 |
|---|---|---|
| 会场体检（红 / 黄 / 绿即时反馈） | 右侧“会场体检”卡，改动 0.35 秒后自动刷新（`venue-check.js`） | —（Unity 端用“检查结构”+ 剧情带入模拟） |
| 用模板新建会场 | 模板下拉 +“＋ 用模板新建” | 会场区模板下拉 +“用模板新建会场” |
| 复制会场（在好框架上改） | “复制当前会场”（S254 前已有） | “复制此会场”（S254 新增） |
| 只玩这个会场 | “▶ 只玩这个会场” | “▶ 只玩这个会场”（有单独检查点） |
| 网页 ↔ Unity 同步 | “🔗 写进 Unity 项目”：选一次项目文件夹，之后每次改动 1 秒后自动写入 | “我的世界”一栏：草稿没改动时**自动载入**网页写进来的新版本；有改动时只提示、不替换；“保存为我的世界”写回给网页 |

- 共用文件：`Assets/Resources/WorldPark/MyWorld.worldpark.json`（用户内容；只在作者点按钮或网页同步时写入；Unity 覆盖前把旧版备份到 `Assets/Resources/WorldPark/.history/MyWorld.previous.worldpark.json`，以“.”开头的目录 Unity 不导入、不进构建）。
- 开始页“开始世界”、世界工坊“开始世界”、构建 Windows 版：都优先用「我的世界」（窗口里没有草稿时，或网页写进了新版本且 Unity 草稿没改动时）。
- 会场体检规则（`Tools/LevelStudioWeb/venue-check.js`，全局 `WPCheck`）：骨架（规则 / 活动 / 反转 / 结局 / 门）、现场（BFS 最快路线 + 读卡时间 2 秒 + 字数/6 对比限时；有鬼时用网页探索内核真跑“直奔出口”看会不会被抓；鬼速 ≥ 玩家、出生点 < 4 格、路线两格内没高草）、博弈（固定出法能否赢 / 看穿后能否赢）。主世界 6 会场：红 0 · 黄 0 · 绿 20。

### B. Windows 电脑端

- **构建 Windows 版**：世界工坊“构建 Windows 版”、开始页同名按钮、菜单 `MarioTrickster/世界工坊 World Workshop/构建 Windows 版 Build Windows`；Unity 关着时双击根目录 `BUILD_WINDOWS_GAME.cmd`（批处理入口 `WorldParkBuilder.BuildWindowsBatch`，参数 `-worldPack`、`-worldOut`）。输出 `Builds/Windows/<世界id>/MarioTrickster.exe` + `先读我.txt`（`Builds/` 已被 Git 忽略）。构建时显式列出场景，**不改 Build Settings**；临时允许拖动窗口大小，结束后还原 PlayerSettings 与原来打开的场景。
- **标题页**（只在 Windows 版启动或“回到标题”后出现；编辑器“开始世界”仍直接开始）：继续旅程（显示“上次停在：会场 · 阶段”）/ 开始新旅程（已有检查点时要点两次）/ 设置 / 退出。
- **Esc 菜单**（剧情字幕卡、博弈桌、现场探索都能按）：继续 / 设置 / 保存检查点（安全阶段）/ **重来本会场** / 回到标题 / 退出。菜单打开时下面的画面只在重绘事件里画：看得见、不接收点击。
- **重来本会场**：`WorldParkProgress.RetryVenue`（与网页 `play.js retryVenue` 同语义：重放到最后一次 travel 进本会场；入口会场则回到旅程开头）；本会场已发现的线索与活动结果撤销，其他会场结果保留；GAME OVER 结局底栏也有“重来本会场”按钮。
- **界面缩放**：新 `WorldParkUi`（`BeginHud` 用 GUI.matrix 按 `Screen.height/900` 缩放，1080p = 1.2 倍；乘以玩家“界面大小” 85% / 100% / 115% / 130% / 150%，存在 PlayerPrefs）。探索 HUD、运行器面板、演出层、字幕卡、博弈桌全部跟随；鬼头顶的“!/?”坐标同步换算。依据：Xbox 无障碍准则 PC 正文 ≥ 18px@1080p（本项目正文 15–16 虚拟像素 × 1.2 = 18–19px）。
- **设置**：界面大小、音量（AudioListener.volume，存 PlayerPrefs）、全屏 / 窗口切换（Alt+Enter 同效）。
- **结束旅程**：保存检查点后回到标题页（原来是停在“本次运行已结束”死角）。
- 运行中被拦截（数据错误）时面板上有“回到标题”“退出游戏”按钮（独立版不再卡死）。

### C. 验证工具

- `Tools/WorldParkVerify/venue-check.cjs`（19 项：主世界全绿 + 5 个坏样例必须被抓到）；run_all 新步骤“S254会场体检”。
- `web-game-smoke.cjs` 修复（桩上下文补 `clearTimeout`，加载 `explore.js` + `venue-check.js`），新增“体检面板红 0”“模板下拉”两条。
- `web-play-smoke.cjs` 新增创作流程段：入口会场体检、模板 6 个、用模板新建 `v_chase_my` 后体检刷新（含“直奔”）、▶ 只玩这个会场从 `v_chase_my` 开始；截图 `22_创作_会场体检与模板`、`23_创作_只玩这个会场`。
- Progress 纯逻辑测试 43 → 60 断言（重来本会场）；Unity EditMode 新测试 `S254_RetryVenueRewindsToVenueEntryWithoutTouchingOtherVenues`、`S254_UiScaleIsClampedAndMenuActionsAreDistinct`。
- 根目录 `RUN_UNITY_TESTS.cmd`：Unity 关着时一键跑原生 EditMode 测试（结果 `Builds/UnityTests/`）。

## 改动文件

- 新增：`Assets/Scripts/WorldPark/WorldParkUi.cs`(+.meta)、`Tools/LevelStudioWeb/venue-check.js`、`Tools/WorldParkVerify/venue-check.cjs`、`BUILD_WINDOWS_GAME.cmd`、`RUN_UNITY_TESTS.cmd`、本文件。
- 修改：`WorldParkRuntime.cs`（标题页 / Esc 菜单 / RestartVenue / ReturnToTitle / EndJourney / 面板缩放）、`WorldParkExploreRuntime.cs`（HUD 缩放 + 共用暂停菜单）、`WorldParkStage.cs` / `WorldParkScene.cs` / `WorldParkTable.cs`（字号 × 界面大小、样式随设置重建；字幕卡 offerRetry；“Esc 菜单”提示）、`WorldParkProgress.cs`（RetryVenue）、`Editor/WorldParkBuilder.cs`（Generate / BuildWindows / BuildWindowsBatch / 菜单）、`Editor/WorldParkWindow.cs`（我的世界同步、复制会场、模板、只玩这个会场、构建按钮）、`Editor/StartHereWindow.cs`（我的世界状态 + 构建按钮）、`FeatureMap.cs`（+5 项、+2 个目标，85 项）、`docs/FEATURE_MAP.md`、`Tools/LevelStudioWeb/shell.html` / `worldpark-app.js` / `build.py` / `index.html`、`Tools/WorldParkVerify/run_all.ps1` / `web-game-smoke.cjs` / `web-play-smoke.cjs` / `Progress/Program.cs`、`Assets/Tests/EditMode/WorldParkProgressTests.cs`。
- 规则内核（`BorderlandGame` / `WorldParkExplore` / `WorldParkPack`）**没有改**：剧情带入模拟仍 46/46。

## 已验证 / 未验证

- 已验证（2026-10-21 10:10 run_all 18 步全部 PASS，无 SKIP）：Unity 真实引用编译（运行时 230 + 编辑器 41 + EditMode 测试 13 个源文件）、核心 288 断言、探索 99、检查点 60、旅程 212、跨端 61 + 35 用例差异 0、探索内核跨端 62958 项差异 0、试玩画面跨端差异 0、会场体检 19/19、剧情带入模拟 46/46、网页点位画布 22、网页博弈冒烟、网页重建（85 项功能地图）、**真实浏览器（无界面 Edge）自动试玩 21 张截图错误 0（含创作流程）**、功能地图文档同步。
- **未验证**（许可证未激活，命令沙箱也读不到用户的 Unity 授权）：Unity 原生 EditMode / PlayMode 测试、Unity 里实际玩、**Windows 版真正构建出 exe 并运行**、标题页 / Esc 菜单 / 设置 / 界面缩放的实际画面、`BUILD_WINDOWS_GAME.cmd` 与 `RUN_UNITY_TESTS.cmd` 实跑、网页“🔗 写进 Unity 项目”与 Unity 自动载入的端到端（浏览器文件夹授权无法自动化）。

## 给用户的 Unity 自测清单（约 20 分钟）

先在 Unity Hub 登录 → 偏好设置 → 许可证 → 添加 → 免费个人版；双击 `OPEN_MARIOTRICKSTER_UNITY.cmd` 打开项目。

1. **开始页**：应看到“我的世界：还没有——开始世界用主世界「空白之城」”和“构建 Windows 版”按钮。（Unity 关着时也可以先双击 `RUN_UNITY_TESTS.cmd` 跑一遍原生测试。）
2. **开始世界**：霓虹亮灯 → ♣3 字幕卡（右上角写“Esc 菜单”）。按 **Esc**：出现半透明菜单；点“设置”把界面大小调到 130%，字应整体变大；返回 → 继续。
3. **现场探索**：走到调查点按 E；按 Esc → 菜单出现、计时停；点“重来本会场”→ 应重新亮灯入场、刚才的线索没了。
4. **博弈桌**（♦5 数字猜心）：打一两轮后按 Esc → 菜单提示“博弈中途不存档”；点“回到标题”→ 出现标题页（四个花色霓虹 + 世界名）；点“继续旅程”→ 回到最近的检查点。
5. **故意输一个会场**：GAME OVER 结局底栏应有“重来本会场”。
6. **结束旅程**：任意结局字幕卡右下角“保存检查点，回到标题”→ 回到标题页（原来停在“本次运行已结束”死角）。
7. **创作**：世界工坊会场区 → 模板选“♠5 鬼抓人” →“用模板新建会场”→ 改个名字 →“▶ 只玩这个会场”，应直接从这个会场开始；再点“保存为我的世界”。
8. **网页同步**：双击 `Tools/LevelStudioWeb/index.html` → 世界乐园页 →“🔗 写进 Unity 项目”→ 选本项目文件夹（网页会问载入哪一边）；在网页改一个会场名字，切回 Unity 点一下世界工坊窗口 → 状态行应写“已自动载入「我的世界」”（如果 Unity 草稿有未保存改动，则只出现黄色提示和两个按钮）。
9. **构建 Windows 版**：世界工坊“构建 Windows 版”→ 确认 → 几分钟后自动打开 `Builds/Windows/<世界id>/`；双击 exe：先是标题页；试 Alt+Enter、Esc 菜单、退出游戏。

哪一步不对，截图或把 `Editor.log`（`%LOCALAPPDATA%\Unity\Editor\Editor.log`）、`Builds\build-windows.log` 发给 AI。

## 审计与后续

完整审计（对照最终诉求 + 外部资料）见 `outputs/borderland-audit-s254/AUDIT.md`（交付物，不进 Git）。本轮没改规则的缺口：签证天数（跨会场生存压力，原作核心）、鬼“最后看见你的位置”残影、IMGUI → uGUI / UI Toolkit（Unity 官方说 IMGUI 不宜做正式玩家界面；S254 先用矩阵缩放把可读性补到 PC 标准）、触屏 / 手柄。
