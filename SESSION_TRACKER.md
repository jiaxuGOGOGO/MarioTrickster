# MarioTrickster Session Tracker

> **AI 协作唯一入口文档**：AI 每次新对话**只需读取本文件**，即可了解当前状态、行为规范并进入工作循环。

---

## 0. AI 行为规范（新对话必读）

> 本节定义了 AI 在本项目中必须遵守的所有规则。无论换号还是新对话，读完本节即可无缝衔接。

### 0.1 文档读取顺序

```text
1. SESSION_TRACKER.md（本文件）→ 必读，获取当前状态、待办、行为规范
2. 用户消息 → 理解本次任务
3. 按需读取其他文档：
   - 需要完整上下文（功能清单/Bug库/技术决策）→ 读 MarioTrickster_Progress_Summary.md
   - 需要纵览全局（设计规划vs实现状态/模块完成度）→ 读 MASTER_TRACKER.md
   - 需要测试用例/影响矩阵/键位表 → 读 MarioTrickster_Testing_Guide.md
   - 需要协作流程/Git速查 → 读 AI_WORKFLOW.md
   - 需要游戏设计/核心机制 → 读 GAME_DESIGN.md
```

### 0.2 Bug 修复必须流程

修复任何 Bug 时，**必须按顺序执行以下 5 步**：

| 步骤 | 操作 | 说明 |
|------|------|------|
| 1 | **修复代码** | 实施 Bug 修复 |
| 1.5 | **同步更新测试场景** | 如果修复涉及新行为/新元素/行为变更，必须同步更新 `TestSceneBuilder.cs` 中对应 Stage 的场景布局和指示标签（见 §0.8） |
| 2 | **查询影响矩阵** | 读取 `Testing_Guide.md` 第四章 4.1 节的影响矩阵表，根据修改的脚本查找“直接影响”和“间接可能影响”的测试项 |
| 3 | **更新回归清单** | 在本文件第 3 节“回归验证清单”中填写需要回归验证的测试项，将第 4 节中受影响的已通过测试从 ✅ 改为 🔄 |
| 4 | **更新文档** | 更新 SESSION_TRACKER.md（本文件）和 Progress_Summary.md 中的相关记录 |
| 5 | **通知用户** | 在修复报告中明确列出：修复内容 + 回归验证清单 + 验证步骤 |

### 0.3 推送前文档联动更新矩阵

> **核心原则**：每条信息只有一个“真相源”文档，其他文档只引用不重复。修改时先更新真相源，再更新引用方。

**信息真相源分配表：**

| 信息类型 | 真相源文档 | 引用方（只引用不重复） |
|---------|-----------|-------------------|
| **全局设计与实现映射** | **MASTER_TRACKER §1** | **无其他引用（全局总览）** |
| 当前状态/Session号 | SESSION_TRACKER §1 | Progress_Summary 头部 |
| AI 行为规范 | SESSION_TRACKER §0 | AI_WORKFLOW 引用此处 |
| Bug 完整库 | Progress_Summary §3 | SESSION_TRACKER §5 只放未完成的 |
| 测试用例全量列表 | Testing_Guide §3 | SESSION_TRACKER/Progress_Summary 只引用数量 |
| 影响矩阵 | Testing_Guide §4 | SESSION_TRACKER §0.2 引用 |
| 文档职责导航 | SESSION_TRACKER §7 | README + AI_WORKFLOW 引用 |
| 开场模板 | SESSION_TRACKER §8 | AI_WORKFLOW §1.1 引用 |
| 工作流图解 | README | AI_WORKFLOW 引用 |
| 文件结构树 | Progress_Summary §8 | 无其他引用 |
| 键位表 | SESSION_TRACKER §6 | Testing_Guide §5 引用 |

**按修改类型触发的联动检查表：**

AI 每次 `git push` 前，根据本次修改类型，按表逐行检查：

| 修改类型 | SESSION_TRACKER | Progress_Summary | Testing_Guide | AI_WORKFLOW | README | MASTER_TRACKER |
|---------|:-:|:-:|:-:|:-:|:-:|:-:|
| 修复 Bug | §1状态 + §3回归 + §4测试 + §5待办 | §3 Bug库 + §5 Session记录 | §4影响矩阵(新脚本时) | — | — | 状态变更时更新 §1 |
| ↑ 涉及可见行为变更时 | + **§0.8 TestSceneBuilder 同步** | — | — | — | — | — |
| 新增/修改测试用例 | §4测试进度 | §4测试数量 + §8文件描述 | §3测试表 + §7进度 | — | — | — |
| 新增功能/脚本 | §1状态 + §5待办 + **§0.8 TestSceneBuilder 同步** | §2功能清单 + §5 Session + §8文件树 | §2手动测试(新测试项) + §4矩阵(新脚本) | — | — | **新增映射到 §1** |
| **架构重构/底层变更** | §1状态 + §3回归 | §4技术决策 + §8文件树 | §4影响矩阵(脚本变更) | — | — | **更新 §1 对应脚本** |
| **美术/音效资源接入** | §1状态 | §2功能清单 + §8文件树 | — | — | — | **更新 §1.4 状态** |
| **网络联机开发** | §1状态 + §5待办 | §2功能清单 + §4技术决策 | §2手动测试(联机项) | — | — | **更新 §1.3 状态** |
| **平衡性/数值调整** | §1状态 | §5 Session记录 | — | — | — | **更新 §1.3 状态** |
| 修改文档体系/流程 | §0规范 + §7导航 | Progress_Summary 头部 | —（❗如涉及章节编号变更，同时执行“重编号”行） | 相关章节 | 导航表 + 图解 | — |
| 修改键位/操作 | §6键位表 | — | §5键位表 | — | — | — |
| 用户测试反馈 | §2反馈 + §4测试状态 | Bug库(新Bug时) | — | — | — | 状态变更时更新 §1 |
| **重编号/重组文档章节** | grep搜索全部.md中对该文档§编号的引用并同步 | 同左 | 同左 | 同左 | 同左 | 同左 |

> **使用方法**：推送前确定本次修改属于哪一行，然后横向检查每一列是否已更新。“—”表示无需更新。

### 0.4 AI 防错规则

| 规则 | 说明 |
|------|------|
| **小步推送** | 每完成一个 Bug 修复或功能就推送一次，不要攻到最后才推。防止中途断开丢失工作。 |
| **反馈解析确认** | 用户发来测试反馈后，如果信息不完整（缺少具体测试项/回归清单结果），主动向用户确认。 |
| **手动修改检测** | 每次克隆仓库后，检查 `git log -1` 的提交者是否为 AI。如果不是，说明用户手动提交过，应主动询问修改内容并同步文档。 |
| **推送前联动检查** | 每次 `git push` 前必须执行 §0.3 联动矩阵。这是强制规则，不是建议。 |
| **MASTER_TRACKER 自检** | 每次新增功能、重构架构、接入资源或调整平衡性后，必须检查 `MASTER_TRACKER.md` §1 矩阵是否需要新增行或更新状态，并同步更新 §2 的完成度百分比。 |
| **章节引用同步检查** | 修改任何文档的章节编号（合并/拆分/重排）时，必须执行以下两条搜索确保无遗漏：`grep -rn '§' *.md`（搜索 § 符号引用）和 `grep -rn '第.*章\|第.*节' *.md`（搜索“第X章/第X节”格式引用）。找到的所有旧编号引用必须更新为新编号。范围包括：信息真相源分配表、联动矩阵本身、README 流程图、AI_WORKFLOW 中的引用等。 |
| **TestSceneBuilder 同步检查** | 每次代码修改后，必须检查是否触发 §0.8 的同步条件。如触发，必须在同一次 commit 中同步更新 TestSceneBuilder.cs。如果对话即将中断而未完成同步，必须在 §5 待办队列中标注“❗ TestSceneBuilder 待同步”。 |

### 0.5 积分管理规则

- 用户会在开场消息中提醒积分阈值（通常 300）
- 接近阈值时，**立即暂停当前工作**，优先更新 SESSION_TRACKER.md 和 Progress_Summary.md 并推送
- 推送后告知用户当前进度和未完成的工作，以便下次衔接

### 0.6 Git 推送规范

- 分支：`master`
- Commit 消息：英文，首行概述，空行后详细列出修改
- 推送前确认：`git status` 检查所有修改都已暂存
- Token 认证：使用用户提供的 GitHub Token

### 0.7 Session 编号规则

- 每次新对话如果产生了代码修改或重要文档更新，Session 编号 +1
- 仅回答问题或阅读文档不增加 Session 编号
- 新 Session 编号 = 第 1 节“当前状态总览”中的最新 Session + 1

### 0.8 测试场景同步规则（TestSceneBuilder 联动）

> **核心原则**：功能开发与测试场景必须同步更新，不允许“先写功能、后补场景”。这样用户拉取代码后立即就能 Build 出包含新功能的测试场景，形成“开发→场景更新→测试→反馈→修复”的紧密闭环。

**触发条件（任一满足即触发）：**

| 触发场景 | 操作 | 示例 |
|---------|------|------|
| 新增关卡元素/敌人/道具 | 在对应 Stage 中添加新元素实例 + 指示标签 | 新增 LaserTrap → 在 Stage 9 中添加 9J 子区域 |
| 修复影响玩家可见行为的 Bug | 更新对应 Stage 的指示标签以反映新的预期行为 | 修复击退 → Stage 7 标签添加“确认有击退” |
| 新增测试项/测试步骤 | 在对应 Stage 中添加验证所需的场景元素 | 新增“多回合测试” → Stage 7 添加多个 GoalZone |
| 新增调试开关/快捷键 | 在最相关的 Stage 标签中添加快捷键提示 | 新增 F9 无冷却 → Stage 4/5/6 标签添加“F9 取消冷却” |

**TestSceneBuilder Stage 对应关系（与 Testing_Guide 测试顺序一致）：**

| Stage | 对应测试项 | 包含元素 |
|-------|---------|----------|
| 1 | 测试 1：Mario 基础移动 | 平地 + 台阶 + 墙壁 |
| 2 | 测试 2：Trickster 基础移动 | 同上 |
| 3 | 测试 3：移动平台 | MovingPlatform |
| 4 | 测试 4：伪装系统 | 可伪装对象 |
| 5 | 测试 5：道具操控 | ControllablePlatform/Hazard/Block |
| 6 | 测试 6：扫描技能 | Trickster 伪装目标 |
| 7 | 测试 7：胜负判定 | 敌人 + 金币 + GoalZone |
| 8 | 测试 8：暂停系统 | 无特殊元素 |
| 9A-9I | 测试 9：关卡元素 | 地刺/摆锤/火焰/弹跳怪/弹跳平台/单向平台/崩塌平台/隐藏通道/伪装墙 |
| 终点 | 胜利判定 | GoalZone |

**跨对话衔接要点：**
- 新对话 AI 读取本节后，必须在每次代码修改时检查是否需要同步更新 TestSceneBuilder
- 如果当前对话即将中断，在 SESSION_TRACKER §5 待办队列中标注“❗ TestSceneBuilder 待同步：XXX”，确保下次对话能接上

---

## 1. 当前状态总览

| 字段 | 值 |
|------|-----|
| **最新 Session** | Session 17 (B027 根本修复 + B028 角色碰撞卡死修复) |
| **日期** | 2026-04-03 |
| **分支** | master |
| **项目阶段** | 游戏体验提升 (Sprint 2 进行中) |
| **编译状态** | ⚠️ Session 16-17 多项修复待验证 |
| **阻塞问题** | 无 |

---

## 2. 测试反馈记录 (Session 12)

```text
测试日期：2026-04-02
测试人：用户

✅ 测试 1-8 全部通过！

测试项 A（多回合终点判定 B020）：✅ 通过
- 第一回合到终点：有胜利画面
- 按 N 后第二回合到终点：有胜利画面
- Trickster 胜利：有

测试项 B（暂停功能 B021）：✅ 通过
- ESC 暂停：有遮罩和 PAUSED
- ESC 恢复：直接恢复无 RESUMED

测试项 C（序列化错误 B019）：✅ 通过
- Console 红色错误：已消除

测试项 D（CameraController B016）：✅ 通过
- 多次 Build 后只有 1 个

Session 12 修复汇总：
- B018 游戏结束UI修复：TestSceneBuilder 新增 GameUI 对象创建
- B019 originalColor 序列化冲突：父类改 protected，子类移除重复声明
- B020 第二回合终点无反应：GoalZone 新增 ResetTrigger()，GameManager.ResetRound() 调用
- B021 移除 RESUMED 提示：移除 GameManager 和 GameUI 中的恢复提示逻辑
- B016 源头修复：TestSceneBuilder Build 前清理已有 CameraController/GameUI
```

---

## 3. 回归验证清单

> AI 修复 Bug 后按 0.2 节流程自动填写。用户测试时除了当前测试项，还需快速验证此清单中的项目。
> 简化操作见 Testing_Guide.md 第四章 4.4 节。

**当前状态：Session 16 多项修复后待回归**

| 测试项 | 原因 | 简化验证操作 |
|--------|------|------------|
| 🔄 测试 1：Mario 基础移动 | MarioController.cs 添加了 knockback stun 机制 | WASD 移动 + Space 跳跃，确认手感不变 |
| 🔄 测试 2：Trickster 基础移动 | TricksterController.cs 添加了 knockback stun 机制 | 方向键移动 + 跳跃，确认手感不变 |
| 🔄 测试 7：胜负判定与UI | DamageDealer.cs 修改了击退通知逻辑 | 碰敌人受伤确认有击退效果 + 红色闪烁 |
| 🔄 测试 9：关卡设计系统 | CollapsingPlatform.cs 字段重命名 + SpikeTrap 击退测试 | 地刺伤害确认有击退 + 崩塌平台流程正常 |
| 🔄 UI 显示 | GameUI.cs 修改了计时器显示区域 + 新增无冷却指示器 | 确认时间显示完整不被裁剪 |
| 🔄 场景生成 | TestSceneBuilder.cs 完全重写为闯关形式 | Clear + Build 后确认 9 个 Stage + 标签正常显示 |
| 🔄 测试 6.5：镜头系统 | B027 根本修复: LevelManager.Start() 覆盖相机边界 (maxX=50) | 走完全部 Stage，确认镜头始终跟随 Mario |
| 🔄 角色碰撞 | B028: Mario/Trickster 分离到专用 Layer，禁用两者碰撞 | Trickster 走向 Mario 应该互相穿过，不会卡住 |
| 🔄 测试 7：敌人碰撞 | B028 副作用检查: Mario 在 Player Layer | 确认 SimpleEnemy 仍能碰撞 Mario 造成伤害 |

---

## 4. 手动测试进度总览

| 测试项 | 状态 | 说明 |
|--------|------|------|
| 测试 1：Mario 基础移动 | 🔄 待回归 | WASD 移动 + Space 跳跃（MarioController 添加了 knockback stun） |
| 测试 2：Trickster 基础移动 | 🔄 待回归 | 方向键移动 + 跳跃（TricksterController 添加了 knockback stun） |
| 测试 3：移动平台跟随 | ✅ 通过 | 站上平台不被甩飞 |
| 测试 4：伪装系统 | ✅ 通过 | P 伪装/解除，O/I 切换形态 |
| 测试 5：道具操控能力 | ✅ 通过 | Telegraph→Active→Cooldown 流程正常 |
| 测试 6：扫描技能 | ✅ 通过 | Q 键扫描，脉冲+文字提示正常 |
| 测试 6.5：镜头系统 | ✅ 通过 | 平滑跟随，无晃动 |
| 测试 7：胜负判定与UI | 🔄 待回归 | 多回合胜利/失败画面 + 受伤击退效果 |
| 测试 8：暂停系统 | ✅ 通过 | ESC 暂停/恢复正常，无多余提示 |
| EditMode 自动化测试 | ✅ 通过 | 59/59 全部通过（Session 13 修复 ForceAwake + DisguiseSystem断言） |
| PlayMode 自动化测试 | ✅ 通过 | 21/21 全部通过（用户通过 Test Runner 验证） |

**全部测试通过！手动 9/9 + EditMode 59/59 + PlayMode 21/21 = 总计 89/89 ✅**

> **新工具**：Session 13 新增 TestReportRunner，可通过 `MarioTrickster → Run Tests → Export Full Report` 一键运行所有测试并导出完整错误报告到 `TestReport.txt`，无需逐个点击查看错误。

> **Session 13 修复**：EditMode 测试中 `AddComponent` 不会自动调用 `Awake()`，导致 16 个测试失败。通过添加 `ForceAwake()` 辅助方法（反射调用 Awake）统一修复。

---

## 5. 待办队列 (Backlog)

> AI 每次对话从队首取未完成任务。用户消息中指定的任务优先级最高。

| 优先级 | ID | 描述 | 状态 |
|--------|-----|------|------|
| ~~P0~~ | B020 | 第二回合终点无反应 | ✅ 已修复已验证 |
| ~~P0~~ | B021 | RESUMED 恢复提示多余 | ✅ 已修复已验证 |
| ~~P0~~ | B019 | originalColor 序列化冲突 | ✅ 已修复已验证 |
| ~~P0~~ | B018 | 游戏结束UI未显示 | ✅ 已修复已验证 |
| ~~P0~~ | B016 | 镜头来回轻微晃动 | ✅ 已修复已验证 |
| ~~P0~~ | B015 | 扫描提示矛盾 Bug | ✅ 已修复已验证 |
| ~~P0~~ | B017 | 终点无胜利判定 | ✅ 已修复已验证 |
| ~~P0~~ | UI | Trickster状态文字被裁剪 | ✅ 已修复已验证 |
| ~~P0~~ | B022 | CollapsingPlatform stateTimer 字段隐藏冲突 | ✅ 已修复 (Session 16) |
| ~~P0~~ | B023 | 受伤无击退效果（只有红色闪烁+扣血） | ✅ 已修复 (Session 16) |
| ~~P0~~ | B024 | UI时间显示不全（被裁剪） | ✅ 已修复 (Session 16) |
| ~~P0~~ | B025 | 新增无冷却调试开关 (F9) | ✅ 已实现 (Session 16) |
| ~~P0~~ | B026 | 测试场景重构为闯关形式 + 场景指示标签 | ✅ 已实现 (Session 16) |
| ~~P0~~ | B027 | 闯关场景相机边界未设置，Stage 3 后镜头不跟随 | ✅ 根本修复 (Session 17): LevelManager 边界同步 |
| ~~P0~~ | B028 | Trickster 跳到 Mario 头上无法跳走（角色碰撞卡死） | ✅ 已修复 (Session 17): Player/Trickster Layer 分离 + 禁用碰撞 |
| **P1** | — | **关卡设计系统 (Level Design)** | ✅ 框架已完成 |
| **P1** | — | **音效系统 (Audio)** | 未开始 |
| P2 | — | 动画系统完善 | 未开始 |
| P2 | — | 主菜单 UI | 未开始 |

---

## 6. 键位速查（避免混淆）

| 角色 | 按键 | 功能 |
|------|------|------|
| Mario (P1) | WASD | 移动 |
| Mario (P1) | Space | 跳跃 |
| Mario (P1) | **Q** | **扫描**（需要 Trickster 先按 P 伪装才能检测到） |
| Trickster (P2) | 方向键 | 移动 |
| Trickster (P2) | 上/右Ctrl | 跳跃 |
| Trickster (P2) | **P** | **伪装/解除伪装** |
| Trickster (P2) | O/I | 切换伪装形态 |
| Trickster (P2) | **L** | **操控道具**（不是伪装！） |
| 全局 | ESC | 暂停/恢复 |
| 全局 | F5 | 快速重启 |
| 全局 | **F9** | **切换无冷却模式**（调试用，取消所有技能冷却） |
| 回合结束 | R | 重启关卡 |
| 回合结束 | N | 下一回合 |

---

## 7. 文档职责导航（单一真相源）

| 文档 | 职责 | 谁看 |
|------|------|------|
| `SESSION_TRACKER.md` | **入口 + 规范**：当前状态、AI 行为规范、回归清单、测试进度、待办 | **AI 每次必读**，用户每次测试必填 |
| `MASTER_TRACKER.md` | **全局总览**：设计愿景与代码实现的映射矩阵、模块完成度 | 用户需要纵览全局进度时看 |
| `MarioTrickster_Progress_Summary.md` | **存档**：功能清单、Bug 库、技术决策、文件结构、Session 历史 | AI 需要完整上下文时读 |
| `MarioTrickster_Testing_Guide.md` | **手册**：全量测试用例、影响矩阵、键位表、调试信息说明 | AI 修 Bug 时查影响矩阵，用户测试时看 |
| `AI_WORKFLOW.md` | **用户指南**：开场模板、Git 报错速查、协作流程、回归验证说明 | 用户不知道怎么操作时看 |
| `GAME_DESIGN.md` | **设计**：游戏概念、核心机制、美术/引擎选型 | 项目初期或迷失方向时看 |

---

## 8. AI 换号/新对话开场模板

> 用户每次开新对话，只需发送以下内容：

```text
GitHub Token: ghp_你的token
仓库：https://github.com/jiaxuGOGOGO/MarioTrickster

请先用 Token 克隆仓库，读取根目录的 SESSION_TRACKER.md 获取当前状态和待办。

本次任务：[填写你的测试反馈，或指定新的待办任务]

积分提醒：请在我积分接近300时暂停，优先更新 SESSION_TRACKER.md、Progress_Summary.md 和 MASTER_TRACKER.md 并推送。
```

> AI 收到后的标准动作：
> 1. 用 Token 配置 Git 并克隆仓库
> 2. 读取 `SESSION_TRACKER.md`（本文件），执行第 0 节的行为规范
> 3. 根据用户消息确定本次任务
> 4. 如果是 Bug 修复，按 0.2 节流程执行
> 5. 如果是新功能开发，按 `AI_WORKFLOW.md` 第 3 章流程执行
> 6. 推送前按 0.3 节检查文档更新
