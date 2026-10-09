# S243: slice AI-generated sheets (magenta bg) -> 16x16 palette pixel art rows.
import numpy as np, json, sys
from PIL import Image
R='/home/user/workspace/art243/raw/'
PAL={'k':(0.1,0.09,0.12),'w':(1,1,1),'r':(0.9,0.2,0.22),'R':(0.6,0.1,0.12),'y':(1.0,0.86,0.25),'Y':(0.85,0.6,0.12),
'b':(0.3,0.6,0.95),'B':(0.16,0.34,0.7),'g':(0.4,0.75,0.35),'G':(0.2,0.45,0.2),'n':(0.62,0.42,0.22),'N':(0.4,0.26,0.14),
's':(0.7,0.69,0.66),'S':(0.42,0.41,0.4),'p':(0.72,0.45,1.0),'P':(0.45,0.22,0.7),'c':(0.55,0.9,1.0),'o':(0.95,0.6,0.18),'x':(0.05,0.04,0.04),
# S243 additions
'f':(0.98,0.78,0.6),'F':(0.78,0.52,0.38),'d':(0.14,0.2,0.42),'e':(0.25,0.25,0.3)}
keys=list(PAL); P=np.array([PAL[k] for k in keys])
def load(n):
    a=np.asarray(Image.open(R+n).convert('RGB')).astype(float)/255
    r,g,b=a[...,0],a[...,1],a[...,2]
    bg=(r>0.6)&(b>0.6)&(g<0.45)&(np.abs(r-b)<0.35)
    return a,~bg
def segs(proj,thr):
    on=proj>thr; out=[];s=None
    for i,v in enumerate(on):
        if v and s is None: s=i
        if not v and s is not None: out.append([s,i]); s=None
    if s is not None: out.append([s,len(on)])
    return out
def merge_to(sg,n):
    sg=[list(x) for x in sg if x[1]-x[0]>3]
    while len(sg)>n:
        gaps=[sg[i+1][0]-sg[i][1] for i in range(len(sg)-1)]; i=int(np.argmin(gaps)); sg[i]=[sg[i][0],sg[i+1][1]]; del sg[i+1]
    return sg
def boxes(n,rows,cols):
    a,m=load(n); out=[]
    rs=merge_to(segs(m.sum(1),2),rows)
    assert len(rs)==rows,(n,len(rs))
    for y0,y1 in rs:
        cs=merge_to(segs(m[y0:y1].sum(0),1),cols); assert len(cs)==cols,(n,len(cs))
        for x0,x1 in cs:
            sub=m[y0:y1,x0:x1]; ys=np.where(sub.any(1))[0]
            out.append((x0,y0+ys[0],x1,y0+ys[-1]+1))
    return a,m,out
def quant(rgb):
    d=((rgb[:,:,None,:]-P[None,None])**2*np.array([0.3,0.59,0.11])).sum(-1)
    return d.argmin(-1)
def downs(a,m,box,S,side,fill=None,anchor='bottom',cw=None):
    x0,y0,x1,y1=box; w,h=x1-x0,y1-y0
    sc=side/max(w,h) if fill is None else fill
    tw,th=max(1,round(w*sc)),max(1,round(h*sc))
    crop=np.dstack([a[y0:y1,x0:x1],m[y0:y1,x0:x1].astype(float)])
    im=Image.fromarray((crop*255).astype(np.uint8),'RGBA')
    # premultiplied box resample
    pm=crop.copy(); pm[...,:3]*=pm[...,3:4]
    imp=Image.fromarray((pm*255).astype(np.uint8),'RGBA').resize((tw,th),Image.BOX)
    q=np.asarray(imp).astype(float)/255; al=q[...,3]
    rgb=np.where(al[...,None]>0.01,q[...,:3]/np.maximum(al[...,None],1e-3),0)
    grid=[['.']*S for _ in range(S)]
    ox=(S-tw)//2; oy=S-th if anchor=='bottom' else (S-th)//2
    idx=quant(rgb)
    for y in range(th):
        for x in range(tw):
            if al[y,x]>=0.45 and 0<=oy+y<S and 0<=ox+x<S: grid[oy+y][ox+x]=keys[idx[y,x]]
    return grid
DARK=set('kxdNSRBGPe')
def luma(c): r,g,b=PAL[c]; return 0.299*r+0.587*g+0.114*b
def fix_outline(g,S=16):
    on=lambda x,y:0<=x<S and 0<=y<S and g[y][x]!='.'
    edge=[(x,y) for y in range(S) for x in range(S) if on(x,y) and not(on(x-1,y) and on(x+1,y) and on(x,y-1) and on(x,y+1))]
    dark=[e for e in edge if luma(g[e[1]][e[0]])<0.3]
    if edge and len(dark)/len(edge)<0.8:
        for x,y in edge:
            if luma(g[y][x])>=0.3: g[y][x]='k'
    return g
def audit(g,S=16):
    cells=[c for r in g for c in r if c!='.']; fill=len(cells)/(S*S)
    on=lambda x,y:0<=x<S and 0<=y<S and g[y][x]!='.'
    edge=[(x,y) for y in range(S) for x in range(S) if on(x,y) and not(on(x-1,y) and on(x+1,y) and on(x,y-1) and on(x,y+1))]
    dk=sum(luma(g[y][x])<0.3 for x,y in edge)/max(1,len(edge))
    return dict(colors=len(set(cells)),fill=round(fill,2),dark=round(dk,2))
out={'icons':{},'hero':[],'imp':[],'tiles':{},'audit':{}}
names={'propsA.png':(3,4,['FireTrap','ControllableBlocker','CollapsingPlatform','SpringPad','CrackFloor','IronCage','SnareTrap','OilBarrel','Tripwire','Vent','CrackedWall','Bush']),
'propsB.png':(2,5,['OneWayDoor','PoisonPool','Glue','RoomLamp','SpikeTrap','Cannon','PickupSpot','Crate','GrassGround','BananaPeel']),
'badges.png':(2,4,['BadgeLoot','BadgeExit','BadgeQuestion','BadgeAlert','BadgeEye','BadgeStar','BadgeAmbush','BadgeHide'])}
for f,(r,c,ks) in names.items():
    a,m,bx=boxes(f,r,c)
    for k,b in zip(ks,bx):
        flat=(b[2]-b[0])>(b[3]-b[1])*2
        g=downs(a,m,b,16,16 if flat else (14 if k in ('CrackFloor','CrackedWall','PickupSpot','Crate','GrassGround') else 15),anchor='bottom' if not k.startswith('Badge') else 'center')
        g=fix_outline(g); out['icons'][k]=[''.join(r) for r in g]; out['audit'][k]=audit(g)
for f,key in (('hero.png','hero'),('imp.png','imp')):
    a,m,bx=boxes(f,1,5)
    H=max(b[3]-b[1] for b in bx); W=max(b[2]-b[0] for b in bx); sc=15/max(H,W)
    for i,b in enumerate(bx):
        g=fix_outline(downs(a,m,b,16,15,fill=sc,anchor='bottom'))
        out[key].append([''.join(r) for r in g]); out['audit'][f'{key}{i}']=audit(g)
a,m,bx=boxes('tiles.png',2,3)
for k,b in zip(['GroundTop','GroundFill','Wall','Platform','StoneTop','MossWall'],bx):
    x0,y0,x1,y1=b
    th=5 if k=='Platform' else 16
    crop=Image.fromarray((a[y0:y1,x0:x1]*255).astype(np.uint8)).resize((16,th),Image.BOX)
    idx=quant(np.asarray(crop).astype(float)/255)
    out['tiles'][k]=[''.join(keys[v] for v in row) for row in idx]
# background: 64x36, 8 own colors, low contrast
bg=Image.open(R+'bg.png').convert('RGB').resize((64,36),Image.BOX)
arr=np.asarray(bg).astype(float)/255; mean=arr.mean((0,1)); arr=(arr-mean)*1.3; arr=arr+np.array([0.27,0.27,0.37])  # mid value, low contrast: black outlines of the main layer pop
q=Image.fromarray((np.clip(arr,0,1)*255).astype(np.uint8)).quantize(8,method=Image.MEDIANCUT)
pal=np.array(q.getpalette()[:24]).reshape(8,3)/255; ix=np.asarray(q)
out['bg']={'pal':[[round(v,3) for v in p] for p in pal],'rows':[''.join(str(v) for v in r) for r in ix]}
json.dump(out,open('/home/user/workspace/art243/art.json','w'),ensure_ascii=False,indent=0)
for k,v in out['audit'].items(): print(k,v)
