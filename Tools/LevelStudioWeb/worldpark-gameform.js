'use strict';
// S258 博弈参数表单的纯逻辑。字段、中文标签、取值范围与 Unity 世界工坊 DrawGameFields（WorldParkWindow.cs）一致：
// 玩法 / 轮数 1–20 / 每人命数 1–20 / 目标比例 1–99（只在数字猜心）/ 有人出局后逐轮追加规则（只在数字猜心）/
// 通关所需分数 1–40（只在分还是偷）/ 随机种子 / 参加者 2–8 人（新参加者 id 用 UniqueId(…,"npc")，名字“新参加者”，人格 reasoner）。
// 这里只把表单操作变成新的 game 对象，不判断规则对错：对错只由 BorderlandGame.check 决定（与 C# 同语义的唯一规则来源）。
const WPGameForm = (() => {
  const Modes = [['beauty', '数字猜心'], ['mark', '看不见的标记'], ['split', '分还是偷']];
  const Personas = [['player', '玩家本人'], ['naive', '直觉型'], ['reasoner', '推一层'], ['deep', '深推'], ['adaptive', '跟随者'], ['liar', '说谎者'], ['loyal', '老实人'], ['mirror', '以牙还牙']];
  const Ranges = { rounds: [1, 20], lives: [1, 20], ratio: [1, 99], goal: [1, 40] };
  const Labels = { mode: '玩法', rounds: '轮数', lives: '每人命数', ratio: '目标比例（平均×%）', escalate: '有人出局后逐轮追加规则', goal: '通关所需分数', seed: '随机种子（同种子=同结果）' };
  // 玩法一句话说明：取自 borderland.js 每轮给玩家的提示原文。
  const ModeRule = {
    beauty: '每轮报 0–100 的整数，最接近“全体平均×比例”的人本轮不扣命。',
    mark: '每轮宣告自己背后的标记（♠ ♥ ♦ ♣），答错失去一条命。',
    split: '每轮选“分”或“偷”：双分各 +1；偷对分者 +2 且对方失去一命；双偷都得不到。'
  };
  const MinSeats = 2, MaxSeats = 8;
  const clone = g => JSON.parse(JSON.stringify(g || {}));
  const int = v => { const n = Number(v); return Number.isFinite(n) ? Math.trunc(n) : 0; };
  const clamp = (field, v) => Math.min(Ranges[field][1], Math.max(Ranges[field][0], v));
  function uniqueId(ids, prefix) { const used = new Set(ids); let id = prefix, i = 2; while (used.has(id)) id = prefix + '_' + i++; return id; }
  // 当前玩法需要显示的字段（渐进披露：与本玩法无关的字段不显示，与 Unity 相同）。
  function fields(mode) { return ['rounds', 'lives'].concat(mode === 'beauty' ? ['ratio', 'escalate'] : mode === 'split' ? ['goal'] : [], ['seed']); }
  function set(game, field, value) {
    const d = clone(game);
    if (field === 'mode') {
      if (!Modes.some(m => m[0] === value)) return d;
      d.mode = value;
      // 切换玩法时补上该玩法必需、但原来无效的参数（Unity 的滑条也会把无效值拉回范围内）。
      if (value === 'beauty' && !(d.ratio >= 1 && d.ratio <= 99)) d.ratio = 80;
      if (value === 'split' && !(d.goal >= 1 && d.goal <= 40)) d.goal = 3;
      if (d.escalate !== 0 && d.escalate !== 1) d.escalate = 0;
      return d;
    }
    if (field === 'escalate') { d.escalate = value === true || value === 1 || value === '1' || value === 'on' ? 1 : 0; return d; }
    if (field === 'seed') { d.seed = Math.max(0, int(value)); return d; }
    if (Object.prototype.hasOwnProperty.call(Ranges, field)) { d[field] = clamp(field, int(value)); return d; }
    return d;
  }
  function seatSet(game, index, field, value) {
    const d = clone(game), seat = (d.seats || [])[index];
    if (!seat) return d;
    if (field === 'persona') { if (Personas.some(p => p[0] === value)) seat.persona = value; }
    else if (field === 'name') seat.name = String(value ?? '');
    else if (field === 'id') seat.id = String(value ?? '').trim();
    return d;
  }
  function seatAdd(game) {
    const d = clone(game);
    if (!Array.isArray(d.seats)) d.seats = [];
    if (d.seats.length >= MaxSeats) return d;
    d.seats.push({ id: uniqueId(d.seats.map(s => s && s.id), 'npc'), name: '新参加者', persona: 'reasoner' });
    return d;
  }
  // 只剩一位“玩家本人”时不允许删掉他：否则这局没有你。
  function canRemove(game, index) {
    const seats = (game && game.seats) || [], seat = seats[index];
    if (!seat || seats.length <= MinSeats) return false;
    return !(seat.persona === 'player' && seats.filter(s => s && s.persona === 'player').length === 1);
  }
  function seatRemove(game, index) { const d = clone(game); if (canRemove(game, index)) d.seats.splice(index, 1); return d; }
  function summary(game) {
    const g = game || {}, seats = (g.seats || []).length;
    const mode = (Modes.find(m => m[0] === g.mode) || [, '未选玩法'])[1];
    let s = `${mode} · ${seats} 人（含你）· ${g.rounds} 轮 · 每人 ${g.lives} 命`;
    if (g.mode === 'beauty') s += ` · 比例 ${g.ratio}%` + (g.escalate === 1 ? ' · 出局后加规则' : '');
    if (g.mode === 'split') s += ` · 目标 ${g.goal} 分`;
    return s;
  }
  // 校验结果的人话版：只换说法，不改判定。
  function explain(issue) {
    const map = {
      game_player_seat: '参加者里必须恰好有一位“玩家本人”（就是你）。',
      game_escalate_invalid: '“有人出局后逐轮追加规则”只能开或关。',
      game_seat_persona_invalid: '有参加者的性格未知，请在下拉框里重新选一个。',
      game_seat_id_invalid: '有参加者的编号为空或含斜杠（编号在“高级”里）。',
      game_seat_id_duplicate: '两位参加者的编号重复（编号在“高级”里）。'
    };
    return map[issue && issue.code] || (issue && issue.text) || '';
  }
  return { Modes, Personas, Ranges, Labels, ModeRule, MinSeats, MaxSeats, fields, set, seatSet, seatAdd, seatRemove, canRemove, summary, explain, uniqueId };
})();
if (typeof module !== 'undefined') module.exports = WPGameForm;
