// S249：世界工坊唯一主创作入口；地图/横版编辑仍是它的子工具。不会改写旧 LIB/OWLIB 或旧地图。
const WPLS = 'mariotrickster.studio.worldpark.v1';
const WPUI = { pack:null, vi:0, ni:0, ci:0, pi:0, session:null, notice:'', spatial:null };
const wpEl = id => document.getElementById(id);
const wpEsc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const wpKind = { story:'剧情', room:'守宝房', explore:'现场探索', game:'多人博弈（数字猜心/标记/分还是偷）', reveal:'揭示', ending:'结局', concept:'新玩法草稿' };
const wpV = () => WPUI.pack.venues[WPUI.vi];
const wpN = () => (wpV()?.nodes || [])[WPUI.ni];
const wpP = () => (wpN()?.points || [])[WPUI.pi];
function wpId(list, base) { let id=base,i=2; while(list.some(x=>x.id===id))id=base+'_'+i++; return id; }
function wpRoomExists(name) { return ['默认房间','default','Assets/Scenes/Step1_PrankRoom.unity'].includes((name||'').trim()) || (typeof OW_ROOMS!=='undefined'&&Object.hasOwn(OW_ROOMS,name)) || (typeof LIB!=='undefined'&&LIB.some(x=>x.name===name)); }
function wpPersist(spatial=false) { if(!spatial&&WPUI.spatial){WPUI.spatial.undoStack=[];WPUI.spatial.redoStack=[];} WPUI.session=null; let saved=false; try { localStorage.setItem(WPLS,WorldPark.toJson(WPUI.pack)); WPUI.notice='草稿已保存在此浏览器；完整分享请导出世界包和引用房间的关卡包。'; saved=true; } catch(e) { WPUI.notice='浏览器保存失败，请立即导出世界包：'+e.message; } wpSummary(); wpUnitySoon(); return saved; }
function wpBackupDraft() { const old=localStorage.getItem(WPLS); if(old)localStorage.setItem(WPLS+'.previous',old); }
function wpMessage(text) { WPUI.notice=text; const el=wpEl('wpNotice'); if(el)el.textContent=text; }
function wpLoadSample() { if(!confirm('载入内置示例会替换当前世界草稿。请先导出你的内容；浏览器也会保留上一个草稿。继续吗？'))return; try { wpBackupDraft();WPUI.pack=WorldPark.parse(JSON.stringify(WORLD_PARK_SAMPLE));WPUI.vi=WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender(); }catch(e){wpMessage(e.message);} }
function wpOptions(items, selected, empty=true) { return (empty?'<option value="">未指定</option>':'')+(items||[]).map(x=>`<option value="${wpEsc(x.id)}" ${x.id===selected?'selected':''}>${wpEsc(x.name||x.label||x.id)} · ${wpEsc(x.id)}</option>`).join(''); }
function wpField(label,field,value,target,multiline=false,help='') { return `<label class="f" for="wp_${target}_${field}">${wpEsc(label)}</label><${multiline?'textarea':'input'} class="f" id="wp_${target}_${field}" data-wp-target="${target}" data-wp-field="${field}" ${multiline?'':`type="text" value="${wpEsc(value)}"`}>${multiline?wpEsc(value):''}${multiline?'</textarea>':''}${help?`<p class="hint">${wpEsc(help)}</p>`:''}`; }
function wpNumber(label,field,value,target,min,max,help='') { return `<label class="f" for="wp_${target}_${field}">${wpEsc(label)}</label><input class="f" id="wp_${target}_${field}" data-wp-target="${target}" data-wp-field="${field}" data-wp-number="true" type="number" min="${min}" max="${max}" step="1" value="${wpEsc(value)}">${help?`<p class="hint">${wpEsc(help)}</p>`:''}`; }
function wpRecord(target) { return target==='world'?WPUI.pack:target==='venue'?wpV():target==='node'?wpN():target==='clue'?wpV().clues[WPUI.ci]:wpP(); }
function wpBindFields() {
  document.querySelectorAll('[data-wp-field]').forEach(el=>el.oninput=()=>{
    const rec=wpRecord(el.dataset.wpTarget),k=el.dataset.wpField;if(!rec)return;
    if(el.dataset.wpNumber==='true'){
      const n=Number(el.value);if(!Number.isInteger(n)||n<Number(el.min)||n>Number(el.max)){wpMessage(`${k} 请填 ${el.min}–${el.max} 的整数；原数值未改变。`);return;}
      if(el.dataset.wpTarget==='point'&&(k==='x'||k==='y')){const check=wpPointPositionCheck(rec,k,n);if(!check.ok){el.value=rec[k];wpMessage(`坐标未改变：${check.reason}`);return;}}
      rec[k]=n;
    }else rec[k]=el.value;
    wpPersist();wpLists();if(el.dataset.wpTarget==='node'&&k==='mapText')wpSpatialRefresh();if(el.dataset.wpTarget==='point'&&(k==='x'||k==='y'))wpSpatialRefresh();
  });
  document.querySelectorAll('[data-wp-select]').forEach(el=>el.onchange=()=>{const [target,field]=el.dataset.wpSelect.split('/'),rec=wpRecord(target);if(!rec)return;rec[field]=el.value;if(target==='point'&&field==='kind')wpPointKindFields(rec);wpPersist();wpRender();});
  document.querySelectorAll('[data-wp-requires]').forEach(el=>el.oninput=()=>{const p=wpP();if(!p)return;p.requires=el.value.split(/[，,]/).map(x=>x.trim()).filter(Boolean);wpPersist();wpLists();});
  document.querySelectorAll('[data-wp-route]').forEach(el=>el.oninput=()=>{const p=wpP();if(!p)return;p.route=el.value.split(/[;；\s]+/).map(x=>x.trim().replace(/，/g,',')).filter(Boolean);wpPersist();wpLists();wpSpatialRefresh();});
}
// S252：追逐者独有字段（speed/sight/route）只在 kind=chaser 时存在，与 C# 导出一致。
function wpPointKindFields(p) {
  if(p.kind==='chaser'){if(!Number.isInteger(p.speed))p.speed=0;if(!Number.isInteger(p.sight))p.sight=0;if(!Array.isArray(p.route))p.route=[];}
  else{delete p.speed;delete p.sight;delete p.route;}
}
function wpLists() {
  const p=WPUI.pack,v=wpV(); if(!v)return;
  wpEl('wpVenueList').innerHTML=p.venues.map((x,i)=>`<button class="btn wp-item" data-wp-venue="${i}" aria-current="${i===WPUI.vi}"><b>${wpEsc(x.name||'未命名会场')}</b><small>${wpEsc(x.id)} · ${(x.nodes||[]).length} 阶段 · ${wpEsc(x.suit||'自定义分类')}</small></button>`).join('');
  document.querySelectorAll('[data-wp-venue]').forEach(b=>b.onclick=()=>{WPUI.vi=Number(b.dataset.wpVenue);WPUI.ni=WPUI.ci=WPUI.pi=0;wpRender();});
  wpEl('wpNodes').innerHTML=(v.nodes||[]).map((n,i)=>{const branch=['room','explore'].includes(n.kind)?`胜利 → ${wpEsc(n.onWin||'未指定')}<br>失败 → ${wpEsc(n.onLose||'未指定')}`:n.kind==='ending'?`结算：${wpEsc(n.result)}`:`→ ${wpEsc(n.next||'未指定')}`;return `<button class="wp-node ${wpEsc(n.kind)}" data-wp-node="${i}" aria-current="${i===WPUI.ni}"><span class="badge">${wpEsc(wpKind[n.kind]||n.kind)}</span><b>${wpEsc(n.name||n.id)}</b><code>${wpEsc(n.id)}</code><small>${branch}</small></button>`;}).join('')||'<div class="empty">此会场还没有阶段。点“添加阶段”。</div>';
  document.querySelectorAll('[data-wp-node]').forEach(b=>b.onclick=()=>{WPUI.ni=Number(b.dataset.wpNode);WPUI.pi=0;wpRender();});
}
function wpSummary() {
  if(!WPUI.pack)return;const issues=WorldPark.validate(WPUI.pack,wpRoomExists),errors=issues.filter(x=>x.severity==='error'),warnings=issues.filter(x=>x.severity==='warn');
  wpEl('wpNotice').textContent=WPUI.notice;wpEl('wpCheckTitle').textContent=errors.length?`${errors.length} 处结构问题`:'世界包结构预检通过';wpEl('wpCheckTitle').style.color=errors.length?'var(--bad)':'var(--ok)';
  wpEl('wpIssues').innerHTML=issues.map(i=>`<div class="owiss ${i.severity==='error'?'Error':'Warn'}"><b>${wpEsc(i.venue)}${i.node?'/'+wpEsc(i.node):''}</b> ${wpEsc(i.text)}<small class="hint"> [${wpEsc(i.code)}]</small></div>`).join('')||'<p class="hint">没有发现结构问题；这不等于物理、AI、视线或游戏体验验收。</p>';
  wpEl('wpCheckInfo').textContent=`${WPUI.pack.venues.length} 会场 · ${WPUI.pack.venues.reduce((s,v)=>s+(v.nodes||[]).length,0)} 阶段 · ${warnings.length} 提醒。探索地图只做结构预检，实际移动/碰撞由 Unity 验收。`;
  wpEl('wpJson').value=WorldPark.toJson(WPUI.pack);wpRehearsal();
  wpVenueCheckSoon();
}
function wpExploreTools(n,v) {
  const host=wpEl('wpExploreTools');if(!host)return;if(!n||n.kind!=='explore'){host.innerHTML='';return;}
  const points=n.points||[],point=points[WPUI.pi];
  host.innerHTML=`<h3>探索地图与可见标记</h3><p class="hint">这是作者表单，不是另一个网页游戏。地图文本使用小镇工坊同一文字地图格式；复制只读取当前旧小镇，不会修改它。点位 x 左 0、y 下 0，必须落在可通行格。</p>${wpField('mapText（探索地图文本）','mapText',n.mapText,'node',true,'T 是探索出生点；外圈须挡路。探索不强制旧小镇的门、日程或 M。')}<div class="wp-toolbar"><button class="btn small" id="wpCopyTown">从当前小镇作者数据复制地图</button></div>${wpNumber('timeout（秒；0 = 无倒计时）','timeout',n.timeout,'node',0,2147483647)}<div id="wpSpatialHost"></div><label class="f" for="wpPointSelect">编辑哪一个探索点</label><select class="f" id="wpPointSelect">${wpOptions(points,point?.id,false)}</select><div class="wp-toolbar"><button class="btn small" id="wpAddPoint">＋ 调查点：在图上放置</button><button class="btn small" id="wpAddExit">＋ 出口：在图上放置</button><button class="btn small" id="wpAddChaser">＋ 追逐者（鬼）：在图上放置</button><button class="btn small" id="wpDeletePoint">删除选中点</button></div>${point?wpPointForm(point,v):'<p class="hint">选择“＋ 调查点”“＋ 出口”或“＋ 追逐者”后，在画布的可通行格点击放置。调查点会同时建立当前阶段的 interact 线索；玩家彩排不会看到出口 outcome。追逐者（鬼）会巡逻、看见玩家就追，碰到玩家 = 本次探索失败。</p>'}`;
  wpEl('wpCopyTown').onclick=wpCopyTownMap;wpEl('wpAddPoint').onclick=()=>wpSpatialArm('clue');wpEl('wpAddExit').onclick=()=>wpSpatialArm('exit');wpEl('wpAddChaser').onclick=()=>wpSpatialArm('chaser');wpEl('wpDeletePoint').onclick=wpSpatialDelete;wpEl('wpPointSelect').onchange=e=>{WPUI.pi=points.findIndex(x=>x.id===e.target.value);wpRender();};wpSpatialMount(n,v);
}
function wpPointForm(point,v) {
  const interact=(v.clues||[]).filter(c=>c.trigger==='interact'&&c.foundAt===wpN().id);
  const head=wpField(point.kind==='chaser'?'鬼的名字（玩家被抓时看到）':'标记名称（玩家可见）','label',point.label,'point')+wpField('探索点ID','id',point.id,'point')+`<label class="f" for="wpPointKind">点类型</label><select class="f" id="wpPointKind" data-wp-select="point/kind"><option value="clue" ${point.kind==='clue'?'selected':''}>clue / 观察线索</option><option value="exit" ${point.kind==='exit'?'selected':''}>exit / 出口</option><option value="chaser" ${point.kind==='chaser'?'selected':''}>chaser / 追逐者（鬼）</option></select><div class="row"><div>${wpNumber('x','x',point.x,'point',-2147483648,2147483647)}</div><div>${wpNumber('y','y',point.y,'point',-2147483648,2147483647)}</div></div>`;
  if(point.kind==='chaser')return head+`<div class="row"><div>${wpNumber('speed（十分之一格/秒；0=默认34）','speed',point.speed??0,'point',0,80,'允许 10–80；玩家是 40。比玩家慢才能被甩开。')}</div><div>${wpNumber('sight（视距格数；0=默认7）','sight',point.sight??0,'point',0,20,'视野是面前 ±60° 扇形，墙会挡；1.5 格内背后也会察觉。')}</div></div><label class="f" for="wpPointRoute">route（巡逻格，按顺序循环；写成 x,y，用分号或空格隔开；空=原地守望）</label><input class="f" id="wpPointRoute" data-wp-route="true" value="${wpEsc((point.route||[]).join('; '))}"><p class="hint">高草里静止不动只有贴身才会被发现；在草里走动草会晃，鬼会过来查看。设计建议：最短路线穿过它的巡逻线，同时留一条有高草或墙角掩护的绕路。</p>`;
  return head+(point.kind==='clue'?`<label class="f" for="wpPointClue">关联 interact 线索</label><select class="f" id="wpPointClue" data-wp-select="point/clue">${wpOptions(interact,point.clue)}</select><p class="hint">进入 explore 不会自动发现；玩家必须在真实阶段交互，网页彩排则手动点击“观察”。</p>`:`<label class="f" for="wpPointOutcome">作者出口结果</label><select class="f" id="wpPointOutcome" data-wp-select="point/outcome"><option value="win" ${point.outcome==='win'?'selected':''}>win</option><option value="lose" ${point.outcome==='lose'?'selected':''}>lose</option></select>`) + `<label class="f" for="wpPointRequires">requires（逗号分隔的线索ID，可留空）</label><input class="f" id="wpPointRequires" data-wp-requires="true" value="${wpEsc((point.requires||[]).join(', '))}"><p class="hint">出口所需线索只在这里给作者编辑；玩家视图不泄漏结果。</p>`;
}
function wpCopyTownMap() {
  const n=wpN();if(!n||n.kind!=='explore'){wpMessage('请先选择一个 explore 阶段。');return;}
  if(typeof OWLIB==='undefined'||!OWLIB.length||typeof owToText!=='function'){wpMessage('当前浏览器没有可读取的小镇作者数据；旧地图没有被改动。');return;}
  const i=typeof OWCUR==='number'?Math.max(0,Math.min(OWCUR,OWLIB.length-1)):0,source=OWLIB[i];n.mapText=owToText(source);wpPersist();wpRender();wpMessage(`已从当前小镇「${source.name||'未命名小镇'}」复制地图文本到此 explore；旧地图未修改。`);
}
function wpPointPositionCheck(point,field,value) {
  if(typeof WorldParkSpatial==='undefined')return {ok:false,reason:'空间画布模块尚未接入构建；当前未保存坐标改动。'};
  const x=field==='x'?value:point.x,y=field==='y'?value:point.y;
  return WorldParkSpatial.canPlace(wpN()?.mapText,x,y);
}
function wpSpatialRefresh() { if(WPUI.spatial?.opts)WPUI.spatial.opts.mapText=wpN()?.mapText||'';WPUI.spatial?.refresh(); }
function wpSpatialArm(kind) { if(!WPUI.spatial){wpMessage('空间画布模块尚未接入构建；未新建点位。');return;}WPUI.spatial.arm(kind); }
function wpSpatialSelect(index) { WPUI.pi=index;wpRender(); }
function wpSpatialPointFields() { const point=wpP();if(!point)return;document.querySelectorAll('[data-wp-target="point"][data-wp-field]').forEach(el=>{if(el.dataset.wpField==='x'||el.dataset.wpField==='y')el.value=point[el.dataset.wpField];}); }
function wpCreateExplorePoint(kind,x,y) {
  const n=wpN(),v=wpV();if(!n||n.kind!=='explore'||!v)return -1;
  const check=typeof WorldParkSpatial==='undefined'?{ok:false,reason:'空间画布模块尚未接入构建。'}:WorldParkSpatial.canPlace(n.mapText,x,y);if(!check.ok){wpMessage(`未新建点位：${check.reason}`);return -1;}
  const points=n.points||(n.points=[]);let clue='';
  if(kind==='clue'){
    const clues=v.clues||(v.clues=[]),clueId=wpId(clues,'clue');
    clues.push({id:clueId,name:'新调查线索（待写）',foundAt:n.id,revealAt:(v.nodes||[]).find(x=>x.kind==='reveal')?.id||'',hint:'【待写草稿】请写玩家先观察到的事实。',truth:'【待写草稿】请写之后重新解释的真相。',trigger:'interact'});clue=clueId;
  }
  if(kind==='chaser'){points.push({id:wpId(points,'oni'),kind:'chaser',label:'鬼',clue:'',outcome:'',x,y,requires:[],speed:0,sight:0,route:[]});WPUI.pi=points.length-1;wpPersist(true);return WPUI.pi;}
  points.push({id:wpId(points,kind==='clue'?'clue_point':'exit'),kind,label:kind==='clue'?'新调查点（待写）':'新出口（待写）',clue,outcome:kind==='exit'?'win':'',x,y,requires:[]});WPUI.pi=points.length-1;wpPersist(true);return WPUI.pi;
}
function wpDeleteExplorePoint(index) { const n=wpN();if(!n||n.kind!=='explore'||!n.points?.[index])return -1;n.points.splice(index,1);WPUI.pi=Math.max(0,Math.min(index,n.points.length-1));wpPersist(true);return n.points.length?WPUI.pi:0; }
function wpSpatialDelete() { if(!WPUI.spatial){wpMessage('空间画布模块尚未接入构建；未删除点位。');return;}WPUI.spatial.deleteSelected(); }
function wpSpatialMount(n,v) {
  const host=wpEl('wpSpatialHost');if(!host)return;
  if(typeof WorldParkSpatial==='undefined'){host.innerHTML='<p class="empty">空间画布模块尚未接入构建；当前点位不会被静默新增或移动。</p>';return;}
  if(!WPUI.spatial)WPUI.spatial=new WorldParkSpatial.Editor();
  WPUI.spatial.mount(host,{contextKey:`${v.id}/${n.id}`,mapText:n.mapText,points:n.points||(n.points=[]),clues:v.clues||(v.clues=[]),selectedIndex:WPUI.pi,
    onSelect:index=>wpSpatialSelect(index),
    onCreate:(kind,x,y)=>wpCreateExplorePoint(kind,x,y),
    onDelete:index=>wpDeleteExplorePoint(index),
    onRestore:index=>{WPUI.pi=Math.max(0,index);},
    onCommit:change=>{wpPersist(true);if(change.type==='undo'||change.type==='redo')wpRender();else {wpSpatialPointFields();wpSpatialRefresh();}}
  });
}
function wpRender() {
  if(!WPUI.pack)return;const p=WPUI.pack;if(!p.venues.length){p.venues.push(wpBlankVenue('venue_1'));WPUI.vi=0;}WPUI.vi=Math.max(0,Math.min(WPUI.vi,p.venues.length-1));const v=wpV();WPUI.ni=Math.max(0,Math.min(WPUI.ni,(v.nodes||[]).length-1));WPUI.ci=Math.max(0,Math.min(WPUI.ci,(v.clues||[]).length-1));WPUI.pi=Math.max(0,Math.min(WPUI.pi,(wpN()?.points||[]).length-1));
  wpLists();wpEl('wpWorldFields').innerHTML=wpField('世界名称','name',p.name,'world')+wpField('世界ID','id',p.id,'world')+wpField('世界设定（作者可自由定义）','premise',p.premise,'world',true)+`<label class="f" for="wpEntryVenue">首个会场</label><select class="f" id="wpEntryVenue" data-wp-select="world/entryVenue">${wpOptions(p.venues,p.entryVenue)}</select>`;
  wpEl('wpVenueFields').innerHTML=wpField('会场名称','name',v.name,'venue')+wpField('会场ID','id',v.id,'venue',false,'ID改名后请同步修复链接；系统不会偷偷改写你的分支。')+`<div class="row"><div>${wpField('分类 / 花色（自由文字）','suit',v.suit,'venue')}</div><div>${wpNumber('难度标记（非负整数）','difficulty',v.difficulty,'venue',0,2147483647)}</div></div>`+wpField('空间设定','setting',v.setting,'venue')+`<label class="f" for="wpVenueRole">玩家角色</label><select class="f" id="wpVenueRole" data-wp-select="venue/role"><option value="trickster" ${v.role==='trickster'?'selected':''}>trickster / 捣蛋者</option><option value="player" ${v.role==='player'?'selected':''}>player / 玩家</option>${!['trickster','player'].includes(v.role)?`<option value="${wpEsc(v.role)}" selected>待适配：${wpEsc(v.role)}</option>`:''}</select>`+wpField('入场就应公开的胜负规则','rules',v.rules,'venue',true)+`<label class="f" for="wpStageEntry">本会场入口阶段</label><select class="f" id="wpStageEntry" data-wp-select="venue/entry">${wpOptions(v.nodes,v.entry)}</select>`;
  const n=wpN();wpEl('wpNodeFields').innerHTML=n?wpNodeForm(n,v):'<p class="empty">请添加或选择一个阶段。</p>';wpExploreTools(n,v);
  wpEl('wpClueSelect').innerHTML=wpOptions(v.clues,v.clues[WPUI.ci]?.id,false);const c=v.clues[WPUI.ci];const foundOptions=c?.trigger==='interact'?(v.nodes||[]).filter(x=>x.kind==='explore'):v.nodes;
  wpEl('wpClueFields').innerHTML=c?wpField('线索名称','name',c.name,'clue')+wpField('线索ID','id',c.id,'clue')+`<label class="f" for="wpClueTrigger">发现方式</label><select class="f" id="wpClueTrigger" data-wp-select="clue/trigger"><option value="stage" ${c.trigger==='stage'?'selected':''}>stage / 进入阶段自动发现</option><option value="interact" ${c.trigger==='interact'?'selected':''}>interact / 在 explore 中交互发现</option></select><label class="f" for="wpFound">首次观察阶段</label><select class="f" id="wpFound" data-wp-select="clue/foundAt">${wpOptions(foundOptions,c.foundAt)}</select><label class="f" for="wpRevealed">重新解释阶段</label><select class="f" id="wpRevealed" data-wp-select="clue/revealAt">${wpOptions(v.nodes,c.revealAt)}</select>`+wpField('玩家先能看见的事实','hint',c.hint,'clue',true)+wpField('作者真相（玩家彩排不会提前显示）','truth',c.truth,'clue',true):'<p class="hint">线索将事实与解释分开；interact 线索进入阶段不会自动发现。</p>';
  wpEl('wpLinks').innerHTML=p.links.map((l,i)=>`<div class="wp-link"><select class="f" aria-label="链接${i+1}来源" data-wp-link="${i}/from">${wpOptions(p.venues,l.from)}</select><span>→</span><select class="f" aria-label="链接${i+1}目的" data-wp-link="${i}/to">${wpOptions(p.venues,l.to)}</select><input class="f" aria-label="链接${i+1}条件" data-wp-link="${i}/requires" value="${wpEsc(l.requires)}" placeholder="结局标签；any=任意结局"><button class="btn small" data-wp-delete-link="${i}" aria-label="删除链接${i+1}">删除</button></div>`).join('')||'<p class="hint">会场链接可以匹配任意作者结局标签（如recorded）或any任意结局，不强制你收集旧小镇的门或日程。</p>';
  document.querySelectorAll('[data-wp-link]').forEach(el=>el.onchange=()=>{const [i,k]=el.dataset.wpLink.split('/');p.links[Number(i)][k]=el.value;wpPersist();});document.querySelectorAll('[data-wp-delete-link]').forEach(el=>el.onclick=()=>{p.links.splice(Number(el.dataset.wpDeleteLink),1);wpPersist();wpRender();});wpBindFields();wpSummary();
}
function wpNodeForm(n,v) {
  const kindOptions=Object.hasOwn(wpKind,n.kind)?'':`<option value="${wpEsc(n.kind)}" selected>待适配：${wpEsc(n.kind)}</option>`;
  if(n.kind==='game'&&!n.game){n.game=wpBlankGame();wpPersist();}
  let branch='';if(['room','explore','game'].includes(n.kind))branch=(n.kind==='room'?wpField('关联现有房间名称','room',n.room,'node',false,'需与Unity关卡库/内置样板同名；世界包本身不携带房间网格。'):n.kind==='game'?wpGameForm(n):'<p class="hint">探索地图、超时和点位在本面板下方的“探索地图与可见标记”里编辑。</p>')+`<label class="f" for="wpWin">胜利后</label><select class="f" id="wpWin" data-wp-select="node/onWin">${wpOptions(v.nodes,n.onWin)}</select><label class="f" for="wpLose">失败后</label><select class="f" id="wpLose" data-wp-select="node/onLose">${wpOptions(v.nodes,n.onLose)}</select>`;else if(n.kind==='ending')branch=`${wpField('本场结局标签（clear/fail兼容，也可写recorded等自定义标签；any保留给链接通配）','result',n.result,'node')}`;else branch=`<label class="f" for="wpNext">下一阶段</label><select class="f" id="wpNext" data-wp-select="node/next">${wpOptions(v.nodes,n.next)}</select>`;
  return wpField('阶段名称','name',n.name,'node')+wpField('阶段ID','id',n.id,'node')+`<label class="f" for="wpNodeKind">阶段类型</label><select class="f" id="wpNodeKind" data-wp-select="node/kind">${kindOptions}${Object.entries(wpKind).map(([k,l])=>`<option value="${k}" ${n.kind===k?'selected':''}>${l}</option>`).join('')}</select>`+wpField('玩家可见的文字','text',n.text,'node',true)+branch;
}
// S251 博弈阶段：参数用 JSON 编辑（与 Unity 世界窗口、C# BorderlandGame.Def 同字段）；校验直接用共享内核。
function wpBlankGame() { return {mode:'beauty',rounds:6,lives:3,ratio:80,goal:0,seed:1,escalate:1,seats:[{id:'me',name:'你',persona:'player'},{id:'a',name:'对手',persona:'reasoner'}]}; }
function wpGameForm(n) {
  const issues=BorderlandGame.check(n.game);
  return `<p class="hint">博弈参数：mode = beauty 数字猜心 / mark 看不见的标记 / split 分还是偷；persona = player 玩家本人（恰好一位）/ naive 直觉型 / reasoner 推一层 / deep 深推 / adaptive 跟随者 / liar 说谎者 / loyal 老实人 / mirror 以牙还牙。改完离开文本框即校验；体验请用下方彩排实际打一局，或运行剧情模拟。</p><label class="f" for="wpGameJson">博弈参数（JSON）</label><textarea class="f" id="wpGameJson" rows="9" onchange="wpGameJson(this.value)">${wpEsc(JSON.stringify(n.game,null,1))}</textarea>`+(issues.length?`<p class="hint">${issues.map(i=>(i.severity==='error'?'错误：':'提醒：')+wpEsc(i.text)).join('<br>')}</p>`:'<p class="hint">博弈参数检查通过。</p>');
}
function wpGameJson(text) { const n=wpN();if(!n||n.kind!=='game')return;let g;try{g=JSON.parse(text);}catch(e){wpMessage('博弈参数不是有效 JSON，原参数未改变：'+e.message);return;}if(!g||typeof g!=='object'||Array.isArray(g)||!Array.isArray(g.seats)){wpMessage('博弈参数必须是对象并包含 seats 列表；原参数未改变。');return;}n.game=g;WPUI.session=null;wpPersist();wpRender(); }
function wpBlankNode(id='intro') { return {id,kind:'story',name:id==='intro'?'入场':'新阶段',text:'',room:'',next:'',onWin:'',onLose:'',result:'',mapText:'',timeout:0,points:[]}; }
function wpBlankVenue(id) { const intro=wpBlankNode('intro'),done=wpBlankNode('done');intro.next='done';done.kind='ending';done.name='结束';done.result='clear';return {id,name:'我的新会场',setting:'',role:'trickster',suit:'自定义',difficulty:1,rules:'',entry:'intro',nodes:[intro,done],clues:[]}; }
function wpStep(action) { try { if(!WPUI.session)WPUI.session=WorldPark.start(WorldPark.parse(WorldPark.toJson(WPUI.pack)));if(action==='next')WPUI.session.Continue();else if(action==='win'||action==='lose')WPUI.session.ResolveRoom(action==='win');else if(action==='explore-win'||action==='explore-lose')WPUI.session.ResolveExplore(action==='explore-win');else if(action.startsWith('observe:'))WPUI.session.ObserveClue(action.slice(8));else if(action.startsWith('travel:'))WPUI.session.Travel(action.slice(7));else if(action.startsWith('game:'))wpGameChoose(action.slice(5));else if(action==='game-resolve'){WPUI.session.ResolveGame(WPUI.gameChoices.slice());WPUI.gameChoices=[];WPUI.gameKey='';}wpRehearsal(); }catch(e){wpEl('wpPlayer').textContent='彩排已停止：'+e.message;} }
function wpGameChoose(choice) { const n=WPUI.session.Current,trial=(WPUI.gameChoices||[]).concat([String(choice).trim()]),st=BorderlandGame.run(n.game,trial);if(st.error){wpMessage('这个选择无效：'+st.error);return;}WPUI.gameChoices=trial; }
function wpGameRehearsal(s,n,box,controls,button) {
  const key=s.venueId+'/'+n.id;if(WPUI.gameKey!==key||WPUI.gameSession!==s){WPUI.gameKey=key;WPUI.gameSession=s;WPUI.gameChoices=[];}
  const st=BorderlandGame.run(n.game,WPUI.gameChoices);if(st.error){box.textContent+='\n\n博弈数据有误：'+st.error;return;}
  box.textContent+='\n\n这是真实博弈，不是作者注入：NPC 只按人格和公开信息行动，结果由内核算出。\n\n'+st.transcript.slice(-40).join('\n');
  if(st.finished){box.textContent+='\n\n真实结果：'+(st.won?'通过':'失败')+'。';button('提交博弈结果，进入'+(st.won?'通过':'失败')+'分支','game-resolve');return;}
  box.textContent+='\n\n'+st.pending.join('\n')+'\n'+st.prompt;
  if(n.game.mode==='beauty'){const input=document.createElement('input');input.className='f';input.type='number';input.min='0';input.max='100';input.style.width='90px';input.id='wpGamePick';controls.appendChild(input);const b=document.createElement('button');b.className='btn small';b.textContent='写下这个数字';b.onclick=()=>wpStep('game:'+input.value);controls.appendChild(b);}
  else if(n.game.mode==='mark'){for(const suit of ['♠','♥','♦','♣'])button('宣告 '+suit,'game:'+suit);}
  else{button('分','game:分');button('偷','game:偷');}
}
function wpRehearsal() {
  const s=WPUI.session,box=wpEl('wpPlayer'),controls=wpEl('wpPlayerActions');controls.innerHTML='';if(!s){box.textContent='按“从世界入口彩排”检查玩家知道什么、分支会去哪。这里只是阶段彩排，不模拟物理、碰撞、距离、视线、AI或倒计时，也不会保存游戏进度。';wpEl('wpTrace').textContent='';return;}
  const n=s.Current;box.textContent=`${s.Venue.name} / ${n.name}\n${wpKind[n.kind]||n.kind}\n\n${n.text}`;if(n.kind==='room')box.textContent+='\n\n关联房间：'+n.room+'\n下面的守住/失守由作者手动注入，不是已实际试玩。';if(n.kind==='explore')box.textContent+='\n\n此页是阶段彩排：请手动观察线索后，由作者选择模拟活动结果；它不运行地图物理。';
  const evidence=s.Venue.clues.filter(c=>s.found.has(s.venueId+'/'+c.id));for(const c of evidence)box.textContent+=`\n\n${c.name}：${c.hint}${s.revealed.has(s.venueId+'/'+c.id)?'\n重新解释：'+c.truth:''}`;
  const button=(text,action)=>{const b=document.createElement('button');b.className='btn small';b.textContent=text;b.onclick=()=>wpStep(action);controls.appendChild(b);};
  if(['story','reveal'].includes(n.kind))button('继续','next');if(n.kind==='room'){button('注入：守住','win');button('注入：失守','lose');}
  if(n.kind==='explore'){for(const p of n.points||[])if(p.kind==='clue'&&p.clue&&!s.found.has(s.venueId+'/'+p.clue))button('模拟观察：'+(p.label||p.clue),'observe:'+p.clue);if((n.points||[]).some(p=>p.kind==='exit')){button('作者模拟：活动胜利','explore-win');button('作者模拟：活动失败','explore-lose');}}
  if(n.kind==='game')wpGameRehearsal(s,n,box,controls,button);
  if(n.kind==='concept')box.textContent+='\n\n这是尚未实现的玩法草稿；不能模拟成功或当作真实通关。';if(n.kind==='ending'){box.textContent+='\n\n会场总体结算（作者定义）：'+n.result;for(const l of s.pack.links.filter(l=>l.from===s.venueId)){const dest=WorldPark.venue(s.pack,l.to);if(s.CanTravel(l.to))button('前往 '+dest.name,'travel:'+l.to);else box.textContent+='\n后续会场：'+(dest?.name||l.to)+'（条件未满足 / 待适配）';}}
  const authorLog=s.log.map(x=>x.replace(/^揭示线索：.*$/,'作者记录：线索已在可揭示阶段解释').replace(/^活动结果：/,'作者模拟活动：')).join(' → ');wpEl('wpTrace').textContent='作者彩排记录（不是玩家视图；可能含模拟结果）：'+authorLog;
}
function wpDownload(name,text,type='application/json') { const blob=new Blob([text],{type}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000); }
function wpApplyJson(json) { const p=WorldPark.parse(json);wpBackupDraft();WPUI.pack=p;WPUI.vi=WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender(); }
function wpInit() {
  try{const saved=localStorage.getItem(WPLS);WPUI.pack=WorldPark.parse(saved||JSON.stringify(WORLD_PARK_SAMPLE));WPUI.notice=saved?'已恢复此浏览器的世界草稿。':'内置原创示例已载入，可自由修改。';}catch(e){WPUI.pack=WorldPark.parse(JSON.stringify(WORLD_PARK_SAMPLE));WPUI.notice='原浏览器草稿无法读取，未覆盖它；暂时显示样例：'+e.message;}
  wpEl('wpSample').onclick=wpLoadSample;wpEl('wpAddVenue').onclick=()=>{const p=WPUI.pack;p.venues.push(wpBlankVenue(wpId(p.venues,'venue')));WPUI.vi=p.venues.length-1;WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender();};wpEl('wpCloneVenue').onclick=()=>{const v=JSON.parse(JSON.stringify(wpV()));v.id=wpId(WPUI.pack.venues,v.id+'_copy');v.name+='（副本）';WPUI.pack.venues.push(v);WPUI.vi=WPUI.pack.venues.length-1;WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender();};
  wpEl('wpDeleteVenue').onclick=()=>{if(WPUI.pack.venues.length===1){wpMessage('至少保留一个会场。');return;}if(!confirm('删除当前会场？现有链接会保留并提示你修复。'))return;WPUI.pack.venues.splice(WPUI.vi,1);WPUI.vi=WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender();};wpEl('wpAddNode').onclick=()=>{const v=wpV(),n=wpBlankNode(wpId(v.nodes,'stage'));v.nodes.push(n);WPUI.ni=v.nodes.length-1;WPUI.pi=0;wpPersist();wpRender();};wpEl('wpDeleteNode').onclick=()=>{if(!wpN())return;if(!confirm('删除此阶段？分支、线索和探索点引用会保留并提示你修复。'))return;wpV().nodes.splice(WPUI.ni,1);WPUI.ni=WPUI.pi=0;wpPersist();wpRender();};
  wpEl('wpAddClue').onclick=()=>{const v=wpV();v.clues.push({id:wpId(v.clues,'clue'),name:'新线索',foundAt:wpN()?.id||v.entry,revealAt:v.nodes.find(n=>n.kind==='reveal')?.id||'',hint:'',truth:'',trigger:'stage'});WPUI.ci=v.clues.length-1;wpPersist();wpRender();};wpEl('wpDeleteClue').onclick=()=>{if(!wpV().clues[WPUI.ci])return;wpV().clues.splice(WPUI.ci,1);WPUI.ci=0;wpPersist();wpRender();};wpEl('wpClueSelect').onchange=e=>{WPUI.ci=wpV().clues.findIndex(c=>c.id===e.target.value);wpRender();};
  wpEl('wpAddLink').onclick=()=>{WPUI.pack.links.push({from:wpV().id,to:WPUI.pack.venues.find(v=>v.id!==wpV().id)?.id||wpV().id,requires:'clear'});wpPersist();wpRender();};wpEl('wpStart').onclick=()=>{WPUI.session=null;wpStep('start');};wpEl('wpExport').onclick=()=>wpDownload((WPUI.pack.id||'world')+'.worldpark.json',WorldPark.toJson(WPUI.pack));wpEl('wpImport').onclick=()=>wpEl('wpFile').click();wpEl('wpFile').onchange=async e=>{const f=e.target.files[0];if(!f)return;try{if(f.size>1048576)throw Error('文件超过1 MB');wpApplyJson(await f.text());}catch(err){wpMessage('导入被拒绝，当前内容未改：'+err.message);}finally{e.target.value='';}};wpEl('wpApplyJson').onclick=()=>{try{wpApplyJson(wpEl('wpJson').value);}catch(e){wpMessage('应用被拒绝，当前内容未改：'+e.message);}};wpRender();
}
wpInit();
// S253：网页试玩入口（规则全部来自与 C# 逐步对比的纯逻辑；画面在 play.js）。index.html#play 直接玩内置主世界。
function wpPlayCurrent(){if(typeof WPPlay==='undefined'){wpMessage('网页试玩脚本没有载入。');return;}WPPlay.open(WorldPark.toJson(WPUI.pack),{label:'正在试玩：世界工坊里的当前草稿“'+(WPUI.pack.name||WPUI.pack.id)+'”'});}
(()=>{const a=document.getElementById('wpPlayWeb');if(a)a.onclick=wpPlayCurrent;const b=document.getElementById('btnPlayWeb');if(b)b.onclick=wpPlayCurrent;if(typeof location!=='undefined'&&/^#play\b/.test(location.hash)&&typeof WPPlay!=='undefined')WPPlay.open(JSON.stringify(WORLD_PARK_SAMPLE),{label:'正在试玩：内置主世界“空白之城”'});})();

// ═════ S254：创作便利——会场模板 / 只玩这个会场 / 会场体检 / 写进 Unity 项目 ═════
// 模板 = 内置主世界里做好的会场（只读复制，绝不改写内置示例）。
function wpTemplates() { return (typeof WORLD_PARK_SAMPLE!=='undefined'&&WORLD_PARK_SAMPLE.venues)||[]; }
function wpTemplateKind(v) { const nodes=v.nodes||[];if(nodes.some(n=>n.kind==='game'))return '多人博弈';if(nodes.some(n=>n.kind==='explore'&&(n.points||[]).some(p=>p.kind==='chaser')))return '躲鬼探索';if(nodes.some(n=>n.kind==='explore'))return '调查与抉择';return '剧情'; }
function wpTemplateFill() { const sel=wpEl('wpTemplate');if(!sel)return;sel.innerHTML=wpTemplates().map((v,i)=>`<option value="${i}">模板：${wpEsc(v.name||v.id)} · ${wpTemplateKind(v)}</option>`).join('')||'<option value="">没有可用模板</option>'; }
function wpFromTemplate() {
  const t=wpTemplates()[Number(wpEl('wpTemplate').value)];if(!t){wpMessage('没有可用模板。');return;}
  const p=WPUI.pack,v=JSON.parse(JSON.stringify(t));v.id=wpId(p.venues,t.id+'_my');v.name=(t.name||t.id)+'（我的改编）';
  p.venues.push(v);WPUI.vi=p.venues.length-1;WPUI.ni=WPUI.ci=WPUI.pi=0;wpPersist();wpRender();
  wpMessage('已用模板“'+(t.name||t.id)+'”新建会场 '+v.id+'：地图、鬼、线索、反转、博弈参数都已复制，直接改。点“▶ 只玩这个会场”马上试；要让旅程走到它，在“会场之间的入口”接一扇门。');
}
function wpPlayVenue() {
  if(typeof WPPlay==='undefined'){wpMessage('网页试玩脚本没有载入。');return;}
  const v=wpV();if(!v)return;const p=WorldPark.parse(WorldPark.toJson(WPUI.pack));p.entryVenue=v.id;
  try{WorldPark.start(p);}catch(e){wpMessage('这个会场现在还不能开始：'+e.message+'（看右侧“结构预检”）');return;}
  WPPlay.open(WorldPark.toJson(p),{label:'正在试玩：只玩会场“'+(v.name||v.id)+'”（从它的入口开始）'});
}
// 会场体检：停笔 0.35 秒后只算当前会场（与试玩同一套内核，见 venue-check.js）。
function wpVenueCheckSoon() { clearTimeout(WPUI.chkTimer);WPUI.chkTimer=setTimeout(wpVenueCheckRender,350); }
function wpVenueCheckRender() {
  const box=wpEl('wpVenueCheck'),title=wpEl('wpVenueCheckTitle');if(!box||!title||!WPUI.pack)return;
  if(typeof WPCheck==='undefined'){box.innerHTML='<p class="hint">体检脚本没有载入。</p>';return;}
  const v=wpV();if(!v)return;let r;
  try{r=WPCheck.venue(WPUI.pack,v.id);}catch(e){box.innerHTML='<p class="hint">体检中断：'+wpEsc(e.message)+'</p>';return;}
  const c=r.counts,cls={bad:'Error',warn:'Warn',good:'Good',info:'Info'},tag={bad:'红',warn:'黄',good:'绿',info:'·'},order={bad:0,warn:1,good:2,info:3};
  title.textContent=`${v.name||v.id}：红 ${c.bad} · 黄 ${c.warn} · 绿 ${c.good}`;title.style.color=c.bad?'var(--bad)':c.warn?'var(--warn)':'var(--ok)';
  box.innerHTML=r.items.slice().sort((a,b)=>order[a.level]-order[b.level]).map(i=>`<div class="owiss ${cls[i.level]}"><b>${tag[i.level]}</b> ${wpEsc(i.text)}</div>`).join('')||'<p class="hint">没有发现问题。</p>';
}
// 写进 Unity 项目：复用 app.js 的“连接项目文件夹”（SY / syConnect，同一页面全局）；只写 Assets/Resources/WorldPark/MyWorld.worldpark.json。
const WPUNITY='Assets/Resources/WorldPark/MyWorld.worldpark.json';
function wpUnityLinked() { return typeof SY!=='undefined'&&!!SY&&!!SY.dir; }
async function wpUnityFile(create) { const assets=await SY.dir.getDirectoryHandle('Assets');const res=await assets.getDirectoryHandle('Resources',{create});const dir=await res.getDirectoryHandle('WorldPark',{create});return dir.getFileHandle('MyWorld.worldpark.json',{create}); }
function wpUnityStatus(text) { const b=wpEl('wpSyncUnity');if(b)b.textContent=text; }
async function wpUnityWrite() {
  if(!WPUI.unityOn||!wpUnityLinked())return;
  try{const json=WorldPark.toJson(WPUI.pack),f=await wpUnityFile(true),w=await f.createWritable();await w.write(json);await w.close();wpUnityStatus('🔗 已写进 '+SY.name+' ✓');}
  catch(e){WPUI.unityOn=false;wpUnityStatus('🔗 写进 Unity 项目（出错，点此重连）');wpMessage('写进 Unity 项目失败：'+e.message);}
}
function wpUnitySoon() { if(!WPUI.unityOn)return;clearTimeout(WPUI.unityTimer);WPUI.unityTimer=setTimeout(wpUnityWrite,1000); }
async function wpSyncUnity() {
  if(typeof syConnect!=='function'){wpMessage('同步模块没有载入。');return;}
  if(!wpUnityLinked()){await syConnect();if(!wpUnityLinked())return;}
  try{ // 项目里已有世界且不同：先问要不要载入（Unity 世界工坊“存给网页”的内容也能拿回来）
    const f=await wpUnityFile(false),text=await (await f.getFile()).text();
    if(text.trim()&&text!==WorldPark.toJson(WPUI.pack)){let other=null;try{other=WorldPark.parse(text);}catch(e){other=null;}
      if(other&&confirm('Unity 项目里已有世界“'+(other.name||other.id)+'”（MyWorld.worldpark.json），和网页草稿不同。\n\n确定 = 把它载入网页（网页草稿先备份在浏览器）\n取消 = 用网页草稿覆盖它')){wpBackupDraft();WPUI.pack=other;WPUI.vi=WPUI.ni=WPUI.ci=WPUI.pi=0;WPUI.unityOn=true;wpPersist();wpRender();wpUnityStatus('🔗 已连 '+SY.name+'：自动写入');wpMessage('已载入 Unity 项目里的世界；之后网页的改动会自动写回。');return;}}
  }catch(e){ /* 文件还不存在：直接写 */ }
  WPUI.unityOn=true;await wpUnityWrite();
  if(WPUI.unityOn)wpMessage('已写进 Unity 项目：'+WPUNITY+'。之后每次改动 1 秒后自动写入；Unity 世界工坊草稿没改动时会自动载入（有没保存的改动时只提示），开始页“开始世界”也会优先用它。');
}
(()=>{const on=(id,fn)=>{const el=wpEl(id);if(el)el.onclick=fn;};on('wpFromTemplate',wpFromTemplate);on('wpPlayVenue',wpPlayVenue);on('wpSyncUnity',wpSyncUnity);wpTemplateFill();wpVenueCheckRender();})();

// S249：全部作者模块加载后再切世界主入口。
document.querySelector('[data-page="worldpark"]')?.click();
