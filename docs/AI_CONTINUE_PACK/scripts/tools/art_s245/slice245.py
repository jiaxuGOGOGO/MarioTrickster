# S245: slice town / decor / fx sheets + parallax strips -> art245/art.json
import sys, json, numpy as np
sys.path.insert(0,'/home/user/workspace/tools_s243')
import importlib.util
spec=importlib.util.spec_from_file_location('sl','/home/user/workspace/tools_s243/slice.py')
src=open('/home/user/workspace/tools_s243/slice.py').read(); src=src[:src.index('out={')]  # functions only, no side effects
ns={}; exec(src,ns)
ns['R']='/home/user/workspace/art245/raw/'
from PIL import Image
boxes,downs2,fix_outline,audit,keys=ns['boxes'],ns['downs2'],ns['fix_outline'],ns['audit'],ns['keys']
def g_boxes(f,r,c):
    ns['R']='/home/user/workspace/art245/raw/'; return boxes(f,r,c)
out={'town':{},'decor':{},'fx':{},'elems':{},'fixes':{},'audit':{},'strips':{}}
sheets={'town.png':('town',4,4,['TGrass','TPath','TRoof','TWall','TTree','TWater','TFence','TTallGrass','TMud','TMountain','THill','TCave','TDoor','THome','TSpawn','TSign']),
'decor.png':('decor',3,4,['DecoPainting','DecoShelf','DecoCandle','DecoPlant','DecoClock','DecoWindow','DecoRug','DecoBanner','DecoArmor','DecoBarrels','DecoChandelier','DecoWeb']),
'elems.png':('elems',3,4,['PendulumTrap','SawBlade','StateQueueTrap','BouncyPlatform','MovingPlatform','ConveyorBelt','BreakableBlock','FakeWall','HiddenPassage','BouncingEnemy','FlyingEnemy','_brick']),
'fixes.png':('fixes',2,4,['BananaPeel2','ControllableBlocker2','Tripwire2','Collectible2','FxRain2','FxDrop','FxCannonball2','FakeWall']),
'fx.png':('fx',3,4,['FxRain','FxSplash','FxAcid','FxLeaf','FxFog','FxSnow','FxBomb','FxCannonball','FxTaunt','FxMound','FxHook','FxStormCloud'])}
FULL={'TGrass','TPath','TRoof','TWall','TWater','TMud'}
for f,(grp,r,c,ks) in sheets.items():
    a,m,bx=g_boxes(f,r,c)
    for k,b in zip(ks,bx):
        if k.startswith('_'): continue
        if k in FULL:
            # ground-like tiles: fill the whole 16x16 cell (crop the inner square so it tiles)
            x0,y0,x1,y1=b; s=min(x1-x0,y1-y0); cx,cy=(x0+x1)//2,(y0+y1)//2; s=int(s*0.8)
            bb=(cx-s//2,cy-s//2,cx+s//2,cy+s//2)
            g=downs2(a,np.ones_like(m),bb,16,anchor='center')
        else:
            flat=(b[2]-b[0])>(b[3]-b[1])*2
            side=16 if flat else 15
            g=downs2(a,m,b,side,anchor='center' if grp=='fx' else 'bottom')
            g=fix_outline(g)
        out[grp][k]=[''.join(r) for r in g]; out['audit'][k]=audit(g)
# parallax strips: own 4-colour palette, magenta = transparent, 160 x 40
for f,W,H in (('far.png',160,40),('near.png',160,40)):
    im=Image.open(ns['R']+f).convert('RGB'); a=np.asarray(im).astype(float)/255
    r,g,b=a[...,0],a[...,1],a[...,2]; bg=(r>0.6)&(b>0.6)&(g<0.45)
    rows=np.where((~bg).mean(1)>0.02)[0]; top=max(0,rows.min()-4)
    a=a[top:]; bg=bg[top:]
    h=a.shape[0]; w=a.shape[1]; Hh=max(8,round(W*h/w))
    rgb=Image.fromarray((a*255).astype(np.uint8)).resize((W,Hh),Image.NEAREST)
    msk=np.asarray(Image.fromarray((~bg*255).astype(np.uint8)).resize((W,Hh),Image.BOX))/255>0.5
    q=rgb.quantize(5,method=Image.MEDIANCUT); pal=np.array(q.getpalette()[:15]).reshape(5,3)/255; ix=np.asarray(q)
    # drop palette entries that are magenta
    rowsS=[''.join(str(ix[y][x]) if msk[y][x] and not (pal[ix[y][x]][0]>0.6 and pal[ix[y][x]][2]>0.6 and pal[ix[y][x]][1]<0.45) else '.' for x in range(W)) for y in range(Hh)]
    out['strips'][f[:-4]]={'pal':[[round(v,3) for v in p] for p in pal],'rows':rowsS}
    print(f,W,Hh)
json.dump(out,open('/home/user/workspace/art245/art.json','w'),ensure_ascii=False,indent=0)
for k,v in out['audit'].items(): print(k,v)
