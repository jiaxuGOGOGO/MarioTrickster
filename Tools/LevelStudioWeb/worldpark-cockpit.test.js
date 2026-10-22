const assert=require('assert'),fs=require('fs'),path=require('path'),C=require('./worldpark-cockpit.js');
const pack=JSON.parse(fs.readFileSync(path.join(__dirname,'../../Assets/Resources/WorldPark/Borderland.worldpark.json'),'utf8').replace(/^\uFEFF/,''));
const before=JSON.stringify(pack);let n=0;const ok=(v,t)=>{assert.ok(v,t);n++;};
for(const v of pack.venues){const e=C.edges(v);for(const x of e){ok(v.nodes.some(n=>n.id===x.from),'分支起点来自当前会场');ok(v.nodes.some(n=>n.id===x.to),'分支终点来自当前会场');}
for(const node of v.nodes){const got=e.filter(x=>x.from===node.id);if(['explore','game','room'].includes(node.kind)){ok(got.length===2,'活动必须展示成功/失败两条实际分支');ok(got.some(x=>x.to===node.onWin&&x.label==='成功'),'成功分支正确');ok(got.some(x=>x.to===node.onLose&&x.label==='失败'),'失败分支正确');}if(node.kind==='ending')ok(!got.length,'结局不虚构下一阶段');ok(C.resolve(pack,v.id,node.id)?.ni===v.nodes.indexOf(node),'按稳定ID导航');}
const ds=C.destinations(pack,v);ok(ds.length===pack.links.filter(l=>l.from===v.id).length,'门只展示当前会场实际链接');}
ok(JSON.stringify(pack)===before,'模型不修改世界');ok(C.resolve(pack,'bad')===null,'缺失会场拒绝');ok(C.resolve(pack,pack.venues[0].id,'bad')===null,'缺失阶段拒绝');ok(C.icon('map').includes('aria-hidden="true"'),'图标由中文按钮名解释');ok(C.icon('unknown').includes('<svg'),'未知图标安全备用');
const h=fs.readFileSync(path.join(__dirname,'shell.html'),'utf8');for(const id of ['pageCockpit','wcHost','wcMapDialog','wcMapEditor','wcDock','wcRoomReturn','wcChildNote'])ok(h.includes('id="'+id+'"'),'界面入口存在 '+id);
console.log('worldpark-cockpit: '+n+' assertions passed');
