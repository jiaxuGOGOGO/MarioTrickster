// ── 纯逻辑（与项目 C# 同规则的网页移植；最终以 Unity / 沙盒体检为准）──
const RULES = { L2Up: 2, L2Side: 5, L2Fall: 30, JumpUp: 2, JumpSide: 4, JumpUpSide: 2, MaxFall: 30, SpringHeadroom: 4, MuzzleClear: 3, MaxPool: 3, RunSpeed: 4.95, StartDelay: 4 };
// S232：跑速 / 开局等待从 Unity 调参默认值来（build.py 读 MarioMindTuningSO.cs → TUNING），不再在网页里另写一份
if (typeof TUNING !== 'undefined') { RULES.RunSpeed = Math.round(9 * TUNING.marioSpeedScale.v * 100) / 100; RULES.StartDelay = TUNING.startDelaySeconds.v; }
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
  if (g.length > 16 || g[0].length > 64) res.issues.push({ x: -1, y: -1, t: `${g[0].length}×${g.length}：大房间（宽 >64 或高 >16）→ 游戏里用「智能跟随」镜头（死亡细胞式：跟着你走、马里奥靠近自动拉远；屏外红箭头 + 右上角小地图）。打开「游戏一屏」可看一屏有多大`, sev: 'info' });
  for (const c of ['M', 'T', 'G']) { const n = find(g, c).length; if (n !== 1) res.issues.push({ x: -1, y: -1, t: `需要且只能有一个 ${W.info.get(c).zh} ${c}（现在 ${n} 个）`, sev: 'error' }); }
  if (step1 && find(g, 'o').length !== 1) res.issues.push({ x: -1, y: -1, t: '第 1 步房间需要且只能有一个宝物 o', sev: 'error' });
  if (res.issues.some(i => i.sev === 'error')) return res;
  const h = g.length, w = g[0].length;
  for (let x = 0; x < w; x++) { if (!W.solid.has(g[0][x]) || !W.solid.has(g[h - 1][x])) { res.issues.push({ x: -1, y: -1, t: '最上面一行和最下面一行必须全是实心（墙 W / 地面 #）', sev: 'error' }); break; } }
  for (let row = 0; row < h; row++) if (!W.solid.has(g[row][0]) || !W.solid.has(g[row][w - 1])) { res.issues.push({ x: -1, y: -1, t: '最左和最右一列必须全是墙 W', sev: 'error' }); break; }
  // S235（= C# Step1Bounds.BorderLeaks）：外圈只用 墙 W / 地面 # / 平台 =；会塌 / 能穿 / 能炸的格子看着像出口 → 黄色提醒
  if (!res.issues.some(i => i.sev === 'error')) for (let row = 0; row < h; row++) for (let x = 0; x < w; x++) {
    if (!(row === 0 || row === h - 1 || x === 0 || x === w - 1) || 'W#='.includes(g[row][x])) continue;
    res.issues.push({ x, y: h - 1 - row, t: `外圈这里是「${(W.info.get(g[row][x]) || { zh: g[row][x] }).zh}」：会塌 / 能穿 / 能炸，看着像出口。外圈建议只用墙 W / 地面 #（游戏里外面有看不见的墙，不会真的掉出去）`, sev: 'warn' });
  }
  res.issues.push(...placementIssues(W, g, step1));
  const M = find(g, 'M')[0], G = find(g, 'G')[0], O = find(g, 'o')[0];
  const a = path(W, g, M, O || G), b = O ? path(W, g, O, G) : [];
  if (!a || !b) res.issues.push({ x: M[0], y: M[1], t: 'AI 马里奥按楼层寻路走不通（跳跃间距在临界区：往上 ≤2 格、左右 ≤2 格；平跳 ≤4 格）', sev: 'warn' });
  if (a && b) {
    res.route = a.concat(b.slice(1)); let len = 0; const times = [RULES.StartDelay];
    for (let i = 1; i < res.route.length; i++) { const [px, py] = res.route[i - 1], [qx, qy] = res.route[i]; len += Math.hypot(qx - px, qy - py); times.push(RULES.StartDelay + len / RULES.RunSpeed); }
    res.seconds = RULES.StartDelay + len / RULES.RunSpeed;
    res.lootAt = O ? times[a.length - 1] : 0; res.times = times;
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

// ── S207：移动工具（与 Unity LevelWorkshopModel.SelectAt/MoveBlock/ClampMove/CopyBlock/PasteBlock/ClearBlock 同规则）──
// 选区 sel = {x0,y0,x1,y1}（x 从左 0、y 从下 0）。外圈永远不动也不被覆盖；M/T/G/o 只能搬、不复制、不被覆盖/清掉。
const UNIQUE = 'MTGo';
const isFrame = (x, y, w, h) => x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1;
const groupsWithNeighbours = c => c !== '.' && c !== ' ' && c !== '#' && c !== 'W' && c !== '=';
function selectAt(g, x, y) {
  const h = g.length, w = h ? g[0].length : 0;
  if (x < 0 || y < 0 || x >= w || y >= h || isFrame(x, y, w, h)) return null;
  const c = at(g, x, y); if (c === '.' || c === ' ') return null;
  const s = { x0: x, y0: y, x1: x, y1: y }; if (!groupsWithNeighbours(c)) return s;
  const seen = new Set(), q = [[x, y]];
  while (q.length) {
    const [cx, cy] = q.pop();
    if (cx < 0 || cy < 0 || cx >= w || cy >= h || isFrame(cx, cy, w, h) || at(g, cx, cy) !== c || seen.has(key(cx, cy))) continue;
    seen.add(key(cx, cy)); s.x0 = Math.min(s.x0, cx); s.y0 = Math.min(s.y0, cy); s.x1 = Math.max(s.x1, cx); s.y1 = Math.max(s.y1, cy);
    q.push([cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]);
  }
  return s;
}
const selBox = (ax, ay, bx, by) => ({ x0: Math.min(ax, bx), y0: Math.min(ay, by), x1: Math.max(ax, bx), y1: Math.max(ay, by) });
function clampMove(w, h, s, dx, dy) { return [Math.max(1 - s.x0, Math.min(w - 2 - s.x1, dx)), Math.max(1 - s.y0, Math.min(h - 2 - s.y1, dy))]; }
function moveBlock(g, s, dx, dy) {
  const h = g.length, w = g[0].length, rows = g.map(r => r.split('')), lifted = [];
  for (let y = s.y0; y <= s.y1; y++) for (let x = s.x0; x <= s.x1; x++) {
    if (x < 0 || y < 0 || x >= w || y >= h || isFrame(x, y, w, h)) continue;
    const c = rows[h - 1 - y][x]; if (c === '.' || c === ' ') continue; lifted.push([x, y, c]); rows[h - 1 - y][x] = '.';
  }
  for (const [x, y, c] of lifted) { const nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h || isFrame(nx, ny, w, h)) continue; rows[h - 1 - ny][nx] = c; }
  return rows.map(r => r.join(''));
}
function copyBlock(g, s) {
  const h = g.length, w = g[0].length, out = [];
  for (let y = s.y0; y <= s.y1; y++) for (let x = s.x0; x <= s.x1; x++) {
    if (x < 0 || y < 0 || x >= w || y >= h || isFrame(x, y, w, h)) continue;
    const c = at(g, x, y); if (c === '.' || c === ' ' || UNIQUE.includes(c)) continue; out.push([x - s.x0, y - s.y0, c]);
  }
  return out;
}
function pasteBlock(g, block, x, y) {
  const h = g.length, w = g[0].length, rows = g.map(r => r.split(''));
  for (const [bx, by, c] of block) { const nx = x + bx, ny = y + by; if (nx < 0 || ny < 0 || nx >= w || ny >= h || isFrame(nx, ny, w, h) || UNIQUE.includes(rows[h - 1 - ny][nx])) continue; rows[h - 1 - ny][nx] = c; }
  return rows.map(r => r.join(''));
}
function clearBlock(g, s) {
  const h = g.length, w = g[0].length, rows = g.map(r => r.split(''));
  for (let y = s.y0; y <= s.y1; y++) for (let x = s.x0; x <= s.x1; x++) if (x >= 0 && y >= 0 && x < w && y < h && !isFrame(x, y, w, h) && !UNIQUE.includes(rows[h - 1 - y][x])) rows[h - 1 - y][x] = '.';
  return rows.map(r => r.join(''));
}
/** S207：游戏镜头（与 Unity Step1RoomCamera 同规则）：宽 > 64 或高 > 16 → 智能跟随（一屏约 12 格高 × 16:9）。 */
const CAMERA = { maxWholeWidth: 64, maxWholeHeight: 16, followViewHeight: 12, aspect: 16 / 9 };
function cameraPlan(w, h) {
  const big = w > CAMERA.maxWholeWidth || h > CAMERA.maxWholeHeight;
  const vh = big ? CAMERA.followViewHeight : h, vw = big ? Math.round(CAMERA.followViewHeight * CAMERA.aspect) : w;
  return { big, viewW: Math.min(w, vw), viewH: Math.min(h, vh) };
}

// ── S208：新建关卡向导 / 模式印章 / 节奏条 / 转移点提示（与 Unity LevelBlueprint 同规则，改一边必须改另一边）──
// 印章格式：rows 从上到下；stand = 哪一行是"马里奥站的那一行"；'_' = 不动原来的格；'*' = 主角机关（没指定就用 def）。
const PATTERNS = [
  { id: 'ambush', zh: '伏击点', def: '~', stand: 0, rows: ['b..*'], tip: '草丛隔两格放机关：你躲着、按准时机（最基础）' },
  { id: 'slide', zh: '滑铲送火', def: '~', stand: 0, rows: ['b.R.n..b*U'], tip: '绊线启动 → 香蕉皮滑过去 → 火点燃油桶（连锁入门）' },
  { id: 'highlow', zh: '高低两路', def: '~', stand: 2, rows: ['...--------', '--.........', '...*...*...', '___________'], tip: '地面有机关，头顶一条单向台面高路：谨慎型会走高路' },
  { id: 'pit', zh: '陷坑回廊', def: '~', stand: 0, rows: ['b.......', '__CCC___', '__.*.___'], tip: '塌桥下挖一格深的坑（坑里放火），掉下去能跳出来——地面要 ≥3 格厚' },
  { id: 'spring', zh: '弹射落点', def: '~', stand: 0, rows: ['b.J..b*'], tip: '弹簧把他弹上天（空中不能动），落点放机关；弹簧头顶要空 4 格' },
  { id: 'gate', zh: '关门打狗', def: '~', stand: 0, rows: ['b.*['], tip: '封路墙挡他 3.5 秒，让他停在机关上' },
  { id: 'snare', zh: '回马枪', def: 'Y', stand: 0, rows: ['b..*.'], tip: '放在宝物旁：他拿宝后急着回去，回程第一个坑' },
  { id: 'cannon', zh: '炮台走廊', def: 'K', stand: 0, rows: ['c.*....'], tip: '箱子挡在炮后，炮口前空 3 格（每局 1 发，要选准时机）' },
];
/** 盖章：左下角对齐 (x, standY)（standY = 马里奥站的那格的 y，y 从下 0）。外圈、M/T/G/o 所在格不动。返回新网格。 */
function stampPattern(g, p, x, standY, star) {
  const h = g.length, w = g[0].length, rows = g.map(r => r.split('')), uniq = 'MTGo';
  p.rows.forEach((line, i) => {
    const y = standY + (p.stand - i);
    for (let dx = 0; dx < line.length; dx++) {
      let c = line[dx]; if (c === '_') continue; if (c === '*') c = star || p.def;
      const nx = x + dx; if (nx <= 0 || y <= 0 || nx >= w - 1 || y >= h - 1) continue;
      const row = h - 1 - y; if (uniq.includes(rows[row][nx])) continue; rows[row][nx] = c;
    }
  });
  return rows.map(r => r.join(''));
}
const patternById = id => PATTERNS.find(p => p.id === id);
/** 向导能选的主角机关（都能在一层地面上教）。 */
const WIZ_STARS = ['~', 'n', '[', 'J', 'Y', 'Q', 'K', 'C'];
const WIZ_LENGTH = { 20: 48, 30: 64, 40: 94 };
/** 每个主角机关的"起承转合"四段（印章 id + 这段里的主角）。转 = 让地形变一变（陷坑）；塌桥本身就是地形 → 转换成弹簧。 */
const WIZ_RECIPES = {
  '~': [['ambush', '~'], ['slide', '~'], ['pit', '~'], ['gate', '~']],
  n: [['ambush', 'n'], ['slide', 'n'], ['pit', '~'], ['gate', 'n']],
  '[': [['ambush', '['], ['slide', '~'], ['pit', '~'], ['gate', '~']],
  J: [['spring', '~'], ['ambush', 'n'], ['pit', '~'], ['gate', '~']],
  Y: [['ambush', 'Y'], ['slide', 'Y'], ['pit', '~'], ['gate', 'Y']],
  Q: [['ambush', 'Q'], ['slide', 'Q'], ['pit', '~'], ['gate', 'Q']],
  K: [['cannon', 'K'], ['cannon', 'K'], ['pit', '~'], ['gate', '~']],
  C: [['pit', '~'], ['ambush', '~'], ['spring', '~'], ['pit', '~']],
};
const wizardRecipe = star => WIZ_RECIPES[star] || WIZ_RECIPES['~'];
const BEAT_ZH = ['起', '承', '转', '合'];
const BEAT_TIP = ['起 · 教：主角机关单独出现，旁边有草丛。先摸清他走多快、什么时候按', '承 · 加深：同一个机关接上别的，连成一套', '转 · 意外：让地形变一变（塌桥掉坑 / 弹簧弹飞），打乱他的路线', '合 · 收尾：宝物旁最后一道——他拿宝后回程第一个就是这里'];
/** 新建关卡向导：返回 { grid, beats:[x0..x4], notes, goal }。确定性（同样的选择永远同样的结果）。 */
function wizardLevel(star, seconds, idea) {
  const w = WIZ_LENGTH[seconds] || 48, h = 12, rows = [];
  for (let r = 0; r < h; r++) rows.push(r === 0 ? 'W'.repeat(w) : r >= h - 3 ? 'W' + '#'.repeat(w - 2) + 'W' : 'W' + '.'.repeat(w - 2) + 'W');
  let g = rows; const sy = 3; // 站的那一行 y=3（下面 3 层地面，挖坑也不会挖穿）
  const put = (x, c) => { const r = g[h - 1 - sy].split(''); r[x] = c; g[h - 1 - sy] = r.join(''); };
  put(2, 'G'); put(4, 'M'); put(w - 4, 'o');
  const x0 = 7, x4 = w - 6, beats = [0, 1, 2, 3, 4].map(i => Math.round(x0 + (x4 - x0) * i / 4));
  const recipe = wizardRecipe(star), notes = [];
  recipe.forEach(([pid, s], i) => {
    const p = patternById(pid), pw = p.rows[0].length, span = beats[i + 1] - beats[i];
    // 长关卡（这段 ≥ 17 格）：主模式放前面，后面再补一个伏击点，避免 10 秒以上什么都没发生
    const extra = span >= 17, px = beats[i] + (extra ? 2 : Math.max(0, Math.floor((span - pw) / 2)));
    g = stampPattern(g, p, px, sy, s);
    if (extra) g = stampPattern(g, patternById('ambush'), beats[i] + Math.floor(span * 0.62), sy, i === 2 || s === 'K' ? '~' : s);
    notes.push({ x: px, y: sy, text: BEAT_TIP[i] + `（模式：${p.zh}）` });
  });
  // 捣蛋者出生点：中间附近第一块空地（脚下实心、左右各空 1 格）
  const mid = Math.floor(w / 2), row = h - 1 - sy;
  for (let d = 0; d < w; d++) for (const x of [mid - d, mid + d]) {
    if (x < 6 || x > w - 7) continue;
    if (g[row][x] === '.' && g[row][x - 1] === '.' && g[row][x + 1] === '.' && g[row + 1][x] === '#') { put(x, 'T'); d = w; break; }
  }
  const zh = (ELEMENTS.find(e => e.c === star) || { zh: star }).zh;
  const goal = (idea && idea.trim()) || `这关让马里奥被${zh}坑：起（教）→ 承（连起来）→ 转（地形变了）→ 合（回程第一个坑）`;
  return { grid: g, beats, notes, goal };
}
/** 起承转合分段：有存的就用存的；没有就按 马里奥出生点 → 宝物 之间平均切 4 段。 */
function beatBounds(g, beats) {
  if (beats && beats.length === 5) return beats;
  const M = find(g, 'M')[0], O = find(g, 'o')[0] || find(g, 'G')[0];
  if (!M || !O) return null;
  const a = Math.min(M[0], O[0]) + 2, b = Math.max(M[0], O[0]) - 1;
  if (b - a < 8) return null;
  return [0, 1, 2, 3, 4].map(i => Math.round(a + (b - a) * i / 4));
}
/** 马里奥每次经过机关的时刻（去程 + 回程都算：回程你还能再用一次）。stops = check().onRoute，route/times = check() 的路线与每点时刻。 */
function routePasses(route, times, stops) {
  const out = [];
  if (!route || !times) return out;
  for (const s of stops) {
    let inPass = false, best = 1e9, bt = 0;
    for (let i = 0; i < route.length; i++) {
      const d = Math.hypot(route[i][0] - s.x, route[i][1] - s.y), near = d <= 1.5;
      if (near) { if (!inPass || d < best) { best = d; bt = times[i]; } inPass = true; }
      else if (inPass) { out.push(bt); inPass = false; best = 1e9; }
    }
    if (inPass) out.push(bt);
  }
  return out.sort((a, b) => a - b);
}
/** 节奏：机关前后 1 秒算"紧张"，其余是"喘气"。连续紧张 ≥ 8 秒 → 提醒加空地；连续 ≥ 10 秒没事 → 提醒太空。 */
const RHYTHM = { busyHalf: 1, maxBusy: 8, maxIdle: 10, startGrace: 4 };
function rhythm(stops, total) {
  const segs = [], warn = [];
  if (!(total > 0)) return { segs, warn };
  const iv = stops.map(t => [Math.max(0, t - RHYTHM.busyHalf), Math.min(total, t + RHYTHM.busyHalf)]).sort((a, b) => a[0] - b[0]);
  const busy = []; for (const s of iv) { const l = busy[busy.length - 1]; if (l && s[0] <= l[1]) l[1] = Math.max(l[1], s[1]); else busy.push(s.slice()); }
  let t = 0;
  for (const [a, b] of busy) { if (a > t) segs.push({ a: t, b: a, busy: false }); segs.push({ a, b, busy: true }); t = b; }
  if (t < total) segs.push({ a: t, b: total, busy: false });
  for (const s of segs) {
    const len = s.b - s.a;
    if (s.busy && len >= RHYTHM.maxBusy) warn.push({ at: s.a, t: `${s.a.toFixed(0)}–${s.b.toFixed(0)} 秒连续 ${len.toFixed(0)} 秒都在机关里，没有喘气的地方：中间空出 3–5 格` });
    if (!s.busy && len >= RHYTHM.maxIdle && s.b > RHYTHM.startGrace + 0.01) warn.push({ at: s.a, t: `${s.a.toFixed(0)}–${s.b.toFixed(0)} 秒连续 ${len.toFixed(0)} 秒什么都没发生：这里可以加一个机关（或者是故意留的长休息）` });
  }
  return { segs, warn };
}
/** 转移点：伪装着站着不动他不会怀疑，但你"走过去"的路上被看见会起疑。机关 5 格（你的操控范围）内没有草丛/箱子/墙挡着 → 提示"先伪装好等他来"。 */
const COVER_CHARS = 'bcU12'; // 草丛、箱子、油桶；随机槽位 1（箱子或草丛）/ 2（草丛或空）也算
function coverHints(g, stops) {
  const h = g.length, out = [];
  for (const s of stops) {
    const row = h - 1 - s.y; let ok = false;
    for (let dy = -1; dy <= 2 && !ok; dy++) for (let dx = -5; dx <= 5 && !ok; dx++) {
      if (!dx && !dy) continue; const r = row - dy, x = s.x + dx; if (r < 0 || r >= h || x <= 0 || x >= g[0].length - 1) continue;
      if (COVER_CHARS.includes(g[r][x]) || (g[r][x] === 'W' && r > 0 && r < h - 1)) ok = true; // 房间里的隔墙也能挡视线
    }
    if (!ok && !out.some(o => o.c === s.c && Math.abs(o.x - s.x) <= 1 && o.y === s.y)) out.push(s); // 一整段塌桥/裂缝地板只提示一次
  }
  return out;
}

if (typeof module !== 'undefined') module.exports = { makeWorld, check, path, placementIssues, stripSlots, find, selectAt, selBox, clampMove, moveBlock, copyBlock, pasteBlock, clearBlock, cameraPlan, PATTERNS, stampPattern, wizardLevel, wizardRecipe, beatBounds, rhythm, routePasses, coverHints, WIZ_STARS, WIZ_LENGTH };


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
    if (d.type === 'mariotrickster-levelpack' || Array.isArray(d.levels) && d.levels.length && d.levels[0].grid || Array.isArray(d.overworlds)) return { kind: 'pack', pack: d };
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
