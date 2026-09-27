# 分册：项目地图（文件在哪 / 关键数值 / 字符表）

## 状态（S200 交付时）
- Registry 内置条目 44（`BUILTIN_ENTRY_COUNT`）；`BuilderVersion` 17；`MarioMindTuningSO.CurrentDataVersion` 13（S202）；EditMode 测试约 385 个（Step1RushMarioTests 100 个）。
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
| 探索计划（新字符登记 NotProbed） | `Editor/MechanismExplorationPlan.cs` |
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
| 文档 | `SESSION_TRACKER.md`、`docs/DESIGN_CONSTITUTION_v1.0.md`、`docs/ELEMENT_LEGEND.md`、`docs/step1/S1xx–S200_*.md` |

## 已实现玩法清单（避免重复造）
连招计数/顿帧/震屏/伤害递减（S185/S193）· 弹簧/裂缝地板/香蕉皮（S193–194）· 楼层寻路/监狱塔/自动镜头（S195）· 单向捷径门/裂墙/箱庭总览（S196）· 炸弹/缩小/通风管/时间静止/毒池黏胶/图例（S197）· 可破坏地形/炸弹伤双方/大炮瞄准与人肉发射/绳套/随机道具箱（S198）· 油桶连锁/铁笼/诱饵/警报/马里奥踢门（S199）· 连锁编排 F/一键布置/预判接力/挑衅 T/绊线 R/马里奥学习层/诱捕走廊（S200）· 完美连锁回放/炸弹策略加固（铆钉）/陷阱试探菜单/检查轨迹热力图/马里奥躲炸弹与抢道具（S202）。

## 待办候选（上次推荐）
可推动的油桶（推到路线上当一环）· 陷阱试探多策略对手（埋伏型/引诱型）· 多座楼串联"地下一百层" · 马里奥性格（谨慎型/贪财型，宪法第 2 步）。

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
