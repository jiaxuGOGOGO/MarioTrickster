# AI_WORKFLOW

> **定位**：本文现在只作为 Git 操作和故障反馈附录保留，不再是 AI 接手项目的主入口。新 AI 接管请读 [SESSION_TRACKER.md](./SESSION_TRACKER.md) 与 [docs/AI_TAKEOVER_PROTOCOL.md](./docs/AI_TAKEOVER_PROTOCOL.md)；日常做关卡和换素材请读 [docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md)。[1] [2] [3]

---

## 1. 什么时候看这个文档

| 场景 | 是否看本文 | 更权威的入口 |
| --- | --- | --- |
| 让 AI 继续开发、修 Bug、更新项目 | 不需要。 | [SESSION_TRACKER.md](./SESSION_TRACKER.md) |
| 新 AI 第一次接手 | 不需要。 | [docs/AI_TAKEOVER_PROTOCOL.md](./docs/AI_TAKEOVER_PROTOCOL.md) |
| 不知道怎么做关卡、换素材、接机制 | 不需要。 | [docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md](./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md) |
| Git pull、push、冲突、代理报错 | 需要。 | 本文。 |
| Unity 报错需要反馈给 AI | 可参考。 | 本文的故障反馈模板。 |

项目过去的长模板、复杂交接话术和多文档同步规则已经收束到 `SESSION_TRACKER.md` 与 `AI_TAKEOVER_PROTOCOL.md`。本文不再复制这些内容，避免同一规则出现多个版本。[1] [2]

---

## 2. 固定安全更新命令（后续原样使用）

先关闭Unity，在 **MarioTrickster仓库根目录** 打开PowerShell，完整粘贴以下命令。无需填写版本号或本机路径。脚本只恢复本次新建的stash，保留所有备份；任何失败立即停止，不会自动解决冲突。没有本地修改时不创建、更不应用旧stash。拒绝分支错误、未完成Git操作或未备份干净的工作区。

```powershell
$ErrorActionPreference = 'Stop'
function GitSafe {
    $output = & git @args
    if ($LASTEXITCODE -ne 0) { throw "Git failed: git $($args -join ' ')" }
    return (($output -join "`n").Trim())
}
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    throw 'Please close Unity before updating.'
}
$root = GitSafe rev-parse --show-toplevel
if ([IO.Path]::GetFullPath($root).TrimEnd('\','/') -ne (Get-Location).Path.TrimEnd('\','/')) {
    throw 'Open PowerShell in the repository root first.'
}
if (!(Test-Path -LiteralPath 'Assets') -or !(Test-Path -LiteralPath 'ProjectSettings')) {
    throw 'This is not the Unity project root.'
}
$origin = GitSafe remote get-url origin
if ($origin -notmatch '^(https://github\.com/|git@github\.com:)jiaxuGOGOGO/MarioTrickster(?:\.git)?/?$') {
    throw "Unexpected origin: $origin"
}
$branch = GitSafe symbolic-ref --quiet --short HEAD
if ($branch -ne 'genspark_ai_developer') { throw "Unexpected branch: $branch" }
GitSafe config --local core.longpaths true | Out-Null
foreach ($name in @('MERGE_HEAD','REBASE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','BISECT_LOG','rebase-apply','rebase-merge','sequencer','index.lock')) {
    $path = GitSafe rev-parse --git-path $name
    if (Test-Path -LiteralPath $path) { throw "Unfinished Git operation or lock: $name" }
}
$backup = ''
$dirty = GitSafe status --porcelain=v1 --untracked-files=all
if ($dirty) {
    $before = GitSafe stash list -1 --format=%H
    $tag = 'safe-update-' + [Guid]::NewGuid().ToString('N')
    Write-Host (GitSafe stash push --include-untracked -m $tag)
    $backup = GitSafe rev-parse --verify refs/stash
    $subject = GitSafe log -1 --format=%s $backup
    if (!$backup -or $backup -eq $before -or $subject -notlike "*$tag*") {
        throw 'A unique new backup was not verified. Update stopped.'
    }
    Write-Host "Retained backup: $backup ($tag)"
    if (GitSafe status --porcelain=v1 --untracked-files=all) {
        throw 'Some changes remain unbacked up (possibly a submodule). Update stopped.'
    }
}
Write-Host (GitSafe pull --ff-only --no-rebase origin genspark_ai_developer)
if ($backup) {
    Write-Host (GitSafe stash apply $backup)
    Write-Host "Applied this update backup only; stash retained: $backup"
}
Write-Host (GitSafe log -1 --oneline)
Write-Host (GitSafe status --short)
Write-Host 'Update complete. You may open Unity.'
```

如果pull失败，本次备份仍在，脚本不继续apply；如果apply冲突，备份同样保留，停止并把完整输出发给AI。**不使用pop、不删除stash、不reset、不clean，不盲目应用以前的备份。** 本脚本没有在用户Windows机器执行验证；Git路径/分支或冲突异常时保守停止。

只读检查可以单独用 `git status`、`git log --oneline -10`；不要改用下面错误排查中的破坏性快捷方式。

---

## 3. 常见 Git 报错

| 报错 | 通常原因 | 处理方式 |
| --- | --- | --- |
| `Your local changes would be overwritten` | 本地有未提交修改，远程也要更新同一批文件。 | 使用上方完整安全更新脚本；若仍失败，保留备份并反馈输出。 |
| `cannot pull with rebase: You have unstaged changes` | 工作区未清理。 | 使用上方安全脚本，不放弃本地修改。 |
| `Updates were rejected` | 远程比本地新。 | 停止并反馈分支分歧；本地更新不自动rebase或强推。 |
| `Failed to connect to github.com port 443` | 网络或代理问题。 | 检查代理软件，再按本文第 5 节配置 Git 代理。 |
| `Author identity unknown` | 没配置 Git 用户名和邮箱。 | 运行 `git config --global user.name "你的名字"` 与 `git config --global user.email "你的邮箱"`。 |
| `dubious ownership` | Git 不信任当前目录。 | 运行 `git config --global --add safe.directory "项目路径"`。 |

---

## 4. Unity 报错反馈模板

AI 不需要你填写系统黑话，但需要能看见问题本身。遇到 Unity 红字、功能异常或运行效果不对时，把下面三类信息合成一条消息即可。

| 信息 | 怎么写 |
| --- | --- |
| 你做了什么 | “我打开某个窗口 / 点了某个按钮 / 在某关按了某个键”。 |
| 预期是什么 | “我以为会生成关卡 / 播放动画 / 角色跳起来”。 |
| 实际发生什么 | 粘贴完整 Console 报错，或描述“没反应 / 只显示第一帧 / 角色穿墙”。 |

```text
我刚才做了：[具体操作]
预期结果：[本来应该发生什么]
实际结果：[现在发生什么]
Console 报错：[如果有，请完整粘贴红字和堆栈]
补充截图：[可选]
```

如果是素材问题，优先补一句素材命名，例如 `hero_idle_00 / hero_run_00 / hero_jump_00 / hero_fall_00` 是否齐全，这样 AI 能更快判断是分类器、切片还是挂载链路的问题。[4]

---

## 5. 代理与新电脑配置

| 操作 | 命令 |
| --- | --- |
| 配置 Git 用户名 | `git config --global user.name "你的GitHub用户名"` |
| 配置 Git 邮箱 | `git config --global user.email "你的邮箱"` |
| 设置 HTTP 代理 | `git config --global http.proxy http://127.0.0.1:端口号` |
| 设置 HTTPS 代理 | `git config --global https.proxy http://127.0.0.1:端口号` |
| 查看代理 | `git config --global --get http.proxy` |
| 取消 HTTP 代理 | `git config --global --unset http.proxy` |
| 取消 HTTPS 代理 | `git config --global --unset https.proxy` |
| 设置安全目录 | `git config --global --add safe.directory "项目路径"` |

首次克隆项目建议包含子模块：

```bash
git clone --recurse-submodules https://github.com/jiaxuGOGOGO/MarioTrickster.git
```

如果已经普通克隆，再运行：

```bash
git submodule update --init --recursive
```

---

## References

[1]: ./SESSION_TRACKER.md "MarioTrickster Session Tracker"
[2]: ./docs/AI_TAKEOVER_PROTOCOL.md "AI Takeover Protocol"
[3]: ./docs/PLANNER_FAST_LEVEL_PRODUCTION_GUIDE.md "Planner Fast Level Production Guide"
[4]: ./docs/ASSET_IMPORT_PIPELINE_GUIDE.md "Asset Import Pipeline Guide"
