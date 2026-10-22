// S253：网页试玩画面纯逻辑（Tools/LevelStudioWeb/play-model.js）↔ C# WorldParkSceneModel / WorldParkTableModel / WorldParkStageCues 逐项对比。
// 在主世界“空白之城”上自动走三条不同旅程（全调查 / 不调查 / 混合选择），每个动作前比较字幕卡、博弈桌（多个时刻）、演出提示。
const fs = require('fs'), path = require('path'), cp = require('child_process');
const repo = path.join(__dirname, '..', '..');
const W = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'worldpark.js'));
const BG = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'borderland.js'));
const M = require(path.join(repo, 'Tools', 'LevelStudioWeb', 'play-model.js'));
const dir = path.join(repo, '.work', 'worldpark-verify'); fs.mkdirSync(dir, { recursive: true });
const worldText = fs.readFileSync(path.join(repo, 'Assets', 'Resources', 'WorldPark', 'Borderland.worldpark.json'), 'utf8').replace(/^\uFEFF/, '');
const TIMES = [-1, 0, 0.3, 0.5, 0.96, 1.5, 2.2, 3.0, 9];

function apply(s, a) {
  if (a === 'next') return s.Continue();
  if (a === 'win' || a === 'lose') return s.ResolveRoom(a === 'win');
  if (a === 'explore-win' || a === 'explore-lose') return s.ResolveExplore(a === 'explore-win');
  if (a.startsWith('observe:')) return s.ObserveClue(a.slice(8));
  if (a.startsWith('game:')) return s.ResolveGame(a.slice(5).split('|'));
  return s.Travel(a.slice(7));
}
// 自动旅程：policy = {observe, exploreWin, picks:{beauty,mark,split}}
function journey(policy) {
  const s = W.start(W.parse(worldText)), actions = [], visited = new Set([s.venueId]);
  for (let guard = 0; guard < 200; guard++) {
    const n = s.Current; let a = null;
    if (n.kind === 'story' || n.kind === 'reveal') a = 'next';
    else if (n.kind === 'room') a = 'win';
    else if (n.kind === 'explore') {
      if (policy.observe) for (const q of n.points || []) if (q && q.kind === 'clue') { const c = (s.Venue.clues || []).find(x => x.id === q.clue); if (c && c.trigger === 'interact' && !s.found.has(s.venueId + '/' + c.id)) { actions.push('observe:' + c.id); apply(s, 'observe:' + c.id); } }
      a = policy.exploreWin ? 'explore-win' : 'explore-lose';
    } else if (n.kind === 'game') {
      const ch = [];
      const score = st => st.error ? -1e9 : st.finished ? (st.won ? 1e6 : -1e6) : (st.alive[st.player] ? st.lives[st.player] * 1000 + st.points[st.player] : -1e6);
      const options = n.game.mode === 'beauty' ? Array.from({ length: 101 }, (_, i) => String(i)) : n.game.mode === 'mark' ? ['♠', '♥', '♦', '♣'] : ['分', '偷'];
      for (let r = 0; r < 40; r++) {
        let pick = policy.picks[n.game.mode];
        if (policy.greedy) { let best = -Infinity; for (const o of options) { const sc = score(BG.run(n.game, ch.concat([o]))); if (sc > best) { best = sc; pick = o; } } }
        ch.push(pick); const st = BG.run(n.game, ch); if (st.error || st.finished) break;
      }
      if (policy.search && n.game.mode !== 'beauty') {
        const dfs = pre => { const st = BG.run(n.game, pre); if (st.error) return null; if (st.finished) return st.won ? pre : null; if (pre.length >= 8) return null; for (const o of options) { const r = dfs(pre.concat([o])); if (r) return r; } return null; };
        const win = dfs([]); if (win) { ch.length = 0; ch.push(...win); }
      }
      a = 'game:' + ch.join('|');
    } else if (n.kind === 'ending') {
      const doors = (s.pack.links || []).filter(l => l.from === s.venueId && !visited.has(l.to) && s.CanTravel(l.to)), door = policy.last ? doors[doors.length - 1] : doors[Math.min(policy.door || 0, doors.length - 1)];
      if (!door) { actions.push('end'); break; }
      a = 'travel:' + door.to; visited.add(door.to);
    }
    actions.push(a); apply(s, a);
  }
  return actions;
}
const cases = [
  { name: '全调查·通过·必胜博弈·走第一扇门', json: worldText, times: TIMES, actions: journey({ observe: true, exploreWin: true, greedy: true, search: true, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '全调查·通过·必胜博弈·走最后一扇门', json: worldText, times: TIMES, actions: journey({ observe: true, exploreWin: true, greedy: true, search: true, last: true, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '全调查·通过·必胜博弈·走第二扇门', json: worldText, times: TIMES, actions: journey({ observe: true, exploreWin: true, greedy: true, search: true, door: 1, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '全调查·通过·贪心博弈', json: worldText, times: TIMES, actions: journey({ observe: true, exploreWin: true, greedy: true, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '不调查·通过·贪心博弈', json: worldText, times: TIMES, actions: journey({ observe: false, exploreWin: true, greedy: true, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '全调查·通过·猜20/♠/分', json: worldText, times: TIMES, actions: journey({ observe: true, exploreWin: true, picks: { beauty: '20', mark: '♠', split: '分' } }) },
  { name: '不调查·失败', json: worldText, times: TIMES, actions: journey({ observe: false, exploreWin: false, picks: { beauty: '50', mark: '♣', split: '偷' } }) },
  { name: '不调查·通过·猜0/♥/偷', json: worldText, times: TIMES, actions: journey({ observe: false, exploreWin: true, picks: { beauty: '0', mark: '♥', split: '偷' } }) }
];
// 'end' 只是 JS 侧的旅程结束标记：C# 侧在它之前输出最后一帧再停止（Apply 会失败，失败文字两边都不比较）
fs.writeFileSync(path.join(dir, 'views-cases.json'), JSON.stringify(cases));
const proj = path.join(repo, 'Tools', 'WorldParkVerify', 'Snapshot', 'WorldParkSnapshot.csproj');
cp.execFileSync('dotnet', ['run', '--project', proj, '--', 'views', path.join(dir, 'views-cases.json'), path.join(dir, 'csharp-views.json')], { stdio: 'inherit' });
const cs = JSON.parse(fs.readFileSync(path.join(dir, 'csharp-views.json'), 'utf8'));

function jsViews(c) {
  const s = W.start(W.parse(c.json)), frames = [];
  for (const action of c.actions) {
    const v = s.Venue, n = s.Current;
    const f = { action, venue: s.venueId, node: s.nodeId, entrance: M.Cues.Entrance(v) };
    if (['story', 'reveal', 'ending', 'room'].includes(n.kind)) { const sc = M.Scene.Build(s, null); f.scene = sc; f.ready = M.Scene.ReadyAt(sc); f.visible = [0, 0.4, 1.0, 3.3].map(x => M.Scene.Visible(sc.text, x)); f.ending = M.Cues.Ending(v, n); }
    if (action.startsWith('game:')) {
      const ch = action.slice(5).split('|'), tables = [], elim = [];
      tables.push(M.Table.Build(n.game, null, BG.run(n.game, []), -1));
      for (let k = 1; k <= ch.length; k++) {
        const before = BG.run(n.game, ch.slice(0, k - 1)), st = BG.run(n.game, ch.slice(0, k));
        for (const x of c.times) tables.push(M.Table.Build(n.game, before, st, x));
        elim.push(M.Cues.NewlyEliminated(n.game, before, st));
        if (st.finished) f.activity = M.Cues.Activity(v, st.won, st.won ? '博弈通过' : '你输掉了这场博弈');
      }
      f.tables = tables; f.eliminated = elim;
    }
    if (action === 'explore-win' || action === 'explore-lose') f.activity = M.Cues.Activity(v, action === 'explore-win', '现场结果');
    frames.push(f);
    if (action === 'end') break;
    apply(s, action);
  }
  const beeps = []; for (let r = 12.0; r > -0.5; r -= 0.37) beeps.push(M.Cues.CountdownBeep(r, r - 0.37));
  return { frames, beeps };
}
let diffs = 0, compared = 0; const report = [];
function cmp(where, a, b) {
  compared++;
  if (a === undefined) a = null; if (b === undefined) b = null;
  if (typeof a === 'number' && typeof b === 'number') { if (Math.abs(a - b) > 1e-9 && !(a > 1e300 && b > 1e300)) { diffs++; report.push(`${where}: C# ${a} ≠ JS ${b}`); } return; }
  if (Array.isArray(a) && Array.isArray(b)) { if (a.length !== b.length) { diffs++; report.push(`${where}: 长度 C# ${a.length} ≠ JS ${b.length}`); return; } for (let i = 0; i < a.length; i++) cmp(where + '[' + i + ']', a[i], b[i]); return; }
  if (a && b && typeof a === 'object' && typeof b === 'object') { for (const k of new Set([...Object.keys(a), ...Object.keys(b)])) cmp(where + '.' + k, a[k], b[k]); return; }
  if (a !== b) { diffs++; report.push(`${where}: C# ${JSON.stringify(a)} ≠ JS ${JSON.stringify(b)}`); }
}
const lines = [];
for (let i = 0; i < cases.length; i++) {
  const j = jsViews(cases[i]), c = cs[i];
  const failOk = c.error === '' || c.error.startsWith('end:');
  if (!failOk) { diffs++; report.push(cases[i].name + ': C# 流程失败 ' + c.error); }
  cmp(cases[i].name + '.frames', c.frames, j.frames); cmp(cases[i].name + '.beeps', c.beeps, j.beeps);
  const scenes = j.frames.filter(f => f.scene), twists = scenes.reduce((a, f) => a + f.scene.twists.length, 0), tables = j.frames.reduce((a, f) => a + (f.tables ? f.tables.length : 0), 0);
  lines.push(`${cases[i].name}：${cases[i].actions.length} 个动作，${scenes.length} 张字幕卡（反转卡 ${twists}），${tables} 个博弈桌画面，经过 ${[...new Set(j.frames.map(f => f.venue))].join('→')}`);
}
console.log(lines.join('\n'));
if (report.length) console.log(report.slice(0, 25).join('\n'));
console.log(`试玩画面: ${cases.length} 条旅程, 比较 ${compared} 项, 差异: ${diffs}`);
process.exit(diffs ? 1 : 0);
