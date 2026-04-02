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
| **编译状态** | ✅ 编译通过，测试 1-8 全部通过 |
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

## 3. 手动测试进度总览

| 测试项 | 状态 | 说明 |
|--------|------|------|
| 测试 1：Mario 基础移动 | ✅ 通过 | WASD 移动 + Space 跳跃 |
| 测试 2：Trickster 基础移动 | ✅ 通过 | 方向键移动 + 跳跃 |
| 测试 3：移动平台跟随 | ✅ 通过 | 站上平台不被甩飞 |
| 测试 4：伪装系统 | ✅ 通过 | P 伪装/解除，O/I 切换形态 |
| 测试 5：道具操控能力 | ✅ 通过 | Telegraph→Active→Cooldown 流程正常 |
| 测试 6：扫描技能 | ✅ 通过 | Q 键扫描，脉冲+文字提示正常 |
| 测试 6.5：镜头系统 | ✅ 通过 | 平滑跟随，无晃动 |
| 测试 7：胜负判定与UI | ✅ 通过 | 多回合胜利/失败画面正常显示 |
| 测试 8：暂停系统 | ✅ 通过 | ESC 暂停/恢复正常，无多余提示 |
| EditMode 自动化测试 | ⬜ 待运行 | 预期 59 个用例全部通过 |
| PlayMode 自动化测试 | ⬜ 待运行 | 预期 21 个用例全部通过 |

**手动测试进度：9/9 全部通过！剩余自动化测试待运行。**

---

## 4. 待办队列 (Backlog)

> AI 每次对话从队首取任务。所有 P0 Bug 已修复并验证通过。

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
| **P1** | — | **关卡设计系统 (Level Design)** | 未开始 |
| **P1** | — | **音效系统 (Audio)** | 未开始 |
| P2 | — | 动画系统完善 | 未开始 |
| P2 | — | 主菜单 UI | 未开始 |

---

## 5. 键位速查（避免混淆）

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

## 6. 文档职责导航（单一真相源）

| 文档 | 职责 | 谁看 |
|------|------|------|
| `SESSION_TRACKER.md` | **入口**：当前状态、测试进度、待办 | **AI 每次必读**，用户每次测试必填 |
| `MarioTrickster_Progress_Summary.md` | **存档**：功能清单、Bug 库、技术决策、文件结构 | AI 需要完整上下文时读 |
| `MarioTrickster_Testing_Guide.md` | **手册**：全量测试用例、键位表、UI 调试信息说明 | 用户测试/排查问题时看 |
| `AI_WORKFLOW.md` | **规范**：开场模板、Git 报错速查、协作流程 | 用户不知道怎么让 AI 干活时看 |
| `GAME_DESIGN.md` | **设计**：游戏概念、核心机制、美术/引擎选型 | 项目初期或迷失方向时看 |

---

## 7. AI 换号/新对话开场模板

> 用户每次开新对话，只需发送以下内容：

```text
GitHub Token: ghp_你的token
仓库：https://github.com/jiaxuGOGOGO/MarioTrickster

请先用 Token 克隆仓库，读取根目录的 SESSION_TRACKER.md 获取当前状态和待办。

本次任务：[填写你的测试反馈，或指定新的待办任务]

积分提醒：请在我积分接近300时暂停，优先更新 SESSION_TRACKER.md 和 Progress_Summary.md 并推送。
```
