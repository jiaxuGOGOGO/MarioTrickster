# 分册：加新机关 / 新元素（ASCII 字符）

## 清单（全部做完才算完成）
1. **挑字符**：先查已用字符（`project-map.md` 字符表）。可用的：`A D N V Z u q a r z`（S200 时）。确认：`grep -n "asciiChar = 'X'" Assets/Scripts/LevelDesign/AsciiElementRegistry.cs` 为空。
2. **组件脚本** `Assets/Scripts/LevelElements/Pranks/<Name>.cs`：
   - 玩家按 L 触发的 → 继承 `ControllableLevelElement`，实现 `OnTelegraphStart/OnTelegraphEnd/OnActivate/OnActiveEnd`；预警由基类管（H3）。一次性的用 `ExtraControlCondition()` 返回 `!used`。
   - 被动/环境的 → 继承 `LevelElementBase`，在 `Awake` 里设 `elementName/category/tags/description`。
   - 必须实现 `OnLevelReset()`（回合复原）。
   - 需要全局列表时：静态 `List` + `OnEnable/OnDisable` 增删（参考 `OilBarrel.All`、`OneWayDoor.AllDoors`）。
   - 事件用 `public static event`，只带位置或 MarioController，**不带捣蛋者信息**（H4）。
   - 判断逻辑写成 `public static` 纯函数供测试。
   - 文件头注释写：规则、H 条款怎么满足、参考来源。
3. **Registry**：`AsciiElementRegistry.cs` 的内置列表末尾（`};` 之前）加 `AsciiElementEntry`（`elementName` = 主题 key，`componentTypeNames` = 组件类名，isSolid/isTrigger/尺寸/颜色）；`BUILTIN_ENTRY_COUNT`+1 并在注释写 `SXXX: +X`。
4. **说明书** `ElementCatalog.cs`：`I('X', "Key", "中文名", "English", Role.xxx, "是什么（大白话）", "怎么放", needsSupport:…, step1: true)`。Role：玩家按 L 的用 `PlayerPrank`（会进连招路线），被动特殊物用 `Special`。
5. **主题槽位** `LevelThemeProfile.cs`：`new ElementSpriteMapping { elementKey = "Key" },`。
6. **探索计划** `Editor/MechanismExplorationPlan.cs`：`NotProbed` 字符串末尾加字符 + 注释一行。
7. **摆放规则**（需要时）`ElementCatalog.PlacementIssues`：头顶空间、炮口、宽度上限等，报错文字带 `(x,y)` 坐标。
8. **死局/可达**：实心与否决定可达性按最坏情况算；会"打开路"的（塌/炸/门）放进 `LevelDeadlockAnalyzer.PersistentOpeners` 视情况。
9. **构建器** `Editor/Step1PrankRoomBuilder.cs`：在配置区 `foreach (var x in root.GetComponentsInChildren<X>(true)) { x.Configure(tuning.…); EditorUtility.SetDirty(x); count++; }`；`BuilderVersion`+1。
10. **数值** → `MarioMindTuningSO`（Header + Tooltip 中文 + `dataVersion` 块）。
11. **文字** → `Step1Text`；**图例** → `Step1MapLegend.Entries`（+ 需要时动态标签）；**帮助** → `Step1Text.Help`。
12. **连招**：会"坑到"马里奥的，在 `Step1Combo.Start` 订阅事件 `Register("kind")`（记得 OnDestroy 退订）。需要进工坊连招路线但不是 PlayerPrank 的 → `ComboRouteAnalyzer.IsChainPart`。
13. **与爆炸互动**：`TricksterBomb.Blast` 里加一行 `GetComponentInParent<X>()` 分支（炸弹、油桶共用规则）。
14. **样板**：至少放进一个样板（`LevelWorkshopModel.HakoniwaSample` 等），跑 verify.sh 确认仍可玩。
15. **测试** 1 个：纯逻辑 + 接线 StringAssert + `MissingFromCatalog` 为空 + 样板 Playable。
16. **文档**：`docs/ELEMENT_LEGEND.md` 在 `| \`K\` |` 前插一行（列：字符 | 中英名 | 是什么 | 怎么放 | 路/视线 | 主题 key | 贴图适配 | 建议像素 | 第1步）。

## 常见坑
- 触发器要在 `Awake` 里 `GetComponent<BoxCollider2D>().isTrigger = true`。
- 视觉子物体叫 `Visual`（视碰分离架构 S37），用 `transform.Find("Visual")`。
- 碰撞体不能大于视觉（H3）。
- 你自己（捣蛋者）是否也会中招要明确（公平原则：多数陷阱双方都会中）。

## S216 手感清单（新机关必做）
- 发动瞬间要有冲击画面（Step1Fx.Ring/Burst/Dust），范围型效果的冲击环 = 真实范围（H3 可读）。
- 会把人弄飞的：用 ApplyKnockbackStun(秒, true, false)，在 sim S216 的 cases 里加一行（高度要 < 房间头顶空格）。
- 状态变化不要瞬移（落下/吊起用 Step1Feel.DropProgress / SmoothStep01 缓动 0.1–0.3 秒）。
