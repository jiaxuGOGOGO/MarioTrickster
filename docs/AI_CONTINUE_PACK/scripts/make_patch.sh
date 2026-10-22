#!/usr/bin/env bash
# 打交付包：把"本地有、GitHub 上还没有"的所有提交做成 SXXXpatch.zip（补丁 + 双击安装的 .bat），并在全新克隆上验证。
# 用法：bash make_patch.sh S201 "最后一句提示（英文，bat 里显示）"
set -e
unset version
SID=$1; HINT=${2:-"In Unity: Test Runner - EditMode - Run All, then play."}
WS=${WS:-/home/user/workspace}
[ -z "$SID" ] && { echo "usage: make_patch.sh S201 [hint]"; exit 1; }
cd "$WS/repo"
git fetch -q origin
git rebase -q origin/genspark_ai_developer
OUT="$WS/out/${SID}patch"; rm -rf "$OUT" "$WS/out/${SID}patch.zip"; mkdir -p "$OUT"
git format-patch origin/genspark_ai_developer..HEAD -o "$OUT" >/dev/null
N=$(ls "$OUT"/*.patch 2>/dev/null | wc -l); [ "$N" = 0 ] && { echo "没有新提交"; exit 1; }
BAT="$OUT/apply_${SID}.bat"
{
echo "@echo off"; echo "setlocal"
echo "rem MarioTrickster ${SID}. Double-click. Safe to run again (skips what is already applied)."
echo 'set "PROJ=E:\BaiduNetdiskDownload\MarioTricksterGensparkAI\MarioTrickster"'
echo 'set "PKG=%~dp0"'
echo 'where git >nul 2>nul || (echo [X] git not found & pause & exit /b 1)'
echo 'if not exist "%PROJ%\.git" if exist "%~dp0..\.git" set "PROJ=%~dp0.."'
echo 'if not exist "%PROJ%\.git" (echo Project folder not found. Drag your MarioTrickster folder here and press Enter: & set /p PROJ=)'
echo 'set "PROJ=%PROJ:"=%"'
echo 'if not exist "%PROJ%\.git" (echo [X] not a git project: %PROJ% & pause & exit /b 1)'
echo 'cd /d "%PROJ%"'
echo 'if exist ".git\rebase-apply" git am --abort'
echo 'git checkout genspark_ai_developer'
echo 'git pull'
i=0
for p in "$OUT"/*.patch; do
  i=$((i+1)); pn=$(basename "$p")
  subj=$(sed -n 's/^Subject: \[PATCH[^]]*\] //p' "$p" | head -1)
  frag=$(echo "$subj" | sed 's/^[^:]*: //' | sed 's/[^A-Za-z0-9 ,-].*//' | cut -c1-40 | sed 's/ *$//')   # 在第一个特殊字符处截断，保证和 git log 原文一致
  [ ${#frag} -lt 8 ] && frag=$(echo "$subj" | sed 's/[^A-Za-z0-9 ,-].*//' | cut -c1-40)
  echo "git log --oneline -60 | findstr /C:\"$frag\" >nul && (echo [OK] part $i already applied.) || ("
  echo "  git -c user.name=MarioTrickster -c user.email=local@mariotrickster am --whitespace=nowarn \"%PKG%$pn\" || (git am --abort & echo [X] part $i apply failed, send a screenshot. & pause & exit /b 1)"
  echo ")"
done
echo 'rem S234: your own lines (MyTownStories.json / MyMarioReactions.json) go to GitHub too, as your own commit'
echo 'for %%F in (MyTownStories.json MyMarioReactions.json) do if exist "Assets\Resources\%%F" git add "Assets\Resources\%%F"'
echo 'for %%F in (MyTownStories.json.meta MyMarioReactions.json.meta) do if exist "Assets\Resources\%%F" git add "Assets\Resources\%%F"'
echo 'git diff --cached --quiet || git -c user.name=MarioTrickster -c user.email=local@mariotrickster commit -m "User-written lines (my own edits)"'
echo 'git log --oneline -3'
echo 'echo.'; echo 'echo Upload to GitHub now? Y/N'; echo 'set /p ANS='; echo 'if /I "%ANS%"=="Y" git push'
echo 'echo.'; echo "echo Done. $HINT"; echo 'pause'
} > "$BAT"
sed -i 's/$/\r/' "$BAT"
(cd "$WS/out" && zip -qr "${SID}patch.zip" "${SID}patch")
rm -rf /tmp/vt && git clone -q -b genspark_ai_developer https://github.com/jiaxuGOGOGO/MarioTrickster.git /tmp/vt
(cd /tmp/vt && git -c user.name=t -c user.email=t@t am -q "$OUT"/*.patch)
if diff <(git -C /tmp/vt ls-tree -r HEAD | awk '{print $3,$4}') <(git ls-tree -r HEAD | awk '{print $3,$4}') >/dev/null; then echo "TREE IDENTICAL -> $WS/out/${SID}patch.zip ($N patch)"; else echo "TREE MISMATCH"; exit 1; fi
rm -rf /tmp/vt
