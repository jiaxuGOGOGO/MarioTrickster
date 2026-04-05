# MarioTrickster Session Tracker

> **AI 协作唯一入口文档**：新对话**只需读取本文件**即可无缝衔接。

---

## 0. 敏捷协作新规（S23 起生效，最高优先级）

> **核心理念**：文档更新不创造价值，代码才创造价值。把算力集中在发现问题和解决问题上。

### 第一层：日常必做（耗能 < 5%）

每次 `git push` 前，**仅更新本文件**的以下三处，绝不触碰其他任何长文档：
1. **§1 状态总览** — 更新 Session 号和一句话描述
2. **§3 回归标记** — 受影响的测试项打 🔄
3. **§4 待办队列** — 更新任务状态

防回归检查全面交给本地 **114 个自动化测试**兜底，不再手动维护复杂交叉验证表格。

### 第二层：Git 替代历史，代码即文档

- 历史记录全靠**详细的 Git Commit Message**，不再写 Session 历史到 Progress_Summary
- 新对话主动通过 `git log --oneline -n 10` 回溯近期变更
- 需要深入了解某次修改时，用 `git show <hash>` 查看具体 diff

### 核心防坑机制：`// [AI防坑警告]` 源码注释

对于容易被新对话改坏的核心底层逻辑，**必须直接在 `.cs` 源码对应方法上方写中文警告注释**。格式：

```csharp
// [AI防坑警告] 这里是两段式弹射的核心逻辑，不要用 bounceStunTimer 替代。
// 蓄力冻结期(_isPreparingBounce)和飞行期(_isBouncing)必须分开处理。
// 如果合并会导致 HandleDirection 的 maxSpeed 截断在冻结期就生效，破坏抛物线。
// 修改前请先理解完整的弹射流程：碰撞→PrepareBounce()→冻结→ExecuteBounce()→飞行→落地解除
```

这是**跨对话防退化的最强武器**，比任何文档都可靠，因为 AI 修改代码时一定会读到它。

### 第三层：集中归档（仅限用户指令触发）

只有当用户明确发送 **"执行文档大同步"** 时，才去全量更新以下文档：
- `MarioTrickster_Progress_Summary.md`
- `MASTER_TRACKER.md`
- `MarioTrickster_Testing_Guide.md`

**日常开发中绝对不碰这三个文件。**

### 性能编码自检（P1-P7，保留但精简）

每次写新代码时脑中过一遍，推送前跑一次 grep：

| 编号 | 一句话规则 |
|------|-----------|
| P1 | OnGUI 中禁止 `new GUIStyle`，用类字段惰性初始化 |
| P2 | Update/FixedUpdate 中禁止 `FindObjectsOfType`，缓存引用 |
| P3 | OnRenderObject 必须 `if (Camera.current != Camera.main) return` |
| P4 | 每帧方法中禁止 `new Material`，Awake 中创建 |
| P5 | Update 中禁止无限制 `Instantiate`，用对象池 |
| P6 | while 循环必须有明确退出条件，协程中必须有 yield |
| P7 | OnGUI 中避免全屏 GUI.DrawTexture，用 Canvas UI |

```bash
# 推送前一键自检
grep -rn 'new GUIStyle' Assets/Scripts/ | grep -v '// cached\|InitStyles'
grep -rn 'FindObject' Assets/Scripts/ | grep -v 'Awake\|Start\|//'
grep -rn 'new Material' Assets/Scripts/ | grep -v 'Awake\|Start\|Setup'
grep -rn 'Instantiate' Assets/Scripts/ | grep -v 'Awake\|Start\|Build\|Create\|Setup'
```

### 其他保留规则（精简版）

- **Session 编号**：产生代码修改时 +1，仅回答问题不加
- **Git 规范**：master 分支，英文 commit，首行概述 + 空行 + 详细列表
- **积分管理**：接近阈值时立即暂停，优先更新本文件并推送
- **TestSceneBuilder 同步**：新增/修改可见行为时，同一 commit 中更新对应 Stage 标签

---

## 1. 当前状态总览

| 字段 | 值 |
|------|-----|
| **最新 Session** | Session 26b (Level Studio 精简为纯本地三合一) |
| **日期** | 2026-04-05 |
| **分支** | master |
| **阶段** | Sprint 2 游戏体验提升 |
| **编译状态** | ⚠️ S26b 精简后待用户 Unity 验证 |
| **阻塞** | 无 |
| **交接说明** | S26b 精简 Level Builder Tab：删除 LevelImageAnalyzer.cs（过度设计），重写为纯本地三合一（字典速查 + 5个经典片段追加 + 文本框编辑/Build）。图片识别留在外部 AI 聊天框。接班 AI 请先 `git log --oneline -n 5`。 |

---

## 2. 回归验证清单

> 用户测试时逐项快速验证。AI 修复代码后只需在此标记受影响项。

| 状态 | 测试项 | 关键验证点 |
|:----:|--------|-----------|
| 🔄 | 测试 1：Mario 基础移动 | WASD + Space，手感不变，单独按S不触发下落 |
| 🔄 | 测试 2：Trickster 移动 | 方向键移动正常；融入后方向键切换目标不移动 |
| ✅ | 测试 3：移动平台 | 站上不被甩飞 |
| ✅ | 测试 4：伪装系统 | P 伪装/解除，O/I 切换 |
| 🔄 | 测试 5：道具操控 | 融入后红/灰连线；方向键磁吸切换；L 触发红线目标 |
| ✅ | 测试 6：扫描技能 | Q 键脉冲+文字正常 |
| 🔄 | 测试 6.5：镜头 | 走完全部 Stage 镜头始终跟随 |
| 🔄 | 测试 7：胜负判定 | 碰敌有击退 + 终点有胜利画面 |
| ✅ | 测试 8：暂停 | ESC 暂停/恢复 |
| 🔄 | 测试 9A：地刺 | 碰到有合理击退 |
| 🔄 | 测试 9B：摆锤 | Trickster L键可控制 |
| 🔄 | 测试 9C：火陷阱 | 碰到向后退，不向上飞 |
| 🔄 | 测试 9D：弹跳怪 | 碰到有击退，踩踏消灭 |
| 🔄 | 测试 9E：弹跳平台 | **S22核心**：冻结~0.25s → 完整抛物线 → 空中可微弱转向 → 落地/碰墙恢复 |
| 🔄 | 测试 9F：单向平台 | S+Space 下落，单独S不落 |
| 🔄 | 测试 9G：崩塌平台 | 重生在新位置 + Trickster可触发 |
| 🔄 | 测试 9H：隐藏通道 | 双向穿越 + 冷却时间 |
| 🔄 | 测试 9I：伪装墙 | 走入变透明 + L键变实体 |
| 🔄 | 场景生成 | Clear + Build 后标签正常 |
| ✅ | EditMode 自动化 | 59/59 通过 |
| ✅ | PlayMode 自动化 | 21/21 通过 |

---

## 3. 自动化测试（安全网）

- **EditMode**: 59 个（结构/静态逻辑验证）
- **PlayMode**: 21 个（运行时行为验证）
- **运行方式**: `MarioTrickster → Run Tests → Export Full Report (All)` 导出到 `TestReport.txt`
- **总计 114 个测试**（含 34 个 Prefab/资源检查）作为防回归兜底

---

## 4. 待办队列

> 用户消息中指定的任务优先级最高。

| 优先级 | 描述 | 状态 |
|--------|------|------|
| **紧急** | S26b Level Studio 精简 (删除AI分析器，重写为纯本地三合一) | ✅ 已完成，待用户 Unity 验证 |
| **紧急** | 等待用户 Unity 测试 S22 弹跳平台重构结果 | ✅ 代码已推送，待用户反馈 |
| **P1** | 关卡设计系统完善 | ✅ Level Studio 已交付 |
| **P1** | 音效系统 (Audio) | 未开始 |
| P2 | 动画系统完善 | 未开始 |
| P2 | 主菜单 UI | 未开始 |

---

## 5. 键位速查

| 角色 | 按键 | 功能 |
|------|------|------|
| Mario | WASD | 移动 |
| Mario | Space | 跳跃 |
| Mario | S | 隐藏通道传送（双向） |
| Mario | S+Space | 单向平台下落 |
| Mario | Q | 扫描 |
| Trickster | 方向键 | 移动（融入时=磁吸切换目标） |
| Trickster | 上/右Ctrl | 跳跃 |
| Trickster | P | 伪装/解除 |
| Trickster | O/I | 切换伪装形态 |
| Trickster | L | 操控道具 |
| 全局 | ESC | 暂停/恢复 |
| 全局 | F5 | 快速重启 |
| 全局 | Ctrl+T | 打开 Test Console 窗口 |
| 全局 | F9 | 无冷却模式（调试） |
| 回合结束 | R/N | 重启/下一回合 |

---

## 6. 文档导航

| 文档 | 一句话职责 |
|------|-----------|
| **SESSION_TRACKER.md**（本文件） | AI 入口：状态 + 规范 + 回归 + 待办 |
| MASTER_TRACKER.md | 全局总览：设计愿景 vs 代码实现映射 |
| Progress_Summary.md | 存档：功能清单 + Bug库 + 文件树 |
| Testing_Guide.md | 手册：测试用例 + 影响矩阵 |
| AI_WORKFLOW.md | 用户指南：开场模板 + Git速查 |
| GAME_DESIGN.md | 游戏设计文档 |

---

## 7. 新对话开场模板

```text
GitHub Token: ghp_你的token
仓库：https://github.com/jiaxuGOGOGO/MarioTrickster

请先用 Token 克隆仓库，读取根目录的 SESSION_TRACKER.md 获取当前状态和待办。

本次任务：[你的需求]

积分提醒：请在我积分接近300时暂停，优先更新 SESSION_TRACKER.md 并推送。
```

> AI 收到后：克隆 → 读本文件 → `git log --oneline -n 10` 回溯近期变更 → 执行任务 → 推送前只更新本文件

## [2026-04-04] Level Studio 教程与关卡设计指南
- 编写了详尽的 `LevelStudio_DesignGuide.md`，包含 Test Console 的完整使用教程。
- 搜集并整理了基于 GMTK 四步法和 Celeste 关卡设计模式的优秀参考资源。
- 对比经典平台跳跃游戏，识别并列出了项目中缺失的高/中/低优先级游戏要素（如传送带、旋转锯片、飞行敌人等）。
- 提供了基于现有框架快速扩展新要素的工作流建议。

### Element Palette 生成位置修复
- 修复 `TestConsoleWindow.SpawnElementAtSceneCenter`：将 `sceneView.camera.transform.position` 替换为 `sceneView.pivot`
- 原因：camera.transform.position 是 Scene 摄像机的 3D 位置（含透视偏移），在 2D 模式下与画面可视中心存在较大偏差
- 效果：Element Palette 生成的元素现在准确出现在 Scene 视图画面中心

## [2026-04-05] S26b: Level Studio 精简为纯本地三合一

### 减法操作
- **删除** `Assets/Scripts/Editor/LevelImageAnalyzer.cs` — 过度设计，图片识别留在外部 AI 聊天框
- **精简** `Assets/Scripts/Editor/LevelSnippetLibrary.cs` — 从 15+ 片段精简为 5 个核心经典片段
- **重写** TestConsoleWindow 中 S26 区块 — 删除 AI Analyzer / Missing Browser / 拼接器等复杂 UI

### 保留的三合一功能 (DrawCustomTemplateSection)
1. **字典速查表** — 内嵌 18 种字符映射参考（#=地面 ^=地刺 E=弹跳怪 等），始终可见
2. **经典片段库** — 5 个预设片段（教学平台 / 弹跳深渊 / 陷阱走廊 / 敌人遇战 / 综合挑战），点击「追加到文本框」像搞积木一样拼装
3. **文本框 + Build** — 粘贴外部 AI 生成的 ASCII / 手动编写 / 片段追加，一键「Build From Text」生成关卡

### 设计原则
- 纯本地功能，零外部依赖，不需要 API Key
- 所有新功能仅在 EditMode 下可用
- 工作流：外部 AI 聊天框识别图片 → 复制 ASCII → 粘贴到文本框 → Build
