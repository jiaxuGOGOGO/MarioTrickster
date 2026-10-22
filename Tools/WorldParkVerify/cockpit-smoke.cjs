// S257 驾驶舱真实浏览器验收：无界面 Edge / Chrome、独立临时配置（不碰作者真实草稿与 Unity 文件），真实点击与鼠标拖画。
// 调试端口只监听 127.0.0.1（随机端口）。找不到浏览器或连不上调试端口时输出“驾驶舱浏览器: SKIP（原因）”并以 0 退出，run_all 记为 SKIP。
// 用法：node Tools/WorldParkVerify/cockpit-smoke.cjs        截图在 .work/worldpark-verify/cockpit/
const fs = require('fs'), path = require('path'), cp = require('child_process'), os = require('os');
const repo = path.join(__dirname, '..', '..');
const outDir = path.join(repo, '.work', 'worldpark-verify', 'cockpit');
const page = path.join(repo, 'Tools', 'LevelStudioWeb', 'index.html');
const exe = [process.env.WEB_SMOKE_BROWSER, 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe', 'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe', 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].find(p => p && fs.existsSync(p));
if (!exe) { console.log('驾驶舱浏览器: SKIP（没有找到 Edge / Chrome）'); process.exit(0); }
if (!fs.existsSync(page)) { console.log('驾驶舱浏览器: FAIL（缺少 index.html，先运行 build.py）'); process.exit(1); }
fs.mkdirSync(outDir, { recursive: true });
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'wpp-smoke-'));
const sleep = ms => new Promise(r => setTimeout(r, ms));
const proc = cp.spawn(exe, ['--headless=new', '--remote-debugging-port=0', '--remote-debugging-address=127.0.0.1', '--remote-allow-origins=*', '--no-sandbox', '--in-process-gpu', '--no-first-run', '--no-default-browser-check', '--disable-gpu', '--disable-extensions', '--disable-background-networking', '--disable-sync', '--mute-audio', '--autoplay-policy=no-user-gesture-required', '--user-data-dir=' + profile, '--window-size=1280,800', 'about:blank'], { stdio: 'ignore' });
// --no-sandbox / --in-process-gpu：受限账户（如 AI 的命令沙箱）里浏览器子进程会被拒绝访问而崩溃；这里只打开本仓库自己的页面。
// 调试端口只监听 127.0.0.1；端口号由浏览器写进 DevToolsActivePort（Windows 沙箱里管道句柄传不过去，所以不用 --remote-debugging-pipe）
let ws = null, seq = 0; const waiting = new Map(), errors = [];
const wr = { write: s => ws.send(s.replace(/\0$/, '')) };
const envError = m => { const e = Error(m); e.env = true; return e; };
async function connect() {
  const portFile = path.join(profile, 'DevToolsActivePort');
  for (let i = 0; i < 100 && !fs.existsSync(portFile); i++) await sleep(100);
  if (!fs.existsSync(portFile)) throw envError('浏览器没有打开调试端口');
  const [port, wsPath] = fs.readFileSync(portFile, 'utf8').trim().split(/\r?\n/);
  let target = null;
  for (let i = 0; i < 50 && !target; i++) { try { const list = await (await fetch('http://127.0.0.1:' + port + '/json/list')).json(); target = list.find(t => t.type === 'page' && t.webSocketDebuggerUrl); } catch (e) { } if (!target) await sleep(200); }
  ws = new WebSocket(target ? target.webSocketDebuggerUrl : 'ws://127.0.0.1:' + port + wsPath);
  await new Promise((res, rej) => { const t = setTimeout(() => rej(envError('连接浏览器调试端口超时（' + port + '）')), 8000); ws.onopen = () => { clearTimeout(t); res(); }; ws.onerror = () => { clearTimeout(t); rej(envError('连不上浏览器调试端口（受限环境可能不放行本机回环连接）')); }; });
  ws.onmessage = ev => onMsg(JSON.parse(typeof ev.data === 'string' ? ev.data : Buffer.from(ev.data).toString('utf8')));
}
function onMsg(msg) {
  {
    if (msg.id && waiting.has(msg.id)) { const w = waiting.get(msg.id); waiting.delete(msg.id); msg.error ? w.reject(Error(msg.error.message)) : w.resolve(msg.result); }
    else if (msg.method === 'Runtime.exceptionThrown') errors.push('页面异常：' + (msg.params.exceptionDetails.exception ? msg.params.exceptionDetails.exception.description : msg.params.exceptionDetails.text));
    else if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') errors.push('console.error：' + msg.params.args.map(a => a.value || a.description || '').join(' '));
  }
}
let sid = null;
const send = (method, params, useSession = true) => new Promise((resolve, reject) => { const id = ++seq; const t = setTimeout(() => { waiting.delete(id); reject(Error(method + ' 超时')); }, 20000); waiting.set(id, { resolve: v => { clearTimeout(t); resolve(v); }, reject: e => { clearTimeout(t); reject(e); } }); wr.write(JSON.stringify(useSession && sid ? { id, method, params: params || {}, sessionId: sid } : { id, method, params: params || {} }) + '\0'); });
const evaluate = async expr => { const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true }); if (r.exceptionDetails) throw Error('evaluate 失败：' + (r.exceptionDetails.exception ? r.exceptionDetails.exception.description : r.exceptionDetails.text)); return r.result.value; };
const KEYS = { Space: [' ', 32], KeyE: ['e', 69], KeyD: ['d', 68], KeyW: ['w', 87], KeyA: ['a', 65], KeyS: ['s', 83], Escape: ['Escape', 27], Enter: ['Enter', 13] };
const key = async (code, holdMs) => { const [k, vk] = KEYS[code]; await send('Input.dispatchKeyEvent', { type: 'keyDown', code, key: k, windowsVirtualKeyCode: vk, text: k.length === 1 ? k : undefined }); if (holdMs) await sleep(holdMs); await send('Input.dispatchKeyEvent', { type: 'keyUp', code, key: k, windowsVirtualKeyCode: vk }); };
const shots = []; let shotN = 0;
const shot = async name => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(outDir, String(++shotN).padStart(2, '0') + '_' + name + '.png'); fs.writeFileSync(f, Buffer.from(r.data, 'base64')); shots.push(path.basename(f)); };
// S257：独立临时浏览器配置，不碰作者真实草稿或 Unity 文件。
const keepAlive=setInterval(()=>{},1000);process.on('exit',()=>{try{proc.kill();}catch(e){}});
(async()=>{
 const results=[];const must=(v,name)=>{if(!v)throw Error(name);results.push(name);};
 const click=async selector=>{must(await evaluate(`(()=>{const b=document.querySelector(${JSON.stringify(selector)});if(!b||b.disabled)return false;b.click();return true;})()`),'点击 '+selector);await sleep(180);};
 const shotNamed=async name=>{const r=await send('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(outDir,name+'.png'),Buffer.from(r.data,'base64'));};
 const paintRoad=async()=>{
   await click('[data-wpm-brush="="]');
   const p=await evaluate("(()=>{const s=WorldParkSpatial.mapShape(wpN().mapText),c=document.querySelector('[data-wpm-canvas]'),b=c.getBoundingClientRect();for(let y=s.height-2;y>0;y--)for(let x=1;x<s.width-2;x++)if(s.at(x,y)==='.'&&s.at(x+1,y)==='.'&&!(wpN().points||[]).some(p=>p.y===y&&(p.x===x||p.x===x+1)))return {x:b.left+(x+.5)*b.width/s.width,y:b.top+(s.height-y-.5)*b.height/s.height,x2:b.left+(x+1.5)*b.width/s.width,gx:x,gy:y};return null;})()");must(!!p,'找到可绘制的实际地面');
   await send('Input.dispatchMouseEvent',{type:'mouseMoved',x:p.x,y:p.y});await send('Input.dispatchMouseEvent',{type:'mousePressed',x:p.x,y:p.y,button:'left',buttons:1,clickCount:1});await send('Input.dispatchMouseEvent',{type:'mouseMoved',x:p.x2,y:p.y,button:'left',buttons:1});await send('Input.dispatchMouseEvent',{type:'mouseReleased',x:p.x2,y:p.y,button:'left',buttons:0,clickCount:1});await sleep(120);return p;
 };
 try{
  await connect();await send('Page.enable');await send('Runtime.enable');await send('Emulation.setDeviceMetricsOverride',{width:1280,height:900,deviceScaleFactor:1,mobile:false});
  await send('Page.navigate',{url:'file:///'+page.replace(/\\/g,'/')});
  for(let i=0;i<60;i++){await sleep(150);if(await evaluate("typeof WPCockpit!=='undefined'&&document.getElementById('wcHost').children.length>0").catch(()=>false))break;}
  must(await evaluate("document.getElementById('pageCockpit').classList.contains('on')"),'默认进入驾驶舱');
  must(await evaluate("document.querySelectorAll('[data-wc-venue]').length===6"),'六会场都可导航');
  const original=await evaluate('WorldPark.toJson(WPUI.pack)');
  await sleep(400);await shotNamed('驾驶舱');
  await evaluate("document.querySelector('.wc-system').scrollIntoView({block:'end'});true");await sleep(100);await shotNamed('项目联动图');await evaluate("document.getElementById('pageCockpit').scrollTop=0;true");
  await click('[data-wc="rules"]');must(await evaluate("document.getElementById('pageWorldPark').classList.contains('on')"),'规则入口到世界工坊');
  await click('[data-wc-dock="cockpit"]');await click('[data-wc="map"]');
  must(await evaluate("document.getElementById('wcMapDialog').open"),'打开当前会场地图子工具');
  must(await evaluate("document.getElementById('wcMapTitle').textContent.includes('三扇门')"),'地图明确标出编辑对象');
  await shotNamed('地图编辑');
  // 检查真实可操作的画布与控件；下一步通过 PointerEvent 在空地画道路。
  const controls=await evaluate("(()=>{const h=document.getElementById('wcMapEditor');return {buttons:[...h.querySelectorAll('button')].map(b=>({text:b.textContent,attr:[...b.attributes].filter(a=>a.name.startsWith('data-')).map(a=>[a.name,a.value])})),inputs:[...h.querySelectorAll('input')].map(e=>({type:e.type,value:e.value,attr:[...e.attributes].filter(a=>a.name.startsWith('data-')).map(a=>[a.name,a.value])})),canvases:h.querySelectorAll('canvas').length};})()");
  fs.writeFileSync(path.join(outDir,'地图控件.json'),JSON.stringify(controls,null,2));must(controls.canvases>0,'大画布存在');
  await paintRoad();must(await evaluate("!document.querySelector('[data-wpm-undo]').disabled"),'真实鼠标拖绘产生临时改动');await click('[data-wpm-undo]');must(await evaluate("!document.querySelector('[data-wpm-redo]').disabled"),'地图内撤销可重做');await click('[data-wpm-redo]');must(await evaluate('WorldPark.toJson(WPUI.pack)')===original,'画图与临时撤销不提前写世界');
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await sleep(120);
  must(await evaluate('WorldPark.toJson(WPUI.pack)')===original,'取消地图不改世界');
  // 用生产地图变换验证应用入口，真实绘制与尺寸控制由后续地图控件步骤执行。
  await click('[data-wc="map"]');
  await evaluate("window.__mapBefore=WorldPark.toJson(WPUI.pack);window.__pointBefore=JSON.stringify(wpN().points);window.__mapSize=WorldParkSpatial.mapShape(wpN().mapText).width;true");
  const changed=await evaluate("(()=>{const h=document.getElementById('wcMapEditor');const buttons=[...h.querySelectorAll('button')];const b=buttons.find(b=>/扩宽|宽.*\\+|向右.*扩|右.*\\+/.test(b.textContent));if(!b)return false;b.click();return true;})()");
  must(changed,'尺寸快捷扩宽按钮可点');
  const road=await paintRoad();
  const applied=await evaluate("(()=>{const b=[...document.getElementById('wcMapEditor').querySelectorAll('button')].find(b=>/应用/.test(b.textContent));if(!b||b.disabled)return false;b.click();return true;})()");must(applied,'应用回当前阶段可点');await sleep(300);
  must(await evaluate('WorldParkSpatial.mapShape(wpN().mapText).width>__mapSize'),'扩宽地图写回当前阶段');must(await evaluate('JSON.stringify(wpN().points)===__pointBefore'),'默认扩宽点位坐标不变');must(await evaluate('WorldPark.validate(WPUI.pack,wpRoomExists).filter(x=>x.severity===\'error\').length===0'),'应用地图结构仍通过');
  must(await evaluate(`WorldParkSpatial.mapShape(wpN().mapText).at(${road.gx},${road.gy})==='='`),'实际拖画地形写回世界');
  await click('[data-wc="undo"]');must(await evaluate('WorldPark.toJson(WPUI.pack)')===original,'驾驶舱撤销恢复原世界');
  await click('[data-wc="map"]');await evaluate("window.__oldText=wpN().text;wpN().text+='另一操作的新文字';true");await click('[data-wpm-apply]');must(await evaluate("document.getElementById('wcMapDialog').open&&wpN().text.endsWith('另一操作的新文字')"),'地图应用拒绝覆盖打开后发生的新改动');await click('[data-wpm-cancel]');await evaluate('wpN().text=__oldText;wpRender();true');
  // 图标博弈卡必须展示真实成功和失败两条路。
  await click('[data-wc-venue="1"]');must(await evaluate("document.querySelectorAll('.wc-branches button').length>=4"),'博弈阶段展示真实分支');
  // 横版子创作使用隔离测试夹具，不改内置主世界文件。
  await evaluate("(()=>{const v=wpV(),n=wpBlankNode('test_room');n.kind='room';n.name='测试横版片段';n.room='默认恶作剧房间';n.onWin=v.nodes.find(n=>n.kind==='ending').id;n.onLose=n.onWin;v.nodes.push(n);WPUI.ni=v.nodes.length-1;wpPersist(false,'测试夹具');wpRender();return true;})()");
  await sleep(250);
  await click('[data-wc-node="test_room"]');
  await click('#wcRoomOpen');must(await evaluate("document.getElementById('pageDesign').style.display!=='none'&&!document.getElementById('wcRoomReturn').hidden"),'按房间引用进入子工具并出现返回');
  must(await evaluate("document.getElementById('wcChildNote').textContent.includes('1 个阶段')"),'显示同名引用影响');
  await click('#wcRoomReturn');must(await evaluate("wpN().id==='test_room'&&document.getElementById('pageWorldPark').classList.contains('on')"),'从房间子创作回到原阶段');
  await click('[data-wc-dock="cockpit"]');await click('[data-wc="unity"]');must(await evaluate("document.getElementById('wcHelpDialog').open&&document.getElementById('wcHelpBody').textContent.includes('不能')"),'Windows导航明确实际边界');await click('#wcHelpClose');
  // S258：博弈参数表单（真实点击与输入），每一步都能从驾驶舱撤销回原样。
  const s258Start=await evaluate('WorldPark.toJson(WPUI.pack)');
  const iNum=await evaluate("WPUI.pack.venues.findIndex(v=>v.id==='v_number')");
  await click('[data-wc-dock="cockpit"]');await click('[data-wc-venue="'+iNum+'"]');await click('[data-wc="game"]');
  must(await evaluate("wpN().kind==='game'&&!!document.querySelector('[data-wpg-mode=\"beauty\"][aria-checked=\"true\"]')&&!!document.getElementById('wpg_ratio')"),'博弈表单显示当前玩法和比例');
  must(await evaluate("!document.getElementById('wpGameJson').offsetParent"),'JSON 文本框收进高级');
  await evaluate("(()=>{const e=document.getElementById('wpg_rounds');e.value='5';e.dispatchEvent(new Event('change'));return true;})()");await sleep(150);
  must(await evaluate('wpN().game.rounds===5'),'表单改轮数写回世界');
  await click('[data-wpg-mode="split"]');must(await evaluate("wpN().game.mode==='split'&&wpN().game.goal===3&&!!document.getElementById('wpg_goal')&&!document.getElementById('wpg_ratio')"),'切玩法只显示该玩法字段');
  must(await evaluate("BorderlandGame.check(wpN().game).filter(i=>i.severity==='error').length===0&&!!document.querySelector('.wpg-issue.ok')"),'切换后共享规则校验通过');
  await click('[data-wpg-add]');must(await evaluate('wpN().game.seats.length===7'),'表单加参加者');
  must(await evaluate("document.querySelectorAll('.wpg-seat button')[0].disabled"),'唯一的玩家本人不能删');
  await evaluate("(()=>{document.querySelector('.wpg').scrollIntoView({block:'start'});document.getElementById('pageWorldPark').scrollTop-=110;return true;})()");await sleep(120);await shotNamed('博弈表单');
  for(let k=0;k<3;k++){await click('[data-wc-dock="cockpit"]');await click('[data-wc="undo"]');}
  must(await evaluate('WorldPark.toJson(WPUI.pack)')===s258Start,'三步表单改动都能撤销回原样');
  // S258：在地图上点鬼的巡逻格（真实鼠标），再点一次去掉，画布撤销恢复原样。
  const iChase=await evaluate("WPUI.pack.venues.findIndex(v=>v.id==='v_chase')");
  await click('[data-wc-dock="cockpit"]');await click('[data-wc-venue="'+iChase+'"]');await click('[data-wc="points"]');await sleep(150);
  // S259 F02：只选对阶段不算导航通过，工具栏和画布必须直接进入可见区域。
  const landingWorld=await evaluate('WorldPark.toJson(WPUI.pack)');
  for(const width of [1920,1280,680]){
    await send('Emulation.setDeviceMetricsOverride',{width,height:947,deviceScaleFactor:1,mobile:false});
    await click('[data-wc-dock="cockpit"]');await click('[data-wc-venue="'+iChase+'"]');await click('[data-wc="points"]');await sleep(800);
    const landing=await evaluate("(()=>{const pg=document.getElementById('pageWorldPark'),s=document.getElementById('wpSafety'),b=document.querySelector('[data-wps-mode=route]'),v=document.querySelector('.wp-spatial-viewport');const rect=e=>{const r=e.getBoundingClientRect();return {top:r.top,bottom:r.bottom,height:r.height};};return {page:rect(pg),safety:rect(s),button:rect(b),viewport:rect(v),scroll:pg.scrollTop};})()");
    fs.writeFileSync(path.join(outDir,'点位导航_'+width+'.json'),JSON.stringify(landing,null,2));await shotNamed('点位导航_'+width);
    must(landing.button.top>=Math.max(landing.page.top,landing.safety.bottom)-2&&landing.button.bottom<=landing.page.bottom,'点位入口直接显示鬼路线工具栏 '+width);
    must(landing.viewport.top<landing.page.bottom-80&&landing.viewport.bottom>landing.safety.bottom+80,'点位入口直接显示画布 '+width);
    must(await evaluate('WorldPark.toJson(WPUI.pack)')===landingWorld,'点位导航不改世界 '+width);
  }
  await send('Emulation.setDeviceMetricsOverride',{width:1280,height:900,deviceScaleFactor:1,mobile:false});
  const routeStart=await evaluate('WorldPark.toJson(WPUI.pack)');
  const tap=async(x,y)=>{await evaluate('WPUI.spatial.canvas.scrollIntoView({block:"center",inline:"center"});true');await sleep(80);
    const q=await evaluate('(()=>{const sp=WPUI.spatial,c=sp.canvas,b=c.getBoundingClientRect();return {x:b.left+('+x+'+.5)*sp.zoom*b.width/c.width,y:b.top+(sp.preview.height-'+y+'-.5)*sp.zoom*b.height/c.height};})()');
    await send('Input.dispatchMouseEvent',{type:'mouseMoved',x:q.x,y:q.y});await send('Input.dispatchMouseEvent',{type:'mousePressed',x:q.x,y:q.y,button:'left',buttons:1,clickCount:1});await send('Input.dispatchMouseEvent',{type:'mouseReleased',x:q.x,y:q.y,button:'left',buttons:0,clickCount:1});await sleep(220);};
  const oni=await evaluate("(()=>{const ps=wpN().points;const i=ps.findIndex(p=>p.kind==='chaser');return {i,x:ps[i].x,y:ps[i].y,len:ps[i].route.length};})()");
  await tap(oni.x,oni.y);must(await evaluate('WPUI.pi==='+oni.i),'在图上点选鬼');
  await click('[data-wps-mode="route"]');must(await evaluate("WPUI.spatial.mode==='route'"),'进入画鬼路线');
  const target=await evaluate("(()=>{const n=wpN(),p=n.points["+oni.i+"];const s=WorldParkSpatial.mapShape(n.mapText);for(let y=1;y<s.height-1;y++)for(let x=1;x<s.width-1;x++){if(WorldParkSpatial.canPlace(n.mapText,x,y).ok&&!p.route.some(r=>{const c=WorldPark.chaserCell(r);return c&&c.x===x&&c.y===y;})&&!n.points.some(o=>o.x===x&&o.y===y))return {x,y};}return null;})()");
  must(target,'找到可加的巡逻格');const cellText=target.x+','+target.y;
  await tap(target.x,target.y);must(await evaluate("wpN().points["+oni.i+"].route.length==="+(oni.len+1)+"&&wpN().points["+oni.i+"].route.at(-1)==='"+cellText+"'"),'在图上点格加入路线');
  must(await evaluate("[...document.querySelectorAll('[data-wp-route]')].some(e=>e.value.includes('"+cellText+"'))"),'表单里的巡逻格同步');
  must(await evaluate("WPUI.spatial.mode==='route'"),'加一格后仍在画路线');
  await shotNamed('鬼路线');
  await tap(target.x,target.y);must(await evaluate("wpN().points["+oni.i+"].route.length==="+oni.len),'再点一次去掉巡逻格');
  await tap(target.x,target.y);await click('[data-wps-action="undo"]');
  must(await evaluate('WorldPark.toJson(WPUI.pack)')===routeStart,'画布撤销后路线恢复原样');
  await click('[data-wps-mode="select"]');
  // 审计修正的回归：页签层级、放大布局不残留、安全条吸顶齐平
  must(await evaluate("[...document.querySelectorAll('.tab')].map(t=>t.dataset.page).slice(0,2).join()==='cockpit,worldpark'"),'主入口世界工坊排在子创作前');
  await click('[data-wc-dock="cockpit"]');await click('[data-wc-venue="0"]');await click('[data-wc="points"]');must(await evaluate("document.getElementById('pageWorldPark').classList.contains('wp-map-focus')"),'摆点位进入放大布局');
  await click('[data-page="overworld"]');await click('[data-page="worldpark"]');must(await evaluate("!document.getElementById('pageWorldPark').classList.contains('wp-map-focus')"),'切页后放大布局不残留');
  must(await evaluate("(()=>{const pg=document.getElementById('pageWorldPark');pg.scrollTop=1500;const b=pg.querySelector('.wp-safety').getBoundingClientRect(),t=pg.getBoundingClientRect();const ok=pg.scrollTop>0&&Math.abs(b.top-t.top)<=2;pg.scrollTop=0;return ok;})()"),'安全条吸顶不漏出正文');
  for(const width of [1845,900,680]){await send('Emulation.setDeviceMetricsOverride',{width,height:900,deviceScaleFactor:1,mobile:false});await sleep(200);must(await evaluate('document.body.scrollWidth<=innerWidth+2'),'页面无横向溢出 '+width);}
  must(errors.length===0,'页面脚本错误为0');fs.writeFileSync(path.join(outDir,'验证结果.json'),JSON.stringify({results,errors},null,2));console.log('驾驶舱浏览器: PASS '+results.length+' 项，错误: '+errors.length);
 }catch(e){if(e.env){console.log('驾驶舱浏览器: SKIP（'+e.message+'）');}else{errors.push(e.message);console.error('驾驶舱浏览器: FAIL '+e.stack);process.exitCode=1;}fs.writeFileSync(path.join(outDir,'错误.json'),JSON.stringify(errors,null,2));}
 finally{try{ws?.close();proc.kill();}catch(e){}clearInterval(keepAlive);}
})();
