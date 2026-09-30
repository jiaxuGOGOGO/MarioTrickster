# 分册：修 bug / 用户反馈

## 流程
1. 用户截图 → `gsk understand_images -i <url> -r "描述画面、报错文字、红色元素、Console 内容"`。Unity 报错看 Console 第一条红字（文件名:行号）。
2. 复述"为什么坏了"（大白话）→ 找到根因再改，别打补丁绕过。
3. 能写成纯逻辑的，先写一个会失败的测试，再修。
4. 汇报里写：**为什么坏 / 怎么修 / 你怎么确认修好了**。

## 历史坑（再出现直接照着修）
| 症状 | 根因 | 修法 |
|---|---|---|
| 按 B/G/F 没反应 | 某些机器旧 Input 读不到 | 用 `Step1Keys.Down`（两套输入都读） |
| 工坊 Play 后空白 / NullReference | 缓存字段被 Unity 序列化 | 缓存字段加 `[NonSerialized]`，用前判空重建 |
| 马里奥在楼层洞口下来回跳 | 旧 AI 头顶目标 → 徘徊 | 往上的路点走 `AuthoredRouteTarget`（`UseAuthoredSteering`） |
| 马里奥卡住但"在动" | 卡住判定用的是"动没动" | 改为"有没有朝目标前进"（`SecondsUntilStuck`） |
| 捣蛋者贴墙粘住 | 贴墙时仍有水平输入 | `HitsWall` 时清水平速度 |
| 塌桥只塌一格 | 每格独立 | `collapseWholeSpan` 整段一起塌 |
| 测试 H4 误报 | 检查范围过大 | 只检查"听"的入口段落（NoteNoise…Forget 之间） |
| 马里奥在台边/楼梯口左右抖、拿不到正下方的宝物 | 转向"对准 0.3 格就停"，身体一半还在台上（S209） | 用 `LevelPathPlanner.SteerX`（目标在下方继续走下台边）；`LevelRouteFollower` 走一遍复现 |
| 卡住救援后又回到原处循环 | 救援放"最近安全格" | `RescueAlongRoute` 沿路线往前放（S209） |
| 捣蛋者/马里奥空中粘墙或悬空 | 贴墙判定在方向键之前；伪装变大长进墙 | 判定放 HandleDirection 之后；`FeetAlignedOffsetY`；`BodyUnstick.Resolve`（S209） |
| 编辑器编译报 MonoBehaviour 找不到（沙盒） | nupkg 解压文件无读权限 | `chmod -R u+rwX unityref`（setup 已处理） |
| dotnet 报 version 错（沙盒） | 环境变量 `version=N/A` | `unset version`（脚本已处理） |
| 编辑器 cc2 找不到运行时类型 | 先要编好 `cc/out/cc.dll` | verify.sh 的顺序就是先 cc 后 cc2 |

## 性能问题
工坊卡：检查放到停笔后（`EditorApplication.update` 延迟 0.35s）、按物理签名去重、只在格子变化时重绘。游戏卡：去掉 Update 里的 Find、GUIStyle 缓存、NonAlloc 物理查询。

## S209 教训
死局检查（理论可达）≠ AI 真的会走过去。改 AI 移动/转向后，除了 verify 的死局检查，还要看 "S209 按马里奥走法走一遍" 那一行；用户报"徘徊/卡住"先用 `LevelRouteFollower.Run(grid)` 和 `Run(grid, true)`（旧规则）对照复现。

## S215–S216 教训
| 现象 | 原因 | 修法 |
|---|---|---|
| 被弹簧/炸弹/大炮弄飞"像在月球"、撞天花板、飞太远 | 硬直期只在往下掉时加重力 | `Step1Feel.StunStep` 全程重力（LaunchFeel.gravity）；新机关用 `ApplyKnockbackStun(秒, true, false)` |
| 用户说"生硬、突兀" | 瞬移、每帧随机抖、没有冲击点 | 缓动（DropProgress/SmoothStep01）、平滑噪声（TelegraphShake/ShakeOffset）、Step1Fx 冲击环 |
| 小镇里房间被炸弹困死但单房间检查通过 | 加固只按本回合 3 颗，小镇还能带进 3 颗 | `Step1PrankRoomBuilder.ExtraBombs = OverworldTown.MaxBonusBombs`（S215） |
| 升版本后一堆旧测试红 | 测试写死 `AreEqual(N, 版本号)` | 一律 `GreaterOrEqual` |
| 改了 sim/Check.cs 但 verify 没跑新检查 | 以前只在 setup 时复制 | verify.sh 第 4 步开头会复制（S216 已修） |
