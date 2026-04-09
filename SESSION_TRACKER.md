# MarioTrickster Session Tracker

> **AI 协作唯一入口文档**：新对话**只需读取本文件**即可无缝衔接。

---

## 0. 敏捷协作新规（S53 起生效：全面转向设计驱动）

> **核心宪章：停止预见性基建，设计优先。架构必须是隐形的。**

### AI 的角色定位（S53 起）

从本 Session 起，AI 从“系统架构师”转变为用户的**“关卡设计助手 + 按需机制实现者”**。

| 规则 | 说明 |
|------|------|
| **禁止主动基建** | 严禁 AI 主动提出底层重构、解耦、测试管线升级。135 个测试 + 柔性断言已足够兜底。 |
| **JIT 按需开发** | 只有当用户在拼关卡时明确说“我需要一个新机制（如锯片）”，AI 才允许写代码。由设计倒推代码。 |
| **低摩擦交付** | 新增机制时，AI 静默处理所有底层逻辑（继承基类、更新 Registry 等）。对用户只说“在文本框打 Z 生成锯片”，不用底层名词增加认知负担。 |
| **消除摩擦力** | 若用户调优参数导致验证器/辅助线报错打断设计心流，AI 的唯一任务是“极简微创手术”抹平报错，让系统顺应用户的设计直觉。 |
| **风险透明** | AI 仍应主动提醒实际风险（如验证器发现不可达路径），但以“一句话提醒”的方式，不展开技术细节。 |

### 基建状态总结（S53 冻结线）

以下基建已完备，不再追加：

| 基建 | 状态 |
|------|------|
| 物理度量系统 (PhysicsMetrics Facade + PhysicsConfigSO) | S53 完成，唯一真理源已打通 |
| 柔性测试管线 (TAS + 135 个测试 + 耗时预警) | S50-S53 稳定 |
| 视碰分离架构 | S37 已固化 |
| 关卡生成/验证/可视化全链路 | S32-S48 已闭环 |
| 实时手感面板 | S52 已交付 |
| 陷阱基类 (BaseHazard) | S52 已就绪，等待设计驱动扩展 |

### 日常必做（保留，耗能 < 5%）

每次 `git push` 前，**仅更新本文件**的以下三处，绝不触碰其他任何长文档：
1. **§1 状态总览** — 更新 Session 号和一句话描述
2. **§2 回归标记** — 受影响的测试项打 🔄
3. **§4 待办队列** — 更新任务状态

防回归检查全面交给本地 **135 个自动化测试**兜底，不再手动维护复杂交叉验证表格。

### Git 替代历史，代码即文档

- 历史记录全靠**详细的 Git Commit Message**，不再写 Session 历史到 Progress_Summary
- 新对话主动通过 `git log --oneline -n 10` 回溯近期变更
- 需要深入了解某次修改时，用 `git show <hash>` 查看具体 diff

### 核心防坑机制：`// [AI防坑警告]` 源码注释

对于容易被新对话改坏的核心底层逻辑，**必须直接在 `.cs` 源码对应方法上方写中文警告注释**。这是**跨对话防退化的最强武器**。

### 集中归档（仅限用户指令触发）

只有当用户明确发送 **“执行文档大同步”** 时，才去全量更新 Progress_Summary / MASTER_TRACKER / Testing_Guide。**日常开发中绝对不碰这三个文件。**

### 性能编码自检（P1-P7，保留但精简）

每次写新代码时脑中过一遍，推送前跑一次 grep：

| 编号 | 一句话规则 |
|------|----------|
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

### 其他保留规则

- **Session 编号**：产生代码修改时 +1，仅回答问题不加
- **Git 规范**：master 分支，英文 commit，首行概述 + 空行 + 详细列表
- **积分管理**：接近阈值时立即暂停，优先更新本文件并推送
- **TestSceneBuilder 同步**：新增/修改可见行为时，同一 commit 中更新对应 Stage 标签

---

## 1. 当前状态总览

| 字段 | 值 |
|------|-----|
| **最新 Session** | Session 58i (S58i: 美术指引大白话重写 + 画风替换指令) |
| **日期** | 2026-04-09 |
| **分支** | master |
| **阶段** | Sprint 2 游戏体验提升 — 美术指引已重写，等待用户确立画风偏好 |
| **编译状态** | ✅ 零代码变更，仅文档优化 |
| **阻塞** | 无 |
| **交接说明** | S58i 对 `ART_PIPELINE_GUIDE.md` 进行了三项重大优化：**① 重写 §3.1 日常工作流为端到端闭环**，明确每一步谁做什么（你 vs AI vs Unity 自动化），覆盖从“发指令”到“素材入库”的全过程，特别补充了 Step 2“本地出图不满意时如何反馈给 AI 调整”的反馈循环。**② 重写 §3.4 快捷指令为大白话版**，每个指令补充“为什么要做”和“你需要准备什么”，指令 1 改为先问用户风格偏好再出 Prompt。**③ 新增指令 4「整体画风推翻重来」**，覆盖“做到一半想换风格”场景，并在顶部补充“锚点不是枷锁”说明。用户下一步：告诉 AI 想要什么风格（或上传参考图），然后触发指令 1 确立锚点。 |

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
| 🔄 | 场景生成 | **S56重点验证**: ASCII Build 新字符(@f<SX)能正确生成对应元素，旧字符行为不变 |
| 🔄 | 编辑器 Picking / Size Sync | **S57c重点验证**: Visual 模式点击/框选 `Visual` 只选中 Visual，不再回跳 Root；开启 Size Sync 后修改 `Visual.localScale` 与 `Root.BoxCollider2D.size` 会双向同步；Mario/Trickster Root 仍保持不缩放 |
| ✅ | EditMode 自动化 | 109/109 通过（S37 视碰分离后全量通过） |
| 🔄 | PlayMode 自动化 | **S53重点验证**: 26/26 通过 + 柔性模式下应看到 S53 耗时校验日志 |

---

## 3. 自动化测试（安全网）

- **EditMode**: 109 个（结构/静态逻辑验证，S37 全量通过）
- **PlayMode**: 26 个（运行时行为验证，S51 新增 2 个数据驱动 TAS 测试）
- **运行方式**: `MarioTrickster → Run Tests → Export Full Report (All)` 导出到 `TestReport.txt`
- **总计 135 个测试**（EditMode 109 + PlayMode 26）作为防回归兜底

---

## 4. 待办队列

> **S53 起：设计优先。** 用户的关卡设计需求是唯一的任务源。

| 优先级 | 描述 | 状态 |
|--------|------|------|
| **最高** | **概念锚点出图**：使用 `PROMPT_RECIPES.md` 中的概念锚点蓝图在 ComfyUI/Midjourney 出图，满意后记录 Seed，保存到 `Assets/Art/Reference/Reference_Anchor.png`。 | 🚀 工单已派发，等待用户本地出图 |
| **高** | **批量资产生产**：概念锚点确立后，使用 IPAdapter 喂入锚点图 + PROMPT_RECIPES 配方批量出图，用 `AI_SpriteSlicer` 一键切片。 | ⏳ 等待概念锚点确立 |
| **高** | 验证 S57c 编辑器工作流：Visual 模式选取是否彻底只落到 Visual；`Size Sync` 是否能同步 `Visual.localScale` 与 `BoxCollider2D.size`；新增机关是否自动继承该行为。 | ⏳ 待用户验证 |
| **按需** | JIT 机制填充：仅当设计关卡极度需要时，才由 AI 在后台静默实现新机制。 | 待触发 |
| **按需** | 参数调优：拖动 PhysicsConfigSO 滑块调手感，若触发报错则由 AI 微创手术抹平。 | 待触发 |
| 远期 | 音效系统 / 动画完善 / 主菜单 UI | 未开始 |

<details>
<summary>📦 历史基建存档（S26-S55，已冻结，点击展开）</summary>

| Session | 描述 | 状态 |
|---------|------|------|
| S57b | Palette补全 + Picking重构 + 多项修复 + Ground/Platform水平合并 | ⏳ 待验证 |
| S57 | 运行时内存溢出防护：MemoryGuard + QualitySettings 降级 + 场景切换资源释放 | ⏳ 待验证 |
| S56 | ASCII 关卡元素扩展：锯片(@) 飞行敌人(f) 传送带(<) 检查点(S) 可破坏方块(X) | ⏳ 待验证 |
| S55c | 路线 B 一键导入脚本 + 工作流精简 | ⏳ 待验证 |
| S55b | ASCII 模板物理验证闭环机制（验证器 + 工作流防坑规则） | ⏳ 待验证 |
| S55 | Z字攀爬塔 ASCII 模板物理可行性修复（零代码变更） | ⏳ 待验证 |
| S54 | 手感预设管理系统 Preset Manager | ⏳ 待验证 |
| S53 | PhysicsMetrics Facade 统一真理源 + TAS 轨迹耗时预警 | ⏳ 待验证 |
| S52 | 柔性测试降级 + PhysicsConfigSO 手感面板 + BaseHazard 基类 | ⏳ 待验证 |
| S51 | 零代码数据驱动测试管线 DDPT | ⏳ 待验证 |
| S50 | TAS 录播系统 (10x 物理加速) | ⏳ 待验证 |
| S49 | IInputProvider 输入解耦 | ⏳ 待验证 |
| S48-S48c | KillZone 三重检测 + 日志修复 + Registry 序列化 | ✅/⏳ |
| S47 | BFS 可达性验证器 | ⏳ 待验证 |
| S46 | Data-Driven Registry | ⏳ 待验证 |
| S45 | Doc-as-Code 文档同步引擎 | ⏳ 待验证 |
| S44-S44c | OneWayPlatform 合并 + 下落修复 + 敌人穿地修复 | ⏳ 待验证 |
| S43 | 关卡生成验证系统修复 + 行业对标 | ⏳ 待验证 |
| S41 | LevelEditorPickingManager v3 | ⏳ 待验证 |
| S40 | [SelectionBase] 框选修复 | ✅ |
| S39 | 弹跳平台重构 | ⏳ 待验证 |
| S37 | 视碰分离架构重构 | ✅ 109/109 |
| S36 | 弹跳平台 Game Feel | ⏳ 待验证 |
| S35 | 关卡布局安全性修复 | ⏳ 待验证 |
| S33 | Level Builder 联动 | ⏳ 待验证 |
| S32 | 视碰分离与度量转译 | ⏳ 待验证 |
| S26b | Level Studio 精简 | ✅ |

</details>

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

## [2026-04-07] S55: Z字攀爬塔 ASCII 模板物理可行性修复

### 问题诊断

用户测试发现模板 2（Z字攀爬塔）Build 后"只有一个平台一个敌人"，实际生成了多个元素但完全不可玩。经逐行分析 ASCII 模板与 `AsciiLevelGenerator.cs` 转换逻辑，确认**根因是 ASCII 模板设计缺陷，非转换代码 bug**。转换代码忠实地按字符位置生成了所有元素。

| 缺陷 | 原模板表现 | 物理后果 |
|------|-----------|---------|
| **平台仅 1 格宽** | 每个平台只有单个 `=` | 角色碰撞体宽 0.8 格，几乎无法站稳 |
| **敌人在平台下方** | `e` 在 `=` 的下一行（ASCII 下一行 = 更低的 worldY） | 敌人生成在平台下方空中，因重力直接掉落 |
| **平台间距超限** | 左右交错间距约 9 格水平 + 3 格垂直 | 远超 MAX_JUMP_DISTANCE=4.5 和 MAX_JUMP_HEIGHT=2.5 |

### 修复方案

重新设计 ASCII 模板，保持 Z 字攀爬设计意图，修正所有物理约束：

| 参数 | 原值 | 新值 | 安全上限 |
|------|------|------|---------|
| 平台宽度 | 1 格 | 4 格 (`====`) | — |
| 垂直间距 | 3+ 格 | 2 格 | 2 格 |
| 水平间隙 | 9+ 格 | 3 格 | 4 格 |
| 敌人位置 | 平台下方 1 行 | 平台上方 1 行 | — |

新模板经 Python 脚本逐跳验证，所有路径均在安全范围内。

### 修改文件

| 文件 | 变更内容 |
|------|---------|
| `LevelDesign_References/ASCII_Templates.md` | 重写模板 2 的 ASCII 布局 + 新增 S55 重新设计说明和物理验证段落 |

### 零代码变更
本次仅修改 Markdown 模板文件，不触碰任何 .cs 文件。用户需在 Unity Level Studio 中粘贴新模板 Build 验证。

## [2026-04-07] S55b: ASCII 模板物理验证闭环机制

### 问题背景

S55 修复了 T02 模板的三个物理缺陷后，用户提出核心问题：**如何避免未来搜集的参考关卡反复出现同类问题？** 审计全部 8 个模板后发现，T02 的问题模式（敌人在平台下方、平台过窄、间距超限）是 AI 生成 ASCII 模板时的系统性通病。

### 根因分析

| 通病 | 原因 | 影响范围 |
|------|------|---------|
| 敌人在平台下方 | AI 不理解 ASCII 坐标系（第 1 行 = 最高 worldY） | T02（已修复） |
| 平台仅 1 格宽 | AI 倾向用单字符表示元素，不考虑碰撞体尺寸 | T01/T06/T07/T08（多为设计意图） |
| 间距超出跳跃极限 | AI 不做物理验证，凭视觉感觉布局 | T02（已修复） |
| 缺少闭环验证 | 生成后无自动检查，直到 Unity Build 才发现 | 所有模板 |

### 解决方案：三层防线

**第一层：源头防坑（AI 指令加固）**
- 在 `AI_PROMPT_WORKFLOW.md` 路线 A 步骤 3 和路线 B 提示词中新增 **物理防坑规则**：
  1. 主要站立面平台 >= 3 格宽
  2. 敌人必须在平台上方行（行号更小 = 更高 worldY）
  3. 逐步验证跳跃路径
  4. 行宽一致性

**第二层：自动验证（Python 验证器）**
- 新增 `LevelDesign_References/validate_ascii_template.py`，9 项检查：
  P1 行宽一致性 / P2 起终点存在 / P3 禁止空格 / P4 字符合法 / P5 平台宽度 / P6 敌人支撑 / P7 跳跃可达 / P8 起点支撑 / P9 终点可达
- 路线 A 新增步骤 3.5 强制运行验证器
- 路线 B 第三步新增验证器运行步骤

**第三层：用户 Unity 实测（已有）**
- 粘贴 Build 后在 Unity 中 Play 测试

### 修改文件

| 文件 | 变更内容 |
|------|---------|
| `LevelDesign_References/validate_ascii_template.py` | 新增 ASCII 模板物理验证器（~300 行 Python） |
| `LevelDesign_References/AI_PROMPT_WORKFLOW.md` | 路线 A 新增物理防坑规则 + 步骤 3.5 自动验证；路线 B 新增防坑规则 + 验证步骤 |

### 零代码变更
本次不触碰任何 .cs 文件。

## [2026-04-07] S55c: 路线 B 一键导入脚本 + 工作流精简

### 问题背景

路线 B 第三步"人工闭环操作"需要 5 个手动步骤（追加模板 → 运行验证 → 更新登记簿 3 处 → git push），操作繁琐且容易遗漏。同时第一步和第二步的"复制黑名单"措辞存在歧义。

### 解决方案

1. **一键导入脚本** `import_template.py`：将 5 个手动步骤自动化为 1 行命令
   ```bash
   python3 LevelDesign_References/import_template.py ai_reply.md --source "来源"
   ```
   脚本自动：解析 AI 回复 → 追加模板（自动编号）→ 物理验证（失败自动回滚）→ 更新登记簿 3 处 → git push

2. **复制指引消歧**：第一步改为表格明确说明该复制【快速复制区】和【已探索灵感来源】两个区块

3. **第三步精简**：从 5 步手动操作改为 2 步（保存文件 + 运行脚本）

### 修改文件

| 文件 | 变更内容 |
|------|---------|
| `LevelDesign_References/import_template.py` | 新增一键导入脚本（~250 行 Python） |
| `LevelDesign_References/AI_PROMPT_WORKFLOW.md` | 路线 B 第一步消歧 + 第三步精简为一键脚本 |

### 零代码变更
本次不触碰任何 .cs 文件。
