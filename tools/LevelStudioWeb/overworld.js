// ── S210 大地图（星露谷视角小镇）纯逻辑：逐行移植 Assets/Scripts/Overworld/OverworldMap.cs（同样的检查文字、同样的寻路顺序；verify.sh 逐项对照）──
// OW_TILES 由 build.py 从 OverworldCatalog.cs 生成。
const OW = { MinW: 16, MinH: 12, MaxW: 192, MaxH: 128, DayStart: 360, DayEnd: 1320, LatestDoor: 1200, NightStart: 1140, MaxPickups: 3,
  Rules: { marioSpeed: 3.4, tricksterSpeed: 5.0, minutesPerSecond: 4.0, visitMinutes: 60.0 } };
const owTile = c => { if (c === ' ') c = '.'; if (c >= '1' && c <= '9') c = '1'; return OW_TILES.find(t => t.c === c) || null; };
const owIsDoor = c => c >= '1' && c <= '9';
const owSolid = c => { const t = owTile(c); return !t || t.solid; };
const owSight = c => { const t = owTile(c); return !t || t.sight; };
const owCost = c => { const t = owTile(c); return !t || t.solid ? 0 : t.cost; };
const owSpeed = c => c === 'g' ? 0.55 : c === '"' ? 0.8 : c === '^' ? 0.75 : 1.0;
const pad2 = n => String(n).padStart(2, '0');
function owClock(min) { min = Math.max(0, Math.floor(min)); return `${pad2(Math.floor(min / 60))}:${pad2(min % 60)}`; }
function owParseClock(s) { s = String(s || '').trim().replace('：', ':'); const p = s.split(':'); if (p.length !== 2 || !/^\d+$/.test(p[0]) || !/^\d+$/.test(p[1])) return null; const h = +p[0], m = +p[1]; if (h > 23 || m > 59) return null; return h * 60 + m; }
function owAt(m, x, y) { const r = m.rows.length - 1 - y; if (r < 0 || r >= m.rows.length || x < 0 || x >= m.rows[r].length) return 't'; return m.rows[r][x]; }
function owFind(m, c) { const l = []; const H = m.rows.length; for (let r = 0; r < H; r++) for (let x = 0; x < m.rows[r].length; x++) if (m.rows[r][x] === c) l.push([x, H - 1 - r]); return l; }
const owW = m => m.rows.length ? m.rows[0].length : 0;
const owWalk = (m, x, y) => x >= 0 && y >= 0 && x < owW(m) && y < m.rows.length && !owSolid(owAt(m, x, y));

function owParse(text) {
  const m = { kind: 'overworld', name: '', goal: '', id: '', rows: [], doors: [], notes: [] }; const grid = [];
  for (const raw of String(text || '').replace(/\r/g, '').replace(/\uFEFF/g, '').split('\n')) {
    if (raw.startsWith('#')) {
      const line = raw.trimEnd();
      if (line.startsWith('# Overworld:')) m.name = line.slice(12).trim();
      else if (line.startsWith('# Goal:')) m.goal = line.slice(7).trim();
      else if (line.startsWith('# Source:')) m.id = line.slice(9).trim();
      else if (line.startsWith('# Door:')) {
        const p = line.slice(7).split('|'); const n = parseInt(p[0].trim(), 10);
        if (n >= 1 && n <= 9 && /^\d+$/.test(p[0].trim())) { const d = { n, minute: 480, room: '' }; if (p.length >= 2) { const mm = owParseClock(p[1]); if (mm !== null) d.minute = mm; } if (p.length >= 3) d.room = p.slice(2).join('|').trim(); m.doors = m.doors.filter(o => o.n !== n); m.doors.push(d); }
      } else if (line.startsWith('# Note: (')) {
        const close = line.indexOf(')'); const xy = close > 9 ? line.slice(9, close).split(',') : [];
        if (xy.length === 2 && /^-?\d+$/.test(xy[0]) && /^-?\d+$/.test(xy[1])) m.notes.push({ x: +xy[0], y: +xy[1], text: line.slice(close + 1).trim() });
      }
      continue;
    }
    const row = raw.trimEnd(); if (row.length) grid.push(row.replace(/ /g, '.'));
  }
  const w = grid.reduce((a, r) => Math.max(a, r.length), 0); m.rows = grid.map(r => r.padEnd(w, '.'));
  m.doors.sort((a, b) => a.n - b.n); return m;
}
const owOne = s => String(s || '').replace(/[\r\n]/g, ' ').trim();
function owToText(m) {
  const L = [`# Overworld: ${owOne(m.name || '未命名小镇')}`];
  if (m.goal) L.push(`# Goal: ${owOne(m.goal)}`); if (m.id) L.push(`# Source: ${owOne(m.id)}`);
  for (const d of m.doors.slice().sort((a, b) => a.n - b.n)) L.push(`# Door: ${d.n} | ${owClock(d.minute)} | ${owOne(d.room).replace(/\|/g, '/')}`);
  for (const n of m.notes) L.push(`# Note: (${n.x},${n.y}) ${owOne(n.text)}`);
  return L.concat(m.rows).join('\n') + '\n';
}
function owFromJson(d) {
  const m = { kind: 'overworld', name: d.name || '', goal: d.goal || '', id: d.id || '', rows: (d.grid || []).map(r => String(r).replace(/ /g, '.')), doors: [], notes: [] };
  const w = m.rows.reduce((a, r) => Math.max(a, r.length), 0); m.rows = m.rows.map(r => r.padEnd(w, '.'));
  for (const o of d.doors || []) { const n = Math.floor(+o.n || 0); if (n < 1 || n > 9) continue; const mm = owParseClock(o.time); m.doors = m.doors.filter(x => x.n !== n); m.doors.push({ n, minute: mm === null ? 480 : mm, room: o.room || '' }); }
  for (const o of d.notes || []) m.notes.push({ x: +o.x | 0, y: +o.y | 0, text: o.text || '' });
  m.doors.sort((a, b) => a.n - b.n); return m;
}
const owToJson = m => ({ kind: 'overworld', id: m.id || '', name: m.name || '', goal: m.goal || '', grid: m.rows.slice(), doors: m.doors.slice().sort((a, b) => a.n - b.n).map(d => ({ n: d.n, time: owClock(d.minute), room: d.room || '' })), notes: m.notes.slice() });
function owNewMap(w, h) { w = Math.max(OW.MinW, Math.min(OW.MaxW, w)); h = Math.max(OW.MinH, Math.min(OW.MaxH, h)); const rows = []; for (let r = 0; r < h; r++) rows.push(r === 0 || r === h - 1 ? 't'.repeat(w) : 't' + '.'.repeat(w - 2) + 't'); return rows; }

// S217：往外扩展 / 裁掉（大世界）。逐行移植 C# OverworldMap.Resize（verify 逐字对照）。返回 {ok, why, lost}；ok 时就地改 m.rows / m.notes。
function owResize(m, left, right, top, bottom) {
  const w = owW(m), h = m.rows.length, nw = w + left + right, nh = h + top + bottom;
  if (!w || !h) return { ok: false, why: '画布是空的', lost: 0 };
  if (nw < OW.MinW || nh < OW.MinH) return { ok: false, why: `太小了：至少 ${OW.MinW} 宽 × ${OW.MinH} 高（改完会是 ${nw}×${nh}）`, lost: 0 };
  if (nw > OW.MaxW || nh > OW.MaxH) return { ok: false, why: `太大了：最多 ${OW.MaxW} 宽 × ${OW.MaxH} 高（改完会是 ${nw}×${nh}）。更大的世界请拆成几张小镇`, lost: 0 };
  const old = m.rows.map(r => r.split(''));
  for (let r = 0; r < h; r++) for (let x = 0; x < w; x++) {
    const onL = x === 0, onR = x === w - 1, onT = r === 0, onB = r === h - 1;
    if (!(onL || onR || onT || onB) || old[r][x] !== 't') continue;
    if ((!onL || left > 0) && (!onR || right > 0) && (!onT || top > 0) && (!onB || bottom > 0)) old[r][x] = '.';
  }
  let lost = 0;
  for (let r = 0; r < h; r++) for (let x = 0; x < w; x++) { const nx = x + left, nr = r + top; if ((nx < 0 || nr < 0 || nx >= nw || nr >= nh) && old[r][x] !== '.' && old[r][x] !== 't') lost++; }
  const g = [];
  for (let r = 0; r < nh; r++) {
    const row = [];
    for (let x = 0; x < nw; x++) {
      const ox = x - left, oy = r - top; let c = ox >= 0 && oy >= 0 && ox < w && oy < h ? old[oy][ox] : '.';
      if ((x === 0 || r === 0 || x === nw - 1 || r === nh - 1) && !owSolid(c)) { if (c !== '.') lost++; c = 't'; }
      row.push(c);
    }
    g.push(row.join(''));
  }
  m.rows = g;
  m.notes = m.notes.map(n => ({ x: n.x + left, y: n.y + bottom, text: n.text })).filter(n => n.x >= 0 && n.y >= 0 && n.x < nw && n.y < nh);
  return { ok: true, why: '', lost };
}

// Dijkstra，与 C# MinHeap 同样的平手规则（先比代价，再比格子编号）→ 路线逐格一致
const ODX = [1, -1, 0, 0], ODY = [0, 0, 1, -1];
function owPath(m, a, b) {
  const w = owW(m), h = m.rows.length;
  if (!owWalk(m, a[0], a[1]) || !owWalk(m, b[0], b[1])) return null;
  const dist = new Array(w * h).fill(Infinity), prev = new Int32Array(w * h).fill(-1), heap = [];
  const less = (p, q) => p[0] < q[0] || (p[0] === q[0] && p[1] < q[1]);
  const push = (d, i) => { heap.push([d, i]); let c = heap.length - 1; while (c > 0) { const p = (c - 1) >> 1; if (!less(heap[c], heap[p])) break; [heap[c], heap[p]] = [heap[p], heap[c]]; c = p; } };
  const pop = () => { const top = heap[0], last = heap.pop(); if (heap.length) { heap[0] = last; let c = 0; for (;;) { const l = c * 2 + 1, r = l + 1; let mm = c; if (l < heap.length && less(heap[l], heap[mm])) mm = l; if (r < heap.length && less(heap[r], heap[mm])) mm = r; if (mm === c) break; [heap[c], heap[mm]] = [heap[mm], heap[c]]; c = mm; } } return top; };
  const s = a[1] * w + a[0], goal = b[1] * w + b[0]; dist[s] = 0; push(0, s);
  while (heap.length) {
    const [d, i] = pop(); if (d > dist[i]) continue; if (i === goal) break;
    const x = i % w, y = (i / w) | 0;
    for (let k = 0; k < 4; k++) { const nx = x + ODX[k], ny = y + ODY[k]; if (!owWalk(m, nx, ny)) continue; const j = ny * w + nx, nd = d + owCost(owAt(m, nx, ny)); if (nd < dist[j]) { dist[j] = nd; prev[j] = i; push(nd, j); } }
  }
  if (dist[goal] === Infinity) return null;
  const p = []; for (let i = goal; i !== -1; i = prev[i]) p.push([i % w, (i / w) | 0]); return p.reverse();
}
function owWalkSeconds(m, p, speed) { if (!p) return -1; let t = 0; for (let i = 1; i < p.length; i++) t += 1.0 / (speed * owSpeed(owAt(m, p[i][0], p[i][1]))); return t; }

function owSchedule(m, R) {
  const sc = { stops: [], homePath: null, homeArrive: 0, ok: true };
  const home = owFind(m, 'M'), tsp = owFind(m, 'T'); if (home.length !== 1) { sc.ok = false; return sc; }
  let at = home[0], tAt = tsp.length === 1 ? tsp[0] : home[0], now = OW.DayStart;
  for (const d of m.doors.slice().sort((a, b) => a.minute - b.minute || a.n - b.n)) {
    const cells = owFind(m, String(d.n)); if (cells.length !== 1) { sc.ok = false; continue; }
    const p = owPath(m, at, cells[0]); if (!p) { sc.ok = false; continue; }
    const st = { door: d, cell: cells[0], path: p, depart: Math.max(now, d.minute) };
    st.marioSeconds = owWalkSeconds(m, p, R.marioSpeed); st.arrive = st.depart + st.marioSeconds * R.minutesPerSecond; st.leave = st.arrive + R.visitMinutes;
    st.tricksterSeconds = owWalkSeconds(m, owPath(m, tAt, cells[0]), R.tricksterSpeed);
    sc.stops.push(st); at = cells[0]; tAt = cells[0]; now = st.leave;
  }
  sc.homePath = owPath(m, at, home[0]);
  sc.homeArrive = now + (sc.homePath ? owWalkSeconds(m, sc.homePath, R.marioSpeed) * R.minutesPerSecond : 0);
  if (!sc.homePath || sc.homeArrive > OW.DayEnd) sc.ok = false; return sc;
}
function owLead(sc, k, mps) { const st = sc.stops[k]; const prevFree = k === 0 ? OW.DayStart : sc.stops[k - 1].leave; return (st.arrive - (prevFree + st.tricksterSeconds * mps)) / mps; }
const f1 = v => (Math.round(Math.abs(v) * 10) / 10).toFixed(1); // C# {0:0.0}

/** roomOk(name) → null = OK，否则原因。返回 { issues:[{sev,x,y,text}], schedule, playable, headline } */
function owCheck(m, R, roomOk) {
  R = R || OW.Rules; const issues = [];
  const E = (t, x = -1, y = -1) => issues.push({ sev: 'Error', x, y, text: t }), Wn = (t, x = -1, y = -1) => issues.push({ sev: 'Warn', x, y, text: t }), I = (t, x = -1, y = -1) => issues.push({ sev: 'Info', x, y, text: t });
  const done = sc => { const errors = issues.filter(i => i.sev === 'Error').length; return { issues, schedule: sc || null, playable: errors === 0, headline: errors === 0 ? `✓ 可以试玩（${issues.filter(i => i.sev === 'Warn').length} 个提醒）` : `✗ ${errors} 个问题要改` }; };
  const playable = () => !issues.some(i => i.sev === 'Error');
  const w = owW(m), h = m.rows.length;
  if (!w || !h) { E('画布是空的'); return done(); }
  if (m.rows.some(r => r.length !== w)) { E('每一行长度要一样'); return done(); }
  if (w < OW.MinW || h < OW.MinH) E(`小镇太小：至少 ${OW.MinW} 宽 × ${OW.MinH} 高（现在 ${w}×${h}）`);
  if (w > OW.MaxW || h > OW.MaxH) E(`小镇太大：最多 ${OW.MaxW} 宽 × ${OW.MaxH} 高（现在 ${w}×${h}）`);
  for (let r = 0; r < h; r++) for (let x = 0; x < w; x++) if (!owTile(m.rows[r][x])) { E(`不认识的字符 '${m.rows[r][x]}'（大地图只能用大地图的格子）`, x, h - 1 - r); return done(); }
  let frame = true;
  for (let x = 0; x < w && frame; x++) if (!owSolid(m.rows[0][x]) || !owSolid(m.rows[h - 1][x])) frame = false;
  for (let r = 0; r < h && frame; r++) if (!owSolid(m.rows[r][0]) || !owSolid(m.rows[r][w - 1])) frame = false;
  if (!frame) E('最外一圈必须挡路（树 t / 房屋 W / 水 w / 栅栏 f），不然会走出地图');
  for (const c of 'MT') { const n = owFind(m, c).length; if (n !== 1) E(`需要且只能有一个${owTile(c).zh} ${c}（现在 ${n} 个）`); }
  const doorCells = new Map();
  for (let n = 1; n <= 9; n++) { const cs = owFind(m, String(n)); if (cs.length > 1) E(`门 ${n} 画了 ${cs.length} 个：每个数字只能用一次`, cs[1][0], cs[1][1]); if (cs.length >= 1) doorCells.set(n, cs[0]); }
  if (!doorCells.size) E('至少要有一扇门（数字 1–9）：门连到横版房间，马里奥每天去门里拿宝');
  const pickups = owFind(m, '?').length; if (pickups > OW.MaxPickups) E(`道具箱最多 ${OW.MaxPickups} 个（现在 ${pickups} 个）`);
  owPropsCheckCounts(m, E, Wn); // S218 大机关
  if (!playable()) return done();
  const home = owFind(m, 'M')[0], tsp = owFind(m, 'T')[0];
  for (const [n, c] of [...doorCells].sort((a, b) => a[0] - b[0])) {
    const d = m.doors.find(o => o.n === n);
    if (!d || !String(d.room || '').trim()) { E(`门 ${n} 还没连房间（右边选它通到哪个横版关卡）`, c[0], c[1]); continue; }
    if (d.minute < OW.DayStart || d.minute > OW.LatestDoor) E(`门 ${n} 的时间 ${owClock(d.minute)} 要在 06:00–20:00 之间`, c[0], c[1]);
    if (roomOk) { const why = roomOk(d.room); if (why) E(`门 ${n} 连的房间「${d.room}」${why}`, c[0], c[1]); }
    if (!owPath(m, home, c)) E(`马里奥从家走不到门 ${n}`, c[0], c[1]);
    if (!owPath(m, tsp, c)) E(`你（捣蛋者）走不到门 ${n}`, c[0], c[1]);
    let nearHouse = false; for (let k = 0; k < 4; k++) if (owAt(m, c[0] + ODX[k], c[1] + ODY[k]) === 'W') nearHouse = true;
    if (!nearHouse) Wn(`门 ${n} 旁边没有房屋 W：画在房子墙面前一格，玩家一眼就知道这是门`, c[0], c[1]);
    let cover = false;
    for (let yy = c[1] - 4; yy <= c[1] + 4 && !cover; yy++) for (let xx = c[0] - 4; xx <= c[0] + 4; xx++)
      if ((xx !== c[0] || yy !== c[1]) && '"ct^h'.includes(owAt(m, xx, yy)) && !(xx <= 0 || yy <= 0 || xx >= w - 1 || yy >= h - 1)) { cover = true; break; }
    if (!cover) I(`门 ${n} 附近 4 格内没有高草/木箱/树/山丘/山洞：你在门口等他时没地方躲`, c[0], c[1]);
  }
  for (const d of m.doors) if (!doorCells.has(d.n)) Wn(`门 ${d.n} 有设置但地图上没画（多余的设置会被忽略）`);
  const byT = new Map(); for (const d of m.doors) if (doorCells.has(d.n)) { if (!byT.has(d.minute)) byT.set(d.minute, []); byT.get(d.minute).push(d); }
  for (const [t, g] of byT) if (g.length > 1) E(`门 ${g.map(d => d.n).join('、')} 的时间都是 ${owClock(t)}：每扇门的时间要不一样（马里奥一次只去一扇）`);
  if (!owPath(m, home, tsp)) E('你的出生点和马里奥的家不连通');
  owPropsCheckReach(m, home, E, Wn); // S218：落点走不回家 = 困住（H1）
  owCheckMountains(m, Wn); // S219
  if (!playable()) return done();
  const sc = owSchedule(m, R);
  if (!sc.homePath) E('马里奥最后回不了家');
  else if (sc.homeArrive > OW.DayEnd) E(`日程太满：没人捣乱时他 ${owClock(sc.homeArrive)} 才到家，要在 ${owClock(OW.DayEnd)} 前（把门的时间提前、少一扇门，或把门放近一点）`);
  for (let k = 0; k < sc.stops.length; k++) {
    const st = sc.stops[k];
    if (k > 0 && st.door.minute < sc.stops[k - 1].leave - 0.01) I(`门 ${st.door.n}：他 ${owClock(sc.stops[k - 1].leave)} 才从上一扇门出来，比 ${owClock(st.door.minute)} 晚，会马上直接过去（你少了准备时间）`, st.cell[0], st.cell[1]);
    const lead = owLead(sc, k, R.minutesPerSecond);
    if (lead < 0) Wn(`门 ${st.door.n}：你全速赶过去也比他晚 ${f1(lead)} 秒——只能用香蕉皮/挑衅拖住他，或把门的时间往后挪`, st.cell[0], st.cell[1]);
  }
  if (owFind(m, 'i').length === 0 && sc.stops.some(s => s.arrive >= OW.NightStart)) I('晚上还有门要去，但地图上没有路灯 i：晚上他看得很近，门口全是暗处（对你很有利）');
  return done(sc);
}
const owIssueText = i => (i.x >= 0 ? `(${i.x},${i.y}) ` : '') + i.text;

// 视线（和 C# CanSee 一样；网页用来画"他看得见的范围"）
const owHeight = c => c === 'A' ? 2 : c === '^' ? 1 : 0;
function owLos(m, ax, ay, bx, by) { const dx = bx - ax, dy = by - ay, dist = Math.hypot(dx, dy), steps = Math.max(1, Math.ceil(dist * 4)); const sx = Math.floor(ax), sy = Math.floor(ay), ex = Math.floor(bx), ey = Math.floor(by); const eye = Math.max(owHeight(owAt(m, sx, sy)), owHeight(owAt(m, ex, ey))); for (let i = 1; i < steps; i++) { const t = i / steps, cx = Math.floor(ax + dx * t), cy = Math.floor(ay + dy * t); if ((cx === sx && cy === sy) || (cx === ex && cy === ey)) continue; const c = owAt(m, cx, cy); if (owSight(c) || owHeight(c) > eye) return false; } return true; }

/** Tiled / CSV 数字 → 大地图字符的默认对应（导入时还能在对话框里改）。 */
const OW_TILED_DEFAULT = { 0: '.', 1: '.', 2: '=', 3: 'W', 4: 't', 5: 'w', 6: 'f', 7: '"', 8: 'c', 9: 'g', 10: 'i', 11: 'n', 12: '?', 13: 'M', 14: 'T' };
function owFromNumbers(nums, map) { map = map || OW_TILED_DEFAULT; return nums.map(r => r.map(v => map[v] !== undefined ? map[v] : '.').join('')); }

if (typeof module !== 'undefined') module.exports = { OW, owParse, owToText, owFromJson, owToJson, owCheck, owSchedule, owPath, owLead, owClock, owNewMap, owResize, owIssueText, owFind, owAt, owTile };
// ── S212 时间滑条：某一分钟马里奥（无人捣乱时）在哪 —— 逐行移植 OverworldGuide.MarioAt / Along（verify.sh 逐项对照）──
function owAlong(m, p, seconds, speed) { if (!p || !p.length) return [0, 0]; let t = 0; for (let i = 1; i < p.length; i++) { t += 1.0 / (speed * owSpeed(owAt(m, p[i][0], p[i][1]))); if (t > seconds + 1e-9) return p[i - 1]; } return p[p.length - 1]; }
function owMarioAt(m, sc, minute, speed, mps) {
  const home = owFind(m, 'M'); const w = { cell: home.length ? home[0] : [0, 0], insideDoor: 0, what: '在家' };
  if (!sc) return w; let at = w.cell, atWhat = '在家';
  for (const s of sc.stops) {
    if (minute < s.depart) { w.cell = at; w.what = atWhat === '在家' ? `在家（${owClock(s.depart)} 出发去门 ${s.door.n}）` : atWhat; return w; }
    if (minute < s.arrive) { w.cell = owAlong(m, s.path, (minute - s.depart) / mps, speed); w.what = `走向门 ${s.door.n}（${owClock(s.arrive)} 到）`; return w; }
    if (minute < s.leave) { w.cell = s.cell; w.insideDoor = s.door.n; w.what = `在门 ${s.door.n} 里面偷东西（${owClock(s.leave)} 出来）`; return w; }
    at = s.cell; atWhat = `刚从门 ${s.door.n} 出来`;
  }
  const homeDepart = sc.stops.length ? sc.stops[sc.stops.length - 1].leave : OW.DayStart;
  if (sc.homePath && minute < sc.homeArrive && minute >= homeDepart) { w.cell = owAlong(m, sc.homePath, (minute - homeDepart) / mps, speed); w.what = `回家路上（${owClock(sc.homeArrive)} 到家）`; return w; }
  w.cell = home.length ? home[0] : at; w.what = '到家了'; return w;
}
// ── S215 一天总览：逐行移植 Assets/Scripts/Overworld/CampaignLedger.cs（verify.sh 逐项对照）──
const owShortZh = zh => { const i = zh.indexOf('（'); return i > 0 ? zh.slice(0, i) : zh; };
function owLedger(m, W, resolve, bombsPerRound, maxBonus) {
  const rep = { rooms: [], warnings: [], allKinds: [], maxBombs: bombsPerRound + maxBonus }, seen = new Set();
  const counts = e => e && (e.r === 'PlayerPrank' || e.c === 'R' || e.c === 'U');
  const order = []; for (const e of ELEMENTS) if (counts(e) && !order.includes(e.k)) order.push(e.k);
  for (const d of m.doors.slice().sort((a, b) => a.minute - b.minute || a.n - b.n)) {
    if (owFind(m, String(d.n)).length !== 1) continue;
    const r = { door: d.n, clock: owClock(d.minute), room: d.room, missing: false, kinds: [], star: '', firstTime: [], pickups: 0, total: 0 };
    const g = resolve(d.room); if (!g) { r.missing = true; rep.rooms.push(r); continue; }
    const cnt = {}, zh = {};
    for (const row of g) for (const c0 of row.replace(/[123]/g, '.')) {
      if (c0 === '?') { r.pickups++; continue; }
      const e = W.info.get(c0); if (!counts(e) || e.proposal) continue;
      cnt[e.k] = (cnt[e.k] || 0) + 1; zh[e.k] = owShortZh(e.zh); r.total++;
    }
    let best = 0; for (const k of order) if (cnt[k]) { r.kinds.push({ key: k, zh: zh[k], n: cnt[k] }); if (cnt[k] > best) { best = cnt[k]; r.star = zh[k]; } }
    for (const k of r.kinds) if (!seen.has(k.key)) { seen.add(k.key); r.firstTime.push(k.zh); rep.allKinds.push(k.zh); }
    rep.rooms.push(r);
  }
  rep.rooms.forEach((r, i) => {
    if (r.missing) { rep.warnings.push(`门 ${r.door}：找不到房间「${r.room}」`); return; }
    if (r.total === 0) rep.warnings.push(`门 ${r.door}「${r.room}」里没有机关：进门只能躲，没有捣蛋的乐趣`);
    if (r.firstTime.length >= 3) rep.warnings.push(`门 ${r.door}「${r.room}」一口气第一次出现 ${r.firstTime.length} 种机关（${r.firstTime.join('、')}）：一次教太多，玩家记不住。前面的门先放一两种`);
    const p = rep.rooms[i - 1]; if (i > 0 && !p.missing && r.star && r.star === p.star) rep.warnings.push(`门 ${p.door} 和门 ${r.door} 主角都是${r.star}：连着两扇门像在重复。换一个房间，或在后一扇门加一种变化`);
  });
  return rep;
}
const owLedgerLines = rep => rep.rooms.map(r => r.missing ? `门${r.door} ?` : `门${r.door} ${r.room} 主角=${r.star} 共${r.total} 道具箱${r.pickups} 新=${r.firstTime.join('、')} [${r.kinds.map(k => k.zh + k.n).join(' ')}]`).concat(rep.warnings);
// ── S218 小镇大机关（巨炮 K + 靶心 X / 滚石 O / 水塔 U）+ 天气：逐行移植 Assets/Scripts/Overworld/OverworldProps.cs（verify.sh 逐字对照）──
const OWP = { Muzzle: 3, MaxShot: 96, WindShift: 3, FloodRadius: 3, MaxRoll: 64, MaxBig: 12, ChainRadius: 1.5, DX: [1, -1, 0, 0], DY: [0, 0, 1, -1], DirZh: ['右', '左', '上', '下'] };
const owIsBig = c => c === 'K' || c === 'O' || c === 'U';
const owCellS = c => `(${c[0]},${c[1]})`;
const owLabel = (m, c) => { const ch = owAt(m, c[0], c[1]), t = owTile(ch); return `${t ? t.zh : '?'}${ch}(${c[0]},${c[1]})`; };
const owSame = (a, b) => a[0] === b[0] && a[1] === b[1];
function owBigAll(m) { const l = [], H = m.rows.length; for (let r = 0; r < H; r++) for (let x = 0; x < m.rows[r].length; x++) if (owIsBig(m.rows[r][x])) l.push([x, H - 1 - r]); return l; }
function owAim(m, k) {
  let target = k, dir = -1, dist = Infinity; const w = owW(m), h = m.rows.length;
  for (let d = 0; d < 4; d++) for (let s = 1; s <= OWP.MaxShot; s++) {
    const x = k[0] + OWP.DX[d] * s, y = k[1] + OWP.DY[d] * s; if (x < 0 || y < 0 || x >= w || y >= h) break;
    if (owAt(m, x, y) === 'X') { if (s < dist) { dist = s; dir = d; target = [x, y]; } break; }
  }
  return dir >= 0 ? { target, dir, dist } : null;
}
function owMuzzle(m, k, dir) { const l = []; for (let s = 1; s <= OWP.Muzzle; s++) { const x = k[0] + OWP.DX[dir] * s, y = k[1] + OWP.DY[dir] * s; if (!owWalk(m, x, y)) break; l.push([x, y]); } return l; }
function owLanding(m, tg, wind) {
  let px = tg[0], py = tg[1]; if (wind >= 0) { px += OWP.DX[wind] * OWP.WindShift; py += OWP.DY[wind] * OWP.WindShift; }
  px = Math.max(1, Math.min(owW(m) - 2, px)); py = Math.max(1, Math.min(m.rows.length - 2, py));
  for (let r = 0; r <= 12; r++) for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) { if (Math.max(Math.abs(dx), Math.abs(dy)) !== r) continue; if (owWalk(m, px + dx, py + dy)) return [px + dx, py + dy]; }
  return tg;
}
const owSmash = c => c === 'c' || c === 'f';
function owLane(m, o, dir, smashed) {
  const l = [], w = owW(m), h = m.rows.length;
  for (let s = 1; s <= OWP.MaxRoll; s++) {
    const x = o[0] + OWP.DX[dir] * s, y = o[1] + OWP.DY[dir] * s; if (x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1) break;
    const c = owAt(m, x, y); if (owWalk(m, x, y)) l.push([x, y]); else if (owSmash(c)) { l.push([x, y]); if (smashed) smashed.push([x, y]); } else break;
  }
  return l;
}
const owFloodable = c => c === '.' || c === '=' || c === '"';
function owFloodCells(m, u, radius) { const l = [], w = owW(m), h = m.rows.length; for (let y = u[1] - radius; y <= u[1] + radius; y++) for (let x = u[0] - radius; x <= u[0] + radius; x++) { if ((x - u[0]) ** 2 + (y - u[1]) ** 2 > radius * radius) continue; if (x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1) continue; if (owFloodable(owAt(m, x, y))) l.push([x, y]); } return l; }
function owChainTargets(m, ix, iy, radius) { radius = radius === undefined ? OWP.ChainRadius : radius; const l = []; owBigAll(m).forEach((c, k) => { const dx = c[0] + 0.5 - ix, dy = c[1] + 0.5 - iy, d = dx * dx + dy * dy; if (d <= radius * radius + 1e-9) l.push({ d, k, c }); }); l.sort((a, b) => a.d - b.d || a.k - b.k); return l.map(t => t.c); }
function owPropsCheckCounts(m, E, Wn) {
  const all = owBigAll(m);
  if (all.length > OWP.MaxBig) E(`大机关（巨炮 / 滚石 / 水塔）最多 ${OWP.MaxBig} 个（现在 ${all.length} 个）：太多了玩家记不住，也看不清谁连着谁`, -1, -1);
  for (const c of all) {
    if (owAt(m, c[0], c[1]) !== 'K') continue; const a = owAim(m, c);
    if (!a) { let any = false; for (let d = 0; d < 4; d++) if (owMuzzle(m, c, d).length) any = true; if (!any) E('巨炮 K 四面都被挡住：炮口前要空地（最好 3 格）', c[0], c[1]); else Wn('巨炮 K 同一行 / 同一列找不到靶心 X：只能坐进去自己瞄（远程按 L / 被连锁震响时没有默认落点，会打 12 格远）', c[0], c[1]); continue; }
    if (!owMuzzle(m, c, a.dir).length) E('巨炮 K 朝靶心那边第一格就被挡住：炮口前要空地（最好 3 格）', c[0], c[1]);
  }
}
function owPropsCheckReach(m, home, E, Wn) {
  for (const c of owBigAll(m)) {
    const ch = owAt(m, c[0], c[1]);
    if (ch === 'K') { const a = owAim(m, c); if (!a) continue; for (let w = -1; w < 4; w++) { const l = owLanding(m, a.target, w); if (!owPath(m, l, home)) { E(`巨炮 K 的落点 ${owCellS(l)}${w < 0 ? '' : '（大风往' + OWP.DirZh[w] + '吹）'}走不回马里奥的家：被轰过去就困住了（把靶心 X 挪到开阔处）`, c[0], c[1]); break; } } }
    else if (ch === 'O') { let best = 0; for (let d = 0; d < 4; d++) best = Math.max(best, owLane(m, c, d).length); if (best < 3) Wn(`滚石 O 四个方向最多只能滚 ${best} 格：放在长直路的一头才有用`, c[0], c[1]); }
  }
}
function owTriggers(m, c) {
  const res = [], ch = owAt(m, c[0], c[1]);
  const add = (ix, iy) => { for (const t of owChainTargets(m, ix, iy)) if (!owSame(t, c) && !res.some(r => owSame(r, t))) res.push(t); };
  if (ch === 'K') { const a = owAim(m, c); if (a) { const l = owLanding(m, a.target, -1); add(l[0] + 0.5, l[1] + 0.5); } }
  else if (ch === 'O') for (let d = 0; d < 4; d++) { const lane = owLane(m, c, d); if (lane.length) { const e = lane[lane.length - 1]; add(e[0] + 0.5, e[1] + 0.5); } }
  return res;
}
function owDoorsNear(m, c, r) { const l = []; for (let n = 1; n <= 9; n++) for (const d of owFind(m, String(n))) if (Math.abs(d[0] - c[0]) + Math.abs(d[1] - c[1]) <= r) l.push(d); return l; }
function owLongestChain(m) {
  const all = owBigAll(m), edges = new Map(all.map(c => [c + '', owTriggers(m, c)])); let best = [];
  const dfs = path => { if (path.length > best.length) best = path.slice(); for (const n of edges.get(path[path.length - 1] + '')) if (!path.some(p => owSame(p, n))) { path.push(n); dfs(path); path.pop(); } };
  for (const c of all) dfs([c]);
  return best;
}
function owPropsDescribe(m) {
  const lines = [], all = owBigAll(m);
  if (!all.length) { lines.push({ text: '还没有大机关：巨炮 K + 靶心 X、滚石 O、水塔 U 是小镇专用的夸张机关（L 发动）', x: -1, y: -1 }); owDescribeMountains(m, lines); return lines; }
  for (const c of all) {
    const ch = owAt(m, c[0], c[1]); let s;
    if (ch === 'K') {
      const a = owAim(m, c);
      if (!a) s = `${owLabel(m, c)}：同一行 / 列没有靶心 X（坐进去自己瞄）`;
      else {
        const l = owLanding(m, a.target, -1), near = [];
        for (const dc of owDoorsNear(m, l, 3)) near.push('门 ' + owAt(m, dc[0], dc[1]));
        for (const t of owTriggers(m, c)) near.push('震响 ' + owLabel(m, t));
        let peels = 0; for (let yy = l[1] - 1; yy <= l[1] + 1; yy++) for (let xx = l[0] - 1; xx <= l[0] + 1; xx++) if (owAt(m, xx, yy) === 'n') peels++;
        if (peels > 0) near.push(`香蕉皮 ${peels}`);
        let dirs = 0; for (let d = 0; d < 4; d++) if (owMuzzle(m, c, d).length) dirs++;
        s = `${owLabel(m, c)} → 靶心 X(${a.target[0]},${a.target[1]})：往${OWP.DirZh[a.dir]}飞 ${a.dist} 格，炮口 ${owMuzzle(m, c, a.dir).length} 格；落点旁：${near.length ? near.join('、') : '空地'}；坐进去能瞄 ${dirs} 个方向`;
      }
    } else if (ch === 'O') {
      const parts = [];
      for (let d = 0; d < 4; d++) {
        const sm = [], lane = owLane(m, c, d, sm); if (!lane.length) continue;
        const e = lane[lane.length - 1], tr = owChainTargets(m, e[0] + 0.5, e[1] + 0.5).filter(t => !owSame(t, c)).map(t => owLabel(m, t));
        parts.push(`往${OWP.DirZh[d]} ${lane.length} 格` + (sm.length ? `（撞碎 ${sm.length}）` : '') + (tr.length ? ' → 震响 ' + tr.join('、') : ''));
      }
      s = `${owLabel(m, c)}：` + (parts.length ? parts.join('；') : '四面堵死，推不动');
    } else {
      const f = owFloodCells(m, c, OWP.FloodRadius), f2 = owFloodCells(m, c, OWP.FloodRadius + 1), R = OWP.FloodRadius;
      const grass = f.filter(p => owAt(m, p[0], p[1]) === '"').length, path = f.filter(p => owAt(m, p[0], p[1]) === '=').length; let peels = 0;
      for (let yy = c[1] - R; yy <= c[1] + R; yy++) for (let xx = c[0] - R; xx <= c[0] + R; xx++) if ((xx - c[0]) ** 2 + (yy - c[1]) ** 2 <= R * R && owAt(m, xx, yy) === 'n') peels++;
      s = `${owLabel(m, c)}：淹 ${f.length} 格变泥地（雨天 ${f2.length} 格），其中石子路 ${path}` + (grass > 0 ? `、高草 ${grass}（你的藏身处也没了）` : '') + (peels > 0 ? `；冲活香蕉皮 ${peels}` : '');
    }
    lines.push({ text: s, x: c[0], y: c[1] });
  }
  const chain = owLongestChain(m);
  lines.push(chain.length >= 2 ? { text: `最长连锁：${chain.map(c => owLabel(m, c)).join(' → ')}（${chain.length} 连）`, x: chain[0][0], y: chain[0][1] }
    : { text: '还没有连锁：把靶心 X 放在滚石 / 水塔 / 另一门巨炮旁 1 格内，或让滚石滚到头正好撞上它们', x: -1, y: -1 });
  owDescribeMountains(m, lines);
  return lines;
}
// ── S219：巨炮瞄准 / 山地 / 山洞 / 泥石流 / 闪电（逐行移植 OverworldProps.cs）──
Object.assign(OWP, { MinAim: 3, MaxAim: 24, MudLen: 6, LightningRadius: 1.5 });
function owDefaultAim(m, k) { const a = owAim(m, k); if (a) return { dir: a.dir, dist: Math.max(OWP.MinAim, a.dist) }; for (let d = 0; d < 4; d++) if (owMuzzle(m, k, d).length) return { dir: d, dist: 12 }; return { dir: 0, dist: 12 }; }
function owAimStep(m, k, a, key) { if (key < 0 || key > 3) return a; let { dir, dist } = a; const opp = dir ^ 1; if (key === dir) dist = Math.min(Math.max(OWP.MaxAim, dist), dist + 1); else if (key === opp) { if (dist > OWP.MinAim) dist--; else if (owMuzzle(m, k, opp).length) dir = opp; } else if (owMuzzle(m, k, key).length) dir = key; return { dir, dist }; }
const owAimLanding = (m, k, dir, dist, wind) => owLanding(m, [k[0] + OWP.DX[dir] * dist, k[1] + OWP.DY[dir] * dist], wind);
const owAimOk = (m, k, dir, land, home) => owMuzzle(m, k, dir).length > 0 && !!owPath(m, land, home);
const owNextTo = (m, c, k) => { for (let d = 0; d < 4; d++) if (owAt(m, c[0] + OWP.DX[d], c[1] + OWP.DY[d]) === k) return true; return false; };
function owCaveExit(m, c) { const l = owFind(m, 'h'), i = l.findIndex(p => owSame(p, c)); if (i < 0) return null; const j = i ^ 1; return j < l.length ? l[j] : null; }
function owMudDir(m, c) { if (owAt(m, c[0], c[1]) !== '^') return -1; for (let d = 0; d < 4; d++) if (owAt(m, c[0] + OWP.DX[d], c[1] + OWP.DY[d]) === 'A') return d ^ 1; return -1; }
function owMudLane(m, c, smashed) {
  const l = [], dir = owMudDir(m, c); if (dir < 0) return l; const w = owW(m), h = m.rows.length;
  for (let s = 1; s <= OWP.MudLen; s++) { const x = c[0] + OWP.DX[dir] * s, y = c[1] + OWP.DY[dir] * s; if (x <= 0 || y <= 0 || x >= w - 1 || y >= h - 1) break; const ch = owAt(m, x, y); if (owWalk(m, x, y)) l.push([x, y]); else if (owSmash(ch)) { l.push([x, y]); if (smashed) smashed.push([x, y]); } else break; }
  return l;
}
const owMuddable = c => owFloodable(c) || c === '^' || owSmash(c);
function owMudSources(m, ix, iy) { const l = []; for (const c of owFind(m, '^')) { if (owMudDir(m, c) < 0) continue; const dx = c[0] + 0.5 - ix, dy = c[1] + 0.5 - iy; if (dx * dx + dy * dy <= OWP.ChainRadius * OWP.ChainRadius + 1e-9) l.push(c); } return l; }
function owCheckMountains(m, Wn) {
  const caves = owFind(m, 'h');
  if (caves.length % 2 === 1) { const c = caves[caves.length - 1]; Wn(`山洞 h 有 ${caves.length} 个：两个一对，最后一个没配对（按 E 钻不过去）`, c[0], c[1]); }
  for (const c of caves) if (!owNextTo(m, c, 'A')) Wn('山洞 h 旁边没有山 A：画在山脚下，玩家一眼就知道这是洞', c[0], c[1]);
}
function owDescribeMountains(m, lines) {
  const caves = owFind(m, 'h');
  for (let i = 0; i + 1 < caves.length; i += 2) lines.push({ text: `山洞 h(${caves[i][0]},${caves[i][1]}) ⇄ h(${caves[i + 1][0]},${caves[i + 1][1]})：钻进去按 E 从另一头出来（马里奥不知道这条路）`, x: caves[i][0], y: caves[i][1] });
  for (const c of owFind(m, '^')) {
    const d = owMudDir(m, c); if (d < 0) continue; const sm = [], lane = owMudLane(m, c, sm); if (!lane.length) continue;
    const path = lane.filter(p => owAt(m, p[0], p[1]) === '=').length;
    lines.push({ text: `泥石流 ^(${c[0]},${c[1]})：下雨天被冲击 → 往${OWP.DirZh[d]}冲 ${lane.length} 格变泥地` + (path > 0 ? `（石子路 ${path}）` : '') + (sm.length ? `，冲垮 ${sm.length}` : ''), x: c[0], y: c[1] });
  }
  for (const c of owFind(m, 'i')) {
    const near = [];
    for (const t of owChainTargets(m, c[0] + 0.5, c[1] + 0.5, OWP.LightningRadius)) near.push('震响 ' + owLabel(m, t));
    for (const t of owMudSources(m, c[0] + 0.5, c[1] + 0.5)) near.push(`泥石流 ^(${t[0]},${t[1]})`);
    if (near.length) lines.push({ text: `路灯 i(${c[0]},${c[1]})：雷雨天按 L 召唤闪电 → ` + near.join('、'), x: c[0], y: c[1] });
  }
}
// 天气（输入随机）：FNV-1a + xorshift，全部 uint32（和 C# 一样）
function owHash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h = (h ^ s.charCodeAt(i)) >>> 0; h = Math.imul(h, 16777619) >>> 0; } return h; }
// S219：天气池看地图格局（有路灯才有雷雨 5，有 ≥2 个山洞才有酸雨 6）——和 C# OverworldEvents.Pool 一样
function owWeatherPool(m) { const l = [0, 1, 2, 3, 4]; if (owFind(m, 'i').length) l.push(5); if (owFind(m, 'h').length >= 2) l.push(6); return l; }
function owDayOf(name, day, pool) {
  pool = pool || [0, 1, 2, 3, 4];
  let h = owHash(name || ''); h = (h ^ (Math.imul(day, 2654435761 | 0) >>> 0)) >>> 0;
  h = (h ^ (h << 13)) >>> 0; h = (h ^ (h >>> 17)) >>> 0; h = (h ^ (h << 5)) >>> 0;
  return { day, h, kind: day <= 1 ? 0 : pool[h % pool.length], wind: (h >>> 8) % 4 };
}
const owDayOfMap = (m, day) => owDayOf(m.name, day, owWeatherPool(m));
const owWet = d => d.kind === 2 || d.kind === 5 || d.kind === 6;
const OW_WEATHER = ['Clear', 'Wind', 'Rain', 'Fog', 'Market', 'Storm', 'Acid'];
const owMarketDelay = (d, door) => ((d.h >>> (door * 3)) % 3) * 15;
function owApplyDay(m, d) { if (d.kind === 6) { for (const c of owFind(m, '"')) { const r = m.rows.length - 1 - c[1]; m.rows[r] = m.rows[r].slice(0, c[0]) + '.' + m.rows[r].slice(c[0] + 1); } return; } if (d.kind !== 4) return; let prev = -9999; for (const door of m.doors.slice().sort((a, b) => a.minute - b.minute || a.n - b.n)) { let t = door.minute + owMarketDelay(d, door.n); t = Math.max(t, prev + 15); t = Math.min(t, OW.LatestDoor); door.minute = t; prev = t; } }
function owWeatherZh(d) {
  switch (d.kind) {
    case 1: return `🌬 大风（往${OWP.DirZh[d.wind]}吹）：巨炮落点被吹偏 ${OWP.WindShift} 格`;
    case 2: return `🌧 雨天：水塔淹得更大（半径 ${OWP.FloodRadius + 1}），香蕉皮滑得更久，山丘被震会泥石流`;
    case 3: return '🌫 雾天：他只看得见平时 6 成远（你也更好躲）';
    case 4: return '🧺 赶集日：他每扇门晚 0–30 分钟出门（时间表已更新）';
    case 5: return '⛈ 雷雨：在路灯旁按 L 召唤闪电（1.5 格内晕），山丘被震会泥石流';
    case 6: return '☂ 酸雨：高草全枯了（只剩山洞能躲），他打着伞只看得见 7 成远';
    default: return '☀ 晴天：一切照常';
  }
}
function owWeatherPreview(m, from, n) {
  const l = [];
  for (let day = from; day < from + n; day++) {
    const d = owDayOfMap(m, day); let s = `第 ${day} 天 ${owWeatherZh(d)}`;
    if (d.kind === 4) { const c = owParse(owToText(m)); owApplyDay(c, d); s += '：' + c.doors.slice().sort((a, b) => a.minute - b.minute || a.n - b.n).map(x => `门${x.n} ${owClock(x.minute)}`).join(' '); }
    l.push(s);
  }
  return l;
}
