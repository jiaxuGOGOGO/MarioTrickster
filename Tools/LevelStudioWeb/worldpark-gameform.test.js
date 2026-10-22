'use strict';
// S258 博弈参数表单纯逻辑测试：表单只产生新对象，对错仍由共享规则 BorderlandGame.check 判定。
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const F = require('./worldpark-gameform.js');
const BorderlandGame = require('./borderland.js');
let n = 0; const ok = (v, msg) => { assert.ok(v, msg); n++; }; const eq = (a, b, msg) => { assert.deepStrictEqual(a, b, msg); n++; };
const base = { mode:'beauty', rounds:6, lives:3, ratio:80, goal:0, seed:1, escalate:1, seats:[{ id:'me', name:'你', persona:'player' }, { id:'a', name:'对手', persona:'reasoner' }] };
const frozen = JSON.stringify(base);
// 与 Unity DrawGameFields 一致的字段和标签
eq(F.Modes.map(m => m[0]), ['beauty', 'mark', 'split'], '玩法顺序与 BorderlandGame.Modes 相同');
eq(F.Personas.map(p => p[0]), ['player', 'naive', 'reasoner', 'deep', 'adaptive', 'liar', 'loyal', 'mirror'], '人格顺序与 BorderlandGame.Personas 相同');
const cs = fs.readFileSync(path.join(__dirname, '../../Assets/Scripts/Editor/WorldParkWindow.cs'), 'utf8');
for (const [id, name] of F.Modes) ok(cs.includes(`"${id} ${name}"`), 'Unity 玩法标签一致：' + name);
for (const [id, name] of F.Personas) ok(cs.includes(`"${id} ${name}"`), 'Unity 人格标签一致：' + name);
ok(cs.includes('IntSlider("轮数", g.rounds, 1, 20)') && cs.includes('IntSlider("每人命数", g.lives, 1, 20)'), 'Unity 轮数/命数范围 1–20');
ok(cs.includes('g.ratio, 1, 99') && cs.includes('g.goal, 1, 40'), 'Unity 比例 1–99、目标分 1–40');
eq(F.Ranges, { rounds:[1, 20], lives:[1, 20], ratio:[1, 99], goal:[1, 40] }, '网页范围与 Unity 相同');
ok(cs.includes('UniqueId(g.seats.Select(s => s.id), "npc"), name = "新参加者", persona = "reasoner"'), 'Unity 新参加者默认值');
// 渐进披露：只显示本玩法用到的字段
eq(F.fields('beauty'), ['rounds', 'lives', 'ratio', 'escalate', 'seed'], '数字猜心显示比例与追加规则');
eq(F.fields('mark'), ['rounds', 'lives', 'seed'], '看不见的标记不显示比例和目标分');
eq(F.fields('split'), ['rounds', 'lives', 'goal', 'seed'], '分还是偷显示目标分');
// 数值：取整并拉回范围（与 Unity 滑条相同），不改原对象
eq(F.set(base, 'rounds', '25').rounds, 20, '轮数超上限拉回 20');
eq(F.set(base, 'rounds', '0').rounds, 1, '轮数低于下限拉回 1');
eq(F.set(base, 'lives', '4.7').lives, 4, '命数取整');
eq(F.set(base, 'ratio', '150').ratio, 99, '比例拉回 99');
eq(F.set(base, 'seed', '-3').seed, 0, '种子不小于 0');
eq(F.set(base, 'rounds', 'abc').rounds, 1, '非数字按 0 处理再拉回下限');
eq(F.set(base, 'escalate', false).escalate, 0, '追加规则可关');
eq(F.set(base, 'escalate', true).escalate, 1, '追加规则可开');
eq(F.set(base, 'unknown', 5), JSON.parse(frozen), '未知字段不改任何东西');
eq(JSON.stringify(base), frozen, '所有操作都不改原对象');
// 玩法切换：补上必需参数，结果通过共享规则
const split = F.set(Object.assign({}, base, { goal:0 }), 'mode', 'split');
eq(split.mode, 'split', '切到分还是偷'); eq(split.goal, 3, '目标分无效时补 3');
const beauty = F.set(Object.assign({}, base, { mode:'mark', ratio:0 }), 'mode', 'beauty');
eq(beauty.ratio, 80, '比例无效时补 80');
eq(F.set(base, 'mode', 'poker'), JSON.parse(frozen), '未知玩法不改');
ok(BorderlandGame.check(beauty).filter(i => i.severity === 'error').length === 0, '切回数字猜心后无错误');
// 参加者
const added = F.seatAdd(base);
eq(added.seats.length, 3, '加一位'); eq(added.seats[2], { id:'npc', name:'新参加者', persona:'reasoner' }, '新参加者默认值与 Unity 相同');
eq(F.seatAdd(added).seats[3].id, 'npc_2', '编号不重复（npc_2）');
let full = base; for (let i = 0; i < 10; i++) full = F.seatAdd(full);
eq(full.seats.length, 8, '最多 8 位');
ok(!F.canRemove(base, 0), '两人时不能删'); ok(!F.canRemove(added, 0), '唯一的玩家本人不能删'); ok(F.canRemove(added, 1), '对手可删');
eq(F.seatRemove(added, 0).seats.length, 3, '删唯一玩家被拒绝'); eq(F.seatRemove(added, 2).seats.length, 2, '删对手成功');
eq(F.seatSet(base, 1, 'persona', 'liar').seats[1].persona, 'liar', '改性格');
eq(F.seatSet(base, 1, 'persona', 'robot').seats[1].persona, 'reasoner', '未知性格不改');
eq(F.seatSet(base, 1, 'name', '狐').seats[1].name, '狐', '改名字');
eq(F.seatSet(base, 1, 'id', ' fox ').seats[1].id, 'fox', '编号去空格');
eq(F.seatSet(base, 9, 'name', 'x'), JSON.parse(frozen), '越界座位不改');
// 人话解释只换说法
const noPlayer = F.seatSet(base, 0, 'persona', 'naive');
const issue = BorderlandGame.check(noPlayer).find(i => i.code === 'game_player_seat');
ok(issue && /玩家本人/.test(F.explain(issue)), '缺玩家时说人话');
ok(F.explain({ code:'game_rounds_invalid', text:'博弈轮数必须是 1–20' }) === '博弈轮数必须是 1–20', '没有专门说法时用原文');
// 主世界三个博弈会场：表单读得懂、改一下再改回去完全一致
const pack = JSON.parse(fs.readFileSync(path.join(__dirname, '../../Assets/Resources/WorldPark/Borderland.worldpark.json'), 'utf8'));
const games = pack.venues.flatMap(v => v.nodes.filter(x => x.game).map(x => x.game));
eq(games.length, 3, '主世界有 3 个博弈阶段');
for (const g of games) {
  ok(F.Modes.some(m => m[0] === g.mode) && g.seats.every(s => F.Personas.some(p => p[0] === s.persona)), '表单认得 ' + g.mode);
  ok(/人（含你）/.test(F.summary(g)), '摘要可读 ' + g.mode);
  const back = F.set(F.set(g, 'rounds', g.rounds + 1), 'rounds', g.rounds);
  eq(back, g, '改了再改回与原数据一致 ' + g.mode);
}
console.log(`worldpark-gameform: ${n} assertions passed`);
