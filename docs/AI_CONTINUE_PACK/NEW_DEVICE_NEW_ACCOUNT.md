# 新电脑 / 新 Manus 账号接续指南

## 结论：以 GitHub 为真相源，接续包为启动说明

**不要把旧电脑、旧 Manus 账号、聊天记录或本地工作区当作项目的唯一存档。** 它们都可以丢失或无法访问。

| 层级 | 承载内容 | 是否跨电脑 / 跨 Manus 账号 |
|---|---|---|
| GitHub `genspark_ai_developer` | 代码、Unity 资产、文档、脚本、已确认的进度 | 是；**唯一真相源** |
| `SESSION_TRACKER.md` + `docs/AI_CONTINUE_PACK/HANDOFF_STATE.md` | 当前进度、正在做什么、测试结果、已知风险 | 是；必须随提交更新 |
| `docs/AI_CONTINUE_PACK/` | 给新 AI 的规则、设计宪法、按任务读取的参考与启动流程 | 是；随仓库克隆 |
| Manus Skill | 让新 Manus 更快知道何时读哪些仓库文件 | 可导入新账号，但只是加速器，不保存项目状态 |
| Unity / Git / GitHub 登录凭据 | 本机用户级权限 | 否；新电脑或新账号必须重新授权，绝不写入仓库 |

## 每次停止开发前的“可迁移完成”标准

只有同时满足以下条件，才认为本次工作不会因换电脑或换 Manus 账号中断：

1. `git status --short` 没有未解释的文件；
2. 代码、文档和必要的 `.meta` 已按逻辑拆分提交；
3. 分支已经 `git push origin genspark_ai_developer`；
4. `SESSION_TRACKER.md` 的最新状态和 `HANDOFF_STATE.md` 已更新；
5. 写明已运行的 Unity 测试、结果、未运行项和已知风险；
6. 任何尚未准备推送的改动都有可验证的 `git diff --binary` 补丁或 `git bundle` 备份。

> 未 push 的修改只存在于旧电脑。新 Manus 账号无法从聊天记录或旧账号自动恢复这些修改。

## 一键项目存档

在仓库根目录运行“存档”工具。它会读取 Git 状态和最新 `TestReport.txt`，拒绝把未经审计的 Unity `.meta` 自动混入提交。

```powershell
# 只查看此时是否适合存档；不改任何文件
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\save_checkpoint.ps1 -Mode Status

# 真正保存：提交已跟踪的开发修改、接续包与最新状态，并同步到 GitHub
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\save_checkpoint.ps1 -Mode Save -Push -Message "checkpoint: 描述本次完成内容"

# 无法联网时：导出可携带的补丁和清单到 .handoff/，带到新电脑或附到新账号对话
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\save_checkpoint.ps1 -Mode Export
```

`Save` 不会自动提交新的非 `.meta` 文件，避免把未知资产或玩家内容误存入 Git；它会明确列出这些文件，要求先审阅。首次使用前，正常 Windows 用户的 Git 必须已有 `user.name` 与 `user.email`。

## 新电脑启动步骤

### 1. 取得代码

```powershell
git clone --branch genspark_ai_developer --single-branch https://github.com/jiaxuGOGOGO/MarioTrickster.git
cd MarioTrickster
```

如果 GitHub 使用了新的账号：把新账号加入仓库协作者，或 fork 后把 `origin` 改为自己的 fork。不要把 PAT、SSH 私钥或 Unity 许可证写入仓库。

### 2. 安装/确认本机工具

- Unity Hub + **Unity 2022.3.61f1** + Windows Standalone Support；
- Git；
- Visual Studio 2022 的 Unity 游戏开发工作负载；
- Python 3.12+（涉及 Python 辅助工具时）；
- Node.js（涉及网页设计台时）。

在仓库根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\bootstrap_windows.ps1
```

该脚本只检查环境与仓库状态，不安装软件、不修改 Git 配置、不写入凭据。

### 3. 打开和验证 Unity

1. 在 Unity Hub 用 **Open / 从磁盘打开** 选择该新克隆目录；
2. 等待首次导入完成；
3. 打开测试中心：`Ctrl+Alt+T`；
4. 运行 EditMode；涉及物理、输入、镜头或实际玩法时再运行 PlayMode、试玩和“马里奥自己跑”。

### 4. 在新 Manus 账号开始对话

在新对话第一条消息贴入下面的提示，并附上仓库链接或本地克隆目录：

```text
继续开发 GitHub 仓库 jiaxuGOGOGO/MarioTrickster 的 genspark_ai_developer 分支。
请先读取 SESSION_TRACKER.md、docs/AI_CONTINUE_PACK/HANDOFF_STATE.md、
docs/AI_CONTINUE_PACK/SKILL.md 和与本次任务对应的 references 分册。
以 Git HEAD 和仓库内文档为事实来源，不依赖任何旧 Manus 账号或历史对话。
先报告：当前 commit、工作树状态、最新测试结果、未完成事项和风险；
然后再开始实现。不要拉取或依赖已废弃的 MarioTrickster-Art 子模块。
```

## 尚未推送时如何交接

换电脑之前，优先提交并推送。如果必须中断，可在旧电脑仓库根目录执行：

```powershell
git diff --binary > MarioTrickster-handoff-unpushed.patch
git bundle create MarioTrickster-handoff.bundle origin/genspark_ai_developer..HEAD
```

把 `.patch` 或 `.bundle` 作为文件带到新电脑或新账号对话。新电脑验证后分别使用：

```powershell
git apply --check MarioTrickster-handoff-unpushed.patch
git apply MarioTrickster-handoff-unpushed.patch
# 或
git fetch MarioTrickster-handoff.bundle HEAD:handoff/recovered
```

## Manus Skill 的正确用法

仓库内现有 `docs/AI_CONTINUE_PACK/SKILL.md` 已是项目专用接续包。应把它导出/保存为可导入的新账号的 `.skill` 包，作为**可选的启动加速器**；但不能只依赖它，因为 skill 不会自动同步你当天未 push 的代码、测试报告或 Git 状态。

推荐策略：

1. **GitHub + 仓库内接续包：必需。**
2. **导入同一接续包为 Manus Skill：推荐。** 新账号能更快自动加载规则。
3. **每次提交后更新 `HANDOFF_STATE.md`：必需。**
4. **旧/新账户之间临时传单个未上传补丁：可使用 Handoff 或附件。** 这不是持续同步方案。
