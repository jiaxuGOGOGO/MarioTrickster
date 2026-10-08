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

## S235 "掉出房间回不来、血不掉、马里奥照跑"
| 现象 | 根因 | 修法 |
|---|---|---|
| 捣蛋者出界后停在外面 | KillZone 只认 PlayerHealth（捣蛋者没有）且只管 y；BodyUnstick 最短方向可能往外推 | `Step1RoomGuard`（看不见的墙 + `TricksterLives.FellOut`）；`BodyUnstick.RoomSize` + `Step1Bounds.ClampInside` |
| 回出生点后又被拽回绳套/铁笼/炮口 | 控制机关每帧锁位置 | `Step1Bounds.Teleported(...)` → 放人 |
| 小镇里动不了 | 身体压进不可走格 → Move 每步被挡 | `OverworldTown.Unstick` / `OverworldMap.Unstick` |

## S236 交互 / 测试别扭
| 现象 | 根因 | 修法 |
|---|---|---|
| 问卷按数字没反应 | 只读旧 Input | `Step1Keys.Digit1to5()`（新键都要两套输入） |
| F9 按了像没反应 | 只清旧系统冷却 | `Step1QuickTest.NoLimits` 接到 TricksterKit/Decoy/Taunt |
| 自爆输了显示"马里奥逃走了" | Classify 没有这一类 | `Outcome.TricksterSelfHit` |
| 停时间时你沉进地板 | isKinematic 时硬直分支仍写速度 | 冻住时零速度返回 |

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

## S217 教训（用户："运行时画面固定不动、控制不了"）
| 现象 | 原因 | 修法 |
|---|---|---|
| 小镇进去画面不动 | 说明面板只有 H 能关，开着时整个小镇停 | 面板任意键关（Step1Keys.AnyDown）+ 快速测试模式不弹 |
| 按键全没反应 | 键盘焦点在工坊窗口 | PlayFocus 进 Play 切 Game 窗口；游戏里 `!Application.isFocused` 提示点画面 |
| "按任意键开始" / R / N / Esc 没反应 | 只读旧 Input | 一律 Step1Keys（KeyboardInputProvider 全局键也改了） |
| 早上马里奥不动像卡了 | 08:00 才出门，没提示 | 底部"几点出门、还有几秒、按住空格快进" |
| 用户测试被问卷/弹窗打断、反馈丢 | 每局 5 道问卷 | 测试中心：快速测试模式 + F8 反馈 + 打包 zip；用户发来反馈包 → 先读 00_给AI的话.md、HealthCheck.md、feedback.md（错误行有位置），再看截图（gsk understand_images） |

## S220：编辑器窗口卡顿（IMGUI）
- 症状："下拉菜单卡""拖半天没反应，然后突然拖下来"。原因：OnGUI 每个事件（Layout + Repaint + 鼠标移动）都会执行；里面只要有读盘 / 全图检查 / 验房间变体，就会卡。
- 规则：OnGUI 里只读缓存。改地图后调用 `Touch()`（版本 +1，0.12 秒后空闲再 Recheck）；会改变布局的状态只在 MouseUp / EditorApplication.update / delayCall 里改。
- 文件列表用 `LevelLibrary.Signature`（文件数 + 最新修改时间）缓存；写文件后调用 `Invalidate` / `OverworldBuilder.InvalidateCaches()`。
- 网页同理：mousemove 只做 `owDraw()`（贴离屏底图 + 悬停框），mouseup 才 `owRender()`；改了地图要 `OWT.ver++`。

## S221：受伤保护期规则
- 无敌 / 保护期必须**从站起来那一刻**开始算（`GraceAfter(stun) = stun + overworldHurtGraceSeconds`），而且保护期里**也不再晕**。否则晕 3 秒 > 无敌 2.5 秒，站起来就又被击倒（连控）。
- 新的伤害源都走 HitMario / HurtYou，不要自己写扣心或定身。

## S225 "按了没反应"
- 第 1 步房间关掉了旧界面（GlobalGameUICanvas/GameUI），它们原来接收 `TricksterController.OnAbilityFailed` / `DisguiseSystem.OnDisguiseFailed`。现在由 `Step1FailFeedback` 接收并 `Step1Hint.Show(Step1Text.AbilityFailZh(...))`。
- 用户说"按 X 没反应"：先看该键的失败分支有没有 Hint；新失败原因必须在 `AbilityFailZh` 加一行（sim 会从 GetAbilityFailReason 源码抽取逐条检查）。

## S227 反馈包里的 step1_rounds*.csv
- 用 `Step1ExitReport.ParseAll` 读（按列位置，新旧表头都行）；把文件放进 `repo/docs/step1/data/` 后 sim 会跑出口报告。`tuning_version` 为 0 = S227 以前的旧版本局 → 只作参考，不据此调数值。`stuck_rescues` 很大 + 150 秒超时 = 卡住类 bug，先要 F8 截图定位。
