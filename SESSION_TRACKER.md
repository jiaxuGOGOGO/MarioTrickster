# MarioTrickster Session Tracker

> **AI 协作唯一入口文档**：AI 每次对话只需读取本文件，即可了解当前状态并进入反馈循环。

---

## 1. 当前状态总览

| 字段 | 值 |
|------|-----|
| **最新 Session** | Session 12 (B018/B019/B020/B021 修复) |
| **日期** | 2026-04-02 |
| **分支** | master |
| **项目阶段** | MVP 核心开发 (Sprint 1) |
| **编译状态** | 待验证 (多项修复已推送) |
| **阻塞问题** | 无 |

---

## 2. 测试反馈记录 (Session 12)

```text
测试日期：2026-04-02
测试人：AI (Manus) + 用户反馈

=== 修复项 B018（游戏结束UI未显示）===
- 根因：TestSceneBuilder 未创建 GameUI 对象
- 修复：TestSceneBuilder 新增 GameUI 对象创建
- 状态：✅ 代码修复已完成

=== 修复项 B019（originalColor 序列化冲突）===
- 根因：ControllableBlock 和父类 ControllablePropBase 都声明了 private Color originalColor
- 修复：父类改为 protected，子类移除重复声明
- 状态：✅ 已修复

=== 修复项 B016 源头修复（CameraController 重复叠加）===
- 根因：TestSceneBuilder 每次 Build 都 AddComponent 不检查已有
- 修复：Build 前先清理已有的 CameraController 和 GameUI
- 状态：✅ 已修复

=== 修复项 B020（第二回合终点无反应）===
- 根因：GoalZone.triggered 在第一回合设为 true 后，ResetRound 未重置
- 修复：GoalZone 新增 ResetTrigger() 方法，GameManager.ResetRound() 调用它
- 状态：✅ 代码修复已完成，待用户验证

=== 修复项 B021（移除 RESUMED 恢复提示）===
- 用户反馈：暂停恢复后显示 "RESUMED" 没必要
- 修复：移除 GameManager 和 GameUI 中的恢复提示逻辑
- 状态：✅ 已修复

附带更新：
- GameManager.ResetRound()：新增 GoalZone 重置和 ControllablePropBase 重置
- TestSceneBuilder.cs：Build 前清理已有 CameraController/GameUI
- ControllablePropBase.cs：originalColor 改为 protected
- ControllableBlock.cs：移除重复 originalColor
- ComponentSetupTests.cs：新增 5 个 GameUI EditMode 测试用例
```

---

## 3. 本次测试清单 (待用户测试)

> **请重新生成测试场景后测试以下项目。**
> 操作：MarioTrickster → Clear Test Scene → MarioTrickster → Build Test Scene → Ctrl+S 保存

### 测试项 A：多回合终点判定（B020 修复验证）

| 步骤 | 操作 | 预期结果 |
|------|------|----------|
| 1 | 控制 Mario 走到右侧绿色方块 | "MARIO WINS!" 胜利画面 |
| 2 | 按 N 进入下一回合 | 回合数变为 2，角色回到出生点 |
| 3 | 再次控制 Mario 走到绿色方块 | **再次**显示 "MARIO WINS!" 胜利画面 |
| 4 | 按 N 进入第三回合 | 回合数变为 3 |
| 5 | 让 Mario 掉入深渊 | "TRICKSTER WINS!" 画面 |

### 测试项 B：暂停功能（B021 验证）

| 步骤 | 操作 | 预期结果 |
|------|------|----------|
| 1 | 按 ESC | 半透明遮罩 + "PAUSED" 大字 |
| 2 | 再按 ESC | 直接恢复游戏，**不再显示** "RESUMED" |

### 测试项 C：序列化错误消除（B019 验证）

| 步骤 | 观察 | 预期结果 |
|------|------|----------|
| 1 | Console 面板 | **不再**出现 "The same field name is serialized multiple times" 红色错误 |

### 测试项 D：CameraController 不重复（B016 验证）

| 步骤 | 操作 | 预期结果 |
|------|------|----------|
| 1 | 连续运行 Build Test Scene 2次 | Main Camera Inspector 中始终只有 **1个** CameraController |

---

## 4. 反馈模板（测试后填写）

```text
测试日期：
测试人：

测试项 A（多回合终点判定）：
- 第一回合到终点：[ 有胜利画面 / 没有 ]
- 按 N 后第二回合到终点：[ 有胜利画面 / 没有 ]
- 第三回合 Trickster 胜利：[ 有 / 没有 ]

测试项 B（暂停功能）：
- ESC 暂停：[ 有遮罩和PAUSED / 没有 ]
- ESC 恢复：[ 直接恢复无RESUMED / 仍有RESUMED ]

测试项 C（序列化错误）：
- Console 红色错误：[ 已消除 / 仍有 ]

测试项 D（CameraController）：
- 多次 Build 后只有1个：[ 是 / 否 ]

新发现的问题/新需求：
- （如有请描述）
```

---

## 5. 待办队列 (Backlog)

> AI 每次对话从队首取任务。

| 优先级 | ID | 描述 | 状态 |
|--------|-----|------|------|
| P0 | B020 | **第二回合终点无反应** | ✅ 已修复（GoalZone.triggered 未重置），待用户验证 |
| P0 | B021 | **RESUMED 恢复提示多余** | ✅ 已修复（移除恢复提示逻辑） |
| P0 | B019 | originalColor 序列化冲突 | ✅ 已修复（子类重复声明） |
| P0 | B018 | 游戏结束UI未显示 | ✅ 已修复（TestSceneBuilder 未创建 GameUI） |
| P0 | B016 | 镜头来回轻微晃动 | ✅ 已修复已验证（CameraController 重复添加，已从源头修复） |
| P0 | B015 | 扫描提示矛盾 Bug | ✅ 已修复已验证 |
| P0 | B017 | 终点无胜利判定 | ✅ 已修复已验证 |
| P0 | UI | Trickster状态文字被裁剪 | ✅ 已修复已验证 |
| P1 | — | 关卡设计系统 (Level Design) | 未开始 |
| P1 | — | 音效系统 (Audio) | 未开始 |
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
| 回合结束 | R | 重启关卡 |
| 回合结束 | N | 下一回合 |

---

## 7. 文档职责导航（单一真相源）

| 文档 | 职责 | 谁看 |
|------|------|------|
| `SESSION_TRACKER.md` | **入口**：当前状态、本次测试项、反馈模板、待办 | **AI 每次必读**，用户每次测试必填 |
| `MarioTrickster_Progress_Summary.md` | **存档**：功能清单、Bug 库、技术决策、文件结构 | AI 需要完整上下文时读 |
| `MarioTrickster_Testing_Guide.md` | **手册**：全量测试用例、键位表、UI 调试信息说明 | 用户测试/排查问题时看 |
| `AI_WORKFLOW.md` | **规范**：开场模板、Git 报错速查、协作流程 | 用户不知道怎么让 AI 干活时看 |
| `GAME_DESIGN.md` | **设计**：游戏概念、核心机制、美术/引擎选型 | 项目初期或迷失方向时看 |

---

## 8. AI 换号/新对话开场模板

> 用户每次开新对话，只需发送以下内容：

```text
GitHub Token: ghp_你的token
仓库：https://github.com/jiaxuGOGOGO/MarioTrickster

请先用 Token 克隆仓库，读取根目录的 SESSION_TRACKER.md 获取当前状态和待办。

本次任务：[填写你的测试反馈，或指定新的待办任务]

积分提醒：请在我积分接近300时暂停，优先更新 SESSION_TRACKER.md 和 Progress_Summary.md 并推送。
```
