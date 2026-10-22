# S245 town mock: renders a sample town using the same rules as OverworldGame art mode (WorldArt data, 16px/cell, y-sorted sprites, far/near parallax strips).
import re,numpy as np
from PIL import Image
src=open('/home/user/workspace/tools_s243/slice.py').read(); ns={}; exec(src[:src.index('def load')],ns); PAL=ns['PAL']
def dicts(path):
    s=open(path).read(); out={}
    for m in re.finditer(r'\{ "([A-Za-z0-9]+)", new\[\] \{(.*?)\} \}',s,re.S):
        rows=re.findall(r'"([^"]*)"',m.group(2))
        if rows and all(len(r)==len(rows[0]) for r in rows): out[m.group(1)]=rows
    return out,s
W,ws=dicts('/home/user/workspace/repo/Assets/Scripts/Overworld/WorldArt.cs')
A,_=dicts('/home/user/workspace/repo/Assets/Scripts/Gameplay/Step1/Step1Art.cs'); W.update({k:v for k,v in A.items() if k not in W})
def strip(name):
    pal=re.search(name+r'Palette =\s*\{(.*?)\};',ws,re.S).group(1); pal=[[float(x.rstrip('f')) for x in re.findall(r'[0-9.]+f',p)] for p in re.findall(r'new\[\] \{([^}]*)\}',pal)]
    rows=re.findall(r'"([^"]*)"',re.search(name+r'Strip =\s*\{(.*?)\};',ws,re.S).group(1))
    h,w=len(rows),len(rows[0]); a=np.zeros((h,w,4),np.uint8)
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c!='.': i=int(c,36); a[y,x]=[*(int(v*255) for v in pal[i][:3]),255]
    return Image.fromarray(a)
def spr(k):
    rows=W[k]; h,w=len(rows),len(rows[0]); a=np.zeros((h,w,4),np.uint8)
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c!='.' and c in PAL: a[y,x]=[*(int(v*255) for v in PAL[c][:3]),255]
    return Image.fromarray(a)
MAP=[ # row 0 = north (top)
"AAAA^^......tt....t.....AAAA",
"AhAA^..tt.........tt....AAA^",
"^^^....WWWWW...t....WWWW..^.",
"..t....WWWWW......t.WWWW....",
"....t..WW1WW..ff....W2WW..t.",
".......==.=====ff===.=......",
"..\"\"\"..==....=......=..ww..",
".\"\"\"\"..=..t..=.tt...=.www..",
"..\"\"...=......=....==.ww.t.",
"...t...======M======........",
"..........g..=..............",
"..WWWW...ggg.=....t...WWWWW.",
"..WWWW....g..=........WWWWW.",
"..WW3W.......=..T.....WW4WW.",
"....=========.=============.",
"...t.....t.........t.....t..",
]
MAP=[r.ljust(28,".")[:28] for r in MAP]
GROUND=lambda c:'TPath' if c in '=MT1234' else 'TWater' if c=='w' else 'TMud' if c=='g' else 'TGrass'
P=16; H=len(MAP); Wd=len(MAP[0]); peek=4
img=Image.new('RGBA',(Wd*P,(H+peek)*P),(140,189,237,255))
far,near=strip('Far'),strip('Near')
def tile_strip(s,y_base,sc,off):
    s=s.resize((s.width*sc,s.height*sc),Image.NEAREST)
    x=-off
    while x<img.width: img.alpha_composite(s,(x,y_base-s.height)) if x>=0 else img.alpha_composite(s.crop((-x,0,s.width,s.height)),(0,y_base-s.height)); x+=s.width
tile_strip(far,peek*P+8,3,40); tile_strip(near,peek*P+12,3,90)
import subprocess,json as _j
# S246: transitions via the web JS function (same as C#, sim cross-checked)
def gpk_all():
    reqs=[]
    for y,row in enumerate(MAP):
        for x,c in enumerate(row):
            cy=H-1-y; nb=lambda xx,yy: None if xx<0 or yy<0 or xx>=Wd or yy>=H else GROUND(MAP[H-1-yy][xx])
            reqs.append([GROUND(c),nb(x,cy+1),nb(x+1,cy),nb(x,cy-1),nb(x-1,cy),x,cy])
    js=open('/home/user/workspace/repo/tools/LevelStudioWeb/app.js').read(); i=js.index('const owGroundRank'); j=js.index('function owShadowW')
    code="const owIsDoor=c=>c>='1'&&c<='9';"+js[i:j]+"const R="+_j.dumps(reqs)+";const out=R.map(r=>{const a=[];for(let py=0;py<16;py++){const row=[];for(let px=0;px<16;px++)row.push(owGroundPixelKey(r[0],r[1],r[2],r[3],r[4],r[5],r[6],px,py,16));a.push(row)}return a});console.log(JSON.stringify(out))"
    return _j.loads(subprocess.run(['node','-e',code],capture_output=True,text=True).stdout)
G=gpk_all(); k=0
cache={}
for y,row in enumerate(MAP):
    for x,c in enumerate(row):
        keys=G[k]; k+=1; tile=Image.new('RGBA',(P,P))
        for py in range(P):
            for px in range(P):
                kk=keys[py][px]
                col=(196,228,248,255) if kk=='Foam' else (cache.setdefault(kk,spr(kk)).getpixel((px,P-1-py)))
                tile.putpixel((px,P-1-py),col)
        img.alpha_composite(tile,(x*P,(y+peek)*P))
# shadows
from PIL import ImageDraw
sh=Image.new('RGBA',img.size,(0,0,0,0)); sd=ImageDraw.Draw(sh)
SW={'t':1.1,'A':1.2,'f':0.9,'M':0.9,'T':0.9}
for y,row in enumerate(MAP):
    for x,c in enumerate(row):
        below=MAP[y+1][x] if y+1<H else '.'
        w=SW.get(c,0)
        if c=='W' and below not in 'W1234': w=1.05
        if w: cx=(x+0.58)*P; cy=(y+peek+0.88)*P; sd.ellipse((cx-w*P/2,cy-w*P/4,cx+w*P/2,cy+w*P/4),fill=(0,0,0,97))
for (yy,xx) in ((7.4,12.3),(8.6,9.6)): cx=(xx+0.5)*P; cy=(yy+peek+0.9)*P; sd.ellipse((cx-0.35*P,cy-0.17*P,cx+0.35*P,cy+0.17*P),fill=(0,0,0,97))
img.alpha_composite(sh)
items=[]
for y,row in enumerate(MAP):
    for x,c in enumerate(row):
        below=MAP[y+1][x] if y+1<H else '.'
        k={'W':'TWall' if below not in 'W1234' else 'TRoof','t':'TTree','f':'TFence','"':'TTallGrass','A':'TMountain','^':'THill','h':'TCave','M':'THome','T':'TSpawn'}.get(c)
        if c in '1234': k='TDoor'
        if not k: continue
        sc={'TTree':1.5,'TMountain':1.4,'TDoor':1.1,'THome':1.1,'TSpawn':1.1}.get(k,1.0)
        items.append((y,k,x,sc))
items.append((7.4,'Hero0',12.3,1.0)); items.append((8.6,'Imp0',9.6,1.0))
for y,k,x,sc in sorted(items,key=lambda t:(t[1]=='TTallGrass',t[0])):
    s=spr(k); n=int(16*sc); s=s.resize((n,n),Image.NEAREST)
    yy=int((y+peek)*P+P-n-(4 if k=='TTree' else 0)); xx=int(x*P+P/2-n/2)
    img.alpha_composite(s,(xx,yy))
img=img.resize((img.width*3,img.height*3),Image.NEAREST); img.save('/home/user/workspace/art245/town_mock_s246.png'); print(img.size)
