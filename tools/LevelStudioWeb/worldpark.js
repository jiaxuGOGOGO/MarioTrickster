'use strict';
// S249：WorldParkPack 的网页预检与「阶段彩排」。它不渲染物理游戏，也不替代 Unity 的运行验收。
const WorldPark = (() => {
  // S251：博弈内核与 C# BorderlandGame.cs 同语义；浏览器里由先加载的 borderland.js 提供全局对象。
  const BG = typeof BorderlandGame !== 'undefined' ? BorderlandGame : require('./borderland.js');
  const TYPE = 'mariotrickster-worldpark', VERSION = 1;
  const LIMIT = { json:262144, venues:64, nodes:256, clues:256, points:256, links:256, total:512, steps:256 };
  const blank = x => typeof x !== 'string' || !x.trim();
  const obj = x => x && typeof x === 'object' && !Array.isArray(x);
  const invalidKey = x => typeof x === 'string' && /[\/\u0000-\u001f]/.test(x);
  const venue = (p,id) => (p?.venues || []).find(v => v && v.id === id);
  const node = (v,id) => (v?.nodes || []).find(n => n && n.id === id);
  const runnable = k => ['room','story','reveal','ending','explore','game'].includes(k);
  const edges = n => !n ? [] : ['room','explore','game'].includes(n.kind) ? [n.onWin,n.onLose].filter(x=>!blank(x)) : ['story','reveal'].includes(n.kind) && !blank(n.next) ? [n.next] : [];
  const key = (v,id) => v+'/'+id;
  const known = new Set('.=Wtwf"cgin?KXOUB^Ah+*123456789MT');
  const solid = new Set('WtwfciKOUBA');
  // S252 追逐者（与 WorldParkExplore.cs 同语义）：每阶段最多4个，巡逻格最多32个，格式严格 "x,y"（每段≤6字符的十进制整数，无空格）。
  const MAX_CHASERS = 4, MAX_ROUTE = 32;
  function chaserCell(raw) {
    if (typeof raw !== 'string' || !raw) return null;
    const parts = raw.split(',');
    if (parts.length !== 2) return null;
    for (const s of parts) if (!s || s.length > 6 || !/^-?[0-9]+$/.test(s)) return null;
    return { x: parseInt(parts[0], 10), y: parseInt(parts[1], 10) };
  }

  function strings(rec, keys, where) {
    for (const k of keys) {
      if (rec[k] === undefined) rec[k] = '';
      if (typeof rec[k] !== 'string') throw Error(`${where}字段 ${k} 必须是文字`);
    }
  }
  function list(rec, k, max, where) {
    if (rec[k] === undefined) rec[k] = [];
    if (!Array.isArray(rec[k]) || rec[k].length > max) throw Error(`${where}列表 ${k} 格式错误或超过上限`);
  }
  function integer(rec, k, min, max, where) {
    if (rec[k] === undefined) rec[k] = 0;
    if (!Number.isInteger(rec[k]) || rec[k] < min || rec[k] > max) throw Error(`${where}字段 ${k} 必须是 ${min}–${max} 的整数`);
  }
  function parse(json) {
    if (typeof json !== 'string' || !json.trim() || json.length > LIMIT.json) throw Error('世界包为空或超过262144字符');
    let depth=0,containers=0,inString=false,escaped=false;
    for(const c of json){
      if(inString){if(escaped)escaped=false;else if(c==='\\')escaped=true;else if(c==='"')inString=false;}
      else if(c==='"')inString=true;
      else if(c==='{'||c==='['){if(++depth>64||++containers>2048)throw Error('世界包JSON容器层级或数量过多');}
      else if(c==='}'||c===']')depth--;
    }
    const p=JSON.parse(json.replace(/^\uFEFF/,''));
    if(!obj(p)||p.type!==TYPE||p.v!==VERSION)throw Error('需要v1的.worldpark.json世界包，不是旧关卡包');
    strings(p,['id','name','premise','entryVenue'],'根节点'); list(p,'venues',LIMIT.venues,'根节点'); list(p,'links',LIMIT.links,'根节点');
    let total=0;
    for(const v of p.venues){
      if(!obj(v))throw Error('会场必须是对象');
      strings(v,['id','name','setting','role','suit','rules','entry'],'会场'); integer(v,'difficulty',-2147483648,2147483647,'会场');
      list(v,'nodes',LIMIT.nodes,'会场'); list(v,'clues',LIMIT.clues,'会场'); total+=v.nodes.length;
      if(total>LIMIT.total)throw Error('世界包总阶段数超过512');
      for(const n of v.nodes){
        if(!obj(n))throw Error('阶段必须是对象');
        strings(n,['id','kind','name','text','room','next','onWin','onLose','result','mapText'],'阶段'); integer(n,'timeout',-2147483648,2147483647,'阶段'); list(n,'points',LIMIT.points,'阶段');
        for(const point of n.points){
          if(!obj(point))throw Error('探索点必须是对象');
          strings(point,['id','kind','label','clue','outcome'],'探索点'); integer(point,'x',-2147483648,2147483647,'探索点'); integer(point,'y',-2147483648,2147483647,'探索点'); list(point,'requires',LIMIT.clues,'探索点');
          for(const requirement of point.requires) if(typeof requirement!=='string')throw Error('探索点 requires 只能包含线索ID文字');
          if(point.kind==='chaser'||point.speed!==undefined)integer(point,'speed',-2147483648,2147483647,'探索点');
          if(point.kind==='chaser'||point.sight!==undefined)integer(point,'sight',-2147483648,2147483647,'探索点');
          if(point.kind==='chaser'||point.route!==undefined){list(point,'route',1e9,'探索点');for(const r of point.route)if(typeof r!=='string')throw Error('追逐者 route 只能包含 "x,y" 文字');}
        }
        if(n.game!==undefined){
          const g=n.game;if(!obj(g))throw Error('博弈规则 game 必须是对象');
          strings(g,['mode'],'博弈');for(const k of ['rounds','lives','ratio','goal','seed','escalate'])integer(g,k,-2147483648,2147483647,'博弈');list(g,'seats',BG.MaxParseSeats,'博弈');
          for(const seat of g.seats){if(!obj(seat))throw Error('博弈参与者必须是对象');strings(seat,['id','name','persona'],'博弈参与者');}
        }
      }
      for(const c of v.clues){
        if(!obj(c))throw Error('线索必须是对象');
        if(c.trigger===undefined)c.trigger='stage';
        strings(c,['id','name','foundAt','revealAt','hint','truth','trigger'],'线索');
      }
    }
    for(const l of p.links){if(!obj(l))throw Error('链接必须是对象');strings(l,['from','to','requires'],'链接');}
    return p;
  }

  function reachable(v,start,skip='') {
    const seen=new Set(),todo=[start];
    while(todo.length){const id=todo.shift();if(blank(id)||id===skip||seen.has(id))continue;const n=node(v,id);if(!n)continue;seen.add(id);todo.push(...edges(n));}
    return seen;
  }
  function mapShape(text) {
    const rows=[];
    for(const raw of String(text || '').replace(/\r/g,'').replace(/^\uFEFF/,'').split('\n')) {
      if(raw.startsWith('#')) continue;
      const row=raw.trimEnd().replace(/ /g,'.'); if(row.length) rows.push(row);
    }
    const w=rows[0]?.length || 0, h=rows.length;
    const at=(x,y)=>x<0||y<0||x>=w||y>=h ? 't' : rows[h-1-y][x];
    const walk=(x,y)=>!solid.has(at(x,y));
    return {rows,w,h,at,walk};
  }
  function exploreMapIssues(n,v,clues) {
    const issues=[],add=(code,text)=>issues.push({severity:'error',code,text,venue:v.id,node:n.id});
    let m=null,usable=false,spawn=[],reach=new Set();
    if(blank(n.mapText))add('explore_map_required','探索需要地图文本');
    else {
      const raw=mapShape(n.mapText).rows, first=raw[0]?.length||0;
      if(!raw.length)add('explore_map_empty','地图没有网格行');
      else {if(raw.some(r=>r.length!==first))add('explore_map_rectangular','网格每行必须同宽');if(first<5||raw.length<4||first>192||raw.length>128)add('explore_map_size','地图尺寸须在5×4至192×128');}
      const width=raw.length?Math.max(...raw.map(r=>r.length)):0,rows=raw.map(r=>r.padEnd(width,'.'));
      const at=(x,y)=>x<0||y<0||x>=width||y>=rows.length?'t':rows[rows.length-1-y][x];
      const walk=(x,y)=>x>=0&&y>=0&&x<width&&y<rows.length&&known.has(at(x,y))&&!solid.has(at(x,y));
      m={w:width,h:rows.length,at,walk};let allKnown=true;
      outer:for(let y=0;y<m.h;y++)for(let x=0;x<m.w;x++)if(!known.has(at(x,y))){add('explore_map_tile_unknown','地图有未知字符');allKnown=false;break outer;}
      let closed=m.w>0&&m.h>0;for(let x=0;x<m.w;x++)if(!solid.has(at(x,0))||!solid.has(at(x,m.h-1)))closed=false;for(let y=0;y<m.h;y++)if(!solid.has(at(0,y))||!solid.has(at(m.w-1,y)))closed=false;
      if(!closed)add('explore_map_boundary','地图外圈须封闭');for(let y=0;y<m.h;y++)for(let x=0;x<m.w;x++)if(at(x,y)==='T')spawn.push({x,y});
      if(spawn.length!==1)add('explore_spawn_unique','需要唯一T出生点');else usable=allKnown&&closed&&walk(spawn[0].x,spawn[0].y);
      if(usable){const q=[spawn[0]];for(let head=0;head<q.length;head++){const c=q[head],key=c.x+','+c.y;if(reach.has(key)||!walk(c.x,c.y))continue;reach.add(key);for(const [dx,dy]of[[1,0],[-1,0],[0,1],[0,-1]])q.push({x:c.x+dx,y:c.y+dy});}}
    }
    const ids=new Set(),cs=new Map((v.clues||[]).filter(Boolean).map(c=>[c.id,c]));let exits=false,wins=false,chasers=0;
    for(const [i,p]of(n.points||[]).entries()){
      if(!p){add('explore_point_null','空探索点');continue;}
      if(blank(p.id))add('explore_point_id_required','探索点需要id');else if(invalidKey(p.id))add('id_key_invalid','ID不能含斜杠或控制字符');else if(ids.has(p.id))add('explore_point_id_duplicate','探索点ID重复');else ids.add(p.id);
      if(!['clue','exit','chaser'].includes(p.kind))add('explore_point_kind','点类型须为clue、exit或chaser');
      if(m){if(!m.walk(p.x,p.y))add('explore_point_walkable','点须在可通行格');else if(usable&&!reach.has(p.x+','+p.y))add('explore_point_unreachable','点须从T可达');}
      if(p.kind==='clue'){const c=cs.get(p.clue);if(blank(p.clue)||!c)add('explore_point_clue_missing','线索点须引用存在的线索');else{if(c.trigger!=='interact')add('explore_point_clue_trigger','现场线索须为interact');if(c.foundAt!==n.id)add('explore_point_clue_stage','发现阶段须为当前探索');}}
      else if(p.kind==='chaser'){
        chasers++;
        if(p.speed!==0&&(p.speed<10||p.speed>80))add('explore_chaser_speed','追逐者speed：0=默认34，否则10–80（十分之一格/秒）');
        if(p.sight!==0&&(p.sight<1||p.sight>20))add('explore_chaser_sight','追逐者sight：0=默认7，否则1–20格');
        const route=p.route||[];let bad=route.length>MAX_ROUTE;
        for(const raw of route){if(bad)break;const c=chaserCell(raw);if(!c){bad=true;break;}if(!m)continue;if(!m.walk(c.x,c.y))bad=true;else if(usable&&!reach.has(c.x+','+c.y))bad=true;}
        if(bad)add('explore_chaser_route','追逐者route须是至多32个"x,y"巡逻格，且可通行、T可达');
        if(usable){const sx=p.x-spawn[0].x,sy=p.y-spawn[0].y;if(sx*sx+sy*sy<16)issues.push({severity:'warn',code:'explore_chaser_spawn_close',text:'追逐者出生格离T不到4格',venue:v.id,node:n.id});}
      }
      else if(p.kind==='exit'){exits=true;if(!['win','lose'].includes(p.outcome))add('explore_exit_outcome','出口须为win或lose');if(p.outcome==='win')wins=true;const seen=new Set();for(const req of p.requires||[]){if(blank(req)||seen.has(req)){add('explore_exit_requires_invalid','条件不能为空或重复');continue;}seen.add(req);if(!cs.has(req))add('explore_exit_requires_missing','条件引用不存在的线索');}}
    }
    if(!exits)add('explore_exit_required','至少需要一个出口');else if(!wins)issues.push({severity:'warn',code:'explore_exit_win_required',text:'没有win出口；若不是有意设计的悲剧，请补目标',venue:v.id,node:n.id});
    if(chasers>MAX_CHASERS)add('explore_chaser_limit','每个探索阶段最多4个追逐者');
    return issues;
  }
  function validate(p,roomExists) {
    const issues=[],add=(severity,code,text,v='',n='')=>issues.push({severity,code,text,venue:v||'',node:n||''});
    if(!p){add('error','pack_null','世界包不存在');return issues;}
    if(p.v!==VERSION)add('error','version_invalid','世界包版本必须是 1');
    if(blank(p.id))add('error','pack_id_required','世界包需要 id');
    const ids=new Set();
    for(const [i,v] of (p.venues||[]).entries()){
      if(!v){add('error','venue_null',`第 ${i+1} 个会场为空`);continue;}
      if(blank(v.id)){add('error','venue_id_required',`第 ${i+1} 个会场需要 id`);continue;}
      if(invalidKey(v.id))add('error','id_key_invalid','ID不能含斜杠或控制字符；显示名称不受此限制',v.id);
      if(ids.has(v.id))add('error','venue_id_duplicate','会场 id 重复：'+v.id,v.id);else ids.add(v.id);
    }
    if(blank(p.entryVenue))add('error','entry_venue_required','世界包需要 entryVenue');else if(!ids.has(p.entryVenue))add('error','entry_venue_missing','entryVenue 不存在：'+p.entryVenue);
    for(const v of p.venues||[]){if(!v)continue;
      if(v.difficulty<0)add('error','venue_difficulty_invalid','会场难度不能小于 0',v.id);
      if(blank(v.role))add('error','venue_role_required','会场需要 role',v.id);else if(!['trickster','player'].includes(v.role))add('warn','unsupported_role','角色尚待适配：'+v.role+'；草稿可以保存但不能运行此会场',v.id);
      const ns=new Map();if(!(v.nodes||[]).length)add('error','venue_nodes_required','会场至少需要一个节点',v.id);
      for(const [i,n] of (v.nodes||[]).entries()){
        if(!n){add('error','node_null',`第 ${i+1} 个节点为空`,v.id);continue;}
        if(blank(n.id)){add('error','node_id_required',`第 ${i+1} 个节点需要 id`,v.id);continue;}
        if(invalidKey(n.id))add('error','id_key_invalid','ID不能含斜杠或控制字符；显示名称不受此限制',v.id,n.id);
        if(ns.has(n.id))add('error','node_id_duplicate','节点 id 重复：'+n.id,v.id,n.id);else ns.set(n.id,n);
      }
      if(blank(v.entry))add('error','venue_entry_required','会场需要 entry 节点',v.id);else if(!ns.has(v.entry))add('error','venue_entry_missing','会场 entry 不存在：'+v.entry,v.id);
      for(const n of ns.values()){
        if(blank(n.kind)){add('error','node_kind_required','节点需要 kind',v.id,n.id);continue;}
        if(n.kind==='concept')add('warn','unsupported_concept','concept 是可保存的创作草案，需要新增玩法适配器后才能运行',v.id,n.id);
        else if(!runnable(n.kind))add('warn','unsupported_node_kind','未知玩法 kind 尚待适配：'+n.kind+'；草稿可以保存',v.id,n.id);
        if(n.kind==='room'){
          if(blank(n.room))add('error','room_required','room 节点需要 room',v.id,n.id);
          else if(!roomExists)add('warn','room_existence_unchecked','未提供房间查询，无法确认 room 是否可实际运行：'+n.room,v.id,n.id);
          else {let exists=false;try{exists=roomExists(n.room);}catch(e){}if(!exists)add('error','room_missing','当前房间适配器找不到 room：'+n.room,v.id,n.id);}
        } else if(n.kind==='ending'){
          if(blank(n.result))add('error','ending_result_required','ending 节点需要 result',v.id,n.id);else if(n.result==='any')add('error','ending_result_invalid','any 是会场链接通配条件，请用其他结局标签',v.id,n.id);
        } else if(n.kind==='explore'){
          if(!Number.isInteger(n.timeout)||n.timeout<0||n.timeout>2147483647)add('error','explore_timeout_invalid','explore timeout 必须是 0–2147483647 的秒数',v.id,n.id);
        } else if(n.kind==='game'){
          for(const i of BG.check(n.game))add(i.severity,i.code,i.text,v.id,n.id);
        }
      }
      const target=(n,k,prefix)=>{if(blank(n[k]))add('error',prefix+'_required',n.id+' 缺少 '+k,v.id,n.id);else if(!ns.has(n[k]))add('error',prefix+'_missing',n.id+' 的 '+k+' 指向不存在节点：'+n[k],v.id,n.id);};
      for(const n of ns.values())if(['story','reveal'].includes(n.kind))target(n,'next','node_next');else if(['room','explore','game'].includes(n.kind)){target(n,'onWin',n.kind+'_win');target(n,'onLose',n.kind+'_lose');}
      const seen=reachable(v,v.entry),unknown=[...seen].some(id=>!runnable(ns.get(id)?.kind));
      if(ns.size&&ns.has(v.entry)&&![...seen].some(id=>ns.get(id)?.kind==='ending')&&!unknown)add('error','ending_unreachable','从会场 entry 无法到达 ending',v.id);
      for(const n of ns.values())if(!seen.has(n.id))add('warn','node_unreachable','节点从会场 entry 不可达：'+n.id,v.id,n.id);
      const cs=new Map();for(const [i,c] of (v.clues||[]).entries()){
        if(!c){add('error','clue_null',`第 ${i+1} 条线索为空`,v.id);continue;}
        if(blank(c.id)){add('error','clue_id_required',`第 ${i+1} 条线索需要 id`,v.id);continue;}
        if(invalidKey(c.id))add('error','id_key_invalid','ID不能含斜杠或控制字符；显示名称不受此限制',v.id,c.id);
        if(cs.has(c.id))add('error','clue_id_duplicate','线索 id 重复：'+c.id,v.id,c.id);else cs.set(c.id,c);
      }
      for(const c of cs.values()){
        if(blank(c.foundAt))add('error','clue_found_required','线索需要 foundAt 来源节点',v.id,c.id);else if(!ns.has(c.foundAt))add('error','clue_found_missing','线索 foundAt 不存在：'+c.foundAt,v.id,c.id);
        if(blank(c.revealAt))add('error','clue_reveal_required','线索需要 revealAt 节点',v.id,c.id);else if(!ns.has(c.revealAt))add('error','clue_reveal_missing','线索 revealAt 不存在：'+c.revealAt,v.id,c.id);
        if(!['stage','interact'].includes(c.trigger))add('error','clue_trigger_invalid','线索 trigger 只能是 stage 或 interact',v.id,c.id);
        if(c.trigger==='interact'&&ns.has(c.foundAt)&&ns.get(c.foundAt).kind!=='explore')add('error','clue_interact_explore','interact 线索的 foundAt 必须是 explore 节点',v.id,c.id);
        if(ns.has(c.revealAt)&&ns.get(c.revealAt).kind!=='reveal')add('error','clue_reveal_kind','线索 revealAt 必须是 reveal 节点',v.id,c.id);
        if(ns.has(c.foundAt)&&ns.has(c.revealAt)&&!reachable(v,c.foundAt).has(c.revealAt))add('error','clue_reveal_before_found','线索必须先在 foundAt 找到，之后才能到 revealAt 揭示',v.id,c.id);
        if(c.foundAt===c.revealAt&&ns.has(c.foundAt))add('warn','clue_same_stage','发现和解释发生在同一阶段；若要反转，应提前提供观察事实',v.id,c.id);
        else if(reachable(v,v.entry,c.foundAt).has(c.revealAt))add('warn','clue_discovery_bypass','有路线未发现此线索就进入揭示；该线索真相不会显示。若需要必达线索，请修复分支',v.id,c.id);
      }
      for(const n of ns.values())if(n.kind==='explore')issues.push(...exploreMapIssues(n,v,cs));
    }
    const seenLinks=new Set();for(const [i,l] of (p.links||[]).entries()){
      if(!l){add('error','link_null',`第 ${i+1} 条会场连接为空`);continue;}
      if(blank(l.from))add('error','link_from_required','会场连接需要 from');else if(!ids.has(l.from))add('error','link_from_missing','连接来源会场不存在：'+l.from,l.from);
      if(blank(l.to))add('error','link_to_required','会场连接需要 to',l.from);else if(!ids.has(l.to))add('error','link_to_missing','连接目标会场不存在：'+l.to,l.from);
      if(blank(l.requires))add('error','link_requires_required','会场连接需要 requires',l.from);else if(l.requires!=='any'&&ids.has(l.from)&&!venue(p,l.from).nodes.some(n=>n?.kind==='ending'&&n.result===l.requires))add('warn','link_result_unreachable','来源会场没有结局标签：'+l.requires+'；连接暂时无法解锁',l.from);
      const linkKey=l.from+'\u001f'+l.to+'\u001f'+l.requires;if(seenLinks.has(linkKey))add('warn','link_duplicate','会场连接重复：'+l.from+' → '+l.to,l.from);else seenLinks.add(linkKey);
    }
    return issues;
  }
  function blocker(p,id) {
    const v=venue(p,id),seen=reachable(v,v?.entry);
    for(const i of validate(p)){const active=i.venue===id,global=!i.venue,isLink=i.code.startsWith('link_'),atNode=i.node&&seen.has(i.node);if(i.severity==='error'&&!isLink&&(global||(active&&(!i.node||atNode||i.code.startsWith('clue_')||i.code==='id_key_invalid'))))return i.text;if(i.severity==='warn'&&active&&i.code.startsWith('unsupported_')&&(!i.node||atNode))return i.text;}
    return '';
  }
  class Session {
    constructor(p){this.pack=p;this.venueId=p.entryVenue;this.nodeId='';this.found=new Set();this.revealed=new Set();this.venueResults=Object.create(null);this.results=this.venueResults;this.roomResults=[];this.activityResults=[];this.log=[];this.error='';this.steps=0;this.stopped=false;this.enter(this.Venue.entry);}
    get Venue(){return venue(this.pack,this.venueId);} get Current(){return node(this.Venue,this.nodeId);}
    check(){if(this.stopped)throw Error(this.error||'流程已停止');this.error='';}
    enter(id){
      if(this.steps>=LIMIT.steps){this.stopped=true;this.error='流程超过 256 步预算，已停止';throw Error(this.error);}
      const n=node(this.Venue,id);if(!n)throw Error('后续节点不存在：'+id);this.nodeId=id;this.steps++;this.log.push('进入阶段：'+this.venueId+'/'+id);
      for(const c of this.Venue.clues||[]){const clueKey=key(this.venueId,c.id);if(c.foundAt===id&&c.trigger!=='interact'&&!this.found.has(clueKey)){this.found.add(clueKey);this.log.push('找到线索：'+clueKey);}if(c.revealAt===id&&this.found.has(clueKey)&&!this.revealed.has(clueKey)){this.revealed.add(clueKey);this.log.push('揭示线索：'+clueKey);}}
      if(n.kind==='ending'){this.results[this.venueId]=n.result;this.log.push('本地结果：'+this.venueId+'='+n.result);} return n;
    }
    Continue(){this.check();if(!['story','reveal'].includes(this.Current?.kind))throw Error('当前阶段不能继续；只有 story 或 reveal 可以继续');return this.enter(this.Current.next);}
    ResolveRoom(won){this.check();if(this.Current?.kind!=='room')throw Error('当前阶段不是 room，不能结算房间结果');const fromVenue=this.venueId,fromNode=this.nodeId,result=this.enter(won?this.Current.onWin:this.Current.onLose);this.roomResults.push({venue:fromVenue,node:fromNode,won:!!won});this.log.push('房间结果：'+fromVenue+'/'+fromNode+'='+(won?'win':'lose'));return result;}
    ObserveClue(id){this.check();if(this.Current?.kind!=='explore')throw Error('当前阶段不是 explore，不能观察线索');const c=(this.Venue.clues||[]).find(x=>x&&x.id===id);if(!c||c.trigger!=='interact'||c.foundAt!==this.nodeId)throw Error('这不是当前 explore 的可交互线索：'+id);const clueKey=key(this.venueId,id);if(this.found.has(clueKey))return this.Current;this.found.add(clueKey);this.log.push('找到线索：'+clueKey);return this.Current;}
    ResolveExplore(won){this.check();if(this.Current?.kind!=='explore')throw Error('当前阶段不是 explore，不能提交活动结果');const fromVenue=this.venueId,fromNode=this.nodeId,result=this.enter(won?this.Current.onWin:this.Current.onLose);this.activityResults.push({venue:fromVenue,node:fromNode,kind:'explore',won:!!won});this.log.push('探索结果：'+fromVenue+'/'+fromNode+'='+(won?'win':'lose'));return result;}
    // S251：用逐轮选择重算整局博弈，必须恰好走完；与 C# Session.ResolveGame 同语义。
    ResolveGame(choices){this.check();const n=this.Current;if(n?.kind!=='game')throw Error('当前阶段不是 game，不能结算博弈');if(!Array.isArray(choices)||!choices.length)throw Error('博弈需要至少一轮选择');if(choices.some(c=>typeof c!=='string'||c.includes('|')))throw Error('博弈选择不能为空或包含 |');const st=BG.run(n.game,choices);if(st.error)throw Error(st.error);if(!st.finished)throw Error('博弈尚未结束，还需要第 '+(st.round+1)+' 轮的选择');const fromVenue=this.venueId,fromNode=this.nodeId,result=this.enter(st.won?n.onWin:n.onLose);this.activityResults.push({venue:fromVenue,node:fromNode,kind:'game',won:st.won});this.log.push('博弈结果：'+fromVenue+'/'+fromNode+'='+(st.won?'win':'lose'));return result;}
    CanTravel(id){return this.Current?.kind==='ending'&&this.pack.links.some(l=>l.from===this.venueId&&l.to===id&&(l.requires==='any'||l.requires===this.results[this.venueId]))&&venue(this.pack,id)&&!blocker(this.pack,id);}
    Travel(id){this.check();if(!this.CanTravel(id))throw Error(blocker(this.pack,id)||'会场未解锁或当前未结局');this.venueId=id;return this.enter(this.Venue.entry);}
  }
  function start(p){const why=blocker(p,p.entryVenue);if(why)throw Error(why);const v=venue(p,p.entryVenue);if(!v||!node(v,v.entry))throw Error('入口会场或入口节点不存在');return new Session(p);}
  return {TYPE,VERSION,LIMIT,MAX_CHASERS,MAX_ROUTE,chaserCell,parse,validate,start,venue,node,edges,reachable,blocker,mapShape,toJson:p=>JSON.stringify(p,null,2)};
})();
if (typeof module !== 'undefined') module.exports = WorldPark;
