// ── S210 大地图（星露谷视角小镇）纯逻辑：逐行移植 Assets/Scripts/Overworld/OverworldMap.cs（同样的检查文字、同样的寻路顺序；verify.sh 逐项对照）──
// OW_TILES 由 build.py 从 OverworldCatalog.cs 生成。
const OW = { MinW: 16, MinH: 12, MaxW: 192, MaxH: 128, DayStart: 360, DayEnd: 1320, LatestDoor: 1200, NightStart: 1140, MaxPickups: 3, MaxHeartPickups: 3, MaxEnergyPickups: 4, MaxStorms: 4, MaxBolts: 6,
  Rules: { marioSpeed: 3.4, tricksterSpeed: 5.0, minutesPerSecond: 4.0, visitMinutes: 60.0 } };
// S232：小镇速度 / 时间也从 Unity 调参默认值来（同上）
if (typeof TUNING !== 'undefined') OW.Rules = { marioSpeed: TUNING.overworldMarioSpeed.v, tricksterSpeed: TUNING.overworldTricksterSpeed.v, minutesPerSecond: TUNING.overworldMinutesPerSecond.v, visitMinutes: TUNING.overworldVisitMinutes.v };
// S232：数值关系检查（TuningAudit.cs 的同一张规则表）
// S233：TU_OVR = 从 Unity 项目里读到的 RushMarioTuning.asset（你在 Inspector 手动改过的值）；没连 = null，用默认值
var TU_OVR = null;
function tuFromYaml(yaml) { const o = {}; for (const raw of String(yaml || '').replace(/\r/g, '').split('\n')) { if (!raw.startsWith('  ') || raw.startsWith('   ')) continue; const c = raw.indexOf(': '); if (c < 3) continue; const k = raw.slice(2, c), v = raw.slice(c + 2).trim(); if (!k || k.startsWith('m_') || !/^[A-Za-z0-9_]+$/.test(k)) continue; if (/^[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$/.test(v)) o[k] = +v; } return Object.keys(o).sort().reduce((a, k) => (a[k] = o[k], a), {}); }
function tuDiff(asset) { const o = []; if (typeof TUNING === 'undefined') return o; for (const k of Object.keys(TUNING)) { if (asset[k] === undefined || k === 'dataVersion') continue; const e = TUNING[k], v = e.t === 'bool' ? (e.v ? 1 : 0) : e.v; if (Math.abs(asset[k] - v) > 1e-4) o.push(`${k}: 默认 ${tuF(v)} → 你改成 ${tuF(asset[k])}`); } return o; }
function tuApply(asset) { TU_OVR = asset; const g = (k, d) => asset && asset[k] !== undefined ? asset[k] : d; if (typeof TUNING === 'undefined') return; OW.Rules = { marioSpeed: g('overworldMarioSpeed', TUNING.overworldMarioSpeed.v), tricksterSpeed: g('overworldTricksterSpeed', TUNING.overworldTricksterSpeed.v), minutesPerSecond: g('overworldMinutesPerSecond', TUNING.overworldMinutesPerSecond.v), visitMinutes: g('overworldVisitMinutes', TUNING.overworldVisitMinutes.v) }; }
function tuValue(tok) { tok = tok.trim(); let mul = 1; const st = tok.indexOf('*'); if (st > 0) { mul = +tok.slice(st + 1).trim(); tok = tok.slice(0, st).trim(); } if (/^-?\d+(\.\d+)?$/.test(tok)) return +tok * mul; if (TU_OVR && TU_OVR[tok] !== undefined) return TU_OVR[tok] * mul; const e = typeof TUNING !== 'undefined' ? TUNING[tok] : null; if (!e) return null; return (e.t === 'bool' ? (e.v ? 1 : 0) : e.v) * mul; }
const tuF = v => String(Math.round(v * 1000) / 1000);
function tuAudit() {
  return (typeof TUNING_RULES === 'undefined' ? [] : TUNING_RULES).map(line => {
    const p = line.split('|'), ex = p[0].trim(), why = p.length > 1 ? p[1].trim() : '';
    const op = ex.includes('<=') ? '<=' : ex.includes('>=') ? '>=' : ex.includes('<') ? '<' : '>', i = ex.indexOf(op);
    const a = tuValue(ex.slice(0, i)), b = tuValue(ex.slice(i + op.length));
    if (a === null || b === null) return { rule: ex, why, ok: false, detail: '规则里的名字找不到（字段改名了？）' };
    const ok = op === '<' ? a < b : op === '<=' ? a <= b : op === '>' ? a > b : a >= b;
    return { rule: ex, why, ok, detail: `${tuF(a)} ${op} ${tuF(b)}` };
  });
}
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
  const m = { kind: 'overworld', name: '', goal: '', id: '', rows: [], doors: [], notes: [], storms: [], residents: [] }; const grid = [];
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
      } else if (line.startsWith('# Storm:')) { const st = owParseStorm(line.slice(8)); if (st) m.storms.push(st); }
      else if (line.startsWith('# Resident:')) { const rs = owParseResident(line.slice(11)); if (rs) { m.residents = m.residents.filter(o => o.door !== rs.door); m.residents.push(rs); } }
      continue;
    }
    const row = raw.trimEnd(); if (row.length) grid.push(row.replace(/ /g, '.'));
  }
  const w = grid.reduce((a, r) => Math.max(a, r.length), 0); m.rows = grid.map(r => r.padEnd(w, '.'));
  m.doors.sort((a, b) => a.n - b.n); m.residents.sort((a, b) => a.door - b.door); return m;
}
const owOne = s => String(s || '').replace(/[\r\n]/g, ' ').trim();
function owToText(m) {
  const L = [`# Overworld: ${owOne(m.name || '未命名小镇')}`];
  if (m.goal) L.push(`# Goal: ${owOne(m.goal)}`); if (m.id) L.push(`# Source: ${owOne(m.id)}`);
  for (const d of m.doors.slice().sort((a, b) => a.n - b.n)) L.push(`# Door: ${d.n} | ${owClock(d.minute)} | ${owOne(d.room).replace(/\|/g, '/')}`);
  for (const n of m.notes) L.push(`# Note: (${n.x},${n.y}) ${owOne(n.text)}`);
  for (const st of m.storms || []) L.push(`# Storm: ${owStormText(st)}`);
  for (const rs of (m.residents || []).slice().sort((a, b) => a.door - b.door)) L.push(`# Resident: ${owResidentText(rs)}`); // S232
  return L.concat(m.rows).join('\n') + '\n';
}
function owFromJson(d) {
  const m = { kind: 'overworld', name: d.name || '', goal: d.goal || '', id: d.id || '', rows: (d.grid || []).map(r => String(r).replace(/ /g, '.')), doors: [], notes: [], storms: [], residents: [] };
  const w = m.rows.reduce((a, r) => Math.max(a, r.length), 0); m.rows = m.rows.map(r => r.padEnd(w, '.'));
  for (const o of d.doors || []) { const n = Math.floor(+o.n || 0); if (n < 1 || n > 9) continue; const mm = owParseClock(o.time); m.doors = m.doors.filter(x => x.n !== n); m.doors.push({ n, minute: mm === null ? 480 : mm, room: o.room || '' }); }
  for (const o of d.notes || []) m.notes.push({ x: +o.x | 0, y: +o.y | 0, text: o.text || '' });
  for (const o of d.storms || []) { const st = owParseStorm(String(o)); if (st) m.storms.push(st); }
  for (const o of d.residents || []) { const rs = owParseResident(String(o)); if (rs) { m.residents = m.residents.filter(x => x.door !== rs.door); m.residents.push(rs); } }
  m.doors.sort((a, b) => a.n - b.n); m.residents.sort((a, b) => a.door - b.door); return m;
}
const owToJson = m => { const j = { kind: 'overworld', id: m.id || '', name: m.name || '', goal: m.goal || '', grid: m.rows.slice(), doors: m.doors.slice().sort((a, b) => a.n - b.n).map(d => ({ n: d.n, time: owClock(d.minute), room: d.room || '' })), notes: m.notes.slice() }; if ((m.storms || []).length) j.storms = m.storms.map(owStormText); if ((m.residents || []).length) j.residents = m.residents.slice().sort((a, b) => a.door - b.door).map(owResidentText); return j; };
// S232：住户（和 C# OverworldMap.ParseResident / ResidentText 一样）
function owParseResident(s) {
  const p = String(s || '').split('|'); if (p.length < 2 || !/^\s*\d+\s*$/.test(p[0])) return null; const n = +p[0].trim(); if (n < 1 || n > 9) return null;
  const name = owOne(p[1]); if (!name) return null; return { door: n, name, trait: p.length >= 3 ? owOne(p[2]) : '' };
}
const owResidentText = r => `${r.door} | ${owOne(r.name).replace(/\|/g, '/')} | ${owOne(r.trait).replace(/\|/g, '/')}`;
// S220：雷区（和 C# OverworldMap.ParseStorm / StormText 一样）
function owParseStorm(s) {
  const p = String(s || '').split('|'); if (p.length < 3) return null;
  const xy = p[0].split(','); if (xy.length !== 4) return null;
  const v = xy.map(t => t.trim()); if (!v.every(t => /^-?\d+$/.test(t))) return null;
  const mn = p[1].trim(), mx = p[2].trim(); if (!/^-?\d+$/.test(mn) || !/^-?\d+$/.test(mx)) return null;
  const a = v.map(Number);
  return { x0: Math.min(a[0], a[2]), y0: Math.min(a[1], a[3]), x1: Math.max(a[0], a[2]), y1: Math.max(a[1], a[3]), min: +mn, max: +mx, always: p.length >= 4 && p[3].trim() === 'always' };
}
const owStormText = st => `${st.x0},${st.y0},${st.x1},${st.y1} | ${st.min} | ${st.max}` + (st.always ? ' | always' : '');
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
  m.storms = (m.storms || []).map(st => ({ ...st, x0: st.x0 + left, x1: st.x1 + left, y0: st.y0 + bottom, y1: st.y1 + bottom })).filter(st => st.x0 >= 1 && st.y0 >= 1 && st.x1 <= nw - 2 && st.y1 <= nh - 2); // S220
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
  const hearts = owFind(m, '+').length, energy = owFind(m, '*').length; // S220
  if (hearts > OW.MaxHeartPickups) E(`补心 + 最多 ${OW.MaxHeartPickups} 个（现在 ${hearts} 个）：太多了受伤就没意义`);
  if (energy > OW.MaxEnergyPickups) E(`能量 * 最多 ${OW.MaxEnergyPickups} 个（现在 ${energy} 个）：太多了一天能放好几次雷云`);
  owStormCheck(m, E, Wn, I);
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
const owIsBig = c => c === 'K' || c === 'O' || c === 'U' || c === 'B'; // S228：钟楼 B
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
    } else if (ch === 'B') {
      s = `${owLabel(m, c)}：钟一响全镇听见（他停下转头看），不伤人；冲击 ${OWP.ChainRadius} 格内会震响它`;
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
function owWeatherPool(m) { const l = [0, 1, 2, 3, 4]; if (owFind(m, 'i').length || (m.storms || []).length) l.push(5); if (owFind(m, 'h').length >= 2) l.push(6); return l; }
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
    case 5: return '⛈ 雷雨：雷区一阵阵劈闪电（地上闪光 = 快跑），在路灯旁按 L 召唤闪电，山丘被震会泥石流';
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

// ═════════ S220：雷区——逐行移植 C# OverworldStorm（同一个哈希 → 同一批落点，verify 逐字对照）═════════
const OWS = { VolleySeconds: 10, MinutesPerSecond: 4 };
function owStormNearSafe(m, x, y) { for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const c = owAt(m, x + dx, y + dy); if (c === 'M' || c === 'T' || owIsDoor(c)) return true; } return false; }
function owStrikeable(m, st) { const l = [], w = owW(m), h = m.rows.length; for (let y = Math.max(0, st.y0); y <= Math.min(h - 1, st.y1); y++) for (let x = Math.max(0, st.x0); x <= Math.min(w - 1, st.x1); x++) { if (!owWalk(m, x, y) || owStormNearSafe(m, x, y)) continue; l.push([x, y]); } return l; }
function owStormPlus(m, c) { const l = [c]; for (let k = 0; k < 4; k++) { const x = c[0] + ODX[k], y = c[1] + ODY[k]; if (owWalk(m, x, y)) l.push([x, y]); } return l; }
const owXs = h => { h = (h ^ (h << 13)) >>> 0; h = (h ^ (h >>> 17)) >>> 0; h = (h ^ (h << 5)) >>> 0; return h === 0 ? 0x9E3779B9 : h; };
function owStormSeed(name, day, zone, volley) {
  let h = owHash(name || '');
  h = (h ^ (Math.imul(day, 2654435761 | 0) >>> 0)) >>> 0;
  h = (h ^ (Math.imul(zone + 1, 2654435769 | 0) >>> 0)) >>> 0;
  h = (h ^ (Math.imul(volley, 2246822507 | 0) >>> 0)) >>> 0;
  return owXs(owXs(h));
}
function owStormVolley(m, zone, day, volley) {
  const res = []; const st = (m.storms || [])[zone]; if (!st) return res;
  const pool = owStrikeable(m, st); if (!pool.length) return res;
  const lo = Math.max(1, st.min), hi = Math.max(lo, Math.min(OW.MaxBolts, st.max));
  let h = owStormSeed(m.name, day, zone, volley);
  const n = lo + (h % (hi - lo + 1));
  for (let k = 0; k < n && pool.length; k++) { h = owXs(h); const i = h % pool.length; res.push(pool[i]); pool[i] = pool[pool.length - 1]; pool.pop(); }
  return res;
}
function owStormVolleyIndex(minute, zone) { const period = Math.max(1, OWS.VolleySeconds * OWS.MinutesPerSecond); return Math.floor((minute - OW.DayStart) / period + zone * 0.37); }
function owStormCheck(m, E, W, I) {
  const ss = m.storms || []; if (!ss.length) return;
  if (ss.length > OW.MaxStorms) E(`雷区最多 ${OW.MaxStorms} 个（现在 ${ss.length} 个）`, -1, -1);
  const w = owW(m), h = m.rows.length;
  ss.forEach((st, i) => {
    const nm = `雷区 ${i + 1}`;
    if (st.x0 < 1 || st.y0 < 1 || st.x1 > w - 2 || st.y1 > h - 2) { E(`${nm} 超出地图（要在最外一圈以内）`, st.x0, st.y0); return; }
    if (st.min < 1 || st.max > OW.MaxBolts || st.min > st.max) E(`${nm}：每次劈的道数要满足 1 ≤ 最少 ≤ 最多 ≤ ${OW.MaxBolts}（现在 ${st.min}–${st.max}）`, st.x0, st.y0);
    const pool = owStrikeable(m, st);
    if (!pool.length) { E(`${nm} 里没有能劈的格子（全是墙 / 水，或者紧挨着门 / 家 / 出生点）`, st.x0, st.y0); return; }
    for (const d of m.doors) for (const c of owFind(m, String(d.n)))
      if (c[0] >= st.x0 - 1 && c[0] <= st.x1 + 1 && c[1] >= st.y0 - 1 && c[1] <= st.y1 + 1) W(`${nm} 挨着门 ${d.n}：门口 1 格不会劈，但他进门的路上会被劈（拖住他是好事，劈太多他会一直晕）`, c[0], c[1]);
    if (st.min > pool.length) W(`${nm} 只有 ${pool.length} 格能劈，少于最少 ${st.min} 道 → 实际每次最多劈 ${pool.length} 道`, st.x0, st.y0);
    I(`${nm}：${st.x1 - st.x0 + 1}×${st.y1 - st.y0 + 1} 格，每次同时劈 ${st.min}–${st.max} 道，${st.always ? '每天都劈' : '只在雷雨天劈'}`, st.x0, st.y0);
  });
}
// S220：伤害说明（由 build.py 从 OverworldCatalog.Harm 生成 OW_HARM；没生成时为空）
const owHarm = c => (typeof OW_HARM !== 'undefined' && OW_HARM[c]) || '';

// ═════════ S232：小镇居民的话（TownStory.cs 逐行移植：同样的选句、同样的哈希；verify 逐字对照）+ 道具箱洗牌袋（OverworldPickupBag.cs）═════════
const TS_TRAITS = ['baker', 'granny', 'kid', 'mayor', 'painter', 'guard'];
const TS_TRAIT_ZH = { baker: '面包师', granny: '老奶奶', kid: '小孩', mayor: '镇长', painter: '画家', guard: '守夜人' };
const tsTraitZh = t => TS_TRAIT_ZH[t] || '居民';
const TS_NAMES = ['阿梅', '桂婆婆', '小豆', '老镇长', '小林', '大熊', '阿梅二号', '桂婆婆的妹妹', '小豆的哥哥'];
const TS_SINCERE_EVERY = 3, TS_MIN_BACK = 5;
function tsDefaultResident(door) { const i = Math.max(1, Math.min(9, door)) - 1; return { door, name: TS_NAMES[i], trait: TS_TRAITS[i % TS_TRAITS.length] }; }
function tsResidentOf(m, door) { const r = (m.residents || []).find(x => x.door === door); if (!r || !r.name) return tsDefaultResident(door); return { door, name: r.name, trait: TS_TRAITS.includes(r.trait) ? r.trait : tsDefaultResident(door).trait }; }
function tsMatch(need, f) {
  const op = need.includes('>=') ? '>=' : need.includes('<=') ? '<=' : need.includes('!=') ? '!=' : '='; const i = need.indexOf(op); if (i <= 0) return false;
  const k = need.slice(0, i).trim(), v = need.slice(i + op.length).trim(), x = f[k] === undefined ? '' : String(f[k]);
  if (op === '=') return x === v; if (op === '!=') return x !== v;
  if (!/^-?\d+$/.test(x) || !/^-?\d+$/.test(v)) return false; return op === '>=' ? +x >= +v : +x <= +v;
}
const tsSincere = l => l.tone === 'sincere';
function tsMem() { return { lastDay: {}, said: new Set(), visits: {}, defended: {}, looted: {}, missed: {}, witnessed: {}, unlock: {}, totalDays: 0, comic: TS_SINCERE_EVERY, sincereDay: -1, yDay: 0, yOutcome: '', yDefended: 0, yLooted: 0 }; }
function tsPick(table, when, who, f, mem, day) {
  let best = null, bT = 1e9, bS = -1, bL = 1e9, bH = 0; const ok = mem.comic >= TS_SINCERE_EVERY && mem.sincereDay !== day;
  for (const l of table) {
    if (l.when !== when || l.who !== who) continue; if (l.once && mem.said.has(l.id)) continue;
    if (mem.lastDay[l.id] !== undefined && day - mem.lastDay[l.id] < Math.max(1, l.coolDays)) continue;
    if (tsSincere(l) && !ok) continue; if (!l.needs.every(n => tsMatch(n, f))) continue;
    const sc = l.needs.length, last = mem.lastDay[l.id] !== undefined ? mem.lastDay[l.id] : -1, tie = owHash(l.id + '|' + day + '|' + (f.door !== undefined ? f.door : ''));
    const better = l.tier < bT || (l.tier === bT && (sc > bS || (sc === bS && (last < bL || (last === bL && tie < bH)))));
    if (!best || better) { best = l; bT = l.tier; bS = sc; bL = last; bH = tie; }
  }
  if (!best && when === 'witness') return null; // S233：当场喊的话不兜底
  if (!best) { let fb = null, fL = 1e9, fS = -1; // 兜底：冷却中的好笑话挑最久没说的（和 C# PickFallback 一样）
    for (const l of table) { if (l.when !== when || l.who !== who || l.once || tsSincere(l) || !l.needs.every(n => tsMatch(n, f))) continue; const last = mem.lastDay[l.id] !== undefined ? mem.lastDay[l.id] : -1, sc = l.needs.length; if (!fb || last < fL || (last === fL && sc > fS)) { fb = l; fL = last; fS = sc; } }
    return fb; }
  return best;
}
function tsSay(l, mem, day) { mem.lastDay[l.id] = day; mem.said.add(l.id); if (tsSincere(l)) { mem.comic = 0; mem.sincereDay = day; if (mem.unlock[l.id] === undefined) mem.unlock[l.id] = mem.totalDays + 1; } else mem.comic++; }
const tsGet = (d, n) => d[n] || 0;
function tsCoverage(t) {
  const out = [];
  for (const tr of TS_TRAITS) {
    const mine = t.filter(x => x.who === 'door' && x.when === 'back' && (x.needs.length === 0 || x.needs.includes('trait=' + tr) || !x.needs.some(n => n.startsWith('trait='))));
    const own = mine.filter(x => x.needs.includes('trait=' + tr)).length, sin = mine.filter(tsSincere).length;
    out.push(`${tsTraitZh(tr)}：回小镇 ${mine.length} 句（专属 ${own}、真情 ${sin}）` + (own < TS_MIN_BACK ? ` ⚠ 专属少于 ${TS_MIN_BACK} 句，玩几天就会听到重复` : ''));
  }
  out.push(`当场喊：${t.filter(x => x.when === 'witness').length} 句（大机关在他家门口砸中马里奥时）`);
  for (const w of ['morning', 'dayend']) out.push(`${w === 'morning' ? '早上闲话' : '一天结束'}：${t.filter(x => x.when === w).length} 句`);
  return out;
}
function tsRehearseResult(map, day, door) { const h = owHash('rehearse|' + map + '|' + day + '|' + door) % 5; return h < 2 ? 'defended' : h < 4 ? 'looted' : 'missed'; }
function tsRehearse(m, t, days) {
  const rep = { lines: [], said: 0, sincere: 0, repeats3: 0, distinct: 0, first: {} }, mem = tsMem(), heard = {}, firstOrder = [];
  const doors = m.doors.slice().sort((a, b) => a.minute - b.minute || a.n - b.n).filter(d => owFind(m, String(d.n)).length === 1);
  const hear = (l, day, where) => {
    if (!l) { rep.lines.push(`第${day}天 ${where} —`); return; }
    tsSay(l, mem, day); rep.said++; if (tsSincere(l)) rep.sincere++;
    if (heard[l.id] !== undefined && day - heard[l.id] < 3) rep.repeats3++; heard[l.id] = day;
    rep.lines.push(`第${day}天 ${where} ${tsSincere(l) ? '♥' : '·'}${l.id}`);
  };
  for (let day = 1; day <= days; day++) {
    const w = OW_WEATHER[owDayOfMap(m, day).kind].toLowerCase();
    hear(tsPick(t, 'morning', 'town', { yesterday: mem.yDay > 0 ? mem.yOutcome : 'none', ydeath: 'none', ybighits: '0', ydefended: String(mem.yDefended), ylooted: String(mem.yLooted), weather: w, day: String(day) }, mem, day), day, '早上');
    let def = 0, loot = 0;
    for (const d of doors) {
      const res = tsRehearseResult(m.name, day, d.n), r = tsResidentOf(m, d.n);
      mem.visits[d.n] = tsGet(mem.visits, d.n) + 1;
      if (res === 'defended') { mem.defended[d.n] = tsGet(mem.defended, d.n) + 1; def++; } else if (res === 'looted') { mem.looted[d.n] = tsGet(mem.looted, d.n) + 1; loot++; } else mem.missed[d.n] = tsGet(mem.missed, d.n) + 1;
      const f = { door: String(d.n), trait: r.trait, result: res, weather: w, visits: String(tsGet(mem.visits, d.n)), defended: String(tsGet(mem.defended, d.n)), looted: String(tsGet(mem.looted, d.n)), missed: String(tsGet(mem.missed, d.n)), witnessed: String(tsGet(mem.witnessed, d.n)), day: String(day), n: String(tsGet(mem.visits, d.n)), bighit: 'no', bells: '0' };
      const l = tsPick(t, 'back', 'door', f, mem, day);
      if (l && tsSincere(l) && rep.first[r.trait] === undefined) { rep.first[r.trait] = day; firstOrder.push(r.trait); }
      hear(l, day, '门' + d.n + tsTraitZh(r.trait));
    }
    const outcome = doors.length > 0 && def * 2 >= doors.length ? 'won' : 'lost';
    hear(tsPick(t, 'dayend', 'mario', { outcome, death: 'none', byyou: 'no', cause: '', bighits: '0', chain: '0', caught: '0', defendedtoday: String(def), lootedtoday: String(loot), weather: w, day: String(day), bells: '0' }, mem, day), day, '结束');
    mem.yDay = day; mem.yOutcome = outcome; mem.yDefended = def; mem.yLooted = loot; mem.totalDays = day;
  }
  rep.mem = mem; rep.distinct = Object.keys(heard).length; rep.firstOrder = firstOrder.map((k, i) => [k, rep.first[k], i]).sort((a, b) => a[1] - b[1] || a[2] - b[2]).map(x => x[0]);
  return rep;
}
const tsRehearsalSummary = (r, days) => `彩排 ${days} 天：说了 ${r.said} 句（不同的 ${r.distinct} 句），真心话 ${r.sincere} 句；3 天内重复 ${r.repeats3} 次` +
  (r.firstOrder.length ? '；第一次真心话：' + r.firstOrder.map(k => `${tsTraitZh(k)} 第${r.first[k]}天`).join('、') : '；还没有人说真心话');
// ═════════ S233：当场喊 / 居民笔记本 / 台词检查 / 灵感骰子 / 存档（TownStory.cs、IdeaDice.cs 逐行移植；verify 逐字对照）═════════
const TS_WITNESS_RANGE = 7;
function tsNearestDoor(m, x, y, maxDist) { let best = 0, bd = maxDist * maxDist + 1e-9; for (const d of m.doors.slice().sort((a, b) => a.n - b.n)) { const c = owFind(m, String(d.n)); if (c.length !== 1) continue; const dx = c[0][0] + 0.5 - x, dy = c[0][1] + 0.5 - y, dd = dx * dx + dy * dy; if (dd < bd) { bd = dd; best = d.n; } } return best; }
function tsWitnessFacts(r, cause, w, mem, day) { return { door: String(r.door), trait: r.trait, cause: cause === ' ' ? '' : cause, weather: w, witnessed: String(tsGet(mem.witnessed, r.door)), visits: String(tsGet(mem.visits, r.door)), day: String(day) }; }
function tsWitness(m, t, mem, door, cause, w, day) { if (door <= 0) return null; mem.witnessDay = mem.witnessDay || {}; if (mem.witnessDay[door] === day) return null; mem.witnessed[door] = tsGet(mem.witnessed, door) + 1; const r = tsResidentOf(m, door), l = tsPick(t, 'witness', 'door', tsWitnessFacts(r, cause, w, mem, day), mem, day); mem.witnessDay[door] = day; if (l) tsSay(l, mem, day); return l; }
function tsNeedHint(l) {
  const p = [];
  for (const n of l.needs) {
    if (n.startsWith('trait=')) continue;
    if (n.startsWith('visits>=')) p.push('来往 ' + n.slice(8) + ' 次'); else if (n.startsWith('defended>=')) p.push('守住 ' + n.slice(10) + ' 次'); else if (n.startsWith('looted>=')) p.push('被偷 ' + n.slice(8) + ' 次');
    else if (n.startsWith('witnessed>=')) p.push('在他家门口闹出 ' + n.slice(11) + ' 次大动静'); else if (n.startsWith('day>=')) p.push('第 ' + n.slice(5) + ' 天以后');
    else if (n === 'outcome=won') p.push('那天你赢了'); else if (n === 'outcome=lost') p.push('那天你输了');
    else if (n.startsWith('weather=')) { const w = n.slice(8); p.push(w === 'rain' ? '下雨天' : w === 'fog' ? '大雾天' : w === 'storm' ? '雷雨天' : w === 'wind' ? '大风天' : '特别的天气'); }
    else p.push('某个特别的日子');
  }
  return p.length ? p.join('、') : '多来几次';
}
const tsFill = (s, r) => String(s || '').replace(/\{name\}/g, r ? r.name : '').replace(/\{trait\}/g, r ? tsTraitZh(r.trait) : '');
function tsNotebookText(m, t, mem) {
  const got = t.filter(l => tsSincere(l) && mem.said.has(l.id)).length, tot = t.filter(tsSincere).length;
  let s = `居民笔记本  真心话 ${got}/${tot} 段　累计 ${mem.totalDays} 天\n`;
  for (const d of m.doors.slice().sort((a, b) => a.n - b.n)) {
    if (owFind(m, String(d.n)).length !== 1) continue; const r = tsResidentOf(m, d.n), n = d.n;
    s += `\n门${n} ${tsTraitZh(r.trait)}·${r.name}　来往 ${tsGet(mem.visits, n)}　守住 ${tsGet(mem.defended, n)}　被偷 ${tsGet(mem.looted, n)}　没赶上 ${tsGet(mem.missed, n)}　门口大动静 ${tsGet(mem.witnessed, n)}\n`;
    for (const l of t.filter(x => tsSincere(x) && x.who === 'door' && x.needs.includes('trait=' + r.trait))) s += '  ' + (mem.said.has(l.id) ? `♥ ${tsFill(l.zh, r)}` + (mem.unlock[l.id] !== undefined ? `（第 ${mem.unlock[l.id]} 天）` : '') : `？ 还没听过——${tsNeedHint(l)}`) + '\n';
  }
  s += '\n镇上 / 马里奥\n';
  for (const l of t.filter(x => tsSincere(x) && x.who !== 'door')) s += '  ' + (mem.said.has(l.id) ? `♥ ${l.who === 'mario' ? '马里奥' : '镇上'}：${tsFill(l.zh, null)}` : `？ 还没听过——${tsNeedHint(l)}`) + '\n';
  return s;
}
const TS_FACT_KEYS = { back: ['door', 'trait', 'result', 'weather', 'visits', 'defended', 'looted', 'missed', 'witnessed', 'day', 'n', 'bighit', 'bells'], witness: ['door', 'trait', 'cause', 'weather', 'witnessed', 'visits', 'day'], morning: ['yesterday', 'ydeath', 'ybighits', 'ydefended', 'ylooted', 'weather', 'day'], dayend: ['outcome', 'death', 'byyou', 'cause', 'bighits', 'chain', 'caught', 'defendedtoday', 'lootedtoday', 'weather', 'day', 'bells'] };
function tsNeedKey(n) { for (const op of ['>=', '<=', '!=', '=']) { const i = n.indexOf(op); if (i > 0) return n.slice(0, i).trim(); } return ''; }
function tsValidate(t) {
  const o = [], ids = new Set();
  for (const l of t) {
    if (ids.has(l.id)) o.push(`✗ ${l.id}：id 重复`); ids.add(l.id);
    const keys = TS_FACT_KEYS[l.when]; if (!keys) { o.push(`✗ ${l.id}：时机 ${l.when} 不认识（morning / back / dayend / witness）`); continue; }
    const who = l.when === 'morning' ? 'town' : l.when === 'dayend' ? 'mario' : 'door';
    if (l.who !== who) o.push(`✗ ${l.id}：${l.when} 的话只能由 ${who} 说（现在写的是 ${l.who}）`);
    for (const n of l.needs) { const k = tsNeedKey(n); if (!k || !keys.includes(k)) o.push(`✗ ${l.id}：条件 ${n} 用不了（${l.when} 能用：${keys.join(' ')}）`); }
    if (tsSincere(l) && !l.once) o.push(`⚠ ${l.id}：真心话没勾"只说一次"（说两遍就不真心了）`);
    if (tsSincere(l) && !l.needs.some(n => n.includes('>='))) o.push(`⚠ ${l.id}：真心话没有"要挣"的条件（例如 visits>=3）——第一天就会说`);
    if (!(l.en || '').length) o.push(`· ${l.id}：还没写英文（可以先空着）`);
    if ((l.zh || '').length > 70) o.push(`· ${l.id}：中文 ${l.zh.length} 字，屏幕上 5 秒读不完（建议 ≤ 70）`);
  }
  return o;
}
// 台词表导出（和 C# TownStory.ToJson 逐字一致 → 直接替换 Assets/Resources/TownStories.json）
function tsJ(s) { return '"' + String(s || '').replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\n/g, '\\n') + '"'; }
function tsToJson(t) {
  let s = '{\n  "version": 1,\n  "lines": [\n';
  t.forEach((l, i) => { s += `    {"id": ${tsJ(l.id)}, "who": ${tsJ(l.who)}, "when": ${tsJ(l.when)}, "tier": ${l.tier}, "tone": ${tsJ(l.tone)}, "coolDays": ${l.coolDays}${l.once ? ', "once": true' : ''}, "needs": [${l.needs.map(tsJ).join(', ')}], "zh": ${tsJ(l.zh)}, "en": ${tsJ(l.en)}${(l.note || '').length ? ', "note": ' + tsJ(l.note) : ''}}${i < t.length - 1 ? ',\n' : '\n'}`; });
  return s + '  ]\n}\n';
}
// 灵感骰子（IdeaDice.cs）
const ID_PROPS = ['巨炮 K', '滚石 O', '水塔 U', '钟楼 B'], ID_SMALLS = ['香蕉皮 L', '高草（躲）', '雷区', '山丘 ^（挡视线）', '山洞 h（一对）', '木箱伪装 P', '挑衅 T', '道具箱 ?'];
const ID_WEATHERS = ['晴天', '大风', '雨天', '雾天', '赶集日', '雷雨', '酸雨'];
const ID_TWISTS = ['让他背对这户人家的门', '两个机关连成一串（冲击 1.5 格内）', '全程只许用一次挑衅', '让这户人家当场看见他被砸', '门口 5 格内没有草可躲', '他坐炮抄近路时你去拨歪', '你得先绕到他身后', '让他差点发现你（视锥擦过）', '这户人家今天第 3 次来往（准备说真心话）', '宝贝被偷也要好笑', '下一扇门只差 30 秒', '用天气替你出手（你不按 L）'];
const idPick = (a, k) => a[owHash(k) % a.length];
function idRoll(m, seed, i) {
  const doors = (m ? m.doors : []).filter(d => owFind(m, String(d.n)).length === 1).map(d => d.n).sort((a, b) => a - b); if (!doors.length) doors.push(1);
  const k = (m ? m.name : '') + '|' + seed + '|' + i, door = doors[owHash(k + '|door') % doors.length], r = tsResidentOf(m, door);
  const it = { door, trait: r.trait, prop: idPick(ID_PROPS, k + '|prop'), small: idPick(ID_SMALLS, k + '|small'), weather: idPick(ID_WEATHERS, k + '|w'), twist: idPick(ID_TWISTS, k + '|t') };
  it.text = `🎲 门${door} ${tsTraitZh(r.trait)}·${r.name}家门口｜${it.weather}｜${it.prop} + ${it.small}｜限制：${it.twist}`;
  it.lineStub = `{"id": "idea_${r.trait}_${seed}_${i}", "who": "door", "when": "witness", "tier": 1, "tone": "comic", "coolDays": 2, "needs": ["trait=${r.trait}"], "zh": "（${tsTraitZh(r.trait)}看见这一下会说什么？）", "en": ""}`;
  return it;
}
const idRolls = (m, seed, n) => Array.from({ length: n }, (_, i) => idRoll(m, seed, i).text);

// 道具箱洗牌袋
const OW_BAG = ['Bomb', 'Bomb', 'Energy', 'Taunt', 'Heart'];
const owBagZh = k => k === 'Energy' ? '◆能量 +1' : k === 'Taunt' ? '📣挑衅 +1' : k === 'Heart' ? '❤补心 +1' : '💣炸弹 +1';
function owBagShuffled(name, cycle) {
  const b = OW_BAG.slice(); let x = (owHash(name || '') ^ (Math.imul(cycle, 2654435761 | 0) >>> 0)) >>> 0; if (x === 0) x = 1;
  for (let i = b.length - 1; i > 0; i--) { x = (x ^ (x << 13)) >>> 0; x = (x ^ (x >>> 17)) >>> 0; x = (x ^ (x << 5)) >>> 0; const j = x % (i + 1); const t = b[i]; b[i] = b[j]; b[j] = t; }
  return b;
}
function owBagOf(name, day, box, count) { if (day <= 1) return 'Bomb'; const k = (day - 2) * Math.max(1, count) + box; return owBagShuffled(name, Math.floor(k / OW_BAG.length))[k % OW_BAG.length]; }
function owBagPreview(m, from, n) { const l = owFind(m, '?'), out = []; if (!l.length) return out; for (let d = from; d < from + n; d++) out.push(`第 ${d} 天 道具箱：` + l.map((_, i) => owBagZh(owBagOf(m.name, d, i, l.length))).join(' / ')); return out; }
