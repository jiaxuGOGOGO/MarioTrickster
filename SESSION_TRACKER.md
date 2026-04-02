# MarioTrickster Session Tracker

> **AI 协作唯一入口文档**：AI 每次对话只需读取本文件，即可了解当前状态并进入反馈循环。

---

## 1. 当前状态总览

| 字段 | 值 |
|------|-----|
| **最新 Session** | Session 12 (B018 游戏结束UI修复) |
| **日期** | 2026-04-02 |
| **分支** | master |
| **项目阶段** | MVP 核心开发 (Sprint 1) |
| **编译状态** | 待验证 (B018修复已推送) |
| **阻塞问题** | 无 |

---

## 2. 测试反馈记录 (Session 12)

```text
测试日期：2026-04-02
测试人：AI (Manus)

修复项 B018（游戏结束UI未显示）：
- 根因：TestSceneBuilder 一键生成测试场景时未创建 GameUI 对象，
  导致场景中没有 GameUI 实例订阅 GameManager.OnGameOver 事件。
  mario.unity 手动场景中同样没有 GameUI 组件。
- 修复：在 TestSceneBuilder.cs 中新增 GameUI 对象创建（挂载到 Managers 对象下），
  GameUI.Start() 会自动查找 MarioController 并订阅 GameManager 事件。
- 状态：✅ 代码修复已完成，待用户在 Unity 中验证

附带更新：
- TestSceneBuilder.cs：修复 B016 源头问题，Build Test Scene 前先清理已有 CameraController，避免重复叠加导致镜头晃动
- TestSceneBuilder.cs：同样对 GameUI 添加重复清理保护
- Clear Test Scene：优化清理逻辑，先清理相机上所有 CameraController 再删除其他对象
- ComponentSetupTests.cs：新增 5 个 GameUI EditMode 测试用例
- MarioTrickster_Testing_Guide.md：更新测试 7 为胜负判定+UI显示，新增 B018 修复说明
```

---

## 3. 本次测试清单 (待用户测试)

> **请重新生成测试场景后测试以下项目。**
> 操作：MarioTrickster → Clear Test Scene → MarioTrickster → Build Test Scene → Ctrl+S 保存

### 测试项 A：B018 游戏结束UI显示

| 步骤 | 操作 | 预期结果 |
|------|------|----------|
| 1 | 控制 Mario (WASD) 走到右侧绿色方块 | Console 输出 "Mario 到达终点！触发胜利判定" |
| 2 | 观察屏幕 | 半透明黑色遮罩 + 红色横幅 + 黄色大字 "MARIO WINS!" + 比分 + 闪烁提示 |
| 3 | 按 R | 场景重启，胜利画面消失 |
| 4 | 按 F5 重启后，让 Mario 掉入深渊 | 蓝色横幅 + "TRICKSTER WINS!" |
| 5 | 按 N | 下一回合开始，回合数+1 |

### 测试项 B：暂停画面（附带验证）

| 步骤 | 操作 | 预期结果 |
|------|------|----------|
| 1 | 按 ESC | 半透明遮罩 + "PAUSED" 大字 |
| 2 | 再按 ESC | 恢复游戏，短暂显示 "RESUMED" |

### 测试项 C：HUD 显示（附带验证）

| 步骤 | 观察 | 预期结果 |
|------|------|----------|
| 1 | 左上角 | Mario HP: ♥ ♥ ♥ |
| 2 | 顶部居中 | 倒计时 02:00 |
| 3 | 右上角 | Round 1 | Mario 0 - Trickster 0 |

---

## 4. 反馈模板（测试后填写）

```text
测试日期：
测试人：

测试项 A（B018 游戏结束UI）：
- 走到绿色方块：[ 触发胜利判定 / 未触发 ]
- 胜利画面显示：[ 有半透明遮罩和大字 / 没有 ]
- 按 R 重启：[ 正常 / 异常 ]
- Trickster 胜利画面：[ 有 / 没有 ]

测试项 B（暂停画面）：
- ESC 暂停：[ 有遮罩和PAUSED / 没有 ]
- ESC 恢复：[ 有RESUMED提示 / 没有 ]

测试项 C（HUD显示）：
- 生命值：[ 正常显示 / 不显示 ]
- 倒计时：[ 正常显示 / 不显示 ]
- 回合信息：[ 正常显示 / 不显示 ]

新发现的问题/新需求：
- （如有请描述）
```

---

## 5. 待办队列 (Backlog)

> AI 每次对话从队首取任务。

| 优先级 | ID | 描述 | 状态 |
|--------|-----|------|------|
| P0 | B018 | **游戏结束UI未显示** | ✅ 已修复（根因：TestSceneBuilder 未创建 GameUI），待用户验证 |
| P0 | B016 | 镜头来回轻微晃动 | ✅ 已修复已验证（根因：CameraController重复添加，Session 12 从源头修复 TestSceneBuilder） |
| P0 | B015 | 扫描提示矛盾 Bug | ✅ 已修复已验证 |
| P0 | B017 | 终点无胜利判定 | ✅ 已修复已验证（逻辑通过，UI问题由B018解决） |
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
