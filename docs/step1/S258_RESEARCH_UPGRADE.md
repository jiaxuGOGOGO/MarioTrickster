# S258 对照业界做法升级：Unity 危险按钮、博弈参数表单、图上画鬼路线

日期：2026-10-21。用户 17:30 的要求：“确认是否这个是目前最优解决方案，参考全网资料，然后按照最合适的顺序升级优化。”给用户看的版本是 `outputs/world-cockpit-s258/调研与升级报告.md`；这份是给接手 AI 的。

## 1. 结论

S257 的驾驶舱和图上编辑地图，大方向和业界的游戏工具设计做法一致：作者在看得见的世界上直接改，常用的放在第一层、细节按需展开，每一步都能撤销，入口按“想做什么”组织。但对照下来还差三处，本轮都已补上。第一处，Unity 世界工坊里“从当前小镇复制地图文本”会整张替换地图，却没有确认，也不能撤销。用户在网页上踩过同类的坑（S256），而 Unity 是正式端，所以排第一。第二处，网页的博弈参数只能写 JSON，可六个会场里有三个的核心玩法就是博弈；Unity 那边早就是表单（`DrawGameFields`），两端不一致。第三处，鬼的巡逻路线是最后一项还要手打坐标的空间数据。

## 2. 依据（摘要）

| 原则 | 来源 | 对应到本项目 |
|---|---|---|
| 作者看到世界的同时就能改；敌人按路径走时，编辑器应把路径画出来，不让作者在脑中推算 | Rouse《Designing Design Tools》[1] | S257 已做地图画布；本轮补“图上画鬼路线”和连线显示 |
| 直接操作：对象可见，用小步、可逆、立即可见的动作改；同时保留精确输入；不可用时就地说明 | NN/g Direct Manipulation [2] | 路线点一下加、再点去掉、可撤销；巡逻格文本框保留；不能放的格写清原因 |
| 受约束的选项（向导 / 只能从一组里选）减少出错 | GDC 2025 工具设计圆桌笔记 [3] | 博弈参数改为按钮 + 下拉 + 有范围的数字 |
| 渐进披露：首屏只放常用项，次级入口要明显，层级不超过 2 层 | NN/g Progressive Disclosure [4] | 驾驶舱 → 世界工坊 → “显示高级格子”；表单只显示当前玩法用到的字段，JSON 收进高级 |
| 路径用点列表或折线表示，挂在实体上、画在图上 | LDtk 实体字段 [5]、Tiled 折线 [6] | 鬼的 `route` 本就是点列表，只缺图上编辑 |
| 节点图在小项目里直观，分支多了会“爆炸” | Twine [7]、articy:draft [8]、r/gamedesign 讨论 [9] | 本项目每会场 6 个阶段、6 个会场，规模小，S257 的图标流程视图合适；“拖线改分支”可以做，但不急 |
| 危险的编辑器操作先模态确认 | Unity `EditorUtility.DisplayDialog` [10]、`Undo.RecordObject` [11] | 用 DisplayDialog 确认，再加“换回替换前的地图”；没用 Undo.RecordObject，因为 pack 不一定是窗口的序列化字段，而且本机没法实测 |

## 3. 顺序与理由

① Unity 危险按钮：改动最小，堵住的是用户已经踩过的同类事故，而且在正式端。② 网页博弈参数表单：作者摩擦最大（JSON 要写花括号、引号和英文字段名），覆盖三个会场，并且让两端一致。③ 图上画鬼路线：最后一项手打的空间数据，只影响 ♠5，并且已有文本框可用，所以排最后。

以后的候选（未做，等用户实际用过再定）：④ 在流程图上拖线改成功 / 失败分支（现在用下拉，可用但不直观）；⑤ 中文化剩下的英文字段（timeout、speed、sight 等）、ID 自动生成；⑥ 画鬼的视野扇形；⑦ 顶部 7 个页签收纳旧工具。GDC 圆桌最重要的一条是“先坐在用户身后看他用”，所以最好的下一步是让用户按指南实际做一个会场，看他卡在哪里。

## 4. 改动

| 文件 | 改了什么 |
|---|---|
| `Assets/Scripts/Editor/WorldParkWindow.cs` | “从当前小镇复制地图文本”先弹 `DisplayDialog`（整张替换 / 点位不跟着移动 / 原小镇不改写），确认后才替换；记住替换前的文本（`[NonSerialized] mapUndoNode / mapUndoText`），出现“换回替换前的地图”；两个分支都 `Check()` + `GUIUtility.ExitGUI()`。整个文件统一为 CRLF（原来混了 LF） |
| `Tools/LevelStudioWeb/worldpark-gameform.js`（新） | 表单纯逻辑 `WPGameForm`：玩法 / 人格列表与中文名、范围（轮数、命数 1–20，比例 1–99，目标分 1–40，与 Unity 滑条相同）、`fields(mode)` 只给当前玩法的字段、`set / seatSet / seatAdd / seatRemove / canRemove`（返回新对象，不改原对象；新参加者 `npc` / “新参加者” / `reasoner`，与 Unity 相同）、`summary`、`explain`（只换说法）。不判断对错 |
| `Tools/LevelStudioWeb/worldpark-app.js` | `wpGameForm` 改为表单；新增 `wpGameApply / wpGameSet / wpGameSeat / wpGameSeatAdd / wpGameSeatRemove`，都走 `wpPersist(false, 标签)`（进改动记录、可撤销）；JSON 文本框收进 `wpAdv`；画布 `onCommit` 遇到 `route` 时整页重绘，让巡逻格文本框同步；探索区提示加一句怎么画路线 |
| `Tools/LevelStudioWeb/worldpark-spatial.js` | 新模式 `route` + 按钮“✎ 鬼的路线”（没选中鬼时就地提示）；`routeToggle(shape, route, x, y)`：点已有格就去掉，新格按 `placement` 检查（可通行、出生点走得到）后加到末尾，上限 `MAX_ROUTE = 32`（与 `explore.js` 的 `MaxChaserRoute` 相同）；格式 `x,y` 用 `WorldPark.chaserCell` 解析；画路线时不拖动点位；路线画成连线，虚线表示从最后一格回到第 1 格 |
| `Tools/LevelStudioWeb/build.py`、`cockpit.css`、`worldpark-cockpit.js` | 表单模块并入构建；`.wpg*` 样式；驾驶舱两句说明 |
| 测试 | `worldpark-gameform.test.js`（新，61 项，含与 `WorldParkWindow.cs` 标签和范围逐项对照、主世界三个博弈“改了再改回”一致）；`worldpark-spatial.test.js` 增至 34 项；`run_all.ps1` 新步骤“网页博弈参数表单”；驾驶舱浏览器验收增至 90 项（真实点击改轮数、切玩法、加参加者、驾驶舱撤销三步回原样；真实鼠标点鬼、画一格、再点去掉、画布撤销回原样） |
| 文档 | `FeatureMap.cs` 登记 `web.gameform`、`web.chaserroute`、`world.mapreplace` 并重新生成 `docs/FEATURE_MAP.md`；指南第 5 节（鬼路线）、第 7 节（博弈表单）、第 12 节表格第 1、5 条；课程练习 2b；交接文件 |

规则层（`BorderlandGame.cs` / `borderland.js` / `explore.js` / `WorldParkExplore.cs`）和所有世界数据都没改，所以不需要新增 parity 用例。

## 5. 验证

已验证：`run_all.ps1` 全部通过（见第 6 节的实际结果）；Unity 引用编译通过（`WorldParkWindow.cs` 能针对 Unity DLL 编译）；真实浏览器里的表单和路线操作见上表；截图 `.work/worldpark-verify/cockpit/博弈表单.png`、`鬼路线.png`。

未验证：Unity 里没有人点过新的确认框和“换回替换前的地图”（许可证未激活，批处理与 Play 跑不了）；网页表单与 Unity 表单只在源码层对照过字段，没有在 Unity 里并排看过；鬼路线改了以后好不好躲，要用户 ▶ 只玩这个会场 实际玩了才知道。

## 6. 一键验证结果

2026-10-21 17:51 `Tools\WorldParkVerify\run_all.ps1` 退出码 0：28 步 PASS、0 步 FAIL、0 步 SKIP。其中新增 / 相关步骤：网页博弈参数表单 61 项、网页点位画布 34 项、网页驾驶舱模型 144 项、网页博弈彩排冒烟通过（它在 Node 里按清单加载网页脚本，本轮把 `worldpark-gameform.js` 补进清单，与 build.py 一致）、S257 真实浏览器驾驶舱 90 项、S253 真实浏览器试玩通过、功能地图 94 项同步、Unity 引用编译通过、剧情带入模拟 46/46。

## 参考

[1] Richard Rouse III, Designing Design Tools, Game Developer. https://www.gamedeveloper.com/design/designing-design-tools
[2] Nielsen Norman Group, Direct Manipulation: Definition. https://www.nngroup.com/articles/direct-manipulation/
[3] Robin-Yann Storm, Notes of the Tool Design Roundtables at GDC 2025. https://rystorm.medium.com/notes-of-the-tool-design-roundtables-at-gdc-2025-0b9885730ff5
[4] Nielsen Norman Group, Progressive Disclosure. https://www.nngroup.com/articles/progressive-disclosure/
[5] LDtk, Entities. https://ldtk.io/docs/general/editor-components/entities/
[6] Tiled forum, Associating enemies with polylines. https://discourse.mapeditor.org/t/associating-enemies-with-polylines/2670
[7] Twine Reference, Navigating. http://twinery.org/reference/en/editing-stories/navigating.html
[8] articy:draft, Flow view. https://www.articy.com/help/UI_View_Flow.html
[9] r/gamedesign, Node-based narrative tools might not scale as well. https://www.reddit.com/r/gamedesign/comments/1rrx7ep/
[10] Unity Scripting API, EditorUtility.DisplayDialog. https://docs.unity3d.com/6000.6/Documentation/ScriptReference/EditorUtility.DisplayDialog.html
[11] Unity Scripting API, Undo.RecordObject. https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Undo.RecordObject.html
