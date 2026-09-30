# S215 全局把握：一天总览 + 炸弹预算修复 + 菜单减法

## 用户问题
"因为涉及到关卡切换 以及整体资源调配道具机关 目前项目对全局把握或者方便设计的全局功能是否足够是否需要增加或者做减法"

## 审计（先查再改）
- 全局看的地方已有：小镇工坊（画门/时间表/时间拖条/🤖模拟一天）、网页大地图、关卡工坊（单个房间的检查/加固）。
- **缺的是"按马里奥顺序看一整天"**：每扇门的房间主打什么机关、哪扇门第一次出现新机关、有没有两扇门重复、一扇门一次教太多——这些要一个个房间点开才知道。
- **真 bug**：小镇里一个房间最多有 3（本回合）+ 3（小镇带进来的 MaxBonusBombs）= 6 颗炸弹，但加固只按 3 颗算。探针：地下监狱·四层 按 3 颗加固后，6 颗能把马里奥困死（(2,5)(15,5)(18,5)(36,5)(42,9)）。另外 3 个样板房间安全。
- 菜单：MarioTrickster 下 42 个菜单项、15 个窗口；"小镇工坊"有两个菜单入口；小镇工坊里的"🏠 关卡工坊"按钮靠菜单字符串调用（菜单改名就失效）。

## 做了（加）
- `Overworld/CampaignLedger.cs`（纯逻辑，游戏 / sim / 网页同一套）：按门的顺序列每个房间的机关种类、主角（数量最多的机关）、第一次出现的机关、道具数；提醒：找不到房间 / 房间没机关 / 一扇门第一次出现 ≥3 种 / 连续两扇门主角一样。
- 小镇工坊侧栏"📋 一天总览"（每行 ✎ 打开房间）；网页大地图同名卡片（`owLedger`），两边逐字一致（verify 对照）。
- 炸弹预算：`Step1PrankRoomBuilder.ExtraBombs`，小镇建房间时 = MaxBonusBombs，加固按 3+3 算；BuilderVersion 19（房间场景会自动重建）。

## 做了（减）
- 旧测试/验证/AI Arena/Level Builder/Planner/Docs Automator/UI Canvas 重建等 14 项移进 `MarioTrickster/旧工具 (Legacy)/`（只搬不删；Run Tests/、Art Pipeline/ 保留原路径，测试控制台靠它们）。
- 删掉重复的 `MarioTrickster/Overworld/Town Workshop` 入口；"🏠 关卡工坊"按钮改为直接 `LevelWorkshopWindow.Open()`。

## 刻意没做
- 没做新的"总控台"窗口（再加一个窗口就是在做加法；总览放进已有的小镇工坊/网页大地图）。
- 没改样板小镇内容：第 1 扇门一次出现 8 种、门 3 和门 4 主角都是塌桥——作为提醒显示给制作人，由你决定（参考 GMTK《Super Mario 3D World's 4 Step Level Design》https://www.youtube.com/watch?v=dBmIkEvEBtA ：一关先教一个点子再变化）。
- 路线秒数 C# 与网页算法不同，只显示不对照。

## 验证
- sim：C# CampaignLedger.Lines 与网页 owLedgerLines 在样板小镇逐字一致；样板该出的 2 条提醒都出；verify ALL GREEN（173 项）。
- EditMode（需在 Unity 跑）：Ledger_FlagsTeachingOverloadAndRepeats、Wiring_S215_BombBudgetAndMenus。
- 未在 Unity 验证：菜单实际位置、6 颗加固的实际重建（每个房间约 0.2–8 秒）。
