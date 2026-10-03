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
# S217：网页 owResize 跑同一组扩展/裁剪，交给 C# OverworldMap.Resize 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owResize,OW_SAMPLE};',c);const X=c.X;
const cases={all8:[8,8,8,8],east16:[0,16,0,0],west16:[16,0,0,0],north16:[0,0,16,0],south16:[0,0,0,16],x2:[0,44,0,32],crop4:[-4,-4,-4,-4],crop1:[-1,-1,-1,-1]};const o={};
for(const k in cases){const m=X.owParse(X.OW_SAMPLE);const r=X.owResize(m,...cases[k]);o[k]=r.ok?m.rows.join('\\n')+'|'+r.lost+'|'+m.notes.map(n=>n.x+','+n.y).join(';'):'x '+r.why;}
fs.writeFileSync('$WS/sim/ow_resize.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_resize.json"
# S218：网页 owCheck / owPropsDescribe / owDayOf / owWeatherPreview 跑星露大镇，交给 C# OverworldProps / OverworldEvents 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owCheck,owIssueText,owPropsDescribe,owDayOf,owWeatherPreview,OW_BIG_SAMPLE};',c);const X=c.X;
const m=X.owParse(X.OW_BIG_SAMPLE);const o=[];for(const i of X.owCheck(m).issues)o.push(i.sev+' '+X.owIssueText(i));for(const l of X.owPropsDescribe(m))o.push('D '+l.text);
for(let d=1;d<=20;d++){const w=X.owDayOf(m.name,d);o.push('W '+d+' '+w.kind+' '+w.wind);}for(const l of X.owWeatherPreview(m,1,8))o.push(l);
fs.writeFileSync('$WS/sim/ow_props.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_props.json"
# S219：网页 星露山镇 检查 / 总览 / 天气池 / 瞄准 / 山丘视线，交给 C# 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owCheck,owIssueText,owPropsDescribe,owDayOfMap,owWeatherPreview,owDefaultAim,owAimStep,owAimLanding,owAimOk,owFind,owLos,OW_MTN_SAMPLE};',c);const X=c.X;
const m=X.owParse(X.OW_MTN_SAMPLE);const o=[];for(const i of X.owCheck(m).issues)o.push(i.sev+' '+X.owIssueText(i));for(const l of X.owPropsDescribe(m))o.push('D '+l.text);
for(let d=1;d<=30;d++){const w=X.owDayOfMap(m,d);o.push('W '+d+' '+w.kind+' '+w.wind);}for(const l of X.owWeatherPreview(m,1,14))o.push(l);
const k=[26,16];let a=X.owDefaultAim(m,k);const home=X.owFind(m,'M')[0];for(const key of [0,0,1,1,1,2,3,3,0,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]){a=X.owAimStep(m,k,a,key);const l=X.owAimLanding(m,k,a.dir,a.dist,2);o.push('A '+a.dir+' '+a.dist+' '+l[0]+','+l[1]+' '+(X.owAimOk(m,k,a.dir,l,home)?'True':'False'));}
for(const q of [[35.5,20.5,35.5,24.5],[33.5,22.5,38.5,22.5],[36.5,22.5,30.5,22.5],[50.5,31.5,50.5,38.5],[40.5,30.5,60.5,30.5]])o.push('L '+(X.owLos(m,...q)?'True':'False'));
fs.writeFileSync('$WS/sim/ow_mtn.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_mtn.json"
# S220：网页 星露雷镇 检查 / 雷区落点 / 轮次 / 文本往返 / 天气池，交给 C# 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={owParse,owToText,owCheck,owIssueText,owStormVolley,owStormVolleyIndex,owDayOfMap,OW_STORM_SAMPLE};',c);const X=c.X;
const m=X.owParse(X.OW_STORM_SAMPLE);const o=[];for(const i of X.owCheck(m).issues)o.push(i.sev+' '+X.owIssueText(i));
for(let z=0;z<m.storms.length;z++)for(let v=0;v<12;v++)o.push('V '+z+' '+v+' '+X.owStormVolley(m,z,5,v).map(q=>q[0]+','+q[1]).join(';'));
for(const mm of [360,399.9,400,455.5,800])for(let z=0;z<2;z++)o.push('I '+mm+' '+z+' '+X.owStormVolleyIndex(mm,z));
o.push('T '+X.owToText(m).split('\n').join('/'));for(let d=1;d<=20;d++){const w=X.owDayOfMap(m,d);o.push('W '+d+' '+w.kind);}
fs.writeFileSync('$WS/sim/ow_storm.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_storm.json"
# S232：网页 居民台词彩排 / 道具箱洗牌袋 / 数值关系 / 住户文本，交给 C# TownStory / OverworldPickupBag / TuningAudit 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={RULES,OW,tuAudit,tsRehearse,tsRehearsalSummary,tsCoverage,TS_STORIES,owParse,owToText,owToJson,owFromJson,owParseResident,OW_SAMPLE,OW_BIG_SAMPLE,OW_MTN_SAMPLE,owBagPreview};',c);const X=c.X;const o=[];
for(const s of [X.OW_SAMPLE,X.OW_BIG_SAMPLE,X.OW_MTN_SAMPLE]){const m=X.owParse(s);const r=X.tsRehearse(m,X.TS_STORIES,21);o.push('R '+X.tsRehearsalSummary(r,21));for(const l of r.lines)o.push('L '+l);for(const l of X.owBagPreview(m,1,12))o.push('B '+l);}
for(const l of X.tsCoverage(X.TS_STORIES))o.push('C '+l);for(const r of X.tuAudit())o.push('T '+(r.ok?'ok':'bad')+' '+r.rule+' '+r.detail);
o.push('S '+X.RULES.RunSpeed+' '+X.RULES.StartDelay+' '+X.OW.Rules.marioSpeed+' '+X.OW.Rules.tricksterSpeed+' '+X.OW.Rules.minutesPerSecond+' '+X.OW.Rules.visitMinutes);
const m=X.owParse(X.OW_SAMPLE);m.residents.push(X.owParseResident(' 3 | 王|阿姨 | painter'),X.owParseResident('1|阿梅'));m.residents=m.residents.filter(Boolean);
o.push('X '+X.owToText(X.owFromJson(JSON.parse(JSON.stringify(X.owToJson(m))))).split('\n').filter(l=>l.startsWith('# Resident')).join('/'));
fs.writeFileSync('$WS/sim/ow_story.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_story.json"
# S233：网页 当场喊 / 居民笔记本 / 台词检查 / 台词导出 / 灵感骰子 / 读 .asset，交给 C# 逐字对照
command -v node >/dev/null && (cd "$WS/repo/tools/LevelStudioWeb" && node -e "
const fs=require('fs'),vm=require('vm');const html=fs.readFileSync('index.html','utf8');
const src=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]).join('\n');
const c={};vm.createContext(c);vm.runInContext(src.slice(0,src.indexOf('// ── 状态'))+';this.X={tsRehearse,tsWitness,tsNearestDoor,tsNotebookText,tsValidate,tsToJson,idRolls,idRoll,owFind,tuFromYaml,tuDiff,TS_STORIES,owParse,owDayOfMap,OW_WEATHER,OW_SAMPLE,OW_BIG_SAMPLE};',c);const X=c.X;const o=[];
for(const s of [X.OW_SAMPLE,X.OW_BIG_SAMPLE]){const m=X.owParse(s);const r=X.tsRehearse(m,X.TS_STORIES,21);const mem=r.mem;
 for(let d=22;d<=27;d++)for(const dr of m.doors.slice().sort((a,b)=>a.n-b.n)){const w=X.OW_WEATHER[X.owDayOfMap(m,d).kind].toLowerCase();const l=X.tsWitness(m,X.TS_STORIES,mem,dr.n,d%2?'K':'i',w,d);const l2=X.tsWitness(m,X.TS_STORIES,mem,dr.n,'K',w,d);o.push('W '+d+' '+dr.n+' '+(l?l.id:'-')+' '+(l2?'DUP':''));}
 for(const l of X.tsNotebookText(m,X.TS_STORIES,mem).split('\n'))o.push('N '+l);
 for(const q of [[3.5,3.5],[20,10],[0,0],[50.5,30.5]])o.push('D '+X.tsNearestDoor(m,q[0],q[1],7));for(const dr of m.doors.slice().sort((a,b)=>a.n-b.n)){const c=X.owFind(m,String(dr.n));if(c.length!==1)continue;for(const e of [6.9,7.1])o.push('DE '+dr.n+' '+e+' '+X.tsNearestDoor(m,c[0][0]+0.5+e,c[0][1]+0.5,7));}
 for(const l of X.idRolls(m,7,4))o.push('I '+l);o.push('IS '+X.idRoll(m,7,0).lineStub);}
for(const v of X.tsValidate(X.TS_STORIES))o.push('V '+v);
const bad=JSON.parse(JSON.stringify(X.TS_STORIES.slice(0,3)));bad[0].needs=['frends>=2'];bad[1].tone='sincere';bad[1].once=false;bad[2].when='noon';for(const v of X.tsValidate(bad))o.push('VB '+v);
o.push('J '+(X.tsToJson(X.TS_STORIES)===fs.readFileSync('../../Assets/Resources/TownStories.json','utf8')?'same':'diff'));
const y='%YAML 1.1\nMonoBehaviour:\n  m_Name: RushMarioTuning\n  dataVersion: 26\n  overworldMarioSpeed: 3.9\n  overworldChaseSpeed: 4.25\n  soundRings: 0\n  nested:\n    x: 1\n  weird name: 3\n  overworldVisionRange: 1e1\n';
const a=X.tuFromYaml(y);o.push('Y '+Object.keys(a).map(k=>k+'='+a[k]).join(','));for(const d of X.tuDiff(a))o.push('YD '+d);
fs.writeFileSync('$WS/sim/ow_s233.json',JSON.stringify(o));") || rm -f "$WS/sim/ow_s233.json"
(cd "$WS/sim" && rm -rf obj bin && dotnet build -c Release -nologo -v q -p:Version=1.0.0 2>&1 | grep -E " error " | head -10; dotnet bin/Release/net8.0/sim.dll) || ok=0
[ $ok = 1 ] && echo "VERIFY ALL GREEN（提醒：Unity 里的 EditMode 测试仍需用户跑）" || { echo "VERIFY FAILED"; exit 1; }
