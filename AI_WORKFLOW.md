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

## 2. Git 日常速查

| 你想做什么 | 命令 |
| --- | --- |
| 查看当前有没有本地修改 | `git status` |
| 拉取 AI 已推送的最新代码 | `git pull` |
| 查看最近 10 次提交 | `git log --oneline -10` |
| 保存本地改动并推送 | `git add -A && git commit -m "your message" && git push` |
| 本地有修改但想先拉远程 | `git stash && git pull && git stash pop` |
| 放弃所有本地未提交修改 | `git reset --hard HEAD` |
| 强制与远程 master 对齐 | `git fetch origin && git reset --hard origin/master` |
| 更新子模块 | `git submodule update --init --recursive` |

> **安全提醒**：如果本地有你想保留的改动，不要直接运行 `reset --hard`。先 `git status` 看清楚，再决定是否提交或 stash。

---

## 3. 常见 Git 报错

| 报错 | 通常原因 | 处理方式 |
| --- | --- | --- |
| `Your local changes would be overwritten` | 本地有未提交修改，远程也要更新同一批文件。 | 先 `git add -A && git commit -m "local work"`，或 `git stash` 后再 `git pull`。 |
| `cannot pull with rebase: You have unstaged changes` | 工作区未清理。 | 先提交、stash 或放弃本地修改。 |
| `Updates were rejected` | 远程比本地新。 | 先 `git pull --rebase`，解决冲突后再 `git push`。 |
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
