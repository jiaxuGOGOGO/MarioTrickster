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
