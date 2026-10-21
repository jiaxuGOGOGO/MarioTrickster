// S253：网页试玩探索内核（Tools/LevelStudioWeb/explore.js）↔ C# WorldParkExplore 逐步对比。
// 输入脚本由 JS 内核上的“自动驾驶”生成（走向线索、调查、躲进草里、来回晃、直冲出口……），然后两边回放同一份脚本。
// 位置类数值容差 1e-6；模式、事件、结束、胜负、消息、线索必须完全一致。
const fs = require('fs'), path = require('path'), cp = require('child_process');
const repo = path.join(__dirname, '..', '..');
const W = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'worldpark.js'));
const X = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'explore.js'));
const dir = path.join(repo, '.work', 'worldpark-verify'); fs.mkdirSync(dir, { recursive: true });
const worldText = fs.readFileSync(path.join(repo, 'Assets', 'Resources', 'WorldPark', 'Borderland.worldpark.json'), 'utf8').replace(/^\uFEFF/, '');

function apply(s, a) {
  if (a === 'next') return s.Continue();
  if (a === 'win' || a === 'lose') return s.ResolveRoom(a === 'win');
  if (a === 'explore-win' || a === 'explore-lose') return s.ResolveExplore(a === 'explore-win');
  if (a.startsWith('observe:')) return s.ObserveClue(a.slice(8));
  if (a.startsWith('game:')) return s.ResolveGame(a.slice(5).split('|'));
  return s.Travel(a.slice(7));
}
function session(json, pre) { const s = W.start(W.parse(json)); for (const a of pre) apply(s, a); return s; }
function withEntry(venueId) { const p = JSON.parse(worldText); p.entryVenue = venueId; return JSON.stringify(p); }

// ── 自动驾驶：在 JS 内核上闭环生成输入，记录成开环脚本 ──
function pilot(json, pre, plan, maxSteps = 4000) {
  const s = session(json, pre), r = X.create(s).run, steps = [];
  const dtCycle = [1 / 60, 1 / 30, 0.05, 0.1, 1 / 30, 0.07];
  let k = 0;
  const tick = (h, v) => { const dt = dtCycle[k++ % dtCycle.length]; steps.push([dt, h, v]); r.Tick(dt, h, v); };
  const goTo = (tx, ty, limit = 1200) => {
    for (let n = 0; n < limit && !r.Ended && steps.length < maxSteps; n++) {
      const cx = Math.floor(r.x), cy = Math.floor(r.y);
      const dxT = tx + 0.5 - r.x, dyT = ty + 0.5 - r.y;
      if (dxT * dxT + dyT * dyT < 0.25 * 0.25) return true;
      const p = X.path(r.map, { x: cx, y: cy }, { x: tx, y: ty });
      if (!p) return false;
      const nxt = p.length > 1 ? p[1] : p[0];
      let hx = nxt.x + 0.5 - r.x, hy = nxt.y + 0.5 - r.y; const len = Math.sqrt(hx * hx + hy * hy) || 1;
      tick(hx / len, hy / len);
    }
    return false;
  };
  for (const cmd of plan) {
    if (r.Ended || steps.length >= maxSteps) break;
    if (cmd.go) goTo(cmd.go[0], cmd.go[1]);
    else if (cmd.act) { steps.push(['act', cmd.act]); r.Interact(cmd.act); }
    else if (cmd.wait) { for (let i = 0; i < cmd.wait && !r.Ended; i++) tick(0, 0); }
    else if (cmd.wiggle) { for (let i = 0; i < cmd.wiggle && !r.Ended; i++) tick(i % 12 < 6 ? 1 : -1, 0); }
    else if (cmd.raw) { for (const st of cmd.raw) { steps.push(st); if (st[0] === 'act') r.Interact(st[1]); else r.Tick(+st[0], +st[1], +st[2]); } }
  }
  return steps;
}
const pt = (json, pre, id) => { const s = session(json, pre); return s.Current.points.find(q => q.id === id); };

const cases = [];
function add(name, json, pre, plan) { cases.push({ name, json, pre, steps: pilot(json, pre, plan) }); }

// ♣3 三扇门：调查全部线索后走检修门；以及缺线索直接去检修门
{
  const json = worldText, pre = ['next'], P = id => pt(json, pre, id);
  const plan = [];
  for (const id of ['rule_board', 'scorch', 'handle_tag']) { const q = P(id); plan.push({ go: [q.x, q.y] }, { act: id }); }
  const exit = P('service_door'); plan.push({ act: 'service_door' }, { go: [exit.x, exit.y] }, { act: 'service_door' });
  add('♣3 调查全部后检修门', json, pre, plan);
  add('♣3 不调查直奔检修门', json, pre, [{ go: [exit.x, exit.y] }, { act: 'service_door' }, { act: 'scorch' }, { act: 'nope' }]);
  const main = P('main_door'); add('♣3 冲向大门', json, pre, [{ go: [main.x, main.y] }, { act: 'main_door' }, { raw: [[0.1, 1, 0], ['act', 'main_door']] }]);
  add('♣3 超时', json, pre, [{ wait: 800 }, { act: 'main_door' }, { raw: [[0.1, 1, 0]] }]);
}
// ♠5 鬼抓人：直冲旗、躲草不动、草里来回晃、先读公告板再走操场
{
  const json = withEntry('v_chase'), pre = ['next'], P = id => pt(json, pre, id);
  const base = P('base'), notice = P('notice'), badge = P('badge');
  add('♠5 直冲旗', json, pre, [{ go: [base.x, base.y] }, { act: 'base' }]);
  add('♠5 公告板→工作证→旗', json, pre, [{ go: [notice.x, notice.y] }, { act: 'notice' }, { go: [badge.x, badge.y] }, { act: 'badge' }, { go: [base.x, base.y] }, { act: 'base' }]);
  // 找离出生点最近的高草，进去不动 / 来回晃
  const s = session(json, pre), r = X.create(s).run; let grass = null, best = 1e9;
  for (let y = 0; y < r.map.H; y++) for (let x = 0; x < r.map.W; x++) if (X.hides(X.at(r.map, x, y)) && X.walkable(r.map, x, y)) { const p = X.path(r.map, { x: Math.floor(r.x), y: Math.floor(r.y) }, { x, y }); if (p && p.length < best) { best = p.length; grass = { x, y }; } }
  if (grass) {
    add('♠5 草里不动', json, pre, [{ go: [grass.x, grass.y] }, { wait: 500 }]);
    add('♠5 草里来回晃', json, pre, [{ go: [grass.x, grass.y] }, { wiggle: 600 }]);
  }
  add('♠5 无效输入', json, pre, [{ raw: [[2, 0, 0], [-0.1, 0, 0], ['NaN', 0, 0], [0.1, 'Infinity', 0], [0.1, 3, 4], [0, 0, 0]] }, { go: [base.x, base.y] }]);
}
// 自定义小图：原地守的鬼（无路线、每 1.5 秒转身）、自定义速度与视距
{
  const p = JSON.parse(worldText); p.entryVenue = 'v_chase';
  const v = p.venues.find(x => x.id === 'v_chase'), n = v.nodes.find(x => x.kind === 'explore');
  n.mapText = ['tttttttttttttttttttt', 't..................t', 't..""""""..........t', 't..""""""....###...t', 't.......T....#.....t', 't..................t', 't.........""".....5t', 'tttttttttttttttttttt'].join('\n').replace(/#/g, 'W').replace(/5/g, '.');
  n.points = [
    { id: 'base', kind: 'exit', label: '旗', x: 17, y: 1, outcome: 'win', requires: [] },
    { id: 'guard', kind: 'chaser', label: '守门的鬼', x: 15, y: 3, speed: 50, sight: 5, route: [] },
    { id: 'walker', kind: 'chaser', label: '巡逻的鬼', x: 3, y: 1, speed: 20, sight: 12, route: ['3,1', '10,1', '10,6', '3,6'] }
  ];
  n.timeout = 30;
  v.clues = v.clues.filter(c => c.foundAt !== n.id);
  const json = JSON.stringify(p), pre = ['next'];
  add('小图 原地守的鬼', json, pre, [{ wait: 120 }, { go: [17, 1] }, { act: 'base' }]);
  add('小图 草里等巡逻', json, pre, [{ go: [4, 5] }, { wait: 300 }, { go: [17, 1] }, { act: 'base' }]);
}

fs.writeFileSync(path.join(dir, 'explore-cases.json'), JSON.stringify(cases));
const proj = path.join(repo, 'Tools', 'WorldParkVerify', 'Snapshot', 'WorldParkSnapshot.csproj');
cp.execFileSync('dotnet', ['run', '--project', proj, '--', 'explore', path.join(dir, 'explore-cases.json'), path.join(dir, 'csharp-explore.json')], { stdio: 'inherit' });
const cs = JSON.parse(fs.readFileSync(path.join(dir, 'csharp-explore.json'), 'utf8'));

// ── JS 回放 ──
function jsRun(c) {
  let s; try { s = W.start(W.parse(c.json)); } catch (e) { return { name: c.name, error: 'start:' + e.message }; }
  for (const a of c.pre) { try { apply(s, a); } catch (e) { return { name: c.name, error: 'pre:' + a + ':' + e.message }; } }
  const made = X.create(s); if (!made.run) return { name: c.name, error: 'create:' + made.error };
  const r = made.run, st = x => { const o = X.snapshot(x); o.ev = o.events.length; delete o.events; return o; };
  const states = [st(r)], acts = [];
  for (const step of c.steps) { if (step[0] === 'act') { const res = r.Interact(step[1]); acts.push({ ok: res.ok, m: res.message }); } else r.Tick(+step[0], +step[1], +step[2]); states.push(st(r)); }
  return { name: c.name, error: '', states, acts, events: r.Events };
}
let diffs = 0, compared = 0, totalSteps = 0; const report = [];
const num = (a, b) => Math.abs(a - b) <= 1e-6;
function cmp(where, a, b) {
  compared++;
  if (typeof a === 'number' && typeof b === 'number') { if (!num(a, b)) { diffs++; report.push(`${where}: C# ${a} ≠ JS ${b}`); return false; } return true; }
  if (Array.isArray(a) && Array.isArray(b)) { if (a.length !== b.length) { diffs++; report.push(`${where}: 长度 C# ${a.length} ≠ JS ${b.length}`); return false; } let ok = true; for (let i = 0; i < a.length; i++) ok = cmp(where + '[' + i + ']', a[i], b[i]) && ok; return ok; }
  if (a && b && typeof a === 'object' && typeof b === 'object') { let ok = true; for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) ok = cmp(where + '.' + k, a[k], b[k]) && ok; return ok; }
  if (a !== b) { diffs++; report.push(`${where}: C# ${JSON.stringify(a)} ≠ JS ${JSON.stringify(b)}`); return false; }
  return true;
}
const summary = [];
for (let i = 0; i < cases.length; i++) {
  const j = jsRun(cases[i]), c = cs[i];
  const before = diffs;
  cmp(cases[i].name + '.error', c.error, j.error);
  if (!c.error && !j.error) {
    totalSteps += c.states.length;
    for (let k = 0; k < Math.max(c.states.length, j.states.length); k++) { if (!cmp(`${cases[i].name}#${k}`, c.states[k], j.states[k])) break; }
    cmp(cases[i].name + '.acts', c.acts, j.acts); cmp(cases[i].name + '.events', c.events, j.events);
  }
  const last = j.states ? j.states[j.states.length - 1] : null;
  summary.push(`${cases[i].name}：${cases[i].steps.length} 步` + (last ? `，${last.ended ? (last.won ? '通关' : (last.caughtBy ? '被' + last.caughtBy + '抓' : '失败')) : '未结束'}，${(j.events || []).map(e => e.type).join('/') || '无追逐事件'}` : '') + (diffs > before ? '  ← 有差异' : ''));
}
console.log(summary.join('\n'));
if (report.length) console.log(report.slice(0, 20).join('\n'));
console.log(`探索内核: ${cases.length} 用例, ${totalSteps} 个逐步状态, 比较 ${compared} 项, 差异: ${diffs}`);
process.exit(diffs ? 1 : 0);
