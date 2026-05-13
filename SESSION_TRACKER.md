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
| **最新 Session** | Session 121（Pivot 统一升级：修复角色换皮脚底悬空 + 手动 Pivot 选项 + 事后修正工具） |
| **日期** | 2026-05-13 |
| **分支** | master |
| **阶段** | Sprint 2.5 美术自动化落地期 — S121 修复素材导入后 Pivot 未正确设置导致角色脚底悬空的问题，统一所有导入/切片工具的 Pivot 逻辑到 PivotPresetUtility，新增参考 Unity Sprite Editor 的10 种预设 + Custom 手动输入 + 9 宫格快捷按钮，新增独立 Pivot 修正工具和一键修复菜单项。 |
| **编译状态** | ✅ 沙盒无 Unity CLI，本轮完成代码审查与文档同步；待用户在 Unity 中 `git pull` 后刷新 Console，用 Apply Art 给 Mario 换皮确认脚底对齐。 |
| **阻塞** | 无。所有现有导入路径保持兼容，Auto 模式行为与之前一致，新增手动选项不影响已有工作流。 |
| **交接说明** | S121 延续文档入口；素材导入、Pivot 设置与事后修正看 `docs/ASSET_IMPORT_PIPELINE_GUIDE.md` 第 5 节。 |


### [S121] 最新知识沉淀

1. **角色换皮脚底悬空的根因**：`AssetApplyToSelected.GetPivotForPhysicsHint()` 在目标对象没有 `ImportedAssetMarker` 时返回 -1，导致 Pivot 默认为 Center(0.5,0.5) 而非角色应有的 BottomCenter(0.5,0)。修复方案：Auto 模式现在会检查目标对象是否有 `MarioController` 等角色组件。
2. **Pivot 逻辑统一到 PivotPresetUtility**：`AssetApplyToSelected`、`AssetImportPipeline`、`AI_SpriteSlicer` 三个工具的 Pivot 计算全部走 `PivotPresetUtility.ResolvePreset()`，不再各自硬编码。
3. **用户手动覆盖优先于自动推断**：用户选择非 Auto 的任何预设时，系统直接使用用户选择，不再推断。Custom 模式允许输入任意 (x,y) 坐标。
4. **TA_AssetValidator 尊重自定义 Pivot**：合规巡检现在会识别 `SpriteAlignment.Custom`，标记为“用户手动设置”而非违规。新增“一键修复 Pivot”菜单项也会跳过 Custom 帧。
5. **PivotRepairTool 是独立的事后修正入口**：支持单个场景物体、单张贴图、批量文件夹三种模式，不依赖导入管线，可随时使用。

### [S120] 知识沉淀

1. **商业素材状态分两层处理**：`IDLE/RUN/JUMP/FALL` 是当前可直接驱动主角运行时 `SpriteStateAnimator` 的运动状态；`ATTACK/HURT/DEATH/CAST/SKILL/STEALTH/DISGUISE/BLEND/OPEN/CLOSE/LOOP/IMPACT` 等是通用语义状态，先进入分类摘要和 Marker 记录，不自动改玩家、敌人或道具行为。
2. **分类顺序必须保护道具和特效**：商业素材文件名里常带 `idle/cast/loop`，不能只因出现状态词就判成角色；分类器应先识别背景、地形、平台、道具、陷阱、特效等明确用途，再在角色候选路径上判断状态驱动。
3. **未来新状态走“记录优先、机制按需接线”**：新增潜行、融入环境、释放技能、敌人攻击、道具开关等动画时，先保证导入不崩、摘要可见、文档一致；只有关卡设计明确需要交互时，再由 JIT 机制把对应语义状态接到运行时逻辑。

### [S119] 知识沉淀

1. **主角单组状态素材也应走状态机**：当目标对象是 PlayerStateDriven（Mario/主角）且素材名或切片名能识别到 `run/idle/jump/fall` 任意一组时，`ArtAssetClassifier` 必须返回 `StateDriven`，不要因为只有一组 RUN 就退回普通循环动画。
2. **只有 RUN 时待机必须静态兜底**：`GetStateFramesOrFallback` 对 Idle/Jump/Fall 使用 RUN 第一帧兜底，只有 Run 状态保留完整跑步帧，避免角色站着也循环跑步。
3. **导入新角色与给主角换皮两条路径保持一致**：`AssetImportPipeline` 和 `Apply Art to Selected` 的状态回退规则必须同步，用户既可以先单独导入跑步图测试，也可以之后补齐 `IDLE/RUN/JUMP/FALL` 完整动画。

### [S118] 知识沉淀

1. **不要直接依赖 `UnityEditor.U2D.Sprites` 编译期命名空间**：当前项目 Unity 环境没有该命名空间，业务脚本一旦 `using UnityEditor.U2D.Sprites` 或声明 `SpriteRect` 就会全项目编译失败。
2. **切片读写统一走兼容桥接层**：`AI_SpriteSlicer`、`AssetImportPipeline`、`TA_AssetValidator`、`PlannerProductionAssistant` 只调用 `SpriteSheetDataProviderBridge.GetSpriteMetaData` / `SetSpriteMetaData`，桥接层内部集中处理版本差异和弃用警告。
3. **策划助手批量改名保持二段式安全流程**：先生成建议并弹窗确认，再按 `SpriteMetaData.name + rect` 匹配切片改名；不要恢复成直接改文件名或后台静默改名。

### [S117] 知识沉淀

1. **`TextureImporter.spritesheet` 警告需在桥接层集中处理**：业务脚本不得直接访问旧接口，统一走 `SpriteSheetDataProviderBridge`，避免警告和版本兼容逻辑散落到多个窗口。
2. **多 Sprite 改名必须匹配切片矩形**：策划生产助手改切片名时按原 Sprite 名和 `rect` 双重匹配，避免同名帧或同图集多帧时改错对象。
3. **后续新增切片读写只走桥接层**：新代码不要直接碰 `TextureImporter.spritesheet`，读写都用桥接层提供的 SpriteMetaData 方法。

### [S116] 知识沉淀

1. **语义报告现在可以安全落地改名**：`PlannerProductionAssistant` 新增“采纳建议并批量改名”，先根据报告生成预览，弹窗确认后才执行；取消时只保留建议，不动原素材。
2. **改名粒度遵守 Unity 资源结构**：多 Sprite 图通过新版 Sprite 数据接口改 Sprite 切片名；单图只改对应 Texture 资源名；同一路径多建议但不是多 Sprite 时跳过并给警告，避免误改整张文件。
3. **用户诉求优先级明确**：素材命名混乱时，默认流程是“生成语义报告 → 用户看建议 → 确认采纳 → 工具批量改名”，而不是后台偷偷全部重命名。

### [S115] 知识沉淀

1. **角色状态动画以命名约定驱动，不让用户填系统参数**：只要帧名包含 `idle/run/jump/fall`，分类器即可自动分组；对主角/玩家对象，即使只识别到单组 `RUN` 也可走 `SpriteStateAnimator` 并用静态帧兜底缺失状态；普通非角色多帧素材仍走 `SpriteFrameAnimator`，避免误判。
2. **文档入口必须分层收束**：README 面向用户；`SESSION_TRACKER.md` 与 `AI_TAKEOVER_PROTOCOL.md` 面向新 AI；`PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md` 面向日常关卡/换素材；`ASSET_IMPORT_PIPELINE_GUIDE.md` 面向素材导入和状态动画；`AI_WORKFLOW.md` 只保留 Git 与故障反馈附录。
3. **持续更新文档可以整理，但不能断交接链**：旧长模板和重复教程应合并到权威入口；真正影响后续接手的状态、测试、待办仍必须落在本文件，避免清理文档时清掉项目记忆。
4. **仓库内只保留一个用户 README**：GitHub 文件搜索里的目录级 `README.md` 容易误导用户；保留根目录 `README.md`，把工具内部说明改名为 `SEF_GUIDE.md` / `PIPELINE_GUIDE.md`，把迁移说明改名为 `ART_REPO_POINTER.md` 或专项路径兼容指针。

### [S114] 知识沉淀

1. **素材命名混乱先做语义报告，不强行改原文件**：商业包引用关系脆弱，自动重命名容易破坏现有引用；更安全的第一步是用 `PlannerProductionAssistant` 生成分类、推荐槽位与建议命名报告。
2. **Theme Profile 批量绑定可以自动化到“明显命中”级别**：按文件名 token 和主题槽关键字自动填 `ground/platform/wall/Mario/Trickster/elementSprites`，识别不到或已手动确认的槽位不强行清空。
3. **新机制入口先模板化，代码仍由强 AI 执行**：Unity 内窗口生成标准实现请求，把 Root/Visual、ASCII 字符、素材接入、测试和文档要求一次性带上，减少用户来回解释。

### [S113] 知识沉淀

1. **策划入口必须“一页可用”**：LevelStudio、AssetPipeline、ArtClassifier、SEF、测试报告能力已经存在，但分散在长文档中会抵消效率；面向用户应优先给 `PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md` 这种“做关卡 / 换素材 / 接机制”一页式路径。
2. **Apply Art 的真实目标永远是行为 Root**：用户在 Root 模式或 Visual 模式下都可能点中对象；工具必须后台归一到 Root，再替换 Visual 上的 Sprite/动画/材质，不能让用户理解或手动处理视碰分离细节。
3. **当前优化重点不是重做架构**：项目已能支撑白盒关卡生产、商业素材替换、状态动画和测试回归；下一步应继续减少批量命名、主题槽自动绑定和素材包语义识别成本。

### [S112] 知识沉淀

1. **状态动画分组不能吃整套图集文本**：`BuildStateGroups` 必须基于单帧自身名称/路径识别 idle/run/jump/fall；把全量 joined 文本拼进去会让每一帧同时含有所有状态词，最终被第一个状态分支误吞。
2. **素材分类不能用裸子串匹配**：`background` 天然包含 `ground`，裸 `Contains` 会让背景被误判为 Platform；分类 token 必须做边界化匹配，并让 Background 优先于 Platform。

### [S111] 知识沉淀

1. **角色素材不能再靠纯循环播放器**：主角已有 `IsGrounded`、`Speed`、`VerticalSpeed` 状态输出，商业角色帧应拆成 idle/run/jump/fall 四组，由 `SpriteStateAnimator` 按运动状态自动切换；普通场景动画、背景动画、特效动画才继续使用 `SpriteFrameAnimator` 循环播放。
2. **商业素材导入必须先分类再落位**：S111 新增 `ArtAssetClassifier`，用文件名/路径/目标对象/physicsType 推断 `artRole`、`animationMode`、`runtimeBehavior` 与 `visualEffectPolicy`，再统一写入 `ImportedAssetMarker`，避免同一素材在 Import Pipeline 与 Apply Art 两条入口出现不同结果。
3. **SEF 效果切换的第一原则是先清旧状态**：Quick Apply 现在默认执行清状态，清空 `MaterialPropertyBlock`、恢复颜色/材质属性、调用 `ResetAllEffects()` 后再套新预设；“一键还原当前物体为默认无效果”用于快速撤回试错效果，`visualEffectLocked` 则保护已确认的关卡美术效果不被误覆盖。

### [S110] 知识沉淀

1. **SpriteRenderer 本身只显示一张 Sprite**：把 Sprite Sheet 应用到物体时，如果只替换 `SpriteRenderer.sprite`，Unity 只会显示当前帧，不会自动播放整套切片。
2. **多帧素材必须有显式播放组件**：S110 新增 `SpriteFrameAnimator`，由工具自动挂到 Visual 物体上，负责在 PlayMode 中按 `frames` 循环切换 Sprite。
3. **帧顺序不能按字符串排序**：`F10` 会排在 `F2` 前面；S110 改为按 Sprite 的 `rect.y` 从上到下、`rect.x` 从左到右排序，保证横向/多行 Sprite Sheet 播放顺序稳定。

### [S109] 知识沉淀

1. **Asset Import Pipeline 的拖拽入口必须支持外部文件路径**：Unity 编辑器拖入电脑外部 PNG/JPG 时，`DragAndDrop.paths` 给到的是项目外绝对路径，不能只用 `AssetDatabase.LoadAssetAtPath` 读取；必须先复制进 `Assets/Art/Imported` 再 `ImportAsset`。
2. **拖拽失败必须有前台反馈**：用户看到按钮灰掉时无法判断是没拖中、格式不支持还是路径没导入；S109 新增窗口底部结果提示和 Console 日志，避免“没反应”。
3. **手动“添加贴图/添加文件夹”也应走同一套入口**：否则外部文件选择器选到项目外素材时仍然会静默失败。

### [S108] 知识沉淀

1. **Test Console 调用 SEF 编辑器窗口时必须建立 Editor asmdef 引用**：`SEF_QuickApply` 与 `SpriteEffectFactoryWindow` 源码存在，但它们编译在 `SpriteEffectFactory.Editor` 程序集中；`MarioTrickster.Editor` 未引用该程序集时，Unity 会在 `TestConsoleWindow` 报 `CS0103`。
2. **`TextureImporter.spritesheet` 历史警告已在 S117 清理**：`AI_SpriteSlicer` / `AssetImportPipeline` / `TA_AssetValidator` / `PlannerProductionAssistant` 已迁移到 `UnityEditor.U2D.Sprites.ISpriteEditorDataProvider` 桥接层；后续新增切片读写不得回退旧 API。
3. **视频 `Color primaries` 提示不是 C# 编译错误**：它来自 `idle_drive.mp4` 的媒体元数据，Unity/WindowsMediaFoundation 会回退默认色彩配置，最多可能带来轻微色偏，不影响脚本编译。

### [S107] 知识沉淀

1. **SEF Quick Apply 效果不可见的根因是三重缺失**：(a) SpriteEffectController 缺 `[ExecuteAlways]` 导致编辑器模式 LateUpdate 不执行；(b) 应用预设后未调用 `EditorSyncProperties()` 导致 keyword 未同步到 Material；(c) Shadow 效果在 shader fragment 中完全没有实现代码。三者同时存在才导致"看不出任何效果"。
2. **素材应用到已有物体的核心原则是"只换皮不换骨"**：替换 SpriteRenderer 贴图 + SEF Material，但保留所有已有的行为组件（BaseHazard/DamageDealer/ControllableHazard 等）。碰撞体自动按新贴图尺寸重新适配。
3. **展位资产的行为模板应支持自动推断**：根据物体已有组件（ControllableHazard→爆炸、SawBlade→锯片、DamageDealer→接触伤害）自动选择对应模板，用户无需手动配置。
4. **ExplosiveHazard 继承 BaseHazard 而非 DamageDealer**：因为爆炸是范围伤害，需要 Physics2D.OverlapCircleAll，而不是单体碰撞。同时继承 BaseHazard 获得防刷屏保护。

### [S106] 知识沉淀

1. **素材到 Object 的核心矛盾是“步骤多”而非“技术难”**：规范化、切片、设置 Pivot、创建 Object、挂载组件、保存 Prefab 每一步都简单，但串联 6 步的操作成本很高；解法是封装为一键流程。
2. **效果选择与应用应解耦**：SEF Quick Apply 证明“选预设→自动应用→自动保存蓝图”可以把用户决策压缩到“点一下”，用户精力应只放在 shader 效果选择上。
3. **商业素材的最大痛点是“合集图”**：一张大图包含多个不同 Object，必须先裁切再导入；Python 工具的 `--auto` 模式（连通域检测）是最低成本方案。
4. **视碰分离架构必须贯穿到导入工具**：生成的 Object 必须遵守 S37 的 Root + Visual 分层，否则与现有关卡生成/主题换肤系统不兼容。

### [S105] 知识沉淀

1. **防裁切不能只盯 Blender 端**：即使上游已有 `padding=1.40`，下游图生视频仍可能把角色重新“贴回画框边”；因此真正给 QC 喂帧之前，必须在 `02_nobg` 阶段基于**全序列联合包围盒**再做一次统一安全构图重排。
2. **颜色回正必须前移到帧级**：若只在 `Sprite Sheet` 收口阶段做一次整图匹配，`auto_qc.py` 读取到的仍是发灰、掉饱和的中间帧，结果会出现“成图已写回但 QC 仍判色偏”的假失败；所以参考图 Histogram Matching 必须前移到 `02_nobg`，最终整图写回只作为保险。
3. **最大连通域是去脏边的低风险手术**：对于 rembg 后的零散脏像素，与其放宽 QC 阈值，不如先保留前景最大连通域并重建 Alpha；这样既能压掉边缘误检，又不会破坏角色主体轮廓。

---

## 2. 回归验证清单

> 用户测试时逐项快速验证。AI 修复代码后只需在此标记受影响项。
>
> **S74 说明**：本次为美术教程蒸馏落库（テレコム《アニメーション・バイブル》），**未改动运行时代码**；下表状态保持不变。新增30条规则主要影响未来美术资产生产。核心影响：動画16条(振り向き立体意識/各種歩き・走りバリエーション/カメラワーク)はsprite sheetアニメーション生産に直結、透過光法則はTrickster幽霊形態に直結、マルチプレーンカメラはUnity Parallaxに直结、画面動はボス戦VFXに直結。冲突仲裁0条：全規則既有と補完関係。
>
> **S91 说明**：本次仅沉淀 `trickster_style` 的本地验证标准作业模板，**未改动运行时代码**；下表状态保持不变。
>
> **S92 说明**：本次新增项目交接总览文档，澄清“关卡主线未消失、美术是支线供给系统”的桥接关系，**未改动运行时代码**；下表状态保持不变。
>
> **S93 说明**：本次新增 `research/COMFYUI_DISTILLATION_TO_ANIMATION_IMPLEMENTATION_GUIDE_2026-04-12.md`，汇总 Reddit / GitHub / ComfyUI 官方资料，对“教程/书籍蒸馏为何难以直接转成动画效果”给出工程化解释，并固定四条推荐落地路线：`单图肖像驱动`、`双图角色短动作`、`单图伪3D场景/物件`、`设定图批量衍生`。**未改动运行时代码**；下表状态保持不变。
>
> **S94 说明**：本次新增 `docs/PIPELINE_ALIGNMENT_AND_ART_LANDING_PLAYBOOK.md`，明确项目后续必须采用 **A 轨关卡白盒主线 / B 轨美术使能支线 / C 轨资产回接桥接层** 的双轨并行、单主线治理模型，并把正式资产的最小接回定义固定为 **目标槽位 / 目录位置 / 命名规则 / 导入参数 / 废弃条件**。**未改动运行时代码**；下表状态保持不变。
>
> **S95 说明**：本次统一 `README.md` 与 `docs/ART_PIPELINE_GUIDE.md` 的当前阶段起步口径，明确 **“从零开始”总入口继续保留，但 MarioTrickster 当前默认不再从总入口启动**；此阶段应优先使用 **LoRA 本地验证入口** 或 **白盒关卡续航入口**。同时把执行顺序固定为：**先完成 `trickster_style` 本地验证，再以首批命名资产做切片量产；关卡白盒 meanwhile 持续推进。** **未改动运行时代码**；下表状态保持不变。
>
> **S96 说明**：本次新增 `docs/STANDARD_CONVERSATION_PROTOCOL.md` 与 `prompts/STANDARD_PROJECT_PROMPTS.md`，并更新 `README.md` 导航，把后续所有“更新 / 升级 / 优化项目”的新对话统一约束为：**先读档恢复上下文，再判断阶段与边界、给出计划、执行、验证、落库与收口**；用户的自定义问题统一放入模板末尾补充插槽。**未改动运行时代码**；下表状态保持不变。
>
> **S97 说明**：本次完成 `trickster_style` LoRA 本地验证闭环。用户按工单跑完 30 张测试图（3 题材 × 4 模式 × 3 权重 + 3 基线），AI 审图后形成完整结论：推荐权重（场景 0.8 / 角色 0.6~0.8 / 道具 0.6）、触发词甜区（B1/B2 均可，B3 在非角色 Prompt 下会拉向角色化）、污染物清单（等距视角/橙砖路/红屋顶/自动注入小角色/圆球树冠）、专属去污词四组。结论已落库到 `PROMPT_RECIPES.md` 资产卡。同时将工单派发中暴露的三个流程问题固化到 `WORKORDER_QA_STANDARD.md` 第九条。**未改动运行时代码**；下表状态保持不变。

| 状态 | 测试项 | 关键验证点 |
|:----:|--------|-----------|
| 🔄 | 测试 0：动画管线 12GB 烟测 | **S100重点验证**: 默认 `480×480 / 17帧 / 6步` 可直跑；`jump` 自动走 `416×544 / 17帧 / 6步`；手动传入超预算参数时日志出现 `[12GB护栏]` 且不会爆显存 |
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
| 🔄 | EditMode 自动化 | **S120重点验证**: `ArtAssetClassifierTests` idle/run/jump/fall 边界测试、主角单 RUN 状态动画测试、商业语义状态摘要测试、道具 idle 与特效 cast 防误判测试通过；Apply Art 对主角应用单组 RUN 仍挂 `SpriteStateAnimator`，普通非角色多帧素材仍回退 `SpriteFrameAnimator`；Unity Editor 重新编译无新增红错 |
| 🔄 | PlayMode 自动化 | **S53重点验证**: 26/26 通过 + 柔性模式下应看到 S53 耗时校验日志 |
| 🔄 | AnimPipeline：idle 自动生成链路 | **S105重点验证**: 删除/改名 `assets/videos/idle_drive.mp4` 后执行 `python run_pipeline.py --action idle`，应触发 Blender 从 `Breathing Idle.fbx` 重建 drive video；日志中需出现“有效可渲染网格数”“动作振幅已放大 1.30x”与 `padding=1.40` 提示，若为 animation-only FBX 则继续出现“自动生成代理人体”；`02_nobg` 阶段还应新增“安全构图重排”“逐帧回正”日志；最终 `final_no_alpha.png` 应成功写回，QC 仍保持 `480×480 / 17帧 / 6步`，且成图颜色不再发灰、头顶/帽檐/武器不再轻易裁切、微动作观感不回退 |

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
| **最高** | **`trickster_style` 本地验证闭环**：用户已完成 30 张测试图出图与回传，AI 已完成全部审图与判定。触发词甜区、推荐权重、污染物清单与专属去污词均已实测落库到 `PROMPT_RECIPES.md`。同时本轮还将工单派发中暴露的三个问题（全局设置必须从用户工作流截图提取、文件命名利用自动编号、回传模板禁止要求用户填写主观判定项）固化到 `WORKORDER_QA_STANDARD.md` 第九条。 | ✅ 已完成（S97） |
| **高** | **标准提示词体系与固定入口落地**：后续所有“更新 / 升级 / 优化项目”的新对话必须优先走标准协议与模板库，禁止再以过度随意的自然语言直接开局；用户自定义问题保留在补充插槽，不得替代固定骨架。 | ✅ 已完成（S96 已写入 `docs/STANDARD_CONVERSATION_PROTOCOL.md`、`prompts/STANDARD_PROJECT_PROMPTS.md`、`README.md` 与本文件） |

| **高** | **换号续接极速化**：新对话默认直接提供仓库地址 + token + 当前任务，避免中途补条件；仓库首页还需给出“七类白话入口 / 是否必须落库 / 落到哪 / 何时自动 push”的一页总表。 | ✅ 已完成（S90 已写入 `README.md`、`PROMPT_RECIPES.md` 与本文件） |
| **高** | **白话入口后端联动闭环**：从“从零开始”“探索 30 张”“Civitai 页面填写”“本地验证”“登记入库”到“正式量产”，都必须对应后端阶段、落库位置与推送触发条件，不能只留前端口令。 | ✅ 已完成（S89 已写入 `ART_PIPELINE_GUIDE.md`、`PROMPT_RECIPES.md` 与本文件） |
| **高** | **旧版优秀提示词后台保留**：允许前台对白话入口做极简化，但后台必须按项目效率与质量原则保留仍有价值的旧规则，禁止把四区隔离、分流逻辑、结构保护、参数甜区与失败模式一起清洗掉。 | ✅ 已完成（S90 已写入 `PROMPT_RECIPES.md` 与本文件） |
| **高** | **概念锚点出图**：使用 `PROMPT_RECIPES.md` 中的概念锚点蓝图在 ComfyUI/Midjourney 出图，满意后记录 Seed，保存到 `Assets/Art/Reference/Reference_Anchor.png`。 | 🚀 工单已派发，等待用户本地出图 |
| **高** | **美术蒸馏 GitHub 闭环**：菜单 1 执行后必须在仓库内改 `prompts/PROMPT_RECIPES.md`、同步更新 `SESSION_TRACKER.md`、提交并推送远端；临时 OCR / 摘录文件不入库。 | ✅ 已完成（S62 协议增强 + S63 Hart 蒸馏 + S64 Telecom 蒸馏 + S65 松岡蒸馏 + S66 砂糖蒸馏 + S67 みにまる蒸馏 + S68 OCHABI蒸馏 + S69 Peter Han蒸馏 + S70 吉田誠治蒸馏 + S71 Telecom第二輪深度蒸留 + S72 室井康雄蒸留 + S73 バニリゾ蒸留 + S74 テレコムBible蒸留 + S75 室井康雄第二輪蒸留 + S76 松岡伸治《エフェクトグラフィックス》蒸留） |
| **高** | **LoRA 训练路线研究与性价比判断**：围绕 Civitai / LiblibAI 在线训练、本地 4070 自训、云 GPU 自训与继续探索方案完成调研，结论见 `LoRA_Training_Decision_Report.md`；本轮又把“本地训练参数 / Civitai 页面填写 / 训练排障”三类求助入口补进白话速查表，避免用户重复追问同类问题。 | ✅ 已完成 |
| **高** | **Level Studio × SEF 编辑器入口编译修复**：Art & Effects Hub 内的 SEF Quick Apply / Sprite Effect Factory 按钮因缺少 `SpriteEffectFactory.Editor` asmdef 引用触发 `CS0103`，S108 已补引用。 | ✅ 已完成（S108） |
| **高** | **Asset Import Pipeline 外部素材拖入修复**：用户截图反馈拖入素材没反应，S109 已让拖拽区和“添加贴图/文件夹”同时支持项目外图片，自动复制进 `Assets/Art/Imported` 并给可见反馈。 | ✅ 已完成（S109） |
| **高** | **Sprite Sheet 应用后只显示第一帧修复**：S110 新增 `SpriteFrameAnimator`，导入新 Object 和应用到已有物体时自动挂载多帧播放组件。 | ✅ 已完成（S110） |
| **最高** | **商业素材统一分类与 SEF 丝滑切换**：S111 新增 `ArtAssetClassifier` + `SpriteStateAnimator` + Marker 扩展字段，让角色/道具/陷阱/背景/场景动画按用途落位；SEF Quick Apply 增加切换前清状态、一键还原与美术效果锁定保护。 | ✅ 已完成（S111） |
| **最高** | **ArtAssetClassifier EditMode 回归修复**：用户报告 112 个 EditMode 中 2 个失败；S112 已全局修复状态帧分组污染与 `background`/`ground` 子串误判，避免角色状态动画落成 Loop、背景素材落成 Platform。 | ✅ 已修复，待用户 Unity 重跑确认 |
| **最高** | **策划高速关卡生产入口**：S113 新增 `docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md`，把“做关卡、换商业素材、接人物/机关动画、让 AI 后台补机制”的路径合并为面向策划的一页式功能介绍与教程；README 与 LevelStudio 指南已加入口。 | ✅ 已完成（S113） |
| **高** | **Apply Art 选中归一优化**：S113 已让 `Apply Art to Selected` 在用户选中 Root 或 Visual 时都自动回到行为 Root 执行换皮，降低 Visual 模式下把标记/碰撞/Prefab 写错层的风险。 | ✅ 已完成（S113） |
| **最高** | **策划生产助手短板优化**：S114 新增 `PlannerProductionAssistant`，把商业素材包命名混乱、Theme Profile 手动填槽、新机制请求不稳定三项短板压成一个 Unity 内窗口入口。 | ✅ 已完成（S114，待 Unity 实机验证） |
| **最高** | **角色状态动画自动挂载与商业状态兼容**：S115 补强 `idle/run/jump/fall` 命名分类边界测试；S119 追加主角单组 RUN 也能状态驱动；S120 把攻击、受伤、死亡、施法、技能特效、潜行、伪装、融入环境、道具开关等商业素材状态纳入分类摘要和文档，不改变现有运行行为。 | ✅ 已完成（S120，待 Unity 实机验证） |
| **高** | **批量资产生产**：`trickster_style` 已验证通过，可进入首批量产。需先确定目标槽位（如地刺、平台、背景等），补齐接回定义（目标槽位 / 目录位置 / 命名规则 / 导入参数 / 废弃条件），然后启动窄切片量产。量产时配合去污词使用，道具类需加强 Prompt 约束。 | 🚀 验证已通过，等待确定首批槽位后启动 |
| **高** | **ComfyUI 蒸馏→动画资产工程化**：不要继续把教程蒸馏停留在摘要层，需把现有动画/透视/镜头蒸馏结果重写成 `任务卡 + 工作流模板 + 参数甜区 + 故障树`。推荐先建立四条窄工作流：`单图肖像驱动`、`双图角色短动作`、`单图伪3D场景/物件`、`设定图批量衍生`；再逐步扩成可组合的生产线。**S94 追加约束**：这条支线必须绑定已命名资产需求推进，不得再以“大而全万能动画流”为默认目标。 | 🚀 主干已能跑通；S105 已继续把稳定性前移到 `02_nobg` 阶段，补齐 **全序列安全构图重排、帧级颜色回正、最大连通域去脏边** 三项返修。当前等待用户实机验证 QC 是否已解除 crop / color 失败，并确认微动作观感未因安全缩放而回退 |
| **最高** | **美术资产独立仓库分离执行**：`tyu` 已改名为 `MarioTrickster-Art`，通过 git-filter-repo 拆分 93 条历史提交并推送，配置 Git LFS，主仓库清理已迁移目录并挂载 Submodule 到 `Assets/MarioTrickster-Art`，各原目录已留下面包屑索引。 | ✅ 已完成（S98） |
| **高** | **素材导入自动化管线**：从外部购买/下载的素材到项目可用 Object + Prefab 蓝图的全链路工具。包含 Asset Import Pipeline、SEF Quick Apply、Python 裁切工具、拖入自动触发。 | ✅ 已完成（S106） |
| **高** | **素材应用到已有物体 + 展位行为自动配置 + SEF 效果修复**：(1) Apply Art to Selected 窗口（Ctrl+Shift+A）；(2) ExplosiveHazard 爆炸型陷阱组件；(3) 行为模板自动推断与挂载；(4) SEF Quick Apply 编辑器可见性修复。 | ✅ 已完成（S107） |
| **最高** | **恢复关卡白盒主线**：直接重新启用 `Level Studio / Custom Template Editor / Build From Text`，基于现有 ASCII 模板库与片段库继续拼装、测试并迭代 1~2 个完整关卡段；美术换肤只服务于已验证白盒，不再反向阻塞关卡设计。**S94 已固定执行原则**：A 轨（关卡白盒）永远是唯一上游任务源。**S95 已固定默认入口**：若当前要推项目总主线，应直接从“先继续白盒关卡，不等最终美术”这类窄入口启动。 | 🚀 已形成桥接结论，当前可立即恢复执行 |
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
| **README.md** | 唯一用户入口：GitHub 搜 `read` 时只看这个文件，从这里判断该看哪份指南。 |
| **SESSION_TRACKER.md**（本文件） | AI 入口：状态、规范、回归、待办与最新知识沉淀。 |
| **docs/AI_TAKEOVER_PROTOCOL.md** | 新 AI 接手协议：读档顺序、后台执行原则与文档地图。 |
| **docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md** | 日常生产入口：做关卡、换素材、批量整理、接新机制。 |
| **docs/ASSET_IMPORT_PIPELINE_GUIDE.md** | 素材导入入口：Object/Prefab 生成、Sprite Sheet、角色状态动画命名与挂载。 |
| **AI_WORKFLOW.md** | Git 与故障反馈附录：pull/push、冲突、代理、Unity 报错反馈模板。 |
| `Assets/SpriteEffectFactory/SEF_GUIDE.md` / `Assets/anim_pipeline/MarioTrickster_AnimPipeline/PIPELINE_GUIDE.md` | 内部维护指南：只在维护对应工具时阅读，不作为用户主入口。 |
| GAME_DESIGN.md | 游戏设计文档。 |

---

## 8. 新对话开场模板

```text
主代码库：https://github.com/jiaxuGOGOGO/MarioTrickster
美术仓库：https://github.com/jiaxuGOGOGO/MarioTrickster-Art
Token：[如需推送则填写]

你先按 docs/AI_TAKEOVER_PROTOCOL.md 和 SESSION_TRACKER.md 静默读档接手。
本次任务：[用大白话写需求]
```

> AI 收到后：先读本文件与 `docs/AI_TAKEOVER_PROTOCOL.md` → 按任务类型补读专项文档 → `git log --oneline -n 10` 回溯近期变更 → 执行任务 → 更新本文件并提交推送。

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

## [2026-04-13] [S105] Idle 管线视觉降维重构：VAE 安全渲染 + Global Auto-Trim + 安全色彩回正
### 问题背景
S104 本地烟测证明工程护栏已生效（代理接管、动作振幅放大、无网格兜底均正常），但最终成图暴露出视频模型的潜空间认知缺陷：**空手 FBX 触发 Proxy 接管后，巨帽/长锤等外轮廓特征被吞；随后直方图回正又把缺失暗部错误摊到角色浅色区域，最终出现 5/5 裁切与黑化污染。**
### 根因分析
| 问题 | 根因 | S105 处理 |
|------|------|-----------|
| Proxy 仍会吞帽子/武器 | 1.4x 留白只覆盖肉身 bbox，无法兜住 AI 脑补出的高帽/长锤外扩体积 | Proxy 接管时将 Blender 正交 padding 提升到 **2.5x** |
| 潜空间曝光错乱 | 纯白代理体 + 纯绿背景对低饱和参考图反差过大 | 驱动视频改为 **中灰发光代理体 + 深灰背景** |
| 留白过大导致角色占比塌陷 | 仅增大 padding 会把主体整体缩小，并留下大量透明废边 | 在 `02_nobg` 阶段新增 **Global Auto-Trim**，按全序列统一 bbox 裁切 |
| 颜色中毒/黑化 | `match_histograms` 强制复制暗部体积分布，缺失的帽子/武器暗像素被摊到皮肤与衣物 | 废除直方图算法，改为 **非透明区域 Mean RGB Shift**，失败时保留原色 |
### 修改文件
| 文件 | 变更内容 |
|------|---------|
| `Assets/anim_pipeline/MarioTrickster_AnimPipeline/blender_render_drive_video.py` | 代理材质从纯白改为中灰；默认绿幕分支改为深灰背景；Proxy 接管且有效网格为 0 时强制 `ortho_padding >= 2.5` 并打印日志 |
| `Assets/anim_pipeline/MarioTrickster_AnimPipeline/run_pipeline.py` | 移除 02_nobg 阶段的安全缩放重排；新增逐帧前景清理 + **Global Auto-Trim**；废除直方图回正，统一改为安全均值平移写回 |
### 本地验证
1. `python3.11 -m py_compile run_pipeline.py blender_render_drive_video.py` 通过。
2. `python3.11 /home/ubuntu/validate_s105_patch.py` 通过：
   - Global Auto-Trim 统一裁切 bbox 成功，3 帧输出尺寸一致。
   - 极端暗参考图下安全均值平移仅执行 `-40 / -40 / -40`，输出均值仍为 `185 / 175 / 165`，未出现黑化。
   - Blender 脚本文本检查确认中灰代理体、深灰背景、Proxy `2.5x` 留白日志均已落地。
### 待用户烟测确认
- 重新拉取最新 `master` 后，用同一套 idle 素材复跑本地烟测，重点观察：
  1. Blender 日志是否出现 `padding=2.50`
  2. `final_no_alpha.png` 是否摆脱大面积透明废边
  3. 帽子/长锤是否仍被吞，角色是否恢复正常浅色而不再黑化
