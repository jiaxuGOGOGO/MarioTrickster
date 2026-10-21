'use strict';
// S251：弥留之国式博弈内核网页版，与 Assets/Scripts/WorldPark/BorderlandGame.cs 逐字同语义（跨端用例守护）。
const BorderlandGame = (() => {
  const MinSeats = 2, MaxSeats = 8, MaxRounds = 20, MaxLives = 20, MaxParseSeats = 64;
  const Modes = ['beauty', 'mark', 'split'];
  const Personas = ['player', 'naive', 'reasoner', 'deep', 'adaptive', 'liar', 'loyal', 'mirror'];
  const Suits = ['♠', '♥', '♦', '♣'];
  const isMode = v => Modes.includes(v), isPersona = v => Personas.includes(v);
  const safe = v => (typeof v === 'string' ? v : '');
  const div = (a, b) => Math.trunc(a / b);
  const invalidId = v => typeof v !== 'string' || !v.trim() || /[\/\u0000-\u001f]/.test(v);

  function check(d) {
    const issues = [], add = (severity, code, text) => issues.push({ severity, code, text });
    if (!d) { add('error', 'game_def_required', 'game 阶段需要 game 规则对象'); return issues; }
    if (!isMode(d.mode)) add('error', 'game_mode_invalid', '博弈 mode 只能是 beauty / mark / split：' + safe(d.mode));
    if (d.rounds < 1 || d.rounds > MaxRounds) add('error', 'game_rounds_invalid', '博弈轮数必须是 1–' + MaxRounds);
    if (d.lives < 1 || d.lives > MaxLives) add('error', 'game_lives_invalid', '每人命数必须是 1–' + MaxLives);
    if (d.mode === 'beauty' && (d.ratio < 1 || d.ratio > 100)) add('error', 'game_ratio_invalid', '数字猜心的比例必须是 1–100（百分比）');
    if (d.goal < 0 || d.goal > 100) add('error', 'game_goal_invalid', '目标分数必须是 0–100');
    if (d.seed < 0) add('error', 'game_seed_invalid', '随机种子不能小于 0');
    if (d.escalate !== 0 && d.escalate !== 1) add('error', 'game_escalate_invalid', 'escalate 只能是 0 或 1');
    const seats = d.seats || [];
    if (seats.length < MinSeats || seats.length > MaxSeats) add('error', 'game_seats_invalid', '参与者必须是 ' + MinSeats + '–' + MaxSeats + ' 人（含玩家）');
    const ids = new Set(); let players = 0, deceivers = 0;
    for (const seat of seats) {
      if (!seat) { add('error', 'game_seat_id_invalid', '参与者为空'); continue; }
      if (invalidId(seat.id)) add('error', 'game_seat_id_invalid', '参与者 id 不能为空、含斜杠或控制字符');
      else if (ids.has(seat.id)) add('error', 'game_seat_id_duplicate', '参与者 id 重复：' + seat.id); else ids.add(seat.id);
      if (!isPersona(seat.persona)) add('error', 'game_seat_persona_invalid', '未知人格：' + safe(seat.persona));
      if (seat.persona === 'player') players++;
      if (d.mode === 'mark' && seat.persona === 'liar') deceivers++;
      if (d.mode === 'split' && ['liar', 'mirror', 'adaptive', 'reasoner', 'deep'].includes(seat.persona)) deceivers++;
    }
    if (players !== 1) add('error', 'game_player_seat', '必须恰好有一个 persona 为 player 的玩家席位');
    if ((d.mode === 'mark' || d.mode === 'split') && players === 1 && seats.length >= MinSeats && deceivers === 0)
      add('warn', 'game_no_deceiver', '没有会说谎或会报复的参与者：这局缺少信任压力，结果几乎没有悬念');
    return issues;
  }

  function run(d, choices) {
    const result = runCore(d, choices), given = (choices || []).length;
    if (result.error === '' && result.finished && result.records.length < given)
      result.error = '博弈已在第' + result.round + '轮结束，多余的选择：' + (given - result.records.length) + '个';
    return result;
  }

  function runCore(d, choices) {
    const st = { finished: false, won: false, error: '', round: 0, player: -1, lives: [], points: [], alive: [], prompt: '', transcript: [], pending: [], records: [] };
    for (const issue of check(d)) if (issue.severity === 'error') { st.error = issue.text; return st; }
    const n = d.seats.length;
    for (let i = 0; i < n; i++) { st.lives.push(d.lives); st.points.push(0); st.alive.push(true); if (d.seats[i].persona === 'player') st.player = i; }
    const rng = { s: d.seed % 2147483647 };
    if (rng.s <= 0) rng.s = 1;
    const input = choices || [];
    let used = 0, eliminatedTotal = 0, prevTarget = -1, playerBetrayed = false, ruleStage = 0, duel = false;
    const exposed = new Array(n).fill(false), meet = new Array(n).fill(0), lastAgainst = new Array(n).fill('split');
    st.transcript.push(intro(d));

    for (let r = 0; r < d.rounds; r++) {
      st.pending.length = 0;
      st.pending.push('第' + (r + 1) + '/' + d.rounds + '轮' + (r === d.rounds - 1 ? '（最后一轮）' : '') + '　存活：' + aliveSummary(d, st));
      let marks = null, opp = -1, meet0 = 0;
      if (d.mode === 'beauty') {
        const active = [];
        if (ruleStage >= 1) active.push('同数字无效');
        if (ruleStage >= 2) active.push('命中目标时其他人各扣两条命');
        if (duel) active.push('只剩两人时有人选0则选100者获胜');
        st.pending.push('追加规则：' + (active.length === 0 ? '无' : active.join('；')));
        st.prompt = '选择 0–100 的整数；最接近“全体平均×' + d.ratio + '%”者本轮不扣命';
      } else if (d.mode === 'mark') {
        marks = new Array(n).fill(0);
        for (let i = 0; i < n; i++) if (st.alive[i]) marks[i] = rand(rng, 4);
        for (const k of aliveNpcs(d, st)) st.pending.push(d.seats[k].name + '说你的标记是' + Suits[claim(d, marks, k, st.player, r)]);
        st.prompt = '宣告你自己的标记：♠ ♥ ♦ ♣（答错失去一条命）';
      } else {
        const npcs = aliveNpcs(d, st);
        opp = npcs[r % npcs.length]; meet0 = meet[opp];
        st.pending.push('本轮对手：' + d.seats[opp].name + '　对方说：“' + promise(d, opp, meet0, playerBetrayed) + '”');
        st.pending.push('你：' + st.points[st.player] + '分 ' + st.lives[st.player] + '命　目标：' + d.goal + '分');
        st.prompt = '选择：分 或 偷（双分各+1；偷对分者+2且对方失去一命；双偷什么都得不到）';
      }

      if (used >= input.length) return st;
      const raw = typeof input[used] === 'string' ? input[used] : ''; used++;
      // S252：picks/marks/target/opponent/winners 供博弈桌逐座位翻牌（与 C# RoundRecord 同语义）。
      const rec = { round: r + 1, choice: raw, livesBefore: st.lives[st.player], livesAfter: 0, pointsAfter: 0, aliveAfter: 0, correct: true, informedAnswer: '', firstClaim: '', eliminated: [],
        picks: new Array(n).fill(''), marks: new Array(n).fill(''), target: -1, opponent: -1, winners: [] };
      st.transcript.push('—— 第' + (r + 1) + '轮 ——');
      for (const line of st.pending) st.transcript.push(line);

      if (d.mode === 'beauty') {
        const pick = parseNumber(raw);
        if (pick < 0) return bad(st, r, raw);
        prevTarget = beautyRound(d, st, pick, rng, ruleStage, duel, prevTarget, rec);
      } else if (d.mode === 'mark') {
        const suit = parseSuit(raw);
        if (suit < 0) return bad(st, r, raw);
        markRound(d, st, marks, suit, r, exposed, rec);
      } else {
        const move = parseMove(raw);
        if (move === '') return bad(st, r, raw);
        const theirs = splitRound(d, st, opp, meet0, move, r, playerBetrayed, lastAgainst, rec);
        meet[opp]++; lastAgainst[opp] = move; if (move === 'steal' && theirs === 'split') playerBetrayed = true;
      }

      for (let i = 0; i < n; i++)
        if (st.alive[i] && st.lives[i] <= 0) { st.alive[i] = false; eliminatedTotal++; rec.eliminated.push(d.seats[i].name); st.transcript.push(d.seats[i].name + '出局'); }
      // 每轮最多追加一条；只剩两人时优先宣布决斗规则（与 C# 同语义）。
      if (d.mode === 'beauty' && d.escalate === 1 && st.alive[st.player] && aliveNpcs(d, st).length > 0 && r < d.rounds - 1) {
        if (!duel && aliveNpcs(d, st).length === 1) {
          duel = true;
          st.transcript.push('【新规则】从下一轮起：只剩两人时，若有人选0，选100的人获胜');
        } else if (eliminatedTotal > ruleStage && ruleStage < 2) {
          ruleStage++;
          st.transcript.push(ruleStage === 1 ? '【新规则】从下一轮起：两人以上选同一个数字，该数字无效并扣命' : '【新规则】从下一轮起：有人正好命中目标时，其他人各扣两条命');
        }
      }
      rec.livesAfter = st.lives[st.player]; rec.pointsAfter = st.points[st.player];
      rec.aliveAfter = aliveNpcs(d, st).length + (st.alive[st.player] ? 1 : 0);
      st.records.push(rec); st.round = r + 1;

      const lastRound = r === d.rounds - 1;
      if (!st.alive[st.player]) return finish(st, false, '你失去了全部的命');
      if (aliveNpcs(d, st).length === 0) {
        if (d.mode === 'split' && st.points[st.player] < d.goal) return finish(st, false, '对手都已出局，但你的分数没有达到目标');
        return finish(st, true, '你是最后的存活者');
      }
      if (lastRound) {
        if (d.mode === 'split' && st.points[st.player] < d.goal) return finish(st, false, '时间到，分数只有' + st.points[st.player] + '，未达到' + d.goal);
        return finish(st, true, '坚持到了最后一轮');
      }
    }
    return finish(st, st.alive[st.player], '轮数结束');
  }

  function beautyRound(d, st, pick, rng, ruleStage, duel, prevTarget, rec) {
    const n = d.seats.length, R = d.ratio, order = [];
    for (let i = 0; i < n; i++) if (st.alive[i]) order.push(i);
    const c = new Array(n).fill(0);
    for (const i of order) {
      if (i === st.player) { c[i] = pick; continue; }
      const j = rand(rng, 10), p = d.seats[i].persona;
      let v;
      // 只剩两人时“更小的数一定赢”，除直觉型外都会写 0（与 C# 同语义）。
      if (order.length === 2 && p !== 'naive') v = 0;
      else if (p === 'naive') v = 40 + 2 * j;
      else if (p === 'deep') v = div(50 * R * R, 10000) + div(j, 3);
      else if (p === 'adaptive') v = prevTarget < 0 ? 30 + j : prevTarget;
      else v = div(50 * R, 100) + j - 4;
      c[i] = Math.max(0, Math.min(100, v));
    }
    let sum = 0; for (const i of order) sum += c[i];
    const cnt = order.length, dupRule = ruleStage >= 1, exactRule = ruleStage >= 2, zeroRule = duel && order.length === 2;
    const invalid = new Array(n).fill(false);
    if (dupRule) for (const i of order) for (const k of order) if (k !== i && c[k] === c[i]) invalid[i] = true;
    const dist = v => Math.abs(v * 100 * cnt - sum * R);
    let best = Infinity; for (const i of order) if (!invalid[i]) best = Math.min(best, dist(c[i]));
    let winners = order.filter(i => !invalid[i] && dist(c[i]) === best);
    let zeroHit = false;
    if (zeroRule && !invalid[order[0]] && !invalid[order[1]]) {
      const a = order[0], b = order[1];
      const hundred = c[a] === 100 && c[b] === 0 ? a : c[b] === 100 && c[a] === 0 ? b : -1;
      if (hundred >= 0) { winners = [hundred]; zeroHit = true; }
    }
    const exactHit = !zeroHit && exactRule && winners.length > 0 && best === 0;
    const target = div(2 * sum * R + 100 * cnt, 200 * cnt);
    for (const i of order) rec.picks[i] = String(c[i]);
    rec.target = target;
    rec.winners = winners.slice();
    st.transcript.push(order.map(i => d.seats[i].name + '选' + c[i]).join('，'));
    st.transcript.push('平均×' + R + '% ≈ ' + target);
    const inv = order.filter(i => invalid[i]).map(i => d.seats[i].name);
    if (inv.length) st.transcript.push('重复数字无效：' + inv.join('、'));
    const win = winners.map(i => d.seats[i].name);
    st.transcript.push('最接近：' + (win.length ? win.join('、') : '无人'));
    if (zeroHit) st.transcript.push('0 对 100：选100的人获胜');
    if (exactHit) st.transcript.push('有人正好命中！其他人各扣两条命');
    for (const i of order) { if (winners.includes(i)) continue; st.lives[i] -= invalid[i] ? 1 : (exactHit ? 2 : 1); }
    st.transcript.push('命数：' + order.map(i => d.seats[i].name + Math.max(0, st.lives[i]) + '命').join('，'));
    rec.correct = winners.includes(st.player);
    return target;
  }

  function markRound(d, st, marks, suit, r, exposed, rec) {
    const n = d.seats.length, me = st.player;
    rec.informedAnswer = Suits[majority(d, st, marks, me, r, exposed, true)];
    rec.firstClaim = Suits[firstClaim(d, st, marks, me, r)];
    const declared = new Array(n).fill(0);
    for (let i = 0; i < n; i++) {
      if (!st.alive[i]) continue;
      if (i === me) declared[i] = suit;
      else if (d.seats[i].persona === 'liar') declared[i] = marks[i];
      else if (d.seats[i].persona === 'naive') declared[i] = firstClaim(d, st, marks, i, r);
      else declared[i] = majority(d, st, marks, i, r, exposed, true);
      rec.picks[i] = Suits[declared[i]];
      rec.marks[i] = Suits[marks[i]];
    }
    st.transcript.push('你宣告' + Suits[suit] + '，真实是' + Suits[marks[me]] + (suit === marks[me] ? '：答对' : '：答错，失去一条命'));
    rec.correct = suit === marks[me];
    for (let i = 0; i < n; i++) {
      if (!st.alive[i]) continue;
      if (declared[i] !== marks[i]) st.lives[i] -= 1;
      if (i !== me) st.transcript.push(d.seats[i].name + '宣告' + Suits[declared[i]] + '，真实是' + Suits[marks[i]] + (declared[i] === marks[i] ? '' : '：答错'));
    }
    for (let k = 0; k < n; k++) {
      if (!st.alive[k] || k === me) continue;
      for (let t = 0; t < n; t++) {
        if (!st.alive[t] || t === k) continue;
        const c = claim(d, marks, k, t, r);
        if (c !== marks[t]) { st.transcript.push('公开记录：' + d.seats[k].name + '对' + (t === me ? '你' : d.seats[t].name) + '说' + Suits[c] + '，实际是' + Suits[marks[t]]); exposed[k] = true; }
      }
    }
  }

  function splitRound(d, st, opp, meet0, move, r, playerBetrayed, lastAgainst, rec) {
    const p = d.seats[opp].persona;
    let theirs;
    if (p === 'liar') theirs = 'steal';
    else if (p === 'mirror' || p === 'adaptive') theirs = meet0 === 0 ? 'split' : lastAgainst[opp];
    else if (p === 'reasoner') theirs = playerBetrayed ? 'steal' : 'split';
    else if (p === 'deep') theirs = (r === d.rounds - 1 || playerBetrayed) ? 'steal' : 'split';
    else theirs = 'split';
    const me = st.player, name = d.seats[opp].name;
    rec.opponent = opp;
    rec.picks[me] = moveName(move);
    rec.picks[opp] = moveName(theirs);
    st.transcript.push('你选' + moveName(move) + '，' + name + '选' + moveName(theirs));
    if (move === 'split' && theirs === 'split') { st.points[me] += 1; st.points[opp] += 1; st.transcript.push('双方各得1分'); }
    else if (move === 'steal' && theirs === 'split') { st.points[me] += 2; st.lives[opp] -= 1; st.transcript.push('你得2分，' + name + '失去一条命'); }
    else if (move === 'split' && theirs === 'steal') { st.points[opp] += 2; st.lives[me] -= 1; st.transcript.push(name + '得2分，你失去一条命'); }
    else st.transcript.push('两人都偷，什么都没得到');
    st.transcript.push('你：' + st.points[me] + '分 ' + Math.max(0, st.lives[me]) + '命；' + name + '：' + st.points[opp] + '分 ' + Math.max(0, st.lives[opp]) + '命');
    if (move === 'steal' && theirs === 'split' && !playerBetrayed) st.transcript.push('所有人都看到了：你偷了一个选分的人');
    rec.correct = theirs === 'split';
    return theirs;
  }

  function claim(d, marks, speaker, target, r) {
    if (d.seats[speaker].persona !== 'liar') return marks[target];
    if (d.seats[target].persona === 'player' && r === 0) return marks[target];
    return (marks[target] + 1) % 4;
  }
  function firstClaim(d, st, marks, target, r) {
    for (let k = 0; k < d.seats.length; k++) if (st.alive[k] && k !== target) return k === st.player ? marks[target] : claim(d, marks, k, target, r);
    return 0;
  }
  function majority(d, st, marks, target, r, exposed, excludeExposed) {
    const counts = [0, 0, 0, 0], firstSeat = [Infinity, Infinity, Infinity, Infinity];
    let any = false;
    for (let pass = 0; pass < 2 && !any; pass++)
      for (let k = 0; k < d.seats.length; k++) {
        if (!st.alive[k] || k === target) continue;
        if (pass === 0 && excludeExposed && exposed[k]) continue;
        const c = k === st.player ? marks[target] : claim(d, marks, k, target, r);
        counts[c]++; if (k < firstSeat[c]) firstSeat[c] = k; any = true;
      }
    let bestSuit = 0;
    for (let s = 1; s < 4; s++) if (counts[s] > counts[bestSuit] || (counts[s] === counts[bestSuit] && firstSeat[s] < firstSeat[bestSuit])) bestSuit = s;
    return bestSuit;
  }
  function promise(d, opp, meet0, playerBetrayed) {
    const p = d.seats[opp].persona;
    if (p === 'mirror' || p === 'adaptive') return meet0 === 0 ? '我会选分' : '你上次对我选什么，我这次就选什么';
    if (p === 'liar') return '我保证选分，相信我';
    if ((p === 'reasoner' || p === 'deep') && playerBetrayed) return '你偷过选分的人，我不会再信你';
    return '我会选分';
  }
  function rand(rng, range) { rng.s = (rng.s * 48271) % 2147483647; return rng.s % range; }
  function aliveNpcs(d, st) { const list = []; for (let i = 0; i < d.seats.length; i++) if (st.alive[i] && i !== st.player) list.push(i); return list; }
  function aliveSummary(d, st) { const parts = []; for (let i = 0; i < d.seats.length; i++) if (st.alive[i]) parts.push((i === st.player ? '你' : d.seats[i].name) + '(' + st.lives[i] + '命)'); return parts.join('、'); }
  function intro(d) { const name = d.mode === 'beauty' ? '数字猜心' : d.mode === 'mark' ? '看不见的标记' : '分还是偷'; return '【' + name + '】' + d.seats.length + '人参加，' + d.rounds + '轮，每人' + d.lives + '条命'; }
  function bad(st, r, raw) { st.error = '第' + (r + 1) + '轮选择无效：' + safe(raw); return st; }
  function finish(st, won, reason) { st.finished = true; st.won = won; st.pending.length = 0; st.prompt = ''; st.transcript.push((won ? '【通关】' : '【失败】') + reason); return st; }
  function parseNumber(raw) { if (typeof raw !== 'string' || !/^[0-9]{1,3}$/.test(raw)) return -1; const v = parseInt(raw, 10); return v <= 100 ? v : -1; }
  const SuitIndex = new Map([['♠', 0], ['S', 0], ['♥', 1], ['H', 1], ['♦', 2], ['D', 2], ['♣', 3], ['C', 3]]);
  function parseSuit(raw) { return SuitIndex.has(raw) ? SuitIndex.get(raw) : -1; }
  function parseMove(raw) { if (raw === 'split' || raw === '分') return 'split'; if (raw === 'steal' || raw === '偷') return 'steal'; return ''; }
  function moveName(m) { return m === 'steal' ? '偷' : '分'; }

  return { MinSeats, MaxSeats, MaxRounds, MaxLives, MaxParseSeats, Modes, Personas, Suits, check, run, isMode, isPersona };
})();
if (typeof module !== 'undefined') module.exports = BorderlandGame;
