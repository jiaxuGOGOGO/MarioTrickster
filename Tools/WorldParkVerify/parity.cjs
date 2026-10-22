const fs=require('fs'),path=require('path'),cp=require('child_process'),assert=require('assert');const repo=path.join(__dirname,'..','..');const W=require(path.join(repo,'Tools','LevelStudioWeb','worldpark.js'));const dir=path.join(repo,'.work','worldpark-verify');fs.mkdirSync(dir,{recursive:true});const csDir=path.join(__dirname,'Snapshot');const base=JSON.parse(fs.readFileSync(path.join(repo,'Assets','Resources','WorldPark','Demo.worldpark.json'),'utf8'));const cases=[];function add(name,actions,edit=()=>{}){const p=structuredClone(base);edit(p);cases.push({name,actions,json:JSON.stringify(p)});}const n=p=>p.venues[0].nodes[1];
add('调查通关',['next','observe:notice','observe:maintenance','observe:plate','explore-win','next']);add('调查失败',['next','observe:plate','explore-lose','next']);add('没调查通关',['next','explore-win','next']);add('没调查失败',['next','explore-lose','next']);add('错误阶段调查',['observe:plate','next','observe:plate','observe:plate','win','next']);
for(const [name,edit] of [['全失败结局可保存运行',p=>n(p).points.filter(q=>q.kind==='exit').forEach(q=>q.outcome='lose')],['无地图',p=>n(p).mapText=''],['无T',p=>n(p).mapText=n(p).mapText.replace('T','.')],['破边',p=>n(p).mapText=n(p).mapText.replace('tttttt','...ttt')],['未知格',p=>n(p).mapText=n(p).mapText.replace('T','@')],['非矩形',p=>n(p).mapText=n(p).mapText.replace('tttttt','ttttt')],['点在墙',p=>n(p).points[0].x=0],['点越界',p=>n(p).points[0].x=99],['点重复',p=>n(p).points[1].id=n(p).points[0].id],['线索不存在',p=>n(p).points[0].clue='missing'],['出口结果错误',p=>n(p).points[3].outcome='banana'],['出口缺条件',p=>n(p).points[3].requires=['missing']],['重复条件',p=>n(p).points[3].requires=['plate','plate']],['不支持点',p=>n(p).points[0].kind='npc'],['负计时',p=>n(p).timeout=-1],['未知角色',p=>p.venues[0].role='alien'],['揭示在先',p=>p.venues[0].clues[0].revealAt='intro'],['stage缺省',p=>delete p.venues[0].clues[0].trigger]])add(name,[],edit);

add('旅程：通过→检修通过→记录层',['next','observe:notice','observe:maintenance','observe:plate','explore-win','next','travel:venue_gallery','next','observe:work_order','explore-win','next','travel:venue_archive','next','observe:ledger','explore-win','next']);
add('旅程：通过→检修失败→记录层',['next','explore-win','next','travel:venue_gallery','next','explore-lose','next','travel:venue_archive','next','explore-win','next']);
add('旅程：失败→记录层；检修锁定',['next','explore-lose','next','travel:venue_gallery','travel:venue_archive','next','explore-win','next']);
add('旅程：未结算不能跨会场',['travel:venue_gallery','travel:venue_archive','next','travel:venue_archive']);
add('自定义结局解锁',['next','explore-win','next','travel:venue_gallery'],p=>{p.venues[0].nodes.find(x=>x.id==='clear_end').result='pass_with_ticket';p.links.find(l=>l.to==='venue_gallery').requires='pass_with_ticket';});
add('结局标签any被拒绝',[],p=>{p.venues[0].nodes.find(x=>x.id==='clear_end').result='any';});
add('链接标签不可达警告',[],p=>{p.links[0].requires='never_written';});
add('记录层只能来自本会场结果',['next','explore-win','next','travel:venue_archive'],p=>{p.links=p.links.filter(l=>l.to!=='venue_archive'||l.from!=='venue_town');});

// S252：追逐者（kind=chaser）。解析与预检必须同语义；行为由 C# 探索内核负责（网页无实时探索）。
const tpos=p=>{const rows=W.mapShape(n(p).mapText).rows;for(let r=0;r<rows.length;r++){const x=rows[r].indexOf('T');if(x>=0)return{x,y:rows.length-1-r};}return{x:1,y:1};};
const oni=(p,extra={})=>{const q=n(p).points[0];return Object.assign({id:'oni',kind:'chaser',label:'鬼',x:q.x,y:q.y,speed:34,sight:7,route:[q.x+','+q.y]},extra);};
add('追逐者合法并可运行',['next','explore-win','next'],p=>n(p).points.push(oni(p)));
add('追逐者速度越界',[],p=>n(p).points.push(oni(p,{speed:5})));
add('追逐者视距越界',[],p=>n(p).points.push(oni(p,{sight:30})));
add('追逐者路线格式',[],p=>n(p).points.push(oni(p,{route:['1, 2']})));
add('追逐者路线在墙',[],p=>n(p).points.push(oni(p,{route:['0,0']})));
add('追逐者路线过长',[],p=>{const o=oni(p);o.route=Array(33).fill(o.route[0]);n(p).points.push(o);});
add('追逐者过多',[],p=>{for(let i=0;i<5;i++)n(p).points.push(oni(p,{id:'oni'+i}));});
add('追逐者贴近出生点',[],p=>{const t=tpos(p);n(p).points.push(oni(p,{x:t.x,y:t.y,route:[]}));});
add('追逐者字段类型错误',[],p=>n(p).points.push(oni(p,{speed:'fast'})));
add('非追逐点route类型错误',[],p=>{n(p).points[0].route=[1];});

// S251：弥留之国式博弈。G 为三种模式的样例规则；withGame 把 game 阶段插进第一会场开头。
const BG=require(path.join(repo,'Tools','LevelStudioWeb','borderland.js'));
const seat=(id,name,persona)=>({id,name,persona});
const G={};
G.beauty={mode:'beauty',rounds:6,lives:2,ratio:80,goal:0,seed:7,escalate:1,seats:[seat('me','你','player'),seat('a','阿久','naive'),seat('b','白石','reasoner'),seat('c','千寻','deep'),seat('d','渡','adaptive')]};
G.mark={mode:'mark',rounds:4,lives:1,ratio:0,goal:0,seed:11,escalate:0,seats:[seat('x','狐','liar'),seat('y','鸦','liar'),seat('me','你','player'),seat('a','阿久','naive'),seat('b','白石','loyal'),seat('c','千寻','reasoner')]};
G.split={mode:'split',rounds:6,lives:2,ratio:0,goal:3,seed:3,escalate:0,seats:[seat('me','你','player'),seat('x','狐','liar'),seat('m','镜','mirror'),seat('c','千寻','deep')]};
function withGame(p,g){const v=p.venues[0];v.nodes.find(x=>x.id==='intro').next='duel';v.nodes.push({id:'duel',kind:'game',name:'博弈',text:'',onWin:'investigate',onLose:'reveal_lose',game:structuredClone(g)});}
function play(g,pick){const ch=[];for(let i=0;i<25;i++){const st=BG.run(g,ch);if(st.finished||st.error)break;const probe=BG.run(g,[...ch,'♠']);ch.push(pick(probe.records[probe.records.length-1]));}return ch;}
const informed=play(G.mark,r=>r.informedAnswer),gullible=play(G.mark,r=>r.firstClaim);
add('博弈：数字猜心后进入调查',['next','game:33|27|22|18|15|12','observe:plate','explore-win','next'],p=>withGame(p,G.beauty));
add('博弈：标记按公开记录推理',['next','game:'+informed.join('|'),'next'],p=>withGame(p,G.mark));
add('博弈：标记轻信第一证词',['next','game:'+gullible.join('|'),'next'],p=>withGame(p,G.mark));
add('博弈：分还是偷全分',['next','game:分|分|分|分|分|分','explore-lose','next'],p=>withGame(p,G.split));
add('博弈：错误输入与未完成',['game:33','next','game:abc','game:33','game:33|27|22|18|15|12|9|9|9|9','game:33|2|7|1|0|0','next'],p=>withGame(p,G.beauty));
for(const [name,edit] of [['模式错误',g=>g.mode='poker'],['轮数为零',g=>g.rounds=0],['命数过多',g=>g.lives=21],['比例为零',g=>g.ratio=0],['目标为负',g=>g.goal=-1],['种子为负',g=>g.seed=-5],['追加规则值错',g=>g.escalate=2],['只有一人',g=>g.seats=g.seats.slice(0,1)],['两个玩家',g=>g.seats[1].persona='player'],['重复席位',g=>g.seats[1].id='me'],['席位含斜杠',g=>g.seats[1].id='a/b'],['未知人格',g=>g.seats[1].persona='alien']])add('博弈预检：'+name,[],p=>{const g=structuredClone(G.beauty);edit(g);withGame(p,g);});
add('博弈预检：标记无说谎者',[],p=>{const g=structuredClone(G.mark);g.seats.forEach(s=>{if(s.persona==='liar')s.persona='loyal';});withGame(p,g);});
add('博弈预检：缺少规则对象',[],p=>{withGame(p,G.beauty);delete p.venues[0].nodes.find(x=>x.id==='duel').game;});
add('博弈预检：缺少分支',[],p=>{withGame(p,G.split);p.venues[0].nodes.find(x=>x.id==='duel').onLose='';});

function state(s){return structuredClone({venue:s.venueId,node:s.nodeId,found:[...s.found].sort(),revealed:[...s.revealed].sort(),results:Object.fromEntries(Object.entries(s.results).sort()),rooms:s.roomResults,activities:s.activityResults,log:s.log});}
function js(t){let p;try{p=W.parse(t.json);}catch(e){return{name:t.name,parsed:false};}const issues=W.validate(p).map(i=>[i.severity,i.code,i.venue,i.node].join('|')).sort();let s;try{s=W.start(p);}catch(e){}const states=[];if(s){states.push(state(s));for(const a of t.actions){let ok=true;try{if(a==='next')s.Continue();else if(a==='win'||a==='lose')s.ResolveRoom(a==='win');else if(a.startsWith('explore-'))s.ResolveExplore(a==='explore-win');else if(a.startsWith('observe:'))s.ObserveClue(a.slice(8));else if(a.startsWith('game:'))s.ResolveGame(a.slice(5).split('|'));else s.Travel(a.slice(7));}catch(e){ok=false;}states.push({ok,state:state(s)});}}return{name:t.name,parsed:true,issues,started:!!s,states};}

// 博弈内核逐轮对比：只含一个 game 节点的最小世界包，顺带比较 game 字段解析。
function gpack(g){return JSON.stringify({type:'mariotrickster-worldpark',v:1,id:'gp',name:'gp',premise:'',entryVenue:'v',venues:[{id:'v',name:'v',setting:'',role:'player',suit:'',rules:'',entry:'g',difficulty:0,nodes:[{id:'g',kind:'game',onWin:'w',onLose:'l',game:g},{id:'w',kind:'ending',result:'clear'},{id:'l',kind:'ending',result:'fail'}],clues:[]}],links:[]});}
const rawPack=game=>JSON.stringify({type:'mariotrickster-worldpark',v:1,id:'gp',name:'gp',premise:'',entryVenue:'v',venues:[{id:'v',role:'player',entry:'g',nodes:[{id:'g',kind:'game',game}],clues:[]}],links:[]});
const gcases=[];const gadd=(name,g,choices)=>gcases.push({name,json:typeof g==='string'?g:gpack(g),choices});
for(const seq of [['33','27','22','18','15','12'],['50','50','50','50','50','50'],['0','0','0','0','0','0'],['100','100','100'],['30','24','19','15','12','10','8'],['33'],[],['abc'],['101'],['-1'],['033','27']])gadd('猜心 '+seq.join(','),G.beauty,seq);
gadd('猜心 无追加',{...G.beauty,escalate:0},['33','27','22','18','15','12']);
gadd('猜心 三人比例50',{...G.beauty,ratio:50,seats:G.beauty.seats.slice(0,3)},['20','10','5','2','1','0']);
for(const [name,seq] of [['推理',informed],['轻信',gullible],['全♠',['♠','♠','♠','♠']],['字母',['S','H','D','C']],['非法',['X']],['原型键',['toString']],['半局',informed.slice(0,1)]])gadd('标记 '+name,G.mark,seq);
gadd('标记 种子改变',{...G.mark,seed:99},play({...G.mark,seed:99},r=>r.informedAnswer));
for(const seq of [['分','分','分','分','分','分'],['偷','偷','偷','偷','偷','偷'],['split','steal','split','steal','split','steal'],['分','分','分','偷','分','偷'],['分','分','分','偷','分','分'],['maybe']])gadd('分偷 '+seq.join(','),G.split,seq);
gadd('分偷 旧五人桌',{...G.split,goal:4,seats:[seat('me','你','player'),seat('a','阿久','loyal'),seat('x','狐','liar'),seat('m','镜','mirror'),seat('b','白石','reasoner'),seat('c','千寻','deep')]},['偷','偷','偷','偷','偷','偷']);
G.duel={mode:'beauty',rounds:15,lives:4,ratio:80,goal:0,seed:3,escalate:1,seats:[seat('me','你','player'),seat('a','甲','naive'),seat('b','乙','naive'),seat('c','丙','reasoner'),seat('e','丁','deep')]};
gadd('猜心 决斗 0对100',G.duel,['31','24','28','26','100','100','100']);
gadd('猜心 决斗 跟着写0',G.duel,['31','24','28','26','0','0','0']);
gadd('分偷 目标0',{...G.split,goal:0},['偷','偷','偷','偷','偷','偷']);
gadd('坏规则 两个玩家',{...G.split,seats:[seat('me','你','player'),seat('m2','你2','player')]},['分']);
gadd('解析 seats非数组',rawPack({mode:'mark',seats:'x'}),[]);
gadd('解析 小数轮数',rawPack({mode:'mark',rounds:1.5,seats:[]}),[]);
gadd('解析 缺字段默认',rawPack({mode:'split',seats:[{id:'me',persona:'player'},{id:'x',persona:'liar'}]}),['分']);
function jsGame(t){let p;try{p=W.parse(t.json);}catch(e){return{name:t.name,parsed:false};}const def=p.venues[0].nodes.find(n=>n.kind==='game').game;const issues=BG.check(def).map(i=>i.severity+'|'+i.code).sort();const st=BG.run(def,t.choices);return{name:t.name,parsed:true,issues,finished:st.finished,won:st.won,error:st.error,round:st.round,player:st.player,lives:st.lives,points:st.points,alive:st.alive,prompt:st.prompt,transcript:st.transcript,pending:st.pending,records:st.records.map(r=>({round:r.round,choice:r.choice,livesBefore:r.livesBefore,livesAfter:r.livesAfter,pointsAfter:r.pointsAfter,aliveAfter:r.aliveAfter,correct:r.correct,informedAnswer:r.informedAnswer,firstClaim:r.firstClaim,eliminated:r.eliminated,picks:r.picks,marks:r.marks,target:r.target,opponent:r.opponent,winners:r.winners}))};}

const proj=path.join(csDir,'WorldParkSnapshot.csproj');
fs.writeFileSync(path.join(dir,'cases.json'),JSON.stringify(cases));cp.execFileSync('dotnet',['run','--project',proj,'--',path.join(dir,'cases.json'),path.join(dir,'csharp.json')],{stdio:'inherit'});
fs.writeFileSync(path.join(dir,'game-cases.json'),JSON.stringify(gcases));cp.execFileSync('dotnet',['run','--no-build','--project',proj,'--','games',path.join(dir,'game-cases.json'),path.join(dir,'csharp-games.json')],{stdio:'inherit'});
const cs=JSON.parse(fs.readFileSync(path.join(dir,'csharp.json'),'utf8')),out=cases.map(js);let diffs=[];for(let i=0;i<out.length;i++){if(JSON.stringify(cs[i])!==JSON.stringify(out[i]))diffs.push({name:cases[i].name,cs:cs[i],js:out[i]});}
const csg=JSON.parse(fs.readFileSync(path.join(dir,'csharp-games.json'),'utf8')),outg=gcases.map(jsGame);for(let i=0;i<outg.length;i++){if(JSON.stringify(csg[i])!==JSON.stringify(outg[i]))diffs.push({name:'内核/'+gcases[i].name,cs:csg[i],js:outg[i]});}
// 防止“两边一起坏”：几条必须成立的体验断言（规则改动后若失败，说明效果变了，需要同步改文档与预期）。
const expect=(ok,msg)=>{if(!ok)diffs.push({name:'体验断言/'+msg});};
const g=name=>outg[gcases.findIndex(c=>c.name===name)];
expect(g('标记 推理').won===true,'按公开记录推理应能活过看不见的标记');
expect(g('标记 轻信').won===false,'轻信第一证词应在标记里出局');
expect(g('标记 推理').records[0].informedAnswer===g('标记 推理').records[0].firstClaim,'第一轮说谎者对你说真话以换取信任');
// S252 博弈桌翻牌数据：每轮各座位亮出的选择必须和记录一致。
const r0=n=>g(n).records[0];
expect(r0('猜心 33').picks[g('猜心 33').player]==='33'&&r0('猜心 33').target>=0&&r0('猜心 33').winners.length>0,'猜心每轮记录各座位数字、目标与最接近者');
expect(r0('标记 推理').marks.every((m,i)=>m!==''||!g('标记 推理').alive[i])&&r0('标记 推理').picks[g('标记 推理').player]!=='','标记每轮记录各座位宣告与真实标记');
expect(r0('分偷 偷,偷,偷,偷,偷,偷').opponent>=0&&r0('分偷 偷,偷,偷,偷,偷,偷').picks[r0('分偷 偷,偷,偷,偷,偷,偷').opponent]!==''&&r0('分偷 偷,偷,偷,偷,偷,偷').picks.filter(x=>x!=='').length===2,'分偷每轮只亮出你和本轮对手的选择');
expect(g('猜心 33').finished===false&&g('猜心 33').pending.length>0,'半局应给出下一轮公开信息');
expect(g('分偷 偷,偷,偷,偷,偷,偷').won===false,'一直偷会被防备而分数不足');
expect(g('分偷 分,分,分,分,分,分').won===false,'一直相信会被说谎者和最后一轮背叛害死');
expect(g('分偷 分,分,分,偷,分,分').won===false,'以牙还牙看不穿最后一轮背叛');
expect(g('分偷 分,分,分,偷,分,偷').won===true,'吃一次亏后防备说谎者并看穿最后一轮的人通关');
expect(g('猜心 决斗 0对100').won===true&&g('猜心 决斗 0对100').transcript.some(l=>l.startsWith('0 对 100')),'只剩两人时第三条规则真正生效：对手冲 0，写 100 的人获胜');
expect(g('猜心 决斗 跟着写0').won===false,'决斗里跟着写 0 会被“同数字无效”拖死');
expect(g('猜心 决斗 0对100').transcript.filter(l=>l.startsWith('【新规则】')).length===3,'三条规则逐轮出现，不会一次塞进多条');
expect(outg.some(r=>r.transcript&&r.transcript.some(l=>l.startsWith('【新规则】'))),'数字猜心应出现规则追加');
fs.writeFileSync(path.join(dir,'parity-diff.json'),JSON.stringify(diffs,null,2));console.log('跨端:',cases.length,'博弈内核:',gcases.length,'差异:',diffs.length);if(diffs.length){console.log(diffs.slice(0,8).map(d=>d.name));process.exitCode=1;}else fs.writeFileSync(path.join(dir,'parity-pass.json'),JSON.stringify({cases:cases.length,games:gcases.length,pass:true}));
