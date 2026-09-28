// ── 纯逻辑（与项目 C# 同规则的网页移植；最终以 Unity / 沙盒体检为准）──
const RULES = { L2Up: 2, L2Side: 5, L2Fall: 30, JumpUp: 2, JumpSide: 4, JumpUpSide: 2, MaxFall: 30, SpringHeadroom: 4, MuzzleClear: 3, MaxPool: 3, RunSpeed: 4.95, StartDelay: 4 };
const SLOT_CHARS = { '1': 'cb', '2': 'b.', '3': '~.' };
const OPENERS = 'Cx|%';

function makeWorld(proposals) {
  const info = new Map();
  for (const e of ELEMENTS) info.set(e.c, e);
  for (const p of (proposals || [])) info.set(p.c, { c: p.c, k: p.key, zh: p.zh, en: p.en || p.key, r: p.role, w: p.effect, p: p.place || '', u: false, s: !!p.support, s1: true, m: 0, so: !!p.solid, hz: false, rgb: p.rgb, proposal: true });
  const solid = new Set(), hazard = new Set();
  for (const [c, e] of info) { if (e.so) solid.add(c); if (e.hz) hazard.add(c); }
  return { info, solid, hazard };
}


// ── L2 可达性（移植 LevelReachabilityAnalyzer，死局检查用；collect=true 时启用"横向跳不能穿高墙"）──
function l2Prep(W, g) {
  if (g._p && g._p.W === W) return g._p;
  const h = g.length, w = g[0].length, solid = new Uint8Array(w * h), haz = new Uint8Array(w * h), oneway = new Uint8Array(w * h), boost = new Uint8Array(w * h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const c = g[h - 1 - y][x], i = y * w + x; solid[i] = W.solid.has(c) ? 1 : 0; haz[i] = W.hazard.has(c) ? 1 : 0; oneway[i] = c === '-' ? 1 : 0; const e = W.info.get(c); boost[i] = e && e.jb ? 1 : 0; }
  return (g._p = { W, w, h, solid, haz, oneway, boost });
}
function l2Solid(W, g, x, y) { const P = l2Prep(W, g); if (x < 0 || x >= P.w || y < 0 || y >= P.h) return false; return P.solid[y * P.w + x] === 1; }
function l2Stand(W, g, x, y) { const P = l2Prep(W, g); if (x < 0 || x >= P.w || y < 0 || y >= P.h) return false; const i = y * P.w + x; if (P.solid[i] || P.haz[i]) return false; if (y === 0) return true; return P.solid[i - P.w] === 1; }
function l2Boost(W, g, x, y) { if (y <= 0) return false; const P = l2Prep(W, g), i = y * P.w + x; return P.boost[i - P.w] === 1 || P.boost[i] === 1; }
function l2OneWay(W, g, x, y) { const P = l2Prep(W, g); return P.oneway[y * P.w + x] === 1; }
function l2Feasible(dx, dy, up, side) { if (dy <= 0) return true; const r = dy / up; if (r > 1) return false; return dx <= side * (1 - r * 0.5); }
function l2Blocked(W, g, fx, fy, tx, ty) { if (ty <= fy) return false; for (let y = fy + 1; y <= Math.min(ty, g.length - 1); y++) if (l2Solid(W, g, fx, y) && !l2OneWay(W, g, fx, y)) return true; return false; }
function l2Arc(W, g, fx, fy, tx, ty, top) {
  if (fx === tx) return true; const step = tx > fx ? 1 : -1, low = Math.max(fy, ty), high = Math.min(g.length - 1, Math.max(low, top));
  for (let cx = fx + step; cx !== tx; cx += step) { let open = false; for (let cy = low; cy <= high && !open; cy++) open = !l2Solid(W, g, cx, cy) || l2OneWay(W, g, cx, cy); if (!open) return false; }
  for (let cy = ty + 1; cy <= low; cy++) if (l2Solid(W, g, tx, cy) && !l2OneWay(W, g, tx, cy)) return false;
  return true;
}
function l2Reach(W, g, sx, sy, collect) {
  const w = g[0].length, h = g.length, seen = new Set(), cells = new Set(), q = [];
  const push = (x, y, b) => { if (x < 0 || x >= w || y < 0 || y >= h) return; const k = x * 10000 + y * 10 + (b ? 1 : 0); if (seen.has(k)) return; seen.add(k); q.push([x, y, b]); };
  push(sx, sy, l2Boost(W, g, sx, sy));
  for (let fy = sy; fy >= 0; fy--) if (l2Stand(W, g, sx, fy)) { push(sx, fy, l2Boost(W, g, sx, fy)); break; }
  let qi = 0;
  while (qi < q.length) {
    const [cx, cy, cb] = q[qi++]; cells.add(key(cx, cy));
    const up = RULES.L2Up + (cb ? RULES.L2Up : 0), side = RULES.L2Side;
    for (let nx = Math.max(0, cx - side); nx <= Math.min(w - 1, cx + side); nx++) {
      const dx = Math.abs(nx - cx);
      for (let ny = cy; ny <= Math.min(h - 1, cy + up); ny++) {
        const dy = ny - cy;
        if (collect && !l2Arc(W, g, cx, cy, nx, ny, cy + up)) continue;
        if (dy > 0 && !l2Feasible(dx, dy, up, side)) continue;
        if (l2Stand(W, g, nx, ny) && !l2Blocked(W, g, cx, cy, nx, ny)) push(nx, ny, l2Boost(W, g, nx, ny));
      }
      const blocked = collect && !l2Arc(W, g, cx, cy, nx, cy, cy + up);
      for (let ny = cy - 1; ny >= Math.max(0, cy - RULES.L2Fall) && !blocked; ny--) {
        if (l2Stand(W, g, nx, ny)) { push(nx, ny, l2Boost(W, g, nx, ny)); break; }
        if (ny > 0 && l2Solid(W, g, nx, ny)) break;
      }
    }
    for (const wdx of [-1, 1]) {
      const wx = cx + wdx; if (wx < 0 || wx >= w) continue;
      if (l2Stand(W, g, wx, cy) && !l2Solid(W, g, wx, cy)) push(wx, cy, l2Boost(W, g, wx, cy));
      if (cy > 0 && !l2Solid(W, g, wx, cy) && l2Stand(W, g, wx, cy - 1)) push(wx, cy - 1, l2Boost(W, g, wx, cy - 1));
    }
  }
  return cells;
}

function l2Step(W, g, cx, cy, cb, collect, out) {
  const w = g[0].length, h = g.length, up = RULES.L2Up + (cb ? RULES.L2Up : 0), side = RULES.L2Side;
  for (let nx = Math.max(0, cx - side); nx <= Math.min(w - 1, cx + side); nx++) {
    const dx = Math.abs(nx - cx);
    for (let ny = cy; ny <= Math.min(h - 1, cy + up); ny++) {
      const dy = ny - cy;
      if (collect && !l2Arc(W, g, cx, cy, nx, ny, cy + up)) continue;
      if (dy > 0 && !l2Feasible(dx, dy, up, side)) continue;
      if (l2Stand(W, g, nx, ny) && !l2Blocked(W, g, cx, cy, nx, ny)) out.push([nx, ny, l2Boost(W, g, nx, ny)]);
    }
    const blocked = collect && !l2Arc(W, g, cx, cy, nx, cy, cy + up);
    for (let ny = cy - 1; ny >= Math.max(0, cy - RULES.L2Fall) && !blocked; ny--) {
      if (l2Stand(W, g, nx, ny)) { out.push([nx, ny, l2Boost(W, g, nx, ny)]); break; }
      if (ny > 0 && l2Solid(W, g, nx, ny)) break;
    }
  }
  for (const wdx of [-1, 1]) {
    const wx = cx + wdx; if (wx < 0 || wx >= w) continue;
    if (l2Stand(W, g, wx, cy) && !l2Solid(W, g, wx, cy)) out.push([wx, cy, l2Boost(W, g, wx, cy)]);
    if (cy > 0 && !l2Solid(W, g, wx, cy) && l2Stand(W, g, wx, cy - 1)) out.push([wx, cy - 1, l2Boost(W, g, wx, cy - 1)]);
  }
}
/** 能回到出口的所有格（状态含弹跳位，结果按格合并）。 */
function l2HomeSet(W, g, exitKey) {
  const w = g[0].length, h = g.length, rev = new Map(), buf = [];
  const sk = (x, y, b) => x * 10000 + y * 10 + (b ? 1 : 0);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (!l2Stand(W, g, x, y)) continue;
    for (const b of [false, true]) {
      if (b !== !!l2Boost(W, g, x, y)) continue;
      buf.length = 0; l2Step(W, g, x, y, b, true, buf);
      const from = sk(x, y, b);
      for (const [nx, ny, nb] of buf) { const t = sk(nx, ny, nb); if (!rev.has(t)) rev.set(t, []); rev.get(t).push(from); }
    }
  }
  const ex = Math.floor(exitKey / 1000), ey = exitKey % 1000, seen = new Set(), q = [];
  for (const b of [false, true]) { const s0 = sk(ex, ey, b); seen.add(s0); q.push(s0); }
  let qi = 0; while (qi < q.length) { const c = q[qi++]; for (const n of (rev.get(c) || [])) if (!seen.has(n)) { seen.add(n); q.push(n); } }
  const cells = new Set(); for (const s of seen) cells.add(key(Math.floor(s / 10000), Math.floor(s / 10) % 1000)); return cells;
}
function l2Standable(W, g) { const s = new Set(); for (let y = 0; y < g.length; y++) for (let x = 0; x < g[0].length; x++) if (l2Stand(W, g, x, y)) s.add(key(x, y)); return s; }

function stripSlots(rows) { return rows.map(r => r.replace(/[123]/g, '.')); }
function at(g, x, y) { const row = g.length - 1 - y; if (row < 0 || row >= g.length || x < 0 || x >= g[row].length) return 'W'; return g[row][x]; }
function canStand(W, g, x, y) { const c = at(g, x, y); if (W.solid.has(c) || W.hazard.has(c)) return false; return y === 0 || W.solid.has(at(g, x, y - 1)); }
function arcClear(W, g, x0, x1, apexY) { const a = Math.min(x0, x1), b = Math.max(x0, x1); for (let x = a; x <= b; x++) { const c = at(g, x, apexY); if (W.solid.has(c) && c !== '-') return false; } return true; }
function key(x, y) { return x * 1000 + y; }

function moves(W, g, cx, cy) {
  const out = [], h = g.length, w = Math.max(...g.map(r => r.length));
  for (const dx of [-1, 1]) {
    const nx = cx + dx; if (nx < 0 || nx >= w || W.solid.has(at(g, nx, cy))) continue;
    for (let y = cy; y >= Math.max(0, cy - RULES.MaxFall); y--) { if (W.solid.has(at(g, nx, y))) break; if (canStand(W, g, nx, y)) { out.push([nx, y]); break; } }
  }
  outer: for (let dy = 1; dy <= RULES.JumpUp; dy++) {
    for (let k = 1; k <= dy; k++) { const a = at(g, cx, cy + k); if (W.solid.has(a) && a !== '-') break outer; }
    for (let dx = -RULES.JumpUpSide; dx <= RULES.JumpUpSide; dx++) {
      const nx = cx + dx, ny = cy + dy; if (nx < 0 || nx >= w || ny >= h) continue;
      if (!canStand(W, g, nx, ny) || !arcClear(W, g, cx, nx, cy + dy)) continue;
      out.push([nx, ny]);
    }
  }
  for (let dx = -RULES.JumpSide; dx <= RULES.JumpSide; dx++) {
    if (Math.abs(dx) < 2) continue; const nx = cx + dx;
    if (nx < 0 || nx >= w || !arcClear(W, g, cx, nx, cy + 1)) continue;
    for (let y = cy; y >= Math.max(0, cy - RULES.MaxFall); y--) { if (W.solid.has(at(g, nx, y))) break; if (canStand(W, g, nx, y)) { out.push([nx, y]); break; } }
  }
  return out;
}

function settle(W, g, x, y) { for (let yy = y; yy >= 0; yy--) if (canStand(W, g, x, yy)) return [x, yy]; return null; }

function buildGraph(W, g) {
  const fwd = new Map(), rev = new Map(), h = g.length, w = Math.max(...g.map(r => r.length));
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    if (!canStand(W, g, x, y)) continue; const k = key(x, y); const list = [];
    for (const [nx, ny] of moves(W, g, x, y)) { const nk = key(nx, ny); list.push(nk); if (!rev.has(nk)) rev.set(nk, []); rev.get(nk).push(k); }
    fwd.set(k, list);
  }
  return { fwd, rev };
}
function bfs(edges, start) { const seen = new Set([start]), q = [start]; while (q.length) { const c = q.shift(); for (const n of (edges.get(c) || [])) if (!seen.has(n)) { seen.add(n); q.push(n); } } return seen; }

function path(W, g, from, to) {
  const a = settle(W, g, from[0], from[1]), b = settle(W, g, to[0], to[1]); if (!a || !b) return null;
  const prev = new Map([[key(a[0], a[1]), -1]]), q = [a];
  while (q.length) {
    const [x, y] = q.shift();
    if (x === b[0] && y === b[1]) { const p = []; for (let k = key(x, y); k !== -1; k = prev.get(k)) p.push([Math.floor(k / 1000), k % 1000]); return p.reverse(); }
    for (const [nx, ny] of moves(W, g, x, y)) { const k = key(nx, ny); if (!prev.has(k)) { prev.set(k, key(x, y)); q.push([nx, ny]); } }
  }
  return null;
}

function find(g, ch) { const out = []; for (let row = 0; row < g.length; row++) for (let x = 0; x < g[row].length; x++) if (g[row][x] === ch) out.push([x, g.length - 1 - row]); return out; }

function placementIssues(W, g, step1) {
  const issues = [], h = g.length, counts = {};
  const err = (x, y, t) => issues.push({ x, y, t, sev: 'error' });
  for (let row = 0; row < h; row++) {
    const line = g[row], y = h - 1 - row;
    for (let x = 0; x < line.length; x++) {
      const c = line[x]; if (c === '.' || c === ' ' || SLOT_CHARS[c]) continue;
      const e = W.info.get(c);
      if (!e) { err(x, y, `'${c}' 不是已知元素，也不是你的新机制提案`); continue; }
      counts[c] = (counts[c] || 0) + 1;
      if (step1 && !e.s1) err(x, y, `${e.zh}：第 1 步房间不用这个元素`);
      if (e.s) { const below = row + 1 < h ? g[row + 1][x] : '.'; if (!W.solid.has(below)) err(x, y, `${e.zh}：脚下不是实心（会悬空）`); }
      if (c === 'w' && (x === 0 || line[x - 1] !== 'w')) { let run = 0; while (x + run < line.length && line[x + run] === 'w') run++; if (run > RULES.MaxPool) err(x, y, `毒池连续 ${run} 格太宽：最多 ${RULES.MaxPool} 格`); }
      if (c === 'Y' || c === 'Q') for (let d = 1; d <= 2; d++) { if (row - d < 0) break; const a = g[row - d][x]; if (W.solid.has(a) && a !== '-') { err(x, y, `${e.zh}：正上方第 ${d} 格是实心，上方至少空 2 格`); break; } }
      if (c === 'J') for (let d = 1; d <= RULES.SpringHeadroom; d++) { if (row - d < 0) break; if (W.solid.has(g[row - d][x])) { err(x, y, `弹簧板：正上方第 ${d} 格是实心，会撞天花板；上方至少空 ${RULES.SpringHeadroom} 格`); break; } }
      if (e.m) for (let d = 1; d <= RULES.MuzzleClear; d++) { const fx = x + e.m * d; if (fx < 0 || fx >= line.length) break; if (W.solid.has(line[fx])) { err(x, y, `${e.zh}：炮口前第 ${d} 格是实心，炮弹会撞碎；前方至少空 ${RULES.MuzzleClear} 格`); break; } }
    }
  }
  if ((counts['O'] || 0) % 2 === 1) issues.push({ x: -1, y: -1, t: `通风管 O 有 ${counts['O']} 个：要成对摆放`, sev: 'error' });
  for (const [c, e] of W.info) if (e.u && (counts[c] || 0) > 1) issues.push({ x: -1, y: -1, t: `${e.zh} '${c}' 只能有 1 个（现在 ${counts[c]} 个）`, sev: 'error' });
  return issues;
}

function check(W, rawGrid, step1) {
  const g = stripSlots(rawGrid), res = { issues: [], deadlock: new Set(), route: null, seconds: 0, onRoute: [], offRoute: [], playable: false };
  if (!g.length || !g[0].length) { res.issues.push({ x: -1, y: -1, t: '画布是空的', sev: 'error' }); return res; }
  const widths = new Set(g.map(r => r.length)); if (widths.size > 1) res.issues.push({ x: -1, y: -1, t: '每一行长度要一样', sev: 'error' });
  if (g[0].length < 12 || g.length < 6) res.issues.push({ x: -1, y: -1, t: `房间太小：至少 12 宽 × 6 高（现在 ${g[0].length}×${g.length}）`, sev: 'error' });
  if (g[0].length > 128 || g.length > 48) res.issues.push({ x: -1, y: -1, t: `房间太大：最多 128 宽 × 48 高（现在 ${g[0].length}×${g.length}）`, sev: 'error' });
  if (g.length > 16) res.issues.push({ x: -1, y: -1, t: `高 ${g.length} 格：超过 16 格一屏装不下，游戏里镜头会跟着人走（能玩，只是看不到全图）`, sev: 'info' });
  for (const c of ['M', 'T', 'G']) { const n = find(g, c).length; if (n !== 1) res.issues.push({ x: -1, y: -1, t: `需要且只能有一个 ${W.info.get(c).zh} ${c}（现在 ${n} 个）`, sev: 'error' }); }
  if (step1 && find(g, 'o').length !== 1) res.issues.push({ x: -1, y: -1, t: '第 1 步房间需要且只能有一个宝物 o', sev: 'error' });
  if (res.issues.some(i => i.sev === 'error')) return res;
  const h = g.length, w = g[0].length;
  for (let x = 0; x < w; x++) { if (!W.solid.has(g[0][x]) || !W.solid.has(g[h - 1][x])) { res.issues.push({ x: -1, y: -1, t: '最上面一行和最下面一行必须全是实心（墙 W / 地面 #）', sev: 'error' }); break; } }
  for (let row = 0; row < h; row++) if (!W.solid.has(g[row][0]) || !W.solid.has(g[row][w - 1])) { res.issues.push({ x: -1, y: -1, t: '最左和最右一列必须全是墙 W', sev: 'error' }); break; }
  res.issues.push(...placementIssues(W, g, step1));
  const M = find(g, 'M')[0], G = find(g, 'G')[0], O = find(g, 'o')[0];
  const a = path(W, g, M, O || G), b = O ? path(W, g, O, G) : [];
  if (!a || !b) res.issues.push({ x: M[0], y: M[1], t: 'AI 马里奥按楼层寻路走不通（跳跃间距在临界区：往上 ≤2 格、左右 ≤2 格；平跳 ≤4 格）', sev: 'warn' });
  if (a && b) {
    res.route = a.concat(b.slice(1)); let len = 0; const times = [RULES.StartDelay];
    for (let i = 1; i < res.route.length; i++) { const [px, py] = res.route[i - 1], [qx, qy] = res.route[i]; len += Math.hypot(qx - px, qy - py); times.push(RULES.StartDelay + len / RULES.RunSpeed); }
    res.seconds = RULES.StartDelay + len / RULES.RunSpeed;
    res.lootAt = O ? times[a.length - 1] : 0;
    for (let row = 0; row < h; row++) for (let x = 0; x < w; x++) {
      const c = g[row][x], e = W.info.get(c); if (!e || !(e.r === 'PlayerPrank' || c === 'R' || c === 'U' || e.proposal)) continue;
      const y = h - 1 - row; let best = 1e9, bi = 0;
      res.route.forEach(([rx, ry], i) => { const d = Math.hypot(rx - x, ry - y); if (d < best) { best = d; bi = i; } });
      const near = 1.5 + (e.m ? 4 : 0);
      if (best <= near) res.onRoute.push({ c, x, y, at: times[bi] }); else if (best > 3) res.offRoute.push({ c, x, y });
    }
    res.onRoute.sort((p, q) => p.at - q.at);
  }
  // 最坏情况（与 Unity LevelDeadlockAnalyzer 同规则）：塌桥/裂缝地板/捷径门/裂墙全部打开 → 马里奥可能站的每一格还能不能回出口
  const L2base = l2Reach(W, g, M[0], M[1], false);
  if (O && !L2base.has(key(O[0], O[1]))) res.issues.push({ x: O[0], y: O[1], t: '马里奥走不到宝物（按 Unity 可达性）', sev: 'error' });
  else if (O && !l2Reach(W, g, O[0], O[1], false).has(key(G[0], G[1]))) res.issues.push({ x: O[0], y: O[1], t: '拿到宝物后回不到出口（按 Unity 可达性）', sev: 'error' });
  if (g.some(r => /[Cx|%]/.test(r))) {
    const worst = g.map(r => r.replace(/[Cx|%]/g, '.'));
    const cand = new Set(L2base); if (O) for (const k of l2Reach(W, g, O[0], O[1], false)) cand.add(k);
    const stand = l2Standable(W, worst);
    for (let row = 0; row < h; row++) for (let x = 0; x < w; x++) if (OPENERS.includes(g[row][x])) { for (let y = h - 1 - row; y >= 0; y--) if (stand.has(key(x, y))) { cand.add(key(x, y)); break; } }
    const exitKey = key(G[0], G[1]), cache = new Map(); let reported = 0, total = 0;
    // 反向可达：先对每个可站格做一次单步展开，建反向边，再从出口反向 BFS（一次搞定，替代逐格 BFS）
    const homeSet = l2HomeSet(W, worst, exitKey);
    for (const k of cand) {
      if (!stand.has(k)) continue;
      let ok = cache.get(k);
      if (ok === undefined) { ok = homeSet.has(k); cache.set(k, ok); }
      if (ok) continue;
      res.deadlock.add(k); total++;
      if (reported++ < 5) res.issues.push({ x: Math.floor(k / 1000), y: k % 1000, t: '塌桥/裂缝地板/捷径门/裂墙打开后，站在这里的马里奥再也回不到出口（死局：给下面留一条回去的路，比如单向台面 -）', sev: 'error' });
    }
    if (total > 5) res.issues.push({ x: -1, y: -1, t: `还有 ${total - 5} 个死局格（画布上红色斜线）`, sev: 'error' });
  }
  const unreach = unreachableStands(W, rawGrid); res.unreach = unreach;
  if (unreach.size) {
    const first = [...unreach].slice(0, 3).map(k => `(${Math.floor(k / 1000)},${k % 1000})`).join(' ');
    res.issues.push({ x: Math.floor([...unreach][0] / 1000), y: [...unreach][0] % 1000, t: `${unreach.size} 个能站的格子马里奥永远上不去（台子太高 / 坑太深 / 被墙隔开）：${first}${unreach.size > 3 ? ' …' : ''}（画布上紫色点；如果是故意给捣蛋者用的高台可以不管）`, sev: 'warn' });
  }
  if (res.offRoute.length) res.issues.push({ x: -1, y: -1, t: `${res.offRoute.length} 个机关离马里奥的路线超过 3 格：只能靠挑衅/诱饵把他引过去`, sev: 'info' });
  res.playable = !res.issues.some(i => i.sev === 'error');
  return res;
}

if (typeof module !== 'undefined') module.exports = { makeWorld, check, path, placementIssues, stripSlots, find };

// ── S205：跳跃辅助 ────────────────────────────────────
/** 从 (x,y) 站着起跳，一步能到哪：ai = AI 马里奥实际会走的（保守：往上 ≤2 格、左右 ≤2 格；平跳 ≤4 格），
 *  phys = 物理上跳得到但 AI 不会稳定走（临界区，H8：别让关键路线只靠这种跳）。 */
function jumpReach(W, rawGrid, x, y) {
  const g = stripSlots(rawGrid), s = settle(W, g, x, y), ai = new Set(), phys = new Set();
  if (!s) return { from: null, ai, phys };
  for (const [nx, ny] of moves(W, g, s[0], s[1])) ai.add(key(nx, ny));
  const buf = []; l2Step(W, g, s[0], s[1], l2Boost(W, g, s[0], s[1]), true, buf);
  for (const [nx, ny] of buf) { const k = key(nx, ny); if (!ai.has(k) && k !== key(s[0], s[1])) phys.add(k); }
  return { from: s, ai, phys };
}
/** 整张图：马里奥（从 M 出发）永远到不了的"能站的格"——台子太高、坑太深等。 */
function unreachableStands(W, rawGrid) {
  const g = stripSlots(rawGrid), M = find(g, 'M')[0], out = new Set();
  if (!M) return out;
  // 塌桥/裂缝/门/裂墙打开后才去得了的地方（比如桥下的坑）也算"去得了"——那是死局检查管的事
  const open = g.map(r => r.replace(/[Cx|%]/g, '.'));
  const reach = l2Reach(W, g, M[0], M[1], false), O = find(g, 'o')[0];
  for (const k of l2Reach(W, open, M[0], M[1], false)) reach.add(k);
  if (O && reach.has(key(O[0], O[1]))) for (const k of l2Reach(W, g, O[0], O[1], false)) reach.add(k);
  for (let yy = 0; yy < g.length; yy++) for (let xx = 0; xx < g[0].length; xx++) if (l2Stand(W, g, xx, yy) && l2Stand(W, open, xx, yy) && !reach.has(key(xx, yy))) out.add(key(xx, yy));
  return out;
}

// ── S205：从别的工具导入 ─────────────────────────────
/** 识别文本格式：Tiled JSON / LDtk（.ldtk 或超简导出 data.json 不含网格 → 用 CSV）/ 数字 CSV / ASCII。 */
function parseForeign(text, name) {
  const t = text.replace(/^\uFEFF/, '').trim(); name = (name || '').toLowerCase();
  if (t[0] === '{') {
    const d = JSON.parse(t);
    if (d.type === 'mariotrickster-levelpack' || Array.isArray(d.levels) && d.levels.length && d.levels[0].grid) return { kind: 'pack', pack: d };
    if (d.grid) return { kind: 'studio', studio: d };
    if (Array.isArray(d.layers) && d.width && d.height && d.tiledversion !== undefined || (Array.isArray(d.layers) && d.layers.some(l => l.type === 'tilelayer'))) {
      const layers = d.layers.filter(l => l.type === 'tilelayer' && Array.isArray(l.data));
      if (!layers.length) throw new Error('Tiled 地图里没有"图块层"（或层数据被压缩了：请在 Tiled 地图属性里把"图层格式"改成 CSV 再导出 JSON）');
      const w = d.width, h = d.height, nums = [];
      for (let r = 0; r < h; r++) { const row = []; for (let c = 0; c < w; c++) { let v = 0; for (const l of layers) { const g = (l.data[r * w + c] || 0) & 0x1fffffff; if (g) v = g; } row.push(v); } nums.push(row); }
      return { kind: 'numbers', from: 'Tiled', nums };
    }
    if (d.levels && d.defs) {
      const lv = d.levels[0], li = (lv.layerInstances || []).find(l => l.__type === 'IntGrid' && l.intGridCsv);
      if (!li) throw new Error('LDtk 文件里没找到 IntGrid 层（或关卡存成了外部文件：请用"超简导出"里的 .csv）');
      const w = li.__cWid, h = li.__cHei, nums = [];
      for (let r = 0; r < h; r++) nums.push(li.intGridCsv.slice(r * w, (r + 1) * w));
      const vals = {}; const def = d.defs.layers.find(x => x.uid === li.layerDefUid);
      if (def && def.intGridValues) for (const v of def.intGridValues) vals[v.value] = v.identifier || '';
      return { kind: 'numbers', from: 'LDtk', nums, names: vals };
    }
    throw new Error('认不出这个 JSON：支持 本设计台 .studio.json / Tiled 导出的 .json / LDtk 的 .ldtk');
  }
  const lines = t.replace(/\r/g, '').split('\n').map(l => l.trimEnd()).filter(l => l.length);
  if (lines.length && lines.every(l => /^\s*-?\d+(\s*,\s*-?\d+)*\s*,?\s*$/.test(l))) {
    const nums = lines.map(l => l.split(',').map(s => s.trim()).filter(s => s.length).map(Number));
    return { kind: 'numbers', from: name.endsWith('.csv') ? 'CSV（LDtk 超简导出 / Tiled CSV）' : '数字网格', nums };
  }
  // ASCII：元数据行是 "# Key: 值"；以 # 开头但全是 #（地面行）的仍是网格
  const meta = lines.filter(l => /^# ?[A-Za-z_]+:/.test(l)), rows = lines.filter(l => !/^# ?[A-Za-z_]+:/.test(l) && !/^# [^#]/.test(l));
  const w = Math.max(...rows.map(r => r.length));
  return { kind: 'ascii', rows: meta.concat(rows.map(r => r.replace(/ /g, '.').padEnd(w, '.'))) };
}
/** 数字网格 + 映射（数字 → 字符）→ ASCII 行；0 或没映射 = 空气。外圈自动补墙、底行补地面（可选）。 */
function numbersToAscii(nums, map, frame) {
  const h = nums.length, w = Math.max(...nums.map(r => r.length));
  const rows = nums.map(r => { let s = ''; for (let x = 0; x < w; x++) { const v = r[x] || 0; s += v && map[v] ? map[v] : '.'; } return s; });
  if (!frame) return rows;
  const out = [('W').repeat(w + 2)];
  for (const r of rows) out.push('W' + r + 'W');
  if (rows.length && /[^#W=]/.test(rows[rows.length - 1])) out.push('W' + '#'.repeat(w) + 'W');
  else out.push('W'.repeat(w + 2));
  return out;
}
/** 默认映射：LDtk 值名 / 常见习惯（1 = 地面）。 */
function defaultMap(values, names) {
  const byName = { ground: '#', floor: '#', wall: 'W', solid: '#', platform: '-', oneway: '-', spike: '^', fire: '~', bridge: 'C', spring: 'J', crate: 'c', bush: 'b', mario: 'M', player: 'M', start: 'M', trickster: 'T', goal: 'G', exit: 'G', loot: 'o', coin: 'o', treasure: 'o' };
  const m = {};
  for (const v of values) {
    const n = ((names && names[v]) || '').toLowerCase().replace(/[^a-z]/g, '');
    m[v] = byName[n] || (v === 1 ? '#' : v === 2 ? 'W' : v === 3 ? '-' : '');
  }
  return m;
}

/** S205：像素图导入——每个像素 = 一格，按颜色找最接近的元素（只在给定元素里找）。pixels = [r,g,b,a]*w*h（0..255）。 */
function imageToAscii(pixels, w, h, elements) {
  const pal = elements.map(e => ({ c: e.c, rgb: e.rgb.map(v => v * 255) }));
  const rows = [], used = {};
  for (let y = 0; y < h; y++) {
    let s = '';
    for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4, a = pixels[i + 3];
      if (a < 128) { s += '.'; continue; }
      let best = '.', bd = 1e9;
      for (const p of pal) { const d = (p.rgb[0] - pixels[i]) ** 2 + (p.rgb[1] - pixels[i + 1]) ** 2 + (p.rgb[2] - pixels[i + 2]) ** 2; if (d < bd) { bd = d; best = p.c; } }
      s += best; used[best] = (used[best] || 0) + 1;
    }
    rows.push(s);
  }
  return { rows, used };
}
