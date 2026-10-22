# S252 更像游戏：黑桃·鬼抓人、演出层、博弈桌与剧情字幕卡

> 日期：2026-10-20。分支 `genspark_ai_developer`，HEAD 仍为 `ecfd954`；本轮与 S248–S251 一样**未提交、未推送**。
> 给用户看的说明：`outputs/borderland-framework-20261020/S252_更像游戏的一轮.md`；本轮剧情模拟记录副本：`outputs/borderland-framework-20261020/剧情模拟-S252/`。

## 1. 用户要求

- 13:52：做黑桃体力类会场（带追逐者的“鬼抓人”）；补演出效果（亮灯入场、倒计时、出局画面和音效）；澄清“在 Unity 里点开始世界亲自走一遍”这件事——现在到底是游戏，还是只是剧情模拟器看一遍。
- 15:01：**还是需要做游戏，不是文字阅读**；增强代入感、要有反转；先做到还原（不是完美还原），以后再慢慢开拓新关卡。

## 2. 澄清：游戏 vs 剧情模拟（交付时必须对用户说清）

- Unity 里是**真实可玩的游戏代码**：WASD 俯视移动、E 调查、躲鬼、全屏博弈桌上点选、剧情字幕卡点击继续。但**到目前为止没有任何人在 Unity 里玩过**：本机 Unity 批处理在 15:05 再试仍报 “No valid Unity Editor license found”（日志 `.work/borderland-framework-20261020/unity/editmode.log`），我也无法操作 Unity 窗口。
- 剧情带入模拟器是**无画面程序**：用与游戏同一套规则内核，由五种手写性格替玩家做决定，输出逐秒记录。它检验规则、平衡、公平、反转是否真的出现、画面要显示的内容是否完整一致；**不检验**手感、画面观感、界面可读性、音效好不好听。
- 用户亲自体验：在 Unity Hub 登录 → 偏好设置 → 许可证 → 添加 → 免费个人版；然后打开项目，开始页 → 开始世界。

## 3. 本轮改动

### 新增（均带唯一 GUID 的 `.meta`）

| 文件 | 职责 |
| --- | --- |
| `WorldPark/WorldParkStageCues.cs` | 演出纯逻辑：`Entrance`（霓虹扑克牌亮灯，`Card` = 花色 + 点数）、`Activity`、`Eliminated`、`Ending`（GAME CLEAR / GAME OVER / 旅程终点，签证天数 = 难度）、`CountdownBeep`/`Urgent`、`NewlyEliminated`、`ChaseLine`/`ChaseSound` |
| `WorldPark/WorldParkSfx.cs` | 程序合成音效（霓虹嗡声、亮灯咔哒、倒计时滴、激光、通过、通关和弦、GAME OVER、警报、草晃、脚步），不引入外部素材 |
| `WorldPark/WorldParkStage.cs` | 演出叠加层（DontDestroyOnLoad，IMGUI，unscaledTime，点击 / 空格跳过；`Busy` 表示模态演出进行中） |
| `WorldPark/WorldParkTableModel.cs` | 博弈桌纯逻辑：`Build(def, before, state, revealT)` → 座位位置、对你说的话、逐个亮牌时刻、扣命/得分、本轮出局、目标线与结算字、新规则；阶段 choose / reveal / result / done |
| `WorldPark/WorldParkTable.cs` | 博弈桌绘制：圆桌座位、命灯、气泡、翻牌、出局激光、滑块写数字 / 四花色 / 分偷按钮、右上“规则与记录” |
| `WorldPark/WorldParkSceneModel.cs` | 剧情字幕卡纯逻辑：`Build(session, playable)` → 扑克牌、标题、正文、**反转卡**（本揭示阶段被重新解释、且你真的发现过的线索：当时以为 → 其实）、错过的线索名、线索本、结局章、下一个会场的门（开 / 关 + 原因）、旅程进度；`Visible`/`TwistAt`/`ReadyAt` 出字时间轴 |
| `WorldPark/WorldParkScene.cs` | 字幕卡绘制：上下黑边电影画幅、逐字正文、反转卡逐张滑入、门卡片、底栏（线索本 / 旅程 / 保存 / 主按钮），点击显示全部 |

### 修改

- `WorldParkPack.cs` + `worldpark.js`（同语义）：探索点位新种类 `chaser`（见第 4 节）及预检；`parity.cjs` 加 10 个追逐者用例。
- `WorldParkExplore.cs`：追逐者内核（巡逻 → 看见就追 → 跟丢去搜寻 → 回巡逻；草里静止贴身前看不见、草里走动会晃；事件 seen/lost/rustle/giveup/caught）。有追逐者的阶段把 Tick 切成 ≤0.05 秒小片，无追逐者阶段行为不变。本轮调试修了三个内核漏洞：①搜寻中近距离草晃不再换目标（`RustleRetarget`=1.5）；②搜寻行走上限 8 秒（`SearchTravelSeconds`）；③1.5 格内直线可通行时鬼直扑（`DirectRange` + `ClearLine`），修复“在鬼面前左右挪一格它永远抓不到”。追逐者是 C# 独有的运行内核（网页只编辑与预检，不跑追逐）。
- `WorldParkExploreRuntime.cs`：鬼的精灵、视锥、“!”/“?”，脚步声随距离，倒计时大字与滴声，被看见警报与边缘闪红，结束时激光 / 通过演出；有鬼的阶段出口不弹确认。
- `WorldParkRuntime.cs`：入场亮灯与结局盖章（`AnnounceStage`）；`game` 阶段改为全屏博弈桌 `DrawGameTable`（旧 `DrawGame`/`DrawSeats` 文字面板已删除），亮牌演完再放出局激光与结果；`story`/`reveal`/`ending`/房间前改为全屏字幕卡 `DrawSceneCard`。故障、结束、房间结果确认仍用原面板。流程推进与存档语义不变。
- `BorderlandGame.cs` + `borderland.js`（同语义）：`RoundRecord` 新增 `picks[]`、`marks[]`、`target`、`opponent`、`winners`（供博弈桌逐个亮牌）；Snapshot 与 parity 同步映射，parity 加 3 条翻牌数据断言。
- `Editor/WorldParkSpatialEditor.cs`、网页 `worldpark-app.js`/`worldpark-spatial.js`：点位画布“＋ 追逐者”，红色“鬼”标记与巡逻格序号，速度 / 视距 / 巡逻路线表单。
- `Borderland.worldpark.json`（生成草稿 `.work/borderland-framework-20261020/make-world.cjs`）：新增 **♠5 鬼抓人**；三个博弈会场新增入场观察线索（见第 5 节）。
- 剧情模拟器 `Tools/WorldParkVerify/Story/`：人格 `chase` 策略（ignore / flee / evade）、追逐统计、博弈桌逐轮核对、字幕卡核对、反转检验；旅程记录加入【亮灯】【倒计时】【激光】【通过】【桌面】【反转】【错过】等画面标记；`STORY_TRACE=1` 输出逐帧追逐轨迹。
- `FeatureMap.cs`：更新 world.play / world.explore / world.game / test.story，新增 world.stage、world.scene；`docs/FEATURE_MAP.md` 已重新生成（79 项）。

## 4. 数据格式新增：追逐者点位

`ExplorePoint.kind = "chaser"`，复用 `id`、`label`（名字）、`x`/`y`（出生格），新增字段只在 chaser 上导出：

- `speed`：整数，十分之一格/秒，0 = 默认 34（3.4 格/秒；玩家 4.0），允许 10–80；巡逻速度 = 追速 × 0.6。
- `sight`：整数格，0 = 默认 7，允许 1–20；视锥半角 60°。
- `route`：`"x,y"` 字符串列表（≤32），巡逻点必须可通行且从 T 走得到；空 = 原地守，每 1.5 秒转 90°。
- 预检码（C#/网页一致）：`explore_point_kind`、`explore_chaser_speed`、`explore_chaser_sight`、`explore_chaser_route`、`explore_chaser_limit`（每阶段 ≤4）、警告 `explore_chaser_spawn_close`（离 T 少于 4 格）。
- 抓捕半径 0.6 格；近身 1.5 格内无视草丛。

## 5. 主世界变化

- 会场 6 个：♣3 三扇门 → ♦5 数字猜心 / ♥7 看不见的标记 / **♠5 鬼抓人** → ♥9 分还是偷 → K 主办者的房间。链接新增 `v_doors → v_chase`（clear）、`v_chase → v_split`（clear）。
- ♠5 鬼抓人（夜间废弃小学，30×17，限时 45 秒）：最短的一格宽走廊正是“走廊的鬼”的巡逻线；绕行操场有高草掩护，“操场的鬼”绕圈巡逻；线索：公告板（真实但隐瞒了走廊就是巡逻线）、草丛里的工作证（反转：鬼也是参加者，抓不到人他就会出局）。
- 博弈会场没有可调查的地图，反转改用 `trigger: "stage"` 的入场观察（到达 `arrival` 即记入线索本，揭示时翻转）：♦5「大屏幕的空栏」「白石的笔记本」、♥7「项圈侧面的小灯」「广播的措辞」、♥9「座位上的节目单」「狐的口头禅」。♥7 的“荷官”真相与内核一致（说谎座位永远宣告自己的真实花色，不会出局）。
- 作者预期 `Borderland.expect.json`：人格加 `chase`（莽/真 ignore、慎 flee、思/随 evade），随的路线改为先走 ♠5，并写入 ♠5 预期（莽、真被抓；思、随通关）。

## 6. 剧情带入模拟结果（46/46 通过）

- 全程旅程：莽、真止步 ♣3；慎 ♣3 → ♥7 → ♥9 出局（差一轮到终点）；思 ♣3 → ♦5 → ♥9 → K 离开；随 ♣3 → **♠5** → ♥9 → K 离开。
- ♠5 单会场：莽、真被抓（迎面撞上走廊的鬼）；慎被看见后甩掉、通关；思、随绕开视野、通关（25 秒 / 45 秒）。
- 新检验：鬼的威胁（2/5 被抓）、追逐张力（慎被看见仍活着离开）、博弈桌 90 轮画面与规则记录一致、字幕卡 129 张内容完整、**六个会场的揭示都至少让一种人格翻开过“当时以为 → 其实”**（♣3 6 张、♦5 6、♥7 6、♠5 4、♥9 8、K 15）。

## 7. 验证

`Tools\WorldParkVerify\run_all.ps1` 13 步全部 PASS（15:55）：Unity 引用编译（新文件无警告）、S248 核心 288 断言、探索 99 断言（含追逐者与“草里来回晃不能冻住鬼”“鬼面前左右挪会被抓”）、检查点 43、三会场旅程 212、点位画布 2/2、跨端 61 世界 + 35 博弈用例差异 0、剧情模拟 46/46、网页点位画布 22、网页博弈彩排冒烟、网页语法、网页重建（79 项功能地图）、功能地图同步。

**未验证**：Unity 原生 EditMode/PlayMode；Play 实机（博弈桌、字幕卡、演出、鬼抓人的画面布局、手感、可读性、音效）；真人试玩。博弈桌与字幕卡是 IMGUI 实现，只经过编译与纯逻辑核对，实际屏幕排版可能需要按真机截图微调。

## 8. 下一步建议

1. **P6 实机与真人试玩（最优先）**：激活 Unity 个人许可证 → 跑 EditMode → 开始世界走完六会场；重点看博弈桌与字幕卡排版、♠5 鬼的速度与限时、♦5 难度（1/5 存活）。
2. 演出层第二轮：会场入场的过场镜头、博弈桌倒计时（原作的限时作答）、出局时座位灯熄灭动画。
3. P9 更多博弈积木与会场；P5 世界分享；P10 冗余删除（需 Unity 可运行后确认无场景引用）。
