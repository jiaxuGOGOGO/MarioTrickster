# 分册：项目地图（文件在哪 / 关键数值 / 字符表）

## 状态（S200 交付时）
- Registry 内置条目 46（`BUILTIN_ENTRY_COUNT`，S241 +i v）；`BuilderVersion` 17；`MarioMindTuningSO.CurrentDataVersion` 14（S203）；EditMode 测试约 385 个（Step1RushMarioTests 100 个）。
- **以 repo 为准**：开工时用 `grep -n "BUILTIN_ENTRY_COUNT =\|BuilderVersion =\|CurrentDataVersion =" -r Assets/Scripts` 重新确认。

## 关键文件
| 用途 | 路径（Assets/Scripts/…） |
|---|---|
| 字符登记 | `LevelDesign/AsciiElementRegistry.cs` |
| 元素说明书 / 摆放规则 / 对比度 | `LevelDesign/ElementCatalog.cs` |
| 主题贴图槽 | `LevelDesign/LevelThemeProfile.cs` |
| 死局 / 可达 / 寻路 | `LevelDesign/LevelDeadlockAnalyzer.cs`、`LevelReachabilityAnalyzer.cs`、`LevelPathPlanner.cs` |
| 连招路线 / 箱庭分析 / 监狱塔 | `LevelDesign/ComboRouteAnalyzer.cs`、`HakoniwaAnalyzer.cs`、`FloorStacker.cs` |
| 第 1 步房间构建（接线中心） | `Editor/Step1PrankRoomBuilder.cs`（`Room`、`BuilderVersion`、各 Configure） |
| 关卡工坊 | `Editor/LevelWorkshopWindow.cs`、`Editor/LevelWorkshopModel.cs`（样板、`Check`、`QuickCheck`） |
| 新字符必须有说明（S238 起） | `LevelDesign/ElementCatalog.cs` `Unexplained` |
| 马里奥性格 / 绕路 | `Gameplay/Step1/MarioPersonality.cs`、`LevelDesign/DetourPlanner.cs` |
| 马里奥 AI | `Gameplay/Step1/RushMarioMind.cs`、`MarioMindDriver.cs`、`MarioEyes.cs`、`MarioVision.cs`、`SuspicionMeter.cs`、`Step1StuckRescue.cs`、`MarioDoorKick.cs` |
| 全部调参 | `Gameplay/Step1/MarioMindTuningSO.cs`（资产 `Resources/Step1/RushMarioTuning`） |
| 捣蛋者技能 | `Gameplay/Step1/TricksterKit.cs`（B 炸弹/Z 缩小/`TricksterBomb.Blast` 统一爆炸）、`DecoyAbility.cs`、`ChainPlan.cs`、`TauntAbility.cs`、`MarioTimeStop.cs`、`RandomPickups.cs` |
| 策略模拟（炸弹困人→加固、路线时间线） | `LevelDesign/StrategySim.cs` |
| 回放 / 陷阱试探 / 自动检查 | `Gameplay/Step1/ChainReplay.cs`、`Step1TrapProbe.cs`、`Step1HandsOffCheck.cs`（ProbeMode、轨迹） |
| 连招 / 手感 | `Gameplay/Step1/Step1Combo.cs`、`Step1ComboFeel.cs`、`Step1Hitstop.cs`、`Step1RoomCamera.Shake` |
| 全局事件 | `Gameplay/Step1/Step1HakoniwaEvents.cs`（塌墙、警报） |
| 文字 / 按键 / 提示 / 图例 | `Step1Text.cs`、`Step1Keys.cs`、`Step1Hint.cs`、`Step1MapLegend.cs`、`Step1Gui.cs` |
| 机关 | `LevelElements/Pranks/*.cs`、`LevelElements/Traps/*.cs`（FireTrap、PranksterCannon、CannonBall…）、`LevelElements/Platforms/CollapsingPlatform.cs` |
| 机关基类 | `Ability/ControllablePropBase.cs`、`LevelElements/ControllableLevelElement.cs`、`LevelElements/LevelElementBase.cs` |
| 测试 | `Assets/Tests/EditMode/Step1RushMarioTests.cs`（helpers：`Read`、`CodeOnly`、`Tuning()`、`reg()`、`Dt`） |
| 关卡库 / 关卡包 | `LevelDesign/LevelPack.cs`、`LevelDesign/MiniJson.cs`、`Editor/LevelLibrary.cs`；关卡文件在 `Assets/Levels/Library/*.txt` |
| 网页关卡设计台 | 仓库根 `tools/LevelStudioWeb/`（build.py 生成 index.html；logic.js 纯逻辑；app.js 界面） |
| 文档 | `SESSION_TRACKER.md`、`docs/DESIGN_CONSTITUTION_v1.0.md`、`docs/ELEMENT_LEGEND.md`、`docs/step1/S1xx–S200_*.md` |

## 已实现玩法清单（避免重复造）
连招计数/顿帧/震屏/伤害递减（S185/S193）· 弹簧/裂缝地板/香蕉皮（S193–194）· 楼层寻路/监狱塔/自动镜头（S195）· 单向捷径门/裂墙/箱庭总览（S196）· 炸弹/缩小/通风管/时间静止/毒池黏胶/图例（S197）· 可破坏地形/炸弹伤双方/大炮瞄准与人肉发射/绳套/随机道具箱（S198）· 油桶连锁/铁笼/诱饵/警报/马里奥踢门（S199）· 连锁编排 F/一键布置/预判接力/挑衅 T/绊线 R/马里奥学习层/诱捕走廊（S200）· 完美连锁回放/炸弹策略加固（铆钉）/陷阱试探菜单/检查轨迹热力图/马里奥躲炸弹与抢道具（S202）· 马里奥性格 冲冲/谨慎/贪财（S203）· 网页关卡设计台 + 设计单工作流（S204）· 跳跃辅助/多格式导入（S205）· 关卡库/关卡包/搭建范围/提案状态（S206）。

## 待办候选（上次推荐）
可推动的油桶（推到路线上当一环）· 陷阱试探多策略对手（埋伏型/引诱型）· 更多性格（胆小型/记仇型）· 多座楼串联"地下一百层" · 马里奥性格（谨慎型/贪财型，宪法第 2 步）。

## 字符表（S200）
| 字符 | key | 中文 | 类别 | 第1步 |
|---|---|---|---|---|
| `#` | Ground | 地面 | Terrain | ✓ |
| `=` | Platform | 平台 | Terrain | ✓ |
| `W` | Wall | 墙 | Terrain | ✓ |
| `-` | OneWayPlatform | 单向台面 | Terrain | ✓ |
| `c` | Crate | 箱子 | Scenery | ✓ |
| `b` | Bush | 草丛 | Scenery | ✓ |
| `d` | Decor | 装饰 | Scenery | ✓ |
| `C` | CollapsingPlatform | 塌桥 | PlayerPrank | ✓ |
| `[` | ControllableBlocker | 封路墙 | PlayerPrank | ✓ |
| `~` | FireTrap | 火 | PlayerPrank | ✓ |
| `J` | SpringPad | 弹簧板 | PlayerPrank | ✓ |
| `x` | CrackFloor | 裂缝地板 | PlayerPrank | ✓ |
| `n` | BananaPeel | 香蕉皮 | PlayerPrank | ✓ |
| `\|` | OneWayDoor | 捷径门 | Special | ✓ |
| `%` | CrackedWall | 裂墙 | Special | ✓ |
| `O` | Vent | 通风管 | Special | ✓ |
| `w` | PoisonPool | 毒池 | Terrain | ✓ |
| `g` | Glue | 黏胶 | Terrain | ✓ |
| `i` | RoomLamp | 灯（S241） | PlayerPrank | ✓ |
| `v` | GrassGround | 草地（S241，实心，遁地不露土包） | Terrain | ✓ |
| `Y` | SnareTrap | 绳套 | PlayerPrank | ✓ |
| `?` | PickupSpot | 道具箱 | Special | ✓ |
| `U` | OilBarrel | 油桶 | Special | ✓ |
| `Q` | IronCage | 铁笼 | PlayerPrank | ✓ |
| `R` | Tripwire | 绊线 | Special | ✓ |
| `K` | Cannon | 大炮（朝右） | PlayerPrank | ✓ |
| `k` | Cannon | 大炮（朝左） | PlayerPrank | ✓ |
| `o` | Collectible | 宝物 | Objective | ✓ |
| `G` | GoalZone | 出口 | Objective | ✓ |
| `M` | MarioSpawn | 马里奥出生点 | Spawn | ✓ |
| `T` | TricksterSpawn | 捣蛋者出生点 | Spawn | ✓ |
| `.` | Air | 空气 | Terrain | ✓ |
| `空格` | Space | 空格 | Terrain | ✓ |

未用可选字符（S200）：A D N V Z u q a r z（选前再 grep 确认）

## S207 新文件
- `Assets/Scripts/Gameplay/Step1/Step1OffscreenMarkers.cs`：大房间屏外红箭头（马里奥/宝物/出口）。
- `Assets/Scripts/Gameplay/Step1/Step1MiniMap.cs`：大房间右上角小地图。
- 镜头逻辑在 `Step1RoomCamera.cs`（SmartFollow / SmartView / DeadZoneFollow）。

## S210 小镇大地图
- 纯逻辑 `Assets/Scripts/Overworld/`：OverworldCatalog / OverworldMap / OverworldWalker / OverworldPack（进 sim）；OverworldMind / OverworldSession（依赖 Unity 类型，不进 sim）。
- 运行时 `Overworld/Runtime/`：OverworldGame（小镇场景）、OverworldRoomLink（房间场景 → 回小镇）。
- 编辑器：OverworldBuilder（菜单 MarioTrickster/Overworld）、OverworldWorkshopWindow（Ctrl+Alt+O）。
- 钩子：Step1PrankRoomBuilder.RoomOverride、MarioMindDriver.SkipStartDelay、Step1PlaytestLog（session 时不弹问卷）、Step1Screen（第 2 个房间起不弹说明）、LevelPack 跳过 kind=overworld、LevelLibrary.ImportPack 也导入小镇、Step1Keys.Held/P/L/E/Return/Escape、Step1Text 改 partial（Step1Text.Overworld.cs）。
- 调参 dataVersion 16：overworld* 17 项。

## S211 场景切换
- `Overworld/SceneTransitPlan.cs`（纯逻辑，进 sim）+ `Overworld/Runtime/SceneTransit.cs`：所有小镇↔房间切换走 `SceneTransit.Go(场景路径, 标题)`，不要再直接 `SceneManager.LoadScene`（失败时才兜底）。
- 不要预加载（allowSceneActivation=false 会堵住后续异步加载）。
- `OverworldBuilder.IsStale/TownFingerprint`：新增会影响房间场景的东西（新主题字段等）要加进指纹。

## S212 转场 + 指引 + 工坊
- `SceneTransit.Go(场景, 标题, 世界坐标起点)` 圆从起点收拢；新场景里调 `SceneTransit.RevealAt(你的位置)` 在你身上展开。运行时每帧 `plan.Tick(plan.Step(unscaledDt), ...)`（卡顿帧封顶）。
- `Overworld/OverworldGuide.cs`（纯逻辑，进 sim）：EdgeArrow / RaceTo / MarioAt / Along；网页 `owMarioAt/owAlong` 逐行移植（verify 对照 ow_scrub.json）。
- `GameManager.RestartOverride`：优先于 EditorRestartHandler；OverworldRoomLink 用它平滑重开房间。以后别的"会话型"场景也用它，不要改 EditorRestartHandler。
- `LevelWorkshopWindow.OpenRoom(名字)`：从别处打开一个关卡库/样板房间。
- 小镇工坊草稿：SessionState `MarioTrickster.Overworld.WorkshopDraft`。

## S213 小镇规则 = 纯逻辑
- `Overworld/OverworldTown.cs`：小镇一帧的全部规则（输入 → 走路/伪装/香蕉皮/挑衅/门/马里奥/抓人/结算）。**改小镇玩法改这里**，OverworldGame 只读键盘、画画面、切场景。提示用 `OverworldTown.Note` 枚举，文字在 OverworldGame.NoteText。
- `Overworld/OverworldBots.cs`：玩家视角模拟（7 种机器人）。新机制加进小镇后，给合适的机器人加一种用法，并在 sim S213 里加一条期望。
- 调参 dataVersion 17：overworldExitGraceSeconds 2.5、overworldAmbushSteps 8。

## S214 同步与切换
- `Editor/WebSync.cs`：Inbox 自动导入 + `Imported` 事件 + 覆盖前备份 + 网页试玩请求。以后新增"会写关卡库/小镇文件"的地方，写之前调 `WebSync.BackupBeforeWrite`。
- 关卡工坊 `StepLibrary / QuickSave / SaveAs`（保留 Pending：`LevelWorkshopModel.CarryPending`）；小镇工坊 `StepTown / RunBots`。

## S215 全局总览
- `Overworld/CampaignLedger.cs`：按门顺序汇总房间（主角机关 / 第一次出现 / 道具 / 提醒）。小镇工坊侧栏 LedgerPanel、网页 `owLedger`/`owLedgerLines`（overworld.js 末尾）+ `owLedgerRender`（app.js）。改规则两边一起改，verify 逐字对照（ow_ledger.json）。
- 炸弹预算：小镇房间按 `bombsPerRound + OverworldTown.MaxBonusBombs` 加固（`Step1PrankRoomBuilder.ExtraBombs`，OverworldBuilder 建房前设、finally 归零）。改 MaxBonusBombs 要升 BuilderVersion。
- S242：黑匣子纯逻辑 `Gameplay/Step1/Step1BlackBox.cs` + 录音机 `Step1BlackBoxRecorder`（Step1Feedback.Awake 挂）；反馈 `Step1Feedback`（JPG、blackbox_NNN.md、events.tsv、F8 后 1–4 标签 `TagOpen`）；打包 `Editor/TestHubWindow.Pack`（预算 / 总结 / Unity 日志错误 / Deflate）。装备栏纯逻辑 `Step1Loadout.cs` + `TricksterLoadout`（Step1Lighting 挂）；`DisguiseSystem.SetDisguises/Select/ShapeChangedAt`、`DisguiseData.tint/sourceChar`；道具诱饵 `Decoy.InitProp/IsProp/Wriggling`。一目了然纯逻辑 `Step1Glance.cs` + `Step1GlanceView`（Step1Combo 挂）；图例 `Step1MapLegend.DrawGrid`。三个纯逻辑文件都在 sim。读反馈包 `scripts/tools/read_feedback_pack.py`。
- S243：像素美术数据 `Gameplay/Step1/Step1Art.cs`（Icons 30 / Frames Hero0–4 Imp0–4 / Tiles 6 / Background 64×36）+ 运行时 `Step1ArtSkin.cs`（生成器挂 GameManager）；调参 artCharacters/artProps/artTiles/artBackground（组：镜头 · 屏幕 · 好不好读）；Resources/Step1Art/<Key>.png 覆盖；生成脚本 `scripts/tools/art_s243/`；文档 S243_ART_SKIN.md + S243_像素素材总览.png。
- S241：光影/遁地/蛛丝纯逻辑 `Gameplay/Step1/Step1Stealth.cs`（sim 也编译它）；画面 + 判亮暗 `Step1Lighting`（`IsLit/Visible/Raining`）；`TricksterBurrow`（U，`BodyBusy`）/`TricksterSilk`（K）/`TricksterFootsteps`（夜里脚步，事件只带位置）由 Step1Lighting 运行时挂到捣蛋者上（Step1Combo 不许碰捣蛋者，H4 测试）；机关预约 / 退还 `TricksterAbilitySystem.ArmProp/HandleMissRefunded` + `ControllablePropBase.ArmOnPress/RefundOnMiss/MissRefund`；图标 `Step1Icons`（tools 生成）+ `Step1PropIcons`；图例 `Step1MapLegend.ForRoom/GroupOf/TagNear`。
- S240：坐进大炮 `PranksterCannon.Seat*`；用过的机关 `ControllablePropBase.SpentThisRound`；连击原因 `Step1ComboFeel.CauseName`；卡住 `Step1StuckRescue.Tick` + `step1_stuck.txt`；临界跳 `LevelRouteFollower.CriticalJumpCells`。
- S239：重复旧系统（热度/警报导演/连锁追踪/路线预算/补偿/锚点起疑层/旧 UGUI）已删；视线检测在 `Gameplay/SightLine.cs`。
- 旧工具菜单 S238 已全删（顶层 10 个）；特效工厂 / 特效快速套用 / 溶解噪声在 `美术 Art/`；S237 起测试报告在 `检查与记录 Checks/测试报告 …`、美术工具在 `美术 Art/工具 Pipeline/…`（sim S237 查 ExecuteMenuItem 调了不存在的菜单）。窗口之间跳转直接调 `XxxWindow.Open()`，不要用菜单字符串。

## S216 手感
- `Gameplay/Step1/Step1Feel.cs`：所有手感曲线纯函数（StunStep 硬直期物理、Simulate 弹飞轨迹、StunOver 落地才恢复、TelegraphRate/Shake、SpringPadScaleY、Ring、HurtTint、ShakeOffset 平滑噪声）+ `LaunchFeel` 运行时参数（Step1Combo.Start 从调参写入）。
- `Gameplay/Step1/Step1Fx.cs`：冲击环/颗粒/尘土/连锁火花线，纯画面，不许加碰撞体（H4）；同屏上限 MaxAlive。
- 让人"飞起来"的新机关：设速度后调 `mario.ApplyKnockbackStun(秒, untilLanded: true, slide: false)`；别再写"硬直期没重力"的代码。sim S216 会检查弹高 < 头顶空格。
- 震屏用 `Step1RoomCamera.Current.Shake(幅度, 秒)`，不要 FindObjectOfType。

## S217 大世界 + 测试流程
- `OverworldMap.Resize(m,l,r,t,b)` ↔ 网页 `owResize`（逐字一致，verify 对照 ow_resize.json）；MaxW/MaxH 192×128。改扩展规则两边一起改。
- `Gameplay/Step1/Step1QuickTest.cs`（PlayerPrefs 开关：不弹说明/问卷）、`Step1Feedback.cs`（F8 截图 + 自动记错误 → PlaytestLogs/Feedback/；`Step1Feedback.Context` 由场景填"此刻情况"）。
- `Editor/TestHubWindow.cs`（Ctrl+Alt+T：一键体检 HealthCheck.md / 快速测试 / 试玩 / 打包反馈；`TinyZip` 不依赖 System.IO.Compression）、`Editor/PlayFocus.cs`（进 Play 切 Game 窗口）。
- `Step1Keys.AnyDown()`；新键：R N Y Space 方向键 A D W S F5 F8 F9 Minus Equals。**以后任何"按任意键 / 结算键"都用 Step1Keys**，不要写 Input.anyKeyDown / Input.GetKeyDown。
- SceneTransit 20 秒保险丝（WatchdogSeconds）。小镇开场 timeScale=1、计时用 unscaledDeltaTime。

## S218 小镇大机关
- 文件：`Overworld/OverworldProps.cs`（OverworldProps + OverworldEvents）、`OverworldTown.cs`（Big / Flight / Arm / Fire / Impact / DodgeCell / Reload）、`OverworldSession.cs`（Day、Changed、MarioWary、CarriedDaze、ReloadDoor、BigHits、BestChain）、`OverworldPack.BigSampleText`。
- 调参 v19：overworldBigFuseSeconds 1.2、overworldBigStunSeconds 2、overworldRollSpeed 9、overworldNoiseRange 14、overworldNoiseSuspicion 35、overworldDazeCarryMinutes 40、overworldDazeCarrySeconds 2、overworldFogSight 0.6、overworldDodgeSteps 4。
- 常量：Muzzle 3、MaxShot 96、WindShift 3、FloodRadius 3、MaxRoll 64、MaxBig 12、ChainRadius 1.5、FlightSeconds 0.9。
- sim 块 "S218 小镇大机关"；verify 生成 ow_props.json（网页 owCheck + owPropsDescribe + owDayOf + owWeatherPreview）。
