// ── 状态 ──────────────────────────────────────────────
const LS = 'mariotrickster.studio.v1', LS2 = 'mariotrickster.studio.v2';
const S = {
  grid: [], name: '', goal: '', notes: [], proposals: [], cuts: {},
  brush: '#', tool: 'brush', zoom: 20, showAll: false,
  ov: { route: true, dead: true, worst: false, notes: true, jump: true, unreach: true, view: false, beats: true, cover: true }, beats: null, stamp: 'ambush'
};
// S207 移动工具：moveSel = 选中的块 {x0,y0,x1,y1}；moveFrom = 拖动起点；moveBox = 框选起点；clip = 复制的块
let moveSel = null, moveFrom = null, moveBox = null, clip = null, prevTool = 'brush';
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

// S206 多关卡：LIB = [{id,name,goal,grid,notes}]，S.* 是当前打开的那一关
let LIB = [], CUR = '';
const newId = () => 'L' + Date.now().toString(36) + Math.random().toString(36).slice(2, 5);
function storeCurrent() { const i = LIB.findIndex(l => l.id === CUR); const rec = { id: CUR, name: S.name, goal: S.goal, grid: S.grid.slice(), notes: S.notes.slice(), beats: S.beats || undefined }; if (i >= 0) LIB[i] = rec; else LIB.push(rec); }
function save() { try { storeCurrent(); localStorage.setItem(LS2, JSON.stringify({ lib: LIB, cur: CUR, proposals: S.proposals, cuts: S.cuts })); } catch (e) { } renderLib(); }
function load() {
  try {
    const d2 = JSON.parse(localStorage.getItem(LS2) || 'null');
    if (d2 && d2.lib && d2.lib.length) { LIB = d2.lib; CUR = d2.cur || LIB[0].id; S.proposals = d2.proposals || []; S.cuts = d2.cuts || {}; openLevel(CUR, true); return; }
    const d = JSON.parse(localStorage.getItem(LS) || 'null'); // 旧版（单关卡）自动迁移
    if (d && d.grid && d.grid.length) { S.proposals = d.proposals || []; S.cuts = d.cuts || {}; CUR = newId(); LIB = [{ id: CUR, name: d.name || '我的关卡', goal: d.goal || '', grid: d.grid, notes: d.notes || [] }]; openLevel(CUR, true); }
  } catch (e) { }
}
function openLevel(id, quiet) {
  if (!quiet && CUR) storeCurrent();
  const l = LIB.find(x => x.id === id) || LIB[0]; if (!l) return;
  CUR = l.id; S.name = l.name; S.goal = l.goal || ''; S.grid = l.grid.slice(); S.notes = (l.notes || []).slice(); S.beats = l.beats && l.beats.length === 5 ? l.beats.slice() : null; undo = []; redo = [];
  if (!quiet) { syncInputs(); fitZoom(); recheck(true); renderNotes(); }
}
const levelStatus = new Map();
function statusOf(l) { const k = l.grid.join('\n') + '|' + S.proposals.map(p => p.c).join(''); const c = levelStatus.get(l.id); if (c && c.k === k) return c.v; const r = check(makeWorld(S.proposals), l.grid, true); const v = r.playable ? 'ok' : 'bad'; levelStatus.set(l.id, { k, v }); return v; }
function renderLib() {
  const box = $('#libList'); if (!box) return; box.innerHTML = '';
  LIB.forEach(l => {
    const b = document.createElement('button'); b.setAttribute('aria-current', l.id === CUR);
    const st = l.id === CUR && R ? (R.playable ? 'ok' : 'bad') : statusOf(l);
    const props = [...new Set(l.grid.join(''))].filter(c => S.proposals.some(p => p.c === c)).join('');
    b.innerHTML = `<span class="st" style="background:${st === 'ok' ? 'var(--ok)' : 'var(--bad)'}"></span><span class="nm">${(l.name || '未命名').replace(/</g, '&lt;')}</span><small>${l.grid[0].length}×${l.grid.length}${props ? ' ⏳' + props : ''}</small>`;
    b.title = (st === 'ok' ? '✓ 预检通过' : '✗ 有问题') + (props ? `\n用到还没实现的提案：${props}` : '');
    b.onclick = () => { if (l.id !== CUR) openLevel(l.id); };
    box.appendChild(b);
  });
  $('#libCount').textContent = `· ${LIB.length}`;
}

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
  const run = () => { W = makeWorld(S.proposals); R = check(W, S.grid, true); R.passes = routePasses(R.route, R.times, R.onRoute); R.rhythm = rhythm(R.passes, R.seconds); R.cover = coverHints(S.grid, R.onRoute); renderSide(); draw(); save(); };
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
  // S207 游戏一屏：大房间里游戏镜头一次能看到多大（以鼠标 / 捣蛋者为中心）
  if (S.ov.view) {
    const cp = cameraPlan(w, h), t = find(S.grid, 'T')[0], c = hover || t;
    if (c) {
      const x0 = Math.max(0, Math.min(w - cp.viewW, c[0] - Math.floor(cp.viewW / 2))), y0 = Math.max(0, Math.min(h - cp.viewH, c[1] - Math.floor(cp.viewH / 2)));
      ctx.strokeStyle = '#6fc3ff'; ctx.lineWidth = 2; ctx.setLineDash([8, 4]); ctx.strokeRect(x0 * z, (h - y0 - cp.viewH) * z, cp.viewW * z, cp.viewH * z); ctx.setLineDash([]); ctx.lineWidth = 1;
      ctx.fillStyle = 'rgba(22,19,31,.85)'; ctx.fillRect(x0 * z + 2, (h - y0 - cp.viewH) * z + 2, 150, 18); ctx.fillStyle = '#6fc3ff'; ctx.textAlign = 'left';
      ctx.font = '600 11px JetBrains Mono, monospace'; ctx.fillText(cp.big ? `游戏一屏 ≈ ${cp.viewW}×${cp.viewH}` : '整间房一屏装下', x0 * z + 6, (h - y0 - cp.viewH) * z + 11); ctx.textAlign = 'center';
    }
  }
  // S207 移动工具：选中框（黄）+ 拖动预览（白虚线）
  if (S.tool === 'move' && moveSel) {
    const box = (s, col, dash) => { ctx.strokeStyle = col; ctx.lineWidth = 2; ctx.setLineDash(dash); ctx.strokeRect(s.x0 * z, (h - 1 - s.y1) * z, (s.x1 - s.x0 + 1) * z, (s.y1 - s.y0 + 1) * z); ctx.setLineDash([]); ctx.lineWidth = 1; };
    box(moveSel, '#ffc83d', []);
    if (moveFrom && hover) { const [dx, dy] = moveOffset(); if (dx || dy) box({ x0: moveSel.x0 + dx, y0: moveSel.y0 + dy, x1: moveSel.x1 + dx, y1: moveSel.y1 + dy }, '#fff', [4, 3]); }
  }
  if (S.tool === 'move' && moveBox && hover) { const b = selBox(moveBox[0], moveBox[1], hover[0], hover[1]); ctx.strokeStyle = '#ffc83d'; ctx.setLineDash([4, 3]); ctx.strokeRect(b.x0 * z, (h - 1 - b.y1) * z, (b.x1 - b.x0 + 1) * z, (b.y1 - b.y0 + 1) * z); ctx.setLineDash([]); }
  // S208 起承转合分段框
  const bb = S.ov.beats ? beatBounds(S.grid, S.beats) : null;
  if (bb) {
    ctx.save(); ctx.strokeStyle = 'rgba(255,200,61,.55)'; ctx.setLineDash([6, 5]); ctx.lineWidth = 1.5; ctx.textAlign = 'left';
    ctx.font = `800 ${Math.max(11, z * 0.7)}px 'Bricolage Grotesque','Noto Sans SC',sans-serif`;
    for (let i = 0; i < 4; i++) {
      const x0 = bb[i] * z, x1 = bb[i + 1] * z;
      ctx.fillStyle = i % 2 ? 'rgba(255,200,61,.035)' : 'rgba(255,200,61,.07)'; ctx.fillRect(x0, z, x1 - x0, (h - 2) * z);
      ctx.beginPath(); ctx.moveTo(x0, z); ctx.lineTo(x0, (h - 1) * z); ctx.stroke();
      ctx.fillStyle = 'rgba(255,200,61,.9)'; ctx.fillText(BEAT_ZH[i], x0 + 5, z + Math.max(11, z * 0.7));
      ctx.fillStyle = 'rgba(255,200,61,.55)'; ctx.font = `600 ${Math.max(9, z * 0.42)}px 'Noto Sans SC',sans-serif`;
      ctx.fillText(['教', '加深', '意外', '收尾'][i], x0 + 5 + Math.max(14, z * 0.9), z + Math.max(11, z * 0.7)); ctx.font = `800 ${Math.max(11, z * 0.7)}px 'Bricolage Grotesque','Noto Sans SC',sans-serif`;
    }
    ctx.beginPath(); ctx.moveTo(bb[4] * z, z); ctx.lineTo(bb[4] * z, (h - 1) * z); ctx.stroke(); ctx.restore();
  }
  // S208 转移点提示：机关 5 格内没有草丛/箱子/隔墙
  if (R && R.cover && S.ov.cover) {
    ctx.strokeStyle = '#ffb347'; ctx.lineWidth = 2;
    for (const s of R.cover) { ctx.beginPath(); ctx.arc(s.x * z + z / 2, (h - 1 - s.y) * z + z / 2, z * 0.72, 0, 7); ctx.stroke(); }
    ctx.lineWidth = 1;
  }
  // S208 印章预览
  if (S.tool === 'stamp' && hover && !S.ov.worst) {
    const p = patternById(S.stamp), prev = stampPattern(S.grid, p, hover[0], hover[1], S.stampStar || null);
    ctx.globalAlpha = 0.75;
    for (let row = 0; row < h; row++) for (let x = 0; x < w; x++) if (prev[row][x] !== S.grid[row][x]) {
      const c = prev[row][x], rgb = colorOf(c); ctx.fillStyle = rgbCss(rgb); ctx.fillRect(x * z, row * z, z - 1, z - 1);
      if (!'#W=.'.includes(c) && z >= 12) { ctx.fillStyle = textOn(rgb); ctx.fillText(c, x * z + z / 2, row * z + z / 2 + 1); }
    }
    ctx.globalAlpha = 1; const pw = p.rows[0].length, top = hover[1] + p.stand;
    ctx.strokeStyle = '#ffc83d'; ctx.setLineDash([4, 3]); ctx.lineWidth = 2; ctx.strokeRect(hover[0] * z, (h - 1 - top) * z, pw * z, p.rows.length * z); ctx.setLineDash([]); ctx.lineWidth = 1;
  }
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
  // S207：Ctrl+点击 / 鼠标中键 = 随时吸一下（不用切工具）
  if (e.ctrlKey || e.metaKey || e.button === 1) { e.preventDefault(); pickAt(c, false); return; }
  const erase = e.button === 2, ch = erase ? '.' : S.brush;
  if (S.tool === 'pick' && !erase) { pickAt(c, true); return; }
  if (S.tool === 'move') { moveDown(c, erase); return; }
  if (S.tool === 'stamp') { if (erase) { setTool(prevTool === 'stamp' ? 'brush' : prevTool); return; } if (S.ov.worst) { toast('正在"最坏情况预览"（只是看，不能画）→ 先取消勾选它'); return; } snapshot(); S.grid = stampPattern(S.grid, patternById(S.stamp), c[0], c[1], S.stampStar || null); recheck(true); toast(`盖上了「${patternById(S.stamp).zh}」→ 看右边检查结果，不对就 Ctrl+Z`); return; }
  if (S.ov.worst) { toast('正在"最坏情况预览"（只是看，不能画）→ 先取消勾选它'); return; }
  snapshot();
  if (S.tool === 'fill' && !erase) { floodFill(c[0], c[1], ch); recheck(); draw(); return; }
  if (S.tool === 'rect' && !erase) { dragStart = c; painting = true; return; }
  painting = ch; setCell(c[0], c[1], ch); draw(); recheck();
});
window.addEventListener('mouseup', () => {
  if (S.tool === 'move') { moveUp(); return; }
  if (dragStart && hover) { const [a, b] = rectOf(dragStart, hover); for (let x = a[0]; x <= b[0]; x++) for (let y = a[1]; y <= b[1]; y++) setCell(x, y, S.brush); recheck(); }
  dragStart = null; painting = false; draw();
});
cv.addEventListener('mousemove', e => {
  const c = cellFromEvent(e); const changed = !hover || !c || hover[0] !== c[0] || hover[1] !== c[1]; hover = c;
  if (c) { const ch = cellAt(c[0], c[1]), en = W.info.get(ch); const n = S.notes.find(n => n.x === c[0] && n.y === c[1]); $('#hoverInfo').textContent = `(${c[0]},${c[1]})  '${ch}'  ${en ? en.zh : ch === '.' ? '空气' : SLOT_CHARS[ch] ? '随机槽位' : '?'}${n ? '  📝 ' + n.text : ''}`; }
  if (painting && typeof painting === 'string' && c && !S.ov.worst && setCell(c[0], c[1], painting)) recheck();
  if (changed) draw();
});
cv.addEventListener('mouseleave', () => { hover = null; draw(); });
// ── S207 吸管 & 移动工具 ─────────────────────────────
function pickAt(c, fromTool) {
  const ch = cellAt(c[0], c[1]);
  if (ch === 'W') { toast('外圈墙不能吸（它固定不动）'); return; }
  setBrush(ch); const en = W.info.get(ch);
  if (fromTool) setTool(prevTool === 'pick' || prevTool === 'move' ? 'brush' : prevTool);
  toast(`吸管：画笔换成「${en ? en.zh : ch === '.' ? '空气（= 橡皮）' : ch}」→ 已回到${TOOL_ZH[S.tool]}，直接画`);
}
const TOOL_ZH = { brush: '画笔', rect: '矩形', fill: '填充', pick: '吸管', move: '移动', stamp: '印章' };
function setTool(t) {
  if (t !== S.tool && S.tool !== 'pick') prevTool = S.tool;
  S.tool = t; if (t !== 'move') { moveSel = null; moveFrom = null; moveBox = null; }
  document.querySelectorAll('[data-tool]').forEach(x => x.setAttribute('aria-pressed', x.dataset.tool === t));
  $('#toolHint').textContent = TOOL_HINT[t] || ''; if ($('#stamps')) renderStamps(); draw();
}
const TOOL_HINT = {
  brush: '画笔：左键画、右键擦。Ctrl+点击 = 吸取格子里的东西',
  rect: '矩形：按住拖出一块，松手填满',
  fill: '填充：把连在一起的同种格子一次换掉',
  pick: '吸管：点一个已放的东西 → 画笔变成它，自动回到上一个工具',
  stamp: '印章：鼠标放在马里奥站的那一格（地面上一格）点一下，盖上左边选中的模式 · 右键 = 退出印章 · 盖错了 Ctrl+Z',
  move: '移动：点住东西拖走（相连的同种一起走）· 空白处拖 = 框选 · 方向键微调 · Del 删除 · Ctrl+C / Ctrl+V 复制到鼠标处 · Esc 取消',
};
function moveOffset() { if (!moveFrom || !hover || !moveSel) return [0, 0]; return clampMove(Wd(), H(), moveSel, hover[0] - moveFrom[0], hover[1] - moveFrom[1]); }
const inSel = (s, c) => s && c[0] >= s.x0 && c[0] <= s.x1 && c[1] >= s.y0 && c[1] <= s.y1;
function moveDown(c, right) {
  if (right) { moveSel = null; draw(); return; }
  if (!inSel(moveSel, c)) moveSel = selectAt(S.grid, c[0], c[1]);
  if (moveSel) moveFrom = c; else moveBox = c;
  draw();
}
function moveUp() {
  if (moveFrom && moveSel) { const [dx, dy] = moveOffset(); if (dx || dy) applyMove(dx, dy); }
  else if (moveBox && hover) { moveSel = selBox(moveBox[0], moveBox[1], hover[0], hover[1]); }
  moveFrom = null; moveBox = null; draw();
}
function applyMove(dx, dy) {
  [dx, dy] = clampMove(Wd(), H(), moveSel, dx, dy); if (!dx && !dy) return;
  snapshot(); S.grid = moveBlock(S.grid, moveSel, dx, dy);
  moveSel = { x0: moveSel.x0 + dx, y0: moveSel.y0 + dy, x1: moveSel.x1 + dx, y1: moveSel.y1 + dy }; recheck();
}
function moveKey(e) {
  const k = e.key, ctrl = e.ctrlKey || e.metaKey;
  if (ctrl && k.toLowerCase() === 'c') { if (!moveSel) return false; clip = copyBlock(S.grid, moveSel); toast(clip.length ? `复制了 ${clip.length} 格（马里奥/捣蛋者/宝物/出口不复制）→ 鼠标放到目标处按 Ctrl+V` : '选区里没有能复制的东西'); return true; }
  if (ctrl && k.toLowerCase() === 'v') { if (!clip || !clip.length || !hover) return false; snapshot(); S.grid = pasteBlock(S.grid, clip, hover[0], hover[1]); recheck(); return true; }
  if (k === 'Escape') { moveSel = null; draw(); return true; }
  if (!moveSel) return false;
  if (k === 'Delete' || k === 'Backspace') { snapshot(); S.grid = clearBlock(S.grid, moveSel); moveSel = null; recheck(); return true; }
  const d = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, 1], ArrowDown: [0, -1] }[k];
  if (d) { applyMove(d[0], d[1]); return true; }
  return false;
}
function addNote(c) {
  const old = S.notes.find(n => n.x === c[0] && n.y === c[1]);
  const t = prompt(`给 (${c[0]},${c[1]}) 写一句批注（想法 / 这里要什么效果 / 问题）。留空 = 删除`, old ? old.text : '');
  if (t === null) return;
  S.notes = S.notes.filter(n => n !== old); if (t.trim()) S.notes.push({ x: c[0], y: c[1], text: t.trim() });
  renderNotes(); draw(); save();
}

// ── 左侧：元素库 ─────────────────────────────────────
function setBrush(c) { S.brush = c; if (S.tool === 'move') setTool('brush'); renderPalette(); renderBrush(); }
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
  const rb = $('#rhythm'); rb.innerHTML = ''; $('#rhSum').textContent = '';
  if (R.rhythm && R.seconds > 0) {
    for (const g of R.rhythm.segs) { const i = document.createElement('i'); i.className = g.busy ? 'busy' : 'rest'; i.style.left = (g.a / R.seconds * 100) + '%'; i.style.width = Math.max(0.6, (g.b - g.a) / R.seconds * 100) + '%'; i.title = `${g.a.toFixed(1)}–${g.b.toFixed(1)} 秒 · ${g.busy ? '经过机关' : '喘气'}`; rb.appendChild(i); }
    const busy = R.rhythm.segs.filter(g => g.busy).reduce((a, g) => a + g.b - g.a, 0);
    $('#rhSum').textContent = `紧张 ${Math.round(busy / R.seconds * 100)}% · ${R.passes.length} 次经过机关`;
  }
  const extra = [...(R.rhythm ? R.rhythm.warn.map(w => ({ x: -1, y: -1, t: '节奏：' + w.t, sev: 'warn' })) : []),
    ...(R.cover && R.cover.length ? [{ x: R.cover[0].x, y: R.cover[0].y, t: `转移点：${R.cover.length} 个机关 5 格内没有草丛/箱子可以躲（橙色圈）——你走过去时会被看见。提前伪装好等他来，或旁边放一丛草 b`, sev: 'info' }] : [])];
  const box = $('#issues'); box.innerHTML = '';
  const allIssues = R.issues.concat(extra);
  if (!allIssues.length) box.innerHTML = '<div class="empty">没有发现问题。去"新机制提案"想点新东西，或者直接"交给 AI"。</div>';
  for (const i of allIssues) {
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
function readForm() { return { status: $('#mStatus').value, c: $('#mChar').value, key: $('#mKey').value.trim(), zh: $('#mZh').value.trim(), role: $('#mRole').value, color: $('#mColor').value, solid: $('#mSolid').checked, support: $('#mSupport').checked, terrain: $('#mTerrain').checked, control: $('#mControl').checked, effect: $('#mEffect').value.trim(), tele: $('#mTele').value.trim(), cost: $('#mCost').value.trim(), counter: $('#mCounter').value.trim(), place: $('#mPlace').value.trim(), combo: $('#mCombo').value.trim() }; }
function fillForm(p) { $('#mStatus').value = p.status || 'idea'; $('#mChar').value = p.c || ''; $('#mKey').value = p.key || ''; $('#mZh').value = p.zh || ''; $('#mRole').value = p.role || 'PlayerPrank'; $('#mColor').value = p.color || '#b08cff'; $('#mSolid').checked = !!p.solid; $('#mSupport').checked = p.support !== false; $('#mTerrain').checked = !!p.terrain; $('#mControl').checked = !!p.control;['Effect', 'Tele', 'Cost', 'Counter', 'Place', 'Combo'].forEach(k => $('#m' + k).value = p[k.toLowerCase()] || ''); renderLaws(); }
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
    const stt = { idea: ['idea', '💡 想法'], go: ['go', '✅ 确认要做'], done: ['ok', '✔ 已实现'] }[p.status || 'idea'];
    const usedIn = p.c ? LIB.filter(l => l.grid.join('').includes(p.c)).map(l => l.name) : [];
    d.innerHTML = `<div class="sw" style="background:${rgbCss(rgb)};color:${textOn(rgb)}">${p.c || '★'}</div><div><h4>${p.zh} <span class="badge ${stt[0]}">${stt[1]}</span> <span class="hint">${ROLE_ZH[p.role] || p.role}</span></h4>${usedIn.length ? `<p>用在：${usedIn.join('、')}</p>` : ''}<p>${p.effect}</p><p>代价：${p.cost || '<span style="color:var(--warn)">没写</span>'} · 反制：${p.counter || '<span style="color:var(--warn)">没写</span>'}</p></div><div style="display:flex;flex-direction:column;gap:4px"><button class="btn small">编辑</button><button class="btn small">删除</button></div>`;
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
function levelSection(L, name, goal, grid, notes, W2) {
  const r = check(W2, grid, true), cell = (x, y) => grid[grid.length - 1 - y][x];
  L.push('', `## 关卡：${name || '（未命名）'}`);
  if (goal) L.push('', `**设计意图**：${goal}`);
  L.push('', `尺寸 ${grid[0].length}×${grid.length}；网页预检：${r.playable ? '✓ 可以试玩' : `✗ ${r.issues.filter(i => i.sev === 'error').length} 个问题`}；马里奥一趟约 ${r.seconds ? r.seconds.toFixed(0) : '?'} 秒，路上 ${r.onRoute.length} 个机关${r.unreach && r.unreach.size ? `；${r.unreach.size} 个能站但上不去的格` : ''}。`);
  if (r.onRoute.length) L.push('', '**马里奥时间线**：' + r.onRoute.map(s => `${s.at.toFixed(0)}s ${W2.info.get(s.c) ? W2.info.get(s.c).zh : s.c}(${s.x},${s.y})`).join(' → '));
  const errs = r.issues.filter(i => i.sev !== 'info');
  if (errs.length) { L.push('', '**网页预检发现的问题**（请修掉或说明）：'); errs.forEach(i => L.push(`- ${i.x >= 0 ? `(${i.x},${i.y}) ` : ''}${i.t}`)); }
  const pend = [...new Set(grid.join(''))].filter(c => S.proposals.some(p => p.c === c && p.status !== 'done'));
  if (pend.length) L.push('', `**用到还没实现的新机制**：${pend.map(c => `\`${c}\` ${S.proposals.find(p => p.c === c).zh}`).join('、')}（Unity 导入时先当空气，实现后重新导入关卡包自动还原）`);
  L.push('', '```text', ...grid, '```');
  if (notes && notes.length) { L.push('', '**格子批注**（坐标 x 从左 0、y 从下 0）：'); notes.forEach(n => L.push(`- (${n.x},${n.y}) '${cell(n.x, n.y)}'：${n.text}`)); }
}
function handoffOverworld() {
  if (!OWLIB.length) return '';
  const L = ['', '## 大地图（小镇）'];
  for (const m of OWLIB) { const r = owCheck(m, OW.Rules, owRoomOk); L.push('', `### ${m.name}  ${r.headline}`); if (m.goal) L.push(m.goal); for (const d of m.doors) L.push(`- 门 ${d.n}  ${owClock(d.minute)} → 房间「${d.room}」`); for (const i of r.issues) L.push(`- [${i.sev}] ${owIssueText(i)}`); L.push('```', owToText(m).trimEnd(), '```'); }
  return L.join('\n');
}
function handoff() {
  storeCurrent();
  const L = [], all = $('#hAll') ? $('#hAll').checked : true, levels = all ? LIB : LIB.filter(l => l.id === CUR);
  L.push('# MarioTrickster 设计单（来自关卡设计台）', '');
  L.push('> 给 AI：请加载接续包（mariotrickster-continue），按这份设计单更新项目（references/web-studio.md）。同时附了关卡包 .levelpack.json（所有关卡数据）。', '> 顺序：① 复述计划 ② 实现"✅ 确认要做"的新机制（new-element 16 步）③ 关卡导入关卡库 / 需要的加成样板，跑完整体检 ④ 处理删改项（先说影响）⑤ 重建网页设计台 ⑥ 交付升级包 + 新接续包 + 新网页。"💡 想法"的提案只讨论、给建议，不实现。', '');
  L.push(`共 ${levels.length} 关：${levels.map(l => l.name || '未命名').join('、')}`);
  for (const l of levels) levelSection(L, l.name, l.goal, l.grid, l.notes, W);
  const props = S.proposals;
  if (props.length) {
    L.push('', '## 新机制提案');
    const order = { go: 0, idea: 1, done: 2 };
    for (const p of props.slice().sort((a, b) => (order[a.status || 'idea'] - order[b.status || 'idea']))) {
      const st = { idea: '💡 想法（先讨论，不实现）', go: '✅ 确认要做', done: '✔ 已实现' }[p.status || 'idea'];
      const usedIn = p.c ? LIB.filter(l => l.grid.join('').includes(p.c)).map(l => l.name) : [];
      L.push('', `### ${p.zh}${p.c ? `（字符 \`${p.c}\`，主题键 ${p.key || '待定'}）` : ''} — ${ROLE_ZH[p.role] || p.role} — **${st}**${usedIn.length ? ` · 用在：${usedIn.join('、')}` : ''}`);
      L.push(`- 做什么：${p.effect}`, `- 预警（H3/H6）：${p.tele || '**未写**'}`, `- 代价（A2）：${p.cost || '**未写**'}`, `- 马里奥怎么反制/发现（H4）：${p.counter || '**未写**'}`);
      if (p.place) L.push(`- 放在哪：${p.place}`); if (p.combo) L.push(`- 能连的机关：${p.combo}`);
      L.push(`- 属性：${p.solid ? '实心' : '可穿过'}${p.support ? '、脚下要实心' : ''}${p.terrain ? '、**会改变地形 → 要进死局/炸弹策略模拟（H1/H9）**' : ''}${p.control ? '、**会控制人 → 必须有时长、必然结束（H9）**' : ''}`);
      const need = LAWS.filter(([, , f]) => f(p)).map(([id, t]) => `${id} ${t}`); if (need.length) L.push(`- 还需要说清：${need.join('；')}`);
    }
  }
  const cuts = Object.entries(S.cuts);
  if (cuts.length) { L.push('', '## 想删掉 / 改掉的旧东西'); cuts.forEach(([c, why]) => { const e = W.info.get(c); L.push(`- \`${c}\` ${e ? e.zh : c}：${why}`); }); L.push('', '（删之前请告诉我影响：哪些样板/测试/连锁在用它。）'); }
  L.push('', '---', `导出时间：${new Date().toLocaleString('zh-CN')} · 设计台规则版本：S207（搭建范围 12–128 × 6–48；宽 >64 或高 >16 = 大房间，游戏里镜头智能跟随、外圈实心；跳高 2 格、平跳 4 格；弹簧头顶 4 格、炮口前 3 格、毒池 ≤3 格）`);
  return L.join('\n') + handoffOverworld();
}

function download(name, text, type) { const a = document.createElement('a'); a.href = URL.createObjectURL(new Blob([text], { type })); a.download = name; a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 1000); }
$('#hCopy').onclick = async () => { const t = handoff(); try { await navigator.clipboard.writeText(t); toast('设计单已复制'); } catch (e) { const ta = document.createElement('textarea'); ta.value = t; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove(); toast('设计单已复制'); } };
$('#hDownload').onclick = () => download(`设计单_${($('#hAll').checked ? LIB.length + '关' : S.name || 'level').replace(/[\\/:*?"<>|]/g, '')}.md`, handoff(), 'text/markdown');
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
function levelPack() { storeCurrent(); return { type: 'mariotrickster-levelpack', v: 1, rules: 'S208', exported: new Date().toISOString(), levels: LIB.map(l => Object.assign({ id: l.id, name: l.name || '未命名', goal: l.goal || '', grid: l.grid, notes: l.notes || [] }, l.beats ? { beats: l.beats } : {})), overworlds: OWLIB.map(owToJson), proposals: S.proposals, cuts: S.cuts }; }
const exportPack = () => { const d = new Date(), stamp = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`; download(`MarioTrickster关卡包_${LIB.length}关_${stamp}.levelpack.json`, JSON.stringify(levelPack(), null, 1), 'application/json'); };
$('#btnExport').onclick = exportPack;
$('#hPack').onclick = exportPack;
$('#btnImport').onclick = () => $('#fileIn').click();
$('#fileIn').onchange = async e => {
  const f = e.target.files[0]; if (!f) return; e.target.value = '';
  try {
    if (/\.(png|gif|bmp)$/i.test(f.name)) return importImage(f);
    const p = /^# Overworld:/m.test(await f.text()) ? { kind: 'owtxt' } : parseForeign(await f.text(), f.name);
    const txt = await f.text();
    if (/^# Overworld:/m.test(txt)) { owAdd(owParse(txt), true); return; }
    if (/\.(csv)$/i.test(f.name) && $('#pageOverworld').classList.contains('on')) { const nums = txt.trim().split(/\r?\n/).map(r => r.split(/[,;\t]/).map(v => parseInt(v, 10) || 0)); owAdd({ kind: 'overworld', name: f.name.replace(/\..*$/, ''), goal: '', id: '', rows: owFromNumbers(nums), doors: [], notes: [] }, true); return; }
    if (p.kind === 'pack') { importPackData(p.pack); owImportPack(p.pack); return; }
    if (p.kind === 'studio') { const d = p.studio; mergeProposals(d.proposals); S.cuts = Object.assign(S.cuts, d.cuts || {}); addImported(d.name || f.name.replace(/\..*$/, ''), d.grid, d.goal, d.notes); return; }
    else if (p.kind === 'ascii') { addImported(nameFromTxt(p.rows, f.name), p.rows.filter(r => !r.startsWith('#')), goalFromTxt(p.rows), []); return; }
    else askMapping(p);
  } catch (err) { toast('导入失败：' + err.message); }
};
function mergeProposals(list) { for (const p of (list || [])) { S.proposals = S.proposals.filter(x => (p.c ? x.c !== p.c : x.zh !== p.zh)); S.proposals.push(p); } W = makeWorld(S.proposals); }
function addImported(name, grid, goal, notes) { storeCurrent(); const existing = LIB.find(l => l.name === name); if (existing && confirm(`已有同名关卡"${name}"，覆盖它吗？（取消 = 另存为新关卡）`)) { Object.assign(existing, { grid, goal: goal || '', notes: notes || [] }); CUR = existing.id; } else { CUR = newId(); LIB.push({ id: CUR, name: existing ? uniqueName(name) : name, goal: goal || '', grid, notes: notes || [] }); } openLevel(CUR, true); applyImport(`已导入"${S.name}"`); }
function importPackData(pk) { mergeProposals(pk.proposals); S.cuts = Object.assign(S.cuts, pk.cuts || {}); storeCurrent(); let n = 0, over = 0; for (const l of (pk.levels || []).filter(l => l.kind !== 'overworld')) { const ex = LIB.find(x => x.id === l.id || x.name === l.name); if (ex) { Object.assign(ex, { name: l.name, goal: l.goal || '', grid: l.grid, notes: l.notes || [], beats: l.beats }); over++; } else LIB.push({ id: l.id || newId(), name: l.name || '未命名', goal: l.goal || '', grid: l.grid, notes: l.notes || [], beats: l.beats }); n++; } CUR = (LIB.find(x => pk.levels[0] && (x.id === pk.levels[0].id || x.name === pk.levels[0].name)) || LIB[0]).id; openLevel(CUR, true); applyImport(`导入关卡包：${n} 关（其中 ${over} 关同名覆盖）`); }
const nameFromTxt = (rows, fn) => { const m = rows.find(r => r.startsWith('# Name: ')); return m ? m.slice(8).trim() : fn.replace(/\..*$/, ''); };
const goalFromTxt = rows => { const m = rows.find(r => r.startsWith('# Goal: ')); return m ? m.slice(8).trim() : ''; };
function applyImport(msg) { snapshot(); syncInputs(); fitZoom(); W = makeWorld(S.proposals); renderAll(); save(); toast(msg); }
function askMapping(p) {
  const values = [...new Set(p.nums.flat())].filter(v => v).sort((a, b) => a - b), m = defaultMap(values, p.names);
  $('#mapFrom').textContent = `来自 ${p.from}：${p.nums[0].length}×${p.nums.length} 格，出现了 ${values.length} 种数字（0 = 空气）。给每种数字选一个元素：`;
  const box = $('#mapRows'); box.innerHTML = '';
  const opts = ['<option value="">空气</option>', ...[...W.info.values()].filter(e => e.c !== '.' && e.c !== ' ').map(e => `<option value="${e.c}">${e.c}  ${e.zh}</option>`)].join('');
  for (const v of values) { const l = document.createElement('span'); l.textContent = `数字 ${v}${p.names && p.names[v] ? `（${p.names[v]}）` : ''}`; const s = document.createElement('select'); s.className = 'f'; s.innerHTML = opts; s.value = m[v] || ''; s.dataset.v = v; box.append(l, s); }
  const dlg = $('#mapDlg'); dlg.showModal();
  $('#mapCancel').onclick = () => dlg.close();
  $('#mapOk').onclick = () => { const map = {}; box.querySelectorAll('select').forEach(s => map[s.dataset.v] = s.value); dlg.close(); addImported(uniqueName(`从 ${p.from} 导入`), numbersToAscii(p.nums, map, $('#mapFrame').checked), '', []); };
}
function importImage(f) {
  const img = new Image(); img.onload = () => {
    if (img.width > 128 || img.height > 48) { toast(`图太大（${img.width}×${img.height}）：1 像素 = 1 格，最多 128×48`); return; }
    const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const x = c.getContext('2d'); x.drawImage(img, 0, 0);
    const els = [...W.info.values()].filter(e => e.s1 && e.c !== ' ');
    const r = imageToAscii(x.getImageData(0, 0, img.width, img.height).data, img.width, img.height, els);
    addImported(f.name.replace(/\.[^.]+$/, ''), r.rows, '', []);
    toast(`已从像素图导入：${Object.entries(r.used).map(([k, n]) => k + '×' + n).join(' ')}`);
  };
  img.onerror = () => toast('这张图读不了'); img.src = URL.createObjectURL(f);
}

// ── 顶部/工具条 ──────────────────────────────────────
document.querySelectorAll('.tab').forEach(t => t.onclick = () => {
  document.querySelectorAll('.tab').forEach(x => x.setAttribute('aria-selected', x === t));
  const p = t.dataset.page; $('#pageDesign').style.display = p === 'design' ? '' : 'none';
  $('#pageMech').classList.toggle('on', p === 'mech'); $('#pageHandoff').classList.toggle('on', p === 'handoff'); $('#pageOverworld').classList.toggle('on', p === 'overworld');
  if (p === 'overworld') owRender();
  if (p === 'handoff') $('#handoffText').textContent = handoff();
  if (p === 'mech') { renderPropList(); renderCuts(); renderLaws(); }
});
document.querySelectorAll('[data-tool]').forEach(b => b.onclick = () => setTool(b.dataset.tool));
$('#btnUndo').onclick = () => { if (!undo.length) return; redo.push(JSON.stringify(S.grid)); S.grid = JSON.parse(undo.pop()); syncInputs(); recheck(true); };
$('#btnRedo').onclick = () => { if (!redo.length) return; undo.push(JSON.stringify(S.grid)); S.grid = JSON.parse(redo.pop()); syncInputs(); recheck(true); };
window.addEventListener('keydown', e => {
  if (e.target.matches('input,textarea,select')) return;
  if (S.tool === 'move' && moveKey(e)) { e.preventDefault(); return; }
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); $('#btnUndo').click(); }
  else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') { e.preventDefault(); $('#btnRedo').click(); }
  else { const m = { b: 'brush', r: 'rect', f: 'fill', i: 'pick', v: 'move', s: 'stamp' }[e.key.toLowerCase()]; if (m && !e.ctrlKey) document.querySelector(`[data-tool=${m}]`).click(); }
});
for (const [id, k] of [['ovRoute', 'route'], ['ovDead', 'dead'], ['ovWorst', 'worst'], ['ovNotes', 'notes'], ['ovJump', 'jump'], ['ovUnreach', 'unreach'], ['ovView', 'view'], ['ovBeats', 'beats'], ['ovCover', 'cover']]) $('#' + id).onchange = e => { S.ov[k] = e.target.checked; if (k === 'worst') $('#worstBanner').hidden = !e.target.checked; draw(); };
$('#zoom').oninput = e => { S.zoom = +e.target.value; draw(); };
$('#palSearch').oninput = renderPalette;
$('#showAll').onchange = e => { S.showAll = e.target.checked; renderPalette(); };
const sel = $('#sampleSel'); for (const n of Object.keys(SAMPLES)) sel.add(new Option(n, n));
function addLevel(name, grid) { storeCurrent(); CUR = newId(); LIB.push({ id: CUR, name, goal: '', grid, notes: [] }); S.beats = null; openLevel(CUR, true); syncInputs(); fitZoom(); recheck(true); renderNotes(); }
function uniqueName(base) { let n = base, i = 2; while (LIB.some(l => l.name === n)) n = `${base} ${i++}`; return n; }
$('#btnLoad').onclick = () => addLevel(uniqueName(sel.value + '（改）'), SAMPLES[sel.value].slice());
$('#btnBlank').onclick = () => addLevel(uniqueName('新关卡'), blank(+$('#inW').value || 48, +$('#inH').value || 12));
// ── S208 新建关卡向导 ─────────────────────────────────
const WZ = { star: '~', sec: 20 };
const WIZ_STAR_TIP = { '~': '最基础：躲着按准时机', n: '滑一下打乱落点', '[': '挡路 3.5 秒', J: '弹上天，空中不能动', Y: '倒吊 10 秒', Q: '关住 3 秒', K: '远距离，每局 1 发', C: '掉下去换一层玩' };
function renderWizard() {
  $('#wizStars').innerHTML = WIZ_STARS.map(c => { const e = W.info.get(c); return `<button type="button" data-c="${c}" aria-pressed="${WZ.star === c}"><b style="background:${rgbCss(e.rgb)};color:${textOn(e.rgb)}">${c}</b>${e.zh}<small>${WIZ_STAR_TIP[c]}</small></button>`; }).join('');
  $('#wizLens').innerHTML = [[20, '约 20 秒', '48 格宽 · 一屏'], [30, '约 30 秒', '64 格宽 · 一屏'], [40, '约 40 秒', '94 格宽 · 镜头跟着走']].map(([s, a, b]) => `<button type="button" data-s="${s}" aria-pressed="${WZ.sec === s}">${a}<small>${b}</small></button>`).join('');
  const rec = wizardRecipe(WZ.star);
  $('#wizBeats').innerHTML = rec.map(([pid, st], i) => { const p = patternById(pid), e = W.info.get(st); return `<div><b>${BEAT_ZH[i]}</b>${p.zh}（${e ? e.zh : st}）</div>`; }).join('');
  $('#wizStars').querySelectorAll('button').forEach(b => b.onclick = () => { WZ.star = b.dataset.c; renderWizard(); });
  $('#wizLens').querySelectorAll('button').forEach(b => b.onclick = () => { WZ.sec = +b.dataset.s; renderWizard(); });
}
function openWizard() { $('#wizName').value = uniqueName('新关卡'); $('#wizIdea').value = ''; renderWizard(); $('#wizDlg').showModal(); $('#wizIdea').focus(); }
$('#wizCancel').onclick = () => $('#wizDlg').close();
$('#wizBlank').onclick = () => { $('#wizDlg').close(); addLevel(uniqueName($('#wizName').value.trim() || '新关卡'), blank(48, 12)); };
$('#wizOk').onclick = () => {
  const d = wizardLevel(WZ.star, WZ.sec, $('#wizIdea').value); $('#wizDlg').close();
  storeCurrent(); CUR = newId(); LIB.push({ id: CUR, name: uniqueName($('#wizName').value.trim() || '新关卡'), goal: d.goal, grid: d.grid, notes: d.notes, beats: d.beats });
  openLevel(CUR, true); syncInputs(); fitZoom(); recheck(true); renderNotes();
  toast('草稿生成好了：金色虚线是起承转合 4 段，黄三角批注写了每段怎么玩。先导进 Unity 玩一局，再改最别扭的一处');
};
$('#libNew').onclick = openWizard;
// ── S208 模式印章 ─────────────────────────────────────
function renderStamps() {
  $('#stamps').innerHTML = PATTERNS.map(p => `<button type="button" data-id="${p.id}" aria-pressed="${S.tool === 'stamp' && S.stamp === p.id}" title="${p.tip}"><b>${p.zh}</b><pre>${p.rows.map(r => r.replace(/_/g, ' ').replace(/\*/g, p.def)).join('\n')}</pre></button>`).join('');
  $('#stamps').querySelectorAll('button').forEach(b => b.onclick = () => { S.stamp = b.dataset.id; setTool('stamp'); toast(`印章「${patternById(S.stamp).zh}」：${patternById(S.stamp).tip}`); });
}
$('#libDup').onclick = () => { storeCurrent(); addLevel(uniqueName(S.name + ' 副本'), S.grid.slice()); };
$('#libDel').onclick = () => { if (LIB.length <= 1) { toast('至少留一关'); return; } if (!confirm(`删除"${S.name}"？（导出过的关卡包不受影响）`)) return; LIB = LIB.filter(l => l.id !== CUR); CUR = ''; openLevel(LIB[0].id, true); syncInputs(); fitZoom(); recheck(true); renderNotes(); };
$('#btnResize').onclick = () => { const w = Math.max(12, Math.min(128, +$('#inW').value)), h = Math.max(6, Math.min(48, +$('#inH').value)); snapshot(); resize(w, h); recheck(true); };
$('#lvName').oninput = e => { S.name = e.target.value; save(); };
$('#lvName').onchange = e => { const n = e.target.value.trim() || '未命名'; if (LIB.some(l => l.id !== CUR && l.name === n)) { toast('已有同名关卡，自动加了编号（导入 Unity 时同名会覆盖）'); S.name = uniqueName(n); e.target.value = S.name; } save(); };
$('#lvGoal').oninput = e => { S.goal = e.target.value; save(); };
function fitZoom() { const st = $('#stage'); const z = Math.floor(Math.min((st.clientWidth - 48) / Wd(), (st.clientHeight - 48) / H())); S.zoom = Math.max(10, Math.min(36, z)); $('#zoom').value = S.zoom; }
function syncInputs() { $('#inW').value = Wd(); $('#inH').value = H(); $('#lvName').value = S.name; $('#lvGoal').value = S.goal; }
function renderAll() { renderPalette(); renderBrush(); renderNotes(); recheck(true); }

load();
if (!S.grid.length) { CUR = newId(); S.grid = SAMPLES['诱捕走廊'].slice(); S.name = '诱捕走廊（改）'; LIB = [{ id: CUR, name: S.name, goal: '', grid: S.grid.slice(), notes: [] }]; S.firstRun = true; }
W = makeWorld(S.proposals); syncInputs(); fitZoom(); renderAll(); setTool('brush'); renderStamps();
// S208：第一次打开（浏览器里还没有任何关卡）→ 直接弹出向导，不让人对着空白画布发呆
if (S.firstRun) setTimeout(openWizard, 300);

// ── S210 大地图（星露谷视角小镇）─────────────────────────
const LSOW = 'mariotrickster.studio.overworld.v1';
let OWLIB = [], OWCUR = 0, OWT = { brush: '=', tool: 'brush', zoom: 16, drag: null, undo: [], rep: null };
try { const d = JSON.parse(localStorage.getItem(LSOW) || 'null'); if (d && d.lib && d.lib.length) { OWLIB = d.lib.map(owFromJson); OWCUR = Math.min(d.cur | 0, OWLIB.length - 1); } } catch (e) { }
if (!OWLIB.length) OWLIB = [owParse(OW_SAMPLE)];
const owM = () => OWLIB[OWCUR];
function owSave() { try { localStorage.setItem(LSOW, JSON.stringify({ lib: OWLIB.map(owToJson), cur: OWCUR })); } catch (e) { } }
/** 门能连的房间：内置样板 + 你在"画关卡"里的所有关卡。关卡库里同名的优先（和 Unity 一样）。 */
function owRoomNames() { const s = new Set(Object.keys(OW_ROOMS)); for (const l of LIB) s.add(l.name); return [...s]; }
function owRoomGrid(name) { const l = LIB.find(x => x.name === name); if (l) return l.grid; const k = OW_ROOMS[name]; return k && SAMPLES[k] ? SAMPLES[k] : null; }
function owRoomOk(name) { const g = owRoomGrid(name); if (!g) return '找不到这个房间（关卡库里没有，也不是内置样板）'; return !check(W, g, true).playable ? '房间本身检查没通过（在关卡工坊里打开它看红格）' : null; }
function owSyncDoors(m) {
  const present = new Set(); for (const r of m.rows) for (const c of r) if (c >= '1' && c <= '9') present.add(+c);
  m.doors = m.doors.filter(d => present.has(d.n));
  for (const n of [...present].sort()) if (!m.doors.some(d => d.n === n)) { const last = m.doors.length ? Math.max(...m.doors.map(d => d.minute)) : 420; m.doors.push({ n, minute: Math.min(OW.LatestDoor, last + 150), room: '默认恶作剧房间' }); }
  m.doors.sort((a, b) => a.n - b.n);
}
function owAdd(m, fromImport) {
  if (!m.rows.length) { toast('这个文件里没有小镇网格'); return; }
  const ex = OWLIB.findIndex(x => x.name === m.name);
  if (ex >= 0 && (!fromImport || confirm(`已有同名小镇"${m.name}"，覆盖它吗？（取消 = 另存）`))) { OWLIB[ex] = m; OWCUR = ex; }
  else { if (ex >= 0) m.name += ' 2'; OWLIB.push(m); OWCUR = OWLIB.length - 1; }
  owSave(); document.querySelector('[data-page=overworld]').click(); toast(`已导入小镇"${m.name}"`);
}
function owImportPack(pk) {
  const list = (pk.overworlds || []).concat((pk.levels || []).filter(l => l.kind === 'overworld'));
  for (const d of list) { const m = owFromJson(d); const ex = OWLIB.findIndex(x => x.name === m.name); if (ex >= 0) OWLIB[ex] = m; else OWLIB.push(m); }
  if (list.length) { owSave(); toast(`导入了 ${list.length} 个小镇（在"大地图"页）`); }
}
function owSet(m, x, y, c) {
  const put = (xx, yy, cc) => { const r = m.rows.length - 1 - yy; m.rows[r] = m.rows[r].slice(0, xx) + cc + m.rows[r].slice(xx + 1); };
  if (c === 'M' || c === 'T' || (c >= '1' && c <= '9')) for (const [ox, oy] of owFind(m, c)) put(ox, oy, '.');
  put(x, y, c);
}
function owFlood(m, x, y, c) {
  const from = owAt(m, x, y); if (from === c || 'MT123456789'.includes(c)) { owSet(m, x, y, c); return; }
  const q = [[x, y]]; let g = 0; const w = owW(m), h = m.rows.length;
  while (q.length && g++ < 10000) { const [a, b] = q.shift(); if (a < 0 || b < 0 || a >= w || b >= h || owAt(m, a, b) !== from) continue; owSet(m, a, b, c); q.push([a + 1, b], [a - 1, b], [a, b + 1], [a, b - 1]); }
}
const owCss = t => `rgb(${t.rgb.map(v => Math.round(v * 255)).join(',')})`;
function owPalette() {
  $('#owPal').innerHTML = ''; $('#owDoorPal').innerHTML = '';
  for (const t of OW_TILES) {
    if (t.c === '1') { for (let n = 1; n <= 9; n++) { const b = document.createElement('button'); b.className = 'btn small'; b.textContent = n; b.style.background = owCss(t); b.setAttribute('aria-pressed', OWT.brush === String(n)); if (OWT.brush === String(n)) b.style.outline = '2px solid var(--loot)'; b.onclick = () => { OWT.brush = String(n); owPalette(); }; $('#owDoorPal').appendChild(b); } continue; }
    const b = document.createElement('button'); b.className = 'chip'; b.setAttribute('aria-pressed', OWT.brush === t.c); b.title = t.w + '\n\n' + t.p;
    b.innerHTML = `<span class="sw" style="background:${owCss(t)};color:#111">${t.c === '.' ? '' : t.c.replace('"', '&quot;')}</span><span class="nm">${t.zh}</span>`;
    b.onclick = () => { OWT.brush = t.c; owPalette(); }; $('#owPal').appendChild(b);
  }
  const cur = owTile(OWT.brush); $('#owTip').textContent = cur ? `${cur.zh}：${cur.w} ${cur.p}` : '';
  document.querySelectorAll('[data-owtool]').forEach(b => b.classList.toggle('gold', b.dataset.owtool === OWT.tool));
}
function owRender() {
  const m = owM(); owSyncDoors(m);
  const pick = $('#owPick'); pick.innerHTML = OWLIB.map((x, i) => `<option value="${i}"${i === OWCUR ? ' selected' : ''}>${(x.name || '未命名小镇').replace(/</g, '&lt;')}</option>`).join('');
  $('#owName').value = m.name; $('#owGoal').value = m.goal;
  const rooms = owRoomNames();
  $('#owDoors').innerHTML = m.doors.length ? '' : '<p class="hint">在画布上用 1–9 画门。</p>';
  for (const d of m.doors) {
    const row = document.createElement('div'); row.className = 'owdoor';
    const opts = (rooms.includes(d.room) ? rooms : rooms.concat([d.room])).map(r => `<option${r === d.room ? ' selected' : ''}>${r.replace(/</g, '&lt;')}</option>`).join('');
    row.innerHTML = `<b>门 ${d.n}</b><input value="${owClock(d.minute)}" aria-label="门 ${d.n} 时间"><select aria-label="门 ${d.n} 房间">${opts}</select>`;
    row.querySelector('input').onchange = e => { const v = owParseClock(e.target.value); if (v === null) { toast('时间格式：08:30'); e.target.value = owClock(d.minute); return; } owPush(); d.minute = v; owRender(); };
    row.querySelector('select').onchange = e => { owPush(); d.room = e.target.value; owRender(); };
    $('#owDoors').appendChild(row);
  }
  const rep = OWT.rep = owCheck(m, OW.Rules, owRoomOk);
  $('#owHead').textContent = '检查　' + rep.headline; $('#owHead').style.color = rep.playable ? 'var(--ok)' : 'var(--bad)';
  $('#owIssues').innerHTML = rep.issues.map(i => `<div class="owiss ${i.sev}">${owIssueText(i).replace(/</g, '&lt;')}</div>`).join('') || '<p class="hint">没有问题。</p>';
  const sc = rep.schedule;
  $('#owSched').innerHTML = sc ? sc.stops.map((s, k) => `门 ${s.door.n}：${owClock(s.depart)} 出发 → ${owClock(s.arrive)} 到 → ${owClock(s.leave)} 出来　你能提前 ${Math.round(owLead(sc, k, OW.Rules.minutesPerSecond))} 秒`).join('<br>') + `<br>${owClock(sc.homeArrive)} 到家` : '（先把检查里的红色问题改掉）';
  owPalette(); owDraw(); owSave();
}
function owPush() { OWT.undo.push(JSON.stringify(owToJson(owM()))); if (OWT.undo.length > 60) OWT.undo.shift(); }
function owDraw() {
  const m = owM(), z = OWT.zoom, w = owW(m), h = m.rows.length, cv = $('#owCanvas'), g = cv.getContext('2d');
  cv.width = w * z; cv.height = h * z;
  const err = new Set((OWT.rep ? OWT.rep.issues : []).filter(i => i.sev === 'Error' && i.x >= 0).map(i => i.x + ',' + i.y));
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const c = owAt(m, x, y), t = owTile(c), px = x * z, py = (h - 1 - y) * z;
    g.fillStyle = t ? owCss(t) : '#f0f'; g.fillRect(px, py, z - 1, z - 1);
    if (c === 'W' && owAt(m, x, y - 1) !== 'W') { g.fillStyle = 'rgba(0,0,0,.25)'; g.fillRect(px, py + z * 0.45, z - 1, z * 0.55); }
    if (c === 't') { g.fillStyle = 'rgba(0,0,0,.25)'; g.beginPath(); g.arc(px + z / 2, py + z / 2, z * 0.42, 0, 7); g.fill(); }
    if ('MT?ni'.includes(c) || (c >= '1' && c <= '9')) { g.fillStyle = '#111'; g.font = `700 ${Math.round(z * 0.62)}px JetBrains Mono,monospace`; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(c, px + z / 2, py + z / 2 + 1); }
    if (err.has(x + ',' + y)) { g.strokeStyle = '#ff5a4e'; g.lineWidth = 2; g.strokeRect(px + 1, py + 1, z - 3, z - 3); }
  }
  const sc = OWT.rep && OWT.rep.schedule;
  if (sc && $('#owRoute').checked) {
    g.fillStyle = 'rgba(255,60,50,.85)';
    for (const p of sc.stops.map(s => s.path).concat(sc.homePath ? [sc.homePath] : [])) for (const [x, y] of p) g.fillRect(x * z + z * 0.36, (h - 1 - y) * z + z * 0.36, z * 0.28, z * 0.28);
  }
  if ($('#owNight').checked) { // 夜晚：路灯 3 格内亮，其余暗（他晚上只看得见 3.5 格）
    const lamps = owFind(m, 'i');
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) if (!lamps.some(([lx, ly]) => (lx - x) ** 2 + (ly - y) ** 2 <= 9)) { g.fillStyle = 'rgba(10,15,50,.5)'; g.fillRect(x * z, (h - 1 - y) * z, z, z); }
  }
  for (const n of m.notes) { g.fillStyle = '#ffc83d'; g.fillText('✎', n.x * z + z / 2, (h - 1 - n.y) * z + z / 2); }
}
function owCell(e) { const cv = $('#owCanvas'), r = cv.getBoundingClientRect(), m = owM(), z = OWT.zoom; const x = Math.floor((e.clientX - r.left) / z), y = m.rows.length - 1 - Math.floor((e.clientY - r.top) / z); return x >= 0 && y >= 0 && x < owW(m) && y < m.rows.length ? [x, y] : null; }
$('#owCanvas').oncontextmenu = e => e.preventDefault();
$('#owCanvas').onmousedown = e => {
  const c = owCell(e); if (!c) return; const paint = e.button === 2 ? '.' : OWT.brush; owPush();
  if (OWT.tool === 'rect' && e.button === 0) { OWT.drag = c; return; }
  if (OWT.tool === 'fill' && e.button === 0) { owFlood(owM(), c[0], c[1], paint); owRender(); return; }
  owSet(owM(), c[0], c[1], paint); OWT.painting = paint; owRender();
};
$('#owCanvas').onmousemove = e => { if (!OWT.painting || OWT.tool !== 'brush' && OWT.painting !== '.') return; const c = owCell(e); if (c && owAt(owM(), c[0], c[1]) !== OWT.painting) { owSet(owM(), c[0], c[1], OWT.painting); owRender(); } };
window.addEventListener('mouseup', e => {
  if (OWT.drag) { const c = owCell(e) || OWT.drag, a = OWT.drag; OWT.drag = null; for (let x = Math.min(a[0], c[0]); x <= Math.max(a[0], c[0]); x++) for (let y = Math.min(a[1], c[1]); y <= Math.max(a[1], c[1]); y++) owSet(owM(), x, y, OWT.brush); owRender(); }
  OWT.painting = null;
});
document.querySelectorAll('[data-owtool]').forEach(b => b.onclick = () => { OWT.tool = b.dataset.owtool; owPalette(); });
$('#owUndo').onclick = () => { if (!OWT.undo.length) return; OWLIB[OWCUR] = owFromJson(JSON.parse(OWT.undo.pop())); owRender(); };
$('#owZoom').oninput = e => { OWT.zoom = +e.target.value; owDraw(); };
$('#owRoute').onchange = owDraw; $('#owNight').onchange = owDraw;
$('#owPick').onchange = e => { OWCUR = +e.target.value; OWT.undo = []; owRender(); };
$('#owName').onchange = e => { owM().name = e.target.value.trim() || '未命名小镇'; owRender(); };
$('#owGoal').onchange = e => { owM().goal = e.target.value.trim(); owSave(); };
$('#owNew').onclick = () => { OWLIB.push({ kind: 'overworld', name: '新小镇 ' + (OWLIB.length + 1), goal: '', id: '', rows: owNewMap(40, 24), doors: [], notes: [] }); OWCUR = OWLIB.length - 1; OWT.undo = []; owRender(); toast('新建 40×24：先画家 M、出生点 T、至少一扇门'); };
$('#owSample').onclick = () => { const s = owParse(OW_SAMPLE); const i = OWLIB.findIndex(x => x.name === s.name); if (i >= 0) { owPush(); OWLIB[i] = s; OWCUR = i; } else { OWLIB.push(s); OWCUR = OWLIB.length - 1; } owRender(); };
$('#owDel').onclick = () => { if (OWLIB.length <= 1) { toast('至少留一个小镇'); return; } if (!confirm(`删除小镇"${owM().name}"？`)) return; OWLIB.splice(OWCUR, 1); OWCUR = 0; owRender(); };
$('#owPack').onclick = exportPack;
$('#owTxt').onclick = () => download(`${(owM().name || '小镇').replace(/[\\/:*?"<>|]/g, '')}.txt`, owToText(owM()), 'text/plain');
