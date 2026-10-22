'use strict';
const assert = require('assert');
const Spatial = require('./worldpark-spatial.js');
const Map = require('./worldpark-map.js');
let count = 0;
function truth(value, message) { count++; assert.ok(value, message); }
function equal(actual, expected, message) { count++; assert.strictEqual(actual, expected, message); }
function same(actual, expected, message) { count++; assert.deepStrictEqual(actual, expected, message); }
function match(actual, expected, message) { count++; assert.match(actual, expected, message); }

const header = '# 会场地图说明必须原样保留\n# 第二条说明\n';
const mapBody = [
  'ttttttt',
  't..f..t',
  't.....t',
  't.T...t',
  't.....t',
  'ttttttt'
].join('\n');
function makeNode() {
  return {
    id:'explore_a', kind:'explore', name:'保留字段测试', mapText:header + mapBody,
    points:[
      { id:'clue_a', kind:'clue', label:'调查', x:1, y:1, clue:'c1', visual:{ tone:'keep' } },
      { id:'exit_a', kind:'exit', label:'门', x:5, y:4, outcome:'win', requires:[] },
      { id:'ghost_a', kind:'chaser', label:'鬼', x:4, y:2, route:['3,2', '3,3'], speed:34, sight:7 }
    ],
    extra:{ keep:true, nested:{ value:9 } }
  };
}

truth(typeof Map.validate === 'function', '应公开 validate');
truth(typeof Map.resize === 'function', '应公开 resize');
truth(typeof Map.paint === 'function', '应公开 paint');
truth(typeof Map.Editor === 'function', '应公开 Editor');
const base = makeNode();
const snapshot = JSON.stringify(base);
const valid = Map.validate(base);
truth(valid.ok, '基础地图应有效');
equal(valid.impact.width, 7, '应读取地图宽度');
equal(valid.impact.height, 6, '应读取地图高度');
equal(valid.impact.routes, 2, '应统计巡逻格');
equal(JSON.stringify(base), snapshot, 'validate 必须纯粹且不改输入');

const bottom = Map.resize(base, 9, 8);
truth(bottom.ok, '左下锚点扩图应成功');
equal(JSON.stringify(base), snapshot, 'resize 成功也不能改输入');
truth(bottom.node.mapText.startsWith(header), '扩图必须原样保留头注释');
equal(bottom.node.points[0].x, 1, '左下锚点点位 x 不变');
equal(bottom.node.points[0].y, 1, '左下锚点点位 y 不变');
same(bottom.node.points[2].route, ['3,2', '3,3'], '左下锚点巡逻格不平移');
equal(Spatial.mapShape(bottom.node.mapText).at(2, 2), 'T', '左下锚点出生坐标不变');
equal(Spatial.mapShape(bottom.node.mapText).at(6, 3), '.', '右扩后旧右边围栏应打开');
equal(Spatial.mapShape(bottom.node.mapText).at(2, 5), '.', '上扩后旧上边围栏应打开');
equal(Spatial.mapShape(bottom.node.mapText).at(3, 4), 'f', '内层已有地形必须保持');
const bottomShape = Spatial.mapShape(bottom.node.mapText);
truth(Array.from({ length:bottomShape.width }, (_, x) => bottomShape.at(x, 0) === 't' && bottomShape.at(x, bottomShape.height - 1) === 't').every(Boolean), '扩图后上下外圈必须封闭为树');
truth(Array.from({ length:bottomShape.height }, (_, y) => bottomShape.at(0, y) === 't' && bottomShape.at(bottomShape.width - 1, y) === 't').every(Boolean), '扩图后左右外圈必须封闭为树');
truth(bottom.impact.opened > 0, '扩图影响应报告打开旧边界');
same(bottom.node.extra, base.extra, 'resize 不得丢失其它节点字段');

const centered = Map.resize(base, 9, 8, 'center');
truth(centered.ok, '居中扩图应成功');
same(centered.impact.moved, { x:1, y:1 }, '居中扩图应报告坐标平移');
equal(centered.node.points[0].x, 2, '居中扩图应平移点位 x');
equal(centered.node.points[0].y, 2, '居中扩图应平移点位 y');
same(centered.node.points[2].route, ['4,3', '4,4'], '居中扩图应平移巡逻格');
equal(Spatial.mapShape(centered.node.mapText).at(3, 3), 'T', '居中扩图应平移出生 T');
truth(centered.node.mapText.startsWith(header), '居中扩图也必须保留头注释');

const cropped = Map.resize(base, 5, 6);
truth(!cropped.ok, '缩宽裁掉出口时必须拒绝');
match(cropped.reason, /裁掉/, '裁切拒绝应说明原因');
equal(cropped.node, base, '失败 resize 应返回原输入引用');
const blockedShrink = Map.resize(base, 5, 6, 'center');
truth(!blockedShrink.ok, '新外圈会挡住出口时必须拒绝');
match(blockedShrink.reason, /挡住/, '挡住点位应说明原因');
equal(JSON.stringify(base), snapshot, '失败 resize 不能改输入');

const small = { mapText:['ttttttt', 't.....t', 't.....t', 't.T...t', 't.....t', 'ttttttt'].join('\n'), points:[], keep:'yes' };
const minimum = Map.resize(small, 5, 4);
truth(minimum.ok, '最小 5 乘 4 尺寸应允许');
equal(Spatial.mapShape(minimum.node.mapText).width, 5, '最小宽度应写入');
equal(Spatial.mapShape(minimum.node.mapText).height, 4, '最小高度应写入');
const maximum = Map.resize(small, 192, 128);
truth(maximum.ok, '最大 192 乘 128 尺寸应允许');
equal(Spatial.mapShape(maximum.node.mapText).width, 192, '最大宽度应写入');
equal(Spatial.mapShape(maximum.node.mapText).height, 128, '最大高度应写入');
truth(!Map.resize(small, 193, 128).ok, '超过最大宽度应拒绝');
truth(!Map.resize(small, 5, 3).ok, '低于最小高度应拒绝');
truth(!Map.resize(small, '5', 4).ok, '字符串尺寸参数应拒绝');
truth(!Map.resize(small, 5, 4, 'top').ok, '未知锚点应拒绝');

const narrow = {
  mapText:['ttttttt', 't..W..t', 't..W..t', 't.T...t', 't..W..t', 'ttttttt'].join('\n'),
  points:[{ id:'far', kind:'exit', label:'门', x:5, y:2, outcome:'win', requires:[] }]
};
truth(Map.validate(narrow).ok, '窄通道基础地图应有效');
const narrowBefore = JSON.stringify(narrow);
const cutPath = Map.paint(narrow, [{ x:3, y:2 }], 'W');
truth(!cutPath.ok, '阻断唯一通道且让点位不可达时必须拒绝');
match(cutPath.reason, /走不到|不可达/, '阻断拒绝应指出可达性');
equal(JSON.stringify(narrow), narrowBefore, '失败涂绘不能改输入');

const twoT = Map.paint(base, [{ x:1, y:2 }, { x:2, y:2 }], 'T');
truth(!twoT.ok, '一次涂两个出生点必须拒绝');
match(twoT.reason, /只能指定一个|多个 T/, 'T 唯一拒绝应说清楚');
const coverT = Map.paint(base, [{ x:2, y:2 }], '.');
truth(!coverT.ok, '普通笔刷不得覆盖出生 T');
const moveT = Map.paint(base, [{ x:3, y:2 }], 'T');
truth(moveT.ok, '移动出生 T 且全部点位仍可达时应成功');
equal(Spatial.mapShape(moveT.node.mapText).at(2, 2), '.', '移动出生时旧 T 必须还原为空地');
equal(Spatial.mapShape(moveT.node.mapText).at(3, 2), 'T', '移动出生时新位置必须为 T');
truth(Map.validate(moveT.node).ok, '移动出生后候选必须仍有效');
truth(!Map.paint(base, [{ x:0, y:2 }], '.').ok, '不得在外圈画可通行格');
truth(!Map.paint(base, [{ x:1, y:1 }], 'z').ok, '未知笔刷字符应拒绝');
truth(!Map.paint(base, [{ x:99, y:1 }], '=').ok, '越界格应拒绝');

// OW_TILES 中的既有其它地形应被接纳，但仍受空间规则与可达性限制。
global.OW_TILES = [{ c:'f', zh:'栅栏', rgb:[.7, .5, .2] }];
const otherTile = Map.paint(base, [{ x:5, y:1 }], 'f');
truth(otherTile.ok, 'OW_TILES 中的其它已知地形应允许涂绘');
equal(Spatial.mapShape(otherTile.node.mapText).at(5, 1), 'f', '其它地形应写入候选');
same(otherTile.node.points[2].route, base.points[2].route, 'paint 不得丢失巡逻路线');
same(otherTile.node.points[0].visual, { tone:'keep' }, 'paint 不得丢失点位其它字段');
equal(JSON.stringify(base), snapshot, '所有 paint 操作均不能原地改基础输入');

const invalidExisting = makeNode();
invalidExisting.mapText = invalidExisting.mapText.replace('ttttttt', 't.....t');
const noRepairByPaint = Map.paint(invalidExisting, [{ x:1, y:1 }], '=');
truth(!noRepairByPaint.ok, '已有无效地图不能由编辑操作静默修复');
match(noRepairByPaint.reason, /先修复/, '已有无效地图必须提示先修');
const noRepairByResize = Map.resize(invalidExisting, 8, 7);
truth(!noRepairByResize.ok, '已有无效地图不能先缩放再掩盖问题');
match(noRepairByResize.reason, /先修复/, 'resize 也必须提示先修');

console.log(`worldpark-map: ${count} assertions passed`);
