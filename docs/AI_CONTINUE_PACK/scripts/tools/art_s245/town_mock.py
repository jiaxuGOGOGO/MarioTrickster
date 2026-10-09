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
for y,row in enumerate(MAP):
    for x,c in enumerate(row): img.alpha_composite(spr(GROUND(c)),(x*P,(y+peek)*P))
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
img=img.resize((img.width*3,img.height*3),Image.NEAREST); img.save('/home/user/workspace/art245/town_mock.png'); print(img.size)
