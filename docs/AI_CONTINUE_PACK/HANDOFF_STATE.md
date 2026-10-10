# 可迁移交接状态

> 新电脑或新 Manus 账号接续时，先读本文件、`SESSION_TRACKER.md` 与 `SKILL.md`。Git 分支和提交是最终事实来源；本文件只描述当前交接状态与验证边界。

## 仓库身份

| 项目 | 值 |
|---|---|
| 规范远端 | `https://github.com/jiaxuGOGOGO/MarioTrickster.git` |
| 规范分支 | `genspark_ai_developer` |
| Unity 版本 | `2022.3.61f1` |
| 已废弃依赖 | 不拉取、不依赖 `MarioTrickster-Art` 子模块 |

## 当前基线（2026-10-10）

- Git 基线提交：`49e5b87d142ea144a4336bdb0f661160c6b84f05`（S246）。
- 在 Windows Unity 编辑器中，EditMode：**357 / 357 通过，0 失败，0 跳过**。
- 已验证但仍待用户审阅/提交的最小修改：
  1. `Assets/Tests/EditMode/Step1RushMarioTests.cs`：S240 数据版本断言由严格等于 29 改为不低于 29；
  2. `Assets/Scripts/Gameplay/Step1/Step1Lighting.cs`：移除会误触发隔离检查的实现名称注释。
- Unity 首次导入会生成 121 个未跟踪 `.meta`；它们是独立的仓库卫生项，**不要**与上述两项修改混合提交。下一位维护者应先做 GUID 冲突和引用检查，再单独处理。

## 接续前强制检查

```powershell
git status --short --branch
git log -1 --oneline
powershell -ExecutionPolicy Bypass -File docs\AI_CONTINUE_PACK\scripts\bootstrap_windows.ps1
```

然后在 Unity Hub 打开当前 Git 工作副本并运行 EditMode。新 AI 在实现前必须报告：当前 commit、工作树改动、最近测试、未完成项与风险。

需要迁移前，优先运行 `scripts/save_checkpoint.ps1 -Mode Save -Push`。该工具会创建一个带测试摘要的 Git checkpoint，并排除未经审计的 Unity `.meta`；无法 push 时使用 `-Mode Export` 生成补丁交接。

## 提交纪律

- 每次开发结束，更新本文件中“当前基线”的提交、测试和未完成项。
- 所有可交接成果必须推送 GitHub；未 push 的工作必须保存 `.patch` 或 `.bundle`。
- 不提交 `Library/`、`Temp/`、`Logs/`、用户私有凭据或本机许可证。
- 不把未审计的 Unity 自动生成 `.meta` 混入功能提交。
