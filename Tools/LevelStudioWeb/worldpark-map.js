'use strict';
/**
 * S257 探索地图可视化编辑工具。
 *
 * API：validate(node) 是不改数据的候选校验；resize(node,width,height,anchor)
 * 与 paint(node,cells,tile) 都返回 {ok,node,reason,impact}；Editor.mount(host,
 * {node,onApply,onCancel,icon}) 只编辑节点克隆，确认时才把克隆交给 onApply。
 * 本模块只读取宿主的 OW_TILES 和 OW_ART，不保存浏览器数据，也不读写关卡库。
 */
const WPMap = (() => {
  const ROOT = typeof globalThis !== 'undefined' ? globalThis : {};
  const TALL = String.fromCharCode(34);
  const DEFAULT_BRUSHES = ['.', '=', 'W', 't', TALL, 'w', 'T'];
  const brushName = tile => tile === '.' ? '空地' : tile === '=' ? '道路' : tile === 'W' ? '墙' : tile === 't' ? '树' : tile === TALL ? '高草' : tile === 'w' ? '水' : tile === 'T' ? '出生' : `地形 ${tile}`;
  const esc = value => String(value == null ? '' : value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/'/g, '&#39;');

  function getSpatial() {
    if (typeof WorldParkSpatial !== 'undefined') return WorldParkSpatial;
    if (ROOT.WorldParkSpatial) return ROOT.WorldParkSpatial;
    if (typeof require === 'function') {
      try { return require('./worldpark-spatial.js'); } catch (_) { return null; }
    }
    return null;
  }

  function getWorldPark() {
    if (typeof WorldPark !== 'undefined') return WorldPark;
    return ROOT.WorldPark || null;
  }

  function tiles() {
    try { if (typeof OW_TILES !== 'undefined' && Array.isArray(OW_TILES)) return OW_TILES; } catch (_) { /* 网页资料尚未加载 */ }
    return Array.isArray(ROOT.OW_TILES) ? ROOT.OW_TILES : [];
  }

  function art() {
    try { if (typeof OW_ART !== 'undefined' && OW_ART) return OW_ART; } catch (_) { /* 网页资料尚未加载 */ }
    return ROOT.OW_ART || null;
  }

  function clone(value) {
    if (Array.isArray(value)) return value.map(clone);
    if (value && typeof value === 'object') {
      const out = {};
      for (const key of Object.keys(value)) out[key] = clone(value[key]);
      return out;
    }
    return value;
  }

  function limits() {
    const s = getSpatial();
    return { minWidth:s?.MIN_WIDTH || 5, minHeight:s?.MIN_HEIGHT || 4, maxWidth:s?.MAX_WIDTH || 192, maxHeight:s?.MAX_HEIGHT || 128 };
  }

  function fail(node, reason, impact = {}) { return { ok:false, node, reason, impact }; }
  function pass(node, impact = {}) { return { ok:true, node, reason:'', impact }; }
  function invalid(reason, impact = {}) { return { ok:false, reason:`地图无效，请先修复：${reason}`, impact }; }
  function inBounds(x, y, width, height) { return Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x < width && y < height; }
  function outer(x, y, width, height) { return x === 0 || y === 0 || x === width - 1 || y === height - 1; }

  // 保留每一条头部空行和说明原文；格子行遵循 WorldParkSpatial 相同的空格和注释规则。
  function readMapText(mapText) {
    const source = String(mapText == null ? '' : mapText);
    const eol = source.includes('\r\n') ? '\r\n' : '\n';
    const header = [], rows = [];
    let gridStarted = false;
    source.split(/\r?\n/).forEach((original, index) => {
      const logical = index === 0 ? original.replace(/^\uFEFF/, '') : original;
      const row = logical.trimEnd().replace(/ /g, '.');
      const ignored = logical.startsWith('#') || !row.length;
      if (!gridStarted && ignored) { header.push(original); return; }
      if (ignored) return;
      gridStarted = true;
      rows.push(row);
    });
    return { source, eol, header, rows };
  }

  function writeMapText(parsed, grid) {
    const body = grid.slice().reverse().map(row => row.join('')).join(parsed.eol);
    return parsed.header.length ? `${parsed.header.join(parsed.eol)}${parsed.eol}${body}` : body;
  }

  function mapInfo(node) {
    const s = getSpatial();
    if (!s?.mapShape || !s?.canPlace) return { ok:false, reason:'地图空间校验模块未加载，不能安全编辑。' };
    if (!node || typeof node !== 'object' || Array.isArray(node)) return { ok:false, reason:'节点必须是对象。' };
    if (typeof node.mapText !== 'string') return { ok:false, reason:'节点缺少 mapText 地图文字。' };
    if (!Array.isArray(node.points)) return { ok:false, reason:'节点的 points 必须是数组。' };
    return { ok:true, s, shape:s.mapShape(node.mapText), parsed:readMapText(node.mapText) };
  }

  function knownTile(tile, s = getSpatial()) {
    if (typeof tile !== 'string' || [...tile].length !== 1) return false;
    if (tiles().some(item => item?.c === tile)) return true;
    if (!s?.mapShape) return false;
    const one = s.mapShape(tile);
    return one.width === 1 && one.height === 1 && one.isKnown(0, 0);
  }

  function solidTile(tile, s = getSpatial()) {
    if (!knownTile(tile, s) || !s?.mapShape) return false;
    return !s.mapShape(tile).isWalkable(0, 0);
  }

  function routeCell(raw) {
    const wp = getWorldPark();
    if (wp?.chaserCell) {
      const found = wp.chaserCell(raw);
      if (found && Number.isInteger(found.x) && Number.isInteger(found.y)) return { x:found.x, y:found.y };
    }
    if (typeof raw !== 'string') return null;
    const found = raw.match(/^\s*(-?\d+)\s*,\s*(-?\d+)\s*$/);
    return found ? { x:Number(found[1]), y:Number(found[2]) } : null;
  }

  function routeText(cell) { return `${cell.x},${cell.y}`; }

  /** 纯校验候选节点；已有问题必须先修，避免编辑器把坏图悄悄搬动或删掉。 */
  function validate(node) {
    const info = mapInfo(node);
    if (!info.ok) return invalid(info.reason);
    const { s, shape } = info, lim = limits();
    const impact = { width:shape.width, height:shape.height, points:node.points.length, routes:0 };
    if (!shape.rectangular) return invalid('每一行必须同宽。', impact);
    if (shape.width < lim.minWidth || shape.height < lim.minHeight || shape.width > lim.maxWidth || shape.height > lim.maxHeight) return invalid(`尺寸须在 ${lim.minWidth}×${lim.minHeight} 至 ${lim.maxWidth}×${lim.maxHeight} 格内；当前为 ${shape.width}×${shape.height}。`, impact);
    for (let y = 0; y < shape.height; y++) for (let x = 0; x < shape.width; x++) if (!shape.isKnown(x, y)) return invalid(`(${x}, ${y}) 有未知地形字符。`, impact);
    for (let x = 0; x < shape.width; x++) if (shape.isWalkable(x, 0) || shape.isWalkable(x, shape.height - 1)) return invalid('外圈必须全部是挡路格，先补好边界。', impact);
    for (let y = 0; y < shape.height; y++) if (shape.isWalkable(0, y) || shape.isWalkable(shape.width - 1, y)) return invalid('外圈必须全部是挡路格，先补好边界。', impact);
    const spawns = [];
    for (let y = 0; y < shape.height; y++) for (let x = 0; x < shape.width; x++) if (shape.at(x, y) === 'T') spawns.push({ x, y });
    if (spawns.length !== 1) return invalid(`需要且只能有一个 T 出生点；当前为 ${spawns.length} 个。`, impact);
    const spawnCheck = s.canPlace(node.mapText, spawns[0].x, spawns[0].y);
    if (!spawnCheck.ok) return invalid(`出生点 T 不能使用：${spawnCheck.reason}`, impact);
    impact.spawn = clone(spawns[0]);
    for (let i = 0; i < node.points.length; i++) {
      const point = node.points[i];
      if (!point || typeof point !== 'object' || Array.isArray(point)) return invalid(`第 ${i + 1} 个探索点不是对象。`, impact);
      if (!Number.isInteger(point.x) || !Number.isInteger(point.y)) return invalid(`第 ${i + 1} 个探索点坐标必须是整数。`, impact);
      const pointCheck = s.canPlace(node.mapText, point.x, point.y);
      if (!pointCheck.ok) return invalid(`第 ${i + 1} 个探索点不能使用：${pointCheck.reason}`, impact);
      if (point.route !== undefined && !Array.isArray(point.route)) return invalid(`第 ${i + 1} 个探索点的巡逻 route 必须是数组。`, impact);
      for (let r = 0; r < (point.route || []).length; r++) {
        const cell = routeCell(point.route[r]);
        impact.routes++;
        if (!cell) return invalid(`第 ${i + 1} 个探索点的第 ${r + 1} 个巡逻格必须写成 x,y。`, impact);
        const routeCheck = s.canPlace(node.mapText, cell.x, cell.y);
        if (!routeCheck.ok) return invalid(`第 ${i + 1} 个探索点的第 ${r + 1} 个巡逻格不能使用：${routeCheck.reason}`, impact);
      }
    }
    impact.message = `地图 ${shape.width}×${shape.height} 有效：1 个出生点、${node.points.length} 个标记、${impact.routes} 个巡逻格。`;
    return { ok:true, reason:'', impact };
  }

  function criticalCells(node, spawn) {
    const all = [{ label:'出生点 T', x:spawn.x, y:spawn.y }];
    node.points.forEach((point, pointIndex) => {
      all.push({ label:`第 ${pointIndex + 1} 个探索点`, x:point.x, y:point.y });
      (point.route || []).forEach((raw, routeIndex) => {
        const cell = routeCell(raw);
        if (cell) all.push({ label:`第 ${pointIndex + 1} 个探索点的第 ${routeIndex + 1} 个巡逻格`, x:cell.x, y:cell.y });
      });
    });
    return all;
  }

  function shiftPoints(candidate, dx, dy) {
    if (!dx && !dy) return;
    candidate.points.forEach(point => {
      point.x += dx;
      point.y += dy;
      if (Array.isArray(point.route)) point.route = point.route.map(raw => {
        const cell = routeCell(raw);
        return cell ? routeText({ x:cell.x + dx, y:cell.y + dy }) : raw;
      });
    });
  }

  /**
   * 改尺寸但不修改输入。bottom-left 保持左下坐标，center 使用 floor 的居中平移。
   * 扩图会只打开原来已变成内层的旧边界挡路格，再以 t 围住新外圈。
   */
  function resize(node, width, height, anchor = 'bottom-left') {
    const source = validate(node);
    if (!source.ok) return fail(node, source.reason, source.impact);
    if (!Number.isInteger(width) || !Number.isInteger(height)) return fail(node, '宽度和高度必须是整数。', source.impact);
    const lim = limits();
    if (width < lim.minWidth || height < lim.minHeight || width > lim.maxWidth || height > lim.maxHeight) return fail(node, `尺寸须在 ${lim.minWidth}×${lim.minHeight} 至 ${lim.maxWidth}×${lim.maxHeight} 格内。`, source.impact);
    if (anchor !== 'bottom-left' && anchor !== 'center') return fail(node, '锚点只能是 bottom-left 或 center。', source.impact);
    const info = mapInfo(node), oldWidth = info.shape.width, oldHeight = info.shape.height;
    if (width === oldWidth && height === oldHeight) return pass(clone(node), { before:{ width:oldWidth, height:oldHeight }, after:{ width, height }, anchor, moved:{ x:0, y:0 }, opened:0, message:'尺寸没有变化，草稿保持不动。' });
    const dx = anchor === 'center' ? Math.floor((width - oldWidth) / 2) : 0;
    const dy = anchor === 'center' ? Math.floor((height - oldHeight) / 2) : 0;
    const shrinking = width < oldWidth || height < oldHeight;
    for (const item of criticalCells(node, source.impact.spawn)) {
      const x = item.x + dx, y = item.y + dy;
      if (!inBounds(x, y, width, height)) return fail(node, `${shrinking ? '缩小' : '调整'}会裁掉${item.label}，已拒绝且原图未动。`, source.impact);
      if (shrinking && outer(x, y, width, height)) return fail(node, `缩小后的新外圈会挡住${item.label}，已拒绝且原图未动。`, source.impact);
    }
    const grid = Array.from({ length:height }, () => Array(width).fill('.'));
    for (let y = 0; y < oldHeight; y++) for (let x = 0; x < oldWidth; x++) {
      const nx = x + dx, ny = y + dy;
      if (inBounds(nx, ny, width, height)) grid[ny][nx] = info.shape.at(x, y);
    }
    const expanded = { left:dx > 0, right:width > oldWidth + dx, bottom:dy > 0, top:height > oldHeight + dy };
    let opened = 0;
    for (let y = 0; y < oldHeight; y++) for (let x = 0; x < oldWidth; x++) {
      const oldEdge = (expanded.left && x === 0) || (expanded.right && x === oldWidth - 1) || (expanded.bottom && y === 0) || (expanded.top && y === oldHeight - 1);
      const nx = x + dx, ny = y + dy;
      if (oldEdge && inBounds(nx, ny, width, height) && !outer(nx, ny, width, height) && !info.shape.isWalkable(x, y)) { grid[ny][nx] = '.'; opened++; }
    }
    for (let x = 0; x < width; x++) { grid[0][x] = 't'; grid[height - 1][x] = 't'; }
    for (let y = 0; y < height; y++) { grid[y][0] = 't'; grid[y][width - 1] = 't'; }
    const candidate = clone(node);
    shiftPoints(candidate, dx, dy);
    candidate.mapText = writeMapText(info.parsed, grid);
    const checked = validate(candidate);
    if (!checked.ok) return fail(node, `调整后的候选地图无效：${checked.reason}；原图未动。`, source.impact);
    return pass(candidate, { before:{ width:oldWidth, height:oldHeight }, after:{ width, height }, anchor, moved:{ x:dx, y:dy }, opened, message:`已调整为 ${width}×${height}；${opened ? `打开了 ${opened} 格旧边界。` : '新外圈已围好。'}` });
  }

  function normalCells(cells, width, height) {
    if (!Array.isArray(cells) || !cells.length) return { ok:false, reason:'一次笔划至少要有一格。' };
    const seen = new Set(), result = [];
    for (const raw of cells) {
      const x = Array.isArray(raw) ? raw[0] : raw?.x, y = Array.isArray(raw) ? raw[1] : raw?.y;
      if (!inBounds(x, y, width, height)) return { ok:false, reason:`(${x}, ${y}) 超出地图范围。` };
      const key = `${x},${y}`;
      if (!seen.has(key)) { seen.add(key); result.push({ x, y }); }
    }
    return { ok:true, cells:result };
  }

  /**
   * 原子化涂绘。出生笔刷只允许一个目标，会把旧 T 变成空地；所有点位和巡逻格必须仍可达。
   */
  function paint(node, cells, tile) {
    const source = validate(node);
    if (!source.ok) return fail(node, source.reason, source.impact);
    const info = mapInfo(node);
    if (!knownTile(tile, info.s)) return fail(node, '笔刷必须是 OW_TILES 或现有地图认识的单个地形字符。', source.impact);
    const selected = normalCells(cells, info.shape.width, info.shape.height);
    if (!selected.ok) return fail(node, selected.reason, source.impact);
    if (tile === 'T' && selected.cells.length !== 1) return fail(node, '出生笔刷一次只能指定一个目标格，避免产生多个 T。', source.impact);
    for (const cell of selected.cells) {
      if (outer(cell.x, cell.y, info.shape.width, info.shape.height) && !solidTile(tile, info.s)) return fail(node, '外圈必须保持挡路，不能在边界画可通行格。', source.impact);
      if (tile !== 'T' && info.shape.at(cell.x, cell.y) === 'T') return fail(node, '出生 T 只能用出生笔刷移动，不能直接覆盖。', source.impact);
    }
    const grid = Array.from({ length:info.shape.height }, (_, y) => Array.from({ length:info.shape.width }, (_, x) => info.shape.at(x, y)));
    const oldSpawn = source.impact.spawn;
    if (tile === 'T') {
      const target = selected.cells[0];
      if (target.x !== oldSpawn.x || target.y !== oldSpawn.y) grid[oldSpawn.y][oldSpawn.x] = '.';
    }
    selected.cells.forEach(cell => { grid[cell.y][cell.x] = tile; });
    const candidate = clone(node);
    candidate.mapText = writeMapText(info.parsed, grid);
    const checked = validate(candidate);
    if (!checked.ok) return fail(node, `这笔涂绘会使地图无效：${checked.reason}；整笔没有写入。`, source.impact);
    return pass(candidate, { changed:selected.cells.length + (tile === 'T' ? 1 : 0), tile, tileName:brushName(tile), spawnMoved:tile === 'T' && (selected.cells[0].x !== oldSpawn.x || selected.cells[0].y !== oldSpawn.y), message:`已用${brushName(tile)}涂绘 ${selected.cells.length} 格。` });
  }

  function lineCells(from, to) {
    const cells = [];
    let x = from.x, y = from.y, dx = Math.abs(to.x - x), sx = x < to.x ? 1 : -1, dy = -Math.abs(to.y - y), sy = y < to.y ? 1 : -1, error = dx + dy;
    while (true) {
      cells.push({ x, y });
      if (x === to.x && y === to.y) break;
      const twice = 2 * error;
      if (twice >= dy) { error += dy; x += sx; }
      if (twice <= dx) { error += dx; y += sy; }
    }
    return cells;
  }

  const CSS = `
.wp-map-editor{color:#efe7d6;background:#191724;border:1px solid #45405d;border-radius:10px;padding:10px;font:14px system-ui,sans-serif}
.wp-map-editor *{box-sizing:border-box}.wp-map-editor button,.wp-map-editor input,.wp-map-editor select{font:inherit}
.wp-map-editor button{border:1px solid #625d78;background:#302c40;color:#efe7d6;border-radius:5px;padding:5px 8px;cursor:pointer}.wp-map-editor button[aria-pressed='true']{background:#5b4b7d;border-color:#b89df0}.wp-map-editor button:disabled{opacity:.55;cursor:not-allowed}
.wp-map-editor__bar{display:flex;flex-wrap:wrap;gap:6px;align-items:center;margin-bottom:8px}.wp-map-editor__bar label{display:flex;gap:4px;align-items:center}.wp-map-editor__bar input{width:64px}.wp-map-editor__bar select{max-width:150px}.wp-map-editor__spacer{flex:1}
.wp-map-editor__view{max-height:520px;overflow:auto;border:1px solid #45405d;background:#100f18;touch-action:none}.wp-map-editor__canvas{display:block;image-rendering:pixelated;touch-action:none;cursor:crosshair}
.wp-map-editor__status{display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap;margin-top:8px;color:#cfc6df}.wp-map-editor__impact{color:#8ad7bd}.wp-map-editor__error{color:#ff9c93}.wp-map-editor__tip{font-size:12px;color:#aaa2bb}
.wp-map-editor input,.wp-map-editor select{background:#191525;color:#efe7d6;border:1px solid #625d78;border-radius:5px;padding:5px}.wp-map-editor button{display:inline-flex;align-items:center;gap:5px}.wp-map-editor .wp-map-editor__swatch{display:inline-block;width:20px;height:20px;margin:0;border-radius:3px;box-shadow:none;cursor:inherit}
`;

  function rgb(def, fallback) {
    const value = def?.rgb;
    return Array.isArray(value) && value.length >= 3 ? `rgb(${value.slice(0, 3).map(v => Math.max(0, Math.min(255, Math.round(Number(v) * 255)))).join(',')})` : fallback;
  }

  function tileDef(tile) { return tiles().find(item => item?.c === tile) || null; }

  function drawArt(ctx, tile, px, py, size) {
    const source = art(), key = source?.tile?.[tile], rows = key && (source.icons?.[key] || source.town?.[key]);
    if (!rows || !source?.pal || !rows.length) return false;
    const h = rows.length, w = Math.max(...rows.map(row => row.length));
    for (let y = 0; y < h; y++) for (let x = 0; x < rows[y].length; x++) {
      const color = source.pal[rows[y][x]];
      if (!color || rows[y][x] === '.') continue;
      ctx.fillStyle = `rgb(${color.map(v => Math.round(v * 255)).join(',')})`;
      ctx.fillRect(px + Math.floor(x * size / w), py + Math.floor(y * size / h), Math.max(1, Math.ceil(size / w)), Math.max(1, Math.ceil(size / h)));
    }
    return true;
  }

  // 没有 OW_ART 时仍画可辨识块形；绝不把地形字母当默认地图画面。
  function drawFallback(ctx, tile, px, py, size) {
    const base = rgb(tileDef(tile), '#59636e');
    ctx.fillStyle = base; ctx.fillRect(px, py, size, size);
    if (tile === '.') { ctx.fillStyle = '#70a85a'; for (let i = 3; i < size; i += 7) ctx.fillRect(px + i, py + ((i * 3) % Math.max(4, size - 3)), 2, 2); }
    else if (tile === '=') { ctx.fillStyle = '#d2bc83'; for (let i = 3; i < size; i += 7) { ctx.fillRect(px + i, py + 3, 3, 2); ctx.fillRect(px + i - 2, py + size - 5, 3, 2); } }
    else if (tile === 'W') { ctx.fillStyle = '#765044'; ctx.fillRect(px + 2, py + Math.floor(size * .42), size - 4, size - Math.floor(size * .42) - 2); ctx.fillStyle = '#b85a4b'; ctx.fillRect(px + 1, py + 3, size - 2, Math.floor(size * .4)); }
    else if (tile === 't') { ctx.fillStyle = '#69492a'; ctx.fillRect(px + Math.floor(size * .43), py + Math.floor(size * .55), Math.max(2, Math.floor(size * .16)), Math.floor(size * .35)); ctx.fillStyle = '#245f39'; ctx.beginPath(); ctx.arc(px + size / 2, py + size * .38, size * .3, 0, Math.PI * 2); ctx.fill(); }
    else if (tile === 'w') { ctx.fillStyle = '#d7efff'; for (let y = 4; y < size; y += 7) ctx.fillRect(px + 2, py + y, size - 4, 2); }
    else if (tile === TALL) { ctx.fillStyle = '#234f33'; for (let x = 3; x < size; x += 5) ctx.fillRect(px + x, py + size * .26, 2, size * .58); }
    else if (tile === 'T') { ctx.fillStyle = '#4f8cff'; ctx.fillRect(px + size * .32, py + size * .24, size * .36, size * .58); ctx.fillStyle = '#ffffff'; ctx.fillRect(px + size * .43, py + size * .1, size * .14, size * .52); ctx.fillRect(px + size * .32, py + size * .22, size * .36, size * .13); }
    else { ctx.fillStyle = 'rgba(255,255,255,.2)'; ctx.fillRect(px + 3, py + 3, size - 6, 3); ctx.fillRect(px + 3, py + 3, 3, size - 6); }
  }

  class Editor {
    constructor() {
      this.zoom = 26; this.brush = '.'; this.rectangle = false; this.draft = null; this.drag = null;
      this.host = null; this.root = null; this.canvas = null; this.options = null; this.message = ''; this.impact = null; this.applyState = 'idle'; this.cancelled = false; this.undoDrafts = []; this.redoDrafts = []; this.anchorValue = 'bottom-left';
    }

    iconText(key, fallback) {
      const icon = this.options?.icon;
      const value = typeof icon === 'function' ? icon(key, fallback) : icon && typeof icon === 'object' ? icon[key] : null;
      return typeof value === 'string' ? value : fallback;
    }

    mount(host, options) {
      if (!host || typeof host.appendChild !== 'function') throw Error('WPMap.Editor.mount 需要一个宿主元素。');
      this.host = host; this.options = options || {}; this.draft = clone(this.options.node || {}); this.drag = null; this.message = '这是临时草稿；点应用前不会改当前阶段。'; this.impact = null; this.applyState = 'idle'; this.cancelled = false; this.undoDrafts = []; this.redoDrafts = []; this.anchorValue = 'bottom-left';
      this.render();
      return this;
    }

    shape() { const s = getSpatial(); return s?.mapShape ? s.mapShape(this.draft?.mapText || '') : { width:0, height:0, at:() => '' }; }
    extraTiles() { return tiles().filter(item => item?.c && !DEFAULT_BRUSHES.includes(item.c) && knownTile(item.c)).slice(0, 80); }

    render() {
      const shape = this.shape(), lim = limits(), width = Math.max(1, Math.min(lim.maxWidth, shape.width || 1)), height = Math.max(1, Math.min(lim.maxHeight, shape.height || 1));
      const defaults = DEFAULT_BRUSHES.map(tile => `<button type='button' data-wpm-brush='${esc(tile)}' aria-pressed='${this.brush === tile}' aria-label='笔刷 ${esc(brushName(tile))}'><canvas class='wp-map-editor__swatch' data-wpm-swatch='${esc(tile)}' width='24' height='24' aria-hidden='true'></canvas>${esc(brushName(tile))}</button>`).join('');
      const extras = this.extraTiles().map(item => `<option value='${esc(item.c)}'${this.brush === item.c ? ' selected' : ''}>${esc(item.zh || item.k || item.c)} ${esc(item.c)}</option>`).join('');
      this.host.innerHTML = `<style>${CSS}</style><section class='wp-map-editor' aria-label='探索地图编辑器'>
<div class='wp-map-editor__bar' role='group' aria-label='地形笔刷'>${defaults}<label>其它地形<select data-wpm-extra aria-label='其它已知地形'><option value=''>选择其它地形</option>${extras}</select></label><button type='button' data-wpm-rectangle aria-pressed='${this.rectangle}' aria-label='矩形拖绘'>矩形拖绘</button></div>
<div class='wp-map-editor__bar' role='group' aria-label='地图尺寸'><label>宽 <input type='number' min='${lim.minWidth}' max='${lim.maxWidth}' value='${width}' data-wpm-width aria-label='地图宽度'></label><label>高 <input type='number' min='${lim.minHeight}' max='${lim.maxHeight}' value='${height}' data-wpm-height aria-label='地图高度'></label><label>锚点 <select data-wpm-anchor aria-label='调整尺寸锚点'><option value='bottom-left' ${this.anchorValue === 'bottom-left' ? 'selected' : ''}>左下不动</option><option value='center' ${this.anchorValue === 'center' ? 'selected' : ''}>居中平移</option></select></label><button type='button' data-wpm-resize>按输入调整</button><button type='button' data-wpm-grow-w>扩宽</button><button type='button' data-wpm-grow-h>扩高</button><span class='wp-map-editor__spacer'></span><button type='button' data-wpm-zoom-out aria-label='缩小地图'>缩小</button><span data-wpm-zoom>${this.zoom}px 格</span><button type='button' data-wpm-undo ${this.undoDrafts.length ? '' : 'disabled'}>↶ 撤销画图</button><button type='button' data-wpm-redo ${this.redoDrafts.length ? '' : 'disabled'}>↷ 重做</button><button type='button' data-wpm-zoom-in aria-label='放大地图'>放大</button></div>
<div class='wp-map-editor__view' aria-label='可滚动地图画布'><canvas class='wp-map-editor__canvas' data-wpm-canvas tabindex='0' aria-label='探索地图，坐标原点在左下角'></canvas></div>
<div class='wp-map-editor__status' aria-live='polite'><span data-wpm-status></span><span class='wp-map-editor__impact' data-wpm-impact></span></div>
<div class='wp-map-editor__bar'><span class='wp-map-editor__tip'>拖动涂绘；矩形模式用拖拽框选。调查、门和鬼标记会随草稿显示。</span><span class='wp-map-editor__spacer'></span><button type='button' data-wpm-cancel>取消</button><button type='button' data-wpm-apply ${this.applyState !== 'idle' ? 'disabled' : ''}>${esc(this.iconText('apply', '应用回当前阶段'))}</button></div>
</section>`;
      this.root = this.host.querySelector('.wp-map-editor'); this.canvas = this.host.querySelector('[data-wpm-canvas]');
      this.bind(); this.root.querySelectorAll('[data-wpm-swatch]').forEach(c => { const g=c.getContext('2d');g.imageSmoothingEnabled=false;g.fillStyle='#364537';g.fillRect(0,0,24,24);if(!drawArt(g,c.dataset.wpmSwatch,0,0,24))drawFallback(g,c.dataset.wpmSwatch,0,0,24); }); this.draw(); this.chrome();
    }

    bind() {
      this.root.querySelectorAll('[data-wpm-brush]').forEach(button => button.onclick = () => { this.brush = button.dataset.wpmBrush; this.message = `已选择${brushName(this.brush)}笔刷。`; this.chrome(); });
      const extra = this.root.querySelector('[data-wpm-extra]'); extra.onchange = () => { if (extra.value) { this.brush = extra.value; this.message = `已选择${brushName(this.brush)}笔刷。`; this.chrome(); } };
      this.root.querySelector('[data-wpm-undo]').onclick = () => this.undoDraft();
      this.root.querySelector('[data-wpm-redo]').onclick = () => this.redoDraft();
      this.root.querySelector('[data-wpm-rectangle]').onclick = () => { this.rectangle = !this.rectangle; this.message = this.rectangle ? '矩形拖绘已开启。' : '自由拖绘已开启。'; this.chrome(); };
      this.root.querySelector('[data-wpm-resize]').onclick = () => this.resizeFromInputs();
      this.root.querySelector('[data-wpm-grow-w]').onclick = () => this.resizeDraft(this.shape().width + 1, this.shape().height, this.anchor());
      this.root.querySelector('[data-wpm-grow-h]').onclick = () => this.resizeDraft(this.shape().width, this.shape().height + 1, this.anchor());
      this.root.querySelector('[data-wpm-zoom-out]').onclick = () => { this.zoom = Math.max(12, this.zoom - 2); this.draw(); this.chrome(); };
      this.root.querySelector('[data-wpm-zoom-in]').onclick = () => { this.zoom = Math.min(52, this.zoom + 2); this.draw(); this.chrome(); };
      this.root.querySelector('[data-wpm-apply]').onclick = () => this.apply();
      this.root.querySelector('[data-wpm-cancel]').onclick = () => this.cancel();
      this.canvas.addEventListener('pointerdown', event => this.pointerDown(event));
      this.canvas.addEventListener('pointermove', event => this.pointerMove(event));
      this.canvas.addEventListener('pointerup', event => this.pointerUp(event));
      this.canvas.addEventListener('pointercancel', event => this.pointerCancel(event));
      this.canvas.addEventListener('contextmenu', event => event.preventDefault());
    }

    anchor() { return this.root.querySelector('[data-wpm-anchor]').value; }
    resizeFromInputs() { this.resizeDraft(Number(this.root.querySelector('[data-wpm-width]').value), Number(this.root.querySelector('[data-wpm-height]').value), this.anchor()); }
    resizeDraft(width, height, anchor) { const result = resize(this.draft, width, height, anchor); if (result.ok) { this.recordDraft(); this.anchorValue = anchor; this.draft = result.node; this.impact = result.impact; this.message = result.impact.message; this.render(); } else { this.message = result.reason; this.impact = null; this.chrome(); } }
    paintDraft(cells) { const result = paint(this.draft, cells, this.brush); if (result.ok) { this.recordDraft(); this.draft = result.node; this.impact = result.impact; this.message = result.impact.message; this.render(); } else { this.message = result.reason; this.impact = null; this.chrome(); } }

    recordDraft() { this.undoDrafts.push(clone(this.draft));if(this.undoDrafts.length>50)this.undoDrafts.shift();this.redoDrafts=[]; }
    undoDraft() { if(!this.undoDrafts.length)return;this.redoDrafts.push(clone(this.draft));this.draft=this.undoDrafts.pop();this.message='已撤销这次地图编辑；尚未应用到世界。';this.impact=null;this.render(); }
    redoDraft() { if(!this.redoDrafts.length)return;this.undoDrafts.push(clone(this.draft));this.draft=this.redoDrafts.pop();this.message='已重做这次地图编辑；尚未应用到世界。';this.impact=null;this.render(); }

    cellAt(event) {
      const shape = this.shape(), box = this.canvas.getBoundingClientRect();
      if (!box.width || !box.height) return null;
      const x = Math.floor((event.clientX - box.left) * shape.width / box.width), topY = Math.floor((event.clientY - box.top) * shape.height / box.height), y = shape.height - 1 - topY;
      return inBounds(x, y, shape.width, shape.height) ? { x, y } : null;
    }

    pointerDown(event) {
      if (event.button !== 0) return;
      const cell = this.cellAt(event); if (!cell) return;
      event.preventDefault(); this.canvas.setPointerCapture?.(event.pointerId); this.drag = { pointerId:event.pointerId, start:cell, last:cell, cells:new Map([[`${cell.x},${cell.y}`, cell]]) }; this.draw();
    }
    pointerMove(event) {
      if (!this.drag || this.drag.pointerId !== event.pointerId) return;
      const cell = this.cellAt(event); if (!cell) return;
      if (this.rectangle) this.drag.last = cell;
      else lineCells(this.drag.last, cell).forEach(next => this.drag.cells.set(`${next.x},${next.y}`, next));
      this.drag.last = cell; this.draw();
    }
    pointerUp(event) {
      if (!this.drag || this.drag.pointerId !== event.pointerId) return;
      this.canvas.releasePointerCapture?.(event.pointerId);
      const drag = this.drag; this.drag = null;
      let cells = [...drag.cells.values()];
      if (this.rectangle) { cells = []; for (let y = Math.min(drag.start.y, drag.last.y); y <= Math.max(drag.start.y, drag.last.y); y++) for (let x = Math.min(drag.start.x, drag.last.x); x <= Math.max(drag.start.x, drag.last.x); x++) cells.push({ x, y }); }
      this.paintDraft(cells);
    }
    pointerCancel(event) { if (this.drag && this.drag.pointerId === event.pointerId) { this.canvas.releasePointerCapture?.(event.pointerId); this.drag = null; this.message = '已取消这次拖绘，草稿没有变化。'; this.draw(); this.chrome(); } }

    draw() {
      if (!this.canvas) return;
      const shape = this.shape(), width = Math.max(1, shape.width || 1), height = Math.max(1, shape.height || 1), cell = this.zoom;
      this.canvas.width = width * cell; this.canvas.height = height * cell; this.canvas.style.width = `${width * cell}px`; this.canvas.style.height = `${height * cell}px`;
      const ctx = this.canvas.getContext('2d'); ctx.imageSmoothingEnabled = false; ctx.fillStyle = '#100f18'; ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
      for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const px = x * cell, py = (height - 1 - y) * cell, tile = shape.at(x, y) || '.';
        if (!drawArt(ctx, tile, px, py, cell)) drawFallback(ctx, tile, px, py, cell);
        ctx.strokeStyle = 'rgba(255,255,255,.12)'; ctx.strokeRect(px + .5, py + .5, cell - 1, cell - 1);
      }
      this.drawMarkers(ctx, shape, cell);
      if (this.drag) {
        let preview = [...this.drag.cells.values()];
        if (this.rectangle) { preview = []; for (let y = Math.min(this.drag.start.y, this.drag.last.y); y <= Math.max(this.drag.start.y, this.drag.last.y); y++) for (let x = Math.min(this.drag.start.x, this.drag.last.x); x <= Math.max(this.drag.start.x, this.drag.last.x); x++) preview.push({ x, y }); }
        ctx.fillStyle = 'rgba(255,255,255,.27)'; preview.forEach(cell0 => ctx.fillRect(cell0.x * cell + 2, (height - 1 - cell0.y) * cell + 2, cell - 4, cell - 4));
      }
    }

    drawMarkers(ctx, shape, cell) {
      const points = Array.isArray(this.draft?.points) ? this.draft.points : [];
      points.forEach(point => { if (point?.kind === 'chaser') { const cells = (point.route || []).map(routeCell).filter(Boolean); ctx.strokeStyle = 'rgba(255,93,103,.8)'; ctx.lineWidth = Math.max(1, cell * .09); ctx.beginPath(); cells.forEach((route, index) => { const x = (route.x + .5) * cell, y = (shape.height - route.y - .5) * cell; if (!index) ctx.moveTo(x, y); else ctx.lineTo(x, y); }); ctx.stroke(); cells.forEach(route => { ctx.fillStyle = '#ff5d67'; ctx.fillRect((route.x + .5) * cell - 3, (shape.height - route.y - .5) * cell - 3, 6, 6); }); } });
      points.forEach((point, index) => {
        if (!point || !inBounds(point.x, point.y, shape.width, shape.height)) return;
        const x = (point.x + .5) * cell, y = (shape.height - point.y - .5) * cell, radius = Math.max(5, cell * .23);
        if (point.kind === 'chaser') { ctx.fillStyle = '#ff5964'; ctx.beginPath(); ctx.arc(x, y, radius, Math.PI, 0); ctx.lineTo(x + radius, y + radius); ctx.lineTo(x - radius, y + radius); ctx.closePath(); ctx.fill(); ctx.fillStyle = '#281824'; ctx.fillRect(x - radius * .42, y + radius * .15, radius * .2, radius * .2); ctx.fillRect(x + radius * .22, y + radius * .15, radius * .2, radius * .2); }
        else if (point.kind === 'exit') { ctx.fillStyle = '#ffc84b'; ctx.fillRect(x - radius * .55, y - radius * .55, radius * 1.1, radius * 1.1); ctx.fillStyle = '#704d20'; ctx.fillRect(x - radius * .18, y - radius * .18, radius * .36, radius * .73); }
        else { ctx.fillStyle = '#76dcc1'; ctx.beginPath(); ctx.arc(x - radius * .15, y - radius * .12, radius * .48, 0, Math.PI * 2); ctx.fill(); ctx.strokeStyle = '#163c39'; ctx.lineWidth = Math.max(2, radius * .22); ctx.beginPath(); ctx.moveTo(x + radius * .2, y + radius * .2); ctx.lineTo(x + radius * .63, y + radius * .63); ctx.stroke(); }
        ctx.strokeStyle = '#ffffff'; ctx.lineWidth = 1.5; ctx.strokeRect(x - radius, y - radius, radius * 2, radius * 2);
        if (cell >= 18) { ctx.fillStyle = '#ffffff'; ctx.font = '11px system-ui,sans-serif'; ctx.textAlign = 'left'; ctx.textBaseline = 'bottom'; const label=String(point.label || point.id || `标记${index + 1}`).slice(0,18),tw=ctx.measureText(label).width;const tx=Math.max(2,Math.min(x+radius,this.canvas.width-tw-3)),ty=Math.max(13,y-radius);ctx.fillStyle='rgba(15,15,24,.72)';ctx.fillRect(tx-2,ty-12,tw+4,14);ctx.fillStyle='#ffffff';ctx.fillText(label,tx,ty); }
      });
    }

    chrome() {
      if (!this.root) return;
      this.root.querySelectorAll('[data-wpm-brush]').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.wpmBrush === this.brush)));
      const rectangle = this.root.querySelector('[data-wpm-rectangle]'); if (rectangle) rectangle.setAttribute('aria-pressed', String(this.rectangle));
      const zoom = this.root.querySelector('[data-wpm-zoom]'); if (zoom) zoom.textContent = `${this.zoom}px 格`;
      const status = this.root.querySelector('[data-wpm-status]'), impact = this.root.querySelector('[data-wpm-impact]'), verdict = validate(this.draft);
      if (status) { status.textContent = this.message || verdict.reason || verdict.impact.message || '等待编辑。'; status.className = verdict.ok ? '' : 'wp-map-editor__error'; }
      if (impact) impact.textContent = this.impact?.message || (verdict.ok ? `${verdict.impact.width}×${verdict.impact.height}` : '当前草稿尚不可应用');
      const apply = this.root.querySelector('[data-wpm-apply]'); if (apply) apply.disabled = this.applyState !== 'idle';
    }

    finishApply(value) {
      if (value === false || value?.ok === false) { this.applyState = 'idle'; this.message = value?.reason || '当前阶段没有接受草稿；可以继续画。'; this.chrome(); return; }
      this.applyState = 'done'; this.message = '草稿已交给当前阶段应用。'; this.chrome(); const button = this.root.querySelector('[data-wpm-apply]'); if (button) button.disabled = true;
    }
    apply() {
      if (this.applyState !== 'idle') return;
      const verdict = validate(this.draft);
      if (!verdict.ok) { this.message = verdict.reason; this.chrome(); return; }
      if (typeof this.options?.onApply !== 'function') { this.message = '没有应用回调，草稿仍未写入。'; this.chrome(); return; }
      this.applyState = 'busy'; this.chrome();
      try {
        const result = this.options.onApply(clone(this.draft));
        if (result && typeof result.then === 'function') result.then(value => this.finishApply(value)).catch(error => this.finishApply({ ok:false, reason:`应用失败：${error?.message || error}` }));
        else this.finishApply(result);
      } catch (error) { this.finishApply({ ok:false, reason:`应用失败：${error?.message || error}` }); }
    }
    cancel() {
      if (this.cancelled) return;
      this.cancelled = true; this.drag = null; this.applyState = 'cancelled';
      try { this.options?.onCancel?.(); this.message = '已取消，当前阶段没有被改动。'; } catch (error) { this.message = `取消回调失败：${error?.message || error}`; }
      this.draw(); this.chrome();
    }
  }

  return Object.freeze({ MIN_WIDTH:limits().minWidth, MIN_HEIGHT:limits().minHeight, MAX_WIDTH:limits().maxWidth, MAX_HEIGHT:limits().maxHeight, validate, resize, paint, Editor });
})();
if (typeof globalThis !== 'undefined') globalThis.WPMap = WPMap;
if (typeof module !== 'undefined') module.exports = WPMap;
