const src=require('fs').readFileSync('/home/user/workspace/repo/tools/LevelStudioWeb/app.js','utf8');
const i=src.indexOf('const owGroundRank'), j=src.indexOf('function owShadowW');
const owIsDoor=c=>c>='1'&&c<='9';
eval(src.slice(i,j).replace(/^const /gm,'var ').replace(/^function /gm,'function '));
const K=['TGrass','TPath','TWater','TMud',null]; const out=[];
for(let a=0;a<4;a++)for(let b=0;b<5;b++)for(let c=0;c<5;c++)for(let px=0;px<16;px+=3)for(let py=0;py<16;py+=5){out.push([K[a],K[b],K[c],K[(a+b)%5],K[(b+c)%5],a*7+b,c*3+1,px,py,owGroundPixelKey(K[a],K[b],K[c],K[(a+b)%5],K[(b+c)%5],a*7+b,c*3+1,px,py,16)]);}
require('fs').writeFileSync('/home/user/.opencode/skills/mariotrickster-continue/scripts/sim/gpk_web.json',JSON.stringify(out));console.log(out.length, out.filter(r=>r[9]!==r[0]).length);
