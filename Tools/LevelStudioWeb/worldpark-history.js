'use strict';
// S256：世界工坊“安全网”的纯逻辑——改动描述、和内置原版对比、撤销历史、把点挪到最近的空地、检查结果的人话解释。
// 不碰 DOM、不读写 localStorage、不改规则：网页 worldpark-app.js 与 Node 测试（worldpark-history.test.js）共用。
// 地图可走 / 可达的判定不在这里重写：一律调用传进来的 WorldParkSpatial（与画布、Unity 画布同一套规则）。
const WPHistory = (() => {
  const FIELD = {
    world: { name:'世界名称', id:'世界编号', premise:'世界设定', entryVenue:'首个会场', version:'版本' },
    venue: { name:'会场名称', id:'会场编号', suit:'花色 / 分类', difficulty:'难度', setting:'空间设定', role:'玩家角色', rules:'公开规则', entry:'入口阶段' },
    node: { name:'阶段名称', id:'阶段编号', kind:'阶段类型', text:'玩家可见的文字', next:'下一阶段', onWin:'胜利后', onLose:'失败后', result:'结局标签', room:'关联房间', mapText:'地图', timeout:'限时', game:'博弈参数' },
    clue: { name:'线索名称', id:'线索编号', trigger:'发现方式', foundAt:'首次观察阶段', revealAt:'重新解释阶段', hint:'玩家先能看见的事实', truth:'作者真相' },
    point: { label:'名称', id:'编号', kind:'类型', clue:'关联线索', outcome:'出口结果', x:'位置', y:'位置', requires:'需要的线索', speed:'速度', sight:'视距', route:'巡逻路线' },
  };
  const POINT_KIND = { clue:'调查点', exit:'出口', chaser:'鬼' };
  const blankish = v => v === undefined || v === null || v === '' || (Array.isArray(v) && !v.length);
  const same = (a, b) => (blankish(a) && blankish(b)) || JSON.stringify(a) === JSON.stringify(b);
  const nameOf = x => String((x && (x.name || x.label || x.id)) || '未命名');
  const pointName = p => `${POINT_KIND[p && p.kind] || '点位'}「${nameOf(p)}」`;

  // 两张列表配对：先按 id；剩下的按原位置配（这样改了编号也算“同一个东西被改了”，不会变成“删一个、加一个”）。
  function pair(a, b) {
    a = Array.isArray(a) ? a : []; b = Array.isArray(b) ? b : [];
    const pairs = [], usedA = new Set(), usedB = new Set();
    b.forEach((y, j) => { const i = a.findIndex((x, k) => !usedA.has(k) && x && y && x.id === y.id); if (i >= 0) { pairs.push([a[i], y]); usedA.add(i); usedB.add(j); } });
    b.forEach((y, j) => { if (!usedB.has(j) && j < a.length && !usedA.has(j)) { pairs.push([a[j], y]); usedA.add(j); usedB.add(j); } });
    return { pairs, added: b.filter((_, j) => !usedB.has(j)), removed: a.filter((_, i) => !usedA.has(i)) };
  }
  function fieldChanges(kind, a, b, skip) {
    const out = [];
    for (const k of Object.keys(FIELD[kind])) {
      if (skip && skip.includes(k)) continue;
      if (!same(a && a[k], b && b[k]) && !out.includes(FIELD[kind][k])) out.push(FIELD[kind][k]);
    }
    return out;
  }
  function pointChanges(a, b) {
    const out = [], r = pair(a, b);
    for (const p of r.added) out.push(`新增${pointName(p)}`);
    for (const p of r.removed) out.push(`删除${pointName(p)}`);
    for (const [x, y] of r.pairs) {
      const f = fieldChanges('point', x, y);
      if (!f.length) continue;
      if (f.length === 1 && f[0] === '位置') out.push(`${pointName(y)}挪到 (${y.x}, ${y.y})`);
      else out.push(`${pointName(y)}：${f.join('、')}`);
    }
    return out;
  }
  // 一个阶段里改了什么（不带会场名前缀）。
  function nodeChanges(a, b) {
    const out = [], f = fieldChanges('node', a, b);
    if (f.length) out.push(f.join('、'));
    return out.concat(pointChanges(a && a.points, b && b.points));
  }
  // 一个会场里改了什么：返回“阶段名：改了什么”式的短句列表。skip 用于和模板比时忽略编号与名称。
  function venueChanges(a, b, skip) {
    const out = [], f = fieldChanges('venue', a, b, skip);
    if (f.length) out.push('会场设定：' + f.join('、'));
    const nodes = pair(a && a.nodes, b && b.nodes);
    for (const n of nodes.added) out.push(`新增阶段「${nameOf(n)}」`);
    for (const n of nodes.removed) out.push(`删除阶段「${nameOf(n)}」`);
    for (const [x, y] of nodes.pairs) for (const c of nodeChanges(x, y)) out.push(`${nameOf(y)}：${c}`);
    const clues = pair(a && a.clues, b && b.clues);
    for (const c of clues.added) out.push(`新增线索「${nameOf(c)}」`);
    for (const c of clues.removed) out.push(`删除线索「${nameOf(c)}」`);
    for (const [x, y] of clues.pairs) { const g = fieldChanges('clue', x, y); if (g.length) out.push(`线索「${nameOf(y)}」：${g.join('、')}`); }
    return out;
  }
  function linkName(l, venues) { const vn = id => nameOf((venues || []).find(v => v && v.id === id) || { id }); return `${vn(l && l.from)} → ${vn(l && l.to)}`; }
  // 整个世界前后对比：给“改动记录”和“刚才改了什么”用。
  function describe(oldPack, newPack) {
    const out = [];
    if (!oldPack || !newPack) return ['整个世界'];
    const f = fieldChanges('world', oldPack, newPack);
    if (f.length) out.push('世界：' + f.join('、'));
    const venues = pair(oldPack.venues, newPack.venues);
    for (const v of venues.added) out.push(`新增会场「${nameOf(v)}」`);
    for (const v of venues.removed) out.push(`删除会场「${nameOf(v)}」`);
    for (const [x, y] of venues.pairs) for (const c of venueChanges(x, y)) out.push(`${nameOf(y)} · ${c}`);
    const links = pair((oldPack.links || []).map((l, i) => Object.assign({ id: '#' + i }, l)), (newPack.links || []).map((l, i) => Object.assign({ id: '#' + i }, l)));
    for (const l of links.added) out.push(`新增入口 ${linkName(l, newPack.venues)}`);
    for (const l of links.removed) out.push(`删除入口 ${linkName(l, oldPack.venues)}`);
    for (const [x, y] of links.pairs) if (!same(x.from, y.from) || !same(x.to, y.to) || !same(x.requires, y.requires)) out.push(`入口 ${linkName(y, newPack.venues)}：${same(x.requires, y.requires) ? '来源 / 目的' : '条件 ' + (x.requires || '空') + ' → ' + (y.requires || '空')}`);
    return out;
  }
  function summarize(list, max = 2) {
    if (!list || !list.length) return '没有实际变化';
    return list.slice(0, max).join('；') + (list.length > max ? `；等 ${list.length} 处` : '');
  }

  // 内置原版：同 id 的会场；模板副本（xxx_my、xxx_copy、xxx_my_2…）回到它的模板。
  function originOf(venue, samplePack) {
    const list = (samplePack && samplePack.venues) || [];
    if (!venue) return null;
    const exact = list.find(v => v.id === venue.id);
    if (exact) return { venue: exact, kind: '原版' };
    let id = String(venue.id || '');
    for (let i = 0; i < 4; i++) {
      const base = id.replace(/_(my|copy)(_\d+)?$/, '');
      if (base === id) break;
      id = base;
      const t = list.find(v => v.id === id);
      if (t) return { venue: t, kind: '模板' };
    }
    return null;
  }
  // 和原版比：模板副本不比较编号和名称（它们本来就该不同）。
  function compareToOrigin(venue, samplePack) {
    const o = originOf(venue, samplePack);
    if (!o) return null;
    return { kind: o.kind, origin: o.venue, changes: venueChanges(o.venue, venue, o.kind === '模板' ? ['id', 'name'] : null) };
  }

  // 撤销历史：每条 = 改动前后两份完整世界 JSON + 一句人话。连续打字（同一句描述、2.5 秒内）合并成一条。
  class History {
    constructor(limit = 200) { this.undoStack = []; this.redoStack = []; this.limit = limit; }
    record(before, after, label, place, now = Date.now(), mergeMs = 2500) {
      if (typeof before !== 'string' || typeof after !== 'string' || before === after) return null;
      const top = this.undoStack[this.undoStack.length - 1];
      this.redoStack = [];
      if (top && top.label === label && top.after === before && now - top.time <= mergeMs && !top.sealed) {
        top.after = after; top.time = now; if (place) top.place = place;
        if (top.before === top.after) this.undoStack.pop();
        return top;
      }
      const e = { before, after, label: label || '改动', place: place || null, time: now };
      this.undoStack.push(e);
      while (this.undoStack.length > this.limit) this.undoStack.shift();
      return e;
    }
    // 下一条改动不再并进上一条（撤销 / 恢复原版 / 一键修复之后调用）。
    seal() { const top = this.undoStack[this.undoStack.length - 1]; if (top) top.sealed = true; }
    undo() { const e = this.undoStack.pop(); if (!e) return null; e.sealed = true; this.redoStack.push(e); return e; }
    redo() { const e = this.redoStack.pop(); if (!e) return null; this.undoStack.push(e); return e; }
    get canUndo() { return this.undoStack.length > 0; }
    get canRedo() { return this.redoStack.length > 0; }
    // 最新在前：[{label,time,place}]
    list() { return this.undoStack.slice().reverse().map(e => ({ label: e.label, time: e.time, place: e.place })); }
    // 连续撤销 count 条，返回最后一条（调用方把世界换成它的 before）。
    undoMany(count) { let e = null; for (let i = 0; i < count; i++) { const x = this.undo(); if (!x) break; e = x; } return e; }
    // 存进浏览器：只留最新、总字符数不超过 maxChars 的那几条。
    serialize(maxChars = 1500000) {
      const keep = []; let size = 0;
      for (let i = this.undoStack.length - 1; i >= 0; i--) {
        const e = this.undoStack[i], s = e.before.length + e.after.length + 200;
        if (size + s > maxChars) break;
        size += s; keep.unshift({ before: e.before, after: e.after, label: e.label, place: e.place, time: e.time });
      }
      return { v: 1, undo: keep };
    }
    // 只有最后一条的 after 等于当前草稿时才接上（否则说明草稿在别处被改过，旧历史不可信，丢弃）。
    static restore(data, currentJson, limit = 200) {
      const h = new History(limit);
      if (!data || data.v !== 1 || !Array.isArray(data.undo) || !data.undo.length) return h;
      const last = data.undo[data.undo.length - 1];
      if (!last || last.after !== currentJson) return h;
      for (const e of data.undo) if (e && typeof e.before === 'string' && typeof e.after === 'string') h.undoStack.push({ before: e.before, after: e.after, label: String(e.label || '改动'), place: e.place || null, time: Number(e.time) || 0, sealed: true });
      return h;
    }
  }

  // 把点挪到最近的空地：只用 WorldParkSpatial 的同一套“可走 + 从 T 走得到”规则。
  function reachableCells(spatial, mapText) {
    const shape = spatial.mapShape(mapText), spawns = [];
    for (let y = 0; y < shape.height; y++) for (let x = 0; x < shape.width; x++) if (shape.at(x, y) === 'T') spawns.push([x, y]);
    if (spawns.length !== 1) return { ok: false, reason: `地图需要且只能有一个 T 出生点（现在 ${spawns.length} 个）`, cells: [], spawn: null };
    const probe = spatial.placement(shape, spawns[0][0], spawns[0][1]);
    if (!probe.ok) return { ok: false, reason: probe.reason, cells: [], spawn: null };
    const seen = new Set([spawns[0].join(',')]), queue = [spawns[0]];
    for (let i = 0; i < queue.length; i++) {
      const [cx, cy] = queue[i];
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = cx + dx, ny = cy + dy, k = nx + ',' + ny;
        if (!seen.has(k) && shape.isWalkable(nx, ny)) { seen.add(k); queue.push([nx, ny]); }
      }
    }
    return { ok: true, reason: '', cells: queue, spawn: spawns[0] };
  }
  function nearestCell(reach, x, y, taken, minSpawnDist2 = 1) {
    let best = null, bestD = Infinity;
    for (const [cx, cy] of reach.cells) {
      if (taken && taken.has(cx + ',' + cy)) continue;
      const sx = cx - reach.spawn[0], sy = cy - reach.spawn[1];
      if (sx * sx + sy * sy < minSpawnDist2) continue;
      const d = (cx - x) * (cx - x) + (cy - y) * (cy - y);
      if (d < bestD) { bestD = d; best = { x: cx, y: cy }; }
    }
    return best;
  }
  // 返回 { ok, reason, moves:[{name,from,to}], stuck:[name] }；会直接改 node.points（调用方负责记入撤销历史）。
  function fixPoints(spatial, node) {
    const res = { ok: true, reason: '', moves: [], stuck: [] };
    if (!spatial || !node || node.kind !== 'explore') return Object.assign(res, { ok: false, reason: '只有现场探索阶段才有点位' });
    const reach = reachableCells(spatial, node.mapText);
    if (!reach.ok) return Object.assign(res, { ok: false, reason: '地图本身有问题，先修地图：' + reach.reason });
    const good = new Set(reach.cells.map(c => c.join(','))), points = node.points || [];
    const taken = new Set(points.filter(p => good.has(p.x + ',' + p.y)).map(p => p.x + ',' + p.y));
    taken.add(reach.spawn.join(','));
    for (const p of points) {
      if (!good.has(p.x + ',' + p.y)) {
        const to = nearestCell(reach, p.x, p.y, taken, p.kind === 'chaser' ? 16 : 1) || nearestCell(reach, p.x, p.y, taken, 1);
        if (!to) { res.stuck.push(pointName(p)); continue; }
        res.moves.push({ name: pointName(p), from: [p.x, p.y], to: [to.x, to.y] });
        p.x = to.x; p.y = to.y; taken.add(to.x + ',' + to.y);
      }
      if (p.kind === 'chaser' && Array.isArray(p.route)) {
        let changed = false;
        p.route = p.route.map(cell => {
          const m = /^\s*(-?\d+)\s*,\s*(-?\d+)\s*$/.exec(String(cell));
          if (m && good.has(Number(m[1]) + ',' + Number(m[2]))) return cell;
          const at = m ? nearestCell(reach, Number(m[1]), Number(m[2]), null, 1) : { x: p.x, y: p.y };
          changed = true; return at ? at.x + ',' + at.y : p.x + ',' + p.y;
        });
        if (changed) res.moves.push({ name: pointName(p) + '的巡逻路线', from: null, to: null });
      }
    }
    return res;
  }
  // 当前阶段里哪些点落在走不了 / 走不到的格子上（给结构预检列出具体是谁）。
  function badPoints(spatial, node) {
    if (!spatial || !node || node.kind !== 'explore') return [];
    const shape = spatial.mapShape(node.mapText);
    return (node.points || []).map(p => ({ p, r: spatial.placement(shape, p.x, p.y) })).filter(x => !x.r.ok).map(x => ({ name: pointName(x.p), x: x.p.x, y: x.p.y, tile: shape.at(x.p.x, x.p.y), reason: x.r.reason }));
  }

  // 检查结果的人话解释：what = 出了什么事；how = 怎么修。没登记的代码返回 null（界面显示原文）。
  const EXPLAIN = {
    explore_point_walkable: ['有点位（调查点 / 出口 / 鬼）落在走不了的格子上（墙、树、水……），玩家碰不到它。', '多半是刚换了地图或改了地图文字。点“把点挪到最近的空地”；或在地图上把它拖到空地；或撤销刚才那一步。'],
    explore_point_unreachable: ['点位在空地上，但从出生点 T 走不过去（被墙围住了）。', '点“把点挪到最近的空地”，或在地图上开一条路。'],
    explore_chaser_route: ['鬼的巡逻路线里有走不了或走不到的格子。', '点“把点挪到最近的空地”（会顺手把路线格挪到最近的空地），或在鬼的表单里改路线。'],
    explore_chaser_spawn_close: ['鬼离玩家出生点 T 不到 4 格，一开局就可能被抓。', '把鬼拖远一点。'],
    explore_chaser_limit: ['一个现场最多只能有 4 个鬼。', '删掉多出来的鬼。'],
    explore_chaser_speed: ['鬼的速度超出范围（0 = 默认 34，否则 10–80；玩家是 40）。', '在鬼的表单里改速度。'],
    explore_chaser_sight: ['鬼的视距超出范围（0 = 默认 7 格，否则 1–20）。', '在鬼的表单里改视距。'],
    explore_map_required: ['这个现场探索阶段还没有地图。', '用模板新建一个会场（地图会一起复制），或在“地图文字”里画一张。'],
    explore_map_empty: ['地图里没有任何格子。', '在“地图文字”里画一张，或撤销刚才那一步。'],
    explore_map_rectangular: ['地图每一行的长度不一样。', '把每行补成一样多的字符（空地用 . ）。'],
    explore_map_size: ['地图太小或太大（至少 5×4，最多 192×128）。', '加行 / 加列，或删掉一些。'],
    explore_map_boundary: ['地图最外一圈必须挡路（墙、树等），不然玩家会走出地图。', '把最外一圈改成 t 或 W。'],
    explore_map_tile_unknown: ['地图里有不认识的字符。', '对照指南的地图字符表改掉它。'],
    explore_spawn_unique: ['地图里必须有且只有一个 T（玩家出生点）。', '保留一个 T，删掉多余的；没有就加一个。'],
    explore_exit_required: ['这个现场没有出口，玩家永远走不出去。', '点“＋ 出口”在地图上放一个。'],
    explore_exit_win_required: ['没有任何一个出口通向“胜利”，玩家不可能赢。', '把一个出口的结果设成 win。'],
    explore_exit_outcome: ['出口的结果必须是 win（胜利）或 lose（失败）。', '在出口表单里选一个。'],
    explore_exit_requires_missing: ['出口要求的线索不存在（可能被删了或改了编号）。', '在出口的“需要的线索”里删掉或改对。'],
    explore_exit_requires_invalid: ['出口要求的线索写重复了或有空的。', '在出口的“需要的线索”里整理一下。'],
    explore_point_clue_missing: ['调查点没有连上线索，玩家查看时什么也得不到。', '在调查点表单的“关联线索”里选一条。'],
    explore_point_clue_trigger: ['调查点连的线索不是“在现场查看发现”的类型。', '把那条线索的发现方式改成 interact。'],
    explore_point_clue_stage: ['调查点连的线索，“首次观察阶段”写的不是这个现场。', '把那条线索的首次观察阶段改成这个现场。'],
    explore_point_id_duplicate: ['两个点位用了同一个编号。', '打开“显示高级格子”，改掉其中一个的编号。'],
    explore_timeout_invalid: ['限时必须是 0（不限时）或正整数秒。', '改成一个整数。'],
    node_unreachable: ['这个阶段从会场入口走不到（没有任何分支连到它）。', '在别的阶段的分支里连上它，或者删掉它。'],
    ending_unreachable: ['从会场入口走不到任何结局，玩家会卡在半路。', '检查每个阶段的“下一阶段 / 胜利后 / 失败后”有没有接上。'],
    ending_result_required: ['结局阶段需要一个结局标签（比如 clear、gameover）。', '在结局阶段填一个标签。'],
    ending_result_invalid: ['any 只能用在会场之间的入口上，不能当结局标签。', '换一个标签，比如 clear。'],
    venue_entry_missing: ['会场的入口阶段被删了或改了编号。', '在“本会场入口阶段”里重新选一个。'],
    venue_entry_required: ['会场还没选入口阶段。', '在“本会场入口阶段”里选一个。'],
    venue_nodes_required: ['会场里一个阶段都没有。', '点“添加阶段”，或撤销刚才那一步。'],
    venue_id_duplicate: ['两个会场用了同一个编号。', '打开“显示高级格子”，改掉其中一个的编号。'],
    venue_role_required: ['会场的玩家角色是空的。', '打开“显示高级格子”，选 player / 玩家。'],
    unsupported_role: ['这个玩家角色还没有实现，会场不能运行。', '打开“显示高级格子”，选 player / 玩家。'],
    entry_venue_missing: ['世界的“首个会场”被删了或改了编号。', '在“首个会场”里重新选一个。'],
    entry_venue_required: ['世界还没选“首个会场”。', '在“首个会场”里选一个。'],
    link_from_missing: ['有一扇门的来源会场不存在了。', '在“会场之间的入口”里改好或删掉这一条。'],
    link_to_missing: ['有一扇门通向的会场不存在了。', '在“会场之间的入口”里改好或删掉这一条。'],
    link_requires_required: ['有一扇门没写打开条件。', '填一个结局标签（clear / gameover……），或写 any 表示任何结局都开。'],
    link_result_unreachable: ['有一扇门要求的结局标签，来源会场里没有任何结局会给出，所以这扇门永远打不开。', '把门的条件改成来源会场真有的结局标签，或写 any。'],
    link_duplicate: ['两扇门完全一样。', '删掉一扇。'],
    node_id_duplicate: ['两个阶段用了同一个编号。', '打开“显示高级格子”，改掉其中一个的编号。'],
    node_kind_required: ['有个阶段没选类型。', '在阶段表单里选一个类型。'],
    clue_found_missing: ['线索的“首次观察阶段”不存在了。', '在线索表单里重新选。'],
    clue_found_required: ['线索没选“首次观察阶段”。', '在线索表单里选一个。'],
    clue_reveal_missing: ['线索的“重新解释阶段”不存在了。', '在线索表单里重新选。'],
    clue_reveal_required: ['线索没选“重新解释阶段”。', '在线索表单里选一个揭示阶段。'],
    clue_reveal_kind: ['线索的“重新解释阶段”必须是揭示阶段。', '在线索表单里选一个揭示阶段。'],
    clue_reveal_before_found: ['线索在被发现之前就被揭示了（揭示阶段排在发现阶段前面）。', '让揭示阶段排在发现阶段之后。'],
    clue_interact_explore: ['“在现场查看发现”的线索，首次观察阶段必须是现场探索阶段。', '把首次观察阶段改成一个现场探索阶段，或把发现方式改成 stage。'],
    clue_id_duplicate: ['两条线索用了同一个编号。', '打开“显示高级格子”，改掉其中一个的编号。'],
    clue_discovery_bypass: ['有一条路线可以不发现这条线索就走到揭示，那时这条线索的真相不会显示。', '如果它必须被看到，就让所有路线都经过发现它的阶段；只是“细心才有”的彩蛋就不用管。'],
    clue_same_stage: ['线索在同一个阶段里被发现又被解释，没有“先误会、后反转”的时间差。', '想做反转，就让玩家早一点看到事实，晚一点再解释。'],
    room_missing: ['守宝房阶段引用的横版房间找不到（旧系统）。', '建议把阶段类型改成现场探索或剧情。'],
    room_required: ['守宝房阶段没填房间名（旧系统）。', '建议把阶段类型改成现场探索或剧情。'],
    room_existence_unchecked: ['无法确认这个横版房间是否存在（旧系统）。', '建议把阶段类型改成现场探索或剧情。'],
    unsupported_concept: ['这是“新玩法草稿”阶段，只能保存想法，还不能玩。', '告诉 AI 你想要的玩法；现在先用剧情或现场探索代替。'],
    unsupported_node_kind: ['这个阶段类型还没有实现。', '换成已有的类型。'],
    venue_difficulty_invalid: ['难度不能小于 0。', '改成 0 或正整数。'],
    id_key_invalid: ['编号里不能有斜杠或特殊字符（名称随便写）。', '打开“显示高级格子”，把编号改成字母数字。'],
    pack_id_required: ['世界没有编号。', '打开“显示高级格子”，填一个世界编号。'],
  };
  function explain(code) {
    if (EXPLAIN[code]) return { what: EXPLAIN[code][0], how: EXPLAIN[code][1] };
    code = String(code || '');
    if (/_null$/.test(code)) return { what: '世界里有一个空的条目（多半是手改世界 JSON 留下的）。', how: '撤销刚才那一步，或在“高级：查看 / 编辑世界JSON”里删掉它。' };
    if (/_id_required$/.test(code)) return { what: '有东西的编号是空的。', how: '打开“显示高级格子”，给它填一个编号（字母数字即可）。' };
    if (code === 'link_from_required' || code === 'link_to_required') return { what: '有一扇门没选来源或目的会场。', how: '在“会场之间的入口”里选好，或删掉这一条。' };
    if (code === 'clue_trigger_invalid') return { what: '线索的发现方式只能是“进入阶段自动发现”或“在现场查看发现”。', how: '在线索表单里选一个。' };
    if (code === 'explore_point_kind') return { what: '点位的类型只能是调查点、出口或鬼。', how: '在点位表单里选一个类型。' };
    if (code === 'version_invalid') return { what: '世界包的版本号不对（只能是 1）。', how: '多半是导入了别的文件；撤销刚才那一步。' };
    if (/^game_/.test(code)) return { what: '博弈参数里有一项超出范围或写错了（具体见下面的原文）。', how: '在“博弈参数”里按原文提示改，或撤销刚才那一步。' };
    if (/^node_\w+_(missing|required)$/.test(code) || code === 'node_next') return { what: '这个阶段的分支（下一阶段 / 胜利后 / 失败后）没接好，剧情会断在这里。', how: '在阶段表单里把分支选好。' };
    return null;
  }
  // 哪些代码可以用“把点挪到最近的空地”一键修。
  const POINT_FIX = new Set(['explore_point_walkable', 'explore_point_unreachable', 'explore_chaser_route']);

  return Object.freeze({ FIELD, describe, summarize, venueChanges, nodeChanges, originOf, compareToOrigin, History, fixPoints, badPoints, reachableCells, explain, POINT_FIX });
})();
if (typeof module !== 'undefined') module.exports = WPHistory;
