# 分册：打包交付

```bash
bash scripts/verify.sh                      # 必须 VERIFY ALL GREEN
cd /home/user/workspace/repo && git add -A && git commit -q -m "feat(step1): <英文一句话> (SXXX)"
bash scripts/make_patch.sh SXXX "In Unity: Test Runner - EditMode - Run All, then Workshop - <样板> - Play."
```
- `make_patch.sh` 会：fetch + rebase 到 `origin/genspark_ai_developer` → 把**所有未上传的提交**做成补丁（用户漏传的旧补丁会一起带上）→ 生成可重复双击的 `apply_SXXX.bat`（已打过的自动跳过）→ zip → 在全新克隆上 `git am` 并比对，必须输出 **TREE IDENTICAL**。
- 元素/样板有变时先 `python3 tools/LevelStudioWeb/build.py`，把 `tools/LevelStudioWeb/index.html` 也一起交付（命名"MarioTrickster关卡设计台.html"）。
- 交付：`genspark_deliver_files` 一次交 `out/SXXXpatch.zip` + `docs/step1/SXXX_*.md`。
- commit 信息**英文**（bat 用它判断是否已打过）；中文写在文档里。
- 不要 `git push`（用户在 bat 里选 Y 自己推）。
- 汇报最后给用户的步骤固定三步：① 解压双击 `apply_SXXX.bat` 选 Y ② Unity → Test Runner → EditMode → Run All ③ Ctrl+Alt+W 工坊 → 样板 → 试玩（写清具体按什么键体验新东西）。

## 交付后刷新接续包（换账号不丢进度）
```bash
SK=<技能目录>; rm -rf "$SK/scripts/pending" && mkdir -p "$SK/scripts/pending" && cp /home/user/workspace/out/SXXXpatch/*.patch "$SK/scripts/pending/"
cp -r "$SK/." /home/user/workspace/repo/docs/AI_CONTINUE_PACK/ && rm -rf /home/user/workspace/repo/docs/AI_CONTINUE_PACK/scripts/pending   # 仓库里那份不放 pending（避免补丁套补丁）
cd "$(dirname "$SK")" && zip -qr /home/user/workspace/out/mariotrickster-continue.skill "$(basename "$SK")"
```
规则、分册有改动时同样这样刷新，并把 `.skill` 一起交付。

## S214 起的交付包（给用户的 zip）
```bash
cd /home/user/workspace/out && rm -rf D<N> && cp -r D<N-1> D<N> && rm D<N>/01_安装到项目/*
cp SXXXpatch/*.patch D<N>/01_安装到项目/ && cp SXXXpatch/apply_SXXX.bat D<N>/01_安装到项目/install_and_upload.bat
cp ../repo/docs/step1/SXXX_*.* D<N>/03_说明文档/ && cp ../repo/tools/LevelStudioWeb/index.html "D<N>/04_关卡设计台网页/MarioTrickster关卡设计台.html"
cp mariotrickster-continue.skill D<N>/02_接续包_换账号用/   # 先按上面刷新 .skill
# 重写 D<N>/00_先看我_怎么用.md（大白话：这次改了什么表格 + 你的三步 + 诚实说明）→ zip → deliver
```
- 沙盒里没有 D<N-1> 时：按 SKILL.md 0.6 的五个文件夹结构从头建。
- 用户要求"同步到技能 / 存技能"：改完技能目录后跑 `gsk skills save ~/.opencode/skills/mariotrickster-continue`（返回 `"saved": true` 才算存好），并交付 `.skill` 文件给用户换账号用。
