// ── S254 会场体检：作者每改一处就能看到“这个会场好不好玩”的数字反馈（设计建议，不是规则）。
//    只读世界包，绝不改写；复用与 Unity 同语义的内核（worldpark.js / explore.js / borderland.js）真实跑：
//    · 现场探索：按格子算最快通关路线与“看完全部线索”路线，对比限时；再让一个“直奔出口、不理鬼”的玩家真实跑一遍，
//      看会不会被鬼抓——被抓 = 追逐有威胁（好），不被抓 = 鬼形同虚设。
//    · 多人博弈：固定出法（无脑）能不能赢、“每轮都看穿对手”的最优出法能不能赢——前者赢 = 太简单，后者也输 = 可能无解。
//    · 会场骨架：亮灯入场公开规则 → 游戏 → 反转 → 结局（参考原作结构，见 docs/step1/S254_*）。
//    体验的最终验收仍是 Tools/WorldParkVerify/Story 剧情带入模拟和真人试玩。
const WPCheck = (() => {
  'use strict';
  const WP = typeof WorldPark !== 'undefined' ? WorldPark : require('./worldpark.js');
  const EX = typeof WPExplore !== 'undefined' ? WPExplore : require('./explore.js');
  const BG = typeof BorderlandGame !== 'undefined' ? BorderlandGame : require('./borderland.js');
  const r1 = x => Math.round(x * 10) / 10;
  const label = p => (p && (p.label || p.id)) || '?';
  const ReadCharsPerSecond = 6; // 中文仔细阅读约每分钟 300–500 字；取偏慢值，因为玩家边读边想

  // 4 邻格 BFS 步数（与内核同一可通行判定）；结果按“格”计，玩家速度 EX.K.Speed 格/秒。
  function bfs(m, from) {
    const w = m.W, h = m.H, dist = new Int32Array(w * h).fill(-1), q = [];
    if (!EX.walkable(m, from.x, from.y)) return { dist, w };
    dist[from.y * w + from.x] = 0; q.push(from.y * w + from.x);
    for (let qi = 0; qi < q.length; qi++) {
      const i = q[qi], x = i % w, y = (i / w) | 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const nx = x + dx, ny = y + dy; if (!EX.walkable(m, nx, ny)) continue;
        const j = ny * w + nx; if (dist[j] >= 0) continue; dist[j] = dist[i] + 1; q.push(j);
      }
    }
    return { dist, w };
  }
  function stepPath(m, a, b) { // 最短 4 邻格路径（含起终点）
    const { dist, w } = bfs(m, b); if (dist[a.y * w + a.x] < 0) return null;
    const out = [{ x: a.x, y: a.y }]; let cur = { x: a.x, y: a.y };
    while (!(cur.x === b.x && cur.y === b.y)) {
      const d = dist[cur.y * w + cur.x];
      let next = null;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) { const nx = cur.x + dx, ny = cur.y + dy; if (EX.walkable(m, nx, ny) && dist[ny * w + nx] === d - 1) { next = { x: nx, y: ny }; break; } }
      if (!next) return null; out.push(next); cur = next;
    }
    return out;
  }
  function permutations(list) { if (list.length <= 1) return [list.slice()]; const out = []; list.forEach((x, i) => { for (const rest of permutations(list.slice(0, i).concat(list.slice(i + 1)))) out.push([x].concat(rest)); }); return out; }
  // 从 start 经过 stops（任意顺序）到 end 的最少步数与顺序；stops ≤ 6 时穷举，否则就近贪心。
  function tour(m, start, stops, end) {
    const keys = [start].concat(stops, [end]), D = keys.map(k => bfs(m, k));
    const d = (i, j) => { const v = D[i].dist[keys[j].y * D[i].w + keys[j].x]; return v < 0 ? Infinity : v; };
    const idx = stops.map((_, i) => i + 1), last = keys.length - 1;
    let best = { steps: Infinity, order: [] };
    const orders = idx.length <= 6 ? permutations(idx) : [(() => { const left = idx.slice(), o = []; let at = 0; while (left.length) { left.sort((a, b) => d(at, a) - d(at, b)); at = left.shift(); o.push(at); } return o; })()];
    for (const o of orders) { let s = 0, at = 0; for (const k of o) { s += d(at, k); at = k; } s += d(at, last); if (s < best.steps) best = { steps: s, order: o.map(k => keys[k]) }; }
    return best;
  }

  function sessionAt(pack, v, n) { // 设计期分析：直接把会话放到该阶段；入场即得（stage）的线索视为已得
    const copy = WP.parse(WP.toJson(pack)); copy.entryVenue = v.id;
    const s = WP.start(copy); s.nodeId = n.id;
    for (const c of s.Venue.clues || []) if (c && c.trigger !== 'interact') s.found.add(v.id + '/' + c.id);
    return s;
  }

  // “直奔玩家”：按最快路线走（先拿出口要求的线索），完全不理会鬼；返回真实内核里的结果。
  function rusher(pack, v, n, route, stopIds) {
    const s = sessionAt(pack, v, n), made = EX.create(s);
    if (!made.run) return { error: made.error };
    const run = made.run, dt = 0.05, limit = (n.timeout > 0 ? n.timeout : 240) + 1;
    let wi = 1;
    for (let t = 0; t < limit && !run.Ended; t += dt) {
      if (wi >= route.length) break;
      const c = route[wi], tx = c.x + 0.5, ty = c.y + 0.5, dx = tx - run.x, dy = ty - run.y, dist = Math.hypot(dx, dy);
      if (dist < 0.12) {
        const stop = stopIds.get(c.x + ',' + c.y);
        if (stop) run.Interact(stop);
        wi++; continue;
      }
      run.Tick(dt, dx / dist, dy / dist);
    }
    return { ended: run.Ended, won: run.Won, caughtBy: run.CaughtBy || '', time: r1(run.Elapsed), message: run.LastMessage };
  }

  function exploreReport(pack, v, n, add) {
    const where = '「' + (n.name || n.id) + '」';
    let m; try { m = EX.parseMap(n.mapText); } catch (e) { add('bad', where + '地图无法读取：' + e.message); return; }
    const spawns = EX.find(m, 'T'); if (spawns.length !== 1) { add('bad', where + '地图需要且只能有一个 T 出生点'); return; }
    const T = spawns[0], pts = n.points || [];
    const clues = pts.filter(p => p && p.kind === 'clue'), exits = pts.filter(p => p && p.kind === 'exit'), chasers = pts.filter(p => p && p.kind === 'chaser');
    const fromT = bfs(m, T), reach = p => fromT.dist[p.y * fromT.w + p.x] >= 0;
    for (const p of clues.concat(exits)) if (!reach(p)) add('bad', where + '“' + label(p) + '”从出生点走不到');
    const wins = exits.filter(e => e.outcome === 'win' && reach(e)), loses = exits.filter(e => e.outcome === 'lose');
    if (!wins.length) { add('bad', where + '没有走得到的 win 出口：这个现场不可能通关'); return; }
    const speed = EX.K.Speed;
    // 最快通关：任选一个 win 出口，并先拿到它要求的线索
    let best = null;
    for (const e of wins) {
      const stops = [];
      let ok = true;
      for (const id of e.requires || []) {
        const c = (v.clues || []).find(x => x && x.id === id);
        if (c && c.trigger !== 'interact') continue; // 入场即得
        const p = clues.find(q => q.clue === id && reach(q));
        if (!p) { add('bad', where + '出口“' + label(e) + '”要求的线索 ' + id + ' 在现场没有可走到的调查点'); ok = false; break; }
        if (!stops.includes(p)) stops.push(p);
      }
      if (!ok) continue;
      const t = tour(m, T, stops, e);
      if (t.steps < Infinity && (!best || t.steps < best.steps)) best = { steps: t.steps, order: t.order, exit: e };
    }
    if (!best) return;
    // 读线索卡也花时间（推理型现场的压力主要来自读和想）：每张卡 2 秒 + 每秒约 6 个字。
    const readOf = p => { const c = (v.clues || []).find(x => x && x.id === p.clue); return c ? 2 + (String(c.name || '').length + String(c.hint || '').length) / ReadCharsPerSecond : 2; };
    const fast = best.steps / speed + best.order.reduce((s, p) => s + readOf(p), 0);
    const reachable = clues.filter(reach);
    const full = reachable.length ? Math.min(...wins.map(e => tour(m, T, reachable, e).steps)) / speed + reachable.reduce((s, p) => s + readOf(p), 0) : fast;
    const timeout = n.timeout || 0, loose = chasers.length ? 3.5 : 2.5; // 有鬼时要留出绕路/躲草的时间
    const fullText = reachable.length ? '走完并读完全部 ' + reachable.length + ' 条线索约 ' + r1(full) + ' 秒' : '最快约 ' + r1(full) + ' 秒';
    if (timeout <= 0) add('info', where + '没有倒计时：' + fullText + '（原作几乎每个游戏都有时限，倒计时是压迫感的主要来源）');
    else if (fast > timeout) add('bad', where + '最快通关（含读必要线索）约 ' + r1(fast) + ' 秒，超过限时 ' + timeout + ' 秒：不可能通关');
    else if (full > timeout) add('good', where + '限时 ' + timeout + ' 秒：' + fullText + '，看不完——玩家必须取舍；最快通关约 ' + r1(fast) + ' 秒（确认关键线索在必经之路附近）');
    else {
      const ratio = timeout / full;
      if (ratio <= loose) add('good', where + '限时 ' + timeout + ' 秒：' + fullText + '（' + r1(ratio) + ' 倍余量）——仔细调查来得及，但不宽裕');
      else add('warn', where + '限时 ' + timeout + ' 秒是仔细调查所需（' + fullText + '）的 ' + r1(ratio) + ' 倍：倒计时几乎没有压力，可以缩短');
    }
    if (loses.length) add('good', where + '有 ' + loses.length + ' 个陷阱出口：选错会失败，出口抉择有分量');
    else if (!chasers.length && timeout <= 0) add('warn', where + '没有陷阱出口、没有鬼、也没有倒计时：这个现场没有失败方式');
    if (chasers.length) {
      // 最快路线（格子序列）
      const route = [T]; let at = T;
      for (const stop of best.order.concat([best.exit])) { const p = stepPath(m, at, stop); if (!p) break; route.push(...p.slice(1)); at = stop; }
      const stopIds = new Map(); for (const p of best.order) stopIds.set(p.x + ',' + p.y, p.id); stopIds.set(best.exit.x + ',' + best.exit.y, best.exit.id);
      const r = rusher(pack, v, n, route, stopIds);
      if (r.error) add('warn', where + '无法真实试跑：' + r.error);
      else if (r.caughtBy) add('good', where + '“直奔出口、不理鬼”的玩家在 ' + r.time + ' 秒被「' + r.caughtBy + '」抓住：必须观察巡逻、绕路或躲草，追逐有威胁');
      else if (r.ended && r.won) add('warn', where + '“直奔出口、不理鬼”的玩家 ' + r.time + ' 秒就通关了，没被抓：鬼形同虚设——把巡逻线挪到必经之路上，或加大视距');
      else add('info', where + '直奔试跑结果：' + (r.message || (r.ended ? '失败' : '未结束')) + '（' + r.time + ' 秒）');
      for (const c of chasers) {
        const cs = (c.speed > 0 ? c.speed : EX.K.DefaultChaserSpeed) / 10;
        if (cs >= speed) add('warn', where + '鬼「' + label(c) + '」追速 ' + cs + ' 格/秒不比玩家（' + speed + '）慢：被看见基本跑不掉（原作 5♠ 的鬼是能甩开的）');
        if (Math.hypot(c.x - T.x, c.y - T.y) < 4) add('bad', where + '鬼「' + label(c) + '」离出生点不到 4 格：开局就会被看见');
      }
      let cover = 0; const seen = new Set();
      for (const p of route) for (let dx = -2; dx <= 2; dx++) for (let dy = -2; dy <= 2; dy++) { const k = (p.x + dx) + ',' + (p.y + dy); if (seen.has(k)) continue; seen.add(k); if (EX.hides(EX.at(m, p.x + dx, p.y + dy))) cover++; }
      if (!cover) add('warn', where + '最快路线两格内没有可躲的高草：被鬼看见时只能硬跑');
    }
  }

  function gameReport(n, add) {
    const where = '「' + (n.name || n.id) + '」', g = n.game;
    if (!g) { add('bad', where + '缺少博弈参数'); return; }
    const st0 = BG.run(g, []); if (st0.error) { add('bad', where + '博弈参数有误：' + st0.error); return; }
    const options = g.mode === 'beauty' ? Array.from({ length: 101 }, (_, i) => String(i)) : g.mode === 'mark' ? ['♠', '♥', '♦', '♣'] : ['分', '偷'];
    const score = st => st.error ? -1e9 : st.finished ? (st.won ? 1e6 : -1e6) : (st.alive[st.player] ? st.lives[st.player] * 1000 + st.points[st.player] : -1e6);
    const play = pick => { const ch = []; for (let r = 0; r < 60; r++) { ch.push(pick(ch)); const st = BG.run(g, ch); if (st.error || st.finished) return st; } return BG.run(g, ch); };
    const constants = g.mode === 'beauty' ? ['0', '22', '33', '50'] : options;
    const easy = constants.filter(c => { const st = play(() => c); return st.finished && st.won; });
    const greedy = play(ch => { let best = -Infinity, pick = options[0]; for (const o of options) { const sc = score(BG.run(g, ch.concat([o]))); if (sc > best) { best = sc; pick = o; } } return pick; });
    let solvable = greedy.finished && greedy.won;
    if (!solvable && g.mode !== 'beauty') {
      let budget = 20000;
      const dfs = pre => { if (--budget < 0) return false; const st = BG.run(g, pre); if (st.error) return false; if (st.finished) return st.won; if (pre.length >= 8) return false; return options.some(o => dfs(pre.concat([o]))); };
      solvable = dfs([]);
    }
    const shown = c => g.mode === 'beauty' ? '一直写 ' + c : '一直' + (g.mode === 'mark' ? '宣告 ' : '选「') + c + (g.mode === 'mark' ? '' : '」');
    if (!solvable) add('bad', where + '即使每轮都看穿对手（作弊级最优出法）也赢不了：可能无解，请调轮数/命数/对手人格');
    else if (easy.length) add('warn', where + '无脑' + easy.map(shown).join('、') + ' 也能赢：太容易，换更会读人的对手人格或加轮数');
    else add('good', where + '固定出法都会输，读懂对手能赢：需要真正的博弈');
    add('info', where + '共 ' + (g.rounds || '?') + ' 轮上限、玩家 ' + (g.lives || '?') + ' 条命、' + (g.seats || []).length + ' 人同桌');
  }

  function reachableNodes(v) {
    const out = [], seen = new Set(), q = [v.entry];
    while (q.length) {
      const id = q.shift(); if (!id || seen.has(id)) continue; seen.add(id);
      const n = (v.nodes || []).find(x => x && x.id === id); if (!n) continue; out.push(n);
      if (['room', 'explore', 'game'].includes(n.kind)) q.push(n.onWin, n.onLose); else if (n.kind === 'story' || n.kind === 'reveal') q.push(n.next);
    }
    return out;
  }

  /** 体检一个会场：返回 {items:[{level:'bad'|'warn'|'good'|'info', text}], counts}。 */
  function venue(pack, venueId) {
    const items = [], add = (level, text) => items.push({ level, text });
    const v = (pack.venues || []).find(x => x && x.id === venueId);
    if (!v) return { items: [{ level: 'bad', text: '会场不存在：' + venueId }], counts: { bad: 1, warn: 0, good: 0, info: 0 } };
    const nodes = reachableNodes(v), kinds = new Set(nodes.map(n => n.kind));
    const acts = nodes.filter(n => ['room', 'explore', 'game'].includes(n.kind));
    if (!String(v.rules || '').trim()) add('warn', '入场没写公开规则：原作每个会场先在手机上公布规则、难度和时限，玩家知道规则才有紧张感');
    else add('good', '入场公开规则：' + String(v.rules).slice(0, 40) + (String(v.rules).length > 40 ? '…' : ''));
    if (!acts.length) add('warn', '没有可玩的活动阶段（现场探索 / 多人博弈 / 横版房间）：这个会场只有文字');
    for (const n of acts) if (n.onWin && n.onWin === n.onLose) add('warn', '「' + (n.name || n.id) + '」胜负走同一条路：失败没有后果');
    if (!kinds.has('reveal')) add('warn', '没有“揭示”阶段：通关后缺少“原来如此”的反转');
    else {
      const ids = new Set(nodes.map(n => n.id)), twists = (v.clues || []).filter(c => c && String(c.truth || '').trim());
      const unrevealed = twists.filter(c => !c.revealAt || !ids.has(c.revealAt));
      if (!twists.length) add('warn', '有揭示阶段，但没有一条线索写了“作者真相”：揭示时没有东西可以反转');
      else if (unrevealed.length) add('warn', unrevealed.length + ' 条线索写了真相但揭示阶段不可达：' + unrevealed.map(c => c.name || c.id).join('、'));
      else add('good', twists.length + ' 条线索会在揭示阶段被重新解释（反转）');
    }
    const endings = nodes.filter(n => n.kind === 'ending');
    if (acts.length && endings.length < 2) add('warn', '只有 ' + endings.length + ' 个结局：通关与失败最好走向不同结局（GAME CLEAR / GAME OVER）');
    const doors = (pack.links || []).filter(l => l && l.from === v.id);
    add('info', doors.length ? '结局后有 ' + doors.length + ' 扇门通往其他会场' : '结局后没有门：旅程会在这个会场结束');
    for (const n of acts) {
      try { if (n.kind === 'explore') exploreReport(pack, v, n, add); else if (n.kind === 'game') gameReport(n, add); }
      catch (e) { add('warn', '「' + (n.name || n.id) + '」体检中断：' + e.message); }
    }
    const counts = { bad: 0, warn: 0, good: 0, info: 0 }; for (const i of items) counts[i.level]++;
    return { items, counts };
  }
  function world(pack) { return (pack.venues || []).map(v => Object.assign({ id: v.id, name: v.name }, venue(pack, v.id))); }
  return { venue, world };
})();
if (typeof module !== 'undefined') module.exports = WPCheck;
