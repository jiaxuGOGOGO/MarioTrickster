# MarioTrickster

> **一句话定位**：MarioTrickster 是一款非对称对抗平台跳跃游戏，一名玩家扮演闯关者通过关卡，另一名玩家扮演捣蛋者伪装为障碍、地形或怪物来阻止闯关者。[1] [2]

本文档是仓库首页，也是**唯一用户入口 README**。如果你在 GitHub 文件搜索里搜 `read`，只需要打开仓库根目录这个 `README.md`；旧的目录级 README 已改名为内部指南或迁移指针，避免同一套规则散落在多个长文档中反复维护。[1] [3]

---

## 1. 先看哪一个文档

| 你的目标 | 权威入口 | 说明 |
| --- | --- | --- |
| 继续开发、让 AI 接手、提交测试反馈 | [SESSION_TRACKER.md](./SESSION_TRACKER.md) | 当前进度、待办队列、防坑规则与推送前更新点都以这里为准。 |
| 新 AI 第一次接管项目 | [docs/AI_TAKEOVER_PROTOCOL.md](./docs/AI_TAKEOVER_PROTOCOL.md) | 极简接管协议，保留“后台读档、少打扰、自动落库”的协作方式。 |
| 做关卡、换素材、接机制 | [docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) | 面向策划的一页式工作流，优先从这里开始日常制作。 |
| 导入商业素材、Sprite Sheet 或角色帧 | [docs/ASSET_IMPORT_PIPELINE_GUIDE.md](./docs/ASSET_IMPORT_PIPELINE_GUIDE.md) | 记录 Asset Import Pipeline、Apply Art、SEF、单 RUN 试跑、完整 idle/run/jump/fall 状态动画和通用商业状态命名规则。 |
| 在 Unity 里验证功能和导出测试报告 | [MarioTrickster_Testing_Guide.md](./MarioTrickster_Testing_Guide.md) | 手动测试、自动测试、回归报告入口都在这里。 |
| 查看设计矩阵与长期状态 | [MASTER_TRACKER.md](./MASTER_TRACKER.md) | 设计规划与实现状态的对照表。 |
| 查历史细节和旧 Bug | [MarioTrickster_Progress_Summary.md](./MarioTrickster_Progress_Summary.md) | 深档案，只有需要追溯长期历史时再读。 |
| 查 Git 常用命令 | [AI_WORKFLOW.md](./AI_WORKFLOW.md) | 已收束为短附录，不再作为协作主入口。 |
| 查 SEF 像素特效工具细节 | [Assets/SpriteEffectFactory/SEF_GUIDE.md](./Assets/SpriteEffectFactory/SEF_GUIDE.md) | 仅在维护 Sprite Effect Factory 时阅读。 |
| 查旧动画生成管线脚本 | [Assets/anim_pipeline/MarioTrickster_AnimPipeline/PIPELINE_GUIDE.md](./Assets/anim_pipeline/MarioTrickster_AnimPipeline/PIPELINE_GUIDE.md) | 仅在维护历史动画管线时阅读，日常导入仍看素材导入指南。 |

> **协作原则**：人类用户只需要用大白话说目标；AI 必须在后台读取必要文档、处理架构约束、更新 `SESSION_TRACKER.md` 并推送，不把内部术语和参数表转嫁给用户。[1] [3]

---

## 2. 当前最短使用路径

### AI 自动测试：你只需启动并体验结果

`Ctrl+T → 创作与试玩 → AI 自动搭建与覆盖测试 → 一键开始`

默认会先无人值守执行全部 EditMode/PlayMode 回归，再生成机制组合，自动让双方 AI 跑局并汇总结果。首次启动按提示保存原场景；批次完成或停止后恢复原场景。无需逐局摆图、切角色或整理日志。

| 档位 | 自动计划 | 用途 |
| --- | --- | --- |
| 快速探索（默认） | 6 个场景 × 3 种画像 = 18 局 | 第一次确认整条自动流程 |
| 机制覆盖 | 全部 19 种机制单项 + 19 个邻接组合，合计 114 局 | 扩展覆盖现有机制 |
| 两两共现 | 19 个单项 + 全部 171 种无序组合，合计 570 局 | 长时间组合探索；不是时序/参数穷举 |

三种画像为谨慎、冲刺、主动探索；所有输入仍受真实物理、能量与冷却约束。自动记录生成、接近、双方接触、实际激活及观察到的阶段，并跟踪扫描、附身、连锁、热度、危机、揭穿、拿宝撤离与路线预算事件。**没有观察到就列为覆盖缺口，不会自动当作通过。AI 通关也不等于好玩。**

- 报告保存在 `reports/ai_exploration/<批次>/`：`summary.txt`、`report.json`、每个场景的 `.txt`，以及启用回归时的 `TestReport.txt`。
- 面板可筛选失败/覆盖不足案例，点击“AI 重测此场景”或“我亲自试玩此场景”。编辑器重启后可恢复最近报告入口，不会擅自续跑旧批次。
- 布局种子与场景源码可复现；物理帧时序仍受 Unity 环境影响。报告记录物理/玩法配置快照，复测使用当前项目配置，不会自动覆盖你的调参。
- 每局完整重入 Play，保留运行预算与停止入口。Unity 回归阶段的停止请求会等待 Test Runner 安全退出；长时间无响应请查看 Test Runner。
- 沙盒已执行 49 项模型测试，未执行真实 Unity 编译、全量回归或 AI 物理试玩。这些会在你本地启动上面的入口后运行；详细边界见 SESSION_TRACKER 的 S153。

### 手动创作与美术工作流

| 场景 | 你做什么 | 系统自动承接什么 |
| --- | --- | --- |
| 做一段新关卡 | 打开 Unity 2022.3 LTS，用 `Ctrl+T → 创作与试玩`，选起步房间，在网格画布上直接画，再点“搭建并试玩画布”。 | 自动恢复本机草稿、支持整笔撤销；提示保存原场景后生成独立练习场，并保留后续换皮入口。 |
| 给白盒换商业素材 | 选中 Root 或 Visual，使用 Apply Art，拖入素材后应用。 | 自动归一到行为 Root，只替换视觉、动画和材质，不破坏碰撞与行为。[2] [4] |
| 导入角色状态动画 | 把帧命名为 `hero_idle_00`、`hero_run_00`、`hero_jump_00`、`hero_fall_00` 这类格式；只有 `hero_run_00` 也能先试跑。 | 应用到角色目标时自动挂 `SpriteStateAnimator`，完整四状态按各自帧播放；单 RUN 左右移动播放跑步，缺失状态用静态帧兜底。[4] |
| 导入普通循环动画 | 提供多帧 Sprite Sheet 或散帧，命名为火把、水面、传送门、特效等非角色循环用途。 | 自动挂 `SpriteFrameAnimator` 循环播放；攻击、受伤、死亡、潜行、技能释放等额外商业状态会先记录为状态摘要，不会破坏现有控制器。[4] |
| 让 AI 增加新机制 | 直接说“我想要一个会怎样互动的机关/敌人/能力”。 | AI 按项目规则补代码、字典、测试和文档，完成后自动提交推送。[1] [3] |

**现在最短的创作循环**：选小房间 → 左键画 / 右键擦 → 选择自己玩哪一方 → 搭建并试玩 → 查看耗时与结束位置 → 返回改一处。ASCII、美术、AI Arena 与原调参工具仍在“高级工具（原工作台）”。

- 草稿自动保存仅限本机；导出 `.txt` 后可分享或加入 Git。场景中的手工美术/摆放不会自动回写画布，请使用“试玩当前场景”保留这些修改。
- 编辑器 F5/R 会完整重入 Play，以恢复未保存关卡和已销毁对象；需要启用 Reload Scene，仍有 Unity 重入耗时，并非瞬时复活。
- S152 的 25 项文档模型测试及静态语法检查已通过；真实 Unity 编译、PlayMode 测试与玩法手感仍待验证。详见 [当前交接](./SESSION_TRACKER.md)。

推荐顺序是先把白盒玩法跑通，再接入美术素材。项目的核心目标仍是关卡节奏、Trickster 伏击点和机制互动，美术管线是服务于已验证玩法的支线能力。[2]

---

## 3. 仓库结构

| 仓库 | 地址 | 职责 |
| --- | --- | --- |
| 主仓库 | [MarioTrickster](https://github.com/jiaxuGOGOGO/MarioTrickster) | 游戏逻辑代码、关卡设计、核心配置和主文档。 |
| 美术仓库 | [MarioTrickster-Art](https://github.com/jiaxuGOGOGO/MarioTrickster-Art) | 美术源文件、Sprite 导出、参考图和美术资产资料。 |

美术仓库通过 Git Submodule 挂载在主仓库的 `Assets/MarioTrickster-Art/` 目录下。首次克隆建议使用以下命令，避免遗漏子模块资源。[1]

```bash
git clone --recurse-submodules https://github.com/jiaxuGOGOGO/MarioTrickster.git
```

---

## 4. 开发者快速启动

| 步骤 | 动作 |
| --- | --- |
| 1 | 使用 Unity 2022.3 LTS 打开项目根目录。 |
| 2 | 先读 [SESSION_TRACKER.md](./SESSION_TRACKER.md)，确认当前 Session、阻塞和待办队列。 |
| 3 | 如需做关卡或换素材，读 [策划快速指南](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) 与 [素材导入指南](./docs/ASSET_IMPORT_PIPELINE_GUIDE.md)。 |
| 4 | 修改后按 [测试指南](./MarioTrickster_Testing_Guide.md) 导出报告；推送前更新 `SESSION_TRACKER.md` 的状态总览、回归标记和待办队列。 |

---

## References

[1]: ./SESSION_TRACKER.md "MarioTrickster Session Tracker"
[2]: ./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md "Planner Fast Level Production Guide"
[3]: ./docs/AI_TAKEOVER_PROTOCOL.md "AI Takeover Protocol"
[4]: ./docs/ASSET_IMPORT_PIPELINE_GUIDE.md "Asset Import Pipeline Guide"
