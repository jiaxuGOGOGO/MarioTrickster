# 按游戏里的规则把默认房间拼成一张"截图"（沙盒没有 Unity，用同一份 art.json + 同样的缩放规则）
import json,sys,re
sys.path.insert(0,'/home/user/workspace/tools_s243')
from PIL import Image, ImageDraw
from slice import PAL
d=json.load(open('/home/user/workspace/art243/art.json'))
room=[l for l in open('/home/user/workspace/sim/room_template.txt').read().split('\n') if l]
# 解析一个布局：1->c 2->b 3->~
room=[r.replace('1','c').replace('2','b').replace('3','~') for r in room]
reg={}
s=open('/home/user/workspace/repo/Assets/Scripts/LevelDesign/AsciiElementRegistry.cs',encoding='utf-8').read()
for m in re.finditer(r"asciiChar\s*=\s*'(.)',\s*elementName\s*=\s*\"([^\"]*)\".*?visualScale\s*=\s*(Vector2\.one|new Vector2\(([\d.]+)f?,\s*([\d.]+)f?\))",s,re.S):
    sx=float(m.group(4)) if m.group(4) else 1; sy=float(m.group(5)) if m.group(5) else 1
    reg[m.group(1)]=(m.group(2),sx,sy)
C=int(sys.argv[1]) if len(sys.argv)>1 else 3
P=16*C
H=len(room); W=max(len(r) for r in room)
img=Image.new('RGB',(W*P,H*P),(41,41,56))
# 背景
bp=d['bg']['pal']; br=d['bg']['rows']
bw,bh=len(br[0]),len(br); k=max((W+2)/bw,(H+2)/bh)*P
for y,r in enumerate(br):
    for x,c in enumerate(r):
        col=tuple(int(v*255) for v in bp[int(c)]) if c.isdigit() else None
        if col is None: continue
        x0=(W*0.5-0.5 - bw*k/P/2 + x*k/P +0.5)*P; y0=(H*0.5-0.5 - bh*k/P/2 + y*k/P+0.5)*P
        ImageDraw.Draw(img).rectangle([x0,y0,x0+k,y0+k],fill=col)
def blit(rows,cx,cy,scale,flip=False,lift=0):
    # cx,cy 单元格中心（屏幕坐标，y 向下），scale = 格
    pix=P*scale/16
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c=='.': continue
            xx=(15-x) if flip else x
            col=tuple(int(v*255) for v in PAL[c])
            x0=cx*P+P/2-8*pix+xx*pix; y0=cy*P+P/2-8*pix+y*pix-lift*P
            ImageDraw.Draw(img).rectangle([x0,y0,x0+pix-0.01,y0+pix-0.01],fill=col)
def blitp(rows,cx,cy):
    pix=P/16
    for y,r in enumerate(rows):
        for x,c in enumerate(r):
            if c=="." : continue
            col=tuple(int(v*255) for v in PAL[c]); x0=cx*P+x*pix; y0=cy*P+P*0.35+y*pix
            ImageDraw.Draw(img).rectangle([x0,y0,x0+pix-0.01,y0+pix-0.01],fill=col)
solid=lambda ch: ch in '#=WCxv'
for y,r in enumerate(room):
    for x,ch in enumerate(r):
        if ch in '#=W v'.replace(' ','') or ch=='-':
            air = y==0 or not solid(room[y-1][x]) if x<len(room[y-1]) else True
            key='Wall' if ch=='W' else 'Platform' if ch=='-' else ('GroundTop' if air else 'GroundFill')
            if ch=='v': key=None
            if key: (blit(d['tiles'][key],x,y,1) if key!='Platform' else blitp(d['tiles'][key],x,y))
        if ch in reg and reg[ch][0] in d['icons'] and ch not in '#=W-Go':
            n,sx,sy=reg[ch]; s=max(min(max(sx,sy),1),0.6) if n not in('GoalZone','Collectible') else 1; lift=(s-sy)*0.5
            blit(d['icons'][n],x,y,s,flip=(ch=='k'),lift=lift)
        if ch=='M': blit(d['hero'][0],x,y,1)
        if ch=='T': blit(d['imp'][0],x,y,1)
        if ch=='G': blit(d['icons']['GoalZone'],x,y,1)
        if ch=='o': blit(d['icons']['Collectible'],x,y,0.9,lift=-0.05)
img.save(sys.argv[2] if len(sys.argv)>2 else '/home/user/workspace/tools_s244/room_now.png'); print(img.size)
