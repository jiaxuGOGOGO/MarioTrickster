# 分册：关卡 / 样板 / 工坊 / 箱庭

## 搭建范围（S206，网页与 Unity 同一规则 `LevelWorkshopModel.BoundsIssues`）
宽 12–128、高 6–48；最左/最右列 W、顶/底行全实心；M/T/G/o 各一个；高 >16 镜头跟随。

## 坐标与格式
- ASCII 网格，**第 0 行在最上面**；工坊/检查里的 `(x,y)` 是 **x 从左 0、y 从下 0**。转换：`row = 高度-1-y`。
- 宽度通常 48，最高 48 行（`FloorStacker.MaxFloors=11`）。外圈必须 `W`，底行实心。
- 必须各 1 个：`M` 马里奥出生、`T` 捣蛋者出生、`G` 出口、`o` 宝物。
- 随机槽位 `1 2 3`（`Step1Layout.Slots`）只在默认房间用。

## 物理常识（不看会做出走不通的图）
- 马里奥跳高 ≈2 格，AI 只在水平距离 <2.25 时往上跳 → 楼梯/台阶间隔 ≤2 格高、左右错开 ≤2 格。
- 捣蛋者跳力 20（≈2.5 格）。
- 掉下一层后**必须有路回去**（单向台面 `-` 阶梯），否则死局检查报"再也回不到出口"。
- 弹簧 `J` 头顶空 4 格；大炮炮口前空 3 格；绳套/铁笼头顶空 2 格；毒池连续 ≤3 格；通风管成对。
- 大多数物件 `needsSupport`：脚下必须实心。

## 箱庭原则（用户要的魂系手法）
- 一栋楼 = 一个连续空间（不切场景）；楼与楼之间才切换（"地下一百层" = 约 25 栋串联，**尚未实现**）。
- 每层一个身份（主机关 + 藏身处），层间多条路，**单向捷径门 `|`**（绕一圈回来打开）、**秘密裂墙 `%`**、环路。
- 用工坊"箱庭总览"看楼层身份/连接/捷径省步数；"连招路线"看哪些机关能连成一套（种类越多越好）。
- 参考：Undead Burg / Stormveil（[Level Design Book](https://book.leveldesignbook.com/studies/sp/undead-burg)）、Metroidvania 地图设计（[PC Gamer](https://www.pcgamer.com/how-to-design-a-great-metroidvania-map/)）。

## 样板在哪
`Assets/Scripts/Editor/LevelWorkshopModel.cs`：`PrisonSample`（两层）、`HakoniwaSample`（四层箱庭）、`LureSample`（诱捕走廊，S200）。默认房间：`Step1PrankRoomBuilder.Room`。
新样板：加 `public static readonly string[] XxxSample`（体检脚本会**自动发现**以 Sample 结尾的字段）+ 在 `LevelWorkshopWindow.DrawToolbar` 加按钮。

## 策略死局（S202）
炸弹能炸普通地形 → 静态死局检查漏掉"炸掉台阶把他困在坑里"。`StrategySim` 模拟最坏对手，构建时自动加固承重格（铆钉）。新图若 verify 报"炸弹仍能困住"→ 给坑里加第二条回去的路（单向台面 `-` 阶梯），别关加固。工坊"策略模拟"开关可视化；"检查轨迹"看自动检查/陷阱试探的实际走位。

## 改完必做
`bash scripts/verify.sh` → 第 4 步会逐个样板报 `✓ 可以试玩` 或具体红格原因。用 Python 改样板时**按行列精确替换并断言原字符是 '.'**，行长度必须保持 48。

## S207 大房间
- 宽 >64（`maxWholeRoomWidth`）或高 >16 → 开局自动 `bigRoomCamera`（默认 SmartFollow）。C 键 4 种镜头。屏外红箭头 `Step1OffscreenMarkers`、小地图 `Step1MiniMap` 只给玩家看（H4）。
- 回合时间 / 自动检查超时由构建器按 `StrategySim` 路线秒数放宽（`RoundTimeLimit` / `HandsOffTimeout`），默认房间数值不变。
- 上限仍 128×48（186 宽实测模拟 31s 且加固失败）。要更长旅程 → 多房间连廊，不要放大单图。
- 样板 `LongHallSample`（94×15）。

## S208 起步帮手（用户说"不知道从哪下手"时先推荐这个）
- 网页"＋ 新关卡（向导）"/ 工坊"向导…"：点子 + 主角机关（~ n [ J Y Q K C）+ 20/30/40 秒 → 起承转合 4 段可玩草稿。
- 8 个模式印章（伏击点/滑铲送火/高低两路/陷坑回廊/弹射落点/关门打狗/回马枪/炮台走廊）；用户想加自己的印章 → 两边 Patterns 一起加 + verify。
- 节奏：紧张 = 经过机关 ±1s；连续紧张 ≥8s / 连续没事 ≥10s 提醒。转移点：机关 5 格内无 b c U 1 2 / 隔墙 → 提示（不是硬错误）。

## S210 小镇大地图（星露谷视角）
- 文件：`Assets/Levels/Overworld/名字.txt`；`# Overworld:` `# Goal:` `# Door: n | HH:MM | 房间名` `# Note: (x,y) 文字` + 网格（第一行 = 最上，y=0 在底）。
- 格子只能用 OverworldCatalog 的 15 种（和房间字符表**分开**）。门 1–9 画在房子墙面下方一格；房间名先找关卡库同名，再找 `LevelWorkshopModel.SampleRooms` / 默认房间。
- 检查 `OverworldMap.Check`（网页 `owCheck` 同文字）；无人捣乱一天 `OverworldWalker.SimulateDay`。改检查文字要两边一起改，verify 会逐字对照。
- 新样板小镇：写进 `OverworldPack`，build.py 自动读。
- S212 小镇工坊快捷键（网页同）：B R F E I、Alt+点吸管、1–9 门、Ctrl+Z/Y、Ctrl+S、F5、Ctrl+滚轮、中键拖、Ctrl+C/V 与网页互通；门行 ◎ 定位 / ✎ 打开房间；⏱ 时间滑条。
- 一键试玩 `OverworldBuilder.BuildAll`：每门一个 `Assets/Scenes/Overworld/Room_N.unity`（带 OverworldRoomLink）+ `Town.unity`，登记 Build Settings。

## S215 一整天的节奏（一天总览会提醒）
- 一扇门第一次出现的机关最好 1–2 种（≥3 种会提醒"一次教太多"）；连续两扇门主角机关别一样。参考 GMTK 4 Step Level Design：先教、再变化、再考、再收尾。

## S217 大世界
- 小镇工坊 / 网页"↔ 扩展"：四周/单向 +8/+16、×2、裁 4；往哪边扩拆哪边围栏、外圈封树、老镇不变、批注平移；裁掉东西先问。上限 192×128，更大 → 拆多张小镇。
- 游戏里地面是一张贴图、工坊只画可见格 → 大图不卡。`-`/`=` 镜头远近。

## S218 小镇大机关（巨炮 K + 靶心 X / 滚石 O / 水塔 U）
- 规则全在 `Assets/Scripts/Overworld/OverworldProps.cs`（纯几何）+ `OverworldTown` 的 Arm/Fire/Impact/TickBigs/DodgeCell；网页 overworld.js `owProps*` 逐行移植。改检查 / 总览文字 → 两边一起改（verify 对照 ow_props.json）。
- 巨炮：同一行/列最近的靶心；炮口 = 朝靶心方向紧挨的 ≤3 个能走格；落点 = 靶心（大风偏 3 格）→ 最近能走格（环形扫描）。检查：没有靶心 / 炮口堵死 = 红；**任一风向的落点走不回家 = 红（H1）**。
- 滚石：离开推的人的方向滚；撞碎 c f、碰到别的挡路就停；**永远不碰最外一圈**。四向最长 <3 格 = 黄。
- 水塔：半径 3（雨天 4）内的 . = " 变泥地 g；冲活香蕉皮。
- 连锁：冲击点（炮弹落点 / 滚道尽头）1.5 格内的大机关 → 深度 +1 同样预警再发动。总览 `Describe` + `LongestChain`（≤12 个大机关）。
- 地形改变只"打开"（撞碎 / 变泥），记在 `OverworldSession.Changed`，当天有效；新一天清空。新大机关也必须满足"只打开不关死"，否则要进 CheckReach 的最坏情况检查。
- 天气 `OverworldEvents.Of(地图名, 第几天)`：第 1 天晴；大风/雨/雾/赶集。赶集日 ApplyTo 改门时间（仍有序 ≥15 分钟、≤20:00）。
- 联动：`OverworldSession.CarriedDaze` → `OverworldRoomLink` → `MarioMindDriver.AddStartDelay`；`ReloadDoor`（守住一户）→ `OverworldTown.Reload` 装填旁边最近的用过的巨炮/水塔。
- 样板 `OverworldPack.BigSampleText`（星露大镇 72×40，最长 3 连）；build.py 读成 `OW_BIG_SAMPLE`。
- 想加第 4 种大机关：OverworldCatalog 加 T(...) → OverworldProps.IsBig + 几何函数 + CheckCounts/CheckReach/Describe/Triggers → OverworldTown.Fire 分支 → OverworldGame 造型 → 网页同步 → sim S218 块加一行 → 测试。

## S219 巨炮能坐能瞄 + 山地 + 雷雨 / 酸雨 / 泥石流
- 瞄准纯逻辑：`OverworldProps.DefaultAim / AimStep / AimLanding / AimOk`（顺着 = 远、反着 = 近到 3 格就调头、横着 = 转；落点走不回家 = 不许打，H1）。瞄准记在 `OverworldSession.CannonAim`（当天有效），远程 L / 连锁震响都打瞄好的地方；没瞄过 = 靶心 X（S218 的图不变）。
- 你坐炮：`OverworldTown.Seat`（E 坐进 / 方向键 Input.aim 1–4 / L 发射 / E 下来 / overworldCannonSeatSeconds 自动）。马里奥坐炮：`Ride` + `PlanRide`（每站一次，两次 BFS `StepsField`，省 ≥ overworldMarioCannonSaveSteps 才坐；MarioWary 有 'K' 就不坐）→ 瞄 overworldMarioCannonAimSeconds → 飞；`TryTamper` = 你 L 拨歪 → 他落地晕 + 记住 K。
- 山地：`Height`（A=2、^=1）进 `OverworldMap.LineOfSight`（中间格比两端都高 = 挡）；`^` 走 0.75 倍速、站上去 L +1 格（`PrankRange`）；`h` 山洞 hides=true，`CaveExit` 两两配对（扫描顺序），E 钻过去（`TryCave` 在 TryDoor 之前）。马里奥寻路不走隧道。
- 灾害：`MudDir`（挨着山 A 的山丘，方向 = 离开山）、`MudLane` 6 格、`Muddable` 只改地面 / 木箱栅栏（门 家 洞 靶心 不改）；触发 = `Impact` 在 `OverworldEvents.Wet`（雨 / 雷雨 / 酸雨）时 `MudSources` 1.5 格内；水塔淹到源头 = 山洪（任何天气）。闪电 = 路灯 'i' 在 Storm 天可被 Arm（L 或连锁），FireLightning 1.5 格晕 + Impact。
- 天气池 `OverworldEvents.Pool(map)`：基础 5 种 + 有路灯 → Storm + ≥2 山洞 → Acid。**一定用 `Of(map, day)`**（旧 `Of(name, day)` = 基础 5 种，只给老测试用）。网页 `owDayOfMap`。酸雨 ApplyTo：高草 → 草。
- 样板 `OverworldPack.MountainSampleText`（星露山镇）；build.py 读成 `OW_MTN_SAMPLE`；sim 块 "S219"，verify 生成 ow_mtn.json（检查 + 总览 + 天气 30 天 + 预览 14 天 + 24 步瞄准 + 5 条山丘视线）。
- 调参 v20：overworldCannonSeatSeconds 6、overworldCannonFireSeconds 0.6、overworldMarioCannonAimSeconds 1.6、overworldMarioCannonSaveSteps 10、overworldAcidSight 0.7。

## S226 机关浪费门槛
- 样板房间：马里奥所有可能路线（含捷径打开/掉落/谨慎绕开）3 格外的机关算浪费，每个房间 ≤15%；每种机关 ≥2 个搭档。
- 小镇：每个镇泥石流至少 1 处冲到他每天走的路、滚石/水塔至少一半碰得到、至少 1 门炮罩住他的路。新增小镇地图前先跑 sim。

## S228 钟楼 B / 轰出窗户
- 钟楼 `B`：声音型大机关（IsBig），不伤人不改地形；放在他路线旁 3–5 格、离门远一点。每张图 1 个。
- 房间炮打中他 → `OverworldSession.WindowFlingDoor` → 回小镇 `OverworldProps.WindowLanding`（背离房子 6 格、走得回家，否则不轰）。
