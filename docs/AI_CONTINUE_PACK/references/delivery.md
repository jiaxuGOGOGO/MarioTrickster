# 分册：打包交付

```bash
bash scripts/verify.sh                      # 必须 VERIFY ALL GREEN
cd /home/user/workspace/repo && git add -A && git commit -q -m "feat(step1): <英文一句话> (SXXX)"
bash scripts/make_patch.sh SXXX "In Unity: Test Runner - EditMode - Run All, then Workshop - <样板> - Play."
```
- `make_patch.sh` 会：fetch + rebase 到 `origin/genspark_ai_developer` → 把**所有未上传的提交**做成补丁（用户漏传的旧补丁会一起带上）→ 生成可重复双击的 `apply_SXXX.bat`（已打过的自动跳过）→ zip → 在全新克隆上 `git am` 并比对，必须输出 **TREE IDENTICAL**。
- 交付：`genspark_deliver_files` 一次交 `out/SXXXpatch.zip` + `docs/step1/SXXX_*.md`。
- commit 信息**英文**（bat 用它判断是否已打过）；中文写在文档里。
- 不要 `git push`（用户在 bat 里选 Y 自己推）。
- 汇报最后给用户的步骤固定三步：① 解压双击 `apply_SXXX.bat` 选 Y ② Unity → Test Runner → EditMode → Run All ③ Ctrl+Alt+W 工坊 → 样板 → 试玩（写清具体按什么键体验新东西）。
