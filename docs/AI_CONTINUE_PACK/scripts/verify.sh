#!/usr/bin/env bash
# 每次改完代码必跑：① 运行时代码编译 ② 编辑器+测试编译 ③ 测试里的字符串断言对照源码 ④ 纯逻辑体检（样板/寻路/说明书/监狱塔）
# 全部通过才允许打补丁。任何一步失败 → 修好再跑，不要跳过。
unset version
WS=${WS:-/home/user/workspace}
PACK=$(cd "$(dirname "$0")" && pwd)
ok=1
echo "== 1/4 运行时编译 =="; (cd "$WS/cc" && dotnet build -nologo -v q -o out -p:Version=1.0.0 2>&1 | grep -E " error |Build succeeded" | sed 's/\[.*//' | sort -u | head -20) | tee /tmp/b1.txt; grep -q "Build succeeded" /tmp/b1.txt || ok=0
echo "== 2/4 编辑器+测试编译 =="; (cd "$WS/cc2/full" && dotnet build -nologo -v q -p:Version=1.0.0 2>&1 | grep -E " error |Build succeeded" | sed 's/\[.*//' | sort -u | head -20) | tee /tmp/b2.txt; grep -q "Build succeeded" /tmp/b2.txt || ok=0
echo "== 3/4 字符串断言 =="; python3 "$PACK/check_string_asserts.py" "$WS/repo/Assets" || ok=0
echo "== 4/4 纯逻辑体检 =="
python3 - "$WS" <<'PY'
import re,sys
ws=sys.argv[1]
s=open(ws+'/repo/Assets/Scripts/Editor/Step1PrankRoomBuilder.cs',encoding='utf-8').read()
i=s.index('public static readonly string[] Room =');j=s.index('};',i)
open(ws+'/sim/room_template.txt','w').write('\n'.join(re.findall(r'"([^"]+)"',s[i:j])))
PY
# S208：用网页 logic.js 生成全部向导关卡，交给 C# 体检逐字对照（没装 node 就跳过对照）
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && python3 build.py >/dev/null && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(src.indexOf('const ELEMENTS='),src.indexOf('// ── 状态'))+';this.X={wizardLevel,WIZ_STARS};',c);
const o={};for(const s of c.X.WIZ_STARS)for(const t of [20,30,40])o['wiz_'+s+'_'+t]=c.X.wizardLevel(s,t,'').grid;fs.writeFileSync('$WS/sim/wiz_web.json',JSON.stringify(o));") || rm -f "$WS/sim/wiz_web.json"
(cd "$WS/sim" && rm -rf obj bin && dotnet build -c Release -nologo -v q -p:Version=1.0.0 2>&1 | grep -E " error " | head -10; dotnet bin/Release/net8.0/sim.dll) || ok=0
[ $ok = 1 ] && echo "VERIFY ALL GREEN（提醒：Unity 里的 EditMode 测试仍需用户跑）" || { echo "VERIFY FAILED"; exit 1; }
