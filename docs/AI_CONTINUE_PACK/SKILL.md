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
| 关卡/样板/工坊/箱庭/监狱塔/小镇大地图（S210 星露谷视角）/小镇大机关·连锁·天气（S218）/巨炮瞄准·山地·雷雨泥石流（S219）/心·雷区·补心能量·Q 雷云·像素图标·防卡（S220） | `references/level-design.md` |
| 修 bug / 用户截图报错 / "没反应" / 用户发来反馈包 zip | `references/bugfix.md` |
| 调研玩法再升级 | `references/research.md` |
| 文件在哪、关键数值、字符表 | `references/project-map.md` |
| 用户发来"设计单"/网页设计台 / 改网页 | `references/web-studio.md` |
| 机关/受伤/弹飞的手感与画面（"生硬、不自然"、特效、震屏） | `references/new-element.md` 末尾"S216 手感清单" + `references/project-map.md` "S216 手感" |
| 全局总览/一天节奏/菜单整理 | `references/level-design.md` + `references/project-map.md` "S215" |
| 小镇居民台词 / 住户 / 真心话 / 道具箱洗牌袋 / 数值关系表（S232） | `repo/docs/step1/S232_STORIES_RANDOM_TUNING.md` §3 §6 §7 |
| 用户自己改台词 / 台词编辑器 / MyTownStories.json（S234） | `repo/docs/step1/S234_MY_LINES_EDITOR.md` §0 §4 |
| 当场喊 / 居民笔记本 / 存档 / 台词本 / 灵感骰子 / 网页读 .asset（S233） | `repo/docs/step1/S233_WITNESS_NOTEBOOK_AUTHORING.md` §3 §6 §7 |
| 打包交付给用户 | `references/delivery.md` |
| 用户问"项目能做什么 / 某功能在哪 / 怎么用来创作" | `repo/docs/FEATURE_MAP.md`（= FeatureMap.cs）+ `repo/docs/step1/S236_FEATURE_MAP_AND_EASIER_TESTING.md` §2 |

## 0.5 换账号 / 全新对话也能接上（不依赖任何账号记忆）

- 本包**自给自足**：不需要旧账号的历史对话、记忆或 AI Drive。仓库是公开的（`https://github.com/jiaxuGOGOGO/MarioTrickster`），任何账号都能克隆。
- 用户如果**没有把技能加进新账号**，而是直接把 `mariotrickster-continue.skill`（zip）发进对话：`gsk download` 下来 → `unzip` 到 `~/.opencode/skills/` 或工作区 → 读里面的 `SKILL.md`，照做即可。
- `scripts/pending/*.patch` = 打包时**还没上传到 GitHub** 的改动。`setup_sandbox.sh` 会自动检查：GitHub 上缺哪个就补哪个（已有的跳过）。所以哪怕用户换了账号、忘了跑 bat，进度也不会丢。补完后在汇报里提醒用户"上次的补丁还没上传，这次的 bat 会一起带上"。
- 上传 GitHub 是用户**自己电脑上的 git 账号**在做（bat 里选 Y），和 Genspark 账号无关。如果用户换了 GitHub 账号、推不上去：让他在仓库设置里把新账号加为协作者，或 fork 后告诉你新地址（改 `setup_sandbox.sh` 与 `make_patch.sh` 里的仓库地址）。
- bat 找不到项目文件夹时会让用户**把 MarioTrickster 文件夹拖进窗口**，换电脑/换路径也能用。
- **每次交付后**都要刷新本包：`scripts/pending/` 换成最新未上传补丁（`make_patch.sh` 的输出里那些 .patch），并重新打 `.skill` 交给用户，让用户"随时都拿着最新的接续包"。

## 0.6 进度快照（打包时写入，以 SESSION_TRACKER 为准）

- 最新交付：**S240**（`repo/docs/step1/S240_CANNON_SEAT_SPENT_PROPS_COMBO_STUCK.md`——**永久规则**：① 捣蛋者用大炮 = 没伪装站炮口按 ↓ 坐进去（`PranksterCannon.Seat` / `TricksterController.EnterSeat` 锁身），方向键瞄准 + 虚线预览，空格/L 发射，`tricksterCannonSpeed/Cooldown/MaxSitSeconds`；坐着时 `PranksterCannon.TricksterSeated` = 其他技能键不生效（新技能键也要判它）；马里奥钻炮不变。② 用过的机关：`ControllablePropBase.SpentThisRound` → 变灰、不选、不连线、不编号；新的"一次性"机关只要 `ExtraControlCondition` 返回 false 就自动生效；`GreyWhenSpent` 可关。③ 连击显示原因：`Step1ComboFeel.CauseName/ChainText/WindowLeft01`；新的"坑到马里奥"事件要给原因名。④ 卡住：`Step1StuckRescue.Tick`（晕/张望 = 暂停不清零 + `stuckHardCapSeconds` 兜底），救援前物理查地面，记 `PlaytestLogs/step1_stuck.txt` + 自动截图；工坊检查轨迹显示 `LevelRouteFollower.CriticalJumpCells`（黄 = 只能跳满 2 格才出得去，塌后也查）和试玩卡点。默认房间塌桥坑两边加了台阶，sim 要求默认房间临界跳 = 0；数值版本 29）。上一版 **S239**（`repo/docs/step1/S239_FUN_FIRST_MERGE.md`——用户："不考虑任何宪法 只考虑游戏好玩"。**永久规则**：和现有玩法重复的旧系统已整块删掉（热度 TricksterHeatMeter / 警报导演 AlarmCrisisDirector / 连锁追踪 PropComboTracker / RepeatInterferenceStack / RouteBudgetService / InterferenceCompensationPolicy / CounterRevealReward / 第 0 步锚点起疑层 MarioSuspicionTracker 等 / 旧 UGUI 界面 GlobalGameUICanvas、GameUI；CoreLoopOnly、StripUnusedLegacy、Debug* 开关也没了）——**不要按宪法第 4 步把它们加回**，对应玩法见文档 §2 表；视线检测 = `Gameplay/SightLine.cs`（`CanWitness` / `IsOneWayPlatform`）；§3 两个点子（残留痕迹、越坑越警觉）等用户拍板；PlayMode 测试 33 条）。上一版 **S238**（`repo/docs/step1/S238_CLEANUP_DONE.md`——**永久规则**：旧工具已全删（Ctrl+T 测试台 / AI Arena / 旧探索 MechanismExplorationPlan / 录像 InputRecorder / TestSceneBuilder / MemoryGuard / LevelStudioPlaySession）——**不要再引用或重建**；F5/R 重开 = `Editor/PlayRetry.cs`；**调参文件只有 RushMarioTuning 一个**（GameplayLoopConfig 已删，`GameplayMetrics` 读 `MarioMindTuningSO`，扫描/能量/附身 20 个在「你的技能」组；不许再加第二个配置 SO）；性格 = 一个下拉 `marioPersonality`（旧 `personalitiesEnabled/fixedPersonality` 是 HideInInspector 私有，只用于升级）；"新元素必须写说明" = `ElementCatalog.Unexplained`；菜单顶层 **10 个**（开始页 / 测试中心 / ▶ 试玩房间 / ▶ 试玩小镇 / 关卡工坊 / 小镇工坊 / 台词编辑器 / 检查与记录 Checks / 美术 Art / 安全网 Safety，sim 要求恰好 10）；旧文档只进 `docs/archive/`；测试唯一入口 = 测试中心 🧪 跑 EditMode 测试）。上一版 **S237**（`repo/docs/step1/S237_DEDUP_AND_LAST_WORK.md`——**永久规则**：新菜单只能进已有顶层；新调参字段必须 `[Tooltip("中文")]` 且所在 Header 要在 `TuningGroups.All` 归组；场景重建看 `Step1PrankRoomBuilder.BuildKey(tuning)`；工坊打开/保存关卡调 `StartHereWindow.Touch`。D1–D4 M1–M3 已在 S238 做完）。上一版 **S236**（`repo/docs/step1/S236_FEATURE_MAP_AND_EASIER_TESTING.md`——**永久规则（防遗忘）**：`LevelDesign/FeatureMap.cs` 是"项目能做什么"的唯一来源（Unity 开始页 Ctrl+Alt+H、网页功能地图页、docs/FEATURE_MAP.md 都读它）。**新菜单 / 新游戏按键 / 网页新面板(h3/页签) / docs/step1 新文档 都必须登记一项（能并进已有的就并），`since` 写 Session 号，并用 `FeatureMap.Markdown()` 重新生成 docs/FEATURE_MAP.md**（verify 失败时 sim 把新内容写到 /tmp/opencode/FEATURE_MAP.md，复制过去即可）；功能删了就删那项；字符串里不许英文双引号。测试用 F9 = `Step1QuickTest.NoLimits`（技能无限，CSV mode=f9 不算出口）；问卷数字用 `Step1Keys.Digit1to5()`）。上一版 **S235**（`repo/docs/step1/S235_FALL_OUT_OF_ROOM.md`——掉出房间：`Step1Bounds` 纯逻辑 + `Step1RoomGuard`（外圈外看不见的墙、出界回出生点 -`fallOutLivesLost` 命、马里奥出界 `RescueNow`）；`BodyUnstick.RoomSize` 推出墙只往里；**新的"会把人挪走/锁住"的机关必须用 `Step1Bounds.Teleported` 判断人被传送走了就放手**；新"会把人轰飞"的机关不用再管出界（守卫兜底）；小镇 `OverworldMap.Unstick`；数值版本 27、构建器 21）。上一版 **S234**（`repo/docs/step1/S234_MY_LINES_EDITOR.md`——**永久规则 21–24 必读**：`Assets/Resources/My*.json` 是**用户写的**，AI 的补丁不许包含 / 修改（sim 检查）；AI 改默认台词只改 `TownStory.Default` + `TownStories.json`；读台词只走 `TownStory.Load`（游戏）/ `TownStoryEditorWindow.Effective()`（编辑器）；台词编辑器 Ctrl+Alt+L；用户发来 F8 包里有 MyTownStories.json = 照他的语气补）。上一版 **S233**（`repo/docs/step1/S233_WITNESS_NOTEBOOK_AUTHORING.md`——**开工先读 §3 E 表 + §6 永久规则 16–20**：大机关砸中马里奥 → 最近那户当场喊 `TownStory.WitnessOn`（新"砸中"事件必须走 `OverworldTown.HitMario`）；N 居民笔记本；居民记忆存 PlayerPrefs `OverworldGame.MemoryKey`；台词检查 `TownStory.Validate`（✗/⚠ 必须 0）；网页台词本导出 = `TownStory.ToJson`；灵感骰子 `Overworld/IdeaDice.cs`；网页只读 `RushMarioTuning.asset`（`TuningAudit.FromYaml`）。下一步候选 E11 笔记本贴纸）。上一版 **S232**（第四轮调研 `repo/docs/step1/S232_STORIES_RANDOM_TUNING.md`——**开工先读 §3 E 表 + §6 永久规则 11–15**：小镇居民的话 `Overworld/TownStory.cs` + `Assets/Resources/TownStories.json`（改台词改这个文件，sim 要求它 = `TownStory.Default`：改了一边用 `TownStory.ToJson(TownStory.Default)` 重新生成）；住户 `# Resident:`；道具箱洗牌袋 `OverworldPickupBag`；网页读 Unity 调参默认值 `TUNING` + 数值关系 `TuningAudit.Rules`（新数值有大小关系就加一行）。居民只说话不行动（H4）。下一步候选 E8 居民笔记本、E4b 网页读 .asset）。上一版 **S231**（第三轮调研 `repo/docs/step1/S231_RESEARCH_V3_PLAN.md`——**开工先读 §3 E 表**：第 3 次同种坑换台词 `MarioReaction.Line(b,nth)`；体检中位数 + 改版对比 `Step1ExitReport.A12/CompareVersions`（每边 ≥5 局）；结算最稀罕一招 `RarestLine`；换图自检 `OverworldArt.Audit` + 菜单）。上一版 **S230**（`repo/docs/step1/S230_RESEARCH_VS_CODE_AUDIT.md`：调研×代码×GitHub 对照；town_days.csv 进体检/F8 包，反应时间对比机器人 0.15–0.6 s；GitHub master 落后开发分支 81 提交，待用户决定是否合并）。上一版 **S229**（`repo/docs/step1/S229_DEATH_ENDS_DAY.md`：小镇心掉光 = 这一天结束、结算写死因、6 秒自动重开、平局；town_days.csv 记反应时间；数值版本 26）。上一版 **S228**（`repo/docs/step1/S228_RESEARCHED_NUMBERS_BELL_WINDOW.md`：修小镇声音到不了 ? 的老 bug、调研定值、钟楼 B、房间炮把他轰出窗户；数值版本 25）。上一版 **S227**（阶段 D 填表自动化 `repo/docs/step1/S227_EXIT_REPORT_FROM_YOUR_DATA.md`：`Step1ExitReport` 读试玩 CSV → 测试中心体检"第 1 步出口"；小镇房间/快速测试也记一行；CSV 加 tuning_version、mode）。上一版 **S226**（自我对照审计 `repo/docs/step1/S226_SELF_AUDIT_AND_DEFERRED.md`：S225 E5/E6/E7/E9/E10 全做完 + 补房间马里奥统计门槛；看情况的按键条 `Step1Text.ControlsBarFor`；游戏速度 `roomGameSpeed`；中招反应 `MarioReaction.Punch`；门 4 山坡；数值版本 24）。上一版 **S225**（第二轮调研 + 方案 `repo/docs/step1/S225_RESEARCH_V2_EXECUTION_PLAN.md`——**开工先读它的 §3 E 表**；按 L/P 失败说原因 `Step1FailFeedback`；起疑说原因 `SuspicionCause`/`Step1Text.CauseIntent`；sim 房间死区门槛）。上一版 S224 少等待 + 阶段 C 可读性；S222 总方案 `repo/docs/step1/S222_RESEARCH_MASTERPLAN.md`（§4 阶段表、§7–8 更正）。
- 数值版本 `MarioMindTuningSO.CurrentDataVersion = 28`；房间构建器 `Step1PrankRoomBuilder.BuilderVersion = 22`。
- 近几次做了什么（详情看 `repo/docs/step1/S21x_*.md`）：
  - S210 星露谷视角小镇大地图（门 = 恶作剧房间）；S211 场景转场；S212 圆形转场 + 指引箭头 + 编辑器快捷键；
  - S213 小镇规则纯逻辑化 + 7 种机器人玩家模拟；S214 网页 ↔ Unity 自动同步（Inbox 文件夹、▶ 在 Unity 试玩、PageUp/PageDown 切关）；
  - S215 一天总览（CampaignLedger，Unity 小镇工坊 + 网页大地图逐字一致）、小镇房间按 3+3 颗炸弹加固、14 个旧菜单收进"旧工具 (Legacy)"；
  - S217 小镇"↔ 扩展"（C# Resize = 网页 owResize）、上限 192×128；说明面板任意键关、进 Play 自动切 Game 窗口、全局键两套输入、转场保险丝、等出门提示；测试中心（一键体检 / 快速测试模式 / F8 反馈 / 打包 zip）；
  - S224 少等待 + 可读性：sim 有宪法 P4 死区统计（`OverworldBots.Report.deadZoneSeconds`，连续 ≥10 秒干等）；`PlayDay(..., autoFast:true)` = 游戏里的默认；新声音要画圈 → 半径必须调用 `Step1Readability` 里和判定同一个函数（sim 字符串检查）；草晃不画声音圈（是看见不是听见）；
  - S223 阶段 B 反应：新坑种类 = 在 MarioReactions.json + MarioReaction.Default 各加一行（sim 逐项对照），愣住+动作 ≤ 该坑晕眩秒数；
  - S222 审计：旧机器人除 Chaos/Slow 外完全确定（N 天 = 1 个样本）→ 统计门槛必须开 humanNoise；方案阶段 B 马里奥中招反应 → C 可读性收尾 → D 第 1 步出口验证（用户 20 局）→ E 美术/UGC；B–D 期间冻结横向加系统；
  - S221 探针全流程模拟 → 修连控（GraceAfter）/ 雷云等待（StormBlocksRoute）/ 机器人按 Q；
  - S220 心/伤害表/雷区/补心能量/Q 雷云/带心进房间（OverworldStorm；OverworldTown TickStorms/FireStrike/TryCloud/HurtYou/HitMario/GainEnergy；OverworldRoomCarry；OverworldArt + Editor/OverworldArtTools；工坊 Touch/Recheck 防卡规则：OnGUI 里不做读盘/全图检查）；
  - S219 巨炮瞄准 / 马里奥坐炮 / 山地视线 / 山洞 / 雷雨闪电 / 酸雨 / 泥石流（OverworldProps 瞄准+山地函数；OverworldTown Seat/Ride/TryTamper/TryCave/FireLightning/FireMud；Events.Pool(map)）；
  - S218 小镇大机关（OverworldProps + OverworldEvents；OverworldTown Arm/Fire/Impact；马里奥听见起疑、吃过亏会躲；砸晕带进房间、守住一户重新装填；天气第 1 天晴；网页逐字对照）；
  - S216 修复"硬直期往上飞没有重力"（Step1Feel.StunStep，全程重力 40、落地才恢复），受伤红白闪 + 小跳、Step1Fx 冲击环/尘土/连锁火花线、预警越来越急、平滑震屏。
- 交付包固定结构（`out/D<N>/`）：`00_先看我_怎么用.md`、`01_安装到项目`（全部补丁 + `install_and_upload.bat`）、`02_接续包_换账号用`（本 .skill）、`03_说明文档`（S21x_*.md）、`04_关卡设计台网页/MarioTrickster关卡设计台.html`。
- 用户规划（S229）：**所有想法和调研反馈优化都做完后再测一两轮**——测试不是开工前提；小镇心掉光 = 结算重开（不困死，但天灾真能打死人）。
- 上次汇报推荐的下一步（用户说"继续"时做）：用户 S228 说"忽略试玩、先按调研做"→ 试玩不是开工前提。剩下只有：机器人手抖校准（需要反应时间数据，CSV 没有）、改键、牛群/停电（新 AI / 改日程，仍不做）。有新 CSV 就放 `repo/docs/step1/data/` 让体检出建议。新增"一次性起疑"类刺激时：确认它真的能到 `?`（sim S228 有正反测试）。
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
- 用户已明确：**忽略"连玩 20 局"门槛**（S195），并且 S224 说"先做得更值得玩再试玩"——试玩是验收参考，不是开工前提；要**魂系/艾尔登法环式箱庭**；借鉴只借规则不借素材。

## 2. 质量红线（历次踩坑总结）

1. **零代码扩展**：加元素只动 `AsciiElementRegistry`（+`BUILTIN_ENTRY_COUNT`）+ `ElementCatalog` + `LevelThemeProfile` 槽位（`ElementCatalog.Unexplained` 必须为空），**不改生成器核心**。
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
17. **用户写的内容归用户（S234）**：`Assets/Resources/MyTownStories.json`、`MyMarioReactions.json` 永远不进补丁、不改；要改默认台词改 `TownStory.Default` 并用 `TownStory.ToJson(TownStory.Default)` 重新生成 `TownStories.json`；读台词只走 `TownStory.Load` / `TownStoryEditorWindow.Effective()`。
18. **功能地图（S236）**：见 0.6 永久规则；汇报"新东西"时同时说它在开始页哪一区。
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
- 汇报结构（S21x 起固定）：一句结论 → 为什么 / 做了什么 / 已经查过的 / 刻意没做的（避免矫枉过正）→ "你的三/四步"；旧格式：一句结论 → 新东西表格（名字 / 怎么用 / 代价·反制）→ 验证情况（诚实写哪些是 Unity 里才能确认的）→ **最少步骤**（① 解压双击 `install_and_upload.bat` 选 Y ② Unity Ctrl+Alt+T 测试中心 → 🧪 跑 EditMode 测试 ③ 工坊 Ctrl+Alt+W → 样板 → 试玩）→ 1–2 个下一步选项。
- 用户说"继续"= 做上次汇报里推荐的下一步。用户给截图 = 先用 `gsk understand_images` 看清再判断。
- 调研结论要**带来源链接**。
- 用户本地项目路径：`E:\BaiduNetdiskDownload\MarioTricksterGensparkAI\MarioTrickster`；仓库 `https://github.com/jiaxuGOGOGO/MarioTrickster.git`，分支 `genspark_ai_developer`（用户用 bat 推送，AI 不直接推）。
