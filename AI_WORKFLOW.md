# AI 协作工作流指南 (AI_WORKFLOW)

**项目名称**：MarioTrickster（非对称对抗平台跳跃游戏）
**技术栈**：Unity 2022.3 LTS (2D Core)、C#、Rider、GitHub、ComfyUI / Stable Diffusion
**仓库地址**：`https://github.com/jiaxuGOGOGO/MarioTrickster`
**本地路径**：`E:\unity project\exercise1\MarioTrickster`

> 更新时间：2026-04-02 (Session 12)
> 文件名：`AI_WORKFLOW.md`（原 `与 AI 高效协作开发工作流指南.md`）

本指南旨在帮助你以最低的沟通成本、最少的积分消耗，与 AI 进行最高效的协作开发。遇到特定情况时，直接按目录跳转到对应章节，复制模板使用即可。

---

## 你在整个循环中只需要做 3 件事

> 其他所有事情（代码修复、文档同步、回归分析、推送）都由 AI 自动处理。

```text
① 发模板开对话  →  ② pull + 测试  →  ③ 填反馈发给 AI  →  循环
```

### 每个环节的防错要点

| 环节 | 你要做什么 | 绝对不要做什么 | 为什么 |
|------|----------|------------|------|
| **① 发模板** | 复制 `SESSION_TRACKER.md` §8 的模板，只改“本次任务”那一行 | 不要自己写开场白，不要省略积分提醒 | 模板已包含 AI 需要的所有指令 |
| **② 拉取** | AI 说“已推送”后，**先 `git pull`，再打开 Unity** | 不要跳过 pull 直接测试 | 你可能测的是旧代码 |
| **② 测试** | 按 `Testing_Guide.md` 测试项操作，**同时检查 SESSION_TRACKER §3 回归清单** | 不要跳过回归清单中的项目 | 跳过会导致回归验证失效 |
| **③ 填反馈** | 用 `SESSION_TRACKER.md` §2 的反馈模板，**每项都填** | 不要只说“通过了”，要写具体测了什么 | AI 需要具体信息来更新测试状态 |

### 3 个绝对红线（违反会导致循环断裂）

| 红线 | 后果 | 如果已经违反了怎么办 |
|------|------|------------------|
| **不要在 Unity/Rider 中手动改代码** | AI 不知道你改了什么，文档和代码永久不同步 | 下次对话告诉 AI：“我手动改了 XXX.cs 的 XXX 方法，请同步文档” |
| **不要手动编辑任何 .md 文档** | 破坏真相源和联动矩阵的一致性 | 下次对话告诉 AI：“我手动改了 XXX.md，请检查联动同步” |
| **不要在积分低于 500 时还派新任务** | AI 可能来不及存档就断开 | 看到积分低于 500，发“请存档”而不是新任务 |

### 如果你确实需要手动改代码怎么办？

有时你可能想在 Unity 中微调参数或快速修一个小问题。这完全可以，但必须在下次对话中告诉 AI：

```text
本次任务：[你的任务]

额外说明：我手动修改了以下文件，请先同步文档：
- XXX.cs：修改了 XXX 方法的 XXX 参数
- 原因：XXX
```

AI 会自动读取你改的文件，更新所有相关文档，确保同步。

---

## 快捷查询目录

| 你遇到的情况 | 跳转章节 |
| :--- | :--- |
| 不知道我该做什么/不该做什么 | [你只需要做 3 件事](#你在整个循环中只需要做-3-件事) |
| 换了新对话框，需要快速衔接 | [1.1 新对话开启模板](#11-新对话开启模板) |
| 当前对话快结束了，需要存档 | [1.2 上下文保存机制](#12-上下文保存机制对话结束前) |
| 需要 AI 直接推送代码到 GitHub | [1.3 GitHub Token 配置与使用](#13-github-token-配置与使用) |
| 积分快用完了，需要紧急存档 | [1.4 积分管理与紧急存档策略](#14-积分管理与紧急存档策略) |
| Unity 飘红报错了 | [2.1 报错提问模板](#21-报错提问模板) |
| 游戏效果不对但没报错 | [2.2 逻辑错误](#22-逻辑错误没报错但效果不对) |
| 修复 Bug 后担心影响其他功能 | [2.4 回归影响与定向验证](#24-回归影响与定向验证) |
| 想做一个全新的功能/系统 | [3.1 需求确认与计划](#31-步骤一需求确认与计划) |
| 需要 AI 帮我写代码并推送 | [3.5 让 AI 直接写代码并推送](#35-步骤五让-ai-直接写代码并推送) |
| 需要画新的怪物/场景素材 | [4.1 索要 ComfyUI/SD 提示词](#41-索要-comfyuisd-提示词) |
| 需要批量生成动画帧 | [4.2 批量动画帧生成](#42-批量动画帧生成) |
| 不知道 Git 命令怎么用 | [5. Git 命令速查](#5-git-命令速查) |
| 拉取远程代码时冲突了 | [5.2 同步操作三种场景](#52-同步操作三种场景) |
| 换了新电脑，需要重新配置 | [5.8 换电脑后的衔接流程](#58-换电脑后的衔接流程) |
| 梯子/代理配置问题 | [5.9 代理配置速查](#59-代理配置速查) |
| 不知道文件该放哪里 | [6. Unity 项目结构规范](#6-unity-项目结构规范) |
| 想让 AI 帮忙做关卡设计 | [7.1 关卡设计沟通](#71-关卡设计沟通) |
| 想让 AI 帮忙调平衡性 | [7.2 平衡性调整沟通](#72-平衡性调整沟通) |
| 联机/网络相关问题 | [7.3 网络联机问题沟通](#73-网络联机问题沟通) |
| 性能出问题了 | [7.4 性能问题沟通](#74-性能问题沟通) |

---

## ⚡ Git 拉取速查（每次 AI 推送后必看）

> **规律：有本地改动就先 commit，再 pull。**

### 场景 1：只拉取 AI 的新代码，本地没有任何改动

```cmd
git pull
```

### 场景 2：本地有新场景 / 有改动，同时要拉取 AI 的新代码

```cmd
git add -A
git commit -m "本地进度"
git pull --rebase
git push
```

### 场景 3：放弃本地所有修改，强制使用 AI 的最新代码（最常用）

如果你在本地测试时改乱了，或者想完全同步 AI 的最新进度：

```cmd
git fetch --all
git reset --hard origin/main
git pull
```

> **常见报错速查**
>
> | 报错信息 | 原因 | 解决方法 |
> |----------|------|----------|
> | `Your local changes would be overwritten` | 本地有未提交修改 | 先 `git add -A` → `git commit` |
> | `cannot pull with rebase: You have unstaged changes` | 同上 | 同上 |
> | `Updates were rejected (fetch first)` | 没拉取就直接 push | 先 `git pull --rebase` 再 push |
> | `Failed to connect to github.com port 443` | 网络/梯子问题 | 开梯子，或配置代理（见 5.9 节） |
> | `Author identity unknown` | 未配置 Git 用户信息 | `git config --global user.email "你的邮箱"` 和 `git config --global user.name "你的名字"` |

---

## 1. 快速启动与上下文衔接

每次开启新对话时，AI 会丢失之前所有记忆。以下机制配合使用：**1.2 负责"存档"，1.1 负责"读档"，1.3 让 AI 能直接推送代码，1.4 确保积分耗尽前不丢失进度**。

完整流程是：
> 对话快结束 → 用 1.2 让 AI 存档（自动推送到 GitHub） → 下次开新对话 → 用 1.1 模板（3行）让 AI 自己去仓库读档

**核心设计**：AI 每次新对话先读 `SESSION_TRACKER.md`（当前状态 + AI 行为规范 + 回归清单 + 待办队列），需要完整上下文时再读 `MarioTrickster_Progress_Summary.md`（功能清单、Bug 库、技术决策、Session 历史）。需要纵览全局进度时读 `MASTER_TRACKER.md`（设计愿景与实现状态的映射矩阵、模块完成度）。你开新对话时**不需要手动复制粘贴任何长内容**，只需要给 AI 三样东西：Token、仓库地址、本次任务。

### 1.1 新对话开启模板

> ⚠️ **注意**：最新版的开场模板已移至 `SESSION_TRACKER.md` 底部，请直接去那里复制。

### 1.2 上下文保存机制（对话结束前）

当对话即将结束或你感觉聊了很多内容时，发送以下请求。AI 会自动更新 `MarioTrickster_Progress_Summary.md` 并推送到 GitHub，**你不需要手动保存任何东西**：

```
请存档：更新 MarioTrickster_Progress_Summary.md 并推送到 GitHub。
```

AI 收到后会自动执行：
1. 更新 `SESSION_TRACKER.md`（当前状态、测试进度、回归清单、待办队列）
2. 更新 `MarioTrickster_Progress_Summary.md`（已完成功能、Bug清单、Session 记录、技术决策）
3. 更新 `MASTER_TRACKER.md`（全局设计与实现映射状态）
4. 提交并推送到 GitHub
5. 确认推送成功

如果你想更精确地控制存档内容，也可以用完整版：

```
请存档并推送，本次新增/变更：
- 新完成：[列出本次完成的功能]
- 新发现Bug：[列出新Bug]
- 决策变更：[如有]
```

### 1.3 GitHub Token 配置与使用

**为什么需要 Token？** AI 每次对话都在全新的沙盒环境中运行，没有你的 GitHub 登录凭据。提供 Token 后，AI 可以直接克隆仓库、写入代码、推送提交，省去你手动复制粘贴代码的麻烦。

**生成步骤：**

1. 打开 https://github.com/settings/tokens
2. 点击 **Generate new token (classic)**（选 classic，不要选 fine-grained）
3. **Note** 填写：`manus-push`
4. **Expiration** 选择：`90 days`（到期后重新生成即可）
5. **Select scopes** 只勾选 **repo**（勾父级，子项自动全选）
6. 点击 **Generate token**
7. 复制生成的 `ghp_xxxx` 字符串，**妥善保存**（只显示一次）

**使用方式：** 每次新对话时，在开场模板中附上 Token，AI 会用以下方式推送：

```bash
git clone https://ghp_你的token@github.com/jiaxuGOGOGO/MarioTrickster.git
# ... 写入代码 ...
git add -A && git commit -m "描述" && git push
```

**安全提醒：**

| 注意事项 | 说明 |
| :--- | :--- |
| Token 等同于密码 | 不要发到公开场所（公开聊天室、论坛等） |
| 只给 repo 权限 | 不要勾选其他权限，最小化风险 |
| 到期后重新生成 | 90天后需要重新走一遍生成流程 |
| 可随时撤销 | 在 https://github.com/settings/tokens 删除即可立即失效 |

### 1.4 积分管理与紧急存档策略

Manus 对话有积分限制。为了避免积分耗尽导致工作丢失，建议遵循以下策略：

**预防措施（每次对话开头就做）：**

在开场模板中加入积分提醒（1.1 模板已包含），告诉 AI 在积分接近 300 时暂停工作。AI 无法实时查看你的积分余额，所以当你注意到积分快用完时，立即发送：

```
积分快用完了，请立刻：
1. 把已写好的所有代码推送到 GitHub
2. 生成进度总结文档并推送
3. 停止其他工作
```

**紧急存档流程：**

当积分紧张时，AI 会按以下优先级行动：

| 优先级 | 动作 | 说明 |
| :--- | :--- | :--- |
| 1 | 更新 `SESSION_TRACKER.md` | 确保下次对话能无缝衔接（当前状态+待办+回归清单） |
| 2 | 更新 `Progress_Summary.md` | 保存完整存档（Bug库、Session记录、技术决策） |
| 2.5 | 更新 `MASTER_TRACKER.md` | 同步全局设计与实现映射状态 |
| 3 | 推送已完成的代码到 GitHub | 确保代码不丢失 |
| 4 | 列出未完成的任务清单 | 方便下次继续 |

**如果来不及推送 GitHub：** AI 会把所有文件作为附件直接发给你，你手动放进 Unity 项目即可。

---

## 2. 遇到 Bug 时的最高效提问法

AI 无法看到你的屏幕。提供的信息越全面，解决越快，消耗的对话轮次（积分）越少。

### 2.1 报错提问模板

Unity Console 出现红字报错时，确保包含以下三个要素：

| 必需信息 | 怎么获取 | 示例 |
| :--- | :--- | :--- |
| **完整错误信息** | 点击 Console 中的报错，复制底部的完整堆栈追踪 | `NullReferenceException: Object reference not set... at PlayerController.Update() in line 45` |
| **触发条件** | 描述你做了什么操作导致报错 | "按下空格键让角色跳跃时，在半空中突然报错" |
| **相关代码** | 报错指向的脚本名和代码段（优先粘贴文本，截图辅助） | "报错指向 `PlayerController.cs` 的第 45 行" |

提问示例：

```
我在测试跳跃时遇到了报错。当我按下空格键，角色起跳后立刻报错。
报错信息是：
NullReferenceException: Object reference not set to an instance of an object
  at PlayerController.Update () in PlayerController.cs:45

这是我目前的 PlayerController.cs 代码：
[粘贴完整代码]
```

### 2.2 逻辑错误（没报错，但效果不对）

没有红字报错，但游戏表现不符合预期时，描述**预期**与**实际**的差异：

```
目前没有报错，但逻辑不对。
预期表现：角色碰到砖块应该停下来。
实际表现：角色直接穿过了砖块。
我已经给角色加了 Rigidbody2D 和 BoxCollider2D，砖块也有 BoxCollider2D。
相关脚本：[粘贴代码或说明在 GitHub 哪个文件]
```

### 2.3 Unity 编辑器问题

如果问题不是代码报错，而是 Unity 编辑器本身的操作问题（如组件配置、Inspector 设置等），请截图 + 文字描述：

```
我在 Unity Inspector 里遇到了问题。
我想做的事：[描述目标]
我做了什么操作：[描述步骤]
现在的状态：[截图 + 文字描述]
```

### 2.4 回归影响与定向验证

修复 Bug 时修改的代码可能影响其他已通过或未测试的功能。项目已建立了一套完整的防护机制：

**你不需要做任何额外工作**，AI 会自动处理：

1. AI 修复 Bug 后，按 `SESSION_TRACKER.md` §0.2 流程查询影响矩阵（详见 `Testing_Guide.md` 第四章）
2. AI 在 `SESSION_TRACKER.md` §3 “回归验证清单”中标记需要重测的项目
3. AI 推送前按 `SESSION_TRACKER.md` §0.3 联动矩阵检查所有文档是否同步
4. 你测试时，除了当前测试项，还需快速验证清单中的回归项（每项约 30 秒）
5. 在反馈模板中一起填写结果

> 详细的联动更新规则和信息真相源分配表见 `SESSION_TRACKER.md` §0.3。

---

## 3. 开发新功能的标准工作流

开发新功能时，不要让 AI 直接"写一个完整的系统"，而是采用**拆解与迭代**的方式，每次只做一小步。

### 3.1 步骤一：需求确认与计划

先让 AI 写一个实现计划，确认逻辑无误后再写代码：

```
我需要实现捣蛋者的"变身系统"。玩家按下 E 键可以变成最近的障碍物。
请先给我写一个实现计划，包括：
1. 需要新建哪些脚本
2. 需要哪些 Unity 组件（如 Collider, SpriteRenderer）
3. 脚本之间的调用关系
确认计划后我们再开始写代码。
```

### 3.2 步骤二：获取代码与粘贴

AI 会给你完整的 C# 脚本内容。你需要在 Rider 或 Unity 中：

1. 在 `Assets/Scripts/` 对应子文件夹下新建 C# 文件（**文件名必须与类名完全一致**）
2. 将 AI 给的代码**完整粘贴**进去，覆盖默认代码
3. 回到 Unity，将脚本拖拽挂载到对应的 GameObject 上

### 3.3 步骤三：配置 Inspector 变量

AI 会在代码中使用 `[SerializeField]` 暴露变量。粘贴代码后，务必在 Unity Inspector 面板中检查是否有需要拖拽赋值的空槽位（如：需要指定玩家的 Transform，或设置跳跃力度数值）。

### 3.4 步骤四：测试与反馈

测试后把结果反馈给 AI：

```
功能测试结果：
- 变身按键响应：正常
- 变身后外观切换：正常
- 变身后碰撞体切换：有问题，变成管道后碰撞体还是原来的大小
- 变身冷却计时：正常
请帮我修复碰撞体的问题。
```

### 3.5 步骤五：让 AI 直接写代码并推送

如果你在开场模板中提供了 GitHub Token（见 1.3），可以直接让 AI 写代码并推送到仓库，省去手动复制粘贴的步骤：

```
请直接帮我实现 InputManager.cs（本地双人输入管理），写好后推送到 GitHub。
放在 Assets/Scripts/Core/ 目录下。
```

AI 会执行以下流程：克隆仓库 → 创建/修改文件 → 提交 → 推送。你在本地只需要 `git pull` 就能拿到最新代码：

```cmd
cd /d "E:\unity project\exercise1\MarioTrickster"
git pull
```

> **注意**：如果你本地有未提交的修改，先 `git stash` 暂存，pull 之后再 `git stash pop` 恢复。

---

## 4. 美术与素材生成的沟通

### 4.1 索要 ComfyUI/SD 提示词

需要新的游戏素材时，让 AI 生成精确的英文提示词：

```
我需要在 ComfyUI 中生成一个【超级马里奥风格的食人花怪物】。
要求：
- 风格：像素风格 (Pixel art)
- 分辨率：32x32
- 视角：侧面
- 背景：纯色透明
- 用途：可变身对象
请帮我写正向提示词和反向提示词。
```

### 4.2 批量动画帧生成

需要角色动画的 Sprite Sheet 时：

```
我需要为闯关者角色生成一组行走动画的 Sprite Sheet。
要求：
- 帧数：6帧
- 风格：与现有角色一致（像素风格，32x32）
- 动作：向右行走循环
请帮我写 ComfyUI 工作流配置或 SD 提示词。
```

### 4.3 特效素材

```
我需要一个变身特效的动画序列。
要求：
- 效果：烟雾散开 + 闪光
- 帧数：8帧
- 分辨率：64x64
- 背景：透明
请帮我写提示词。
```

### 4.4 推荐的免费素材来源

以下是经过调研确认的高质量免费素材，可直接用于 MVP 阶段：

| 素材包 | 许可证 | 内容 | 链接 |
| :--- | :--- | :--- | :--- |
| Pixel Frog - Pixel Adventure 1 & 2 | CC0 | 角色/物体/地块/道具/20种敌人 | https://pixelfrog-assets.itch.io/pixel-adventure-1 |
| Block Land 16x16 | CC0 | Mario/Minecraft风格地块集 | itch.io 搜索 "Block Land" |
| JuhoSprite Mario World风格 | -- | 角色/敌人/3个世界 | https://juhosprite.itch.io/ |
| Brackeys' Platformer Bundle | -- | 角色/地块/音效/音乐 | https://brackeysgames.itch.io/brackeys-platformer-bundle |
| Monsters Creatures Fantasy | -- | 怪物/敌人角色素材 | https://luizmelo.itch.io/monsters-creatures-fantasy |

---

## 5. Git 命令速查

以下所有命令都在 **cmd** 或 **PowerShell** 中运行。

### 5.1 日常操作（最常用）

每次在 Unity 里改了东西，想同步到 GitHub：

```cmd
cd /d "E:\unity project\exercise1\MarioTrickster"
git add .
git commit -m "简要描述改了什么"
git push
```

### 5.2 同步操作三种场景

以下所有命令都先进入项目目录：

```cmd
cd /d "E:\unity project\exercise1\MarioTrickster"
```

#### 场景 A：本地没改过东西，直接拉取远程更新

最简单的情况，直接拉：

```cmd
git pull
```

#### 场景 B：本地有修改，想保留本地改动同时拉取远程

```cmd
git stash              # 把本地修改临时藏起来
git pull               # 拉取远程代码
git stash pop          # 把之前藏的本地修改恢复回来
```

如果 `git stash pop` 提示 **CONFLICT**（本地和远程改了同一个文件的同一处），根据情况选择：

```cmd
# 想用远程版本（AI推送的）覆盖冲突文件：
git checkout --theirs "冲突文件名"
git add .
git commit -m "合并远程更新，保留远程版本"

# 想用本地版本保留自己的修改：
git checkout --ours "冲突文件名"
git add .
git commit -m "合并远程更新，保留本地版本"
```

#### 场景 C：不要本地修改，直接用远程覆盖本地

```cmd
git checkout -- .       # 丢弃所有未提交的本地修改
git pull                # 拉取远程最新内容
```

> **快速判断用哪个：** 不确定本地有没有改过？先跑 `git status`。显示 `nothing to commit, working tree clean` 就用场景 A，否则根据是否想保留本地改动选 B 或 C。

### 5.3 查看状态

不确定哪些文件改了：

```cmd
git status
```

### 5.4 查看历史记录

看之前的提交记录：

```cmd
git log --oneline -10
```

### 5.5 撤销操作

| 场景 | 命令 | 说明 |
| :--- | :--- | :--- |
| 改了文件还没 add，想撤销 | `git checkout -- 文件名` | 恢复到上次 commit 的状态 |
| 已经 add 但还没 commit | `git reset HEAD 文件名` | 取消 add，文件改动保留 |
| 已经 commit 但还没 push | `git reset --soft HEAD~1` | 撤销最近一次 commit，改动保留 |
| 已经 push 了，想回退 | `git revert HEAD` | 创建一个新 commit 来撤销上一次的改动 |

### 5.6 分支操作（后期用）

当你想尝试新功能又怕改坏主分支时：

```cmd
git checkout -b 新分支名          # 创建并切换到新分支
git checkout master               # 切回主分支
git merge 新分支名                # 把新分支的改动合并到主分支
git branch -d 新分支名            # 删除已合并的分支
```

### 5.7 常见问题

| 问题 | 解决方法 |
| :--- | :--- |
| push 失败，提示 Connection reset | 检查梯子是否开启，或设置 Git 代理：`git config --global http.proxy http://127.0.0.1:你的代理端口` |
| push 失败，提示 rejected | 先 `git pull --rebase` 再 `git push` |
| 提示 dubious ownership | `git config --global --add safe.directory "E:/unity project/exercise1/MarioTrickster"` |
| 不小心把 Library 文件夹传上去了 | `git rm -r --cached Library/` 然后 commit 并 push |

### 5.8 换电脑后的衔接流程

新电脑装好 Git 后，按以下顺序一次性配置完成：

**第一步：配置 Git 身份**

```cmd
git config --global user.name "你的GitHub用户名"
git config --global user.email "你的邮箱"
```

**第二步：配置代理（如果用梯子访问 GitHub）**

```cmd
# 设置 HTTP 代理（端口号换成你梯子的实际端口，常见的有 7890、1080、10808 等）
git config --global http.proxy http://127.0.0.1:7890
git config --global https.proxy http://127.0.0.1:7890
```

查看梯子端口号的方法：打开你的代理软件（Clash / V2rayN / Shadowsocks 等），在设置里找“本地 HTTP 代理端口”。

如果不用梯子或不确定，跳过这步，等遇到 `Connection reset` 报错时再配。

**第三步：克隆项目**

```cmd
cd /d "E:\unity project\exercise1"
git clone https://github.com/jiaxuGOGOGO/MarioTrickster.git
```

克隆时会要求登录 GitHub，输入用户名和密码（或 Token）即可。

**第四步：配置安全目录（避免报错）**

```cmd
git config --global --add safe.directory "E:/unity project/exercise1/MarioTrickster"
```

**第五步：用 Unity Hub 打开项目**

Unity Hub → Open → 选择 `E:\unity project\exercise1\MarioTrickster` 文件夹 → 等待导入完成。

> **一句话总结：** 新电脑只需要 Git + Unity，然后 `git clone` 拉代码，所有脚本和文档都在仓库里，零损耗衔接。

### 5.9 代理配置速查

| 操作 | 命令 |
| :--- | :--- |
| 设置代理 | `git config --global http.proxy http://127.0.0.1:端口号` |
| 设置 HTTPS 代理 | `git config --global https.proxy http://127.0.0.1:端口号` |
| 查看当前代理配置 | `git config --global --get http.proxy` |
| 取消代理 | `git config --global --unset http.proxy` 和 `git config --global --unset https.proxy` |
| 查看所有 Git 配置 | `git config --global --list` |

**常见代理软件默认端口：**

| 代理软件 | 默认 HTTP 端口 |
| :--- | :--- |
| Clash for Windows | 7890 |
| V2rayN | 10808 |
| Shadowsocks | 1080 |
| Clash Verge | 7890 |

> **注意：** 以上是默认值，实际端口以你代理软件设置页面显示的为准。换电脑后如果梯子软件也重装了，记得重新配置 Git 代理。

---

## 6. Unity 项目结构规范

> ⚠️ **注意**：完整的项目文件结构树已移至 `MarioTrickster_Progress_Summary.md` 的"八、项目文件结构"章节。

**命名规范**：

| 类型 | 规范 | 示例 |
| :--- | :--- | :--- |
| C# 脚本 | PascalCase，文件名 = 类名 | `PlayerController.cs` |
| 场景文件 | PascalCase + 下划线编号 | `Level_01.unity` |
| Sprite 图片 | 小写 + 下划线 | `player_idle_01.png` |
| Prefab | PascalCase | `GreenPipe.prefab` |
| 动画 | PascalCase + 状态 | `Player_Walk.anim` |

---

## 7. 特定场景沟通模板

### 7.1 关卡设计沟通

```
我需要设计第 [X] 关的布局。
关卡主题：[如：地下管道区]
预期难度：[简单/中等/困难]
关卡长度：[大约多少屏]
特殊机制：[如：有移动平台、有水区域]
可变身对象种类：[如：管道、砖块、蘑菇]
请帮我设计关卡布局草图和障碍物分布方案。
```

### 7.2 平衡性调整沟通

```
我在测试中发现平衡性问题：
当前情况：[如：捣蛋者胜率过高，闯关者几乎无法通关]
测试场景：[如：第一关，测试了10局，闯关者只赢了2局]
我觉得可能的原因：[如：变身冷却太短，闯关者扫描冷却太长]
请帮我分析并给出调整建议。
```

### 7.3 网络联机问题沟通

```
我在实现联机功能时遇到问题。
使用的网络方案：[如：Unity Netcode for GameObjects]
问题描述：[如：客户端看到的角色位置和主机端不同步]
相关代码：[粘贴网络同步相关的代码]
网络环境：[如：本地两台电脑局域网测试]
```

### 7.4 性能问题沟通

```
游戏运行时出现卡顿。
卡顿时机：[如：场景中有超过20个可变身对象时]
帧率情况：[如：从60fps掉到20fps]
Profiler 截图：[如果会用 Unity Profiler，截图发过来]
怀疑原因：[如：可能是碰撞检测太多]
```

---

## 8. 沟通原则与避坑指南

### 8.1 核心原则

| 原则 | 说明 | 为什么重要 |
| :--- | :--- | :--- |
| **小步快跑** | 每次只让 AI 写一个脚本或实现一个具体的小功能 | AI 处理过长代码容易出错，小块代码更容易测试和排查 |
| **同步 Git** | 每完成一个功能，立刻 `git add . && git commit && git push` | 改坏了可以随时回退，不丢失已完成的工作 |
| **提供完整上下文** | 自己改了代码一定要推送到 GitHub 或把改动后的完整代码发给 AI | AI 不知道你私下改了什么，基于旧代码的建议会导致更多 Bug |
| **文本优先** | 报错信息和代码尽量复制文本，截图只作辅助 | 文本可以直接被 AI 读取分析，准确率远高于图片识别 |
| **先计划后编码** | 新功能先让 AI 出计划，确认后再写代码 | 避免写了一堆代码发现方向不对，浪费时间和积分 |
| **Token 常备** | 每次新对话都附上 GitHub Token | 让 AI 能直接推送代码，省去手动复制粘贴 |

### 8.2 省积分技巧

| 技巧 | 说明 |
| :--- | :--- |
| 一次性提供完整信息 | 不要分多条消息发，把报错+代码+描述合成一条发 |
| 善用进度总结 | 对话结束前生成总结，下次开场直接粘贴，省去重复解释 |
| 批量提问 | 如果有多个小问题，合成一条消息一起问 |
| 明确说"只要代码" | 如果不需要解释，说"直接给我代码，不需要解释"，减少输出长度 |
| 指定文件名 | 说"修改 PlayerController.cs 的第 45 行"比"帮我改一下跳跃代码"更精确 |
| 提前设积分警戒线 | 开场就说"积分到300时暂停"，避免工作丢失 |
| 让 AI 直接推送 | 提供 Token 后说"写好直接推送"，省去你手动操作的来回沟通 |

### 8.3 常见错误做法

| 错误做法 | 正确做法 |
| :--- | :--- |
| "报错了" | 粘贴完整报错信息 + 触发条件 + 相关代码 |
| "帮我写个完整的游戏" | 拆解成具体的小功能，逐个实现 |
| 只发截图不发文本 | 文本为主，截图为辅 |
| 自己改了代码不告诉 AI | 改了就 push，或把改动后的代码发给 AI |
| 一次对话聊太多不同的事 | 一次对话聚焦一个主题，做完存档再开新对话 |
| 积分用完才想起存档 | 开场就设警戒线，提前存档 |
| 每次新对话不带 Token | 开场模板里固定包含 Token |

---

## 9. 开发里程碑检查清单

> ⚠️ **注意**：完整的开发计划和里程碑追踪已移至 `MarioTrickster_Progress_Summary.md` 的"五、下一步开发计划"章节。

---

> **核心理念**：把 AI 看作一位**有经验但看不见你屏幕的远程结对编程伙伴**。你提供的"情报"越精确，AI 输出的代码质量就越高，消耗的积分就越少。每次新对话都是一次"交接班"，用好模板和 Token，让交接零损耗。
