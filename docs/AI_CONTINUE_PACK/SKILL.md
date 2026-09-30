---
name: mariotrickster-continue
description: 继续开发 Unity 游戏 MarioTrickster（第 1 步恶作剧房间 / 关卡工坊 / 捣蛋者 vs 冲冲型马里奥 AI）时必用。用户提到 MarioTrickster、马里奥捣蛋、恶作剧房间、关卡工坊、箱庭、连锁陷阱、马里奥 AI、S2xx 补丁、大地图/小镇/星露谷视角、一天总览、网页设计台同步、机关手感/弹飞/受伤/特效/震屏、apply_Sxxx.bat、"继续"上次的游戏开发，或上传该项目的截图/报错时加载。内含设计宪法硬规则、质量红线、沙盒无 Unity 验证环境一键搭建、补丁交付流程，并按功能（新机关/AI/关卡/技能/修 bug/调研）分册给出做法。
---

# MarioTrickster 接续包（S200 起）

这是一个**用户不会写代码**的独立游戏项目。你是他的唯一程序 + 关卡助手。质量靠下面的规则守住，不靠记忆。

## 0. 开工三步（每个新对话都做，不用问用户）

1. **搭环境**（约 1–2 分钟，可重复运行）：
   ```bash
   SK=$(dirname "$(ls -d ~/.opencode/skills/*/SKILL.md 2>/dev/null | xargs grep -l "name: mariotrickster-continue" | head -1)")
   bash "$SK/scripts/setup_sandbox.sh"      # 之后：bash "$SK/scripts/verify.sh"、bash "$SK/scripts/make_patch.sh SXXX"
   ```
   产物：`/home/user/workspace/{repo,unityref,cc,cc2/full,sim}`。找不到技能目录时：仓库里 `docs/AI_CONTINUE_PACK/` 有同一套文件（双保险）。
2. **读现状**：`repo/SESSION_TRACKER.md` 顶部"最新 Session"行 + 最后一个 `### [SXXX] 用户：` 条目；`git -C repo log --oneline -5`。
   - GitHub 分支 `genspark_ai_developer` 可能**落后**于上次交付（用户还没双击 bat 上传）。若 tracker 写的最新 Session 在 git log 里找不到 → 让用户先跑上次的 `apply_SXXX.bat` 并选 Y 上传，**或**把用户重新上传的 zip 里的 `.patch` 用 `git am` 打到本地再继续。
3. **按任务类型读分册**（只读需要的）：

| 用户想要 | 读 |
|---|---|
| 加新机关/元素（ASCII 字符、陷阱、场景物） | `references/new-element.md` |
| 改马里奥 AI（感知、起疑、追逐、学习、卡住） | `references/mario-ai.md` |
| 捣蛋者技能/按键（炸弹、诱饵、连锁、挑衅…） | `references/trickster-skill.md` |
| 关卡/样板/工坊/箱庭/监狱塔/小镇大地图（S210 星露谷视角）/小镇大机关·连锁·天气（S218）/巨炮瞄准·山地·雷雨泥石流（S219） | `references/level-design.md` |
| 修 bug / 用户截图报错 / "没反应" / 用户发来反馈包 zip | `references/bugfix.md` |
| 调研玩法再升级 | `references/research.md` |
| 文件在哪、关键数值、字符表 | `references/project-map.md` |
| 用户发来"设计单"/网页设计台 / 改网页 | `references/web-studio.md` |
| 机关/受伤/弹飞的手感与画面（"生硬、不自然"、特效、震屏） | `references/new-element.md` 末尾"S216 手感清单" + `references/project-map.md` "S216 手感" |
| 全局总览/一天节奏/菜单整理 | `references/level-design.md` + `references/project-map.md` "S215" |
| 打包交付给用户 | `references/delivery.md` |

## 0.5 换账号 / 全新对话也能接上（不依赖任何账号记忆）

- 本包**自给自足**：不需要旧账号的历史对话、记忆或 AI Drive。仓库是公开的（`https://github.com/jiaxuGOGOGO/MarioTrickster`），任何账号都能克隆。
- 用户如果**没有把技能加进新账号**，而是直接把 `mariotrickster-continue.skill`（zip）发进对话：`gsk download` 下来 → `unzip` 到 `~/.opencode/skills/` 或工作区 → 读里面的 `SKILL.md`，照做即可。
- `scripts/pending/*.patch` = 打包时**还没上传到 GitHub** 的改动。`setup_sandbox.sh` 会自动检查：GitHub 上缺哪个就补哪个（已有的跳过）。所以哪怕用户换了账号、忘了跑 bat，进度也不会丢。补完后在汇报里提醒用户"上次的补丁还没上传，这次的 bat 会一起带上"。
- 上传 GitHub 是用户**自己电脑上的 git 账号**在做（bat 里选 Y），和 Genspark 账号无关。如果用户换了 GitHub 账号、推不上去：让他在仓库设置里把新账号加为协作者，或 fork 后告诉你新地址（改 `setup_sandbox.sh` 与 `make_patch.sh` 里的仓库地址）。
- bat 找不到项目文件夹时会让用户**把 MarioTrickster 文件夹拖进窗口**，换电脑/换路径也能用。
- **每次交付后**都要刷新本包：`scripts/pending/` 换成最新未上传补丁（`make_patch.sh` 的输出里那些 .patch），并重新打 `.skill` 交给用户，让用户"随时都拿着最新的接续包"。

## 0.6 进度快照（打包时写入，以 SESSION_TRACKER 为准）

- 最新交付：**S219**（巨炮能坐能瞄——你和马里奥都能坐、落点先画出来、你能拨歪他的炮；山丘 ^ / 山 A / 山洞 h；天气池按格局：雷雨召唤闪电、酸雨枯草；湿天冲击 → 泥石流、水塔 → 山洪；样板"星露山镇"）。上一版 S218：小镇大机关 + 连锁 + 天气 + 小镇↔房间联动。`scripts/pending/` 里是还没上传到 GitHub 的补丁（setup 会自动补上；已上传的自动跳过）。
- 数值版本 `MarioMindTuningSO.CurrentDataVersion = 20`；房间构建器 `Step1PrankRoomBuilder.BuilderVersion = 20`。
- 近几次做了什么（详情看 `repo/docs/step1/S21x_*.md`）：
  - S210 星露谷视角小镇大地图（门 = 恶作剧房间）；S211 场景转场；S212 圆形转场 + 指引箭头 + 编辑器快捷键；
  - S213 小镇规则纯逻辑化 + 7 种机器人玩家模拟；S214 网页 ↔ Unity 自动同步（Inbox 文件夹、▶ 在 Unity 试玩、PageUp/PageDown 切关）；
  - S215 一天总览（CampaignLedger，Unity 小镇工坊 + 网页大地图逐字一致）、小镇房间按 3+3 颗炸弹加固、14 个旧菜单收进"旧工具 (Legacy)"；
  - S217 小镇"↔ 扩展"（C# Resize = 网页 owResize）、上限 192×128；说明面板任意键关、进 Play 自动切 Game 窗口、全局键两套输入、转场保险丝、等出门提示；测试中心（一键体检 / 快速测试模式 / F8 反馈 / 打包 zip）；
  - S219 巨炮瞄准 / 马里奥坐炮 / 山地视线 / 山洞 / 雷雨闪电 / 酸雨 / 泥石流（OverworldProps 瞄准+山地函数；OverworldTown Seat/Ride/TryTamper/TryCave/FireLightning/FireMud；Events.Pool(map)）；
  - S218 小镇大机关（OverworldProps + OverworldEvents；OverworldTown Arm/Fire/Impact；马里奥听见起疑、吃过亏会躲；砸晕带进房间、守住一户重新装填；天气第 1 天晴；网页逐字对照）；
  - S216 修复"硬直期往上飞没有重力"（Step1Feel.StunStep，全程重力 40、落地才恢复），受伤红白闪 + 小跳、Step1Fx 冲击环/尘土/连锁火花线、预警越来越急、平滑震屏。
- 交付包固定结构（`out/D<N>/`）：`00_先看我_怎么用.md`、`01_安装到项目`（全部补丁 + `install_and_upload.bat`）、`02_接续包_换账号用`（本 .skill）、`03_说明文档`（S21x_*.md）、`04_关卡设计台网页/MarioTrickster关卡设计台.html`。
- 上次汇报推荐的下一步（用户说"继续"时做）：① 用户试玩星露山镇后发反馈包 → 调瞄准 / 马里奥瞄准窗口 / 泥石流长度；② 房间里的大炮也改成"坐进去瞄准、落点先画出来"（手感统一）；③ 山丘瞭望（站上去按住 Tab 看他整条路线，代价被看见）。
- 用户要求（S217 起长期有效）：**每次升级都按本技能做安装包**，装到 `E:\BaiduNetdiskDownload\MarioTricksterGensparkAI\MarioTrickster`，bat 里选 Y 自动推到 GitHub；**测试流程不能有反复操作的阻碍**（新功能默认不弹窗、有快速测试模式、反馈走 F8 + 测试中心打包）。

## 1. 设计宪法硬规则（违反 = 不许交付）

权威原文：`repo/docs/DESIGN_CONSTITUTION_v1.0.md`。每次改动都要自问这 10 条：

| # | 规则 | 落地做法 |
|---|---|---|
| H1 | 马里奥从任何可达状态都有拿宝+撤离的路线 | 新元素在死局检查里按**最坏情况**算（会塌的按塌、会挡的按挡、会炸开的按不炸）；样板必须 `Check().Playable` |
| H2 | 识破前必有可见预兆（? → !） | 只能往 `SuspicionMeter` 加值，不能直接切 Chasing |
| H3 | 陷阱有预警；判定只在激活期；判定框 ≤ 精灵 | 走 `ControllablePropBase` 的 Telegraph；自动触发也要调 `OnTricksterActivate`（不跳预警） |
| H4 | **AI 不作弊**：马里奥只能看（视锥+距离+遮挡）、听（只有位置）、记（自己的经历） | 马里奥侧代码（RushMarioMind/MarioMindDriver/SuspicionMeter/MarioVision/MarioEyes）禁止出现：`TricksterPossessionGate CurrentAnchor IsHiddenAndArmed IsFullyBlended DisguiseSystem TricksterPossessionState CanBePossessed PossessionAnchor`；声音入口只收 `Vector2 where` |
| H5 | 扫描 100% 真实 | 不做假阳性 |
| H6 | 同一信号全局一种含义；静音也看得懂 | 每个效果都要有**画面**提示（闪烁/字幕/头顶字），不能只靠声音 |
| H7 | 改移动参数 → 全部关卡重跑可达性 | 跑 `verify.sh` |
| H8 | 跳跃不在临界区 | 马里奥实际跳高 ≈2 格；AI 只在目标 dx<2.25 时往上跳（`LevelPathPlanner.JumpUpSide=2`） |
| H9 | 无卡死；任何控制都有结束 | 关人/晕/吊都有计时自动放；最外圈与最底层不可炸；卡住救援 `Step1StuckRescue` |
| H10 | 无人干预时马里奥通关率 ≥95% | 学习/小心只能**减速**不能绕路停住；新 AI 行为不能让 hands-off 卡死 |

支柱与 AI 协作规则（同样必须遵守）：
- **每种优势都要有代价/反制**（宪法 A2）：新技能必须写清"代价"和"马里奥怎么反制"。
- **修"不公平"先改呈现（预兆、可见度），后改数值**；压倒性打法先加代价，不先禁用。
- **数值不写死在玩法代码里**：全部进 `MarioMindTuningSO`（资产 `Resources/Step1/RushMarioTuning`），带中文 Tooltip。
- **重玩变化来自马里奥的性格/目标/学习状态，不来自地图平移。**
- 实现者不能宣称"好玩"——只能说"具备条件"，好不好玩由用户试玩判断。
- 用户已明确：**忽略"连玩 20 局"门槛**（S195）；要**魂系/艾尔登法环式箱庭**；借鉴只借规则不借素材。

## 2. 质量红线（历次踩坑总结）

1. **零代码扩展**：加元素只动 `AsciiElementRegistry`（+`BUILTIN_ENTRY_COUNT`）+ `ElementCatalog` + `LevelThemeProfile` 槽位 + `MechanismExplorationPlan.NotProbed`，**不改生成器核心**。
2. **版本号三件套**：新数值 → `MarioMindTuningSO.CurrentDataVersion`+1 并加 `if (dataVersion < N)` 默认值块；场景结构变 → `Step1PrankRoomBuilder.BuilderVersion`+1 并写一行注释。
3. **每个新机制配测试**（加在 `Tests/EditMode/Step1RushMarioTests.cs` 的 `static LevelPathPlanner.Cell CellOfIn(` 之前）：纯逻辑静态函数测行为 + `StringAssert` 测关键接线 + H4 检查 + 样板仍可玩。
4. **纯逻辑优先**：判断写成 `public static` 纯函数（如 `ShouldFire`、`CanDecoy`、`AlarmAt`），MonoBehaviour 只做接线——这样沙盒能验证。
5. **随机要可复现**：用回合种子 `MarioMindDriver.RoundSeed`，不用裸 `Random`。
6. **按键两套输入都读**：用 `Step1Keys.Down(KeyCode.X)`（新键先在 `Step1Keys` 的 switch 里加一行），否则用户那边"按了没反应"。按键前检查 `Step1HandsOffCheck.IsRunning / Step1PlaytestLog.IsTyping / Step1Screen.HelpOpen / Time.timeScale`。
7. **所有玩家可见文字**进 `Step1Text`（中英对照），并更新 `ControlsBar`、`Help`、`Step1MapLegend`。
8. **性能**：不在 Update 里 `FindObjectsOfType`（用静态列表/缓存），物理查询用 NonAlloc，OnGUI 用 `Step1Gui.Text` 缓存样式。
9. **样板/房间改了要跑 verify.sh**：体检会报具体格子问题（悬空、炮口被挡、弹簧头顶、死局）——按提示挪格子，别关检查。
10. **策略死局**：任何"会破坏地形/改变地形"的新机制，都要进 `StrategySim`（或说明为什么不影响），verify 的"炸弹仍能困住"=失败。
11. **沙盒没有 Unity**：永远不要说"测试通过"，只能说"编译通过 + 字符串断言 N 条 + 纯逻辑体检通过，Unity 里 EditMode 测试请你跑"。
12. **网页设计台跟着项目走**：元素/样板/规则有变 → `python3 tools/LevelStudioWeb/build.py` 重建，`index.html` 随升级包交付（用户在网页里画的图要和 Unity 一致）。
13. **交付前必须 `verify.sh` 全绿 + `make_patch.sh` 显示 TREE IDENTICAL**。
14. **手感（S216）**：会把人弄飞的机关，设速度后调 `ApplyKnockbackStun(秒, untilLanded: true, slide: false)`；**不许写"硬直期没有重力"的代码**；新弹飞在 sim S216 的 cases 里加一行（弹高 < 房间头顶空格）。发动要有冲击画面（`Step1Fx`，纯画面、不加碰撞体——H4），范围型效果的冲击环 = 真实范围；状态变化不瞬移（0.1–0.3 秒缓动）。震屏用 `Step1RoomCamera.Current.Shake`。
15. **版本断言用 `GreaterOrEqual`**：测试里不要写 `AreEqual(N, CurrentDataVersion/BuilderVersion)`，否则下次升版本旧测试全红。新数值默认块若要**覆盖**旧块写过的字段（如 springLaunchSpeed），把新 `if (dataVersion < N)` 块放在 `UpgradeData` 最后（`dataVersion = CurrentDataVersion;` 之前）。
16. **两份脚本要同步**：技能目录 `scripts/` 与仓库 `docs/AI_CONTINUE_PACK/scripts/`（sim/Check.cs、verify.sh、setup_sandbox.sh）改一处就两处都改；新纯逻辑文件要进 sim 时，两份 `setup_sandbox.sh` 的 SIMSRC 列表都加，并给当前 `sim/sim.csproj` 加一行。verify.sh 会先把技能里的 `sim/Check.cs` 复制到 `$WS/sim/`。sim 的 Check.cs 里元组类型要写 `UnityEngine.Vector2`。

## 3. 标准工作循环

```
读分册 → 调研（需要时，见 research.md）→ 写纯逻辑 + 接线 + Tuning + Text + 测试
→ bash scripts/verify.sh（红了就修）→ 更新文档三处 → git commit（英文，末尾带 (SXXX)）
→ bash scripts/make_patch.sh SXXX "提示" → 刷新接续包（delivery.md 末尾）→ 打 out/D<N> 包 → genspark_deliver_files → 中文汇报
```
文档三处：`SESSION_TRACKER.md`（"最新 Session"行 + `### [SXXX] 用户：` 条目）、`docs/ELEMENT_LEGEND.md`（新字符插在 `| \`K\` |` 行前）、`docs/step1/SXXX_主题.md`（大白话说明 + 参数表 + 规则保障 + 下一步）。

## 4. 和用户沟通（非常重要）

- 用户**不懂技术**：全中文、大白话；说"为什么坏了 / 怎么修的 / 你要点哪里"，不说类名。
- 汇报结构（S21x 起固定）：一句结论 → 为什么 / 做了什么 / 已经查过的 / 刻意没做的（避免矫枉过正）→ "你的三/四步"；旧格式：一句结论 → 新东西表格（名字 / 怎么用 / 代价·反制）→ 验证情况（诚实写哪些是 Unity 里才能确认的）→ **最少步骤**（① 解压双击 `apply_SXXX.bat` 选 Y ② Unity Test Runner → EditMode → Run All ③ 工坊 Ctrl+Alt+W → 样板 → 试玩）→ 1–2 个下一步选项。
- 用户说"继续"= 做上次汇报里推荐的下一步。用户给截图 = 先用 `gsk understand_images` 看清再判断。
- 调研结论要**带来源链接**。
- 用户本地项目路径：`E:\BaiduNetdiskDownload\MarioTricksterGensparkAI\MarioTrickster`；仓库 `https://github.com/jiaxuGOGOGO/MarioTrickster.git`，分支 `genspark_ai_developer`（用户用 bat 推送，AI 不直接推）。
