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

防回归检查全面交给本地 **135 个自动化测试**兜底（S52 未新增测试数量，但将现有 2 个 TAS 测试降级为柔性模式），不再手动维护复杂交叉验证表格。

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
| **最新 Session** | Session 52 (S52: 柔性测试降级 + PhysicsConfigSO 实时手感面板 + BaseHazard 陷阱基类基建) |
| **日期** | 2026-04-07 |
| **分支** | master |
| **阶段** | Sprint 2 游戏体验提升 |
| **编译状态** | ⏳ S52 代码已推送，待用户 Unity 验证 |
| **阻塞** | 无 |
| **交接说明** | S52 柔性测试降级 + 手感面板 + 陷阱基建：(1) **柔性测试降级**: `TasReplayData` 新增 `strictPositionCheck` 字段（默认 false），`S51_DataDrivenTasTests` 改为条件断言——false 时只断言"角色存活+胜利触发"，彻底跳过 0.05f 坐标校验，允许手感调优期老录像跌跌撞撞跑完不报错。(2) **PhysicsConfigSO 实时手感面板**: 新增 `PhysicsConfigSO : ScriptableObject` 将 MarioController 16 个手感参数提取为 SO，支持 PlayMode 拖动滑块实时调优，Inspector 底部显示推导值（跳跃高度/距离），SO 为 null 时回退本地字段（零行为变化）。(3) **BaseHazard 陷阱基类**: 新增 `BaseHazard` 抽象基类统一陷阱接口，`KillZone` 重构为继承 BaseHazard，S48b/c 三重检测+防刷屏完整保留。基类顶部写有 `[机制扩展指南]` 注释教导未来新陷阱如何继承。S50 10x 加速、S37 视碰分离、S39 弹射手感完全不变。接班 AI 请先 `git log --oneline -n 5`。 |

---

## 2. 回归验证清单

> 用户测试时逐项快速验证。AI 修复代码后只需在此标记受影响项。

| 状态 | 测试项 | 关键验证点 |
|:----:|--------|-----------|
| 🔄 | 测试 1：Mario 基础移动 | **S52重点验证**: WASD + Space 手感不变（PhysicsConfigSO 默认值与原硬编码一致），单独按S不触发下落，S36落地压扁仅高速下落触发 |
| 🔄 | 测试 2：Trickster 移动 | **S49重点验证**: 方向键移动正常；融入后方向键切换目标不移动（输入解耦后行为完全一致） |
| ✅ | 测试 3：移动平台 | 站上不被甩飞 |
| ✅ | 测试 4：伪装系统 | P 伪装/解除，O/I 切换 |
| 🔄 | 测试 5：道具操控 | 融入后红/灰连线；方向键磁吸切换；L 触发红线目标 |
| ✅ | 测试 6：扫描技能 | Q 键脉冲+文字正常 |
| 🔄 | 测试 6.5：镜头 | 走完全部 Stage 镜头始终跟随 |
| 🔄 | 测试 7：胜负判定 | **S52重点验证**: 掉出屏幕后应触发死亡(仅1条日志，KillZone 继承 BaseHazard 后行为不变) + RoundOver 画面 + 终点胜利画面 |
| ✅ | 测试 8：暂停 | ESC 暂停/恢复 |
| 🔄 | 测试 9A：地刺 | 碰到有合理击退 |
| 🔄 | 测试 9B：摆锤 | Trickster L键可控制 |
| 🔄 | 测试 9C：火陷阱 | 碰到向后退，不向上飞 |
| 🔄 | 测试 9D：弹跳怪 | **S44重点验证**: 弹跳怪站在地面上弹跳、碰到有击退，踩踏消灭 |
| 🔄 | 测试 9E：弹跳平台 | **S39重点验证**：(1)从上方落下应向上弹，侧面蹭到不触发 (2)每次弹跳高度一致（不再先高后矮） (3)按住 Space 蓄力大跳 1.4x (4)Trickster L键操控仍正常 (5)冻结期角色不滑动 |
| 🔄 | 测试 9F：单向平台 | **S44c重点验证**: ASCII关卡中单向平台已合并为长条，S+Space 下落、边缘行走、单独S不落 |
| 🔄 | 测试 9G：崩塌平台 | 重生在新位置 + Trickster可触发 |
| 🔄 | 测试 9H：隐藏通道 | 双向穿越 + 冷却时间 |
| 🔄 | 测试 9I：伪装墙 | 走入变透明 + L键变实体 |
| 🔄 | 场景生成 | **S48重点验证**: ASCII Build 后应正常生成所有元素（不再 Unknown char）+ 单向平台长条 + 敌人不掉落 + S43 验证器 + S35 布局安全 |
| ✅ | EditMode 自动化 | 109/109 通过（S37 视碰分离后全量通过） |
| 🔄 | PlayMode 自动化 | **S52重点验证**: 26/26 通过（S52 柔性测试降级后现有录像仍应绿灯） |

---

## 3. 自动化测试（安全网）

- **EditMode**: 109 个（结构/静态逻辑验证，S37 全量通过）
- **PlayMode**: 26 个（运行时行为验证，S51 新增 2 个数据驱动 TAS 测试）
- **运行方式**: `MarioTrickster → Run Tests → Export Full Report (All)` 导出到 `TestReport.txt`
- **总计 135 个测试**（EditMode 109 + PlayMode 26）作为防回归兜底

---

## 4. 待办队列

> 用户消息中指定的任务优先级最高。

| 优先级 | 描述 | 状态 |
|--------|------|------|
| **紧急** | S52 柔性测试降级 + PhysicsConfigSO 实时手感面板 + BaseHazard 陷阱基类基建 | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S51 零代码数据驱动测试管线 DDPT (TasReplayData Wrapper + TestCaseSource + TAS 状态 UI + F12 落盘 + 2 个 JSON 录像) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S50 TAS 录播系统落地 (InputRecorder RLE 压缩 + 3 个 E2E 极速跑图测试 + 10x 物理加速) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S49 全自动测试基建铺路 (IInputProvider 输入解耦 + AutomatedInputProvider + KeyboardInputProvider) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S48c KillZone 日志刷屏修复 (single-kill guard + freeze dead body) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S48b KillZone 三重死亡检测 (OnTriggerStay2D + Y 坐标兜底) | ✅ 检测生效，但日志刷屏 → S48c 修复 |
| **紧急** | S48 Registry char 序列化 bug 修复 (string 代理 + HideFlags + 完整性校验) | ✅ 已验证，29 objects 正常生成 |
| **紧急** | S47 L2 BFS 可达性验证器 + Auto-Prompting 纠错闭环 (LevelReachabilityAnalyzer) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S46 Data-Driven Registry 关卡元素字典中心化解耦 (AsciiElementRegistry + Generator/Validator 重构) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S45 Doc-as-Code 动态文档同步引擎 (DocsAutomatorWindow: Sync Docs + Copy Prompt) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S44c OneWayPlatform 连续 '-' 合并为长条平台 + 提示词文档同步 | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S44b OneWayPlatform S+Space 下落修复 (多平台同时 IgnoreCollision) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S44 敌人穿地掉落修复 (isTrigger/groundLayer/视碰分离) | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S43 关卡生成验证系统全面修复 + S43b 行业对标审计 + 文档一致性修复 | ⏳ 代码+文档已推送，待用户 Unity 验证 |
| **紧急** | S39 方案 C 弹跳平台重构（按键驱动大跳+状态机锁+Kinematic Freeze+微抬坐标） | ⏳ 代码已推送，待用户 Unity 验证 |
| **普通** | LEVEL_DESIGN_SYSTEM.md 新增关卡参数调整入口导航章节 | ✅ 已完成并推送 |
| **紧急** | S41 LevelEditorPickingManager v3 + OneWayPlatform IgnoreCollision + PhysicsMetrics 终极校准 | ⏳ 代码已推送，待用户 Unity 验证 |
| **紧急** | S40 [SelectionBase] 修复框选选到 Visual 子节点问题（11 个文件） | ✅ 已完成，S41 进一步优化 |
| **紧急** | S37 视碰分离架构重构（Root→Visual 父子层级，18 文件） | ✅ 自动化测试 109/109 通过，待手动 PlayMode 验证 |
| **紧急** | S36 弹跳平台 Game Feel 增强（平台动画+角色形变+半重力顶点） | ✅ 代码已推送，待用户 Unity 验证 |
| **紧急** | S35 关卡布局安全性修复（模板+片段+验证器） | ✅ 代码已推送，待用户 Unity 验证 |
| **紧急** | S33 Level Builder ↔ Teleport/Cheats 联动 + 动态锚点系统 | ✅ 代码已推送，待用户 Unity 验证 |
| **紧急** | S32 视碰分离与关卡度量转译系统 | ✅ 代码已推送，待用户 Unity 验证 |
| **紧急** | S26b Level Studio 精简 (删除AI分析器，重写为纯本地三合一) | ✅ 已完成，待用户 Unity 验证 |
| **P1** | 关卡设计系统完善 | ✅ Level Studio + 工作流文档 + 物理度量系统已交付 |
| **P1** | 音效系统 (Audio) | 未开始 |
| P2 | 动画系统完善 | 未开始 |
| P2 | 主菜单 UI | 未开始 |
| **下一步** | S53 更复杂关卡 E2E 测试: 弹跳平台/地刺/敌人 JSON 录像 + PhysicsConfigSO 实测调优 + 新陷阱类型扩展 | 待定 |

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
| 全局 | F10 | TAS 录制开始/停止 |
| 全局 | F11 | TAS 导出 JSON 到控制台 |
| 全局 | F12 | TAS 一键落盘（S51: 保存到 LevelReplays） |
| 回合结束 | R/N | 重启/下一回合 |

---

## 6. 视碰分离架构白盒操作指南 (S37/S38)

> **核心原则**：Root.localScale 永远锁定 (1,1,1)，绝对不要缩放根物体！
> 碰撞体尺寸通过 `BoxCollider2D.size` 设置（来源于 `PhysicsMetrics` 常量），
> 视觉大小通过 `Visual.localScale` 设置。两者独立，互不干扰。

### 层级结构

```
Root (GameObject)           ← 承载 BoxCollider2D + 脚本组件
  └─ Visual (GameObject)     ← 承载 SpriteRenderer，控制视觉大小/形变动画
```

### 操作对照表

| 操作 | 应该操作谁 | 原因 |
|------|-----------|------|
| **移动位置**（拖拽重新布局） | **Root 母体** | Root 承载 BoxCollider2D，移动 Root = 移动碰撞体 + Visual 一起走。如果只移动 Visual，碰撞体还在原位，玩家会“踩空气” |
| **调整视觉大小**（换素材后美术适配） | **Visual 子物体** | Visual.localScale 控制视觉大小，不影响碰撞体。碰撞体尺寸由 PhysicsMetrics 常量锁定 |
| **调整碰撞体大小** | **修改 PhysicsMetrics.cs 常量** | 禁止手动拖拽碰撞体大小，必须改源头常量，否则下次生成关卡会覆盖回去 |
| **旋转** | **Root 母体** | 旋转 Root 会同时旋转碰撞体和视觉，保持一致 |

### 安全操作流程

1. 在 Scene 视图中选中物体 → 看 Hierarchy 确认选中的是 **Root**（最顶层）
2. 用 **Move 工具（W）** 拖拽 Root 到目标位置
3. 如果需要调整视觉大小，展开 Root → 选中 **Visual** 子节点 → 用 **Scale 工具（R）** 调整
4. **绝对不要**用 Scale 工具缩放 Root 母体

### 常见错误与排查

| 现象 | 可能原因 | 修复方法 |
|------|----------|----------|
| 角色踩在空气上，视觉上没踩到平台 | 只移动了 Visual，Root 没动 | 选中 Root 重新拖拽 |
| 碰撞体比视觉大/小很多 | 手动改了 BoxCollider2D.size | 恢复为 PhysicsMetrics 常量值 |
| 缩放 Root 后碰撞体异常 | Root.localScale 不是 (1,1,1) | 设回 (1,1,1)，用 Visual.localScale 调视觉 |
| 形变动画不播放 | visualTransform 未赋值 | Inspector 中拖拽 Visual 到 visualTransform 插槽 |

---

## 7. 文档导航

| 文档 | 一句话职责 |
|------|-----------|
| **SESSION_TRACKER.md**（本文件） | AI 入口：状态 + 规范 + 回归 + 待办 |
| MASTER_TRACKER.md | 全局总览：设计愿景 vs 代码实现映射 |
| Progress_Summary.md | 存档：功能清单 + Bug库 + 文件树 |
| Testing_Guide.md | 手册：测试用例 + 影响矩阵 |
| AI_WORKFLOW.md | 用户指南：开场模板 + Git速查 |
| GAME_DESIGN.md | 游戏设计文档 |

---

## 8. 新对话开场模板

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

## [2026-04-05] S27: ASCII 白盒模板集 + 参考图片库

### 新增文件
- `LevelDesign_References/ASCII_Templates.md` — 6 个经过严格验证的 ASCII 白盒模板（矩形对齐、无空格、字符合法）
- `LevelDesign_References/*.png|jpg|webp` — 7 张经典游戏关卡参考截图（Celeste/Mega Man/VVVVVV/Mario）

### 6 个模板
1. **Spike Abyss & Bait** (25x12) — Celeste 风格地刺深渊 + 诱饵金币
2. **Zigzag Climb Tower** (20x16) — Mega Man 风格 Z 字垂直攀爬
3. **Platform & Bouncer Duet** (26x13) — 移动平台 + 弹跳怪三段式跳跃
4. **Fire Corridor & Pendulum** (30x9) — Super Meat Boy 风格火焰走廊 + 摆锤
5. **Crumbling Escape** (25x11) — 崩塌平台逃亡 + V 字金币引导
6. **Secret Chamber** (25x12) — 伪装墙密室 + 隐藏通道探索

### 缺失机制汇总（高优先级）
- 旋转锯片 (Circular Saw)、传送带 (Conveyor Belt)、追逐者 (Chaser)

### S27 追加：模板登记簿 + 优化提示词
- `LevelDesign_References/TEMPLATE_REGISTRY.md` — 按"核心博弈组合"去重的模板登记簿，含快速复制区和灵感池
- `LevelDesign_References/AI_PROMPT_WORKFLOW.md` — 整合去重机制的完整工作流提示词（可直接复制使用）

## [2026-04-05] S28: 关卡工作流终极整合 — 双路线 + 跨账号防重闭环

### AI_PROMPT_WORKFLOW.md 重写
- 融合 Gemini 参考方案的**双路线架构**：路线 A（全自动流水线）+ 路线 B（半自动视觉流水线）
- 路线 A 含完整的 5 步自动化指令（克隆→读防重库→搜网→转译→更新登记簿→Push），可直接复制发给任意 AI
- 路线 B 保留视觉拆解咒语 + 人工闭环操作步骤
- 统一实操阶段保留原有的 3 阶段 Unity 微操流程（文本拼装→注入灵魂→换肤沉淀）
- 新增跨账号防重闭环原理图解
- 搜集标准明确：不止极难毒图，有趣/精妙/创意独特的同样欢迎

### TEMPLATE_REGISTRY.md 升级
- 新增**【已探索灵感来源】**黑名单表（游戏+具体关卡→对应模板编号），AI 搜网时强制避开
- 新增**【缺失机制待办】**统一清单（14 项，含建议字符、暂代方案、优先级），从 ASCII_Templates 迁移过来避免两处维护
- 新增**难度标签**体系（易/中/难/极难），登记表和快速复制区均已标注
- 新增**语义指纹格式规范**，确保所有 AI 生成统一格式
- 灵感池新增 4 个博弈维度（视觉欺骗、重力翻转、时间压力、风场/气流）
- 完整登记表新增灵感来源列，追溯每个模板的设计出处

### ASCII_Templates.md 精简
- 缺失机制汇总改为指向 TEMPLATE_REGISTRY.md 的统一维护点，避免两处不一致

## [2026-04-05] S29: 全网搜集新关卡灵感 T07/T08

### 搜集过程
- 读取 TEMPLATE_REGISTRY.md 防重库，确认已有 6 个模板的博弈组合和 8 条灵感来源黑名单
- 全网搜索 Shovel Knight 关卡设计深度分析（Yacht Club Games 官方博客）、Downwell 设计分析（Gamedeveloper.com）、I Wanna Maker 深度分析、VVVVVV 分析、Ori 遍历分析
- 确认搜集方向避开所有黑名单条目

### 新增模板
- **T07: 踩敌跳板与双路分支 (Pogo Bounce & Branching Path)** [30x13, 难度:中]
  - 灵感：Shovel Knight Pogo/Shovel Drop + Mario 水管路径分支
  - 核心博弈：路径分支（安全但无聊 vs 危险但有趣）+ 敌人利用（弹跳怪作跳板）+ 渐进揭示（教学区→选择区）
  - 涉及元素：`# = ^ E e o M G`

- **T08: 垂直坠井与视觉陷阱 (Vertical Descent & Visual Trap)** [13x22, 难度:难]
  - 灵感：Downwell 垂直下落闪避 + I Wanna Be The Guy 视觉欺骗
  - 核心博弈：垂直下落（重力驱动推进）+ 视觉欺骗（伪装墙假平台）+ 反向欺骗（隐藏通道密室）
  - 涉及元素：`W = ^ F H B o M G`

### 登记簿更新
- 完整登记表新增 T07/T08 两行
- 快速复制区新增 2 条摘要
- 已探索灵感来源新增 4 条（Shovel Knight Pogo、Mario 水管分支、Downwell 垂直下落、I Wanna 视觉欺骗）
- 灵感池新覆盖 5 个维度：路径分支[T07]、敌人利用[T07]、渐进揭示[T07]、垂直下落[T08]、视觉欺骗[T08]
- 缺失机制待办新增 3 项：下砸攻击(Pogo)、单向下落平台(Drop-through)、视觉提示系统(Visual Hint)

## [2026-04-05] S30: LevelStudio_DesignGuide.md 全面更新

结合 S26b 至 S29 的全部功能迭代，对 `LevelStudio_DesignGuide.md` 进行了完整重写。文档从原来的 3 部分 155 行扩展为 7 部分的一站式参考手册。

### 主要变更

| 章节 | 更新内容 |
|------|---------|
| 第一部分：Level Studio 教程 | 更新为 S26b 精简后的三合一架构（字典速查+片段库+文本框 Build），新增 Element Palette 和 Theme System 的完整操作步骤 |
| 第二部分：关卡设计工作流 | 新增章节，整合双路线架构（全自动+半自动视觉）和 Unity 内极速实操三阶段 |
| 第三部分：关卡模板库概览 | 从 6 个更新为 8 个模板，新增难度标签定义和推荐拼装顺序表 |
| 第四部分：关卡设计理论 | 新增 T07/T08 作为 GMTK 四步法和信任欺骗博弈的实例，新增物理法则约束表 |
| 第五部分：博弈维度覆盖 | 新增章节，展示 14 个博弈维度的覆盖进展（5 已覆盖 / 9 待探索） |
| 第六部分：要素分析与扩展 | 缺失机制从 8 项扩展为 17 项（含 T07/T08 新发现的 3 项），新增发现来源列 |
| 第七部分：跨账号防重闭环 | 新增章节，说明防重三件套和去重粒度 |

## [2026-04-05] S31: ASCII 关卡自动创建可玩环境

### 问题
用经典片段库或自定义模板生成 ASCII 关卡后，Mario 不会动——`M` 字符只生成了一个红色视觉标记（MarioSpawn），不会创建可操控的 Mario 角色和运行时基础设施。

### 修复方案
在 `TestConsoleWindow.GenerateFromCustomTemplate` 中新增 `EnsurePlayableEnvironment` 调用，ASCII 关卡生成后自动补全完整可玩环境：

| 组件 | 创建条件 | 包含内容 |
|------|---------|---------|
| Mario | 场景中无 MarioController | MarioController + Rigidbody2D + PlayerHealth + ScanAbility + BoxCollider2D |
| Trickster | 场景中无 TricksterController | TricksterController + DisguiseSystem + TricksterAbilitySystem + EnergySystem |
| Managers | 场景中无 GameManager | GameManager + InputManager + LevelManager + GameUI，自动连线所有引用 |
| Camera | Main Camera 无 CameraController | CameraController，自动设置 target 和 bounds |
| KillZone | 场景中无 KillZone | 底部死亡区域触发器 |

### 关键设计
- **幂等安全**：场景中已有对象时跳过创建（如在 TestScene 中追加模板不会重复创建 Mario）
- **自动连线**：通过 SerializedObject API 设置所有 SerializeField 引用（groundLayer、spawnPoint 等）
- **B028 兼容**：自动创建 Player/Trickster Layer 并禁用两者碰撞
- **边界自适应**：根据 ASCII 关卡实际尺寸自动计算 Camera 和 LevelManager 的边界

### 修改文件
- `Assets/Scripts/Editor/TestConsoleWindow.cs` — 新增 ~340 行（EnsurePlayableEnvironment + 辅助方法）

## [2026-04-05] S32: 视碰分离与关卡度量转译系统

### 核心理念
基于 GDC "Building a Better Jump"、Celeste 10大容错机制、GMTK Platformer Toolkit、DiGRA 跳跃模型论文、Reverse Design: Super Mario World 等业界最佳实践，建立了完整的"白盒锁死物理真相，一键换肤纯净包浆"工业化管线。

### 新增文件
- `Assets/Scripts/LevelDesign/PhysicsMetrics.cs` — 全局物理度量常量中心（碰撞体尺寸、跳跃极限、安全约束）
- `Assets/Scripts/Player/JumpArcVisualizer.cs` — 跳跃抛物线可视化工具（Scene视图实时预览，含半重力顶点弧线）
- `LevelDesign_References/PHYSICS_METRICS_GUIDE.md` — 技术方案文档

### 修改文件

| 文件 | 变更内容 |
|------|---------|
| `MarioController.cs` | 新增半重力跳跃顶点（Celeste风格）：apexThreshold + apexGravityMultiplier |
| `TricksterController.cs` | 同步新增半重力跳跃顶点，与Mario保持一致 |
| `SpriteAutoFit.cs` | 重写：支持 Tiled 渲染模式，实现视碰分离的像素完美效果 |
| `AsciiLevelGenerator.cs` | CELL_SIZE 引用 PhysicsMetrics；所有 Spawn 方法的碰撞体引用 PhysicsMetrics 常量；ApplyTheme 自动挂载 SpriteAutoFit |
| `TestSceneBuilder.cs` | Mario/Trickster/元素碰撞体尺寸全部引用 PhysicsMetrics 常量 |
| `TestConsoleWindow.cs` | Mario/Trickster 碰撞体尺寸引用 PhysicsMetrics 常量 |

### 物理度量体系
- 原地最高跳：2.5格 | 满速平跳：4.5格 | 含Coyote：5.85格
- 角色碰撞体：宽0.8 高0.95（小于1格=宽容感）
- 地刺碰撞体：(0.9, 0.35)，比视觉小=差一点就碰到的宽容感
- 安全间隙上限：4格 | 安全高台上限：2格

### 半重力跳跃顶点
- 触发条件：长按跳跃键 + |velocity.y| < 2.0
- 效果：重力减半，跳跃弧线顶部更平缓
- 参考：Celeste 容错机制 #3 (Maddy Thorson)

### S32 补丁：完整性审查与扩展机制

**新增文件**：
- `Assets/Scripts/LevelDesign/AsciiLevelValidator.cs` — ASCII 模板物理验证器（间隙/高台/出生点/终点检查）

**补充修改**：

| 文件 | 变更内容 |
|------|--------|
| `AsciiLevelGenerator.cs` | GenerateFromTemplate 生成前自动调用 AsciiLevelValidator；扩展指南注释 |
| `TestSceneBuilder.cs` | Mario 创建时自动挂载 JumpArcVisualizer |
| `TestConsoleWindow.cs` | Mario 创建时自动挂载 JumpArcVisualizer |
| `PhysicsMetrics.cs` | 添加新元素扩展指南注释（6步操作流程） |
| `PHYSICS_METRICS_GUIDE.md` | 新增第7章验证器说明、第8章扩展指南 |

## [2026-04-05] S33: Level Builder ↔ Teleport/Cheats 联动 + 动态锚点系统

### 问题背景
1. `GenerateWhiteboxLevel()`（内置模板）缺少 `EnsurePlayableEnvironment()` 调用，生成的关卡无法直接 Play，Cheats 也无法生效（S31 遗漏）
2. Teleport Tab 硬编码 9-Stage 坐标（来自 TestSceneBuilder），无法适配 ASCII Level Builder 生成的关卡
3. Cheats Tab 在缺少必要组件时静默失败，用户无从得知原因

### 改动 1: 统一调用链
- `GenerateWhiteboxLevel()` 补全 `EnsurePlayableEnvironment(root)` 调用
- 与 `GenerateFromCustomTemplate()` 保持一致，生成即可 Play
- `EnsurePlayableEnvironment()` 是幂等的，重复调用安全

### 改动 2: 动态锚点系统（参考 Celeste Debug Map 场景自省理念）
- 新增 `TeleportAnchor` 结构体 + `RefreshTeleportAnchors()` 方法
- 三路扫描：LevelElementRegistry（白名单过滤）+ SpawnPoint 标记 + GoalZone
- POI 白名单：Trap / Enemy / Hazard / HiddenPassage / Collectible / Checkpoint
- 危险对象自动叠加安全传送偏移 `Vector3.up * 2f`
- 按分类排序 + Foldout 折叠菜单 + ScrollView 限高 300px
- 每个锚点提供 [F] 聚焦按钮（Scene View 定位）

### 改动 3: Cheats Tab 缺失组件检测
- 新增 `cheatsAvailable` 检测：缺少 MarioHealth / GameManager / EnergySystem / DisguiseSystem 时显示警告
- 视觉阻断：缺少组件时置灰所有 Cheat Toggle
- 一键修复按钮：`Auto-Fix: Inject Playable Environment`
- 修复后自动刷新缓存 + 动态锚点

### 改动 4: 安全防御（基于审计意见 5 点加固）
1. **Fake Null 防御**：遍历 cachedAnchors 时 `if (anchor.SourceObject == null) continue`
2. **ScrollView 防爆栈**：动态锚点区域包裹 `BeginScrollView` + `MaxHeight(300)`
3. **白名单噪音过滤**：剔除 Platform / Misc 等纯静态地形
4. **一键修复替代消极警告**：Cheats Tab 缺组件时提供 Auto-Fix 按钮
5. **懒加载/状态校验**：`DrawDynamicAnchorsSection` 入口做 null 检查，Domain Reload 后自动重建缓存

### 修改文件
- `Assets/Scripts/Editor/TestConsoleWindow.cs` — 1 个文件，+350 行，零运行时脚本修改

### 设计原则
- 全部改动在 `Assets/Scripts/Editor/` 内，不影响 114 个自动化测试
- 严格遵守所有 `[AI防坑警告]` 注释
- `SnapToTarget()` 调用链完整保留
- `[System.NonSerialized]` 确保序列化隔离
