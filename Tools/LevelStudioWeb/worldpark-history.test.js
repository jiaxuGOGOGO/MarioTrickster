'use strict';
// S256：世界工坊安全网纯逻辑测试（node Tools/LevelStudioWeb/worldpark-history.test.js）。
// 覆盖：改动描述、和内置原版 / 模板对比、撤销历史（合并、撤销、重做、上限、存取）、把点挪到最近的空地、每个检查代码都有人话解释。
const fs = require('fs'), path = require('path'), vm = require('vm');
const H = require('./worldpark-history.js');
const S = require('./worldpark-spatial.js');
const root = path.resolve(__dirname, '..', '..');
const web = f => fs.readFileSync(path.join(__dirname, f), 'utf8').replace(/if \(typeof module[^\n]*\n?/g, '');
const ctx = { console }; vm.createContext(ctx);
for (const f of ['borderland.js', 'worldpark.js']) vm.runInContext(web(f), ctx, { filename: f });
const WorldPark = vm.runInContext('WorldPark', ctx);
const sampleText = fs.readFileSync(path.join(root, 'Assets', 'Resources', 'WorldPark', 'Borderland.worldpark.json'), 'utf8').replace(/^\uFEFF/, '');
const sample = JSON.parse(sampleText);
const clone = x => JSON.parse(JSON.stringify(x));
let n = 0, fail = 0;
const ok = (c, m) => { n++; if (!c) { fail++; console.log('FAIL ' + m); } };

// ---------- 改动描述 ----------
const K = sample.venues.find(v => v.id === 'v_final'), room = K.nodes.find(x => x.id === 'room');
const town = 'tttttttttttt\nt..........t\nt.WWWWWWWW.t\nt.WWWWWWWW.t\nt..........t\nt....T.....t\ntttttttttttt';
{
  const b = clone(sample); b.venues.find(v => v.id === 'v_final').nodes.find(x => x.id === 'room').mapText = town;
  const d = H.describe(sample, b);
  ok(d.length === 1 && d[0] === `${K.name} · ${room.name}：地图`, '换地图描述为“会场 · 阶段：地图”：' + d.join(' | '));
  ok(H.summarize(d) === d[0], '一处改动的摘要就是它本身');
}
{
  const b = clone(sample); b.venues[0].name = '新名字';
  ok(H.describe(sample, b)[0] === '新名字 · 会场设定：会场名称', '改会场名称：' + H.describe(sample, b)[0]);
  const c = clone(sample); c.venues[0].id = 'renamed';
  ok(H.describe(sample, c).length === 1 && /会场编号/.test(H.describe(sample, c)[0]), '改会场编号仍是“同一个会场被改了”，不是删一个加一个');
}
{
  const b = clone(sample); const v = clone(K); v.id = 'v_final_my'; v.name = 'K（我的改编）'; b.venues.push(v);
  ok(H.describe(sample, b)[0] === '新增会场「K（我的改编）」', '新增会场');
  const c = clone(sample); c.venues.splice(1, 1);
  ok(H.describe(sample, c).some(x => x.startsWith('删除会场「')), '删除会场');
}
{
  const b = clone(sample); const p = b.venues.find(v => v.id === 'v_final').nodes.find(x => x.id === 'room').points[0]; p.x = 3; p.y = 9;
  const d = H.describe(sample, b);
  ok(d.length === 1 && d[0].endsWith(`调查点「${p.label}」挪到 (3, 9)`), '挪动点位：' + d.join(' | '));
  const c = clone(sample); c.venues.find(v => v.id === 'v_final').nodes.find(x => x.id === 'room').points.push({ id: 'oni', kind: 'chaser', label: '鬼', x: 15, y: 2, route: [] });
  ok(H.describe(sample, c)[0].endsWith('新增鬼「鬼」'), '新增鬼');
  const e = clone(sample); e.links[0].requires = 'any';
  ok(/^入口 .+ → .+：条件 .+ → any$/.test(H.describe(sample, e)[0]), '改门的条件：' + H.describe(sample, e)[0]);
  const f = clone(sample); f.venues[0].clues[0].truth = '改了';
  ok(/线索「.+」：作者真相$/.test(H.describe(sample, f)[0]), '改线索真相');
  ok(H.summarize(['a', 'b', 'c']) === 'a；b；等 3 处', '多处改动的摘要');
  ok(H.describe(sample, clone(sample)).length === 0, '没改就没有描述');
}

// ---------- 和内置原版 / 模板对比 ----------
{
  const cur = clone(K);
  const r0 = H.compareToOrigin(cur, sample);
  ok(r0 && r0.kind === '原版' && r0.changes.length === 0, '未改动的主世界会场：和原版相同');
  cur.nodes.find(x => x.id === 'room').mapText = town;
  const r1 = H.compareToOrigin(cur, sample);
  ok(r1.changes.length === 1 && r1.changes[0] === `${room.name}：地图`, '换了地图：和原版相比 1 处：' + r1.changes.join(' | '));
  const mine = clone(K); mine.id = 'v_final_my_2'; mine.name = 'K（我的改编）';
  const r2 = H.compareToOrigin(mine, sample);
  ok(r2 && r2.kind === '模板' && r2.origin.id === 'v_final' && r2.changes.length === 0, '模板副本：找到模板，编号和名称不算改动');
  ok(H.compareToOrigin({ id: 'venue_3', nodes: [] }, sample) === null, '全新空白会场没有原版');
}

// ---------- 撤销历史 ----------
{
  const h = new H.History(5);
  ok(h.record('a', 'a', 'x') === null && !h.canUndo, '没变化不记');
  h.record('a', 'b', '会场名称', null, 1000); h.record('b', 'c', '会场名称', null, 2000);
  ok(h.undoStack.length === 1 && h.undoStack[0].before === 'a' && h.undoStack[0].after === 'c', '连续打字合并成一条');
  h.record('c', 'd', '会场名称', null, 9000);
  ok(h.undoStack.length === 2, '停顿超过 2.5 秒另起一条');
  h.record('d', 'e', '地图', null, 9100);
  ok(h.undoStack.length === 3, '换了改动对象另起一条');
  let e = h.undo(); ok(e.before === 'd' && h.canRedo, '撤销回到改动前');
  e = h.redo(); ok(e.after === 'e', '重做回到改动后');
  h.undo(); h.record('d', 'f', '地图', null, 9200);
  ok(!h.canRedo && h.undoStack.length === 3 && h.undoStack[2].before === 'd', '撤销后再改：重做清空，且不并进已撤销的那条');
  e = h.undoMany(3); ok(e.before === 'a' && !h.canUndo, '一次退回多步');
  const g = new H.History(3); for (let i = 0; i < 6; i++) g.record('s' + i, 's' + (i + 1), 'L' + i, null, i * 10000);
  ok(g.undoStack.length === 3 && g.undoStack[0].before === 's3', '超过上限丢最旧的');
  ok(g.list()[0].label === 'L5', '改动记录最新在前');
  g.seal(); g.record('s6', 's7', 'L5', null, 50001);
  ok(g.undoStack.length === 3 && g.undoStack[2].before === 's6', 'seal 之后同名改动也另起一条');
  const saved = JSON.parse(JSON.stringify(g.serialize()));
  const back = H.History.restore(saved, 's7');
  ok(back.undoStack.length === 3 && back.undo().before === 's6', '存进浏览器再读回，能继续撤销');
  ok(H.History.restore(saved, '别的草稿').undoStack.length === 0, '草稿已在别处改过：旧历史作废，不会撤错');
  const big = new H.History(); for (let i = 0; i < 5; i++) big.record('x'.repeat(1000) + i, 'x'.repeat(1000) + (i + 1), 'L' + i, null, i * 10000);
  ok(big.serialize(4500).undo.length === 2, '存储有大小上限，只留最新的几条');
}

// ---------- 把点挪到最近的空地（与画布同一套规则） ----------
{
  // y=0 在最下面一行；T 在 (2,2)；(3,3) 是墙。
  const map = 'tttttttttt\nt..W.....t\nt.T......t\nt........t\ntttttttttt';
  const node = { kind: 'explore', mapText: map, points: [
    { id: 'c1', kind: 'clue', label: '名单', x: 3, y: 3 },
    { id: 'e1', kind: 'exit', label: '门', x: 8, y: 1 },
    { id: 'o1', kind: 'chaser', label: '鬼', x: 0, y: 0, route: ['3,3', '7,2'] },
  ] };
  const bad = H.badPoints(S, node);
  ok(bad.length === 2 && bad[0].name === '调查点「名单」' && bad[0].tile === 'W', '列出落在墙里的点：' + bad.map(b => b.name + b.tile).join(','));
  const r = H.fixPoints(S, node);
  ok(r.ok && !r.stuck.length, '一键挪点成功');
  ok(node.points.every(p => S.canPlace(map, p.x, p.y).ok), '挪完每个点都能放（可走且从 T 走得到）');
  const c1 = node.points[0]; ok(Math.abs(c1.x - 3) + Math.abs(c1.y - 3) === 1, '调查点只挪到紧挨着的空地');
  ok(node.points[1].x === 8 && node.points[1].y === 1, '本来就合法的点不动');
  const o = node.points[2]; ok((o.x - 2) ** 2 + (o.y - 2) ** 2 >= 16, '鬼挪到离出生点至少 4 格的地方');
  ok(o.route[0] !== '3,3' && o.route[1] === '7,2' && o.route.every(c => S.canPlace(map, ...c.split(',').map(Number)).ok), '巡逻路线里的墙格也挪到空地');
  ok(new Set(node.points.map(p => p.x + ',' + p.y)).size === 3, '挪完的点不叠在一起');
  const noT = H.fixPoints(S, { kind: 'explore', mapText: 'ttttt\nt...t\nt...t\nttttt', points: [] });
  ok(!noT.ok && /T 出生点/.test(noT.reason), '地图没有 T 时不乱挪，说明原因');
}
// 用真实世界复现用户遇到的情况：K 的钟楼控制室换成一张别的地图 → 结构预检报错 → 一键挪点 → 不再报错。
{
  const b = clone(sample), n2 = b.venues.find(v => v.id === 'v_final').nodes.find(x => x.id === 'room');
  n2.mapText = '# 换来的地图\ntttttttttttttttttttt\nt..................t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt.WWWWWWWWWWWWWWWW.t\nt..................t\nt........T.........t\ntttttttttttttttttttt';
  const before = WorldPark.validate(WorldPark.parse(JSON.stringify(b)), () => true).filter(i => i.severity === 'error');
  ok(before.some(i => i.code === 'explore_point_walkable' && i.venue === 'v_final' && i.node === 'room'), '复现：换地图后报“点须在可通行格”');
  const r = H.fixPoints(S, n2);
  const after = WorldPark.validate(WorldPark.parse(JSON.stringify(b)), () => true).filter(i => i.severity === 'error');
  ok(r.ok && r.moves.length >= 1 && !after.length, `一键挪点后结构预检没有错误（挪了 ${r.moves.length} 个）`);
  const restored = clone(b); const vv = restored.venues.find(v => v.id === 'v_final'); vv.nodes[vv.nodes.findIndex(x => x.id === 'room')] = clone(room);
  ok(H.compareToOrigin(vv, sample).changes.length === 0, '只恢复这个阶段的原版后，会场和原版完全一致');
}

// ---------- 每个检查代码都有人话解释 ----------
{
  const src = fs.readFileSync(path.join(__dirname, 'worldpark.js'), 'utf8') + fs.readFileSync(path.join(__dirname, 'borderland.js'), 'utf8');
  const codes = new Set();
  for (const m of src.matchAll(/'([a-z]+(?:_[a-z]+)+)'/g)) if (/(_required|_missing|_invalid|_duplicate|_null|_unreachable|_walkable|_limit|_route|_sight|_speed|_close|_boundary|_empty|_rectangular|_size|_unknown|_unique|_outcome|_kind|_trigger|_stage|_bypass|_found|_unchecked|_concept|_role|_seat|_deceiver)$/.test(m[1]) && !/^(stage|interact)$/.test(m[1])) codes.add(m[1]);
  codes.add('node_next_missing'); codes.add('node_next_required');
  const missing = [...codes].filter(c => !H.explain(c));
  ok(codes.size > 60 && !missing.length, `每个检查代码都有人话解释（${codes.size} 个）` + (missing.length ? '；缺：' + missing.join(',') : ''));
  ok(H.explain('explore_point_walkable').how.includes('把点挪到最近的空地') && H.POINT_FIX.has('explore_point_walkable'), '点位落墙的解释给出一键修法');
  ok(H.explain('不存在的代码') === null, '没登记的代码返回 null（界面显示原文）');
}

console.log(fail ? `worldpark-history: ${fail}/${n} FAILED` : `worldpark-history: ${n} assertions passed`);
process.exit(fail ? 1 : 0);
