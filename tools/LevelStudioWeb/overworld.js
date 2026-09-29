// ── S210 大地图（星露谷视角小镇）纯逻辑：逐行移植 Assets/Scripts/Overworld/OverworldMap.cs（同样的检查文字、同样的寻路顺序；verify.sh 逐项对照）──
// OW_TILES 由 build.py 从 OverworldCatalog.cs 生成。
const OW = { MinW: 16, MinH: 12, MaxW: 96, MaxH: 64, DayStart: 360, DayEnd: 1320, LatestDoor: 1200, NightStart: 1140, MaxPickups: 3,
  Rules: { marioSpeed: 3.4, tricksterSpeed: 5.0, minutesPerSecond: 4.0, visitMinutes: 60.0 } };
const owTile = c => { if (c === ' ') c = '.'; if (c >= '1' && c <= '9') c = '1'; return OW_TILES.find(t => t.c === c) || null; };
const owIsDoor = c => c >= '1' && c <= '9';
const owSolid = c => { const t = owTile(c); return !t || t.solid; };
const owSight = c => { const t = owTile(c); return !t || t.sight; };
const owCost = c => { const t = owTile(c); return !t || t.solid ? 0 : t.cost; };
const owSpeed = c => c === 'g' ? 0.55 : c === '"' ? 0.8 : 1.0;
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
      if ((xx !== c[0] || yy !== c[1]) && '"ct'.includes(owAt(m, xx, yy)) && !(xx <= 0 || yy <= 0 || xx >= w - 1 || yy >= h - 1)) { cover = true; break; }
    if (!cover) I(`门 ${n} 附近 4 格内没有高草/木箱/树：你在门口等他时没地方躲`, c[0], c[1]);
  }
  for (const d of m.doors) if (!doorCells.has(d.n)) Wn(`门 ${d.n} 有设置但地图上没画（多余的设置会被忽略）`);
  const byT = new Map(); for (const d of m.doors) if (doorCells.has(d.n)) { if (!byT.has(d.minute)) byT.set(d.minute, []); byT.get(d.minute).push(d); }
  for (const [t, g] of byT) if (g.length > 1) E(`门 ${g.map(d => d.n).join('、')} 的时间都是 ${owClock(t)}：每扇门的时间要不一样（马里奥一次只去一扇）`);
  if (!owPath(m, home, tsp)) E('你的出生点和马里奥的家不连通');
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
function owLos(m, ax, ay, bx, by) { const dx = bx - ax, dy = by - ay, dist = Math.hypot(dx, dy), steps = Math.max(1, Math.ceil(dist * 4)); const sx = Math.floor(ax), sy = Math.floor(ay), ex = Math.floor(bx), ey = Math.floor(by); for (let i = 1; i < steps; i++) { const t = i / steps, cx = Math.floor(ax + dx * t), cy = Math.floor(ay + dy * t); if ((cx === sx && cy === sy) || (cx === ex && cy === ey)) continue; if (owSight(owAt(m, cx, cy))) return false; } return true; }

/** Tiled / CSV 数字 → 大地图字符的默认对应（导入时还能在对话框里改）。 */
const OW_TILED_DEFAULT = { 0: '.', 1: '.', 2: '=', 3: 'W', 4: 't', 5: 'w', 6: 'f', 7: '"', 8: 'c', 9: 'g', 10: 'i', 11: 'n', 12: '?', 13: 'M', 14: 'T' };
function owFromNumbers(nums, map) { map = map || OW_TILED_DEFAULT; return nums.map(r => r.map(v => map[v] !== undefined ? map[v] : '.').join('')); }

if (typeof module !== 'undefined') module.exports = { OW, owParse, owToText, owFromJson, owToJson, owCheck, owSchedule, owPath, owLead, owClock, owNewMap, owIssueText, owFind, owAt, owTile };
