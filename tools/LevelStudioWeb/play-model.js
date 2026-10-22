// ── S253 网页试玩的“看得见的东西”（纯逻辑）：逐行移植
//    Assets/Scripts/WorldPark/WorldParkStageCues.cs（演出时机与文字）
//    Assets/Scripts/WorldPark/WorldParkSceneModel.cs（剧情 / 揭示 / 结局字幕卡）
//    Assets/Scripts/WorldPark/WorldParkTableModel.cs（博弈桌）
//    Tools/WorldParkVerify/explore-parity.cjs 用 Snapshot views 子命令与 C# 逐项对比；改文字/时机必须两边同时改。
const WPPlayModel = (() => {
  'use strict';
  const WP = typeof WorldPark !== 'undefined' ? WorldPark : require('./worldpark.js');
  const isBlank = s => s == null || s === '';

  // ── WorldParkStageCues ──
  const Cues = {
    EntranceSeconds: 3.4, ResultSeconds: 2.6, EliminatedSeconds: 1.6, CountdownFrom: 10, CountdownUrgent: 5,
    SuitSymbol(suit) { const s = String(suit == null ? '' : suit).replace(/^\s+/, ''); if (!s.length) return ''; return '♠♥♦♣'.indexOf(s[0]) >= 0 ? s[0] : ''; },
    Rank(d) { if (d === 1) return 'A'; if (d === 11) return 'J'; if (d === 12) return 'Q'; if (d === 13) return 'K'; return d >= 2 && d <= 10 ? String(d) : ''; },
    Card(v) { if (!v) return ''; return Cues.SuitSymbol(v.suit) + Cues.Rank(v.difficulty | 0); },
    Tone(v) { const s = Cues.SuitSymbol(v ? v.suit : ''); return s === '♥' || s === '♦' ? 'red' : 'cold'; },
    VisaDays(v) { return !v ? 1 : Math.max(1, v.difficulty | 0); },
    Entrance(v) {
      if (!v) return null;
      return { kind: 'entrance', card: Cues.Card(v), title: v.name == null ? v.id : v.name, sub: '难度 ' + (v.difficulty | 0) + (isBlank(v.suit) ? '' : ' · ' + v.suit) + ' · 参加登记完成 · GAME START', seconds: Cues.EntranceSeconds, sound: 'neon', tone: Cues.Tone(v) };
    },
    Activity(v, won, reason) {
      return won
        ? { kind: 'pass', card: Cues.Card(v), title: '通过', sub: reason == null ? '' : reason, seconds: 1.4, sound: 'pass', tone: Cues.Tone(v) }
        : { kind: 'laser', card: Cues.Card(v), title: '出局', sub: reason == null ? '' : reason, seconds: Cues.ResultSeconds, sound: 'laser', tone: Cues.Tone(v) };
    },
    Eliminated(name) { return { kind: 'eliminated', card: '', title: (name == null ? '' : name) + ' 出局', sub: 'GAME OVER', seconds: Cues.EliminatedSeconds, sound: 'laser', tone: 'red' }; },
    Ending(v, n) {
      if (!n || n.kind !== 'ending') return null;
      const result = n.result == null ? '' : n.result;
      if (result === 'clear') return { kind: 'clear', card: Cues.Card(v), title: 'GAME CLEAR', sub: '签证 +' + Cues.VisaDays(v) + ' 天', seconds: Cues.ResultSeconds, sound: 'clear', tone: Cues.Tone(v) };
      if (result === 'dead' || result === 'fail') return { kind: 'gameover', card: Cues.Card(v), title: 'GAME OVER', sub: n.name == null ? '' : n.name, seconds: Cues.ResultSeconds, sound: 'gameover', tone: 'red' };
      return { kind: 'end', card: Cues.Card(v), title: '旅程终点', sub: isBlank(n.name) ? result : n.name, seconds: Cues.ResultSeconds, sound: 'end', tone: Cues.Tone(v) };
    },
    CountdownBeep(before, after) {
      if (!(before > after) || after < 0) return 0;
      const a = Math.ceil(before - 1e-9), b = Math.ceil(after - 1e-9);
      if (b < a && b >= 1 && b <= Cues.CountdownFrom) return b;
      return 0;
    },
    Urgent(r) { return r > 0 && r <= Cues.CountdownUrgent; },
    NewlyEliminated(def, before, after) {
      const names = [];
      if (!def || !before || !after || !def.seats) return names;
      const n = Math.min(def.seats.length, Math.min(before.alive.length, after.alive.length));
      for (let i = 0; i < n; i++) if (i !== after.player && before.alive[i] && !after.alive[i] && def.seats[i]) names.push(def.seats[i].name);
      return names;
    },
    ChaseLine(e) {
      switch (e.type) {
        case 'seen': return '「' + e.name + '」看见你了！';
        case 'lost': return '「' + e.name + '」跟丢了你';
        case 'rustle': return '草在晃——「' + e.name + '」起疑了';
        case 'giveup': return '「' + e.name + '」放弃搜寻，回去巡逻';
        case 'caught': return '被「' + e.name + '」抓住了';
      }
      return '';
    },
    ChaseSound(e) { return e.type === 'seen' ? 'alarm' : e.type === 'rustle' ? 'rustle' : ''; }
  };

  // ── WorldParkSceneModel ──
  const Scene = {
    CharsPerSecond: 30, TwistDelay: 0.5, TwistGap: 0.9,
    ResultLabel(r) { switch (r == null ? '' : r) { case 'clear': return '通过'; case 'fail': return '未通过'; case 'dead': return '出局'; case 'leave': return '离开'; case 'any': return '任意'; default: return r == null ? '' : r; } },
    Build(s, playable) {
      const sc = { kind: '', card: '', tone: '', venueName: '', title: '', text: '', rules: '', twists: [], missed: [], notes: [], stamp: '', stampSub: '', doors: [], journey: [], action: '' };
      if (!s || !s.pack) return sc;
      const v = s.Venue, n = s.Current;
      if (!v || !n) return sc;
      sc.kind = n.kind == null ? '' : n.kind;
      sc.card = Cues.Card(v); sc.tone = Cues.Tone(v);
      sc.venueName = isBlank(v.name) ? v.id : v.name;
      sc.title = isBlank(n.name) ? n.id : n.name;
      sc.text = n.text == null ? '' : n.text;
      sc.rules = v.rules == null ? '' : v.rules;
      for (const c of v.clues || []) {
        if (!c) continue;
        const key = v.id + '/' + c.id, found = s.found.has(key), revealed = s.revealed.has(key);
        const name = c.name == null ? c.id : c.name;
        if (sc.kind === 'reveal' && c.revealAt === n.id) {
          if (revealed) sc.twists.push({ clue: c.id, name, before: c.hint == null ? '' : c.hint, after: c.truth == null ? '' : c.truth });
          else if (!found) sc.missed.push(name);
        }
        if (found) sc.notes.push({ clue: c.id, name, before: c.hint == null ? '' : c.hint, after: revealed ? (c.truth == null ? '' : c.truth) : '' });
      }
      if (sc.kind === 'ending') {
        const cue = Cues.Ending(v, n);
        sc.stamp = cue.title; sc.stampSub = cue.sub;
        for (const l of s.pack.links || []) {
          if (!l || l.from !== s.venueId) continue;
          const target = WP.venue(s.pack, l.to);
          const can = !!s.CanTravel(l.to), play = !!target && (!playable || playable(target));
          const door = {
            venue: l.to == null ? '' : l.to,
            name: !target ? (l.to == null ? '' : l.to) : (isBlank(target.name) ? target.id : target.name),
            card: !target ? '?' : Cues.Card(target),
            open: can && play,
            reason: !can ? '需要本场结果为“' + Scene.ResultLabel(l.requires) + '”' : !play ? '这个会场的玩法还没有构建' : ''
          };
          const existing = sc.doors.findIndex(d => d.venue === door.venue);
          if (existing < 0) sc.doors.push(door);
          else if (door.open && !sc.doors[existing].open) sc.doors[existing] = door;
        }
      } else sc.action = sc.kind === 'room' ? '读完提示，进入房间' : '继续';
      for (const venue of s.pack.venues || []) {
        if (!venue) continue;
        let state = venue.id === s.venueId ? '当前' : '未到达';
        if (Object.prototype.hasOwnProperty.call(s.results, venue.id)) state = Scene.ResultLabel(s.results[venue.id]);
        else if (venue.id !== s.venueId && s.CanTravel(venue.id)) state = '可前往';
        // S253：会场名已经以牌面开头时不再重复牌面；与 WorldParkSceneModel.cs 同语义
        const card = Cues.Card(venue), label = isBlank(venue.name) ? venue.id : venue.name;
        sc.journey.push((card.length === 0 || label.startsWith(card) ? label : card + ' ' + label) + ' — ' + state);
      }
      return sc;
    },
    Visible(text, t) {
      const len = text == null ? 0 : text.length;
      if (t <= 0 || len === 0) return 0;
      let k = Math.min(len, Math.floor(t * Scene.CharsPerSecond));
      if (k > 0 && k < len) { const code = text.charCodeAt(k - 1); if (code >= 0xD800 && code <= 0xDBFF) k++; }
      return k;
    },
    TextDone(sc) { return (!sc || sc.text == null ? 0 : sc.text.length) / Scene.CharsPerSecond; },
    TwistAt(sc, i) { return Scene.TextDone(sc) + Scene.TwistDelay + i * Scene.TwistGap; },
    ReadyAt(sc) { if (!sc) return 0; return sc.twists.length === 0 ? Scene.TextDone(sc) : Scene.TwistAt(sc, sc.twists.length - 1) + 0.4; }
  };

  // ── WorldParkTableModel ──
  const Table = {
    FlipStart: 0.5, FlipGap: 0.45, ResultDelay: 0.35, ResultHold: 1.6,
    RevealLength(rec, player) {
      let npc = 0;
      if (rec) for (let i = 0; i < rec.picks.length; i++) if (i !== player && !isBlank(rec.picks[i])) npc++;
      return Table.FlipStart + Math.max(0, npc - 1) * Table.FlipGap + Table.ResultDelay + Table.ResultHold;
    },
    Build(def, before, st, revealT) {
      const v = { mode: '', phase: '', player: -1, seats: [], roundLine: '', prompt: '', info: [], revealing: false, resultShown: false, inputOpen: false, finished: false, won: false, flipped: 0, revealLength: 0, centerBig: '', centerSmall: '', newRule: '', finishLine: '' };
      if (!def || !def.seats || !st || !isBlank(st.error)) { v.phase = 'error'; return v; }
      v.mode = def.mode == null ? '' : def.mode;
      v.player = st.player;
      const n = Math.min(def.seats.length, st.alive.length);
      const rec = st.records.length > 0 ? st.records[st.records.length - 1] : null;
      if (before && (before.alive.length !== n || before.records.length !== st.records.length - 1)) before = null;
      if (rec && revealT >= 0) {
        v.revealLength = Table.RevealLength(rec, st.player);
        const resultAt = v.revealLength - Table.ResultHold;
        if (revealT < resultAt) v.phase = 'reveal';
        else if (revealT < v.revealLength) v.phase = 'result';
      }
      if (v.phase === '') v.phase = st.finished ? 'done' : 'choose';
      v.revealing = v.phase === 'reveal' || v.phase === 'result';
      v.resultShown = v.phase === 'result' || v.phase === 'done';
      v.inputOpen = v.phase === 'choose';
      v.finished = v.phase === 'done';
      v.won = st.won;
      const src = v.phase === 'reveal' && before ? before : st;
      let npcCount = 0; for (let i = 0; i < n; i++) if (i !== st.player) npcCount++;
      let k = 0;
      for (let i = 0; i < n; i++) {
        const s = { index: i, name: '', initial: '', me: i === st.player, alive: true, active: false, spotlight: false, revealed: false, winner: false, wrong: false, eliminatedNow: false, lives: 0, maxLives: def.lives, points: 0, lost: 0, gain: 0, x: 0, y: 0, bubble: '', pick: '', truth: '' };
        s.name = s.me ? '你' : (isBlank(def.seats[i].name) ? (def.seats[i].id == null ? '' : def.seats[i].id) : def.seats[i].name);
        s.initial = s.me ? '你' : firstChar(s.name);
        s.alive = src.alive[i];
        s.lives = Math.max(0, i < src.lives.length ? src.lives[i] : 0);
        s.points = i < src.points.length ? src.points[i] : 0;
        if (s.me) { s.x = 0.5; s.y = 0.86; }
        else { const a = Math.PI + Math.PI * (k + 1) / (npcCount + 1); s.x = 0.5 + 0.40 * Math.cos(a); s.y = 0.46 + 0.34 * Math.sin(a); k++; }
        v.seats.push(s);
      }
      if (v.phase === 'choose') buildChoose(def, st, v, n);
      else if (rec) buildReveal(def, before, st, rec, v, n, v.phase === 'done' ? Number.MAX_VALUE : revealT);
      if (v.phase === 'choose') v.roundLine = '第 ' + (st.round + 1) + ' / ' + def.rounds + ' 轮' + (st.round + 1 === def.rounds ? '（最后一轮）' : '');
      else if (v.phase === 'done') v.roundLine = '博弈结束 · 共 ' + st.round + ' 轮';
      else v.roundLine = '第 ' + (!rec ? st.round : rec.round) + ' 轮 · 亮牌';
      if (v.mode === 'split') v.roundLine += '　你的目标：' + def.goal + ' 分';
      if (v.finished) {
        const last = st.transcript.length > 0 ? st.transcript[st.transcript.length - 1] : '';
        v.finishLine = (st.won ? '你活下来了' : '失败') + (last.startsWith('【') && last.indexOf('】') > 0 ? '：' + last.slice(last.indexOf('】') + 1) : '');
      }
      return v;
    }
  };
  function buildChoose(def, st, v, n) {
    v.prompt = st.prompt == null ? '' : st.prompt;
    for (const line of st.pending) {
      let said = '', who = -1;
      if (v.mode === 'mark') { const r = speaker(def, n, st.player, line, '', '说你的标记是'); who = r.who; said = r.rest; if (who >= 0) said = '你的标记是 ' + said; }
      else if (v.mode === 'split') { const r = speaker(def, n, st.player, line, '本轮对手：', '　对方说：'); who = r.who; said = r.rest; if (who >= 0) { said = said.replace(/^[“”"]+|[“”"]+$/g, ''); v.seats[who].spotlight = true; } }
      if (who >= 0) v.seats[who].bubble = said;
      else v.info.push(line);
    }
    for (const s of v.seats) s.active = s.alive && (v.mode !== 'split' || s.me || s.spotlight);
  }
  function buildReveal(def, before, st, rec, v, n, t) {
    let j = 0, flips = 0;
    for (let i = 0; i < n; i++) {
      const s = v.seats[i], pick = i < rec.picks.length ? (rec.picks[i] == null ? '' : rec.picks[i]) : '';
      s.active = pick !== '';
      if (v.mode === 'split' && i === rec.opponent) s.spotlight = true;
      if (!s.active) continue;
      s.pick = pick;
      if (s.me) s.revealed = true;
      else { s.revealed = t >= Table.FlipStart + j * Table.FlipGap; if (s.revealed) flips++; j++; }
    }
    v.flipped = flips;
    if (!v.resultShown) return;
    const winnerNames = [];
    for (let i = 0; i < n; i++) {
      const s = v.seats[i];
      if (before) {
        s.lost = Math.max(0, Math.max(0, before.lives[i]) - Math.max(0, st.lives[i]));
        s.gain = i < st.points.length && i < before.points.length ? st.points[i] - before.points[i] : 0;
        s.eliminatedNow = before.alive[i] && !st.alive[i];
      }
      if (!s.active) continue;
      if (v.mode === 'mark') { s.truth = i < rec.marks.length ? (rec.marks[i] == null ? '' : rec.marks[i]) : ''; s.wrong = s.truth !== '' && s.truth !== s.pick; }
      if (v.mode === 'beauty' && rec.winners.includes(i)) { s.winner = true; winnerNames.push(s.name); }
    }
    const me = st.player;
    if (v.mode === 'beauty') {
      v.centerBig = '目标 ' + rec.target;
      v.centerSmall = '最接近：' + (winnerNames.length > 0 ? winnerNames.join('、') : '无人') + '；其余的人扣命';
    } else if (v.mode === 'mark') {
      const mine = me >= 0 && me < rec.marks.length ? rec.marks[me] : '?';
      v.centerBig = '你的标记是 ' + mine;
      v.centerSmall = rec.correct ? '答对了' : '答错，失去一条命';
    } else {
      const opp = rec.opponent, oppName = opp >= 0 && opp < v.seats.length ? v.seats[opp].name : '对手';
      const mine = me >= 0 && me < rec.picks.length ? rec.picks[me] : '', theirs = opp >= 0 && opp < rec.picks.length ? rec.picks[opp] : '';
      v.centerBig = '你 ' + mine + '　·　' + oppName + ' ' + theirs;
      if (mine === '分' && theirs === '分') v.centerSmall = '双方各 +1 分';
      else if (mine === '偷' && theirs === '分') v.centerSmall = '你 +2 分，' + oppName + ' 失去一条命';
      else if (mine === '分' && theirs === '偷') v.centerSmall = oppName + ' +2 分，你失去一条命';
      else v.centerSmall = '两人都偷，什么都没得到';
    }
    const from = before ? before.transcript.length : 0;
    for (let i = from; i < st.transcript.length; i++) if (st.transcript[i].startsWith('【新规则】')) v.newRule = st.transcript[i];
  }
  function speaker(def, n, player, line, prefix, mid) {
    let best = -1, bestLen = -1, rest = '';
    for (let i = 0; i < n; i++) {
      if (i === player) continue;
      const name = def.seats[i].name == null ? '' : def.seats[i].name;
      if (name.length === 0) continue;
      const head = prefix + name + mid;
      if (line.startsWith(head) && name.length > bestLen) { best = i; bestLen = name.length; rest = line.slice(head.length); }
    }
    return { who: best, rest };
  }
  function firstChar(name) {
    if (isBlank(name)) return '?';
    const c = name.charCodeAt(0);
    return c >= 0xD800 && c <= 0xDFFF && name.length > 1 ? name.slice(0, 2) : name.slice(0, 1);
  }

  return { Cues, Scene, Table };
})();
if (typeof module !== 'undefined') module.exports = WPPlayModel;
