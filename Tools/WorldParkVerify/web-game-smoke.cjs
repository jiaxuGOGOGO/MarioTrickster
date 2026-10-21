// S251 冒烟：用桩 DOM 加载真实网页脚本，走到 ♦5 博弈阶段，经彩排面板逐轮选择并提交结果。
const fs = require('fs'), vm = require('vm'), path = require('path');
const root = path.resolve(__dirname, '..', '..');
const web = f => fs.readFileSync(path.join(root, 'Tools', 'LevelStudioWeb', f), 'utf8').replace(/if \(typeof module[^\n]*\n?/g, '');
const els = {};
function stub(id) {
  return els[id] || (els[id] = { id, innerHTML: '', textContent: '', value: '', children: [], style: {}, dataset: {},
    appendChild(c) { this.children.push(c); return c; }, set onclick(f) { this._click = f; }, get onclick() { return this._click; } });
}
const document = { getElementById: stub, querySelector: () => null, querySelectorAll: () => [], createElement: tag => ({ tag, children: [], style: {}, dataset: {}, appendChild(c) { this.children.push(c); } }), addEventListener() {} };
// S254：worldpark-app.js 用 setTimeout/clearTimeout 做“停笔后体检 / 自动写进 Unity”的延时（浏览器自带，这里补上）。
const ctx = { console, document, window: {}, localStorage: { getItem: () => null, setItem() {} }, confirm: () => true, alert() {}, setTimeout, clearTimeout, structuredClone };
ctx.window = ctx; vm.createContext(ctx);
const sample = JSON.parse(fs.readFileSync(path.join(root, 'Assets', 'Resources', 'WorldPark', 'Borderland.worldpark.json'), 'utf8').replace(/^\uFEFF/, ''));
vm.runInContext('const WORLD_PARK_SAMPLE=' + JSON.stringify(sample) + ';', ctx);
// 地形表：浏览器里由 build.py 内联为 OW_TILES；这里用 Node 版探索内核读出同一张表再注入。
vm.runInContext('const OW_TILES=' + JSON.stringify(require(path.join(root, 'Tools', 'LevelStudioWeb', 'explore.js')).TILES()) + ';', ctx);
for (const f of ['borderland.js', 'worldpark.js', 'explore.js', 'venue-check.js', 'worldpark-history.js', 'worldpark-app.js']) vm.runInContext(web(f), ctx, { filename: f });
const run = code => vm.runInContext(code, ctx);
let fail = 0; const ok = (c, m) => { console.log((c ? 'PASS ' : 'FAIL ') + m); if (!c) fail++; };
// S254 会场体检面板：当前会场（入口 ♣3）应给出红 0 的体检结果。
run('wpVenueCheckRender()');
ok(/红 0/.test(stub('wpVenueCheckTitle').textContent) && /owiss Good/.test(stub('wpVenueCheck').innerHTML), '会场体检面板：' + stub('wpVenueCheckTitle').textContent);
ok(/模板：/.test(stub('wpTemplate').innerHTML), '会场模板下拉列出内置会场');
// 直接从 ♦5 入场：与世界入口相同的会话 API，只是把入口换成 v_number。
run(`WPUI.pack=WorldPark.parse(JSON.stringify(WORLD_PARK_SAMPLE));const p2=JSON.parse(WorldPark.toJson(WPUI.pack));p2.entryVenue='v_number';WPUI.session=WorldPark.start(WorldPark.parse(JSON.stringify(p2)));WPUI.session.Continue();`);
ok(run('WPUI.session.Current.kind') === 'game', '彩排会话到达博弈阶段：' + run('WPUI.session.Current.name'));
run('wpRehearsal()');
const controls = () => stub('wpPlayerActions').children;
ok(controls().some(c => c.tag === 'input') && controls().some(c => c.textContent === '写下这个数字'), '数字猜心面板出现数字输入框与“写下这个数字”按钮');
ok(stub('wpPlayer').textContent.includes('追加规则：无'), '面板显示公开信息（追加规则）');
run(`wpStep('game:abc')`);
ok(run('WPUI.gameChoices.length') === 0, '非法输入被拒绝，不计入选择');
let guard = 0;
while (!run('BorderlandGame.run(WPUI.session.Current.game,WPUI.gameChoices).finished') && guard++ < 30) {
  const st = run('BorderlandGame.run(WPUI.session.Current.game,WPUI.gameChoices)');
  const t = st.transcript.filter(l => l.startsWith('平均')).pop();
  const pick = t ? Math.max(0, Math.floor(parseInt(t.split('≈ ')[1]) * 0.8) - 1) : 31;
  const input = controls().find(c => c.tag === 'input'); input.value = String(pick);
  controls().find(c => c.textContent === '写下这个数字').onclick();
}
const final = run('BorderlandGame.run(WPUI.session.Current.game,WPUI.gameChoices)');
ok(final.finished, '逐轮点击打完整局（' + final.round + ' 轮，' + (final.won ? '通过' : '失败') + '）');
const resolve = controls().find(c => (c.textContent || '').startsWith('提交博弈结果'));
ok(!!resolve, '整局结束后出现“提交博弈结果”按钮');
resolve.onclick();
ok(['reveal', 'ending'].includes(run('WPUI.session.Current.kind')), '提交后进入分支：' + run('WPUI.session.Current.kind') + ' / ' + run('WPUI.session.Current.name'));
ok(run('WPUI.session.log.some(l=>l.startsWith("博弈结果："))'), '会话日志记录真实博弈结果');
// 标记与分偷两种按钮
for (const [venue, label] of [['v_mark', '宣告 ♠'], ['v_split', '分']]) {
  run(`{const p3=JSON.parse(WorldPark.toJson(WPUI.pack));p3.entryVenue='${venue}';WPUI.session=WorldPark.start(WorldPark.parse(JSON.stringify(p3)));WPUI.session.Continue();wpRehearsal();}`);
  ok(controls().some(c => c.textContent === label), venue + ' 面板出现“' + label + '”按钮');
}
// 博弈参数 JSON 编辑：坏 JSON 不改原参数
run(`WPUI.vi=WPUI.pack.venues.findIndex(v=>v.id==='v_number');WPUI.ni=WPUI.pack.venues[WPUI.vi].nodes.findIndex(n=>n.kind==='game');`);
const before = run('JSON.stringify(wpN().game)');
run(`wpGameJson('{bad')`);
ok(run('JSON.stringify(wpN().game)') === before, '坏 JSON 不会改写博弈参数');
ok(run('wpGameForm(wpN())').includes('博弈参数检查通过'), '主世界博弈参数通过共享内核校验');
console.log(fail ? 'web game smoke FAIL ' + fail : 'web game smoke PASS');
process.exit(fail ? 1 : 0);
