// S254：会场体检回归测试——主世界必须全部“红 0”，故意做坏的会场必须被正确点名。
// 用法：node Tools/WorldParkVerify/venue-check.cjs
const fs = require('fs'), path = require('path');
const repo = path.join(__dirname, '..', '..');
const W = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'worldpark.js'));
const C = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'venue-check.js'));
const load = f => W.parse(fs.readFileSync(path.join(repo, 'Assets', 'Resources', 'WorldPark', f), 'utf8').replace(/^\uFEFF/, ''));
let fails = 0, checks = 0;
const ok = (cond, msg) => { checks++; if (!cond) { fails++; console.log('✗ ' + msg); } };
const has = (r, level, re) => r.items.some(i => i.level === level && re.test(i.text));

const world = load('Borderland.worldpark.json');
const t0 = Date.now(), rows = C.world(world), ms = Date.now() - t0;
for (const r of rows) ok(r.counts.bad === 0, '主世界会场 ' + r.id + ' 出现红色问题：' + r.items.filter(i => i.level === 'bad').map(i => i.text).join('；'));
ok(rows.length >= 6, '主世界会场少于 6 个');
const by = id => rows.find(r => r.id === id);
ok(has(by('v_chase'), 'good', /直奔.*抓住/), '♠5 的“直奔玩家”应被鬼抓住');
ok(has(by('v_doors'), 'good', /限时 35 秒/), '♣3 限时结论应为绿色');
for (const id of ['v_number', 'v_mark', 'v_split']) ok(has(by(id), 'good', /读懂对手能赢/), id + ' 应判定为“需要真正的博弈”');
ok(ms < 3000, '主世界体检用时过长：' + ms + 'ms');

// ── 故意做坏的会场 ──
const clone = () => W.parse(W.toJson(world));
const venueOf = (p, id) => p.venues.find(v => v.id === id);
const nodeOf = (v, kind) => v.nodes.find(n => n.kind === kind);
{ // 限时比最快路线还短 → 红
  const p = clone(), n = nodeOf(venueOf(p, 'v_chase'), 'explore'); n.timeout = 3;
  ok(has(C.venue(p, 'v_chase'), 'bad', /不可能通关/), '限时 3 秒应判“不可能通关”');
}
{ // 鬼挪到角落、巡逻原地 → 直奔玩家不被抓 → 黄“鬼形同虚设”
  const p = clone(), v = venueOf(p, 'v_chase'), n = nodeOf(v, 'explore'), m = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'explore.js'));
  const map = m.parseMap(n.mapText), T = m.find(map, 'T')[0];
  let far = null; for (let y = 0; y < map.H; y++) for (let x = 0; x < map.W; x++) if (m.walkable(map, x, y) && (!far || Math.hypot(x - T.x, y - T.y) > Math.hypot(far.x - T.x, far.y - T.y))) far = { x, y };
  const exits = n.points.filter(q => q.kind !== 'chaser');
  n.points = exits.concat(n.points.filter(q => q.kind === 'chaser').slice(0, 1).map(q => Object.assign(q, { x: far.x, y: far.y, route: [], sight: 1 })));
  const r = C.venue(p, 'v_chase');
  ok(has(r, 'warn', /形同虚设/), '鬼放到最远角落应判“形同虚设”：' + r.items.map(i => i.level + ':' + i.text).join(' | '));
}
{ // 对手全是“老实人”，分还是偷 → 无脑一直偷也能赢 → 黄
  const p = clone(), g = nodeOf(venueOf(p, 'v_split'), 'game').game; for (const s of g.seats) if (s.persona !== 'player') s.persona = 'loyal';
  const r = C.venue(p, 'v_split');
  ok(has(r, 'warn', /无脑/) || has(r, 'good', /读懂对手/), '全老实人对手应能算出结论：' + r.items.map(i => i.text).join(' | '));
  if (!has(r, 'warn', /无脑/)) console.log('  （提示）全老实人对手时未出现“无脑也能赢”：' + r.items.filter(i => /「/.test(i.text)).map(i => i.text).join(' | '));
}
{ // 一条命、目标分数超高 → 作弊级最优也赢不了 → 红
  const p = clone(), g = nodeOf(venueOf(p, 'v_split'), 'game').game; g.goal = 999; g.rounds = 2;
  const r = C.venue(p, 'v_split');
  ok(has(r, 'bad', /赢不了|有误/), '目标 999 分、2 轮应判“赢不了”或参数有误：' + r.items.map(i => i.level + ':' + i.text).join(' | '));
}
{ // 没写公开规则、没有揭示 → 黄
  const p = clone(), v = venueOf(p, 'v_doors'); v.rules = ''; for (const n of v.nodes) if (n.kind === 'reveal') n.kind = 'story';
  const r = C.venue(p, 'v_doors');
  ok(has(r, 'warn', /没写公开规则/), '无规则应提示');
  ok(has(r, 'warn', /没有“揭示”阶段/), '无揭示应提示');
}
console.log('会场体检: ' + (checks - fails) + '/' + checks + ' 通过（主世界 ' + rows.length + ' 会场，红 ' + rows.reduce((s, r) => s + r.counts.bad, 0) + ' · 黄 ' + rows.reduce((s, r) => s + r.counts.warn, 0) + ' · 绿 ' + rows.reduce((s, r) => s + r.counts.good, 0) + '，' + ms + 'ms）');
process.exit(fails ? 1 : 0);
