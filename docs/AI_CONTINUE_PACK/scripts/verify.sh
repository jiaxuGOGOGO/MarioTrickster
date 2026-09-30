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
cp "$PACK/sim/Check.cs" "$PACK/sim/FakeUnity.cs" "$WS/sim/" 2>/dev/null  # S216：技能里的体检脚本才是最新的（以前只在 setup 时复制，改了不生效）
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
# S210：网页 owCheck 跑样板小镇 + 拆桥反例，交给 C# 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owCheck,owToText,owClock,owIssueText,OW_SAMPLE};',c);const X=c.X;
const dump=m=>{const r=X.owCheck(m);const o=r.issues.map(i=>i.sev+' '+X.owIssueText(i));if(r.schedule){for(const s of r.schedule.stops)o.push('stop '+s.door.n+' '+X.owClock(s.arrive)+' '+X.owClock(s.leave)+' '+s.path.map(p=>'('+p[0]+','+p[1]+')').join(';'));o.push('home '+X.owClock(r.schedule.homeArrive));}return o;};
const m=X.owParse(X.OW_SAMPLE);const b=X.owParse(X.owToText(m));for(const y of [14,19]){const r=b.rows.length-1-y;b.rows[r]=b.rows[r].slice(0,21)+'ww'+b.rows[r].slice(23);}
fs.writeFileSync('$WS/sim/ow_web.json',JSON.stringify({sample:dump(m),broken:dump(b)}));") || rm -f "$WS/sim/ow_web.json"
# S212：网页 owMarioAt（时间滑条）跑样板小镇，每 20 分钟一个时刻，交给 C# 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owCheck,owMarioAt,owClock,OW_SAMPLE,OW};',c);const X=c.X;
const m=X.owParse(X.OW_SAMPLE);const sc=X.owCheck(m).schedule;const o=[];for(let t=X.OW.DayStart;t<=X.OW.DayEnd;t+=20){const w=X.owMarioAt(m,sc,t,X.OW.Rules.marioSpeed,X.OW.Rules.minutesPerSecond);o.push(X.owClock(t)+' '+w.cell[0]+','+w.cell[1]+' '+w.what);}
fs.writeFileSync('$WS/sim/ow_scrub.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_scrub.json"
# S214：网页 syLevelFromTxt 读回 sim 上一步写的 Unity 关卡库 .txt，必须和网页原网格一致（第一次跑时还没有 txt → 跳过）
command -v node >/dev/null && [ -f "$WS/sim/sync_level.txt" ] && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.match(/function syLevelFromTxt[\s\S]*?\n}\n/)[0]+';this.f=syLevelFromTxt;',c);
const u=c.f(fs.readFileSync('$WS/sim/sync_level.txt','utf8'),'x.txt');const g=JSON.parse(fs.readFileSync('$WS/sim/sync_grid.json','utf8'));
const ok=u.name==='同步测试'&&u.goal==='目标'&&u.notes.length===1&&u.notes[0].text==='批注'&&JSON.stringify(u.grid)===JSON.stringify(g);
fs.writeFileSync('$WS/sim/sync_back.json',ok?'ok':'name='+u.name+' goal='+u.goal+' notes='+u.notes.length+' grid='+(JSON.stringify(u.grid)===JSON.stringify(g)));") || rm -f "$WS/sim/sync_back.json"
# S215：网页 owLedger 跑样板小镇（房间 = 内置样板），交给 C# CampaignLedger 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owLedger,owLedgerLines,OW_SAMPLE,OW_ROOMS,SAMPLES,makeWorld};',c);const X=c.X;
const m=X.owParse(X.OW_SAMPLE);const r=X.owLedger(m,X.makeWorld([]),n=>{const k=X.OW_ROOMS[n];return k&&X.SAMPLES[k]?X.SAMPLES[k]:null;},3,3);
fs.writeFileSync('$WS/sim/ow_ledger.json',JSON.stringify(X.owLedgerLines(r)));") || rm -f "$WS/sim/ow_ledger.json"
(cd "$WS/sim" && rm -rf obj bin && dotnet build -c Release -nologo -v q -p:Version=1.0.0 2>&1 | grep -E " error " | head -10; dotnet bin/Release/net8.0/sim.dll) || ok=0
[ $ok = 1 ] && echo "VERIFY ALL GREEN（提醒：Unity 里的 EditMode 测试仍需用户跑）" || { echo "VERIFY FAILED"; exit 1; }
