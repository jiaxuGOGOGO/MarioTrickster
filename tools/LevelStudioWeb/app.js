// ── 状态 ──────────────────────────────────────────────
const LS = 'mariotrickster.studio.v1';
const S = {
  grid: [], name: '', goal: '', notes: [], proposals: [], cuts: {},
  brush: '#', tool: 'brush', zoom: 20, showAll: false,
  ov: { route: true, dead: true, worst: false, notes: true, jump: true, unreach: true }
};
let W = makeWorld([]), R = null, undo = [], redo = [], hover = null, dragStart = null, painting = false, dirtyTimer = 0;
const $ = s => document.querySelector(s);
const rgbCss = (rgb, a = 1) => `rgba(${Math.round(rgb[0] * 255)},${Math.round(rgb[1] * 255)},${Math.round(rgb[2] * 255)},${a})`;
const hexToRgb = h => [1, 3, 5].map(i => parseInt(h.substr(i, 2), 16) / 255);
const rgbToHex = rgb => '#' + rgb.map(v => Math.round(v * 255).toString(16).padStart(2, '0')).join('');
const lum = rgb => { const f = v => v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); return 0.2126 * f(rgb[0]) + 0.7152 * f(rgb[1]) + 0.0722 * f(rgb[2]); };
const textOn = rgb => lum(rgb) > 0.22 ? '#17131f' : '#f5efe2';
function toast(t) { const el = $('#toast'); el.textContent = t; el.classList.add('on'); clearTimeout(el._t); el._t = setTimeout(() => el.classList.remove('on'), 1800); }
const ROLE_ZH = { Terrain: '地形', Scenery: '摆件', PlayerPrank: '机关（按 L）', AutoHazard: '自动危险', Objective: '目标', Spawn: '出生点', Movement: '移动', Enemy: '敌人', Special: '特殊', Skill: '捣蛋者技能', MarioAI: '马里奥行为' };
const ROLE_ORDER = ['Spawn', 'Objective', 'Terrain', 'PlayerPrank', 'Special', 'Scenery', 'Movement', 'AutoHazard', 'Enemy'];

function save() { try { localStorage.setItem(LS, JSON.stringify({ grid: S.grid, name: S.name, goal: S.goal, notes: S.notes, proposals: S.proposals, cuts: S.cuts })); } catch (e) { } }
function load() { try { const d = JSON.parse(localStorage.getItem(LS) || 'null'); if (d && d.grid && d.grid.length) Object.assign(S, d); } catch (e) { } }

// ── 网格编辑 ──────────────────────────────────────────
const H = () => S.grid.length, Wd = () => S.grid[0].length;
const cellAt = (x, y) => S.grid[H() - 1 - y][x];
function setCell(x, y, c) {
  if (x < 0 || y < 0 || x >= Wd() || y >= H()) return false;
  const row = H() - 1 - y; if (S.grid[row][x] === c) return false;
  const e = W.info.get(c);
  if ('MTG'.includes(c) || (e && e.u)) S.grid = S.grid.map(r => r.split(c).join('.'));
  S.grid[row] = S.grid[row].substr(0, x) + c + S.grid[row].substr(x + 1); return true;
}
function snapshot() { undo.push(JSON.stringify(S.grid)); if (undo.length > 200) undo.shift(); redo = []; }
function blank(w, h) {
  const g = []; for (let r = 0; r < h; r++) g.push(r === 0 || r === h - 1 ? 'W'.repeat(w) : 'W' + '.'.repeat(w - 2) + 'W');
  const row = h - 2; g[row] = 'W.G.M' + '.'.repeat(w - 12) + 'T..o..W'.slice(0) ; g[row] = g[row].substr(0, w - 1) + 'W';
  return g;
}
function resize(w, h) {
  const old = S.grid, oh = old.length, ow = old[0].length, g = [];
  for (let r = 0; r < h; r++) {
    const src = oh - h + r; let line = '';
    for (let x = 0; x < w; x++) {
      let c = src >= 0 && x < ow ? old[src][x] : '.';
      if (r === 0 || x === 0 || x === w - 1) c = 'W';
      if (x >= ow - 1 && x < w - 1 && r !== 0 && src >= 0 && old[src][ow - 1] === 'W' && x !== w - 1) c = src === oh - 1 ? old[src][ow - 1] === 'W' ? '#' : c : '.';
      line += c;
    }
    g.push(line);
  }
  if (g[h - 1].replace(/W/g, '').length === 0) g[h - 1] = 'W' + '#'.repeat(w - 2) + 'W';
  S.grid = g;
}
function floodFill(x, y, c) {
  const from = cellAt(x, y); if (from === c) return;
  const q = [[x, y]], seen = new Set();
  while (q.length) { const [cx, cy] = q.pop(); const k = cx * 1000 + cy; if (seen.has(k) || cx < 0 || cy < 0 || cx >= Wd() || cy >= H() || cellAt(cx, cy) !== from) continue; seen.add(k); setCell(cx, cy, c); q.push([cx + 1, cy], [cx - 1, cy], [cx, cy + 1], [cx, cy - 1]); }
}

// ── 检查（停笔后跑） ─────────────────────────────────
function recheck(now) {
  clearTimeout(dirtyTimer);
  const run = () => { W = makeWorld(S.proposals); R = check(W, S.grid, true); renderSide(); draw(); save(); };
  if (now) run(); else dirtyTimer = setTimeout(run, 180);
}

// ── 画布 ─────────────────────────────────────────────
const cv = $('#cv'), ctx = cv.getContext('2d');
function colorOf(c) { if (c === '.') return [0.11, 0.1, 0.17]; if (SLOT_CHARS[c]) return [0.45, 0.4, 0.6]; const e = W.info.get(c); return e ? e.rgb : [0.8, 0.1, 0.8]; }
function draw() {
  const z = S.zoom, w = Wd(), h = H(), dpr = window.devicePixelRatio || 1;
  cv.width = w * z * dpr; cv.height = h * z * dpr; cv.style.width = w * z + 'px'; cv.style.height = h * z + 'px';
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const grid = S.ov.worst ? S.grid.map(r => r.replace(/[Cx|%]/g, '.')) : S.grid;
  ctx.font = `600 ${Math.max(9, z * 0.55)}px JetBrains Mono, monospace`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (let row = 0; row < h; row++) for (let x = 0; x < w; x++) {
    const c = grid[row][x], rgb = colorOf(c), px = x * z, py = row * z;
    ctx.fillStyle = rgbCss(rgb); ctx.fillRect(px, py, z - (c === '.' ? 0 : 1), z - (c === '.' ? 0 : 1));
    if (c === '.') { ctx.fillStyle = 'rgba(255,255,255,.035)'; ctx.fillRect(px + z / 2 - 1, py + z / 2 - 1, 2, 2); continue; }
    const e = W.info.get(c);
    if (e && e.proposal) { ctx.strokeStyle = '#ffc83d'; ctx.setLineDash([3, 2]); ctx.strokeRect(px + 1.5, py + 1.5, z - 4, z - 4); ctx.setLineDash([]); }
    if (!'#W='.includes(c) && z >= 12) { ctx.fillStyle = textOn(rgb); ctx.fillText(c, px + z / 2, py + z / 2 + 1); }
  }
  if (R && S.ov.dead) {
    ctx.strokeStyle = 'rgba(255,90,78,.85)'; ctx.lineWidth = 1.5;
    for (const k of R.deadlock) { const x = Math.floor(k / 1000), y = k % 1000, px = x * z, py = (h - 1 - y) * z; ctx.beginPath(); for (let d = -z; d < z; d += 5) { ctx.moveTo(px + Math.max(0, d), py + Math.max(0, -d)); ctx.lineTo(px + Math.min(z, z + d), py + Math.min(z, z - d)); } ctx.stroke(); }
    ctx.lineWidth = 1;
  }
  if (R && S.ov.route && R.route) {
    ctx.strokeStyle = 'rgba(255,90,78,.9)'; ctx.lineWidth = Math.max(2, z / 8); ctx.lineJoin = 'round'; ctx.setLineDash([z / 3, z / 5]);
    ctx.beginPath(); R.route.forEach(([x, y], i) => { const px = x * z + z / 2, py = (h - 1 - y) * z + z / 2; i ? ctx.lineTo(px, py) : ctx.moveTo(px, py); }); ctx.stroke(); ctx.setLineDash([]); ctx.lineWidth = 1;
    ctx.font = `600 ${Math.max(9, z * 0.45)}px JetBrains Mono, monospace`;
    for (const s of R.onRoute) { const px = s.x * z + z / 2, py = (h - 1 - s.y) * z - z * 0.35; ctx.fillStyle = 'rgba(22,19,31,.85)'; const t = s.at.toFixed(0) + 's'; const tw = ctx.measureText(t).width + 6; ctx.fillRect(px - tw / 2, py - z * 0.3, tw, z * 0.6); ctx.fillStyle = '#ffc83d'; ctx.fillText(t, px, py); }
    for (const s of R.offRoute) { ctx.strokeStyle = 'rgba(157,147,179,.9)'; ctx.setLineDash([2, 2]); ctx.strokeRect(s.x * z + 1, (h - 1 - s.y) * z + 1, z - 2, z - 2); ctx.setLineDash([]); }
  }
  if (R) for (const i of R.issues) if (i.x >= 0 && i.sev === 'error') { ctx.strokeStyle = '#ff5a4e'; ctx.lineWidth = 2; ctx.strokeRect(i.x * z + 1, (h - 1 - i.y) * z + 1, z - 2, z - 2); ctx.lineWidth = 1; }
  if (R && R.unreach && S.ov.unreach) { ctx.fillStyle = 'rgba(176,140,255,.9)'; for (const k of R.unreach) { const x = Math.floor(k / 1000), y = k % 1000; ctx.beginPath(); ctx.arc(x * z + z / 2, (h - 1 - y) * z + z * 0.78, Math.max(2, z / 7), 0, 7); ctx.fill(); } }
  // S205 跳跃辅助：鼠标停的地方站着起跳，一步能到哪
  if (S.ov.jump && hover && !painting && !dragStart) {
    const jr = jumpReach(W, S.grid, hover[0], hover[1]);
    $('#jumpKey').hidden = !jr.from;
    if (jr.from) {
      const paint = (set, col) => { for (const k of set) { const x = Math.floor(k / 1000), y = k % 1000, px = x * z, py = (h - 1 - y) * z; ctx.fillStyle = col; ctx.fillRect(px + 2, py + 2, z - 4, z - 4); } };
      paint(jr.phys, 'rgba(255,179,71,.45)'); paint(jr.ai, 'rgba(95,211,154,.45)');
      const fx = jr.from[0] * z + z / 2, fy = (h - 1 - jr.from[1]) * z + z / 2;
      ctx.strokeStyle = '#5fd39a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(fx, fy, z * 0.35, 0, 7); ctx.stroke(); ctx.lineWidth = 1;
    }
  } else if ($('#jumpKey')) $('#jumpKey').hidden = true;
  if (S.ov.notes) for (const n of S.notes) { const px = n.x * z, py = (h - 1 - n.y) * z; ctx.fillStyle = '#ffc83d'; ctx.beginPath(); ctx.moveTo(px + z, py); ctx.lineTo(px + z, py + z * 0.5); ctx.lineTo(px + z * 0.5, py); ctx.fill(); }
  if (focusCell) { ctx.strokeStyle = '#ffc83d'; ctx.lineWidth = 3; ctx.strokeRect(focusCell[0] * z - 2, (h - 1 - focusCell[1]) * z - 2, z + 4, z + 4); ctx.lineWidth = 1; }
  if (dragStart && hover && S.tool === 'rect') { const [a, b] = rectOf(dragStart, hover); ctx.strokeStyle = '#fff'; ctx.setLineDash([4, 3]); ctx.strokeRect(a[0] * z, (h - 1 - b[1]) * z, (b[0] - a[0] + 1) * z, (b[1] - a[1] + 1) * z); ctx.setLineDash([]); }
  else if (hover) { ctx.strokeStyle = 'rgba(255,255,255,.7)'; ctx.strokeRect(hover[0] * z + .5, (h - 1 - hover[1]) * z + .5, z - 1, z - 1); }
}
let focusCell = null;
function rectOf(a, b) { return [[Math.min(a[0], b[0]), Math.min(a[1], b[1])], [Math.max(a[0], b[0]), Math.max(a[1], b[1])]]; }
function cellFromEvent(e) { const r = cv.getBoundingClientRect(); const x = Math.floor((e.clientX - r.left) / S.zoom), row = Math.floor((e.clientY - r.top) / S.zoom); if (x < 0 || row < 0 || x >= Wd() || row >= H()) return null; return [x, H() - 1 - row]; }
cv.addEventListener('contextmenu', e => e.preventDefault());
cv.addEventListener('mousedown', e => {
  const c = cellFromEvent(e); if (!c) return;
  if (e.altKey) { addNote(c); return; }
  const erase = e.button === 2, ch = erase ? '.' : S.brush;
  if (S.tool === 'pick' && !erase) { setBrush(cellAt(c[0], c[1])); return; }
  snapshot();
  if (S.tool === 'fill' && !erase) { floodFill(c[0], c[1], ch); recheck(); draw(); return; }
  if (S.tool === 'rect' && !erase) { dragStart = c; painting = true; return; }
  painting = ch; setCell(c[0], c[1], ch); draw(); recheck();
});
window.addEventListener('mouseup', () => {
  if (dragStart && hover) { const [a, b] = rectOf(dragStart, hover); for (let x = a[0]; x <= b[0]; x++) for (let y = a[1]; y <= b[1]; y++) setCell(x, y, S.brush); recheck(); }
  dragStart = null; painting = false; draw();
});
cv.addEventListener('mousemove', e => {
  const c = cellFromEvent(e); const changed = !hover || !c || hover[0] !== c[0] || hover[1] !== c[1]; hover = c;
  if (c) { const ch = cellAt(c[0], c[1]), en = W.info.get(ch); const n = S.notes.find(n => n.x === c[0] && n.y === c[1]); $('#hoverInfo').textContent = `(${c[0]},${c[1]})  '${ch}'  ${en ? en.zh : ch === '.' ? '空气' : SLOT_CHARS[ch] ? '随机槽位' : '?'}${n ? '  📝 ' + n.text : ''}`; }
  if (painting && typeof painting === 'string' && c && setCell(c[0], c[1], painting)) recheck();
  if (changed) draw();
});
cv.addEventListener('mouseleave', () => { hover = null; draw(); });
function addNote(c) {
  const old = S.notes.find(n => n.x === c[0] && n.y === c[1]);
  const t = prompt(`给 (${c[0]},${c[1]}) 写一句批注（想法 / 这里要什么效果 / 问题）。留空 = 删除`, old ? old.text : '');
  if (t === null) return;
  S.notes = S.notes.filter(n => n !== old); if (t.trim()) S.notes.push({ x: c[0], y: c[1], text: t.trim() });
  renderNotes(); draw(); save();
}

// ── 左侧：元素库 ─────────────────────────────────────
function setBrush(c) { S.brush = c; renderPalette(); renderBrush(); }
function renderPalette() {
  const q = $('#palSearch').value.trim().toLowerCase(); const box = $('#palette'); box.innerHTML = '';
  const all = [...W.info.values()].filter(e => e.c !== ' ');
  const groups = {};
  for (const e of all) { if (!S.showAll && !e.s1 && !e.proposal) continue; if (q && !(e.zh + e.en + e.c + e.k).toLowerCase().includes(q)) continue; const r = e.proposal ? 'Proposal' : e.r; (groups[r] = groups[r] || []).push(e); }
  const order = ['Proposal', ...ROLE_ORDER];
  let n = 0;
  for (const r of order) {
    if (!groups[r]) continue;
    const h = document.createElement('h3'); h.textContent = r === 'Proposal' ? '你的新机制提案' : ROLE_ZH[r] || r; box.appendChild(h);
    const pal = document.createElement('div'); pal.className = 'pal';
    for (const e of groups[r]) {
      n++;
      const b = document.createElement('button'); b.className = 'chip' + (e.proposal ? ' prop' : '') + (S.cuts[e.c] ? ' off' : '');
      b.setAttribute('aria-pressed', S.brush === e.c); b.title = `${e.zh} ${e.en}\n${e.w}`;
      b.innerHTML = `<span class="sw" style="background:${rgbCss(e.rgb)};color:${textOn(e.rgb)}">${e.c === '.' ? '·' : e.c}</span><span class="nm">${e.zh}</span>`;
      b.onclick = () => setBrush(e.c); pal.appendChild(b);
    }
    box.appendChild(pal);
  }
  $('#palCount').textContent = `· ${n}`;
}
function renderBrush() {
  const e = W.info.get(S.brush);
  $('#brushInfo').innerHTML = e ? `<b>${e.c === '.' ? '·' : e.c} ${e.zh}</b> ${e.en}<br>${e.w}<br><span style="color:var(--chalk)">怎么放：</span>${e.p || '—'}${e.s ? '<br>⚠ 脚下要实心' : ''}${e.u ? '<br>⚠ 整张图只能有 1 个' : ''}` : '—';
}

// ── 右侧：结果 ───────────────────────────────────────
function renderSide() {
  if (!R) return;
  const errs = R.issues.filter(i => i.sev === 'error').length, warns = R.issues.filter(i => i.sev === 'warn').length;
  const v = $('#verdict'); v.className = 'verdict ' + (R.playable ? 'ok' : 'bad');
  v.innerHTML = `<div class="big">${R.playable ? '可以交给 AI 试玩' : `还有 ${errs} 个问题要改`}</div><div class="hint">${R.route ? `马里奥走一趟约 ${R.seconds.toFixed(0)} 秒 · 路上 ${R.onRoute.length} 个机关` : '马里奥的路线还没算出来'}${warns ? ` · ${warns} 个提醒` : ''}</div><div class="hint" style="margin-top:4px">网页检查是快速预检；最终以 Unity 工坊和 AI 的完整体检为准（炸弹困人、自动检查等只在那边跑）。</div>`;
  const tl = $('#timeline'); tl.innerHTML = '<div class="track"></div>';
  if (R.route && R.seconds > 0) {
    const pos = t => `calc(10px + (100% - 30px) * ${Math.min(1, t / R.seconds)})`;
    const run = document.createElement('div'); run.className = 'run'; run.style.left = pos(0); tl.appendChild(run);
    if (R.lootAt) { const lm = document.createElement('div'); lm.className = 'lootmark'; lm.style.left = pos(R.lootAt); tl.appendChild(lm); }
    const lanes = [], gap = 22 / Math.max(120, tl.clientWidth - 30) * R.seconds; let maxLane = 0;
    for (const s of R.onRoute) {
      let lane = 0; while (lane < lanes.length && s.at - lanes[lane] < gap) lane++; lanes[lane] = s.at; maxLane = Math.max(maxLane, lane);
      const d = document.createElement('div'); d.className = 'stop'; d.style.left = pos(s.at); d.style.top = (54 + lane * 34) + 'px';
      const e = W.info.get(s.c); d.title = `${e ? e.zh : s.c} · ${s.at.toFixed(1)} 秒 · (${s.x},${s.y})`;
      d.innerHTML = `<span class="g" style="background:${rgbCss(e ? e.rgb : [1, 1, 1])};color:${textOn(e ? e.rgb : [1, 1, 1])}">${s.c}</span><small>${s.at.toFixed(0)}</small>`;
      d.onclick = () => focusOn(s.x, s.y); tl.appendChild(d);
    }
    tl.style.height = (96 + maxLane * 34) + 'px';
    let t = 0; const tick = () => { if (!document.body.contains(run)) return; t = (t + 0.016 * 4) % (R.seconds + 2); run.style.left = pos(Math.min(t, R.seconds)); requestAnimationFrame(tick); };
    if (!matchMedia('(prefers-reduced-motion: reduce)').matches) requestAnimationFrame(tick);
    $('#tlLoot').textContent = R.lootAt ? `拿宝 ${R.lootAt.toFixed(0)}s` : ''; $('#tlEnd').textContent = `出口 ${R.seconds.toFixed(0)}s`;
  } else { tl.innerHTML += '<div class="hint" style="padding:12px">路线算不出来时不显示时间线</div>'; $('#tlLoot').textContent = $('#tlEnd').textContent = ''; }
  const box = $('#issues'); box.innerHTML = '';
  if (!R.issues.length) box.innerHTML = '<div class="empty">没有发现问题。去"新机制提案"想点新东西，或者直接"交给 AI"。</div>';
  for (const i of R.issues) {
    const d = document.createElement('div'); d.className = 'issue ' + i.sev;
    d.innerHTML = `<span class="dot"></span><span>${i.x >= 0 ? `<code>(${i.x},${i.y})</code> ` : ''}${i.t}</span>`;
    if (i.x >= 0) d.onclick = () => focusOn(i.x, i.y); box.appendChild(d);
  }
}
function focusOn(x, y) {
  focusCell = [x, y]; draw();
  const st = $('#stage'); st.scrollTo({ left: x * S.zoom - st.clientWidth / 2 + 24, top: (H() - 1 - y) * S.zoom - st.clientHeight / 2 + 24, behavior: 'smooth' });
  setTimeout(() => { focusCell = null; draw(); }, 1500);
}
function renderNotes() {
  const box = $('#notesList'); box.innerHTML = '';
  if (!S.notes.length) { box.innerHTML = '<div class="empty">按住 Alt 点格子，写下"这里想要什么效果"。AI 会逐条照着改。</div>'; return; }
  for (const n of S.notes) { const d = document.createElement('div'); d.className = 'issue info'; d.innerHTML = `<span class="dot" style="background:var(--loot)"></span><span><code>(${n.x},${n.y})</code> ${n.text}</span>`; d.onclick = () => focusOn(n.x, n.y); box.appendChild(d); }
}

// ── 提案 ─────────────────────────────────────────────
const LAWS = [
  ['H3', '触发前要有预警', f => f.role !== 'Terrain' && f.role !== 'Scenery' && !f.tele],
  ['H4', '马里奥只能看/听/记，不读你的信息', f => f.role === 'MarioAI'],
  ['H6', '静音也看得懂（画面提示）', f => !f.tele],
  ['H9', '控人要有时长、必然结束', f => f.control],
  ['H1', '改地形：最坏情况下仍有路（死局/炸弹模拟）', f => f.terrain],
  ['A2', '每种优势都要有代价和反制', f => !f.cost || !f.counter],
];
function readForm() { return { c: $('#mChar').value, key: $('#mKey').value.trim(), zh: $('#mZh').value.trim(), role: $('#mRole').value, color: $('#mColor').value, solid: $('#mSolid').checked, support: $('#mSupport').checked, terrain: $('#mTerrain').checked, control: $('#mControl').checked, effect: $('#mEffect').value.trim(), tele: $('#mTele').value.trim(), cost: $('#mCost').value.trim(), counter: $('#mCounter').value.trim(), place: $('#mPlace').value.trim(), combo: $('#mCombo').value.trim() }; }
function fillForm(p) { $('#mChar').value = p.c || ''; $('#mKey').value = p.key || ''; $('#mZh').value = p.zh || ''; $('#mRole').value = p.role || 'PlayerPrank'; $('#mColor').value = p.color || '#b08cff'; $('#mSolid').checked = !!p.solid; $('#mSupport').checked = p.support !== false; $('#mTerrain').checked = !!p.terrain; $('#mControl').checked = !!p.control;['Effect', 'Tele', 'Cost', 'Counter', 'Place', 'Combo'].forEach(k => $('#m' + k).value = p[k.toLowerCase()] || ''); renderLaws(); }
function renderLaws() { const f = readForm(); $('#mLaws').innerHTML = LAWS.map(([id, t, need]) => `<span class="${need(f) ? 'need' : ''}" title="${t}">${id} ${t}${need(f) ? ' ← 请写清' : ''}</span>`).join(''); }
document.querySelectorAll('#pageMech input,#pageMech select,#pageMech textarea').forEach(el => el.addEventListener('input', renderLaws));
$('#mSave').onclick = () => {
  const f = readForm(), msg = $('#mMsg');
  const isCell = !['Skill', 'MarioAI'].includes(f.role);
  if (!f.zh || !f.effect) { msg.textContent = '至少写中文名和"它做什么"。'; return; }
  if (isCell) {
    if (f.c.length !== 1 || '.123 '.includes(f.c)) { msg.textContent = '画在格子里的机制需要 1 个字符（不能用 . 1 2 3 空格）。'; return; }
    const existing = ELEMENTS.find(e => e.c === f.c); if (existing) { msg.textContent = `字符 ${f.c} 已经是"${existing.zh}"了。可用：A D N V Z a h i j l m p q r s t u v y z 0 4-9 等`; return; }
  } else f.c = '';
  f.rgb = hexToRgb(f.color); f.en = f.key || f.zh;
  S.proposals = S.proposals.filter(p => (f.c ? p.c !== f.c : p.zh !== f.zh)); S.proposals.push(f);
  msg.textContent = isCell ? `已保存。回"画关卡"，元素库最上面就有 ${f.c} ${f.zh}。` : '已保存（技能/行为类不画在格子里，会写进设计单）。';
  W = makeWorld(S.proposals); renderPropList(); renderPalette(); recheck(true);
};
$('#mClear').onclick = () => { fillForm({}); $('#mMsg').textContent = ''; };
function renderPropList() {
  const box = $('#propList');
  if (!S.proposals.length) { box.innerHTML = '<div class="empty">还没有提案。左边填一个试试：比如"磁铁陷阱：把马里奥吸过来"。</div>'; return; }
  box.innerHTML = '<h3 style="margin:0 0 6px">已有提案</h3>';
  for (const p of S.proposals) {
    const d = document.createElement('div'); d.className = 'p';
    const rgb = p.rgb || [0.7, 0.55, 1];
    d.innerHTML = `<div class="sw" style="background:${rgbCss(rgb)};color:${textOn(rgb)}">${p.c || '★'}</div><div><h4>${p.zh} <span class="hint">${ROLE_ZH[p.role] || p.role}</span></h4><p>${p.effect}</p><p>代价：${p.cost || '<span style="color:var(--warn)">没写</span>'} · 反制：${p.counter || '<span style="color:var(--warn)">没写</span>'}</p></div><div style="display:flex;flex-direction:column;gap:4px"><button class="btn small">编辑</button><button class="btn small">删除</button></div>`;
    const [eb, db] = d.querySelectorAll('button'); eb.onclick = () => fillForm(p);
    db.onclick = () => { if (!confirm(`删除提案"${p.zh}"？画布上用到的格子会变成空气。`)) return; S.proposals = S.proposals.filter(x => x !== p); if (p.c) S.grid = S.grid.map(r => r.split(p.c).join('.')); W = makeWorld(S.proposals); renderPropList(); renderPalette(); recheck(true); };
    box.appendChild(d);
  }
}
function renderCuts() {
  const box = $('#cutList'); box.innerHTML = '';
  for (const e of ELEMENTS.filter(e => e.s1 && !'MTGo.#W'.includes(e.c))) {
    const l = document.createElement('label'); const on = !!S.cuts[e.c];
    l.innerHTML = `<input type="checkbox" ${on ? 'checked' : ''}> <span class="sw" style="display:inline-grid;place-items:center;width:20px;height:20px;border-radius:4px;background:${rgbCss(e.rgb)};color:${textOn(e.rgb)};font:600 11px var(--mono)">${e.c}</span> ${e.zh} <input class="f" style="flex:1;padding:4px 8px;${on ? '' : 'display:none'}" placeholder="原因 / 想怎么改" value="${(S.cuts[e.c] || '').replace(/"/g, '&quot;')}">`;
    const [cb, why] = l.querySelectorAll('input');
    cb.onchange = () => { if (cb.checked) { S.cuts[e.c] = S.cuts[e.c] || '（没写原因）'; why.style.display = ''; why.focus(); } else { delete S.cuts[e.c]; why.style.display = 'none'; } save(); renderPalette(); };
    why.oninput = () => { S.cuts[e.c] = why.value || '（没写原因）'; save(); };
    box.appendChild(l);
  }
}

// ── 交接单 ───────────────────────────────────────────
function handoff() {
  const r = R || check(W, S.grid, true), L = [];
  L.push('# MarioTrickster 设计单（来自关卡设计台）', '');
  L.push('> 给 AI：请加载接续包（mariotrickster-continue），按这份设计单更新项目。关卡加成工坊样板并跑完整体检（含炸弹策略模拟、三种性格自动检查）；新机制按"新机关"分册 16 步清单实现；删改项先说明影响再动手。完成后交付升级包 + 更新接续包。', '');
  L.push(`## 关卡：${S.name || '（未命名）'}`);
  if (S.goal) L.push('', `**设计意图**：${S.goal}`);
  L.push('', `尺寸 ${Wd()}×${H()}；网页预检：${r.playable ? '✓ 可以试玩' : `✗ ${r.issues.filter(i => i.sev === 'error').length} 个问题`}；马里奥一趟约 ${r.seconds ? r.seconds.toFixed(0) : '?'} 秒，路上 ${r.onRoute.length} 个机关。`);
  if (r.onRoute.length) L.push('', '**马里奥时间线**：' + r.onRoute.map(s => `${s.at.toFixed(0)}s ${W.info.get(s.c) ? W.info.get(s.c).zh : s.c}(${s.x},${s.y})`).join(' → '));
  const errs = r.issues.filter(i => i.sev !== 'info');
  if (errs.length) { L.push('', '**网页预检发现的问题**（请修掉或说明）：'); errs.forEach(i => L.push(`- ${i.x >= 0 ? `(${i.x},${i.y}) ` : ''}${i.t}`)); }
  L.push('', '```text', ...S.grid, '```');
  if (S.notes.length) { L.push('', '**格子批注**（坐标 x 从左 0、y 从下 0）：'); S.notes.forEach(n => L.push(`- (${n.x},${n.y}) '${cellAt(n.x, n.y)}'：${n.text}`)); }
  const used = new Set(S.grid.join('')); const props = S.proposals;
  if (props.length) {
    L.push('', '## 新机制提案');
    for (const p of props) {
      L.push('', `### ${p.zh}${p.c ? `（字符 \`${p.c}\`，主题键 ${p.key || '待定'}）` : ''} — ${ROLE_ZH[p.role] || p.role}${p.c && used.has(p.c) ? ' · 已画进关卡' : ''}`);
      L.push(`- 做什么：${p.effect}`, `- 预警（H3/H6）：${p.tele || '**未写**'}`, `- 代价（A2）：${p.cost || '**未写**'}`, `- 马里奥怎么反制/发现（H4）：${p.counter || '**未写**'}`);
      if (p.place) L.push(`- 放在哪：${p.place}`); if (p.combo) L.push(`- 能连的机关：${p.combo}`);
      L.push(`- 属性：${p.solid ? '实心' : '可穿过'}${p.support ? '、脚下要实心' : ''}${p.terrain ? '、**会改变地形 → 要进死局/炸弹策略模拟（H1/H9）**' : ''}${p.control ? '、**会控制人 → 必须有时长、必然结束（H9）**' : ''}`);
      const need = LAWS.filter(([, , f]) => f(p)).map(([id, t]) => `${id} ${t}`); if (need.length) L.push(`- 还需要说清：${need.join('；')}`);
    }
  }
  const cuts = Object.entries(S.cuts);
  if (cuts.length) { L.push('', '## 想删掉 / 改掉的旧东西'); cuts.forEach(([c, why]) => { const e = W.info.get(c); L.push(`- \`${c}\` ${e ? e.zh : c}：${why}`); }); L.push('', '（删之前请告诉我影响：哪些样板/测试/连锁在用它。）'); }
  L.push('', '---', `导出时间：${new Date().toLocaleString('zh-CN')} · 设计台规则版本：S203（跳高 2 格、平跳 4 格、弹簧头顶 4 格、炮口前 3 格、毒池 ≤3 格）`);
  return L.join('\n');
}
function download(name, text, type) { const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([text], { type })); a.download = name; a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 1000); }
$('#hCopy').onclick = async () => { const t = handoff(); try { await navigator.clipboard.writeText(t); toast('设计单已复制'); } catch (e) { const ta = document.createElement('textarea'); ta.value = t; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove(); toast('设计单已复制'); } };
$('#hDownload').onclick = () => download(`设计单_${(S.name || 'level').replace(/[\\/:*?"<>|]/g, '')}.md`, handoff(), 'text/markdown');
$('#hTxt').onclick = () => download(`${(S.name || 'my_level').replace(/[\\/:*?"<>|]/g, '')}.txt`, S.grid.join('\n') + '\n', 'text/plain');
function gridToPng(grid, scale) {
  const h = grid.length, w = grid[0].length, c = document.createElement('canvas'); c.width = w * scale; c.height = h * scale; const x = c.getContext('2d');
  for (let r = 0; r < h; r++) for (let q = 0; q < w; q++) { const ch = grid[r][q]; if (ch === '.') continue; x.fillStyle = rgbCss(colorOf(ch)); x.fillRect(q * scale, r * scale, scale, scale); }
  return c;
}
$('#hPng').onclick = () => gridToPng(S.grid, 1).toBlob(b => { const a = document.createElement('a'); a.href = URL.createObjectURL(b); a.download = `${(S.name || 'level').replace(/[\\/:*?"<>|]/g, '')}_1px.png`; a.click(); });
$('#hPal').onclick = () => {
  const els = [...W.info.values()].filter(e => e.s1 && e.c !== ' ' && e.c !== '.'), cell = 28, cols = 4, c = document.createElement('canvas');
  c.width = cols * 170; c.height = Math.ceil(els.length / cols) * (cell + 8) + 8; const x = c.getContext('2d'); x.fillStyle = '#16131f'; x.fillRect(0, 0, c.width, c.height);
  x.font = '14px sans-serif'; x.textBaseline = 'middle';
  els.forEach((e, i) => { const px = (i % cols) * 170 + 8, py = Math.floor(i / cols) * (cell + 8) + 8; x.fillStyle = rgbCss(e.rgb); x.fillRect(px, py, cell, cell); x.fillStyle = '#efe7d6'; x.fillText(`${e.c} ${e.zh}`, px + cell + 8, py + cell / 2); });
  c.toBlob(b => { const a = document.createElement('a'); a.href = URL.createObjectURL(b); a.download = 'MarioTrickster_调色板.png'; a.click(); });
};
$('#btnExport').onclick = () => download(`${(S.name || 'mariotrickster').replace(/[\\/:*?"<>|]/g, '')}.studio.json`, JSON.stringify({ v: 1, grid: S.grid, name: S.name, goal: S.goal, notes: S.notes, proposals: S.proposals, cuts: S.cuts }, null, 1), 'application/json');
$('#btnImport').onclick = () => $('#fileIn').click();
$('#fileIn').onchange = async e => {
  const f = e.target.files[0]; if (!f) return; e.target.value = '';
  try {
    if (/\.(png|gif|bmp)$/i.test(f.name)) return importImage(f);
    const p = parseForeign(await f.text(), f.name);
    if (p.kind === 'studio') { const d = p.studio; Object.assign(S, { grid: d.grid, name: d.name || '', goal: d.goal || '', notes: d.notes || [], proposals: d.proposals || [], cuts: d.cuts || {} }); applyImport('已导入设计台项目'); }
    else if (p.kind === 'ascii') { S.grid = p.rows; applyImport('已导入 ASCII 关卡'); }
    else askMapping(p);
  } catch (err) { toast('导入失败：' + err.message); }
};
function applyImport(msg) { snapshot(); syncInputs(); fitZoom(); W = makeWorld(S.proposals); renderAll(); toast(msg); }
function askMapping(p) {
  const values = [...new Set(p.nums.flat())].filter(v => v).sort((a, b) => a - b), m = defaultMap(values, p.names);
  $('#mapFrom').textContent = `来自 ${p.from}：${p.nums[0].length}×${p.nums.length} 格，出现了 ${values.length} 种数字（0 = 空气）。给每种数字选一个元素：`;
  const box = $('#mapRows'); box.innerHTML = '';
  const opts = ['<option value="">空气</option>', ...[...W.info.values()].filter(e => e.c !== '.' && e.c !== ' ').map(e => `<option value="${e.c}">${e.c}  ${e.zh}</option>`)].join('');
  for (const v of values) { const l = document.createElement('span'); l.textContent = `数字 ${v}${p.names && p.names[v] ? `（${p.names[v]}）` : ''}`; const s = document.createElement('select'); s.className = 'f'; s.innerHTML = opts; s.value = m[v] || ''; s.dataset.v = v; box.append(l, s); }
  const dlg = $('#mapDlg'); dlg.showModal();
  $('#mapCancel').onclick = () => dlg.close();
  $('#mapOk').onclick = () => { const map = {}; box.querySelectorAll('select').forEach(s => map[s.dataset.v] = s.value); dlg.close(); S.grid = numbersToAscii(p.nums, map, $('#mapFrame').checked); S.name = S.name || '导入的关卡'; applyImport(`已从 ${p.from} 导入`); };
}
function importImage(f) {
  const img = new Image(); img.onload = () => {
    if (img.width > 128 || img.height > 48) { toast(`图太大（${img.width}×${img.height}）：1 像素 = 1 格，最多 128×48`); return; }
    const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const x = c.getContext('2d'); x.drawImage(img, 0, 0);
    const els = [...W.info.values()].filter(e => e.s1 && e.c !== ' ');
    const r = imageToAscii(x.getImageData(0, 0, img.width, img.height).data, img.width, img.height, els);
    S.grid = r.rows; S.name = S.name || f.name.replace(/\.[^.]+$/, '');
    applyImport(`已从像素图导入：${Object.entries(r.used).map(([k, n]) => k + '×' + n).join(' ')}`);
  };
  img.onerror = () => toast('这张图读不了'); img.src = URL.createObjectURL(f);
}

// ── 顶部/工具条 ──────────────────────────────────────
document.querySelectorAll('.tab').forEach(t => t.onclick = () => {
  document.querySelectorAll('.tab').forEach(x => x.setAttribute('aria-selected', x === t));
  const p = t.dataset.page; $('#pageDesign').style.display = p === 'design' ? '' : 'none';
  $('#pageMech').classList.toggle('on', p === 'mech'); $('#pageHandoff').classList.toggle('on', p === 'handoff');
  if (p === 'handoff') $('#handoffText').textContent = handoff();
  if (p === 'mech') { renderPropList(); renderCuts(); renderLaws(); }
});
document.querySelectorAll('[data-tool]').forEach(b => b.onclick = () => { S.tool = b.dataset.tool; document.querySelectorAll('[data-tool]').forEach(x => x.setAttribute('aria-pressed', x === b)); });
$('#btnUndo').onclick = () => { if (!undo.length) return; redo.push(JSON.stringify(S.grid)); S.grid = JSON.parse(undo.pop()); syncInputs(); recheck(true); };
$('#btnRedo').onclick = () => { if (!redo.length) return; undo.push(JSON.stringify(S.grid)); S.grid = JSON.parse(redo.pop()); syncInputs(); recheck(true); };
window.addEventListener('keydown', e => {
  if (e.target.matches('input,textarea,select')) return;
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); $('#btnUndo').click(); }
  else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') { e.preventDefault(); $('#btnRedo').click(); }
  else { const m = { b: 'brush', r: 'rect', f: 'fill', i: 'pick' }[e.key.toLowerCase()]; if (m && !e.ctrlKey) document.querySelector(`[data-tool=${m}]`).click(); }
});
for (const [id, k] of [['ovRoute', 'route'], ['ovDead', 'dead'], ['ovWorst', 'worst'], ['ovNotes', 'notes'], ['ovJump', 'jump'], ['ovUnreach', 'unreach']]) $('#' + id).onchange = e => { S.ov[k] = e.target.checked; draw(); };
$('#zoom').oninput = e => { S.zoom = +e.target.value; draw(); };
$('#palSearch').oninput = renderPalette;
$('#showAll').onchange = e => { S.showAll = e.target.checked; renderPalette(); };
const sel = $('#sampleSel'); for (const n of Object.keys(SAMPLES)) sel.add(new Option(n, n));
$('#btnLoad').onclick = () => { if (!confirm(`载入"${sel.value}"会替换当前画布（批注也清空）。继续？`)) return; snapshot(); S.grid = SAMPLES[sel.value].slice(); S.notes = []; S.name = sel.value + '（改）'; syncInputs(); fitZoom(); recheck(true); renderNotes(); };
$('#btnBlank').onclick = () => { if (!confirm('新建空白房间会替换当前画布。继续？')) return; snapshot(); S.grid = blank(+$('#inW').value || 48, +$('#inH').value || 12); S.notes = []; S.name = ''; syncInputs(); recheck(true); renderNotes(); };
$('#btnResize').onclick = () => { const w = Math.max(12, Math.min(128, +$('#inW').value)), h = Math.max(6, Math.min(48, +$('#inH').value)); snapshot(); resize(w, h); recheck(true); };
$('#lvName').oninput = e => { S.name = e.target.value; save(); };
$('#lvGoal').oninput = e => { S.goal = e.target.value; save(); };
function fitZoom() { const st = $('#stage'); const z = Math.floor(Math.min((st.clientWidth - 48) / Wd(), (st.clientHeight - 48) / H())); S.zoom = Math.max(10, Math.min(36, z)); $('#zoom').value = S.zoom; }
function syncInputs() { $('#inW').value = Wd(); $('#inH').value = H(); $('#lvName').value = S.name; $('#lvGoal').value = S.goal; }
function renderAll() { renderPalette(); renderBrush(); renderNotes(); recheck(true); }

load();
if (!S.grid.length) { S.grid = SAMPLES['诱捕走廊'].slice(); S.name = '诱捕走廊（改）'; }
W = makeWorld(S.proposals); syncInputs(); fitZoom(); renderAll();
