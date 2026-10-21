// S253：真实浏览器（无界面 Edge / Chrome）自动玩一遍网页试玩，收集页面错误并截图关键画面。
// 调试端口只监听 127.0.0.1（随机端口）；键盘是真实按键事件。找不到浏览器时输出 SKIP。
// 用法：node Tools/WorldParkVerify/web-play-smoke.cjs        截图在 .work/worldpark-verify/web-play/
const fs = require('fs'), path = require('path'), cp = require('child_process'), os = require('os');
const repo = path.join(__dirname, '..', '..');
const outDir = path.join(repo, '.work', 'worldpark-verify', 'web-play');
const page = path.join(repo, 'Tools', 'LevelStudioWeb', 'index.html');
const exe = [process.env.WEB_SMOKE_BROWSER, 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe', 'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe', 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].find(p => p && fs.existsSync(p));
if (!exe) { console.log('网页试玩冒烟: SKIP（没有找到 Edge / Chrome）'); process.exit(0); }
if (!fs.existsSync(page)) { console.log('网页试玩冒烟: 失败（缺少 index.html，先运行 build.py）'); process.exit(1); }
fs.rmSync(outDir, { recursive: true, force: true }); fs.mkdirSync(outDir, { recursive: true });
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

// 页面内的小助手：读状态、点按钮、规划博弈选择（与 play-parity.cjs 同一策略）
const BOT = `window.__bot = {
  p() { return WPPlay._state(); },
  st() { const p = this.p(); if (!p) return { open: false }; const s = p.s, r = p.ex && p.ex.run, q = p.root;
    const big = p.cue && p.cue.el ? p.cue.el.querySelector('.big') : null, stamp = q.querySelector('.wpp-stamp');
    return { open: true, mode: p.mode, cue: p.cue ? p.cue.c.kind : '', cueOpacity: big ? +getComputedStyle(big).opacity : -1,
      modal: !!q.querySelector('.wpp-modal:not(.wpp-pause)'), modalText: (q.querySelector('.wpp-modal:not(.wpp-pause) h2') || {}).textContent || '', modalBody: (q.querySelector('.wpp-modal:not(.wpp-pause) .wpp-panel') || {}).textContent || '',
      venue: s ? s.venueId : '', node: s ? s.nodeId : '', kind: s ? s.Current.kind : '', ready: p.mode === 'scene' && p.sc ? p.sc.t >= WPPlayModel.Scene.ReadyAt(p.sc.sc) : false,
      doors: p.mode === 'scene' ? [...q.querySelectorAll('.wpp-door.open')].map(b => b.dataset.v) : [], stamp: stamp ? stamp.className.replace('wpp-stamp', '').trim() : '',
      twistsOn: q.querySelectorAll('.wpp-twist.on').length, twists: q.querySelectorAll('.wpp-twist').length,
      ended: r ? r.Ended : false, won: r ? r.Won : false, chasersSeen: r ? r.Chasers.filter(c => r.PlayerSees(c)).length : 0, remaining: r ? r.Remaining : 0,
      phase: p.g && p.g.view ? p.g.view.phase : '', round: p.g ? p.g.st.round : 0, flipped: p.g && p.g.view ? p.g.view.flipped : 0,
      top: [...q.querySelectorAll('.wpp-toasts.top .wpp-toast')].map(t => t.textContent), clueCard: !!(q.querySelector('.wpp-cluecard') && !q.querySelector('.wpp-cluecard').hidden),
      canvas: (() => { const c = q.querySelector('canvas'); if (!c) return null; const d = c.getContext('2d').getImageData(Math.floor(c.width / 2), Math.floor(c.height / 2), 1, 1).data; return { w: c.width, h: c.height, center: [d[0], d[1], d[2]] }; })() };
  },
  click(sel) { const el = this.p().root.querySelector(sel); if (!el) return false; el.click(); return true; },
  points() { const s = this.p().s; return (s.Current.points || []).filter(q => q && q.kind !== 'chaser').map(q => ({ id: q.id, kind: q.kind, x: q.x, y: q.y, clue: q.clue || '', outcome: q.outcome || '' })); },
  teleport(x, y) { const r = this.p().ex.run; r.x = x; r.y = y; return true; },
  nearChaser() { const r = this.p().ex.run, c = r.Chasers[0]; if (!c) return null; let best = null;
    for (let dy = -5; dy <= 5; dy++) for (let dx = -5; dx <= 5; dx++) { const tx = Math.floor(c.x) + dx, ty = Math.floor(c.y) + dy, px = tx + 0.5, py = ty + 0.5, ddx = px - c.x, ddy = py - c.y, d = Math.hypot(ddx, ddy);
      if (d < 2.5 || d > 4.5 || !WPExplore.walkable(r.map, tx, ty) || (ddx * c.fx + ddy * c.fy) / d < 0.6 || !WPExplore.lineOfSight(r.map, c.x, c.y, px, py)) continue; if (!best || d < best.d) best = { x: px, y: py, d }; }
    return best; },
  plan() { const def = this.p().s.Current.game, BG = BorderlandGame;
    const options = def.mode === 'beauty' ? Array.from({ length: 101 }, (_, i) => String(i)) : def.mode === 'mark' ? ['♠', '♥', '♦', '♣'] : ['分', '偷'];
    const score = st => st.error ? -1e9 : st.finished ? (st.won ? 1e6 : -1e6) : (st.alive[st.player] ? st.lives[st.player] * 1000 + st.points[st.player] : -1e6);
    if (def.mode !== 'beauty') { const dfs = pre => { const st = BG.run(def, pre); if (st.error) return null; if (st.finished) return st.won ? pre : null; if (pre.length >= 8) return null; for (const o of options) { const r = dfs(pre.concat([o])); if (r) return r; } return null; }; const w = dfs([]); if (w) return w; }
    const ch = []; for (let k = 0; k < 40; k++) { let best = -Infinity, pick = options[0]; for (const o of options) { const sc = score(BG.run(def, ch.concat([o]))); if (sc > best) { best = sc; pick = o; } } ch.push(pick); const st = BG.run(def, ch); if (st.error || st.finished) break; } return ch; },
  submit(c) { const p = this.p(), box = p.root.querySelector('.wpp-ctl'); if (!box) return false;
    if (p.g.def.mode === 'beauty') { const nm = box.querySelector('.wpp-num'); if (!nm) return false; nm.value = c; nm.dispatchEvent(new Event('input')); box.querySelector('[data-a=go]').click(); return true; }
    const b = [...box.querySelectorAll('[data-s]')].find(x => x.dataset.s === c); if (!b) return false; b.click(); return true; }
}; true`;

const keepAlive = setInterval(() => { }, 1000);
process.on('exit', () => { try { proc.kill(); } catch (e) { } });
(async () => {
  const fail = m => { errors.push(m); };
  const t0 = Date.now();
  try {
    await connect();
    try { await send('Page.enable'); await send('Runtime.enable'); } catch (e) { throw envError('浏览器页面没有响应（' + e.message + '）'); }
    await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false });
    await send('Page.navigate', { url: 'file:///' + page.replace(/\\/g, '/') + '#play' });
    for (let i = 0; i < 50; i++) { await sleep(200); if (await evaluate("typeof WPPlay !== 'undefined' && WPPlay.isOpen()").catch(() => false)) break; }
    if (!(await evaluate("typeof WPPlay !== 'undefined' && WPPlay.isOpen()"))) throw Error('#play 没有自动打开试玩');
    await evaluate(BOT);
    await sleep(600); await shot('标题');
    if (!(await evaluate("__bot.click('[data-a=new]')"))) throw Error('标题页没有“开始旅程”');
    const seen = { venues: new Set(), scenes: 0, explores: 0, games: 0, twists: 0, cues: {}, entranceOpacity: [], chaseSeen: 0, clueCards: 0, retried: new Set() };
    const shotOnce = new Set();
    const once = async name => { if (shotOnce.has(name)) return; shotOnce.add(name); await shot(name); };
    const prefer = ['v_chase', 'v_split', 'v_final', 'v_mark', 'v_number'];
    let handled = '', plan = null, planIdx = 0, lastPhase = '';
    for (let step = 0; step < 900; step++) {
      const st = await evaluate('__bot.st()');
      if (!st.open) { fail('试玩被意外关闭'); break; }
      for (const m of st.top) if (m.includes('出错')) fail('画面提示：' + m);
      if (st.venue) seen.venues.add(st.venue);
      if (st.mode === 'summary') { seen.summary = true; await shot('旅程总结'); break; }
      if (st.cue) {
        seen.cues[st.cue] = (seen.cues[st.cue] || 0) + 1;
        if (st.cue === 'entrance') { await sleep(1500); const s2 = await evaluate('__bot.st()'); seen.entranceOpacity.push(s2.cueOpacity); await once('入场霓虹'); }
        else { await sleep(700); await once('演出_' + st.cue); }
        await evaluate("__bot.click('.wpp-cue')"); await sleep(120); continue;
      }
      if (st.modal) { if (/锁定|Tick|已经结束/.test(st.modalBody)) fail('结果面板出现内核提示：' + st.modalBody.slice(0, 60)); await sleep(300); await once('结果面板_' + st.modalText); await evaluate("__bot.click('.wpp-modal .wpp-btn.main')"); await sleep(150); continue; }
      const here = st.venue + '/' + st.node + '#' + step;
      if (st.mode === 'scene') {
        if (!st.ready) { if (st.kind === 'reveal' && st.twists && !shotOnce.has('反转卡')) { await sleep(400); } await key('Space'); await sleep(200); continue; }
        seen.scenes++;
        if (st.twists) { seen.twists += st.twistsOn; await once('反转卡'); }
        if (st.kind === 'ending') {
          await once('结局_' + (st.stamp || 'end'));
          const next = st.stamp === 'gameover' ? null : (prefer.find(v => st.doors.includes(v) && !seen.venues.has(v)) || st.doors.find(v => !seen.venues.has(v)));
          if (next) { await evaluate(`__bot.click('.wpp-door.open[data-v="${next}"]')`); }
          else if (st.stamp === 'gameover' && !seen.retried.has(st.venue)) { seen.retried.add(st.venue); await once('结局_被抓'); if (!(await evaluate("__bot.click('[data-a=retry]')"))) fail('出局结局没有“重来本会场”按钮'); }
          else if (!(await evaluate("__bot.click('[data-a=finish]')"))) await evaluate("__bot.click('[data-a=main]')");
          handled = ''; await sleep(250); continue;
        }
        if (seen.scenes === 1) await shot('第一张字幕卡');
        await key('Space'); await sleep(200); continue;
      }
      if (st.mode === 'explore') {
        const nodeKey = st.venue + '/' + st.node;
        if (handled !== nodeKey + seen.explores) {
          seen.explores++; handled = nodeKey + seen.explores;
          await key('KeyD', 450); await key('KeyW', 350); await sleep(150);
          const s1 = await evaluate('__bot.st()');
          if (!s1.canvas || s1.canvas.w < 100) fail('探索画布没有尺寸');
          await shot('现场_' + st.venue);
          if (st.venue === 'v_chase' && !seen.caughtTest) {
            seen.caughtTest = true; const spot = await evaluate('__bot.nearChaser()');
            if (!spot) fail('找不到鬼面前的可站位置');
            else { await evaluate(`__bot.teleport(${spot.x}, ${spot.y})`); await sleep(350); await shot('鬼的视野_被发现');
              for (let i = 0; i < 60; i++) { const s4 = await evaluate('__bot.st()'); if (s4.ended || s4.cue) break; await sleep(150); }
              const s5 = await evaluate('__bot.st()'); if (!(s5.ended || s5.cue)) fail('站在鬼面前 9 秒都没被抓');
              continue; }
          }
          const pts = await evaluate('__bot.points()');
          for (const q of pts.filter(q => q.kind === 'clue')) {
            await evaluate(`__bot.teleport(${q.x + 0.5}, ${q.y + 0.5})`); await sleep(90); await key('KeyE'); await sleep(160);
            const s2 = await evaluate('__bot.st()'); if (s2.clueCard) { seen.clueCards++; if (seen.clueCards === 1) await shot('线索卡'); }
            if (s2.chasersSeen) { seen.chaseSeen++; await once('鬼的视野_' + st.venue); }
            if (s2.ended) break;
          }
          for (const q of pts.filter(q => q.kind === 'exit' && q.outcome === 'win')) {
            const s3 = await evaluate('__bot.st()'); if (s3.ended) break;
            await evaluate(`__bot.teleport(${q.x + 0.5}, ${q.y + 0.5})`); await sleep(90); await key('KeyE'); await sleep(200);
          }
        } else { await sleep(300); if (Date.now() - t0 > 240000) { fail('探索卡住：' + nodeKey); break; } }
        continue;
      }
      if (st.mode === 'game') {
        const nodeKey = st.venue + '/' + st.node;
        if (handled !== nodeKey + seen.games + 'g') { seen.games++; handled = nodeKey + seen.games + 'g'; plan = await evaluate('__bot.plan()'); planIdx = 0; await sleep(300); await shot('博弈桌_' + st.venue); }
        if (st.phase === 'choose' && planIdx < plan.length) { if (!(await evaluate(`__bot.submit(${JSON.stringify(plan[planIdx])})`))) fail('博弈提交失败：' + plan[planIdx]); planIdx++; await sleep(150); }
        else if (st.phase === 'reveal' || st.phase === 'result') {
          if (planIdx === 1 && lastPhase !== st.phase && st.phase === 'result') await once('亮牌结果_' + st.venue);
          if (planIdx > 1) await key('Space');
          await sleep(200);
        } else await sleep(200);
        lastPhase = st.phase;
        continue;
      }
      await sleep(200);
    }
    const lines = [
      `走过会场：${[...seen.venues].join(' → ')}（${seen.venues.size} 个）`,
      `字幕卡 ${seen.scenes} 张 · 反转卡逐张亮出 ${seen.twists} 张 · 现场 ${seen.explores} 次（线索卡 ${seen.clueCards} 次，看见鬼 ${seen.chaseSeen} 次）· 博弈 ${seen.games} 场`,
      `演出：${Object.entries(seen.cues).map(([k, v]) => k + '×' + v).join('，')} · 入场霓虹不透明度 ${seen.entranceOpacity.map(x => x.toFixed(2)).join('/')}`,
      `被抓后重来：${[...seen.retried].join('、') || '无'}`,
      `截图 ${shots.length} 张 → ${path.relative(repo, outDir)}`
    ];
    if (seen.venues.size < 4) fail('走过的会场少于 4 个');
    if (!seen.summary) fail('没有走到旅程总结');
    if (seen.scenes > 60) fail('字幕卡循环次数异常（' + seen.scenes + '），可能卡在某个按钮上');
    if (!seen.retried.has('v_chase')) fail('没有覆盖“被鬼抓住 → 重来本会场”');
    if (!seen.explores || !seen.games || !seen.twists) fail('没有同时覆盖现场 / 博弈 / 反转卡');
    if (seen.entranceOpacity.some(x => x < 0.5)) fail('入场霓虹在 1.5 秒时仍不可见');
    // ── S254：创作流程（世界页）：会场体检面板 → 用模板新建会场 → 只玩这个会场 ──
    await send('Page.navigate', { url: 'file:///' + page.replace(/\\/g, '/') });
    for (let i = 0; i < 50; i++) { await sleep(200); if (await evaluate("typeof wpV === 'function' && !!document.getElementById('wpVenueCheck') && document.querySelectorAll('#wpVenueCheck .owiss').length > 0").catch(() => false)) break; }
    await evaluate(BOT);
    const a = await evaluate(`(() => ({ items: document.querySelectorAll('#wpVenueCheck .owiss').length, title: document.getElementById('wpVenueCheckTitle').textContent, venues: WPUI.pack.venues.length, tpl: document.getElementById('wpTemplate').options.length }))()`);
    if (!a.items) fail('世界页的会场体检没有内容');
    if (!/红 0/.test(a.title)) fail('内置主世界入口会场体检出现红色问题：' + a.title);
    if (a.tpl < 6) fail('会场模板少于 6 个：' + a.tpl);
    const b = await evaluate(`(() => { const sel = document.getElementById('wpTemplate'); sel.value = String(WORLD_PARK_SAMPLE.venues.findIndex(v => v.id === 'v_chase')); document.getElementById('wpFromTemplate').click(); return { venues: WPUI.pack.venues.length, id: wpV().id, name: wpV().name }; })()`);
    if (b.venues !== a.venues + 1 || b.id !== 'v_chase_my') fail('用模板新建会场失败：' + JSON.stringify(b));
    await sleep(900);
    const c = await evaluate(`(() => { document.getElementById('wpVenueCheck').scrollIntoView({ block: 'center' }); return { title: document.getElementById('wpVenueCheckTitle').textContent, text: document.getElementById('wpVenueCheck').textContent }; })()`);
    if (!c.title.includes('我的改编') || !/直奔/.test(c.text)) fail('新会场的体检没有刷新或缺少直奔试跑：' + c.title);
    await sleep(200); await shot('创作_会场体检与模板');
    await evaluate("document.getElementById('wpPlayVenue').click(); true");
    for (let i = 0; i < 20 && !(await evaluate("WPPlay.isOpen()")); i++) await sleep(150);
    await evaluate(BOT); await sleep(300);
    if (!(await evaluate("__bot.click('[data-a=new]')"))) fail('只玩这个会场：标题页没有“开始旅程”');
    let venueSeen = '';
    for (let i = 0; i < 30 && !venueSeen; i++) { await sleep(200); const st = await evaluate('__bot.st()'); if (st.venue) venueSeen = st.venue; }
    if (venueSeen !== 'v_chase_my') fail('只玩这个会场没有从该会场开始：' + venueSeen);
    await sleep(1200); await shot('创作_只玩这个会场');
    await evaluate("WPPlay.close(); true");
    // ── S256：安全网（世界页）：误点“整张替换地图”→ 结构预检说人话 → 只玩被拦并就地说明 → 一键挪点 → Ctrl+Z 撤销 → 只恢复这个阶段 → Ctrl+Z / Ctrl+Y ──
    const ctrl = async (code, shift) => { const k = code.slice(3).toLowerCase(), vk = k.toUpperCase().charCodeAt(0), m = 2 | (shift ? 8 : 0); await send('Input.dispatchKeyEvent', { type: 'keyDown', code, key: k, windowsVirtualKeyCode: vk, modifiers: m }); await send('Input.dispatchKeyEvent', { type: 'keyUp', code, key: k, windowsVirtualKeyCode: vk, modifiers: m }); };
    const sd = await evaluate(`(() => { window.confirm = () => true; const vi = WPUI.pack.venues.findIndex(v => v.id === 'v_final'); if (vi < 0) return { err: '主世界里没有 v_final' }; WPUI.vi = vi; WPUI.ni = WPUI.pack.venues[vi].nodes.findIndex(n => n.id === 'room'); WPUI.pi = 0; wpRender();
      window.__orig = wpN().mapText; const origin0 = document.getElementById('wpOrigin').textContent;
      wpAdvSet(true); const btn = document.getElementById('wpCopyTown'); if (!btn) return { err: '没有“用小镇页的地图整张替换”按钮' }; btn.click(); wpAdvSet(false);
      document.getElementById('wpIssues').scrollIntoView({ block: 'center' });
      return { origin0, changed: wpN().mapText !== window.__orig, errs: WPUI.errCount, issues: document.getElementById('wpIssues').textContent, last: document.getElementById('wpLast').textContent, origin1: document.getElementById('wpOrigin').textContent, fix: !!document.querySelector('#wpIssues [data-wp-issue-fix]') }; })()`);
    if (sd.err) fail('安全网：' + sd.err);
    else {
      if (!/完全一样/.test(sd.origin0)) fail('安全网：未改动的主世界会场没有显示“和内置原版完全一样”：' + sd.origin0.slice(0, 80));
      if (!sd.changed || sd.errs < 1) fail('安全网：整张替换地图后没有出现必须修的问题（' + sd.errs + '）');
      if (!/必须修 · K 主办者的房间 · 钟楼控制室/.test(sd.issues) || !/怎么修/.test(sd.issues)) fail('安全网：结构预检没有写清在哪、怎么修：' + sd.issues.slice(0, 120));
      if (!sd.fix) fail('安全网：结构预检没有“把点挪到最近的空地”按钮');
      if (!/用小镇地图替换/.test(sd.last) || !/必须修的问题 0 → /.test(sd.last)) fail('安全网：安全条没有说清刚才改了什么、影响了什么：' + sd.last.slice(0, 120));
      if (!/改了 \d+ 处/.test(sd.origin1)) fail('安全网：和原版对比没有列出改动');
      await sleep(250); await shot('安全网_结构预检说人话');
      const se = await evaluate(`(() => { document.getElementById('wpPlayVenue').click(); const pb = document.getElementById('wpPlayBlock'); pb.scrollIntoView({ block: 'center' }); return { open: WPPlay.isOpen(), block: pb.textContent, toast: (document.getElementById('wpToast') || {}).textContent || '' }; })()`);
      if (se.open) fail('安全网：有必须修的问题时“只玩这个会场”仍然打开了试玩');
      if (!/还不能玩「K 主办者的房间」/.test(se.block) || !/还不能玩/.test(se.toast)) fail('安全网：只玩被拦时按钮下方 / 浮动提示没有说明原因：' + se.block.slice(0, 80));
      await sleep(250); await shot('安全网_只玩被拦就地说明');
      const sf = await evaluate(`(() => { const b = document.querySelector('#wpPlayBlock [data-wp-issue-fix]'); if (!b) return { err: '按钮下方的说明里没有一键挪点按钮' }; b.click(); document.getElementById('wpSpatialHost').scrollIntoView({ block: 'center' }); return { errs: WPUI.errCount, bad: WPHistory.badPoints(WorldParkSpatial, wpN()).length, block: document.getElementById('wpPlayBlock').textContent, toast: document.getElementById('wpToast').textContent }; })()`);
      if (sf.err) fail('安全网：' + sf.err);
      else if (sf.errs !== 0 || sf.bad !== 0) fail(`安全网：一键挪点后仍有问题（必须修 ${sf.errs}，坏点 ${sf.bad}）`);
      else if (!/可以点 ▶/.test(sf.block) || !/挪到最近的空地/.test(sf.toast)) fail('安全网：修好后没有提示“可以玩了” / 挪了哪些点：' + sf.block.slice(0, 60));
      await sleep(250); await shot('安全网_一键挪点后');
      await evaluate("document.activeElement && document.activeElement.blur && document.activeElement.blur(); true");
      await ctrl('KeyZ'); await sleep(150);
      const sg = await evaluate(`(() => ({ errs: WPUI.errCount, toast: document.getElementById('wpToast').textContent, venue: wpV().id, node: wpN().id }))()`);
      if (sg.errs < 1 || !/已撤销：一键挪点/.test(sg.toast) || sg.venue !== 'v_final' || sg.node !== 'room') fail('安全网：Ctrl+Z 没有撤销一键挪点或没回到原处：' + JSON.stringify(sg));
      const sh = await evaluate(`(() => { const b = document.getElementById('wpRestoreNode'); if (!b) return { err: '没有“只恢复这个阶段”按钮' }; b.click(); return { same: wpN().mapText === window.__orig, errs: WPUI.errCount, origin: document.getElementById('wpOrigin').textContent }; })()`);
      if (sh.err) fail('安全网：' + sh.err);
      else if (!sh.same || sh.errs !== 0 || !/完全一样/.test(sh.origin)) fail('安全网：只恢复这个阶段后地图没回到原版：' + JSON.stringify(sh).slice(0, 160));
      await ctrl('KeyZ'); await sleep(120); const su = await evaluate('wpN().mapText === window.__orig');
      await ctrl('KeyY'); await sleep(120); const sr = await evaluate('wpN().mapText === window.__orig');
      if (su || !sr) fail('安全网：Ctrl+Z / Ctrl+Y 没有正确撤销 / 重做“恢复原版”');
      await sleep(900);
      const si = await evaluate(`(() => { const box = document.getElementById('wpHistBox'); box.open = true; document.getElementById('wpSafety').scrollIntoView({ block: 'start' }); return { rows: document.querySelectorAll('#wpHistList .wp-hist-row').length, saved: !!localStorage.getItem(WPLS + '.history'), first: (document.querySelector('#wpHistList .wp-hist-row span') || {}).textContent || '' }; })()`);
      if (si.rows < 3 || !si.saved) fail('安全网：改动记录没有列出 / 没有存进浏览器：' + JSON.stringify(si));
      await sleep(250); await shot('安全网_改动记录');
      await evaluate("document.getElementById('wpHistBox').open = false; true");
      lines.splice(lines.length - 1, 0, `安全网：整张替换地图 → 必须修 ${sd.errs} 处（人话 + 修复按钮）· 只玩被拦并就地说明 · 一键挪点 → 0 处 · Ctrl+Z 撤销 · 只恢复这个阶段 = 原版 · Ctrl+Z/Ctrl+Y · 改动记录 ${si.rows} 条（最新：${si.first}）`);
    }
    lines.splice(lines.length - 1, 0, `创作流程：入口会场体检 ${a.items} 条（${a.title}）· 模板 ${a.tpl} 个 · 用模板新建 → ${b.id}（${c.title}）· 只玩这个会场 → 从 ${venueSeen} 开始`);
    console.log(lines.join('\n'));
  } catch (e) {
    if (e.env) { console.log('网页试玩冒烟: SKIP（' + e.message + '）'); try { proc.kill(); } catch (x) { } setTimeout(() => process.exit(0), 300); return; }
    fail('冒烟脚本中断：' + e.message);
  }
  for (const e of errors.slice(0, 12)) console.log('✗ ' + e);
  console.log(`网页试玩冒烟: 用时 ${((Date.now() - t0) / 1000).toFixed(0)} 秒, 错误: ${errors.length}`);
  try { ws.close(); } catch (e) { }
  setTimeout(() => { try { proc.kill(); } catch (e) { } try { fs.rmSync(profile, { recursive: true, force: true }); } catch (e) { } process.exit(errors.length ? 1 : 0); }, 400);
})();
