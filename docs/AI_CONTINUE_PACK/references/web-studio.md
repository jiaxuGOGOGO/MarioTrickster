# 分册：网页关卡设计台 & 收到"设计单"时怎么做

用户通常会一起发：**设计单**（.md 或粘贴的文字）+ **关卡包**（`*.levelpack.json`，所有关卡 + 提案）。完整流程见 `docs/step1/S206_LEVEL_LIBRARY_AND_WORKFLOW.md`。
- 提案状态：💡 想法 = 只讨论给建议、不实现；✅ 确认要做 = 实现；✔ 已实现 = 不动。
- 关卡包导入 Unity：`LevelLibrary.ImportPack` → `Assets/Levels/Library/名字.txt`；未实现字符记 `# Pending:`，实现后让用户**重新导入同一个关卡包**还原。
- 你实现完机制后：在沙盒里自己跑一遍 `LevelPack.Parse` + `LevelWorkshopModel.Check` 验证用户的每一关（用关卡包文件），把结果（✓/✗ 及原因）写进汇报；需要的关卡还可以放进 `Assets/Levels/Library/` 一起随补丁交付。

用户在网页 `tools/LevelStudioWeb/index.html`（单文件，双击打开）里画关卡、写批注、提新机制、勾选删改，然后把**设计单**（markdown，开头是"# MarioTrickster 设计单（来自关卡设计台）"）发给你。也可能附 `.studio.json`。

## 收到设计单的处理顺序
1. **读全**：关卡 ASCII（```text 块）、设计意图、时间线、网页预检问题、格子批注（坐标 x 左 0、y 下 0）、新机制提案、删改项。
2. **先复述计划**（一句话一项）：哪些照做、哪些需要调整及原因（违反宪法 / 物理做不到 / 和现有机制重复）。不要静默改掉用户的设计。
3. **新机制提案** → 按 `new-element.md` 16 步实现。提案里"还需要说清"的条款（H3 预警 / A2 代价反制 / H9 控人时长 / H1 改地形）你来补上合理默认值，并在汇报里写出来让用户确认。技能类按 `trickster-skill.md`，马里奥行为类按 `mario-ai.md`（性格走特质表）。
4. **关卡** → 加成 `LevelWorkshopModel.XxxSample`（名字用用户起的），按批注逐条落实；跑 `verify.sh`（含炸弹策略模拟、所有样板可玩）。网页预检的问题要么修掉，要么说明为什么不是问题。
5. **删改项** → 先列影响（哪些样板/测试/连锁/文档在用），能改就改，真要删：Registry/Catalog/Theme/NotProbed/样板/测试/文档一起删，并说明。
6. **重建网页**：`python3 tools/LevelStudioWeb/build.py`（新元素自动出现在网页元素库），`index.html` 随升级包一起交付。
7. 汇报：做了什么（按设计单条目对照）、哪些调整了及原因、用户下一步。

## 用户用别的工具画的图
- 网页设计台能直接导入：Tiled（.json 需层格式 CSV / .csv）、LDtk（.ldtk 或超简导出 .csv）、像素图 .png（1 像素 1 格，颜色见"导出调色板"）。让用户先在网页导入、检查、生成设计单再发来最省事。
- 用户直接发 Excalidraw / 手绘 / 截图：用 `gsk understand_images` 读图，转成 ASCII 网格（外圈 W、底行 #、M/T/G/o 各一个），在回复里贴出网格请用户确认后再实现；拿不准的格子标出来问，不要猜。
- 跳跃辅助的含义：绿 = LevelPathPlanner 一步（AI 真会走），黄 = L2 一步（物理可达但 AI 不稳，H8 临界区）。

## 网页维护
- 数据唯一来源是 C# 源码（build.py 解析 `AsciiElementRegistry` / `ElementCatalog` / 样板）；**不要手改 index.html**。
- 规则改动（跳跃、摆放规则、死局）要同步 `logic.js`（纯逻辑，可 node 测：vm 载入 data+logic 后 `check(makeWorld([]), grid, true)`），保证四个样板结果与 Unity 一致。
- 网页检查只是预检；最终以 verify.sh / Unity 为准，汇报里不要把"网页通过"说成"通过"。

## S207
- 宽 >64 或高 >16 = 大房间 → 游戏里智能跟随镜头（死亡细胞式）+ 屏外红箭头 + 小地图；回合时间按路线自动放宽。上限仍 128×48。
- 工具：画笔 B / 矩形 R / 填充 F / 吸管 I（吸完自动回原工具；Ctrl+点击随时吸）/ 移动 V（拖动、框选、方向键、Del、Ctrl+C/V；外圈与 M/T/G/o 受保护）。logic.js 的 selectAt/moveBlock/clampMove/copyBlock/pasteBlock/clearBlock 与 LevelWorkshopModel 同名函数同规则——改一边必须改另一边。
- "显示（只改画面）"勾选框不改关卡/检查/导出；"最坏情况预览"勾着时禁止绘制；"游戏一屏"= cameraPlan（与 Step1RoomCamera 同阈值）。
- 关卡包 `rules: 'S207'`。

## S208 起步帮手（与 Unity `LevelDesign/LevelBlueprint.cs` 同规则同数据，改一边必须改另一边）
- logic.js：`PATTERNS`(8 个印章：rows 从上到下，`stand`=马里奥站的行，`_` 不动、`*` 主角) / `stampPattern`（不动外圈、不覆盖 MTGo）/ `WIZ_RECIPES`+`wizardLevel(star, 20|30|40, idea)`（确定性；48/64/94 宽 × 高 12，下 3 层地面，站 y=3；段 ≥17 格补伏击点）/ `beatBounds` / `routePasses`（去程+回程）/ `rhythm`（RHYTHM 常量）/ `coverHints`（COVER_CHARS + 房内隔墙，5 格）。
- verify.sh 第 4 步：node 跑网页 wizardLevel 生成 24 张 → `sim/wiz_web.json` → C# 逐字对照 + 可玩 + 炸弹策略；8 印章单独可玩。改了印章/配方后两边都要改，否则 verify 红。
- 生成的草稿要求"干净"：无节奏提醒、无转移点提示（EditMode 测试 WizardDraftsArePlayableCleanAndDeterministic）。
- 设计原则（Morai Maker 研究）：AI 起草只在用户点"新建/盖章"时发生，从不自动往图里塞；同样选择 = 同样结果。
- 关卡包 levels[].beats（5 个 x）可选；rules 'S208'。

## S210 大地图页
- `tools/LevelStudioWeb/overworld.js`：逐行移植 `Assets/Scripts/Overworld/OverworldMap.cs`（Dijkstra 同样平手规则，检查文字一致）。build.py 生成 `OW_TILES`（OverworldCatalog）、`OW_SAMPLE`（OverworldPack）、`OW_ROOMS`（SampleRooms → SAMPLES 键）。
- 存储 localStorage `mariotrickster.studio.overworld.v1`；关卡包多一个 `overworlds:[{kind:"overworld",name,goal,grid,doors:[{n,time,room}],notes}]`。
- verify.sh 用 node 生成 `sim/ow_web.json`（样板 + 拆桥反例），Check.cs 对照。
- S212：大地图页快捷键在 `owKey`（大地图页打开时全局 keydown 先交给它）；`owMarioAt` 与 C# `OverworldGuide.MarioAt` 逐行一致；`owEditRoom` 把内置样板房间复制进关卡库再打开。
- S214 同步：网页 app.js 末尾 `SY`（File System Access API）。网页 → `Assets/Levels/Inbox/web_*.json`（Unity `Editor/WebSync.cs` 收完即删；`mariotrickster-play` = 试玩请求）；Unity → 网页读 `Assets/Levels/Library/*.txt`、`Assets/Levels/Overworld/*.txt`（`syLevelFromTxt` 还原 # Pending 字符）。哈希记在 localStorage `mariotrickster.sync.hashes`，目录句柄在 IndexedDB `mariotrickster.sync`。冲突：首次连接 Unity 为准 + 网页留副本；之后双改留网页（Unity 覆盖前 `WebSync.BackupBeforeWrite` 备份到 Library/MarioTricksterHistory）。
- 改关卡库 .txt 格式时：`LevelPack.ToText` 与网页 `syLevelFromTxt` 一起改，verify 里 S214 会逐字对照。
