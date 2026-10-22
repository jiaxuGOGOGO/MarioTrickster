'use strict';
// S257：驾驶舱只读当前世界；不存另一份世界数据、不推进游戏会话。
// 图标采用 Lucide/Feather 线形语汇，许可随单文件内联保留（见 cockpit.css）。
const WPCockpit = (() => {
  const icons = {
    cockpit:'<rect x="3" y="3" width="7" height="9" rx="1"/><rect x="14" y="3" width="7" height="5" rx="1"/><rect x="14" y="12" width="7" height="9" rx="1"/><rect x="3" y="16" width="7" height="5" rx="1"/>',
    world:'<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c5 5 5 13 0 18-5-5-5-13 0-18"/>',
    map:'<path d="m3 5 6-2 6 2 6-2v16l-6 2-6-2-6 2ZM9 3v16M15 5v16"/>',
    story:'<path d="M4 4h16v12H8l-4 4Z"/><path d="M8 8h8M8 12h5"/>',
    clue:'<circle cx="10" cy="10" r="6"/><path d="m15 15 6 6M10 7v6M7 10h6"/>',
    game:'<rect x="4" y="3" width="13" height="17" rx="2"/><path d="m11 7 3 4-3 4-3-4ZM18 7l3 1-2 13-4-1"/>',
    reveal:'<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
    ending:'<path d="M5 21V3l14 3-14 4M3 21h4"/>',
    link:'<path d="M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-2 2M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l2-2"/>',
    check:'<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6Z"/><path d="m8 12 3 3 5-6"/>',
    play:'<path d="m7 4 14 8-14 8Z"/>',
    room:'<path d="M3 21h18M5 21V3h14v18M9 21V8h6v13M12 14h1"/>',
    monitor:'<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M12 17v4M8 21h8"/>',
    save:'<path d="M4 3h14l3 3v15H3V3ZM7 3v6h10V3M7 21v-8h10v8"/>',
    help:'<circle cx="12" cy="12" r="9"/><path d="M9 9a3 3 0 1 1 4 3c-1 1-1 1-1 3M12 18h.01"/>',
    back:'<path d="m9 5-7 7 7 7M2 12h20"/>',
    brush:'<path d="m14 4 6-2 2 2-9 9-4-4ZM9 13c-5-1-2 6-7 7 8 2 10-1 7-7"/>',
    point:'<path d="M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/>',
    arrow:'<path d="M3 12h18m-6-6 6 6-6 6"/>',
    concept:'<path d="M9 18h6M9 21h6M8 15c-6-7 2-15 8-9 4 4-1 7-1 9"/>'
  };
  const short = {story:'入场 / 剧情', explore:'现场探索', game:'多人博弈', room:'横版挑战', reveal:'真相反转', ending:'结局', concept:'玩法草稿'};
  const kindIcon = k => ({explore:'map',room:'room'}[k] || k);
  function edges(v) {
    return (v.nodes || []).flatMap(n => ['explore','game','room'].includes(n.kind)
      ? [{from:n.id,to:n.onWin,label:'成功'},{from:n.id,to:n.onLose,label:'失败'}].filter(e=>e.to)
      : ['ending','concept'].includes(n.kind) ? [] : n.next ? [{from:n.id,to:n.next,label:'继续'}] : []);
  }
  function destinations(pack,v) {
    return (pack.links || []).filter(l=>l.from===v.id).map(l=>({to:l.to,name:(pack.venues.find(x=>x.id===l.to)||{}).name||'未找到会场',requires:l.requires}));
  }
  function resolve(pack,venueId,nodeId) {
    const vi=pack.venues.findIndex(v=>v.id===venueId); if(vi<0)return null;
    const ni=nodeId?pack.venues[vi].nodes.findIndex(n=>n.id===nodeId):-1;
    if(nodeId&&ni<0)return null;return {vi,ni:ni<0?0:ni};
  }
  const icon = name => `<svg class="wc-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[name]||icons.help}</svg>`;
  return {edges,destinations,resolve,icon,short,kindIcon};
})();
if (typeof module !== 'undefined' && module.exports) module.exports = WPCockpit;

// 所有 UI 回调留在浏览器内；纯模型可以单独被 Node 测试。
if (typeof document !== 'undefined' && typeof WPUI !== 'undefined') {
  const WC = {timer:0,roomContext:null,mapContext:null};
  const wcButton=(action,ic,title,sub='',attrs='')=>`<button type="button" class="wc-action" data-wc="${action}" ${attrs}>${WPCockpit.icon(ic)}<span><b>${wpEsc(title)}</b>${sub?`<small>${wpEsc(sub)}</small>`:''}</span>${WPCockpit.icon('arrow')}</button>`;
  const wcBadge = v => (v.name||'').match(/^[♠♥♦♣]\s*\d+|^K\b/)?.[0] || ((v.suit||'').match(/[♠♥♦♣]/)?.[0]||'◇')+(v.difficulty||'');
  function wcPage(name) { document.querySelector(`[data-page="${name}"]`)?.click(); }
  function wcJump(id,nodeId,focusMap=false) {
    if(nodeId){const ni=wpV().nodes.findIndex(n=>n.id===nodeId);if(ni>=0){WPUI.ni=ni;WPUI.pi=0;}}
    wpRender();wcPage('worldpark');
    const page=wpEl('pageWorldPark');if(focusMap)page.classList.add('wp-map-focus');
    const el=wpEl(id);if(el){
      // 点位布局会重排网格：布局完成后再量位置，不能沿用重排前的平滑滚动目标。
      if(focusMap){const top=page.scrollTop+el.getBoundingClientRect().top-page.getBoundingClientRect().top-(wpEl('wpSafety')?.offsetHeight||0)-12;page.scrollTo({top:Math.max(0,top),behavior:'instant'});}
      else el.scrollIntoView({behavior:'smooth',block:'start'});
      el.classList.add('wp-flash');setTimeout(()=>el.classList.remove('wp-flash'),1400);
    }
  }
  function wcExplore() { const n=wpN();return n?.kind==='explore'?n:wpV().nodes.find(n=>n.kind==='explore'); }
  function wcSelect(vi) { WPUI.vi=vi;WPUI.ni=WPUI.ci=WPUI.pi=0;wpRender();wpCockpitRender(); }
  function wpCockpitSoon() { clearTimeout(WC.timer);WC.timer=setTimeout(wpCockpitRender,180); }
  function wpCockpitRender() {
    const host=wpEl('wcHost');if(!host||!WPUI.pack)return;
    const p=WPUI.pack,v=wpV(),n=wpN(),explore=wcExplore(),errs=WPUI.errCount||0,ni=v.nodes.findIndex(x=>x.kind==='game');
    const linkCount=p.links.length,selected=(k)=>WPUI.vi===k?'true':'false';
    host.innerHTML=`<div class="wc-layout"><aside class="wc-rail"><span class="wc-eyebrow">当前世界</span><h2>${wpEsc(p.name)}</h2><p class="wc-muted">${p.venues.length} 个会场 · ${linkCount} 扇门</p><div class="wc-venues">${p.venues.map((x,i)=>`<button class="wc-venue" aria-current="${selected(i)}" data-wc-venue="${i}" title="${x.nodes.length} 个阶段"><span class="wc-suit">${wpEsc(wcBadge(x))}</span><span><b>${wpEsc(x.name.replace(/^[♠♥♦♣]\s*\d+\s*|^K\s+/,''))}</b>${x.id===p.entryVenue?'<small>旅程起点</small>':''}</span></button>`).join('')}</div><div class="wc-rail-bottom">${wcButton('templates','game','做新会场','从现成模板改起')}${wcButton('world','world','世界工坊','完整编辑，全部功能还在')}</div></aside>
      <div class="wc-main"><div class="wc-title"><div><h2>${wpEsc(v.name)}</h2></div><button class="btn gold wc-play" data-wc="play">${WPCockpit.icon('play')}只玩这个会场</button></div>
      <div class="wc-workbench"><div class="wc-mapcard"><div class="wc-sectionhead"><b>${explore?'现场地图':'这一场在博弈桌上进行'}</b><span class="wc-muted">${explore?`${WorldParkSpatial.mapShape(explore.mapText).width} × ${WorldParkSpatial.mapShape(explore.mapText).height} 格`:'不需要探索地图'}</span></div>${explore?'<canvas id="wcMapThumb" aria-label="当前会场探索地图预览"></canvas>':`<div class="wc-table-preview">${WPCockpit.icon(ni>=0?'game':'story')}<b>${ni>=0?WPCockpit.short.game:'剧情 / 结局'}</b></div>`}<div class="wc-mini-legend">${explore?'● 出生　◇ 调查　▣ 出口　▲ 鬼':'先写公开规则，再做选择，最后揭示'}</div></div>
      <div class="wc-actions"><span class="wc-eyebrow">你想改什么？</span>${wcButton('map','brush','画地形 / 改大小',explore?'只改这一场，能撤销':'本会场没有探索阶段',explore?'':'disabled')}${wcButton('points','point','摆线索 / 门 / 鬼',explore?'点图放置；鬼的路线也在图上点':'本会场没有探索阶段',explore?'':'disabled')}${wcButton('rules','story','改规则 / 剧情','直接到当前会场')}${wcButton('game','game','调博弈参数',ni>=0?'选玩法、轮数、参加者':'本会场没有博弈阶段',ni>=0?'':'disabled')}${wcButton('clues','reveal','做伏笔 / 反转','当时以为 → 其实')}</div></div>
      <div class="wc-sectionhead"><h3>这个会场怎样运转</h3><span class="wc-muted">点图标，就到那一阶段 · 箭头来自实际分支</span></div>
      <div class="wc-stageflow">${v.nodes.map(x=>`<div class="wc-stage"><button data-wc-node="${wpEsc(x.id)}" aria-current="${x.id===n?.id}">${WPCockpit.icon(WPCockpit.kindIcon(x.kind))}<span class="wc-muted">${wpEsc(WPCockpit.short[x.kind]||'阶段')}</span><b>${wpEsc(x.name||x.id)}</b>${x.id===v.entry?'<small class="wc-entry">入口</small>':''}</button><div class="wc-branches">${WPCockpit.edges(v).filter(e=>e.from===x.id).map(e=>`<button data-wc-node="${wpEsc(e.to)}">${wpEsc(e.label)} → ${wpEsc(v.nodes.find(t=>t.id===e.to)?.name||'未指定阶段')}</button>`).join('')}${x.kind==='ending'?`<span class="wc-result">结局：${wpEsc(x.result)}</span>`:''}</div></div>`).join('')}</div>
      <div class="wc-bottom"><div class="wc-health ${errs?'bad':''}">${WPCockpit.icon('check')}<div><b>${errs?`${errs} 处必须修`:'结构预检通过'}</b><small>${WPUI.chk?.venue===v.id?`当前会场体检：红 ${WPUI.chk.bad} · 黄 ${WPUI.chk.warn}`:'体检计算中'} · 预检不是实机验收</small></div><button class="btn small" data-wc="check">去体检</button><button class="btn small" data-wc="undo" ${WPUI.hist?.canUndo?'':'disabled'}>↶ 撤销</button></div><div class="wc-health">${WPCockpit.icon('save')}<div><b>${WPUI.unityOn?'自动写入 Unity 已开启':'草稿在这个浏览器里'}</b><small>${WPUI.unityOn?'改动约 1 秒后写入「我的世界」':'换电脑前导出；打开地图不改世界'}</small></div><button class="btn small" data-wc="export">导出世界</button></div></div>
      <div class="wc-sectionhead"><h3>会场之间的门</h3><button class="btn small" data-wc="links">接门 / 改条件</button></div><div class="wc-travel">${WPCockpit.destinations(p,v).map(l=>`<button data-wc-travel="${wpEsc(l.to)}">${WPCockpit.icon('link')}<span>${wpEsc(l.name)}<small>${l.requires==='any'?'任意结局':'需要结局：'+wpEsc(l.requires)}</small></span>${WPCockpit.icon('arrow')}</button>`).join('')||'<span class="wc-muted">这里是终点，或还没接下一场的门。</span>'}</div>
      <details class="wc-system" open><summary>项目全景：子创作如何接回世界</summary><div class="wc-systemflow"><div class="wc-systemtools">${wcButton('map','map','会场地图','地形 + 范围',explore?'':'disabled')}${wcButton('rooms','room','横版房间','只由横版挑战阶段引用')}${wcButton('town','map','旧小镇工具','独立小镇；不会自动进世界')}</div><div class="wc-systemarrow">→<small>应用 / 引用</small></div><div class="wc-systemcore">${WPCockpit.icon('world')}<b>世界 → 会场 → 阶段</b><small>一份世界包</small><button class="btn small" data-wc="world">进入世界工坊</button></div><div class="wc-systemarrow">→<small>读取同一规则</small></div><div class="wc-systemtools">${wcButton('playworld','play','网页试玩','快速验证体验')}${wcButton('unity','monitor','Windows 正式游戏','送到 Unity，构建电脑端')}${wcButton('check','check','会场体检','结构与体验风险分开看')}</div></div><div class="wc-systemfoot">地图应用时才改变当前阶段；房间另存关卡包，世界包只记房间名。浏览器不能直接构建 Windows 游戏。</div></details></div></div>`;
    host.querySelectorAll('[data-wc]').forEach(b=>b.onclick=()=>wcAction(b.dataset.wc));
    host.querySelectorAll('[data-wc-venue]').forEach(b=>b.onclick=()=>wcSelect(Number(b.dataset.wcVenue)));
    host.querySelectorAll('[data-wc-node]').forEach(b=>b.onclick=()=>{if(v.nodes.some(n=>n.id===b.dataset.wcNode))wcJump('wpNodeFields',b.dataset.wcNode);else wpToast('这个分支指向的阶段不存在，请在结构预检里修复。');});
    host.querySelectorAll('[data-wc-travel]').forEach(b=>b.onclick=()=>{const i=p.venues.findIndex(v=>v.id===b.dataset.wcTravel);if(i>=0)wcSelect(i);else wpToast('这个目的会场已不存在，请修复门的链接。');});
    if(explore)wcThumb(explore);wcDockRender();
  }
  function wcThumb(n) {
    const c=wpEl('wcMapThumb');if(!c)return;const s=WorldParkSpatial.mapShape(n.mapText),z=Math.max(2,Math.min(18,Math.floor(520/Math.max(1,s.width))));c.width=Math.min(192,s.width)*z;c.height=Math.min(128,s.height)*z;
    const g=c.getContext('2d');g.imageSmoothingEnabled=false;
    for(let y=0;y<Math.min(128,s.height);y++)for(let x=0;x<Math.min(192,s.width);x++){const t=s.at(x,y);g.fillStyle=typeof owTile==='function'&&owTile(t)?owCss(owTile(t)):t==='T'?'#4f8cff':'#30263e';g.fillRect(x*z,(s.height-1-y)*z,z,z);}
    for(const p of n.points||[]){const x=(p.x+.5)*z,y=(s.height-p.y-.5)*z,r=Math.max(2,z*.42);g.fillStyle=p.kind==='exit'?'#ffc83d':p.kind==='chaser'?'#ff5a4e':'#bcb0ff';g.beginPath();if(p.kind==='exit')g.rect(x-r,y-r,r*2,r*2);else if(p.kind==='chaser'){g.moveTo(x,y-r);g.lineTo(x+r,y+r);g.lineTo(x-r,y+r);}else{g.moveTo(x,y-r);g.lineTo(x+r,y);g.lineTo(x,y+r);g.lineTo(x-r,y);}g.closePath();g.fill();}
    c.onclick=wpOpenMapEditor;c.title='点地图：画当前会场地形 / 改大小';
  }
  function wcDockRender() {
    const el=wpEl('wcDockContext');if(el&&WPUI.pack)el.textContent=`${wpV().name} / ${wpN()?.name||'未选阶段'}`;
    const back=wpEl('wcRoomReturn');if(back)back.hidden=!WC.roomContext;
  }
  function wcAction(a) {
    const v=wpV(),ex=wcExplore();
    if(a==='map'){if(ex)wpOpenMapEditor(ex.id);else wpToast('这一场没有探索地图。请选择含探索阶段的会场。');}
    else if(a==='points'){if(ex)wcJump('wpSpatialHost',ex.id,true);else wpToast('这一场没有探索点位。');}
    else if(a==='play'){wcPage('worldpark');wpPlayVenue();}
    else if(a==='playworld')wpPlayCurrent();
    else if(a==='rules')wcJump('wpVenueFields');
    else if(a==='clues')wcJump('wpClueFields');
    else if(a==='game'){const n=v.nodes.find(n=>n.kind==='game');if(n)wcJump('wpNodeFields',n.id);}
    else if(a==='check')wcJump(WPUI.errCount?'wpIssues':'wpVenueCheck');
    else if(a==='links')wcJump('wpLinks');
    else if(a==='templates')wcJump('wpTemplate');
    else if(a==='world'){wpEl('pageWorldPark').classList.remove('wp-map-focus');wcPage('worldpark');}
    else if(a==='rooms'){const n=v.nodes.find(n=>n.kind==='room');if(n){WPUI.ni=v.nodes.indexOf(n);wpRender();wpOpenRoomChild();}else {wcPage('design');wpToast('这是独立的房间子工具；当前会场未引用横版房间。世界工坊添加“横版挑战”阶段后按房间名称接回。');}}
    else if(a==='town'){wcPage('overworld');wpToast('小镇是独立素材工具，不会自动改变会场。回世界工坊用“高级：整张替换地图”才会影响当前地图。');}
    else if(a==='unity')wpUnityHelp();
    else if(a==='export')wpEl('wpExport').click();
    else if(a==='undo')wpUndo();
  }
  function wpOpenMapEditor(nodeId) {
    if(typeof WPMap==='undefined'){wpToast('地图编辑模块未载入，原地图没有改变。');return;}
    const ex=typeof nodeId==='string'?wpV().nodes.find(n=>n.id===nodeId):wcExplore();if(!ex||ex.kind!=='explore'){wpToast('请先选择一个现场探索阶段。');return;}
    const shape=WorldParkSpatial.mapShape(ex.mapText);if(shape.width>192||shape.height>128){wpToast('地图超出了可编辑范围（最大 192×128），请先在高级地图文本或原版恢复中修正。','去修',()=>wcJump('wpExploreTools',ex.id));return;}
    WPUI.ni=wpV().nodes.indexOf(ex);wpRender();
    const d=wpEl('wcMapDialog'),host=wpEl('wcMapEditor'),venueId=wpV().id,id=ex.id,before=JSON.stringify(ex);
    WC.mapContext={venueId,id,before};wpEl('wcMapTitle').textContent=`画地图：${wpV().name} / ${ex.name}`;
    const editor=new WPMap.Editor();
    editor.mount(host,{node:ex,onCancel:()=>{WC.mapContext=null;d.close();},onApply:candidate=>{
      const at=WPCockpit.resolve(WPUI.pack,venueId,id);if(!at){wpToast('原来的会场或阶段已不存在，未应用地图。');return false;}
      const current=WPUI.pack.venues[at.vi].nodes[at.ni];if(JSON.stringify(current)!==before){wpToast('这个阶段在打开地图后又被改过，为避免覆盖，请关闭后重开；原世界未改。');return false;}
      const check=WPMap.validate(candidate);if(!check.ok){wpToast(check.reason);return false;}
      WPUI.vi=at.vi;WPUI.ni=at.ni;WPUI.pack.venues[at.vi].nodes[at.ni]=candidate;
      wpPersist(false,`图上编辑地图：${wpV().name} · ${candidate.name}`);WC.mapContext=null;d.close();wpRender();wpCockpitRender();wpToast('地图已应用回这个探索阶段。规则、线索文字和其他会场没有改。','↶ 撤销',wpUndo);return true;
    }});d.showModal();
  }
  function wpRoomTools() {
    const n=wpN();if(!n||n.kind!=='room')return '';
    const names=typeof owRoomNames==='function'?owRoomNames():[];
    return `<div class="wc-room-tools"><label class="f">从关卡库选房间<select class="f" id="wcRoomSelect">${[...new Set([n.room,...names])].filter(Boolean).map(name=>`<option ${name===n.room?'selected':''}>${wpEsc(name)}</option>`).join('')}</select></label><button class="btn" id="wcRoomOpen">${WPCockpit.icon('room')}打开这个房间子创作</button><p class="hint">只按名称引用；房间地图另存关卡包。网页试玩不运行横版物理，正式体验请在 Unity 测。</p></div>`;
  }
  function wpRoomBind() {
    const sel=wpEl('wcRoomSelect');if(sel)sel.onchange=()=>{wpN().room=sel.value;wpPersist(false,'更换横版房间引用');wpRender();};
    const b=wpEl('wcRoomOpen');if(b)b.onclick=wpOpenRoomChild;
  }
  function wpOpenRoomChild() {
    const n=wpN();if(!n||n.kind!=='room'||!n.room){wpToast('先从关卡库选择一个房间。');return;}
    if(typeof owRoomGrid!=='function'||!owRoomGrid(n.room)){wpToast('找不到这个房间，请先在子创作里创建或导入。');return;}
    WC.roomContext={venue:wpV().id,node:n.id,room:n.room};owEditRoom(n.room);wcDockRender();
    const refs=WPUI.pack.venues.flatMap(v=>v.nodes.filter(x=>x.kind==='room'&&x.room===n.room)).length;
    wpEl('wcChildNote').textContent=`正在改「${n.room}」：世界中 ${refs} 个阶段按此名称引用；改完点“应用房间引用并返回”。房间地图请另存关卡包。`;wpEl('wcChildNote').hidden=false;
  }
  function wpReturnRoomChild() {
    const ctx=WC.roomContext;if(!ctx)return;
    const at=WPCockpit.resolve(WPUI.pack,ctx.venue,ctx.node);if(!at){wpToast('原阶段已不存在，房间仍保留在关卡库，未修改世界。');return;}
    const target=WPUI.pack.venues[at.vi].nodes[at.ni];if(target.kind!=='room'||target.room!==ctx.room){wpToast('原阶段的房间引用已经改变，为防覆盖，未应用；房间仍在关卡库。');return;}
    storeCurrent();save();WPUI.vi=at.vi;WPUI.ni=at.ni;target.room=S.name;wpPersist(false,'从子创作应用房间引用：'+S.name);
    WC.roomContext=null;wpEl('wcChildNote').hidden=true;wcJump('wpNodeFields');wpCockpitRender();wpToast('已返回。世界包记住房间名；改过的房间需另存关卡包或连接 Unity 同步。');
  }
  function wpUnityHelp() {
    const d=wpEl('wcHelpDialog');wpEl('wcHelpBody').innerHTML=`<h2>送到 Windows 正式游戏</h2><div class="wc-helpsteps"><span>${WPCockpit.icon('save')}<b>1　保存世界</b><button class="btn" id="wcHelpSync">写进 Unity 项目</button><small>Chrome / Edge 选项目文件夹；不支持时导出世界包，在 Unity 导入。</small></span><span>${WPCockpit.icon('monitor')}<b>2　Unity 亲自试玩</b><small>打开项目 → 世界工坊 → 开始世界。需先激活个人许可证。</small></span><span>${WPCockpit.icon('play')}<b>3　构建电脑游戏</b><small>Unity 世界工坊 → 构建 Windows 版；或 Unity 关闭时双击 BUILD_WINDOWS_GAME.cmd。</small></span></div><p class="hint">网页不能替你激活许可证或直接构建 exe。Unity 实机、原生测试和 Windows 构建目前未验证。</p><button class="btn" id="wcHelpClose">知道了，返回驾驶舱</button>`;
    wpEl('wcHelpSync').onclick=async()=>{d.close();wcPage('worldpark');await wpSyncUnity();wpCockpitSoon();};wpEl('wcHelpClose').onclick=()=>d.close();d.showModal();
  }
  // 提供给现有页面调用，不替换旧入口与安全网。
  Object.assign(globalThis,{wpCockpitSoon,wpCockpitRender,wpOpenMapEditor,wpRoomTools,wpRoomBind,wpReturnRoomChild});
  document.querySelectorAll('[data-wc-dock]').forEach(b=>b.onclick=()=>{const a=b.dataset.wcDock;if(a==='cockpit'){wcPage('cockpit');wpCockpitRender();}else if(a==='return')wpReturnRoomChild();else wcAction(a);});
  const d=wpEl('wcMapDialog');if(d)d.addEventListener('cancel',()=>{WC.mapContext=null;});
  const tab=document.querySelector('[data-page="cockpit"]');if(tab){const prev=tab.onclick;tab.onclick=e=>{prev?.(e);wpCockpitRender();};}
  const navIcons={cockpit:'cockpit',design:'room',overworld:'map',worldpark:'world',mech:'concept',handoff:'story',map:'help'};
  document.querySelectorAll('.tab[data-page]').forEach(b=>{const label=b.dataset.page==='cockpit'?'驾驶舱':b.textContent;b.innerHTML=WPCockpit.icon(navIcons[b.dataset.page])+wpEsc(label);});
  // 审计修正：放大布局只服务“摆点位”这一次跳转，点任何页签都先恢复普通布局。
  document.querySelectorAll('.tab[data-page]').forEach(b=>b.addEventListener('click',()=>wpEl('pageWorldPark')?.classList.remove('wp-map-focus')));
  const dockIcons={cockpit:'cockpit',map:'brush',world:'world',check:'check',export:'save',return:'back'};
  document.querySelectorAll('[data-wc-dock]').forEach(b=>{b.innerHTML=WPCockpit.icon(dockIcons[b.dataset.wcDock])+wpEsc(b.textContent.replace(/^[◫✎◇✓↓↶]\s*/,''));});
  wpCockpitRender();if(!/^#play\b/.test(location.hash)){wcPage('cockpit');}
}
