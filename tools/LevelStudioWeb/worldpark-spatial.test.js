'use strict';
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const Spatial = require('./worldpark-spatial.js');

const map = [
  '# 本行是说明，不能算作地图高度',
  'WWWWW',
  'W.T.W',
  'W...W',
  'WWWWW'
].join('\n');
const shape = Spatial.mapShape(map);
assert.strictEqual(shape.width, 5, '宽度应读取实际网格');
assert.strictEqual(shape.height, 4, '注释行不能算入地图高度');
assert.strictEqual(shape.at(2, 2), 'T', 'y=2 必须对应从顶部数第二行');
assert.strictEqual(shape.at(1, 1), '.', 'y=1 必须对应从底部数第二行');
assert.strictEqual(shape.at(2, 0), 'W', 'y=0 必须是文本最底行');
assert.strictEqual(Spatial.canPlace(map, 1, 1).ok, true, '可通行格必须允许摆放');
assert.strictEqual(Spatial.canPlace(map, 0, 1).ok, false, '墙格必须拒绝摆放');
assert.strictEqual(Spatial.canPlace(map, 7, 1).ok, false, '地图外必须拒绝摆放');
assert.strictEqual(Spatial.canPlace('WWWWW\nW...W\nWWWW\n', 1, 1).ok, false, '非矩形地图必须拒绝摆放');
assert.strictEqual(Spatial.canPlace('WWWWW\nW#..W\nW...W\nWWWWW', 1, 2).ok, false, '未知字符必须拒绝摆放');
assert.strictEqual(Spatial.canPlace('WWW\nW.W\nWWW', 1, 1).ok, false, '小于最小地图尺寸必须拒绝摆放');
// S250：与 Unity 画布一致，点位必须能从唯一 T 出生点走到。
const walled = 'WWWWWWW\nW..W..W\nW.TW..W\nW..W..W\nWWWWWWW';
assert.strictEqual(Spatial.canPlace(walled, 1, 1).ok, true, '与 T 连通的格必须允许摆放');
assert.strictEqual(Spatial.canPlace(walled, 4, 2).ok, false, '被墙隔开的可通行格必须拒绝摆放');
assert.match(Spatial.canPlace(walled, 4, 2).reason, /走不到/, '不可达拒绝需说明原因');
assert.strictEqual(Spatial.canPlace(walled.replace('T', '.'), 1, 1).ok, false, '没有 T 出生点时必须拒绝摆放');

const here = __dirname;
const app = fs.readFileSync(path.join(here, 'worldpark-app.js'), 'utf8');
const shell = fs.readFileSync(path.join(here, 'shell.html'), 'utf8');
assert.match(app, /new WorldParkSpatial\.Editor\(\)/, '世界工坊必须实例化空间编辑器');
assert.match(app, /WorldParkSpatial\.canPlace\(n\.mapText,x,y\)/, '画布新增必须校验当前地图的可通行格');
assert.match(app, /trigger:'interact'/, '调查点必须同步建立 interact 线索');
assert.match(app, /【待写草稿】请写玩家先观察到的事实。/, '新线索必须清楚标为待写草稿');
assert.match(app, /wpSpatialRefresh\(\)/, '表单坐标或地图变更必须刷新画布');
assert.match(shell, /\/\*WORLD_PARK_SPATIAL\*\//, 'shell必须为独立空间模块保留构建占位符');
assert.ok(shell.indexOf('/*WORLD_PARK_SPATIAL*/') < shell.indexOf('/*APP*/'), '空间模块必须先于worldpark-app执行');

// S258：鬼的巡逻格在图上点：点新格加入、点已有格去掉、墙和够不着的格拒绝、上限与 explore.js 相同。
{
  const room = Spatial.mapShape('WWWWWWW\nW.....W\nW..T..W\nW.....W\nWWWWWWW');
  const r0 = ['1,1'];
  const add = Spatial.routeToggle(room, r0, 5, 3);
  assert.strictEqual(add.ok, true, '可通行格加入路线');
  assert.deepStrictEqual(add.route, ['1,1', '5,3'], '新格加到末尾，格式 x,y');
  assert.deepStrictEqual(r0, ['1,1'], '不改原路线数组');
  const del = Spatial.routeToggle(room, add.route, 1, 1);
  assert.strictEqual(del.removed, true, '点已有格就去掉');
  assert.deepStrictEqual(del.route, ['5,3'], '去掉后其余顺序不变');
  assert.strictEqual(Spatial.routeToggle(room, ['1，1'], 1, 1).removed, true, '全角逗号写的格也认得');
  const wall = Spatial.routeToggle(room, [], 0, 0);
  assert.strictEqual(wall.ok, false, '墙格不能当巡逻格');
  assert.match(wall.reason, /不可通行/, '拒绝要说明原因');
  assert.strictEqual(Spatial.MAX_ROUTE, 32, '巡逻格上限与 explore.js MaxChaserRoute 相同');
  const explore = fs.readFileSync(path.join(here, 'explore.js'), 'utf8');
  assert.match(explore, /MaxChaserRoute: 32/, 'explore.js 上限仍为 32');
  const many = Array.from({ length:32 }, (_, k) => '1,' + (k % 3 + 1));
  assert.strictEqual(Spatial.routeToggle(room, many, 5, 1).ok, false, '满 32 格不能再加');
  assert.strictEqual(Spatial.routeToggle(room, null, 2, 2).ok, true, '没有路线时也能开始画');
}
console.log('worldpark-spatial: 34 assertions passed');
