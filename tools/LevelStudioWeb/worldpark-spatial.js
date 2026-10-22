'use strict';
// S250：世界工坊探索点位画布。只操作宿主交给它的同一份 node.points / venue.clues，
// 不保存副本，也不读取或写入旧 OWLIB / LIB。
const WorldParkSpatial = (() => {
  const MIN_WIDTH = 5, MIN_HEIGHT = 4, MAX_WIDTH = 192, MAX_HEIGHT = 128;
  const KNOWN = new Set('.=Wtwf"cgin?KXOUB^Ah+*123456789MT');
  const SOLID = new Set('WtwfciKOUBA');
  const clone = value => JSON.parse(JSON.stringify(value));

  function mapShape(text) {
    const rows = [];
    for (const raw of String(text || '').replace(/\r/g, '').replace(/^\uFEFF/, '').split('\n')) {
      if (raw.startsWith('#')) continue;
      const row = raw.trimEnd().replace(/ /g, '.');
      if (row.length) rows.push(row);
    }
    const width = rows.reduce((max, row) => Math.max(max, row.length), 0);
    const height = rows.length;
    const rectangular = !!width && rows.every(row => row.length === width);
    const at = (x, y) => x < 0 || y < 0 || x >= width || y >= height ? '' : (rows[height - 1 - y] || '')[x] || '';
    const inBounds = (x, y) => Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x < width && y < height;
    const isKnown = (x, y) => inBounds(x, y) && KNOWN.has(at(x, y));
    const isWalkable = (x, y) => isKnown(x, y) && !SOLID.has(at(x, y));
    return { rows, width, height, rectangular, at, inBounds, isKnown, isWalkable };
  }

  function placement(shape, x, y) {
    if (!shape.rectangular) return { ok:false, reason:'地图每一行必须同宽；先修正 mapText 后才能放置点位。' };
    if (shape.width < MIN_WIDTH || shape.height < MIN_HEIGHT || shape.width > MAX_WIDTH || shape.height > MAX_HEIGHT) {
      return { ok:false, reason:`地图尺寸须在 ${MIN_WIDTH}×${MIN_HEIGHT} 至 ${MAX_WIDTH}×${MAX_HEIGHT} 格内；当前为 ${shape.width}×${shape.height}。` };
    }
    if (!shape.inBounds(x, y)) return { ok:false, reason:`(${x}, ${y}) 超出地图范围；x 为 0–${shape.width - 1}，y 为 0–${shape.height - 1}。` };
    if (!shape.isKnown(x, y)) return { ok:false, reason:`(${x}, ${y}) 是未知地图字符，不能放置点位。` };
    if (!shape.isWalkable(x, y)) return { ok:false, reason:`(${x}, ${y}) 是不可通行格「${shape.at(x, y)}」，点位必须放在可通行格。` };
    // 与 Unity 画布一致：必须能从唯一的 T 出生点走到，否则运行时玩家永远够不着这个点。
    const spawns = [];
    for (let sy = 0; sy < shape.height; sy++) for (let sx = 0; sx < shape.width; sx++) if (shape.at(sx, sy) === 'T') spawns.push([sx, sy]);
    if (spawns.length !== 1) return { ok:false, reason:`地图需要且只能有一个 T 出生点（当前 ${spawns.length} 个），无法确认点位可达。` };
    if (!reachable(shape, spawns[0], x, y)) return { ok:false, reason:`(${x}, ${y}) 虽可通行，但从 T 出生点走不到。` };
    return { ok:true, reason:'' };
  }

  function reachable(shape, from, tx, ty) {
    const seen = new Set([from[0] + ',' + from[1]]), queue = [from];
    for (let i = 0; i < queue.length; i++) {
      const [cx, cy] = queue[i];
      if (cx === tx && cy === ty) return true;
      for (const [dx, dy] of [[1,0],[-1,0],[0,1],[0,-1]]) {
        const nx = cx + dx, ny = cy + dy, key = nx + ',' + ny;
        if (!seen.has(key) && shape.isWalkable(nx, ny)) { seen.add(key); queue.push([nx, ny]); }
      }
    }
    return false;
  }

  function canPlace(text, x, y) { return placement(mapShape(text), x, y); }
  // S258：鬼的巡逻格。格式 "x,y" 与 WorldPark.chaserCell / explore.js 相同；上限与 explore.js MaxChaserRoute（32）相同。
  const MAX_ROUTE = 32;
  function routeCellOf(raw) {
    if (typeof WorldPark !== 'undefined' && WorldPark.chaserCell) return WorldPark.chaserCell(raw);
    const m = /^\s*(-?\d+)\s*[,，]\s*(-?\d+)\s*$/.exec(String(raw ?? ''));
    return m ? { x:Number(m[1]), y:Number(m[2]) } : null;
  }
  // 点到已有巡逻格 = 去掉它；点到新格 = 按放置规则检查后加到末尾。返回新数组，不改原数组。
  function routeToggle(shape, route, x, y) {
    const list = Array.isArray(route) ? route.slice() : [];
    const at = list.findIndex(raw => { const c = routeCellOf(raw); return !!c && c.x === x && c.y === y; });
    if (at >= 0) { list.splice(at, 1); return { ok:true, removed:true, route:list, reason:'' }; }
    const verdict = placement(shape, x, y);
    if (!verdict.ok) return { ok:false, removed:false, route:list, reason:verdict.reason };
    if (list.length >= MAX_ROUTE) return { ok:false, removed:false, route:list, reason:`一只鬼最多 ${MAX_ROUTE} 个巡逻格。` };
    list.push(`${x},${y}`);
    return { ok:true, removed:false, route:list, reason:'' };
  }

  function tileColor(tile) {
    if (tile === 'T') return '#4f8cff';
    if (tile === 'M') return '#ff5a4e';
    if (tile === 'W' || tile === 't' || tile === 'w') return '#50445f';
    if ('fciKOUBA'.includes(tile)) return '#3c344d';
    if (tile === '=' || tile === '^') return '#6d6a87';
    if (tile === '?' || tile === 'g' || tile === 'n') return '#665276';
    if (tile === 'X') return '#7b5632';
    if (tile === 'O') return '#326d86';
    if (tile === 'h' || tile === '+') return '#785736';
    if (tile && !KNOWN.has(tile)) return '#6e3150';
    return '#211c2e';
  }

  class Editor {
    constructor() {
      this.zoom = 24;
      this.mode = 'select';
      this.historyKey = '';
      this.undoStack = [];
      this.redoStack = [];
      this.hover = null;
      this.message = '';
      this.drag = null;
      this.opts = null;
      this.host = null;
      this.canvas = null;
      this.viewport = null;
      this.shape = mapShape('');
    }

    mount(host, options) {
      this.host = host;
      this.opts = options;
      const nextKey = String(options.contextKey || '');
      if (nextKey !== this.historyKey) {
        this.historyKey = nextKey;
        this.undoStack = [];
        this.redoStack = [];
        this.mode = 'select';
        this.message = '';
      }
      this.render();
      return this;
    }

    arm(mode) {
      if (!['select', 'clue', 'exit', 'chaser', 'route'].includes(mode)) return;
      if (mode === 'route') {
        const p = (this.opts.points || [])[this.selectedIndex()];
        if (!p || p.kind !== 'chaser') { this.mode = 'select'; this.message = '先点选一只鬼（红色“鬼”字），再点“✎ 鬼的路线”。'; this.updateChrome(); return; }
        this.mode = 'route';
        this.message = `画「${p.label || p.id}」的路线：依次点可通行格，鬼按顺序循环巡逻；再点一次已有的格就去掉它。`;
        this.updateChrome();
        return;
      }
      this.mode = mode;
      this.message = mode === 'select' ? '选择模式：点击标记选择，拖动标记移动。' : `放置模式：点击可通行格新增${mode === 'clue' ? '调查点' : mode === 'chaser' ? '追逐者（鬼）' : '出口'}。`;
      this.updateChrome();
    }

    snapshot() {
      return {
        points: clone(this.opts.points || []),
        clues: clone(this.opts.clues || []),
        selectedIndex: this.selectedIndex()
      };
    }

    selectedIndex() {
      const count = (this.opts.points || []).length;
      const n = Number(this.opts.selectedIndex);
      return Number.isInteger(n) && n >= 0 && n < count ? n : -1;
    }

    record(before, selectedIndex = this.selectedIndex()) {
      const after = this.snapshot();
      after.selectedIndex = selectedIndex;
      if (JSON.stringify(before) === JSON.stringify(after)) return false;
      this.undoStack.push(before);
      if (this.undoStack.length > 100) this.undoStack.shift();
      this.redoStack = [];
      return true;
    }

    restore(snapshot) {
      const points = this.opts.points || [];
      const clues = this.opts.clues || [];
      points.splice(0, points.length, ...clone(snapshot.points));
      clues.splice(0, clues.length, ...clone(snapshot.clues));
      this.opts.onRestore?.(snapshot.selectedIndex);
    }

    undo() {
      const previous = this.undoStack.pop();
      if (!previous) return;
      this.redoStack.push(this.snapshot());
      this.restore(previous);
      this.message = '已撤销本探索地图的上一步点位操作。';
      this.opts.onCommit?.({ type:'undo' });
    }

    redo() {
      const next = this.redoStack.pop();
      if (!next) return;
      this.undoStack.push(this.snapshot());
      this.restore(next);
      this.message = '已重做本探索地图的上一步点位操作。';
      this.opts.onCommit?.({ type:'redo' });
    }

    render() {
      if (!this.host || !this.opts) return;
      this.shape = mapShape(this.opts.mapText);
      this.host.innerHTML = `<div class="wp-spatial" aria-label="探索地图点位画布">
        <div class="wp-spatial-toolbar">
          <span class="wp-spatial-mode" role="group" aria-label="点位工具">
            <button class="btn small" type="button" data-wps-mode="select" title="点击选择已有标记；拖动标记移动">选择 / 移动</button>
            <button class="btn small" type="button" data-wps-mode="clue" title="点击可通行格新建调查点及同阶段 interact 线索">＋ 调查点</button>
            <button class="btn small" type="button" data-wps-mode="exit" title="点击可通行格新建出口">＋ 出口</button>
            <button class="btn small" type="button" data-wps-mode="chaser" title="点击可通行格放置追逐者（鬼）">＋ 追逐者</button>
            <button class="btn small" type="button" data-wps-mode="route" title="先选中一只鬼，再在图上依次点它的巡逻格；再点一次已有的格可去掉">✎ 鬼的路线</button>
          </span>
          <button class="btn small" type="button" data-wps-action="undo">撤销</button>
          <button class="btn small" type="button" data-wps-action="redo">重做</button>
          <span class="spacer"></span>
          <button class="btn small" type="button" data-wps-action="zoom-out" aria-label="缩小地图">−</button>
          <label class="wp-spatial-zoom">缩放 <input type="range" min="12" max="44" step="2" value="${this.zoom}" data-wps-zoom></label>
          <button class="btn small" type="button" data-wps-action="zoom-in" aria-label="放大地图">＋</button>
          <button class="btn small" type="button" data-wps-action="origin" title="回到地图左上角">重置视图</button>
        </div>
        <div class="wp-spatial-viewport" data-wps-viewport><canvas data-wps-canvas aria-label="探索地图；地图坐标x左0、y下0"></canvas></div>
        <div class="wp-spatial-status"><span data-wps-hover>移动鼠标查看格子坐标</span><span data-wps-summary></span></div>
      </div>`;
      this.canvas = this.host.querySelector('[data-wps-canvas]');
      this.viewport = this.host.querySelector('[data-wps-viewport]');
      this.bind();
      this.paintCanvas();
      this.updateChrome();
    }

    bind() {
      this.host.querySelectorAll('[data-wps-mode]').forEach(button => button.onclick = () => this.arm(button.dataset.wpsMode));
      this.host.querySelectorAll('[data-wps-action]').forEach(button => button.onclick = () => {
        const action = button.dataset.wpsAction;
        if (action === 'undo') this.undo();
        else if (action === 'redo') this.redo();
        else if (action === 'zoom-out') this.setZoom(this.zoom - 2);
        else if (action === 'zoom-in') this.setZoom(this.zoom + 2);
        else if (action === 'origin' && this.viewport) { this.viewport.scrollLeft = 0; this.viewport.scrollTop = 0; this.message = '已回到地图左上角；坐标仍以左下角为原点。'; this.updateChrome(); }
      });
      const zoom = this.host.querySelector('[data-wps-zoom]');
      zoom.oninput = () => this.setZoom(Number(zoom.value));
      this.canvas.addEventListener('pointermove', event => this.pointerMove(event));
      this.canvas.addEventListener('pointerleave', () => { if (!this.drag) { this.hover = null; this.updateChrome(); } });
      this.canvas.addEventListener('pointerdown', event => this.pointerDown(event));
      this.canvas.addEventListener('pointerup', event => this.pointerUp(event));
      this.canvas.addEventListener('pointercancel', () => { this.drag = null; this.updateChrome(); });
      // 不监听 wheel 作为摆放动作：滚轮仅供外层页面正常滚动，绝不会生成或移动点位。
    }

    setZoom(value) {
      this.zoom = Math.max(12, Math.min(44, Math.round(value / 2) * 2));
      this.message = `缩放为 ${this.zoom} 像素/格；大图可用画布滚动条平移查看。`;
      this.paintCanvas();
      this.updateChrome();
    }

    previewSize() {
      return { width:Math.max(1, Math.min(MAX_WIDTH, this.shape.width || 1)), height:Math.max(1, Math.min(MAX_HEIGHT, this.shape.height || 1)) };
    }

    paintCanvas() {
      if (!this.canvas) return;
      this.shape = mapShape(this.opts.mapText);
      const preview = this.previewSize(), cell = this.zoom;
      this.preview = preview;
      this.canvas.width = preview.width * cell;
      this.canvas.height = preview.height * cell;
      this.canvas.style.width = `${this.canvas.width}px`;
      this.canvas.style.height = `${this.canvas.height}px`;
      const ctx = this.canvas.getContext('2d');
      ctx.imageSmoothingEnabled = false;
      ctx.fillStyle = '#16131f';
      ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
      for (let y = 0; y < preview.height; y++) for (let x = 0; x < preview.width; x++) {
        const tile = this.shape.at(x, y);
        const top = (preview.height - 1 - y) * cell;
        ctx.fillStyle = tileColor(tile);
        ctx.fillRect(x * cell, top, cell, cell);
        if (cell >= 14) {
          ctx.strokeStyle = 'rgba(239,231,214,.11)';
          ctx.strokeRect(x * cell + .5, top + .5, cell - 1, cell - 1);
          if (tile) {
            ctx.fillStyle = tile === 'T' ? '#fff' : 'rgba(239,231,214,.68)';
            ctx.font = `${Math.max(9, Math.floor(cell * .48))}px var(--mono, monospace)`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(tile, (x + .5) * cell, top + cell * .54);
          }
        }
      }
      const selected = this.selectedIndex();
      (this.opts.points || []).forEach((point, index) => this.paintPoint(ctx, point, index === selected, index, preview, cell));
      if (this.shape.width > MAX_WIDTH || this.shape.height > MAX_HEIGHT) {
        ctx.fillStyle = 'rgba(255,90,78,.88)';
        ctx.fillRect(0, 0, this.canvas.width, 24);
        ctx.fillStyle = '#16131f';
        ctx.font = 'bold 12px sans-serif';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.fillText('地图超出最大尺寸；仅预览左下区域，不能编辑点位。', 8, 12);
      }
    }

    paintPoint(ctx, point, selected, index, preview, cell) {
      if (!Number.isInteger(point?.x) || !Number.isInteger(point?.y) || point.x < 0 || point.y < 0 || point.x >= preview.width || point.y >= preview.height) return;
      const x = (point.x + .5) * cell, y = (preview.height - point.y - .5) * cell;
      const chaser = point.kind === 'chaser';
      const color = chaser ? '#ff5a64' : point.kind === 'exit' ? '#ffc83d' : '#76d6c3';
      if (chaser && Array.isArray(point.route)) {
        // 巡逻格：红色小方块 + 序号；格式解析与 C#/网页规则共用 chaserCell。
        const inside = c => c && c.x >= 0 && c.y >= 0 && c.x < preview.width && c.y < preview.height;
        const cells = point.route.map(routeCellOf).filter(inside);
        if (cells.length) {
          // S258：把巡逻顺序画成连线（鬼 → 第 1 格 → … → 最后一格，虚线回到第 1 格表示循环）。
          const px = c => (c.x + .5) * cell, py = c => (preview.height - c.y - .5) * cell;
          ctx.save();
          ctx.strokeStyle = selected ? 'rgba(255,90,100,.85)' : 'rgba(255,90,100,.35)';
          ctx.lineWidth = selected ? 2.5 : 1.5;
          ctx.beginPath(); ctx.moveTo(x, y); cells.forEach(c => ctx.lineTo(px(c), py(c))); ctx.stroke();
          if (cells.length > 1) { ctx.setLineDash([4, 4]); ctx.beginPath(); ctx.moveTo(px(cells[cells.length - 1]), py(cells[cells.length - 1])); ctx.lineTo(px(cells[0]), py(cells[0])); ctx.stroke(); }
          ctx.restore();
        }
        point.route.forEach((raw, r) => {
          const c = routeCellOf(raw);
          if (!c || c.x < 0 || c.y < 0 || c.x >= preview.width || c.y >= preview.height) return;
          const rx = (c.x + .5) * cell, ry = (preview.height - c.y - .5) * cell, d = Math.max(4, cell * .3);
          ctx.fillStyle = selected ? 'rgba(255,90,100,.9)' : 'rgba(255,90,100,.45)';
          ctx.fillRect(rx - d / 2, ry - d / 2, d, d);
          if (cell >= 22) { ctx.fillStyle = '#16131f'; ctx.font = 'bold 9px sans-serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(String(r + 1), rx, ry + .5); }
        });
      }
      ctx.beginPath();
      ctx.arc(x, y, Math.max(5, cell * .27), 0, Math.PI * 2);
      ctx.fillStyle = color;
      ctx.fill();
      ctx.lineWidth = selected ? 3 : 1.5;
      ctx.strokeStyle = selected ? '#fff' : '#16131f';
      ctx.stroke();
      ctx.fillStyle = '#16131f';
      ctx.font = `bold ${Math.max(8, Math.floor(cell * .40))}px sans-serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(chaser ? '鬼' : point.kind === 'exit' ? '↗' : '•', x, y + .5);
      if (cell >= 20) {
        const label = String(point.label || point.id || `点${index + 1}`).slice(0, 14);
        ctx.font = '11px var(--sans, sans-serif)';
        ctx.textAlign = 'left';
        ctx.fillStyle = '#efe7d6';
        ctx.fillText(label, x + cell * .33, y - cell * .30);
      }
    }

    cellAt(event) {
      if (!this.canvas || !this.preview) return null;
      const box = this.canvas.getBoundingClientRect();
      if (!box.width || !box.height) return null;
      const canvasX = (event.clientX - box.left) * this.canvas.width / box.width;
      const canvasY = (event.clientY - box.top) * this.canvas.height / box.height;
      const x = Math.floor(canvasX / this.zoom);
      const y = this.preview.height - 1 - Math.floor(canvasY / this.zoom);
      return { x, y };
    }

    pointAt(cell) {
      if (!cell) return -1;
      const points = this.opts.points || [];
      const selected = this.selectedIndex();
      if (selected >= 0 && points[selected]?.x === cell.x && points[selected]?.y === cell.y) return selected;
      return points.findIndex(point => point?.x === cell.x && point?.y === cell.y);
    }

    pointerDown(event) {
      if (event.button !== 0) return;
      const cell = this.cellAt(event);
      if (!cell) return;
      event.preventDefault();
      this.canvas.setPointerCapture?.(event.pointerId);
      const pointIndex = this.mode === 'route' ? -1 : this.pointAt(cell); // 画路线时不拖动点位
      this.drag = { pointerId:event.pointerId, start:cell, pointIndex, before:this.snapshot(), moved:false, invalid:'' };
      this.hover = cell;
      this.updateChrome();
    }

    pointerMove(event) {
      const cell = this.cellAt(event);
      if (!cell) return;
      this.hover = cell;
      const drag = this.drag;
      if (drag && drag.pointerId === event.pointerId && drag.pointIndex >= 0) {
        const point = (this.opts.points || [])[drag.pointIndex];
        const verdict = placement(this.shape, cell.x, cell.y);
        if (verdict.ok && point && (point.x !== cell.x || point.y !== cell.y)) {
          point.x = cell.x;
          point.y = cell.y;
          drag.moved = true;
          drag.invalid = '';
          this.paintCanvas();
        } else if (!verdict.ok) drag.invalid = verdict.reason;
      }
      this.updateChrome();
    }

    pointerUp(event) {
      const drag = this.drag;
      if (!drag || drag.pointerId !== event.pointerId) return;
      this.canvas.releasePointerCapture?.(event.pointerId);
      this.drag = null;
      const cell = this.cellAt(event) || drag.start;
      if (drag.pointIndex >= 0) {
        if (drag.moved && this.record(drag.before)) {
          this.message = `已移动点位到 (${(this.opts.points || [])[drag.pointIndex].x}, ${(this.opts.points || [])[drag.pointIndex].y})。`;
          this.opts.onCommit?.({ type:'move', index:drag.pointIndex });
        } else if (drag.invalid) this.message = `没有移动：${drag.invalid}`;
        else this.choose(drag.pointIndex);
      } else if (this.mode === 'route') {
        this.toggleRoute(cell, drag.before);
      } else if (this.mode === 'select') {
        this.message = '这里没有点位。要新建，请选择“＋ 调查点”或“＋ 出口”，再点击可通行格。';
      } else {
        const verdict = placement(this.shape, cell.x, cell.y);
        if (!verdict.ok) this.message = `没有新建：${verdict.reason}`;
        else this.create(this.mode, cell, drag.before);
      }
      this.updateChrome();
    }

    choose(index) {
      this.opts.onSelect?.(index);
    }

    toggleRoute(cell, before) {
      const index = this.selectedIndex(), point = (this.opts.points || [])[index];
      if (!point || point.kind !== 'chaser') { this.mode = 'select'; this.message = '先点选一只鬼，再画路线。'; return; }
      const result = routeToggle(this.shape, point.route, cell.x, cell.y);
      if (!result.ok) { this.message = `路线没有改变：${result.reason}`; return; }
      point.route = result.route;
      this.record(before, index);
      this.message = result.removed ? `已从路线去掉 (${cell.x}, ${cell.y})；现在 ${result.route.length} 个巡逻格。` : `已加入第 ${result.route.length} 个巡逻格 (${cell.x}, ${cell.y})。`;
      this.opts.onCommit?.({ type:'route', index });
    }

    create(kind, cell, before) {
      const index = this.opts.onCreate?.(kind, cell.x, cell.y);
      if (!Number.isInteger(index) || index < 0) {
        this.message = '没有新建点位；当前草稿未改变。';
        return;
      }
      this.record(before, index);
      this.message = `已新建${kind === 'clue' ? '调查点及同阶段 interact 线索' : kind === 'chaser' ? '追逐者（鬼）；点“✎ 鬼的路线”在图上点它的巡逻格' : '出口'}：(${cell.x}, ${cell.y})。`;
      this.opts.onSelect?.(index);
    }

    deleteSelected() {
      const index = this.selectedIndex();
      if (index < 0) { this.message = '请先选择一个探索点再删除。'; this.updateChrome(); return; }
      const before = this.snapshot();
      const next = this.opts.onDelete?.(index);
      if (!Number.isInteger(next)) { this.message = '没有删除点位；当前草稿未改变。'; this.updateChrome(); return; }
      this.record(before, next);
      this.message = '已删除地图标记；关联线索仍保留，避免误删你已写的剧情。';
      this.opts.onSelect?.(next);
    }

    refresh() {
      if (!this.opts || !this.host) return;
      this.paintCanvas();
      this.updateChrome();
    }

    updateChrome() {
      if (!this.host || !this.opts) return;
      this.host.querySelectorAll('[data-wps-mode]').forEach(button => {
        const active = button.dataset.wpsMode === this.mode;
        button.setAttribute('aria-pressed', active ? 'true' : 'false');
      });
      const undo = this.host.querySelector('[data-wps-action="undo"]');
      const redo = this.host.querySelector('[data-wps-action="redo"]');
      if (undo) undo.disabled = !this.undoStack.length;
      if (redo) redo.disabled = !this.redoStack.length;
      const zoom = this.host.querySelector('[data-wps-zoom]');
      if (zoom && Number(zoom.value) !== this.zoom) zoom.value = this.zoom;
      const hover = this.host.querySelector('[data-wps-hover]');
      const summary = this.host.querySelector('[data-wps-summary]');
      if (hover) {
        if (!this.hover) hover.textContent = this.message || '移动鼠标查看格子坐标；x 左 0、y 下 0。';
        else {
          const verdict = placement(this.shape, this.hover.x, this.hover.y);
          const tile = this.shape.at(this.hover.x, this.hover.y) || '地图外';
          hover.textContent = `(${this.hover.x}, ${this.hover.y}) · 「${tile}」· ${verdict.ok ? '可放置' : verdict.reason}`;
        }
      }
      if (summary) {
        const points = this.opts.points || [];
        const invalid = !this.shape.rectangular || this.shape.width < MIN_WIDTH || this.shape.height < MIN_HEIGHT || this.shape.width > MAX_WIDTH || this.shape.height > MAX_HEIGHT;
        summary.textContent = `${this.shape.width}×${this.shape.height} · ${points.length} 个标记 · ${this.zoom}px/格${invalid ? ' · 地图规格未通过，禁止摆放' : ''}`;
      }
    }
  }

  return Object.freeze({ MIN_WIDTH, MIN_HEIGHT, MAX_WIDTH, MAX_HEIGHT, mapShape, canPlace, placement, Editor, routeToggle, MAX_ROUTE });
})();
if (typeof module !== 'undefined') module.exports = WorldParkSpatial;
