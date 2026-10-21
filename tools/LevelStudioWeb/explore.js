// ── S253 网页试玩的现场探索内核：逐行移植 Assets/Scripts/WorldPark/WorldParkExplore.cs
//    + OverworldMap 的 Walkable / Path / LineOfSight / CanSee / SeesRustle / Free / Move。
//    计算顺序与 C# 一致（Tools/WorldParkVerify/parity.cjs 用同一包、同一输入脚本逐步对比位置、追逐者状态与事件）。
//    规则改动必须两边同时改；这里不允许出现 C# 没有的规则。
const WPExplore = (() => {
  'use strict';
  const WP = typeof WorldPark !== 'undefined' ? WorldPark : require('./worldpark.js');

  // ── 地形表：浏览器里用 build.py 生成的 OW_TILES；Node 下按 build.py 同一正则读 OverworldCatalog.cs ──
  let TILES = typeof OW_TILES !== 'undefined' ? OW_TILES : null;
  if (!TILES && typeof require !== 'undefined') {
    const fs = require('fs'), path = require('path');
    const src = fs.readFileSync(path.join(__dirname, '..', '..', 'Assets', 'Scripts', 'Overworld', 'OverworldCatalog.cs'), 'utf8');
    const re = /T\('(.)', "(\w+)", "([^"]+)", "([^"]+)", "(\w+)", (\w+), (\w+), (\w+), (\d+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, "((?:[^"\\]|\\.)*)", "((?:[^"\\]|\\.)*)"\)/g;
    TILES = []; let m;
    while ((m = re.exec(src))) TILES.push({ c: m[1], k: m[2], zh: m[3], solid: m[6] === 'true', sight: m[7] === 'true', hides: m[8] === 'true', cost: +m[9], rgb: [+m[10], +m[11], +m[12]] });
  }
  const byChar = new Map((TILES || []).map(t => [t.c, t]));
  const tile = c => { if (c === ' ') c = '.'; if (c >= '1' && c <= '9') c = '1'; return byChar.get(c) || null; };
  const solid = c => { const t = tile(c); return !t || t.solid; };
  const blocksSight = c => { const t = tile(c); return !t || t.sight; };
  const hides = c => { const t = tile(c); return !!t && !!t.hides; };
  const cost = c => { const t = tile(c); return !t || t.solid ? 0 : t.cost; };
  const height = c => c === 'A' ? 2 : c === '^' ? 1 : 0;

  // ── 地图（与 OverworldMap.Parse / At / Find 同语义，只取网格）──
  function parseMap(text) {
    const grid = [];
    for (const raw of String(text || '').replace(/\r/g, '').replace(/\uFEFF/g, '').split('\n')) {
      if (raw.startsWith('#')) continue;
      const row = raw.trimEnd(); if (row.length > 0) grid.push(row.replace(/ /g, '.'));
    }
    const w = grid.reduce((a, r) => Math.max(a, r.length), 0);
    const rows = grid.map(r => r.padEnd(w, '.'));
    return { rows, W: rows.length > 0 ? rows[0].length : 0, H: rows.length };
  }
  function at(m, x, y) { const r = m.H - 1 - y; if (r < 0 || r >= m.H || x < 0 || x >= m.rows[r].length) return 't'; return m.rows[r][x]; }
  function find(m, c) { const l = []; for (let r = 0; r < m.H; r++) for (let x = 0; x < m.rows[r].length; x++) if (m.rows[r][x] === c) l.push({ x, y: m.H - 1 - r }); return l; }
  const walkable = (m, x, y) => x >= 0 && y >= 0 && x < m.W && y < m.H && !solid(at(m, x, y));

  // Dijkstra（与 C# MinHeap 同样的平手规则：先比代价，再比格子编号）
  const DX = [1, -1, 0, 0], DY = [0, 0, 1, -1];
  function path(m, a, b) {
    const w = m.W, h = m.H;
    if (!walkable(m, a.x, a.y) || !walkable(m, b.x, b.y)) return null;
    const MAX = 2147483647, dist = new Array(w * h).fill(MAX), prev = new Int32Array(w * h).fill(-1), heap = [];
    const less = (p, q) => p[0] < q[0] || (p[0] === q[0] && p[1] < q[1]);
    const push = (d, i) => { heap.push([d, i]); let c = heap.length - 1; while (c > 0) { const p = (c - 1) >> 1; if (!less(heap[c], heap[p])) break; const t = heap[c]; heap[c] = heap[p]; heap[p] = t; c = p; } };
    const pop = () => { const top = heap[0], last = heap.pop(); if (heap.length) { heap[0] = last; let c = 0; for (;;) { const l = c * 2 + 1, r = l + 1; let mm = c; if (l < heap.length && less(heap[l], heap[mm])) mm = l; if (r < heap.length && less(heap[r], heap[mm])) mm = r; if (mm === c) break; const t = heap[c]; heap[c] = heap[mm]; heap[mm] = t; c = mm; } } return top; };
    const s = a.y * w + a.x, goal = b.y * w + b.x; dist[s] = 0; push(0, s);
    while (heap.length) {
      const [d, i] = pop(); if (d > dist[i]) continue; if (i === goal) break;
      const x = i % w, y = (i / w) | 0;
      for (let k = 0; k < 4; k++) { const nx = x + DX[k], ny = y + DY[k]; if (!walkable(m, nx, ny)) continue; const j = ny * w + nx, nd = d + cost(at(m, nx, ny)); if (nd < dist[j]) { dist[j] = nd; prev[j] = i; push(nd, j); } }
    }
    if (dist[goal] === MAX) return null;
    const p = []; for (let i = goal; i !== -1; i = prev[i]) p.push({ x: i % w, y: (i / w) | 0 }); return p.reverse();
  }

  function lineOfSight(m, ax, ay, bx, by) {
    const dx = bx - ax, dy = by - ay, dist = Math.sqrt(dx * dx + dy * dy);
    const steps = Math.max(1, Math.ceil(dist * 4));
    const sx = Math.floor(ax), sy = Math.floor(ay), ex = Math.floor(bx), ey = Math.floor(by);
    const eye = Math.max(height(at(m, sx, sy)), height(at(m, ex, ey)));
    for (let i = 1; i < steps; i++) {
      const t = i / steps;
      const cx = Math.floor(ax + dx * t), cy = Math.floor(ay + dy * t);
      if ((cx === sx && cy === sy) || (cx === ex && cy === ey)) continue;
      const c = at(m, cx, cy);
      if (blocksSight(c) || height(c) > eye) return false;
    }
    return true;
  }
  // 追逐者不用路灯（C# 传入空列表、night=false），这里只移植用到的分支
  function canSee(m, ex, ey, fx, fy, tx, ty, r) {
    const dx = tx - ex, dy = ty - ey, d = Math.sqrt(dx * dx + dy * dy);
    const under = at(m, Math.floor(tx), Math.floor(ty));
    if (hides(under) && d > r.grassRadius) return false;
    if (d > r.nearRadius) {
      const range = r.range;
      if (d > range) return false;
      const fl = Math.sqrt(fx * fx + fy * fy);
      if (fl > 1e-6) {
        const cos = (dx * fx + dy * fy) / (d * fl);
        if (cos < Math.cos(r.halfAngleDeg * Math.PI / 180.0)) return false;
      }
    }
    return lineOfSight(m, ex, ey, tx, ty);
  }
  function seesRustle(m, ex, ey, fx, fy, tx, ty, r) {
    if (!hides(at(m, Math.floor(tx), Math.floor(ty)))) return false;
    return canSee(m, ex, ey, fx, fy, tx, ty, Object.assign({}, r, { grassRadius: 1e9 }));
  }
  const BodyRadius = 0.3;
  function overlaps(m, x, y, r) {
    const x0 = Math.floor(x - r), x1 = Math.floor(x + r - 1e-9), y0 = Math.floor(y - r), y1 = Math.floor(y + r - 1e-9);
    for (let cx = x0; cx <= x1; cx++) for (let cy = y0; cy <= y1; cy++) if (!walkable(m, cx, cy)) return true;
    return false;
  }
  const free = (m, x, y, r = BodyRadius) => !overlaps(m, x, y, r);
  function move(m, x, y, dx, dy, r = BodyRadius) {
    const n = Math.max(1, Math.ceil(Math.max(Math.abs(dx), Math.abs(dy)) / 0.1));
    const sx = dx / n, sy = dy / n;
    for (let i = 0; i < n; i++) {
      if (sx !== 0 && !overlaps(m, x + sx, y, r)) x += sx;
      if (sy !== 0 && !overlaps(m, x, y + sy, r)) y += sy;
    }
    return { x, y };
  }

  // ── 常量（与 C# 相同）──
  const K = {
    DefaultChaserSpeed: 34, DefaultChaserSight: 7, MaxChasers: 4, MaxChaserRoute: 32,
    PlayerViewRadius: 8.0, CatchRadius: 0.6, ChaserHalfAngle: 60.0, ChaserNearRadius: 1.5,
    SearchSeconds: 2.0, RustleRetarget: 1.5, SearchTravelSeconds: 8.0, DirectRange: 1.5, ChaseSlice: 0.05,
    Speed: 4.0, NearbyRadius: 1.1, MaxTickSeconds: 1.0
  };
  const finite = v => typeof v === 'number' && isFinite(v);
  const safe = v => v == null ? '' : String(v);

  class Chaser {
    constructor(point) {
      this.point = point; this.x = point.x + 0.5; this.y = point.y + 0.5; this.fx = 0; this.fy = 1;
      this.mode = 'patrol'; this.SeesPlayer = false; this.lastX = 0; this.lastY = 0;
      this.searchLeft = 0; this.turnLeft = 0; this.repathLeft = 0; this.searchTravel = 0;
      this.route = []; this.routeIndex = 0; this.path = null; this.pathIndex = 0; this.goalX = -2147483648; this.goalY = -2147483648;
    }
    get Name() { return this.point.label ? this.point.label : this.point.id; }
    get ChaseSpeed() { return (this.point.speed > 0 ? this.point.speed : K.DefaultChaserSpeed) / 10.0; }
    get PatrolSpeed() { return this.ChaseSpeed * 0.6; }
    get Sight() { return this.point.sight > 0 ? this.point.sight : K.DefaultChaserSight; }
    get Rules() { const s = this.Sight; return { range: s, nightRange: s, halfAngleDeg: K.ChaserHalfAngle, nearRadius: K.ChaserNearRadius, grassRadius: K.ChaserNearRadius, lampRadius: 0, night: false }; }
  }

  class Run {
    constructor(session, map, node, sx, sy) {
      this.session = session; this.map = map; this.node = node; this.x = sx; this.y = sy;
      this.Elapsed = 0; this.Remaining = 0; this.Ended = false; this.Won = false; this.LastMessage = ''; this.CaughtBy = '';
      this.Chasers = []; this.Events = [];
      this.buildChasers(); this.refreshRemaining();
    }
    get PlayerHidden() { return hides(at(this.map, Math.floor(this.x), Math.floor(this.y))); }
    get Hunted() { return this.Chasers.some(c => c.mode === 'chase'); }
    PlayerSees(c) { if (!c) return false; const dx = c.x - this.x, dy = c.y - this.y; return dx * dx + dy * dy <= K.PlayerViewRadius * K.PlayerViewRadius && lineOfSight(this.map, this.x, this.y, c.x, c.y); }

    Tick(dt, h, v) {
      if (this.Ended) { this.LastMessage = '探索已经结束，等待确认结果'; return; }
      if (!finite(dt) || !finite(h) || !finite(v)) { this.LastMessage = '移动输入或时间不是有限数值'; return; }
      if (dt < 0 || dt > K.MaxTickSeconds) { this.LastMessage = '本次 Tick 时间必须在 0 到 ' + K.MaxTickSeconds + ' 秒之间'; return; }
      let usable = dt;
      if (this.node.timeout > 0) usable = Math.min(usable, Math.max(0.0, this.node.timeout - this.Elapsed));
      const length = Math.sqrt(h * h + v * v);
      if (length > 1.0) { h /= length; v /= length; }
      let blocked;
      if (this.Chasers.length === 0) {
        const dx = h * K.Speed * usable, dy = v * K.Speed * usable;
        const wantedX = this.x + dx, wantedY = this.y + dy;
        const moved = move(this.map, this.x, this.y, dx, dy);
        this.x = moved.x; this.y = moved.y; this.Elapsed += usable;
        blocked = Math.abs(this.x - wantedX) > 1e-7 || Math.abs(this.y - wantedY) > 1e-7;
      } else {
        blocked = false;
        let left = usable;
        while (left > 1e-12 && !this.Ended) {
          const step = Math.min(left, K.ChaseSlice);
          const dx = h * K.Speed * step, dy = v * K.Speed * step;
          const wantedX = this.x + dx, wantedY = this.y + dy;
          const moved = move(this.map, this.x, this.y, dx, dy);
          const playerMoved = Math.abs(moved.x - this.x) > 1e-9 || Math.abs(moved.y - this.y) > 1e-9;
          this.x = moved.x; this.y = moved.y;
          if (Math.abs(this.x - wantedX) > 1e-7 || Math.abs(this.y - wantedY) > 1e-7) blocked = true;
          this.Elapsed += step; left -= step;
          this.updateChasers(step, playerMoved);
        }
      }
      this.refreshRemaining();
      if (this.Ended) return;
      this.LastMessage = blocked ? '移动被地图边界或障碍阻挡' : '';
      if (this.node.timeout > 0 && this.Elapsed >= this.node.timeout - 1e-9) {
        this.Ended = true; this.Won = false; this.Remaining = 0; this.LastMessage = '探索超时，已锁定失败结果';
      }
    }

    get NearbyPoint() {
      if (this.Ended) return null;
      const points = this.node.points || [];
      let best = null, bestDistance = Number.MAX_VALUE;
      const radiusSquared = K.NearbyRadius * K.NearbyRadius;
      for (let i = 0; i < points.length; i++) {
        const point = points[i];
        if (!point || point.kind === 'chaser') continue;
        const px = point.x + 0.5, py = point.y + 0.5, dx = px - this.x, dy = py - this.y, distance = dx * dx + dy * dy;
        if (distance > radiusSquared + 1e-9) continue;
        if (!lineOfSight(this.map, this.x, this.y, px, py)) continue;
        if (distance < bestDistance - 1e-9) { best = point; bestDistance = distance; }
      }
      return best;
    }

    /** 返回 {ok, message}；出口只锁存结果，绝不自行 ResolveExplore（与 C# Interact 同语义）。 */
    Interact(pointId) {
      const fail = message => { this.LastMessage = message; return { ok: false, message }; };
      if (this.Ended) return fail('探索已经结束，等待确认结果');
      const point = this.findPoint(pointId);
      if (!point) return fail('当前探索没有现场点：' + safe(pointId));
      const px = point.x + 0.5, py = point.y + 0.5, dx = px - this.x, dy = py - this.y;
      if (dx * dx + dy * dy > K.NearbyRadius * K.NearbyRadius + 1e-9) return fail('离现场点太远，必须在 ' + K.NearbyRadius + ' 格内交互');
      if (!lineOfSight(this.map, this.x, this.y, px, py)) return fail('现场点被墙体或遮挡隔开，无法交互');
      if (point.kind === 'clue') {
        const s = this.session, v = s.Venue, clue = (v?.clues || []).find(c => c && c.id === point.clue);
        const already = !!clue && s.found.has(v.id + '/' + clue.id);
        try { s.ObserveClue(point.clue); } catch (e) { return fail(e.message); }
        const message = (already ? '已观察过该线索：' : '已观察线索：') + safe(clue?.name);
        this.LastMessage = message; return { ok: true, message };
      }
      if (point.kind !== 'exit') return fail('现场点类型不能交互：' + safe(point.kind));
      const missing = this.missingRequirements(point.requires);
      if (missing.length > 0) return fail('出口还缺少线索：' + missing.join('、'));
      this.Ended = true; this.Won = point.outcome === 'win'; this.refreshRemaining();
      this.LastMessage = '出口已锁定；请确认后提交现场结果';
      return { ok: true, message: this.LastMessage };
    }

    // ── 追逐者 ──
    buildChasers() {
      for (const point of this.node.points || []) {
        if (!point || point.kind !== 'chaser' || this.Chasers.length >= K.MaxChasers) continue;
        if (!walkable(this.map, point.x, point.y)) continue;
        const c = new Chaser(point);
        for (const raw of point.route || []) {
          const cell = WP.chaserCell(raw);
          if (cell && walkable(this.map, cell.x, cell.y) && c.route.length < K.MaxChaserRoute) c.route.push(cell);
        }
        if (c.route.length > 0) face(c, c.route[0].x + 0.5 - c.x, c.route[0].y + 0.5 - c.y);
        c.turnLeft = 1.5;
        this.Chasers.push(c);
      }
    }
    updateChasers(step, playerMoved) {
      const x = this.x, y = this.y, m = this.map;
      for (const c of this.Chasers) {
        const rules = c.Rules;
        const sees = canSee(m, c.x, c.y, c.fx, c.fy, x, y, rules);
        const rustle = !sees && playerMoved && seesRustle(m, c.x, c.y, c.fx, c.fy, x, y, rules);
        if (sees) {
          if (c.mode !== 'chase') this.addEvent(c, 'seen');
          c.mode = 'chase'; c.lastX = x; c.lastY = y;
        } else if (c.mode === 'chase') {
          c.mode = 'search'; c.searchLeft = K.SearchSeconds; c.searchTravel = K.SearchTravelSeconds; c.path = null;
          this.addEvent(c, 'lost');
        } else if (rustle) {
          const fresh = c.mode !== 'search';
          const rx = x - c.lastX, ry = y - c.lastY;
          if (fresh || rx * rx + ry * ry > K.RustleRetarget * K.RustleRetarget) {
            if (fresh) { this.addEvent(c, 'rustle'); c.searchTravel = K.SearchTravelSeconds; }
            c.mode = 'search'; c.searchLeft = K.SearchSeconds; c.lastX = x; c.lastY = y; c.path = null;
          }
        }
        c.SeesPlayer = sees;
        c.repathLeft -= step;
        if (c.mode === 'chase') this.moveToward(c, x, y, c.ChaseSpeed * step, true);
        else if (c.mode === 'search') {
          const ddx = c.lastX - c.x, ddy = c.lastY - c.y;
          if (ddx * ddx + ddy * ddy > 0.04 && c.searchTravel > 0) {
            c.searchTravel -= step;
            this.moveToward(c, c.lastX, c.lastY, c.ChaseSpeed * 0.8 * step, false);
          } else {
            c.searchLeft -= step; c.turnLeft -= step;
            if (c.turnLeft <= 0) { turn(c); c.turnLeft = 0.5; }
            if (c.searchLeft <= 0) { c.mode = 'patrol'; c.path = null; c.turnLeft = 1.5; this.addEvent(c, 'giveup'); }
          }
        } else this.patrol(c, step);
        const cx = c.x - x, cy = c.y - y;
        if (cx * cx + cy * cy <= K.CatchRadius * K.CatchRadius) {
          this.Ended = true; this.Won = false; this.CaughtBy = c.Name; this.LastMessage = '被「' + c.Name + '」抓住了';
          this.addEvent(c, 'caught');
          return;
        }
      }
    }
    patrol(c, step) {
      if (c.route.length === 0) {
        const hx = c.point.x + 0.5 - c.x, hy = c.point.y + 0.5 - c.y;
        if (hx * hx + hy * hy > 0.04) { this.moveToward(c, c.point.x + 0.5, c.point.y + 0.5, c.PatrolSpeed * step, false); return; }
        c.turnLeft -= step;
        if (c.turnLeft <= 0) { turn(c); c.turnLeft = 1.5; }
        return;
      }
      const target = c.route[c.routeIndex % c.route.length];
      const tx = target.x + 0.5, ty = target.y + 0.5, dx = tx - c.x, dy = ty - c.y;
      if (dx * dx + dy * dy <= 0.0025) { c.routeIndex = (c.routeIndex + 1) % c.route.length; c.path = null; return; }
      this.moveToward(c, tx, ty, c.PatrolSpeed * step, false);
    }
    moveToward(c, tx, ty, distance, chasing) {
      const cx = Math.floor(c.x), cy = Math.floor(c.y), gx = Math.floor(tx), gy = Math.floor(ty);
      const startX = c.x, startY = c.y, nearX = tx - c.x, nearY = ty - c.y;
      if ((cx === gx && cy === gy) || (nearX * nearX + nearY * nearY <= K.DirectRange * K.DirectRange && this.clearLine(c.x, c.y, tx, ty))) {
        c.path = null; this.direct(c, tx, ty, distance);
      } else {
        const need = c.path === null || c.goalX !== gx || c.goalY !== gy || (chasing && c.repathLeft <= 0);
        if (need) { c.path = path(this.map, { x: cx, y: cy }, { x: gx, y: gy }); c.pathIndex = 1; c.goalX = gx; c.goalY = gy; c.repathLeft = 0.25; }
        if (c.path === null) return;
        while (distance > 1e-9 && c.pathIndex < c.path.length) {
          const nx = c.path[c.pathIndex].x + 0.5, ny = c.path[c.pathIndex].y + 0.5;
          const ddx = nx - c.x, ddy = ny - c.y, d = Math.sqrt(ddx * ddx + ddy * ddy);
          if (d <= distance) { c.x = nx; c.y = ny; distance -= d; c.pathIndex++; }
          else { c.x += ddx / d * distance; c.y += ddy / d * distance; distance = 0; }
        }
        if (distance > 1e-9 && c.pathIndex >= c.path.length) this.direct(c, tx, ty, distance);
      }
      face(c, c.x - startX, c.y - startY);
    }
    clearLine(ax, ay, bx, by) {
      const dx = bx - ax, dy = by - ay, d = Math.sqrt(dx * dx + dy * dy);
      const n = Math.max(1, Math.ceil(d / 0.1));
      for (let i = 1; i <= n; i++) { const t = i / n; if (!free(this.map, ax + dx * t, ay + dy * t)) return false; }
      return true;
    }
    direct(c, tx, ty, distance) {
      const dx = tx - c.x, dy = ty - c.y, d = Math.sqrt(dx * dx + dy * dy);
      if (d < 1e-9) return;
      const s = Math.min(distance, d) / d;
      const moved = move(this.map, c.x, c.y, dx * s, dy * s);
      c.x = moved.x; c.y = moved.y;
    }
    addEvent(c, type) { this.Events.push({ time: this.Elapsed, chaser: c.point.id, name: c.Name, type }); }
    findPoint(id) { if (!id) return null; return (this.node.points || []).find(p => p && p.id === id) || null; }
    missingRequirements(requires) {
      const missing = []; if (!requires) return missing;
      const venue = this.session.Venue;
      for (const clueId of requires) {
        const k = safe(venue ? venue.id : '') + '/' + safe(clueId);
        if (this.session.found.has(k)) continue;
        let label = safe(clueId);
        const clue = (venue?.clues || []).find(c => c && c.id === clueId);
        if (clue && clue.name) label = clue.name;
        missing.push(label);
      }
      return missing;
    }
    refreshRemaining() { this.Remaining = this.node.timeout > 0 ? Math.max(0.0, this.node.timeout - this.Elapsed) : 0.0; }
  }
  function face(c, dx, dy) { const len = Math.sqrt(dx * dx + dy * dy); if (len < 1e-9) return; c.fx = dx / len; c.fy = dy / len; }
  function turn(c) { const fx = c.fx; c.fx = -c.fy; c.fy = fx; }

  /** 在会话当前 explore 节点上创建纯运行；返回 {run, error}。 */
  function create(session) {
    if (!session) return { run: null, error: '探索会话不存在' };
    const node = session.Current;
    if (!node || node.kind !== 'explore') return { run: null, error: '当前阶段不是 explore' };
    if (node.timeout < 0) return { run: null, error: 'explore timeout 不能小于 0' };
    if (!node.mapText || !String(node.mapText).trim()) return { run: null, error: 'explore 缺少 mapText' };
    const map = parseMap(node.mapText);
    if (map.W <= 0 || map.H <= 0) return { run: null, error: 'explore 地图为空' };
    const spawns = find(map, 'T');
    if (spawns.length !== 1) return { run: null, error: 'explore 地图需要且只能有一个 T 出生点' };
    const x = spawns[0].x + 0.5, y = spawns[0].y + 0.5;
    if (!free(map, x, y)) return { run: null, error: 'explore T 出生点不可站立' };
    return { run: new Run(session, map, node, x, y), error: '' };
  }

  /** 跨端对比与试玩画面共用的状态快照（字段与 Snapshot explore 子命令一致）。 */
  function snapshot(run) {
    const near = run.NearbyPoint;
    return {
      x: run.x, y: run.y, elapsed: run.Elapsed, remaining: run.Remaining, ended: run.Ended, won: run.Won, caughtBy: run.CaughtBy, msg: run.LastMessage,
      hidden: run.PlayerHidden, hunted: run.Hunted, nearby: near ? near.id : '',
      chasers: run.Chasers.map(c => ({ id: c.point.id, x: c.x, y: c.y, fx: c.fx, fy: c.fy, mode: c.mode, sees: c.SeesPlayer, lastX: c.lastX, lastY: c.lastY, playerSees: run.PlayerSees(c) })),
      events: run.Events.map(e => ({ time: e.time, chaser: e.chaser, name: e.name, type: e.type })),
      found: Array.from(run.session.found).sort()
    };
  }

  return { K, create, snapshot, parseMap, at, find, walkable, path, lineOfSight, canSee, seesRustle, free, move, tile, solid, blocksSight, hides, height, TILES: () => TILES };
})();
if (typeof module !== 'undefined') module.exports = WPExplore;
