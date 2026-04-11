# MarioTrickster 美术管线蒸馏流程优化方案 (V4.1)

## 1. 效率瓶颈分析 (S75-S79 案例复盘)

在 S75 至 S79 的五轮蒸馏闭环中，系统暴露出了显著的算力消耗不平衡问题。前三轮（S75-S77）的新规则提取阶段总计耗费约 22 轮工具调用，成功写入 36 条高价值规则。然而，随后的查缺补漏环节（S78-S79）却消耗了 27 轮工具调用，仅补入 17 条遗漏规则。

这种头重脚轻的算力消耗模式主要源于以下系统性瓶颈：

第一，**并行精读与合并阶段的结构化缺失**。并行处理大部头 PDF（如 250 页的《アニメーション・バイブル》）虽然极大提升了阅读速度，但由于缺乏严格的 JSON Schema 约束，各子任务返回的规则名称格式混乱（混杂中、日、英及特殊符号）。这导致合并去重脚本产生高达 90% 的假阳性误判（168 条疑似遗漏中 151 条为误报），迫使系统在二次审计中耗费大量算力进行人工比对。

第二，**高频次的文件检索与全量读取**。每次写入新规则前，系统都需要反复使用 `grep` 命令定位插入点（行号），且由于 `PROMPT_RECIPES.md` 文件体积不断膨胀，频繁的全量读取导致上下文窗口压力剧增，拖慢了响应速度。

第三，**手工字符串替换的脆弱性**。目前更新 `SESSION_TRACKER.md` 依赖于 `sed` 或 Python 脚本的硬编码字符串替换，极易因格式微调或特殊字符导致脚本崩溃，进而引发多次试错重试。

## 2. 业界最佳实践借鉴

为了重构蒸馏流程，我们引入了 2025-2026 年间业界在 AI 生产管线优化方面的几项核心最佳实践：

**增量编译与知识去重前置**
参考 Andrej Karpathy 提出的 LLM 知识库增量编译架构 [1]，我们应放弃“先全量提取，后全量比对”的低效模式。在并行精读阶段，应直接将已有的 `PROMPT_RECIPES.md` 核心目录作为上下文输入给子任务，要求子任务在提取时即刻完成初步去重，仅返回真正的新知识。

**强制结构化输出 (Structured Outputs)**
现代数据提取管线普遍采用强制 JSON Schema 输出 [2]。通过在并行任务中定义严格的字段格式（如 `rule_name` 必须为统一的日文原名，`tags` 必须为小写下划线格式），可以彻底消除合并阶段因格式差异导致的假阳性误判，使后续的自动化比对脚本达到 100% 的准确率。

**管道化文件更新 (Pipeline Automation)**
Cohorte Projects 的 ComfyUI 生产指南指出，可靠的管线必须消除人工介入的脆弱环节 [3]。对于 Markdown 文件的更新，应采用基于标记（Marker）的定向注入，而非依赖脆弱的正则表达式或行号匹配。

## 3. 蒸馏流程优化方案 (V4.1)

基于上述分析，我们对菜单 1（喂书蒸馏）的提示词和执行流程进行以下重构：

### 3.1 提示词重构：引入 Schema 与前置去重

修改触发蒸馏的复制粘贴提示词，增加强制结构化输出要求：

> 0. **PDF 拆分并行精读**：收到文件后，先按章节拆分并行精读。**【效率优化】**：在下发并行任务时，必须要求子任务输出严格的 JSON Schema（包含 `rule_name_jp`, `core_tags`, `drawer_category`）。
> 1. **前置去重**：子任务在提取前，必须先比对已有的规则名，若为同义词或上位概念覆盖，直接丢弃，不返回结果。

### 3.2 流程优化：标记化写入与批量提交

放弃使用 `grep` 查找行号，改为在 `PROMPT_RECIPES.md` 中设置不可见的 HTML 注释标记作为锚点。

| 抽屉分类 | 插入锚点标记 |
|---|---|
| 解剖与形态 | `<!-- INSERT_ANATOMY_RULES_HERE -->` |
| 透视与物件 | `<!-- INSERT_PERSPECTIVE_RULES_HERE -->` |
| 动画与物理 | `<!-- INSERT_ANIMATION_RULES_HERE -->` |
| 光影与材质 | `<!-- INSERT_LIGHTING_RULES_HERE -->` |
| AI 硬核参数 | `<!-- INSERT_PARAMS_RULES_HERE -->` |

当合并脚本完成去重后，直接读取整个 Markdown 文件，通过字符串替换 `replace('<!-- INSERT_XXX -->', new_rules + '\n<!-- INSERT_XXX -->')` 实现精准注入。这不仅将写入操作的工具调用次数从 5-8 次降至 1 次，更彻底消除了因行号变动导致的写入错误。

### 3.3 追踪器更新自动化

对于 `SESSION_TRACKER.md` 的更新，统一使用标准的 Python 脚本进行结构化更新，避免使用脆弱的 `sed` 命令。通过预定义更新模板，确保每次 Session 闭环的记录格式完全一致。

## References

[1] MindStudio. "What Is Andrej Karpathy's LLM Knowledge Base Architecture." 2026. https://www.mindstudio.ai/blog/karpathy-llm-knowledge-base-architecture-compiler-analogy/
[2] Generative AI Newsroom. "Structured Outputs: Making LLMs Reliable for Document Processing." 2024. https://generative-ai-newsroom.com/structured-outputs-making-llms-reliable-for-document-processing-c3b6b2baed36
[3] Cohorte Projects. "The ComfyUI Production Playbook." 2025. https://www.cohorte.co/blog/the-comfyui-production-playbook
